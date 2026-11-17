---
id: x02-prompt
title: "Prompt: IMPROVE-UX X02 — the cold-read close"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/DOCS-CONVENTIONS.md
lane: improve-ux
updated: 2026-11-13
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **X02** — the lane's cold-read close. A fresh agent that has
> **not** seen the lane runs the gate, walks all 12 journey rows on a
> running instance, checks every DoD line, and appends the `## X02`
> handoff note with before → after numbers. **Who is it for:** the next
> coding agent (fresh context). **When to read the rest:** when starting
> X02 (X01 + all R/A/O units have shipped; this is the last unit).

# Fresh-context prompt: IMPROVE-UX X02

You are closing the Kumunita IMPROVE-UX lane on `development` (HEAD
**`53957693`, O06 just closed** — the operator runbook records the
escalation outcome as parked). Behind it: **every other unit in the
lane** — X01 (the gate), R01–R33 (resident), A01–A15 (admin/moderator),
O01–O06 (operator) — has shipped. X02 is the **cold-read close**: you
verify the lane's own Definition of Done against a running instance and
record the result in the handoff note. The plan card:

> X02 — **Close + cold read**: a fresh agent that has not seen this lane
> runs the gate, walks every journey row below on a running instance
> (closed-loop + handoff tests), checks each DoD line, writes the handoff
> note. Depends: all. **Done when:** handoff note with before → after,
> or an honest held close.

**The one thing to get right.** X02 is a **verification + documentation**
unit — no new code, no new feature. You are an *auditor*, not a builder.
The output is an honest `## X02` section in
`improve-ux-handoff-notes.md` that a cold reader (or the SEC lane) can
use to understand the lane's true final state. Where the DoD is met, say
so plainly with the measured number. Where it is **not** met — a
measure-artifact, a parked item, a residual count that is not a defect —
say that honestly and cite the unit that recorded the residual (the R33
handoff precedent). **Do not force a number to 0 to make the DoD line
pass.** An honest held close is the plan's own exit condition.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) — the doctrine, including "Don't pause
  mid-task to check in" and the don't-pause rule.
- **`docs/DOCS-CONVENTIONS.md`** — the shared doc rules (front-matter,
  the **linking rule**, the tier model). The handoff note is a doc change.
- **`plan-improve-ux.md`** — the **X02 card**, the **Journeys table**
  (12 rows), the **Definition of Done** (8 lines), and the **"Not in this
  lane"** list. These are your checklist.
- **`improve-ux-handoff-notes.md`** — read the **last 3 sections**
  (R32, R33, O06) to match the register and the before → after shape.
  The note is append-only; do not edit existing sections.
- **`tools/ux/ux-baseline.json`** — the current baseline counts (the
  `generated` note at the top tells you what each re-base is for).
- **`git log --oneline -3`** + `git status` — confirm O06's `53957693`
  is intact; you are on `development`.

**The work** (all of it is verification + one append to the handoff
note):

### 1. Run the gate

```bash
python3 tools/ux/ux-measure.py --check
```

Record the output. DoD line 2 requires "no tracked count increased" and
lists four counts that must reach **0** (emit-without-link, jargon, a11y
leads after R23, plain/none empty states ≤ 1). Check each one against
the actual numbers in the output. If a count is non-zero, check the
handoff notes to see whether a previous unit already recorded the
residual as a measure artifact (the R33 `empty_plain` = 3 and
`empty_none` = 31 precedent) — cite that unit's section rather than
re-explaining it.

### 2. Run the build + test suites

```bash
dotnet build
dotnet test
```

Both must be green (DoD line 7). If a known-red test fails, check the
handoff notes for the known-reds list; note it but do not fix it.

### 3. Start the instance and walk the journeys

