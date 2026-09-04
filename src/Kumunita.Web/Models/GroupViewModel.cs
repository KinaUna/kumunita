using System.ComponentModel.DataAnnotations;

namespace Kumunita.Web.Models;

/// <summary>
/// One <b>group row</b> on the <c>/groups</c> list (M2 plan U9; F14 — "my group
/// list shows only groups I own plus groups I belong to").
/// <para>
/// **Exact 3-field projection (U9 pin)** — <see cref="Id"/>, <see cref="Name"/>,
/// <see cref="MemberCount"/>. The source <see cref="Kumunita.Core.UserInfo.Group"/>'s
/// <c>OwnerId</c>, <c>Description</c>, and <c>Created</c> are *not* on this record
/// (the plan's U9 line: "<c>GroupViewModel</c> is a small projection
/// <c>{ Guid Id, string Name, int MemberCount }</c>" — the <c>Group</c> document
/// itself is not a view model; the <c>GroupId</c> in the plan is an opaque
/// <see cref="Kumunita.Core.UserInfo.Group.Id"/> string, see U7's
/// <c>VisibleProfile.SubjectId</c> deviation pin on the opaque subject lane).
/// <see cref="Kumunita.Core.UserInfo.GroupMembership"/> rows never reach the model
/// — only their <see cref="MemberCount"/> does.
/// </para>
/// </summary>
/// <param name="Id">The <see cref="Kumunita.Core.UserInfo.Group.Id"/> (opaque
/// string; not a guaranteed <see cref="Guid"/>).</param>
/// <param name="Name">The group's name (the list's display cell).</param>
/// <param name="MemberCount">The number of the group's active
/// <see cref="Kumunita.Core.UserInfo.GroupMembership"/> rows — U9's second
/// M2 <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupMembersAsync"/>
/// read (the design doc §2.2 contingent-ADD lane the drift-guard opened with
/// <c>GetGroupsForUserAsync</c>).</param>
public sealed record GroupViewModel(string Id, string Name, int MemberCount);

/// <summary>
/// The <b>group list</b> view model (M2 plan U9). Holds the
/// <see cref="IReadOnlyList{GroupViewModel}"/> projection the
/// <c>/groups</c> <c>Index</c> action renders. Exactly one member (the
/// <see cref="Groups"/> collection) — nothing else; the view has no channel to a
/// <see cref="Kumunita.Core.UserInfo.Group"/>'s raw fields.
/// </summary>
public sealed class GroupListViewModel
{
    /// <summary>The groups the actor owns or is a member of (F14's projection).</summary>
    public IReadOnlyList<GroupViewModel> Groups { get; init; } = Array.Empty<GroupViewModel>();
}

/// <summary>
/// The <b>create group</b> form model (M2 plan U9). Bound via <c>[FromForm]</c> on
/// <c>GroupsController.Create</c>. The *owner* is never form-bound — it is minted
/// by the Web layer from <c>KumunitaPrincipal.SubjectId(User)</c> and passed to the
/// M1 seam <see cref="Kumunita.Core.UserInfo.IUserInfoService.CreateGroupAsync"/>
/// (the actor is the owner; ADR 0003 SoD by structural identity, not a re-gate).
/// <see cref="Name"/> is required (a nameless group is a dead row);
/// <see cref="Description"/> is optional (M1's
/// <see cref="Kumunita.Core.UserInfo.Group.Description"/> is a nullable
/// <c>string?</c>).
/// </summary>
public sealed class GroupCreateModel
{
    [Required, MaxLength(100)]
    [Display(Name = "Group name")]
    public string? Name { get; set; }

    [MaxLength(500)]
    [Display(Name = "Description (optional)")]
    public string? Description { get; set; }
}
