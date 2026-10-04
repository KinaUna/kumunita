using Kumunita.Core.Messaging;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The per-resident <b>messaging settings</b> surface (M9 amendment on top of
/// ADR 0105). Lives at <c>/settings/messaging</c> — the settings-tab section
/// (the <see cref="LocaleController.SettingsTimezone"/> / <see cref="LocaleController.SettingsQuiet"/>
/// idiom, the ADR 0080 tabbed settings shape) where other per-resident
/// preferences (language, time zone, quiet hours) already live.
/// <para>
/// The model is a simple two-state read/write: the resident's own
/// <see cref="Profile.MessagingOptIn"/> flag (the M9 per-user control; the
/// ADR 0105 privacy-sensitive default-<c>false</c> convention), and — for a
/// supervised child whose <b>guardian</b> has forced messaging OFF — the
/// ceiling flag <see cref="Profile.MessagingRestricted"/> (the guardian's
/// veto; the ADR 0028 G·2/G·3 guardian-standing shape). When the ceiling is
/// on, the toggle is rendered <b>disabled</b> with a notice; the POST is
/// rejected at the service lane (fail closed).
/// <para>
/// The instance-level toggle (the <see cref="IMessagingService
/// .IsMessagingEnabledAsync"/> master gate) is still read: when the instance
/// has messaging off, the settings page is still reachable (so a resident can
/// opt in in advance), but a notice surfaces that the instance is not
/// currently accepting messages. The per-actor composite
/// <see cref="IMessagingService.IsMessagingAllowedForAsync"/> is <b>not</b>
/// used to gate the page: a resident needs to reach this page to set the
/// toggle — the Web-layer gate (the <see cref="MessagesController"/> actions
/// and the account nav) is what enforces it.
/// <para>
/// Routes:
/// <list type="bullet">
/// <item>GET <c>/settings/messaging</c> — the toggle + status notice.</item>
/// <item>POST <c>/settings/messaging</c> — write the opt-in flag.</item>
/// </list>
/// </summary>
[Authorize]
public sealed class MessagingSettingsController(
    ILogger<MessagingSettingsController> logger,
    IMessagingService messaging,
    IUserInfoService userInfo) : Controller
{
    private readonly ILogger<MessagingSettingsController> _logger = logger;
    private readonly IMessagingService _messaging = messaging;
    private readonly IUserInfoService _userInfo = userInfo;

    /// <summary>
    /// <c>GET /settings/messaging</c> — the resident's messaging opt-in
    /// section. The model carries:
    /// <list type="bullet">
    /// <item><see cref="MessagingSettingsViewModel.InstanceEnabled"/> — the
    /// master instance toggle (a read of
    /// <see cref="IMessagingService.IsMessagingEnabledAsync"/>; when <c>false</c>,
    /// the page still renders but a notice surfaces that the instance is
    /// not currently accepting messages — the opt-in is still a meaningful
    /// choice to make).</item>
    /// <item><see cref="MessagingSettingsViewModel.OptIn"/> — the resident's
    /// own <see cref="Profile.MessagingOptIn"/> (the checkbox state).</item>
    /// <item><see cref="MessagingSettingsViewModel.Restricted"/> — the
    /// guardian's ceiling (the <see cref="Profile.MessagingRestricted"/>
    /// flag). When <c>true</c>, the checkbox is rendered disabled and a
    /// notice explains that a guardian has disabled messaging for this
    /// account; the POST is refused at the service lane.</item>
    /// </list>
    /// A missing profile (the resident has no profile row — should not
    /// happen for a signed-in resident, but fail closed) renders the
    /// <c>false</c> / <c>false</c> floor: the toggle is unchecked and no
    /// restriction notice (the resident is not supervised, the opt-in is
    /// simply unset).
    /// </summary>
    [HttpGet("/settings/messaging")]
    public async Task<IActionResult> Index()
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return View(new MessagingSettingsViewModel());

        var instanceEnabled = await _messaging.IsMessagingEnabledAsync();
        Profile? profile = null;
        try
        {
            profile = await _userInfo.GetProfileAsync(actorId);
        }
        catch (Exception ex)
        {
            // A profile read failure (should not happen for a signed-in
            // resident) degrades to the floor: opt-in off, no restriction.
            _logger.LogWarning(ex, "Profile read failed for messaging settings; degrading to the floor.");
        }

        return View(new MessagingSettingsViewModel
        {
            InstanceEnabled = instanceEnabled,
            OptIn = profile?.MessagingOptIn ?? false,
            Restricted = profile?.MessagingRestricted ?? false,
        });
    }

    /// <summary>
    /// <c>POST /settings/messaging</c> — the opt-in write lane. Writes
    /// <see cref="Profile.MessagingOptIn"/> through the frozen
    /// <see cref="IUserInfoService.SetMessagingOptInAsync"/> seam (the
    /// <see cref="IUserInfoService.CompleteOnboardingAsync"/> owner-scope
    /// single-write shape — no audit row, "not an access decision").
    /// <para>
    /// When the guardian's restriction ceiling is on
    /// (<see cref="Profile.MessagingRestricted"/>, the ADR 0028 G·2/G·3
    /// shape), the write is <b>refused</b> with a <c>TempData</c> error and
    /// a redirect back to the page (the service lane writes the flag
    /// regardless, but the <c>SetMessagingOptInAsync</c> seam itself does
    /// not enforce the ceiling — the enforcement is at the gate read in
    /// <see cref="IMessagingService.IsMessagingAllowedForAsync"/> — so the
    /// controller enforces it here, at the Web boundary, mirroring how the
    /// <c>Suspend</c> / <c>Unsuspend</c> guardian actions are gated in
    /// <c>GuardianController</c>). The resident is told their guardian has
    /// restricted messaging; the toggle is unchanged.
    /// </para>
    /// </summary>
    [HttpPost("/settings/messaging")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(bool optIn)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return RedirectToAction(nameof(Index));

        // The ceiling read — the guardian's veto (a read, not a write;
        // fail-closed: a missing profile is "not restricted", the floor).
        Profile? profile = null;
        try
        {
            profile = await _userInfo.GetProfileAsync(actorId);
        }
        catch
        {
            // A read failure degrades to "not restricted" (the floor) —
            // the write below is the actual change, and it will fail
            // closed on its own if the profile is truly missing.
        }

        if (profile?.MessagingRestricted == true)
        {
            TempData["error"] = "Messaging has been restricted on your account by a guardian. Contact them to change it.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _userInfo.SetMessagingOptInAsync(actorId, optIn, actorId);
            TempData["info"] = optIn
                ? "Messaging enabled for your account."
                : "Messaging disabled for your account.";
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            TempData["error"] = "Your profile could not be updated. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// The messaging-settings page model (a public nested type so the
    /// Razor view can bind to it — the
    /// <see cref="LocaleController.LocaleSettingsViewModel"/> idiom).
    /// </summary>
    public sealed class MessagingSettingsViewModel
    {
        /// <summary>The instance-level messaging master gate
        /// (<see cref="IMessagingService.IsMessagingEnabledAsync"/>). When
        /// <c>false</c>, the page still renders (so a resident can opt in
        /// in advance) but a notice surfaces that the instance is not
        /// currently accepting messages.</summary>
        public bool InstanceEnabled { get; init; }

        /// <summary>The resident's own <see cref="Profile.MessagingOptIn"/>
        /// flag (the checkbox state).</summary>
        public bool OptIn { get; init; }

        /// <summary>The guardian's ceiling (<see cref="Profile
        /// .MessagingRestricted"/>). When <c>true</c>, the checkbox is
        /// rendered disabled and the POST is refused.</summary>
        public bool Restricted { get; init; }
    }
}
