using Kumunita.Core.Usage;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using System.Security.Claims;
using Claim = System.Security.Claims.Claim;
using ClaimTypes = Kumunita.Core.Identity.ClaimTypes;

namespace Kumunita.Web.Tests;

/// <summary>
/// IMPROVE-UX A15 — the admin half of the storage loop: the
/// <see cref="AdminController.SetStorageQuotaOverride"/> POST lane
/// (<c>POST /admin/accounts/{subjectId}/quota</c>) that sets / clears a
/// per-resident quota override on the account-detail page (A14's "request more
/// space" nudge points at this surface).
/// <para>
/// <list type="bullet">
/// <item><b>(a)</b> — a whole-MiB value sets the override on the resident's
/// <c>subjectId</c> (the route param, never a re-keyed free text), the actor is
/// the signed-in admin, and the flash + redirect-back land on the detail page
/// (the <see cref="AdminStorageController"/>.Save / the <see cref="AccountController.RequestMoreSpace"/>
/// flash-redirect shape).</item>
/// <item><b>(b)</b> — <c>0</c> sets the per-resident <b>unlimited</b> sentinel
/// (the C-UP·5 sentinel preserved per-resident, distinct from "clear").</item>
/// <item><b>(c)</b> — a <b>blank</b> value <b>clears</b> the override (back to
/// the community default) — <c>null</c> is passed to the Core lane.</item>
/// <item><b>(d)</b> — an invalid value (negative / non-numeric) is refused: the
/// Core lane is NOT called and the error flash lands.</item>
/// <item><b>(e)</b> — the Razor surface (Views/Admin/Manage.cshtml) renders the
/// override card: the set form + the clear form + the effective-quota / usage /
/// community-default labels (the house "string pin, no TestServer" idiom of the
/// <see cref="AccountControllerStorageRequestTests"/> / <see cref="BookmarksControllerTests"/>
/// precedent).</item>
/// </list>
/// </para>
/// <para>
/// Harness: the direct-construction shape of
/// <see cref="AdminControllerBlockTests"/> (the <see cref="AppDbContext"/> is a
/// never-opened dummy — the POST lane never touches identity / userInfo; only
/// the optional <see cref="IStorageSettingsService"/> + <c>User</c> are
/// consulted) + the <see cref="AccountControllerStorageRequestTests"/>
/// <see cref="NoOpTempDataProvider"/> so the flash lands in <c>TempData</c>.
/// </para>
/// </summary>
public class AdminStorageQuotaOverrideTests
{
    private const string Admin   = "admin-001";
    private const string Resident = "resident-001";

    // ── (a) — a whole-MiB value sets the override + flashes + redirects back ──

    [Fact]
    public async Task SetStorageQuotaOverride_MiBValue_SetsOverride_WithAdminActor_RedirectsBack()
    {
        var (controller, settings) = Build();

        var action = await controller.SetStorageQuotaOverride(Resident, "100");

        // The Core write lane was consulted with the resident's subjectId (the
        // route param — the target, never re-keyed), the parsed bytes (100 MiB),
        // and the signed-in admin as the actor (ADR 0050 split: the Web boundary
        // carries the standing, the Core lane records the actor).
        await settings.Received(1).SetResidentQuotaOverrideAsync(
            Resident, 100L * 1024L * 1024L, Admin);

        // Redirects back to the detail page the form lives on.
        var redirect = Assert.IsType<RedirectResult>(action);
        Assert.Equal($"/admin/accounts/{Resident}", redirect.Url);

        // The saved-confirmation flash (the ADR 0261 FlashAsync seam — the en
        // value is the floor when no provider is resolvable in the harness).
        Assert.Equal("Quota override saved.", controller.TempData?["info"] as string);
    }

    // ── (b) — 0 sets the per-resident unlimited sentinel ─────────────────────

    [Fact]
    public async Task SetStorageQuotaOverride_Zero_SetsUnlimitedSentinel()
    {
        var (controller, settings) = Build();

        var action = await controller.SetStorageQuotaOverride(Resident, "0");

        // 0 = unlimited for that resident (the C-UP·5 sentinel preserved
        // per-resident) — NOT a clear (a clear passes null).
        await settings.Received(1).SetResidentQuotaOverrideAsync(Resident, 0L, Admin);
        Assert.IsType<RedirectResult>(action);
        Assert.Equal("This account's quota is now unlimited.",
            controller.TempData?["info"] as string);
    }

