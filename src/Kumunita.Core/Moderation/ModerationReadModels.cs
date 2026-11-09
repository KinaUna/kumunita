using Kumunita.Core.Posts;

namespace Kumunita.Core.Moderation;

/// <summary>
/// One row of a reporter's <c>My reports</c> list (IMPROVE-UX R05 — the
/// reporter-facing content-report read seam's row shape, the D1 "My reports"
/// shape from <c>docs/plans-milestones/in-progress/plan-improve-ux.md</c>).
/// The row carries the report's identity + context
/// (<see cref="ReportId"/> / <see cref="PostId"/> / <see cref="ComponentId"/>
/// / <see cref="Reason"/> / <see cref="At"/>), its status
/// (<see cref="Status"/>), and the R03 resolution shape
/// (<see cref="Outcome"/> + <see cref="ResolutionNote"/> +
/// <see cref="ResolvedAt"/> / <see cref="ResolvedBy"/> — all four
/// <c>null</c> until the <c>ResolveReportAsync</c> lane writes them).
/// <para>
/// <see cref="TargetTitle"/> is the target's title (the ADR 0173
/// <c>TargetKind</c> switch), resolved the same way the
/// <c>GetDetailAsync</c> read seam does — **but gated on the reporter's
/// visibility**: for a <b>post</b> target (the original M3b shape,
/// <c>TargetKind == null</c>) the title is shown only while the reporter can
/// still read the post (denied / hidden / removed / deleted →
/// <c>null</c>, the R05 DoD "target no longer visible → no title" pin). For
/// the other target kinds (event / project / board / todo / grouppost) the
/// title is resolved directly (their visibility rules are out of scope for
/// R05 — the plan is posts-first, A02).
/// </para>
/// </summary>
public sealed record MyReportRow
{
    public required string ReportId { get; init; }
    public required string PostId { get; init; }
    public string? ComponentId { get; init; }
    public string? Reason { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset At { get; init; }
    public string? TargetKind { get; init; }
    public string? TargetTitle { get; init; }
    public ReportOutcome? Outcome { get; init; }
    public string? ResolutionNote { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public string? ResolvedBy { get; init; }
}

/// <summary>
/// One resolved row of the <c>/moderation</c> queue (IMPROVE-CODE C09 — the
/// <see cref="ModerationController"/>'s raw Marten reads moved behind the
/// <see cref="ModerationService"/> read seam, ADR 0006 §D "route + authz +
/// shape"). The row's display fields are resolved here (the component's
/// name, the reporter's display name, the reply / target's title + author
/// name); the Web layer maps this onto its <c>ReportRow</c> view-model
/// verbatim (the served HTML is unchanged).
/// </summary>
public sealed record ModerationQueueRow
{
    public required string Id { get; init; }
    public required string PostId { get; init; }
    public string? PostTitle { get; init; }
    public string? ComponentId { get; init; }
    public string? ComponentName { get; init; }
    public string? ReporterName { get; init; }
    public string? Reason { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset At { get; init; }
    public string? ReplyId { get; init; }
    public string? ReplyAuthorName { get; init; }
    public string? TargetKind { get; init; }
    public string? TargetId { get; init; }
    public string? TargetTitle { get; init; }
    public string? TargetAuthorName { get; init; }
}

/// <summary>
/// The scoped queue (IMPROVE-CODE C09) — the actor's in-scope rows in the
/// queue's order (<c>At</c> desc, <c>Status</c> desc) + the per-status
/// counts over the **scoped** rows (the controller's previous
/// <c>ByStatus</c> shape).
/// </summary>
public sealed record ModerationQueueReadModel
{
    public required IReadOnlyList<ModerationQueueRow> Rows { get; init; }
    public required IReadOnlyDictionary<string, int> ByStatus { get; init; }
}

/// <summary>
/// One standing-moderator row for the assign form (the
/// <c>StandingModerator</c> display shape, moved with the detail seam).
/// </summary>
public sealed record ModerationStandingModerator(string UserId, string? DisplayName);

/// <summary>
/// The <c>GET /moderation/{id}</c> detail (IMPROVE-CODE C09) — the report +
/// its resolved target (post, or event / project / board / todo / grouppost
/// via <c>TargetKind</c> / <c>TargetId</c>) + the reply discriminator (ADR
/// 0023) + the standing-moderator list for the assign form. The Web layer
/// maps this onto <c>ModerationResolveViewModel</c> verbatim (the served
/// HTML is unchanged); the <c>IsAssignable</c> / <c>IsUnlockable</c> /
/// <c>IsResolvable</c> display predicates stay in the controller (they are
/// pure shape over <c>Status</c> + <c>Moderators.Count</c> + the actor's
/// GlobalAdmin standing).
/// </summary>
public sealed class ModerationDetailReadModel
{
    public required string ReportId { get; init; }
    public required string PostId { get; init; }
    public string? ComponentId { get; init; }
    public string? ComponentName { get; init; }
    public string? Reason { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset At { get; init; }
    public string? ReporterName { get; init; }
    public string? PostTitle { get; init; }
    public string? PostBody { get; init; }
    public string? PostAuthorName { get; init; }
    public string? ReplyId { get; init; }
    public string? ReplyBody { get; init; }
    public string? ReplyAuthorName { get; init; }
    public string? TargetKind { get; init; }
    public string? TargetId { get; init; }
    public string? TargetTitle { get; init; }
    public string? TargetAuthorName { get; init; }
    public required IReadOnlyList<ModerationStandingModerator> Moderators { get; init; }
}
