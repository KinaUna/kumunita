---
id: is40-prompt
title: "IS40 prompt — the control-map traceability gate: every row's seam resolves, every code row pins a real test"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS40 — The control-map traceability gate (M1: 9/24 → all rows traced)

## What you are doing

You are the **forty-second working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS39 are committed
(IS39 = `b7c69298`).

**Finding E8 (severity M):** the control map at `docs/security/control-map.md`
is the lane's "every threat must be covered" self-check. IS39 just rewrote it so
**every row links a resolving seam and every *code* row names an existing
pinning test** — but a hand-written table has no teeth: the next person who adds
a row can write a seam that 404s, or name a test that does not exist, and nothing
in the build will complain. That is the philosophy's *documentation as the
control*: the promise is in a table, not a gate.

This unit is the **gate half** of the traceability pair IS39 set up. IS40 adds
**one new test file** that parses `docs/security/control-map.md` and fails the
build when (a) any row's `Seam` link does not resolve to a real file, (b) a code
row names a test that does not exist, or (c) an owner-ops row has no resolving
evidence link. **This is a gate unit — one new test file, no product code, no
behavior change.** It reuses the existing `SecGate` house idiom (the IS04/IS15/
IS24/IS38b gates): read the tree, assert — no `TestServer`.

The plan's **IS40** section (`docs/plans-milestones/plan-improve-sec.md`,
search `#### IS40`) is the spec.

## The one thing to get right

**One new file** (`tests/Kumunita.Web.Tests/SecGate/ControlMapTraceabilityTests.cs`)
that parses the control-map table and pins its three invariants. The three tests
the plan names, and what each asserts on the **current** 33-row table (24
existing + 8 new, IS39):

| test (plan name) | asserts |
|---|---|
| `EveryRow_HasASeamLink_ThatResolves` | every data row's **`Seam`** column has ≥ 1 markdown link, **and every markdown-link target in that cell resolves** to an existing repo-relative file (a `../../src/…` code seam, a `../ops/…` runbook, an `../adr/…` ADR, the `../../Dockerfile`, …). No dead link. |
| `EveryCodeRow_NamesAPinningTestThatExists` | for every row whose **`Pinned by (test)`** cell is **not** the owner-ops marker, every backtick-quoted test identifier in that cell is known to the test tree (`SecGateSource.TestMethodExists(name)`). |
| `EveryOpsRow_LinksARunbookThatExists` | for every row whose **`Pinned by (test)`** cell **is** the owner-ops marker, the `Seam` column carries ≥ 1 markdown link whose target resolves (a `../ops/…` runbook, or the ADR / `decisions` / `Dockerfile` that records the posture). |

**The owner-ops marker** (exact string, the gate's code/ops discriminator):
`— (owner-ops, no code seam)` — a `Pinned by (test)` cell that is exactly this
is an owner-ops row; any other content is a code row that must pin a real test.

> **One naming nuance to respect (do not trip on it):** the plan's third test
> is called `EveryOpsRow_LinksARunbookThatExists`, but two owner-ops rows do
> not link a `../ops/` runbook — "Pinned image + packages" links the `Dockerfile`
> + a `decisions` entry, and "PBKDF2 password hashing" links ADR 0172. So the
> honest invariant is **"the owner-ops row has a resolving evidence link"**
> (runbook *or* the ADR / decision / Dockerfile recording the posture), not
> "must link a `../ops/` file." Implement that; keep the plan's method name.
> (IS39 already verified all 25 `src/` + 18 ADR/ops links resolve, so this
> passes on the current table.)

## The contract (exact)

**New file:** `tests/Kumunita.Web.Tests/SecGate/ControlMapTraceabilityTests.cs`

- `namespace Kumunita.Web.Tests.SecGate;` (same as the other `SecGate` gates).
- `public class ControlMapTraceabilityTests` with three `[Fact]`s:
  `EveryRow_HasASeamLink_ThatResolves`, `EveryCodeRow_NamesAPinningTestThatExists`,
  `EveryOpsRow_LinksARunbookThatExists`.
- A doc-comment on the class: IS40 (IMPROVE-SEC), the E8/M1 pin
  ("9/24 → all rows traced"), and the one-line rationale (*the parts are cheap,
  the linkage is rare — this is the linkage*).
- Reuse the shared reader **`SecGateSource`** (same assembly, already present):
  `SecGateSource.ReadSrc("docs/security/control-map.md")`,
  `SecGateSource.TestMethodExists(name)`, `SecGateSource.RepoRoot` — do **not**
  re-implement the repo-root walk-up.
- **Parsing the table (the only fiddly part):** from the file text, take the
  contiguous block of lines starting with `|` under the `## 5. Control map`
  heading; drop the header line (`| Control | …`) and the `|----|` separator.
  Split each row on `|`, trim the fields, and drop the leading/trailing empty
  ones → `[Control, Answers, Seam, Pinned by (test), ADR]`. Extract markdown
  links in the `Seam` cell with a regex like
  `\[([^\]]+)\]\(([^)\s]+)\)` and resolve each target against `SecGateSource.RepoRoot`
  (strip any `#fragment`; `File.Exists` must be true). Extract backtick-quoted
  identifiers in the `Pinned by (test)` cell with `` `([^`]+)` `` and, for code
  rows, assert `TestMethodExists` on each (a class name or a `Class.Method` —
  `TestMethodExists` is a substring scan of `tests/**/*.cs`, the IS04 idiom;
  `Class` and `Class.Method` both satisfy it since the method name appears).
- **Do not hardcode the 33 rows.** The gate parses the live table, so the next
  person who adds a row is automatically covered — that is the whole point.
- **Fail with a useful message:** name the offending row (`Control` text) and
  the dead link / missing test, so the failure points straight at the fix.

## Constraints

- **One new file** (the test class) + the handoff note. No product code, no
  change to `control-map.md` (if a seam/test name is *actually* wrong, do not
  "fix" the table silently — that is IS39's file; pause and record per the
  SEC protocol, rule 9).
- **Do not weaken a test to make it pass.** If the current table legitimately
  fails one, that is a real finding to surface (a dead seam or a mis-named test),
  not something to special-case. (Expected: all three green on IS39's table.)
- **Do not edit `SecGateSource.cs`** unless a needed helper is genuinely absent —
  the four members above cover this unit.
- The house gate idiom: **string pin, no `TestServer`**; read the source tree
  and assert (the `F7F8_HardeningGateTests` / `LogTemplatePiiGateTests` precedent).

## Deliverables (1 new file)

| # | File | Change |
|---|---|---|
| 1 | `tests/Kumunita.Web.Tests/SecGate/ControlMapTraceabilityTests.cs` | new gate: 3 `[Fact]`s over the parsed control-map table (seam resolves / code row pins a real test / owner-ops row has a resolving evidence link) |

## Entry reads (only these — no whole-file reads beyond these)

- `docs/security/control-map.md` (the `## 5. Control map` table + the intro —
  this is the artifact being parsed; ~74 lines).
