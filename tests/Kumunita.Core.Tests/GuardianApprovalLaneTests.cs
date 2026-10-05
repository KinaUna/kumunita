// GU community-approval lane (ADR 0141) — the supervised-child branch of the
// group-invite / community-add lanes + the guardian resolve lanes:
//
// · InviteGroupMemberAsync on a supervised child → one notification row per
//   ACTIVE guardian (kind guardian.group_invite, recipient = the guardian,
//   LinkPath / AcceptPath / DeclinePath = /me/children/{childId}); the
//   invitation row stays Pending (the membership does not land until the
//   guardian approves).
// · InviteGroupMemberAsync on an unsupervised resident → the ADR 0083
//   shape unchanged (kind group.invite, recipient = the invitee).
// · AddCommunityMemberAsync on a supervised child → a pending
//   CommunityMembershipRequest row (NO ComponentMembership row) + one
//   guardian notification (kind guardian.community_invite) + the
//   community.membership.request audit row.
// · SetCommunityMembershipAsync on a supervised child → the same pending
//   shape (the admin lane's supervised branch).
// · ApproveCommunityMembershipRequestAsync → the ComponentMembership row
//   lands (GetCommunityIdsAsync includes it — invariant C4), the request
//   row is Approved, audit community.membership.approve Via: Guardian;
//   non-guardian refused (UnauthorizedAccessException).
// · DeclineCommunityMembershipRequestAsync → no ComponentMembership row,
//   the request row is Declined.
// · RejectGroupInvitationAsync → the invitation row is Declined, no
//   GroupMembership row; non-guardian refused.
// · AcceptGroupInvitationAsync on a supervised child → refused (the
//   pre-existing GU gate: the guardian must resolve instead).
//
// Harness: the single-store shape (M1DocTypes + M6DocTypes), the real
// NotificationService over a recording mailer + a Substitute
// ITranslationProvider (the UserInfoServiceGroupNotificationTests
// precedent), and a UserInfoService wired with an IServiceProvider that
// resolves the same NotificationService (the circular-dependency
// avoidance).
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

