namespace Kumunita.Core.Posts;

/// <summary>
/// A group-post create input (ADR 0013, G·2/G·8) — <see cref="PostService.CreatePostAsync"/>'s
/// group-lane analog (<see cref="GroupPostDraft"/>'s input to
/// <see cref="PostService.CreateGroupPostAsync"/>). <see cref="GroupId"/> is
/// the non-empty channel (enforced in <see cref="PostService.CreateGroupPostAsync"/>:
/// null/empty ⇒ <see cref="ArgumentException"/> **before** any decision — no
/// audit row is written for such input, G·3); <see cref="Title"/> optional (M3's
/// <see cref="PostDraft"/> nullable-Title handling); <see cref="Body"/> non-null
/// (the <see cref="Post.Body"/> non-null invariant).
/// <para>
/// **No <see cref="Kumunita.Core.Authorization.Audience"/> member** (G·8): the
/// service writes the audience non-null **empty** — the author's choice is
/// *which group* (the lane), not an audience, so an <see cref="Audience"/> is
/// deliberately not on this record (contrast <see cref="PostDraft"/>'s
/// <see cref="PostDraft.Audience"/>).
/// </para>
/// </summary>
public sealed record GroupPostDraft(string GroupId, string? Title, string Body);
