using Kumunita.Authorization.Infrastructure;
using Kumunita.Authorization.Infrastructure.SeedData;
using Marten;
using Marten.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Authorization;

public static class AuthorizationModule
{
    public static IServiceCollection AddAuthorizationModule(
        this IServiceCollection services)
    {
        // No additional services needed beyond Wolverine and Marten
        // All handlers are discovered by Wolverine convention
        services.AddSingleton<IInitialData, DefaultVisibilityPoliciesSeed>();

        return services;
    }

    public static StoreOptions AddAuthorizationSchema(this StoreOptions opts)
        => opts.AddAuthorizationMartenSchema();
}