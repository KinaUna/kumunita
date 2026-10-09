using Kumunita.Core.AdminOnboarding;
using Kumunita.Core.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M30 (ADR 0153, M30·1–M30·7) — the <c>/admin/onboarding</c> surface: the
/// GlobalAdmin's guided walk-through through the seven most important initial
/// settings (community name, languages, moderation, notifications, storage
/// limits, site content, issue escalation — the closed set in the README /
/// <c>Milestones.cs</c> order). Mirrors <see cref="AdminSiteController"/>
/// (ADR 0150, the <c>/admin/site</c> shape) + <see cref="OnboardingController"/>
/// (ADR 0132 M22, the <c>/onboarding</c> shape) exactly:
/// <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>-gated (the
/// <c>AdminController</c> precedent), a thin read over the
/// <b>one</b> <see cref="IAdminOnboardingService.GetAsync"/> read seam
/// (M30·3 — best-effort, never audited, the ADR 0050
/// <c>IsSignupOpenAsync</c> shape), a thin write over the <b>one</b>
/// <see cref="IAdminOnboardingService.CompleteAsync"/> lane (M30·4 — the
/// ADR 0150 / ADR 0050 single audited write-lane shape, exactly one
/// <c>AccessAudit</c> row), and the flash is the
/// <c>adminonboarding.flash_done</c> key (the <see cref="AdminQuietController.FlashAsync"/>
/// idiom, the <see cref="LocaleController.FlashAsync"/> /
/// <see cref="OnboardingController.FlashAsync"/> shape, admin-scope).
/// <para>
/// **Rides frozen lanes (M30·1, M30·7):** the walk-through's *only* write
/// is <see cref="IAdminOnboardingService.CompleteAsync"/>; every step the
/// view surfaces links into the existing admin surface that already owns
/// the setting (community name + languages → <c>/admin/languages</c>,
/// moderation → <c>/admin/announcements/comments</c>, notifications →
/// <c>/admin/quiet</c>, storage limits → <c>/admin/storage/settings</c>,
/// site content → <c>/admin/site</c>, issue escalation →
/// <c>/admin/announcements</c> — the M32 placeholder, the register's known
/// deferral, not a drift). The walk-through **never** re-implements a write
/// lane (M30·6, the M22 D3 "rides frozen lanes" pin, admin-scope).
/// </para>
/// <para>
/// **Non-blocking (M30·5):** sign-in is never gated; the "is completion
/// stamped?" read is the <see cref="IAdminOnboardingService.GetAsync"/>
/// best-effort read (never a claim — the thin-token rule, ADR 0001-B); the
/// U05 admin banner partial reads the <b>same</b> read seam, so the banner
/// + the view resolve to the <b>same</b> value (M30·5 — the read is the
/// single seam the banner + the view call). The <c>[Authorize(Roles =
/// GlobalAdmin)]</c> here is the standard <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>
/// gate (the surface is admin-scope, owner = the signed-in
/// <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>), **not** an
/// onboarding gate (M30·3 — the read is a public admin surface, never a new
/// authorization surface, the ADR 0001-B thin-token rule).
/// </para>
/// </summary>
[Route("admin/onboarding")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminOnboardingController(
    IAdminOnboardingService adminOnboarding,
    // The per-request localization read seams — used to render the
    // adminonboarding.flash_done flash in the admin's effective language
    // (the AdminQuietController.FlashAsync / OnboardingController.FlashAsync
    // idiom). Optional (default null) so any test-construction site that
    // builds this controller without the seams keeps compiling and renders
    // the KnownTranslationKeys.EnValues source text (the kw-l floor, ADR
    // 0015 D1); DI always supplies the live ILocalizationService +
    // ITranslationProvider in the app.
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// Resolve an <c>adminonboarding.*</c> kw-l key to the admin's effective
    /// language (the house <see cref="EffectiveLanguageCode.ResolveAsync"/> +
    /// <see cref="ITranslationProvider.GetAsync"/> seam — the same chain the
    /// view's <c>&lt;kw-l&gt;</c> TagHelper uses, so the flash string renders
    /// in the admin's language). Falls back to the
    /// <see cref="KnownTranslationKeys.EnValues"/> source text when the
    /// translation seam is absent (the test-construction floor, ADR 0015 D1
    /// — code is the floor, so an admin never sees a raw key).
    /// </summary>
    private async Task<string> FlashAsync(string key)
    {
        if (localization is null || translationProvider is null)
            return KnownTranslationKeys.EnValues.GetValueOrDefault(key) ?? key;
        return await translationProvider.GetAsync(
            key,
            await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider));
    }

    // ── GET /admin/onboarding — the walk-through page (M30·2, M30·7) ───────

    /// <summary>
    /// <c>GET /admin/onboarding</c> — the guided walk-through. Seeds the view
    /// model with the current <see cref="AdminOnboarding.CompletedAt"/> state
    /// (the <see cref="IAdminOnboardingService.GetAsync"/> read's inverse,
    /// M30·3 — best-effort: a missing store / missing row / read failure
    /// degrades to <c>Completed = false</c>, the floor, the banner shows) +
    /// the closed seven-step list (<see cref="AdminOnboardingViewModel.ClosedSteps"/>
    /// — the M30·7 pin, the M22 D3 "rides frozen lanes" shape, admin-scope).
    /// The U05 <c>_AdminOnboardingBanner</c> partial reads the
    /// <b>same</b> <see cref="IAdminOnboardingService.GetAsync"/> seam, so
    /// the banner + the view resolve to the <b>same</b> value (M30·5).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var completedAt = await adminOnboarding.GetAsync();
        return View(new AdminOnboardingViewModel
        {
            Completed = completedAt is not null,
            Steps     = AdminOnboardingViewModel.ClosedSteps,
        });
    }

    // ── POST /admin/onboarding/complete — the ONE write (M30·4) ────────────

    /// <summary>
    /// <c>POST /admin/onboarding/complete</c> — the <b>one</b> write action
    /// (M30·1, M30·6): stamps <see cref="AdminOnboarding.CompletedAt"/> = now
    /// through <see cref="IAdminOnboardingService.CompleteAsync"/> (the
    /// ADR 0150 / ADR 0050 single audited write-lane shape — exactly one
    /// <c>AccessAudit</c> row, <c>Via = Admin</c>, action
    /// <c>admin_onboarding.complete</c>, <c>TargetKind</c>
    /// "admin-onboarding", the <c>site.save</c> / <c>signup.set-open</c> /
    /// <c>timezone.set-default</c> singleton-toggle shape, M30·4; the lane
    /// **upserts** the singleton — it never creates a second row, M30·2;
    /// **strong consistency** — the new value is live on the very next
    /// <see cref="IAdminOnboardingService.GetAsync"/> / banner read).
    /// Success → a surfaced <c>TempData["info"]</c> (the registered
    /// <c>adminonboarding.flash_done</c> kw-l key, the U05 closed set, M30·6)
    /// + redirect to <c>/admin/onboarding</c> (the
    /// <see cref="AdminSiteController.SaveHome"/> /
    /// <see cref="OnboardingController.Finish"/> shape — the
    /// <c>[ValidateAntiForgeryToken]</c> POST + the
    /// <c>TempData["info"]</c> flash + the
    /// <c>RedirectToAction(nameof(Index))</c> redirect).
    /// </summary>
    [HttpPost("complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete()
    {
        var actor = ActorId(User) ?? string.Empty;
        await adminOnboarding.CompleteAsync(actor);
        TempData["info"] = await FlashAsync("adminonboarding.flash_done");
        return RedirectToAction(nameof(Index));
    }
}
