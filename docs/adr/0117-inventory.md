# ADR 0117 — M16: Inventory (check-out / check-in + usage history)

Status: Accepted
Date: 2026-09-29
Extends the **M5 Projects bounded-context precedent** (the
`Kumunita.Core.Projects` context, the `M5DocTypes` surface, the
`ProjectToAuditableResource` 6-member adapter, the C-M5·6
creator-vs-broad-standing split — ADR 0086 / ADR 0106), the **M9
`M9DocTypes` parallel-surface + unique-index-as-witness precedent**
(`convo_uidx_pair` — ADR 0105 D1), the **ADR 0004 §B.1
delta-detected, idempotent, zero-migration document-surface shape**,
the **frozen `IAuthorizationService` seam** (the 4-method surface + the
`IDocumentSession` overloads, C3 — ADR 0006 §A), the **frozen
`AccessAction` / `AccessVia` sets + the frozen `Decide()`** (ADR 0006 —
the "M16 adds an *adapter*, not a *branch*" pin), the **ADR 0024
soft-delete flag** (the item's delete lane), the **ADR 0018 authored-in
`LanguageCode` tag** (the item's display tag), and the **closed
`KnownTranslationKeys` registry + the en/de/fr/da parity pins** (ADR
0015 / ADR 0052). This ADR makes the **neighborhood's shared,
community-owned, or private things trackable** (the ARCHITECTURE.md M16
value-chain row, verbatim): **check out / check in** a shared,
community-owned, or private resource (equipment, clothes for sports
teams, books, etc.), **track where items are** (the item's current
holder), and **optionally track how much they are used by whom** (the
append-only usage history) — a standalone core surface over **two
documents, one new surface, one adapter, and zero new authorization
surface** (C-M16·4), with **zero migrations for existing surfaces**
(D1).

## Context

The neighborhood today has a place for **tasks** (M5 to-dos) and
**events** (M4), but no place to say *the ladder is with Anna*, *the
club jersey is at home*, or *who has used the projector this season*.
The README roadmap row names M16 exactly: "**Inventory — check-out /
check-in shared, community-owned, or private resources (equipment,
clothes for sports teams, books, etc.), track where items are, and
optionally how much they are used by whom.**"

M5 (Projects) already proved the shape M16 copies: a **standalone core
surface** on its own bounded context (`Kumunita.Core.Projects`), its own
`M5DocTypes` Marten surface (the ADR 0004 §B.1 parallel-surface shape —
delta-detected, applied idempotently at boot, **zero migrations for
existing surfaces**), one **adapter** onto the **frozen**
`IAuthorizationService` (the `ProjectToAuditableResource` 6-member
projection — `Id` / `Name` / `OwnerId` / `Audience` / `ComponentId` /
`TargetKind`, the **only** difference being the `TargetKind` string),
the **creator ∪ GlobalAdmin vs. broad-standing** split over its write
lanes (C-M5·6), and the `projects.*` `kw-l` block. The M9 (`M9DocTypes`)
precedent adds the **unique-index-as-witness** idiom — the
`convo_uidx_pair` unique index is the F1 idempotency witness, making a
concurrent double-open of a conversation fail at the **DB layer**
instead of in app code.

The constraint that shapes the decision is the same one that shaped M5 /
M9: **compose the frozen seams, never extend them** (C-M16·4) — M16
adds an **adapter** (`InventoryItemToAuditableResource`) onto the
**frozen** `IAuthorizationService` (ADR 0006 §A — `CanAsync` /
`CanSeeAsync` + the `IDocumentSession` overloads, C3), with **no** new
`AccessAction`, **no** new `AccessVia`, **no** branch in `Decide()`, and
**no** method on `IAuthorizationService` (inventory is not a group
surface — the group lane is not touched); and **the DB is the arbiter of
the state transition** (D4) — the F1 atomicity is witnessed by a unique
partial index, not by app-level locking. The surface stays **core** (D6)
— M16 is a **milestone**, not a **lane** (M9's off-by-default
`LocaleSettings` toggle was the *lane* shape; M16 is the M5 *milestone*
shape — always available, no admin gate).

## Decision

**D1 — Two documents, one new surface, no new bounded context.** M16
adds a new bounded context `Kumunita.Core.Inventory` with **two
documents**: `InventoryItem` (the thing being tracked — `Id` string
surrogate, `Name` non-empty, `OwnerKind` **string** ∈
`shared`/`community`/`private` (the M5 `KanbanStatuses` shape),
`Description` optional, `ComponentId` (a feed filter, never a gate —
C-M3·2), `AuthorId` (the standing owner — D5), `Audience` (the **exact**
`Post` `Audience`, `null` = public — D2/D3), `CurrentHolderId` nullable
(`null` = in the pool — D4), `IsDeleted` (ADR 0024), `LanguageCode`
(ADR 0018), `Created`, `Modified`) and `InventoryCheckout` (the
append-only usage record — `Id`, `ItemId`, `BorrowerId`, `CheckedOutAt`,
`CheckedInAt` nullable — open when null, `Note` optional), registered on
a **new `M16DocTypes`** surface (the M9/M5 `*DocTypes` precedent — a
parallel additive surface, delta-detected and applied idempotently at
boot, **zero migrations** for existing surfaces; the `InventoryItem`
`(ComponentId, Created)` + `(OwnerKind, Created)` feed/filter indexes;
the `InventoryCheckout` `(ItemId, CheckedOutAt)` thread-ordering index +
the **unique partial index** on `(ItemId)` where `CheckedInAt IS NULL`
— the F1 idempotency witness, the M9 `convo_uidx_pair` shape). The usage
history is the `InventoryCheckout` records *inside* the same context —
**not** a separate "Usage" bounded context. *Forbids:* a third "Usage"
document/context for the history; a migration of an existing surface; a
`M15DocTypes`-style additive-on-an-old-surface shape.

**D2 — Access = the frozen `IAuthorizationService` + an adapter, zero
new branch.** M16 adds an **adapter**
`InventoryItemToAuditableResource` (the **exact** M5
`ProjectToAuditableResource` 6-member projection — `Id = Item.Id`,
`Name = Item.Name`, `OwnerId = Item.AuthorId` (the `Decide()` owner
branch), `Audience = Item.Audience` (the **exact** `Post` `Audience`,
`null` = public, projected verbatim, never mutated), `ComponentId =
Item.ComponentId` (a feed filter, never an access boundary — C-M3·2),
`TargetKind = "inventory"` (the **exact** string — the
`AccessAudit.TargetKind` discriminator for this line of decisions, C3);
the **only** difference from the M5 adapter is the `TargetKind` value)
and routes **every** read decision through the **frozen**
`IAuthorizationService` (`CanAsync(Read)` detail / `CanSeeAsync(Read)`
list, the `IDocumentSession` overloads for C3 same-transaction audits).
**No** new `AccessAction`, **no** new `AccessVia`, **no** new branch in
`Decide()`, **no** group-lane method on `IAuthorizationService`
(inventory is not a group surface). *Forbids:* a new
`AccessAction`/`AccessVia`; a new `Decide()` branch; a new method on
`IAuthorizationService` for inventory; reading group membership for
access in the Inventory context.

**D3 — Ownership kind is a label, never an access gate.**
`InventoryItem.OwnerKind` ∈ {`shared`, `community`, `private`} is a
**string** (the M5 `KanbanStatuses` string-not-enum shape) that drives
the **UI grouping** and the **write standing** breadth (D5) — it
**never** gates reads. The read decision is always the item's `Audience`
via the frozen service (D2). The list view filters candidates by
`OwnerKind` (a **filter, never a gate** — the `componentId` C-M3·2
discipline), but the audience decision is unchanged. *Forbids:*
`OwnerKind` as an enum; `OwnerKind` deciding a read/access decision; a
`Decide()` branch keyed on `OwnerKind`.

**D4 — Check-out / check-in = an atomic state transition + an
append-only record.** Check-out sets `InventoryItem.CurrentHolderId`
(+ the checkout record's open state) and **appends** an
`InventoryCheckout`; check-in clears `CurrentHolderId` and **closes**
the open record (sets `CheckedInAt`). The checkout record is
**append-only** — never mutated or deleted (the "usage history" is this
record set — F3). At most **one open checkout per item** at a time,
enforced at the DB layer by a **unique partial index** on `(ItemId)`
where `CheckedInAt IS NULL` (the M9 `convo_uidx_pair` F1 idempotency
shape) — the transition and its audit row commit or roll back together
(one `SaveChangesAsync`, C3); a concurrent double-check-out's second
commit fails at the DB layer and is surfaced to the loser as "already
checked out" (the DB is the arbiter, not the app). *Forbids:* a separate
"usage" table/context; mutating/deleting a closed checkout record; a
second open checkout of the same item (the index is the witness).

**D5 — The write standing is a server-side probe, never a new
`AccessVia`.** Who may create / edit / delete an item, and who may check
it out / in, is decided by a **pure server-side standing probe** in the
service (composing the frozen `IAuthorizationService` "can see it" +
`IUserInfoService` for the GlobalAdmin role probe — the M5 standing-probe
shape, C-M5·6): **creator ∪ GlobalAdmin** over an item's create/edit/
delete; the **broad** standing — **any member who can see it** — over a
`community`/`shared` item's check-out/check-in (the M5 `ClaimTodoAsync`
broad-standing precedent, ADR 0106); **owner ∪ GlobalAdmin** over a
`private` item's check-out/check-in; the **current holder ∪ creator ∪
GlobalAdmin** over a check-in (the close is the holder's act, or a
standing override). The Web `[Authorize]` attribute is a
**convenience pre-gate only**, never the source of truth. No new
`AccessVia` value beyond the existing set (the ADR 0044 "no new
AccessVia beyond Owner/Admin" precedent). *Forbids:* a new `AccessVia`
value; the Web `[Authorize]` as the source of truth; a standing that
lets a non-visible actor check out an item.

**D6 — M16 is a core surface, not a lane: no off-by-default toggle.**
M16 is a standing core surface (like M5 Projects): **no** off-by-default
admin toggle, **no** `LocaleSettings`-style admin switch, **no**
`KumunitaFeature` admin flag. It is always available to the community.
(M9's off-by-default toggle was a *lane*; M16 is a *milestone*.)
*Forbids:* an off-by-default admin toggle for M16; a
`LocaleSettings`-style switch; an admin flag gating the surface.

**D7 — The close flip is one atomic unit (U06).** `Milestones.cs` M16
`StatusNext` → `StatusDone` + M17 `StatusPlanned` → `StatusNext`;
`MilestonesTests.cs` re-pinned to
`M17_Is_The_Single_InProgress_Milestone` + the shipped list gains M16 +
M18 stays planned; the README **Roadmap** (M16 → done, M17 → in
progress) + the M16 **Status** line; `docs/STATUS.md`; and
`docs/ARCHITECTURE.md`'s value-chain table gains the **M16 row** (the
table currently ends at the M15 row). All four surfaces ship **in the
same unit** (the M15/U10 docs-parity precedent, C-M16·7). *Forbids:*
reordering M17/M18; editing a shipped milestone's text; breaking the
`MilestonesTests` pins; flipping one surface and not the others.

**D8 — No notification lane in M16 (deferred).** M16 composes **only**
the frozen `IAuthorizationService` + `IUserInfoService` (the standing
probe) over the host-registered Marten `IDocumentStore`. It does **not**
take an `INotificationService` emitter, does **not** register a
Wolverine handler, and does **not** emit a nudge on check-out/check-in.
The "nudge the owner on checkout" lane is **deferred** — a follow-on
lane with its own ADR (the README's "optionally" is satisfied by the
usage history — F3 — not by notifications). *Forbids:* an
`INotificationService` in the `InventoryService` composition; a
Wolverine handler for inventory; a nudge emission in M16.

## Consequences

**Positive:**

- The neighborhood's **shared things become trackable** (the README M16
  row, end-to-end): a `shared` / `community` / `private` item is
  created, **checked out** (the `CurrentHolderId` flips + the
  `InventoryCheckout` record is appended — **one atomic commit**, F1),
  **checked in** (the `CurrentHolderId` clears + the open record closes),
  and its **usage history** is the **append-only** record set (F3 —
  "optionally how much they are used by whom" is a **fact**, not a
  claim — C-M16·3). A concurrent double-check-out resolves to **one**
  winner at the **DB layer** (the unique partial index — the F1 witness,
  the M9 `convo_uidx_pair` precedent).
- **Zero new authorization surface** (C-M16·4): the **frozen**
  `IAuthorizationService` is the read decision (the adapter's
  `TargetKind = "inventory"` is a **data** value, not a **code** branch),
  the **frozen** `AccessAction` / `AccessVia` sets are unchanged, and
  `Decide()` gains **no** branch — M16 adds an *adapter*, not a *branch*
  (C-M16·1). The write standing is a **server-side probe** over the
  frozen seams (D5) — the Web `[Authorize]` is a convenience pre-gate,
  never the source of truth (F4).
- **Zero migrations for existing surfaces** (D1): the `M16DocTypes`
  surface is **parallel** (the ADR 0004 §B.1 shape — delta-detected and
  applied idempotently at boot) — the two new tables + the three indexes
  are additive; the `M1DocTypes` … `M9DocTypes` surfaces are
  **byte-identical** after M16 (§drift-guard).
- **The `private` standing is the narrowest** (D5): a resident's
  **personal** item (their own jersey, their own camera) is invisible to
  a non-member (the list's aggregate row shows `hiddenCount ≥ 1`; the
  detail is a **404, never a 403** — the C-M3·4 non-leaky shape) and only
  the **owner ∪ GlobalAdmin** can check it out — a personal item is
  never lent by accident, and its existence is not even disclosed
  (C-M16·5).
- **Audit-by-default** (C-M16·2): every inventory read decision (the
  detail's single-target row `TargetKind = "inventory"` + the list's one
  **aggregate** row `visibleCount` / `hiddenCount`) and every write
  decision (the create / edit / delete / check-out / check-in lanes)
  writes **exactly one** `AccessAudit` row — **Allow and Deny** — in the
  **same transaction** as the domain write (C3, the
  `IDocumentSession` overloads) — the SECURITY.md audit floor held on
  the M16 surface.
- **A milestone, not a lane** (D6 / C-M16·6): M16 is **always**
  available to the community (the M5 Projects shape) — **no**
  off-by-default admin toggle, **no** `LocaleSettings`-style switch —
  the surface does not depend on an admin's opt-in (C-M16·6).

**Neutral / cost:**

- **The named trade — the private-vs-shared write-standing breadth**
  (§FACES): a `community` / `shared` item lets **any member who can see
  it** check it out (a **broad** standing — frictionless community
  lending, the M5 `ClaimTodoAsync` precedent), while a `private` item
  restricts the standing to the **owner ∪ GlobalAdmin** (the M5
  creator-standing shape). The cost: "who can act" is a standing probe
  over `OwnerKind` (D5), **not** a pure audience decision. The invariant
  that keeps this safe is the **C-M16·5 split**: the *visibility*
  decision is **always** the frozen Audience (D2), and the *action*
  decision is **always** the standing probe (D5) — the two never leak
  into each other (a resident can **see** a shared item; the standing is
  what lets them **act** — the M5 creator-vs-broad-standing trade,
  C-M5·6, carried to M16).
- One new bounded context + one new document surface + one new service
  seam + one new controller + four views + one nav entry + the
  twenty `inv.*` `kw-l` keys (the `KnownTranslationKeys` closed set —
  the parity pins **extend** with the twenty keys, never reshape — §kw-l).
- The F1 atomicity is **DB-enforced** (the unique partial index) — a
  concurrent double-check-out **fails** at the Postgres layer (a caught
  conflict, surfaced as "already checked out") — the app never
  "resolves" the race (the M9 `convo_uidx_pair` precedent — the DB is
  the arbiter).

**Not chosen:**

- **A third "Usage" document or context for the history** (C-M16·3 — the
  usage history **is** the `InventoryCheckout` records; a separate
  "Usage" store would be a second copy to drift, reconcile, and
  authorize — the D1 *Forbids* pin).
- **A new `AccessAction` / `AccessVia` / `Decide()` branch / method on
  `IAuthorizationService`** (C-M16·4 / D2 — the adapter is the **only**
  new authorization surface; a branch would be a new decision path the
  frozen seam does not own).
- **`OwnerKind` as an enum or a read gate** (D3 / C-M16·1 — the label is
  a **string** (the M5 `KanbanStatuses` shape) and drives only the UI
  grouping + the write-standing breadth; the read decision is **always**
  the frozen Audience).
- **A notification on check-out/check-in** (D8 — the M6
  `INotificationService` emitter idiom is a **new trust boundary** — an
  outbound nudge on a state transition — that M16 deliberately does not
  take; the README's "optionally" is the usage history, F3 — own ADR,
  §deferred).
- **An off-by-default admin toggle** (D6 / C-M16·6 — M16 is a
  **milestone**, not a **lane**; M9's `LocaleSettings` toggle was the
  *lane* shape — M16 is the M5 *milestone* shape, always available).

**Deferred to their own lanes (each a named follow-on, own ADR):**

- **The "nudge the owner on checkout" notification lane** (D8): the M6
  `INotificationService` emitter idiom + a Wolverine handler — a **new
  outbound surface** on a state transition that M16 deliberately does
  not take (the "optionally" is the **usage history** — F3). Own ADR as
  a new trust boundary.
- **The per-item translation lane** (the M5 `TodoTranslation` shape):
  the item's `LanguageCode` tag (ADR 0018) is present for display; a
  full translation record (one row per item × language, with its own
  author standing matrix) is a **different inventory** — an own lane,
  own ADR. M16's item is **single-language** by design (the README row
  does not name translations).
- **The media / attachments-on-items lane** (the ADR 0025 / ADR 0034
  shape): an image / a file on an item is a **media lane** over the
  ADR 0011 storage substrate — a new surface with its own standing
  matrix. M16's item is **text + ownership** (the README row names the
  *thing* and its *use*, not its *picture*). Own ADR.
