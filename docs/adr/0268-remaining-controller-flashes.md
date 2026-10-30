# ADR 0268 — Remaining resident-facing controller flashes are localized

Status: Accepted
Date: 2026-10-29

## Context
After the Admin controllers (ADR 0267), 49 literal flash messages remained in Account, Inventory, Kiosk, Languages, Locale, MessagingSettings, Moderation,
ReportTargets, SecuritySettings and Tag (UserPortability and AdminPortability already localize through their own `T` helper).

## Decision
- 44 of the messages move to `FlashAsync(key, english, args)` (English floor unchanged); the 5 Locale ones use that controller's own private `FlashAsync(key)`
  with a new `settings.profile_unavailable` key, because its private overload would otherwise capture the extension's English argument.
- 34 new keys in en/de/fr/da: `account.flash_*` (3), `inventory.flash_*` (4), `kiosk.flash_*` (4), `languages.flash_*` (8), `settings.profile_unavailable`, `messaging.flash_*` (2),
  `moderation.flash_*` (5), `security.flash_*` (5), `tags.flash_*` (2). `flash.report_submitted` and `flash.translation_choose_language` reused.
- The only literal `TempData` assignments left are the five `T(...)`-based portability failure messages, which are already localized.

## Not covered
Not compiled or run (no .NET SDK here); de/fr/da text machine-written, review parked. Remaining literal user-facing English in controllers:
91 `ModelState.AddModelError` messages and 8 `BadRequest("…")` bodies, plus service exception text surfaced via `ex.Message` (English at the source in Core).

Compiled and run 2026-10-29 (build clean; `Kumunita.Web.Tests` 1156 tests, 0 failed).

The improve-check.ps1 `KnownTranslationKeys.cs` pin was re-based 12839 -> 12975 on 2026-10-29 for the 34 new keys x en/de/fr/da of this lane (the ADR 0265/0266 shipped-additions mechanism).
