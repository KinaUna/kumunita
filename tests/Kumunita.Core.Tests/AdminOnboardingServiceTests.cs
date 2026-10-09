using Kumunita.Core;
using Kumunita.Core.AdminOnboarding;
using Kumunita.Core.Authorization;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M30 U06 (ADR 0153) — the <see cref="AdminOnboardingService"/> read +
/// write lane pins (the ADR 0150 / ADR 0050 single-write-lane shape,
/// M30·2 / M30·3 / M30·4):
/// <list type="number">
/// <item><b>GetAsync_MissingStore_ReturnsNull</b> — a test construction
/// with no usable <c>IDocumentStore</c> degrades to <c>null</c>
/// (not-yet-guided, the floor — M30·2 / M30·3, the ADR 0050
/// <c>IsSignupOpenAsync</c> best-effort shape).</item>
/// <item><b>GetAsync_MissingRow_ReturnsNull</b> — a fresh store (no
/// <c>AdminOnboarding</c> row) returns <c>null</c> (M30·2 / M30·3).</item>
/// <item><b>CompleteAsync_WritesOneAccessAuditRow</b> — exactly one
/// <c>AccessAudit</c> row (<c>Via = Admin</c>, action
/// <c>admin_onboarding.complete</c>, <c>TargetKind</c> "admin-onboarding" —
/// M30·4, the <c>site.save</c> / <c>signup.set-open</c> /
/// <c>timezone.set-default</c> singleton-toggle shape).</item>
/// <item><b>CompleteAsync_StrongConsistency_LiveOnNextGetAsync</b> — the
/// stamped <c>CompletedAt</c> is live on the very next
/// <see cref="IAdminOnboardingService.GetAsync"/> (strong consistency —
/// M30·4, invariant C4).</item>
/// <item><b>CompleteAsync_UpsertsSingleton_NoDuplicateRow</b> — two
/// completions still yield exactly one <c>AdminOnboarding</c> row (the
/// M29·6 / ADR 0150 D6 upsert pin — M30·2).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Harness note (the U03 / U05 idiom):</b> every <c>*DocTypes.Configure</c>
/// call in this repo lives in the host's <c>AddMarten</c> lambda, not in
/// <c>DependencyInjection.cs</c>. Because the
/// <see cref="AdminOnboardingDocTypes.Configure"/> host line in
/// <c>Program.cs</c> (U03) registers the doc type for the *host*, this
/// harness wires the SAME <see cref="AdminOnboardingDocTypes.Configure"/>
/// call in its own <c>DocumentStore.For</c> lambda — without it the
/// <c>AdminOnboarding</c> doc is invisible to Marten (the
/// <see cref="SiteContentServiceTests.BootStoreAsync"/> /
/// <see cref="AdminOnboardingSeederTests.BootStoreAsync"/> idiom).
/// </para>
/// <para>
/// <b>Collision idiom:</b> the doc type is fully qualified as
/// <c>AdminOnboarding.AdminOnboarding</c> (the type and its parent namespace
/// share the name <c>AdminOnboarding</c>; the unqualified name in this
/// namespace resolves to the *namespace*) — the exact
/// <c>SiteContent.SiteContent</c> / <c>SurfaceLabels.SurfaceLabels</c> idiom.
/// The service is fully qualified as
/// <c>AdminOnboarding.AdminOnboardingService</c> for the same reason.
/// </para>
/// <para>
/// <b>The "missing store" pin (test 1)</b> uses an NSubstitute
/// <c>IDocumentStore</c> whose <c>QuerySession()</c> is stubbed to throw —
/// the <c>GetAsync</c> read's <c>try</c>/<c>catch</c> (the ADR 0050
/// best-effort shape, M30·3) degrades to <c>null</c> — the same
/// "a test construction with no usable store" the pin names. The other
/// four pins exercise the real service against a real Postgres scratch
/// store (the <see cref="PostgresFixture"/> idiom, the
/// <see cref="SiteContentServiceTests"/> shape — the audit-row /
/// strong-consistency / upsert pins are store-behaviour pins, the
/// <see cref="SiteContentServiceTests.SaveAsync_WritesOneAccessAuditRow"/>
/// precedent).
/// </para>
/// <para>
/// No new Core or Web code; the service is U03's deliverable — these pins
/// are the first M30 service tests (the U06 seam-test unit).
/// </para>
public class AdminOnboardingServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — a missing store degrades to null (M30·2 / M30·3) ──────────────
    // A test construction with no usable IDocumentStore (the QuerySession
    // call throws) → GetAsync returns null (= not-yet-guided, the floor).
    // The read never throws and never returns a sentinel other than null —
    // the ADR 0050 IsSignupOpenAsync best-effort shape (M30·3).

    [Fact(DisplayName = "U06 GetAsync with a missing store returns null (the not-yet-guided floor)")]
    public async Task GetAsync_MissingStore_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        // A test construction with no usable store (null! — the QuerySession
        // call NREs, the GetAsync read's try/catch degrades to null — M30·3,
        // the ADR 0050 IsSignupOpenAsync best-effort shape).
        var svc = new AdminOnboarding.AdminOnboardingService(null!);

        var result = await svc.GetAsync(ct);

        // null = not-yet-guided (the floor — the banner shows, the
        // walk-through is available). Never a sentinel, never a throw.
        Assert.Null(result);
    }

    // ── 2 — a missing row degrades to null (M30·2 / M30·3) ─────────────────
    // A fresh store (no AdminOnboarding row — a fresh boot before the
    // seeder ran) → GetAsync returns null (the not-yet-guided floor).

    [Fact(DisplayName = "U06 GetAsync with a fresh store (no row) returns null")]
    public async Task GetAsync_MissingRow_ReturnsNull()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new AdminOnboarding.AdminOnboardingService(store);

        // No row seeded — a fresh boot before the seeder ran.
        var result = await svc.GetAsync(ct);

        Assert.Null(result);
    }

    // ── 3 — CompleteAsync writes exactly one AccessAudit row (M30·4) ──────
    // The audit row: Via = Admin, action "admin_onboarding.complete",
    // TargetKind "admin-onboarding", TargetId "singleton" — the
    // site.save / signup.set-open / timezone.set-default singleton-toggle
    // shape (M30·4).

    [Fact(DisplayName = "U06 CompleteAsync writes exactly one AccessAudit row (admin_onboarding.complete, admin-onboarding, Admin)")]
    public async Task CompleteAsync_WritesOneAccessAuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new AdminOnboarding.AdminOnboardingService(store);

        const string actor = "admin-onboarding-u06-audit";
        await svc.CompleteAsync(actor, ct);

        // Exactly one AccessAudit row with the expected shape.
        await using var q = store.QuerySession();
        var auditRows = await q.Query<AccessAudit>()
            .Where(a => a.Action == "admin_onboarding.complete" && a.TargetKind == "admin-onboarding")
            .ToListAsync(ct);

        Assert.Single(auditRows);
        Assert.Equal("admin_onboarding.complete", auditRows[0].Action);
        Assert.Equal("admin-onboarding", auditRows[0].TargetKind);
        Assert.Equal(AdminOnboarding.AdminOnboarding.SingletonId, auditRows[0].TargetId);
        Assert.Equal(AccessVia.Admin, auditRows[0].Via);
        Assert.Equal(actor, auditRows[0].ActorId);
        Assert.Equal(actor, auditRows[0].EffectivePrincipalId);
        Assert.Equal(AccessOutcome.Allow, auditRows[0].Outcome);

        // The completion was stamped (the doc half of the one session).
        await using var q2 = store.QuerySession();
        var row = await q2.LoadAsync<AdminOnboarding.AdminOnboarding>(
            AdminOnboarding.AdminOnboarding.SingletonId, ct);
        Assert.NotNull(row);
        Assert.NotNull(row!.CompletedAt);
    }

    // ── 4 — strong consistency: live on the next GetAsync (M30·4, C4) ─────
    // A completion is immediately visible on the very next GetAsync call —
    // no restart, no projection lag.

    [Fact(DisplayName = "U06 CompleteAsync is strong-consistency (live on the next GetAsync)")]
    public async Task CompleteAsync_StrongConsistency_LiveOnNextGetAsync()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new AdminOnboarding.AdminOnboardingService(store);

        // The pre-stamp read is null (a fresh store — no row).
        Assert.Null(await svc.GetAsync(ct));

        var before = DateTimeOffset.UtcNow;
        const string actor = "admin-onboarding-u06-consistency";
        await svc.CompleteAsync(actor, ct);

        // The very next GetAsync reflects the stamp (strong consistency).
        var after = await svc.GetAsync(ct);
        Assert.NotNull(after);
        Assert.True(after.Value >= before,
            $"Expected the stamped CompletedAt ({after}) >= the pre-stamp clock ({before}).");
    }

    // ── 5 — upserts the singleton (no duplicate row) (M30·2, ADR 0150 D6) ─
    // Two completions still yield exactly one row — the singleton is
    // upserted in place (the M29·6 / ADR 0150 D6 pin, M30·2).

    [Fact(DisplayName = "U06 CompleteAsync upserts the singleton (two completions, one row)")]
    public async Task CompleteAsync_UpsertsSingleton_NoDuplicateRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new AdminOnboarding.AdminOnboardingService(store);

        // Completion 1.
        await svc.CompleteAsync("admin-onboarding-u06-upsert-1", ct);

        // Completion 2 (a different actor — the upsert must still be one row).
        await svc.CompleteAsync("admin-onboarding-u06-upsert-2", ct);

        // Still exactly one row.
        await using var q = store.QuerySession();
        var rows = await q.Query<AdminOnboarding.AdminOnboarding>().ToListAsync(ct);
        Assert.Single(rows);
        Assert.Equal(AdminOnboarding.AdminOnboarding.SingletonId, rows[0].Id);

        // The latest completion wins (upsert, not append).
        Assert.NotNull(rows[0].CompletedAt);
    }

    // ─── Shared helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Boot a fresh scratch store with the <c>AdminOnboarding</c> doc type
    /// registered in the <c>DocumentStore.For</c> lambda (the U03 harness
    /// note — the host <c>Program.cs</c> line registers the doc type for the
    /// host; the harness wires the SAME
    /// <see cref="AdminOnboardingDocTypes.Configure"/> call here, the same
    /// idiom as every other <c>*DocTypes</c> test harness). Without it the
    /// <c>AdminOnboarding</c> doc is invisible to Marten.
    /// </summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            AdminOnboardingDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
