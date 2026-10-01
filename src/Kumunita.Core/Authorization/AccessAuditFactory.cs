namespace Kumunita.Core.Authorization;

/// <summary>
/// The <b>single</b> factory for single-target <see cref="AccessAudit"/> rows
/// (invariant C-IP·1). A <b>pure static helper</b> — it returns a
/// fully-populated <see cref="AccessAudit"/>; the <b>caller</b> stores it in
/// their own <c>IDocumentSession</c> (the "caller's session" idiom — C3: the
/// audit row commits atomically with the domain write, in the caller's
/// transaction).
/// <para>
/// Replaces the three private <c>StoreAuditRow</c> copies that previously
/// lived in <c>EventService</c>, <c>InventoryService</c>, and
/// <c>ProjectService</c> (each independently re-decided
/// <c>EffectivePrincipalId</c>, <c>At</c>, and <c>Id</c> — the seam
/// drift risk this factory eliminates).
/// </para>
/// <para>
/// <b>Zero new authorization surface</b> (C-IP·2): no new
/// <c>AccessAction</c>, no new <c>AccessVia</c> value, no new
/// <c>IAuthorizationService</c> method, no new <c>TargetKind</c>, no new
/// <c>DocTypes</c> surface, no DI registration.
/// </para>
/// </summary>
public static class AccessAuditFactory
{
    /// <summary>
    /// Creates a single-target <see cref="AccessAudit"/> row (C3 —
    /// "always-on, in-transaction"). The row is <b>not</b> stored here; the
    /// caller stores it in their own session.
    /// <para>
    /// <c>EffectivePrincipalId</c> is set to <paramref name="actorId"/> —
    /// the actor acts as themself (no delegation in write lanes; the
    /// break-glass-attached shape is the aggregate-row path, not this one).
    /// </para>
    /// </summary>
    /// <param name="actorId">The acting account.</param>
    /// <param name="action">The action string (e.g. "event.create",
    /// "inventory.checkout", "todo.update").</param>
    /// <param name="targetKind">The resource kind (e.g. "event",
    /// "inventory", "todo", "board", "goal", "project").</param>
    /// <param name="targetId">The resource id (the single-target shape).</param>
    /// <param name="via">The <see cref="AccessVia"/> tag (Owner / Admin /
    /// etc.) — the standing the actor qualified under.</param>
    /// <param name="outcome">The <see cref="AccessOutcome"/> (Allow / Deny).
    /// Defaults to <see cref="AccessOutcome.Allow"/> (the three previous
    /// copies each defaulted to Allow in different ways; the factory
    /// unifies them).</param>
    /// <returns>A fully-populated <see cref="AccessAudit"/> ready for the
    /// caller to store.</returns>
    public static AccessAudit SingleTarget(
        string actorId,
        string action,
        string targetKind,
        string targetId,
        AccessVia via,
        AccessOutcome outcome = AccessOutcome.Allow)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = action,
            TargetKind = targetKind,
            TargetId = targetId,
            VisibleCount = null,
            HiddenCount = null,
            Via = via,
            Outcome = outcome,
        };
}
