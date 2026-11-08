---
id: is41-prompt
title: "IS41 prompt — doc-truth pass: correct the doc/code lies + land the four lane lessons as rules"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS41 — Doc-truth pass + the lessons as rules (E10/E11/E13 + the four rules)

## What you are doing

You are the **forty-third working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS40 are committed
(IS40 = `5e2fa559`).

This unit is a **docs-only pass**: fix the remaining doc/code divergences and
land the lane's four hard-won lessons as **rules** the next agent reads on
session start. No code, no build, no new feature surface.

Three findings drive it:

- **E10 (M):** `data-classes.md` has a five-class prose table but no pointer to
  the code that now carries the per-type policy (`[DataClass]` + design §2.5,
  landed by IS16–IS20). New types (sessions, outbox, group messages) are absent
  from the prose. The prose and the code have drifted apart.
- **E11 (M):** `decisions-open-items.md` still claims "tokens hashed at rest"
  for the verify lane, whose actual link credential is the token **row id**
  (owner decision U23 = n/a, 2026-10-31). The B4 row now correctly says class
  (b) crosses B4 (IS09 landed that), but the token-at-rest sentence is still
  the old F2 claim.
- **E13 (L):** the "named invariant tests" open item is stale —
  `EvaluateAudience_EmptyAudience_AllMode_Denies` and
  `C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded` exist and are pinned
  by `AccessViaDenyMatrixTests.EmptyAudienceDenies_AndDelegationScope_ArePinned`
  (IS04). Close the item with links.

And the **four lessons** (the rules the lane learned, per DoD-12): a new doc
type carries `[DataClass]`; a new audience-bearing read is decided in-method or
marked `// SEC-READ:`; log templates never carry PII; views never carry inline
handlers. Each is enforced by a `SecGate` test. These go into
`AGENTS.md`'s "Conventions to preserve" section.

The plan's **IS41** section (`plan-improve-sec.md`, search `#### IS41`) is the
spec.

## The one thing to get right

**≤ 4 docs modified, no code.** Each fix is small and surgical:

| Doc | Change |
|---|---|
| `docs/security/data-classes.md` (53 lines) | Keep the five-class prose (§3, lines 29–36). Add a pointer sentence after the table: the per-type policy now lives in `[DataClass]` (ADR 0298) + design §2.5 (`docs/design/improve-sec-design.md` §2.5, the 81-row table). Add the missing classes to the prose: sessions (`SessionDoc`), outbox (`OutboxEmail`), group messages (`GroupMessage`/`GroupConversation`). |
| `docs/security/decisions-open-items.md` (181 lines) | Three surgical edits: (1) correct the token-at-rest claim → "the verify-lane link credential is the token **row id** (owner decision U23, 2026-10-31; no separate hash needed)"; (2) close the stale "named invariant tests" item (line ≈ 177) — mark it **Closed** with links to `AccessViaDenyMatrixTests.EmptyAudienceDenies_AndDelegationScope_ArePinned` (IS04) and the two test names; (3) add a short line recording the retention decision (D6: the D6 defaults in `RetentionOptions`, IS33/IS34a/IS35). |
| `AGENTS.md` (96 lines) | Append one bullet to the **"Conventions to preserve"** section (≈ line 66): the four lane rules + "the `SecGate` tests enforce each." Keep it to 2–3 lines, matching the section's style. |
| (optional 4th) `docs/security/trust-boundaries.md` | If the B4 row needs a cross-ref to the D6 retention of the outbox/DM snippets, add one parenthetical. Only if the B4 row currently omits it — verify first. |

