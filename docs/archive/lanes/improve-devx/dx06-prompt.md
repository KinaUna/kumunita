---
id: dx06-prompt
title: "DX06 prompt — triage the server-scoped tests: identify the sole class-fixture allowlist class for the DX07/DX08 assembly-fixture move"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX06** — branch **B**, unit 1
> (start of the Fast-tests branch: DX06 → DX07 → DX08 → DX09). DX01–DX05 are
> committed; **DX06 lands no code and no file other than the handoff note.** It
> runs one `grep`, reads only the hits, and writes **the class-fixture
> allowlist** that DX08 (Core) and DX09 (Web) convert around: the list of test
> classes that touch **server-level** Postgres state and must therefore keep
> their own class fixture (one container each) when the rest of the suite moves
> to one `[assembly: AssemblyFixture]` per assembly.
> **Who is it for:** the clean-slate agent starting DX06.
> **When to read the rest:** before writing the allowlist section.

# DX06 — Triage server-scoped tests (no code)

## What you are doing

You are carrying **DX06** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D2** decision row and
the **DX06** unit section first; they are your spec). DX06 is the **triage**
that makes DX07/DX08/DX09 safe: before anyone strips
`IClassFixture<PostgresFixture>` off the test classes (to share one
assembly-wide container), we must know which classes **cannot** share one
container because they mutate **server-level** state (roles, extensions,
`ALTER SYSTEM`, cross-database catalog work) rather than just their own
scratch database. That list is the **allowlist**.

**The per-unit contract (plan D8) binds you:** read only the named entry
reads; edit only the deliverables; run only the named checks; pause and say so
in the handoff if an entry read no longer matches this plan or the plan itself;
never edit around a failing test. **DX06 edits exactly one file** (the handoff
note) — no source, no `csproj`, no fixture, no devx-check rule.

## Drift facts you must know (verified 2026-11-14, `development`, HEAD `e9801e54`)

1. **DX06 lands no code.** The only deliverable is a new `## DX06` section in
   `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`. Do **not**
   create a file, do **not** touch `PostgresFixture.cs`, do **not** add a
   devx-check rule or a `devx-baseline.json` entry. The rule that *consumes*
   this allowlist lands at **DX08** (the `IClassFixture<PostgresFixture>`
   count-outside-allowlist rule, baseline 0); DX06 only *produces* the list.
2. **The ADR is 0307, not the plan's "0300".** The plan body and the
   `devx-check` rule reference say "ADR 0300"; the actual committed ADR is
   `docs/adr/0307-developer-feedback-loop.md` (D1–D7, D2 = the assembly
   fixture). Cite **0307** in the handoff; note the plan's "0300" as a known
   stale number (same drift the DX04 handoff already recorded).
3. **The `grep` hits exactly 3 files — 1 test class + 2 fixture files.** Running
   the plan's own entry-read command
   (`grep -rln "CREATE ROLE\|ALTER SYSTEM\|CREATE EXTENSION\|BootConnectionString\|Container\." tests --include=*.cs`)
   yields:
   - `tests/Kumunita.Core.Tests/PostgresFixture.cs` — **the fixture itself**
     (defines `Container`, `BootConnectionString`, `NewDatabaseAsync`). Not a
     test class; it is *what* the fixture is. **Not on the allowlist** — it is
     the thing being shared.
   - `tests/Kumunita.Web.Tests/PostgresFixture.cs` — **the Web copy** of the
     same fixture (the E10 byte-for-byte copy DX09 later deletes/links). Not a
     test class; **not on the allowlist**.
   - `tests/Kumunita.Core.Tests/F22_AppRoleCannotDropSchemaTests.cs` — **the
     only test class**, and the **sole allowlist entry** (reason below).
   There are **no** other hits: `grep -rln "DROP DATABASE" tests --include=*.cs`
   returns only `F22`, and no test class outside `F22` calls
   `BootConnectionString` / `Container.` / `CREATE ROLE` / `ALTER SYSTEM` /
   `CREATE EXTENSION`.
