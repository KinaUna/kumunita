# C10-3b — Portability: ResolveAsync / ClassifyAsync / ExportAsync strategy table

## What you are doing

You are the **twelfth** agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). C10 has four targets; three are shipped (DI self-registration, `DeleteCoreAsync` eraser, `EmitAsync`/`CreateAsync`/`UpdateTodoAsync` phase extraction). **Your unit is the last one: the Portability per-entity-kind strategy table.**

Three methods in `UserPortabilityService.cs` are still over the 80-line / CC-15 ceiling:

| Method | Lines | CC | Baseline entry |
|--------|-------|----|----------------|
| `ResolveAsync` | 251 | 70 | `UserPortabilityService.ResolveAsync(...)` max 251 |
| `ClassifyAsync` | 148 | 47 | `UserPortabilityService.ClassifyAsync(...)` max 148 |
| `ExportAsync` | 156 | 28 | `UserPortabilityService.ExportAsync(...)` max 156 |

**Goal:** split each into a per-entity-kind strategy table (one strategy per entity kind — the `UserPortabilityEntityStatus` enum + the entity-kind switch arms that already exist in the code). Unit-test each strategy. After the split, run `--tighten` to drop the now-under-ceiling entries.

**You are NOT** doing C11+ (views/TS/duplicates), C12 (seam-test gate), or C13 (lane close). No new ADR (ceiling is 0289). No route, served-HTML, or document-schema change.

## Starting state

- Branch `development`, clean tree at `f52776de` (C10 DI self-registration just landed).
- `UserPortabilityService.cs` is **1 669 lines** (baselined). The three target methods are in it.
- `PortabilityValidateTests.cs` + `PortabilityApplyDocumentsTests.cs` exist (C10-3a, the first-ever tests for those). `UserPortabilityResolveTests.cs`, `UserPortabilityClassifyTests.cs`, `UserPortabilityExportTests.cs` also exist — **extend them, don't replace**.
- The ratchet baseline entries for these three methods are at their current values in `tools/code/code-baseline.json`. Shrink-only: after the split, `--tighten` drops entries that fall under ceiling.

## Entry reads

- `UserPortabilityService.cs` — read `ResolveAsync`, `ClassifyAsync`, `ExportAsync` **and their entity-kind switch/if arms**. The entity kinds *are* the strategy-table keys; you are extracting what is already branched, not redesigning.
- The existing `UserPortabilityResolveTests.cs` / `ClassifyTests.cs` / `ExportTests.cs` — they exercise the monolith; your split must keep them passing.
- `docs/philosophy/in-code.md` — the part-vs-whole rule + the comment rule.
- The ratchet baseline entries for the three methods.

## Do

**One commit** (or two if the split is large enough to warrant ResolveAsync separately from Classify/Export), each green:

1. **Extract the entity-kind arms** from each of the three methods into per-kind strategies (a `record`/`class` per kind, or a strategy-table keyed by the entity-kind value). The existing switch/if branches are the seams — don't invent new kinds.
2. **Re-point the three methods** to iterate the strategy table. Their public signatures are frozen (C08/C09 committed); only the body changes.
3. **Unit-test each strategy** (≥ 1 test per strategy naming it). Extend the existing `UserPortability*Tests.cs` files.
4. **Baseline:** `dotnet run --project tools/code/CodeMetrics -- --tighten tools/code/code-baseline.json . .tmp/metrics-tighten` → the three entries should drop (or shrink to their new values). No raises without an `adr:` reason (you have none).

## Do NOT

- Do NOT change the **public signatures** of `ResolveAsync` / `ClassifyAsync` / `ExportAsync` — only the body.
- Do NOT **rename or re-sign** any C08/C09 committed interface method.
- Do NOT **invent new entity kinds** or new modules.
- Do NOT raise any baseline `max` / `cc_max` / `count` without an `adr:` reason.
- Do NOT touch Razor views, `rich-editor.ts`, upload clones, or the `IcsWriter` clone (that's C11).

## Exit criteria

1. `ResolveAsync`, `ClassifyAsync`, `ExportAsync` each under 80 lines / CC 15, or their baseline entries carry a specific reason.
2. Each per-kind strategy has ≥ 1 unit test.
3. `dotnet build` → 0 errors. Core + Web test suites pass (use the xunit.v3 in-process runner: `dotnet exec tests/<Proj>/bin/Debug/net10.0/<Proj>.dll`).
4. `pwsh tools/code/code-check.ps1` → `OK (0 violations)`.
5. `pwsh tools/docs/improve-check.ps1` → `ALL GATES PASS`.

## Mechanics

- **xunit.v3:** `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.<Class>"` (rebuild the test project first).
- **Terminal output can be swallowed** — redirect to `.tmp/*.log` and `tail`.
- **`multi_replace_string_in_file` fails on repeated identical strings** — use `sed -i` for identical in-file repeats.
- **Boundary edges are a hard ratchet** — check `code-baseline.json → boundary_violations` before moving a type into a different module.
- **Commit style:** `feat(improve-code): C10 — <short description>` (em dash, no ADR in subject).
