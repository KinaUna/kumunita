---
id: improve-code-handoff-notes
title: "IMPROVE-CODE — Handoff Notes"
tier: archive
kind: handoff
status: archived
audiences: [developer, agent]
lane: improve-code
related:
  - docs/archive/lanes/improve-code/plan-improve-code.md
  - docs/adr/0286-iuserservice-access-core-split.md
  - docs/adr/0287-projects-module-split.md
updated: 2026-11-05
---

> **Abstract:** the closing handoff note for the second IMPROVE round
> (the **code** lane, `C00`–`C13`) — what the differentiation work that
> shipped in C00–C12 actually changed, and — the reason this note exists —
> **the cold-read verification (C13) found the lane does not meet 7 of its
> 10 Definition-of-Done lines**, including the ratchet gate going *up* three
> times without an `adr:` reason. It records each DoD line's measured value
> at close, so a successor lane (`IMPROVE-CODE-2`) can pick up exactly the
> unmet lines.
> **Who is it for:** whoever picks up `IMPROVE-CODE-2`, and any reviewer who
> wants the honest before → after, not the intended one.
> **When to read the rest:** before starting any successor unit, or before
> deciding whether a DoD line that "looks done" actually is.

# IMPROVE-CODE — handoff notes (closed with unmet DoD)

## What the lane is

The code half of the second IMPROVE round. Its goal (plan §What this lane is)
was to **differentiate the code along real seams** — split the god-types and
god-methods, link the copied patterns through one named seam, move data out
of code, and put tests where the parts meet — and to replace the
grandfathered size gates (IMPROVE a/e/f) with a Roslyn **ratchet** gate that
can only go down.

## What actually shipped in C00–C12 (the differentiation that landed)

These are real and verified — the lane's core intent is present in the tree:

- **C00** — the ratchet gate (`tools/code/CodeMetrics` `--check`/`--tighten`,
  `code-check.ps1`/`code-report.ps1`, `code-baseline.json`), gates a/e/f
  retired, the CI `code` job, and the doctrine rules (ratchet / part-vs-whole
  / comment) in `AGENTS.md` + `in-code.md`. **DoD #2 and #9 are met.**
- **C02** — translation keys, guide pages, and the sample corpus moved to
  embedded resources; `KnownTranslationKeys.cs` is gone from the file list.
- **C03** — `INotificationEmitter` + `NotificationKeys` (a contract for
  Notifications, off the concrete `NotificationService`).
- **C04** — decision `*Policy` parts extracted (e.g. `EventStandingPolicy`,
  `PostTranslationPolicy`), each with a unit test.
- **C05** — `ContentTargetResolver` (one content-target resolver for
  Bookmark + PersonalNote, in a new `Core/Targets/`).
- **C06** — the shared `TranslationLane` + per-surface descriptors.
- **C07** — the `UserInfo` module split into `GroupService` /
  `CommunityMembershipService` / `GuardianService` / `ProfileSettingsService`;
  `IUserInfoService` is gone (its successors are baselined).
- **C08** — the `Projects` module split into `BoardService` / `TodoService` /
  `ProjectService`; `BoardsController` / `TodosController` exist; the
  view-model builders are extracted.
- **C09** — controllers off Marten for many surfaces; the 21-controller
  allowlist (each with a lane-accurate reason) is in the baseline.
- **C10** — `AddKumunitaCore` shrunk to module self-registration; the
  portability `Resolve/Classify/Export` strategy table + first tests for
  `PortabilityValidate`/`PortabilityApplyDocuments`.
- **C11** — the six longest views split into partials; `rich-editor.ts`
  split; the composer picker trio called directly; one `UploadGateHandler`;
  one `IcsFormat` calendar serializer.
- **C12** — the seam-test rule flipped from report to **gate**.

**The split work is the good news.** The god-types are no longer god-types.
The bad news is recorded below.

## Close (C13 — the cold-read verification)

**Status: the lane is closed with unmet DoD.** C13 is the cold reader. It
re-ran the tooling from a clean state (`code-check.ps1`, `code-report.ps1`,
`dotnet run tools/code/CodeMetrics`, and the `git log -p` the DoD itself
names) and checked every DoD line against the tree at HEAD `219ba33c`
(branch `development`). **7 of the 10 lines do not hold.** Per the plan's own
instruction for C13 — *"If any DoD line fails, stop and say which one and why
— do not fix it silently"* — this note records the failures rather than
papering over them, and hands them to `IMPROVE-CODE-2`.

