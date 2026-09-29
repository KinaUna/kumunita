# Plan: M16 — Inventory (check-out / check-in + usage history)

> **M16 is the current in-progress milestone** (`Milestones.cs`: `M16` = `StatusNext`,
> `M17`/`M18` = `StatusPlanned`). This register is the **milestone-level** plan; each
> unit is a separate **self-contained** file under `docs/plans-milestones/in-progress/`
> (`m16-u00.md` … `m16-u06.md`) that a fresh agent can execute reading **only its own
> unit plan + its entry reads**. When a unit is done, its plan file is moved to
> `docs/plans-milestones/done/`.
>
> **Handoff notes (scratch tier):** `docs/plans-milestones/in-progress/m16-inventory-handoff-notes.md`
> — `## U#` sections appended (never rewritten) as each unit completes; a new agent
> reads the relevant `## U#` sections *after* its own unit plan for context.
>
> **Design doc (primary tier):** `docs/design/m16-inventory-design.md` — authored by
> **U00** (this register's D1–D8, C-M16·*, F1–F5 + the named trade, and the §kw-l
> key list are [PROPOSED] here and **locked verbatim** into the design doc by U00).
> Every later unit reads the relevant design-doc section **verbatim**, not this
> register.
>
> **ADR:** `docs/adr/0117-inventory.md` (confirm **0117** is free against
> `docs/adr/README.md` — the index currently ends at **0116**) — authored by **U00**,
> `Status: Accepted`, with its row appended to the `docs/adr/README.md` index after
> 0116.
>
> **Unit register:** `docs/plans-milestones/in-progress/m16-u00.md` … `m16-u06.md`.
> Each unit plan states its own goal, entry reads, deliverables, and exit. A unit
> agent reads **only its unit plan + its entry reads** — it does **not** need the
> register. The unit plans are the source of truth for *how*; this register is the
> source of truth for *what* (the [PROPOSED] decisions U00 locks).
>
> **U00 is the sign-off gate.** No code unit (U01+) may start until U00 has locked
> the decisions into `docs/design/m16-inventory-design.md` and `docs/adr/0117-inventory.md`.
>
> **[PROPOSED] veto window.** The D1–D8 below are [PROPOSED] and **lockable** (U00
> promotes them to Accepted in ADR 0117 + the design doc). A veto window applies: if a
> decision is vetoed before U00 locks it, the register + the affected unit plans are
> amended and the veto is recorded in the handoff notes. After U00 locks, a change is a
> **drift event** (design doc §drift-guard).
>
> **Atomicity contract (this register is sized for ~32K-context agents).** Every unit is
> small enough that an agent with a ~32K token context window can read **only its own
> unit plan + its entry reads**, execute, and hand off. Concretely, per unit:
>
> - **Entry reads: 4–8 files** (named, with a one-line why each). No "read the whole
>   project."
> - **Deliverables: ≤ 7 small files** (each named, with the exact required content or
>   the pinned test names it must contain).
> - **Exit: one `dotnet build Kumunita.slnx -c Debug`** + **one** `dotnet exec` test
>   assembly run (the relevant test project), both green. A unit that needs a second
>   test assembly to prove its exit is **too big** — split it.
>
> **Unit-series rules (apply to every unit):**
>
> 1. **Never touch files outside your own Deliverables list.** If a change is needed in
>    another unit's files, note it in the handoff notes under *your* `## U#` section and
>    stop — do not edit it.
> 2. **Never rewrite the design doc outside its §drift-guard.** U00 authored it; U01+
>    read it verbatim. A change is a drift event.
> 3. **No tests beyond the pinned list.** A unit adds **only** the pinned tests named in
>    its Deliverables; it does not re-run or re-name tests from earlier units.
> 4. **The frozen seams are never extended.** `IAuthorizationService` (4-method surface +
>    the group lane), `AccessAction` (`Read`/`Moderate` only), `AccessVia` (Owner/Admin +
>    the existing values), and `Decide()` are frozen. M16 adds an **adapter**
>    (`InventoryItemToAuditableResource`), **not a branch** (C-M16·4).
> 5. **M16 is a core milestone surface, not a lane.** It has **no** off-by-default admin
>    toggle and **no** `LocaleSettings`-style admin switch (D6 / C-M16·6) — it is always
>    available to the community, like M5 Projects.
>
> **The world seam (what M16 builds on):** M16 is a **new bounded context**
> (`Kumunita.Core.Inventory`) with **two documents** (`InventoryItem`,
> `InventoryCheckout`) on a **new `M16DocTypes`** surface (the M9/M5 precedent), an
> **adapter** onto the **frozen** `IAuthorizationService` (the M5 `ProjectToAuditableResource`
> shape — an *adapter*, not a *branch*), and the **`inv.*` `kw-l` registry** + the 4
> parity tests (the M5 `projects.*` key shape). It composes **only** the frozen seams —
> `IAuthorizationService` + `IUserInfoService` (the standing probe) — over the host-registered
> Marten `IDocumentStore`. It does **not** touch a new `AccessAction`, a new `AccessVia`,
> a new `Decide()` branch, a new translation table, or a notification lane (D8).

---

## Understanding

**What M16 does (README, verbatim):** *Inventory: check-out / check-in shared,
community-owned, or private resources (equipment, clothes for sports teams, books, etc.),
track where items are, and optionally how much they are used by whom.*

So M16 is a **standalone core surface** (not a group/post feature) that tracks a set of
**items** a community can share:

- **An item** (`InventoryItem`) — a thing that can be checked out: a name, an **ownership
  kind** (`shared` / `community` / `private`), an optional description, an **audience**
  (visibility — the exact `Post` `Audience`, `null` = public), a `componentId` (a feed
  filter, never a gate), an `authorId` (the standing owner), and a **current holder**
  (`CurrentHolderId`, `null` = in the pool / at home).
- **A check-out** (`InventoryCheckout`) — the append-only usage record: which item, which
  borrower, when it went out, when (if ever) it came back, an optional note. The
  **history** of a item's checkouts is the "optionally how much they are used by whom"
  surface.
- **The transition** — check-out sets `CurrentHolderId` + appends a checkout record;
  check-in clears it + closes the open record. One open checkout per item at a time
  (enforced at the DB layer by a unique index — the M9 `convo_uidx_pair` shape).
