---
id: adr-0117
title: "M16: Inventory (check-out / check-in + usage history)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m16-inventory-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0117](0117-inventory.md) — M16 Inventory — a shared neighborhood items/room catalogue (a `Community__`-scoped `InventoryItem`, an additive `InventoryItemKind` `OwnerKind` label, and an additive `InventoryCheckout` usage-history doc), with the `InventoryItem → AuditableResource` adapter and the `InventoryItemService` `Audience`-read / owner-∪-`GlobalAdmin`-write standing, server-rendered, tsc-only

Status: Accepted | Date: 2026-09-19

> **Abstract:** M16 ships a **shared neighborhood items/room catalogue** on
> the ADR [0070](0070-board-edit-lane-and-board-polish.md) `Community__`-scoped single-neighborhood shape (the M5
> `Project`/`Todo`/`Kanban` *milestone* shape, the ADR [0062](0062-admin-surface-split.md) tab shape, the
> ADR [0061](0061-email-and-notification-language.md)/0013 standing shape — **not** a multi-tenant inventory, and
> **not** the M5 `Project`): an `Item` is a **neighborhood asset** anyone
> may browse and record a use of, the *owner* ∪ `GlobalAdmin` manages it,
> and the usage **history is the data** (a new `InventoryCheckout` doc, not
> a re-derivation). It rides the frozen base: the `Audience` read decision,
> the `IAuthorizableResource` adapter, the `AccessAudit` write row, the
> tsc-only / no-dependency build, the `KnownTranslationKeys` closed set —
> **no** new bounded context, **no** new `AccessAction` / `AccessVia` /
> `Decide()` branch, **no** new authorization seam, **no** EF Core. The
> standing is **owner ∪ `GlobalAdmin`** (the C-M2·3 shape, **no**
> `Moderator` standing, the ADR [0040](0040-pages-system-and-user-kinds-and-blog-feed.md) retired-`Moderator` precedent). Close
> flip: `Milestones.cs` M16 `StatusNext` → `StatusDone` + M17
> `StatusPlanned` → `StatusNext`. **For** a developer touching
> `Kumunita.Core/Inventory/`, `Kumunita.Web/Controllers/Inventory*`,
> `Kumunita.Web/Views/Inventory/`, `Kumunita.Web/Security/`, or the
> `tests/*` inventory families. **Read the rest** before changing the
> item/checkout shape, the standing, the adapter, or the close flip. The
> close-flip detail, the "Not chosen" list, and the deferred-lane catalog
> are in the [handoff note](../design/adr-handoffs.md#adr-0117--m16-inventory-moved-detail).

## Context

The README Roadmap's M16 row names a **shared neighborhood items/room
catalogue** (ladder, grill, tools, spare room): an `Item` with `OwnerId` +
`Available/CheckedOut` + optional `Usage` history. M16 sits **between** the
shipped M15 (Kanban) and the planned M17 (Translation workbench) in the
M-letter order. It is the *third* `Community__`-scoped catalogue surface
after the M5 `Project` and the M5 `ProjectTodo`/`ProjectKanban*` — the same
single-neighborhood shape (ADR [0070](0070-board-edit-lane-and-board-polish.md)), the same tab shape (ADR [0062](0062-admin-surface-split.md)), the
same standing shape (owner ∪ `GlobalAdmin`, **no** `Moderator`), the same
`tsc-only` build (ADR [0031](0031-wysiwyg-editor-and-toolbar.md)), the same `KnownTranslationKeys` closed set
(ADR [0015](0015-ui-view-localization-mechanics.md)), the same **server-rendered** constraint. The *inventory* is
**not** the M5 `Project` (an owned, per-project surface): an `Item` is a
**neighborhood asset** (no `ProjectId` FK), not a *project deliverable*.
The *owner* is the `Profile` the `OwnerId` points at; a `GlobalAdmin` can
manage **any** item, a *plain resident* only **their own** — the C-M2·3
owner ∪ `GlobalAdmin` shape. The *usage history* is a **new**
`InventoryCheckout` document (a **record** of a use), **not** a
re-derivation from the item's state and **not** a third "Usage" store (the
ADR [0057](0057-user-guides-in-help-subtree-and-consistency-loop.md) D1 *Forbids:* pin).

## Decision

