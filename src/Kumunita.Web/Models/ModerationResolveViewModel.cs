using Kumunita.Web.Models;

namespace Kumunita.Web.Models;

/// <summary>
/// The single-report resolve / assign / unlock surface (M3b U7,
/// F5/F6 render; C-M3b·4 SoD pin) — the view-model for
/// <c>GET /moderation/{id}</c>. A *projection* of the
/// <see cref="Kumunita.Core.Posts.Report"/> + the referenced
/// <see cref="Kumunita.Core.Posts.Post"/> rows (M3 POCOs, already
/// loaded in the controller's own read session) + the
/// standing-moderator list for the assign form (M1's
/// <see cref="Kumunita.Core.UserInfo.ModeratorAssignment"/>'s rows
/// on the report's <c>ComponentId</c>).
/// <para>
/// The <b>SoD</b> (C-M3b·4 / ADR 0003) pin is enforced
/// <b>upstream</b>: the controller is
/// <c>[Authorize(Roles = GlobalAdmin)]</c>, and the Core's
/// <see cref="Kumunita.Core.Moderation.ModerationService"/> methods
/// <b>each</b> call <c>IAuthorizationService.CanAsync(actor,
/// AccessAction.Moderate, target, session)</c> inside the caller's
/// transaction (the U5 lane shape). This view-model only
/// *renders*; it never re-derives a decision (the ADR 0006-D
/// thin-controller rule, mirrored from M1's
/// <see cref="Kumunita.Web.Models.AdminIndexViewModel"/> and M2's
/// <see cref="Kumunita.Web.Models.DirectoryViewModel"/>).
/// </para>
/// <para>
/// The <see cref="IsAssignable"/> / <see cref="IsUnlockable"/> /
/// <see cref="IsResolvable"/> predicates are derived from
/// <see cref="Status"/> (the §2.3 item-2 four Status-literal pins:
/// <c>filed</c> / <c>assigned</c> / <c>unlocked</c> /
/// <c>resolved</c>) — a display-level gate only. The <b>real</b>
/// SoD / authorization gate is in the Core (U5's
/// <c>CanAsync</c> call, and its
/// <c>decision.Allowed = false</c> path — the "audit-row
/// written, domain-write not executed" C3 shape — U7's Web
/// layer has no decision path, just the redirect shape).
/// </para>
/// </summary>
public sealed class ModerationResolveViewModel
{
    // ── The report row (M3-registered POCO, never reshaped) ─────────
    public string ReportId { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string? ComponentId { get; set; }
    public string? Reason { get; set; }
    public string? Status { get; set; }        // one of: filed / assigned / unlocked / resolved (or null pre-U4)
    public DateTimeOffset At { get; set; }
    public string? ReporterName { get; set; }   // IUserInfoService.GetProfileAsync read (a read, not a decision)

    // ── The referenced post (for the title + body preview) ──────────
    public string? PostTitle { get; set; }
    public string? PostBody { get; set; }
    public string? ComponentName { get; set; }   // IUserInfoService.GetComponentAsync read (a read, not a decision)
    public string? PostAuthorName { get; set; }  // IUserInfoService.GetProfileAsync read (a read, not a decision)

    // ── The standing-moderator list for the assign form ─────────────
    public IReadOnlyList<StandingModerator> Moderators { get; set; } = [];

    // ── Display-level predicates (derived from Status) ──────────────
    // These are NOT decision gates. They control which <form> block
    // the Razor view renders (the assign / unlock / resolve form is
    // shown or hidden by a CSS / Razor conditional based on these).
    // The real gate is the SoD [Authorize(Roles = GlobalAdmin)]
    // on the class + the Core's CanAsync inside each write lane.
    public bool IsAssignable { get; set; }
    public bool IsUnlockable { get; set; }
    public bool IsResolvable { get; set; }

    // True iff the current viewer is a GlobalAdmin (the C-M3b·4 SoD
    // display-level projection: the action forms are GlobalAdmin-only
    // — a standing-moderator on this component (flag ON) sees the
    // report's detail but NO action forms. The actual write-lane
    // gate is the action-level [Authorize(Roles = GlobalAdmin)] +
    // the Core's CanAsync(Moderate) call, both required.)
    public bool IsGlobalAdminView { get; set; }

    // ── Derived predicates (filled by the controller) ───────────────
    // A "filed"-status report: assignable + unlockable + resolvable.
    // An "assigned"-status report: unlockable + resolvable (NOT assignable —
    //   the assign lane has already run; the UI offers unlock / resolve).
    // An "unlocked"-status report: resolvable (NOT assignable, NOT unlockable —
    //   the unlock flag-flip has already run; the UI offers only resolve).
    // A "resolved"-status report: none of the three forms (the report is
    //   closed; the UI offers only the "this report is resolved" hint).
}

/// <summary>
/// One standing-moderator row on the assign form. The
/// <see cref="Kumunita.Core.UserInfo.ModeratorAssignment"/>'s
/// <c>UserId</c> (the subject id; the M1 SoD audit convention —
/// the <c>ModeratorAssignment.UserId</c> IS the acting subject,
/// the same "the user id IS the subject id in a resident-facing
/// lane" convention as <see cref="Kumunita.Core.Posts.Report.ReporterId"/>
/// for U4's <c>FileReportAsync</c>). The <see cref="DisplayName"/>
/// is a <c>IUserInfoService.GetProfileAsync</c> read (a read, not
/// a decision — the M2
/// <see cref="DirectoryViewModel.VisibleProfile"/> precedent).
/// </summary>
public sealed record StandingModerator(
    string SubjectId,
    string? DisplayName);
