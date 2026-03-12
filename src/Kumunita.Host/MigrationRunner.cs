using Kumunita.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kumunita.Host;

public static class MigrationRunner
{
    public static async Task RunAsync(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Register only what's needed for migrations
        builder.Services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("kumunitadb")));

        WebApplication app = builder.Build();

        using IServiceScope scope = app.Services.CreateScope();
        IdentityDbContext db = scope.ServiceProvider
            .GetRequiredService<IdentityDbContext>();

        await db.Database.MigrateAsync();

        Console.WriteLine("EF Core migrations applied successfully.");
    }
}