using Kumunita.Announcements.Domain;
using Kumunita.Shared.Kernel.Announcements;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OpenIddict.Validation.AspNetCore;
using Wolverine.Http;

namespace Kumunita.Host.Endpoints;

/// <summary>
/// Returns the number of published, non-expired announcements for a community tenant.
/// Used by the community landing page stats row.
/// The {slug} route segment triggers CommunityTenantMiddleware, which validates
/// membership and scopes the IQuerySession to the correct tenant.
/// </summary>
public static class AnnouncementCountEndpoint
{
    [WolverineGet("/api/announcements/{slug}/count")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public static async Task<IResult> GetCount(
        string slug,
        IQuerySession session,
        CancellationToken ct)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        int count = await session
            .Query<Announcement>()
            .CountAsync(a =>
                a.Status == AnnouncementStatus.Published &&
                (a.ExpiresAt == null || a.ExpiresAt > now), ct);

        return Results.Ok(count);
    }
}
