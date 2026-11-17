---
id: o06-prompt
title: "Prompt: IMPROVE-UX O06 — the escalation outcome is parked, and the provisioning pointer is there"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/ops/monitoring.md
  - docs/adr/0155-issue-submission-escalation.md
  - docs/adr/0159-escalation-authorization.md
  - docs/DOCS-CONVENTIONS.md
lane: improve-ux
updated: 2026-11-13
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **O06** — the last O-lane unit before the X02 cold-read
> close. One small docs-only edit to
> [`docs/ops/monitoring.md`](../../../ops/monitoring.md): record the
> **escalation outcome** as a *parked, by-design* limitation (E22 — the
> cross-instance protocol change) so a maintainer reading the operator
> runbook does not go hunting for a channel that was never built, and put
> the provisioning pointer at
> `claude/deployment-manager-plan.md` (a cross-repo plan, cited as a
> backticked path, **not** a Markdown link) where the runbook's "not in
> this lane" belongs. **Who is it for:** the next coding agent (fresh
> context). **When to read the rest:** when starting O06 (all O01–O05
> have shipped; this is the runbook tail of the D8 operator surface).

# Fresh-context prompt: IMPROVE-UX O06

You are continuing the Kumunita repository on `development` (HEAD
**`8b0e4016`, O05 just closed** — the two operator runbooks
`privacy-operations.md` + `first-boot.md` now match the shipped
behaviour). Behind it: the whole A-lane (A01–A15), the R-lane
(R01–R33), and the O-lane through **O05** have all shipped. O06 is the
**docs-only** tail of the operator surface — a single small edit to one
runbook. The plan card:

> O06 — Record E22 (escalation outcome) as parked and link
> `deployment-manager-plan.md` for provisioning — docs only. Read first:
> `docs/ops/monitoring.md`. Depends: —. **Done when:** parked item
> visible in the runbook.

The plan also says it explicitly (two places): D9 "escalation outcome
**parked** (cross-instance protocol change)", and the "Not in this lane"
list — "Cross-instance escalation outcome (E22) → parked (O06)".

The finding it records (E22): the operator's **escalation to the
maintainer is one-way** (`ErrorReportService.AcceptInboundAsync`,
`EscalationInboundController`). The escalation *mechanism* ships (the
origin forwards, the receiving instance accepts — ADRs
[0155](../../../adr/0155-issue-submission-escalation.md) /
[0159](../../../adr/0159-escalation-authorization.md)); what does **not**
ship is the **outcome channel** — the reporter/origin never hears back
whether an escalated issue was resolved. That return path is a
cross-instance protocol change, deliberately out of scope for this lane
(D9). The runbook is the one place an operator would expect to see that
called out, and it currently says nothing about escalation at all — so the
honest fix is a short "parked" note, not a feature.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) — the doctrine (the don't-pause-mid-task
  rule; "link, don't backtick").
- **`docs/DOCS-CONVENTIONS.md`** — the shared doc rules (front-matter, the
  **linking rule**, the **one-fact-one-place** rule, the tier model).
  O06 is a doc change; these rules apply.
- **`plan-improve-ux.md`** — the **O06 card** + the **E22** evidence row
  + D9 + the "Not in this lane" list + DoD #5.
- **`docs/ops/monitoring.md`** — the file you are editing. Read it in
  full; it is a short operator runbook (`/health`, uptime, logs, capacity,
  review) and today has **no** escalation or provisioning section.
- **`improve-ux-handoff-notes.md`** — skim the **O04 / O05** sections for
  the register and the "docs-only, one commit" shape O06 should match.
- **`git status`** + the branch (O05's `8b0e4016` must be intact; you are
  on `development`).

**The one thing to get right.** O06 is a **docs-only** unit — no code, no
test, no migration. This is an **operator runbook**, so the register is
precise technical ops prose for a maintainer/GlobalAdmin — *not* the
plain-English resident register. Two things are load-bearing:

