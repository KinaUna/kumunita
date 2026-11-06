using Kumunita.Core.Moderation;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Moderation</c> bounded context's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The moderation
/// service (report triage / actions). Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class ModerationModule
{
    /// <summary>Registers the <c>Moderation</c> bounded context's seam.</summary>
    public static IServiceCollection AddModeration(this IServiceCollection services)
    {
        // Factory preserves the composition root's explicit optional-dependency
        // resolution (INotificationEmitter / IUserInfoService resolve to null
        // when unregistered — the C03 emitter seam + the user-info seam).
        services.AddTransient<IModerationService>(sp => new ModerationService(
            sp.GetRequiredService<Core.UserInfo.IProfileSettingsService>(),
            sp.GetRequiredService<Core.Authorization.IAuthorizationService>(),
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetService<Core.Notifications.INotificationEmitter>(),
            sp.GetService<Core.UserInfo.IUserInfoService>()));
        return services;
    }
}
