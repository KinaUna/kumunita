namespace Kumunita.Core.ErrorReports;

/// <summary>
/// One platform-error report row (ADR 0154, M31·1 — a platform-error
/// signal, NOT a content-moderation report; the Posts/Report doc, ADR 0023,
/// is untouched). One row per report (the Usage/UsageEvent row-per-event
/// shape; the Id is the conventional string identity, Marten-generated).
/// The field set below is the 11-member ceiling (D1) — no field outside
/// this set may appear in the doc.
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
}
