using Kumunita.Core.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Authorization</c> bounded context's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The frozen
/// ADR 0006-D read-decision path — "may this actor see that resource?" is
/// resolved per request (thin token, fat authorization), never an identity
/// claim. The <c>AuditPurgeOptions</c> is the OPS §8 §6.4 per-instance purge
/// tiering (config, not improvised).
/// </summary>
public static class AuthorizationModule
{
    /// <summary>
    /// Registers the <c>Authorization</c> bounded context's domain service.
    /// Named <c>AddKumunitaAuthorization</c> (not <c>AddAuthorization</c>) to
    /// avoid colliding with ASP.NET's <c>PolicyServiceCollectionExtensions
    /// .AddAuthorization</c> — both live in <c>Kumunita.Core</c>'s import scope
    /// at the host call site (Program.cs), so the framework method must remain
    /// unambiguous there.
    /// </summary>
    public static IServiceCollection AddKumunitaAuthorization(this IServiceCollection services)
    {
        services.AddTransient<IAuthorizationService, AuthorizationService>();

        // Step-7 (M1 plan §6.4): the AuditPurge tiering is per-instance
        // config, not improvised (§5: "the purge decision ... is set in the
        // job's config").
        services.AddOptions<Authorization.AuditPurgeOptions>();

        return services;
    }
}
