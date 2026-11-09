using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// IS26 — the extracted break-glass probe (ADR 0299).
/// <para>
/// <see cref="BreakGlassProbe.HasActiveAsync"/> is now the single source of
/// truth for "an active override" (a consumed, non-expired
/// <c>mt."AdminOverride"</c> row). These tests pin the three-way
/// consumed/expired matrix directly against the probe, reusing the hand-rolled
/// table's boot shape (the raw operator-style insert mirrors the OPS §9 path).
/// The behaviour is identical to what <see cref="AuthorizationService"/>
/// delegates to — see the sibling <c>BreakGlass_*</c> seam tests in
/// <see cref="AuthorizationServiceTests"/>.
/// </para>
/// </summary>
public class BreakGlassProbeTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Probe_ActiveOverride_True()
    {
        var (store, connString) = await BootAsync();
        const string user = "u-probe-active";
        var now = DateTimeOffset.UtcNow;

        // Consumed and unexpired → an active override.
        await SeedAdminOverrideAsync(connString, user, "tok-probe-1",
            grantedAt: now.AddHours(-2), expiresAt: now.AddHours(+2),
            consumedAt: now.AddHours(-1));

        await using var session = store.QuerySession();
        var active = await BreakGlassProbe.HasActiveAsync(session, user, TestContext.Current.CancellationToken);
        Assert.True(active);
    }

    [Fact]
    public async Task Probe_Expired_False()
    {
        var (store, connString) = await BootAsync();
        const string user = "u-probe-expired";
        var now = DateTimeOffset.UtcNow;

        // Consumed but past expiry → no active override.
        await SeedAdminOverrideAsync(connString, user, "tok-probe-2",
            grantedAt: now.AddDays(-30), expiresAt: now.AddDays(-1),
            consumedAt: now.AddDays(-29));

        await using var session = store.QuerySession();
        var active = await BreakGlassProbe.HasActiveAsync(session, user, TestContext.Current.CancellationToken);
        Assert.False(active);
    }

    [Fact]
    public async Task Probe_NotConsumed_False()
    {
        var (store, connString) = await BootAsync();
        const string user = "u-probe-unconsumed";
        var now = DateTimeOffset.UtcNow;

        // Unconsumed (consumedAt NULL) — a grant not yet consumed is not an
        // active elevation, so the probe is false.
        await SeedAdminOverrideAsync(connString, user, "tok-probe-3",
            grantedAt: now.AddHours(-2), expiresAt: now.AddHours(+2),
            consumedAt: null);

        await using var session = store.QuerySession();
        var active = await BreakGlassProbe.HasActiveAsync(session, user, TestContext.Current.CancellationToken);
        Assert.False(active);
    }

    // ── Shared bootstrap: store + connection string ──────────────────────

    private async Task<(IDocumentStore, string)> BootAsync()
    {
        var connString = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(connString);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return (store, connString);
    }

    private static async Task SeedAdminOverrideAsync(
        string connString, string userId, string token,
        DateTimeOffset grantedAt, DateTimeOffset expiresAt,
        DateTimeOffset? consumedAt)
    {
        // The AdminOverride table is hand-rolled (AuthorizationFeature /
        // ADR 0004 §B.1) — the operator writes it through psql (OPS §9), so
        // the test mirrors that path with a raw Npgsql insert on a fresh
        // connection (no session required — no document to track).
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO \"mt\".\"AdminOverride\" " +
            "  (\"id\", \"userId\", \"token\", \"grantedAt\", \"expiresAt\", \"consumedAt\") " +
            "VALUES (@id, @userId, @token, @grantedAt, @expiresAt, @consumedAt)";

        static void Add(NpgsqlCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }

        Add(cmd, "@id", Guid.NewGuid().ToString("N"));
        Add(cmd, "@userId", userId);
        Add(cmd, "@token", token);
        Add(cmd, "@grantedAt", grantedAt);
        Add(cmd, "@expiresAt", expiresAt);
        if (consumedAt is null)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = "@consumedAt";
            p.NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.TimestampTz;
            p.Value = DBNull.Value;
            cmd.Parameters.Add(p);
        }
        else
        {
            Add(cmd, "@consumedAt", consumedAt);
        }

        await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
