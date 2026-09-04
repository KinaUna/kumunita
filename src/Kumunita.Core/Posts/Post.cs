namespace Kumunita.Core.Posts;

/// <summary>
/// A post (M3). <see cref="Audience"/> is **non-null** (invariant C1 — empty audience
/// denies; the author's bootstrap default is an *empty* audience, so the owner branch
/// is the *only* lane that lets the author see their own draft).
/// <see cref="ComponentId"/> is a **feed organizer**, never an access boundary (C-M3·2).
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
    // No Status — the hidden/removed surface is M3b (the M3b deferral close,
    // "Out of scope — M3b deferral"): M3's post has no Status column.
}
