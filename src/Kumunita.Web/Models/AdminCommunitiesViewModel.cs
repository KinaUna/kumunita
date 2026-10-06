namespace Kumunita.Web.Models;

/// <summary>
/// The <c>/admin/communities</c> page (ADR 0062). The community list
/// (enabled + disabled) with the add / edit / mandatory / enable-disable
/// actions. Reuses the <see cref="AdminIndexViewModel.CommunityRow"/>
/// shape (the shared row type the <c>/admin/accounts</c> page also renders).
/// </summary>
public sealed class AdminCommunitiesViewModel
{
    public IReadOnlyList<CommunityRowWithMembers> Communities { get; init; } = [];
    public int DisabledCommunityCount => Communities.Count(c => !c.Enabled);

    /// <summary>
    /// A <see cref="AdminIndexViewModel.CommunityRow"/> enriched with the
    /// community's <b>member rows</b> (the <c>/admin/communities</c>
    /// "manage users" panel, ADR 0012's admin-side lanes) — who's an
    /// explicit member and whether that account holds the
    /// <c>Moderator</c> role with a scope assignment on this community.
    /// Mandatory communities carry <see cref="Mandatory"/> — everyone is an
    /// implicit member there, so the panel offers "add as moderator" without
    /// a remove.
    /// </summary>
    public sealed class CommunityRowWithMembers : AdminIndexViewModel.CommunityRow
    {
        public IReadOnlyList<MemberRow> Members { get; init; } = [];
        public IReadOnlyList<CandidateRow> Candidates { get; init; } = [];
    }

    /// <summary>One member of a community: the account id, a display name
    /// (null when the account has no profile — the id renders as a
    /// fallback), the full elevated-role set (so the moderator-assign
    /// form carries the complete roleNames through — not just Moderator),
    /// and the full moderator-scope set (so the un-moderate form carries
    /// the remaining communities — the ADR 0030 set-lane contract).</summary>
    public sealed class MemberRow
    {
        public string UserId { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
        /// <summary>Whether this account moderates this specific community
        /// (Moderator role + a scope assignment on this community).</summary>
        public bool IsModerator { get; init; }
        /// <summary>The account's full elevated-role set (the form's
        /// <c>roleNames</c> hidden inputs — the ADR 0030 independent set).</summary>
        public IReadOnlyList<string> Roles { get; init; } = [];
        /// <summary>The account's full moderator-scope set (the form's
        /// <c>componentIds</c> hidden inputs — the ADR 0030 complete-set
        /// contract; the Manage-page pattern).</summary>
        public IReadOnlyList<string> ModeratorScopes { get; init; } = [];
    }

    /// <summary>Add-picker candidate for the "manage users" panel: an
    /// account that is not already an explicit member of this community
    /// (the panel is GlobalAdmin-gated; the Core lanes enforce standing
    /// per write, so no further filtering is applied here).</summary>
    public sealed class CandidateRow
    {
        public string UserId { get; init; } = string.Empty;
        public string? DisplayName { get; init; }
    }
}

/// <summary>
/// The form-bound shape of the <c>/admin/communities</c> "manage users"
/// panel's moderator actions (<c>SetCommunityModerator</c> /
/// <c>UnsetCommunityModerator</c>). The ADR 0030 set-lane contract (the
/// Manage-page pattern): the form renders the target's <em>complete</em>
/// desired role set + scope set at GET time (already loaded into
/// <see cref="AdminCommunitiesViewModel.MemberRow"/> by
/// <c>BuildAccountDataAsync</c> — no database read in the POST path) as
/// hidden inputs, and the action passes the full set straight through
/// to <see cref="Kumunita.Core.Identity.IIdentityService.SetRoleAsync"/>.
/// </summary>
public sealed class CommunityModeratorFormViewModel
{
    [System.ComponentModel.DataAnnotations.Required]
    public string ComponentId { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>The target's complete desired elevated-role set (ADR 0030
    /// — any subset of the three elevated roles; empty = no elevated
    /// role). SetCommunityModerator carries the member's current role set
    /// + Moderator; UnsetCommunityModerator carries the set minus
    /// Moderator (other roles preserved).</summary>
    public string[] RoleNames { get; set; } = [];

    /// <summary>The target's complete desired moderator-scope set (ADR
    /// 0030's "complete scope" contract). SetCommunityModerator carries
    /// the member's current scope set + this community;
    /// UnsetCommunityModerator carries the set minus this community
    /// (other communities' scopes preserved). Empty + no Moderator role =
    /// "clear the scope" (SetRoleAsync's demote branch).</summary>
    public string[] ComponentIds { get; set; } = [];
}
