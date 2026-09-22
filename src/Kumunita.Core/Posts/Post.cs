namespace Kumunita.Core.Posts;

/// <summary>
/// The hidden/removed surface (M3b, C-M3b·3). The **enum is the single
/// M3b ADD on the Post POCO** (ADR 0004 §B.1 additive — delta-detected,
/// idempotent, no re-seed); the default is <see cref="Active"/> so a
/// post's <c>Status == null</c> check is not needed (the enum defaults to
/// the active state).
/// </summary>
public enum PostStatus
{
    /// <summary>The posted state (M3's behavior, unchanged for a visible post).</summary>
    Active,
    /// <summary>Soft-hidden by a <see cref="Kumunita.Core.Authorization.AccessAction.Moderate"/>-gated write lane (F3; C-M3b·3).</summary>
    Hidden,
    /// <summary>Hard-removed by a <see cref="Kumunita.Core.Authorization.AccessAction.Moderate"/>-gated write lane (F4; C-M3b·3).</summary>
    Removed
}

/// <summary>
/// A post (M3). <see cref="Audience"/> is **non-null** (invariant C1 — empty audience
/// denies; the author's bootstrap default is an *empty* audience, so the owner branch
/// is the *only* lane that lets the author see their own draft).
/// <see cref="ComponentId"/> is a **feed organizer**, never an access boundary (C-M3·2).
/// <see cref="Status"/> is the M3b hide/remove surface (C-M3b·3) — the single
/// M3b ADD on this POCO (ADR 0004 §B.1 additive).
/// </summary>
public sealed class Post
{
    public string Id { get; set; } = string.Empty;
    public string ComponentId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    public Authorization.Audience Audience { get; set; } = null!;
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }

    // M3b ADD (C-M3b·3, ADR 0004 §B.1 additive; the single new Post field):
    /// <summary>
    /// The hide/remove surface (M3b C-M3b·3, F3/F4). Written only by the
    /// <see cref="Kumunita.Core.Authorization.AccessAction.Moderate"/>-gated
    /// write lanes (<see cref="PostService.HidePostAsync"/> /
    /// <see cref="PostService.RemovePostAsync"/>). Default
    /// <see cref="PostStatus.Active"/>.
    /// </summary>
    public PostStatus Status { get; set; } = PostStatus.Active;

    // group posts ADD (ADR 0013, ADR 0004 §B.1 additive — the single new
    // Post field after M3b's Status):
    /// <summary>
    /// The group channel this post belongs to (ADR 0013). **Non-empty ⇒
    /// group-lane post** (G·2): membership is the **sole** access decision
    /// (G·1 — the audience lane is never evaluated; G·8 — the audience is
    /// written non-null **empty**), <see cref="ComponentId"/> is **empty**
    /// (lane exclusivity — the post is structurally absent from
    /// <see cref="PostService.ListFeedAsync"/> / <see cref="PostService.ListAllFeedAsync"/>,
    /// design doc §2.3(a)), and only members may create or see it (G·3/G·4).
    /// Empty
    /// (the default) ⇒ component post (M3/M3b), unchanged. Written **only**
    /// via <c>PostService.CreateGroupPostAsync</c>.
    /// </summary>
    public string GroupId { get; set; } = string.Empty;

    // ADR 0018 authored-in-language ADD (ADR 0004 §B.1 additive — the third
    // additive Post field after M3b's Status and ADR 0013's GroupId):
    /// <summary>
    /// The BCP-47 code of the language this post was **authored in** (ADR 0018,
    /// ADR 0005 B). Written at create time
    /// (<see cref="PostService.CreatePostAsync"/> /
    /// <see cref="PostService.CreateGroupPostAsync"/>) and, as the ADR 0014 /
    /// 0016 edit lanes were amended in 2026-09-13, also on the author-only edit
    /// lane (<see cref="PostService.UpdatePostAsync"/> /
    /// <see cref="PostService.UpdateGroupPostAsync"/>) so an author can correct
    /// the language the post was written in. Materialized from the
    /// instance default (<see cref="Kumunita.Core.Localization.LocaleSettings.DefaultLanguageCode"/>,
    /// with <c>en</c> as the floor) when the author leaves it unchosen, so no
    /// stored row is empty. **Not a translation mechanism** (ADR 0005 C is
    /// unchanged — the body is never machine-translated by the platform); it is
    /// the tag a future search surface and any user-added translations key off.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    // ADR 0024 author soft-delete ADD (ADR 0004 §B.1 additive — the fourth
    // additive Post field after M3b's Status, ADR 0013's GroupId, ADR 0018's
    // LanguageCode):
    /// <summary>
    /// Set when the <b>author</b> soft-deletes this post (ADR 0024);
    /// <c>null</c> while it is live. Distinct from
    /// <see cref="PostStatus.Hidden"/> / <see cref="PostStatus.Removed"/>
    /// (the M3b <b>moderator</b> surface) — this is the author's own
    /// "take it down" action. The record is kept (never hard-deleted) and
    /// read lanes (feeds) hide it, but the replies it parents are their own
    /// documents and remain visible (ADR 0024). Additive — Marten's schema
    /// builder picks up the new field on the existing doc-type surface
    /// (ADR 0004 §B.1); no seed reset, no schema-file change.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // RC U03 content-image ADD (ADR 0004 §B.1 additive — the 5th additive Post
    // field after M3b's Status, ADR 0013's GroupId, ADR 0018's LanguageCode,
    // ADR 0024's DeletedAt):
    /// <summary>
    /// The content images referenced by <see cref="Body"/> — the
    /// <c>MediaObject</c> ids appearing as <c>/content-image/{id}</c> links in
    /// the rendered body (RC R·3). Populated server-side by the owning write
    /// lane (RC U04/U05); the serving route's reverse lookup
    /// (<see cref="PostService.FindPostByImageIdAsync"/>) reads this (RC R·4).
    /// The 5th additive field after <see cref="Status"/> (M3b),
    /// <see cref="GroupId"/> (ADR 0013), <see cref="LanguageCode"/> (ADR 0018),
    /// <see cref="DeletedAt"/> (ADR 0024) (ADR 0004 §B.1 — additive,
    /// delta-detected, idempotent, no seed reset; RC R·7).
    /// </summary>
    public IReadOnlyList<string> ImageIds { get; set; } = [];

    // ATT U3 file-attachment ADD (ADR 0034, ADR 0004 §B.1 additive — the 6th
    // additive Post field after M3b's Status, ADR 0013's GroupId, ADR 0018's
    // LanguageCode, ADR 0024's DeletedAt, RC's ImageIds):
    /// <summary>
    /// The attachment file ids referenced by <see cref="Body"/> — the
    /// <c>MediaObject</c> ids appearing as <c>/attachment/{id}</c> links in the
    /// rendered body (C-ATT·1/2). Populated server-side by the owning write
    /// lane (ATT U4); the serving route's reverse lookup
    /// (<see cref="PostService.FindPostByAttachmentIdAsync"/>) reads this
    /// (C-ATT·4). The 6th additive field after <see cref="Status"/> (M3b),
    /// <see cref="GroupId"/> (ADR 0013), <see cref="LanguageCode"/> (ADR 0018),
    /// <see cref="DeletedAt"/> (ADR 0024), <see cref="ImageIds"/> (RC) —
    /// **separate from** <see cref="ImageIds"/> (C-ATT·5; a post's images stay
    /// in <see cref="ImageIds"/>, its files in <see cref="AttachmentIds"/>)
    /// (ADR 0004 §B.1 — additive, delta-detected, idempotent, no seed reset;
    /// ADR 0034, C-ATT·5).
    /// </summary>
    public IReadOnlyList<string> AttachmentIds { get; set; } = [];

    // ADR 0037 draft ADD (ADR 0004 §B.1 additive — the 7th additive Post field
    // after M3b's Status, ADR 0013's GroupId, ADR 0018's LanguageCode,
    // ADR 0024's DeletedAt, RC's ImageIds, ATT's AttachmentIds):
    /// <summary>
    /// True while this post is an unsaved draft (ADR 0037). A draft is
    /// **invisible to everyone except its author** — the authorization
    /// algorithm is never consulted for a draft (no owner branch, no audience,
    /// no membership, no break-glass); visibility is a pure
    /// <c>Post.AuthorId == actorId</c> check in the service layer.
    /// Feeds exclude drafts unconditionally (the author's own feed does not
    /// surface drafts either — the author reaches drafts via the detail lane
    /// or a future "My drafts" list). Set to <c>false</c> by
    /// <see cref="PostService.PublishPostAsync"/> when the author is ready to
    /// share. Default <c>false</c> — existing posts are never drafts.
    /// Orthogonal to <see cref="Status"/> (the moderator surface) and
    /// <see cref="DeletedAt"/> (the author soft-delete): a post can be a
    /// draft AND active, or a draft AND hidden by a moderator, simultaneously.
    /// </summary>
    public bool IsDraft { get; set; } = false;

    // TG tag ADD (ADR 0044, ADR 0004 §B.1 additive — the 8th additive Post
    // field after M3b's Status, ADR 0013's GroupId, ADR 0018's LanguageCode,
    // ADR 0024's DeletedAt, RC's ImageIds, ATT's AttachmentIds, ADR 0037's
    // IsDraft):
    /// <summary>
    /// The <see cref="Kumunita.Core.Tags.Tag"/> ids attached to this post
    /// (the <c>TG</c> lane, ADR 0044 D2) — the multi-valued, **non-access**
    /// subject labels (a label, never a gate, C-TG·1; D5 — a tag grants no
    /// membership, access, or moderation scope). One field covers **both** the
    /// community and the group post lane (ADR 0013 — they share this
    /// <see cref="Post"/> doc). Populated server-side by the tag write lane
    /// (U5's <c>TagService</c>); the serving browse / autocomplete read seams
    /// read this computed **over** the content the actor may already read
    /// (C-TG·2). Default <see cref="IReadOnlyList{T}">empty</see> — existing
    /// posts read back with no tags (the ADR 0004 §B.1 additive no-reseed
    /// pin, F11).
    /// </summary>
    public IReadOnlyList<string> TagIds { get; set; } = [];
}
