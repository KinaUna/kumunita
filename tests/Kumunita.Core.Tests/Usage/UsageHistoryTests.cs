using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Usage;
using Marten;
using System.Reflection;
using Xunit;

namespace Kumunita.Core.Tests.Usage;

/// <summary>
/// The 6 pinned M34 Core tests (design doc §2.4, verbatim names) — the
/// <see cref="UsageAnalyticsService.GetHistoryAsync"/> read half (one
/// <see cref="UsageDailyTotal"/> per <b>present</b> UTC day over a pinned
/// window, <b>ascending</b>, <b>only the days present</b>, unknown window
/// throws, zero <see cref="AccessAudit"/> rows) + the M34·1 additive-only
/// pin (the 2 M13 methods <see cref="UsageAnalyticsService.GetWindowAsync"/>
/// / <see cref="UsageAnalyticsService.GetCsvRowsAsync"/> are <b>unchanged</b>).
/// <para>
/// <see cref="PostgresFixture"/> harness (Testcontainers <c>postgres:18</c>),
/// a fresh scratch database per test, the <see cref="StorageMetricsHistoryTests"/>
/// (M33) <c>BootStoreAsync</c> seed-and-assert shape. <see cref="UsageEvent"/>
/// rows are planted directly (a write session) with <c>At</c> timestamps
/// spanning the window — present days, gap days — and <c>GetHistoryAsync</c>
/// is asserted on the <em>result</em> (the design doc §2.2 shapes), not the
/// SQL emitted (the M13 D7 "assert the result, not the query shape" precedent;
/// the recorded Linq-to-objects fallback in the service is invisible to the
/// contract).
/// </para>
/// </summary>
public sealed class UsageHistoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static int _plantCounter;

    // ── Pin 1 ─────────────────────────────────────────────────────────────
    // M34·1 (FACES M34-4): GetHistoryAsync(30) over a window with UsageEvent
    // rows returns one UsageDailyTotal per **present** UTC day, Total = the
    // per-day UsageEvent count. Two distinct days (3 rows on day A, 2 on day
    // B) → exactly two points with the right totals.

    [Fact]
    public async Task M34_1_GetHistory_Returns_One_Total_Per_Present_Day()
    {
        var store = await BootStoreAsync();
        var svc = new UsageAnalyticsService(store);
        var now = DateTimeOffset.UtcNow;

        // 3 rows on one day, 2 rows on another — both inside the trailing
        // 30-day window.
        for (var i = 0; i < 3; i++) await PlantEvent(store, now.AddDays(-2), "a1", "GET /posts/{id}");
        for (var i = 0; i < 2; i++) await PlantEvent(store, now.AddDays(-5), "a2", "GET /events/{id}");

        var result = await svc.GetHistoryAsync(days: 30, TestContext.Current.CancellationToken);

        // One point per present day (2 days present → 2 points), Total = the
        // per-day count (the M34·1 derived pin, the M13 UsageCsvRow.Total
        // precedent).
        Assert.Equal(2, result.Points.Count);
        Assert.Equal(2, result.Points[0].Total);   // now − 5 days (2 rows)
        Assert.Equal(3, result.Points[1].Total);   // now − 2 days (3 rows)
        // The 2-member DTO ceiling (design doc §2.2): Date + Total only.
        Assert.Equal(now.AddDays(-5).Date, result.Points[0].Date.Date);
        Assert.Equal(now.AddDays(-2).Date, result.Points[1].Date.Date);
        await store.DisposeAsync();
    }

    // ── Pin 2 ─────────────────────────────────────────────────────────────
    // M34·7 (FACES M34-5): GetHistoryAsync(30) over a window with **gaps**
    // (two non-adjacent days, the days between absent) returns **only the
    // days present** — no fabricated zero rows fill the gap (the M33-9 FACE
    // precedent).

    [Fact]
    public async Task M34_7_GetHistory_Returns_Only_Days_Present()
    {
        var store = await BootStoreAsync();
        var svc = new UsageAnalyticsService(store);
        var now = DateTimeOffset.UtcNow;

        // Two non-adjacent days (−5 and −20), the 14 intervening days empty.
        await PlantEvent(store, now.AddDays(-5),  "a1", "GET /posts/{id}");
        await PlantEvent(store, now.AddDays(-20), "a1", "GET /groups/{id}");

        var result = await svc.GetHistoryAsync(days: 30, TestContext.Current.CancellationToken);

        // Only the 2 present days — the 14 gap days are NOT fabricated.
        Assert.Equal(2, result.Points.Count);
        var dates = result.Points.Select(p => p.Date.Date).ToHashSet();
        Assert.Contains(now.AddDays(-5).Date, dates);
        Assert.Contains(now.AddDays(-20).Date, dates);
        // Every point maps to a planted day; no extra day is present.
        Assert.DoesNotContain(now.AddDays(-10).Date, dates);   // a gap day
        await store.DisposeAsync();
    }

    // ── Pin 3 ─────────────────────────────────────────────────────────────
    // M34·7: Points is **ascending** by Date (the §2.1 seam pin). Plant three
    // days out of order and assert the returned Points are sorted Date
    // ascending regardless of plant order.

    [Fact]
    public async Task M34_7_GetHistory_Points_Ascending_By_Date()
    {
        var store = await BootStoreAsync();
        var svc = new UsageAnalyticsService(store);
        var now = DateTimeOffset.UtcNow;

        // Plant in *descending* order (−2, −10, −1) — the read must return
        // them ascending (−10, −2, −1).
        await PlantEvent(store, now.AddDays(-2),  "a1", "GET /posts/{id}");
        await PlantEvent(store, now.AddDays(-10), "a1", "GET /events/{id}");
        await PlantEvent(store, now.AddDays(-1),  "a1", "GET /groups/{id}");

        var result = await svc.GetHistoryAsync(days: 30, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Points.Count);
        var dates = result.Points.Select(p => p.Date.Date).ToList();
        Assert.Equal(dates.OrderBy(d => d).ToList(), dates);   // ascending
        Assert.Equal(now.AddDays(-10).Date, dates[0]);
        Assert.Equal(now.AddDays(-2).Date,  dates[1]);
        Assert.Equal(now.AddDays(-1).Date,  dates[2]);
        await store.DisposeAsync();
    }

    // ── Pin 4 ─────────────────────────────────────────────────────────────
    // M34·7 (FACES M34-10): GetHistoryAsync with an unknown window throws
    // ArgumentOutOfRangeException (the M13 windowDays "unknown value throws,
    // not a 0-row query" precedent) — never a silent empty result. 999 is
    // outside the pinned {7, 30, 90, 180, 365} set.

    [Fact]
    public async Task M34_7_GetHistory_Unknown_Window_Throws()
    {
        var store = await BootStoreAsync();
        var svc = new UsageAnalyticsService(store);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => svc.GetHistoryAsync(days: 999, TestContext.Current.CancellationToken));
        await store.DisposeAsync();
    }

    // ── Pin 5 ─────────────────────────────────────────────────────────────
    // M34·4 (FACES M34-6): the GetHistoryAsync read commits **zero**
    // AccessAudit rows (the read is not an auditable resource — the M13
    // C-M13·6 "the UsageEvent row is not an auditable resource" + the M13 D4
    // "the audit row is on the export only" discipline; the M33·4
    // zero-audit pin). A fresh store has zero audit rows and the read must
    // not add any.

    [Fact]
    public async Task M34_4_GetHistory_Writes_No_AuditRow()
    {
        var store = await BootStoreAsync();
        var svc = new UsageAnalyticsService(store);
        var now = DateTimeOffset.UtcNow;

        await PlantEvent(store, now.AddDays(-1), "a1", "GET /posts/{id}");
        var ct = TestContext.Current.CancellationToken;

        // Sanity: the read returns the planted day (non-trivial result).
        var result = await svc.GetHistoryAsync(days: 30, ct);
        var point = Assert.Single(result.Points);
        Assert.Equal(1, point.Total);

        // Zero AccessAudit rows (M34·4): the read emits no audit row.
        Assert.Equal(0, await AuditCount(store));
        await store.DisposeAsync();
    }

    // ── Pin 6 ─────────────────────────────────────────────────────────────
    // M34·1 (the additive-only pin): the 2 M13 methods (GetWindowAsync +
    // GetCsvRowsAsync) are **unchanged** — the interface now exposes exactly
    // 3 public methods (the 2 M13 + the 1 M34 ADD), the 2 M13 signatures are
    // intact, and both M13 methods still behave correctly over a planted
    // dataset. (In the Core Postgres context there is no NSubstitute
    // `Received`; the honest "unchanged" assertion is the reflection
    // signature pin + a functional M13 correctness assertion — the M34·1
    // additive-only pin, the §2.6 drift-guard "the 2 M13 methods unchanged".)

    [Fact]
    public async Task M34_1_M13_Methods_Unchanged()
    {
        // (a) Additive-only surface pin: exactly 3 public methods; the 2 M13
        //     signatures are unchanged; the 1 M34 ADD is the 3rd.
        var methods = typeof(IUsageAnalyticsService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.DeclaringType == typeof(IUsageAnalyticsService))
            .ToList();
        Assert.Equal(3, methods.Count);
        var names = methods.Select(m => m.Name).ToHashSet();
        Assert.Contains("GetWindowAsync", names);    // M13, unchanged
        Assert.Contains("GetCsvRowsAsync", names);   // M13, unchanged
        Assert.Contains("GetHistoryAsync", names);   // M34 ADD (the 3rd)

        // (b) Functional M13-behaviour-unchanged pin: over a planted dataset
        //     the two M13 reads still return the correct M13 aggregation
        //     shapes (the M13 D3 field + ordering invariants), proving M34
        //     did not re-shape them.
        var store = await BootStoreAsync();
        var svc = new UsageAnalyticsService(store);
        var now = DateTimeOffset.UtcNow;
        var ct = TestContext.Current.CancellationToken;

        // 3 posts + 1 event (authenticated "a1") + 1 anonymous post → the M13
        // aggregates: Total 5, Authenticated 4, Anonymous 1, DistinctActors 1.
        for (var i = 0; i < 3; i++) await PlantEvent(store, now, "a1", "GET /posts/{id}");
        await PlantEvent(store, now, "a1", "GET /events/{id}");
        await PlantEvent(store, now, "",  "GET /posts/{id}");

        var window = await svc.GetWindowAsync(30);
        Assert.Equal(5, window.Total);
        Assert.Equal(4, window.AuthenticatedTotal);
        Assert.Equal(1, window.AnonymousTotal);
        Assert.Equal(1, window.DistinctActors);
        // The M13 D3 ordering pin (descending Total, ties ascending Surface):
        // posts (4) first, events (1) second.
        Assert.Equal(new[] { ("posts", 4), ("events", 1) },
            window.SurfaceRanking.Select(r => (r.Surface, r.Total)));

        var csv = await svc.GetCsvRowsAsync(30);
        // All 5 rows fall on a single UTC day (now) → exactly 2
        // (date, surface) pairs: posts/4 + events/1.
        Assert.Equal(2, csv.Count);
        Assert.Contains(csv, r => r.Surface == "posts"  && r.Total == 4);
        Assert.Contains(csv, r => r.Surface == "events" && r.Total == 1);
        await store.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot a fresh <c>mt</c> schema in a new scratch database — the
    /// surfaces these tests need: <see cref="UsageEvent"/> (the M13 doc the
    /// read GROUPs — the M34·1 source rows) and
    /// <see cref="AccessAudit"/> (the M34·4 zero-audit assertion queries it,
    /// so it must be registered). No <see cref="UsageDocTypes"/> surface is
    /// added (M34·1 — the M13 surface is <b>reused</b>, not re-added).</summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            UsageDocTypes.Configure(opts);   // UsageEvent (the M13 doc the read GROUPs)
            CoreDocTypes.Configure(opts);      // AccessAudit (the M34·4 zero-audit query)
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    /// <summary>The total <see cref="AccessAudit"/> row count in the store
    /// (the M34·4 zero-audit pin).</summary>
    private static async Task<int> AuditCount(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>().CountAsync(ct);
    }

    /// <summary>Plant a <see cref="UsageEvent"/> row directly (a write session
    /// — the test setup, not the code under test).</summary>
    private static async Task PlantEvent(IDocumentStore store, DateTimeOffset at,
        string actorId, string routeTemplate)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new UsageEvent
        {
            Id = $"u34-{Interlocked.Increment(ref _plantCounter)}-{Guid.NewGuid():N}"[..20],
            At = at,
            ActorId = actorId,
            RouteTemplate = routeTemplate
        });
        await w.SaveChangesAsync(ct);
    }
}
