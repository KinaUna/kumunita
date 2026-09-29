# M16 — Inventory (check-out / check-in + usage history) — design

> **Milestone M16.** The neighborhood's **shared, community-owned, or
> private things** (equipment, sports-team clothes, books, …) become
> **trackable**: an item a community can share, a **check-out / check-in
> transition** that says who it is currently with, and an **append-only
> usage history** — "track where items are, and optionally how much they
> are used by whom," the README roadmap row names verbatim.
>
> **A standalone core surface** (a roadmap letter, not a named lane): one
> **new bounded context** `Kumunita.Core.Inventory` with **two
> documents** (`InventoryItem`, `InventoryCheckout`) on a **new
> `M16DocTypes`** surface (the M9 `M9DocTypes` / M5 `M5DocTypes`
> precedent — additive, idempotent, **zero migrations** for existing
> surfaces), one **adapter** onto the **frozen**
> `IAuthorizationService` (the exact M5 `ProjectToAuditableResource`
> 6-member projection — an *adapter*, not a *branch*), and the **`inv.*`**
> `kw-l` registry block (the M5 `projects.*` key shape — §kw-l below).
> **Zero new authorization surface** (C-M16·4): no new `AccessAction`, no
> new `AccessVia`, no branch in `Decide()`, no method on
> `IAuthorizationService`, no `INotificationService` (D8).
>
> **Status.** **LOCKED.** The decisions D1–D8, the invariants
> C-M16·1–7, the FACES F1–F5 + the named trade, the §kw-l key list, and
> the §gate test names are locked in **ADR 0117 (Accepted, 2026-09-29)**.
> The `[PROPOSED]` set in the register
> `docs/plans-milestones/plan-m16-inventory.md` is the locked set this
> doc restates **verbatim** (the veto window closed before U00 ran — the
> U00 handoff entry records the lock).
>
> **The one thing every unit must respect:** M16 **composes only frozen
> seams** — the `IAuthorizationService` + `IUserInfoService` over the
> host-registered Marten `IDocumentStore`. It does **not** touch a new
> `AccessAction`, a new `AccessVia`, a new `Decide()` branch, a new
> translation table, or a notification lane (C-M16·4, D2, D8). Every
> register unit (U01's context + surface + adapter, U02's read lanes,
> U03's write lanes + standing probes, U04/U05's Web surface) enforces
> this at a different seam; the §drift-guard frozen list below is the
> **exact** set that is untouched.

## Context

M5 (Projects) already proved the shape M16 copies: a **standalone core
surface** on its own bounded context (`Kumunita.Core.Projects`), its own
`*DocTypes` Marten surface, one **adapter** onto the frozen
`IAuthorizationService` (the `ProjectToAuditableResource` 6-member
projection — `Id` / `Name` / `OwnerId` / `Audience` / `ComponentId` /
`TargetKind`, the **only** difference being the `TargetKind` string), the
**creator ∪ GlobalAdmin vs. broad-standing** split over its write lanes
(C-M5·6), and a `projects.*` `kw-l` block. The M9 (`M9DocTypes`)
precedent adds the **unique-index-as-witness** idiom — the
`convo_uidx_pair` partial-ish unique index is the F1 idempotency witness,
making a concurrent double-open of a conversation fail at the DB layer
instead of in app code. M16 composes **both** precedents over the **same
frozen seams**:

1. **The frozen `IAuthorizationService`** — `CanAsync(Read)` (detail) /
   `CanSeeAsync(Read)` (list) with the `IDocumentSession` overloads (C3 —
   the audit row commits **in the same transaction** as the domain
   write), over an `IAuditableResource` (ADR 0006 §A). The frozen surface
   is 4-method + the group lane; the frozen `AccessAction` set is
   `Read` / `Moderate`; the frozen `AccessVia` set is Owner / Admin
   (+ the existing values). **M16 adds none of these — it adds an
   adapter** (C-M16·1 / C-M16·4).
2. **The M5 `ProjectToAuditableResource` shape**
   (`src/Kumunita.Core/Projects/ProjectToAuditableResource.cs`) — the
   exact 6-member projection the `InventoryItemToAuditableResource`
   mirrors (D2): `Id = Item.Id`; `Name = Item.Name`; `OwnerId =
   Item.AuthorId` (the owner branch of `Decide()`); `Audience =
   Item.Audience` (the **exact** `Post` `Audience`, ADR 0001-B —
   `null` = public — projected verbatim, never mutated); `ComponentId =
   Item.ComponentId` (a feed filter, **never** an access boundary —
   C-M3·2); `TargetKind = "inventory"` (the **exact** string — the
   `AccessAudit.TargetKind` discriminator for this line of decisions,
   C3).
3. **The `*DocTypes` parallel-surface shape** (`src/Kumunita.Core/M9DocTypes.cs`)
   — `opts.Schema.For<T>()` + named indexes; `ApplyAllConfiguredChangesTo
   DatabaseAsync()` delta-detects and applies the new tables
   **idempotently** at boot (ADR 0004 §B.1) — **zero migrations for
   existing surfaces**. The M9 `convo_uidx_pair` (`UniqueIndex("convo_uidx_pair", c => c.ParticipantA, c => c.ParticipantB)`)
   is the **F1 idempotency witness** the `M16DocTypes` open-checkout
   unique partial index copies (the witness of at-most-one-open-checkout).
4. **The M5 standing split** (C-M5·6 / ADR 0086) — the write lanes'
   **creator ∪ GlobalAdmin** standing probe (the `ProjectService`
   standing-probe shape — compose the frozen "can see it" +
   `IUserInfoService`'s GlobalAdmin probe **server-side**) and the **broad
   standing** precedent (the `ClaimTodoAsync` "any member who can see it"
   shape, ADR 0106) — D5's two standing classes.
5. **The M5 `projects.*` `kw-l` block in `KnownTranslationKeys`** (the
   closed-key registry, ADR 0015) — the `inv.*` block it joins (the
   `KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests`
   pins extend automatically — §kw-l).
6. **The ADR 0024 soft-delete flag** (`IsDeleted`) — the item's delete
   lane's shape (a flag flip, never a row delete — the checkout records
   are the append-only history, C-M16·3).
7. **The ADR 0018 authored-in `LanguageCode` tag** — the item's
   display-tag shape (single-language in M16 — the translation lane is
   §deferred).

The gap M16 closes is the README row itself: the neighborhood today has
a place for **tasks** (M5 to-dos) and **events** (M4), but no place to
say *the ladder is with Anna*, *the club jersey is at home*, or *who has
used the projector this season*. M16 adds that surface: two documents,
one new surface, one adapter, read + write lanes, a Web face (list /
detail / create / edit / delete + the check-out/check-in actions + the
usage-history section + the nav entry), twenty `inv.*` `kw-l` keys — and
**no** new authorization surface, **no** notification lane, **no**
media, **no** recurrence (§deferred).

