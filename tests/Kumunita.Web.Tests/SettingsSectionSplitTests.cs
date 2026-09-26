using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0080 — the /settings surface split (the ADR 0062 pattern): three
/// linkable section pages on <see cref="LocaleController"/> (Language at
/// <c>Index</c> — which also carries the Email &amp; notification language
/// section, ADR 0061, folded in 2026-09-30 — plus Time zone and Date &amp; time
/// format) sharing one <see cref="LocaleController.LocaleSettingsViewModel"/>.
/// Pins the split's two observable halves: each section GET renders its own
/// view name with the shared model, and each save lane redirects back to the
/// section that owns it (the subject-null branches, which return before any
/// <c>TempData</c> write — the harness has no <see cref="ITempDataProvider"/>).
/// </summary>
public class SettingsSectionSplitTests
{
    private const string Subject = "subj-1";

    private static LocaleController BuildController(
        bool signedIn,
        Profile? profile = null)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync()
            .Returns(Task.FromResult<IReadOnlyList<LanguageCatalog>>(new[]
            {
                new LanguageCatalog { Id = "en", NativeName = "English",  Enabled = true, SortOrder = 0 },
                new LanguageCatalog { Id = "de", NativeName = "Deutsch",  Enabled = true, SortOrder = 1 },
                new LanguageCatalog { Id = "fr", NativeName = "Français", Enabled = true, SortOrder = 2 },
            }));
        // The account sections read these even for a signed-out subject.
        localization.GetDefaultTimezoneAsync().Returns(Task.FromResult("UTC"));
        localization.GetDefaultDateFormatAsync()
            .Returns(Task.FromResult(DateFormat.FloorFormat));

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Subject)
            .Returns(Task.FromResult(profile));

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        var defaultSettings = new LocaleSettings { DefaultLanguageCode = "en" };
        readSession.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LocaleSettings?>(defaultSettings));
        store.QuerySession().Returns(readSession);

        var controller = new LocaleController(localization, userInfo, store);
        var httpContext = new DefaultHttpContext();
        if (signedIn)
        {
            httpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new[] { new System.Security.Claims.Claim("Kumunita.Sub", Subject) }, "TestAuth"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    // ── Section GETs render their own view with the shared model ─────────

    [Fact]
    public async Task Index_Renders_Index_View_With_The_Shared_Model()
    {
        var controller = BuildController(signedIn: true,
            profile: new Profile { SubjectId = Subject, TimeZone = "Europe/Warsaw" });

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        // Index() renders View(model) with the convention-resolved name (the
        // action name "Index") — ViewName is null until view resolution.
        Assert.True(view.ViewName is null or "Index");
        var model = Assert.IsType<LocaleController.LocaleSettingsViewModel>(view.ViewData.Model);
        Assert.NotNull(model.Timezone);
    }

    [Fact]
    public async Task SettingsTimezone_Renders_Timezone_View_With_The_Shared_Model()
    {
        var controller = BuildController(signedIn: true,
            profile: new Profile { SubjectId = Subject, TimeZone = "Europe/Warsaw" });

        var result = await controller.SettingsTimezone();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Timezone", view.ViewName);
        var model = Assert.IsType<LocaleController.LocaleSettingsViewModel>(view.ViewData.Model);
        Assert.NotNull(model.Timezone);
        Assert.Equal("Europe/Warsaw", model.Timezone.SelectedId);
    }

    [Fact]
    public async Task SettingsDateFormat_Renders_DateFormat_View_With_The_Shared_Model()
    {
        var controller = BuildController(signedIn: true,
            profile: new Profile { SubjectId = Subject, DateFormat = "yyyy-MM-dd HH:mm" });

        var result = await controller.SettingsDateFormat();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("DateFormat", view.ViewName);
        var model = Assert.IsType<LocaleController.LocaleSettingsViewModel>(view.ViewData.Model);
        Assert.NotNull(model.DateFormat);
    }

    [Fact]
    public void SettingsEmailLanguage_RetiredRoute_Redirects_To_Language_Tab()
    {
        // The email & notification language section (ADR 0061) was folded into
        // the Language tab (ADR 0080, 2026-09-30); the retired GET route now
        // just points back at the page that owns the section.
        var controller = BuildController(signedIn: true,
            profile: new Profile { SubjectId = Subject, EmailLanguage = "de" });

        var result = controller.SettingsEmailLanguage();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(LocaleController.Index), redirect.ActionName);
    }

    // ── Save lanes redirect back to the section that owns them ────────────
    // (The subject-null branches — signed-out, fail-closed — return before
    // any TempData write, so the harness's missing ITempDataProvider is never
    // touched.)

    [Fact]
    public async Task SaveTimezone_SignedOut_Redirects_To_SettingsTimezone()
    {
        var controller = BuildController(signedIn: false);

        var result = await controller.SaveTimezone(timezone: "Europe/Warsaw", clear: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(LocaleController.SettingsTimezone), redirect.ActionName);
    }

    [Fact]
    public async Task SaveDateFormat_SignedOut_Redirects_To_SettingsDateFormat()
    {
        var controller = BuildController(signedIn: false);

        var result = await controller.SaveDateFormat(format: "yyyy-MM-dd", customFormat: null, clear: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(LocaleController.SettingsDateFormat), redirect.ActionName);
    }

    [Fact]
    public async Task SaveEmailLanguage_SignedOut_Redirects_To_Language_Tab()
    {
        var controller = BuildController(signedIn: false);

        var result = await controller.SaveEmailLanguage(code: "de", clear: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(LocaleController.Index), redirect.ActionName);
    }

    [Fact]
    public async Task Save_NoOp_Still_Redirects_To_Index()
    {
        // The language lane keeps its long-standing redirect home (the
        // /settings landing tab). Exercised via the no-op branch (no code,
        // no clear) — the only branch that returns before writing TempData
        // or the cookie, which this harness (no ITempDataProvider) can't
        // observe.
        var controller = BuildController(signedIn: false);

        var result = await controller.Save(code: null, clear: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(LocaleController.Index), redirect.ActionName);
    }
}
