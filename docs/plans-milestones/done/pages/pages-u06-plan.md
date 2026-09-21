# U6 — The translation lane live on pages (standing + display)

- **Lane:** Pages (`PG`)
- **Unit:** U6 (of U0–U07)
- **Kind:** multilingual completion (the ADR 0029 standing + the ADR 0027
  chip-swap, live on a `Page`)

## Goal

Make the **user-added translation** lane fully live on a `Page`: the
**`AddTranslationAsync` form** on the post view (the ADR 0029 standing:
GlobalAdmin ∪ Translator ∪ community-Moderator scoped by `ComponentId`), the
**ADR 0027 chip-swap** display (the authored-in variant is the default visible
one, a chip per `PageTranslation` row, click to swap) reusing the existing
post/announcement translation-swap partial **verbatim**, and the **add-only**
unique-index behavior (the `(PageId, LanguageCode)` index, U01). This is the
lane's multilingual completion — the M6 multilingual goal (ADR 0005) is "the
platform exercised in more than one language"; pages are the last UGC surface
to gain the lane.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/pages-design.md` §3.5 — the **translation** contract (the
   `PageTranslation` row shape, the ADR 0018 authored-in tag, the ADR
   0022/0026/0029 standing, the ADR 0027 chip-swap display, the add-only
   unique-index rule, the "never machine-translated" pin (ADR 0005 C)).
2. `src/Kumunita.Core/Announcements/AnnouncementService.cs` (the
   `AddTranslationAsync` lane) — the **standing matrix** to mirror (the ADR
   0029: GlobalAdmin ∪ Translator ∪ community-Moderator scoped by
   `ComponentId`) + the add-only unique-index rejection.
3. `docs/adr/0027-post-reply-translation-display-and-swap.md` — the
   **chip-swap** display to reuse verbatim (the authored-in default + a chip
   per translation row + click-to-swap) — the `Page` post view (U04's
   `Show.cshtml`) gains this exact surface.
4. `src/Kumunita.Web/Views/Announcements/…` (the translation-swap partial) —
   the **partial** the `Page` post view reuses verbatim (do **not** invent a
   second chip-swap).
5. `docs/adr/0029-announcement-translations.md` — the **standing** (the ADR
   0029 matrix) + the **add-only** rule the `Page` translation lane carries
   over.

## Deliverables (closed set)

1. **The `AddTranslationAsync` form** on the `Page` post view
   (`Views/Pages/Show.cshtml`) — a "Add a translation" section (the
   `LanguageCode` picker + the `Title?` + `Body` fields) that calls
   `IPageService.AddTranslationAsync` (U03). The standing is the ADR 0029
   matrix (U02's `CheckTranslateStanding`) — GlobalAdmin ∪ Translator ∪
   community-Moderator scoped by `ComponentId`. A plain resident sees no form
   (403 on submit).
2. **The ADR 0027 chip-swap** on the `Page` post view — the authored-in
   variant (the `Page.Body` in `Page.LanguageCode`) is the **default** visible
   one; a **chip** per `PageTranslation` row (the `LanguageCode`); click a
   chip to **swap** the visible body to that row's `Title?`/`Body`. Reuse the
   **existing** post/announcement translation-swap partial **verbatim** (do
   **not** invent a second one).
3. **The add-only unique-index behavior** — a second `AddTranslationAsync` of
   the same `(PageId, LanguageCode)` is **rejected** by the unique index (U01,
   the `pg_tr_uidx_page_lang` short-name precedent). The lane's correction
   path is the same standing *adding* the right one (the unique index prevents
   a duplicate; there is no `UpdateTranslationAsync`).
4. **`PG_*` translation tests** (`Kumunita.Core.Tests` + `Kumunita.Web.Tests`):
   - a Translator can add a `fr`/`de`/… row on a `Page`, the chip-swap renders
     it (the `Show.cshtml` shows the chip + the swapped body);
   - a second add of the same `(PageId, LanguageCode)` is **rejected** by the
     unique index (a test asserts the `UniqueViolationException` or the
     service-level `InvalidOperationException`);
   - a community-Moderator's standing is the **ADR 0029 matrix** (allowed on a
     page whose `ComponentId` is their community, **denied** on a
     flat/public page with no `ComponentId`);
   - a plain resident has **no** add-translation standing (403).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green — the ADR 0029 standing matrix + the add-only unique-index rejection
  tests pass.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green — the chip-swap render test (a `Page` post view with a `PageTranslation`
  row shows the chip + the swapped body) passes.
- A Translator can add a `fr`/`de`/… row on a page, the chip-swap renders it,
  a second add of the same `(PageId, LanguageCode)` is rejected by the unique
  index, and a community-Moderator's standing is the ADR 0029 matrix (allowed
  on their community's page, denied on a flat/public page).
- **No machine translation** (ADR 0005 C stands) — the `PageTranslation` rows
  are **user-added**, never auto-generated.
- Append a `## U6 — translation lane live (standing + chip-swap)` section to
  `pages-handoff-notes.md`.

## Notes / deviations

- **The chip-swap is the ADR 0027 partial, reused verbatim.** Do **not**
  invent a second chip-swap for `Page` — the `Post`/`Announcement`/`Page`
  post views share **one** translation-swap surface (the lane's core
  invariant: one renderer, one editor, one translation lane, one display).
- **The standing is the ADR 0029 matrix, not a new one.** A community-
  Moderator's standing on a `Page` translation is the *same*
  `moderator:{CommunityId}` claim the announcement translation lane (ADR
  0029) already uses — the new dimension is *which page*, not a new
  authorization kind. If a standing cell can't be expressed with the existing
  role claims, that's **drift-pause (c)** — a new ADR, not a silent addition.
- **Add-only, not upsert.** There is no `UpdateTranslationAsync` (the ADR
  0022 add-only rule). A wrong translation is corrected by the same standing
  *adding* the right one; the unique index prevents a duplicate. Do **not**
  add an update lane (a **new ADR** if wanted).
- **The `PageTranslation` row is the ADR 0022/0026/0029 shape** (the
  `PostTranslation` convention) — U01 declared it; this unit *lives* it (the
  form + the display + the standing + the add-only rule).
