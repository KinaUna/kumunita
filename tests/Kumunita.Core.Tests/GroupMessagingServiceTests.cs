using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.GroupMessaging;
using Kumunita.Core.Messaging;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <see cref="GroupMessagingService"/> behavior pins (the group-messaging
/// lane — the N:1 generalization of the M9 1:1 surface in ADR 0105, with the
/// invitation flow added by the ADR 0278 amendment). The shape follows
/// <see cref="MessagingServiceTests"/> — same <see cref="PostgresFixture"/>
/// , same <c>BootStoreAsync</c> / <c>AuditRows</c> helpers, fresh scratch
/// Postgres per test method.
/// <para>
/// The pins this harness owns:
/// <list type="number">
/// <item><b>Open — the creator is the only initial participant.</b>
/// <see cref="IGroupMessagingService
/// .OpenGroupAsync"/> stores exactly one <c>GroupConversation</c> row whose
/// <c>Participants</c> is just the creator, one
/// <c>GroupMessagingInvitation</c> Pending row per distinct recipient
/// (the invitation flow, ADR 0278 amendment), one
/// <c>group.message.invite</c> nudge per recipient (the accept/decline
/// deep-links), and commits one <c>group.message.open</c>
/// <see cref="AccessAudit"/> row with <c>Via = Owner</c> /
/// <c>Outcome = Allow</c>.</item>
/// <item><b>Open — the one-person "group" is refused.</b> An empty recipient
/// list, or a recipient list that contains only the actor, is an
/// <see cref="ArgumentException"/> (a one-person group is a 1:1 — the M9
/// surface covers that) and no row is stored.</item>
/// <item><b>Accept — the self-lane adds the invitee to the participant set.
/// </b> <see cref="IGroupMessagingService.AcceptInviteAsync"/> by the
/// invitee moves their <c>GroupMessagingInvitation</c> row Pending →
/// Accepted and appends their id to <c>Participants</c> (sorted, de-duped)
/// — they become a participant on the very next read (invariant C4) — and
/// commits one <c>group.message.invite.accept</c> audit row
/// (<c>Via = Owner</c>).</item>
/// <item><b>Decline — the self-lane never touches the participant set.</b>
/// <see cref="IGroupMessagingService.DeclineInviteAsync"/> by the invitee
/// moves their row Pending → Declined; <b>no</b> participant set is touched
/// (the invitee never becomes a member), and one
/// <c>group.message.invite.decline</c> audit row is committed.</item>
/// <item><b>Accept/decline — the self-lane gate + the C-M2b·3
/// invalid-transition wall.</b> A non-invitee (a resident who was not
/// invited on this conversation) gets an
/// <see cref="InvalidOperationException"/>; an already-resolved row
/// (Accepted / Declined) is an invalid transition (the same wall).</item>
/// <item><b>Member roster — accepted + pending, the participant gate.</b>
/// <see cref="IGroupMessagingService.GetMembersAsync"/> by a participant
/// returns the accepted participants + the pending invitees (each with their
/// <c>Pending</c> flag set); by a non-participant it throws
/// <see cref="KeyNotFoundException"/> (the non-leaky 404 — a non-participant
/// cannot see the roster, that would leak the pending invitees' presence).
/// </item>
/// <item><b>Non-leaky 404 — a non-participant's read is indistinguishable
/// from a missing id.</b> <see cref="IGroupMessagingService
/// .GetConversationAsync"/> with a known id but a non-participant actor
/// throws <see cref="KeyNotFoundException"/> — never 403, never a leak (the
/// D3/D4 shape from ADR 0105, generalized to the N:1 participant set).</item>
/// <item><b>Send — the nudge universe is every participant except the
/// sender.</b> After the invitees accept, <see cref="IGroupMessagingService
/// .SendAsync"/> stores exactly one <c>GroupMessage</c> row, one
/// <c>group.message.send</c> audit row, and (with the notification lane
/// wired) exactly one <c>group.message.new</c> <see cref="Notification"/>
/// row per participant other than the sender — the sender never nudges
/// themself, and a pending invitee (not yet a participant) is not nudged.
/// </item>
/// <item><b>Invite members — the any-participant-can-invite rule.</b>
/// <see cref="IGroupMessagingService.InviteMembersAsync"/> by a participant
/// creates a <c>GroupMessagingInvitation</c> Pending row for each invitee
/// (de-duped against the participant set — a re-invite of an existing member
/// is a no-op) + one <c>group.message.invite</c> nudge per invitee, and
/// commits one <c>group.message.invite</c> audit row; by a non-participant
/// it throws <see cref="KeyNotFoundException"/> (the non-leaky 404 shape)
/// and never writes.</item>
/// <item><b>F5 — the toggle is the whole gate (carried to the N:1
/// surface).</b> With the instance toggle off, every non-toggle seam refuses
/// with <see cref="UnauthorizedAccessException"/> before any row is read or
/// written — the same C-M9·2 hard gate as the M9 surface.</item>
/// </list>
/// </summary>
public class GroupMessagingServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Open — the creator is the only initial participant ───────────────

    /// <summary>
    /// <see cref="IGroupMessagingService.OpenGroupAsync"/> stores exactly one
    /// <c>GroupConversation</c> row whose <c>Participants</c> is just the
    /// creator (the invitation flow — the recipients are pending invitees,
    /// not participants yet), one <c>GroupMessagingInvitation</c> Pending
    /// row per distinct recipient, one <c>group.message.invite</c> nudge per
    /// recipient (the accept/decline deep-links), and commits one
    /// <c>group.message.open</c> audit row with <c>Via = Owner</c> /
    /// <c>Outcome = Allow</c> (the D5 shape from ADR 0105, generalized to the
    /// group surface).
    /// </summary>
    [Fact]
    public async Task OpenGroup_CreatorOnlyParticipant_PendingInvites_AndAuditRow()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);

        var created = await svc.OpenGroupAsync("alice", new[] { "carol", "bob" });

        // The participant set is just the creator (the invitation flow —
        // bob + carol are pending invitees, not participants yet).
        Assert.Equal(new[] { "alice" }, created.ParticipantIds);
        Assert.Equal("alice", created.CreatorId);

        var convos = await QueryGroupConversations(store);
        Assert.Single(convos);
        Assert.Equal(new[] { "alice" }, convos[0].Participants);
        Assert.Equal("alice", convos[0].CreatorId);

        // Exactly one Pending invitation row per distinct recipient (bob +
        // carol), each with InvitedBy = the creator (alice).
        var invites = await QueryInvitations(store);
        var pending = invites.Where(i => i.Status == GroupMessagingInvitationStatus.Pending).ToList();
        Assert.Equal(2, pending.Count);
        Assert.Contains(pending, i => i.InvitedId == "bob" && i.InvitedBy == "alice");
        Assert.Contains(pending, i => i.InvitedId == "carol" && i.InvitedBy == "alice");
        Assert.All(pending, i => Assert.Equal(created.Id, i.ConversationId));

        // Exactly two group.message.invite nudges — one per recipient (bob +
        // carol), each with the accept/decline deep-links (the ADR 0095
        // shape, generalized to the group-message thread).
        var notifs = await QueryNotifications(store);
        var nudges = notifs.Where(n => n.Kind == "group.message.invite").ToList();
        Assert.Equal(2, nudges.Count);
        Assert.Contains(nudges, n => n.RecipientId == "bob");
        Assert.Contains(nudges, n => n.RecipientId == "carol");
        Assert.DoesNotContain(nudges, n => n.RecipientId == "alice");
        Assert.All(nudges, n => Assert.Equal($"/group-messages/{created.Id}", n.LinkPath));
        Assert.All(nudges, n => Assert.Equal($"/group-messages/{created.Id}/invitations/accept", n.AcceptPath));
        Assert.All(nudges, n => Assert.Equal($"/group-messages/{created.Id}/invitations/decline", n.DeclinePath));
        // The idempotency key is the (conversation, invitee) shape (one row
        // per (thread, invitee), the business key).
        Assert.All(nudges, n => Assert.StartsWith(
            "notification:group.message.invite:", n.IdempotencyKey));

        // Exactly one group.message.open audit row (the D5 shape).
        var audits = await AuditRows(store);
        var row = Assert.Single(audits, a => a.Action == "group.message.open");
        Assert.Equal("alice", row.ActorId);
        Assert.Equal(Authorization.AccessVia.Owner, row.Via);
        Assert.Equal(Authorization.AccessOutcome.Allow, row.Outcome);
        Assert.Equal("group.message", row.TargetKind);
        Assert.Equal(created.Id, row.TargetId);
    }

    /// <summary>
    /// The one-person "group" is refused — an empty recipient list, or a
    /// recipient list that contains only the actor (de-duped to the creator
    /// alone), is an <see cref="ArgumentException"/> and no row is stored
    /// (a one-person group is a 1:1 — the M9 surface covers that).
    /// </summary>
    [Fact]
    public async Task OpenGroup_OnePersonGroup_ThrowsArgument_AndStoresNothing()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);

        // Empty recipient list.
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.OpenGroupAsync("alice", Array.Empty<string>()));

        // A recipient list that de-dupes to only the actor.
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.OpenGroupAsync("alice", new[] { "alice" }));

        var convos = await QueryGroupConversations(store);
        Assert.Empty(convos);
        var invites = await QueryInvitations(store);
        Assert.Empty(invites);
        var audits = await AuditRows(store);
        Assert.DoesNotContain(audits, a => a.Action == "group.message.open");
    }

    // ── Accept — the self-lane adds the invitee to the participant set ────

    /// <summary>
    /// <see cref="IGroupMessagingService.AcceptInviteAsync"/> by the
    /// invitee moves their <c>GroupMessagingInvitation</c> row Pending →
    /// Accepted, appends their id to <c>Participants</c> (sorted, de-duped —
    /// they become a participant on the very next read, invariant C4), and
    /// commits one <c>group.message.invite.accept</c> audit row
    /// (<c>Via = Owner</c> / <c>Outcome = Allow</c>).
    /// </summary>
    [Fact]
    public async Task AcceptInvite_ByInvitee_AddsToParticipants_AndAuditRow()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);
        var convo = await svc.OpenGroupAsync("alice", new[] { "bob", "carol" });

        // bob (an invitee) accepts — the self-lane.
        await svc.AcceptInviteAsync(convo.Id, "bob");

        // bob is now a participant (the participant set is [alice, bob],
        // sorted). carol is still pending (she has not accepted).
        var convos = await QueryGroupConversations(store);
        var updated = Assert.Single(convos);
        Assert.Equal(new[] { "alice", "bob" }, updated.Participants);
        Assert.DoesNotContain("carol", updated.Participants);

        // bob's invite row is now Accepted (ResolvedBy = bob, ResolvedAt
        // stamped); carol's is still Pending.
        var invites = await QueryInvitations(store);
        var bobRow = Assert.Single(invites, i => i.InvitedId == "bob");
        Assert.Equal(GroupMessagingInvitationStatus.Accepted, bobRow.Status);
        Assert.Equal("bob", bobRow.ResolvedBy);
        Assert.NotNull(bobRow.ResolvedAt);
        var carolRow = Assert.Single(invites, i => i.InvitedId == "carol");
        Assert.Equal(GroupMessagingInvitationStatus.Pending, carolRow.Status);

        // Exactly one group.message.invite.accept audit row.
        var audits = await AuditRows(store);
        var acceptRow = Assert.Single(audits, a => a.Action == "group.message.invite.accept");
        Assert.Equal("bob", acceptRow.ActorId);
        Assert.Equal("bob", acceptRow.EffectivePrincipalId);
        Assert.Equal(Authorization.AccessVia.Owner, acceptRow.Via);
        Assert.Equal(Authorization.AccessOutcome.Allow, acceptRow.Outcome);
        Assert.Equal("group.message", acceptRow.TargetKind);
        Assert.Equal(convo.Id, acceptRow.TargetId);
    }

    // ── Decline — the self-lane never touches the participant set ─────────

    /// <summary>
    /// <see cref="IGroupMessagingService.DeclineInviteAsync"/> by the
    /// invitee moves their <c>GroupMessagingInvitation</c> row Pending →
    /// Declined; <b>no</b> participant set is touched (the invitee never
    /// becomes a member), and one <c>group.message.invite.decline</c> audit
    /// row is committed (<c>Via = Owner</c> / <c>Outcome = Allow</c>).
    /// </summary>
    [Fact]
    public async Task DeclineInvite_ByInvitee_LeavesParticipantSet_Untouched_AndAuditRow()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);
        var convo = await svc.OpenGroupAsync("alice", new[] { "bob", "carol" });

        // bob (an invitee) declines — the self-lane.
        await svc.DeclineInviteAsync(convo.Id, "bob");

        // bob is NOT a participant (the participant set is unchanged — just
        // the creator, alice). carol is still pending.
        var convos = await QueryGroupConversations(store);
        var unchanged = Assert.Single(convos);
        Assert.Equal(new[] { "alice" }, unchanged.Participants);
        Assert.DoesNotContain("bob", unchanged.Participants);

        // bob's invite row is now Declined (ResolvedBy = bob, ResolvedAt
        // stamped).
        var invites = await QueryInvitations(store);
        var bobRow = Assert.Single(invites, i => i.InvitedId == "bob");
        Assert.Equal(GroupMessagingInvitationStatus.Declined, bobRow.Status);
        Assert.Equal("bob", bobRow.ResolvedBy);
        Assert.NotNull(bobRow.ResolvedAt);

        // Exactly one group.message.invite.decline audit row.
        var audits = await AuditRows(store);
        var declineRow = Assert.Single(audits, a => a.Action == "group.message.invite.decline");
        Assert.Equal("bob", declineRow.ActorId);
        Assert.Equal("bob", declineRow.EffectivePrincipalId);
        Assert.Equal(Authorization.AccessVia.Owner, declineRow.Via);
        Assert.Equal(Authorization.AccessOutcome.Allow, declineRow.Outcome);
        Assert.Equal(convo.Id, declineRow.TargetId);
    }

    // ── Accept/decline — the self-lane gate + the C-M2b·3 wall ────────────

    /// <summary>
    /// <see cref="IGroupMessagingService.AcceptInviteAsync"/> by a
    /// <b>non-invitee</b> (a resident who was not invited on this
    /// conversation) throws <see cref="InvalidOperationException"/> — the
    /// self-lane gate (C-M2b·2). The participant set is unchanged.
    /// </summary>
    [Fact]
    public async Task AcceptInvite_ByNonInvitee_ThrowsInvalidOperation_AndStoresNothing()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);
        var convo = await svc.OpenGroupAsync("alice", new[] { "bob" });

        // carol is NOT an invitee on this conversation — her self-lane call
        // is refused (the Core's C-M2b·2 wall; the Web's 404 gate is the
        // first wall).
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AcceptInviteAsync(convo.Id, "carol"));

        // The participant set is unchanged (just the creator, alice); bob's
        // invite row is still Pending.
        var convos = await QueryGroupConversations(store);
        Assert.Equal(new[] { "alice" }, Assert.Single(convos).Participants);
        var invites = await QueryInvitations(store);
        Assert.Equal(GroupMessagingInvitationStatus.Pending,
            Assert.Single(invites, i => i.InvitedId == "bob").Status);
        var audits = await AuditRows(store);
        Assert.DoesNotContain(audits, a => a.Action == "group.message.invite.accept");
    }

    /// <summary>
    /// <see cref="IGroupMessagingService.AcceptInviteAsync"/> on an
    /// <b>already-resolved</b> row (Accepted / Declined) throws
    /// <see cref="InvalidOperationException"/> — the C-M2b·3
    /// invalid-transition wall (a double-accept, or an accept-after-decline,
    /// is refused).
    /// </summary>
    [Fact]
    public async Task AcceptInvite_AlreadyResolved_ThrowsInvalidOperation()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);
        var convo = await svc.OpenGroupAsync("alice", new[] { "bob", "carol" });

        // bob accepts — the row moves Pending → Accepted.
        await svc.AcceptInviteAsync(convo.Id, "bob");

        // A second accept (a double-accept) is an invalid transition.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AcceptInviteAsync(convo.Id, "bob"));

        // carol declines — the row moves Pending → Declined.
        await svc.DeclineInviteAsync(convo.Id, "carol");

        // An accept-after-decline is an invalid transition.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.AcceptInviteAsync(convo.Id, "carol"));
    }

    // ── Member roster — accepted + pending, the participant gate ──────────

    /// <summary>
    /// <see cref="IGroupMessagingService.GetMembersAsync"/> by a participant
    /// returns the accepted participants + the pending invitees (each with
    /// their <c>Pending</c> flag set — the view lists both, with the pending
    /// ones marked "(pending)").
    /// </summary>
    [Fact]
    public async Task GetMembers_ByParticipant_ReturnsAcceptedAndPending()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);
        var convo = await svc.OpenGroupAsync("alice", new[] { "bob", "carol" });

        // alice is the only participant; bob + carol are pending invitees.
        var members = await svc.GetMembersAsync(convo.Id, "alice");
        Assert.Equal(3, members.Members.Count);
        Assert.Contains(members.Members, m => m.SubjectId == "alice" && !m.Pending);
        Assert.Contains(members.Members, m => m.SubjectId == "bob" && m.Pending);
        Assert.Contains(members.Members, m => m.SubjectId == "carol" && m.Pending);

        // After bob accepts, the roster is alice + bob (accepted) + carol
        // (pending) — bob's Pending flag flips to false.
        await svc.AcceptInviteAsync(convo.Id, "bob");
        var members2 = await svc.GetMembersAsync(convo.Id, "alice");
        Assert.Equal(3, members2.Members.Count);
        Assert.Contains(members2.Members, m => m.SubjectId == "bob" && !m.Pending);
        Assert.Contains(members2.Members, m => m.SubjectId == "carol" && m.Pending);
    }

    /// <summary>
    /// <see cref="IGroupMessagingService.GetMembersAsync"/> by a
    /// <b>non-participant</b> throws <see cref="KeyNotFoundException"/>
    /// (the non-leaky 404 — a non-participant cannot see the roster, that
    /// would leak the pending invitees' presence).
    /// </summary>
    [Fact]
    public async Task GetMembers_ByNonParticipant_ThrowsKeyNotFound()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);
        var convo = await svc.OpenGroupAsync("alice", new[] { "bob" });

        // carol is a pending invitee, NOT a participant — she cannot see the
        // roster (that would leak bob's pending-invite presence).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetMembersAsync(convo.Id, "carol"));
    }

    // ── Non-leaky 404 — a non-participant's read ──────────────────────────

    /// <summary>
    /// <see cref="IGroupMessagingService.GetConversationAsync"/> with a known
    /// conversation id but a non-participant actor throws
    /// <see cref="KeyNotFoundException"/> — never 403, never a leak (the
    /// D3/D4 shape from ADR 0105, generalized to the N:1 participant set).
    /// A missing id throws the same exception (indistinguishable).
    /// </summary>
    [Fact]
    public async Task GetConversation_NonParticipant_ThrowsKeyNotFound_Indistinguishable()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);
        var convo = await svc.OpenGroupAsync("alice", new[] { "bob" });

        // carol (a pending invitee, not a participant) is refused with the
        // non-leaky 404.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetConversationAsync(convo.Id, "carol", 1));

        // A missing id is the same shape (indistinguishable).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.GetConversationAsync("no-such-id", "alice", 1));
    }

    // ── Send — the nudge universe + the audit row ─────────────────────────

    /// <summary>
    /// After the invitees accept, <see cref="IGroupMessagingService
    /// .SendAsync"/> stores exactly one <c>GroupMessage</c> row, one
    /// <c>group.message.send</c> audit row (<c>Via = Owner</c> /
    /// <c>Outcome = Allow</c>, the D5 shape), and (with the notification
    /// lane wired) exactly one <c>group.message.new</c>
    /// <see cref="Notification"/> row per participant other than the sender —
    /// the sender never nudges themself, and a pending invitee (not yet a
    /// participant) is not nudged (the D6 shape from ADR 0105, generalized
    /// to the N:1 nudge universe, with the invitation-flow twist that a
    /// pending invitee is outside the nudge set until they accept).
    /// </summary>
    [Fact]
    public async Task Send_NudgesEveryParticipantExceptSender_PendingInvitee_NotNudged()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);
        var convo = await svc.OpenGroupAsync("alice", new[] { "bob", "carol" });

        // bob accepts (becomes a participant); carol stays pending.
        await svc.AcceptInviteAsync(convo.Id, "bob");

        // alice → the participants (alice + bob), except the sender (alice).
        // carol is still pending — she is NOT nudged.
        await svc.SendAsync(convo.Id, "alice", "hello everyone");

        // Exactly one message row.
        var messages = await QueryGroupMessages(store, convo.Id);
        var msg = Assert.Single(messages);
        Assert.Equal("alice", msg.SenderId);
        Assert.Equal("hello everyone", msg.Body);
        Assert.Null(msg.ReadBy);

        // Exactly one group.message.send audit row (the D5 shape).
        var audits = await AuditRows(store);
        var sendRow = Assert.Single(audits, a => a.Action == "group.message.send");
        Assert.Equal("alice", sendRow.ActorId);
        Assert.Equal(Authorization.AccessVia.Owner, sendRow.Via);
        Assert.Equal(Authorization.AccessOutcome.Allow, sendRow.Outcome);

        // Exactly one group.message.new nudge — for bob (the only
        // non-sender participant). carol (still pending) and alice (the
        // sender) are NOT nudged.
        var notifs = await QueryNotifications(store);
        var nudges = notifs.Where(n => n.Kind == "group.message.new").ToList();
        var nudge = Assert.Single(nudges);
        Assert.Equal("bob", nudge.RecipientId);
        Assert.DoesNotContain(nudges, n => n.RecipientId == "carol");
        Assert.DoesNotContain(nudges, n => n.RecipientId == "alice");
        Assert.All(nudges, n => Assert.StartsWith(
            "notification:group.message.new:", n.IdempotencyKey));
        Assert.All(nudges, n => Assert.Equal($"/group-messages/{convo.Id}", n.LinkPath));
    }

    // ── Mark read — the per-participant read marker ───────────────────────

    /// <summary>
    /// <see cref="IGroupMessagingService.MarkReadAsync"/> appends the
    /// actor's id to <c>ReadBy</c> on the actor's unread messages (sender
    /// ≠ actor) only. The sender's own messages and the other
    /// participants' read state are never touched (the D8 shape from ADR
    /// 0105, generalized to the per-participant read-marker set — the
    /// sender's message is still unread for every other participant).
    /// </summary>
    [Fact]
    public async Task MarkRead_AppendsActorToReadBy_OnTheirUnreadMessagesOnly()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);
        var convo = await svc.OpenGroupAsync("alice", new[] { "bob", "carol" });

        // bob + carol accept (both become participants).
        await svc.AcceptInviteAsync(convo.Id, "bob");
        await svc.AcceptInviteAsync(convo.Id, "carol");

        // alice → bob + carol; bob → alice + carol.
        await svc.SendAsync(convo.Id, "alice", "from alice");
        await svc.SendAsync(convo.Id, "bob", "from bob");

        // bob marks the thread read: his own message is untouched (he
        // "read" it by sending it), alice's message gains bob's id.
        await svc.MarkReadAsync(convo.Id, "bob");

        var messages = await QueryGroupMessages(store, convo.Id);
        var fromAlice = Assert.Single(messages, m => m.SenderId == "alice");
        var fromBob = Assert.Single(messages, m => m.SenderId == "bob");

        // bob's own message: ReadBy stays null (the sender's own message is
        // never marked read by the sender).
        Assert.Null(fromBob.ReadBy);
        // alice's message: bob's id is in ReadBy; carol's is not yet (she
        // has not marked it read — the per-participant marker).
        Assert.NotNull(fromAlice.ReadBy);
        Assert.Contains("bob", fromAlice.ReadBy!);
        Assert.DoesNotContain("carol", fromAlice.ReadBy!);

        // No audit row for a read-state lane (the C-M9·4 shape — reads /
        // read-state lanes never audit).
        var audits = await AuditRows(store);
        Assert.DoesNotContain(audits, a => a.Action == "group.message.mark_read");
    }

    // ── Invite members — the any-participant-can-invite rule ──────────────

    /// <summary>
    /// <see cref="IGroupMessagingService.InviteMembersAsync"/> by a
    /// participant creates a <c>GroupMessagingInvitation</c> Pending row for
    /// each invitee (de-duped against the participant set — a re-invite of an
    /// existing member is a no-op) + one <c>group.message.invite</c> nudge
    /// per invitee, and commits one <c>group.message.invite</c> audit row
    /// (<c>Via = Owner</c> / <c>Outcome = Allow</c>). The invitee is NOT
    /// added to the participant set (the invitation flow — they join only on
    /// accept).
    /// </summary>
    [Fact]
    public async Task InviteMembers_ByParticipant_CreatesPendingInvite_AndAuditRow()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);
        var convo = await svc.OpenGroupAsync("alice", new[] { "bob" });

        // alice (the creator, a participant) invites dave.
        await svc.InviteMembersAsync(convo.Id, "alice", new[] { "dave" });

        // dave is now a pending invitee (NOT a participant yet).
        var convos = await QueryGroupConversations(store);
        var unchanged = Assert.Single(convos);
        Assert.Equal(new[] { "alice" }, unchanged.Participants);
        Assert.DoesNotContain("dave", unchanged.Participants);

        var invites = await QueryInvitations(store);
        var daveRow = Assert.Single(invites, i => i.InvitedId == "dave");
        Assert.Equal(GroupMessagingInvitationStatus.Pending, daveRow.Status);
        Assert.Equal("alice", daveRow.InvitedBy);

        // Exactly one group.message.invite audit row (the invite action).
        var audits = await AuditRows(store);
        var inviteRow = Assert.Single(audits, a => a.Action == "group.message.invite");
        Assert.Equal("alice", inviteRow.ActorId);
        Assert.Equal(Authorization.AccessVia.Owner, inviteRow.Via);
        Assert.Equal(convo.Id, inviteRow.TargetId);

        // Exactly one group.message.invite nudge for dave (the invitee).
        var notifs = await QueryNotifications(store);
        var daveNudges = notifs.Where(n => n.Kind == "group.message.invite" && n.RecipientId == "dave").ToList();
        Assert.Single(daveNudges);
        Assert.Equal($"/group-messages/{convo.Id}/invitations/accept", daveNudges[0].AcceptPath);
    }

    /// <summary>
    /// <see cref="IGroupMessagingService.InviteMembersAsync"/> by a
    /// <b>non-participant</b> throws <see cref="KeyNotFoundException"/>
    /// (the non-leaky 404 shape, D3/D4) and never writes — the participant
    /// set is unchanged and no invitation row is created.
    /// </summary>
    [Fact]
    public async Task InviteMembers_ByNonParticipant_ThrowsKeyNotFound_AndStoresNothing()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        await EnableToggleAsync(store);
        var convo = await svc.OpenGroupAsync("alice", new[] { "bob" });

        // carol is a pending invitee, NOT a participant — she cannot invite
        // dave (the non-leaky 404).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.InviteMembersAsync(convo.Id, "carol", new[] { "dave" }));

        // The participant set is unchanged; no invitation row for dave.
        var convos = await QueryGroupConversations(store);
        Assert.Equal(new[] { "alice" }, Assert.Single(convos).Participants);
        var invites = await QueryInvitations(store);
        Assert.DoesNotContain(invites, i => i.InvitedId == "dave");
        var audits = await AuditRows(store);
        Assert.DoesNotContain(audits, a => a.Action == "group.message.invite");
    }

    // ── F5 — the toggle is the whole gate (carried to the N:1 surface) ────

    /// <summary>
    /// With the instance toggle off (the M9 <c>false</c> floor — the same
    /// <c>LocaleSettings.MessagingEnabled</c> master gate as the 1:1
    /// surface), every non-toggle group-messaging seam refuses with
    /// <see cref="UnauthorizedAccessException"/> before any row is read or
    /// written (the C-M9·2 hard gate, carried to the N:1 surface). No
    /// <c>group.*</c> audit row is committed.
    /// </summary>
    [Fact]
    public async Task ToggleOff_AllGroupSeams_Refuse_AndStoreNothing()
    {
        var (store, svc) = await BootStoreWithNotificationsAsync();
        // The toggle is off (the fresh-instance floor).

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.OpenGroupAsync("alice", new[] { "bob" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.GetConversationAsync("any-id", "alice", 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.ListGroupsAsync("alice", 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.SendAsync("any-id", "alice", "hello"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.MarkReadAsync("any-id", "alice"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.InviteMembersAsync("any-id", "alice", new[] { "bob" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.AcceptInviteAsync("any-id", "alice"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.DeclineInviteAsync("any-id", "alice"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.GetMembersAsync("any-id", "alice"));

        var convos = await QueryGroupConversations(store);
        Assert.Empty(convos);
        var invites = await QueryInvitations(store);
        Assert.Empty(invites);
        var audits = await AuditRows(store);
        Assert.DoesNotContain(audits, a => a.Action.StartsWith("group.message"));
    }

    // ── Harness ──────────────────────────────────────────────────────────

    /// <summary>
    /// Enable the instance toggle for a test that needs it. The master gate
    /// is the M9 <c>LocaleSettings.MessagingEnabled</c> row — the group lane
    /// rides the same toggle (one master gate, one writer), so the write is
    /// done through a scratch <see cref="Messaging.MessagingService"/>
    /// (the M9 admin-write seam) over the same scratch store.
    /// </summary>
    private static async Task EnableToggleAsync(IDocumentStore store)
    {
        var msvc = new Messaging.MessagingService(store);
        await msvc.SetMessagingEnabledAsync(true, "u-admin");
    }

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            GroupMessagingDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private async Task<(IDocumentStore store, GroupMessagingService svc)>
        BootStoreWithNotificationsAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            NotificationDocTypes.Configure(opts);
            MessagingDocTypes.Configure(opts);        // LocaleSettings (the toggle's home)
            GroupMessagingDocTypes.Configure(opts);   // GroupConversation + GroupMessage + GroupMessagingInvitation
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        // The directory read lane (IUserInfoService) — null for every
        // subject (no Profile rows are planted in these tests).
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>())
            .Returns(_ => Task.FromResult<Profile?>(null));

        // The translator (the ADR 0061 seam) — key-derived markers.
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

        // The service is constructed with the notification lane wired (the
        // D6 nudge pin needs it). The IMentionService lane is left
        // unwired (the mention pin is the M36 surface's; this harness
        // focuses on the group lane's own seams).
        var svc = new GroupMessagingService(store, userInfo, notificationService);
        return (store, svc);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<AccessAudit>().ToListAsync(ct);
    }

    private static async Task<IReadOnlyList<GroupConversation>> QueryGroupConversations(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<GroupConversation>().ToListAsync(ct);
    }

    private static async Task<IReadOnlyList<GroupMessage>> QueryGroupMessages(IDocumentStore store, string conversationId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<GroupMessage>().Where(m => m.ConversationId == conversationId).ToListAsync(ct);
    }

    private static async Task<IReadOnlyList<GroupMessagingInvitation>> QueryInvitations(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<GroupMessagingInvitation>().ToListAsync(ct);
    }

    private static async Task<IReadOnlyList<Notification>> QueryNotifications(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<Notification>().ToListAsync(ct);
    }
}
