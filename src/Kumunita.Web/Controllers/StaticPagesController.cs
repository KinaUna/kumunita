using Kumunita.Core;
using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The static-page routes (ADR 0005 A; M5 FACES) — <c>/terms</c>, <c>/help</c>
/// and <c>/about</c> over <see cref="ITranslationProvider.GetPageAsync"/>. The
/// per-page fallback (M·2) and the <c>null</c> = truly-absent floor live in the
/// provider; this controller is a **thin** route: it reads the
/// <see cref="LocaleCookie"/> (M·5 — the one Web HTTP seam), passes the
/// preferred code as a plain BCP-47 string (M·8 — Core stays HTTP-free), and
/// renders the <see cref="LocalizedPage"/>.
///
/// <c>/about</c> is wired here (ML-UI U7, D7-4 — **shipped**), not in
/// <c>HomeController.About</c>: one route, one owner. A <c>null</c> page (the
/// <c>about</c> page truly absent in any language) renders the **existing**
/// product-story view (<c>Views/Home/About</c>) rather than a 404 — a fresh
/// instance's <c>/about</c> is the product pitch; an admin can create a real
/// <c>about</c> <see cref="LocalizedPage"/> (M·4 — data, not config) and it then
/// renders.
/// </summary>
public sealed class StaticPagesController(
    ITranslationProvider provider,
    IOptions<CommunityOptions> community) : Controller
{
    private static readonly string[] Slugs = { "terms", "help", "about" };

    /// <summary>
    /// <c>GET /terms</c> — the terms-of-use static page (M5 FACES). Resolves in
    /// the resident's preferred language (cookie → instance default → <c>en</c>,
    /// M·1); a per-page fallback degrades one page at a time (M·2). A
    /// <c>null</c> result is a 404 (the page truly does not exist in any
    /// language — M·2's page floor, contrast the UI-string floor of the key
    /// itself, M·1).
    /// </summary>
    [HttpGet("/terms")]
    public Task<IActionResult> Terms() => Page("terms");

    /// <summary>
    /// <c>GET /help</c> — the help/support static page (M5 FACES). Same
    /// per-page fallback + 404 floor as <see cref="Terms"/>.
    /// </summary>
    [HttpGet("/help")]
    public Task<IActionResult> Help() => Page("help");

    /// <summary>
    /// <c>GET /about</c> — the product story (ML-UI U7, D7-4; FACES L9).
    /// Resolves an admin-created <c>about</c> <see cref="LocalizedPage"/> in the
    /// preferred language (per-page fallback, M·2). When the page is **truly
    /// absent** (null — no <c>about</c> row in any language), it renders the
    /// existing product-story view (<c>Views/Home/About</c>) instead of a 404 —
    /// a fresh instance's <c>/about</c> is the product pitch. This route
    /// replaced the former <c>HomeController.About</c> (deleted in U7).
    /// </summary>
    [HttpGet("/about")]
    public Task<IActionResult> About() => Page("about", fallBackToProductStory: true);

    private async Task<IActionResult> Page(string slug, bool fallBackToProductStory = false)
    {
        // Guard against route spoofing: only the ADR 0005 A static slugs.
        if (!Slugs.Contains(slug))
            return NotFound();

        var preferred = LocaleCookie.Read(Request);
        var page = await provider.GetPageAsync(slug, preferred);
        if (page is null)
        {
            // L9's "truly absent" branch: /about degrades to the existing
            // product-story view (a fresh instance's about page). Other slugs
            // keep the 404 floor (M·2's page floor).
            if (fallBackToProductStory)
                return View("~/Views/Home/About",
                    new Models.HomeViewModel(community.Value.Name, community.Value.SupportEmail));
            return NotFound();
        }

        ViewData["Slug"] = slug;
        return View("Page", page);
    }
}
