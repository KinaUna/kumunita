using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M28 (ADR 0151 D6, design doc §2.6 items 18–20) — the guardian's
/// "Time limits" surface on the child's <c>Detail</c> page (the GU
/// inverse of the M20 quiet lane, ADR 0121). Three pinned Web tests +
/// the re-pinned <c>ChildAccountItem</c> 4-field record pin.
/// <para>
/// The surface (D6): a "Time limits" section on <c>/me/children/{childId}</c>
/// (NOT a new route — the GU Detail page's existing sections as the shape).
/// GET seeds the form with the current schedule via
/// <see cref="IUserInfoService.GetChildTimeLimitAsync"/> (the guardian-gated
/// read, §2.1); POST saves/clears via
/// <see cref="IUserInfoService.SetChildTimeLimitAsync"/> (one audit row,
/// <c>guardian.time-limit.set</c>; <c>null</c> = row delete, the M20 clear
/// idiom, C-M28·3 floor). Flash:
/// <c>guardian.timelimit.flash_saved</c> / <c>_cleared</c> (the
/// <see cref="Kumunita.Core.Localization.KnownTranslationKeys.EnValues"/>
/// floor).
/// <para>
/// The child has **no** self-lane (C-M28·7) — the only write is the
/// guardian-gated seam. A GlobalAdmin reaches the same
/// <see cref="IUserInfoService.SetChildTimeLimitAsync"/> write seam (the
/// G·5 safety valve) — the standing is resolved by the seam, not a
/// separate admin page.
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom
/// (the <see cref="GuardianAssignmentTests"/> / <see cref="GuardianDeleteChildTests"/>
/// shape): a real <see cref="UserInfoService"/> (so the
/// <c>ActiveLinkAsync</c> standing gate + the
/// <c>SetChildTimeLimitAsync</c> seam run real Marten SQL) + an NSubstitute
/// <see cref="IIdentityService"/> (inert for these tests). Each test hands
/// itself a fresh scratch Postgres DB (<see cref="PostgresFixture
/// .NewDatabaseAsync"/>), the <c>BootStoreAsync</c> shape the GU lane
/// established.
/// </para>
/// </summary>
public sealed class GuardianTimeLimitSurfaceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Guardian = "m28-sf-guardian-001";
    private const string Child    = "m28-sf-child-001";

    // ── F5 — the Detail GET seeds the Time limits section ──────────────────
    // A guardian with an active link over a child who has a saved
    // GuardianTimeLimitSchedule (Enabled=true, Blocked mode, Hours=[22,23],
    // DaysOfWeek=[6]) → the Detail action's ViewData["TimeLimitsSection"]
    // carries the current schedule values (the GetChildTimeLimitAsync
    // guardian-gated read, §2.1). The floor (C-M28·3) is NOT exercised here
    // (the schedule exists and is enabled); the disabled / null floor is
    // pinned by the F3 pure tests (U02) + the F6 middleware tests (U04).

    [Fact]
    public async Task F5_Detail_Get_SeedsWithCurrentSchedule()
    {
        var (controller, store) = await BuildAsync();
        var ct = TestContext.Current.CancellationToken;

        // Seed the child's active GuardianTimeLimitSchedule (the guardian
        // set it — the row is per-child, id = ChildId, the M20 shape).
        await using (var seedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            seedSession.Store(new GuardianTimeLimitSchedule
            {
                ChildId   = Child,
                Enabled   = true,
                Mode      = TimeLimitMode.Blocked,
                Hours     = [22, 23],
                DaysOfWeek = [6],
                Updated   = DateTimeOffset.UtcNow,
            });
            await seedSession.SaveChangesAsync(ct);
        }

        // The Detail GET — the standing gate (ActiveLinkAsync) passes
        // (a seeded active link); the reads run against the real store.
        // The action returns View(model) (no explicit view name — ViewName
        // is null, resolved by convention from the action name), so we
        // assert on the ViewData directly.
        var result = await controller.Detail(Child);

        var view = Assert.IsType<ViewResult>(result);
        Assert.NotNull(view.ViewData);

        // ViewData["TimeLimitsSection"] — the TimeLimitsSection record
        // (the M20 Quiet shape, the GU inverse).
        var section = Assert.IsType<TimeLimitsSection>(
            view.ViewData["TimeLimitsSection"]);

        Assert.True(section.Enabled);
        Assert.Equal("blocked", section.Mode);
        Assert.Equal([22, 23], section.Hours.ToArray());
        Assert.Equal([6], section.DaysOfWeek.ToArray());
    }

    // ── F5 — the Detail POST saves + flashes ────────────────────────────────
    // A guardian with an active link over a child → POST SaveTimeLimits
    // (enabled=true, mode="blocked", hours=[22,23], daysOfWeek=[6],
    // clear=null) → the SetChildTimeLimitAsync seam writes the row (one
    // audit row, guardian.time-limit.set) → the action redirects to Detail
    // + flashes guardian.timelimit.flash_saved (the
    // KnownTranslationKeys.EnValues floor). Strong consistency (C-M28·4):
    // the row is live on the very next read.

    [Fact]
    public async Task F5_Detail_Post_SavesAndFlashes()
    {
        var (controller, store) = await BuildAsync();
        var ct = TestContext.Current.CancellationToken;

        // The happy path — the guardian saves a Blocked-mode schedule.
        var result = await controller.SaveTimeLimits(
            Child,
            enabled: true,
            mode: "blocked",
            hours: [22, 23],
            daysOfWeek: [6],
            clear: null);

        // The action redirects to Detail (the Suspend / Unsuspend precedent).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        // The flash: guardian.timelimit.flash_saved (the
        // KnownTranslationKeys.EnValues floor — the "Time limits saved…"
        // text the _FlashToast surfaces).
        Assert.NotNull(controller.TempData?["info"]);
        Assert.Contains("Time limits saved",
            (string?)controller.TempData?["info"] ?? "");

        // The row is live on the very next read (C-M28·4, strong
        // consistency — the ADR 0050 IsSignupOpen shape, no projection,
        // no cache). The real UserInfoService wrote it.
        await using var readSession = store.QuerySession();
        var schedule = await readSession
            .LoadAsync<GuardianTimeLimitSchedule>(Child, ct);
        Assert.NotNull(schedule);
        Assert.True(schedule!.Enabled);
        Assert.Equal(TimeLimitMode.Blocked, schedule.Mode);
        Assert.Equal([22, 23], schedule.Hours);
        Assert.Equal([6], schedule.DaysOfWeek);

        // ONE audit row for the write (the GU "one audited row" pin,
        // the guardian.suspend / guardian.unsuspend shape).
        var auditCount = await CountAuditAsync(
            store, "guardian.time-limit.set", Child, ct);
        Assert.Equal(1, auditCount);
    }

    // ── F5 — the Detail POST with clear=1 deletes the row ──────────────────
    // A guardian with an active link over a child who has a saved
    // GuardianTimeLimitSchedule → POST SaveTimeLimits (clear="1") → the
    // SetChildTimeLimitAsync seam deletes the row (the M20 "clear" idiom,
    // null schedule = row delete, C-M28·3 floor) → the action redirects to
    // Detail + flashes guardian.timelimit.flash_cleared. The read seam
    // then returns null (the floor: no schedule = never restricted).

    [Fact]
    public async Task F5_Detail_Post_Clear_SetsNull()
    {
        var (controller, store) = await BuildAsync();
        var ct = TestContext.Current.CancellationToken;

        // Seed a schedule (the guardian set it earlier).
        await using (var seedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            seedSession.Store(new GuardianTimeLimitSchedule
            {
                ChildId   = Child,
                Enabled   = true,
                Mode      = TimeLimitMode.Blocked,
                Hours     = [22, 23],
                DaysOfWeek = [6],
                Updated   = DateTimeOffset.UtcNow,
            });
            await seedSession.SaveChangesAsync(ct);
        }

        // Confirm the row exists before the clear.
        await using (var preSession = store.QuerySession())
        {
            var pre = await preSession.LoadAsync<GuardianTimeLimitSchedule>(Child, ct);
            Assert.NotNull(pre);
            Assert.True(pre!.Enabled);
        }

        // The clear — clear="1" → the Core seam deletes the row.
        var result = await controller.SaveTimeLimits(
            Child,
            enabled: false,
            mode: null,
            hours: null,
            daysOfWeek: null,
            clear: "1");

        // The action redirects to Detail (the Suspend / Unsuspend precedent).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Detail", redirect.ActionName);

        // The flash: guardian.timelimit.flash_cleared (the
        // KnownTranslationKeys.EnValues floor — the "Time limits cleared…"
        // text the _FlashToast surfaces).
        Assert.NotNull(controller.TempData?["info"]);
        Assert.Contains("Time limits cleared",
            (string?)controller.TempData?["info"] ?? "");

        // The row is now null (the floor, C-M28·3: no schedule = never
        // restricted). The GetActiveTimeLimitAsync read (the enforcement
        // seam) returns null.
        await using var postSession = store.QuerySession();
        var post = await postSession.LoadAsync<GuardianTimeLimitSchedule>(Child, ct);
        Assert.Null(post);
    }

    // ── Harness (the GuardianAssignmentTests BuildAsync shape, verbatim) ───

    private async Task<(GuardianController controller, IDocumentStore store)> BuildAsync()
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

        // Seed the guardian's own active link (the standing gate passes).
        await using (var seedSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            seedSession.Store(new GuardianLink
            {
                Id         = Guid.NewGuid().ToString("N"),
                GuardianId = Guardian,
                ChildId    = Child,
                Status     = GuardianLinkStatus.Active,
                CreatedAt  = DateTimeOffset.UtcNow,
            });
            await seedSession.SaveChangesAsync(ct);
        }

        // The real UserInfoService (the GetChildTimeLimitAsync read + the
        // SetChildTimeLimitAsync seam run against the real store — the
        // strong-consistency assertions need a live row).
        var userInfo = new UserInfoService(store);

        // The IIdentityService — NSubstitute (inert for these tests; the
        // time-limit seams are on IUserInfoService, not IIdentityService).
        var identity = Substitute.For<IIdentityService>();

        var controller = new GuardianController(userInfo, identity, store);

        // The ClaimsPrincipal — the actor's subject id via the Kumunita.Sub
        // claim (KumunitaPrincipal.SubjectId reads this).
        var httpContext = new DefaultHttpContext();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(
                    Kumunita.Core.Identity.ClaimTypes.Subject,
                    Guardian) },
                authenticationType: "test"));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // TempData — the happy path writes TempData["info"] before the
        // redirect; a NoOpTempDataProvider prevents the NRE.
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return (controller, store);
    }

    private static async Task<int> CountAuditAsync(
        IDocumentStore store, string action, string targetId, CancellationToken ct)
    {
        await using var session = store.QuerySession();
        return await session.Query<AccessAudit>()
            .Where(a => a.Action == action && a.TargetId == targetId)
            .CountAsync(ct);
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
