using System.Diagnostics;
using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Kumunita.Web.Models;
using Kumunita.Web.Security;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The site home surface (the landing/roadmap view) + the signed-in
/// "what's new" feed. Thin (ADR 0006-D): every access decision comes from
/// the existing per-surface read seams — the same ones /community,
/// /announcements and /pages use — so a row is visible here only when the
/// viewer could already see it in that surface's own feed. The feed is
/// best-effort display shaping (top-N per surface, ADR 0051 variant
/// selection when a translation row exists); a seam that is absent or
/// throws degrades that column to empty, never the page itself.
/// </summary>
public class HomeController : Controller
{
    /// <summary>How many rows to show per feed column (the "latest" quick scan).</summary>
    private const int FeedRowsPerColumn = 5;

    /// <summary>The semantic badge marker for a pinned announcement (the
    /// view renders it translated; posts/community badges carry a display
    /// name directly).</summary>
    public const string BadgePinned = "pinned";

    private readonly CommunityOptions _community;

    /// <param name="posts">Optional feed seam — the all-sections feed
    /// (<see cref="PostService.ListAllFeedAsync"/>). Null in test
    /// constructions; the posts column degrades to empty.</param>
    /// <param name="announcements">Optional — the visible announcement list
    /// (<see cref="IAnnouncementService.ListVisibleAsync"/>).</param>
    /// <param name="pages">Optional — the page tree read
    /// (<see cref="IPageService.GetTreeAsync"/>).</param>
    /// <param name="authz">Optional — the single CanSeeAsync decision over
    /// the page candidate set (the /pages tree browse's shape).</param>
    /// <param name="localization">Optional — the per-request language
    /// chain (ADR 0051 variant selection).</param>
    /// <param name="userInfo">Optional — the components/profile/community-name
    /// reads the posts column needs.</param>
    /// <param name="translationProvider">Optional — the translation read
    /// seam; when present, rows show the viewer's-language variant (the
    /// ADR 0022 floor) where one exists.</param>
    public HomeController(
        IOptions<CommunityOptions> community,
        PostService? posts = null,
        IAnnouncementService? announcements = null,
        IPageService? pages = null,
        IAuthorizationService? authz = null,
        ILocalizationService? localization = null,
        IUserInfoService? userInfo = null,
        ITranslationProvider? translationProvider = null)
    {
        _community = community.Value;
        Posts = posts;
        Announcements = announcements;
        Pages = pages;
        Authz = authz;
        Localization = localization;
        UserInfo = userInfo;
        TranslationProvider = translationProvider;
    }

    // The optional feed seams (internal so the tests can assert the
    // degradation shape — the view model, not these).
    internal PostService? Posts { get; }
    internal IAnnouncementService? Announcements { get; }
    internal IPageService? Pages { get; }
    internal IAuthorizationService? Authz { get; }
    internal ILocalizationService? Localization { get; }
    internal IUserInfoService? UserInfo { get; }
    internal ITranslationProvider? TranslationProvider { get; }

    public async Task<IActionResult> Index()
    {
        var feed = await BuildFeedAsync().ConfigureAwait(false);
        return View(new HomeViewModel(_community.Name, _community.SupportEmail, feed));
    }

    // NOTE (ML-UI U7): GET /about moved to StaticPagesController.About — one
    // route, one owner. It now renders an admin-created `about` Page (the PG
    // tree) when one exists, falling back to the product-story view
    // (Views/StaticPages/About) when the page is truly absent. The footer's
    // asp-action="About" link is a route, so it still targets /about unchanged.

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    // ── the signed-in "what's new" feed ────────────────────────────────────

