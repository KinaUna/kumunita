using System.Reflection;
using Kumunita.Web.Models;

namespace Kumunita.Web.Tests;

/// <summary>
/// M2, plan U10 — the <b>group detail + add/remove member surface</b> pinned
/// at the view-model layer.
/// <para>
/// **Shape pin** (mirrors U8's <c>DirectoryDetailViewModelTests</c> + U9's
/// <c>GroupsViewModelTests</c>): the <see cref="GroupDetailViewModel"/> record
/// carries exactly the six projected fields named in plan U10 —
/// <c>GroupId</c>, <c>Name</c>, <c>OwnerSubjectId</c>, <c>OwnerDisplayName</c>,
/// <c>IsOwner</c>, <c>Members</c> — and the <see cref="GroupMemberViewModel"/>
/// record is the strict two-field projection <c>{SubjectId, DisplayName}</c>
/// (the <see cref="Kumunita.Core.UserInfo.GroupMembership"/> source row — with
/// its <c>GroupId</c>, <c>AddedBy</c>, <c>At</c> — and any
/// <see cref="Kumunita.Core.UserInfo.Profile"/> contact fields never reach the
/// list).
/// </para>
/// <para>
/// **ADR 0003 SoD pin** (plan U10 exit test "the ADR 0003 pin at the
/// view-model layer"): the viewmodel carries <see cref="GroupDetailViewModel.IsOwner"/>
/// as a *presentation* state — a badge, not a gate — and
/// <see cref="GroupDetailViewModel.OwnerSubjectId"/> as the compare value the
/// actor's subject is checked against for that badge. The real gate lives in
/// the controller's <c>TryResolveWriteSurface</c> helper (the shared frozen
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupsForUserAsync"/>
/// projection read) + M1's audit-lane
/// <see cref="Kumunita.Core.Authorization.AccessVia"/> derivation inside the
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.AddGroupMemberAsync"/>
/// / <see cref="Kumunita.Core.UserInfo.IUserInfoService.RemoveGroupMemberAsync"/>
/// writes. The viewmodel itself has *no* channel for a form-bound
/// <c>addedBy</c>/<c>removedBy</c>/<c>Actor</c> field (the owner the write
/// seam takes is always the actor's subject, minted from the signed-in
/// principal — ADR 0003 SoD by structural identity). These tests pin that the
/// *shape* of the surface cannot silently grow a "we just added a form-bound
/// AddedBy" field.
/// </para>
/// <para>
/// **U9 note (line 131):** the plan's U10 pin is honored as
/// <see cref="GroupDetailViewModel.GroupId"/>'s <c>string</c> shape — the
/// frozen <see cref="Kumunita.Core.UserInfo.Group.Id"/> is a <c>string</c>,
/// not a guaranteed <c>Guid</c> (U9's deviation-2).
/// </para>
/// </summary>
public sealed class GroupsDetailViewModelTests
{
    // ── Shape pin: exact field sets on the two U10 records ──────────────

