---
id: adr-0280
title: "The resident's self-serve change-password surface moves off its own settings tab onto the Security tab (co-located with MFA — one tab holds both credential-security options)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/archive/lanes/sessions/sessions-handoff-notes.md]
supersedes: [adr-0138, adr-0174]
superseded_by: []
updated: 2026-11-02
---

# ADR [0280](0280-change-password-into-security-tab.md) — The resident's self-serve change-password surface moves off its own settings tab onto the Security tab (co-located with MFA — one tab holds both credential-security options)

Status: Accepted
Date: 2026-10-31
Amends: 0138 (its **resident surface** — the self-serve change-password form
and its `/account/password` GET + `POST` lane — is re-homed onto the Security
tab; the four-conjunct *lock decision* (`IsChangePasswordLockedForAsync`) and
the `LocaleSettings.SamplePasswordChangeLocked` additive-field shape are
**unchanged** — ADR [0138](0138-sample-data-change-password-lock.md) is the source of the lock rule, this ADR does not
restate it, and the lock still applies identically to the now-relocated form).
Amends: 0174 (the per-resident **Security tab** at
`/settings/security/mfa`, `SecuritySettingsController` — it gains a
change-password section, so the tab now holds **both** credential-security
options: the MFA enrollment / recovery-rotation / disable surface *and* the
change-password form). Amends: 0194 (the sample-account **MFA lock** and the
sample-account **change-password lock** are two independent `GlobalAdmin`
toggles on the *same* Security tab — a resident who is MFA-locked but not
password-locked still keeps their change-password lane, and vice-versa; the
`MfaLocked` page now also renders the change-password section).

## Context

ADR [0138](0138-sample-data-change-password-lock.md) landed the resident's self-serve change-password surface as a
**standalone settings tab** (`nav.change_password` in `_SettingsTabs.cshtml`,
`/account/password` on `AccountController`) — a natural home at the time,
mirroring the ADR [0028](0028-guardian-controls-account-scope-supervision.md) "Children" / ADR [0080](0080-settings-surface-split.md) settings-tab idiom. ADR [0174](0174-mfa-qr-code-and-security-tab.md)
then landed the per-resident **Security tab** (`/settings/security/mfa`,
`SecuritySettingsController`) as the home of the MFA enrollment +
recovery-rotation + disable surface (previously reached only by deep-linking
the URL). ADR [0194](0194-sample-data-mfa-lock.md) added the sample-account MFA lock as the ADR [0138](0138-sample-data-change-password-lock.md)
change-password lock's MFA analog — the two credential-security options
(password + two-factor) now live on **two separate tabs**, and the two
sample-account locks are independent `GlobalAdmin` toggles that a resident
hits independently.

A resident managing their account security had to jump between two tabs
(Change password, then Security) for what is conceptually one thing:
"keep my account credentials safe." There is no data-model reason the two
surfaces are separate — they share the same `SecuritySettingsController`
authorization posture, the same `IIdentityService` seam family
(`ChangePasswordAsync` / `IsChangePasswordLockedForAsync` vs the MFA token
seams), and the same "the security stamp rotates on a write, re-stamp the
live session" concern. The only reason they were separate was historical
(ADR [0138](0138-sample-data-change-password-lock.md) shipped before ADR [0174](0174-mfa-qr-code-and-security-tab.md) created the Security tab).

This ADR consolidates the two: the change-password form now renders **on the
Security tab** (the same page as MFA), and the standalone "Change password"
tab is removed. The write lane moves from `AccountController.ChangePassword`
(`/account/password`) to `SecuritySettingsController.ChangePassword`
(`/settings/security/password`), and the ADR [0138](0138-sample-data-change-password-lock.md) four-conjunct lock still
gates it identically (the `IsChangePasswordLockedForAsync` read is now
`SecuritySettingsController.IsPasswordChangeLockedAsync`, delegating to the
same Core seam).

## Decision

### The surface is one tab, not two

The Security tab (`/settings/security/mfa`, the `Mfa` view) renders **both**
credential-security options: the MFA surface (the existing ADR [0174](0174-mfa-qr-code-and-security-tab.md) / 0194
enrollment / recovery-rotation / disable) **and** a new change-password
section (the ADR [0138](0138-sample-data-change-password-lock.md) form, the `_PasswordChange` partial). The standalone
"Change password" tab is removed from `_SettingsTabs.cshtml` (the
`nav.change_password` tab row + the `("Account", "ChangePassword")` active
case), so the settings strip no longer carries two tabs that are both about
the account's credentials.

