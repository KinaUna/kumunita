using Marten;

namespace Kumunita.Core.Authorization;

/// <summary>
/// The AuthorizationModule's public surface — ADR 0006 §A (frozen; changes are breaking).
/// <para>
/// <b>The authorization path is unique</b> (ADR 0006-D): every feature-module access
/// check goes through this interface. Feature modules never read group membership for
/// access purposes (the *Distributed fragmentation* anti-pattern) and never re-derive
/// access on their own (invariants 1–6 belong to this module alone).
/// </para>
/// <para>
/// Request shape (ADR 0006-D): <c>IIdentityService.GetCurrentAsync</c> → thin principal →
/// <c>IUserInfoService.GetGroupIdsAsync</c> *once* per request → <see cref="CanAsync"/> /
/// <see cref="CanSeeAsync"/> per check/list.
/// </para>
/// <para>
/// **Audit and transaction (invariant C3):** the frozen methods run standalone and
/// commit their own decision's audit row in their own transaction. The
/// <c>IDocumentSession</c> overloads (ADR 0006-E *compatible* lane — an *added*
/// method, the frozen signatures untouched) append the audit row into the caller's
/// transaction,
/// a command handler's domain write and the access decision against it commit or roll
/// back together — "no silent, unaudited access", a concurrency failure rolls back both.
/// </para>
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Single-target decision — detail views ("may I read this post?").
    /// Evaluates the §4.4 algorithm (owner branch → moderation → break-glass →
    /// <c>MatchGroups</c>) and records the decision's
    /// <see cref="AccessAudit"/> row (always, Allow or Deny) in its own commit.
    /// </summary>
    Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target);

    /// <summary>
    /// <see cref="CanAsync(string, AccessAction, IAuditableResource)"/> with the audit row
    /// written into <paramref name="session"/>
    /// (the caller's in-flight transaction — the same-transaction guarantee,
    /// invariant C3). The caller must dispose/complete the session.
    /// </summary>
    Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target, IDocumentSession session);

    /// <summary>
    /// Bulk decision — list views (feeds, directory, boards): one group-load, one
    /// matching pass over all candidates sharing the same audience matcher as
    /// <see cref="CanAsync(string, AccessAction, IAuditableResource)"/> — invariant C6,
    /// the no-drift property — one aggregate
    /// <see cref="AccessAudit"/> row (<c>visibleCount</c>/<c>hiddenCount</c>) *plus* one
    /// row per visible audience-restricted item. Committing in its own transaction.
    /// </summary>
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates);

    /// <summary>
    /// <see cref="CanSeeAsync(string, AccessAction, IEnumerable{IAuditableResource})"/>
    /// with the aggregate + per-visible-item audit rows written into
    /// <paramref name="session"/> (invariant C3).
    /// </summary>
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates, IDocumentSession session);
}
