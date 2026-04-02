using Kumunita.Identity.Domain;
using Kumunita.Identity.Infrastructure;
using Marten;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kumunita.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        // EF Core + ASP.NET Core Identity
        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("kumunitadb"));

            // Required by OpenIddict
            options.UseOpenIddict();
        });

        services.AddIdentity<AppUser, AppRole>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireNonAlphanumeric = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        // OpenIddict
        services.AddOpenIddictForIdentity(configuration, environment);

        // Registered as singletons (not hosted services) so they can be resolved and
        // called explicitly before app.Run() — ensuring seeding completes before the
        // server accepts its first request. See IdentityModule.SeedIdentityAsync.
        services.AddSingleton<OpenIddictSeeder>();

        services.Configure<AdminSeedOptions>(
            configuration.GetSection(AdminSeedOptions.Section));
        services.AddSingleton<AdminSeeder>();

        return services;
    }

    /// <summary>
    /// Seeds the OpenIddict client registration and the initial admin account
    /// <strong>synchronously before the server starts accepting requests</strong>.
    /// Call this after applying EF Core migrations and before <c>app.Run()</c> to
    /// guarantee that the OIDC client exists when the first browser request arrives.
    /// </summary>
    public static async Task SeedIdentityAsync(this WebApplication app)
    {
        await app.Services.GetRequiredService<OpenIddictSeeder>().SeedAsync();
        await app.Services.GetRequiredService<AdminSeeder>().SeedAsync();
    }

    public static Marten.StoreOptions AddIdentitySchema(this Marten.StoreOptions opts)
        => opts.AddIdentityMartenSchema(); // calls IdentityMartenConfiguration
}