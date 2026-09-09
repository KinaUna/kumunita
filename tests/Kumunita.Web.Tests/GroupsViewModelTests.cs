using System.Reflection;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;

namespace Kumunita.Web.Tests;

/// <summary>
/// M2, plan U9 — the <b>group-list privacy pin at the view-model layer</b>.
/// The design doc F14 + U9's deliverable pin the row as a *small projection*
/// <c>{ Id, Name, MemberCount }</c> — the full <see cref="Group"/> document
/// (with <c>OwnerId</c>, <c>Description</c>, <c>Created</c>) and the raw
/// <see cref="GroupMembership"/> rows are *not* view models. These tests pin
/// that the <see cref="GroupViewModel"/> record carries <b>exactly</b> the
/// three projected fields and nothing else (a "we just added the OwnerId to
/// the group row" regression is caught here, not in prod), and that
/// <see cref="GroupCreateModel"/> exposes only the two form-bound fields
/// (<c>Name</c> required, <c>Description</c> optional) — never the owner
/// (which the controller mints from the signed-in principal, ADR 0003 SoD by
/// structural identity).
/// </summary>
public sealed class GroupsViewModelTests
{
    [Fact]
    public void GroupViewModel_Has_Exactly_Three_Projected_Fields()
    {
        var fields = typeof(GroupViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // The U9 pin: Id + Name + MemberCount — and *nothing else*.
        // No OwnerId, no Description, no Created (the full Group doc is not a VM).
        Assert.Equal(new[] { "Id", "MemberCount", "Name" }, fields.ToArray());
    }

    [Fact]
    public void GroupViewModel_Excludes_SourceGroupFields()
    {
        // The source Group's own fields must have *no* corresponding member on
        // the row type — the list never renders them, and the row has nowhere
        // to land their values.
        var groupFields = new[] { "OwnerId", "Description", "Created" };
        var viewModelProps = typeof(GroupViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var field in groupFields)
            Assert.DoesNotContain(field, viewModelProps);
    }

    [Fact]
    public void GroupCreateModel_Exposes_OnlyNameAndDescription()
    {
        var fields = typeof(GroupCreateModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // Name + Description only. No OwnerId / Owner — the actor is minted as
        // the owner by the controller from the signed-in principal (ADR 0003 SoD
        // by structural identity, never a form-bound owner id).
        Assert.Equal(new[] { "Description", "Name" }, fields.ToArray());
    }

    // ── m2b drift lane: the two invitation-projection records ────────────

    /// <summary>
    /// m2b shape pin: <see cref="InvitationViewModel"/> (the "/groups"
    /// list's "Your invitations" card row) is the strict 3-tuple
    /// <c>{ GroupId, GroupName, InvitedByDisplayName }</c> — the
    /// <see cref="Kumunita.Core.UserInfo.GroupInvitation"/> source row's
    /// <c>Id</c>/<c>UserId</c>/<c>InvitedBy</c> (raw subjects),
    /// <c>Status</c>, and timestamp/resolution fields never reach the UI:
    /// a pending row is always <c>Pending</c> (the read lane filters it),
    /// the invitee has no subject channel (they are the actor), and
    /// "who resolved it, when" is an <c>AccessAudit</c> lane fact.
    /// </summary>
    [Fact]
    public void InvitationViewModel_Has_Exactly_Three_Projected_Fields()
    {
        var fields = typeof(InvitationViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(
            new[] { "GroupId", "GroupName", "InvitedByDisplayName" },
            fields.ToArray());

        var props = typeof(InvitationViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var field in new[]
                 { "Id", "UserId", "InvitedBy", "Status", "InvitedAt", "ResolvedAt", "ResolvedBy" })
            Assert.DoesNotContain(field, props);
    }

    /// <summary>
    /// m2b shape pin: <see cref="PendingInvitationViewModel"/> (the
    /// "/groups/{id}" invite lane's pending row) is the strict 2-tuple
    /// <c>{ SubjectId, DisplayName }</c> — the <see
    /// cref="Kumunita.Core.UserInfo.GroupMemberViewModel"/> pin carried to
    /// the invitation axis. <c>SubjectId</c> is the invitee's opaque subject
    /// (the cancel route's <c>{subjectId}</c> segment); the source row's
    /// <c>InvitedBy</c>/<c>InvitedAt</c>/<c>Status</c> never reach the model
    /// (audit-lane and read-lane facts, not a member-list-shaped UI).
    /// </summary>
    [Fact]
    public void PendingInvitationViewModel_Has_Exactly_Two_Projected_Fields()
    {
        var fields = typeof(PendingInvitationViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[] { "DisplayName", "SubjectId" }, fields.ToArray());

        var props = typeof(PendingInvitationViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var field in new[]
                 { "GroupId", "InvitedBy", "Status", "InvitedAt", "ResolvedAt", "ResolvedBy", "Email", "Phone" })
            Assert.DoesNotContain(field, props);
    }
}