- `tests/Kumunita.Web.Tests/SecGate/SecGateSource.cs` (whole — 70 lines; the
  four members you reuse: `RepoRoot`, `ReadSrc`, `AllSrcFiles`,
  `TestMethodExists`).
- `tests/Kumunita.Web.Tests/SecGate/AccessViaDenyMatrixTests.cs` lines 1–45
  (the house gate style: namespace, doc-comment, `TestMethodExists` reuse,
  `[Fact]` shape) — and one more `SecGate` gate of your choice for the
  link-extraction idiom.
- The plan's **IS40** section + the **IS39** section (what IS39 put in the table)
  + the **DoD-7 / P-7** line (the shape the gate must prove).

## Verification

- [ ] `git status` shows **only** the new test file (plus the handoff note).
- [ ] Build green; `dotnet exec Kumunita.Web.Tests.dll -class
  "Kumunita.Web.Tests.SecGate.ControlMapTraceabilityTests"` →
  **Total: 3, Failed: 0, Skipped: 0**.
- [ ] **The gate actually bites** (the point of the unit): temporarily break one
  thing and confirm red — (a) change a seam link to a bogus `src/…` path →
  `EveryRow_HasASeamLink_ThatResolves` fails; (b) rename a pinned test in the
  table to a nonsense name → `EveryCodeRow_NamesAPinningTestThatExists` fails.
  Then **revert** the break (the table must end unmodified).
- [ ] Full suite: `dotnet exec Kumunita.Web.Tests.dll` shows no *new* failure.
  (The pre-existing baseline reds — `DataClassCoverageTests`
  `EveryResidentExportType…` / `EveryDeleteOrPseudonymizeType…`, and
  `ImproveHarnessTests.CodeCheck_Gate_Passes` — are recorded for their owner
  units and are **not** IS40's; do not chase them.)

## Definition of done (IS40)

- [ ] One new file; the three `[Fact]`s parse the live table (no hardcoding) and
      fail with the offending row + the dead link / missing test named.
- [ ] All three green on IS39's current table; the gate fails on an injected
      dead seam and on an injected mis-named test (then reverted).
- [ ] No product code touched; `control-map.md` ends unmodified; `SecGateSource`
      reused, not re-implemented.
- [ ] One commit (the new test + a `## IS40` handoff section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`); the
      prompt file is **not** part of the unit commit (lane convention).

## Next unit

**IS41 — Doc-truth pass + the lessons as rules.** Fixes the doc/code lies
(`decisions-open-items.md` token-at-rest, the stale "named invariant tests" item,
`data-classes.md` → a pointer to `[DataClass]` + design §2.5), and lands the
four lane lessons as *rules* in `AGENTS.md` (a new doc type carries `[DataClass]`;
a new audience-bearing read is decided or `// SEC-READ:`-marked; log templates
never carry PII; views never carry inline handlers — the `SecGate` tests enforce
each). This is the last unit before `IS43` (run & record) closes the lane.
