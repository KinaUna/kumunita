namespace Kumunita.Core.Authorization;

/// <summary>
/// The <b>single</b> author-based standing→<see cref="AccessVia"/> mapper
/// (invariant C-IP·1). A <b>pure static helper</b> — it maps the actor's
/// standing to the audit tag; it does <b>not</b> decide the standing (the
/// caller's standing check already did), does not query, and does not store.
/// <para>
/// The pattern is uniform across events, todos, boards, goals, and projects:
/// the actor is the <b>author/owner</b> of the resource →
/// <see cref="AccessVia.Owner"/>; otherwise the actor qualified under an
/// elevated role (GlobalAdmin) → <see cref="AccessVia.Admin"/> (the
/// least-distortion slot — a new <c>AccessVia</c> value is forbidden).
/// </para>
/// <para>
/// Replaces the five private mappers that previously lived in
/// <c>EventService</c> (<c>AuditViaFor</c>) and <c>ProjectService</c>
/// (<c>TodoAuditViaFor</c> / <c>BoardAuditViaFor</c> / <c>GoalAuditViaFor</c>
/// / <c>ProjectAuditViaFor</c>) — each an independent copy of the same
/// two-line expression (the seam drift risk this matrix eliminates).
/// </para>
/// <para>
/// <b>Zero new authorization surface</b> (C-IP·2): no new
/// <see cref="AccessVia"/> value, no new <c>AccessAction</c>, no new
/// <c>IAuthorizationService</c> method, no new <c>TargetKind</c>, no DI
/// registration.
/// </para>
/// <para>
/// <b>Not covered:</b> the role-based <c>InventoryService.AuditVia</c>
/// (<c>IsGlobalAdmin(actorRoles) ? Admin : Owner</c>) — a different pattern
/// (no owner id in the signature). It stays in place.
/// </para>
/// </summary>
public static class StandingMatrix
{
    /// <summary>
    /// Maps the actor's standing to the <see cref="AccessVia"/> audit tag for
    /// an author-based resource (event / todo / board / goal / project): the
    /// author/owner → <see cref="AccessVia.Owner"/>; a GlobalAdmin (or other
    /// elevated role) → <see cref="AccessVia.Admin"/>.
    /// </summary>
    /// <param name="actorId">The acting account.</param>
    /// <param name="ownerId">The resource's author/owner id
    /// (<c>event.AuthorId</c> / <c>todo.AuthorId</c> / <c>board.AuthorId</c>
    /// / <c>goal.AuthorId</c> / <c>project.AuthorId</c>).</param>
    /// <returns><see cref="AccessVia.Owner"/> if the actor is the owner;
    /// <see cref="AccessVia.Admin"/> otherwise.</returns>
    public static AccessVia AuditVia(string actorId, string ownerId)
        => string.Equals(ownerId, actorId, StringComparison.Ordinal)
            ? AccessVia.Owner
            : AccessVia.Admin;
}
