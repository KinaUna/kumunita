# ADR 0266 — Groups controller flashes are localized

Status: Accepted
Date: 2026-10-29

## Context
The Groups controller had 60 literal English `TempData` messages (group lifecycle, membership, invitations, join requests, group posts/replies/events, translations).

## Decision
- All 60 go through `this.FlashAsync(key, english, args)`; the English floor equals the old text.
- 22 new `groups.flash_*` keys in en/de/fr/da (created, member added/removed, left, description cleared/saved, now private/public, deleted, invited, invitation
  states, join-request states, post/event added). Names and ids are `{0}`/`{1}` placeholders (e.g. "Approved {0}’s request to join “{1}”.").
- Reused: `posts.flash_*` (post/reply updates), `events.flash_updated/_published`, `flash.translation_*` and `flash.translation_needs_name_or_description`.
- The language name inside translation messages is still the seeded English language name from `SeedLanguageName` (as before).
- The `improve-check.ps1` gate's `KnownTranslationKeys.cs` pin was re-based 12587 -> 12675 on 2026-10-29 by this ADR (22 new keys x en/de/fr/da).

## Not covered
Not compiled or run (no .NET SDK here); de/fr/da text machine-written, review parked. The possessive "{0}’s request" is phrased per language (de "von {0}", fr "de {0}", da "{0}s").
Remaining literal flashes: 107 across the Admin* and other controllers.
