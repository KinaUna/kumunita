using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C3-03 — the extracted <see cref="IdentityPlatformGateService"/> seam (the
/// 12 admin-settled platform-gate + sample-data-lock methods that left
/// <c>IdentityService</c>), pinned over a fresh scratch Postgres
/// (<see cref="PostgresFixture"/> harness). The seam is constructed directly —
/// <c>documentStore</c> is the real one; <c>sampleDataOptions</c> is <c>null</c>
/// (so the sample-data lock is off); <c>userInfo</c> / <c>userManager</c> are
/// unused by the three gate lanes exercised here (the <c>null!</c> reference is
/// never dereferenced). The seam's own <see cref="IIdentityPlatformGateService"
/// /> surface is what this test targets — the seam-test rule-2 gate reads this
/// type name.
/// </summary>
public class IdentityPlatformGateServiceSeamTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── The `false` floor — a fresh instance ships invitation-only (ADR 0050) ──

    [Fact]
    public async Task IsSignupOpen_FreshInstance_FloorsToFalse()
    {
        var (svc, _) = await BuildAsync();
        Assert.False(await svc.IsSignupOpenAsync());
    }

    // ── SetSignupOpen: exactly one audit row (via Admin) + live on next read ──

    [Fact]
    public async Task SetSignupOpen_WritesAdminAuditAndIsLiveOnNextRead()
    {
        const string actor = "seam-gate-admin";
        var (svc, store) = await BuildAsync();

        Assert.False(await svc.IsSignupOpenAsync()); // the floor before the set
        await svc.SetSignupOpenAsync(true, actor);

        Assert.True(await svc.IsSignupOpenAsync()); // live on the next read

        var rows = await AuditRows(store, action: "signup.set-open");
        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(actor, row.ActorId);
        Assert.Equal(AccessVia.Admin, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
        Assert.Equal("signup", row.TargetKind);
    }

    // ── Sample-data lock is OFF when no options are supplied (the null floor) ──

    [Fact]
    public async Task IsSampleDataEnabled_NoOptions_FloorsToFalse()
    {
        var (svc, _) = await BuildAsync();
        Assert.False(await svc.IsSampleDataEnabledAsync());
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A real Marten store over a fresh scratch Postgres (the
    /// <see cref="PostgresFixture"/> harness) + an
    /// <see cref="IdentityPlatformGateService"/> wired with a
    /// <c>null</c> sample-data options (the lock is off), a substitute
    /// <c>userInfo</c>, and a <c>null!</c> userManager (unused by these lanes).
    /// </summary>
    private async Task<(IIdentityPlatformGateService svc, IDocumentStore store)> BuildAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);

        var svc = new IdentityPlatformGateService(
            documentStore: store,
            sampleDataOptions: null,
            userInfo: Substitute.For<IUserInfoService>(),
            userManager: null!);

        return (svc, store);
    }

    private static async Task<IReadOnlyList<AccessAudit>> AuditRows(
        IDocumentStore store, string? action = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        System.Linq.IQueryable<AccessAudit> query = session.Query<AccessAudit>();
        if (action is not null)
            query = query.Where(a => a.Action == action);
        return await query.OrderBy(a => a.At).ToListAsync(ct);
    }
}
