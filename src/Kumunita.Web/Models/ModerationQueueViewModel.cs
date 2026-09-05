using Kumunita.Web.Models;

namespace Kumunita.Web.Models;

/// <summary>
/// The <c>/moderation</c> queue (M3b U7, F5/F6 render + the C-M3b·4
/// SoD pin): a projection of the <c>Report</c> rows (M3-registered
/// POCO, M3b workflow) ordered <c>At</c> desc + <c>Status</c> desc
/// (the plan's line-254 pin). Thin-controller shape: the controller
/// reads the <c>Report</c> rows + the reporter's / post's display
/// names (a <c>GetProfileAsync</c> read, not a decision — the M2
/// <see cref="DirectoryViewModel.VisibleProfile"/> precedent) and
/// projects to the <see cref="ReportRow"/> rows + the
/// <see cref="ByStatus"/> count row.
/// <para>
/// **SoD (C-M3b·4):** the list is the GlobalAdmin's queue. A
/// standing-moderator's read on this surface is not a M3b FACES
/// outcome (F5/F6 are GlobalAdmin lanes); the view is only rendered
/// under <c>[Authorize(Roles = GlobalAdmin)]</c> (the Web's standing
/// gate — ADR 0003 SoD, mirrored from M1's
/// <see cref="Kumunita.Web.Controllers.AdminController"/> shape).
/// </para>
/// </summary>
public sealed class ModerationQueueViewModel
{
    public IReadOnlyList<ReportRow> Reports { get; set; } = [];

    /// <summary>
    /// The §2.3 item-2 four Status-literal counts: <c>filed → int</c>,
    /// <c>assigned → int</c>, <c>unlocked → int</c>, <c>resolved →
    /// int</c> — plus any report with a <c>Status == null</c> (the
    /// "filed default" — M3's POCO registers <c>Status</c> as
    /// nullable until M3b's write lane sets it; a null-Status report
    /// is a M3-early-seed / pre-U4-lane row, and is counted under
    /// <see cref="ByStatus"/>'s <c>"filed"</c> bucket in the queue's
    /// by-status row — no new Status literal is introduced for it,
    /// per C-M3b·1 / §2.3 item 3 "no new literal").
    /// </summary>
    public IReadOnlyDictionary<string, int> ByStatus { get; set; } =
        new Dictionary<string, int>
        {
            ["filed"]    = 0,
            ["assigned"] = 0,
            ["unlocked"] = 0,
            ["resolved"] = 0,
        };

    public int TotalCount { get; set; }
}

/// <summary>
/// One queue row. The low-entropy projection (M2 §9 shape — never
/// a M3 POCO with its raw fields leaking out): the
/// <see cref="Kumunita.Core.Posts.Report"/> POCO's <c>Id</c>, the
/// post's title (a <c>PostService.GetPostAsync</c>-shaped read — a
/// read, not a decision), the component name (a
/// <c>IUserInfoService.GetComponentAsync</c> read — a read, not a
/// decision), the reporter's display name (a
/// <c>IUserInfoService.GetProfileAsync</c> read — a read, not a
/// decision), the <c>Reason</c> (verbatim, no truncation), the
/// <c>Status</c> (the §2.3 item-2 literal), and the <c>At</c> stamp
/// (UTC, as written by U4's <c>FileReportAsync</c>).
/// </summary>
public sealed record ReportRow(
    string Id,
    string PostId,
    string? PostTitle,
    string? ComponentName,
    string? ReporterName,
    string? Reason,
    string? Status,
    DateTimeOffset At);
