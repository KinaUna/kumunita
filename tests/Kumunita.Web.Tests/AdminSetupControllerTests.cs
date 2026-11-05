using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// C12 — the <see cref="AdminSetupController"/> seam (the <c>/admin/setup</c>
/// handoff, ADR 0001). The one-time lane where the seed GlobalAdmin account
/// receives its real password. The testable seam: the M2 closed-lane gate.
/// Once <see cref="IIdentityService.IsFirstBootSetupCompleteAsync"/> returns
/// <c>true</c> (the token was consumed / the account has a password), the GET
/// form returns <c>404</c> (not a 200-with-a-form) so an anonymous caller
/// cannot probe the account's existence — the "404-is-closed" rule.
/// </summary>
public sealed class AdminSetupControllerTests
{
    private static (AdminSetupController controller, IIdentityService identity) Build(bool setupComplete)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.IsFirstBootSetupCompleteAsync().Returns(setupComplete);

        // The GET paths never touch userManager / the claims factory (only the
        // POST swap+sign-in does), so null is enough; the factory is sealed
        // (NSubstitute can't proxy it) and its base ctor only stores the ref.
        // The GET paths never touch userManager (only the POST swap+sign-in
        // does); the factory base ctor null-checks it, so a real instance over
        // a substitute store is enough — the store is never called here.
        var userManager = new UserManager<User>(
            Substitute.For<IUserStore<User>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<IPasswordHasher<User>>(),
            new List<IUserValidator<User>>(),
            new List<IPasswordValidator<User>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            NullLogger<UserManager<User>>.Instance);
        var roleManager = new RoleManager<IdentityRole>(
            Substitute.For<IRoleStore<IdentityRole>>(),
            new List<IRoleValidator<IdentityRole>>(),
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);

        var claimsFactory = new KumunitaClaimsPrincipalFactory(
            userManager,
            roleManager,
            Substitute.For<IUserInfoService>(),
            Substitute.For<IProfileSettingsService>(),
            identity,
            Options.Create(new IdentityOptions()));

        var controller = new AdminSetupController(identity, userManager, claimsFactory);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return (controller, identity);
    }

    [Fact]
    public async Task Setup_OpenLane_ReturnsTheForm()
    {
        var (controller, _) = Build(setupComplete: false);
        var action = await controller.Setup();
        // The lane is open → the form is served.
        Assert.IsType<ViewResult>(action);
    }

    [Fact]
    public async Task Setup_ClosedLane_ReturnsNotFound()
    {
        // M2 — the setup lane is closed once the seed-admin account has a
        // working password: the GET returns 404 (uniform — no account-existence
        // signal to an anonymous caller).
        var (controller, identity) = Build(setupComplete: true);
        var action = await controller.Setup();
        Assert.IsType<NotFoundResult>(action);
        await identity.Received(1).IsFirstBootSetupCompleteAsync();
    }
}
