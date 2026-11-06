using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Inventory</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The Inventory
/// bounded context's service seam (M16, ADR 0117). Composes **only** the frozen
/// seams (D8 / C-M16·6): IDocumentStore + IUserInfoService + IAuthorizationService.
/// Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class InventoryModule
{
    /// <summary>Registers the <c>Inventory</c> bounded context's seam.</summary>
    public static IServiceCollection AddInventory(this IServiceCollection services)
    {
        services.AddTransient<Inventory.IInventoryService>(sp => new Inventory.InventoryService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IAuthorizationService>(),
            sp.GetRequiredService<IUserInfoService>()));

        return services;
    }
}
