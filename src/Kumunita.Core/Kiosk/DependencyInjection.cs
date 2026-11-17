using Kumunita.Core.Kiosk;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Kiosk</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The kiosk
/// service (the dedicated read-only display surface). Core stays HTTP-free
/// (ADR 0006-D).
/// </summary>
public static class KioskModule
{
    /// <summary>Registers the <c>Kiosk</c> bounded context's seam.</summary>
    public static IServiceCollection AddKiosk(this IServiceCollection services)
    {
        services.AddTransient<IKioskService>(sp => new KioskService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<Core.Authorization.IAuthorizationService>()));
        // ADR 0302 (IMPROVE-UX R15) — the shared kiosk link's Core seam
        // (create / revoke / validate). Pure Core — the R16 middleware and
        // the R18 settings surface resolve it (the IKioskService precedent
        // above; the KioskLinkService carries no IAuthorizationService —
        // the owner-scope check is data-scoped, not a Decide() branch).
        services.AddTransient<IKioskLinkService, KioskLinkService>();
        return services;
    }
}
