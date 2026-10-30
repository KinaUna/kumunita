# ADR 0261 — A shared FlashAsync extension; Guardian, Announcement and Event flashes localized

Status: Accepted
Date: 2026-10-29

## Context
About 330 `TempData["info"|"error"]` messages in 29 controllers were literal English. The ~20 controllers that already localize flashes each carry a private
`FlashAsync` that needs constructor-injected `ITranslationProvider`/`ILocalizationService`; copying that into every remaining controller would change
constructors and every test that builds them.

## Decision
- `ControllerFlashExtensions.FlashAsync(this ControllerBase, key, english, params args)` (`Kumunita.Web.Security`) resolves the provider and localization service
  from `HttpContext.RequestServices`, so no constructor changes. Fail-open to the caller's English floor (no HttpContext, no request services, no provider,
  or a provider returning the key/empty); placeholders use the invariant culture. Rejected: per-controller copies, a base class.
- Converted in this patch: Announcement (17), Event (16) and Guardian (29 incl. the two time-limit flashes that read `EnValues` directly) — 49 new keys in
  en/de/fr/da (`flash.*` for the shared translation/comment/not-signed-in messages, `announcements.flash_*`, `events.flash_*`, `guardian.flash_*`).
- Tests that assert English flash text keep passing: they use a bare `DefaultHttpContext`, which has no request services, so the floor is returned.
- `ex.Message` flashes (service exception text) are untouched; they are English at the source in Core.

## Not covered
Not compiled or run (no .NET SDK here). `ControllerFlashExtensionsTests` is new and unrun. de/fr/da text is machine-written, review parked.
Remaining literal flashes: 285 across the other controllers (Projects, Groups, Posts, Admin*, Page, Community, …), plus BadRequest/ModelState messages and `ex.Message`.
