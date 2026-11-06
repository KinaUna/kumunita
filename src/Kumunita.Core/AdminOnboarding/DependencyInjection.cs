using Kumunita.Core.AdminOnboarding;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>AdminOnboarding</c> bounded context's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The admin
/// onboarding lane (first-admin bootstrap / setup flow). Core stays HTTP-free
/// (ADR 0006-D).
/// </summary>
public static class AdminOnboardingModule
{
    /// <summary>Registers the <c>AdminOnboarding</c> bounded context's seam.</summary>
    public static IServiceCollection AddAdminOnboarding(this IServiceCollection services)
    {
        services.AddTransient<IAdminOnboardingService>(sp => new AdminOnboardingService(
            sp.GetRequiredService<Marten.IDocumentStore>()));
        return services;
    }
}
