namespace Kumunita.Core.AdminOnboarding;

/// <summary>
/// The read + write seams for the <c>AdminOnboarding</c> singleton
/// (ADR 0153). The <c>GetAsync</c> read is the ADR 0050
/// <c>IsSignupOpenAsync</c> best-effort shape (a missing store / missing
/// row / read failure degrades to <c>null</c> = not-yet-guided — never
/// throws, never returns a sentinel other than <c>null</c>); the
/// <c>CompleteAsync</c> write is the ADR 0150 / ADR 0050
/// <c>SetSignupOpenAsync</c> single audited write-lane shape (one session,
/// one <c>AccessAudit</c> row, strong consistency).
/// </summary>
public interface IAdminOnboardingService
{
    /// <summary>
    /// Returns the <c>AdminOnboarding</c> singleton's <c>CompletedAt</c>
    /// value. **Best-effort**: a missing store (a test construction with no
    /// <c>IDocumentStore</c>), a missing row (a fresh boot before the seeder
    /// ran), or a read failure degrades to <c>null</c> (= not-yet-guided, the
    /// floor). The read never throws and never returns a sentinel other than
    /// <c>null</c>; the banner + the /admin/onboarding page always render
    /// (M30·3, the ADR 0050 <c>IsSignupOpenAsync</c> shape). The read is a
    /// **public admin surface** (GlobalAdmin-gated by the controller's
    /// <c>[Authorize]</c>, not a new authorization surface) and is **never
    /// audited** (a read, not an access decision, M30·3, ADR 0001-B
    /// thin-token).
    /// </summary>
    Task<DateTimeOffset?> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Stamps the <c>AdminOnboarding</c> singleton's <c>CompletedAt</c> to
    /// <see cref="DateTimeOffset.UtcNow"/> — the ADR 0150 / ADR 0050 single
    /// audited write-lane shape. Loads the singleton (a missing row is
    /// **upserted**, not load-or-creates — the lane never creates a second
    /// row, M30·2), stamps <c>CompletedAt = now</c>, and saves in one session
    /// (invariant C3). Exactly **one** <c>AccessAudit</c> row per call
    /// (<c>Via = Admin</c>, action <c>admin_onboarding.complete</c>,
    /// <c>TargetKind</c> "admin-onboarding" — the <c>site.save</c> /
    /// <c>signup.set-open</c> / <c>timezone.set-default</c> singleton-toggle
    /// shape, M30·4). **Strong consistency** (invariant C4): the new value
    /// is live on the very next <c>GetAsync</c> / banner read. The lane
    /// **upserts** the singleton — it never creates a second row (M30·2,
    /// the ADR 0150 D6 pin).
    /// </summary>
    Task CompleteAsync(string actorBy, CancellationToken ct = default);
}
