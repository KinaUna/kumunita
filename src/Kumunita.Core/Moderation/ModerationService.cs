using Kumunita.Core.Authorization;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Moderation;

/// <summary>
/// The report-workflow composition service (M3b U4; bounded context
/// <c>Kumunita.Core.Moderation</c>, ADR 0006-D lane). The M3b analog of
/// M2's <see cref="Kumunita.Core.UserInfo.DirectoryService"/> and M3's
/// <see cref="Kumunita.Core.Posts.PostService"/>. A pure caller of the
/// two frozen M1/M2 modules — <see cref="IUserInfoService"/> read seams
/// (including the flag-flip seam
/// <see cref="IUserInfoService.SetComponentModeratorAccessAsync"/>,
/// used by U5's F6 lane) and <see cref="IAuthorizationService"/> (the
/// single decision path) — plus its own <see cref="IDocumentStore"/>
/// for the write lanes.
/// <para>
/// M3b's **only** new authorization-surface addition (ADR 0006-E
/// compatible lane) is U5's <c>CanReadWithReportAsync</c>. U4's
/// <see cref="FileReportAsync"/> is a **resident-facing intake action**
/// (C-M3b·1, F1) — it is **not** an access decision and makes **no**
/// <see cref="IAuthorizationService"/> call; it composes only the
/// caller-provided <see cref="IDocumentStore"/> and writes two rows
/// (the <see cref="Report"/> domain row and the
/// <see cref="AccessAudit"/> row) into the **one** caller-owned
/// transaction (C3 / ADR 0006-C — one <c>SaveChangesAsync</c>). The
/// audit row carries the pinned tag <see cref="AccessVia.Admin"/>
/// (NOT <see cref="AccessVia.Report"/> — reserved for the read branch,
/// C-M3b·2; NOT <see cref="AccessVia.Owner"/> — the C1 owner-branch
/// negative). <see cref="Report.Status"/> is set to the exact literal
/// <c>"filed"</c> (the first of the four Status-literal pins, §2.3
/// item 2).
/// </para>
/// <para>
/// U5 appends <c>AssignReportAsync</c> (F5, C-M3b·4, GlobalAdmin-gated),
/// <c>UnlockAsync</c> / <c>ResolveReportAsync</c> (F6, C-M3b·4, C5
/// flag-flip), and <c>CanReadWithReportAsync</c> (F2, C-M3b·2, the
/// <c>Via = Report</c> read branch) to this same file — U4's
/// deliverable is the ctor + this one write lane only.
/// </para>
/// </summary>
public sealed class ModerationService
{
    private readonly IUserInfoService _userInfo;
    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;

