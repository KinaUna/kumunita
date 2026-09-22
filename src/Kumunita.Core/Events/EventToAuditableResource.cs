using Kumunita.Core.Authorization;

namespace Kumunita.Core.Events;

/// <summary>
/// Adapter (M4, U02 — pinned in the design doc §3.3): presents an
/// <see cref="Event"/> to the frozen <see cref="IAuthorizationService"/> as an
/// <see cref="IAuditableResource"/>. It mirrors
/// <see cref="Posts.PostToAuditableResource"/> verbatim — the same 6-member
/// projection; the **only** difference is that <see cref="TargetKind"/> is
/// <c>"event"</c> instead of <c>"post"</c>.
/// <para>
/// Mapping (ADR 0054 §3.3):
/// <para>
/// <c>Id</c> = <see cref="Event.Id"/>; <c>Name</c> = <see cref="Event.Title"/>
/// or a 60-char-truncated <see cref="Event.Body"/> (the audit row's human-facing
/// label — the <c>PostToAuditableResource</c> shape); <c>OwnerId</c> =
/// <see cref="Event.AuthorId"/> — the owner branch of the <c>Decide()</c>
/// algorithm is the *only* lane that lets the author see their own
/// empty-audience draft (the ADR 0037 author pin); <c>Audience</c> =
/// <see cref="Event.Audience"/> (the **exact** <c>Post</c>
/// <see cref="Audience"/>, ADR 0001-B / 0036 — <c>null</c> = public, the frozen
/// <c>Decide()</c> branch 5; the adapter projects it as-is and never mutates
/// it); <c>ComponentId</c> = <see cref="Event.ComponentId"/> — a feed filter,
/// *never* an access boundary (C-M3·2), so projecting it here is safe and
/// carries no decision weight; <c>TargetKind</c> = <c>"event"</c> (the
/// **exact** string — the <see cref="AccessAudit.TargetKind"/> discriminator
/// for this line of decisions, C3).
/// </para>
/// <para>
/// The adapter is the **only** new authorization surface (M4): it plugs into
/// the **frozen** <see cref="IAuthorizationService"/> (ADR 0006 §A —
/// <c>CanAsync</c> / <c>CanSeeAsync</c>) with **no** signature change, **no**
/// new <c>AccessAction</c>, **no** new <c>AccessVia</c>, and **no** new branch
/// in <c>Decide()</c> — M4 adds an *adapter*, not a *branch*. The adapter does
/// not *own* the <see cref="Event"/>: a single instance is safe to pass into
/// either overload (<c>CanAsync</c> detail, <c>CanSeeAsync</c> feed) — each
/// call is a value-level projection, not a shared-mutable-state hazard.
/// <c>sealed</c> keeps the surface closed (ADR 0006-D's single-decision-path
/// is what matters, not subclassability).
/// </para>
/// </summary>
public sealed class EventToAuditableResource : IAuditableResource
{
    /// <summary>
    /// Create an adapter for <paramref name="event"/>.
    /// </summary>
    public EventToAuditableResource(Event @event) => Event = @event;

    /// <summary>The event this adapter presents. The adapter does not own it.</summary>
    public Event Event { get; }

    /// <summary>Resource id = the event's document identity.</summary>
    public string Id => Event.Id;

    /// <summary>
    /// Display name for the audit row — the title, or the body truncated to 60
    /// chars (57 + "...") when there is no title (the
    /// <see cref="Posts.PostToAuditableResource.Name"/> shape; the design doc
    /// §3.3 pin).
    /// </summary>
    public string Name => Event.Title ?? (Event.Body.Length < 60 ? Event.Body : Event.Body[..57] + "...");

    /// <summary>Absolute owner = the author (the owner branch of the
    /// <c>Decide()</c> algorithm; the ADR 0037 author pin, ADR 0054 §3.4).</summary>
    public string? OwnerId => Event.AuthorId;

    /// <summary>
    /// The event's audience, projected verbatim (the **exact** <c>Post</c>
    /// <see cref="Audience"/>, ADR 0001-B / 0036 — the adapter never mutates it).
    /// <c>null</c> = public (the frozen <c>Decide()</c> branch 5).
    /// </summary>
    public Audience? Audience => Event.Audience;

    /// <summary>
    /// Component scope — a feed filter (C-M3·2), never an access boundary;
    /// carrying it here gives the moderator-scoped standing its scoping key
    /// without M4 gaining a moderator read branch.
    /// </summary>
    public string? ComponentId => Event.ComponentId;

    /// <summary>
    /// Resource target kind — <c>"event"</c> (the **exact** string; the
    /// <see cref="AccessAudit.TargetKind"/> discriminator for this line of
    /// decisions, C3).
    /// </summary>
    public string TargetKind => "event";
}
