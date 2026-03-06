using Kumunita.Identity.Domain;
using Marten;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Kumunita.Identity.Infrastructure;

public class CustomClaimsHandler : IOpenIddictServerHandler<OpenIddictServerEvents.HandleTokenRequestContext>
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IQuerySession _session;

    public CustomClaimsHandler(
        UserManager<AppUser> userManager,
        IQuerySession session)
    {
        _userManager = userManager;
        _session = session;
    }

    public async ValueTask HandleAsync(
        OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        var appUser = await _userManager.FindByNameAsync(
            context.Principal?.Identity?.Name ?? string.Empty);

        if (appUser is null) return;

        var userId = appUser.DomainId;

        // Load roles from ASP.NET Core Identity
        var roles = await _userManager.GetRolesAsync(appUser);
        foreach (var role in roles)
            context.Principal!.SetClaim("role", role);

        // Load preferred language from Marten UserProfile
        var profile = await _session.LoadAsync<UserProfile>(userId);
        if (profile is not null)
            context.Principal!.SetClaim("preferred_language",
                profile.PreferredLanguage.Value);

        // Load group memberships from Marten
        var memberships = await _session
            .Query<UserGroupMembership>()
            .Where(m => m.UserId == userId)
            .Select(m => m.GroupId.Value.ToString())
            .ToListAsync();

        foreach (var groupId in memberships)
            context.Principal!.SetClaim("group", groupId);
    }
}