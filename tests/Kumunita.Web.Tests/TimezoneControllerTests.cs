using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using System.Security.Claims;

namespace Kumunita.Web.Tests;

/// <summary>
/// Verifies <see cref="TimezoneController"/> (ADR 0019) — the user-facing
/// time-zone preference settings page. The pin:
/// <list type="bullet">
/// <item><b>Index</b> — the view model carries the OS zones, the actor's
/// current override (or null), the selected id (override if set, else the
/// platform default), and the platform default.</item>
/// <item><b>Save with a zone id</b> — calls
/// <c>SetProfileTimezoneAsync(subject, zone, subject)</c> and redirects.</item>
/// <item><b>Save with clear=1</b> — calls
/// <c>SetProfileTimezoneAsync(subject, null, subject)</c> and redirects.</item>
/// <item><b>Save with no zone and no clear</b> — does not call the lane;
/// redirects.</item>
/// <item><b>KeyNotFoundException</b> — the lane throws; the controller
/// catches, sets TempData error, and still redirects (no 500).</item>
/// </list>
/// </summary>
public class TimezoneControllerTests
{
    private const string Subject = "subj-tz-001";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    private static (TimezoneController controller, IUserInfoService userInfo, ILocalizationService localization) Build(string? principalSubjectId)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var localization = Substitute.For<ILocalizationService>();
        localization.GetDefaultTimezoneAsync().Returns("Europe/Warsaw");

        var controller = new TimezoneController(userInfo, localization);

        var httpContext = new DefaultHttpContext();
        if (principalSubjectId is not null)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, principalSubjectId) },
                    authenticationType: "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, userInfo, localization);
    }

    // ── Index ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_ReturnsViewModel_WithZonesAndCurrentOverride()
    {
        var (controller, userInfo, _) = Build(Subject);
        userInfo.GetProfileAsync(Subject).Returns(new Profile
        {
            SubjectId = Subject,
            TimeZone = "America/New_York",
        });

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<TimezoneController.TimezoneSettingsViewModel>(view.ViewData.Model);

        Assert.NotEmpty(vm.Zones);
        Assert.Equal("America/New_York", vm.CurrentId);
        Assert.Equal("America/New_York", vm.SelectedId); // override wins
        Assert.Equal("Europe/Warsaw", vm.DefaultId);
    }

    [Fact]
    public async Task Index_WhenNoOverride_SelectedIsPlatformDefault()
    {
        var (controller, userInfo, _) = Build(Subject);
        userInfo.GetProfileAsync(Subject).Returns(new Profile
        {
            SubjectId = Subject,
            TimeZone = null,
        });

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<TimezoneController.TimezoneSettingsViewModel>(view.ViewData.Model);

        Assert.Null(vm.CurrentId);
        Assert.Equal("Europe/Warsaw", vm.SelectedId); // falls back to platform default
        Assert.Equal("Europe/Warsaw", vm.DefaultId);
    }

    [Fact]
    public async Task Index_Unauthenticated_SelectedIsPlatformDefault()
    {
        var (controller, _, _) = Build(null); // no principal

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<TimezoneController.TimezoneSettingsViewModel>(view.ViewData.Model);

        Assert.Null(vm.CurrentId);
        Assert.Equal("Europe/Warsaw", vm.SelectedId);
    }

    // ── Save ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Save_WithZoneId_CallsLaneAndRedirects()
    {
        var (controller, userInfo, _) = Build(Subject);
        var zone = "Asia/Tokyo";

        var action = await controller.Save(zone, null);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(TimezoneController.Index), redirect.ActionName);

        await userInfo.Received(1).SetProfileTimezoneAsync(Subject, zone, Subject);
    }

    [Fact]
    public async Task Save_WithClear_CallsLaneWithNullAndRedirects()
    {
        var (controller, userInfo, _) = Build(Subject);

        var action = await controller.Save(null, "1");
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(TimezoneController.Index), redirect.ActionName);

        await userInfo.Received(1).SetProfileTimezoneAsync(Subject, null, Subject);
    }

    [Fact]
    public async Task Save_NoZoneNoClear_DoesNotCallLane()
    {
        var (controller, userInfo, _) = Build(Subject);

        var action = await controller.Save(null, null);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(TimezoneController.Index), redirect.ActionName);

        await userInfo.DidNotReceiveWithAnyArgs().SetProfileTimezoneAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Save_KeyNotFound_CatchesAndRedirects()
    {
        var (controller, userInfo, _) = Build(Subject);
        userInfo.SetProfileTimezoneAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>())
            .Returns(Task.FromException(new KeyNotFoundException()));

        var action = await controller.Save("UTC", null);
        Assert.IsType<RedirectToActionResult>(action);
    }
}
