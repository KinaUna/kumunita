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
