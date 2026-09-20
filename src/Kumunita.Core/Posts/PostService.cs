using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Posts;

/// <summary>
/// The posts-side composition service (M3, plan U6; bounded context
/// <c>Kumunita.Core.Posts</c>, ADR 0006-D lane). The M3 analog of M2's
/// <see cref="DirectoryService"/>. A pure caller of the two frozen modules —
/// <see cref="IUserInfoService"/> read seams and <see cref="IAuthorizationService"/>
/// (the single decision path) — plus its own <see cref="IDocumentStore"/> for the
/// read/write lanes; it never reads <c>GroupMembership</c>/<c>DelegationGrant</c>
/// for its own access decisions (the same "feature modules never re-derive
/// access" ADR 0006-D boundary that pins M1/M2). Owns M3's two product rules:
/// the §2.3 candidate filter (C-M3·2 — the component is a *feed organizer /
/// candidate filter*, never an access decision) and the §2.4 reply-inherits rule
/// (C-M3·1 — a <see cref="PostReply"/> has **no** own authorization evaluation;
/// its visibility inherits the parent post's single <c>Read</c> decision).
/// <para>
/// Session shape (invariant C3 — same transaction): reads open their own
/// <c>QuerySession</c> (mirroring M2's read lane; the standalone
/// <c>IAuthorizationService</c> overloads commit their own aggregate /
/// decision audit row); **writes go through the caller's session** (the
/// <c>IDocumentSession</c> overloads on <see cref="CreatePostAsync"/> /
/// <see cref="CreateReplyAsync"/> — one <c>SaveChangesAsync</c>, so the domain
/// write and any in-session audit row commit or roll back atomically). ADR 0006-D
/// keeps this concrete (it composes two *seams*, not itself a seam).
/// </para>
/// </summary>
public sealed class PostService
{
    private static readonly int PageSize = 30;

    private readonly IUserInfoService _userInfo;
    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;
    // TG (ADR 0044, U8b register patch) — the tag-lane write seam. **Optional**
    // (nullable default) so the 7 existing test call sites that construct
    // `PostService` positionally (userInfo, authz, store) keep compiling
    // unchanged (the §2.6 "new dependency on PostService, not a new seam on a
    // frozen interface" line — the ADR 0006-D lane pin preserved; the
    // `PostService_MakesNoNewModerateCall` test's surface-level reflection
    // assertion is over `TagService`'s ctor, not `PostService`'s, so adding
    // this parameter is a no-op for that pin). The DI registration passes the
    // live `ITagService`; tests that exercise the tag path construct it
    // explicitly.
    private readonly ITagService? _tags;

