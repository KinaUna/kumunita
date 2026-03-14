using Kumunita.Communities.Domain;
using Kumunita.Communities.Domain.Events;
using Kumunita.Communities.Exceptions;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Auth;
using Kumunita.Shared.Kernel.Communities;
using Kumunita.Shared.Kernel.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Marten;
using Marten.Linq;
using Microsoft.AspNetCore.Http;
using Wolverine.Http;
using Kumunita.Communities.Contracts.Commands;
using Kumunita.Communities.Contracts.Queries;

namespace Kumunita.Communities.Handlers;

/// <summary>
/// Handles community provisioning and deactivation.
/// All endpoints here require the platform_admin claim.
/// </summary>
public static class PlatformCommunityHandler
{
    [WolverinePost("/platform/communities")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PlatformAdmin")]
    public static async Task<IResult> CreateCommunity(
        CreateCommunityCommand command,
        IDocumentSession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        CommunitySlug slug = new CommunitySlug(command.Slug);

        // Check slug uniqueness
        bool existing = await session
            .Query<Community>()
            .AnyAsync(c => c.Slug == slug.Value, ct);

        if (existing)
            throw new CommunitySlugAlreadyExistsException(slug.Value);

        Community community = new Community
        {
            Id = CommunityId.New(),
            Slug = slug.Value,
            Name = new LocalizedContent(command.Name),
            Description = new LocalizedContent(command.Description),
            Address = command.City is not null ? new CommunityAddress(
                command.Street ?? string.Empty,
                command.City,
                command.PostalCode ?? string.Empty,
                command.Country ?? string.Empty) : null,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByPlatformAdmin = user.FindFirstValue(ClaimTypes.Name) ?? "unknown"
        };

        session.Store(community);

        session.Events.Append(community.Id.Value, new CommunityCreated(
            community.Id,
            community.Slug,
            community.CreatedByPlatformAdmin,
            community.CreatedAt));

        return Results.Created($"/communities/{community.Slug}", new { community.Id, community.Slug });
    }

    [WolverineDelete("/platform/communities/{slug}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "PlatformAdmin")]
    public static async Task<IResult> DeactivateCommunity(
        string slug,
        IDocumentSession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        Community community = await session
                                  .Query<Community>()
                                  .FirstOrDefaultAsync(c => c.Slug == slug, ct)
                              ?? throw new CommunityNotFoundException(slug);

        if (!community.IsActive)
            return Results.Conflict(new { message = "Community is already inactive." });

        community.IsActive = false;
        community.DeactivatedAt = DateTimeOffset.UtcNow;
        community.DeactivatedByPlatformAdmin = user.FindFirstValue(ClaimTypes.Name) ?? "unknown";

        session.Store(community);

        session.Events.Append(community.Id.Value, new CommunityDeactivated(
            community.Id,
            community.Slug,
            community.DeactivatedByPlatformAdmin,
            community.DeactivatedAt.Value));

        return Results.Ok();
    }

    [WolverineGet("/platform/communities")]
    [Authorize(Policy = "PlatformAdmin")]
    public static async Task<IResult> GetAllCommunities(
        GetAllCommunitiesQuery query,
        IQuerySession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        IMartenQueryable<Community> q = session.Query<Community>();

        if (!query.IncludeInactive)
            q = (IMartenQueryable<Community>)q.Where(c => c.IsActive);

        IReadOnlyList<Community> communities = await q
            .OrderBy(c => c.Slug)
            .ToListAsync(ct);

        // TODO: resolve member counts in a single batch query
        IReadOnlyList<string> preferredLanguages = user.GetPreferredLanguage();
        return Results.Ok(communities.Select(c => new CommunityResult(
            c.Id,
            c.Slug,
            c.Name.Resolve(preferredLanguages),
            c.Description.Resolve(preferredLanguages),
            c.Address?.City,
            c.Address?.Country,
            c.IsActive,
            MemberCount: 0)));
    }
}