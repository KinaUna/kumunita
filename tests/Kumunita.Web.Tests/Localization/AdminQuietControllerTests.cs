using System.Reflection;
using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M20 (ADR 0121, D8) — the <c>/admin/quiet</c> cadence surface
/// (<see cref="AdminQuietController"/>) — the GlobalAdmin's thin control
/// plane over the §6.4 notification-flush cadence
/// (<see cref="ILocalizationService.GetQuietCheckMinutesAsync"/> read +
/// <see cref="ILocalizationService.SetQuietCheckMinutesAsync"/> single
/// audited write lane, U04's seams). Mirrors
/// <see cref="AdminSignupControllerTests"/> (the house idiom): direct
/// construction with NSubstitute seams + <see cref="DefaultHttpContext"/>
/// + an in-memory <see cref="ITempDataProvider"/> for the flash assertions;
/// no Postgres (the one-audit-row pin lives in Core.Tests — U04's GATE-6 —
/// so the Web pin is the seam-call shape + the TempData flash, not the
/// audit row). Pins:
/// <list type="number">
/// <item><b>Index</b> — the view model carries the current cadence from
/// <c>GetQuietCheckMinutesAsync</c> (assert the seam was called exactly
/// once).</item>
/// <item><b>Save (valid)</b> — calls <c>SetQuietCheckMinutesAsync(30,
/// admin)</c> (assert the exact minutes + the signed-in actor), redirects
/// to <c>Index</c>, sets <c>TempData["info"]</c> to the registered
/// <c>admin.quiet.flash_saved</c> value (the U06 key, the
/// <see cref="KnownTranslationKeys.EnValues"/> floor — the
/// translationProvider is <c>null</c> in this harness), and does <b>not</b>
/// set <c>TempData["error"]</c>.</item>
/// <item><b>Save (out-of-range)</b> — the seam's
/// <c>ArgumentOutOfRangeException</c> is caught and surfaced as
/// <c>TempData["error"]</c> (the Web boundary's validation-error shape);
/// <c>TempData["info"]</c> is <b>not</b> set (no success flash). The seam
/// is still called (assert <c>Received(1)</c>) — the Web boundary does not
/// pre-validate the value (the ADR 0050 split: the Web delegates, the seam
/// validates). The Web boundary adds no second audit (the one-audit-row
/// pin lives in Core.Tests — the seam owns it; the controller calls the
/// seam exactly once).</item>
/// <item><b>Gate</b> — <c>[Authorize(Roles = GlobalAdmin)]</c> is
/// present on the controller (the house idiom — the
/// <see cref="AdminAnalyticsControllerTests"/>'
/// <c>Route_Exists_And_GlobalAdmin_Only</c> pin shape: the gate is the
/// role, the Web owns it, the Core seam does not re-check
/// <c>User</c>).</item>
/// </list>
/// </summary>
public class AdminQuietControllerTests
{
    private const string Admin = "admin-quiet-001";