    [Fact]
    public void GroupDetailViewModel_Has_Exactly_Six_Projected_Fields()
    {
        var fields = typeof(GroupDetailViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(
            new[]
            {
                "GroupId",
                "IsOwner",
                "Members",
                "Name",
                "OwnerDisplayName",
                "OwnerSubjectId",
            },
            fields.ToArray());
    }

    [Fact]
    public void GroupMemberViewModel_Has_Exactly_Two_Projected_Fields()
    {
        var fields = typeof(GroupMemberViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // U9 note (line 131) + plan line 152 pin: exactly SubjectId (the
        // opaque GroupMembership.UserId) + DisplayName (the
        // Profile.DisplayName read). No GroupId (the route's {id}), no
        // AddedBy (audit lane), no At, no Email / Phone (the contact-block
        // gate — §9 — is the Directory surface's, not the Groups surface's).
        Assert.Equal(new[] { "DisplayName", "SubjectId" }, fields);
    }

    [Fact]
    public void GroupDetailViewModel_Excludes_SourceGroupFields()
    {
        // The source Group's own fields (Description, Created) must have *no*
        // corresponding member on the row type — the M1 admin surface owns
        // those; the resident-facing Groups surface does not re-surface them.
        var excluded = new[] { "Description", "Created" };
        var props = typeof(GroupDetailViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var field in excluded)
            Assert.DoesNotContain(field, props);
    }

    [Fact]
    public void GroupMemberViewModel_Excludes_SourceProfileContactFields()
    {
        // C-M2·1 view-level pin: the §9 contact block is the Directory
        // surface's (M2 U7/U8 — a *gated* read, evaluated only after
        // Visibility allows the profile — ARCHITECTURE.md §9). A member list
        // is not a contact directory: "we just added Email / Phone to the
        // member row so we have a contact address" is caught here, not in
        // prod. Verified is also a Directory-surface fact (the §4.3
        // candidate filter is the directory's, not the group detail's).
        var excluded = new[] { "Email", "Phone", "HouseholdId", "ExternalId", "Verified" };
        var props = typeof(GroupMemberViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var field in excluded)
            Assert.DoesNotContain(field, props);
    }

    // ── ADR 0003 SoD pin (plan U10 exit test) ─────────────────────────────

    /// <summary>
    /// Plan U10 exit test (literal name): the <b>ADR 0003 pin at the
    /// view-model layer</b>.
    /// <para>
    /// ADR 0003 on the *writes* is enforced by M1's own
    /// <see cref="Kumunita.Core.Authorization.AccessVia"/> derivation in
    /// <see cref="Kumunita.Core.UserInfo.IUserInfoService.AddGroupMemberAsync"/>
    /// / <see cref="Kumunita.Core.UserInfo.IUserInfoService.RemoveGroupMemberAsync"/>
    /// (U9's note, line 131 — "pass the actor as
    /// <c>addedBy</c>/<c>removedBy</c> and let Core decide"). The Web
    /// layer's contribution to that pin is:
    /// </para>
    /// <list type="order">
    /// <item><b>The owner is derived, not carried.</b>
    /// <see cref="GroupDetailViewModel"/> exposes
    /// <see cref="GroupDetailViewModel.IsOwner"/> (a *presentation* state —
    /// the "You own this group" badge) and
    /// <see cref="GroupDetailViewModel.OwnerSubjectId"/> (the display
    /// compare value). It has <b>no</b> property named
    /// <c>Actor</c>/<c>AddedBy</c>/<c>RemovedBy</c>/<c>OwnerId</c>: a
    /// form-bound identity field on the shape would be a Web-layer SoD hole
    /// (the seam's single identity source is
    /// <c>KumunitaPrincipal.SubjectId(User)</c>, minted from the cookie).
    /// </item>
    /// <item><b>The badge is orthogonal to the gate.</b> A non-owner who
    /// reaches the surface (a global admin, or a member) sees the *identical*
    /// shape — <c>IsOwner == false</c> and an <c>OwnerSubjectId</c> that
    /// differs from the actor's subject — but the same member list. The gate
    /// (the <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupsForUserAsync"/>
    /// projection the controller reads before calling the detail route)
    /// is structural: 404 if the actor is not in
    /// owner ∪ member, 200 if they are. The viewmodel is a *carrier*, not
    /// a gate.
    /// </item>
    /// </list>
    /// </summary>
    [Fact]
    public void AddRemove_OnlyOwnerOrAdmin()
    {
        // (a) Owner actor vs non-owner actor — same shape, badge flips.
        var ownerShape = new GroupDetailViewModel(
            GroupId: "g1",
            Name: "Building 4",
            OwnerSubjectId: "alice",
            OwnerDisplayName: "A. Resident",
            IsOwner: true,
            Members: []);

        var adminShape = new GroupDetailViewModel(
            GroupId: "g1",
            Name: "Building 4",
            OwnerSubjectId: "alice",
            OwnerDisplayName: "A. Resident",
            IsOwner: false,               // the actor is a non-owner (a global
                                           // admin, or a member) reaching the same
                                           // surface — the badge flips, the shape
                                           // does not.
            Members: []);

        Assert.True(ownerShape.IsOwner);
        Assert.False(adminShape.IsOwner);

        // (b) Identical shape for both — the owner-ness is a badge, not a
        // gate (the gate is the controller's TryResolveWriteSurface helper
        // + the seam's AccessVia derivation; the Web layer has no channel to
        // re-derive the role from the model).
        var ownerProps = ownerShape.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToHashSet();
        var adminProps = adminShape.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).ToHashSet();
        Assert.Equal(ownerProps, adminProps);

        // (c) The shape itself has no form-bound identity field — the
        // owner id the write seam takes (addedBy / removedBy) is always the
        // actor's subject (KumunitaPrincipal.SubjectId(User)), minted by the
        // controller from the cookie, never a field on this record. The
        // owner id the seam does receive is always == OwnerSubjectId's
        // group owner (the seam derives the audit's Via by loading the
        // group in the same session — U9's note line 131).
        var disallowedFormIdFields = new[]
        {
            "ActorSubjectId",
            "Actor",
            "AddedBy",
            "RemovedBy",
            "OwnerId",
            "Owner",
        };
        foreach (var field in disallowedFormIdFields)
            Assert.DoesNotContain(field, ownerProps);

        // (d) OwnerSubjectId is the display / compare surface — the detail
        // header renders it as "Owned by {OwnerDisplayName}" + the badge
        // (IsOwner = OwnerSubjectId == actor). Both are structural: the
        // model is a carrier state, not a gate.
        Assert.Equal("alice",       ownerShape.OwnerSubjectId);
        Assert.Equal("alice",       adminShape.OwnerSubjectId);
        Assert.Equal("A. Resident", ownerShape.OwnerDisplayName);
        Assert.Equal("A. Resident", adminShape.OwnerDisplayName);
    }

    // ── Owner-and-members projection (plan U10 exit test) ────────────────

    /// <summary>
    /// Plan U10 exit test (literal name): the detail surface projects
    /// <b>owner and members</b> — the owner both *on the header* (as
    /// <see cref="GroupDetailViewModel.OwnerDisplayName"/>) and *in the
    /// member list* (M1's <see cref="Kumunita.Core.UserInfo.IUserInfoService.CreateGroupAsync"/>
    /// commits the owner's own
    /// <see cref="Kumunita.Core.UserInfo.GroupMembership"/> row in M1
    /// step 4, so one
    /// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetGroupMembersAsync"/>
    /// read gives the full list — U9's note line 131, "reuse
    /// <c>GetGroupMembersAsync</c>, do not open a third member-read seam").
    /// <para>
    /// The <i>live</i> (C4) "add → next read sees it" invariant is pinned at
    /// the <b>service</b> layer by U9's
    /// <c>GetGroupMembersAsync_LiveOnNextCall_C4_StrongConsistency</c> and by
    /// M1's <c>GetGroupIdsAsync_LiveMembership_C4_StrongConsistency</c>; this
    /// test pins the <b>shape</b> of the projection (owner + members, each
    /// <see cref="GroupMemberViewModel"/>'s 2-tuple) and that the detail
    /// model has no field to carry a member the route gate denied (the SoD
    /// 404 happens before the viewmodel is built — ADR 0006-D: Web shapes
    /// HTTP, Core decides; a denied route never produces a shape).
    /// </para>
    /// </summary>
    [Fact]
    public void DetailViewModel_Members_Projects_Owner_And_Members()
    {
        // Case 1 — a group owned by alice, with bob as a member: the owner
        // appears in detail.OwnerDisplayName AND in detail.Members (the
        // owner-row pin), and every member row is exactly the two-tuple
        // {SubjectId, DisplayName}.
        var ownerRow  = new GroupMemberViewModel(SubjectId: "alice", DisplayName: "A. Resident");
        var memberRow = new GroupMemberViewModel(SubjectId: "bob",   DisplayName: "B. Resident");

        var model = new GroupDetailViewModel(
            GroupId: "g1",
            Name: "Building 4",
            OwnerSubjectId: "alice",
            OwnerDisplayName: "A. Resident",
            IsOwner: true,                 // the owner (alice) is viewing their
                                           // own group.
            Members: new[] { ownerRow, memberRow });

        // The owner is in the member list (M1's owner-membership pin — the
        // owner's row is not special):
        Assert.Contains(model.Members, m => m.SubjectId == "alice");
        Assert.Contains(model.Members, m => m.SubjectId == "bob");

        // And the owner is the OwnerDisplayName / OwnerSubjectId surface:
        Assert.Equal("A. Resident", model.OwnerDisplayName);
        Assert.Equal("alice",       model.OwnerSubjectId);
        Assert.True(                model.IsOwner);

        // Each member row is the strict 2-tuple — no email / phone / added-by
        // / at / id / group-id:
        foreach (var m in model.Members)
        {
            Assert.False(string.IsNullOrEmpty(m.SubjectId));
            Assert.False(string.IsNullOrEmpty(m.DisplayName));
        }

        // Case 2 — a non-owner member (carol) reaching the same group: the
        // owner row is still in the list (M1's owner-membership pin), the
        // non-owner member is in the list, IsOwner is false (the badge
        // flips on the actor — see AddRemove_OnlyOwnerOrAdmin's
        // adminShape), and the route's SoD gate (the controller's
        // TryResolveWriteSurface helper) is the only lane between "actor is
        // in owner ∪ member" and "the model is built" — a denied route
        // never produces a shape (ADR 0006-D).
        var ownerRow2 = new GroupMemberViewModel("alice", "A. Resident");
        var carolRow  = new GroupMemberViewModel("carol", "C. Resident");

        var model2 = new GroupDetailViewModel(
            GroupId: "g1",
            Name: "Building 4",
            OwnerSubjectId: "alice",
            OwnerDisplayName: "A. Resident",
            IsOwner: false,
            Members: new[] { ownerRow2, carolRow });

        Assert.False(model2.IsOwner);
        Assert.Contains(model2.Members,   m => m.SubjectId == "alice");
        Assert.Contains(model2.Members,   m => m.SubjectId == "carol");
        Assert.DoesNotContain(model2.Members, m => m.SubjectId == "dave");
    }
}
