using Kumunita.Core.Identity;
using Kumunita.Core.Moderation;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/moderation</c> surface (M3b U7): the <b>queue</b>
/// (F5/F6 reads, sorted <c>At</c> desc + <c>Status</c> desc —
/// the plan's U7 line-254 pin) and the <b>resolve UI</b>
/// (F5 assign / F6 unlock / F6 resolve form per report).
/// <c>Index</c> (<c>GET /moderation</c>) renders the queue;
/// <c>Resolve</c> (<c>GET /moderation/{id}</c>) renders the
/// detail + action forms.
/// <para>
/// <b>Gate shape (C-M3b·4 / ADR 0003 SoD + the U6 handoff-note
/// clarification, plan § U7 line-252):</b>
/// <table border="0" summary="two-tier gate">
///   <tr>
///     <td><b>Queue / Detail (read lanes)</b> —
///         <c>[Authorize]</c> (signed-in). A GlobalAdmin sees
///         <b>every</b> report; a standing moderator
///         (<c>Moderator</c> role + a <see
///         cref="ModeratorAssignment"/> row on the report's
///         <c>ComponentId</c> +
///         <c>Component.ModeratorAccess == true</c>) sees <b>only
///         the reports whose component they moderate and whose
///         component has the flag ON</b>. A plain resident
///         (<c>Member</c>, no standing-moderator row) sees
///         <b>no reports</b>. This is a <b>read lane, not a
///         decision lane</b> — no <c>CanAsync</c> call, no
///         <c>AccessAudit</c> row (the M2
///         <see cref="DirectoryController"/> / M1
///         <see cref="AdminController"/> queue shape). The
///         <c>ModeratorAccess</c> flag is read via the plain
///         <c>IUserInfoService</c> read seams (the U6
///         handoff's "flag ON branch via the read seam"
///         clarification — <b>not</b> via
///         <c>SetComponentModeratorAccessAsync</c>, which is
///         the GlobalAdmin-only <b>write</b> seam in C-M3b·4).
///     </td>
///   </tr>
///   <tr>
///     <td><b>Assign / Unlock / Resolve (write lanes)</b> —
///         <c>[Authorize(Roles = GlobalAdmin)]</c> at
///         <b>action</b> level (not class — the class itself is
///         <c>[Authorize]</c>: signed-in, so a
///         standing-moderator can still <b>read</b> the
///         detail; the three POSTs reject non-GlobalAdmin at
///         the ASP.NET Core gate). ADR 0003 / C-M3b·4: "A
///         <b>Moderator</b> caller is <b>denied</b> … the write
///         is <b>not executed</b>" — the class-level
///         <c>[Authorize(Roles = GlobalAdmin)]</c> on M1's
///         <see cref="AdminController"/> is the M1 shape; U7's
///         <b>action-level</b> variant is needed because
///         <c>Index</c> / <c>Resolve</c> (GETs) are
///         <b>scoped-read lanes</b>, not GlobalAdmin-only. The
///         decision gate on the write lanes is the
///         <c>CanAsync(actor, AccessAction.Moderate, target,
///         session)</c> call <b>inside</b> each U5 method
///         (C3 / ADR 0006-C: the <c>decision.Allowed =
///         false</c> path writes the audit row but NOT the
///         domain write — "no partial write").
///     </td>
///   </tr>
/// </table>
/// </para>
/// <para>
/// <b>Display-level predicates (Razor view, NOT decision
/// gates):</b>
/// <list type="bullet">
///   <item><see cref="ModerationResolveViewModel.IsAssignable"/>
///       = <c>Status == "filed" && IsGlobalAdmin &&
///       moderators.Count &gt; 0</c> (a standing-moderator
///       never sees the assign form — the C-M3b·4 SoD pin).
///   </item>
///   <item><see cref="ModerationResolveViewModel.IsUnlockable"/>
///       = <c>IsGlobalAdmin && Status in {filed, assigned}</c>.
///   </item>
///   <item><see cref="ModerationResolveViewModel.IsResolvable"/>
///       = <c>IsGlobalAdmin && Status in {filed, assigned,
///       unlocked}</c>.
///   </item>
/// </list>
/// A standing-moderator reads the report details + their
/// assignee row (if any) but sees <b>no action forms</b> — the
/// C-M3b·4 "Moderator caller is denied, write not executed"
/// shape, projected at the display level (the actual SoD
/// enforcement is the action-level
/// <c>[Authorize(Roles = GlobalAdmin)]</c> + the Core's
/// <c>CanAsync(Moderate)</c> — the two independent gates).
/// </para>
/// <para>
/// **SoD pin confirmation (U7 Exit line, ADR 0003):** this
/// controller does <b>not</b> modify
/// <see cref="AdminController"/> / <c>/admin</c> — U7 is a
/// <b>new</b> surface under the <b>new</b> <c>/moderation</c>
/// route; M1's <c>/admin</c> (account / role / verify + the
/// M1 audit / break-glass lanes) is <b>unchanged</b> by M3b
/// (plan line 257–258 Exit; design-doc invariant C-M3b·4
/// "/admin is unchanged"). ADR 0003's "delegating-
/// moderation" SoD gate holds by construction on the write
/// lanes (the GlobalAdmin standing on the Web + the
/// <c>CanAsync(Moderate)</c> decision in Core are two
/// independent gates, both required by the write-lane
/// shape; the read lanes are scoped-read, not decision
/// lanes — ADR 0006-D).
/// </para>
/// </summary>
[Authorize]
[Route("moderation")]
public sealed class ModerationController(
    // IMPROVE-CODE C09 — the <see cref="IUserInfoService"/> /
    // <see cref="IProfileSettingsService"/> reads (the reporter /
    // reply-author / target-author / standing-moderator display
    // names + the scoped-read <c>ModeratorAssignment</c> /
    // <c>Component.ModeratorAccess</c> reads) now live in the Core
    // read seam <see cref="IModerationService"/> (ADR 0006 §D
    // "route + authz + shape"); the controller resolves only the
    // interface (the concrete <see cref="ModerationService"/> is
    // the Core's composition-root concern).
    IModerationService moderation) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    private static bool IsGlobalAdmin(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.IsGlobalAdmin(user);

    private static bool IsStandingModerator(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.IsModerator(user);

    // ── /moderation — the queue (scoped read) ─────────────────────────

    /// <summary>
    /// The moderated queue (M3b U7; plan line-254 pin
    /// "<c>At</c> desc + <c>Status</c> desc"). A <b>scoped
    /// read lane</b>: a GlobalAdmin sees every <see cref="Report"/>;
    /// a standing-moderator sees only the reports where
    /// (a) they hold a <see cref="ModeratorAssignment"/> row
    /// on the report's <c>ComponentId</c>, and (b) that
    /// component's <see cref="Component.ModeratorAccess"/>
    /// flag is ON (the U6 handoff's "flag ON branch via the
    /// read seam" clarification — the <c>ModeratorAccess</c>
    /// bool is a plain <c>Component</c> POCO field, a read,
    /// not a write lane). A plain resident
    /// (<c>Member</c>, no standing-moderator row) sees no
    /// reports (the C-M3b·4 SoD pin projects at the display
    /// level: the queue shows only scoped rows, the write
    /// lanes are additionally action-gated to GlobalAdmin
    /// — the two independent gates). No <c>CanAsync</c> call,
    /// no <c>AccessAudit</c> row (the M2
    /// <see cref="DirectoryController"/> queue shape — a read
    /// lane, not a decision lane).
    /// <para>
    /// IMPROVE-CODE C09 — the scoped-read + the per-row
    /// resolution (the component / reporter / reply /
    /// target names) now live in the Core read seam
    /// <see cref="IModerationService.GetQueueAsync"/> (ADR
    /// 0006 §D "route + authz + shape"); this action maps
    /// the read model onto <see cref="ModerationQueueViewModel"/>
    /// verbatim (the served HTML is unchanged).
    /// </para>
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var actor = SubjectId(User) ?? string.Empty;
        var isGlobalAdmin = IsGlobalAdmin(User);
        var isStanding = IsStandingModerator(User);

        // The scoped queue (the scoped-read filter + the per-row
        // resolution — the controller's previous Marten reads, moved
        // to the Core read seam, ADR 0006 §D): a GlobalAdmin sees
        // every report; a standing-moderator sees only the
        // reports where (a) they hold a ModeratorAssignment row
        // on the report's ComponentId, AND (b) the component's
        // ModeratorAccess flag is ON (the U6 handoff's flag ON
        // gate). A plain resident (Member, no standing-moderator
        // row) sees no reports (the C-M3b·4 SoD pin projects at
        // the display level).
        var queue = await moderation.GetQueueAsync(actor, isGlobalAdmin, isStanding).ConfigureAwait(false);

        if (queue.Rows.Count == 0)
            return View(new ModerationQueueViewModel());

        var rows = queue.Rows.Select(r => new ReportRow(
            r.Id,
            r.PostId,
            r.PostTitle,
            r.ComponentName,
            r.ReporterName,
            r.Reason,
            r.Status,
            r.At,
            r.ReplyId,
            r.ReplyAuthorName,
            TargetKind: r.TargetKind,
            TargetId: r.TargetId,
            TargetTitle: r.TargetTitle,
            TargetAuthorName: r.TargetAuthorName))
            .ToList();

        return View(new ModerationQueueViewModel
        {
            Reports    = rows,
            ByStatus   = queue.ByStatus,
            TotalCount = queue.Rows.Count,
        });
    }

    // ── /moderation/{id} — the detail + action forms (scoped read) ────

    /// <summary>
    /// The single-report <c>GET /moderation/{id}</c> surface —
    /// the report details + (GlobalAdmin-only) the three action
    /// forms (assign / unlock / resolve). A
    /// standing-moderator on the report's component (flag ON)
    /// sees the details but no action forms (the C-M3b·4 SoD
    /// display-level projection). A plain resident or a
    /// non-scoped user sees 404 for any report they are not in
    /// scope on (the M3 U7 detail-fail-safe shape:
    /// <c>NotFound()</c>, not an empty <see
    /// cref="ModerationResolveViewModel"/>).
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> Resolve([FromRoute] string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actor = SubjectId(User) ?? string.Empty;
        var isGlobalAdmin = IsGlobalAdmin(User);
        var isStanding = IsStandingModerator(User);

        // The detail + the target / reply / standing-moderator
        // resolution (the controller's previous Marten reads, moved
        // to the Core read seam, ADR 0006 §D): a missing report is
        // the Core's <see cref="KeyNotFoundException"/> → 404 (the
        // controller maps to a 404 — non-leaky).
        ModerationDetailReadModel detail;
        try
        {
            detail = await moderation.GetDetailAsync(id).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        // Scoped-read (IMPROVE-CODE C09 — E11, the SEC lane's
        // decision, not C09's): a global admin sees every report; a
        // standing-moderator sees only the reports on their
        // components (flag ON); a plain resident sees none
        // (C-M3b·4 SoD display-level projection; the
        // decision-level gate on the write lanes is the
        // action-level [Authorize] + the Core's
        // CanAsync(Moderate) — U7's read is a read, not a
        // decision, per ADR 0006-D + the M2/M3 queue shape).
        if (!isGlobalAdmin)
        {
            if (!(isStanding && await moderation.IsInActorScopeAsync(actor, detail.ComponentId).ConfigureAwait(false)))
                return NotFound();
        }

        var moderators = detail.Moderators
            .Select(m => new StandingModerator(m.UserId, m.DisplayName))
            .ToList();

        var statusKey = detail.Status;
        var model = new ModerationResolveViewModel
        {
            ReportId        = detail.ReportId,
            PostId          = detail.PostId,
            ComponentId     = detail.ComponentId,
            Reason          = detail.Reason,
            Status          = statusKey,
            At              = detail.At,
            ReporterName    = detail.ReporterName,
            PostTitle       = detail.PostTitle,
            PostBody        = detail.PostBody,
            ComponentName   = detail.ComponentName,
            PostAuthorName  = detail.PostAuthorName,
            ReplyId         = detail.ReplyId,
            ReplyBody       = detail.ReplyBody,
            ReplyAuthorName = detail.ReplyAuthorName,
            TargetKind      = detail.TargetKind,
            TargetId        = detail.TargetId,
            TargetTitle     = detail.TargetTitle,
            TargetAuthorName = detail.TargetAuthorName,
            Moderators      = moderators,

            // Display-level predicates (the §2.3 item-2 four
            // Status literals: filed / assigned / unlocked /
            // resolved). A "filed" report: assignable + unlockable
            // + resolvable (for a GlobalAdmin). An "assigned"
            // report: unlockable + resolvable. An "unlocked"
            // report: resolvable. A "resolved" report: none (the
            // report is closed). All three are GlobalAdmin-only
            // (the C-M3b·4 SoD display-level projection — a
            // standing-moderator sees the detail but NO forms;
            // the actual write-lane gate is the action-level
            // [Authorize(Roles = GlobalAdmin)] + the Core's
            // CanAsync(Moderate) call).
            IsAssignable = isGlobalAdmin && statusKey == "filed" && moderators.Count > 0,
            IsUnlockable = isGlobalAdmin && statusKey is "filed" or "assigned",
            IsResolvable = isGlobalAdmin && statusKey is "filed" or "assigned" or "unlocked",
            IsGlobalAdminView = isGlobalAdmin,
        };

        return View(model);
    }

    // ── /moderation/{id}/assign — POST (GlobalAdmin-only — C-M3b·4 SoD)

    /// <summary>
    /// The "assign to a standing moderator" submit (F5, C-M3b·4
    /// SoD gate — <b>only a GlobalAdmin</b>). Delegates to the
    /// frozen <see cref="ModerationService.AssignReportAsync"/> —
    /// the Core's <c>CanAsync(actor, AccessAction.Moderate,
    /// target, session)</c> SoD decision gate is inside that
    /// method (C3 / ADR 0006-C: the <c>decision.Allowed =
    /// false</c> path writes the audit row but NOT the
    /// domain write; "no partial write"). Web-side
    /// defense-in-depth: validate that the chosen
    /// <paramref name="assignedToModeratorId"/> is in the
    /// report's standing-moderator list (the Core trusts the
    /// caller's standing — a non-valid id passed to
    /// <c>AssignReportAsync</c> would just be written as a
    /// <see cref="ModeratorAssignment"/> row for a non-
    /// moderator — a semantic error the §2.3 pins do not
    /// forbid but the UI should not invite).
    /// <para>
    /// <b>Gate:</b> <c>[Authorize(Roles = GlobalAdmin)]</c>
    /// (action-level) — a standing-moderator is rejected by
    /// ASP.NET Core before the action body runs (C-M3b·4: a
    /// "Moderator caller is denied, write not executed").
    /// </para>
    /// </summary>
    [HttpPost("{id}/assign")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.GlobalAdmin)]
    public async Task<IActionResult> Assign(
        [FromRoute] string id,
        [FromForm] string assignedToModeratorId)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        if (string.IsNullOrEmpty(assignedToModeratorId))
        {
            TempData["error"] = await this.FlashAsync("moderation.flash_choose_moderator", "Choose a moderator to assign.");
            return Redirect($"/moderation/{id}");
        }

        var admin = SubjectId(User) ?? string.Empty;

        // Web-side defense-in-depth (IMPROVE-CODE C09 — the
        // validation reads moved to the Core read seam, ADR
        // 0006 §D): the chosen id must be a standing-moderator
        // on this report's component (A non-valid id would just
        // be written as a ModeratorAssignment row for a
        // non-moderator — a semantic error the §2.3 pins do
        // not forbid but the UI should not invite); a report
        // with no ComponentId is the C-M3b·4 "no fabricated
        // component" defensive case (the form shouldn't have
        // offered the choice, but we reject with a 404 either
        // way — a report with no component on the queue would
        // have been scoped out already, so this is the "stale
        // form" shape — the report was edited between the GET
        // and the POST).
        ModerationDetailReadModel detail;
        try
        {
            detail = await moderation.GetDetailAsync(id).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        if (detail.ComponentId is null)
        {
            // C-M3b·4 "no fabricated component" — a report with
            // no component can't have a standing-moderator
            // scope (the form shouldn't have offered the
            // choice) — reject with a 404 (the report is in an
            // invalid shape, the "stale form" shape).
            return NotFound();
        }
        if (!await moderation.IsStandingModeratorOnComponentAsync(
            detail.ComponentId, assignedToModeratorId).ConfigureAwait(false))
        {
            TempData["error"] = await this.FlashAsync("moderation.flash_not_moderator", "That account is not a standing moderator on this report's component. (The form may be out of sync — refresh.)");
            return Redirect($"/moderation/{id}");
        }

        try
        {
            await moderation.AssignReportAsync(
                reportId:              id,
                assignedToModeratorId: assignedToModeratorId,
                globalAdminId:         admin)
                .ConfigureAwait(false);
            TempData["info"] = await this.FlashAsync("moderation.flash_assigned", "Report assigned.");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return Redirect("/moderation");
    }

    // ── /moderation/{id}/unlock — POST (GlobalAdmin-only — C-M3b·4 SoD)

    /// <summary>
    /// The "report-driven unlock" (F6, the C5 activation event) —
    /// the <c>Component.ModeratorAccess = true</c> flag-flip
    /// via the M1 frozen <c>IUserInfoService.
    /// SetComponentModeratorAccessAsync</c> seam (the "separate
    /// commit" shape from U5's doc-comment — the M1 seam opens
    /// its own session; the caller's transaction commits the
    /// <see cref="Report/"/> <c>Status</c> + the
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/>
    /// row atomically). Delegates to the frozen
    /// <see cref="ModerationService.UnlockAsync"/> (the SoD gate
    /// is the
    /// <c>[Authorize(Roles = GlobalAdmin)]</c> action-level
    /// attribute + the Core's <c>CanAsync(Moderate)</c> inside
    /// U5's method).
    /// </summary>
    [HttpPost("{id}/unlock")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.GlobalAdmin)]
    public async Task<IActionResult> Unlock([FromRoute] string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var admin = SubjectId(User) ?? string.Empty;

        try
        {
            // IMPROVE-CODE C09 — the committed lane (the Core's own
            // session; the C5 flag-flip inside is unchanged, ADR
            // 0006 §D).
            await moderation.UnlockAsync(
                reportId:      id,
                globalAdminId: admin)
                .ConfigureAwait(false);
            TempData["info"] = await this.FlashAsync("moderation.flash_unlocked", "Report unlocked (C5 activated on the component).");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return Redirect("/moderation");
    }

    // ── /moderation/{id}/resolve — POST (GlobalAdmin-only — C-M3b·4 SoD)

    /// <summary>
    /// The "close the report" submit (F6 — the resolve
    /// counterpart to <see cref="Unlock"/>). Delegates to the
    /// frozen <see cref="IModerationService.ResolveReportAsync"/>
    /// (the same SoD / gate shape as <see cref="Unlock"/> —
    /// the Core's <c>CanAsync(Moderate)</c> inside U5's method
    /// + this action's <c>[Authorize(Roles = GlobalAdmin)]</c>;
    /// the <see cref="Report/"/> <c>Status</c> is set to the
    /// §2.3 item-2 literal <c>"resolved"</c> and the M1
    /// <c>SetComponentModeratorAccessAsync</c> seam is called
    /// with <c>on = true</c> as well — U5's behavior note
    /// (the handoff): the flag-flip runs on both the unlock
    /// and the resolve lane; the resolve lane is the "final
    /// close" event, the unlock lane is the "activation"
    /// event).
    /// </summary>
    [HttpPost("{id}/resolve")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.GlobalAdmin)]
    public async Task<IActionResult> ResolvePost([FromRoute] string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        var admin = SubjectId(User) ?? string.Empty;

        // IMPROVE-UX R04 — the optional resolve-form pair (the D1
        // closed vocabulary + the ≤500 note, the R03 seam's optional
        // parameters). Both are optional: an empty / absent form field
        // means the user left it blank, and the pre-R04 "no outcome
        // recorded" call shape (the two-argument call the seam still
        // compiles against) is what the seam gets back. The outcome is
        // parsed with the AdminController precedent (line 513,
        // <c>Enum.TryParse</c> + <c>ignoreCase: true</c>) — an
        // unrecognized value is treated as "left blank" (the
        // pre-R04 shape), never an error. The note is passed through
        // verbatim — the R03 lane's own <c>ResolveReportNote</c>
        // helper is the ≤500-cap second line of defense (the form's
        // <c>maxlength</c> is the first); the controller does not add
        // its own validation (the prompt's explicit do-not).
        var outcomeRaw = Request.Form["outcome"].ToString();
        var outcome = Enum.TryParse<Kumunita.Core.Posts.ReportOutcome>(outcomeRaw, ignoreCase: true, out var parsed)
            ? (Kumunita.Core.Posts.ReportOutcome?)parsed
            : null;
        var noteRaw = Request.Form["resolutionNote"].ToString();
        var note = string.IsNullOrWhiteSpace(noteRaw) ? null : noteRaw;

        try
        {
            // IMPROVE-CODE C09 — the committed lane (the Core's own
            // session; the C5 flag-flip inside is unchanged, ADR
            // 0006 §D). IMPROVE-UX R04 — the optional outcome + note
            // pair (the R03 seam's optional parameters; null / null
            // is the pre-R04 "no outcome recorded" call shape).
            await moderation.ResolveReportAsync(
                reportId:       id,
                globalAdminId:  admin,
                outcome:        outcome,
                resolutionNote: note)
                .ConfigureAwait(false);
            TempData["info"] = await this.FlashAsync("moderation.flash_resolved", "Report resolved.");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return Redirect("/moderation");
    }
}
