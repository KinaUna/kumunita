using Microsoft.AspNetCore.Http;

namespace Kumunita.Web.Security;

/// <summary>
/// The <c>kumunita.nav</c> preference cookie (ADR 0111). The value is one of the
/// nav-variant ids (<see cref="Row"/> or <see cref="Rail"/>). Like
/// <see cref="LocaleCookie"/> it is a **preference, never a claim** (thin-token
/// rule — ADR 0001-B): read here in the Web layer, passed to the layout as a plain
/// string; it is never part of the identity or an authorization decision.
/// <para>
/// <b>Default B (the compact top row):</b> when the cookie is absent, blank, or an
/// unknown value, <see cref="Read"/> returns <see cref="Row"/>. So a first-time
/// resident, a fresh browser, and an operator who deletes the cookie all land on
/// variant B without any write. Variant C (the icon rail) is the explicit opt-in.
/// </para>
/// </summary>
public static class NavLayoutCookie
{
    public const string Name = "kumunita.nav";
    public const int MaxAgeDays = 365;

    /// <summary>The default variant — the compact top row (mockup "B").</summary>
    public const string Row = "b";

    /// <summary>The explicit opt-in variant — the icon rail + slim top bar
    /// (mockup "C").</summary>
    public const string Rail = "c";

    /// <summary>Both variants a resident may choose. Order here is the order the
    /// account-menu picker lists them.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Row, Rail };

    /// <summary>Reads the preferred variant from the request, defaulting to
    /// <see cref="Row"/> (variant B) when absent, blank, or unknown.</summary>
    public static string Read(HttpRequest request)
    {
        var value = request.Cookies.TryGetValue(Name, out var cookie) ? cookie : null;
        return Normalize(value);
    }

    /// <summary>Maps an arbitrary incoming value (from the picker form) to a known
    /// variant, defaulting to <see cref="Row"/> (variant B) for anything unknown
    /// or blank. Used by the endpoint so a tampered <c>variant</c> field can never
    /// produce a third layout.</summary>
    public static string Normalize(string? value)
    {
        return string.Equals(value, Rail, StringComparison.OrdinalIgnoreCase)
            ? Rail
            : Row;
    }

    /// <summary>Writes the preference (the account-menu picker save). <c>HttpOnly</c>,
    /// <c>SameSite=Lax</c>, <see cref="MaxAgeDays"/> — the same shape as
    /// <see cref="LocaleCookie"/>.</summary>
    public static void Write(HttpResponse response, string variant)
    {
        response.Cookies.Append(Name, Normalize(variant), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(MaxAgeDays),
        });
    }

    /// <summary>Clears the preference (reverts to the variant B default).</summary>
    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
        });
    }
}
