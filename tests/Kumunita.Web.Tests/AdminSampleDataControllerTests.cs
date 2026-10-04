using Kumunita.Core.Identity;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using System.Security.Claims;

namespace Kumunita.Web.Tests;

/// <summary>
/// Verifies <see cref="AdminSampleDataController"/> (ADR 0138) — the
/// GlobalAdmin's sample-data change-password lock control plane. The pin:
/// <list type="bullet">
/// <item><b>Index, sample data off</b> — the surface is unreachable by
/// construction (a real deployment never carries the <c>SampleData__Enabled</c>
/// flag, the ADR 0056 shape) → <b>404</b>; the lock seam is not even read.</item>
/// <item><b>Index, sample data on</b> — the view model carries the current lock
/// (<c>IsSamplePasswordChangeLockedAsync</c>, the <c>false</c> floor).</item>
/// <item><b>Save, sample data off</b> — the gate is authoritative on the write
/// path too (a crafted POST to a real instance is refused) → <b>404</b>;
/// <c>SetSamplePasswordChangeLockedAsync</c> is NOT invoked.</item>
/// <item><b>Save, sample data on (lock)</b> — calls
/// <c>SetSamplePasswordChangeLockedAsync(true, actor)</c> and redirects.</item>
/// <item><b>Save, sample data on (unlock)</b> — calls
/// <c>SetSamplePasswordChangeLockedAsync(false, actor)</c> and redirects.</item>
/// </list>
/// Mirrors <see cref="AdminSignupControllerTests"/> exactly — the singleton-
/// toggle shape (one audited write lane), so the audit-row assertion lives in
/// the Core tests (the service owns it), and the controller is tested as the
/// thin seam.
/// </summary>
public class AdminSampleDataControllerTests
{
    private const string Admin = "admin-sample-001";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    private static (AdminSampleDataController controller, IIdentityService identity) Build(bool sampleDataEnabled)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.IsSampleDataEnabledAsync().Returns(sampleDataEnabled);

        var controller = new AdminSampleDataController(identity);

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
    public async Task Index_SampleDataDisabled_Returns404()
    {
        // A real deployment never carries the SampleData__Enabled flag, so the
        // surface is unreachable by construction (the ADR 0056 shape) → 404.
        var (controller, identity) = Build(sampleDataEnabled: false);

        var action = await controller.Index();
        Assert.IsType<NotFoundResult>(action);

        // The lock seam is not even read on a real instance (the existence gate
        // short-circuits before the value read).
        await identity.DidNotReceiveWithAnyArgs().IsSamplePasswordChangeLockedAsync();
    }

    [Fact]
    public async Task Index_SampleDataEnabled_ReturnsViewModel_WithCurrentLock()
    {
        var (controller, identity) = Build(sampleDataEnabled: true);
        identity.IsSamplePasswordChangeLockedAsync().Returns(true);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<Kumunita.Web.Models.SampleAdminViewModel>(view.ViewData.Model);

        Assert.True(vm.PasswordChangeLocked);
    }

    [Fact]
    public async Task Index_SampleDataEnabled_LockOff_FalseFloor()
    {
        var (controller, identity) = Build(sampleDataEnabled: true);
        identity.IsSamplePasswordChangeLockedAsync().Returns(false);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<Kumunita.Web.Models.SampleAdminViewModel>(view.ViewData.Model);

        Assert.False(vm.PasswordChangeLocked);
    }

    // ── Save ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Save_SampleDataDisabled_Returns404_AndDoesNotWrite()
    {
        // The gate is authoritative on the write path too — a crafted POST to a
        // real instance is refused (the ADR 0050 gate shape); the lock seam is
        // not invoked.
        var (controller, identity) = Build(sampleDataEnabled: false);

        var action = await controller.Save(true);
        Assert.IsType<NotFoundResult>(action);

        await identity.DidNotReceiveWithAnyArgs()
            .SetSamplePasswordChangeLockedAsync(Arg.Any<bool>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Save_Lock_CallsServiceAndRedirects()
    {
        var (controller, identity) = Build(sampleDataEnabled: true);

        var action = await controller.Save(true);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSampleDataController.Index), redirect.ActionName);

        await identity.Received(1).SetSamplePasswordChangeLockedAsync(true, Admin);
    }

    [Fact]
    public async Task Save_Unlock_CallsServiceAndRedirects()
    {
        var (controller, identity) = Build(sampleDataEnabled: true);

        var action = await controller.Save(false);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSampleDataController.Index), redirect.ActionName);

        await identity.Received(1).SetSamplePasswordChangeLockedAsync(false, Admin);
    }
}
