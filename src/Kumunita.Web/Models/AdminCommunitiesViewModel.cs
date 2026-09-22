namespace Kumunita.Web.Models;

/// <summary>
/// The <c>/admin/communities</c> page (ADR 0062). The community list
/// (enabled + disabled) with the add / edit / mandatory / enable-disable
/// actions. Reuses the <see cref="AdminIndexViewModel.CommunityRow"/>
/// shape (the shared row type the <c>/admin/accounts</c> page also renders).
/// </summary>
public sealed class AdminCommunitiesViewModel
{
    public IReadOnlyList<AdminIndexViewModel.CommunityRow> Communities { get; init; } = [];
    public int DisabledCommunityCount => Communities.Count(c => !c.Enabled);
}
