using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>UserInfo</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (the C10 brief: "a new module brings its own
/// wiring" — FACES: <i>Adaptive</i>). <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>
/// composes this. The registrations are moved verbatim (comments + code) from
/// the original <c>AddKumunitaCore</c> body; behavior is unchanged — the same
/// transient services, the same ctor seams, the same "AddTransient with the
/// store injected" shape (C3 single-commit).
/// <para>
/// The module owns: the identity-graph read/write seams
/// (<see cref="IUserInfoService"/> / <see cref="IGuardianService"/> /
/// <see cref="IProfileSettingsService"/>), the group + community-membership
/// seams, the M2 <see cref="DirectoryService"/> composition root, and the M23
/// <see cref="IProfileFindService"/> find-people seam. The <see
/// cref="IAuthorizationService"/> it composes is the frozen ADR 0006-D read
/// decision path (no new authorization surface — the ADR 0006-D lane pin).
/// </para>
/// </summary>
public static class UserInfoModule
{
    /// <summary>Registers the <c>UserInfo</c> bounded context's domain services.</summary>
    public static IServiceCollection AddUserInfo(this IServiceCollection services)
    {
        services.AddTransient<IUserInfoService, UserInfoService>();
        services.AddTransient<IGroupService, GroupService>();
        services.AddTransient<ICommunityMembershipService, CommunityMembershipService>();
        services.AddTransient<IGuardianService, GuardianService>();
        services.AddTransient<IProfileSettingsService, ProfileSettingsService>();

        // M2 (plan U5): the directory-side composition root — a concrete class
        // (it composes two *seams*, not itself a seam: no interface, ADR
        // 0006-D's "single seam" rule applies to the *modules* it calls, not
        // to the caller).
        services.AddTransient<DirectoryService>(sp =>
            new DirectoryService(
                sp.GetRequiredService<IUserInfoService>(),
                sp.GetRequiredService<IProfileSettingsService>(),
                sp.GetRequiredService<IAuthorizationService>()));

        // M23 (ADR 0123 D4): the find-people composition root — the
        // DirectoryService shape above, extended with the host-registered
        // Marten IDocumentStore (the tag resolve + the CanSeeAsync aggregate
        // audit row) so the two paged reads (by-tag + bio-substring) compose
        // the frozen seams only (the ADR 0006-D lane pin — zero new
        // authorization surface, C-M23·2/D7). An interface (unlike
        // DirectoryService) so the Web layer resolves a stable seam.
        services.AddTransient<IProfileFindService>(sp => new ProfileFindService(
            sp.GetRequiredService<IProfileSettingsService>(),
            sp.GetRequiredService<IAuthorizationService>(),
            sp.GetRequiredService<Marten.IDocumentStore>()));

        return services;
    }
}
