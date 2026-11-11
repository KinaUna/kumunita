using Kumunita.Web.Security;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The resident-facing identity surface (M1 step 8): signup → verify → login, sign-out,
/// the <c>AccessDeniedPath</c> target, and the M1 profile-bootstrap page.
/// <para>
/// Cookie auth is already wired in <c>Program.cs</c>; <see cref="KumunitaClaimsPrincipalFactory"/>
/// (step 6) is the only place that mints the admissible claim set (<see cref="ClaimTypes.All"/>),
/// and <see cref="IIdentityService"/> (Core, step 6) owns the signup/verify semantics. This
/// controller only shapes HTTP and calls the frozen seams.
/// </para>
/// <para>
/// Unverified accounts cannot sign in: <see cref="KumunitaClaimsPrincipalFactory"/> omits the
/// <see cref="Roles.Member"/> role while <c>Profile.Verified</c> is false, and
/// <see cref="SignInManager{TUser}"/>'s lockout/verification checks hold the login itself —
/// no extra middleware needed.
/// </para>
/// </summary>
// C3-05 (ADR 0295) — the document-store ctor param is gone. The one direct
// read this controller used to perform itself (the verify-link token load) is
// now the IdentityModule's <c>LoadVerifyTokenAsync</c> seam (the
// <see cref="IIdentityService"/> owns the <c>IdentityToken</c> document), so
// the controller retires its Marten session and its allowlist entry.
public sealed class AccountController(
    SignInManager<User> signInManager,
    UserManager<User> userManager,
    IIdentityService identity,
    IUserInfoService userInfo,
    // ADR 0301 D2 (IMPROVE-UX R13) — the self-service deletion SCHEDULING seam
    // (R11's IDeletionScheduler, DI-registered in the Identity module): the
    // self-serve lane now SCHEDULES the deletion (the R12 due-job executes it
    // after the grace period) — it no longer calls
    // IIdentityService.DeleteAccountAsync directly (ADR 0301 D4: that seam's
    // contract stands, the grace period lives above it). An OPTIONAL param
    // (the `EffectiveTimezoneResolver? = null` / `IUserInfoService` harness
    // idiom used across this repo's controllers) so the existing test harnesses
    // that construct this controller with the four original args keep compiling;
    // DI always supplies the live scheduler in the app, and the R13 Delete
    // tests pass an NSubstitute explicitly.
    IDeletionScheduler? scheduler = null,
    // ADR 0304 (IMPROVE-UX A08) — the email invitation seam (A07's Core
    // service, DI-registered in the Identity module): the `?invite=` gate
    // opens the self-service surface while the ADR 0050 gate is closed and
    // runs the AcceptPresentedAsync write (RegisterAsync + the Accepted mark
    // + the invitation.accept audit row). OPTIONAL (the `IDeletionScheduler?`
    // harness idiom) so the existing direct-construction harnesses keep
    // compiling; DI always supplies it, and the A08 tests pass an
    // NSubstitute. When null AND a token is presented, the gate decides
    // (fail-closed — no account is created on an unresolvable seam; the
    // SignupClosed floor holds, which is the ADR 0050 invariant).
    IInvitationService? invitationService = null) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        user.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value;

    // ── IMPROVE-UX A12 — the resident "Invite a neighbour" surface ────────
    // A11 settled the mode (Off / On / OnWithApproval) + the rule
    // (GetResidentInvitePostureAsync); A12 settles the *resident surface*
    // (the GET /account/invite + the POST /account/invite that creates the
    // row in the mode's initialStatus: Pending when the mode is On,
    // PendingApproval when the mode is OnWithApproval — the ADR 0304 D6 c
    // row state). The invitation seam (IInvitationService, A07's Core
    // service) is the same one the A08 sign-up gate uses (the
    // AccountController ctor already carries it for the A08 ?invite= gate)
    // — the A12 Invite actions read the A11 rule + the A12 CreateAsync
    // initialStatus param. The admin approval queue (the /admin/invitations
    // Approve button) is a separate surface (the
    // AdminInvitationsController.Approve POST — the A12 admin lane).

    // ADR 0215 — the MFA challenge's model errors are localized from the registry
    // (account.mfa.error.*), resolved from the request scope like LocalizeValidationAsync;
    // the English text is the floor when there is no provider (unit-test harness) or
    // the lookup fails.
    private async Task<string> MfaTextAsync(string key, string english)
    {
        var services = HttpContext?.RequestServices;
        var provider = services?.GetService<Kumunita.Core.Localization.ITranslationProvider>();
        if (services is null || provider is null)
        {
            return english;
        }
        try
        {
            var localization = services.GetService<Kumunita.Core.Localization.ILocalizationService>();
            var lang = await Kumunita.Web.Security.EffectiveLanguageCode.ResolveAsync(
                HttpContext?.Request, localization, provider);
            var text = await provider.GetAsync(key, lang);
            return string.IsNullOrWhiteSpace(text) ? english : text;
        }
        catch
        {
            return english;
        }
    }

    // P1-5 (translation audit) — localize the four Account forms' DataAnnotations
    // messages. The platform's closed translation surface is the KnownTranslationKeys
    // registry (ADR 0015); the four account.err.* keys live there (en/de/fr/da).
    // Resolved from the request scope (the Storage() idiom) so the constructor is
    // unchanged for the existing harnesses. A no-op on a non-HTTP render (null
    // provider) or an unmapped attribute.
    private async Task LocalizeValidationAsync(object model)
    {
        // In unit tests (and any non-HTTP host) RequestServices is absent —
        // skip localization rather than throw on a null provider.
        var services = HttpContext?.RequestServices;
        var request = HttpContext?.Request;
        if (services is null)
        {
            return;
        }
        var localization = services.GetService<Kumunita.Core.Localization.ILocalizationService>();
        var provider = services.GetService<Kumunita.Core.Localization.ITranslationProvider>();
        if (provider is null)
        {
            return;
        }
        await Kumunita.Web.Localization.AccountValidationLocalizer.ApplyAsync(
            model, ModelState, request, localization, provider);
    }

    // ── My storage (M25 U7 — the self-only resident usage read) ────────────────────────

    /// <summary>
    /// <c>GET /account/storage</c> — the resident's **own** storage usage (M25
    /// U7, C-UP·4/F7): their own usage bytes, the community per-user quota (or
    /// "unlimited"), and how much of it remains. **Self-only** — the subject is
    /// the signed-in principal (<see cref="SubjectId(System.Security.Claims.ClaimsPrincipal)"/>),
    /// minted server-side and never taken from a route param, so a resident can
    /// never read another resident's numbers.
    /// <para>
    /// <b>Read-only</b> — no write lane here (the admin settings surface, U5/U6,
    /// is the only writer), and it emits **no** <c>AccessAudit</c> row
    /// (C-UP·7: a resident's own usage is not an audience-restricted read).
    /// The seam (<see cref="Kumunita.Core.Usage.IStorageSettingsService"/>) is
    /// resolved from the request scope — the <see cref="Verify(string)"/> idiom —
    /// so the controller constructor is unchanged. The usage read
    /// (<c>GetPerUserUsageBytesAsync</c>) reuses the M24 C-SM·7
    /// <c>Σ SizeBytes WHERE CreatedById</c> seam (U4); the quota comes from
    /// <c>GetOrCreateAsync</c> (the create-if-missing sentinel). Sentinel
    /// (C-UP·5): a quota of <c>0</c> ⇒ **unlimited** — rendered as "Unlimited",
    /// not <c>0</c> remaining and not an error.
    /// </para>
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Storage()
    {
        // Self-only (C-UP·4/F7): the subject is the signed-in principal —
        // never a path param.
        var subject = SubjectId(User);
        if (subject is null)
            return Challenge();

        // Resolve the read seam from the request scope (the Verify idiom) — keeps
        // the controller constructor untouched for the existing harnesses.
        var settings = HttpContext.RequestServices
            .GetRequiredService<Kumunita.Core.Usage.IStorageSettingsService>();

        // Two independent reads (quota doc + own usage): start both, then await
        // each for its value (the AdminStorageController.Index shape).
        var quotaTask  = settings.GetOrCreateAsync(CancellationToken.None);
        var usageTask  = settings.GetPerUserUsageBytesAsync(subject, CancellationToken.None);
        var quota      = await quotaTask;
        long usage      = await usageTask;

        bool unlimited = quota.PerUserQuotaBytes == 0;      // C-UP·5 sentinel
        var remaining  = unlimited ? (long?)null
                                   : Math.Max(0, quota.PerUserQuotaBytes - usage);

        return View(new ResidentStorageViewModel
        {
            MyUsageBytes      = usage,
            PerUserQuotaBytes = quota.PerUserQuotaBytes,
            RemainingBytes    = remaining,                  // null ⇒ unlimited
            QuotaUnlimited    = unlimited,
            MyUsageHuman      = ResidentStorageViewModel.FormatBytes(usage),
            QuotaHuman        = unlimited ? "Unlimited" : ResidentStorageViewModel.FormatBytes(quota.PerUserQuotaBytes),
            RemainingHuman    = unlimited ? "Unlimited" : ResidentStorageViewModel.FormatBytes(remaining!.Value)
        });
    }

    // ── IMPROVE-UX A12 — the resident "Invite a neighbour" surface ────────
    // The A11 rule (GetResidentInvitePostureAsync) decides whether the
    // surface is visible (MayInvite = true) or hidden (MayInvite = false —
    // the Off floor). When visible, the POST creates the invitation row in
    // the mode's initialStatus: Pending when RequiresApproval = false
    // (the On mode), PendingApproval when RequiresApproval = true (the
    // OnWithApproval mode — the ADR 0304 D6 c row state). The ADR 0304
    // D4 invariants (single-use, expiring, revocable) are unchanged.

    /// <summary>
    /// <c>GET /account/invite</c> (IMPROVE-UX A12, ADR 0304 D6 c) — the
    /// resident's "Invite a neighbour" form. The A11 rule
    /// (<see cref="IInvitationService.GetResidentInvitePostureAsync"/>)
    /// decides whether the surface is **visible**
    /// (<see cref="ResidentInvitePosture.MayInvite"/> = <c>true</c> — the
    /// <c>On</c> / <c>OnWithApproval</c> modes) or **hidden**
    /// (<see cref="ResidentInvitePosture.MayInvite"/> = <c>false</c> —
    /// the <c>Off</c> floor, the surface is absent, not a 403). The
    /// subject is the signed-in principal (self-only, never a path
    /// param). The <see cref="ResidentInvitePosture.RequiresApproval"/>
    /// flag is carried on the view model (the view renders the
    /// "pending approval" notice when <c>true</c> — the A12
    /// <c>OnWithApproval</c> mode's "your invitation is pending
    /// approval" text, the ADR 0304 D6 c shape). The ADR 0304 D6(a)
    /// expiry default (7 days) is the A09 call-site's default
    /// (<see cref="AdminInvitationsController.DefaultExpiryDays"/>).
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Invite([FromQuery] string? email = null)
    {
        var subject = SubjectId(User);
        if (subject is null)
            return Challenge();

        // Fail-closed (the A08 "null AND a token is presented, the gate
        // decides" idiom): the invitation seam is the A12 surface's whole
        // contract (the A11 rule + the CreateAsync initialStatus lane). A
        // null seam (the A09 harness shape) is a refusal, not a silent
        // create (the C3 no-silent-missing-email floor).
        if (invitationService is null)
            return Challenge();
        var invites = invitationService;   // the non-null local (the null guard above)

        // The A11 rule (the A12 surface's gate — the MayInvite /
        // RequiresApproval posture). The service does not re-check User
        // (the ADR 0050 split — the [Authorize] boundary is the access
        // decision; the rule is the resident-invite-mode read).
        var posture = await invites.GetResidentInvitePostureAsync(subject);

        return View(new InviteViewModel
        {
            MayInvite          = posture.MayInvite,
            RequiresApproval   = posture.RequiresApproval,
            Email              = email ?? string.Empty,
        });
    }

    /// <summary>
    /// <c>POST /account/invite</c> (IMPROVE-UX A12, ADR 0304 D6 c) —
    /// create the invitation for <paramref name="email"/>. The A11 rule
    /// is re-read (the gate — <see cref
    /// "ResidentInvitePosture.MayInvite"/> must be <c>true</c> for the
    /// create to proceed; a <c>false</c> posture is a refusal flash, not
    /// a 403). The row's initial status is
    /// <see cref="EmailInvitationStatus.PendingApproval"/> when
    /// <see cref="ResidentInvitePosture.RequiresApproval"/> is
    /// <c>true</c> (the <c>OnWithApproval</c> mode — the row is gated
    /// behind the admin approval queue) or
    /// <see cref="EmailInvitationStatus.Pending"/> when
    /// <see cref="ResidentInvitePosture.RequiresApproval"/> is
    /// <c>false</c> (the <c>On</c> mode — the row is immediately usable).
    /// The ADR 0304 D4 invariants are unchanged (the
    /// <see cref="IInvitationService.CreateAsync"/> seam owns the
    /// token-hash-at-rest, the single-use pin, the expiring contract).
    /// The <c>invitation.create</c> audit row is the service's
    /// (<c>Via = Owner</c>, <c>TargetKind = "invitation"</c> — the
    /// ADR 0304 D3 shape, the A07 contract). The A12
    /// <c>invitation.approved</c> nudge (the
    /// <see cref="IInvitationService.ApproveAsync"/> seam) is the
    /// admin-lane's — this surface does not emit it.
    /// </summary>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(InviteViewModel model)
    {
        var subject = SubjectId(User);
        if (subject is null)
            return Challenge();

        // Fail-closed (the A08 "null AND a token is presented, the gate
        // decides" idiom): the invitation seam is the A12 surface's whole
        // contract (the A11 rule + the InviteAsync initialStatus lane). A
        // null seam (the A09 harness shape) is a refusal, not a silent
        // create (the C3 no-silent-missing-email floor).
        if (invitationService is null)
            return Challenge();
        var invites = invitationService;   // the non-null local (the null guard above)

        // The A11 rule (the gate — re-read on the POST, not just the GET;
        // the mode may have changed between the GET and the POST).
        var posture = await invites.GetResidentInvitePostureAsync(subject);
        if (!posture.MayInvite)
        {
            TempData["error"] = await this.FlashAsync(
                "account.invite_off",
                "Resident invitations are not enabled for this community. Please contact a GlobalAdmin.");
            return RedirectToAction(nameof(Invite));
        }

        // The email shape check (the A09 IsPlausibleEmail shape — a
        // local@domain with a dot in the domain; no regex dependency).
        var email = (model.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsPlausibleEmail(email))
        {
            TempData["error"] = await this.FlashAsync(
                "account.invite_invalid_email",
                "Not a valid email address.");
            return RedirectToAction(nameof(Invite), new { Email = model.Email });
        }

        // The ADR 0304 D6(a) expiry default (7 days) — the A09 call-site's
        // default (the ADR "ships no default — the A09/A12 surfaces will
        // set it; the proposed default is 7 days"; this call site settles
        // it for the resident surface, the A09 AdminInvitationsController
        // shape).
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        // The initialStatus (the A12 mode's row state — the ADR 0304 D6 c
        // "approval state"): PendingApproval when the mode is
        // OnWithApproval (the RequiresApproval = true flag, the A11 rule's
        // output), Pending when the mode is On (the RequiresApproval =
        // false flag). The ADR 0304 D4 invariants are unchanged.
        var initialStatus = posture.RequiresApproval
            ? EmailInvitationStatus.PendingApproval
            : EmailInvitationStatus.Pending;

        // The resident's display name (the email's {0}) — the service
        // floors to "there" when null/blank (the
        // BuildVerificationEmailAsync shape). Read through the request
        // services (no ctor change, the AdminInvitationsController
        // ActorDisplayNameAsync shape).
        string inviterDisplayName;
        try
        {
            inviterDisplayName = (await userInfo.GetProfileAsync(subject))?.DisplayName ?? string.Empty;
        }
        catch
        {
            inviterDisplayName = string.Empty;   // fail-open (the email lane is best-effort on the name)
        }

        // The A09 InviteAsync delivery seam (create + stage the invitation
        // email in one transaction — the C3 no-silent-missing-email
        // guarantee). The initialStatus param (the A12 mode's row state)
        // is the last named argument (the A07/A08/A09 contract is
        // unchanged — the A09 call sites use the default Pending).
        await invites.InviteAsync(
            email, subject, inviterDisplayName, expiresAt,
            initialStatus: initialStatus);

        // The flash names the mode (the A12 "your invitation is pending
        // approval" notice when OnWithApproval; the simple "sent" when On).
        if (posture.RequiresApproval)
        {
            TempData["info"] = await this.FlashAsync(
                "account.invite_pending_approval",
                "Your invitation for {0} is pending approval. A GlobalAdmin will review it.",
                email);
        }
        else
        {
            TempData["info"] = await this.FlashAsync(
                "account.invite_success",
                "Invitation sent to {0}.",
                email);
        }
        return RedirectToAction(nameof(Invite));
    }

    /// <summary>A shape check for an invited address (the A09
    /// <see cref="AdminInvitationsController.IsPlausibleEmail"/> shape — a
    /// <c>local@domain</c> with a dot in the domain; no regex dependency,
    /// the house "no new package" rule).</summary>
    private static bool IsPlausibleEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var at = email.LastIndexOf('@');
        if (at <= 0 || at == email.Length - 1) return false;
        var local = email[..at];
        var domain = email[(at + 1)..];
        if (local.Length == 0 || local.Contains(" ")) return false;
        if (domain.Length < 3 || !domain.Contains('.')) return false;
        if (domain.StartsWith('.') || domain.EndsWith('.')) return false;
        return !domain.Contains("..");
    }

    // ── Change password (self-serve; ADR 0138) ───────────────────────────────
    // The resident's self-serve change-password surface moved to the Security
    // tab 2026-10-31 (the ADR 0138 surface, now co-located with MFA — one tab
    // holds both credential-security options). It is the
    // SecuritySettingsController.ChangePassword action (POST
    // /settings/security/password) + the _PasswordChange partial rendered on
    // the Mfa / MfaLocked views. The former /account/password GET + POST
    // actions and their Views/Account/ChangePassword*.cshtml views are removed.

    // ── Delete account (self-serve; ADR 0301 grace period, amending ADR 0142 D5) ──

    /// <summary>
    /// ADR 0301 D2 (IMPROVE-UX R13) — the pending <see cref="DeletionRequest"/>
    /// read for the signed-in resident, resolved from the request scope (the
    /// <see cref="Storage()"/> <c>GetRequiredService</c> idiom — this controller
    /// keeps the C3-05 "no IDocumentStore ctor param" invariant). A
    /// <see cref="DeletionRequestStatus.Pending"/> row is the scheduled state
    /// (the view renders the "deletion scheduled for {date}" notice + a Cancel
    /// button); a missing row, or a <see cref="DeletionRequestStatus
    /// .Cancelled"/> / <see cref="DeletionRequestStatus.Executed"/> row, is
    /// <c>null</c> (the schedule form renders — a re-schedule is a fresh act,
    /// ADR 0301 D2). The read is unaudited (the <c>QuerySession</c>
    /// convention); the request is the signed-in principal (self-only, never a
    /// path param).
    /// </summary>
    private async Task<DeletionRequest?> LoadPendingDeletionAsync(string subjectId)
    {
        var services = HttpContext?.RequestServices;
        if (services is null)
            return null;
        var store = services.GetService<Marten.IDocumentStore>();
        if (store is null)
            return null; // harness shape — the C3-05 "no store" idiom
        await using var session = store.QuerySession();
        var row = await session.LoadAsync<DeletionRequest>(subjectId);
        return row is { Status: DeletionRequestStatus.Pending } ? row : null;
    }

    /// <summary>
    /// ADR 0019 (IMPROVE-UX R13) — render the scheduled <see cref
    /// "DeletionRequest.DueAt"/> in the resident's effective time zone (their
    /// zone when the resolver is available — the app shape — the <c>UTC</c>
    /// floor in the harness shape, where <c>EffectiveTimezoneResolver</c> is
    /// not registered). The format follows the current culture (the
    /// resident's effective language drives the UI strings; the date glyph
    /// set is the platform's, the ADR 0020 floor).
    /// </summary>
    private async Task<string> FormatDueAtAsync(DateTimeOffset dueAt)
    {
        var services = HttpContext?.RequestServices;
        var timezone = services?.GetService<Kumunita.Web.Localization.EffectiveTimezoneResolver>();
        var zone = timezone is not null ? await timezone.GetAsync()
                                        : System.TimeZoneInfo.Utc;
        return TimeZoneInfo.ConvertTime(dueAt, zone).ToString(
            System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.LongDatePattern);
    }

    /// <summary>
    /// <c>GET /account/delete</c> — the resident's self-serve delete-account
    /// form (ADR 0142 D7, now ADR 0301 D1: **every** resident — the ADR 0142
    /// D5 GlobalAdmin gate is removed by ADR 0301 D1). The subject is the
    /// signed-in principal (never a path param). The form asks for the
    /// resident's current password (verified server-side on the POST) and an
    /// explicit acknowledgment checkbox (the dangerous-action guard). The lede
    /// text explains the consequences: the account is removed, the resident's
    /// audit trail is pseudonymized (their identity replaced by a tombstone),
    /// and their group/community memberships are removed — the privacy lane
    /// OPS.md §9 / ARCHITECTURE.md §5 prescribe.
    /// <para>
    /// **ADR 0301 D2 — the grace period:** when the resident already has a
    /// pending <see cref="DeletionRequest"/> row, the form is replaced by the
    /// "deletion scheduled for {date} — sign in to cancel" notice + a Cancel
    /// button (the ADR 0301 D2 "no extra button, no extra form" — the Cancel
    /// button is the explicit lane the R13 prompt calls for; the ADR defines
    /// the *rule*, this surface renders it). The <see cref
    /// "DeleteAccountViewModel.IsScheduled"/> / <see cref
    /// "DeleteAccountViewModel.ScheduledFor"/> flags carry the state.
    /// <para>
    /// **ADR 0301 D3 — the child rule:** a resident under an active
    /// <c>GuardianLink</c> cannot self-delete (the R11 seam enforces this at
    /// scheduling time — the <see cref="ScheduleDeletionAsync"/> seam throws
    /// <see cref="UnauthorizedAccessException"/>). The GET does not pre-check
    /// (the read shape keeps this surface a single seam); the POST surfaces
    /// the distinct "contact a parent/guardian" notice (the
    /// <see cref="DeleteAccountViewModel.IsChildRefused"/> flag) — the ADR
    /// 0301 D3 "the same shape as 0142 D5's old <c>SelfDeletionRefused</c>,
    /// with a different reason".
    /// </para>
    /// </summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Delete()
    {
        // The subject must be the signed-in principal — the [Authorize]
        // attribute above guarantees that (an unauthenticated request is
        // redirected to /Account/Login by the cookie handler's
        // AccessDeniedPath), so this method only runs for an authenticated
        // resident. A defensive null-check mirrors the Storage() /
        // ChangePassword() shape.
        var subject = SubjectId(User);
        if (subject is null)
            return Challenge();

        // ADR 0301 D2 — the pending DeletionRequest read (self-only; the
        // QuerySession convention). A Pending row renders the scheduled
        // state; otherwise the schedule form renders (the ADR 0142 D5
        // GlobalAdmin gate is gone — every resident sees the form, ADR
        // 0301 D1).
        var pending = await LoadPendingDeletionAsync(subject);
        var vm = new DeleteAccountViewModel
        {
            IsScheduled   = pending is not null,
            ScheduledFor  = pending is null ? null : await FormatDueAtAsync(pending.DueAt)
        };

        return View(vm);
    }

    /// <summary>
    /// <c>POST /account/delete</c> — the self-serve SCHEDULE lane (ADR 0301
    /// D2 — the ADR 0142 D7 surface shape, now scheduling rather than
    /// executing). <b>The guard is authoritative on the write path too</b>
    /// (the ADR 0138 shape): the resident must have checked the acknowledgment
    /// box AND provided the correct current password — otherwise the form
    /// re-renders with the error (the account is untouched). When both pass,
    /// the <see cref="IDeletionScheduler.ScheduleDeletionAsync"/> seam (R11)
    /// writes the pending <see cref="DeletionRequest"/> row + one
    /// <c>"account.delete.scheduled"</c> audit row (ADR 0301 D1's "one
    /// summary" shape, <c>Via: Owner</c>); the R12 due-job executes the
    /// existing <see cref="IIdentityService.DeleteAccountAsync"/> lane after
    /// the grace period (ADR 0301 D4 — the seam's contract stands, the grace
    /// period lives above it).
    /// <para>
    /// **The two up-front refusals (both before any write — the ADR 0301
    /// D2/D3 fail-closed shape, the R11 seam's guard ordering):**
    /// <list type="bullet">
    /// <item><see cref="InvalidOperationException"/> — the account does not
    /// exist, or the resident is the last <c>GlobalAdmin</c> (the ADR 0142 D3
    /// lockout pin — the R11 seam reuses the guard the
    /// <c>DeleteCoreAsync</c> lane already ran, above the seam).</item>
    /// <item><see cref="UnauthorizedAccessException"/> — the resident is a
    /// child under an active <c>GuardianLink</c> (the ADR 0301 D3 child rule).
    /// The ADR 0142 D5 GlobalAdmin gate that used to throw this exception is
    /// removed (ADR 0301 D1); the catch is now the *distinct* "contact a
    /// parent/guardian" notice (the <see cref
    /// "DeleteAccountViewModel.IsChildRefused"/> flag — the ADR 0301 D3 "the
    /// same shape as 0142 D5's old <c>SelfDeletionRefused</c>, with a
    /// different reason").
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// On success the resident stays signed in (their credential is intact —
    /// the account is only *scheduled*); the GET now renders the scheduled
    /// state (the "deletion scheduled for {date} — sign in to cancel" notice
    /// + the Cancel button). The R14 SEC review (the ADR 0301 D2/D3 surface)
    /// may add a re-auth step here; R13 keeps the existing
    /// <c>CheckPasswordAsync</c> guard as the re-auth (the ADR 0301 D2 "the
    /// act of returning invalidates the pending request" is the cancel — the
    /// re-auth on the POST is the schedule's own proof-of-possession).
    /// </para>
    /// </summary>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(DeleteAccountViewModel model)
    {
        // Self-only (the C-UP·4/F7 shape from Storage()): the subject is
        // the signed-in principal — never a path param.
        var subject = SubjectId(User);
        if (subject is null)
            return Challenge();

        // ADR 0301 D2 — a re-schedule (a pending row already exists) is a
        // fresh act that resets the row (the R11 seam's "one row per
        // resident" invariant, ADR 0301 D2); the GET renders the scheduled
        // state + the Cancel button, so this POST need not pre-read the
        // row (the R11 seam's ScheduleDeletionAsync overwrites the existing
        // row on a re-schedule — the ADR 0301 D2 "a re-schedule is a fresh
        // act" rule). The GET's LoadPendingDeletionAsync read is the single
        // store surface for the scheduled-state notice.

        // The dangerous-action guard: the resident must have checked the
        // acknowledgment box. A bare [Required] on a bool would accept
        // false (the bound value is non-null), so the check is explicit.
        if (!model.Confirmed)
        {
            ModelState.AddModelError(
                nameof(model.Confirmed), await this.FlashAsync("validation.confirm_deletion", "You must confirm that you understand your account will be permanently deleted."));
        }

        // Verify the current password (the self-serve lane proves it is really
        // this resident deleting their own account — not a crafted request).
        // A wrong password is a form error (the account is untouched).
        var user = await userManager.FindByIdAsync(subject);
        if (user is null)
        {
            ModelState.AddModelError(nameof(model.Password), await this.FlashAsync("validation.account_not_found", "We could not find that account — sign in again."));
        }
        else
        {
            var currentOk = await userManager.CheckPasswordAsync(user, model.Password);
            if (!currentOk)
            {
                ModelState.AddModelError(nameof(model.Password), await this.FlashAsync("validation.password_wrong", "Your password is incorrect."));
            }
        }

        if (!ModelState.IsValid)
        {
            // ADR 0301 D2 — preserve the scheduled state (if any) on the
            // re-render: a validation error does not invalidate the pending
            // row, so the resident still sees the "deletion scheduled for
            // {date}" notice (the ADR 0301 D2 "the resident sees the date
            // they already committed to" shape — a re-schedule resets the
            // row, but a validation error does not reach the schedule seam).
            var pendingOnReRender = await LoadPendingDeletionAsync(subject);
            model.IsScheduled   = pendingOnReRender is not null;
            model.ScheduledFor  = pendingOnReRender is null ? null : await FormatDueAtAsync(pendingOnReRender.DueAt);
            await LocalizeValidationAsync(model);
            return View(model);
        }

        // ADR 0301 D2 — the R13 prompt: "add IDeletionScheduler scheduler to
        // the controller ctor" — the DI-registered seam (the app shape). A
        // harness that does not supply one reaches this null-ref (the C3-05
        // "no store" idiom — the harness passes a Substitute explicitly, the
        // R13 test shape). The `!` is the ADR 0301 D2 "the seam is
        // registered" invariant, not a defensive swallow.
        // ADR 0301 D2 — the R13 prompt: "add IDeletionScheduler scheduler to
        // the controller ctor". The promoted ctor param is the primary source
        // (the harness passes a Substitute explicitly — the C3-05 "no store"
        // idiom); a fallback to the request scope covers the DI-registered
        // app shape (a harness that constructs the controller with 4 args
        // and the app has the seam registered).
        var schedulerRef = scheduler
            ?? HttpContext?.RequestServices?.GetService<IDeletionScheduler>()
            ?? throw new InvalidOperationException(
                "The self-service deletion scheduler (IDeletionScheduler) is not registered — " +
                "the ADR 0301 D2 (IMPROVE-UX R13) DI binding is missing.");

        try
        {
            // The single audited write lane (the R11 seam — the ADR 0301 D1
            // "one summary" shape, Via: Owner — actor == target on the
            // self-serve branch). Writes the pending DeletionRequest row +
            // the "account.delete.scheduled" audit row in one session; the
            // R12 due-job executes the existing DeleteAccountAsync lane
            // (pseudonymize + remove memberships/profile/Identity — the ADR
            // 0142 D2/D4 invariants) after the grace period (the ADR 0301
            // D2 N=14 days proposed; the R14 SEC review may adjust the
            // IDeletionScheduler.GracePeriodDays constant).
            await schedulerRef.ScheduleDeletionAsync(subject);
        }
        catch (UnauthorizedAccessException)
        {
            // ADR 0301 D3 (the child rule) — the resident is a child under
            // an active GuardianLink. The ADR 0142 D5 GlobalAdmin gate that
            // used to throw this exception is removed (ADR 0301 D1); this
            // catch is now the *distinct* "contact a parent/guardian" notice
            // (the DeleteAccountViewModel.IsChildRefused flag — the ADR
            // 0301 D3 "the same shape as 0142 D5's old SelfDeletionRefused,
            // with a different reason"). The ADR 0301 D3 "Also the
            // GlobalAdmin-removal path" clarification (a GlobalAdmin must
            // dissolve the link before removing a child) is the admin lane's
            // surface, not this one — this lane is self-serve only.
            //
            // // TODO ❓ (the R13 prompt's one design call — flag, don't
            // silently pick): the ADR 0301 D3 child rule fires at
            // SCHEDULING time (the R11 seam enforces it). The R12 handler's
            // `catch (UnauthorizedAccessException)` is *defensive* (at HEAD
            // the self-serve branch does not throw it, so it is unreachable
            // for the child rule at execution). The open question (for R14's
            // SEC review) is whether the child rule should also be
            // *re-checked* at execution time — i.e. a child whose
            // GuardianLink is dissolved mid-grace-window, then re-created,
            // would still be deleted at execution (the R11 seam's check is
            // scheduling-only). R13 does not touch the R12 handler.
            ModelState.AddModelError(string.Empty, await this.FlashAsync(
                "account.delete_child_refused",
                "This account is under a guardian's supervision. Contact a parent or guardian to manage the deletion (ADR 0301 D3)."));
            return View(new DeleteAccountViewModel { IsChildRefused = true });
        }
        catch (InvalidOperationException ex)
        {
            // ADR 0142 D3 (the last-GlobalAdmin lockout pin — the R11 seam
            // reuses the guard the DeleteCoreAsync lane already ran, above
            // the seam) or the account no longer exists (the ADR 0142
            // "the id is an error, not a silent no-op" shape). The message
            // is the R11 seam's own (English — the ADR 0301 D1 "the message
            // is the R11 seam's" floor; a localized version is the R14
            // SEC-review surface).
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        // The account is *scheduled*, not deleted: the resident stays signed
        // in (their credential is intact — the ADR 0301 D2 "the act of
        // returning invalidates the pending request" is the cancel, so the
        // session must outlive the schedule). Redirect to the GET (the
        // scheduled state renders: the "deletion scheduled for {date} — sign
        // in to cancel" notice + the Cancel button). A positive flash tells
        // the resident the schedule took effect (the ADR 0142 D7 "the
        // message is the positive 'your account was deleted' form" — now
        // "your deletion is scheduled").
        TempData["info"] = await this.FlashAsync(
            "account.flash_scheduled",
            "Your account deletion is scheduled. Sign in and cancel any time before the date to keep your account.");
        return RedirectToAction(nameof(Delete));
    }

    /// <summary>
    /// <c>POST /account/delete/cancel</c> — the Cancel button (ADR 0301 D2 —
    /// "cancel = signing in": the resident signs back in, the R13 surface
    /// calls this). The guard shape matches the POST /account/delete (the
    /// dangerous-action lane — the resident proves possession with the
    /// current password + the acknowledgment checkbox); a wrong password or
    /// unchecked box re-renders the schedule form (the account is untouched
    /// — the row stays Pending). On success the
    /// <see cref="IDeletionScheduler.CancelDeletionAsync"/> seam (R11) moves
    /// the row <see cref="DeletionRequestStatus.Pending"/> →
    /// <see cref="DeletionRequestStatus.Cancelled"/> (idempotent — the
    /// ADR 0301 D2 "no extra button, no extra form"; the <c>CancelledAt</c>
    /// timestamp is the record, no audit row). Redirects to the GET (the
    /// schedule form renders again).
    /// <para>
    /// The ADR 0301 D3 child rule does not apply here (the child already
    /// cannot schedule — the R11 seam refused at scheduling time — so a
    /// child has no Pending row to cancel; the idempotent no-op covers the
    /// crafted-POST case). The ADR 0142 D3 last-GlobalAdmin pin does not
    /// apply either (the pin fires at scheduling + at execution, not at
    /// cancel — the resident may still cancel a schedule they made before
    /// becoming the last GlobalAdmin, and the R12 due-job's execution-time
    /// re-check is the ADR 0301 D2 "the guard re-checks at the scheduled
    /// execution instant" surface, not this lane's).
    /// </para>
    /// </summary>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    // ADR 0301 D2 (IMPROVE-UX R13) — the Cancel button's URL. The R13
    // prompt specifies `POST /account/delete/cancel` (a sub-path, not the
    // convention route's `/Account/DeleteCancel`); the `[Route]` attribute
    // overrides the controller's `{controller=Home}/{action=Index}/{id?}`
    // convention (the repo's `AdminSignupController` / `AdminPortabilityController`
    // pattern — the controller-level `[Route]` is absent on AccountController,
    // so this is the action-level `[Route]` shape used by the admin-surface
    // controllers). The Cancel form (Views/Account/Delete.cshtml) targets
    // this URL via `asp-action="DeleteCancel"` (the action name stays
    // `DeleteCancel` for the view-to-action binding; the `[Route]` sets the
    // URL).
    [Route("account/delete/cancel")]
    public async Task<IActionResult> DeleteCancel(DeleteAccountViewModel model)
    {
        // Self-only (the C-UP·4/F7 shape from Storage()): the subject is
        // the signed-in principal — never a path param.
        var subject = SubjectId(User);
        if (subject is null)
            return Challenge();

        // The dangerous-action guard (the ADR 0142 D7 shape, generalized —
        // cancel is also a write lane, so the same two guards apply): the
        // resident must have checked the acknowledgment box AND provided
        // the correct current password. A bare [Required] on a bool would
        // accept false (the bound value is non-null), so the check is
        // explicit.
        if (!model.Confirmed)
        {
            ModelState.AddModelError(
                nameof(model.Confirmed), await this.FlashAsync("validation.confirm_deletion", "You must confirm that you understand your account will be permanently deleted."));
        }

        var user = await userManager.FindByIdAsync(subject);
        if (user is null)
        {
            ModelState.AddModelError(nameof(model.Password), await this.FlashAsync("validation.account_not_found", "We could not find that account — sign in again."));
        }
        else
        {
            var currentOk = await userManager.CheckPasswordAsync(user, model.Password);
            if (!currentOk)
            {
                ModelState.AddModelError(nameof(model.Password), await this.FlashAsync("validation.password_wrong", "Your password is incorrect."));
            }
        }

        if (!ModelState.IsValid)
        {
            await LocalizeValidationAsync(model);
            return RedirectToAction(nameof(Delete));
        }

        // ADR 0301 D2 — the Cancel seam (R11; idempotent — the ADR 0301 D2
        // "no extra button, no extra form"). The DI-registered seam (the app
        // shape); a harness that does not supply one reaches this null-ref
        // (the C3-05 "no store" idiom — the harness passes a Substitute
        // explicitly, the R13 test shape).
        // ADR 0301 D2 — the Cancel seam (the R13 prompt: "add
        // IDeletionScheduler scheduler to the controller ctor"). The
        // promoted ctor param is the primary source (the harness passes a
        // Substitute explicitly); the request-scope fallback covers the
        // DI-registered app shape.
        var schedulerRef = scheduler
            ?? HttpContext?.RequestServices?.GetService<IDeletionScheduler>()
            ?? throw new InvalidOperationException(
                "The self-service deletion scheduler (IDeletionScheduler) is not registered — " +
                "the ADR 0301 D2 (IMPROVE-UX R13) DI binding is missing.");

        // Cancel writes no audit row (the ADR 0301 D2 "no extra form — the
        // cancel is the act of returning; the CancelledAt timestamp on the
        // row is the record"). A Pending row → Cancelled; a missing /
        // already-Cancelled / already-Executed row is a no-op (the idempotent
        // shape — the ADR 0301 D2 "a re-schedule is a fresh act" inverse).
        await schedulerRef.CancelDeletionAsync(subject);

        // Positive flash + redirect to the GET (the schedule form renders
        // again — the ADR 0301 D2 "the resident may still cancel a schedule
        // they made" shape; the Cancel button is the explicit lane).
        TempData["info"] = await this.FlashAsync(
            "account.flash_cancelled",
            "Your account deletion has been cancelled. Your account is still active.");
        return RedirectToAction(nameof(Delete));
    }

    // ── Signup ──────────────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Signup([FromQuery] string? invite = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/");   // ADR 0238 — already signed in: Home, not the new-resident profile step

        var model = new SignupViewModel();

        // ADR 0304 (IMPROVE-UX A08) — resolve the presented invitation token
        // (the `?invite=` query param) via the Core presented-token seam (Trap A:
        // the hashing lives in one place, in Core — the Web host cannot hash).
        // A live token prefills the invited email (Invitation.Email) and marks
        // InvitationActive — the form then renders EVEN WHEN the ADR 0050 gate is
        // closed (the ADR 0304 D4 "works while sign-up is closed" invariant).
        // The token is a credential (ADR 0304 D2) — carried into the POST via a
        // hidden field (model.InviteToken), never a visible form control.
        var token = NormalizeToken(invite);
        if (token is not null && invitationService is not null)
        {
            var invitation = await invitationService.ValidatePresentedAsync(token);
            if (invitation is not null)
            {
                model.InvitationActive = true;
                model.Email         = invitation.Email;   // prefill the invited address
                model.InviteToken   = token;               // the hidden field's value
            }
        }

        // ADR 0050 — the sign-up gate (the admin-settled instance value, the
        // `false` floor — a fresh instance ships invitation-only). Closed + no
        // valid token → the invitation-only notice instead of the form. The gate
        // is authoritative (an authenticated admin still creates accounts through
        // the Guardian / admin lanes, not this self-service surface); a valid
        // invitation is the ADR 0304 exception that opens the surface — the gate
        // is not bypassed.
        if (!await identity.IsSignupOpenAsync() && !model.InvitationActive)
            return View("SignupClosed", new SignupClosedViewModel());

        // F9 (AUTH-15, U3) — surface the flag + site key to the view (pin §4b): the
        // challenge widget + the hidden BotChallengeToken input render ONLY when the
        // owner's KUMUNITA__SIGNUP__BOT_CHECK flag is on (the registered verifier is
        // NOT the Noop default). Resolved from the request scope (the
        // LocalizeValidationAsync idiom — the constructor is unchanged). Off (the
        // NoopBotChallengeVerifier, the default) → BotChallengeEnabled stays false and
        // the signup form is byte-identical to before this seam (the standing owner
        // deferral, OPS.md §10; the frozen CSP invariant — no new origin exposed).
        var botChallengeVerifier = HttpContext?.RequestServices
            ?.GetService<Kumunita.Core.Identity.IBotChallengeVerifier>();
        if (botChallengeVerifier is not Kumunita.Core.Identity.NoopBotChallengeVerifier)
        {
            model.BotChallengeEnabled = true;
            model.BotChallengeSiteKey = HttpContext?.RequestServices
                ?.GetService<Microsoft.Extensions.Options.IOptions<Kumunita.Core.Identity.SignupBotChallengeOptions>>()
                ?.Value.SiteKey;
        }

        return View(model);
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("signup")]
    public async Task<IActionResult> Signup(SignupViewModel model)
    {
        // ADR 0304 (IMPROVE-UX A08) — resolve the presented invitation token
        // (the hidden `InviteToken` form field, echoed from the GET's
        // `?invite=` query param) via the Core presented-token seam. The token
        // is a credential (ADR 0304 D2) — never compared raw here; the hashing
        // + email-match pin live in Core (Trap A). A live token lets the
        // write path through EVEN WHEN the ADR 0050 gate is closed (the
        // ADR 0304 D4 "works while sign-up is closed" invariant).
        var token = NormalizeToken(model.InviteToken);
        Invitation? invitation = null;
        if (token is not null && invitationService is not null)
            invitation = await invitationService.ValidatePresentedAsync(token);

        // ADR 0050 — the gate is authoritative on the write path too (the GET hides
        // the form, but the POST is the actual account-creation surface): a closed
        // gate denies the self-service write — UNLESS a live invitation is
        // presented (the ADR 0304 exception that opens the surface; the gate is
        // not bypassed, the invitation IS the exception). Closed + no live token
        // (absent / invalid / expired / revoked) → the ADR 0050 floor holds.
        if (!await identity.IsSignupOpenAsync() && invitation is null)
            return View("SignupClosed", new SignupClosedViewModel());

        if (!ModelState.IsValid)
        {
            await LocalizeValidationAsync(model);
            return View(model);
        }

        // F9 (AUTH-15, U3) — the bot-challenge gate (pin §4b): when the owner's
        // KUMUNITA__SIGNUP__BOT_CHECK flag is on, the signup POST must carry a
        // valid challenge token (the widget wrote it into BotChallengeToken).
        // Resolved from the request scope (the LocalizeValidationAsync idiom — the
        // constructor is unchanged for the existing direct-construction harnesses)
        // so a harness with no HTTP host / no registered IBotChallengeVerifier is a
        // no-op (null → skipped), exactly like the F3 leg-2 gate. The Core lane
        // (RegisterCoreAsync / IIdentityService) does NOT call the verifier — the
        // verification is a Web-enforced I/O gate (the IMailerStage "designed
        // handoff" precedent, pin §4b). On false: re-render with the
        // BotChallengeFailed flag (the EmailDomainDisallowed precedent, §3b) — a
        // bot is not proven human, and the reason is NOT leaked (the
        // uniform-rejection shape, the F3 leg-2 precedent).
        var botChallengeVerifier = HttpContext?.RequestServices
            ?.GetService<Kumunita.Core.Identity.IBotChallengeVerifier>();
        if (botChallengeVerifier is not null &&
            !await botChallengeVerifier.IsVerifiedAsync(model.BotChallengeToken))
        {
            model.BotChallengeFailed = true;
            ModelState.AddModelError(string.Empty, await this.FlashAsync("account.signup_bot_failed", "The security check did not pass. Please try again."));
            return View(model);
        }

        // ADR 0304 (IMPROVE-UX A08) — a live invitation (regardless of the gate
        // state) takes the invitation write seam: the single-address pin + the
        // RegisterAsync lane + the Accepted mark + the invitation.accept audit
        // row, in one Core lane (A07's AcceptAsync contract — the
        // AcceptPresentedAsync overload delegates to it, Trap A). The invited
        // person lands on the same "check your inbox" flash as the open-gate path.
        if (invitation is not null && token is not null)
        {
            // ADR 0304 D4 (IMPROVE-UX A08) — the single-address pin: the signing-up
            // email must match the invitation's (the token is only good for the
            // invited address). A07's AcceptAsync enforces this in Core (step 2,
            // before the RegisterAsync lane); the Web surface checks it FIRST so a
            // mismatch re-renders with a friendly notice + the invited email
            // refilled (the EmailDomainDisallowed / BotChallengeFailed
            // re-render-with-flag precedent), instead of surfacing a raw exception.
            // The Core check remains the source of truth (this is a UX shortcut,
            // not a security gate).
            if (!string.Equals(
                    model.Email.Trim().ToLowerInvariant(), invitation.Email,
                    StringComparison.Ordinal))
            {
                model.InvitationActive = true;   // the token is still live — only the email was wrong
                model.InvitationMismatch = true;
                model.Email = invitation.Email;   // refill the invited address for the retry
                ModelState.AddModelError(string.Empty, await this.FlashAsync(
                    "account.signup_invite_email_mismatch",
                    "This invitation is for a different email address. Please sign up with the invited address."));
                return View(model);
            }

            try
            {
                await invitationService!.AcceptPresentedAsync(
                    token, model.Email, model.DisplayName, model.Password);
            }
            catch (DisallowedEmailDomainException ex)
            {
                // F9 (AUTH-15) — the disposable-email blocklist rejection (caught
                // BEFORE the InvalidOperationException catch — pin §3b).
                model.EmailDomainDisallowed = true;
                ModelState.AddModelError(string.Empty, await this.FlashAsync("validation.domain_not_accepted", "The email domain '{0}' is not accepted for new accounts.", ex.Domain));
                return View(model);
            }
            catch (InvalidOperationException ex)
            {
                // A07's AcceptAsync refusal (the row is no longer Pending — e.g. a
                // second accept, a revoked/expired token) or a RegisterAsync failure
                // (e.g. the invited email already has an account). Friendly
                // re-render, not a crash (the EmailAlreadyExists precedent).
                if (ex.Message.StartsWith("An account with email", StringComparison.OrdinalIgnoreCase))
                    model.EmailAlreadyExists = true;
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }

            TempData["info"] = await this.FlashAsync("account.flash_created", "Account created. Check your inbox for the verification link.");
            return RedirectToAction(nameof(Login));
        }

        // ── open-gate path (no live invitation) ─────────────────────────────
        try
        {
            // RegisterAsync: creates the unverified account, bootstraps the Profile
            // (visibility self-only, Core-defaulted), mints the verify token, stages the
            // verification email into the durable outbox — all in one Core transaction.
            await identity.RegisterAsync(model.DisplayName, model.Email, model.Password);
        }
        catch (DisallowedEmailDomainException ex)
        {
            // F9 (AUTH-15) — the disposable-email blocklist rejection. Caught BEFORE the
            // `InvalidOperationException` catch (the dedicated type must not be swallowed
            // by it) and re-rendered with the `EmailDomainDisallowed` flag (the
            // `EmailAlreadyExists` precedent) — pin §3b.
            model.EmailDomainDisallowed = true;
            ModelState.AddModelError(string.Empty, await this.FlashAsync("validation.domain_not_accepted", "The email domain '{0}' is not accepted for new accounts.", ex.Domain));
            return View(model);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.StartsWith("An account with email", StringComparison.OrdinalIgnoreCase))
                model.EmailAlreadyExists = true;
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["info"] = await this.FlashAsync("account.flash_created", "Account created. Check your inbox for the verification link.");
        return RedirectToAction(nameof(Login));
    }

    /// <summary>
    /// The A08 (IMPROVE-UX A08) token normalizer: trims a presented
    /// <c>?invite=</c> / hidden-field value and returns <c>null</c> for a blank
    /// one (the ESC·5 guard — the seam then refuses it and opens no session).
    /// The raw value is passed through unchanged otherwise (the hashing is Core's
    /// job — Trap A).
    /// </summary>
    private static string? NormalizeToken(string? presented) =>
        string.IsNullOrWhiteSpace(presented) ? null : presented.Trim();

    // ── Resend confirmation email (an unactivated account already exists for the email) ──

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResendVerification([FromQuery] string? email) =>
        View(new ResendVerificationViewModel { Email = email ?? string.Empty });

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("resend")]
    public async Task<IActionResult> ResendVerification(ResendVerificationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LocalizeValidationAsync(model);
            return View(model);
        }

        await identity.ResendVerificationEmailAsync(model.Email);
        // M1 — uniform response: whether or not an account exists for this email
        // (and whether the per-account attempt bound is exhausted), the response is
        // identical. The Core service returns different Reason strings for the two
        // failure modes; both must produce the SAME response so an attacker probing
        // which emails are registered cannot distinguish them. The uniform message
        // is "if an unverified account exists, a link is on its way" — true for the
        // fresh-account path, false for the no-account / exhausted paths, but the
        // resident cannot tell which is which.
        TempData["info"] =
            $"If there is an unverified account with email '{model.Email}', a new " +
            "verification link is on its way. Check your inbox (and your spam folder).";
        return RedirectToAction(nameof(Login));
    }

    // ── Verify (the one designed handoff) ───────────────────────────────────────────────
    // ADR 0146 — the handoff is now two-shaped: the self-serve lane (the credential was
    // set at signup) confirms the account on the link click, while the **child lane**
    // (the guardian created the account without holding the child's credential — ADR
    // 0028 "supervision rides the link, not the password") collects the child's own
    // password on the confirmation page and activates the account in one commit. The
    // lane is decided by whether the account has a password (the single-source
    // `UserManager.HasPasswordAsync` read), so a resident who exhausts the resend
    // attempts and is manually verified still gets the confirm-only surface, and a
    // child who re-requests the link still gets the set-password surface.

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Verify([FromQuery] string id)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/");   // ADR 0238 — already signed in: Home, not the new-resident profile step

        var (token, errorModel) = await LoadUsableVerifyTokenAsync(id);
        if (token is null)
            return View(errorModel);

        if (await AccountHasPasswordAsync(token.UserId))
        {
            // Self-serve lane — the credential exists.
            //
            // SEC L3 (U24, 2026-10-31) — the GET no longer signs in. A GET must
            // be side-effect-free (a prefetching proxy, a browser prefetch, or a
            // crawler must not consume the one-time token or mint a session
            // cookie). The GET renders a confirm page; the POST (same link,
            // same token row id round-tripped in the hidden input) validates
            // + signs in via ConfirmAndSignInAsync and is the only path that
            // consumes the token (ADR 0146's "consume at the POST" principle).
            return View(new VerifyViewModel { RequiresPassword = false, Id = id });
        }

        // Child lane — the account has no password; render the set-password form so
        // the child sets their own credential at the confirmation link.
        return View(new VerifyViewModel { RequiresPassword = true, Id = id });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify(VerifyViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/");   // ADR 0238 — already signed in: Home, not the new-resident profile step

        // The POST carries the token row id back in the model (the form's hidden
        // input) — the child lane re-submits to the same link.
        var id = model.Id ?? string.Empty;
        var (token, errorModel) = await LoadUsableVerifyTokenAsync(id);
        if (token is null)
            return View(errorModel ?? new VerifyViewModel { Id = id });

        if (!await AccountHasPasswordAsync(token.UserId))
        {
            // Child lane — the account has no password yet.
            if (!ModelState.IsValid)
            {
                await LocalizeValidationAsync(model);
                return View(new VerifyViewModel { RequiresPassword = true, Error = model.Error, Id = id });
            }

            // Set the child's own password + flip Verified + consume the token, one
            // Core commit (the ADR 0146 seam), then sign the resident in.
            try
            {
                var profile = await identity.VerifyAndSetPasswordAsync(token.Token, model.Password);
                return await SignInAfterVerifyAsync(
                    await userManager.FindByIdAsync(profile.SubjectId)
                        ?? throw new InvalidOperationException("Account not found."));
            }
            catch (InvalidOperationException ex)
            {
                return View(new VerifyViewModel { RequiresPassword = true, Error = ex.Message, Id = id });
            }
        }

        // Self-serve lane — the credential exists; confirm and sign in.
        return await ConfirmAndSignInAsync(token);
    }

    /// <summary>
    /// Whether the account for a subject id has a password set (the ADR 0146
    /// lane-decision read: true ⇒ self-serve lane / confirm-only; false ⇒
    /// child lane / collect the password at the link). A user lookup is
    /// required (the <see cref="UserManager{TUser}.HasPasswordAsync"/> seam
    /// takes a <c>User</c>, not an id), so this is a single EF read — cheap,
    /// and the same read the confirm / set-password branches will do.
    /// </summary>
    private async Task<bool> AccountHasPasswordAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is not null && await userManager.HasPasswordAsync(user);
    }

    /// <summary>
    /// Resolves the verification link to a usable single-use token, or (null, the
    /// error model) when the link is invalid/expired/consumed (the M1 error shape,
    /// unchanged). Shared by the GET and POST lanes so the two agree on the
    /// usable-token definition.
    /// </summary>
    private async Task<(IdentityToken? token, VerifyViewModel? errorModel)> LoadUsableVerifyTokenAsync(string id)
    {
        // C3-05 (ADR 0295) — the by-id read now runs through the IdentityModule
        // seam (the <c>IdentityToken</c> document is the Identity's), so this
        // controller no longer opens its own session. The usability decision
        // (Kind + IsUsableAt) stays here — it is the Web's presentable-error
        // concern.
        var token = await identity.LoadVerifyTokenAsync(id);
        if (token is not null
            && token.Kind == IdentityToken.KindVerify
            && token.IsUsableAt(DateTimeOffset.UtcNow))
        {
            return (token, null);
        }

        return (null, new VerifyViewModel
        {
            Error = "This verification link is invalid, expired, or already used. " +
                    "Sign up again or ask an admin to verify your account."
        });
    }

    /// <summary>
    /// Confirms a verification token (self-serve lane, no credential write) and signs
    /// the resident in — the M1 handoff end, factored out of the former monolithic
    /// <c>Verify</c> action. The child lane calls the <c>VerifyAndSetPasswordAsync</c>
    /// seam + <see cref="SignInAfterVerifyAsync"/> itself instead.
    /// </summary>
    private async Task<IActionResult> ConfirmAndSignInAsync(IdentityToken token)
    {
        // VerifyWithTokenAsync flips Profile.Verified and consumes the token in one
        // Core transaction (audit row via:Owner).
        var profile = await identity.VerifyWithTokenAsync(token.Token);
        var user = await userManager.FindByIdAsync(profile.SubjectId)
            ?? throw new InvalidOperationException("Account not found.");

        return await SignInAfterVerifyAsync(user);
    }

    /// <summary>
    /// Mints the admissible claim set and signs the resident in with the persistent
    /// 14-day cookie every sign-in lane issues (the M1 "land signed-in" handoff end).
    /// Factored out so both the confirm-only and set-password lanes end the handoff
    /// the same way.
    /// </summary>
    private async Task<IActionResult> SignInAfterVerifyAsync(User user)
    {
        // The verification link is the handoff end — the resident should land signed-in,
        // so mint the cookie through the same factory step 6 uses at sign-in (the claim
        // set is the whole principal; no extra DB read on later requests).
        var factory = HttpContext.RequestServices.GetRequiredService<KumunitaClaimsPrincipalFactory>();
        var identityPrinciple = await factory.CreateAsync(user);

        // Mint the same persistent, 14-day cookie every sign-in lane issues: the handler
        // honors the explicit ExpiresUtc and, with sliding expiration on, refreshes the
        // ticket as the resident keeps the tab open. Without this, the handler falls
        // back to a session cookie that dies when the browser closes.
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,  // persistent cookie — survives a browser close
            ExpiresUtc = DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(14)),
        };

        // SEC M5 (U17) — attach the current security stamp so the
        // RevocationStampMiddleware (which reads `kumunita.stamp` from
        // AuthenticationProperties.Items) can validate it on subsequent
        // requests. Without this, the verify sign-in lane mints a cookie
        // with no stamp and the middleware passes it through (fail-open).
        var stamp = await userManager.GetSecurityStampAsync(user);
        if (!string.IsNullOrEmpty(stamp))
        {
            authProperties.Items["kumunita.stamp"] = stamp;
        }

        await HttpContext.SignInAsync(
            scheme: CookieAuthenticationDefaults.AuthenticationScheme,
            principal: identityPrinciple,
            properties: authProperties);

        return Redirect("/profile/edit");
    }

    // ── Login ───────────────────────────────────────────────────────────────────────────

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login([FromQuery] string? returnUrl = null, [FromQuery] string? error = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/");   // ADR 0234 — sign-in lands on Home

        // `error` arrives as a short code (not the message text) — the query string is
        // user-visible and may be bookmarked/shared, so keep it token-like. The
        // code→message table lives in the Login view (D6 — "the mapping is a 3-row
        // table in the view"). The three known codes are:
        //   "blocked"          — BlockedAccountMiddleware (suspension mid-session)
        //                        + the POST-Login block-enforcement guard.
        //   "account-removed"  — PrivilegedStampMiddleware (user deleted).
        //   "role-changed"     — PrivilegedStampMiddleware (role-set mismatch).
        // An unknown code is passed through verbatim — the view falls back to
        // `Model.Error` (forward-compatible: a future code still shows something).

        // Hide the "Received a first-boot setup token?" hint once the seed-admin
        // setup has already been completed (or its token has since expired) — after
        // that there is no live setup lane left to complete, and the account signs in
        // with a real password.
        var showSetupLink = !await identity.IsFirstBootSetupCompleteAsync();

        // ADR 0050 — the sign-up gate (the `false` floor — a fresh instance ships
        // invitation-only): when closed, the view suppresses the "No account yet?
        // Sign up." affordance (a closed gate has no self-service signup surface
        // to point at).
        var signupOpen = await identity.IsSignupOpenAsync();

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            Error = error,   // ← the code, not a resolved message (D6: the view does the mapping)
            ShowSetupLink = showSetupLink,
            SignupOpen = signupOpen,
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LocalizeValidationAsync(model);
            return View(model);
        }

        var user = await userManager.FindByNameAsync(model.Email)
                 ?? await userManager.FindByEmailAsync(model.Email);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, await this.FlashAsync("validation.login_failed", "Email or password is incorrect."));
            return View(model);
        }

        // Leg 1 — password check (the pin §5 leg 1). `isPersistent: false` so a
        // not-yet-MFA'd sign-in does not mint the long-lived cookie (the pin §5
        // leg 1: "the only change from today is `isPersistent: true` →
        // `isPersistent: false`"). The .NET 10.0.13 `PasswordSignInAsync` calls
        // `SignInOrTwoFactorAsync` after a successful password check, which
        // returns `SignInResult.TwoFactorRequired` (Succeeded == false,
        // RequiresTwoFactor == true) when MFA is required — so we must check
        // `RequiresTwoFactor` BEFORE `Succeeded` (a `TwoFactorRequired` result
        // is NOT a failure). For the non-MFA path (leg 1.5c, the
        // NonAdmin_MfaOptional guarantee), the sign-in still completes — the
        // cookie just won't be long-lived, which is functionally equivalent
        // (the cookie's lifetime is set by the handler's ExpireTimeSpan, not
        // by the IsPersistent flag, for the non-MFA case).
        var result = await signInManager.PasswordSignInAsync(
            user, model.Password, isPersistent: false, lockoutOnFailure: true);

        // Leg 1.5 — MFA gate (the pin §5 leg 1.5). `result.RequiresTwoFactor`
        // is the .NET 10 two-factor gate (① round-2 reflection-verified — the
        // property on `SignInResult` is present in 10.0.13). The .NET 10.0.13
        // `SignInResult` states are:
        //   · wrong password (any MFA state)        → Succeeded=false, RequiresTwoFactor=false
        //   · correct password + MFA enabled        → Succeeded=false, RequiresTwoFactor=true  (TwoFactorRequired)
        //   · correct password + MFA disabled       → Succeeded=true,  RequiresTwoFactor=false  (Success)
        // so `RequiresTwoFactor` is the authoritative challenge gate and is
        // ONLY set when the password was correct AND MFA is enabled
        // (`CheckPasswordSignInCoreAsync` returns `SignInResult.Failed` for a
        // wrong password before `SignInOrTwoFactorAsync` runs, so a wrong
        // password on an MFA account has `RequiresTwoFactor == false` and
        // falls through to the `Succeeded` check below — it does NOT get the
        // challenge). The pin §5's "fallback" (`IsTwoFactorEnabledAsync`) is a
        // compile-time alternative for if `RequiresTwoFactor` did not resolve;
        // it resolves (F3U2's `nameof(SignInResult.RequiresTwoFactor)` is
        // green), so it is not OR'd in here — OR-ing it would wrongly show the
        // challenge for a wrong password on an MFA account.
        if (result.RequiresTwoFactor)
        {
            // The account is MFA-protected and the password was correct.
            // Return the MFA challenge (a TOTP/recovery-code field on the
            // login view) — do NOT yet redirect to `ReturnUrl` (the pin §5
            // leg 1.5). The user is already in a temporary (non-persistent)
            // sign-in from leg 1 (the `TwoFactorUserIdScheme` cookie, which
            // `SignInOrTwoFactorAsync` mints), so the leg-2 methods can read
            // the pending user back (the pin §5 leg 1.5: "no separate
            // pending-user store is required"). Complete the sign-in at the
            // challenge leg (leg 2, `LoginMfa`).
            // Preserve the resident's email + RememberMe flag in the model so
            // the view can render the challenge form (the `MfaPending` flag
            // drives the view's branch; the `MfaCode` / `MfaRecoveryCode`
            // fields are the challenge inputs, empty here — the resident
            // fills them in).
            model.MfaPending = true;
            model.Email = user.Email ?? user.UserName ?? model.Email;
            model.MfaCode = null;
            model.MfaRecoveryCode = null;
            return View(model);
        }

        // A real sign-in failure (wrong password, lockout, not-allowed). The
        // `TwoFactorRequired` case was handled above; if `Succeeded` is
        // `false` here, it is a genuine failure — fail as today (the existing
        // `AccountController` error path, the pin §5 leg 1 "If Succeeded is
        // false → fail as today").
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty,
                result.IsLockedOut
                    ? await this.FlashAsync("validation.account_locked", "Account is temporarily locked. Try again in a few minutes.")
                    : await this.FlashAsync("validation.login_failed", "Email or password is incorrect."));
            return View(model);
        }

        // Leg 1.5b — the "Enforce for GlobalAdmin" requirement (the pin §5
        // leg 1.5b). Reached only on a successful password check (
        // `Succeeded == true`) for a non-MFA account. A `GlobalAdmin` who has
        // NOT enrolled yet (i.e. `TwoFactorEnabled == false`) is redirected to
        // the MFA enrollment surface instead of proceeding — they cannot reach
        // the admin surface with password-only (the pin §5 leg 1.5b: "a
        // GlobalAdmin with TwoFactorEnabled == false is redirected to
        // /settings/security/mfa (enroll) instead of proceeding"). "U4 must
        // complete the sign-in for this user only after they enroll" — after
        // enrolling (the enrollment POST flips `SetTwoFactorEnabledAsync
        // (user, true)` + issues recovery codes via F3.U3's `MfaEnable`
        // action) the resident re-signs-in and leg 1.5 now returns
        // `TwoFactorRequired`, completing via the leg-2 challenge.
        // ADR 0194 — the sample-admin exemption: a sample account (a member of
        // the closed SampleAccountEmails set, on a SampleData__Enabled
        // instance) is EXEMPT from this forced enrollment. Without this
        // exemption, a locked demo instance (the admin has locked MFA for the
        // sample accounts so a visitor can't break the shared credential)
        // would redirect the sample GlobalAdmin to /settings/security/mfa on
        // every sign-in — but that page is itself locked for the sample
        // accounts (IsMfaLockedForAsync), so the admin could never sign in
        // with the shared password. The exemption reads the two-conjunct
        // IsSampleDataAccountAsync seam (conjuncts 1+3 only — the admin-toggle
        // gate is NOT consulted, so a locked instance still lets its admin
        // through; the GlobalAdmin check is NOT applied either, since the
        // exemption the login path needs is *precisely for* the sample
        // GlobalAdmin). A real (non-sample) account and the non-sample
        // GlobalAdmin are unaffected — this leg still forces them to enroll.
        // Use `UserManager.GetRolesAsync` (the repo's verified pattern —
        // `AccountController.cs:281`) + `Kumunita.Core.Identity.Roles.GlobalAdmin`
        // (NOT `ClaimsPrincipal.IsInRole`, which does not see the custom
        // `Kumunita.Role` claim type).
        var userRoles = await userManager.GetRolesAsync(user);
        if (userRoles.Contains(Kumunita.Core.Identity.Roles.GlobalAdmin)
            && !await signInManager.IsTwoFactorEnabledAsync(user)
            && !(user.Id is not null && await identity.IsSampleDataAccountAsync(user.Id)))
        {
            // **Do NOT sign out here.** The `SecuritySettingsController`
            // (F3.U3) is `[Authorize]`, so the resident must be signed in to
            // reach the enrollment page. This branch is only reached when the
            // password was correct (`Succeeded == true`, above), and an
            // unenrolled account's leg-1 `PasswordSignInAsync` mints the app
            // cookie (MFA is off, so `SignInOrTwoFactorAsync` takes the
            // non-MFA branch → `SignInWithClaimsAsync`), so the resident IS
            // signed in — the redirect to enrollment works. Signing out would
            // break the `[Authorize]` (auth challenge → redirect back to
            // /Account/Login → loop).
            // SEC H2 (invariant S-2): this redirect is a **convenience only**,
            // not the control. The server-side gate is
            // `AdminTwoFactorMiddleware` (registered in Program.cs after
            // UseAuthentication, before UseAuthorization), which enforces
            // 2FA on every subsequent request for an un-enrolled GlobalAdmin
            // and redirects any non-allow-listed route to enrolment. Even if
            // this branch were skipped, the middleware is what keeps an
            // un-enrolled admin out of the rest of the site.
            return RedirectToAction("Mfa", "SecuritySettings");
        }

        // Leg 1.5c — the existing path for non-MFA users, UNCHANGED from today
        // (the pin §5 leg 1.5c, the NonAdmin_MfaOptional guarantee). The
        // **persistent** sign-in — `PasswordSignInAsync(user, model.Password,
        // isPersistent: true, lockoutOnFailure: true)` (the pin §5 leg 1.5c:
        // "the persistent sign-in … the current code's exact shape"; the app
        // "Always mint[s] a persistent, long-lived cookie") — is called here
        // (NOT in leg 1, which used `isPersistent: false` so the not-yet-MFA'd
        // sign-in does not mint the long-lived cookie). This re-verifies the
        // password (already verified at leg 1, so this succeeds) and re-mints
        // the app cookie as **persistent** (overwriting the non-persistent
        // cookie leg 1 minted), preserving the pre-F3.U4 behavior for
        // non-MFA users exactly. Then the `profile.Blocked` check + the
        // `ReturnUrl` redirect stay exactly as today.
        await signInManager.PasswordSignInAsync(
            user, model.Password, isPersistent: true, lockoutOnFailure: true);

        var profile = await userInfo.GetProfileAsync(user.Id ?? string.Empty);
        if (profile is not null && profile.Blocked)
        {
            await signInManager.SignOutAsync();
            // The GET-Login action maps the "blocked" code to the resident-facing
            // message (see Login GET) — keep it as a code here too, consistent with
            // the BlockedAccountMiddleware path and non-informative in URLs.
            return RedirectToAction(nameof(Login), new { error = "blocked" });
        }

        return Url.IsLocalUrl(model.ReturnUrl)
            ? Redirect(model.ReturnUrl)
            : Redirect("/");   // ADR 0234 — sign-in lands on Home
    }

    // ── MFA challenge leg (F3.U4, the pin §5 leg 2) ──────────────────────────────────
    //
    // The pin §4 routes table: `POST /account/login/mfa` → `LoginMfa
    // (MfaChallengeViewModel)`. The action accepts a TOTP **or** a recovery
    // code for the pending (temporarily signed-in) user, then completes the
    // **persistent** sign-in via `SignInManager.TwoFactorAuthenticatorSignIn
    // Async(code, isPersistent, rememberClient)` / `TwoFactorRecoveryCode
    // SignInAsync(recoveryCode)` (both ③ U2-build-verifies against the
    // round-3 shape — F3U2_TokenStoreApiCompileCheckTests.cs proved these
    // names compile against `Microsoft.AspNetCore.Identity 10.0.13`).
    //
    // **Leg-2 return types (the pin §5 leg 2: "if a build shows they do NOT
    // auto-mint, U4 adds a `HandleSignInAsync` follow-up and records it"):**
    // the compiler resolves both methods to `Task<SignInResult>` (the
    // `result.Succeeded` branch below compiles green — `SignInResult` is the
    // return type, NOT `bool` or `IdentityResult`). The .NET 10.0.13 source
    // confirms both call `DoTwoFactorSignInAsync` → `SignInWithClaimsAsync`
    // (which mints the app cookie) on success; no `HandleSignInAsync`
    // follow-up is needed (recorded in the handoff note).
    //
    // **Model note:** the action binds to `LoginViewModel` (the same model the
    // `Login` action uses, the pin §5 leg 1.5 "a code/recovery-code field on
    // the login view") — NOT a separate `MfaChallengeViewModel`. The
    // `MfaCode` / `MfaRecoveryCode` fields (added to `LoginViewModel` by this
    // unit) carry the TOTP / recovery-code inputs; `MfaPending` + `Email` +
    // `ReturnUrl` + `RememberMe` are carried through for the error re-render.
    //
    // **URL note (F3.U4 refinement of the pin §4 table):** the pin's §4 table
    // names `POST /account/login/mfa`, but `AccountController` is routed by
    // the conventional `{controller=Home}/{action=Index}/{id?}` route
    // (Program.cs:789) — under that route this action is reachable at
    // `POST /Account/LoginMfa` (NOT `/account/login/mfa`). This is consistent
    // with the repo's other `AccountController` actions (`/Account/Login`,
    // `/Account/Signup`, `/Account/Logout`) and the pin §5's "the enforcement
    // seam is `AccountController.Login` POST" — the challenge leg lives in
    // the same controller and is reachable by the same convention. The view
    // form posts to `asp-action="LoginMfa"` (the conventional URL), not the
    // pin's literal `/account/login/mfa` path. The pin's URL is the
    // *semantic* name for the leg; the actual URL is the conventional one.
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> LoginMfa(LoginViewModel model)
    {
        // F5 (AUTH-04, Option B) — the leg-2 revocation gate (pin §1d / §2
        // "leg-2 gating"). A kill-all (or password change) that rotated the
        // security stamp AFTER leg-1 minted the pending
        // `Identity.TwoFactorUserId` cookie leaves that cookie's stamp
        // (stamped at leg-1 by the `OnSigningIn` handler in `Program.cs`)
        // no longer matching the DB's current stamp. Without this gate the
        // resident would complete leg-2 with a valid TOTP / recovery code and
        // mint a FRESH app cookie after the kill — the half-signed-in state
        // the F5 pin explicitly forbids (§1d: "the half-signed-in window F5
        // must not let a kill-all leave dangling").
        //
        // The check reads the pending cookie's `Properties.Items["kumunita.stamp"]`
        // (the per-session carrier, NOT a claim type — invariant-set-B intact)
        // and compares it to the DB's current `SecurityStamp` (via
        // `UserManager.GetSecurityStampAsync` — the framework's own
        // `ValidateSecurityStampAsync` comparison, inlined here so the gate
        // can sign out and re-render the challenge rather than returning a
        // bare `SignInResult.Failed`). A mismatch → sign out (clears BOTH the
        // pending `Identity.TwoFactorUserId` cookie AND the app scheme, the
        // same `SignInManager.SignOutAsync` shape the `!result.Succeeded`
        // branch below uses) + re-render the challenge with a revocation
        // message. The resident must re-do leg-1 (password) to get a fresh
        // pending cookie with the current stamp.
        //
        // If the pending cookie carries no stamp (e.g. leg-1 was minted
        // before F5 shipped) the gate is a no-op — the resident's legitimate
        // leg-2 completion is not blocked (backward compat, the same rule as
        // `RevocationStampMiddleware`).
        // Resolve IAuthenticationService explicitly (not via the AuthenticateAsync
        // extension, which calls GetRequiredService and throws in test harnesses
        // that use a DefaultHttpContext without authentication services registered).
        // In production this is always available (AddAuthentication registers it).
        // If it's absent (unit-test harness), the gate is a no-op — the test does
        // not exercise the authentication pipeline.
        if (HttpContext.RequestServices is not null
            && HttpContext.RequestServices.GetService<IAuthenticationService>() is not null)
        {
            var pendingAuth = await HttpContext.AuthenticateAsync(IdentityConstants.TwoFactorUserIdScheme);
            if (pendingAuth.Succeeded
                && pendingAuth.Principal?.Identity?.Name is string pendingUserId
                && pendingAuth.Properties?.Items.TryGetValue("kumunita.stamp", out var pendingStampObj) == true
                && pendingStampObj is string pendingStamp)
            {
                var pendingUser = await userManager.FindByIdAsync(pendingUserId);
                if (pendingUser is not null)
                {
                    var currentStamp = await userManager.GetSecurityStampAsync(pendingUser);
                    if (pendingStamp != currentStamp)
                    {
                        // A kill-all / password change rotated the stamp after leg-1.
                        // Reject the pending cookie and force the resident back to
                        // leg-1 (password) so a fresh pending cookie is minted with
                        // the current stamp.
                        await signInManager.SignOutAsync();
                        ModelState.AddModelError(string.Empty,
                            await MfaTextAsync("account.mfa.error.revoked",
                    "This sign-in was revoked. Please enter your password again."));
                        model.MfaPending = true;
                        return View("Login", model);
                    }
                }
            }
        }

        // The pin §5 leg 2: at least one of `MfaCode` (TOTP) or
        // `MfaRecoveryCode` (the pin §6 escape) must be present.
        if (string.IsNullOrWhiteSpace(model.MfaCode)
            && string.IsNullOrWhiteSpace(model.MfaRecoveryCode))
        {
            ModelState.AddModelError(string.Empty,
                await MfaTextAsync("account.mfa.error.empty",
                    "Enter a code from your authenticator app, or a recovery code."));
            model.MfaPending = true;
            return View("Login", model);
        }

        // Leg 2 — complete the sign-in. The user comes from the pending temp
        // sign-in (leg 1, `isPersistent: false`), stored in the
        // `TwoFactorUserIdScheme` cookie by `SignInOrTwoFactorAsync` (the
        // .NET 10.0.13 source at `SignInOrTwoFactorAsync` — `
        // Context.SignInAsync(IdentityConstants.TwoFactorUserIdScheme,
        // StoreTwoFactorInfo(userId, loginProvider))`), so the leg-2 methods
        // take NO `user` arg (they call `RetrieveTwoFactorInfoAsync` to read
        // it back — the pin §5 leg 2, both ③ round-3 verified).
        // `TwoFactorAuthenticatorSignInAsync` (TOTP) and
        // `TwoFactorRecoveryCodeSignInAsync` (recovery code) each return
        // `Microsoft.AspNetCore.Identity.SignInResult` (the
        // compiler-verified shape — the `result.Succeeded` branch below is the
        // proof; fully-qualified to disambiguate from
        // `Microsoft.AspNetCore.Mvc.SignInResult`, which is also in scope in
        // the Web project). The .NET 10.0.13 source confirms both call
        // `DoTwoFactorSignInAsync` → `SignInWithClaimsAsync` (which mints the
        // app cookie) on success; no `HandleSignInAsync` follow-up is needed
        // (recorded in the handoff note — the pin §5 leg 2 "do not pre-commit
        // to `HandleSignInAsync`").
        Microsoft.AspNetCore.Identity.SignInResult result;
        if (!string.IsNullOrWhiteSpace(model.MfaRecoveryCode))
        {
            // Recovery code (the pin §6 escape — presented **once**;
            // `TwoFactorRecoveryCodeSignInAsync` redeems it under the hood via
            // `UserManager.RedeemTwoFactorRecoveryCodeAsync`, dropping the
            // count by 1 — the F3.U5 test `GlobalAdminRecoveryCode_WorksOnce`
            // asserts this). 1-arg, NO user (③ round-3 verified).
            // Note: the .NET 10.0.13 source at
            // `TwoFactorRecoveryCodeSignInCoreAsync` calls
            // `DoTwoFactorSignInAsync(user, info, isPersistent: false,
            // rememberClient: false)` — the recovery path is
            // always non-persistent + no remember-client (the `RememberMe`
            // flag does not apply to the recovery-code path).
            result = await signInManager.TwoFactorRecoveryCodeSignInAsync(
                model.MfaRecoveryCode.Trim());
        }
        else
        {
            // TOTP code (RFC 6238). Code-first, NO user (③ round-3 verified).
            // `isPersistent` + `rememberClient` take the `RememberMe` flag the
            // resident chose on the password form (the pin §5 leg 2:
            // `TwoFactorAuthenticatorSignInAsync(code, isPersistent,
            // rememberClient)`).
            result = await signInManager.TwoFactorAuthenticatorSignInAsync(
                model.MfaCode!.Trim(),
                isPersistent: model.RememberMe,
                rememberClient: model.RememberMe);
        }

        if (!result.Succeeded)
        {
            // ADR 0214 — a wrong code used to SignOutAsync here, which discards the
            // pending `TwoFactorUserId` cookie: the challenge re-rendered with
            // "Try again", but the retry could never succeed (no pending sign-in
            // left), so every typo forced the resident back to the password leg.
            // Identity already counts each failed TOTP / recovery code towards
            // lockout (AccessFailedAsync inside the leg-2 methods), so the pending
            // cookie is kept for another try and only a lockout ends the challenge.
            if (result.IsLockedOut)
            {
                await signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty,
                    await MfaTextAsync("account.mfa.error.locked",
                    "Too many incorrect codes. This account is temporarily locked; try again later."));
                model.MfaPending = false;
                model.MfaCode = null;
                model.MfaRecoveryCode = null;
                ModelState.Remove(nameof(model.MfaCode));
                ModelState.Remove(nameof(model.MfaRecoveryCode));
                return View("Login", model);
            }

            ModelState.AddModelError(string.Empty,
                await MfaTextAsync("account.mfa.error.invalid",
                    "The code was not valid. Try again, or use a recovery code."));
            // Re-render with the wrong code cleared (the tag helper prefers ModelState
            // over the model, so the ModelState entries are removed too).
            model.MfaCode = null;
            model.MfaRecoveryCode = null;
            ModelState.Remove(nameof(model.MfaCode));
            ModelState.Remove(nameof(model.MfaRecoveryCode));
            model.MfaPending = true;
            return View("Login", model);
        }

        // Complete the sign-in and redirect to `ReturnUrl` (the pin §5 leg 2:
        // "complete the sign-in and mint the auth cookie on success"). The
        // cookie was minted by the leg-2 sign-in method above (via
        // `DoTwoFactorSignInAsync` → `SignInWithClaimsAsync`).
        //
        // ADR 0234 — a recovery-code sign-in means the second factor is lost or
        // suspect: send the person to the MFA settings to re-enroll or rotate the
        // remaining codes. It wins over a ReturnUrl (the code was just spent).
        if (!string.IsNullOrWhiteSpace(model.MfaRecoveryCode))
            return Redirect("/settings/security/mfa");

        return Url.IsLocalUrl(model.ReturnUrl)
            ? Redirect(model.ReturnUrl)
            : Redirect("/");   // ADR 0234 — Home, not the profile editor
    }

    // ── Sign-out ────────────────────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // SignOutAsync (not a bare SignOutAsync on the auth scheme) clears BOTH
        // of the app's cookies: AddIdentity registers an auxiliary anonymous
        // .AspNetCore.Identity.Application cookie alongside the kumunita.auth
        // cookie, and only this path signs out the ApplicationScheme too.
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ── Access denied (the cookie's AccessDeniedPath target, Program.cs) ────────────────

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ── Profile (U14: M1 bootstrap page redirected to M2's editor) ─────────────────────
    //
    // U14 (M2) closed M1's name/email bootstrap page. The M1 action's
    // read + write lanes are both superseded by /profile/edit (U11's write
    // lane, the single profile-editor surface M2 promised). The M2 plan's
    // line 184 ("the /Account/Profile route returns a 301 redirect to
    // /profile/edit, or 404 if removed") chose the 301 stub path: a
    // permanent redirect so any M1 bookmarks / mail links that still
    // reference the old address land on the live editor. The M1
    // <c>ProfileViewModel</c> (name + email + Verified) is now dead
    // surface-level code; M2's <c>ProfileEditViewModel</c> (U11) carries
    // the two audience fields (Visibility, ContactVisibility) + the two
    // M1 bootstrap fields. M3 may delete the M1 VM; U14 does not (the M2
    // plan line 183 limits U14 to the controller / nav / view).
    [Authorize]
    [HttpGet]
    public IActionResult Profile() =>
        RedirectPermanent("/profile/edit");
}