    public ModerationService(
        IUserInfoService userInfo,
        IAuthorizationService authz,
        IDocumentStore store)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz    = authz    ?? throw new ArgumentNullException(nameof(authz));
        _store    = store    ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// File a resident report against a post (F1; C-M3b·1). A
    /// **resident-facing intake** action — it makes **no**
    /// <see cref="IAuthorizationService"/> call (it is not an access
    /// decision; the precedent is M1's
    /// <c>IUserInfoService.UpsertProfileAsync</c>, also an intake
    /// write with no decision). It **does** append an
    /// <see cref="AccessAudit"/> row — with the pinned filing
    /// <c>Via</c> tag = <see cref="AccessVia.Admin"/> (two negatives:
    /// NOT <see cref="AccessVia.Report"/> — reserved for the
    /// <c>Via = Report</c> read branch, C-M3b·2; NOT
    /// <see cref="AccessVia.Owner"/> — the C1 owner-branch) — and
    /// writes <c>Report.Status = "filed"</c> (the exact literal per
    /// §2.3 item 2: the first of the four Status-literal pins).
    /// <para>
    /// C3 / ADR 0006-C (no silent unaudited access): the
    /// <see cref="Report"/> row and the <see cref="AccessAudit"/> row
    /// are **both** staged into the caller's
    /// <see cref="IDocumentSession"/> and committed in **one**
    /// <c>SaveChangesAsync</c> — the same-transaction / no-partial-write
    /// discipline (§2.3 item 4) holds: a failed save rolls back both
    /// rows atomically.
    /// </para>
    /// </summary>
    /// <param name="postId">The post the report is filed against (must
    /// exist in the caller's session or a <see cref="KeyNotFoundException"/>
    /// is thrown — no partial write).</param>
    /// <param name="actorId">The acting resident (the reporter; no
    /// delegation / break-glass path in this lane — resident-facing).</param>
    /// <param name="reason">Optional free-text reason supplied by the
    /// resident (nullable).</param>
    /// <param name="session">The caller's in-flight
    /// <see cref="IDocumentSession"/> — this service does not open one
    /// (the caller owns the transaction, per the shared
    /// IDocumentSession-overload convention C3 / ADR 0006-E: the same
    /// contract the write lanes on
    /// <see cref="Kumunita.Core.Posts.PostService"/> follow — e.g.
    /// <see cref="Kumunita.Core.Posts.PostService.CreatePostAsync"/> /
    /// <see cref="Kumunita.Core.Posts.PostService.CreateReplyAsync"/> /
    /// <see cref="Kumunita.Core.Posts.PostService.HidePostAsync"/> /
    /// <see cref="Kumunita.Core.Posts.PostService.RemovePostAsync"/>).</param>
    /// <returns>1 — the count of <see cref="Report"/> rows created by
    /// this call (a success signal; the only path past the guards
    /// wrote exactly one row).</returns>
    public async Task<int> FileReportAsync(
        string postId,
        string actorId,
        string? reason,
        IDocumentSession session)
    {
        if (string.IsNullOrEmpty(postId))  throw new ArgumentException("A post id is required.",  nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("A resident actor is required.", nameof(actorId));
        if (session is null)               throw new ArgumentNullException(nameof(session));

        // Load the post from the caller's session — the report is filed
        // against a specific post; a missing post is a failed call
        // (no report row, no audit row — no partial write).
        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{postId}' was not found; nothing to file a report against.");

        var now = DateTimeOffset.UtcNow;

        // Domain row: the report itself (C-M3b·1 — the resident-facing
        // intake write lane; the four Status-literal pins, §2.3 item 2:
        // this lane sets the exact literal "filed").
        var report = new Report
        {
            Id         = Guid.NewGuid().ToString("N"),
            PostId     = postId,
            ReporterId = actorId,
            ComponentId = post.ComponentId,   // carry the post's component scope (non-null per the M3 POCO, C-M3·2)
            Reason     = reason,
            Status     = "filed",             // §2.3 item 2 — exact literal pin (first of the four)
            At         = now
        };

        session.Store(report);

        // Audit row: the write-lane audit (C3 / ADR 0006-C — Always On).
        // NOT a CanAsync decision row: it is an intake-action audit row,
        // hand-written here (the M1 SetComponentModeratorAccessAsync
        // precedent — also a write-lane-with-no-decision call). The Via
        // tag is the pinned filing tag (§2.3 item 1 — Admin; two
        // negatives: not Report [reserved for the read branch, C-M3b·2],
        // not Owner [C1 owner-branch]).
        var audit = new AccessAudit
        {
            Id                    = Guid.NewGuid().ToString("N"),
            At                    = now,
            ActorId               = actorId,
            EffectivePrincipalId  = actorId,   // resident-facing: no delegation / break-glass path
            Action                = "report.file",
            TargetKind            = "post",
            TargetId              = postId,
            Via                   = AccessVia.Admin,   // §2.3 item 1 — exact literal pin
            Outcome             = AccessOutcome.Allow  // intake lane: Allow (no Deny path without a CanAsync call)
        };

        session.Store(audit);

        // C3 — one SaveChangesAsync: the Report row and the AccessAudit
        // row commit atomically (no partial write; ADR 0006-C; §2.3
        // item 4).
        await session.SaveChangesAsync().ConfigureAwait(false);

        return 1;   // one row created
    }

    /// <summary>
    /// Assign a report to a standing moderator (F5; C-M3b·4, SoD).
    /// GlobalAdmin-gated write lane: calls
    /// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource, Marten.IDocumentSession)"/>
    /// with <see cref="AccessAction.Moderate"/> *before* writing, in the
    /// caller's <see cref="Marten.IDocumentSession"/> (ADR 0006-E
    /// compatible lane — the audit row lands in the same transaction).
    /// <para>
    /// A <c>Decision.Allowed = false</c> result still writes the audit
    /// row (C3, ADR 0006-C — Allow and Deny) but does **not** write the
    /// <see cref="Report.Status"/> update or the
    /// <see cref="ModeratorAssignment"/> row (the "no partial write"
    /// discipline — §2.3 item 4 pin).
    /// </para>
    /// <para>
    /// On success: <see cref="Report.Status"/> = <c>"assigned"</c>
    /// (the §2.3 item 2 literal), <see cref="ModeratorAssignment"/>
    /// row written for (<paramref name="assignedToModeratorId"/>,
    /// <c>report.ComponentId</c>) with <c>GrantedBy =
    /// <paramref name="globalAdminId"/></c> (the SoD audit trail),
    /// <see cref="AccessAudit"/> row with <c>Via = decision.Via</c>.
    /// One <see cref="Marten.IDocumentSession.SaveChangesAsync"/>
    /// commits atomically (C3 — the domain write and the audit row are
    /// the same commit).
    /// </para>
    /// <para>
    /// The GlobalAdmin-only standing is *enforced by the Web layer*
    /// (U7's <c>ModerationController</c>, <c>[Authorize(Roles =
    /// GlobalAdmin)]</c>) — the Core <see cref="AccessAction.Moderate"/>
    /// gate is the SoD discriminator between a standing-moderator and a
    /// GlobalAdmin actor; the M1 <c>SetComponentModeratorAccessAsync</c>
    /// precedent holds the same split (Core trusts the caller, Web
    /// verifies the standing).
    /// </para>
    /// </summary>
    public async Task AssignReportAsync(
        string reportId,
        string assignedToModeratorId,
        string globalAdminId,
        IDocumentSession session)
    {
        if (string.IsNullOrEmpty(reportId))              throw new ArgumentException("A report id is required.",             nameof(reportId));
        if (string.IsNullOrEmpty(assignedToModeratorId)) throw new ArgumentException("An assigned moderator id is required.", nameof(assignedToModeratorId));
        if (string.IsNullOrEmpty(globalAdminId))         throw new ArgumentException("A GlobalAdmin actor id is required.",   nameof(globalAdminId));
        if (session is null)                             throw new ArgumentNullException(nameof(session));

        var report = await session.LoadAsync<Report>(reportId).ConfigureAwait(false);
        if (report is null)
            throw new KeyNotFoundException($"Report '{reportId}' was not found in the session; nothing to assign.");

        var post = await session.LoadAsync<Post>(report.PostId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{report.PostId}' (referenced by report '{reportId}') was not found in the session.");

        // SoD gate: the `Moderate` action discriminates a GlobalAdmin
        // writer from a standing-moderator writer (the M1 §A decision
        // algorithm's moderation branch is gated on
        // `Component.ModeratorAccess = true` AND a
        // `ModeratorAssignment` row — that's the *reader* side; the
        // *writer* side gate lives in the Web layer per ADR 0003). The
        // decision's audit row lands in the caller's transaction (ADR
        // 0006-E compatible lane) — no partial write either way.
        var decision = await _authz.CanAsync(
            globalAdminId, AccessAction.Moderate, new PostToAuditableResource(post), session)
            .ConfigureAwait(false);

        if (decision.Allowed)
        {
            var now = DateTimeOffset.UtcNow;

            report.Status = "assigned";   // §2.3 item 2 literal
            session.Store(report);

            if (report.ComponentId is not null)
            {
                var existingAssignments = await session
                    .Query<ModeratorAssignment>()
                    .Where(a => a.UserId == assignedToModeratorId && a.ComponentId == report.ComponentId)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var assignmentAssignment = existingAssignments.Count > 0
                    ? existingAssignments[0]
                    : new ModeratorAssignment
                    {
                        Id          = Guid.NewGuid().ToString("N"),
                        UserId      = assignedToModeratorId,
                        ComponentId = report.ComponentId
                    };
                assignmentAssignment.GrantedBy = globalAdminId;
                assignmentAssignment.At        = now;
                session.Store(assignmentAssignment);
            }

            session.Store(new AccessAudit
            {
                Id                   = Guid.NewGuid().ToString("N"),
                At                   = DateTimeOffset.UtcNow,
                ActorId              = globalAdminId,
                EffectivePrincipalId = globalAdminId,
                Action               = "report.assign",
                TargetKind           = "report",
                TargetId             = reportId,
                Via                  = decision.Via,
                Outcome              = AccessOutcome.Allow
            });
        }

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Unlock the report (F6; C-M3b·4, the "report-driven unlock" —
    /// the C5 activation event). GlobalAdmin-gated: the same SoD gate
    /// shape as <see cref="AssignReportAsync"/>. On success, calls
    /// <see cref="IUserInfoService.SetComponentModeratorAccessAsync"/>
    /// (the M1 flag-flip seam, *unchanged*, GlobalAdmin-gated) with
    /// <c>on = true</c> — the activation that enables the
    /// <see cref="CanReadWithReportAsync"/> read branch (C-M3b·2) for
    /// the standing moderator on *subsequent* renders.
    /// <para>
    /// The M1 seam opens its own session (M1's frozen shape — a
    /// separate commit from the caller's transaction); the
    /// <see cref="Report.Status"/> update and the
    /// <see cref="AccessAudit"/> row land in the caller's
    /// <see cref="Marten.IDocumentSession"/> transaction (C3 — the
    /// report-domain and report-audit rows commit atomically). The
    /// flag-flip is a separate commit per the M1 seam's contract;
    /// this is the "flag-flip commits separately" tension the §2.0
    /// drift-guard applies — the M1 seam is frozen (unit-series
    /// rule 4: never reshapes frozen M1 interfaces).
    /// </para>
    /// <para>
    /// <see cref="Report.Status"/> = <c>"unlocked"</c> (the §2.3
    /// item 2 literal).
    /// </para>
    /// </summary>
    public async Task UnlockAsync(
        string reportId,
        string globalAdminId,
        IDocumentSession session)
    {
        if (string.IsNullOrEmpty(reportId))      throw new ArgumentException("A report id is required.",         nameof(reportId));
        if (string.IsNullOrEmpty(globalAdminId)) throw new ArgumentException("A GlobalAdmin actor id is required.", nameof(globalAdminId));
        if (session is null)                     throw new ArgumentNullException(nameof(session));

        var report = await session.LoadAsync<Report>(reportId).ConfigureAwait(false);
        if (report is null)
            throw new KeyNotFoundException($"Report '{reportId}' was not found in the session; nothing to unlock.");

        var post = await session.LoadAsync<Post>(report.PostId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{report.PostId}' (referenced by report '{reportId}') was not found in the session.");

        var decision = await _authz.CanAsync(
            globalAdminId, AccessAction.Moderate, new PostToAuditableResource(post), session)
            .ConfigureAwait(false);

        if (decision.Allowed)
        {
            // M1 seam (frozen shape — own session, own commit). The
            // "same IDocumentSession transaction" pin in §2.4 item 2
            // applies to the report-domain + audit rows (the caller's
            // transaction); the flag-flip seam commits separately per
            // its M1 contract.
            if (report.ComponentId is not null)
                await _userInfo.SetComponentModeratorAccessAsync(
                    report.ComponentId, true, globalAdminId)
                    .ConfigureAwait(false);

            report.Status = "unlocked";
            session.Store(report);

            session.Store(new AccessAudit
            {
                Id                   = Guid.NewGuid().ToString("N"),
                At                   = DateTimeOffset.UtcNow,
                ActorId              = globalAdminId,
                EffectivePrincipalId = globalAdminId,
                Action               = "report.unlock",
                TargetKind           = "report",
                TargetId             = reportId,
                Via                  = decision.Via,
                Outcome              = AccessOutcome.Allow
            });
        }

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Resolve the report (F6; C-M3b·4, the "resolve" counterpart to
    /// <see cref="UnlockAsync"/>). Same SoD / audit shape as
    /// <see cref="UnlockAsync"/>; <see cref="Report.Status"/> =
    /// <c>"resolved"</c> (the §2.3 item 2 literal). Also calls
    /// <see cref="IUserInfoService.SetComponentModeratorAccessAsync"/>
    /// with <c>on = true</c> on success (the flag-flip — see
    /// <see cref="UnlockAsync"/>'s doc-comment for the "separate
    /// commit" note).
    /// </summary>
    public async Task ResolveReportAsync(
        string reportId,
        string globalAdminId,
        IDocumentSession session)
    {
        if (string.IsNullOrEmpty(reportId))      throw new ArgumentException("A report id is required.",         nameof(reportId));
        if (string.IsNullOrEmpty(globalAdminId)) throw new ArgumentException("A GlobalAdmin actor id is required.", nameof(globalAdminId));
        if (session is null)                     throw new ArgumentNullException(nameof(session));

        var report = await session.LoadAsync<Report>(reportId).ConfigureAwait(false);
        if (report is null)
            throw new KeyNotFoundException($"Report '{reportId}' was not found in the session; nothing to resolve.");

        var post = await session.LoadAsync<Post>(report.PostId).ConfigureAwait(false);
        if (post is null)
            throw new KeyNotFoundException($"Post '{report.PostId}' (referenced by report '{reportId}') was not found in the session.");

        var decision = await _authz.CanAsync(
            globalAdminId, AccessAction.Moderate, new PostToAuditableResource(post), session)
            .ConfigureAwait(false);

        if (decision.Allowed)
        {
            if (report.ComponentId is not null)
                await _userInfo.SetComponentModeratorAccessAsync(
                    report.ComponentId, true, globalAdminId)
                    .ConfigureAwait(false);

            report.Status = "resolved";
            session.Store(report);

            session.Store(new AccessAudit
            {
                Id                   = Guid.NewGuid().ToString("N"),
                At                   = DateTimeOffset.UtcNow,
                ActorId              = globalAdminId,
                EffectivePrincipalId = globalAdminId,
                Action               = "report.resolve",
                TargetKind           = "report",
                TargetId             = reportId,
                Via                  = decision.Via,
                Outcome              = AccessOutcome.Allow
            });
        }

        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The <see cref="AccessVia.Report"/> read branch (F2; C-M3b·2) —
    /// the **standalone read lane** (ADR 0006-E "compatible lane"; the
    /// **single** new authorization-surface addition M3b makes).
    /// <para>
    /// The shape, per §2.4 item 1: **new method on
    /// <see cref="ModerationService"/>** (NOT a branch inside
    /// <c>AuthorizationService.Decide</c> — that would couple the
    /// M1-frozen decision algorithm to <see cref="Report"/> reads, a
    /// rule 4 violation), calling the **standalone**
    /// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// overload (the **own-commit** variant — M3's
    /// <c>PostService.GetPostAsync</c> precedent, the "audit-row in
    /// own commit" shape).
    /// </para>
    /// <para>
    /// **C5 unactivated = still no access** (C-M3b·2, §2.4 item 4):
    /// a <see cref="ModeratorAssignment"/>-holding viewer with **no**
    /// filed <see cref="Report"/> for this post's
    /// (<c>PostId</c>) is **denied** — the branch is triggered by the
    /// *filed report*, not by the <see cref="AccessAction.Moderate"/>
    /// action alone. The "moderator with no filed report" case is
    /// handled explicitly here: a Deny <see cref="AccessAudit"/> row
    /// with <see cref="AccessVia.Report"/> is written (the §2.4 item 3
    /// literal pin) and the call returns a deny decision.
    /// </para>
    /// <para>
    /// When a **filed report exists** for the post, this method
    /// delegates to the **standalone** <see cref="CanAsync(string,
    /// AccessAction, IAuditableResource)"/> overload (the own-commit
    /// variant) — the audit row is written by the M1 frozen seam in
    /// its own commit, and the <see cref="Decision"/> it returns is
    /// the result to the caller.
    /// </para>
    /// </summary>
    /// <returns>The <see cref="Decision"/> — <c>Allowed = true</c> iff
    /// the actor's standing (via the M1-§A decision algorithm) allows
    /// a <see cref="AccessAction.Read"/> on the post *and* a filed
    /// <see cref="Report"/> exists for that post. The Web layer
    /// renders 403 on <c>Allowed = false</c>.</returns>
    public async Task<Decision> CanReadWithReportAsync(
        string postId, string actorId)
    {
        if (string.IsNullOrEmpty(postId))  throw new ArgumentException("A post id is required.",  nameof(postId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An actor id is required.", nameof(actorId));

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var post = await session.LoadAsync<Post>(postId).ConfigureAwait(false);
        if (post is null)
        {
            // A missing post is a decision-less failure (no audit row
            // to write — there's no access decision being made; the
            // caller should handle a 404 in the Web layer).
            return new Decision(false, AccessVia.Report, actorId);
        }

        var filedReports = await session
            .Query<Report>()
            .Where(r => r.PostId == postId)
            .ToListAsync()
            .ConfigureAwait(false);

        if (filedReports.Count == 0)
        {
            // C5 unactivated — no filed report for this post. The
            // C-M3b·2 pin: the branch is triggered by the *filed
            // report*, not by the `Moderate` action alone. We write
            // the Deny audit row *here* (this method's own commit —
            // the standalone lane's C3 shape), carrying the pinned
            // `AccessVia.Report` (the §2.4 item 3 literal on the read
            // branch — one of the only two `Via` tags M3b writes:
            // this one and the filing `Admin` literal in
            // `FileReportAsync`).
            session.Store(new AccessAudit
            {
                Id                   = Guid.NewGuid().ToString("N"),
                At                   = DateTimeOffset.UtcNow,
                ActorId              = actorId,
                EffectivePrincipalId = actorId,
                Action               = "read",
                TargetKind           = "post",
                TargetId             = postId,
                Via                  = AccessVia.Report,
                Outcome              = AccessOutcome.Deny
            });
            await session.SaveChangesAsync().ConfigureAwait(false);

            return new Decision(false, AccessVia.Report, actorId);
        }

        // A filed report exists — delegate to the **standalone
        // <see cref="IAuthorizationService.CanAsync(string,
        // AccessAction, IAuditableResource)"/> overload (the
        // own-commit variant — §2.4 item 1 pin: the "audit-row in own
        // commit" shape, the M3 <c>PostService.GetPostAsync</c>
        // precedent). The M1 frozen seam handles the §A decision
        // algorithm; its audit row commits in its own transaction.
        // If the decision denies even with a filed report, the
        // denial is the M1 seam's (the C-M3b·2 read branch is a gate,
        // not an override — it only enables the standing-moderator
        // branch to run; it does not force-allow).
        return await _authz.CanAsync(
            actorId, AccessAction.Read, new PostToAuditableResource(post))
            .ConfigureAwait(false);
    }
}
