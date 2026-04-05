using Kumunita.Authorization.Domain;
using Kumunita.Shared.Kernel;

namespace Kumunita.Tests.Authorization;

public class UserAuthorizationStateTests
{
    private static readonly UserId UserId = new(Guid.NewGuid());

    [Fact]
    public void CreateForNewUser_HasEmptyRolesAndNotSuspended()
    {
        UserAuthorizationState state = UserAuthorizationState.CreateForNewUser(UserId);

        Assert.Equal(UserId, state.Id);
        Assert.Empty(state.Roles);
        Assert.Empty(state.GroupIds);
        Assert.Empty(state.ActiveTokenIds);
        Assert.False(state.IsSuspended);
    }

    [Fact]
    public void ApplyRoleAssigned_AddsRole()
    {
        UserAuthorizationState state = UserAuthorizationState.CreateForNewUser(UserId);

        state.ApplyRoleAssigned("Member");

        Assert.Contains("Member", state.Roles);
        Assert.True(state.HasRole("Member"));
    }

    [Fact]
    public void ApplyRoleAssigned_IsIdempotentOnDuplicate()
    {
        UserAuthorizationState state = UserAuthorizationState.CreateForNewUser(UserId);
        state.ApplyRoleAssigned("Member");
        state.ApplyRoleAssigned("Member");

        Assert.Single(state.Roles);
    }

    [Fact]
    public void ApplyRoleRevoked_RemovesOnlyMatchingRole()
    {
        UserAuthorizationState state = UserAuthorizationState.CreateForNewUser(UserId);
        state.ApplyRoleAssigned("Member");
        state.ApplyRoleAssigned("Moderator");

        state.ApplyRoleRevoked("Member");

        Assert.DoesNotContain("Member", state.Roles);
        Assert.Contains("Moderator", state.Roles);
    }

    [Fact]
    public void ApplySuspended_SetsSuspendedFlag()
    {
        UserAuthorizationState state = UserAuthorizationState.CreateForNewUser(UserId);

        state.ApplySuspended();

        Assert.True(state.IsSuspended);
    }

    [Fact]
    public void ApplyReactivated_ClearsSuspendedFlag()
    {
        UserAuthorizationState state = UserAuthorizationState.CreateForNewUser(UserId);
        state.ApplySuspended();

        state.ApplyReactivated();

        Assert.False(state.IsSuspended);
    }

    [Fact]
    public void TrackToken_AddsTokenId()
    {
        UserAuthorizationState state = UserAuthorizationState.CreateForNewUser(UserId);
        CapabilityTokenId tokenId = new(Guid.NewGuid());

        state.TrackToken(tokenId);

        Assert.Contains(tokenId, state.ActiveTokenIds);
    }

    [Fact]
    public void RemoveToken_RemovesOnlyMatchingToken()
    {
        UserAuthorizationState state = UserAuthorizationState.CreateForNewUser(UserId);
        CapabilityTokenId tokenA = new(Guid.NewGuid());
        CapabilityTokenId tokenB = new(Guid.NewGuid());
        state.TrackToken(tokenA);
        state.TrackToken(tokenB);

        state.RemoveToken(tokenA);

        Assert.DoesNotContain(tokenA, state.ActiveTokenIds);
        Assert.Contains(tokenB, state.ActiveTokenIds);
    }

    [Fact]
    public void RevokeAllTokens_ClearsActiveTokenIds()
    {
        UserAuthorizationState state = UserAuthorizationState.CreateForNewUser(UserId);
        state.TrackToken(new CapabilityTokenId(Guid.NewGuid()));
        state.TrackToken(new CapabilityTokenId(Guid.NewGuid()));

        state.RevokeAllTokens();

        Assert.Empty(state.ActiveTokenIds);
    }
}
