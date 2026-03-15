using Kumunita.Announcements.Contracts.Queries;
using Kumunita.Announcements.Domain;
using Kumunita.Authorization.Domain;
using Kumunita.Communities.Domain;
using Kumunita.Identity.Domain;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Announcements;
using Kumunita.Shared.Kernel.Auth;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OpenIddict.Validation.AspNetCore;
using System.Security.Claims;
using Wolverine.Http;

namespace Kumunita.Host.Endpoints;

/// <summary>
/// Combined announcement feed — returns up to <see cref="PageSize"/> published
/// announcements per community the authenticated user belongs to, grouped by community.
/// Used by both the Home dashboard and the /announcements combined view.
/// </summary>
public static class CombinedAnnouncementFeedEndpoint
{
    private const int PageSize = 5;

    /// <summary>
    /// Returns published, non-expired announcements grouped by community.
    /// Community names and UserProfile data are loaded from the platform-level
    /// (DEFAULT) tenant; announcements and targeting state are loaded per community
    /// tenant via <see cref="IDocumentStore"/> to respect multi-tenancy.
    /// </summary>
    [WolverineGet("/announcements")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public static async Task<IResult> GetCombinedFeed(
        ClaimsPrincipal user,
        IQuerySession session,     // DEFAULT tenant — Community + UserProfile
        IDocumentStore store,      // per-tenant — Announcement + UserAuthorizationState
        CancellationToken ct)
    {
        List<string> slugs = user.GetCommunitySlugs().ToList();

        if (slugs.Count == 0)
            return Results.Ok(Array.Empty<CommunityAnnouncementGroup>());

        IReadOnlyList<string> preferredLanguages = user.GetPreferredLanguage();
        UserId userId = user.GetUserId();

        // Community documents live in the platform-level (DEFAULT) tenant,
        // the same session used by GetUserCommunities and PlatformCommunityHandler.
        IReadOnlyList<Community> communities = await session
            .Query<Community>()
            .Where(c => c.IsActive && slugs.Contains(c.Slug))
            .ToListAsync(ct);

        Dictionary<string, string> communityNameBySlug = communities.ToDictionary(
            c => c.Slug,
            c => c.Name.Resolve(preferredLanguages));

        List<CommunityAnnouncementGroup> groups = new List<CommunityAnnouncementGroup>();

        foreach (string slug in slugs)
        {
            if (!communityNameBySlug.TryGetValue(slug, out string? communityName))
                continue;

            await using IQuerySession tenantSession = store.QuerySession(
                new Marten.Services.SessionOptions { TenantId = slug });

            // Targeting context is per-community and stored in the community tenant.
            UserAuthorizationState? authState =
                await tenantSession.LoadAsync<UserAuthorizationState>(userId, ct);

            IReadOnlyList<string> memberRoles = authState?.Roles ?? [];
            IReadOnlyList<GroupId> memberGroups = authState?.GroupIds ?? [];

            // Fetch more than PageSize upfront to absorb targeting filter losses.
            IReadOnlyList<Announcement> candidates = await tenantSession
                .Query<Announcement>()
                .Where(a => a.Status == AnnouncementStatus.Published
                    && (a.ExpiresAt == null || a.ExpiresAt > DateTimeOffset.UtcNow))
                .OrderByDescending(a => a.PublishedAt)
                .Take(PageSize * 3)
                .ToListAsync(ct);

            List<Announcement> targeted = candidates
                .Where(a => a.Target.Includes(memberRoles, memberGroups))
                .Take(PageSize)
                .ToList();

            // Batch-load author display names. UserProfile docs are platform-level
            // so use the DEFAULT session, same as CustomClaimsHandler.
            List<UserId> authorIds = targeted.Select(a => a.OwnerId).Distinct().ToList();

            Dictionary<UserId, string> authorNames = [];

            if (authorIds.Count > 0)
            {
                IReadOnlyList<UserProfile> profiles = await session
                    .Query<UserProfile>()
                    .Where(p => authorIds.Contains(p.Id))
                    .ToListAsync(ct);

                authorNames = profiles.ToDictionary(p => p.Id, p => p.DisplayName);
            }

            List<AnnouncementSummary> summaries = targeted
                .Select(a => new AnnouncementSummary(
                    a.Id,
                    Title: a.Title.Resolve(preferredLanguages),
                    Excerpt: MakeExcerpt(a.Body.Resolve(preferredLanguages)),
                    AuthorName: authorNames.GetValueOrDefault(a.OwnerId, string.Empty),
                    a.PublishedAt!.Value,
                    IsUniversal: a.Target.IsUniversal,
                    a.Status))
                .ToList();

            groups.Add(new CommunityAnnouncementGroup(slug, communityName, summaries));
        }

        return Results.Ok(groups);
    }

    private static string MakeExcerpt(string body, int maxLength = 200)
    {
        if (body.Length <= maxLength)
            return body;

        return string.Concat(body.AsSpan(0, maxLength).TrimEnd(), "…");
    }
}