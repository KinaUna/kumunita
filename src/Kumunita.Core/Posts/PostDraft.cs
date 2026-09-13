using Authorization = Kumunita.Core.Authorization;

namespace Kumunita.Core.Posts;

/// <summary>
/// <see cref="PostService.CreatePostAsync"/>'s input (M3 design §2.2).
/// <see cref="Audience"/> is **non-null** (invariant C1 — the deny-by-default
/// posture; the author's bootstrap default is an *empty* audience, so the
/// owner branch is the only lane that lets the author see their own draft).
/// Written **verbatim** into the <see cref="Post"/> row (ADR 0001-B — the
/// author's choice is absolute; M3's seam test
/// <c>AuthorAudienceWrittenVerbatim</c> pins the DB row's
/// <c>Audience</c> as bit-identical to this input).
/// </summary>
public sealed record PostDraft(
    string ComponentId,
    string? Title,
    string Body,
    Authorization.Audience Audience,
    // ADR 0018 authored-in-language tag (ADR 0005 B). Optional trailing
    // parameter: a null/empty code is materialized from the instance default
    // at write time (see PostService.CreatePostAsync), so the existing
    // positional call sites (7 in PostServiceTests) keep compiling unchanged
    // and post the instance default.
    string? LanguageCode = null);
