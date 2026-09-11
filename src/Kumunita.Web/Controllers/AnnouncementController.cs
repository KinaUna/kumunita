using System.Collections.Generic;
using Kumunita.Core.Announcements;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/announcements</c> surface (the "platform announcements" lane —
/// distinct from the per-community <see cref="PostsController"/>'s
/// audience-restricted <see cref="Posts.Post"/> lanes):
/// <list type="bullet">
/// <item><c>GET /announcements/{id}</c> — the detail view: the full body
/// of one announcement. <b>Open</b> to unauthenticated visitors, like the
/// list; the
/// <see cref="Kumunita.Core.Announcements.IAnnouncementService.GetAsync"/>
/// visibility gate is the sole reader (public scope always, community
/// scope when signed in, a community-targeted row visible to that
/// community's moderator/members or a GlobalAdmin). A missing or not
/// visible id is a 404 (announcements are not audience-restricted
/// content, so there is no 403/audit lane — see the service's seam).</item>
/// <item><c>GET /announcements</c> — the read surface, <b>open</b> to
/// unauthenticated visitors (a public-scope announcement is by definition
/// visible whether or not the visitor is signed in — the maintenance-notice
/// case). The controller's <see cref="ListVisibleAsync"/> filter is the
/// sole visibility gate: public scope always, community scope when
/// signed in.</item>
/// <item><c>GET /announcements/new</c> + <c>POST /announcements/new</c> —
/// the write lane, <b>[Authorize(Roles = GlobalAdmin, Moderator)]</b>. A
/// GlobalAdmin may author <see cref="AnnouncementScope.Public"/> or
/// <see cref="AnnouncementScope.Community"/>; a Moderator may author
/// <see cref="AnnouncementScope.Community"/> only — the same split
/// <see cref="AnnouncementService.CreateAsync"/> re-checks server-side at
/// POST (defense-in-depth: the ASP.NET gate narrows the author, the service
/// narrows the scope, together they pin the two-way split).</item>
/// <item><c>GET /announcements/{id}/edit</c> + <c>POST
/// /announcements/{id}/edit</c> — the edit write lane,
/// <b>[Authorize(Roles = GlobalAdmin, Moderator)]</b>. A GlobalAdmin may edit
/// either scope; a Moderator may edit
/// <see cref="AnnouncementScope.Community"/> only — the same split
/// <see cref="AnnouncementService.CreateAsync"/> /
/// <see cref="AnnouncementService.UpdateAsync"/> re-check server-side at
/// POST, against the <em>edited</em> scope (defense-in-depth: a Moderator
/// could GET any seeded form regardless of the row's actual scope, so the
/// service is what guarantees the split is real on the write).</item>
/// <item><c>POST /announcements/{id}/delete</c> — <b>[Authorize(Roles =
/// GlobalAdmin)]</b> (delete the lane is GlobalAdmin-only by design; the
/// <see cref="AnnouncementService"/> delete lane is the single write surface).</item>
/// </list>
/// <para>
/// <b>ADR 0006-D:</b> a thin HTTP layer — routes + authz + shape; the
/// visibility split is the <see cref="AnnouncementService"/>'s, never
/// re-derived here. The principal's subject id + role set is minted from the
/// signed-in cookie (<see cref="KumunitaPrincipal"/>), passed to the service
/// as the <c>actorId</c> + <c>authorRoles</c> — no DB re-read for role
/// shape (the claim set is the principal, ADR 0006-B); the author's
/// <em>display name</em> (a read surface only, never an access decision) is
/// a plain <c>IUserInfoService.GetProfileAsync</c> read.
/// </para>
/// </summary>
public sealed class AnnouncementController(
    IAnnouncementService announcements,
    IUserInfoService userInfo,
    IDocumentStore store) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    private static IReadOnlySet<string> RoleSet(System.Security.Claims.ClaimsPrincipal user)
        => user?.Claims
            .Where(c => c.Type == Kumunita.Core.Identity.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet()
            ?? new HashSet<string>();

    /// <summary>The caller's role-dependent scope options (GlobalAdmin: both
    /// scopes; Moderator: community only) — a shape convenience, never the gate.</summary>
    private static IReadOnlyCollection<AnnouncementScope> RoleAllowedScopes(IReadOnlySet<string> roles)
    {
        var allowed = new List<AnnouncementScope> { AnnouncementScope.Community };
        if (roles.Contains(Roles.GlobalAdmin))
            allowed.Insert(0, AnnouncementScope.Public);
        return allowed;
    }

    /// <summary>
    /// Seeds a compose form's <c>AllowedScopes</c> and <c>TargetCommunities</c> —
    /// a GlobalAdmin may target any community, a Moderator only the ones they
    /// moderate (read from their <c>moderator:{id}</c> standing claims). A shape
    /// convenience only; the service pins the whole split server-side at POST.
    /// </summary>
    private async Task SeedComposeOptionsAsync(AnnouncementComposeViewModel model, IReadOnlySet<string> roles)
    {
        model.AllowedScopes = RoleAllowedScopes(roles);
        var components = await userInfo.GetComponentsAsync(enabledOnly: true).ConfigureAwait(false);
        model.TargetCommunities = components
            .Where(c => roles.Contains(Roles.GlobalAdmin) || roles.Contains(Roles.ModeratorComponent(c.Id)))
            .ToList();
    }

    // ── Read (GET /announcements) ─

    /// <summary>
    /// The read surface: the caller-visible announcements (public scope always,
    /// community scope when signed in) — latest first. No [Authorize]: visitors
    /// see the public-scope set; residents see the union.
    /// </summary>
    [HttpGet("/announcements")]
    public async Task<IActionResult> Index()
    {
        var subjectId = SubjectId(User);
        var roles     = RoleSet(User);
        var visible   = await announcements.ListVisibleAsync(subjectId, roles);

        var authorIds = visible.Select(a => a.AuthorId).Distinct().ToHashSet();

        // Resolve each distinct author's display name once (a *display* lookup,
        // never an access decision — the public/community split gate is the
        // service's, not the display-name's). Missing profile row: fall back
        // to the raw subject id (null-safe: no null-coalescing exception).
        var authorNames = new Dictionary<string, string>(authorIds.Count);
        foreach (var id in authorIds)
        {
            string name = id;
            var profile = await userInfo.GetProfileAsync(id);
            if (profile?.DisplayName is not null && profile.DisplayName.Length > 0)
                name = profile.DisplayName;
            authorNames[id] = name;
        }

        var components = await userInfo.GetComponentsAsync(enabledOnly: true);
        var componentNames = components.ToDictionary(c => c.Id, c => c.Name);

        var rows = visible
            .Select(a => new AnnouncementRow(a.Id, a.Scope, a.Title ?? string.Empty, a.Body, a.Created,
                                             authorNames[a.AuthorId], a.Pinned, a.CommunityId, a.CommunityId is not null && componentNames.TryGetValue(a.CommunityId, out var cn) ? cn : null))
            .ToList();

        return View(new AnnouncementIndexViewModel(rows));
    }

    // ── Detail (GET /announcements/{id}) ───────────────────────────────────

    /// <summary>
    /// The detail view: one announcement's full body (the /announcements list
    /// shows a preview link per row). No [Authorize]: the same as the list,
    /// the service's <see cref="Kumunita.Core.Announcements.IAnnouncementService.GetAsync"/>
    /// gate is the reader (public scope always, community scope when signed
    /// in, a community-targeted row visible to that community's
    /// moderator/members or a GlobalAdmin). A missing or not-visible id both
    /// return null and map to <see cref="NotFound"/> (announcements are not
    /// audience-restricted content — no 403/audit lane on this bounded
    /// context, unlike the <see cref="PostsController"/>'s post lane).
    /// </summary>
    [HttpGet("/announcements/{id}")]
    public async Task<IActionResult> Detail(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var a = await announcements.GetAsync(id, SubjectId(User), RoleSet(User));
        if (a is null)
            return NotFound();

        // The author's display name — a *display* read (GetProfileAsync),
        // never an access decision (the visibility gate already ran inside
        // the service). Missing profile row: fall back to the raw subject id.
        var profile = await userInfo.GetProfileAsync(a.AuthorId);
        string authorName = profile?.DisplayName is not null && profile.DisplayName.Length > 0
            ? profile.DisplayName
            : a.AuthorId;

        string? communityName = null;
        if (a.CommunityId is not null)
        {
            var components = await userInfo.GetComponentsAsync(enabledOnly: true);
            communityName = components.FirstOrDefault(c => c.Id == a.CommunityId)?.Name;
        }

        return View(new AnnouncementDetailViewModel(
            a.Id, a.Scope, a.Title, a.Body, a.Created, a.Modified,
            authorName, a.Pinned, communityName));
    }

    // ── Create (GET + POST /announcements/new) ─────────────────────────────

    /// <summary>
    /// <c>GET /announcements/new</c> — the write lane's shape. The scope
    /// picker options are the caller's role-dependent set (a GlobalAdmin: both
    /// scopes; a Moderator: community only) — a shape convenience, the
    /// service pins the split server-side at POST.
    /// </summary>
    [HttpGet("/announcements/new")]
    [Authorize(Roles = "GlobalAdmin,Moderator")]
    public async Task<IActionResult> New()
    {
        var model = new AnnouncementComposeViewModel { Scope = "Community" };
        await SeedComposeOptionsAsync(model, RoleSet(User));
        return View(model);
    }

    /// <summary>
    /// <c>POST /announcements/new</c> — the write lane. On success, redirects
    /// to the read page (the new announcement is visible to the visitor
    /// immediately — the split is the gate, not a re-render). On failure,
    /// re-renders <c>New</c> with the caller's <c>AllowedScopes</c> restored
    /// to their role-dependent set (a POST that 500s into a fresh GET with a
    /// different AllowedScopes would mis-seed the scope picker).
    /// </summary>
    [HttpPost("/announcements/new")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "GlobalAdmin,Moderator")]
    public async Task<IActionResult> New(AnnouncementComposeViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Body))
            ModelState.AddModelError(nameof(model.Body), "Body is required.");
        if (!Enum.TryParse<AnnouncementScope>(model.Scope, out var scope))
            ModelState.AddModelError(nameof(model.Scope), "Scope is required.");

        if (!ModelState.IsValid)
        {
            await SeedComposeOptionsAsync(model, RoleSet(User));
            return View(model);
        }

        // C3 same-transaction lane: the service's SaveChangesAsync is the single
        // write; the caller's store.LightweightSession() is the in-flight
        // transaction (mirrors the PostsController write-lane shape).
        await using var session = store.LightweightSession();
        var authorId = SubjectId(User) ?? string.Empty;
        if (string.IsNullOrEmpty(authorId))
        {
            ModelState.AddModelError(string.Empty, "Not signed in.");
            return View(model);
        }

        try
        {
            var created = await announcements.CreateAsync(
                new Announcement
                {
                    Title  = string.IsNullOrWhiteSpace(model.Title) ? string.Empty : model.Title.Trim(),
                    Body   = model.Body!,
                    Scope  = scope,
                    Pinned = model.Pinned,
                    CommunityId = string.IsNullOrWhiteSpace(model.CommunityId) ? null : model.CommunityId,
                },
                actorId:     authorId,
                authorRoles: RoleSet(User),
                session);
            TempData["info"] = "Announcement created.";
            return RedirectToAction("Index");
        }
        catch (UnauthorizedAccessException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await SeedComposeOptionsAsync(model, RoleSet(User));
            return View(model);
        }
    }

    // ── Edit (GET + POST /announcements/{id}/edit) ─────────────────────────

    /// <summary>
    /// <c>GET /announcements/{id}/edit</c> — the edit write lane's shape,
    /// seeded from the existing announcement (Title/Body/Scope preserved).
    /// The scope picker options are the caller's role-dependent set (a
    /// GlobalAdmin: both scopes; a Moderator: community only); a Moderator
    /// viewing a public-scope announcement gets a 403 here already (the GET
    /// is a shape convenience, the service's <see cref="IAnnouncementService.UpdateAsync"/>
    /// split re-check is the real gate — but a form a user can't submit
    /// shouldn't be rendered in the first place).
    /// </summary>
    [HttpGet("/announcements/{id}/edit")]
    [Authorize(Roles = "GlobalAdmin,Moderator")]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        await using var session = store.QuerySession();
        var existing = await session.LoadAsync<Announcement>(id);
        if (existing is null)
            return NotFound();

        var roles = RoleSet(User);
        if (existing.Scope == AnnouncementScope.Public && !roles.Contains(Roles.GlobalAdmin))
        {
            TempData["error"] = "Only a GlobalAdmin may edit a public-scope announcement.";
            return new ForbidResult();
        }

        var model = new AnnouncementComposeViewModel
        {
            Id = id,
            Title = existing.Title,
            Body  = existing.Body,
            Scope = existing.Scope.ToString(),
            Pinned = existing.Pinned,
            CommunityId = existing.CommunityId,
        };
        await SeedComposeOptionsAsync(model, roles);
        return View(model);
    }

    /// <summary>
    /// <c>POST /announcements/{id}/edit</c> — the edit write lane. On
    /// success, redirects to the read page (the edit is visible to the
    /// visitor immediately — the split is the gate, not a re-render). A
    /// denied scope-vs-role split (e.g. a Moderator editing a public-scope
    /// announcement) is a 403; a missing id is a 404.
    /// </summary>
    [HttpPost("/announcements/{id}/edit")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "GlobalAdmin,Moderator")]
    public async Task<IActionResult> Edit(string id, AnnouncementComposeViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();
        if (string.IsNullOrWhiteSpace(model.Body))
            ModelState.AddModelError(nameof(model.Body), "Body is required.");
        if (!Enum.TryParse<AnnouncementScope>(model.Scope, out var scope))
            ModelState.AddModelError(nameof(model.Scope), "Scope is required.");

        if (!ModelState.IsValid)
        {
            // Re-seed the picker (the invalid-POST path has no existing doc to
            // seed from — the edit target isn't loadable without a write
            // session; fall back to the role-dependent shape, the same way the
            // create lane does).
            model.Id = id;
            await SeedComposeOptionsAsync(model, RoleSet(User));
            return View(model);
        }

        await using var session = store.LightweightSession();
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            TempData["error"] = "Not signed in.";
            return new UnauthorizedResult();
        }

        try
        {
            await announcements.UpdateAsync(
                new Announcement
                {
                    Id     = id,
                    Title  = model.Title ?? string.Empty,
                    Body   = model.Body!,
                    Scope  = scope,
                    Pinned = model.Pinned,
                    CommunityId = string.IsNullOrWhiteSpace(model.CommunityId) ? null : model.CommunityId,
                },
                actorId:    actorId,
                actorRoles: RoleSet(User),
                session);
            TempData["info"] = "Announcement updated.";
            return RedirectToAction("Index");
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

    // ── Delete (POST /announcements/{id}/delete) ───────────────────────────

    /// <summary>
    /// <c>POST /announcements/{id}/delete</c> — the GlobalAdmin delete lane.
    /// A missing announcement is a 404 (the service's
    /// <see cref="KeyNotFoundException"/> maps to <see cref="NotFound"/>);
    /// on success, the caller returns to the read page (the announcement is
    /// gone for everyone — a hard delete, not a soft-hidden state).
    /// </summary>
    [HttpPost("/announcements/{id}/delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "GlobalAdmin")]
    public async Task<IActionResult> Delete(string id)
    {
        await using var session = store.LightweightSession();
        try
        {
            await announcements.DeleteAsync(id, session);
            TempData["info"] = "Announcement deleted.";
            return RedirectToAction("Index");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
