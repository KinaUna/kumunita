using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Messaging;

/// <summary>
/// Composition service for the <see cref="Kumunita.Core.Messaging"/> bounded
/// context (M9, ADR 0105 — direct 1:1 signed-in resident messaging). U02
/// ships the two admin-toggle seams (design doc §D2): a read-with-floor over
/// the <see cref="LocaleSettings"/> singleton (the <c>false</c> floor — a
/// missing row reads as **off**, the deliberate inverse of the ADR 0101
/// <c>true</c> floor) and the admin write that persists the flag plus its
/// single <c>messaging.toggle</c> <see cref="Authorization.AccessAudit"/> row
/// in the same session (C-M9·3, the ADR 0101
/// <c>announcementcomments.set-enabled</c> write-row shape). U03 appends the
/// conversation / message seams (D1–D8): find-or-create on the unordered
/// pair (F1), participant-gated thread read with a non-leaky 404 (D3/D4),
/// the cap-checked send with the <c>message.new</c> nudge (D6/D7), the
/// personal read-state lane (D8), and the <c>HasMore</c> paging discipline
/// (ADR 0090). The ctor's optional <c>NotificationService?</c> seam is the
/// ADR 0077 optional-nudge-param idiom — the U02 tests construct the service
/// without it and keep compiling unchanged.
/// </summary>
public sealed class MessagingService : IMessagingService
{
    /// <summary>The plain-text cap (D7 — the ADR 0105 D7 pin; the design doc's
    /// §Drift-guard may amend the number, not the cap rule).</summary>
    public const int MaxBodyChars = 2000;

    /// <summary>The list / thread page size (the ADR 0090 D4 neighborhood-scale
    /// page; <c>HasMore</c> is the sole paging signal, ADR 0090 D1).</summary>
    public const int PageSize = 20;

    private readonly IDocumentStore _store;
    // The OtherDisplayName resolution lane (design doc refinement 8) —
    // optional (the ADR 0077 optional-param idiom): a harness that pins the
    // Core contract without a directory read passes nothing and the list / thread
    // rows carry a null OtherDisplayName (the view renders the raw id itself).
    private readonly IUserInfoService? _userInfo;
    // The M6 nudge lane (D6) — optional (the ADR 0077 idiom): when absent
    // (a test harness that only pins the Core contract) the send commits its
    // message + audit row and skips the notification emission.
    private readonly NotificationService? _notifications;

    public MessagingService(
        IDocumentStore store,
        IUserInfoService? userInfo = null,
        NotificationService? notifications = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _userInfo = userInfo;         // null = no directory read (the U02 pins construct with store only)
        _notifications = notifications;   // null = nudge lane unwired (U02's 2 pins)
    }

