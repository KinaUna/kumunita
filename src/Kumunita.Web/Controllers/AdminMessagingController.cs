using Kumunita.Core.Messaging;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/messaging</c> surface (M9, ADR 0105) — the
/// GlobalAdmin's thin control plane over whether signed-in residents may
/// **message each other directly** at all. Mirrors
/// <see cref="AdminAnnouncementCommentsController"/> (ADR 0101) verbatim in
/// shape: <see cref="Roles.GlobalAdmin"/>-gated, a thin wrapper over the
/// <b>two</b> matching <see cref="IMessagingService"/> toggle seams
/// (<see cref="IMessagingService.IsMessagingEnabledAsync"/> /
/// <see cref="IMessagingService.SetMessagingEnabledAsync"/>), and the
/// audit row is the **service's** (exactly one <c>AccessAudit</c>,
/// <c>Via = Admin</c>, <c>TargetKind</c> <c>"messaging.toggle"</c> — the
/// singleton-toggle shape, the same as <c>announcementcomments.set-enabled</c>
/// and the ADR 0101 family).
/// <para>
/// <b>The gate.</b> Messaging is a privacy-sensitive opt-in (D2 — the
/// deliberate inverse of ADR 0101's <c>true</c> floor): a fresh instance's
/// <see cref="Kumunita.Core.Localization.LocaleSettings.MessagingEnabled"/>
/// row is <b>missing and reads as OFF</b>; an admin opens it to let
/// residents exchange direct messages. While off, <b>no surface renders and
/// every service seam refuses</b> (C-M9·2) — the toggle is enforced in the
/// service, never only in the view. A non-participant (a GlobalAdmin
/// included) never gets a <c>BreakGlass</c> read of any conversation (D3) —
/// the D3 wall is *stronger* than this surface.
/// </para>
/// </summary>
[Route("admin/messaging")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminMessagingController(
    IMessagingService messaging) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/messaging</c> — the on / off toggle. Seeds the form
    /// with the current gate
    /// (<see cref="IMessagingService.IsMessagingEnabledAsync"/>, the
    /// <c>false</c> floor — a missing settings row reads as OFF, D2).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new MessagingAdminViewModel
        {
            Enabled = await messaging.IsMessagingEnabledAsync()
        };

        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/messaging</c> — sets the gate. Delegates to
    /// <see cref="IMessagingService.SetMessagingEnabledAsync"/> (the single
    /// audited write lane — exactly one <c>AccessAudit</c> row,
    /// <c>Via = Admin</c>, <c>"messaging.toggle"</c>; the controller adds
    /// none — C-M9·3). Success → a surfaced <c>info</c> + redirect (the
    /// change is live on the very next
    /// <see cref="IMessagingService.IsMessagingEnabledAsync"/> / render —
    /// data, not config; the resident nav entry's gate flips immediately).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(bool enabled)
    {
        var actor = ActorId(User) ?? string.Empty;
        await messaging.SetMessagingEnabledAsync(enabled, actor);
        TempData["info"] = enabled
            ? "Messaging is now open — signed-in residents can open direct conversations."
            : "Messaging is now closed — the nav entry is hidden and every conversation seam refuses.";
        return RedirectToAction(nameof(Index));
    }

    // ── View model (public nested type so the Razor view can bind to it) ──

    public sealed class MessagingAdminViewModel
    {
        public bool Enabled { get; init; }
    }
}
