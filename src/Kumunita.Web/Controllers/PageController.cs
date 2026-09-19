using Authorization = Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <b>Pages</b> lane's Web surface (the <c>PG</c> lane, ADR 0039 §3.8) —
/// the surface that reads and writes the <see cref="Page"/> /
/// <see cref="PageTranslation"/> docs (the single source; the legacy
/// per-slug static-page doc was retired in U07). Six route groups, over the
/// frozen <see cref="IPageService"/> seams (CQRS-lite, ADR 0039):
/// <list type="bullet">
/// <item><c>GET /pages</c> — the tree browse (the
///       <see cref="Kumunita.Core.Authorization.IAuthorizationService.CanSeeAsync"/>(Read)-filtered
///       forest; a denied page is <b>absent</b>, not blanked).</item>
/// <item><c>GET /pages/{**path}</c> — the post view (one
///       <see cref="MarkdownRenderer"/> over the body; the ADR 0027
///       chip-swap across the <see cref="PageTranslation"/> rows).</item>
/// <item><c>GET/POST /pages/new</c> — the composer (title, WYSIWYG body,
///       parent picker, audience, language, mount point).</item>
/// <item><c>GET/POST /pages/{id}/edit</c> — the edit lane (round-trips
///       through the <see cref="AudienceEditorModel"/> /
///       <see cref="AudienceEditorModel.BuildAudience"/> single source).</item>
/// <item><c>POST /pages/{id}/publish</c> — the author-only draft→published
///       flip (ADR 0037).</item>
/// <item><c>POST /pages/{id}/delete</c> / <c>POST /pages/{id}/move</c> —
///       the admin/Moderator soft-delete + reparent lanes.</item>
/// </list>
/// <para>
/// **404 vs 403 are DISTINCT here and MUST NOT be collapsed** (ADR 0039
/// §3.8; the page-specific split, unlike posts/announcements which 404 both):
/// <c>GET /pages/{**path}</c> returns <b>404</b> on a page that is absent
/// (<see cref="KeyNotFoundException"/> from <see cref="IPageService
/// .GetByPathAsync"/>) and <b>403</b> on a page that exists but is denied to
/// the caller (a <see cref="Decision"/> deny, or a draft the caller did not
/// author). The write lanes' standing (author / Moderator / GlobalAdmin, the
/// ADR 0039 §3.7 table) is re-checked by the <b>service</b> (C3); the
/// controller's <c>[Authorize]</c>/<c>[Authorize(Roles=...)]</c> are the
/// Web-layer pre-gates, and a <see cref="UnauthorizedAccessException"/> from
/// the service is surfaced as <b>403</b> (not folded into a 404).
/// </para>
/// <para>
/// **Two invariants (U03 handoff):** (a) the Web layer sets
/// <see cref="Page.ImageIds"/> / <see cref="Page.AttachmentIds"/> (via
/// <see cref="ContentImageIds.ExtractContentImageIds"/> /
/// <see cref="AttachmentIds.ExtractAttachmentIds"/> in
/// <c>Kumunita.Web.Security</c>) <b>before</b> calling a write lane — Core
/// normalizes <c>?? []</c> and never parses the body; (b) <see
/// cref="IPageService.PublishAsync"/> is author-only (ADR 0037 — no
/// <c>actorRoles</c> param) and Move/Delete are admin/Moderator (NOT author).
/// </para>
/// </summary>
[Route("pages")]
public sealed class PageController(
    IPageService pages,
    Authorization.IAuthorizationService authz,
    ILocalizationService localization,
    IUserInfoService userInfo,
    IDocumentStore store) : Controller
{
    // ── GET /pages — the tree browse (C6 CanSeeAsync(Read)-filtered) ─────────

    /// <summary>
    /// <c>GET /pages</c> — the tree browse: the live <see cref="Page"/>
    /// forest, <see cref="Kumunita.Core.Authorization.IAuthorizationService.CanSeeAsync"/>(Read)-filtered
    /// to the caller's visible set (a denied page is <b>absent</b> from the
    /// rendered tree — not blanked), with the author-only draft gate applied
    /// (a non-author never sees a <see cref="Page.IsDraft"/> page). A visible
    /// page whose parent is denied is hoisted to the top level (it is still
    /// reachable by its full path — the path is the real address). An empty
    /// forest is a valid shape (the view renders a "no pages yet" note).
    /// </summary>
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> Index()
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;

        var tree = await pages.GetTreeAsync().ConfigureAwait(false);

        // (1) the author-only draft gate (Web-layer pin): a non-author never
        //     sees a draft (a draft is its author's, until published).
        var candidate = tree
            .Where(p => !(p.IsDraft && !string.Equals(p.AuthorId, actorId, StringComparison.Ordinal)))
            .ToList();

        // (2) the Read decision over the whole candidate set — ONE
        //     CanSeeAsync (the C6 aggregate, not one per page).
        var visible = candidate;
        if (candidate.Count > 0)
        {
            var visibleIds = (await authz.CanSeeAsync(
                    actorId, Authorization.AccessAction.Read,
                    candidate.Select(p => new PageToAuditableResource(p)))
                .ConfigureAwait(false)).Visible
                .Select(v => v.Id)
                .ToHashSet(StringComparer.Ordinal);
            visible = candidate.Where(p => visibleIds.Contains(p.Id)).ToList();
        }

        var byId = tree.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var visibleSet = visible.ToDictionary(p => p.Id, StringComparer.Ordinal);

        // The forest: a visible page's children are the visible pages nested
        // under it; a visible page whose parent is absent (denied) is a root.
        PageNode ToNode(Page p)
        {
            var kids = visible
                .Where(c => string.Equals(c.ParentId, p.Id, StringComparison.Ordinal))
                .ToList();
            return new PageNode(
                p.Id, p.Title, PagePaths.Href(byId, p), p.IsDraft,
                kids.Select(ToNode).ToList());
        }

        var roots = visible
            .Where(p => p.ParentId is null || !visibleSet.ContainsKey(p.ParentId))
            .ToList();

        return View(new PageTreeViewModel(roots.Select(ToNode).ToList()));
    }

    // ── GET /pages/{**path} — the post view (404 absent / 403 denied) ───────

    /// <summary>
    /// <c>GET /pages/{**path}</c> — the post view: the full-body read of the
    /// page at that path (the ADR 0039 §3.3 ancestor-slug chain). <b>404</b>
    /// on absent (<see cref="KeyNotFoundException"/>), <b>403</b> on denied
    /// (a <see cref="Decision"/> deny, or a draft the caller did not author)
    /// — the page-specific split, MUST NOT be collapsed (ADR 0039 §3.8). A
    /// public (null-audience) page is world-readable to anonymous callers; a
    /// non-public one is 403 to them.
    /// </summary>
    [HttpGet]
    [Route("{**path}")]
    public async Task<IActionResult> Show(string path)
    {
        Page page;
        try
        {
            page = await pages.GetByPathAsync(path).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var actorId = KumunitaPrincipal.SubjectId(User);

        // Draft gate (author-only, Web-layer pin): a non-author never sees a
        // draft — 403 (denied), even if they would pass the Read decision.
        if (page.IsDraft && !string.Equals(page.AuthorId, actorId ?? string.Empty, StringComparison.Ordinal))
            return new ForbidResult();

        // The Read decision.
        if (actorId is null)
        {
            // Anonymous: a public (null-audience) page is world-readable; a
            // non-public one is denied to them (403 — the page-specific split).
            if (page.Audience is not null)
                return new ForbidResult();
        }
        else
        {
            var decision = await authz.CanAsync(
                actorId, Authorization.AccessAction.Read, new PageToAuditableResource(page)).ConfigureAwait(false);
            if (!decision.Allowed)
                return new ForbidResult();
        }

        var translations = await pages.GetTranslationsAsync(page.Id).ConfigureAwait(false);
        var tree = await pages.GetTreeAsync().ConfigureAwait(false);
        var byId = tree.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var isAuthor = string.Equals(page.AuthorId, actorId ?? string.Empty, StringComparison.Ordinal);

        // The ADR 0027 chip-swap language set: the enabled catalog, marking
        // which have a stored PageTranslation row (the original is always
        // available — it IS the page's own language).
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        var languages = catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .Select(l => new LanguageOption(
                l.Id, l.NativeName,
                l.Id == page.LanguageCode
                    || translations.Any(t => t.LanguageCode == l.Id)))
            .ToList();

        // Display names (best-effort — a missing profile / component is a
        // display gap, not an error; the page still renders).
        var authorProfile = string.IsNullOrEmpty(page.AuthorId) ? null : await userInfo.GetProfileAsync(page.AuthorId).ConfigureAwait(false);
        string? communityName = null;
        if (page.ComponentId is { } component)
            communityName = (await userInfo.GetComponentsAsync(true).ConfigureAwait(false))
                .FirstOrDefault(c => c.Id == component)?.Name;

        // ADR 0029 — the "add a translation" affordance flag (the display pin):
        // the same rule the AddTranslationAsync write-lane gate re-checks
        // server-side (U06; the PostService.CanAddTranslation /
        // AnnouncementService.CanTranslateAnnouncement shape). A display
        // convenience only — the real gate is the service (C3).
        var canTranslate = PageService.CanTranslatePage(
            actorId ?? string.Empty, KumunitaPrincipal.RoleSet(User), page);

        return View(new PageShowViewModel(
            page.Id,
            page.Title,
            page.Body,
            PagePaths.Href(byId, page),
            page.LanguageCode,
            translations,
            languages,
            isAuthor,
            page.IsDraft,
            isAuthor,
            authorProfile?.DisplayName,
            communityName,
            canTranslate
        ));
    }

    // ── GET/POST /pages/new — the composer ───────────────────────────────────

    /// <summary>
    /// <c>GET /pages/new</c> — the composer form (any signed-in actor — the
    /// ADR 0040 pre-gate; the service re-checks standing at write time, C3).
    /// ADR 0040 differentiates the two page kinds:
    /// <list type="bullet">
    /// <item>A <b>GlobalAdmin</b> opens on a <b>system</b> page (the
    ///       <c>system/</c> namespace — the parent picker offers the system
    ///       pages, so they can add <c>system/about</c>, <c>system/help</c>,
    ///       or a new system sub-level) and can also switch to a <b>blog</b>
    ///       page (their own <c>blog/…</c> root).</item>
    /// <item>A plain <b>resident</b> opens on a <b>blog</b> page (their own
    ///       personal namespace — the parent picker offers only their own blog
    ///       pages, or "Top level" for their first page, which becomes their
    ///       blog root). They have <i>no</i> system-page standing — the service
    ///       denies a <c>Kind = System</c> create for them (C3).</item>
    /// </list>
    /// </summary>
    [HttpGet]
    [Route("new")]
    [Authorize]
    public async Task<IActionResult> New()
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        var roles = KumunitaPrincipal.RoleSet(User);
        bool isAdmin = roles.Contains(Roles.GlobalAdmin);

        // ADR 0039 §3.4 (amended 2026-09-17 — now consistent with posts, ADR
        // 0036): the composer's default is **non-public, community-visible** —
        // signed-in residents can see it, unauthenticated visitors cannot.
        // The audience is Community=true + empty grants (the posts default).
        var model = new PageComposeViewModel
        {
            IsPublic = false,
            Audience = new AudienceEditorModel { Mode = "Any", Grants = "[]" },
            // ADR 0040 — the kind is role-derived: an admin opens on a system
            // page (the platform namespace); a resident opens on a blog page
            // (their personal namespace). A resident CANNOT author a system
            // page (the service denies it, C3), so a non-admin is pinned to
            // "User".
            Kind = isAdmin ? "System" : "User",
            IsAdmin = isAdmin,
        };
        await SeedComposeOptionsAsync(model, actorId).ConfigureAwait(false);
        // ADR 0041 — the default scope is "All residents" (every signed-in
        // resident sees the page, no community required — the announcement
        // lane's flat scope analog). The view's scope dropdown lets the author
        // narrow to a specific community or open the "Individual access" editor.
        model.Scope = PageComposeViewModel.ScopeAllResidents;
        return View(model);
    }

    /// <summary>
    /// <c>POST /pages/new</c> — create a page (the <c>PG</c> lane's write
    /// entry). Sets <see cref="Page.ImageIds"/> / <see cref="Page.AttachmentIds"/>
    /// server-side from the body <b>before</b> calling
    /// <see cref="IPageService.CreateAsync"/> (the U03 invariant — Core never
    /// parses the body). A <see cref="UnauthorizedAccessException"/> (standing
    /// re-check) is surfaced as <b>403</b>; a root-slug collision
    /// (<see cref="InvalidOperationException"/>) and a bad form are re-rendered
    /// with the error.
    /// </summary>
    [HttpPost]
    [Route("new")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> New(PageComposeViewModel model)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            ModelState.AddModelError(string.Empty, "You must be signed in to create a page.");
            await SeedComposeOptionsAsync(model, string.Empty).ConfigureAwait(false);
            return View(model);
        }

        if (!model.IsValid)
        {
            await SeedComposeOptionsAsync(model, actorId).ConfigureAwait(false);
            return View(model);
        }

        // ADR 0041 — resolve the Scope dropdown to the (Audience, ComponentId)
        // pair the service stores (the single write-side mapping).
        var (audience, componentId) = ResolveAudience(model);

        var page = new Page
        {
            // ADR 0040 — the page kind (System = a platform page under the
            // `system/` root, GlobalAdmin-only; User = a blog page under the
            // actor's own `blog/…` root). Parsed from the form's Kind string;
            // the service re-checks standing for System pages (a resident's
            // System create is a 403, C3 — the view hides the toggle from
            // them, so this is a belt-and-braces gate).
            Kind = ParseKind(model.Kind),
            Title = model.Title!.Trim(),
            Body = model.Body ?? string.Empty,
            // The slug is the title, slugified (the derived path's leaf; the
            // composer has no explicit slug field — a hand-maintained second
            // naming surface the lane's closed set does not call for).
            Slug = Slugify(model.Title!.Trim()),
            ParentId = string.IsNullOrWhiteSpace(model.ParentId) ? null : model.ParentId,
            // IsPublic (the composer's toggle, off by default — ADR 0039 §3.4
            // amended 2026-09-17) ⇒ null audience + null component
            // (world-readable, the public *capability*); a non-public page's
            // scope is resolved from the Scope dropdown (ADR 0041) — All
            // residents, a specific community, or the "Individual access"
            // editor's explicit grants.
            Audience = audience,
            ComponentId = componentId,
            MountPoint = string.IsNullOrWhiteSpace(model.MountPoint) ? null : model.MountPoint,
            LanguageCode = model.LanguageCode ?? string.Empty,
            ImageIds = ContentImageIds.ExtractContentImageIds(model.Body),
            AttachmentIds = AttachmentIds.ExtractAttachmentIds(model.Body),
        };

        await using var session = store.LightweightSession();
        try
        {
            await pages.CreateAsync(page, actorId, KumunitaPrincipal.RoleSet(User), session).ConfigureAwait(false);
            TempData["info"] = "Page created.";
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            // Standing re-check failed (C3) — 403, the page-specific split
            // (NOT folded into a 404).
            return new ForbidResult();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await SeedComposeOptionsAsync(model, actorId).ConfigureAwait(false);
            return View(model);
        }
    }

    // ── GET/POST /pages/{id}/edit — the edit lane ────────────────────────────

    /// <summary>
    /// <c>GET /pages/{id}/edit</c> — the edit form, round-tripped from the
    /// stored page (author / GlobalAdmin / community <c>Moderator</c> — the
    /// standing pre-gate; the service re-checks at write time, C3). The
    /// audience round-trips through <see cref="AudienceEditorModel
    /// .FromAudience"/> (the ADR 0036 single source);
    /// <see cref="PageComposeViewModel.IsPublic"/> is <c>page.Audience is
    /// null</c>.
    /// </summary>
    [HttpGet]
    [Route("{id:guid}/edit")]
    [Authorize]
    public async Task<IActionResult> Edit(string id)
    {
        Page page;
        try
        {
            await using var session = store.QuerySession();
            page = (await session.LoadAsync<Page>(id).ConfigureAwait(false))!;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        if (page is null || page.IsDeleted)
            return NotFound();

        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        // ADR 0040 — HasEditStanding is kind-aware (a system page is
        // GlobalAdmin-only; a blog page is author ∪ GlobalAdmin). The service
        // re-checks standing at write time (C3).
        if (!HasEditStanding(page, actorId, KumunitaPrincipal.RoleSet(User)))
            return new ForbidResult();

        var model = new PageComposeViewModel
        {
            PageId = page.Id,
            // ADR 0040 — the page's kind round-trips from the stored page (a
            // blog page stays a blog page, a system page stays a system page —
            // the kind is NOT a form field on the edit lane; it is the page's
            // standing namespace). Drives the parent-picker differentiation.
            Kind = page.Kind == PageKind.User ? "User" : "System",
            Title = page.Title,
            Body = page.Body,
            ParentId = page.ParentId,
            IsPublic = page.Audience is null,
            Audience = AudienceEditorModel.FromAudience(page.Audience),
            // ADR 0041 — the Scope round-trips from the stored page's
            // (Audience, ComponentId): null = public; AllResidents flag = All
            // residents; Community flag + ComponentId = that community id;
            // anything else (explicit grants) = Individual access.
            Scope = DeriveScope(page),
            MountPoint = page.MountPoint,
            LanguageCode = page.LanguageCode,
            IsAdmin = KumunitaPrincipal.RoleSet(User).Contains(Roles.GlobalAdmin),
        };
        await SeedComposeOptionsAsync(model, actorId).ConfigureAwait(false);
        return View(model);
    }

    /// <summary>
    /// <c>POST /pages/{id}/edit</c> — update a page (the ADR 0039 §3.8 edit
    /// lane). Round-trips through <see cref="AudienceEditorModel
    /// .BuildAudience"/>; sets <see cref="Page.ImageIds"/> /
    /// <see cref="Page.AttachmentIds"/> server-side before calling
    /// <see cref="IPageService.UpdateAsync"/>. <b>403</b> on a standing
    /// re-check (<see cref="UnauthorizedAccessException"/>), <b>404</b> on
    /// absent (<see cref="KeyNotFoundException"/>).
    /// </summary>
    [HttpPost]
    [Route("{id:guid}/edit")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Edit(string id, PageComposeViewModel model)
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        if (string.IsNullOrEmpty(actorId))
        {
            ModelState.AddModelError(string.Empty, "You must be signed in to edit a page.");
            await SeedComposeOptionsAsync(model, string.Empty).ConfigureAwait(false);
            return View(model);
        }

        if (!model.IsValid)
        {
            await SeedComposeOptionsAsync(model, actorId).ConfigureAwait(false);
            return View(model);
        }

        Page existing;
        try
        {
            await using var session = store.LightweightSession();
            existing = (await session.LoadAsync<Page>(id).ConfigureAwait(false))!;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        if (existing is null || existing.IsDeleted)
            return NotFound();

        if (!HasEditStanding(existing, actorId, KumunitaPrincipal.RoleSet(User)))
            return new ForbidResult();

        // ADR 0041 — the same scope → (Audience, ComponentId) resolution as
        // New POST (the ResolveAudience helper is the single source).
        var (audience, componentId) = ResolveAudience(model);

        var updated = new Page
        {
            Id = existing.Id,
            // ParentId + Slug + IsDraft are the edit lane's invariants — NOT
            // re-posted (reparenting + re-slug is the move lane; publish is
            // its own lane).
            ParentId = existing.ParentId,
            Slug = existing.Slug,
            IsDraft = existing.IsDraft,
            Title = model.Title!.Trim(),
            Body = model.Body ?? string.Empty,
            Audience = audience,
            ComponentId = componentId,
            MountPoint = string.IsNullOrWhiteSpace(model.MountPoint) ? null : model.MountPoint,
            LanguageCode = model.LanguageCode ?? string.Empty,
            ImageIds = ContentImageIds.ExtractContentImageIds(model.Body),
            AttachmentIds = AttachmentIds.ExtractAttachmentIds(model.Body),
        };

        await using var session2 = store.LightweightSession();
        try
        {
            await pages.UpdateAsync(updated, actorId, KumunitaPrincipal.RoleSet(User), session2).ConfigureAwait(false);
            TempData["info"] = "Page updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── POST /pages/{id}/publish — the author-only draft→published flip ──────

    /// <summary>
    /// <c>POST /pages/{id}/publish</c> — flip a page <c>IsDraft</c> →
    /// published (ADR 0037: <b>author-only</b>; the service re-pins
    /// author-OR — a GlobalAdmin who is not the author is still denied, the
    /// draft is its author's). A <see cref="UnauthorizedAccessException"/> is
    /// surfaced as <b>403</b>, a <see cref="KeyNotFoundException"/> as
    /// <b>404</b> (the page-specific split — not collapsed).
    /// </summary>
    [HttpPost]
    [Route("{id:guid}/publish")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Publish(string id)
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        await using var session = store.LightweightSession();
        try
        {
            var page = await pages.PublishAsync(id, actorId, session).ConfigureAwait(false);
            TempData["info"] = "Page published.";
            var byId = (await pages.GetTreeAsync().ConfigureAwait(false)).ToDictionary(p => p.Id, StringComparer.Ordinal);
            return RedirectToAction(nameof(Show), new { path = PagePaths.Href(byId, page) });
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── POST /pages/{id}/delete — the admin/Moderator soft-delete lane ───────

    /// <summary>
    /// <c>POST /pages/{id}/delete</c> — soft-delete a page (a
    /// <see cref="Page.IsDeleted"/> flip, the ADR 0039 §3.6 lane). ADR 0040
    /// makes the standing kind-aware: a <b>system</b> page is
    /// <b>GlobalAdmin only</b>; a <b>blog</b> page is <b>author ∪
    /// GlobalAdmin</b> (the resident may delete their own blog page — the
    /// old "author cannot delete their own page" pin is superseded by the
    /// ADR 0040 matrix). <b>403</b> on a standing re-check, <b>404</b> on
    /// absent. The service is the authority (C3); the <c>[Authorize]</c>
    /// pre-gate is relaxed to any signed-in actor so a resident's own-blog
    /// delete is reachable.
    /// </summary>
    [HttpPost]
    [Route("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Delete(string id)
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        await using var session = store.LightweightSession();
        try
        {
            await pages.DeleteAsync(id, actorId, KumunitaPrincipal.RoleSet(User), session).ConfigureAwait(false);
            TempData["info"] = "Page deleted.";
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    // ── POST /pages/{id}/move — the admin/Moderator reparent lane ────────────

    /// <summary>
    /// <c>POST /pages/{id}/move</c> — reparent a page (a <see cref="Page
    /// .ParentId"/> change) and/or change its slug (a path change), the ADR
    /// 0039 §3.3 lane. ADR 0040 makes the standing kind-aware: a
    /// <b>system</b> page is <b>GlobalAdmin only</b>; a <b>blog</b> page is
    /// <b>author ∪ GlobalAdmin</b> (the resident may reparent their own blog
    /// page within their own blog — the service's ADR 0040 namespace guard
    /// blocks a cross-namespace move). The service's cycle-guard (a page
    /// cannot move under itself / a descendant) and depth-cap reject a bad
    /// move (<see cref="InvalidOperationException"/> → re-rendered) or a
    /// standing re-check (<see cref="UnauthorizedAccessException"/> →
    /// <b>403</b>). The service is the authority (C3); the <c>[Authorize]</c>
    /// pre-gate is relaxed to any signed-in actor so a resident's own-blog
    /// move is reachable.
    /// </summary>
    [HttpPost]
    [Route("{id:guid}/move")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Move(string id, string? newParentId, string? newSlug)
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        await using var session = store.LightweightSession();
        try
        {
            await pages.MoveAsync(
                id,
                string.IsNullOrWhiteSpace(newParentId) ? null : newParentId,
                string.IsNullOrWhiteSpace(newSlug) ? null : Slugify(newSlug.Trim()),
                actorId, KumunitaPrincipal.RoleSet(User), session).ConfigureAwait(false);
            TempData["info"] = "Page moved.";
            return RedirectToAction(nameof(Index));
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
            return RedirectToAction(nameof(Edit), new { id });
        }
    }

    // ── POST /pages/{id}/translations — the user-added-translation lane ─────

    /// <summary>
    /// <c>POST /pages/{id}/translations</c> — a **user-added-translation
    /// intake** (ADR 0029; the ADR 0022 post-translation lane carried onto
    /// <see cref="Page"/>, U06). Standing re-checked **server-side** by
    /// <see cref="IPageService.AddTranslationAsync"/> (a GlobalAdmin or a
    /// Translator on any page, plus a community Moderator of a page scoped to
    /// a community they moderate — a flat/public page has no such moderator
    /// lane; the ADR 0029 matrix): the <c>[Authorize]</c> gate is a
    /// convenience pre-gate, the service is the authority (C3). A
    /// <see cref="KeyNotFoundException"/> (the page is absent) is a
    /// <b>404</b>; an <see cref="UnauthorizedAccessException"/> (the actor has
    /// no standing) is a <b>403</b> (the page-specific split, ADR 0039 §3.8).
    /// On success it redirects back to <see cref="Show"/>, which re-renders the
    /// now-present chip via the existing ADR 0027 chip-swap markup (no display
    /// rework).
    /// </summary>
    [HttpPost("{id:guid}/translations")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "GlobalAdmin,Moderator,Translator")]
    public async Task<IActionResult> AddTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? title,
        [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        // Re-run the Read gate (absent → 404; denied/draft → 403) — the
        // page-specific split, the same shape Show uses. The route is keyed by
        // the page id (the {id:guid} shape — the Edit/Publish/Delete/Move
        // lanes), so the page is loaded by id from the store (the Edit GET
        // lane's pattern), not round-tripped through GetByPathAsync. These
        // gates run BEFORE the form validation so a bad form on an absent /
        // denied page is a 404/403, not a redirect.
        Page page;
        try
        {
            await using var readSession = store.QuerySession();
            page = (await readSession.LoadAsync<Page>(id).ConfigureAwait(false))!;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        if (page is null || page.IsDeleted)
            return NotFound();

        if (page.IsDraft && !string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
            return new ForbidResult();

        var decision = await authz.CanAsync(
            actorId, Authorization.AccessAction.Read, new PageToAuditableResource(page)).ConfigureAwait(false);
        if (!decision.Allowed)
            return new ForbidResult();

        // The page is read-authorized — now the form validation (a shape
        // error redirects back to the post view with the error banner).
        var path = await DerivePathAsync(page).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Show), new { path });
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return RedirectToAction(nameof(Show), new { path });
        }

        await using var session = store.LightweightSession();
        try
        {
            await pages.AddTranslationAsync(
                id,
                languageCode,
                string.IsNullOrWhiteSpace(title) ? null : title,
                body,
                actorId,
                KumunitaPrincipal.RoleSet(User),
                session).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode).ConfigureAwait(false);
        TempData["info"] = $"Translation added ({name}).";
        return RedirectToAction(nameof(Show), new { path });
    }

    // ── ADR 0048 — edit + delete lanes for page translations ───────────────
    // ADR 0029 (carried over) was add-only; ADR 0048 lifts the "add-only" pin
    // on the same standing matrix (GlobalAdmin ∪ Translator). Failure shapes
    // mirror <see cref="AddTranslation"/> (denied → 403; missing → 404).

    /// <summary>
    /// <c>POST /pages/{id}/translations/update</c> — **updates** the existing
    /// user-added translation of the page (ADR 0048). Standing is the same as
    /// <see cref="AddTranslation"/> (a GlobalAdmin or a Translator, server-side
    /// authority via <see cref="IPageService.UpdateTranslationAsync"/>); the
    /// <c>[Authorize(Roles)]</c> gate is the convenience pre-gate (C3).
    /// Failure shapes mirror <see cref="AddTranslation"/> (denied → 403;
    /// missing page or row → 404).
    /// </summary>
    [HttpPost("{id:guid}/translations/update")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "GlobalAdmin,Moderator,Translator")]
    public async Task<IActionResult> UpdateTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? title,
        [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        Page page;
        try
        {
            await using var readSession = store.QuerySession();
            page = (await readSession.LoadAsync<Page>(id).ConfigureAwait(false))!;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        if (page is null || page.IsDeleted)
            return NotFound();

        if (page.IsDraft && !string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
            return new ForbidResult();

        var decision = await authz.CanAsync(
            actorId, Authorization.AccessAction.Read, new PageToAuditableResource(page)).ConfigureAwait(false);
        if (!decision.Allowed)
            return new ForbidResult();

        var path = await DerivePathAsync(page).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Show), new { path });
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return RedirectToAction(nameof(Show), new { path });
        }

        await using var session = store.LightweightSession();
        try
        {
            await pages.UpdateTranslationAsync(
                id,
                languageCode,
                string.IsNullOrWhiteSpace(title) ? null : title,
                body,
                actorId,
                KumunitaPrincipal.RoleSet(User),
                session).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode).ConfigureAwait(false);
        TempData["info"] = $"Translation updated ({name}).";
        return RedirectToAction(nameof(Show), new { path });
    }

    /// <summary>
    /// <c>POST /pages/{id}/translations/remove</c> — **removes** the existing
    /// user-added translation of the page (ADR 0048). Standing is the same as
    /// <see cref="AddTranslation"/>; the service is the authority (C3).
    /// Failure shapes mirror <see cref="AddTranslation"/> (denied → 403;
    /// missing page or row → 404).
    /// </summary>
    [HttpPost("{id:guid}/translations/remove")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "GlobalAdmin,Moderator,Translator")]
    public async Task<IActionResult> RemoveTranslation(
        [FromRoute] string id, [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        Page page;
        try
        {
            await using var readSession = store.QuerySession();
            page = (await readSession.LoadAsync<Page>(id).ConfigureAwait(false))!;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        if (page is null || page.IsDeleted)
            return NotFound();

        if (page.IsDraft && !string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
            return new ForbidResult();

        var decision = await authz.CanAsync(
            actorId, Authorization.AccessAction.Read, new PageToAuditableResource(page)).ConfigureAwait(false);
        if (!decision.Allowed)
            return new ForbidResult();

        var path = await DerivePathAsync(page).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return RedirectToAction(nameof(Show), new { path });
        }

        await using var session = store.LightweightSession();
        try
        {
            await pages.RemoveTranslationAsync(
                id, languageCode, actorId, KumunitaPrincipal.RoleSet(User), session)
                .ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode).ConfigureAwait(false);
        TempData["info"] = $"Translation removed ({name}).";
        return RedirectToAction(nameof(Show), new { path });
    }

    /// <summary>
    /// The <see cref="Page"/>'s post-view route value (ADR 0039 §3.3) for the
    /// <c>Show</c> redirect — the <see cref="PagePaths.Href"/> the tree browse
    /// + the existing <see cref="Publish"/> redirect use (the single
    /// <c>/pages/…</c> value the route consumes). Loads the live tree to
    /// resolve the ancestor-slug chain (the path is never stored, ADR 0039
    /// §3.3).
    /// </summary>
    private async Task<string> DerivePathAsync(Page page)
    {
        var tree = await pages.GetTreeAsync().ConfigureAwait(false);
        var byId = tree.ToDictionary(p => p.Id, StringComparer.Ordinal);
        return PagePaths.Href(byId, page);
    }

    // ── shared seeding + helpers ─────────────────────────────────────────────

    /// <summary>
    /// ADR 0041 — derives the view model's <see
    /// cref="PageComposeViewModel.Scope"/> from a stored page's
    /// <c>(Audience, ComponentId)</c> pair (the inverse of the write-side
    /// mapping): <c>null</c> audience ⇒ <c>IsPublic</c> (no scope, the view
    /// gates on <c>IsPublic</c>); <c>Audience.AllResidents</c> ⇒
    /// <see cref="PageComposeViewModel.ScopeAllResidents"/>;
    /// <c>Audience.Community</c> + a non-empty <c>ComponentId</c> ⇒ that
    /// community id; anything else (explicit user/group grants) ⇒
    /// <see cref="PageComposeViewModel.ScopeIndividual"/> (reveal the
    /// editor).
    /// </summary>
    private static string? DeriveScope(Page page)
    {
        if (page.Audience is null) return null;
        if (page.Audience.AllResidents) return PageComposeViewModel.ScopeAllResidents;
        if (page.Audience.Community && !string.IsNullOrWhiteSpace(page.ComponentId))
            return page.ComponentId;
        return PageComposeViewModel.ScopeIndividual;
    }

    /// <summary>
    /// ADR 0041 — resolves the composer's <see
    /// cref="PageComposeViewModel.Scope"/> dropdown to the
    /// <c>(Audience, ComponentId)</c> pair the service stores (the write-side
    /// inverse of <see cref="DeriveScope"/>). The 4 cases:
    /// <list type="bullet">
    /// <item><c>IsPublic = true</c> ⇒ <c>(null, null)</c> — world-readable,
    /// unauthenticated included.</item>
    /// <item><c>Scope = "AllResidents"</c> ⇒
    /// <c>(Audience { AllResidents = true }, null)</c> — the new frozen
    /// <c>Decide()</c> resident branch (any signed-in reader, no community
    /// required).</item>
    /// <item><c>Scope = a component id</c> ⇒
    /// <c>(Audience { Community = true }, that id)</c> — the existing
    /// community branch (readers in that community).</item>
    /// <item><c>Scope = "Individual"</c> ⇒
    /// <c>(model.Audience.BuildAudience(), null)</c> — the editor's explicit
    /// user/group grants.</item>
    /// </list>
    /// </summary>
    private static (Kumunita.Core.Authorization.Audience? Audience, string? ComponentId) ResolveAudience(PageComposeViewModel model)
    {
        if (model.IsPublic)
            return (null, null);
        if (string.Equals(model.Scope, PageComposeViewModel.ScopeAllResidents, StringComparison.Ordinal))
            return (new Kumunita.Core.Authorization.Audience(Authorization.AudienceMode.Any, []) { AllResidents = true }, null);
        if (string.Equals(model.Scope, PageComposeViewModel.ScopeIndividual, StringComparison.Ordinal))
            return (model.Audience.BuildAudience(), null);
        return (new Kumunita.Core.Authorization.Audience(Authorization.AudienceMode.Any, []) { Community = true }, model.Scope);
    }

    /// <summary>
    /// Resolves a language code to its native name for a confirmation banner
    /// (the <see cref="Kumunita.Web.Controllers
    /// .AnnouncementController.SeedLanguageName"/> pattern). A "a read, not a
    /// decision" catalog lookup; falls back to the raw code when the language
    /// is not in the catalog (a never-blank shape).
    /// </summary>
    private async Task<string> SeedLanguageName(string code)
    {
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        return catalog.FirstOrDefault(l => l.Id == code)?.NativeName ?? code;
    }

    /// <summary>
    /// Seeds the composer / edit form's picker options (the
    /// <see cref="PageComposeViewModel.Languages"/> catalog, the
    /// <see cref="PageComposeViewModel.ParentPages"/> parent picker, the
    /// <see cref="PageComposeViewModel.Components"/> community picker, and the
    /// <c>_GrantPickers</c> user / group options the audience editor reads) —
    /// called on every composer / edit render (including a failed
    /// <c>POST</c>'s re-render). <paramref name="actorId"/> drives the ADR
    /// 0040 parent-picker differentiation (a blog page's picker is scoped to
    /// the actor's own blog; an empty id — a signed-out re-render — scopes it
    /// to nothing).
    /// </summary>
    private async Task SeedComposeOptionsAsync(PageComposeViewModel model, string actorId)
    {
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        model.Languages = catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .Select(l => (l.Id, l.NativeName))
            .ToList();

        var tree = await pages.GetTreeAsync().ConfigureAwait(false);
        var byId = tree.ToDictionary(p => p.Id, StringComparer.Ordinal);

        // ADR 0040 — the parent picker differentiates by the page kind being
        // composed (the "differentiate parent-page options between system and
        // user pages" requirement):
        //   • a <b>System</b> page (admin-only) may nest only under another
        //     System page — offer the <c>system/</c> namespace (the `system`
        //     root + its sub-pages) so an admin adds <c>system/about</c>,
        //     <c>system/help</c>, or a new system sub-level;
        //   • a <b>User</b> (blog) page may nest only under the actor's OWN
        //     blog pages — offer their own blog root + sub-pages (or the form's
        //     "Top level" option for their first page, which becomes their blog
        //     root). The ownership guard in the service enforces this (C3); the
        //     picker is the UX that makes a cross-namespace or cross-resident
        //     parent simply unselectable.
        // A "Top level" (no parent) option is always available in the form — it
        // creates a root page (a blog root for a User page, a top-level page for
        // a System page).
        var kind = ParseKind(model.Kind);
        var candidates = kind == PageKind.System
            ? tree.Where(p => p.Kind == PageKind.System)
            : tree.Where(p => p.Kind == PageKind.User
                && string.Equals(p.AuthorId, actorId, StringComparison.Ordinal));
        model.ParentPages = candidates
            .Select(p => (p.Id, PagePaths.Href(byId, p)))
            .ToList();

        var components = await userInfo.GetComponentsAsync(true).ConfigureAwait(false);
        model.Components = components.Select(c => (c.Id, c.Name)).ToList();

        // The audience editor's user / group picker options (the _GrantPickers
        // partial reads these from ViewData) — the reusable M2 grant-pick shape.
        var selfId = string.IsNullOrWhiteSpace(actorId) ? null : actorId;
        var profiles = (await userInfo.GetProfilesAsync(true).ConfigureAwait(false)).ToList();
        var users = profiles
            .Where(p => !p.Blocked && p.SubjectId != selfId)
            .Select(p => new GrantOption { Id = p.SubjectId, Label = p.DisplayName, Kind = "User" })
            .ToList();
        var groups = (await userInfo.GetPublicGroupsAsync().ConfigureAwait(false))
            .Select(g => new GrantOption { Id = g.Id, Label = g.Name, Kind = "Group" })
            .ToList();
        ViewData["Audience_Users"] = users;
        ViewData["Audience_Groups"] = groups;
    }

    /// <summary>
    /// The edit lane's standing (ADR 0040, amending ADR 0039 §3.7) the
    /// controller pre-gates; the service re-checks the same standing
    /// server-side — C3. Kind-aware, mirroring <see cref="PageService
    /// .CheckEditStanding"/> exactly:
    /// <list type="bullet">
    /// <item>A <b>System</b> page is <b>GlobalAdmin only</b> — the author
    ///       branch (a system page may have a GlobalAdmin author, but standing
    ///       is role-based, not ownership-based) and the community-Moderator
    ///       lane are both disabled (a platform page has no community to
    ///       moderate).</item>
    /// <item>A <b>User</b> (blog) page is <b>author ∪ GlobalAdmin</b> — the
    ///       community-Moderator lane is disabled (a resident's personal blog
    ///       is not community content).</item>
    /// </list>
    /// The <see cref="Page.AuthorId"/> is the standing key for a blog page
    /// (a page is not a profile — there is no
    /// <see cref="Kumunita.Core.UserInfo.Profile.OwnedBy"/> analog).
    /// </summary>
    private static bool HasEditStanding(Page page, string actorId, IReadOnlySet<string> roles)
    {
        if (page.Kind == PageKind.System)
            return roles.Contains(Roles.GlobalAdmin);

        // PageKind.User (a blog page): the author (the resident) ∪ GlobalAdmin
        // (platform override).
        if (string.Equals(page.AuthorId, actorId, StringComparison.Ordinal)) return true;
        return roles.Contains(Roles.GlobalAdmin);
    }

    /// <summary>
    /// Maps the form's <see cref="PageComposeViewModel.Kind"/> string
    /// (<c>"System"</c>/<c>"User"</c>) to the <see cref="PageKind"/> enum.
    /// A blank or unrecognized value defaults to <see cref="PageKind.System"/>
    /// (the ADR 0039 shape — a plain <c>/pages/new</c> is a system page; the
    /// blog lane posts <c>"User"</c> explicitly).
    /// </summary>
    private static PageKind ParseKind(string? kind)
        => string.Equals(kind, "User", StringComparison.OrdinalIgnoreCase)
            ? PageKind.User
            : PageKind.System;

    /// <summary>
    /// A title → slug derivation (a display-label → path-segment mapping; the
    /// lane's derived path is built from the <c>(ParentId, Slug)</c> chain).
    /// Lowercase, alnum + hyphens, trimmed; a blank result (a non-ASCII-only
    /// title) falls back to <c>"page"</c> so the path is never a blank leaf.
    /// </summary>
    private static string Slugify(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "page";
        var builder = new System.Text.StringBuilder(title.Length);
        foreach (var ch in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else if (ch is ' ' or '-' or '_' or '.' or '/')
                builder.Append('-');
        }
        var slug = builder.ToString().Trim('-');
        // Collapse repeated hyphens.
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return string.IsNullOrEmpty(slug) ? "page" : slug;
    }
}
