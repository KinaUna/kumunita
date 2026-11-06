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
/// The <c>/pages/{id}/translations</c> translation lane — the
/// three user-added-translation actions (ADR 0015 UI-view
/// localization, ADR 0026 group-community name/description
/// translations). A partial class of
/// <see cref="PageController"/> extracted so the main file stays
/// under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class PageController
{
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
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return RedirectToAction(nameof(Show), new { path });
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_needs_text", "A translation needs some text.");
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
        TempData["info"] = await this.FlashAsync("flash.translation_added", "Translation added ({0}).", name);
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
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
            return RedirectToAction(nameof(Show), new { path });
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = await this.FlashAsync("flash.translation_needs_text", "A translation needs some text.");
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
        TempData["info"] = await this.FlashAsync("flash.translation_updated", "Translation updated ({0}).", name);
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
            TempData["error"] = await this.FlashAsync("flash.translation_choose_language", "Choose a language for the translation.");
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
        TempData["info"] = await this.FlashAsync("flash.translation_removed", "Translation removed ({0}).", name);
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

        var components = await profileSettings.GetComponentsAsync(true).ConfigureAwait(false);
        model.Components = components.Select(c => (c.Id, c.Name)).ToList();

        // The audience editor's user / group picker options (the _GrantPickers
        // partial reads these from ViewData) — the reusable M2 grant-pick shape.
        var selfId = string.IsNullOrWhiteSpace(actorId) ? null : actorId;
        var profiles = (await profileSettings.GetProfilesAsync(true).ConfigureAwait(false)).ToList();
        var users = profiles
            .Where(p => !p.Blocked && p.SubjectId != selfId)
            .Select(p => new GrantOption { Id = p.SubjectId, Label = p.DisplayName, Kind = "User" })
            .ToList();
        var groups = (await groupService.GetPublicGroupsAsync().ConfigureAwait(false))
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
