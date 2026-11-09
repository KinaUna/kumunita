using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.Portability;
using Kumunita.Core.UserInfo;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Portability</c> bounded context's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The operator
/// plane (M11, ADR 0108) + the resident lane (M27, ADR 0148). Composes only
/// the frozen seams; the <c>AppDbContext</c> reference is the EF-Core
/// identity-schema seam (ADR 0004 §B — EF only for Identity tables). Core stays
/// HTTP-free (ADR 0006-D).
/// </summary>
public static class PortabilityModule
{
    /// <summary>Registers the <c>Portability</c> bounded context's seams.</summary>
    public static IServiceCollection AddPortability(this IServiceCollection services)
    {
        services.AddTransient<IPortabilityService>(sp => new PortabilityService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            // C3-07 (ADR 0297) — the identity-principal store contract (the EF
            // Core AppDbContext stays a private detail of the Identity module).
            sp.GetRequiredService<IIdentityPrincipalStore>(),
            sp.GetRequiredService<UserManager<User>>(),
            sp.GetRequiredService<RoleManager<IdentityRole>>(),
            sp.GetRequiredService<IOptions<CommunityOptions>>(),
            sp.GetRequiredService<IMediaStore>(),
            sp.GetRequiredService<IMediaFileStore>(),
            // IS29 (ADR 0299 §4, D10) — the transparency emission seam (the
            // last ctor parameter; the C03 INotificationEmitter contract).
            sp.GetRequiredService<Notifications.INotificationEmitter>()));

        services.AddTransient<IUserPortabilityService>(sp => new UserPortabilityService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IUserInfoService>(),
            sp.GetRequiredService<IMediaStore>(),
            sp.GetRequiredService<UserManager<User>>()));
        // C3-03 (ADR 0293) — the ownership-guard seam (the C08 test-compat
        // facade shape; the UserPortabilityService guard methods are thin
        // forwarders to it and keep compiling unchanged).
        services.AddTransient<IImportOwnershipGuardService>(sp => new ImportOwnershipGuardService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<UserManager<User>>()));

        return services;
    }
}
