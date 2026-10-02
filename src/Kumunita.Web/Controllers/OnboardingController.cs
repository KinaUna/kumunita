using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M22 (ADR 0132, D4) — the guided walk-through of a new resident's account
/// setup. Two actions, one controller (the <see cref="LocaleController"/>
/// settings-tabs shape):
/// <list type="bullet">
/// <item><c>GET /onboarding</c> — the step sequence (the closed
/// <c>onboarding.*</c> keys, D7) + the current completion state + the
/// finish/skip action. The model is the owner-scope read
/// (<see cref="IUserInfoService.GetProfileAsync"/> for
/// <c>OnboardingCompletedAt</c> + the per-step "set / not set" hints).
/// </item>
/// <item><c>POST /onboarding/finish</c> — the <b>one</b> write action: calls
/// U01's <see cref="IUserInfoService.CompleteOnboardingAsync"/> for the
/// signed-in subject and redirects home with the <c>onboarding.flash_done</c>
/// flash (the <c>LocaleController.FlashAsync</c> idiom).
/// </item>
/// <item><c>POST /onboarding/skip</c> — the optional "skip for now": the
/// <b>same</b> <see cref="IUserInfoService.CompleteOnboardingAsync"/> lane
/// (D4 §1.a-C2 — skip→finish; the completion flag does not distinguish
/// finish from skip, so it is the <c>onboarding.flash_done</c> flash too).
/// </item>
/// </list>
/// <para>
/// <b>Rides frozen lanes (D3):</b> the walk-through's <b>only</b> write is
/// <c>CompleteOnboardingAsync</c>; every field it surfaces links into the
/// existing editor/settings that owns it (display name/contact →
/// <c>/profile/edit</c>, avatar → <c>/profile</c>, UI language →
/// <c>/settings/language</c>, timezone → <c>/settings/timezone</c>, date
/// format → <c>/settings/dateformat</c>, email language →
/// <c>/settings/language</c>). The walk-through <b>never</b> re-implements a
/// write lane (C-M22·4).
/// </para>
/// <para>
/// <b>Non-blocking (D5):</b> sign-in is never gated; the "is a new user?"
/// check is the owner-scope read of <c>OnboardingCompletedAt</c> (never a
/// claim — the thin-token rule, ADR 0001-B). The <c>[Authorize]</c> here is
/// the standard signed-in gate (the surface is owner-scope, owner = the
/// signed-in actor), <b>not</b> an onboarding gate.
/// </para>
/// </summary>
[Authorize]
public sealed class OnboardingController(
    IUserInfoService userInfo,
    // The per-request localization read seams — used to render the
    // finish/skip flash in the resident's effective language (the
    // LocaleController.FlashAsync / ProfileController.FlashAsync idiom).
    // Optional (default null) so any test-construction site that builds this
    // controller without the seams keeps compiling and renders the
    // KnownTranslationKeys.EnValues source text (the kw-l floor, ADR 0015 D1);
    // DI always supplies the live ILocalizationService + ITranslationProvider
    // in the app.
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// Resolve an <c>onboarding.*</c> kw-l key to the resident's effective
    /// language (the house <see cref="EffectiveLanguageCode.ResolveAsync"/> +
    /// <see cref="ITranslationProvider.GetAsync"/> seam — the same chain the
    /// view's <c>&lt;kw-l&gt;</c> TagHelper uses, so the flash string renders
    /// in the resident's language). Falls back to the
    /// <see cref="KnownTranslationKeys.EnValues"/> source text when the
    /// translation seam is absent (the test-construction floor, ADR 0015 D1 —
    /// code is the floor, so a resident never sees a raw key).
    /// </summary>
    private async Task<string> FlashAsync(string key)
    {
        if (localization is null || translationProvider is null)
            return KnownTranslationKeys.EnValues.GetValueOrDefault(key) ?? key;
        return await translationProvider.GetAsync(
            key,
            await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider));
    }

    // ── GET /onboarding — the step sequence + completion state ────────────

    /// <summary>
    /// <c>GET /onboarding</c> — the guided walk-through. Seeds the model from
    /// the actor's saved <see cref="Profile"/> (the owner-scope read — the
    /// <c>OnboardingCompletedAt</c> completion state + the per-step "set /
    /// not set" hints off the frozen read seams, D3/D5). Owner-scope only —
    /// the principal is the signed-in actor, never a form field. A null
    /// subject (an unauthenticated request) seeds the not-completed floor, the
    /// <see cref="LocaleController.BuildModel"/> null-safe idiom.
    /// </summary>
    [HttpGet("/onboarding")]
    public async Task<IActionResult> Index()
    {
        var subjectId = SubjectId(User);
        Profile? profile = subjectId is null ? null : await userInfo.GetProfileAsync(subjectId);

        // The UI-language step reads the per-request preference cookie (a
        // plain BCP-47 string, not a Profile field — the thin-token rule,
        // ADR 0001-B; the LocaleController language-cookie lane). No subject
        // ⇒ no request ⇒ null (the "no preference" floor).
        var uiLanguage = HttpContext?.Request is { } req ? LocaleCookie.Read(req) : null;

        return View(new OnboardingViewModel
        {
            // The completion read (D2/D5): the owner-scope Profile field,
            // re-projected (never a claim, D6). null = not completed (the
            // banner floor, C-M22·5).
            OnboardingCompletedAt = profile?.OnboardingCompletedAt,
            // The per-step "set / not set" hints (D4/D5, C-M22·5) — each is
            // the non-empty check on the frozen read seam the step links into
            // (the design-doc §6 table).
            HasDisplayName = !string.IsNullOrWhiteSpace(profile?.DisplayName),
            HasAvatar = !string.IsNullOrWhiteSpace(profile?.AvatarId),
            HasTimezone = !string.IsNullOrWhiteSpace(profile?.TimeZone),
            HasDateFormat = !string.IsNullOrWhiteSpace(profile?.DateFormat),
            HasEmailLanguage = !string.IsNullOrWhiteSpace(profile?.EmailLanguage),
            UiLanguageCode = uiLanguage,
        });
    }

    // ── POST /onboarding/finish — the ONE write (U01's lane) ──────────────

    /// <summary>
    /// <c>POST /onboarding/finish</c> — stamp completion through U01's
    /// <see cref="IUserInfoService.CompleteOnboardingAsync"/> (the owner-scope,
    /// no-audit-row, never-load-or-creates lane, D2/C-M22·3) and redirect home
    /// with the <c>onboarding.flash_done</c> flash (the <c>FlashAsync</c>
    /// idiom). The walk-through's only write (D3/C-M22·4). A missing profile
    /// (fail-closed, the <c>SetProfileTimezoneAsync</c> pin) redirects home —
    /// the banner is gated on the read, so it will not persist for a
    /// profile-less actor.
    /// </summary>
    [HttpPost("/onboarding/finish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finish()
    {
        var subjectId = SubjectId(User) ?? string.Empty;
        try
        {
            await userInfo.CompleteOnboardingAsync(subjectId, subjectId);
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            // No profile to stamp (fail-closed) — treat as already-handled and
            // redirect home (the banner is gated on the read, so it will not
            // persist for a profile-less actor).
            return RedirectToAction("Index", "Home");
        }
        TempData["info"] = await FlashAsync("onboarding.flash_done");
        return RedirectToAction("Index", "Home");
    }

    // ── POST /onboarding/skip — routes to the SAME lane (§1.a-C2) ─────────

    /// <summary>
    /// <c>POST /onboarding/skip</c> — "skip for now": the <b>same</b>
    /// <see cref="IUserInfoService.CompleteOnboardingAsync"/> lane as
    /// <see cref="Finish"/> (D4 §1.a-C2 — skip→finish; the resident who skips
    /// is still "done" for banner-clearing purposes, and the flash is the
    /// single <c>onboarding.flash_done</c> key — the only user-visible
    /// difference is the button label they clicked).
    /// </summary>
    [HttpPost("/onboarding/skip")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Skip() => await Finish();
}
