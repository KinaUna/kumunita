# ADR 0263 — Page controller flashes are localized

Status: Accepted
Date: 2026-10-29

## Context
Next in the flash order after Posts (ADR 0262): the Page controller had 14 literal English `TempData` messages plus two sign-in `ModelState` errors.

## Decision
- The 14 flashes and the two sign-in model errors go through `this.FlashAsync(key, english)`; translation messages reuse `flash.translation_*`.
- 8 new keys in en/de/fr/da: `pages.flash_created/_updated/_published/_reset/_deleted/_moved`, `pages.flash_signin_create/_edit`.
- English floor unchanged, so existing tests keep passing; no constructor changes.

## Not covered
de/fr/da text machine-written, review parked. Remaining literal flashes: across the other controllers.

## Gate re-baseline
The improve-check (a) `KnownTranslationKeys.cs` pin re-based 12335 -> 12367 for
this lane on 2026-10-29 (the 8 new `pages.flash_*` keys x en/de/fr/da).
