using Kumunita.Core.Mentions;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Messaging</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The messaging-side
/// service seam (M9, ADR 0091). Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class MessagingModule
{
    /// <summary>Registers the <c>Messaging</c> bounded context's seam.</summary>
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddTransient<Messaging.IMessagingService>(sp => new Messaging.MessagingService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<INotificationEmitter>(),
            sp.GetRequiredService<IMentionService>()));

        return services;
    }
}