- **The recurring / reserved check-out lane** (a pre-booked slot, a
  hold): a **reservation** is a second state (booked-but-not-checked-out)
  the F1 transition does not model — a new document, a new transition, a
  new standing matrix (who may book vs. who may check out). Own ADR as a
  new surface on top of the F1 shape.
- **A third "Usage" document or context** (C-M16·3 **forbids**): the
  usage history **is** the `InventoryCheckout` records (D4 / F3) — a
  separate "Usage" doc/context is the anti-pattern the D1 *Forbids:*
  names. No ADR is needed for what is forbidden.

**This ADR is the milestone's decision record (U00); the unit series
U01–U06 executes it, and the U06 close flips the docs parity surface
(D7 — the C-M16·7 precedent, the M15/U10 docs-parity shape).**

## Affected files

- `src/Kumunita.Core/Inventory/InventoryItem.cs` — new (the item
  document; U01).
- `src/Kumunita.Core/Inventory/InventoryCheckout.cs` — new (the
  append-only usage record; U01).
- `src/Kumunita.Core/Inventory/InventoryItemToAuditableResource.cs` —
  new (the adapter — the exact M5 `ProjectToAuditableResource` 6-member
  projection, `TargetKind = "inventory"`; U01).
- `src/Kumunita.Core/Inventory/IInventoryService.cs` +
  `src/Kumunita.Core/Inventory/InventoryService.cs` — new (the read
  lanes `ListItemsAsync` / `GetItemAsync` / `GetHistoryAsync` — U02; the
  write lanes `CreateItemAsync` / `EditItemAsync` / `DeleteItemAsync` /
  `CheckOutAsync` / `CheckInAsync` + the D5 standing probes + the F1
  atomic transition — U03).
