using System.Security.Claims;
using Kumunita.Core.Messaging;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M9 amendment — the <see cref="MessagingSettingsController"/> unit tests
/// (the /settings/messaging per-resident messaging opt-in surface). The
/// pins:
/// <list type="number">
/// <item><b>GET renders the model.</b> The action reads
///       <see cref="IMessagingService.IsMessagingEnabledAsync"/> into
///       <see cref="MessagingSettingsController.MessagingSettingsViewModel.InstanceEnabled"/>
///       and the resident's <see cref="Profile.MessagingOptIn"/> +
///       <see cref="Profile.MessagingRestricted"/> into the model. A
///       missing profile degrades to the floor (opt-in off, not
///       restricted).</item>
/// <item><b>POST writes the opt-in.</b> The action calls
///       <see cref="IUserInfoService.SetMessagingOptInAsync"/> with the
///       actor's subject id + the form's <c>optIn</c> value + the actor's
///       subject id (the self-scope check, the
///       <c>CompleteOnboardingAsync</c> owner-scope shape).</item>
/// <item><b>POST refuses when restricted.</b> When the guardian's ceiling
///       (<see cref="Profile.MessagingRestricted"/>) is on, the action
///       sets <c>TempData["error"]</c> and does <b>not</b> call
///       <see cref="IUserInfoService.SetMessagingOptInAsync"/> — the
///       Web-boundary enforcement of the ceiling (the ADR 0028 G·2/G·3
///       shape carried to the messaging lane).</item>
/// </list>
/// No database, no Testcontainers — the controller consumes the
/// <b>interfaces</b> and NSubstitute fills in.
/// </summary>
public sealed class MessagingSettingsControllerTests
{
    private const string Actor = "subj-resident-ms-001";

    // ── GET renders the model ─────────────────────────────────────────────

    [Fact]
    public async Task Index_Renders_Model_WithInstanceEnabledAndOptIn()
    {
        var (controller, messaging, userInfo) = Build(true, optIn: true, restricted: false);

        var action = await controller.Index();
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<MessagingSettingsController.MessagingSettingsViewModel>(view.ViewData.Model);

        Assert.True(model.InstanceEnabled);
        Assert.True(model.OptIn);
        Assert.False(model.Restricted);

        await messaging.Received(1).IsMessagingEnabledAsync();
    }

    [Fact]
    public async Task Index_MissingProfile_FloorsToOptInOff_NotRestricted()
    {
        var (controller, messaging, userInfo) = Build(true, optIn: null, restricted: null);

        var action = await controller.Index();
        var view   = Assert.IsType<ViewResult>(action);
        var model  = Assert.IsType<MessagingSettingsController.MessagingSettingsViewModel>(view.ViewData.Model);

        Assert.True(model.InstanceEnabled);
        Assert.False(model.OptIn);          // the floor
        Assert.False(model.Restricted);     // the floor
    }

    // ── POST writes the opt-in ────────────────────────────────────────────

    [Fact]
    public async Task Save_OptInTrue_CallsSetMessagingOptInAsync()
    {
        var (controller, messaging, userInfo) = Build(true, optIn: false, restricted: false);

        var action = await controller.Save(optIn: true);
        var redirect = Assert.IsType<RedirectToActionResult>(action);

        Assert.Equal(nameof(MessagingSettingsController.Index), redirect.ActionName);
        await userInfo.Received(1).SetMessagingOptInAsync(Actor, true, Actor);
    }

    [Fact]
    public async Task Save_OptInFalse_CallsSetMessagingOptInAsync()
    {
        var (controller, messaging, userInfo) = Build(true, optIn: true, restricted: false);

        var action = await controller.Save(optIn: false);
        var redirect = Assert.IsType<RedirectToActionResult>(action);

        Assert.Equal(nameof(MessagingSettingsController.Index), redirect.ActionName);
        await userInfo.Received(1).SetMessagingOptInAsync(Actor, false, Actor);
    }

    // ── POST refuses when the guardian's ceiling is on ───────────────────

    [Fact]
    public async Task Save_Restricted_Refuses_NoWrite()
    {
        var (controller, messaging, userInfo) = Build(true, optIn: false, restricted: true);

        var action = await controller.Save(optIn: true);
        var redirect = Assert.IsType<RedirectToActionResult>(action);

        Assert.Equal(nameof(MessagingSettingsController.Index), redirect.ActionName);
        // The ceiling was enforced at the Web boundary: the write was refused.
        await userInfo.DidNotReceiveWithAnyArgs()
            .SetMessagingOptInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<string>());
    }

    // ── harness ───────────────────────────────────────────────────────────

    private static (MessagingSettingsController controller, IMessagingService messaging, IUserInfoService userInfo)
        Build(bool instanceEnabled, bool? optIn, bool? restricted)
    {
        var messaging = Substitute.For<IMessagingService>();
        messaging.IsMessagingEnabledAsync().Returns(instanceEnabled);

        var userInfo = Substitute.For<IUserInfoService>();
        if (optIn is not null || restricted is not null)
        {
            userInfo.GetProfileAsync(Actor).Returns(new Profile
            {
                SubjectId = Actor,
                DisplayName = "Ben",
                MessagingOptIn = optIn ?? false,
                MessagingRestricted = restricted ?? false,
            });
        }
        else
        {
            userInfo.GetProfileAsync(Actor).Returns((Profile?)null);
        }

        var controller = new MessagingSettingsController(
            NullLogger<MessagingSettingsController>.Instance, messaging, userInfo);
        WireControllerContext(controller, Actor);

        return (controller, messaging, userInfo);
    }

    private static void WireControllerContext(Controller controller, string subjectId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new List<Claim> { new(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId) },
                authenticationType: "test"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };

        // The Save action sets TempData["info"] / ["error"] — a no-op
        // provider closes the gap without requiring a real session (the
        // MessagesControllerTests.NoOpTempDataProvider idiom).
        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), new NoOpTempDataProvider());
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();

        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
