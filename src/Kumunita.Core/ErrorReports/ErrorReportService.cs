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

    /// <summary>
    /// The escalation-authorization seam (ADR 0159, U04) — present on an
    /// instance with the ESC lane wired (the DI line), <c>null</c> on a
    /// test-construction site that builds the service with only the store
    /// (the M31 localization / translationProvider floor precedent). When
    /// <c>null</c>, <see cref="AcceptInboundAsync"/> short-circuits to
    /// <c>Error: "token service absent"</c> so those sites keep compiling.
    /// </summary>
    private readonly IEscalationTokenService? _tokenService;

    public ErrorReportService(IDocumentStore store, IEscalationTokenService? tokenService = null)
    {
        _store        = store;
        _tokenService = tokenService;
    }

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
            TriageStatus  = "new",            // the M31 floor; TriagedAt/TriagedBy stay null
            // M32 additive projection (M32·4) — draft.Origin is "error-page"
            // (the M31 500 form default, the M32-3 FACE) or "general" (the
            // M32 /issues/new form). ResolvedAt/ResolvedBy/ResolutionNote
            // stay null until MarkResolvedAsync (M32·8).
            Origin        = draft.Origin
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

    /// <summary>
    /// Stamp a new/triaged report resolved + exactly one AccessAudit row
    /// (one session). Returns null (a no-op — no audit row, no state
    /// change) when the report is missing or already resolved (M32·8
    /// idempotency pin, the M31·6 MarkTriagedAsync shape verbatim).
    /// </summary>
    public async Task<ErrorReport?> MarkResolvedAsync(string reportId, string actorId, string? resolutionNote, CancellationToken ct = default)
    {
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var stored = await session.LoadAsync<ErrorReport>(reportId, ct).ConfigureAwait(false);
        if (stored is null || stored.TriageStatus == "resolved")
        {
            return null; // no-op — a missing or already-resolved report (M32·8)
        }
        stored.TriageStatus   = "resolved";
        stored.ResolvedAt     = DateTimeOffset.UtcNow;
        stored.ResolvedBy     = actorId;
        stored.ResolutionNote = resolutionNote;
        session.Store(stored);
        // Exactly one AccessAudit row (Via = Admin — the M32·8 pin).
        session.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = DateTimeOffset.UtcNow,
            ActorId              = actorId,
            EffectivePrincipalId = actorId,
            Action               = "errorreport.resolve",
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

    /// <summary>
    /// Accept an inbound (escalated) report (ADR 0159, ESC·5 / ESC·7) — the
    /// M32 <c>MarkResolvedAsync</c> idempotent-write-lane shape (the ADR 0006
    /// C3 single-write-lane): re-validate the token → the idempotent lookup →
    /// the blank-description guard → store the received row + **one**
    /// <c>AccessAudit</c> row (<c>Via = Escalation</c>) in one write session.
    /// The M31 3 + the M32 1 methods are **unchanged** (ESC·1). Core stays
    /// HTTP-free (ESC·6 / ADR 0006-D): the Web inbound endpoint resolved the
    /// presented <c>Bearer</c> plaintext to <paramref name="draft"/>.
    /// <c>TokenId</c> / <c>FromInstance</c> before this call.
    /// </summary>
    public async Task<InboundResult> AcceptInboundAsync(InboundReport draft, CancellationToken ct = default)
    {
        if (_tokenService is null)
        {
            return new InboundResult(Created: false, Row: null!, Error: "token service absent");
        }

        // ESC·5 — confirm the token row still exists and is non-revoked before
        // creating a row (the Web endpoint resolved the Bearer plaintext to
        // draft.TokenId via ValidateAsync; a revoked / unknown token →
        // "invalid token", which the Web endpoint maps to a 401 — no row, no
        // audit row). ESC·7 — the idempotent lookup on (EscalationTokenId,
        // EscalationSourceId): a re-delivery of the same origin report under
        // the same token returns the existing row, not a duplicate.
        using (var session = _store.QuerySession())
        {
            var token = await session.LoadAsync<EscalationToken>(draft.TokenId, ct).ConfigureAwait(false);
            if (token is null || token.RevokedAt is not null)
            {
                return new InboundResult(Created: false, Row: null!, Error: "invalid token");
            }
            var existing = await session.Query<ErrorReport>()
                .Where(r => r.EscalationTokenId == draft.TokenId && r.EscalationSourceId == draft.SourceReportId)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (existing is not null)
            {
                return new InboundResult(Created: false, Row: existing, Error: null);
            }
        }

        // ESC·5 / M32·4 — the blank-description guard (no row, no audit row).
        if (string.IsNullOrWhiteSpace(draft.Description))
        {
            return new InboundResult(Created: false, Row: null!, Error: "description required");
        }

        // ESC·5 — store the received row + exactly one AccessAudit row in ONE
        // write session (the ADR 0006 C3 single-write-lane, the M32·8
        // precedent).
        var now = DateTimeOffset.UtcNow;
        var report = new ErrorReport
        {
            Id                   = Guid.NewGuid().ToString("N"),
            SubjectId            = draft.OriginSubjectId,
            Description          = draft.Description,
            ContactEmail         = draft.ContactEmail,
            RequestId            = draft.RequestId,
            ExceptionType        = draft.ExceptionType,
            // UserAgent stays null — the origin's payload doesn't carry a UA
            // (the M32 8-field forward shape); the triage / resolve fields
            // stay null until the local admin acts on the row.
            Created              = draft.OriginCreated,
            TriageStatus         = "new",              // the M31 default
            Origin               = "escalated",        // the ESC·2 additive value
            FromInstance         = draft.FromInstance, // the accepting token's Label (ESC·8 display)
            EscalationReceivedAt = now,
            EscalationSourceId   = draft.SourceReportId,
            EscalationTokenId    = draft.TokenId
        };

        await using var writeSession = _store.OpenSession(new Marten.Services.SessionOptions());
        writeSession.Store(report);
        // Exactly one AccessAudit row (Via = Escalation — the ESC·10 pin; the
        // M2M standing: the receiving admin is not the actor, the origin
        // platform is).
        writeSession.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = now,
            ActorId              = draft.OriginSubjectId,
            EffectivePrincipalId = draft.OriginSubjectId,
            Action               = "errorreport.inbound",
            TargetKind           = "error-report",
            TargetId             = report.Id,
            Via                  = AccessVia.Escalation,
            Outcome              = AccessOutcome.Allow
        });
        await writeSession.SaveChangesAsync(ct).ConfigureAwait(false);
        return new InboundResult(Created: true, Row: report, Error: null);
    }
}
