# Plan: M17 — Bookmarks (save posts, events, todos, etc. for quick personal access)

> **M17 is the current in-progress milestone** (`Milestones.cs`: `M17` =
> `StatusNext`, `M18`/`M19` = `StatusPlanned`). This register is the
> **milestone-level** plan; each unit is a separate **self-contained** file under
> `docs/plans-milestones/in-progress/` (`m17-u00.md` … `m17-u05.md`) that a fresh
> agent can execute reading **only its own unit plan + its entry reads**. When a
> unit is done, its plan file is moved to `docs/plans-milestones/done/`.
>
> **Handoff notes (scratch tier):** `docs/plans-milestones/in-progress/m17-bookmarks-handoff-notes.md`
> — `## U#` sections appended (never rewritten) as each unit completes; a new agent
> reads the relevant `## U#` sections *after* its own unit plan for context.
>
> **Design doc (primary tier):** `docs/design/m17-bookmarks-design.md` — authored
> by **U00** (this register's D1–D8, C-M17·*, F1–F5 + the named trade, and the
> §kw-l key list are [PROPOSED] here and **locked verbatim** into the design doc
> by U00). Every later unit reads the relevant design-doc section **verbatim**,
> not this register.
>
> **ADR:** `docs/adr/0118-bookmarks.md` (confirm **0118** is free against
> `docs/adr/README.md` — the index currently ends at **0117**) — authored by
> **U00**, `Status: Accepted`, with its row appended to the `docs/adr/README.md`
> index after 0117.
>
> **Unit register:** `docs/plans-milestones/in-progress/m17-u00.md` … `m17-u05.md`.
> Each unit plan states its own goal, entry reads, deliverables, and exit. A unit
> agent reads **only its unit plan + its entry reads** — it does **not** need the
> register. The unit plans are the source of truth for *how*; this register is the
> source of truth for *what* (the [PROPOSED] decisions U00 locks).
>
> **U00 is the sign-off gate.** No code unit (U01+) may start until U00 has locked
> the decisions into `docs/design/m17-bookmarks-design.md` and
> `docs/adr/0118-bookmarks.md`.
>
> **[PROPOSED] veto window.** The D1–D8 below are [PROPOSED] and **lockable**
> (U00 promotes them to Accepted in ADR 0118 + the design doc). A veto window
> applies: if a decision is vetoed before U00 locks it, the register + the
> affected unit plans are amended and the veto is recorded in the handoff notes.
> After U00 locks, a change is a **drift event** (design doc §drift-guard).
>
> **Atomicity contract (this register is sized for ~32K-context agents).** Every
> unit is small enough that an agent with a ~32K token context window can read
> **only its own unit plan + its entry reads**, execute, and hand off.
> Concretely, per unit:
>
> - **Entry reads: 4–8 files** (named, with a one-line why each). No "read the
>   whole project."
> - **Deliverables: ≤ 7 small files** (each named, with the exact required
>   content or the pinned test names it must contain).
> - **Exit: one `dotnet build Kumunita.slnx -c Debug`** + **one** `dotnet exec`
>   test assembly run (the relevant test project), both green. A unit that needs
>   a second test assembly to prove its exit is **too big** — split it.
>
> **Unit-series rules (apply to every unit):**
>
> 1. **Never touch files outside your own Deliverables list.** If a change is
>    needed in another unit's files, note it in the handoff notes under *your*
>    `## U#` section and stop — do not edit it.
> 2. **Never rewrite the design doc outside its §drift-guard.** U00 authored it;
>    U01+ read it verbatim. A change is a drift event.
> 3. **No tests beyond the pinned list.** A unit adds **only** the pinned tests
>    named in its Deliverables; it does not re-run or re-name tests from earlier
>    units.
> 4. **The frozen seams are never extended.** `IAuthorizationService`
>    (4-method surface + the group lane), `AccessAction` (`Read`/`Moderate` only),
>    `AccessVia` (Owner/Admin + the existing values), and `Decide()` are frozen.
>    M17 adds **no adapter at all** — a bookmark is a *personal-by-id* record
>    (the M9 ADR 0105 shape), never an audience decision (D2).
> 5. **M17 is a core milestone surface, not a lane.** It has **no** off-by-default
>    admin toggle and **no** `LocaleSettings`-style admin switch (D6 / C-M17·6) —
>    it is always available to every resident, like M5 Projects.

