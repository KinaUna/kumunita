namespace Kumunita.Core.Identity;

/// <summary>
/// Count seam for the <see cref="EmailDeadLetter"/> document (OPS.md §8 — a non-empty
/// set drives <c>/health</c> "degraded"; ARCHITECTURE.md §5/§6.2).
/// <para>
/// Kept behind an interface so the Web-side consumer (the
/// <c>HealthController</c>) can run without a live Postgres: the real implementation
/// opens an <c>IQuerySession</c> against the store, a test double simply returns a
/// canned count. This mirrors the repo's existing service-seam convention
/// (<c>IIdentityService</c>, <c>IUserInfoService</c>).
/// </para>
/// </summary>
public interface IEmailDeadLetterCounter
{
    /// <summary>
    /// Number of <see cref="EmailDeadLetter"/> rows currently in the store.
    /// </summary>
    /// <param name="ct">Optional cancellation token.</param>
    Task<int> GetCountAsync(CancellationToken ct);
}
