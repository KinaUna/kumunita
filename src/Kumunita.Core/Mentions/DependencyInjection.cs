using Kumunita.Core.Mentions;
using Kumunita.Core.UserInfo;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Mentions</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. M36 (ADR 0165,
/// U03): the @mentions shared module — the one new Core read seam (M36·1). A
/// plain store-composing read over the frozen M2 <see cref="DirectoryService"/>
/// catalog seam (M36·2 — reused, not re-shaped): it composes ListAsync, does no
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> call, writes
/// zero AccessAudit rows (M36·5), and carries no HttpClient / Wolverine handler
/// (M36·9, ADR 0006-D). Resolves the concrete <see cref="DirectoryService"/>
/// (Part 2.0 — no IDirectoryService interface is invented).
/// </summary>
public static class MentionsModule
{
    /// <summary>Registers the <c>Mentions</c> bounded context's read seam.</summary>
    public static IServiceCollection AddMentions(this IServiceCollection services)
    {
        services.AddTransient<IMentionService>(sp => new MentionService(
            sp.GetRequiredService<Kumunita.Core.UserInfo.IDirectoryService>()));

        return services;
    }
}