---

## Understanding

**What M17 does (README, verbatim):** *Bookmarks: save posts, events, todos, etc.
for quick personal access.*

So M17 is a **standalone personal surface** (not a group/post feature) that lets a
resident pin the content they care about — from any of the resident content
surfaces — into one private list they can return to:

- **A bookmark** (`Bookmark`) — one row: **whose** (`OwnerId` — the bookmarking
  resident), **what** (`TargetKind` + `TargetId` — the exact `TargetKind`
  vocabulary the codebase already emits: `post` / `event` / `todo` /
  `announcement` / `page`), and **when** (`Created`). That is the whole
  document.
- **The toggle** — bookmark again is a no-op; unbookmark removes the row.
  One row per (owner, target), enforced at the DB layer by a unique index
  (the M9 `convo_uidx_pair` idempotency shape).
- **The bookmark list** — the resident's own bookmarks, grouped by
  `TargetKind`, each row linking back to the target. A bookmark whose target is
  no longer readable (soft-deleted, hidden, gone) **degrades** — it never 404s,
  403s, or leaks a title.

**The one rule that keeps M17 safe (D3):** you may only bookmark **what you can
see** — the write lane re-runs the target's frozen `Read` decision (the same
`CanAsync(Read)` the detail page uses) *before* the row is created. A bookmark
**grants nothing**: it is not an audience, not an `AccessVia`, not a standing.
Reading the bookmark list is the owner's personal read (the M9
participant-by-id / M6 inbox shape) — a **non-owner** (a `GlobalAdmin` included)
gets a non-leaky 404 and **no audit row**.

**What M17 is NOT (deferred lanes, each its own ADR):**

- **No notifications.** A "someone bookmarked your post" nudge (the M6
  `INotificationService` emitter idiom) is **deferred** — its own ADR (D7).
- **No bookmarks on groups themselves** (only on the content surfaces listed in
  D1 — the ADR 0013 membership-lane surfaces have their own visibility world).
- **No bookmark sharing / "top bookmarked posts" / per-surface stats.** A
  bookmark is private to the owner; aggregate analytics over bookmarks is a
  follow-on lane (the M13 `UsageEvent` lane already captures raw request
  telemetry).
- **No reorder / folders / notes on bookmarks.** One row, one link, one toggle.
  Folders and manual ordering are a follow-on lane, its own ADR.

---

## Assumptions / decisions — [PROPOSED, lockable by ADR 0118 in U00]

> U00 locks these into `docs/design/m17-bookmarks-design.md` +
> `docs/adr/0118-bookmarks.md` (verbatim) and records any veto. A `*Forbids:*`
> tail names the drift the decision guards against.

- **D1 — One document, one new surface, one new bounded context.** M17 adds a
  new bounded context `Kumunita.Core.Bookmarks` with **one document**:
  `Bookmark` (`OwnerId`, `TargetKind`, `TargetId`, `Created`), registered on a
  **new `M17DocTypes`** surface (the M9/M5 `*DocTypes` precedent — a parallel
  additive surface, delta-detected and applied idempotently at boot, **zero
  migrations** for existing surfaces). `TargetKind` is a **string** restricted
  to the closed set {`post`, `event`, `todo`, `announcement`, `page`} — the
  exact vocabulary the existing `*ToAuditableResource` adapters already emit
  (the M5 `KanbanStatuses` string-not-enum shape). *Forbids:* a separate
  document per surface; a `Bookmark`-per-kind set of tables; an enum type for
  `TargetKind`; a migration of an existing surface.

- **D2 — A bookmark is a personal-by-id record, never an audience decision.**
  M17 adds **no adapter**, **no** new `AccessAction`, **no** new `AccessVia`,
  **no** branch in `Decide()`, **no** method on `IAuthorizationService` (the M9
  ADR 0105 shape — a *participant-by-id* / *owner-by-id* record, not an audience
  decision). The **owner's** own list is a personal read (like the M6 inbox):
  no `CanSeeAsync` pass over the bookmark rows themselves. *Forbids:* a
  `BookmarkToAuditableResource`; running the owner's own bookmark list through
  `IAuthorizationService`; a new `AccessAction`/`AccessVia`.

