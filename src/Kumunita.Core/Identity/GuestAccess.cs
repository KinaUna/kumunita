namespace Kumunita.Core.Identity;

/// <summary>
/// M19 (ADR 0120, D2) — the admin-settled, time-bounded guest standing.
/// One document per guest (id = the guest's <see cref="SubjectId"/>). The
/// standing is live only while <c>ValidFrom ≤ now &lt; ValidUntil</c> (D3);
/// a guest outside the window has no standing (no <c>Guest</c> claim —
/// U02's mint, C-M19·3). <see cref="AllowedSurfaces"/> is the closed,
/// enumerated surface set (D4) — the floor is <see cref="GuestSurface.None"/>
/// (an empty set is a valid, least-privileged state, not an error —
/// C-M19·4).
/// </summary>
public sealed class GuestAccess
{
    /// <summary>Document identity — the guest's subject id (one allowance per
    /// guest account; the same id as <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/>).</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>
    /// The bounded window start (D3, "when"). The standing is live from this
    /// instant (inclusive). Set by an admin (D6), not by the guest.
    /// </summary>
    public DateTimeOffset ValidFrom { get; set; }

    /// <summary>
    /// The bounded window end (D3, "when"). The standing is live **until**
    /// this instant (exclusive: <c>now &lt; ValidUntil</c>). A guest at or
    /// past this instant has no standing. Never unbounded (no <c>null</c> —
    /// C-M19·3 forbids an unbounded guest standing).
    /// </summary>
    public DateTimeOffset ValidUntil { get; set; }

    /// <summary>
    /// The closed, enumerated surface set the guest may access (D4). The
    /// floor is <see cref="GuestSurface.None"/> (an empty set). A typo or an
    /// unknown value grants nothing. Additive: a new surface is a new
    /// <see cref="GuestSurface"/> value, never a string (the ADR 0030
    /// append precedent).
    /// </summary>
    public GuestSurface AllowedSurfaces { get; set; } = GuestSurface.None;

    /// <summary>
    /// The admin subject id that settled this standing (recorded for audit;
    /// the <see cref="Kumunita.Core.Authorization.AccessAudit"/> row's <c>ActorId</c>
    /// is the same value at write time).
    /// </summary>
    public string SetByAdmin { get; set; } = string.Empty;
}
