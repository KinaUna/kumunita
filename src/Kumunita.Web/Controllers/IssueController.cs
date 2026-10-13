using System.Diagnostics;
using Kumunita.Core.ErrorReports;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M32 (ADR 0155) — the public general issue-submission surface
/// (<c>GET /issues/new</c> + <c>POST /issues/new</c>). A resident — signed-in
/// or anonymous — files an issue that is not tied to an error page.
/// </summary>
/// <remarks>
/// <b>Public, anonymous-safe, non-blocking (M32·4).</b> There is deliberately
/// no <c>[Authorize]</c> gate — the M31·2 500-report-form precedent: an
/// anonymous visitor's <c>SubjectId</c> is the empty string, and the U03
/// write lane maps that to the <c>AccessVia.Anonymous</c> audit row. A blank
/// <see cref="IssueFormModel.Description"/> is a form-level 400 re-render,
/// never a 500 back to the resident. The draft is created with
/// <c>Origin = "general"</c> + <c>ExceptionType = null</c> (the M32·4 pin —
/// the general issue is not tied to an error page, unlike the M31 500 form's
/// <c>"error-page"</c> default).
/// </remarks>
public class IssueController : Controller
{
    private readonly IErrorReportService _errorReports;

    public IssueController(IErrorReportService errorReports)
    {
        _errorReports = errorReports;
    }

    /// <summary>
    /// <c>GET /issues/new</c> — render the public issue-submission form.
    /// No <c>[Authorize]</c> gate (M32·4).
    /// </summary>
    [HttpGet("/issues/new")]
    public IActionResult GetNew()
    {
        return View("New", new IssueFormModel());
    }

    /// <summary>
    /// <c>POST /issues/new</c> — bind the resident's description + optional
    /// contact email; on a valid model, store one
    /// <see cref="Kumunita.Core.ErrorReports.ErrorReport"/> row + exactly one
    /// <c>AccessAudit</c> row through the U03
    /// <see cref="IErrorReportService.CreateAsync"/> write lane with
    /// <c>Origin = "general"</c> (M32·4), then re-render the form view with
    /// <see cref="IssueFormModel.FormSubmitted"/> = true (the
    /// <c>issue.thanks</c> confirmation — no redirect, no modal, the M31·2
    /// shape). A blank description re-renders the form pre-filled (400,
    /// never a 500).
    /// </summary>
    [HttpPost("/issues/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostNew(IssueFormModel form)
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var userAgent = Truncate(HttpContext.Request.Headers.UserAgent.ToString(), 256);
        string subjectId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;

        if (!ModelState.IsValid)
        {
            // A form-level validation error: re-render the form with the
            // resident's already-typed values preserved — never a 500 (M32·4).
            return View("New", new IssueFormModel
            {
                Description = form.Description,
                ContactEmail = form.ContactEmail,
                FormSubmitted = false,
            });
        }

        var draft = new ErrorReportDraft(
            SubjectId: subjectId,
            Description: form.Description,
            ContactEmail: string.IsNullOrWhiteSpace(form.ContactEmail) ? null : form.ContactEmail,
            RequestId: requestId,
            ExceptionType: null, // a general issue is not tied to an error page (M32·4)
            UserAgent: userAgent,
            Origin: "general");  // the M32·4 pin

        try
        {
            _ = await _errorReports.CreateAsync(draft).ConfigureAwait(false);
        }
        catch
        {
            // Swallow: a report failure must never surface as a 500 to the
            // resident (the M31·5 "never a 500 back to the resident" pin).
            // The confirmation still renders.
        }

        return View("New", new IssueFormModel { FormSubmitted = true });
    }

    /// <summary>Truncates a value to <paramref name="max"/> chars (the
    /// user-agent cap, the M31 register's U04 shape); null-safe (null in,
    /// null out).</summary>
    private static string? Truncate(string? value, int max) =>
        value is null ? null : (value.Length <= max ? value : value[..max]);
}
