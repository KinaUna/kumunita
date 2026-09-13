using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using System.Security.Claims;

namespace Kumunita.Web.Tests;

/// <summary>
/// Verifies <see cref="AdminTimezoneController"/> (ADR 0019) — the GlobalAdmin's
/// platform-default time zone control plane. The pin:
/// <list type="bullet">
/// <item><b>Index</b> — the view model carries the OS zones and the current
/// platform default.</item>
/// <item><b>Save with a valid zone</b> — calls
/// <c>SetDefaultTimezoneAsync(zone, actor)</c> and redirects.</item>
/// <item><b>Save with an invalid zone</b> — the service throws
/// <see cref="InvalidOperationException"/>; the controller maps to
/// <b>409</b> (not a redirect).</item>
/// </list>
/// </summary>
public class AdminTimezoneControllerTests
{
    private const string Admin = "admin-tz-001";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    private static (AdminTimezoneController controller, ILocalizationService localization) Build(string? principalSubjectId)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetDefaultTimezoneAsync().Returns("Europe/Warsaw");

        var controller = new AdminTimezoneController(localization);

        var httpContext = new DefaultHttpContext();
        if (principalSubjectId is not null)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, principalSubjectId),
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
                    },
                    authenticationType: "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, localization);
    }

    // ── Index ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_ReturnsViewModel_WithZonesAndCurrentDefault()
    {
        var (controller, _) = Build(Admin);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminTimezoneController.TimezoneAdminViewModel>(view.ViewData.Model);

        Assert.NotEmpty(vm.Zones);
        Assert.Equal("Europe/Warsaw", vm.CurrentId);
    }

    // ── Save ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Save_ValidZone_CallsServiceAndRedirects()
    {
        var (controller, localization) = Build(Admin);
        var zone = "Asia/Tokyo";

        var action = await controller.Save(zone);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminTimezoneController.Index), redirect.ActionName);

        await localization.Received(1).SetDefaultTimezoneAsync(zone, Admin);
    }

    [Fact]
    public async Task Save_InvalidZone_MapsTo409()
    {
        var (controller, localization) = Build(Admin);
        localization.SetDefaultTimezoneAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("Unknown time zone: Not/AZone")));

        var action = await controller.Save("Not/AZone");
        Assert.IsType<StatusCodeResult>(action);
        Assert.Equal(409, Assert.IsType<StatusCodeResult>(action).StatusCode);
    }
}
