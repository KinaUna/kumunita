# IP — Integration Polish: rolling handoff notes

> Every IP unit appends a `## U##` section here before it moves its own
> plan to `done/`. The file is created by U00 and moved to `done/` at U07.

## U00 — Docs parity + decision lock

**Delivered:**
- Corrected `ARCHITECTURE.md` ADR range — dropped the hardcoded `0001–0053`
  in favour of "0001 onward (the running decision ledger — append-only, highest
  number = newest)" so the line never needs a manual update.
- Extended the value-chain table in `ARCHITECTURE.md` to M18–M23 (5 new rows:
  M18 recurring events / M19 guest accounts / M20 notification quiet times /
  M21 document management / M23 extended profiles). M22 is in-progress and does
  not get a row.
- Added a one-line rationale comment to `Milestones.cs` before the M23 entry
  explaining why M23 ships before M22 (M23 was pulled forward and completed
  while M22 remained `StatusPlanned` / deferred).
- Locked [PROPOSED] D1–D6 verbatim. No amendments — the six decisions
  (D1 `AccessAuditFactory` pure static helper; D2 `StandingMatrix` pure
  static helper; D3 rate-limiting on message send + post/reply create;
  D4 log-and-degrade in `catch {}` blocks; D5 unit tests for
  `BlockedAccountMiddleware` + `PrivilegedStampMiddleware`;
  D6 localized `?error=` codes in the Login view) are all confirmed as
  consistent with the codebase's current state.

**Open questions:** none.

**Next unit:** U01 — `AccessAuditFactory` + the three `StoreAuditRow` call
sites. Entry: `src/Kumunita.Core/Authorization/AccessAudit.cs`,
`src/Kumunita.Core/Events/EventService.cs` (StoreAuditRow, ~line 1697),
`src/Kumunita.Core/Inventory/InventoryService.cs` (StoreAuditRow, line 507),
`src/Kumunita.Core/Projects/ProjectService.cs` (StoreAuditRow, ~line 3498).

## U01 — `AccessAuditFactory` + the three `StoreAuditRow` deletions

**Delivered:**
- New `src/Kumunita.Core/Authorization/AccessAuditFactory.cs` — a pure
  static class with `SingleTarget(actorId, action, targetKind, targetId, via,
  outcome = Allow)`: sets all 9 `AccessAudit` fields, `EffectivePrincipalId =
  actorId`, `VisibleCount`/`HiddenCount` = null (single-target shape).
- Deleted the private `StoreAuditRow` from `EventService`, `InventoryService`,
  and `ProjectService`; all **57** call sites (8 + 9 + 40) now call
  `session.Store(AccessAuditFactory.SingleTarget(...))` — the "caller's
  session" idiom preserved. `InventoryService.AuditVia` (role-based) left in
  place; the `*AuditViaFor` mappers untouched (U02).
- New `tests/Kumunita.Core.Tests/AccessAuditFactoryTests.cs` (2 pin tests).
- **Exit:** `dotnet build` clean; `Kumunita.Core.Tests` 1088/1088 green;
  `private static void StoreAuditRow` grep empty across `Kumunita.Core`.

**Open questions:** none. **Note:** actual call-site count is 57 (the
register estimated ~62, projecting ~45 Project) — codebase wins for mechanics.

**Next unit:** U02 — `StandingMatrix` + the four `*AuditViaFor` mappers.

## U02 — `StandingMatrix` + the five author-based mapper deletions

**Delivered:**
- New `src/Kumunita.Core/Authorization/StandingMatrix.cs` — a pure static
  class with `AuditVia(actorId, ownerId)`:
  `string.Equals(ownerId, actorId, Ordinal) ? Owner : Admin`. Zero new
  authorization surface (C-IP·2): no new `AccessVia` value, no DI registration.
- Deleted the five author-based mappers: `AuditViaFor` (EventService) +
  `TodoAuditViaFor` / `BoardAuditViaFor` / `GoalAuditViaFor` /
  `ProjectAuditViaFor` (ProjectService). All five bodies were the same
  `string.Equals(resource.AuthorId, actorId, Ordinal) ? Owner : Admin` — the
  register's "owner property name differs per resource" line was a stale
  estimate; the codebase wins (all `.AuthorId`).
- Replaced all **26** call sites (2 EventService + 24 ProjectService) with
  `StandingMatrix.AuditVia(actorId, <resource>.AuthorId)`; updated 8 stale
  `<see cref="…"/>` references (7 ProjectService + 1 EventService) to point at
  `StandingMatrix.AuditVia`.
- `InventoryService.AuditVia(IReadOnlySet<string>)` (role-based, a different
  pattern) left in place, as the register requires.
- New `tests/Kumunita.Core.Tests/StandingMatrixTests.cs` (3 pin tests: owner →
  Owner, non-owner → Admin, ordinal case-sensitive).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` clean (0 warnings);
  `Kumunita.Core.Tests` 1091/1091 green (U01's 1088 + 3 new); `AuditViaFor`
  grep empty in `EventService.cs` + `ProjectService.cs`.

**Open questions:** none. **Note:** actual call-site count is 26 (the register
estimated ~22) — codebase wins for mechanics.

**Next unit:** U03 — the two rate-limit policies (`message` + `write`) + the
`[EnableRateLimiting]` attributes in `Kumunita.Web`.
