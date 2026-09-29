using Kumunita.Core.Authorization;

namespace Kumunita.Core.Inventory;

/// <summary>
/// Adapter (U01 — pinned in the design doc §9.2): presents an
/// <see cref="InventoryItem"/> to the frozen <see cref="IAuthorizationService"/>
/// as an <see cref="IAuditableResource"/>. It mirrors
/// <see cref="Projects.ProjectToAuditableResource"/> verbatim — the same
/// 6-member projection (<c>Id</c> / <c>Name</c> / <c>OwnerId</c> /
/// <c>Audience</c> / <c>ComponentId</c> / <c>TargetKind</c>); the **only**
/// difference is that <see cref="TargetKind"/> is <c>"inventory"</c> instead
/// of <c>"project"</c>.
/// <para>
/// Mapping (ADR 0117 D2 / the design doc §9.2):
/// <para>
/// <c>Id</c> = <see cref="InventoryItem.Id"/> (the document identity);
/// <c>Name</c> = <see cref="InventoryItem.Name"/> (the audit row's human-facing
/// label — the <c>Name</c> is non-empty by pin, so no truncation fallback is
/// needed); <c>OwnerId</c> = <see cref="InventoryItem.AuthorId"/> — the owner
/// branch of the <c>Decide()</c> algorithm; <c>Audience</c> =
/// <see cref="InventoryItem.Audience"/> (the **exact** <c>Post</c>
/// <see cref="Audience"/>, ADR 0001-B / 0036 — <c>null</c> = public, the frozen
/// <c>Decide()</c> branch; the adapter projects it as-is and never mutates it);
/// <c>ComponentId</c> = <see cref="InventoryItem.ComponentId"/> — a feed filter,
/// *never* an access boundary (C-M3·2), so projecting it here carries no
/// decision weight; <c>TargetKind</c> = <c>"inventory"</c> (the **exact** string
/// — the <see cref="AccessAudit.TargetKind"/> discriminator for this line of
/// decisions, C3).
/// </para>
/// <para>
/// The adapter is the **only** new authorization surface for M16 (C-M16·1 /
/// C-M16·4): it plugs into the **frozen** <see cref="IAuthorizationService"/>
/// (ADR 0006 §A — <c>CanAsync</c> / <c>CanSeeAsync</c>) with **no** signature
/// change, **no** new <c>AccessAction</c>, **no** new <c>AccessVia</c>, and **no**
/// new branch in <c>Decide()</c> — M16 adds an *adapter*, not a *branch*. The
/// adapter does not *own* the <see cref="InventoryItem"/>: a single instance is
/// safe to pass into either overload (<c>CanAsync</c> detail,
/// <c>CanSeeAsync</c> list) — each call is a value-level projection, not a
/// shared-mutable-state hazard. <c>sealed</c> keeps the surface closed (ADR
/// 0006-D's single-decision-path is what matters, not subclassability).
/// <see cref="InventoryItem.OwnerKind"/> is **never** consulted here (D3 /
/// C-M16·1 / C-M16·5 — it is a grouping + standing-breadth label, not an access
/// gate).
/// </para>
/// </summary>
public sealed class InventoryItemToAuditableResource : IAuditableResource
{
    /// <summary>
    /// Create an adapter for <paramref name="item"/>.
    /// </summary>
    public InventoryItemToAuditableResource(InventoryItem item) => Item = item;

    /// <summary>The item this adapter presents. The adapter does not own it.</summary>
    public InventoryItem Item { get; }

    /// <summary>Resource id = the item's document identity.</summary>
    public string Id => Item.Id;

    /// <summary>
    /// Display name for the audit row — the name (the <c>Name</c> is non-empty
    /// by pin, so no truncation fallback is needed).
    /// </summary>
    public string Name => Item.Name;

    /// <summary>Absolute owner = the author (the owner branch of the
    /// <c>Decide()</c> algorithm; the standing owner, D5).</summary>
    public string? OwnerId => Item.AuthorId;

    /// <summary>
    /// The item's audience, projected verbatim (the **exact** <c>Post</c>
    /// <see cref="Audience"/>, ADR 0001-B / 0036 — the adapter never mutates it).
    /// <c>null</c> = public (the frozen <c>Decide()</c> branch).
    /// </summary>
    public Audience? Audience => Item.Audience;

    /// <summary>
    /// Component scope — a feed filter (C-M3·2), never an access boundary;
    /// carrying it here gives the standing its scoping key without M16 gaining an
    /// access branch.
    /// </summary>
    public string? ComponentId => Item.ComponentId;

    /// <summary>
    /// Resource target kind — <c>"inventory"</c> (the **exact** string; the
    /// <see cref="AccessAudit.TargetKind"/> discriminator for this line of
    /// decisions, C3).
    /// </summary>
    public string TargetKind => "inventory";
}
