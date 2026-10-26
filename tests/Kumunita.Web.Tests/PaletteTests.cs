using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.SiteContent;
using Kumunita.Web.Controllers;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0175 — the palette preference (<see cref="PaletteCookie"/>), its
/// endpoint (<see cref="PaletteController"/>) and the stable monogram colour
/// (<see cref="AvatarHue"/>). Pure unit tests: no Postgres, no HTTP pipeline.
/// </summary>
public sealed class PaletteTests
{
    private static HttpRequest RequestWithCookie(string? value)
    {
        var ctx = new DefaultHttpContext();
        if (value is not null)
            ctx.Request.Headers.Cookie = $"{PaletteCookie.Name}={value}";
        return ctx.Request;
    }

    [Fact]
    public void Read_NoCookie_UsesPlatformDefault()
    {
        Assert.Equal(PaletteCookie.Ember, PaletteCookie.Read(RequestWithCookie(null), PaletteCookie.Ember));
    }

    [Fact]
    public void Read_NoCookie_NoDefault_IsForest()
    {
        Assert.Equal(PaletteCookie.Forest, PaletteCookie.Read(RequestWithCookie(null)));
    }

    [Fact]
    public void Read_MemberCookie_OverridesPlatformDefault()
    {
        Assert.Equal(PaletteCookie.Forest, PaletteCookie.Read(RequestWithCookie("forest"), PaletteCookie.Ember));
        Assert.Equal(PaletteCookie.Ember, PaletteCookie.Read(RequestWithCookie("EMBER"), PaletteCookie.Forest));
    }

    [Fact]
    public void Read_UnknownCookie_FallsBackToPlatformDefault()
    {
        Assert.Equal(PaletteCookie.Ember, PaletteCookie.Read(RequestWithCookie("nope"), PaletteCookie.Ember));
    }

    [Fact]
    public void Normalize_Unknown_IsForest()
    {
        Assert.Equal(PaletteCookie.Forest, PaletteCookie.Normalize("nope"));
        Assert.Equal(PaletteCookie.Forest, PaletteCookie.Normalize(null));
    }

    [Fact]
    public void Save_KnownPalette_WritesCookie_AndRedirectsLocalOnly()
    {
        var controller = new PaletteController { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.Url = new StubUrlHelper();

        var result = controller.Save("ember", "/community");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/community", redirect.Url);
        Assert.Contains($"{PaletteCookie.Name}=ember",
            controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public void Save_UnknownPalette_ClearsOverride()
    {
        var controller = new PaletteController { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.Url = new StubUrlHelper();

        controller.Save("nope", "/community");

        Assert.Contains($"Expires=Thu, 01 Jan 1970",
            controller.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public void Save_ExternalReturnUrl_RedirectsHome()
    {
        var controller = new PaletteController { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.Url = new StubUrlHelper();

        var result = controller.Save("ember", "https://evil.example/");

        Assert.Equal("/", Assert.IsType<RedirectResult>(result).Url);
    }

    [Fact]
    public void AvatarHue_IsStable_AndInRange()
    {
        var a = AvatarHue.For("subject-123");
        Assert.Equal(a, AvatarHue.For("subject-123"));
        Assert.InRange(a, 0, AvatarHue.Count - 1);
        Assert.Equal(0, AvatarHue.For(null));
        Assert.Equal(0, AvatarHue.For(""));
    }

    [Fact]
    public void AvatarHue_SpreadsAcrossAllBuckets()
    {
        var seen = new HashSet<int>();
        for (var i = 0; i < 200; i++) seen.Add(AvatarHue.For($"subject-{i}"));
        Assert.Equal(AvatarHue.Count, seen.Count);
    }
    [Fact]
    public void ReadOverride_IsNullWithoutKnownCookie()
    {
        Assert.Null(PaletteCookie.ReadOverride(RequestWithCookie(null)));
        Assert.Null(PaletteCookie.ReadOverride(RequestWithCookie("nope")));
        Assert.Equal(PaletteCookie.Ember, PaletteCookie.ReadOverride(RequestWithCookie("ember")));
    }

    [Fact]
    public async Task Save_EmptyValue_ClearsOverride()
    {
        var controller = new PaletteController { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.Url = new StubUrlHelper();

        controller.Save("", "/");

        // A delete is emitted as an expired Set-Cookie for the same name.
        Assert.Contains($"{PaletteCookie.Name}=", controller.Response.Headers.SetCookie.ToString());
        Assert.Contains("expires=", controller.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        await Task.CompletedTask;
    }

    // ── ADR 0175 D3 — the admin's platform default (SiteContent.DefaultPalette) ──

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    [Fact]
    public void SiteContent_DefaultPalette_DefaultsToForest()
    {
        Assert.Equal("forest", new SiteContent().DefaultPalette);
    }

    [Fact]
    public async Task SavePalette_NormalizesAndSavesOnce_PassingOtherFieldsThrough()
    {
        var current = new SiteContent { HomeHeroEyebrow = "keep me", AboutShowScope = false };
        var site = Substitute.For<ISiteContentService>();
        site.GetAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(current));
        var controller = new AdminSiteController(site)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, "admin-1"),
                        new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
                    }, "test")),
                },
            },
        };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());

        var result = await controller.SavePalette(
            new AdminSiteController.AdminSiteViewModel.AppearanceSection { DefaultPalette = "EMBER" });

        Assert.IsType<RedirectToActionResult>(result);
        await site.Received(1).SaveAsync(
            Arg.Is<SiteContent>(c => c.DefaultPalette == "ember" && c.HomeHeroEyebrow == "keep me" && !c.AboutShowScope),
            "admin-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavePalette_UnknownValue_StoresForest()
    {
        var site = Substitute.For<ISiteContentService>();
        site.GetAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new SiteContent()));
        var controller = new AdminSiteController(site)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());

        await controller.SavePalette(
            new AdminSiteController.AdminSiteViewModel.AppearanceSection { DefaultPalette = "<script>" });

        await site.Received(1).SaveAsync(
            Arg.Is<SiteContent>(c => c.DefaultPalette == "forest"),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
    // Local-URL stub: only paths starting with a single '/' are local
    // (the "no open redirect" pin, the F3U5 StubUrlHelper idiom).
    private sealed class StubUrlHelper : IUrlHelper
    {
        public ActionContext ActionContext { get; } = new();
        public string Action(UrlActionContext actionContext) => "stub-action";
        public string Content(string? content) => content ?? string.Empty;
        public bool IsLocalUrl(string? url) =>
            !string.IsNullOrEmpty(url) && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
        public string RouteUrl(UrlRouteContext routeContext) => "stub-route";
        public string Link(string? linkName, object? values) => "stub-link";
    }
}
