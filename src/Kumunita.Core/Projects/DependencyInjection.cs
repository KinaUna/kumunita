using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Mentions;
using Kumunita.Core.Notifications;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Projects</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The Projects
/// bounded context's service seams (M5, ADR 0067) — IProjectService,
/// IBoardService (C08 split, ADR 0287), ITodoService (C08 split). Each composes
/// the same frozen seams. Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class ProjectsModule
{
    /// <summary>Registers the <c>Projects</c> bounded context's seams.</summary>
    public static IServiceCollection AddProjects(this IServiceCollection services)
    {
        services.AddTransient<Projects.IProjectService>(sp => new Projects.ProjectService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IAuthorizationService>(),
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<INotificationEmitter>(),
            sp.GetRequiredService<ITranslationProvider>(),
            sp.GetRequiredService<ILocalizationService>(),
            sp.GetRequiredService<ITagService>(),
            sp.GetRequiredService<IMentionService>()));

        // IMPROVE-CODE C08 (ADR 0287): the Board and Todo sibling seams, split
        // from IProjectService — each composes the same frozen seams.
        services.AddTransient<Projects.IBoardService>(sp => new Projects.BoardService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IAuthorizationService>(),
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<INotificationEmitter>(),
            sp.GetRequiredService<ITranslationProvider>(),
            sp.GetRequiredService<ILocalizationService>(),
            sp.GetRequiredService<ITagService>(),
            sp.GetRequiredService<IMentionService>()));
        services.AddTransient<Projects.ITodoService>(sp => new Projects.TodoService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IAuthorizationService>(),
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<INotificationEmitter>(),
            sp.GetRequiredService<ITranslationProvider>(),
            sp.GetRequiredService<ILocalizationService>(),
            sp.GetRequiredService<ITagService>(),
            sp.GetRequiredService<IMentionService>()));

        return services;
    }
}
