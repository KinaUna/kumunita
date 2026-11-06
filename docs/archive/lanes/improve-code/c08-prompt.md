---
id: c08-prompt
title: "C08 — Split the Projects module (ADR B)"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C08 — Split the Projects module (ADR B)

## What you are doing

You are the **ninth** agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). Your unit is **C08**, the ninth of 14. It is a **seam-splitting** unit (E4, *The god part*), the Projects twin of C07: `IProjectService` is **58 methods** across three aggregates and `ProjectService.cs` ≈ **2 300 code lines** / `ProjectsController.cs` ≈ **3 190 code lines** — both over the 600-line ceiling, and one controller owns four route families. The **C01 ADR** (ADR B, `docs/adr/0287-projects-module-split.md`, accepted) already decided *what* to do. You implement it.

The split, per ADR 0287: **three interfaces in the Projects module**, each a distinct aggregate, plus **three controllers**:

| Core interface | Owns | Web controller (route family) |
|---|---|---|
| `IProjectService` (shrunk) | projects + goals — CRUD, association, translations | `ProjectsController` (`/projects`, `/projects/goals`) |
| `IBoardService` (new) | boards, lanes, placements — CRUD, placement moves, board↔project/event association, translations | `BoardsController` (new, `/projects/boards`) |
| `ITodoService` (new) | todos, comments, claim/assign — CRUD, subtasks, claim/assign/status, todo↔project/event association, translations | `TodosController` (new, `/projects/todos`) |

The **route attributes move verbatim**; the route table (what `EndpointDataSource` enumerates) is **unchanged** — you prove that with a diff test, not by hand.

**How this differs from C07:** C07 split one access-core interface into siblings while *keeping the interface count low*. C08 also splits `IProjectService` into three, but the Web side is a **controller split** (one god controller holding four route families → three controllers, one per route family) with the **route table pinned by test**. The standing checks already have their C04 policy homes (`ProjectStandingPolicy` / `BoardStandingPolicy` / `TodoStandingPolicy` / `GoalStandingPolicy`) — call **through** them, don't copy them.

**You are NOT** doing C00–C07 (all shipped) or C09–C13. No new feature, **no route or rendered-HTML change**, no document-schema change, **no new ADR** (C08 builds against ADR 0287). You do NOT move controllers off Marten (C09) or split long Core methods (C10).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at `b72125d4` (C07 just landed).
- `IProjectService.cs` ≈ **58 methods** (interface baseline `max: 58`) · `ProjectService.cs` ≈ **2 300 code lines** (baseline `max: 2296`) · `ProjectsController.cs` ≈ **3 190 code lines** (baseline `max: 3190`) · class baselines: `ProjectService` **65** public methods, `ProjectsController` **63**.
- The two new siblings **do not exist yet** (`IBoardService` / `ITodoService`, `BoardService` / `TodoService`, `BoardsController` / `TodosController` — confirm none exist before creating).
- C04 standing policies already exist under `src/Kumunita.Core/Projects/`: `ProjectStandingPolicy`, `BoardStandingPolicy`, `TodoStandingPolicy`, `GoalStandingPolicy`.
- C06 already migrated the 12 Projects translation methods to the shared `TranslationLane` — in `ProjectService` they are now **thin forwarders**. Move those forwarders verbatim into their owning sub-interface; do not re-implement them.
- Commit style: `feat(improve-code): C08 — <short description>` (conventional commits, em dash, no ADR in subject).
- Gates to keep green: `tools/code/code-check.ps1` (the ratchet) and `tools/docs/improve-check.ps1` (docs gates). Both are green now.
- Baseline: `tools/code/code-baseline.json` has the `ProjectService` / `ProjectsController` / `IProjectService` file, class and interface entries you will **retire or shrink**. **Trust the baseline + the code over the ADR's exact numbers** — re-derive the method → interface map from `IProjectService.cs` before touching anything (ADR 0287's cluster shape is the target, but the exact per-method assignment must be verified against the code + the ADR's aggregate descriptions).

## Entry reads

