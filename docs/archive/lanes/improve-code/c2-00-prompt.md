---
id: c2-00-prompt
title: "C2-00 — Ratchet honesty: prove the fixed gate runs on a real PR, then resolve the 30 raises"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C2-00 — Ratchet honesty: prove the fixed gate runs on a real PR, then resolve the 30 raises

## What you are doing

You are the first agent on the **IMPROVE-CODE-2** lane (plan:
`docs/archive/lanes/improve-code/plan-improve-code-2.md`). Your unit is **C2-00**, the
first of 9. It is the **gate unit** — the predecessor's DoD #1, and the unit
that makes every later unit honest. It has exactly two parts:

1. **Prove the fixed gate actually runs on a real CI pull request** — not a
   local `dotnet run`, but the `code` job in `.github/workflows/ci.yml`
   failing a PR that raises a baseline `max` without an `adr:` reason, and
   passing one that does. The gate logic is already fixed (commit
   `ebf5b3b5`); this part is the *end-to-end* check of the one case the fix
   was built for — `CODE_BASELINE_BASE` = the PR's base branch — which no
   local test can exercise because locally the base is `origin/development`
   and the file is present.
2. **Resolve the 30 unjustified raises** the fixed gate now surfaces, in the
   two shapes the plan (D1, D2) names: the **4 shrinkable** god-type/method
   raises, and the **26 boundary-coupling** raises. After this unit, running
   the gate against the predecessor's "before" is either green or every
   raise carries a named `adr:`.