- **Usage history** — the read-only aggregate of a item's checkout records (the "how much
  … by whom" surface).

**What M16 is NOT (deferred lanes, each its own ADR):**

- **No notifications.** The optional "nudge the item's owner when it is checked out" lane
  (the M6 `INotificationService` emitter idiom) is **deferred** — a follow-on lane with its
  own ADR (D8). M16's core is the state transition + the history; it does not emit
  notifications and does not register a Wolverine handler.
- **No per-item translations.** A item's name/description are single-language in M16 (the
  `LanguageCode` authored-in tag shape is present for display, but a full translation lane
  like M5's `TodoTranslation` is **deferred** — its own ADR).
- **No media / attachments on items.** The M3 ADR 0025 / ADR 0034 image + attachment lanes
  are **deferred** for M16 (a item is text + ownership in M16).
- **No recurring / reserved check-outs** (a pre-booked slot, a hold) — a follow-on lane,
  its own ADR.
- **No new bounded context beyond `Kumunita.Core.Inventory`.** The usage history is the
  append-only `InventoryCheckout` records inside the **same** context (D4), not a
  separate "Usage" context.

---

## Assumptions / decisions — [PROPOSED, lockable by ADR 0117 in U00]

> U00 locks these into `docs/design/m16-inventory-design.md` + `docs/adr/0117-inventory.md`
> (verbatim) and records any veto. A `*Forbids:*` tail names the drift the decision
> guards against.

- **D1 — Two documents, one new surface, no new bounded context.** M16 adds a new bounded
  context `Kumunita.Core.Inventory` with **two documents**: `InventoryItem` (the thing
  being tracked) and `InventoryCheckout` (the append-only usage record), registered on a
  **new `M16DocTypes`** surface (the M9/M5 `*DocTypes` precedent — a parallel additive
  surface, delta-detected and applied idempotently at boot, **zero migrations** for
  existing surfaces). The usage history is the `InventoryCheckout` records *inside* the
  same context — **not** a separate "Usage" bounded context.
  *Forbids:* a third "Usage" document/context for the history; a migration of an existing
  surface; a `M15DocTypes`-style additive-on-an-old-surface shape.

- **D2 — Access = the frozen `IAuthorizationService` + an adapter, zero new branch.**
  M16 adds an **adapter** `InventoryItemToAuditableResource` (the **exact** M5
  `ProjectToAuditableResource` 6-member projection — `Id`/`Name`/`OwnerId`/`Audience`/
  `ComponentId`/`TargetKind`; the only difference is `TargetKind = "inventory"`) and
  routes **every** read decision through the **frozen** `IAuthorizationService`
  (`CanAsync(Read)` detail / `CanSeeAsync(Read)` list). **No** new `AccessAction`, **no**
  new `AccessVia`, **no** new branch in `Decide()`, **no** group-lane method on
  `IAuthorizationService` (inventory is not a group surface).
  *Forbids:* a new `AccessAction`/`AccessVia`; a new `Decide()` branch; a new method on
  `IAuthorizationService` for inventory; reading group membership for access in the
  Inventory context.

- **D3 — Ownership kind is a label, never an access gate.** `InventoryItem.OwnerKind`
  ∈ {`shared`, `community`, `private`} is a **string** (the M5 `KanbanStatuses`
  string-not-enum shape) that drives the **UI grouping** and the **write standing**
  breadth (D4) — it **never** gates reads. The read decision is always the item's
  `Audience` via the frozen service (D2). The list view filters candidates by
  `OwnerKind` (a **filter, never a gate** — the `componentId` C-M3·2 discipline), but the
  audience decision is unchanged.
  *Forbids:* `OwnerKind` as an enum; `OwnerKind` deciding a read/access decision; a
  `Decide()` branch keyed on `OwnerKind`.

- **D4 — Check-out / check-in = an atomic state transition + an append-only record.**
  Check-out sets `InventoryItem.CurrentHolderId` (+ the checkout record's open state) and
  **appends** an `InventoryCheckout`; check-in clears `CurrentHolderId` and **closes** the
  open record (sets `CheckedInAt`). The checkout record is **append-only** — never
  mutated or deleted (the "usage history" is this record set). At most **one open
  checkout per item** at a time, enforced at the DB layer by a **unique partial index** on
  `(ItemId)` where `CheckedInAt IS NULL` (the M9 `convo_uidx_pair` F1 idempotency shape).
  *Forbids:* a separate "usage" table/context; mutating/deleting a closed checkout record;
  a second open checkout of the same item (the index is the witness).

- **D5 — The write standing is a server-side probe, never a new `AccessVia`.** Who may
  create / edit / delete an item, and who may check it out / in, is decided by a **pure
  server-side standing probe** in the service (composing the frozen `IAuthorizationService`
  "can see it" + `IUserInfoService` for the role probe): **creator ∪ GlobalAdmin** over an
  item's create/edit/delete; and the **broad** standing — **any member who can see it** —
  over a `community`/`shared` item's check-out/check-in (the M5 `ClaimTodoAsync` broad
  standing precedent). The Web `[Authorize]` attribute is a **convenience pre-gate only**,
  never the source of truth. No new `AccessVia` value beyond the existing set (the TG
  ADR 0044 "no new AccessVia beyond Owner/Admin" precedent).
  *Forbids:* a new `AccessVia` value; the Web `[Authorize]` as the source of truth; a
  standing that lets a non-visible actor check out an item.

- **D6 — M16 is a core surface, not a lane: no off-by-default toggle.** M16 is a standing
  core surface (like M5 Projects): **no** off-by-default admin toggle, **no**
  `LocaleSettings`-style admin switch, **no** `KumunitaFeature` admin flag. It is always
  available to the community. (M9's off-by-default toggle was a *lane*; M16 is a
  *milestone*.)
  *Forbids:* an off-by-default admin toggle for M16; a `LocaleSettings`-style switch; an
  admin flag gating the surface.

