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
/// One <b>invitation the actor received</b> on the <c>/groups</c> list
/// (m2b — the "Your invitations" card; the accept/decline self-lane's UI
/// shape). <see cref="GroupId"/> is the route's <c>{id}</c> the two action
/// forms post to; <see cref="GroupName"/> is the display label (resolved at
/// projection through the m2b <c>GetGroupAsync</c> read — the invitee cannot
/// reach the group detail, owner ∪ member gate, until they accept; the name
/// must not depend on it). The <see cref="Kumunita.Core.UserInfo.GroupInvitation"/>
/// source row's <c>Status</c> / <c>InvitedAt</c> / resolution stamps never
/// reach the model — the card only ever carries <em>pending</em> rows, and
/// "who resolved it" is an <c>AccessAudit</c> lane fact, not a UI fact
/// (docs/design/m2b-group-invitations.md).
/// </summary>
public sealed record InvitationViewModel(
    string GroupId,
    string GroupName,
    string InvitedByDisplayName);

/// <summary>
/// The <b>group list</b> view model (M2 plan U9 + m2b). Holds the
/// <see cref="IReadOnlyList{GroupViewModel}"/> projection the
/// <c>/groups</c> <c>Index</c> action renders plus — since m2b — the actor's
/// own pending invitations (<see cref="Invitations"/>; empty for an actor
/// without any). The view has no channel to a
/// <see cref="Kumunita.Core.UserInfo.Group"/>'s raw fields.
/// </summary>
public sealed class GroupListViewModel
{
    /// <summary>The groups the actor owns or is a member of (F14's projection).</summary>
    public IReadOnlyList<GroupViewModel> Groups { get; init; } = Array.Empty<GroupViewModel>();

    /// <summary>
    /// The actor's <b>own</b> pending group invitations (m2b read lane #2 —
    /// the <c>GetPendingInvitationsForUserAsync</c> projection, each row's
    /// group name resolved through the single-group read). Empty when the
    /// actor holds none; the card is then absent from the view (not an
    /// "no invitations" placeholder — an empty pending list is the absence of
    /// a feature, not a state).
    /// </summary>
    public IReadOnlyList<InvitationViewModel> Invitations { get; init; } = Array.Empty<InvitationViewModel>();
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
/// <para>
/// ADR 0010 adds <see cref="IsPrivate"/> (opt-in; <c>false</c> keeps the
/// group public — the back-office "keep it out of the grant/access lists"
/// switch, not a role/ACL: a private group's privacy gate is ADR 0003 SoD by
/// the same standing as its description edit lane — owner ∪ GlobalAdmin).
/// </para>
/// </summary>
public sealed class GroupCreateModel
{
    [Required, MaxLength(100)]
    [Display(Name = "Group name")]
    public string? Name { get; set; }

    [MaxLength(500)]
    [Display(Name = "Description (optional)")]
    public string? Description { get; set; }

