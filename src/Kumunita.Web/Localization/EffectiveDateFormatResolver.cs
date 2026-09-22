using System;
using System.Threading.Tasks;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;

namespace Kumunita.Web.Localization;

/// <summary>
/// Resolves the **effective date-time format** the current request renders
/// in (ADR 0020 — the exact companion to
/// <see cref="EffectiveTimezoneResolver"/>: same "override → platform
/// default → floor" shape, same per-request cache, same HTTP boundary).
/// <para>
/// <b>Resolution order:</b> signed-in actor's <see cref="Profile.DateFormat"/>
/// (their personal override) → the instance default (<see
/// cref="LocaleSettings.DefaultDateFormat"/> via <see
/// cref="ILocalizationService.GetDefaultDateFormatAsync"/>) → the <see
/// cref="DateFormat.FloorFormat"/> floor (never null, never blank, never a
/// throw). Anonymous requests resolve to the instance default (no override on
/// an unsigned principal, so the fallback is the platform setting itself).
/// </para>
/// <para>
/// <b>Per-request cache.</b> A page renders many timestamps through the
/// <c>kw-dt</c> TagHelper; resolving each one independently would mean one
/// <c>GetProfileAsync</c> + one <c>GetDefaultDateFormatAsync</c> round-trip
/// per stamp. This scoped service resolves **once per request** (the first
/// <see cref="GetAsync"/> call) and returns the cached format string
/// thereafter — the same "one read per request, served to all consumers"
/// shape <see cref="EffectiveTimezoneResolver"/> uses. The cache is on the
/// service instance (scoped), not a static, so concurrent requests do not
/// interfere.
/// </para>
/// <para>
/// <b>Graceful floor.</b> A stored value that is not a usable .NET custom
/// datetime format string (a blank, or a string .NET cannot apply — e.g. a
/// hand-edited row) is treated as "no such format, fall through" (the same
/// "degrade to the next tier, never a throw" rule
/// <see cref="EffectiveTimezoneResolver"/> applies to an unknown IANA id).
/// </para>
/// </summary>
public sealed class EffectiveDateFormatResolver
{
    private readonly IUserInfoService _userInfo;
    private readonly ILocalizationService _localization;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Per-request cache (the service is registered scoped — one instance per
    // request). Null until the first GetAsync call; thereafter the resolved
    // format string is returned for every subsequent call on this instance.
    private string? _cached;
    private bool _resolved;

    /// <param name="userInfo">The profile read seam (the actor's
    /// <c>Profile.DateFormat</c> override).</param>
    /// <param name="localization">The platform-default read seam
    /// (<c>LocaleSettings.DefaultDateFormat</c> via
    /// <see cref="ILocalizationService.GetDefaultDateFormatAsync"/>, the
    /// floor lives in the Core).</param>
    /// <param name="httpContextAccessor">Reaches the request's signed-in
    /// principal (registered via <c>AddHttpContextAccessor</c>, the same
    /// seam <c>ClaimsSource</c>, <c>kw-l</c>, and
    /// <see cref="EffectiveTimezoneResolver"/> use).</param>
    public EffectiveDateFormatResolver(
        IUserInfoService userInfo,
        ILocalizationService localization,
        IHttpContextAccessor httpContextAccessor)
    {
        _userInfo = userInfo;
        _localization = localization;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Resolves (once per request) and returns the effective .NET custom
    /// datetime format string for the current request. Never null, never
    /// blank, never a throw — a stored value that .NET cannot apply falls
    /// through to the next tier (the Core's floor covers the rest).
    /// </summary>
    public async Task<string> GetAsync()
    {
        if (_resolved)
            return _cached!;

        var principal = _httpContextAccessor.HttpContext?.User;
        var subject = principal is not null ? KumunitaPrincipal.SubjectId(principal) : null;
        var overrideFmt = subject is null
            ? null
            : (await _userInfo.GetProfileAsync(subject))?.DateFormat;

        _cached = FirstUsable(
            overrideFmt,
            await _localization.GetDefaultDateFormatAsync(),
            DateFormat.FloorFormat);

        _resolved = true;
        return _cached;
    }

    // ── Internal floor (the public surface of the resolver) ────────────

    // Walk the candidate tiers (override → default → floor) and return the
    // first one that is a usable .NET custom datetime format string; the
    // floor itself is always usable, so this never returns null.
    private static string FirstUsable(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (DateFormat.IsValid(c))
                return c!;
        }

        // Unreachable (DateFormat.IsValid(FloorFormat) is true) — but keep a
        // hard floor so a future preset regression cannot surface a blank.
        return DateFormat.FloorFormat;
    }
}
