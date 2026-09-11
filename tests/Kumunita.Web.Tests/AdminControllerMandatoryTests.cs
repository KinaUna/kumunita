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
/// The <c>/admin</c> mandatory surface (ADR 0012, now duplicated on the
/// admin list): <see cref="AdminController.ToggleCommunityMandatory"/> and the
/// mandatory flag on <see cref="AdminController.AddCommunity"/>. Both reuse the
/// same **GlobalAdmin-only** <see cref="IUserInfoService
/// .SetCommunityMandatoryAsync"/> lane the community's own manage page uses,
/// so the pins hold the standing (the handed-in claim set carries
/// <c>GlobalAdmin</c>) and confirm the add-form composes *create-then-set*
/// (rather than silently no-opping when the box is unchecked).
/// <para>
/// Follows the <see cref="AdminControllerBlockTests"/> harness: an
/// <see cref="IUserInfoService"/> seam substituted with NSubstitute, the
/// principal built from raw claims, and the expected <c>TempData</c>/redirect
/// NRE swallowed — the assertion lives on the substitute's call log, not on
/// the returned <c>IActionResult</c>.
/// </para>
/// </summary>
public class AdminControllerMandatoryTests
{
    private const string AdminSubject = "subj-admin-001";
    private const string CompId = "comp-001";

    // ── ToggleCommunityMandatory (POST) ─────────────────────────────────────

    [Fact]
    public async Task ToggleCommunityMandatory_GlobalAdmin_Delegates_Lane()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo, roles: [Roles.GlobalAdmin]);

        try { await controller.ToggleCommunityMandatory(CompId, true); }
        catch { /* expected: TempData/redirect NRE — assert on the call log */ }

        await userInfo.Received(1).SetCommunityMandatoryAsync(
            CompId, true, AdminSubject,
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)));
    }

    [Fact]
    public async Task ToggleCommunityMandatory_ToOptional_Delegates_False()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo, roles: [Roles.GlobalAdmin]);

        try { await controller.ToggleCommunityMandatory(CompId, false); }
        catch { /* expected */ }

        await userInfo.Received(1).SetCommunityMandatoryAsync(
            CompId, false, AdminSubject,
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)));
    }

    [Fact]
    public async Task ToggleCommunityMandatory_BlankComponentId_NeverInvokesService()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo, roles: [Roles.GlobalAdmin]);

        try { await controller.ToggleCommunityMandatory(string.Empty, true); }
        catch { /* expected: the guard's early redirect NREs */ }

        await userInfo.DidNotReceiveWithAnyArgs().SetCommunityMandatoryAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>());
    }

    // ── AddCommunity (POST) — the mandatory checkbox composes create-then-set ──

    [Fact]
    public async Task AddCommunity_WhenMandatory_Creates_Then_SetsMandatory()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.CreateCommunityAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>())
            .Returns(new Component { Id = CompId, Name = "The Club" });
        var controller = Build(userInfo, roles: [Roles.GlobalAdmin]);

        var model = new AddCommunityViewModel { Name = "The Club", Mandatory = true };
        try { await controller.AddCommunity(model); }
        catch { /* expected: the create-then-set NRE after both calls */ }

        await userInfo.Received(1).CreateCommunityAsync("The Club", null, AdminSubject);
        await userInfo.Received(1).SetCommunityMandatoryAsync(
            CompId, true, AdminSubject,
            Arg.Is<IReadOnlySet<string>>(s => s.Contains(Roles.GlobalAdmin)));
    }

    [Fact]
    public async Task AddCommunity_WhenNotMandatory_DoesNotSetMandatory()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = Build(userInfo, roles: [Roles.GlobalAdmin]);

        var model = new AddCommunityViewModel { Name = "The Club", Mandatory = false };
        try { await controller.AddCommunity(model); }
        catch { /* expected */ }

        await userInfo.Received(1).CreateCommunityAsync("The Club", null, AdminSubject);
        await userInfo.DidNotReceiveWithAnyArgs().SetCommunityMandatoryAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>());
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    private static AdminController Build(IUserInfoService userInfo, string[] roles)
    {
        // The AppDbContext is never queried by these lanes (the toggle is a
        // plain core delegate; the add form composes two core seam calls), so a
        // dummy Npgsql connection string satisfies the constructor — the
        // connection is opened nowhere.
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=_test_never_opened;Username=_;******")
                .Options);

        var controller = new AdminController(
            identities: db,
            store:      Substitute.For<IDocumentStore>(),
            identity:   Substitute.For<IIdentityService>(),
            userInfo:   userInfo);

        // Principal from raw claims: the AdminSubject claim is what
        // AdminSubjectId reads, and the role claim carries GlobalAdmin into
        // KumunitaPrincipal.RoleSet(User) — the very seam both actions hand to
        // Core, so these pins hold the "GlobalAdmin-only" standing rule.
        var claims = new List<Claim> { new(KumunitaClaimTypes.Subject, AdminSubject) };
        claims.AddRange(roles.Select(r => new Claim(KumunitaClaimTypes.Role, r)));

        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new ServiceCollection().BuildServiceProvider();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        return controller;
    }
}
