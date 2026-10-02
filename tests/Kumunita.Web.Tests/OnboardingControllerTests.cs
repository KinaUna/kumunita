using System.Reflection;
using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// M22 · U02 — the <see cref="OnboardingController"/> /
/// <see cref="OnboardingViewModel"/> Web-surface pins (the U02 exit gate's
/// Web.Tests-only half). Three faces, matching the register's
/// <c>§gate</c> for the walk-through surface:
/// <list type="number">
/// <item><b>GATE-5 — the banner is gated on the completion read and is
/// non-blocking (D5, C-M22·5).</b> A <c>GET /onboarding</c> for a not-completed
/// resident (a <c>null</c> <c>OnboardingCompletedAt</c>) exposes
/// <see cref="OnboardingViewModel.BannerEligible"/> = <c>true</c>; a
/// completed resident (a non-null stamp) exposes <c>false</c>. The per-step
/// "set / not set" hints are the derived reads off the frozen seams. The model
/// exposes the completion state; the controller never gates sign-in and reads
/// no claim (the owner-scope read, D6).</item>
/// <item><b>GATE-3 — the walk-through's only write is
/// <see cref="IUserInfoService.CompleteOnboardingAsync"/> (D3, C-M22·4).</b>
/// <c>POST /onboarding/finish</c> calls the U01 lane exactly once for the
/// signed-in subject, redirects home with the <c>onboarding.flash_done</c>
/// flash, and calls <b>no</b> other field-write lane. <c>POST
/// /onboarding/skip</c> routes to the same single lane.</item>
/// <item><b>GATE-4 — zero new authorization surface (D6, C-M22·2).</b> The
/// controller injects no <see cref="IAuthorizationService"/> and the view model
/// exposes no <c>Can</c>/<c>Decide</c>/<c>AccessVia</c> member — M22 rides the
/// owner-scope lane, it does not extend the authorization seams.</item>
/// </list>
/// </summary>
public class OnboardingControllerTests
{
    private const string Owner = "resident-1";

    // ── GATE-5 — banner gated on the completion read, non-blocking ───────

    /// <summary>
    /// GATE-5 / C-M22·5 — a not-completed resident (a <c>null</c>
    /// <c>OnboardingCompletedAt</c>, the floor) gets <c>GET /onboarding</c>
    /// returning a view whose model exposes <see cref="OnboardingViewModel
    /// .BannerEligible"/> = <c>true</c> + <c>OnboardingCompletedAt = null</c>
    /// + every step hint <c>false</c> (nothing set yet). Owner-scope read: the
    /// model is seeded from the signed-in subject's <see cref="Profile"/>
    /// (never a claim, D6).
    /// </summary>
    [Fact]
    public async Task Index_NotCompleted_Exposes_BannerEligible_And_NullCompletion()
    {
        var (controller, userInfo) = BuildController(Owner);
        // A fresh resident: a profile with no completion stamp and no fields set.
        var profile = new Profile { SubjectId = Owner, DisplayName = "A. Resident" };
        userInfo.GetProfileAsync(Owner).Returns(profile);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<OnboardingViewModel>(view.ViewData.Model);

        Assert.Null(model.OnboardingCompletedAt);
        Assert.True(model.BannerEligible);           // null completion → the banner renders (the floor)
        Assert.False(model.HasAvatar);
        Assert.False(model.HasTimezone);
        Assert.False(model.HasDateFormat);
        Assert.False(model.HasEmailLanguage);
        // The read seam was consulted for the signed-in subject, exactly once.
        await userInfo.Received(1).GetProfileAsync(Owner);
    }

