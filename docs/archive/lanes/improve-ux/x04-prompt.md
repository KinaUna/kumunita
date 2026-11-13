---
id: x04-prompt
title: "Prompt: IMPROVE-UX X04 — close F7 (the two missing ADRs, D2 + D8)"
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
> IMPROVE-UX **X04** — the successor to X03 (the cold-read successor,
> `fc8ae1c2`). X02's honest held close filed 7 findings (F1–F7); X03 closed
> F1 + F4 + F5. This unit closes **F7** — the **two missing ADRs** (D2 the
> status page, D8 the operator's instance status) — the one line of the
> lane's Definition of Done that X02 marked **not met** (DoD line 6: 4 of 6
> ADRs present). It is **doc-only**: two `docs/adr/` files, no code. F2, F3,
> F6 are explicitly **not in this unit's scope**. **Who is it for:** the next
> coding agent (fresh context). **When to read the rest:** when starting X04
> (X03 is committed at `fc8ae1c2`).

# Fresh-context prompt: IMPROVE-UX X04 (close F7 — the two missing ADRs)

You are on `development` (HEAD **`fc8ae1c2`** — X03's close of F1 + F4 + F5,
committed 2026-11-13). Behind it: **every unit in the lane** has shipped, and
X02's cold read filed 7 findings (F1–F7). X03 closed F1 (the A09 view-location
500), F4 (the `jargon` residual record), F5 (the `notify_without_link`
residual record). This unit closes the remaining **DoD line 6** gap: **F7 —
the two ADRs that are missing** (D2 the status page, D8 the operator's
instance status). The other four named ADRs already exist (D3 = [0301], D4 =
[0302], D6 = [0303], D7 = [0304]).

