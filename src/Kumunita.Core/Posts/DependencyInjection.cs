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

        // C3-03 — the group-post lane (ADR 0013) extracted from PostService as a
        // real part-vs-whole surface (the C3-00 EventRsvpGuardianService pattern).
        // The 4 group-post methods (ListGroupFeedAsync / GetGroupPostAsync /
        // CreateGroupPostAsync / UpdateGroupPostAsync) live here; the Posts
        // consumers (GroupsController, KioskItemCatalog) resolve this seam.
        services.AddTransient<Posts.IGroupPostService>(sp => new Posts.GroupPostService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<Core.Authorization.IAuthorizationService>(),
            sp.GetRequiredService<INotificationEmitter>(),
            sp.GetRequiredService<IMentionService>()));

        return services;
    }
}
