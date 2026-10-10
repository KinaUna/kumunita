using Kumunita.Core.Authorization;
using Marten;

namespace Kumunita.Core.SiteContent;

/// <summary>
/// The read + write lanes for the <c>SiteContent</c> singleton (ADR 0150).
/// Composes the host-registered Marten <see cref="IDocumentStore"/> (the same
/// "concrete service in the Core composition root" shape as
/// <c>IdentityService</c> / <c>PageService</c>) — reads open their own
/// <c>QuerySession</c>; the audited write opens a write
/// <see cref="IDocumentSession"/> and commits the doc + its single
/// <c>AccessAudit</c> row in one session (invariant C3). Core stays
/// HTTP-free (ADR 0006-D): the Web gate (U07) is the only place the admin
/// authorization is produced.
/// </summary>
public sealed class SiteContentService : ISiteContentService
{
    private readonly IDocumentStore _store;

    public SiteContentService(IDocumentStore store) => _store = store;

    /// <inheritdoc />
    public async Task<SiteContent> GetAsync(CancellationToken ct = default)
    {
        // ADR 0050 IsSignupOpenAsync best-effort shape (SITE·1): a missing row
        // (a fresh boot before the seeder ran) or a read failure degrades to the
        // **in-code fallback** — a fresh SiteContent with every field at its
        // shipped default (the byte-identical kw-l text + every section shown).
        // The read never throws and never returns null; the page always renders.
        // World-readable — never an access decision, never a claim, never audited
        // (SITE·1, ADR 0001-B thin-token).
        using var session = _store.QuerySession();
        var row = await session
            .LoadAsync<SiteContent>(SiteContent.SingletonId, ct)
            .ConfigureAwait(false);
        return row ?? new SiteContent();
    }

    /// <inheritdoc />
    public async Task SaveAsync(SiteContent content, string actorBy, CancellationToken ct = default)
    {
        // ADR 0050 SetSignupOpenAsync single audited write-lane shape (SITE·2):
        // one write session, the doc + exactly one AccessAudit row (Via = Admin,
        // action "site.save", TargetKind "site") commit together (invariant C3,
        // strong consistency C4 — live on the very next GetAsync / render). The
        // lane upserts the singleton — it never creates a second row (SITE·6); a
        // missing row is a no-op (the seeder is the only writer that creates the
        // row on a fresh boot, so a fresh instance is already at the in-code
        // fallback).
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var stored = await session
            .LoadAsync<SiteContent>(SiteContent.SingletonId, ct)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return; // a missing row is a no-op — the lane never load-or-creates (SITE·6)
        }

        // The full 13-field set, verbatim from the caller's SiteContent.
        stored.HomeHeroEyebrow     = content.HomeHeroEyebrow;
        stored.HomeHeroLead        = content.HomeHeroLead;
        stored.HomeShowAboutButton = content.HomeShowAboutButton;
        stored.HomeShowFeatures    = content.HomeShowFeatures;
        stored.HomeShowRoadmap     = content.HomeShowRoadmap;
        stored.AboutHeroEyebrow    = content.AboutHeroEyebrow;
        stored.AboutHeroLead       = content.AboutHeroLead;
        stored.AboutShowFeatures   = content.AboutShowFeatures;
        stored.AboutShowScope      = content.AboutShowScope;
        stored.AboutShowPhilosophy = content.AboutShowPhilosophy;
        stored.AboutShowProject    = content.AboutShowProject;
        stored.AboutShowWhatsNew   = content.AboutShowWhatsNew;
        stored.AboutShowContactCta = content.AboutShowContactCta;

        session.Store(stored);

