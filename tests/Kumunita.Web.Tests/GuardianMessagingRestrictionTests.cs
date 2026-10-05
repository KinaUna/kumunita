using Kumunita.Core;
using Kumunita.Core.Authorization;
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
/// M9 amendment — the <see cref="GuardianController.SetChildMessaging"/>
/// integration tests (the /me/children/{childId}/messaging POST, the
/// guardian's messaging-restriction ceiling). The pins:
/// <list type="number">
/// <item><b>Standing gate first.</b> A non-guardian (no active
///       <see cref="GuardianLink"/> over the child) is a
///       <see cref="NotFoundResult"/> — the ADR 0028 deny-by-default
///       shape, the <see cref="GuardianAssignmentTests.Assign_NonGuardian_Returns404"/>
///       idiom verbatim. The ceiling write is never reached.</item>
/// <item><b>Happy path.</b> A guardian with an active link POSTs
///       <c>restricted = true</c>: the action calls
///       <see cref="IUserInfoService.SetChildMessagingRestrictionAsync"/>
///       with the (childId, restricted, guardianId) tuple, the ceiling is
///       live on the very next <see cref="IUserInfoService
///       .GetProfileAsync"/> read (C4 strong consistency), a
///       <c>guardian.messaging_restrict</c> audit row with
///       <c>Via: Guardian</c> is committed, and the action redirects
///       back to <c>Detail</c>.</item>
/// <item><b>Round-trip.</b> A second POST with <c>restricted = false</c>
///       lifts the ceiling; the ceiling is back to <c>false</c> on the
///       next read. The write is idempotent in effect (the flag is the
///       state, not a log).</item>
/// </list>
/// Integration tests (not NSubstitute-only) — the same harness shape as
/// <see cref="GuardianAssignmentTests"/> (the real Marten store, the real
/// <see cref="UserInfoService"/>, the real <c>ActiveLinkAsync</c> standing
/// gate) — the ceiling write is the <see
/// cref="IUserInfoService.SetChildMessagingRestrictionAsync"/> seam, and
/// the <c>guardian.messaging_restrict</c> audit row is only produced by
/// the real store.
/// </summary>
public sealed class GuardianMessagingRestrictionTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Guardian = "gm-guardian-001";
    private const string Child = "gm-child-001";
    private const string NonGuardian = "gm-non-guardian-001";

    // ── 1 — standing gate: a non-guardian is a 404, no write ─────────────

    [Fact]
    public async Task SetChildMessaging_NonGuardian_Returns404_NoWrite()
    {
        var (controller, store) = await BuildAsync(actorSubjectId: NonGuardian);

        var result = await controller.SetChildMessaging(Child, restricted: true);

        Assert.IsType<NotFoundResult>(result);

        // The ceiling is untouched (the standing gate fired before the
        // write; the ADR 0028 deny-by-default shape).
        var profile = await (new UserInfoService(store)).GetProfileAsync(Child);
        Assert.False(profile?.MessagingRestricted ?? false);

        // No audit row (the refuse fired before the write; the
        // "not an access decision" + "no standing" combined shape).
        Assert.Equal(0, await CountAuditAsync(store, "guardian.messaging_restrict", Child));
    }

    // ── 2 — happy path: the ceiling is written, audited, live on next read ─

    [Fact]
    public async Task SetChildMessaging_Guardian_RestrictedTrue_CeilingLive_AuditedViaGuardian()
    {
        var (controller, store) = await BuildAsync(actorSubjectId: Guardian);
        var userInfo = new UserInfoService(store);

        var result = await controller.SetChildMessaging(Child, restricted: true);
        var redirect = Assert.IsType<RedirectToActionResult>(result);

        // The redirect goes back to the Detail page (the standard
        // "save + back to curation" shape, the Suspend/Unsuspend idiom).
        Assert.Equal(nameof(GuardianController.Detail), redirect.ActionName);
        Assert.Equal(Child, redirect.RouteValues["childId"]);

        // The ceiling is live on the very next read (C4).
        Assert.True((await userInfo.GetProfileAsync(Child))!.MessagingRestricted);

        // The audit row (guardian.messaging_restrict, Via: Guardian, the
        // GU G·2 pin carried to the messaging lane).
        var row = await LastAuditAsync(store, "guardian.messaging_restrict", Child);
        Assert.Equal(AccessVia.Guardian, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal(Guardian, row.ActorId);
    }

    // ── 3 — round-trip: a second POST lifts the ceiling ──────────────────

    [Fact]
    public async Task SetChildMessaging_RestrictedFalse_LiftsCeiling()
    {
        var (controller, store) = await BuildAsync(actorSubjectId: Guardian);
        var userInfo = new UserInfoService(store);

        // Restrict first.
        await controller.SetChildMessaging(Child, restricted: true);
        Assert.True((await userInfo.GetProfileAsync(Child))!.MessagingRestricted);

        // Allow — the ceiling lifts (the child's own opt-in now decides).
        var result = await controller.SetChildMessaging(Child, restricted: false);
        Assert.IsType<RedirectToActionResult>(result);

        Assert.False((await userInfo.GetProfileAsync(Child))!.MessagingRestricted);
    }

    // ── harness ───────────────────────────────────────────────────────────

    private async Task<(GuardianController controller, IDocumentStore store)> BuildAsync(string actorSubjectId)
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
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // Seed the child's profile (the lane loads it by SubjectId).
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
            await session.SaveChangesAsync(ct);
        }

        // Seed the guardian's active link (the standing gate reads it).
        if (actorSubjectId == Guardian)
        {
            await SeedGuardianLinkAsync(store, Guardian, Child, ct);
        }

        var userInfo = new UserInfoService(store);
        var identity = Substitute.For<IIdentityService>();   // unused in these tests
        var controller = new GuardianController(userInfo, identity, store);

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
        // prevents the NRE (the GuardianAssignmentTests idiom).
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return (controller, store);
    }

    private static async Task SeedGuardianLinkAsync(IDocumentStore store, string guardianId, string childId, CancellationToken ct)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new GuardianLink
        {
            Id = Guid.NewGuid().ToString("N"),
            GuardianId = guardianId,
            ChildId = childId,
            Status = GuardianLinkStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await session.SaveChangesAsync(ct);
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
