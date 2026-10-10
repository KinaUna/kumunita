# ADR 0160 — Nested help guides: each tabbed surface's sub-pages become child guides under their top-level guide (the UG guide tree gains a second level)

Status: Accepted
Date: 2026-10-10
Amends: **0057** (the resident-guide surface — this ADR extends the flat
`help/` guide tree to a two-level tree: top-level guides stay direct children
of the canonical `help` page exactly as ADR 0057 D1 pinned them, and a new
closed set of *nested* guides hangs one level deeper, each under its
top-level surface guide). **Additive** on **0039** (the `Page` doc + the
`(ParentId, Slug)` unique index — a nested guide is a normal `Page` row whose
`ParentId` is a guide's id rather than `help`'s; the index and the ancestor
chain are reused verbatim), **0040** (`PageKind.System` + the `system/`
namespace — a nested guide inherits the system-page standing matrix, and the
namespace guard still holds: a guide under a guide under `help` under
`system`, a resident's `User` page never under the guide tree), **0042 D1**
(the `en` code-owned / non-`en` community-owned split — a nested guide's `en`
body is code-owned by the `GuidePages()` floor; its `de` / `fr` / `da`
baselines ship hand-curated, seeded once and then community-owned — the same
shape as the top-level guides' baselines, ADR 0057 D2, never machine-translated
per **0005 C**),
**0058** (the reset-to-seeded lane — a nested guide resets exactly like a
top-level one, because it is found by `AllSeededSlugs` / `FindSeededPageText`
over the whole tree), and **0128** (the `/admin/help` status probe — a nested
guide is listed and reset identically). No amendment to the storage pins of
any of those — this ADR is additive on their decisions exactly as written.

## Context

ADR 0057 gave the platform a resident-facing guide for every *top-level*
surface: `posts`, `groups`, `events`, `projects`, `announcements`,
`directory`, `people`, `inventory`, `bookmarks`, `documents`, `pages`, `tags`,
plus the account-and-role guides. Each is a flat `Page` doc, a direct child of
the canonical `help` page (ADR 0057 D1). That worked when a surface was a
single thing.

Several surfaces now **tab their own sub-pages**: Projects has *Projects ·
To-dos · Boards*; Events has *Events · Calendar*; Settings has *Language ·
Time zone · Date format · Items per page · Home page · Quiet hours · Messaging ·
Children · Change password · Delete account*; a group's detail has *Feed ·
Members · Settings*; and the Admin shell has *Overview · Accounts ·
Communities · Platform · Security · Analytics*. A resident who reads the
**Projects** guide and wants to know how a *board* works finds the board
prose buried in the middle of a page about to-dos — or, for a tab that has no
dedicated top-level guide at all (Settings, group Members, Admin Accounts),
finds **nothing**.

The existing top-level guides already *describe* these sub-pages inline
(`projects` covers to-dos, boards, goals; `settings` would cover the tabs;
`groups` covers members and group settings; `admins` covers the admin tabs).
So the *knowledge* is present; the *structure* is flat. The fix is not new
prose so much as **surfacing the prose a resident already has to wade for**,
by giving each tab its own page *under* the surface guide it belongs to — the
exact "a page is either a folder or a leaf, or both ('folder-with-index')"
shape ADR 0039 §3.3 already names, and the same tree ADR 0057 hung the first
level on.

**The fork that determined this ADR:** keep the guides flat and grow the
top-level pages into ever-longer prose **or** introduce a **second level** in
the guide tree. The second level was chosen (user sign-off 2026-10-10): it is
free (a nested guide is a `Page` row the existing seeder, editor, translator,
authorization, and tree-browse all already handle — no new surface, no new
engine, no new route), it keeps each guide short and on-topic (a board is
explained *at* the board, not five scroll-positions into a to-do page), and it
mirrors the app's own information architecture — the resident's mental model
is "Projects → Boards", and the help tree now matches it.

The one genuine cost is the **flat exact-set pin** ADR 0057 D1 laid down
("each guide a direct child of `help`", the `UG_GuideRegistryTests` drift
pin). That pin is a *storage* invariant, not a *display* one, and it was
laid down to keep the guide set **closed and enumerable** — the same property
this ADR preserves, now at two levels. ADR 0160 therefore keeps the pin for
the top level (the top-level guides are still exactly the `help` children not
in the parent map) and extends it to the nested level (each nested guide is
exactly the child of its map-named parent), so the tree remains closed,
enumerable, and drift-pinned — just one level deeper.

## Decision

### D1 — A closed `GuideParentMap` is the single source of the nesting shape

- A new public static `FirstBootSeeder.GuideParentMap()` returns a
  `slug → parentSlug` dictionary. A guide slug **absent** from the map is a
  **top-level** guide (a direct child of the canonical `help` page — the
  ADR 0057 D1 shape, unchanged for all 23 existing top-level guides). A guide
  slug **present** in the map is a **nested** guide: its `ParentId` is the
  page seeded under the map's value (its parent guide), which is itself a
  top-level guide under `help`.
- The map is a **closed set** (the ADR 0057 "a new guide is a new lane, not a
  silent addition" rule applied to nesting): a new nested guide is a new
  entry here *and* a new `GuidePages()` row, in the same lane. The map and
  the `GuidePages()` registry are the **single source** both the seeder and
  the tests read, so they cannot drift on the nesting shape.
- **The map's values are always top-level guide slugs** (never a nested
  guide's slug): the tree is exactly two levels deep under `help`, never
  three. A slug that is itself a value in the map (e.g. `projects`,
  `events`, `settings`, `groups`, `admins`) is therefore top-level even when
  it also has children.

### D2 — The 17 new nested guides (+ the `settings` index)

The closed nested set, each a `Page` doc `Kind = System`, `Audience = null`
(public — a how-to is not audience-gated, ADR 0057 D1), `LanguageCode = en`
(authored-in the source language, ADR 0042 D1), `AuthorId = string.Empty`
(platform content), `IsDraft = false`, `IsDeleted = false`:

| Slug (leaf) | Parent guide | The tab it documents |
|---|---|---|
| `todos` | `projects` | Projects → To-dos |
| `boards` | `projects` | Projects → Boards |
| `calendar` | `events` | Events → Calendar |
| `timezone` | `settings` | Settings → Time zone |
| `date-format` | `settings` | Settings → Date & time format |
| `page-size` | `settings` | Settings → Items per page |
| `home` | `settings` | Settings → Home page |
| `quiet-hours` | `settings` | Settings → Quiet hours |
| `messaging` | `settings` | Settings → Messaging |
| `change-password` | `settings` | Settings → Change password |
| `members` | `groups` | Group → Members |
| `group-settings` | `groups` | Group → Settings |
| `accounts` | `admins` | Admin → Accounts |
| `communities` | `admins` | Admin → Communities |
| `platform` | `admins` | Admin → Platform |
| `security` | `admins` | Admin → Security |
| `analytics` | `admins` | Admin → Analytics |
| `settings` | — (top-level) | Settings index (the new container for the /settings tabs) |

(17 nested + 1 new top-level index guide `settings`, itself a child of
`help`; the `language`, `child-accounts`, and `deleting-an-account` tabs keep
their existing top-level guides, which the `settings` index cross-links rather
than re-nests. Total guide tree: the original 23 top-level guides + `settings`
= 24 top-level, + 17 nested = **41 guides**.)

### D3 — Seeder + backfill resolve the parent from the map

- `SeedUserGuidesAsync` iterates `GuidePages()` in order (top-level entries
  precede their children by construction) and, for each guide, resolves its
  parent: the in-flight id of the guide named by `GuideParentMap[slug]` when
  present, else the `help` page id. The resolved id is stored as the guide's
  `ParentId`. The existing create-if-missing + code-wins-refresh invariant
  (ADR 0057 D1 / ADR 0042 D1) is preserved per guide.
- `BackfillUserGuidesAsync` (the warm-boot path) applies the **same** map
  resolution, so a deployment that predates the nested set backfills the
  nested guides under their correct top-level parent (not directly under
  `help`). A parent that was itself community-edited is still found (the
  map's value is the parent *slug*, resolved by the parent guide's row,
  which exists under `help` by the top-level pin).

### D4 — The `en` floor and the hand-curated `de`/`fr`/`da` baselines both ship

- The 17 nested guides + the `settings` index ship their **`en`** body in
  `GuidePages()` (the code-owned floor, ADR 0042 D1) **and** their **`de` /
  `fr` / `da`** baseline bodies in `DeGuidePages()` / `FrGuidePages()` /
  `DaGuidePages()` (the community-owned floor, ADR 0057 D2 — the exact same
  shape the original 23 top-level guides use). A pristine boot therefore gives
  a German / French / Danish reader a real localized guide for every nested
  sub-page, not an `en`-floor fallback.
- The baselines are **seeded create-if-missing, then community-owned** (the
  ADR 0042 D1 invariant): an in-app edit by a Translator / GlobalAdmin is never
  clobbered by a later deploy. The warm-boot
  `BackfillGuideTranslationsAsync` reaches the nested guides exactly as it
  reaches the top-level ones (it walks the two-level guide subtree, not just
  `help`'s direct children).
- The `UG_GuideRegistryTests` baseline pin asserts the general invariant —
  "every guide that *has* a `de`/`fr`/`da` baseline carries exactly one row
  per language, and the second boot does not duplicate them" — so it holds
  for the full 41-guide set with all guides baselined.
- The "never machine-translated" clause (ADR 0005 C) is preserved: the lane
  ships only hand-curated baseline prose; no baseline is machine-generated.
- **This is the default going forward:** a new guide (top-level or nested)
  ships with its `en` floor *and* its `de`/`fr`/`da` hand-curated baselines in
  the same lane, mirroring the four-surface set and the original top-level
  guides (ADR 0057 D2). A guide that ships `en`-only is the exception and
  must say so in its ADR, not the norm.

### D5 — The tree browse, the `/admin/help` surface, and the reset lane are unchanged

- The `/pages` tree browse (ADR 0039 §3.8) renders a nested guide as a child
  of its parent guide automatically — it walks the `ParentId` chain, so no
  view change is needed. A resident sees `help → projects → boards`.
- `AllSeededSlugs()`, `FindSeededPageText()`, `HasSeededText()`, and the
  ADR 0128 status probe + bulk reset already query **by slug across the whole
  tree**, so a nested guide is listed, reset, and status-checked identically
  to a top-level one. No change.
- The `PagePaths.Derive` chain (ADR 0039 §3.3) resolves a nested guide's
  href to `/pages/system/help/{parent}/{slug}` (e.g.
  `/pages/system/help/projects/boards`) — no change, the chain is
  parent-id-driven.

## Consequences

- **For the resident:** every tabbed surface's sub-pages now have a dedicated
  help page *under* the surface guide, matching the app's tab structure. The
  root `help` page's guides list is extended to name the new sub-pages (in
  all four seeded languages), so the link is discoverable from the top level.
- **For the tree pin:** the top-level guides are still exactly the `help`
  children not in the map (23 unchanged + `settings`), and each nested guide
  is exactly the child of its map-named parent. The set remains closed,
  enumerable, and drift-pinned — the `UG_GuideRegistryTests` family is the
  gate, updated to the two-level shape.
- **For the seeder:** one new public method (`GuideParentMap`) and a parent
  resolution in `SeedUserGuidesAsync` / `BackfillUserGuidesAsync`. The
  `(ParentId, Slug)` unique index, the `Page` doc, the doc-type surface, the
  standing matrix, and the translator / reset / status lanes are all
  unchanged.
- **For the non-`en` baseline:** the new guides ship their `de` / `fr` / `da`
  baselines hand-curated in the same lane as the `en` floor (D4) — the same
  ADR 0057 D2 shape the original 23 top-level guides use, and now the default
  for any new guide. This is the ADR 0042 D1 split held at the new level.

## Amendments

### 2026-10-10 — the non-`en` baselines ship with the guides (the default going forward)

The original wording of D4 deferred the 17 nested guides' `de` / `fr` / `da`
baselines to a later human Translator lane (the guides shipped `en`-only on
first boot). User sign-off (2026-10-10) reverses that: **the hand-curated
`de` / `fr` / `da` baselines for all 17 nested guides + the `settings` index
now ship in the same lane as the `en` floor** — added to `DeGuidePages()` /
`FrGuidePages()` / `DaGuidePages()`, seeded create-if-missing by
`SeedGuideTranslationsAsync` and backfilled by
`BackfillGuideTranslationsAsync` (which was fixed to walk the two-level guide
subtree, not just `help`'s direct children). D4 is re-stated accordingly, and
the count is corrected to **17 nested** (the original D2 prose said "18" — the
table, `GuideParentMap()`, and the code all agree on 17).

This makes "a guide ships its `en` floor *and* its `de` / `fr` / `da`
hand-curated baselines" the **default for any new guide**, mirroring the
four-surface set and the original top-level guides (ADR 0057 D2). The
"never machine-translated" clause (ADR 0005 C) is unchanged — the baselines
are hand-written prose, not machine output.
