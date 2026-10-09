namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The input to <see cref="IErrorReportService.CreateAsync"/> — the six
/// members the Web layer binds from the 500 error page (the resident's
/// subject + description + optional contact email) and the request context
/// (request ID + exception type + user agent). Never a doc; never stored
/// directly (CreateAsync projects it onto an ErrorReport row).
/// </summary>
public sealed record ErrorReportDraft(
    string SubjectId,          // ClaimTypes.Subject; string.Empty when anonymous
    string Description,        // required — the resident's free-text "what were you doing"
    string? ContactEmail,      // optional — for anonymous follow-up; never mailed in M31
    string RequestId,          // the HttpContext.TraceIdentifier, for log correlation
    string? ExceptionType,     // from IExceptionHandlerFeature; null if the feature is absent
    string? UserAgent);        // the browser UA, truncated to 256 chars by the caller
