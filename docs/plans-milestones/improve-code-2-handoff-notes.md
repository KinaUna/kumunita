---
id: improve-code-2-handoff-notes
title: "IMPROVE-CODE-2 — Handoff Notes"
tier: living
kind: handoff
status: current
audiences: [developer, agent]
lane: improve-code-2
related:
  - docs/plans-milestones/plan-improve-code-2.md
  - docs/adr/0290-improve-code-2-baseline-raises-and-seams.md
  - docs/archive/lanes/improve-code/handoff-notes.md
updated: 2026-11-05
---

> **Abstract:** the running handoff log for the IMPROVE-CODE-2 lane.
> C2-00 (this unit) recorded the CI-shaped failing and passing gate runs,
> resolved the 30 unjustified raises (the `adr: 0290` named exception record),
> and confirmed all verification gates green. **Who is it for:** the
> successor units C2-01…C2-08, and the reviewer of C2-08's close.
> **When to read the rest:** before starting any C2-unit, or before deciding
> whether a DoD line that "looks done" actually is.

# IMPROVE-CODE-2 — handoff notes

## C2-00 — Ratchet honesty (2026-11-05)

### Part 1 — the CI-shaped gate runs

**Failing run (the gate bites on the real "before"):**

- **Command:** `CI=true CODE_BASELINE_BASE=311d0a4b pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/code/code-check.ps1`
- **`CODE_BASELINE_BASE`:** `311d0a4b` (the predecessor's C00 "before" — the commit the lane started from)
- **Result:** `code-check: 30 violation(s)` — **exit code 1**
- **The 30 violations:** 4 shrinkable god-type/method raises + 26 boundary-count raises (the exact list in the "before → after" table below)
- **What this proves:** the gate's `CODE_BASELINE_BASE` check runs on a real base ref and fails a PR that raised a baseline `max`/`count`/`cc_max` without an `adr:` field. This is the end-to-end proof that no local test can exercise — locally the base is `origin/development` (where the file is present and the raise check compares against the *current* tree, not the "before").

**Passing run (the `adr:` fields resolve the raises):**

- **Command:** `CI=true CODE_BASELINE_BASE=311d0a4b pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/code/code-check.ps1` (after the `adr: 0290` additions)
- **`CODE_BASELINE_BASE`:** `311d0a4b`
- **Result:** `code-check: OK (0 violations)` — **exit code 0**
- **What this proves:** every raise now carries a named `adr:` field the gate accepts. The ratchet is honest.

**CI wiring confirmed (not changed):**

- `.github/workflows/ci.yml` `code` job: `fetch-depth: 0` ✅, `CODE_BASELINE_BASE: origin/${{ github.base_ref || 'release' }}` ✅, `pull_request: [release]` trigger ✅. No wiring defect found.

### Part 2 — the 30 raises resolved

**Resolution: `adr: 0290` on all 30 entries.** ADR 0290 (status: Proposed) is the named exception record. The 4 shrinkable god-type/method entries carry their **named seams** (a tractable part-vs-whole split the C13 cold read would verify, not a ceremony type); the clean-resolution 4-shrink is a *distinct* scope the successor must explicitly adopt (ADR 0290's scope note — C2-05 is the closest thematic home but the plan does not currently scope the class/interface/method shrinks to it). The 26 boundary edges carry the per-edge read (intended / flagged-accidental + the owner ADR + C2-03's 3 flagged-accidental edges).

**Before → after for each of the 30:**

| # | Entry | Before (`311d0a4b`) | Now | After (resolution) |
|---|---|---:|---:|---|
| 1 | `EventService` **class** max | 30 | 31 | `adr: 0290` (named seam: `EventRsvpGuardianService`) |
| 2 | `PageService` **class** max | 28 | 30 | `adr: 0290` (named seam: `PageTranslationStandingService`) |
| 3 | `IEventService` **interface** max | 27 | 28 | `adr: 0290` (named seam: `IEventRsvpGuardian`) |
| 4 | `HomeController.BuildPostRowsAsync` **cc_max** | 20 | 22 | `adr: 0290` (named seam: `HomeFeedRowBuilder`) |
| 5 | `ErrorReports→Authorization:AccessAudit` count | 2 | 3 | `adr: 0290` (intended; ADR 0006) |
| 6 | `Mentions→UserInfo:DirectoryService` count | 1 | 2 | `adr: 0290` (intended; ADR 0286) |
| 7 | `Notifications→Identity:VerificationOptions` count | 1 | 2 | `adr: 0290` (**flagged-accidental**; C2-03) |
| 8 | `Notifications→UserInfo:Profile` count | 1 | 2 | `adr: 0290` (intended; ADR 0286) |
| 9 | `Portability→Identity:AppDbContext` count | 2 | 3 | `adr: 0290` (**flagged-accidental**; C2-03) |
| 10 | `Portability→Identity:User` count | 4 | 5 | `adr: 0290` (intended; ADR 0286) |
| 11 | `Posts→Identity:Roles` count | 1 | 2 | `adr: 0290` (intended; ADR 0006) |
| 12 | `Projects→Authorization:AccessAuditFactory` count | 1 | 3 | `adr: 0290` (intended; ADR 0287) |
| 13 | `Projects→Authorization:Audience` count | 10 | 12 | `adr: 0290` (intended; ADR 0287) |
| 14 | `Projects→Authorization:StandingMatrix` count | 1 | 3 | `adr: 0290` (intended; ADR 0287) |
| 15 | `Projects→Events:Event` count | 1 | 2 | `adr: 0290` (intended; ADR 0115) |
| 16 | `Projects→Events:EventToAuditableResource` count | 1 | 2 | `adr: 0290` (intended; ADR 0115) |
| 17 | `Projects→Identity:Roles` count | 1 | 7 | `adr: 0290` (intended; ADR 0287) |
| 18 | `Projects→Query:PageSizer` count | 1 | 3 | `adr: 0290` (intended; ADR 0090) |
| 19 | `Projects→Web:TagSlugs` count | 2 | 3 | `adr: 0290` (**flagged-accidental**; C2-03) |
| 20 | `UserInfo→Authorization:AccessAudit` count | 1 | 5 | `adr: 0290` (intended; ADR 0286) |
| 21 | `UserInfo→Identity:Roles` count | 1 | 4 | `adr: 0290` (intended; ADR 0286) |
| 22 | `Web→Authorization:Audience` count | 16 | 19 | `adr: 0290` (intended; ADR 0006) |
| 23 | `Web→Identity:Roles` count | 34 | 36 | `adr: 0290` (intended; ADR 0006) |
| 24 | `Web→Identity:User` count | 71 | 74 | `adr: 0290` (intended; ADR 0286) |
| 25 | `Web→Messaging:Message` count | 18 | 21 | `adr: 0290` (intended; ADR 0278) |
| 26 | `Web→Projects:BoardItemPlacement` count | 1 | 2 | `adr: 0290` (intended; ADR 0287) |
| 27 | `Web→Projects:KanbanBoard` count | 5 | 6 | `adr: 0290` (intended; ADR 0287) |
| 28 | `Web→Projects:ProjectGoal` count | 1 | 2 | `adr: 0290` (intended; ADR 0287) |
| 29 | `Web→Projects:TodoItem` count | 5 | 6 | `adr: 0290` (intended; ADR 0287) |
| 30 | `Web→Tags:Tag` count | 7 | 9 | `adr: 0290` (intended; ADR 0006) |

**Count check:** 4 shrinkable + 26 boundary = **30**. All 30 carry `adr: 0290`.
No raise is silent. The 3 flagged-accidental edges (#7, #9, #19) are named for C2-03's contract-extraction work.

### Verification (all green, 2026-11-05)

- **`CI=true CODE_BASELINE_BASE=311d0a4b code-check.ps1`** — **exit 0** (the gate passes against the predecessor's "before" — no un-`adr`'d raise remains)
- **`code-check.ps1`** (local, no CI) — **`OK (0 violations)`, exit 0**
- **`dotnet build Kumunita.slnx -c Debug`** — **0 errors** (113 pre-existing warnings, none introduced)
- **`improve-check.ps1`** — **`ALL GATES PASS`** (gate b: ADR index drift = 0 after adding 0290 to the index)
- **`docs-check.ps1`** — 3 pre-existing failures in archived predecessor files (not caused by this unit; they existed before C2-00 started)
- **No C# source code touched** — this unit is the `adr:` record + the baseline entries + the ADR + the index. The clean-resolution 4-shrink (shrink the named seams under the ceiling, the part unit-tested, part-vs-whole passing) is a *distinct scope* the successor must explicitly adopt (ADR 0290's scope note) — C2-00 does the floor (the gate is green; no raise is silent), not the ceiling (the entry still retired).

### Files changed by C2-00

| File | Change |
|---|---|
| `docs/adr/0290-improve-code-2-baseline-raises-and-seams.md` | **New** — the ADR (status: Proposed) |
| `docs/adr/README.md` | ADR 0290 added to the index |
| `tools/code/code-baseline.json` | 30 entries gained `"adr": "0290"` (4 class/interface/method + 26 boundary) |
| `docs/plans-milestones/improve-code-2-handoff-notes.md` | **New** — this note |

### What C2-00 did NOT do (the plan's scope)

- **Did not shrink** the 4 god-type/method entries (the clean-resolution 4-shrink — the named seams are in ADR 0290; the successor must explicitly adopt the scope, see ADR 0290's scope note). The `adr:` named here is the floor (the gate is green; no raise is silent); the shrinking is the ceiling (the entry retires under the 25/20/15 ceilings).
- **Did not extract** the 3 flagged-accidental edges (C2-03's work — they are named in ADR 0290)
- **Did not collapse** the translation lane (C2-01/C2-02)
- **Did not reduce** the Marten allowlist (C2-04)
- **Did not shrink** the still-large files (C2-05)
- **Did not add tests** for `MailAlarms` / `UsagePurge` (C2-06)
- **Did not add** the SEC pointer (C2-07)

### DoD check (C2-00)

- [x] Part 1: CI-shaped failing run (30 raises) **and** passing run recorded with command, `CODE_BASELINE_BASE`, and exit code; `ci.yml` wiring confirmed (not changed)
- [x] Part 2a: each of the 4 shrinkable entries carries a named `adr:` (0290)
- [x] Part 2b: each of the 26 boundary entries carries a named `adr:` (0290) or is flagged-accidental (3 named for C2-03)
- [x] `tools/code/code-baseline.json` — every raised `max`/`count`/`cc_max` carries `adr:`
- [x] `dotnet build Kumunita.slnx -c Debug` — 0 errors
- [x] `tools/code/code-check.ps1` — green (local) **and** the fixed gate run against the predecessor's "before" no longer reports an un-`adr`'d raise
- [x] `tools/docs/improve-check.ps1` — ALL GATES PASS
- [x] Handoff note written with before → after for all 30

**C2-00 is complete.** The next unit is **C2-01 — Translation lane, first surface**.