- **D7 — The close flip is one atomic unit.** The M16 close (U06) flips the roadmap parity
  **together**: `Milestones.cs` M16 `StatusNext`→`StatusDone`, M17 `StatusPlanned`→
  `StatusNext`; `MilestonesTests.cs` re-pinned to
  `M17_Is_The_Single_InProgress_Milestone` + the shipped list gains M16 + M18 stays
  planned; the README **Roadmap** (M16 → done, M17 → in progress) + the M16 **Status**
  line; `docs/STATUS.md`; and `docs/ARCHITECTURE.md`'s value-chain table gains the **M16
  row** (the table currently ends at the M15 row). All four surfaces ship **in the same
  unit** (the M15/U10 docs-parity precedent).
  *Forbids:* reordering M17/M18; editing a shipped milestone's text; breaking the
  `MilestonesTests` pins; flipping one surface and not the others.

- **D8 — No notification lane in M16 (deferred).** M16 composes **only** the frozen
  `IAuthorizationService` + `IUserInfoService` (the standing probe) over the
  host-registered Marten `IDocumentStore`. It does **not** take an `INotificationService`
  emitter, does **not** register a Wolverine handler, and does **not** emit a nudge on
  check-out/check-in. The "nudge the owner on checkout" lane is **deferred** — a follow-on
  lane with its own ADR (the README's "optionally" is satisfied by the usage history, not
  by notifications).
  *Forbids:* an `INotificationService` in the `InventoryService` composition; a Wolverine
  handler for inventory; a nudge emission in M16.

---

## Invariants (C-M16·1 … C-M16·7) — U00 locks these verbatim

- **C-M16·1 — The access model is the frozen seam + the adapter.** Every inventory read
  decision (detail **and** list) goes through the **frozen** `IAuthorizationService`
  (`CanAsync(Read)` / `CanSeeAsync(Read)`) over `InventoryItemToAuditableResource`
  (`TargetKind = "inventory"`); `OwnerKind` is **never** an access gate (D2/D3). M16 adds
  an *adapter*, not a *branch*.
- **C-M16·2 — Every read decision audits, in-transaction.** Every inventory read decision
  (detail: a single-target row, `TargetKind = "inventory"`, `TargetId =` the item id;
  list: one **aggregate** row, `visibleCount`/`hiddenCount`, `TargetId` null) writes
  **exactly one** `AccessAudit` row — **Allow and Deny** — in the **same transaction** as
  any concurrent domain write (invariant C3, via the `IDocumentSession` overloads).
- **C-M16·3 — Append-only history; atomic transition.** A check-out is a state transition
  on `InventoryItem` (`CurrentHolderId` set) that **appends** an `InventoryCheckout`; a
  check-in clears `CurrentHolderId` and **closes** the open record. Checkout records are
  **append-only** — never mutated or deleted (D4). At most **one open checkout per item**
  (the unique partial index is the witness).
- **C-M16·4 — Zero new `AccessAction`/`AccessVia`/`Decide()` branch.** M16 adds **no**
  `AccessAction`, **no** `AccessVia` value, **no** branch in `Decide()`, **no** method on
  `IAuthorizationService` (D2). The write standing (D5) is a pure server-side probe, never
  a new `AccessVia`.
- **C-M16·5 — Visibility is the Audience; standing is the action.** A item is only ever
  listed/seen if the actor passes the `CanSeeAsync(Read)` pass (the list's single
  aggregate row is the C-M3·3 shape); `OwnerKind` narrows the candidate set (a filter,
  never a gate — C-M3·2) but does **not** change the audience decision. The **action**
  decision (may this actor check out / edit / delete) is the D5 standing probe — the two
  never leak into each other (a resident can *see* a shared item; the *standing* is what
  lets them *act*).
- **C-M16·6 — M16 is a standing core surface (no toggle).** M16 has **no** off-by-default
  admin toggle and **no** `LocaleSettings`-style admin switch (D6); it is always
  available to the community.
- **C-M16·7 — The close flip is atomic.** The M16 close (U06) flips the four surfaces —
  `Milestones.cs` (M16→done, M17→next), `MilestonesTests.cs` (re-pinned to
  `M17_Is_The_Single_InProgress_Milestone` + shipped list + M18 planned), the README
  Roadmap/Status, and `docs/STATUS.md` + the `docs/ARCHITECTURE.md` value-chain M16 row —
  **in the same unit** (D7).

---

## FACES (F1–F5) + the named trade — U00 refines

- **F1 — The check-out / check-in transition is atomic.** The item's `CurrentHolderId`
  flips **and** the checkout record is appended/closed in **one commit**. A concurrent
  double-check-out of the same item resolves to **one** winner: the unique partial index
  on `(ItemId)` where `CheckedInAt IS NULL` (the M9 `convo_uidx_pair` shape) makes the
  second commit fail (a caught conflict → the loser sees "already checked out"). The
  transition and its audit row commit or roll back together (C-M16·3).
- **F2 — The list is one `CanSeeAsync(Read)` pass (C6) with one aggregate audit row,
  filtered by `OwnerKind`** (a filter, never a gate — C-M3·2 / C-M16·5). One group-load,
  one matching pass, one aggregate `AccessAudit` row (`visibleCount`/`hiddenCount`).
- **F3 — The usage history is the append-only checkout records** (C-M16·3) — a read-only
  aggregate per item; no writes beyond the checkout/check-in transition. "Optionally how
  much … by whom" is satisfied by this history, not by a separate mechanism.
- **F4 — Every write standing is a server-side probe** (creator ∪ GlobalAdmin over an
  item's create/edit/delete; any member who can see it over a community/shared
  check-out/check-in) — the Web `[Authorize]` is a convenience pre-gate only (C-M16·5,
  D5).
- **F5 — M16 is a standalone core surface** (C-M16·6 / D6): it does not depend on a lane
  being on; the only external composition is the frozen `IAuthorizationService` +
  `IUserInfoService`.

**The named trade — the private-vs-shared write-standing breadth.** A `community`/`shared`
item lets **any member who can see it** check it out (a broad standing — frictionless
community lending), while a `private` item restricts the standing to the **owner ∪
GlobalAdmin** (the M5 creator-standing shape). The trade: a broad standing for shared
items is more frictionless, but it means **"who can act"** is a standing probe over
`OwnerKind` (D5), **not** a pure audience decision. The invariant that keeps this safe is
the **C-M16·5 split**: the *visibility* decision is **always** the frozen Audience (D2),
and the *action* decision is **always** the standing probe (D5) — no code path lets one
decide the other. A resident can see a shared item (audience) but the standing probe is
what lets them act; and a private item's owner can act even where a member cannot. This is
the named trade U00 must carry into the design doc (the M5 creator-vs-broad-standing
trade, C-M5·6).

