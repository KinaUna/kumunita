using Kumunita.Core.Kiosk;
using Marten;

namespace Kumunita.Web.Services;

/// <summary>
/// The <c>/settings/kiosk</c> shared-link list read seam (ADR 0302,
/// IMPROVE-UX R18). The <see cref="IKioskLinkService"/> Core seam (the R15
/// frozen surface) is the create / revoke / **validate** trio — it deliberately
/// has no owner-scoped *list*, because the only production reader of an owner's
/// active links is this settings surface (the owner's own view of their own
/// credential rows). R18 therefore reads through a small Web-layer seam, the
/// <see cref="KioskItemCatalog"/> / <see cref="EscalationForwarder"/>
/// "Web layer owns the read" precedent: the <c>KioskController</c> stays
/// off direct Marten (the IMPROVE-CODE C09 shape), and the Core seam stays
/// frozen (R18's scope rule).
/// <para>
/// **Read-only, owner-scoped, never the token.** Returns the owner's
/// **active** <see cref="KioskLink"/> rows (those whose <c>RevokedAt</c> is
/// still <c>null</c> — the R15 state machine: a revoked row is terminal and
/// out of the list), newest first, each carrying the metadata the settings
/// surface displays (<c>Id</c>, <c>CreatedAt</c>, <c>LastUsedAt</c>). The
/// <c>TokenHash</c> is on the row but is never shown (the raw token was handed
/// to the owner exactly once at creation, ADR 0302 D2 — there is no
/// "show again"; a re-read of the row cannot recover it).
/// </para>
/// </summary>
public interface IKioskLinkListReader
{
    /// <summary>
    /// The <paramref name="ownerSubjectId"/>'s active (non-revoked)
    /// <see cref="KioskLink"/> rows, newest first. An empty list (no links, or
    /// the store seam absent) never throws.
    /// </summary>
    Task<IReadOnlyList<KioskLink>> ListActiveAsync(
        string ownerSubjectId, CancellationToken ct = default);
}

/// <inheritdoc />
/// <summary>
/// The <see cref="IKioskLinkListReader"/> implementation. Reads through the
/// Marten <see cref="IDocumentStore"/> (the <see cref="EscalationForwarder" />
/// <c>store.QuerySession()</c> idiom — the session is disposed, the singleton
/// store is **not**, the house rule from the
/// <c>SessionRevocationMiddleware</c> fix). The owner-scope filter is data-
/// scoped (the <see cref="KioskLink.OwnerSubjectId"/> column, the
/// <c>KioskLinkService</c> owner-pin precedent), not an
/// <c>IAuthorizationService</c> branch.
/// </summary>
public sealed class KioskLinkListReader(IDocumentStore store) : IKioskLinkListReader
{
    public async Task<IReadOnlyList<KioskLink>> ListActiveAsync(
        string ownerSubjectId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ownerSubjectId))
        {
            return [];
        }

        using var session = store.QuerySession();
        var links = await session.Query<KioskLink>()
            .Where(l => l.OwnerSubjectId == ownerSubjectId
                        && l.RevokedAt == null)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
        return links;
    }
}
