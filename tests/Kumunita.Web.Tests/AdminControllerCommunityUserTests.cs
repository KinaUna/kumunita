using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
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
/// The <c>/admin/communities</c> "manage users" panel (the ADR 0012
/// admin-side lanes from the community's perspective, the inverse of the
/// per-account "set of communities" surface). The four POST actions
/// (<see cref="AdminController.AddCommunityUser"/> /
/// <see cref="AdminController.RemoveCommunityUser"/> /
/// <see cref="AdminController.SetCommunityModerator"/> /
/// <see cref="AdminController.UnsetCommunityModerator"/>) hand their
/// inputs through to the audited Core seams. The assertion lives on the
/// NSubstitute call log; the null <c>TempData</c> NRE is swallowed — the
/// same harness shape as <see cref="AdminControllerSetRoleTests"/>.
/// </summary>
public class AdminControllerCommunityUserTests
{
    private const string AdminSubject  = "subj-admin-001";
    private const string TargetSubject = "subj-target-001";
    private const string CompId        = "comp-001";
    private const string OtherComp     = "comp-002";

    // ── AddCommunityUser — the ADR 0012 add-member lane, GlobalAdmin standing ──

    [Fact]
    public async Task AddCommunityUser_Delegates_ToAddCommunityMember_Async()
    {
        var identity = Substitute.For<IIdentityService>();
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(identity, userInfo);

        try { await controller.AddCommunityUser(CompId, TargetSubject); }
        catch { /* expected: TempData NRE — assert on the log */ }

        await userInfo.Received(1).AddCommunityMemberAsync(
            CompId, TargetSubject, AdminSubject,
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)));
        // A plain member add never touches the role surface — the
        // moderator standing has its own lane (SetCommunityModerator).
        await identity.DidNotReceiveWithAnyArgs().SetRoleAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task AddCommunityUser_BlankArgs_NeverInvokesService()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(Substitute.For<IIdentityService>(), userInfo);

        try { await controller.AddCommunityUser(string.Empty, string.Empty); }
        catch { /* expected: the guard's early redirect NREs */ }

        await userInfo.DidNotReceiveWithAnyArgs().AddCommunityMemberAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>());
    }

    // ── RemoveCommunityUser — the ADR 0012 remove-member lane ──

    [Fact]
    public async Task RemoveCommunityUser_Delegates_ToRemoveCommunityMember_Async()
    {
        var identity = Substitute.For<IIdentityService>();
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(identity, userInfo);

        try { await controller.RemoveCommunityUser(CompId, TargetSubject); }
        catch { /* expected: TempData NRE — assert on the log */ }

        await userInfo.Received(1).RemoveCommunityMemberAsync(
            CompId, TargetSubject, AdminSubject,
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)));
        // A plain member remove never touches the role surface (the
        // moderator scope has its own lane — UnsetCommunityModerator).
        await identity.DidNotReceiveWithAnyArgs().SetRoleAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task RemoveCommunityUser_BlankArgs_NeverInvokesService()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(Substitute.For<IIdentityService>(), userInfo);

        try { await controller.RemoveCommunityUser(string.Empty, string.Empty); }
        catch { /* expected */ }

        await userInfo.DidNotReceiveWithAnyArgs().RemoveCommunityMemberAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>());
    }

    // ── SetCommunityModerator — the set-lane (ADR 0030, the Manage-page
    // pattern): the complete desired role set (carrying Moderator) + the
    // complete scope set (carrying this community) pass through to
    // IIdentityService.SetRoleAsync unchanged ──

    [Fact]
    public async Task SetCommunityModerator_Passes_CompleteRoleAndScopeSet_ToCoreSeam()
    {
        var identity = Substitute.For<IIdentityService>();
        var controller = Build(identity, Substitute.For<IUserInfoService>());

        var model = new CommunityModeratorFormViewModel
        {
            ComponentId  = CompId,
            UserId       = TargetSubject,
            RoleNames    = [Roles.Moderator],
            ComponentIds = [CompId, OtherComp],
        };

        try { await controller.SetCommunityModerator(model); }
        catch { /* expected */ }

        await identity.Received(1).SetRoleAsync(
            targetSubjectId: TargetSubject,
            adminSubjectId:  AdminSubject,
            roles:           Arg.Is<IReadOnlyCollection<string>>(s => s.Contains(Roles.Moderator)),
            componentIds:    Arg.Is<IReadOnlyList<string>>(l => l.Contains(CompId) && l.Contains(OtherComp)));
        // The moderator lane never touches the membership row (the
        // posting right is a distinct data lane — AddCommunityUser).
        await Substitute.For<IUserInfoService>().DidNotReceiveWithAnyArgs().AddCommunityMemberAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>());
    }

    [Fact]
    public async Task SetCommunityModerator_BlankArgs_NeverInvokesService()
    {
        var identity = Substitute.For<IIdentityService>();
        var controller = Build(identity, Substitute.For<IUserInfoService>());

        var model = new CommunityModeratorFormViewModel { ComponentId = string.Empty, UserId = string.Empty };
        try { await controller.SetCommunityModerator(model); }
        catch { /* expected */ }

        await identity.DidNotReceiveWithAnyArgs().SetRoleAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<IReadOnlyList<string>>());
    }

    // ── UnsetCommunityModerator — the set-lane: the complete desired role
    // set (without Moderator) + the scope set (without this community)
    // pass through unchanged ──

    [Fact]
    public async Task UnsetCommunityModerator_Passes_Without_ModeratorRole_And_Without_ThisCommunity()
    {
        var identity = Substitute.For<IIdentityService>();
        var controller = Build(identity, Substitute.For<IUserInfoService>());

        var model = new CommunityModeratorFormViewModel
        {
            ComponentId  = CompId,
            UserId       = TargetSubject,
            // The role set no longer carries Moderator (other roles may
            // still be present — the form preserves them).
            RoleNames    = [Roles.Translator],
            // The scope set no longer carries CompId (the target still
            // moderates OtherComp — preserved).
            ComponentIds = [OtherComp],
        };

        try { await controller.UnsetCommunityModerator(model); }
        catch { /* expected */ }

        await identity.Received(1).SetRoleAsync(
            targetSubjectId: TargetSubject,
            adminSubjectId:  AdminSubject,
            roles:           Arg.Is<IReadOnlyCollection<string>>(s => s.Contains(Roles.Translator) && !s.Contains(Roles.Moderator)),
            componentIds:    Arg.Is<IReadOnlyList<string>>(l => l.Contains(OtherComp) && !l.Contains(CompId)));
    }

    // ── Harness (mirrors AdminControllerSetRoleTests.Build) ─────────────────

    private static AdminController Build(IIdentityService identity, IUserInfoService userInfo)
    {
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=_test_never_opened;Username=_;******")
                .Options);

        var controller = new AdminController(
            identities: db,
            store:      Substitute.For<IDocumentStore>(),
            identity:   identity,
            userInfo:   userInfo,
            pages:      Substitute.For<IPageService>());

        var claims = new List<Claim>
        {
            new(KumunitaClaimTypes.Subject, AdminSubject),
            new(KumunitaClaimTypes.Role,    Roles.GlobalAdmin),
        };

        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new ServiceCollection().BuildServiceProvider();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        return controller;
    }
}
