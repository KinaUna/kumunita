using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Kumunita.Identity.Infrastructure;

/// <summary>
/// Used exclusively by EF Core design-time tooling (<c>dotnet ef migrations add</c>, etc.).
/// Never instantiated at runtime — the DI-registered <see cref="IdentityDbContext"/> is used instead.
///
/// The connection string is resolved in priority order:
///   1. <c>ConnectionStrings__kumunitadb</c> environment variable (set this to your Aspire DB URL)
///   2. Hard-coded local-Postgres fallback for zero-config developer machines
/// </summary>
internal sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        string connectionString =
            config.GetConnectionString("kumunitadb")
            ?? "Host=localhost;Port=5432;Database=kumunita;Username=postgres;Password=postgres";

        DbContextOptionsBuilder<IdentityDbContext> optionsBuilder = new();

        optionsBuilder
            .UseNpgsql(connectionString)
            .UseOpenIddict();

        return new IdentityDbContext(optionsBuilder.Options);
    }
}