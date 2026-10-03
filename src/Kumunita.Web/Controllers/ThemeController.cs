using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The theme preference endpoint (ADR 0133). A resident chooses between the
/// three theme modes — <see cref="ThemeCookie.Auto"/> (follow the OS
/// <c>prefers-color-scheme</c>, the default), <see cref="ThemeCookie.Light"/>,
/// and <see cref="ThemeCookie.Dark"/> (the Forest dark theme) — from the
/// account menu.
/// <para>
/// The write is the same shape as <see cref="NavLayoutController"/>: a
/// <c>mode</c> form value → <see cref="ThemeCookie.Write"/> (normalized to
/// Auto/Light/Dark), then a redirect back to <c>returnUrl</c> when present
/// (a local URL only — no open redirect), else the home page. The new theme
/// renders on that return trip, which doubles as the confirmation — the
/// resident sees it take effect immediately, so no flash message is needed.
/// </para>
/// <para>
/// The write is the same frozen <see cref="ThemeCookie"/> cookie: a
/// **preference, never a claim** (thin-token rule — ADR 0001-B), never part of
/// the authorization decision. That makes it harmless, so it is a plain public
/// route (<c>/theme</c>) like <see cref="NavLayoutController"/> — the form is
/// <c>[ValidateAntiForgeryToken]</c>-protected, and the value is normalized so a
/// tampered <c>mode</c> field can never yield a fourth mode.
/// </para>
/// </summary>
public sealed class ThemeController : Controller
{
    /// <summary>
    /// <c>POST /theme</c> — a <c>mode</c> form value →
    /// <see cref="ThemeCookie.Write"/> (normalized to Auto/Light/Dark), then
    /// redirect back to <c>returnUrl</c> when present (a local URL only — no
    /// open redirect), else the home page. The new theme renders on that return
    /// trip, which doubles as the confirmation.
    /// </summary>
    [HttpPost("/theme")]
    [ValidateAntiForgeryToken]
    public IActionResult Save(string? mode, string? returnUrl = null)
    {
        ThemeCookie.Write(Response, ThemeCookie.Normalize(mode));

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return Redirect("/");
    }
}
