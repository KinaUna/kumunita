using Kumunita.Authorization.Domain;
using Kumunita.Shared.Kernel;

namespace Kumunita.Tests.Authorization;

public class CapabilityTokenTests
{
    private static readonly UserId RequesterId = new(Guid.NewGuid());
    private static readonly UserId OwnerId = new(Guid.NewGuid());
    private const string Resource = "UserProfile";
    private const string Action = "read";

    [Theory]
    [InlineData(SensitivityTier.Public)]
    [InlineData(SensitivityTier.Standard)]
    [InlineData(SensitivityTier.Sensitive)]
    public void Issue_CreatesActiveToken(SensitivityTier tier)
    {
        CapabilityToken token = CapabilityToken.Issue(
            RequesterId, OwnerId, Resource, Action, tier, requestContext: null);

        Assert.Equal(CapabilityTokenStatus.Active, token.Status);
        Assert.Equal(tier, token.SensitivityTier);
        Assert.True(token.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.False(token.IsUsed);
    }

    [Fact]
    public void Issue_PublicTier_ExpiresIn24Hours()
    {
        CapabilityToken token = CapabilityToken.Issue(
            RequesterId, OwnerId, Resource, Action, SensitivityTier.Public, null);

        Assert.True(token.ExpiresAt > DateTimeOffset.UtcNow.AddHours(23));
        Assert.True(token.ExpiresAt <= DateTimeOffset.UtcNow.AddHours(24).AddSeconds(5));
    }

    [Fact]
    public void IsValid_ReturnsTrueForFreshActiveToken()
    {
        CapabilityToken token = CapabilityToken.Issue(
            RequesterId, OwnerId, Resource, Action, SensitivityTier.Standard, null);

        Assert.True(token.IsValid());
    }

    [Fact]
    public void IsValid_ReturnsFalseWhenRevoked()
    {
        CapabilityToken token = CapabilityToken.Issue(
            RequesterId, OwnerId, Resource, Action, SensitivityTier.Standard, null);

        token.Revoke();

        Assert.False(token.IsValid());
        Assert.Equal(CapabilityTokenStatus.Revoked, token.Status);
    }

    [Fact]
    public void MarkUsed_OnSensitiveTier_ConsumesToken()
    {
        CapabilityToken token = CapabilityToken.Issue(
            RequesterId, OwnerId, Resource, Action, SensitivityTier.Sensitive, null);

        token.MarkUsed();

        Assert.Equal(CapabilityTokenStatus.Consumed, token.Status);
        Assert.True(token.IsUsed);
        Assert.False(token.IsValid());
    }

    [Fact]
    public void MarkUsed_OnPublicTier_LeavesStatusActive()
    {
        CapabilityToken token = CapabilityToken.Issue(
            RequesterId, OwnerId, Resource, Action, SensitivityTier.Public, null);

        token.MarkUsed();

        Assert.Equal(CapabilityTokenStatus.Active, token.Status);
        Assert.True(token.IsUsed);
        // IsValid returns false because IsUsed=true, even though Status is Active
        Assert.False(token.IsValid());
    }

    [Fact]
    public void Revoke_SetsStatusToRevoked()
    {
        CapabilityToken token = CapabilityToken.Issue(
            RequesterId, OwnerId, Resource, Action, SensitivityTier.Standard, null);

        token.Revoke();

        Assert.Equal(CapabilityTokenStatus.Revoked, token.Status);
    }
}
