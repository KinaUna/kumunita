using Microsoft.AspNetCore.Http;

namespace Kumunita.Web.Security;

/// <summary>
/// The <c>kumunita.theme</c> preference cookie (ADR 0133). The value is one of
/// the three theme modes: <see cref="Auto"/> (default — follow the OS
/// <c>prefers-color-scheme</c>), <see cref="Light"/>, or <see cref="Dark"/>
/// (the Forest dark theme).
/// <para>
/// Like <see cref="NavLayoutCookie"/> and <see cref="LocaleCookie"/> it is a
/// **preference, never a claim** (thin-token rule — ADR 0001-B): read here in
/// the Web layer and passed to the layout as a plain string; it is never part
/// of the identity or an authorization decision.
/// </para>
/// <para>
/// <b>Default <see cref="Auto"/>:</b> when the cookie is absent, blank, or an
/// unknown value, <see cref="Read"/> returns <see cref="Auto"/>. Auto is
/// resolved **in CSS** (<c>site.css</c> <c>@media (prefers-color-scheme:
/// dark)</c>), so the dark theme appears with no JS and no first-paint flash,
/// and it live-follows the OS when the OS preference flips. An explicit
/// <see cref="Light"/>/<see cref="Dark"/> pick is the resident's override: it
/// wins over the OS on every device and browser (cookie, so it survives across
/// browsers, unlike a JS <c>localStorage</c> choice).
/// </para>
/// </summary>
public static class ThemeCookie
{
    public const string Name = "kumunita.theme";
    public const int MaxAgeDays = 365;

    /// <summary>The default mode — follow the OS <c>prefers-color-scheme</c>
    /// (resolved in CSS, so no flash and no JS).</summary>
    public const string Auto = "auto";

    /// <summary>Explicit light (the site's default light theme).</summary>
    public const string Light = "light";

    /// <summary>Explicit dark (the Forest dark theme).</summary>
    public const string Dark = "dark";

    /// <summary>All three modes a resident may choose. Order here is the order
    /// the account-menu picker lists them (Auto first — the default).</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Auto, Light, Dark };

    /// <summary>Reads the preferred mode from the request, defaulting to
    /// <see cref="Auto"/> when absent, blank, or unknown.</summary>
    public static string Read(HttpRequest request)
    {
        var value = request.Cookies.TryGetValue(Name, out var cookie) ? cookie : null;
        return Normalize(value);
    }

    /// <summary>Maps an arbitrary incoming value (from the picker form) to a
    /// known mode, defaulting to <see cref="Auto"/> for anything unknown or
    /// blank. Used by the endpoint so a tampered <c>mode</c> field can never
    /// produce a fourth mode.</summary>
    public static string Normalize(string? value)
    {
        return string.Equals(value, Light, StringComparison.OrdinalIgnoreCase)
            ? Light
            : string.Equals(value, Dark, StringComparison.OrdinalIgnoreCase)
                ? Dark
                : Auto;
    }

    /// <summary>Writes the preference (the account-menu picker save).
    /// <c>HttpOnly</c>, <c>SameSite=Lax</c>, <see cref="MaxAgeDays"/> — the same
    /// shape as <see cref="NavLayoutCookie"/>.</summary>
    public static void Write(HttpResponse response, string mode)
    {
        response.Cookies.Append(Name, Normalize(mode), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(MaxAgeDays),
        });
    }

    /// <summary>Clears the preference (reverts to the <see cref="Auto"/> default).</summary>
    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
        });
    }
}
