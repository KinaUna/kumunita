# U5 — the `LanguagesController` admin surface (thin, GlobalAdmin-gated)

> **ML lane · U5 (of 9).** Sealed unit — sized for a ~64K-context fresh agent,
> one at a time. The **primary** reference is the design doc
> `docs/design/multilingual-design.md` §5 (the admin-surface actions table) +
> §Drift guard (the 10-action pin). **Secondary** is the unit register
> `docs/plans-milestones/plan-multilingual.md` (this unit's row). **Scratch** is
> `docs/plans-milestones/in-progress/multilingual-handoff-notes.md` (read the
> **U4** section — it confirms every Core seam this controller delegates to is
> already registered in the host container).

## Goal

Ship the **one** Web artifact U5 owns — `src/Kumunita.Web/Controllers/LanguagesController.cs`:
a **thin, `[Authorize(Roles = GlobalAdmin)]`-gated** controller that delegates
each `/admin/languages` action to the **one** matching `ILocalizationService`
method and maps M·7's blocked `RemoveLanguageAsync` throw to **409**. The audit
rows are the **service's** (M·6) — the controller never touches an `AccessAudit`
row. The resident settings page + static-page routes are **U6** (not this unit);
`LocaleCookie` is **U4** (already shipped).

## Entry reads (minimal; 3–5 files)

1. `docs/design/multilingual-design.md` §5 — the 10-action table (routes, verbs,
   the exact `ILocalizationService` method each delegates to) + the
   "M·7's blocked removal ⇒ **409**" note. (Authoritative.)
2. `src/Kumunita.Core/Localization/ILocalizationService.cs` — the **exact** 12
   method signatures to delegate to (shipped by U3; do not re-shape).
3. `src/Kumunita.Web/Controllers/AdminController.cs` — the `[Authorize(Roles =
   Kumunita.Core.Identity.Roles.GlobalAdmin)]` precedent, the
   `[ValidateAntiForgeryToken]` + `RedirectToAction` + `TempData` + `catch`
   idiom, and `AdminSubjectId(User)` (the `Kumunita.Sub` subject read).
4. `src/Kumunita.Web/Security/KumunitaPrincipal.cs` — `SubjectId(ClaimsPrincipal)`
   (the thin-token subject read; **not** a BCL claim, so `User.Identity.Name`
   does not work).
5. `src/Kumunita.Core/Localization/LanguageCatalog.cs` + `LanguageCompleteness.cs`
   — the shapes `Index` renders (catalog rows + the per-language completeness
   record).

## Deliverables (closed set — 1 file)

**New** `src/Kumunita.Web/Controllers/LanguagesController.cs` — the
GlobalAdmin-gated thin surface over `ILocalizationService`. One constructor
parameter: `ILocalizationService` (the **only** Core seam U5 consumes — both GET
actions and all 7 POST actions are `ILocalizationService` methods, so no
`IDocumentStore`, no `ITranslationProvider`, no audit writes here). The 10
actions, exactly the design-doc §5 table:

| Action | Verb | Route | Delegates to |
|---|---|---|---|
| `Index` | GET | `/admin/languages` | `ListLanguagesAsync` + `GetCompletenessAsync` (per-language completeness view) |
| `Add` | POST | `/admin/languages` | `AddLanguageAsync(code, nativeName, actorId)` |
| `Enable` | POST | `/admin/languages/{code}/enable` | `SetLanguageEnabledAsync(code, true, actorId)` |
| `Disable` | POST | `/admin/languages/{code}/disable` | `SetLanguageEnabledAsync(code, false, actorId)` |
| `Reorder` | POST | `/admin/languages/reorder` | `ReorderLanguagesAsync(codesInOrder, actorId)` |
| `Remove` | POST | `/admin/languages/{code}` | `RemoveLanguageAsync(code, actorId)` (M·7: default ⇒ **409**) |
| `SetDefault` | POST | `/admin/languages/{code}/default` | `SetDefaultLanguageAsync(code, actorId)` |
| `SaveTranslation` | POST | `/admin/languages/{code}/translations` | `UpsertTranslationAsync(key, code, text, actorId)` |
| `SavePage` | POST | `/admin/languages/{code}/pages/{slug}` | `UpsertPageAsync(slug, code, title, body, actorId)` |
| `PreviewPage` | GET | `/admin/languages/{code}/pages/{slug}` | `GetPageAsync(slug, code)` (the editor/preview pane) |

Shape notes (matching the `AdminController` idiom, so the Web surface is
consistent):
- **Gate:** `[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]` on
  the class (the `AdminController` precedent). The thin-token subject is read
  per action via `KumunitaPrincipal.SubjectId(User)` and passed to the service
  as the `actorId` — **never** the BCL `Identity.Name`.
- **POST hygiene:** every mutating action is `[ValidateAntiForgeryToken]`; on
  success it does `TempData["info"] = …` + `RedirectToAction(nameof(Index))`; on
  a bad `ArgumentException` it re-validates to `Index` (the `AddCommunity`
  precedent). `Remove` alone maps `InvalidOperationException` (M·7's blocked
  default removal) to **`StatusCode(409)`** — the optimistic-concurrency story
  the design doc §5 pins.
- **`Index` / `PreviewPage`** are thin reads: `Index` collects the catalog
  (`ListLanguagesAsync`) + one `GetCompletenessAsync` per enabled language (the
  M·12 completeness view); `PreviewPage` returns the one `LocalizedPage` (or
  `null` → a "no saved translation yet" note) for the editor/preview pane.
- **No audit writes, no direct `IDocumentStore` access** — M·6's one-`AccessAudit`-row
  guarantee is the service's (U3), per ARCHITECTURE.md §5 / ADR 0006-D.

**Not in U5's deliverables (out of scope for this unit):** the resident
settings page (cookie write — **U6**, `LocaleCookie`'s only touch point), the
static-page routes `/terms` `/about` `/help` (**U6**, over
`ITranslationProvider.GetPageAsync`), and the `Index`/`PreviewPage` **views**
(`Views/Admin/Languages/*.cshtml`) — U6 owns the Razor surface that binds to
this controller; U5 ships the controller seam so U6 has a stable HTTP surface to
build against. (U5's exit is build-green; the views land in U6 per the unit
register.)

## Exit

1. `dotnet build Kumunita.slnx -c Debug` is **green** (touched projects:
   `Kumunita.Web` — the controller compiles against the shipped
   `ILocalizationService` / `LanguageCatalog` / `LanguageCompleteness` shapes).
2. The **U5** section is appended to
   `docs/plans-milestones/in-progress/multilingual-handoff-notes.md` **before**
   the folder move.
3. This plan file is moved `in-progress/` → `done/`.