public sealed class GuardianApprovalLaneTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    private const string GroupId = "grp-0141";
    private const string GroupName = "Garden Club";
    private const string OwnerId = "subj-owner-0141";
    private const string ComponentId = "safety";
    private const string ComponentName = "Safety";
    private const string ChildId = "subj-child-0141";
    private const string GuardianId = "subj-guardian-0141";
    private const string SecondGuardianId = "subj-guardian2-0141";
    private const string StrangerId = "subj-stranger-0141";
    private const string AdminId = "subj-admin-0141";

    // ── InviteGroupMemberAsync — supervised child: guardian fan-out ────────

    [Fact]
    public async Task InviteSupervisedChild_NotifiesGuardian_NotChild_AndKeepsInvitationPending()
    {
        var boot = await BootAsync();

        await boot.UserInfo.InviteGroupMemberAsync(GroupId, ChildId, OwnerId);

        // The invitation row is Pending (the child's self-lane refuse gate
        // still applies — the membership has NOT landed).
        var invitation = await boot.UserInfo.GetPendingInvitationsForUserAsync(ChildId);
        Assert.Single(invitation);

        // The membership has NOT landed (the child is not yet a member).
        Assert.DoesNotContain(GroupId, await boot.UserInfo.GetGroupIdsAsync(ChildId));

        // The GUARDIAN got the notification (kind guardian.group_invite,
        // LinkPath / AcceptPath / DeclinePath = /me/children/{childId}).
        var rows = await NotificationsFor(boot.Store, GuardianId, NotificationKinds.GuardianGroupInvite);
        var row = Assert.Single(rows);
        Assert.Equal(NotificationKinds.GuardianGroupInvite, row.Kind);
        Assert.Equal($"/me/children/{ChildId}", row.LinkPath);
        Assert.Equal($"/me/children/{ChildId}", row.AcceptPath);
        Assert.Equal($"/me/children/{ChildId}", row.DeclinePath);
        Assert.Contains(GroupName, row.Body);
        Assert.Contains("Child One", row.Body);

        // The CHILD got nothing (the recipient is the guardian).
        Assert.Empty(await NotificationsFor(boot.Store, ChildId, null));
    }

    [Fact]
    public async Task InviteSupervisedChild_TwoActiveGuardians_EachGetsOneRow()
    {
        var boot = await BootAsync();

        await boot.UserInfo.CreateGuardianLinkAsync(ChildId, SecondGuardianId);
        await boot.UserInfo.InviteGroupMemberAsync(GroupId, ChildId, OwnerId);

        var g1 = await NotificationsFor(boot.Store, GuardianId, NotificationKinds.GuardianGroupInvite);
        var g2 = await NotificationsFor(boot.Store, SecondGuardianId, NotificationKinds.GuardianGroupInvite);
        Assert.Single(g1);
        Assert.Single(g2);
        // Different idempotency keys (the per-guardian suffix) — the two
        // rows coexist.
        Assert.NotEqual(g1[0].IdempotencyKey, g2[0].IdempotencyKey);
    }

    [Fact]
    public async Task InviteUnsupervisedResident_EmitsChildFacingGroupInvite_Uncchanged()
    {
        var boot = await BootAsync();

        await boot.UserInfo.InviteGroupMemberAsync(GroupId, StrangerId, OwnerId);

        var rows = await NotificationsFor(boot.Store, StrangerId, NotificationKinds.GroupInvite);
        Assert.Single(rows);
        Assert.Equal($"/groups/{GroupId}/invitations/accept", rows[0].AcceptPath);
        Assert.Equal($"/groups/{GroupId}/invitations/decline", rows[0].DeclinePath);
        // The ADR 0083 shape: the invitee's self-lane GET links.
        Assert.Contains(GroupName, rows[0].Body);
    }

    // ── ApproveGroupInvitationAsync (pre-existing) still lands the
    //    membership; the GUARDIAN is recorded as ResolvedBy. ────────────────

    [Fact]
    public async Task GuardianApprovesGroupInvitation_MembershipLands_ResolvedByGuardian()
    {
        var boot = await BootAsync();
        await boot.UserInfo.InviteGroupMemberAsync(GroupId, ChildId, OwnerId);

        await boot.UserInfo.ApproveGroupInvitationAsync(GroupId, ChildId, GuardianId);

        Assert.Contains(GroupId, await boot.UserInfo.GetGroupIdsAsync(ChildId));

        // The row is no longer pending (the self-lane read is empty).
        Assert.Empty(await boot.UserInfo.GetPendingInvitationsForUserAsync(ChildId));

        // The group.invite.approve audit row (Via: Guardian) committed.
        var audits = await AuditsFor(boot.Store, "group.invite.approve");
        Assert.Single(audits);
        Assert.Equal(Authorization.AccessVia.Guardian, audits[0].Via);
        Assert.Equal(GuardianId, audits[0].ActorId);
    }

    // ── RejectGroupInvitationAsync — the new guardian decline lane ─────────

    [Fact]
    public async Task GuardianRejectsGroupInvitation_RowDeclined_NoMembership()
    {
        var boot = await BootAsync();
        await boot.UserInfo.InviteGroupMemberAsync(GroupId, ChildId, OwnerId);

        await boot.UserInfo.RejectGroupInvitationAsync(GroupId, ChildId, GuardianId);

        // No membership landed.
        Assert.DoesNotContain(GroupId, await boot.UserInfo.GetGroupIdsAsync(ChildId));
        // The row is resolved (no longer pending).
        Assert.Empty(await boot.UserInfo.GetPendingInvitationsForUserAsync(ChildId));

        var audits = await AuditsFor(boot.Store, "group.invite.reject");
        Assert.Single(audits);
        Assert.Equal(Authorization.AccessVia.Guardian, audits[0].Via);
    }

    [Fact]
    public async Task RejectGroupInvitationByNonGuardian_Refused()
    {
        var boot = await BootAsync();
        await boot.UserInfo.InviteGroupMemberAsync(GroupId, ChildId, OwnerId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.UserInfo.RejectGroupInvitationAsync(GroupId, ChildId, StrangerId));
    }

    [Fact]
    public async Task ChildSelfAccept_Refused_WhileSupervised()
    {
        var boot = await BootAsync();
        await boot.UserInfo.InviteGroupMemberAsync(GroupId, ChildId, OwnerId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.UserInfo.AcceptGroupInvitationAsync(GroupId, ChildId));
    }

    // ── AddCommunityMemberAsync — supervised child: pending request ────────

    [Fact]
    public async Task AdminAddsSupervisedChildToCommunity_PendingRequestNotMembership()
    {
        var boot = await BootAsync();
        var roles = new HashSet<string> { Identity.Roles.GlobalAdmin };

        await boot.UserInfo.AddCommunityMemberAsync(ComponentId, ChildId, AdminId, roles);

        // The membership has NOT landed (no explicit row — GetCommunityIds
        // reads the union; for a non-mandatory component a missing row means
        // the child is not a member).
        Assert.DoesNotContain(ComponentId, await boot.UserInfo.GetCommunityIdsAsync(ChildId));

        // A PENDING request row exists (the guardian's curation list).
        var pending = await boot.UserInfo
            .GetPendingCommunityMembershipRequestsForChildAsync(ChildId);
        Assert.Single(pending);
        Assert.Equal(AdminId, pending[0].RequestedBy);

        // The community.membership.request audit row committed.
        Assert.Single(await AuditsFor(boot.Store, "community.membership.request"));

        // The GUARDIAN got the notification (kind
        // guardian.community_invite; the manage-child deep-link).
        var rows = await NotificationsFor(boot.Store, GuardianId,
            NotificationKinds.GuardianCommunityInvite);
        var row = Assert.Single(rows);
        Assert.Equal($"/me/children/{ChildId}", row.LinkPath);
        Assert.Contains(ComponentName, row.Body);
        Assert.Contains("Child One", row.Body);

        // The child got nothing.
        Assert.Empty(await NotificationsFor(boot.Store, ChildId, null));
    }

    [Fact]
    public async Task SetCommunityMembershipForSupervisedChild_SamePendingShape()
    {
        var boot = await BootAsync();

        await boot.UserInfo.SetCommunityMembershipAsync(ComponentId, ChildId, AdminId);

        Assert.DoesNotContain(ComponentId, await boot.UserInfo.GetCommunityIdsAsync(ChildId));
        Assert.Single(await boot.UserInfo
            .GetPendingCommunityMembershipRequestsForChildAsync(ChildId));
        Assert.Single(await AuditsFor(boot.Store, "community.membership.request"));
        Assert.Single(await NotificationsFor(boot.Store, GuardianId,
            NotificationKinds.GuardianCommunityInvite));
    }

    [Fact]
    public async Task AdminAddsUnsupervisedResidentToCommunity_MembershipLands_Uncchanged()
    {
        var boot = await BootAsync();

        await boot.UserInfo.SetCommunityMembershipAsync(ComponentId, StrangerId, AdminId);

        // The ADR 0012 admin lane: the membership lands immediately.
        Assert.Contains(ComponentId, await boot.UserInfo.GetCommunityIdsAsync(StrangerId));
        // No pending request row for an unsupervised resident.
        Assert.Empty(await boot.UserInfo
            .GetPendingCommunityMembershipRequestsForChildAsync(StrangerId));
        Assert.Single(await AuditsFor(boot.Store, "community.add-member"));
    }

    // ── ApproveCommunityMembershipRequestAsync — the approve write ─────────

    [Fact]
    public async Task GuardianApprovesCommunityRequest_MembershipLands_AuditGuardian()
    {
        var boot = await BootAsync();
        var roles = new HashSet<string> { Identity.Roles.GlobalAdmin };
        await boot.UserInfo.AddCommunityMemberAsync(ComponentId, ChildId, AdminId, roles);

        await boot.UserInfo.ApproveCommunityMembershipRequestAsync(ComponentId, ChildId, GuardianId);

        // C4 — the membership is live on the very next read.
        Assert.Contains(ComponentId, await boot.UserInfo.GetCommunityIdsAsync(ChildId));

        // The request row is resolved (no longer pending).
        Assert.Empty(await boot.UserInfo
            .GetPendingCommunityMembershipRequestsForChildAsync(ChildId));

        var audits = await AuditsFor(boot.Store, "community.membership.approve");
        Assert.Single(audits);
        Assert.Equal(Authorization.AccessVia.Guardian, audits[0].Via);
        Assert.Equal(GuardianId, audits[0].ActorId);
    }

    [Fact]
    public async Task ApproveCommunityRequestByNonGuardian_Refused()
    {
        var boot = await BootAsync();
        var roles = new HashSet<string> { Identity.Roles.GlobalAdmin };
        await boot.UserInfo.AddCommunityMemberAsync(ComponentId, ChildId, AdminId, roles);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.UserInfo.ApproveCommunityMembershipRequestAsync(ComponentId, ChildId, StrangerId));
    }

    // ── DeclineCommunityMembershipRequestAsync — the decline write ─────────

    [Fact]
    public async Task GuardianDeclinesCommunityRequest_NoMembership_RowDeclined()
    {
        var boot = await BootAsync();
        var roles = new HashSet<string> { Identity.Roles.GlobalAdmin };
        await boot.UserInfo.AddCommunityMemberAsync(ComponentId, ChildId, AdminId, roles);

        await boot.UserInfo.DeclineCommunityMembershipRequestAsync(ComponentId, ChildId, GuardianId);

        Assert.DoesNotContain(ComponentId, await boot.UserInfo.GetCommunityIdsAsync(ChildId));
        Assert.Empty(await boot.UserInfo
            .GetPendingCommunityMembershipRequestsForChildAsync(ChildId));

        var audits = await AuditsFor(boot.Store, "community.membership.decline");
        Assert.Single(audits);
        Assert.Equal(Authorization.AccessVia.Guardian, audits[0].Via);
    }

    [Fact]
    public async Task DeclineCommunityRequestByNonGuardian_Refused()
    {
        var boot = await BootAsync();
        var roles = new HashSet<string> { Identity.Roles.GlobalAdmin };
        await boot.UserInfo.AddCommunityMemberAsync(ComponentId, ChildId, AdminId, roles);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.UserInfo.DeclineCommunityMembershipRequestAsync(ComponentId, ChildId, StrangerId));
    }

    // ── Re-request (the m2b C-M2b·3 reset shape) ───────────────────────────

    [Fact]
    public async Task ReAddSupervisedChildAfterDecline_ResetsRowToPending()
    {
        var boot = await BootAsync();
        var roles = new HashSet<string> { Identity.Roles.GlobalAdmin };
        await boot.UserInfo.AddCommunityMemberAsync(ComponentId, ChildId, AdminId, roles);
        await boot.UserInfo.DeclineCommunityMembershipRequestAsync(ComponentId, ChildId, GuardianId);

        // Re-add → the row resets to Pending (no second row — the unique
        // (ComponentId, UserId) index).
        await boot.UserInfo.AddCommunityMemberAsync(ComponentId, ChildId, AdminId, roles);
        var pending = await boot.UserInfo
            .GetPendingCommunityMembershipRequestsForChildAsync(ChildId);
        var row = Assert.Single(pending);
        Assert.Equal(CommunityMembershipRequestStatus.Pending, row.Status);
        Assert.Null(row.ResolvedBy);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed class Boot
    {
        public required IDocumentStore Store { get; init; }
        public required UserInfoService UserInfo { get; init; }
    }

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

        await using (var s = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            s.Store(new Group
            {
                Id = GroupId,
                Name = GroupName,
                OwnerId = OwnerId,
                Created = DateTimeOffset.UtcNow,
            });
            s.Store(new Component { Id = ComponentId, Name = ComponentName, Enabled = true });
            s.Store(new Profile
            {
                SubjectId = ChildId,
                DisplayName = "Child One",
                Email = ChildId + "@ex.net",
                Verified = true,
            });
            s.Store(new Profile
            {
                SubjectId = GuardianId,
                DisplayName = "Guardian",
                Email = GuardianId + "@ex.net",
                Verified = true,
            });
            s.Store(new Profile
            {
                SubjectId = SecondGuardianId,
                DisplayName = "Second Guardian",
                Email = SecondGuardianId + "@ex.net",
                Verified = true,
            });
            s.Store(new Profile
            {
                SubjectId = StrangerId,
                DisplayName = "Stranger",
                Email = StrangerId + "@ex.net",
                Verified = true,
            });
            s.Store(new Profile
            {
                SubjectId = AdminId,
                DisplayName = "Admin",
                Email = AdminId + "@ex.net",
                Verified = true,
            });
            s.Store(new Profile
            {
                SubjectId = OwnerId,
                DisplayName = "Owner",
                Email = OwnerId + "@ex.net",
                Verified = true,
            });
            await s.SaveChangesAsync(ct);
        }

        // The GU formation — the (guardian, child) active link.
        var userInfoForLink = new UserInfoService(store);
        await userInfoForLink.CreateGuardianLinkAsync(ChildId, GuardianId);

        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => Task.FromResult((string)ci[0]));

        var mailer = Substitute.For<IMailerStage>();
        mailer.StageAsync(
                Arg.Any<IDocumentSession>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var userInfoForNs = new UserInfoService(store);
        var ns = new NotificationService(store, userInfoForNs, translator, mailer, null, null);

        var sc = new ServiceCollection();
        sc.AddSingleton(ns);
        var sp = sc.BuildServiceProvider();

        var userInfo = new UserInfoService(store, sp);

        return new Boot { Store = store, UserInfo = userInfo };
    }

    private static async Task<IReadOnlyList<Notification>> NotificationsFor(
        IDocumentStore store, string recipientId, string? kind)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<Notification>().Where(n => n.RecipientId == recipientId);
        if (kind is not null)
            q = q.Where(n => n.Kind == kind);
        return await q.ToListAsync(ct);
    }

    private static async Task<IReadOnlyList<Authorization.AccessAudit>> AuditsFor(
        IDocumentStore store, string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<Authorization.AccessAudit>()
            .Where(a => a.Action == action)
            .ToListAsync(ct);
    }
}
