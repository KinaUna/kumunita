# ADR 0264 — Community controller flashes are localized

Status: Accepted
Date: 2026-10-29

## Context
Next in the flash order: the Community controller had 15 literal English `TempData` messages (mandatory/optional, membership add/remove/leave, translations).

## Decision
- All 15 go through `this.FlashAsync(key, english, args)`; translation add/update/remove and "choose a language" reuse `flash.translation_*`.
- 9 new keys in en/de/fr/da: `communities.flash_mandatory/_optional/_gone/_member_added/_member_removed/_use_leave/_cannot_leave/_left` and `flash.translation_needs_name_or_description`
  (community translations need a name and/or description, unlike the text-only wording of the other controllers).
- The existing tests that pin refusal branches by an expected `NullReferenceException` from the `TempData` write are unaffected: the flash text is resolved
  first (English floor on a bare `DefaultHttpContext`), and the `TempData` assignment still throws as before.

## Not covered
Not compiled or run (no .NET SDK here); de/fr/da text machine-written, review parked. Remaining literal flashes:  across the other controllers.
