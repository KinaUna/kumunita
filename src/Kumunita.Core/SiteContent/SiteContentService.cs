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
}