---

## Approach

Three tracks + a close, each unit atomic and self-contained. **U00 is the sign-off gate
(no code unit may start before it).**

**Track A — Docs (U00).** The design doc (decisions/invariants/FACES + trade/§kw-l key
list/gate tests/§drift-guard) + ADR 0117 + the ADR index row.

**Track B — Core (U01, U02, U03).** U01 the context docs + `M16DocTypes` + the adapter.
U02 the read lanes + the service seam + the request DTOs + the DI registration. U03 the
write lanes (create / edit / delete / check-out / check-in) + the standing probes + the
atomic transition.

**Track C — Web (U04, U05).** U04 the controller + view models + the list/detail/create
views + the `inv.*` `kw-l` keys (× en/de/fr/da). U05 the edit/delete actions + the
check-out/check-in action buttons + the usage-history section + the nav entry.

**Close (U06).** The three acceptance gate tests + the docs parity flip (the D7 four
surfaces, in the same unit).

**Unit map** (sized for ~32K):

| Unit | Track | One-line goal |
|---|---|---|
| **U00** | A | Design doc + ADR 0117 (lock D1–D8, C-M16·*, F1–F5 + trade, §kw-l, gate tests, §drift-guard) |
| **U01** | B | `Kumunita.Core.Inventory` context: `InventoryItem` + `InventoryCheckout` + `M16DocTypes` (+ the open-checkout unique index) + the adapter |
| **U02** | B | Read lanes (`IInventoryService` + `InventoryService` list/detail/history) + `InventoryRequests.cs` + the DI registration + pinned Core tests |
| **U03** | B | Write lanes (create / edit / delete / check-out / check-in) + the standing probes + the atomic transition + pinned Core tests |
| **U04** | C | `InventoryController` + view models + list/detail/create views + the `inv.*` `kw-l` keys (×4) + pinned Web tests |
| **U05** | C | edit/delete actions + check-out/check-in buttons + usage-history section + the nav entry + pinned Web tests |
| **U06** | Close | the three acceptance gate tests + the docs parity flip (D7, four surfaces in one unit) |

---

## Workflow (per unit, sized for ~32K)

Each unit agent:

1. Reads **its own unit plan** (`docs/plans-milestones/in-progress/m16-uNN.md`) in full.
2. Reads the **entry reads** listed in the unit plan (4–8 files, named, with a one-line
   why each).
3. If a **handoff note** exists for an earlier unit the current one depends on, reads that
   `## U#` section of `docs/plans-milestones/in-progress/m16-inventory-handoff-notes.md`.
4. Executes the **deliverables** (≤ 7 small files) named in the unit plan.
5. Runs the **exit gate**: `dotnet build Kumunita.slnx -c Debug` **+** the relevant
   `dotnet exec` test assembly, both green.
6. **Appends** a `## U# — <one-line summary>` section to the handoff notes (the state it
   left the codebase in, any drift observed, any follow-on noted) — **never** rewrites an
   earlier `## U#` section.
7. Moves its own unit plan from `docs/plans-milestones/in-progress/` to
   `docs/plans-milestones/done/`.

**Running the tests (the house quirk — AGENTS.md):** `dotnet test` and VS Test Explorer
are **broken on this machine** (Zero tests ran / Exit 5 — a runner/discovery bug, not a
real failure). The reliable path is to build, then run each test assembly in-process
through xunit.v3's own runner:

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

`Kumunita.Core.Tests` takes ~20 s (it starts `postgres:18` via Testcontainers; clean up
Docker containers with `docker container prune` if the process is killed).
`Kumunita.Web.Tests` runs in well under a second. A unit's **exit gate** runs **only the
relevant** test assembly (U01–U03: Core.Tests; U04–U05: Web.Tests; U06: **both**).

**PowerShell safety (the house quirk — copilot-instructions.md):** no here-strings via the
terminal; `$variables` do **not** survive between separate terminal commands (the
fresh-process bug) — one self-contained command or a `.ps1`; keep commands single-line;
use `git --no-pager`.

---

## U00 — Design doc + ADR 0117 (the sign-off gate)

**Goal.** Author `docs/design/m16-inventory-design.md` (the **primary-tier** doc that
locks this register's [PROPOSED] D1–D8, C-M16·1–7, F1–F5 + the named trade, the §kw-l
key list, the three gate tests, and the §drift-guard) + `docs/adr/0117-inventory.md`
(`Status: Accepted`) + the `docs/adr/README.md` index row (after 0116). **This is the
sign-off gate — no code unit may start until it is done.**

**Entry reads (6).**
1. `docs/plans-milestones/plan-m16-inventory.md` (this register) — the [PROPOSED]
   decisions to lock.
2. `docs/plans-milestones/done/m15-u00.md` — the M15 U00 design-doc unit plan (the
   structure + the §drift-guard / §kw-l / §gate section convention to mirror).
3. `docs/adr/README.md` (the index, through 0116) — to confirm **0117** is free and to
   append the row in the right place.
4. `docs/adr/0116-*.md` (the M15 ADR) — the ADR shape (Status, Context/Decision/
   Consequences) to mirror.
5. `src/Kumunita.Core/Projects/ProjectToAuditableResource.cs` — the exact adapter
   shape (the 6-member projection) the design doc's §9.2 (adapter) section pins.
6. `src/Kumunita.Core/M9DocTypes.cs` — the exact `*DocTypes` surface shape the design
   doc's §2 (documents) section pins (the M9 `convo_uidx_pair` unique-index shape).

