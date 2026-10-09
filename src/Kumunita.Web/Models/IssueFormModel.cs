namespace Kumunita.Web.Models;

/// <summary>
/// The small bind model for the public general issue-submission form
/// (<c>POST /issues/new</c>, M32 / ADR 0155). The Web layer binds the two
/// resident-supplied fields; the request context (request ID, user agent) is
/// added by the controller when it projects this onto the
/// <see cref="Kumunita.Core.ErrorReports.ErrorReportDraft"/> it passes to
/// <see cref="Kumunita.Core.ErrorReports.IErrorReportService.CreateAsync"/>.
/// </summary>
/// <remarks>
/// <b>Blank description is a 400 re-render, never a 500 (M32·4).</b> The
/// <see cref="Description"/> field is required + non-blank: the controller
/// checks <c>ModelState</c> and re-renders the form with the resident's
/// already-typed values preserved rather than calling the write lane. The
/// optional <see cref="ContactEmail"/> is captured for the GlobalAdmin to
/// read (the escalation lane may forward it). The form is <b>public</b> (no
/// <c>[Authorize]</c> gate, M32·4 — the M31·2 500-form precedent): an
/// anonymous visitor can file a general issue without signing in.
/// </remarks>
public class IssueFormModel
{
    /// <summary>The resident's free-text description of the issue (required,
    /// non-blank — the form-level 400 re-render guard; the register's U04
    /// "validate (Description required, non-blank)" pin). Bound from the
    /// <c>issue.description.*</c> kw-l labelled textarea.</summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(2000)]
    public string Description { get; set; } = "";

    /// <summary>The optional contact email for follow-up. Bound from the
    /// <c>issue.email.*</c> kw-l labelled input; an invalid email is a
    /// form-level 400 re-render, not a crash.</summary>
    [System.ComponentModel.DataAnnotations.EmailAddress]
    [System.ComponentModel.DataAnnotations.MaxLength(254)]
    public string? ContactEmail { get; set; }

    /// <summary>True after a successful submission — the view renders the
    /// <c>issue.thanks</c> confirmation instead of the form (the M31·2
    /// "no redirect, no modal" shape, M32·4).</summary>
    public bool FormSubmitted { get; set; }
}
