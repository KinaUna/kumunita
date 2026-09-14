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
// ADR 0018 authored-in-language tag (ADR 0005 B) is the optional trailing
// parameter: a null/empty code is materialized from the instance default at
// write time (see PostService.CreateGroupPostAsync), so the existing
// positional call sites (5 in GroupPostServiceTests) keep compiling unchanged
// and post the instance default.
// RC R·3/R·7 (ADR 0025) — the content-image references, **server-side**
// (the Web layer parses the body's /content-image/{id} links via
// ContentImageIds.ExtractContentImageIds before calling the service — Core
// stays body-parse-free, R·5). Written onto the Post doc (ADR 0004 §B.1
// additive field) with a null-coalesce to an empty list (the POCO field is
// non-null, `= []`).
//
// §Pinned contract amendment (U04) — the design doc pins this as
// `IReadOnlyList<string> ImageIds = []`, but a collection expression is
// **not** a legal C# default parameter value (CS1736: default values must be
// compile-time constants). The closest source-compatible shape is a
// **nullable** default: the existing positional call sites (5 in
// GroupPostServiceTests) keep compiling unchanged (they omit it ⇒ null), and
// PostService coalesces null → []. See the U04 handoff note.
public sealed record GroupPostDraft(string GroupId, string? Title, string Body, string? LanguageCode = null, IReadOnlyList<string>? ImageIds = null);
