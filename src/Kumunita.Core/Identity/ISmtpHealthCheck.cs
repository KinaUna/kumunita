namespace Kumunita.Core.Identity;

/// <summary>
/// Reachability seam for the configured SMTP relay (OPS.md §8 — /health).
/// <para>
/// Kept behind an interface (the same convention as <see cref="IEmailDeadLetterCounter"/>)
/// so the Web-side consumer (<c>HealthController</c>) and its unit tests can run
/// without a network round-trip: the real implementation performs a minimal
/// SMTP handshake against the <c>SMTP__*</c> options, a test double simply
/// returns True/False.
/// <para>
/// A failed handshake means email *delivery* is broken (the durable
/// <c>OutboxEmail</c> retry / dead-letter path is working as designed); the relay
/// being down is the operator signal for OPS §7, so /health reports "degraded"
/// rather than failing the liveness probe outright.
/// </summary>
public interface ISmtpHealthCheck
{
    /// <summary>
    /// Probes the relay and returns a diagnostic result: <see cref="SmtpHealthResult.Reachable"/>
    /// plus (when false) a <see cref="SmtpHealthResult.Reason"/> naming the exact step that
    /// failed, so /health can tell the operator <em>why</em> mail is degraded without
    /// digging through relay logs.
    /// </summary>
    /// <param name="ct">Cancellation / timeout for the round-trip.</param>
    Task<SmtpHealthResult> CheckAsync(CancellationToken ct);
}

/// <summary>
/// Diagnostic outcome of a single SMTP reachability probe (OPS.md §8 — /health).
/// <paramref name="Reachable"/> is the old boolean signal; <paramref name="Reason"/>
/// carries the per-step cause when it is <c>false</c> (banner, EHLO, AUTH, DNS/
/// connect, timeout…), and is <c>null</c> on a passing probe. The reason is
/// operator-facing text and must never contain credentials.
/// </summary>
public sealed record SmtpHealthResult(bool Reachable, string? Reason)
{
    /// <summary>Passing probe (greeting + EHLO, + AUTH when configured).</summary>
    public static SmtpHealthResult Ok { get; } = new(Reachable: true, Reason: null);

    /// <summary>Failure with a human-readable cause for the operator.</summary>
    public static SmtpHealthResult Fail(string reason) => new(Reachable: false, Reason: reason);
}

/// <summary>
/// Implementation of <see cref="ISmtpHealthCheck"/> over the bound
/// <see cref="SmtpOptions"/>: delegates to <see cref="SmtpProbe"/> (the
/// sockets-level minimal SMTP handshake).
/// <para>
/// Unconfigured shape (empty <c>SMTP__Host</c>): returns <c>false</c>, not an
/// exception — a deployment that has no relay at all reports the same degraded
/// signal as a down relay, and the controller's response surfaces the cause.
/// </summary>
public sealed class SmtpHealthCheck(
    Microsoft.Extensions.Options.IOptions<SmtpOptions> options) : ISmtpHealthCheck
{
    private readonly SmtpOptions _cfg = options.Value;

    /// <inheritdoc />
    public async Task<SmtpHealthResult> CheckAsync(CancellationToken ct)
    {
        var raw = await SmtpProbe.TryHandshakeAsync(_cfg, ct).ConfigureAwait(false);
        return raw.Ok ? SmtpHealthResult.Ok : SmtpHealthResult.Fail(raw.Message ?? "unknown error");
    }
}