**Deliverables (3 files).**
1. `docs/design/m16-inventory-design.md` — the full design doc, with **at least** the
   sections: `## Context`; `## Goals / Non-goals`; `## Parts affected`;
   `## Seams & contracts (mandatory)`; `## Decisions (D1–D8, locked — ADR 0117)` (the
   register's D1–D8 **verbatim**); `## Invariants (C-M16·1–7, locked verbatim)`;
   `## FACES (F1–F5) + the named trade`; `## §kw-l — the key list` (the `inv.*` keys ×
   en/de/fr/da, the U04 source of truth); `## §gate — the three acceptance tests`
   (closed-loop / handoff-authorization-boundary / part-vs-whole-audit-completeness);
   `## §deferred` (the D8 notifications lane + the per-item-translation lane + the media
   lane + the recurring-checkout lane, each "own ADR"); `## §drift-guard — the frozen
   pins + the drift log`.
2. `docs/adr/0117-inventory.md` — `Status: Accepted`; Context (the README M16 row);
   Decision (D1–D8, the two documents + `M16DocTypes` + the adapter + the standing probe
   + the core-surface + no-notification stance); Consequences (the C-M16·* invariants).
3. `docs/adr/README.md` — the index row for **0117** appended **after** the 0116 row
   (confirm 0117 is free first).

**Exit.** Docs-only — **no build gate, no test gate** (the M15/U00 precedent: a docs
unit). Verify the three files exist and the ADR index row is in the right place. Append
a `## U00 — design doc + ADR 0117 locked` section to the handoff notes (the locked
decisions, the key list, the gate tests). **No code unit may start until this is done.**

---

## U01 — The Inventory context (documents + `M16DocTypes` + the adapter)

**Goal.** Create the `Kumunita.Core.Inventory` bounded context: the two documents
(`InventoryItem`, `InventoryCheckout`), the `M16DocTypes` surface (with the
open-checkout unique partial index), and the `InventoryItemToAuditableResource` adapter —
all registered in `Program.cs`.

**Entry reads (7).**
1. `docs/design/m16-inventory-design.md` — §2 (documents) + §9.2 (adapter), **verbatim**.
2. `src/Kumunita.Core/Projects/TodoItem.cs` — the document shape to mirror (the
   `Title`/`ComponentId`/`AuthorId`/`Audience`/`IsDeleted`/`LanguageCode`/`Created`/
   `Modified` field set).
3. `src/Kumunita.Core/Messaging/Conversation.cs` — the M9 document + the `M9DocTypes`
   reference (the `convo_uidx_pair` unique-index shape).
4. `src/Kumunita.Core/M9DocTypes.cs` — the exact `*DocTypes` surface shape (the
   `opts.Schema.For<T>()` + `.UniqueIndex` idiom).
5. `src/Kumunita.Core/Projects/ProjectToAuditableResource.cs` — the exact adapter
   shape (the 6-member projection; `TargetKind` is the only difference).
6. `src/Kumunita.Web/Program.cs` (the `M5DocTypes.Configure(opts)` /
   `M9DocTypes.Configure(opts)` block) — where `M16DocTypes.Configure(opts)` goes.
7. `tests/Kumunita.Core.Tests/` (an existing doc-registration test, if present) — the
   pinned-test shape for the new surface.

**Deliverables (6 files).**
1. `src/Kumunita.Core/Inventory/InventoryItem.cs` — the item document: `Id` (string,
   surrogate), `Name` (non-empty — the card label + the adapter `Name`), `OwnerKind`
   (string: `shared`/`community`/`private`, the M5 `KanbanStatuses` shape), `Description`
   (optional Markdown), `ComponentId` (a feed filter, never a gate — C-M3·2), `AuthorId`
   (the standing owner — D5), `Audience` (the exact `Post` `Audience`, `null` = public —
   D2/D3), `CurrentHolderId` (nullable — who it's currently with; `null` = in the pool —
   D4), `IsDeleted` (the ADR 0024 soft-delete flag), `LanguageCode` (the ADR 0018
   authored-in tag), `Created`, `Modified`.
2. `src/Kumunita.Core/Inventory/InventoryCheckout.cs` — the append-only usage record:
   `Id`, `ItemId`, `BorrowerId`, `CheckedOutAt`, `CheckedInAt` (nullable — open when
   null), `Note` (optional).
3. `src/Kumunita.Core/M16DocTypes.cs` — the surface: `opts.Schema.For<InventoryItem>()`
   (the `(ComponentId, Created)` + `(OwnerKind, Created)` feed/filter indexes) +
   `opts.Schema.For<InventoryCheckout>()` (the `(ItemId, CheckedOutAt)`
   thread-ordering index + the **unique partial index** on `(ItemId)` where
   `CheckedInAt IS NULL` — the F1 idempotency witness, the M9 `convo_uidx_pair` shape).
4. `src/Kumunita.Core/Inventory/InventoryItemToAuditableResource.cs` — the adapter (the
   **exact** `ProjectToAuditableResource` 6-member projection; `TargetKind = "inventory"`;
   `OwnerId = AuthorId`, `Audience = Item.Audience` projected verbatim).
5. `src/Kumunita.Web/Program.cs` — `M16DocTypes.Configure(opts);` added to the
   `*DocTypes.Configure(opts)` block (after `M9DocTypes.Configure(opts)`).
6. A pinned Core test (in `tests/Kumunita.Core.Tests/`) — the `M16DocTypes` surface
   registers both documents + the unique partial index (the M9/M5 doc-registration test
   shape).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green **+** `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` green. Append
`## U01 — context docs + M16DocTypes + adapter` to the handoff notes (the field set, the
index set, the adapter's `TargetKind`).

---

## U02 — Read lanes + the service seam + request DTOs + DI

**Goal.** Add the `IInventoryService` + `InventoryService` **read lanes** (list by
`OwnerKind` filter + `CanSeeAsync(Read)`; detail + `CanAsync(Read)`; usage-history read),
the `InventoryRequests.cs` DTOs, and the `DependencyInjection.cs` registration — the M5
`IProjectService` read-lane shape (the write lanes are U03).

**Entry reads (7).**
1. `docs/design/m16-inventory-design.md` — the read-lane contracts (list/detail/history),
   **verbatim**.
2. `src/Kumunita.Core/Projects/IProjectService.cs` (the read-lane block) — the exact
   read-lane shape (the `CanSeeAsync(Read)` list + the `CanAsync(Read)` detail + the
   404-vs-403 split).
3. `src/Kumunita.Core/Authorization/IAuthorizationService.cs` — the frozen surface
   (the `CanAsync`/`CanSeeAsync` + the `IDocumentSession` overloads, C3).
4. `src/Kumunita.Core/Projects/ProjectRequests.cs` — the sealed-record DTO shape.
5. `src/Kumunita.Core/DependencyInjection.cs` (the M9/M5 service-registration block) —
   the `AddTransient<IService>(sp => new Service(store, IUserInfoService,
   IAuthorizationService))` shape to mirror.
6. `src/Kumunita.Core/Projects/ProjectPages.cs` — the paged-result shape.
7. `tests/Kumunita.Core.Tests/` (an existing read-lane test) — the pinned-test shape
   (the list aggregate audit row, the 404-vs-403 detail).

**Deliverables (5 files).**
1. `src/Kumunita.Core/Inventory/IInventoryService.cs` — the seam: `ListItemsAsync`
   (candidates = `!IsDeleted`, filtered by the optional `ownerKind` (a filter, never a
   gate — C-M3·2 / C-M16·5) + the optional `componentId`; survivors
   `CanSeeAsync(Read)`-filtered; ordered by `Created` desc; paged; the **aggregate**
   `AccessAudit` row, `TargetKind = "inventory"`), `GetItemAsync` (`CanAsync(Read)`
   detail, the 404-vs-403 split — C-M3·4), `GetHistoryAsync` (the item's checkout records
   ordered by `CheckedOutAt` desc — F3). The write lanes are **not** here (U03).
2. `src/Kumunita.Core/Inventory/InventoryService.cs` — the `InventoryService` read-lane
   implementation (composing `IDocumentStore` + `IUserInfoService` +
   `IAuthorizationService` — the M5/M9 composition shape).
3. `src/Kumunita.Core/Inventory/InventoryRequests.cs` — the request DTOs (the
   sealed-record shape — `CreateItemRequest` with `Name`/`OwnerKind`/`Description`/
   `ComponentId`; the read filters as method params, not DTOs).
4. `src/Kumunita.Core/DependencyInjection.cs` — `services.AddTransient<Inventory.IInventoryService>(sp
   => new Inventory.InventoryService(sp.GetRequiredService<IDocumentStore>(),
   sp.GetRequiredService<IUserInfoService>(), sp.GetRequiredService<IAuthorizationService>()))`
   (the M9 `IMessagingService` registration shape — store + IUserInfoService +
   IAuthorizationService; **no** `INotificationService` — D8).
5. Pinned Core tests (in `tests/Kumunita.Core.Tests/`) — (a) the list's single aggregate
   `AccessAudit` row (`visibleCount`/`hiddenCount`, `TargetKind = "inventory"`); (b) the
   detail's 404-vs-403 split (a non-visible item is a 404, not a 403 — C-M3·4); (c) the
   history read returns the checkout records ordered by `CheckedOutAt` desc (F3).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green **+** `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` green. Append
`## U02 — read lanes + service seam + DI` to the handoff notes (the read-lane contract,
the composition, the pinned tests).

---

## U03 — Write lanes (create / edit / delete / check-out / check-in) + standing probes

**Goal.** Add the `InventoryService` **write lanes** (create / edit / delete / check-out /
check-in), the **standing probes** (D5 — creator ∪ GlobalAdmin over create/edit/delete;
any member who can see it over a community/shared check-out/check-in; owner ∪ GlobalAdmin
over a private check-out/check-in), and the **atomic transition** (F1 — the
`CurrentHolderId` flip + the append/close of the checkout record in one commit, the
unique partial index the F1 witness). The write lanes re-check standing **server-side**
(the Web `[Authorize]` is a convenience pre-gate only — F4).

**Entry reads (7).**
1. `docs/design/m16-inventory-design.md` — the write-lane contracts + the standing
   probes + the F1 atomicity, **verbatim**.
2. `src/Kumunita.Core/Projects/ProjectService.cs` (a write-lane block — the
   `CreateTodoAsync` / standing-probe shape) — the exact write-lane shape (the
   `IDocumentSession` caller-transaction param, the standing probe, the
   `SaveChangesAsync` commit).
3. `src/Kumunita.Core/Authorization/IAuthorizationService.cs` (the `CanAsync` +
   `IDocumentSession` overload) — the C3 same-transaction guarantee the write lanes
   compose.
4. `src/Kumunita.Core/M9DocTypes.cs` (the `convo_uidx_pair` unique index) — the F1
   idempotency-witness shape the check-out lane enforces.
5. `src/Kumunita.Core/Inventory/IInventoryService.cs` (U02) — the seam the write lanes
   extend (the M4 full-interface-first pin — U02 ships the reads, U03 appends the writes;
   a rename is a drift event).
6. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` (the role-probe shape — the
   GlobalAdmin probe) — the standing-probe composition (D5).
7. `tests/Kumunita.Core.Tests/` (an existing write-lane test) — the pinned-test shape
   (the atomic double-check-out, the standing-probe deny, the append-only history).

**Deliverables (5 files).**
1. `src/Kumunita.Core/Inventory/IInventoryService.cs` — **extend** with the write lanes
   (the M4 full-interface-first pin — a rename/re-scope is a drift event):
   `CreateItemAsync` (the standing probe: the actor **is** the creator — `AuthorId` =
   the actor; one `AccessAudit` row, `TargetKind = "inventory"`, `Via Owner/Admin`),
   `EditItemAsync` (the standing probe: creator ∪ GlobalAdmin — D5), `DeleteItemAsync`
   (the standing probe: creator ∪ GlobalAdmin; the ADR 0024 soft-delete flag),
   `CheckOutAsync` (the standing probe: any member who can see it — community/shared;
   owner ∪ GlobalAdmin — private; the **atomic** transition — F1), `CheckInAsync`
   (the standing probe: the current holder ∪ creator ∪ GlobalAdmin; closes the open
   record).
2. `src/Kumunita.Core/Inventory/InventoryService.cs` — **extend** with the write-lane
   implementations (the standing probes compose the frozen `IAuthorizationService`
   "can see it" + `IUserInfoService` "role probe"; the `CheckOutAsync`/`CheckInAsync`
   transitions take the caller's `IDocumentSession` (C3) and commit the
   `CurrentHolderId` flip + the append/close of the checkout record in **one**
   `SaveChangesAsync` — F1; the unique partial index (U01) is the F1 witness — a
   concurrent double-check-out's second commit fails, the loser sees "already checked
   out").
3. `tests/Kumunita.Core.Tests/` — pinned tests: (a) the **atomic double-check-out**
   (two concurrent check-outs of the same item → exactly one wins, the other sees "already
   checked out" — F1 / C-M16·3); (b) the **standing-probe deny** (a non-standing member
   of a private item cannot check it out, the deny `AccessAudit` row commits — D5 /
   C-M16·5); (c) the **append-only history** (a check-out + a check-in produces exactly
   one checkout record with `CheckedOutAt` + `CheckedInAt` set — C-M16·3).
4. `tests/Kumunita.Core.Tests/` — a pinned test for the **create** lane (the actor is the
   creator, `AuthorId` = the actor, one `AccessAudit` row `Via Owner/Admin`).
5. `tests/Kumunita.Core.Tests/` — a pinned test for the **edit/delete** standing probe
   (creator ∪ GlobalAdmin; a non-creator non-GlobalAdmin is denied, the deny row
   commits).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green **+** `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` green. Append
`## U03 — write lanes + standing probes + atomic transition` to the handoff notes (the
standing probes, the F1 witness, the pinned tests).

---

## U04 — Web surface part 1 (controller + list/detail/create + `inv.*` `kw-l`)

**Goal.** Add the `InventoryController` (list / detail / create), the view models, the
three views (`list.cshtml` / `detail.cshtml` / `create.cshtml`), and the `inv.*` `kw-l`
keys registered in `KnownTranslationKeys` (× en/de/fr/da — the U00 §kw-l key list is the
source of truth). The four `KnownTranslationKeys` parity tests +
`KwLRegistryConsistencyTests` pin them automatically.

**Entry reads (7).**
1. `docs/design/m16-inventory-design.md` — the §kw-l key list + the Web-view contracts,
   **verbatim**.
2. `src/Kumunita.Web/Controllers/` (the M5 `ProjectController` or the M9
   `MessagingController`) — the controller shape (the `[Authorize]` convenience pre-gate,
   the view-model mapping, the `IInventoryService` composition).
3. `src/Kumunita.Web/Views/` (the M5/`Projects` views) — the view shape (the Razor
   `@model`, the `kw-l` `@localize` tag helper, the paged-list markup).
4. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the `projects.*` block) —
   the `kw-l` registration shape (the `inv.*` keys go here).
