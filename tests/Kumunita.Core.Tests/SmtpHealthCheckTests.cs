using System.Net;
using System.Net.Sockets;
using System.Text;
using Kumunita.Core.Identity;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Tests;

/// <summary>
/// Unit tests for <see cref="SmtpHealthCheck"/> (OPS §8 — the /health
/// mail-reachability seam):
/// <list type="bullet">
/// <item>the unconfigured shape (empty host) must report unreachable rather than throw,</item>
/// <item>a live handshake against a fake SMTP listener must report reachable,</item>
/// <item>the plaintext-AUTH path (Secure=None) accepts and rejects PLAIN/LOGIN correctly,</item>
/// <item>the STARTTLS path (Secure=Tls — the default) names its diagnostic when the
/// relay does not advertise STARTTLS, or refuses the upgrade,</item>
/// <item>each failure exposes the step that broke plus the relay's own reply
/// (e.g. 535, 421, 535), so /health is actionable without digging through logs.</item>
/// </list>
/// </summary>
public class SmtpHealthCheckTests
{
    [Fact]
    public Task CheckAsync_When_HostUnconfigured_Returns_False()
    {
        var check = new SmtpHealthCheck(Options.Create(new SmtpOptions()));

        return CheckFalse(check.CheckAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckAsync_When_RelayAnswersHandshake_Plain_Returns_True()
    {
        var relay = FakeSmtpRelay.Start();   // default: NoAuth mode → banner + EHLO (no STARTTLS)
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port,
                Secure = SmtpOptions.SecureNone   // plain relay — the Mailpit / localhost shape
            }));

