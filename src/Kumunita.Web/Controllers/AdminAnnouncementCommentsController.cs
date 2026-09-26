using Kumunita.Core.Announcements;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/announcements/comments</c> surface (ADR 0101) — the
/// GlobalAdmin's thin control plane over whether signed-in residents may
/// **comment on platform announcements** at all. Mirrors
/// <see cref="AdminSignupController"/> (ADR 0050) exactly:
/// <see cref="Roles.GlobalAdmin"/>-gated (the <c>AdminController</c>
/// precedent), a thin wrapper over the <b>two</b> matching
/// <see cref="IAnnouncementService"/> seams
/// (<see cref="IAnnouncementService.AreAnnouncementCommentsEnabledAsync"/>
/// / <see cref="IAnnouncementService.SetAnnouncementCommentsEnabledAsync"/>),
/// and the audit row is the **service's** (exactly one <c>AccessAudit</c>,
/// <c>Via = Admin</c>, action <c>announcementcomments.set-enabled</c>,
/// <c>TargetKind</c> "announcementcomments" — the singleton-toggle shape, the
/// same as <c>signup.set-open</c> / <c>timezone.set-default</c> /
/// <c>dateformat.set-default</c>).
/// <para>
/// <b>Why a dedicated controller.</b> The <c>AdminController</c>'s constructor
/// is pinned by two Web-layer test harnesses
/// (<c>AdminControllerBlockTests</c> / <c>AdminControllerMandatoryTests</c>),
/// so a new dependency there would break them. A separate
/// <c>/admin/announcements/comments</c> surface (mirroring
/// <c>/admin/signup</c> / <c>/admin/timezone</c> / <c>/admin/dateformat</c>) is
/// the convention-consistent shape — the codebase already puts each
/// admin-settled instance value on its own controller, and the
/// announcement-comments gate is their exact analog (one admin-settled
/// instance value, one audited write lane).
/// </para>
/// <para>
/// <b>The gate.</b> While the platform widens beyond the development circle,
/// the discussion lane ships <c>true</c> (open) by default (the
/// <see cref="Kumunita.Core.Localization.LocaleSettings
/// .AnnouncementCommentsEnabled"/> <c>true</c> floor); an admin tightens it to
/// <c>false</c> to silence the surface. The flip affects only the
/// signed-in-resident comment lane — a visitor can never comment regardless
/// of the gate, and existing announcements / comments are never touched by it
/// (a data-level gate, not a content deletion).
/// </para>
/// </summary>
[Route("admin/announcements/comments")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminAnnouncementCommentsController(
    IAnnouncementService announcements) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/announcements/comments</c> — the open / closed toggle.
    /// Seeds the form with the current gate
    /// (<see cref="IAnnouncementService.AreAnnouncementCommentsEnabledAsync"/>,
    /// the <c>true</c> floor).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new AnnouncementCommentsAdminViewModel
        {
            Enabled = await announcements.AreAnnouncementCommentsEnabledAsync()
        };

        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/announcements/comments</c> — sets the gate. Delegates to
    /// <see cref="IAnnouncementService.SetAnnouncementCommentsEnabledAsync"/>
    /// (the single audited write lane — exactly one <c>AccessAudit</c> row,
    /// <c>Via = Admin</c>). Success → a surfaced <c>info</c> + redirect (the
    /// change is live on the very next
    /// <see cref="IAnnouncementService.AreAnnouncementCommentsEnabledAsync"/>
    /// / render — data, not config).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(bool enabled)
    {
        var actor = ActorId(User) ?? string.Empty;
        await announcements.SetAnnouncementCommentsEnabledAsync(enabled, actor);
        TempData["info"] = enabled
            ? "Commenting on announcements is now open — signed-in residents can comment."
            : "Commenting on announcements is now closed — no signed-in resident can add a comment.";
        return RedirectToAction(nameof(Index));
    }

    // ── View model (public nested type so the Razor view can bind to it) ──

    public sealed class AnnouncementCommentsAdminViewModel
    {
        public bool Enabled { get; init; } = true;
    }
}
