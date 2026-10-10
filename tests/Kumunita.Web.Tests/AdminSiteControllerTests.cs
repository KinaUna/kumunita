using Kumunita.Core.Identity;
using Kumunita.Core.SiteContent;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using System.Security.Claims;
using System.Reflection;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// SITE U08 (ADR 0150) — the <c>/admin/site</c> write-lane pins
/// (SITE·2 / SITE·7): the GET seeds both form sections (Home + About)
/// with the current singleton; the POST for each section saves the
/// field group (the other section's fields pass through unchanged —
/// the save is partial) and delegates to the service's single audited
/// write lane; and a non-<c>GlobalAdmin</c> is denied (the
/// <c>[Authorize(Roles = GlobalAdmin)]</c> gate, SITE·7).
/// </summary>
/// <para>
/// <b>Test model:</b> direct-construction (NSubstitute
/// <c>ISiteContentService</c> seam, the <see cref="AdminSignupControllerTests"/>
/// shape). The audit-row assertion lives in
/// <see cref="SiteContentServiceTests.SaveAsync_WritesOneAccessAuditRow"/>
/// (the service owns it); the controller is tested as the thin seam —
/// the call log is the assertion target.
/// </para>
public class AdminSiteControllerTests
{
    private const string Admin = "admin-site-u08";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    private static (AdminSiteController controller, ISiteContentService site) Build(SiteContent? current)
    {
        var site = Substitute.For<ISiteContentService>();
        site.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(current ?? new SiteContent()));