5. `src/Kumunita.Core/Inventory/IInventoryService.cs` (U02/U03) — the seam the
   controller composes.
6. `tests/Kumunita.Web.Tests/` (an existing controller test) — the pinned-test shape
   (the list page renders, the detail page 404-vs-403, the create flow).
7. `docs/design/m16-inventory-design.md` — the §gate tests (the closed-loop /
   handoff-authorization-boundary shapes the Web tests must not break).

**Deliverables (7 files).**
1. `src/Kumunita.Web/Models/InventoryViewModels.cs` — the view models (the list row:
   `Name`/`OwnerKind`/`CurrentHolderName`/`Created`; the detail: the item fields + the
   history; the create form: `Name`/`OwnerKind`/`Description`/`ComponentId`).
2. `src/Kumunita.Web/Controllers/InventoryController.cs` — the controller: `List`
   (the `IInventoryService.ListItemsAsync` + the view-model mapping + the paged markup),
   `Detail` (the `GetItemAsync` + the `GetHistoryAsync` + the 404-vs-403 split),
   `Create` (the `CreateItemAsync` + the `[Authorize]` convenience pre-gate — F4).
3. `src/Kumunita.Web/Views/Inventory/List.cshtml` — the list view (the `@localize`
   `kw-l` tags, the paged markup, the `OwnerKind` filter links).