### The write lane moves to `SecuritySettingsController`

`SecuritySettingsController.ChangePassword` —
`POST /settings/security/password` (the `ChangePasswordViewModel` form model
is unchanged) — is the single self-serve write lane. It mirrors the ADR [0138](0138-sample-data-change-password-lock.md)
`AccountController.ChangePassword` POST shape, with one idiom difference:
where the AccountController lane **re-rendered** the form on a failed
write (a wrong current password / a validation error), this lane
**redirects-and-flashes** (the `TempData` flash, the `_FlashToast` surface)
back to the `Mfa` page. The reason: a re-render of `Mfa()` would **re-mint
the one-time MFA secrets** (`GenerateNewAuthenticatorKey` + the 10 recovery
codes — the enrollment leg's "shown once" invariant), which must not happen
on a failed password change. Redirecting to the `Mfa` page (a steady
count-only GET) leaves the MFA secrets untouched. The write itself
(`identity.ChangePasswordAsync(subject, new, byAdmin: false)`) and the
sign-out + redirect-to-login on success are unchanged from ADR [0138](0138-sample-data-change-password-lock.md).

### The ADR [0138](0138-sample-data-change-password-lock.md) lock still applies, identically

`SecuritySettingsController.IsPasswordChangeLockedAsync` reads the
`IIdentityService.IsChangePasswordLockedForAsync` seam for the signed-in
subject (the ADR [0138](0138-sample-data-change-password-lock.md) four-conjunct decision — `SampleData__Enabled` + the
admin password-lock gate + sample-account membership + the sample-
`GlobalAdmin` exemption). The GET (the `Mfa` / `MfaLocked` surface carries a
`PasswordChangeLocked` flag, and the `_PasswordChange` partial renders the
"password changes are locked" notice in place of the form when it is set)
and the POST (a locked account is denied before any write) agree by
construction, exactly as ADR [0138](0138-sample-data-change-password-lock.md) prescribed.

### The two sample-account locks stay independent

The MFA lock (ADR [0194](0194-sample-data-mfa-lock.md), `IsMfaLockedForAsync`) and the change-password lock
(ADR [0138](0138-sample-data-change-password-lock.md), `IsChangePasswordLockedForAsync`) are **independent** `GlobalAdmin`
toggles. A resident who is MFA-locked but not password-locked still sees
the change-password form on the Security tab (the `MfaLocked` page now
carries a `PasswordChangeLocked` flag too, and renders the
`_PasswordChange` partial); a resident who is password-locked but not
MFA-locked still sees the full MFA enrollment surface (the `Mfa` page's
`PasswordChangeLocked` flag is `true`, but the MFA section is unaffected).

## Not covered

- The **GlobalAdmin** reset-a-resident's-password surface (the
  `AdminSampleDataController` / admin-lane `ChangePasswordAsync(subject, new,
  byAdmin: true)` write) is **unaffected** — it is a different lane
  (admin-scoped, `byAdmin: true`) and does not live on the resident's
  Security tab.
- The `ChangePasswordLockedViewModel` type is **retained** (it is still
  referenced by doc-comments in `DeleteAccountViewModels.cs` and
  `AccountController.cs` as the ADR [0138](0138-sample-data-change-password-lock.md) "surface-replaced-by-notice"
  precedent); the standalone `ChangePasswordLocked` *view* (the
  `Views/Account/ChangePasswordLocked.cshtml` page) is removed — the
  "password changes are locked" notice now renders inside the Security tab's
  `_PasswordChange` partial (a section, not a page).
- No new translation keys are added — the surface reuses the existing
  `account.change_password_*` + `account.flash_password_changed` +
  `validation.current_password_wrong` keys (already registered in
  `en` / `de` / `fr` / `da`, ADR [0138](0138-sample-data-change-password-lock.md)). The now-unused `nav.change_password`
  tab-label key remains registered (a dead key is harmless — the
  `KwLRegistryConsistencyTests` is used→registered, not the reverse),
  registered for any future re-homing.
