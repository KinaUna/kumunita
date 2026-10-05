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
