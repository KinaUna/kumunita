using Kumunita.Core.Authorization;
using Marten;

namespace Kumunita.Core.Kiosk;

/// <summary>
/// The <see cref="IKioskService"/> impl (ADR 0164, M35·1 — the 2-method
/// seam). A plain store-composing service (the <c>SiteContentService</c>
/// structural template, the M31 <c>IErrorReportService</c> / M13
/// <c>IUsageAnalyticsService</c> precedent): reads open their own
/// <c>QuerySession</c>; the audited write commits the doc + its single
/// <c>AccessAudit</c> row in the **caller's** <c>IDocumentSession</c>
/// (the M3 <c>CreatePostAsync</c> same-transaction precedent). Core stays
/// HTTP-free (ADR 0006-D, M35·8): the Web gate is the only place the
/// sign-in authorization is produced (the ADR 0164 amendment — the gate is
/// now [Authorize], any signed-in user, not GlobalAdmin).
/// <para>
/// **Three intentional departures from the <c>SiteContentService</c>
/// template** (the register is the source of truth, pinned in the design
/// doc §2.2 divergence notes):
/// <list type="bullet">
/// <item><b>Upsert vs. no-op.</b> <see cref="SavePlaylistAsync"/> is an
/// **upsert** — a missing <see cref="KioskPlaylist"/> row is **created**
/// (a resident can configure their playlist on a fresh instance without a
/// seeder — the ADR 0164 amendment). The <c>SiteContentService.SaveAsync</c>
/// template is a no-op on a missing row (SITE·6).</item>
/// <item><b>Caller's session vs. own session.</b>
/// <see cref="SavePlaylistAsync"/> takes the **caller's**
/// <c>IDocumentSession</c> (in-transaction with surrounding work). The
/// <c>SiteContentService.SaveAsync</c> template opens its **own** session.</item>
/// <item><b>Nullable read vs. never-null read.</b>
/// <see cref="GetPlaylistAsync"/> returns <c>null</c> on a missing row (the
/// M35-8 floor renders <c>kiosk.empty</c>). The
/// <c>SiteContentService.GetAsync</c> template returns a never-null in-code
/// fallback.</item>
/// </list>
/// </para>
/// <para>
/// The <c>IAuthorizationService</c> is carried for composition-root symmetry
/// (the M23 <c>ProfileFindService</c> / M5 <c>PostService</c> precedent)
/// but is **not invoked** — M35·1 pins "no <c>Decide()</c> branch"; the
/// controller (<c>[Authorize]</c> — any signed-in user, the ADR 0164
/// amendment) enforces the sign-in standing (the per-resident preference
/// precedent).
/// </para>
/// </summary>
public sealed class KioskService : IKioskService
{
    private readonly IDocumentStore _store;

    public KioskService(IDocumentStore store, IAuthorizationService authz)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        // authz is carried for composition-root symmetry (the M23
        // ProfileFindService / M5 PostService precedent) but is **not**
        // invoked — M35·1 pins "no Decide() branch"; the controller
        // ([Authorize] — any signed-in resident) enforces the standing (the
        // per-resident write-lane precedent). Validated non-null so a missing
        // DI registration fails loudly at resolution time.
        _ = authz ?? throw new ArgumentNullException(nameof(authz));
    }

    /// <inheritdoc />
    public async Task<KioskPlaylist?> GetPlaylistAsync(string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));

        // M35·5 (ADR 0164 D5) — the read is a config load, not an access
        // decision: zero AccessAudit rows, zero authorization calls. A
        // missing row (a resident who has not configured their playlist)
        // returns null — the display renders the kiosk.empty message
        // (M35-8 FACE). Unlike the SiteContentService.GetAsync template
        // (which returns a never-null in-code fallback), the kiosk has no
        // in-code fallback content — an empty playlist is a legitimate
        // state, not a degraded one.
        using var session = _store.QuerySession();
        return await session
            .LoadAsync<KioskPlaylist>(actorId, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<KioskPlaylist> SavePlaylistAsync(
        IReadOnlyList<KioskView> views,
        int viewIntervalSeconds,
        int refreshIntervalSeconds,
        KioskDisplayMode displayMode,
        string actorId,
        IDocumentSession session,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));

        // M35·5 (ADR 0164 D5) — exactly one kiosk.save AccessAudit row,
        // TargetKind = "kiosk", Via = Owner. Within the caller's session
        // (in-transaction with any surrounding work — the M3 CreatePostAsync
        // same-transaction precedent). Upsert: a missing row is created
        // (the register's M35·2 pin — the one intentional departure from the
        // SiteContentService.SaveAsync no-op-on-missing template; a resident
        // can configure their playlist on a fresh instance without a seeder).
        // The row is **per-resident** — keyed on the actor's subject id, so
        // the acting resident's own playlist is upserted (the ADR 0164
        // amendment: per-resident, not a shared GlobalAdmin singleton).
        var row = await session
            .LoadAsync<KioskPlaylist>(actorId, ct)
            .ConfigureAwait(false)
            ?? new KioskPlaylist { Id = actorId };

        row.Views                  = views;
        row.ViewIntervalSeconds    = viewIntervalSeconds;
        row.RefreshIntervalSeconds = refreshIntervalSeconds;
        row.DisplayMode            = displayMode;

        session.Store(row);

        // Exactly one AccessAudit row (the timezone.set-default /
        // locale.save per-resident-toggle shape, M35·5 — the one-row
        // convention). Via = Owner: the acting resident writes their own
        // playlist (the ADR 0164 amendment — per-resident, not the admin
        // surface), TargetId = the resident's subject id (the row's key).
        session.Store(new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "kiosk.save",
            TargetKind = "kiosk",
            TargetId = actorId,
            Via = AccessVia.Owner,
            Outcome = AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return row;
    }
}
