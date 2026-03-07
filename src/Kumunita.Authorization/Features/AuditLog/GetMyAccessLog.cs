using Kumunita.Authorization.Domain;
using Kumunita.Identity.Domain;
using Kumunita.Shared.Kernel;
using Marten;

namespace Kumunita.Authorization.Features.AuditLog;

public record GetMyAccessLog(
    UserId RequesterId,
    int PageSize = 20,
    int PageNumber = 1);

public record AccessLogEntry(
    AuditEventType EventType,
    // Requester identity is deliberately obscured for non-admin viewers
    // Users see "a member" or "an admin" not the specific user ID
    string RequesterLabel,
    string ResourceTypeName,
    string Action,
    SensitivityTier SensitivityTier,
    DateTimeOffset OccurredAt);

public static class GetMyAccessLogHandler
{
    public static async Task<IReadOnlyList<AccessLogEntry>> Handle(
        GetMyAccessLog query,
        IQuerySession session,
        CancellationToken ct)
    {
        var entries = await session
            .Query<AuditEntry>()
            .Where(e => e.OwnerId == query.RequesterId)
            .OrderByDescending(e => e.OccurredAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        // Load requester's own state to check if they're admin
        var requesterState = await session
            .LoadAsync<UserAuthorizationState>(query.RequesterId, ct);

        return entries.Select(e => new AccessLogEntry(
            e.EventType,
            // Users see a label, not the actual requester ID
            // This protects admin identity while giving transparency
            RequesterLabel: GetRequesterLabel(e, requesterState),
            e.ResourceTypeName,
            e.Action,
            e.SensitivityTier,
            e.OccurredAt)).ToList();
    }

    private static string GetRequesterLabel(
        AuditEntry entry,
        UserAuthorizationState? viewerState)
    {
        // If viewer is admin, show actual requester ID
        if (viewerState?.HasRole(AppRole.SystemRoles.Admin) == true)
            return entry.RequesterId.Value.ToString();

        // Owner accessing their own data
        if (entry.RequesterId == entry.OwnerId)
            return "You";

        // Otherwise obscure the identity
        return entry.SensitivityTier >= SensitivityTier.Sensitive
            ? "An administrator"
            : "A member";
    }
}