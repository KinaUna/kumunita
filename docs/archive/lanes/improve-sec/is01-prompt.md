# IS01 — Design doc Part 1: context, invariants, findings register

## What you are doing

You are the **first working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). Your unit is **IS01**, the
second of 46 (IS00, the plan + README pointer, is already committed).
IS01 has **exactly one deliverable**:

**Write `docs/design/improve-sec-design.md`, Part 1** — the design doc's
`## Context`, `## Invariants`, and `## Findings register` sections.
**No code. No build. No tests.** No other file in the repo changes in this
unit except the one new handoff-notes file named below.

**You are NOT** doing IS02 (Part 2a: pins + fix shapes + test names), IS03
(Part 2b: the 81-row data-class table), IS04 (the gate harness), or any
phase A–F unit. You are NOT writing `tools/sec/sec-measure.py`, NOT touching
`Program.cs`, NOT annotating doc types, and NOT fixing a single finding.
The findings in the register are *transcribed*, not re-audited: IS02
re-verifies every file:line, and any row it finds already fixed becomes "n/a"
in IS02, not now.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- Commit style: `docs(improve-sec): IS01 — <short description>` (conventional
  commit, em dash, no ADR in the subject — no ADR exists yet).
- Read **first**: `AGENTS.md` and `.github/copilot-instructions.md` — the
  doctrine this lane enforces (closed-loop, handoff test, part-vs-whole,
  audit-by-default, the doc conventions).
- Read **second**: `docs/plans-milestones/plan-improve-sec.md` in full —
  the **Decisions** table (D1–D10), the **Evidence** table (E1–E13), the
  **Why now**, and the **IS01** unit spec under "Phase 0". That unit spec is
  your contract; where this prompt and the plan differ, the plan wins.
- Read **third**: `docs/DOCS-CONVENTIONS.md` (front-matter, abstract,
  linking rules) and the design-doc shape the repo expects.
- No predecessor handoff exists yet — this is the lane's first working unit.

## The work

### 1. Create `docs/design/improve-sec-design.md` (target ≈ 180 lines)

Front-matter per `docs/DOCS-CONVENTIONS.md` — model it on
`docs/design/prod-hardening-f1-gdpr-deletion.md` (same folder, same tier):
`tier: living`, `kind: design`, `status: proposed` (the lane is `proposed`,
not accepted — do **not** invent an ADR number in the doc; ADRs 0298/0299
are created by IS16/IS25), `lane: improve-sec`, an `updated:` date, and the
`related:` list from the plan's front-matter. Include the standard abstract
block (one-paragraph summary + **Who is it for** + **When to read the
rest**) — `improve-check` enforces the doc rules.

Then exactly three sections, in this order:

**`## Context`** — 40–60 lines. Distill, do not copy, the plan's *Why now*
+ *What this lane is (and is not)*:

- The one-paragraph problem statement in the philosophy's words: the SEC and
  prod-hardening lanes closed the known holes (H1–L8, F1–F22, U08's
  `Decide()` matrix is clean); **this lane goes after the linkage** — every
  security/privacy promise gets a seam in the code and a test that pins it,
  instead of living in a doc, a runbook, or a reviewer's memory. *"The parts
  are cheap, the linkage is rare."*
- The four severity-S gaps (E1 instance export, E2 incomplete erasure, E3
  PII→Sentry breadcrumbs, E4 decision-free reads incl. the `TodoDetail`
  leak) in two or three lines each — with the *seam with no gate* framing,
  not the detail (the register below holds the detail).
- The scope correction, **verbatim in meaning**: this repo has **no
  tenant-by-slug model** — one instance per neighborhood (ARCHITECTURE.md);
  "tenant isolation" maps to community/component scoping, which U08
  verified and the `IS04` deny-matrix gate pins.
- What the lane is **not**: not a re-audit, not a re-fix of H1–L8/F1–F22,
  does not reopen owner decisions (U21, U23/U24, F6, F22 Option A), adds no
  feature surface beyond the fixes.
- The gate doctrine: every fix ships with a gate test that fails when the
  next change drifts; baselines start measured and are **retired to zero,
  never frozen** (the IMPROVE-CODE precedent).

