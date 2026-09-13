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
/// ADR 0021 — the <see cref="AdminController.SetRole"/> lane hands the
/// <see cref="Roles.Translator"/> string through to
/// <see cref="IIdentityService.SetRoleAsync"/> unchanged (the Web is a thin
/// wrapper; the Core lane decides whether the role is add-able / remove-able).
/// The existing <see cref="AdminControllerMandatoryTests"/> /
/// <see cref="AdminControllerBlockTests"/> harness is reused: NSubstitute seam,
/// principal from raw claims, and the assertion lives on the substitute's
/// call log.
/// </summary>
public class AdminControllerSetRoleTests
{
    private const string AdminSubject = "subj-admin-001";
    private const string TargetSubject = "subj-target-001";

    [Fact]
    public async Task SetRole_Translator_PassesThrough_ToCoreSeam()
    {
        var identity = Substitute.For<IIdentityService>();
        var controller = Build(identity);

        var model = new SetRoleViewModel
        {
            TargetSubjectId = TargetSubject,
            Role = Roles.Translator,
            ComponentIds = [],
        };

        // The controller maps "Member" to the no-elevated-role sentinel; any
        // other string (including "Translator") passes through unchanged.
        // TempData / redirect NRE is swallowed — the assertion is on the log.
        try { await controller.SetRole(model); }
        catch { /* expected: TempData/redirect NRE */ }

        await identity.Received(1).SetRoleAsync(
            targetSubjectId: TargetSubject,
            adminSubjectId:  AdminSubject,
            role:            Roles.Translator,
            componentIds:    Arg.Is<IReadOnlyList<string>>(l => l.Count == 0));
    }

    [Fact]
    public async Task SetRole_Member_Resets_NoElevatedRole()
    {
        // The "Member" sentinel: the controller maps it to Roles.Member,
        // which Core interprets as "remove every elevated role". The seam
        // call carries Roles.Member — the pin holds that mapping.
        var identity = Substitute.For<IIdentityService>();
        var controller = Build(identity);

        var model = new SetRoleViewModel
        {
            TargetSubjectId = TargetSubject,
            Role = Roles.Member,
            ComponentIds = [],
        };

        try { await controller.SetRole(model); }
        catch { /* expected */ }

        await identity.Received(1).SetRoleAsync(
            targetSubjectId: TargetSubject,
            adminSubjectId:  AdminSubject,
            role:            Roles.Member,
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