4. `src/Kumunita.Web/Views/Inventory/Detail.cshtml` — the detail view (the item fields +
   the usage-history section — F3).
5. `src/Kumunita.Web/Views/Inventory/Create.cshtml` — the create form (the `Name`/
   `OwnerKind`/`Description`/`ComponentId` fields, the `@localize` `kw-l` tags).
6. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the `inv.*` keys
   registered (the U00 §kw-l key list, × en/de/fr/da — the `projects.*` block shape).
7. `tests/Kumunita.Web.Tests/` — pinned tests: (a) the list page renders (the
   `OwnerKind` filter + the paged markup); (b) the detail page 404-vs-403 (a non-visible
   item is a 404, not a 403 — C-M3·4); (c) the create flow (a valid create → the item
   appears in the list).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green **+** `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` green. Append
`## U04 — Web surface part 1 (controller + list/detail/create + kw-l)` to the handoff
notes (the view-model shapes, the `inv.*` key list, the pinned tests).

---

## U05 — Web surface part 2 (edit/delete + check-out/check-in + nav)

**Goal.** Add the edit/delete actions, the check-out/check-in action buttons (+
confirmation), the usage-history section (already in the U04 detail view — U05 wires the
action buttons to it), and the **nav entry** (the sidebar "Inventory" link, the M5
`projects` nav-entry shape). The check-out/check-in actions route to the U03 write lanes
(the standing probe is server-side — F4; the Web `[Authorize]` is a convenience
pre-gate only).

**Entry reads (7).**
1. `docs/design/m16-inventory-design.md` — the Web-action contracts + the nav-entry
   contract, **verbatim**.
2. `src/Kumunita.Web/Controllers/` (the M5 `ProjectController` edit/delete actions) —
   the action-button shape (the `[HttpPost]` + the `[Authorize]` convenience pre-gate +
   the redirect-on-success).
3. `src/Kumunita.Web/Views/Shared/` (the sidebar/nav partial) — the nav-entry shape
   (the `@localize` `kw-l` tag + the route link).
4. `src/Kumunita.Web/Views/Inventory/Detail.cshtml` (U04) — the detail view the
   action buttons go into.
5. `src/Kumunita.Core/Inventory/IInventoryService.cs` (U02/U03) — the seam the
   check-out/check-in actions route to.
6. `tests/Kumunita.Web.Tests/` (an existing action test) — the pinned-test shape
   (the check-out action, the check-in action, the nav entry present).
7. `docs/design/m16-inventory-design.md` — the §gate tests (the handoff-authorization-
   boundary shape the action tests must satisfy).

