using Kumunita.Core.Privacy;

namespace Kumunita.Core.Posts;

/// <summary>
/// The **resolution outcome** of a report (IMPROVE-UX R03). Written only
/// by the <c>ResolveReportAsync</c> lane when a moderator closes the
/// report — <see cref="Report.Outcome"/> is <c>null</c> until that
/// moment. The closed vocabulary is the D1 "My reports" shape in
/// <c>docs/plans-milestones/in-progress/plan-improve-ux.md</c>:
/// the moderator's chosen action on the target, stored verbatim so
/// the reporter's "My reports" page can render it and the A02
/// outcome-execution lane can re-derive the same action on the
/// target post (posts first).
/// </summary>
public enum ReportOutcome
{
    /// <summary>The moderator reviewed the report and took no action on the target.</summary>
    NoAction,
    /// <summary>The target was hidden (the A02 action: <c>HidePostAsync</c> — reversible via <c>UnhidePostAsync</c>).</summary>
    Hidden,
    /// <summary>The target was removed (the A02 action: <c>RemovePostAsync</c> — the hard lane).</summary>
    Removed,
    /// <summary>An outcome outside the closed three (a moderator free-text note explains).</summary>
    Other
}

/// <summary>
/// A *dormant* report row (M3b workflow). The **table** is registered in M3
/// for forward compatibility (the Q1↔Q3 resolution: the table in M3, the flow
/// in M3b); M3's surface ships **no** workflow, **no** tests, and **no**
/// <see cref="Status"/> writes against it. <see cref="Status"/> is nullable
/// until M3b lands a write lane that sets it.
/// <para>
/// IMPROVE-UX R03 adds the **resolution shape** — the <see
/// cref="Outcome"/> enum (the D1 closed vocabulary), the
/// <see cref="ResolutionNote"/> (≤500 chars, the moderator's
/// free-text for the <see cref="ReportOutcome.Other"/> / nuance
/// cases), and the <see cref="ResolvedAt"/> /
/// <see cref="ResolvedBy"/> pair (the same audit shape
/// <see cref="At"/> / <see cref="ReporterId"/> carries for the
/// filing lane). All four are <c>null</c> until the
/// <c>ResolveReportAsync</c> lane writes them (ADR 0004 §B.1
/// additive — Marten delta-detects the new columns; pre-existing
/// rows keep <c>null</c> and need no back-fill).
/// </para>
/// </summary>
[DataClass(DataClassKind.AuditModeration, Erasure.Pseudonymize, ExportScope.Never,
    reason: "F1 §2C decision (now decided): moderation state, not authored content (IS23); M27 C-M27·3 excludes it from the delete set",
    principalField: "ReporterId")]
public sealed class Report
{
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string ReporterId { get; set; } = string.Empty;
    public string? ComponentId { get; set; }
    public string? Reason { get; set; }
    public string? Status { get; set; }   // null until M3b's write lane sets it
    public DateTimeOffset At { get; set; }

    /// <summary>
    /// The report's **target surface** (ADR 0173, the report-targets lane):
    /// one of the closed vocabulary <c>post</c> / <c>reply</c> /
    /// <c>event</c> / <c>project</c> / <c>board</c> / <c>todo</c> /
    /// <c>grouppost</c>. <b>Derived at display time</b> for the two
    /// pre-existing discriminators — a <c>null</c> <see cref="ReplyId"/>
    /// is a <c>post</c>-targeted report, a set <see cref="ReplyId"/> a
    /// <c>reply</c>-targeted one — so no new column is required for the
    /// original M3b shape and pre-existing rows stay valid (ADR 0004 §B.1
    /// additive-only). New filing lanes set this directly.
    /// </summary>
    public string? TargetKind { get; set; }

    /// <summary>
    /// The target's own id for the new lanes (ADR 0173): an
    /// <c>event</c> / <c>project</c> / <c>board</c> / <c>todo</c> /
    /// <c>grouppost</c> document id. <b>Null</b> for the original
    /// <c>post</c> and <c>reply</c> discriminators (their ids live on
    /// <see cref="PostId"/> / <see cref="ReplyId"/>). ADR 0004 §B.1
    /// additive (delta-detected, idempotent; no migration or back-fill).
    /// </summary>
    public string? TargetId { get; set; }