The 12 journey rows (from `plan-improve-ux.md` § "Journeys for the cold
read") are your test script. Start the app (`dotnet run --project
src/Kumunita.Web --launch-profile http`), then walk each row:

| # | Audience | Journey | What to verify |
|---|---|---|---|
| 1 | Resident | Report a post → see the outcome | Inbox row links to "My reports" with outcome; post hidden if so decided |
| 2 | Resident | Report an issue → hear back | Issue shows its resolution note on "My reports" |
| 3 | Resident | Follow a notification to deleted content | Friendly status page with nav (not a blank body) |
| 4 | Resident | Get assigned a to-do | Inbox row opens the to-do |
| 5 | Resident | Leave the platform | Deletion scheduled, cancellable, executes after the grace period |
| 6 | Kiosk viewer | Wall screen in a common room | Runs on a kiosk link; private items never shown; owner revokes it |
| 7 | Guest | Arrive, learn the window, reach an admin | End date visible; message link works |
| 8 | Moderator | Report filed in my community → act | Inbox row + badge → hide/remove from the report → reporter told |
| 9 | Admin | Invite a neighbour while sign-up is closed | Invitation accepted, admin notified |
| 10 | Admin | Resident needs more space | Request arrives; override set in-app |
| 11 | Operator | First boot to healthy | `/admin/status` green, no SeedAdmin warning, no log reading |
| 12 | Operator | SMTP breaks | Inbox alarm + red status row, not only an email |

For each row, record: **pass** (journey completes in-app with no
copy-paste, re-key, or "ask the admin") or **held** (state what is
missing and cite the unit or parked item that explains it).

> **Note on running the instance:** if the Postgres dependency is not
> available (the instance won't start), record that fact and mark the
> journey rows you could not execute as **held (environment)**, not
> failed. The DoD line 1 says "on a running instance" — if you cannot
> get one, say so honestly.

### 4. Check each DoD line

| DoD # | Requirement | How to check |
|---|---|---|
| 1 | Every journey row passes on a running instance | § 3 above |
| 2 | `ux-measure.py --check` green; four counts reach 0 | § 1 above |
| 3 | HTML page requests never render a blank error body (R02 tests) | `dotnet test --filter FullyQualifiedName~StatusCodePages` or the R02 test class |
| 4 | Two SEC review notes (R14, R19) exist; findings fixed or filed | Read both sections in the handoff notes |
| 5 | `how-it-works.md` + two runbooks match shipped behaviour; docs-check passes | R22 + O05 handoff sections; `pwsh tools/docs/docs-check.ps1` (note pre-existing failures, don't fix) |
| 6 | ADRs written for D2, D3 (amends 0142), D4, D6, D7, D8 | `ls docs/adr/01{50,51,52,53,54,55,56,57,58,59,60,61,62,63,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79,80}*.md` — confirm each named ADR exists |
| 7 | `dotnet build`, unit + integration tests green; translation parity green | § 2 above |
| 8 | Handoff note records before → after numbers, honestly | This note |

### 5. Append the `## X02` section to `improve-ux-handoff-notes.md`

Append (do not edit existing content) a section shaped like the other
entries:

```
## X02 — cold-read close (the lane's Definition of Done, verified) — **SHIPPED** (or **HELD**)

### Gate + build
- `ux-measure.py --check`: <output summary, cite any non-zero counts and
  the unit that recorded them as measure artifacts>
- `dotnet build`: green / <failure>
- `dotnet test`: <N passed, N failed, cite known-reds if any>

### Journeys (12 rows)
| # | Journey | Result | Note |
|---|---|---|---|
| 1 | Report a post → see the outcome | pass / held | … |
…

### DoD lines
| # | Requirement | Status | Note |
|---|---|---|---|
| 1–8 | … | met / held | … |

### Summary (one paragraph)
<The lane's final state: what shipped, what is parked, what residual
counts remain and why they are not defects. This is the sentence a cold
reader or the SEC lane reads first.>
```

### 6. Front-matter + commit

- `improve-ux-handoff-notes.md`: no front-matter change needed (it is a
  living doc, append-only; `updated:` should be `2026-11-13` if not
  already).
- **One commit** (the house rule):
  `docs(ux): cold-read close — X02 handoff note (IMPROVE-UX X02)`
- Commit only `improve-ux-handoff-notes.md`. Do **not** commit unrelated
  working-tree changes (the prompt files under `in-progress/` are
  untracked — leave them).

**Scope rules:**

- **No code changes.** No `.cs`, no `.cshtml`, no `.json`, no test.
  X02 is verification + one append to the handoff note.
- **One commit.**
- **Honest register.** A held line is not a failure. Cite the unit that
  recorded the residual.
- **Do not** open a new lane, do not fix pre-existing docs-check failures
  in other docs, do not re-baseline `ux-baseline.json` (if a count
  genuinely regressed, that is a bug in a shipped unit — flag it in the
  note, don't paper over it).

**Before committing:** `python3 tools/ux/ux-measure.py --check` (must
pass) + `dotnet build` (green) + read your `## X02` section top to
bottom to confirm every line is honest and every held item is cited.

Then report: the gate result, the build/test result, the 12-row journey
table, the 8-line DoD table, the commit hash, and a one-line summary
(e.g. "The IMPROVE-UX lane closes with N of 12 journeys passing on a
running instance, 7 of 8 DoD lines met, and N parked items honestly
recorded — the handoff note is the cold reader's entry point").