- **D1 — The `InventoryItem` is a `Community__`-scoped document, not a
  `Project` sub-surface.** A new `InventoryItem` doc: `DocId` +
  `CommunityId` (the ADR [0070](0070-board-edit-lane-and-board-polish.md) shape) + **`OwnerId`** (the standing reads
  this) + `Title` + `Description` + `OwnerKind` (an **additive**
  `InventoryItemKind` constant string — the "ladder/grill/room" label; the
  M5 `KanbanStatuses` additive-string shape) + `Availability` (an
  **additive** `InventoryAvailability` constant string: `available` /
  `checked_out` / `maintenance`) + `LanguageCode` (ADR [0018](0018-ugc-authored-in-language-tag.md)) +
  `CreatedAt` / `UpdatedAt`. A new `InventoryItemDocTypes` registration.
  **A third "Usage" document or context is forbidden** (the ADR [0057](0057-user-guides-in-help-subtree-and-consistency-loop.md) D1
  *Forbids:* pin, re-applied): the usage history **is** the
  `InventoryCheckout` records (D4).
- **D2 — The `InventoryItem → AuditableResource` adapter is the *only* new
  authorization surface.** A new `InventoryItemToAuditableResource`
  adapter (the ADR [0006](0006-module-boundary-contracts.md) §B.2 `PageToAuditableResource` shape): the read
  decision is **always** the frozen `Audience` (the ADR [0001](0001-core-stack-and-identity-model.md)-B / ADR [0061](0061-email-and-notification-language.md)
  path); the **write** standing (create / edit / delete /
  `CheckedOut` toggle) is **owner ∪ `GlobalAdmin`** (the C-M2·3 shape,
  **no** `Moderator` standing). **No** new `AccessAction` / `AccessVia` /
  `Decide()` branch / `IAuthorizationService` method (the ADR [0006](0006-module-boundary-contracts.md) §B.2
  frozen-base pin).
- **D3 — The `OwnerKind` is a *label*, not a read gate.** The
  `OwnerKind` is an additive `InventoryItemKind` constant string (the M5
  `KanbanStatuses` shape) — the "ladder/grill/room/tool/other" taxonomy.
  It is a **display** field (the `ItemCardView.OwnerKind` read, the UI
  grouping on the `Index` view) — **not** a read gate and **not** a
  standing decision (the standing is D2). The closed set is
  `InventoryItemKinds.Known` (the M5 `KanbanStatuses.Known` shape), the
  `KwLRegistryConsistencyTests` pin re-applied to
  `inventory.kind.{kind}` keys.
- **D4 — The `InventoryCheckout` is a new additive document, the usage
  history, not a re-derivation.** A new `InventoryCheckout` doc: `DocId` +
  `ItemId` + `UserId` (the *user*, not the *owner*) + `CheckedOutAt` /
  `CheckedInAt` (nullable — open until stamped) + `Notes` (the ADR [0018](0018-ugc-authored-in-language-tag.md)
  shape) + `LanguageCode`. A new `InventoryCheckoutDocTypes` registration.
  The checkout is the **data** the README row names as the "optional
  `Usage` history" — a *record* of a use (who used it when, for what),
  **not** a re-derivation from the item's state and **not** a third
  "Usage" store (the ADR [0057](0057-user-guides-in-help-subtree-and-consistency-loop.md) D1 *Forbids:* pin).
- **D5 — The `InventoryItemService` is the owner-scope write seam.** The
  *owner* edits/deletes/reorders their own items and marks
  `CheckedOut` ↔ `Available`; the `GlobalAdmin` does the same for **any**
  item; a plain resident gets the **read** (D2) + the **additive**
  `InventoryCheckout` record (D4) — they can *record* a use but not *edit*
  the item. Standing resolved by the `IAuthorizationService.CanAsync` /
  `CanSeeAsync` frozen seams (D2), **not** a new `Decide()` branch.
- **D6 — The `Views/Inventory/` surface is a flat catalogue,
  server-rendered.** The `Index` view is the catalogue (a list of items
  **grouped by `OwnerKind`** (D3), the per-item `ItemCardView` with the
  usage count from D4, the `New item` / `Edit` / `Delete` / `Record a use`
  actions gated by D2/D5). The `New` / `Edit` views are the item form. The
  `History` view is the usage log (the D4 records). The M5 `Project`
  tab shape, the ADR [0062](0062-admin-surface-split.md) tab shape, the ADR [0043](0043-system-pages-shipped-and-discoverable.md)/0044 server-rendered
  constraint, the ADR [0031](0031-wysiwyg-editor-and-toolbar.md) tsc-only / no-dependency build.