    // ── (c) — a blank value clears the override (back to the community default) ─

    [Fact]
    public async Task SetStorageQuotaOverride_Blank_ClearsOverride_PassesNull()
    {
        var (controller, settings) = Build();

        var action = await controller.SetStorageQuotaOverride(Resident, "   ");

        // Blank = clear → the Core lane receives null (the override ?? global
        // fallback signal — the resident is back on the community default).
        await settings.Received(1).SetResidentQuotaOverrideAsync(Resident, (long?)null, Admin);
        Assert.IsType<RedirectResult>(action);
        Assert.Equal("Quota override cleared — this account uses the community default.",
            controller.TempData?["info"] as string);
    }

    // ── (d) — an invalid value is refused: the Core lane is NOT called ───────

    [Fact]
    public async Task SetStorageQuotaOverride_InvalidValue_Refused_DoesNotCallCoreLane()
    {
        var (controller, settings) = Build();

        var action = await controller.SetStorageQuotaOverride(Resident, "-5");

        // A negative value is rejected — the Core write lane is never consulted.
        await settings.DidNotReceiveWithAnyArgs()
            .SetResidentQuotaOverrideAsync(Arg.Any<string>(), Arg.Any<long?>(), Arg.Any<string>());

        // Redirects back with the error flash (the value semantics pin).
        var redirect = Assert.IsType<RedirectResult>(action);
        Assert.Equal($"/admin/accounts/{Resident}", redirect.Url);
        Assert.Equal(
            "The quota override must be a whole number of MiB, 0 for unlimited, or left blank to use the community default.",
            controller.TempData?["error"] as string);
    }

    // ── (e) — the Razor surface: the override card (set + clear + labels) ────

    [Fact]
    public void ManageView_Contains_TheStorageOverrideCard_SetAndClearForms_AndLabels()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Admin", "Manage.cshtml");
        Assert.True(File.Exists(path), $"Manage.cshtml not found at {path}.");
        var html = File.ReadAllText(path);

        // The card + the effective / used / community-default label keys.
        Assert.Contains("admin.manage.storage_quota", html, StringComparison.Ordinal);
        Assert.Contains("admin.manage.storage_quota_effective", html, StringComparison.Ordinal);
        Assert.Contains("admin.manage.storage_quota_used", html, StringComparison.Ordinal);
        Assert.Contains("admin.manage.storage_quota_community", html, StringComparison.Ordinal);

        // The set form: posts to the /quota action with the quotaMiBInput field.
        Assert.Contains("/quota\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"quotaMiBInput\"", html, StringComparison.Ordinal);
        Assert.Contains("admin.manage.storage_quota_save", html, StringComparison.Ordinal);

        // The clear form: a blank quotaMiBInput (the controller reads it as
        // "clear → back to the community default"), gated on an active override.
        Assert.Contains("admin.manage.storage_quota_clear", html, StringComparison.Ordinal);

        // The labels are programmatic (a <label for=…> on the input — the a11y
        // 4.1.2 / 1.3.1 house rule), and no inline JS (the CspInlineDiscipline
        // gate — the clear is its own form, not an onclick).
        Assert.Contains("for=\"quota-override-input\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("onclick", html, StringComparison.Ordinal);
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    private static (AdminController controller, IStorageSettingsService settings) Build()
    {
        var settings = Substitute.For<IStorageSettingsService>();

        // The AppDbContext is never queried by the POST lane (the override
        // write delegates straight to the optional IStorageSettingsService
        // seam), so a never-opened dummy Npgsql connection satisfies the
        // constructor — the connection is opened nowhere.
        var db = new Kumunita.Core.Identity.AppDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Kumunita.Core.Identity.AppDbContext>()
                .UseNpgsql("Host=localhost;Database=_test_never_opened;Username=_;Password=_")
                .Options);

        var controller = new AdminController(
            identities: db,
            store:      Substitute.For<Marten.IDocumentStore>(),
            identity:   Substitute.For<Kumunita.Core.Identity.IIdentityService>(),
            userInfo:   Substitute.For<Kumunita.Core.UserInfo.IUserInfoService>(),
            community:  Substitute.For<Kumunita.Core.UserInfo.ICommunityMembershipService>(),
            profileSettings: Substitute.For<Kumunita.Core.UserInfo.IProfileSettingsService>(),
            pages:      Substitute.For<Kumunita.Core.Pages.IPageService>(),
            storageSettings: settings);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Subject, Admin) }, "test")),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return (controller, settings);
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
