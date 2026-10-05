// The guardian's event-attendance gate (ADR 0144) — the supervised-child
// branch of the event RSVP self-lane + the guardian resolve lanes:
//
// · RsvpAsync by a supervised child under the DEFAULT
//   Profile.EventRsvpMode (GuardianApproves) → refused
//   (InvalidOperationException — the Web's user-presentable
//   TempData["error"] shape): NO EventRsvp row, a Pending
//   GuardianEventRequest row is stored (the child's desired status), and
//   one guardian.event_request notification per ACTIVE guardian (recipient
//   = the guardian, LinkPath = /me/children/{childId}).
// · RsvpAsync by an UNSUPERVISED resident → the ADR 0054 shape unchanged
//   (the RSVP row lands, no request row, no guardian notification).
// · RsvpAsync under GuardianNotifies → the RSVP row lands (auto-approve)
//   + one guardian.event_rsvp notification per active guardian (the window
//   to veto).
// · RsvpAsync under ChildDecides → the RSVP row lands, no guardian
//   notification at all.
// · ApproveEventRsvpAsync → the child's EventRsvp row is written with the
//   request's DesiredStatus, the request row is Approved (ResolvedBy =
//   guardian), one guardian.event_rsvp_approve audit row Via: Guardian.
// · DenyEventRsvpAsync → NO EventRsvp row, the request row is Denied, one
//   guardian.event_rsvp_deny audit row Via: Guardian.
// · VetoEventRsvpAsync → the child's existing EventRsvp row is deleted, one
//   guardian.event_rsvp_veto audit row Via: Guardian.
// · A non-guardian on any resolve lane → UnauthorizedAccessException (the
//   Web's non-leaky 404); re-approving / re-denying a resolved request →
//   InvalidOperationException.
// · SetChildEventRsvpModeAsync → the profile's EventRsvpMode field is
//   written + one guardian.event_rsvp_mode audit row Via: Guardian; a
//   non-guardian → UnauthorizedAccessException.
//
// Harness: the EventServiceTests store shape (M1DocTypes + M3DocTypes +
// M4DocTypes, fresh scratch Postgres per test) + the
// GuardianApprovalLaneTests NotificationService wiring (real
// NotificationService over a recording mailer + a Substitute
// ITranslationProvider) — the GU lane's circular-dependency-avoidance
// pattern (the UserInfoService is wired with an IServiceProvider that
// resolves the same NotificationService).

using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

public sealed class GuardianEventRsvpGateTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    private const string EventId = "ev-0144";
    private const string AuthorId = "subj-author-0144";
    private const string ChildId = "subj-child-0144";
    private const string GuardianId = "subj-guardian-0144";
    private const string SecondGuardianId = "subj-guardian2-0144";
    private const string StrangerId = "subj-stranger-0144";

    // ── The gate — supervised child, default posture (GuardianApproves) ───

    [Fact]
    public async Task SupervisedChildDefaultMode_RsvpRefused_RequestStored_GuardianNotified()
    {
        var boot = await BootAsync();

        // The self-lane is refused with the user-presentable error...
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Events.RsvpAsync(EventId, ChildId, RsvpStatus.Going));

        // ...NO RSVP row landed (the child is not yet attending)...
        Assert.Empty(await RsvpsFor(boot.Store, ChildId));

        // ...but a PENDING request row was stored (the child's desired
        // status — the GU group-invitation request-row shape).
        var requests = await RequestsFor(boot.Store, EventId, ChildId);
        var request = Assert.Single(requests);
        Assert.Equal(GuardianEventRequestStatus.Pending, request.Status);
        Assert.Equal(RsvpStatus.Going, request.DesiredStatus);
        Assert.Null(request.ResolvedBy);

        // One guardian.event_request row per active guardian (recipient =
        // the guardian, LinkPath = the manage-child page — the GU
        // guardian.group_invite fan-out shape).
        var rows = await NotificationsFor(boot.Store, GuardianId,
            NotificationKinds.GuardianEventRequest);
        var row = Assert.Single(rows);
        Assert.Equal(GuardianId, row.RecipientId);
        Assert.Equal($"/me/children/{ChildId}", row.LinkPath);
        Assert.Contains("Child One", row.Body);

        // The CHILD got nothing (the recipient is the guardian).
        Assert.Empty(await NotificationsFor(boot.Store, ChildId, null));
    }

    [Fact]
    public async Task SupervisedChild_TwoActiveGuardians_EachGetsOneRequestNotification()
    {
        var boot = await BootAsync();
        await boot.UserInfo.CreateGuardianLinkAsync(ChildId, SecondGuardianId);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Events.RsvpAsync(EventId, ChildId, RsvpStatus.Going));

        var g1 = await NotificationsFor(boot.Store, GuardianId,
            NotificationKinds.GuardianEventRequest);
        var g2 = await NotificationsFor(boot.Store, SecondGuardianId,
            NotificationKinds.GuardianEventRequest);
        Assert.Single(g1);
        Assert.Single(g2);
        // Different idempotency keys (the per-guardian suffix) — the two
        // rows coexist.
        Assert.NotEqual(g1[0].IdempotencyKey, g2[0].IdempotencyKey);
    }

    [Fact]
    public async Task SupervisedChildReRequest_SameRequestRow_RefreshedToPending()
    {
        var boot = await BootAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Events.RsvpAsync(EventId, ChildId, RsvpStatus.Going));
        await boot.Events.DenyEventRsvpAsync(EventId, ChildId, GuardianId);

        // A fresh act by the child supersedes the resolved row — the
        // (EventId, ChildId) unique index keeps ONE row; it flips back to
        // Pending with the child's new desired status.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Events.RsvpAsync(EventId, ChildId, RsvpStatus.Maybe));

        var rows = await RequestsFor(boot.Store, EventId, ChildId);
        var row = Assert.Single(rows);
        Assert.Equal(GuardianEventRequestStatus.Pending, row.Status);
        Assert.Equal(RsvpStatus.Maybe, row.DesiredStatus);
        Assert.Null(row.ResolvedBy);
    }

    // ── The gate — the other two postures ───────────────────────────────────

    [Fact]
    public async Task SupervisedChild_NotifiesPosture_RsvpLands_GuardianNotified()
    {
        var boot = await BootAsync();
        await boot.UserInfo.SetChildEventRsvpModeAsync(
            ChildId, EventRsvpMode.GuardianNotifies, GuardianId);

        // The self-lane is allowed (auto-approve)...
        var rsvp = await boot.Events.RsvpAsync(EventId, ChildId, RsvpStatus.Going);
        Assert.Equal(RsvpStatus.Going, rsvp.Status);

        // ...and the guardian got the veto-window notification (kind
        // guardian.event_rsvp, LinkPath = the event detail page).
        var rows = await NotificationsFor(boot.Store, GuardianId,
            NotificationKinds.GuardianEventRsvp);
        var row = Assert.Single(rows);
        Assert.Equal($"/events/{EventId}", row.LinkPath);

        // No request row (there is nothing to approve — the attendance
        // already landed).
        Assert.Empty(await RequestsFor(boot.Store, EventId, ChildId));
    }

    [Fact]
    public async Task SupervisedChild_ChildDecidesPosture_RsvpLands_NoGuardianNotification()
    {
        var boot = await BootAsync();
        await boot.UserInfo.SetChildEventRsvpModeAsync(
            ChildId, EventRsvpMode.ChildDecides, GuardianId);

        var rsvp = await boot.Events.RsvpAsync(EventId, ChildId, RsvpStatus.Going);
        Assert.Equal(RsvpStatus.Going, rsvp.Status);

        // The child decided for themselves — the guardian got NOTHING.
        Assert.Empty(await NotificationsFor(boot.Store, GuardianId, null));
        Assert.Empty(await RequestsFor(boot.Store, EventId, ChildId));
    }

    // ── The gate — unsupervised actors are never gated ──────────────────────

    [Fact]
    public async Task UnsupervisedResident_RsvpLands_Unchanged()
    {
        var boot = await BootAsync();

        // A stranger (no active GuardianLink over them) is never gated
        // regardless of posture — the ADR 0054 self-service shape.
        var rsvp = await boot.Events.RsvpAsync(EventId, StrangerId, RsvpStatus.Going);
        Assert.Equal(RsvpStatus.Going, rsvp.Status);
        Assert.Empty(await RequestsFor(boot.Store, EventId, StrangerId));
        Assert.Empty(await NotificationsFor(boot.Store, GuardianId, null));
    }

    // ── The guardian resolve lanes ───────────────────────────────────────────

    [Fact]
    public async Task GuardianApproves_ChildRsvpLands_RequestApproved_AuditRow()
    {
        var boot = await BootAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Events.RsvpAsync(EventId, ChildId, RsvpStatus.Going));

        var request = await boot.Events.ApproveEventRsvpAsync(EventId, ChildId, GuardianId);
        Assert.Equal(GuardianEventRequestStatus.Approved, request.Status);
        Assert.Equal(GuardianId, request.ResolvedBy);
        Assert.NotNull(request.ResolvedAt);

        // The child's RSVP row was written with the request's DesiredStatus.
        var rsvps = await RsvpsFor(boot.Store, ChildId);
        var rsvp = Assert.Single(rsvps);
        Assert.Equal(EventId, rsvp.EventId);
        Assert.Equal(RsvpStatus.Going, rsvp.Status);

        // The guardian.event_rsvp_approve audit row (Via: Guardian).
        var audits = await AuditsFor(boot.Store, "guardian.event_rsvp_approve");
        Assert.Single(audits);
        Assert.Equal(AccessVia.Guardian, audits[0].Via);
        Assert.Equal(GuardianId, audits[0].ActorId);
    }

    [Fact]
    public async Task GuardianDenies_NoRsvpRow_RequestDenied_AuditRow()
    {
        var boot = await BootAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Events.RsvpAsync(EventId, ChildId, RsvpStatus.Going));

        var request = await boot.Events.DenyEventRsvpAsync(EventId, ChildId, GuardianId);
        Assert.Equal(GuardianEventRequestStatus.Denied, request.Status);
        Assert.Equal(GuardianId, request.ResolvedBy);

        // A denial writes NO RSVP row (the child did not attend).
        Assert.Empty(await RsvpsFor(boot.Store, ChildId));

        var audits = await AuditsFor(boot.Store, "guardian.event_rsvp_deny");
        Assert.Single(audits);
        Assert.Equal(AccessVia.Guardian, audits[0].Via);
    }

    [Fact]
    public async Task GuardianVeto_ChildRsvpDeleted_AuditRow()
    {
        var boot = await BootAsync();
        await boot.UserInfo.SetChildEventRsvpModeAsync(
            ChildId, EventRsvpMode.GuardianNotifies, GuardianId);
        await boot.Events.RsvpAsync(EventId, ChildId, RsvpStatus.Going);
        var rsvps = await RsvpsFor(boot.Store, ChildId);
        Assert.Single(rsvps);

        // The veto (the GuardianNotifies posture's window to undo).
        await boot.Events.VetoEventRsvpAsync(EventId, ChildId, GuardianId);

        Assert.Empty(await RsvpsFor(boot.Store, ChildId));

        var audits = await AuditsFor(boot.Store, "guardian.event_rsvp_veto");
        Assert.Single(audits);
        Assert.Equal(AccessVia.Guardian, audits[0].Via);
    }

    [Fact]
    public async Task GuardianVeto_NoRsvpRow_Refused()
    {
        var boot = await BootAsync();

        // A veto over nothing is a user-presentable 404.
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => boot.Events.VetoEventRsvpAsync(EventId, ChildId, GuardianId));
    }

    // ── Standing (G·2/G·3) + state-machine pins ─────────────────────────────

    [Fact]
    public async Task NonGuardian_ApproveDenyVetoAllRefused()
    {
        var boot = await BootAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Events.RsvpAsync(EventId, ChildId, RsvpStatus.Going));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.Events.ApproveEventRsvpAsync(EventId, ChildId, StrangerId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.Events.DenyEventRsvpAsync(EventId, ChildId, StrangerId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.Events.VetoEventRsvpAsync(EventId, ChildId, StrangerId));
    }

    [Fact]
    public async Task ApproveAlreadyResolvedRequest_Refused()
    {
        var boot = await BootAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Events.RsvpAsync(EventId, ChildId, RsvpStatus.Going));
        await boot.Events.DenyEventRsvpAsync(EventId, ChildId, GuardianId);

        // A resolved request cannot be re-approved — the child's fresh act
        // (a new request row) is the only path.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => boot.Events.ApproveEventRsvpAsync(EventId, ChildId, GuardianId));
    }

    [Fact]
    public async Task ApproveWithoutRequest_Refused()
    {
        var boot = await BootAsync();

        // Nothing to approve (the child never asked).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => boot.Events.ApproveEventRsvpAsync(EventId, ChildId, GuardianId));
    }

    // ── The posture write lane (SetChildEventRsvpModeAsync) ─────────────────

    [Fact]
    public async Task SetChildEventRsvpMode_WritesProfileMode_AuditRow()
    {
        var boot = await BootAsync();

        await boot.UserInfo.SetChildEventRsvpModeAsync(
            ChildId, EventRsvpMode.GuardianNotifies, GuardianId);

        await using (var s = boot.Store.QuerySession())
        {
            var profile = await s.Query<Profile>()
                .Where(p => p.SubjectId == ChildId).SingleAsync();
            Assert.Equal(EventRsvpMode.GuardianNotifies, profile.EventRsvpMode);
        }

        var audits = await AuditsFor(boot.Store, "guardian.event_rsvp_mode");
        Assert.Single(audits);
        Assert.Equal(AccessVia.Guardian, audits[0].Via);
        Assert.Equal(GuardianId, audits[0].ActorId);
    }

    [Fact]
    public async Task SetChildEventRsvpModeByNonGuardian_Refused()
    {
        var boot = await BootAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => boot.UserInfo.SetChildEventRsvpModeAsync(
                ChildId, EventRsvpMode.ChildDecides, StrangerId));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed class Boot
    {
        public required IDocumentStore Store { get; init; }
        public required UserInfoService UserInfo { get; init; }
        public required EventService Events { get; init; }
    }

    private async Task<Boot> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        // The EventServiceTests store shape (the Event / EventRsvp /
        // GuardianEventRequest surface = M4DocTypes) + the GU lane's
        // Notification surface (M6DocTypes).
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M4DocTypes.Configure(opts);
            M6DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        await using (var s = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            s.Store(new Event
            {
                Id = EventId, AuthorId = AuthorId,
                Title = "Cleanup day", Body = "body 0144",
                Start = DateTimeOffset.UtcNow.AddDays(30),
                End = DateTimeOffset.UtcNow.AddDays(30).AddHours(2),
                IsDraft = false, IsDeleted = false,
            });
            s.Store(new Profile
            {
                SubjectId = ChildId, DisplayName = "Child One",
                Email = ChildId + "@ex.net", Verified = true,
            });
            s.Store(new Profile
            {
                SubjectId = GuardianId, DisplayName = "Guardian",
                Email = GuardianId + "@ex.net", Verified = true,
            });
            s.Store(new Profile
            {
                SubjectId = SecondGuardianId, DisplayName = "Second Guardian",
                Email = SecondGuardianId + "@ex.net", Verified = true,
            });
            s.Store(new Profile
            {
                SubjectId = StrangerId, DisplayName = "Stranger",
                Email = StrangerId + "@ex.net", Verified = true,
            });
            s.Store(new Profile
            {
                SubjectId = AuthorId, DisplayName = "Author",
                Email = AuthorId + "@ex.net", Verified = true,
            });
            await s.SaveChangesAsync(ct);
        }

        // The GU formation — the (guardian, child) active link.
        var userInfoForLink = new UserInfoService(store);
        await userInfoForLink.CreateGuardianLinkAsync(ChildId, GuardianId);

        // The GU lane's NotificationService wiring (real service over a
        // recording mailer + a Substitute ITranslationProvider; the
        // circular-dependency avoidance).
        var translator = Substitute.For<ITranslationProvider>();
        translator.GetAsync(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => Task.FromResult((string)ci[0]));

        var mailer = Substitute.For<IMailerStage>();
        mailer.StageAsync(
                Arg.Any<IDocumentSession>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var ns = new NotificationService(
            store, new UserInfoService(store), translator, mailer, null, null);

        var sc = new ServiceCollection();
        sc.AddSingleton(ns);
        var sp = sc.BuildServiceProvider();

        var userInfo = new UserInfoService(store, sp);
        var events = new EventService(
            store, new AuthorizationService(store, userInfo), userInfo, ns);

        return new Boot { Store = store, UserInfo = userInfo, Events = events };
    }

    private static async Task<IReadOnlyList<GuardianEventRequest>> RequestsFor(
        IDocumentStore store, string eventId, string childId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<GuardianEventRequest>()
            .Where(r => r.EventId == eventId && r.ChildId == childId)
            .ToListAsync(ct);
    }

    private static async Task<IReadOnlyList<EventRsvp>> RsvpsFor(
        IDocumentStore store, string userId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await s.Query<EventRsvp>()
            .Where(r => r.UserId == userId)
            .ToListAsync(ct);
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
