using System.IO;
using Kumunita.Core.Identity;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using System.Security.Claims;

namespace Kumunita.Web.Tests;

/// <summary>
/// Verifies <see cref="AdminSignupController"/> (ADR 0050) — the GlobalAdmin's
/// sign-up gate control plane (open vs. invitation-only). The pin:
/// <list type="bullet">
/// <item><b>Index</b> — the view model carries the current gate
/// (<c>IsSignupOpenAsync</c>, the <c>false</c> floor — a fresh instance ships
/// invitation-only).</item>
/// <item><b>Save (open)</b> — calls
/// <c>SetSignupOpenAsync(true, actor)</c> and redirects to Index.</item>
/// <item><b>Save (invitation-only)</b> — calls
/// <c>SetSignupOpenAsync(false, actor)</c> and redirects to Index.</item>
/// </list>
/// Mirrors <see cref="AdminTimezoneControllerTests"/> exactly — the singleton-toggle
/// shape (one audited write lane), so the audit-row assertion lives in the Core
/// tests (the service owns it), and the controller is tested as the thin seam.
/// </summary>
public class AdminSignupControllerTests
{
    private const string Admin = "admin-signup-001";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    private static (AdminSignupController controller, IIdentityService identity) Build(bool isOpen)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.IsSignupOpenAsync().Returns(isOpen);
        // IMPROVE-UX A11 — the 3-state resident-invite mode (the `Off` floor,
        // ADR 0304 §Amendments A11) reads `Off` by default; the A11 tests
        // override this to pin the round-trip.
        identity.GetResidentInviteSettingAsync().Returns(
            Kumunita.Core.Identity.ResidentInviteSetting.Off);

