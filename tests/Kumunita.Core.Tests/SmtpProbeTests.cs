using Kumunita.Core.Identity;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C12 seam test for the <see cref="SmtpProbe"/> delivery preflight (OPS §8).
/// The sockets-level handshake (greeting → EHLO → STARTTLS when Tls → AUTH when
/// creds) is driven against a loopback <c>FakeSmtpRelay</c> by
/// <see cref="SmtpHealthCheckTests"/>. <see cref="SmtpProbe"/> is <c>internal</c>
/// (no <c>InternalsVisibleTo</c> — the repo's Core-test constraint), so these
/// two pure-config legs that short-circuit **before** any socket I/O are driven
/// through its public seam <see cref="SmtpHealthCheck"/>: the unconfigured relay
/// (empty <c>Host</c>) and the exactly-one-or-zero credential invariant.
/// </summary>
public sealed class SmtpProbeTests
{
    [Fact]
    public async Task UnconfiguredHost_FailsWithNotConfigured()
    {
        var check = new SmtpHealthCheck(Options.Create(new SmtpOptions { Host = "" }));
        var result = await check.CheckAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Reachable);
        Assert.StartsWith("not configured", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyUserSet_FailsMisconfigured_NoIo()
    {
        // Exactly-one-or-zero invariant: one credential set is a configuration
        // error the real send would fail on — rejected before any I/O.
        var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
        {
            Host = "relay.example",
            User = "u",
            Pass = ""
        }));
        var result = await check.CheckAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Reachable);
        Assert.Contains("exactly one of", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyPassSet_FailsMisconfigured_NoIo()
    {
        var check = new SmtpHealthCheck(Options.Create(new SmtpOptions
        {
            Host = "relay.example",
            User = "",
            Pass = "p"
        }));
        var result = await check.CheckAsync(TestContext.Current.CancellationToken);

        Assert.False(result.Reachable);
        Assert.Contains("exactly one of", result.Reason, StringComparison.Ordinal);
    }
}
