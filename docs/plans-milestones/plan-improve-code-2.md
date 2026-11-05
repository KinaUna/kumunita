---
id: plan-improve-code-2
title: "Plan: IMPROVE-CODE-2 — make the ratchet honest, then land the unmet DoD"
tier: living
kind: plan
status: proposed
audiences: [developer, agent]
related:
  - docs/archive/lanes/improve-code/plan-improve-code.md
  - docs/archive/lanes/improve-code/handoff-notes.md
  - docs/adr/0006-module-boundary-contracts.md
  - docs/adr/0286-iuserservice-access-core-split.md
  - docs/adr/0287-projects-module-split.md
  - docs/philosophy/in-code.md
lane: improve-code-2
updated: 2026-11-05
---

> **Abstract:** the successor to the `IMPROVE-CODE` lane, which closed on
> `2026-11-05` with **7 of 10 Definition-of-Done lines unmet** — its own
> cold reader (`C13`) recorded the failures honestly rather than papering
> over them, and handed the gaps to this lane. The first unit does **not**
> touch product code: it makes the ratchet gate *honest* — the check that
> flags a raised baseline without an `adr:` reason was dead in CI (it
> hardcoded `origin/main`, skipped an unreadable base, and a guard bug made
> it never flag). The units after it work the unmet DoD lines in the order
> the gate demands: the 30 unjustified raises first (the ratchet that lets
> the rest be honest), then the translation-lane collapse, the module
> surface, the still-large files, the two untested services, and the missing
> SEC pointer. The close unit lands the six-member "shipped" flip — the one
> the first lane deliberately withheld — because *this* lane finishes the
> reduction the first one promised.
> **Who is it for:** whoever picks up a `C2`-unit, and the reviewer of the
> close.
> **When to read the rest:** before starting any `C2`-unit, or before
> deciding whether a DoD line that "looks done" actually is.

# Plan: IMPROVE-CODE-2 — make the ratchet honest, then land the unmet DoD

The predecessor (`IMPROVE-CODE`, `C00`–`C13`, archived at
[`docs/archive/lanes/improve-code/`](../archive/lanes/improve-code/)) is the
second round of the IMPROVE project's code half. Its differentiation is real
and stays in the tree — the god-types are no longer god-types, the seams are
split, the doctrine is in place, and `DoD #2` (gates a/e/f retired) and
`#9` (ratchet / part-vs-whole / comment rule in one place) are met. But its
cold read found **7 of the 10 DoD lines do not hold**, and the lane is
archived *with unmet DoD* so this lane inherits the exact gaps rather than
a papered-over close.

This plan is the *reduction* half of the round — the numbers the first lane
promised but did not reach. It is measured from the tree at `development`
`ebf5b3b5` (2026-11-05), which is the predecessor's close plus the gate-fix
commit.

## Why now

The first lane opened with a gate that ratcheted *up* — every lane close that
grew a god-file raised its baseline, because the alternative was a red gate.
`IMPROVE-CODE` built a ratchet that can only go down and made it the gate.
But the cold reader found the ratchet's one teeth — the *"a `max` raised
above the base without an `adr:` fails"* check — was **dead in CI**: it
hardcoded `origin/main` (CI targets `release`, and the baseline file only
exists on `development`), it silently skipped when the base was unreadable,
and a guard made it never flag even a readable base. So the 30 baseline
raises the lane introduced stayed green.

The gate-fix commit (`ebf5b3b5`) made that check real: the base ref is now
`CODE_BASELINE_BASE` (CI sets it to the PR base; local default
`origin/development`), it **fails closed under CI** when the base is
unreadable, and the guard bug is gone. The consequence is the honest
starting position for this lane: **running the fixed gate against the
predecessor's real "before" (`311d0a4b`) reports 30 unjustified raises.**
That is the red-gate reality going forward, and it is the first unit here.

This lane does not re-open the *shape* of the work (the splits, the doctrine,
the seams) — it lands the *numbers*. The plan's own instruction, carried
forward from the predecessor: *"If any DoD line fails, stop and say which one
and why — do not fix it silently."*

## What this lane is (and is not)