        var controller = new AdminSignupController(identity);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, identity);
    }

    // ── Index ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_ReturnsViewModel_WithCurrentGate()
    {
        var (controller, _) = Build(isOpen: true);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminSignupController.SignupAdminViewModel>(view.ViewData.Model);

        Assert.True(vm.IsOpen);
    }

    [Fact]
    public async Task Index_ClosedGate_ViewModelReflectsInvitationOnly()
    {
        var (controller, _) = Build(isOpen: false);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminSignupController.SignupAdminViewModel>(view.ViewData.Model);

        Assert.False(vm.IsOpen);
    }

    // ── Save ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Save_Open_CallsServiceAndRedirects()
    {
        var (controller, identity) = Build(isOpen: false);

        var action = await controller.Save(true);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSignupController.Index), redirect.ActionName);

        await identity.Received(1).SetSignupOpenAsync(true, Admin);
    }

    [Fact]
    public async Task Save_InvitationOnly_CallsServiceAndRedirects()
    {
        var (controller, identity) = Build(isOpen: true);

        var action = await controller.Save(false);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSignupController.Index), redirect.ActionName);

        await identity.Received(1).SetSignupOpenAsync(false, Admin);
    }

    // ── SaveNotify (ADR 0077 — the notify-admins gate, a second independent
    //    audited lane on the same /admin/signup surface) ────────────────────

    [Fact]
    public async Task Index_ReturnsViewModel_WithNotifyGateDefault()
    {
        var (controller, identity) = Build(isOpen: true);
        identity.IsNotifyAdminsOnSignupAsync().Returns(true);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminSignupController.SignupAdminViewModel>(view.ViewData.Model);

        Assert.True(vm.IsOpen);
        Assert.True(vm.NotifyAdmins);
    }

    [Fact]
    public async Task SaveNotify_On_CallsServiceAndRedirects()
    {
        var (controller, identity) = Build(isOpen: true);

        var action = await controller.SaveNotify(true);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSignupController.Index), redirect.ActionName);

        await identity.Received(1).SetNotifyAdminsOnSignupAsync(true, Admin);
    }

    [Fact]
    public async Task SaveNotify_Off_CallsServiceAndRedirects()
    {
        var (controller, identity) = Build(isOpen: true);

        var action = await controller.SaveNotify(false);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSignupController.Index), redirect.ActionName);

        await identity.Received(1).SetNotifyAdminsOnSignupAsync(false, Admin);
    }

    // ── IMPROVE-UX A11 — the 3-state resident-invite mode (ADR 0304, amended) ──
    // The A11 "Done when" bar (the Web half): (f) the POST stores the setting
    // (the round-trip) + the GET seeds the form with the current value; (g)
    // the view renders the 3-option radio group (not a raw enum value). The
    // audit-row pin lives in the Core tests (the service owns the audit row —
    // the AdminSignupControllerTests house idiom, the singleton-toggle shape).

    [Fact]
    public async Task Index_ReturnsViewModel_WithResidentInviteFloorOff()
    {
        var (controller, _) = Build(isOpen: true);
        // The `Off` floor is the default stub (Build sets it) — a fresh
        // instance ships with resident self-invite off (ADR 0304
        // §Amendments A11).
        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminSignupController.SignupAdminViewModel>(view.ViewData.Model);

        Assert.Equal(Kumunita.Core.Identity.ResidentInviteSetting.Off, vm.ResidentInvite);
    }

    [Fact]
    public async Task Index_ReturnsViewModel_WithResidentInviteOn()
    {
        var (controller, identity) = Build(isOpen: true);
        identity.GetResidentInviteSettingAsync().Returns(
            Kumunita.Core.Identity.ResidentInviteSetting.On);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminSignupController.SignupAdminViewModel>(view.ViewData.Model);

        Assert.Equal(Kumunita.Core.Identity.ResidentInviteSetting.On, vm.ResidentInvite);
    }

    [Fact]
    public async Task Index_ReturnsViewModel_WithResidentInviteOnWithApproval()
    {
        var (controller, identity) = Build(isOpen: true);
        identity.GetResidentInviteSettingAsync().Returns(
            Kumunita.Core.Identity.ResidentInviteSetting.OnWithApproval);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminSignupController.SignupAdminViewModel>(view.ViewData.Model);

        Assert.Equal(Kumunita.Core.Identity.ResidentInviteSetting.OnWithApproval, vm.ResidentInvite);
    }

    [Fact]
    public async Task SaveResidentInvite_Off_CallsServiceAndRedirects()
    {
        var (controller, identity) = Build(isOpen: true);

        var action = await controller.SaveResidentInvite(
            Kumunita.Core.Identity.ResidentInviteSetting.Off);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSignupController.Index), redirect.ActionName);

        await identity.Received(1).SetResidentInviteSettingAsync(
            Kumunita.Core.Identity.ResidentInviteSetting.Off, Admin);
    }

    [Fact]
    public async Task SaveResidentInvite_On_CallsServiceAndRedirects()
    {
        var (controller, identity) = Build(isOpen: true);

        var action = await controller.SaveResidentInvite(
            Kumunita.Core.Identity.ResidentInviteSetting.On);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSignupController.Index), redirect.ActionName);

        await identity.Received(1).SetResidentInviteSettingAsync(
            Kumunita.Core.Identity.ResidentInviteSetting.On, Admin);
    }

    [Fact]
    public async Task SaveResidentInvite_OnWithApproval_CallsServiceAndRedirects()
    {
        var (controller, identity) = Build(isOpen: true);

        var action = await controller.SaveResidentInvite(
            Kumunita.Core.Identity.ResidentInviteSetting.OnWithApproval);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSignupController.Index), redirect.ActionName);

        await identity.Received(1).SetResidentInviteSettingAsync(
            Kumunita.Core.Identity.ResidentInviteSetting.OnWithApproval, Admin);
    }

    // (g) the view renders the 3-option radio group (not a raw enum value):
    // the AdminSignup/Index.cshtml source carries the 3 radio inputs (Off /
    // On / OnWithApproval) + the 3 localized option labels (the kw-l floor +
    // the `for`/`id` pairing the a11y heuristic credits as labeled). The
    // house idiom is the view-source pin (AdminOnboardingBannerTests /
    // KwLRegistryConsistencyTests / NavMoreFoldTests — the test project does
    // not render Razor; it inspects the .cshtml source).
    [Fact]
    public void View_RendersThreeOptionRadioGroup_NotRawEnumValue()
    {
        var src = LoadViewSource();

        // The 3 radio inputs (name="residentInvite", one per state).
        Assert.Contains("type=\"radio\" name=\"residentInvite\"", src);
        Assert.Contains("value=\"Off\"", src);
        Assert.Contains("value=\"On\"", src);
        Assert.Contains("value=\"OnWithApproval\"", src);

        // The 3 localized option labels (the kw-l key + the en floor).
        Assert.Contains("admin.signup_resident_invite_off", src);
        Assert.Contains("admin.signup_resident_invite_on", src);
        Assert.Contains("admin.signup_resident_invite_on_approval", src);

        // The form posts to the dedicated route (the SaveResidentInvite POST).
        Assert.Contains("action=\"/admin/signup/resident-invite\"", src);
    }

    private static string LoadViewSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
        {
            dir = dir.Parent;
        }
        Assert.True(dir is not null,
            "Could not locate the repo root (Kumunita.slnx) above the test output directory.");
        var path = Path.Combine(dir!.FullName, "src", "Kumunita.Web", "Views", "AdminSignup", "Index.cshtml");
        Assert.True(File.Exists(path), $"AdminSignup/Index.cshtml not found at {path}.");
        return File.ReadAllText(path);
    }
}
