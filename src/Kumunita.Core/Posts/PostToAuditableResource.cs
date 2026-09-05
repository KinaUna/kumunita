using Kumunita.Core.Authorization;

namespace Kumunita.Core.Posts;

/// <summary>
/// Adapter (M3, U5 — pinned in the design doc §2.2): presents a
/// <see cref="Post"/> to the frozen <see cref="IAuthorizationService"/> as an
/// <see cref="IAuditableResource"/>. Mapping (C-M3·3 / C3 pin):
/// <para>
/// <c>Id</c> = <see cref="Post.Id"/>; <c>Name</c> = <see cref="Post.Title"/>
/// or a 60-char-truncated <see cref="Post.Body"/> (the audit row's human-facing
/// label); <c>OwnerId</c> = <see cref="Post.AuthorId"/> — the owner branch of
/// the decision algorithm is the *only* lane that lets the author see their own
/// empty-audience draft (invariant C1); <c>Audience</c> = <see cref="Post.Audience"/>
/// (non-null by construction — C1 pins the non-null requirement; the audience
/// is written **verbatim** per ADR 0001-B, so the adapter projects it as-is and
/// never mutates it); <c>ComponentId</c> = <see cref="Post.ComponentId"/> —
/// C-M3·2: a feed organizer / moderation scope, *never* an access boundary, so
/// projecting it here is safe and carries no decision weight; <c>TargetKind</c>
/// = <c>"post"</c> (C-M3·3: the aggregate-feed row's
/// <see cref="AccessAudit.TargetKind"/> discriminator).
/// </para>
/// <para>
/// The adapter does not *own* the <see cref="Post"/>: a single instance is
/// safe to pass into either <see cref="IAuthorizationService"/> overload
/// (<c>CanAsync</c> detail, <c>CanSeeAsync</c> feed) — each call is a
/// value-level projection, not a shared-mutable-state hazard. Replies get
/// **no** adapter (C-M3·1): a <c>PostReply</c>'s visibility IS the parent
/// post's single <c>Read</c> decision — no second <c>CanSeeAsync</c> call, no
/// reply of its own produces an audit row. <c>sealed</c> keeps the surface
/// closed (ADR 0006-D's single-decision-path is what matters, not
/// subclassability).
/// </para>
/// </summary>
public sealed class PostToAuditableResource : IAuditableResource
{
    /// <summary>
    /// Create an adapter for <paramref name="post"/>.
    /// </summary>
    public PostToAuditableResource(Post post) => Post = post;

    /// <summary>The post this adapter presents. The adapter does not own it.</summary>
    public Post Post { get; }

    /// <summary>Resource id = the post's document identity.</summary>
    public string Id => Post.Id;

    /// <summary>
    /// Display name for the audit row — the title, or the body truncated to 60
    /// chars (57 + "...") when there is no title (U5 plan pin).
    /// </summary>
    public string Name => Post.Title ?? (Post.Body.Length < 60 ? Post.Body : Post.Body[..57] + "...");

    /// <summary>Absolute owner = the author (owner branch of the §4.4 decision algorithm; C1's owner-branch exception).</summary>
    public string? OwnerId => Post.AuthorId;

    /// <summary>
    /// The post's audience, projected verbatim (ADR 0001-B — the adapter never
    /// mutates it; C1 pins non-null by construction).
    /// </summary>
    public Audience? Audience => Post.Audience;

    /// <summary>
    /// Component scope — a feed organizer / moderation scope (C-M3·2), never
    /// an access boundary; carrying it here gives the moderator-scoped
    /// surfaces (M3b) their scoping key without M3 gaining a moderator read
    /// branch.
    /// </summary>
    public string? ComponentId => Post.ComponentId;

    /// <summary>
    /// Resource target kind — <c>"post"</c> (C-M3·3: the aggregate-feed
    /// <see cref="AccessAudit.TargetKind"/> discriminator for this line of
    /// decisions).
    /// </summary>
    public string TargetKind => "post";
}
