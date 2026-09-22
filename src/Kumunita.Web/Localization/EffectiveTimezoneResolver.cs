using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;

namespace Kumunita.Web.Localization;

/// <summary>
/// Resolves the **effective time zone** the current request renders in (ADR 0019
/// — "the platform default, overridden per account" mirrors the locale feature's
/// "instance default, overridden per resident" shape exactly):
/// <para>
/// <b>Resolution order:</b> signed-in actor's <see cref="Profile.TimeZone"/>
/// (their personal override) → the instance default
/// (<see cref="LocaleSettings.DefaultTimezone"/> via
/// <see cref="ILocalizationService.GetDefaultTimezoneAsync"/>) → the <c>UTC</c>
/// floor (never null, never empty, never a throw). Anonymous requests resolve to
/// the instance default (no override on an unsigned principal, so the fallback
/// is the platform setting itself).
/// </para>
/// <para>
/// <b>Per-request cache.</b> A page renders many timestamps through the
/// <c>kw-dt</c> TagHelper; resolving each one independently would mean one
/// <c>GetProfileAsync</c> + one <c>GetDefaultTimezoneAsync</c> round-trip per
/// stamp. This scoped service resolves **once per request** (the first
/// <see cref="GetAsync"/> call) and returns the cached <see cref="TimeZoneInfo"/>
/// thereafter — the same "one read per request, served to all consumers" shape
/// the <c>ClaimsSource</c> seam uses. The cache is on the service instance
/// (scoped), not a static, so concurrent requests do not interfere.
/// </para>
/// <para>
/// <b>HTTP boundary.</b> The actor's subject id is read from the request
/// principal via <see cref="KumunitaPrincipal.SubjectId"/> (the thin-token
/// idiom — the same claim <c>ProfileController</c> uses); the Core seams
/// (<see cref="IUserInfoService.GetProfileAsync"/>,
/// <see cref="ILocalizationService.GetDefaultTimezoneAsync"/>) stay
/// HTTP-free (ADR 0006-D). A <c>null</c> principal (signed-out) yields
/// <c>null</c> for the override → the instance default is used (the
/// <c>"preference if present"</c> rule).
/// </para>
/// </summary>
public sealed class EffectiveTimezoneResolver
{
    private readonly IUserInfoService _userInfo;
    private readonly ILocalizationService _localization;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Per-request cache (the service is registered scoped — one instance per
    // request). Null until the first GetAsync call; thereafter the resolved
    // TimeZoneInfo is returned for every subsequent call on this instance.
    private TimeZoneInfo? _cached;
    private bool _resolved;

    /// <param name="userInfo">The profile read seam (the actor's
    /// <c>Profile.TimeZone</c> override).</param>
    /// <param name="localization">The platform-default read seam
    /// (<c>LocaleSettings.DefaultTimezone</c> via
    /// <see cref="ILocalizationService.GetDefaultTimezoneAsync"/>, the
    /// <c>UTC</c> floor lives in the Core).</param>
    /// <param name="httpContextAccessor">Reaches the request's signed-in
    /// principal (registered via <c>AddHttpContextAccessor</c>, the same
    /// seam <c>ClaimsSource</c> and the <c>kw-l</c> TagHelper use).</param>
    public EffectiveTimezoneResolver(
        IUserInfoService userInfo,
        ILocalizationService localization,
        IHttpContextAccessor httpContextAccessor)
    {
        _userInfo = userInfo;
        _localization = localization;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Resolves (once per request) and returns the effective
    /// <see cref="TimeZoneInfo"/> for the current request. Never null, never
    /// a throw — a stored id that does not exist on the OS falls back to the
    /// instance default (the Core's <c>UTC</c> floor covers the rest).
    /// </summary>
    public async Task<TimeZoneInfo> GetAsync()
    {
        if (_resolved)
            return _cached!;

        var principal = _httpContextAccessor.HttpContext?.User;
        var subject = principal is not null ? KumunitaPrincipal.SubjectId(principal) : null;
        var tzId = subject is null
            ? null
            : (await _userInfo.GetProfileAsync(subject))?.TimeZone;

        _cached = TryConvert(tzId)
            ?? TryConvert(await _localization.GetDefaultTimezoneAsync())
            ?? FallbackUtc();

        _resolved = true;
        return _cached;
    }

    // ── Internal conversion + floor (public surface of the resolver) ─────

    /// <summary>
    /// Convert an IANA id to a <see cref="TimeZoneInfo"/>, returning <c>null</c>
    /// when the id is blank or not present on the OS (e.g. an admin-typed id
    /// the OS has no data for — the <c>TimeZoneInfo</c> lookup throws
    /// <c>TimeZoneNotFoundException</c>, which we treat as "no such zone,
    /// fall through"). Never a throw.
    /// </summary>
    public static TimeZoneInfo? TryConvert(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        try
        {
            return System.TimeZoneInfo.FindSystemTimeZoneById(id!);
        }
        catch
        {
            // TimeZoneNotFoundException / ArgumentException — not a zone this OS
            // knows about; the caller falls through to the next candidate.
            return null;
        }
    }

    private static TimeZoneInfo FallbackUtc() =>
        System.TimeZoneInfo.FindSystemTimeZoneById("UTC");

    /// <summary>
    /// The IANA time zone ids available on this OS, as (id, display-name)
    /// pairs sorted by **current UTC offset** (ascending, so the most
    /// western / negative-offset zones come first) then by id — the
    /// <c>&lt;select&gt;</c> option source for both the user-override page
    /// (<c>/settings/timezone</c>) and the admin platform-default surface
    /// (<c>/admin/timezone</c>). Excludes the OS-internal pseudo-zones (their
    /// ids start with <c>*</c>, e.g. <c>*Dynamic</c>). A shared source so the
    /// two pickers and the resolver's own validation all see the same
    /// universe. The offset is taken "now" so groups of zones share the same
    /// order regardless of DST (their ids break the tie deterministically).
    /// </summary>
    public static List<(string Id, string DisplayName)> Zones()
    {
        var now = DateTime.UtcNow.Date;
        return System.TimeZoneInfo.GetSystemTimeZones()
            .Where(z => !z.Id.StartsWith("*"))
            .OrderBy(z => z.GetUtcOffset(now).TotalHours)
            .ThenBy(z => z.Id, StringComparer.Ordinal)
            .Select(z => (z.Id, z.DisplayName))
            .ToList();
    }
}
