using System.Net.Sockets;
using System.Text;

namespace Kumunita.Core.Identity;

/// <summary>
/// The probe core behind <see cref="SmtpHealthCheck"/>: the exact delivery
/// preflight the durable <c>OutboxEmail</c> handler runs — TCP connect,
/// <c>220</c> banner, EHLO, and (when <see cref="SmtpOptions.User"/>/
/// <see cref="SmtpOptions.Pass"/> are configured) <c>AUTH</c> — so a
/// "healthy" reading is a real guarantee email delivery works, not just
/// that a relay answers its greeting. Split out of the seam so tests can
/// drive it with a loopback fake SMTP server.
/// <para>
/// Deliberate choice over <see cref="System.Net.Mail.SmtpClient"/>: a
/// raw send-probe would actually transmit a message on a live relay, and
/// <c>SmtpClient</c>'s handshake internals aren't exposed. The commands here
/// (greeting + EHLO + AUTH) are exactly what <see cref="SmtpSender.SendAsync"/>
/// does before <c>MAIL FROM</c>, so the coverage is equivalent with zero side
/// effects — no email is dispatched on a probe pass.
/// <para>
/// The <c>Tls</c> handshake is <em>not</em> part of the check: <see cref="SmtpOptions"/>
/// documents that both <c>Tls</c> (STARTTLS) and <c>None</c> (Mailpit, local relay)
/// are legitimate shapes, and the unencrypted banner is the protocol-defined first
/// message on both (RFC 5321).
/// <para>
/// Unconfigured shape (empty <see cref="SmtpOptions.Host"/>): returns
/// <c>false</c> without any I/O — the same "degraded" signal a down relay
/// reports, and the controller's response already distinguishes the two
/// causes for the operator (OPS &#167;7). The "exactly one or zero" credential
/// invariant <see cref="SmtpSender"/> enforces applies here too: exactly one
/// of <c>User</c>/<c>Pass</c> set is a configuration error the real send would
/// fail on, so the probe reports unavailable rather than half-proving anything.
/// </summary>
internal static class SmtpProbe
{
    private const string ProbeDomain = "health.kumunita";
    private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Runs the full delivery preflight (greeting, EHLO, and AUTH when
    /// credentials are configured) against <paramref name="options"/> and
    /// returns true only if every step succeeds or completes within one
    /// shared timeout window.
    /// </summary>
    public static async Task<bool> TryHandshakeAsync(SmtpOptions options, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
            return false;

        bool hasUser = !string.IsNullOrWhiteSpace(options.User);
        bool hasPass = !string.IsNullOrWhiteSpace(options.Pass);
        if (hasUser != hasPass)
            return false;   // exactly-one-or-zero invariant (SmtpSender's, mirrored so the probe and the send can't disagree)

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(
                Environment.GetEnvironmentVariable("KUMUNITA_SMTP_HEALTH_TIMEOUT_MS") is { } msStr && int.TryParse(msStr, out var ms)
                    ? TimeSpan.FromMilliseconds(ms)
                    : DefaultHandshakeTimeout);

            await socket.ConnectAsync(options.Host, options.Port, linked.Token).ConfigureAwait(false);

            // One reader/writer pair over a single shared stream — SMTP over TCP
            // is a bidirectional line protocol; two independent streams each hold
            // a buffering layer that would starve the other side of backpressure.
            using var stream = new NetworkStream(socket, ownsSocket: false);
            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
            using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };

            // 220 banner: any 2xx greeting line (RFC 5321 &#167;4.2).
            string banner = await ReadReplyAsync(reader, linked.Token).ConfigureAwait(false);
            if (!banner.StartsWith("2", StringComparison.Ordinal))
                return false;

            // EHLO advertises the capabilities (incl. AUTH, if it does any at
            // all); a 250 here proves the relay is actually speaking SMTP, not
            // just a generic TCP listener on this port.
            writer.WriteLine($"EHLO {ProbeDomain}");
            string ehlo = await ReadReplyAsync(reader, linked.Token).ConfigureAwait(false);
            if (!ehlo.StartsWith("250", StringComparison.Ordinal))
                return false;

            if (!hasUser)
                return true;   // no-auth shape (Mailpit / localhost relay) — greeting + EHLO is everything a real send needs

            // AUTH — only PLAIN or LOGIN are in scope: those are the two BCL
            // actually supports, and they're the only shapes SmtpSender's
            // NetworkCredential path ever speaks. Any other advertised mechanism
            // would fail a real send with "Unsupported AUTH", so the probe
            // reports unavailable rather than pretending the handshake proved
            // anything.
            string? mechanism = ParseAdvertisedAuthMechanism(ehlo);
            if (mechanism is null)
                return false;   // relay doesn't advertise AUTH; credentials would be useless

            // RFC 4954: PLAIN carries base64("\0 user \0 pass") inline on the
            // AUTH line — a correct 235 is the whole proof in that one round
            // trip (which is exactly the shape BCL's SmtpClient emits).
            if (mechanism.Equals("PLAIN", StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteLine($"AUTH PLAIN {ComputePlainToken(options.User!, options.Pass!)}");
                string plainReply = await ReadReplyAsync(reader, linked.Token).ConfigureAwait(false);
                return plainReply.StartsWith("235", StringComparison.Ordinal);
            }

            // RFC 4954: LOGIN is two 334 challenges (user, then password).
            if (mechanism.Equals("LOGIN", StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteLine("AUTH LOGIN");
                string first = await ReadReplyAsync(reader, linked.Token).ConfigureAwait(false);
                if (!first.StartsWith("334", StringComparison.Ordinal))
                    return false;

                writer.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(options.User!)));
                string second = await ReadReplyAsync(reader, linked.Token).ConfigureAwait(false);
                if (!second.StartsWith("334", StringComparison.Ordinal))
                    return false;

                writer.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(options.Pass!)));
                string final = await ReadReplyAsync(reader, linked.Token).ConfigureAwait(false);
                return final.StartsWith("235", StringComparison.Ordinal);
            }

            return false;   // advertised a mechanism BCL can't drive — same dead-letter outcome a real send would have.
        }
        catch (OperationCanceledException)
        {
            // Timeout (linked CancelAfter) or caller cancellation — both are "down"
            // for health-probe purposes.
            return false;
        }
        catch
        {
            // DNS failure, connection refused, a reset mid-handshake — all the
            // same operator signal: the relay is not delivering.
            return false;
        }
    }

    /// <summary>
    /// RFC 4954 &#167;2: PLAIN's inline initial-response is
    /// <c>base64("\0" username "\0" password)</c>.
    /// </summary>
    private static string ComputePlainToken(string user, string pass)
    {
        var bytes = new List<byte>(2 + user.Length + pass.Length);
        bytes.Add(0);
        bytes.AddRange(Encoding.UTF8.GetBytes(user));
        bytes.Add(0);
        bytes.AddRange(Encoding.UTF8.GetBytes(pass));
        return Convert.ToBase64String(bytes.ToArray());
    }

    /// <summary>
    /// Reads one SMTP reply: the first line, plus any continuation lines marked
    /// with <c>xxx-</c> (RFC 5321 multi-line format), up to and including the
    /// terminating <c>xxx&nbsp;</c> line.
    /// </summary>
    private static async Task<string> ReadReplyAsync(StreamReader reader, CancellationToken ct)
    {
        var lines = new List<string>(4);
        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            lines.Add(line);
            // A line whose 4th character is not a dash ends the response.
            if (line.Length < 4 || line[3] != '-')
                break;
        }

        if (lines.Count == 0)
            throw new IOException(
                "The relay closed the connection before sending a full reply.",
                new TimeoutException());

        return string.Join("\r\n", lines);
    }

    /// <summary>
    /// Extracts the first AUTH mechanism advertised on a capability line (e.g.
    /// <c>250-AUTH PLAIN LOGIN</c> yields <c>PLAIN</c>), or <c>null</c> when the
    /// relay advertises no AUTH at all (its Mailpit / local-relay shape).
    /// </summary>
    private static string? ParseAdvertisedAuthMechanism(string ehloReply)
    {
        foreach (string rawLine in ehloReply.Split(new[] { "\r\n" }, StringSplitOptions.None))
        {
            string line = rawLine.Trim();
            int authIdx = line.IndexOf("AUTH ", StringComparison.OrdinalIgnoreCase);
            if (authIdx < 0)
                continue;

            string tail = line[(authIdx + "AUTH ".Length)..].Trim();
            int space = tail.IndexOf(' ');
            string mechanism = space < 0 ? tail : tail[..space];
            if (string.IsNullOrEmpty(mechanism))
                return null;

            return mechanism;
        }

        return null;
    }
}
