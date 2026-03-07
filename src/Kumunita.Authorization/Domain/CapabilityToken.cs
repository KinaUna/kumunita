using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Domain;

namespace Kumunita.Authorization.Domain;

public class CapabilityToken : IAuditableEntity
{
    public CapabilityTokenId Id { get; private set; }

    // Who is requesting access
    public UserId RequesterId { get; private set; }

    // Whose data is being accessed
    public UserId OwnerId { get; private set; }

    // What is being accessed
    public string ResourceTypeName { get; private set; } = string.Empty;

    // What action is being performed
    public string Action { get; private set; } = string.Empty;

    // Sensitivity tier — determines token lifetime
    public SensitivityTier SensitivityTier { get; private set; }

    // Token state
    public CapabilityTokenStatus Status { get; private set; }

    // Expiry — set based on sensitivity tier at issuance
    public DateTimeOffset ExpiresAt { get; private set; }

    // Single-use tracking for Sensitive and Restricted tiers
    public bool IsUsed { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }

    // Context — what triggered this request (for audit trail)
    public string? RequestContext { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
        = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; }
        = DateTimeOffset.UtcNow;

    private CapabilityToken() { }

    public static CapabilityToken Issue(
        UserId requesterId,
        UserId ownerId,
        string resourceTypeName,
        string action,
        SensitivityTier sensitivityTier,
        string? requestContext)
    {
        var expiresAt = sensitivityTier switch
        {
            SensitivityTier.Public => DateTimeOffset.UtcNow.AddHours(24),
            SensitivityTier.Standard => DateTimeOffset.UtcNow.AddMinutes(60),
            SensitivityTier.Sensitive => DateTimeOffset.UtcNow.AddSeconds(30),
            SensitivityTier.Restricted => DateTimeOffset.UtcNow.AddSeconds(30),
            _ => DateTimeOffset.UtcNow.AddMinutes(5)
        };

        return new CapabilityToken
        {
            Id = new CapabilityTokenId(Guid.NewGuid()),
            RequesterId = requesterId,
            OwnerId = ownerId,
            ResourceTypeName = resourceTypeName,
            Action = action,
            SensitivityTier = sensitivityTier,
            Status = CapabilityTokenStatus.Active,
            ExpiresAt = expiresAt,
            RequestContext = requestContext
        };
    }

    public bool IsValid()
        => Status == CapabilityTokenStatus.Active
        && !IsUsed
        && DateTimeOffset.UtcNow < ExpiresAt;

    public void MarkUsed()
    {
        IsUsed = true;
        UsedAt = DateTimeOffset.UtcNow;
        // Single-use tiers are immediately invalidated after use
        if (SensitivityTier >= SensitivityTier.Sensitive)
            Status = CapabilityTokenStatus.Consumed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Revoke()
    {
        Status = CapabilityTokenStatus.Revoked;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum CapabilityTokenStatus
{
    Active,
    Consumed,  // used once — Sensitive/Restricted tiers
    Expired,
    Revoked    // administrative revocation
}