1. **Record E22 as parked, by design — do not build it.** Add a short
   note (a couple of sentences, a single bullet or a one-line "Known
   parked" block is fine) that the escalation **outcome** channel — the
   reporter hearing back on an escalated issue — is **not built in this
   lane by design**: the escalation *forward/accept* mechanism ships
   (ADRs 0155 / 0159), but the return path is a cross-instance protocol
   change (D9) and is deliberately out of scope. A maintainer reading the
   runbook should see this as a known, intentional limit, not a bug they
   must chase.
2. **The provisioning pointer is a backticked prose path, NOT a link.**
   `claude/deployment-manager-plan.md` is a **cross-repo** plan — it is
   not under this repo's `docs/`, so a Markdown link to it would **fail
   the broken-link gate** (Gate 4). Cite it exactly as the plan does:
   inline code, `` `claude/deployment-manager-plan.md` `` — prose about a
   path, not a link. This is the one place the "link, don't backtick"
   rule *yields*, because the target is not a repo-relative doc.

**⚠️ Do NOT touch any `.cs`, `.cshtml`, `.json`, or test file.** O06 is
`docs/ops/monitoring.md` only (plus, if a second operator runbook
*already* owns the "what this lane does not do" list, that one — but
prefer the single file the card names). No ADR (nothing is being
decided — E22 is being *recorded as parked*, not resolved). Do **not**
open X02 (the cold-read close) — that is a separate, larger unit and the
next prompt after this.

The **work** (all in `docs/ops/monitoring.md`):

1. **The parked note** — add a short, clearly-labeled section or bullet
   (e.g. a "## Known parked (this lane)" heading, or a bold **Parked:**
   line under the existing review/watch bullets) recording E22: the
   escalation **outcome** channel (reporter hears back on an escalated
   issue) is not built in this lane by design — the forward/accept
   mechanism ships (link ADR [0155](../../../adr/0155-issue-submission-escalation.md) and
   ADR [0159](../../../adr/0159-escalation-authorization.md), both repo-relative — those
   *are* Markdown links), but the return path is a cross-instance protocol
   change (D9 of the plan) and is out of scope. ~3–4 sentences.
2. **The provisioning pointer** — in that same note or a neighbouring
   line, state that **provisioning automation** is owned by the
   deployment-manager lane and point the reader at
   `claude/deployment-manager-plan.md` (**backticked prose path**, not a
   link — see invariant 2 above). One sentence.
3. **Front-matter** — add
   `docs/adr/0155-issue-submission-escalation.md` and
   `docs/adr/0159-escalation-authorization.md` to the `related:` list, and
   set `updated: 2026-11-13`.
4. **Doc-check + ux gate** — run
   `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/docs-check.ps1`
   and confirm **your file** adds no new gate failure (a pre-existing
   failure in *another* doc is not yours to fix — note it, don't fix it).
   `python3 tools/ux/ux-measure.py --check` must not regress (it counts
   leads in *views*, not runbooks — but run it to be safe).

**Scope rules:**

- **Docs only.** One file: `docs/ops/monitoring.md`. No `.cs`, no
  `.cshtml`, no `.json`, no test, no ADR.
- **One commit** (the house rule): `docs(ux): record the escalation outcome as parked + provisioning pointer (IMPROVE-UX O06)`.
- **Register:** precise operator/GlobalAdmin prose.
- **Link, don't backtick — with one named exception.** ADR 0155 / 0159
  (repo-relative doc paths) are Markdown links; `claude/deployment-manager-plan.md`
  (cross-repo) stays a backticked prose path. Do not create a second home
  for the "not in this lane" list — if one already exists in another
  operator runbook, link to it rather than restating.
- **Do not** build the escalation outcome, and do not file it as a defect
  — it is parked by design. Do not open X02.

**Before committing:** `python3 tools/ux/ux-measure.py --check` (must not
regress) + `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File
tools/docs/docs-check.ps1` (confirm your file adds no new failure) + read
`docs/ops/monitoring.md` top-to-bottom once in the operator register to
confirm the parked note is honest and the provisioning pointer is a
backticked path, not a broken link.

Then report: the before → after for the note, the `docs-check.ps1` result
(and note any pre-existing failures in *other* docs you left alone), the
ux-gate result, the commit hash, and a one-line summary ("The operator
runbook now records the escalation outcome as parked-by-design (cross-
instance protocol change, D9) and points provisioning at the
deployment-manager plan — without a broken cross-repo link"). Do not
commit unrelated work.
