# ADR 0214 — Login and MFA challenge fixes

Status: Accepted

## Context
A live check of the failed-login and MFA screens (local app, a sample resident with MFA on)
found one functional bug and four smaller issues.

## Changes
- **Retry bug (`AccountController.LoginMfa`).** A wrong code called `SignOutAsync`, discarding the
  pending `TwoFactorUserId` cookie. The challenge re-rendered with "Try again", but a retry with
  the right code could never succeed; every typo forced the password leg again. The cookie is now
  kept. Identity's leg-2 methods already count each failed TOTP or recovery code towards lockout
  (`AccessFailedAsync`), so a lockout (`IsLockedOut`) signs out and returns to the password form
  with a message. Two tests added (`F3U5_MfaLoginTests`).
- **Wrong code cleared.** The typed TOTP/recovery code is no longer echoed back after a failure
  (the model and its ModelState entries are cleared).
- **Announcement and focus (`Account/Login.cshtml`).** The error alert and both validation summaries
  get `role="alert"`; the code field is autofocused on the challenge, the password field after a
  failed sign-in (`autofocus` attribute, no script).
- **Challenge page.** The sign-up and first-boot-setup links are hidden while the MFA challenge shows.
- **Status text (`SecuritySettings/Mfa.cshtml`).** "recovery code s" (a space before the plural
  suffix) is now "recovery codes". The MFA settings strings stay plain English on purpose
  (see the view's header comment); the challenge page strings are still not translated.

## Not verified
Nothing was compiled or run (no dotnet in the scratch environment); run the two new tests and the
existing F3U5/F5U4 MFA tests. The lockout branch was reasoned from Identity's behaviour, not exercised live.
