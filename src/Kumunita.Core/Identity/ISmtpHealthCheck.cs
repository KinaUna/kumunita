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
    /// True if a minimal SMTP handshake (banner + EHLO/HELO response) completes
    /// against the configured relay without throwing or timing out.
    /// </summary>
    /// <param name="ct">Cancellation / timeout for the round-trip.</param>
    Task<bool> IsReachableAsync(CancellationToken ct);
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
    public async Task<bool> IsReachableAsync(CancellationToken ct)
        => await SmtpProbe.TryHandshakeAsync(_cfg, ct).ConfigureAwait(false);
}
