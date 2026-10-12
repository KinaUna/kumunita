using System.Diagnostics;
using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.ErrorReports;
using Kumunita.Core.Events;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.SiteContent;
using Kumunita.Core.UserInfo;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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

    private readonly ILogger<HomeController> _logger;
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
    /// <param name="siteContent">Optional — the admin-settled landing-surface
    /// content read seam (the <see cref="Kumunita.Core.SiteContent.SiteContent"/>
    /// singleton, ADR 0150). Null in test constructions; the view degrades to
    /// the in-code fallback (the byte-identical shipped <c>kw-l</c> text +
    /// every section shown), so the page always renders (SITE·1).</param>
    /// <param name="errorReports">Optional — the M31 (ADR 0154) report-an-issue
    /// write lane (<see cref="IErrorReportService.CreateAsync"/>). Null in test
    /// constructions; the 500 page still renders and the form is still shown —
    /// a submit with no seam degrades to a 500-safe confirmation, never a crash
    /// (M31·5 "never a 500 back to the resident").</param>
    /// <param name="events">Optional — the upcoming-events feed seam
    /// (<see cref="Kumunita.Core.Events.IEventService.ListUpcomingAsync"/>),
    /// the same read the /events surface uses. Null in test constructions;
    /// the home "upcoming" events column degrades to empty.</param>
    /// <param name="projects">Optional — the to-dos feed seam
    /// (<see cref="Kumunita.Core.Projects.IProjectService.ListTodosAsync"/>),
    /// the same read the /projects/todos surface uses. Null in test
    /// constructions; the home "upcoming" to-dos column degrades to empty.</param>
    public HomeController(
        ILogger<HomeController> logger,
        IOptions<CommunityOptions> community,
        PostService? posts = null,
        IAnnouncementService? announcements = null,
        IPageService? pages = null,
        IAuthorizationService? authz = null,
        ILocalizationService? localization = null,
        IUserInfoService? userInfo = null,
        ITranslationProvider? translationProvider = null,
        ISiteContentService? siteContent = null,
        IErrorReportService? errorReports = null,
        Kumunita.Core.Events.IEventService? events = null,
        Kumunita.Core.Projects.IProjectService? projects = null)
    {
        _logger = logger;
        _community = community.Value;
        Posts = posts;
        Announcements = announcements;
        Pages = pages;
        Authz = authz;
        Localization = localization;
        UserInfo = userInfo;
        TranslationProvider = translationProvider;
        SiteContentService = siteContent;
        ErrorReports = errorReports;
        Events = events;
        Projects = projects;
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
    internal ISiteContentService? SiteContentService { get; }
    internal IErrorReportService? ErrorReports { get; }
    internal Kumunita.Core.Events.IEventService? Events { get; }
    internal Kumunita.Core.Projects.IProjectService? Projects { get; }

    public async Task<IActionResult> Index()
    {
        var feed = await BuildFeedAsync().ConfigureAwait(false);

        // ADR 0149 D1 — the signed-in resident's hide-home-intro preference
        // (Profile.HideHomeIntro): when set, the two top intro sections are
        // omitted so a resident lands straight on the "What's new" feed. The
        // read is best-effort and anonymous-safe: an anonymous visitor has no
        // subject (floor = show the intro), a missing seam or a failed read
        // degrades to the floor, and the page always renders. This is a
        // *display* read only — it never gates anything.
        bool hideIntro = false;
        bool showUpcoming = false;
        bool mineOnly = false;
        var subjectId = KumunitaPrincipal.SubjectId(User);
        if (subjectId is not null && UserInfo is not null)
        {
            try
            {
                var profile = await UserInfo.GetProfileAsync(subjectId).ConfigureAwait(false);
                hideIntro = profile?.HideHomeIntro ?? false;
                // The home "upcoming" display preference (Profile.HomeShowUpcoming)
                // — a pure display read (a read, not a decision): when the
                // resident has opted in, the /home page shows the next events +
                // to-dos above the feed; the floor (false) keeps the page exactly
                // as before. The one profile read serves both flags.
                showUpcoming = profile?.HomeShowUpcoming ?? false;
                // The home "upcoming" scope (Profile.HomeUpcomingMineOnly, ADR
                // 0163 D1a): when on, the section lists the resident's *own*
                // events + to-dos rather than the community-wide lists (the
                // floor, false, is the community-wide shape the section shipped
                // in). Same display-read posture — it changes what is listed,
                // never whether a row is authorized (the mine-scoped reads
                // apply their own CanSeeAsync, as the community-wide ones do).
                mineOnly = profile?.HomeUpcomingMineOnly ?? false;
            }
            catch
            {
                hideIntro = false; // floor: show the intro (never an error page)
                showUpcoming = false; // floor: no upcoming section (never an error page)
                mineOnly = false; // floor: community-wide lists (never an error page)
            }
        }

        // SITE (ADR 0150) — the admin-settled landing-surface content read seam
        // (the SiteContent singleton). Best-effort, the ADR 0050 / ADR 0149
        // shape: a missing seam (a test construction with no service), a missing
        // store, a missing row (a fresh boot before the seeder ran), or a read
        // failure all degrade to null, and the view renders the in-code fallback
        // (the byte-identical shipped kw-l text + every section shown). The page
        // always renders — never a blank page, never an error (SITE·1). This is a
        // *display* read only — it never gates anything.
        Kumunita.Core.SiteContent.SiteContent? site = null;
        if (SiteContentService is not null)
        {
            try
            {
                site = await SiteContentService.GetAsync().ConfigureAwait(false);
            }
            catch
            {
                site = null; // floor: the in-code fallback (the view degrades to shipped text)
            }
        }

        // The home "upcoming" section (the resident's Profile.HomeShowUpcoming
        // preference). Built only when the resident has opted in and is
        // signed-in (an anonymous visitor never sees their own feed — the
        // BuildFeedAsync posture); each column is best-effort (a missing seam
        // or a read failure degrades that column to empty, never the page).
        // Null ⇒ the view omits the section entirely (the ADR 0004 §B.1
        // additive shape — a fresh account that never opted in sees exactly
        // the pre-feature home page).
        HomeUpcoming? upcoming = null;
        if (showUpcoming && subjectId is not null)
        {
            // One per-request language read for the whole section (the
            // BuildFeedAsync ADR 0051 idiom); null ⇒ show authored-in.
            string? upLang = null;
            if (TranslationProvider is not null && Localization is not null)
            {
                try
                {
                    upLang = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, Localization, TranslationProvider).ConfigureAwait(false);
                }
                catch { upLang = null; } // display fallback, not an error
            }

            var upcomingEvents = await BuildUpcomingEventRowsAsync(subjectId, upLang, mineOnly).ConfigureAwait(false);
            var upcomingTodos = await BuildUpcomingTodoRowsAsync(subjectId, upLang, mineOnly).ConfigureAwait(false);

            // Both columns empty ⇒ collapse the section to null (the view omits
            // it entirely) so an opted-in resident with nothing upcoming sees
            // exactly the pre-feature home page — the feed's IsEmpty → hide
            // idiom (no empty "Coming up" header stranded on the page).
            upcoming = (upcomingEvents.Count == 0 && upcomingTodos.Count == 0)
                ? null
                : new HomeUpcoming(upcomingEvents, upcomingTodos);
        }

        return View(new HomeViewModel(_community.Name, _community.SupportEmail, feed, hideIntro, site, upcoming));
    }

    // NOTE (ML-UI U7): GET /about moved to StaticPagesController.About — one
    // route, one owner. It now renders an admin-created `about` Page (the PG
    // tree) when one exists, falling back to the product-story view
    // (Views/StaticPages/About) when the page is truly absent. The footer's
    // asp-action="About" link is a route, so it still targets /about unchanged.

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // M31 (ADR 0154) — read the exception from IExceptionHandlerFeature when
        // this page is the UseExceptionHandler target (Program.cs, the
        // production path). A direct visit (no feature) still renders — the
        // form is the affordance, the exception context is best-effort
        // metadata only (never a gate, M31·2).
        Exception? ex = HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            ExceptionType = ex?.GetType().Name,
            ExceptionMessage = ex?.Message,
        });
    }

    /// <summary>
    /// M31 (ADR 0154) — the 500-page report-an-issue submission. Binds the
    /// resident's description + optional contact email, and (on a valid
    /// model) stores one <see cref="Kumunita.Core.ErrorReports.ErrorReport"/>
    /// row + exactly one <c>AccessAudit</c> row through the U03
    /// <see cref="IErrorReportService.CreateAsync"/> write lane, then re-renders
    /// the error view with the <c>errorreport.thanks</c> confirmation
    /// (<see cref="ErrorViewModel.FormSubmitted"/> = true — no redirect, no
    /// modal, M31·2).
    /// </summary>
    /// <remarks>
    /// <b>Blank description is a 400 re-render, never a 500 (M31·5).</b> The
    /// <c>[Required]</c> + <c>[MinLength]</c> on
    /// <see cref="ErrorReportFormModel.Description"/> surface in
    /// <see cref="ModelState"/>; a failed model re-renders the form pre-filled
    /// (the resident's already-typed text is preserved) instead of calling the
    /// write lane. The form is <b>public</b> (no <c>[Authorize]</c>, M31·2 /
    /// M31·9): an anonymous visitor's <c>SubjectId</c> is the empty string, and
    /// the U03 lane maps that to the <c>AccessVia.Anonymous</c> audit row (the
    /// §2.1 pin — U03's done work, not re-litigated here). The user agent is
    /// truncated to 256 chars before the draft (the register's U04 shape).
    /// </remarks>
    [HttpPost("/Home/Error/Report")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ErrorReport(ErrorReportFormModel form)
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var userAgent = Truncate(HttpContext.Request.Headers.UserAgent.ToString(), 256);
        string subjectId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;

        if (!ModelState.IsValid)
        {
            // A form-level validation error: re-render the form (the existing
            // "Something went wrong" + request-ID content stays intact) with
            // the resident's already-typed values preserved — never a 500.
            return View("Error", new ErrorViewModel
            {
                RequestId = requestId,
                FormSubmitted = false,
                FormDescription = form.Description,
                FormContactEmail = form.ContactEmail,
            });
        }

        // The write lane is optional (null in test constructions): when present,
        // store the report row + one audit row in one session (the U03
        // single-write-lane shape); a failure must never crash the page —
        // degrade to the confirmation (M31·5 "never a 500 back to the resident").
        if (ErrorReports is not null)
        {
            var draft = new ErrorReportDraft(
                SubjectId: subjectId,
                Description: form.Description,
                ContactEmail: string.IsNullOrWhiteSpace(form.ContactEmail) ? null : form.ContactEmail,
                RequestId: requestId,
                ExceptionType: HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error?.GetType().Name,
                UserAgent: userAgent);
            try
            {
                _ = await ErrorReports.CreateAsync(draft).ConfigureAwait(false);
            }
            catch
            {
                // Swallow: a report failure must never surface as a 500 to the
                // resident (the page already failed once — the report is a best-
                // effort follow-up, M31·5). The confirmation still renders.
            }
        }

        return View("Error", new ErrorViewModel
        {
            RequestId = requestId,
            FormSubmitted = true,
        });
    }

    /// <summary>Truncates a value to <paramref name="max"/> chars (the
    /// user-agent cap, the register's U04 shape); null-safe (null in, null
    /// out).</summary>
    private static string? Truncate(string? value, int max) =>
        value is null ? null : (value.Length <= max ? value : value[..max]);

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
                catch (Exception ex) when (ex is not UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Home feed author-profile read failed for post {PostId}; rendering without it.", post.AuthorId);
                    profile = null;
                }

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

    // ── the signed-in "upcoming" section (events + to-dos, the resident's
    // Profile.HomeShowUpcoming preference) ─────────────────────────────────

    /// <summary>
    /// The next upcoming events the viewer can see — the same read + single
    /// <c>CanSeeAsync</c> decision the /events feed uses
    /// (<see cref="Kumunita.Core.Events.IEventService.ListUpcomingAsync"/>),
    /// best-effort: a missing seam or a read failure yields an empty column,
    /// never an error page. The full visible list is returned (not truncated)
    /// so the view can render the first <see cref="HomeUpcoming.Columns"/>
    /// and "show more" the rest client-side (the <c>mine-more</c> idiom).
    /// </summary>
    private async Task<IReadOnlyList<HomeUpcomingRow>> BuildUpcomingEventRowsAsync(string subjectId, string? effLang, bool mineOnly)
    {
        if (Events is null)
            return [];
        try
        {
            // Two scopes, both the resident's own CanSeeAsync reads:
            //  - mineOnly (ADR 0163 D1a): the resident's *own* upcoming events —
            //    their RSVP'd or authored events (EV-MINE, ADR 0065), via
            //    ListMineAsync (Start-ascending, live, author-drafts included).
            //  - otherwise (the floor): the community's upcoming events, page 1
            //    of the feed — ListUpcomingAsync(null, subjectId, ...). The
            //    pageSize is generous (a quick-scan, not a feed page): one page
            //    of 20 covers "next 5 + show more" without a second round trip.
            IReadOnlyList<Event> visible;
            if (mineOnly)
                visible = await Events.ListMineAsync(subjectId).ConfigureAwait(false);
            else
                visible = (await Events.ListUpcomingAsync(null, subjectId, page: 1, pageSize: 20).ConfigureAwait(false)).Items;

            var rows = new List<HomeUpcomingRow>(visible.Count);
            foreach (var ev in visible)
            {
                var title = ev.Title;
                var body = ev.Body;
                if (effLang is not null)
                {
                    try
                    {
                        var translations = await Events.GetEventTranslationsAsync(ev.Id).ConfigureAwait(false);
                        var match = translations.FirstOrDefault(t =>
                            string.Equals(t.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
                        if (match is not null)
                        {
                            if (!string.IsNullOrWhiteSpace(match.Title)) title = match.Title;
                            body = match.Body;
                        }
                    }
                    catch { /* best-effort: the translation read degrades, the row does not */ }
                }

                rows.Add(new HomeUpcomingRow(
                    title,
                    MarkdownRenderer.PlainTextPreview(body, 140),
                    ev.Start,
                    $"/events/{ev.Id}",
                    string.IsNullOrWhiteSpace(ev.Location) ? null : ev.Location));
            }
            return rows;
        }
        catch
        {
            return []; // best-effort: the column degrades, the page does not
        }
    }

    /// <summary>
    /// The top to-dos the viewer can see — the same read + single
    /// <c>CanSeeAsync</c> decision the /projects/todos feed uses
    /// (<see cref="Kumunita.Core.Projects.IProjectService.ListTodosAsync"/>),
    /// best-effort: a missing seam or a read failure yields an empty column,
    /// never an error page. The full visible list is returned (not truncated)
    /// so the view can render the first <see cref="HomeUpcoming.Columns"/>
    /// and "show more" the rest client-side (the <c>mine-more</c> idiom).
    /// </summary>
    private async Task<IReadOnlyList<HomeUpcomingRow>> BuildUpcomingTodoRowsAsync(string subjectId, string? effLang, bool mineOnly)
    {
        if (Projects is null)
            return [];
        try
        {
            // Two scopes, both the resident's own CanSeeAsync read (the same
            // ListTodosAsync the /projects/todos feed uses — a filter, never a
            // gate):
            //  - mineOnly (ADR 0163 D1a): the to-dos *assigned to the
            //    resident* (assigneeId = the subject), the "assigned to me"
            //    idiom (ProjectsController.TodosIndex: effectiveAssigneeId).
            //  - otherwise (the floor): the community's to-dos (assigneeId
            //    null). One page of 20 (the same generous quick-scan window as
            //    the events column).
            var page = await Projects.ListTodosAsync(
                    null,
                    mineOnly ? subjectId : null,
                    subjectId,
                    page: 1,
                    pageSize: 20).ConfigureAwait(false);

            var rows = new List<HomeUpcomingRow>(page.Items.Count);
            foreach (var t in page.Items)
            {
                var title = t.Title;
                var body = t.Body;
                if (effLang is not null)
                {
                    try
                    {
                        var translations = await Projects.GetTodoTranslationsAsync(t.Id).ConfigureAwait(false);
                        var match = translations.FirstOrDefault(tr =>
                            string.Equals(tr.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
                        if (match is not null)
                        {
                            if (!string.IsNullOrWhiteSpace(match.Title)) title = match.Title;
                            if (match.Body is not null) body = match.Body;
                        }
                    }
                    catch { /* best-effort: the translation read degrades, the row does not */ }
                }

                rows.Add(new HomeUpcomingRow(
                    title,
                    MarkdownRenderer.PlainTextPreview(body, 140),
                    t.DueAt,
                    $"/projects/todos/{t.Id}",
                    string.IsNullOrWhiteSpace(t.Status) ? null : t.Status));
            }
            return rows;
        }
        catch
        {
            return []; // best-effort: the column degrades, the page does not
        }
    }
}
