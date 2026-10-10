namespace Kumunita.Core.ErrorReports;

/// <summary>
/// One platform-error report row (ADR 0154 + ADR 0155 — a platform-error
/// signal, NOT a content-moderation report; the Posts/Report doc, ADR 0023,
/// is untouched). One row per report (the Usage/UsageEvent row-per-event
/// shape; the Id is the conventional string identity, Marten-generated).
/// The field set below is the **19-member ESC ceiling** (ADR 0159 D1, ESC·2)
/// — the M32 15 (ADR 0155 D1) unchanged (the M31 11 [ADR 0154 D1] + the M32
/// additive 4 [M32·3]) + the ESC additive 4 (ESC·2). No field outside this
/// set may appear in the doc; no M31 / M32 field is re-shaped (M32·1 /
/// M32·3 / ESC·2). The four ESC additive fields are each null for
/// locally-filed rows.
/// </summary>
public sealed class ErrorReport
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The report's subject: ClaimTypes.Subject; string.Empty when anonymous.</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>The resident's free-text "what were you trying to do" — required, non-blank.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Optional contact email (anonymous follow-up); never mailed in M31.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>The HttpContext.TraceIdentifier, for log correlation.</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>The exception type (e.g. NullReferenceException); null if IExceptionHandlerFeature is absent.</summary>
    public string? ExceptionType { get; set; }

    /// <summary>The browser UA, truncated to 256 chars by the caller; null if absent.</summary>
    public string? UserAgent { get; set; }

    /// <summary>The report's creation instant, UTC.</summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// The triage state. CLOSED IN M31 to {"new", "triaged"} (D2). M32
    /// adds "resolved" additively (ADR 0004 §B.1 — a string field, no
    /// migration); M32 does not re-shape these two.
    /// </summary>
    public string TriageStatus { get; set; } = "new";

    /// <summary>The triage instant, UTC; null until triaged.</summary>
    public DateTimeOffset? TriagedAt { get; set; }

    /// <summary>The GlobalAdmin's ClaimTypes.Subject who triaged; null until triaged.</summary>
    public string? TriagedBy { get; set; }

    // ── M32's additive 4 (ADR 0155 D1, M32·3 — the M31 11 above unchanged) ──

    /// <summary>
    /// Where the report was filed. CLOSED SET (M32·4) extended
    /// additively in ESC to {"error-page","general","escalated"} (ESC·2)
    /// — the M31 500 form writes "error-page" (the default), the M32
    /// /issues/new form writes "general", and a received (escalated) row
    /// writes "escalated" (the accepting token's origin). The two M31 / M32
    /// values keep their exact strings. A string field — ADR 0004 §B.1
    /// idempotent delta at boot, no migration.
    /// </summary>
    public string Origin { get; set; } = "error-page";

    /// <summary>The resolution instant, UTC; null until resolved (M32·8).</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The GlobalAdmin's ClaimTypes.Subject who resolved; null until resolved (M32·8).</summary>
    public string? ResolvedBy { get; set; }

    /// <summary>The admin's free-text "what was done"; null until resolved (M32·8).</summary>
    public string? ResolutionNote { get; set; }

    // ── ESC's additive 4 (ADR 0159 D1, ESC·2 — the M32 15 above unchanged) ──
    // Each is null for locally-filed rows; a non-null FromInstance marks a
    // received (escalated) row (Origin == "escalated").

    /// <summary>The accepting token's Label — the origin platform's identity (ESC·2 / ESC·8); null for locally-filed.</summary>
    public string? FromInstance { get; set; }

    /// <summary>The receive instant, UTC (ESC·2); null for locally-filed.</summary>
    public DateTimeOffset? EscalationReceivedAt { get; set; }

    /// <summary>The origin's report id — the inbound idempotency key (ESC·2 / ESC·7); null for locally-filed.</summary>
    public string? EscalationSourceId { get; set; }

    /// <summary>Which EscalationToken accepted it (ESC·2 / ESC·7); null for locally-filed.</summary>
    public string? EscalationTokenId { get; set; }
}
