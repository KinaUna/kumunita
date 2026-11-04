using Kumunita.Core.Mentions;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>GroupMessaging</c> bounded context's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The group
/// messaging lane (the N:1 generalization of the M9 1:1 surface, ADR 0105).
/// Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class GroupMessagingModule
{
    /// <summary>Registers the <c>GroupMessaging</c> bounded context's seam.</summary>
    public static IServiceCollection AddGroupMessaging(this IServiceCollection services)
    {
        services.AddTransient<GroupMessaging.IGroupMessagingService>(sp => new GroupMessaging.GroupMessagingService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<INotificationEmitter>(),
            sp.GetRequiredService<IMentionService>()));

        return services;
    }
}
