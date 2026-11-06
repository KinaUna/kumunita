using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Notifications</c> bounded context's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The notifications
/// service (M6, ADR 0076) + the bounce/complaint suppression write + lookup seam
/// (F17, MAIL-03) + the C03 <c>INotificationEmitter</c> emission contract
/// (a separate, module-local extension so the C03 registration does not grow
/// the baselined method). Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class NotificationsModule
{
    /// <summary>Registers the <c>Notifications</c> bounded context's seams.</summary>
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        services.AddTransient<Notifications.NotificationService>(sp => new Notifications.NotificationService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<ITranslationProvider>(),
            sp.GetRequiredService<IMailerStage>(),
            sp.GetService<Microsoft.Extensions.Options.IOptions<Notifications.NotificationOptions>>(),
            // C3-07 (ADR 0297) — the instance base URL via the IInstanceBaseUrlReader
            // read contract (the Identity module owns + registers the seam), so this
            // module codes against a contract, not the concrete VerificationOptions.
            sp.GetService<IInstanceBaseUrlReader>(),
            sp.GetRequiredService<ILocalizationService>()));

        services.AddTransient<Notifications.ISuppressionService>(sp => new Notifications.SuppressionService(
            sp.GetRequiredService<Marten.IDocumentStore>()));

        return services;
    }

    /// <summary>
    /// C03 (IMPROVE-CODE lane) — the <c>INotificationEmitter</c> emission
    /// contract (D6: cross-module callers resolve the interface, never the
    /// concrete <see cref="Notifications.NotificationService"/>). Registered as
    /// a **separate, module-local extension** (not inlined into
    /// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>) so the C03
    /// registration does not grow the baselined method.
    /// </summary>
    public static IServiceCollection AddNotificationEmitter(this IServiceCollection services)
    {
        services.AddTransient<Notifications.INotificationEmitter>(
            sp => sp.GetRequiredService<Notifications.NotificationService>());
        return services;
    }
}
