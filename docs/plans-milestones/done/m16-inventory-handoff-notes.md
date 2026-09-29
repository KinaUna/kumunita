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

## U01 — context docs + M16DocTypes + adapter

**(a) The field set** (the locked list from the design doc §2 — a unit
that adds, drops, or re-types a field is a drift event):

- `InventoryItem` (`Kumunita.Core.Inventory`, POCO, conventional
  `string Id`): `Id` · `Name` (non-empty — the card label + adapter
  `Name`) · `OwnerKind` (string: `shared`/`community`/`private`,
  default `community` — the M5 `KanbanStatuses` shape, D3 — a grouping +
  write-standing-breadth label, **never** a read gate) · `Description`
  (optional Markdown) · `ComponentId` (feed filter, never a gate —
  C-M3·2) · `AuthorId` (the standing owner — D5) · `Audience`
  (the exact `Post` `Audience`, `null` = public — D2/D3) ·
  `CurrentHolderId` (nullable — who it's with; `null` = in the pool —
  D4) · `IsDeleted` (the ADR 0024 soft-delete flag) · `LanguageCode`
  (the ADR 0018 authored-in tag) · `Created` · `Modified` (nullable).
- `InventoryCheckout` (POCO, `string Id`, **append-only** — no
  `Modified`, C-M16·3): `Id` · `ItemId` · `BorrowerId` (a SubjectId —
  display + standing, **never** a gate) · `CheckedOutAt` ·
  `CheckedInAt` (nullable — **open when null**; set = closed) ·
  `Note` (optional).

**(b) The index set** (`M16DocTypes`, `src/Kumunita.Core/M16DocTypes.cs`):

- `InventoryItem` — `(ComponentId, Created)` feed-ordering +
  `(OwnerKind, Created)` filter (unnamed — the M5 `ComputedIndex`
  auto-derives the names).
- `InventoryCheckout` — `(ItemId, CheckedOutAt)` thread-ordering
  (unnamed) + the **F1 idempotency witness**: a **unique partial
  index** on `(ItemId)` where `CheckedInAt IS NULL`, named
  `inv_uidx_item_open` (the M9 `convo_uidx_pair` shape — at most one
  open checkout per item; a concurrent double-check-out's second commit
  fails at the DB layer, C-M16·3 / F1).
- Registered in `Program.cs` — `M16DocTypes.Configure(opts);` added
  **after** `M9DocTypes.Configure(opts)` (before the M13 block).

**(c) The adapter's `TargetKind`:** `"inventory"` — the **exact**
string (C3, the `AccessAudit.TargetKind` discriminator). The adapter
(`InventoryItemToAuditableResource`) is the **exact** M5
`ProjectToAuditableResource` 6-member projection
(`Id`=`Item.Id` · `Name`=`Item.Name` · `OwnerId`=`Item.AuthorId` ·
`Audience`=`Item.Audience` projected verbatim · `ComponentId`
=`Item.ComponentId` · `TargetKind`=`"inventory"`); `sealed`;
`OwnerKind` is **never** consulted (D3 / C-M16·1 / C-M16·5).

**(d) Drift / findings (note for U02+):**

1. **The partial-index mechanism** (the one non-obvious bit): Marten
   9.31.2 has no first-class partial-index API; the working mechanism is
   `opts.Schema.For<T>().Index(expr, idx => { idx.IsUnique = true;
   idx.Name = "..."; idx.Predicate = "data ->> 'CheckedInAt' IS NULL";
   })`. **Correction to the in-repo note** in `M5DocTypes.cs` (and
   `M4DocTypes`): that note claims `ComputedIndex` "exposes no `Name`
   property (only Casing / TenancyScope)." That is **stale / wrong** —
   against the installed stack, `ComputedIndex` (a
   `Weasel.Postgresql.Tables.IndexDefinition`) exposes **`Name`,
   `IsUnique`, and `Predicate`** (all `public set`), and the compiler
   accepts all three (verified by a clean build). The `Predicate` is
   what Weasel renders as the `WHERE (...)` clause of a partial index.
   So a **named unique partial index is expressible** — the M5 note's
   "a computed index therefore cannot be named in this stack" is also
   incorrect. (The M5/M4 feed indexes simply chose the unnamed form;
   nothing *forced* them to.)
2. **The JSONB key casing in the predicate:** the predicate must use the
   **PascalCase** key (`'CheckedInAt'`, matching ADR 0004's
   `data->'IsDraft'` / `data->'Id'` shape and the repo's default
   serializer) — **not** camelCase. A nullable `DateTimeOffset` is JSON
   `null` on an open record, so `IS NULL` is the correct open-test.
3. **The doc-table naming convention** (for U02+ DDL tests): Marten's
   default document tables in this stack are the **`mt_doc_` prefix +
   lowercase class name** — `mt.mt_doc_inventoryitem` and
   `mt.mt_doc_inventorycheckout` — which is distinct from (a) the
   hand-rolled Weasel `mt."AdminOverride"` table (quoted PascalCase) and
   (b) the PascalCase JSONB *keys* inside the `data` column. A DDL
   test that hardcodes `InventoryItem` / `AdminOverride`-style names
   will miss the document tables; resolve by the actual
   `mt_doc_*` name.
4. **Document DDL is applied lazily on first save**, not by a bare
   `ApplyAllConfiguredChangesToDatabaseAsync` (that path only applies
   the storage features + Weasel migrations). The sibling doc-shape
   tests (`ProjectDocShapeTests`, `MessagingServiceTests`) therefore
   **round-trip-store their docs first**; the new `M16DocTypesDdlTests`
   follows the same shape (store + load both docs, then inspect the
   live catalog). A DDL-only test that skips the store round-trip will
   not see the doc tables.