- `src/Kumunita.Core/Inventory/InventoryRequests.cs` — new (the
  request DTOs — the sealed-record shape; U02).
- `src/Kumunita.Core/M16DocTypes.cs` — new (the surface — the
  `InventoryItem` `(ComponentId, Created)` + `(OwnerKind, Created)`
  indexes; the `InventoryCheckout` `(ItemId, CheckedOutAt)` index + the
  **unique partial index** on `(ItemId)` where `CheckedInAt IS NULL` —
  the F1 witness; U01).
- `src/Kumunita.Core/DependencyInjection.cs` — the
  `AddTransient<IInventoryService>(…)` registration (the M5 / M9 shape —
  **no** `INotificationService`, D8; U02).
- `src/Kumunita.Web/Program.cs` — `M16DocTypes.Configure(opts);` added
  to the `*DocTypes.Configure(opts)` block (after
  `M9DocTypes.Configure(opts)`; U01).
- `src/Kumunita.Web/Models/InventoryViewModels.cs` — new (the view
  models; U04).
- `src/Kumunita.Web/Controllers/InventoryController.cs` — new (the
  list / detail / create — U04; the edit / delete / check-out /
  check-in — U05).
- `src/Kumunita.Web/Views/Inventory/List.cshtml` /
  `Views/Inventory/Detail.cshtml` / `Views/Inventory/Create.cshtml`
  (U04) + `Views/Inventory/Edit.cshtml` (U05) — new.
- `src/Kumunita.Web/Views/Shared/` (the sidebar/nav partial) — the
  "Inventory" nav entry (U05).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the
  twenty `inv.*` keys × en/de/fr/da (the `projects.*` block shape — the
  parity pins extend; U04).
- `tests/Kumunita.Core.Tests/` — the U01 (the surface + the adapter) +
  U02 (the read-lane) + U03 (the write-lane + the standing-probe) pins
  (names verbatim in the design doc §Feedback loops) +
  `tests/Kumunita.Web.Tests/` — the U04 (3) + U05 (4) pins + the U06
  three acceptance gate tests (the design doc §gate:
  `M16_Acceptance_ClosedLoop_CheckOutCheckInHistory` /
  `M16_Acceptance_HandoffAuthorizationBoundary_PrivateAndShared` /
  `M16_Acceptance_PartVsWholeAuditCompleteness`) + the D7 four-surface
  docs-parity pins.
- `src/Kumunita.Web/Milestones.cs` + `README.md` + `docs/STATUS.md` +
  `docs/ARCHITECTURE.md` + `tests/Kumunita.Web.Tests/MilestonesTests.cs`
  — the D7 four-surface flip (the U06 close, **in the same unit** —
  C-M16·7).