    public async Task<bool> IsMessagingEnabledAsync()
    {
        using var session = _store.QuerySession();
        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, CancellationToken.None)
            .ConfigureAwait(false);
        // The <c>false</c> floor: a missing singleton reads as "messaging off"
        // (the deliberate inverse of the AnnouncementCommentsEnabled
        // <c>true</c> floor — a privacy-sensitive opt-in, design doc §D2).
        return settings?.MessagingEnabled == true;
    }

    public async Task SetMessagingEnabledAsync(bool enabled, string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to change the messaging gate.");

        await using var session = _store.OpenSession(new SessionOptions());
        var settings = await session
            .LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, CancellationToken.None)
            .ConfigureAwait(false);
        if (settings is null)
            settings = new LocaleSettings { MessagingEnabled = enabled };
        else
            settings.MessagingEnabled = enabled;

        // The singleton-toggle audit row (the signup.set-open /
        // announcementcomments.set-enabled shape — ADR 0004 §B.1 additive
        // field, one row per flip, C-M9·3).
        session.Store(settings);
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "messaging.toggle",
            TargetKind = "messaging.toggle",
            TargetId = "messaging.toggle",
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    // ── Conversation / message seams (U03 — D1–D8) ─────────────────────────

    /// <inheritdoc />
    public async Task<ConversationRef> OpenConversationAsync(string actorId, string otherId)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required to open a conversation.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(otherId))
            throw new ArgumentException("The other resident id is required to open a conversation.", nameof(otherId));
        if (actorId == otherId)
            throw new ArgumentException("A conversation is between two distinct residents (D1 — no self-conversations).", nameof(otherId));

        await EnsureEnabledAsync().ConfigureAwait(false);

        // The pair is stored **sorted** (ParticipantA < ParticipantB) so the
        // unordered pair has exactly one canonical form — the
        // <c>convo_uidx_pair</c> unique index (M9DocTypes) is the F1
        // idempotency witness: a second open of the same pair (either order)
        // finds the existing conversation instead of violating the index.
        var (participantA, participantB) =
            string.CompareOrdinal(actorId, otherId) < 0 ? (actorId, otherId) : (otherId, actorId);

        await using var session = _store.OpenSession(new SessionOptions());
        var existing = await session
            .Query<Conversation>()
            .Where(c => c.ParticipantA == participantA && c.ParticipantB == participantB)
            .FirstOrDefaultAsync(CancellationToken.None)
            .ConfigureAwait(false);

        if (existing is null)
        {
            // F1 — one conversation per unordered pair. The unique index
            // guarantees this even under concurrent opens; a race that loses
            // the insert surfaces as a unique-violation, not a second row.
            var created = new Conversation
            {
                Id = Guid.NewGuid().ToString("N"),
                ParticipantA = participantA,
                ParticipantB = participantB,
                Created = DateTimeOffset.UtcNow
            };
            session.Store(created);

            // The D5 open row (design doc §2.3): Action = "message.open",
            // TargetKind = "message", TargetId = the conversation id,
            // Via = Owner, Outcome = Allow — same session, one
            // SaveChangesAsync (C-M9·3).
            session.Store(new Authorization.AccessAudit
            {
                Id = Guid.NewGuid().ToString("N"),
                At = DateTimeOffset.UtcNow,
                ActorId = actorId,
                EffectivePrincipalId = actorId,
                Action = "message.open",
                TargetKind = "message",
                TargetId = created.Id,
                Via = Authorization.AccessVia.Owner,
                Outcome = Authorization.AccessOutcome.Allow
            });
            await session.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            existing = created;
        }

        var other = actorId == participantA ? participantB : participantA;
        return new ConversationRef(
            existing.Id,
            other,
            await ResolveOtherDisplayNameAsync(other).ConfigureAwait(false),
            LastMessageBody: null,
            LastMessageAt: existing.LastMessageAt,
            UnreadCount: await CountUnreadForAsync(session, existing.Id, actorId).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<ConversationDetail> GetConversationAsync(string conversationId, string actorId, int page)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("A conversation id is required.", nameof(conversationId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required.", nameof(actorId));

        await EnsureEnabledAsync().ConfigureAwait(false);

        await using var session = _store.QuerySession();
        var ct = CancellationToken.None;
        var convo = await session.LoadAsync<Conversation>(conversationId, ct).ConfigureAwait(false);
        if (convo is null || !IsParticipant(convo, actorId))
            // D3/D4 — non-leaky 404: a missing conversation and a
            // non-participant (GlobalAdmin included) are indistinguishable
            // (C-M9·1; the ADR 0028 GU precedent). No audit row (C-M9·4).
            throw new KeyNotFoundException("The conversation was not found.");

        // ADR 0090 — page floors to 1; <c>HasMore</c> is the sole paging
        // signal (a full page implies there may be more).
        var p = Math.Max(1, page);
        var messages = await session.Query<Message>()
            .Where(m => m.ConversationId == convo.Id)
            .OrderByDescending(m => m.Created)
            .Skip((p - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var hasMore = messages.Count == PageSize;

        var other = actorId == convo.ParticipantA ? convo.ParticipantB : convo.ParticipantA;
        return new ConversationDetail(
            new ConversationRef(
                convo.Id,
                other,
                await ResolveOtherDisplayNameAsync(other).ConfigureAwait(false),
                LastMessageBody: null,
                LastMessageAt: convo.LastMessageAt,
                UnreadCount: await CountUnreadForAsync(session, convo.Id, actorId).ConfigureAwait(false)),
            messages,
            hasMore);
    }

    /// <inheritdoc />
    public async Task<ConversationList> ListConversationsAsync(string actorId, int page)
    {
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required.", nameof(actorId));

        await EnsureEnabledAsync().ConfigureAwait(false);

        await using var session = _store.QuerySession();
        var ct = CancellationToken.None;
        var p = Math.Max(1, page);

        // F4 — the list row is only ever built for the caller's own
        // conversations: the pair query matches exactly the two conversations
        // the actor participates in; no one else's id can appear.
        var myConversations = await session.Query<Conversation>()
            .Where(c => c.ParticipantA == actorId || c.ParticipantB == actorId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var sorted = myConversations
            .OrderByDescending(c => c.LastMessageAt ?? c.Created)
            .Skip((p - 1) * PageSize)
            .Take(PageSize)
            .ToList();
        var hasMore = sorted.Count == PageSize;

        var items = new List<ConversationRef>(sorted.Count);
        foreach (var c in sorted)
        {
            var other = actorId == c.ParticipantA ? c.ParticipantB : c.ParticipantA;
            var last = await session.Query<Message>()
                .Where(m => m.ConversationId == c.Id)
                .OrderByDescending(m => m.Created)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            items.Add(new ConversationRef(
                c.Id,
                other,
                await ResolveOtherDisplayNameAsync(other).ConfigureAwait(false),
                last?.Body,
                c.LastMessageAt,
                await CountUnreadForAsync(session, c.Id, actorId).ConfigureAwait(false)));
        }
        return new ConversationList(items, hasMore);
    }

    /// <inheritdoc />
    public async Task SendAsync(string conversationId, string actorId, string body)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("A conversation id is required.", nameof(conversationId));
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An acting resident id is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("The message body is required (D7 — plain text, non-blank).", nameof(body));
        if (body.Length > MaxBodyChars)
            throw new ArgumentException(
                $"The message body is capped at {MaxBodyChars} characters (D7).", nameof(body));

        await EnsureEnabledAsync().ConfigureAwait(false);

        await using var session = _store.OpenSession(new SessionOptions());
        var ct = CancellationToken.None;
        var convo = await session.LoadAsync<Conversation>(conversationId, ct).ConfigureAwait(false);
        if (convo is null || !IsParticipant(convo, actorId))
            // D3/D4 — non-leaky 404 for a non-participant (never 403, never
            // a write on a conversation the actor does not belong to).
            throw new KeyNotFoundException("The conversation was not found.");

        var messageId = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var message = new Message
        {
            Id = messageId,
            ConversationId = convo.Id,
            SenderId = actorId,
            Body = body,
            Created = now,
            // D8 — recipient-only read marker; null = unread.
            ReadBy = null,
            LanguageCode = "en"   // ADR 0018 authored-in tag (the display lane's entry; M9 stores the authored text as-is)
        };
        session.Store(message);
        convo.LastMessageAt = now;
        session.Store(convo);

        // D6 — the M6 nudge: the *other* participant (never the sender) gets
        // exactly one <c>message.new</c> notification, deduped by the stable,
        // content-derived idempotency key. Emission runs **before** the
        // single SaveChangesAsync so the domain write + the inbox row + the
        // outbox envelope commit atomically (the ADR 0076 D5 shape); a failed
        // email never rolls back the message. The per-target gate is skipped
        // (targetId null — messaging has no per-target scope, design doc §D6
        // refinement 3). When the nudge lane is unwired (U02's harness) the
        // emission is skipped — the Core contract (message + audit row)
        // stands on its own.
        var recipient = actorId == convo.ParticipantA ? convo.ParticipantB : convo.ParticipantA;
        if (_notifications is not null)
        {
            // ADR 0078 — the suppression gate may return null (a sample
            // account in a production environment): no inbox row, no email.
            // The message itself is the durable record (D5) — proceed to
            // commit either way.
            await _notifications.EmitAsync(
                    session,
                    recipient,
                    Notifications.NotificationKinds.MessageNew,
                    $"notification:message.new:{messageId}",
                    body,
                    targetId: null,
                    linkPath: $"/messages/{convo.Id}",
                    ct: ct)
                .ConfigureAwait(false);
        }

        // The D5 send row (design doc §2.3): Action = "message.send",
        // TargetKind = "message", TargetId = the conversation id,
        // Via = Owner, Outcome = Allow — same session, one
        // SaveChangesAsync (C-M9·3).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "message.send",
            TargetKind = "message",
            TargetId = convo.Id,
            Via = Authorization.AccessVia.Owner,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

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
        var convo = await session.LoadAsync<Conversation>(conversationId, ct).ConfigureAwait(false);
        if (convo is null || !IsParticipant(convo, actorId))
            // D3/D4 — non-leaky 404; a non-participant never touches the
            // read state of a conversation they do not belong to.
            throw new KeyNotFoundException("The conversation was not found.");

        // D8 — caller-only: exactly the messages the *other* participant
        // sent that the caller has not yet read. The sender's own messages
        // (and the other participant's read state) are never touched. No
        // audit row (C-M9·4 — reads / read-state lanes never audit).
        var other = actorId == convo.ParticipantA ? convo.ParticipantB : convo.ParticipantA;
        var unread = await session.Query<Message>()
            .Where(m => m.ConversationId == convo.Id
                        && m.SenderId == other
                        && m.ReadBy == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (unread.Count == 0)
            return;                                    // nothing to mark — no-op
        var now = DateTimeOffset.UtcNow;
        foreach (var m in unread)
            m.ReadBy = actorId;
        session.Store(unread.ToArray());
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Seams' private helpers ──────────────────────────────────────────────

    /// <summary>The C-M9·2 hard gate — every non-admin seam refuses when the
    /// toggle is off (a missing row is the <c>false</c> floor, design doc §D2).</summary>
    private async Task EnsureEnabledAsync()
    {
        if (!await IsMessagingEnabledAsync().ConfigureAwait(false))
            throw new UnauthorizedAccessException("Messaging is not enabled on this instance.");
    }

    /// <summary>The D4 participant check — the id comparison is the whole
    /// access story (no <c>IAuthorizationService</c>, no new
    /// <c>AccessAction</c>, C-M9·5).</summary>
    private static bool IsParticipant(Conversation c, string actorId)
        => c.ParticipantA == actorId || c.ParticipantB == actorId;

    /// <summary>The unread count (D8): the messages the
    /// <paramref name="actorId"/> has not read — sender is the *other*
    /// participant, <c>ReadBy</c> still null. A pure read — takes an
    /// <c>IQuerySession</c> (the read paths pass a read-only session; the
    /// write paths' <c>IDocumentSession</c> is a subtype, so both compile).</summary>
    private static async Task<int> CountUnreadForAsync(IQuerySession session, string conversationId, string actorId)
    {
        var convo = await session.LoadAsync<Conversation>(conversationId, CancellationToken.None).ConfigureAwait(false);
        if (convo is null)
            return 0;
        var other = actorId == convo.ParticipantA ? convo.ParticipantB : convo.ParticipantA;
        return await session.Query<Message>()
            .Where(m => m.ConversationId == conversationId
                        && m.SenderId == other
                        && m.ReadBy == null)
            .CountAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The <c>OtherDisplayName</c> resolution (design doc §D0 refinement 8):
    /// the frozen <see cref="IUserInfoService.GetProfileAsync"/> read lane,
    /// null-safe — a missing profile (or a harness without the seam) yields a
    /// null, and the view renders the raw id itself.
    /// </summary>
    private async Task<string?> ResolveOtherDisplayNameAsync(string otherId)
    {
        if (_userInfo is null) return null;
        var profile = await _userInfo.GetProfileAsync(otherId).ConfigureAwait(false);
        return profile is not null && !string.IsNullOrWhiteSpace(profile.DisplayName)
            ? profile.DisplayName
            : null;
    }
}