**Deliverables (6 files).**
1. `src/Kumunita.Web/Controllers/InventoryController.cs` — **extend** with the actions:
   `Edit` (the `EditItemAsync` + the `[Authorize]` pre-gate — F4), `Delete` (the
   `DeleteItemAsync` + the `[Authorize]` pre-gate), `CheckOut` (the `CheckOutAsync` +
   the `[Authorize]` pre-gate — the standing probe is server-side), `CheckIn` (the
   `CheckInAsync` + the `[Authorize]` pre-gate).
2. `src/Kumunita.Web/Views/Inventory/Edit.cshtml` — the edit form (the `Name`/
   `OwnerKind`/`Description`/`ComponentId` fields, the `@localize` `kw-l` tags).
3. `src/Kumunita.Web/Views/Inventory/Detail.cshtml` — **extend** (U04) with the
   check-out/check-in action buttons (+ the "currently with" display) + the delete
   button (the `[Authorize]`-gated visibility, the `@localize` `kw-l` tags).
4. `src/Kumunita.Web/Views/Shared/` (the sidebar/nav partial) — **extend** with the
   "Inventory" nav entry (the `@localize` `kw-l` tag + the `/inventory` route link — the
   M5 `projects` nav-entry shape).
5. `tests/Kumunita.Web.Tests/` — pinned tests: (a) the **check-out action** (a valid
   check-out → the item's `CurrentHolderId` is set + the history shows the new record);
   (b) the **check-in action** (a valid check-in → the `CurrentHolderId` is cleared + the
   history record is closed); (c) the **nav entry present** (the sidebar has the
   "Inventory" link, the `@localize` `kw-l` tag resolves).
6. `tests/Kumunita.Web.Tests/` — a pinned test for the **edit/delete** actions (a valid
   edit → the item's fields are updated; a valid delete → the item is soft-deleted + no
   longer in the list).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green **+** `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` green. Append
`## U05 — Web surface part 2 (edit/delete + check-out/check-in + nav)` to the handoff
notes (the action shapes, the nav entry, the pinned tests).

---

## U06 — Close (the three acceptance gate tests + the docs parity flip)

**Goal.** Add the **three acceptance gate tests** (the U00 §gate: closed-loop /
handoff-authorization-boundary / part-vs-whole-audit-completeness) and the **docs parity
flip** (D7 — the four surfaces, in the same unit). **This is the close — the M16
milestone ships here.**

**Entry reads (7).**
1. `docs/design/m16-inventory-design.md` — the §gate tests + the §drift-guard,
   **verbatim**.
2. `docs/plans-milestones/plan-m16-inventory.md` (this register) — the D7 close-flip
   contract (the four surfaces).
3. `src/Kumunita.Web/Milestones.cs` — the M16 `StatusNext` → `StatusDone` flip + the
   M17 `StatusPlanned` → `StatusNext` flip (the D7 flip).
4. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the re-pin to
   `M17_Is_The_Single_InProgress_Milestone` + the shipped list gains M16 + M18 stays
   planned (the D7 flip).
5. `README.md` (the Roadmap + the M16 Status line) — the M16 → done, M17 → in progress
   flip + the M16 Status line (the D7 flip).
6. `docs/STATUS.md` — the M16 → done flip (the D7 flip).
7. `docs/ARCHITECTURE.md` (the value-chain table, ending at the M15 row) — the M16 row
   added (the D7 flip).

**Deliverables (7 files).**
1. `tests/Kumunita.Web.Tests/` — the **three acceptance gate tests** (the U00 §gate):
   (a) **closed-loop** (create a shared item → a second resident checks it out → checks
   it in → the item's usage history shows exactly one checkout — C-M16·3 / F1); (b)
   **handoff-authorization-boundary** (a private item is invisible to a non-member —
   the list hides it, the aggregate row shows `hiddenCount ≥ 1`; the detail is a 404,
   not a 403 — C-M16·5; a shared item is visible to all members but only the standing
   actor can check it out — the standing-probe deny commits — D5); (c)
   **part-vs-whole-audit-completeness** (the list's aggregate `AccessAudit` row commits
   atomically with any concurrent write — C-M16·2 / C3; the checkout/check-in
   transition's audit row commits atomically with the state change — C-M16·3).
2. `src/Kumunita.Web/Milestones.cs` — M16 `StatusNext` → `StatusDone`; M17
   `StatusPlanned` → `StatusNext` (the D7 flip).
3. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — re-pin to
   `M17_Is_The_Single_InProgress_Milestone` + the shipped list gains M16 + M18 stays
   planned (the D7 flip — the `MilestonesTests` pins).
4. `README.md` — the M16 Roadmap row → done; the M17 Roadmap row → in progress; the
   M16 Status line (the D7 flip).
5. `docs/STATUS.md` — the M16 → done flip (the D7 flip).
6. `docs/ARCHITECTURE.md` — the value-chain table gains the **M16 row** (the D7 flip —
   the table currently ends at the M15 row).
7. `docs/plans-milestones/in-progress/m16-inventory-handoff-notes.md` — the final
   `## U06 — close (gate tests + docs parity flip)` section (the M16 milestone is
   shipped; the roadmap parity is flipped; the handoff notes are complete).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green **+** **both** test assemblies
green: `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
**and** `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
(the gate tests span both; the parity tests run in Web.Tests). **The M16 milestone is
shipped.** Append the final handoff-notes section. Move all the `m16-u*.md` unit plans
that are not yet in `done/` to `done/`.

---

## Deferred (named lanes, each its own ADR)

- **The "nudge the owner on checkout" lane** (the M6 `INotificationService` emitter
  idiom) — **D8**; a follow-on lane with its own ADR.
- **The per-item translation lane** (the M5 `TodoTranslation` shape) — a follow-on lane
  with its own ADR.
- **The media / attachments lane on items** (the M3 ADR 0025 / ADR 0034 shape) — a
  follow-on lane with its own ADR.
- **The recurring / reserved check-out lane** (a pre-booked slot, a hold) — a follow-on
  lane with its own ADR.

Each of these is **not** in M16's scope; M16's core is the state transition + the usage
history + the Web surface + the roadmap parity flip.
