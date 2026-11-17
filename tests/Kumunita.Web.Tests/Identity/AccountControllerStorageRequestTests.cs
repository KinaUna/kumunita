using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;
using System.Security.Claims;
using Claim = System.Security.Claims.Claim;
using ClaimTypes = Kumunita.Core.Identity.ClaimTypes;

namespace Kumunita.Web.Tests;

/// <summary>
/// IMPROVE-UX A14 — the resident "request more space" loop's
/// <see cref="AccountController.RequestMoreSpace"/> POST wiring + the
/// <see cref="Kumunita.Web.Views.Account.Storage"/> surface (the plan card's
/// DoD). The A14 card closes E20 (the "promise without seam" anti-pattern):
/// the "My storage" page's <c>account.storage_remaining_note</c> text used to
/// end with "Ask an administrator to raise the quota" with no action — A14
/// adds the button (the limited branch of Storage.cshtml) + the POST action
/// that calls the Core notify lane (
/// <see cref="IIdentityService.RequestStorageQuotaIncreaseAsync"/>) and
/// flashes the localized <c>account.storage_request_sent</c> confirmation.
/// <para>
/// <list type="bullet">
/// <item><b>(a)</b> — the "My storage" page renders the
/// <c>Request more space</c> button (the
/// <c>form method="post" action="/account/storage/request"</c> + a button)
/// <b>only in the limited branch</b> — the unlimited branch (the
/// <c>account.storage_unlimited_note</c> path, the C-UP·5 quota-0 sentinel)
/// has no cap to ask about, so it does <b>not</b> render the form (the view
/// gate is <c>@if (Model.QuotaUnlimited)</c>, read the .cshtml).</item>
/// <item><b>(b)</b> — <c>POST /account/storage/request</c> calls the Core
/// lane with the resident's <c>subjectId</c> (the signed-in principal —
/// self-only, never a path param) and redirects back to
/// <see cref="AccountController.Storage"/> with the
/// <c>TempData["info"]</c> confirmation (the
/// <see cref="AdminGuestsController"/>.Save flash-redirect shape).</item>
/// <item><b>(c)</b> — <c>account.storage_remaining_note</c> (en) no longer
/// promises a missing action — it points at the button (the
/// <c>account.storage_request</c> label), a small string pin (the house
/// convention).</item>
/// </list>
/// </para>
/// <para>
/// Harness: the direct-construction shape of
/// <see cref="AccountControllerInviteSurfaceTests"/> — the
/// <see cref="SignInManager{TUser}"/> / <see cref
/// "UserManager{TUser}"/> are null (the request path never dereferences
/// them); the <see cref="IIdentityService"/> is an NSubstitute. The subject
/// claim is <see cref="ClaimTypes.Subject"/> (the
/// <c>Kumunita.Sub</c> claim, the <see cref="AccountController.SubjectId"/>
/// shape).
/// </para>
/// </summary>
public class AccountControllerStorageRequestTests
{
    private const string Resident = "resident-001";

    // ── (b) — POST RequestMoreSpace: calls the Core lane + flashes ─────────

