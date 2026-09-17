using Kumunita.Core;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The static-page routes (ADR 0005 A; M5 FACES) — <c>/terms</c>, <c>/help</c>
/// and <c>/about</c>. PG U05 (ADR 0039 §3.8/§3.9) **retargets** these onto the
/// new <see cref="Page"/> tree: each route first tries
/// <see cref="IPageService.GetByPathAsync"/> (one store — the absorb) and, when
/// the page is absent from the tree, falls back to the legacy
/// <see cref="ITranslationProvider.GetPageAsync"/> over
/// <see cref="LocalizedPage"/> (kept readable until U07 retires it — the
/// "absorb, don't yank" discipline, ADR 0039 §3.9 step 3).
/// <para>
/// **<c>/about</c> is special (the U05 drift pin):** <b>no <c>about</c> page is
/// seeded</b> — a fresh instance's <c>/about</c> is the full-bleed
/// product-story view (<c>Views/StaticPages/About</c>, driven by
/// <see cref="Models.HomeViewModel"/>), not a Markdown page. So the primary
/// tree miss falls through to the product-story view. An admin-created
/// <c>about</c> <see cref="Page"/> (or a legacy <c>about</c>
/// <see cref="LocalizedPage"/>) wins when present. This deliberately deviates
/// from the plan's "seed all three" wording in favor of the lane's
/// byte-identical exit gate (recorded in the U05 handoff notes).
/// </para>
/// <para>
/// The three hard-coded routes are **kept** (backward-compatible) but read from
/// the tree **first** — one store, not two. The primary decision path is the
/// <see cref="Page"/> doc (public by construction, <see cref="Page.Audience"/>
/// = <c>null</c>); these routes remain un-audited public readers (the legacy
/// lane's contract — a static page is public by construction).
/// </para>
/// </summary>
public sealed class StaticPagesController(
    IPageService pages,
    ITranslationProvider provider,
    IOptions<CommunityOptions> community) : Controller
{
    private static readonly string[] Slugs = { "terms", "help", "about" };

    /// <summary>
    /// <c>GET /terms</c> — the terms-of-use static page (M5 FACES). Reads the
    /// seeded <see cref="Page"/> (the absorb) first; the legacy
    /// <see cref="LocalizedPage"/> fallback (M·2 per-page language fallback) is
    /// the "absorb, don't yank" path until U07 retires it. A truly-absent page
    /// is a 404 (M·2's page floor).
    /// </summary>
    [HttpGet("/terms")]
    public Task<IActionResult> Terms() => Page("terms");

    /// <summary>
    /// <c>GET /help</c> — the help/support static page (M5 FACES). Same
    /// tree-first + legacy-fallback + 404 floor as <see cref="Terms"/>.
    /// </summary>
    [HttpGet("/help")]
    public Task<IActionResult> Help() => Page("help");

    /// <summary>
    /// <c>GET /about</c> — the product story (ML-UI U7, D7-4; FACES L9). The
    /// primary tree read is absent on a fresh instance (no <c>about</c>
    /// <see cref="Page"/> is seeded — the U05 drift pin), so this degrades to
    /// the existing product-story view (<c>Views/StaticPages/About</c>) — a
    /// fresh instance's <c>/about</c> is the product pitch. An admin-created
    /// <c>about</c> page (a <see cref="Page"/> or a legacy
    /// <see cref="LocalizedPage"/>) renders in preference. This route replaced
    /// the former <c>HomeController.About</c> (deleted in U7).
    /// </summary>
    [HttpGet("/about")]
    public Task<IActionResult> About() => Page("about", fallBackToProductStory: true);

    private async Task<IActionResult> Page(string slug, bool fallBackToProductStory = false)
    {
        // Guard against route spoofing: only the ADR 0005 A static slugs.
        if (!Slugs.Contains(slug))
            return NotFound();

        // 1. The new Page tree (the absorb — the primary decision path,
        //    ADR 0039 §3.8). A missing path is a clean "absent" (the
        //    KeyNotFoundException), not an error — fall through to the legacy
        //    store (the "absorb, don't yank" contract until U07 retires
        //    LocalizedPage).
        Page? page = null;
        try
        {
            page = await pages.GetByPathAsync(slug);
        }
        catch (KeyNotFoundException)
        {
            // absent from the tree — fall through (the legacy fallback is the
            // U07-retired seam kept alive).
        }

        if (page is not null)
        {
            ViewData["Slug"] = slug;
            // The view (Views/StaticPages/Page.cshtml) is the legacy
            // static-page renderer (@model LocalizedPage — Title/Body/Updated);
            // adapt the Page's static-page shape to it. The body renders via
            // the single MarkdownRenderer, and the (Title, Body, Updated)
            // triple is byte-identical to the legacy LocalizedPage output
            // (the seeder writes both from the same `now`, ADR 0039 §3.9). The
            // ADR 0027 chip-swap is the post view's job — these legacy routes
            // keep the static-page shape.
            return View("Page", new LocalizedPage
            {
                Slug = slug,
                LanguageCode = page.LanguageCode,
                Title = page.Title,
                Body = page.Body,
                Updated = page.Modified ?? page.Created
            });
        }

        // 2. Legacy fallback (the old LocalizedPage store — retired in U07):
        // the per-page language fallback (M·2) the resident's preference.
        var preferred = LocaleCookie.Read(Request);
        var legacy = await provider.GetPageAsync(slug, preferred);
        if (legacy is not null)
        {
            ViewData["Slug"] = slug;
            return View("Page", legacy);
        }

        // 3. Truly absent in BOTH stores: /about degrades to the product-story
        //    view (a fresh instance's about page); other slugs keep the 404
        //    floor (M·2's page floor).
        if (fallBackToProductStory)
            return View("About",
                new Models.HomeViewModel(community.Value.Name, community.Value.SupportEmail));
        return NotFound();
    }
}
