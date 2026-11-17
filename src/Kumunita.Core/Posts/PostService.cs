using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.Translations;
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
public partial class PostService
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

    // M6 (ADR 0076, plan U04) — the M6 notification emitter seam (F1 post-reply +
    // F2 group-post). **Optional** (nullable default) so the existing test call
    // sites that construct `PostService` positionally (userInfo, authz, store,
    // [tags]) keep compiling unchanged — the same CS1736 shape as `_tags` above.
    // The DI registration passes the live `Notifications.NotificationService`;
    // the frozen service's `EmitAsync` surface is untouched (unit-series rule 4).
    private readonly INotificationEmitter? _notifications;

    // M36 (ADR 0165, U04) — the mention-resolution read seam (the U03
    // `Mentions.IMentionService`, the M36·1 / M36·2 / M36·9 pins). **Optional**
    // (nullable default, the same CS1736 shape as `_tags` / `_notifications`)
    // so the existing test call sites that construct `PostService` positionally
    // keep compiling unchanged. The DI registration passes the live
    // `Mentions.MentionService` (composing the frozen `DirectoryService` read).
    private readonly Mentions.IMentionService? _mentions;

    // ADR 0288 (IMPROVE-CODE C06) — the shared translation-variant write lane (a D6-legal
    // `record` in the Translations module). The six post/reply write methods below are thin
    // forwarders into these two lanes; the surface-specific data (standing matrix, the row's
    // title/body shape, the reply's parent-post standing re-check) lives in the descriptor's
    // functions, verbatim from the pre-C06 bodies. The read seams (Get*TranslationsAsync) stay
    // here — their shape is surface-specific (the reply read is batched); the lane is the write seam.
    public TranslationLane<Post, PostTranslation> PostTranslationLane { get; }
    public TranslationLane<PostReply, ReplyTranslation> ReplyTranslationLane { get; }

    public PostService(IUserInfoService userInfo, IAuthorizationService authz, IDocumentStore store,
        ITagService? tags = null,
        INotificationEmitter? notifications = null,
        Mentions.IMentionService? mentions = null)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _tags = tags;
        _notifications = notifications;
        _mentions = mentions;
        // C3-03 — the group-post lane (the 4 group-post methods live on this
        // seam; PostService keeps thin forwarders for the C08 test-compat
        // facade). Same deps as PostService's group-post bodies used.
        _groupPosts = new GroupPostService(store, authz, notifications, mentions);

        PostTranslationLane = new TranslationLane<Post, PostTranslation>(new TranslationDescriptor<Post, PostTranslation>
        {
            TargetKind = "post",
            ActionPrefix = "posttranslation",
            Noun = "Post",
            LoadStandingExtra = (_, _) => Task.FromResult<object?>(null),
            ResolveStanding = (Post p, object? extra, string actorId, IReadOnlySet<string> actorRoles) =>
                PostTranslationPolicy.ResolveTranslationStanding(p.GroupId.Length > 0, p.ComponentId, p.AuthorId, actorId, actorRoles),
            LoadParent = (session, id) => session.LoadAsync<Post>(id),
            Find = (session, id, lang) => session.Query<PostTranslation>().Where(t => t.PostId == id && t.LanguageCode == lang).FirstOrDefaultAsync(),
            Create = (id, lang, title, body, actor, created) => new PostTranslation
            {
                Id = Guid.NewGuid().ToString("N"), PostId = id, LanguageCode = lang,
                Title = title, Body = body!, AuthorId = actor, Created = created,
            },
            ApplyUpdate = (row, title, body, _, _) =>
            {
                row.Title = string.IsNullOrWhiteSpace(title) ? null : title;
                row.Body = body!;
            },
        });

        ReplyTranslationLane = new TranslationLane<PostReply, ReplyTranslation>(new TranslationDescriptor<PostReply, ReplyTranslation>
        {
            TargetKind = "reply",
            ActionPrefix = "replytranslation",
            Noun = "Reply",
            LoadStandingExtra = async (IDocumentSession session, PostReply reply) =>
            {
                var parent = await session.LoadAsync<Post>(reply.PostId);
                if (parent is null)
                    throw new KeyNotFoundException($"Reply '{reply.Id}' has no parent post; nothing to translate.");
                return (object)parent;
            },
            ResolveStanding = (PostReply reply, object? extra, string actorId, IReadOnlySet<string> actorRoles) =>
            {
                // The reply itself has no component (C-M3·1) — its standing reads the parent
                // post's group/component scope and the reply's own AuthorId.
                var parent = (Post)extra!;
                return PostTranslationPolicy.ResolveTranslationStanding(
                    parent.GroupId.Length > 0, parent.ComponentId, reply.AuthorId, actorId, actorRoles);
            },
            LoadParent = (session, id) => session.LoadAsync<PostReply>(id),
            Find = (session, id, lang) => session.Query<ReplyTranslation>().Where(t => t.ReplyId == id && t.LanguageCode == lang).FirstOrDefaultAsync(),
            Create = (id, lang, _, body, actor, created) => new ReplyTranslation
            {
                Id = Guid.NewGuid().ToString("N"), ReplyId = id, Body = body!, AuthorId = actor, Created = created,
            },
            ApplyUpdate = (row, _, body, _, _) => { row.Body = body!; },
        });
    }

    /// <summary>
    /// M26 (C-SORT·2/5) — the post-feed ordering for the three feed seams
    /// (community / all-sections / group): a <c>null</c> spec keeps the pinned
    /// <c>OrderByDescending(p => p.Created)</c> line **byte-for-byte** (C-SORT·2);
    /// a non-null spec applies the closed post-feed allowlist
    /// (U2 §2.2 rows 1–3: <c>created</c> / <c>modified</c> / <c>title</c>) and
    /// ends with the <c>.ThenBy(p => p.Id)</c> tie-breaker (C-SORT·5).
    /// <para>
    /// <b>Marten 9.31.2 drift (U4):</b> the frozen §2.2 comparator rules pin
    /// <c>?? MinValue</c> (nullable dates) and <c>?? ""</c> (nullable strings)
    /// sentinels inside the OrderBy key — but Marten's Linq parser rejects both
    /// (<c>BadLinqExpressionException: Invalid OrderBy() expression</c>). The
    /// bare nullable member is what Marten accepts, so the <c>modified</c> and
    /// <c>title</c> keys order on the raw column and Postgres supplies the
    /// null-ordering (nulls-first in desc, nulls-last in asc — the **opposite**
    /// of the pinned sentinels). This is a documented deviation from Part 2's
    /// comparator rules, forced by the persistence provider, not a design
    /// choice; the <c>OrdinalIgnoreCase</c> comparator on <c>title</c> is
    /// preserved (it applies to the non-null comparison).
    /// </para>
    /// <para>
    /// The <c>ThenBy</c> is invoked fully-qualified as
    /// <c>Queryable.ThenBy(…)</c> — on Marten's
    /// <c>IAsyncQueryable</c>/<c>IOrderedAsyncQueryable</c> the unqualified
    /// <c>.ThenBy(…)</c> form is ambiguous with an async-enumerable
    /// extension, so the fully-qualified <c>System.Linq.Queryable</c> overload
    /// is the one that resolves.
    /// </para>
    /// </summary>
    private static IQueryable<Post> OrderByPostSort(IQueryable<Post> q, SortSpec? sort)
    {
        if (sort is null)
            return q.OrderByDescending(p => p.Created); // ← the pinned line, verbatim

        // C-SORT·5 — the .ThenBy(p => p.Id) tie-breaker on every non-null sort
        // path; the primary key's comparator is the surface's rule (U2 §2.2
        // rows 1–3: created direct, modified nullable→bare, title nullable→bare
        // with OrdinalIgnoreCase — the ?? sentinels are a Marten 9.31.2 drift,
        // see the doc-comment above).
        return sort.Key switch
        {
            "created" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(p => p.Created), p => p.Id)
                : Queryable.ThenBy(q.OrderBy(p => p.Created), p => p.Id),
            "modified" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(p => p.Modified), p => p.Id)
                : Queryable.ThenBy(q.OrderBy(p => p.Modified), p => p.Id),
            "title" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(p => p.Title, StringComparer.OrdinalIgnoreCase), p => p.Id)
                : Queryable.ThenBy(q.OrderBy(p => p.Title, StringComparer.OrdinalIgnoreCase), p => p.Id),
            _ => Queryable.ThenBy(q.OrderByDescending(p => p.Created), p => p.Id), // C-SORT·1 — unreachable (Parse already fell back); the default is pinned anyway
        };
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
    public async Task<FeedResult> ListFeedAsync(string componentId, string actorId, int page,
        SortSpec? sort = null, int? pageSize = null)
    {
        if (string.IsNullOrEmpty(componentId)) throw new ArgumentException("A component feed requires a componentId.", nameof(componentId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));
        if (page < 1) page = 1;

        // The resident's items-per-page override (the Web resolves
        // Profile.PageSize → PageSizer.Resolve and passes it); null ⇒ the
        // class's historical PageSize constant (the pre-override behavior).
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        IQueryable<Post> q = session
            .Query<Post>()
            .Where(p => p.ComponentId == componentId && p.DeletedAt == null && !p.IsDraft && p.Status == PostStatus.Active);

        var candidates = await OrderByPostSort(q, sort)
            .Skip((page - 1) * ps)
            .Take(ps)
            .ToListAsync()
            .ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return runs **before** any
        // decision and **before** the D2 CountAsync: an oversized page is a
        // no-decision, Total: 0, HasMore: false.
        if (candidates.Count == 0)
            return new FeedResult(Visible: Array.Empty<Post>(), HiddenCount: 0, Page: page, Total: 0, HasMore: false);

        // M7 (ADR 0090 D2 / C-M7·7) — the candidate-set count (pre-decision,
        // post-filter), one CountAsync over the same filtered query (the
        // page's window is non-empty here, so the component has candidates).
        int candidateCount = await session
            .Query<Post>()
            .Where(p => p.ComponentId == componentId && p.DeletedAt == null && !p.IsDraft && p.Status == PostStatus.Active)
            .CountAsync()
            .ConfigureAwait(false);

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

        return new FeedResult(Visible: visible, HiddenCount: visibleSet.HiddenCount, Page: page, Total: candidateCount, HasMore: candidates.Count == ps);
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
            IReadOnlyCollection<string> componentIds, string actorId, int page,
            SortSpec? sort = null, int? pageSize = null)
    {
        if (componentIds is null) throw new ArgumentNullException(nameof(componentIds));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));
        if (page < 1) page = 1;

        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        if (componentIds.Count == 0)
            return new FeedResult(Visible: Array.Empty<Post>(), HiddenCount: 0, Page: page, Total: 0, HasMore: false);

        await using var session = _store.QuerySession();
        IQueryable<Post> q = session
            .Query<Post>()
            .Where(p => componentIds.Contains(p.ComponentId) && p.DeletedAt == null && !p.IsDraft && p.Status == PostStatus.Active);

        var candidates = await OrderByPostSort(q, sort)
            .Skip((page - 1) * ps)
            .Take(ps)
            .ToListAsync()
            .ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return runs **before** any
        // decision and **before** the D2 CountAsync: an oversized page is a
        // no-decision, Total: 0, HasMore: false.
        if (candidates.Count == 0)
            return new FeedResult(Visible: Array.Empty<Post>(), HiddenCount: 0, Page: page, Total: 0, HasMore: false);

        // M7 (ADR 0090 D2 / C-M7·7) — the candidate-set count (pre-decision,
        // post-filter), one CountAsync over the same filtered query.
        int candidateCount = await session
            .Query<Post>()
            .Where(p => componentIds.Contains(p.ComponentId) && p.DeletedAt == null && !p.IsDraft && p.Status == PostStatus.Active)
            .CountAsync()
            .ConfigureAwait(false);

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

        return new FeedResult(Visible: visible, HiddenCount: visibleSet.HiddenCount, Page: page, Total: candidateCount, HasMore: candidates.Count == ps);
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

        // A01 (IMPROVE-UX, the E1/E2 leak fix) — the reads honour
        // <see cref="PostStatus"/>. A <see cref="PostStatus.Hidden"/> /
        // <see cref="PostStatus.Removed"/> post is no longer served to an
        // ordinary reader who reaches it by a direct <c>/posts/{id}</c> URL:
        // the **author** (the "owner branch" — they see the "hidden by a
        // moderator" placeholder, the ADR 0024 author-sees-its-own-deleted-post
        // precedent) and a **moderator** (the report lane's hide/remove/unhide
        // lever) / break-glass still see it; any other reader gets the same
        // <c>Post = null</c> fail-closed shape as a Deny (the Web 403 / R01·R02
        // status page — the repo's "403 on denied, 404 leaks which ids are real"
        // pin). The feeds + search already exclude non-Active (the candidate
        // filter above); this is the detail-lane half of the same rule. The
        // method's existing return-null contract (not a
        // <see cref="KeyNotFoundException"/>) is preserved: the ~25 call sites
        // that check <c>Post is null</c> already map to the fail-closed shape,
        // and a hidden post must not leak *which* id was real (a 404 would).
        if (post.Status != PostStatus.Active)
        {
            var privileged = string.Equals(post.AuthorId, actorId, StringComparison.Ordinal)
                || decision.Via is AccessVia.Moderator or AccessVia.BreakGlass;
            if (!privileged)
                return new PostDetailResult(Post: null, Replies: Array.Empty<PostReply>());
        }

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
        // SEC-READ: owner-only — posting-right gate reads the actor's own effective community set (GetEffectiveCommunityIdsAsync); the write is the actor's own post
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
            // The EFFECTIVE community set (the GetEffectiveCommunityIdsAsync
            // seam) — raw membership MINUS the guardian's per-community block.
            // A supervised child whose guardian blocked this community (a
            // mandatory one included — ADR 0012's "cannot remove" is bypassed
            // by "block") is denied the posting right, even though their
            // implicit/explicit membership still shows in the raw read.
            var communities = await _userInfo
                .GetEffectiveCommunityIdsAsync(actorId)
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

        // ADR 0084 — the community-post emitter (opt-OUT kind: the kind is on
        // by default — every member of the community is notified; the resident
        // can store an Enabled=false subscription row for the target community
        // id to opt out). Recipient universe: the community's members (via
        // ComponentMembership), excluding the author. The IsDraft pin is the
        // ADR 0037 intake-honesty gate (a draft is invisible to all but the
        // author, so a notification for it would leak intake). One
        // SaveChangesAsync commits the post + the inbox rows atomically (C3).
        if (_notifications is not null && !(draft.IsDraft ?? false))
        {
            var members = await session.Query<UserInfo.ComponentMembership>()
                .Where(m => m.ComponentId == draft.ComponentId)
                .Select(m => m.UserId)
                .ToListAsync().ConfigureAwait(false);
            foreach (var member in members)
            {
                if (string.Equals(member, actorId, StringComparison.Ordinal))
                    continue;                                   // the post's author does not notify themselves
                await _notifications.EmitAsync(
                    session,
                    recipientId: member,
                    kind: NotificationKinds.CommunityPost,
                    idempotencyKey: $"notification:community.post:{post.Id}:{member}",
                    body: UgcSnippets.Truncate(post.Body),
                    linkPath: $"/posts/{post.Id}",
                    targetId: draft.ComponentId,
                    ct: default).ConfigureAwait(false);
            }
        }

        // M36 (ADR 0165, U04) — the mention emitter (the reserved
        // `post.mention` kind going live, M36·3 / M36·4). Additive, *inside*
        // the lane (the frozen method signature + audit rows untouched):
        // extract candidate `@`-tokens from the post body, resolve them
        // against the frozen directory read (the author is never a
        // recipient — M36-5 self pin), and emit one `post.mention`
        // notification per resolved recipient on the caller's session. The
        // per-recipient idempotency key (M36·4) keeps a re-emission of the
        // same (post, recipient) pair a no-op (ADR 0076 D4 / F10). The draft
        // gate (M36·8, ADR 0037) — a draft save runs no mention emitter; the
        // mention fires only when the post is published (the `IsDraft` pin).
        // `EmitAsync` does not commit; the `SaveChangesAsync` below is the
        // single commit (C3). No new authorization surface, no `AccessAudit`
        // row (M36·5 — a mention is a personal read).
        if (_mentions is not null && _notifications is not null && !(draft.IsDraft ?? false))
        {
            var mentionTokens = Mentions.MentionTokens.ExtractTokens(post.Body);
            if (mentionTokens.Count > 0)
            {
                var mentionRecipients = await _mentions.ResolveAsync(mentionTokens, actorId)
                    .ConfigureAwait(false);
                foreach (var recipientId in mentionRecipients)
                {
                    await _notifications.EmitAsync(
                        session,
                        recipientId: recipientId,
                        kind: NotificationKinds.PostMention,
                        idempotencyKey: $"notification:post.mention:{post.Id}:{recipientId}",
                        body: UgcSnippets.Truncate(post.Body),
                        targetId: null,
                        linkPath: $"/posts/{post.Id}",
                        ct: default).ConfigureAwait(false);
                }
            }
        }

        await session.SaveChangesAsync().ConfigureAwait(false);

        // TG (ADR 0044, U8b) — the tag attach lane (C3 single-transaction
        // idiom: the write + the audit row commit atomically on the **same**
        // <c>IDocumentSession</c> — the <c>PostTranslationLane.AddAsync</c>
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
        // SEC-READ: owner-only — author-only edit lane (ADR 0014; the load is followed by post.AuthorId == actorId, non-author throws)
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
        // SEC-READ: no disclosure — the parent-post load is to route the reply-mention notification to the parent author (internal routing, not a read surface)
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

        // M6 (ADR 0076, plan U04) — the F1 post-reply emitter. The **parent
        // post's** author is the recipient (the reply's author is the *sender*);
        // the UGC snippet is the reply's own body in the reply's authored language
        // (ADR 0018 — the content's own language, not the recipient's). The
        // idempotency key is the §6.3 `notification:post.reply:{replyId}` shape
        // (D4, F10). `EmitAsync` runs on the caller's session and does **not**
        // commit — the `SaveChangesAsync` below is the single commit (C3). The
        // parent post is loaded from the caller's session (the reply's parent
        // must exist for the reply to be meaningful — a missing parent is a
        // caller error, surfaced as a 404 via the Web layer's exception mapping).
        if (_notifications is not null)
        {
            var parentPost = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
            if (parentPost is not null && !string.IsNullOrWhiteSpace(parentPost.AuthorId)
                && !string.Equals(parentPost.AuthorId, actorId, StringComparison.Ordinal))
            {
                await _notifications.EmitAsync(
                    session,
                    recipientId: parentPost.AuthorId,
                    kind: NotificationKinds.PostReply,
                    idempotencyKey: $"notification:post.reply:{reply.Id}",
                    body: TruncateUgcSnippet(reply.Body),
                    targetId: null,
                    linkPath: $"/posts/{postId}#reply-{reply.Id}",
                    ct: default).ConfigureAwait(false);
            }
        }

        // M36 (ADR 0165, U04 + U05) — the mention emitter on the reply
        // surface, **lane-aware** (M36·3 / M36·4). The reply lane is shared
        // by community replies (Part 2.3 lane 2, `post.mention`) and group
        // post replies (Part 2.3 lane 4, `group.post.mention`) — the code has
        // a **single** `CreateReplyAsync` (no separate group-reply method), so
        // the parent post's `GroupId` (non-empty ⇒ group lane) selects the
        // kind + the `linkPath`. Additive, *inside* the lane (the frozen
        // method signature + audit rows untouched): extract candidate
        // `@`-tokens from the reply body, resolve them against the frozen
        // directory read (the author is never a recipient — M36-5 self pin),
        // and emit one mention notification per resolved recipient on the
        // caller's session. The per-recipient idempotency key (M36·4) keeps a
        // re-emission of the same (reply, recipient) pair a no-op (ADR 0076
        // D4 / F10); the key's kind + the `linkPath` are the lane-selected
        // values above. `EmitAsync` does not commit; the `SaveChangesAsync`
        // below is the single commit (C3). No new authorization surface, no
        // `AccessAudit` row (M36·5 — a mention is a personal read).
        if (_mentions is not null && _notifications is not null)
        {
            var mentionTokens = Mentions.MentionTokens.ExtractTokens(reply.Body);
            if (mentionTokens.Count > 0)
            {
                // Lane detection (Part 2.3 lane 2 vs. lane 4): the parent
                // post's GroupId distinguishes a community reply (empty ⇒
                // `post.mention`, `/posts/{postId}#reply-{replyId}`) from a
                // group post reply (non-empty ⇒ `group.post.mention`,
                // `/groups/{groupId}/posts/{postId}#reply-{replyId}`).
                var replyParent = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
                var isGroupLane = replyParent is not null && !string.IsNullOrEmpty(replyParent.GroupId);
                var mentionKind = isGroupLane
                    ? NotificationKinds.GroupPostMention
                    : NotificationKinds.PostMention;
                var mentionLinkPath = isGroupLane
                    ? $"/groups/{replyParent!.GroupId}/posts/{postId}#reply-{reply.Id}"
                    : $"/posts/{postId}#reply-{reply.Id}";
                var mentionRecipients = await _mentions.ResolveAsync(mentionTokens, actorId)
                    .ConfigureAwait(false);
                foreach (var recipientId in mentionRecipients)
                {
                    await _notifications.EmitAsync(
                        session,
                        recipientId: recipientId,
                        kind: mentionKind,
                        idempotencyKey: $"notification:{mentionKind}:{reply.Id}:{recipientId}",
                        body: UgcSnippets.Truncate(reply.Body),
                        targetId: null,
                        linkPath: mentionLinkPath,
                        ct: default).ConfigureAwait(false);
                }
            }
        }

        await session.SaveChangesAsync().ConfigureAwait(false);
        return reply;
    }

    // ─── M6 (ADR 0076) — the UGC-snippet truncation (U04 deliverable) ──────

    /// <summary>
    /// The M6 UGC snippet (the U04 deliverable — "truncated to ~200 chars"). The
    /// snippet is the **sender's** authored content (ADR 0018 — the content's own
    /// language, not the recipient's); it is appended to the recipient's
    /// localized template by <c>NotificationService.EmitAsync</c> (C-M6·6). A
    /// long body is trimmed to ~200 chars + an ellipsis so the email / inbox row
    /// does not carry the whole message.
    /// </summary>
    private static string TruncateUgcSnippet(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = text.Trim();
        return text.Length <= 200 ? text : text[..200] + "…";
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
        // SEC-READ: owner-only — author-only reply-edit lane (ADR 0016; the load is followed by reply.AuthorId == actorId, non-author throws)
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

    // ─── ADR 0016 — author-only group-post edit lane: C3-03 — the
    //     UpdateGroupPostAsync body lives on <see cref="GroupPostService"/>
    //     (the forwarder is with the other 3 group-post forwarders below). ───

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
        // SEC-READ: owner-only — author-only soft-delete lane (ADR 0024; the load is followed by post.AuthorId == actorId, non-author throws)
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
        // SEC-READ: owner-only — author-only reply-soft-delete lane (ADR 0024; the load is followed by reply.AuthorId == actorId, non-author throws)
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
        // SEC-READ: owner-only — author-only draft publish lane (ADR 0037; the load is followed by post.AuthorId == actorId, non-author throws)
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
        // SEC-READ: owner-only — the read is scoped to the actor's own drafts (p.AuthorId == actorId, ADR 0037 drafts invisible to all but the author)
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
    /// Unhide a post (A03; the inverse of <see cref="HidePostAsync"/> —
    /// restores a hidden post to the feed). Same
    /// <see cref="AccessAction.Moderate"/>-gated write-lane shape as
    /// <see cref="HidePostAsync"/> / <see cref="RemovePostAsync"/>: the
    /// <c>CanAsync(Moderate)</c> decision runs **before** any write, in the
    /// **same** <c>IDocumentSession</c> transaction as the <c>Status</c>
    /// write (C3 same-transaction; ADR 0006-C: audit always on — Allow
    /// *and* Deny); a denied call is **not executed at all** (no
    /// <c>Status</c> write, no partial state) — the audit row still commits
    /// in the caller's <c>SaveChangesAsync</c> (C3). Does **not** undo a
    /// <see cref="PostStatus.Removed"/> post (the F4 hard lane is
    /// irreversible by design — a <c>Removed</c> post stays
    /// <c>Removed</c>; the reversible lever only reverses <c>Hidden</c>).
    /// </summary>
    public async Task UnhidePostAsync(string postId, string actorId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(postId)) throw new ArgumentException("A post id is required.", nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("A moderating actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{postId}' was not found in the session; nothing to unhide.");

        // C3 / ADR 0006-C — audit row always written (Allow or Deny), in the
        // caller's transaction. The decision gate runs *before* any write.
        var decision = await _authz.CanAsync(actorId, AccessAction.Moderate,
                new PostToAuditableResource(post), session)
            .ConfigureAwait(false);

        if (decision.Allowed)
        {
            post.Status = PostStatus.Active;
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

    // ─── group posts (ADR 0013) — C3-03: the 4-method group-post surface
    //     (ListGroupFeedAsync / GetGroupPostAsync / CreateGroupPostAsync /
    //     UpdateGroupPostAsync) extracted to <see cref="GroupPostService"/>
    //     (a real part-vs-whole surface; the C3-00 EventRsvpGuardianService
    //     pattern). The Posts DI registration supplies the live instance.
    //     PostService keeps these as thin forwarders (the C08 test-compat
    //     facade pattern — ProjectService still implements ITodoService /
    //     IBoardService via sibling forwarders) so the existing Core tests
    //     that construct a PostService and call the group lane run unchanged.
    //     The Web layer resolves <see cref="IGroupPostService"/> directly. ───

    /// <summary>
    /// The <see cref="GroupPostService"/> lane this class's 4 group-post
    /// forwarders delegate to (C3-03). Built once from the same deps
    /// (<see cref="_store"/> / <see cref="_authz"/> / <see cref="_notifications"/>
    /// / <see cref="_mentions"/>) so the forwarders are behaviour-identical to
    /// the pre-extraction bodies.
    /// </summary>
    private readonly GroupPostService _groupPosts;

    /// <summary>
    /// A group's channel feed (ADR 0013) — delegates to
    /// <see cref="GroupPostService.ListGroupFeedAsync"/> (C3-03 extraction;
    /// the body lives on the <see cref="GroupPostService"/> seam).
    /// </summary>
    public Task<FeedResult> ListGroupFeedAsync(string groupId, string actorId, int page,
        SortSpec? sort = null, int? pageSize = null)
        => _groupPosts.ListGroupFeedAsync(groupId, actorId, page, sort, pageSize);

    /// <summary>
    /// A group post's detail + replies (ADR 0013) — delegates to
    /// <see cref="GroupPostService.GetGroupPostAsync"/> (C3-03 extraction).
    /// </summary>
    public Task<PostDetailResult> GetGroupPostAsync(string groupId, string postId, string actorId)
        => _groupPosts.GetGroupPostAsync(groupId, postId, actorId);

    /// <summary>
    /// Creates a group post (ADR 0013) — delegates to
    /// <see cref="GroupPostService.CreateGroupPostAsync"/> (C3-03 extraction).
    /// </summary>
    public Task<Post> CreateGroupPostAsync(GroupPostDraft draft, string actorId, IDocumentSession session)
        => _groupPosts.CreateGroupPostAsync(draft, actorId, session);

    /// <summary>
    /// Re-write a group-lane post (author-only, ADR 0016) — delegates to
    /// <see cref="GroupPostService.UpdateGroupPostAsync"/> (C3-03 extraction).
    /// </summary>
    public Task<Post> UpdateGroupPostAsync(
        string postId, string actorId, string? title, string body, string? languageCode,
        IDocumentSession session, IReadOnlyList<string>? attachmentIds = null)
        => _groupPosts.UpdateGroupPostAsync(postId, actorId, title, body, languageCode, session, attachmentIds);

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
        // SEC-READ: caller-gated by PostsController.Detail / GroupsController.GroupPostDetail / HomeController.Index (the parent post's Read decision, already made by the caller — C-M3·1)
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
    /// The **read** seam for the feed cards' reply counts (ADR 0226): for each
    /// post id in <paramref name="postIds"/> that has at least one reply, the
    /// number of <see cref="PostReply"/> documents. One query for the whole page
    /// (the "one query per surface" preference, like
    /// <see cref="GetReplyTranslationsAsync"/>); a post with no replies is simply
    /// absent from the result (callers read it as 0). A soft-deleted reply
    /// (ADR 0024) still counts — the parent's reply count stays honest, the same
    /// number the post detail's "Replies (N)" heading shows. Owns its
    /// <c>QuerySession</c> (C3 read lane); **not** an authorization surface and
    /// writes **no** audit row (C-M3·1 — a reply inherits the parent post's single
    /// <c>Read</c> decision, already made by the feed).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> CountRepliesAsync(IReadOnlyCollection<string> postIds)
    {
        // SEC-READ: no disclosure — returns only post-id → reply-count; no audience-bearing field is exposed (ADR 0226 feed-card count)
        ArgumentNullException.ThrowIfNull(postIds);
        if (postIds.Count == 0)
            return new Dictionary<string, int>();

        await using var session = _store.QuerySession();
        // TODO (ADR 0226): if a page ever carries many replies, move the grouping
        // into the query; for a feed page (<= PageSize posts) one projected column is fine.
        var owners = await session
            .Query<PostReply>()
            .Where(r => postIds.Contains(r.PostId))
            .Select(r => r.PostId)
            .ToListAsync()
            .ConfigureAwait(false);

        return owners
            .GroupBy(id => id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
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
        // SEC-READ: caller-gated by PostsController.Detail / GroupsController.GroupPostDetail (the parent post's Read decision, already made by the caller — C-M3·1)
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

    // ─── ADR 0022 + 0048 — post / reply translation write lanes ─────────────
    // C2-01 (IMPROVE-CODE-2): the six per-surface `*Translation*` write methods
    // (add / update / remove × post / reply) are retired. Their body was a one-
    // line forwarder into the shared <see cref="TranslationLane{TParent,TVar}"/>
    // (ADR 0288); the Web layer now codes against <see cref="PostTranslationLane"/>
    // and <see cref="ReplyTranslationLane"/> directly (a D6-legal `record` in the
    // Translations module, so pointing the controller at it adds no boundary edge).
    // The standing matrix, the audit-row shape, the (parent, language) row shape,
    // and the caller-supplied session are all defined once in the descriptor —
    // byte-identical to the retired forwarders. The read seams
    // (<see cref="GetPostTranslationsAsync"/> / <see cref="GetReplyTranslationsAsync"/>)
    // and the <see cref="CanAddTranslation"/> display pin stay on this service:
    // their shape is surface-specific (the reply read is batched; the display pin
    // is the C04 seam the Web calls), which the write lane intentionally does not own.

    /// <summary>
    /// The public ADR 0022 standing probe the Web layer calls to decide whether
    /// to render the "add a translation" affordance (a <b>display</b> pin, not a
    /// gate — the real deny is the <see cref="PostTranslationLane"/> /
    /// <see cref="ReplyTranslationLane"/> standing re-check, which re-runs the
    /// same rule server-side). C04: the rule moved to
    /// <see cref="PostTranslationPolicy.CanAddTranslation"/> — a thin forwarder
    /// here keeps the call surface unchanged; the Web no longer reaches the
    /// concrete service for the decision. ADR 0048: the edit / remove lanes
    /// (the two lanes' <c>UpdateAsync</c> / <c>RemoveAsync</c>) reuse this exact
    /// display pin, so the "add a …" / "edit …" / "remove …" affordances render
    /// under the same standing.
    /// </summary>
    public static bool CanAddTranslation(
        bool isGroupLane, string componentId, string rowAuthorId, string actorId, IReadOnlySet<string> actorRoles)
        => PostTranslationPolicy.CanAddTranslation(isGroupLane, componentId, rowAuthorId, actorId, actorRoles);

}
