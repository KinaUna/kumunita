using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using KumunitaClaimTypes = Kumunita.Core.Identity.ClaimTypes;

namespace Kumunita.Web.Tests;

/// <summary>
/// The admin shell's self-block guard: <see cref="AdminController.Block"/> must refuse a
/// request where the target's subject id equals the admin's own subject id, <em>before</em>
/// touching the Core admin lane (<see cref="IIdentityService.BlockAsync"/> — no Profile.Blocked
/// flip, no audit row, no security-stamp rotation). A GlobalAdmin who blocked themselves would
/// leave themselves standing-less (Member/Moderator/GlobalAdmin all stripped at the seam) — and
/// if they were the only GlobalAdmin, they could not self-restore through the /admin surface.
/// <para>
/// Also pins:
/// <list type="number">
/// <item>An admin blocking a <em>different</em> resident — the Core lane IS invoked, with
///       <c>(target, admin)</c> (the <see cref="IIdentityService.BlockAsync(string, string)"/>
///       ordered signature).</item>
/// <item>Unblocking an account (including the admin's own — the Unblock lane has <em>no</em>
///       self-guard by design) — the Core lane IS invoked with <c>(target, admin)</c>.</item>
/// </list>
/// </para>
/// <para>
/// Both actions end in <c>RedirectToAction(nameof(Index))</c> (which requires the MVC Url
/// pipeline the raw-controller test harness doesn't set up). The guard's effect is observable
/// purely from NSubstitute's call log, so the tests drive the action to completion and swallow
/// the expected Url NRE — the assertion lives on <c>identity.*.Received*</c>, not on the
/// returned IAction.
/// </para>
/// </summary>
public class AdminControllerBlockTests
{
    // ── Self-block guard ─────────────────────────────────────────────────────

    /// <summary>
    /// The rule: an admin cannot block themselves. The guard fires before the Core
    /// admin lane, so <see cref="IIdentityService.BlockAsync"/> must NOT be invoked.
    /// </summary>
    [Fact]
    public async Task Block_When_Self_Does_Not_Call_Core()
    {
        const string admin = "subj-admin-001";
        var (controller, identity) = Build(admin);

        try { await controller.Block(admin); } catch { /* expected: guard refuses, then
                                                        Url-action NRE — we don't assert that */ }

        await identity.DidNotReceiveWithAnyArgs().BlockAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    // ── Happy path — Block a different resident ─────────────────────────────

    /// <summary>
    /// An admin blocking a different resident: the Core lane IS invoked once, with
    /// <c>(target, admin)</c> — the <see cref="IIdentityService.BlockAsync(string, string)"/>
    /// signature.
    /// </summary>
    [Fact]
    public async Task Block_When_DifferentSubject_Calls_Core_WithBothIds()
    {
        const string admin  = "subj-admin-001";
        const string target = "subj-resident-001";
        var (controller, identity) = Build(admin);

        try { await controller.Block(target); } catch { /* expected */ }

        await identity.Received(1).BlockAsync(target, admin);
    }

    // ── Unblock — no self-guard (an admin can self-restore) ─────────────────

    /// <summary>
    /// The Unblock lane deliberately has no self-guard: if another admin (or an earlier
    /// misstep) blocked this admin, they must be able to restore their own standing
    /// through the /admin surface.
    /// </summary>
    [Fact]
    public async Task Unblock_When_Self_Calls_Core_WithBothIds()
    {
        const string admin = "subj-admin-001";
        var (controller, identity) = Build(admin);

        try { await controller.Unblock(admin); } catch { /* expected */ }

        await identity.Received(1).UnblockAsync(admin, admin);
    }

    /// <summary>
    /// Unblock of a different resident — the happy-path admin lane.
    /// </summary>
    [Fact]
    public async Task Unblock_When_DifferentSubject_Calls_Core_WithBothIds()
    {
        const string admin  = "subj-admin-001";
        const string target = "subj-resident-001";
        var (controller, identity) = Build(admin);

        try { await controller.Unblock(target); } catch { /* expected */ }

        await identity.Received(1).UnblockAsync(target, admin);
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    private static (AdminController controller, IIdentityService identity) Build(string adminSubjectId)
    {
        var identity = Substitute.For<IIdentityService>();

        // The AppDbContext is never queried by these lanes (the self-block check is a
        // plain Ordinal string comparison; the happy paths delegate straight to Core),
        // so a dummy Npgsql connection string is enough to satisfy the constructor — the
        // connection is opened nowhere.
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=_test_never_opened;Username=_;Password=_")
                .Options);

        var controller = new AdminController(
            identities: db,
            store:      Substitute.For<IDocumentStore>(),
            identity:   identity,
            userInfo:   Substitute.For<IUserInfoService>());

        // Controller.User is read-only (derives from Controller.Context.User). Set the
        // DefaultHttpContext's User to a principal whose Subject claim is the admin's —
        // AdminSubjectId(User) = User.FindFirst(KumunitaClaimTypes.Subject).Value.
        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new ServiceCollection().BuildServiceProvider();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(KumunitaClaimTypes.Subject, adminSubjectId) }));
        controller.ControllerContext =
            new ControllerContext { HttpContext = ctx };

        // Note: the controller's TempData writes NRE in this harness (no ISessionStore);
        // that's fine — the NRE lands *after* the Core lane (identity.BlockAsync /
        // UnblockAsync) has been called or not, and the assertion lives on NSubstitute's
        // call log, not on what the controller recorded in TempData.

        return (controller, identity);
    }
}
