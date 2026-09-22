using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/dateformat</c> surface (ADR 0020) — the GlobalAdmin's thin
/// control plane over the <b>platform default</b> date-time format. Mirrors
/// <see cref="AdminTimezoneController"/> / its <c>Save</c> action exactly:
/// <see cref="Roles.GlobalAdmin"/>-gated (the <c>AdminController</c> precedent),
/// a thin wrapper over the <b>one</b> matching
/// <see cref="ILocalizationService"/> method (<see
/// cref="ILocalizationService.SetDefaultDateFormatAsync"/>), and the audit row
/// is the **service's** (exactly one <c>AccessAudit</c>, <c>Via = Admin</c>,
/// action <c>dateformat.set-default</c>, <c>TargetKind</c> "dateformat",
/// <c>TargetId</c> = the format string — the <c>timezone.set-default</c> pin).
/// <para>
/// <b>Presets + custom.</b> The picker offers the curated
/// <see cref="DateFormat.Presets"/> (a (label, format-string) list) <i>and</i> a
/// "Custom…" option that reveals a free-text box for a .NET custom datetime
/// format string (the admin's power-user path — the value stored is the format
/// string <i>itself</i>, so a custom format is just another value, not a special
/// case). The same picker shape is reused on the resident settings surface
/// (<c>/settings/dateformat</c> in <see cref="LocaleController"/>), so the two
/// share one convention (the <c>/admin/timezone</c> / <c>/settings/timezone</c>
/// share the same).
/// </para>
/// <para>
/// <b>Fail-closed.</b> A blank or unusable format string throws
/// <c>InvalidOperationException</c> in the service <b>before</b> any write
/// (no audit row for the blocked attempt — the <c>RemoveLanguageAsync</c> /
/// M·7 pin); the controller maps it to <b>409</b> (the optimistic-concurrency
/// story, the <c>Remove</c> precedent) + a surfaced message.
/// </para>
/// </summary>
[Route("admin/dateformat")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminDateFormatController(
    ILocalizationService localization) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/dateformat</c> — the platform-default picker. Seeds the
    /// picker with the curated <see cref="DateFormat.Presets"/> (the shared
    /// (label, format-string) source) and the current default (<see
    /// cref="ILocalizationService.GetDefaultDateFormatAsync"/>, the
    /// <see cref="DateFormat.FloorFormat"/> floor) as the pre-selected value
    /// (a preset, or a "custom" value when the current default isn't one of
    /// them).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        string current = await localization.GetDefaultDateFormatAsync();

        var model = new DateFormatAdminViewModel
        {
            Presets = DateFormat.Presets
                .Select(p => (p.Label, p.Format))
                .ToList(),
            CurrentFormat = current,
            // A preset (Match returns the preset) → no custom box; a custom
            // format string (Match returns null) → the view reveals the box.
            CurrentIsPreset = DateFormat.Match(current) is not null,
        };

        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/dateformat</c> — sets the platform default. The form
    /// posts either a preset's format string (<c>format</c>) or a custom one
    /// (<c>customFormat</c>); the effective value is the custom string when it
    /// is present, else the preset. Delegates to <see
    /// cref="ILocalizationService.SetDefaultDateFormatAsync"/> (the single
    /// audited write lane). A blank/unusable string → the service's
    /// <c>InvalidOperationException</c> (fail-closed, **no audit row**) →
    /// mapped to **409** + a surfaced error (the <c>Remove</c> precedent).
    /// Success → a surfaced <c>info</c> + redirect (the change is live on the
    /// very next <c>GetDefaultDateFormatAsync</c> / render, M·4: data, not
    /// config).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string? format, string? customFormat)
    {
        var actor = ActorId(User) ?? string.Empty;
        // The select posts the preset's format string as `format`; when "Custom…"
        // is chosen it posts `customFormat` = the free-text value (and `format`
        // = "custom"). A blank/`"custom"` `customFormat` means "use the preset".
        bool hasCustom = !string.IsNullOrWhiteSpace(customFormat) && customFormat != "custom";
        // A preset select posts `format` = the preset string; the "Custom…"
        // select posts `format` = "custom" (a sentinel, never a valid format) +
        // `customFormat` = the free-text value. A blank/`"custom"` `customFormat`
        // means "use the preset". Null-safe: if neither yields a value the
        // service's fail-closed validation throws (→ 409).
        string? candidate = hasCustom ? customFormat : format;
        if (string.IsNullOrWhiteSpace(candidate) || candidate == "custom")
            candidate = format;
        string chosen = candidate ?? string.Empty;

        try
        {
            await localization.SetDefaultDateFormatAsync(chosen, actor);
            TempData["info"] = $"Platform default date & time format set to “{chosen}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            // M·7 / fail-closed: the service rejected the format string (blank or
            // unusable) before any write — no audit row was committed for the
            // blocked attempt. Surface it as 409 (the optimistic-concurrency
            // story) + a message the admin can act on.
            TempData["error"] = ex.Message;
            return StatusCode(409);
        }
    }

    // ── View model (public nested type so the Razor view can bind to it) ──

    public sealed class DateFormatAdminViewModel
    {
        public List<(string Label, string Format)> Presets { get; init; } = new();

        /// <summary>The platform default format string (a preset, or a custom
        /// value when it isn't one of <see cref="Presets"/>).</summary>
        public string CurrentFormat { get; init; } = DateFormat.FloorFormat;

        /// <summary>Whether <see cref="CurrentFormat"/> is a preset (true) or a
        /// custom format string (false) — the view uses this to reveal the
        /// "Custom…" text box instead of a lambda in the Razor markup.</summary>
        public bool CurrentIsPreset { get; init; } = true;
    }
}