            Assert.True((await check.CheckAsync(TestContext.Current.CancellationToken)).Reachable);
        }
    }

    [Fact]
    public async Task CheckAsync_When_NothingListening_Returns_False()
    {
        // Reserving (and immediately releasing) a port makes a subsequent
        // connect to it refused — the "relay down" shape.
        int port = ReserveFreePort();
        var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = port
        }));

        var result = await check.CheckAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Reachable);
        // The diagnostic must name the target host:port and the underlying
        // socket error so /health is actionable.
        Assert.Contains($"127.0.0.1:{port}", result.Reason);
        Assert.Contains("SocketException", result.Reason);
    }

    /// <summary>
    /// Reserves an OS-assigned TCP port and releases it immediately — any
    /// subsequent connect to the same port is refused (the "relay down"
    /// shape the /health probe is meant to detect).
    /// </summary>
    private static int ReserveFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int assigned = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return assigned;
    }

    [Fact]
    public async Task CheckAsync_When_RelayAdvertisesAuthPlain_Accepts_Returns_True()
    {
        var relay = FakeSmtpRelay.Start(FakeSmtpRelay.Mode.AuthPlainAccept);
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port,
                User = "kumunita@kumunita",
                Pass = "relay-secret",
                Secure = SmtpOptions.SecureNone   // plaintext AUTH path
            }));

            Assert.True((await check.CheckAsync(TestContext.Current.CancellationToken)).Reachable);
        }
    }

    [Fact]
    public async Task CheckAsync_When_RelayAdvertisesAuthPlain_Rejects_Returns_False()
    {
        var relay = FakeSmtpRelay.Start(FakeSmtpRelay.Mode.AuthPlainReject);
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port,
                User = "wrong-user",
                Pass = "wrong-pass",
                Secure = SmtpOptions.SecureNone
            }));

            var result = await check.CheckAsync(TestContext.Current.CancellationToken);
            Assert.False(result.Reachable);
            // Diagnostic must name the step: AUTH rejection, plus the relay's
            // own 535 reply, so an operator reading /health sees exactly why
            // the credentials were rejected.
            Assert.Contains("AUTH", result.Reason);
            Assert.Contains("535", result.Reason);
        }
    }

    [Fact]
    public async Task CheckAsync_When_RelayAdvertisesAuthLogin_Accepts_Returns_True()
    {
        var relay = FakeSmtpRelay.Start(FakeSmtpRelay.Mode.AuthLoginAccept);
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port,
                User = "kumunita@kumunita",
                Pass = "relay-secret",
                Secure = SmtpOptions.SecureNone
            }));

            Assert.True((await check.CheckAsync(TestContext.Current.CancellationToken)).Reachable);
        }
    }

    [Fact]
    public async Task CheckAsync_When_RelayAdvertisesOnlyUnsupportedMechanism_Returns_False()
    {
        // The relay advertises AUTH CRAM-MD5 — the BCL (and therefore SmtpSender)
        // can't drive it, so the probe must not pass the credentials through
        // even if it could connect at all.
        var relay = FakeSmtpRelay.Start(FakeSmtpRelay.Mode.AuthAdsNoUsableMech);
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port,
                User = "kumunita@kumunita",
                Pass = "relay-secret",
                Secure = SmtpOptions.SecureNone
            }));

            var result = await check.CheckAsync(TestContext.Current.CancellationToken);
            Assert.False(result.Reachable);
            Assert.Contains("CRAM-MD5", result.Reason);
        }
    }

    [Fact]
    public async Task CheckAsync_When_EhloFailsAfterBanner_Returns_False()
    {
        var relay = FakeSmtpRelay.Start(FakeSmtpRelay.Mode.BadEhlo);
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port,
                Secure = SmtpOptions.SecureNone
            }));

            var result = await check.CheckAsync(TestContext.Current.CancellationToken);
            Assert.False(result.Reachable);
            // Diagnostic must name the step that broke (EHLO) and the relay's
            // 421 reply, so /health is actionable instead of "unreachable".
            Assert.Contains("EHLO", result.Reason);
            Assert.Contains("421", result.Reason);
        }
    }

    [Fact]
    public async Task CheckAsync_When_OnlyOneCredentialSet_Returns_False()
    {
        // The exactly-one-or-zero invariant (SmtpSender's, mirrored here): a
        // half-configured credential pair is a configuration error, and the
        // probe must not claim to have proven reachability.
        var onlyUser = new SmtpHealthCheck(Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = 2525,   // arbitrary; never reached
            User = "kumunita@kumunita",
            Pass = null
        }));

        var onlyPass = new SmtpHealthCheck(Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = 2525,
            User = null,
            Pass = "relay-secret"
        }));

        var userOnly = await onlyUser.CheckAsync(TestContext.Current.CancellationToken);
        var passOnly = await onlyPass.CheckAsync(TestContext.Current.CancellationToken);

        Assert.False(userOnly.Reachable);
        Assert.False(passOnly.Reachable);
        // Config error must be distinguishable from a live-connection failure —
        // no I/O happened here, so the message should say "misconfigured".
        Assert.Contains("misconfigured", userOnly.Reason);
        Assert.Contains("misconfigured", passOnly.Reason);
    }

    [Fact]
    public async Task CheckAsync_When_SecureTls_And_RelayDoesNotAdvertiseStarttls_Fails_With_Diagnostic()
    {
        // The default SmtpOptions.Secure is "Tls". A real-world relay that is
        // plain-only (e.g. a local Mailpit-style relay) would advertise EHLO
        // capabilities without STARTTLS, and the BCL SmtpClient would refuse
        // to connect with EnableSsl=true against it. The probe must fail at
        // the STARTTLS check and say so explicitly — this is the exact
        // diagnostic the "AUTH: relay does not advertise any AUTH mechanism"
        // bug on the Coolify test server was masking.
        var relay = FakeSmtpRelay.Start(FakeSmtpRelay.Mode.PlainNoStarttls);
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port,
                User = "kumunita@kumunita",
                Pass = "relay-secret",
                Secure = SmtpOptions.SecureTls   // default
            }));

            var result = await check.CheckAsync(TestContext.Current.CancellationToken);
            Assert.False(result.Reachable);
            Assert.Contains("STARTTLS", result.Reason);
        }
    }

    [Fact]
    public async Task CheckAsync_When_SecureTls_And_RelayAdvertisesStarttls_ButRefusesUpgrade_Fails()
    {
        // The relay advertises STARTTLS in EHLO but replies 454 to the
        // upgrade command — e.g. a relay with a transient TLS subsystem
        // outage. The probe must name the refusal and the relay's own
        // 454 reply so /health is actionable.
        var relay = FakeSmtpRelay.Start(FakeSmtpRelay.Mode.StarttlsRefused);
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port,
                User = "kumunita@kumunita",
                Pass = "relay-secret",
                Secure = SmtpOptions.SecureTls
            }));

            var result = await check.CheckAsync(TestContext.Current.CancellationToken);
            Assert.False(result.Reachable);
            Assert.Contains("STARTTLS", result.Reason);
            Assert.Contains("454", result.Reason);
        }
    }

    private static async Task CheckFalse(Task<SmtpHealthResult> result)
    {
        Assert.False((await result).Reachable);
    }
}

