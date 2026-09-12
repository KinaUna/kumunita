using Microsoft.AspNetCore.Http;

namespace Kumunita.Web.Security;

/// <summary>
/// The <c>kumunita.locale</c> preference cookie (ADR 0005 B; M·5). The value is a
/// BCP-47 code (a <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/>).
/// **Not** a claim (thin-token rule — ADR 0001-B): read here, passed to
/// <see cref="Kumunita.Core.Localization.ITranslationProvider"/> as a plain string;
/// it is never part of the identity or the authorization decision. This is the
/// **only** place the cookie is read or written (M·8 — Core stays HTTP-free).
/// </summary>
public static class LocaleCookie
{
    public const string Name = "kumunita.locale";
    public const int MaxAgeDays = 365;

    /// <summary>Reads the preferred code from the request, or <c>null</c> (no
    /// preference — resolve to the instance default, M·1).</summary>
    public static string? Read(HttpRequest request)
    {
        // A blank value is the same as no preference (M·1): fall through to null so
        // the provider resolves the instance default.
        var value = request.Cookies.TryGetValue(Name, out var cookie) ? cookie : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Writes the preference (the settings-page save, M7 FACES). Value = a
    /// BCP-47 code; <c>HttpOnly</c>, <c>SameSite=Lax</c>, <see cref="MaxAgeDays"/>.</summary>
    public static void Write(HttpResponse response, string languageCode)
    {
        response.Cookies.Append(Name, languageCode, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromDays(MaxAgeDays),
        });
    }

    /// <summary>Clears the preference ("reset to default" on the settings page).</summary>
    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
        });
    }
}
