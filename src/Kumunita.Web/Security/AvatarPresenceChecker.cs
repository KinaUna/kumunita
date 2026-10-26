using Kumunita.Core.Media;
using Kumunita.Core.UserInfo;

namespace Kumunita.Web.Security;

/// <summary>
/// Answers "does this resident actually have an avatar we can serve?" (a
/// <b>read, never a decision</b> — the avatar serving lane is "all platform
/// users" by construction, ADR 0057, so no <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>
/// call and no <see cref="Kumunita.Core.Authorization.AccessAudit"/> row are
/// part of this check). The <c>&lt;kw-avatar /&gt;</c> TagHelper uses it to
/// decide, server-side, whether to emit the avatar <c>&lt;img&gt;</c> at all
/// — so a resident with no avatar, or whose stored file no longer exists,
/// renders the monogram directly and the browser never makes a request that
/// will fail closed to a 404.
/// </summary>
public interface IAvatarPresenceChecker
{
    /// <summary>
    /// <c>true</c> when the resident exists, is not blocked, has an
    /// <c>AvatarId</c>, and the stored media object resolves — i.e. exactly
    /// the set of conditions under which
    /// <c>GET /profile/avatar/{subjectId}</c> (the
    /// <see cref="Kumunita.Web.Controllers.ProfileController.Avatar"/> lane)
    /// returns the image bytes rather than a 404. Every structural failure
    /// (unknown profile, blocked profile, no avatar set, missing media doc)
    /// folds to <c>false</c>.
    /// </summary>
    Task<bool> HasAvatarAsync(string subjectId);
}

/// <inheritdoc/>
/// <remarks>
/// Registered as a <b>scoped</b> service: one instance per request, so the
/// per-subject cache is request-scoped by construction and a page that lists
/// many rows for the same resident (a feed, a comment list, a board) does not
/// re-run the profile + media read for each row.
/// </remarks>
public sealed class AvatarPresenceChecker(
    IUserInfoService userInfo,
    IMediaStore media) : IAvatarPresenceChecker
{
    // Request-scoped (the service is scoped): one instance per request, so a
    // plain field doubles as a per-request cache — no static / HttpContext
    // juggling needed, and the cache can never outlive the request.
    private readonly Dictionary<string, bool> _cache = new(StringComparer.Ordinal);

    public async Task<bool> HasAvatarAsync(string subjectId)
    {
        if (string.IsNullOrEmpty(subjectId)) return false;
        if (_cache.TryGetValue(subjectId, out var cached)) return cached;

        var profile = await userInfo.GetProfileAsync(subjectId).ConfigureAwait(false);

        // The profile must exist, be unblocked, and name an avatar. (The
        // serving lane — ProfileController.Avatar — folds every one of these
        // to a 404, so the "no avatar set" / blocked / unknown cases all
        // resolve here without a media read.)
        var avatarId = profile is { Blocked: false } ? profile.AvatarId : null;
        if (string.IsNullOrEmpty(avatarId))
        {
            _cache[subjectId] = false;
            return false;
        }

        // The media doc must also resolve (C-MED·7 — the serving lane 404s on
        // a missing doc). This is the rare "avatar id set but the file was
        // removed" case.
        var present = await media.GetAsync(avatarId).ConfigureAwait(false) is not null;
        _cache[subjectId] = present;
        return present;
    }
}
