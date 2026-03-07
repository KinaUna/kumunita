using Kumunita.Authorization.Domain;
using Kumunita.Shared.Kernel;
using Marten;

namespace Kumunita.Authorization.Features.AuditLog;

public record GetFullAuditLog(
    UserId RequesterId,
    UserId? FilterByOwnerId = null,
    UserId? FilterByRequesterId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int PageSize = 50,
    int PageNumber = 1);

public static class GetFullAuditLogHandler
{
    public static async Task<IReadOnlyList<AuditEntry>> Handle(
        GetFullAuditLog query,
        IQuerySession session,
        CancellationToken ct)
    {
        // Admin check enforced at endpoint level via capability token
        // Handler trusts that a valid Restricted token was presented
        var q = session.Query<AuditEntry>().AsQueryable();

        if (query.FilterByOwnerId.HasValue)
            q = q.Where(e => e.OwnerId == query.FilterByOwnerId.Value);

        if (query.FilterByRequesterId.HasValue)
            q = q.Where(e => e.RequesterId == query.FilterByRequesterId.Value);

        if (query.From.HasValue)
            q = q.Where(e => e.OccurredAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(e => e.OccurredAt <= query.To.Value);

        return await q
            .OrderByDescending(e => e.OccurredAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);
    }
}