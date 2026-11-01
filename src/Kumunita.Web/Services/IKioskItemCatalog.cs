using Kumunita.Core.Kiosk;

namespace Kumunita.Web.Services;

/// <summary>
/// One pickable item in the kiosk playlist editor (ADR 0285): the id the
/// entry stores, the label the picker shows, the owning group for group
/// posts / group events (<c>null</c> otherwise), and an optional secondary
/// line (the group name).
/// </summary>
public sealed record KioskItemOption(string Id, string Title, string? GroupId, string? Subtitle);

/// <summary>
/// The kiosk playlist's item catalog (ADR 0285) — the Web-side composition
/// that answers "which items of this type can I put on my kiosk?" for the
/// <c>/settings/kiosk</c> picker. It adds no access logic of its own: every
/// list goes through an existing read seam (the M8 search surfaces for the
/// content types, the component list for the calendar), so a resident is only
/// ever offered items they can already see. An interface so the
/// <see cref="Controllers.KioskController"/> tests substitute it.
/// </summary>
public interface IKioskItemCatalog
{
    /// <summary>The picker's result cap.</summary>
    public const int MaxResults = 20;

    /// <summary>
    /// The items of <paramref name="type"/> that <paramref name="actorId"/> can
    /// see. A blank <paramref name="query"/> returns the most recent items
    /// (newest first); otherwise a case-insensitive match on the title / body
    /// (the M8 search semantics). For <see cref="KioskItemType.Calendar"/> the
    /// options are the enabled community components (the "All" choice is the
    /// caller's — it is a label, not an item). At most
    /// <see cref="MaxResults"/> options.
    /// </summary>
    Task<IReadOnlyList<KioskItemOption>> SearchAsync(
        KioskItemType type, string? query, string actorId, CancellationToken ct = default);
}
