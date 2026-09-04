using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Bootstrap;

/// <summary>
/// Versioned schema steps apply on boot in ALL environments (ADR 0004 B, OPS.md §2/§3):
/// a pristine database gets its initial state with no operator step (a first boot is
/// also the first seeder run), an existing one is a no-op, or forward-only when the
/// image carries new steps. Document-shape auto-creation remains the dev-only loop
/// in <c>Program.cs</c> (ADR 0004).
/// </summary>
public static class SchemaBootstrap
{
    /// <summary>
    /// Applies the Marten versioned storage-feature steps (<c>mt</c> schema) and the EF Core
    /// Identity migrations (<c>identity</c> schema), then — and only on a pristine DB —
    /// runs the first-boot seeder (M1 step 6, plan: community row, seed admin, default
    /// components, language catalog, first-boot email). Resolves its services in a
    /// scope: <see cref="AppDbContext"/> is scoped, so it cannot be resolved from the
    /// root provider.
    /// </summary>
    public static async Task ApplyAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var logger = sp.GetRequiredService<ILogger>();
        var store = sp.GetRequiredService<IDocumentStore>();
        var identity = sp.GetRequiredService<AppDbContext>();

        var firstBoot = await DbBootstrap.IsPristineAsync(identity, ct);

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync();
        await identity.Database.MigrateAsync(ct);

        if (firstBoot)
        {
            // Resolve the seeder's dependencies from the same scope: the seeder is the
            // only writer that may see the pristine schemas, and the plan's step 6 pins
            // these five steps to the first-boot lane.
            var userManager = sp.GetRequiredService<UserManager<User>>();
            var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
            var userInfo = sp.GetRequiredService<IUserInfoService>();
            var mailer = sp.GetRequiredService<IMailerStage>();
            var community = sp.GetRequiredService<IOptions<CommunityOptions>>().Value;
            var seedAdmin = sp.GetRequiredService<IOptions<SeedAdminOptions>>().Value;
            var verification = sp.GetRequiredService<IOptions<VerificationOptions>>().Value;
            // The seeder is static: log under the caller's category (the generic ILogger
            // with the type as an argument, not a type-argument — static types can't
            // be type arguments).
            var seededLogger = sp.GetRequiredService<ILogger>();

            logger.LogInformation("First boot: schema initialization complete. "
                                  + "Running the first-boot seeder (plan M1 step 6). ");

            await FirstBootSeeder.SeedAsync(
                identity, store, userManager, roleManager, userInfo, mailer,
                community,
                seedAdmin,
                verification.TtlDays,
                seededLogger,
                ct);
        }
    }
}
