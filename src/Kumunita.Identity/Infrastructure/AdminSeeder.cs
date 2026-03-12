using Kumunita.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kumunita.Identity.Infrastructure;

/// <summary>
/// Idempotent startup task that:
/// <list type="bullet">
///   <item>Creates the four system roles (Admin, Moderator, Member, Guest) if absent.</item>
///   <item>Creates the initial admin account when no Admin role member exists yet.</item>
/// </list>
/// The admin account has <see cref="AppUser.MustChangePassword"/> set to <see langword="true"/>
/// so the user is forced to choose a new password on first login.
/// </summary>
internal sealed class AdminSeeder(
    IServiceProvider serviceProvider,
    IOptions<AdminSeedOptions> options,
    ILogger<AdminSeeder> logger) : BackgroundService
{
    /// <summary>
    /// Runs the seeding logic. Called explicitly during app startup (before the server
    /// begins accepting requests) so roles and the admin account are ready for the
    /// first login attempt.
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        AdminSeedOptions opts = options.Value;

        if (!opts.IsConfigured)
        {
            logger.LogWarning(
                "AdminSeeder: 'Kumunita:InitialAdmin' section is not configured — " +
                "skipping initial admin seed. Set Email and Password to enable it.");
            return;
        }

        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();

        RoleManager<AppRole>  roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        UserManager<AppUser>  userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        await EnsureSystemRolesAsync(roleManager);
        await EnsureAdminUserAsync(userManager, opts);
    }

    // Kept for compatibility — delegates to SeedAsync so the class can still be
    // registered as a hosted service if needed in the future.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => SeedAsync(stoppingToken);

    // ── Roles ────────────────────────────────────────────────────────────────

    private async Task EnsureSystemRolesAsync(RoleManager<AppRole> roleManager)
    {
        string[] systemRoles =
        [
            AppRole.SystemRoles.Admin,
            AppRole.SystemRoles.Moderator,
            AppRole.SystemRoles.Member,
            AppRole.SystemRoles.Guest,
        ];

        foreach (string roleName in systemRoles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            IdentityResult result = await roleManager.CreateAsync(new AppRole
            {
                Name     = roleName,
                IsSystem = true,
            });

            if (result.Succeeded)
                logger.LogInformation("AdminSeeder: created system role '{Role}'", roleName);
            else
                logger.LogError(
                    "AdminSeeder: failed to create role '{Role}': {Errors}",
                    roleName,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    // ── Admin user ───────────────────────────────────────────────────────────

    private async Task EnsureAdminUserAsync(UserManager<AppUser> userManager, AdminSeedOptions opts)
    {
        // Bail out early — an admin was already seeded (or created manually).
        IList<AppUser> existingAdmins = await userManager.GetUsersInRoleAsync(AppRole.SystemRoles.Admin);
        if (existingAdmins.Count > 0)
        {
            logger.LogDebug("AdminSeeder: admin user already exists — skipping.");
            return;
        }

        AppUser admin = new()
        {
            UserName          = string.IsNullOrWhiteSpace(opts.UserName) ? opts.Email : opts.UserName,
            Email             = opts.Email,
            EmailConfirmed    = true,   // pre-confirm so RequireConfirmedEmail doesn't block first login
            MustChangePassword = true,  // redirect to change-password page after first login
        };

        IdentityResult createResult = await userManager.CreateAsync(admin, opts.Password);

        if (!createResult.Succeeded)
        {
            logger.LogError(
                "AdminSeeder: failed to create admin user '{Email}': {Errors}",
                opts.Email,
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
            return;
        }

        IdentityResult roleResult = await userManager.AddToRoleAsync(admin, AppRole.SystemRoles.Admin);

        if (roleResult.Succeeded)
            logger.LogInformation(
                "AdminSeeder: admin account '{Email}' created with MustChangePassword=true",
                opts.Email);
        else
            logger.LogError(
                "AdminSeeder: admin user created but role assignment failed: {Errors}",
                string.Join(", ", roleResult.Errors.Select(e => e.Description)));
    }
}