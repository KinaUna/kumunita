using Kumunita.Shared.Kernel.Auth;
using Kumunita.Shared.Kernel.Communities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Shared.Infrastructure.MultiTenancy;

// ── Requirements ──────────────────────────────────────────────────────────────

public record CommunityRoleRequirement(CommunityRole MinimumRole) : IAuthorizationRequirement;

public record PlatformAdminRequirement() : IAuthorizationRequirement;

// ── Handlers ──────────────────────────────────────────────────────────────────

/// <summary>
/// Reads the {slug} route value and checks the user has at least
/// the required CommunityRole in that community.
/// </summary>
public class CommunityRoleHandler : AuthorizationHandler<CommunityRoleRequirement>
{
    private readonly IHttpContextAccessor _accessor;

    public CommunityRoleHandler(IHttpContextAccessor accessor) => _accessor = accessor;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CommunityRoleRequirement requirement)
    {
        var httpContext = _accessor.HttpContext;

        if (httpContext is null)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        var slug = httpContext.Request.RouteValues["slug"]?.ToString();

        if (string.IsNullOrWhiteSpace(slug))
        {
            // No community context — deny role-scoped policies
            context.Fail();
            return Task.CompletedTask;
        }

        var userRole = context.User.GetCommunityRole(slug);

        if (userRole >= requirement.MinimumRole)
            context.Succeed(requirement);
        else
            context.Fail();

        return Task.CompletedTask;
    }
}

public class PlatformAdminHandler : AuthorizationHandler<PlatformAdminRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminRequirement requirement)
    {
        if (context.User.IsPlatformAdmin())
            context.Succeed(requirement);
        else
            context.Fail();

        return Task.CompletedTask;
    }
}

// ── Registration extension ────────────────────────────────────────────────────

public static class CommunityAuthorizationExtensions
{
    public static IServiceCollection AddCommunityAuthorization(
        this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddSingleton<IAuthorizationHandler, CommunityRoleHandler>();
        services.AddSingleton<IAuthorizationHandler, PlatformAdminHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy("PlatformAdmin", policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PlatformAdminRequirement()))
            .AddPolicy("CommunityMember", policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new CommunityRoleRequirement(CommunityRole.Member)))
            .AddPolicy("CommunityModerator", policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new CommunityRoleRequirement(CommunityRole.Moderator)))
            .AddPolicy("CommunityManager", policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new CommunityRoleRequirement(CommunityRole.Manager)));

        return services;
    }
}