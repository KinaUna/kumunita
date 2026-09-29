# M16 — Inventory — handoff notes (scratch tier)

> **Handoff notes (scratch tier).** `## U#` sections are **appended**
> (never rewritten) as each unit completes. A new agent reads the
> relevant `## U#` section *after* its own unit plan for context. The
> **primary tier** is `docs/design/m16-inventory-design.md` (authored
> U00); the **secondary tier** is the register
> `docs/plans-milestones/plan-m16-inventory.md`. This file is the
> scratch record — what each unit left the codebase in, any drift
> observed, any follow-on noted.

## U00 — design doc + ADR 0117 locked

**(a) The locked D1–D8** (the [PROPOSED] set from the register, locked
with **no veto recorded** — the veto window closed before U00 ran; the
locked text is in `docs/design/m16-inventory-design.md`
`## Decisions (D1–D8, locked — ADR 0117)` and restated in
`docs/adr/0117-inventory.md` `## Decision`):

- **D1** — Two documents (`InventoryItem` + `InventoryCheckout`), one
  new `M16DocTypes` surface (the M9/M5 parallel-surface precedent —
  additive, idempotent, **zero migrations** for existing surfaces), **no
  new bounded context** beyond `Kumunita.Core.Inventory`; the usage
  history is the `InventoryCheckout` records *inside* the same context
  (no third "Usage" doc/context).
- **D2** — Access = the **frozen** `IAuthorizationService` + the
  **adapter** `InventoryItemToAuditableResource` (the **exact** M5
  `ProjectToAuditableResource` 6-member projection — the **only**
  difference is `TargetKind = "inventory"`); every read via
  `CanAsync(Read)` / `CanSeeAsync(Read)`; **no** new `AccessAction` /
  `AccessVia` / `Decide()` branch / method on `IAuthorizationService`;
  no group-membership read in the Inventory context.
- **D3** — `OwnerKind` ∈ {`shared`, `community`, `private`} is a
  **string** (the M5 `KanbanStatuses` shape) driving the **UI grouping**
  + the **write-standing breadth** (D5) — **never** a read gate; the
  list filters candidates by `OwnerKind` (a **filter, never a gate** —
  C-M3·2); the audience decision is unchanged.
- **D4** — Check-out / check-in = an **atomic state transition**
  (`CurrentHolderId` flip + the `InventoryCheckout` append/close in
  **one** `SaveChangesAsync`) + an **append-only** record (never
  mutated or deleted); at most **one open checkout per item** — the
  **unique partial index** on `(ItemId)` where `CheckedInAt IS NULL` is
  the **F1 witness** (the M9 `convo_uidx_pair` shape — the DB is the
  arbiter).
- **D5** — The write standing is a **server-side probe** (composing the
  frozen `IAuthorizationService` "can see it" + `IUserInfoService`
  GlobalAdmin probe): **creator ∪ GlobalAdmin** over create/edit/delete;
  **any member who can see it** over a `community`/`shared`
  check-out/check-in (the M5 `ClaimTodoAsync` broad-standing precedent);
  **owner ∪ GlobalAdmin** over a `private` one; **current holder ∪
  creator ∪ GlobalAdmin** over a check-in. The Web `[Authorize]` is a
  **convenience pre-gate only**, never the source of truth. **No** new
  `AccessVia` value (the ADR 0044 "no new AccessVia beyond Owner/Admin"
  pin).
