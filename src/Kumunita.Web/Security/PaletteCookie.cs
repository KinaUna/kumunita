using Microsoft.AspNetCore.Http;

namespace Kumunita.Web.Security;

/// <summary>
/// The <c>kumunita.palette</c> preference cookie (ADR 0175). A palette is the
/// platform's colour identity — <see cref="Forest"/> (green, the default) or
/// <see cref="Ember"/> (warm charcoal/amber) — and is independent of the
/// light/dark/auto mode in <see cref="ThemeCookie"/>.
/// <para>
/// Resolution order: the resident's cookie (member override) → the platform
/// default the admin chose → <see cref="Forest"/>. Because cookies are per
/// host, a resident on several platforms keeps a separate choice for each,
/// and the admin default is what lets them tell the platforms apart.
/// </para>
/// <para>
/// Like <see cref="ThemeCookie"/> it is a **preference, never a claim**
/// (thin-token rule — ADR 0001-B): never part of identity or authorization.
/// </para>
/// </summary>
public static class PaletteCookie
{
    public const string Name = "kumunita.palette";
    public const int MaxAgeDays = 365;

    /// <summary>The original green identity (dark values warmed by ADR 0175).</summary>
    public const string Forest = "forest";

    /// <summary>Warm charcoal-brown surfaces with an amber primary action.</summary>
    public const string Ember = "ember";

    /// <summary>Palettes a resident or admin may choose; order = picker order.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Forest, Ember };

    /// <summary>
    /// Reads the member's override, falling back to <paramref name="platformDefault"/>
    /// (the admin's choice) and then <see cref="Forest"/>.
    /// </summary>
    public static string Read(HttpRequest request, string? platformDefault = null)
    {
        var cookie = request.Cookies.TryGetValue(Name, out var value) ? value : null;
        if (IsKnown(cookie)) return Normalize(cookie);
        return Normalize(platformDefault);
    }

    /// <summary>The member's override alone — <c>null</c> when the cookie is absent or
    /// unknown (i.e. the member is following the platform default). The account-menu
    /// picker uses this to mark "Platform default" vs a chosen palette.</summary>
    public static string? ReadOverride(HttpRequest request)
    {
        var cookie = request.Cookies.TryGetValue(Name, out var value) ? value : null;
        return IsKnown(cookie) ? Normalize(cookie) : null;
    }

    /// <summary>Maps any incoming value to a known palette (unknown → Forest).</summary>
    public static string Normalize(string? value) =>
        string.Equals(value, Ember, StringComparison.OrdinalIgnoreCase) ? Ember : Forest;

    public static bool IsKnown(string? value) =>
        string.Equals(value, Forest, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Ember, StringComparison.OrdinalIgnoreCase);

    /// <summary>Writes the member override (same cookie shape as <see cref="ThemeCookie"/>).</summary>
    public static void Write(HttpResponse response, string palette)
    {
        response.Cookies.Append(Name, Normalize(palette), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(MaxAgeDays),
        });
    }

    /// <summary>Clears the member override (revert to the platform default).</summary>
    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
        });
    }
}