    /// <summary>
    /// The reply a report targets — <b>nullable</b> (an ADR 0004 §B.1
    /// additive POCO field: Marten delta-detects the new column and it is
    /// idempotent across re-seed, so pre-existing rows keep <c>null</c> and
    /// need no migration or back-fill).
    /// <para>
    /// <b>Target discriminator (ADR 0023, the reply-report-target lane):</b>
    /// <c>null</c> means the report is against the <see cref="PostId"/> itself
    /// (the original M3b shape); <b>non-<c>null</c></b> means the report is
    /// against a specific <see cref="Kumunita.Core.Posts.PostReply"/> of that
    /// post. <see cref="PostId"/> is populated in <b>both</b> cases — for a
    /// reply report it is the reply's parent post (the reply's own
    /// <see cref="Kumunita.Core.Posts.PostReply.PostId"/>), which keeps the
    /// queue/resolve read path on the single post key and the C-M3·1
    /// "reply-inherits" visibility rule intact (a reply has no own audience;
    /// seeing the post is what makes the reply reportable in the first place).
    /// </para>
    /// </summary>
    public string? ReplyId { get; set; }

    // ── IMPROVE-UX R03 — the resolution shape (ADR 0004 §B.1 additive;
    //    the single new surface on the Report POCO for the D1 "My
    //    reports" + A02 outcome-execution lanes). All four are nullable
    //    (a filed-but-not-yet-resolved report keeps them null; pre-
    //    R03 rows stay null without back-fill; Marten delta-detects
    //    the new columns idempotently at boot, the ADR 0004 §B.1
    //    additive discipline) and written **only** by
    //    <see cref="Kumunita.Core.Moderation.ModerationService"/>
    //    <c>ResolveReportAsync</c> — the filing / assign / unlock
    //    lanes do not touch them (the same lane-isolation invariant
    //    the M3b Status-literal pins enforce). ──

    /// <summary>
    /// The moderator's chosen outcome on the report's target (IMPROVE-UX
    /// R03, the D1 closed vocabulary). <c>null</c> until the
    /// <c>ResolveReportAsync</c> lane writes it (the "filed" state). The
    /// A02 outcome-execution lane re-derives the same action on the
    /// target post from this value — the <see cref="ReportOutcome.Hidden"/>
    /// value maps to <c>PostService.HidePostAsync</c>,
    /// <see cref="ReportOutcome.Removed"/> to
    /// <c>PostService.RemovePostAsync</c>, and the other two are
    /// no-action / free-text (the A02 lane decides the post-target-only
    /// scope and the reply-target TODO ❓).
    /// </summary>
    public ReportOutcome? Outcome { get; set; }

    /// <summary>
    /// The moderator's free-text note for the reporter (IMPROVE-UX R03;
    /// ≤500 chars, enforced by the <c>ResolveReportAsync</c> lane before
    /// the store). The D1 "My reports" page renders this verbatim
    /// (the "moderator-picked outcome + optional note" shape); the
    /// R07 <c>ReportResolved</c> notification body re-uses it.
    /// <c>null</c> for no-note resolutions (the common
    /// <see cref="ReportOutcome.Hidden"/> /
    /// <see cref="ReportOutcome.Removed"/> shape) and for every
    /// pre-resolution state.
    /// </summary>
    public string? ResolutionNote { get; set; }

    /// <summary>
    /// The instant (UTC) the <c>ResolveReportAsync</c> lane wrote the
    /// <see cref="Outcome"/> + <see cref="ResolutionNote"/> pair
    /// (IMPROVE-UX R03). Mirrors the filing lane's <see cref="At"/>
    /// (same audit shape: the pair is written together, in the same
    /// transaction as the lane's <c>AccessAudit</c> row).
    /// </summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>
    /// The GlobalAdmin / standing-moderator's <c>SubjectId</c> who
    /// resolved the report (IMPROVE-UX R03) — the <c>globalAdminId</c>
    /// parameter the <c>ResolveReportAsync</c> lane carries. Mirrors the
    /// filing lane's <see cref="ReporterId"/> (same principal shape;
    /// the pseudonymizer's <see cref="DataClassAttribute.PrincipalField"/>
    /// stays <c>ReporterId</c> — the resolution principal is the
    /// moderator's, a different axis, and the ADR 0298 erasure rule
    /// (principalField = the resident-facing principal) does not extend
    /// to it).
    /// </summary>
    public string? ResolvedBy { get; set; }
}
