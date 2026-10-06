# M26 — Sorting — handoff notes

> **Scratch tier** (the M25 `m25-handoff-notes.md` precedent). One `## U#`
> section per unit, **appended** (never rewritten); the next unit reads only
> this file's latest section + its own entry-read list. Created by U1; moved
> to `docs/plans-milestones/done/m26/` by the close unit U18.

## U1 — design doc Part 1

- **C-SORT·6 precondition: PASS.** `src/Kumunita.Web/Milestones.cs` read —
  M25 (Upload limits) is `StatusDone`, M26 (Sorting) is `StatusNext`, M27 is
  `StatusPlanned`. The single-in-progress contract holds; M26 may begin.
- **Invariants (frozen, 8):** C-SORT·1 (closed allowlist per surface),
  C-SORT·2 (default-preserving), C-SORT·3 (Core HTTP-free), C-SORT·4 (sort
  is a display facet, never a gate), C-SORT·5 (stable ordering / unique
  tie-breaker), C-SORT·6 (single-in-progress milestone contract), C-SORT·7
  (no new bounded context / document / schema), C-SORT·8 (sort rides the
  pager).
- **FACES (frozen, 12):** F1 (absent sort → current order preserved exactly),
  F2 (allowed key + `dir=asc` → ascending), F3 (allowed key + `dir=desc` →
  descending), F4 (non-allowlisted key → surface default, no error), F5
  (invalid/absent `dir=` → key's default direction), F6 (re-orders only the
  authorized set), F7 (stable tie-breaker across a `HasMore` window), F8
  (pager links carry `?sort=`/`?dir=`), F9 (control offers exactly the
  allowed keys), F10 (signed-in/anonymous get the same sort behavior),
  F11 (no new `AccessAudit` row on a sorted read), F12 (`sort.*` labels
  resolve en/de/fr/da).
- **Deliverables landed:** `docs/design/m26-sorting-design.md` Part 1
  (Context / Scope / the verified reused surface / decisions D-SORT·1–8 /
  invariants C-SORT·1–8 / FACES F1–F12 / the 18-surface catalog (proposal —
  U2 confirms against the Core models) / parts affected / risks /
  drift-guard). No code, no build.
- **Handoff to U2:** append `## Seams & contracts (Part 2, written by U2)`
  to the design doc — exact C# shapes (U3–U9), per-surface allowlists
  confirmed against the models, the Web `_Sort` contract + pager-carry rule,
  the pinned test names, the U17 acceptance gate, and the drift-guard.
  See `m26-u02.md`.

## U2 — design doc Part 2

- **`SortSpec`/`SortKeys` shape (locked, §2.1):** namespace
  `Kumunita.Core.Query`; `record SortSpec(string Key, bool Descending)`
  (lowercase key, resolved direction) + pure static
  `SortKeys.Parse(key, dir, IReadOnlySet<string> allowedKeys, defaultKey,
  defaultDir)`; **no DI registration** (U3 confirms zero new
  `AddKumunitaCore` lines).
- **Allowlists: 18/18 locked (§2.2)** against the actual models, with 3
  model-confirmed corrections (locked in §2.8): C-1 tag→posts default is
  **asc** (`TagService.cs:531` is `OrderBy`, not `OrderByDescending`); C-2
  people surfaces have **no `created` key** (`Profile` has no `Created`
  field — `name`/`DisplayName` only); C-3 todos have **no `priority` key**
  (`TodoItem` has no `Priority` property — `status` instead).
- **Seam pins (§2.3):** all 18 current `OrderBy…` lines pinned with
  file:line; additive `SortSpec? sort = null`, `null` = byte-for-byte
  unchanged order; `.ThenBy(x => x.Id)` tie-breaker on all.
- **Test files (pinned counts):** Core **13** (1 parser + 12 group files,
  names per §2.5); Web **9** (1 + 8 surface groups, names per §2.6).
- **U17 gate (§2.7):** closed-loop (sorted ≡ unsorted authorization,
  hidden stays hidden) / handoff (unknown `?sort=` → default, no error) /
  part-vs-whole (pager carries `?sort=`/`?dir=` across all 18 surfaces).
- **Handoff to U3:** add the pure Core `SortSpec` + `SortKeys.Parse` in
  `Kumunita.Core.Query` + the 6 pinned `SortSpecTests` (no Postgres, no DI).
  See `m26-u03.md`.

## U3 — Core SortSpec

- **Landed:** `src/Kumunita.Core/Query/SortSpec.cs` — `sealed record
  SortSpec(string Key, bool Descending)` + `static class SortKeys.Parse(key,
  dir, allowedKeys, defaultKey, defaultDir)` in the new
  `Kumunita.Core.Query` namespace (byte-for-byte per design §2.1 — no drift).
  **DI confirmation: zero new `AddKumunitaCore` registrations** (value object +
  static parser — C-SORT·7 holds; `DependencyInjection.cs` convention read, no
  precedent requires a registration).
- **Tests:** `tests/Kumunita.Core.Tests/Query/SortSpecTests.cs` — all 6 pinned
  pure tests pass (no Postgres): `Parse_AllowedKey_Applies`,
  `Parse_UnknownKey_Defaults`, `Parse_InvalidDir_Defaults`, `Parse_NullKey_Defaults`,
  `Parse_DirAsc_DescendingFalse`, `Parse_DirDesc_DescendingTrue`.
- **Key normalization: lowercase** (per U2 §2.1 pin) — `Parse` applies
  `Trim().ToLowerInvariant()` before the allowlist match; `SortSpec.Key` is
  always lowercase. `dir` matching is exact (`"asc"`/`"desc"`), other/null →
  the key's default direction.
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0 errors);
  `Kumunita.Core.Tests` suite 1251 total, 0 failed (6 new + existing).
- **Handoff to U4:** additive `SortSpec? sort = null` on the 3 post-feed
  seams + the `PostFeed_*` group tests (design §2.3 rows 1–3, §2.5 pins 7–12).
  See `m26-u04.md`.

## U4 — Core post feeds

- **Landed:** `SortSpec? sort = null` added to the 3 post-feed seams
  (`PostService.ListFeedAsync` / `ListAllFeedAsync` / `ListGroupFeedAsync`) —
  the shared ordering lives in a single private helper `OrderByPostSort`
  (`src/Kumunita.Core/Posts/PostService.cs`) so all three apply the identical
  closed allowlist (U2 §2.2 rows 1–3: `created`/`modified`/`title`) + the
  `.ThenBy(Id)` tie-breaker. **Interface (deliverable 2) is a no-op:**
  `PostService` is a `sealed` concrete class with **no** `IPostService`
  interface in the tree (confirmed — the file-attachments U3 note pins this),
  so no interface seam to edit. `null` path is byte-for-byte the pinned
  `OrderByDescending(p => p.Created)` (C-SORT·2 confirmed); `CanSeeAsync` /
  `HasMore` / `Total` untouched (C-SORT·4).
