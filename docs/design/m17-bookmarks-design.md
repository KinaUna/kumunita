---
id: design-m17-bookmarks
title: "M17 — Bookmarks"
tier: reference
kind: design
status: current
audiences: [developer, agent]
related:
  - docs/adr/0118-bookmarks.md
  - docs/adr/0105-messaging.md
  - docs/adr/0004-data-persistence-and-schema-evolution.md
updated: 2026-11-02
---

# M17 — Bookmarks (save posts, events, todos, etc. for quick personal

> **Abstract:** the **bookmarks** design — the resident's **quick-access list**: a private pin of the content they care about (post / event / todo / announcement / page) into one returnable list. One new bounded context (`Kumunita.Core.Bookmarks`) + one `Bookmark` doc; **no adapter** (a personal-by-id record). **Who is it for:** a developer touching the `Bookmark` doc; an agent that needs the personal-by-id shape. **When to read the rest:** before touching the bookmarks context, the pin/unpin lane, or the `bm.*` keys.

access) — design

> **Milestone M17.** The resident's own **quick-access list**: a private,
> personal pin of the content they care about — from any of the resident
> content surfaces (post / event / todo / announcement / page) — into one
> list they can return to, "save posts, events, todos, etc. for quick
> personal access," the README roadmap row names verbatim.
>
> **A standalone core surface** (a roadmap letter, not a named lane): one
> **new bounded context** `Kumunita.Core.Bookmarks` with **one document**
> (`Bookmark`) on a **new `BookmarkDocTypes`** surface (the M9 `MessagingDocTypes` /
> M5 `ProjectDocTypes` precedent — additive, idempotent, **zero migrations** for
> existing surfaces), **no adapter at all** (a bookmark is a *personal-by-id*
> record, the ADR [0105](../adr/0105-messaging.md) participant-by-id shape — D2), and the **`bm.*`**
> `kw-l` registry block (the §kw-l below).
> **Zero new authorization surface** (C-M17·1): no new `AccessAction`, no
> new `AccessVia`, no branch in `Decide()`, no method on
> `IAuthorizationService`, no `INotificationService` (D7). The **only**
> authorization call in the entire milestone is the D3 write-lane
> visibility check, which is the **target's own** frozen
> `CanAsync(Read)` — M17 adds no decision of its own.
>
> **Status.** **LOCKED.** The decisions D1–D8, the invariants
> C-M17·1…7, the FACES F1–F5 + the named trade, the §kw-l key list, and
> the §gate test names are locked in **ADR [0118](../adr/0118-bookmarks.md) (Accepted, 2026-09-29)**.
> The `[PROPOSED]` set in the register
> [`docs/archive/lanes/m17/plan-m17-bookmarks.md`](../archive/lanes/m17/plan-m17-bookmarks.md) is the locked set this
> doc restates **verbatim** (the U00 handoff entry records the lock;
> **no veto** was recorded before the lock).
>
> **The one thing every unit must respect:** M17 **composes only frozen
> seams** — the `IAuthorizationService` (the D3 write-lane check only) +
> the owning surfaces' existing read seams (the D5 degraded resolution)
> over the host-registered Marten `IDocumentStore`. It does **not** add a
> new `AccessAction`, a new `AccessVia`, a new `Decide()` branch, a new
> adapter, a new `IAuthorizationService` method, or a notification lane
> (C-M17·1, D2, D7). Every register unit (U01's context + surface, U02's
> service seam, U03's list surface, U04's toggle button) enforces this at
> a different seam; the §drift-guard frozen list below is the **exact**
> set that is untouched.

## Context

M16 (Inventory) shipped the **shared-resource** world: a bounded context,
a `*DocTypes` surface, one adapter onto the frozen
`IAuthorizationService`, a standing split over its write lanes, and an
`inv.*` `kw-l` block — and the whole neighborhood now has a place for
*the ladder is with Anna*. M17 ships the **personal-pin** world: one
private list, over the five resident content surfaces, that **moves with
the resident**, not the content.

The precedents M17 composes:

1. **The frozen `IAuthorizationService`** (ADR [0006](../adr/0006-module-boundary-contracts.md) §A) — `CanAsync(Read)`
   (detail) over an `IAuditableResource`, the `IDocumentSession`
   overloads for C3 same-transaction audits. The frozen surface is
   4-method + the group lane; the frozen `AccessAction` set is
   `Read` / `Moderate`; the frozen `AccessVia` set is Owner / Admin (+
   the existing values). **M17 adds none of these — it adds no adapter
   at all** (C-M17·1 / D2).
2. **The ADR [0105](../adr/0105-messaging.md) (M9 messaging) participant-by-id shape** — the
   *primary* precedent (D2 / C-M17·2): a **personal-by-id record** whose
   access story is **identity comparison in the service**, never an
   audience decision. A non-participant — an operator included — gets a
   **non-leaky 404**, and the owner's own read is a **personal read**
   that **never audits** (M9: "reads never audit"; the M6 inbox shape).
   The owner's own bookmark list is exactly this: `Where(b =>
   b.OwnerId == ownerId)`, **zero** `AccessAudit` rows, **zero**
   `CanSeeAsync` passes over the bookmark rows (C-M17·2).
3. **The `*DocTypes` parallel-surface shape** (`MessagingDocTypes` /
   `ProjectDocTypes`) — `opts.Schema.For<T>()` + named indexes;
   `ApplyAllConfiguredChangesToDatabaseAsync()` delta-detects and
   applies the new table **idempotently** at boot (ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md) §B.1) —
   **zero migrations for existing surfaces**. The M9 `convo_uidx_pair`
   (`UniqueIndex("convo_uidx_pair", c => c.ParticipantA, c =>
   c.ParticipantB)`) is the **F1 idempotency witness** the
   `BookmarkDocTypes` unique index on `(OwnerId, TargetKind, TargetId)`
   copies (the witness of at-most-one-row-per-owner-target).
4. **The closed-string-set idiom** (the M5 `KanbanStatuses` shape) —
   `TargetKind` is a **string** restricted to the closed set
   {`post`, `event`, `todo`, `announcement`, `page`}, the **exact
   vocabulary the existing `*ToAuditableResource` adapters and the
   `AccessAudit` rows already emit** (`TargetKind = "post"` in
   `PostService`, `"event"` in `EventService`, `"todo"` in
   `ProjectService`, `"announcement"` in `AnnouncementService`,
   `"page"` in `PageService`) — D1.
5. **The `bm.*` `kw-l` block in `KnownTranslationKeys`** (the
   closed-key registry, ADR [0015](../adr/0015-ui-view-localization-mechanics.md)) — the `bm.*` block it joins (the
   `KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests`
   pins extend automatically — §kw-l).
6. **The M5 `projects.*` core-surface precedent** (D6) — M17 is a
   **milestone**, not a **lane**: always available, **no**
   off-by-default admin toggle (the inverse of the ADR [0101](../adr/0101-announcement-comments-lane.md)/0105
   `LocaleSettings` toggle shape — C-M17·6).

The gap M17 closes is the README row itself: the neighborhood today has
everywhere to *find* the content (feeds, search, the calendar), but
nowhere to say *these five things are mine to come back to*. M17 adds
that surface: one document, one new surface, one service seam, a list
view + nav entry + toggle button on the five target detail surfaces,
twelve `bm.*` `kw-l` keys (the obs-2 ADR [0118](../adr/0118-bookmarks.md) amendment adds the `bm.toggle.*` flash pair — **14 total**) — and **no** new authorization surface, **no**
notification lane, **no** folders, **no** sharing (§deferred).

M17 builds entirely on **frozen, verified seams** (the list above). This
is a **milestone** (a roadmap letter, not a named lane): the close unit
(U05) flips `Milestones.cs` / the README Roadmap / [`docs/STATUS.md`](../STATUS.md) /
[`docs/ARCHITECTURE.md`](../ARCHITECTURE.md) + `MilestonesTests.cs` (the AGENTS.md doc↔code
parity contract — D8). This design doc (authored U00, **LOCKED**) is the
**primary tier**; the register
[`docs/archive/lanes/m17/plan-m17-bookmarks.md`](../archive/lanes/m17/plan-m17-bookmarks.md) is the secondary tier; the
scratch handoff note is
[`docs/archive/lanes/m17/m17-bookmarks-handoff-notes.md`](../archive/lanes/m17/m17-bookmarks-handoff-notes.md).

## Scope

**In (this milestone):**

- The `Bookmark` document (ns `Kumunita.Core.Bookmarks`; `Id` / `OwnerId`
  / `TargetKind` / `TargetId` / `Created` — §2.1) and the **new
  `BookmarkDocTypes`** surface (the **unique** index on `(OwnerId,
  TargetKind, TargetId)` + a non-unique `(OwnerId, Created)`
  list-ordering index; ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md) §B.1 additive, **zero migrations** for
  existing surfaces).
- `IBookmarkService` + `BookmarkService` — the three seams
  `ListAsync` / `ToggleAsync` / `RemoveAsync` (the D3 write-lane
  visibility check; the D5 degraded resolution; the C-M17·2
  zero-audit personal read) + the DI registration.
- The `BookmarksController` + the `GET /bookmarks` list view (grouped by
  `TargetKind`, the `bm.list.degraded` label on dangling rows, the
  per-row unbookmark) + the nav entry.
- The `_BookmarkButton` partial + the `POST /bookmarks/toggle` action,
  wired into the **five** target detail surfaces (post, group post,
  event, todo, announcement — plus the page surface per D1).
- The `bm.*` `kw-l` keys × en/de/fr/da (the §kw-l list).
- The 14 pinned seam tests (§2.5) + the three acceptance gate tests
  (§gate).

**Out (deferred lanes, each its own ADR — §deferred):**

- The "nudge the author on bookmark" notification lane (D7).
- ~~Bookmark folders / manual ordering~~ — folders are **settled by ADR [0168](../adr/0168-bookmark-folders.md)**; manual ordering remains deferred.
- Bookmark sharing / per-surface "top bookmarked" stats.
- Bookmarks on **groups** themselves (the ADR [0013](../adr/0013-group-posts-membership-lane.md) membership-lane
  surfaces have their own visibility world).

## Human cost

Gives the resident's attention **back**: the pin replaces the
re-discovery cost of finding the five things a resident keeps circling
back to (the same post, the same event, the same page) with one
navigation step, and the list degrades **quietly** — a dead pin renders
as one static label, never a dead-end 404 and never a leak. It
optimizes the whole (a resident's working set), not a part: there is no
engagement metric, no per-surface count, no ranking surface
(C-M17·6 / D6 — **no** analytics over bookmarks in M17). The team's
boundary is respected by construction: the milestone is one document,
one seam, one list — a pace that can hold in the atomic-unit
register.

## Parts affected

- **`Kumunita.Core`** — a **new bounded context**
  `Kumunita.Core/Bookmarks/` (`Bookmark`, `IBookmarkService`,
  `BookmarkService`, the result records, `BookmarkDocTypes`) + its DI
  registration in `DependencyInjection.cs`.
- **`Kumunita.Web`** — `BookmarksController` + the `Views/Bookmarks/`
  list view + the `_BookmarkButton` partial + the five target detail
  views' button wiring + the nav entry + the `bm.*` `kw-l` rows (×4).
- **`tests/Kumunita.Core.Tests`** — `BookmarkServiceTests.cs` (the 14
  pinned seams, §2.5). **`tests/Kumunita.Web.Tests`** — the list /
  toggle / button pins + the three acceptance gate tests (U05).
- **Unchanged (the frozen set):** `IAuthorizationService`,
  `AccessAction`, `AccessVia`, `Decide()`, every existing `*DocTypes`
  surface, `LocaleSettings`, the notification machinery,
  `Milestones.cs` until U05 (D8).

## Seams & contracts (mandatory)

M17 touches **no existing seam**. It creates one context, one interface,
one document surface, one Web controller + partial — and it depends on
**exactly** the frozen set named in §2.2–§2.4. It does **not** touch the
**access model** (audiences, groups, delegation, moderator scope): a
bookmark is **not** an audience, an `AccessVia`, or a standing — it is a
pointer (C-M17·1 / D2). There is **no migration path** to plan: the
`BookmarkDocTypes` surface is delta-detected and applied idempotently at boot
(ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md) §B.1), and the **only** audited call in the milestone is the
D3 write-lane check, which is the **target's own** frozen
`CanAsync(Read)` (the target's own audit row, in the target's own
transaction — M17 commits none of its own, C-M17·2 / C-M17·3).

### 2.1 New M17-owned Core types (exact C#)

```csharp
namespace Kumunita.Core.Bookmarks;