M16 builds entirely on **frozen, verified seams** (the list above). This
is a **milestone** (a roadmap letter, not a named lane): the close unit
(U06) flips `Milestones.cs` / the README Roadmap / `docs/STATUS.md` /
`docs/ARCHITECTURE.md` + `MilestonesTests.cs` (the AGENTS.md doc↔code
parity contract — D7). This design doc (authored U00, **LOCKED**) is the
**primary tier**; the register
`docs/plans-milestones/plan-m16-inventory.md` is the secondary tier; the
scratch handoff note is
`docs/plans-milestones/in-progress/m16-inventory-handoff-notes.md`.

## Goals / Non-goals

**In (shipped by M16):** the `Kumunita.Core/Inventory/` bounded context
— the two documents `InventoryItem` + `InventoryCheckout` (U01), the
`M16DocTypes` surface with the **unique partial index** on `(ItemId)`
where `CheckedInAt IS NULL` (the F1 witness, U01), the
`InventoryItemToAuditableResource` adapter (the exact M5 6-member
projection, `TargetKind = "inventory"`, U01), the read lanes
(`ListItemsAsync` / `GetItemAsync` / `GetHistoryAsync`, U02), the write
lanes (`CreateItemAsync` / `EditItemAsync` / `DeleteItemAsync` /
`CheckOutAsync` / `CheckInAsync`) + the D5 standing probes + the F1
atomic transition (U03), the DI registration (U02), the Web face —
`InventoryController` + view models + list/detail/create views (U04) +
edit/delete + the check-out/check-in action buttons + the usage-history
section + the nav entry (U05), the **twenty `inv.*` `kw-l` keys ×
en/de/fr/da** (U04, §kw-l), and the **three acceptance gate tests**
(U06, §gate) + the **D7 docs parity flip** (U06).

**Out (each a named follow-on lane, ADR 0117 Consequences — own ADRs;
the deferred-lane list is locked in §deferred):** the "nudge the owner
on checkout" notification lane (D8 — the M6 `INotificationService`
emitter idiom); the per-item translation lane (the M5 `TodoTranslation`
shape — the item's `LanguageCode` tag is present, the translation
records are not); the media / attachments-on-items lane (the ADR 0025 /
ADR 0034 shape); the recurring / reserved check-out lane (a pre-booked
slot, a hold); a third "Usage" document or context (C-M16·3 — the history
**is** the checkout records).

## Human cost

This is a **resident** surface, not an operator one — and it spends
**attention it earns back**. The check-out / check-in action is one tap
on the item's detail page (the F1 transition is atomic — a double
check-out resolves to one winner, FACES F1), and it **removes** a
conversation ("do you have the ladder?") from the neighborhood's chat or
group channel. The **usage history** (F3) is the "optionally how much
they are used by whom" the roadmap row names — a read-only aggregate, no
maintenance cost at all (C-M16·3: append-only, never mutated or deleted).
The `private` standing (owner ∪ GlobalAdmin over check-out/check-in —
D5) means a resident's **personal** item (their own jersey, their own
camera) is never lent by accident: the C-M16·5 split keeps *visibility*
always the Audience and *acting* always the standing probe, so a
resident can **see** a shared item and the standing decides who **acts**.
Nothing is taken from the neighborhood's trust: the surface is
audit-by-default (C-M16·2 — every read decision, Allow **and** Deny,
writes exactly one `AccessAudit` row in the same transaction), the
`private` items are invisible to a non-member (404-not-403, the C-M3·4
shape), and the `OwnerKind` label (D3) is a **grouping and standing
breadth** signal, never a read gate. The one real cost is the standing
breadth on `shared` / `community` items (any member who can see it can
check it out — the named trade, §FACES): frictionless lending is bought
with a *standing* decision, not an *audience* one — and the C-M16·5
split is the invariant that keeps the two from leaking into each other.

## Parts affected

- **`Kumunita.Core/Inventory/`** (new context — additive) —
  `InventoryItem.cs` (the item document: `Id` string surrogate, `Name`
  non-empty, `OwnerKind` **string** ∈ `shared`/`community`/`private` —
  the M5 `KanbanStatuses` string-not-enum shape, `Description` optional,
  `ComponentId` (a feed filter, never a gate — C-M3·2), `AuthorId` (the
  standing owner — D5), `Audience` (the exact `Post` `Audience`,
  `null` = public — D2/D3), `CurrentHolderId` nullable (`null` = in the
  pool — D4), `IsDeleted` (ADR 0024), `LanguageCode` (ADR 0018),
  `Created`, `Modified`), `InventoryCheckout.cs` (the append-only usage
  record: `Id`, `ItemId`, `BorrowerId`, `CheckedOutAt`, `CheckedInAt`
  nullable — open when null, `Note` optional),
  `InventoryItemToAuditableResource.cs` (the adapter — the exact M5
  6-member projection, `TargetKind = "inventory"`),
  `IInventoryService.cs` + `InventoryService.cs` (the read lanes U02,
  the write lanes + standing probes U03), `InventoryRequests.cs` (the
  request DTOs — the sealed-record shape).
- **`Kumunita.Core/M16DocTypes.cs`** (new surface) —
  `opts.Schema.For<InventoryItem>()` (the `(ComponentId, Created)` +
  `(OwnerKind, Created)` feed/filter indexes) +
  `opts.Schema.For<InventoryCheckout>()` (the `(ItemId, CheckedOutAt)`
  thread-ordering index + the **unique partial index** on `(ItemId)`
  where `CheckedInAt IS NULL` — the F1 idempotency witness, the M9
  `convo_uidx_pair` shape, U01).
- **`Kumunita.Core/DependencyInjection.cs`** (additive) — the
  `AddTransient<IInventoryService>(sp => new InventoryService(store,
  IUserInfoService, IAuthorizationService))` registration (the M5 / M9
  shape — **no** `INotificationService`, D8, U02).
- **`Kumunita.Web/Program.cs`** — `M16DocTypes.Configure(opts);` added to
  the `*DocTypes.Configure(opts)` block (after
  `M9DocTypes.Configure(opts)`, U01).
- **`Kumunita.Web/Models/InventoryViewModels.cs`** (new — U04) +
  **`Kumunita.Web/Controllers/InventoryController.cs`** (new — U04:
  list / detail / create; U05: edit / delete / check-out / check-in) +
  **`Kumunita.Web/Views/Inventory/`** (`List.cshtml` / `Detail.cshtml` /
  `Create.cshtml` U04; `Edit.cshtml` U05) + the sidebar/nav partial's
  "Inventory" entry (U05).
- **`Kumunita.Core/Localization/KnownTranslationKeys.cs`** — the
  twenty `inv.*` keys in `EnValues` / `DeValues` / `FrValues` /
  `DaValues` (U04 — the parity pins extend automatically, §kw-l).
- **`tests/Kumunita.Core.Tests/`** (U01/U02/U03 pins) +
  **`tests/Kumunita.Web.Tests/`** (U04/U05/U06 pins, the three
  acceptance gate tests in U06).