    /// <summary>An in-memory <see cref="ITempDataProvider"/> — closes the
    /// <c>TempData</c> bag for the save-lane flash writes so the assertion
    /// can read <c>TempData["info"]</c> / <c>TempData["error"]</c> after the
    /// action returns (the <see cref="AdminSignupControllerTests"/> /
    /// <see cref="LocaleControllerQuietSectionTests"/> idiom).</summary>
    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        private readonly Dictionary<string, object?> _bag = new();
        public IDictionary<string, object?> LoadTempData(HttpContext context) => _bag;
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            _bag.Clear();
            foreach (var (k, v) in values) _bag[k] = v;
        }
    }

    private static (AdminQuietController controller, ILocalizationService localization) Build(
        int currentCadence = 60)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetQuietCheckMinutesAsync().Returns(currentCadence);

        var controller = new AdminQuietController(localization);

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

        var provider = new NoOpTempDataProvider();
        controller.TempData = new TempDataDictionary(httpContext, provider);
        return (controller, localization);
    }

    // ── Gate ───────────────────────────────────────────────────────────────

    [Fact]
    public void Controller_Carries_GlobalAdmin_Role_Authorize()
    {
        // The gate (D8, C-M20·6): the controller carries
        // [Authorize(Roles = GlobalAdmin)] — a non-GlobalAdmin actor never
        // reaches the action (the sign-in challenge is the platform's
        // authz-failure shape). Asserted by the attribute's presence +
        // role value (the AdminAnalyticsControllerTests /
        // AdminPortabilityControllerTests / AdminGuestsControllerTests
        // Controller_Carries_GlobalAdmin_Role_Authorize idiom).
        var attr = typeof(AdminQuietController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Roles.GlobalAdmin, attr!.Roles);
    }

    // ── Index ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_ReturnsViewModel_WithCurrentCadence()
    {
        var (controller, localization) = Build(currentCadence: 30);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminQuietController.CadenceAdminViewModel>(view.ViewData.Model);

        Assert.Equal(30, vm.Minutes);
        await localization.Received(1).GetQuietCheckMinutesAsync();
    }

    [Fact]
    public async Task Index_FloorCadence_ViewModelReflectsFloor()
    {
        var (controller, _) = Build(currentCadence: 60);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminQuietController.CadenceAdminViewModel>(view.ViewData.Model);

        Assert.Equal(60, vm.Minutes);
    }

    // ── Save (valid) ───────────────────────────────────────────────────────

    [Fact]
    public async Task Save_Valid_CallsSeamWithMinutesAndActorAndRedirects()
    {
        var (controller, localization) = Build();

        var action = await controller.Save(30);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminQuietController.Index), redirect.ActionName);

        // The seam is called exactly once with the exact minutes + the
        // signed-in actor (the ActorId(User) seam). The one-audit-row pin
        // lives in Core.Tests (the seam owns it); the Web boundary calls
        // the seam exactly once — no second audit.
        await localization.Received(1).SetQuietCheckMinutesAsync(30, Admin);

        // The success flash (the registered admin.quiet.flash_saved kw-l
        // key — the U06 set) is set; no error flash.
        Assert.NotNull(controller.TempData["info"]);
        Assert.Null(controller.TempData["error"]);
    }

    [Fact]
    public async Task Save_Valid_FlashIsTheRegisteredFlashSavedKwLKey()
    {
        var (controller, _) = Build();

        await controller.Save(90);

        // The translationProvider is null in this harness, so FlashAsync
        // resolves to the KnownTranslationKeys.EnValues floor (the U06
        // key's English source text — code is the floor, ADR 0015 D1).
        var expected = KnownTranslationKeys.EnValues["admin.quiet.flash_saved"];
        Assert.Equal(expected, controller.TempData["info"]);
    }

    // ── Save (out-of-range) ────────────────────────────────────────────────

    [Fact]
    public async Task Save_OutOfRange_BelowFloor_SurfacesErrorAndNoInfoFlash()
    {
        var (controller, localization) = Build();

        // The seam's 5–1440 validation (C-M20·6): a value below 5 throws
        // ArgumentOutOfRangeException before any write (no audit row for
        // the blocked attempt — the RemoveLanguageAsync M·7 pin shape).
        localization
            .When(x => x.SetQuietCheckMinutesAsync(0, Arg.Any<string>()))
            .Do(_ => throw new ArgumentOutOfRangeException("minutes", "The cadence must be between 5 and 1440 minutes."));

        var action = await controller.Save(0);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminQuietController.Index), redirect.ActionName);

        // The seam was called (the Web delegates, the seam validates — the
        // ADR 0050 split: the Web does not pre-validate). Exactly once.
        await localization.Received(1).SetQuietCheckMinutesAsync(0, Arg.Any<string>());

        // The error flash is set (the Web boundary's validation-error
        // shape — the 95-place TempData["error"] convention); the success
        // flash is NOT set (no info flash on a failed save).
        Assert.NotNull(controller.TempData["error"]);
        Assert.Null(controller.TempData["info"]);
    }

    [Fact]
    public async Task Save_OutOfRange_AboveCeiling_SurfacesErrorAndNoInfoFlash()
    {
        var (controller, localization) = Build();

        localization
            .When(x => x.SetQuietCheckMinutesAsync(99999, Arg.Any<string>()))
            .Do(_ => throw new ArgumentOutOfRangeException("minutes", "The cadence must be between 5 and 1440 minutes."));

        var action = await controller.Save(99999);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminQuietController.Index), redirect.ActionName);

        await localization.Received(1).SetQuietCheckMinutesAsync(99999, Arg.Any<string>());

        Assert.NotNull(controller.TempData["error"]);
        Assert.Null(controller.TempData["info"]);
    }

    // ── kw-l closure witness (the M20 admin.quiet.* keys are in the
    //    registry — the U06 closed set, U07 consumes and adds none) ──────

    [Fact]
    public void AdminQuiet_Keys_ArePresent_And_NonEmpty_In_All_Four_Languages()
    {
        // The M20 admin.quiet.* keys (U06's closed set, design doc §10 /
        // register's closed-kw-l table) are each present non-empty in all
        // four language dictionaries (en/de/fr/da). The
        // KnownTranslationKeys_ParityTests /
        // KwLRegistryConsistencyTests pins auto-extend over AllKeys and
        // pin the same closure; this is an explicit witness for the M20
        // admin surface specifically (the M19
        // AdminGuests_Keys_ArePresent_And_NonEmpty_In_All_Four_Languages
        // precedent).
        string[] adminKeys =
        {
            "admin.quiet.title",
            "admin.quiet.cadence_label",
            "admin.quiet.save",
            "admin.quiet.flash_saved",
        };

        var dictionaries = new[]
        {
            ("en", KnownTranslationKeys.EnValues),
            ("de", KnownTranslationKeys.DeValues),
            ("fr", KnownTranslationKeys.FrValues),
            ("da", KnownTranslationKeys.DaValues),
        };

        foreach (var (lang, dict) in dictionaries)
        {
            foreach (var key in adminKeys)
            {
                Assert.True(dict.ContainsKey(key),
                    $"The kw-l key '{key}' is not present in the {lang} dictionary.");
                Assert.False(
                    string.IsNullOrWhiteSpace(dict[key]),
                    $"The kw-l key '{key}' is empty in the {lang} dictionary.");
            }
        }
    }
}
