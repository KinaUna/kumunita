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
}
