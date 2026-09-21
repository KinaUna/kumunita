using Kumunita.Core;
using Kumunita.Web;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Verifies <see cref="HomeController.Index"/> reflects <see cref="CommunityOptions"/>
/// into the view model so a config-wiring regression (e.g. wrong section name, or
/// <c>Community__*</c> env binding broken in OPS.md) is caught here, not in prod.
/// </summary>
public class HomeControllerTests
{
    private static async Task<(HomeController controller, HomeViewModel? vm)> RunIndexAsync(string name, string? supportEmail)
    {
        var options = Options.Create(new CommunityOptions
        {
            Name = name,
            SupportEmail = supportEmail,
        });
        var controller = new HomeController(options);
        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        return (controller, Assert.IsType<HomeViewModel>(view.ViewData.Model));
    }

    [Fact]
    public async Task Index_Renders_CommunityName_From_Config()
    {
        var (_, vm) = await RunIndexAsync("Maplewood Residents", "maps@example.com");

        Assert.Equal("Maplewood Residents", vm!.CommunityName);
    }

    [Fact]
    public async Task Index_Forwards_SupportEmail_From_Config()
    {
        var (_, vm) = await RunIndexAsync("Maplewood", "resident@example.org");

        Assert.Equal("resident@example.org", vm!.SupportEmail);
    }

    [Fact]
    public async Task Index_FallsBack_To_DefaultName_When_Unset()
    {
        // Program.cs binds Community__* on startup; if nothing is set, the POCO
        // default "Kumunita" surfaces — pinning this here protects the OPS default.
        var options = Options.Create(new CommunityOptions());
        var controller = new HomeController(options);
        var view = Assert.IsType<ViewResult>(await controller.Index());

        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);
        Assert.Equal("Kumunita", vm.CommunityName);
    }

    [Fact]
    public async Task Index_NoFeed_ForAnonymous_Viewer()
    {
        // The feed is signed-in only; an anonymous visitor (no SubjectId claim)
        // gets null Feed — the view renders hero + roadmap only.
        var controller = new HomeController(Options.Create(new CommunityOptions()));
        // No controller context ⇒ User is null ⇒ SubjectId null.
        var view = Assert.IsType<ViewResult>(await controller.Index());

        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);
        Assert.Null(vm.Feed);
    }
}
