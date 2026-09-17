using Kumunita.Core;
using Kumunita.Core.Pages;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The static-page routes (ADR 0005 A; M5 FACES) — <c>/terms</c>, <c>/help</c>
/// and <c>/about</c>. PG U05 (ADR 0039 §3.8/§3.9) retargeted these onto the
/// new <see cref="Page"/> tree; **PG U07 (this unit) completes the absorb** —
/// the legacy fallback seam is gone, so each route reads **only** the tree
/// (<see cref="IPageService.GetByPathAsync"/> — one store, not two). A path
/// absent from the tree is a 404 (the page floor), except <c>/about</c>, which
/// degrades to the product-story view.
/// <para>
/// **<c>/about</c> is special (the U05 drift pin, kept):** <b>no <c>about</c>
/// page is seeded</b> — a fresh instance's <c>/about</c> is the full-bleed
/// product-story view (<c>Views/StaticPages/About</c>, driven by
/// <see cref="Models.HomeViewModel"/>), not a Markdown page. An admin-created
/// <c>about</c> <see cref="Page"/> (the tree) wins when present.
/// </para>
/// <para>
/// The three hard-coded routes are **kept** (backward-compatible) and read
/// from the tree. They remain un-audited public readers (the static-page
/// contract — these resolve to the seeded canonical pages, which are public,
/// <see cref="Page.Audience"/> = <c>null</c>).
/// </para>
/// </summary>
public sealed class StaticPagesController(
    IPageService pages,
    IOptions<CommunityOptions> community) : Controller
{
    private static readonly string[] Slugs = { "terms", "help", "about" };

    /// <summary>
    /// <c>GET /terms</c> — the terms-of-use static page (M5 FACES). Reads the
    /// <see cref="Page"/> tree (the absorb); a truly-absent page is a 404
    /// (the page floor).
    /// </summary>
    [HttpGet("/terms")]
    public Task<IActionResult> Terms() => Page("terms");

    /// <summary>
    /// <c>GET /help</c> — the help/support static page (M5 FACES). Same
    /// tree-read + 404 floor as <see cref="Terms"/>.
    /// </summary>
    [HttpGet("/help")]
    public Task<IActionResult> Help() => Page("help");

    /// <summary>
    /// <c>GET /about</c> — the product story (ML-UI U7, D7-4; FACES L9). The
    /// tree read is absent on a fresh instance (no <c>about</c>
    /// <see cref="Page"/> is seeded — the U05 drift pin), so this degrades to
    /// the existing product-story view (<c>Views/StaticPages/About</c>) — a
    /// fresh instance's <c>/about</c> is the product pitch. An admin-created
    /// <c>about</c> page (the tree) renders in preference. This route replaced
    /// the former <c>HomeController.About</c> (deleted in U7).
    /// </summary>
    [HttpGet("/about")]
    public Task<IActionResult> About() => Page("about", fallBackToProductStory: true);

    private async Task<IActionResult> Page(string slug, bool fallBackToProductStory = false)
    {
        // Guard against route spoofing: only the ADR 0005 A static slugs.
        if (!Slugs.Contains(slug))
            return NotFound();

        // The one store: the Page tree (the absorb, ADR 0039 §3.8). A missing
        // path is a clean "absent" (KeyNotFoundException), not an error.
        Page? page;
        try
        {
            page = await pages.GetByPathAsync(slug);
        }
        catch (KeyNotFoundException)
        {
            page = null;
        }

        if (page is not null)
        {
            ViewData["Slug"] = slug;
            // The view (Views/StaticPages/Page.cshtml) is the static-page
            // renderer (Title/Body/Updated); the body renders via the single
            // MarkdownRenderer. The (Title, Body, Updated) triple is
            // byte-identical to the retired static-page output (the seeder
            // wrote both from the same `now`, ADR 0039 §3.9).
            return View("Page", new StaticPageViewModel(
                slug,
                page.Title,
                page.Body,
                page.Modified ?? page.Created));
        }

        // Truly absent from the tree: /about degrades to the product-story
        // view (a fresh instance's about page); other slugs keep the 404
        // floor (the page floor).
        if (fallBackToProductStory)
            return View("About",
                new Models.HomeViewModel(community.Value.Name, community.Value.SupportEmail));
        return NotFound();
    }

    /// <summary>
    /// The static-page render model for <c>/terms</c> / <c>/help</c> /
    /// <c>/about</c> — the (Slug, Title, Body, Updated) shape the
    /// <c>Views/StaticPages/Page</c> view binds (Title + rendered Body +
    /// last-updated line). <see cref="Updated"/> is the page's
    /// <see cref="Page.Modified"/> (falling back to
    /// <see cref="Page.Created"/>).
    /// </summary>
    public sealed record StaticPageViewModel(
        string Slug,
        string Title,
        string Body,
        DateTimeOffset Updated);
}
