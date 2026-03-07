using Kumunita.Authorization.Domain;
using Kumunita.Authorization.Domain.Events;
using Kumunita.Identity.Domain;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;
using Marten;

namespace Kumunita.Authorization.Features.CapabilityTokens;

public record RequestCapabilityToken(
    UserId RequesterId,
    UserId OwnerId,
    string ResourceTypeName,
    string Action,
    string? RequestContext = null);

public record CapabilityTokenResponse(
    bool Granted,
    CapabilityTokenId? TokenId,
    DateTimeOffset? ExpiresAt,
    string? DenialReason);

public static class RequestCapabilityTokenHandler
{
    public static async Task<(CapabilityTokenResponse, IDomainEvent)> Handle(
        RequestCapabilityToken cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        // Step 1 — validate resource type exists
        var resource = ResourceType.Find(cmd.ResourceTypeName);
        if (resource is null)
        {
            var denied = Deny("Unknown resource type", cmd, resource?.SensitivityTier
                ?? SensitivityTier.Restricted, session);
            return (denied.response, denied.evt);
        }

        // Step 2 — public resources need no token
        if (resource.SensitivityTier == SensitivityTier.Public)
        {
            var token = CapabilityToken.Issue(
                cmd.RequesterId, cmd.OwnerId,
                cmd.ResourceTypeName, cmd.Action,
                resource.SensitivityTier, cmd.RequestContext);
            session.Store(token);
            session.Store(AuditEntry.ForTokenIssued(token));
            return (Grant(token), new CapabilityTokenIssued(token.Id, cmd.RequesterId, cmd.OwnerId, cmd.ResourceTypeName, cmd.Action, resource.SensitivityTier, token.ExpiresAt));
        }

        // Step 3 — load requester's authorization state
        var requesterState = await session
            .LoadAsync<UserAuthorizationState>(cmd.RequesterId, ct);

        if (requesterState is null || requesterState.IsSuspended)
        {
            var denied = Deny("Requester account is suspended or not found",
                cmd, resource.SensitivityTier, session);
            return (denied.response, denied.evt);
        }

        // Step 4 — restricted resources require admin role
        if (resource.SensitivityTier == SensitivityTier.Restricted
            && !requesterState.HasRole(AppRole.SystemRoles.Admin)
            && !requesterState.HasRole(AppRole.SystemRoles.Moderator))
        {
            var denied = Deny("Insufficient role for restricted resource",
                cmd, resource.SensitivityTier, session);
            return (denied.response, denied.evt);
        }

        // Step 5 — admins can always access non-restricted resources
        if (requesterState.HasRole(AppRole.SystemRoles.Admin))
        {
            var token = IssueAndStore(cmd, resource, session);
            return (Grant(token), new CapabilityTokenIssued(token.Id, cmd.RequesterId, cmd.OwnerId, cmd.ResourceTypeName, cmd.Action, resource.SensitivityTier, token.ExpiresAt));
        }

        // Step 6 — owner can always access their own data
        if (cmd.RequesterId == cmd.OwnerId)
        {
            var token = IssueAndStore(cmd, resource, session);
            return (Grant(token), new CapabilityTokenIssued(token.Id, cmd.RequesterId, cmd.OwnerId, cmd.ResourceTypeName, cmd.Action, resource.SensitivityTier, token.ExpiresAt));
        }

        // Step 7 — evaluate visibility policy
        var policy = await session
            .Query<VisibilityPolicy>()
            .FirstOrDefaultAsync(p =>
                p.OwnerId == cmd.OwnerId &&
                p.ResourceTypeName == cmd.ResourceTypeName, ct);

        if (policy is null)
        {
            // No policy means private by default — deny
            var denied = Deny("No visibility policy found — defaulting to private",
                cmd, resource.SensitivityTier, session);
            return (denied.response, denied.evt);
        }

        var accessGranted = policy.Visibility switch
        {
            VisibilityLevel.Members => true, // any authenticated member
            VisibilityLevel.SharedGroups =>
                requesterState.GroupIds.Any(g =>
                    policy.AllowedGroupIds.Contains(g)),
            VisibilityLevel.SpecificGroups =>
                policy.AllowedGroupIds.Any(g =>
                    requesterState.GroupIds.Contains(g)),
            VisibilityLevel.SpecificUsers =>
                policy.AllowedUserIds.Contains(cmd.RequesterId),
            VisibilityLevel.Private => false,
            _ => false
        };

        if (!accessGranted)
        {
            var denied = Deny("Visibility policy denied access",
                cmd, resource.SensitivityTier, session);
            return (denied.response, denied.evt);
        }

        // Step 8 — all checks passed, issue token
        var grantedToken = IssueAndStore(cmd, resource, session);
        return (Grant(grantedToken), new CapabilityTokenIssued(grantedToken.Id, cmd.RequesterId, cmd.OwnerId, cmd.ResourceTypeName, cmd.Action, resource.SensitivityTier, grantedToken.ExpiresAt));
    }

    private static CapabilityToken IssueAndStore(
        RequestCapabilityToken cmd,
        ResourceDescriptor resource,
        IDocumentSession session)
    {
        var token = CapabilityToken.Issue(
            cmd.RequesterId, cmd.OwnerId,
            cmd.ResourceTypeName, cmd.Action,
            resource.SensitivityTier, cmd.RequestContext);

        session.Store(token);
        session.Store(AuditEntry.ForTokenIssued(token));
        return token;
    }

    private static (CapabilityTokenResponse response, CapabilityTokenDenied evt) Deny(
        string reason,
        RequestCapabilityToken cmd,
        SensitivityTier tier,
        IDocumentSession session)
    {
        session.Store(AuditEntry.ForTokenDenied(
            cmd.RequesterId, cmd.OwnerId,
            cmd.ResourceTypeName, cmd.Action,
            tier, reason, cmd.RequestContext));

        return (
            new CapabilityTokenResponse(false, null, null, reason),
            new CapabilityTokenDenied(
                cmd.RequesterId, cmd.OwnerId,
                cmd.ResourceTypeName, reason));
    }

    private static CapabilityTokenResponse Grant(CapabilityToken token)
        => new(true, token.Id, token.ExpiresAt, null);
}