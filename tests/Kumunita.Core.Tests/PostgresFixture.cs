using DotNet.Testcontainers.Containers;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Kumunita.Core.Tests;

/// <summary>
/// One shared postgres:18 container (matches the repo's docker-compose dev image) per test
/// class. Each test method is handed a connection string to a *fresh scratch database* so
/// no test clobbers another's schema state — critical because both <c>IsPristineAsync</c>
/// and <c>KumunitaFeature</c> read/modify the live Postgres catalog.
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
    /// A brand-new catalog is what lets <c>IsPristineAsync</c> return <c>true</c>
    /// and lets the DDL apply run as a true first boot.
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
