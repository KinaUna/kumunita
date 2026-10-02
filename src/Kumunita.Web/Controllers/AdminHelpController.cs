using Kumunita.Core.Pages;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/help</c> surface (ADR 0128) — the GlobalAdmin's control plane
/// over the **seeded** platform help pages. Two capabilities:
/// <list type="bullet">
/// <item>A **status list** of every seeded page, each flagged when the code
/// ships newer text than what the DB holds (<see
/// cref="IPageService.GetSeededPageStatusAsync"/> — a read-only display probe,
/// computed on visit, no schema, no boot hook).</item>
/// <item>Per-page and **Reset all** destructive actions, delegating to the
/// ADR 0058 reset lane (<see cref="IPageService.ResetToSeededAsync"/> and
/// <see cref="IPageService.ResetAllSeededPagesAsync"/>) — GlobalAdmin-only,
/// audited (<c>page.reset</c>, <c>Via = Admin</c>), re-writing the <c>en</c>
/// body and the de/fr/da <c>PageTranslation</c> rows to the seeded baseline.</item>
/// </list>
/// <para>
/// <b>Why a dedicated controller.</b> The <c>AdminController</c>'s constructor
/// is pinned by the Web test harnesses (<c>AdminControllerBlockTests</c> /
/// <c>AdminControllerMandatoryTests</c>), so new seams go on their own
/// controller — the same convention as <c>AdminSignupController</c>,
/// <c>AdminTimezoneController</c>, and the other <c>/admin/*</c> surfaces.
/// </para>
/// <para>
/// <b>The reset gate.</b> A reset is destructive (it overwrites hand-edited
/// text), so it stays opt-in and explicit — the admin sees which pages have
/// newer shipped text and chooses to apply, per page or all at once. Silent
/// auto-apply is the anti-pattern this ADR exists to reject (ADR 0042 D1).
/// </para>
/// </summary>
[Route("admin/help")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminHelpController(
    IPageService pages,
    IDocumentStore store) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/help</c> — lists every seeded page with a "newer shipped
    /// text" flag (read-only). No writes, no audit row.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var status = await pages.GetSeededPageStatusAsync();
        var model = new HelpAdminViewModel
        {
            Pages = status,
            PagesWithUpdates = status.Count(p => p.HasNewerShippedText)
        };
        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/help/reset</c> — resets a single seeded page to its
    /// seeded text (ADR 0058 lane, GlobalAdmin-only, audited <c>page.reset</c>).
    /// The caller supplies the page id; a page with no seeded baseline is a
    /// form error, never a silent no-op.
    /// </summary>
    [HttpPost("reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(string pageId)
    {
        var actor = ActorId(User) ?? string.Empty;
        var roles = KumunitaPrincipal.RoleSet(User);
        try
        {
            using var session = store.LightweightSession();
            await pages.ResetToSeededAsync(pageId, actor, roles, session);
            TempData["info"] = "Page reset to its seeded text.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>POST /admin/help/reset-all</c> — resets **every** seeded page to its
    /// seeded text (ADR 0128, the batch lane). One audited write lane per page
    /// (<c>page.reset</c>, <c>Via = Admin</c>), one <c>SaveChangesAsync</c>.
    /// Non-GlobalAdmin callers are denied (<see cref="UnauthorizedAccessException"/>);
    /// the <c>[Authorize]</c> attribute is the first line of defense, the
    /// service re-check is the second.
    /// </summary>
    [HttpPost("reset-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAll()
    {
        var actor = ActorId(User) ?? string.Empty;
        var roles = KumunitaPrincipal.RoleSet(User);
        using var session = store.LightweightSession();
        var count = await pages.ResetAllSeededPagesAsync(actor, roles, session);
        TempData["info"] = count == 1
            ? "1 page reset to its seeded text."
            : $"{count} pages reset to their seeded text.";
        return RedirectToAction(nameof(Index));
    }

    // ── View model (public nested type so the Razor view can bind to it) ──

    public sealed class HelpAdminViewModel
    {
        public IReadOnlyList<SeededPageStatus> Pages { get; init; } = Array.Empty<SeededPageStatus>();

        /// <summary>Count of seeded pages flagged with newer shipped text.</summary>
        public int PagesWithUpdates { get; init; }
    }
}
