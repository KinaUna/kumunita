using Kumunita.Core.Identity;
using Kumunita.Core.Moderation;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Marten;

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
    IDocumentStore store,
    IUserInfoService userInfo,
    ModerationService moderation) : Controller
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
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var actor = SubjectId(User) ?? string.Empty;
        var isGlobalAdmin = IsGlobalAdmin(User);
        var isStanding = IsStandingModerator(User);

        // The raw queue — sorted At desc + Status desc (plan's line-254
        // pin). The scoped-read filter is applied below (a read, not a
        // decision); see the "scoped-read filter" block.
        var reports = await session
            .Query<Report>()
            .OrderByDescending(r => r.At)
            .ThenByDescending(r => r.Status)
            .ToListAsync()
            .ConfigureAwait(false);

        if (reports.Count == 0)
            return View(new ModerationQueueViewModel());

        var componentNames = await BuildComponentNameLookupAsync(session).ConfigureAwait(false);

        var rows = new List<ReportRow>(reports.Count);
        var byStatus = new Dictionary<string, int>
        {
            ["filed"]    = 0,
            ["assigned"] = 0,
            ["unlocked"] = 0,
            ["resolved"] = 0,
        };

        // The queue's *scoped-read* filter: a GlobalAdmin sees
        // every report; a standing-moderator sees only the
        // reports where (a) they hold a
        // ModeratorAssignment row on the report's
        // ComponentId, AND (b) the component's
        // ModeratorAccess flag is ON. A plain resident
        // (Member, no standing-moderator row) sees no reports.
        // This is a READ lane, not a decision lane — no
        // CanAsync call, no AccessAudit row (the M2
        // DirectoryController queue shape — a read lane, not a
        // decision lane; the "read seam" clarification in the
        // U6 handoff note). GetAssignmentsAsync is the
        // standing-moderator-scope read (the M1
        // AdminController's line 64 shape); Component.ModeratorAccess
        // is a plain POCO field read (the U6 handoff's "flag ON
        // branch via the read seam, not via
        // SetComponentModeratorAccessAsync" note — the flag is
        // a plain field, read as part of the Component query). A
        // null ComponentId is always out of a standing-moderator's
        // scope (the C-M3b·4 "no fabricated component" shape).
        if (!isGlobalAdmin && isStanding)
        {
            var myComponents = (await userInfo
                .GetAssignmentsAsync(actor)
                .ConfigureAwait(false))
                .Where(a => a.ComponentId is not null)
                .Select(a => a.ComponentId)
                .ToHashSet();
            // The components in the queue that this actor
            // moderates (via their ModeratorAssignment rows). Then
            // filter to the ones where the ModeratorAccess flag is
            // ON (the U6 handoff's flag ON gate).
            var scopedComponentIds = new HashSet<string>();
            foreach (var componentName in componentNames.Keys)
            {
                if (!myComponents.Contains(componentName))
                    continue;
                var component = await session.LoadAsync<Component>(componentName).ConfigureAwait(false);
                if (component?.ModeratorAccess == true)
                    scopedComponentIds.Add(componentName);
            }
            reports = reports.Where(r =>
                r.ComponentId is not null && scopedComponentIds.Contains(r.ComponentId))
                .ToList();
        }
        else if (!isGlobalAdmin)
        {
            // A plain resident (Member, no standing-moderator row)
            // sees NO reports — the C-M3b·4 SoD pin projects at the
            // display level on the read lanes (the real write-lane
            // gate is the action-level [Authorize] + the Core's
            // CanAsync(Moderate) — the two independent gates).
            reports = [];
        }

        foreach (var r in reports)
        {
            var post     = await session.LoadAsync<Post>(r.PostId).ConfigureAwait(false);
            var reporter = await userInfo.GetProfileAsync(r.ReporterId).ConfigureAwait(false);

            var statusKey = r.Status ?? "filed";   // "filed" as the M3-registered POCO default
            if (!byStatus.ContainsKey(statusKey))
                byStatus[statusKey] = 0;
            byStatus[statusKey] += 1;

            rows.Add(new ReportRow(
                r.Id,
                r.PostId,
                post?.Title,
                r.ComponentId is not null ? componentNames.GetValueOrDefault(r.ComponentId) : null,
                reporter?.DisplayName,
                r.Reason,
                statusKey,
                r.At));
        }

        return View(new ModerationQueueViewModel
        {
            Reports    = rows,
            ByStatus   = byStatus,
            TotalCount = reports.Count,
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

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var actor = SubjectId(User) ?? string.Empty;
        var isGlobalAdmin = IsGlobalAdmin(User);
        var isStanding = IsStandingModerator(User);

        var report = await session.LoadAsync<Report>(id).ConfigureAwait(false);
        if (report is null) return NotFound();

        // Scoped-read: a global admin sees every report; a
        // standing-moderator sees only the reports on their
        // components (flag ON); a plain resident sees none
        // (C-M3b·4 SoD display-level projection; the
        // decision-level gate on the write lanes is the
        // action-level [Authorize] + the Core's
        // CanAsync(Moderate) — U7's read is a read, not a
        // decision, per ADR 0006-D + the M2/M3 queue shape).
        if (!isGlobalAdmin)
        {
            // A standing-moderator's read is:
            //   GetAssignmentsAsync(actor) contains report.ComponentId
            //   AND
            //   Component[report.ComponentId].ModeratorAccess == true
            // (the U6 handoff's "flag ON branch via the read seam" note —
            // the flag is a plain POCO field read; never via
            // SetComponentModeratorAccessAsync, which is the GlobalAdmin-
            // only WRITE seam for the flag-flip).
            if (!(isStanding && await IsInActorScopeAsync(session, actor, report.ComponentId)))
                return NotFound();
        }

        var post = await session.LoadAsync<Post>(report.PostId).ConfigureAwait(false);
        if (post is null) return NotFound();

        var reporter  = await userInfo.GetProfileAsync(report.ReporterId).ConfigureAwait(false);
        var postAuthor = await userInfo.GetProfileAsync(post.AuthorId).ConfigureAwait(false);
        var componentNames = await BuildComponentNameLookupAsync(session).ConfigureAwait(false);

        // The standing-moderator list for the assign form
        // (GlobalAdmin-only render shape: a standing-moderator
        // never sees the assign form; the moderator list is the
        // rows from ModeratorAssignment on this report's
        // ComponentId for the display — the "who can be
        // assigned" list for the GlobalAdmin's choice).
        var standingModeratorIds = report.ComponentId is null
            ? []
            : (await session
                .Query<ModeratorAssignment>()
                .Where(a => a.ComponentId == report.ComponentId)
                .Select(a => a.UserId)
                .ToListAsync()
                .ConfigureAwait(false));

        var moderators = new List<StandingModerator>(standingModeratorIds.Count);
        foreach (var uid in standingModeratorIds)
        {
            var profile = await userInfo.GetProfileAsync(uid).ConfigureAwait(false);
            moderators.Add(new StandingModerator(uid, profile?.DisplayName));
        }

        var statusKey = report.Status ?? "filed";
        var model = new ModerationResolveViewModel
        {
            ReportId        = report.Id,
            PostId          = report.PostId,
            ComponentId     = report.ComponentId,
            Reason          = report.Reason,
            Status          = statusKey,
            At              = report.At,
            ReporterName    = reporter?.DisplayName,
            PostTitle       = post.Title,
            PostBody        = post.Body,
            ComponentName   = report.ComponentId is not null ? componentNames.GetValueOrDefault(report.ComponentId) : null,
            PostAuthorName  = postAuthor?.DisplayName,
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
            TempData["error"] = "Choose a moderator to assign.";
            return Redirect($"/moderation/{id}");
        }

        var admin = SubjectId(User) ?? string.Empty;

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());

        // Web-side defense-in-depth: the chosen id must be a
        // standing-moderator on this report's component (A
        // non-valid id would just be written as a
        // ModeratorAssignment row for a non-moderator — a
        // semantic error the §2.3 pins do not forbid but the
        // UI should not invite); a report with no
        // ComponentId is the C-M3b·4 "no fabricated component"
        // defensive case (U5's AssignReportAsync would skip
        // the upsert) — the form shouldn't have offered the
        // choice, but we reject with a 404 either way (a
        // report with no component on the queue would have
        // been scoped out already, so this is the "stale
        // form" shape — the report was edited between the GET
        // and the POST).
        var report = await session.LoadAsync<Report>(id).ConfigureAwait(false);
        if (report is null) return NotFound();

        if (report.ComponentId is not null)
        {
            var valid = await session
                .Query<ModeratorAssignment>()
                .AnyAsync(a => a.ComponentId == report.ComponentId && a.UserId == assignedToModeratorId)
                .ConfigureAwait(false);
            if (!valid)
            {
                TempData["error"] = "That account is not a standing moderator on this report's component. (The form may be out of sync — refresh.)";
                return Redirect($"/moderation/{id}");
            }
        }
        else
        {
            // C-M3b·4 "no fabricated component" — a report with
            // no component can't have a standing-moderator
            // scope (the form shouldn't have offered the
            // choice) — reject with a 404 (the report is in an
            // invalid shape, the "stale form" shape).
            return NotFound();
        }

        try
        {
            await moderation.AssignReportAsync(
                reportId:              id,
                assignedToModeratorId: assignedToModeratorId,
                globalAdminId:         admin,
                session:               session)
                .ConfigureAwait(false);
            TempData["info"] = "Report assigned.";
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

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        try
        {
            await moderation.UnlockAsync(
                reportId:      id,
                globalAdminId: admin,
                session:       session)
                .ConfigureAwait(false);
            TempData["info"] = "Report unlocked (C5 activated on the component).";
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
    /// frozen <see cref="ModerationService.ResolveReportAsync"/>
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

        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        try
        {
            await moderation.ResolveReportAsync(
                reportId:      id,
                globalAdminId: admin,
                session:       session)
                .ConfigureAwait(false);
            TempData["info"] = "Report resolved.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return Redirect("/moderation");
    }

    // ── Private helpers ───────────────────────────────────────────────

    /// <summary>
    /// One round-trip to the <see cref="Component"/> table —
    /// the component-name lookup used by both the
    /// <c>Index</c> queue projection and the <c>Resolve</c>
    /// detail projection (the M1 <see cref="AdminController"/>
    /// Index shape: a
    /// <c>session.Query&lt;Component&gt;()</c> join, no
    /// <c>CanAsync</c> call — a <b>display</b> read, not an
    /// access decision — ADR 0006-D).
    /// </summary>
    private static async Task<Dictionary<string, string>> BuildComponentNameLookupAsync(IDocumentSession session)
    {
        var components = await session
            .Query<Component>()
            .ToListAsync()
            .ConfigureAwait(false);
        return components.ToDictionary(c => c.Id, c => c.Name);
    }

    /// <summary>
    /// The full scoped-read check (one report row): the actor
    /// (a standing-moderator) has a
    /// <see cref="ModeratorAssignment"/> row on the report's
    /// <c>ComponentId</c> (the M1 <see cref="AdminController"/>'s
    /// line 64 shape — the standing-moderator-scope read seam),
    /// AND the component's <c>ModeratorAccess</c> flag is
    /// <c>true</c> (the U6 handoff's "flag ON branch via the
    /// read seam" note — the flag is a plain POCO field read,
    /// NEVER via
    /// <c>SetComponentModeratorAccessAsync</c> which is the
    /// GlobalAdmin-only WRITE seam). A <c>null</c>
    /// <c>ComponentId</c> is always <c>false</c> (a standing-
    /// moderator has no scope on a global report — the C-M3b·4
    /// "no fabricated component" shape).
    /// </summary>
    private async Task<bool> IsInActorScopeAsync(IDocumentSession session, string actor, string? componentId)
    {
        if (componentId is null) return false;   // C-M3b·4 "no fabricated component"
        var assignments = await userInfo.GetAssignmentsAsync(actor).ConfigureAwait(false);
        if (!assignments.Any(a => a.ComponentId == componentId))
            return false;
        var component = await session.LoadAsync<Component>(componentId).ConfigureAwait(false);
        return component?.ModeratorAccess == true;
    }
}
