using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Moderation;

/// <summary>
/// The <c>ModerationService</c> read seam (IMPROVE-CODE C09 — the
/// <see cref="Controllers.ModerationController"/>'s raw Marten reads moved
/// behind this interface, ADR 0006 §D "route + authz + shape"). The Web
/// layer maps the read models onto its view-models verbatim (served HTML
/// unchanged); the write lanes (<see cref="ModerationService"/>
/// <c>AssignReportAsync</c> / <c>UnlockAsync</c> / <c>ResolveReportAsync</c>)
/// are unchanged (they already take an <see cref="Marten.IDocumentSession"/>,
/// the C3 same-transaction lane — the controller's session).
/// <para>
/// <b>E11 (the SEC lane's decision, not C09's):</b>
/// <see cref="IsInActorScopeAsync"/> is the <see
/// cref="Controllers.ModerationController"/>'s
/// <c>IsInActorScopeAsync</c> moved **verbatim** (the
/// <c>ModeratorAssignment</c> read + the <c>Component.ModeratorAccess</c>
/// flag read). Routing it through <see cref="IAuthorizationService"/> is the
/// SEC lane's decision (the E11 handoff); C09 does **not** change who may
/// do what — the <c>ModeratorAssignment</c> read stays as-is.
/// </para>
/// </summary>
public interface IModerationService
{
    /// <summary>
    /// The scoped <c>GET /moderation</c> queue (the controller's
    /// <c>Index</c> body minus the shape mapping): the raw report rows in
    /// the queue's order (<c>At</c> desc, <c>Status</c> desc) filtered by
    /// the actor's scope (GlobalAdmin → every report; standing-moderator →
    /// the reports on their components with <c>ModeratorAccess</c> ON; a
    /// plain resident → no reports), with the component name, the reporter
    /// name, the reply / target title + author name resolved (the raw
    /// Marten reads moved here, ADR 0006 §D). <paramref name="actorId"/>
    /// is the subject's id (empty for a non-actor — the queue is empty);
    /// <paramref name="isGlobalAdmin"/> / <paramref name="isStanding"/>
    /// are the actor's role flags (resolved by the controller, passed in —
    /// the C08 base-controller idiom: the service "calls through, not
    /// re-derive"). The <c>ByStatus</c> counts are over the **scoped**
    /// rows (the controller's previous <c>ByStatus</c> shape).
    /// </summary>
    Task<ModerationQueueReadModel> GetQueueAsync(
        string actorId,
        bool isGlobalAdmin,
        bool isStanding,
        CancellationToken ct = default);

    /// <summary>
    /// The <c>GET /moderation/{id}</c> detail (the controller's
    /// <c>Resolve</c> body minus the shape mapping): the report + its
    /// resolved target (post, or event / project / board / todo /
    /// grouppost via <c>TargetKind</c> / <c>TargetId</c>) + the reply
    /// discriminator (ADR 0023) + the standing-moderator list for the
    /// assign form. Throws <see cref="KeyNotFoundException"/> when the
    /// report is absent (the controller maps to 404 — non-leaky). The
    /// scoped-read gate (a standing-moderator sees only the reports on
    /// their components with <c>ModeratorAccess</c> ON) is the caller's
    /// concern (the controller's <c>IsInActorScopeAsync</c> call before
    /// the <c>GetDetailAsync</c> call) — the seam is the **sole reader**
    /// of the report + target docs, not the scope decision (C-M37·1
    /// shape, the Notes precedent).
    /// </summary>
    Task<ModerationDetailReadModel> GetDetailAsync(
        string reportId,
        CancellationToken ct = default);

