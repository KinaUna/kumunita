# U9 — Multilingual: the close (roadmap bump + doc sync + folder moves)

**Milestone:** multilingual (`ML`, ADR 0005) · **Register:** `docs/plans-milestones/done/plan-multilingual.md` · **Read first:** the design doc `## Multilingual — Gate (recorded by U8)` + `## Multilingual — Closed (recorded by U9)` (the fill-in target) + the **U8** section of `docs/plans-milestones/done/multilingual-handoff-notes.md`.

## Goal

**Close the multilingual lane.** U1–U7 shipped the two content documents, the read seam, the admin seam, the cookie, the Web surface, and the 19 seam tests; U8 ran + recorded the acceptance gate (370/370 green). U9 is the **governance close**: move the roadmap `ML` → *done* / `M4` → *next* (keeping `MilestonesTests.cs` in step — it currently pins `ML` as the single in-progress milestone), sync the README + `ARCHITECTURE.md` §8/§9 so they no longer describe `ML` as "next", fill the design doc's `## Multilingual — Closed (recorded by U9)` placeholder with the consistency checklist, and record the one **`/about` follow-on** U6 explicitly handed to U9 (documented, *not* shipped — it would be a new Web ADD outside this lane's sealed scope, so it is recorded for the M4 lane / a follow-up rather than added here). **Doc + one test file — build must stay green; no production code is added.**

**This unit changes no production code** (no new Core/Web seams — that would be a `## U9 — Drift pause` per unit-series rule 4). The only code-adjacent change is the **roadmap status bump** (`Milestones.cs`, data only) + the **test that pins it** (`MilestonesTests.cs`). Everything else is documentation. If the build or either test assembly goes red, the cause is a U1–U7/U8 production/test bug — record the exact failing name + suspected file in the handoff note and **stop** (do not fix production code here).

## Entry reads

1. `docs/design/multilingual-design.md` — `## Scope` (the `/about` note: `LocalizedPage` is **terms, about, help**), `§Pinned seam tests` (19 names to confirm verbatim), `## Multilingual — Gate (recorded by U8)` (the recorded counts to reference), and the `## Multilingual — Closed (recorded by U9)` **placeholder** (the fill-in target — replace its *Placeholder* italic paragraph, keep the heading).
2. `docs/plans-milestones/done/multilingual-handoff-notes.md` — **only the U8 section** (the gate run + the "handing to U9" note naming exactly this close) + the **U6 section** (the `/about` follow-on it hands to U9).
3. `src/Kumunita.Web/Milestones.cs` — the current roadmap (`ML` = `StatusNext`, `M4` = `StatusPlanned`).
4. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the two pins that must move with the bump: `Roadmap_Covers_M0_Through_M6_In_Order` (order unchanged — `ML` stays at index 5) and `Multilingual_Is_The_Single_InProgress_Milestone` (currently asserts `ML` is the single `StatusNext` — **this must flip to `M4`** once `ML` is `done`).
5. `README.md` (the `## Roadmap` section + the "Multilingual next" paragraph + the `Multilingual` feature bullet) and `docs/ARCHITECTURE.md` (§8 "Current state" / §9 "Current state", the milestone table row for `ML`/`M6`, and the `M1DocTypes` "lands with M6" comment note).
6. `src/Kumunita.Web/Controllers/StaticPagesController.cs` + `Views/` (confirm `/terms` + `/help` exist and **`/about` does not** — the `HomeController.About` product-story page is the current `/about`; the M5 seam test exercises the `terms`/`help` slugs, **not** `about`) — the evidence base for the `/about` follow-on note.

## Deliverables (5 files — 1 code data-file, 1 test, 3 docs)

1. **Modified:** `src/Kumunita.Web/Milestones.cs` — the **roadmap bump**:
   - `ML` row: `StatusNext` → `StatusDone` (title unchanged).
   - `M4` row: `StatusPlanned` → `StatusNext` (the new single in-progress milestone).
   - The `M5` / `M6` rows stay `StatusPlanned`. Order is unchanged (`M0, M1, M2, M3, GP, ML, M4, M5, M6` — `ML` keeps index 5, only its status flips). This keeps the home-page roadmap honest: `ML` shipped, `M4` is next.
2. **Modified:** `tests/Kumunita.Web.Tests/MilestonesTests.cs` — keep the pins in step with the bump:
   - `Roadmap_Covers_M0_Through_M6_In_Order` — **unchanged** (the order is identical; `ML` is still index 5).
   - `M0_Through_M3_Are_Marked_Done` — extend the `done` set to include `GP` **and** `ML` (they are now both `StatusDone`); keep asserting `M0–M3` + the two named lanes are `done`.
   - `Multilingual_Is_The_Single_InProgress_Milestone` → **rename + re-pin** to assert the **single** `StatusNext` is now **`M4`** (the events lane) — the exact mirror of what the test did while `ML` was in progress. The name becomes e.g. `Events_Is_The_Single_InProgress_Milestone` (a roadmap-test rename, **not** a §Pinned-seam-test rename — the 19 multilingual names are untouched).
