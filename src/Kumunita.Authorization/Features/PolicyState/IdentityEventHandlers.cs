using Kumunita.Authorization.Domain;
using Kumunita.Authorization.Exceptions;
using Kumunita.Identity.Domain;
using Kumunita.Identity.Domain.Events;
using Kumunita.Shared.Kernel;
using Marten;

namespace Kumunita.Authorization.Features.PolicyState;

public static class IdentityEventHandlers
{
    // Wolverine discovers these by convention — no registration needed
    public static async Task Handle(
        UserRegistered evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        var state = UserAuthorizationState.CreateForNewUser(evt.UserId);
        // New members get the Member role by default
        state.ApplyRoleAssigned(AppRole.SystemRoles.Member);
        session.Store(state);

        // Create default visibility policies for all sensitive resources
        await CreateDefaultVisibilityPolicies(evt.UserId, session, ct);
    }

    public static async Task Handle(
        RoleAssigned evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        var state = await session.LoadAsync<UserAuthorizationState>(evt.UserId, ct)
            ?? throw new AuthorizationStateNotFoundException(evt.UserId);

        state.ApplyRoleAssigned(evt.RoleName);
        session.Store(state);
    }

    public static async Task Handle(
        RoleRevoked evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        var state = await session.LoadAsync<UserAuthorizationState>(evt.UserId, ct)
            ?? throw new AuthorizationStateNotFoundException(evt.UserId);

        state.ApplyRoleRevoked(evt.RoleName);
        session.Store(state);
    }

    public static async Task Handle(
        UserAddedToUserGroup evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        var state = await session.LoadAsync<UserAuthorizationState>(evt.UserId, ct)
            ?? throw new AuthorizationStateNotFoundException(evt.UserId);

        state.ApplyAddedToGroup(evt.GroupId);
        session.Store(state);
    }

    public static async Task Handle(
        UserRemovedFromUserGroup evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        var state = await session.LoadAsync<UserAuthorizationState>(evt.UserId, ct)
            ?? throw new AuthorizationStateNotFoundException(evt.UserId);

        state.ApplyRemovedFromGroup(evt.GroupId);
        session.Store(state);
    }

    public static async Task<AccountSuspended> Handle(
        AccountSuspended evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        var state = await session.LoadAsync<UserAuthorizationState>(evt.UserId, ct)
            ?? throw new AuthorizationStateNotFoundException(evt.UserId);

        // Revoke all active tokens immediately on suspension
        state.ApplySuspended();
        state.RevokeAllTokens();
        session.Store(state);

        // Return the event so Wolverine re-publishes it for
        // other modules to react to (e.g. invalidate active sessions)
        return evt;
    }

    public static async Task Handle(
        AccountReactivated evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        var state = await session.LoadAsync<UserAuthorizationState>(evt.UserId, ct)
            ?? throw new AuthorizationStateNotFoundException(evt.UserId);

        state.ApplyReactivated();
        session.Store(state);
    }

    private static async Task CreateDefaultVisibilityPolicies(
        UserId userId,
        IDocumentSession session,
        CancellationToken ct)
    {
        // Default policies — conservative, user can relax them later
        var defaults = new[]
        {
            (ResourceType.ProfileBio.Name, VisibilityLevel.Members),
            (ResourceType.ProfilePhoneNumber.Name, VisibilityLevel.Private),
            (ResourceType.ProfileAlternativeEmail.Name, VisibilityLevel.Private),
            (ResourceType.ProfileAddress.Name, VisibilityLevel.Private),
            (ResourceType.DirectoryEntryLocation.Name, VisibilityLevel.Members),
            (ResourceType.GroupMembership.Name, VisibilityLevel.Members),
        };

        foreach (var (resourceType, defaultVisibility) in defaults)
        {
            var policy = VisibilityPolicy.CreateDefault(
                userId, resourceType, defaultVisibility);
            session.Store(policy);
        }
    }
}
