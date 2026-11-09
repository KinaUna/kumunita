using System.Security.Claims;
using Kumunita.Core.ErrorReports;
using Kumunita.Core.Localization;
using Kumunita.Core.Moderation;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>GET /reports/mine</c> surface (IMPROVE-UX R06 — the D1 "My reports"
/// shape's Web side): the signed-in resident's own reports, with two stacked
/// sections (content reports + platform issues), a friendly empty state when
/// the resident has filed neither, and a link in the account menu
/// (<c>_AccountNav.cshtml</c>).
/// <para>
/// **Two read seams (R05), one action, one route.** The
/// <see cref="IModerationService"/> seam's
/// <c>ListMyReportsAsync(reporterId)</c> (R05 — the reporter's content
/// reports, the row shape <see cref="MyReportRow"/>) and the
/// <see cref="IErrorReportService"/> seam's
/// <c>ListMineAsync(subjectId)</c> (R05 — the filer's platform issues,
/// the raw <see cref="ErrorReport"/> docs). Both are **reads, not
/// decisions** (R05's seams already pin that: zero
/// <see cref="IAuthorizationService"/> passes, zero <c>AccessAudit</c>
/// rows) — the controller passes the signed-in subject's
/// <see cref="KumunitaPrincipal.SubjectId"/> to both. The ADR 0105 /
/// M17 <see cref="BookmarksController"/> "operator has no read standing"
/// gate: a caller with no <c>SubjectId</c> gets a non-leaky
/// <see cref="NotFoundResult"/> — neither seam is called (the sole-reader
/// pin, C-M17·1 carried to the my-reports surface: the controller never
/// re-derives access, and a caller with no read standing has no read to
/// delegate).
/// </para>
/// <para>
/// **R07 / R08 anchor ids.** Each content-report row carries
/// <c>id="r-{ReportId}"</c> and each issue row carries
/// <c>id="i-{Id}"</c> (in the view) so the R07 <c>ReportResolved</c> link
/// (<c>/reports/mine#r-{id}</c>) and the R08 <c>issue.resolved</c> link
/// (<c>/reports/mine#i-{id}</c>) land on the right row without a follow-on
/// refactor.
/// </para>
/// <para>
/// **The view-model is inline** (a sealed record in this file, not a new
/// <c>Models/</c> class): the surface has exactly two lists + one derived
/// boolean; the two read seams' row shapes are already Core's
/// (<see cref="MyReportRow"/> / <see cref="ErrorReport"/>), so the
/// view-model is a thin holder the view can destructure directly.
/// </para>
/// <para>
/// **Translation keys.** A new <c>reports.*</c> namespace (this unit adds
/// the first surface for it — the ADR 0289 C02 embedded-resource loader
/// merges every <c>Localization/Keys/{lang}/*.json</c> file; the
/// <c>KnownTranslationKeys_ParityTests</c> / <c>KwLRegistryConsistencyTests</c>
/// pins auto-extend over the new set, so no separate pin re-baseline is
/// needed).
/// </para>
/// </summary>
[Authorize]
public sealed class ReportsMineController(
    IModerationService moderation,
    IErrorReportService errorReports,
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null) : Controller
{
    /// <summary>
    /// The two stacked sections (the D1 "My reports" shape): the
    /// resident's content reports (<see cref="Reports"/> — the R05
    /// <see cref="MyReportRow"/> rows, the reporter's own view — the
    /// <see cref="MyReportRow.TargetTitle"/> is already null-gated by the
    /// R05 seam when the target is no longer readable) + the filer's
    /// platform issues (<see cref="Issues"/> — the raw
    /// <see cref="ErrorReport"/> docs the R05 seam returns, carrying
    /// every field the page shows). <see cref="HasAny"/> is a derived
    /// convenience for the view's empty-state branch (both lists empty
    /// → one friendly line, the E16 "one next action" shape).
    /// </summary>
    public sealed record ReportsMineViewModel(
        IReadOnlyList<MyReportRow> Reports,
        IReadOnlyList<ErrorReport> Issues)
    {
        public bool HasAny => Reports.Count > 0 || Issues.Count > 0;
    }

    private static string? SubjectId(ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /reports/mine</c> — the signed-in resident's own reports
    /// (the D1 "My reports" shape, two stacked sections — content reports
    /// + platform issues). A **read, not a decision**: the two R05 seams
    /// are the sole readers (zero <see cref="IAuthorizationService"/>
    /// passes, zero <c>AccessAudit</c> rows — the ADR 0105 / M17
    /// <see cref="BookmarksController"/> "operator has no read standing"
    /// gate: a caller with no <c>SubjectId</c> gets a non-leaky
    /// <see cref="NotFoundResult"/> before either seam is called).
    /// </summary>
    [HttpGet("/reports/mine")]
    public async Task<IActionResult> Index()
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            // The ADR 0105 / M17 "operator has no read standing" gate
            // (the <see cref="BookmarksController.Index"/> precedent):
            // a caller with no <c>SubjectId</c> has no read standing
            // over any my-reports list. A non-leaky 404 — **no**
            // <c>AccessAudit</c> row is committed, and **neither** seam
            // is called (the sole-reader pin — the controller never
            // re-derives access, and a caller with no read standing has
            // no read to delegate).
            return NotFound();
        }

        // The two R05 read seams (IMPROVE-UX R05, frozen — this unit does
        // not re-derive access, not add a decision, not write an audit
        // row — the ADR 0105 / M17 "personal read, zero audit" shape).
        // The reporterId / subjectId argument is the signed-in subject —
        // the same KumunitaPrincipal.SubjectId the BookmarksController /
        // ErrorReportAdminController use.
        var reports = await moderation.ListMyReportsAsync(actorId);
        var issues = await errorReports.ListMineAsync(actorId);

        return View(new ReportsMineViewModel(reports, issues));
    }
}