- **Tests:** `tests/Kumunita.Core.Tests/Posts/PostSortTests.cs` — all 6 pinned
  `PostFeed_*` names pass (driving `ListFeedAsync`; the other two seams share
  `OrderByPostSort`): `PostFeed_SortSpecNull_CurrentOrder`,
  `PostFeed_SortCreatedAsc`, `PostFeed_SortModifiedDesc`,
  `PostFeed_SortTitle_Ordinal`, `PostFeed_InvalidKey_DefaultOrder`,
  `PostFeed_StableTieBreakBy_Id`.
- **Tie-breaker pin (C-SORT·5):** `.ThenBy(Id)` present on every non-null sort
  path — written as `Queryable.ThenBy(q.OrderBy(…), p => p.Id)`.
- **Marten 9.31.2 drift (documented, NOT a silent change):** the frozen
  Part-2 comparator rules pin `?? MinValue` (modified) / `?? ""` (title)
  sentinels in the OrderBy key, but Marten's Linq parser rejects both
  (`BadLinqExpressionException: Invalid OrderBy() expression` — verified by a
  probe). Per the user's call, the keys order on the **raw nullable column**
  and Postgres supplies the null-ordering (nulls-first in desc, nulls-last in
  asc — the opposite of the pinned sentinels); the `OrdinalIgnoreCase`
  comparator on `title` is preserved. `PostFeed_SortModifiedDesc` /
  `PostFeed_SortTitle_Ordinal` pin the actual Postgres behavior and carry the
  drift comment. **Part 2 (§2.2/§2.3) is unchanged** — the deviation is
  recorded here and in the `OrderByPostSort` doc-comment. Downstream units
  (U5–U9) with nullable keys will hit the same Marten limit.
- **Also (compiler):** on Marten's `IAsyncQueryable`, the unqualified
  `.ThenBy(…)` form is ambiguous with an async-enumerable extension (CS0411);
  the fully-qualified `Queryable.ThenBy(…)` is the one that resolves.
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0 errors);
  `Kumunita.Core.Tests` suite 1257 total, 0 failed (6 new + existing).
- **Handoff to U5:** additive `SortSpec? sort = null` on the 3 event seams
  (`EventService.ListUpcomingAsync` / `ListPastAsync` /
  `ListGroupEventsAsync`) + the `Event*_*` group tests. Note the same Marten
  nullable-key limit will apply to any nullable event key. See `m26-u05.md`.

## U5 — Core events

- **Landed:** `SortSpec? sort = null` added to the 3 event seams
  (`EventService.ListUpcomingAsync` / `ListPastAsync` /
  `ListGroupEventsAsync`) — the shared ordering lives in a single private
  helper `OrderByEventSort` (`src/Kumunita.Core/Events/EventService.cs`),
  mirroring U4's `OrderByPostSort` shape, taking a `defaultAscending` flag so
  each sub-surface keeps its **own** pinned direction (U2 §2.2 rows 4–6:
  `start`/`created`/`title`). The `IEventService` seams gained the matching
  parameter (deliverable 2 — a real edit here, unlike U4, since
  `IEventService` exists). **`null` path is byte-for-byte the pinned line per
  sub-surface:** upcoming / group = `OrderBy(Start)` **asc**, past =
  `OrderByDescending(Start)` **desc** (C-SORT·2; the upcoming-asc/past-desc
  split is preserved, not collapsed). `CanSeeAsync` / `CanSeeGroupFeedAsync` /
  `HasMore` untouched (C-SORT·4).
- **Tests:** `tests/Kumunita.Core.Tests/Events/EventSortTests.cs` — all 8
  pinned `Event*_*` names pass: `EventUpcoming_SortSpecNull_CurrentOrderAsc`,
  `EventPast_SortSpecNull_CurrentOrderDesc`, `EventUpcoming_SortCreatedAsc`,
  `EventUpcoming_SortCreatedDesc`, `EventUpcoming_SortTitle_Ordinal`,
  `EventPast_SortTitle_Ordinal`, `EventUpcoming_InvalidKey_DefaultOrder`,
  `EventUpcoming_StableTieBreakBy_Id`.
- **Tie-breaker pin (C-SORT·5):** `Queryable.ThenBy(..., e => e.Id)` present
  on every non-null sort path (the fully-qualified form — U4's carry-forward
  (1), the unqualified form is ambiguous on Marten's `IAsyncQueryable`).
- **U4 carry-forward (3) confirmed, NOT a drift:** U2 §2.2 rows 4–6 keys
  (`Start`/`Created`/`Title`) are all **non-null** in the `Event` model, so
  the Marten 9.31.2 `?? sentinel` limit (U4's `BadLinqExpressionException`)
  does not apply here — no sentinel forms used, no documented deviation.
- **Parameter order (user-confirmed deviation from Part 2 §2.3 literal text):**
  the 3 event seams already have a positional `CancellationToken ct` that ~27
  existing call sites pass positionally (3 in `EventController.cs`, 1 in
  `IcsFeedTests.cs`, ~23 NSubstitute stubs in `Kumunita.Web.Tests`).
  §2.3's literal "before `ct`" ordering would break all of those (far outside
  this Core unit's ≤3-file scope) and defeat §2.3's own "a new caller compiles
  unchanged" intent; U4's `PostService` precedent has no `ct` at all (so the
  clause never applied there). Per the user's call, `sort` is placed **after**
  `ct` (the trailing param): `..., int page, CancellationToken ct = default,
  SortSpec? sort = null`. **Part 2 (§2.2/§2.3) is unchanged** — this ordering
  choice is recorded here and in the `OrderByEventSort` doc-comment. Downstream
  units (U6–U9) with a positional `ct` seam should follow the same trailing-
  `sort` convention.
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0 errors);
  `Kumunita.Core.Tests` suite 1265 total, 0 failed (8 new + existing; U4 was
  1257).
- **Handoff to U6:** additive `SortSpec? sort = null` on the 4 project seams
  + the `Project*_*`/`Todo*_*` tests. See `m26-u06.md`.

## U6 — Core projects