/// One personal pin. A pointer, not content: no Audience, no IsDeleted,
/// no Modified (D1/D2/D4).
public sealed class Bookmark
{
    public string Id { get; set; } = "";                       // string surrogate
    public string OwnerId { get; set; } = "";                  // the bookmarking resident
    public string TargetKind { get; set; } = "";               // closed set:
                                                               //   post | event | todo
                                                               //   | announcement | page
    public string TargetId { get; set; } = "";                 // the target's doc id
    public DateTimeOffset Created { get; set; }                // never mutated (D4)
}

/// The new parallel surface (ADR 0004 §B.1 — additive, idempotent, zero
/// migrations for existing surfaces).
public static class BookmarkDocTypes
{
    public static void Configure(Marten.Schema.StoreOptions opts)
    {
        opts.Schema.For<Bookmark>()
            .UniqueIndex("bm_uidx_owner_target", b => b.OwnerId, b => b.TargetKind, b => b.TargetId)
            .Index("bm_ix_owner_created", b => b.OwnerId, b => b.Created);
    }
}
```

**No** `Audience`, **no** `IsDeleted`, **no** `Modified`, **no**
`ComponentId`, **no** `LanguageCode` (D1/D2/D4) — the exact field set is
a frozen pin (§2.7). `TargetKind` is a **string** (the M5
`KanbanStatuses` string-not-enum shape) restricted to the closed set
{`post`, `event`, `todo`, `announcement`, `page`} — the exact
vocabulary the existing `*ToAuditableResource` adapters already emit.
The **unique** index on `(OwnerId, TargetKind, TargetId)` is the F1
idempotency witness (the M9 `convo_uidx_pair` shape); the non-unique
`(OwnerId, Created)` index is the list-ordering index.

### 2.2 The service seam (exact C#)

```csharp
namespace Kumunita.Core.Bookmarks;

public sealed record BookmarkRow(
    string TargetKind,
    string TargetId,
    string? Title,        // null when Degraded (D5)
    string? Link,         // null when Degraded (D5)
    bool Degraded,
    DateTimeOffset Created);

public sealed record BookmarkGroup(string Kind, IReadOnlyList<BookmarkRow> Items);
public sealed record BookmarkListResult(IReadOnlyList<BookmarkGroup> Groups);

public enum BookmarkToggleStatus { Bookmarked, AlreadyBookmarked, Refused, Removed, NotBookmarked }
public sealed record BookmarkToggleResult(BookmarkToggleStatus Status);

public interface IBookmarkService
{
    /// The owner's own rows, grouped by TargetKind, each row resolved or
    /// degraded per D5. NO AccessAudit row (C-M17·2). NO CanSeeAsync pass
    /// over the bookmark rows (C-M17·2 — the ADR 0105 / M6-inbox shape).
    Task<BookmarkListResult> ListAsync(string ownerId);

    /// Re-runs the target's frozen CanAsync(Read) FIRST (D3):
    ///   Deny / absent  => Refused            (404; no row; no surviving Deny row)
    ///   visible + dup  => AlreadyBookmarked  (no-op; one row, one Created — F1)
    ///   visible        => Bookmarked         (the row is created)
    Task<BookmarkToggleResult> ToggleAsync(string ownerId, string targetKind, string targetId, IDocumentSession session);

    /// Removes the owner's row by the unique key (D4 — physical removal).
    ///   owner's row present => Removed;  absent => NotBookmarked (no-op).
    /// NO target read at all — a dangling row is removable (D5 / F4).
    Task<BookmarkToggleResult> RemoveAsync(string ownerId, string targetKind, string targetId, IDocumentSession session);

