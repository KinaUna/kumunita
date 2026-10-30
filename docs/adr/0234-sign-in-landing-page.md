# ADR 0234 — Where a sign-in lands: Home, and MFA settings after a recovery code

Status: Accepted

## Context
ADR 0232 noticed that a recovery-code sign-in ended on `/profile/edit`. Reading the controller showed it was not special to
recovery: every sign-in without a `returnUrl` (password, authenticator code, recovery code, and the "already signed in"
bounce on `GET /Account/Login`) redirected to `/profile/edit`. The "Home page" setting only hides the home intro, so there is
no landing preference to honour.

## Decision
- Password and authenticator-code sign-ins without a local `returnUrl` land on `/` (Home, the feed). The "already signed in"
  bounce on `GET /Account/Login` goes to `/` too.
- A **recovery-code** sign-in lands on `/settings/security/mfa`, so the person can re-enroll the authenticator or rotate the
  remaining codes. It takes precedence over a `returnUrl`: the code was just spent and the second factor is lost or suspect.
- A local `returnUrl` is still honoured for every other sign-in.
- Unchanged on purpose: sign-up and verify-email flows (including their "already signed in" bounces) still go to
  `/profile/edit`, which is the right first stop for a new resident.

## Tests
`F3U5_MfaLoginTests`: TOTP sign-in, non-admin sign-in and sample-admin sign-in now assert `/`; the recovery-code test asserts
`/settings/security/mfa`. E2E specs `signup_verify_login` and `post_write` wait for Home after login (the verify step still
expects `/profile/edit`). Not compiled or run in this sandbox (no dotnet, no Playwright stack).

## Follow-ups
- The harness' `IsLocalUrl` stub always returns false, so "recovery wins over a local returnUrl" has no unit test.
- Sign-up/verify bounces for an already signed-in person could go Home as well; left alone to keep this change to sign-in.