**You are NOT** doing C2-01…C2-08. You are NOT the translation-lane collapse
(#5), the module-surface reduction (#6), the still-large files (#3/#4), the
two untested services (#7), or the SEC pointer (#8). You are NOT re-opening
the part-vs-whole splits (C05/C07/C08) or the doctrine rules (C00) — those
are met and stay. You are NOT touching routes, rendered HTML, document
schemas, or adding a feature.

**The gate is already fixed — do not re-fix it.** The three defects (the
hardcoded `origin/main`, the silent skip on an unreadable base, and the guard
bug) were fixed in `ebf5b3b5` and are verified. Your job is to *prove the CI
path* and to *work the raises*, not to touch the gate logic again. If you
find a genuine fourth defect in the gate, stop and say which and why — do not
silently patch it; that would change the very thing this lane inherits.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `ebf5b3b5` (the gate-fix commit).
- Commit style: `fix(improve-code-2): C2-00 — <short description>` (conventional
  commits, em dash, **no ADR in the subject** unless the commit *is* the ADR
  the `adr:` field points at).
- Read `AGENTS.md` and `.github/copilot-instructions.md` first — they are the
  doctrine you are enforcing. Read the plan:
  `docs/archive/lanes/improve-code/plan-improve-code-2.md` — the sections **Why now**,
  **D1**, **D2**, and **C2-00** are your spec. Everything else is context for
  later units.
- Read the predecessor's record: `docs/archive/lanes/improve-code/`
  (`plan-improve-code.md` for the DoD lines, `handoff-notes.md` for the
  authoritative 30-raise breakdown and the C13 cold-read method).
- The gate you are inheriting: `tools/code/CodeMetrics/Program.cs`
  (`CheckOriginMain` — the `CODE_BASELINE_BASE` / fail-closed /
  first-introduction logic), `tools/code/code-check.ps1` (the wrapper), and
  the `code` job in `.github/workflows/ci.yml` (`fetch-depth: 0`,
  `CODE_BASELINE_BASE: origin/${{ github.base_ref || 'release' }}`).
- The baseline: `tools/code/code-baseline.json` (unchanged by the gate fix —
  25 files · 91 methods · 7 classes · 8 interfaces · 478 boundary entries ·
  21 controllers_marten_allowlist). The 30 raises are the diff between this
  file and the predecessor's "before" (`311d0a4b`).

## Part 1 — prove the fixed gate runs on a real CI pull request

The five gate behaviors are already verified locally (first-introduction
skips, missing-base fails closed, readable-base reports, local default
works, wrapper propagates exit). The one path only CI exercises is
`CODE_BASELINE_BASE` = the **PR base** with a *shallow→full* fetch. Prove it
end-to-end, without relying on a human to push a PR:

- **The failing case:** simulate a PR whose base is the predecessor's
  "before" — i.e. run the gate exactly as the `code` job would, with
  `CI=true` and `CODE_BASELINE_BASE` set to the "before" ref
  (`311d0a4b`), against the current baseline. It must **fail** and list the
  30 raises. This is the "a PR that raises a baseline `max` without `adr:`
  is rejected" proof.
- **The passing case:** stage the Part-2 resolution (the 4 shrinks under the
  ceiling *and/or* the `adr:` fields), re-run the same CI-shaped command, and
  show it **passes** (or that the only remaining entries carry a named
  `adr:` the gate accepts).
- **The wiring check:** confirm `.github/workflows/ci.yml` sets
  `CODE_BASELINE_BASE: origin/${{ github.base_ref || 'release' }}` and
  `fetch-depth: 0`, and that the `code` job runs on the `pull_request:
  [release]` trigger. Do not change the trigger or the ref expression —
  change only what a genuine wiring defect would require, and say so.

Record both the failing run and the passing run (the exact command, the
`CODE_BASELINE_BASE` value, and the exit code) in this unit's handoff note
entry, so the successor and the close can see the gate bit on the real
"before."

## Part 2 — resolve the 30 unjustified raises

The fixed gate reports 30. Resolve them in the two shapes the plan names.
**A raise is resolved when the gate no longer fails on it — either the entry
retired (the type shrank under the ceiling) or it carries an `adr:` field
that names the ADR it amends or creates.**

### 2a. The 4 shrinkable god-type / method raises (D2)

These are over the *ceiling* (class 25 / interface 20), not merely over the
predecessor's baseline, so shrinking under the ceiling retires the entry
cleanly:

| Baseline entry | Ceiling | Now (baselined) |
|---|---:|---:|
| `Core/Events/EventService.cs` **class** (public methods) | 25 | **31** |
| `Core/Pages/PageService.cs` **class** (public methods) | 25 | **30** |
| `Core/Events/IEventService.cs` **interface** (methods) | 20 | **28** |
| one method **cc_max** (cyclomatic complexity) | 15 | **22** |

Shrink each by the same discipline the predecessor applied to
`UserInfoService` / `ProjectService`: a **named** sub-service per the
part-vs-whole rule (each must have ≥2 callers, or its own unit test, or an
owned route family / aggregate — the predecessor's C13 cold read verified
that rule is not ceremony), with each extracted part unit-tested. The
cc_max-22 method splits on a decision seam (a `*Policy` per the C04 pattern,
with a test) or extracts its branchy helper. Only if a surface is genuinely
warranted does it stay — then it carries an `adr:` field naming the ADR.

**Do not** produce a ceremony type to hit a number. The part-vs-whole rule
is the gate's anti-ceremony teeth; a split that fails it is folded back.

### 2b. The 26 boundary-coupling raises (D1)

These are cross-module edge *counts* that went up (e.g.
`Web->Identity:User` 71→74, `Web->Tags:Tag` 7→9,
`Projects->Authorization:Audience` 10→12, `Web->Messaging:Message` 18→21).
They are **not** a size problem — the C06/C07/C08 splits *intended* the
coupling. The honest default is a named `adr:` on the affected entries; the
improvement is picking out the ones that are *accidental* integration and
reducing them. For each of the 26:

- **Intended coupling** (the split deliberately crosses that edge) → add an
  `adr:` field naming the ADR (the predecessor's ADRs 0286/0287 are the
  likely owners; a new amending ADR if none covers it).
- **Accidental coupling** (a concrete `*Service` where an interface/policy
  would do) → that is C2-03's territory; *note it here* with the edge and the
  file, and let C2-03 do the contract extraction. Do not do C2-03's work now.

A blanket `adr:` on all 26 with no per-edge read is the floor, not the
ceiling. The unit's handoff note entry must say, for each of the 26, which
ADR covers it and whether it is intended or flagged-accidental.

## Definition of done (C2-00)

- [ ] Part 1: the CI-shaped failing run (30 raises) **and** the passing run
      are recorded with the exact command, `CODE_BASELINE_BASE`, and exit
      code; the `ci.yml` wiring is confirmed (not changed unless a genuine
      defect is named).
- [ ] Part 2a: each of the 4 shrinkable entries is either retired (shrink
      under the ceiling, part unit-tested, part-vs-whole passes) or carries a
      named `adr:`.
- [ ] Part 2b: each of the 26 boundary entries carries a named `adr:` or is
      flagged-accidental (named for C2-03). No raise is silent.
- [ ] `tools/code/code-baseline.json` — the changed entries: every raised
      `max`/`count` either lowered, removed, or carries `adr:`.
- [ ] `dotnet build Kumunita.slnx -c Debug` — 0 errors. The affected
      extracted parts have unit tests that pass (run them, not just build).
- [ ] `tools/code/code-check.ps1` — green on the current tree, **and** the
      fixed gate run against the predecessor's "before" no longer reports an
      un-`adr`'d raise.
- [ ] `tools/docs/improve-check.ps1` — ALL GATES PASS.
- [ ] The handoff note for this unit is written (the running log — the
      "scratch" tier — next to this register, per the three-tier contract),
      recording the before → after for each of the 30.