    /// <summary>
    /// The reporter's <c>My reports</c> read (IMPROVE-UX R05 — the D1 "My
    /// reports" shape the <c>MyReportRow</c> record models): the content
    /// reports <paramref name="reporterId"/> filed (where
    /// <see cref="Kumunita.Core.Posts.Report.ReporterId"/> == the
    /// reporter's id), newest-first (<c>At</c> desc), each row carrying the
    /// report's context (<c>PostId</c> / <c>ComponentId</c> /
    /// <c>Reason</c> / <c>At</c>), its status, and the R03 resolution shape
    /// (<see cref="Kumunita.Core.Posts.Report.Outcome"/> +
    /// <see cref="Kumunita.Core.Posts.Report.ResolutionNote"/> +
    /// <see cref="Kumunita.Core.Posts.Report.ResolvedAt"/> /
    /// <see cref="Kumunita.Core.Posts.Report.ResolvedBy"/> — all four
    /// <c>null</c> until the <c>ResolveReportAsync</c> lane writes them).
    /// <para>
    /// The row's <c>TargetTitle</c> is the target's title (the ADR 0173
    /// <c>TargetKind</c> switch the <c>GetDetailAsync</c> read seam uses),
    /// resolved the same way — **but gated on the reporter's visibility**:
    /// for a <b>post</b> target (the original M3b shape,
    /// <c>TargetKind == null</c>) the title is shown only while the reporter
    /// can still read the post (a <c>null</c> <c>Post</c> in the
    /// <see cref="Kumunita.Core.Posts.PostDetailResult"/> — denied / hidden /
    /// removed / deleted — means no title, the R05 DoD "target no longer
    /// visible → no title" pin). For the other target kinds (event / project
    /// / board / todo / grouppost) the title is resolved directly (their
    /// visibility rules are out of scope for R05 — the plan is posts-first,
    /// A02).
    /// </para>
    /// <para>
    /// A **read, not a decision** (the <c>GetQueueAsync</c> /
    /// <c>GetDetailAsync</c> precedent): the report is the reporter's own;
    /// the read is a list, not a scoped-read gate — no
    /// <see cref="IAuthorizationService"/> call, no <c>AccessAudit</c> row.
    /// Returns an empty list when the reporter filed no reports.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<MyReportRow>> ListMyReportsAsync(
        string reporterId,
        CancellationToken ct = default);

    /// <summary>
    /// The standing-moderator scope read for the assign form (the
    /// <c>ModeratorAssignment</c> rows on <paramref name="componentId"/>):
    /// the <c>UserId</c> + the display name (resolved via
    /// <see cref="IUserInfoService.GetProfileAsync"/>, the controller's
    /// previous <c>StandingModerator</c> shape). An empty list when the
    /// <c>ComponentId</c> is null (the C-M3b·4 "no fabricated component"
    /// shape).
    /// </summary>
    Task<IReadOnlyList<ModerationStandingModerator>> GetStandingModeratorsAsync(
        string? componentId,
        CancellationToken ct = default);

    // ── E11 (the SEC lane's decision) — moved verbatim, behaviour unchanged ─

    /// <summary>
    /// The full scoped-read check (one report row): the actor (a
    /// standing-moderator) has a <see cref="ModeratorAssignment"/> row on
    /// the report's <c>ComponentId</c> (the M1 <c>AdminController</c>'s
    /// line 64 shape — the standing-moderator-scope read seam), AND the
    /// component's <c>ModeratorAccess</c> flag is <c>true</c> (the U6
    /// handoff's "flag ON branch via the read seam" note — the flag is a
    /// plain POCO field read, NEVER via
    /// <c>IProfileSettingsService.SetComponentModeratorAccessAsync</c>
    /// which is the GlobalAdmin-only WRITE seam). A <c>null</c>
    /// <c>ComponentId</c> is always <c>false</c> (a standing-moderator
    /// has no scope on a global report — the C-M3b·4 "no fabricated
    /// component" shape).
    /// <para>
    /// <b>TODO ❓ SEC (E11):</b> this is an access decision outside
    /// <see cref="IAuthorizationService"/> — routing it through the
    /// authorization seam is the SEC lane's decision (the IMPROVE-CODE
    /// plan's E11 handoff, "the authorization path is unique" ADR 0006 §D).
    /// C09 moves it verbatim off the controller (the ADR 0006 §D thin-HTTP
    /// seam) and does **not** change who may do what.
    /// </para>
    /// </summary>
    Task<bool> IsInActorScopeAsync(
        string actor,
        string? componentId,
        CancellationToken ct = default);

    /// <summary>
    /// The standing-moderator read for the <c>Assign</c> web-side
    /// defense-in-depth (IMPROVE-CODE C09 — the controller's previous
    /// <c>AnyAsync</c> read moved behind this service, ADR 0006 §D): a
    /// <c>true</c> when <paramref name="moderatorId"/> holds a
    /// <see cref="ModeratorAssignment"/> row on <paramref
    /// name="componentId"/>, a <c>false</c> otherwise (the controller's
    /// previous <c>flash_not_moderator</c> redirect shape — the form
    /// was out of sync, not a 404).
    /// </summary>
    Task<bool> IsStandingModeratorOnComponentAsync(
        string componentId,
        string moderatorId,
        CancellationToken ct = default);

    // ── Committed write lanes (IMPROVE-CODE C09 — the controller's write
    //    lanes moved off the controller's own session) ───────────────

    /// <summary>
    /// The committed <c>Assign</c> lane (IMPROVE-CODE C09 — the
    /// <see cref="Kumunita.Web.Controllers.ModerationController"/>'s
    /// <c>Assign</c> write lane moved behind this service, ADR 0006 §D
    /// "route + authz + shape"). Delegates to the session-based
    /// <c>AssignReportAsync</c> in the service's own session (the SoD
    /// <c>CanAsync(Moderate)</c> gate inside that method is unchanged).
    /// Throws <see cref="KeyNotFoundException"/> when the report is
    /// absent (the controller maps to a 404).
    /// </summary>
    Task AssignReportAsync(
        string reportId,
        string assignedToModeratorId,
        string globalAdminId,
        CancellationToken ct = default);

    /// <summary>
    /// The committed <c>Unlock</c> lane (IMPROVE-CODE C09 — the
    /// <see cref="Kumunita.Web.Controllers.ModerationController"/>'s
    /// <c>Unlock</c> moved behind this service, ADR 0006 §D). Delegates
    /// to the session-based <c>UnlockAsync</c> in the service's own
    /// session (the C5 <c>SetComponentModeratorAccessAsync</c>
    /// flag-flip inside that method is unchanged). Throws
    /// <see cref="KeyNotFoundException"/> when the report is absent
    /// (the controller maps to a 404).
    /// </summary>
    Task UnlockAsync(
        string reportId,
        string globalAdminId,
        CancellationToken ct = default);

    /// <summary>
    /// The committed <c>Resolve</c> lane (IMPROVE-CODE C09 — the
    /// <see cref="Kumunita.Web.Controllers.ModerationController"/>'s
    /// <c>ResolvePost</c> moved behind this service, ADR 0006 §D).
    /// Delegates to the session-based <c>ResolveReportAsync</c> in the
    /// service's own session (the C5 <c>SetComponentModeratorAccessAsync</c>
    /// flag-flip inside that method is unchanged). Throws
    /// <see cref="KeyNotFoundException"/> when the report is absent
    /// (the controller maps to a 404).
    /// <para>
    /// IMPROVE-UX R03 — the optional <paramref name="outcome"/> /
    /// <paramref name="resolutionNote"/> pair (both <c>null</c> by
    /// default, so the pre-R03 two-parameter call shape — the
    /// <c>ModerationController</c>'s <c>ResolvePost</c> action — keeps
    /// compiling unchanged). When supplied, the lane writes them onto
    /// the <see cref="Kumunita.Core.Posts.Report"/> row (the R03
    /// resolution shape: <see cref="Kumunita.Core.Posts.Report.Outcome"/>
    /// + <see cref="Kumunita.Core.Posts.Report.ResolutionNote"/>
    /// + <see cref="Kumunita.Core.Posts.Report.ResolvedAt"/> +
    /// <see cref="Kumunita.Core.Posts.Report.ResolvedBy"/>) in the
    /// same transaction as the lane's existing
    /// <c>AccessAudit</c> row — which is **unchanged** (the R03 pin:
    /// same shape, same action/target).
    /// </para>
    /// </summary>
    Task ResolveReportAsync(
        string reportId,
        string globalAdminId,
        Kumunita.Core.Posts.ReportOutcome? outcome = null,
        string? resolutionNote = null,
        CancellationToken ct = default);

    /// <summary>
    /// The resident-facing <c>POST /posts/{postId}/report</c> intake lane
    /// (IMPROVE-CODE-2 C2-03 — added to the seam so the Web crossing of the
    /// <c>Posts</c> / <c>ReportTargets</c> controllers codes against the
    /// interface, not the <see cref="ModerationService"/> concrete). The
    /// <c>ModerationService</c> implementation is unchanged (the SoD
    /// <c>CanAsync(Report)</c> gate inside is intact). Returns the row count
    /// filed (the <c>0</c> / <c>1</c> the controller maps to its flash).
    /// Throws <see cref="ArgumentException"/> on an empty post id / actor,
    /// <see cref="System.ArgumentNullException"/> on a null session.
    /// </summary>
    Task<int> FileReportAsync(
        string postId,
        string actorId,
        string? reason,
        IDocumentSession session);

    /// <summary>
    /// The resident-facing report-on-a-target intake lane (IMPROVE-CODE-2
    /// C2-03 — added to the seam so the <c>ReportTargets</c> controller codes
    /// against the interface, not the <see cref="ModerationService"/>
    /// concrete). The <c>ModerationService</c> opens its own write session and
    /// performs the single <c>SaveChangesAsync</c> (the C3 / ADR 0006-C
    /// no-partial-write discipline is unchanged — the <c>Report</c> row and the
    /// filing <c>AccessAudit</c> row still commit or roll back atomically).
    /// Returns the row count filed. Throws <see cref="ArgumentException"/>
    /// on an empty target id / actor.
    /// </summary>
    Task<int> FileReportTargetAsync(
        string targetKind,
        string targetId,
        string? componentId,
        string? authorId,
        string actorId,
        string? reason);

    /// <summary>
    /// The resident-facing report-on-a-reply intake lane (IMPROVE-CODE-2
    /// C2-03 — added to the seam so the <c>Posts</c> controller codes against
    /// the interface, not the <see cref="ModerationService"/> concrete). The
    /// <c>ModerationService</c> implementation is unchanged. Returns the row
    /// count filed. Throws <see cref="ArgumentException"/> on an empty reply
    /// id / actor, <see cref="System.ArgumentNullException"/> on a null
    /// session.
    /// </summary>
    Task<int> FileReplyReportAsync(
        string replyId,
        string actorId,
        string? reason,
        IDocumentSession session);
}
