using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The guardian's event-attendance gate (ADR 0144) — the
/// <see cref="GuardianController"/> POST-lane integration tests (the
/// /me/children/{childId}/eventrsvp surface, the guardian's
/// event-attendance policy + resolve lanes). The pins:
/// <list type="number">
/// <item><b>Standing gate first.</b> <see cref
/// "GuardianController.SetEventRsvpMode"/> by a non-guardian (no active
///       <see cref="GuardianLink"/> over the child) is a
///       <see cref="NotFoundResult"/> — the ADR 0028 deny-by-default shape,
///       the <see cref="GuardianMessagingRestrictionTests"/> idiom verbatim.
///       The mode write is never reached.</item>
/// <item><b>Happy path.</b> A guardian with an active link POSTs the mode:
///       the action calls
///       <see cref="IUserInfoService.SetChildEventRsvpModeAsync"/> with the
///       (childId, mode, guardianId) tuple, the mode is live on the very next
///       <see cref="IUserInfoService.GetProfileAsync"/> read (C4 strong
///       consistency), a <c>guardian.event_rsvp_mode</c> audit row with
///       <c>Via: Guardian</c> is committed, and the action redirects back to
///       <c>Detail</c>.</item>
/// <item><b>Resolve lanes.</b> <see cref
/// "GuardianController.ApproveEventRsvp"/> / <see cref
/// "GuardianController.DenyEventRsvp"/> / <see cref
/// "GuardianController.VetoEventRsvp"/> delegate to the
/// <see cref="IEventService"/> seams (a non-guardian is a 404 — the Core
/// standing gate's <see cref="UnauthorizedAccessException"/> shape; the
/// approve lane writes the child's RSVP row + the
/// <c>guardian.event_rsvp_approve</c> audit row).</item>
/// <item><b>Null-safe.</b> A controller built without an
/// <see cref="IEventService"/> (the nullable ctor param — the five existing
/// test constructions) 404s the resolve lanes instead of throwing.</item>
/// </list>
/// Integration tests (not NSubstitute-only) — the same harness shape as
/// <see cref="GuardianMessagingRestrictionTests"/> (the real Marten store,
/// the real <see cref="UserInfoService"/>, the real
/// <c>ActiveLinkAsync</c> standing gate) — the mode write is the
/// <see cref="IUserInfoService.SetChildEventRsvpModeAsync"/> seam, and the
/// <c>guardian.event_rsvp_mode</c> audit row is only produced by the real
/// store.
/// </summary>
public sealed class GuardianEventRsvpGateTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Guardian = "ge-guardian-0144";
    private const string Child = "ge-child-0144";
    private const string NonGuardian = "ge-non-guardian-0144";
    private const string EventId = "ge-event-0144";

    // ── 1 — standing gate: a non-guardian is a 404, no write ─────────────

    [Fact]
    public async Task SetEventRsvpMode_NonGuardian_Returns404_NoWrite()
    {
        var (controller, store) = await BuildAsync(actorSubjectId: NonGuardian);

        var result = await controller.SetEventRsvpMode(Child, EventRsvpMode.GuardianNotifies);

        Assert.IsType<NotFoundResult>(result);

        // The mode is untouched (the standing gate fired before the write;
        // the ADR 0028 deny-by-default shape).
        var profile = await (new UserInfoService(store)).GetProfileAsync(Child);
        Assert.Equal(EventRsvpMode.GuardianApproves, profile?.EventRsvpMode);

        // No audit row (the refuse fired before the write).
        Assert.Equal(0, await CountAuditAsync(store, "guardian.event_rsvp_mode", Child));
    }

    // ── 2 — happy path: the mode is written, audited, live on next read ──

    [Theory]
    [InlineData(EventRsvpMode.GuardianApproves)]
    [InlineData(EventRsvpMode.GuardianNotifies)]
    [InlineData(EventRsvpMode.ChildDecides)]
    public async Task SetEventRsvpMode_Guardian_ModeLive_AuditedViaGuardian(EventRsvpMode mode)
    {
        var (controller, store) = await BuildAsync(actorSubjectId: Guardian);
        var userInfo = new UserInfoService(store);

        var result = await controller.SetEventRsvpMode(Child, mode);
        var redirect = Assert.IsType<RedirectToActionResult>(result);

        // The redirect goes back to the Detail page (the standard
        // "save + back to curation" shape, the SetChildMessaging idiom).
        Assert.Equal(nameof(GuardianController.Detail), redirect.ActionName);
        Assert.Equal(Child, redirect.RouteValues["childId"]);

        // The mode is live on the very next read (C4).
        Assert.Equal(mode, (await userInfo.GetProfileAsync(Child))!.EventRsvpMode);

        // The audit row (guardian.event_rsvp_mode, Via: Guardian, the GU
        // G·2 pin carried to the event-attendance lane).
        var row = await LastAuditAsync(store, "guardian.event_rsvp_mode", Child);
        Assert.Equal(AccessVia.Guardian, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(Guardian, row.ActorId);
    }

    // ── 3 — resolve lanes: delegate to the IEventService seams ────────────

    [Fact]
    public async Task ApproveEventRsvp_Guardian_WritesChildRsvp_AuditedViaGuardian()
    {
        var (controller, store) = await BuildAsync(actorSubjectId: Guardian);

        // The child asked to attend (the self-lane refusal stored the
        // Pending request row + left no RSVP row).
        var events = EventsService(store);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => events.RsvpAsync(EventId, Child, RsvpStatus.Going));

        var result = await controller.ApproveEventRsvp(Child, EventId);
        Assert.IsType<RedirectToActionResult>(result);

        // The child's RSVP row landed with the request's desired status.
        await using (var session = store.QuerySession())
        {
            var rsvp = await session.Query<EventRsvp>()
                .Where(r => r.EventId == EventId && r.UserId == Child)
                .SingleAsync();
            Assert.Equal(RsvpStatus.Going, rsvp.Status);
        }

        // The request row is Approved (ResolvedBy = the guardian).
        await using (var session = store.QuerySession())
        {
            var request = await session.Query<GuardianEventRequest>()
                .Where(r => r.EventId == EventId && r.ChildId == Child)
                .SingleAsync();
            Assert.Equal(GuardianEventRequestStatus.Approved, request.Status);
            Assert.Equal(Guardian, request.ResolvedBy);
        }

        // The guardian.event_rsvp_approve audit row (Via: Guardian).
        var row = await LastAuditAsync(store, "guardian.event_rsvp_approve", EventId);
        Assert.Equal(AccessVia.Guardian, row.Via);
        Assert.Equal(Guardian, row.ActorId);
    }

    [Fact]
    public async Task DenyEventRsvp_NonGuardian_Returns404_NoWrite()
    {
        var (controller, store) = await BuildAsync(actorSubjectId: NonGuardian);
        var events = EventsService(store);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => events.RsvpAsync(EventId, Child, RsvpStatus.Going));

        var result = await controller.DenyEventRsvp(Child, EventId);

        // The Core standing gate fired (UnauthorizedAccessException → the
        // Web's non-leaky 404); the request row is untouched (still
        // Pending).
        Assert.IsType<NotFoundResult>(result);
        await using (var session = store.QuerySession())
        {
            var request = await session.Query<GuardianEventRequest>()
                .Where(r => r.EventId == EventId && r.ChildId == Child)
                .SingleAsync();
            Assert.Equal(GuardianEventRequestStatus.Pending, request.Status);
        }
        Assert.Equal(0, await CountAuditAsync(store, "guardian.event_rsvp_deny", EventId));
    }

    [Fact]
    public async Task VetoEventRsvp_Guardian_RemovesChildRsvp_AuditedViaGuardian()
    {
        var (controller, store) = await BuildAsync(actorSubjectId: Guardian);
        var events = EventsService(store);

        // The GuardianNotifies posture: the child RSVPs freely...
        await (new UserInfoService(store)).SetChildEventRsvpModeAsync(
            Child, EventRsvpMode.GuardianNotifies, Guardian);
        await events.RsvpAsync(EventId, Child, RsvpStatus.Going);

        // ...and the guardian vetoes (the window to undo).
        var result = await controller.VetoEventRsvp(Child, EventId);
        Assert.IsType<RedirectToActionResult>(result);

        await using (var session = store.QuerySession())
        {
            Assert.Equal(0, await session.Query<EventRsvp>()
                .Where(r => r.EventId == EventId && r.UserId == Child)
                .CountAsync());
        }

        var row = await LastAuditAsync(store, "guardian.event_rsvp_veto", EventId);
        Assert.Equal(AccessVia.Guardian, row.Via);
        Assert.Equal(Guardian, row.ActorId);
    }

    // ── 4 — null-safe: no IEventService ⇒ the resolve lanes 404 ──────────

    [Fact]
    public async Task ResolveLanes_WithoutEventService_Return404()
    {
        var (controller, _) = await BuildAsync(actorSubjectId: Guardian, withEvents: false);

        Assert.IsType<NotFoundResult>(await controller.ApproveEventRsvp(Child, EventId));
        Assert.IsType<NotFoundResult>(await controller.DenyEventRsvp(Child, EventId));
        Assert.IsType<NotFoundResult>(await controller.VetoEventRsvp(Child, EventId));
    }

    // ── harness ───────────────────────────────────────────────────────────

    private static EventService EventsService(IDocumentStore store)
        => new(store, new AuthorizationService(store, new UserInfoService(store)), new UserInfoService(store));

    private async Task<(GuardianController controller, IDocumentStore store)> BuildAsync(
        string actorSubjectId, bool withEvents = true)
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
            M4DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // Seed the child's profile (the lane loads it by SubjectId) + the
        // event the resolve lanes act on.
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            session.Store(new Profile
            {
                SubjectId = Child,
                DisplayName = "Child",
                Verified = true,
                Blocked = false,
                Visibility = new Audience(),
            });
            session.Store(new Event
            {
                Id = EventId,
                AuthorId = "ge-author-0144",
                Title = "Cleanup day",
                Body = "body 0144",
                Start = DateTimeOffset.UtcNow.AddDays(30),
                End = DateTimeOffset.UtcNow.AddDays(30).AddHours(2),
                IsDraft = false,
                IsDeleted = false,
            });
            await session.SaveChangesAsync(ct);
        }

        // Always seed the (Guardian, Child) active link — it is the
        // prerequisite for the gate, not for the actor's standing. The
        // actor (Guardian or NonGuardian) holds a different relationship
        // to the lane; the gate fires on the child's link existence.
        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            session.Store(new GuardianLink
            {
                Id = Guid.NewGuid().ToString("N"),
                GuardianId = Guardian,
                ChildId = Child,
                Status = GuardianLinkStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await session.SaveChangesAsync(ct);
        }

        var userInfo = new UserInfoService(store);
        var identity = Substitute.For<IIdentityService>();   // unused in these tests
        var controller = new GuardianController(
            userInfo, identity, store,
            withEvents ? EventsService(store) : null);

        // The ClaimsPrincipal — the actor's subject id via the Kumunita.Sub
        // claim (KumunitaPrincipal.SubjectId reads this).
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(
                    Kumunita.Core.Identity.ClaimTypes.Subject,
                    actorSubjectId) },
                authenticationType: "test"));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // TempData — the happy path writes TempData["info"]; a NoOp provider
        // prevents the NRE (the GuardianMessagingRestrictionTests idiom).
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return (controller, store);
    }

    private static async Task<int> CountAuditAsync(IDocumentStore store, string action, string targetId)
    {
        await using var session = store.QuerySession();
        return await session.Query<AccessAudit>()
            .Where(a => a.Action == action && a.TargetId == targetId)
            .CountAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<AccessAudit> LastAuditAsync(IDocumentStore store, string action, string targetId)
    {
        await using var session = store.QuerySession();
        var row = await session.Query<AccessAudit>()
            .Where(a => a.Action == action && a.TargetId == targetId)
            .OrderByDescending(a => a.At)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(row);
        return row!;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