**`## Invariants`** — the plan's **exact ids and wording** (do not
reword, do not renumber, do not add or drop any):

- **P-1** a doc type cannot be registered without a `[DataClass]`.
- **P-2** account deletion leaves no row whose policy says `Delete`, and no un-tombstoned principal where it says `Pseudonymize`.
- **P-3** no log template carries a PII placeholder; nothing below Warning reaches a third party.
- **P-4** every read of an audience-bearing type is decided in-method or marked with its gate.
- **P-5** no GlobalAdmin-only path yields class (b) content without an operator-granted override.
- **P-6** every class (a)/(b)/(c) type has a retention key or a `Keep` reason.
- **P-7** every control-map row links a seam and names a pinning test.
- **P-8** no inline script or handler in views.

Each invariant gets one supporting sentence: which finding(s) it closes
(e.g. P-5 closes E1) and which unit/gate enforces it (e.g. P-5 → IS25–IS30,
the `AdminPortabilityScopeTests` gate). One sentence each — the register
carries the evidence, not this section.

**`## Findings register`** — a table with **exactly 13 rows, E1–E13**,
transcribed from the plan's Evidence table. Columns: `# · Sev · Finding
(one line) · Where (file:line) · Principle / anti-pattern · Units`. Take
the rows **as written** — the *Where* column carries file:line references
measured at `main` `1970cfc4` (2026-11-06); **transcribe them verbatim and
say so in a table note**: "line numbers are the plan's measurement at
`1970cfc4`; IS02 re-verifies every one and corrects or marks n/a." Do **not**
spot-check them yourself beyond reading the plan's rows — IS02 owns
re-verification (that is the seam between this unit and the next, and
blurring it is exactly the drift this lane exists to prevent).

### 2. Create `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`

The lane's running log (the plan's per-unit contract says each unit appends a
`## IS<n>` section here). This file does not exist yet, so **create** it:
front-matter per the conventions (model on
`docs/archive/lanes/improve-code/improve-code-3-handoff-notes.md` —
`tier: living`, `kind: handoff`, `status: current`, `lane: improve-sec`),
a one-line abstract, and the **`## IS01 — Design doc Part 1`** section
(3–8 lines): the files created, the section list + line count of the design
doc, the register row count (13), the invariant ids (P-1…P-8), anything
noticed (e.g. a plan row that read ambiguously), and a pointer that IS02
re-verifies the file:line refs.

## Verification (all green before you finish)

- [ ] `dotnet build` is **not** run (there is no code in this unit — running
  it is a scope slip, not a verification).
- [ ] `pwsh tools/docs/improve-check.ps1` — **ALL GATES PASS** (this is what
  enforces the front-matter / abstract / linking rules on your two new files).
- [ ] The design doc renders as markdown with the three sections in order:
  Context → Invariants → Findings register.
- [ ] The Invariants section contains **exactly** P-1…P-8, verbatim ids and
  wording, no ninth invariant.
- [ ] The register has **13 rows** (E1–E13), each naming its unit(s), with
  the `1970cfc4` measurement note.
- [ ] No other file in the repo is modified (`git status` shows only the two
  new files).

## Definition of done (IS01)

- [ ] `docs/design/improve-sec-design.md` exists, ≈180 lines, correct
  front-matter, `status: proposed`, sections in order.
- [ ] Invariants P-1…P-8 are verbatim from the plan, each with its closing
  finding and enforcing unit.
- [ ] Findings register: 13 rows, E1–E13, verbatim *Where* refs + the
  measurement-at-`1970cfc4` note pointing at IS02.
- [ ] Handoff note exists with the `## IS01` section (3–8 lines, the shape
  above).
- [ ] `improve-check.ps1` ALL GATES PASS; nothing else in the repo touched.
- [ ] One commit, style `docs(improve-sec): IS01 — design doc part 1 (context, invariants, findings register)`.

**If a DoD line fails, stop and say which one and why — do not fix it
silently, and do not start IS02's work to make it pass.** A failed check is
a recoverable step, not a stopping point: read the error, adjust within the
unit's scope, continue.
