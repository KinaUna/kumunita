using System.Security.Claims;
using Kumunita.Shared.Kernel.Auth;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.Http;

namespace Kumunita.Shared.Infrastructure.MultiTenancy;

/// <summary>
/// Extracts the {slug} route value and configures the Marten tenant session
/// for the current request. Must be registered after UseAuthentication() and
/// UseAuthorization() but before MapWolverineEndpoints().
///
/// For cross-tenant routes (no {slug}), the session is left as multi-tenant
/// so handlers can call QueryAcrossAllTenants explicitly.
/// </summary>
public class CommunityTenantMiddleware
{
    private readonly RequestDelegate _next;

    public CommunityTenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IDocumentStore store)
    {
        if (context.Request.RouteValues.TryGetValue("slug", out object? slugValue) &&
            slugValue is string slug &&
            !string.IsNullOrWhiteSpace(slug))
        {
            ClaimsPrincipal user = context.User;

            if (user.Identity?.IsAuthenticated == true && !user.IsPlatformAdmin())
            {
                if (!user.IsMemberOf(slug))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }

            context.Items[MartenTenantKey] = slug;
        }

        await _next(context);
    }

    public const string MartenTenantKey = "marten-tenant-id";
}

/// <summary>
/// Marten session factory that scopes sessions to the community tenant
/// extracted by <see cref="CommunityTenantMiddleware"/>.
/// Replaces UseLightweightSessions() — also creates lightweight sessions.
/// Registered as scoped so it can read the current HTTP context per request.
/// </summary>
public sealed class TenantAwareSessionFactory(
    IDocumentStore store,
    IHttpContextAccessor accessor) : ISessionFactory
{
    private string? TenantId =>
        accessor.HttpContext?.Items[CommunityTenantMiddleware.MartenTenantKey] as string;

    public IQuerySession QuerySession()
    {
        string? id = TenantId;
        return id is not null
            ? store.QuerySession(new SessionOptions { TenantId = id })
            : store.QuerySession();
    }

    public IDocumentSession OpenSession()
    {
        string? id = TenantId;
        return id is not null
            ? store.LightweightSession(new SessionOptions { TenantId = id })
            : store.LightweightSession();
    }
}
