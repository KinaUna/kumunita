using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using KumunitaClaimTypes = Kumunita.Core.Identity.ClaimTypes;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0030 — the <see cref="AdminController.SetRole"/> lane hands the **set** of
/// elevated roles through to <see cref="IIdentityService.SetRoleAsync"/> unchanged
/// (the Web is a thin wrapper; the Core lane decides which roles are add-able /
/// remove-able). Roles are independent/composable: a resident may hold any subset of
/// GlobalAdmin / Moderator / Translator. The existing
/// <see cref="AdminControllerMandatoryTests"/> / <see cref="AdminControllerBlockTests"/>
/// harness is reused: NSubstitute seam, principal from raw claims, and the assertion
/// lives on the substitute's call log.
/// </summary>
public class AdminControllerSetRoleTests
{
    private const string AdminSubject = "subj-admin-001";
    private const string TargetSubject = "subj-target-001";

    [Fact]
    public async Task SetRole_SingleRole_Translator_PassesThrough_ToCoreSeam()
    {
        var identity = Substitute.For<IIdentityService>();
        var controller = Build(identity);

        var model = new SetRoleViewModel
        {
            TargetSubjectId = TargetSubject,
            RoleNames = [Roles.Translator],
            ComponentIds = [],
        };

        // ADR 0030 — the seam carries the *set* of elevated roles. A single Translator
        // is a one-element set. TempData / redirect NRE is swallowed — the assertion is
        // on the log.
        try { await controller.SetRole(model); }
        catch { /* expected: TempData/redirect NRE */ }

        await identity.Received(1).SetRoleAsync(
            targetSubjectId: TargetSubject,
            adminSubjectId:  AdminSubject,
            roles:           Arg.Is<IReadOnlyCollection<string>>(s => s.Count == 1 && s.Contains(Roles.Translator)),
            componentIds:    Arg.Is<IReadOnlyList<string>>(l => l.Count == 0));
    }

    [Fact]
    public async Task SetRole_NoRoles_EmptySet_Means_Member()
    {
        // ADR 0030 — "Member (no elevated role)" is the *empty set* (Member is the
        // implicit verified-resident standing, never a carried role). The seam call
        // carries an empty set — the pin holds that mapping.
        var identity = Substitute.For<IIdentityService>();
        var controller = Build(identity);

        var model = new SetRoleViewModel
        {
            TargetSubjectId = TargetSubject,
            RoleNames = [],
            ComponentIds = [],
        };

        try { await controller.SetRole(model); }
        catch { /* expected */ }

        await identity.Received(1).SetRoleAsync(
            targetSubjectId: TargetSubject,
            adminSubjectId:  AdminSubject,
            roles:           Arg.Is<IReadOnlyCollection<string>>(s => s.Count == 0),
            componentIds:    Arg.Is<IReadOnlyList<string>>(l => l.Count == 0));
    }

    [Fact]
    public async Task SetRole_MultipleRoles_AreIndependent_AndAllPassThrough()
    {
        // ADR 0030 — roles are independent/composable: a GlobalAdmin may also hold
        // Translator (e.g. to stand in for the community's translators). Both must reach
        // the seam in the same set.
        var identity = Substitute.For<IIdentityService>();
        var controller = Build(identity);

        var model = new SetRoleViewModel
        {
            TargetSubjectId = TargetSubject,
            RoleNames = [Roles.GlobalAdmin, Roles.Translator],
            ComponentIds = [],
        };

        try { await controller.SetRole(model); }
        catch { /* expected */ }

        await identity.Received(1).SetRoleAsync(
            targetSubjectId: TargetSubject,
            adminSubjectId:  AdminSubject,
            roles:           Arg.Is<IReadOnlyCollection<string>>(s =>
                s.Count == 2 && s.Contains(Roles.GlobalAdmin) && s.Contains(Roles.Translator)),
            componentIds:    Arg.Is<IReadOnlyList<string>>(l => l.Count == 0));
    }

    // ── Harness (mirrors AdminControllerMandatoryTests.Build) ───────────────

    private static AdminController Build(IIdentityService identity)
    {
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=_test_never_opened;Username=_;******")
                .Options);

        var controller = new AdminController(
            identities: db,
            store:      Substitute.For<IDocumentStore>(),
            identity:   identity,
            userInfo:   Substitute.For<IUserInfoService>());

        var claims = new List<Claim>
        {
            new(KumunitaClaimTypes.Subject, AdminSubject),
            new(KumunitaClaimTypes.Role, Roles.GlobalAdmin),
        };

        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new ServiceCollection().BuildServiceProvider();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        return controller;
    }
}
