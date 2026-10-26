namespace Kumunita.Core.Posts;

/// <summary>
/// A *dormant* report row (M3b workflow). The **table** is registered in M3
/// for forward compatibility (the Q1↔Q3 resolution: the table in M3, the flow
/// in M3b); M3's surface ships **no** workflow, **no** tests, and **no**
/// <see cref="Status"/> writes against it. <see cref="Status"/> is nullable
/// until M3b lands a write lane that sets it.
/// </summary>
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
}
