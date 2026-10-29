# ADR 0269 — Controller ModelState validation messages are localized

Status: Accepted
Date: 2026-10-29

## Context
110 `ModelState.AddModelError(..., "English")` calls (91 single-line, 19 multi-line) in ten controllers (Account, Announcement, Document, Event, Groups, Guardian, Inventory, Posts, Profile,
Projects) rendered English validation and sign-in/permission text under every locale. Many messages repeat across controllers.

## Decision
- Each call becomes `ModelState.AddModelError(field, await this.FlashAsync(key, "English"))` — the same shared extension as the flashes (ADR 0261), English floor
  byte-identical, so tests that assert the English validation text are unaffected.
- 63 distinct messages → 63 new `validation.*` keys in en/de/fr/da (one key per distinct text, reused across controllers: "A title is required.",
  "Audience mode is required (Any or All).", the end-after-start messages, …); `flash.not_signed_in` and `flash.comment_needs_text` reused.
- Multi-line calls (concatenated literals, the `{ex.Domain}` interpolations → `{0}`, the lockout ternary) are converted too; `account.signup_bot_failed` is reused for the security-check message. The four `MfaTextAsync` calls were already localized.
- Sign-in and permission messages are one key per action (`validation.signin_post_edit`, `validation.permission_board_create`, …) rather than a "{0}" template, so each
  language controls its own grammar.
- A script confirmed every call site sits inside an async method before the edit was written.

## Not covered
Not compiled or run (no .NET SDK here); de/fr/da text machine-written, review parked. Remaining literal English in controllers: 8 `BadRequest("…")` bodies and
service exception text surfaced via `ex.Message` / `ModelState` (English at the source in Core). Client-side (TypeScript) messages were not part of this scan.
