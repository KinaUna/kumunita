using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.Translations;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Posts;

/// <summary>
/// The media-finder lane — the four Find*By*IdAsync lookups
/// that resolve a post or reply by its attached media or attachment
/// id (ADR 0011 media-and-file-storage). A partial class of
/// <see cref="PostService"/> extracted so the main file stays under the
/// 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class PostService
{
    /// <summary>
    /// <b>Reverse-lookup</b> read seam (RC U03, R·5): the first post (by
    /// <c>Created</c> ascending — deterministic) whose
    /// <see cref="Post.ImageIds"/> contains <paramref name="mediaId"/> — the
    /// serving route's owner resolution reads this (RC R·4). **Un-audited**
    /// (RC R·5 — the audit row belongs to the route's
    /// <see cref="IAuthorizationService.CanAsync"/>, not this read); null when
    /// no post references the id (the route 404s). Read-only — no write lane,
    /// no <c>actorId</c> parameter (there is no decision to log here).
    /// </summary>
    public async Task<Post?> FindPostByImageIdAsync(string mediaId)
    {
        if (string.IsNullOrEmpty(mediaId))
            throw new ArgumentException("A media id is required.", nameof(mediaId));

        await using var session = _store.QuerySession();
        return await session
            .Query<Post>()
            .Where(p => p.ImageIds.Contains(mediaId))
            .OrderBy(p => p.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>Reverse-lookup</b> read seam (RC U03, R·5): the first reply (by
    /// <c>Created</c> ascending) whose <see cref="PostReply.ImageIds"/>
    /// contains <paramref name="mediaId"/>. **Un-audited** (RC R·5 — the audit
    /// row belongs to the route's <see cref="IAuthorizationService.CanAsync"/>);
    /// null when no reply references the id (the route 404s). Read-only.
    /// </summary>
    public async Task<PostReply?> FindReplyByImageIdAsync(string mediaId)
    {
        if (string.IsNullOrEmpty(mediaId))
            throw new ArgumentException("A media id is required.", nameof(mediaId));

        await using var session = _store.QuerySession();
        return await session
            .Query<PostReply>()
            .Where(r => r.ImageIds.Contains(mediaId))
            .OrderBy(r => r.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>Reverse-lookup</b> read seam (ATT U3, C-ATT·4): the first post (by
    /// <c>Created</c> ascending — deterministic) whose
    /// <see cref="Post.AttachmentIds"/> contains <paramref name="mediaId"/> —
    /// the attachment serving route's owner resolution reads this (C-ATT·7).
    /// **Un-audited** (the audit row belongs to the route's
    /// <see cref="IAuthorizationService.CanAsync"/>, not this read); null when
    /// no post references the id (the route 404s). Read-only — no write lane,
    /// no <c>actorId</c> parameter (there is no decision to log here).
    /// </summary>
    public async Task<Post?> FindPostByAttachmentIdAsync(string mediaId)
    {
        if (string.IsNullOrEmpty(mediaId))
            throw new ArgumentException("A media id is required.", nameof(mediaId));

        await using var session = _store.QuerySession();
        return await session
            .Query<Post>()
            .Where(p => p.AttachmentIds.Contains(mediaId))
            .OrderBy(p => p.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>Reverse-lookup</b> read seam (ATT U3, C-ATT·4): the first reply (by
    /// <c>Created</c> ascending) whose <see cref="PostReply.AttachmentIds"/>
    /// contains <paramref name="mediaId"/>. **Un-audited** (the audit row
    /// belongs to the route's <see cref="IAuthorizationService.CanAsync"/>);
    /// null when no reply references the id (the route 404s). Read-only.
    /// </summary>
    public async Task<PostReply?> FindReplyByAttachmentIdAsync(string mediaId)
    {
        if (string.IsNullOrEmpty(mediaId))
            throw new ArgumentException("A media id is required.", nameof(mediaId));

        await using var session = _store.QuerySession();
        return await session
            .Query<PostReply>()
            .Where(r => r.AttachmentIds.Contains(mediaId))
            .OrderBy(r => r.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

}
