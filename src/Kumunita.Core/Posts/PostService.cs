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
    /// A post's detail + its one-level replies (F10, §2.4): one
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
    /// </summary>
    public async Task<Post> CreatePostAsync(PostDraft draft, string actorId, IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An authoring actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

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
}
