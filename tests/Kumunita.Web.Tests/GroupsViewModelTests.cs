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
}
