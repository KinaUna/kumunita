# C11 — Web: views, TypeScript and the small duplicates

## What you are doing

You are the **thirteenth** agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). C10 (shipped, `71f50a24`+) finished the Core-method splits; C11 is the Web + TS surface — the plan's named targets are **five**, all in the C11 section:

1. **Razor views** — apply the U06 criterion (> 150 lines **and** repeated or independently touched) to `TodoDetail`, `BoardDetail`, `_Layout`, `Groups/Detail`, `Groups/PostDetail` and `Guardian/Detail`. Extract the blocks that qualify; **record the blocks that were NOT extracted, and why** (one short comment per block). Served HTML is byte-identical (the Razor doctrine in `docs/dev/razor-verification.md`).
2. **`rich-editor.ts`** (1 634 lines) — split `bindRichEditor` (an **828-line closure**) into ES modules (toolbar, commands, paste/markdown bridge, selection state). Plain `tsc`, no bundler (ADR 0031). The existing `ts:build` task (`npm run build` in `src/Kumunita.Web`) must pass.
3. **Composer picker trio** — delete the 9 forwarding stubs (the U05 residue) and call `ComposerSeedOptions` directly; migrate the Profile / Groups private copies to the shared helper.
4. **One `UploadHandler`** — replace the copied upload blocks in `ContentImageController` / `AttachmentController` / `DocumentController` with a shared handler. The three controllers' public surface (routes, audit rows, `TempData`) is unchanged.
5. **One calendar serializer** — replace the `IcsWriter` / `TodoIcsWriter` clone with a single shared implementation. Output bytes are byte-identical.

**The goal in one sentence:** view + TS lists each at ≤ 3 baselined entries, the picker trio is defined once with no stubs, and ≈ 500 clone lines are gone — all along seams that already exist, no new ADR (ceiling 0289), no route or rendered-HTML change.

**You are NOT** doing C00–C10 (all shipped) or C12–C13. No new feature, no route or served-HTML change (extracted partials must render identically), no document-schema change, **no new ADR**.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at `71f50a24` (C10-3b just landed).
- `tools/code/code-baseline.json` carries the current baselines for the over-ceiling views, TS functions, and any method over ceiling. The ratchet is **shrink-only**: after a split, `--tighten` drops entries that fell under ceiling; a new over-ceiling part gets a fresh entry at its current value.
- **Razor verification doctrine** (`docs/dev/razor-verification.md`) and **Razor partials + extension-published** quirks are in repo memory — read them before touching any `.cshtml`.
- The **ratchet + part-vs-whole + comment rules** are in `AGENTS.md` → `docs/philosophy/in-code.md`.
- Commit style: `feat(improve-code): C11 — <short description>` (em dash, no ADR in subject).
- Gates to keep green after **every** commit: `pwsh tools/code/code-check.ps1` → `OK (0 violations)` and `pwsh tools/docs/improve-check.ps1` → `ALL GATES PASS`.

## Entry reads

- The plan's **C11** section (the Do/Exit above), and the **part-vs-whole rule** (`docs/philosophy/in-code.md`).
- `docs/dev/razor-verification.md` — the known Razor traps in this codebase (the `@media` block-count pin, partial-extension publishing).
- `src/Kumunita.Web/Views/Projects/TodoDetail.cshtml` (1 154) and `BoardDetail.cshtml` (1 090) — read the full view; note which blocks are repeated or independently touched (the U06 criterion).
- `src/Kumunita.Web/client/rich-editor.ts` — the `bindRichEditor` closure and its sub-concerns (toolbar, commands, paste, selection).
- `src/Kumunita.Web/Controllers/ContentImageController.cs`, `AttachmentController.cs`, `DocumentController.cs` — the three upload blocks; identify the shared shape.
- `src/Kumunita.Web/Controllers/IcsWriter.cs` and `TodoIcsWriter.cs` — the two calendar serializers; note the shared vs. divergent parts.
- `ComposerSeedOptions.cs` + the 9 forwarding stubs + the Profile/Groups private copies (find them with `grep -rn "ComposerSeedOptions\|picker" src/Kumunita.Web/Controllers/`).
- The ratchet baseline entries for the views, TS file, and TS functions in `tools/code/code-baseline.json`.

## Do

