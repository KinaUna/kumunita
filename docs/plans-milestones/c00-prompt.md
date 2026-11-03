# C00 — Ratchet gate + baseline (retire IMPROVE a/e/f)

## What you are doing

You are the first agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). Your unit is **C00**, the first of 13. You are building the Roslyn-based code-metrics tool, the baseline JSON, the `code-check` / `code-report` wrappers, the CI job, and the doctrine entries in `AGENTS.md` / `in-code.md` — then retiring the three grandfathering gates (a), (e) and (f) from `tools/docs/improve-check.ps1`.

**You are NOT** doing any of C01–C13. You are NOT splitting services, moving data to resources, writing ADRs, or touching routes, rendered HTML, document schemas, or comment content.

**The tool does not exist yet.** The plan references `tools/code/CodeMetrics/Program.cs` as if it is already on disk — it is not. You are building it from scratch.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- Commit style: `feat(improve-code): C00 — <short description>` (conventional commits, em dash, no ADR in subject since C00 writes no ADR).
- Read `AGENTS.md` and `.github/copilot-instructions.md` first — they are the doctrine you are enforcing.
- Read the full plan: `docs/plans-milestones/plan-improve-code.md`. The sections **D1, D2, D6**, **The gate (what C00 builds)**, and **C00** are your spec. Everything else in the plan is context for later units.
- The existing gate you are replacing: `tools/docs/improve-check.ps1`. Read it fully before editing. It has gates (a)–(g); you remove (a), (e), (f) and keep (b), (c), (d), (g).
- The test harness that references the gate: `tests/Kumunita.Web.Tests/ImproveHarnessTests.cs`.
- CI: `.github/workflows/ci.yml` — add a `code` job alongside the existing `test`, `docs`, `e2e` jobs.

## The measurement tool (`tools/code/CodeMetrics/`)

Create a new .NET console project under `tools/code/CodeMetrics/` (a `.csproj` referencing `Microsoft.CodeAnalysis.CSharp` — **not** the `RoslynDir` fallback path; use the NuGet package). It must produce three output categories, one per CSV file, into a target directory:

### 1. `files.csv` — per-file code metrics

| column | meaning |
|---|---|
| `file` | repo-relative path, forward slashes |
| `code_lines` | lines holding at least one non-trivia token (strip comments, whitespace, `#if` lines) |
| `comment_lines` | lines whose first non-blank char starts a `//` or is inside a `/* … */` block |
| `blank_lines` | the rest |

### 2. `methods.csv` — per-method metrics