- **ADR B** (`docs/adr/0287-projects-module-split.md`) — the three-aggregate split, the caller sets, the route-family map, and the `IBoardService`-over-ceiling note.
- The plan's **C08** section, **D3** (god interfaces), **D6** (module boundaries), and the **part-vs-whole rule** (`docs/philosophy/in-code.md` § "The ratchet, the split, the comment").
- `IProjectService.cs` — **signatures only** (a large share is doc-comment; re-derive the method → aggregate map).
- `ProjectService.cs` and `ProjectsController.cs` — **one aggregate at a time**, in the commit order below.
- The C04 standing policies (`ProjectStandingPolicy` / `BoardStandingPolicy` / `TodoStandingPolicy` / `GoalStandingPolicy`) — the shared standing checks your three classes call through.
- The Projects DI registration (`src/Kumunita.Core/DependencyInjection.cs`) and the Web registration — where the new services + controllers get registered.
- The existing Projects test files under `tests/Kumunita.Core.Tests/` and `tests/Kumunita.Web.Tests/` — these **move with their aggregate**.
- `tools/code/code-baseline.json` (the `ProjectService` / `ProjectsController` / `IProjectService` / per-method entries you retire).

## Do

**One commit per aggregate**, in this order (each commit is a full unit: interface + moved implementation + Web controller + caller/DI re-point + moved tests + baseline updates, and it is green):

1. **Boards** → `IBoardService` + `BoardService` (Core) + `BoardsController` (Web, `/projects/boards`).
2. **Todos** → `ITodoService` + `TodoService` (Core) + `TodosController` (Web, `/projects/todos`).
3. **Projects + goals** → the **shrunk** `IProjectService` + `ProjectService` (Core) + the **shrunk** `ProjectsController` (Web, `/projects`, `/projects/goals`).

Each commit:
- **Create the sibling interface + implementation.** Move the aggregate's methods **verbatim** (move, not rename — no signature or body change). Keep the C06 translation forwarders intact inside their aggregate. Route **attributes** on the moved controller actions move verbatim too.
- **Shared private helpers** that two or more of the implementations now need → an internal shared seam (part-vs-whole-legal, ≥ 2 callers); standing checks go **through** the C04 policies, not a new copy.
- **Callers inject only what they use:** switch each named caller's DI from `IProjectService` to the specific sibling. Do not leave a caller on the god interface for a method that now lives elsewhere.
- **Route-table pin (the C08 proof):** a test enumerates `EndpointDataSource` and asserts the **full route table** (paths + HTTP verbs, the Projects family) is **unchanged** by the split. Write it before/keep it green across all three commits — this is what "route attributes move verbatim" means here.
- **Move the tests** for that aggregate to a file naming the new type (e.g. a Boards board-lane test → `BoardService...Tests` / `BoardsControllerTests`) — or keep the file but point it at the sibling. The **seam-test rule** requires each public `I*Service` to be named by at least one integration test file.
- **Baseline:** retire the moved methods' old entries (a moved method gets a new id; the old entry goes stale and must be removed) and the `ProjectService` / `ProjectsController` / `IProjectService` file/class/interface entries as they fall under the ceiling. Shrink-only — a sibling method, class or file over a ceiling lands as a *new* entry at its current value. **ADR 0287 notes `IBoardService` (~24) exceeds the interface ceiling (20): baseline it at its current value (with the C08 reason), do not exceed it without an `adr:` raise.**

## Do NOT do

