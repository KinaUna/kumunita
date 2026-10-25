using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// F22 (DB-18, ADR 0171) — the enforcement witness for the two-role split: the
/// runtime APP role is demoted to DML and holds no schema-level DDL, while the
/// MIGRATION role keeps it. This is the Postgres-privilege-layer proof that the
/// least-privilege property is real (not just asserted in a doc) — the pin §5
/// test <c>AppRole_CannotDropSchema</c>.
/// </summary>
/// <para>
/// The fixture's <c>kumunita</c> role is the Testcontainers bootstrap superuser, so
/// here the *app* role is represented by a distinct non-owner role (<c>kumunita_app</c>)
/// and the *migration* role by <c>kumunita_migrate</c> — the same shape the prod
/// <c>dev-db-init</c> provisioning produces (ADR 0171: <c>kumunita_migrate</c> owns
/// the DB + the <c>mt</c> schema; the app role holds CONNECT + CREATE on <c>mt</c>
/// only — enough to create the Wolverine/Marten tables it owns at boot, NOT enough
/// to create/own/drop the schema or the database).
/// </para>
/// <list type="bullet">
/// <item><b>Refused (SQLSTATE 42501 insufficient_privilege):</b> the app role cannot
/// <c>CREATE SCHEMA</c> (permission denied for database), cannot <c>DROP SCHEMA mt</c>
/// (must be owner of schema), and cannot <c>DROP DATABASE</c> (must be owner of
/// database). This is the pin's named assertion + the symmetric CREATE/DROP pairs.</item>
/// <item><b>Allowed (the "didn't break the app" half):</b> the app role CAN
/// <c>CREATE TABLE</c> in <c>mt</c> + do DML (<c>INSERT</c>/<c>SELECT</c>) on it —
/// schema-level CREATE + table DML are intact (the Wolverine <c>wolverine_*</c> +
/// Marten <c>mt_doc_*</c> tables it creates at StartAsync).</item>
/// <item><b>Witness (capability moved, not removed):</b> the migration role CAN
/// <c>CREATE SCHEMA</c> — the DDL-creation capability still exists, on the role
/// that needs it (the boot schema-apply / operator restore role).</item>
/// </list>
/// </summary>
public sealed class AppRoleCannotDropSchemaTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string MigrateRole = "kumunita_migrate";
    private const string AppRole = "kumunita_app";
    private const string Password = "f22-test-pass";

    [Fact]
    public async Task AppRole_CannotDropSchema()
    {
        var bootDb = fixture.BootConnectionString;
        var db = "kumunita_f22_" + Guid.NewGuid().ToString("n")[..10];

        // Provision the two roles + a scratch DB owned by the migration role (superuser).
        await ExecAsync(bootDb, $"CREATE ROLE {MigrateRole} LOGIN PASSWORD '{Password}';");
        await ExecAsync(bootDb, $"CREATE ROLE {AppRole} LOGIN PASSWORD '{Password}';");
        await ExecAsync(bootDb, $"CREATE DATABASE {db} OWNER {MigrateRole};");

        var appCs = SwapDb(bootDb, db, AppRole, Password);
        var migCs = SwapDb(bootDb, db, MigrateRole, Password);

        try
        {
            // Pre-create `mt` owned by the migration role; grant the app role CREATE on
            // it (the ADR 0171 forced exception — the app creates the wolverine_*/mt_doc_*
            // tables it owns at StartAsync).
            await ExecAsync(migCs, $"CREATE SCHEMA mt AUTHORIZATION {MigrateRole};");
            await ExecAsync(migCs, $"GRANT USAGE, CREATE ON SCHEMA mt TO {AppRole};");

            // ── REFUSED: the app role holds no schema-level DDL (SQLSTATE 42501) ──
            // (1) CREATE SCHEMA — permission denied for database (no DB-level CREATE).
            await AssertSqlStateAsync(appCs, "42501", "CREATE SCHEMA f22_new;");
            // (2) DROP SCHEMA mt — must be owner of schema (app does not own mt).
            await AssertSqlStateAsync(appCs, "42501", "DROP SCHEMA mt;");
            // (3) DROP DATABASE — must be owner of database (app is not the DB owner).
            await AssertSqlStateAsync(appCs, "42501", $"DROP DATABASE {db};");

            // ── ALLOWED: the app role still does DML (the "didn't break the app") ──
            // (4) schema-level CREATE on `mt` (create the table it owns at boot).
            await ExecAsync(appCs, "CREATE TABLE mt.f22_probe(i integer);");
            // (5) DML on its own table.
            await ExecAsync(appCs, "INSERT INTO mt.f22_probe VALUES (1), (2);");
            Assert.Equal(2, await ScalarLongAsync(appCs, "SELECT count(*) FROM mt.f22_probe;"));

            // ── WITNESS: the capability was moved to the migration role, not removed ──
            // (6) the migration role (DB owner) CAN create a schema.
            await ExecAsync(migCs, "CREATE SCHEMA f22_witness;");
            Assert.True(await ScalarBoolAsync(migCs,
                "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'f22_witness');"));

            // (7) the app role is NOT the database owner (the DB-18 FAIL, closed).
            var dbOwner = await ScalarStringAsync(appCs,
                "SELECT r.rolname FROM pg_database d JOIN pg_roles r ON r.oid = d.datdba WHERE d.datname = current_database();");
            Assert.Equal(MigrateRole, dbOwner);
            Assert.NotEqual(AppRole, dbOwner);
        }
        finally
        {
            // Tear down (as the fixture superuser).
            await ExecAsync(bootDb, $"DROP DATABASE IF EXISTS {db};");
            await ExecAsync(bootDb, $"DROP ROLE IF EXISTS {AppRole};");
            await ExecAsync(bootDb, $"DROP ROLE IF EXISTS {MigrateRole};");
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static string SwapDb(string cs, string db, string user, string password)
        => new NpgsqlConnectionStringBuilder(cs)
        {
            Database = db,
            Username = user,
            Password = password,
            // No pooling: pooled connections to the scratch DB would stay open after
            // `await using` disposes them, and `DROP DATABASE` in teardown would then
            // fail with 55006 "being accessed by other users".
            Pooling = false,
        }.ToString();

    private static async Task ExecAsync(string cs, string sql)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarLongAsync(string cs, string sql)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (long)(await cmd.ExecuteScalarAsync()!);
    }

    private static async Task<bool> ScalarBoolAsync(string cs, string sql)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (bool)(await cmd.ExecuteScalarAsync()!);
    }

    private static async Task<string> ScalarStringAsync(string cs, string sql)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (string)(await cmd.ExecuteScalarAsync()!);
    }

    /// <summary>Assert a statement fails with SQLSTATE <paramref name="expected"/> (insufficient_privilege).</summary>
    private static async Task AssertSqlStateAsync(string cs, string expected, string sql)
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        var ex = await Assert.ThrowsAnyAsync<NpgsqlException>(() => cmd.ExecuteNonQueryAsync());

        // The Postgres error surfaces as the innermost NpgsqlException.
        var postgre = ex.InnerException is NpgsqlException inner ? inner : ex;
        Assert.Equal(expected, postgre.SqlState);
    }
}
