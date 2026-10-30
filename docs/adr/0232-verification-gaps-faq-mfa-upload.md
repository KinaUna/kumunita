# ADR 0232 — Verification gaps: FAQ accordion in dark mode, MFA challenge, recovery code, lockout, PPTX/image upload

Status: Accepted

## Context
Five checks were still open from the UI refresh: the dark-mode MFA challenge, recovery-code sign-in, the lockout branch,
the FAQ accordion chevron in dark mode, and a real PPTX/image upload now that the Documents allow-list is open (ADR 0228).
All were run live against the local dev app (seed admin account; the resident accounts have random passwords).

## Findings
- **FAQ accordion (`_FaqAccordion`) was broken in dark mode.** Bootstrap's accordion ships light-only colours and `site.css`
  hard-coded `#1c4532` (open question) and `#46534b` (answer text). In dark the panels stayed cream, the answer text measured
  2.2:1 against the page, and the chevron was a fixed `#212529` stroke. No page renders the partial yet (it is a drop-in), so
  nobody saw it.
- **MFA challenge (leg 2 of sign-in):** card, info alert, hints, labels, Verify button and the wrong-code error all pass in
  Forest/Ember x light/dark (lowest: Verify 5.1:1 Forest dark, error text 5.3:1 light).
- **Recovery-code sign-in:** works end to end; a wrong code shows the localized "not valid" error and keeps the challenge,
  a valid code signs in once and the unused count drops 10 → 9.
- **Lockout:** the 5th wrong password shows "Account is temporarily locked. Try again in a few minutes."; the correct password
  is refused as well (no oracle). Error colour passes in all four combinations (5.3–7.5:1).
- **Upload:** PNG (`image/png`) and PPTX (`...presentationml.presentation`) are accepted; the list shows IMG and PPT tiles,
  the detail pages say "Image" / "Presentation", downloads are `attachment` with the original filename. A disallowed type
  (`application/x-msdownload`) still returns 415.
- **Gap, not fixed here:** the 415 (and the other guard statuses) from a normal browser form post return an empty body, so the
  person sees a blank page. See follow-ups.

## Decision
Only the FAQ accordion changes. In `palettes.css` (loaded after `site.css`):
- Feed Bootstrap's accordion variables from the palette tokens (`--kmb-surface`, `--kmb-ink`, `--kmb-ink-soft`, `--kmb-tint-a`,
  `--kmb-border`, `--kmb-accent`).
- Open-question text uses `--kmb-accent`; answer text uses `--kmb-ink-soft`.
- The chevron is a `currentColor` mask instead of a fixed-colour SVG background, so it follows the text colour in every combination.
- Focus ring uses `--kmb-accent`.
The hard-coded `site.css` rules stay as the pre-palette fallback.

## Verified
Contrast after the change (text on its own background): open question 9.6 / 7.9 / 4.9 / 6.9, closed question 14.8 / 12.8 /
15.5 / 13.4, answer 10.9 / 10.4 / 12.4 / 10.5 (Forest light, Forest dark, Ember light, Ember dark); the chevron's colour equals
the button text colour in all four.

## Follow-ups
- Form posts that fail a guard (type, size, empty) should re-render the form with a message instead of a bare status.
- Recovery-code sign-in lands on `/profile/edit`; confirm that is the intended post-recovery destination.
- Info alerts are Bootstrap teal in Ember; they could take a palette tint.
