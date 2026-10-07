using Kumunita.Core;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// SITE U08 (ADR 0150) — the <c>/home</c> read seam + ADR 0149 composability
/// (SITE·1 / SITE·5): the <see cref="HomeController.Index"/> view model
/// reflects the <see cref="SiteContent"/> singleton when present (the
/// admin's hero text + section toggles); the in-code fallback (a null
/// <c>Site</c>) is used when the service is absent or the store is missing;
/// and the ADR 0149 <c>Profile.HideHomeIntro</c> per-resident preference
/// is composable with — not replaced by — the platform flag.
/// </summary>
/// <para>
/// <b>Test model:</b> direct-construction (NSubstitute <c>ISiteContentService</c>
/// + <c>IUserInfoService</c> seams, the <see cref="HomeControllerTests"/>
/// shape). The view model's <c>Site</c> field is the assertion target — the
/// Razor view's <c>@if</c> wraps are pure server-side conditionals over that
/// field (SITE·9: a hidden section is not in the DOM at all), so asserting
/// the model is the authoritative pin for the composability behavior.
/// </para>
public class HomeControllerSiteContentTests
{
    private static (HomeController controller, Kumunita.Core.SiteContent.ISiteContentService site) Build(Kumunita.Core.SiteContent.SiteContent? siteContent)
    {
        var site = Substitute.For<Kumunita.Core.SiteContent.ISiteContentService>();
        site.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(siteContent ?? new Kumunita.Core.SiteContent.SiteContent()));

        var options = Options.Create(new CommunityOptions
        {
            Name = "Maplewood",
            SupportEmail = "maps@example.com",
        });

