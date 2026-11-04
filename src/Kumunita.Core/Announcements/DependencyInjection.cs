using Kumunita.Core.Announcements;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Announcements</c> feature module's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The announcement
/// service (the <c>PostDocTypes</c> feature module — not a top-level bounded
/// context, ADR 0017 edit-lane author-or-GlobalAdmin). Core stays HTTP-free
/// (ADR 0006-D).
/// </summary>
public static class AnnouncementsModule
{
    /// <summary>Registers the <c>Announcements</c> module's seam.</summary>
    public static IServiceCollection AddAnnouncements(this IServiceCollection services)
    {
        services.AddTransient<IAnnouncementService>(sp => new AnnouncementService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<Core.UserInfo.IUserInfoService>(),
            sp.GetRequiredService<Core.UserInfo.IProfileSettingsService>(),
            sp.GetRequiredService<Core.Notifications.INotificationEmitter>(),
            sp.GetRequiredService<Core.Mentions.IMentionService>()));
        return services;
    }
}
