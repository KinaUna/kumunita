using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using KumunitaClaimTypes = Kumunita.Core.Identity.ClaimTypes;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0142 — the admin-removal lane's Web guards. <see cref
/// "AdminController.DeleteAccount"/> (the GlobalAdmin surface
/// <c>POST /admin/delete</c>) must refuse a request where the target's
/// subject id equals the admin's own subject id, <em>before</em> touching
/// the Core lane (<see cref="IIdentityService.DeleteAccountAsync"/>) — a
/// GlobalAdmin who wants to leave uses their own
/// <c>POST /account/delete</c> lane (the self-serve branch,
/// <c>Via: Owner</c>), not the admin lane. The Block self-guard
/// (<see cref="AdminControllerBlockTests"/>) is the precedent: the guard's
/// effect is observable purely from NSubstitute's call log, so the tests
/// drive the action to completion and swallow the expected Url NRE — the
/// assertion lives on <c>identity.DeleteAccountAsync.Received*</c>.
/// <para>
/// Also pins:
/// <list type="number">
/// <item>An admin deleting a <em>different</em> resident — the Core lane IS
///       invoked, with <c>(target, admin)</c>.</item>
/// <item>The self-serve <see cref
/// "AccountController.Delete"/> POST refuses a wrong password (the
/// CheckPasswordAsync seam, the ChangePassword precedent) and a
/// non-acknowledged checkbox — in both cases the Core lane is NOT called
/// (the account is untouched).</item>
/// </list>
/// </para>
/// </summary>
public class AdminControllerDeleteAccountTests
{
    // ── Self-delete guard (the Block self-guard precedent) ─────────────────

    /// <summary>
    /// The rule: an admin cannot delete themselves through the admin lane.
    /// The guard fires before the Core admin lane, so
    /// <see cref="IIdentityService.DeleteAccountAsync"/> must NOT be invoked.
    /// </summary>
    [Fact]
    public async Task DeleteAccount_When_Self_Does_Not_Call_Core()
    {
        const string admin = "subj-admin-001";
        var (controller, identity) = Build(admin);

        try { await controller.DeleteAccount(admin); } catch { /* expected */ }

        await identity.DidNotReceiveWithAnyArgs()
            .DeleteAccountAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    // ── Happy path — admin deletes a different resident ────────────────────

    /// <summary>
    /// An admin deleting a different resident: the Core lane IS invoked once,
    /// with <c>(target, admin)</c> — the admin-initiated branch (actor ≠
    /// target, <c>Via: Admin</c>).
    /// </summary>
    [Fact]
    public async Task DeleteAccount_When_DifferentSubject_Calls_Core_WithBothIds()
    {
        const string admin  = "subj-admin-001";
        const string target = "subj-resident-001";
        var (controller, identity) = Build(admin);

        try { await controller.DeleteAccount(target); } catch { /* expected */ }

        await identity.Received(1).DeleteAccountAsync(target, admin);
    }

    // ── The self-serve POST /account/delete guards (AccountController) ─────

    /// <summary>
    /// A wrong current password refuses the self-serve delete (the
    /// ChangePassword POST precedent): the Core lane is NOT invoked (the
    /// account is untouched).
    /// </summary>
    [Fact]
    public async Task Account_Delete_Post_WrongPassword_Does_Not_Call_Core()
    {
        const string subject = "subj-resident-001";
        var (controller, identity, userManager) = BuildAccount(subject);
        userManager.CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>()!)
            .Returns(false);

        var model = new DeleteAccountViewModel
        {
            Password  = "wrong-password",
            Confirmed = true
        };

        // The POST re-renders the form (a model error is added). The harness
        // has no view infrastructure, so the View(model) call NREs *after*
        // the guard; the assertion lives on the NSubstitute call log.
        try { await controller.Delete(model); } catch { /* expected */ }

        await identity.DidNotReceiveWithAnyArgs()
            .DeleteAccountAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// An unchecked acknowledgment checkbox refuses the self-serve delete:
    /// the Core lane is NOT invoked (the dangerous-action guard).
    /// </summary>
    [Fact]
    public async Task Account_Delete_Post_Unconfirmed_Does_Not_Call_Core()
    {
        const string subject = "subj-resident-001";
        var (controller, identity, _) = BuildAccount(subject);

        var model = new DeleteAccountViewModel
        {
            Password  = "correct-password",
            Confirmed = false    // the checkbox is unchecked
        };

        try { await controller.Delete(model); } catch { /* expected */ }

        await identity.DidNotReceiveWithAnyArgs()
            .DeleteAccountAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// The happy path: correct password + checked box → the Core lane IS
    /// invoked with <c>(subject, subject)</c> — the self-deletion branch
    /// (actor == target, <c>Via: Owner</c>).
    /// </summary>
    [Fact]
    public async Task Account_Delete_Post_Valid_Calls_Core_SelfBranch()
    {
        const string subject = "subj-resident-001";
        var (controller, identity, userManager) = BuildAccount(subject);
        userManager.CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>()!)
            .Returns(true);

        var model = new DeleteAccountViewModel
        {
            Password  = "correct-password",
            Confirmed = true
        };

        try { await controller.Delete(model); } catch { /* expected */ }

        await identity.Received(1).DeleteAccountAsync(subject, subject);
    }

    // ── Harnesses ──────────────────────────────────────────────────────────

    private static (AdminController controller, IIdentityService identity) Build(string adminSubjectId)
    {
        var identity = Substitute.For<IIdentityService>();

        // The AppDbContext is never queried by these lanes (the self-delete
        // check is a plain Ordinal string comparison; the happy paths
        // delegate straight to Core), so a dummy Npgsql connection string is
        // enough to satisfy the constructor (the AdminControllerBlockTests
        // precedent).
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=_test_never_opened;Username=_;******")
                .Options);

        var controller = new AdminController(
            identities: db,
            store:      Substitute.For<IDocumentStore>(),
            identity:   identity,
            userInfo:   Substitute.For<IUserInfoService>(),
            pages:      Substitute.For<IPageService>());

        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new ServiceCollection().BuildServiceProvider();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(KumunitaClaimTypes.Subject, adminSubjectId) }));
        controller.ControllerContext =
            new ControllerContext { HttpContext = ctx };

        return (controller, identity);
    }

    private static (AccountController controller, IIdentityService identity, UserManager<User> userManager)
        BuildAccount(string subjectId)
    {
        var identity = Substitute.For<IIdentityService>();
        // UserManager has no parameterless constructor (NSubstitute can't
        // Substitute.For<T>() it bare). Construct it with substituted
        // dependencies (the AccountControllerChangePasswordTests harness
        // shape) so we can set up CheckPasswordAsync / FindByIdAsync.
        var userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UserManager<User>>.Instance);
        userManager.FindByIdAsync(subjectId)
            .Returns(new User { Id = subjectId });

        var controller = new AccountController(
            signInManager: Substitute.For<SignInManager<User>>(
                userManager,
                Substitute.For<IHttpContextAccessor>(),
                Substitute.For<IUserClaimsPrincipalFactory<User>>(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<SignInManager<User>>.Instance,
                Substitute.For<IAuthenticationSchemeProvider>(),
                Substitute.For<IUserConfirmation<User>>()),
            userManager:   userManager,
            identity:      identity,
            userInfo:      Substitute.For<IUserInfoService>(),
            store:         Substitute.For<IDocumentStore>());

        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new ServiceCollection().BuildServiceProvider();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(KumunitaClaimTypes.Subject, subjectId) }));
        controller.ControllerContext =
            new ControllerContext { HttpContext = ctx };

        return (controller, identity, userManager);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
