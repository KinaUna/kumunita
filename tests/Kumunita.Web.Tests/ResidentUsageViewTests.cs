using System.Collections.Generic;
using Kumunita.Core.Usage;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using Claim = System.Security.Claims.Claim;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;

namespace Kumunita.Web.Tests;

/// <summary>
/// The 2 pinned resident Web tests for the self-only storage-usage read
/// (M25 U7 — <see cref="AccountController.Storage"/>, design doc §2.4, items
/// 22–23, verbatim):
/// <see cref="ResidentUsageView_SelfOnly"/> (C-UP·4/F7) and
/// <see cref="ResidentUsageView_QuotaZeroShowsUnlimited"/> (C-UP·5).
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom
/// (NSubstitute seams + <see cref="DefaultHttpContext"/> — the shape
/// <see cref="AdminStorageControllerTests"/> and
/// <see cref="BlockedAccountMiddlewareTests"/> use), <b>not</b> a full host
/// boot. <see cref="IStorageSettingsService"/> (the read seams —
/// <c>GetOrCreateAsync</c> + <c>GetPerUserUsageBytesAsync</c>) is NSubstituted
/// and registered in the controller's <see
/// cref="HttpContext.RequestServices"/> (the
/// <c>AccountController.Verify</c> idiom: the action resolves the seam from the
/// request scope, so the controller constructor is unchanged). The signed-in
/// resident's principal carries the <see cref="Kumunita.Core.Identity.ClaimTypes.Subject"/>
/// claim; the action's subject is <b>that</b> value — never a path param
/// (self-only, C-UP·4/F7).
/// <para>
/// <b>The "unlimited" sentinel (C-UP·5):</b> a quota of <c>0</c> renders as
/// "Unlimited" (<see cref="ResidentStorageViewModel.RemainingBytes"/> =
/// <c>null</c>, <c>QuotaHuman</c>/<c>RemainingHuman</c> = "Unlimited") — not
/// <c>0</c> remaining and not an error.
/// </para>
/// </summary>
public class ResidentUsageViewTests
{
    private const string Resident = "m25-resident-001";   // the signed-in subject
    private const string Other    = "m25-resident-other";  // a resident's numbers must never be read

    // ── The 2 resident Web pins (design §2.4, items 22–23) ─────────────────

    [Fact]
    public async Task ResidentUsageView_SelfOnly()
    {
        var settings = Substitute.For<IStorageSettingsService>();
        settings.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(new CommunityStorageSettings { PerUserQuotaBytes = 5000 });
        // The signed-in resident's own usage (the C-SM·7 Σ SizeBytes WHERE
        // CreatedById seam, re-exposed by IStorageSettingsService).
        settings.GetPerUserUsageBytesAsync(Resident, Arg.Any<CancellationToken>())
            .Returns(1000L);
        // A *different* resident's usage is stubbed to a distinct figure so a
        // self-only pin can prove it is never read (C-UP·4/F7).
        settings.GetPerUserUsageBytesAsync(Other, Arg.Any<CancellationToken>())
            .Returns(7777L);

        var controller = Build(settings);
        var result = await controller.Storage();
        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<ResidentStorageViewModel>(view.Model);

        // Self-only (C-UP·4/F7): the read is driven by the signed-in principal
        // — exactly the resident's own subject — and never another resident's.
        await settings.Received(1).GetPerUserUsageBytesAsync(Resident, Arg.Any<CancellationToken>());
        await settings.DidNotReceive().GetPerUserUsageBytesAsync(Other, Arg.Any<CancellationToken>());

        // The resident's own numbers: usage, the community per-user quota, and
        // remaining = quota − usage (a concrete cap, so remaining is not null).
        Assert.Equal(1000L, vm.MyUsageBytes);
        Assert.Equal(5000L, vm.PerUserQuotaBytes);
        Assert.Equal(4000L, vm.RemainingBytes);
        Assert.False(vm.QuotaUnlimited);
        Assert.Equal(ResidentStorageViewModel.FormatBytes(1000), vm.MyUsageHuman);
        Assert.NotEqual("Unlimited", vm.QuotaHuman);          // a concrete quota, not "Unlimited"
        Assert.Equal(ResidentStorageViewModel.FormatBytes(4000), vm.RemainingHuman);
    }

    [Fact]
    public async Task ResidentUsageView_QuotaZeroShowsUnlimited()
    {
        // C-UP·5 (the sentinel): the community per-user quota is 0 ⇒ unlimited.
        var settings = Substitute.For<IStorageSettingsService>();
        settings.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(new CommunityStorageSettings { PerUserQuotaBytes = 0 });
        settings.GetPerUserUsageBytesAsync(Resident, Arg.Any<CancellationToken>())
            .Returns(999L);

        var controller = Build(settings);
        var result = await controller.Storage();   // renders — not 404, not an error

        // A quota of 0 is a successful read (ViewResult), never an error status.
        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<ResidentStorageViewModel>(view.Model);

        Assert.Equal(999L, vm.MyUsageBytes);
        Assert.Equal(0L, vm.PerUserQuotaBytes);
        Assert.True(vm.QuotaUnlimited);
        // "Unlimited" (C-UP·5) — not 0 remaining and not an error.
        Assert.Null(vm.RemainingBytes);
        Assert.Equal("Unlimited", vm.QuotaHuman);
        Assert.Equal("Unlimited", vm.RemainingHuman);
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the controller with a NSubstituted <see cref="IStorageSettingsService"/>
    /// registered in the controller's <see cref="HttpContext.RequestServices"/>
    /// (the <c>AccountController.Verify</c> idiom — the action resolves the seam
    /// from the request scope, so no constructor dependency is added and the
    /// existing direct-construction harnesses keep compiling). Over a
    /// <see cref="DefaultHttpContext"/> whose <see cref="HttpContext.User"/> carries
    /// an authenticated principal whose <see cref="Kumunita.Core.Identity.ClaimTypes.Subject"/>
    /// is <see cref="Resident"/> (the self-only subject, C-UP·4/F7).
    /// </summary>
    private static AccountController Build(IStorageSettingsService settings)
    {
        // The resident-facing surface reads none of the identity seams on the
        // Storage lane (they serve signup/verify/login), so null is safe — it is
        // never dereferenced on this path (the AccountControllerSignupGateTests
        // null-ctor pattern).
        var controller = new AccountController(
            signInManager: null!,
            userManager:   null!,
            identity:      null!,
            userInfo:      null!,
            store:         null!);

        var context = new DefaultHttpContext
        {
            User = BuildPrincipal(),
            RequestServices = new ServiceCollection()
                .AddSingleton<IStorageSettingsService>(settings)
                .BuildServiceProvider(),
        };
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        // A directly-constructed controller has no TempData; Controller.View(model)
        // reads the TempData getter, whose default path would look up
        // ITempDataDictionaryFactory from RequestServices (absent here). Setting it
        // directly with a no-op provider (the AdminStorageControllerTests house
        // pattern) short-circuits that lookup.
        controller.TempData = new TempDataDictionary(context, new NoOpTempDataProvider());
        return controller;
    }

    private static ClaimsPrincipal BuildPrincipal() =>
        new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Resident),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Kumunita.Core.Identity.Roles.Member),
                },
                authenticationType: "test"));

    /// <summary>
    /// No-op <see cref="ITempDataProvider"/> (the
    /// <see cref="AdminStorageControllerTests"/> house pattern): the assertion
    /// target is the view-model projection, not the TempData bag, so the provider
    /// loads an empty dictionary and drops saved values.
    /// </summary>
    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the view model, not the bag
        }
    }
}