- **The U06 parity surfaces (D7 — one unit):** `Milestones.cs` (M16
  `StatusNext` → `StatusDone`; M17 `StatusPlanned` → `StatusNext`),
  `MilestonesTests.cs` (re-pinned to
  `M17_Is_The_Single_InProgress_Milestone` + the shipped list gains M16
  + M18 stays planned), the `README.md` Roadmap (M16 → done, M17 → in
  progress) + the M16 **Status** line, `docs/STATUS.md`, and
  `docs/ARCHITECTURE.md` (the value-chain table gains the **M16 row** —
  the table currently ends at the M15 row).

**Unchanged (the frozen surface — the §drift-guard frozen list is the
verbatim set below):** `IAuthorizationService` (the 4-method surface +
the group lane), the `AccessAction` set (`Read` / `Moderate`), the
`AccessVia` set (Owner / Admin + the existing values), `Decide()`,
`IUserInfoService`, the `Post` `Audience` type, the existing
`*DocTypes` surfaces (`M1DocTypes` … `M9DocTypes`), the
`KnownTranslationKeys` existing blocks, the `TranslationResource` store,
and every existing controller / view / view-model.

## Seams & contracts (mandatory)

**Created** (all new, all in `Kumunita.Core/Inventory/` unless noted):

- **`InventoryItem` + `InventoryCheckout`** (D1, U01) — the two
  documents, POCOs with the conventional `string` `Id` (the M9/M5
  convention). The `InventoryItem` field set and the
  `InventoryCheckout` field set are the **locked** lists in §Parts
  affected — a unit that adds, drops, or re-types a field is a drift
  event (C-M16·3 pins the append-only record shape).

- **`M16DocTypes`** (D1/D4, U01) — the parallel surface
  (`src/Kumunita.Core/M16DocTypes.cs`, the exact `M9DocTypes` shape):

  ```csharp
  public static class M16DocTypes
  {
      public static void Configure(StoreOptions opts)
      {
          // InventoryItem — (ComponentId, Created) + (OwnerKind, Created)
          // feed/filter indexes (a filter, never a gate — C-M3·2 / C-M16·5).
          opts.Schema.For<InventoryItem>()
                 .Index(i => new { i.ComponentId, i.Created })
                 .Index(i => new { i.OwnerKind, i.Created });

          // InventoryCheckout — the (ItemId, CheckedOutAt)
          // thread-ordering index + the **unique partial index** on
          // (ItemId) where CheckedInAt IS NULL — the F1 idempotency
          // witness (at most one open checkout per item; a concurrent
          // double-check-out's second commit fails at the DB layer, the
          // M9 convo_uidx_pair shape).
          opts.Schema.For<InventoryCheckout>()
                 .Index(c => new { c.ItemId, c.CheckedOutAt });
          // + the unique partial index on (ItemId) where CheckedInAt IS NULL.
      }
  }
  ```

  Registered in `Program.cs` (`M16DocTypes.Configure(opts)` after
  `M9DocTypes.Configure(opts)`). Delta-detected + applied idempotently
  at boot — **zero migrations for existing surfaces** (ADR 0004 §B.1).

- **`InventoryItemToAuditableResource`** (D2, U01) — the **exact** M5
  `ProjectToAuditableResource` 6-member projection:

  ```csharp
  public sealed class InventoryItemToAuditableResource : IAuditableResource
  {
      public InventoryItemToAuditableResource(InventoryItem item) => Item = item;
      public InventoryItem Item { get; }
      public string Id => Item.Id;                      // the document identity
      public string Name => Item.Name;                  // non-empty by pin
      public string? OwnerId => Item.AuthorId;          // the Decide() owner branch
      public Audience? Audience => Item.Audience;       // exact Post Audience, null = public, verbatim
      public string? ComponentId => Item.ComponentId;   // a feed filter, never a gate (C-M3·2)
      public string TargetKind => "inventory";          // the exact string (the C3 discriminator)
  }
  ```

  **The only** new authorization surface for M16 (C-M16·1): it plugs
  into the **frozen** `IAuthorizationService` with **no** signature
  change, **no** new `AccessAction`, **no** new `AccessVia`, **no** new
  branch in `Decide()`. The adapter does not *own* the item (a single
  instance is safe to pass into either overload — `CanAsync` detail /
  `CanSeeAsync` list — each call is a value-level projection). `sealed`
  keeps the surface closed.

- **`IInventoryService` + `InventoryService` — read lanes** (D2/D4,
  U02) — composing `IDocumentStore` + `IUserInfoService` +
  `IAuthorizationService` (the M5 / M9 composition shape):
  `ListItemsAsync` (candidates = `!IsDeleted`, filtered by the optional
  `ownerKind` (a **filter, never a gate** — C-M3·2 / C-M16·5) + the
  optional `componentId`; survivors `CanSeeAsync(Read)`-filtered;
  ordered by `Created` desc; paged; **exactly one** aggregate
  `AccessAudit` row `TargetKind = "inventory"`, `visibleCount` /
  `hiddenCount`, `TargetId` null — the C6 / C-M3·3 shape),
  `GetItemAsync` (`CanAsync(Read)` detail, the **404-vs-403 split** — a
  non-visible item is a 404, never a 403, C-M3·4 — one single-target
  `AccessAudit` row, `TargetId` = the item id), `GetHistoryAsync` (the
  item's `InventoryCheckout` records ordered by `CheckedOutAt` desc —
  F3, the read-only "how much … by whom" surface).

- **`IInventoryService` + `InventoryService` — write lanes** (D4/D5,
  U03) — each takes the caller's `IDocumentSession` (C3 — the audit row
  commits in the **same transaction** as the domain write):
  `CreateItemAsync` (the actor **is** the creator — `AuthorId` = the
  actor; one `AccessAudit` row `TargetKind = "inventory"`, `Via
  Owner/Admin`), `EditItemAsync` (the standing probe: **creator ∪
  GlobalAdmin** — D5), `DeleteItemAsync` (the standing probe: **creator
  ∪ GlobalAdmin**; the ADR 0024 soft-delete flag — the item's checkout
  history survives the flag), `CheckOutAsync` (the standing probe:
  **any member who can see it** — community/shared; **owner ∪
  GlobalAdmin** — private; the **atomic** transition — F1: the
  `CurrentHolderId` flip + the appended `InventoryCheckout` commit in
  **one** `SaveChangesAsync`), `CheckInAsync` (the standing probe: the
  **current holder ∪ creator ∪ GlobalAdmin**; clears `CurrentHolderId`
  + closes the open record — sets `CheckedInAt`). The standing probes
  compose the frozen `IAuthorizationService` "can see it" +
  `IUserInfoService` "GlobalAdmin probe" **server-side**; the Web
  `[Authorize]` attribute is a **convenience pre-gate only** (D5 / F4)
  — never the source of truth.

- **`InventoryRequests.cs`** (U02) — the sealed-record request DTOs
  (the `ProjectRequests` shape): `CreateItemRequest` with `Name` /
  `OwnerKind` / `Description` / `ComponentId` (the read filters are
  method params, not DTOs).

