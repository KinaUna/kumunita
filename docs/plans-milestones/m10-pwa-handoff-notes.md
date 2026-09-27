# M10 handoff notes

One `## U#` section per unit, **appended, never rewritten** (the shared
scratch tier of the three-tier contract — see the register header). Each
entry: files written/touched, decisions locked or refined (with the
design-doc / ADR section it maps to), anything that drifted from the plan's
text, and the next unit's entry reads.

The register is `docs/plans-milestones/plan-m10-pwa-responsive.md`. Each
unit ships its own self-contained plan in `in-progress/pwa-responsive-uNN.md`;
when a unit is done its plan file moves to `done/`. The **next unit's** agent
reads its own unit plan + this file's most recent `## U#` section + its entry
reads — it does not re-derive the register.

## Kickoff — M10 is the in-progress milestone; register + unit plans authored

- **What this is.** M10 (PWA and responsive design) is already the single
  `StatusNext` milestone in `Milestones.cs` (the M9 U07 close moved it
  there; no M10 work has started — `in-progress/` is empty and there is no
  `src/Kumunita.Core` messaging-*adjacent* PWA surface, no manifest, no
  service worker, no `pwa-install.ts` anywhere: grep-confirmed greenfield).
  This kickoff records the register's authoring, not a milestone-status
  change (M10 is already `StatusNext` — the flip happens at **U07 close**,
  when M10 → `StatusDone` and M11 → `StatusNext`).
- **Files touched (this kickoff: the plan tier only):**
  - `docs/plans-milestones/plan-m10-pwa-responsive.md` — the unit register
    (U00–U07), [PROPOSED] D1–D10 + C-M10·1–8 + F1–F7, per-unit Goal /
    Entry reads / Deliverables / Pinned tests / Exit.
  - `docs/plans-milestones/in-progress/pwa-responsive-u00.md` …
    `pwa-responsive-u06.md` — the seven self-contained unit plans (U00–U06;
    U07's close is defined in the register's `## U07` section directly —
    the close unit reads it there, no separate in-progress file).
  - `docs/plans-milestones/m10-pwa-handoff-notes.md` — this file.
- **Open veto window:** D1–D10 in the register are the [PROPOSED] set the
  user can still change cheaply **before U00 runs**. After U00 they are
  locked by `docs/design/m10-pwa-responsive-design.md` + ADR 0107.
- **ADR number for M10:** **0107** (the index ran 0001–0106; `0107` was
  free — verified against `docs/adr/README.md`; `0106` is the to-do
  self-assign lane, the current highest). U00 will author
  `docs/adr/0107-pwa-and-responsive-design.md` + the `docs/adr/README.md`
  row.
- **Grounding facts verified at kickoff (U00's entry reads should re-verify,
  not trust this):** Bootstrap 5.3 precompiled dist CSS + the `.kmb-*`
  design-system layer in `site.css`; six existing
  `@media (max-width: 767.98px)` blocks at `site.css` lines 578 / 1058 /
  1407 / 1694 / 2105; CSP shipped in `Program.cs` (~line 486–512,
  `script-src 'self'`); the tsc-only pipeline (`package.json` "build": "tsc",
  `client/**/*.ts` → `wwwroot/js`); the logo PNGs are only 42×42 + 64×64
  today (the manifest's 192/512 pair are new assets); the Playwright harness
  config is `tests/Kumunita.Web.Tests/playwright.config.ts` (`baseURL`
  `http://localhost:5199`, the M3/M4 author-not-run precedent's specs live
  there); `Milestones.cs` M10 = `StatusNext`, M11–M14 `StatusPlanned`.
- **Not touched (historical records, per the "don't edit done records"
  discipline):** the M6/M9 deferral notes that name "push / PWA push (M9
  owns PWA)" (ADR 0076/0083/0084 + the `done/notifications/` handoff) —
  those are done-milestone records; M10's design doc *supersedes* the
  deferral by owning the substrate and re-naming the follow-on lane "PWA
  push" (each named in the register's Deferred lanes list).
- **Next:** U00 (see `in-progress/pwa-responsive-u00.md`).