- **D3 — Bookmarking requires visibility; bookmarking grants nothing.** The
  write lane (bookmark) **re-runs the target's frozen `Read` decision**
  (`CanAsync(Read)` over the target's existing adapter — the *same call the
  detail page uses*) and **refuses 404** (not 403 — the Deny row **does not
  survive**, same as M16's create-gate posture) when the target is not
  visible. The bookmark row itself **never** appears in any `CanSeeAsync`
  pass, never widens an audience, and never confers standing. *Forbids:* a
  bookmark row that lets an invisible target be bookmarked; a bookmark
  consulted by any read decision anywhere; a `GlobalAdmin` peek branch into the
  target's visibility on the write lane (the detail page's own decision is the
  truth).

- **D4 — One row per (owner, target); toggle = idempotent add / remove.** The
  unique index on `(OwnerId, TargetKind, TargetId)` is the witness: a second
  bookmark of the same target is a **no-op** (the button reflects "bookmarked");
  unbookmark removes the row. **No** `IsDeleted` flag — removal is physical
  (a bookmark is a *pointer*, not content: there is nothing to preserve, and a
  re-bookmark after removal is a fresh `Created` timestamp). *Forbids:* an
  `IsDeleted` soft-delete on `Bookmark`; a second row for the same (owner,
  target); mutating `Created`.

