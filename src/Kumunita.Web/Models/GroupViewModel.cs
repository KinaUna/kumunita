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

/// <summary>
/// One <b>member row</b> on the <c>/groups/{id}</c> member list (M2 plan U10).
/// <para>
/// **Exact 2-field projection pin** — <see cref="SubjectId"/> +
/// <see cref="DisplayName"/>. The <see cref="Kumunita.Core.UserInfo.GroupMembership"/>
/// source row never reaches the model (its <c>GroupId</c> is the route's <c>{id}</c>,
/// its <c>AddedBy</c> is an audit-lane fact, not a UI fact), and the
/// <see cref="Kumunita.Core.UserInfo.Profile"/>'s contact fields
/// (<c>Email</c>/<c>Phone</c>) are <b>not</b> on this record — a member list is not
/// a contact directory; the <c>Directory</c> surface (M2 U7/U8) owns the §9-gated
/// contact block, the <c>Groups</c> surface never opens it (view-model-level
/// C-M2·1 pin, ADR 0006-D — the Web shapes HTTP and the projection decides what
/// reaches the Razor view). U9's note: "reuse <c>GetGroupMembersAsync</c>, do
/// <b>not</b> open a third member-read seam" — this record is the <b>shape</b>
/// of that one lane's U10 consumer.
/// </para>
/// </summary>
/// <param name="SubjectId">The member's opaque
/// <see cref="Kumunita.Core.UserInfo.GroupMembership.UserId"/> subject (a
/// <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/>).</param>
/// <param name="DisplayName">The member's
/// <see cref="Kumunita.Core.UserInfo.Profile.DisplayName"/> — resolved at
/// projection from the frozen <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfileAsync"/>
/// lane; an owner with no profile falls back to the raw
/// <see cref="SubjectId"/> (fail-safe, not a silent blank row).</param>
public sealed record GroupMemberViewModel(string SubjectId, string DisplayName);

/// <summary>
/// The <b>group detail</b> view model (M2 plan U10 — <c>/groups/{id}</c>). Holds
/// the group's identity, its <see cref="OwnerSubjectId"/> (the opaque
/// <see cref="Kumunita.Core.UserInfo.Group.OwnerId"/> — the actor compares this
/// against their own subject to see the "You are the owner" badge and the
/// add/remove forms remain visible), the owner's
/// <see cref="OwnerDisplayName"/> (the display-only
/// <see cref="Kumunita.Core.UserInfo.Profile.DisplayName"/> of that subject —
/// a member *list row* is its own record; the owner also appears in
/// <see cref="Members"/> because <c>CreateGroupAsync</c> commits the owner's own
/// <see cref="Kumunita.Core.UserInfo.GroupMembership"/> row in M1, so the detail
/// "projects owner and members" with one member-list lane — U9's
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupMembersAsync"/>),
/// and <see cref="IsOwner"/> (a display-only badge + a hint; the <b>real</b> ADR
/// 0003 SoD pin lives in the projection rule of
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupsForUserAsync"/>
/// and in M1's <c>Via: Owner</c> audit derivation in the write path — this
/// field is a <i>presentation</i> state, not a gate).
/// <para>
/// **Not a <see cref="Kumunita.Core.UserInfo.Group"/> dump** — <c>GroupId</c> is
/// carried only to survive the add/remove POSTs (a form field), <c>Description</c>
/// and <c>Created</c> are omitted (the M1 "admin surface" owns them, not this
/// resident-facing one), and <c>IsOwner</c> is derived from a string compare
/// rather than a separate role claim (the single identity source is the
/// signed-in principal — ADR 0003 SoD by structural identity, mirroring U9).
/// </para>
/// </summary>
/// <param name="GroupId">The <see cref="Kumunita.Core.UserInfo.Group.Id"/>
/// (opaque string — same pin as the U9 list-row <see cref="GroupViewModel.Id"/>
/// deviation from the frozen <see cref="Kumunita.Core.UserInfo.Group"/>.</param>
/// <param name="Name">The group's name (the detail header).</param>
/// <param name="OwnerSubjectId">The group owner's opaque
/// <see cref="Kumunita.Core.UserInfo.Group.OwnerId"/> subject (the form's
/// "removedBy" hint + the <see cref="IsOwner"/> compare source — never a
/// claim, never a form-writable field).</param>
/// <param name="OwnerDisplayName">The owner's display name (a single
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfileAsync"/> read;
/// falls back to <paramref name="OwnerSubjectId"/> when the profile is absent —
/// fail-safe, not a silent "(owner)" stub).</param>
/// <param name="IsOwner">Whether the signed-in actor is <paramref
/// name="OwnerSubjectId"/> — a <b>presentation</b> state ("You are the owner"
/// badge on the <c>Detail</c> view); the SoD <b>gate</b> is M1's
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.AddGroupMemberAsync"/>
/// / <see cref="Kumunita.Core.UserInfo.IUserInfoService.RemoveGroupMemberAsync"/>
/// derivation (<c>Via: Owner</c>/<c>Via: Admin</c>) + the
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupsForUserAsync"/>
/// projection (non-owner/non-member 404 from the route).</param>
/// <param name="Members">The per-group member rows (owner included — see
/// <see cref="GroupMemberViewModel"/>'s doc for the owner-row pin), projected
/// through <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupMembersAsync"/>
/// (the U9 second M2 read — one read lane serves both U9's <c>MemberCount</c>
/// and U10's <c>Members</c>; never a third seam, design doc §2.7).</param>
public sealed record GroupDetailViewModel(
    string GroupId,
    string Name,
    string OwnerSubjectId,
    string OwnerDisplayName,
    bool IsOwner,
    IReadOnlyList<GroupMemberViewModel> Members);

// U10's add/remove routes carry a single [FromForm] subjectId each (the route
// distinguishes add vs remove) — no dedicated form model needed, matching
// U7/U8's "a form is a field, not a record" pin. The owner id the write seams
// take (`addedBy` / `removedBy`) is always the actor's subject, minted
// from the signed-in principal by the controller — never a form field.
