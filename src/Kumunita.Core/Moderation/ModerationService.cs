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
    /// <see cref="IDocumentSession"/> — U4 does not open one (the
    /// caller owns the transaction, per the IDocumentSession-overload
    /// convention C3 / ADR 0006-E, shared by M3's
    /// <see cref="Kumunita.Core.Posts.PostService.CreatePostAsync"/> /
    /// <see cref="Kumunita.Core.Posts.PostService.CreateReplyAsync"/> /
    /// U3's <see cref="Kumunita.Core.Posts.PostService.HidePostAsync"/>
    /// / <see cref="Kumunita.Core.Posts.PostService.RemovePostAsync"/>).</param>
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
}
