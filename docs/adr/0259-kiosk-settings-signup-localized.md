# ADR 0259 — Kiosk Settings and Account/Signup are localized

Status: Accepted
Date: 2026-10-29

## Context
The hard-coded-English scan (after ADR 0258) listed Kiosk/Settings and Account/Signup as the next views. Kiosk Settings was fully English (title, headings,
hints, table headers, option labels, aria-labels, placeholder, tip, buttons); Signup had the page title, the bot-check and disallowed-domain messages,
the "resend confirmation" line and the "Sign in" link in English.

## Decision
- 29 new keys in en/de/fr/da: `kiosk.settings.*` (page_title, intro, display_heading, view/refresh interval hints, playlist_heading, col_view/community/label,
  kind_none/board/calendar/todos, cal_day/week/month, community_placeholder, aria_slide_* with a `{0}` slide number, tip, save, preview) and
  `account.signup_bot_failed/_domain_disallowed/_resend_prompt/_resend_link`.
- `kw-l` for text content; attributes, option placeholder and the title come from server-side variables with an English floor when there is no provider.
  Slide aria-labels use `string.Format(label, i + 1)`.
- The Kiosk intro is one plain-text paragraph: the two inline `/kiosk` links are dropped (the Preview button at the bottom already links there), because
  `kw-l` escapes its content and cannot carry markup. `<em>—</em>` emphasis is dropped for the same reason.
- Existing keys reused: `kiosk.settings.display_mode` (aria-label), `account.signup_title`, `account.login_submit`.

## Not covered
Applied and build- + test-verified in-repo (solution builds 0 errors; `Kumunita.Web.Tests` 1152/0 failed, `Kumunita.Core.Tests` 1621/0 failed). de/fr/da text is machine-written, review parked.
The improve-check gate (a) ceiling for `KnownTranslationKeys.cs` was re-based 11859 → 12091 for these twenty-nine new keys (the same shipped-additions mechanism the ADR 0258 close used; annotated in the gate).
Remaining from the scan: Guardian status labels, controller flash/error messages, admin pages, Page/Index and page-tree leftovers.
