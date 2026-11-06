using Kumunita.Core.Tags;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Tags</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The tag service
/// (the tag catalog — the ML lane's tag translation surface, ADR 0021
/// closed-set doctrine). Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class TagsModule
{
    /// <summary>Registers the <c>Tags</c> bounded context's seam.</summary>
    public static IServiceCollection AddTags(this IServiceCollection services)
    {
        services.AddTransient<ITagService>(sp => new TagService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<Core.Authorization.IAuthorizationService>(),
            sp.GetRequiredService<Core.Localization.ITranslationProvider>()));
        return services;
    }
}
