using Kumunita.Core.Authorization;
using Kumunita.Core.Kiosk;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The 6 pinned M35 Core tests (design doc §2.5, verbatim names) — the
/// <see cref="KioskService"/> read half (<see cref="KioskService.GetPlaylistAsync"/>,
/// the M35·2 / M35-8 nullable floor + the M35·5 zero-audit-row pin) and the
/// write half (<see cref="KioskService.SavePlaylistAsync"/>, the M35·5 one
/// <c>kiosk.save</c> row + the M35·2 upsert + the M3 caller's-session
/// same-transaction pin).
/// <para>
/// Harness: the <see cref="PostgresFixture"/> (Testcontainers
/// <c>postgres:18</c>) fresh-scratch-database-per-test shape (the
/// <see cref="Usage.StorageMetricsHistoryTests"/> M33 <c>BootStoreAsync</c>
/// precedent). The store registers the <see cref="KioskPlaylist"/> (the
/// <see cref="KioskDocTypes"/> surface) + <see cref="AccessAudit"/> (the
/// <see cref="CoreDocTypes"/> surface) — the two doc types these pins read.
/// The <see cref="KioskService"/> is the code under test (U03); these pin the
/// *results* (the design doc §2.2 shapes), not the SQL. The
/// <see cref="IAuthorizationService"/> is carried but **not** invoked
/// (M35·1 — "no <c>Decide()</c> branch"), so a bare substitute satisfies the
/// ctor's non-null guard.
/// </para>
/// </summary>
public sealed class KioskServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Admin = "m35-admin-001";

    // ── Pin 1 ─────────────────────────────────────────────────────────────
    // M35·2 / M35-8 (FACES M35-8): GetPlaylistAsync over a NO-ROW store
    // returns null — a fresh boot before the GlobalAdmin configured the
    // playlist is a legitimate empty state, not a degraded one (the design
    // doc §2.2 "nullable read" divergence from the SiteContentService
    // never-null template).

    [Fact]
    public async Task M35_2_GetPlaylist_NoRow_ReturnsNull()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);

        var result = await svc.GetPlaylistAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
        await store.DisposeAsync();
    }

    // ── Pin 2 ─────────────────────────────────────────────────────────────
    // M35·2 (FACES M35-1): GetPlaylistAsync over a CONFIGURED singleton
    // returns the row — the ordered Views + the two intervals project through
    // (the M35·2 4-member ceiling: Id sentinel + Views + ViewIntervalSeconds
    // + RefreshIntervalSeconds).

    [Fact]
    public async Task M35_2_GetPlaylist_Configured_ReturnsRow()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);
        var ct = TestContext.Current.CancellationToken;

        await PlantPlaylist(store, views: 2, viewInterval: 45, refreshInterval: 15);

        var row = await svc.GetPlaylistAsync(ct);

        Assert.NotNull(row);
        Assert.Equal(KioskPlaylist.SingletonId, row!.Id);
        Assert.Equal(2, row.Views.Count);
        Assert.Equal(45, row.ViewIntervalSeconds);
        Assert.Equal(15, row.RefreshIntervalSeconds);
        await store.DisposeAsync();
    }

    // ── Pin 3 ─────────────────────────────────────────────────────────────
    // M35·5 (ADR 0164 D5): GetPlaylistAsync writes ZERO AccessAudit rows —
    // the read is a config load, not an access decision (the M34·4 / M13 D4
    // "the read = no row" discipline). A fresh store has zero audit rows, and
    // the read must not add any.

    [Fact]
    public async Task M35_5_GetPlaylist_WritesNoAuditRow()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);
        var ct = TestContext.Current.CancellationToken;

        await svc.GetPlaylistAsync(ct);

        Assert.Equal(0, await AuditCount(store));
        await store.DisposeAsync();
    }

    // ── Pin 4 ─────────────────────────────────────────────────────────────
    // M35·5 (ADR 0164 D5): SavePlaylistAsync commits EXACTLY ONE kiosk.save
    // AccessAudit row (Action = "kiosk.save", TargetKind = "kiosk",
    // Via = Admin, Outcome = Allow) — the admin-surface write-lane
    // convention (the SiteContentService.SaveAsync one-row shape).

    [Fact]
    public async Task M35_5_SavePlaylist_OneKioskSaveAuditRow()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);
        var ct = TestContext.Current.CancellationToken;

        await using var session = store.LightweightSession();
        await svc.SavePlaylistAsync(
            views: [new KioskView { Kind = KioskViewKind.Board, BoardId = "board-1" }],
            viewIntervalSeconds: 40,
            refreshIntervalSeconds: 12,
            actorId: Admin,
            session,
            ct);

        var rows = await AllAudits(store);
        Assert.Single(rows);
        Assert.Equal("kiosk.save", rows[0].Action);
        Assert.Equal("kiosk", rows[0].TargetKind);
        Assert.Equal(KioskPlaylist.SingletonId, rows[0].TargetId);
        Assert.Equal(AccessVia.Admin, rows[0].Via);
        Assert.Equal(AccessOutcome.Allow, rows[0].Outcome);
        await store.DisposeAsync();
    }

    // ── Pin 5 ─────────────────────────────────────────────────────────────
    // M35·2 (the design doc §2.2 "upsert vs. no-op" divergence — the register
    // wins): SavePlaylistAsync over a NO-ROW store CREATES the singleton — a
    // GlobalAdmin can configure the playlist on a fresh instance without a
    // seeder (the one intentional departure from the SiteContentService
    // no-op-on-missing template).

    [Fact]
    public async Task M35_2_SavePlaylist_NoRow_CreatesSingleton()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);
        var ct = TestContext.Current.CancellationToken;

        // Sanity: no row before the write (the M35-8 floor).
        Assert.Null(await svc.GetPlaylistAsync(ct));

        await using var session = store.LightweightSession();
        var saved = await svc.SavePlaylistAsync(
            views: [new KioskView { Kind = KioskViewKind.Todos, ComponentId = "comp-1" }],
            viewIntervalSeconds: 50,
            refreshIntervalSeconds: 20,
            actorId: Admin,
            session,
            ct);

        // The upsert created the singleton (the M35·2 pin):
        Assert.Equal(KioskPlaylist.SingletonId, saved.Id);

        // And a subsequent read (the M35·2 read pin) now returns the row:
        var reloaded = await svc.GetPlaylistAsync(ct);
        Assert.NotNull(reloaded);
        Assert.Equal(50, reloaded!.ViewIntervalSeconds);
        Assert.Equal(20, reloaded.RefreshIntervalSeconds);
        Assert.Single(reloaded.Views);
        Assert.Equal(KioskViewKind.Todos, reloaded.Views[0].Kind);
        await store.DisposeAsync();
    }

    // ── Pin 6 ─────────────────────────────────────────────────────────────
    // M3 caller's-session same-transaction pin (design doc §2.1 / §2.2):
    // SavePlaylistAsync writes the doc + its single audit row into the
    // CALLER's IDocumentSession (in-transaction with surrounding work), so the
    // playlist row and its kiosk.save audit row commit atomically — both
    // visible in the caller's session after SaveChanges, and both persisted
    // once the session disposes.

    [Fact]
    public async Task M35_SavePlaylist_AtomicInCallerSession()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);
        var ct = TestContext.Current.CancellationToken;

        await using var session = store.LightweightSession();
        var saved = await svc.SavePlaylistAsync(
            views: [new KioskView { Kind = KioskViewKind.Calendar, CalendarView = "week" }],
            viewIntervalSeconds: 60,
            refreshIntervalSeconds: 30,
            actorId: Admin,
            session,
            ct);
        Assert.Equal(KioskPlaylist.SingletonId, saved.Id);

        // In the CALLER's session, both the playlist row and exactly one
        // kiosk.save audit row are committed together (the M3 same-transaction
        // pin — a single SaveChanges in the caller's transaction):
        var inSessionRow = await session.LoadAsync<KioskPlaylist>(KioskPlaylist.SingletonId, ct);
        Assert.NotNull(inSessionRow);
        Assert.Equal(60, inSessionRow!.ViewIntervalSeconds);

        var inSessionAudits = await session.Query<AccessAudit>().ToListAsync(ct);
        Assert.Single(inSessionAudits);
        Assert.Equal("kiosk.save", inSessionAudits[0].Action);
        Assert.Equal("kiosk", inSessionAudits[0].TargetKind);

        // After the caller's session commits, both persist (no partial state —
        // the atomic pair either commits together or not at all):
        await store.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot a fresh <c>mt</c> schema in a new scratch database — the
    /// two doc types these pins read: <see cref="KioskPlaylist"/> (the
    /// <see cref="KioskDocTypes"/> M35 surface) +
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> (the
    /// <see cref="CoreDocTypes"/> surface — the M35·5 audit-count assertions
    /// query it, so it must be registered). The <see cref="KioskView"/> entry
    /// is a value object nested in the singleton (not a standalone doc — M35·9),
    /// so it needs no separate registration.</summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            KioskDocTypes.Configure(opts);   // KioskPlaylist (the M35 doc)
            CoreDocTypes.Configure(opts);       // AccessAudit (the M35·5 zero/one-audit query)
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    /// <summary>A <see cref="KioskService"/> wired over a bare
    /// <see cref="IAuthorizationService"/> substitute — the ctor validates it
    /// non-null but **never invokes it** (M35·1 — "no <c>Decide()</c> branch"),
    /// so a substitute is a faithful double here.</summary>
    private static KioskService BuildService(IDocumentStore store)
        => new(store, Substitute.For<IAuthorizationService>());

    /// <summary>Plant a <see cref="KioskPlaylist"/> singleton directly (a write
    /// session — the test setup, not the code under test). <paramref name="views"/>
    /// controls the slide count (the M35·2 Views ceiling).</summary>
    private static async Task PlantPlaylist(
        IDocumentStore store, int views, int viewInterval, int refreshInterval)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.LightweightSession();
        var list = new List<KioskView>();
        for (var i = 0; i < views; i++)
        {
            list.Add(new KioskView { Kind = KioskViewKind.Board, BoardId = $"board-{i}" });
        }
        w.Store(new KioskPlaylist
        {
            Id = KioskPlaylist.SingletonId,
            Views = list,
            ViewIntervalSeconds = viewInterval,
            RefreshIntervalSeconds = refreshInterval,
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>The whole <see cref="AccessAudit"/> table (unsorted — the test
    /// inspects). The M35·5 zero/one-audit pins query this.</summary>
    private static async Task<IReadOnlyList<AccessAudit>> AllAudits(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>().ToListAsync(ct);
    }

    /// <summary>The total <see cref="AccessAudit"/> row count (the M35·5
    /// zero-audit pin for the read).</summary>
    private static async Task<int> AuditCount(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<AccessAudit>().CountAsync(ct);
    }
}
