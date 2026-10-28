# ADR 0198 — Account pages

Status: **Proposed** (scaffolding landed)
Date: 2026-10-28
Relates to: ADR 0197 (profile), ADR 0182 (forms)

## Decision

- The short account pages (Login, Signup, Verify, ResendVerification, ChangePassword, ChangePasswordLocked, Delete,
  SignupClosed, AccessDenied) keep their `col-md-6 col-lg-5` column and gain one inner `div.kmb-auth`: a centred surface
  card (`--kmb-surface`, `--kmb-border`, 16px radius, 2rem padding, 1.25rem on phones). No form, field or message
  markup changed, so MFA, bot-challenge and validation behaviour is untouched.
- Login, Signup, Verify and ResendVerification show a small logo tile (`.kmb-auth-logo`, `--kmb-logo-tile`) above
  the heading; it is `aria-hidden`. The tile is the same placeholder as the nav until the real per-palette logo exists.
- Inside the card: labels in the heading font, 12px inputs on `--kmb-control-border`, the form's submit as a full-width
  button, the links below centred and muted, alerts with the input radius.
- My storage keeps its three stat tiles, now `.kmb-detail-card.kmb-stat` instead of `text-bg-light`.

## Open

- Login/signup were not previewed signed out (the preview session is signed in); the change-password page was checked
  with the same CSS injected. Verify Login (password and MFA branches), Signup with errors, and Verify in both palettes,
  light and dark, at phone width.
- Account > Portability pages and the settings tab strip are not covered; the Settings tab strip already follows the pills.
- The Delete page keeps `btn-danger`; the Bootstrap red is not palette-tuned.
