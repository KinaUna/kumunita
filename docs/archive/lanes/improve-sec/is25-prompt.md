---
id: is25-prompt
title: "IS25 prompt — ADR 0299: the instance export is operator-gated"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS25 — ADR 0299: the instance export is operator-gated

## What you are doing

You are the **twenty-fifth working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS24 are committed.

This unit writes **one ADR** (≤ 80 lines) + the ADR index row. No code, no
build.

The E1 finding (severity S): the GlobalAdmin instance export
(`GET admin/portability/export`) carries **every private message**, all
audience-restricted posts, profile contact fields, and reports — contradicting
the messaging D3 wall ("a GlobalAdmin never gets a read of any conversation")
and ADR 0003's privacy default. The operator-gated design (D1): a GlobalAdmin
alone gets a **content-free** export; a **full** export requires an
operator-granted break-glass `AdminOverride` (the existing ADR 0003 path,
no in-app endpoint can grant it), is audited `Via: BreakGlass`, and leaves a
transparency entry residents can see (D10).

This ADR is the spec that IS26 (`BreakGlassProbe`), IS27
(`PortabilityExportScope` + the content-free filter), IS28 (controller gate +
audit), IS29 (resident transparency), and IS30 (export docs) implement.

## The one thing to get right

The ADR must **supersede** the C-M11·6 "quiet surface" line for the full scope.
The M11 design doc (`docs/design/m11-portability-design.md`) currently says
the full export is a "quiet surface" (no second party, no transparency).
ADR 0299 **does not change that for the content-free default** — it changes
it for the full scope only, which now requires the operator override and the
transparency entry. Read the C-M11·6 line, name it, and state the supersession
explicitly in the ADR.

## Deliverables (2 files)

1. **`docs/adr/0299-instance-export-operator-gated.md`** (≤ 80 lines).
   Content, in order:
   - **Context:** the E1 finding (GlobalAdmin instance export carries all
     private content; contradicts messaging D3 wall + ADR 0003 privacy default).
   - **Decision (D1, chosen 2026-11-06):** content-free by default; full only
     under an active `AdminOverride`; audited `Via: BreakGlass` with the scope
     recorded; resident transparency per D10 (a notification to every non-guest
     resident — the existing inbox, no new surface; `kind: instance.full-export`;
     states the date and that the operator authorised it, no actor name).
   - **Supersedes:** C-M11·6 "quiet surface" line **for the full scope only**
     (the content-free default is unchanged: no second party, no
     transparency — the GlobalAdmin gets structure + public content + their
     own data, which is not a privacy breach).
   - **Rejected alternatives:** strip private content entirely (loses
     migration; pushes the operator to raw `pg_dump`); accept and disclose
     (keeps a GlobalAdmin content read that contradicts the messaging D3 wall).
   - **Constraints:** no in-app endpoint can grant the `AdminOverride`
     (the ADR 0003 path — the operator grants it out-of-band); the
     `BreakGlassProbe.HasActiveAsync` seam (IS26) is the gate; the
     `PortabilityExportScope` enum (IS27) encodes the scope; the audit row
     records `Action = portability.export.{content-free|full}` and
     `Via = Admin|BreakGlass`.

2. **The ADR index row** in `docs/adr/README.md` (append after 0298):
   `| 0299 | The instance export is operator-gated — content-free by default; full only under an active AdminOverride, audited Via: BreakGlass, with resident transparency (D1/D10) | Accepted |`

## Entry reads (only these — no whole-file reads)

- `docs/adr/0003-roles-and-moderator-scoping.md` lines 40–70 (the
  `AdminOverride` / break-glass path, the privacy default).
- `docs/design/m11-portability-design.md` lines 148–170 (the current export
  description); the C-M11·6 constraint (`grep -n "C-M11·6"` in that file).
- The messaging D3 wall (`sed -n '160,180p docs/design/m9-messaging-design.md`
  — `### D3 · Participant-only access; no GlobalAdmin break-glass`: a GlobalAdmin
  never gets a read of any conversation).
- `docs/plans-milestones/plan-improve-sec.md` — the D1 row and the E1 row
  (the Decisions table + Evidence table, already in context via the plan
  attachment).
- `docs/adr/README.md` — the last 5 rows (the index format).

## Verification

- [ ] The ADR renders (≤ 80 lines; `grep -c "" docs/adr/0299-*.md` ≤ 80).
- [ ] The ADR names C-M11·6 explicitly and states the supersession scope
  (full only, not the content-free default).
- [ ] The ADR index row is appended (the table has a new `| 0299 |` row).
- [ ] No code touched; no build.
- [ ] No PII in the ADR (type names and seam names only; no fixture emails).
- [ ] One `## IS25` handoff section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (the ADR path, the line count, the C-M11·6 supersession sentence,
      the D1/D10 constraints, the rejected alternatives, next-unit notes).
- [ ] One commit.

## Definition of done (IS25)

- [ ] `docs/adr/0299-instance-export-operator-gated.md` exists, ≤ 80 lines,
      renders, names C-M11·6 and the supersession scope.
- [ ] The ADR index row is in `docs/adr/README.md`.
- [ ] No code; no build; no PII; one commit; one handoff section.
- [ ] **Next unit: IS26** (`BreakGlassProbe` — extract, don't widen the
      frozen seam; `src/Kumunita.Core/Authorization/BreakGlassProbe.cs`
      new, `public static Task<bool> HasActiveAsync(IQuerySession, string userId)`,
      the exact SQL moved from the private method; `AuthorizationService.cs`
      delegates to it; no `IAuthorizationService` change per ADR 0006).