        var controller = new HomeController(
            NullLogger<HomeController>.Instance,
            options,
            siteContent: site);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return (controller, site);
    }

    // ── 1 — a fresh instance renders the shipped text + every section shown
    // (SITE·1 / SITE·3) ─────────────────────────────────────────────────────
    // A missing row degrades to the in-code fallback (all defaults); the
    // Site field is non-null and every toggle is true.

    [Fact(DisplayName = "U08 FreshInstance renders shipped text, every section shown")]
    public async Task FreshInstance_RendersShippedText_EverySectionShown()
    {
        // No row in the store — the service returns the in-code fallback.
        var (controller, _) = Build(siteContent: null);

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);

        // The Site field is non-null (the in-code fallback, not a blank page).
        Assert.NotNull(vm.Site);

        // The 4 text fields are the shipped defaults (byte-identical to kw-l).
        Assert.Equal("A private home for one neighbourhood", vm.Site!.HomeHeroEyebrow);
        Assert.Equal(
            "One quiet place for everything your street does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours.",
            vm.Site.HomeHeroLead);
        Assert.Equal("Private by default", vm.Site.AboutHeroEyebrow);

        // Every section toggle is true (every section shown).
        Assert.True(vm.Site.HomeShowAboutButton);
        Assert.True(vm.Site.HomeShowFeatures);
        Assert.True(vm.Site.HomeShowRoadmap);
        Assert.True(vm.Site.AboutShowFeatures);
        Assert.True(vm.Site.AboutShowScope);
        Assert.True(vm.Site.AboutShowPhilosophy);
        Assert.True(vm.Site.AboutShowProject);
        Assert.True(vm.Site.AboutShowWhatsNew);
        Assert.True(vm.Site.AboutShowContactCta);

        // The ADR 0149 default: intro is shown (HideIntro = false).
        Assert.False(vm.HideIntro);
    }

    // ── 2 — a saved row with HomeShowAboutButton = false hides the hero button
    // (SITE·9) ──────────────────────────────────────────────────────────────
    // The admin has turned off the "What it is & how it works" CTA button;
    // the view model reflects it (the view's @if wrap is the DOM-level pin).

    [Fact(DisplayName = "U08 SavedRow with HomeShowAboutButton=false hides the home hero button")]
    public async Task SavedRow_HomeShowAboutButtonFalse_HidesHomeHeroButton()
    {
        var saved = new Kumunita.Core.SiteContent.SiteContent
        {
            HomeHeroEyebrow     = "A private home for one neighbourhood",
            HomeHeroLead        = "One quiet place for everything your street does.",
            HomeShowAboutButton = false,  // the admin turned off the button
            HomeShowFeatures    = true,
            HomeShowRoadmap     = true,
        };

        var (controller, site) = Build(siteContent: saved);

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);

        Assert.NotNull(vm.Site);
        Assert.False(vm.Site!.HomeShowAboutButton); // the button is hidden
        Assert.True(vm.Site.HomeShowFeatures);       // the other sections are still shown
        Assert.True(vm.Site.HomeShowRoadmap);

        // The service was called (the read seam is wired).
        await site.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    // ── 3 — ADR 0149 composability: HideHomeIntro=true + HomeShowFeatures=true
    // hides the feature cards (SITE·5) ───────────────────────────────────────
    // The per-resident preference is the *resident's* choice to hide; the
    // platform flag is the *admin's* choice to show. When the resident
    // hides the intro (HideHomeIntro=true), the feature cards are hidden
    // even if the admin has them shown (HomeShowFeatures=true). The view's
    // @if (!HideIntro && HomeShowFeatures) wrap is the DOM-level pin.

    [Fact(DisplayName = "U08 ADR 0149: HideHomeIntro=true + HomeShowFeatures=true → feature cards hidden")]
    public async Task ADR_0149_HideHomeIntroTrue_HomeShowFeaturesTrue_HidesHomeFeatureCards()
    {
        // The resident has set Profile.HideHomeIntro = true (ADR 0149).
        const string subjectId = "u08-resident-0149";

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(subjectId)
            .Returns(Task.FromResult(new Profile { SubjectId = subjectId, HideHomeIntro = true }));

        // The admin has HomeShowFeatures = true (the platform flag says "show").
        var saved = new Kumunita.Core.SiteContent.SiteContent
        {
            HomeHeroEyebrow     = "A private home for one neighbourhood",
            HomeHeroLead        = "One quiet place for everything your street does.",
            HomeShowAboutButton = true,
            HomeShowFeatures    = true,   // admin says "show the features"
            HomeShowRoadmap     = true,
        };

        var site = Substitute.For<Kumunita.Core.SiteContent.ISiteContentService>();
        site.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(saved));

        var options = Options.Create(new CommunityOptions
        {
            Name = "Maplewood",
            SupportEmail = "maps@example.com",
        });

        var controller = new HomeController(
            NullLogger<HomeController>.Instance,
            options,
            userInfo: userInfo,
            siteContent: site);

        // Signed-in as the resident who set HideHomeIntro=true.
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId) },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);

        // The per-resident preference is reflected in the view model.
        Assert.True(vm.HideIntro);

        // The platform flag is still "show" — but the composability rule is:
        // both must be true for the sections to appear. HideIntro=true wins.
        Assert.True(vm.Site!.HomeShowFeatures); // the admin's flag is unchanged

        // The combined gate (view's @if): !HideIntro && HomeShowFeatures
        // = !true && true = false → the feature cards are hidden.
        Assert.False(!vm.HideIntro && vm.Site.HomeShowFeatures);
    }

    // ── 4 — ADR 0149 composability: HideHomeIntro=false + HomeShowFeatures=false
    // hides the feature cards (SITE·5) ───────────────────────────────────────
    // The platform flag is the *admin's* choice to show. When it is false,
    // the sections are hidden regardless of the per-resident preference.

    [Fact(DisplayName = "U08 ADR 0149: HideHomeIntro=false + HomeShowFeatures=false → feature cards hidden")]
    public async Task ADR_0149_HideHomeIntroFalse_HomeShowFeaturesFalse_HidesHomeFeatureCards()
    {
        // The resident has NOT set HideHomeIntro (the default: false).
        const string subjectId = "u08-resident-0149b";

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(subjectId)
            .Returns(Task.FromResult(new Profile { SubjectId = subjectId, HideHomeIntro = false }));

        // The admin has HomeShowFeatures = false (the platform flag says "hide").
        var saved = new Kumunita.Core.SiteContent.SiteContent
        {
            HomeHeroEyebrow     = "A private home for one neighbourhood",
            HomeHeroLead        = "One quiet place for everything your street does.",
            HomeShowAboutButton = true,
            HomeShowFeatures    = false,  // admin says "hide the features"
            HomeShowRoadmap     = true,
        };

        var site = Substitute.For<Kumunita.Core.SiteContent.ISiteContentService>();
        site.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(saved));

        var options = Options.Create(new CommunityOptions
        {
            Name = "Maplewood",
            SupportEmail = "maps@example.com",
        });

        var controller = new HomeController(
            NullLogger<HomeController>.Instance,
            options,
            userInfo: userInfo,
            siteContent: site);

        // Signed-in as the resident who did NOT set HideHomeIntro.
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId) },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.Index();
        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);

        // The per-resident preference is the default (show the intro).
        Assert.False(vm.HideIntro);

        // The platform flag says "hide" — the combined gate is:
        // !HideIntro && HomeShowFeatures = !false && false = false → hidden.
        Assert.False(vm.Site!.HomeShowFeatures);
        Assert.False(!vm.HideIntro && vm.Site.HomeShowFeatures);
    }
}
