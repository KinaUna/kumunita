using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Moderation;
using Kumunita.Core.Posts;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The community posts surface (M3, plan U7) — the three routes
/// <c>/community/{componentId}</c> (feed), <c>/posts/{id}</c> (detail +
/// one-level replies), <c>/posts/new</c> (compose). A *thin* HTTP layer
/// (ADR 0006-D): routes + authz + shape; all access decisions come from
/// <see cref="PostService"/> (the single decision path, C1 through C6); the
/// controller never re-derives access (the M2
/// <see cref="Kumunita.Web.Controllers.DirectoryController"/> M2 thin-
/// controller precedent, the same "route + authz + shape" pattern).
/// <para>
/// <b>§2.3 candidate-filter table at the Web layer</b> (the M3 Web-layer
/// precondition that Core never sees):
/// <list type="number">
/// <item>Unauthenticated ⇒ challenge (the <see cref="AuthorizeAttribute"/>) —
/// Core never sees an empty actor (Core requires a non-empty
/// <c>actorId</c>).</item>
/// <item>Missing/disabled component ⇒ 404 (via the
/// <see cref="IUserInfoService.GetComponentsAsync(bool)"/> candidate-set
/// read — a "which community?" precondition that the
/// <see cref="PostService.ListFeedAsync"/> feed query targets).
/// No candidate posts are loaded, no <c>AccessAudit</c> row (C-M3·2: the
/// component is a *feed organizer*, not a decision).</item>
/// <item>Present enabled component ⇒ <c>PostService.ListFeedAsync</c> —
/// the M3's §2.3 candidate-filter + single <c>CanSeeAsync</c> shape, the
/// Core's <see cref="PostToAuditableResource"/> adapter is the sole
/// <c>AccessAudit</c> subject (C3).</item>
/// </list>
/// <para>
/// <b>Detail / 403 shape:</b> a <see
/// cref="Kumunita.Core.Posts.PostService.GetPostAsync"/> result with
/// <c>Post = null</c> covers **both** "does not exist" (no decision row
/// ran, C3) and "audience denied" (the <c>CanAsync</c> decision row <i>was</i>
/// written — the audit trail distinguishes; M3 has no moderator
/// branch that changes the outcome, C5). The controller maps both to
/// the <see cref="StatusCode(int)"/> 403 shape — the M3's § U7 "403 on
/// denied, not a blank page" — because a 404 is information-leaky (its
/// shape distinguishes "id exists" from "id doesn't"; a generic 403
/// tells the viewer *nothing* about which ids are real).
/// <para>
/// <b>Write lane (POST /posts/new)</b> (the M3 write path, the M2
/// <see cref="Kumunita.Web.Controllers.ProfileController">Edit</see> POST
/// precedent): open the controller's own
/// <see cref="DocumentStore.LightweightSession()"/> session, delegate the
/// write to <see cref="PostService.CreatePostAsync"/> (the C3 same-
/// transaction lane: the service is the <b>caller</b> of the session,
/// and the service's <c>SaveChangesAsync</c> is the single write),
/// redirect to the new <c>/posts/{id}</c>. The <b>audience</b> editor
/// (a M2 reusable <see cref="Kumunita.Web.Models.AudienceEditorModel"/>,
/// the "reuse, don't re-invent" pin) round-trips through
/// <see cref="Kumunita.Web.Models.AudienceEditorModel.BuildAudience()"/>
/// (the single deserialization site — the M2 single-source pin) into the
/// <see cref="Kumunita.Core.Authorization.Audience"/> the
/// <see cref="PostDraft"/> carries — the <b>author's choice verbatim</b>
/// (ADR 0001-B), never a second <c>Audience</c> object. The component
/// picker (<c>ComponentId</c>) is a **feed organizer / candidate filter**
/// selection (C-M3·2): the component is *which community* the post
/// lands under, not an access decision — the audience is the *only*
/// access boundary on this form.
/// </para>
/// </summary>
[Authorize]
public sealed class PostsController(
    PostService posts,
    ModerationService moderation,
    IUserInfoService userInfo,
    ILocalizationService localization,
    IDocumentStore store,
    // ADR 0044 (TG lane) — the tag read seam. The Detail action renders the
    // post's tags (display names resolved in the viewer's language) and the
    // Edit GET pre-seeds the existing tag slugs for the tag-suggest input.
    // **Optional** (default null) so the existing test-construction sites
    // that build this controller without a tag service keep compiling —
    // the tag display/seed surfaces are no-ops when the seam is absent.
    // DI always supplies the live <c>ITagService</c> in the app.
    ITagService? tags = null,
    // ADR 0051 (extending ADR 0049 to the /community list surfaces) — the
    // per-request translation read seam, used only to auto-select which of an
    // item's pre-rendered variants (ADR 0022 posts, ADR 0026 community names)
    // is default-visible in the feed rows. **Optional** so any test-construction
    // site exercising the as-authored fallback keeps compiling; DI always
    // supplies the seam in the app. (PostService is sealed — this controller is
    // exercised by the integration/FACES lanes, not NSubstitute, per
    // TranslationDisplayTests.)
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    // ── Feed (GET /community/{componentId}) ──────────────────────────────

    /// <summary>
    /// The community feed (F1/F2/F8/F9, the §2.3 candidate-filter shape):
    /// the visible posts for <paramref name="componentId"/> (the
    /// candidate filter is the component's <c>Post</c> set — a *feed
    /// organizer*, never an access decision; C-M3·2) + the hidden count
    /// (the <see cref="FeedResult.HiddenCount"/> row's count; the hidden
    /// posts' rows' fields never reach the view model — F1/F2).
    /// §2.3 row 2 (missing/disabled component) is a 404 at this layer;
    /// §2.3 row 1 (unauth) is the <see cref="AuthorizeAttribute"/>.
    /// </summary>
    [HttpGet("/community/{componentId}")]
    public async Task<IActionResult> Index([FromRoute] string componentId, int page = 1)
    {
        if (string.IsNullOrEmpty(componentId))
            return NotFound();

        var actor = SubjectId(User) ?? string.Empty;

        // §2.3 row 2 — the missing/disabled-component 404. The
        // <see cref="IUserInfoService.GetComponentsAsync(bool)"/>
        // candidate set (M3's single freeze-surface ADD — the composer's
        // component picker, the feed's grouping, the feed's candidate
        // filter — the same read lane serves all three) is a *candidate*
        // set, never a visible set (C-M3·2) and never an <c>AccessAudit</c>
        // subject (C-M3·2; the Core-layer seam test
        // GetComponentsAsync_CandidateFilterEmitsNoAuditRow pins this).
        var components = await userInfo.GetComponentsAsync(enabledOnly: true);
        var component = components.FirstOrDefault(c => c.Id == componentId);
        if (component is null)
            // §2.3 row 2: missing/disabled component — the pre-feed
            // 404 (no candidate posts loaded, no audit row). A disabled
            // component is the same class of bug as a missing one —
            // the "enabledOnly" flag on the read is the Web-layer
            // pin, the Core never branches on <c>Enabled</c> for its
            // own access decision.
            return NotFound();

        var feed = await posts.ListFeedAsync(componentId, actor, page: page);

        // Whether the current viewer holds a posting right on *this* community —
        // the exact rule the composer's POST gate enforces (AccessibleComponentsAsync
        // mirrors PostService.CreatePostAsync's gate). Drives the "Write a post"
        // button's visibility in the view: if the viewer can't post to this
        // component, the button would just dead-end on the composer, so we hide it.
        var accessible = await AccessibleComponentsAsync(User);
        var canPost = accessible.Any(c => c.Id == componentId);

        // ADR 0012 — the feed-header surface flags (the CommunityController
        // route gates are the SoD walls; these only decide what the view
        // offers to this viewer): mandatory state (the badge), management
        // standing (the "Manage" link), the self-leave offer (a member with
        // no management standing, optional community only).
        var manages = KumunitaPrincipal.IsGlobalAdmin(User)
            || KumunitaPrincipal.HasRole(User, Roles.ModeratorComponent(componentId));
        var isMandatory = component.Mandatory;
        var canLeave = canPost && !manages && !isMandatory;

        // ADR 0026 / ADR 0053 — the feed-header "Translations" link (next to
        // the ADR 0012 "Manage members" link): offered when the viewer holds
        // the community's translation standing (GlobalAdmin ∪ Translator —
        // the same rule the dedicated page's gate accepts; a component
        // moderator may view the rows via the page but the feed only
        // advertises it to the standing that acts on it).
        var canTranslate = userInfo.CanTranslateCommunity(
            actor, KumunitaPrincipal.RoleSet(User));

        // ADR 0051 — extend ADR 0049's default-visible-variant rule (already in
        // force on the detail views) to this list surface: the feed header shows
        // the community's name in the viewer's current language when a
        // translation exists (else the authored name), and each row shows the
        // post's title/body in the viewer's current language when a translation
        // exists (else the authored — the ADR 0022 floor). One read of the shared
        // per-request chain; a "a read, not a decision" surface (the feed's
        // CanSeeAsync already ran). No Core / schema change.
        string? effLang = null;
        if (translationProvider is not null)
        {
            effLang = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider);
            component.Name = await ResolveCommunityNameAsync(component.Id, component.Name, effLang);
            foreach (var p in feed.Visible)
                await ApplyTranslationToPostAsync(p, effLang);
        }
        // The sidebar directory (the viewer's own community list, the
        // airy-side-list rail) shows each community's name in the viewer's
        // current language when a translation exists (the ADR 0026 floor),
        // matching the header and the row badges above — the ADR 0051 extension
        // to the community directory surface (a read, not a decision: the
        // component's enabled-visibility gate already ran).
        var accessibleLinks = new List<CommunityLink>(accessible.Count);
        foreach (var c in accessible)
            accessibleLinks.Add(new CommunityLink(
                c.Id,
                effLang is not null ? await ResolveCommunityNameAsync(c.Id, c.Name, effLang) : c.Name));

        var items = new List<PostListItem>(feed.Visible.Count);
        foreach (var post in feed.Visible)
        {
            // The author's display name — a <c>GetProfileAsync</c> read
            // (a *display* lookup, never an <c>AccessAudit</c> subject;
            // the audience decision is <b>already made</b> by
            // <see cref="PostService.ListFeedAsync"/>'s single
            // <c>CanSeeAsync</c> call). The M2
            // <see cref="Kumunita.Web.Controllers.GroupsController"/>
            // detail display-name precedent (N+1 acceptable; the
            // feed's <c>Post</c> count is small by design — a
            // neighborhood, not a firehose).
            var profile = await userInfo.GetProfileAsync(post.AuthorId);
            var preview = MarkdownRenderer.PlainTextPreview(post.Body, 200);
            items.Add(new PostListItem(
                post.Id,
                post.Title,
                preview,
                post.Created,
                profile?.DisplayName ?? post.AuthorId,
                post.AuthorId));
        }

        return View(new FeedViewModel
        {
            ComponentId = componentId,
            ComponentName = component.Name,
            Items = items,
            Total = feed.Total,
            CanPost = canPost,
            IsMandatory = isMandatory,
            CanManageCommunity = manages,
            CanTranslateCommunity = canTranslate,
            CanLeaveCommunity = canLeave,
            // The viewer's own community directory only: communities they have
            // access to (membership ∪ moderator scope ∪ GlobalAdmin — the same
            // <see cref="AccessibleComponentsAsync"/> rule driving CanPost). A
            // viewer with no reachable communities renders no pill directory;
            // a GlobalAdmin still sees every enabled community.
            Communities = accessibleLinks,
            // M7 (ADR 0090 D5) — the pager (F2 one-page no-render pin): null on
            // a single page so the _Pager partial renders nothing. No filter
            // form on this surface (D9) — the links carry ?page=N only.
            Pager = (feed.HasMore || page > 1)
                ? PagedViewModel.ForRoute($"/community/{componentId}", page, 30, feed.HasMore)
                : null,
        });
    }

    // ── All-sections feed (GET /community) ────────────────────────────────

    /// <summary>
    /// The all-sections community feed: the union of visible posts across
    /// every <b>enabled</b> <c>Component</c>, each row labelled with its
    /// section name (the <see cref="PostListItem.ComponentName"/> badge).
    /// <para>
    /// The same ADR invariants as the single-section feed apply:
    /// <see cref="PostService.ListAllFeedAsync"/> is the seam — one
    /// <c>CanSeeAsync</c> pass over the whole candidate set (C6/C-M3·3),
    /// one aggregate <c>AccessAudit</c> row (not one per component; a
    /// per-component call would leak "which components hold content" via
    /// the audit lane). The <see cref="IUserInfoService.GetComponentsAsync(bool)"/>
    /// candidate set (enabled only) is the *feed organizer* — never an
    /// access decision (C-M3·2).
    /// </para>
    /// <para>
    /// When no enabled components exist (a bootstrap edge), the action
    /// renders an empty feed (the "no posts yet" shape, not a 404 — a 404
    /// would be indistinguishable from a missing route).
    /// </para>
    /// </summary>
    [HttpGet("/community")]
    public async Task<IActionResult> AllSections(int page = 1)
    {
        var actor = SubjectId(User) ?? string.Empty;

        var components = await userInfo.GetComponentsAsync(enabledOnly: true);
        // The viewer's posting reach is the same rule the composer's POST gate uses
        // (AccessibleComponentsAsync mirrors PostService.CreatePostAsync's gate). On
        // the all-sections feed, "Write a post" is offered only when the viewer can
        // actually post to *at least one* enabled community — otherwise the button
        // would just dead-end on an empty composer.
        var accessible = await AccessibleComponentsAsync(User);
        var canPost = accessible.Count > 0;

        if (components.Count == 0)
        {
            return View("Index", new FeedViewModel
            {
                ComponentName = "Community",
                Items = [],
                Total = 0,
                CanPost = false, // no communities at all, so no posting right to offer
            });
        }

        var componentIds = components.Select(c => c.Id).ToList();
        var nameByComponentId = components.ToDictionary(c => c.Id, c => c.Name);

        var feed = await posts.ListAllFeedAsync(componentIds, actor, page: page);

        // ADR 0051 — the all-sections feed shows each post + its section name in
        // the viewer's current language when a translation exists, else the
        // authored-in text (the ADR 0022/0026 floor). One read of the shared
        // per-request chain; a read, not a decision (ListAllFeedAsync's
        // CanSeeAsync already ran). No Core / schema change.
        string? effLang = null;
        if (translationProvider is not null)
        {
            effLang = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider);
            foreach (var c in components)
                nameByComponentId[c.Id] = await ResolveCommunityNameAsync(c.Id, nameByComponentId[c.Id], effLang);
            foreach (var p in feed.Visible)
                await ApplyTranslationToPostAsync(p, effLang);
        }
        // The sidebar directory (the viewer's own community list, the
        // airy-side-list rail) shows each community's name in the viewer's
        // current language when a translation exists (the ADR 0026 floor),
        // matching the row section badges above — the ADR 0051 extension to the
        // community directory surface (a read, not a decision: the component's
        // enabled-visibility gate already ran).
        var accessibleLinks = new List<CommunityLink>(accessible.Count);
        foreach (var c in accessible)
            accessibleLinks.Add(new CommunityLink(
                c.Id,
                effLang is not null ? await ResolveCommunityNameAsync(c.Id, c.Name, effLang) : c.Name));

        var items = new List<PostListItem>(feed.Visible.Count);
        foreach (var post in feed.Visible)
        {
            var profile = await userInfo.GetProfileAsync(post.AuthorId);
            var preview = MarkdownRenderer.PlainTextPreview(post.Body, 200);
            items.Add(new PostListItem(
                post.Id,
                post.Title,
                preview,
                post.Created,
                profile?.DisplayName ?? post.AuthorId,
                post.AuthorId,
                nameByComponentId.TryGetValue(post.ComponentId, out var name) ? name : null,
                post.ComponentId));
        }

        return View("Index", new FeedViewModel
        {
            ComponentName = "Community",
            Items = items,
            Total = feed.Total,
            CanPost = canPost,
            // The viewer's own community directory only — the same reachable
            // set that drives CanPost above (a member sees their communities,
            // a GlobalAdmin sees every enabled one), so a viewer with no
            // reachable communities renders no pill directory.
            Communities = accessibleLinks,
            // M7 (ADR 0090 D5) — the pager (F2 one-page no-render pin): null on
            // a single page so the _Pager partial renders nothing. No filter
            // form on this surface (D9) — the links carry ?page=N only.
            Pager = (feed.HasMore || page > 1)
                ? PagedViewModel.ForRoute("/community", page, 30, feed.HasMore)
                : null,
        });
    }

    // ── ADR 0051 — list-surface variant selection (the Web-layer helpers) ──

    /// <summary>
    /// ADR 0051 — pick the community's name in the viewer's current language
    /// from its user-added name/description translation (ADR 0026), falling
    /// back to the authored <paramref name="fallbackName"/> when no translation
    /// row for <paramref name="effLang"/> exists or its name is blank (the
    /// ADR 0026 floor). A "a read, not a decision" surface — the community's
    /// enabled-visibility gate already ran; this only chooses which stored,
    /// human-authored row to show.
    /// </summary>
    private async Task<string> ResolveCommunityNameAsync(string componentId, string fallbackName, string effLang)
    {
        if (translationProvider is null)
            return fallbackName;
        var translations = await userInfo.GetCommunityTranslationsAsync(componentId);
        var match = translations.FirstOrDefault(t => String.Equals(t.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
        return match is not null && !string.IsNullOrWhiteSpace(match.Name) ? match.Name : fallbackName;
    }

    /// <summary>
    /// ADR 0051 — in place, swap <paramref name="post"/>'s Title/Body to the
    /// translation row in the viewer's current language when one exists: the
    /// translation's body (required on the row), and its title when non-blank
    /// (otherwise the authored title is kept — the ADR 0022 floor). A read, not
    /// a decision: the post's <c>CanSeeAsync</c> already ran in the feed read.
    /// No content is generated, rewritten, or fetched — only the exact
    /// human-authored row (ADR 0018/0022) is selected.
    /// </summary>
    private async Task ApplyTranslationToPostAsync(Post post, string effLang)
    {
        if (translationProvider is null)
            return;
        var translations = await posts.GetPostTranslationsAsync(post.Id);
        var match = translations.FirstOrDefault(t => String.Equals(t.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return;
        if (!string.IsNullOrWhiteSpace(match.Title))
            post.Title = match.Title; // blank translation title → the authored-in title
        post.Body = match.Body; // Body is required on a translation row
    }

    // ── Detail + one-level replies (GET /posts/{id}) ─────────────────────

    /// <summary>
    /// A post's detail + its one-level replies (F10, §2.4 4-shape table):
    /// the <see cref="Kumunita.Core.Posts.PostService.GetPostAsync"/>
    /// shape — one <c>CanAsync</c> decision row (C-M3·3, C6), the
    /// <b>already-authorized</b> reply list (C-M3·1: no second
    /// <c>Can*Async</c> on a reply — the reply's visibility inherits the
    /// parent's single <c>Read</c> decision; a
    /// <b>Denied</b> parent <b>short-circuits</b> at the parent — the
    /// replies' rows are *not* loaded at the Core layer either). A
    /// missing post is the same <c>Post = null</c> shape (the "no
    /// decision row ran" vs. "decision row Deny'd" distinction is on
    /// the <c>AccessAudit</c> row, not the Web shape) — the controller
    /// maps both to the 403 fail-closed (the §2.3 "403 on denied, not
    /// a blank page"; a 404 is information-leaky about which ids are
    /// real).
    /// </summary>
    [HttpGet("/posts/{id}")]
    public async Task<IActionResult> Detail([FromRoute] string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User) ?? string.Empty;

        var result = await posts.GetPostAsync(id, actor);
        if (result.Post is null)
        {
            // Forbid() is the idiomatic ASP.NET Core 403 (the "403 on
            // denied, not a blank page" § U7 pin). Deliberately not
            // NotFound(): a 404 says "this id is not real" — the very
            // information-leak the §2.3 fail-closed posture avoids.
            return Forbid();
        }

        // The author's display name (a <c>GetProfileAsync</c> read, not
        // a decision — the audience decision is already made by
        // <see cref="PostService.GetPostAsync"/>'s single
        // <c>CanAsync</c> call). The M2
        // <see cref="Kumunita.Web.Controllers.GroupsController"/> detail
        // display-name precedent.
        var authorProfile = await userInfo.GetProfileAsync(result.Post.AuthorId);

        // The <b>already-authorized</b> reply list (C-M3·1: no
        // re-check in the controller; no second <c>Can*Async</c> call —
        // the reply's visibility inherits the parent's single
        // <c>Read</c> decision; the parent's Allow was the
        // <see cref="PostService.GetPostAsync"/> shape, so the replies
        // are all "visible under the parent"). Each reply's
        // <see cref="Kumunita.Web.Models.ReplyItem.AuthorDisplayName"/>
        // is a <c>GetProfileAsync</c> read (the N+1 acceptable — the
        // reply count is small by design; one-level).
        // ADR 0022 — the post's user-added translations (a "a read, not a
        // decision" surface; the parent's single Read decision already ran in
        // GetPostAsync) and the enabled-catalog language set the chips /
        // "add a translation" candidate list render from.
        var postTranslations = await posts.GetPostTranslationsAsync(result.Post.Id);
        var enabledLanguages = await SeedLanguagePickerAsync();
        var translationCodes = postTranslations.Select(t => t.LanguageCode).ToHashSet();
        var languages = enabledLanguages
            .Select(l => new LanguageOption(l.Code, l.NativeName, translationCodes.Contains(l.Code)))
            .ToList();
        var actorRoles = KumunitaPrincipal.RoleSet(User);
        var canTranslate = PostService.CanAddTranslation(
            result.Post.GroupId.Length > 0, result.Post.ComponentId, result.Post.AuthorId, actor, actorRoles);

        // ADR 0044 (TG lane) — the post's tags, resolved to display names in
        // the viewer's language (the ADR 0005 preference order —
        // ListForActorAsync already does the TagTranslation lookup). A "a
        // read, not a decision" surface: the post's single Read decision ran
        // in GetPostAsync above, and the tag seam is access-scoped by
        // construction (C-TG·1 — a tag is a label, never a gate; a dangling
        // TagId whose Tag row the actor can't resolve is simply dropped, so
        // a broken reference renders as *nothing*, not a 404/error). One
        // read of the shared per-request chain; N+1 not applicable (a single
        // base-query call, the M2 GroupsController display-name precedent).
        // No ITagService (test construction site) ⇒ empty list (no-op).
        var tagRows = new List<(string Slug, string DisplayedName)>();
        if (tags is not null && result.Post.TagIds.Count > 0)
        {
            var postTagIds = result.Post.TagIds.ToHashSet(StringComparer.Ordinal);
            var readable = await tags.ListForActorAsync(actor);
            tagRows = readable
                .Where(t => postTagIds.Contains(t.Tag.Id))
                .Select(t => (Slug: t.Tag.Slug, DisplayedName: t.DisplayedName))
                .OrderBy(x => x.DisplayedName, StringComparer.Ordinal)
                .ThenBy(x => x.Slug, StringComparer.Ordinal)
                .ToList();
        }

        // Batch-load every reply's translations up front (one query for the
        // whole reply list — the M2 read-lane "one query per surface" preference).
        var replyIds = result.Replies.Select(r => r.Id).ToList();
        var allReplyTranslations = await posts.GetReplyTranslationsAsync(replyIds);
        var translationsByReply = allReplyTranslations
            .GroupBy(t => t.ReplyId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Kumunita.Core.Posts.ReplyTranslation>)g.ToList());

        var replyItems = new List<ReplyItem>(result.Replies.Count);
        foreach (var reply in result.Replies)
        {
            var replyAuthorProfile = await userInfo.GetProfileAsync(reply.AuthorId);
            // ADR 0022 — the reply's standing is its parent's (community lane,
            // same component scope); the reply's own author is the Owner branch.
            var canTranslateReply = PostService.CanAddTranslation(
                result.Post.GroupId.Length > 0, result.Post.ComponentId, reply.AuthorId, actor, actorRoles);
            replyItems.Add(new ReplyItem(
                reply.Id,
                replyAuthorProfile?.DisplayName ?? reply.AuthorId,
                reply.AuthorId,
                reply.Body,
                reply.Created,
                reply.Modified,
                reply.AuthorId == actor,
                translationsByReply.TryGetValue(reply.Id, out var trs) ? trs : [],
                canTranslateReply,
                reply.DeletedAt,
                reply.LanguageCode));
        }

        // ADR 0018 — the reply form's authored-in language picker options
        // (the enabled catalog, the SeedLanguagePickerAsync source), stored
        // on ViewData (the same read-only channel the composer's grant
        // picker uses — the detail VM is a projection, not a form-bound
        // model, so it does not carry picker option lists).
        ViewData["Reply_Languages"] = enabledLanguages;

        // Back-link display name — the post's community name in the viewer's
        // language (the ADR 0026 floor, exactly the feed's
        // ResolveCommunityNameAsync idiom; a read, not a decision: the
        // post's single Read decision already ran in GetPostAsync). A
        // display gap, not an error: when the component cannot be resolved
        // the raw ComponentId stays the fallback (the page still renders).
        string? communityName = null;
        if (result.Post.ComponentId.Length > 0)
        {
            communityName = (await userInfo.GetComponentsAsync(enabledOnly: true))
                .FirstOrDefault(c => c.Id == result.Post.ComponentId)?.Name;
            if (communityName is not null && translationProvider is not null)
            {
                string? backLinkLang = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider);
                communityName = await ResolveCommunityNameAsync(result.Post.ComponentId, communityName, backLinkLang);
            }
        }

        return View(new PostDetailViewModel
        {
            Post = result.Post,
            AuthorDisplayName = authorProfile?.DisplayName ?? result.Post.AuthorId,
            AuthorSubjectId = result.Post.AuthorId,
            Replies = replyItems,
            IsAuthor = result.Post.AuthorId == actor,
            PostTranslations = postTranslations,
            Languages = languages,
            CanTranslate = canTranslate,
            OriginalLanguageCode = result.Post.LanguageCode, // TD·1/TD·4 (ADR 0027) — the authored-in code, read from the ADR 0018 field.
            Tags = tagRows,
            CommunityDisplayName = communityName ?? result.Post.ComponentId,
        });
    }

    // ── Compose (GET + POST /posts/new) ──────────────────────────────────

    /// <summary>
    /// The composer's <c>GET</c>. Seeds the form with:
    /// <list type="bullet">
    /// <item><b>Component picker</b> (a <c>ComponentId</c> dropdown) —
    /// the <see cref="IUserInfoService.GetComponentsAsync(bool)"/>
    /// candidate set (M3's single freeze-surface ADD; C-M3·2: a *feed
    /// organizer* / *candidate filter*; the composer's picker is the
    /// "which community is this post for" bucket, never an access
    /// decision). Enabled only (the disabled components are not
    /// visible to the user — the "not on this form" shape,
    /// not a "visible but not pickable" shape).</item>
    /// <item><b>Audience editor</b> (a <see
    /// cref="Kumunita.Web.Models.AudienceEditorModel"/> — the M2
    /// reuse-verbatim pin, U7's "reuse, don't re-invent") — seeded
    /// with the bootstrap self-only shape (ADR 0001-B; invariant C1: an
    /// empty audience's <b>owner branch</b> is the only way the author
    /// sees their own draft, so the editor's <b>default</b> is an
    /// *empty audience* — the "you'll only see this post yourself
    /// until you add a grant" shape; the user adds a grant to share
    /// it, never the reverse).</item>
    /// </list>
    /// A missing/unreadable component list is fail-safe: the form seeds
    /// an empty shape (the M2
    /// <see cref="Kumunita.Web.Controllers.ProfileController">Edit</see>
    /// GET's "missing <c>Profile</c> row ⇒ empty editor" precedent). The
    /// picker itself is the <b>poster-reachable</b> enabled set (the
    /// <see cref="AccessibleComponentsAsync"/> rule mirrors the POST gate
    /// in <see cref="PostService.CreatePostAsync"/>), not the full enabled
    /// set.
    /// </summary>
    /// <summary>
    /// The enabled components <paramref name="user"/> can post to — the
    /// picker's candidate set, filtered by the **same** posting-right rule the
    /// <see cref="PostService.CreatePostAsync"/> gate applies at POST time:
    /// a <c>GlobalAdmin</c> sees every enabled component; a per-component
    /// Moderator (<see cref="Kumunita.Core.Identity.Roles.ModeratorComponent(string)"/>
    /// claim) sees their scoped components **plus** any explicit memberships;
    /// everyone else sees only the components they hold a
    /// <c>ComponentMembership</c> row for. Keeping the picker and the POST
    /// gate on one rule is what stops a plain member from selecting a
    /// community they can never post to (the POST gate remains the
    /// authoritative deny and is unchanged).
    /// </summary>
    private async Task<IReadOnlyList<Component>> AccessibleComponentsAsync(
        System.Security.Claims.ClaimsPrincipal user)
    {
        var all = await userInfo.GetComponentsAsync(enabledOnly: true);

        if (KumunitaPrincipal.IsGlobalAdmin(user))
            return all; // GlobalAdmin bypasses the membership check

        var accessible = new HashSet<string>();
        var subject = SubjectId(user);
        if (!string.IsNullOrEmpty(subject))
            accessible.UnionWith(await userInfo.GetCommunityIdsAsync(subject));

        // Per-component Moderator scope grants a posting right on top of
        // explicit membership rows (the same claim set the POST gate reads).
        var prefix = Kumunita.Core.Identity.Roles.ModeratorComponent(string.Empty); // "moderator:"
        foreach (var role in KumunitaPrincipal.RoleSet(user))
        {
            if (role.StartsWith(prefix, StringComparison.Ordinal) && role.Length > prefix.Length)
                accessible.Add(role[prefix.Length..]);
        }

        return all.Where(c => accessible.Contains(c.Id)).ToList();
    }

    /// <summary>
    /// Seeds the composer's "Who to grant to" option lists for the shared
    /// <c>Views/Shared/_GrantPickers</c> partial — the <b>same option
    /// source</b> the M2 <see cref="ProfileController"/>'s editor uses
    /// (its private <c>SeedGrantPickerOptionsAsync</c>, mirrored here for
    /// the composer surface): <b>Users</b> — every visible, non-blocked,
    /// verified <c>Profile</c> <b>except the actor themselves</b> (the F6
    /// "thin token, fat authorization" rule: self-access is implicit and
    /// <b>Groups</b> — the platform-wide <i>public</i> group
    /// list (<c>IUserInfoService.GetPublicGroupsAsync</c>; ADR 0010 — a
    /// private group is an organizing/membership unit, never granted as an
    /// audience, so it stays out of the composer's picker; a resident's
    /// membership does not constrain which <i>public</i> groups they may
    /// name in a post's audience — the decision is on the <c>Group</c>
    /// subject).
    /// Stored on the statically-typed <see cref="Controller.ViewData"/>
    /// (NOT <c>ViewBag</c> — the bag's indexer throws
    /// <see cref="RuntimeBinderException"/>; the same channel the M2
    /// profile editor reads its options from). Read-only view data, never
    /// model properties on <see cref="PostComposeViewModel"/> — the
    /// only form-bound grants field remains the partial's hidden
    /// <c>Audience.Grants</c> textarea (the M2 U11 / F13 single-source
    /// pin, carried verbatim to the composer).
    /// </summary>
    private async Task SeedGrantPickerOptionsAsync()
    {
        var profiles = await userInfo.GetProfilesAsync(verifiedOnly: true);
        var selfId = SubjectId(User);
        var userOptions = profiles
            .Where(p => !p.Blocked)
            .Where(p => !string.Equals(p.SubjectId, selfId, StringComparison.Ordinal))
            .Select(p => new GrantOption
            {
                Id    = p.SubjectId,
                Label = string.IsNullOrWhiteSpace(p.DisplayName) ? p.SubjectId : p.DisplayName,
                Kind  = "User",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // ADR 0010: the composer's audience = public groups only (a private
        // group is an organizing unit, never granted as an audience).
        var groups = await userInfo.GetPublicGroupsAsync();
        var groupOptions = groups
            .Select(g => new GrantOption
            {
                Id    = g.Id,
                Label = string.IsNullOrWhiteSpace(g.Name) ? g.Id : g.Name,
                Kind  = "Group",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ViewData["Audience_Users"] = userOptions;
        ViewData["Audience_Groups"] = groupOptions;
    }

    /// <summary>
    /// Seeds the composer's <b>authored-in language</b> picker (ADR 0018,
    /// ADR 0005 B) — the instance's **enabled** <see
    /// cref="Kumunita.Core.Localization.LanguageCatalog"/>, ordered by
    /// <c>SortOrder</c>. Read through <see cref="ILocalizationService.ListLanguagesAsync"/>
    /// (the HTTP-free seam, ADR 0005 D) — the exact catalog read the
    /// <see cref="LocaleController.Index"/> page uses, so this composer
    /// dependency mirrors an established lane rather than re-deriving the
    /// catalog from the store. The composer leaves the selection empty by
    /// default so the *instance default* is what the service materializes
    /// server-side at write time — the picker is the set of choices, not the
    /// choice. Stored on <see cref="PostComposeViewModel.Languages"/>
    /// ([BindNever]).
    /// </summary>
    private async Task<IReadOnlyList<(string Code, string NativeName)>> SeedLanguagePickerAsync()
    {
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        return catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .Select(l => (l.Id, l.NativeName))
            .ToList();
    }

    [HttpGet("/posts/new")]
    public async Task<IActionResult> New()
    {
        // The picker is seeded with the poster-reachable set only: a plain
        // member no longer sees communities they cannot post to; the POST
        // gate in <see cref="PostService.CreatePostAsync"/> remains the
        // authoritative deny (mapped to a form error).
        var components = await AccessibleComponentsAsync(User);

        // The composer's default selection is the *first* enabled, reachable
        // component (a "which community?" — not a "which is my
        // default?"). A "no reachable components" shape is a
        // fail-closed empty form (the §2.3 404 shape, not on the
        // composer — the user can still sign-in / create a component
        // via M1's seeder; the "no enabled components" edge is a
        // bootstrap edge, not a runtime error).
        var first = components.FirstOrDefault();
        var model = new PostComposeViewModel
        {
            Components = components.Select(c => (c.Id, c.Name)).ToList(),
            ComponentId = first is null ? string.Empty : first.Id, // empty when zero reachable components
            Languages = await SeedLanguagePickerAsync(), // ADR 0018 — the authored-in language picker.
            // ADR 0018 — pre-select the instance default so the picker
            // highlights the right option and a no-change submit is a
            // concrete BCP-47 code (never an empty row).
            LanguageCode = await localization.GetDefaultLanguageCodeAsync(),
            // ADR 0036 — the composer's <b>default</b> audience is
            // "all community members": CommunityVisible is seeded
            // <c>true</c> (the community branch allows every member of
            // the target component), grants stay empty (the granular
            // picker is hidden until the poster opts into a narrower
            // audience). Unchecking the box and adding grants is the
            // opt-in to granular restriction — the audience written on
            // <c>POST</c> is still the composer's verbatim choice
            // (ADR 0001-B; the M2
            // <see cref="Kumunita.Web.Controllers.ProfileController"/>
            // "never a second audience object" pin applies verbatim
            // here: exactly one <c>Audience</c> is the writer's
            // shape at the <c>POST</c>).
            Audience = new AudienceEditorModel
            {
                Mode = "Any",
                Grants = "[]",
                CommunityVisible = true,
            },
        };

        // The "Who to grant to" option lists (verified residents +
        // groups) the shared _GrantPickers partial renders — the same
        // single source the M2 Profile/Edit editor uses.
        await SeedGrantPickerOptionsAsync();

        return View(model);
    }

    /// <summary>
    /// The composer's <c>POST</c> (the M3 write lane, the M2
    /// <see cref="Kumunita.Web.Controllers.ProfileController">Edit</see>
    /// POST precedent). Validates the shape (<see
    /// cref="PostComposeViewModel.IsValid"/> — the §2.3 "a guard is a
    /// shape, not a controller-assert" pin; the M3's § U7 "403 on
    /// denied" pin is for the *detail*, not the composer — the
    /// composer's validation is <c>Model.IsValid</c>, not a
    /// runtime-thrown shape), writes through
    /// <see cref="PostService.CreatePostAsync"/> (the
    /// <see cref="IDocumentStore.LightweightSession()"/> lane — the C3
    /// same-transaction shape: the controller opens the session, the
    /// service's <c>SaveChangesAsync</c> is the single write),
    /// redirects to the new post's <c>/posts/{id}</c> (the M2
    /// "redirect after write" precedent).
    /// <para>
    /// **ADR 0001-B / ADR 0006-E lane:** the audience editor's
    /// <see cref="Kumunita.Web.Models.AudienceEditorModel.BuildAudience()"/>
    /// deserializer is the single deserialization site (the M2 single-
    /// source pin); the
    /// <see cref="Kumunita.Core.Posts.PostDraft"/>'s
    /// <see cref="Kumunita.Core.Authorization.Audience"/> is the
    /// composer's <b>verbatim</b> choice (never a second
    /// <c>Audience</c> object, never an auto-augmented shape — the
    /// M3's seam test
    /// <see cref="Kumunita.Core.Posts.PostService"/>
    /// <c>AuthorAudienceWrittenVerbatim</c> pins the DB row's
    /// <c>Audience</c> as bit-identical to the draft's). The component
    /// picker (<c>ComponentId</c>) is a *feed organizer* selection
    /// (C-M3·2: the "which community?" — a <b>filter</b>, never a gate;
    /// a "disabled component" write is a <c>Model.IsValid</c> false
    /// shape at the Web layer, not a Core-thrown shape).
    /// </para>
    /// </summary>
    [HttpPost("/posts/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New([FromForm] PostComposeViewModel model)
    {
        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to post.");
            return View(model);
        }

        // Re-load the *reachable* component set with the same rule as the
        // <c>GET</c>'s picker (single source: <see cref="AccessibleComponentsAsync"/>)
        // — the <c>Model.IsValid</c> guard is a *shape* guard (the component,
        // the body, the audience editor's <c>IsValid</c>); the "is that
        // component enabled and reachable" check is the <b>Web-layer
        // precondition</b> that the <c>Post.Draft</c> write targets a real,
        // admissible component (the §2.3 row 2 shape).
        var components = await AccessibleComponentsAsync(User);
        model.Components = components.Select(c => (c.Id, c.Name)).ToList();
        model.Languages = await SeedLanguagePickerAsync(); // ADR 0018 — re-seed on re-render

        // Re-seed the picker option lists so a failed-shape re-render
        // below still shows the "Who to grant to" options (the
        // unauthenticated guard above renders without them — an
        // acceptable edge: the picker then shows its empty-pool note).
        await SeedGrantPickerOptionsAsync();

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.ComponentId))
                ModelState.AddModelError(nameof(model.ComponentId), "Choose a community.");
            if (string.IsNullOrWhiteSpace(model.Body))
                ModelState.AddModelError(nameof(model.Body), "Body is required.");
            if (model.Audience is null || !model.Audience.IsValid)
                ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            return View(model);
        }

        if (components.All(c => c.Id != model.ComponentId))
        {
            // §2.3 row 2 — the "not in the reachable set" write path (disabled
            // component, or one the actor is not a member of / has no
            // moderator scope on). The shape is a form error, not a 404 (a 404
            // on POST is a non-standard shape; the M2 <see
            // cref="GroupsController"/> "a form is a shape" precedent applies).
            ModelState.AddModelError(nameof(model.ComponentId),
                "That community is not enabled, or you do not have a posting right on it.");
            return View(model);
        }

        // §2.3 row 4 (the <c>Unauthenticated</c> row) is the
        // <see cref="AuthorizeAttribute"/>; §2.3 row 5 (the
        // <c>Authenticated, unverified</c> row) is the same shape
        // as the verified row (M3's §2.3 differs from M2's
        // <b>on purpose</b>: M3's candidate set is "this
        // component's posts," not "the viewer's candidate set" —
        // verification state never gates the feed; the audience
        // decision is the <c>CanSeeAsync</c> shape on the
        // <b>read</b> side, the <b>write</b> side is the
        // author's choice verbatim).
        var draft = new PostDraft(
            ComponentId: model.ComponentId,
            Title: string.IsNullOrWhiteSpace(model.Title) ? null : model.Title,
            Body: model.Body,
            Audience: model.Audience.BuildAudience(),
            LanguageCode: string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode, // ADR 0018 — null/empty ⇒ instance default materialized server-side.
            ImageIds: ContentImageIds.ExtractContentImageIds(model.Body), // RC R·3 — server-side parse of the body's /content-image/{id} links; the client never sends the ids (a form field would be spoofable).
            AttachmentIds: AttachmentIds.ExtractAttachmentIds(model.Body), // ATT U7 (C-ATT·4) — server-side parse of the body's /attachment/{id} links; the client never sends the ids (a form field would be spoofable).
            IsDraft: model.SaveAsDraft, // ADR 0037 — draft mode: saved but invisible to all but the author until published.
            TagSlugs: TagSlugs.Parse(model.TagIds)); // TG (ADR 0044, U8b) — the tag input (client/lib/tag-suggest.ts posts a JSON array of label strings; the server parses + normalizes).

        // C3 same-transaction lane: the controller opens the
        // <c>IDocumentStore.LightweightSession()</c>, the
        // <see cref="PostService.CreatePostAsync"/> service's
        // <c>SaveChangesAsync</c> is the single write. One
        // <c>SaveChangesAsync</c>; the <c>Post</c> document and
        // the in-session <c>AccessAudit</c> row (the write's audit
        // row, the C3 "audit always on" shape) commit or roll back
        // atomically.
        await using var session = store.LightweightSession();

        // Posting-right gate (Core <see cref="PostService.CreatePostAsync"/>):
        // the actor's admissible role set — the same claim-set-as-principal
        // shape the <c>AnnouncementController</c>'s private <c>RoleSet</c>
        // helper hands Core; we share it via <see cref="KumunitaPrincipal
        // .RoleSet"/>. The Core call throws
        // <see cref="UnauthorizedAccessException"/> when the actor is not a
        // GlobalAdmin and has no <c>ComponentMembership</c> row on
        // <c>draft.ComponentId</c> — mapped to a form error here (matching the
        // composer's "a form is a shape" precedent; no 403 on a POST).
        var roles = KumunitaPrincipal.RoleSet(User);
        Post post;
        try
        {
            post = await posts.CreatePostAsync(draft, actor, roles, session);
        }
        catch (UnauthorizedAccessException)
        {
            ModelState.AddModelError(nameof(model.ComponentId),
                "You are not a member of this community yet. An admin can add you under /admin → Accounts.");
            return View(model);
        }
        catch (ArgumentException ex) when (ex.ParamName == "input")
        {
            // TG (ADR 0044, U8b) — a bad tag slug is an ArgumentException
            // from TagService.DeriveSlug (C-TG·4) — mapped to a form error
            // on the TagIds field (the M3 "a form is a shape" precedent).
            // The `when (ex.ParamName == "input")` guard scopes the catch to
            // the DeriveSlug throws (the `nameof(input)` is "input") and
            // lets any other `ArgumentException` (a real bug) propagate.
            ModelState.AddModelError(nameof(model.TagIds), "A tag name is invalid (use letters, digits, hyphens, underscores).");
            return View(model);
        }

        var component = components.First(c => c.Id == model.ComponentId);
        TempData["info"] = $"Post added to “{component.Name}”.";
        return Redirect($"/posts/{post.Id}");
    }

    // ── Edit (GET + POST /posts/{id}/edit) — author-only write lane ─────────

    /// <summary>
    /// <c>GET /posts/{id}/edit</c> — the author-only edit lane's shape. The form
    /// is seeded from the post's current Title/Body/Audience (the
    /// <see cref="Kumunita.Web.Models.AudienceEditorModel.FromAudience"/>
    /// round-trip seeds the audience editor from the post's
    /// <see cref="Kumunita.Core.Authorization.Audience"/> value — the inverse
    /// of the composer's <c>BuildAudience</c> deserialization site, so the
    /// "Who to grant to" picker pre-checks the current grants).
    /// <para>
    /// <b>Authz shape (author-only):</b> the acting user must be the post's
    /// author (<see cref="Post.AuthorId"/> == the signed-in subject). A
    /// non-author never reaches the form — the <c>Detail</c> GET's
    /// <c>CanSeeAsync</c> already ran on the read, but the edit lane's decision
    /// is a *different* one (write-by-authorship, not read-by-audience), so it
    /// is re-checked here as a Web-layer shape gate and then re-pinned
    /// server-side by <see cref="PostService.UpdatePostAsync"/> (defense-in-depth:
    /// the same "form is a shape, service is the gate" split the
    /// <see cref="AnnouncementController"/> edit lane uses). A missing id and a
    /// non-author are both a <c>Forbid()</c> 403 (the M3 U7 "403 on denied, not
    /// a blank page" pin; a 404 would leak which ids are real).
    /// </para>
    /// </summary>
    [HttpGet("/posts/{id}/edit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User) ?? string.Empty;
        await using var session = store.QuerySession();
        var post = await session.LoadAsync<Post>(id);
        if (post is null)
            return Forbid();

        // Author-only shape gate (the service's UpdatePostAsync re-pins this
        // at POST — the Web layer's 403 here is the fail-closed shape, not the
        // decision). A non-author never sees the form.
        if (post.AuthorId != actor)
            return Forbid();

        var model = new PostComposeViewModel
        {
            ComponentId = post.ComponentId,
            Title = post.Title,
            Body = post.Body,
            Audience = AudienceEditorModel.FromAudience(post.Audience),
            // The picker is not editable on the edit lane (the post's feed
            // organizer is immutable after creation — UpdatePostAsync does not
            // touch ComponentId); seed it with the single current component so
            // the read-only display is well-formed.
            Components = await SeedEditableComponentListAsync(post.ComponentId),
            // ADR 0018 (amended 2026-09-13) — the authored-in language tag is
            // editable on this lane: seed the picker from the enabled catalog
            // and pre-select the post's stored tag (the ADR 0017 edit-lane
            // precedent — pre-select the stored value, not the instance default).
            Languages = await SeedLanguagePickerAsync(),
            LanguageCode = post.LanguageCode,
            // ADR 0044 (TG lane) — the tag-suggest input's starting chips (slugs,
            // each removable). Rendered as data-tag-suggest-initial on #tags.
            ExistingTagSlugs = await SeedExistingTagSlugsAsync(post),
        };
        await SeedGrantPickerOptionsAsync();
        return View(model);
    }

    /// <summary>
    /// ADR 0044 (TG lane) — the edit form's pre-seeded tag **slugs** (the
    /// tag-suggest input's starting chips). Slugs are the charset-safe business
    /// key (C-TG·4) — seeding a *display name* (possibly translated, with
    /// accents / uppercase) would throw in the service's <c>DeriveSlug</c> on
    /// re-save, so the seed is the slug, not the display name. The slugs come
    /// from the <see cref="Tag"/> rows the post's <c>TagIds</c> resolve to
    /// (<c>TagIds</c> hold Ids, not slugs); a dangling Id whose <see cref="Tag"/>
    /// row is gone is dropped (the C-TG·1 broken-reference floor — renders as
    /// nothing, not an error). Opens its own short-lived query session so the
    /// same call seeds both the edit GET and the POST re-render paths (the
    /// re-render must re-seed, or a validation-error / bad-slug re-render would
    /// open the tag input with no chips — and under the U8b empty-set-detach
    /// semantics a follow-up submit would then silently drop the author's
    /// intended tags). A missing post is an empty list (fail-closed shape —
    /// the author-only gate is the real deny).
    /// </summary>
    private async Task<IReadOnlyList<string>> SeedExistingTagSlugsAsync(Post? post)
    {
        if (post is null || post.TagIds.Count == 0)
            return [];

        await using var session = store.QuerySession();
        var postTagIds = post.TagIds.ToHashSet(StringComparer.Ordinal);
        var tagDocs = await session.Query<Tag>()
            .Where(t => postTagIds.Contains(t.Id))
            .ToListAsync();
        return tagDocs
            .Select(t => t.Slug)
            .Where(s => !string.IsNullOrEmpty(s))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// <c>POST /posts/{id}/edit</c> — the author-only edit write lane. On
    /// success, redirects to the post's <c>/posts/{id}</c> (the edit is visible
    /// to the viewer immediately). A denied author is a 403 (the
    /// <see cref="PostService.UpdatePostAsync"/>
    /// <c>UnauthorizedAccessException</c> maps to <c>Forbid()</c>; a missing id
    /// is a 404). The audience editor's
    /// <see cref="AudienceEditorModel.BuildAudience"/> deserializer is the
    /// single deserialization site (the M2 single-source pin, carried verbatim
    /// to the edit lane); the post's <see cref="Post.ComponentId"/> is
    /// **immutable** on this lane (the post's feed organizer is a creation-time
    /// choice — a re-targeting edit is out of scope for the author-edit).
    /// </summary>
    [HttpPost("/posts/{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromForm] PostComposeViewModel model)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to edit this post.");
            return Forbid();
        }

        // Load the post up front: the author-only gate + the post's real
        // ComponentId (the component is immutable on this lane — the posted
        // model.ComponentId is pinned to the post's own, so a tampered value
        // cannot re-target the post's feed organizer). A missing id or a
        // non-author is a 403 (the M3 U7 "403 on denied, not a blank page"
        // pin; a 404 would leak which ids are real). The service's
        // UpdatePostAsync re-pins the author-only gate at POST (defense-in-depth).
        await using var probe = store.QuerySession();
        var post = await probe.LoadAsync<Post>(id);
        if (post is null || post.AuthorId != actor)
            return Forbid();

        model.ComponentId = post.ComponentId;
        model.Components = await SeedEditableComponentListAsync(post.ComponentId);
        model.Languages = await SeedLanguagePickerAsync(); // ADR 0018 — re-seed on re-render
        await SeedGrantPickerOptionsAsync();
        // ADR 0044 (TG lane) — the re-render (a validation error, or a bad tag
        // slug below) must re-seed the tag input's chips from the post's
        // current tags: the bound model's ExistingTagSlugs is [BindNever] and
        // therefore [] on POST, so without this the tag input would reopen with
        // no chips — and under the U8b empty-set-detach semantics a follow-up
        // submit would silently drop the author's intended tags. Seeded before
        // both re-render returns (the !ModelState.IsValid path and the
        // ArgumentException bad-slug path), so both show a well-formed state.
        model.ExistingTagSlugs = await SeedExistingTagSlugsAsync(post);

        if (string.IsNullOrWhiteSpace(model.Body))
            ModelState.AddModelError(nameof(model.Body), "Body is required.");
        if (model.Audience is null || !model.Audience.IsValid)
            ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");

        if (!ModelState.IsValid)
            return View(model);

        // The false branch above guarantees model.Audience is non-null and
        // well-formed; bind it to a local so the deserialization call is
        // null-obviously-safe (the single deserialization site, the M2
        // single-source pin).
        var audienceEditor = model.Audience
            ?? new AudienceEditorModel { Mode = "Any", Grants = "[]" };
        var audience = audienceEditor.BuildAudience();

        await using var session = store.LightweightSession();
        try
        {
            await posts.UpdatePostAsync(
                id,
                actor,
                string.IsNullOrWhiteSpace(model.Title) ? null : model.Title,
                model.Body,
                audience,
                string.IsNullOrWhiteSpace(model.LanguageCode) ? null : model.LanguageCode, // ADR 0018 (amended) — the authored-in tag
                session,
                AttachmentIds.ExtractAttachmentIds(model.Body), // ATT U12 (C-ATT·4/8) — the post edit lane re-parses the re-submitted body (replace-style); the image edit lane stays byte-for-byte (C-ATT·9).
                // TG (ADR 0044, U8b register patch) — tri-state tag submit: the bound
                // TagIds field is string? — null means the field was **not posted**
                // (the client/lib/tag-suggest.ts hidden field only exists when the
                // tag module wired it, i.e. JS is on) ⇒ preserve the post's existing
                // tags (a JS-disabled author editing their body must not lose tags);
                // a **present** field (even an empty `[]` when the author removed
                // every chip) is authoritative ⇒ empty detaches all, non-empty
                // attaches (the U8b register patch's detach semantics in
                // PostService.UpdatePostAsync). TagSlugs.Parse still normalizes
                // (trim / dedup / drop-blank) the present case.
                model.TagIds is null ? null : TagSlugs.Parse(model.TagIds));
            TempData["info"] = "Post updated.";
            return Redirect($"/posts/{id}");
        }
        catch (UnauthorizedAccessException)
        {
            // Not the author — the shape is a 403 (a re-render of the form would
            // leak the post's content to a non-author; a 404 would leak which ids
            // are real; the 403 tells the viewer nothing about either).
            return Forbid();
        }
        catch (ArgumentException ex) when (ex.ParamName == "input")
        {
            // TG (ADR 0044, U8b) — a bad tag slug (C-TG·4) → a form error on
            // the TagIds field (the M3 "a form is a shape" precedent).
            ModelState.AddModelError(nameof(model.TagIds), "A tag name is invalid (use letters, digits, hyphens, underscores).");
            return View(model);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Seeds the edit form's <see cref="PostComposeViewModel.Components"/>
    /// picker list with the **single** component the post already belongs to
    /// (the post's <c>ComponentId</c> is immutable on the edit lane — the feed
    /// organizer is a creation-time choice, never a re-targetable edit). Reads
    /// the component's name so the read-only display is well-formed; returns an
    /// empty list when the component is missing/disabled (a fail-closed shape —
    /// the form still renders, the picker is just empty, and the service's
    /// author-only gate is the real deny).
    /// </summary>
    private async Task<IReadOnlyList<(string Id, string Name)>> SeedEditableComponentListAsync(string? componentId)
    {
        if (string.IsNullOrEmpty(componentId))
            return [];

        var components = await userInfo.GetComponentsAsync(enabledOnly: true);
        var component = components.FirstOrDefault(c => c.Id == componentId);
        if (component is null)
            return [];

        return new List<(string Id, string Name)> { (component.Id, component.Name) };
    }

    // ── Reply (POST /posts/{id}/replies) — M3b U6 micro-fix ─────────────────

    /// <summary>
    /// A one-level reply write (M3b deferral item 5; M3 deferral list § U6).
    /// The M3 read path (GET <c>/posts/{id}</c>) already showed the reply form
    /// but left the route 404 — this action closes it. A **thin** Web lane
    /// (ADR 0006-D: routes + authz + shape): it **delegates** the write to the
    /// existing, frozen M3 U6 seam
    /// <see cref="PostService.CreateReplyAsync"/> — **no new Core seam, no new
    /// seam-test name** (the §2.5 test-14 shape/absence anchor).
    /// <para>
    /// <b>Authz shape:</b> the reply inherits the parent's single
    /// <c>Read</c> decision (C-M3·1; <see cref="PostService.CreateReplyAsync"/>
    /// re-checks no access). Before opening a write session we re-run the
    /// parent's <c>Read</c> decision via
    /// <see cref="PostService.GetPostAsync"/> — the same fail-closed shape as
    /// this file's <c>Detail</c> GET: the <see cref="PostDetailResult"/> is
    /// <c>Post = null</c> for **both** "does not exist" and "audience
    /// denied" (Core doesn't distinguish — the audit row does), and this
    /// action maps that to the <c>Forbid()</c> 403 shape (the M3 U7 "403 on
    /// denied, not a blank page" pin; a 404 is information-leaky about
    /// which ids are real). This keeps the reply against an
    /// <b>existing, visible</b> parent without reshaping the Core seam.
    /// </para>
    /// <para>
    /// <b>Session shape (C3 same-transaction lane):</b> the controller opens its
    /// own <see cref="IDocumentStore.LightweightSession()"/> and passes it to
    /// <see cref="PostService.CreateReplyAsync"/> (the M3 U6
    /// <c>IDocumentSession</c> overload). That service method performs the
    /// single <c>SaveChangesAsync</c>, so the <c>PostReply</c> document and any
    /// in-session audit row commit or roll back atomically — the
    /// <see cref="PostService.CreatePostAsync"/> <c>LightweightSession</c>
    /// precedent in this file.
    /// </para>
    /// </summary>
    [HttpPost("/posts/{id}/replies")]
    public async Task<IActionResult> Replies([FromRoute] string id, [FromForm] string? body, [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            // [Authorize] is the primary gate (class level); this is the
            // fail-closed shape in case the principal carries no subject.
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            // A reply is a body-only write (C-M3·1: no own audience, no title).
            // Fail-closed to the detail page — the form is re-presented there.
            TempData["error"] = "A reply needs some text.";
            return Redirect($"/posts/{id}");
        }

        // Authz via the parent's single Read decision (C-M3·1; the reply
        // inherits this, so the decision is the pre-write gate). GetPostAsync
        // returns Post = null for **both** "missing" and "denied" (Core
        // doesn't distinguish; the audit row does), so both map to the
        // Forbid() 403 shape — the M3 U7 "403 on denied, not a blank page"
        // pin and the Detail-GET precedent (a 404 leaks which ids are real).
        var parent = await posts.GetPostAsync(id, actor);
        if (parent.Post is null)
            return Forbid();

        // C3 same-transaction lane: the controller owns the session; the
        // service's SaveChangesAsync is the single write (the M3 U6 precedent
        // in this file — cf. New()'s CreatePostAsync).
        await using var session = store.LightweightSession();
        await posts.CreateReplyAsync(id, actor, body, session, string.IsNullOrWhiteSpace(languageCode) ? null : languageCode, AttachmentIds.ExtractAttachmentIds(body)); // ADR 0018 — the reply's own authored-in tag; null/empty ⇒ instance default. ATT U7 (C-ATT·4/8) — the reply's attachment ids, parsed server-side from the body (the deliberate asymmetry: the image reply lane does not persist ImageIds, C-ATT·9 — untouched).

        TempData["info"] = "Reply added.";
        return Redirect($"/posts/{id}");
    }

    // ── Reply edit (POST /posts/{id}/replies/{replyId}/edit) — author-only ──

    /// <summary>
    /// A one-level reply <b>edit</b> (ADR 0016, author-only):
    /// <c>POST /posts/{id}/replies/{replyId}/edit</c>. A body-only re-write via
    /// <see cref="PostService.UpdateReplyAsync"/> — the service is the decision:
    /// only the reply's own author may edit it (no moderator or GlobalAdmin
    /// branch; a moderator's lever over a reply is the parent post's Hide/Remove,
    /// not re-writing text). Before writing, the parent's single <c>Read</c>
    /// decision is re-run via <see cref="PostService.GetPostAsync"/> (the same
    /// fail-closed shape as <see cref="Replies"/>: <c>Post = null</c> for both
    /// "does not exist" and "audience denied", mapped to the 403 shape) and the
    /// reply must be **under this post** (a replyId on a different post 403s —
    /// the component lane's non-leaky posture). A non-author is the
    /// <see cref="UnauthorizedAccessException"/> wall, mapped to the 403 shape.
    /// The edit stamps <c>PostReply.Modified</c> forward (null until first
    /// edited); <c>PostId</c> / <c>AuthorId</c> / <c>Created</c> are untouched.
    /// One <c>SaveChangesAsync</c> (C3 same-transaction lane).
    /// </summary>
    [HttpPost("/posts/{id}/replies/{replyId}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditReply(
        [FromRoute] string id, [FromRoute] string replyId, [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(replyId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to edit this reply.");
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            // A reply is a body-only write (C-M3·1: no own audience, no title).
            // Fail-closed to the detail page — the form is re-presented there.
            TempData["error"] = "A reply needs some text.";
            return Redirect($"/posts/{id}");
        }

        // The parent's single Read decision (C-M3·1; the reply inherits it) is
        // the pre-write gate: GetPostAsync returns Post = null for **both**
        // "missing" and "denied" (Core doesn't distinguish — the audit row does),
        // so both map to the Forbid() 403 shape — the component lane's non-leaky
        // posture (the Replies / Detail-GET precedent; a 404 leaks which ids are
        // real).
        var parent = await posts.GetPostAsync(id, actor);
        if (parent.Post is null)
            return Forbid();

        // The reply must be **under this post** (a replyId on a different post is
        // not reachable through this lane — the 403 shape, non-leaky).
        if (parent.Replies.All(r => r.Id != replyId))
            return Forbid();

        // C3 same-transaction lane: the controller owns the session; the
        // service's SaveChangesAsync is the single write (the Replies() precedent).
        await using var session = store.LightweightSession();
        try
        {
            await posts.UpdateReplyAsync(replyId, actor, body, session, AttachmentIds.ExtractAttachmentIds(body)); // ATT U7 (C-ATT·4/8) — the reply's re-parsed attachment ids (replace-style, the U4 lane's existing idiom); the image reply lane stays byte-for-byte (C-ATT·9).
        }
        catch (UnauthorizedAccessException)
        {
            // Not the author — the 403 shape (the component lane's non-leaky
            // posture; a re-render would leak the reply's content to a non-author).
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["info"] = "Reply updated.";
        return Redirect($"/posts/{id}");
    }

    // ── Author soft-delete (ADR 0024, component lane — 403 shape) ──────────

    /// <summary>
    /// Author <b>soft-deletes</b> a community post (ADR 0024):
    /// <c>POST /posts/{id}/delete</c>. A thin Web lane (ADR 0006-D) delegating
    /// the write + author-stand decision to
    /// <see cref="PostService.DeletePostAsync"/> — the record is kept
    /// (<see cref="Post.DeletedAt"/> stamped), never hard-deleted. The
    /// component lane's non-leaky posture: a non-author is a 403 (<see
    /// cref="Forbid"/>) and a missing post a 404 (<see cref="NotFound"/>) —
    /// the <see cref="EditReply"/> precedent. The parent's single <c>Read</c>
    /// decision (C-M3·1) is re-run as the pre-write gate (a <c>Post = null</c>
    /// shape is a 403).
    /// </summary>
    [HttpPost("/posts/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePost([FromRoute] string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to delete this post.");
            return Forbid();
        }

        // The parent's single Read decision (C-M3·1) is the pre-write gate —
        // GetPostAsync returns Post = null for **both** "missing" and "denied"
        // (the EditReply precedent), so both map to the 403 shape (non-leaky).
        var parent = await posts.GetPostAsync(id, actor);
        if (parent.Post is null)
            return Forbid();

        await using var session = store.LightweightSession();
        try
        {
            await posts.DeletePostAsync(id, actor, session);
        }
        catch (UnauthorizedAccessException)
        {
            // Non-author — the 403 shape (a re-render would leak the post's
            // content to a non-author).
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["info"] = "Post deleted.";
        return Redirect($"/posts/{id}");
    }

    // ── Publish (POST /posts/{id}/publish) — author-only (ADR 0037) ────────

    /// <summary>
    /// Publish a draft post (ADR 0037): <c>POST /posts/{id}/publish</c>.
    /// <b>Author-only</b> — the sole lever that clears
    /// <see cref="Post.IsDraft"/> is the author's own choice (a non-author,
    /// even a GlobalAdmin, is denied: a draft is invisible to them, so they
    /// have no affordance to reach this; the service re-pins the author gate
    /// server-side). The pre-write gate is the same
    /// <see cref="PostService.GetPostAsync"/> the delete lane uses: a
    /// <c>Post = null</c> (missing <em>or</em> a draft the actor is not the
    /// author of) maps to the 403 shape (non-leaky), and the
    /// <see cref="PostService.PublishPostAsync"/> author gate is the real
    /// decision at POST. A missing id is a 404; on success, redirect back to
    /// the detail page (now live).
    /// </summary>
    [HttpPost("/posts/{id}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish([FromRoute] string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to publish this post.");
            return Forbid();
        }

        // Pre-write gate (the DeletePost precedent): GetPostAsync returns
        // Post = null for **both** "missing" and "draft the actor is not the
        // author of" (the ADR 0037 author-only draft gate) → 403 (non-leaky).
        var parent = await posts.GetPostAsync(id, actor);
        if (parent.Post is null)
            return Forbid();

        await using var session = store.LightweightSession();
        try
        {
            await posts.PublishPostAsync(id, actor, session);
        }
        catch (UnauthorizedAccessException)
        {
            // Non-author — the 403 shape (a re-render would leak the post's
            // content to a non-author; a draft is author-only by construction).
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["info"] = "Post published.";
        return Redirect($"/posts/{id}");
    }

    /// <summary>
    /// Author <b>soft-deletes</b> a community-post reply (ADR 0024):
    /// <c>POST /posts/{id}/replies/{replyId}/delete</c>. The record is kept
    /// (<see cref="PostReply.DeletedAt"/> stamped) — the detail view renders a
    /// placeholder in its place and the reply still counts toward the parent's
    /// count. Same component-lane 403 shape as <see cref="EditReply"/>: the
    /// parent's single <c>Read</c> decision is the pre-write gate, the reply
    /// must be under this post, a non-author is a 403 and a missing reply a
    /// 404.
    /// </summary>
    [HttpPost("/posts/{id}/replies/{replyId}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReply([FromRoute] string id, [FromRoute] string replyId)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(replyId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to delete this reply.");
            return Forbid();
        }

        // The parent's single Read decision (C-M3·1) is the pre-write gate
        // (the EditReply precedent: Post = null → 403, non-leaky).
        var parent = await posts.GetPostAsync(id, actor);
        if (parent.Post is null)
            return Forbid();

        // The reply must be **under this post** (a replyId on a different post
        // is not reachable through this lane — the 403 shape, non-leaky).
        if (parent.Replies.All(r => r.Id != replyId))
            return Forbid();

        await using var session = store.LightweightSession();
        try
        {
            await posts.DeleteReplyAsync(replyId, actor, session);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        TempData["info"] = "Reply deleted.";
        return Redirect($"/posts/{id}");
    }

    // ── Translations (ADR 0022) — user-added post / reply translations ────

    /// <summary>
    /// Adds a **user-added translation** of the post into
    /// <paramref name="languageCode"/> (ADR 0022):
    /// <c>POST /posts/{id}/translations</c>. A thin Web lane (ADR 0006-D:
    /// routes + shape) that delegates the write + standing decision to
    /// <see cref="PostService.AddPostTranslationAsync"/> (the standing
    /// — author / community moderator / GlobalAdmin — is re-pinned
    /// server-side; the detail page's <see cref="PostDetailViewModel
    /// .CanTranslate"/> is only the display affordance).
    /// <para>
    /// <b>Precondition (C-M3·1):</b> the viewer must be able to see the post
    /// (re-run the parent's single <c>Read</c> decision via
    /// <see cref="PostService.GetPostAsync"/> — the exact <c>Replies</c> /
    /// <c>EditReply</c> precedent; a <c>Post = null</c> shape is a 403). A
    /// denied standing actor is a 403 (<see cref="UnauthorizedAccessException"/>
    /// → <c>Forbid()</c>; a re-render would leak content to a non-qualifying
    /// actor); a missing post is a 404.
    /// </para>
    /// <para>
    /// <b>Session shape (C3):</b> the controller owns the
    /// <see cref="IDocumentStore.LightweightSession()"/>; the service's
    /// <c>SaveChangesAsync</c> is the single write — the
    /// <see cref="PostTranslation"/> row and its <c>AccessAudit</c> row commit
    /// atomically.
    /// </para>
    /// </summary>
    [HttpPost("/posts/{id}/translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? title,
        [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/posts/{id}");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return Redirect($"/posts/{id}");
        }

        // C-M3·1 precondition: the viewer must be able to see the post.
        var existing = await posts.GetPostAsync(id, actor);
        if (existing.Post is null)
            return Forbid();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.AddPostTranslationAsync(
                id,
                languageCode,
                string.IsNullOrWhiteSpace(title) ? null : title,
                body,
                actor,
                actorRoles,
                session);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = $"Translation added ({name}).";
        return Redirect($"/posts/{id}");
    }

    /// <summary>
    /// Adds a **user-added translation** of a reply into
    /// <paramref name="languageCode"/> (ADR 0022):
    /// <c>POST /posts/{id}/replies/{replyId}/translations</c>. A thin Web lane
    /// (ADR 0006-D) delegating to <see cref="PostService
    /// .AddReplyTranslationAsync"/> (standing re-pinned server-side). Body-only
    /// (a reply has no title — C-M3·1). Precondition + session shape mirror
    /// <see cref="AddTranslation"/>; a denied standing actor is a 403, a missing
    /// post/reply a 404 / 403 (the component lane's non-leaky posture).
    /// </summary>
    [HttpPost("/posts/{id}/replies/{replyId}/translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddReplyTranslation(
        [FromRoute] string id, [FromRoute] string replyId, [FromForm] string? languageCode, [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(replyId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/posts/{id}");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return Redirect($"/posts/{id}");
        }

        // C-M3·1 precondition + the reply must be under this post (the EditReply
        // precedent — a replyId on a different post is not reachable here).
        var parent = await posts.GetPostAsync(id, actor);
        if (parent.Post is null)
            return Forbid();
        if (parent.Replies.All(r => r.Id != replyId))
            return Forbid();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.AddReplyTranslationAsync(
                replyId,
                languageCode,
                body,
                actor,
                actorRoles,
                session);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = $"Translation added ({name}).";
        return Redirect($"/posts/{id}");
    }

    // ── ADR 0048 — edit + delete lanes for post / reply translations ─────
    // ADR 0022 was add-only; ADR 0048 lifts the "add-only" pin on the same
    // standing matrix (author / community moderator / GlobalAdmin), keyed
    // by the (parentId, languageCode) pair the unique index enforces.

    /// <summary>
    /// **Updates** the existing user-added translation of the post in
    /// <paramref name="languageCode"/> (ADR 0048):
    /// <c>POST /posts/{id}/translations/update</c>. Thin Web lane; delegates
    /// to <see cref="PostService.UpdatePostTranslationAsync"/>. Precondition +
    /// failure shapes mirror <see cref="AddTranslation"/> (denied standing →
    /// 403 <c>Forbid</c>; missing post/row → 404 <c>NotFound</c>).
    /// </summary>
    [HttpPost("/posts/{id}/translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? title,
        [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/posts/{id}");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return Redirect($"/posts/{id}");
        }

        var existing = await posts.GetPostAsync(id, actor);
        if (existing.Post is null)
            return Forbid();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.UpdatePostTranslationAsync(
                id,
                languageCode,
                string.IsNullOrWhiteSpace(title) ? null : title,
                body,
                actor,
                actorRoles,
                session);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = $"Translation updated ({name}).";
        return Redirect($"/posts/{id}");
    }

    /// <summary>
    /// **Removes** the existing user-added translation of the post in
    /// <paramref name="languageCode"/> (ADR 0048):
    /// <c>POST /posts/{id}/translations/remove</c>. Thin Web lane; delegates
    /// to <see cref="PostService.RemovePostTranslationAsync"/>. Failure
    /// shapes mirror <see cref="AddTranslation"/> (denied standing → 403
    /// <c>Forbid</c>; missing post/row → 404 <c>NotFound</c>).
    /// </summary>
    [HttpPost("/posts/{id}/translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/posts/{id}");
        }

        var existing = await posts.GetPostAsync(id, actor);
        if (existing.Post is null)
            return Forbid();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.RemovePostTranslationAsync(id, languageCode, actor, actorRoles, session);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = $"Translation removed ({name}).";
        return Redirect($"/posts/{id}");
    }

    /// <summary>
    /// **Updates** the existing user-added translation of a reply in
    /// <paramref name="languageCode"/> (ADR 0048):
    /// <c>POST /posts/{id}/replies/{replyId}/translations/update</c>. Thin Web
    /// lane; delegates to <see cref="PostService.UpdateReplyTranslationAsync"/>.
    /// Failure shapes mirror <see cref="AddReplyTranslation"/>.
    /// </summary>
    [HttpPost("/posts/{id}/replies/{replyId}/translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReplyTranslation(
        [FromRoute] string id, [FromRoute] string replyId,
        [FromForm] string? languageCode, [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(replyId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/posts/{id}");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return Redirect($"/posts/{id}");
        }

        var parent = await posts.GetPostAsync(id, actor);
        if (parent.Post is null)
            return Forbid();
        if (parent.Replies.All(r => r.Id != replyId))
            return Forbid();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.UpdateReplyTranslationAsync(replyId, languageCode, body, actor, actorRoles, session);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = $"Translation updated ({name}).";
        return Redirect($"/posts/{id}");
    }

    /// <summary>
    /// **Removes** the existing user-added translation of a reply in
    /// <paramref name="languageCode"/> (ADR 0048):
    /// <c>POST /posts/{id}/replies/{replyId}/translations/remove</c>. Thin Web
    /// lane; delegates to <see cref="PostService.RemoveReplyTranslationAsync"/>.
    /// Failure shapes mirror <see cref="AddReplyTranslation"/>.
    /// </summary>
    [HttpPost("/posts/{id}/replies/{replyId}/translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveReplyTranslation(
        [FromRoute] string id, [FromRoute] string replyId, [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(replyId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/posts/{id}");
        }

        var parent = await posts.GetPostAsync(id, actor);
        if (parent.Post is null)
            return Forbid();
        if (parent.Replies.All(r => r.Id != replyId))
            return Forbid();

        var actorRoles = KumunitaPrincipal.RoleSet(User);
        await using var session = store.LightweightSession();
        try
        {
            await posts.RemoveReplyTranslationAsync(replyId, languageCode, actor, actorRoles, session);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = $"Translation removed ({name}).";
        return Redirect($"/posts/{id}");
    }

    /// <summary>
    /// Resolves a BCP-47 code to its catalog <c>NativeName</c> for a
    /// <c>TempData</c> confirmation message (a display convenience — a
    /// <see cref="Kumunita.Core.Localization.ILocalizationService
    /// .ListLanguagesAsync"/> read, not a decision). Falls back to the raw
    /// code when the language is not in the catalog (a never-blank shape).
    /// </summary>
    private async Task<string> SeedLanguageName(string code)
    {
        var catalog = await localization.ListLanguagesAsync();
        return catalog.FirstOrDefault(l => l.Id == code)?.NativeName ?? code;
    }

    // ── Report (POST /posts/{id}/report) — M3b U8 resident-facing intake ────

    /// <summary>
    /// A resident-facing **report intake** action (M3b U8; C-M3b·1, F1). The
    /// M3b deferral item 1 surface: any resident who can *currently see* the
    /// post may file a report against it. This is a Web-layer **thin** lane
    /// (ADR 0006-D: routes + shape) that delegates the write to U4's frozen
    /// <see cref="ModerationService.FileReportAsync"/> — the Core lane makes
    /// **no** <c>IAuthorizationService</c> call (it is an *intake* action, not
    /// an access decision; the C-M3b·1 pin holds verbatim), so this action's
    /// only gate is the pre-write **read decision** on the post itself.
    /// <para>
    /// <b>Authz shape</b> (C-M3b·1): the precondition "the resident can see
    /// the post" is enforced here at the Web layer — re-run the post's single
    /// <c>Read</c> decision via <see cref="PostService.GetPostAsync"/>
    /// (the exact <c>Replies</c> precedent above). A <see
    /// cref="PostDetailResult"/> with <c>Post = null</c> covers **both**
    /// "does not exist" and "audience denied" (Core doesn't distinguish — the
    /// audit row does), and this action maps that to the <c>Forbid()</c> 403
    /// shape (the M3 U7 "403 on denied, not a blank page" pin; a 404 is
    /// information-leaky about which ids are real). After this gate the post is
    /// confirmed *existing and visible*, so the lane cannot
    /// <see cref="KeyNotFoundException"/> on a missing post.
    /// </para>
    /// <para>
    /// <b>Session shape (C3 / ADR 0006-C):</b> the controller opens its own
    /// <see cref="IDocumentStore.LightweightSession"/> and passes it to
    /// <see cref="ModerationService.FileReportAsync"/>; that service performs
    /// the single <c>SaveChangesAsync</c>, so the <c>Report</c> row and the
    /// filing <c>AccessAudit</c> row (the pinned filing tag
    /// <c>AccessVia.Admin</c>) commit or roll back atomically — no partial
    /// write, one <c>SaveChangesAsync</c> (the <c>New()</c> / <c>Replies</c>
    /// session precedent in this file).
    /// </para>
    /// </summary>
    [HttpPost("/posts/{id}/report")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("report")]
    public async Task<IActionResult> Report([FromRoute] string id, [FromForm] string? reason)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
        {
            // [Authorize] is the primary gate (class level); this is the
            // fail-closed shape in case the principal carries no subject.
            return Forbid();
        }

        // C-M3b·1 precondition: the resident must be able to *see* the post.
        // GetPostAsync returns Post = null for **both** "missing" and "denied"
        // (Core doesn't distinguish; the audit row does), so both map to the
        // Forbid() 403 shape — the same fail-closed gate the Replies lane uses.
        var existing = await posts.GetPostAsync(id, actor);
        if (existing.Post is null)
            return Forbid();

        // C3 same-transaction lane: the controller owns the session; the
        // service's SaveChangesAsync is the single write (the New()/Replies()
        // precedent). The "reason" is optional — FileReportAsync accepts null.
        await using var session = store.LightweightSession();
        await moderation.FileReportAsync(id, actor, reason, session);

        TempData["info"] = "Report submitted. A moderator may review it.";
        return Redirect($"/posts/{id}");
    }

    // ── Reply report (POST /posts/{id}/replies/{replyId}/report) ──────────
    // ADR 0023 — the reply-report-target lane. The Web-layer gate is
    // identical to the post-report lane: C-M3·1 "reply-inherits" says a
    // reply has no own audience / no own audit row — its visibility is the
    // parent post's single Read decision. So "can see the post" is exactly
    // the precondition to be able to see (and therefore report) its reply.

    /// <summary>
    /// A resident-facing **reply-report intake** action (ADR 0023). The
    /// Web-layer shape mirrors <see cref="Report"/> exactly — the only
    /// difference is the write target: it delegates to
    /// <see cref="ModerationService.FileReplyReportAsync"/> (the Core lane
    /// loads the reply + its parent post, sets
    /// <see cref="Kumunita.Core.Posts.Report.ReplyId"/> on the
    /// <see cref="Kumunita.Core.Posts.Report"/> row, and writes the filing
    /// audit row with the pinned <c>AccessVia.Admin</c> tag).
    /// <para>
    /// <b>Authz shape (C-M3·1 reply-inherits):</b> the precondition "the
    /// resident can see the post" is the same
    /// <see cref="PostService.GetPostAsync"/> single-Read-decision gate
    /// <see cref="Report"/> uses — a reply has no own audience, so seeing
    /// the post is what makes its reply reportable. A
    /// <see cref="PostDetailResult"/> with <c>Post = null</c> covers both
    /// "does not exist" and "audience denied" and maps to the
    /// <c>Forbid()</c> 403 shape (the same fail-closed pin).
    /// </para>
    /// <para>
    /// <b>Session shape (C3 / ADR 0006-C):</b> the controller opens its own
    /// <see cref="IDocumentStore.LightweightSession"/> and passes it to
    /// <see cref="ModerationService.FileReplyReportAsync"/>; that service
    /// performs the single <c>SaveChangesAsync</c>, so the
    /// <c>Report</c> row and the filing <c>AccessAudit</c> row commit or
    /// roll back atomically — no partial write, one <c>SaveChangesAsync</c>.
    /// </para>
    /// </summary>
    [HttpPost("/posts/{id}/replies/{replyId}/report")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportReply(
        [FromRoute] string id,
        [FromRoute] string replyId,
        [FromForm] string? reason)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();
        if (string.IsNullOrEmpty(replyId))
            return NotFound();

        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return Forbid();

        // C-M3·1 reply-inherits: the resident must be able to *see* the
        // parent post. GetPostAsync returns Post = null for **both**
        // "missing" and "denied" (Core doesn't distinguish; the audit row
        // does), so both map to the Forbid() 403 shape — the same
        // fail-closed gate the Report lane uses.
        var existing = await posts.GetPostAsync(id, actor);
        if (existing.Post is null)
            return Forbid();

        // The reply's PostId must match the route's post id — a reply
        // belonging to a different post is not reportable under this
        // route (defensive; the Core lane would load it by replyId alone
        // and could file a report whose PostId disagrees with the route).
        await using var session = store.LightweightSession();
        var reply = await session.LoadAsync<Kumunita.Core.Posts.PostReply>(replyId).ConfigureAwait(false);
        if (reply is null || reply.PostId != id)
            return NotFound();

        await moderation.FileReplyReportAsync(replyId, actor, reason, session);

        TempData["info"] = "Report submitted. A moderator may review it.";
        return Redirect($"/posts/{id}");
    }

    }
