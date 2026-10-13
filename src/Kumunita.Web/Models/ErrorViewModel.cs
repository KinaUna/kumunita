namespace Kumunita.Web.Models;

public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    // ── M31 (ADR 0154) — the 500-page report-an-issue affordance (M31·2) ──
    // The exception captured from IExceptionHandlerFeature (display metadata
    // only — never re-thrown, never gates the page).

    /// <summary>The exception type (e.g. <c>NullReferenceException</c>)
    /// captured from <see cref="Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature"/>;
    /// null when the feature is absent (e.g. a directly-visited /Home/Error).</summary>
    public string? ExceptionType { get; set; }

    /// <summary>The exception message (the <c>Exception.Message</c> text) —
    /// display metadata for the resident's report context; never re-thrown.</summary>
    public string? ExceptionMessage { get; set; }

    /// <summary>True after the resident submits the report form — the view
    /// then renders the <c>errorreport.thanks</c> confirmation in place of the
    /// form (no redirect, no modal — M31·2). Default false (M31-7: a page the
    /// resident did not submit renders the form unchanged).</summary>
    public bool FormSubmitted { get; set; }

    /// <summary>The resident's free-text "what were you trying to do" — echoed
    /// back into the form on a validation re-render (a blank description is a
    /// 400 re-render, never a 500). Default empty.</summary>
    public string FormDescription { get; set; } = "";

    /// <summary>The optional contact email the resident supplied for anonymous
    /// follow-up; never mailed in M31 (M32's escalation lane may add that).
    /// Echoed back on a validation re-render.</summary>
    public string? FormContactEmail { get; set; }
}