- **D6** — M16 is a **core surface, not a lane**: **no** off-by-default
  admin toggle, **no** `LocaleSettings`-style switch, **no**
  `KumunitaFeature` admin flag — always available to the community (the
  M5 Projects shape; M9's off-by-default toggle was the *lane* shape).
- **D7** — The close flip is **one atomic unit (U06)**: `Milestones.cs`
  (M16 → done, M17 → next) + `MilestonesTests.cs` (re-pinned to
  `M17_Is_The_Single_InProgress_Milestone` + the shipped list gains M16
  + M18 stays planned) + the README Roadmap (M16 → done, M17 → in
  progress) + the M16 Status line + `docs/STATUS.md` +
  `docs/ARCHITECTURE.md` (the value-chain table gains the **M16 row** —
  the table currently ends at the M15 row) — **all four surfaces in the
  same unit** (C-M16·7, the M15/U10 docs-parity precedent).
- **D8** — **No notification lane** in M16 (deferred): composes **only**
  the frozen `IAuthorizationService` + `IUserInfoService` over
  `IDocumentStore`; **no** `INotificationService`, **no** Wolverine
  handler, **no** nudge on check-out/check-in — the README's
  "optionally" is the **usage history** (F3), not notifications.

**(b) The three §gate acceptance test names** (U06 adds them — the
locked names are in `docs/design/m16-inventory-design.md`
`## §gate — the three acceptance tests (U06)`):

1. `M16_Acceptance_ClosedLoop_CheckOutCheckInHistory` (the **closed
   loop** — create a `shared` item → a second resident checks it out →
   checks it in → the history shows **exactly one** checkout record with
   `CheckedOutAt` + `CheckedInAt` set + the `CurrentHolderId` cleared —
   C-M16·3 / F1 / F3 / D4).
2. `M16_Acceptance_HandoffAuthorizationBoundary_PrivateAndShared` (the
   **handoff** — a `private` item is invisible to a non-member: the
   list's aggregate row shows `hiddenCount ≥ 1`, the detail is a **404,
   not a 403** — C-M16·5 / C-M3·4; a `shared` item is visible to all
   members but only the **standing** actor can check it out — a
   non-standing member's check-out is **denied**, the deny
   `AccessAudit` row commits — D5 / C-M16·5 — *see* ≠ *act*).
3. `M16_Acceptance_PartVsWholeAuditCompleteness` (the **part vs. whole**
   — the list's **aggregate** `AccessAudit` row commits **atomically**
   with any concurrent write (C-M16·2 / C3); the check-out/check-in
   transition's `AccessAudit` row commits **atomically** with the state
   change (C-M16·3 — one `SaveChangesAsync`, the F1 witness)).

**(c) The §kw-l key list** (the **twenty** `inv.*` keys × en/de/fr/da —
U04 registers them in `KnownTranslationKeys`; the locked table is in
`docs/design/m16-inventory-design.md` `## §kw-l — the key list`):
`inv.nav` · `inv.list.title` · `inv.list.empty` · `inv.list.ownerKind.shared`
· `inv.list.ownerKind.community` · `inv.list.ownerKind.private` ·
`inv.list.filter` · `inv.create.title` · `inv.create.name` ·
`inv.create.ownerKind` · `inv.create.description` · `inv.create.component`
· `inv.create.submit` · `inv.detail.title` · `inv.detail.currentHolder` ·
`inv.detail.history` · `inv.detail.edit` · `inv.detail.delete` ·
`inv.detail.checkOut` · `inv.detail.checkIn` · `inv.edit.title` ·
`inv.edit.submit` — each × **4 languages** (en/de/fr/da), the
`projects.*` block shape (the `KnownTranslationKeys_ParityTests` +
`KwLRegistryConsistencyTests` pins extend automatically).

**(d) Drift from the register:** none — the locked D1–D8, C-M16·1–7,
F1–F5 + the named trade (the private-vs-shared write-standing breadth —
the M5 creator-vs-broad-standing trade C-M5·6 carried to M16), the
§kw-l list, and the §gate names match the register
(`docs/plans-milestones/plan-m16-inventory.md`) **verbatim**; the
source was checked against the live `ProjectToAuditableResource`
(the 6-member projection — the adapter shape), the `M9DocTypes` surface
(the `*DocTypes` + the `convo_uidx_pair` unique-index shape), the
ADR 0116 shape (the ADR Status/Context/Decision/Consequences + the
deferred-lane + affected-files sections), and the ADR 0117-free
confirmation against `docs/adr/README.md` (the index ends at 0116 —
0117 is free) — **no source-driven refinement was needed** (the drift
log in `docs/design/m16-inventory-design.md` §drift-guard is empty at
U00).

**(e) ADR 0117 verified free:** confirmed against
`docs/adr/README.md` — the index ends at **0116** (M15: Translation
bulk); **0117** is free and the new index row is appended **after**
the 0116 row (line 123, immediately after the 0116 row on line 122).

**Sign-off gate:** U00 is complete. The locked decisions (D1–D8),
the invariants (C-M16·1–7), the FACES (F1–F5) + the named trade, the
§kw-l key list, the §gate test names, the §deferred lane list, and the
§drift-guard frozen list are all locked in
`docs/design/m16-inventory-design.md` (the **primary tier**) +
`docs/adr/0117-inventory.md` (Status: **Accepted**, 2026-09-29) + the
`docs/adr/README.md` index row. **No code unit (U01+) may start until
this is done** — U00 is now done, so the U01–U06 unit series may
proceed in order (each reading **only its own unit plan + its entry
reads**).
