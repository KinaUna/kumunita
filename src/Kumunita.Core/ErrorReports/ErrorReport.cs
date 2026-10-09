namespace Kumunita.Core.ErrorReports;

/// <summary>
/// One platform-error report row (ADR 0154 + ADR 0155 — a platform-error
/// signal, NOT a content-moderation report; the Posts/Report doc, ADR 0023,
/// is untouched). One row per report (the Usage/UsageEvent row-per-event
/// shape; the Id is the conventional string identity, Marten-generated).
/// The field set below is the **15-member M32 ceiling** (ADR 0155 D1) — the
/// M31 11 (ADR 0154 D1) unchanged + the M32 additive 4 (M32·3). No field
/// outside this set may appear in the doc; no M31 field is re-shaped
/// (M32·1 / M32·3).
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
    /// Where the report was filed. CLOSED SET {"error-page","general"}
    /// (M32·4) — the M31 500 form writes "error-page" (the default), the
    /// M32 /issues/new form writes "general". A string field — ADR 0004
    /// §B.1 idempotent delta at boot, no migration.
    /// </summary>
    public string Origin { get; set; } = "error-page";

    /// <summary>The resolution instant, UTC; null until resolved (M32·8).</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The GlobalAdmin's ClaimTypes.Subject who resolved; null until resolved (M32·8).</summary>
    public string? ResolvedBy { get; set; }

    /// <summary>The admin's free-text "what was done"; null until resolved (M32·8).</summary>
    public string? ResolutionNote { get; set; }
}
