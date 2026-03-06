using Kumunita.Identity.Domain;
using Kumunita.Identity.Infrastructure;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
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
        services.AddOpenIddictForIdentity(configuration);

        // Custom claims enrichment
        services.AddSingleton<CustomClaimsHandler>();

        return services;
    }

    public static Marten.StoreOptions AddIdentitySchema(this Marten.StoreOptions opts)
        => opts.AddIdentityMartenSchema(); // calls IdentityMartenConfiguration
}