---
id: is42-prompt
title: "IS42 prompt — owner-ops ledger: one record of every operator step, each with a 'last verified' column"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS42 — Owner-ops ledger (E12)

## What you are doing

You are the **forty-second working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS41 are committed
(IS41 = `2a3a0817`).

This unit closes **E12 (L)**: owner-ops open items are scattered across two
archived handoffs (prod-hardening `## Summary`, SEC deferred list) with **no
single ledger** and **no "last verified" column**. The plan's **IS42** section
(`plan-improve-sec.md`, search `#### IS42`) is the spec.

This unit is a **docs-only pass**. It does **one thing**: create a single
ledger that records every step the *owner/operator* must run against a live
instance (a provider console, a prod volume, a CI runner, a break-glass
rehearsal) — each row naming **what proves the control is in place** and a
**"last verified"** column that is **blank until the owner actually runs it**.

The one thing to get right, in the philosophy's words: **the ledger is a
record, not a control.** It flips no values, opens no decisions, and asserts
nothing is done. A row whose "last verified" cell is blank is *open*, not a
failure — that is the whole point of the column.

## The one thing to get right

**≤ 3 docs (one new + two link edits), no code.** The core deliverable is a
single table. Merge exactly three source groups, deduped, one row each:

| # | Column | Meaning |
|---|--------|---------|
| 1 | **item** | the operator step, in one line (imperative). |
| 2 | **source** | where it came from — the `F-row` (e.g. `F22`), the `SEC unit` (e.g. `L1/U28`), or the `IS unit` (e.g. `IS30`). |
| 3 | **what proves it** | the witness that the control *exists in code/docs* — a test name, ADR, config key, or runbook section (this is what makes the row verifiable; it is **not** the owner's attestation). |
| 4 | **last verified** | **blank = not verified** (the default for every new row); a date + a one-word evidence note once the owner runs it. |

The three source groups:

- **Prod-hardening owner-ops open items** — the "Owner-ops open items" column of
  `docs/archive/lanes/prod-hardening/hardening-handoff-notes.md` `## Summary`
  (starts ≈ line 1186). The non-"—" cells: **F4** (pgBackRest/WAL + PITR
  drill), **F5** (live kill-all end-to-end), **F9** (disposable-emails.txt +
  Turnstile key), **F10** (`SENTRY_DSN`), **F11** (alert sink → human
  channel), **F12** (real `Community__SupportEmail`), **F13** (real support
  mailbox), **F14** (Coolify deploy gated on CI + runner Docker + branch
  protection), **F16** (run ClamAV on prod), **F17** (bounce webhook →
  `POST /api/mail/suppress`), **F18** (provider alarm rules + "last verified"
  stamp), **F19** (live E2E in CI + ADR 0050 gate open), **F20** (rollback
  drill + stamp), **F21** (enable Postgres TLS + CA bundle), **F22**
  (provision `kumunita_migrate` + demote app role + set
  `ConnectionStrings:KumunitaMigrate`).
- **SEC operator checks** — `docs/archive/lanes/sec/sec-handoff-notes.md`
  `## Summary` (≈ line 378) + its deferred list: **L1/U28** (account
  enumeration — parked), **L8c** (log-forging — parked), the **"to pin"
  digests** (`node:22`, `postgres:18`, `axllent/mailpit:latest`).
- **New in this lane** — **`Retention__*` reviewed** (the D6 keys,
  `docs/ops/configuration-reference.md` ≈ lines 92–99; review = confirm the
  day-counts are sane for this instance), **the `SENTRY_DSN` decision now
  safe** after IS05/IS08 (the PII floor + log gate landed, so enabling it no
  longer leaks PII — `configuration-reference.md` line 52), and **the
  full-export runbook rehearsed** (IS30 landed the break-glass "full instance
  export" runbook section; find it with `grep -n "full instance export\|break-glass" docs/ops/`).

> **De-dup is required.** F10's `SENTRY_DSN` and the lane's "SENTRY_DSN
> decision now safe" are **one** row (source `F10 + IS05/IS08`), not two.
> `Community__SupportEmail` appears in F12 and F13 — one row, source
> `F12/F13`.

## Constraints

- **Docs only.** No `.cs`, no `.ts`, no build. `git status` shows the new
  ledger, the two link edits, and the handoff note — nothing else.
- **No values flipped, no decisions made.** The ledger *records* owner steps;
  it must not say an item is done, must not change any config value, and must
  not close any open item. A blank "last verified" cell is the correct end
  state for every row this unit writes.
- **New ADR: no.** A ledger is a record, not a decision — no `docs/adr/` row.
- **Front-matter + abstract are required** (the doc rules in
  `docs/DOCS-CONVENTIONS.md`). Match the `docs/ops/` family — see
  `docs/ops/configuration-reference.md`'s header (`tier: reference`,
  `kind: runbook`, `audiences: [platform-maintainer]`) and its one-paragraph
  `> **Abstract:** … **Who is it for:** … **When to read the rest:** …`.
  Use `id: ops-owner-ops-ledger`.
- **Link the ledger, don't duplicate it.** Add exactly one `Related` bullet in
  `docs/ops/hardening-checklist.md` and one "related surfaces" bullet in
  `docs/SECURITY.md`, each one line pointing at the new file. Do not restate
  the table.

## Deliverables (≤ 3 docs)

| # | File | Change |
|---|------|--------|
| 1 | `docs/ops/owner-ops-ledger.md` | **new** — front-matter + abstract + the single ledger table (item · source · what proves it · last verified), covering the three source groups above, de-duped |
| 2 | `docs/ops/hardening-checklist.md` | one `Related` bullet linking the ledger (≈ lines 118–122) |
| 3 | `docs/SECURITY.md` | one "related surfaces" bullet linking the ledger (≈ lines 58–66) |

## Entry reads (only these — no whole-file reads beyond these)

- `docs/archive/lanes/prod-hardening/hardening-handoff-notes.md` lines 1186–1230
  (the `## Summary` table — read only the "Owner-ops open items" column for
  each non-"—" F-row).
- `docs/archive/lanes/sec/sec-handoff-notes.md` lines 378–400 (the `## Summary`
  + the "Deferred list" — L1/U28, L8c, "to pin" digests).
- `docs/ops/configuration-reference.md` lines 50–100 (the `SENTRY_DSN` row +
  the `Retention__*` rows, for the exact key names).
- `grep -n "full instance export\|break-glass" docs/ops/` — locate the IS30
  runbook section so the "full-export runbook rehearsed" row links it.
- `docs/ops/hardening-checklist.md` lines 116–122 (the `Related` section, for
  the link bullet).
- `docs/SECURITY.md` lines 58–66 (the "related surfaces" list, for the link
  bullet).
- `docs/ops/configuration-reference.md` lines 1–20 (front-matter format only).
- The plan's **IS42** section + the **E12** evidence row.

## Verification

- [ ] `git status` shows **only** the new ledger + the two link edits (plus the
      handoff note). No `.cs`, no `.ts`.
- [ ] `owner-ops-ledger.md` has front-matter + abstract; the table has exactly
      the four columns **item · source · what proves it · last verified**.
- [ ] Every non-"—" prod-hardening owner-ops F-row (F4, F5, F9, F10, F11, F12,
      F13, F14, F16, F17, F18, F19, F20, F21, F22) is a row.
- [ ] The three SEC operator checks (L1/U28, L8c, "to pin" digests) are rows.
- [ ] The three new lane rows (`Retention__*` reviewed, `SENTRY_DSN` decision
      safe, full-export runbook rehearsed) are rows and are **de-duped** against
      the F-rows they overlap.
- [ ] Every **last verified** cell is blank (no row is marked done).
- [ ] `hardening-checklist.md` and `SECURITY.md` each link the ledger.
- [ ] No new ADR. No config value changed. No open item closed.

## Definition of done (IS42)

- [ ] ≤ 3 docs (one new + two link edits); no code; no new ADR; nothing
      flipped.
- [ ] The ledger exists, is linked from both docs, and covers all three source
      groups with every "last verified" cell blank.
- [ ] One commit (the new ledger + the two link edits + a `## IS42` handoff
      section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`); the
      prompt file is **not** part of the unit commit (lane convention).

## Next unit

**IS43 — Run and record.** Full build, both full suites, and
`tools/sec/sec-report.ps1`; record before → after for the six baselines
(control-map pinned 9/24 → 100%; undecided erasure 25 → 0; PII log
placeholders 7 → 0; unmarked decision-free reads 52 → 0; inline handlers
8 → 0; unaudited admin reads 3 → 0) against the IS04 baseline; delete
`tools/sec/sec-measure.py`; **if a gate is red, stop and list it.** Then
`IS44` (independent cold-read verification) and `IS45` (close flip + archive).
