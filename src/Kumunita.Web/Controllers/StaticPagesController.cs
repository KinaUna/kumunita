using Kumunita.Core;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The static-page routes (ADR 0005 A; M5 FACES) — <c>/terms</c>, <c>/help</c>,
/// <c>/privacy</c>, <c>/conduct</c> (SP U01, ADR 0043 D2) and <c>/about</c>.
/// PG U05 (ADR 0039 §3.8/§3.9) retargeted these onto the
/// new <see cref="Page"/> tree; **PG U07 (this unit) completes the absorb** —
/// the legacy fallback seam is gone, so each route reads **only** the tree
/// (<see cref="IPageService.ResolvePageAsync"/> — one store, not two). A path
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
/// The five hard-coded routes are **kept** (backward-compatible) and read
/// from the tree. They remain un-audited public readers (the static-page
/// contract — these resolve to the seeded canonical pages, which are public,
/// <see cref="Page.Audience"/> = <c>null</c>). <c>/privacy</c> and
/// <c>/conduct</c> are 404-floor routes (the page floor, no product-story
/// fallback — ADR 0043 D2: that seam is <c>/about</c>'s only).
/// </para>
/// <para>
/// **Localized body (ADR 0043 D7, amended):** the body + title render in the
/// request's **effective language** — the same frozen chain the
/// <c>&lt;kw-l&gt;</c> TagHelper resolves with (the ADR 0015 / ADR 0046
/// per-request default-language chain: the <c>kumunita.locale</c> cookie, else
/// the browser's <c>Accept-Language</c> tags, else the instance default,
/// else the <c>en</c> floor). A page translation row
/// (<see cref="PageTranslation"/>) for that language supplies the body; when
/// absent, the authored-in (the seeded <c>en</c>) body is the floor — a
/// resident never sees a blank page. This is what lets the seeded
/// de/fr/da translations of these pages (the <c>FirstBootSeeder</c> seed
/// lane) render on a warm deployment where the page itself was authored in
/// <c>en</c>.
/// </para>
/// </summary>
public sealed class StaticPagesController : Controller
{
    private readonly IPageService _pages;
    private readonly ITranslationProvider _translations;
    private readonly ILocalizationService _localization;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<CommunityOptions> _community;

    public StaticPagesController(
        IPageService pages,
        ITranslationProvider translations,
        ILocalizationService localization,
        IHttpContextAccessor httpContextAccessor,
        IOptions<CommunityOptions> community)
    {
        _pages = pages;
        _translations = translations;
        _localization = localization;
        _httpContextAccessor = httpContextAccessor;
        _community = community;
    }

    private static readonly string[] Slugs = { "terms", "help", "about", "privacy", "conduct" };

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
    /// <c>GET /privacy</c> — the privacy-policy static page (SP U01, ADR 0043
    /// D2). Same tree-read + 404 floor as <see cref="Terms"/> — the
    /// <c>fallBackToProductStory</c> seam is <c>/about</c>'s only.
    /// </summary>
    [HttpGet("/privacy")]
    public Task<IActionResult> Privacy() => Page("privacy");

    /// <summary>
    /// <c>GET /conduct</c> — the code-of-conduct static page (SP U01, ADR 0043
    /// D2). Same tree-read + 404 floor as <see cref="Terms"/> — the
    /// <c>fallBackToProductStory</c> seam is <c>/about</c>'s only.
    /// </summary>
    [HttpGet("/conduct")]
    public Task<IActionResult> Conduct() => Page("conduct");

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
        //
        // ADR 0040 — the seeder now nests the canonical static pages under the
        // `system/` root, so `system/{slug}` is the primary resolution. The
        // bare `{slug}` fallback covers instances seeded before ADR 0040 whose
        // terms/help/about were still orphan roots (the seeder re-parents them
        // on next boot, but a page an admin created pre-migration may not have
        // been re-seeded) — keeping the hard-coded routes from 404ing.
        (Page Page, IReadOnlyList<PageTranslation> Translations) resolved;
        try
        {
            resolved = await _pages.ResolvePageAsync($"system/{slug}");
        }
        catch (KeyNotFoundException)
        {
            try
            {
                resolved = await _pages.ResolvePageAsync(slug);
            }
            catch (KeyNotFoundException)
            {
                return FallThrough(fallBackToProductStory);
            }
        }

        var page = resolved.Page;
        var pageTranslations = resolved.Translations;

        // Resolve the effective language the same way <kw-l> does (ADR 0015 D1
        // / ADR 0046 — the frozen per-request default-language chain): the
        // kumunita.locale cookie if present, else the browser's
        // Accept-Language tags (matched against the enabled catalog), else
        // the provider's instance-default → en floor. Core stays HTTP-free
        // (M·8) — we read the request here (the Web layer) and pass plain
        // BCP-47 strings to the provider.
        var request = _httpContextAccessor.HttpContext?.Request;
        var pref = request is null ? null : LocaleCookie.Read(request);
        string effective;
        if (!string.IsNullOrWhiteSpace(pref))
        {
            effective = await _translations.ResolveEffectiveLanguageAsync(pref);
        }
        else if (request is not null)
        {
            var enabled = await _localization.ListLanguagesAsync();
            var candidates = RequestLanguage.BrowserCandidates(request, enabled);
            effective = await _translations.ResolveEffectiveLanguageAsync(candidates);
        }
        else
        {
            effective = await _translations.ResolveEffectiveLanguageAsync((string?)null);
        }

        // Pick the (Title, Body) pair for the effective language. The
        // authored-in body (the seeded en) is the floor: a page with no
        // translation row for the effective language renders its own body
        // (the ADR 0022 read-pin — an un-translated page is not an error).
        var translation = pageTranslations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, effective, StringComparison.OrdinalIgnoreCase));
        var title = translation?.Title ?? page.Title;
        var body = translation?.Body ?? page.Body;

        ViewData["Slug"] = slug;
        // The view (Views/StaticPages/Page.cshtml) is the static-page
        // renderer (Title/Body/Updated); the body renders via the single
        // MarkdownRenderer. The (Title, Body, Updated) triple is
        // byte-identical to the retired static-page output (the seeder
        // wrote both from the same `now`, ADR 0039 §3.9).
        return View("Page", new StaticPageViewModel(
            slug, title, body, page.Modified ?? page.Created));
    }

    private IActionResult FallThrough(bool fallBackToProductStory)
    {
        if (fallBackToProductStory)
            return View("About",
                new Models.HomeViewModel(_community.Value.Name, _community.Value.SupportEmail));
        return NotFound();
    }

    /// <summary>
    /// The static-page render model for <c>/terms</c> / <c>/help</c> /
    /// <c>/privacy</c> / <c>/conduct</c> / <c>/about</c> — the (Slug, Title,
    /// Body, Updated) shape the <c>Views/StaticPages/Page</c> view binds
    /// (Title + rendered Body + last-updated line). <see cref="Updated"/> is
    /// the page's <see cref="Page.Modified"/> (falling back to
    /// <see cref="Page.Created"/>).
    /// </summary>
    public sealed record StaticPageViewModel(
        string Slug,
        string Title,
        string Body,
        DateTimeOffset Updated);
}
