# C3-07 — IMPROVE-CODE-3 unit prompt (the 3 flagged-accidental boundary edges)

You are the **eighth agent** on the **IMPROVE-CODE-3** lane in the **Kumunita**
repo (`/home/agent/projects/kumunita`, a self-hosted single-community platform
on .NET 10, Marten + Wolverine, Razor Pages/MVC, Postgres). C3-00…C3-06 are
done. You run the **C3-07** unit — **D5: the 3 flagged-accidental boundary
edges** named in ADR 0290 for C2-03 (never landed because C2 closed held).
Each edge is a concrete cross-module type where a contract extraction
(interface / `*Policy` / `*Keys`) would retire it. After C3-07, the only
remaining unit is **C3-08 the close** (the six-member flip decision).

## Why you're here

C3-06 (ADR 0296) retired the last clean allowlist entry (`ReportTargetsController`),
bringing the allowlist to **17** (the honest floor; DoD #6 `≤ 5` not met).
The 5 carried-in DoD lines that **do** hold (ratchet honest, translation ≤ 40,
concrete-edges ≤ 9, two untested services covered, both E11 SEC pointers) are
green. The two that **don't**: DoD #3/#4 (files **23 → 8** — **met**) and
DoD #6 (Marten allowlist **≤ 5** — **17**, the honest floor). C3-07 is the
last work unit before the close; C3-08 re-verifies every DoD line from a clean
state and decides the six-member flip.

## What this unit must achieve (the unit DoD)

**Retire the 3 flagged-accidental boundary edges** via contract extraction
(the D5 resolution). Each edge is a concrete type crossing where a contract
(interface / `*Policy` / `*Keys`) would do. Extract the contract, re-point the
caller to the contract, and the edge retires from the
`boundary_violations` baseline. The `adr: 0290` field on each edge entry is
dropped with the edge (the gate's stale-entry check passes; the edge is gone).

The 3 edges (measured at C3-06 close, `2bae8552`):

| # | Edge | The accidental concrete type | The contract that would do |
|---|---|---|---|
| 1 | `Notifications → Identity : VerificationOptions` | `NotificationService` reads `VerificationOptions` (a concrete `IdentityService` property/seam) directly | a `IVerificationOptionsReader` interface (or a `*Policy` type) that the Notifications module codes against; `IdentityService` implements it |
| 2 | `Portability → Identity : AppDbContext` | the C10 `Portability` export reads `AppDbContext` (the EF Core Identity tables) directly | a `IIdentityUserReader` / `IIdentityReader` interface (or a `*Policy`) that the Portability module codes against; the Identity module implements it |
| 3 | `Projects → Web : TagSlugs` | the C08 `TodoService` / `BoardService` reads `TagSlugs` (a concrete `TagService` type in the Web layer) directly | a `ITagSlugReader` interface (or `*Keys`) that the Projects module codes against; the Tags/Web module implements it |

**Note the edge-3 direction:** `Projects → Web : TagSlugs` is unusual — a
Core module referencing a Web-layer concrete type. Verify the actual direction
in the baseline before extracting; if `TagSlugs` lives in `Kumunita.Core/Tags/`,
the edge is `Projects → Tags : TagSlugs` (a Core-to-Core edge), and the
contract extraction is the Tags-module boundary.

## How to do it

For **each** edge, in order (they are independent; do them one at a time):

1. **Verify the edge in the baseline.**
   ```bash
   python3 -c "import json; d=json.load(open('tools/code/code-baseline.json')); \
   [print(e) for e in d['boundary_violations'] if '<from>' in e.get('from','') and '<to>' in e.get('to','') and '<symbol>' in e.get('symbol','')]"
   ```
   Confirm the `from`/`to`/`symbol` triple and the current `count`. Note the
   `adr: 0290` field.

2. **Find the concrete type.** Identify the file + the type the crossing
   module references directly. Read the call site to understand the read
   surface (which properties/methods the caller actually uses).

3. **Extract the contract.** Create a minimal interface (or `*Policy` /
   `*Keys` type) in the **source** module (the module that *owns* the type).
   The interface exposes only the surface the caller needs (the
   part-vs-whole test: a C13 cold read of the interface on its own passes —
   it names the concern, not the implementation).

4. **Re-point the caller.** Change the crossing module to code against the
   interface (not the concrete type). Update the DI registration in the
   source module's `DependencyInjection.cs` to bind the concrete to the
   interface. Update any test construction sites.

5. **Verify the edge retired.** Run `CodeMetrics` (or `pwsh tools/code/code-check.ps1`
   in report mode) — the `boundary_violations` count for that edge should drop
   by the number of references re-pointed. If the edge drops to 0, it retires
   from the baseline (the stale-entry check passes). If it drops but does not
   reach 0 (other references remain), the count tightens but the edge stays
   (the `adr: 0290` field remains — it is still a named exception).

6. **Update the baseline** (text-only; preserve the 36 `\u003C` escapes). If
   the edge retired, remove the entry. If it tightened, lower the `count`.
   Validate with `python3 -c "import json; json.load(open(...))"`.

7. **Build + test.** `dotnet build` → 0 errors. Run the affected test suites
   (the crossing module's tests + the source module's tests for the new
   interface binding).

## Rules of the road

- **Ratchet + gate are the arbiter.** Retire an edge only when `CodeMetrics`
  confirms the count dropped (or retired). Don't guess.
- **Part-vs-whole is the test.** The contract must pass a C13 cold read —
  it names a real concern, not a ceremony wrapper. If the interface would be
  1 method that just returns a property, ask: is this a `*Policy` or `*Keys`
  instead? (a value type, not an interface)
- **No new edges.** The contract extraction must not mint a new boundary edge.
  If the interface lands in a module the caller doesn't already reference,
  that's a new edge — reconsider the placement.
- **No raises.** This unit should only **lower** the `boundary_violations`
  count (edges retire or tighten). If you find you need to **raise** a
  baseline entry (a new `files`/`classes`/`methods`/`interfaces` entry for
  the new contract type), **stop** — the contract type is too large or the
  extraction is producing ceremony. Reconsider.
- **Commit style** (see the C3-06 commits): `feat(improve-code-3): C3-07 —
  <short desc>`. No ADR number in the **subject**. The ADR number goes in
  the **body** when you add one.
- **Keep the docs in sync** (`AGENTS.md`): the ADR (the 3 edges retired vs
  tightened + the contracts extracted + any DI binding changes), the ADR-index
  README row (gate (b): `docs/adr/0*.md` file count == README row count),
  and the handoff notes. **No `WhatsNew.cs` row** (C3-07 is a unit, not the
  close — the close is C3-08).
- **Don't pause mid-task** (AGENTS.md): a failed tool call is a recoverable
  step — read the error, adjust, continue.

## Definition of done — verify in this order before you call it done

1. `dotnet build` → 0 errors.
2. The affected tests (the crossing modules' tests + the source modules' tests
   for the new interface binding) pass.
3. `pwsh tools/code/code-check.ps1` → **0 violations** (the 3 edges are
   retired or tightened; the `adr: 0290` field is dropped for retired edges;
   any new contract type is under every ceiling so it needs no baseline entry).
4. `pwsh tools/docs/improve-check.ps1` → **ALL GATES PASS** (esp. the ADR-index
   drift gate (b) if you added an ADR).
5. `tools/code/code-baseline.json` still has its 36 `\u003C` escapes;
   `boundary_violations` is **lower** than 465 (edges retired or tightened);
   `files` 8 · `classes` 3 · `interfaces` 8 · `methods` 89 · `allowlist` 17
   unchanged unless a contract type legitimately moves them (if so, the move
   is measured, not hand-typed).
6. The handoff notes have a C3-07 entry (the 3 edges retired vs tightened,
   the contracts extracted, the DI changes, the boundary before → after, and
   — if any edge did not fully retire — the honest "edge tightened but
   remains, count N → M" finding for C3-08).
7. `git status` clean and committed.

## Starting state (verified at C3-06 close, `2bae8552`)

`CodeMetrics`: **files 8, classes 3, interfaces 8, methods 89,
boundary_violations 465, allowlist 17**. Branch `development`, HEAD `2bae8552`.
The 3 flagged-accidental edges (from ADR 0290, named for C2-03) are:

- `Notifications → Identity : VerificationOptions` (count 2, `adr: 0290`)
- `Portability → Identity : AppDbContext` (count 3, `adr: 0290`)
- `Projects → Web : TagSlugs` (count 3, `adr: 0290`) — **verify the actual
  `from`/`to`/`symbol` in the baseline before extracting** (the direction
  may be `Projects → Tags : TagSlugs` if `TagSlugs` is in Core)

The 5 lines that **hold** (re-verify at the close): #1 ratchet honest · #5
translation ≤ 40 (39) · #6 concrete-edges ≤ 9 (9 named, `adr: 0291`) · #7
`MailAlarmThresholdPolicy` + `UsagePurgePolicy` covered · #8 both E11 SEC
pointers present. The 2 lines that **don't**: #3/#4 files **8** (≤ 8 target,
**met**) · #6 Marten allowlist **17** (≤ 5 target, **not met** — the honest
floor, ADR 0296).
