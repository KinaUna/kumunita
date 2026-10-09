using Kumunita.Core.Authorization;
using Marten;

namespace Kumunita.Core.AdminOnboarding;

/// <summary>
/// The read + write lanes for the <c>AdminOnboarding</c> singleton
/// (ADR 0153). Composes the host-registered Marten <see
/// cref="IDocumentStore"/> (the same "concrete service in the Core
/// composition root" shape as <c>SiteContentService</c> /
/// <c>SurfaceLabelsService</c>) — reads open their own <c>QuerySession</c>;
/// the audited write opens a write <see cref="IDocumentSession"/> and commits
/// the doc + its single <c>AccessAudit</c> row in one session (invariant C3).
/// Core stays HTTP-free (ADR 0006-D): the Web gate (U04) is the only place the
/// GlobalAdmin authorization is produced.
/// </summary>
public sealed class AdminOnboardingService : IAdminOnboardingService
{
    private readonly IDocumentStore _store;

    public AdminOnboardingService(IDocumentStore store) => _store = store;

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetAsync(CancellationToken ct = default)
    {
        // ADR 0050 IsSignupOpenAsync best-effort shape (M30·3): a missing
        // store (a test construction with no IDocumentStore), a missing row
        // (a fresh boot before the seeder ran), or a read failure degrades
        // to null (= not-yet-guided, the floor). The read never throws and
        // never returns a sentinel other than null; the banner + the
        // /admin/onboarding page always render. The read is a public admin
        // surface — never an access decision, never audited (M30·3, ADR
        // 0001-B thin-token).
        try
        {
            using var session = _store.QuerySession();
            var row = await session
                .LoadAsync<AdminOnboarding>(AdminOnboarding.SingletonId, ct)
                .ConfigureAwait(false);
            return row?.CompletedAt;
        }
        catch
        {
            return null; // a read failure degrades to null (M30·3)
        }
    }

    /// <inheritdoc />
    public async Task CompleteAsync(string actorBy, CancellationToken ct = default)
    {
        // ADR 0050 SetSignupOpenAsync / ADR 0150 SaveAsync shape (M30·4): one
        // write session, the doc + exactly one AccessAudit row (Via = Admin,
        // action "admin_onboarding.complete", TargetKind "admin-onboarding")
        // commit together (invariant C3, strong consistency C4 — live on the
        // very next GetAsync / banner read). The lane upserts the singleton —
        // it never creates a second row (M30·2, the ADR 0150 D6 pin); a
        // missing row is upserted (the seeder is the only writer that creates
        // the row on a fresh boot, so a fresh instance is already at the floor).
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var stored = await session
            .LoadAsync<AdminOnboarding>(AdminOnboarding.SingletonId, ct)
            .ConfigureAwait(false)
            ?? new AdminOnboarding(); // a missing row is upserted (M30·2)

        stored.Complete(); // stamps CompletedAt = DateTimeOffset.UtcNow

        session.Store(stored);

        // Exactly one AccessAudit row (the site.save / signup.set-open /
        // timezone.set-default singleton-toggle shape, M30·4) — the real
        // AccessAudit doc shape (Id + EffectivePrincipalId + Outcome) as
        // SiteContentService writes it.
        session.Store(new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorBy,
            EffectivePrincipalId = actorBy,
            Action = "admin_onboarding.complete",
            TargetKind = "admin-onboarding",
            TargetId = AdminOnboarding.SingletonId,
            Via = AccessVia.Admin,
            Outcome = AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