- **Is:** making the ratchet gate honest in CI, then resolving the six unmet
  DoD lines from the predecessor — the 30 unjustified raises, the
  translation-method collapse, the module-boundary surface, the still-large
  files, the two untested services, and the one missing SEC pointer — and
  landing the six-member close the first lane withheld.
- **Is not:** a re-architecture (the modular monolith and the three access
  modules of ADR 0006 stay), a feature, a route or rendered-HTML change, a
  document-schema change, a comment sweep, or a re-opening of the
  part-vs-whole splits (C05/C07/C08) or the doctrine rules (C00) — those are
  met and stay.
- **Rule for every unit (the ratchet):** a baseline `max` only goes down.
  A value that must go *up* needs an `adr:` reason in the baseline entry — a
  named, defensible exception, not a silent one. Shrinking a type under the
  ceiling and having its baseline entry retire is always allowed.

## Carried-in definitions of done (the unmet lines, in the order this lane works them)

These are the six lines the predecessor's cold read did not hold, plus the
close. The "Before" column is the predecessor's measurement at close
(`219ba33c`); the "Target" column is the predecessor plan's stated line. The
gate now fails on every `max` raised without `adr:` — so a unit that must
raise anything names the ADR it amends or creates first.

| # | DoD line | Target | Before (predecessor close) |
|---|---|---|---|
| 1 | **Ratchet honest + the 30 raises resolved.** No baseline `max` rose without an `adr:` reason. | decreases/removals only, or each raise carries `adr:` | **30 raises**, none with `adr:` — 4 shrinkable god-type/method (below) + 26 boundary-coupling |
| 5 | **Translation lane collapsed.** `*Translation*` methods onto the `TranslationLane` + per-surface descriptors. | ≤40 methods (from ~132–195) · clone regions ≤5 000 | `*Translation*` methods ~132–195 (tool member-column: 195 across 46 files); clone regions 3 935 |
| 6 | **Module surface reduced.** Concrete-service edges out; controllers off Marten. | 0 concrete-service edges · Marten allowlist ≤5 | **478** boundary entries baselined · Marten allowlist **21** |
| 3/4 | **The still-large files off the list.** | files ≤8 · methods ≤45 · classes ≤3 · interfaces ≤2 · views ≤3 · TS ≤2 · the 7 named god-files gone | files **25** · methods 91 · classes 7 · interfaces **8** · views 3 · TS 0 (13 ts functions); `FirstBootSeeder` (991) · `SampleDataSeeder` (1633) · `ProjectsController` (802) still over 600 |
| 7 | **The two untested services covered.** | every service with a unit-tested part | `MailAlarms` = 0 test refs · `UsagePurge` = 0 test refs (`Suppression` already covered) |
| 8 | **The missing SEC pointer added.** | both E11 items → `// TODO ❓ SEC` with file/line refs | Moderation half done (`IModerationService.cs:97`); `ClaimTodoAsync` pointer missing |

The 4 shrinkable god-type / method raises (the `DoD #1` half that is a
*refactor*, distinct from the 26 boundary raises that are an `adr:`
decision):

| Baseline entry | Ceiling | C00 (`311d0a4b`) | Now (baselined) |
|---|---:|---:|---:|
| `Core/Events/EventService.cs` **class** (public methods) | 25 | 30 | **31** |
| `Core/Pages/PageService.cs` **class** (public methods) | 25 | 28 | **30** |
| `Core/Events/IEventService.cs` **interface** (methods) | 20 | 27 | **28** |
| one method **cc_max** (cyclomatic complexity) | 15 | 20 | **22** |

All three named types are also *over the ceiling* (class 25 / interface 20),
not merely over the predecessor's baseline — so shrinking them under the
ceiling retires the baseline entry entirely, which is the cleanest resolution.

## Decisions

