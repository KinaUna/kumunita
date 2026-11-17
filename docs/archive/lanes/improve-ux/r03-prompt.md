---
id: r03-prompt
title: "Prompt: IMPROVE-UX R03 report resolution fields"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r02-prompt.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-09
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R03. **Who is it for:** the next coding agent. **When to read the rest:**
> when starting R03 (the lane's next Core unit after R02).

# Fresh-context prompt: IMPROVE-UX R03

You are continuing the Kumunita repository on `development`. Implement
**IMPROVE-UX unit R03 only**: the `Report` resolution fields and the `Outcome`
seam on `ResolveReportAsync`. R01 (the status page) and R02 (the status-page
re-execute gate) have already landed — build on them, don't re-open them.
Do not start R04 or any later unit, and do not add the web resolve form or any
controller/view change (that is R04).

Before editing, read [AGENTS.md](../../../../AGENTS.md),
[testing-and-ci.md](../../../dev/testing-and-ci.md), and the lane rules plus the
R03 card in [plan-improve-ux.md](plan-improve-ux.md). Check `git status` and
the current branch; preserve all existing work. Note R03 is **Core-side**
(`Kumunita.Core` Moderation context) — there is no Razor view in this unit.

R03 scope:

- Add a resolution shape to the `Report` document: an `Outcome` enum
  (`NoAction` / `Hidden` / `Removed` / `Other`), a `ResolutionNote` (max 500
  chars), and `ResolvedAt` / `ResolvedBy`.
- `ResolveReportAsync` gains the outcome + note parameters and writes them on
  the report. Keep the existing audit row **unchanged** (same shape, same
  action/target) — the unit's pin is that the audit row does not change.
- Unit tests pin the stored outcome + note + resolved-at/by, and that the
  audit row is unchanged.

Read first: [`Report.cs`](../../../../src/Kumunita.Core/Posts/Report.cs),
[`ModerationService.ReportIntake.cs`](../../../../src/Kumunita.Core/Moderation/ModerationService.ReportIntake.cs)
around the resolve path (≈ lines 648–720), and
[`IModerationService.cs`](../../../../src/Kumunita.Core/Moderation/IModerationService.cs)
around the resolve seam (≈ lines 160–175). Confirm the actual Marten / service
APIs in the current `.csproj` versions before writing — do not copy an
older-version pattern from memory. Keep this to one seam and no more than
about five files. Follow the repository's existing Core document +
service + unit-test patterns (xunit.v3, the `dotnet exec … -class` runner).

Do **not**: add the web form (R04), execute any hide/remove action on the
target (A02), emit any notification (R07), or change the `ReportFiled` path.
No new translation keys are needed (this is data, not UI copy).

Run the narrowest relevant tests/checks, then report changed files,
verification results, the commit hash, and any held questions. Make one commit
for R03; do not commit unrelated work.
