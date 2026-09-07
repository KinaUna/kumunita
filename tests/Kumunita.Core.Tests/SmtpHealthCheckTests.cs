using System.Net;
using System.Net.Sockets;
using System.Text;
using Kumunita.Core.Identity;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Tests;

/// <summary>
/// Unit tests for <see cref="SmtpHealthCheck" /> (OPS §8 — the /health
/// mail-reachability seam): the unconfigured shape (empty host) must report
/// unreachable rather than throw, and a live handshake against a fake SMTP
/// listener must report reachable.
/// </summary>
public class SmtpHealthCheckTests
{
    [Fact]
    public Task IsReachableAsync_When_HostUnconfigured_Returns_False()
    {
        var check = new SmtpHealthCheck(Options.Create(new SmtpOptions()));

        return CheckFalse(check.CheckAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IsReachableAsync_When_RelayAnswersHandshake_Returns_True()
    {
        var relay = FakeSmtpRelay.Start();
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port
            }));

            Assert.True((await check.CheckAsync(TestContext.Current.CancellationToken)).Reachable);
        }
    }

    [Fact]
    public async Task IsReachableAsync_When_NothingListening_Returns_False()
    {
        // FreeTcpPort reserves (and immediately releases) a port, so a connect
        // to it is refused — the "relay down" shape.
        int port = FreeTcpPort.ReserveAndRelease();
        var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
        {
            Host = "127.0.0.1",
            Port = port
        }));

        Assert.False((await check.CheckAsync(TestContext.Current.CancellationToken)).Reachable);
    }

    [Fact]
    public async Task IsReachableAsync_When_RelayAdvertisesAuthPlain_Accepts_Returns_True()
    {
        var relay = FakeSmtpRelay.Start(FakeSmtpRelay.Mode.AuthPlainAccept);
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port,
                User = "kumunita@kumunita",
                Pass = "relay-secret"
            }));

            Assert.True((await check.CheckAsync(TestContext.Current.CancellationToken)).Reachable);
        }
    }

    [Fact]
    public async Task IsReachableAsync_When_RelayAdvertisesAuthPlain_Rejects_Returns_False()
    {
        var relay = FakeSmtpRelay.Start(FakeSmtpRelay.Mode.AuthPlainReject);
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port,
                User = "wrong-user",
                Pass = "wrong-pass"
            }));

            var result = await check.CheckAsync(TestContext.Current.CancellationToken);
            Assert.False(result.Reachable);
            // Diagnostic must name the step: AUTH rejection, plus the relay's own
            // 535 reply, so an operator reading /health sees exactly why the
            // credentials were rejected.
            Assert.Contains("AUTH", result.Reason);
            Assert.Contains("535", result.Reason);
        }
    }

    [Fact]
    public async Task IsReachableAsync_When_RelayAdvertisesAuthLogin_Accepts_Returns_True()
    {
        var relay = FakeSmtpRelay.Start(FakeSmtpRelay.Mode.AuthLoginAccept);
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port,
                User = "kumunita@kumunita",
                Pass = "relay-secret"
            }));

            Assert.True((await check.CheckAsync(TestContext.Current.CancellationToken)).Reachable);
        }
    }

    [Fact]
    public async Task IsReachableAsync_When_RelayAdvertisesOnlyUnsupportedMechanism_Returns_False()
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
                Pass = "relay-secret"
            }));

            var result = await check.CheckAsync(TestContext.Current.CancellationToken);
            Assert.False(result.Reachable);
            Assert.Contains("CRAM-MD5", result.Reason);
        }
    }

    [Fact]
    public async Task IsReachableAsync_When_EhloFailsAfterBanner_Returns_False()
    {
        var relay = FakeSmtpRelay.Start(FakeSmtpRelay.Mode.BadEhlo);
        using (relay)
        {
            var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
            {
                Host = "127.0.0.1",
                Port = relay.Port
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
    public async Task IsReachableAsync_When_OnlyOneCredentialSet_Returns_False()
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

    private static async Task CheckFalse(Task<SmtpHealthResult> result)
    {
        Assert.False((await result).Reachable);
    }
}

/// <summary>
/// Minimal loopback SMTP responder: a <c>220</c> banner, and for each
/// subsequent command (EHLO / AUTH PLAIN / AUTH LOGIN) whatever the
/// <see cref="Mode"/> requires — exactly the shape the probe walks.
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
    /// the probe's AUTH path (or a deliberately-broken relay).
    /// </summary>
    public enum Mode
    {
        NoAuth,                 // banner + EHLO (no AUTH advertised) — the Mailpit shape
        AuthPlainAccept,        // EHLO advertises AUTH PLAIN; AUTH returns 235
        AuthPlainReject,        // EHLO advertises AUTH PLAIN; AUTH returns 535
        AuthLoginAccept,        // EHLO advertises AUTH LOGIN; two 334s then 235
        AuthAdsNoUsableMech,    // EHLO advertises AUTH CRAM-MD5 (BCL doesn't support) → probe returns false
        BadEhlo                 // EHLO returns 421 (non-2xx) — the "down after banner" shape
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    public static FakeSmtpRelay Start()                 => Start(Mode.NoAuth);
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

                string? request;
                while ((request = await ReadLineAsync(stream).ConfigureAwait(false)) is not null)
                {
                    if (string.IsNullOrWhiteSpace(request))
                        continue;

                    if (request.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || request.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                    {
                        // Mode determines which capability lines the relay "offers".
                        string ehlo = _mode switch
                        {
                            Mode.AuthPlainAccept => "250-fake.kumunita\r\n250-AUTH PLAIN\r\n250 HELP\r\n",
                            Mode.AuthPlainReject => "250-fake.kumunita\r\n250-AUTH PLAIN\r\n250 HELP\r\n",
                            Mode.AuthAdsNoUsableMech => "250-fake.kumunita\r\n250-AUTH CRAM-MD5\r\n250 HELP\r\n",
                            Mode.AuthLoginAccept => "250-fake.kumunita\r\n250-AUTH LOGIN\r\n250 HELP\r\n",
                            Mode.BadEhlo => "421 service not available\r\n",
                            _ => "250-fake.kumunita\r\n250 HELP\r\n"   // mode-independent baseline (NoAuth)
                        };
                        await WriteAsync(stream, ehlo).ConfigureAwait(false);
                        continue;
                    }

                    int space = request.IndexOf(' ', StringComparison.Ordinal);
                    // cmd is the first whitespace-delimited token on the line (no
                    // trailing space — it's a pure prefix).
                    string cmd = (space < 0 ? request : request[..space]).ToUpperInvariant();

                    // AUTH ... — the shape the probe actually sends in each mode.
                    // The relay-side dispatch only cares about the first word of
                    // the mechanism (e.g. "AUTH PLAIN <token>" → "PLAIN"); any
                    // trailing argument is the mechanism's own initial-response
                    // payload and is meaningless to this dispatch.
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

                    // Anything else (bare EHLO already handled; QUIT, etc.) — accept.
                    await WriteAsync(stream, "250 OK\r\n").ConfigureAwait(false);
                }
            }
        }
        catch
        {
            // Client went away (normal when the probe's socket closes first).
        }
    }

    private static async Task<string> ReadLineAsync(NetworkStream stream)
    {
        var sb = new StringBuilder(64);
        var buffer = new byte[1];
        while (await stream.ReadAsync(buffer.AsMemory(0, 1)).ConfigureAwait(false) == 1)
        {
            if (buffer[0] == (byte)'\n')
                break;
            sb.Append((char)buffer[0]);
        }
        return sb.ToString();
    }

    private static async Task WriteAsync(NetworkStream stream, string text)
        => await stream.WriteAsync(Encoding.ASCII.GetBytes(text)).ConfigureAwait(false);

    public void Dispose()
    {
        _disposed = true;
        _listener.Stop();
    }
}

/// <summary>Reserves a TCP port on loopback and releases it immediately, yielding a guaranteed-refused port.</summary>
internal static class FreeTcpPort
{
    public static int ReserveAndRelease()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