    /// <summary>
    /// Assembles the feed for a signed-in visitor, or null for an anonymous
    /// one. Each column is built independently and best-effort: a missing
    /// seam or a read failure yields an empty column, never an error page —
    /// the home page must always render the hero + roadmap.
    /// </summary>
    private async Task<HomeFeed?> BuildFeedAsync()
    {
        var subjectId = KumunitaPrincipal.SubjectId(User);
        if (subjectId is null)
            return null;

        // ADR 0051 — one per-request language read for the whole feed; null
        // (no provider / no language chain) means "show authored-in".
        string? effLang = null;
        if (TranslationProvider is not null && Localization is not null)
        {
            try
            {
                effLang = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, Localization, TranslationProvider).ConfigureAwait(false);
            }
            catch
            {
                effLang = null; // display fallback, not an error
            }
        }

        var roles = User.Claims
            .Where(c => c.Type == Kumunita.Core.Identity.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.Ordinal);

        return new HomeFeed(
            Posts: await BuildPostRowsAsync(subjectId, effLang).ConfigureAwait(false),
            Announcements: await BuildAnnouncementRowsAsync(subjectId, roles, effLang).ConfigureAwait(false),
            Pages: await BuildPageRowsAsync(subjectId, effLang).ConfigureAwait(false));
    }

    /// <summary>
    /// The latest posts the viewer can see — the same read the /community
    /// all-sections feed uses (<see cref="PostService.ListAllFeedAsync"/>, one
    /// aggregate CanSeeAsync over the enabled-component candidate set),
    /// truncated to <see cref="FeedRowsPerColumn"/>. The post's community
    /// name becomes the row badge (the ADR 0026 / 0051 floor).
    /// </summary>
    private async Task<IReadOnlyList<HomeFeedRow>> BuildPostRowsAsync(string subjectId, string? effLang)
    {
        if (Posts is null || UserInfo is null)
            return [];
        try
        {
            var components = await UserInfo.GetComponentsAsync(enabledOnly: true).ConfigureAwait(false);
            if (components.Count == 0)
                return [];

            var componentIds = components.Select(c => c.Id).ToList();
            var nameByComponentId = components.ToDictionary(c => c.Id, c => c.Name);

            // ADR 0051 — resolve each community name in the viewer's language
            // (the ADR 0026 floor) for the badges.
            if (effLang is not null)
            {
                foreach (var c in components)
                {
                    var translations = await UserInfo.GetCommunityTranslationsAsync(c.Id).ConfigureAwait(false);
                    var match = translations.FirstOrDefault(t =>
                        string.Equals(t.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
                    if (match is not null && !string.IsNullOrWhiteSpace(match.Name))
                        nameByComponentId[c.Id] = match.Name;
                }
            }

            var feed = await Posts.ListAllFeedAsync(componentIds, subjectId, page: 1).ConfigureAwait(false);

            var rows = new List<HomeFeedRow>(feed.Visible.Count);
            foreach (var post in feed.Visible.Take(FeedRowsPerColumn))
            {
                var title = post.Title;
                var body = post.Body;
                if (effLang is not null)
                {
                    var translations = await Posts.GetPostTranslationsAsync(post.Id).ConfigureAwait(false);
                    var match = translations.FirstOrDefault(t =>
                        string.Equals(t.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Title)) title = match.Title;
                        body = match.Body;
                    }
                }

                Profile? profile = null;
                try { profile = await UserInfo.GetProfileAsync(post.AuthorId).ConfigureAwait(false); }
                catch { profile = null; }

                var preview = (string.IsNullOrWhiteSpace(post.Title)
                    ? MarkdownRenderer.PlainTextPreview(body, 200)
                    : MarkdownRenderer.PlainTextPreview(body, 140));

                rows.Add(new HomeFeedRow(
                    string.IsNullOrWhiteSpace(title) ? preview ?? string.Empty : title,
                    preview,
                    post.Created,
                    $"/posts/{post.Id}",
                    profile?.DisplayName,
                    post.AuthorId,
                    nameByComponentId.TryGetValue(post.ComponentId, out var communityName) ? communityName : null));
            }
            return rows;
        }
        catch
        {
            return []; // best-effort: the column degrades, the page does not
        }
    }

    /// <summary>
    /// The latest visible announcements — the same read the /announcements
    /// list uses (<see cref="IAnnouncementService.ListVisibleAsync"/>: public
    /// always, community for signed-in), truncated to
    /// <see cref="FeedRowsPerColumn"/>>. A pinned announcement gets the
    /// "pinned" badge (the layout's existing pin concept).
    /// </summary>
    private async Task<IReadOnlyList<HomeFeedRow>> BuildAnnouncementRowsAsync(
            string subjectId, System.Collections.Generic.IReadOnlySet<string> roles, string? effLang)
    {
        if (Announcements is null)
            return [];
        try
        {
            var list = await Announcements.ListVisibleAsync(subjectId, roles).ConfigureAwait(false);

            var rows = new List<HomeFeedRow>(list.Count);
            foreach (var a in list.Take(FeedRowsPerColumn))
            {
                var title = a.Title;
                var body = a.Body;
                if (effLang is not null)
                {
                    var translations = await Announcements.GetAnnouncementTranslationsAsync(a.Id).ConfigureAwait(false);
                    var match = translations.FirstOrDefault(t =>
                        string.Equals(t.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Title)) title = match.Title;
                        body = match.Body;
                    }
                }

                // The badge is a semantic marker ("pinned"), not a display
                // string — the view decides how to render it (translated).
                rows.Add(new HomeFeedRow(
                    title,
                    MarkdownRenderer.PlainTextPreview(body, 140),
                    a.Created,
                    $"/announcements/{a.Id}",
                    a.Pinned ? BadgePinned : null));
            }
            return rows;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// The most recently created visible pages — the same read + single
    /// CanSeeAsync decision the /pages tree browse uses
    /// (<see cref="IPageService.GetTreeAsync"/> + draft gate + one aggregate
    /// <see cref="IAuthorizationService.CanSeeAsync"/>>), truncated to
    /// <see cref="FeedRowsPerColumn"/>>.
    /// </summary>
    private async Task<IReadOnlyList<HomeFeedRow>> BuildPageRowsAsync(string subjectId, string? effLang)
    {
        if (Pages is null || Authz is null)
            return [];
        try
        {
            var tree = await Pages.GetTreeAsync().ConfigureAwait(false);

            // (1) the author-only draft gate (the /pages tree browse's shape)
            var candidate = tree
                .Where(p => !(p.IsDraft && !string.Equals(p.AuthorId, subjectId, StringComparison.Ordinal)))
                .ToList();
            if (candidate.Count == 0)
                return [];

            // (2) the Read decision over the whole candidate set — ONE
            //     CanSeeAsync (the /pages aggregate shape, not one per page).
            var visibleIds = (await Authz.CanSeeAsync(
                    subjectId, AccessAction.Read,
                    candidate.Select(p => new PageToAuditableResource(p)))
                .ConfigureAwait(false)).Visible
                .Select(v => v.Id)
                .ToHashSet(StringComparer.Ordinal);

            var byId = tree.ToDictionary(p => p.Id, StringComparer.Ordinal);

            var rows = new List<HomeFeedRow>();
            foreach (var page in candidate
                         .Where(p => visibleIds.Contains(p.Id))
                         .OrderByDescending(p => p.Created)
                         .Take(FeedRowsPerColumn))
            {
                var title = page.Title;
                if (effLang is not null)
                {
                    var translations = await Pages.GetTranslationsAsync(page.Id).ConfigureAwait(false);
                    var match = translations.FirstOrDefault(t =>
                        string.Equals(t.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
                    if (match is not null && !string.IsNullOrWhiteSpace(match.Title))
                        title = match.Title;
                }

                rows.Add(new HomeFeedRow(title, null, page.Created, PagePaths.Href(byId, page)));
            }
            return rows;
        }
        catch
        {
            return [];
        }
    }
}
