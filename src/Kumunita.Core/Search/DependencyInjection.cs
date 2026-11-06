using Kumunita.Core.Search;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Search</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The search
/// service. Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class SearchModule
{
    /// <summary>Registers the <c>Search</c> bounded context's seam.</summary>
    public static IServiceCollection AddSearch(this IServiceCollection services)
    {
        services.AddTransient<ISearchService>(sp => new SearchService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<Core.Authorization.IAuthorizationService>(),
            sp.GetRequiredService<Core.UserInfo.IUserInfoService>(),
            sp.GetRequiredService<Core.UserInfo.IProfileSettingsService>()));
        return services;
    }
}