    [Display(Name = "Private group")]
    public bool IsPrivate { get; set; }
}

/// <summary>
/// One <b>pending invitation row</b> on the <c>/groups/{id}</c> invite lane
/// (m2b — the owner's "Pending invitations" list with its cancel links).
/// Strict projection, the <see cref="GroupMemberViewModel"/> 2-tuple pin
/// carried to the invitation axis: <see cref="SubjectId"/> (the invitee's
/// opaque <see cref="Kumunita.Core.UserInfo.GroupInvitation.UserId"/> — the
/// cancel route's <c>{subjectId}</c>) + <see cref="DisplayName"/> (a single
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfileAsync"/>
/// read; the raw subject id when the profile is absent — fail-safe, not a
/// silent blank row). The source row's <c>InvitedBy</c> / <c>InvitedAt</c>
/// / <c>Status</c> never reach the model: the list shows only <em>pending</em>
/// rows, and "who invited, when" is an <c>AccessAudit</c> lane fact, not a
/// member-list-shaped UI fact (docs/design/m2b-group-invitations.md).
/// </summary>
public sealed record PendingInvitationViewModel(string SubjectId, string DisplayName);

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
/// carried only to survive the add/remove POSTs (a form field) and
/// <c>Created</c> is omitted (a M1-era admin-surface fact, not a resident-facing
/// one). <see cref="Description"/> is carried since ADR 0009 (the detail's
/// "About" block + the owner ∪ GlobalAdmin edit lane's form default), and
/// <c>IsOwner</c> is derived from a string compare
/// rather than a separate role claim (the single identity source is the
/// signed-in principal — ADR 0003 SoD by structural identity, mirroring U9).
/// </para>
/// </summary>
/// <param name="GroupId">The <see cref="Kumunita.Core.UserInfo.Group.Id"/>
/// (opaque string — same pin as the U9 list-row <see cref="GroupViewModel.Id"/>
/// deviation from the frozen <see cref="Kumunita.Core.UserInfo.Group"/>.</param>
/// <param name="Name">The group's name (the detail header).</param>
/// <param name="Description">The group's optional
/// <see cref="Kumunita.Core.UserInfo.Group.Description"/> (rendered under the
/// header when non-empty — ADR 0009); null when the group holds none. The
/// edit lane's form default is <b>the actor minting nothing form-bound</b>:
/// the lane's standing (owner ∪ GlobalAdmin, ADR 0007's new-lane rule) is the
/// controller's <c>TryResolveOwnerSurface</c> gate, never a field here.</param>
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
/// <param name="PendingInvitations">The group's pending invitations
/// (m2b read lane #3 — the <c>GetPendingInvitationsForGroupAsync</c>
/// projection, each row's display name resolved through
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfileAsync"/>).
/// Empty when the group holds none; the invite lane then renders its
/// invite form only. The detail's <see cref="IsOwner"/> badge is the
/// *presentation* hint for it — the real SoD gate (C-M2b·1: owner ∪
/// GlobalAdmin) is in the controller's invite/cancel actions, not on this
/// carrier.</param>
/// <param name="IsPrivate">ADR 0010 — the group's
/// <see cref="Kumunita.Core.UserInfo.Group.IsPrivate"/></param>
public sealed record GroupDetailViewModel(
    string GroupId,
    string Name,
    string? Description,
    string OwnerSubjectId,
    string OwnerDisplayName,
    bool IsOwner,
    IReadOnlyList<GroupMemberViewModel> Members,
    IReadOnlyList<PendingInvitationViewModel> PendingInvitations,
    IReadOnlyList<ResidentOption> ResidentCandidates,
    bool IsPrivate = false)
{
    // ── Group posts (ADR 0013) — the membership-scoped feed lives on the
    //    detail page (GroupPosts / GroupPostsTotal); the composer is its
    //    own page (GET /groups/{id}/posts/new + the paired POST). The
    //    Detail action loads the feed from the same
    //    ListGroupFeedAsync / GetGroupIdsAsync lanes the old
    //    /groups/{id}/posts page used, so the access decision + aggregate
    //    audit row are unchanged (G·1/G·3/G·5).
    //    These are object-initializer properties (not positional params) so
    //    their defaults may be non-constant (a C# positional default must be a
    //    compile-time constant — [] is not), and the existing shape-pinning Web
    //    test keeps compiling unchanged. ──
    public IReadOnlyList<PostListItem> GroupPosts { get; init; } = [];

    public int GroupPostsTotal { get; init; }

    public bool CanPost { get; init; }
}

// U10's add/remove routes carry a single [FromForm] subjectId each (the route
// distinguishes add vs remove) — no dedicated form model needed, matching
// U7/U8's "a form is a field, not a record" pin. The owner id the write seams
// take (`addedBy` / `removedBy`) is always the actor's subject, minted
// from the signed-in principal by the controller — never a form field.

/// <summary>
/// One selectable resident for the detail's "Add a member" dropdown — the
/// same two-field shape as <see cref="GroupMemberViewModel"/>, projected from
/// a non-blocked <see cref="Kumunita.Core.UserInfo.Profile"/> (the directory's
/// visibility surface — every non-blocked resident, the platform is
/// invitation-only) <i>minus</i> the group's current members (adding someone
/// already in is a no-op the form should not offer). The view renders it as
/// a plain <c>&lt;option&gt;</c> and filters client-side (name contains).
/// </summary>
/// <param name="SubjectId">The resident's
/// <see cref="Kumunita.Core.UserInfo.Profile.SubjectId"/> (the form's
/// <c>subjectId</c> field value — the add-member seam's <c>userId</c>).</param>
/// <param name="DisplayName">The resident's
/// <see cref="Kumunita.Core.UserInfo.Profile.DisplayName"/> (falling back to
/// the raw <see cref="SubjectId"/> when the name is blank — fail-safe, not a
/// silent blank option).</param>
public sealed record ResidentOption(string SubjectId, string DisplayName);
// ── Group posts (ADR 0013, group-posts milestone U7) — the channel's three
//    view-model types. Mirrors of the M3 post surface's view types (U7 pin:
//    "mirror, minus the audience slot") — the group lane's access decision
//    is membership (G·1), so there is no audience editor and no component
//    picker; the group's membership is the audience proxy. The rows reuse
//    the M3 <see cref="PostListItem"/> / <see cref="ReplyItem"/> records
//    verbatim (same namespace; "reuse, don't re-invent"). The controller
//    (GroupsController's group-post actions) is the only writer of these. ──

/// <summary>
/// The <b>group post detail</b> surface (ADR 0013, U7) —
/// <c>GET /groups/{id}/posts/{postId}</c> + its one-level reply list. A
/// *projection* of
/// <see cref="Kumunita.Core.Posts.PostService.GetGroupPostAsync"/>'s
/// <see cref="Kumunita.Core.Posts.PostDetailResult"/> — the single detail
/// decision row (G·5, TargetId = the post id) is already written at the
/// Core layer, and the <see cref="Replies"/> list is the
/// <b>already-authorized</b> one-level set returned *as-is* under the
/// parent's single group-lane decision (G·7 — no second evaluation, no
/// per-reply row; the C-M3·1 analog). A <b>denied</b> or <b>missing</b>
/// post is mapped by the controller to a 404 (the group lane's fail-closed
/// shape — G·3/G·4, the GroupsController "a non-visible group 404s"
/// precedent; the audience lane is never evaluated, G·8) before this model
/// is built, so the view never receives one for a post the viewer may not
/// read. <see cref="GroupId"/> is the reply form's <b>target slot</b>
/// (the M3 <see cref="PostDetailViewModel"/>'s reply-form target analog —
/// the form posts to <c>/groups/{GroupId}/posts/{Post.Id}/replies</c>).
/// <para>
/// **No dedicated reply view-model** (the U7 pin, exactly as M3 renders
/// its reply form inline on the detail page): a one-field <c>body</c>
/// plain form, no per-reply audience (G·7 — a reply inherits the parent's
/// single group-lane decision).
/// </para>
/// </summary>
public sealed class GroupPostDetailViewModel
{
    public string GroupId { get; set; } = string.Empty;

