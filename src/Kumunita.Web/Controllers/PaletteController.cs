using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The palette preference endpoint (ADR 0175). Same shape as
/// <see cref="ThemeController"/>: a <c>palette</c> form value →
/// <see cref="PaletteCookie.Write"/> (normalized), then a local-only redirect.
/// An empty value (the "Platform default" choice) clears the member override.
/// </summary>
public sealed class PaletteController : Controller
{
    [HttpPost("/palette")]
    [ValidateAntiForgeryToken]
    public IActionResult Save(string? palette, string? returnUrl = null)
    {
        if (PaletteCookie.IsKnown(palette))
            PaletteCookie.Write(Response, palette!);
        else
            PaletteCookie.Clear(Response);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return Redirect("/");
    }

    // TODO (ADR 0175 D3): the admin's platform default — a POST /admin/site/palette
    // action in AdminSiteController persisting the choice through the site-content
    // customization lane (ADR 0150), then read by _Layout via a small service.
}
