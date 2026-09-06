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
/// <item><c>GET /announcements</c> — the read surface, <b>open</b> to
/// unauthenticated visitors (a public-scope announcement is by definition
/// visible whether or not the visitor is signed in — the maintenance-notice
/// case). The controller's <see cref="ListVisibleAsync(bool)"/> filter is the
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

    // ── Read (GET /announcements) ──────────────────────────────────────────

    /// <summary>
    /// The read surface: the caller-visible announcements (public scope always,
    /// community scope when signed in) — latest first. No [Authorize]: visitors
    /// see the public-scope set; residents see the union.
    /// </summary>
    [HttpGet("/announcements")]
    public async Task<IActionResult> Index()
    {
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var visible = await announcements.ListVisibleAsync(isAuthenticated);

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

        var rows = visible
            .Select(a => new AnnouncementRow(a.Id, a.Scope, a.Title ?? string.Empty, a.Body, a.Created,
                                             authorNames[a.AuthorId]))
            .ToList();

        return View(new AnnouncementIndexViewModel(rows));
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
    public IActionResult New()
    {
        var roles = RoleSet(User);
        var allowed = new List<AnnouncementScope> { AnnouncementScope.Community };
        if (roles.Contains(Roles.GlobalAdmin))
            allowed.Insert(0, AnnouncementScope.Public);

        return View(new AnnouncementComposeViewModel { Scope = "Community", AllowedScopes = allowed });
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
            var roles = RoleSet(User);
            var allowed = new List<AnnouncementScope> { AnnouncementScope.Community };
            if (roles.Contains(Roles.GlobalAdmin))
                allowed.Insert(0, AnnouncementScope.Public);
            model.AllowedScopes = allowed;
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
                    Title = string.IsNullOrWhiteSpace(model.Title) ? string.Empty : model.Title.Trim(),
                    Body  = model.Body!,
                    Scope = scope,
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
            var roles = RoleSet(User);
            var allowed = new List<AnnouncementScope> { AnnouncementScope.Community };
            if (roles.Contains(Roles.GlobalAdmin))
                allowed.Insert(0, AnnouncementScope.Public);
            model.AllowedScopes = allowed;
            return View(model);
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