- **Landed:** `SortSpec? sort = null` added to the 4 project seams
  (`ProjectService.ListTodosAsync` / `ListBoardsAsync` / `ListProjectsAsync` /
  `ListGoalsAsync`) + the 4 `IProjectService` seams (deliverable 2 — a real
  edit, `IProjectService` exists). The shared ordering lives in two private
  helpers in `src/Kumunita.Core/Projects/ProjectService.cs`:
  `OrderByProjectSort<T,…>` (Boards / Projects / Goals — the identical
  `created`/`modified`/`title` allowlist, U2 §2.2 rows 8–10; the three models
  share the shape but have no common base type, so each call site passes its
  own `created`/`modified`/`title`/`id` selectors) and `OrderByTodoSort`
  (Todos — U2 §2.2 row 7: `created`/`modified`/`title`/`due`/`status`). Both
  keep the pinned `OrderByDescending(Created)` **byte-for-byte** on the
  `null` path (C-SORT·2) and apply `Queryable.ThenBy(… , x => x.Id)` on every
  non-null path (C-SORT·5; the fully-qualified form — U4/U5 carry-forward
  (1)). `CanSeeAsync` / `HasMore` / `Skip`/`Take` untouched (C-SORT·4). The
  trailing-`sort` param convention (U5) is followed — all 4 seams already have
  a positional `CancellationToken ct`, so `sort` is the trailing param.
- **Tests:** `tests/Kumunita.Core.Tests/Projects/ProjectSortTests.cs` — all
  pinned `Project*_*`/`Todo*_*` names pass: `Todo_SortSpecNull_CurrentOrder`,
  `Todo_SortDue_NullsLast`, `Todo_SortDue_NullsLast_Desc`, `Todo_SortStatus`,
  `Todo_SortTitle_Ordinal`, `Board_SortCreatedAsc`, `Board_SortTitle_Ordinal`,
  `Project_SortCreatedDesc`, `Project_SortModifiedAsc`, `Goal_SortCreatedAsc`,
  `Todo_InvalidKey_DefaultOrder`, `Todo_StableTieBreakBy_Id`.
- **`due` nulls-last pin — MARTEN DRIFT (U6, the C-5 carry-forward bit, as
  expected):** U2 §2.2 row 7 pins `due` as "nulls last in *both* directions"
  via a `ThenBy(t => t.DueAt is null)` boolean flag before the value compare.
  Marten 9.31.2's Linq parser rejects **any** non-member OrderBy expression —
  `t.DueAt is null` / `t.DueAt == null` / `t.DueAt ?? sentinel` all throw
  `BadLinqExpressionException: Invalid OrderBy() expression` (verified by
  probe). So the `due` key orders on the **raw nullable column** and Postgres
  supplies its default null-ordering: nulls-**last** in asc, nulls-**first**
  in desc (the opposite of the pinned both-dirs rule). The
  `Todo_SortDue_NullsLast` / `Todo_SortDue_NullsLast_Desc` tests pin the
  *actual* Postgres behavior. **Part 2 (§2.2/§2.3) is unchanged** — recorded
  here and in the `OrderByTodoSort` doc-comment. The same limit applies to the
  `modified` (nullable date) + `status` (nullable string) keys — they order on
  the raw column too (Postgres nulls-last asc / nulls-first desc), and
  `Todo_SortStatus` + `Project_SortModifiedAsc` pin that (the `OrdinalIgnoreCase`
  comparator is preserved for the non-null comparison).
- **Brief-vs-frozen-doc mismatch (NOT a drift-pause; user-confirmed,
  mirroring the U4/U5 documented-deviation convention):** the unit brief
  (`m26-u06.md`) pins a `priority` key + a `Todo_SortPriority` test, but
  frozen Part 2 §2.8 correction **C-3** removed that key — `TodoItem` has **no**
  `Priority` property (confirmed in `Projects/TodoItem.cs`), and the locked
  todos allowlist is exactly `created`/`modified`/`title`/`due`/`status`. Per
  the user's call, the `priority` key is **not** implemented and there is no
  `Todo_SortPriority` test; the frozen C-3 allowlist wins.
- **Kanban column-order non-change (named non-decision):** the card order
  **within** a column is content the user sets — untouched by this unit; only
  the 4 *feed* seams' row ordering changed. The existing filters
  (assignee/project/blocked) are frozen and unchanged.
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0 errors);
  `Kumunita.Core.Tests` suite 1277 total, 0 failed (12 new + existing; U5 was
  1265).
- **Handoff to U7:** additive `SortSpec? sort = null` on the 3 misc-list
  seams (announcements, documents, inventory) + their tests. See `m26-u07.md`.

## U7 — Core misc lists

- **Landed:** `SortSpec? sort = null` added to the 3 misc-list seams
  (`AnnouncementService.ListVisiblePagedAsync` — allowlist `created`/
  `modified`/`title`; `DocumentService.ListAsync` — + `size` → `SizeBytes`
  (non-null `long`); `InventoryService.ListItemsAsync` — + `name` → `Name`
  (non-null)) + the 2 real interface signatures
  (`IAnnouncementService.ListVisiblePagedAsync`,
  `IInventoryService.ListItemsAsync` — `DocumentService` has **no**
  interface; `DocumentController` injects the concrete class directly, so
  deliverable 2 is 2 files, not 3). The shared ordering lives in a new
  `src/Kumunita.Core/Query/MiscSortSupport.OrderByMiscSort<T,TCreated,
  TModified,TSize>(…)` static helper — one generic method for all 3
  surfaces (the U6 selector technique; each call site passes its own
  `created`/`modified`/`title`/`size-or-name`/`id` selectors + the
  surface's closed `allowedKeys` set, which guards each branch so an
  out-of-allowlist key (e.g. "size" on an inventory call) falls to the
  default branch). `null` keeps each pinned `OrderByDescending(Created)`
  **byte-for-byte** (C-SORT·2); every non-null path applies
  `Queryable.ThenBy(…, x => x.Id)` (C-SORT·5; the fully-qualified form —
  U4/U5/U6 carry-forward (1)). `CanSeeAsync` / `HasMore` / `Skip`/`Take` /
  document access control (ADR 0122) / the inventory owner-kind/component
  filters untouched (C-SORT·4). Trailing-`sort` param convention (U5/U6)
  followed — announcements and inventory already have a positional
  `CancellationToken ct`, so `sort` is the trailing param; `ListAsync` has
  no `ct` at all, so `sort` is appended directly after `page`.
- **Tests:** `tests/Kumunita.Core.Tests/MiscList/MiscListSortTests.cs` —
  all 15 pinned tests pass: `Announcement_SortSpecNull_CurrentOrder`,
  `Announcement_SortModifiedDesc_NullsFirst`, `Announcement_SortTitle_
  Ordinal`, `Announcement_InvalidKey_DefaultOrder`,
  `Announcement_StableTieBreakBy_Id`, `Document_SortSpecNull_CurrentOrder`,
  `Document_SortSize`, `Document_SortModifiedAsc_NullsLast`,
  `Document_InvalidKey_DefaultOrder`, `Document_StableTieBreakBy_Id`,
  `Inventory_SortSpecNull_CurrentOrder`, `Inventory_SortName_Ordinal`,
  `Inventory_SortModifiedDesc_NullsFirst`,
  `Inventory_InvalidKey_DefaultOrder`, `Inventory_StableTieBreakBy_Id`.
