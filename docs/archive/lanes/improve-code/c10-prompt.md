---
id: c10-prompt
title: "C10 — Long Core methods along real seams"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C10 — Long Core methods along real seams

## What you are doing

You are the **eleventh** agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). Your unit is **C10**, the eleventh of 14. C09 (shipped, `c1e722a6`+) moved domain work out of the controllers; C10 is the same discipline one level deeper — **inside the Core services**, where the C00 ratchet now lists the genuinely long methods. Four targets, all named in the plan:

1. **`AddKumunitaCore` (754 lines, `DependencyInjection.cs`)** → one `Add<Module>()` extension **in each module folder** (module self-registration: the module owns its wiring, and `AddKumunitaCore` composes them).
2. **`IdentityService.DeleteCoreAsync` (~288 lines)** → an **`IUserDataEraser` per module**, run in a fixed order. Each module owns how its data is erased. **An integration test proves full erasure across modules** (there is already `tests/Kumunita.Core.Tests/IdentityServiceDeleteLaneGdprTests.cs` — extend it, don't replace it).
3. **Portability** `ResolveAsync` (251) / `ClassifyAsync` / `ExportAsync` (156) / `Validate.Run` → a **per-entity-kind strategy table**. Unit tests per strategy; **the first tests ever for `PortabilityApplyDocuments` and `PortabilityValidate`**.
4. **`NotificationService.EmitAsync`, `PageService.CreateAsync`, `TodoService.UpdateTodoAsync`** (the plan says "after C08: `TodoService`" — C08 shipped, so it is `TodoService` now) → **extract the named steps they already comment as phases.**

**The goal in one sentence:** no Core method over **80 code lines** or **CC 15** except baselined entries that carry a reason — and the splits follow seams that already exist (phase comments, per-entity kinds, module folders), not invented structure.

**You are NOT** doing C00–C09 (all shipped) or C11–C13. No new feature, no route or rendered-HTML change, no document-schema change, **no new ADR** (ADR ceiling is 0289 — don't raise it). You do NOT clean Razor/TS/upload dupes (C11), do NOT gate the seam-test rule (C12), and do NOT do the lane close (C13).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at `c1e722a6` (C09 just landed).
- `DependencyInjection.cs` is **754 lines**; `IdentityService.cs` 1 838; `UserPortabilityService.cs` 1 669. The ratchet already baselines their over-ceiling methods at their *current* values — the ratchet is **shrink-only**: after you split, a method that falls under a ceiling just loses its entry (`--tighten` drops it); a *new* over-ceiling part gets a fresh entry at its current value.
- The **ratchet + part-vs-whole + comment rules** are in `AGENTS.md` → `docs/philosophy/in-code.md` — the split must be *part-vs-whole-legal* (each new part: own callers, own test, own aggregate or module concern) and every new/changed comment follows the rule (what/when/throws + ADR number where one applies).
- Commit style: `feat(improve-code): C10 — <short description>` (conventional commits, em dash, no ADR in subject).
- Gates to keep green after **every** commit: `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/code/code-check.ps1` → `code-check: OK (0 violations)` and `... tools/docs/improve-check.ps1` → `improve-check: ALL GATES PASS`.

## Entry reads

- The plan's **C10** section (verbatim Do/Exit above), and the **part-vs-whole rule** (`docs/philosophy/in-code.md`).
- `src/Kumunita.Core/DependencyInjection.cs` — read the whole file first; note which registrations belong to which module folder, and any cross-module ordering dependencies (those stay in the composing method).
- `IdentityService.DeleteCoreAsync` and its three call sites — map which **modules**' data each phase touches; that map *is* the fixed erasure order.
- `Portability/UserPortabilityService.cs` (`ResolveAsync` / `ClassifyAsync` / `ExportAsync`) + `PortabilityApplyDocuments` + `PortabilityValidate` — the entity kinds are the strategy-table keys.
- `NotificationService.EmitAsync`, `PageService.CreateAsync`, `TodoService.UpdateTodoAsync` — **read the phase comments first**; you are extracting what is already named, not redesigning.
- The ratchet baseline entries for the methods above in `tools/code/code-baseline.json` (they carry the current `max`/`cc_max` you are about to shrink).

## Do

**One of the four targets (or one tight cluster of one target) per commit**, each a full unit (split + re-pointed call sites + tests + baseline) and green:

1. **`AddKumunitaCore`** → per-module `Add<Module>()` extensions. Cross-module ordering stays in the composer. *Note:* this can land before or after the eraser — the eraser is the riskiest, so if the eraser's module seams make the registrations clearer, do it first.
2. **`DeleteCoreAsync`** → `IUserDataEraser` per module + a fixed-order composer + the **integration erasure test** (full erasure across modules; extend `IdentityServiceDeleteLaneGdprTests.cs`).
3. **Portability** → per-entity-kind strategy table + unit tests per strategy + the first tests for `PortabilityApplyDocuments` and `PortabilityValidate`.
4. **`EmitAsync` / `CreateAsync` / `UpdateTodoAsync`** → extract the commented phases into named steps.

Each commit:
- **Extract along the existing seam** — a phase comment, an entity kind, a module folder. If a target has no real seam, *stop* and re-reason it into the baseline with a specific reason instead of inventing structure.
- **Behaviour is unchanged** — the re-pointed entry points return identical results; the existing tests (Identity/Portability/Page/Projects) keep passing.
- **Tests travel with the part** — every new part gets ≥ 1 test naming it (the DoD's "every controller is named by at least one xunit test" is the C12 gate, but C10's own exit *requires* the strategy tests + the first Portability tests + the erasure integration test).
- **Baseline (the C10 proof):** after each split, run `dotnet run --project tools/code/CodeMetrics -- --tighten tools/code/code-baseline.json . .tmp/metrics-tighten` — over-ceiling entries that dropped under their ceiling are *removed*; new parts over ceiling get a fresh entry at their current value. Shrink-only; a raise needs an `adr:` reason (you are not allowed to make one).

## Do NOT do

- Do NOT **rename or re-sign** any C07/C08/C09 committed interface method — C10 adds parts and re-points callers; the frozen seams stay (only *add* overloads where a committed form is missing, the C09 idiom).
- Do NOT **change any route, served HTML, or document schema** — a re-pointed service entry returns the identical result.
- Do NOT **invent structure the plan doesn't name** (no new modules, no new ADR, no event-sourcing, no saga) — the four targets and their named seams are the whole scope.
- Do NOT clean **Razor views, `rich-editor.ts`, upload clones, the `IcsWriter`↔`TodoIcsWriter` clone, or the picker-stub trio** — that's **C11**.
- Do NOT turn the **seam-test rule** from report-only into a gate — that's **C12**.
- Do NOT **raise any baseline `max`/`cc_max`/`count`** or any boundary edge cap without an `adr:` reason — the ratchet is shrink-only; a raise without a reason fails CI.
- Do NOT touch `tools/docs/improve-check.ps1` or the docs gates.

## Exit criteria (all must pass before you finish)

1. **No Core method over 80 code lines or CC 15** except `code-baseline.json` entries that carry a specific reason (no blanket C00 generics); the `AddKumunitaCore` / `DeleteCoreAsync` / portability / phase-method entries are removed or re-reasoned at their new (lower) values.
2. **`AddKumunitaCore` composes per-module `Add<Module>()` extensions** — each module folder owns its wiring.
3. **`IUserDataEraser` exists per module**, runs in a fixed order, and **the integration erasure test passes** (full erasure across modules).
4. **The portability strategies are unit-tested per strategy**, and `PortabilityApplyDocuments` + `PortabilityValidate` have their first tests.
5. **No route, served HTML, or document schema changed** — existing test suites (Identity, Portability, Page, Projects, the C09 controller tests) still pass.
6. **Both gates green**: `code-check: OK (0 violations)` + `improve-check: ALL GATES PASS`.

## Mechanics (learned by the C09 agent — cost you hours if you ignore them)

- **xunit.v3 in-process runner:** `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.<Class>"` (and the same for `Kumunita.Web.Tests`). **Rebuild the test project before trusting a run** — a stale DLL passes while the build is broken. The summary line is `Total: N, Errors: 0, Failed: 0`.
- **Terminal output can be swallowed** — redirect to `.tmp/*.log` (gitignored) and `tail`/`grep`. `cd` may not persist across loop iterations — use absolute paths.
- **`multi_replace_string_in_file` fails on repeated identical strings** — use `sed -i` for identical in-file repeats.
- **Committed-overload idiom** (when you must add a committed form of a session-based method): if the inner method commits itself, the wrapper only opens `await using var session = _store.LightweightSession();` and delegates — **no second `SaveChangesAsync`**. Verify which way the target commits *before* writing the wrapper.
- **The controller-Marten gate is a raw substring match** — comments and doc-comments count. If you re-point a controller you touched in C09, rephrase any `IDocumentStore`/`IDocumentSession` mention in its comments.
- **Boundary edges are a hard ratchet** — a new `A→B:Symbol` file reference that doesn't exist in `boundary_violations` fails CI. If an extraction lands a type in a different module than its callers, check `tools/code/code-baseline.json → boundary_violations` *before* writing the file (C09 hit this on the EventCalendar builder — it stayed blocked at C09 for exactly this).
- **Long-running things:** `dotnet build` (~10–20 s) and a test-class run (~5–30 s) are fine synchronously; say so when you start one.
