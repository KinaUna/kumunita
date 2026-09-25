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
/// (<c>IsSignupOpenAsync</c>, the <c>true</c> floor).</item>
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
}