**One of the five targets per commit** (or one tight cluster), each green (build + tests + both gates):

1. **Views** — extract the qualifying blocks into partials (`.cshtml` under `Views/Shared/` or the feature folder). Record each block you did NOT extract, and why (one `// not extracted: <reason>` comment at the extraction site). Re-run the affected page's test (or a manual `curl` against the live server per `docs/dev/razor-verification.md`) to confirm byte-identical HTML.
2. **`rich-editor.ts`** — split into ES modules under `src/Kumunita.Web/client/`. `ts:build` (`npm run build`) must pass. The existing `ts:watch` task is for dev only.
3. **Composer stubs** — delete the 9 forwarding stubs; migrate Profile/Groups copies to `ComposerSeedOptions`. Existing controller tests must keep passing.
4. **`UploadHandler`** — one shared handler; the three controllers call it. Audit rows, `TempData`, and route attributes are unchanged.
5. **Calendar serializer** — one shared implementation; `IcsWriter` and `TodoIcsWriter` become thin wrappers or are folded into one type. Output bytes are identical (pin with a test).

Each commit:
- **Extract along the existing seam** — a repeated block, a phase comment, a shared shape. If a target has no real seam, *stop* and re-reason it into the baseline with a specific reason instead of inventing structure.
- **Behaviour is unchanged** — served HTML, uploaded-file shape, ICS output, and picker state are identical.
- **Tests travel with the part** — every new type gets ≥ 1 test naming it (the part-vs-whole rule).
- **Baseline:** after each split, run `dotnet run --project tools/code/CodeMetrics -- --tighten tools/code/code-baseline.json . .tmp/metrics-tighten` — entries under ceiling are dropped; new parts over ceiling get a fresh entry. Shrink-only; a raise needs an `adr:` reason (you have none).

## Do NOT do

- Do NOT **change any route, served HTML, or document schema** — a re-pointed partial or handler returns identical output.
- Do NOT **invent structure the plan doesn't name** (no new modules, no new ADR, no bundler, no SPA).
- Do NOT touch **Core services, the seam-test rule, or the lane close** — those are C12/C13.
- Do NOT **raise any baseline `max`/`cc_max`/`count`** or any boundary edge cap without an `adr:` reason.
- Do NOT touch `tools/docs/improve-check.ps1` or the docs gates.

## Exit criteria (all must pass before you finish)

1. **View and TS baseline lists are at ≤ 3 entries each** (the `code-baseline.json` `views` and `ts` arrays, after `--tighten`).
2. **The picker trio is defined once** — zero forwarding stubs, Profile/Groups use `ComposerSeedOptions` directly.
3. **One `UploadHandler`** serves all three upload controllers; the ICS output is byte-identical (pinned by a test).
4. **No route, served HTML, or document schema changed** — existing Web test suites still pass.
5. **Both gates green**: `code-check: OK (0 violations)` + `improve-check: ALL GATES PASS`.

## Mechanics (learned by prior agents — cost hours if you ignore them)

- **xunit.v3 in-process runner:** `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll -class "Kumunita.Web.Tests.<Class>"`. **Rebuild the test project before trusting a run.**
- **Razor verification:** the rendered HTML is the evidence. Get a live server (`docker compose up` or `dotnet run --project src/Kumunita.Web`) and `curl` the page, or use the browser tools. The `@media` block-count pin is in `docs/dev/testing-and-ci.md` — do not break it.
- **TS build:** `cd src/Kumunita.Web && npm run build` (the `ts:build` task). The `ts:watch` task is for dev; use `ts:build` to verify.
- **Terminal output can be swallowed** — redirect to `.tmp/*.log` and `tail`/`grep`.
- **`multi_replace_string_in_file` fails on repeated identical strings** — use `sed -i` for identical in-file repeats.
- **Boundary edges are a hard ratchet** — check `tools/code/code-baseline.json → boundary_violations` before moving a type into a different module.
- **Razor partials must be published** (the `razor-partial-extension-published` quirk in repo memory) — a partial under `Views/Shared/` is picked up automatically; a partial under a feature folder must match the route area.
- **Long-running things:** `dotnet build` (~10–20 s), `npm run build` (~5–10 s), and a test-class run (~5–30 s) are fine synchronously; say so when you start one.
