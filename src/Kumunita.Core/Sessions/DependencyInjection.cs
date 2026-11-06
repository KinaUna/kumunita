using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Sessions</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The session
/// service (the sealed, audit-kind-carrying seam pinned by
/// <c>session-service-sealed-and-audit-kind</c>). Core stays HTTP-free (ADR
/// 0006-D).
/// </summary>
public static class SessionsModule
{
    /// <summary>Registers the <c>Sessions</c> bounded context's seam.</summary>
    public static IServiceCollection AddSessions(this IServiceCollection services)
    {
        services.AddTransient<Sessions.SessionService>(sp => new Sessions.SessionService(
            sp.GetRequiredService<Marten.IDocumentStore>()));
        // IMPROVE-CODE-2 C2-03 — the crossing modules (the SecuritySettings
        // controller, the Program sign-in handler, the SessionRevocation
        // middleware) code against the ISessionService interface (the C03
        // INotificationEmitter pattern; ADR 0006-D "route + authz + shape").
        services.AddTransient<Sessions.ISessionService>(sp => sp.GetRequiredService<Sessions.SessionService>());
        return services;
    }
}
