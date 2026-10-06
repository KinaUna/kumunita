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
/// ADR 0028 §F (the count-aware steering amendment, 2026-10-05) — the
/// <b>pinned Web controller tests</b> for the <see cref="GuardianController.
/// Detail"/> surface's <c>IsSoleGuardian</c> flag, which the Detail view
/// reads to branch the independence/delete lanes:
///
/// <list type="bullet">
///   <item><b>Co-guardian</b> (the child has ≥2 active guardians) —
///     <c>IsSoleGuardian</c> is <c>false</c>: the view shows the "Remove
///     myself as guardian" lane and does NOT show the delete lane.</item>
///   <item><b>Sole guardian</b> (this is the only active guardian) —
///     <c>IsSoleGuardian</c> is <c>true</c>: the view shows the "Hand over
///     the account" lane and the "Delete the child account" lane.</item>
/// </list>
///
/// <para>
/// Integration tests (real Marten <c>GuardianLink</c> rows + the real
/// <see cref="UserInfoService"/> reads the Detail action performs —
/// <c>GetGroupIdsAsync</c>, <c>GetCommunityIdsAsync</c>,
/// <c>GetPendingInvitationsForUserAsync</c>, <c>GetComponentsAsync</c>,
/// <c>GetPendingCommunityMembershipRequestsForChildAsync</c>,
/// <c>GetProfileAsync</c>; the <c>IIdentityService</c> is NSubstituted,
/// unused by Detail). The <c>ActiveGuardianCountAsync</c> seam counts
/// <b>active</b> <c>GuardianLink</c> rows for the child (Pending rows are
/// excluded — they confer no standing, the G·2 rule).
/// </para>
/// </summary>
public sealed class GuardianRemoveMyselfSteeringTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Child = "rms-child-001";
    private const string GuardianA = "rms-guardian-a-001";
    private const string GuardianB = "rms-guardian-b-001";

    [Fact]
    public async Task Detail_CoGuardian_IsSoleGuardian_Is_False()
    {
        // Two active guardians — the acting actor is GuardianA; GuardianB is
        // the other active guardian.
        var (controller, _) = await BuildAsync(
            actorSubjectId: GuardianA,
            seedLinks: new[] { (GuardianA, Child), (GuardianB, Child) });

        var result = await controller.Detail(Child);
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(false, view.ViewData["IsSoleGuardian"]);
    }

    [Fact]
    public async Task Detail_SoleGuardian_IsSoleGuardian_Is_True()
    {
        // Only one active guardian — the acting actor is the sole guardian.
        var (controller, _) = await BuildAsync(
            actorSubjectId: GuardianA,
            seedLinks: new[] { (GuardianA, Child) });

        var result = await controller.Detail(Child);
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(true, view.ViewData["IsSoleGuardian"]);
    }

    [Fact]
    public async Task Detail_PendingCoGuardian_Still_Sole_IsSoleGuardian_Is_True()
    {
        // ADR 0028 §F: a child with one <b>active</b> + one <b>pending</b>
        // guardian is still <b>sole</b> for steering — Pending rows confer no
        // standing yet (the standing gates query Active exclusively), so the
        // count is 1 and the sole-guardian steering (hand-over / delete)
        // applies. This is the G·2 "service is the resolver" rule held in
        // the count read.
        var (controller, store) = await BuildAsync(
            actorSubjectId: GuardianA,
            seedLinks: new[] { (GuardianA, Child) });
        // Seed GuardianB as a <b>Pending</b> link (the conferrer's assignment
        // — not yet accepted). The Pending row must NOT count toward the
        // active guardian count — the G·2 rule (Pending rows confer no
        // standing yet) is what makes this the sole-guardian case.
        await SeedGuardianLinkAsync(store, GuardianB, Child, GuardianLinkStatus.Pending);

        var result = await controller.Detail(Child);
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(true, view.ViewData["IsSoleGuardian"]);
    }

    // ── Shared harness (the GuardianAssignmentTests BuildAsync shape) ─────

    private async Task<(GuardianController controller, IDocumentStore store)> BuildAsync(
        string actorSubjectId,
        (string guardianId, string childId)[] seedLinks)
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

        foreach (var (guardianId, childId) in seedLinks)
        {
            await SeedGuardianLinkAsync(store, guardianId, childId, GuardianLinkStatus.Active);
        }

        // The real UserInfoService — the Detail action's reads run against
        // the real store (the standing gate ActiveLinkAsync + the count read
        // ActiveGuardianCountAsync both query real GuardianLink rows).
        var userInfo = new UserInfoService(store);
        // The IIdentityService — NSubstitute (Detail does not call it; all
        // other GU actions that do are out of scope here).
        var identity = Substitute.For<IIdentityService>();

        var controller = new GuardianController(userInfo, identity, store);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(
                    Kumunita.Core.Identity.ClaimTypes.Subject,
                    actorSubjectId) },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return (controller, store);
    }

    private static async Task SeedGuardianLinkAsync(
        IDocumentStore store, string guardianId, string childId, GuardianLinkStatus status)
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new GuardianLink
        {
            Id = Guid.NewGuid().ToString("N"),
            GuardianId = guardianId,
            ChildId = childId,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await session.SaveChangesAsync();
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