| # | Decision | Chosen | Alternatives rejected |
|---|---|---|---|
| D1 | How the 30 raises are resolved | **Per-edge, not blanket.** The 4 shrinkable god-type/method raises: shrink under the ceiling where the split is a real seam (a *named* sub-service per the predecessor's own part-vs-whole rule) or give an `adr:` if the surface is genuinely warranted. The 26 boundary raises: a blanket `adr:` on the whole set is the honest default (the C06/C07/C08 splits *intended* the coupling); picking out the ones that are accidental integration and reducing them is the improvement, but each must be a *named* `adr:` entry, not a silent raise. | Shrinking all 30 (impossible for the 26 — they are real coupling, not a size problem); a blanket `adr:` with no per-edge review (the improvement the lane exists to find) |
| D2 | The 4 shrinkable god-types | **Shrink under the ceiling**, retiring the baseline entries. `EventService` (31) and `PageService` (30) split by the same discipline the predecessor applied to `UserInfoService` / `ProjectService` — a *named* sub-service per the part-vs-whole rule, each with its own unit test. `IEventService` (28) narrows to its true surface. The cc_max-22 method splits on a decision seam (a `*Policy` per the C04 pattern) or extracts its branchy helper. Only if a surface is genuinely warranted does it stay with an `adr:` — the gate then names the reason. | Retiring the ratchet or raising the ceiling (the anti-pattern the whole round exists to stop); a mechanical split that produces ceremony types (the part-vs-whole rule forbids this — the predecessor's cold read verified the *good* splits pass it) |
| D3 | The translation collapse | **Finish the C06 migration.** The `TranslationLane` + per-surface descriptors exist; the per-surface `*Translation*` methods were not collapsed onto them. One surface per commit, the method retires as it moves, the count goes down per surface. | Re-architecting the lane (the descriptors are the seam; this is the migration onto it) |
| D4 | The module surface | **Concrete edges → contracts; the 21 Marten allowlist entries reduced to ≤5.** Where a concrete `*Service` crosses a module, it becomes the interface (the C03 `INotificationEmitter` pattern) or a named `*Policy`/`*Keys`. A controller on Marten moves its document access to the service it owns; the allowlist keeps only the ones whose reason is *not* "C09 survivor." | A project split (the predecessor explicitly rejected one); a blanket "allow it" on the 21 (several are the same seam the C09 unit left open) |
| D5 | The still-large files | **Shrink the three over-600 files** (`FirstBootSeeder` 991, `SampleDataSeeder` 1633, `ProjectsController` 802) by the same data-to-resource and service-extraction the predecessor applied to their siblings. `ProjectsController` loses the route families its C08 twin (the Boards/Todos controllers) already took; the seeders follow the C02 data-move. | Raising the file ceiling (the anti-pattern); a partial extraction that keeps the entry baselined (the baseline must retire, not just shrink) |
| D6 | The two untested services | **`MailAlarms` and `UsagePurge` each get a unit-tested decision part** — a `*Policy` or pure helper with a test, per the C04/C12 pattern. This is the part-vs-whole rule applied to the *test* requirement, not a new type for ceremony. | A test that just calls the service (no decision extracted, so the seam-test rule stays unmet for them) |
| D7 | The SEC pointer | **`TodoService.ClaimTodoAsync` (`TodoService.cs:1024`) gets the `// TODO ❓ SEC` pointer** — the claim-to-do standing is an access decision outside `IAuthorizationService` that E11 flagged. The Moderation half is already done (`IModerationService.cs:97`). Behaviour unchanged; this is the pointer only. | Resolving the SEC question now (out of scope — the pointer names it for the `SEC` lane, matching the predecessor's E11 intent) |

## The units

Unit IDs are `C2-00` … `C2-09` — the "2" is the successor round, and the
"00" is the honest first step (the gate) rather than the first split. Each
unit is one commit, one named seam or one named number, and the gate must be
green before the next. A unit that raises a baseline entry writes or amends
its ADR *in the same commit* — the gate fails otherwise.

| # | Unit | DoD line | The named seam / number |
|---|---|---|---|
| C2-00 | **Ratchet honesty — verify the fixed gate in CI and resolve the 30 raises** | #1 | the gate's `CODE_BASELINE_BASE` check actually runs on a PR; the 4 shrinkable god-types retire under the ceiling (D2) *or* carry an `adr:`; the 26 boundary entries each carry a named `adr:` (D1) |
| C2-01 | **Translation lane — first surface** | #5 | the first `*Translation*` cluster collapsed onto the `TranslationLane` + its descriptor; the count drops per surface (D3) |
| C2-02 | **Translation lane — the rest of the surfaces** | #5 | `*Translation*` methods ≤40; clone regions ≤5 000 |
| C2-03 | **Module surface — concrete edges** | #6 | the 478 boundary edges: concrete `*Service` crossings become interfaces/policies; the count drops (D4) |
| C2-04 | **Module surface — the Marten allowlist** | #6 | the 21-allowlist down to ≤5; the rest move their document access to the service they own (D4) |
| C2-05 | **The still-large files** | #3/#4 | `FirstBootSeeder` / `SampleDataSeeder` / `ProjectsController` off the file list (D5) |
| C2-06 | **The two untested services** | #7 | `MailAlarms` + `UsagePurge` each a unit-tested decision part (D6) |
| C2-07 | **The SEC pointer** | #8 | `ClaimTodoAsync` carries the `// TODO ❓ SEC` pointer (D7) |
| C2-08 | **Close + independent verification** | the close | re-run the tooling from a clean state, check every DoD line against the tree, and land the six-member flip — *or* record the failures honestly and hand to `IMPROVE-CODE-3`, exactly as the predecessor's C13 did |

## The close (the six-member flip, landed *here* — withheld by the predecessor)

The predecessor's C13 close **deliberately did not land** the `AGENTS.md`
six-member contract (`Milestones.cs` / `WhatsNew.cs` / `README.md` /
`STATUS.md` / `ARCHITECTURE.md` / the two pin tests) because the lane closed
with unmet DoD — landing a "shipped reduction" would have recorded a
reduction that did not happen. That honest hold is preserved: **C2-08 lands
the six-member flip only if every carried-in DoD line holds at its close.**
The `WhatsNew` 0.86.x entry and the `Milestones` entry are added then, with
the *true* before → after — the before being the predecessor's close
(`219ba33c`), not its intent.

If any DoD line fails at C2-08, the unit does the predecessor's exact move:
record the failures, do not land the flip, and hand the remainder to
`IMPROVE-CODE-3`. The gate (now honest) is what makes that hand-off
honest-by-construction rather than by self-report.

> **The hand-off is written.** `C2-08` closed the lane **held** (DoD #3/#4
> files `23 > 8`; DoD #6 Marten-allowlist `20 > 5`); the remainder is
> [`plan-improve-code-3`](plan-improve-code-3.md) — the successor register,
> which works the two held lines (the module surface + the still-large files)
> and lands the six-member flip the predecessor withheld.

## Evidence / starting numbers (measured 2026-11-05, `development` `ebf5b3b5`)

- **Gate:** the fixed `code-check.ps1` runs `CODE_BASELINE_BASE`
  (CI → PR base; local default `origin/development`); fails closed under CI
  on an unreadable base; skips only a first-introduction baseline (file never
  on base). `improve-check.ps1` = ALL GATES PASS. Build 0 errors.
- **Baseline at close** (`tools/code/code-baseline.json`, unchanged by the
  gate fix): 25 files · 91 methods · 7 classes · 8 interfaces · 3 views · 0
  ts files · 13 ts_functions · 478 boundary_violations · 21
  controllers_marten_allowlist · seamTestMode=gate.
- **The 30 raises** (the fixed gate vs the predecessor's "before" `311d0a4b`):
  4 shrinkable god-type/method (the table above) + 26 boundary-coupling
  (`Web->Identity:User` 71→74, `Web->Tags:Tag` 7→9,
  `Projects->Authorization:Audience` 10→12, `Web->Messaging:Message` 18→21,
  …) — none carries an `adr:`.
- **The still-large files:** `FirstBootSeeder.cs` 991 · `SampleDataSeeder.cs`
  1633 · `ProjectsController.cs` 802 (all over the 600 file ceiling, all
  baselined).
- **The untested services:** `MailAlarms` = 0 test refs · `UsagePurge` = 0
  test refs.
- **The missing pointer:** `TodoService.ClaimTodoAsync` at `TodoService.cs:1024`
  (the Moderation twin is done at `IModerationService.cs:97`).
