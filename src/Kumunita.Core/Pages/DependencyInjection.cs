using Kumunita.Core.Mentions;
using Kumunita.Core.Notifications;
using Kumunita.Core.Tags;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Pages</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The pages-side
/// service seam (PG, ADR 0039). Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class PagesModule
{
    /// <summary>Registers the <c>Pages</c> bounded context's seam.</summary>
    public static IServiceCollection AddPages(this IServiceCollection services)
    {
        services.AddTransient<Pages.IPageService>(sp => new Pages.PageService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<ITagService>(),
            sp.GetRequiredService<INotificationEmitter>(),
            sp.GetRequiredService<IMentionService>()));

        return services;
    }
}