    /// <summary>
    /// GATE-5 / C-M22·5 — a completed resident (a non-null
    /// <c>OnboardingCompletedAt</c>) gets <c>GET /onboarding</c> returning a
    /// model with <see cref="OnboardingViewModel.BannerEligible"/> =
    /// <c>false</c>, and the per-step hints reflect the frozen read seams
    /// (the fields that are set read <c>true</c>; the UI-language hint is the
    /// per-request <c>LocaleCookie</c> read, not a <c>Profile</c> field).
    /// Non-blocking (D5): the completion is a profile-field read, never a
    /// claim (D6).
    /// </summary>
    [Fact]
    public async Task Index_Completed_Exposes_BannerCleared_And_SetHints()
    {
        var (controller, userInfo) = BuildController(Owner);
        var stamp = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var profile = new Profile
        {
            SubjectId = Owner,
            DisplayName = "A. Resident",
            AvatarId = "abc123",
            TimeZone = "Europe/Warsaw",
            DateFormat = "yyyy-MM-dd",
            EmailLanguage = "de",
            OnboardingCompletedAt = stamp,
        };
        userInfo.GetProfileAsync(Owner).Returns(profile);
        // The UI-language hint is the per-request cookie read (the thin-token
        // rule, ADR 0001-B — a plain BCP-47 string, not a Profile field).
        controller.ControllerContext.HttpContext.Request.Headers["Cookie"] =
            $"{Kumunita.Web.Security.LocaleCookie.Name}=de";

        var result = await controller.Index();

        var model = Assert.IsType<OnboardingViewModel>(Assert.IsType<ViewResult>(result).ViewData.Model);

        Assert.Equal(stamp, model.OnboardingCompletedAt);
        Assert.False(model.BannerEligible);          // non-null completion → the banner clears
        Assert.True(model.HasDisplayName);
        Assert.True(model.HasAvatar);
        Assert.True(model.HasTimezone);
        Assert.True(model.HasDateFormat);
        Assert.True(model.HasEmailLanguage);
        Assert.Equal("de", model.UiLanguageCode);    // the cookie read (a string, not a Profile field)
    }

    // ── GATE-3 — the walk-through's only write is CompleteOnboardingAsync ─