    /// The owner's own per-target state (F1 — the button reflects
    /// "bookmarked" on reload): does the owner have a row for
    /// (targetKind, targetId)? A **read** keyed on the owner's own row —
    /// NO target read (D5 / F4 — a dangling row still answers `true`), NO
    /// AccessAudit row, NO CanAsync pass (C-M17·2 — the `ListAsync`
    /// personal-read precedent). A composition detail of the personal-read
    /// surface, not a new authorization surface (§2.7 amendment).
    Task<bool> IsBookmarkedAsync(string ownerId, string targetKind, string targetId);
}
```

The composition root (U02) wires `BookmarkService` over the
host-registered Marten `IDocumentStore` + the owning surfaces' read seams
(the existing service reads the list resolution uses) + the frozen
`IAuthorizationService` (the D3 check only) + — per the 2026-09-30
D3/F2 amendment (ADR [0118](../adr/0118-bookmarks.md)) — the frozen `IIdentityService` (the
announcement branch resolves the owner's real standing in-Core). **No**
`INotificationService` (D7). The non-owner path is the Web layer's:
`[Authorize]` + a controller `ownerId == User id` check, **404** on
mismatch, **no** `AccessAudit` row (§2.4).

### 2.3 The D3 write-lane visibility table

| Target state at toggle time | Result | Row | Audit |
|---|---|---|---|
| visible to the actor (the target's own frozen `CanAsync(Read)` ⇒ Allow) | `Bookmarked` | one row created, fresh `Created` | the **target's own** frozen audit row (M17 adds none) |
| invisible to the actor / absent (Deny, soft-deleted, gone) | `Refused` → **404** (never 403) | **no** row | **no** M17 Deny row survives (the M16 create-gate posture) |
| already bookmarked by the actor | `AlreadyBookmarked` → no-op | **no** second row (the unique index is the witness — F1) | as the first case |

| Row's target state at list time | Row rendering | Leak surface |
|---|---|---|
| resolves to a doc visible to the owner | `Title` + `Link` set, `Degraded = false` | the target's own visible fields |
| absent / soft-deleted / no longer visible to the owner | `Degraded = true`, `Title` / `Link` = **null** → the closed `bm.list.degraded` label | **exactly one static string** (C-M17·5) |

### 2.4 The D2 personal-read shape

`ListAsync` is a **read**: the owner's own rows
(`Where(b => b.OwnerId == ownerId)`, ordered by `Created`), resolved
row-by-row against the owning surface's existing read seam (§2.3 table
2). **No** `AccessAudit` row, **no** `CanSeeAsync` pass over the bookmark
rows (the ADR [0105](../adr/0105-messaging.md) "reads never audit" + the M6-inbox personal-read
shape — C-M17·2). The **non-owner** path is the Web layer's:
`[Authorize]` + the controller's `ownerId == User id` check ⇒ **404** on
mismatch (a `GlobalAdmin` included — the ADR [0105](../adr/0105-messaging.md) "operator has no read
standing" precedent), and that 404 commits **no** `AccessAudit` row.

### 2.5 Pinned seam tests (exact names)

File `tests/Kumunita.Core.Tests/BookmarkServiceTests.cs`:

1. `F1_ToggleIsIdempotentOneRow`
2. `F1_ToggleRemoveIsPhysicalNoIsDeleted`
3. `F2_WriteLaneRequiresTargetVisibility`
4. `F2_InvisibleTargetRefused404NoRow`
5. `F2_DuplicateIsNoOp`
6. `F3_OwnerListLoadsZeroAuditRows`
7. `F3_NonOwnerGets404NoAuditRow`
8. `F4_DanglingRowDegradesNoTitleNoLink`
9. `F4_UnbookmarkOnDanglingRowStillWorks`
10. `F4_VisibleRowHasTitleAndLink`
11. `Bookmark_NoAudienceNoIsDeletedNoModified`
12. `BookmarkService_MakesNoCanSeeAsyncCallOnList`
13. `BookmarkService_MakesNoModerateCall`
14. `TargetKind_IsTheClosedStringSet`

### 2.6 Acceptance gate (U05 records)

The three acceptance tests, verbatim from the register's §gate:

- **(a) closed-loop** — a resident bookmarks a post they can see → it
  appears in their `/bookmarks` under the `post` group → they unbookmark
  it → it is gone; a second bookmark of the same post is a no-op — one
  row, one `Created`.
- **(b) handoff-authorization-boundary** — bookmarking a post the
  resident *cannot* see ⇒ 404, no row, no Deny row surviving; a
  `GlobalAdmin` who is not the owner of a bookmark list gets a 404 with
  **no** `AccessAudit` row — the C-M17·2 zero-audit personal read.
- **(c) part-vs-whole-audit-completeness** — the owner's own list load
  commits **zero** `AccessAudit` rows — C-M17·2 — while the *write*
  lane's single `CanAsync(Read)` check is the target's own frozen
  decision (C-M17·3): the part (one toggle) and the whole (the list)
  audit differently by design, and both pins hold together.

### 2.7 Drift-guard (frozen once written)

The **frozen pins**: the frozen seams (`IAuthorizationService`,
`AccessAction`, `AccessVia`, `Decide()`); the C-M17·1…7 table; the
F1–F5 + named trade; the D1–D8 decisions; the §kw-l key list; the closed
`TargetKind` set; the `Bookmark` doc shape (the exact field set, §2.1);
the `IBookmarkService` ctor + the 4 public methods + the
`BookmarkToggleStatus` enum (the ctor **composes** the frozen
`IAuthorizationService` + the owning surfaces' read seams + — per the
2026-09-30 D3/F2 amendment, ADR [0118](../adr/0118-bookmarks.md) — the frozen `IIdentityService`
for the announcement branch only; the **4-method public signature is
frozen** — the 4th, `IsBookmarkedAsync`, added by the 2026-10-04 F1
amendment, ADR [0118](../adr/0118-bookmarks.md) — and the added dependencies are frozen seams, so
they are composition detail, not a new surface); the §2.3 / §2.4 tables; and the
**15** test names (§2.5 — the 14 original + the amendment's
`GlobalAdmin_Beats_CommunityScope_On_Bookmark_Toggle`). A change to any
of these after U00 is a **drift event** (`## U<m> — Drift pause` per the
unit-series rules; recorded in the §drift-guard drift log below).

## FACES (F1–F5) + the named trade

- **F1 — The toggle is idempotent and atomic.** Bookmarking the same
  target twice commits **one** row (the unique index is the witness — the
  M9 `convo_uidx_pair` shape); the button reflects "bookmarked" on
  reload — **completed** (the 2026-10-04 F1 amendment, ADR [0118](../adr/0118-bookmarks.md)): the
  detail-surface `_BookmarkButton` partial now `@inject`s the frozen
  `IBookmarkService` and resolves the owner's own per-target state inline
  via `await Bookmarks.IsBookmarkedAsync(ownerId, Kind, Id)` (a
  C-M17·2-shape personal read — no `AccessAudit`, no `CanAsync`, no target
  read; the `ListAsync` precedent), and renders a **"Remove"** form
  POSTing to `/bookmarks/{kind}/{id}/remove` when bookmarked (the D4
  physical-removal lane; the `Remove` action's `returnUrl` redirect-back
  keeps the actor on the detail page), vs. the **"Bookmark"** form POSTing
  to `/bookmarks/toggle` otherwise. The partial is the **sole** resolver —
  it makes exactly one targeted personal read and no other service call
  (the "no owner-list load to decide the button's label" pin holds; the
  read is targeted, not the full list; no `CanAsync` branch). An
  unsigned-in visitor (no `SubjectId`) resolves NotBookmarked with no
  query (the C-M17·2 "operator has no read standing" gate). Unbookmarking
  removes the row; re-bookmarking is a fresh `Created` (C-M17·4).
- **F2 — The write lane is the target's own frozen decision.**
  Bookmarking a post you cannot see ⇒ **404**, no row, no Deny row
  surviving (the M16 create-gate posture); the *same* `CanAsync(Read)`
  call the detail page used is the truth — M17 adds no branch
  (C-M17·3).
- **F3 — The list is the owner's personal read (zero audit).** The
  owner's own `/bookmarks` loads their rows with **zero** `AccessAudit`
  rows and zero `CanSeeAsync` passes over the bookmark rows (C-M17·2); a
  `GlobalAdmin` who is not the owner gets a 404 and no row.
- **F4 — A dangling bookmark degrades, never leaks.** The target is
  soft-deleted / hidden / removed ⇒ the row shows the generic
  `bm.list.degraded` label, no title, no link, no id; unbookmark still
  works (C-M17·5, D5).
- **F5 — M17 is a standalone core surface** (C-M17·6 / D6): it does not
  depend on a lane being on; the only external composition is the frozen
  `IAuthorizationService` (D3 write lane) + the owning surfaces'
  existing read seams (D5 list resolution).

**The named trade — the dangling-row UX vs. the leak surface.** M17
keeps the bookmark row when its target dies (soft-deleted post, removed
event, hidden todo) instead of silently removing it, because
*silently-removed bookmarks are worse than a greyed-out one* — the
resident's list would shrink under them with no explanation. The trade:
the row then has to render something for a target it can no longer
resolve, and the invariant that keeps that safe is **C-M17·5** — the
degraded label is a **closed `kw-l` key** (`bm.list.degraded`) with
**no title, no link, no id**, so the leak surface of a dangling row is
exactly one static string. The unbookmark control stays (removing a
dangling row needs no target read at all — it is keyed on the row's own
id).

## Feedback loops

The 14 pinned seams (§2.5) cover the service boundary (toggle
idempotency, the 404 split, the zero-audit read, the degraded row, the
doc shape, the closed set); the Web pins cover the list rendering, the
button state, and the non-owner 404; the three acceptance gates (§2.6)
cover the closed loop, the authorization boundary, and the
part-vs-whole audit split. Watch: a `bm.list.degraded` row that
*resolves to a title* (a leak), or a list load that *commits an
`AccessAudit` row* (an audit leak) — both are pinned red.

## Emergent impact

Privacy **improves** by construction: the surface's only leak surface is
one static string (the named trade), the non-owner 404 leaks nothing
(C-M17·2), and bookmarking a target *cannot* exist for a target the
actor cannot see (C-M17·3) — so the list is a **subset** of the
resident's already-visible world, never a window into it. Trust: the
list is the resident's own; nobody else — a `GlobalAdmin` included — can
read it, which is stronger than the content surfaces themselves
(an operator can *see* a public post, but not *that* a resident pinned
it). Reliability: one row, one toggle, a DB-layer idempotency witness —
there is no state machine to break. Cost: one new table + one index; the
list resolution is one extra read per row, bounded by the resident's own
pin count.

## Local-optimization check

The part being optimized: **the resident's quick-access** — one click to
the working set. The whole pays: one small personal store, one list
query, one button on five detail views. It does **not** optimize a
metric: there is no "top bookmarked" ranking, no per-surface count, no
nudge (C-M17·6 / D7 / §deferred) — the pin is private by
construction, so it cannot become an engagement lever.

## FACES check

**f**lexible — the closed `TargetKind` set means a new content surface
joins the pin world with one vocabulary word + one list-resolution
branch, no schema change. **a**daptive — a dead pin degrades to a label
instead of breaking the list (F4). **c**oherent — the one rule (D3)
means the pin can never outlive the visibility that justified it at
creation, and the list never leaks what the detail page doesn't.
**e**nergizing — one step to the working set. **s**table — one
document, one index, one seam, no state machine, no toggle to drift.
Consumed: the **stable** face pays for **flexible** — the closed set is
deliberately five-wide, not open (adding a sixth kind is a drift event).
The named trade (dangling-row UX vs. leak surface) is the priced cost.

## Rollout & rollback

Deploy: the `BookmarkDocTypes` surface delta-detects and applies at boot
(ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md) §B.1 — idempotent, **zero migrations** for existing surfaces);
no seeder step, no data migration, no feature flag (D6 — there is
**nothing to roll back**: removing the `Bookmark` table + the
`BookmarkDocTypes` registration restores the pre-M17 state; no row is
referenced by anything). See [`docs/OPS.md`](../OPS.md).

