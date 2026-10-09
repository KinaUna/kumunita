using Kumunita.Core.Authorization;
using Marten;

namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The read + two audited-write lanes for the ErrorReport doc (ADR 0154).
/// Composes the host-registered Marten IDocumentStore (the
/// AdminOnboardingService / SiteContentService shape) — reads open their
/// own QuerySession; each audited write opens one write IDocumentSession
/// and commits the doc + its single AccessAudit row together (C3).
/// </summary>
public sealed class ErrorReportService : IErrorReportService
{
    private readonly IDocumentStore _store;

    public ErrorReportService(IDocumentStore store) => _store = store;

    /// <summary>Store one ErrorReport row (new) + exactly one AccessAudit row (one session).</summary>
    public async Task<ErrorReport> CreateAsync(ErrorReportDraft draft, CancellationToken ct = default)
    {
        var report = new ErrorReport
        {
            Id            = Guid.NewGuid().ToString("N"),
            SubjectId     = draft.SubjectId,
            Description   = draft.Description,
            ContactEmail  = draft.ContactEmail,
            RequestId     = draft.RequestId,
            ExceptionType = draft.ExceptionType,
            UserAgent     = draft.UserAgent,
            Created       = DateTimeOffset.UtcNow,
            TriageStatus  = "new"             // the M31 floor; TriagedAt/TriagedBy stay null
        };

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(report);
        // Exactly one AccessAudit row (the §2.1 Via pin) — the SiteContentService
        // / AdminOnboardingService field set. Via = Resident (non-blank
        // SubjectId) or Anonymous (blank SubjectId, the new additive value).
        session.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = DateTimeOffset.UtcNow,
            ActorId              = draft.SubjectId,
            EffectivePrincipalId = draft.SubjectId,
            Action               = "errorreport.create",
            TargetKind           = "error-report",
            TargetId             = report.Id,
            Via                  = string.IsNullOrEmpty(draft.SubjectId)
                                       ? AccessVia.Anonymous   // §2.1 pin (new additive value)
                                       : AccessVia.Resident,    // §2.1 pin (ADR 0041)
            Outcome              = AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return report;
    }

    /// <summary>
    /// Stamp a new report triaged + exactly one AccessAudit row (one
    /// session). Returns null (a no-op — no audit row, no state change)
    /// when the report is missing or already triaged (M31·6).
    /// </summary>
    public async Task<ErrorReport?> MarkTriagedAsync(string reportId, string actorId, CancellationToken ct = default)
    {
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var stored = await session.LoadAsync<ErrorReport>(reportId, ct).ConfigureAwait(false);
        if (stored is null || stored.TriageStatus == "triaged")
        {
            return null; // no-op — a missing or already-triaged report (M31·6)
        }
        stored.TriageStatus = "triaged";
        stored.TriagedAt    = DateTimeOffset.UtcNow;
        stored.TriagedBy    = actorId;
        session.Store(stored);
        // Exactly one AccessAudit row (Via = Admin — the §2.1 pin).
        session.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = DateTimeOffset.UtcNow,
            ActorId              = actorId,
            EffectivePrincipalId = actorId,
            Action               = "errorreport.triage",
            TargetKind           = "error-report",
            TargetId             = reportId,
            Via                  = AccessVia.Admin,
            Outcome              = AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return stored;
    }

    /// <summary>Newest-first listing (Created DESC); a read, never audited (M31·4).</summary>
    public async Task<IReadOnlyList<ErrorReport>> ListAsync(int maxCount = 100, CancellationToken ct = default)
    {
        using var session = _store.QuerySession();
        return await session.Query<ErrorReport>()
            .OrderByDescending(x => x.Created)   // the repo's pinned ordering shape (AnnouncementService/MessagingService/UserInfoService .OrderByDescending(.Created))
            .Take(maxCount)
            .ToListAsync(ct).ConfigureAwait(false);
    }
}
