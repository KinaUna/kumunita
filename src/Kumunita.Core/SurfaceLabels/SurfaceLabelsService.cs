using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Marten;

namespace Kumunita.Core.SurfaceLabels;

/// <summary>
/// The read + write lanes for the <c>SurfaceLabels</c> singleton (ADR 0152,
/// M29). Composes the host-registered Marten <see cref="IDocumentStore"/>
/// (the same "concrete service in the Core composition root" shape as
/// <c>SiteContentService</c> / <c>PageService</c>) and the frozen
/// <see cref="ITranslationProvider"/> seam (the <c>kw-l</c> floor, M29·3 /
/// M29·4). The <see cref="ITranslationProvider"/> is optional (the
/// <c>ProjectService</c> / <c>EventReminderService</c> shape — a
/// direct-construction Core test harness that builds the service without the
/// provider still resolves the override; an unset / blank label then falls
/// back to the raw <c>fallbackKey</c> string). Reads open their own
/// <c>QuerySession</c>; the audited write opens a write
/// <see cref="IDocumentSession"/> and commits the doc + its single
/// <c>AccessAudit</c> row in one session (invariant C3). Core stays
/// HTTP-free (ADR 0006-D): the Web gate (U08) is the only place the GlobalAdmin
/// authorization is produced.
/// </summary>
public sealed class SurfaceLabelsService : ISurfaceLabelsService
{
    private readonly IDocumentStore _store;

    /// <summary>
    /// The <c>kw-l</c> floor provider (M29·3 / M29·4). Optional — a null value
    /// (a direct-construction harness with no provider) degrades to the raw
    /// <c>fallbackKey</c> string for the fallback path; a set / non-blank
    /// admin label still resolves (M29·1 consistency holds either way).
    /// </summary>
    private readonly ITranslationProvider? _translations;

    public SurfaceLabelsService(
        IDocumentStore store,
        ITranslationProvider? translations = null)
    {
        _store = store;
        _translations = translations;
    }

    /// <inheritdoc />
    public async Task<string> GetLabelAsync(
        string surfaceKey, string fallbackKey, string? effectiveLanguage,
        CancellationToken ct = default)
    {
        // ADR 0050 IsSignupOpenAsync best-effort shape (M29·2, SITE·1): a
        // missing row (a fresh boot before the seeder ran) or a read failure
        // degrades to the all-null fallback — never throws, never blank.
        // World-readable — never an access decision, never a claim, never
        // audited (M29·2, ADR 0001-B thin-token).
        string? adminLabel = null;
        try
        {
            using var session = _store.QuerySession();
            var row = await session
                .LoadAsync<SurfaceLabels>(SurfaceLabels.SingletonId, ct)
                .ConfigureAwait(false);
            adminLabel = row?.GetLabel(surfaceKey); // null when unset / blank / unknown key
        }
        catch
        {
            adminLabel = null; // a read failure degrades to the kw-l fallback
        }

        // M29·3 / M29·8: a set, non-blank admin label is returned verbatim — a
        // single-string override shown in all languages (the viewer's language
        // only matters for the kw-l fallback).
        if (!string.IsNullOrWhiteSpace(adminLabel))
        {
            return adminLabel;
        }

        // The kw-l floor (M29·3 / M29·4): the canonical source text in the
        // viewer's effective language. The registry entries stay (ADR 0152 D3)
        // — this is the fallback the in-code all-null default resolves to.
        // Best-effort: a null provider (a direct-construction harness) or a
        // provider lookup failure degrades to the raw key (never blank).
        if (_translations is not null)
        {
            try
            {
                return await _translations
                    .GetAsync(fallbackKey, effectiveLanguage)
                    .ConfigureAwait(false);
            }
            catch
            {
                // fall through to the raw-key floor — a resident never sees a
                // blank label (the provider-floor discipline, ADR 0015 D1).
            }
        }

        return fallbackKey;
    }

    /// <inheritdoc />
    public async Task SaveAsync(SurfaceLabels labels, string actorBy, CancellationToken ct = default)
    {
        // ADR 0050 SetSignupOpenAsync single audited write-lane shape (M29·5,
        // SITE·2): one write session, the doc + exactly one AccessAudit row
        // (Via = Admin, action "surface_labels.save", TargetKind
        // "surface-labels") commit together (invariant C3, strong consistency
        // C4 — live on the very next GetLabelAsync / render). The lane
        // upserts the singleton — it never creates a second row (M29·6); a
        // missing row is a no-op (the seeder is the only writer that creates
        // the row on a fresh boot, so a fresh instance is already at the
        // in-code all-null fallback).
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var stored = await session
            .LoadAsync<SurfaceLabels>(SurfaceLabels.SingletonId, ct)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return; // a missing row is a no-op — the lane never load-or-creates (M29·6)
        }

        // The full 13-field set, verbatim from the caller's SurfaceLabels
        // (a blank stored label is stored blank and falls back to the kw-l
        // key at resolution — M29·3 / M29·4, the §2.5 pin 6 shape).
        stored.Home        = labels.Home;
        stored.Announcements = labels.Announcements;
        stored.Community   = labels.Community;
        stored.Groups      = labels.Groups;
        stored.Events      = labels.Events;
        stored.Projects    = labels.Projects;
        stored.Inventory   = labels.Inventory;
        stored.Bookmarks   = labels.Bookmarks;
        stored.Documents   = labels.Documents;
        stored.Pages       = labels.Pages;
        stored.Tags        = labels.Tags;
        stored.Directory   = labels.Directory;
        stored.People      = labels.People;

        session.Store(stored);

        // Exactly one AccessAudit row (the site.save / signup.set-open /
        // timezone.set-default singleton-toggle shape, M29·5) — the real
        // AccessAudit doc shape (Id + EffectivePrincipalId + Outcome).
        session.Store(new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorBy,
            EffectivePrincipalId = actorBy,
            Action = "surface_labels.save",
            TargetKind = "surface-labels",
            TargetId = SurfaceLabels.SingletonId,
            Via = AccessVia.Admin,
            Outcome = AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