### The headline: the ratchet went *up* (DoD #1 — FAIL)

DoD #1 is the lane's whole reason to exist: *"no baseline `max` went up during
the lane (`git log -p tools/code/code-baseline.json` shows only decreases and
removals)."* Running the ratchet gate against the lane's real "before" (C00,
`311d0a4b`) shows **30 baseline values rose without an `adr:` field** (the
gate's escape hatch, unused). The first read here undercounted this as "3";
the gate-fix pass (below) measured the true number. The 30 split two ways:

- **4 shrinkable god-type / method raises** (the class/interface/method
  surface went up) — these are the "shrink the type back under the ceiling"
  items:

  | Baseline entry | C00 (`311d0a4b`) | Now | Raised in |
  |---|---:|---:|---|
  | `Core/Events/EventService.cs` **class** (public methods) | 30 | **31** | `12b5c0b5` (C09) |
  | `Core/Pages/PageService.cs` **class** (public methods) | 28 | **30** | `004a253e` (C09) |
  | `Core/Events/IEventService.cs` **interface** (methods) | 27 | **28** | `12b5c0b5` (C09) |
  | one method **cc_max** (complexity) | 20 | **22** | (C07/C08 era) |

- **26 boundary-count raises** — cross-module edge *counts* went up (e.g.
  `Web->Identity:User` 71→74, `Web->Tags:Tag` 7→9,
  `Projects->Authorization:Audience` 10→12, `Web->Messaging:Message` 18→21).
  These are **not** shrinkable god-types; the fix is an `adr:` justification
  (the C06/C07/C08 splits added real cross-module edges) or accepting the
  coupling — not a refactor.

`git log -p tools/code/code-baseline.json 311d0a4b..HEAD` shows the added
`+ "max": …` / `+ "count": …` lines; none of the 30 carries an `adr` field.
**The gate was dead in CI, which is how they stayed green:** it hardcoded
`origin/main` (CI targets `release`; the file only exists on `development`),
it silently skipped when the base was unreadable, and a guard bug made it
never flag even a readable base. That is now fixed (see the gate-fix note
below), so these 30 are the red-gate reality going forward. **This is exactly
the failure the plan opens with** — the gate that was
supposed to stop growth allowed three increments of it.

### DoD line by line (met / failed + measured)

Before = the C00 baseline (`code-baseline.json` @ `311d0a4b`) or the plan's
Evidence table; after = the fresh measurement at HEAD. "FAIL" lines are the
successor lane's scope.

| # | DoD line | Target | Before (C00/plan) | After (measured @ `219ba33c`) | Verdict |
|---|---|---|---|---|---|
| 1 | No baseline `max` went up | decreases/removals only | — | **30 raises**, none with `adr:` — 4 shrinkable god-type/method (table above) + 26 boundary-coupling | ❌ **FAIL** |
| 2 | Gates a/e/f gone + re-baseline block deleted | retired | a/e/f present | marked `RETIRED (IMPROVE-CODE C00)`; `improve-check` = ALL GATES PASS | ✅ met |
| 3 | List sizes ↓ to files≤8 · methods≤45 · classes≤3 · interfaces≤2 · views≤3 · TS≤2 | ≤ those | files 22 · methods 107 · classes 9 · interfaces 6 · views 9 · ts 1 (+13 funcs) | **files 25** · methods 91 · classes 7 · **interfaces 8** · views 3 · ts 0 (**13 ts functions baselined**) | ❌ **FAIL** (files *grew* 22→25; 4 of 6 sub-metrics over) |
| 4 | `KnownTranslationKeys`/`FirstBootSeeder`/`SampleDataSeeder`/`ProjectsController`/`ProjectService`/`UserInfoService`/`IUserInfoService` **off** the lists | 7 gone | — | `KnownTranslationKeys`, `UserInfoService`, `IUserInfoService`, `ProjectService`(class split) **gone**; but **`FirstBootSeeder` (991), `SampleDataSeeder` (1633), `ProjectsController` (802) still on the file list** | ❌ **FAIL** (3 of 7 still present) |
| 5 | Clones ≤5 000 (from 10 291) + `*Translation*` methods ≤40 (from 133) | ≤ those | 133 methods / 10 291 clone lines | `*Translation*` methods **~132–195** (tool member-column: 195 across 46 files) — **not ≤40**; clone regions **3935** (plan E7 cited 337) | ❌ **FAIL** (both halves) |
| 6 | Concrete-service edges 0; Marten controllers ≤5 allowlisted | 0 / ≤5 | 205 folder→folder edges / 21 controllers | **478** boundary entries baselined; **Marten allowlist = 21** (target ≤5) | ❌ **FAIL** (both halves) |
| 7 | Seam-test gated + services-with-unittest ≥10 + every controller named | — | 0 services with a unit-tested part | seam-test **gated** ✅ (C12); but **`MailAlarms` = 0 test refs, `UsagePurge` = 0 test refs** (two of the three E12 "no tests" services C12 was to cover) | ⚠️ **PARTIAL** |
| 8 | Both E11 items → SEC with file/line refs, behaviour unchanged | 2 pointers | — | `IsInActorScopeAsync` → `IModerationService.cs:97` has `TODO ❓ SEC (E11)` ✅; **the claim-to-do pointer (C04: "leave a `// TODO ❓ SEC`") is missing** — `ClaimTodoAsync` now lives in `TodoService.cs:1024` with no SEC pointer | ❌ **FAIL** (1 of 2) |
| 9 | Ratchet / part-vs-whole / comment rule in `AGENTS.md` + `in-code.md`, one place each | — | — | `in-code.md` §"The ratchet, the split, the comment" holds all three; `AGENTS.md` links out | ✅ met |
| 10 | Cold-read verification passes | — | — | this run — surfaced #1, #3, #4, #5, #6, #8 as failures | ❌ **fails** |

**Net: 2 met, 1 partial, 7 not met.**

### Cold-read verification (the part-vs-whole sample)

As the cold reader I re-ran the tool, checked every DoD line (above), and
picked **three new types at random** to judge against the part-vs-whole rule
(a new type needs ≥2 callers, **or** its own unit test, **or** an owned route
family/aggregate). **All three pass** — the splits are not ceremony:

| New type | (a) callers | (b) own unit test | (c) owns aggregate/route family | Verdict |
|---|---|---|---|---|
| `ContentTargetResolver` (C05, `Core/Targets/`) | 3 (TranslationLane, Targets/DI, ContentTargetKinds) | `ContentTargetKindsTests.cs` + the Bookmark/PersonalNote service tests | — | ✅ passes (a)+(b) |
| `TodoService` / `ITodoService` (C08) | 14 (Todo/Board/Project controllers, ReportTargets, KioskItemCatalog, …) | named by 5 test files | **the to-do aggregate** | ✅ passes (a)+(b)+(c) |
| `EventStandingPolicy` (C04) | 2 (`EventService`, `EventController`) | `EventStandingPolicyTests.cs` | — | ✅ passes (a)+(b) |

So the *shape* of the work is sound. The lane's failures are not
misceremonized types — they are the **reduction numbers** (the ratchet went up;
the per-surface translation methods and the controller-Marten surface did not
shrink to the stated targets), plus one missing SEC pointer.

### Build + test + gate verification (all green at close, 2026-11-05)

- **`git status --porcelain`** — clean apart from the C13 change set (this
  note, the archive `git mv` of the lane, and the four ADR link fixes); no
  production C# or test C# touched by the close.
- **`code-check.ps1`** — **`OK (0 violations)`** (note: green here only
  because `origin/main` lacks `code-baseline.json`, which skips the DoD #1
  raise-without-`adr:` check — see the headline).
- **`improve-check.ps1`** — **`ALL GATES PASS`** (gates a/e/f retired; b/c/d/g
  green).
- **`dotnet build Kumunita.slnx -c Debug`** — **Build succeeded, 0 errors**
  (113 pre-existing `xUnit1051`/nullable warnings, none introduced by the
  close — the close is docs + a `git mv`, no C# touched).
- **`WhatsNewTests` / `MilestonesTests`** — **untouched and still green**
  (the honest close deliberately adds **no** `WhatsNew` "shipped" entry and
  **no** `Milestones` entry, because the lane is not done; see the
  close-flip note below).

### The close flip — deliberately *not* landed

The `AGENTS.md` six-member contract (`Milestones.cs` / `WhatsNew.cs` /
`README.md` / `STATUS.md` / `ARCHITECTURE.md` / the two pin tests) records a
**shipped** reduction. Because 7 of 10 DoD lines are unmet — including the
ratchet having gone *up* — landing the "measurably smaller, no new feature"
`WhatsNew` 0.86.0 entry would record a reduction that did not happen. **The
honest close does not flip the six members.** When `IMPROVE-CODE-2` lands the
unmet DoD lines (DoD #1, #3, #4, #5, #6, #7, #8), *its* close unit lands the
six-member flip with the true before → after. The lane is archived with this
note as its record so `IMPROVE-CODE-2` inherits the exact gaps.

### Lane status

**The lane is closed with unmet DoD (DoD #1, #3, #4, #5, #6, #8 fail; #7
partial; #2 and #9 met).** It is archived here, not declared shipped. The
differentiation that C00–C12 built is real and stays; the reduction the plan
promised is not there yet.

## Handoffs to `IMPROVE-CODE-2` (the successor)

Pick these up as the successor lane's units, in this order (DoD #1 first — it
is the gate that lets the others be honest).

> **The gate is already fixed (2026-11-05).** The C13 close recorded DoD #1
> with the gate still dead in CI (it hardcoded `origin/main`, skipped on an
> unreadable base, and a guard bug made it never flag). A follow-up commit
> fixed all three in `tools/code/CodeMetrics/Program.cs` + `ci.yml`: the base
> ref is now `CODE_BASELINE_BASE` (CI sets it to the PR base; local default
> `origin/development`), it **fails closed under CI** when the base is
> unreadable, and the guard bug is gone. The DoD #1 number below is the
> authoritative one the fixed gate produces. The fix is a genuine improvement
> independent of the raises it now surfaces.

1. **DoD #1 — resolve the 30 unjustified raises.** Two distinct sub-tasks:
   - **4 shrinkable god-type / method raises** (`EventService` class 31,
     `PageService` class 30, `IEventService` 28, + one method cc_max 22).
     Either **shrink them back under the ceiling** (class 25 / interface 20;
     all are over it and baselined) or give each an **`adr:` reason** (a new
     amending ADR) so the raise is a *named, defensible* exception instead of
     a silent one.
   - **26 boundary-count raises** (cross-module edges went up: `Web->Identity:User`
     71→74, `Web->Tags:Tag` 7→9, `Projects->Authorization:Audience` 10→12,
     `Web->Messaging:Message` 18→21, …). These are **not** shrinkable —
     they are the real coupling the C06/C07/C08 splits introduced. Decide
     per-edge: justify with `adr:` (the split is intentional) or reduce the
     coupling. A blanket `adr:` on all 26 is the honest default; picking the
     ones that are genuine accidental-integration is the improvement.
   Whichever is chosen, the gate (now fixed) fails until it is — that is the
   point of the ratchet.
2. **DoD #5 — the translation lane.** `*Translation*` methods are ~132–195
   (target ≤40). C06 built the `TranslationLane` + descriptors, but the
   per-surface `*Translation*` methods were not collapsed onto it. Finish the
   migration the C06 unit planned ("one surface per commit").
3. **DoD #6 — the module surface.** 478 boundary edges and 21
   Marten-allowlisted controllers (target ≤5). The plan's own E9/E10 targets
   are the baseline to work against; the 21 allowlist entries each carry a
   "C09 survivor" reason, so the successor knows which are deliberate and
   which are still open.
4. **DoD #3 / #4 — the still-large files.** `FirstBootSeeder` (991),
   `SampleDataSeeder` (1633), `ProjectsController` (802) are over the 600
   file ceiling and baselined. C02/C08 shrank their siblings; these three are
   the remainder.
5. **DoD #7 — the two untested services.** `MailAlarms` and `UsagePurge` have
   zero test references (C12's E12 list named all three; `Suppression` got
   coverage, these two did not).
6. **DoD #8 — the missing SEC pointer.** `TodoService.ClaimTodoAsync`
   (`TodoService.cs:1024`) holds the claim-to-do standing that E11 flagged as
   an access decision outside `IAuthorizationService`. It needs the
   `// TODO ❓ SEC` pointer (the Moderation half is done at
   `IModerationService.cs:97`).

**What is NOT a successor unit:** the part-vs-whole splits (C05/C07/C08) and
the doctrine rules (C00) are met and stay. Do not re-open them.
