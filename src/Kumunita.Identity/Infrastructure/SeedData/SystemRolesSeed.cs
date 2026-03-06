using Kumunita.Identity.Domain;
using Marten;
using Marten.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Identity.Infrastructure.SeedData;

public class SystemRolesSeed : IInitialData
{
    private readonly IServiceProvider _services;

    public SystemRolesSeed(IServiceProvider services)
        => _services = services;

    public async Task Populate(IDocumentStore store, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<AppRole>>();

        foreach (var roleName in new[]
        {
            AppRole.SystemRoles.Admin,
            AppRole.SystemRoles.Moderator,
            AppRole.SystemRoles.Member,
            AppRole.SystemRoles.Guest
        })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new AppRole
                {
                    Name = roleName,
                    IsSystem = true
                });
        }
    }
}
