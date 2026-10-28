# ADR 0215 — MFA challenge page translations

Status: Accepted

## Context
ADR 0214 left the sign-in MFA challenge (Login view, MfaPending branch) and its four model-error
messages (AccountController.LoginMfa) as hard-coded English. The settings-side MFA page stays plain
English on purpose (see that view's header comment); the challenge is part of sign-in, which is localized.

## Changes
- Ten new keys in en/de/fr/da (informal register, as the neighbouring account keys):
  `account.mfa.challenge_intro`, `code_label`, `code_hint`, `recovery_label`, `recovery_hint`,
  `verify`, and `error.invalid`, `error.empty`, `error.locked`, `error.revoked`.
- `Account/Login.cshtml`: the MFA block uses `<kw-l>` for the intro (after the bold email), both
  labels, both hints and the Verify button.
- `AccountController`: `MfaTextAsync(key, english)` resolves the effective language and the
  translation from the request scope (the `LocalizeValidationAsync` idiom); the English text is the
  floor when there is no provider (unit-test harness) or the lookup fails. The four
  `ModelState.AddModelError` calls in `LoginMfa` use it.

## Notes
- The intro keeps the bold email outside the key ("<b>email</b> has two-factor ..."), which reads
  fine in all four languages but fixes the word order around the address.
- German, French and Danish text is machine-written; have a native speaker glance at it.

## Not verified
Nothing was compiled or run (no dotnet); the translation parity and closure tests should be run.
