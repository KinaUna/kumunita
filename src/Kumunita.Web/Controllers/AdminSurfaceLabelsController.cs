using Kumunita.Core.Localization;
using Kumunita.Core.SurfaceLabels;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/labels</c> surface (ADR 0152, M29·7) — the GlobalAdmin's edit
/// page for the **13 top-navigation surfaces**' display labels (<see
/// cref="ISurfaceLabelsService"/>). Mirrors <see cref="AdminSiteController"/>
/// (ADR 0150) exactly: <see cref="Roles.GlobalAdmin"/>-gated (the
/// <c>AdminController</c> precedent), a thin wrapper over the
/// <see cref="ISurfaceLabelsService"/> seams (<see cref="ISurfaceLabelsService.GetAsync"/>
/// / <see cref="ISurfaceLabelsService.SaveAsync"/>), and the audit row is the
/// **service's** (exactly one <c>AccessAudit</c>, <c>Via = Admin</c>, action
/// <c>surface_labels.save</c>, <c>TargetKind</c> "surface-labels" — the
/// singleton-toggle shape, the same as <c>site.save</c> /
/// <c>signup.set-open</c> / <c>timezone.set-default</c>).
/// <para>
/// <b>Why a dedicated controller.</b> The <c>AdminController</c>'s constructor
/// is pinned by two Web-layer test harnesses (<c>AdminControllerBlockTests</c>
/// / <c>AdminControllerMandatoryTests</c>), so a new dependency there would
/// break them. A separate <c>/admin/labels</c> surface (mirroring
/// <c>/admin/site</c> / <c>/admin/signup</c> / <c>/admin/timezone</c>) is the
/// convention-consistent shape (M29·7).
/// </para>
/// <para>
/// <b>The label is a display override, never a re-route</b> (ADR 0152 D2): the
/// save only changes the <b>text</b> the nav item and the surface's
/// <c>&lt;h1&gt;</c> header show — it never moves a link, never changes a
/// route, never touches an <c>asp-route-*</c> / <c>href</c>. A <b>blank</b>
/// field **clears** that surface's label (it falls back to the surface's
/// <c>kw-l</c> key in the viewer's effective language — the "empty = use
/// default" shape, M29·3).
/// </para>
/// <para>
/// <b>The save is the full 13-field set</b> (ADR 0152 D1, M29·5): unlike
/// <see cref="AdminSiteController"/> (which saves one section at a time),
/// <c>/admin/labels</c> is a single 13-row form with a single Save button — the
/// POST loads the current singleton, applies the full 13-field set from the
/// form, and saves in one session. The service's <see
/// cref="ISurfaceLabelsService.SaveAsync"/> is the single audited write lane
/// (exactly one <c>AccessAudit</c> row, strong consistency — the new value is
/// live on the very next render, invariant C4). A missing row is a no-op in
/// the service (the seeder is the only row-creator, M29·6) — this controller
/// never creates a row.
/// </para>
/// </summary>
[Route("admin/labels")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminSurfaceLabelsController(
    ISurfaceLabelsService labels,
    ILocalizationService? localization = null) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// Resolves the enabled <see cref="Kumunita.Core.Localization
    /// .LanguageCatalog"/> set for the translation pickers (ADR 0158, the ADR
    /// 0018 / ADR 0022 <c>SeedLanguagePickerAsync</c> shape — the instance's
    /// enabled languages, ordered by <c>SortOrder</c>). Best-effort: a missing
    /// seam (a test construction with no localization) or a read failure yields
    /// an empty list, so the page still renders (the translation section simply
    /// offers no candidate languages). A read — never an audit row. The exact
    /// <see cref="AdminSiteController.EnabledLanguagesAsync"/> idiom carried
    /// over.
    /// </summary>
    private Task<IReadOnlyList<(string Code, string NativeName)>> EnabledLanguagesAsync() =>
        localization is null
            ? Task.FromResult<IReadOnlyList<(string Code, string NativeName)>>([])
            : ComposerSeedOptions.SeedLanguagePickerAsync(localization);

    /// <summary>Resolves a BCP-47 code to its catalog <c>NativeName</c> for a
    /// <c>TempData</c> confirmation message (the ADR 0157
    /// <see cref="AdminSiteController"/> <c>LanguageName</c> idiom). Falls back
    /// to the raw code when the language is not in the catalog (a never-blank
    /// shape).</summary>
    private async Task<string> LanguageName(string code)
    {
        if (localization is null) return code;
        var catalog = await localization.ListLanguagesAsync();
        return catalog.FirstOrDefault(l => l.Id == code)?.NativeName ?? code;
    }

    /// <summary>
    /// <c>GET /admin/labels</c> — seeds the 13-row form with the current
    /// singleton's **stored** label set (<see cref="ISurfaceLabelsService.GetAsync"/>,
    /// the ADR 0050 <c>IsSignupOpenAsync</c> best-effort shape — a missing row
    /// degrades to the all-null fallback, so every field renders blank and the
    /// form always renders; M29·3 / M29·4). A blank field means "no admin label
    /// set — the nav + header fall back to the surface's <c>kw-l</c> key."
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var current = await labels.GetAsync();

        // ADR 0158 — the LBL-2 surface-label translation section (the ADR 0152
        // §D8 "future LBL-2 translation lane"). Best-effort reads: a missing
        // seam, a missing store, or a read failure degrades to empty lists, so
        // the page always renders (the translation section simply offers no
        // candidates). The enabled languages come from the instance's
        // LanguageCatalog (the ADR 0018 composer-pickup shape); each enabled
        // language is flagged with whether a translation row already exists
        // (the exact AdminSiteController.Index translation-seeding idiom).
        var enabled = await EnabledLanguagesAsync();
        IReadOnlyList<SurfaceLabelTranslation> translations = [];
        try
        {
            translations = await labels.GetTranslationsAsync();
        }
        catch
        {
            translations = []; // floor: no translations (the labels resolve the singleton)
        }
        var translationCodes = translations.Select(t => t.LanguageCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var languages = enabled
            .Select(l => new LanguageOption(l.Code, l.NativeName, translationCodes.Contains(l.Code)))
            .ToList();

        var model = new AdminSurfaceLabelsViewModel
        {
            // ADR 0158 — the surface-label translation section (populated for
            // both the add picker + the existing-row edit/remove affordances).
            Languages = languages,
            Translations = translations,
            Home        = current.Home,
            Announcements = current.Announcements,
            Community   = current.Community,
            Groups      = current.Groups,
            Events      = current.Events,
            Projects    = current.Projects,
            Inventory   = current.Inventory,
            Bookmarks   = current.Bookmarks,
            Documents   = current.Documents,
            Pages       = current.Pages,
            Tags        = current.Tags,
            Directory   = current.Directory,
            People      = current.People,
        };

        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/labels</c> — saves the full 13-field label set (the ADR
    /// 0152 D1 ceiling). Delegates to <see cref="ISurfaceLabelsService.SaveAsync"/>
    /// (the single audited write lane — exactly one <c>AccessAudit</c> row,
    /// <c>Via = Admin</c>, action <c>surface_labels.save</c>, <c>TargetKind</c>
    /// "surface-labels" — the <c>site.save</c> shape, M29·5). A blank field
    /// clears that surface's label (it falls back to the surface's
    /// <c>kw-l</c> key). Success → a surfaced <c>info</c> + redirect (the
    /// change is live on the very next render — strong consistency, invariant
    /// C4).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminSurfaceLabelsViewModel model)
    {
        var actor = ActorId(User) ?? string.Empty;
        await labels.SaveAsync(new SurfaceLabels
        {
            // The full 13-field set, verbatim from the form (a blank field
            // stores blank → falls back to the kw-l key at resolution, M29·3).
            Home        = model.Home,
            Announcements = model.Announcements,
            Community   = model.Community,
            Groups      = model.Groups,
            Events      = model.Events,
            Projects    = model.Projects,
            Inventory   = model.Inventory,
            Bookmarks   = model.Bookmarks,
            Documents   = model.Documents,
            Pages       = model.Pages,
            Tags        = model.Tags,
            Directory   = model.Directory,
            People      = model.People,
        }, actor);
        TempData["info"] = "The surface labels have been saved.";
        return RedirectToAction(nameof(Index));
    }

    // ── ADR 0158 — the LBL-2 surface-label translation lanes (the ADR 0152
    //    §D8 "future LBL-2 translation lane"; the ADR 0157 SITE-2
    //    hero-translation shape, carried onto the surface-labels surface):
    //    add a translation into a language, edit the existing row for a
    //    language, or remove it. The [Authorize(Roles = GlobalAdmin)] class
    //    gate is the standing (the ADR 0152 D8 GlobalAdmin pin — the surface
    //    labels have no per-resident owner, so the Translator standing does
    //    not qualify, ADR 0021); the service writes the AccessAudit row
    //    (surface_labels_translation.add / .update / .remove, Via = Admin). ──

    /// <summary>
    /// <c>POST /admin/labels/translations</c> — adds (or overwrites, the
    /// upsert shape) a translation of the 13 surface labels into
    /// <paramref name="languageCode"/> (ADR 0158; the ADR 0157
    /// <see cref="AdminSiteController.AddTranslation"/> shape, carried onto
    /// the surface labels). At least one of the 13 label fields must be
    /// non-blank (the write seam rejects an empty translation); blank fields
    /// leave that surface at the singleton's value (the fallback). One
    /// <c>AccessAudit</c> row (the service's
    /// <c>surface_labels_translation.add</c>), strong-consistency (live on the
    /// next render in that language).
    /// </summary>
    [HttpPost("translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTranslation([FromForm] SurfaceLabelTranslationForm form)
    {
        if (string.IsNullOrWhiteSpace(form.LanguageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Index));
        }
        if (form.IsBlank())
        {
            TempData["error"] = "A translation needs at least one surface label.";
            return RedirectToAction(nameof(Index));
        }

        var actor = ActorId(User) ?? string.Empty;
        await labels.AddTranslationAsync(
            form.LanguageCode, form.ToSurfaceLabelTranslation(), actor);

        var name = await LanguageName(form.LanguageCode);
        TempData["info"] = $"Translation added ({name}).";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>POST /admin/labels/translations/update</c> — updates the existing
    /// translation for <paramref name="form.LanguageCode"/> (ADR 0158; the
    /// ADR 0157 <see cref="AdminSiteController.UpdateTranslation"/> shape).
    /// The 13 label fields are replaced verbatim (a blank field clears that
    /// surface's override → the singleton's value is the fallback again); at
    /// least one must be non-blank. A missing row is a no-op redirect with a
    /// flash (a double-update is a shape error for this route — the page
    /// offers the edit affordance only for languages that have a row). One
    /// <c>AccessAudit</c> row (the service's
    /// <c>surface_labels_translation.update</c>).
    /// </summary>
    [HttpPost("translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTranslation([FromForm] SurfaceLabelTranslationForm form)
    {
        if (string.IsNullOrWhiteSpace(form.LanguageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Index));
        }
        if (form.IsBlank())
        {
            TempData["error"] = "A translation needs at least one surface label.";
            return RedirectToAction(nameof(Index));
        }

        var actor = ActorId(User) ?? string.Empty;
        try
        {
            await labels.UpdateTranslationAsync(
                form.LanguageCode, form.ToSurfaceLabelTranslation(), actor);
        }
        catch (KeyNotFoundException)
        {
            TempData["error"] = "That translation no longer exists; add it again.";
            return RedirectToAction(nameof(Index));
        }

        var name = await LanguageName(form.LanguageCode);
        TempData["info"] = $"Translation updated ({name}).";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>POST /admin/labels/translations/remove</c> — removes the existing
    /// translation for <paramref name="languageCode"/> (ADR 0158; the ADR
    /// 0157 <see cref="AdminSiteController.RemoveTranslation"/> shape). The
    /// surface labels revert to the singleton's value in that language. A
    /// missing row is a no-op redirect with a flash (a double-remove is a
    /// shape error for this route — the page offers the remove affordance only
    /// for languages that have a row). One <c>AccessAudit</c> row (the
    /// service's <c>surface_labels_translation.remove</c>).
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
            await labels.RemoveTranslationAsync(languageCode, actor);
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
    //    the AdminSiteController.AdminSiteViewModel shape) ──

    /// <summary>
    /// The <c>/admin/labels</c> form model — the 13 <c>SurfaceLabels</c> label
    /// override fields (the ADR 0152 D1 ceiling). The field names match the
    /// <c>SurfaceLabels</c> POCO exactly, so Razor model binding works with no
    /// adapter. Each is <c>string?</c>: <c>null</c> / blank = "no admin label —
    /// use the surface's <c>kw-l</c> fallback" (M29·3).
    /// </summary>
    public sealed class AdminSurfaceLabelsViewModel
    {
        /// <summary>The Home nav/header label (fallback <c>nav.home</c>).</summary>
        public string? Home { get; init; }

        /// <summary>The Announcements nav/header label (fallback
        /// <c>nav.announcements</c>).</summary>
        public string? Announcements { get; init; }

        /// <summary>The Community nav/header label (fallback
        /// <c>nav.community</c>).</summary>
        public string? Community { get; init; }

        /// <summary>The Groups nav/header label (fallback <c>nav.groups</c>).</summary>
        public string? Groups { get; init; }

        /// <summary>The Events nav/header label (fallback <c>nav.events</c>).</summary>
        public string? Events { get; init; }

        /// <summary>The Projects nav/header label (fallback <c>nav.projects</c>).</summary>
        public string? Projects { get; init; }

        /// <summary>The Inventory nav/header label (fallback <c>inv.nav</c>).</summary>
        public string? Inventory { get; init; }

        /// <summary>The Bookmarks nav/header label (fallback <c>bm.nav</c>).</summary>
        public string? Bookmarks { get; init; }

        /// <summary>The Documents nav/header label (fallback <c>documents.title</c>).</summary>
        public string? Documents { get; init; }

        /// <summary>The Pages nav/header label (fallback <c>nav.pages</c>).</summary>
        public string? Pages { get; init; }

        /// <summary>The Tags nav/header label (fallback <c>nav.tags</c>).</summary>
        public string? Tags { get; init; }

        /// <summary>The Directory nav/header label (fallback <c>nav.directory</c>).</summary>
        public string? Directory { get; init; }

        /// <summary>The People nav/header label (fallback <c>nav.people</c>).</summary>
        public string? People { get; init; }

        // ── ADR 0158 — the LBL-2 surface-label translation section ───────
        // [BindNever] — the translation write lanes are dedicated POST
        // actions (the add / update / remove shape, the ADR 0157 SITE-2
        // precedent), not part of the 13-row form group; these are
        // display-only seeds for the translation UI. The exact
        // AdminSiteViewModel.Languages / .Translations idiom.

        /// <summary>Every enabled <see cref="Kumunita.Core.Localization
        /// .LanguageCatalog"/> language (in <c>SortOrder</c>) with its
        /// <see cref="LanguageOption.HasTranslation"/> flag — the set the
        /// "add a translation" candidate list + the "existing translations"
        /// edit/remove affordances render from (the ADR 0157 detail-surface
        /// <c>LanguageOption</c> shape, shared).</summary>
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public IReadOnlyList<LanguageOption> Languages { get; init; } = [];

        /// <summary>The singleton's existing surface-label translations (one
        /// row per language, the ADR 0158 <c>SurfaceLabelTranslation</c> set)
        /// — the rows the edit/remove affordances render from, and the source
        /// the "add" candidate list excludes.</summary>
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public IReadOnlyList<SurfaceLabelTranslation> Translations { get; init; } = [];
    }

    /// <summary>
    /// The <c>[FromForm]</c> binding shape for the three translation POST
    /// actions (the ADR 0157 <see cref="AdminSiteController"/>
    /// <c>[FromForm]</c> idiom, carried onto the 13 surface-label fields).
    /// The 13 label properties match the <see cref="SurfaceLabelTranslation"/>
    /// POCO exactly, so the form field names bind with no adapter;
    /// <see cref="ToSurfaceLabelTranslation"/> projects it onto the Core
    /// doc. <see cref="IsBlank"/> is the controller's pre-check for the
    /// service's "at least one non-blank" rule (a defensive early flash
    /// before the <c>ArgumentException</c> the service throws — the same
    /// shape the <c>AddTranslation</c> / <c>UpdateTranslation</c> actions
    /// use for the <c>languageCode</c> blank-check).
    /// </summary>
    public sealed class SurfaceLabelTranslationForm
    {
        /// <summary>The BCP-47 code of the target language (a
        /// <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/>).</summary>
        public string? LanguageCode { get; init; }

        /// <summary>The Home label (optional — the singleton's value is the
        /// fallback).</summary>
        public string? Home { get; init; }

        /// <summary>The Announcements label (optional).</summary>
        public string? Announcements { get; init; }

        /// <summary>The Community label (optional).</summary>
        public string? Community { get; init; }

        /// <summary>The Groups label (optional).</summary>
        public string? Groups { get; init; }

        /// <summary>The Events label (optional).</summary>
        public string? Events { get; init; }

        /// <summary>The Projects label (optional).</summary>
        public string? Projects { get; init; }

        /// <summary>The Inventory label (optional).</summary>
        public string? Inventory { get; init; }

        /// <summary>The Bookmarks label (optional).</summary>
        public string? Bookmarks { get; init; }

        /// <summary>The Documents label (optional).</summary>
        public string? Documents { get; init; }

        /// <summary>The Pages label (optional).</summary>
        public string? Pages { get; init; }

        /// <summary>The Tags label (optional).</summary>
        public string? Tags { get; init; }

        /// <summary>The Directory label (optional).</summary>
        public string? Directory { get; init; }

        /// <summary>The People label (optional).</summary>
        public string? People { get; init; }

        /// <summary>True when all 13 label fields are blank (the controller's
        /// pre-check for the service's "at least one non-blank" rule — a
        /// translation with nothing in it is not a translation, the ADR 0157
        /// "at least one non-blank" rule carried from the ADR 0026
        /// group/community name+description rule).</summary>
        public bool IsBlank() =>
            string.IsNullOrWhiteSpace(Home) &&
            string.IsNullOrWhiteSpace(Announcements) &&
            string.IsNullOrWhiteSpace(Community) &&
            string.IsNullOrWhiteSpace(Groups) &&
            string.IsNullOrWhiteSpace(Events) &&
            string.IsNullOrWhiteSpace(Projects) &&
            string.IsNullOrWhiteSpace(Inventory) &&
            string.IsNullOrWhiteSpace(Bookmarks) &&
            string.IsNullOrWhiteSpace(Documents) &&
            string.IsNullOrWhiteSpace(Pages) &&
            string.IsNullOrWhiteSpace(Tags) &&
            string.IsNullOrWhiteSpace(Directory) &&
            string.IsNullOrWhiteSpace(People);

        /// <summary>Projects this form onto the Core
        /// <see cref="SurfaceLabelTranslation"/> doc (the 13 label fields; the
        /// <see cref="LanguageCode"/> is carried separately by the
        /// service-lane parameters — the ADR 0157 "form fields are the
        /// translation's value, the languageCode is the key" split).</summary>
        public SurfaceLabelTranslation ToSurfaceLabelTranslation() => new()
        {
            Home        = Home,
            Announcements = Announcements,
            Community   = Community,
            Groups      = Groups,
            Events      = Events,
            Projects    = Projects,
            Inventory   = Inventory,
            Bookmarks   = Bookmarks,
            Documents   = Documents,
            Pages       = Pages,
            Tags        = Tags,
            Directory   = Directory,
            People      = People,
        };
    }
}
