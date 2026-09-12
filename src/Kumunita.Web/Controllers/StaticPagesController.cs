using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The static-page routes (ADR 0005 A; M5 FACES) — <c>/terms</c> and
/// <c>/help</c> over <see cref="ITranslationProvider.GetPageAsync"/>. The
/// per-page fallback (M·2) and the <c>null</c> = truly-absent floor live in
/// U2's provider; this controller is a **thin** route: it reads the
/// <see cref="LocaleCookie"/> (M·5 — the one Web HTTP seam), passes the
/// preferred code as a plain BCP-47 string (M·8 — Core stays HTTP-free), and
/// renders the <see cref="LocalizedPage"/> (or a 404 when the page truly does
/// not exist in any language).
/// </summary>
public sealed class StaticPagesController(
    ITranslationProvider provider) : Controller
{
    private static readonly string[] Slugs = { "terms", "help" };

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

    private async Task<IActionResult> Page(string slug)
    {
        // Guard against route spoofing: only the ADR 0005 A static slugs.
        if (!Slugs.Contains(slug))
            return NotFound();

        var preferred = LocaleCookie.Read(Request);
        var page = await provider.GetPageAsync(slug, preferred);
        if (page is null)
            return NotFound();

        ViewData["Slug"] = slug;
        return View("Page", page);
    }
}
