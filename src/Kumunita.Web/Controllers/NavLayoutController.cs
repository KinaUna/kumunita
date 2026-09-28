using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The nav-variant preference endpoint (ADR 0111). A signed-in resident chooses
/// between the two navigation layouts — variant B, the compact top row
/// (<see cref="NavLayoutCookie.Row"/>), and variant C, the icon rail
/// (<see cref="NavLayoutCookie.Rail"/>) — from the account menu.
/// <para>
/// The write is the same frozen <see cref="NavLayoutCookie"/> cookie: a
/// **preference, never a claim** (thin-token rule — ADR 0001-B), never part of
/// the authorization decision. That makes it harmless, so it is a plain public
/// route (<c>/nav-layout</c>) like <see cref="PublicLocaleController"/> — the
/// form is <c>[ValidateAntiForgeryToken]</c>-protected, and the value is
/// normalized so a tampered <c>variant</c> field can never yield a third layout.
/// The change takes effect on the next request (preference → data, not config);
/// a <c>returnUrl</c> sends the resident back to the page they were on.
/// </para>
/// </summary>
public sealed class NavLayoutController : Controller
{
    /// <summary>
    /// <c>POST /nav-layout</c> — a <c>variant</c> form value →
    /// <see cref="NavLayoutCookie.Write"/> (normalized to B or C), then redirect
    /// back to <c>returnUrl</c> when present (a local URL only — no open
    /// redirect), else the home page. The new layout renders on that return
    /// trip, which doubles as the confirmation: the resident sees it take
    /// effect immediately, so no flash message is needed.
    /// </summary>
    [HttpPost("/nav-layout")]
    [ValidateAntiForgeryToken]
    public IActionResult Save(string? variant, string? returnUrl = null)
    {
        NavLayoutCookie.Write(Response, NavLayoutCookie.Normalize(variant));

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return Redirect("/");
    }
}