## Risks

- **The list-resolution seam drifts from the owning surface's read
  seam** (a row that should degrade resolves, or vice versa) — pinned by
  F4 tests 8–10 + gate (c); the resolution must use the owning surface's
  *existing* seam, never a new one (unit-series rule 4).
- **A leak on a dangling row** (title/id/audit) — pinned by C-M17·5 +
  the named trade: the closed `bm.list.degraded` key is the **only**
  output for a dead target.
- **An accidental `AccessAudit` row** on the owner's list or the
  non-owner 404 — pinned by F3 tests 6–7 + gate (b)/(c).
- **The toggle button wired into a surface whose detail decision differs
  from the D3 check** — the same `CanAsync(Read)` call is the single
  truth (F2); a divergent check is a drift event (§2.7).

## Integration step served

The value-chain arrow this moves: **awareness → decision** — the
resident's working set (what they already acted on or intend to act on)
becomes one step reachable, so the next action (attending the event,
doing the todo, reading the page) is frictionless. It is not a new
signal and not a new coordination surface; it is the resident's own
index into the surfaces that already exist.

## World seams

One inbound world seam: the resident's existing sign-in (the list is
`[Authorize]`; the toggle posts through the standard CSRF shape). Zero
outbound seams — the list links **back into** the platform (each row's
`Link` is a same-origin detail route), never out. No tool handoff is
created or depended on; the pin is a pointer into the surfaces the
resident already crosses.

## Decisions (D1–D8, locked — ADR [0118](../adr/0118-bookmarks.md))

- **D1 — One document, one new surface, one new bounded context.** M17
  adds a new bounded context `Kumunita.Core.Bookmarks` with **one
  document**: `Bookmark` (`OwnerId`, `TargetKind`, `TargetId`,
  `Created`), registered on a **new `BookmarkDocTypes`** surface (the M9/M5
  `*DocTypes` precedent — a parallel additive surface, delta-detected and
  applied idempotently at boot, **zero migrations** for existing
  surfaces). `TargetKind` is a **string** restricted to the closed set
  {`post`, `event`, `todo`, `announcement`, `page`} — the exact
  vocabulary the existing `*ToAuditableResource` adapters already emit
  (the M5 `KanbanStatuses` string-not-enum shape). *Forbids:* a separate
  document per surface; a `Bookmark`-per-kind set of tables; an enum
  type for `TargetKind`; a migration of an existing surface.
- **D2 — A bookmark is a personal-by-id record, never an audience
  decision.** M17 adds **no adapter**, **no** new `AccessAction`, **no**
  new `AccessVia`, **no** branch in `Decide()`, **no** method on
  `IAuthorizationService` (the M9 ADR [0105](../adr/0105-messaging.md) shape — a *participant-by-id*
  / *owner-by-id* record, not an audience decision). The **owner's** own
  list is a personal read (like the M6 inbox): no `CanSeeAsync` pass
  over the bookmark rows themselves. *Forbids:* a
  `BookmarkToAuditableResource`; running the owner's own bookmark list
  through `IAuthorizationService`; a new `AccessAction`/`AccessVia`.
