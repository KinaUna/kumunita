# ADR 0203 — Signed-out auth verification

Status: Accepted

## Context
Login and Signup (ADR 0198) had not been checked signed out. Verified by
fetching the signed-out HTML (credentials omitted) into the pane: Login in
Forest dark/light and Ember light/dark, phone width, and the error, validation,
suspended-account and MFA-branch elements (injected from the view's own markup).
Signup is closed on the dev instance (SignupClosed page, which looks right);
the open form shares Login's classes and was not served.

## Decision
- `.text-danger` is 3.7:1 on the dark cards (under 4.5:1 for small text).
  In both dark palettes it is now #f28b82 (>= 6.5:1), site-wide (CSS only).
  `.text-info` was already 5.2-6.0:1 in all four combinations.

## Open
- The real MFA challenge and failed-login error were not exercised end to end
  (they need a live login, which the verification session cannot enter).
- The login logo tile is the same red-orange in both palettes (per-palette logo is an open item).