> **Do not rewrite `WhatsNew.cs`** (the plan's E11 says "historical,
> untouched"). The token-at-rest correction is a *current* doc fix in
> `decisions-open-items.md`, not a rewrite of the 0.68 entry.

## Constraints

- **Docs only.** No `.cs`, no `.ts`, no build. `git status` shows only the
  doc files (plus the handoff note).
- **Do not add a new ADR.** The four rules are conventions, not decisions —
  they go in `AGENTS.md`, not `docs/adr/`.
- **Keep edits surgical.** One bullet in `AGENTS.md`; two or three short
  paragraph edits in `decisions-open-items.md`; one pointer + one or two added
  rows in `data-classes.md`. Do not restructure any doc.
- **The four rules (exact, for the `AGENTS.md` bullet):**
  1. A new registered doc type carries `[DataClass]` (ADR 0298).
  2. A new read of an audience-bearing type is decided in-method or marked
     `// SEC-READ:` (IS15 gate).
  3. Log templates never carry a PII placeholder (IS08 gate).
  4. Views never carry an inline `on*` handler or `<script>` block (IS38/IS38c
     gate).
  All four are enforced by `SecGate` tests in `tests/Kumunita.Web.Tests/SecGate/`.

## Deliverables (≤ 4 docs)

| # | File | Change |
|---|---|---|
| 1 | `docs/security/data-classes.md` | pointer to `[DataClass]` + design §2.5; add sessions, outbox, group messages to the prose |
| 2 | `docs/security/decisions-open-items.md` | token-at-rest correction; close the stale "named invariant tests" item; record D6 retention |
| 3 | `AGENTS.md` | one bullet in "Conventions to preserve" with the four rules |
| 4 | (optional) `docs/security/trust-boundaries.md` | B4 cross-ref to D6 retention, if missing |

## Entry reads (only these — no whole-file reads beyond these)

- `docs/security/data-classes.md` (whole — 53 lines; the five-class prose + the
  §3.1 audit-log note).
- `docs/security/decisions-open-items.md` lines 140–181 (the two items to fix:
  the token-at-rest claim and the "named invariant tests" row at ≈ line 177).
- `AGENTS.md` lines 62–96 (the "Conventions to preserve" section + what
  follows).
- `docs/design/improve-sec-design.md` line 439 (the §2.5 heading — you need
  the exact text for the pointer).
- `docs/security/trust-boundaries.md` line 32 (the B4 row — verify whether it
  already mentions D6 retention).
- The plan's **IS41** section + the **E10/E11/E13** evidence rows + the
  **DoD-12** line.

## Verification

- [ ] `git status` shows **only** the doc files (plus the handoff note). No
      `.cs`, no `.ts`.
- [ ] `data-classes.md`: the five-class prose is intact; a pointer to
      `[DataClass]` + design §2.5 is present; sessions, outbox, group messages
      are named.
- [ ] `decisions-open-items.md`: the token-at-rest sentence now says "row id"
      (not "hashed"); the "named invariant tests" row says **Closed** with
      links; a D6 retention line is present.
- [ ] `AGENTS.md`: the "Conventions to preserve" section has the four rules +
      "the SecGate tests enforce each."
- [ ] No new ADR. `WhatsNew.cs` is untouched.

## Definition of done (IS41)

- [ ] ≤ 4 docs modified; no code; no new ADR; `WhatsNew.cs` untouched.
- [ ] The doc/code divergences (E10, E11, E13) are closed: `data-classes.md`
      points to the code; `decisions-open-items.md` is corrected; the stale
      item is closed.
- [ ] The four lane lessons are rules in `AGENTS.md`, each naming its
      enforcing gate.
- [ ] One commit (the doc changes + a `## IS41` handoff section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`); the
      prompt file is **not** part of the unit commit (lane convention).

## Next unit

**IS42 — Owner-ops ledger.** Merges the prod-hardening `## Summary` open items,
the SEC operator checks, and the new ones (`Retention__*` reviewed, the
`SENTRY_DSN` decision, the full-export runbook rehearsed) into a single
`docs/ops/owner-ops-ledger.md` with a "last verified" column. Linked from
`docs/ops/hardening-checklist.md` and `docs/SECURITY.md`. No values flipped;
the ledger is a record. This is the last content unit before `IS43` (run &
record) closes the lane.
