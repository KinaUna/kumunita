using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0143 — the guardian delete-child lane's Web guards.
/// <see cref="GuardianController.DeleteChild"/>
/// (POST <c>me/children/{childId}/delete</c>) must:
/// <list type="bullet">
/// <item>Refuse a non-guardian's POST before touching the Core lane
///       (<see cref="IIdentityService.DeleteChildAccountAsync"/>) — the
///       standing gate (an active link for the pair, else
///       <see cref="NotFound()"/>, the ADR 0012/0013 "a non-guardian learns
///       nothing" shape). The <see cref="GuardianAssignmentTests"/>
///       non-guardian test is the precedent: the standing gate runs first,
///       so the Core seam must NOT be invoked.</item>
/// <item>Refuse an unchecked acknowledgment checkbox (the dangerous-action
///       guard) — the Core lane is NOT invoked (the account is untouched,
///       the ADR 0142 <c>DeleteAccountViewModel.Confirmed</c> precedent).</item>
/// <item>Invoke the Core lane once with <c>(childId, subject)</c> on the
///       happy path — the single audited write (via: Guardian, the ADR 0028
///       "narrower standing" rule).</item>
/// <item>Surface a 404 when the Core lane re-gates the standing (the
///       fail-closed pin — a crafted POST that slips past the Web guard
///       reaches the seam directly).</item>
/// </list>
/// <para>
/// The assertion lives on <c>identity.DeleteChildAccountAsync
/// .Received*</c> (the <c>AdminControllerDeleteAccountTests</c> precedent):
/// the guard's effect is observable purely from NSubstitute's call log, so
/// the tests drive the action and swallow any expected Url NRE (the
/// harness has no view infrastructure; the <c>RedirectToAction</c> NRE is
/// expected and irrelevant to the assertion target).
/// </para>
/// </summary>
public sealed class GuardianDeleteChildTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Guardian = "gd-guardian-001";
    private const string Child = "gd-child-001";
    private const string NonGuardian = "gd-non-guardian-001";

    // ── A non-guardian's POST is refused (404, the Core seam not invoked) ───

    [Fact]
    public async Task DeleteChild_NonGuardian_Returns404_DoesNotCallCore()
    {
        var (controller, identity) = await BuildAsync(
            actorSubjectId: NonGuardian, seedGuardianLink: false);

        // No active link for (NonGuardian, Child) → the standing gate
        // (ActiveLinkAsync) returns null → 404, before the Core seam.
        var result = await controller.DeleteChild(
            Child, new GuardianDeleteChildForm { Confirmed = true });

        Assert.IsType<NotFoundResult>(result);
        await identity.DidNotReceiveWithAnyArgs()
            .DeleteChildAccountAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    // ── Unchecked checkbox refuses (the Core seam is NOT invoked) ──────────

    [Fact]
    public async Task DeleteChild_UncheckedConfirmed_DoesNotCallCore()
    {
        var (controller, identity) = await BuildAsync();

        // The guardian holds an active link (the standing gate passes), but
        // the dangerous-action guard (the Confirmed checkbox) refuses
        // before the Core lane is reached.
        var result = await controller.DeleteChild(
            Child, new GuardianDeleteChildForm { Confirmed = false });

        // The action redirects to Detail with the form's error (the TempData
        // ["error"] the _FlashToast surfaces — the Suspend / Unsuspend /
        // Dissolve precedent in this controller).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        await identity.DidNotReceiveWithAnyArgs()
            .DeleteChildAccountAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    // ── Happy path — the Core seam IS invoked with (child, guardian) ───────

    [Fact]
    public async Task DeleteChild_HappyPath_CallsCoreWithChildAndGuardian()
    {
        var (controller, identity) = await BuildAsync();

        var result = await controller.DeleteChild(
            Child, new GuardianDeleteChildForm { Confirmed = true });

        // The happy path redirects to Index (the child is no longer in the
        // guardian's list — the Dissolve action's redirect precedent).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);

        // The single audited write (via: Guardian — the ADR 0028 "narrower
        // standing" rule) is invoked once with (child, guardian).
        await identity.Received(1)
            .DeleteChildAccountAsync(Child, Guardian);
    }

    // ── Core seam re-gates (UnauthorizedAccessException) → 404 ──────────────

    [Fact]
    public async Task DeleteChild_CoreSeamRefused_Returns404()
    {
        var (controller, identity) = await BuildAsync();

        // The Web standing gate passes (an active link is seeded), but the
        // Core seam re-gates (the fail-closed pin) — simulate by having the
        // seam throw UnauthorizedAccessException (the Web surfaces a 404).
        identity.DeleteChildAccountAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Faulted(new UnauthorizedAccessException(
                "No active guardian link for (guardian, child).")));

        var result = await controller.DeleteChild(
            Child, new GuardianDeleteChildForm { Confirmed = true });

        Assert.IsType<NotFoundResult>(result);
    }

    // ── Core seam refuses (InvalidOperationException) → redirect with error ─

    [Fact]
    public async Task DeleteChild_CoreSeamThrowsInvalidState_RedirectsToIndexWithError()
    {
        var (controller, identity) = await BuildAsync();

        // The Core seam throws InvalidOperationException (the idempotency
        // pin — a second call for an already-deleted child — or the
        // account no longer exists). The Web surfaces the message on the
        // Index page's TempData error surface (the Suspend / Unsuspend /
        // Dissolve precedent in this controller).
        identity.DeleteChildAccountAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Faulted(new InvalidOperationException("No account.")));

        var result = await controller.DeleteChild(
            Child, new GuardianDeleteChildForm { Confirmed = true });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.NotNull(controller.TempData?["error"]);
    }

    // ── Harness (the GuardianAssignmentTests BuildAsync shape, verbatim) ────

    private async Task<(GuardianController controller, IIdentityService identity)> BuildAsync(
        string actorSubjectId = Guardian,
        bool seedGuardianLink = true)
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        if (seedGuardianLink)
        {
            await using var session = store.OpenSession(new Marten.Services.SessionOptions());
            session.Store(new GuardianLink
            {
                Id = Guid.NewGuid().ToString("N"),
                GuardianId = Guardian,
                ChildId = Child,
                Status = GuardianLinkStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await session.SaveChangesAsync(ct);
        }

        var userInfo = new UserInfoService(store);
        var identity = Substitute.For<IIdentityService>();

        var controller = new GuardianController(userInfo, identity, store);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(
                    ClaimTypes.Subject, actorSubjectId) },
                authenticationType: "test"));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return (controller, identity);
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    /// <summary>
    /// A faulted <see cref="Task"/> (the bare-Task NSubstitute idiom —
    /// <c>Task.FromException</c> only exists for <c>Task&lt;T&gt;</c>).
    /// </summary>
    private static Task Faulted(Exception ex)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetException(ex);
        return tcs.Task;
    }
}
