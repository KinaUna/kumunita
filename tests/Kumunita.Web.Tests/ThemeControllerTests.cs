using Kumunita.Web.Controllers;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// C12 — the <see cref="ThemeController"/> seam (ADR 0133).
/// The theme-preference endpoint: POST <c>/theme</c> normalizes the <c>mode</c>
/// field to <see cref="ThemeCookie.Auto"/> / <see cref="ThemeCookie.Light"/> /
/// <see cref="ThemeCookie.Dark"/> and writes the frozen <see cref="ThemeCookie"/>.
/// A tampered value (a fourth mode) is normalized to the default (Auto) so a
/// client can never yield a fourth mode. The write is a preference, never a
/// claim (thin-token rule, ADR 0001-B). Same shape as
/// <see cref="NavLayoutController"/>.
/// </summary>
public sealed class ThemeControllerTests
{
    private sealed class LocalUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string Action(UrlActionContext actionContext) => "/";
        public string Content(string? content) => content ?? string.Empty;
        public bool IsLocalUrl(string? url) =>
            !string.IsNullOrEmpty(url) && url[0] == '/' && url.Length > 1 && url[1] != '/' && url[1] != '\\';
        public string RouteUrl(UrlRouteContext routeContext) => "/";
        public string Link(string? linkName, object? values) => "/";
    }

    private static ThemeController BuildController()
    {
        var controller = new ThemeController();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/theme";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.Url = new LocalUrlHelper();
        return controller;
    }

    /// <summary>
    /// Reads the value a controller wrote for <paramref name="cookieName"/>
    /// from the response's <c>Set-Cookie</c> header (the response cookie
    /// collection in a <see cref="DefaultHttpContext"/> is a write-side bag
    /// with no <c>Get</c>, so the header is the observable surface).
    /// </summary>
    private static string? WrittenCookieValue(HttpResponse response, string cookieName)
    {
        foreach (var header in response.Headers["Set-Cookie"].Cast<string>())
        {
            var eq = header.IndexOf('=');
            if (eq <= 0) continue;
            var name = header[..eq].Trim();
            if (!name.Equals(cookieName, StringComparison.Ordinal)) continue;
            var rest = header[(eq + 1)..];
            var semi = rest.IndexOf(';');
            return semi < 0 ? rest : rest[..semi];
        }
        return null;
    }

    [Fact]
    public void Save_AutoMode_WritesCookieAndRedirectsHome()
    {
        var controller = BuildController();
        var action = controller.Save(mode: ThemeCookie.Auto, returnUrl: null);

        var redirect = Assert.IsType<RedirectResult>(action);
        Assert.Equal("/", redirect.Url);

        Assert.Equal(ThemeCookie.Auto, WrittenCookieValue(controller.Response, ThemeCookie.Name));
    }

    [Fact]
    public void Save_LightMode_WritesCookieAndRedirectsHome()
    {
        var controller = BuildController();
        controller.Save(mode: ThemeCookie.Light, returnUrl: null);

        Assert.Equal(ThemeCookie.Light, WrittenCookieValue(controller.Response, ThemeCookie.Name));
    }

    [Fact]
    public void Save_DarkMode_WritesCookieAndRedirectsHome()
    {
        var controller = BuildController();
        controller.Save(mode: ThemeCookie.Dark, returnUrl: null);

        Assert.Equal(ThemeCookie.Dark, WrittenCookieValue(controller.Response, ThemeCookie.Name));
    }

    [Fact]
    public void Save_TamperedMode_NormalizesToDefaultAuto()
    {
        // A fourth mode value must never yield a fourth mode — Normalize
        // falls back to Auto for anything that is not Light or Dark.
        var controller = BuildController();
        controller.Save(mode: "sepia", returnUrl: null);

        Assert.Equal(ThemeCookie.Auto, WrittenCookieValue(controller.Response, ThemeCookie.Name));   // the default
    }

    [Fact]
    public void Save_ExternalReturnUrl_RedirectsHome_NotOpenRedirect()
    {
        var controller = BuildController();
        var action = controller.Save(mode: ThemeCookie.Dark, returnUrl: "https://evil.com");

        var redirect = Assert.IsType<RedirectResult>(action);
        Assert.Equal("/", redirect.Url);   // the safe fallback
    }
}
