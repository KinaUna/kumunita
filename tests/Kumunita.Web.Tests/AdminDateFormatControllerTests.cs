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
/// Verifies <see cref="AdminDateFormatController"/> (ADR 0020) — the GlobalAdmin's
/// platform-default date-time format control plane. Mirrors
/// <see cref="AdminTimezoneControllerTests"/> exactly; the pin:
/// <list type="bullet">
/// <item><b>Index</b> — the view model carries the curated presets and the
/// current platform default.</item>
/// <item><b>Save with a valid preset</b> — calls
/// <c>SetDefaultDateFormatAsync(preset, actor)</c> and redirects.</item>
/// <item><b>Save with an invalid value</b> — the service throws
/// <see cref="InvalidOperationException"/>; the controller maps to
/// <b>409</b> (not a redirect).</item>
/// </list>
/// </summary>
public class AdminDateFormatControllerTests
{
    private const string Admin = "admin-df-001";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    private static (AdminDateFormatController controller, ILocalizationService localization) Build(string? principalSubjectId)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetDefaultDateFormatAsync().Returns(DateFormat.LongFormat);

        var controller = new AdminDateFormatController(localization);

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
    public async Task Index_ReturnsViewModel_WithPresetsAndCurrentDefault()
    {
        var (controller, _) = Build(Admin);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminDateFormatController.DateFormatAdminViewModel>(view.ViewData.Model);

        Assert.NotEmpty(vm.Presets);
        Assert.Equal(DateFormat.LongFormat, vm.CurrentFormat);
        Assert.True(vm.CurrentIsPreset); // the Long format is one of the presets
    }

    // ── Save ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Save_ValidPreset_CallsServiceAndRedirects()
    {
        var (controller, localization) = Build(Admin);
        const string fmt = "yyyy-MM-dd HH:mm"; // the ISO preset's format string

        // A preset select posts `format` = the preset string, `customFormat` blank.
        var action = await controller.Save(fmt, null);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminDateFormatController.Index), redirect.ActionName);

        await localization.Received(1).SetDefaultDateFormatAsync(fmt, Admin);
    }

    [Fact]
    public async Task Save_CustomFormat_CallsServiceWithCustomString()
    {
        var (controller, localization) = Build(Admin);
        const string custom = "dd.MM.yyyy HH:mm"; // a custom (non-preset) format

        // The "Custom…" select posts `format` = "custom" (sentinel) +
        // `customFormat` = the free-text value; the controller stores the free-text
        // value (the hasCustom branch), not the sentinel.
        var action = await controller.Save("custom", custom);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminDateFormatController.Index), redirect.ActionName);

        await localization.Received(1).SetDefaultDateFormatAsync(custom, Admin);
        await localization.DidNotReceive().SetDefaultDateFormatAsync("custom", Admin);
    }

    [Fact]
    public async Task Save_Invalid_MapsTo409()
    {
        var (controller, localization) = Build(Admin);
        localization.SetDefaultDateFormatAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("Unknown date format:   ")));

        // A blank `format` and blank `customFormat` → chosen = "" → the service's
        // fail-closed validation throws → 409 (not a redirect).
        var action = await controller.Save("   ", null);
        Assert.IsType<StatusCodeResult>(action);
        Assert.Equal(409, Assert.IsType<StatusCodeResult>(action).StatusCode);
    }
}
