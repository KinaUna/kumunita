# GA U03 — Core seam tests (the 3 pinned names)

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-assignment.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Implement the **3** Core seam tests from the design doc §Pinned contract in
`tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs` (the GU lane's
`GuardianControlsTests.cs` shape — the `PostgresFixture`, the seed-and-
assert pattern). **This unit does NOT author the Web tests (U06) and does
NOT run the acceptance gate (U07).**

## Context you need (read these first, in this order)

1. `docs/design/guardian-assignment-design.md` § **`## Pinned contract`
   → `### Pinned seam tests (exact names)`** (U01 pinned the 3 names) — the
   *primary* source for this unit. Match the names verbatim.
2. `tests/Kumunita.Core.Tests/GuardianControlsTests.cs` — the GU lane's
   seam-test shape to mirror: the `PostgresFixture` usage, the seed-and-
   assert, the `IIdentityService` + `IUserInfoService` injection, the
   `RegisterAsync` + `VerifyWithTokenAsync` seeding pattern (the M1
   lifecycle).
3. `tests/Kumunita.Core.Tests/PostgresFixture.cs` — the harness (the
   fresh-scratch-DB shape; the `CreateUser` / `CreateProfile` helpers if
   they exist).
4. `src/Kumunita.Core/Identity/IdentityService.cs` §`FindSubjectByEmailAsync`
   (U02's impl — the null return + the subject id the tests assert).
5. `src/Kumunita.Core/Identity/IIdentityService.cs` §`FindSubjectByEmailAsync`
   (the seam's doc-comment — the G-A·2 no-leak shape the tests pin).
6. `docs/design/guardian-assignment-design.md` § Invariants G-A·2 (the
   no-leak shape) + G-A·1 (the standing gate — not exercised in these 3
   tests, but the context for the file's doc-comment).

## Deliverables (1 file, new)

### `tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs` (new)

**3 tests**, one per pinned name. Mirror the `GuardianControlsTests.cs`
harness (the `PostgresFixture`, the `IIdentityService` + `IUserInfoService`
injection, the `RegisterAsync` + `VerifyWithTokenAsync` seeding):

1. `FindSubjectByEmail_ReturnsSubjectIdForKnownEmail` — seed a verified
   account with a known email via `RegisterAsync` + `VerifyWithTokenAsync`
   (the M1 lifecycle); assert `FindSubjectByEmailAsync(email)` returns the
   account's subject id (the `ThinPrincipal.SubjectId`).
2. `FindSubjectByEmail_ReturnsNullForUnknownEmail` — assert
   `FindSubjectByEmailAsync("no-account@example.com")` returns null
   (G-A·2's no-leak shape — a null, not an exception).
3. `FindSubjectByEmail_IsCaseInsensitiveOnEmail` — seed an account with
   email `User@Example.com`; assert
   `FindSubjectByEmailAsync("user@example.com")` returns the subject id
   (the ASP.NET Identity `FindByEmailAsync` case-insensitive precedent).

The file's doc-comment anchors the GA lane (ADR 0038) + the G-A·2 no-leak
shape. **No** `using` for `Kumunita.Web` (these are Core tests). **No**
new fixture (reuse `PostgresFixture`).

## Exit

`dotnet build` green. Run via the **reliable path** (AGENTS.md): `dotnet
build Kumunita.slnx -c Debug` then `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
--filter-method "*FindSubjectByEmail*"` — reports **3 GA tests discovered,
3 executed**. Record the pass/red status of each (for U07's gate). **No
gate recorded** (U07). **Clean up Docker:** if the process was killed,
`docker container prune`. Handoff note: 3 lines starting `## U03 — Core
seam tests (3)` — (a) the file path, (b) the 3 names (verbatim), (c) the
pass/red counts (for U07 to consume), (d) any `## U<m> — Drift pause`.