- Do NOT **rename or re-sign** any method — the split is a move. Audit rows are unchanged because the `Via` values are recorded by `AuthorizationService`, not by the service being split — do not touch them.
- Do NOT **change any route** (path or verb) or any served HTML — the route-table diff must be empty and the board / to-do detail pages identical (the U06 Razor doctrine).
- Do NOT touch the C06 **`TranslationLane`** or re-implement the Projects translation forwarders (C06's territory).
- Do NOT move controllers off Marten (C09) or split long Core methods like `BoardDetail` / `TodoDetail` into a builder beyond what C08 needs (C10's territory) — though extracting the two long action bodies to a `*ViewModelBuilder` **is** in scope here (see Exit).
- Do NOT raise any baseline `max` / `count` **without an `adr:` reason** — the ratchet is shrink-only (a raise without a reason fails CI).
- Do NOT touch `tools/docs/improve-check.ps1` or the docs gates.

## Exit criteria (all must pass before you finish)

1. **None of the five Projects files** (`ProjectService.cs`, `ProjectsController.cs`, `IProjectService.cs`, plus the new `BoardService` / `TodoService` / `BoardsController` / `TodosController`) is over the 600-code-line file ceiling — except a type that lands over, which must carry a fresh baseline entry at its current value.
2. **`IBoardService`** over the 20-method interface ceiling carries a fresh baseline entry (ADR 0287's noted ~24); the three interfaces each satisfy the part-vs-whole test.
3. **Route-table diff is empty** — the `EndpointDataSource` test enumerating the Projects route family passes before and after; served HTML for board and to-do detail is identical.
4. Each of `IBoardService` / `ITodoService` (and the shrunk `IProjectService`) has an **integration test file naming it** (seam-test rule).
5. Existing Projects tests pass **unmodified in behaviour** (moved, not rewritten).
6. `pwsh tools/code/code-check.ps1` → exit 0 (stale-method rule satisfied; boundary edges legal; no max raised without an `adr:`).
7. `pwsh tools/docs/improve-check.ps1` → exit 0.
8. `dotnet build` clean; `IBoardService` / `ITodoService` / the shrunk `IProjectService` and the three controllers all resolve at runtime.

## Practical notes

- **The route split is the heart of C08 (and its risk).** `ProjectsController` currently interleaves four route families. Splitting it into three controllers is safe *only* because the route table is pinned by the `EndpointDataSource` test — write that test first and keep it green across all three commits. A moved `[Route]`/`[Http*]` attribute that changes a path or verb will show up there.
- **C06 is in-flight inside ProjectService.** The 12 translation methods are now thin forwarders into the shared `TranslationLane`. Move them **verbatim** with their aggregate — do not "clean them up" or re-point them; that would change C06's already-shipped seam.
- **The ratchet's moved-method rule:** a moved method gets a **new id**. Its old entry goes stale and must be *removed*, and the new id must land **under** the ceiling — a split cannot carry its old exemption along. Budget baseline edits into every aggregate commit.
- **Edit `code-baseline.json` via `--tighten`** (`dotnet run --project tools/code/CodeMetrics -- --tighten tools/code/code-baseline.json .`), which removes stale entries and preserves `adr:` fields. The file uses `\u003C`/`\u003E` escapes — a naive `json.load`/`dump` round-trip churns every escape site and muddies the diff; prefer `--tighten` over a textual hand-edit here.
- **Constructor-assignment trap (learned in C07):** when you add a sibling dependency to a controller or service constructor, **assign every parameter to its field** in the body — C07 shipped two controllers whose new `profileSettings` / `groupService` fields were never assigned (silent NRE at first call). Assign all fields, and add a null-check where the old one had one.
- **DI registration trap (learned in C07):** tests that build a service by hand register the **interface**, not the concrete type — `sc.AddSingleton<INotificationEmitter>(ns)`, not `sc.AddSingleton(ns)`. Resolve-by-interface from a concrete-type registration is a no-op and the emission silently never fires.
- **Trust the code, not the ADR's counts.** ADR 0287 gives the three-aggregate shape; re-derive the exact per-method assignment from `IProjectService.cs` + the ADR's aggregate descriptions. A method you can't place in an aggregate belongs in the shrunk `IProjectService` (projects + goals) — when unsure, keep it there.
- **One aggregate per commit** keeps each diff reviewable and lets the baseline retire step by step — do not batch all three aggregates into one commit.
- The **xunit.v3 discovery quirk** (`dotnet test` → "Zero tests ran / Exit code 5") is in `docs/dev/testing-and-ci.md`; run a class with the in-process runner: `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.<Class>"` (and the matching `Kumunita.Web.Tests.dll` for Web).
- **Terminal output can be swallowed** in this sandbox — the reliable channel is the Serena REPL running `subprocess.run(...)` and returning the output as the **last string expression** (never `print()`). Redirect long runs to `.tmp/` and read the file; verify file existence with a read rather than trusting an empty terminal.
