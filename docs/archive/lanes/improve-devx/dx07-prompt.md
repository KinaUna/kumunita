---
id: dx07-prompt
title: "DX07 prompt — assembly fixture in Core.Tests: the first context rides one shared Postgres container"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX07** — branch **B**, unit 2
> (Fast-tests: DX06 → **DX07** → DX08 → DX09). DX06 is committed. **DX07 is the
> first code unit of Branch B**: it introduces one `[assembly: AssemblyFixture]`
> so the whole Core test assembly shares a **single** `postgres:18` container
> instead of one per test class, then moves the **Bookmark\* and Inventory\***
> classes off their own `IClassFixture<PostgresFixture>` onto it. That is the
> proof that the shared-container approach is safe before DX08's `sed` converts
> the remaining ~150 classes. **The consuming devx rule does NOT land here** —
> it lands at DX08 (same pattern as DX06 produced its list; DX07 only proves the
> mechanism works for one context).
> **Who is it for:** the clean-slate agent starting DX07.
> **When to read the rest:** before writing `AssemblyFixtures.cs`.

# DX07 — Assembly fixture in Core.Tests (first context)

## What you are doing

You are carrying **DX07** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D2** decision row and
the **DX07** unit section first; they are your spec). DX06 produced the
allowlist (`F22_AppRoleCannotDropSchemaTests` is the one class that must keep a
private fixture — **do not touch it**). DX07 proves the assembly-fixture
mechanism on a small, safe first context (Bookmark\* + Inventory\*) so that
DX08's mechanical conversion of the rest is low-risk.

The mechanism: xunit.v3 lets an assembly declare one shared fixture instance
that every test class in the assembly receives, instead of each class spinning
up its own. `PostgresFixture` already hands **each test method a fresh scratch
database** via `NewDatabaseAsync` (155 callers), so classes sharing one server
do not clobber each other's catalog — that is precisely why the move is safe.

**The per-unit contract (plan D8) binds you:** read only the named entry
reads; edit only the deliverables; run only the named checks (the in-process
`-class` runner form, not `dotnet test`); pause and say so in the handoff if an
entry read no longer matches this plan or the plan itself; **never edit around a
failing test.**

## Drift facts you must know (verified 2026-11-15, `development`, HEAD `d46daf8c`)

1. **xunit.v3 is `4.0.3`** (`tests/Kumunita.Core.Tests/Kumunita.Core.Tests.csproj`
   L26: `<PackageReference Include="xunit.v3" Version="4.0.3" />`). `Xunit.AssemblyFixtureAttribute` **exists in 4.0.3**
   (confirmed in the package XML:
   `~/.nuget/packages/xunit.v3.extensibility.core/4.0.3/…/xunit.v3.core.xml`,
   18 references; property `AssemblyFixtureType` — i.e. the **non-generic**
   `AssemblyFixture(typeof(PostgresFixture))` form the plan uses is the right
   one, NOT a generic `AssemblyFixture<PostgresFixture>`). Per AGENTS.md "the
   one thing": re-confirm the attribute + its ctor from the package XML, not
   from memory, before you write the attribute.
2. **`PostgresFixture.cs` is 63 lines** and is `public sealed class
   PostgresFixture : IAsyncLifetime`. Its builder is:
   ```
   new PostgreSqlBuilder("postgres:18").WithDatabase("kumunita")
       .WithUsername("kumunita").WithPassword("kumunita").Build();
   ```
   There is **no `WithCommand` / `max_connections` today** (verified by grep).
   Because parallel classes will now share one server, you add
   `.WithCommand("-c", "max_connections=300")` to the builder (the plan's value)
   **before** `.Build()`. Also refresh the class's one-line summary (it currently
   says "per test class" — after this unit it is "per assembly").
3. **The five target classes**, all currently
   `public class …(PostgresFixture fixture) : IClassFixture<PostgresFixture>`:
   - `BookmarkBatchReadTests`
   - `BookmarkDocTypesDdlTests` — **also** declares `, IAsyncLifetime`
   - `BookmarkServiceTests`
   - `InventoryDocTypesDdlTests` — **also** declares `, IAsyncLifetime`
   - `InventoryServiceTests`
   Strip **only** `IClassFixture<PostgresFixture>` (and, where it is the first
   of two interfaces, the trailing comma), **leaving `IAsyncLifetime` intact**.
   A sed that deletes the whole interface list breaks the two `…DocTypesDdl…`
   classes — that is the trap to avoid. **Do not add `IAsyncLifetime` to the
   three that lack it** — that is not part of DX07.
