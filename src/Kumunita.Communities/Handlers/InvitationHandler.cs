using Kumunita.Communities.Contracts.Commands;
using Kumunita.Communities.Contracts.Queries;
using Kumunita.Communities.Domain;
using Kumunita.Communities.Domain.Events;
using Kumunita.Communities.Exceptions;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Auth;
using Kumunita.Shared.Kernel.Communities;
using Marten;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using Wolverine.Http;

namespace Kumunita.Communities.Handlers;

public static class InvitationHandler
{
    [WolverinePost("/communities/{slug}/invitations")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "CommunityModerator")]
    public static async Task<IResult> InviteMember(
        string slug,
        InviteMemberCommand command,
        IDocumentSession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        UserId currentUserId = user.GetUserId();
        CommunityRole? currentRole = user.GetCommunityRole(slug);

        // Moderators can only invite at Member level
        if (currentRole == CommunityRole.Moderator && command.AssignedRole != CommunityRole.Member)
            throw new CannotAssignRoleAboveOwnException();

        Community community = await session
                                  .Query<Community>()
                                  .FirstOrDefaultAsync(c => c.Slug == slug && c.IsActive, ct)
                              ?? throw new CommunityNotFoundException(slug);

        // Check the email isn't already an active member
        // TODO: cross-reference with AppUser by email, check CommunityMembership

        CommunityInvitation invitation = new CommunityInvitation
        {
            Id = CommunityInvitationId.New(),
            CommunityId = community.Id,
            InvitedByUserId = currentUserId,
            InvitedEmail = command.InvitedEmail.ToLowerInvariant(),
            Token = GenerateToken(),
            AssignedRole = command.AssignedRole,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        session.Store(invitation);

        session.Events.Append(invitation.Id.Value, new InvitationCreated(
            community.Id,
            invitation.Id,
            invitation.InvitedEmail,
            invitation.AssignedRole,
            currentUserId,
            invitation.ExpiresAt,
            invitation.CreatedAt));

        // TODO: dispatch email notification via Wolverine message

        return Results.Created($"/communities/{slug}/invitations/{invitation.Id}", new PendingInvitationResult(
            invitation.Id,
            invitation.InvitedEmail,
            invitation.AssignedRole,
            invitation.ExpiresAt,
            invitation.CreatedAt));
    }

    [WolverinePost("/invitations/{token}/accept")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public static async Task<IResult> AcceptInvitation(
        string token,
        IDocumentSession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        CommunityInvitation invitation = await GetValidInvitation(token, session, ct);
        UserId currentUserId = user.GetUserId();

        string? userEmail = user.GetEmail();
        if (userEmail is null || !userEmail.Equals(invitation.InvitedEmail, StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { error = "This invitation was sent to a different email address." }, statusCode: StatusCodes.Status403Forbidden);

        Community community = await session.LoadAsync<Community>(invitation.CommunityId.Value, ct)
                              ?? throw new CommunityNotFoundException("unknown");

        // Check not already a member
        CommunityMembership? existingMembership = await session
            .Query<CommunityMembership>()
            .FirstOrDefaultAsync(m =>
                m.CommunityId == invitation.CommunityId &&
                m.UserId == currentUserId &&
                m.Status == MembershipStatus.Active, ct);

        if (existingMembership is not null)
            throw new AlreadyCommunityMemberException(community.Slug);

        CommunityMembership membership = new CommunityMembership
        {
            Id = CommunityMembershipId.New(),
            CommunityId = invitation.CommunityId,
            UserId = currentUserId,
            Role = invitation.AssignedRole,
            Status = MembershipStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
            InvitedBy = invitation.InvitedByUserId
        };

        invitation.Status = InvitationStatus.Accepted;
        invitation.RespondedAt = DateTimeOffset.UtcNow;
        invitation.ResultingMembershipId = membership.Id;

        session.Store(membership);
        session.Store(invitation);

        session.Events.Append(membership.Id.Value, new MemberJoinedCommunity(
            community.Id,
            community.Slug,
            currentUserId,
            membership.Role,
            membership.InvitedBy,
            membership.JoinedAt));

        return Results.Ok(new { community.Slug, membership.Role });
    }

    [WolverinePost("/invitations/{token}/decline")]
    public static async Task<IResult> DeclineInvitation(
        string token,
        IDocumentSession session,
        CancellationToken ct)
    {
        CommunityInvitation invitation = await GetValidInvitation(token, session, ct);

        invitation.Status = InvitationStatus.Declined;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        session.Store(invitation);

        session.Events.Append(invitation.Id.Value, new InvitationDeclined(
            invitation.CommunityId,
            invitation.Id,
            DateTimeOffset.UtcNow));

        return Results.Ok();
    }

    [WolverineDelete("/communities/{slug}/invitations/{invitationId}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "CommunityModerator")]
    public static async Task<IResult> RevokeInvitation(
        string slug,
        CommunityInvitationId invitationId,
        IDocumentSession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        CommunityInvitation invitation = await session.LoadAsync<CommunityInvitation>(invitationId.Value, ct)
                                         ?? throw new CommunityInvitationNotFoundException(invitationId.ToString());

        if (invitation.Status != InvitationStatus.Pending)
            return Results.Conflict(new { message = "Only pending invitations can be revoked." });

        invitation.Status = InvitationStatus.Revoked;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        session.Store(invitation);

        session.Events.Append(invitation.Id.Value, new InvitationRevoked(
            invitation.CommunityId,
            invitation.Id,
            user.GetUserId(),
            DateTimeOffset.UtcNow));

        return Results.Ok();
    }

    [WolverineGet("/communities/{slug}/invitations")]
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "CommunityModerator")]
    public static async Task<IResult> GetPendingInvitations(
        string slug,
        IQuerySession session,
        CancellationToken ct)
    {
        Community community = await session
                                  .Query<Community>()
                                  .FirstOrDefaultAsync(c => c.Slug == slug, ct)
                              ?? throw new CommunityNotFoundException(slug);

        IReadOnlyList<CommunityInvitation> invitations = await session
            .Query<CommunityInvitation>()
            .Where(i => i.CommunityId == community.Id && i.Status == InvitationStatus.Pending && i.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return Results.Ok(invitations.Select(i => new PendingInvitationResult(
            i.Id, i.InvitedEmail, i.AssignedRole, i.ExpiresAt, i.CreatedAt)));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<CommunityInvitation> GetValidInvitation(
        string token,
        IQuerySession session,
        CancellationToken ct)
    {
        CommunityInvitation invitation = await session
                                             .Query<CommunityInvitation>()
                                             .FirstOrDefaultAsync(i => i.Token == token, ct)
                                         ?? throw new CommunityInvitationNotFoundException(token);

        if (invitation.Status == InvitationStatus.Accepted)
            throw new InvitationAlreadyUsedException(token);

        if (invitation.Status != InvitationStatus.Pending || invitation.IsExpired)
            throw new InvitationExpiredException(token);

        return invitation;
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}