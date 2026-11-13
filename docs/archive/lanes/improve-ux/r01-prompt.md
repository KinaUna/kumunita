---
id: r01-prompt
title: "Prompt: IMPROVE-UX R01 status page"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/dev/testing-and-ci.md
  - docs/dev/razor-verification.md
lane: improve-ux
updated: 2026-11-09
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX R01. **Who is it for:** the next coding agent. **When to read the rest:** when starting R01 after X01.

# Fresh-context prompt: IMPROVE-UX R01

You are continuing the Kumunita repository on `development`. Implement **IMPROVE-UX unit R01 only**: the localized status page. Do not start R02 or any later unit.

Before editing, read [AGENTS.md](../../../../AGENTS.md), [testing-and-ci.md](../../../dev/testing-and-ci.md), [razor-verification.md](../../../dev/razor-verification.md), and the lane rules plus R01 card in [plan-improve-ux.md](plan-improve-ux.md). Check `git status` and the current branch; preserve all existing work.

R01 scope:

- Add `StatusController` with `GET /status/{code}` and `Views/Status/Index.cshtml`.
- Provide localized text for 404, 403, 400, 413, 415, 429, and 500, with Back, Home, and Search actions and the normal site navigation.
- Add new localization keys to en/de/fr/da, following the existing conventions.
- Verify the page renders with navigation for each listed status code.

Start with [`Program.cs`](../../../../src/Kumunita.Web/Program.cs) around status handling and [`Error.cshtml`](../../../../src/Kumunita.Web/Views/Shared/Error.cshtml). Keep this to one seam and no more than about five files plus translation JSON. Follow the repository's existing MVC, localization, and Razor patterns. Do **not** wire `UseStatusCodePagesWithReExecute`, change controller status returns, or broaden error handling; that belongs to R02. Do not change UX behavior outside this status page.

Run the narrowest relevant tests/checks, then report changed files, verification results, the commit hash, and any held questions. Make one commit for R01; do not commit unrelated work.
