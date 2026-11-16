---
id: dev-recipes
title: "Recipes — the six how-tos a cold maintainer copies and runs"
tier: living
kind: guide
status: current
audiences: [developer, agent]
related:
  - docs/dev/code-map.md
  - docs/dev/testing-and-ci.md
  - docs/dev/README.md
lane: improve-devx
updated: 2026-11-16
---

> **Abstract:** the six one-hop how-tos the cold-start agent asked for (plan E8):
> add a UI string · add a field to a doc type · add a new doc type · add a
> controller + view · add a test and where it goes · read a red CI run. Each is
> a pointer to the right place + the one command, not a restatement of the map
> or the gates. **Who is it for:** whoever makes one of these changes.
> **When to read the rest:** before the change, not after.

# Recipes

Six recipes; each lists the **files**, the **tests** that must stay green, the
**live gate(s)** that fire if you drift, and the **one command** to run.
"Where a concern lives" is [`code-map.md`](code-map.md) — link, don't restate.
The gate names and runner form are the live ones in
[`testing-and-ci.md`](testing-and-ci.md) — the runner is `tools/check.ps1` and
the targeted in-process form `dotnet exec <tests.dll>` (never `dotnet test`).

## Add a UI string

- **Surface naming (the D10 rule):** surface = **lower-case controller name**
  (key's first dot-segment, one JSON file per surface per language). Known
  exceptions live in the code map (e.g. `inv.*` on the Inventory surface) —
  **rename nothing** (a rename orphans admin-edited translation rows).
- **Files:** `src/Kumunita.Core/Localization/Keys/` — one file named after the
  surface, in **each** of the `en`, `de`, `fr`, `da` language folders
  (`en` is the floor); the loader is `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`.
- **Tests:** the `KnownTranslationKeys` closure/parity tests in
  `tests/Kumunita.Core.Tests/` keep green.
- **Gates:** `code-check` (file-size ratchet) if the JSON grows; `improve-check`
  gate `b` (ADR index) if you also added an ADR.
- **Command:** `pwsh tools/check.ps1 -Fast`.

## Add a field to a doc type

- **Where:** the context's POCO (one row per context in `docs/dev/code-map.md`);
  registration surface, e.g. `src/Kumunita.Core/InventoryDocTypes.cs` (or the
  context's own `*DocTypes.cs`).
- **Rule:** the field is a plain POCO property — **Marten derives the schema
  delta**; the versioned-schema doctrine is ADR `docs/adr/0004-data-persistence-and-schema-evolution.md`.
- **Tests:** the context's service tests (Core) + the controller tests (Web)
  listed in the code-map row.
- **Gates:** `code-check` (the `.cs` ceiling is shrink-only — a big field set
  may need the part-vs-whole test, ADR-reasoned); `devx-check` rule `code-map`
  (regenerate with `pwsh tools/devx/code-map.ps1` if the row changed).
- **Command:** `pwsh tools/check.ps1` (full — schema changes run the Core suite).

## Add a new doc type

- **Files:** the POCO + its `*DocTypes.cs` registration in the context folder
  (which one? `docs/dev/code-map.md`); the context's `DependencyInjection.cs`.
- **Rules:** ADR `docs/adr/0004-data-persistence-and-schema-evolution.md`
  (Marten owns the document); a new registered doc type carries `[DataClass]`
  (ADR `docs/adr/0298-data-classes-in-code.md`) or the `SecGate`
  `DataClassCoverageTests` in `tests/Kumunita.Web.Tests/SecGate/` reports it.
- **Tests:** a new service test class in `tests/Kumunita.Core.Tests/`; the
  `SecGate` surface stays green.
- **Gates:** `code-check` (ratchet); `devx-check` rule `code-map` (the new
  context gets a map row — regenerate, never hand-edit).
- **Command:** `pwsh tools/check.ps1`.

## Add a controller + view

- **Files:** a `<Name>Controller.cs` in `src/Kumunita.Web/Controllers/` and a
  `<Name>/` view folder in `src/Kumunita.Web/Views/` (view folder = controller name).
- **Rules:** views never carry an inline `on*` handler or a `<script>` block
  (`SecGate` `CspInlineDisciplineGateTests`); audit-by-default reads are
  resolved through the `Authorization` service, not a claim.
- **Tests:** a `<Name>ControllerTests` class in `tests/Kumunita.Web.Tests/`.
- **Gates:** `SecGate` (view + log-template surface); `code-check`
  (`.cshtml` ceiling).
- **Command:** `pwsh tools/check.ps1 -Fast` (Web suite, no Core).

## Add a test (and where it goes)

- **Where:** Core context → `tests/Kumunita.Core.Tests/`; controller / view /
  middleware → `tests/Kumunita.Web.Tests/` (Phase E of this lane mirrors these
  onto context folders — put new tests at the project root only as a
  stopgap).
- **Fixture:** Postgres comes from the assembly fixture (`AssemblyFixtures.cs`
  in each project) — **do not** add `IClassFixture<PostgresFixture>` (the
  `devx-check` `iclassfixture` rules fail it); server-scoped classes are on the
  allowlist only.
- **Gates:** the `devx-check` `vscode-task-dotnet-test` rule (the runner is
  `check.ps1`, not a `dotnet test` task); `improve-check` gate `d` (handoff
  note length, if you append one).
- **Command:** `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
  (or the Web dll), or `pwsh tools/check.ps1 -Only build,web,core`.

## Read a red CI run

- **First:** the `Message → script → fix` table in
  [`testing-and-ci.md`](testing-and-ci.md) — copy the red line into the table;
  each quoted prefix resolves in `tools/` or `tests/` (the `devx-check`
  `testing-and-ci-messages` rule pins that truth).
- **Then:** the gate's own `[FAIL]` line names its script — `docs-check` /
  `improve-check` / `code-check` / `devx-check` (all under `tools/`) — and the
  fix is in the table row, not in the log.
- **Environmental** (browser, Node, `libnss3`) → the CI known-failure
  cheatsheet in the same doc. **By design** (a ratchet red) → re-baseline +
  annotate the ADR in the same commit; never loosen the gate.
- **Command:** `pwsh tools/check.ps1` reproduces the loop locally;
  `pwsh tools/devx/devx-report.ps1` prints the current numbers.
