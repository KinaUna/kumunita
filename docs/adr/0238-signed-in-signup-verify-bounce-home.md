# ADR 0238 — The already-signed-in bounce on sign-up and verify goes Home

Status: Accepted

## Context
ADR 0234 made sign-in land on Home and deliberately left sign-up and verify on `/profile/edit`, because a new resident's first stop
should be their profile. That reasoning does not hold for the "already signed in" bounce: a person who is signed in and opens
`/Account/Signup` or a verify link (a second tab, an old email) is not a new resident, and `/profile/edit` is a surprising place to land.

## Decision
- `GET /Account/Signup`, `GET /Account/Verify` and `POST /Account/Verify` redirect an authenticated person to `/` (Home).
- The real post-verification landing for a new resident (the redirect after the cookie sign-in at the end of the verify POST) stays
  `/profile/edit`, unchanged.
- Tests in `AccountControllerSignupGateTests`: the three bounces return `RedirectResult("/")`; the signup bounce does not read the
  sign-up gate. The harness gains a `signedIn` flag.

## Not covered
Razor and C# were not compiled or run in the authoring environment. The e2e specs do not exercise the bounce.
