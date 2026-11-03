using Kumunita.Core;
using Kumunita.Web;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        var controller = new HomeController(NullLogger<HomeController>.Instance, options);
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
        var controller = new HomeController(NullLogger<HomeController>.Instance, options);
        var view = Assert.IsType<ViewResult>(await controller.Index());

        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);
        Assert.Equal("Kumunita", vm.CommunityName);
    }

    [Fact]
    public async Task Index_FeedPresentButEmpty_ForAnonymous_Viewer_Without_Seams()
    {
        // ADR 0284 — the feed now renders for anonymous visitors too: the
        // posts column is structurally empty for them (posts are always
        // audience-restricted — Post.Audience is non-null so the frozen
        // Decide() has no "public" branch), the announcements column is
        // public-scope only (ListVisibleAsync with a null actor), and the
        // pages column is public pages only (CanSeeAsync with an empty
        // actor — branch 5, the public path).
        //
        // With no feed seams wired (a test construction with just the
        // logger + options) every column degrades to empty, so the Feed is
        // present but IsEmpty — the view's "nothing yet" note path.
        var controller = new HomeController(NullLogger<HomeController>.Instance, Options.Create(new CommunityOptions()));
        // No controller context ⇒ User is null ⇒ SubjectId null.
        var view = Assert.IsType<ViewResult>(await controller.Index());

        var vm = Assert.IsType<HomeViewModel>(view.ViewData.Model);
        Assert.NotNull(vm.Feed);
        Assert.True(vm.Feed!.IsEmpty,
            "With no feed seams wired, every column degrades to empty — the anonymous Feed must be IsEmpty.");
    }
}