**Depended on** (frozen, unchanged — the §drift-guard list):
`IAuthorizationService` (the `CanAsync` / `CanSeeAsync` + the
`IDocumentSession` overloads — C3) — **never extended**; the
`AccessAction` / `AccessVia` sets — **never grown**; `Decide()` —
**never branched**; `IAuditableResource` (the 6-member projection the
adapter fills); `IUserInfoService` (the GlobalAdmin standing probe); the
`Post` `Audience` type (the `InventoryItem.Audience` is the exact same
type — D2/D3); the `AccessAudit` row shape; the existing `*DocTypes`
surfaces (the M16 surface is **parallel**, never additive on an old one
— D1); `KnownTranslationKeys` (the `inv.*` block **joins** the closed
set — the parity pins extend, never reshape).

**Access model:** M16 **touches no group**, **no delegation**, **no
moderator scope**, and **no** `IAuthorizationService` method (D2 /
C-M16·4). The read decision is **always** the item's `Audience` through
the frozen service (C-M16·1 — `OwnerKind` never an access gate); the
action decision is **always** the D5 standing probe (C-M16·5 — the
visibility/standing split). The **migration path** is the ADR 0004
§B.1 delta detection: `ApplyAllConfiguredChangesToDatabaseAsync()`
applies the two new tables + the indexes idempotently at boot — **zero
migrations for existing surfaces** (D1). **Every** read and write
decision audits (C-M16·2), in the existing `AccessAudit` row shape
(`TargetKind = "inventory"`), **in the same transaction** as the domain
write (C3, via the `IDocumentSession` overloads).

## Feedback loops

**How we know it works** (the pinned tests per unit + the three
acceptance tests, §gate):

- **U01 (Core)** — the `M16DocTypes` surface registers both documents +
  the unique partial index (the M9/M5 doc-registration test shape); the
  adapter's projection (the 6 members, `TargetKind = "inventory"`).
- **U02 (Core)** — (a) the list's **single aggregate** `AccessAudit` row
  (`visibleCount` / `hiddenCount`, `TargetKind = "inventory"`); (b) the
  detail's **404-vs-403 split** (a non-visible item is a 404, not a 403
  — C-M3·4); (c) the history read returns the checkout records ordered
  by `CheckedOutAt` desc (F3).
- **U03 (Core)** — (a) the **atomic double-check-out** (two concurrent
  check-outs of the same item → exactly one wins, the other sees
  "already checked out" — F1 / C-M16·3); (b) the **standing-probe
  deny** (a non-standing member of a private item cannot check it out,
  the deny `AccessAudit` row commits — D5 / C-M16·5); (c) the
  **append-only history** (a check-out + a check-in produces exactly
  one checkout record with `CheckedOutAt` + `CheckedInAt` set —
  C-M16·3); (d) the **create** lane (the actor is the creator,
  `AuthorId` = the actor, one `AccessAudit` row `Via Owner/Admin`); (e)
  the **edit/delete** standing probe (creator ∪ GlobalAdmin; a
  non-creator non-GlobalAdmin is denied, the deny row commits).
- **U04 (Web)** — (a) the list page renders (the `OwnerKind` filter +
  the paged markup); (b) the detail page 404-vs-403 (a non-visible item
  is a 404, not a 403 — C-M3·4); (c) the create flow (a valid create →
  the item appears in the list).
- **U05 (Web)** — (a) the **check-out action** (a valid check-out → the
  item's `CurrentHolderId` is set + the history shows the new record);
  (b) the **check-in action** (a valid check-in → the
  `CurrentHolderId` is cleared + the history record is closed); (c) the
  **nav entry present** (the sidebar has the "Inventory" link, the
  `@localize` `kw-l` tag resolves); (d) the **edit/delete** actions (a
  valid edit → the item's fields are updated; a valid delete → the item
  is soft-deleted + no longer in the list).
- **U06 (Web, 3 acceptance + the parity flip)** — the §gate trio and
  the D7 four-surface flip (C-M16·7).

**Which signals, which thresholds, who watches:** the test suite is the
watcher (the `dotnet exec …dll` runner per AGENTS.md;
`Kumunita.Core.Tests` ~20 s over the Testcontainers `postgres:18` path;
`Kumunita.Web.Tests` is fast). The **F1 witness** is the unique partial
index on `(ItemId)` where `CheckedInAt IS NULL` — a Postgres-level
failure the `CheckOutAsync` lane catches and surfaces as "already
checked out" (a second open checkout is impossible at the DB layer,
C-M16·3). The `KnownTranslationKeys_ParityTests` +
`KwLRegistryConsistencyTests` pins watch the twenty `inv.*` keys across
the four languages.

## Emergent impact

