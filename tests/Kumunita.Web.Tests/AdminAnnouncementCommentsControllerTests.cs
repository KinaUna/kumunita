using Kumunita.Core.Announcements;
using Kumunita.Core.Identity;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// C12 — the <see cref="AdminAnnouncementCommentsController"/> seam (ADR 0101).
/// The GlobalAdmin's thin control plane over the announcement-comments gate.
/// Mirrors <see cref="AdminSignupControllerTests"/> exactly — the singleton-toggle
/// shape (one audited write lane). Two legs: Index reads the current gate;
/// Save writes the gate + redirects.
/// </summary>
public sealed class AdminAnnouncementCommentsControllerTests
{
    private const string Admin = "admin-comments-001";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    private static (AdminAnnouncementCommentsController controller, IAnnouncementService svc) Build(bool enabled)
    {
        var svc = Substitute.For<IAnnouncementService>();
        svc.AreAnnouncementCommentsEnabledAsync().Returns(enabled);
        var controller = new AdminAnnouncementCommentsController(svc);
        var httpContext = new DefaultHttpContext();
        // KumunitaPrincipal.SubjectId reads the claim with Type == "Kumunita.Sub"
        // (Kumunita.Core.Identity.ClaimTypes.Subject); use the raw string to keep
        // this test free of the System-vs-Kumunita ClaimTypes ambiguity.
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim("Kumunita.Sub", Admin) }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, svc);
    }

    [Fact]
    public async Task Index_ReturnsViewModel_WithCurrentGate()
    {
        var (controller, _) = Build(enabled: true);
        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminAnnouncementCommentsController.AnnouncementCommentsAdminViewModel>(view.ViewData.Model);
        Assert.True(vm.Enabled);
    }

    [Fact]
    public async Task Save_Open_CallsServiceAndRedirects()
    {
        var (controller, svc) = Build(enabled: false);
        var action = await controller.Save(enabled: true);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminAnnouncementCommentsController.Index), redirect.ActionName);
        await svc.Received(1).SetAnnouncementCommentsEnabledAsync(true, Admin);
    }

    [Fact]
    public async Task Save_Closed_CallsServiceAndRedirects()
    {
        var (controller, svc) = Build(enabled: true);
        var action = await controller.Save(enabled: false);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminAnnouncementCommentsController.Index), redirect.ActionName);
        await svc.Received(1).SetAnnouncementCommentsEnabledAsync(false, Admin);
    }
}
