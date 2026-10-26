# ADR 0176 — One notice strip instead of stacked banners

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0175 (UI refresh), ADR 0110 (what's-new), ADR 0132 (onboarding banner), ADR 0006-D (pinned announcement)

## Context

Up to four notices could stack above the page: the pinned announcement, the guest notice,
the "Finish setting up your account?" bar and the what's-new toast. On a phone they filled
the first screen before any content.

## Decisions

**D1 — One strip, one notice at a time.** The layout wraps the notices in `.kmb-notices`.
`notice-strip.js` shows the first notice that is not dismissed, in DOM order (priority:
pinned > guest > onboarding > what's new) and folds the rest behind a quiet `+N` pill that
cycles through them. Without JS every notice renders, compact.

**D2 — Dismissal stays with each notice.** Pinned and onboarding keep their sessionStorage
keys; what's-new keeps `kumunita-whatsnew-seen-version` (localStorage). The strip only reacts
to hidden/removed notices and to close clicks. Dismissing one reveals the next.

**D3 — What's new becomes an inline notice.** It was a fixed top-right toast that recorded
the version when first shown and auto-hid after 8 s. Queued behind another notice that would
mark it "seen" unseen, so it now records the version only when the resident closes it.

**D4 — Slim single line.** 44px min height, text truncated with an ellipsis, "All
announcements" hidden below `md`; the full text is one tap away. Colours come from the
palette tokens (`--kmb-surface`, `--kmb-accent`).

**D5 — Out of scope.** `_AdminOnboardingBanner` (admin pages) and the flash toast are
unchanged. Embed and kiosk modes hide the strip.

## Consequences / open work

- Behaviour change to verify: the what's-new notice no longer auto-hides.
- `a11y.notices_more` added in en/de/fr/da.
- Tests: none yet for the strip (client-only); add a layout render test if desired.
