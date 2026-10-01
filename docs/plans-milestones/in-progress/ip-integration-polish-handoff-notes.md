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