| column | meaning |
|---|---|
| `file` | repo-relative path |
| `type` | fully-qualified type name |
| `member` | method name + parameter arities (e.g. `ResolveAsync(int,bool)`) |
| `code_lines` | non-trivia lines inside the method body (exclude the signature line) |
| `cyclomatic` | 1 + count of decision points: `if`, `else if`, `for`, `foreach`, `while`, `do`, `switch` (case labels don't count), `&&`, `||`, `? :` |

### 3. `types.csv` — per-type public surface

| column | meaning |
|---|---|
| `file` | repo-relative path |
| `type` | fully-qualified type name |
| `kind` | `class` or `interface` |
| `public_methods` | count of public instance methods (for interfaces, all methods) |

### 4. `boundaries.csv` — cross-module semantic edges (D6)

A **module** is a top-level folder under `src/Kumunita.Core/` (e.g. `Posts/`, `Events/`, `UserInfo/`, `Localization/`, `Bootstrap/`, `Identity/`, `Authorization/`, `Moderation/`). Web → Core edges are also tracked (module = `Web`).

An **edge** is: source file is in module A, target type is defined in module B, B ≠ A, and the target is referenced in code (not just a `using`). An edge is **allowed** when the target is:
- an `interface`
- a `record`
- an `enum`
- a `static class` whose name ends in `Policy`, `Keys`, or `Kinds`

Anything else is a **violation**. Also flag:
- Any controller (file under `src/Kumunita.Web/Controllers/`) that references `IDocumentStore`, `IDocumentSession`, or `IQuerySession` — unless it is in the `controllers-marten-allowlist` array in the baseline (each entry: `{ "file": "...", "reason": "..." }`).

### 5. `clones.csv` — exact-token clone regions (report-only, not gated in C00)

Use 100-token sliding windows. Normalise: keep identifiers, normalise string/numeric literals to a sentinel, skip trivia. Report regions ≥ 10 lines that appear in ≥ 2 distinct files. Columns: `start_file`, `start_line`, `end_line`, `token_count`, `clone_files` (semicolon-joined list of other files).

### CLI

```
dotnet run --project tools/code/CodeMetrics -- <root> <output-dir>
  # e.g.  dotnet run --project tools/code/CodeMetrics -- . .tmp/metrics
  # produces: .tmp/metrics/files.csv, methods.csv, types.csv, boundaries.csv, clones.csv

dotnet run --project tools/code/CodeMetrics -- --check <baseline.json> <root>
  # exits 0 if all items pass; exits 1 and prints violations otherwise

dotnet run --project tools/code/CodeMetrics -- --tighten <baseline.json> <root>
  # rewrites baseline.json, removing stale entries (item gone or now under ceiling)
  # and lowering any entry's max to the current measured value
```

The `--check` rule (from the plan):
1. Any **new** item (not in baseline) over its ceiling → fail.
2. Any **baselined** item whose current value exceeds its `max` → fail.
3. Any **stale** entry (item no longer exists, or is now under the ceiling) → fail (this is the "retirement cannot be forgotten" rule; `--tighten` fixes it).
4. Any `max` **higher** than on `origin/main` without an `adr` field → fail (CI diffs the JSON against the remote).

## Ceilings (D2)

| item | ceiling |
|---|---|
| C# file code lines | **600** |
| Method code lines | **80** |
| Method cyclomatic complexity | **15** |
| Class public methods | **25** |
| Interface methods | **20** |
| Razor view (`.cshtml`) physical lines | **600** |
| TS file code lines | **600** |
| TS named function lines | **80** |
| TS named function CC | **15** |

## Baseline file (`tools/code/code-baseline.json`)

Generate it from the current `development` tree. Schema:

```json
{
  "version": 1,
  "generated": "2026-11-03T00:00:00Z",
  "generated_from": "<commit-sha>",
  "ceilings": {
    "cs_file": 600,
    "method_lines": 80,
    "method_cc": 15,
    "class_public_methods": 25,
    "interface_methods": 20,
    "cshtml_lines": 600,
    "ts_file": 600,
    "ts_function_lines": 80,
    "ts_function_cc": 15
  },
  "files":   [ { "id": "src/…/KnownTranslationKeys.cs", "max": 12963 } ],
  "methods": [ { "id": "src/…/SampleDataSeeder.cs::SampleDataSeeder.BackfillSampleCorpusAsync()", "max": 893, "cc_max": 138 } ],
  "classes": [ { "id": "src/…/UserInfoService.cs::UserInfoService", "max": 86 } ],
  "interfaces": [ { "id": "src/…/IUserInfoService.cs::IUserInfoService", "max": 86 } ],
  "views":   [ { "id": "src/…/Views/Projects/TodoDetail.cshtml", "max": 1154 } ],
  "ts":      [ { "id": "src/…/client/lib/rich-editor.ts", "max": 1634 } ],
  "ts_functions": [ { "id": "src/…/client/lib/rich-editor.ts::bindRichEditor", "max": 828 } ],
  "boundary_violations": [ { "from": "Posts", "to": "Notifications", "symbol": "NotificationService", "count": 26 } ],
  "controllers_marten_allowlist": [ { "file": "…", "reason": "…" } ],
  "seam_test": { "mode": "report" }
}
```

Every entry that exists in the baseline **must** have a `max` equal to the current measured value (so the gate is green on day one). The expected starting counts from the plan: ~22 files, ~99 methods, ~9 classes, ~6 interfaces, ~9 views, 1 TS file + ~9 TS functions, boundary edges for `NotificationService` + others, ~21 Marten controllers. **The actual numbers come from running the tool — do not hardcode the plan's estimates; use the tool's real output.**

## PowerShell wrappers

### `tools/code/code-check.ps1`
- Exit 0 / 1.
- Runs: `dotnet run --project tools/code/CodeMetrics -- --check tools/code/code-baseline.json .`
- Prints the tool's output, exits with its code.
- Self-contained (no here-strings, no inter-command `$variables` leaking to a second process), walks up to the repo root like `improve-check.ps1` does.

### `tools/code/code-report.ps1`
- Runs the tool in measure mode (no `--check`).
- Prints top-N red / amber / green for each category.
- Prints the comment-ratio summary (files where `comment_lines > code_lines`, count and total).
- Prints the clone count and the seam-test counts (report-only in C00).
- No exit code gate — informational.

## Edits to existing files

### `tools/docs/improve-check.ps1`
- **Remove** the entire Gate (a) section (the `$aCeiling`, `$aBaseline`, `$aBaselineSizes`, the loop, the `Check "a"` call, and the entire re-baseline comment block).
- **Remove** the entire Gate (e) section (the `$eCeiling`, `$eBaselineSizes`, the loop, the `Check "e"` call).
- **Remove** the entire Gate (f) section (the `$fCeiling`, `$fBaselineSizes`, the loop, the `Check "f"` call).
- **Keep** Gates (b), (c), (d), (g) exactly as they are.
- Update the header comment to remove (a), (e), (f) from the gate list and add a one-line note: "Code-metrics gates (a), (e), (f) retired in IMPROVE-CODE C00 (2026-11-03) — see `tools/code/code-check.ps1`."
- The `Check` function, `Find-RepoRoot`, `Rel`, `Lines`, `FmEnd`, `HasAbstract`, `Get-SharedH2` helpers all stay.

### `tests/Kumunita.Web.Tests/ImproveHarnessTests.cs`
- Add a test (or extend an existing one) that runs `code-check.ps1` and asserts exit 0. Follow the pattern of the existing harness tests in that file.

### `.github/workflows/ci.yml`
- Add a `code` job (parallel to `test`, `docs`, `e2e`):
  - `dotnet run --project tools/code/CodeMetrics -- --check tools/code/code-baseline.json .`
  - `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/code/code-check.ps1`
  - **Blocking from day one** (no `continue-on-error`).

### `AGENTS.md`
- Add a short paragraph under an existing or new section (e.g. "## Code-metrics gate" or append to the conventions section) covering:
  1. **Ratchet rule** — a baseline number only goes down; raising it needs an `adr:` field in the JSON entry; CI fails on any raise without one.
  2. **Part-vs-whole rule** — a new type must satisfy at least one of: (a) ≥ 2 callers, (b) its own unit test, (c) owns a route family or aggregate. Failing all three = ceremony, fold it back.
  3. **D4 comment rule** — a doc-comment states what, when, throws, plus the ADR number; history stays in the ADR or handoff note; the comment ratio is reported, not gated.
- Each rule stated **once**, linked to the plan. Do not restate the plan's prose.

### `docs/philosophy/in-code.md`
- Add or extend a section that mirrors the three rules above (or links to `AGENTS.md` for them — pick one canonical home, the other links).

## Do NOT do

- Do NOT write any ADR (that is C01).
- Do NOT split any service, controller, or interface (that is C01–C08).
- Do NOT move translation keys, guide pages, or the sample corpus to embedded resources (that is C02).
- Do NOT extract `*Policy` classes (that is C04).
- Do NOT create `ContentTargetResolver` (that is C05).
- Do NOT create `TranslationLane` (that is C06).
- Do NOT change any route, rendered HTML, document schema, or public API surface.
- Do NOT sweep comments (D4 is applied on-touch by later units, not by C00).
- Do NOT add a new NuGet package to any `src/` project. The Roslyn package goes only into `tools/code/CodeMetrics/`.

## Exit criteria (all must pass before you commit)

1. `dotnet build` — clean.
2. `dotnet test` — all existing tests pass (no test changes beyond the new harness test).
3. `pwsh tools/code/code-check.ps1` — exit 0 on the current tree.
4. **Ratchet proof:** deliberately add one code line to a baselined method (e.g. append a no-op `// x` won't work since it's a comment — add `_ = 0;` to a method body), run `code-check.ps1`, confirm it **fails** with that method named, then revert.
5. **Tighten proof:** deliberately remove a line from a baselined method, run `--tighten`, confirm the entry's `max` drops by 1, then restore the line and the baseline.
6. `improve-check.ps1` — gates (b), (c), (d), (g) still pass; gates (a), (e), (f) are gone.
7. The `code` job in `ci.yml` is syntactically valid YAML (run `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"` or `yq` if available).
8. `AGENTS.md` and `in-code.md` carry the three rules, one canonical home, linked from the other.

## Practical notes

- The Roslyn `Microsoft.CodeAnalysis.CSharp` package: use `SyntaxTree.GetRoot()` to walk. For cyclomatic complexity, count `IfStatement`, `ForStatement`, `ForEachStatement`, `WhileStatement`, `DoWhileStatement`, `BinaryExpression` with `AndAlso`/`OrElse`, `ConditionalExpression`. `switch` case labels do not count.
- For "code lines": a line is a code line if it contains at least one token whose `IsTrivia == false`. Use `SyntaxToken.Text` to detect.
- For the boundary pass, you need a **semantic model** (`Compilation`), not just syntax. Build a `CSharpCompilation` from all `src/**/*.cs` files (reference the BCL assemblies via `MetadataReference.CreateFromFile` for `System.Runtime`, `System.Private.CoreLib`, and the other framework assemblies — or use `Microsoft.CodeAnalysis.CSharp.Scripting` / `Microsoft.CodeAnalysis.CSharp.Workspaces` to get references more easily). Alternatively, if a full compilation is too heavy for a console tool, use a **syntax-only** heuristic: scan `using` directives and `typeof()` / `new()` / method-call targets, and resolve the target type's file by searching for its definition in the known module folders. This is less accurate but avoids the compilation step. **Pick the approach that works; document which one you used in the tool's README.**
- The TS metrics (`ts-metrics.js`) are referenced in the plan but do not exist yet. For C00, implement a simple Node.js script `tools/code/ts-metrics.js` that:
  - Walks `src/Kumunita.Web/client/**/*.ts` (skip `node_modules`).
  - Counts physical lines, code lines (non-comment, non-blank), and named-function bodies (a named function = a `function name(...)`, `const name = (...)`, or `function* name(...)` whose body is measured in lines + a rough CC count of `if`/`for`/`while`/`&&`/`||`/`? :`).
  - Outputs `ts.csv` with the same schema as the C# files/methods output.
  - The plan's `NODE_PATH` invocation is a hint for running it with the project's `typescript` package; a plain regex/line-based approach is fine for C00 (the goal is the baseline, not a full parse).
- `.cshtml` views: count physical lines (the plan uses physical lines for views, not code lines — the Razor comment syntax is different). Use `wc -l` equivalent.
- The `Kumunita.slnx` is an XML-based solution file (not `.sln`). The `dotnet build` / `dotnet test` commands work with it. The `CodeMetrics` project does NOT need to be added to the solution file — it is a standalone tool under `tools/`.
- PowerShell: follow the existing `improve-check.ps1` conventions — no here-strings, single logical lines where practical, `Set-StrictMode -Version Latest`, `$ErrorActionPreference = "Stop"`.
- The `global.json` pins the .NET SDK version — check it before creating the `CodeMetrics` csproj to make sure the `TargetFramework` matches.
- When you are done, commit with: `feat(improve-code): C00 — ratchet gate, baseline, retire gates a/e/f, CI code job, doctrine entries`.
