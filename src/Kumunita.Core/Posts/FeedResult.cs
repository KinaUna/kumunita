namespace Kumunita.Core.Posts;

/// <summary>
/// <see cref="PostService.ListFeedAsync"/>'s result (M3 design §2.3 — the
/// C-M3·3 aggregate shape). <see cref="Visible"/> holds the source
/// <see cref="Post"/> documents the single <c>CanSeeAsync</c> call over the
/// component's candidate set actually allowed — never a hidden post's fields
/// (F1/F2). <see cref="HiddenCount"/> counts only the candidates that call
/// evaluated (a post dropped by the U7 Web-layer candidate gate — missing
/// component, unauthenticated — is not "hidden" here: no decision ran, so no
/// audit row names it — C-M3·2). One aggregate <c>AccessAudit</c> row
/// (<c>TargetKind = "post"</c>, via the U5 adapter) is the row for this visit
/// (C-M3·3).
/// <para>
/// <see cref="Total"/> is the **candidate-set count** (pre-decision, C-M7·7);
/// never a viewer-facing total (D1) — ADR 0090 D2. <see cref="HasMore"/> is
/// the sole paging signal (D1, C-M7·4): <c>true</c> iff the page's candidate
/// set filled the page.
/// </para>
/// </summary>
public sealed record FeedResult(
    IReadOnlyList<Post> Visible,
    int HiddenCount,
    int Page,
    int Total,                 // candidate-set count (pre-decision, C-M7·7); never a viewer-facing total (D1)
    bool HasMore);             // D1 — the sole paging signal: candidates.Count == PageSize
