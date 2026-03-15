using Kumunita.Communities.Contracts.Commands;
using Kumunita.Communities.Contracts.Queries;
using Kumunita.Communities.Domain;
using Kumunita.Communities.Domain.Events;
using Kumunita.Communities.Exceptions;
using Kumunita.Identity.Domain;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Auth;
using Kumunita.Shared.Kernel.Communities;
using Marten;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Wolverine.Http;

namespace Kumunita.Communities.Handlers;

public static class MemberManagementHandler
{
    [WolverinePut("/communities/{slug}/members/{userId}/role")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "CommunityManager")]
    public static async Task<IResult> ChangeMemberRole(
        string slug,
        UserId userId,
        ChangeMemberRoleCommand command,
        IDocumentSession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        // Managers cannot elevate anyone beyond Manager
        if (command.NewRole > CommunityRole.Manager)
            return Results.BadRequest(new { message = "Invalid role." });

        Community community = await session
                                  .Query<Community>()
                                  .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive, ct)
                              ?? throw new CommunityNotFoundException(slug);

        CommunityMembership membership = await session
                                             .Query<CommunityMembership>()
                                             .FirstOrDefaultAsync(m =>
                                                 m.CommunityId == community.Id &&
                                                 m.UserId == userId &&
                                                 m.Status == MembershipStatus.Active, ct)
                                         ?? throw new NotACommunityMemberException(slug);

        CommunityRole oldRole = membership.Role;
        membership.Role = command.NewRole;

        session.Store(membership);

        session.Events.Append(membership.Id.Value, new MemberRoleChanged(
            community.Id,
            community.Slug,
            userId,
            oldRole,
            command.NewRole,
            user.GetUserId(),
            DateTimeOffset.UtcNow));

        // MemberRoleChanged is handled by the Authorization module to:
        // 1. Update UserAuthorizationState for this community
        // 2. Revoke active capability tokens (JWT becomes stale → 401 → silent refresh)

        return Results.Ok(new { userId, newRole = command.NewRole });
    }

    [WolverinePost("/communities/{slug}/members/{userId}/suspend")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "CommunityManager")]
    public static async Task<IResult> SuspendMember(
        string slug,
        UserId userId,
        SuspendMemberCommand command,
        IDocumentSession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        Community community = await session
                                  .Query<Community>()
                                  .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive, ct)
                              ?? throw new CommunityNotFoundException(slug);

        CommunityMembership membership = await session
                                             .Query<CommunityMembership>()
                                             .FirstOrDefaultAsync(m =>
                                                 m.CommunityId == community.Id &&
                                                 m.UserId == userId &&
                                                 m.Status == MembershipStatus.Active, ct)
                                         ?? throw new NotACommunityMemberException(slug);

        // Managers cannot suspend other Managers
        if (membership.Role == CommunityRole.Manager)
            return Results.Forbid();

        membership.Status = MembershipStatus.Suspended;
        membership.SuspendedAt = DateTimeOffset.UtcNow;
        membership.SuspendedBy = user.GetUserId();
        membership.SuspensionReason = command.Reason;

        session.Store(membership);

        session.Events.Append(membership.Id.Value, new MemberSuspendedFromCommunity(
            community.Id,
            community.Slug,
            userId,
            user.GetUserId(),
            command.Reason,
            DateTimeOffset.UtcNow));

        return Results.Ok();
    }

    [WolverineDelete("/communities/{slug}/members/{userId}")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public static async Task<IResult> RemoveMember(
        string slug,
        UserId userId,
        IDocumentSession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        UserId currentUserId = user.GetUserId();
        CommunityRole? currentRole = user.GetCommunityRole(slug);

        // Only Managers or the member themselves can remove
        bool isSelf = currentUserId == userId;
        bool isManager = currentRole == CommunityRole.Manager;

        if (!isSelf && !isManager)
            return Results.Forbid();

        Community community = await session
                                  .Query<Community>()
                                  .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive, ct)
                              ?? throw new CommunityNotFoundException(slug);

        CommunityMembership membership = await session
                                             .Query<CommunityMembership>()
                                             .FirstOrDefaultAsync(m =>
                                                 m.CommunityId == community.Id &&
                                                 m.UserId == userId, ct)
                                         ?? throw new NotACommunityMemberException(slug);

        membership.Status = MembershipStatus.Left;
        membership.LeftAt = DateTimeOffset.UtcNow;

        session.Store(membership);

        session.Events.Append(membership.Id.Value, new MemberLeftCommunity(
            community.Id,
            community.Slug,
            userId,
            DateTimeOffset.UtcNow));

        return Results.Ok();
    }

    [WolverineGet("/communities/{slug}/members")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "CommunityModerator")]
    public static async Task<IResult> GetCommunityMembers(
        string slug,
        IQuerySession session,
        CancellationToken ct)
    {
        Community community = await session
                                  .Query<Community>()
                                  .FirstOrDefaultAsync(c => c.Slug == slug, ct)
                              ?? throw new CommunityNotFoundException(slug);

        IReadOnlyList<CommunityMembership> members = await session
            .Query<CommunityMembership>()
            .Where(m => m.CommunityId == community.Id && m.Status == MembershipStatus.Active)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(ct);

        Guid[] memberGuids = members.Select(m => m.UserId.Value).ToArray();
        IReadOnlyList<UserProfile> profiles = await session.LoadManyAsync<UserProfile>(ct, memberGuids);
        Dictionary<Guid, string> displayNames = profiles.ToDictionary(p => p.Id.Value, p => p.DisplayName);

        return Results.Ok(members.Select(m => new CommunityMemberResult(
            m.UserId,
            displayNames.GetValueOrDefault(m.UserId.Value, string.Empty),
            m.Role,
            m.Status,
            m.JoinedAt)));
    }
}