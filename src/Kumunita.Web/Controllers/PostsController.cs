using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    IUserInfoService userInfo,
    IDocumentStore store) : Controller
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
    public async Task<IActionResult> Index([FromRoute] string componentId)
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

        var feed = await posts.ListFeedAsync(componentId, actor, page: 1);

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
            const int previewLength = 200;
            var preview = post.Body.Length <= previewLength
                ? post.Body
                : post.Body[..previewLength].TrimEnd() + "…";
            items.Add(new PostListItem(
                post.Id,
                post.Title,
                preview,
                post.Created,
                profile?.DisplayName ?? post.AuthorId));
        }

        return View(new FeedViewModel
        {
            ComponentId = componentId,
            ComponentName = component.Name,
            Items = items,
            Total = feed.Total,
        });
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
        var replyItems = new List<ReplyItem>(result.Replies.Count);
        foreach (var reply in result.Replies)
        {
            var replyAuthorProfile = await userInfo.GetProfileAsync(reply.AuthorId);
            replyItems.Add(new ReplyItem(
                reply.Id,
                replyAuthorProfile?.DisplayName ?? reply.AuthorId,
                reply.Body,
                reply.Created));
        }

        return View(new PostDetailViewModel
        {
            Post = result.Post,
            AuthorDisplayName = authorProfile?.DisplayName ?? result.Post.AuthorId,
            Replies = replyItems,
            IsAuthor = result.Post.AuthorId == actor,
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
    /// GET's "missing <c>Profile</c> row ⇒ empty editor" precedent).
    /// </summary>
    [HttpGet("/posts/new")]
    public async Task<IActionResult> New()
    {
        var components = await userInfo.GetComponentsAsync(enabledOnly: true);

        // The composer's default selection is the *first* enabled
        // component (a "which community?" — not a "which is my
        // default?"). A "no enabled components" shape is a
        // fail-closed empty form (the §2.3 404 shape, not on the
        // composer — the user can still sign-in / create a component
        // via M1's seeder; the "no enabled components" edge is a
        // bootstrap edge, not a runtime error).
        var model = new PostComposeViewModel
        {
            Components = components.Select(c => (c.Id, c.Name)).ToList(),
            ComponentId = components.FirstOrDefault()!.Id, // empty string when zero components
            // ADR 0001-B — the composer's choice is absolute: the
            // editor's <b>default</b> shape is the *bootstrap* self-only
            // audience (invariant C1: an empty audience is the
            // deny-by-default posture; the owner branch is the only
            // way the author sees their own draft). Mode Any + Grants []
            // is the empty-audience shape; the user's grant addition,
            // or mode switch to <c>All</c> + non-empty grants, is what
            // changes the audience (never an M3 auto-augmentation of
            // the author's choice — the M2
            // <see cref="Kumunita.Web.Controllers.ProfileController"/>
            // "never a second audience object" pin applies verbatim
            // here: exactly one <c>Audience</c> is the writer's
            // shape at the <c>POST</c>).
            Audience = new AudienceEditorModel
            {
                Mode = "Any",
                Grants = "[]",
            },
        };

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

        // Re-load the enabled component set (same shape as the
        // <c>GET</c>'s picker) — the <c>Model.IsValid</c> guard is a
        // *shape* guard (the component, the body, the audience
        // editor's <c>IsValid</c>); the "is that component present-
        // enabled" check is the <b>Web-layer precondition</b> that
        // the <c>Post.Draft</c> write targets a real component (the
        // §2.3 row 2 shape; a write to a disabled component is the
        // same class of bug as a write to a missing component — the
        // Web-layer pin).
        var components = await userInfo.GetComponentsAsync(enabledOnly: true);
        model.Components = components.Select(c => (c.Id, c.Name)).ToList();

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
            // §2.3 row 2 — the "disabled component" write path (the
            // "present" case was covered above; this is the "no
            // match" branch). The shape is a form error, not a
            // 404 (a 404 on POST is a non-standard shape; the M2
            // <see cref="GroupsController"/> "a form is a shape"
            // precedent applies).
            ModelState.AddModelError(nameof(model.ComponentId),
                "That community is not enabled.");
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
            Audience: model.Audience.BuildAudience());

        // C3 same-transaction lane: the controller opens the
        // <c>IDocumentStore.LightweightSession()</c>, the
        // <see cref="PostService.CreatePostAsync"/> service's
        // <c>SaveChangesAsync</c> is the single write. One
        // <c>SaveChangesAsync</c>; the <c>Post</c> document and
        // the in-session <c>AccessAudit</c> row (the write's audit
        // row, the C3 "audit always on" shape) commit or roll back
        // atomically.
        await using var session = store.LightweightSession();
        var post = await posts.CreatePostAsync(draft, actor, session);

        var component = components.First(c => c.Id == model.ComponentId);
        TempData["info"] = $"Post added to “{component.Name}”.";
        return Redirect($"/posts/{post.Id}");
    }

    }
