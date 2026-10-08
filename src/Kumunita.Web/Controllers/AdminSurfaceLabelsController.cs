using Kumunita.Core.SurfaceLabels;
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
    ISurfaceLabelsService labels) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

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
        var model = new AdminSurfaceLabelsViewModel
        {
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
    }
}
