using Kumunita.Shared.Kernel;

namespace Kumunita.Authorization.Domain;

public class AuditEntry
{
    public Guid Id { get; private set; }

    public AuditEventType EventType { get; private set; }

    // Who made the request
    public UserId RequesterId { get; private set; }

    // Whose data was involved
    public UserId OwnerId { get; private set; }

    public string ResourceTypeName { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public SensitivityTier SensitivityTier { get; private set; }

    // Token involved — null for denied requests
    public CapabilityTokenId? TokenId { get; private set; }

    // Why it was denied — null for successful grants
    public string? DenialReason { get; private set; }

    // Context for the request
    public string? RequestContext { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
        = DateTimeOffset.UtcNow;

    private AuditEntry() { }

    public static AuditEntry ForTokenIssued(CapabilityToken token)
        => new()
        {
            Id = Guid.NewGuid(),
            EventType = AuditEventType.TokenIssued,
            RequesterId = token.RequesterId,
            OwnerId = token.OwnerId,
            ResourceTypeName = token.ResourceTypeName,
            Action = token.Action,
            SensitivityTier = token.SensitivityTier,
            TokenId = token.Id,
            RequestContext = token.RequestContext
        };

    public static AuditEntry ForTokenDenied(
        UserId requesterId,
        UserId ownerId,
        string resourceTypeName,
        string action,
        SensitivityTier sensitivityTier,
        string reason,
        string? requestContext)
        => new()
        {
            Id = Guid.NewGuid(),
            EventType = AuditEventType.TokenDenied,
            RequesterId = requesterId,
            OwnerId = ownerId,
            ResourceTypeName = resourceTypeName,
            Action = action,
            SensitivityTier = sensitivityTier,
            DenialReason = reason,
            RequestContext = requestContext
        };

    public static AuditEntry ForTokenUsed(CapabilityToken token)
        => new()
        {
            Id = Guid.NewGuid(),
            EventType = AuditEventType.TokenUsed,
            RequesterId = token.RequesterId,
            OwnerId = token.OwnerId,
            ResourceTypeName = token.ResourceTypeName,
            Action = token.Action,
            SensitivityTier = token.SensitivityTier,
            TokenId = token.Id
        };

    public static AuditEntry ForTokenRevoked(CapabilityToken token)
        => new()
        {
            Id = Guid.NewGuid(),
            EventType = AuditEventType.TokenRevoked,
            RequesterId = token.RequesterId,
            OwnerId = token.OwnerId,
            ResourceTypeName = token.ResourceTypeName,
            Action = token.Action,
            SensitivityTier = token.SensitivityTier,
            TokenId = token.Id
        };
}

public enum AuditEventType
{
    TokenIssued,
    TokenDenied,
    TokenUsed,
    TokenRevoked
}