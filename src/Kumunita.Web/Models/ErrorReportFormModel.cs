namespace Kumunita.Web.Models;

/// <summary>
/// The small bind model for the 500 error page's report form
/// (<c>POST /Home/Error/Report</c>, M31 / ADR 0154). The Web layer binds the
/// two resident-supplied fields; the request context (request ID, exception
/// type, user agent) is added by the controller when it projects this onto
/// the <see cref="Kumunita.Core.ErrorReports.ErrorReportDraft"/> it passes to
/// <see cref="Kumunita.Core.ErrorReports.IErrorReportService.CreateAsync"/>.
/// </summary>
/// <remarks>
/// <b>Blank description is a 400 re-render, never a 500 (M31·5).</b> The
/// <see cref="Description"/> field is required + non-blank: the controller
/// checks <c>ModelState</c> and re-renders the error view with the form
/// pre-filled rather than calling the write lane. The optional
/// <see cref="ContactEmail"/> is never mailed in M31 — it is captured for the
/// GlobalAdmin to read (M32's escalation lane may forward it). The form is
/// <b>public</b> (no <c>[Authorize]</c> gate, M31·2 / M31·9): an anonymous
/// visitor who hits a 500 can file a report.
/// </remarks>
public class ErrorReportFormModel
{
    /// <summary>The resident's free-text "what were you trying to do when the
    /// error happened" (required, non-blank — the form-level 400 re-render
    /// guard; the register's U04 "validate (Description required non-blank)"
    /// pin). Bound from the <c>errorreport.description.*</c> kw-l labelled
    /// textarea.</summary>
    [System.ComponentModel.DataAnnotations.Required]
    public string Description { get; set; } = "";

    /// <summary>The optional contact email for anonymous follow-up (never
    /// mailed in M31). Bound from the <c>errorreport.email.*</c> kw-l labelled
    /// input; an invalid email is a form-level 400 re-render, not a crash.</summary>
    [System.ComponentModel.DataAnnotations.EmailAddress]
    [System.ComponentModel.DataAnnotations.MaxLength(254)]
    public string? ContactEmail { get; set; }
}
