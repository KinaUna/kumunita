using Kumunita.Core.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Events</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The event
/// service (community events — the C08 split from the projects cluster). Core
/// stays HTTP-free (ADR 0006-D).
/// </summary>
public static class EventsModule
{
    /// <summary>Registers the <c>Events</c> bounded context's seam.</summary>
    public static IServiceCollection AddEvents(this IServiceCollection services)
    {
        services.AddTransient<IEventService>(sp => new EventService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<Core.Authorization.IAuthorizationService>(),
            sp.GetRequiredService<Core.UserInfo.IUserInfoService>(),
            sp.GetRequiredService<Core.Notifications.INotificationEmitter>(),
            sp.GetRequiredService<Core.Mentions.IMentionService>()));
        return services;
    }
}
