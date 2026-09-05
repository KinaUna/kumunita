namespace Kumunita.Core.Posts;

/// <summary>
/// <see cref="PostService.GetPostAsync"/>'s result (M3 design §2.3-§2.4 —
/// the C-M3·3 single-decision-row shape). <see cref="Post"/> is the already-loaded
/// <see cref="Post"/> document, null in two fail-closed cases the Web layer
/// maps to distinct responses: the post does not exist (no decision ran, no
/// <c>AccessAudit</c> row — the Core fail-closed shape M2's
/// <see cref="UserInfo.DirectoryDetail"/> pins) vs. the visit was Deny'd
/// (the <c>CanAsync</c> decision's row <i>was</i> written — invariant C3 —
/// and the Web layer renders 403). <see cref="Replies"/> is the one-level
/// reply list returned **as-is** under the parent's single <c>Read</c>
/// decision (invariant C-M3·1): parent Allow ⇒ replies rendered; parent Deny
/// ⇒ replies **not evaluated**, **no** reply of its own produces an
/// <c>AccessAudit</c> row, so the caller must not render them. Exactly one
/// decision call per visit (C6) — this service adds **no** second call on
/// the replies.
/// </summary>
public sealed record PostDetailResult(Post? Post, IReadOnlyList<PostReply> Replies);
