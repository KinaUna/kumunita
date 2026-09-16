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
    string? LanguageCode = null,
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
    // **nullable** default: the existing positional call sites (7 in
    // PostServiceTests) keep compiling unchanged (they omit it ⇒ null), and
    // PostService.CreatePostAsync coalesces null → []. See the U04 handoff note.
    IReadOnlyList<string>? ImageIds = null,
    // ATT U4 (C-ATT·4; design doc §2.3) — the file-attachment references,
    // **server-side** (the Web layer parses the body's /attachment/{id} links
    // via AttachmentIds.ExtractAttachmentIds before calling the service — Core
    // stays body-parse-free, C-ATT·4). Written onto the Post doc (ADR 0004 §B.1
    // additive field, **separate from** ImageIds — C-ATT·5) with a
    // null-coalesce to an empty list (the POCO field is non-null, `= []`).
    //
    // §Pinned contract amendment (ATT U4) — the design doc pins this as
    // `IReadOnlyList<string> AttachmentIds = []`, but a collection expression
    // is **not** a legal C# default parameter value (CS1736: default values
    // must be compile-time constants). The closest source-compatible shape is
    // a **nullable** default: the existing positional call sites keep
    // compiling unchanged (they omit it ⇒ null), and
    // PostService.CreatePostAsync coalesces null → [].
    IReadOnlyList<string>? AttachmentIds = null);