    public PostService(IUserInfoService userInfo, IAuthorizationService authz, IDocumentStore store,
        ITagService? tags = null)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _tags = tags;
    }

    /// <summary>
    /// The community feed for <paramref name="componentId"/> (F1/F2/F8/F9, §2.3):
    /// the candidate set is the component's posts — a **candidate filter, never a
    /// gate** (C-M3·2: this filter is not an access decision, not an
    /// <see cref="AccessAudit"/> subject; the Web layer already 404'd a missing /
    /// disabled component via <see cref="IUserInfoService.GetComponentsAsync"/>,
    /// and unauthenticated never reaches Core). One
    /// <see cref="IAuthorizationService.CanSeeAsync(string, AccessAction, IEnumerable{IAuditableResource})"/>
    /// over the paged candidate set (C6's one shared matching pass) writes the visit's
    /// **single aggregate** <see cref="AccessAudit"/> row (C-M3·3;
    /// <c>TargetKind = "post"</c> via the <see cref="PostToAuditableResource"/>
    /// adapter). <see cref="FeedResult.HiddenCount"/> counts only the candidates
    /// that call evaluated.
    /// </summary>
    public async Task<FeedResult> ListFeedAsync(string componentId, string actorId, int page)
    {
        if (string.IsNullOrEmpty(componentId)) throw new ArgumentException("A component feed requires a componentId.", nameof(componentId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        var candidates = await session
            .Query<Post>()
            .Where(p => p.ComponentId == componentId && p.DeletedAt == null && !p.IsDraft)
            .OrderByDescending(p => p.Created)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync()
            .ConfigureAwait(false);

        if (candidates.Count == 0)
            return new FeedResult(Visible: Array.Empty<Post>(), HiddenCount: 0, Page: page, Total: 0);

        // C6 — one shared matching pass over the whole candidate set; C3 — one aggregate
        // audit row (VisibleCount/HiddenCount), TargetKind "post" (C-M3·3), from that
        // single call. Standalone form (no IDocumentSession overload): this is a plain
        // read with no in-flight caller transaction (M2's ListAsync precedent), so the
        // standalone method's own commit is the correct C3 lane.
        var visibleSet = await _authz.CanSeeAsync(
                actorId, AccessAction.Read,
                candidates.Select(p => new PostToAuditableResource(p)))
            .ConfigureAwait(false);

        // F1/F2: return only the source documents whose id the visible set surfaced —
        // never a hidden post's fields.
        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        var visible = candidates.Where(p => visibleIds.Contains(p.Id)).ToList();

        return new FeedResult(Visible: visible, HiddenCount: visibleSet.HiddenCount, Page: page, Total: visible.Count);
    }

    /// <summary>
    /// The **all-sections** community feed (M3b "feed organizer" extension):
    /// the same shape as <see cref="ListFeedAsync"/> (the candidate filter —
    /// a *feed organizer*, never an access decision — C-M3·2) but the candidate
    /// set is the union of the actor-visible posts across every <see cref="IReadOnlyCollection{T} componentIds"/>
    /// that the Web layer resolved as <c>Enabled</c> (the same
    /// <see cref="IUserInfoService.GetComponentsAsync(bool)"/> candidate set the
    /// single-component feed and the composer both already use).
    /// <para>
    /// <b>Auditing pin (C-M3·3):</b> exactly **one**
    /// <see cref="IAuthorizationService.CanSeeAsync(string, AccessAction, IEnumerable{IAuditableResource})"/>
    /// over the whole candidate set — so the all-sections feed emits a
    /// <b>single</b> aggregate <see cref="AccessAudit"/> row per visit
    /// (invariant C3, C-M3·3 — same lane as <see cref="ListFeedAsync"/>) and
    /// never a per-component row (a per-component call would leak "which
    /// components hold content" via the audit lane). The
    /// <see cref="FeedResult.HiddenCount"/> counts only candidates this call
    /// evaluated; a post in a <c>Disabled</c> or missing component is not in
    /// the candidate set (the §2.3 precondition shape).
    /// </para>
    /// <param name="componentIds">The enabled component ids this feed spans.
    /// Empty ⇒ empty candidate set (no audit row — the §2.3 404 shape, not a
    /// Web-layer 0-candidate edge).</param>
    /// </summary>
    public async Task<FeedResult> ListAllFeedAsync(
            IReadOnlyCollection<string> componentIds, string actorId, int page)
    {
        if (componentIds is null) throw new ArgumentNullException(nameof(componentIds));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));
        if (page < 1) page = 1;

        if (componentIds.Count == 0)
            return new FeedResult(Visible: Array.Empty<Post>(), HiddenCount: 0, Page: page, Total: 0);

        await using var session = _store.QuerySession();
        var candidates = await session
            .Query<Post>()
            .Where(p => componentIds.Contains(p.ComponentId) && p.DeletedAt == null && !p.IsDraft)
            .OrderByDescending(p => p.Created)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync()
            .ConfigureAwait(false);

        if (candidates.Count == 0)
            return new FeedResult(Visible: Array.Empty<Post>(), HiddenCount: 0, Page: page, Total: 0);

        // C-M3·3 — one shared matching pass over the whole candidate set
        // (across all enabled components), one aggregate audit row (the
        // F1/F2 shape, the same "candidate filter is not a gate" pin as
        // ListFeedAsync's single-section lane).
        var visibleSet = await _authz.CanSeeAsync(
                actorId, AccessAction.Read,
                candidates.Select(p => new PostToAuditableResource(p)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        var visible = candidates.Where(p => visibleIds.Contains(p.Id)).ToList();

        return new FeedResult(Visible: visible, HiddenCount: visibleSet.HiddenCount, Page: page, Total: visible.Count);
    }

    /// <summary>
    /// A post's detail + its one-level replies (F10, §2.4):
    /// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// — the post's **single decision row** (C-M3·3, not an aggregate). **No second
    /// <c>CanSeeAsync</c> on the replies** (C-M3·1): the replies are loaded and
    /// returned *as-is* — they carry no <c>Audience</c> of their own and are
    /// rendered iff the parent's <c>Read</c> decision is Allow; parent Deny ⇒
    /// replies **not evaluated**, no reply audit row. A missing post is fail-closed
    /// (no decision ran, no audit row — the M2 detail shape); a Deny returns
    /// <c>Post = null</c> with **no** replies (the decision's row <i>was</i>
    /// written, C3) for the Web layer's 403.
    /// </summary>
    public async Task<PostDetailResult> GetPostAsync(string postId, string actorId)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));

        await using var session = _store.QuerySession();
        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            return new PostDetailResult(Post: null, Replies: Array.Empty<PostReply>());

        // ADR 0037 — draft gate (author-only, no audit row): a draft is
        // invisible to everyone except its author. The authorization algorithm
        // is NOT consulted — no CanAsync, no AccessAudit row, no owner branch,
        // no audience evaluation. The sole decision is a pure
        // AuthorId == actorId ordinal check. A non-author is denied (the Web
        // layer maps to 403); the author sees the draft with its replies
        // (C-M3·1 shape: replies returned as-is, no second evaluation).
        if (post.IsDraft)
        {
            if (!string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
                return new PostDetailResult(Post: null, Replies: Array.Empty<PostReply>());

            var authorReplies = await session
                .Query<PostReply>()
                .Where(r => r.PostId == postId)
                .OrderBy(r => r.Created)
                .ToListAsync()
                .ConfigureAwait(false);

            return new PostDetailResult(Post: post, Replies: authorReplies);
        }

        // C3 — one decision row from this single call; C6 — one matching pass.
        var decision = await _authz.CanAsync(actorId, AccessAction.Read, new PostToAuditableResource(post)).ConfigureAwait(false);

        if (!decision.Allowed)
            // C-M3·1 / F10 — a Deny short-circuits at the parent: replies not
            // evaluated, not loaded for rendering, no reply row at all.
            return new PostDetailResult(Post: null, Replies: Array.Empty<PostReply>());

        // C-M3·1 — the reply list is returned **as-is** under the parent's single
        // decision: no second authorization evaluation, no per-reply audit row.
        var replies = await session
            .Query<PostReply>()
            .Where(r => r.PostId == postId)
            .OrderBy(r => r.Created)
            .ToListAsync()
            .ConfigureAwait(false);

        return new PostDetailResult(Post: post, Replies: replies);
    }

    /// <summary>
    /// Creates a post in the **caller's** in-flight session (invariant C3 — the
    /// same-transaction lane, ADR 0006-E <c>IDocumentSession</c> overloads;
    /// <see cref="IDocumentSession"/> is the caller's, so the service never opens
    /// its own session for writes — mirrors M2's write-lane shape). The author's
    /// chosen <see cref="Audience"/> is written **verbatim** (ADR 0001-B — the
    /// composer's choice is absolute; <c>PostDraft.Audience</c> is non-null, C1);
    /// <c>AuthorId = actorId</c>, <c>ComponentId = draft.ComponentId</c>. One
    /// <c>SaveChangesAsync</c>.
    /// <para>
    /// <b>Community-membership gate (posting right, see
    /// <see cref="UserInfo.ComponentMembership"/>):</b> <paramref name="actorRoles"/>
    /// is the caller's admissible role set (the same <c>IReadOnlySet&lt;string&gt;</c>
    /// author-roles seam <see cref="Announcements.AnnouncementService.CreateAsync"/>
    /// takes — the claim-set-as-principal, not a DB read). A post is
    /// **permitted** iff the actor is a <see cref="Identity.Roles.GlobalAdmin"/>
    /// (bypass) <b>OR</b> a <c>ComponentMembership</c> row exists for
    /// <c>(draft.ComponentId, actorId)</c>. Otherwise a
    /// <see cref="UnauthorizedAccessException"/> is thrown <b>before</b> anything
    /// is stored — the audit row is the caller's job; the service does not open a
    /// second session and does not append an audit row in the denial path
    /// (<see cref="AccessVia"/> does not model "posting membership" — the Web
    /// layer maps the exception to a form error, matching the existing
    /// <see cref="Announcements.AnnouncementService.CreateAsync"/> lane's contract).
    /// Strong consistency (invariant C4): the gate re-evaluates on the live
    /// membership row at the moment of the call — an admin's remove is visible on
    /// the very next create attempt.
    /// </para>
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The actor is not a
    /// <see cref="Identity.Roles.GlobalAdmin"/> and has no
    /// <c>ComponentMembership</c> row for <c>draft.ComponentId</c>.</exception>
    public async Task<Post> CreatePostAsync(PostDraft draft, string actorId,
        IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An authoring actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        // Posting-right gate — GlobalAdmin bypass; a Moderator with an
        // assignment on the target component (the admin-set "scope" row,
        // ADR 0003) also posts; everyone else needs a
        // <c>ComponentMembership</c> row on the target. Strong consistency:
        // all three checks read live rows now, no projection lag
        // (invariant C4).
        var hasGlobalAdmin = actorRoles.Contains(Identity.Roles.GlobalAdmin);
        var moderatorComponentClaim = Identity.Roles.ModeratorComponent(draft.ComponentId);
        var hasComponentModerator = actorRoles.Contains(moderatorComponentClaim);

        if (!hasGlobalAdmin && !hasComponentModerator)
        {
            var communities = await _userInfo
                .GetCommunityIdsAsync(actorId)
                .ConfigureAwait(false);
            if (communities is null || !communities.Contains(draft.ComponentId))
            {
                throw new UnauthorizedAccessException(
                    $"You are not a member of the community this post targets; " +
                    "an admin must add you to the Community Membership in /admin.");
            }
        }

        var post = new Post
        {
            Id = Guid.NewGuid().ToString("N"),
            ComponentId = draft.ComponentId,
            AuthorId = actorId,
            Title = draft.Title,
            Body = draft.Body,
            Audience = draft.Audience, // ADR 0001-B — written verbatim; never mutated here.
            ImageIds = draft.ImageIds ?? [], // RC R·3/R·7 (ADR 0025) — populated server-side by the Web layer; null-coalesce to the POCO's non-null empty list.
            AttachmentIds = draft.AttachmentIds ?? [], // ATT U4 (C-ATT·4) — populated server-side by the Web layer (AttachmentIds.ExtractAttachmentIds); null-coalesce to the POCO's non-null empty list.
            LanguageCode = await ResolveLanguageCodeAsync(draft.LanguageCode, session).ConfigureAwait(false), // ADR 0018
            IsDraft = draft.IsDraft ?? false, // ADR 0037 — draft mode: saved but invisible to all but the author.
            Created = DateTimeOffset.UtcNow
        };

        session.Store(post);
        await session.SaveChangesAsync().ConfigureAwait(false);

        // TG (ADR 0044, U8b) — the tag attach lane (C3 single-transaction
        // idiom: the write + the audit row commit atomically on the **same**
        // <c>IDocumentSession</c> — the <c>AddPostTranslationAsync</c>
        // precedent). Only if the form gave slugs (null/empty ⇒ no tags, the
        // U4 additive default-empty pin). The <c>AttachToPostAsync</c> lane
        // re-checks the standing (author ∪ GlobalAdmin, C-TG·5) and
        // create-or-reuses each tag (C-TG·4); it stores the resolved
        // <c>Tag</c> ids onto <c>Post.TagIds</c> (replacing the POCO's
        // default-empty list). A bad <c>Slug</c> is an
        // <c>ArgumentException</c> from <c>DeriveSlug</c> (C-TG·4) — the
        // Web layer maps it to a form error (the M3 "a form is a shape"
        // precedent).
        if (draft.TagSlugs is { Count: > 0 } && _tags is not null)
        {
            var resolved = await _tags.AttachToPostAsync(post.Id, draft.TagSlugs, actorId, actorRoles, session)
                .ConfigureAwait(false);
            // U8b — persist the resolved TagIds onto the post (the attach
            // lane's own SaveChangesAsync does not reliably carry the loaded
            // post's TagIds mutation to the DB; re-store + save here).
            post.TagIds = resolved.Select(t => t.Id).ToList();
            session.Store(post);
            await session.SaveChangesAsync().ConfigureAwait(false);
        }

        return post;
    }

    /// <summary>
    /// ADR 0018 — resolves the authored-in <c>LanguageCode</c> for a new
    /// post/reply: a non-empty authored code is used verbatim (BCP-47 tag,
    /// ADR 0005 B); a null/empty code is materialized from the instance
    /// default (<see cref="LocaleSettings.DefaultLanguageCode"/>, loaded from
    /// the caller's in-flight session so no second round-trip is needed when
    /// it is already loaded) with <c>en</c> as the floor when the singleton
    /// row is absent. The result is **always** a concrete BCP-47 code — no
    /// stored row is left empty — which is what a future search surface keys
    /// off. This is a tag, not a translation (ADR 0005 C unchanged).
    /// </summary>
    private async Task<string> ResolveLanguageCodeAsync(string? languageCode, IDocumentSession session)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
            return languageCode;

        var settings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, CancellationToken.None).ConfigureAwait(false);
        if (settings is not null && !string.IsNullOrWhiteSpace(settings.DefaultLanguageCode))
            return settings.DefaultLanguageCode;

        return "en";
    }

    /// <summary>
    /// Edits a post in the **caller's** in-flight session (invariant C3 — the
    /// same-transaction lane, mirroring <see cref="CreatePostAsync"/>'s write
    /// shape). **Author-only**: the acting actor must be the post's author
    /// (<see cref="Post.AuthorId"/> == <paramref name="actorId"/>); any other
    /// actor is a hard <see cref="UnauthorizedAccessException"/> (the Web layer
    /// maps that to a 403). This is the whole decision — there is no
    /// moderator / admin edit branch on a post (a post's content belongs to
    /// its author; a moderator's lever over a post is the
    /// <see cref="HidePostAsync"/> / <see cref="RemovePostAsync"/> lane, not a
    /// re-write of its text).
    /// <para>
    /// Applies the edit fields (<paramref name="title"/> /
    /// <paramref name="body"/> / <paramref name="audience"/> /
    /// <paramref name="languageCode"/>) and stamps
    /// <see cref="Post.Modified"/> — the <c>AuthorId</c>,
    /// <c>ComponentId</c>, <c>Created</c>, <see cref="PostStatus"/>,
    /// <see cref="Post.GroupId"/> fields are deliberately
    /// **not** touched (a post's authoring identity, feed organizer, and
    /// moderation state are immutable after creation). The
    /// <paramref name="audience"/> is written **verbatim** (ADR 0001-B) — the
    /// author re-chooses the audience on edit exactly as on create; there is
    /// no auto-augmentation. A missing id is a <see cref="KeyNotFoundException"/>
    /// (the Web layer maps that to a 404); a non-author is the
    /// <see cref="UnauthorizedAccessException"/> above.
    /// <para>
    /// <see cref="Post.LanguageCode"/> (ADR 0018) **is** re-resolved by this
    /// lane (ADR 0014, as amended): an author who wrote the post in the wrong
    /// language can correct it after publishing — the
    /// <paramref name="languageCode"/> is materialized through the shared
    /// <see cref="ResolveLanguageCodeAsync"/> helper, so a non-empty code is
    /// written verbatim and a blank submission falls back to the instance
    /// default (never blanking a stored tag). This mirrors the ADR 0017
    /// announcement edit lane, where the tag was already editable.
    /// </para>
    /// <para>
    /// <paramref name="attachmentIds"/> (ATT U12, C-ATT·4) carries the post's
    /// file-attachment references — the Web layer re-parses the (re-submitted)
    /// body's <c>/attachment/{id}</c> links (via the Web-only
    /// <c>Kumunita.Web.Security.AttachmentIds.ExtractAttachmentIds</c>) and
    /// passes them in; Core writes them verbatim, replace-style (the
    /// <c>body = body ?? string.Empty</c> idiom: the re-parse of the
    /// re-submitted body is authoritative, so the list is replaced wholesale).
    /// <b>Deliberate asymmetry (recorded, C-ATT·8/9):</b> the image lane's
    /// post/group-post edit lanes do <b>not</b> set
    /// <see cref="Post.ImageIds"/> (the image-lane "create only, not edit"
    /// precedent, C-ATT·9); <b>this</b> lane persists
    /// <see cref="Post.AttachmentIds"/> because the attachment serve route
    /// (C-ATT·2) must find the post row owning the id. Optional trailing
    /// parameter (nullable, the CS1736 shape) — the existing controller call
    /// sites keep compiling unchanged.
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The post id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the post's author.</exception>
    public async Task<Post> UpdatePostAsync(
        string postId,
        string actorId,
        string? title,
        string body,
        Authorization.Audience audience,
        string? languageCode,
        IDocumentSession session,
        IReadOnlyList<string>? attachmentIds = null,
        // TG (ADR 0044, U8b) — the tag slugs (the author's typed labels, C-TG·4).
        // Optional trailing parameter (nullable, the CS1736 shape) — existing
        // call sites keep compiling unchanged (they omit it ⇒ null ⇒ **no
        // change** to the post's existing tags). The <c>AttachToPostAsync</c>
        // lane create-or-reuses each tag (C-TG·4) and stores the resolved
        // <c>Tag</c> ids onto <c>Post.TagIds</c>.
        //
        // Detach semantics (U8b register patch): the tri-state is
        //   · null ⇒ preserve the post's existing <c>TagIds</c> (the caller
        //     carried no tag field — the 7 existing Core call sites that
        //     omit this parameter keep their prior no-op behavior);
        //   · an empty (non-null) list ⇒ detach all (clear <c>Post.TagIds</c>);
        //   · a non-empty list ⇒ attach the given set (create-or-reuse each,
        //     store the resolved ids).
        // This makes the edit lane's "editable" surface fully round-trip-
        // capable: the composer's chips-with-remove affordance
        // (client/lib/tag-suggest.ts) posts <c>[]</c> when the author has
        // removed every chip, and that now clears the post's tags rather than
        // silently preserving them.
        IReadOnlyList<string>? tagSlugs = null)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting author is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{postId}' was not found in the session; nothing to edit.");

        // Author-only gate (the sole decision on this lane): only the author may edit.
        if (!string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the author of a post may edit it.");

        post.Title = title;
        post.Body = body ?? string.Empty;
        post.Audience = audience; // ADR 0001-B — written verbatim; never auto-augmented.
        // ADR 0018 (ADR 0014 amended) — the authored-in tag is editable on this lane:
        // the author can correct the language the post was written in. Re-resolved
        // through the shared helper so a blank submission falls back to the
        // instance default (it never blanks a stored tag).
        post.LanguageCode = await ResolveLanguageCodeAsync(languageCode, session).ConfigureAwait(false);
        // ATT U12 (C-ATT·4/8) — the post edit lane persists the re-parsed
        // attachment references (replace-style, the re-parse is authoritative).
        // Deliberate asymmetry: the image lane's edit lane does not set ImageIds
        // (C-ATT·9); this lane persists AttachmentIds so the serve route can find
        // the post row owning the id.
        post.AttachmentIds = attachmentIds ?? [];
        post.Modified = DateTimeOffset.UtcNow;

        session.Store(post);
        await session.SaveChangesAsync().ConfigureAwait(false);

        // TG (ADR 0044, U8b) — the tag attach/detach lane on the edit path (C3
        // single-transaction idiom, same session as the edit write). This lane
        // is **author-only** (the <c>post.AuthorId == actorId</c> gate above
        // re-checks standing), so the actor is always the author — the
        // <c>AttachToPostAsync</c> standing probe short-circuits on the author
        // match (the empty role set below is sufficient: <c>CanAttachToPost</c>
        // returns true on the author match, and <c>ResolveAttachVia</c>
        // resolves <c>Owner</c> first). A GlobalAdmin who is not the author
        // never reaches this code (the author-only gate throws first).
        //
        // Tri-state (the U8b register patch's detach semantics):
        //   · <c>null</c> ⇒ the caller carried no tag field — leave the post's
        //     existing <c>TagIds</c> untouched (the U4 default-empty pin for the
        //     lanes that never wire a tag field, i.e. the 7 existing Core test
        //     call sites that omit this parameter).
        //   · non-empty ⇒ attach: create-or-reuse each tag and store the
        //     resolved ids (a bad slug is an <c>ArgumentException</c> from
        //     <c>DeriveSlug</c>, C-TG·4 — the Web layer maps it to a form
        //     error).
        //   · empty (non-null) ⇒ detach: clear <c>Post.TagIds</c>. The
        //     <c>AttachToPostAsync</c> lane's standing probe is still the
        //     authoritative gate (it throws <c>UnauthorizedAccessException</c>
        //     before anything is written if the actor lacks standing); here the
        //     author-only lane has already run, so it passes. Calling the lane
        //     with an empty set is a documented no-op-attach that stores an
        //     empty id list (its <c>AttachToPostAsync</c> body writes
        //     <c>post.TagIds = tagIds</c> unconditionally once standing passes,
        //     so the empty set clears — matching <c>AttachToPageAsync</c>'s
        //     "no-op detach (empty slugs)" note), which is exactly the detach.
        if (tagSlugs is not null && _tags is not null)
        {
            var resolved = await _tags.AttachToPostAsync(
                    post.Id, tagSlugs, actorId, new HashSet<string>(), session)
                .ConfigureAwait(false);
            post.TagIds = resolved.Select(t => t.Id).ToList();
            session.Store(post);
            await session.SaveChangesAsync().ConfigureAwait(false);
        }

        return post;
    }

    /// <summary>
    /// Creates a one-level reply in the **caller's** in-flight session (invariant
    /// C3). The reply carries **no** <c>Audience</c> (C-M3·1): the parent's
    /// <c>Read</c> decision has already been made by the caller (the Web layer's
    /// detail path / <see cref="GetPostAsync"/>) — this method does **not**
    /// re-check and does **not** write an audit row of its own. One
    /// <c>SaveChangesAsync</c>.
    /// <para>
    /// <paramref name="languageCode"/> (ADR 0018) is the reply's **own**
    /// authored-in tag; null/empty is materialized from the instance default
    /// (the <c>en</c> floor applies when the singleton row is absent), so the
    /// stored row always carries a concrete BCP-47 code. Optional trailing
    /// parameter — the two existing controller call sites keep compiling.
    /// </para>
    /// <para>
    /// <paramref name="attachmentIds"/> (ATT U4, C-ATT·4) carries the reply's
    /// file-attachment references — the Web layer parses the body's
    /// <c>/attachment/{id}</c> links (via <c>Kumunita.Web.Security.AttachmentIds
    /// .ExtractAttachmentIds</c>, a Web-only helper — Core never parses the
    /// body, C-ATT·4) before calling this seam and passes them in; Core writes
    /// them verbatim.
    /// <b>Deliberate asymmetry (recorded, C-ATT·8/9):</b> the image lane's
    /// reply create/edit lane does <b>not</b> set
    /// <see cref="PostReply.ImageIds"/> (the image-lane reply-404 drift pause,
    /// C-ATT·9), but <b>this</b> lane does persist
    /// <see cref="PostReply.AttachmentIds"/>, because the attachment reply
    /// serve (C-ATT·8) resolves the parent post and must find the reply row
    /// owning the id. Optional trailing parameter (nullable, the CS1736
    /// shape) — the existing controller call sites keep compiling unchanged.
    /// </para>
    /// </summary>
    public async Task<PostReply> CreateReplyAsync(string postId, string actorId, string body, IDocumentSession session, string? languageCode = null, IReadOnlyList<string>? attachmentIds = null)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A parent post id is required.", nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("A reply author is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var reply = new PostReply
        {
            Id = Guid.NewGuid().ToString("N"),
            PostId = postId,
            AuthorId = actorId,
            Body = body ?? string.Empty,
            LanguageCode = await ResolveLanguageCodeAsync(languageCode, session).ConfigureAwait(false), // ADR 0018
            AttachmentIds = attachmentIds ?? [], // ATT U4 (C-ATT·4) — populated server-side by the Web layer (AttachmentIds.ExtractAttachmentIds); null-coalesce to the POCO's non-null empty list (the CreatePostAsync precedent).
            Created = DateTimeOffset.UtcNow
        };

        session.Store(reply);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return reply;
    }

    // ─── ADR 0016 — author-only reply-edit lane (body-only) ──────────────

    /// <summary>
    /// Re-write a reply's body, **author-only** (ADR 0016). The lane is a
    /// direct mirror of the component-post edit lane (ADR 0014) adapted to a
    /// reply: only the reply's own <see cref="PostReply.AuthorId"/> may edit;
    /// there is **no** moderator or GlobalAdmin branch (a moderator's lever
    /// over a reply is the parent post's Hide/Remove, not re-writing text).
    /// <para>
    /// The reply's <see cref="PostReply.Body"/> is the **only editable text
    /// field** — <c>PostId</c>, <c>AuthorId</c>, <c>Created</c>, and
    /// <see cref="PostReply.LanguageCode"/> (ADR 0018 — the authored-in tag is
    /// written at create time only) are
    /// immutable (a reply cannot be re-parented or re-attributed; the reply
    /// carries no <c>Audience</c> to re-choose, C-M3·1). As of ATT U4 the
    /// edit also re-copies <see cref="PostReply.AttachmentIds"/> (the body's
    /// re-parsed <c>/attachment/{id}</c> references — see the
    /// <paramref name="attachmentIds"/> note below); the image lane's
    /// <see cref="PostReply.ImageIds"/> stays untouched on this lane (C-ATT·9).
    /// The edit stamps
    /// <see cref="PostReply.Modified"/> forward (null until first edited).
    /// </para>
    /// <para>
    /// <paramref name="attachmentIds"/> (ATT U4, C-ATT·4) re-copies the
    /// reply's file-attachment references from the Web layer's re-parse of the
    /// (re-submitted) body — the same replace-style as
    /// <paramref name="body"/> (a body edit can add <c>or remove</c>
    /// <c>/attachment/{id}</c> links; the re-parse is authoritative, so the
    /// stored list is replaced wholesale, mirroring
    /// <c>reply.Body = body ?? string.Empty</c>). <b>Deliberate asymmetry
    /// (recorded, C-ATT·8/9):</b> the image lane's reply edit lane does
    /// <b>not</b> set <see cref="PostReply.ImageIds"/> (the image-lane
    /// reply-404 drift pause, C-ATT·9); <b>this</b> lane does persist
    /// <see cref="PostReply.AttachmentIds"/> (C-ATT·8). Optional trailing
    /// parameter (nullable, the CS1736 shape) — the existing controller call
    /// sites keep compiling unchanged.
    /// </para>
    /// <para>
    /// Like <see cref="CreateReplyAsync"/>, this method does **not** re-check
    /// authorization against the parent post and writes **no**
    /// <c>Authorization.AccessAudit</c> row of its own (C-M3·1 — the parent's
    /// <c>Read</c> decision governs visibility; the caller's Web layer has
    /// already scoped the reply's authorship to the requesting actor before
    /// reaching this seam). One <c>SaveChangesAsync</c> (invariant C3).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The reply id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the reply's author.</exception>
    public async Task<PostReply> UpdateReplyAsync(
        string replyId,
        string actorId,
        string body,
        IDocumentSession session,
        IReadOnlyList<string>? attachmentIds = null)
    {
        if (string.IsNullOrEmpty(replyId)) throw new ArgumentException("A reply id is required.", nameof(replyId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting author is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var reply = await session.LoadAsync<PostReply>(replyId).ConfigureAwait(false);
        if (reply is null)
            throw new KeyNotFoundException($"Reply '{replyId}' was not found in the session; nothing to edit.");

        // Author-only gate (the sole decision on this lane): only the author may edit.
        if (!string.Equals(reply.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the author of a reply may edit it.");

        reply.Body = body ?? string.Empty;
        reply.AttachmentIds = attachmentIds ?? []; // ATT U4 (C-ATT·4) — the re-parse from the (re-submitted) body is authoritative; replace-style, mirroring the reply.Body line (C-ATT·8 — the image lane's reply ImageIds drift pause stays untouched, C-ATT·9).
        reply.Modified = DateTimeOffset.UtcNow;

        session.Store(reply);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return reply;
    }

    // ─── ADR 0016 — author-only group-post edit lane (title + body only) ───

    /// <summary>
    /// Re-write a **group-lane** post's title and body, **author-only**
    /// (ADR 0016). A direct mirror of the component-post edit lane (ADR 0014)
    /// for the group lane (ADR 0013): only the post's own
    /// <see cref="Post.AuthorId"/> may edit; there is **no** moderator,
    /// GlobalAdmin, or break-glass branch (G·4 — the group lane has no
    /// moderator peek; a moderator's lever over a group post is its absence
    /// from the moderation surface, not re-writing text).
    /// <para>
    /// The editable fields are <see cref="Post.Title"/>,
    /// <see cref="Post.Body"/>, and <see cref="Post.LanguageCode"/> (ADR 0018,
    /// ADR 0016 as amended — the authored-in tag is editable on this lane too, so
    /// an author can correct the language a group post was written in; it is
    /// re-resolved through the shared <see cref="ResolveLanguageCodeAsync"/> helper,
    /// mirroring the ADR 0014 community-post lane and the ADR 0017 announcement
    /// edit lane). The group lane's identity fields are
    /// **immutable** — <c>GroupId</c> (the lane itself), <c>ComponentId</c>
    /// (must stay empty, G·2 lane exclusivity), <c>Audience</c> (non-null
    /// empty, G·8 — there is no audience to re-choose on the group lane),
    /// <c>AuthorId</c>, <c>Created</c>, and <c>Status</c> are all untouched.
    /// The edit stamps <see cref="Post.Modified"/> forward.
    /// </para>
    /// <para>
    /// A missing id is a <see cref="KeyNotFoundException"/>; a non-author is
    /// the <see cref="UnauthorizedAccessException"/>. The Web layer (the group
    /// lane's 404 shape) maps both to a 404 to stay non-leaky, unlike the
    /// component lane's 403. One <c>SaveChangesAsync</c> (invariant C3).
    /// </para>
    /// <para>
    /// <paramref name="attachmentIds"/> (ATT U12, C-ATT·4) carries the group
    /// post's file-attachment references — the Web layer re-parses the
    /// (re-submitted) body's <c>/attachment/{id}</c> links and passes them in;
    /// Core writes them verbatim, replace-style. <b>Deliberate asymmetry
    /// (recorded, C-ATT·8/9):</b> the image lane's post/group-post edit lanes
    /// do <b>not</b> set <see cref="Post.ImageIds"/>; <b>this</b> lane persists
    /// <see cref="Post.AttachmentIds"/> so the serve route can find the group
    /// post row owning the id. Optional trailing parameter (nullable, the
    /// CS1736 shape) — the existing controller call site keeps compiling.
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The post id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the post's author.</exception>
    public async Task<Post> UpdateGroupPostAsync(
        string postId,
        string actorId,
        string? title,
        string body,
        string? languageCode,
        IDocumentSession session,
        IReadOnlyList<string>? attachmentIds = null)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting author is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{postId}' was not found in the session; nothing to edit.");

        // Group-lane check (G·2): only a group post (non-empty GroupId) is
        // editable on this lane. A component post (empty GroupId) is the
        // component edit lane's (ADR 0014) surface, not this one — fail closed.
        if (string.IsNullOrEmpty(post.GroupId))
            throw new KeyNotFoundException($"Post '{postId}' is not a group post; nothing to edit here.");

        // Author-only gate (the sole decision on this lane): only the author may edit.
        if (!string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the author of a post may edit it.");

        post.Title = title;
        post.Body = body ?? string.Empty;
        // ADR 0018 (ADR 0016 amended) — the authored-in tag is editable on this lane:
        // the author can correct the language the post was written in. Re-resolved
        // through the shared helper so a blank submission falls back to the
        // instance default (it never blanks a stored tag).
        post.LanguageCode = await ResolveLanguageCodeAsync(languageCode, session).ConfigureAwait(false);
        // ATT U12 (C-ATT·4/8) — the group-post edit lane persists the re-parsed
        // attachment references (replace-style). Deliberate asymmetry: the image
        // lane's edit lanes do not set ImageIds (C-ATT·9); this lane persists
        // AttachmentIds so the serve route can find the group post row.
        post.AttachmentIds = attachmentIds ?? [];
        post.Modified = DateTimeOffset.UtcNow;
        // GroupId / ComponentId / Audience / AuthorId / Created / Status are
        // deliberately untouched — the group lane's identity is immutable.

        session.Store(post);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return post;
    }

    // ─── ADR 0024 — author soft-delete lanes (post + reply) ─────────────────

    /// <summary>
    /// Soft-delete a post — **author-only** (ADR 0024). A direct sibling of the
    /// ADR 0014 / ADR 0016 edit lanes: only the post's own
    /// <see cref="Post.AuthorId"/> may delete; there is **no** moderator,
    /// GlobalAdmin, or break-glass branch (the author's "take it down" is a
    /// distinct concern from the M3b <b>moderator</b> hide/remove surface,
    /// <see cref="HidePostAsync"/> / <see cref="RemovePostAsync"/> — it does not
    /// touch <see cref="Post.Status"/> at all).
    /// <para>
    /// Lane-neutral (like <see cref="UpdateReplyAsync"/>): it works for a
    /// component post (empty <see cref="Post.GroupId"/>) and a group-lane post
    /// (non-empty <see cref="Post.GroupId"/>) alike — the delete is the same
    /// field write either way, so there is no per-lane split (contrast
    /// <see cref="UpdatePostAsync"/> / <see cref="UpdateGroupPostAsync"/>, which
    /// differ only in editable surface). The Web layer's 403 (component) / 404
    /// (group) failure shape is decided there, not here.
    /// </para>
    /// <para>
    /// The record is **kept** (never hard-deleted): <see cref="Post.DeletedAt"/>
    /// is stamped and <see cref="Post.Modified"/> moves forward. Read lanes hide
    /// it — the feeds (<see cref="ListFeedAsync"/>, <see cref="ListAllFeedAsync"/>,
    /// <see cref="ListGroupFeedAsync"/>) filter on
    /// <c>DeletedAt is null</c> — but the **detail** lanes
    /// (<see cref="GetPostAsync"/>, <see cref="GetGroupPostAsync"/>) still return
    /// the post so the author can see a placeholder, and its replies (their own
    /// documents) remain visible. One <c>SaveChangesAsync</c> (invariant C3).
    /// No <see cref="Kumunita.Core.Authorization.AccessAudit"/> row: the author is
    /// acting on their own content (the ADR 0014 / 0016 edit-lane precedent —
    /// content changes by the author are not audited).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The post id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the post's author.</exception>
    public async Task<Post> DeletePostAsync(string postId, string actorId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting author is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{postId}' was not found in the session; nothing to delete.");

        // Author-only gate (the sole decision on this lane): only the author may
        // delete. A non-author is a denial — the Web layer maps it to its lane's
        // 403/404 shape (the ADR 0014/0016 edit-lane precedent).
        if (!string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the author of a post may delete it.");

        // Idempotent: a second delete just moves the timestamp forward.
        post.DeletedAt = DateTimeOffset.UtcNow;
        post.Modified = DateTimeOffset.UtcNow;

        session.Store(post);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return post;
    }

    /// <summary>
    /// Soft-delete a reply — **author-only** (ADR 0024). A sibling of the ADR 0016
    /// reply-edit lane: only the reply's own <see cref="PostReply.AuthorId"/> may
    /// delete. Lane-neutral (a reply's visibility is always the parent post's
    /// single <c>Read</c> decision, C-M3·1, whether the parent is a component or
    /// group post) — no per-lane split.
    /// <para>
    /// The record is **kept**: <see cref="PostReply.DeletedAt"/> is stamped (and
    /// <see cref="PostReply.Modified"/> moves forward); the detail view renders a
    /// placeholder in place of the body, and the reply still counts toward the
    /// parent's reply count. One <c>SaveChangesAsync</c> (invariant C3). No
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row (the author
    /// acting on their own content — the ADR 0016 edit-lane precedent).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The reply id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the reply's author.</exception>
    public async Task<PostReply> DeleteReplyAsync(string replyId, string actorId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(replyId)) throw new ArgumentException("A reply id is required.", nameof(replyId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting author is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var reply = await session.LoadAsync<PostReply>(replyId).ConfigureAwait(false);
        if (reply is null)
            throw new KeyNotFoundException($"Reply '{replyId}' was not found in the session; nothing to delete.");

        if (!string.Equals(reply.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the author of a reply may delete it.");

        reply.DeletedAt = DateTimeOffset.UtcNow;
        reply.Modified = DateTimeOffset.UtcNow;

        session.Store(reply);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return reply;
    }

    // ─── ADR 0037 — author-only publish lane (draft → live) ─────────────────

    /// <summary>
    /// Publish a draft post — **author-only** (ADR 0037). Clears
    /// <see cref="Post.IsDraft"/> so the post becomes visible under its normal
    /// lane's rules: the component lane's audience decision
    /// (<see cref="Post.Audience"/>) for a community post, or the group lane's
    /// membership decision for a group post. A direct sibling of the ADR 0014 /
    /// 0016 author-only edit lanes and ADR 0024's author-only delete lane: the
    /// sole decision is <c>post.AuthorId == actorId</c> (ordinal comparison); a
    /// non-author is denied (<see cref="UnauthorizedAccessException"/>) even at
    /// GlobalAdmin (ADR 0037's author-only pin — publishing is the author's
    /// choice, not a moderator's or an admin's lever).
    /// <para>
    /// Lane-neutral (like <see cref="DeletePostAsync"/>): it works for a
    /// community post (empty <see cref="Post.GroupId"/>) and a group-lane post
    /// (non-empty <see cref="Post.GroupId"/>) alike — the publish is the same
    /// field write either way, so there is no per-lane split. Publishing is
    /// <b>idempotent</b>: a second publish on an already-live post is a no-op
    /// (it only clears a flag that is already false, and does not stamp
    /// <see cref="Post.Modified"/> in that case, mirroring the ADR 0024 /
    /// announcement no-op-re-save pin). It does <b>not</b> touch
    /// <see cref="Post.Status"/> (the moderator surface) or
    /// <see cref="Post.DeletedAt"/> (the author soft-delete) — a draft that a
    /// moderator has hidden or the author has deleted stays in that state after
    /// publish; only the draft flag clears. One <c>SaveChangesAsync</c>
    /// (invariant C3). No <see cref="Kumunita.Core.Authorization.AccessAudit"/>
    /// row: the author is acting on their own content (the ADR 0014 / 0016 /
    /// 0024 author-lane precedent — content-state changes by the author are not
    /// audited).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The post id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the post's author.</exception>
    public async Task<Post> PublishPostAsync(string postId, string actorId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting author is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{postId}' was not found in the session; nothing to publish.");

        // Author-only gate (ADR 0037): only the author may publish. A non-author
        // is a denial — the Web layer maps it to its lane's 403 (community) /
        // 404 (group) shape, the ADR 0014/0016 edit-lane precedent.
        if (!string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the author of a post may publish it.");

        // Idempotent (the ADR 0024 / announcement no-op-re-save pin): a second
        // publish on an already-live post is a no-op — it does not stamp
        // Modified when nothing changed.
        if (post.IsDraft)
        {
            post.IsDraft = false;
            post.Modified = DateTimeOffset.UtcNow;
        }

        session.Store(post);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return post;
    }

    /// <summary>
    /// The **author's own** drafts (ADR 0037) — the community- and group-lane
    /// <see cref="Post"/>s with <see cref="Post.IsDraft"/> true and
    /// <see cref="Post.AuthorId"/> == <paramref name="actorId"/> (ordinal
    /// comparison), sorted by <see cref="Post.Created"/> descending. This is the
    /// "My drafts" list the Web layer's <c>GET /my/drafts</c> renders — the
    /// discoverability surface for drafts, since feeds deliberately exclude
    /// them.
    /// <para>
    /// <b>Not an authorization surface (the ADR 0037 author-lane precedent):</b>
    /// the only decision is the pure <c>AuthorId == actorId</c> match in the
    /// query itself — there is no <see cref="IAuthorizationService"/> call and
    /// <b>no</b> <see cref="Kumunita.Core.Authorization.AccessAudit"/> row, for
    /// the same reason the ADR 0014 / 0016 edit lanes and ADR 0024's delete
    /// lane write none (the author acting on their own content is not a decision
    /// about <em>others'</em> content, so there is nothing to audit). Opens its
    /// own <c>QuerySession</c> (the C3 read-lane shape — reads never touch the
    /// caller's write session). Deleted drafts (<see cref="Post.DeletedAt"/>
    /// set) are excluded: an author who deleted their own draft does not see it
    /// in the list (a draft the author deleted is gone from their working set,
    /// the ADR 0024 "kept but hidden from the author's working surfaces" shape).
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Post>> ListMyDraftsAsync(string actorId)
    {
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));

        await using var session = _store.QuerySession();
        return await session
            .Query<Post>()
            .Where(p => p.IsDraft && p.AuthorId == actorId && p.DeletedAt == null)
            .OrderByDescending(p => p.Created)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    // ─── M3b C-M3b·3 — the two Moderate-gated write lanes (F3/F4) ─────────────

    /// <summary>
    /// Hide a post (F3; C-M3b·3). The <see cref="AccessAction.Moderate"/>-gated
    /// write lane: calls <see cref="IAuthorizationService.CanAsync(string,
    /// AccessAction, IAuditableResource, IDocumentSession)"/> with
    /// <c>AccessAction.Moderate</c> **before** writing, in the **same**
    /// <c>IDocumentSession</c> transaction as the <c>Status</c> write
    /// (invariant C3 — same-transaction; ADR 0006-C: audit always on — Allow
    /// *and* Deny). A denied call is **not executed at all** (no
    /// <c>Status</c> write, no partial state) — the audit row still commits
    /// in the caller's <c>SaveChangesAsync</c> (C3). The acting identity is
    /// <paramref name="actorId"/>; the audit <c>Via</c> tag is written by the
    /// frozen <see cref="IAuthorizationService"/> (M1 surface), not here.
    /// </summary>
    public async Task HidePostAsync(string postId, string actorId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("A moderating actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{postId}' was not found in the session; nothing to hide.");

        // C3 / ADR 0006-C — audit row always written (Allow or Deny), in the
        // caller's transaction. The decision gate runs *before* any write.
        var decision = await _authz.CanAsync(actorId, AccessAction.Moderate,
                new PostToAuditableResource(post), session)
            .ConfigureAwait(false);

        if (decision.Allowed)
        {
            post.Status = PostStatus.Hidden;
            post.Modified = DateTimeOffset.UtcNow;
            session.Store(post);
        }

        // One SaveChangesAsync — the C3 same-transaction lane (ADR 0006-E): the
        // audit row and (if Allowed) the Status write commit atomically.
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Remove a post (F4; C-M3b·3). The <see cref="AccessAction.Moderate"/>-gated
    /// write lane (the "hard remove" counterpart to
    /// <see cref="HidePostAsync"/>). Same semantics as
    /// <see cref="HidePostAsync"/> but writes
    /// <see cref="PostStatus.Removed"/>. A denied call is not executed at all
    /// (no <c>Status</c> write, no partial state); the audit row still commits
    /// in the caller's <c>SaveChangesAsync</c> (C3).
    /// </summary>
    public async Task RemovePostAsync(string postId, string actorId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("A moderating actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{postId}' was not found in the session; nothing to remove.");

        var decision = await _authz.CanAsync(actorId, AccessAction.Moderate,
                new PostToAuditableResource(post), session)
            .ConfigureAwait(false);

        if (decision.Allowed)
        {
            post.Status = PostStatus.Removed;
            post.Modified = DateTimeOffset.UtcNow;
            session.Store(post);
        }

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    // ─── group posts (ADR 0013) — the group-lane surface (U6; the M3 composition
    //     pattern applied to the membership lane; the lane owns every access read,
    //     ADR 0006-D; no audience / moderate / break-glass branch — G·1/G·4) ───

    /// <summary>
    /// A group's channel feed (G1–G4 FACES, design doc §2.3(a)): the candidate
    /// set is the group's posts — <c>Post.GroupId == groupId</c>,
    /// <c>Created desc</c>, paged with the class's existing <c>PageSize</c> (the
    /// 30-per-page shape is M3's, unchanged). Exactly one
    /// <see cref="IAuthorizationService.CanSeeGroupFeedAsync(string, string, int)"/>
    /// (the **standalone** form — a plain read with no in-flight caller
    /// transaction, the <see cref="ListFeedAsync"/> precedent) writes the
    /// visit's **single aggregate** <c>AccessAudit</c> row (G·5: TargetKind
    /// "grouppost", TargetId null, counts). Allow ⇒ the paged candidates (G1);
    /// Deny ⇒ a <see cref="FeedResult"/> with an **empty** visible list and
    /// <see cref="FeedResult.HiddenCount"/> = the candidate count (G2). **0
    /// candidates ⇒** empty <see cref="FeedResult"/>, no decision, **no** row
    /// (the M3 <see cref="ListFeedAsync"/> 0-candidate shape). **No audience
    /// evaluation of any kind** (G·1/G·8 — membership is the sole decision).
    /// </summary>
    public async Task<FeedResult> ListGroupFeedAsync(string groupId, string actorId, int page)
    {
        if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("A group feed requires a groupId.", nameof(groupId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        var candidates = await session
            .Query<Post>()
            .Where(p => p.GroupId == groupId && p.DeletedAt == null && !p.IsDraft)
            .OrderByDescending(p => p.Created)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync()
            .ConfigureAwait(false);

        if (candidates.Count == 0)
            return new FeedResult(Visible: Array.Empty<Post>(), HiddenCount: 0, Page: page, Total: 0);

        // G·5 (the C-M3·3 analog) — one standalone whole-channel call over the
        // paged candidate set writes the visit's single aggregate AccessAudit
        // row (G·5). Standalone form: a plain read with no caller transaction
        // (the ListFeedAsync precedent), so the standalone method's own commit
        // is the correct C3 lane. The channel is all-or-nothing for a principal,
        // so the paged candidates are returned as-is on Allow.
        var decision = await _authz
            .CanSeeGroupFeedAsync(actorId, groupId, candidates.Count)
            .ConfigureAwait(false);

        if (decision.Allowed)
            // G1 — the paged candidates, as-is (membership is the sole decision; G·1).
            return new FeedResult(Visible: candidates, HiddenCount: 0, Page: page, Total: candidates.Count);

        // G2 — Deny: empty visible list, HiddenCount = the candidate count (the
        // aggregate Deny row **is** the audit evidence — G·1/G·5); never a
        // post's fields.
        return new FeedResult(Visible: Array.Empty<Post>(), HiddenCount: candidates.Count, Page: page, Total: 0);
    }

    /// <summary>
    /// A group post's detail + its one-level replies (G11 FACES, design doc
    /// §2.3(b) — the C-M3·1 analog, G·7): the post is loaded first (the M3
    /// fail-closed shape: missing ⇒ <c>Post = null</c>, no decision, **no**
    /// row); a post with an **empty** <c>GroupId</c> (not a group post) or
    /// <c>GroupId != groupId</c> (route/lane mismatch) ⇒ <c>Post = null</c>,
    /// no row (fail-closed). Otherwise **exactly one**
    /// <see cref="IAuthorizationService.CanSeeGroupAsync(string, string, string?)"/>
    /// (standalone) — the detail decision row (TargetKind "grouppost",
    /// **TargetId = postId**, G·5). Allow ⇒ the post + its
    /// <see cref="PostReply"/> list **as-is** — the replies inherit the parent's
    /// single group-lane decision: no second evaluation, no per-reply row (G·7).
    /// Deny ⇒ <c>Post = null</c> with **no** replies (Web 404 — G·3/G·4) — the
    /// decision's row **was** written (C3). **No** audience evaluation
    /// (G·1/G·8).
    /// </summary>
    public async Task<PostDetailResult> GetGroupPostAsync(string groupId, string postId, string actorId)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));

        await using var session = _store.QuerySession();
        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            // M3 fail-closed shape: the post does not exist ⇒ no decision, no row.
            return new PostDetailResult(Post: null, Replies: Array.Empty<PostReply>());

        // Lane fail-closed (design doc §2.2): a post with an empty GroupId (not a
        // group post) or a route/lane mismatch (GroupId != groupId) is denied with
        // **no** decision and **no** row (the M3 shape, §2.3(b) row 3).
        if (string.IsNullOrEmpty(post.GroupId) || post.GroupId != groupId)
            return new PostDetailResult(Post: null, Replies: Array.Empty<PostReply>());

        // ADR 0037 — draft gate (author-only, no audit row): a group-lane draft is
        // invisible to every member and to any moderator/admin except its author.
        // The group-lane membership decision (CanSeeGroupAsync) is NOT consulted —
        // no CanSeeGroupAsync, no AccessAudit row. The sole decision is a pure
        // AuthorId == actorId ordinal check (ADR 0037 author-only pin, stronger
        // than the lane's membership gate). A non-author member is denied (the
        // group lane's Web 404 shape); the author sees the draft with its
        // replies (G·7 shape: replies returned as-is, no second evaluation).
        if (post.IsDraft)
        {
            if (!string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
                return new PostDetailResult(Post: null, Replies: Array.Empty<PostReply>());

            var authorReplies = await session
                .Query<PostReply>()
                .Where(r => r.PostId == postId)
                .OrderBy(r => r.Created)
                .ToListAsync()
                .ConfigureAwait(false);

            return new PostDetailResult(Post: post, Replies: authorReplies);
        }

        // G11 (C-M3·1 analog, G·7) — exactly one standalone single-target call →
        // the detail decision row (TargetId = postId, G·5). No audience
        // evaluation of any kind (G·1/G·8).
        var decision = await _authz.CanSeeGroupAsync(actorId, groupId, postId).ConfigureAwait(false);

        if (!decision.Allowed)
            // Deny ⇒ Post = null, no replies (Web 404 — G·3/G·4); the row was
            // written (C3).
            return new PostDetailResult(Post: null, Replies: Array.Empty<PostReply>());

        // G·7 — the reply list is returned **as-is** under the parent's single
        // group-lane decision: no second authorization evaluation, no per-reply
        // audit row (the M3 replies shape, §2.3(b) row 1).
        var replies = await session
            .Query<PostReply>()
            .Where(r => r.PostId == postId)
            .OrderBy(r => r.Created)
            .ToListAsync()
            .ConfigureAwait(false);

        return new PostDetailResult(Post: post, Replies: replies);
    }

    /// <summary>
    /// Creates a group post, in the **caller's** in-flight session (invariant
    /// C3). The **create gate is the group-lane decision** (G·3): one
    /// <see cref="IAuthorizationService.CanSeeGroupAsync(string, string, string?, IDocumentSession)"/>
    /// with <c>targetPostId: null</c>, in the caller's transaction — **deny**:
    /// the row is committed by a <c>SaveChangesAsync()</c> **before**
    /// <see cref="UnauthorizedAccessException"/> throws (the gate row must
    /// survive — G6 FACES; Web maps it to 404); **allow**: the gate row + the
    /// new post commit in **one** <c>SaveChangesAsync()</c> (atomic with the
    /// write, C3). The gate is the **sole** decision (G·3): **no**
    /// <c>actorRoles</c> parameter (contrast <see cref="CreatePostAsync"/>'s
    /// GlobalAdmin/moderator skip — a non-member GlobalAdmin is **denied**, G8
    /// FACES/G·4), **no** break-glass, and **no** membership read here (the lane
    /// owns its reads — ADR 0006-D). The write pins G·2/G·8:
    /// <c>ComponentId = string.Empty</c>, <c>Audience = new Audience()</c>
    /// (non-null, **empty**). One <c>SaveChangesAsync()</c>.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The actor (or, under an
    /// in-scope <c>read</c> grant, the owner) is not a member of
    /// <c>draft.GroupId</c> — thrown **after** the gate row is persisted.</exception>
    public async Task<Post> CreateGroupPostAsync(GroupPostDraft draft, string actorId, IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An authoring actor is required.", nameof(actorId));
        if (string.IsNullOrEmpty(draft.GroupId))
            // G·3 — non-empty channel enforced **before** any decision (no audit
            // row is written for such input — the §2.2 GroupPostDraft pin).
            throw new ArgumentException("A group post requires a non-empty GroupId (the group lane).", nameof(draft.GroupId));
        ArgumentNullException.ThrowIfNull(session);

        // G·3 — the create gate **is** the group-lane decision: one session-variant
        // call with targetPostId: null (⇒ the row's TargetId = the group id, the
        // channel as the gate's target), in the caller's transaction. Deny ⇒ the
        // row is persisted by this SaveChangesAsync **before** the throw (the
        // gate row must survive — G6 FACES; Web maps the exception to 404). Allow
        // ⇒ the gate row + the new post commit in one SaveChangesAsync (C3).
        var decision = await _authz
            .CanSeeGroupAsync(actorId, draft.GroupId, null, session)
            .ConfigureAwait(false);

        if (!decision.Allowed)
        {
            await session.SaveChangesAsync().ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"You are not a member of the group '{draft.GroupId}'; " +
                "only group members may post to a group channel.");
        }

        var post = new Post
        {
            Id = Guid.NewGuid().ToString("N"),
            ComponentId = string.Empty, // G·2 — lane exclusivity: structurally absent from the M3 feeds.
            GroupId = draft.GroupId,    // G·2 — the non-empty group lane.
            AuthorId = actorId,
            Title = draft.Title,
            Body = draft.Body,
            Audience = new Audience(),  // G·8 — written non-null **empty**; never authored here.
            ImageIds = draft.ImageIds ?? [], // RC R·3/R·7 (ADR 0025) — populated server-side by the Web layer (the U05 group-post create wiring); null-coalesce to the POCO's non-null empty list (the CreatePostAsync precedent).
            AttachmentIds = draft.AttachmentIds ?? [], // ATT U4 (C-ATT·4) — populated server-side by the Web layer (AttachmentIds.ExtractAttachmentIds); null-coalesce to the POCO's non-null empty list (the CreatePostAsync precedent).
            LanguageCode = await ResolveLanguageCodeAsync(draft.LanguageCode, session).ConfigureAwait(false), // ADR 0018
            IsDraft = draft.IsDraft ?? false, // ADR 0037 — draft mode (group lane); the author-only gate is the same pure AuthorId==actorId check (the author is a group member by construction of the create gate).
            Created = DateTimeOffset.UtcNow
        };

        session.Store(post);
        // One SaveChangesAsync — the C3 same-transaction lane (ADR 0006-E): the
        // gate decision row + the new post commit atomically.
        await session.SaveChangesAsync().ConfigureAwait(false);
        return post;
    }

    // ─── ADR 0022 — user-added post/reply translations lane ────────────────

    /// <summary>
    /// The **read** seam for a post's user-added translations (ADR 0022): the
    /// <see cref="PostTranslation"/> rows under <paramref name="postId"/>.
    /// Opens its own <c>QuerySession</c> (the C3 read-lane shape, mirroring
    /// <see cref="ListFeedAsync"/> — reads never touch the caller's write
    /// session).
    /// <para>
    /// <b>Not an authorization surface (C-M3·1 carried over):</b> a translation
    /// has no own audience — its visibility inherits the parent post's single
    /// <c>Read</c> decision, which the caller has already made (the Web reads
    /// this only after <see cref="GetPostAsync"/> returned the post). So this
    /// method does **not** call <see cref="IAuthorizationService"/> and writes
    /// **no** <c>AccessAudit</c> row — the same "a read, not a decision" pin as
    /// the reply list's as-is return.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<PostTranslation>> GetPostTranslationsAsync(string postId)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));

        await using var session = _store.QuerySession();
        return await session
            .Query<PostTranslation>()
            .Where(t => t.PostId == postId)
            .OrderBy(t => t.LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The **read** seam for a set of replies' user-added translations (ADR
    /// 0022): every <see cref="ReplyTranslation"/> whose
    /// <see cref="ReplyTranslation.ReplyId"/> is in <paramref name="replyIds"/>.
    /// Batching here keeps the post-detail view to a single query for its whole
    /// reply list (the M2 read-lane "one query per surface" preference). Owns
    /// its <c>QuerySession</c> (C3 read lane); **not** an authorization surface
    /// and writes **no** audit row (C-M3·1 — inherits the parent post's single
    /// <c>Read</c> decision, already made by the caller).
    /// </summary>
    public async Task<IReadOnlyList<ReplyTranslation>> GetReplyTranslationsAsync(IReadOnlyCollection<string> replyIds)
    {
        ArgumentNullException.ThrowIfNull(replyIds);
        if (replyIds.Count == 0)
            return [];

        await using var session = _store.QuerySession();
        return await session
            .Query<ReplyTranslation>()
            .Where(t => replyIds.Contains(t.ReplyId))
            .OrderBy(t => t.LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a **user-added translation** of a post in the **caller's** in-flight
    /// session (invariant C3 — the same-transaction lane; the
    /// <see cref="IDocumentSession"/> is the caller's, so the write and the
    /// in-session <c>AccessAudit</c> row commit or roll back atomically).
    /// <para>
    /// <b>Standing (ADR 0022, the approved default):</b> the post's
    /// <b>author</b> (<see cref="AccessVia.Owner"/>); a
    /// <see cref="Identity.Roles.GlobalAdmin"/> (<see cref="AccessVia.Admin"/>);
    /// and — on the **community** lane only — a
    /// <c>Moderator</c> scoped to the post's component
    /// (<see cref="AccessVia.Moderator"/>). On the **group** lane (
    /// <see cref="Post.GroupId"/> non-empty, ADR 0013) the component-moderator
    /// standing does **not** apply (ADR 0007 — no component-moderator scope
    /// exists for a group; GlobalAdmin is the only non-author standing). A
    /// denied actor throws <see cref="UnauthorizedAccessException"/> **before**
    /// anything is stored.
    /// </para>
    /// <para>
    /// <paramref name="languageCode"/> is the **target** language (a
    /// <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/> the Web offers
    /// from the enabled catalog); it is written **verbatim** (never floored to
    /// the instance default — a blank target is a caller error). One
    /// <c>SaveChangesAsync</c>.
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The post id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// author / GlobalAdmin / (community) component-moderator standings.</exception>
    public async Task<PostTranslation> AddPostTranslationAsync(
        string postId,
        string languageCode,
        string? title,
        string body,
        string actorId,
        IReadOnlySet<string> actorRoles,
        IDocumentSession session)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a non-empty body.", nameof(body));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{postId}' was not found in the session; nothing to translate.");

        var via = ResolveTranslationStanding(
            post.GroupId.Length > 0, post.ComponentId, post.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the post's author (or a moderator of the community, or an admin) " +
                "may add a translation of it.");

        var now = DateTimeOffset.UtcNow;
        var translation = new PostTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            PostId = postId,
            LanguageCode = languageCode,
            Title = title,
            Body = body,
            AuthorId = actorId,
            Created = now
        };

        // Audit row (ADR 0022 write-lane, the ModerationService.FileReportAsync
        // precedent — a hand-written audit row with no CanAsync decision call):
        // the Via tag records the standing the actor used (Owner / Moderator /
        // Admin).
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "posttranslation.add",
            TargetKind = "post",
            TargetId = postId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(translation);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return translation;
    }

    /// <summary>
    /// Adds a **user-added translation** of a reply in the **caller's** in-flight
    /// session (invariant C3; the <see cref="IDocumentSession"/> is the caller's).
    /// <para>
    /// <b>Standing (ADR 0022, mirroring the parent post's):</b> the reply's
    /// <b>author</b> (<see cref="AccessVia.Owner"/>); a
    /// <see cref="Identity.Roles.GlobalAdmin"/> (<see cref="AccessVia.Admin"/>);
    /// and — on the **community** lane only — a <c>Moderator</c> scoped to the
    /// parent post's component (<see cref="AccessVia.Moderator"/>). On the
    /// **group** lane the component-moderator standing does not apply (ADR 0007).
    /// A denied actor throws <see cref="UnauthorizedAccessException"/> before
    /// anything is stored.
    /// </para>
    /// <para>
    /// <paramref name="languageCode"/> is the target language (written verbatim;
    /// a blank target is a caller error); the translation is body-only (a reply
    /// has no title — C-M3·1). One <c>SaveChangesAsync</c>.
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The reply id (or its parent post)
    /// is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// author / GlobalAdmin / (community) component-moderator standings.</exception>
    public async Task<ReplyTranslation> AddReplyTranslationAsync(
        string replyId,
        string languageCode,
        string body,
        string actorId,
        IReadOnlySet<string> actorRoles,
        IDocumentSession session)
    {
        if (string.IsNullOrEmpty(replyId)) throw new ArgumentException("A reply id is required.", nameof(replyId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a non-empty body.", nameof(body));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var reply = await session.LoadAsync<PostReply>(replyId).ConfigureAwait(false);
        if (reply is null)
            throw new KeyNotFoundException($"Reply '{replyId}' was not found in the session; nothing to translate.");

        // Standing mirrors the parent post's lane (community vs group) and the
        // parent post's component scope — the reply itself has no component
        // (C-M3·1), so the scope comes from the parent.
        var parent = await session.LoadAsync<Post>(reply.PostId).ConfigureAwait(false);
        if (parent is null)
            throw new KeyNotFoundException($"Reply '{replyId}' has no parent post; nothing to translate.");

        var via = ResolveTranslationStanding(
            parent.GroupId.Length > 0, parent.ComponentId, reply.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the reply's author (or a moderator of the community, or an admin) " +
                "may add a translation of it.");

        var now = DateTimeOffset.UtcNow;
        var translation = new ReplyTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            ReplyId = replyId,
            LanguageCode = languageCode,
            Body = body,
            AuthorId = actorId,
            Created = now
        };

        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "replytranslation.add",
            TargetKind = "reply",
            TargetId = replyId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(translation);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return translation;
    }

    // ─── ADR 0048 — edit + delete lane for post/reply translations ───────
    // The ADR 0022 write lane was add-only (one row per (parent, language)
    // pair, enforced by the unique index in M3DocTypes). ADR 0048 lifts the
    // "add-only" pin: a standing-holder (the same matrix as the add lane —
    // author / GlobalAdmin / (community-lane) component-moderator) may now
    // **update** the existing (parent, language) row in place, or **remove**
    // it. Update is a field-write on the existing row (same Title/Body
    // shape as the add, the parent re-validated by load); Remove is a hard
    // `session.Delete` of the row (the trail is preserved by the
    // <c>AccessAudit</c> row written in the same session — ADR 0024's
    // soft-delete rationale does not apply to a translation: it has no
    // children to keep visible, and a soft-delete would block re-adding the
    // same language via the unique index). Standing is re-derived from the
    // **parent** (the row's own <c>AuthorId</c> is the adder, which may be a
    // different standing-holder — the post's author, a GlobalAdmin, or a
    // community moderator). Both lanes write a hand-written
    // <c>AccessAudit</c> row in the caller's session (C3) and commit
    // atomically (one <c>SaveChangesAsync</c>).

    /// <summary>
    /// **Updates** the existing <see cref="PostTranslation"/> row for
    /// (<paramref name="postId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing (ADR 0048, same as
    /// the ADR 0022 add lane): the post's **author**
    /// (<see cref="AccessVia.Owner"/>), a <see cref="Identity
    /// .Roles.GlobalAdmin"/> (<see cref="AccessVia.Admin"/>), and — on the
    /// community lane only — a <c>Moderator</c> scoped to the post's
    /// component (<see cref="AccessVia.Moderator"/>). The group lane has no
    /// component-moderator standing (ADR 0007). A denied actor throws
    /// <see cref="UnauthorizedAccessException"/> before anything is written.
    /// <para>
    /// The row's <see cref="PostTranslation.Title"/> /
    /// <see cref="PostTranslation.Body"/> are replaced verbatim (a blank
    /// <paramref name="title"/> clears the title, matching the add lane's
    /// null-coalescing; a blank <paramref name="body"/> is a caller error —
    /// a translation with no body is not a translation). The parent
    /// <see cref="Post"/> and the <c>(PostId, LanguageCode)</c> row are both
    /// loaded in the same session: a missing parent is a
    /// <see cref="KeyNotFoundException"/> (the parent was deleted after the
    /// translation was added, or the id is a hallucination), a missing row
    /// is a <see cref="KeyNotFoundException"/> (a shape error for this
    /// route — the Web layer offers this route only for languages that
    /// already have a row). One <c>SaveChangesAsync</c>.
    /// </para>
    /// </summary>
    public async Task<PostTranslation> UpdatePostTranslationAsync(
        string postId, string languageCode, string? title, string body,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a non-empty body.", nameof(body));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{postId}' was not found in the session; nothing to update.");

        var row = await session.Query<PostTranslation>()
            .Where(t => t.PostId == postId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"Post '{postId}' has no translation in '{languageCode}'; nothing to update.");

        var via = ResolveTranslationStanding(
            post.GroupId.Length > 0, post.ComponentId, post.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the post's author (or a moderator of the community, or an admin) " +
                "may edit a translation of it.");

        row.Title = string.IsNullOrWhiteSpace(title) ? null : title;
        row.Body = body;

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "posttranslation.update",
            TargetKind = "post",
            TargetId = postId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(row);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

    /// <summary>
    /// **Removes** the existing <see cref="PostTranslation"/> row for
    /// (<paramref name="postId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing is the same matrix
    /// as <see cref="UpdatePostTranslationAsync"/> — the ADR 0048
    /// author / GlobalAdmin / (community) component-moderator rule,
    /// re-derived from the parent post. A hard <c>session.Delete</c> of the
    /// row; the trail is preserved by the <see cref="Authorization
    /// .AccessAudit"/> row written in the same session (action
    /// <c>posttranslation.remove</c>). A missing parent or a missing row is
    /// a <see cref="KeyNotFoundException"/> (a double-remove is a shape
    /// error for this route — the Web layer offers the delete affordance
    /// only for languages that have a row). One <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task RemovePostTranslationAsync(
        string postId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{postId}' was not found in the session; nothing to remove.");

        var row = await session.Query<PostTranslation>()
            .Where(t => t.PostId == postId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"Post '{postId}' has no translation in '{languageCode}'; nothing to remove.");

        var via = ResolveTranslationStanding(
            post.GroupId.Length > 0, post.ComponentId, post.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the post's author (or a moderator of the community, or an admin) " +
                "may remove a translation of it.");

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "posttranslation.remove",
            TargetKind = "post",
            TargetId = postId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Delete(row);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// **Updates** the existing <see cref="ReplyTranslation"/> row for
    /// (<paramref name="replyId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing mirrors the parent
    /// post's lane (community vs group) and the parent's component scope —
    /// the reply itself has no component (C-M3·1), so the scope comes from
    /// the parent. A denied actor throws
    /// <see cref="UnauthorizedAccessException"/> before anything is
    /// written. A missing parent post, a missing reply, or a missing
    /// (ReplyId, LanguageCode) row is a
    /// <see cref="KeyNotFoundException"/>. One <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task<ReplyTranslation> UpdateReplyTranslationAsync(
        string replyId, string languageCode, string body,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(replyId)) throw new ArgumentException("A reply id is required.", nameof(replyId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a non-empty body.", nameof(body));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var reply = await session.LoadAsync<PostReply>(replyId).ConfigureAwait(false);
        if (reply is null)
            throw new KeyNotFoundException($"Reply '{replyId}' was not found in the session; nothing to update.");

        var parent = await session.LoadAsync<Post>(reply.PostId).ConfigureAwait(false);
        if (parent is null)
            throw new KeyNotFoundException($"Reply '{replyId}' has no parent post; nothing to update.");

        var row = await session.Query<ReplyTranslation>()
            .Where(t => t.ReplyId == replyId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"Reply '{replyId}' has no translation in '{languageCode}'; nothing to update.");

        var via = ResolveTranslationStanding(
            parent.GroupId.Length > 0, parent.ComponentId, reply.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the reply's author (or a moderator of the community, or an admin) " +
                "may edit a translation of it.");

        row.Body = body;

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "replytranslation.update",
            TargetKind = "reply",
            TargetId = replyId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Store(row);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

    /// <summary>
    /// **Removes** the existing <see cref="ReplyTranslation"/> row for
    /// (<paramref name="replyId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing mirrors the parent
    /// post's lane and component scope (the ADR 0048 author / GlobalAdmin /
    /// (community) component-moderator rule). A hard
    /// <c>session.Delete</c>; the trail is preserved by the
    /// <see cref="Authorization.AccessAudit"/> row (action
    /// <c>replytranslation.remove</c>). A missing parent post, reply, or
    /// row is a <see cref="KeyNotFoundException"/>. One
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task RemoveReplyTranslationAsync(
        string replyId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(replyId)) throw new ArgumentException("A reply id is required.", nameof(replyId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var reply = await session.LoadAsync<PostReply>(replyId).ConfigureAwait(false);
        if (reply is null)
            throw new KeyNotFoundException($"Reply '{replyId}' was not found in the session; nothing to remove.");

        var parent = await session.LoadAsync<Post>(reply.PostId).ConfigureAwait(false);
        if (parent is null)
            throw new KeyNotFoundException($"Reply '{replyId}' has no parent post; nothing to remove.");

        var row = await session.Query<ReplyTranslation>()
            .Where(t => t.ReplyId == replyId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"Reply '{replyId}' has no translation in '{languageCode}'; nothing to remove.");

        var via = ResolveTranslationStanding(
            parent.GroupId.Length > 0, parent.ComponentId, reply.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the reply's author (or a moderator of the community, or an admin) " +
                "may remove a translation of it.");

        var now = DateTimeOffset.UtcNow;
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "replytranslation.remove",
            TargetKind = "reply",
            TargetId = replyId,
            Via = via.Value,
            Outcome = Authorization.AccessOutcome.Allow
        };

        session.Delete(row);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The public ADR 0022 standing probe the Web layer calls to decide whether
    /// to render the "add a translation" affordance (a <b>display</b> pin, not a
    /// gate — the real deny is the <see cref="AddPostTranslationAsync"/> /
    /// <see cref="AddReplyTranslationAsync"/> standing check, which re-runs the
    /// same rule server-side). Keeping this in Core — not duplicated in the two
    /// detail controllers — is the ADR 0006-D "the service owns the decision,
    /// not the Web" shape; it delegates to the same
    /// <see cref="ResolveTranslationStanding"/> the write lanes use, so the
    /// display and the gate can never drift apart. ADR 0048: the edit /
    /// remove lanes (the same standing matrix, re-derived from the parent)
    /// reuse this exact display pin, so the "add a …" / "edit …" / "remove …"
    /// affordances render under the same standing — the display and the
    /// three gates can never drift apart.
    /// </summary>
    public static bool CanAddTranslation(
        bool isGroupLane, string componentId, string rowAuthorId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveTranslationStanding(isGroupLane, componentId, rowAuthorId, actorId, actorRoles) is not null;

    /// <summary>
    /// The ADR 0022 translation standing resolver (shared by
    /// <see cref="AddPostTranslationAsync"/> /
    /// <see cref="AddReplyTranslationAsync"/> /
    /// <see cref="CanAddTranslation"/>). Returns the <see cref="AccessVia"/>
    /// the actor qualifies under, or <c>null</c> to deny. Precedence (most
    /// specific standing first, so the audit row records the narrowest right
    /// that applied): the row's **author** (
    /// <see cref="AccessVia.Owner"/>); a **GlobalAdmin**
    /// (<see cref="AccessVia.Admin"/>); and, on the **community** lane only
    /// (a <see cref="Kumunita.Core.Identity.Roles.ModeratorComponent"/> claim
    /// scoped to the post's component) — a **Moderator**
    /// (<see cref="AccessVia.Moderator"/>). The group lane has no
    /// component-moderator standing (ADR 0007).
    /// </summary>
    private static AccessVia? ResolveTranslationStanding(
        bool isGroupLane, string componentId, string rowAuthorId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.Equals(rowAuthorId, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;

        if (actorRoles.Contains(Identity.Roles.GlobalAdmin))
            return AccessVia.Admin;

        if (!isGroupLane
            && !string.IsNullOrEmpty(componentId)
            && actorRoles.Contains(Identity.Roles.ModeratorComponent(componentId)))
            return AccessVia.Moderator;

        return null;
    }

    /// <summary>
    /// <b>Reverse-lookup</b> read seam (RC U03, R·5): the first post (by
    /// <c>Created</c> ascending — deterministic) whose
    /// <see cref="Post.ImageIds"/> contains <paramref name="mediaId"/> — the
    /// serving route's owner resolution reads this (RC R·4). **Un-audited**
    /// (RC R·5 — the audit row belongs to the route's
    /// <see cref="IAuthorizationService.CanAsync"/>, not this read); null when
    /// no post references the id (the route 404s). Read-only — no write lane,
    /// no <c>actorId</c> parameter (there is no decision to log here).
    /// </summary>
    public async Task<Post?> FindPostByImageIdAsync(string mediaId)
    {
        if (string.IsNullOrEmpty(mediaId))
            throw new ArgumentException("A media id is required.", nameof(mediaId));

        await using var session = _store.QuerySession();
        return await session
            .Query<Post>()
            .Where(p => p.ImageIds.Contains(mediaId))
            .OrderBy(p => p.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>Reverse-lookup</b> read seam (RC U03, R·5): the first reply (by
    /// <c>Created</c> ascending) whose <see cref="PostReply.ImageIds"/>
    /// contains <paramref name="mediaId"/>. **Un-audited** (RC R·5 — the audit
    /// row belongs to the route's <see cref="IAuthorizationService.CanAsync"/>);
    /// null when no reply references the id (the route 404s). Read-only.
    /// </summary>
    public async Task<PostReply?> FindReplyByImageIdAsync(string mediaId)
    {
        if (string.IsNullOrEmpty(mediaId))
            throw new ArgumentException("A media id is required.", nameof(mediaId));

        await using var session = _store.QuerySession();
        return await session
            .Query<PostReply>()
            .Where(r => r.ImageIds.Contains(mediaId))
            .OrderBy(r => r.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>Reverse-lookup</b> read seam (ATT U3, C-ATT·4): the first post (by
    /// <c>Created</c> ascending — deterministic) whose
    /// <see cref="Post.AttachmentIds"/> contains <paramref name="mediaId"/> —
    /// the attachment serving route's owner resolution reads this (C-ATT·7).
    /// **Un-audited** (the audit row belongs to the route's
    /// <see cref="IAuthorizationService.CanAsync"/>, not this read); null when
    /// no post references the id (the route 404s). Read-only — no write lane,
    /// no <c>actorId</c> parameter (there is no decision to log here).
    /// </summary>
    public async Task<Post?> FindPostByAttachmentIdAsync(string mediaId)
    {
        if (string.IsNullOrEmpty(mediaId))
            throw new ArgumentException("A media id is required.", nameof(mediaId));

        await using var session = _store.QuerySession();
        return await session
            .Query<Post>()
            .Where(p => p.AttachmentIds.Contains(mediaId))
            .OrderBy(p => p.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>Reverse-lookup</b> read seam (ATT U3, C-ATT·4): the first reply (by
    /// <c>Created</c> ascending) whose <see cref="PostReply.AttachmentIds"/>
    /// contains <paramref name="mediaId"/>. **Un-audited** (the audit row
    /// belongs to the route's <see cref="IAuthorizationService.CanAsync"/>);
    /// null when no reply references the id (the route 404s). Read-only.
    /// </summary>
    public async Task<PostReply?> FindReplyByAttachmentIdAsync(string mediaId)
    {
        if (string.IsNullOrEmpty(mediaId))
            throw new ArgumentException("A media id is required.", nameof(mediaId));

        await using var session = _store.QuerySession();
        return await session
            .Query<PostReply>()
            .Where(r => r.AttachmentIds.Contains(mediaId))
            .OrderBy(r => r.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }
}
