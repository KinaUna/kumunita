using Kumunita.Communities.Contracts.Queries;
using Kumunita.Communities.Domain;
using Kumunita.Communities.Exceptions;
using Kumunita.Shared.Kernel.Auth;
using Kumunita.Shared.Kernel.Communities;
using Marten;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Kumunita.Shared.Kernel;
using Wolverine.Http;

namespace Kumunita.Communities.Handlers;

public static class CommunityQueryHandler
{
    /// <summary>
    /// Returns all communities the authenticated user belongs to.
    /// Used to populate the community switcher in the frontend.
    /// </summary>
    [WolverineGet("/communities")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public static async Task<IResult> GetUserCommunities(
        IQuerySession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        UserId userId = user.GetUserId();

        IReadOnlyList<CommunityMembership> memberships = await session
            .Query<CommunityMembership>()
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .ToListAsync(ct);

        if (!memberships.Any())
            return Results.Ok(Array.Empty<object>());

        Guid[] communityIds = memberships.Select(m => m.CommunityId.Value).ToArray();

        IReadOnlyList<Community> communities = await session
            .Query<Community>()
            .Where(c => c.IsActive && communityIds.Contains(c.Id.Value))
            .ToListAsync(ct);

        // TODO: resolve LocalizedContent Name for user's preferred language
        var result = communities.Select(c => new
        {
            c.Id,
            c.Slug,
            Name = c.Name.Resolve(user.GetPreferredLanguage()),
            c.Address?.City,
            c.Address?.Country,
            Role = memberships.First(m => m.CommunityId == c.Id).Role
        });

        return Results.Ok(result);
    }

    /// <summary>
    /// Returns the community overview for the landing page.
    /// Only accessible to active members of the community.
    /// </summary>
    [WolverineGet("/communities/{slug}")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public static async Task<IResult> GetCommunity(
        string slug,
        IQuerySession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        // Middleware already validated membership — this is a safety check
        if (!user.IsMemberOf(slug))
            return Results.Forbid();

        Community community = await session
                                  .Query<Community>()
                                  .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive, ct)
                              ?? throw new CommunityNotFoundException(slug);

        int memberCount = await session
            .Query<CommunityMembership>()
            .CountAsync(m => m.CommunityId == community.Id && m.Status == MembershipStatus.Active, ct);

        IReadOnlyList<string> preferredLanguages = user.GetPreferredLanguage();

        return Results.Ok(new CommunityResult(
            community.Id,
            community.Slug,
            community.Name.Resolve(preferredLanguages),
            community.Description.Resolve(preferredLanguages),
            community.Address?.City,
            community.Address?.Country,
            community.IsActive,
            memberCount));
    }
}