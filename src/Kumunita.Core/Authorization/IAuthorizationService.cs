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

    /// <summary>
    /// Group lane (ADR 0013 — the group posts milestone's ADD on this
    /// interface, the ADR 0006-E compatible lane: the four frozen signatures
    /// above untouched).
    /// <para>
    /// The decision is **membership only** (G·1): the effective principal's
    /// <b>live</b> membership in <paramref name="groupId"/>
    /// (<c>IUserInfoService.GetGroupIdsAsync</c>; strong consistency — C4).
    /// **No** owner-skip (an author is allowed iff a member — G4 FACES),
    /// **no** <c>AccessVia.Moderator</c> branch, **no** break-glass read
    /// (<c>HasBreakGlassAsync</c> / <c>AdminOverride</c> /
    /// <c>ModeratorAssignment</c> are never touched on this lane — G·4),
    /// **no** audience evaluation (G·1/G·8).
    /// </para>
    /// <para>
    /// Delegation (G·6, C2): an in-scope <c>read</c> grant
    /// (<c>grant.Scope</c> contains <c>"read"</c>) acts with the
    /// **owner's** membership; an out-of-scope grant acts as the delegate
    /// themself (M1's acting-identity rule) and still audits
    /// <c>Via Delegation</c>.
    /// </para>
    /// <para>
    /// Rows (C3): **every** call — Allow **and** Deny — writes exactly one
    /// <see cref="AccessAudit"/> row: <c>TargetKind</c> "grouppost",
    /// <c>Action</c> "read". Standalone methods commit their own row; the
    /// <c>IDocumentSession</c> overloads store the row into the caller's
    /// transaction (same lane as <c>CanAsync(..., session)</c>).
    /// </para>
    /// </summary>

    /// <summary>
    /// Single-target group-lane decision. Row: the **decision** shape —
    /// <c>TargetKind</c> "grouppost", **<c>TargetId</c> =
    /// <paramref name="targetPostId"/> ?? <paramref name="groupId"/>**
    /// (detail ⇒ the post id; create-gate ⇒ the group id), counts null,
    /// Via Group / Delegation, Outcome per membership.
    /// Records the row (always — Allow *and* Deny) in its own commit.
    /// </summary>
    Task<Decision> CanSeeGroupAsync(string actorId, string groupId, string? targetPostId);

    /// <summary>
    /// Same decision with the row **in the caller's transaction** — the
    /// G·3 create-gate lane: <c>PostService.CreateGroupPostAsync</c> commits
    /// the Deny row via its own <c>SaveChangesAsync</c> **before** throwing
    /// (the row must survive — G6 FACES) and commits the Allow row + post in
    /// one <c>SaveChangesAsync</c> (atomic, C3). The caller must
    /// dispose/complete the session.
    /// </summary>
    Task<Decision> CanSeeGroupAsync(string actorId, string groupId, string? targetPostId, IDocumentSession session);

    /// <summary>
    /// **Feed (whole-channel)** decision: the channel is all-or-nothing for a
    /// principal (membership — G·1), so one call per visit covers the page and
    /// writes that visit's **aggregate** row (G·5, the C-M3·3 analog):
    /// <c>TargetKind</c> "grouppost", **<c>TargetId</c> null**,
    /// <c>VisibleCount</c>/<c>HiddenCount</c> = (<paramref name="candidateCount"/>, 0)
    /// on Allow, (0, <paramref name="candidateCount"/>) on Deny.
    /// <paramref name="candidateCount"/> = the page's candidate count this call
    /// evaluated (<c>ListGroupFeedAsync</c> passes the count it loaded;
    /// 0 candidates ⇒ no call, no row — the M3 feed 0-candidate shape).
    /// Records the row in its own commit.
    /// </summary>
    Task<Decision> CanSeeGroupFeedAsync(string actorId, string groupId, int candidateCount);

    /// <summary>
    /// Same feed row in the caller's transaction (the C3 lane — kept for the
    /// interface's two-form pattern). The caller must dispose/complete the
    /// session.
    /// </summary>
    Task<Decision> CanSeeGroupFeedAsync(string actorId, string groupId, int candidateCount, IDocumentSession session);
}
