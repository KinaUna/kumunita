using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Messaging;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <see cref="MessagingService"/> toggle-seam pins (M9, ADR 0105 — U02's
/// 2 of the lane's 14). The shape follows <see cref="AnnouncementServiceTests"/>
/// — same <see cref="PostgresFixture"/>, same <c>BootStoreAsync</c>, same
/// <c>AuditRows</c> helper, fresh scratch Postgres per test method.
/// <para>
/// The toggle is the ADR 0101 admin-toggle shape with the floor <b>inverted</b>
/// (design doc §D2): a missing <see cref="LocaleSettings"/> row reads as
/// **off** (the <c>false</c> floor — messaging is a privacy-sensitive
/// opt-in, not a public-surface default), and the admin write commits exactly
/// one <c>messaging.toggle</c> <see cref="AccessAudit"/> row with
/// <c>Via = Admin</c> / <c>Outcome = Allow</c> (C-M9·3).
/// </para>
/// <para>
/// U03 extends this class with the 12 conversation / message behavior pins —
/// these two toggle pins are U02's and are not rescopeable (the design doc's
/// pinned-test list).
/// </para>
/// </summary>
public class MessagingServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The `false` floor — a fresh instance ships with messaging off ────

    /// <summary>
    /// A missing row is the <c>false</c> floor (the deliberate inverse of the
    /// <c>true</c>-floor convention — the design doc §D2 text), and the probe
    /// is a read: no <see cref="AccessAudit"/> row is committed.
    /// </summary>
    [Fact]
    public async Task IsMessagingEnabled_FreshInstance_FloorsToFalse()
    {
        var store = await BootStoreAsync();
        var svc = new MessagingService(store);

        // A fresh instance has no LocaleSettings row at all (the seeder has
        // not run in this scratch DB); the floor is `false`.
        Assert.False(await svc.IsMessagingEnabledAsync());

        // A read is a read — no audit row is committed for the floor probe
        // (the IsSignupOpen_FreshInstance_FloorsToTrue_NoAuditRow shape, the
        // false floor).
        var audits = await AuditRows(store);
        Assert.Empty(audits);
    }

    // ── The admin flips the gate on ──────────────────────────────────────

    /// <summary>
    /// Enabling the gate persists the flag **and** commits a
    /// <c>messaging.toggle</c> / <c>Via = Admin</c> audit row (the ADR 0101
    /// <c>announcementcomments.set-enabled</c> singleton-toggle shape, flat
    /// sentinel <c>TargetId</c>). A subsequent read sees the flag as **on**.
    /// </summary>
    [Fact]
    public async Task SetMessagingEnabled_Toggle_StoresFlagAndAuditRow()
    {
        var store = await BootStoreAsync();
        var svc = new MessagingService(store);

        await svc.SetMessagingEnabledAsync(true, "u-admin");

        Assert.True(await svc.IsMessagingEnabledAsync());

        var audits = await AuditRows(store);
        var row = Assert.Single(audits, a => a.Action == "messaging.toggle");
        Assert.Equal(Authorization.AccessVia.Admin, row.Via);
        Assert.Equal("messaging.toggle", row.TargetKind);
        Assert.Equal("messaging.toggle", row.TargetId);
        Assert.Equal(Authorization.AccessOutcome.Allow, row.Outcome);
        Assert.Equal("u-admin", row.ActorId);
        // A singleton toggle is not an access change — no VisibleCount /
        // HiddenCount (the single-target shape, the ADR 0019/0020 pin).
        Assert.Null(row.VisibleCount);
        Assert.Null(row.HiddenCount);
    }

    // ── U03 — conversation / message behavior pins (12) ──────────────────

    // ── F1 · Idempotent pair open ────────────────────────────────────────

    /// <summary>
    /// A second <see cref="IMessagingService.OpenConversationAsync"/> of the
    /// same ordered pair (<c>"alice"</c>, <c>"bob"</c>) returns the **same**
    /// conversation <c>Id</c> — no second row is created (F1, the
    /// <c>convo_uidx_pair</c> unique-index witness). One
    /// <c>message.open</c> audit row total (the second open is a no-op
    /// read, not a write).
    /// </summary>
    [Fact]
    public async Task OpenConversation_SamePairTwice_ReturnsSameConversation()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await svc.SetMessagingEnabledAsync(true, "u-admin");

        var first  = await svc.OpenConversationAsync("alice", "bob");
        var second = await svc.OpenConversationAsync("alice", "bob");

        Assert.Equal(first.Id, second.Id);

        var convos = await QueryConversations(store);
        Assert.Single(convos);

        // Exactly one message.open audit row (the second open is a read,
        // not a write — C-M9·3: one audit row per open, not per call).
        var audits = await AuditRows(store);
        Assert.Single(audits, a => a.Action == "message.open");
        Assert.Equal("alice", audits.First(a => a.Action == "message.open").ActorId);
    }

    /// <summary>
    /// Opening the same pair in **either order** (<c>bob</c>→<c>alice</c>
    /// then <c>alice</c>→<c>bob</c>) returns the **same** conversation
    /// <c>Id</c> — the sorted-pair normalization (D1) makes the unordered
    /// pair exactly one canonical form (F1).
    /// </summary>
    [Fact]
    public async Task OpenConversation_SamePairEitherOrder_ReturnsSameConversation()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await svc.SetMessagingEnabledAsync(true, "u-admin");

        var ab = await svc.OpenConversationAsync("alice", "bob");
        var ba = await svc.OpenConversationAsync("bob", "alice");

        Assert.Equal(ab.Id, ba.Id);

        var convos = await QueryConversations(store);
        var convo  = Assert.Single(convos);
        // The pair is stored sorted: ParticipantA < ParticipantB
        Assert.Equal("alice", convo.ParticipantA);
        Assert.Equal("bob",   convo.ParticipantB);

        // The other participant is resolved correctly for each caller:
        Assert.Equal("bob",   ab.OtherParticipantId);
        Assert.Equal("alice", ba.OtherParticipantId);
    }

    /// <summary>
    /// A conversation between two residents is created even when neither
    /// has a <see cref="Profile"/> row (a signed-in resident without a
    /// directory entry — the <c>OtherDisplayName</c> is <c>null</c> in
    /// that case, and the view renders the raw id). The conversation
    /// is still created and returned.
    /// </summary>
    [Fact]
    public async Task OpenConversation_BothParticipants_SignedIn_Only()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await svc.SetMessagingEnabledAsync(true, "u-admin");

        // No profiles planted — the directory read returns null.
        var ref_ = await svc.OpenConversationAsync("user-a", "user-b");

        Assert.NotEmpty(ref_.Id);
        Assert.Equal("user-b", ref_.OtherParticipantId);
        // No profile → null display name (the view renders the raw id).
        Assert.Null(ref_.OtherDisplayName);

        var convos = await QueryConversations(store);
        Assert.Equal("user-a", Assert.Single(convos).ParticipantA);
        Assert.Equal("user-b", Assert.Single(convos).ParticipantB);
    }

    // ── D7 · Send stores the message, both participants can read it ──────

    /// <summary>
    /// <see cref="IMessagingService.SendAsync"/> stores a
    /// <see cref="Message"/> row with the sender's id and body; both
    /// participants can read it via <see cref="IMessagingService
    /// .GetConversationAsync"/> (the message is visible to the caller
    /// and to the other participant). One <c>message.send</c> audit row
    /// with the locked D5 field values.
    /// </summary>
    [Fact]
    public async Task Send_StoresMessage_BothParticipantsSeeIt()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await svc.SetMessagingEnabledAsync(true, "u-admin");

        var convo = await svc.OpenConversationAsync("alice", "bob");
        await svc.SendAsync(convo.Id, "alice", "Hello Bob");

        // Both participants see the message.
        var aliceView = await svc.GetConversationAsync(convo.Id, "alice", 1);
        var bobView   = await svc.GetConversationAsync(convo.Id, "bob",   1);
        Assert.Single(aliceView.Messages);
        Assert.Single(bobView.Messages);
        Assert.Equal("Hello Bob", aliceView.Messages[0].Body);
        Assert.Equal("alice",     aliceView.Messages[0].SenderId);

        var messages = await QueryMessages(store, convo.Id);
        Assert.Equal(1, messages.Count);
        Assert.Equal("alice",  messages[0].SenderId);
        Assert.Equal("Hello Bob", messages[0].Body);
        // ReadBy is null (unread by the recipient) — D8.
        Assert.Null(messages[0].ReadBy);

        // One message.send audit row with the D5 locked values.
        var audits = await AuditRows(store);
        var sendRow = Assert.Single(audits, a => a.Action == "message.send");
        Assert.Equal("alice",       sendRow.ActorId);
        Assert.Equal(convo.Id,      sendRow.TargetId);
        Assert.Equal("message",     sendRow.TargetKind);
        Assert.Equal(Authorization.AccessVia.Owner, sendRow.Via);
        Assert.Equal(Authorization.AccessOutcome.Allow, sendRow.Outcome);
    }

    // ── D6 · One message.new notification per send ───────────────────────

    /// <summary>
    /// <see cref="IMessagingService.SendAsync"/> (with the M6
    /// <see cref="NotificationService"/> wired) stores exactly **one**
    /// <see cref="Notification"/> row for the *other* participant, with
    /// the locked D6 values: <c>Kind = "message.new"</c>,
    /// <c>IdempotencyKey = "notification:message.new:{messageId}"</c>,
    /// <c>RecipientId = the other participant</c>,
    /// <c>LinkPath = "/messages/{conversationId}"</c>.
    /// </summary>
    [Fact]
    public async Task Send_OtherParticipant_GetsMessageNewNotification_Once()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await svc.SetMessagingEnabledAsync(true, "u-admin");

        var convo = await svc.OpenConversationAsync("alice", "bob");
        await svc.SendAsync(convo.Id, "alice", "Hi Bob");

        var notifs = await QueryNotifications(store);
        // Exactly one notification row (D6 — one nudge per send, deduped).
        var row = Assert.Single(notifs, n => n.Kind == "message.new");

        // The recipient is the *other* participant (bob), not the sender.
        Assert.Equal("bob", row.RecipientId);
        // The idempotency key matches the D6 locked shape:
        // "notification:message.new:{messageId}" (32-char hex message id).
        Assert.StartsWith("notification:message.new:", row.IdempotencyKey);
        var sourceId = row.IdempotencyKey.Substring("notification:message.new:".Length);
        Assert.Equal(32, sourceId.Length);
        Assert.True(System.Text.RegularExpressions.Regex.IsMatch(sourceId, "^[0-9a-f]{32}$"),
            $"source id is not a 32-char hex: {sourceId}");
        // The link path is the thread URL for this conversation.
        Assert.Equal($"/messages/{convo.Id}", row.LinkPath);
        // No accept/decline paths (messaging is not actionable).
        Assert.Null(row.AcceptPath);
        Assert.Null(row.DeclinePath);

        // The notification body contains the message body (the UGC
        // snippet, ADR 0018).
        Assert.Contains("Hi Bob", row.Body);
    }

    // ── D7 · Blank / over-cap body → ArgumentException ───────────────────

    /// <summary>
    /// A blank body (empty string, whitespace-only) is rejected with
    /// <see cref="ArgumentException"/> before any DB write — the ADR 0101
    /// 400 shape. No <see cref="Message"/> row, no audit row, no
    /// notification.
    /// </summary>
    [Fact]
    public async Task Send_BlankBody_ArgumentException()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await svc.SetMessagingEnabledAsync(true, "u-admin");

        var convo = await svc.OpenConversationAsync("alice", "bob");

        await Assert.ThrowsAsync<ArgumentException>(() => svc.SendAsync(convo.Id, "alice", ""));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SendAsync(convo.Id, "alice", "   "));

        // No message was stored (the throw happened before the write).
        Assert.Empty(await QueryMessages(store, convo.Id));
        // No send audit row.
        Assert.DoesNotContain(await AuditRows(store), a => a.Action == "message.send");
        // No notification.
        Assert.DoesNotContain(await QueryNotifications(store), n => n.Kind == "message.new");
    }

    /// <summary>
    /// A body exceeding <see cref="MessagingService.MaxBodyChars"/> (2000)
    /// is rejected with <see cref="ArgumentException"/> — the D7 cap.
    /// No DB write.
    /// </summary>
    [Fact]
    public async Task Send_OverCap_ArgumentException()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await svc.SetMessagingEnabledAsync(true, "u-admin");

        var convo = await svc.OpenConversationAsync("alice", "bob");
        var overCap = new string('x', MessagingService.MaxBodyChars + 1);

        await Assert.ThrowsAsync<ArgumentException>(() => svc.SendAsync(convo.Id, "alice", overCap));

        // No message was stored.
        Assert.Empty(await QueryMessages(store, convo.Id));
        // No send audit row.
        Assert.DoesNotContain(await AuditRows(store), a => a.Action == "message.send");
    }

    // ── D3/D4 · Non-participant gets a non-leaky 404 ─────────────────────

    /// <summary>
    /// A resident who is **not** one of the two participants calls
    /// <see cref="IMessagingService.GetConversationAsync"/> and gets
    /// <see cref="KeyNotFoundException"/> (a 404) — never
    /// <see cref="UnauthorizedAccessException"/> (a 403), never data.
    /// The exception type is the non-leaky discriminator: a
    /// non-participant and a missing conversation are indistinguishable
    /// to the caller (D3/D4, C-M9·1).
    /// </summary>
    [Fact]
    public async Task GetConversation_NonParticipant_404_Not403()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await svc.SetMessagingEnabledAsync(true, "u-admin");

        var convo = await svc.OpenConversationAsync("alice", "bob");
        await svc.SendAsync(convo.Id, "alice", "A secret message");

        // "carol" is not a participant — 404, not 403.
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetConversationAsync(convo.Id, "carol", 1));
        // The exception message does not leak whether the conversation
        // exists (non-leaky — the same message for a missing id).
        Assert.Equal("The conversation was not found.", ex.Message);

        // No audit row for the read (C-M9·4).
        Assert.DoesNotContain(await AuditRows(store), a => a.ActorId == "carol");
    }

    /// <summary>
    /// A GlobalAdmin who is **not** a participant of the conversation
    /// also gets <see cref="KeyNotFoundException"/> (404) — the
    /// participant-by-id check is the whole access story (C-M9·5: zero
    /// new authorization surface; no <c>IAuthorizationService</c> call).
    /// </summary>
    [Fact]
    public async Task GetConversation_GlobalAdminNonParticipant_404()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await svc.SetMessagingEnabledAsync(true, "u-admin");

        var convo = await svc.OpenConversationAsync("alice", "bob");
        await svc.SendAsync(convo.Id, "alice", "A secret message");

        // "u-admin" is a GlobalAdmin but not a participant — 404.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetConversationAsync(convo.Id, "u-admin", 1));
    }

    // ── F4 · ListConversations shows only the caller's own conversations ─

    /// <summary>
    /// <see cref="IMessagingService.ListConversationsAsync"/> returns only
    /// the caller's conversations. A non-participant (<c>"carol"</c>) sees
    /// an empty list — no leak of the <c>alice</c>↔<c>bob</c>
    /// conversation (F4, C-M9·1).
    /// </summary>
    [Fact]
    public async Task ListConversations_OnlyOwn_NoLeak()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await svc.SetMessagingEnabledAsync(true, "u-admin");

        var convo = await svc.OpenConversationAsync("alice", "bob");
        await svc.SendAsync(convo.Id, "alice", "Private message");

        // alice sees her one conversation.
        var aliceList = await svc.ListConversationsAsync("alice", 1);
        Assert.Single(aliceList.Items);
        Assert.Equal("bob", aliceList.Items[0].OtherParticipantId);
        Assert.Equal("Private message", aliceList.Items[0].LastMessageBody);

        // bob also sees it (he's the other participant).
        var bobList = await svc.ListConversationsAsync("bob", 1);
        Assert.Single(bobList.Items);
        Assert.Equal("alice", bobList.Items[0].OtherParticipantId);

        // carol sees nothing — no leak.
        var carolList = await svc.ListConversationsAsync("carol", 1);
        Assert.Empty(carolList.Items);
        Assert.False(carolList.HasMore);
    }

    // ── D8 · MarkRead sets ReadBy for the caller only ────────────────────

    /// <summary>
    /// <see cref="IMessagingService.MarkReadAsync"/> sets
    /// <see cref="Message.ReadBy"/> to the caller's id on the messages
    /// the *other* participant sent that the caller has not yet read.
    /// The sender's own messages (where <c>SenderId == caller</c>) are
    /// **not** touched — the other participant's read state is
    /// unchanged. No audit row (C-M9·4).
    /// </summary>
    [Fact]
    public async Task MarkRead_SetsReadByForCallerOnly()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await svc.SetMessagingEnabledAsync(true, "u-admin");

        var convo = await svc.OpenConversationAsync("alice", "bob");
        // alice sends to bob (bob's unread count should be 1).
        await svc.SendAsync(convo.Id, "alice", "Hello from alice");
        // bob sends back (alice's unread count should be 1).
        await svc.SendAsync(convo.Id, "bob", "Hello from bob");

        // Before MarkRead: both have 1 unread.
        var aliceBefore = await svc.GetConversationAsync(convo.Id, "alice", 1);
        var bobBefore   = await svc.GetConversationAsync(convo.Id, "bob",   1);
        Assert.Equal(1, aliceBefore.Conversation.UnreadCount);
        Assert.Equal(1, bobBefore.Conversation.UnreadCount);

        // alice marks her view as read: this sets ReadBy = "alice" on
        // messages where SenderId == "bob" (the ones alice hasn't read).
        await svc.MarkReadAsync(convo.Id, "alice");

        // After MarkRead: alice's unread count drops to 0 (bob's message
        // is now read by alice). Bob's unread count is unchanged (his view
        // of alice's message is still unread — D8: caller-only).
        var aliceAfter = await svc.GetConversationAsync(convo.Id, "alice", 1);
        var bobAfter   = await svc.GetConversationAsync(convo.Id, "bob",   1);
        Assert.Equal(0, aliceAfter.Conversation.UnreadCount);
        Assert.Equal(1, bobAfter.Conversation.UnreadCount);

        // Verify at the DB level: the message bob sent has ReadBy = "alice";
        // the message alice sent has ReadBy = null (unchanged).
        var messages = await QueryMessages(store, convo.Id);
        var aliceMsg = messages.First(m => m.SenderId == "alice");
        var bobMsg   = messages.First(m => m.SenderId == "bob");
        Assert.Null(aliceMsg.ReadBy);          // alice's own message: unchanged
        Assert.Equal("alice", bobMsg.ReadBy);  // bob's message: marked read by alice

        // No audit row for MarkRead (C-M9·4 — reads / read-state never audit).
        Assert.DoesNotContain(await AuditRows(store), a => a.Action == "mark.read");
    }

    // ── D2 · Toggle off: all non-admin seams refuse with 403 ─────────────

    /// <summary>
    /// With the messaging toggle **off** (the <c>false</c> floor), every
    /// non-admin seam — <see cref="IMessagingService
    /// .OpenConversationAsync"/>, <see cref="IMessagingService
    /// .GetConversationAsync"/>, <see cref="IMessagingService
    /// .ListConversationsAsync"/>, <see cref="IMessagingService
    /// .SendAsync"/>, <see cref="IMessagingService
    /// .MarkReadAsync"/> — throws
    /// <see cref="UnauthorizedAccessException"/> (→ 403). The toggle
    /// seams themselves (U02) remain reachable.
    /// </summary>
    [Fact]
    public async Task ToggleOff_AllSeamsRefuse_403()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        // The toggle is off by default (the false floor) — do NOT enable it.
        Assert.False(await svc.IsMessagingEnabledAsync());

        // OpenConversationAsync → 403
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.OpenConversationAsync("alice", "bob"));

        // GetConversationAsync → 403 (even with a real id)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.GetConversationAsync("any-id", "alice", 1));

        // ListConversationsAsync → 403
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.ListConversationsAsync("alice", 1));

        // SendAsync → 403
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.SendAsync("any-id", "alice", "hello"));

        // MarkReadAsync → 403
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.MarkReadAsync("any-id", "alice"));

        // The toggle seam itself is still reachable (U02 — the admin can
        // always flip the gate).
        await svc.SetMessagingEnabledAsync(true, "u-admin");
        Assert.True(await svc.IsMessagingEnabledAsync());
    }

    // ── Harness ──────────────────────────────────────────────────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            // M1DocTypes registers LocaleSettings (the toggle's home) and
            // AccessAudit (the audit-row pin needs it in the schema).
            M1DocTypes.Configure(opts);
            // M9DocTypes registers Conversation / Message (the M9 surface
            // the service writes to; the U02 toggle pins only touch
            // LocaleSettings, but the schema is the full lane's).
            M9DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Query the AccessAudit lane for the row-shape pins.</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().ToListAsync(ct);
    }

    /// <summary>
    /// The U03 harness: a scratch store with <c>M6DocTypes</c> registered
    /// (so the <see cref="Notification"/> table exists for the nudge pin),
    /// a <see cref="MessagingService"/> constructed with a
    /// <see cref="NotificationService"/> (the ADR 0077 optional-nudge-param
    /// wired) and a <see cref="IUserInfoService"/> substitute (returns
    /// <c>null</c> for every profile read — the <c>OtherDisplayName</c>
    /// is <c>null</c> in these tests, which is the expected behavior when
    /// no directory row exists).
    /// </summary>
    private async Task<(IDocumentStore store, MessagingService svc)>
        BootStoreWithNotificationsAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);   // LocaleSettings + AccessAudit
            M6DocTypes.Configure(opts);   // Notification (the D6 nudge table)
            M9DocTypes.Configure(opts);   // Conversation + Message
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        // The directory read lane (IUserInfoService) — null for every
        // subject (no Profile rows are planted in these tests).
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>())
            .Returns(_ => Task.FromResult<Profile?>(null));

        // The translator (the ADR 0061 seam) — key-derived markers
        // (the same RecordingTranslator shape as NotificationServiceTests).
        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => Task.FromResult($"{ci[0]}-en"));

        // The mailer (the M1 durable-email seam) — records the staged
        // tuples; never dispatches (test isolation).
        var mailer = Substitute.For<IMailerStage>();
        mailer.StageAsync(
                Arg.Any<IDocumentSession>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var notificationService = new NotificationService(
            store, userInfo, translator, mailer);

        var svc = new MessagingService(store, userInfo, notificationService);
        return (store, svc);
    }

    /// <summary>Query all <see cref="Conversation"/> rows in the store.</summary>
    private static async Task<IReadOnlyList<Conversation>> QueryConversations(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<Conversation>().ToListAsync(ct);
    }

    /// <summary>Query all <see cref="Message"/> rows for a conversation.</summary>
    private static async Task<IReadOnlyList<Message>> QueryMessages(IDocumentStore store, string conversationId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<Message>().Where(m => m.ConversationId == conversationId).ToListAsync(ct);
    }

    /// <summary>Query all <see cref="Notification"/> rows in the store.</summary>
    private static async Task<IReadOnlyList<Notification>> QueryNotifications(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<Notification>().ToListAsync(ct);
    }
}