- **D5 — The list degrades, never leaks.** The bookmark list resolves each row
  by `TargetKind` + `TargetId` against the owning surface's read seam; a row
  whose target is **absent, soft-deleted, or no longer visible to the owner**
  renders as a **generic degraded label** (a new `kw-l` key: "No longer
  available") with **no title, no link, no id** — and the row remains
  unbookmarkable-but-removable (the unbookmark button still works on the
  degraded row). One list load, **zero** new authorization branches (C-M17·2).
  *Forbids:* a 404/403 on a dangling bookmark row; leaking a removed target's
  title; a per-row `CanSeeAsync` branch in the list beyond the owning
  surface's existing seam.

- **D6 — M17 is a core surface, not a lane: no off-by-default toggle.** M17 is
  a standing core surface (like M5 Projects): **no** off-by-default admin
  toggle, **no** `LocaleSettings`-style admin switch, **no** `KumunitaFeature`
  admin flag. It is always available to the community. (M9's off-by-default
  toggle was a *lane*; M17 is a *milestone*.) *Forbids:* an off-by-default
  admin toggle for M17; a `LocaleSettings`-style switch.

- **D7 — No notification lane in M17 (deferred).** M17 composes **only** the
  frozen `IAuthorizationService` (the D3 write-lane visibility check) + the
  owning surfaces' existing read seams (D5 list resolution) over the
  host-registered Marten `IDocumentStore`. It does **not** take an
  `INotificationService` emitter and does **not** register a Wolverine
  handler. The "nudge the author on bookmark" lane is **deferred** — a
  follow-on lane with its own ADR. *Forbids:* an `INotificationService` in
  the `BookmarkService` composition; a Wolverine handler for bookmarks.

- **D8 — The close flip is one atomic unit (U05).** The M17 close (U05) flips
  the roadmap parity **together**: `Milestones.cs` M17 `StatusNext`→`StatusDone`,
  M18 `StatusPlanned`→`StatusNext`; `MilestonesTests.cs` re-pinned to
  `M18_Is_The_Single_InProgress_Milestone` + the shipped list gains M17 + M19
  stays planned; the README **Roadmap** (M17 → done, M18 → in progress) + the
  M17 **Status** line; `docs/STATUS.md`; and `docs/ARCHITECTURE.md`'s
  value-chain table gains the **M17 row**. All four surfaces ship **in the same
  unit** (the M15/U10, M16/U06 docs-parity precedent). *Forbids:* reordering
  M18/M19; editing a shipped milestone's text; breaking the `MilestonesTests`
  pins; flipping one surface and not the others.

---

## Invariants (C-M17·1 … C-M17·7) — U00 locks these verbatim

- **C-M17·1 — The bookmark is a personal-by-id record; the surface adds zero
  authorization.** `Bookmark` carries `OwnerId` + `TargetKind` + `TargetId`
  and nothing that an access decision reads; there is **no** `Bookmark`
  adapter, **no** new `AccessAction`, **no** new `AccessVia`, **no** `Decide()`
  branch, **no** new `IAuthorizationService` method (D2). The **only**
  authorization call in the entire milestone is the D3 write-lane
  visibility check, which is the target's *own* frozen `CanAsync(Read)` —
  M17 adds no decision of its own.
- **C-M17·2 — The owner's list is a personal read, audited zero.** Loading
  one's **own** bookmark list is the M6-inbox / M9-conversation personal-read
  shape: **no** `CanSeeAsync` pass over the bookmark rows, **no** `AccessAudit`
  row for the owner's own list, **no** owner-check bypass. A **non-owner**
  (a `GlobalAdmin` included) gets a non-leaky **404** — and that 404 commits
  **no** `AccessAudit` row either (the ADR 0105 "operator has no read
  standing" precedent).
- **C-M17·3 — Bookmarking requires visibility; bookmarking grants nothing.**
  The write lane re-runs the target's frozen `Read` decision **before**
  creating the row; Deny ⇒ **404** (not 403), and the bookmark row never
  appears in, widens, or is consulted by any `CanSeeAsync` pass anywhere
  (D3).
- **C-M17·4 — One row per (owner, target); toggle is idempotent.** The unique
  index on `(OwnerId, TargetKind, TargetId)` makes a duplicate bookmark a
  no-op; unbookmark removes the row; `Created` is never mutated (D4). A
  bookmark is a pointer, not content — removal is physical, there is no
  `IsDeleted`.
- **C-M17·5 — The list degrades, never leaks.** A bookmark whose target is
  absent, soft-deleted, or no longer visible renders the generic
  `bm.list.degraded` label — **no title, no link, no id** — and the unbookmark
  control still works on it (D5).
- **C-M17·6 — M17 is a standing core surface (no toggle).** M17 has **no**
  off-by-default admin toggle and **no** `LocaleSettings`-style admin switch
  (D6); it is always available to every resident.
- **C-M17·7 — The close flip is atomic.** The M17 close (U05) flips the four
  surfaces — `Milestones.cs` (M17→done, M18→next), `MilestonesTests.cs`
  (re-pinned to `M18_Is_The_Single_InProgress_Milestone` + shipped list + M19
  planned), the README Roadmap/Status, and `docs/STATUS.md` + the
  `docs/ARCHITECTURE.md` value-chain M17 row — **in the same unit** (D8).

---

## FACES (F1–F5) + the named trade — U00 refines

- **F1 — The toggle is idempotent and atomic.** Bookmarking the same target
  twice commits **one** row (the unique index is the witness — the M9
  `convo_uidx_pair` shape); the button reflects "bookmarked" on reload.
  Unbookmarking removes the row; re-bookmarking is a fresh `Created`
  (C-M17·4).
- **F2 — The write lane is the target's own frozen decision.** Bookmarking a
  post you cannot see ⇒ **404**, no row, no Deny row surviving (the M16
  create-gate posture); the *same* `CanAsync(Read)` call the detail page used
  is the truth — M17 adds no branch (C-M17·3).
- **F3 — The list is the owner's personal read (zero audit).** The owner's
  own `/bookmarks` loads their rows with **zero** `AccessAudit` rows and zero
  `CanSeeAsync` passes over the bookmark rows (C-M17·2); a `GlobalAdmin` who
  is not the owner gets a 404 and no row.
- **F4 — A dangling bookmark degrades, never leaks.** The target is
  soft-deleted / hidden / removed ⇒ the row shows the generic `bm.list.degraded`
  label, no title, no link, no id; unbookmark still works (C-M17·5, D5).
- **F5 — M17 is a standalone core surface** (C-M17·6 / D6): it does not depend
  on a lane being on; the only external composition is the frozen
  `IAuthorizationService` (D3 write lane) + the owning surfaces' existing
  read seams (D5 list resolution).

**The named trade — the dangling-row UX vs. the leak surface.** M17 keeps the
bookmark row when its target dies (soft-deleted post, removed event, hidden
todo) instead of silently removing it, because *silently-removed bookmarks are
worse than a greyed-out one* — the resident's list would shrink under them with
no explanation. The trade: the row then has to render something for a target
it can no longer resolve, and the invariant that keeps that safe is
**C-M17·5** — the degraded label is a **closed `kw-l` key** (`bm.list.degraded`)
with **no title, no link, no id**, so the leak surface of a dangling row is
exactly one static string. The unbookmark control stays (removing a dangling
row needs no target read at all — it is keyed on the row's own id).

---

## Approach

Three tracks + a close, each unit atomic and self-contained. **U00 is the
sign-off gate (no code unit may start before it).**

**Track A — Docs (U00).** The design doc (decisions/invariants/FACES + trade/
§kw-l key list/gate tests/§drift-guard) + ADR 0118 + the ADR index row.

**Track B — Core (U01, U02).** U01 the context doc + `M17DocTypes` (+ the
unique index). U02 the service seam (`IBookmarkService`: list / bookmark /
unbookmark) + the D3 write-lane visibility check + the D5 degraded resolution
+ the DI registration + pinned Core tests.

**Track C — Web (U03, U04).** U03 the `BookmarksController` + the
`/bookmarks` list view (grouped by `TargetKind`, the degraded label, the
per-row unbookmark) + the `bm.*` `kw-l` keys (× en/de/fr/da) + the nav entry.
U04 the **bookmark button** — a shared `_BookmarkButton` partial + the toggle
`POST /bookmarks/toggle` action — wired into the **five** target detail
surfaces (post, group post, event, todo, announcement; pages are in-scope per
D1 but their detail surface is the U04 entry-read list's call).

**Close (U05).** The three acceptance gate tests + the docs parity flip (the
D8 four surfaces, in the same unit).

**Unit map** (sized for ~32K):

| Unit | Track | One-line goal |
|---|---|---|
| **U00** | A | Design doc + ADR 0118 (lock D1–D8, C-M17·*, F1–F5 + trade, §kw-l, gate tests, §drift-guard) |
| **U01** | B | `Kumunita.Core.Bookmarks` context: `Bookmark` + `M17DocTypes` (the `(OwnerId, TargetKind, TargetId)` unique index) + boot wiring |
| **U02** | B | `IBookmarkService` + `BookmarkService` (list / bookmark-with-visibility-check / unbookmark / degraded resolution) + the DI registration + pinned Core tests |
| **U03** | C | `BookmarksController` + `/bookmarks` list view (grouped, degraded rows, unbookmark) + the `bm.*` `kw-l` keys (×4) + the nav entry + pinned Web tests |
| **U04** | C | the `_BookmarkButton` partial + the `POST /bookmarks/toggle` action + wiring into the five target detail views + pinned Web tests |
| **U05** | Close | the three acceptance gate tests + the docs parity flip (D8, four surfaces in one unit) |

---

## Workflow (per unit, sized for ~32K)

Each unit agent:

1. Reads **its own unit plan** (`docs/plans-milestones/in-progress/m17-uNN.md`)
   in full.
2. Reads the **entry reads** listed in the unit plan (4–8 files, named, with a
   one-line why each).
3. If a **handoff note** exists for an earlier unit the current one depends on,
   reads that `## U#` section of
   `docs/plans-milestones/in-progress/m17-bookmarks-handoff-notes.md`.
4. Executes the **deliverables** (≤ 7 small files) named in the unit plan.
5. Runs the **exit gate**: `dotnet build Kumunita.slnx -c Debug` **+** the
   relevant `dotnet exec` test assembly, both green.
6. **Appends** a `## U# — <one-line summary>` section to the handoff notes (the
   state it left the codebase in, any drift observed, any follow-on noted) —
   **never** rewrites an earlier `## U#` section.
7. Moves its own unit plan from `docs/plans-milestones/in-progress/` to
   `docs/plans-milestones/done/`.

The next unit starts fresh, reading only its own plan + its entry reads + the
relevant `## U#` handoff sections.