**Privacy:** the `private` item (the resident's **own** thing) is
invisible to a non-member — the list hides it (`hiddenCount ≥ 1` in the
aggregate row), the detail is a **404, never a 403** (the C-M3·4
non-leaky shape), and the standing probe (D5) means only the **owner ∪
GlobalAdmin** can check it out — a resident's personal item is never
lent by accident, and its existence is not even disclosed. The
audit-by-default (C-M16·2) means every read decision over
audience-restricted items — Allow **and** Deny — is logged in the same
transaction as the domain write (SECURITY.md's audit floor, the
`AccessAudit` lane). **Trust:** the C-M16·5 split is the trust
boundary — *visibility* is **always** the frozen Audience (D2) and
*acting* is **always** the D5 standing probe; the two never leak into
each other, so "who can see it" and "who can act on it" stay separate
decisions even though the `shared` standing is broad (the named trade,
§FACES). **Reliability:** the F1 atomic transition + the unique partial
index make the double-check-out impossible at the DB layer (a failure,
not a race) — the winner/loser is decided by Postgres, not by app code;
the checkout records are **append-only** (C-M16·3) — the history can
never be silently rewritten, which is what makes "how much they are used
by whom" a fact, not a claim. **Legibility:** the `OwnerKind` label
(D3) groups the list into **shared / community / private** without
changing a single access decision — the grouping a resident reads is the
standing breadth they can predict, and the label is a **string** (the M5
`KanbanStatuses` shape), never an enum the compiler would lock in.
**Cost:** the resident's, and it is *less* than today's — one check-out
tap instead of one "do you have the ladder?" conversation; the history
is maintenance-free (append-only, read-only).

## Local-optimization check

This is the **whole** (the neighborhood's shared things), not a part.
It optimizes **coordination** (who has the thing, where it is, who has
used it) at the cost of **none of the resident's privacy** (the
`private` standing is the narrowest of the three — D5), and it
**protects** the trust boundary rather than testing it: the `shared` /
`community` broad standing is a *frictionless-lending* decision the
roadmap row names, and the C-M16·5 split is the invariant that keeps it
from becoming an *audience* decision. The part it deliberately does
**not** optimize is **recurring booking** (a pre-booked slot, a hold —
§deferred, own ADR) — the "optionally how much they are used by whom"
the row names is the **history**, not a reservation calendar, and that
is the correct boundary: M16 owns the **track** the row names
(check-out / check-in / where / by whom), not the **book** a follow-on
lane would own.

## FACES check

- **F1 — The check-out / check-in transition is atomic** (strengthened
  — the house's FACES *stable* + *coherent* face). The item's
  `CurrentHolderId` flips **and** the checkout record is appended /
  closed in **one commit** (C-M16·3). A concurrent double-check-out of
  the same item resolves to **one** winner: the unique partial index on
  `(ItemId)` where `CheckedInAt IS NULL` (the M9 `convo_uidx_pair`
  shape) makes the second commit fail — a caught conflict the loser sees
  as "already checked out." The transition and its audit row commit or
  roll back together (C3, C-M16·2).
- **F2 — The list is one `CanSeeAsync(Read)` pass (C6) with one
  aggregate audit row, filtered by `OwnerKind`** (strengthened —
  *stable*). One group-load, one matching pass, one aggregate
  `AccessAudit` row (`visibleCount` / `hiddenCount`); `OwnerKind` is a
  **filter, never a gate** (C-M3·2 / C-M16·5) — the audience decision is
  unchanged by the filter.
- **F3 — The usage history is the append-only checkout records**
  (strengthened — *coherent*; the record set **is** the history —
  C-M16·3). A read-only aggregate per item; no writes beyond the
  checkout/check-in transition; never mutated or deleted. "Optionally
  how much … by whom" is satisfied by this history, not by a separate
  mechanism (D1 forbids the third "Usage" doc/context).
- **F4 — Every write standing is a server-side probe** (strengthened —
  *adaptive*; D5 / C-M16·4). Creator ∪ GlobalAdmin over create / edit /
  delete; any member who can see it over a `community` / `shared`
  check-out/check-in; owner ∪ GlobalAdmin over a `private` one. The Web
  `[Authorize]` is a **convenience pre-gate only** — the source of
  truth is the probe in the service, in the same transaction as the
  audit row (C3).
- **F5 — M16 is a standalone core surface** (strengthened — *energizing*;
  C-M16·6 / D6). It does not depend on a lane being on: **no**
  off-by-default admin toggle, **no** `LocaleSettings`-style switch,
  **no** `KumunitaFeature` admin flag — it is always available to the
  community, like M5 Projects. The only external composition is the
  frozen `IAuthorizationService` + `IUserInfoService`.

**The named trade (F4/F5 spend standing breadth for frictionless
lending) — the private-vs-shared write-standing breadth:** a
`community` / `shared` item lets **any member who can see it** check it
out (a **broad** standing — frictionless community lending, the M5
`ClaimTodoAsync` precedent), while a `private` item restricts the
standing to the **owner ∪ GlobalAdmin** (the M5 creator-standing shape).
The trade: a broad standing for shared items is more frictionless, but
it means **"who can act"** is a standing probe over `OwnerKind` (D5),
**not** a pure audience decision. The invariant that keeps this safe is
the **C-M16·5 split**: the *visibility* decision is **always** the
frozen Audience (D2), and the *action* decision is **always** the
standing probe (D5) — no code path lets one decide the other. A
resident can **see** a shared item (audience) but the standing probe is
what lets them **act**; and a private item's owner can act even where a
member cannot. This is the M5 creator-vs-broad-standing trade (C-M5·6)
carried to M16.

## Rollout & rollback

**Deployment:** a surface-milestone — two documents + one new surface +
one adapter + one service seam + one controller + four views + one nav
entry + twenty registry keys. **One idempotent delta** at boot:
`M16DocTypes` is delta-detected and applied by
`ApplyAllConfiguredChangesToDatabaseAsync()` (ADR 0004 §B.1) — the two
new tables + the three indexes, **zero migrations for existing
surfaces** (D1). The twenty `inv.*` keys are seeded by the existing
100%-completeness seeder floor (the ADR 0052 warm-boot backfill, the
`projects.*` precedent).

**Rollback (the documented path):**

1. **A mistaken check-out / check-in** — the transition is the **only**
   write to the item's state; the checkout record is **append-only**
   (C-M16·3 — never mutated or deleted), so the history shows exactly
   what happened and the `AccessAudit` row (`CheckOut` / `CheckIn`,
   `ActorId`) names who and when. Correcting it is a **new** check-in /
   check-out by a standing actor — the audit trail is the rollback's
   evidence, not its obstacle.
2. **A mistaken item** (created / edited in error) — the ADR 0024
   soft-delete flag (the `IsDeleted` flip) removes it from the list
   without touching the append-only history (C-M16·3); restoring it is
   the flag flipped back.
3. **A surface-level rollback** — the M16 surface is **parallel and
   additive** (D1: a new context, a new surface, a new adapter, a new
   controller, a new registry block): reverting the code leaves the two
   `mt` tables behind but touches **no existing surface** (the delta is
   additive-only — the M9/M5 precedent). The D7 docs flip (the U06
   close) reverts with the code (the four surfaces flip **together** —
   C-M16·7).

## Risks

- **The frozen-seam boundary is the whole ballgame.** A unit that
  "improves" `IAuthorizationService`, adds an `AccessAction` /
  `AccessVia` / `Decide()` branch, or reads **group membership** for
  inventory access would **break C-M16·4 / D2** — the single most
  load-bearing M16 invariant. It is pinned three ways: the §drift-guard
  frozen list (the verbatim set), the U02/U03 standing-probe deny pins
  (the probe composes only the frozen seams), and the
  `MilestonesTests` / parity test classes staying green unreshaped.
- **The F1 witness is the DB, not the app.** A unit that "fixes" the
  double-check-out with an app-level `if` instead of letting the unique
  partial index fail the second commit would **break the F1 / C-M16·3
  pin** — the U03 atomic double-check-out test is the witness (the
  `convo_uidx_pair` precedent: the DB is the arbiter).
- **The standing-probe and the audience decision must never leak.** A
  unit that lets `OwnerKind` decide a **read** (or the Audience decide a
  **standing**) would **break C-M16·5** — the U03 standing-probe deny +
  the U02 404-vs-403 split pins are the witnesses (the two decisions
  stay separate; the `shared` broad standing is a *standing* decision
  over a *visible* item, never a visibility grant).
- **The `inv.*` keys are a closed set of twenty** (§kw-l) — a unit that
  registers a key not in the list (or drops one) **breaks the parity
  pins** (`KnownTranslationKeys_ParityTests` +
  `KwLRegistryConsistencyTests` — the 4-language + non-empty floors).
  The list is locked here; a future lane adds its key **through the
  registry** (the parity pins extend) and records the drift in
  §drift-guard.
- **The D7 flip is one unit or it is not done.** Flipping
  `Milestones.cs` in U06 and the README in a follow-up commit would
  **break C-M16·7** (the `MilestonesTests` pin
  `M17_Is_The_Single_InProgress_Milestone` is the witness — the four
  surfaces flip **together**, the M15/U10 docs-parity precedent).

## Integration step served

**understanding → coordination** — the value-chain arrow the M16
roadmap row names: the neighborhood's shared things are **tracked**
(check-out / check-in — *where* the item is), **attributed** (the
append-only history — *who* has used it, *how much*), and **governed**
(the standing split — a `shared` thing is frictionless to lend, a
`private` thing is the owner's decision). It is also the **world-seam**
handoff the template asks about: the item's state is a fact the
neighborhood can act on (a check-out is a *handoff* — the item leaves
the pool, is with a person, and comes back), and the history is the
**legible record** of those handoffs (the "optionally how much they are
used by whom" the row names — a fact, not a claim).

## World seams

- **Out** — the neighborhood's **lending**: a check-out is the item
  leaving the pool and going to a person (a real-world handoff the
  platform records — the `CurrentHolderId` is the "where is it" answer);
  the usage history is the **record** of those handoffs a resident can
  read (F3 — the "how much … by whom" surface). The seam is the
  **lending itself** — M16 makes the neighborhood's physical sharing
  visible and attributable without a second system (a spreadsheet, a
  group chat, a memory).
- **In** — the platform: a check-in is the item **returning** (the
  `CurrentHolderId` clears, the open record closes, the history grows
  by one append-only record — C-M16·3). The item's state (in the pool /
  with a person) is the single source of truth before, during, and
  after every transition (the F1 atomic witness).
- **Not** — a **notification** on checkout (§deferred, D8, own ADR — the
  M6 `INotificationService` emitter idiom); a **translation** of the
  item's name/description (§deferred, own ADR — the M5 `TodoTranslation`
  shape); **media** on items (§deferred, own ADR — the ADR 0025 / 0034
  shape); **recurring / reserved** check-outs (§deferred, own ADR — a
  pre-booked slot, a hold); a **third "Usage" document or context**
  (C-M16·3 forbids — the history **is** the checkout records).

## Decisions (D1–D8, locked — ADR 0117)

> **Locked verbatim.** These are the register's [PROPOSED] set, locked
> with **no veto recorded** (the open-veto window closed before U00 ran
> — the U00 handoff entry records the lock). The unit agents copy these,
> not the register's prose.

- **D1 — Two documents, one new surface, no new bounded context.** M16
  adds a new bounded context `Kumunita.Core.Inventory` with **two
  documents**: `InventoryItem` (the thing being tracked) and
  `InventoryCheckout` (the append-only usage record), registered on a
  **new `M16DocTypes`** surface (the M9/M5 `*DocTypes` precedent — a
  parallel additive surface, delta-detected and applied idempotently at
  boot, **zero migrations** for existing surfaces). The usage history is
  the `InventoryCheckout` records *inside* the same context — **not** a
  separate "Usage" bounded context.
  *Forbids:* a third "Usage" document/context for the history; a
  migration of an existing surface; a `M15DocTypes`-style
  additive-on-an-old-surface shape.

- **D2 — Access = the frozen `IAuthorizationService` + an adapter, zero
  new branch.** M16 adds an **adapter**
  `InventoryItemToAuditableResource` (the **exact** M5
  `ProjectToAuditableResource` 6-member projection — `Id`/`Name`/
  `OwnerId`/`Audience`/`ComponentId`/`TargetKind`; the only difference
  is `TargetKind = "inventory"`) and routes **every** read decision
  through the **frozen** `IAuthorizationService`
  (`CanAsync(Read)` detail / `CanSeeAsync(Read)` list). **No** new
  `AccessAction`, **no** new `AccessVia`, **no** new branch in
  `Decide()`, **no** group-lane method on `IAuthorizationService`
  (inventory is not a group surface).
  *Forbids:* a new `AccessAction`/`AccessVia`; a new `Decide()` branch;
  a new method on `IAuthorizationService` for inventory; reading group
  membership for access in the Inventory context.

- **D3 — Ownership kind is a label, never an access gate.**
  `InventoryItem.OwnerKind` ∈ {`shared`, `community`, `private`} is a
  **string** (the M5 `KanbanStatuses` string-not-enum shape) that
  drives the **UI grouping** and the **write standing** breadth (D4) —
  it **never** gates reads. The read decision is always the item's
  `Audience` via the frozen service (D2). The list view filters
  candidates by `OwnerKind` (a **filter, never a gate** — the
  `componentId` C-M3·2 discipline), but the audience decision is
  unchanged.
  *Forbids:* `OwnerKind` as an enum; `OwnerKind` deciding a read/access
  decision; a `Decide()` branch keyed on `OwnerKind`.

- **D4 — Check-out / check-in = an atomic state transition + an
  append-only record.** Check-out sets `InventoryItem.CurrentHolderId`
  (+ the checkout record's open state) and **appends** an
  `InventoryCheckout`; check-in clears `CurrentHolderId` and **closes**
  the open record (sets `CheckedInAt`). The checkout record is
  **append-only** — never mutated or deleted (the "usage history" is
  this record set). At most **one open checkout per item** at a time,
  enforced at the DB layer by a **unique partial index** on `(ItemId)`
  where `CheckedInAt IS NULL` (the M9 `convo_uidx_pair` F1 idempotency
  shape).
  *Forbids:* a separate "usage" table/context; mutating/deleting a
  closed checkout record; a second open checkout of the same item (the
  index is the witness).

- **D5 — The write standing is a server-side probe, never a new
  `AccessVia`.** Who may create / edit / delete an item, and who may
  check it out / in, is decided by a **pure server-side standing probe**
  in the service (composing the frozen `IAuthorizationService` "can see
  it" + `IUserInfoService` for the role probe): **creator ∪ GlobalAdmin**
  over an item's create/edit/delete; and the **broad** standing — **any
  member who can see it** — over a `community`/`shared` item's
  check-out/check-in (the M5 `ClaimTodoAsync` broad standing precedent).
  The Web `[Authorize]` attribute is a **convenience pre-gate only**,
  never the source of truth. No new `AccessVia` value beyond the
  existing set (the TG ADR 0044 "no new AccessVia beyond Owner/Admin"
  precedent).
  *Forbids:* a new `AccessVia` value; the Web `[Authorize]` as the
  source of truth; a standing that lets a non-visible actor check out an
  item.

- **D6 — M16 is a core surface, not a lane: no off-by-default toggle.**
  M16 is a standing core surface (like M5 Projects): **no**
  off-by-default admin toggle, **no** `LocaleSettings`-style admin
  switch, **no** `KumunitaFeature` admin flag. It is always available to
  the community. (M9's off-by-default toggle was a *lane*; M16 is a
  *milestone*.)
  *Forbids:* an off-by-default admin toggle for M16; a
  `LocaleSettings`-style switch; an admin flag gating the surface.

- **D7 — The close flip is one atomic unit.** The M16 close (U06) flips
  the roadmap parity **together**: `Milestones.cs` M16
  `StatusNext`→`StatusDone`, M17 `StatusPlanned`→`StatusNext`;
  `MilestonesTests.cs` re-pinned to
  `M17_Is_The_Single_InProgress_Milestone` + the shipped list gains M16
  + M18 stays planned; the README **Roadmap** (M16 → done, M17 → in
  progress) + the M16 **Status** line; `docs/STATUS.md`; and
  `docs/ARCHITECTURE.md`'s value-chain table gains the **M16 row** (the
  table currently ends at the M15 row). All four surfaces ship **in the
  same unit** (the M15/U10 docs-parity precedent).
  *Forbids:* reordering M17/M18; editing a shipped milestone's text;
  breaking the `MilestonesTests` pins; flipping one surface and not the
  others.

- **D8 — No notification lane in M16 (deferred).** M16 composes **only**
  the frozen `IAuthorizationService` + `IUserInfoService` (the standing
  probe) over the host-registered Marten `IDocumentStore`. It does
  **not** take an `INotificationService` emitter, does **not** register
  a Wolverine handler, and does **not** emit a nudge on
  check-out/check-in. The "nudge the owner on checkout" lane is
  **deferred** — a follow-on lane with its own ADR (the README's
  "optionally" is satisfied by the usage history, not by notifications).
  *Forbids:* an `INotificationService` in the `InventoryService`
  composition; a Wolverine handler for inventory; a nudge emission in
  M16.

## Invariants (C-M16·1–7, locked verbatim)

- **C-M16·1 — The access model is the frozen seam + the adapter.** Every
  inventory read decision (detail **and** list) goes through the
  **frozen** `IAuthorizationService` (`CanAsync(Read)` /
  `CanSeeAsync(Read)`) over `InventoryItemToAuditableResource`
  (`TargetKind = "inventory"`); `OwnerKind` is **never** an access gate
  (D2/D3). M16 adds an *adapter*, not a *branch*.
- **C-M16·2 — Every read decision audits, in-transaction.** Every
  inventory read decision (detail: a single-target row, `TargetKind =
  "inventory"`, `TargetId =` the item id; list: one **aggregate** row,
  `visibleCount`/`hiddenCount`, `TargetId` null) writes **exactly one**
  `AccessAudit` row — **Allow and Deny** — in the **same transaction**
  as any concurrent domain write (invariant C3, via the
  `IDocumentSession` overloads).
- **C-M16·3 — Append-only history; atomic transition.** A check-out is a
  state transition on `InventoryItem` (`CurrentHolderId` set) that
  **appends** an `InventoryCheckout`; a check-in clears
  `CurrentHolderId` and **closes** the open record. Checkout records
  are **append-only** — never mutated or deleted (D4). At most **one
  open checkout per item** (the unique partial index is the witness).
- **C-M16·4 — Zero new `AccessAction`/`AccessVia`/`Decide()` branch.**
  M16 adds **no** `AccessAction`, **no** `AccessVia` value, **no**
  branch in `Decide()`, **no** method on `IAuthorizationService` (D2).
  The write standing (D5) is a pure server-side probe, never a new
  `AccessVia`.
- **C-M16·5 — Visibility is the Audience; standing is the action.** A
  item is only ever listed/seen if the actor passes the
  `CanSeeAsync(Read)` pass (the list's single aggregate row is the
  C-M3·3 shape); `OwnerKind` narrows the candidate set (a filter, never
  a gate — C-M3·2) but does **not** change the audience decision. The
  **action** decision (may this actor check out / edit / delete) is the
  D5 standing probe — the two never leak into each other (a resident can
  *see* a shared item; the *standing* is what lets them *act*).
- **C-M16·6 — M16 is a standing core surface (no toggle).** M16 has
  **no** off-by-default admin toggle and **no** `LocaleSettings`-style
  admin switch (D6); it is always available to the community.
- **C-M16·7 — The close flip is atomic.** The M16 close (U06) flips the
  four surfaces — `Milestones.cs` (M16→done, M17→next),
  `MilestonesTests.cs` (re-pinned to
  `M17_Is_The_Single_InProgress_Milestone` + shipped list + M18
  planned), the README Roadmap/Status, and `docs/STATUS.md` + the
  `docs/ARCHITECTURE.md` value-chain M16 row — **in the same unit**
  (D7).

## §kw-l — the key list (the `inv.*` keys × en/de/fr/da)

> **Locked.** These are the **twenty** `inv.*` keys U04 registers in
> `KnownTranslationKeys` (in `EnValues` / `DeValues` / `FrValues` /
> `DaValues` — the four-language + non-empty floors, the
> `projects.*` block shape). The `KnownTranslationKeys_ParityTests` +
> `KwLRegistryConsistencyTests` pins extend automatically. The **list**
> is locked here — the count and the names — so U04 and the parity pins
> agree. U04 maps them to the views as it authors them; adding a key not
> in this list (or dropping one) is a drift event (§drift-guard).

| Key | The view it serves | `en` floor text |
|---|---|---|
| `inv.nav` | the sidebar/nav entry (U05) | `Inventory` |
| `inv.list.title` | the list page's title (U04) | `Inventory` |
| `inv.list.empty` | the list's empty state (U04) | `No items yet.` |
| `inv.list.ownerKind.shared` | the list's `shared` filter label (U04) | `Shared` |
| `inv.list.ownerKind.community` | the list's `community` filter label (U04) | `Community` |
| `inv.list.ownerKind.private` | the list's `private` filter label (U04) | `Private` |
| `inv.list.filter` | the list's filter prompt (U04) | `Filter by type` |
| `inv.create.title` | the create form's title (U04) | `New item` |
| `inv.create.name` | the create form's `Name` label (U04) | `Name` |
| `inv.create.ownerKind` | the create form's `OwnerKind` label (U04) | `Type` |
| `inv.create.description` | the create form's `Description` label (U04) | `Description` |
| `inv.create.component` | the create form's `ComponentId` label (U04) | `Section` |
| `inv.create.submit` | the create form's submit button (U04) | `Create item` |
| `inv.detail.title` | the detail page's title (U04) | `Item` |
| `inv.detail.currentHolder` | the detail's "currently with" label (U04/U05) | `Currently with` |
| `inv.detail.history` | the detail's usage-history section label (U04) | `Usage history` |
| `inv.detail.edit` | the detail's edit button (U05) | `Edit` |
| `inv.detail.delete` | the detail's delete button (U05) | `Delete` |
| `inv.detail.checkOut` | the detail's check-out button (U05) | `Check out` |
| `inv.detail.checkIn` | the detail's check-in button (U05) | `Check in` |
| `inv.edit.title` | the edit form's title (U05) | `Edit item` |
| `inv.edit.submit` | the edit form's submit button (U05) | `Save changes` |

Each × **4 languages** (`en` / `de` / `fr` / `da`), the closed-key
registry shape (the `KnownTranslationKeys_ParityTests` +
`KwLRegistryConsistencyTests` pins enforce the 4-language set + non-empty
values; the ADR 0052 warm-boot backfill seeds them idempotently — the
`projects.*` precedent).

## §gate — the three acceptance tests (U06)

> **The gate** (the closed-loop / handoff / part-vs-whole shape, per the
> design-doc template). All three over the **full stack** (the routes +
> the store), in `tests/Kumunita.Web.Tests/`.

1. **`M16_Acceptance_ClosedLoop_CheckOutCheckInHistory`** (the
   **closed loop**) — create a `shared` item (the create lane, the
   creator's `AuthorId`) → a **second** resident checks it out (the
   broad standing — D5, the `shared` class) → the second resident
   checks it in (the standing holder — D5) → the item's **usage
   history** shows **exactly one** checkout record with `CheckedOutAt`
   + `CheckedInAt` set (C-M16·3 / F3) and the item's `CurrentHolderId`
   is cleared (F1 / D4) — the "check-out / check-in … track where items
   are … how much they are used by whom" the roadmap row names,
   end-to-end.
2. **`M16_Acceptance_HandoffAuthorizationBoundary_PrivateAndShared`**
   (the **handoff**) — a `private` item is **invisible** to a non-member
   (the list hides it — the aggregate row shows `hiddenCount ≥ 1`; the
   detail is a **404, not a 403** — C-M16·5 / C-M3·4); and a `shared`
   item is **visible to all members** but only the **standing** actor
   can check it out (a non-standing member's check-out is **denied** —
   the deny `AccessAudit` row commits — D5 / C-M16·5) — the
   visibility/standing split (C-M16·5) held at the boundary: *see* ≠
   *act*.
3. **`M16_Acceptance_PartVsWholeAuditCompleteness`** (the **part vs.
   whole**) — the list's **aggregate** `AccessAudit` row commits
   **atomically** with any concurrent write (C-M16·2 / C3 — the
   `IDocumentSession` overload), and the check-out / check-in
   transition's `AccessAudit` row commits **atomically** with the state
   change (C-M16·3 — one `SaveChangesAsync`, the F1 witness) — the
   whole audit trail survives a concurrent write, and the part (a
   single transition) cannot commit without its audit row.

**Cross-surface pins (U06 adds only what's missing):** the frozen-seam
confirmation (C-M16·4 — `IAuthorizationService` / `AccessAction` /
`AccessVia` / `Decide()` untouched after M16); the twenty `inv.*` keys'
four-language parity (the U04 pins already record this — a single
consolidated assertion if not already covered); the D7 four-surface docs
parity flip (C-M16·7 — `Milestones.cs` / `MilestonesTests.cs` / README
Roadmap+Status / `docs/STATUS.md` + `docs/ARCHITECTURE.md` value-chain
M16 row, **in the same unit**).

## §deferred — the deferred-lane list (each a named follow-on, own ADR)

| Deferred lane | The one-line "own ADR" note |
|---|---|
| **The "nudge the owner on checkout" notification lane** (D8) | The M6 `INotificationService` emitter idiom + a Wolverine handler — a **new trust boundary** (an outbound nudge on a state transition) that M16 deliberately does not take. The README's "optionally" is the **usage history** (F3), not a notification. Own ADR as a new outbound surface. |
| **The per-item translation lane** (the M5 `TodoTranslation` shape) | The item's `LanguageCode` tag (ADR 0018) is present for display; a full translation record (one row per item × language, with its own author standing matrix) is a **different inventory** — an own lane, own ADR. M16's item is **single-language** by design (the README row does not name translations). |
| **The media / attachments-on-items lane** (the ADR 0025 / ADR 0034 shape) | An image / a file on an item is a **media lane** over the ADR 0011 storage substrate — a new surface with its own standing matrix. M16's item is **text + ownership** (the README row names the *thing* and its *use*, not its *picture*). Own ADR. |
| **The recurring / reserved check-out lane** (a pre-booked slot, a hold) | A **reservation** is a second state (booked-but-not-checked-out) the F1 transition does not model — a new document, a new transition, a new standing matrix (who may book vs. who may check out). Own ADR as a new surface on top of the F1 shape. |
| **A third "Usage" document or context** (C-M16·3 forbids) | The usage history **is** the `InventoryCheckout` records (D4 / F3) — a separate "Usage" doc/context is the anti-pattern the D1 *Forbids:* names. No ADR is needed for what is forbidden. |

## §drift-guard — the frozen pins + the drift log

**The frozen pins** (the U01–U06 units copy verbatim from this doc;
**untouched** after M16):

1. `IAuthorizationService` — the **4-method surface** + the group lane
   (`CanAsync` / `CanSeeAsync` + the `IDocumentSession` overloads + the
   group-lane methods) — **byte-identical** (C-M16·4 — M16 adds an
   *adapter*, not a *method*).
2. The `AccessAction` set (`Read` / `Moderate`) — **byte-identical**
   (C-M16·4 — no new action).
3. The `AccessVia` set (Owner / Admin + the existing values) —
   **byte-identical** (C-M16·4 / D5 — the standing probe composes the
   existing values; no new value).
4. `Decide()` — **byte-identical** (C-M16·4 / D2 — no new branch; the
   adapter's `TargetKind = "inventory"` is a **data** value, not a
   **code** branch).
5. `IUserInfoService` (the GlobalAdmin standing probe) —
   **byte-identical** (D5 — the probe composes it; M16 adds no method).
6. The `Post` `Audience` type — **byte-identical** (D2/D3 — the
   `InventoryItem.Audience` is the **exact** same type; the adapter
   projects it verbatim, never mutates it).
7. The existing `*DocTypes` surfaces (`M1DocTypes` … `M9DocTypes`) —
   **byte-identical** (D1 — the `M16DocTypes` surface is **parallel**,
   never additive on an old one).
8. The `TranslationResource` store + the `TranslationResource` lane —
   **byte-identical** (D8 — M16 takes no notification emitter; the
   "nudge the owner" lane is §deferred).
9. `KnownTranslationKeys`'s **existing** blocks (the `projects.*` /
   `translations.bulk.*` / … sets) — **byte-identical** (the `inv.*`
   block **joins** the closed set — the parity pins **extend**, never
   reshape).
10. `tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs` +
    `tests/Kumunita.Web.Tests/`'s `KwLRegistryConsistencyTests` —
    **unreshaped** (they extend with the twenty `inv.*` keys via the
    registry, never re-pinned).

**The drift log:** (none at U00 — the locked D1–D8, C-M16·1–7, F1–F5 +
the named trade, the §kw-l key list, and the §gate test names match the
register verbatim; the source was checked against the live
`ProjectToAuditableResource` (the 6-member projection — the adapter
shape), the `M9DocTypes` surface (the `*DocTypes` + the
`convo_uidx_pair` unique-index shape), the ADR 0116 shape (the ADR
Status/Context/Decision/Consequences + the deferred-lane + affected-files
sections), and the ADR 0117-free confirmation against
`docs/adr/README.md` (the index ends at 0116 — 0117 is free) — no
source-driven refinement was needed.)

**The drift-guard rule** (the unit-series rule): a unit that finds the
source has moved beyond a frozen pin records the drift **here** (an
append, not a rewrite) and resolves in favor of the source; it never
silently re-derives a pin from a stale prose. The §kw-l list is the
**exact** closed set of twenty keys — a future lane that adds an
inventory affordance adds its key **through the registry** (the parity
pins extend) and records the drift here. The §gate test names are the
**exact** U06 acceptance set — a future lane that adds an acceptance
test **appends** to the §gate list and records the drift here. A change
to any of the D1–D8 decisions, the C-M16·1–7 invariants, the §kw-l
list, or the §gate names **after U00** is a **drift event** (this log is
the witness).
