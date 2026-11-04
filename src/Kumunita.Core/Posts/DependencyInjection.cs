using Kumunita.Core.Mentions;
using Kumunita.Core.Notifications;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Posts</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The posts-side
/// composition root (M3 U6). Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class PostsModule
{
    /// <summary>Registers the <c>Posts</c> bounded context's seam.</summary>
    public static IServiceCollection AddPosts(this IServiceCollection services)
    {
        services.AddTransient<Posts.PostService>(sp => new Posts.PostService(
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<Core.Authorization.IAuthorizationService>(),
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<ITagService>(),
            sp.GetRequiredService<INotificationEmitter>(),
            sp.GetRequiredService<IMentionService>()));

        return services;
    }
}