4. **`F22_AppRoleCannotDropSchemaTests` is NOT a deliverable.** It is the DX06
   allowlist entry (catalog-level `CREATE ROLE` / `CREATE DATABASE … OWNER` /
   `DROP DATABASE`, ADR 0171) and keeps its private
   `IClassFixture<PostgresFixture>`. Leave it untouched; DX07's "Bookmark\* and
   Inventory\* only" scope is what keeps F22 out.
5. **No devx rule, no `devx-baseline.json` entry, no `csproj` change here.**
   The consuming rule — "devx-check counts `IClassFixture<PostgresFixture>`
   usages outside the allowlist; baseline 0" — lands at **DX08**, which is where
   the `sed` converts the remaining ~150 Core classes (and Web at DX09). DX07
   only proves the mechanism works and records the timing delta.
6. **The runner form** (D8 contract; `docs/dev/testing-and-ci.md` L42–43):
   ```
   dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.BookmarkServiceTests"
   ```
   Single-dash **`-class`** (not `--class`), fully-qualified name
   (namespace `Kumunita.Core.Tests`). Container startup ~15 s is normal.

## DX07 deliverables (2 files + a 5-file mechanical batch)

1. **New `tests/Kumunita.Core.Tests/AssemblyFixtures.cs`** (1–3 lines + a
   comment): `[assembly: AssemblyFixture(typeof(PostgresFixture))]`. Namespace
   `Kumunita.Core.Tests`.
2. **`tests/Kumunita.Core.Tests/PostgresFixture.cs`:** add
   `.WithCommand("-c", "max_connections=300")` to the builder before `.Build()`;
   update the class summary's "per test class" → "per assembly (one shared
   container; each test method still gets a fresh scratch database via
   `NewDatabaseAsync`)".
3. **The five target classes (mechanical batch):** remove
   `IClassFixture<PostgresFixture>` from the class declaration only; keep
   `IAsyncLifetime` on the two `…DocTypesDdl…` classes. One line per file.

**DX07 exit (the named checks):**
- Each of the five classes passes with the `-class` runner form above.
- **The full Core suite passes** (`dotnet exec …/Kumunita.Core.Tests.dll`).
- **Record the Core-suite seconds before → after** in the handoff (the "before"
  is the DX06-era number — measure it, don't assume; DX08's target is ≤ 90 s
  locally, so your before→after is the first data point).

## Handoff

Append a `## DX07 — assembly fixture, first context` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice. It must state: the two fixture files + the 5-class batch as
the deliverables; `F22` untouched (the DX06 allowlist); the
`max_connections=300` addition and why (parallel classes share one server);
that **no devx rule / baseline** landed (it lands at **DX08**); **Core-suite
seconds before → after**; and a cite to **ADR 0307** (D2) with the note that the
plan body's "0300" is a stale number (consistent with the DX04/DX06 handoffs).

## Commit

One commit, conventional-commits + lane tag:

- `test(devx): DX07 — assembly fixture in Core.Tests, first context (IMPROVE-DEVX DX07)`

Stage explicitly (`git add` the two fixture files + the five test classes + the
handoff note), commit with `git commit -F <file>` (write the message to
`.tmp/dx07-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx0N-prompt.md` briefs stay untracked —
DX37 archives them).

## Definition of Done

1. `AssemblyFixtures.cs` declares `[assembly: AssemblyFixture(typeof(PostgresFixture))]`
   (attribute form verified against the 4.0.3 package XML, not memory).
2. `PostgresFixture.cs` adds `max_connections=300` and its summary reflects the
   per-assembly scope.
3. The five Bookmark\*/Inventory\* classes no longer declare
   `IClassFixture<PostgresFixture>`; the two `…DocTypesDdl…` classes keep
   `IAsyncLifetime`; **F22 is untouched**.
4. No devx rule / `devx-baseline.json` / `csproj` change (that is DX08).
5. The five classes pass via `-class`; the full Core suite is green; seconds
   before→after are in the handoff.
6. `git status --short` shows only the six test files + `AssemblyFixtures.cs` +
   the handoff note (plus untracked prompt briefs); one commit on `development`.
