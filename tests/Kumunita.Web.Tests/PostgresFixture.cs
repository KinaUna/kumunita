using DotNet.Testcontainers.Containers;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Kumunita.Web.Tests;

/// <summary>
/// GA U06 (ADR 0038) — one shared postgres:18 container (matches the repo's
/// docker-compose dev image) per test class. Each test method is handed a
/// connection string to a <em>fresh scratch database</em> so no test clobbers
/// another's schema state.
/// <para>
/// This is a <b>byte-for-byte copy</b> of the
/// <see cref="Kumunita.Core.Tests.PostgresFixture"/> in <c>Kumunita.Core.Tests</c>,
/// brought into this assembly because the 5 <c>Assign</c> Web tests are
/// <b>integration</b> tests (the <c>Assign</c> action's standing gate + the
/// happy-path <c>CreateGuardianLinkAsync</c> seam both run real Marten SQL —
/// Marten 9's <c>FirstOrDefaultAsync</c>/<c>ToListAsync</c> cast to the
/// internal <c>MartenLinqQueryable</c> and execute a live query, so they cannot
/// be NSubstituted — and the pinned tests assert <b>real</b> <c>GuardianLink</c>
/// + <c>guardian.create</c> audit rows). See the csproj note + the U06 drift
/// pause: the repo's "Web.Tests is NSubstitute-only, Testcontainers lives in
/// Core.Tests" convention is the seam gap this lane records.
/// </para>
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public PostgreSqlContainer Container =>
        _container ?? throw new InvalidOperationException("Postgres container not started (InitializeAsync not run).");

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("kumunita")
            .WithUsername("kumunita")
            .WithPassword("kumunita")
            .Build();

        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    /// <summary>Connection string to the boot database (used only for admin work like CREATE DATABASE).</summary>
    public string BootConnectionString => Container.GetConnectionString();

    /// <summary>
    /// Create a fresh scratch database and return a connection string targeting it.
    /// A brand-new catalog is what lets the DDL apply run as a true first boot.
    /// </summary>
    public async Task<string> NewDatabaseAsync(CancellationToken ct = default)
    {
        var name = "kumunita_test_" + Guid.NewGuid().ToString("n")[..10];

        await using var boot = new NpgsqlConnection(BootConnectionString);
        await boot.OpenAsync(ct);
        await using var cmd = boot.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE {name}";
        cmd.CommandTimeout = 30;
        await cmd.ExecuteNonQueryAsync(ct);

        // Same server/credentials, just swap the target database.
        var cs = new NpgsqlConnectionStringBuilder(BootConnectionString) { Database = name };
        return cs.ToString();
    }
}
