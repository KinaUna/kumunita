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