3. **Modified:** `README.md` — sync the three `ML`-as-next references:
   - The `**Multilingual** next — … then **M4**: events…` paragraph → restate that multilingual is **shipped** and `M4` (events) is **next**.
   - The `## Roadmap` section: `Multilingual … **Next.**` → `**Done.**` and `**M4** — Events…(Deferred until after multilingual.)` → drop the "deferred" framing, mark it **Next.**.
   - The `Multilingual` **feature bullet** (currently "…the full admin-managed language + translation surface is **next**") → restate as **shipped** (the admin language/translation surface + settings page + static-page routes are live).
   - Leave the `## Deferred` machine-translation-of-UGC bullet **as-is** (ADR 0005 C — still deferred; out of this lane).
4. **Modified:** `docs/ARCHITECTURE.md` — the `ML`-as-next references:
   - §9 "Localization (multilingual)" **Current state** line ("…Everything below … is the **multilingual lane (`ML`)**, pulled forward from M6 to be the next lane") → restate that the `ML` lane is **shipped** (translation provider, cookie, admin surface, `LocalizedPage`) and only the `/about` static-page route remains as a follow-on.
   - §8/§3 the "lands with M6" phrasing around `TranslationResource` / `LocalizedPage` (the `M1DocTypes` "lands with M6's admin UI" note) → restate as **landed (ML)**.
   - The milestone table (§2) row that folds "multilingual" under **M6** → note that multilingual shipped on its own **`ML` lane** (named-lane precedent, like `GP`/media), independent of M6 — so M6's row reads portability/iCal/notifications/search, and `ML` is a sibling completed lane.
5. **Modified:** `docs/design/multilingual-design.md` — **fill** the `## Multilingual — Closed (recorded by U9)` placeholder (replace its *Placeholder* italic paragraph, **keep the heading**) with the consistency checklist the placeholder names: seams present (the 2 content docs, 2 interfaces + record, `LocaleCookie`, `LanguagesController`/`LocaleController`/`StaticPagesController`, the 2 `M1DocTypes` indexes) · 19 §Pinned-seam-test names verbatim in `LocalizationServiceTests.cs` · `Milestones.cs` `ML` → done / `M4` → next · README Roadmap + `ARCHITECTURE.md` §8/§9 synced · **drift-pause count = 0** (U1–U9, no `## U<m> — Drift pause` sections) · the **`/about` follow-on** recorded (see below).

**The `/about` follow-on (recorded, *not* shipped):** the design doc `## Scope` names the three static pages **terms, about, help**. U6 shipped the `/terms` + `/help` routes (via `StaticPagesController`, slug-guarded to `{ terms, help }`) but **not** `/about` — the existing `/about` route is `HomeController.About` (the product-story landing page), and U6 explicitly deferred touching `HomeController` to **U9** (unit-series rule 1). Making `/about` render a `LocalizedPage` body is a **new Web ADD** (a route + a `HomeController` change) that is **outside this lane's sealed scope** (unit-series rule 4: any other Web ADD is a drift pause). So U9 **does not ship it** — instead it records it as an open follow-on in the design-doc `## Multilingual — Closed` section + this handoff entry, naming the exact one-line `HomeController.About` + slug-guard change the M4 lane (or a small follow-up unit) would make, so it is a *known open item* rather than a silent gap. This keeps U9's diff strictly the close (roadmap + docs) and honest about the one deliberately-unshipped seam.

**Out of this unit (unit-series rules 1/2/4):** no new Core/Web seam, no new test beyond the roadmap-pin rename, no re-shaping of the two content docs / two interfaces, no ADR edit (ADR 0005 stands; this close does not open a new design question — the `/about` item is a follow-on note, not an ADR), no renumbering of M4/M5/M6 (`ML` stays a named lane).

## Exit

- `dotnet build Kumunita.slnx -c Debug` **green** (the `Milestones.cs` + `MilestonesTests.cs` change compiles; the doc edits do not).
- **Both** test assemblies green via the AGENTS.md reliable runner (**not** `dotnet test` — the xunit.v3 discovery quirk):
  - `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` — the re-pinned `MilestonesTests` passes with `M4` as the single in-progress.
  - `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` — **unchanged** 280/280 (U9 adds no Core test).
- The U9 handoff-note section appended to `docs/plans-milestones/done/multilingual-handoff-notes.md` **before** the folder move.
- This unit plan file moved to `docs/plans-milestones/done/multilingual-u09-plan.md`.

**Nothing is staged or committed — the user reviews first** (a draft commit message is provided at the end).
