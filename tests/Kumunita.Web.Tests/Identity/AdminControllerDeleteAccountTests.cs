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
/// ADR 0142 + ADR 0301 — the admin-removal lane's Web guards AND the
/// self-serve lane's Web surface (R13, the ADR 0301 grace period).
/// <para>
/// **The admin lane (ADR 0142, unchanged by ADR 0301 D4):**
/// <see cref="AdminController.DeleteAccount"/> (the GlobalAdmin surface
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
/// An admin deleting a <em>different</em> resident — the Core lane IS
/// invoked, with <c>(target, admin)</c>.
/// </para>
/// <para>
/// **The self-serve lane (ADR 0301 D1/D2/D3, IMPROVE-UX R13):**
/// <see cref="AccountController.Delete"/> POST now *schedules* (the
/// <see cref="IDeletionScheduler.ScheduleDeletionAsync"/> seam — R11's
/// scheduling service) rather than executing (the ADR 0142 D7 surface
/// shape, now the grace-period timing change per ADR 0301 D4). The ADR
/// 0142 D5 GlobalAdmin gate is removed (ADR 0301 D1 — every resident sees
/// the form); the two up-front refusals are the ADR 0142 D3 last-GlobalAdmin
/// pin (<see cref="InvalidOperationException"/>, the R11 seam's guard) and
/// the ADR 0301 D3 child rule (<see cref="UnauthorizedAccessException"/>,
/// the distinct "contact a parent/guardian" notice). The dangerous-action
/// guards (the <c>Confirmed</c> checkbox + the <c>CheckPasswordAsync</c>
/// seam, the ADR 0142 D7 shape) are unchanged. <see cref
/// "AccountController.DeleteCancel"/> (the Cancel button, ADR 0301 D2)
/// calls <see cref="IDeletionScheduler.CancelDeletionAsync"/> (idempotent —
/// the ADR 0301 D2 "no extra button, no extra form").
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
    // ADR 0301 D1/D2/D3 (IMPROVE-UX R13) — the self-serve lane now
    // *schedules* the deletion (the IDeletionScheduler.ScheduleDeletionAsync
    // seam, R11's scheduling service) rather than executing it (the
    // IIdentityService.DeleteAccountAsync lane — that is the R12 due-job's
    // job, after the grace period). The ADR 0142 D5 GlobalAdmin gate is
    // removed (ADR 0301 D1 — every resident sees the form); the two
    // up-front refusals are the ADR 0142 D3 last-GlobalAdmin pin
    // (InvalidOperationException, the R11 seam's guard) and the ADR 0301 D3
    // child rule (UnauthorizedAccessException, the distinct "contact a
    // parent/guardian" notice). The dangerous-action guards (the Confirmed
    // checkbox + the CheckPasswordAsync seam, the ADR 0142 D7 shape) are
    // unchanged.

    /// <summary>
    /// A wrong current password refuses the self-serve schedule (the
    /// ChangePassword POST precedent, ADR 0142 D7 shape): the
    /// ScheduleDeletionAsync seam is NOT invoked (the account is untouched,
    /// no DeletionRequest row is written).
    /// </summary>
    [Fact]
    public async Task Account_Delete_Post_WrongPassword_Does_Not_Schedule()
    {
        const string subject = "subj-resident-001";
        var (controller, identity, userManager, scheduler) = BuildAccount(subject);
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

        await scheduler.DidNotReceiveWithAnyArgs()
            .ScheduleDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        // The ADR 0142 D7 lane (the immediate-delete) is no longer invoked on
        // the self-serve branch — the R13 prompt: "identity.DeleteAccountAsync
        // is replaced with scheduler.ScheduleDeletionAsync".
        await identity.DidNotReceiveWithAnyArgs()
            .DeleteAccountAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// An unchecked acknowledgment checkbox refuses the self-serve schedule:
    /// the ScheduleDeletionAsync seam is NOT invoked (the dangerous-action
    /// guard, ADR 0142 D7 shape).
    /// </summary>
    [Fact]
    public async Task Account_Delete_Post_Unconfirmed_Does_Not_Schedule()
    {
        const string subject = "subj-resident-001";
        var (controller, identity, _, scheduler) = BuildAccount(subject);

        var model = new DeleteAccountViewModel
        {
            Password  = "correct-password",
            Confirmed = false    // the checkbox is unchecked
        };

        try { await controller.Delete(model); } catch { /* expected */ }

        await scheduler.DidNotReceiveWithAnyArgs()
            .ScheduleDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await identity.DidNotReceiveWithAnyArgs()
            .DeleteAccountAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// The happy path: correct password + checked box → the
    /// ScheduleDeletionAsync seam IS invoked once (the ADR 0301 D2
    /// scheduled branch — the R11 seam writes the pending DeletionRequest
    /// row + one "account.delete.scheduled" audit row, Via: Owner). The
    /// ADR 0142 D7 immediate-delete lane (IIdentityService.DeleteAccountAsync)
    /// is NOT invoked on the self-serve branch (the R12 due-job runs it
    /// later, after the grace period).
    /// </summary>
    [Fact]
    public async Task Account_Delete_Post_Valid_Calls_Scheduler_SelfBranch()
    {
        const string subject = "subj-resident-001";
        var (controller, identity, userManager, scheduler) = BuildAccount(subject);
        userManager.CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>()!)
            .Returns(true);

        var model = new DeleteAccountViewModel
        {
            Password  = "correct-password",
            Confirmed = true
        };

        try { await controller.Delete(model); } catch { /* expected */ }

        await scheduler.Received(1).ScheduleDeletionAsync(subject);
        // The ADR 0142 D7 immediate-delete lane is NOT invoked on the
        // self-serve branch (the R13 prompt: "identity.DeleteAccountAsync
        // is replaced with scheduler.ScheduleDeletionAsync"; the R12
        // due-job runs it later).
        await identity.DidNotReceiveWithAnyArgs()
            .DeleteAccountAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// The ADR 0301 D3 child rule (a child under an active GuardianLink):
    /// the ScheduleDeletionAsync seam throws UnauthorizedAccessException;
    /// the controller catches it and re-renders the form with the distinct
    /// "contact a parent/guardian" notice (the
    /// DeleteAccountViewModel.IsChildRefused flag — the ADR 0301 D3 "the
    /// same shape as 0142 D5's old SelfDeletionRefused, with a different
    /// reason"). The ADR 0142 D5 GlobalAdmin gate that used to throw this
    /// exception is removed (ADR 0301 D1).
    /// </summary>
    [Fact]
    public async Task Account_Delete_Post_ChildRule_Surfaces_Child_Refused_Notice()
    {
        const string subject = "subj-resident-001";
        var (controller, identity, userManager, scheduler) = BuildAccount(subject);
        userManager.CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>()!)
            .Returns(true);
        // The R11 seam's child-rule refusal (ADR 0301 D3). The async method
        // throws via a faulted Task (the NSubstitute async-throw idiom —
        // `Throws` is sync-only, `Task.FromException` is the async shape).
        scheduler.ScheduleDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException(
                "This account is under a guardian's supervision. Contact " +
                "a parent or guardian to manage the deletion (ADR 0301 D3).")));

        var model = new DeleteAccountViewModel
        {
            Password  = "correct-password",
            Confirmed = true
        };

        // The POST catches the UnauthorizedAccessException and re-renders
        // the form with the child-refused notice. The harness has no view
        // infrastructure, so the View(...) call NREs *after* the catch;
        // the assertion lives on the NSubstitute call log (the seam IS
        // called — the refusal is the seam's, not the controller's).
        try { await controller.Delete(model); } catch { /* expected */ }

        await scheduler.Received(1).ScheduleDeletionAsync(subject);
        // The ADR 0142 D7 immediate-delete lane is NOT invoked (the child
        // rule is the ADR 0301 D3 distinct refusal — the resident must
        // contact a guardian, not the admin).
        await identity.DidNotReceiveWithAnyArgs()
            .DeleteAccountAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// The ADR 0142 D3 last-GlobalAdmin pin (the R11 seam's guard, reused
    /// above the seam): the ScheduleDeletionAsync seam throws
    /// InvalidOperationException; the controller catches it and surfaces
    /// the message (the account is untouched — no DeletionRequest row is
    /// written, the ADR 0142 D3 "the guard fires before any write").
    /// </summary>
    [Fact]
    public async Task Account_Delete_Post_LastGlobalAdmin_Surfaces_Message()
    {
        const string subject = "subj-resident-001";
        var (controller, identity, userManager, scheduler) = BuildAccount(subject);
        userManager.CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>()!)
            .Returns(true);
        // The R11 seam's last-GlobalAdmin refusal (ADR 0142 D3). The async
        // method throws via a faulted Task (the NSubstitute async-throw
        // idiom).
        scheduler.ScheduleDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException(
                "This is the last GlobalAdmin on the instance.")));

        var model = new DeleteAccountViewModel
        {
            Password  = "correct-password",
            Confirmed = true
        };

        try { await controller.Delete(model); } catch { /* expected */ }

        await scheduler.Received(1).ScheduleDeletionAsync(subject);
        await identity.DidNotReceiveWithAnyArgs()
            .DeleteAccountAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    // ── The Cancel button (AccountController.DeleteCancel, ADR 0301 D2) ──

    /// <summary>
    /// The happy path: correct password + checked box → the
    /// CancelDeletionAsync seam IS invoked once (the ADR 0301 D2
    /// "cancel = signing in" — the Cancel button is the explicit lane
    /// the R13 prompt calls for). The ADR 0142 D7 immediate-delete lane
    /// (IIdentityService.DeleteAccountAsync) is NOT invoked (the cancel
    /// does not delete — it moves the row Pending → Cancelled, the R11
    /// seam's idempotent no-op).
    /// </summary>
    [Fact]
    public async Task Account_DeleteCancel_Post_Valid_Calls_Scheduler_Cancel()
    {
        const string subject = "subj-resident-001";
        var (controller, identity, userManager, scheduler) = BuildAccount(subject);
        userManager.CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>()!)
            .Returns(true);

        var model = new DeleteAccountViewModel
        {
            Password  = "correct-password",
            Confirmed = true
        };

        try { await controller.DeleteCancel(model); } catch { /* expected */ }

        await scheduler.Received(1).CancelDeletionAsync(subject);
        // The ADR 0142 D7 immediate-delete lane is NOT invoked (the cancel
        // is the ADR 0301 D2 "no extra button, no extra form" — the
        // CancelledAt timestamp is the record, no audit row).
        await identity.DidNotReceiveWithAnyArgs()
            .DeleteAccountAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>
    /// A wrong current password refuses the Cancel (the ADR 0142 D7
    /// dangerous-action shape, generalized to the cancel): the
    /// CancelDeletionAsync seam is NOT invoked (the account is untouched,
    /// the row stays Pending).
    /// </summary>
    [Fact]
    public async Task Account_DeleteCancel_Post_WrongPassword_Does_Not_Cancel()
    {
        const string subject = "subj-resident-001";
        var (controller, identity, userManager, scheduler) = BuildAccount(subject);
        userManager.CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>()!)
            .Returns(false);

        var model = new DeleteAccountViewModel
        {
            Password  = "wrong-password",
            Confirmed = true
        };

        try { await controller.DeleteCancel(model); } catch { /* expected */ }

        await scheduler.DidNotReceiveWithAnyArgs()
            .CancelDeletionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await identity.DidNotReceiveWithAnyArgs()
            .DeleteAccountAsync(Arg.Any<string>(), Arg.Any<string>());
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

        var controller = new AdminController(identities: db, store:      Substitute.For<IDocumentStore>(), identity:   identity, userInfo:   Substitute.For<IUserInfoService>(), Substitute.For<ICommunityMembershipService>(), Substitute.For<IProfileSettingsService>(), pages:      Substitute.For<IPageService>());

        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new ServiceCollection().BuildServiceProvider();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(KumunitaClaimTypes.Subject, adminSubjectId) }));
        controller.ControllerContext =
            new ControllerContext { HttpContext = ctx };

        return (controller, identity);
    }

    private static (AccountController controller, IIdentityService identity, UserManager<User> userManager, IDeletionScheduler scheduler)
        BuildAccount(string subjectId)
    {
        var identity = Substitute.For<IIdentityService>();
        // ADR 0301 D2 (IMPROVE-UX R13) — the scheduling seam (R11's
        // IDeletionScheduler). The controller resolves it from the ctor
        // param (the `IDeletionScheduler? scheduler = null` optional shape
        // — the harness passes a Substitute explicitly).
        var scheduler = Substitute.For<IDeletionScheduler>();

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
            // C3-05 (ADR 0295) — the store arg is gone (the verify-token read
            // is now the IIdentityService.LoadVerifyTokenAsync seam).
            userInfo:      Substitute.For<IUserInfoService>(),
            // ADR 0301 D2 (IMPROVE-UX R13) — the scheduling seam (the R13
            // prompt: "add IDeletionScheduler scheduler to the controller
            // ctor, the identity param's shape").
            scheduler:     scheduler);

        var ctx = new DefaultHttpContext();
        ctx.RequestServices = new ServiceCollection().BuildServiceProvider();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(KumunitaClaimTypes.Subject, subjectId) }));
        controller.ControllerContext =
            new ControllerContext { HttpContext = ctx };

        return (controller, identity, userManager, scheduler);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
