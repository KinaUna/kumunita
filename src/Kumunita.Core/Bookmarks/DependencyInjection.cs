using Kumunita.Core.Targets;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Bookmarks</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The bookmarks-side
/// service seam (M17, ADR 0118). Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class BookmarksModule
{
    /// <summary>Registers the <c>Bookmarks</c> bounded context's seam.</summary>
    public static IServiceCollection AddBookmarks(this IServiceCollection services)
    {
        services.AddTransient<Bookmarks.IBookmarkService>(sp => new Bookmarks.BookmarkService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IContentTargetResolver>()));

        return services;
    }
}
