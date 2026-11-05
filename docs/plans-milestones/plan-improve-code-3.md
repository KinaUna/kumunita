---
id: plan-improve-code-3
title: "Plan: IMPROVE-CODE-3 — finish the two held lines, then land the six-member flip"
tier: living
kind: plan
status: proposed
audiences: [developer, agent]
related:
  - docs/plans-milestones/plan-improve-code-2.md
  - docs/plans-milestones/improve-code-2-handoff-notes.md
  - docs/adr/0290-improve-code-2-baseline-raises-and-seams.md
  - docs/adr/0291-improve-code-2-c2-03-carry-the-9-concrete-service-crossings.md
  - docs/philosophy/in-code.md
lane: improve-code-3
updated: 2026-11-05
---

> **Abstract:** the successor to the `IMPROVE-CODE-2` lane, which closed
> **held** — its close unit (`C2-08`) re-verified every carried-in DoD line
> against the tree from a clean state and, because two lines do not hold,
> **deliberately did not land the six-member "shipped" flip** (the
> `AGENTS.md` contract: `Milestones.cs` / `WhatsNew.cs` / `README.md` /
> `STATUS.md` / `ARCHITECTURE.md` / the two pin tests). Five of the seven
> lines hold (the ratchet is honest, the translation lane is collapsed, the
> concrete-`*Service` crossings are down to 9 named, the two untested services
> are covered, both E11 SEC pointers are in); the two that fail are the module
> surface (the Marten allowlist at **20**, target **≤ 5**) and the
> still-large files (**23** over the 600 ceiling, target **≤ 8**). This lane
> inherits exactly those two gaps — the handoff names the resolving units for
> each — and works them in the order the dependency demands: the god-service
> shrinks first (they retire the still-large-file entries and, for the services whose controllers are on the allowlist, unblock those keeps), then the allowlist, then
> the remaining large files and the boundary remainder, and — only if **every**
> carried-in DoD line holds — lands the six-member flip the predecessor
> withheld. If any line still fails at the close, the close does the
> predecessor's exact move: record the failures honestly, do **not** land the
> flip, and hand the remainder to a successor.
> **Who is it for:** whoever picks up a `C3`-unit, and the reviewer of the
> close.
> **When to read the rest:** before starting any `C3`-unit, or before deciding
> whether a DoD line that "looks done" actually is.

# Plan: IMPROVE-CODE-3 — finish the two held lines, then land the six-member flip

The predecessor (`IMPROVE-CODE-2`, `C2-00`…`C2-08`, handoff at
[`improve-code-2-handoff-notes.md`](improve-code-2-handoff-notes.md)) is the
reduction round that made the ratchet honest, collapsed the translation lane,
cut the concrete-`*Service` crossings, and covered the two untested services.
Its close (`C2-08`) re-verified the tree from a clean state and **closed held** —
not shipped, not papered over — because two carried-in DoD lines do not hold:

- **DoD #3/#4 (the still-large files):** **23** files over the 600 `cs_file`
  ceiling, target **≤ 8**. `SampleDataSeeder` (1633) is a verified honest-keep
  (two god-methods, each individually over 600); the 7 god-services are named
  keeps; only `FirstBootSeeder` + `ProjectsController` were split.
- **DoD #6 (the Marten allowlist):** **20** controllers on Marten, target **≤
  5**. 19 reasoned keeps (group a god-file-at-ceiling, group b no owning
  service, group c edge-minting, group d diagnostics/C08) + the `C2-05`
  `GoalsController` entry (the 20th).

The five lines that **do** hold (measured, not intended):

- **DoD #1** — the ratchet is honest: the fixed gate (`CODE_BASELINE_BASE`)
  runs against the predecessor's real "before" (`311d0a4b`) and reports
  **0 violations**; the 30 raises all carry `adr: 0290`.
