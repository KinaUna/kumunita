using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Tags;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>TG</c> lane's Web surface (ADR 0044 D5; the design doc §2.1
/// <c>GET /tags</c> + <c>GET /tags/{slug}</c> + <c>POST /api/tags/suggest</c>
/// routes, plus the U8c reword lane <c>POST /tags/{slug}/translate</c>).
/// A *thin* HTTP layer (ADR 0006-D): routes + authz + shape; all
/// access decisions come from the <see cref="ITagService"/> read lane (the
/// content's existing <c>Read</c> decision — never a tag-specific gate,
/// C-TG·1). The controller never re-derives access (the M3
/// <see cref="PostsController"/> thin-controller precedent).
/// <para>
/// **Access-scoped by construction (C-TG·1 / C-TG·2):** both browse actions
/// render only what the access-scoped read seam returns. A tag used only on
/// content the viewer cannot read is **absent** from the list, the by-tag
/// results, and autocomplete — no title, no name, no "hidden" placeholder
/// (F3 / F4). The <c>ByTag</c> action maps the empty shape (slug unknown, or
/// the tag used only on unread content) to a **404** (the register's U7
/// 404-floor pin) — a 404 is the right floor here (a 403 would confirm the
/// tag exists, leaking a subject's existence, C-TG·1).
/// <para>
/// **No <c>AccessAudit</c> row (C-TG·8, D7):** the read lane methods emit no
/// <c>tag.*</c> row of their own; the content's own <c>Read</c>-decision
/// rows are the content's. The controller adds no audit of its own.
/// <para>
/// **404-floor (the register's U7 pin):** <c>ByTag</c> returns
/// <see cref="NotFoundResult"/> when both <c>Posts</c> and <c>Pages</c> are
/// empty — covering both "slug unknown" and "tag used only on unread
/// content" (the two are indistinguishable to the viewer; both map to 404,
/// not a blank page).
/// <para>
/// **U8c reword lane (C-TG·5, C-TG·9):** <c>POST /tags/{slug}/translate</c>
/// delegates the write + standing decision to
/// <see cref="ITagService.AddTagTranslationAsync"/> (the standing — the tag's
/// <c>CreatedBy</c> ∪ GlobalAdmin — is re-pinned server-side; the
/// <see cref="TagByTagViewModel.Translation"/> form's
/// <see cref="TagTranslationForm.CanTranslate"/> is only the display
/// affordance, the ADR 0009 / 0026 "the name is the creator's artifact"
/// rule carried to tags). A missing or unreadable tag is a 404 (the C-TG·1
/// floor); a denied standing actor is a 403 (the tag exists and is
/// readable; the standing decision ran and denied — the M3 403-vs-404
/// split pin, the <see cref="PostsController.AddTranslation"/> precedent).
/// The register's U8c "no new seam" pin holds: the seam is exactly what
/// U5's <c>AddTagTranslationAsync</c> already froze; this is a web-wiring
/// unit, not a seam change.
/// </summary>
[Authorize]
public sealed class TagController(
    ITagService tags,
    IPageService pages,
    ILocalizationService localization,
    IDocumentStore store) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    private static IReadOnlySet<string> ActorRoles(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.RoleSet(user);

    // ── GET /tags — the tag list (F12: empty on a fresh instance) ─────────

    /// <summary>
    /// <c>GET /tags</c> — the tag list: the distinct tags used on ≥ 1 post
    /// / blog page the viewer may already read, each with its use-count and
    /// display name resolved in the viewer's language (the C-TG·2 base query;
    /// the ADR 0005 preference order). F12: a fresh instance (zero tags)
    /// renders an empty list, not a 404 (the "no tags yet" shape).
    /// No <c>AccessAudit</c> row (C-TG·8).
    /// </summary>
    [HttpGet("/tags")]
    public async Task<IActionResult> Index()
    {
        var actor = ActorId(User) ?? string.Empty;
        var items = await tags.ListForActorAsync(actor);
        return View(new TagListViewModel { Tags = items });
    }

    // ── GET /tags/{slug} — the by-tag view (404-floor) ────────────────────

    /// <summary>
    /// <c>GET /tags/{slug}</c> — the posts and blog pages tagged with
    /// <paramref name="slug"/> that the viewer may already read (C-TG·3:
    /// the post's own <c>Read</c> decision is applied before the post is
    /// returned). Both <c>Posts</c> and <c>Pages</c> empty ⇒ **404** (the
    /// register's U7 404-floor pin — the tag is unknown, or used only on
    /// unread content; both map to 404, not a blank page, not a 403 — a 403
    /// would confirm the tag exists, C-TG·1). No <c>AccessAudit</c> row of
    /// the tag lane's own (C-TG·8).
    /// </summary>
    [HttpGet("/tags/{slug}")]
    public async Task<IActionResult> ByTag([FromRoute] string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return NotFound();

        var actor = ActorId(User) ?? string.Empty;
        var posts = await tags.ListPostsByTagAsync(slug, actor);
        var blogPages = await tags.ListPagesByTagAsync(slug, actor);

        // 404-floor: both lists empty = slug unknown or used only on unread
        // content (C-TG·1 / C-TG·2 — the two are indistinguishable and both
        // map to 404, not 403 — a 403 would confirm the tag exists).
        if (posts.Count == 0 && blogPages.Count == 0)
            return NotFound();

        // Display name (the ADR 0005 preference order, resolved to the
        // viewer's language): find the tag in the accessor's readable tag
        // set (one extra base-query call — N+1 acceptable for a neighborhood
        // scale, the M2 GroupsController display-name precedent).
        var tagItems = await tags.ListForActorAsync(actor);
        var displayName = tagItems
            .FirstOrDefault(t => t.Tag.Slug == slug)?.DisplayedName
            ?? slug;

        // The id → page map for PagePaths.Href (the derived /pages/… path —
        // the BlogController precedent: load the full non-deleted tree so
        // an ancestor chain is always resolvable).
        var tree = await pages.GetTreeAsync();
        var pageById = tree.ToDictionary(p => p.Id, StringComparer.Ordinal);

        var model = new TagByTagViewModel
        {
            Slug = slug,
            DisplayedName = displayName,
            Posts = posts,
            Pages = blogPages,
            PageById = pageById,
        };

        // U8c — seed the reword form (C-TG·5 standing split: the form is
        // rendered editable for the creator ∪ GlobalAdmin, disabled for a
        // non-creator attacher; the real deny is the POST lane's standing
        // re-check, the C-TG·5 pin). Only seeded when the tag is readable
        // to this actor (the 404-floor above already returned for the
        // unreadable shape, so a null `tag` here means the tag is unknown
        // — defensive; the form is a display surface, never a gate).
        var tag = tagItems.FirstOrDefault(t => t.Tag.Slug == slug)?.Tag;
        if (tag is not null)
            model.Translation = await SeedTranslationFormAsync(tag, actor, ActorRoles(User));

        return View(model);
    }

    // ── POST /tags/{slug}/translate — the U8c reword lane (C-TG·5, C-TG·9) ──

    /// <summary>
    /// <c>POST /tags/{slug}/translate</c> — adds or overwrites the
    /// <paramref name="languageCode"/> translation of the tag resolved from
    /// <paramref name="slug"/> (ADR 0044 D4, C-TG·5). A thin Web lane
    /// (ADR 0006-D: routes + shape) that delegates the write + standing
    /// decision to <see cref="ITagService.AddTagTranslationAsync"/> (the
    /// standing — the tag's <c>CreatedBy</c> ∪ GlobalAdmin — is re-pinned
    /// server-side; the <see cref="TagTranslationForm.CanTranslate"/> probe
    /// is only the display affordance).
    /// <para>
    /// **Precondition (C-TG·1):** the tag must be readable to this actor
    /// (re-run the read seam via <see cref="ITagService.ListForActorAsync"/>
    /// — the tag's visibility is derived from the content the actor may
    /// already read, never a tag-specific gate). A missing or unreadable
    /// tag is a **404** (the C-TG·1 floor — a 403 would confirm the tag
    /// exists).
    /// </para>
    /// <para>
    /// **Standing denial (C-TG·5):** a non-creator attacher who POSTs gets
    /// a <see cref="UnauthorizedAccessException"/> from the service's
    /// standing re-check, surfaced as a **403** <see cref="ForbidResult"/>
    /// (the tag exists and is readable; the standing decision ran and
    /// denied — distinct from the 404 "unknown or unreadable" shape, the
    /// M3 <see cref="PostsController.AddTranslation"/> 403-vs-404 split
    /// pin).
    /// </para>
    /// <para>
    /// **Session shape (C3):** the controller owns the
    /// <see cref="IDocumentStore.LightweightSession"/>; the service's
    /// <c>SaveChangesAsync</c> is the single write — the
    /// <see cref="TagTranslation"/> row and its
    /// <c>tagtranslation.add</c> <c>AccessAudit</c> row commit atomically
    /// (one row, <c>Via</c> = <c>Owner</c> / <c>Admin</c>, the C-TG·9 pin).
    /// </para>
    /// </summary>
    [HttpPost("/tags/{slug}/translate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Translate(
        [FromRoute] string slug,
        [FromForm] string? languageCode,
        [FromForm] string? name)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return NotFound();

        var actor = ActorId(User) ?? string.Empty;
        if (string.IsNullOrEmpty(actor))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/tags/{Uri.EscapeDataString(slug)}");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["error"] = "A translation needs a non-empty display name.";
            return Redirect($"/tags/{Uri.EscapeDataString(slug)}");
        }

        // C-TG·1 precondition: the tag must be readable to this actor (the
        // same read seam the ByTag action uses — the tag's visibility is
        // derived from the content the actor may already read, never a
        // tag-specific gate, C-TG·1). A missing or unreadable tag is a 404
        // (the C-TG·1 floor — the two are indistinguishable to the viewer,
        // both map to 404, not 403 — a 403 would confirm the tag exists).
        var items = await tags.ListForActorAsync(actor);
        var tag = items.FirstOrDefault(t => t.Tag.Slug == slug)?.Tag;
        if (tag is null)
            return NotFound();

        var roles = ActorRoles(User);
        await using var session = store.LightweightSession();
        try
        {
            await tags.AddTagTranslationAsync(
                tag.Id, languageCode, name.Trim(), actor, roles, session);
        }
        catch (UnauthorizedAccessException)
        {
            // C-TG·5 standing re-check failed (the actor is neither the
            // creator nor a GlobalAdmin) — 403, the M3 403-vs-404 split
            // (the tag exists and is readable; the standing decision ran
            // and denied — distinct from the 404 "unknown or unreadable").
            return new ForbidResult();
        }

        TempData["info"] = "Translation saved.";
        return Redirect($"/tags/{Uri.EscapeDataString(slug)}");
    }

    // ── Form seeding (the U8c reword surface) ─────────────────────────────

    /// <summary>
    /// Seeds the <see cref="TagTranslationForm"/> for the by-tag view
    /// (U8c, C-TG·5, D4): one row per enabled <see cref="LanguageCatalog"/>
    /// (the instance catalog, the ADR 0005 B shape — the U7/U8 read lane
    /// already resolves the display name through this same catalog, so no
    /// new catalog surface is opened), pre-filled with the current
    /// <see cref="TagTranslation.Name"/> for that language when a
    /// translation row exists, otherwise the tag's base
    /// <see cref="Tag.Name"/> (the creator's own spelling — the ADR 0005
    /// preference-order fallback). <see cref="TagTranslationForm
    /// .CanTranslate"/> is the <see cref="ITagService.CanTranslateTag"/>
    /// probe (a display pin — the real deny is the POST lane's standing
    /// re-check, C-TG·5, C3).
    /// <para>
    /// The <see cref="TagTranslation"/> rows are read directly via the
    /// controller's <see cref="IDocumentStore"/> (a plain read, no audit row
    /// — C-TG·8); the <see cref="ITagService"/> seam has no
    /// "get translations" method (the U5/U6 frozen 11-member surface does
    /// not include one, and the register's U8c "no new seam" pin forbids
    /// adding one — the read is a Web-layer convenience, not a service
    /// seam).
    /// </para>
    /// </summary>
    private async Task<TagTranslationForm> SeedTranslationFormAsync(
        Tag tag, string actor, IReadOnlySet<string> roles)
    {
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        var enabled = catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Id, StringComparer.Ordinal)
            .ToList();

        // Marten 9 is async-only for data access (a synchronous
        // <c>.ToList()</c> on the queryable throws
        // <c>NotSupportedException</c> at runtime). <c>ToListAsync()</c> is
        // the async terminal operator (the <c>GuardianAssignmentTests</c>
        // precedent: it casts to the internal <c>MartenLinqQueryable</c> and
        // executes a live query, so it cannot be NSubstituted — the ByTag
        // read-path tests back this with a real scratch-Postgres store).
        await using var session = store.QuerySession();
        var translations = await session
            .Query<TagTranslation>()
            .Where(t => t.TagId == tag.Id)
            .ToListAsync();

        // The (TagId, LanguageCode) unique index enforces one row per pair;
        // a defensive GroupBy (Last) guards against a schema drift that
        // would allow duplicates (the write lane is the real guard — the
        // ADR 0004 §B.1 delta-detected idempotent shape).
        var byLang = translations
            .GroupBy(t => t.LanguageCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Name, StringComparer.Ordinal);

        return new TagTranslationForm
        {
            Slug = tag.Slug,
            BaseName = tag.Name,
            BaseLanguageCode = tag.LanguageCode,
            CanTranslate = tags.CanTranslateTag(tag, actor, roles),
            Rows = enabled
                .Select(l => new TagTranslationRow
                {
                    LanguageCode = l.Id,
                    NativeName = l.NativeName,
                    CurrentName = byLang.TryGetValue(l.Id, out var n) ? n : tag.Name,
                })
                .ToList(),
        };
    }

    // ── POST /api/tags/suggest — the autocomplete seam (F9 / F10) ─────────

    /// <summary>
    /// <c>POST /api/tags/suggest</c> — the <see cref="ITagService
    /// .SuggestAsync"/> seam (F9: viewer-language display name; F10: ≤ 10
    /// cap — both enforced in Core). CSRF-aware: the client
    /// (<c>client/lib/api.ts</c>) sends the
    /// <c>RequestVerificationToken</c> header (ADR 0015 §7). No
    /// <c>AccessAudit</c> row (C-TG·8).
    /// </summary>
    [HttpPost("/api/tags/suggest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suggest([FromBody] SuggestRequest request)
    {
        var actor = ActorId(User) ?? string.Empty;
        var suggestions = await tags.SuggestAsync(request?.Prefix ?? string.Empty, actor);
        return Json(new TagSuggestViewModel { Suggestions = suggestions });
    }
}

/// <summary>
/// The <c>POST /api/tags/suggest</c> request body: the typed prefix to match
/// against (<c>starts_with(displayName, prefix) OR starts_with(slug, prefix)</c>,
/// C-TG·2 / C-TG·4). The client (the <c>client/lib</c> tag-suggest dropdown,
/// U8) sends <c>{ "prefix": "san" }</c> as JSON.
/// </summary>
public sealed record SuggestRequest(string Prefix);
