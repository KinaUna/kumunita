---
id: o05-prompt
title: "Prompt: IMPROVE-UX O05 — the runbooks match the shipped behaviour"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/ops/privacy-operations.md
  - docs/ops/first-boot.md
  - docs/adr/0301-self-service-deletion-grace-period.md
  - docs/DOCS-CONVENTIONS.md
lane: improve-ux
updated: 2026-11-12
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **O05** — bring the two operator runbooks
> (`docs/ops/privacy-operations.md`, `docs/ops/first-boot.md`)
> up to date with the shipped platform. Three doc-drift spots
> (E12, DoD #5): the resident's per-account export still reads
> "deferred / operator extracts", account deletion still reads
> "operator hard-deletes" (self-service deletion with a grace period
> shipped in R10–R13, ADR 0301), and first-boot still points the
> operator at the logs for the `SeedAdmin__*` check (the `/admin/status`
> surface shipped in O01/O02). The break-glass `INSERT` is by-design
> operator SQL (E21, accepted) — it just needs to be one clean,
> copy-paste-ready code block.
> **Who is it for:** the next coding agent (fresh context).
> **When to read the rest:** when starting O05 (both O05 deps — R13 and
> O02 — have shipped; the runbooks are the only thing left in the O-lane
> before O06's parked-note tail).

# Fresh-context prompt: IMPROVE-UX O05

You are continuing the Kumunita repository on `development` (HEAD
`593bb02a`, **R22 just closed** — the resident `how-it-works.md` now
matches the shipped behaviour). Behind it: the whole A-lane (A01–A15),
the R-lane (R01–R21, R23–R33), and the O-lane through **O04** have all
shipped. O05 is the **docs-only** runbook tail of the D8 operator
surface: two operator-facing runbooks still describe a world that no
longer exists — export is "deferred", deletion is an operator `INSERT`,
and first-boot is verified by reading logs. All three of those are now
in-app; the runbooks are the last place they aren't. The plan card:

> O05 — Runbooks: `privacy-operations.md` (export + deletion in-app, D3
> grace period), `first-boot.md` (check `/admin/status` instead of logs
> for `SeedAdmin`), break-glass SQL as one checked block; docs-check
> passes. Read first: those docs. Depends: R13, O02. **Done when:** no
> "deferred" / operator-SQL *deletion* text.

The problem it fixes (E12): the runbooks are **reference-tier
runbooks for the platform maintainer and the community GlobalAdmin** —
not the resident plain-English register of R22. They carry three stale
spots: (1) the resident's per-account export says "self-serve in-app
*once the export service ships (deferred)*" and "operator extracts that
user's records to a file" — but the resident's self-serve export is
shipped at `/account/portability` (`UserPortabilityController`);
(2) account deletion says "disable in-app → **operator hard-deletes**
the user's personal data" — but self-service deletion with a cancellable
grace period shipped in R10–R13 (ADR 0301), and the pseudonymization
of audit rows is unchanged; (3) first-boot says "confirm the **log**
shows schema-init ran once" for the `SeedAdmin__*` check — but the
`/admin/status` operator surface (O01/O02) now carries a
`SeedAdmin__*`-still-set config warning, so the operator checks the
page, not the logs.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) — the doctrine (the don't-pause-mid-task
  rule; "link, don't backtick").
- **`docs/DOCS-CONVENTIONS.md`** — the shared doc rules (front-matter,
  abstract, the **linking rule**, the **one-fact-one-place** rule, the
  tier model). O05 is a doc change; these rules apply.
- **`plan-improve-ux.md`** — the **O05 card** + the **E12** evidence row
  + the operator journey ("First boot to healthy") + DoD #5.
- **`docs/ops/privacy-operations.md`** — the file you are editing.
  Read it in full. Note it already carries a correct, *shipped*
  **Full instance export** section (ADR 0299) and a correct
  **break-glass** section — those are **not** stale; do not reword them.
- **`docs/ops/first-boot.md`** — the second file. Read it in full; the
  operator steps (1–6) are the only part in play.
- **`improve-ux-handoff-notes.md`** — skim the **R13 / R14** (self-service
  deletion, ADR 0301) sections and the **O02** (`/admin/status`) section
  for the honest before → after the runbooks should now reflect.
- **`git status`** + the branch (R22's `593bb02a` must be intact; you are
  on `development`).

**The one thing to get right.** O05 is a **docs-only** unit — no code,
no test, no migration. These are **operator runbooks**, so the register
is precise technical ops prose for a maintainer/GlobalAdmin — *not* the
plain-English resident register of R22. Do not dumb them down; keep the
ADR numbers (runbooks may cite ADRs; the "no ADR numbers" rule was the
resident page's). Three invariants are load-bearing:

1. **No "deferred" export text, no operator-SQL *deletion* text survives.**
   The resident's per-account export is shipped in-app
   (`/account/portability`); account deletion is self-service in-app
   with a grace period (ADR 0301). The operator is no longer the party
   who extracts a resident's data or hard-deletes a resident's account.
2. **Keep the two things that are genuinely operator acts.** The **Full
   instance export** (ADR 0299, whole-neighborhood / legal-hold) and the
   **break-glass elevation** (`AdminOverride`) are by-design operator-at
   the-DB (E21, accepted). Do not remove or soften either — only make
   the break-glass `INSERT` one clean, copy-paste-ready code block.
3. **One fact, one place.** The resident's per-account export and the
   full-instance export are *different* (the doc already warns "do not
   conflate them") — point the resident row at the resident surface and
   leave the full-export section pointing at ADR 0299. Don't create a
   second home for the same fact.

**⚠️ Do NOT touch any `.cs`, `.cshtml`, `.json`, or test file.** O05 is
`docs/ops/privacy-operations.md` + `docs/ops/first-boot.md` only. The DoD
line is "no 'deferred' / operator-SQL deletion text" + "docs-check
passes". If you find a genuinely-stale *third* runbook fact, fix it in
the same two files if it is in the same section; do not open a new lane —
O06 (the E22 parked-note) is the *next* unit and is a separate commit.

The **work** (all in the two runbooks):

1. **Resident export** — in `privacy-operations.md`, find the bullet:
   > **Resident requests data export** → self-serve in-app once the export service ships (deferred). **Until then (manual):** operator extracts that user's records to a file and delivers it securely; log the action.
   Rewrite it to the shipped reality: the resident self-serves their own
   export in-app at `/account/portability` — the operator's role is only
   to point them at it (and, if there's a dispute, pull the
   `portability.export` audit row). Keep it to ~2 sentences. Do **not**
   touch the **Full instance export** section below (that is ADR 0299's
   and is correct).

2. **Account deletion** — in `privacy-operations.md`, find the bullet:
   > **Resident requests account deletion** → 1. Disable the account in-app immediately. 2. **Operator hard-deletes** the user's personal data … 3. **Audit retention nuance:** … pseudonymize …
   Rewrite it to the shipped reality: the resident schedules their own
   deletion from their account settings; it executes after a grace
   period (they cancel by signing back in); a child under a guardian
   cannot. The **pseudonymization** of `AccessAudit`/`Report` rows is
   unchanged — keep that nuance. Link [ADR 0301](../../../adr/0301-self-service-deletion-grace-period.md)
   (relative to this file) in the bullet and **add `docs/adr/0301-…md` to
   the front-matter `related` list**. Remove the "operator hard-deletes"
   instruction entirely. Keep the ADR 0142 link (the design record).

3. **Break-glass SQL as one checked block** — in `privacy-operations.md`,
   the break-glass step 2 currently inlines the `INSERT INTO
   mt."AdminOverride" …` inside prose. Extract it into a single fenced
   ```sql code block (copy-paste-ready) and keep the surrounding numbered
   steps pointing at it. This is a *presentation* change only — the SQL
   and its by-design operator-only status (E21) stay exactly as they are.

4. **First-boot `SeedAdmin` check** — in `first-boot.md`, the operator
   currently verifies by reading logs (step 5: "confirm the log shows
   schema-init ran once"). Add/revise the step so that, after removing
   `SeedAdmin__*` from env, the operator opens **`/admin/status`** (the
   D8 operator surface, O01/O02) and confirms the `SeedAdmin__*`-still-set
   config warning is **gone** — rather than reading logs. Keep the rest of
   steps 1–6 (the token discipline, the 2FA-adjacent bar, the `/health`
   check) unchanged.

5. **Doc-check + ux gate** — run
   `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/docs-check.ps1`
   and confirm **your two files** add no new gate failures (a pre-existing
   failure in *another* doc is not yours to fix here — note it, don't fix
   it). The front-matter `updated` date on both runbooks should be today
   (`2026-11-12`).

**Scope rules:**

- **Docs only.** Two files: `docs/ops/privacy-operations.md` and
  `docs/ops/first-boot.md`. No `.cs`, no `.cshtml`, no `.json`, no test,
  no ADR (ADR 0301 already exists — link it, don't write one).
- **One commit** (the house rule): `docs(ux): runbooks match the shipped
  behaviour — resident export / self-service deletion / SeedAdmin status
  (IMPROVE-UX O05)`.
- **Register:** precise operator/GlobalAdmin prose. The break-glass and
  full-instance-export sections stay technical and authoritative.
- **Link, don't backtick.** ADR 0301 (a named doc path) becomes a
  relative Markdown link in the body *and* an entry in `related`; the
  `/admin/status`, `/account/portability`, `/admin/break-glass` product
  URLs stay in backticks (they are app routes, not doc paths).
- **The ux gate** (`tools/ux/ux-measure.py --check`) must not regress
  (it counts leads in *views*, not runbooks — but run it to be safe).
- **Do not** open or do O06 (the E22 parked-note) in this commit — one
  unit per commit; O06 is the next prompt.
- **Do not** reword the already-correct **Full instance export** (ADR
  0299) or **full break-glass** *semantics* — only the resident export
  row, the deletion row, the break-glass `INSERT` presentation, and the
  first-boot `SeedAdmin` check.

**Before committing:** `python3 tools/ux/ux-measure.py --check` (must not
regress) + `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File
tools/docs/docs-check.ps1` (confirm your two files add no new failure) +
read both edited files top-to-bottom once, in the operator register, to
confirm no "deferred" / operator-hard-delete *deletion* text survives, the
break-glass `INSERT` is one clean block, and the first-boot step points at
`/admin/status`.

Then report: the before → after for each of the four edits, the
`docs-check.ps1` result (and note any pre-existing failures in *other*
docs you left alone), the ux-gate result, the commit hash, and a
one-line summary ("The operator runbooks no longer say export is
'deferred' or that deletion is an operator `INSERT`; the resident
self-serves both, and first-boot's `SeedAdmin` check reads
`/admin/status` instead of the logs"). Do not commit unrelated work.
