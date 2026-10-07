using Kumunita.Core.SiteContent;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/site</c> surface (ADR 0150) — the GlobalAdmin's edit page for
/// the platform's two **landing surfaces** (<c>/home</c> =
/// <c>Views/Home/Index.cshtml</c>, <c>/about</c> =
/// <c>Views/StaticPages/About.cshtml</c>). Mirrors <see cref="AdminSignupController"/>
/// (ADR 0050) exactly: <see cref="Roles.GlobalAdmin"/>-gated (the
/// <c>AdminController</c> precedent), a thin wrapper over the **two**
/// <see cref="ISiteContentService"/> seams (<see cref="ISiteContentService.GetAsync"/>
/// / <see cref="ISiteContentService.SaveAsync"/>), and the audit row is the
/// **service's** (exactly one <c>AccessAudit</c>, <c>Via = Admin</c>, action
/// <c>site.save</c>, <c>TargetKind</c> "site" — the singleton-toggle shape, the
/// same as <c>signup.set-open</c> / <c>timezone.set-default</c> /
/// <c>dateformat.set-default</c>).
/// <para>
/// <b>Why a dedicated controller.</b> The <c>AdminController</c>'s constructor is
/// pinned by two Web-layer test harnesses (<c>AdminControllerBlockTests</c> /
/// <c>AdminControllerMandatoryTests</c>), so a new dependency there would break
/// them. A separate <c>/admin/site</c> surface (mirroring
/// <c>/admin/signup</c> / <c>/admin/timezone</c> / <c>/admin/dateformat</c>) is
/// the convention-consistent shape — the codebase already puts the
/// platform-settled instance values on their own controllers, and the landing
/// surfaces' content is their exact analog (SITE·7).
/// </para>
/// <para>
/// <b>The save is partial</b> (ADR 0150): saving the Home section does not
/// touch the About section's fields, and vice versa (the ADR 0050 "one field
/// per save" shape, extended to "one section per save"). Each POST loads the
/// current singleton, applies **only** its own field group, and saves — the
/// other section's fields pass through unchanged.
/// </para>
/// </summary>
[Route("admin/site")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminSiteController(
    ISiteContentService site) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/site</c> — seeds both form sections (Home + About) with the
    /// current singleton (<see cref="ISiteContentService.GetAsync"/>, the
    /// ADR 0050 <c>IsSignupOpenAsync</c> best-effort shape — a missing row
    /// degrades to the in-code fallback, the shipped defaults, so the form
    /// always renders; SITE·1).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var current = await site.GetAsync();
        var model = new AdminSiteViewModel
        {
            Home = new AdminSiteViewModel.HomeSection
            {
                HomeHeroEyebrow     = current.HomeHeroEyebrow,
                HomeHeroLead        = current.HomeHeroLead,
                HomeShowAboutButton = current.HomeShowAboutButton,
                HomeShowFeatures    = current.HomeShowFeatures,
                HomeShowRoadmap     = current.HomeShowRoadmap,
            },
            About = new AdminSiteViewModel.AboutSection
            {
                AboutHeroEyebrow    = current.AboutHeroEyebrow,
                AboutHeroLead       = current.AboutHeroLead,
                AboutShowFeatures   = current.AboutShowFeatures,
                AboutShowScope      = current.AboutShowScope,
                AboutShowPhilosophy = current.AboutShowPhilosophy,
                AboutShowProject    = current.AboutShowProject,
                AboutShowWhatsNew   = current.AboutShowWhatsNew,
                AboutShowContactCta = current.AboutShowContactCta,
            },
        };

        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/site/save-home</c> — saves the Home section's 5 fields
    /// (the 2 text fields + the 3 section toggles). Delegates to
    /// <see cref="ISiteContentService.SaveAsync"/> (the single audited write
    /// lane — exactly one <c>AccessAudit</c> row, <c>Via = Admin</c>, action
    /// <c>site.save</c>, <c>TargetKind</c> "site" — the <c>signup.set-open</c>
    /// shape, SITE·2). The About section's 8 fields pass through unchanged (the
    /// save is partial — the ADR 0050 "one field per save" shape extended to
    /// "one section per save"). Success → a surfaced <c>info</c> + redirect (the
    /// change is live on the very next <c>GetAsync</c> / render — strong
    /// consistency, invariant C4).
    /// </summary>
    [HttpPost("save-home")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveHome(AdminSiteViewModel.HomeSection home)
    {
        var actor = ActorId(User) ?? string.Empty;
        var current = await site.GetAsync();
        await site.SaveAsync(new SiteContent
        {
            // The Home section's 5 fields, verbatim from the form.
            HomeHeroEyebrow     = home.HomeHeroEyebrow,
            HomeHeroLead        = home.HomeHeroLead,
            HomeShowAboutButton = home.HomeShowAboutButton,
            HomeShowFeatures    = home.HomeShowFeatures,
            HomeShowRoadmap     = home.HomeShowRoadmap,
            // The About section's 8 fields, unchanged (the save is partial).
            AboutHeroEyebrow    = current.AboutHeroEyebrow,
            AboutHeroLead       = current.AboutHeroLead,
            AboutShowFeatures   = current.AboutShowFeatures,
            AboutShowScope      = current.AboutShowScope,
            AboutShowPhilosophy = current.AboutShowPhilosophy,
            AboutShowProject    = current.AboutShowProject,
            AboutShowWhatsNew   = current.AboutShowWhatsNew,
            AboutShowContactCta = current.AboutShowContactCta,
        }, actor);
        TempData["info"] = "The home page's hero text and section toggles have been saved.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>POST /admin/site/save-about</c> — saves the About section's 8 fields
    /// (the 2 text fields + the 6 section toggles). The ADR 0050
    /// <see cref="AdminSignupController.SaveNotify"/> shape (a sibling POST
    /// action with its own <c>[ValidateAntiForgeryToken]</c> + flash + redirect).
    /// The Home section's 5 fields pass through unchanged (the save is partial).
    /// One <c>AccessAudit</c> row per call (SITE·2).
    /// </summary>
    [HttpPost("save-about")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAbout(AdminSiteViewModel.AboutSection about)
    {
        var actor = ActorId(User) ?? string.Empty;
        var current = await site.GetAsync();
        await site.SaveAsync(new SiteContent
        {
            // The About section's 8 fields, verbatim from the form.
            AboutHeroEyebrow    = about.AboutHeroEyebrow,
            AboutHeroLead       = about.AboutHeroLead,
            AboutShowFeatures   = about.AboutShowFeatures,
            AboutShowScope      = about.AboutShowScope,
            AboutShowPhilosophy = about.AboutShowPhilosophy,
            AboutShowProject    = about.AboutShowProject,
            AboutShowWhatsNew   = about.AboutShowWhatsNew,
            AboutShowContactCta = about.AboutShowContactCta,
            // The Home section's 5 fields, unchanged (the save is partial).
            HomeHeroEyebrow     = current.HomeHeroEyebrow,
            HomeHeroLead        = current.HomeHeroLead,
            HomeShowAboutButton = current.HomeShowAboutButton,
            HomeShowFeatures    = current.HomeShowFeatures,
            HomeShowRoadmap     = current.HomeShowRoadmap,
        }, actor);
        TempData["info"] = "The about page's hero text and section toggles have been saved.";
        return RedirectToAction(nameof(Index));
    }

    // ── View model (public nested type so the Razor view can bind to it —
    //    the AdminSignupController.SignupAdminViewModel shape) ──

    /// <summary>
    /// The <c>/admin/site</c> form model — the 13 <c>SiteContent</c> fields
    /// (the ADR 0150 D1 ceiling) grouped into the two form sections (Home +
    /// About). The field names match the <c>SiteContent</c> POCO exactly, so
    /// the Razor model binding works with no adapter.
    /// </summary>
    public sealed class AdminSiteViewModel
    {
        public HomeSection Home { get; init; } = new();

        public AboutSection About { get; init; } = new();

        /// <summary>The <c>/home</c> form section — the 5 Home fields
        /// (the 2 text fields + the 3 section toggles).</summary>
        public sealed class HomeSection
        {
            public string HomeHeroEyebrow { get; init; } = string.Empty;

            public string HomeHeroLead { get; init; } = string.Empty;

            public bool HomeShowAboutButton { get; init; } = true;

            public bool HomeShowFeatures { get; init; } = true;

            public bool HomeShowRoadmap { get; init; } = true;
        }

        /// <summary>The <c>/about</c> form section — the 8 About fields
        /// (the 2 text fields + the 6 section toggles).</summary>
        public sealed class AboutSection
        {
            public string AboutHeroEyebrow { get; init; } = string.Empty;

            public string AboutHeroLead { get; init; } = string.Empty;

            public bool AboutShowFeatures { get; init; } = true;

            public bool AboutShowScope { get; init; } = true;

            public bool AboutShowPhilosophy { get; init; } = true;

            public bool AboutShowProject { get; init; } = true;

            public bool AboutShowWhatsNew { get; init; } = true;

            public bool AboutShowContactCta { get; init; } = true;
        }
    }
}