- **DoD #5** — the translation lane is collapsed: `*Translation*` lane methods
  on concrete services are **39** (from the predecessor's ~132–195), under the
  **≤ 40** target; the gate reports the clone regions in ceiling.
- **DoD #6 (the concrete-edges half)** — the concrete-`*Service` crossings are
  **21 → 9** (12 retired in `C2-03`; the 9 all carry `adr: 0291`), 0 silent.
- **DoD #7** — `MailAlarmThresholdPolicy` + `UsagePurgePolicy` each have a
  plain unit test.
- **DoD #8** — both E11 SEC pointers are present
  (`IModerationService.cs:98` pre-existing; `TodoService.cs:1033` added `C2-07`).

This lane does not re-open the shape of the work (the modular monolith and the
three access modules of ADR 0006 stay; the translation lane and the concrete-
edges half stay where they are; the doctrine rules stay). It lands the *numbers*
the predecessor's `C2-04` and `C2-05` recorded as not-met, and — because *this*
lane finishes the reduction the two predecessor lanes promised — it is the
lane that lands the six-member "shipped" flip. The plan's instruction, carried
forward from both predecessors: *"If any DoD line fails, stop and say which one
and why — do not fix it silently."*

## Why now

The predecessor's close is the honest starting position: the ratchet is
green, the five met lines are *proven* to hold (the gate measures them), and
the two unmet lines are *measured* to fail (the gate reports 23 files over
ceiling and an allowlist of 20). The path to the two held lines is not a new
question — `C2-04` and `C2-05` named it. The module-surface keeps are the
god-services (shrink the service → the allowlist entry can retire), the
no-service controllers (mint the owning service or record the aggregate
decision), and the edge-minting ones (the edge decision). The large-file
remainder is the `SampleDataSeeder` god-methods (a per-entity decomposition),
the 7 god-services (the ADR 0290 named seams + the per-entity splits), and
the C2-03/C2-04 carries that are still over 600. This lane works those in
dependency order so each unit's retirement is a clean move, not a forced
split.

## What this lane is (and is not)

- **Is:** reducing the module surface (the Marten allowlist `20 → ≤ 5`; the
  god-service shrinks that unblock it; the no-service and edge-minting
  keep-resolutions), finishing the still-large files (`23 → ≤ 8`, including
  the `SampleDataSeeder` decomposition and the 7 god-service splits), closing
  the 3 flagged-accidental boundary edges (`Notifications→Identity:VerificationOptions`,
  `Portability→Identity:AppDbContext`, `Projects→Web:TagSlugs`), and — only if
  every carried-in DoD line holds at the close — landing the six-member "shipped"
  flip the predecessor withheld.
- **Is not:** a re-architecture (the modular monolith and the three access
  modules of ADR 0006 stay; the concrete-edges half of DoD #6 — the 9 named
  carries — stays where `C2-03` left them), a feature, a route or
  rendered-HTML change, a document-schema change, a re-opening of the met
  lines (#1, #5, #6-concrete-edges, #7, #8), or a ceremony split (the
  part-vs-whole rule forbids a split that produces types the C13 cold read
  would reject).
- **Rule for every unit (the ratchet):** a baseline `max` only goes down. A
  value that must go *up* needs an `adr:` reason in the baseline entry — a
  named, defensible exception, not a silent one. Shrinking a type under the
  ceiling and having its baseline entry retire is always allowed. A unit that
  must raise anything writes or amends its ADR *in the same commit* — the gate
  fails otherwise.

## Carried-in DoD lines (the two held, in the order this lane works them)

These are the two lines `C2-08` measured as failing, plus the close. The
"Before" column is the `C2-08` measurement at close (`f4487e19`); the "Target"
column is the predecessor plan's stated line. The gate now fails on every
`max` raised without `adr:` — so a unit that must raise anything names the ADR
it amends or creates first.

| # | DoD line | Target | Before (`C2-08`, `f4487e19`) |
|---|---|---|---|
| **#3/#4** | **The still-large files off the list.** files ≤8 · methods ≤45 · classes ≤3 · interfaces ≤2 · views ≤3 · TS ≤2 · the 7 named god-files gone | files **23** · methods **91** · classes **6** · interfaces **8** · views 3 · TS 0 · `SampleDataSeeder` (1633) + the 7 god-services (BoardService 1017 · EventService 997 · IdentityService 989 · UserPortabilityService 922 · PageService 901 · TodoService 828 · PostService 788) still over 600 |
| **#6** | **The module surface reduced.** concrete-service edges ≤9 (met) · Marten allowlist **≤5** | concrete-edges **9** (met, `adr: 0291`) · Marten allowlist **20** |

The 5 lines the predecessor **already holds** (verified by `C2-08`, re-checked
by the close): #1 ratchet honest + 30 raises resolved (`adr: 0290`) · #5
translation `≤ 40` (39) + clone regions `≤ 5 000` · #6 concrete-edges `21 → 9`
(9 named, `adr: 0291`) · #7 `MailAlarms` + `UsagePurge` covered · #8 both E11
SEC pointers present.

The **23 files over 600** at `C2-08` (the DoD #3/#4 "Before" — measured
`f4487e19`):

`GroupsController` 1639 · `SampleDataSeeder` 1633 · `EventController` 1244 ·
`TodosController` 1096 · `BoardService` 1025 · `PostsController` 1010 ·
`EventService` 997 · `IdentityService` 989 · `UserPortabilityService` 922 ·
`PageService` 901 · `BoardsController` 857 · `TodoService` 839 ·
`PageController` 833 · `PostService` 821 · `AdminController` 777 ·
`GuardianService` 771 · `ModerationService` 753 · `GuardianController` 746 ·
`TagService` 736 · `GroupService` 705 · `AnnouncementService` 704 ·
`LocalizationService` 684 · `AnnouncementController` 611.

The **20 controllers on the Marten allowlist** at `C2-08` (the DoD #6
"Before" — measured `f4487e19`), grouped per `C2-04`:

- **(a) god-file-at-ceiling** (6): `PostsController`, `AnnouncementController`,
  `PageController`, `GuardianController`, `ReportTargetsController`,
  `TagController` — each owned by a service over 600 (`PostService` 821,
  `PageService` 901, `GuardianService` 771, `ModerationService` 753,
  `TagService` 736); the allowlist entry can retire once the owning service
  drops under 600 (the C3-00…C3-03 work).
- **(b) no owning service** (5): `AdminController` (no Admin module in Core),
  `GroupsController` (no `IGroupService`), `AttachmentController` +
  `ContentImageController` (Posts+Media 2-module aggregate),
  `AdminAnalyticsController` (writes `AccessAudit`; no Usage write-service).
- **(c) move would mint a new boundary edge** (4): `DocumentController`
  (mint Documents→Tags `Tag` + Documents→Media `MediaObject`),
  `DirectoryController` (needs `HttpContext.Request` effective language),
  `ProjectsController` + `ProjectsBaseController` (C08 builder reads;
  `ProjectService` does not own them).
- **(d) diagnostics / C08 frozen surface** (3): `HealthController` (readiness
  probe), `BoardsController` + `TodosController` (C08 cluster reads;
  `BoardService`/`TodoService` do not own them).
- **C2-05's `GoalsController`** (the 20th): the service owns the Goals
  surface (`ProjectService`); the clean retirement to test the path.

## Decisions

| # | Decision | Chosen | Alternatives rejected |
|---|---|---|---|
| D1 | The god-service shrinks (the group-(a) keeps + the 7 god-files) | **Per-service, in dependency order.** `EventService` (997) + `PageService` (901) first (the ADR 0290 named seams: `EventRsvpGuardianService` + `PageTranslationStandingService` — the 4-shrink clean resolution the predecessor de-scoped); then the remaining 5 god-services (`BoardService`, `TodoService`, `PostService`, `IdentityService`, `UserPortabilityService`) each as a real part-vs-whole split with its own unit test. Each shrink that drops the service under 600 retires its `files` entry **and** its `classes`/`interfaces` entry (the method-count shrinks); the ones whose controller is on the Marten allowlist also unblock that group-(a) keep (C3-01 `PageService`→`PageController`, C3-03 `PostService`/`GuardianService`/`ModerationService`/`TagService`→their controllers — `EventService` owns no allowlist keep). | A mechanical split that produces ceremony types (the part-vs-whole rule forbids this); a blanket "allow it" on the god-services (the anti-pattern the round exists to stop); pre-empting the ADR 0290 de-scope (the predecessor's C13 explicitly forbade) |
| D2 | The `SampleDataSeeder` decomposition (1633; two god-methods each >600) | **Per-entity builders.** Decompose `SeedAsync` (615 code / cc 83) and `BackfillSampleCorpusAsync` (782 / cc 128) into named per-entity builders (accounts / groups / posts / events / tags / pages), each a separate, unit-testable surface. This is a *behaviour-risky rewrite of the two largest methods in the repo* — the predecessor's C2-05 recorded it as a verified honest-keep because a class-level split cannot get a file under 600 (a single 782-line method forces any file over the ceiling). This lane adopts the scope the predecessor de-scoped; the seam is real (each entity-builder is a part-vs-whole surface), not a ceremony split. | Forcing a class-level split (the anti-pattern — it produces a ceremony file over the ceiling, not a reduction); a partial extraction that keeps the entry baselined (the baseline must retire, not just shrink); a behaviour-preserving "just add a TODO" (the predecessor's doctrine: do not paper over) |
| D3 | The Marten allowlist (20 → ≤ 5) | **Dependency-ordered retirement.** (1) `GoalsController` (the 20th — the service owns the surface; the clean first retirement). (2) The group-(a) keeps, as the owning service shrinks under 600 (C3-01 + C3-03 unblock them, where the controller is on the allowlist). (3) The group-(b) no-service keeps, each by minting the owning service (a real service, not a ceremony `I*Service` — the part-vs-whole rule) **or** by the aggregate decision (the writes span modules no single service owns; record the decision, not a forced service). (4) The group-(c) edge-minting keeps, by the edge decision (the controller reads a concrete cross-module type; routing it through a Core service = a gate edge; the decision is to keep it there, with the reason named). (5) The group-(d) diagnostics/C08 keeps, by the named reason (the readiness probe is a pure diagnostic; the C08 cluster reads are frozen). The count goes down per retirement; the gate stays green. | A blanket "allow it" on the 20 (several are the same seam the predecessor left open); minting a ceremony `I*Service` to hide a concrete dependency (the D5 anti-pattern the predecessor named); pre-empting the god-service shrinks (the group-(a) keeps cannot retire until the service is under 600) |
| D4 | The remaining large files (the 23 → ≤ 8 after the god-services + `SampleDataSeeder`) | **Per-file, real part-vs-whole splits.** The files that remain over 600 after D1/D2 are the C2-03/C2-04 carries + the tail. Each split is a *real* part-vs-whole seam (the C13 cold read would pass on its own), not a ceremony split. The baseline entry retires as the file drops under 600. | Raising the file ceiling (the anti-pattern); a partial extraction that keeps the entry baselined (the baseline must retire, not just shrink); a split-for-a-number (the part-vs-whole rule forbids) |
| D5 | The 3 flagged-accidental boundary edges (ADR 0290, named for `C2-03`) | **Contract extraction.** Each of the 3 (`Notifications→Identity:VerificationOptions`, `Portability→Identity:AppDbContext`, `Projects→Web:TagSlugs`) is a concrete cross-module type where an interface / `*Policy` / `*Keys` would do. The contract extraction retires the edge (the gate count drops); the `adr: 0290` field is dropped with it. | A blanket `adr:` on the 3 (they are the accidental-integration class the predecessor's `C2-03` retired for the other 12); a re-architecture (the modular monolith stays) |
| D6 | The six-member flip (the close) | **Lands only if every carried-in DoD line holds at the close.** The `WhatsNew` 0.86.x entry and the `Milestones` entry are added then, with the *true* before → after — the before being the predecessor's close (`219ba33c`: files 25 · methods 91 · classes 7 · interfaces 8 · RED 25 · Marten allowlist 21 · 478 boundary entries · `*Translation*` methods ~132–195), not its intent. `README.md` / `STATUS.md` point at the shipped state (they point at `Milestones.cs`, the source of truth — update the status, do not restate the roadmap list). `ARCHITECTURE.md` reflects the settled module-boundary surface. The two pin tests (`WhatsNewTests` + `MilestonesTests`) stay **green** (they pin the version order and the milestone order/statuses; they are *verified*, not edited, unless a new row changes what they pin). | Landing the flip on a "good enough" story (the anti-pattern the predecessor's C13 named: a "shipped reduction" would record a reduction that did not happen); a partial flip (the six members are a contract — either all six hold or none land) |

## The units

Unit IDs are `C3-00` … `C3-08` — the "3" is the successor round. Each unit is
one commit, one named seam or one named number, and the gate must be green
before the next. A unit that raises a baseline entry writes or amends its ADR
*in the same commit* — the gate fails otherwise.

| # | Unit | DoD line | The named seam / number |
|---|---|---|---|
| C3-00 | **`EventService` shrink** (the ADR 0290 named seam `EventRsvpGuardianService`) | #3/#4 | `EventService` (997) drops under 600; the `EventService` class entry (31 methods) retires; the `IEventService` interface (28) narrows. *(No allowlist entry retires — `EventController` is not on the allowlist; the group-(a) allowlist keeps unblock in C3-01 / C3-03 where the owning service is on the list.)* |
| C3-01 | **`PageService` shrink** (the ADR 0290 named seam `PageTranslationStandingService`) | #3/#4 + #6(a) | `PageService` (901) drops under 600; the `PageService` class entry (30 methods) retires; `PageController`'s allowlist entry can now retire (D1) |
| C3-02 | **`SampleDataSeeder` decomposition** (the 2 god-methods) | #3/#4 | `SeedAsync` (615/cc 83) + `BackfillSampleCorpusAsync` (782/cc 128) into named per-entity builders (accounts / groups / posts / events / tags / pages); the `files` entry retires (1633 → under 600) (D2) |
| C3-03 | **The 7 god-services** (the C2-05 named keeps) | #3/#4 + #6(a) | `BoardService` (1017) · `TodoService` (828) · `PostService` (821) · `IdentityService` (989) · `UserPortabilityService` (922) — each a real part-vs-whole split with its own unit test; the `files`/`classes`/`interfaces` entries retire as each drops under the ceilings; the group-(a) allowlist keeps retire as their owning service drops under 600 (D1) |
| C3-04 | **The still-large files: the tail** (the 23 → ≤ 8) | #3/#4 | the files that remain over 600 after C3-00…C3-03 (`GroupsController` 1639 · `EventController` 1244 · `TodosController` 1096 · `BoardsController` 857 · `AdminController` 777 · `ModerationService` 753 · `GuardianController` 746 · `GroupService` 705 · `AnnouncementService` 704 · `LocalizationService` 684 · `AnnouncementController` 611 · + the tail) — each a real part-vs-whole split; the `files` entries retire; **files ≤ 8** (D4) |
| C3-05 | **The Marten allowlist: the clean retirements** (`GoalsController` + the group-(a) keeps) | #6 | `GoalsController` (the 20th — the service owns the surface) + the group-(a) keeps (`PostsController` · `AnnouncementController` · `PageController` · `GuardianController` · `ReportTargetsController` · `TagController`) — the controller's document access moves to the owning service (now under 600); the allowlist entries retire (D3) |
| C3-06 | **The Marten allowlist: the no-service + edge-minting keeps** | #6 | the group-(b) no-service keeps (`AdminController` · `GroupsController` · `AttachmentController` · `ContentImageController` · `AdminAnalyticsController`) — each by minting the owning service (a real service) or the aggregate decision; the group-(c) edge-minting keeps (`DocumentController` · `DirectoryController` · `ProjectsController` · `ProjectsBaseController`) — by the edge decision (the controller reads a concrete cross-module type); **Marten allowlist ≤ 5** (D3) |
| C3-07 | **The 3 flagged-accidental boundary edges** (ADR 0290, named for C2-03) | #6 (boundary) | `Notifications→Identity:VerificationOptions` · `Portability→Identity:AppDbContext` · `Projects→Web:TagSlugs` — each a contract extraction (an interface / `*Policy` / `*Keys`); the boundary count drops; the `adr: 0290` field is dropped with the edge (D5) |
| C3-08 | **Close + independent verification** | the close | re-run the tooling from a clean state, check every DoD line against the tree, and land the six-member flip — *or* record the failures honestly and hand to a successor, exactly as `C2-08` did |

## The close (the six-member flip, landed *here* — withheld by both predecessors)

The predecessor's `C2-08` close **deliberately did not land** the `AGENTS.md`
six-member contract (`Milestones.cs` / `WhatsNew.cs` / `README.md` /
`STATUS.md` / `ARCHITECTURE.md` / the two pin tests) because the lane closed
held — landing a "shipped reduction" would have recorded a reduction that did
not happen. That honest hold is preserved: **C3-08 lands the six-member flip
only if every carried-in DoD line holds at its close.** The `WhatsNew` 0.86.x
entry and the `Milestones` entry are added then, with the *true* before →
after — the before being the predecessor's close (`219ba33c`), not its intent.

If any DoD line fails at C3-08, the unit does the predecessor's exact move:
record the failures, do not land the flip, and hand the remainder to a
successor. The gate (honest since `C2-00`) is what makes that hand-off
honest-by-construction rather than by self-report.

## Evidence / starting numbers (measured 2026-11-05, `development` `f4487e19`)

- **Gate:** the fixed `code-check.ps1` runs `CODE_BASELINE_BASE`
  (CI → PR base; local default `origin/development`); fails closed under CI
  on an unreadable base; skips only a first-introduction baseline (file never
  on base). `improve-check.ps1` = ALL GATES PASS. Build 0 errors.
- **Baseline at close** (`tools/code/code-baseline.json`, unchanged by the
  close): **23 files** · **91 methods** · **6 classes** · **8 interfaces** ·
  **3 views** · **0 ts files** · **13 ts_functions** · **465
  boundary_violations** · **20 controllers_marten_allowlist** ·
  seamTestMode=gate.
- **The two held lines (the close's measured failures):** DoD #3/#4 files
  **23** (> 8 target) · DoD #6 Marten allowlist **20** (> 5 target).
- **The 5 lines that hold (the close's measured passes):** DoD #1 ratchet
  honest (the fixed gate vs `311d0a4b` = 0 violations; all 30 raises carry
  `adr: 0290`) · DoD #5 translation **39** (≤ 40) + clone regions in ceiling ·
  DoD #6 concrete-edges **21 → 9** (12 retired; 9 named `adr: 0291`) · DoD #7
  `MailAlarmThresholdPolicy` + `UsagePurgePolicy` both with plain unit tests ·
  DoD #8 both E11 SEC pointers present (`IModerationService.cs:98` +
  `TodoService.cs:1033`).
