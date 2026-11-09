---
id: is44-prompt
title: "IS44 prompt — Independent verification (cold read): three probes against the DoD, each PASS/FAIL with evidence, probes reverted"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS44 — Independent verification (cold read)

## What you are doing

You are the **verifying agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS43 are committed
(IS43 = `d08718fe`).

This is the **lane's verification unit (of three)**: `IS43 → IS44 → IS45`.
IS44 does **one thing**: prove the lane's **Definition of Done** still holds,
from a **clean slate, with no lane context**, by running **three probes** and
reporting each DoD line **PASS/FAIL with evidence** — then **reverting every
probe** so the tree is left clean.

You are **not a working agent who fixes things. You are a verifier.** The
plan's **IS44** section (`plan-improve-sec.md`, search `#### IS44`) is the
spec, and the **Definition of Done** (the 13 lines at the end of the plan) is
your checklist.

The one thing to get right, in the philosophy's words: **the probe is the
control; your report is the record.** A probe shows the gate *would* catch the
next drift — that is what you are proving. If a probe does **not** fire (a gate
does not fail when it should), that is a **real finding** — report it, do not
paper over it, and do **not** edit the gate to make it fire.

## The one thing to get right

**Run the three probes, confirm each fires, report each DoD line PASS/FAIL with
evidence, and revert every probe (the tree ends clean).**

| # | Probe (throwaway — revert after) | What it must show |
|---|---|---|
| 1 | Add a throwaway **registered** doc type carrying a `UserId` — a `Schema.For<T>` in a throwaway `*DocTypes.Configure` — **without** `[DataClass]`. Run the Core suite. | `DataClassCoverageTests.EveryRegisteredDocType_HasADataClass` (Core, the P-1 gate) **FAILS**. |
| 2 | Add `logger.LogInformation("{Email}", e)` to any `src/**/*.cs` log site. Run the Web `SecGate`. | `LogTemplatePiiGateTests.NoLogTemplate_CarriesAPiiPlaceholder` **FAILS**. |
| 3 | As a GlobalAdmin with **no** `AdminOverride`, `GET admin/portability/export?scope=full`; and the **default** (content-free) export. | `scope=full` → **403**; the content-free archive contains **no `Message.json`**. (The existing IS28 `AdminPortabilityScopeTests` pin this — running them is the cleanest no-trace form.) |

Each probe is **reverted before you finish** (delete the throwaway file /
remove the log line; `git status` must come back **clean**). Probe 3 needs no
throwaway if you confirm it via the existing IS28 tests.

**The 3 known-reds are NOT probe failures** — they are the lane's recorded
drift (IS43, design §2.7): `SecGate.DataClassCoverageTests.
EveryResidentExportType_IsInUserScopeInventory`,
`SecGate.DataClassCoverageTests.EveryDeleteOrPseudonymizeType_IsHandledByAnEraser`,
and `ImproveHarnessTests.CodeCheck_Gate_Passes`. Do not fix them, and do not
let them read as a probe failing.

## Constraints

- **No fix, no gate edit, no allowlist row.** You *create throwaway probe
  files* to fire probes 1–2 and *revert them*. You do not edit any gate, test,
  or product code to make a probe pass.
- **Clean slate:** read only what a fresh agent would read (the DoD, the repo,
  the three probes). The handoff notes' IS05–IS43 narrative is the *artifact
  you are verifying*, not your context.
- **Leave no trace:** after reverting, `git status --short` shows **only** the
  handoff append. The probe files are deleted, not committed.

## Entry reads (only these — no whole-file reads)

- `docs/plans-milestones/plan-improve-sec.md` — the **IS44** section + the
  **Definition of Done** (the 13 lines).
- `docs/design/improve-sec-design.md` — `§2.7 — Run result (IS43)` (the
  before → after record + the 3 known-reds).
- The gate files you are probing (ranges only):
  `tests/Kumunita.Core.Tests/DataClassCoverageTests.cs`,
  `tests/Kumunita.Web.Tests/SecGate/LogTemplatePiiGateTests.cs`,
  `tests/Kumunita.Web.Tests/AdminPortabilityScopeTests.cs`.
- `docs/dev/testing-and-ci.md` — the xunit.v3 in-process runner form
  (`dotnet exec …/Kumunita.{Web,Core}.Tests.dll`; `dotnet test --filter`
  reports "Zero tests ran / exit 5" here — do not use it).

## Verification

- [ ] `dotnet build` → **0 errors**.
- [ ] **Probe 1** fires (the P-1 gate fails on the un-annotated throwaway
      type), then the probe is reverted and the gate is **green (81/81)**
      again.
- [ ] **Probe 2** fires (the log-PII gate fails on `{Email}`), then the probe
      is reverted and the gate is **green** again.
- [ ] **Probe 3** holds (`scope=full` → 403; content-free archive has no
      `Message.json`).
- [ ] The **13-line DoD** is reported **PASS/FAIL with evidence** for each
      line.
- [ ] After reverting: `git status --short` shows **only** the handoff append;
      the probe files are gone.

## Deliverables

| # | File | Change |
|---|---|---|
| 1 | `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` | **new `## IS44` section** (appended): the three probe results (command · expected · observed · PASS/FAIL), the 13-line DoD PASS/FAIL table with evidence, and a closing line **"probes reverted — `git status` clean."** **If any probe did not fire, or any DoD line is FAIL, the section records it verbatim and the lane does NOT close (IS45 is blocked).** |

One commit: the handoff append only. No `.cs` committed, no ADR, no config
value.

## Definition of done (IS44)

- [ ] All three probes **fired as expected** (evidence recorded) and were
      **reverted** (tree clean).
- [ ] The **13-line DoD** table is present, each line **PASS/FAIL + evidence**.
- [ ] The 3 known-reds are **acknowledged as expected** (not counted as a
      probe failure).
- [ ] One commit; the handoff append is the only changed file; the probes are
      not committed.
- [ ] **All-PASS → next unit: IS45** (close flip + archive: `WhatsNew.cs` +
      `WhatsNewTests` head-pin; `README.md` named-lane row; `STATUS.md`;
      `ARCHITECTURE.md`; ADR index verified; `MilestonesTests` untouched; move
      the lane files to `docs/archive/lanes/improve-sec/`). **Any FAIL → do
      not flip:** record it in the `## IS44` section and hand off to a
      successor.