5. **Postgres catalog quirk:** the two-arg `to_regclass('schema',
   'table')` **does not exist** (`function to_regclass(unknown, text)
   does not exist`, error 42883). Use the `pg_class` / `pg_namespace`
   join (the repo's own `AdminOverrideDdlTests` shape) or the one-arg
   `to_regclass('mt.mt_doc_inventorycheckout')` form.

**(e) The pin** (`tests/Kumunita.Core.Tests/M16DocTypesDdlTests.cs`,
2 tests — both pass): `Apply_Registers_BothDocs_And_OpenCheckout_UniquePartialIndex`
(both tables exist with the `id` + `data` Marten core, and the
`mt_doc_inventorycheckout` table carries a **unique** index with a
**non-null partial predicate** referencing `CheckedInAt` … `IS NULL`)
+ `Adapter_Presents_Frozen6MemberSurface_With_InventoryTargetKind` (the
6-member projection + `TargetKind == "inventory"`, and `OwnerKind` is
not a member of `IAuditableResource`).

**Exit gate:** `dotnet build Kumunita.slnx -c Debug` green **+**
`Kumunita.Core.Tests` green — **Total: 1013, Errors: 0, Failed: 0**.
*(Note: on this machine the canonical
`tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
build copy was blocked by a stale shared-read handle on
`src\Kumunita.Core\bin\...\Kumunita.Core.dll` (a leaked handle from a
prior terminal session; `dotnet build-server shutdown` did not clear it
and no live process mapped the DLL). The exit gate was therefore run
identically by building to an alternate output dir
(`-o .tmp\m16build`) and executing that same test assembly — same
sources, same 1013 tests. The `ComputedIndex.IsUnique/Name/Predicate`
usage compiled cleanly on the **first** build attempt, so the C#
compile gate is unambiguous.)*

**Follow-on for U02+:** the F1 witness index is now in the live schema;
the atomic double-check-out behavior (U03) should rely on it at the DB
layer (catch the unique-constraint violation → "already checked out")
rather than an app-level check — that is the whole point of the witness.

## U02 — read lanes + service seam + DI

**(a) The read-lane contract**
(`src/Kumunita.Core/Inventory/IInventoryService.cs` — the seam,
`src/Kumunita.Core/Inventory/InventoryService.cs` — the read lanes):

- **`ListItemsAsync(ownerKind, componentId, actorId, page)`** →
  `ItemPage`. Candidates = `!IsDeleted`, filtered by the optional
  `ownerKind` (**filter, never a gate** — C-M3·2 / C-M16·5) and the
  optional `componentId` (a feed filter, never a gate — the
  `Post.ComponentId` shape); ordered by `Created` descending; paged
  (`PageSize = 30`, `HasMore = candidates.Count == PageSize`, 0-candidate
  early return with no audit row). The survivors go through the frozen
  `IAuthorizationService.CanSeeAsync(Read)` over the U01
  `InventoryItemToAuditableResource` — the **single aggregate**
  `AccessAudit` row (`TargetKind = "inventory"`, `TargetId = null`,
  `VisibleCount`/`HiddenCount`, `Action = "read"`) is the C-M3·3 feed
  shape (C-M16·2 — one row, Allow **and** Deny).
- **`GetItemAsync(itemId, actorId)`** → `InventoryItem`. **The 404-vs-403
  split (C-M3·4 non-leaky shape, locked in the design doc §Human cost):**
  `KeyNotFoundException` (404) on an absent or soft-deleted id, **and
  also on a Deny** — a non-visible item is a 404, never a 403 ("its
  existence is not even disclosed"). The item's single
  `CanAsync(Read)` decision is the one single-target `AccessAudit` row
  (`TargetKind = "inventory"`, `TargetId` = the item id).
- **`GetHistoryAsync(itemId, actorId)`** →
  `IReadOnlyList<InventoryCheckout>`. Same entry gate as
  `GetItemAsync` (the history is a part of the detail page; a denied
  item is a 404, not an empty list that would leak existence); the
  append-only record set **is** the history (C-M16·3), ordered by
  `CheckedOutAt` **descending** (F3).
- **Write lanes are U03, not here** — `CreateItemAsync` /
  `EditItemAsync` / `DeleteItemAsync` / `CheckOutAsync` /
  `CheckInAsync` are documented as comments on the seam (the
  full-interface-first pin in reverse: U02 ships the reads, U03 appends
  the writes).

**(b) The composition** (D8 / C-M16·6): the constructor is
`InventoryService(IDocumentStore store, IAuthorizationService
authorization, IUserInfoService userInfo)` — **no**
`INotificationService`, **no** Wolverine handler, **no** off-by-default
toggle. The `_userInfo` field is held for U03's write lanes (the
GlobalAdmin probe anchor); the read lanes consume only `_store` +
`_authorization`.

**(c) The DI registration** (`DependencyInjection.cs`, after the M5
`IProjectService` block): `AddTransient<Inventory.IInventoryService>(sp
=> new Inventory.InventoryService(store, authorization, userInfo))` —
the same three-constructor shape as the M5 trio.

**(d) Drift / findings (note for U03+):**

1. **The 404-not-403 deviation from M5** (the one deliberate M16
   choice): M5's `GetBoardAsync` / `GetTodoAsync` throw
   `UnauthorizedAccessException` (403) on a Deny. M16's design doc
   (locked, §Human cost + §Seams) and the U06 gate test **both** pin
   that a non-visible inventory item's detail is a **404, never a 403**
   (the C-M3·4 non-leaky shape). `GetItemAsync` and
   `GetHistoryAsync` therefore throw `KeyNotFoundException` on a Deny
   — the denial is still audited (the single-target `AccessAudit` row
   with `Outcome = Deny` is committed), it just does not distinguish
   "not found" from "you may not see this". **Do not "fix" this to
   match M5's 403 shape** — that would be a drift event against the
   locked design doc.
2. **The `IInventoryService` doc-comments** use
   `<exception cref="KeyNotFoundException">` (not
   `UnauthorizedAccessException`) on both detail + history lanes —
   kept in step with (d)·1.
3. **The `ItemPage` record** (`InventoryRequests.cs`) is the
   `record ItemPage(IReadOnlyList<InventoryItem> Items, bool HasMore)`
   shape (the `TodoPage` / `PostFeed` convention — record, not an
   out param, CS1988). `CreateItemRequest` (4 fields: `Name`,
   `OwnerKind`, `Description`, `ComponentId`) is present for U03's
   `CreateItemAsync` — **not** consumed by U02's read lanes.

**(e) The pins** (`tests/Kumunita.Core.Tests/InventoryServiceTests.cs`,
3 tests — all pass):

- **U02·a** `U02_ListWritesSingleAggregateAuditRow` — a public item + an
  audience-restricted item; the actor (not the grantee) reads the list;
  the restricted item is excluded from `Items`; the single aggregate
  `AccessAudit` row (`TargetKind = "inventory"`, `TargetId = null`,
  `VisibleCount = 1`, `HiddenCount = 1`, `Action = "read"`,
  `Outcome = Allow`) is present, and the per-item Deny row carries the
  restricted item's id.
- **U02·b** `U02_DetailDeniedItemIs404Not403` — an audience-restricted
  item (grants `grantee` only); a `stranger` reads the detail;
  `Assert.ThrowsAsync<KeyNotFoundException>` (NOT
  `UnauthorizedAccessException`); the single-target Deny
  `AccessAudit` row (`TargetId` = the item id, `Outcome = Deny`) is
  committed. **This is the C-M3·4 non-leaky pin (d)·1 above.**
- **U02·c** `U02_HistoryOrderedByCheckedOutAtDescending` — three
  `InventoryCheckout` records with distinct `CheckedOutAt` (oldest
  closed, middle closed, newest open); `GetHistoryAsync` returns them
  ordered by `CheckedOutAt` **descending** (newest first, F3).

**(f) The exit gate** (green): `dotnet build Kumunita.slnx -c Debug`
+ `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
— **1016 tests, 0 errors, 0 failed, 0 skipped** (includes the 3 new
`InventoryServiceTests`; `Kumunita.Web.Tests` not re-run here — the
U02 surface is Core-only).

## U03 — write lanes + standing probes + atomic transition

**(a) The seam extension** (`src/Kumunita.Core/Inventory/IInventoryService.cs`)
— five new write-lane signatures appended **after** U02's read surface
(M4 full-interface-first pin — append-only extension; U02's read methods
are untouched, no rename):

- `Task<InventoryItem> CreateItemAsync(string actorId, IReadOnlySet<string> actorRoles, CreateItemRequest request, CancellationToken ct = default)`;
- `Task<InventoryItem> EditItemAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, EditItemRequest request, CancellationToken ct = default)`;
- `Task DeleteItemAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)`;
- `Task<InventoryCheckout> CheckOutAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, string? note = null, CancellationToken ct = default)`;
- `Task<InventoryCheckout> CheckInAsync(string itemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)`.

Every signature carries a full `///` XML doc (including the exception
contract): `KeyNotFoundException` for absent/soft-deleted items, `UnauthorizedAccessException` for standing deny, `InvalidOperationException` for F1 "already checked out" on `CheckOutAsync`.

**(b) `EditItemRequest`** (`src/Kumunita.Core/Inventory/InventoryRequests.cs`) —
added **after** `CreateItemRequest` (the M5 `UpdateBoardRequest` full-update
shape): `Name` (required), `OwnerKind` (default `"community"`), `Description`,
`ComponentId`. The `EditItemAsync` lane treats it as a full update (no
partial-patch semantics — the M5 precedent).

**(c) `InventoryService`** (`src/Kumunita.Core/Inventory/InventoryService.cs`)
— five write-lane implementations appended after U02's read surface. Each lane:

1. Guards (`ArgumentNullException` / `ArgumentException` on null/blank).
2. Opens its **own** `IDocumentSession` (`_store.OpenSession(new Marten.Services.SessionOptions())` — the M5 self-composed-session convention).
3. Loads the item; `KeyNotFoundException` if `null` or `IsDeleted` (ADR 0024 soft-delete — the flag is read, the row is never deleted).
4. Runs the **pure server-side standing probe** (D5):
   - **create** — any authenticated member (the actor **is** the creator; `AuthorId = actorId`).
   - **edit / delete** — `IsGlobalAdmin(actorRoles) || item.AuthorId == actorId` (creator ∪ GlobalAdmin).
   - **checkout** — `OwnerKind == "private"` ⇒ `isOwner || isAdmin`; else any member who can see it (the broad M5 `ClaimTodoAsync` standing — the read lanes' audience check is the gate, not the standing probe).
   - **checkin** — `isAdmin || isHolder || isCreator` where `isHolder` is `open.BorrowerId == actorId || item.CurrentHolderId == actorId` (current holder ∪ creator ∪ GlobalAdmin).
5. On **deny**: `StoreAuditRow(..., AccessOutcome.Deny)` + `SaveChangesAsync` (C-M16·2 — the Deny row **commits**, not just in-transaction), then `throw UnauthorizedAccessException`. The Deny row is **not** rolled back with the failure — the *audit* is the fact, the *throw* is the signal.
6. On **allow**: mutate the domain → `StoreAuditRow(..., AccessOutcome.Allow)` → `SaveChangesAsync` (C3 — domain write + audit row in one transaction).
7. `CheckOutAsync` wraps `SaveChangesAsync` in a `catch (Exception ex) when (IsUniqueViolation(ex))` — `IsUniqueViolation` walks the `InnerException` chain for an `Npgsql.NpgsqlException` with `SqlState == "23505"`; the loser's commit fails at the **unique partial index** `inv_uidx_item_open` (U01, `Predicate = "data ->> 'CheckedInAt' IS NULL"`) and is rethrown as `InvalidOperationException($"Inventory item '{itemId}' is already checked out.", ex)`. The loser's transaction rolls back atomically (C3) — the item's `CurrentHolderId`, the `InventoryCheckout` record, and the `AccessAudit` row all commit or roll back together.

**F1 witness pin** — the DB is the arbiter (the M9 `convo_uidx_pair` shape), not app code. The `CheckOutAsync` fast-fail `if (item.CurrentHolderId is not null) throw InvalidOperationException(...)` is an optimization (the common single-user double-click), not the arbiter — the index is what makes the concurrent double-check-out atomic.

**Via mapping** (C-M16·4 — no new `AccessVia`): `IsGlobalAdmin(actorRoles)` ⇒ `AccessVia.Admin`; all other branches (creator/owner/holder/broad) ⇒ `AccessVia.Owner` (the M5 `AuditViaFor` least-distortion mapping). `TargetKind = "inventory"` on every row; action strings are `inventory.create` / `inventory.update` / `inventory.delete` / `inventory.checkout` / `inventory.checkin`.

**(d) The pinned tests** (`tests/Kumunita.Core.Tests/InventoryServiceTests.cs`, 4 tests — all pass):

- **U03·1** `U03_AtomicDoubleCheckOut_ExactlyOneWinner` — a public community item; two **concurrent** `CheckOutAsync` calls for `borrower1` / `borrower2`; exactly **one** succeeds and exactly **one** throws `InvalidOperationException` ("already checked out"); `OpenCheckouts(store, "u03-a-item")` returns **exactly one** open record (the loser's rolled back, C3); exactly **one** Allow `inventory.checkout` audit row committed (the loser's rolled back, C-M16·2 / C3).
- **U03·2** `U03_PrivateStandingDeny_Then_AppendOnlyHistory` — a private item (owner author, `Audience(GrantKind.User, owner)`); a stranger's `CheckOutAsync` → `UnauthorizedAccessException` + **exactly one** Deny `inventory.checkout` row for the item (C-M16·2 — Allow **and** Deny); `OpenCheckouts` returns empty (no open record from the denied attempt). The owner then checks out + checks in; `checkout.Id == checkin.Id` (the same single record, closed — `CheckInAsync` re-loads it, so `Assert.Same` would be wrong — use `Assert.Equal(checkout.Id, checkin.Id)`); `checkin.CheckedInAt` is non-null; `AllCheckouts` returns **exactly one** record (append-only, C-M16·3); `item.CurrentHolderId` is null after check-in (D4).
- **U03·3** `U03_CreateLane_AuthorIsCreator_Audited` — `CreateItemAsync(author, MemberRoles, new CreateItemRequest { Name = "Garden hose", OwnerKind = "shared" })`; `AuthorId == author` (D5 — the actor **is** the creator); `OwnerKind == "shared"`; `Audience` null (public default); exactly **one** Allow `inventory.create` row, `TargetKind = "inventory"`, `AccessVia.Owner`.
- **U03·4** `U03_EditDeleteStandingProbe_NonCreatorDenied_DenyRowCommits` — a community item authored by `creator`; a stranger's `EditItemAsync` → `UnauthorizedAccessException` + a single Deny `inventory.update` row, item's `Name` unchanged; the creator's `EditItemAsync` → Allow row, `Name` updated; a stranger's `DeleteItemAsync` → Deny `inventory.delete` row, `IsDeleted` still `false` (ADR 0024 flag untouched); the creator's `DeleteItemAsync` → `IsDeleted` `true` + Allow row (the append-only history survives the flag).

**(e) Drift observed** — none. The five write lanes are pure append-only to the U02 seam (M4 full-interface-first pin honored — U02's read methods are untouched, no rename). No new `AccessAction`, `AccessVia`, `Decide()` branch, or method on `IAuthorizationService` (C-M16·4 honored — the frozen seam is untouched; the standing probe composes the frozen service's `CanAsync(Read)` "can see it" decision + a `Roles.GlobalAdmin` probe, not a new branch).

**(f) The exit gate** (green): `dotnet build Kumunita.slnx -c Debug` (0 warnings, 0 errors)
+ `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
— **1020 tests, 0 errors, 0 failed, 0 skipped** (includes the 4 new U03 tests;
`Kumunita.Web.Tests` not re-run here — the U03 surface is Core-only).

**(g) Follow-ons** for the U04+ lanes (the Web controllers that call these write
lanes):

1. **Web `[Authorize]` is a convenience pre-gate only** (the design doc's standing probe is the **authoritative** decision — the Web layer's `[Authorize(Roles = ...)]` is a UX shortcut, not the gate). A Web controller that pre-gates on `Roles.Member` is fine for a `community` item, but must **not** pre-gate on `Roles.GlobalAdmin` alone (a creator on a private item would be denied by the pre-gate, but the standing probe would allow — the pre-gate must be the **union** of the standing, or omitted entirely).
2. **The F1 loser sees `InvalidOperationException`** ("already checked out") — the Web layer should surface this as a **409 Conflict** (not a 403, not a 404). The loser's transaction rolled back atomically (C3) — the item's `CurrentHolderId`, the `InventoryCheckout` record, and the `AccessAudit` row all committed or rolled back together; no cleanup is needed.
3. **The check-in's `CurrentHolderId` clear + `CheckedInAt` set commit together** (F1, C-M16·3) — a `CheckInAsync` on an item with no open record throws `KeyNotFoundException` (the open-record load is `KeyNotFoundException`-guarded, not the standing probe). The Web layer should surface this as a **404** (no open checkout to close), not a 403.
4. **The `Note` field** on `CheckOutAsync` (optional free-text) is **stored on the record** (the `InventoryCheckout.Note`), not on the item (the item has no `Note` field — the note is per-checkout, the item is the aggregate). A Web form for checkout should surface the `Note` as an optional field; a Web form for check-in should **not** (the check-in has no note — the note is on the checkout).

## U04 — Web surface part 1 (controller + list/detail/create + kw-l)

**(a) The controller** (`src/Kumunita.Web/Controllers/InventoryController.cs`) —
a thin `[Authorize]` HTTP layer (ADR 0006-D: routes + authz + shape). Ctor
`(IInventoryService inventory, IUserInfoService userInfo)`. `[Authorize]`
is a **convenience pre-gate only** (D5/C-M16·5) — the **single decision
path** is the seam's read lanes (which route every read through the frozen
`IAuthorizationService` over the U01 adapter). The controller never
re-derives access — the M5 `ProjectsController` precedent.

- **`List(ownerKind, componentId, page)`** → GET `/inventory` — calls
  `ListItemsAsync(ownerKind, componentId, actorId, page)`; maps `ItemPage`
  to `InventoryListViewModel` (rows = `InventoryItemRow`; `Pager` =
  `PagedViewModel.ForRoute("/inventory", ...)` — **null** on the single
  page, so `_Pager` renders nothing — the F2 no-render pin); the
  `CurrentOwnerKind` / `CurrentComponentId` / `CurrentPage` are echoed so
  the filter form + pager re-link correctly.
- **`Detail(id)`** → GET `/inventory/{id}` — calls `GetItemAsync(id,
  actorId)`; on `KeyNotFoundException` (absent **or** not visible) returns
  **`NotFound`** — a non-visible item is a **404, never a 403** (C-M3·4
  non-leaky — the seam throws `KeyNotFoundException` for both cases; the
  controller does not distinguish). Loads history via `GetHistoryAsync`;
  `AuthorName` / `CurrentHolderName` via `IUserInfoService.GetProfileAsync`
  (missing profile row → the raw id, a valid shape); `CreatorIsActor` =
  `item.AuthorId == actorId`; `CanCheckOut` = the item is **not** currently
  held (`CurrentHolderId` null) — a read-side hint, **not** a gate (the
  check-out lane's standing probe is the gate).
- **`CreateGet()`** → GET `/inventory/new` — seeds the `InventoryEditorModel`
  with the enabled components (`GetComponentsAsync(true)` → `(Id, Name)`
  tuples; empty list when the instance has none — a valid shape).
- **`CreatePost([FromForm] InventoryEditorModel)`** → POST `/inventory` —
  on invalid model: re-renders `Create` with the model (field errors
  surface via the `kw-l` fallback labels — no model-state plumbing needed
  for this lane; the form is a single POST). On valid: `CreateItemAsync`
  (the actor **is** the creator, D5) → **redirect** to
  `/inventory/{item.Id}`.

**Subject extraction** — `private static string? SubjectId(ClaimsPrincipal
user)` = `user.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject).Value`
(the `Kumunita.Sub` thin-principal claim, not the ASP.NET default subject).

**(b) The view-models** (`src/Kumunita.Web/Models/InventoryViewModels.cs`):

- `InventoryListViewModel(Items, Components, CurrentOwnerKind,
  CurrentComponentId, CurrentPage, Pager)` — `Items` is
  `IReadOnlyList<InventoryItemRow>`; `Pager` is `PagedViewModel?` (null =
  single page).
- `InventoryItemRow(Id, Name, OwnerKind, CurrentHolderId,
  CurrentHolderName, Created)`.
- `InventoryDetailViewModel(Item, History, AuthorName,
  CurrentHolderName, CreatorIsActor, CanCheckOut)` — `History` is
  `IReadOnlyList<InventoryCheckout>` (newest-first, as returned by the
  seam).
- `InventoryEditorModel` — `Name` / `OwnerKind` (default `"community"`) /
  `Description` / `ComponentId` / `Components` (an
  `IReadOnlyList<(string Id, string Name)>`, **not** a `List<>` — the
  controller's helper returns `IReadOnlyList<>`); `IsValid` =
  non-whitespace `Name` **and** `OwnerKind` ∈ {`shared`, `community`,
  `private`}.

**(c) The three views** (`src/Kumunita.Web/Views/Inventory/`):

- **`List.cshtml`** — a `@functions { static string OwnerKindKey(string) ... }`
  dynamic-key idiom (the M5 `StatusLabelKey` shape): maps `shared` →
  `inv.list.ownerKind.shared`, `community` → `.community`, `private` →
  `.private`, unknown → the raw value. The ownerKind filter is a
  `<select>` (All + the three kinds), the `componentId` filter an optional
  text input; both re-POST as GET query params. Rows render
  `Name` / `OwnerKind` (via the kw-l key) / `CurrentHolderName` (or the
  "not held" fallback) / `Created` (via `<kw-dt dt="..." format="g">`).
  Empty list → the `inv.list.empty` kw-l row. Pager: `<partial
  name="_Pager" model="Model.Pager" />` (renders nothing when null — F2).
- **`Detail.cshtml`** — `@using Kumunita.Web.Security`; the item's `Name` /
  `OwnerKind` / `AuthorName` / `Created` / `Description` (rendered Markdown
  via `MarkdownRenderer.RenderHtml`, the ADR 0025 shape) + a
  **`inv.detail.history`** section (F3 — the append-only
  `InventoryCheckout` rows, newest-first, each showing borrower / note /
  checked-out-at / checked-in-at). The `inv.detail.edit` /
  `inv.detail.delete` / `inv.detail.checkOut` / `inv.detail.checkIn` links
  are the **U05** surface (they render as kw-l links now but route to the
  U05 actions — the U05 unit wires the actual POST routes + standing).
- **`Create.cshtml`** — a `<form method="post" asp-action="CreatePost">`
  with `Name` (text) / `OwnerKind` (`<select>` of the three kinds, default
  `community`) / `Description` (textarea) / `ComponentId` (`<select>` of
  `Model.Components` — empty when the instance has none) +
  `@Html.AntiForgeryToken()`. The back-link uses `common.cancel` (not a
  new `inv.create.cancel` key — the common key is the precedent).

**(d) The kw-l keys** — **22** `inv.*` keys registered in **all four**
languages (`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`,
appended after the M15 `translations.bulk.*` keys in each dict —
`EnValues` / `DeValues` / `FrValues` / `DaValues`): `inv.nav`,
`inv.list.title`, `inv.list.empty`, `inv.list.ownerKind.shared`,
`inv.list.ownerKind.community`, `inv.list.ownerKind.private`,
`inv.list.filter`, `inv.create.title`, `inv.create.name`,
`inv.create.ownerKind`, `inv.create.description`, `inv.create.component`,
`inv.create.submit`, `inv.detail.title`, `inv.detail.currentHolder`,
`inv.detail.history`, `inv.detail.edit`, `inv.detail.delete`,
`inv.detail.checkOut`, `inv.detail.checkIn`, `inv.edit.title`,
`inv.edit.submit`. **Drift noted**: the design doc's prose says
"twenty" `inv.*` keys but its **table** lists **22** — the table is the
source of truth, so all **22** are registered (88 total `inv.*` lines =
22 × 4). `KwLRegistryConsistencyTests` (auto-scans every view for static
`kw-l` keys) and the Core 4-language parity tests both pin this — both
pass.

**(e) The three pinned Web tests**
(`tests/Kumunita.Web.Tests/InventoryControllerTests.cs`, 3 `[Fact]`
tests — all pass):

- **U04·1** `List_Renders_With_OwnerKind_Filter_And_Paged_Markup` — a
  visible item; `List(ownerKind: "community", ...)` returns `OkObjectResult`
  whose view-model has the item's `Name`, `CurrentOwnerKind = "community"`,
  and a **null** `Pager` (single page — the F2 no-render pin).
- **U04·2** `Detail_404_Not_403_For_NonVisible_Item` — the seam's
  `GetItemAsync` throws `KeyNotFoundException` (the non-visible item's
  shape); the controller returns `NotFoundObjectResult` — **404, never
  403** (C-M3·4 non-leaky).
- **U04·3** `Create_Flow_Valid_Item_Appears_In_List` — a valid
  `InventoryEditorModel`; `CreatePost` returns a `RedirectToActionResult`
  to `/inventory/{id}`; the seam's `CreateItemAsync` is called with the
  actor as `AuthorId` (D5) and the model's `Name` / `OwnerKind` /
  `Description` / `ComponentId` (asserted via NSubstitute's
  `Arg.Do<CreateItemRequest>` capture).

**Test harness** — pure NSubstitute (no Testcontainers): `Build(inventory,
userInfo, subjectId, roles)` wires a `DefaultHttpContext` + a
`ClaimsPrincipal` with the `Kumunita.Sub` / `Kumunita.Role` claims + a
`TempDataDictionary` over a `NoOpTempDataProvider`. The seam's
`GetProfileAsync` / `GetComponentsAsync` default to an empty candidate
set (a valid shape). **Note**: the test file must reference
`Kumunita.Core.Identity.ClaimTypes` **fully qualified** — the unqualified
name is ambiguous with `System.Security.Claims.ClaimTypes` (the .NET
`Claim` / `ClaimsPrincipal` types are imported for the principal wiring).

**(f) Drift observed** — two minor: (1) the controller initially omitted
`using System.Security.Claims;` (build error, fixed); (2) the
`InventoryEditorModel.Components` property was typed `List<>` but the
controller's helper returns `IReadOnlyList<>` (build error, fixed — the
property is now `IReadOnlyList<(string Id, string Name)>`). No seam drift:
the frozen `IAuthorizationService` / `AccessAction` / `AccessVia` /
`Decide()` / `IUserInfoService` are untouched (C-M16·4 honored — the
controller routes every read through the seam, never re-derives). No files
outside the 7 U04 deliverables were modified.

**(g) The exit gate** (green): `dotnet build Kumunita.slnx -c Debug`
(0 warnings, 0 errors)
+ `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
— **630 tests, 0 errors, 0 failed, 0 skipped** (includes the 3 new U04
tests + `KwLRegistryConsistencyTests` + the Core 4-language parity tests,
which all auto-pin the new 22 `inv.*` keys).

**(h) Follow-ons** for the U05 lanes (the edit/delete/check-out/check-in
Web surface + the nav entry):

1. **The Detail view's action links** (`inv.detail.edit` /
   `inv.detail.delete` / `inv.detail.checkOut` / `inv.detail.checkIn`)
   currently render as kw-l links — U05 wires the actual **POST** routes
   (the write lanes are all POST — the M5 precedent) + the standing
   probes (D5: edit/delete = creator ∪ GlobalAdmin; check-out = any member
   who can see it for `community`/`shared`, owner ∪ GlobalAdmin for
   `private`; check-in = current holder ∪ creator ∪ GlobalAdmin).
2. **The F1 loser sees `InvalidOperationException`** ("already checked
   out") — U05 should surface this as a **409 Conflict** (not a 403, not a
   404) — the U03 follow-on #2.
3. **The `Note` field** on the check-out form (optional free-text, stored
   on the `InventoryCheckout` record, not the item) — the U03 follow-on
   #4. The check-in form has **no** `Note` field.
4. **The nav entry** (`inv.nav` key, already registered) — the home-page
   nav link to `/inventory` — a standing core surface (D6, no admin
   toggle). The U05 unit adds the nav entry + wires the edit/delete/
   check-out/check-in routes + the standing probes.
5. **The `Detail` view's `CreatorIsActor` / `CanCheckOut` flags** are
   read-side hints (rendering convenience) — the **gate** is always the
   seam's standing probe (D5) — the U05 POST lanes. The `Detail` view's
   action links should be conditionally rendered (e.g. hide "Edit" when
   `!CreatorIsActor` and the actor is not GlobalAdmin — the read-side
   hint) but the **server-side** standing probe is the authority.

## U05 — Web surface part 2 (edit/delete + check-out/check-in + nav)

**(a) The controller extension** (`src/Kumunita.Web/Controllers/InventoryController.cs`)
— five new actions appended **after** U04's `CreatePost` (the M4
full-interface-first pin honored — U04's `List` / `Detail` / `CreateGet` /
`CreatePost` are untouched, no rename). Every action is a **thin** HTTP
layer (ADR 0006-D: routes + authz + shape) that routes **only** to the
frozen `IInventoryService` seam — the controller **never re-derives
access** (D5 / C-M16·5 / F4 — the `[Authorize]` convenience pre-gate is the
class-level one, not per-action; the standing probe is the seam's). A
`using Microsoft.AspNetCore.Http;` was added (for `StatusCodes.Status409Conflict`).

- **`EditGet(id)`** → GET `/inventory/{id}/edit` — loads the item via
  `GetItemAsync` (the C-M3·4 non-leaky 404 gate), seeds an
  `InventoryEditorModel` from the live item, sets `ViewData["itemId"]` (the
  M5 `BoardEdit` idiom — the id is not a form field), renders `Edit`.
- **`EditPost(id, [FromForm] InventoryEditorModel)`** → POST
  `/inventory/{id}` — validates `IsValid`, posts an `EditItemRequest`
  (four locked fields: `Name` / `OwnerKind` / `Description` /
  `ComponentId` — blank → `null`), calls `EditItemAsync(id, actorId,
  KumunitaPrincipal.RoleSet(User), request)`. `KeyNotFoundException` →
  **404**, `UnauthorizedAccessException` → **403** (the C3 split); success
  → `TempData["info"]` + `Redirect("/inventory/{id}")`.
- **`DeletePost(id)`** → POST `/inventory/{id}/delete` — calls
  `DeleteItemAsync` (the ADR 0024 soft-delete flag; the append-only
  history survives); `KeyNotFoundException` → 404,
  `UnauthorizedAccessException` → 403; success → `Redirect("/inventory")`
  (the feed — the item is no longer listed).
- **`CheckOutPost(id, [FromForm] string? note = null)`** → POST
  `/inventory/{id}/checkout` — calls `CheckOutAsync(id, actorId, roles,
  blank→null note)`. **The F1 loser's `InvalidOperationException`
  ("already checked out") → a `409 Conflict` `ObjectResult`** (the U03
  follow-on #2 — never 403/404; the loser's commit rolled back atomically
  at the unique partial index). `KeyNotFoundException` → 404,
  `UnauthorizedAccessException` → 403; success → `Redirect("/inventory/{id}")`.
- **`CheckInPost(id)`** → POST `/inventory/{id}/checkin` — calls
  `CheckInAsync(id, actorId, roles)`. `KeyNotFoundException` (absent item
  **or no open checkout to close** — the U03 follow-on #3) → **404** (not a
  403); `UnauthorizedAccessException` → 403; success →
  `Redirect("/inventory/{id}")`.

**(b) The edit view** (`src/Kumunita.Web/Views/Inventory/Edit.cshtml`) —
the M5 `BoardEdit` / U04 `Create.cshtml` shape: a `<form method="post"
action="@($"/inventory/{itemId}")">` with `Name` (text) / `OwnerKind`
(`<select>` of the three D3 labels) / `Description` (textarea) /
`ComponentId` (`<select>` of `Model.Components`, rendered only when non-
empty) + `@Html.AntiForgeryToken()`. Reuses U04's registered `inv.create.*`
field-label keys + `inv.edit.title` / `inv.edit.submit` (the §kw-l list —
**no new keys**; the parity + `KwLRegistryConsistencyTests` pins are
untouched). `ViewData["itemId"]` is read in the view (`?? throw`).

**(c) The detail view extension** (`src/Kumunita.Web/Views/Inventory/Detail.cshtml`)
— an **action bar** inserted between the item `<dl>` card and the F3
usage-history section. The top block now computes
`var canManage = Model.CreatorIsActor || KumunitaPrincipal.IsGlobalAdmin(User)`
(a *display* hint — D5 / C-M16·5 — the server-side standing probe is the
source of truth). The "currently with" display was **already** in U04's
`<dl>` (the `inv.detail.currentHolder` row, conditional on
`CurrentHolderId` non-null) — U05 does not re-add it. The bar renders:

- a **check-out** form (only when `Model.CanCheckOut`) — the optional
  `Note` input (per-checkout, the U03 follow-on #4) + a `POST
  /inventory/{id}/checkout` submit (`inv.detail.checkOut`);
- a **check-in** form (only when `CurrentHolderId` is non-null) — a `POST
  /inventory/{id}/checkin` submit (`inv.detail.checkIn`);
- the **Edit** link (`/inventory/{id}/edit`, `inv.detail.edit`) + the
  **Delete** button (`POST /inventory/{id}/delete`, `inv.detail.delete`,
  `data-confirm`) — both only when `canManage`.

All four action keys are U04's registered `inv.detail.*` keys (the §kw-l
list — **no new keys**; the view only uses them, never re-registers).

**(d) The nav entry** (`src/Kumunita.Web/Views/Shared/_Layout.cshtml`) —
the M5 `nav.projects` shape in **both** variants:

- **Variant B** (top row) — a `<li>` in the "More ▾" dropdown, after the
  Projects item: `<a href="/inventory"><kw-l key="inv.nav">Inventory</kw-l></a>`.
- **Variant C** (icon rail) — a `<a class="kmb-rail-btn" href="/inventory">`
  with a box/box-lid SVG + the `visually-hidden` + `kmb-rail-tip` `inv.nav`
  spans, after the Projects rail button.

The `inv.nav` key is U04's registered key (the §kw-l list — **no new
key**). A standing core surface (D6, no admin toggle) — the entry is
**not** wrapped in the `@if (User.Identity?.IsAuthenticated == true)`
resident-only guard that the adjacent Community/Groups/Events/Projects
entries carry; it is a flat entry (like Home + Announcements), matching D6's
"always available to the community."

**(e) The pinned Web tests** (`tests/Kumunita.Web.Tests/InventoryControllerTests.cs`,
5 new `[Fact]` tests — all pass; a `RepoRoot` helper was added to the
class, mirroring the `NavMoreFoldTests` / `PwaManifestTests` structural-pin
idiom):

- **U05·4a** `CheckOut_Valid_Sets_CurrentHolder_And_Appends_Record` — a
  valid check-out → `RedirectResult` to `/inventory/{id}`; the seam's
  `CheckOutAsync` is called with the actor as the borrower + the `note`
  (asserted via `Received(1)`); a denied actor (seam's
  `UnauthorizedAccessException`) → clean `ForbidResult` (403); a missing
  id (seam's `KeyNotFoundException`) → clean `NotFoundResult` (404) — the
  C3 split.
- **U05·4b** `CheckOut_AlreadyCheckedOut_Is_409_Not_403_404` — the F1
  loser's `InvalidOperationException` → an `ObjectResult` with
  `StatusCode == 409` (never 403/404 — the U03 follow-on #2), body
  contains "already checked out" (case-insensitive).
- **U05·5** `CheckIn_Valid_Clears_CurrentHolder_And_Closes_Record` — a
  valid check-in → `RedirectResult` to `/inventory/{id}`; the seam's
  `CheckInAsync` is called with the actor; no open record / absent (seam's
  `KeyNotFoundException`) → clean `NotFoundResult` (404 — the U03
  follow-on #3, NOT a 403); a denied actor (seam's
  `UnauthorizedAccessException`) → clean `ForbidResult` (403).
- **U05·6** `Edit_And_Delete_Valid_Route_To_Seam_And_Redirect` — edit:
  valid form → `RedirectResult` to `/inventory/{id}`, the seam's
  `EditItemAsync` is called with the actor + the four locked fields
  (asserted via the `Arg.Do`-style capture — `capturedEdit`'s
  `Name`/`OwnerKind`/`Description`/`ComponentId`); delete: →
  `RedirectResult` to `/inventory` (the feed), the seam's
  `DeleteItemAsync` is called with the actor. Both lanes: a denied actor →
  `ForbidResult` (403), a missing id → `NotFoundResult` (404) — the C3
  split on each.
- **U05·7** `Nav_Entry_Present_In_Both_Layout_Variants_And_Registry` —
  the `KnownTranslationKeys` `inv.nav` key is present in **all four**
  languages (the §kw-l parity pin — U04's registration; U05 consumes,
  never re-registers), and `_Layout.cshtml` carries `href="/inventory"` +
  `key="inv.nav"` in **both** nav variants (B dropdown + C rail — ≥ 2
  `inv.nav` occurrences), alongside the mirrored M5 `nav.projects` entry
  (the structural-pin idiom — a pure file read, no TestServer).

**(f) Drift observed** — none. The five actions are pure append-only to
U04's controller (no rename/re-scope of `List` / `Detail` / `CreateGet` /
`CreatePost`). No files outside the U05 deliverables were modified: the
frozen `IAuthorizationService` / `AccessAction` / `AccessVia` / `Decide()`
/ `IUserInfoService` are untouched (C-M16·4 honored — the controller routes
every write through the seam's standing probe, never re-derives). No new
`inv.*` kw-l keys (all five action labels + the nav label are U04's
registered keys — the parity + `KwLRegistryConsistencyTests` pins extend
automatically and pass). No new bounded context, no new `AccessVia` value,
no `INotificationService` (D8). The `NavMoreFoldTests` structural pins are
unaffected (the new `inv.nav` dropdown item adds no `data-nav-fold` /
`data-nav-more` hooks — the pin counts remain 2 + 1).

**(g) The exit gate** (green): `dotnet build Kumunita.slnx -c Debug`
(0 errors) + `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` —
**635 tests, 0 errors, 0 failed, 0 skipped** (630 at U04 + the 5 new U05
tests; `KwLRegistryConsistencyTests` + the Core 4-language parity tests
auto-pin the keys and pass; `NavMoreFoldTests` unaffected).

**(h) Follow-on for U06 (the close):** none — U05's surface is complete
(the four write-lane actions + the edit view + the action bar + the nav
entry + the 5 pinned tests). U06 is the close: the three acceptance gate
tests (the U00 §gate — closed-loop / handoff-authorization-boundary /
part-vs-whole-audit-completeness, the locked names in U00(b)) + the D7
docs-parity flip (the four surfaces — `Milestones.cs` M16→done / M17→next,
`MilestonesTests.cs` re-pin to `M17_Is_The_Single_InProgress_Milestone` +
shipped list + M18 planned, the README Roadmap/Status, `docs/STATUS.md`,
and the `docs/ARCHITECTURE.md` value-chain M16 row) — **all in U06's one
unit** (C-M16·7). **U06 is the close — it has its own plan.**

---

## U06 — close (three gate tests + D7 docs-parity flip)

**Role:** the close. U00–U05 are done and committed; this unit ships the
M16 milestone. There is no U07.

**(a) The three acceptance gate tests** (the U00 §gate — locked names, a
rename / re-scope would be a drift event) added to
`tests/Kumunita.Web.Tests/M16AcceptanceGateTests.cs`, all run over the
**full stack** (the real `InventoryController` routes + the real
`InventoryService` over Testcontainers Postgres — not a service-level or
NSubstitute double):

- `M16_Acceptance_ClosedLoop_CheckOutCheckInHistory` — the creator creates
  a **shared** item (controller `CreatePost` → redirect), a second resident
  checks it out (`CheckOutPost`), checks it in (`CheckInPost`); the usage
  history shows **exactly one** closed `InventoryCheckout` (both
  `CheckedOutAt` + `CheckedInAt` set), the item's `CurrentHolderId` is
  cleared, and the `AccessAudit` trail carries create / check-out /
  check-in **Allow** rows (C-M16·3 / F1 / F3 / D4).
- `M16_Acceptance_HandoffAuthorizationBoundary_PrivateAndShared` — a
  **private** item (owner-restricted audience) is hidden from the list
  (aggregate `HiddenCount ≥ 1`, not in the visible set), its detail is a
  **404, not a 403** (C-M3·4 non-leaky), and a stranger's check-out is a
  **403** (`ForbidResult`) with the single **Deny** `inventory.checkout`
  audit row committing; a **shared** item is visible in the list and a
  member's check-out is a **Redirect** + **Allow** row (D5 standing probe —
  *see* ≠ *act*).
- `M16_Acceptance_PartVsWholeAuditCompleteness` — the check-out /
  check-in transition's audit row commits **atomically** with the state
  change (one `SaveChangesAsync`, the F1 witness), the list's aggregate
  row survives a concurrent create, and the **whole** audit trail is
  present (C-M16·2 / C3 / C-M16·3).

The tests boot the full store trio (`UserInfoService` → `AuthorizationService`
→ `InventoryService`) on `M1DocTypes` + `M16DocTypes` over a fresh
`PostgresFixture` database, and drive the **real** `InventoryController`
with a real `ClaimsPrincipal` (`Kumunita.Sub` / `Kumunita.Role`) +
`DefaultHttpContext` — the same path the browser takes.

**(b) The D7 docs-parity flip** (C-M16·7 — the four surfaces flip
**together in this one unit**, no half-flip; M17/M18 are **not** reordered
and the shipped M1–M15 text is **not** edited):

- `src/Kumunita.Web/Milestones.cs` — M16 `StatusNext` → `StatusDone`;
  M17 `StatusPlanned` → `StatusNext` (M18 stays `StatusPlanned`).
- `tests/Kumunita.Web.Tests/MilestonesTests.cs` — `Shipped_Milestones_Are_Marked_Done`
  now includes `"M16"`; the single-in-progress pin renamed
  `M16_Is_The_Single_InProgress_Milestone` →
  `M17_Is_The_Single_InProgress_Milestone` (re-pinned to `"M17"`, the
  planned set drops M17 to leave `M18`). The order pin
  (`… "M15", "M16", "M17", "M18"`) is untouched — no reorder.
- `README.md` — Status block now reads **M17 in progress** … **M1–M16**
  done … **M18 is planned**; Roadmap M16 row → **Done.** (ADR 0117), M17
  row → **In progress.**, M18 stays planned.
- `docs/STATUS.md` — the milestone line flipped: **M16 is done** (ADR 0117)
  … **next is M17** … M18.
- `docs/ARCHITECTURE.md` — the value-chain table gains the
  **M16 inventory** row (`coordination` — the neighborhood's shared /
  community-owned / private things become trackable: check-out / check-in,
  where they are, and who has used them).

**(c) Guardrails honored:** docs + tests only — no new lane, no new code
surface beyond the gate tests. No new bounded context, no new `AccessVia`
value, no `INotificationService` (D8), no new `kw-l` keys, no new ADR
(ADR 0117 was landed by an earlier unit). The frozen `IAuthorizationService`
/ `AccessAction` / `AccessVia` / `Decide()` / `IUserInfoService` seams are
untouched (C-M16·4).

**(d) The exit gate** (green): `dotnet build Kumunita.slnx -c Debug`
(0 errors) + both test assemblies in-process:
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
— **1020 tests, 0 errors, 0 failed, 0 skipped**; and `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` —
**638 tests, 0 errors, 0 failed, 0 skipped** (635 at U05 + the 3 new U06
gate tests; `MilestonesTests` re-pins to M17 and passes).

**(e) M16 milestone is shipped; handoff notes complete.** U06 is the close
— this is the last unit of M16. `m16-u06.md` moves to `done/`.
