# U7 — Absorb complete: retire `LocalizedPage` (destructive, **last**)

- **Lane:** Pages (`PG`)
- **Unit:** U7 (of U0–U07) — **the only destructive unit; runs only after
  U01–U06 are green** (drift-pause (e), the sequencing invariant)
- **Kind:** retirement (delete the old surface — the old store path is removed,
  the `StaticPagesController` routes remain, now reading the tree)

## Goal

**Only after U01–U06 are green:** delete the `LocalizedPage` doc + its
`M1DocTypes` registration + `ITranslationProvider.GetPageAsync` /
`FindPageByImageIdAsync` + `ILocalizationService.UpsertPageAsync` + the
`StaticPagesController`'s *fallback-to-`LocalizedPage`* path (it now reads the
tree) + any `KnownTranslationKeys` page-string entries that were
page-specific. The old `StaticPagesController` routes **remain** (they now
resolve to the tree); only the *old store path* is removed. **The data
migration is a no-op in effect** — the seeded pages (U05) already carry the
`en` bodies, so removing `LocalizedPage` loses nothing a resident can see.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/pages-design.md` §3.9 — the **absorb migration** ordering
   (create the `Page`/`PageTranslation` tables, seed the three pages, keep
   `LocalizedPage` read-only + non-destructive for one release, then **delete
   the `LocalizedPage` surface** in U07 once the new route is green).
2. `docs/plans-milestones/done/pages/pages-handoff-notes.md` — **read the whole
   log** (the `## U1`…`## U6` sections) to confirm all six prior units are
   green + to catch any drift pause that defers work to U07. **Do not run
   U07 if any of U01–U06 is not green** (drift-pause (e), the sequencing
   invariant).
3. `src/Kumunita.Core/Localization/LocalizedPage.cs` +
   `src/Kumunita.Core/Localization/ITranslationProvider.cs` — the **old
   surface** to retire (`GetPageAsync` / `FindPageByImageIdAsync`).
4. `src/Kumunita.Core/Localization/LocalizationService.cs` (~line 590) — the
   `UpsertPageAsync` seam to retire.
5. `src/Kumunita.Web/Controllers/StaticPagesController.cs` — the
   **fallback-to-`LocalizedPage`** path to remove (the routes remain, now
   reading the tree via `IPageService.GetByPathAsync`).

## Deliverables (closed set)

1. **Delete `LocalizedPage`** — `src/Kumunita.Core/Localization/LocalizedPage.cs`
   (the doc) + its **`M1DocTypes`** registration (the
   `.UniqueIndex(p => p.Slug, p => p.LanguageCode)`).
2. **Delete `ITranslationProvider.GetPageAsync` / `FindPageByImageIdAsync`**
   (+ their impls in `LocalizationService`) — the page-specific read seams.
3. **Delete `ILocalizationService.UpsertPageAsync`** (+ its impl) — the
   admin/translator page-write seam (the `Page` write lanes, U03, replace it).
4. **Retarget `StaticPagesController`** — remove the *fallback-to-
   `LocalizedPage`* path; `/terms`/`/help`/`/about` now resolve **only** via
   `IPageService.GetByPathAsync` (the tree) with the `/about` product-story
   `HomeViewModel` fallback (U05) when the `about` page is genuinely absent.
   The **routes remain** (backward-compatible); only the *old store path* is
   gone.
5. **Remove any `KnownTranslationKeys` page-string entries** that were
   page-specific (the `LocalizedPage`-specific UI strings) — leaving the
   generic ones intact.
6. **The ADR 0039 "Consequences" note** — confirm the
   "`LocalizedPage` retired in U07" line is accurate (it is, by this unit).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- **Both** test assemblies green:
  - `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  - `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
- `grep` shows **zero** remaining `LocalizedPage` references in `src/`
  (only the ADR/design-doc historical mentions remain — those are prose, not
  code).
- `/about`/`/terms`/`/help` still resolve and render (now from the tree); the
  about slot still mounts the page; the `Milestones.cs` `PG` row is flipped to
  **`StatusDone`** (and the README Roadmap `PG` row to **Done**) **in the same
  commit** — the lane is **shipped**. `MilestonesTests` re-verified (the
  single-in-progress pin: M4 is now the *only* `StatusNext`; `PG` is
  `StatusDone`).
- Append a `## U7 — absorb complete — LocalizedPage retired` section to
  `pages-handoff-notes.md`, and move the `pages/` folder `in-progress/` →
  `done/` (**last**, after the handoff section is appended).

## Notes / deviations

- **This is the only destructive step in the lane, and it is last.** The
  "no destructive step until the replacement is proven" discipline (drift-pause
  (e)) is the **sequencing invariant**: a fresh agent who picks up this plan
  file must verify U01–U06 are green (the handoff log) before touching a line.
  If any prior unit is not green, **stop** and append `## U7 — BLOCKED`
  naming the failing unit.
- **The data migration is a no-op in effect.** The seeded pages (U05) already
  carry the `en` bodies, so removing `LocalizedPage` loses nothing a resident
  can see. A fresh instance's `/about`/`/terms`/`/help` are byte-identical to
  pre-U07 (the seed is idempotent, U05).
- **The `StaticPagesController` routes remain.** Only the *old store path* is
  removed — a resident hitting `/about` still gets a page (now from the tree,
  or the product-story fallback if the seed is broken). Nothing a resident
  can see is lost; the *code* is what shrinks.
- **The `PG` milestone flips to `StatusDone` here.** U00 added it as
  `StatusPlanned`; U07 is the unit that ships the lane, so U07 flips it to
  `StatusDone` + updates the README Roadmap `PG` row to **Done** + re-verifies
  `MilestonesTests` (the single-in-progress pin: M4 is the only `StatusNext`).
  This is the close's governance step (the RE lane's U08 analog).
- **`grep` gate is the load-bearing exit.** Zero `LocalizedPage` references in
  `src/` (code) is the definition of "retired." The ADR/design-doc mentions
  are prose and remain (they're the historical record). If `grep` finds a code
  reference, the retirement is **incomplete** — fix it before the folder move.