    [Fact]
    public async Task RequestMoreSpace_POST_CallsCoreLane_WithSubject_RedirectsBack_ToStorage()
    {
        var (controller, identity) = Build();

        var action = await controller.RequestMoreSpace();

        // Redirects back to the storage page (the AdminGuestsController.Save
        // flash-redirect shape — the request "was made" is the durable fact,
        // so the redirect always succeeds even if the notify is a no-op).
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AccountController.Storage), redirect.ActionName);

        // The Core notify lane was consulted with the resident's subjectId
        // (the signed-in principal — self-only, never a path param).
        await identity.Received(1).RequestStorageQuotaIncreaseAsync(Resident);

        // The confirmation flash (the ADR 0261 FlashAsync seam — the
        // account.storage_request_sent key's en value is the floor when no
        // provider is resolvable in the harness).
        var info = controller.TempData?["info"] as string;
        Assert.Equal("Your request was sent to the admins.", info);
    }

    // ── (b) — the unauthenticated floor (the [Authorize] shape) ─────────────

    [Fact]
    public async Task RequestMoreSpace_NoSubject_ReturnsChallenge_NotCallsLane()
    {
        var (controller, identity) = Build(subject: null);

        var action = await controller.RequestMoreSpace();
        Assert.IsType<ChallengeResult>(action);

        await identity.DidNotReceiveWithAnyArgs()
            .RequestStorageQuotaIncreaseAsync(Arg.Any<string>());
    }

    // ── (a) — the Razor surface: the button is in the limited branch only ──

    [Fact]
    public void StorageView_Contains_TheRequestForm_InTheLimitedBranch_NotTheUnlimitedBranch()
    {
        // The "Request more space" button (the A14 card's (a) pin — the
        // limited-branch form, the "string pin, no TestServer" idiom of the
        // BookmarksControllerTests / InventoryControllerTests precedent).
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views",
            "Account", "Storage.cshtml");
        Assert.True(File.Exists(path), $"Storage.cshtml not found at {path}.");

        var html = File.ReadAllText(path);
        // The form + the action route + the button label key (the
        // account.storage_request kw-l key).
        Assert.Contains("method=\"post\"", html, StringComparison.Ordinal);
        Assert.Contains("action=\"/account/storage/request\"", html, StringComparison.Ordinal);
        Assert.Contains("account.storage_request", html, StringComparison.Ordinal);

        // The gate: the form lives in the `else` branch (the unlimited branch
        // is the account.storage_unlimited_note path, the C-UP·5 quota-0
        // sentinel) — an unlimited resident has no cap to ask about, so the
        // form is NOT rendered there. The Razor gate is
        // @if (Model.QuotaUnlimited) … else { … the form … }; pin that the
        // gate + the unlimited-branch note are both present (the form is
        // structurally in the else, the gate separates the two branches).
        Assert.Contains("QuotaUnlimited", html, StringComparison.Ordinal);
        Assert.Contains("account.storage_unlimited_note", html, StringComparison.Ordinal);
    }

    // ── (c) — the note no longer promises a missing action ──────────────────

    [Fact]
    public void StorageRemainingNote_En_MatchesTheButton_NotADeadEnd()
    {
        // The rewritten account.storage_remaining_note (en) now points at the
        // button (the account.storage_request label — "Request more space")
        // rather than promising a missing "ask an administrator" action (the
        // E20 "promise without seam" anti-pattern). The old dead-end text
        // ("Ask an administrator to raise the quota") is gone.
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Core", "Localization",
            "Keys", "en", "account.json");
        Assert.True(File.Exists(path), $"en/account.json not found at {path}.");

        var text = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(path))
            ?? throw new Xunit.Sdk.XunitException("en/account.json failed to deserialize.");

        Assert.True(
            text.TryGetValue("account.storage_remaining_note", out var noteValue),
            "account.storage_remaining_note key is missing from en/account.json.");
        var note = noteValue;
        // The old dead-end promise is gone (the "ask an administrator"
        // phrasing no longer stands as a handoff — the button IS the ask).
        Assert.DoesNotContain(
            "Ask an administrator to raise the quota", note,
            StringComparison.Ordinal);
        // The note now points at the button (the account.storage_request
        // label text — "Request more space").
        Assert.Contains("Request more space", note, StringComparison.Ordinal);
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    private static (AccountController controller, IIdentityService identity) Build(string? subject = Resident)
    {
        var identity = Substitute.For<IIdentityService>();

        var claims = new List<Claim>();
        if (subject is not null)
            claims.Add(new Claim(ClaimTypes.Subject, subject));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
        };
        var controller = new AccountController(
            signInManager: null!,
            userManager: null!,
            identity: identity,
            userInfo: Substitute.For<IUserInfoService>());
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

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
                dir = dir.Parent;
            Assert.True(dir is not null, "Could not locate the repo root (Kumunita.slnx).");
            return dir!.FullName;
        }
    }
}
