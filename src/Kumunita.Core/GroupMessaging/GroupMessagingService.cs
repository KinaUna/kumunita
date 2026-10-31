using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.Query;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;

namespace Kumunita.Core.GroupMessaging;

/// <summary>
/// Composition service for the <see cref="Kumunita.Core.GroupMessaging"/>
/// bounded context (the group-messaging lane — multi-participant chat
/// threads, the N:1 generalization of the M9 1:1 surface in ADR 0105).
/// <para>
/// The service composes the host-registered Marten <c>IDocumentStore</c>
/// (the same store-composing shape as <see cref="Kumunita.Core.Messaging
/// .MessagingService"/>). The toggle gate is the same instance-level
/// <c>MessagingEnabled</c> flag read from <c>LocaleSettings</c> — group
/// messaging rides the same master gate as 1:1. The per-actor gate
/// (opt-in + guardian ceiling) is likewise the same <c>IsMessagingAllowedForAsync</c>
/// read on the 1:1 service, surfaced by the controller.
/// </para>
/// </summary>
public sealed class GroupMessagingService : IGroupMessagingService
{
    /// <summary>The plain-text cap (the M9 D7 pin — same cap rule, same number).</summary>
    public const int MaxBodyChars = 2000;

    /// <summary>The group-title cap (a label, not a body — short enough to stay a one-line list-row label).</summary>
    public const int MaxTitleChars = 120;

    /// <summary>The group-list page size (the ADR 0090 D4 neighborhood-scale page).</summary>
    public const int PageSize = 20;

    /// <summary>The thread page size (the "show only the 10 latest" pin from M9).</summary>
    public const int ThreadPageSize = 10;

    private readonly IDocumentStore _store;
    private readonly IUserInfoService? _userInfo;
    private readonly NotificationService? _notifications;
    private readonly Kumunita.Core.Mentions.IMentionService? _mentions;

    public GroupMessagingService(
        IDocumentStore store,
        IUserInfoService? userInfo = null,
        NotificationService? notifications = null,
        Kumunita.Core.Mentions.IMentionService? mentions = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _userInfo = userInfo;
        _notifications = notifications;
        _mentions = mentions;
    }

