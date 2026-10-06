using Kumunita.Core.Query;
using Kumunita.Core.UserInfo;

namespace Kumunita.Web;

/// <summary>
/// The one shared helper every paged feed controller uses to resolve the
/// resident's <b>items-per-page</b> preference
/// (<see cref="UserInfo.Profile.PageSize"/>) to a concrete
/// <see cref="PageSizer.Resolve"/> value to pass to the paged Core seam. One
/// method so no controller duplicates the profile read or the
/// <see cref="PageSizer"/> default/clamp. A no-actor (anonymous / unauth)
/// read — or a missing profile — resolves to the platform default
/// (<see cref="PageSizer.Default"/> = 10); a stored value is clamped to
/// <see cref="PageSizer.Min"/>…<see cref="PageSizer.Max"/>.
/// </summary>
public static class FeedPaging
{
    /// <summary>
    /// Resolve the actor's page size: a null
    /// <paramref name="userInfo"/> (an optional-injection controller whose
    /// test-construction site supplied no seam) or a <c>null</c>/empty
    /// <paramref name="actorId"/> ⇒ the platform default (10); otherwise
    /// <see cref="IUserInfoService.GetProfileAsync"/> →
    /// <see cref="PageSizer.Resolve"/> (null ⇒ 10, clamped 5..100). A profile
    /// read is a *display* lookup, never an access decision (the feed's own
    /// <c>CanSeeAsync</c> already ran), so no audit row is emitted here.
    /// </summary>
    public static async Task<int> PageSizeAsync(IUserInfoService? userInfo, string? actorId)
    {
        if (userInfo is null || string.IsNullOrEmpty(actorId))
            return PageSizer.Default;
        var profile = await userInfo.GetProfileAsync(actorId);
        return PageSizer.Resolve(profile?.PageSize);
    }
}