**The one thing to get right.** X04 is **two ADR decision records** — not a
code change, not a rewrite of what already shipped. The code is *done and
green* (R01/R02 status page, O01–O04 operator status). You are only writing
the decision records that document *what was decided and why*, per the repo
rule "a new capability that settles a design question gets an ADR under
`docs/adr/`". One commit, two new files (+ a front-matter `related:` touch-up
if you cite a design doc). **Do not open F2, F3, or F6** (F2/F3 need a seeded
resident + a live re-walk; F6 is another lane's `improve-sec` archive docs —
each is its own successor's unit).

## F7 — the finding (from X02's `## X02` § DoD line 6)

DoD line 6 requires ADRs for **D2, D3 (amends 0142), D4, D6, D7, D8**. X02
confirmed 4 of 6: D3 = [0301](../../../adr/0301-self-service-deletion-grace-period.md),
D4 = [0302](../../../adr/0302-shared-kiosk-link.md), D6 = [0303](../../../adr/0303-report-queued-routing.md),
D7 = [0304](../../../adr/0304-email-invitations.md). **D2** (the status page,
R01/R02) and **D8** (the operator's instance status, O01–O04) are **absent**
from `docs/adr/`. The plan's D2 card says "ADR (if non-trivial)" — the status
page *is* non-trivial (the HTML-only re-execute filter, the 6 status codes,
the nav, the 500-with-report affordance), so an ADR is expected. The O01–O04
work is a new Core seam (`IInstanceStatusService`) + a new Web page + a
config-warning policy — an ADR is expected. **The next free ADR numbers are
0305 + 0306** (0300–0304 are taken; 0305 + 0306 are free — verified).

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) — the doctrine, including "Don't pause mid-task
  to check in" + "a new capability that settles a design question gets an ADR".
- **`docs/DOCS-CONVENTIONS.md` § The ADR cap** — the ADR is a **~250-line
  target** (the gate buckets ≤250 / 251–400 / >400). The shape: **Status /
  Context / Decision / Consequences** stay in the ADR; **Context** is the
  *forces* (what pushed on the decision), not the options analysis; the
  options analysis moves to a linked design doc or
  [`docs/design/adr-handoffs.md`](../../../design/adr-handoffs.md). ADR
  **numbers are permanent** (the code cites them) — so pick 0305 + 0306 and
  never renumber.
- **`docs/DOCS-CONVENTIONS.md` § The linking rule** — a path mention that
  *names its path* becomes a **relative Markdown link**, not a backtick.
  Broken links are a gate failure.
- **`docs/adr/0304-email-invitations.md`** — the **front-matter template**
  (copy the exact field set: `id: adr-NNNN`, `title:`, `tier: reference`,
  `kind: adr`, `status: accepted`, `audiences:`, `related:`, `supersedes: []`,
  `superseded_by: []`, `lane: improve-ux`, `updated:`) and the prose shape
  (the `# ADR NNNN — <title>` heading, then `Status: Accepted`, `Date:`,
  `Builds on:` / `Amends:` lines, then the body).
- **`improve-ux-handoff-notes.md` § R01/R02 (status page) + § O01–O04
  (operator status)** — the shipped behaviour you are documenting. If the
  R01/R02 + O01–O04 sections are not in the note (doc-only units sometimes
  did not append), the **code + the ADRs named below** are the record.
- **`git log --oneline -5`** + `git status` — confirm `fc8ae1c2` is intact;
  you are on `development`; the untracked prompt files under `in-progress/`
  are not yours to touch.

## The work (all of it is one commit, two new ADR files)

### ADR 0305 — D2, the status page (R01/R02)

**The decision to record** (from the plan's D2 + the code):
- **One friendly status page site-wide**, rendered at `/status/{code}`,
  localized, keeping the nav, offering Back / Home / Search.
- Wired via `UseStatusCodePagesWithReExecute("/status/{code}")` **for HTML
  page requests only** — the re-execute is skipped when the `Accept` header
  lacks `text/html`, when `X-Requested-With` is set, and for `/health`,
  `*.ics`, `/media/`, `/kiosk/slide`, `/settings/kiosk/items`, `/api/`.
  fetch/JSON and machine endpoints keep their bare responses.
- The status codes served (404 / 403 / 400 / 413 / 415 / 429 / 500) and the
  **500-with-report** affordance (the `report-an-issue` lane, ADR [0154]).
- **Rejected alternatives** (from the plan's D2): per-action views (hundreds
  of edits); a "deleted on {date}" variant (deferred — *Not in this lane*).
- **The `Builds on:` / `related:` lines** to cite: ADR [0154] (production
  error handling — the report-an-issue affordance on the 500 page) and
  ADR [0155] (issue submission — the `/issues/new` target the 500 links to).
  The code surface: `src/Kumunita.Web/Controllers/StatusController.cs`,
  `src/Kumunita.Web/Views/Status/Index.cshtml`, the
  `UseStatusCodePagesWithReExecute` call in `Program.cs`, and the gate tests
  `StatusPageGateTests`.

### ADR 0306 — D8, the operator's instance status (O01–O04)

**The decision to record** (from the plan's D8 + the code):
- **`/admin/status`** (GlobalAdmin) — a read-only operator view: version +
  uptime, the `/health` checks, dead-letter count, mail-alarm state, storage
  metrics, and a **`ConfigWarningsPolicy`** (e.g. `SeedAdmin__*` still set
  after an admin exists, `SampleData__Enabled` in Production, `Health__Token`
  unset, Data Protection keys not persisted). **No secret values are
  returned.**
- A **one-line dashboard notice** (admin-only) when any status row is red,
  linking to `/admin/status`; **mail alarms also emitted as an inbox
  notification** (`ops.alarm`) to GlobalAdmins (so an alarm is not only an
  email when SMTP may be what failed).
- **Rejected alternatives** (from the plan's D8): config warnings only;
  leaving it to the deployment manager.
- **The `Builds on:` / `related:` lines** to cite: ADR [0136] (the platform
  storage limit — the storage-metrics source), ADR [0156] (storage metrics
  history), ADR [0162] (analytics history — the `/admin` trend-view precedent),
  ADR [0299] (the instance export is operator-gated — the operator-admin
  plane convention). The code surface:
  `src/Kumunita.Core/InstanceStatus/` (`IInstanceStatusService.cs`,
  `InstanceStatusService.cs`, `ConfigWarningsPolicy.cs`,
  `IMailAlarmStateReader.cs`, `InstanceStatusOptions.cs`),
  `src/Kumunita.Web/Controllers/AdminStatusController.cs`, the `ops.alarm`
  emit in the mail-alarm lane.

**ADR hygiene (the gate checks these):**
- Both ADRs **≤ ~250 lines** (the ADR cap). If you exceed it, move the
  options analysis / test lists to
  [`docs/design/adr-handoffs.md`](../../../design/adr-handoffs.md) and link it
  from the ADR's `related:`.
- Every path mention that names a path is a **relative Markdown link** (the
  linking rule) — no broken links.
- `tier: reference`, `kind: adr`, `status: accepted`, `lane: improve-ux`,
  `updated: 2026-11-13` (the ADR 0304 front-matter shape).

### The handoff append (one `## X04` section)

Append a short `## X04 — close F7 (the two missing ADRs, D2 + D8) — **SHIPPED**`
section to the end of `improve-ux-handoff-notes.md` (match the register of
the X02/X03 sections): the two ADR numbers (0305 D2 + 0306 D8), what each
records (the status page + the operator status), and the honest note that
**this closes DoD line 6 (now 6 of 6 ADRs present)**. Note F2/F3/F6 remain the
next successor's scope.

## Front-matter + commit

- **One commit** (the house rule, per the commit-message-style memory):
  `docs(adr): D2 status page (0305) + D8 operator status (0306) — close F7
  (IMPROVE-UX X04)`.
- Commit **only** the two new ADR files (`docs/adr/0305-*.md`,
  `docs/adr/0306-*.md`) + the `## X04` handoff append in
  `improve-ux-handoff-notes.md`. Do **not** commit unrelated working-tree
  changes (the untracked prompt files under `in-progress/` are not yours to
  touch).

**Scope rules:**

- **F2, F3, F6 are NOT in this unit.** F2 (J7 guest end-date) + F3 (J10
  request-more-space) need a seeded resident + a live re-walk; F6 is another
  lane's `improve-sec` archive docs. Each is a separate successor's unit.
- **Doc-only.** No code changes — the R01/R02 + O01–O04 code is already
  shipped and green. You are writing the decision records that document it.
- **One commit.** Two ADRs + one handoff append are one logical change
  (close DoD line 6).
- **Honest register.** The ADRs record what *was decided and shipped*, not a
  new design. Cite the code surface + the ADRs each builds on.

**Before committing:** `pwsh tools/docs/docs-check.ps1` (must pass — the two
new ADRs' links + front-matter + line count are gate-checked) +
`dotnet build` (green — confirm you did not touch code) + `python3
tools/ux/ux-measure.py --check` (green — the counts are unchanged; an ADR is
not a tracked count). Then read both ADRs top to bottom to confirm every
`related:`/`Builds on:` line names a real ADR + a real file, and read your
`## X04` section to confirm the DoD line 6 close is honest.

Then report: the docs-check + build + gate result, the two ADR numbers
(0305 D2 + 0306 D8) + one line on what each records, the DoD line 6 status
(now 6 of 6 ADRs present), the commit hash, and a one-line summary (e.g.
"IMPROVE-UX X04 closes F7 — the two missing ADRs (0305 the D2 status page,
0306 the D8 operator status) — the lane's last 'not met' DoD line (DoD 6) is
now 6 of 6 ADRs present; F2/F3/F6 stay the next successor's scope").
