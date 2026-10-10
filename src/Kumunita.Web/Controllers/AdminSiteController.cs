using Kumunita.Core.Localization;
using Kumunita.Core.SiteContent;
using Kumunita.Web.Models;
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
    ISiteContentService site,
    ILocalizationService? localization = null) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// Resolves the enabled <see cref="LanguageCatalog"/> set for the
    /// translation pickers (ADR 0157, the ADR 0018 / ADR 0022
    /// <c>SeedLanguagePickerAsync</c> shape — the instance's enabled
    /// languages, ordered by <c>SortOrder</c>). Best-effort: a missing seam
    /// (a test construction with no localization) or a read failure yields an
    /// empty list, so the page still renders (the translation section simply
    /// offers no candidate languages). A read — never an audit row.
    /// </summary>
    private Task<IReadOnlyList<(string Code, string NativeName)>> EnabledLanguagesAsync() =>
        localization is null
            ? Task.FromResult<IReadOnlyList<(string Code, string NativeName)>>([])
            : ComposerSeedOptions.SeedLanguagePickerAsync(localization);

    /// <summary>Resolves a BCP-47 code to its catalog <c>NativeName</c> for a
    /// <c>TempData</c> confirmation message (the ADR 0022
    /// <c>SeedLanguageName</c> display convenience). Falls back to the raw
    /// code when the language is not in the catalog (a never-blank shape).</summary>
    private async Task<string> LanguageName(string code)
    {
        if (localization is null) return code;
        var catalog = await localization.ListLanguagesAsync();
        return catalog.FirstOrDefault(l => l.Id == code)?.NativeName ?? code;
    }

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

        // ADR 0157 — the SITE-2 hero-translation section (the ADR 0150 §D5
        // "future SITE-2 translation lane"). Best-effort reads: a missing seam,
        // a missing store, or a read failure degrades to empty lists, so the
        // page always renders (the translation section simply offers no
        // candidates). The enabled languages come from the instance's
        // LanguageCatalog (the ADR 0018 composer-pickup shape); each enabled
        // language is flagged with whether a translation row already exists.
        var enabled = await EnabledLanguagesAsync();
        IReadOnlyList<SiteContentTranslation> translations = [];
        try
        {
            translations = await site.GetTranslationsAsync();
        }
        catch
        {
            translations = []; // floor: no translations (the hero renders the singleton)
        }
        var translationCodes = translations.Select(t => t.LanguageCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var languages = enabled
            .Select(l => new LanguageOption(l.Code, l.NativeName, translationCodes.Contains(l.Code)))
            .ToList();

        var model = new AdminSiteViewModel
        {
            // ADR 0157 — the hero-translation section (populated for both the
            // add picker + the existing-row edit/remove affordances).
            Languages = languages,
            Translations = translations,
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

    // ── ADR 0157 — the SITE-2 hero-translation lanes (the ADR 0022 / ADR
    //    0048 post-translation shape, adapted to the singleton): add a
    //    translation into a language, edit the existing row for a language,
    //    or remove it. The [Authorize(Roles = GlobalAdmin)] class gate is
    //    the standing (the ADR 0150 D8 GlobalAdmin pin — the site content
    //    has no per-resident owner, so the Translator standing does not
    //    qualify, ADR 0021); the service writes the AccessAudit row
    //    (sitetranslation.add / .update / .remove, Via = Admin). ───────────

    /// <summary>
    /// <c>POST /admin/site/translations</c> — adds (or overwrites, the
    /// upsert shape) a translation of the two heroes' eyebrow + lead into
    /// <paramref name="languageCode"/> (ADR 0157; the ADR 0022
    /// <see cref="PostsController.AddTranslation"/> shape). At least one of
    /// the four hero fields must be non-blank (the write seam rejects an
    /// empty translation); blank fields leave that hero at the singleton's
    /// value (the fallback). One <c>AccessAudit</c> row (the service's
    /// <c>sitetranslation.add</c>), strong-consistency (live on the next
    /// render in that language).
    /// </summary>
    [HttpPost("translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTranslation(
        [FromForm] string? languageCode,
        [FromForm] string? homeHeroEyebrow,
        [FromForm] string? homeHeroLead,
        [FromForm] string? aboutHeroEyebrow,
        [FromForm] string? aboutHeroLead)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Index));
        }
        if (string.IsNullOrWhiteSpace(homeHeroEyebrow) &&
            string.IsNullOrWhiteSpace(homeHeroLead) &&
            string.IsNullOrWhiteSpace(aboutHeroEyebrow) &&
            string.IsNullOrWhiteSpace(aboutHeroLead))
        {
            TempData["error"] = "A translation needs at least one hero field.";
            return RedirectToAction(nameof(Index));
        }

        var actor = ActorId(User) ?? string.Empty;
        await site.AddTranslationAsync(
            languageCode, homeHeroEyebrow, homeHeroLead, aboutHeroEyebrow, aboutHeroLead, actor);

        var name = await LanguageName(languageCode);
        TempData["info"] = $"Translation added ({name}).";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>POST /admin/site/translations/update</c> — updates the existing
    /// translation for <paramref name="languageCode"/> (ADR 0157; the ADR
    /// 0048 <see cref="PostsController.UpdateTranslation"/> shape). The four
    /// hero fields are replaced verbatim (a blank field clears that hero's
    /// override → the singleton's value is the fallback again); at least one
    /// must be non-blank. A missing row is a no-op redirect with a flash
    /// (a double-update is a shape error for this route — the page offers
    /// the edit affordance only for languages that have a row). One
    /// <c>AccessAudit</c> row (the service's <c>sitetranslation.update</c>).
    /// </summary>
    [HttpPost("translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTranslation(
        [FromForm] string? languageCode,
        [FromForm] string? homeHeroEyebrow,
        [FromForm] string? homeHeroLead,
        [FromForm] string? aboutHeroEyebrow,
        [FromForm] string? aboutHeroLead)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Index));
        }
        if (string.IsNullOrWhiteSpace(homeHeroEyebrow) &&
            string.IsNullOrWhiteSpace(homeHeroLead) &&
            string.IsNullOrWhiteSpace(aboutHeroEyebrow) &&
            string.IsNullOrWhiteSpace(aboutHeroLead))
        {
            TempData["error"] = "A translation needs at least one hero field.";
            return RedirectToAction(nameof(Index));
        }

        var actor = ActorId(User) ?? string.Empty;
        try
        {
            await site.UpdateTranslationAsync(
                languageCode, homeHeroEyebrow, homeHeroLead, aboutHeroEyebrow, aboutHeroLead, actor);
        }
        catch (KeyNotFoundException)
        {
            TempData["error"] = "That translation no longer exists; add it again.";
            return RedirectToAction(nameof(Index));
        }

        var name = await LanguageName(languageCode);
        TempData["info"] = $"Translation updated ({name}).";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>POST /admin/site/translations/remove</c> — removes the existing
    /// translation for <paramref name="languageCode"/> (ADR 0157; the ADR
    /// 0048 <see cref="PostsController.RemoveTranslation"/> shape). The hero
    /// reverts to the singleton's value in that language. A missing row is a
    /// no-op redirect with a flash (a double-remove is a shape error for this
    /// route — the page offers the remove affordance only for languages that
    /// have a row). One <c>AccessAudit</c> row (the service's
    /// <c>sitetranslation.remove</c>).
    /// </summary>
    [HttpPost("translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTranslation([FromForm] string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language to remove.";
            return RedirectToAction(nameof(Index));
        }

        var actor = ActorId(User) ?? string.Empty;
        try
        {
            await site.RemoveTranslationAsync(languageCode, actor);
        }
        catch (KeyNotFoundException)
        {
            TempData["error"] = "That translation no longer exists.";
            return RedirectToAction(nameof(Index));
        }

        var name = await LanguageName(languageCode);
        TempData["info"] = $"Translation removed ({name}).";
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

        // ── ADR 0157 — the SITE-2 hero-translation section ─────────────
        // [BindNever] — the translation write lanes are dedicated POST
        // actions (the add / update / remove shape, the ADR 0022 / ADR 0048
        // precedent), not part of the home/about form groups; these are
        // display-only seeds for the translation UI.

        /// <summary>Every enabled <see cref="Kumunita.Core.Localization
        /// .LanguageCatalog"/> language (in <c>SortOrder</c>) with its
        /// <see cref="LanguageOption.HasTranslation"/> flag — the set the
        /// "add a translation" candidate list + the "existing translations"
        /// edit/remove affordances render from (the ADR 0022 detail-surface
        /// <c>LanguageOption</c> shape, shared).</summary>
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public IReadOnlyList<LanguageOption> Languages { get; init; } = [];

        /// <summary>The singleton's existing hero translations (one row per
        /// language, the ADR 0157 <c>SiteContentTranslation</c> set) — the
        /// rows the edit/remove affordances render from, and the source the
        /// "add" candidate list excludes.</summary>
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public IReadOnlyList<SiteContentTranslation> Translations { get; init; } = [];

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
