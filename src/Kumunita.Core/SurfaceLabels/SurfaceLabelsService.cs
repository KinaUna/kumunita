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

            // ADR 0158 D5 (the LBL-2 overlay): the SurfaceLabelTranslation row
            // for this language, if it carries a non-blank value for this
            // surface, wins over the singleton. Best-effort — a missing row /
            // read failure degrades to the singleton below (the M29·8 shape
            // is unchanged when no translation row exists, so a fresh
            // instance resolves exactly as before ADR 0158).
            var translation = await session
                .Query<SurfaceLabelTranslation>()
                .Where(t => t.LanguageCode == effectiveLanguage)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            var translationLabel = translation?.GetLabel(surfaceKey);
            if (!string.IsNullOrWhiteSpace(translationLabel))
            {
                return translationLabel; // the translation wins (ADR 0158 D5)
            }
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
    public async Task<SurfaceLabels> GetAsync(CancellationToken ct = default)
    {
        // ADR 0050 IsSignupOpenAsync best-effort shape (M29·2, SITE·1) — the
        // exact SiteContentService.GetAsync parallel: a missing store, a
        // missing row (a fresh boot before the seeder ran), or a read failure
        // degrades to the **in-code fallback** — a fresh SurfaceLabels with
        // every label field null (the all-null = "use the kw-l fallback"
        // shape, M29·3 / M29·4). The read never throws and never returns null
        // (the /admin/labels form always renders); it is world-readable, never
        // an access decision, never a claim (ADR 0001-B thin-token), never
        // audited (M29·2). This returns the **stored** override fields — NOT
        // the resolved text (that is GetLabelAsync) — so a blank field stays
        // blank and a save clears (the U08 drift fix).
        try
        {
            using var session = _store.QuerySession();
            var row = await session
                .LoadAsync<SurfaceLabels>(SurfaceLabels.SingletonId, ct)
                .ConfigureAwait(false);
            return row ?? new SurfaceLabels();
        }
        catch
        {
            return new SurfaceLabels(); // a read failure degrades to the all-null fallback
        }
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

    // ─── ADR 0158 — the LBL-2 surface-label translation lanes ─────────────
    // The ADR 0152 §D8 "future LBL-2 translation lane", implemented on the
    // ADR 0157 SITE-2 hero-translation shape (a read seam + add / update /
    // remove on the (LanguageCode) key), carried onto the surface-labels
    // surface: the surface labels have no per-resident owner standing, so the
    // standing is a GlobalAdmin only — enforced by the Web gate (the
    // [Authorize(Roles = GlobalAdmin)] on AdminSurfaceLabelsController, ADR
    // 0152 D8), not re-checked here (the ADR 0152 SurfaceLabelsService never
    // calls IAuthorizationService; the write lane's audit row is Via = Admin,
    // the same as SaveAsync). Every write is one session + exactly one
    // AccessAudit row (invariant C3) and is strong-consistency (invariant
    // C4 — live on the very next render).

    /// <inheritdoc />
    public async Task<IReadOnlyList<SurfaceLabelTranslation>> GetTranslationsAsync(
        CancellationToken ct = default)
    {
        // A public surface read (ADR 0152 D3) — the ADR 0157
        // GetSiteContentTranslationsAsync "a read, not a decision" pin: no
        // audit row, no authorization call. A read failure degrades to an
        // empty list so the labels resolve the singleton's value (never a
        // blank nav).
        using var session = _store.QuerySession();
        return await session
            .Query<SurfaceLabelTranslation>()
            .OrderBy(t => t.LanguageCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SurfaceLabelTranslation> AddTranslationAsync(
        string languageCode,
        SurfaceLabelTranslation labels,
        string actorBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (labels is null)
            throw new ArgumentNullException(nameof(labels));
        if (string.IsNullOrEmpty(actorBy))
            throw new ArgumentException("An acting actor is required.", nameof(actorBy));
        if (!labels.HasAnyLabel())
            throw new ArgumentException(
                "A translation needs at least one surface label.", nameof(labels));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        // Upsert: a row for this language already exists → replace it in place
        // (the ADR 0157 "re-adding a language overwrites that row" shape; the
        // (LanguageCode) unique index forbids a second row).
        var existing = await session
            .Query<SurfaceLabelTranslation>()
            .Where(t => t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var translation = existing ?? new SurfaceLabelTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            AuthorId = actorBy,
            Created = now
        };
        translation.LanguageCode = languageCode;
        translation.Home        = BlankToNull(labels.Home);
        translation.Announcements = BlankToNull(labels.Announcements);
        translation.Community   = BlankToNull(labels.Community);
        translation.Groups      = BlankToNull(labels.Groups);
        translation.Events      = BlankToNull(labels.Events);
        translation.Projects    = BlankToNull(labels.Projects);
        translation.Inventory   = BlankToNull(labels.Inventory);
        translation.Bookmarks   = BlankToNull(labels.Bookmarks);
        translation.Documents   = BlankToNull(labels.Documents);
        translation.Pages       = BlankToNull(labels.Pages);
        translation.Tags        = BlankToNull(labels.Tags);
        translation.Directory   = BlankToNull(labels.Directory);
        translation.People      = BlankToNull(labels.People);

        session.Store(translation);
        session.Store(TranslationAuditRow(actorBy, "surface_labels_translation.add", languageCode));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return translation;
    }

    /// <inheritdoc />
    public async Task<SurfaceLabelTranslation> UpdateTranslationAsync(
        string languageCode,
        SurfaceLabelTranslation labels,
        string actorBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (labels is null)
            throw new ArgumentNullException(nameof(labels));
        if (string.IsNullOrEmpty(actorBy))
            throw new ArgumentException("An acting actor is required.", nameof(actorBy));
        if (!labels.HasAnyLabel())
            throw new ArgumentException(
                "A translation needs at least one surface label.", nameof(labels));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var row = await session
            .Query<SurfaceLabelTranslation>()
            .Where(t => t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"The surface labels have no translation in '{languageCode}'; nothing to update.");

        row.Home        = BlankToNull(labels.Home);
        row.Announcements = BlankToNull(labels.Announcements);
        row.Community   = BlankToNull(labels.Community);
        row.Groups      = BlankToNull(labels.Groups);
        row.Events      = BlankToNull(labels.Events);
        row.Projects    = BlankToNull(labels.Projects);
        row.Inventory   = BlankToNull(labels.Inventory);
        row.Bookmarks   = BlankToNull(labels.Bookmarks);
        row.Documents   = BlankToNull(labels.Documents);
        row.Pages       = BlankToNull(labels.Pages);
        row.Tags        = BlankToNull(labels.Tags);
        row.Directory   = BlankToNull(labels.Directory);
        row.People      = BlankToNull(labels.People);

        session.Store(row);
        session.Store(TranslationAuditRow(actorBy, "surface_labels_translation.update", languageCode));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return row;
    }

    /// <inheritdoc />
    public async Task RemoveTranslationAsync(
        string languageCode,
        string actorBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorBy))
            throw new ArgumentException("An acting actor is required.", nameof(actorBy));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var row = await session
            .Query<SurfaceLabelTranslation>()
            .Where(t => t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"The surface labels have no translation in '{languageCode}'; nothing to remove.");

        session.Delete(row);
        session.Store(TranslationAuditRow(actorBy, "surface_labels_translation.remove", languageCode));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Small shared helpers ──

    /// <summary>A blank label field is stored as <c>null</c> (the singleton's
    /// value is the fallback for that field) — the ADR 0157
    /// <c>BlankToNull</c> shape carried over.</summary>
    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Builds the single <c>AccessAudit</c> row for a translation
    /// write (the ADR 0157 hand-written audit-row shape, no
    /// <c>CanAsync</c> decision call — the standing is a GlobalAdmin, pinned
    /// by the Web gate; the row records <c>Via = Admin</c>, the same tag the
    /// singleton's <c>SaveAsync</c> write carries, ADR 0152 D4).</summary>
    private static AccessAudit TranslationAuditRow(string actorBy, string action, string languageCode) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorBy,
            EffectivePrincipalId = actorBy,
            Action = action,
            TargetKind = "surface-labels",
            TargetId = languageCode,
            Via = AccessVia.Admin,
            Outcome = AccessOutcome.Allow
        };
}
