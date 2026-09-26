// ADR 0083 — the group-membership notification emitters:
//
// · AddGroupMemberAsync → kind group.added, recipient = the newly-added
//   resident, idempotency key notification:group.added:{groupId}:{userId},
//   body = the group's display name (appended by EmitAsync to the localized
//   template — the "You've been added to the group {0}" shape).
// · InviteGroupMemberAsync → kind group.invite, same shape (kind + key).
//
// Harness: the single-store shape (the `mt` schema — M1DocTypes for the
// Group/GroupMembership/GroupInvitation/Profile/AccessAudit tables +
// M6DocTypes for the Notification/NotificationPreference tables), the
// NotificationServiceTests shape with a real NotificationService over a
// recording mailer + a Substitute ITranslationProvider, and a
// UserInfoService wired with an IServiceProvider that resolves the same
// NotificationService (the circular-dependency avoidance: the service is
// resolved lazily at emission time, not at construction).
//
// No EF Core involved (no Identity tables, no roles) — the UserInfo lane
// is pure document-store.
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

public sealed class UserInfoServiceGroupNotificationTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    private const string GroupId = "grp-0083";
    private const string OwnerId = "subj-owner-0083";
    private const string AddedResident = "subj-added-0083";
    private const string InvitedResident = "subj-invited-0083";
    private const string NoEmailResident = "subj-noemail-0083";
    private const string GroupName = "Neighborhood Garden Club";

    // ── ADR 0083 D1: AddGroupMemberAsync emits group.added ─────────────────

    [Fact]
    public async Task AddGroupMember_EmitsGroupAddedRow_WithIdempotencyKey_AndStagesEmail()
    {
        var boot = await BootAsync();

        await boot.UserInfo.AddGroupMemberAsync(GroupId, AddedResident, OwnerId);

        var rows = await NotificationsFor(boot.Store, AddedResident, NotificationKinds.GroupAdded);
        var row = Assert.Single(rows);
        Assert.Equal(AddedResident, row.RecipientId);
        Assert.Equal(NotificationKinds.GroupAdded, row.Kind);
        Assert.Equal(
            $"notification:{NotificationKinds.GroupAdded}:{GroupId}:{AddedResident}",
            row.IdempotencyKey);
        // The UGC snippet (group name) is appended after a space to the
        // localized body template (the ADR 0076 / ADR 0061 shape — the same
        // shape the PostService / EventService emitters use).
        Assert.Contains(GroupName, row.Body);

        // The recipient has a planted profile with an email, so the M6
        // conditional stage ran (the recording mailer captured one envelope).
        var email = Assert.Single(boot.Staged);
        Assert.Equal($"notification:{NotificationKinds.GroupAdded}:{GroupId}:{AddedResident}", email.Key);
        Assert.Equal(AddedResident + "@ex.net", email.Recipient);
    }

    [Fact]
    public async Task AddGroupMember_Readd_EmitsOnlyOneRow_IdempotencyKeyDedups()
    {
        var boot = await BootAsync();

        // Two adds of the same (group, user) pair — the second is a re-stamp
        // of the membership row (AddedBy / At update), and the emitter fires
        // again with the SAME idempotency key → the M6 F10 dedup (the
        // IdempotencyKey index) collapses the second emission to a no-op:
        // no second inbox row, no second email.
        await boot.UserInfo.AddGroupMemberAsync(GroupId, AddedResident, OwnerId);
        await boot.UserInfo.AddGroupMemberAsync(GroupId, AddedResident, OwnerId);

        var rows = await NotificationsFor(boot.Store, AddedResident, NotificationKinds.GroupAdded);
        Assert.Single(rows);
        Assert.Single(boot.Staged);
    }

    [Fact]
    public async Task AddGroupMember_ResidentWithoutEmail_StillStoresInboxRow_NoEmail()
    {
        // The D5 invariant: the inbox row is the durable record; the email is
        // best-effort. A resident without a profile email still gets the
        // inbox row (they see it when they sign in / set up their email
        // later); no StageAsync call (there is no address to deliver to —
        // the M4 precedent).
        var boot = await BootAsync();

        // NoEmailResident's profile is seeded with Email = null (see
        // BootAsync) — the recipient has no address to deliver to.
        await boot.UserInfo.AddGroupMemberAsync(GroupId, NoEmailResident, OwnerId);

        var rows = await NotificationsFor(boot.Store, NoEmailResident, NotificationKinds.GroupAdded);
        Assert.Single(rows);                       // the inbox row IS stored
        Assert.Empty(boot.Staged);                 // no email (no address)
    }

    // ── ADR 0083 D2: InviteGroupMemberAsync emits group.invite ─────────────

    [Fact]
    public async Task InviteGroupMember_EmitsGroupInviteRow_WithIdempotencyKey_AndStagesEmail()
    {
        var boot = await BootAsync();

        await boot.UserInfo.InviteGroupMemberAsync(GroupId, InvitedResident, OwnerId);

        var rows = await NotificationsFor(boot.Store, InvitedResident, NotificationKinds.GroupInvite);
        var row = Assert.Single(rows);
        Assert.Equal(InvitedResident, row.RecipientId);
        Assert.Equal(NotificationKinds.GroupInvite, row.Kind);
        Assert.Equal(
            $"notification:{NotificationKinds.GroupInvite}:{GroupId}:{InvitedResident}",
            row.IdempotencyKey);
        Assert.Contains(GroupName, row.Body);

        var email = Assert.Single(boot.Staged);
        Assert.Equal($"notification:{NotificationKinds.GroupInvite}:{GroupId}:{InvitedResident}", email.Key);
        Assert.Equal(InvitedResident + "@ex.net", email.Recipient);
    }

    [Fact]
    public async Task InviteGroupMember_Reinvite_EmitsOnlyOneRow_IdempotencyKeyDedups()
    {
        var boot = await BootAsync();

        // C-M2b·3: a re-invite resets the row to Pending (InvitedBy/InvitedAt
        // updated, ResolvedAt/ResolvedBy cleared), and the emitter fires
        // again with the SAME idempotency key → the F10 dedup collapses the
        // second emission.
        await boot.UserInfo.InviteGroupMemberAsync(GroupId, InvitedResident, OwnerId);
        await boot.UserInfo.InviteGroupMemberAsync(GroupId, InvitedResident, OwnerId);

        var rows = await NotificationsFor(boot.Store, InvitedResident, NotificationKinds.GroupInvite);
        Assert.Single(rows);
        Assert.Single(boot.Staged);
    }

    [Fact]
    public async Task AddAndInvite_DistinctKinds_BothStoredForDifferentRecipients()
    {
        var boot = await BootAsync();

        await boot.UserInfo.AddGroupMemberAsync(GroupId, AddedResident, OwnerId);
        await boot.UserInfo.InviteGroupMemberAsync(GroupId, InvitedResident, OwnerId);

        Assert.Single(await NotificationsFor(boot.Store, AddedResident, NotificationKinds.GroupAdded));
        Assert.Single(await NotificationsFor(boot.Store, InvitedResident, NotificationKinds.GroupInvite));
        // Each recipient gets exactly one email (the two kinds are
        // independent — the preference gate is per-kind).
        Assert.Equal(2, boot.Staged.Count);
    }

    [Fact]
    public async Task WithoutNotificationService_AddAndInvite_Succeed_EmitNothing()
    {
        // The CS1736 seam: the pre-ADR 0083 test harnesses that build
        // UserInfoService(store) positionally pass services = null (the
        // default). The write still succeeds (the membership / invitation
        // rows commit), but no notification row is stored (the
        // NotificationService is simply not available).
        var boot = await BootAsyncWithoutNotifications();

        await boot.UserInfo.AddGroupMemberAsync(GroupId, AddedResident, OwnerId);
        await boot.UserInfo.InviteGroupMemberAsync(GroupId, InvitedResident, OwnerId);

        Assert.Empty(await NotificationsFor(boot.Store, AddedResident, null));
        Assert.Empty(await NotificationsFor(boot.Store, InvitedResident, null));
        Assert.Empty(boot.Staged);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed class Boot
    {
        public required IDocumentStore Store { get; init; }
        public required UserInfoService UserInfo { get; init; }
        public required List<(string Key, string Recipient, string Subject, string Body)> Staged { get; init; }
    }

    /// <summary>
    /// Boots the scratch store with the M1 + M6 surfaces (the
    /// <see cref="NotificationServiceTests"/> shape), wires a real
    /// <see cref="NotificationService"/> (recording mailer + translator),
    /// and builds a <see cref="UserInfoService"/> over the same store with
    /// an <see cref="IServiceProvider"/> that resolves that same
    /// <see cref="NotificationService"/> — the ADR 0083 lazy-resolution
    /// seam that breaks the DI cycle (see the UserInfoService class
    /// doc-comment).
    /// </summary>
    private async Task<Boot> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M6DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // Seed the Group row (AddGroupMemberAsync / InviteGroupMemberAsync
        // both LoadAsync<Group> first and throw "Group not found" on a miss).
        await using (var s = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            s.Store(new Group
            {
                Id = GroupId,
                Name = GroupName,
                OwnerId = OwnerId,
                Created = DateTimeOffset.UtcNow,
            });
            s.Store(new Profile
            {
                SubjectId = AddedResident,
                DisplayName = "Added Resident",
                Email = AddedResident + "@ex.net",
                Verified = true,
            });
            s.Store(new Profile
            {
                SubjectId = InvitedResident,
                DisplayName = "Invited Resident",
                Email = InvitedResident + "@ex.net",
                Verified = true,
            });
            // The D5 test resident — a profile with no email address (the
            // "they'll see it in the inbox when they sign in / add an email
            // later" path). The email nudge is skipped (no address to
            // deliver to) but the inbox row is still stored.
            s.Store(new Profile
            {
                SubjectId = NoEmailResident,
                DisplayName = "Resident without an email",
                Email = null,
                Verified = true,
            });
            await s.SaveChangesAsync(ct);
        }

        // The NotificationService — the same recording-mailer +
        // Substitute-ITranslationProvider shape as NotificationServiceTests.
        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => Task.FromResult((string)ci[0]));

        var staged = new List<(string Key, string Recipient, string Subject, string Body)>();
        var mailer = Substitute.For<IMailerStage>();
        mailer.StageAsync(
                Arg.Any<IDocumentSession>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                staged.Add(((string)callInfo[1], (string)callInfo[2], (string)callInfo[3], (string)callInfo[4]));
                return Task.CompletedTask;
            });

        // The IUserInfoService the NotificationService resolves the
        // recipient's Profile from — the real UserInfoService over the same
        // store (GetProfileAsync is a pure document read).
        var userInfoForNs = new UserInfoService(store);
        var ns = new NotificationService(store, userInfoForNs, translator, mailer);

        // The IServiceProvider the UserInfoService resolves the
        // NotificationService from at emission time (the ADR 0083
        // circular-dependency avoidance).
        var sc = new ServiceCollection();
        sc.AddSingleton(ns);
        var sp = sc.BuildServiceProvider();

        var userInfo = new UserInfoService(store, sp);

        return new Boot { Store = store, UserInfo = userInfo, Staged = staged };
    }

    /// <summary>
    /// Same boot as <see cref="BootAsync"/> but with <c>services = null</c>
    /// on the <see cref="UserInfoService"/> — the pre-ADR 0083 direct-
    /// construction shape (the 181 existing test call sites that build
    /// <c>UserInfoService(store)</c> positionally).
    /// </summary>
    private async Task<Boot> BootAsyncWithoutNotifications()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M6DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        await using (var s = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            s.Store(new Group
            {
                Id = GroupId,
                Name = GroupName,
                OwnerId = OwnerId,
                Created = DateTimeOffset.UtcNow,
            });
            await s.SaveChangesAsync(ct);
        }

        var userInfo = new UserInfoService(store);   // services = null → no emission

        return new Boot
        {
            Store = store,
            UserInfo = userInfo,
            Staged = [],
        };
    }

    private static async Task<IReadOnlyList<Notification>> NotificationsFor(
        IDocumentStore store, string recipientId, string? kind)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        IQueryable<Notification> query = q.Query<Notification>()
            .Where(n => n.RecipientId == recipientId);
        if (kind is not null)
            query = query.Where(n => n.Kind == kind);
        return await query.OrderBy(n => n.Created).ToListAsync(ct);
    }
}
