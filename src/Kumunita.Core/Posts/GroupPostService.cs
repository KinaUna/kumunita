using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Mentions;
using Kumunita.Core.Notifications;
using Kumunita.Core.Query;
using Kumunita.Core.UserInfo;
using Marten;
using Npgsql;

namespace Kumunita.Core.Posts;

/// <summary>
/// The <c>group-post</c> lane (ADR 0013, C3-03) — the 4-method group-post
/// surface extracted from <see cref="PostService"/> as a real part-vs-whole
/// surface. A C13 cold read of this type passes on its own: the group-post
/// concern (the membership-lane decision <see cref="IAuthorizationService"/>
/// <c>CanSeeGroupAsync</c> / <c>CanSeeGroupFeedAsync</c>, the
/// <see cref="GroupPostDraft"/> write shape with <c>ComponentId = string.Empty</c>
/// + <c>Audience = new Audience()</c>, the per-member <see cref="INotificationEmitter"/>
/// emission (ADR 0076), the <see cref="Mentions.MentionTokens"/> mention
/// resolution (ADR 0165)) is a named domain concept with its own standing /
/// policy / guard — not a mechanical "extract N methods" ceremony.
/// <para>
/// **When:** every group-post read and write (the <c>GroupsController</c>
/// surface, the <c>KioskItemCatalog</c> detail read). The
/// <see cref="PostService"/> no longer owns these 4 methods — the
/// <c>Posts</c> DI registration supplies the live instance.
/// </para>
/// <para>
/// **No new boundary edge** (the seam is in the <c>Posts</c> module; the
/// controllers already reference <c>Posts</c> types — the gate's boundary
/// check does not rise). Core stays HTTP-free (ADR 0006-D).
/// </para>
/// </summary>
public sealed class GroupPostService : IGroupPostService
{
    private static readonly int PageSize = 30;

    private readonly IDocumentStore _store;
    private readonly IAuthorizationService _authz;
    private readonly INotificationEmitter? _notifications;
    private readonly Mentions.IMentionService? _mentions;

    public GroupPostService(
        IDocumentStore store,
        IAuthorizationService authz,
        INotificationEmitter? notifications = null,
        Mentions.IMentionService? mentions = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _notifications = notifications;
        _mentions = mentions;
    }

