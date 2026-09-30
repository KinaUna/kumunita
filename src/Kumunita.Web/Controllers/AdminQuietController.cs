using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M20 (ADR 0121, D8) — the <c>/admin/quiet</c> surface: the GlobalAdmin's
/// thin control plane over the §6.4 notification-flush cadence (D6) — how
/// often the durable flush job re-checks the held (deferred) notification
/// emails, in minutes. Mirrors <see cref="AdminSignupController"/> (ADR 0050)
/// + <see cref="AdminTimezoneController"/> (ADR 0019) exactly:
/// <see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>-gated (the
/// <c>AdminController</c> precedent — its constructor is pinned by two
/// Web-layer test harnesses, so the platform-default singletons ride their
/// own controllers), a thin read/set over the <b>one</b> matching
/// <see cref="ILocalizationService"/> seam pair
/// (<see cref="ILocalizationService.GetQuietCheckMinutesAsync"/> /
/// <see cref="ILocalizationService.SetQuietCheckMinutesAsync"/>), and the
/// audit row is the <b>service's</b> (exactly one <c>AccessAudit</c>,
/// <c>Via = Admin</c>, action <c>notification.quiet.cadence</c> — the
/// singleton-toggle shape, the same as <c>signup.set-open</c> /
/// <c>timezone.set-default</c> / <c>dateformat.set-default</c>).
/// <para>
/// <b>The gate (D8, C-M20·6).</b> The GlobalAdmin standing is enforced by
/// the <c>[Authorize(Roles = GlobalAdmin)]</c> boundary (the ADR 0050 split —
/// the Web boundary owns the role check; the Core seam does not re-check
/// <c>User</c>); the seam validates the value (a floor of <c>5</c> minutes
/// and a ceiling of <c>1440</c>) and appends exactly one
/// <c>AccessAudit</c> row on a valid write (the one-audit-row pin lives in
/// Core.Tests, U04's GATE-6). A rejected value throws
/// <see cref="ArgumentOutOfRangeException"/> before any write, and this
/// surface surfaces it (the <c>TempData["error"]</c> shape) — no audit row
/// for the blocked attempt (the <c>RemoveLanguageAsync</c> M·7 pin shape).
/// </para>
/// <para>
/// <b>The flash (the <c>admin.quiet.flash_saved</c> /
/// <c>admin.quiet.flash_error</c> keys, U06's closed set).</b> The flash
/// strings resolve through the house
/// <see cref="EffectiveLanguageCode.ResolveAsync"/> +
/// <see cref="ITranslationProvider.GetAsync"/> seam — the same chain the
/// <c>&lt;kw-l&gt;</c> TagHelper resolves in the view (the
/// <see cref="LocaleController.FlashAsync"/> /
/// <see cref="AdminPortabilityController"/>'s <c>T()</c> idiom) — so the
/// admin sees the confirmation in their effective language, with the
/// <see cref="KnownTranslationKeys.EnValues"/> source text as the floor when
/// the seam is absent (a test-construction site).
/// </para>
/// </summary>
[Route("admin/quiet")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminQuietController(
    ILocalizationService localization,
    // The per-request translation read seam (the flash-message resolution,
    // the LocaleController.FlashAsync / AdminPortabilityController.T
    // idiom). Optional (default null) so a test-construction site that
    // builds this controller without the provider keeps compiling and
    // resolves the flash to the KnownTranslationKeys.EnValues floor; DI
    // always supplies the live ITranslationProvider in the app.
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// Resolve a <c>admin.quiet.*</c> kw-l key to the operator's effective
    /// language (the house <see cref="EffectiveLanguageCode.ResolveAsync"/>
    /// + <see cref="ITranslationProvider.GetAsync"/> seam — the same chain
    /// the view's <c>&lt;kw-l&gt;</c> TagHelper uses, so the flash string
    /// renders in the operator's language). Falls back to the
    /// <see cref="KnownTranslationKeys.EnValues"/> source text when the
    /// translation seam is absent (the test-construction floor, ADR 0015 D1
    /// — code is the floor, so a resident/admin never sees a raw key).
    /// </summary>
    private async Task<string> FlashAsync(string key)
    {
        return translationProvider is null
            ? KnownTranslationKeys.EnValues.GetValueOrDefault(key) ?? key
            : await translationProvider.GetAsync(
                key,
                await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider));
    }

    /// <summary>
    /// <c>GET /admin/quiet</c> — the cadence control plane. Seeds the form
    /// with the current cadence (<see cref="ILocalizationService.GetQuietCheckMinutesAsync"/>
    /// — the C-M20·6 floor: a missing row reads as the default <c>60</c>,
    /// never an error).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new CadenceAdminViewModel
        {
            Minutes = await localization.GetQuietCheckMinutesAsync()
        };
        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/quiet</c> — sets the cadence. Delegates to
    /// <see cref="ILocalizationService.SetQuietCheckMinutesAsync"/> (the
    /// single audited write lane — exactly one <c>AccessAudit</c> row,
    /// <c>Via = Admin</c>, action <c>notification.quiet.cadence</c>,
    /// <c>TargetId</c> = the minutes, C-M20·6). The seam validates 5–1440
    /// (<see cref="ArgumentOutOfRangeException"/> out of range, thrown
    /// <b>before</b> any write → no audit row for the blocked attempt) and
    /// owns the audit; this surface owns only the GlobalAdmin standing check
    /// (the <c>[Authorize]</c> gate, ADR 0050 split). Success → a surfaced
    /// <c>TempData["info"]</c> (the registered <c>admin.quiet.flash_saved</c>
    /// kw-l key, U06's set) + redirect (the change is live on the very next
    /// flush tick — data, not config, D6). Out of range →
    /// <c>TempData["error"]</c> (the registered <c>admin.quiet.flash_error</c>
    /// kw-l key) + redirect, <b>no</b> success flash.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int minutes)
    {
        try
        {
            await localization.SetQuietCheckMinutesAsync(
                minutes, ActorId(User) ?? string.Empty);
            TempData["info"] = await FlashAsync("admin.quiet.flash_saved");
        }
        catch (ArgumentOutOfRangeException)
        {
            // The seam's 5-1440 validation (C-M20·6) — surfaced as a
            // plain-English error flash (the 95-place TempData["error"]
            // convention across the Web controllers; the success flash is
            // the registered admin.quiet.flash_saved kw-l key above). The
            // Web boundary adds no second audit (the one-audit-row pin
            // lives in Core.Tests, U04's GATE-6).
            TempData["error"] = "The cadence must be between 5 and 1440 minutes.";
        }
        return RedirectToAction(nameof(Index));
    }

    // ── View model (public nested type so the Razor view can bind to it) ──

    public sealed class CadenceAdminViewModel
    {
        public int Minutes { get; init; } = 60;
    }
}
