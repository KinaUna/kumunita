using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Inventory;
using Kumunita.Core.Projects;
using Kumunita.Core.Targets;
using Kumunita.Core.UserInfo;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Targets</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The single
/// content-target resolution seam (IMPROVE-CODE C05, ADR 0118 D3/D5 +
/// ADR 0167 D1/D7) — replaces the four private clones (BookmarkService and
/// PersonalNoteService twins). Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class TargetsModule
{
    /// <summary>Registers the <c>Targets</c> bounded context's seam.</summary>
    public static IServiceCollection AddTargets(this IServiceCollection services)
    {
        services.AddTransient<IContentTargetResolver>(sp => new ContentTargetResolver(
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<IGroupService>(),
            sp.GetRequiredService<IAuthorizationService>(),
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IAnnouncementService>(),
            sp.GetRequiredService<IEventService>(),
            sp.GetRequiredService<IProjectService>(),
            sp.GetRequiredService<IBoardService>(),
            sp.GetRequiredService<ITodoService>(),
            sp.GetRequiredService<IInventoryService>(),
            sp.GetRequiredService<IIdentityService>()));

        return services;
    }
}
