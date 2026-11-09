using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Kumunita.Web.Tests;

public sealed class StatusControllerTests
{
    [Theory]
    [InlineData(404)]
    [InlineData(403)]
    [InlineData(400)]
    [InlineData(413)]
    [InlineData(415)]
    [InlineData(429)]
    [InlineData(500)]
    public void Index_ReturnsLocalizedPageForSupportedCode(int code)
    {
        var controller = new StatusController();
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("community.example");
        context.Request.Headers.Referer = "https://community.example/posts/123?sort=new";
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = Assert.IsType<ViewResult>(controller.Index(code));
        var model = Assert.IsType<StatusPageViewModel>(result.Model);

        Assert.Equal(code, model.Code);
        Assert.Equal("/posts/123?sort=new", model.BackUrl);
    }

    [Fact]
    public void Index_DoesNotUseExternalRefererAsBackTarget()
    {
        var controller = new StatusController();
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("community.example");
        context.Request.Headers.Referer = "https://outside.example/";
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = Assert.IsType<ViewResult>(controller.Index(404));
        var model = Assert.IsType<StatusPageViewModel>(result.Model);

        Assert.Equal("/", model.BackUrl);
    }
}