- **documents-`size` + inventory-`name` pins:** `size` →
  `Document.SizeBytes` (`long`, non-null) — ordered directly, no
  sentinel/drift (`Document_SortSize` pins 10 < 100 < 1000). `name` →
  `InventoryItem.Name` (non-null `string`) — `OrdinalIgnoreCase`
  (`Inventory_SortName_Ordinal` pins Alpha < Bolt < Ladder, case-
  insensitive). Both confirmed non-null in the model before implementation.
- **`modified` null-ordering — MARTEN DRIFT (U7, the C-5 carry-forward, as
  expected):** the `modified` (nullable date) key orders on the **raw
  nullable column** — the `?? MinValue` sentinel is rejected by Marten
  9.31.2's Linq parser (`BadLinqExpressionException`); Postgres supplies its
  default null-ordering (nulls-**last** in asc, nulls-**first** in desc).
  `Announcement_SortModifiedDesc_NullsFirst` and
  `Inventory_SortModifiedDesc_NullsFirst` pin nulls-first desc;
  `Document_SortModifiedAsc_NullsLast` pins nulls-last asc. **Part 2
  (§2.2/§2.3) is unchanged** — recorded here and in the `MiscSortSupport`
  doc-comment.
- **Frozen-filter non-change (C-SORT·4, named non-decision):** document
  access control (ADR 0122 — the `CanSeeAsync` / `HiddenCount` /
  `Total`/`HasMore` shape) and the inventory `ownerKind`/`componentId`
  filters are byte-for-byte untouched; the sort replaces only the `OrderBy`
  line.
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0
  errors); `Kumunita.Core.Tests` suite 1292 total, 0 failed (15 new +
  existing; U6 was 1277).
- **Handoff to U8:** additive `SortSpec? sort = null` on the 4 tag/people
  seams + their tests. See `m26-u08.md`.

## U8 — Core tags + people