- **D7 — The close flip (U06).** `Milestones.cs` M16 `StatusNext` →
  `StatusDone` + M17 `StatusPlanned` → `StatusNext`; `MilestonesTests.cs`
  re-pinned; the README Roadmap + M16 Status line; [`docs/STATUS.md`](../STATUS.md);
  [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md)'s value-chain table gains the M16 row. All four
  surfaces ship **in the same unit** (the M15/U10 docs-parity precedent).
  See the handoff note for the full detail.
- **D8 — The *nudge on checkout* is a follow-on lane, not part of M16.**
  The README row's "optionally nudge the owner" is a **notification** (the
  M6 `INotificationService` emitter idiom) — a *new* trust boundary M16
  deliberately **does not** ship (the "optionally" is the **usage
  history** — F3). Own ADR as a new trust boundary.

## Consequences

- **C-M16·1** — The item is a `Community__`-scoped flat catalogue, not a
  `Project` sub-surface (D1, D3). The `OwnerKind` is a **label** (D3),
  **not** a read gate or a standing decision (the standing is D2).
- **C-M16·2** — The adapter is the *only* new authorization surface (D2).
  The read is the frozen `Audience`; the write is owner ∪ `GlobalAdmin`.
  **No** new `AccessAction` / `AccessVia` / `Decide()` branch /
  `IAuthorizationService` method.
- **C-M16·3** — The usage history is the `InventoryCheckout` records, not
  a third "Usage" store (D4). **A third "Usage" document or context is
  forbidden** (the ADR [0057](0057-user-guides-in-help-subtree-and-consistency-loop.md) D1 *Forbids:* pin).
- **C-M16·4** — The standing is owner ∪ `GlobalAdmin`, no `Moderator`
  (D2, D5). The owner manages their own items; the `GlobalAdmin` any; a
  plain resident gets read + record-a-use. **No** `Moderator` standing
  (the ADR [0040](0040-pages-system-and-user-kinds-and-blog-feed.md) retired-`Moderator` precedent).
- **C-M16·5** — The surface is server-rendered, tsc-only, no new dependency
  (D6).
- **C-M16·6** — M16 is a *milestone*, not a *lane*: **always available**,
  **not** an off-by-default admin toggle (the M9 `LocaleSettings` toggle
  *lane* shape is **not** re-applied).
- **C-M16·7** — The close flip is the same-unit docs parity (D7):
  `Milestones.cs` / README / `MilestonesTests.cs` / [`STATUS.md`](../STATUS.md) /
  [`ARCHITECTURE.md`](../ARCHITECTURE.md) all ship in the same unit (the M15/U10 precedent).
- **FACES (F1–F5):** F1 — browse the catalogue (grouped by `OwnerKind`)
  and see availability + usage count. F2 — owner/`GlobalAdmin`
  create/edit/delete items and mark `CheckedOut` ↔ `Available`. F3 —
  record a use (the `InventoryCheckout` record); the usage count updates.
  F4 — read the usage history (the D4 records). F5 — the standing is
  owner ∪ `GlobalAdmin`; the adapter is the only new authorization surface.
- **The named trade:** M16 buys *a catalogue + a usage history + a
  standing* with **one additive `InventoryItem` doc + one additive
  `InventoryCheckout` doc + one `InventoryItemKinds` closed set + one
  adapter + one `InventoryItemService` seam + one `Views/Inventory/`
  surface**, in exchange for **no notification (D8), no third "Usage"
  store (C-M16·3), no `Moderator` standing (C-M16·4), no `Project`
  sub-surface (C-M16·1), no new client dependency (C-M16·5)** — M16 is
  *the catalogue + the history + the standing*, **not** an *inventory
  system* (no reservation, no nudge, no recurring use, no multi-owner —
  each a follow-on lane).
- **The acceptance tests (GATE-1…GATE-6):** GATE-1 — the two additive docs
  (C-M16·1, D1/D4). GATE-2 — the adapter is the only new authorization
  surface (C-M16·2, D2). GATE-3 — owner ∪ `GlobalAdmin`, no `Moderator`
  (C-M16·4, D2/D5). GATE-4 — usage history is the `InventoryCheckout`
  records (C-M16·3, D4). GATE-5 — server-rendered, tsc-only (C-M16·5, D6).
  GATE-6 — the close flip is same-unit (C-M16·7, D7).
- **The deferred lanes (each a named follow-on, own ADR):** (1) the *nudge
  on checkout* (D8) — own ADR. (2) the per-item *translation* lane — M16's
  item is **single-language** by design. (3) the *media / attachments* lane
  — M16's item is **text + ownership**. (4) the *recurring / reserved*
  check-out lane — a new document + transition + standing matrix. (5) a
  *third "Usage" document or context* — **forbidden** (C-M16·3).
