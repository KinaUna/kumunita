---
id: r02-prompt
title: "Prompt: IMPROVE-UX R02 status-code re-execute wiring"
tier: living
kind: plan
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r01-prompt.md
  - docs/dev/testing-and-ci.md
  - docs/dev/razor-verification.md
lane: improve-ux
updated: 2026-11-09
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX R02. **Who is it for:** the next coding agent. **When to read the rest:** when starting R02 after R01.

# Fresh-context prompt: IMPROVE-UX R02

You are continuing the Kumunita repository on `development`. Implement **IMPROVE-UX unit R02 only**: wire `UseStatusCodePagesWithReExecute` so bare HTML status returns land on the R01 status page. Do not start R03 or any later unit, and do not re-open R01's status page or its `status.*` keys.

Before editing, read [AGENTS.md](../../../AGENTS.md), [testing-and-ci.md](../../dev/testing-and-ci.md), [razor-verification.md](../../dev/razor-verification.md), and the lane rules plus the R02 card in [plan-improve-ux.md](plan-improve-ux.md). Check `git status` and the current branch; preserve all existing work. R01 already landed (`StatusController` + `Views/Status/Index.cshtml`, `status.*` keys) — build on it, don't duplicate it.

R02 scope:

- Add `UseStatusCodePagesWithReExecute("/status/{0}")` in the pipeline (R02 depends on R01 being present).
- Gate it to **HTML GET page requests only**. Skip the re-execute when `Accept` lacks `text/html`, when `X-Requested-With` is present (fetch/XHR), and for these paths: `/health`, `*.ics`, `/media/`, `/kiosk/slide`, `/settings/kiosk/items`, and `/api/`. Those keep their bare status responses and unchanged bodies.
- Add integration tests pinning both sides: an HTML 404 re-executes to the status page (body contains the shared nav and the 404 heading); a fetch/`X-Requested-With` 404 body is unchanged; and the numeric status code is unchanged in both cases.
- Do **not** change any controller's `NotFound()`/`Forbid()` returns, and do not add the "deleted on {date}" variant — that stays out of this lane.

Start with [`Program.cs`](../../../src/Kumunita.Web/Program.cs) around the pipeline (see the `UseStatusCodePages` region and `MapControllerRoute`), the R01 `StatusController` for the re-execution target, and one existing integration test as a template for the HTML/fetch pair. Keep this to one seam and no more than about five files. Follow the repository's existing pipeline, testing, and Razor patterns.

Run the narrowest relevant tests/checks, then report changed files, verification results, the commit hash, and any held questions. Make one commit for R02; do not commit unrelated work.