    /// <summary>
    /// The instance-level master gate (the same <c>LocaleSettings.MessagingEnabled</c>
    /// read as <see cref="Kumunita.Core.Messaging.MessagingService.IsMessagingEnabledAsync"/>).
    /// Group messaging rides the same master gate as 1:1 — a disabled instance
    /// disables both surfaces.
    /// </summary>
    public async Task<bool> IsMessagingEnabledAsync()
    {
        using var session = _store.QuerySession();
        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, CancellationToken.None)
            .ConfigureAwait(false);
        return settings?.MessagingEnabled == true;
    }

    // ── Open a new group ───────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<GroupConversationRef> OpenGroupAsync(string actorId, IReadOnlyList<string> recipientIds, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required to open a group.", nameof(actorId));
        if (recipientIds is null || recipientIds.Count == 0)
            throw new ArgumentException("At least one recipient is required to open a group (a one-person group is a 1:1 — the M9 surface covers that).", nameof(recipientIds));
        if (title is not null && title.Length > MaxTitleChars)
            throw new ArgumentException(
                $"The group title is capped at {MaxTitleChars} characters.", nameof(title));

        await EnsureEnabledAsync().ConfigureAwait(false);

        // The invitee set — de-duped, excluding the actor (a self-invite is a
        // no-op; a one-person "group" is a 1:1). The recipients do NOT join
        // the participant set at open time — they get a pending invitation
        // (the invitation flow, ADR 0278 amendment) and join only on accept.
        var invitees = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var r in recipientIds)
        {
            if (!string.IsNullOrWhiteSpace(r) && r != actorId)
                invitees.Add(r);
        }
        if (invitees.Count < 1)
            throw new ArgumentException("At least one distinct recipient (other than the actor) is required.", nameof(recipientIds));

        var now = DateTimeOffset.UtcNow;
        var created = new GroupConversation
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatorId = actorId,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            Created = now,
            // The creator is the only initial participant — the invitees join
            // on accept (the invitation flow, ADR 0278 amendment).
            Participants = new List<string> { actorId }
        };

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = CancellationToken.None;
        session.Store(created);

        // The open audit row (the M9 D5 shape, generalized — Action =
        // "group.message.open", TargetKind = "group.message").
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "group.message.open",
            TargetKind = "group.message",
            TargetId = created.Id,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        });

        // Each invitee: a pending invitation row + a group.message.invite
        // nudge (the accept/decline deep-links). The emission sits in this
        // session's transaction (invariant C3) — the inbox row + the invite
        // row commit atomically with the conversation.
        foreach (var invitee in invitees)
        {
            await UpsertInviteAsync(session, created, invitee, actorId, now, ct).ConfigureAwait(false);
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        return await BuildRefAsync(session, created, actorId).ConfigureAwait(false);
    }

    // ── Invite members to an existing group ────────────────────────────────

    /// <inheritdoc />
    public async Task InviteMembersAsync(string conversationId, string actorId, IReadOnlyList<string> inviteeIds)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("A conversation id is required.", nameof(conversationId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required.", nameof(actorId));
        if (inviteeIds is null || inviteeIds.Count == 0)
            throw new ArgumentException("At least one invitee is required.", nameof(inviteeIds));

        await EnsureEnabledAsync().ConfigureAwait(false);

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = CancellationToken.None;
        var convo = await session.LoadAsync<GroupConversation>(conversationId, ct).ConfigureAwait(false);
        if (convo is null || !convo.Participants.Contains(actorId))
            // Non-leaky 404 — the same D3/D4 shape as the M9 1:1 surface.
            throw new KeyNotFoundException("The conversation was not found.");

        // The invitee set — de-duped, excluding the actor (a self-invite is a
        // no-op) and any existing participant (already a member — no invite).
        var invitees = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var m in inviteeIds)
        {
            if (string.IsNullOrWhiteSpace(m)) continue;
            if (m == actorId) continue;
            if (convo.Participants.Contains(m)) continue;
            invitees.Add(m);
        }
        if (invitees.Count == 0)
            return;   // all candidates were already members (or the actor) — no-op

        var now = DateTimeOffset.UtcNow;
        foreach (var invitee in invitees)
        {
            await UpsertInviteAsync(session, convo, invitee, actorId, now, ct).ConfigureAwait(false);
        }

        // The invite audit row (one row per call — the action is
        // "group.message.invite", TargetKind "group.message").
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "group.message.invite",
            TargetKind = "group.message",
            TargetId = convo.Id,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Accept a pending invitation (self-lane) ────────────────────────────

    /// <inheritdoc />
    public async Task AcceptInviteAsync(string conversationId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("A conversation id is required.", nameof(conversationId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required.", nameof(actorId));

        await EnsureEnabledAsync().ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        await using var session = _store.OpenSession(new SessionOptions());
        var ct = CancellationToken.None;

        var convo = await session.LoadAsync<GroupConversation>(conversationId, ct).ConfigureAwait(false);
        if (convo is null)
            // Non-leaky 404 (the D3/D4 shape — a non-participant / non-invitee
            // getting a 404 is indistinguishable from a missing id).
            throw new KeyNotFoundException("The conversation was not found.");

        // Self-lane (the community-group C-M2b·2 shape): the row must be the
        // actor's own invite (InvitedId == actorId), else an invalid self-lane
        // call (the Web's 404 gate is the first wall; this is the Core's).
        var row = await session.Query<GroupMessagingInvitation>()
            .Where(i => i.ConversationId == conversationId && i.InvitedId == actorId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new InvalidOperationException(
                $"No group-message invitation for {actorId} in conversation {conversationId}");

        // C-M2b·3: only Pending resolves; an already-resolved row (Accepted /
        // Declined) is an invalid transition.
        if (row.Status != GroupMessagingInvitationStatus.Pending)
            throw new InvalidOperationException(
                $"Invitation {row.Id} is already {row.Status}; only a Pending invitation can be accepted.");

        row.Status = GroupMessagingInvitationStatus.Accepted;
        row.ResolvedAt = now;
        row.ResolvedBy = actorId;
        session.Store(row);

        // The membership lands here — append the actor to the participant set
        // (sorted, de-duped). Strong consistency (invariant C4): the actor is
        // a participant on the very next read.
        if (!convo.Participants.Contains(actorId))
        {
            var set = new SortedSet<string>(convo.Participants, StringComparer.Ordinal);
            set.Add(actorId);
            convo.Participants = set.ToList();
            session.Store(convo);
        }

        // Self-lane audit: the invitee's own standing — Via Owner with the
        // invitee as all three identities (the community-group
        // AcceptGroupInvitationAsync audit shape).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "group.message.invite.accept",
            TargetKind = "group.message",
            TargetId = conversationId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Decline a pending invitation (self-lane) ───────────────────────────

    /// <inheritdoc />
    public async Task DeclineInviteAsync(string conversationId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("A conversation id is required.", nameof(conversationId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required.", nameof(actorId));

        await EnsureEnabledAsync().ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        await using var session = _store.OpenSession(new SessionOptions());
        var ct = CancellationToken.None;

        var convo = await session.LoadAsync<GroupConversation>(conversationId, ct).ConfigureAwait(false);
        if (convo is null)
            throw new KeyNotFoundException("The conversation was not found.");

        // Self-lane (C-M2b·2) — the same gate + state check as
        // AcceptInviteAsync, but no participant set is touched (the invitee
        // simply never becomes a member; the row is re-invitable afterward).
        var row = await session.Query<GroupMessagingInvitation>()
            .Where(i => i.ConversationId == conversationId && i.InvitedId == actorId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new InvalidOperationException(
                $"No group-message invitation for {actorId} in conversation {conversationId}");

        if (row.Status != GroupMessagingInvitationStatus.Pending)
            throw new InvalidOperationException(
                $"Invitation {row.Id} is already {row.Status}; only a Pending invitation can be declined.");

        row.Status = GroupMessagingInvitationStatus.Declined;
        row.ResolvedAt = now;
        row.ResolvedBy = actorId;
        session.Store(row);

        // Self-lane audit (the community-group DeclineGroupInvitationAsync
        // shape — no membership lane touched).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "group.message.invite.decline",
            TargetKind = "group.message",
            TargetId = conversationId,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Member roster (accepted + pending) ─────────────────────────────────

    /// <inheritdoc />
    public async Task<GroupMessageMembers> GetMembersAsync(string conversationId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("A conversation id is required.", nameof(conversationId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required.", nameof(actorId));

        await EnsureEnabledAsync().ConfigureAwait(false);

        await using var session = _store.QuerySession();
        var ct = CancellationToken.None;
        var convo = await session.LoadAsync<GroupConversation>(conversationId, ct).ConfigureAwait(false);
        if (convo is null || !convo.Participants.Contains(actorId))
            // Non-leaky 404 (the D3/D4 shape) — a non-participant cannot see
            // the roster (that would leak the pending invitees' presence).
            throw new KeyNotFoundException("The conversation was not found.");

        // The accepted participants (the participant set).
        var members = new List<GroupMessageMember>();
        foreach (var pid in convo.Participants)
            members.Add(new GroupMessageMember(pid, await ResolveDisplayNameAsync(pid).ConfigureAwait(false), Pending: false));

        // The pending invitees (the not-yet-resolved invitation rows).
        var pending = await session.Query<GroupMessagingInvitation>()
            .Where(i => i.ConversationId == conversationId && i.Status == GroupMessagingInvitationStatus.Pending)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var row in pending)
        {
            if (convo.Participants.Contains(row.InvitedId))
                continue;   // a pending row for someone who is already a participant (defensive)
            members.Add(new GroupMessageMember(row.InvitedId, await ResolveDisplayNameAsync(row.InvitedId).ConfigureAwait(false), Pending: true));
        }

        // Sort: accepted first (by display name), then pending (by display
        // name). The list is small (a resident's group) — in-memory sort is
        // fine and avoids a Marten OrderBy on a computed name.
        return new GroupMessageMembers(
            members
                .OrderByDescending(m => m.Pending ? 0 : 1)   // accepted (1) before pending (0)
                .ThenBy(m => m.DisplayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    // ── Pending invitations for a user (self-lane gate seam) ──────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupMessagingInvitation>> GetPendingInvitationsForUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("A resident id is required.", nameof(userId));

        await using var session = _store.QuerySession();
        var ct = CancellationToken.None;
        var rows = await session.Query<GroupMessagingInvitation>()
            .Where(i => i.InvitedId == userId && i.Status == GroupMessagingInvitationStatus.Pending)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows
            .OrderBy(r => r.InvitedAt)
            .ToList();
    }

    // ── Read the thread ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<GroupConversationDetail> GetConversationAsync(string conversationId, string actorId, int page, int? pageSize = null)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("A conversation id is required.", nameof(conversationId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required.", nameof(actorId));
        int ps = PageSizer.ResolveOverride(pageSize, ThreadPageSize);

        await EnsureEnabledAsync().ConfigureAwait(false);

        await using var session = _store.QuerySession();
        var ct = CancellationToken.None;
        var convo = await session.LoadAsync<GroupConversation>(conversationId, ct).ConfigureAwait(false);
        if (convo is null || !convo.Participants.Contains(actorId))
            // Non-leaky 404 (D3/D4 shape).
            throw new KeyNotFoundException("The conversation was not found.");

        var p = Math.Max(1, page);
        var messages = await session.Query<GroupMessage>()
            .Where(m => m.ConversationId == convo.Id)
            .OrderByDescending(m => m.Created)
            .Skip((p - 1) * ps)
            .Take(ps)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var hasMore = messages.Count == ps;

        return new GroupConversationDetail(
            await BuildRefAsync(session, convo, actorId).ConfigureAwait(false),
            messages,
            hasMore);
    }

    // ── List the actor's groups ─────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<GroupConversationList> ListGroupsAsync(string actorId, int page, int? pageSize = null)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required.", nameof(actorId));
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await EnsureEnabledAsync().ConfigureAwait(false);

        await using var session = _store.QuerySession();
        var ct = CancellationToken.None;
        var p = Math.Max(1, page);

        // The actor's groups — the participant set contains the actor's id.
        // Marten translates the Contains on a List<string> to a Postgres
        // ANY clause (the array-column shape).
        //
        // Sorting is done in-memory (the list is small — a resident's groups)
        // because Marten's LINQ parser does not support the null-coalescing
        // operator (`??`) in an OrderBy expression.
        var myGroups = await session.Query<GroupConversation>()
            .Where(c => c.Participants.Contains(actorId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var sorted = myGroups
            .OrderByDescending(c => c.LastMessageAt ?? c.Created)
            .Skip((p - 1) * ps)
            .Take(ps)
            .ToList();
        var hasMore = sorted.Count == ps;

        var items = new List<GroupConversationRef>(sorted.Count);
        foreach (var c in sorted)
            items.Add(await BuildRefAsync(session, c, actorId).ConfigureAwait(false));
        return new GroupConversationList(items, hasMore);
    }

    // ── Send a message ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task SendAsync(string conversationId, string actorId, string body)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("A conversation id is required.", nameof(conversationId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("The message body is required (plain text, non-blank).", nameof(body));
        if (body.Length > MaxBodyChars)
            throw new ArgumentException(
                $"The message body is capped at {MaxBodyChars} characters.", nameof(body));

        await EnsureEnabledAsync().ConfigureAwait(false);

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = CancellationToken.None;
        var convo = await session.LoadAsync<GroupConversation>(conversationId, ct).ConfigureAwait(false);
        if (convo is null || !convo.Participants.Contains(actorId))
            // Non-leaky 404 for a non-participant.
            throw new KeyNotFoundException("The conversation was not found.");

        var messageId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var message = new GroupMessage
        {
            Id = messageId,
            ConversationId = convo.Id,
            SenderId = actorId,
            Body = body,
            Created = now,
            ReadBy = null,
            LanguageCode = "en"
        };
        session.Store(message);
        convo.LastMessageAt = now;
        session.Store(convo);

        // The nudge — every participant except the sender gets exactly one
        // group.message.new notification, deduped by the stable idempotency
        // key (the ADR 0076 D4 shape).
        if (_notifications is not null)
        {
            foreach (var recipient in convo.Participants)
            {
                if (recipient == actorId) continue;
                await _notifications.EmitAsync(
                        session,
                        recipient,
                        Notifications.NotificationKinds.GroupMessageNew,
                        $"notification:group.message.new:{messageId}",
                        body,
                        targetId: null,
                        linkPath: $"/group-messages/{convo.Id}",
                        ct: ct)
                    .ConfigureAwait(false);
            }
        }

        // The mention emitter (the M36 shape, generalized — extract @-tokens
        // from the body, resolve against the directory read, emit one
        // group.message.mention per resolved recipient).
        if (_mentions is not null && _notifications is not null)
        {
            var mentionTokens = Kumunita.Core.Mentions.MentionTokens.ExtractTokens(body);
            if (mentionTokens.Count > 0)
            {
                var mentionRecipients = await _mentions.ResolveAsync(mentionTokens, actorId, ct)
                    .ConfigureAwait(false);
                foreach (var recipientId in mentionRecipients)
                {
                    if (recipientId == actorId) continue;
                    await _notifications.EmitAsync(
                        session,
                        recipientId: recipientId,
                        kind: Notifications.NotificationKinds.GroupMessageMention,
                        idempotencyKey: $"notification:group.message.mention:{messageId}:{recipientId}",
                        body: Notifications.UgcSnippets.Truncate(body),
                        targetId: null,
                        linkPath: $"/group-messages/{convo.Id}",
                        ct: ct).ConfigureAwait(false);
                }
            }
        }

        // The send audit row.
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "group.message.send",
            TargetKind = "group.message",
            TargetId = convo.Id,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Mark read ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task MarkReadAsync(string conversationId, string actorId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("A conversation id is required.", nameof(conversationId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required.", nameof(actorId));

        await EnsureEnabledAsync().ConfigureAwait(false);

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = CancellationToken.None;
        var convo = await session.LoadAsync<GroupConversation>(conversationId, ct).ConfigureAwait(false);
        if (convo is null || !convo.Participants.Contains(actorId))
            throw new KeyNotFoundException("The conversation was not found.");

        // The actor's unread messages: sender ≠ actor, actor's id not yet
        // in ReadBy (or ReadBy is null).
        var unread = await session.Query<GroupMessage>()
            .Where(m => m.ConversationId == convo.Id
                        && m.SenderId != actorId
                        && (m.ReadBy == null || !m.ReadBy.Contains(actorId)))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (unread.Count == 0)
            return;   // nothing to mark — no-op
        var now = DateTimeOffset.UtcNow;
        foreach (var m in unread)
        {
            if (m.ReadBy is null)
                m.ReadBy = new List<string> { actorId };
            else if (!m.ReadBy.Contains(actorId))
                m.ReadBy.Add(actorId);
        }
        session.Store(unread.ToArray());
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Upsert a pending <see cref="GroupMessagingInvitation"/> row for the
    /// (conversation, invitee) pair + emit the <c>group.message.invite</c>
    /// nudge (with the accept/decline deep-links). The business key
    /// (ConversationId, InvitedId) enforces "one row per (thread, invitee)"
    /// (the community-group <c>GroupInvitation</c> convention); a resolved or
    /// existing-Pending row is re-stamped to a fresh Pending (C-M2b·3). The
    /// emission sits in the caller's session transaction (invariant C3) —
    /// the inbox row + the invite row commit atomically with the domain
    /// write. The <c>NotificationService</c> is an optional seam (the
    /// <c>UserInfoService</c> precedent) so the direct-construction test
    /// harnesses that build <c>GroupMessagingService(store)</c> positionally
    /// keep compiling; the DI registration resolves the live instance.
    /// </summary>
    private async Task UpsertInviteAsync(
        IDocumentSession session,
        GroupConversation convo,
        string invitee,
        string invitedBy,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var existing = await session.Query<GroupMessagingInvitation>()
            .Where(i => i.ConversationId == convo.Id && i.InvitedId == invitee)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            session.Store(new GroupMessagingInvitation
            {
                Id = Guid.NewGuid().ToString("N"),
                ConversationId = convo.Id,
                InvitedId = invitee,
                InvitedBy = invitedBy,
                Status = GroupMessagingInvitationStatus.Pending,
                InvitedAt = now
            });
        }
        else
        {
            // Re-invite (C-M2b·3): a resolved row (or an already-Pending
            // re-stamp) resets to the fresh Pending shape — the two resolve
            // stamps are cleared with it.
            existing.InvitedBy = invitedBy;
            existing.Status = GroupMessagingInvitationStatus.Pending;
            existing.InvitedAt = now;
            existing.ResolvedAt = null;
            existing.ResolvedBy = null;
            session.Store(existing);
        }

        if (_notifications is not null)
        {
            await _notifications.EmitAsync(
                session,
                recipientId: invitee,
                kind: Notifications.NotificationKinds.GroupMessageInvite,
                idempotencyKey: $"notification:group.message.invite:{convo.Id}:{invitee}",
                body: null,
                targetId: null,
                linkPath: $"/group-messages/{convo.Id}",
                acceptPath: $"/group-messages/{convo.Id}/invitations/accept",
                declinePath: $"/group-messages/{convo.Id}/invitations/decline",
                ct: ct).ConfigureAwait(false);
        }
    }

    /// <summary>The C-M9·2 hard gate — refuse when the instance toggle is off.</summary>
    private async Task EnsureEnabledAsync()
    {
        if (!await IsMessagingEnabledAsync().ConfigureAwait(false))
            throw new UnauthorizedAccessException("Messaging is not enabled on this instance.");
    }

    /// <summary>
    /// Build a <see cref="GroupConversationRef"/> for the list / thread
    /// pages — the participant display names + the unread count + the
    /// last message body.
    /// </summary>
    private async Task<GroupConversationRef> BuildRefAsync(IQuerySession session, GroupConversation convo, string actorId)
    {
        var ct = CancellationToken.None;
        var last = await session.Query<GroupMessage>()
            .Where(m => m.ConversationId == convo.Id)
            .OrderByDescending(m => m.Created)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var unreadCount = await session.Query<GroupMessage>()
            .Where(m => m.ConversationId == convo.Id
                        && m.SenderId != actorId
                        && (m.ReadBy == null || !m.ReadBy.Contains(actorId)))
            .CountAsync(ct)
            .ConfigureAwait(false);

        var names = new List<string?>(convo.Participants.Count);
        foreach (var pid in convo.Participants)
            names.Add(await ResolveDisplayNameAsync(pid).ConfigureAwait(false));

        return new GroupConversationRef(
            convo.Id,
            convo.CreatorId,
            convo.Participants,
            names,
            last?.Body,
            convo.LastMessageAt,
            unreadCount,
            convo.Title);
    }

    private async Task<string?> ResolveDisplayNameAsync(string id)
    {
        if (_userInfo is null) return null;
        try
        {
            var profile = await _userInfo.GetProfileAsync(id).ConfigureAwait(false);
            return profile is not null && !string.IsNullOrWhiteSpace(profile.DisplayName)
                ? profile.DisplayName
                : null;
        }
        catch
        {
            return null;   // a directory read failure degrades to the raw id
        }
    }
}