        // Exactly one AccessAudit row (the signup.set-open / timezone.set-default
        // singleton-toggle shape, SITE·2) — the real AccessAudit doc shape (Id +
        // EffectivePrincipalId + Outcome) as IdentityService writes it.
        session.Store(new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorBy,
            EffectivePrincipalId = actorBy,
            Action = "site.save",
            TargetKind = "site",
            TargetId = SiteContent.SingletonId,
            Via = AccessVia.Admin,
            Outcome = AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ─── ADR 0157 — the SITE-2 hero-translation lanes ─────────────────────
    // The ADR 0150 §D5 "future SITE-2 translation lane", implemented on the
    // ADR 0022 / ADR 0048 post-translation shape (a read seam + add / update
    // / remove on the (LanguageCode) key), adapted to the singleton: the site
    // content has no per-resident owner standing, so the standing is a
    // GlobalAdmin only — enforced by the Web gate (the [Authorize(Roles =
    // GlobalAdmin)] on AdminSiteController, ADR 0150 D8), not re-checked here
    // (the ADR 0150 SiteContentService never calls IAuthorizationService; the
    // write lane's audit row is Via = Admin, the same as SaveAsync). Every
    // write is one session + exactly one AccessAudit row (invariant C3) and
    // is strong-consistency (invariant C4 — live on the very next render).

    private const string AuditTargetKind = "site";

    /// <inheritdoc />
    public async Task<IReadOnlyList<SiteContentTranslation>> GetTranslationsAsync(CancellationToken ct = default)
    {
        // A public landing-surface read (ADR 0150 D2) — the ADR 0022
        // GetPostTranslationsAsync "a read, not a decision" pin: no audit row,
        // no authorization call. A read failure degrades to an empty list so
        // the hero renders the singleton's value (never a blank page).
        using var session = _store.QuerySession();
        return await session
            .Query<SiteContentTranslation>()
            .OrderBy(t => t.LanguageCode)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SiteContentTranslation> AddTranslationAsync(
        string languageCode,
        string? homeHeroEyebrow,
        string? homeHeroLead,
        string? aboutHeroEyebrow,
        string? aboutHeroLead,
        string actorBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorBy))
            throw new ArgumentException("An acting actor is required.", nameof(actorBy));

        // At least one of the four hero fields must be non-blank (a
        // translation with nothing in it is not a translation — the ADR 0026
        // "at least one non-blank" rule, enforced by the write seam).
        if (string.IsNullOrWhiteSpace(homeHeroEyebrow) &&
            string.IsNullOrWhiteSpace(homeHeroLead) &&
            string.IsNullOrWhiteSpace(aboutHeroEyebrow) &&
            string.IsNullOrWhiteSpace(aboutHeroLead))
            throw new ArgumentException("A translation needs at least one hero field.", nameof(homeHeroEyebrow));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        // Upsert: a row for this language already exists → replace it in place
        // (the ADR 0048 "re-adding a language overwrites that row" shape; the
        // (LanguageCode) unique index forbids a second row).
        var existing = await session
            .Query<SiteContentTranslation>()
            .Where(t => t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        var translation = existing ?? new SiteContentTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            AuthorId = actorBy,
            Created = now
        };
        translation.LanguageCode = languageCode;
        translation.HomeHeroEyebrow = BlankToNull(homeHeroEyebrow);
        translation.HomeHeroLead    = BlankToNull(homeHeroLead);
        translation.AboutHeroEyebrow = BlankToNull(aboutHeroEyebrow);
        translation.AboutHeroLead    = BlankToNull(aboutHeroLead);

        session.Store(translation);
        session.Store(AuditRow(actorBy, "sitetranslation.add", languageCode));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return translation;
    }

    /// <inheritdoc />
    public async Task<SiteContentTranslation> UpdateTranslationAsync(
        string languageCode,
        string? homeHeroEyebrow,
        string? homeHeroLead,
        string? aboutHeroEyebrow,
        string? aboutHeroLead,
        string actorBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorBy))
            throw new ArgumentException("An acting actor is required.", nameof(actorBy));
        if (string.IsNullOrWhiteSpace(homeHeroEyebrow) &&
            string.IsNullOrWhiteSpace(homeHeroLead) &&
            string.IsNullOrWhiteSpace(aboutHeroEyebrow) &&
            string.IsNullOrWhiteSpace(aboutHeroLead))
            throw new ArgumentException("A translation needs at least one hero field.", nameof(homeHeroEyebrow));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var row = await session
            .Query<SiteContentTranslation>()
            .Where(t => t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"The site has no translation in '{languageCode}'; nothing to update.");

        row.HomeHeroEyebrow  = BlankToNull(homeHeroEyebrow);
        row.HomeHeroLead     = BlankToNull(homeHeroLead);
        row.AboutHeroEyebrow = BlankToNull(aboutHeroEyebrow);
        row.AboutHeroLead    = BlankToNull(aboutHeroLead);

        session.Store(row);
        session.Store(AuditRow(actorBy, "sitetranslation.update", languageCode));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return row;
    }

    /// <inheritdoc />
    public async Task RemoveTranslationAsync(string languageCode, string actorBy, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorBy))
            throw new ArgumentException("An acting actor is required.", nameof(actorBy));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var row = await session
            .Query<SiteContentTranslation>()
            .Where(t => t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"The site has no translation in '{languageCode}'; nothing to remove.");

        session.Delete(row);
        session.Store(AuditRow(actorBy, "sitetranslation.remove", languageCode));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Small shared helpers ──

    /// <summary>A blank hero field is stored as <c>null</c> (the singleton's
    /// value is the fallback for that field) — the ADR 0022
    /// <c>PostTranslation.Title</c>-optional null-coalescing shape.</summary>
    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Builds the single <c>AccessAudit</c> row for a translation
    /// write (the ADR 0022 hand-written audit-row shape, no
    /// <c>CanAsync</c> decision call — the standing is a GlobalAdmin, pinned
    /// by the Web gate; the row records <c>Via = Admin</c>, the same tag the
    /// singleton's <c>SaveAsync</c> write carries, ADR 0150 D3).</summary>
    private static AccessAudit AuditRow(string actorBy, string action, string languageCode) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorBy,
            EffectivePrincipalId = actorBy,
            Action = action,
            TargetKind = AuditTargetKind,
            TargetId = languageCode,
            Via = AccessVia.Admin,
            Outcome = AccessOutcome.Allow
        };
}