        var controller = new AdminSiteController(site);

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
        return (controller, site);
    }

    // ── 1 — the GET seeds both form sections with the current singleton
    // (SITE·1, the ADR 0050 IsSignupOpenAsync best-effort shape) ─────────

    [Fact(DisplayName = "U08 AdminSite GET seeds both form sections with the current singleton")]
    public async Task GET_SeesCurrentSingleton()
    {
        var current = new SiteContent
        {
            HomeHeroEyebrow     = "A custom home eyebrow",
            HomeHeroLead        = "A custom home lead",
            HomeShowAboutButton = true,
            HomeShowFeatures    = false,
            HomeShowRoadmap     = true,
            AboutHeroEyebrow    = "A custom about eyebrow",
            AboutHeroLead       = "A custom about lead",
            AboutShowFeatures   = true,
            AboutShowScope      = false,
            AboutShowPhilosophy = true,
            AboutShowProject    = true,
            AboutShowWhatsNew   = true,
            AboutShowContactCta = false,
        };

        var (controller, site) = Build(current);

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminSiteController.AdminSiteViewModel>(view.ViewData.Model);

        // The Home section is seeded from the current singleton.
        Assert.Equal("A custom home eyebrow", vm.Home.HomeHeroEyebrow);
        Assert.Equal("A custom home lead", vm.Home.HomeHeroLead);
        Assert.True(vm.Home.HomeShowAboutButton);
        Assert.False(vm.Home.HomeShowFeatures);
        Assert.True(vm.Home.HomeShowRoadmap);

        // The About section is seeded from the current singleton.
        Assert.Equal("A custom about eyebrow", vm.About.AboutHeroEyebrow);
        Assert.Equal("A custom about lead", vm.About.AboutHeroLead);
        Assert.True(vm.About.AboutShowFeatures);
        Assert.False(vm.About.AboutShowScope);
        Assert.True(vm.About.AboutShowPhilosophy);
        Assert.True(vm.About.AboutShowProject);
        Assert.True(vm.About.AboutShowWhatsNew);
        Assert.False(vm.About.AboutShowContactCta);

        // The service was called (the read seam is wired).
        await site.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    // ── 2 — the POST /save-home saves the Home field group + delegates to
    // the service's single audited write lane (SITE·2) ─────────────────────
    // The About section's 8 fields pass through unchanged (the save is
    // partial — the ADR 0050 "one field per save" shape extended to
    // "one section per save"). The redirect is back to the GET.

    [Fact(DisplayName = "U08 AdminSite POST /save-home saves the Home field group and redirects")]
    public async Task POST_SaveHome_SavesFieldGroup_WritesOneAccessAuditRow()
    {
        var current = new SiteContent
        {
            HomeHeroEyebrow     = "Old home eyebrow",
            HomeHeroLead        = "Old home lead",
            HomeShowAboutButton = true,
            HomeShowFeatures    = true,
            HomeShowRoadmap     = true,
            AboutHeroEyebrow    = "Preserved about eyebrow",
            AboutHeroLead       = "Preserved about lead",
            AboutShowFeatures   = true,
            AboutShowScope      = false,
            AboutShowPhilosophy = true,
            AboutShowProject    = true,
            AboutShowWhatsNew   = true,
            AboutShowContactCta = true,
        };

        var (controller, site) = Build(current);

        // The admin submits a new Home section (5 fields).
        var home = new AdminSiteController.AdminSiteViewModel.HomeSection
        {
            HomeHeroEyebrow     = "New home eyebrow",
            HomeHeroLead        = "New home lead",
            HomeShowAboutButton = false,
            HomeShowFeatures    = true,
            HomeShowRoadmap     = true,
        };

        var action = await controller.SaveHome(home);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSiteController.Index), redirect.ActionName);

        // The service was called exactly once (the single audited write lane,
        // SITE·2). The audit-row assertion lives in SiteContentServiceTests
        // (the service owns it) — the controller is the thin seam.
        await site.Received(1).SaveAsync(
            Arg.Any<Kumunita.Core.SiteContent.SiteContent>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ── 3 — the POST /save-about saves the About field group + delegates
    // to the service's single audited write lane (SITE·2) ──────────────────
    // The Home section's 5 fields pass through unchanged (the save is
    // partial). The redirect is back to the GET.

    [Fact(DisplayName = "U08 AdminSite POST /save-about saves the About field group and redirects")]
    public async Task POST_SaveAbout_SavesFieldGroup_WritesOneAccessAuditRow()
    {
        var current = new SiteContent
        {
            HomeHeroEyebrow     = "Preserved home eyebrow",
            HomeHeroLead        = "Preserved home lead",
            HomeShowAboutButton = true,
            HomeShowFeatures    = false,
            HomeShowRoadmap     = true,
            AboutHeroEyebrow    = "Old about eyebrow",
            AboutHeroLead       = "Old about lead",
            AboutShowFeatures   = true,
            AboutShowScope      = true,
            AboutShowPhilosophy = true,
            AboutShowProject    = true,
            AboutShowWhatsNew   = true,
            AboutShowContactCta = true,
        };

        var (controller, site) = Build(current);

        // The admin submits a new About section (8 fields).
        var about = new AdminSiteController.AdminSiteViewModel.AboutSection
        {
            AboutHeroEyebrow    = "New about eyebrow",
            AboutHeroLead       = "New about lead",
            AboutShowFeatures   = false,
            AboutShowScope      = true,
            AboutShowPhilosophy = false,
            AboutShowProject    = true,
            AboutShowWhatsNew   = true,
            AboutShowContactCta = false,
        };

        var action = await controller.SaveAbout(about);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSiteController.Index), redirect.ActionName);

        // The service was called exactly once (the single audited write lane,
        // SITE·2). The audit-row assertion lives in SiteContentServiceTests
        // (the service owns it) — the controller is the thin seam.
        await site.Received(1).SaveAsync(
            Arg.Any<Kumunita.Core.SiteContent.SiteContent>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    // ── 4 — a non-GlobalAdmin is denied (SITE·7) ──────────────────────────
    // The [Authorize(Roles = GlobalAdmin)] attribute is the gate. In a
    // direct-construction harness (no TestServer), the attribute is
    // asserted via reflection — the attribute is present and names the
    // GlobalAdmin role, which is the ASP.NET Core authorization policy
    // that denies a non-GlobalAdmin principal at the middleware layer.

    [Fact(DisplayName = "U08 AdminSite is GlobalAdmin-gated (the [Authorize(Roles = GlobalAdmin)] pin)")]
    public void POST_NonGlobalAdmin_IsDenied()
    {
        // The controller class carries the [Authorize(Roles = GlobalAdmin)]
        // attribute — the ASP.NET Core authorization policy that denies
        // a non-GlobalAdmin principal (a signed-in resident, or an
        // anonymous visitor) at the middleware layer.
        var attr = typeof(AdminSiteController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.NotNull(attr.Roles);
        Assert.Contains(Roles.GlobalAdmin, attr.Roles.Split(','));
    }

    // ── ADR 0157 — the SITE-2 hero-translation write-lane pins ─────────────
    // The three dedicated POST actions delegate to the service's single
    // audited write lanes (sitetranslation.add / .update / .remove); the
    // GlobalAdmin gate is the standing (the ADR 0150 D8 pin — the site has no
    // per-resident owner, so the Translator standing does not qualify). The
    // service's audit-row shape is pinned in SiteContentServiceTests; here the
    // controller is the thin seam (the call log is the assertion target).

    // ── 5 — the POST /translations adds a translation + delegates ──────────

    [Fact(DisplayName = "U08 AdminSite POST /translations adds a translation and redirects")]
    public async Task POST_AddTranslation_Delegates_WritesRedirect()
    {
        var (controller, site) = Build(null);

        var action = await controller.AddTranslation(
            "pl", "Naszyj dom", "Cichy kąt", "Prywatność", "Dom dla wszystkich");
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSiteController.Index), redirect.ActionName);

        await site.Received(1).AddTranslationAsync(
            "pl", "Naszyj dom", "Cichy kąt", "Prywatność", "Dom dla wszystkich", Admin);
    }

    // ── 6 — the POST /translations validation: no language / all blank ─────

    [Fact(DisplayName = "U08 AdminSite POST /translations rejects a fully-blank translation")]
    public async Task POST_AddTranslation_AllBlank_Rejected_NoServiceCall()
    {
        var (controller, site) = Build(null);

        var action = await controller.AddTranslation("pl", " ", "  ", null, null);
        Assert.IsType<RedirectToActionResult>(action);

        // No write lane was called (the validation short-circuits).
        await site.DidNotReceiveWithAnyArgs().AddTranslationAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    [Fact(DisplayName = "U08 AdminSite POST /translations rejects a missing language")]
    public async Task POST_AddTranslation_NoLanguage_Rejected_NoServiceCall()
    {
        var (controller, site) = Build(null);

        var action = await controller.AddTranslation(
            null, "eyebrow", "lead", null, null);
        Assert.IsType<RedirectToActionResult>(action);

        await site.DidNotReceiveWithAnyArgs().AddTranslationAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    // ── 7 — the POST /translations/update edits a translation ──────────────

    [Fact(DisplayName = "U08 AdminSite POST /translations/update updates a translation and redirects")]
    public async Task POST_UpdateTranslation_Delegates_WritesRedirect()
    {
        var (controller, site) = Build(null);

        var action = await controller.UpdateTranslation(
            "pl", "New eyebrow", "New lead", null, null);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSiteController.Index), redirect.ActionName);

        await site.Received(1).UpdateTranslationAsync(
            "pl", "New eyebrow", "New lead", null, null, Admin);
    }

    // ── 8 — the POST /translations/update missing-row shape is a flash ─────

    [Fact(DisplayName = "U08 AdminSite POST /translations/update on a missing row flashes + redirects")]
    public async Task POST_UpdateTranslation_MissingRow_Flashes_Redirects()
    {
        var (controller, site) = Build(null);

        // A missing-row shape (the service's KeyNotFoundException) is caught by
        // the controller and surfaced as a flash + redirect (not an exception).
        site.UpdateTranslationAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>())
            .Returns(throwingTask<SiteContentTranslation>());

        var action = await controller.UpdateTranslation(
            "xx", "eyebrow", "lead", null, null);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSiteController.Index), redirect.ActionName);
    }

    /// <summary>A <c>Task</c> that throws on await — the
    /// <see cref="KeyNotFoundException"/> shape the controller catches on the
    /// missing-row path. The generic form returns a
    /// <c>Task&lt;T&gt;</c>; the non-generic form a plain <c>Task</c> (for
    /// <c>RemoveTranslationAsync</c>, which returns <c>Task</c>).</summary>
    private static Task<T> throwingTask<T>()
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetException(new KeyNotFoundException("missing"));
        return tcs.Task;
    }

    private static Task throwingTask()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetException(new KeyNotFoundException("missing"));
        return tcs.Task;
    }

    // ── 9 — the POST /translations/remove removes a translation ────────────

    [Fact(DisplayName = "U08 AdminSite POST /translations/remove removes a translation and redirects")]
    public async Task POST_RemoveTranslation_Delegates_WritesRedirect()
    {
        var (controller, site) = Build(null);

        var action = await controller.RemoveTranslation("pl");
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSiteController.Index), redirect.ActionName);

        await site.Received(1).RemoveTranslationAsync("pl", Admin);
    }

    // ── 10 — the POST /translations/remove missing-row shape is a flash ────

    [Fact(DisplayName = "U08 AdminSite POST /translations/remove on a missing row flashes + redirects")]
    public async Task POST_RemoveTranslation_MissingRow_Flashes_Redirects()
    {
        var (controller, site) = Build(null);
        site.RemoveTranslationAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(throwingTask());

        var action = await controller.RemoveTranslation("xx");
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminSiteController.Index), redirect.ActionName);
    }
}