- **Landed:** `SortSpec? sort = null` added to the 4 tag/people seams —
  `TagService.ListPostsByTagPagedAsync` (row 14 — `created`/`modified`/
  `title`), `TagService.ListPagesByTagPagedAsync` (row 15 — `created`/
  `modified`/`title`), `ProfileFindService.FindPeopleByTagAsync` (row 16 —
  `name` → `DisplayName` only, correction C-2) and
  `ProfileFindService.FindPeopleByBioAsync` (row 17 — `name` only) + the 2
  real interface signatures (`ITagService`, `IProfileFindService`). The
  shared ordering lives in a new
  `src/Kumunita.Core/Query/TagPeopleSortSupport.OrderByTagFeedSort<T>(…)`
  (the 2 tag feeds' `created`/`modified`/`title` keys) +
  `OrderByPeopleSort<T>(…)` (the 2 people feeds' `name` key) static
  helpers (the U7 `MiscSortSupport` shape). **In-memory, not Marten** —
  these 4 seams order in-memory candidate lists (LINQ-to-objects), so the
  U4–U7 Marten drifts (the CS0411 `Queryable.ThenBy` disambiguation, the
  `?? MinValue` sentinel rejection) do **not** apply: the `?? MinValue`
  sentinel on the nullable `modified` key works **as pinned** (nulls sort
  first in asc / last in desc), and `ThenBy` is the plain `Enumerable`
  form. `null` keeps each pinned order **byte-for-byte** (C-SORT·2);
  every non-null path applies the `.ThenBy(Id)` / `.ThenBy(SubjectId)`
  tie-breaker (C-SORT·5 — people's identity member is `SubjectId`,
  `Profile` has no `Id`). Trailing-`sort` param convention (U5/U6/U7)
  followed — the tag seams already have a positional
  `CancellationToken ct`, so `sort` is the trailing param; the 2 people
  seams have no `ct` at all, so `sort` is appended after `page`. The
  frozen tag-slug resolve + the `CanSeeAsync` gate / `HasMore` /
  `Skip`/`Take` are byte-for-byte untouched (C-SORT·4).
- **Tests:** `tests/Kumunita.Core.Tests/TagPeople/TagPeopleSortTests.cs` —
  all 18 pinned tests pass (5 tag-posts + 5 tag-pages + 4 people-by-tag +
  4 people-by-bio): `TagPosts_SortSpecNull_CurrentOrder`,
  `TagPosts_SortModifiedDesc`, `TagPosts_SortTitle_Ordinal`,
  `TagPosts_InvalidKey_DefaultOrder`, `TagPosts_StableTieBreakBy_Id`,
  `TagPages_SortSpecNull_CurrentOrder`, `TagPages_SortModifiedDesc`,
  `TagPages_SortTitle_Ordinal`, `TagPages_InvalidKey_DefaultOrder`,
  `TagPages_StableTieBreakBy_Id`, `PeopleByTag_SortSpecNull_CurrentOrder`,
  `PeopleByTag_SortName_Ordinal`,
  `PeopleByTag_InvalidKey_DefaultOrder`,
  `PeopleByTag_StableTieBreakBy_SubjectId`,
  `PeopleByBio_SortSpecNull_CurrentOrder`,
  `PeopleByBio_SortName_Ordinal`,
  `PeopleByBio_InvalidKey_DefaultOrder`,
  `PeopleByBio_StableTieBreakBy_SubjectId`.
- **tag-pages-asc-default + people-name-Ordinal pins:** row 14/15 default
  is `created`, **asc** (the pinned `.OrderBy(p => p.Created)` —
  **ascending**, correction C-1 — the brief's prose said "posts desc";
  the frozen doc + the actual code at `TagService.cs:531`/`:578` win, and
  I preserved asc exactly — no drift pause needed since the brief's own
  §2.3/§2.5 and the frozen §2.2 row 14/15 all say asc). People's `name`
  key → `Profile.DisplayName` (`OrdinalIgnoreCase`); the C-2 correction
  (no `created` key) is honored — the `PeopleByTag_InvalidKey_DefaultOrder`
  / `PeopleByBio_InvalidKey_DefaultOrder` tests pin that `?sort=created`
  falls back to the `name` asc default (not an error).
- **`modified` sentinel-as-pinned:** the `TagPosts_SortModifiedDesc` /
  `TagPages_SortModifiedDesc` tests pin the **frozen §2.2 sentinel
  behavior** (null `Modified` → `?? MinValue` → nulls sort **last** in
  desc / **first** in asc) — the **opposite** of the Postgres
  null-placement U7 pinned (the in-memory difference). Part 2
  (§2.2/§2.3) is unchanged — no drift.
- **Frozen-gate non-change (C-SORT·4, named non-decision):** the tag-slug
  resolve (`session.Query<Tag>().Where(t => t.Slug == …)`) + the people
  `CanSeeAsync` gate + the `HasMore` / `Skip`/`Take` paging are
  byte-for-byte untouched; the sort replaces only the `OrderBy` line (or
  inserts the `OrderBy` between the gate and `Paged` for the people
  seams).
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0
  errors); `Kumunita.Core.Tests` suite **1310** total, **0 failed**
  (18 new + existing; U7 was 1292). `Kumunita.Web.Tests` suite **849**
  total, **0 failed** (the `TagService` / `ProfileFindService` NSub
  substitute call sites in `M7NewlyPagedTests` /
  `TagControllerTests` / `M23FindPeopleTests` updated for the new
  trailing `sort` param).
- **Handoff to U9:** additive `SortSpec? sort = null` on the search seam
  + the `Search*_*` tests. See `m26-u09.md`.

## U9 — Core search

- **Landed:** `SortSpec? sort = null` added to the search seam —
  `SearchService.SearchSurfaceAsync` (+ `ISearchService`) — applied
  **in-memory over the `visible` hit list**, before the `Skip/Take`, only
  when non-null (design §2.3 row 18 — the U8 in-memory pattern, **not**
  the U4–U7 Marten drifts: plain `Enumerable.ThenBy`, the `?? ""`
  sentinel on the nullable `SearchHit.Title` works as pinned). The 10
  per-surface `OrderByDescending(...Created)` candidate queries are
  untouched (they produce the pinned default order, which `null` now
  keeps byte-for-byte — C-SORT·2); the closed allowlist is
  `created`/`title` only (M8 frozen — **relevance is not a sort key**),
  `.ThenBy(h => h.Id)` tie-breaker on every non-null path (C-SORT·5);
  an out-of-allowlist key falls back to the pinned created/**desc**
  default regardless of the spec's own direction (the U8
  `defaultDir` shape — my first draft applied `sort.Descending` to the
  fallback branch and `Search_InvalidKey_DefaultOrder` only passed by
  luck). Trailing-`sort` param (after the existing `ct`) — Web call
  sites + the Web NSub substitute pins compile unchanged. `HasMore` /
  `MaxPerSurface` / the group scope + the `CanSeeAsync` gate are
  byte-for-byte untouched (C-SORT·4).
- **Tests:** `tests/Kumunita.Core.Tests/Search/SearchSortTests.cs`
  (namespace `Kumunita.Core.Tests.SearchSort` — `…Tests.Search` would
  have shadowed the core `Kumunita.Core.Search` namespace in the
  sibling `SearchServiceTests`) — all 6 pinned tests pass over the
  announcements surface (the simplest visible set — the flat predicate,
  no `CanSeeAsync`, no groups): `Search_SortSpecNull_CurrentOrderDesc`,
  `Search_SortCreatedAsc`, `Search_SortTitle_Ordinal`,
  `Search_InvalidKey_DefaultOrder`, `Search_StableTieBreakBy_Id`,
  `Search_RelevanceNotASortKey`.
- **Relevance-untouched pin:** the `Search_RelevanceNotASortKey` test
  pins that a `relevance` request invents **no** order — it falls back
  to the surface default (created, desc, identical to `sort = null`);
  the allowlist offers exactly `created` + `title`.
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0
  errors); `Kumunita.Core.Tests` suite **1316** total, **0 failed**
  (6 new + existing; U8 was 1310).
- **Handoff to U10:** the Web `_Sort` foundation (`SortViewModel` +
  `_Sort` partial + the pager-carry rule) + the community-feed reference
  surface + the Web tests. See `m26-u10.md`.

## U10 — Web _Sort foundation

- **Landed (Web unit):** `SortViewModel`
  (`src/Kumunita.Web/Models/SortViewModel.cs` — the §2.4 shape:
  `BaseUrl`/`CurrentKey`/`CurrentDir`/`Options`/`CarriedParams` + a
  `ForRoute` builder) + the one shared `_Sort` partial
  (`src/Kumunita.Web/Views/Shared/_Sort.cshtml` — a **link set** over the
  surface's allowed keys × asc/desc, the `_Pager` `PagerLink` shape: carried
  filter pairs then the toggled `sort=`/`dir=`; labels via
  `<kw-l key="sort.{key}">` placeholder keys — the set U16 closes). `null`
  model renders nothing (the no-sort pin).
- **Pager-carry rule (C-SORT·8):** when the request carried `?sort=` (a
  non-blank key), the `sort`/`dir` pairs join the existing `Pager`'s
  `FilterParams` (`PostsController.BuildFeedPager`) so prev/next preserve the
  sort; an unsorted read keeps `FilterParams` empty (byte-identical,
  C-SORT·2). `Pager`/`HasMore` behavior otherwise untouched.
- **Reference surface:** the community post feed
  (`PostsController.Index`) — the `?sort=`/`?dir=` params are read Web-only
  (C-SORT·3), parsed via `SortKeys.Parse` against the closed allowlist
  `{created, modified, title}` (created desc default), threaded into
  `PostService.ListFeedAsync`'s `SortSpec`, and `FeedViewModel` gained a
  `SortViewModel? Sort` field; `Views/Posts/Index.cshtml` renders
  `<partial name="_Sort" model="Model.Sort" />` above the `_Pager`.
- **Tests:** `tests/Kumunita.Web.Tests/SortControlTests.cs` — the 3 pinned
  `CommunityFeed_*` tests all pass: `CommunityFeed_SortControl_Renders_AllowedKeys`
  (F9), `CommunityFeed_Pager_Carries_Sort_And_Dir` (C-SORT·8/F8),
  `CommunityFeed_SortParam_DefaultsWhenAbsent` (F1).
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0 errors);
  `Kumunita.Web.Tests` suite **852** total, **0 failed** (3 new + existing).
- **U11–U15 reuse this, do not fork:** the `SortViewModel`/`_Sort` are the
  **only** control — each later surface just passes **its own** allowed-key
  `Options` (and its own `ForRoute` base) + adds its own `sort`/`dir` pairs to
  its existing `Pager`'s `FilterParams`. Do not create a per-surface partial.
  See `m26-u11.md`.

## U11 — Web posts + events

- **Landed (Web unit):** the U10 `_Sort` pattern applied to the **remaining
  post feeds** (all-sections `PostsController.AllSections` + group posts
  `GroupsController.Detail`) + the **events** surfaces (upcoming + past
  `EventController.Index` + group events `GroupsController.Detail`) — 5
  surfaces wired; **reuse, not fork** (the one shared `SortViewModel` +
  `_Sort` partial, C-SORT·1). Each surface passes **its own** closed
  allowlist (`Options`) + its own pager-carry `sort`/`dir` pair (C-SORT·8) —
  the group detail page's **two** sections (posts + events) each parse the
  same `?sort=`/`?dir=` request params against **their own** allowlist +
  default, so a surface-unknown key (e.g. `title` on the group events section)
  falls back to that surface's default order.
- **Per-surface `Options` (U2 §2.2):** the all-sections + group posts
  surfaces share the post-feed allowlist (row 1–3: `created` desc, `modified`
  desc, `title` asc); the upcoming/past event feed surfaces share the event-
  feed allowlist (row 4–5: `start` — asc on upcoming, desc on past — `created`
  desc, `title` asc); the group events section offers **only**
  `start` asc + `created` desc (row 6 — F9, no `title` dead option on the
  section's link set).
- **Pager-carry (C-SORT·8):** each surface's `Pager` `FilterParams` gains **its
  own** `sort`/`dir` pairs (only when the request carried a non-blank
  `?sort=`); an unsorted read keeps `FilterParams` byte-identical to pre-M26
  (C-SORT·2). The `null`/no-sort pin is unchanged (a `null` `Sort` renders
  nothing).
- **Tests:** `tests/Kumunita.Web.Tests/PostEventSortWebTests.cs` — all 15
  pinned names pass: `AllFeed_*` (3), `GroupPostFeed_*` (3),
  `EventUpcoming_*` (3), `EventPast_*` (3), `EventGroup_*` (3). The
  C-SORT·1 surface-unknown-key fallback (e.g. `?sort=title` on the group
  events section falls back to its own default `start`-asc, not to a
  `title` order the section doesn't offer) is carried by the same
  `SortKeys.Parse` call the U10 reference pins
  (`CommunityFeed_SortParam_DefaultsWhenAbsent`). The existing
  `CommunityFeed_*` (U10) + `GroupsDetailViewModelTests` shape pin (renamed
  to `…_TwentyFour_Projected_Fields`, +2 for the new `SortPosts`/`SortEvents`)
  still pass.
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0 errors);
  `Kumunita.Web.Tests` suite **867** total, **0 failed** (15 new + existing;
  U10 was 852).

## U12 — Web projects

- **Landed (Web unit):** the U10 `_Sort` pattern applied to the **projects**
  surfaces — the todos list (`TodosIndex` → `TodoIndexViewModel`), the boards
  list (`BoardsIndex` → `BoardIndexViewModel`), and the projects+goals landing
  (`ProjectsIndex` → `ProjectsIndexViewModel`, **two** paged sections: goals +
  standalone projects) — **reuse, not fork** (the one shared `SortViewModel` +
  `_Sort` partial, C-SORT·1). Each surface passes **its own** closed
  allowlist (`Options`) + **its own** pager-carry `sort`/`dir` pair
  (C-SORT·8, U11's `SortViewModel.SortFilterParams` helper reused — not
  re-derived).
- **Per-surface `Options` (U2 §2.2):** the todos surface (row 7) offers
  `created`/`modified`/`title` + its own `due`/`status` keys (the U6 locked
  set — see the drift pause below); the boards (row 8), projects (row 9), and
  goals (row 10) surfaces each share the identical `created`/`modified`/`title`
  set. `ProjectsIndex` **dual-pager:** the goals + projects sections each pass
  **their own** `SortViewModel` (C-SORT·1) so a surface-unknown key falls back
  to that section's own default (the U11 dual-section rule); **both** sections'
  `BaseUrl` is the **same** `/projects`, so the same `sort`/`dir` query keys are
  safe on each section's own pager without a collision (the brief's
  `psort`/`pdir` alternative was unnecessary — the split is *not* by `BaseUrl`,
  both sections read the request's single `?sort=`/`?dir=` pair).
- **Tests:** `tests/Kumunita.Web.Tests/ProjectSortWebTests.cs` — all 12 pinned
  names pass: `Todo_*` (3), `Board_*` (3), `Project_*` (3), `Goal_*` (3) — each
  `SortControl_Renders_AllowedKeys` / `Pager_Carries_Sort_And_Dir` /
  `SortParam_DefaultsWhenAbsent`. The frozen M7 filters (assignee/project/
  blocked) are still carried alongside the sort/dir pairs (C-SORT·4, the
  `Todo_Pager_Carries_Sort_And_Dir` pin). The existing `ProjectsControllerTests`
  controller-level pins (`…_BlockedOnlyTrue_PassesFilterToService` /
  `ProjectsIndex_GoalsPlusStandaloneProjects_Render`) still pass unchanged —
  they invoke the actions **without** sort params → `feedSort = null` → the
  trailing seam arg is `null`, so their 8-arg NSubstitute setups still match.
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0 errors);
  `Kumunita.Web.Tests` suite **879** total, **0 failed** (12 new + existing;
  U11 was 867).
- **Handoff to U13:** apply the U10 `_Sort` pattern to the announcements +
  documents + inventory surfaces + their Web tests. See `m26-u13.md`.

## U12 — Drift pause

- **`priority` key (the brief's `Todo_SortControl_Offers_Due_Priority_Status`
  name) — NOT implemented; logged as a user-call, mirroring U6's drift pause,
  not a silent change.** The U12 unit brief (`m26-u12.md`) lists the todos
  surface as offering `due`/`priority`/`status` keys + a
  `Todo_SortControl_Offers_Due_Priority_Status` test name. But **frozen Part 2
  (C-3) removed `priority`**: `TodoItem` has **no** `Priority` property
  (confirmed in `Projects/TodoItem.cs`), and the U6-locked todos allowlist is
  exactly `created`/`modified`/`title`/`due`/`status`. Per the user's call
  (mirroring the U6 decision), the `priority` key is **not** implemented and
  there is no `Todo_SortControl_Offers_Due_Priority_Status` test — the frozen
  C-3 allowlist wins. `Todo_SortControl_Renders_AllowedKeys` here pins the
  actual locked set (the 5-key set, no `priority`);
  `Todo_SortParam_DefaultsWhenAbsent` additionally pins that a `?sort=priority`
  request **falls back**
  to the surface's default `created` key (C-SORT·1 / F4), so the brief's intent
  (`priority` is offered **only** if the Core switch resolves it — it doesn't)
  is still honored.
- **Kanban column-order non-change (named non-decision, carried from U6):**
  the card order **within** a kanban board column is content the user sets —
  untouched by this unit. Only the 4 *feed* surfaces' (todos / boards /
  projects / goals) row ordering is now user-selectable via `?sort=`/`?dir=`.
  The existing M7 filters (assignee / project / blocked) are frozen and still
  carried on the pager (C-SORT·4).

## U13 — Web announcements + documents + inventory

- **Landed (Web unit):** the U10 `_Sort` pattern applied to the **announcements**
  (`/announcements`, `AnnouncementController.Index`), **documents**
  (`/documents`, `DocumentController.Index`), and **inventory** (`/inventory`,
  `InventoryController.List`) surfaces — 3 more surfaces wired, all reusing the
  one shared `SortViewModel` + `_Sort` partial verbatim (C-SORT·1, no fork).
  Each surface passes **its own** closed allowlist `Options` + **its own**
  pager-carry `sort`/`dir` pair via U11's `SortViewModel.SortFilterParams`
  helper (C-SORT·8, not re-derived).
- **Per-surface `Options` pin (U2 §2.2):** announcements (row 11) offers
  `created`/`modified`/`title` only (no extra key, F9); documents (row 12)
  offers `created`/`modified`/`title` **plus** its own `size` key (→ the
  non-null `SizeBytes` long); inventory (row 13) offers `created`/`modified`
  **plus** its own `name` key (→ the non-null `Name` string) — **no** `title`
  on inventory (that is announcements/documents' own key). Each extra key is
  pinned **only** on its own surface's `Options` (F9), cross-checked by the
  `Document_SortControl_Offers_Size` / `Inventory_SortControl_Offers_Name`
  pins.
- **Pager-carry scope (C-SORT·8) — surface-specific, not a drift:** the
  announcements + inventory surfaces both carry a `PagedViewModel` pager, so
  each gains **its own** `sort`/`dir` `FilterParams` pair only when the request
  carried a non-blank `?sort=` (C-SORT·2 — an unsorted read stays
  byte-identical). The **documents** surface renders a plain "Older" next-link
  (no `PagedViewModel`, no `Pager` property on `DocumentIndexViewModel`) — so
  C-SORT·8's pager-carry is a non-applicable surface there; the sort control
  (`Sort` field + `_Sort`) is still wired identically. The
  `Document_Pager_Carries_Sort_And_Dir` pin models the pairs +
  `PagedViewModel.ForRoute` shape as the brief names it (data-shape, the
  U10–U12 harness precedent).
- **Tests:** `tests/Kumunita.Web.Tests/MiscListSortWebTests.cs` — all 11
  pinned names pass: `Announcement_SortControl_Renders_AllowedKeys`,
  `Announcement_Pager_Carries_Sort_And_Dir`,
  `Announcement_SortParam_DefaultsWhenAbsent`; `Document_SortControl_Renders_AllowedKeys`,
  `Document_Pager_Carries_Sort_And_Dir`, `Document_SortParam_DefaultsWhenAbsent`,
  `Document_SortControl_Offers_Size`; `Inventory_SortControl_Renders_AllowedKeys`,
  `Inventory_Pager_Carries_Sort_And_Dir`,
  `Inventory_SortParam_DefaultsWhenAbsent`, `Inventory_SortControl_Offers_Name`.
  The frozen filters are still carried alongside the sort/dir pairs (C-SORT·4):
  announcements' has no filter form (D9 — `?page=N` + sort/dir only); the
  inventory pager still carries the frozen `ownerKind`/`componentId` filters
  (the `Inventory_Pager_Carries_Sort_And_Dir` pin). The existing
  `AnnouncementControllerTests` / `DocumentControllerTests` /
  `InventoryControllerTests` controller-level pins still pass unchanged — they
  invoke the actions **without** sort params → `feedSort = null` → the seam's
  pinned `OrderByDescending(Created)` stays byte-for-byte (C-SORT·2).
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0 errors);
  `Kumunita.Web.Tests` suite **890** total, **0 failed** (11 new + existing;
  U12 was 879).
- **No drift pause this unit:** the U13 brief's per-surface key sets and the
  extra-key names (`size`, `name`) match the U7-locked Core allowlists in
  `MiscSortSupport.OrderByMiscSort` exactly — no brief-vs-locked-Part-2
  mismatch, unlike U12's `priority` case.
- **Handoff to U14:** apply the U10 `_Sort` pattern to the tags +
  people-find surfaces + their Web tests. See `m26-u14.md`.

## U14 — Web tags + people

- **Landed (Web unit):** the U10 `_Sort` pattern applied to the **tags**
  surfaces (by-tag posts `row 14` + by-tag pages `row 15` — the
  `TagController.ByTag` **dual-pager** surface, mirroring U12's
  `ProjectsIndex` handling) and the **people-find** surfaces (by-tag
  `row 16` + by-bio `row 17` — the `FindPeopleController` `ByTag` /
  `Index`-bio actions) — 4 surfaces wired, all reusing the one shared
  `SortViewModel` + `_Sort` partial verbatim (C-SORT·1, no fork). Each
  section passes **its own** closed allowlist `Options` + **its own**
  pager-carry `sort`/`dir` pair via U11's `SortViewModel.SortFilterParams`
  helper (C-SORT·8, not re-derived).
- **Per-surface `Options` pin (U2 §2.2):** the tag feeds (rows 14/15)
  each offer `created`/`modified`/`title` — with the **`created`-asc**
  default (the pinned `.OrderBy(p => p.Created)` current order, C-SORT·2 /
  the U8 correction C-1 — the `TagPosts_SortControl_DefaultDir_Asc` pin),
  no dead options (F9). The people feeds (rows 16/17) each offer
  **only** `name` → `DisplayName` (`OrdinalIgnoreCase`, asc — correction
  C-2 — the `People_SortControl_Offers_Name` pin; no `created` key, no
  `title`). The `TagByTagViewModel` **dual-pager split:** both sections'
  `BaseUrl` is the same `/tags/{slug}`, so the same `sort`/`dir` query
  keys ride each section's own pager without a collision (the U12
  precedent) — both read the request's single `?sort=`/`?dir=` pair.
- **Pager-carry scope (C-SORT·8):** the tag sections carry a
  `PagedViewModel` pager — each gains its own `sort`/`dir` pair only when
  the request carried a non-blank `?sort=` (C-SORT·2 — unsorted reads
  byte-identical). The people-find surfaces render a plain "Older"
  next-link (no `PagedViewModel`, the U13 documents precedent) — the
  carry is the `Sort` control's `CarriedParams` + the view's next-link
  (the `…_Pager_Carries_Sort_And_Dir` pins model the data-shape, the
  U13 precedent).
- **Tests:** `tests/Kumunita.Web.Tests/TagPeopleSortWebTests.cs` — all 14
  pinned names pass: `TagPosts_SortControl_Renders_AllowedKeys`,
  `TagPosts_Pager_Carries_Sort_And_Dir`, `TagPosts_SortParam_DefaultsWhenAbsent`;
  `TagPages_SortControl_Renders_AllowedKeys`,
  `TagPages_Pager_Carries_Sort_And_Dir`,
  `TagPages_SortParam_DefaultsWhenAbsent`,
  `TagPages_SortControl_DefaultDir_Asc`; `PeopleByTag_SortControl_Renders_AllowedKeys`,
  `PeopleByTag_Pager_Carries_Sort_And_Dir`,
  `PeopleByTag_SortParam_DefaultsWhenAbsent`;
  `PeopleByBio_SortControl_Renders_AllowedKeys`,
  `PeopleByBio_Pager_Carries_Sort_And_Dir`,
  `PeopleByBio_SortParam_DefaultsWhenAbsent`; `People_SortControl_Offers_Name`.
  The frozen gates are untouched (C-SORT·4): the tag-slug resolve + the
  people `CanSeeAsync` gate / `HasMore` / `Skip`/`Take` are
  byte-for-byte unchanged; the existing `TagControllerTests` /
  `M7NewlyPagedTests` / `M23FindPeopleTests` controller-level pins still
  pass unchanged — they invoke the actions **without** sort params →
  `feedSort = null` → the seam's pinned order stays byte-for-byte
  (C-SORT·2).
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green (0
  errors); `Kumunita.Web.Tests` suite **904** total, **0 failed**
  (14 new + existing; U13 was 890).
- **No drift pause this unit:** the U14 brief's per-surface key sets
  (`created`/`modified`/`title` for the tag feeds; `name`-only for the
  people feeds) match the U8-locked Core allowlists in
  `TagPeopleSortSupport` (`OrderByTagFeedSort` / `OrderByPeopleSort`)
  exactly, and the `created`-asc default matches the Core call sites'
  own `defaultDir: false` — no brief-vs-locked-Part-2 mismatch.
- **Handoff to U15:** apply the U10 `_Sort` pattern to the search
  surface + its Web tests. See `m26-u15.md`.

## U15 — Web search

- **Landed (Web unit):** the U10 `_Sort` pattern applied to the
  **search** surface — the paged single-surface read
  (`/search?surface=<one>`, `SearchController.Index` →
  `SearchIndexViewModel` → `Views/Search/Index.cshtml`) — reusing the
  one shared `SortViewModel` + `_Sort` partial verbatim (C-SORT·1, no
  fork). The `?sort=`/`?dir=` params are read Web-only (C-SORT·3),
  parsed via the same `SortKeys.Parse` call against the closed
  `created`/`title` allowlist (created desc default — the U2 §2.2
  row 18 set, the U9-locked Core switch's exact key set), and the
  resolved `SortSpec?` threads into `ISearchService.SearchSurfaceAsync`
  (the U9 seam — a `null` pair keeps the per-surface
  `OrderByDescending(Created)` byte-for-byte, C-SORT·2). The `Sort`
  field (nullable `SortViewModel`) is **only** set on the
  single-surface shape — the `all` shape has no pager (the
  search-box answer, D1) and the seam's `SortSpec` applies per
  surface, so a cross-surface control would be a dead option (F9);
  the view renders the partial above the `_Pager` under the existing
  `!Model.IsAll` guard (the U13/U14 `Model.Sort is not null`
  convention).
- **Options pin + pager split:** `Options` = exactly `created`
  (desc) + `title` (asc) — **no** relevance key (M8 frozen — the
  `Search_SortControl_Excludes_Relevance` pin), **no** `name` key
  (the Core switch does not resolve it — see the drift pause below).
  The pager (single-surface shape only) carries **its own**
  `sort`/`dir` pair via U11's `SortViewModel.SortFilterParams`
  helper (reused, not re-derived) alongside the frozen
  `q`/`surface`/`scope` pairs (C-SORT·4); an unsorted read keeps the
  pre-M26 pairs byte-identical (C-SORT·2).
- **Tests:** `tests/Kumunita.Web.Tests/SearchSortWebTests.cs` — all
  4 pinned names pass: `Search_SortControl_Renders_AllowedKeys`,
  `Search_Pager_Carries_Sort_And_Dir`,
  `Search_SortParam_DefaultsWhenAbsent`,
  `Search_SortControl_Excludes_Relevance`. The frozen scope-degrade
  gate (D3/F6) + the `CanSeeAsync`/audit-row gates + `HasMore` /
  `MaxPerSurface` are byte-for-byte unchanged (C-SORT·4); the
  existing `SearchControllerTests` controller-level pins (which
  invoke `Index` without sort params → `feedSort = null` → the seam's
  pinned order stays byte-for-byte) still pass unchanged.
- **Exit verified:** `dotnet build Kumunita.slnx -c Debug` green
  (0 errors); `Kumunita.Web.Tests` suite **908** total, **0 failed**
  (4 new + existing; U14 was 904).
- **One drift pause this unit** — see `## U15 — Drift pause` below
  (the brief's `title`/`name` wording vs the U9-locked Core
  allowlist).

## U15 — Drift pause

- **`name` key — NOT offered; logged as a brief-vs-locked-Core mismatch,
  mirroring U12's `priority` case, not a silent change.** The U15 brief
  (`m26-u15.md`) is itself consistent with the locked Core: its
  *Understanding* names the `Options` as "the date/title keys only" and
  its *Rules* say "the search `Options` are the date/title keys only".
  But the brief's *Entry reads* cross-reference (U2 §2.2, register
  `plan-m26-sorting.md`) and the U9 Core deliverable note both carry
  the alternative wording "`created` + the surface's `title`/`name`
  key". The **locked Core** (U9 — frozen Part 2) resolves **exactly
  two keys** in `SearchService.SearchSurfaceAsync`: `created` and
  `title` (→ `SearchHit.Title ?? ""`); the `people` surface's hit
  stores its display name **in** `Title` (a single `SearchHit` shape
  across all ten surfaces — `name` is the people-*find* surfaces' own
  key, U8's `TagPeopleSortSupport`, not this seam's). Per the
  drift-pause guard, the control offers **only** the keys the Core
  `OrderBy…` support actually resolves — `created` + `title` — and a
  `?sort=name` request falls back to the surface default (`created`,
  desc, the C-SORT·1/F4 rule, pinned by
  `Search_SortParam_DefaultsWhenAbsent`). No Core change was made —
  adding a `name` branch to the frozen Part-2 switch would be a
  silent Part-2 edit, out of U15's scope. The U2 §2.2 register
  cross-reference (the "`title`/`name`" wording) is the stale piece;
  it does not change the locked behavior.
- **Relevance non-change (named non-decision, M8 frozen):** the
  `Options` never include a relevance key and a `?sort=relevance`
  request invents **no** order — it falls back to the pinned
  created-desc default (the U9 `Search_RelevanceNotASortKey`
  Core pin's Web twin, `Search_SortControl_Excludes_Relevance`).
