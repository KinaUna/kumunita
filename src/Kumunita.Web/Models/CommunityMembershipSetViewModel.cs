using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// The form model for the <c>/admin</c> → <c>SetCommunityMembership</c> action
/// (GlobalAdmin; adds/removes a <c>ComponentMembership</c> posting right on
/// <c>targetSubjectId</c>). Distinct from <see cref="SetRoleViewModel"/>
/// (which drives role + moderator scope) because the two edit different data
/// — a moderator may govern a community they cannot post to, and a member
/// may post to a community they do not moderate (see
/// <see cref="Kumunita.Core.UserInfo.ComponentMembership"/> for the
/// "posting right vs moderator scope" distinction, and
/// <see cref="Kumunita.Core.UserInfo.ModeratorAssignment"/> for the
/// governance row).
/// </summary>
public sealed class CommunityMembershipSetViewModel
{
    [Required]
    public string TargetSubjectId { get; set; } = string.Empty;

    /// <summary>All <c>ComponentId</c> values the account should be a member
    /// of after this action runs. The controller diffs against the current row
    /// to decide add-vs-remove per community: a component present in this set
    /// that is missing in the current row is added; a component on the current
    /// row that is missing here is removed. Passing an empty set strips all
    /// memberships (GlobalAdmin bypass is unaffected).</summary>
    public string[] CommunityIds { get; set; } = [];
}