/// <summary>
/// Minimal loopback SMTP responder: a <c>220</c> banner, and for each
/// subsequent command (EHLO / AUTH PLAIN / AUTH LOGIN / STARTTLS) whatever
/// the <see cref="Mode"/> requires — exactly the shape the probe walks.
/// </summary>
internal sealed class FakeSmtpRelay : IDisposable
{
    private readonly TcpListener _listener;
    private volatile bool _disposed;
    private readonly Mode _mode;

    private FakeSmtpRelay(TcpListener listener, Mode mode)
    {
        _listener = listener;
        _mode = mode;
    }

    /// <summary>
    /// Behaviors the fake relay can exhibit — each corresponds to one test of
    /// the probe's STARTTLS or AUTH path (or a deliberately-broken relay).
    /// </summary>
    public enum Mode
    {
        PlainNoStarttls = 0,   // banner + EHLO (no STARTTLS, no AUTH advertised) — the Mailpit plain shape
        AuthPlainAccept,       // EHLO advertises AUTH PLAIN; AUTH returns 235
        AuthPlainReject,       // EHLO advertises AUTH PLAIN; AUTH returns 535
        AuthLoginAccept,       // EHLO advertises AUTH LOGIN; two 334s then 235
        AuthAdsNoUsableMech,   // EHLO advertises AUTH CRAM-MD5 (BCL doesn't support) → probe returns false
        BadEhlo,               // EHLO returns 421 (non-2xx) — the "down after banner" shape
        StarttlsRefused        // EHLO advertises STARTTLS; STARTTLS is answered 454 (refused)
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public static FakeSmtpRelay Start() => Start(Mode.PlainNoStarttls);
    public static FakeSmtpRelay Start(Mode mode)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);   // port 0 = OS-assigned free port
        listener.Start();
        var relay = new FakeSmtpRelay(listener, mode);
        _ = Task.Run(relay.AcceptLoopAsync);
        return relay;
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!_disposed)
            {
                var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleAsync(client));
            }
        }
        catch (Exception)
        {
            // SocketException/IOException when the listener is stopped at disposal —
            // both are the expected shutdown path.
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();

                await WriteAsync(stream, "220 fake.kumunita ESMTP\r\n").ConfigureAwait(false);

                bool starttlsRefusalSent = false;
                string? request;
                while ((request = await ReadLineAsync(stream).ConfigureAwait(false)) is not null)
                {
                    if (string.IsNullOrWhiteSpace(request))
                        continue;

                    request = request.Trim();   // ReadLineAsync appends a trailing '\n'

                    if (request.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || request.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                    {
                        string ehlo = _mode switch
                        {
                            Mode.AuthPlainAccept => "250-fake.kumunita\r\n250-AUTH PLAIN\r\n250 HELP\r\n",
                            Mode.AuthPlainReject => "250-fake.kumunita\r\n250-AUTH PLAIN\r\n250 HELP\r\n",
                            Mode.AuthAdsNoUsableMech => "250-fake.kumunita\r\n250-AUTH CRAM-MD5\r\n250 HELP\r\n",
                            Mode.AuthLoginAccept => "250-fake.kumunita\r\n250-AUTH LOGIN\r\n250 HELP\r\n",
                            Mode.BadEhlo => "421 service not available\r\n",
                            Mode.StarttlsRefused => "250-fake.kumunita\r\n250-STARTTLS\r\n250-AUTH PLAIN\r\n250 HELP\r\n",
                            _ => "250-fake.kumunita\r\n250 HELP\r\n"   // plain baseline (no STARTTLS advertised)
                        };
                        await WriteAsync(stream, ehlo).ConfigureAwait(false);
                        continue;
                    }

                    int space = request.IndexOf(' ', StringComparison.Ordinal);
                    string cmd = (space < 0 ? request : request[..space]).ToUpperInvariant();

                    if (cmd == "STARTTLS")
                    {
                        // Only the StarttlsRefused mode advertises it; all other
                        // modes would treat this as an unknown command — but the
                        // probe does not send STARTTLS to them (Secure=None), so
                        // this branch is only reached by the refused-upgrade test.
                        if (_mode == Mode.StarttlsRefused && !starttlsRefusalSent)
                        {
                            await WriteAsync(stream, "454 4.7.0 TLS not available\r\n").ConfigureAwait(false);
                            starttlsRefusalSent = true;
                        }
                        else
                        {
                            await WriteAsync(stream, "502 unknown command " + cmd + "\r\n").ConfigureAwait(false);
                        }
                        continue;
                    }

                    if (cmd == "AUTH")
                    {
                        string rest = (space < 0 ? "" : request[(space + 1)..].Trim()).ToUpperInvariant();
                        int mechSpace = rest.IndexOf(' ', StringComparison.Ordinal);
                        string mechanism = mechSpace < 0 ? rest : rest[..mechSpace];
                        if (mechanism.Equals("PLAIN", StringComparison.OrdinalIgnoreCase))
                        {
                            if (_mode == Mode.AuthPlainReject)
                                await WriteAsync(stream, "535 5.7.8 Authentication credentials invalid\r\n").ConfigureAwait(false);
                            else
                                await WriteAsync(stream, "235 Authentication successful\r\n").ConfigureAwait(false);
                        }
                        else if (mechanism.Equals("LOGIN", StringComparison.OrdinalIgnoreCase))
                        {
                            await WriteAsync(stream, "334 VXNlcm5hbWU6\r\n").ConfigureAwait(false);   // "Username:"
                            _ = await ReadLineAsync(stream).ConfigureAwait(false);                   // user token (ignored)
                            await WriteAsync(stream, "334 UGFzc3dvcmQ6\r\n").ConfigureAwait(false);   // "Password:"
                            _ = await ReadLineAsync(stream).ConfigureAwait(false);                   // pass token (ignored)
                            await WriteAsync(stream, "235 Authentication successful\r\n").ConfigureAwait(false);
                        }
                        else
                        {
                            await WriteAsync(stream, "504 Unrecognized authentication type\r\n").ConfigureAwait(false);
                        }
                        continue;
                    }

                    // Fallback — the probe never sends anything else on these
                    // shapes, but a malformed relay response here is better
                    // than hanging the loop.
                    await WriteAsync(stream, "502 unknown command " + cmd + "\r\n").ConfigureAwait(false);
                }
            }
        }
        catch (Exception)
        {
            // SocketException/IOException when the client closes early or the
            // listener is stopped at disposal — the expected shutdown path.
        }
    }

    private static async Task<string?> ReadLineAsync(Stream stream)
    {
        var sb = new StringBuilder();
        var buf = new byte[1];
        int b;
        while ((b = await stream.ReadAsync(buf, 0, 1).ConfigureAwait(false)) > 0)
        {
            char c = (char)buf[0];
            if (c == '\r' || c == '\n')
            {
                sb.Append('\n');
                break;
            }
            sb.Append(c);
        }
        // b == 0 means "stream still open, retry" per ReadAsync contract — loop.
        if (sb.Length == 0) return null;
        return sb.ToString();
    }

    private static Task WriteAsync(Stream stream, string s) =>
        Task.Run(() => { var bytes = Encoding.ASCII.GetBytes(s); stream.Write(bytes, 0, bytes.Length); stream.Flush(); });

    public void Dispose()
    {
        _disposed = true;
        try { _listener.Stop(); } catch { /* already stopped */ }
    }
}
