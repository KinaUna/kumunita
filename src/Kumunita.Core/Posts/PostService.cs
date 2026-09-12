using Kumunita.Core.Authorization;
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

    public PostService(IUserInfoService userInfo, IAuthorizationService authz, IDocumentStore store)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _store = store ?? throw new ArgumentNullException(nameof(store));
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
            .Where(p => p.ComponentId == componentId)
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
            .Where(p => componentIds.Contains(p.ComponentId))
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
            Created = DateTimeOffset.UtcNow
        };

        session.Store(post);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return post;
    }

    /// <summary>
    /// Creates a one-level reply in the **caller's** in-flight session (invariant
    /// C3). The reply carries **no** <c>Audience</c> (C-M3·1): the parent's
    /// <c>Read</c> decision has already been made by the caller (the Web layer's
    /// detail path / <see cref="GetPostAsync"/>) — this method does **not**
    /// re-check and does **not** write an audit row of its own. One
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task<PostReply> CreateReplyAsync(string postId, string actorId, string body, IDocumentSession session)
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
            Created = DateTimeOffset.UtcNow
        };

        session.Store(reply);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return reply;
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
            .Where(p => p.GroupId == groupId)
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
            Created = DateTimeOffset.UtcNow
        };

        session.Store(post);
        // One SaveChangesAsync — the C3 same-transaction lane (ADR 0006-E): the
        // gate decision row + the new post commit atomically.
        await session.SaveChangesAsync().ConfigureAwait(false);
        return post;
    }
}