    /// <summary>
    /// M26 (C-SORT·2/5) — the post-feed ordering for the group feed: a
    /// <c>null</c> spec keeps the pinned <c>OrderByDescending(p => p.Created)</c>
    /// line **byte-for-byte**; a non-null spec applies the closed post-feed
    /// allowlist (created / modified / title) and ends with the
    /// <c>.ThenBy(p => p.Id)</c> tie-breaker (C-SORT·5).
    /// </summary>
    private static IQueryable<Post> OrderByPostSort(IQueryable<Post> q, SortSpec? sort)
    {
        if (sort is null)
            return q.OrderByDescending(p => p.Created); // ← the pinned line, verbatim

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
            _ => Queryable.ThenBy(q.OrderByDescending(p => p.Created), p => p.Id), // C-SORT·1 — unreachable
        };
    }

    /// <summary>
    /// The M6 UGC snippet (the U04 deliverable — "truncated to ~200 chars").
    /// A long body is trimmed to ~200 chars + an ellipsis so the email / inbox
    /// row does not carry the whole message.
    /// </summary>
    private static string TruncateUgcSnippet(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = text.Trim();
        return text.Length <= 200 ? text : text[..200] + "…";
    }

    /// <summary>
    /// ADR 0018 — the authored-in language tag: a blank submission falls back
    /// to the instance default (the <see cref="LocaleSettings"/> singleton
    /// row's <c>DefaultLanguageCode</c>) with <c>en</c> as the floor. The
    /// result is **always** a concrete BCP-47 code.
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
    /// A group's channel feed (G1–G4 FACES, design doc §2.3(a)): the candidate
    /// set is the group's posts — <c>Post.GroupId == groupId</c>,
    /// <c>Created desc</c>, paged with the class's existing <c>PageSize</c>
    /// (the 30-per-page shape is M3's, unchanged). Exactly one
    /// <see cref="IAuthorizationService.CanSeeGroupFeedAsync(string, string, int)"/>
    /// (the **standalone** form — a plain read with no in-flight caller
    /// transaction, the <see cref="PostService.ListFeedAsync"/> precedent)
    /// writes the visit's **single aggregate** <c>AccessAudit</c> row (G·5:
    /// TargetKind "grouppost", TargetId null, counts). Allow ⇒ the paged
    /// candidates (G1); Deny ⇒ a <see cref="FeedResult"/> with an **empty**
    /// visible list and <see cref="FeedResult.HiddenCount"/> = the candidate
    /// count (G2). **0 candidates ⇒** empty <see cref="FeedResult"/>, no
    /// decision, **no** row (the M3 <see cref="PostService.ListFeedAsync"/>
    /// 0-candidate shape). **No audience evaluation of any kind** (G·1/G·8 —
    /// membership is the sole decision).
    /// </summary>
    public async Task<FeedResult> ListGroupFeedAsync(string groupId, string actorId, int page,
        SortSpec? sort = null, int? pageSize = null)
    {
        if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("A group feed requires a groupId.", nameof(groupId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));
        if (page < 1) page = 1;

        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        IQueryable<Post> q = session
            .Query<Post>()
            .Where(p => p.GroupId == groupId && p.DeletedAt == null && !p.IsDraft);

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
            .Where(p => p.GroupId == groupId && p.DeletedAt == null && !p.IsDraft)
            .CountAsync()
            .ConfigureAwait(false);

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
            return new FeedResult(Visible: candidates, HiddenCount: 0, Page: page, Total: candidateCount, HasMore: candidates.Count == ps);

        // G2 — Deny: empty visible list, HiddenCount = the candidate count (the
        // aggregate Deny row **is** the audit evidence — G·1/G·5); never a
        // post's fields. HasMore is false on Deny (the design doc §7.4 pin —
        // the paging signal is computed from the page's window only on Allow;
        // a Deny visit has no visible page to page through).
        return new FeedResult(Visible: Array.Empty<Post>(), HiddenCount: candidates.Count, Page: page, Total: 0, HasMore: false);
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
    /// <see cref="PostReply"/> list **as-is** — the replies inherit the
    /// parent's single group-lane decision: no second evaluation, no per-reply
    /// row (G·7). Deny ⇒ <c>Post = null</c> with **no** replies (Web 404 —
    /// G·3/G·4) — the decision's row **was** written (C3). **No** audience
    /// evaluation (G·1/G·8).
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
    /// <c>actorRoles</c> parameter (contrast <see cref="PostService.CreatePostAsync"/>'s
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
            ImageIds = draft.ImageIds ?? [], // RC R·3/R·7 (ADR 0025) — populated server-side by the Web layer; null-coalesce to the POCO's non-null empty list.
            AttachmentIds = draft.AttachmentIds ?? [], // ATT U4 (C-ATT·4) — populated server-side by the Web layer; null-coalesce to the POCO's non-null empty list.
            LanguageCode = await ResolveLanguageCodeAsync(draft.LanguageCode, session).ConfigureAwait(false), // ADR 0018
            IsDraft = draft.IsDraft ?? false, // ADR 0037 — draft mode (group lane); the author-only gate is the same pure AuthorId==actorId check.
            Created = DateTimeOffset.UtcNow
        };

        session.Store(post);

        // M6 (ADR 0076, plan U04) — the F2 group-post emitter. **Per-member**
        // emission (the M4 §6.4 "per-recipient" precedent — one inbox row + one
        // conditional email per member, excluding the post's author). Each
        // member's idempotency key is the §6.3 shape `notification:group.post:
        // {postId}:{member}` (D4, F10) — stable + content-derived, so a
        // re-emission of the same post to the same member is a no-op. `EmitAsync`
        // runs on the caller's session and does **not** commit — the
        // `SaveChangesAsync` below is the single commit (C3). The members are
        // read from the caller's session (the same-transaction lane; strong
        // consistency, invariant C4).
        //
        // A draft (ADR 0037 — invisible to all but the author) must **not**
        // notify members: they could not yet see the content, so an inbox
        // row + email for it would leak intake they did not consent to
        // reading (intake honesty). Notification is deferred to the publish
        // lane (the moment the post becomes visible to members).
        if (_notifications is not null && !(draft.IsDraft ?? false))
        {
            var members = await session.Query<GroupMembership>()
                .Where(m => m.GroupId == draft.GroupId)
                .Select(m => m.UserId)
                .ToListAsync()
                .ConfigureAwait(false);
            foreach (var member in members)
            {
                if (string.IsNullOrWhiteSpace(member) || string.Equals(member, actorId, StringComparison.Ordinal))
                    continue;                                   // the post's author does not notify themselves
                await _notifications.EmitAsync(
                    session,
                    recipientId: member,
                    kind: NotificationKinds.GroupPost,
                    idempotencyKey: $"notification:group.post:{post.Id}:{member}",
                    body: TruncateUgcSnippet(post.Body),
                    linkPath: $"/groups/{draft.GroupId}/posts/{post.Id}",
                    targetId: draft.GroupId,
                    ct: default).ConfigureAwait(false);
            }
        }

        // M36 (ADR 0165, U05) — the group post mention emitter (Part 2.3
        // lane 3, `group.post.mention`). Additive, *inside* the lane (the
        // frozen method signature + audit rows untouched). Draft-gated
        // (M36·8, ADR 0037): the same `!(draft.IsDraft ?? false)` pin as the
        // group-post member emission above. Extract candidate `@`-tokens from
        // the post body, resolve them against the frozen directory read (the
        // author is never a recipient — M36-5 self pin), and emit one
        // `group.post.mention` notification per resolved recipient on the
        // caller's session. The per-recipient idempotency key (M36·4) keeps a
        // re-emission of the same (post, recipient) pair a no-op (ADR 0076
        // D4 / F10). `EmitAsync` does not commit; the `SaveChangesAsync`
        // below is the single commit (C3). No new authorization surface, no
        // `AccessAudit` row (M36·5 — a mention is a personal read).
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
                        kind: NotificationKinds.GroupPostMention,
                        idempotencyKey: $"notification:group.post.mention:{post.Id}:{recipientId}",
                        body: UgcSnippets.Truncate(post.Body),
                        targetId: null,
                        linkPath: $"/groups/{draft.GroupId}/posts/{post.Id}",
                        ct: default).ConfigureAwait(false);
                }
            }
        }

        // One SaveChangesAsync — the C3 same-transaction lane (ADR 0006-E): the
        // gate decision row + the new post commit atomically.
        await session.SaveChangesAsync().ConfigureAwait(false);
        return post;
    }

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
    /// ADR 0016 as amended — the authored-in tag is editable on this lane too,
    /// so an author can correct the language a group post was written in; it is
    /// re-resolved through the shared <see cref="ResolveLanguageCodeAsync"/>
    /// helper, mirroring the ADR 0014 community-post lane). The group lane's
    /// identity fields are **immutable** — <c>GroupId</c> (the lane itself),
    /// <c>ComponentId</c> (must stay empty, G·2 lane exclusivity),
    /// <c>Audience</c> (non-null empty, G·8 — there is no audience to re-choose
    /// on the group lane), <c>AuthorId</c>, <c>Created</c>, and <c>Status</c>
    /// are all untouched. The edit stamps <see cref="Post.Modified"/> forward.
    /// </para>
    /// <para>
    /// A missing id is a <see cref="KeyNotFoundException"/>; a non-author is
    /// the <see cref="UnauthorizedAccessException"/>. The Web layer (the group
    /// lane's 404 shape) maps both to a 404 to stay non-leaky, unlike the
    /// component lane's 403. One <c>SaveChangesAsync()</c> (invariant C3).
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
}