    /// <summary>
    /// GATE-3 / C-M22·4 — <c>POST /onboarding/finish</c> is the walk-through's
    /// <b>one</b> write: it calls U01's
    /// <see cref="IUserInfoService.CompleteOnboardingAsync"/> exactly once for
    /// the signed-in subject (actorBy = the same subject — owner-scope, never a
    /// form value), redirects home with the <c>onboarding.flash_done</c> flash
    /// (resolved to the <c>KnownTranslationKeys.EnValues</c> floor in this
    /// test-construction site), and calls <b>no</b> other field-write lane
    /// (the walk-through rides the frozen lanes; it never re-implements a
    /// write, C-M22·4).
    /// </summary>
    [Fact]
    public async Task Finish_Calls_CompleteOnboardingAsync_ExactlyOnce_For_SignedIn_Subject()
    {
        var (controller, userInfo) = BuildController(Owner);
        userInfo.CompleteOnboardingAsync(Owner, Owner).Returns(Task.CompletedTask);

        var result = await controller.Finish();

        // The one write: the U01 owner-scope lane, exactly once, for the
        // signed-in subject (actorBy = the subject).
        await userInfo.Received(1).CompleteOnboardingAsync(Owner, Owner);

        // GATE-3 (C-M22·4): NO other field-write lane is called — the
        // walk-through's only write is CompleteOnboardingAsync.
        await userInfo.DidNotReceive().UpsertProfileAsync(
            Arg.Any<Profile>(), Arg.Any<ProfileUpdate>(), Arg.Any<string?>());
        await userInfo.DidNotReceive().SetProfileAvatarAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>());
        await userInfo.DidNotReceive().SetProfileTimezoneAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>());
        await userInfo.DidNotReceive().SetProfileDateFormatAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>());
        await userInfo.DidNotReceive().SetProfileEmailLanguageAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>());

        // Redirects home (the LocaleController / ProfileController redirect
        // idiom — "Index" / "Home").
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);

        // The flash (the onboarding.flash_done key — authored by U03) is
        // written under the house "info" TempData key on the redirect (the
        // LocaleController.Save / ProfileController.Edit flash idiom). In this
        // test site FlashAsync resolves to the KnownTranslationKeys.EnValues
        // floor (no live ITranslationProvider) — the kw-l floor, ADR 0015 D1 —
        // so the value is non-null either way (the en text, or the key itself
        // if U03 has not yet authored it). The pin asserts a flash WAS written
        // (the GATE-3 "flash on the redirect" shape), not its exact text.
        Assert.NotNull(controller.TempData["info"]);
    }

    /// <summary>
    /// GATE-3 / §1.a-C2 — <c>POST /onboarding/skip</c> routes to the <b>same</b>
    /// <see cref="IUserInfoService.CompleteOnboardingAsync"/> lane as
    /// <see cref="OnboardingController.Finish"/> (skip→finish: the completion
    /// flag does not distinguish finish from skip) — exactly one write, for
    /// the signed-in subject, no other field-write lane.
    /// </summary>
    [Fact]
    public async Task Skip_Calls_The_Same_CompleteOnboardingAsync_Lane_As_Finish()
    {
        var (controller, userInfo) = BuildController(Owner);
        userInfo.CompleteOnboardingAsync(Owner, Owner).Returns(Task.CompletedTask);

        var result = await controller.Skip();

        // The same single lane, exactly once, for the signed-in subject.
        await userInfo.Received(1).CompleteOnboardingAsync(Owner, Owner);
        await userInfo.DidNotReceive().UpsertProfileAsync(
            Arg.Any<Profile>(), Arg.Any<ProfileUpdate>(), Arg.Any<string?>());

        Assert.IsType<RedirectToActionResult>(result);
    }

    // ── GATE-4 — zero new authorization surface ───────────────────────────

    /// <summary>
    /// GATE-4 / C-M22·2 — the <see cref="OnboardingController"/> injects
    /// <b>no</b> <see cref="IAuthorizationService"/> (the walk-through is
    /// owner-scope only — it rides the frozen owner-scope lane; it does not
    /// extend the authorization seams), and the
    /// <see cref="OnboardingViewModel"/> exposes <b>no</b>
    /// <c>Can</c>/<c>Decide</c>/<c>AccessVia</c> member (a claim-encoded
    /// "is new" flag or a per-surface decision would fail here — the thin-
    /// token rule, ADR 0001-B / D6). The "is a new user?" state is a
    /// profile-field read, never a claim.
    /// </summary>
    [Fact]
    public void OnboardingSurface_Has_ZeroAuthorizationSurface()
    {
        // The controller's ctor has no IAuthorizationService parameter (the
        // walk-through never resolves "may this actor see that resource?" —
        // it is owner-scope; the owner IS the signed-in actor).
        var ctorParamTypes = typeof(OnboardingController)
            .GetConstructors()
            .SelectMany(c => c.GetParameters().Select(p => p.ParameterType))
            .ToList();
        Assert.DoesNotContain(
            typeof(Kumunita.Core.Authorization.IAuthorizationService), ctorParamTypes);

        // The view model exposes no authorization/decision member (no
        // Can*/Decide*/AccessVia member — the completion state is a plain
        // profile-field read, D6).
        var vmMembers = typeof(OnboardingViewModel)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.MemberType is MemberTypes.Property || m.MemberType is MemberTypes.Method)
            .Select(m => m.Name)
            .ToList();
        Assert.DoesNotContain(vmMembers, n => n.StartsWith("Can", StringComparison.Ordinal));
        Assert.DoesNotContain(vmMembers, n => n.StartsWith("Decide", StringComparison.Ordinal));
        Assert.DoesNotContain(vmMembers, n => n.Contains("AccessVia", StringComparison.Ordinal));
    }

    // ── fixtures ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="OnboardingController"/> wired to an
    /// <see cref="IUserInfoService"/> stand-in (the write lane never touches a
    /// real store — it is called in-process). The <c>ILocalizationService</c> /
    /// <c>ITranslationProvider</c> seams are left <c>null</c> (the
    /// default-ctor optional params) so <see cref="OnboardingController
    /// .FlashAsync"/> resolves the flash to the
    /// <see cref="Kumunita.Core.Localization.KnownTranslationKeys.EnValues"/>
    /// floor (the kw-l floor, ADR 0015 D1). The signed-in principal carries the
    /// <c>Kumunita.Sub</c> claim (the repo's identity claim —
    /// <see cref="Kumunita.Web.Security.KumunitaPrincipal.SubjectId"/> reads
    /// <c>"Kumunita.Sub"</c>).
    /// </summary>
    private static (OnboardingController controller, IUserInfoService userInfo) BuildController(
        string principalSubjectId)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        // The optional ILocalizationService/ITranslationProvider seams are
        // null (default) — FlashAsync renders the EnValues floor.
        var controller = new OnboardingController(userInfo);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim("Kumunita.Sub", principalSubjectId) }, "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        return (controller, userInfo);
    }

    /// <summary>An in-memory <see cref="ITempDataProvider"/> — closes the
    /// <c>TempData</c> bag for the finish/skip flash write so the assertion can
    /// read <c>TempData["info"]</c> after the action returns (the
    /// <see cref="M23ProfileExtendedTests"/> idiom).</summary>
    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        private readonly Dictionary<string, object?> _bag = new();
        public IDictionary<string, object?> LoadTempData(HttpContext context) => _bag;
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            _bag.Clear();
            foreach (var (k, v) in values) _bag[k] = v;
        }
    }
}
