using Kumunita.Core.Authorization;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Documents</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The documents-side
/// composition root (M21, ADR 0122). Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class DocumentsModule
{
    /// <summary>Registers the <c>Documents</c> bounded context's seams.</summary>
    public static IServiceCollection AddDocuments(this IServiceCollection services)
    {
        services.AddTransient<Documents.DocumentService>(sp => new Documents.DocumentService(
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<IAuthorizationService>(),
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<ITagService>()));

        services.AddTransient<Documents.DocumentFolderService>(sp => new Documents.DocumentFolderService(
            sp.GetRequiredService<Marten.IDocumentStore>()));

        return services;
    }
}
