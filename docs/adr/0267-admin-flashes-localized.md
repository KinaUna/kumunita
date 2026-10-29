# ADR 0267 — Admin controller flashes are localized

Status: Accepted
Date: 2026-10-29

## Context
Nine Admin controllers (Admin, AdminSite, AdminSurfaceLabels, AdminStorage, AdminDateFormat, AdminTimezone, AdminGuests, AdminHelp, AdminQuiet) had 58 literal
English `TempData` messages: community/user/moderator/role/account actions, platform settings saves, translation messages.

## Decision
- All 58 go through `FlashAsync(key, english, args)` with the English floor unchanged. AdminGuests reuses the existing `admin.guests_saved` key; AdminQuiet
  calls the extension as `ControllerFlashExtensions.FlashAsync(this, …)` because the controller has its own private one-argument `FlashAsync`.
- 41 new keys in en/de/fr/da: 38 `admin.flash_*` and three shared `flash.translation_gone_add`, `flash.translation_gone`, `flash.choose_language_remove`
  (used by AdminSite and AdminSurfaceLabels). `flash.translation_*` otherwise reused.
- AdminPortabilityController already localizes through its own `T` and is untouched.

## Not covered
Compiled and run 2026-10-29 (build clean; `Kumunita.Web.Tests` 1156 tests, 0 failed). de/fr/da text machine-written, review parked. Admin-facing text, so lowest priority for review.
Remaining literal flashes: 49 (Account, Inventory, Kiosk, Languages, Locale, MessagingSettings, Moderation, ReportTargets, SecuritySettings, Tag, UserPortability), then BadRequest/ModelState text.