    public Kumunita.Core.Posts.Post Post { get; set; } = null!;

    public string AuthorDisplayName { get; set; } = string.Empty;

    /// <summary>The author's subject id (the <see cref="Kumunita.Core.Posts.Post"/>'s
    /// <c>AuthorId</c>) — a display convenience: the avatar links the audited
    /// serving lane <c>GET /profile/avatar/{subjectId}</c> (the same "a read,
    /// not a decision" pin as <see cref="AuthorDisplayName"/>).</summary>
    public string AuthorSubjectId { get; set; } = string.Empty;

    /// <summary>The already-authorized one-level replies (G·7 — as-is under
    /// the parent's single group-lane decision; the M3
    /// <see cref="ReplyItem"/> shape reused verbatim).</summary>
    public IReadOnlyList<ReplyItem> Replies { get; set; } = [];

    /// <summary>Whether the signed-in actor authored the post (a display
    /// pin, not a gate — the M3 <see cref="PostDetailViewModel.IsAuthor"/>
    /// analog).</summary>
    public bool IsAuthor { get; set; }
}

/// <summary>
/// The <b>group-post composer</b> form-bound model (ADR 0013, U7) —
/// <c>POST /groups/{id}/posts</c>. Mirrors the M3
/// <see cref="PostComposeViewModel"/> **minus** the
/// <see cref="PostComposeViewModel.Audience"/> / audience-picker slot (the
/// U7 pin): the group's membership is the audience proxy — there is no
/// audience to choose, and the service writes the post's <c>Audience</c>
/// non-null and <b>empty</b> (G·8) regardless of anything on this form.
/// Title is optional (a group post's <c>Title ?</c> is the M3
/// <see cref="Kumunita.Core.Posts.Post"/> shape); the body is required.
/// The group's identity is the route's <c>{id}</c> — never form-bound
/// (a form-bound <c>GroupId</c> would be a lane-bypass hole; the
/// controller mints <see cref="Kumunita.Core.Posts.GroupPostDraft.GroupId"/>
/// from the route, and the create gate's membership decision is the
/// authoritative deny — G·3).
/// </summary>
public sealed class GroupPostComposeViewModel
{
    public string? Title { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>The composer's shape is well-formed for a <c>POST</c>:
    /// <see cref="Body"/> must be non-empty (a bodyless post is a dead row;
    /// the M3 <see cref="PostComposeViewModel.IsValid"/> body pin, minus the
    /// component/audience slots that do not exist on this lane).</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Body);
}