4. **`F22_AppRoleCannotDropSchemaTests` is genuinely server-scoped** (the
   reason to allowlist it). Its class summary is the two-role-split witness
   (ADR 0171, the F22 DB-18 pin §5 `AppRole_CannotDropSchema`): it
   `CREATE ROLE`s `kumunita_migrate` + `kumunita_app`, `CREATE DATABASE …
   OWNER kumunita_migrate`, asserts the app role is *refused* `CREATE SCHEMA` /
   `DROP SCHEMA mt` / `DROP DATABASE` (SQLSTATE 42501), then `DROP DATABASE IF
   EXISTS` in teardown. All of that is **catalog-level** state on the shared
   server — it cannot run against a scratch-database-per-class container shared
   by other parallel classes, and two of those tests would race. It must keep a
   private `IClassFixture<PostgresFixture>` (its own container).
5. **The conversion pool (for context, not for DX06 to touch):** 154 Core +
   36 Web test classes currently declare `IClassFixture<PostgresFixture>`.
   After DX08/DX09, every one of them *except the allowlist* loses that
   interface and rides the assembly fixture. `F22` is the one that keeps it.
6. **`xunit.v3 4.0.x AssemblyFixtureAttribute` is DX07's entry read, not
   DX06's.** DX06 does not import it, does not write the attribute, does not
   open the package XML. That is DX07's job (with AGENTS.md "the one thing":
   verify the API from the package, not from memory).

## DX06 deliverable (one file)

Append a `## DX06 — server-scoped triage (no code)` section to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`. 3–8 lines.
It must state, in the handoff's established voice:

- **Files:** none (triage only). No source, no `csproj`, no fixture, no
  devx-check rule / baseline change (the consuming rule lands at DX08).
- **The grep + its 3 hits,** with the two `PostgresFixture.cs` files
  classified as *the fixture, not a test class* (hence excluded), and `F22`
  as the only test class.
- **The allowlist = `F22_AppRoleCannotDropSchemaTests` (Core) only**, with the
  one-line reason: catalog-level `CREATE ROLE` / `CREATE DATABASE … OWNER` /
  `DROP DATABASE` (ADR 0171 two-role-split witness) — cannot share a
  per-class container with parallel classes, so it keeps a private
  `IClassFixture<PostgresFixture>`.
- **The rule that consumes this:** DX08 lands
  "devx-check counts `IClassFixture<PostgresFixture>` usages outside the
  allowlist; baseline 0" — so this section *is* the allowlist's source of
  truth.
- **Cite ADR 0307** (D2), and note the plan's "0300" is a stale number
  (consistent with the DX04 handoff).

**DX06 exit:**
- The `## DX06` section is present in the handoff note and names exactly one
  allowlist class (`F22_AppRoleCannotDropSchemaTests`) with a reason.
- `git status --short` shows **only** the handoff note modified (the untracked
  `dx0N-prompt.md` briefs stay untracked — DX37 archives them). **No source
  file, no `csproj`, no workflow, no `devx-*.json`/`.ps1` changed.**

## Commit

One commit, conventional-commits + lane tag:

- `test(devx): DX06 — server-scoped triage, F22 is the sole allowlist class (IMPROVE-DEVX DX06)`

Stage explicitly (`git add
docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with
`git commit -F <file>` (not an inline `-m` long body — write the message to
`.tmp/dx06-commit-msg.txt` first, then `rm` it), and end with
`git status --short` clean.

## Definition of Done

1. `## DX06` section appended to
   `docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`; it lists
   the grep, classifies the two `PostgresFixture.cs` hits as the fixture (not
   a test class), and names **`F22_AppRoleCannotDropSchemaTests` as the sole
   allowlist class** with a one-line catalog-level reason (ADR 0171).
2. It records that the consuming rule lands at **DX08** (baseline 0 outside the
   allowlist) and cites **ADR 0307** (D2), noting the plan's "0300" is stale.
3. **No code, no `csproj`, no fixture, no devx-check rule / baseline** changed.
4. `git status --short` shows only the handoff note (plus the untracked prompt
   briefs); one commit on `development`.