- **D3 — Bookmarking requires visibility; bookmarking grants nothing.**
  The write lane (bookmark) **re-runs the target's frozen `Read`
  decision** (`CanAsync(Read)` over the target's existing adapter — the
  *same call the detail page uses*) and **refuses 404** (not 403 — the
  Deny row **does not survive**, same as M16's create-gate posture) when
  the target is not visible. The bookmark row itself **never** appears
  in any `CanSeeAsync` pass, never widens an audience, and never confers
  standing. *Forbids:* a bookmark row that lets an invisible target be
  bookmarked; a bookmark consulted by any read decision anywhere; a
  `GlobalAdmin` peek branch into the target's visibility on the write
  lane (the detail page's own decision is the truth).
- **D4 — One row per (owner, target); toggle = idempotent add / remove.**
  The unique index on `(OwnerId, TargetKind, TargetId)` is the witness:
  a second bookmark of the same target is a **no-op** (the button
  reflects "bookmarked"); unbookmark removes the row. **No** `IsDeleted`
  flag — removal is physical (a bookmark is a *pointer*, not content:
  there is nothing to preserve, and a re-bookmark after removal is a
  fresh `Created` timestamp). *Forbids:* an `IsDeleted` soft-delete on
  `Bookmark`; a second row for the same (owner, target); mutating
  `Created`.
- **D5 — The list degrades, never leaks.** The bookmark list resolves
  each row by `TargetKind` + `TargetId` against the owning surface's
  read seam; a row whose target is **absent, soft-deleted, or no longer
  visible to the owner** renders as a **generic degraded label** (a new
  `kw-l` key: "No longer available") with **no title, no link, no id** —
  and the row remains unbookmarkable-but-removable (the unbookmark
  button still works on the degraded row). One list load, **zero** new
  authorization branches (C-M17·2). *Forbids:* a 404/403 on a dangling
  bookmark row; leaking a removed target's title; a per-row
  `CanSeeAsync` branch in the list beyond the owning surface's existing
  seam.
- **D6 — M17 is a core surface, not a lane: no off-by-default toggle.**
  M17 is a standing core surface (like M5 Projects): **no**
  off-by-default admin toggle, **no** `LocaleSettings`-style admin
  switch, **no** `KumunitaFeature` admin flag. It is always available to
  the community. (M9's off-by-default toggle was a *lane*; M17 is a
  *milestone*.) *Forbids:* an off-by-default admin toggle for M17; a
  `LocaleSettings`-style switch.
- **D7 — No notification lane in M17 (deferred).** M17 composes
  **only** the frozen `IAuthorizationService` (the D3 write-lane
  visibility check) + the owning surfaces' existing read seams (D5 list
  resolution) over the host-registered Marten `IDocumentStore` (+, per
  the 2026-09-30 D3/F2 amendment, the frozen `IIdentityService` — a
  composition detail, the 3-method public seam unchanged). It does
  **not** take an `INotificationService` emitter and does **not**
  register a Wolverine handler. The "nudge the author on bookmark" lane
  is **deferred** — a follow-on lane with its own ADR. *Forbids:* an
  `INotificationService` in the `BookmarkService` composition; a
  Wolverine handler for bookmarks.
- **D8 — The close flip is one atomic unit (U05).** The M17 close (U05)
  flips the roadmap parity **together**: `Milestones.cs` M17
  `StatusNext`→`StatusDone`, M18 `StatusPlanned`→`StatusNext`;
  `MilestonesTests.cs` re-pinned to
  `M18_Is_The_Single_InProgress_Milestone` + the shipped list gains M17 +
  M19 stays planned; the README **Roadmap** (M17 → done, M18 → in
  progress) + the M17 **Status** line; [`docs/STATUS.md`](../STATUS.md); and
  [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md)'s value-chain table gains the **M17 row**. All
  four surfaces ship **in the same unit** (the M15/U10, M16/U06
  docs-parity precedent). *Forbids:* reordering M18/M19; editing a
  shipped milestone's text; breaking the `MilestonesTests` pins;
  flipping one surface and not the others.

## Invariants (C-M17·1 … C-M17·7, locked verbatim)

- **C-M17·1 — The bookmark is a personal-by-id record; the surface adds
  zero authorization.** `Bookmark` carries `OwnerId` + `TargetKind` +
  `TargetId` and nothing that an access decision reads; there is **no**
  `Bookmark` adapter, **no** new `AccessAction`, **no** new
  `AccessVia`, **no** `Decide()` branch, **no** new
  `IAuthorizationService` method (D2). The **only** authorization call
  in the entire milestone is the D3 write-lane visibility check, which
  is the target's *own* frozen `CanAsync(Read)` — M17 adds no decision
  of its own.
- **C-M17·2 — The owner's list is a personal read, audited zero.**
  Loading one's **own** bookmark list is the M6-inbox / M9-conversation
  personal-read shape: **no** `CanSeeAsync` pass over the bookmark rows,
  **no** `AccessAudit` row for the owner's own list, **no** owner-check
  bypass. A **non-owner** (a `GlobalAdmin` included) gets a non-leaky
  **404** — and that 404 commits **no** `AccessAudit` row either (the
  ADR [0105](../adr/0105-messaging.md) "operator has no read standing" precedent).
- **C-M17·3 — Bookmarking requires visibility; bookmarking grants
  nothing.** The write lane re-runs the target's frozen `Read` decision
  **before** creating the row; Deny ⇒ **404** (not 403), and the
  bookmark row never appears in, widens, or is consulted by any
  `CanSeeAsync` pass anywhere (D3).
- **C-M17·4 — One row per (owner, target); toggle is idempotent.** The
  unique index on `(OwnerId, TargetKind, TargetId)` makes a duplicate
  bookmark a no-op; unbookmark removes the row; `Created` is never
  mutated (D4). A bookmark is a pointer, not content — removal is
  physical, there is no `IsDeleted`.
- **C-M17·5 — The list degrades, never leaks.** A bookmark whose target
  is absent, soft-deleted, or no longer visible renders the generic
  `bm.list.degraded` label — **no title, no link, no id** — and the
  unbookmark control still works on it (D5).
- **C-M17·6 — M17 is a standing core surface (no toggle).** M17 has
  **no** off-by-default admin toggle and **no** `LocaleSettings`-style
  admin switch (D6); it is always available to every resident.
- **C-M17·7 — The close flip is atomic.** The M17 close (U05) flips the
  four surfaces — `Milestones.cs` (M17→done, M18→next),
  `MilestonesTests.cs` (re-pinned to
  `M18_Is_The_Single_InProgress_Milestone` + shipped list + M19
  planned), the README Roadmap/Status, and [`docs/STATUS.md`](../STATUS.md) + the
  [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md) value-chain M17 row — **in the same unit**
  (D8).

## §kw-l — the key list (the `bm.*` keys × en/de/fr/da)

The closed list, locked here so U03 and the parity tests agree (adjust
individual names to the views as they are authored; the **list** is what
U03 registers — **14 keys** (the obs-2 ADR [0118](../adr/0118-bookmarks.md) amendment, 2026-09-30, adds
the two `bm.toggle.*` flash-toast keys to the original 12):

`bm.nav`, `bm.list.title`, `bm.list.empty`, `bm.list.degraded`,
`bm.list.kind.post`, `bm.list.kind.event`, `bm.list.kind.todo`,
`bm.list.kind.announcement`, `bm.list.kind.page`,
`bm.list.unbookmark`, `bm.button.bookmark`, `bm.button.bookmarked`,
`bm.toggle.bookmarked`, `bm.toggle.removed`.

> `bm.button.bookmarked` ("Bookmarked") is the button's **label** key — it is
> **not** consumed by `_BookmarkButton`. The 2026-10-04 F1 amendment
> (ADR [0118](../adr/0118-bookmarks.md)) completed the F1 design intent (the button reflects
> "bookmarked" on reload) with a two-lane affordance: the bookmarked state
> renders a **"Remove"** button that reuses the closed
> `bm.list.unbookmark` key (the D4 physical-removal lane), and the
> not-bookmarked state renders the **"Bookmark"** button (the D3
> `bm.button.bookmark` key). No new key was added — the closed 14-key set
> (pinned by `BmKeys_AreTheClosedFourteenKeySet`) is unchanged; the
> `bm.button.bookmarked` key stays registered in all four registries but is
> orphaned by the button markup (acceptable: the parity pin checks
> registration, not per-key consumption).

## §gate — the three acceptance tests (U05)

(a) **closed-loop** — a resident bookmarks a post they can see → it
appears in their `/bookmarks` under the `post` group → they unbookmark
it → it is gone; a second bookmark of the same post is a no-op — one
row, one `Created`.
(b) **handoff-authorization-boundary** — bookmarking a post the
resident *cannot* see ⇒ 404, no row, no Deny row surviving; a
`GlobalAdmin` who is not the owner of a bookmark list gets a 404 with
**no** `AccessAudit` row — the C-M17·2 zero-audit personal read.
(c) **part-vs-whole-audit-completeness** — the owner's own list load
commits **zero** `AccessAudit` rows — C-M17·2 — while the *write* lane's
single `CanAsync(Read)` check is the target's own frozen decision
(C-M17·3): the part (one toggle) and the whole (the list) audit
differently by design, and both pins hold together.

## §deferred — the deferred-lane list (each a named follow-on, own ADR)

- The "nudge the author on bookmark" lane (D7).
- ~~Bookmark folders~~ — **settled by ADR [0168](../adr/0168-bookmark-folders.md)** (personal folder tree, depth
  ≤ 2, owner-scoped, delete-moves-to-root). Manual **ordering** within a
  folder remains deferred (own ADR).
- Bookmark sharing / per-surface "top bookmarked" stats.
- Bookmarks on **groups** themselves (the ADR [0013](../adr/0013-group-posts-membership-lane.md) membership-lane
  surfaces have their own visibility world).

## §drift-guard — the frozen pins + the drift log

**Frozen pins** (a change to any of these after U00 is a **drift event**
— record it in the drift log below; a code unit pauses with `## U<m> —
Drift pause` per the unit-series rules): the frozen seams
(`IAuthorizationService`, `AccessAction`, `AccessVia`, `Decide()`); the
D1–D8 decisions; the C-M17·1…7 invariants; the F1–F5 + the named trade;
the §kw-l key list (14 keys); the §gate names (a/b/c); the closed
`TargetKind` set {`post`, `event`, `todo`, `announcement`, `page`}; the
`Bookmark` doc shape (the exact field set, §2.1); the
`IBookmarkService` ctor + the **4** public methods (the 4th,
`IsBookmarkedAsync`, added by the 2026-10-04 F1 amendment) + the
`BookmarkToggleStatus` enum (§2.2 — the ctor composes the frozen
`IAuthorizationService` + the owning surfaces' read seams +, per the
2026-09-30 D3/F2 amendment, the frozen `IIdentityService` for the
announcement branch only; the added dependencies are frozen seams, so they
are composition detail, not a new surface); the §2.3 / §2.4 tables; and
the **15** pinned test names (§2.5 — the 14 original + the amendment's
`GlobalAdmin_Beats_CommunityScope_On_Bookmark_Toggle`).

**Drift log:**

| Date | Unit | Change | Reason |
|---|---|---|---|
| 2026-10-04 | F1 (post-U00) | **F1 completed — the button reflects "bookmarked" on reload** (the F1 design intent left unfinished at ship: the detail-surface `_BookmarkButton` was a static "Bookmark" with no remove affordance, because `Toggle` always 302-redirects so the partial's "bookmarked" branch was dead code). The frozen `IBookmarkService` seam gains a **4th public method**, `Task<bool> IsBookmarkedAsync(ownerId, targetKind, targetId)` — a C-M17·2-shape personal read: NO target read (D5 / F4 — a dangling row still answers `true`), NO `AccessAudit` row, NO `CanAsync` pass (the `ListAsync` precedent), NO new `AccessAction` / `AccessVia` / `Decide()` branch. The Web layer needs **no new component**: `_BookmarkButton` `@inject`s the frozen `IBookmarkService` (the house `@inject`-in-view idiom, e.g. `Documents/New.cshtml`) and resolves the owner's per-target state inline via `await Bookmarks.IsBookmarkedAsync(ownerId, Kind, Id)` (a C-M17·2-shape personal read; an unsigned-in visitor / no `SubjectId` resolves NotBookmarked with no query — the C-M17·2 "operator has no read standing" gate). `_BookmarkButton` becomes two-lane: **bookmarked** ⇒ a form POSTing to `/bookmarks/{kind}/{id}/remove` with the `bm.list.unbookmark` ("Remove") label (the D4 physical-removal lane); **not bookmarked** ⇒ the existing `bm.button.bookmark` "Bookmark" form POSTing to `/bookmarks/toggle`. The `BookmarksController.Remove` action gains an **optional** `returnUrl` form field (redirect-back to the detail surface, the obs-2 precedent) — the `returnUrl`-carrying hidden input is already in the partial from obs-2, so no new markup beyond the lane branch. The partial is the **sole** resolver — it makes exactly one targeted personal read and no other service call (the C-M17·2 pin holds: the read is targeted, not the owner's full bookmark list; no `CanAsync` branch). **No** new `bm.*` key (the closed 14-key set is unchanged — the remove lane reuses `bm.list.unbookmark`; `bm.button.bookmarked` is now orphaned by the button, §kw-l note). The test-churn alternative (a mandatory `IBookmarkService` ctor param on the 6 detail controllers + view-model threading) was rejected — it would break ~29 test harnesses; the TagHelper is the house idiom for this. Pinned by the retargeted `BmButton_Partial_Renders_Correct_Lane_For_State` (the old `..._Label_For_State`; now asserts the two-lane structure + the `IBookmarkService` / `IsBookmarkedAsync` seam read + the `KumunitaPrincipal.SubjectId` owner read) + `BookmarkServiceTests` (the `IsBookmarkedAsync` personal read). | F1's design intent ("the button reflects 'bookmarked' on reload") was left unfinished at ship: the U04 handoff notes said it would be satisfied via `BookmarkState` from the row's `IsBookmarked` flag, but `Toggle` always redirects so no detail view ever set `ViewData["BookmarkState"]` and the partial's "bookmarked" branch was dead code — the button was a static "Bookmark" with no way to *remove* a bookmark from the detail surface. Adding one read-only personal-read method (the exact C-M17·2 shape) + a TagHelper (the house idiom for a per-request value consumed by many views) completes F1 without touching the frozen authorization surface, the closed key set, or the 6 detail controllers. See ADR [0118](../adr/0118-bookmarks.md) `## Amendments` (2026-10-04, F1). |
| 2026-09-30 | obs-4 (post-U00) | D3/F2 write-lane: the `announcement` branch of `BookmarkService.ResolveTargetAsync` now resolves the owner's real standing **in-Core** via the frozen `IIdentityService.GetBySubjectAsync` and hands that role set to `IAnnouncementService.GetAsync`, instead of the pre-amendment **empty** role set. The `BookmarkService` ctor gains an eighth dependency (`IIdentityService`); the `DependencyInjection.cs` M17 factory passes it. The **frozen `IBookmarkService` public signature is unchanged** (3 methods at that point; the 4th, `IsBookmarkedAsync`, landed with the 2026-10-04 F1 amendment); **no** new `AccessAction` / `AccessVia` / `Decide()` branch. Pinned by the 15th test `GlobalAdmin_Beats_CommunityScope_On_Bookmark_Toggle` (fails under the old empty-roles code). | The empty-roles lapse was a D3/F2 violation: the write lane re-ran a *stricter* read than the announcement detail page uses, so a `GlobalAdmin` / community-moderator who **could see** a community-targeted announcement was **denied** its bookmark. Resolving the owner's standing via the frozen principal-read seam restores the "same call the detail page uses" invariant (ADR [0118](../adr/0118-bookmarks.md) D3/F2). Fail-closed preserved: no EF account row ⇒ empty set ⇒ degrades exactly as before. See ADR [0118](../adr/0118-bookmarks.md) `## Amendments` (2026-09-30). |
| 2026-09-30 | obs-2 (post-U00) | **Redirect-back + flash toast** on the bookmark toggle (the button reflects the bookmark state on the surface the user clicked it on, instead of bouncing to `/bookmarks`): the `_BookmarkButton` partial gains a hidden `returnUrl` input (the surface's own detail URL); the `BookmarksController.Toggle` action accepts an optional `returnUrl` form field and redirects to it (or falls back to `/bookmarks` when absent), setting a localized flash (`bm.toggle.bookmarked`) in `TempData["info"]`; `Remove` also sets a flash (`bm.toggle.removed`). The `BookmarksController` ctor gains two **optional** dependencies (`ILocalizationService?`, `ITranslationProvider?`) — the house `T(key)` helper from `AdminPortabilityController`. Two new `bm.toggle.*` kw-l keys added to all four registries (the closed set is now **14 keys**). The **frozen `IBookmarkService` public signature is unchanged** (3 methods at that point; the 4th, `IsBookmarkedAsync`, landed with the 2026-10-04 F1 amendment); **no** new `AccessAction` / `AccessVia` / `Decide()` branch. Pinned by the retargeted `F1_Toggle_...` / `F4_...` / `M17AcceptanceGateTests.ClosedLoop` (redirect to detail URL + flash key assert) + `BmKeys_AreTheClosedFourteenKeySet` + the partial structural pin `BmButton_Partial_Form_Posts_To_Toggle_Endpoint` (the `returnUrl` hidden input). | The original M17 toggle always redirected to `/bookmarks` — the user lost their place on the detail surface (post, event, page, group post, announcement). The obs-2 amendment restores the surface context via the house flash-toast + redirect-back idiom (`AdminPortabilityController` precedent), without extending the frozen `IBookmarkService` seam. See ADR [0118](../adr/0118-bookmarks.md) `## Amendments` (2026-09-30, obs-2). |

---

## ADR [0118](../adr/0118-bookmarks.md) — moved content (D11 trim, 2026-11-02)

## Amendments

### 2026-10-16 — The inventory / directory kinds join the closed set (the M16 / M2 parity lane)

The M37 PersonalNotes closed set (ADR [0167](../adr/0167-personal-notes.md)) already carried the
`directory` kind — a note attaches to **any** content entity, including a
resident's directory page. The M17 surface, by contrast, still pinned the
closed `TargetKind` set to seven kinds (post / event / todo /
announcement / page / project / board) after the 2026-10-16 project /
board amendment — the inventory detail surface had **no bookmark
affordance** at all, and the directory detail surface had only the M37
`_NoteButton` (the view comment explicitly noted "the M17 bookmark closed
set does not include 'directory'").

The amendment (the M37 PersonalNotes D3 / D5 shape re-applied to the M17
surface — no new seam, no new decision, no new `AccessAction` /
`AccessVia` / `Decide()` branch / adapter; the existing frozen
`IInventoryService.GetItemAsync` read is reused for the inventory branch
(the same call the `/inventory/{id}` detail page uses — C-M17·3: the
target's own audit row commits, M17 commits none of its own) and the
existing frozen `IUserInfoService.GetProfileAsync` + `IAuthorizationService`
read is reused for the directory branch (the M37 PersonalNotes
`directory` D3 shape re-applied verbatim)):

- **`BookmarkService.ClosedTargetKinds` gains two kinds**: `inventory`
  and `directory` (the closed set is now **nine** — the M5
  `KanbanStatuses` string-not-enum shape pin holds: string, not enum,
  closed, drift-guarded). `ClosedTargetKindsOrdered` gains the two in
  the same canonical order (after `board`).
- **`BookmarkService` gains a new constructor dependency**: the frozen
  `IInventoryService` (the M16 Inventory detail page's own read — the
  same category of composition as the already-composed
  `IProjectService`). The `IBookmarkService` four-method public
  signature is unchanged (C-M17·1 holds — the composition detail does
  not cross the frozen seam).
- **`BookmarkService.ResolveTargetAsync`** (the D3 write-lane check)
  gains two branches: the `inventory` branch re-runs the frozen
  `IInventoryService.GetItemAsync(targetId, actorId)` read (the same
  call the `/inventory/{id}` detail page uses — C-M17·3); the `directory`
  branch re-runs the frozen `IUserInfoService.GetProfileAsync(targetId)`
  + `IAuthorizationService.CanAsync(Read, ProfileToAuditableResource)`
  read (the M37 PersonalNotes `directory` D3 shape re-applied verbatim).
  Both catch the non-visible / denied case, degrading to `Refused` — the
  M17 404-not-403 Web mapping (the M16 create-gate posture).
- **`BookmarkService.ResolveForListAsync`** (the D5 list resolution,
  the C-M17·2 pure non-auditing doc load) gains two branches: the
  `inventory` branch loads the `InventoryItem` doc directly and degrades
  on `IsDeleted` (the ADR [0024](../adr/0024-author-soft-delete-lane.md) soft-delete flag; `InventoryItem` has no
  `IsDraft` — D8a shape); the `directory` branch loads the profile
  directly (via the already-composed `IUserInfoService.GetProfileAsync`)
  and degrades on a blocked profile (the M37 PersonalNotes `directory`
  list-resolution shape re-applied verbatim). Both return the target's
  `Name` / `DisplayName` + the canonical detail URL (`/inventory/{id}` /
  `/directory/{id}`).
- **The two new `bm.list.kind.inventory` / `bm.list.kind.directory` kw-l
keys** are registered in all four language registries (en / de / fr /
da) — the `directory` label re-uses the M37 `pn.list.kind.directory`
terminology; the `inventory` label is new to M17. The closed `bm.*` key
set grows from 21 to **23 keys** (the
`BmKeys_AreTheClosedTwentyThreeKeySet` pin re-targets the count + the
two new keys).
- **`Views/Inventory/Detail.cshtml`** — the `_BookmarkButton` +
  `_NoteButton` partials join the top-right affordance row (the
  `Projects/TodoDetail.cshtml` ordering precedent: Bookmark first, Note
  second). The `Inventory` detail surface gains **both** affordances
  (the M16 create-gate posture is the D3 write-lane check; the M37
  PersonalNotes closed set already carried the `directory` kind, but the
  `inventory` kind was absent — this amendment adds it to the M37
  surface too, so the inventory detail page's note affordance is also
  now live).
- **`Views/Directory/Detail.cshtml`** — the `_BookmarkButton` partial
  joins the existing top-right affordance row (which already carried
  the `_NoteButton`). The partial's existing shape (the
  `IsBookmarkedAsync` targeted personal read, the two-lane
  Bookmark/Remove forms, the `returnUrl` hidden input) is unchanged —
  no new partial, no new controller action, no new view model.
- **`Views/Bookmarks/Index.cshtml`** — the `_kinds` closed-set array
  gains `inventory` + `directory` (the list view now renders the two new
  groups when the owner has pinned them).
- **`Views/Notes/Index.cshtml`** — the `_kinds` closed-set array gains
  `inventory` (the list view now renders the new group when the owner
  has noted one).
- **`PersonalNoteService`** — the M37 closed set gains the `inventory`
  kind (the M16 Inventory detail page's own read, re-applied: the
  `IInventoryService.GetItemAsync` branch in both `ResolveForListAsync`
  and `ResolveTargetAsync` — the same shape as the existing `board` /
  `document` / `directory` branches). A new constructor dependency on the
  frozen `IInventoryService` joins the composition (the `IPersonalNoteService`
  public signature is unchanged — C-M37·1 holds).
- **`pn.list.kind.inventory` kw-l key** is registered in all four
  language registries (en / de / fr / da) — the closed `pn.*` key set
  grows from 29 to **30 keys** (the
  `KnownTranslationKeys_PnKeys_RegisteredInAllFourLanguages` pin
  re-targets the count + the new key).
- **`tests/Kumunita.Core.Tests/BookmarkServiceTests.cs`** —
  `TargetKind_IsTheClosedStringSet` re-pinned to the nine-kind set
  (count 7 → 9); the harness composes the `InventoryService` (so the new
  `InventoryItem` doc loads + `GetItemAsync` reads resolve).
- **`tests/Kumunita.Core.Tests/PersonalNoteServiceTests.cs`** — the
  harness composes the `InventoryService` (so the new `inventory` branch
  resolves).
- **`tests/Kumunita.Web.Tests/BookmarksControllerTests.cs`** —
  `BmKeys_AreTheClosedTwentyThreeKeySet` re-targeted (count 21 → 23, the
  two new keys added to the `BmKeys` array).

**The frozen surfaces are untouched** (C-M17·1 / C-M37·1 hold): the
`IBookmarkService` **four-method public signature is byte-identical**
(`ListAsync` / `ToggleAsync` / `RemoveAsync` /
`IsBookmarkedAsync` — the 2026-10-04 F1 amendment's shape is the
reference); the `IPersonalNoteService` public signature is unchanged;
**no** new `AccessAction` / `AccessVia` / `Decide()` branch; **no** new
method on `IAuthorizationService`. The only changes are in the **Core
composition** (the two resolver switches gain two branches each — the
same shape as the existing `todo` / `event` / `board` branches; the two
services gain a new `IInventoryService` constructor dependency), the
**key registry** (two new `bm.list.kind.*` keys + one new
`pn.list.kind.*` key × 4 languages), the **views** (the two detail
surfaces gain the existing `_BookmarkButton` / `_NoteButton` partials;
the two list views' `_kinds` arrays gain the new entries), and the
**test pins** (the closed-set counts + the BmKeys / PnKeys counts
re-targeted). The drift-guard frozen list in the design doc is amended
to count the closed set as **nine kinds** (was seven after the
project / board amendment) and to name `inventory` / `directory` among
the closed vocabulary. The §kw-l key list is amended to count **23
keys** (was 21 after the project / board amendment) for `bm.*` and
**30 keys** (was 29) for `pn.*`.

### 2026-10-16 — The project / board kinds join the closed set (the M37 parity lane)

The M37 PersonalNotes closed set (ADR [0167](../adr/0167-personal-notes.md)) already carried the
`project` and `board` kinds — a note attaches to **any** content entity,
including projects and Kanban boards. The M17 surface, by contrast,
still pinned the closed `TargetKind` set to the original five (post /
event / todo / announcement / page) — the project and board detail
surfaces had **no bookmark affordance** (the view comments explicitly
noted "the M17 bookmark closed set does not include 'project' / 'board'";
the M37 _NoteButton partial was the only pin-style affordance on those
two surfaces).

The amendment (the M37 PersonalNotes D3 / D5 shape re-applied to the
M17 surface — no new seam, no new decision, no new `AccessAction` /
`AccessVia` / `Decide()` branch / adapter; the existing frozen
`IProjectService.GetProjectAsync` / `GetBoardAsync` reads are reused,
**exactly** the category the existing `todo` branch already
establishes):

- **`BookmarkService.ClosedTargetKinds` gains two kinds**: `project`
  and `board` (the closed set is now **seven** — the M5
  `KanbanStatuses` string-not-enum shape pin holds: string, not enum,
  closed, drift-guarded). `ClosedTargetKindsOrdered` gains the two in
  the same canonical order (after `page`, before nothing — the M37
  PersonalNotes closed-set order is the source of truth).
- **`BookmarkService.ResolveTargetAsync`** (the D3 write-lane check)
gains two branches: the `project` branch re-runs the frozen
  `IProjectService.GetProjectAsync(targetId, actorId)` read (the same
call the `/projects/{id}` detail page uses — C-M17·3: the target's own
audit row commits, M17 commits none of its own); the `board` branch
re-runs `IProjectService.GetBoardAsync(targetId, actorId)` (the same
call the `/projects/boards/{id}` detail page uses). Both catch
`KeyNotFoundException` (404 on absent / soft-deleted) and
`UnauthorizedAccessException` (403 on denied), degrading to
`Refused` — the M17 404-not-403 Web mapping collapses the split to a
non-leaky 404 (the M16 create-gate posture).
- **`BookmarkService.ResolveForListAsync`** (the D5 list resolution,
the C-M17·2 pure non-auditing doc load) gains two branches: the
`project` branch loads the `Project` doc directly and degrades on
`IsDeleted` (the ADR [0024](../adr/0024-author-soft-delete-lane.md) soft-delete flag; `Project` has no `IsDraft`
— D8a — a project is live on creation); the `board` branch loads the
`KanbanBoard` doc directly and degrades on `IsDeleted` (the same D8a
shape). Both return the target's `Title` + the canonical detail URL
(`/projects/{id}` / `/projects/boards/{id}`) — the M37 PersonalNotes
`project` / `board` list-resolution shape re-applied verbatim.
- **`BookmarkService.IsBookmarkedAsync`** — the closed-set check
(`ClosedTargetKinds.Contains(targetKind)`) is now seven-wide
automatically (the existing `todo` branch precedent — no change to the
method body needed).
- **The `Bookmark` doc's `TargetKind` comment** documents the new
closed set (seven kinds) + the amendment's date.
- **The two new `bm.list.kind.project` / `bm.list.kind.board` kw-l
keys** are registered in all four language registries (en / de / fr /
da) — the terminology matches the M37 `pn.list.kind.project` /
`pn.list.kind.board` labels (one label, one word, across all four
languages). The closed `bm.*` key set grows from 19 to **21 keys**
(the `BmKeys_AreTheClosedTwentyOneKeySet` pin re-targets the count +
the two new keys).
- **`Views/Projects/ProjectDetail.cshtml`** +
  **`Views/Projects/BoardDetail.cshtml`** — the `_BookmarkButton`
  partial joins the top-right affordance row (the `Posts/Detail.cshtml`
  ordering precedent: Bookmark first, Note second). The partial's
  existing shape (the `IsBookmarkedAsync` targeted personal read, the
  two-lane Bookmark/Remove forms, the `returnUrl` hidden input) is
  unchanged — no new partial, no new controller action, no new view
  model.
- **`Views/Bookmarks/Index.cshtml`** — the `_kinds` closed-set array
  gains `project` + `board` (the list view now renders the two new
  groups when the owner has pinned them).
- **`tests/Kumunita.Core.Tests/BookmarkServiceTests.cs`** —
  `TargetKind_IsTheClosedStringSet` re-pinned to the seven-kind set
  (count 5 → 7); the harness boots `ProjectDocTypes` (so the new
  `Project` / `KanbanBoard` doc loads resolve); six new tests (16–21)
  pin the new lanes: F2 write-lane positive + negative for both
  `project` and `board` (the M37 PersonalNotes D3 shape re-applied —
  visible target ⇒ Bookmarked, invisible target ⇒ Refused), F4 list
  resolution positive (Title + Link for a visible project / board) +
  negative (Degrade on a dangling project / board), and the
  soft-delete negative (an `IsDeleted = true` project / board is
  Refused — the D3 table's "absent / soft-deleted" row).
- **`tests/Kumunita.Web.Tests/BookmarksControllerTests.cs`** —
  `BmKeys_AreTheClosedTwentyOneKeySet` re-targeted (count 19 → 21, the
  two new keys added to the `BmKeys` array).

**The frozen surfaces are untouched** (C-M17·1 holds): the
`IBookmarkService` **four-method public signature is byte-identical**
(`ListAsync` / `ToggleAsync` / `RemoveAsync` /
`IsBookmarkedAsync` — the 2026-10-04 F1 amendment's shape is the
reference); **no** new `AccessAction` / `AccessVia` / `Decide()`
branch; **no** new method on `IAuthorizationService`. The only changes
are in the **Core composition** (the `BookmarkService`'s two resolver
switches gain two branches each — the same shape as the existing
`todo` / `event` branches), the **key registry** (two new
`bm.list.kind.*` keys × 4 languages), the **views** (the two detail
surfaces gain the existing `_BookmarkButton` partial; the list view's
`_kinds` array gains two entries), and the **test pins** (the closed-set
count + the BmKeys count re-targeted; six new seam tests). The
drift-guard frozen list in the design doc is amended to count the
closed set as **seven kinds** (was five at M17 ship) and to name
`project` / `board` among the closed vocabulary. The §kw-l key list is
amended to count **21 keys** (was 19 after the ADR [0168](../adr/0168-bookmark-folders.md) folder-tree
block, 2026-10-14).

### 2026-10-04 — F1 completed: the button reflects "bookmarked" on reload (the remove affordance)

The F1 design intent — *"the button reflects 'bookmarked' on reload"* —
was left **unfinished** at ship: the detail-surface `_BookmarkButton`
partial was a static **"Bookmark"** button with **no remove affordance**.
The root cause: `Toggle` always 302-redirects, so the partial's
"bookmarked" branch (driven by `ViewData["BookmarkState"]`) was **dead
code** — no detail view ever set `ViewData["BookmarkState"]`, because the
state was only meaningful on a same-request re-render, which the
redirect-after-write flow never produces. The obs-2 amendment (2026-09-30)
noted the F1 pin was "satisfied by the pre-existing `BookmarkState`
ViewData exception path" — but that path was never exercised by the detail
surfaces, so the user could bookmark a post and then have **no way to
remove it** except via the `/bookmarks` list.

The amendment (completing F1 with the house's **`@inject`-in-view idiom** —
resolve a per-target value inline in the view that renders it, e.g.
`Documents/New.cshtml` `@inject`s `IUserInfoService` and `await`s a read in
its `@{ }` block — rather than threading state through every controller +
view-model):

- **`IBookmarkService` seam gains a 4th public method**,
  `Task<bool> IsBookmarkedAsync(string ownerId, string targetKind, string
  targetId)` — a **C-M17·2-shape personal read**: it queries the owner's
  own row (`Where(b => b.OwnerId == ownerId && b.TargetKind == targetKind
  && b.TargetId == targetId)`) with **NO target read** (D5 / F4 — a
  dangling row still answers `true`), **NO** `AccessAudit` row, **NO**
  `CanAsync` pass (the `ListAsync` precedent), and **NO** new
  `AccessAction` / `AccessVia` / `Decide()` branch. It is a read-only
  personal-read method in the exact C-M17·2 shape, not a new
  authorization surface — the frozen-seam surface (D2 / C-M17·1) is
  unchanged.
- **`_BookmarkButton` becomes two-lane** — the partial `@inject`s the
  frozen `IBookmarkService` and resolves the state inline in its `@{ }`
  block: `var ownerId = User is not null ? KumunitaPrincipal.SubjectId(User)
  : null; var bookmarked = ownerId is not null && await
  Bookmarks.IsBookmarkedAsync(ownerId, Kind, Id);`. An unsigned-in visitor
  (no `SubjectId`) resolves NotBookmarked with **no query** (the C-M17·2
  "operator has no read standing" gate). **Bookmarked** ⇒ a form POSTing
  to `/bookmarks/{kind}/{id}/remove` with the **`bm.list.unbookmark`**
  ("Remove") label — the D4 physical-removal lane; **not bookmarked** ⇒
  the existing **`bm.button.bookmark`** "Bookmark" form POSTing to
  `/bookmarks/toggle` (the D3 write-lane). The partial is the **sole**
  resolver — it makes exactly one targeted personal read and no other
  service call (the C-M17·2 pin holds: the read is targeted, not the
  owner's full bookmark list; no `CanAsync` branch).
- **`BookmarksController.Remove`** — the signature gains an **optional**
  `returnUrl` form field (`Remove(string targetKind, string targetId,
  string? returnUrl = null)`), redirecting to `returnUrl` when present
  (the obs-2 precedent, the `Toggle` shape) so removing a bookmark from a
  detail surface keeps the actor on that surface; it still sets the
  `bm.toggle.removed` flash.
- **No new `bm.*` key** — the remove lane reuses the closed
  `bm.list.unbookmark` key; the `bm.button.bookmarked` label key is now
  **orphaned** by the button (still registered in all four registries, the
  closed 14-key set pinned by `BmKeys_AreTheClosedFourteenKeySet` is
  unchanged; the parity pin checks registration, not per-key
  consumption). See the design doc §kw-l note.
- **Test churn avoided** — the alternative (a mandatory
  `IBookmarkService` ctor param on the 6 detail controllers + view-model
  threading) would have broken ~29 test harnesses; the house
  `@inject`-in-view idiom resolves a per-target value in the view that
  renders it, so no controller or view-model change was needed.

Pinned by the retargeted `BmButton_Partial_Renders_Correct_Lane_For_State`
(the old `BmButton_Partial_Renders_Correct_Label_For_State`; now asserts
the two-lane structure + the `IBookmarkService` / `IsBookmarkedAsync`
seam read + the `KumunitaPrincipal.SubjectId` owner read) +
`BookmarkServiceTests` (the `IsBookmarkedAsync` personal read). The
**frozen `IBookmarkService` public signature** changes from 3 to 4
methods — recorded as a **deliberate amendment** (the design doc §2.7
drift log, 2026-10-04 row), not a silent drift: it is one read-only
personal-read method in the C-M17·2 shape, adding no `AccessAudit`
surface, no `CanAsync` branch, and no authorization decision of its own.

### 2026-09-30 — D3/F2 write-lane: the announcement branch resolves the owner's real standing in-Core (the obs-4 lapse closed)

The **D3** write-lane rule — *"re-runs the target's frozen `Read`
decision … the *same call the detail page uses*"* — is amended in its
**application to the `announcement` target kind only**, and is a
**composition detail, not a new frozen surface**. The original
implementation passed an **empty** role set to
`IAnnouncementService.GetAsync` (the Bookmarks seam hands us only an
owner id — roles never cross the frozen `IBookmarkService` seam), so a
`GlobalAdmin` or community-moderator who could see a **community-targeted**
announcement on the detail page was **denied** the bookmark of it — a
direct D3/F2 violation (the write lane was re-running a *stricter* read
than the detail page uses, the exact divergence D3 forbids).

The amendment: the `announcement` branch of
`BookmarkService.ResolveTargetAsync` now resolves the owner's real
standing **in-Core** via the frozen `IIdentityService.GetBySubjectAsync`
(the ADR [0006](../adr/0006-module-boundary-contracts.md) §A principal-read seam — the **same category of
composition** as the already-composed `IUserInfoService`, which
`GetBySubjectAsync` itself reads), and hands that thin-principal role set
to the announcement read. A `GlobalAdmin` therefore resolves a
community-targeted row (matching the detail page) instead of the old
empty-set denial. **Fail-closed shape preserved:** no EF account row (or
no principal) ⇒ empty set ⇒ the read degrades exactly as before.

**The frozen surfaces are untouched** (C-M17·1 holds): the
`IBookmarkService` **3-method public signature is byte-identical**
(`ListAsync` / `ToggleAsync` / `RemoveAsync` — roles never cross the
seam); **no** new `AccessAction` / `AccessVia` / `Decide()` branch;
**no** new method on `IAuthorizationService`; the only change is one
extra **frozen-seam** composition (the `BookmarkService` ctor gains an
eighth dependency — `IIdentityService` — and the `DependencyInjection.cs`
M17 factory passes it), exactly the category the original seven
(`IUserInfoService`, `IAuthorizationService`, `IDocumentStore`,
`IAnnouncementService`, `IPageService`, `IEventService`,
`IProjectService`) already established. Pinned by the 15th
`BookmarkServiceTests` case
`GlobalAdmin_Beats_CommunityScope_On_Bookmark_Toggle` (it **fails** under
the pre-amendment empty-roles code and **passes** under this one — the
witness). The §drift-guard frozen list in the design doc is amended to
count **15** pinned `BookmarkServiceTests` names and to name
`IIdentityService` among the frozen seams the `BookmarkService` composes.

### 2026-09-30 — obs-2: redirect-back + flash toast on the bookmark toggle

The original M17 `Toggle` action always redirected to `/bookmarks`
(the "redirect after write" precedent). This is a **Web-layer
usability gap**, not a decision-lane change: the user who clicked
"Bookmark" on a post detail page (or event, or page, or group post, or
announcement) was **bounced off the surface** they were reading, losing
their place. The F1 "button reflects bookmarked on reload" pin
(satisfied by the pre-existing `BookmarkState` ViewData exception path)
is unchanged — this amendment adds the **post-toggle confirmation** the
user was previously missing.

The amendment (the house flash-toast + redirect-back idiom — the
`AdminPortabilityController` precedent):

- **`_BookmarkButton` partial** — the model changes from
  `(string Kind, string Id)` to `(string Kind, string Id, string?
  ReturnUrl)`; a new `<input type="hidden" name="returnUrl"
  value="@Model.ReturnUrl" />` is added to the form. All six render
  sites pass the surface's own detail URL as the third element (e.g.
  `("post", Model.Post.Id, $"/posts/{Model.Post.Id}")` for the post
  detail; `("page", Model.Id, Model.Path)` for the page surface —
  `Model.Path` is the `PagePaths.Href` the view model already carries).
- **`BookmarksController.Toggle`** — the signature changes from
  `Toggle(string kind, string id)` to `Toggle(string kind, string id,
  string? returnUrl = null)`. On success (`Bookmarked` /
  `AlreadyBookmarked`), the action sets
  `TempData["info"] = await T("bm.toggle.bookmarked")` and redirects
  to `returnUrl` (or falls back to `/bookmarks` when the field is
  absent — the original M17 behavior, unchanged for callers that don't
  pass it). `Refused` → 404 (unchanged).
- **`BookmarksController.Remove`** — sets
  `TempData["info"] = await T("bm.toggle.removed")` before its
  existing `return Redirect("/bookmarks")` (the remove is initiated
  from the `/bookmarks` list — the redirect target is unchanged; the
  flash toast confirms the removal).
- **`BookmarksController` ctor** — gains two **optional** parameters:
  `ILocalizationService? localization = null` and
  `ITranslationProvider? translationProvider = null` (the
  `AdminPortabilityController` house idiom). The `T(string key)`
  private helper resolves the kw-l key to the operator's effective
  language; when either parameter is null (the test-harness floor),
  `T` returns the raw key — the tests assert the raw key directly.
- **Two new `bm.toggle.*` kw-l keys** added to all four registries
  (en / de / fr / da): `bm.toggle.bookmarked` and
  `bm.toggle.removed`. The closed `bm.*` key set is now **14 keys**
  (the original 12 + the obs-2 flash pair). The `BmKeys` array in
  `BookmarksControllerTests.cs` is extended accordingly; the
  `BmKeys_AreTheClosedTwelveKeySet` test is renamed
  `BmKeys_AreTheClosedFourteenKeySet` and asserts
  `Assert.Equal(14, actualBmKeys.Count)`. The
  `KnownTranslationKeys_ParityTests` (de/fr counts == en count) pass
  automatically since both keys are added to all four languages.

**The frozen surfaces are untouched** (C-M17·1 holds): the
`IBookmarkService` **3-method public signature is byte-identical**;
**no** new `AccessAction` / `AccessVia` / `Decide()` branch; **no**
new method on `IAuthorizationService`. The only change is in the
**Web layer** (the `BookmarksController` + the `_BookmarkButton`
partial + the six render sites) and the **key registry** (two new
`bm.toggle.*` keys × 4 languages). Pinned by the retargeted
`F1_Toggle_Idempotent_One_Row_Second_Call_Returns_AlreadyBookmarked`
(redirect to the detail URL + `TempData["info"] == "bm.toggle.bookmarked"`),
the `F4_Unbookmark_On_Degraded_Row_Still_Works` flash pin
(`TempData["info"] == "bm.toggle.removed"`), the
`M17AcceptanceGateTests.ClosedLoop` retarget (toggle → detail URL,
remove → `/bookmarks` + flash), the `BmKeys_AreTheClosedFourteenKeySet`
(14-key closed set), and the `BmButton_Partial_Form_Posts_To_Toggle_Endpoint`
structural pin (the `returnUrl` hidden input). The design doc
§drift-guard frozen list is amended to count **14 keys** in the §kw-l
key list and to carry the obs-2 drift-log row.
