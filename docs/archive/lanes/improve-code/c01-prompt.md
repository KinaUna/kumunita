---
id: c01-prompt
title: "C01 — ADRs first: the contracts the splits will need"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C01 — ADRs first: the contracts the splits will need

## What you are doing

You are the second agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). Your unit is **C01**, the second of 13. It is a **documentation-only** unit: you write the four ADRs that the later splits (C02–C13) will code against, each decision recorded *before* a line moves.

**You are NOT** doing C00 (already shipped — the gate and baseline exist) and you are NOT doing C02–C13. You write **no C#/TS code, no tests, no splits, no data moves, no resource changes.** The only files you create or edit are the four ADR files and the ADR index. The code-metrics gate is now in place — do not fight it, and do not loosen it.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at `7aeb7bd2`.
- Highest existing ADR is **0285**. The plan's `// TODO ❓` says the four C01 ADRs take the next free ones: **0286, 0287, 0288, 0289** (confirm free at write time with `ls docs/adr/0*.md | sort | tail -1`).
- ADR index: `docs/adr/README.md` (a markdown table; one row per ADR, e.g. `| 0285 | Kiosk playlist: … | Accepted |`).
- ADR shape/conventions: `docs/adr/TEMPLATE.md` + `docs/DOCS-CONVENTIONS.md` (front-matter, abstract, the **250-line cap** per ADR — each C01 ADR must be ≤ 80).
- The doctrine you are codifying: `docs/philosophy/in-code.md` § "The ratchet, the split, the comment" (the part-vs-whole test is the rule for the new types you name).
- **Gate you must keep green:** `tools/docs/improve-check.ps1` **gate (b)** (ADR index drift). Every ADR file you add needs a matching index row in the *same commit* or gate (b) fails. Also run `tools/code/code-check.ps1` before committing — it should be unaffected by docs, but confirm.

## Entry reads

- ADR **0006** (`docs/adr/0006-module-boundary-contracts.md`) — you amend it; read fully.
- The M5 projects ADRs cited in `IProjectService.cs` (grep for `adr:` refs in that file) — the "frozen seams" ADR B must relocate.
- ADR **0015 / 0018** (localization, translations) — the substrate ADR C extends.
- `IUserInfoService.cs`, `IProjectService.cs` (the public surfaces you are redrawing).
- `docs/adr/TEMPLATE.md`, `docs/adr/README.md`.

## The four ADRs (each ≤ 80 lines, status `Accepted`)

- **ADR 0286 (amends 0006) — `IUserInfoService` back to the access core.** The access-core methods return to `IUserInfoService`: profile read, `GetGroupIdsAsync`, `GetActiveGrantAsync`, membership writes, community ids. The rest of the god interface splits into **new sibling interfaces in the UserInfo module**: `IGroupService`, `ICommunityMembershipService`, `IGuardianService`, `IProfileSettingsService`. Name the **method → interface map** and the migration story for callers. Note that **audit rows are unchanged** because the `Via` values do not move.
- **ADR 0287 — the Projects module split.** `IProjectService` (projects + goals), `IBoardService` (boards, lanes, placements), `ITodoService` (todos, comments, claim/assign). Web gets `BoardsController`, `TodosController`, `ProjectsController`, with **routes unchanged**. Name which ADR-cited **"frozen seams" move where**, so the kiosk and other citations keep resolving.
- **ADR 0288 — one shared translation-variant lane.** A single lane (the Core contract plus the Web action helper) that **every** translatable surface uses. It is the contract C06 will build `TranslationLane` against.
- **ADR 0289 — data as embedded resources.** Translation keys, guide pages, and the sample corpus are **embedded resources, not C# literals**. It is the contract C02 will build against.

## Do NOT do

- Do NOT write any C#/TS code, or any test.
- Do NOT split any service, controller, or interface (that is C02–C13).
- Do NOT move any data to resources or delete the U04 views (that is C02).
- Do NOT touch routes, rendered HTML, document schemas, or public API behavior — ADRs only.
- Do NOT modify `tools/code/code-baseline.json`, the ceiling values, or any gate.
- Do NOT renumber existing ADRs or restate the plan's prose — link to it.

## Exit criteria (all must pass before you commit)

1. Four new ADR files exist (`docs/adr/0286-…md` … `0289-…md`), each **≤ 80 lines**, valid front-matter + abstract per `TEMPLATE.md` / `DOCS-CONVENTIONS.md`, status `Accepted`, and ADR 0286 explicitly records that it **amends 0006**.
2. `docs/adr/README.md` has a matching row for all four (same commit).
3. `pwsh tools/docs/improve-check.ps1` — gate **(b)** (and b/c/d/g overall) pass; exit 0.
4. `pwsh tools/code/code-check.ps1` — still exit 0 (docs must not trip the code gate).
5. `dotnet build` and `dotnet test` are unaffected (docs-only change; spot-check build stays clean).
6. Each ADR names its decision(s) concretely (the method→interface map for 0286, the seam relocation for 0287) — not a vague intent. A reviewer can code C02–C08 from these alone.

## Practical notes

- **The ADR index gate (b) is a hard stop.** If you add an ADR file without its index row, `improve-check.ps1` gate (b) fails and so does `ImproveHarnessTests`. Add both in the same commit and run gate (b) yourself before committing.
- Keep each ADR to its decision + consequences + a "related" link to the plan (`docs/plans-milestones/plan-improve-code.md`) and to ADR 0006. Do not restate the plan's prose or the in-code doctrine — link to them.
- ADR 0286 is an **amendment to 0006**, so it should reference 0006 in its front-matter/`related` and say "Amends ADR 0006" explicitly.
- The part-vs-whole test (≥ 2 callers, or its own unit test, or owns a route family/aggregate) is the *reason* each new interface in 0286/0287 is justified — cite it briefly so the split reads as non-ceremony.
- Commit with: `docs(improve-code): C01 — ADRs 0286–0289 (the contracts for the C02–C08 splits)`.
