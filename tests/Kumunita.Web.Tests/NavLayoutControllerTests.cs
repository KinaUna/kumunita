using System;
using System.Linq;
using Kumunita.Web.Controllers;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// C12 — the <see cref="NavLayoutController"/> seam (ADR 0111).
/// The nav-variant preference endpoint: POST <c>/nav-layout</c> normalizes
/// the <c>variant</c> field to B (<see cref="NavLayoutCookie.Row"/>) or C
/// (<see cref="NavLayoutCookie.Rail"/>) and writes the frozen
/// <see cref="NavLayoutCookie"/>. A tampered value (a third variant) is
/// normalized to the default (Row) so a client can never yield a third layout.
/// The write is a preference, never a claim (thin-token rule, ADR 0001-B).
/// </summary>
public sealed class NavLayoutControllerTests
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

    private static NavLayoutController BuildController()
    {
        var controller = new NavLayoutController();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/nav-layout";
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
    public void Save_RowVariant_WritesCookieAndRedirectsHome()
    {
        var controller = BuildController();
        var action = controller.Save(variant: NavLayoutCookie.Row, returnUrl: null);

        var redirect = Assert.IsType<RedirectResult>(action);
        Assert.Equal("/", redirect.Url);

        Assert.Equal(NavLayoutCookie.Row, WrittenCookieValue(controller.Response, NavLayoutCookie.Name));
    }

    [Fact]
    public void Save_RailVariant_WritesCookieAndRedirectsHome()
    {
        var controller = BuildController();
        var action = controller.Save(variant: NavLayoutCookie.Rail, returnUrl: null);

        var redirect = Assert.IsType<RedirectResult>(action);
        Assert.Equal("/", redirect.Url);

        Assert.Equal(NavLayoutCookie.Rail, WrittenCookieValue(controller.Response, NavLayoutCookie.Name));
    }

    [Fact]
    public void Save_TamperedVariant_NormalizesToDefault()
    {
        // A third variant value must never yield a third layout — Normalize
        // falls back to Row (variant B) for anything that is not Rail.
        var controller = BuildController();
        controller.Save(variant: "bogus-value", returnUrl: null);

        Assert.Equal(NavLayoutCookie.Row, WrittenCookieValue(controller.Response, NavLayoutCookie.Name));   // the default
    }

    [Fact]
    public void Save_LocalReturnUrl_RedirectsToReturnUrl()
    {
        var controller = BuildController();
        var action = controller.Save(variant: NavLayoutCookie.Row, returnUrl: "/community/abc");

        var redirect = Assert.IsType<RedirectResult>(action);
        Assert.Equal("/community/abc", redirect.Url);
    }

    [Fact]
    public void Save_ExternalReturnUrl_RedirectsHome_NotOpenRedirect()
    {
        var controller = BuildController();
        // IsLocalUrl("https://evil.com") is false → the open-redirect guard.
        var action = controller.Save(variant: NavLayoutCookie.Row, returnUrl: "https://evil.com");

        var redirect = Assert.IsType<RedirectResult>(action);
        Assert.Equal("/", redirect.Url);   // the safe fallback
    }
}
