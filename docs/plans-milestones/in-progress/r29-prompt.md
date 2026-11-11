---
id: r29-prompt
title: "Prompt: IMPROVE-UX R29 — empty states batch 1 (the 8 feed views to .kmb-empty with one next action)"
tier: living
kind: plan
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/dev/razor-verification.md
lane: improve-ux
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> **R29** — empty states batch 1, the *first* empty-states batch named in
> the plan. It drives the **8 named feed views** (`Posts/Index`, `Blog/Feed`,
> `Home/Index`, `Search/Index`, `Tag/ByTag`, `Tag/Index`, `Notifications/Index`,
> `Notifications/Subscriptions`) to the house `.kmb-empty` idiom (a title, a
> body, and **one next action**) — 6 of the 8 currently count in the
> `empty_plain` baseline (37) and 2 are structurally empty but not counted.
> After R29, the measured `empty_plain` floor drops to **30** (the 6
> currently-counted PLAIN views leave the PLAIN bucket; the 2 structurally-
> empty views were never counted and remain uncounted). The remaining 30
> PLAIN views across the other 4 batches (R30–R33) are **out of scope** and
> stay in the baseline. R29 is self-contained.
> **Who is it for:** the next coding agent (fresh context). **When to read the
> rest:** when starting R29 (R28 has just landed — the `a11y` floor is now
> `49`; the house `.kmb-empty` idiom is the one to internalize first,
> `Groups/Index.cshtml:144`).

# Fresh-context prompt: IMPROVE-UX R29

You are continuing the Kumunita repository on `development` (HEAD `b0914575`,
R28 just landed — the a11y floor is **`49`**; the `empty_plain` floor is
**`37`**, `empty_none` is **`33`**). **R29** is the next unit. The plan card
(in `plan-improve-ux.md`, the R29 row):

> R29 — Empty states batch 1 (feeds): `Posts/Index`, `Blog/Feed`, `Home/Index`,
> `Search/Index`, `Tag/ByTag`, `Tag/Index`, `Notifications/Index`,
> `Notifications/Subscriptions` — `.kmb-empty` + one next action.
> Read first: `Groups/Index.cshtml:144` (reference).
> **Done when: 8 views friendly.**

R29 is a **view-content** unit (same class as R24–R28). You do **not** touch
the script (`tools/ux/ux-measure.py`), the baseline's other categories, or the
7 non-feed views that R30–R33 will cover. You make the 8 named feed views
render the house `.kmb-empty` idiom (a title, a body, and **one next action**),
so the measured `empty_plain` count drops by exactly the leads these views
carry.

## The state of the code (measured 2026-11-11, `development`, HEAD `b0914575`)

- **What a "PLAIN" lead is** — `count_empty_states()` in `tools/ux/ux-measure.py`.
  A list view (one that contains a `@(foreach …)` or `@for …` loop) is a
  **PLAIN** lead when it carries an empty-check (`.Count == 0`, `.Any() == false`,
  `IsNullOrEmpty(...)`, etc.) **but no `.kmb-empty` block**, and is a **NONE**
  lead when it has neither a friendly empty state nor an empty-check. A view
  is **EXCLUDED** from the count when it (a) is a partial (`_`-prefixed
  filename), (b) is in an `Admin*` directory, (c) is one of the `non_list_views`
  (Calendar, Create, Edit, New, Resolve, ResolveReview), (d) has no list-loop
  markup at all, or (e) already has a `.kmb-empty` block.
- **The 8 R29 views, classified (measured 2026-11-11):**

  | View | Current state | In `empty_plain` count? | Fix shape |
  |---|---|---|---|
  | `Posts/Index` | PLAIN (a bare `.alert alert-info`) | **Yes** | replace the alert with `.kmb-empty` |
  | `Blog/Feed` | PLAIN | **Yes** | replace the alert with `.kmb-empty` |
  | `Search/Index` | PLAIN | **Yes** | replace the alert with `.kmb-empty` |
  | `Tag/ByTag` | PLAIN | **Yes** | replace the alert with `.kmb-empty` |
  | `Tag/Index` | PLAIN | **Yes** | replace the alert with `.kmb-empty` |
  | `Notifications/Index` | PLAIN | **Yes** | replace the alert with `.kmb-empty` |
  | `Home/Index` | structurally empty (line 318: `home.feed_empty` text) | **No** (no `@(foreach` markup, the script excludes it) | add a `.kmb-empty` block (cosmetic; no baseline change) |
  | `Notifications/Subscriptions` | NONE (no empty state at all) | **No** (no `@(foreach` markup at the top level — the `@foreach` is inside a `@foreach` of a `GroupBy` result, which the script's `LIST_MARKUP` does not match) | add a `.kmb-empty` block (cosmetic; no baseline change) |

  After R29, the measured `empty_plain` floor drops **37 → 30** (the 6
  currently-counted PLAIN views leave the PLAIN bucket; the 2 structurally-
  empty views were never counted and remain uncounted). The 30 remaining
  PLAIN leads live in the 7 non-feed views that R30–R33 will cover.

- **The house `.kmb-empty` idiom** — see `Groups/Index.cshtml:144`:
  ```cshtml
  @if (Model.Groups.Count == 0)
  {
      <div class="kmb-empty">
          <p class="kmb-empty-title"><kw-l key="groups.empty">No groups yet.</kw-l></p>
          <p class="kmb-empty-body">
              <kw-l key="groups.index_empty_body">Groups organize residents, e.g. "Building 4", "Volunteers", "Bike owners".</kw-l>
          </p>
          <a href="@Url.Action("Create", "Groups")" class="btn btn-primary">
              <kw-l key="groups.create">Create a group</kw-l>
          </a>
      </div>
  }
  else
  {
      @* the normal list markup *@
  }
  ```
  The three elements are non-negotiable: a **title** (`.kmb-empty-title`),
  a **body** (`.kmb-empty-body`), and **one next action** (a button or link
  to the most relevant thing the actor can do in that context). All three
  are `<kw-l>`-wrapped (no hard-coded English — the `l10n` gate requires
  all four languages). The `kmb-empty` / `kmb-empty-title` / `kmb-empty-body`
  classes are the styling hook; do not invent new ones.

## The work

Work view-by-view; after each, re-run the per-view measure (see *Before
committing*) to confirm that view is now a `.kmb-empty` block before moving on.

For each of the 8 R29 views:

1. **Read the view** to find (a) the current empty-state block (if any) and
   (b) the most natural "next action" for the actor in that context.
   - `Posts/Index` — next action: a "Write a post" button (the `posts.write`
     key already exists on the page at line 132; the button should point at
     the same target the existing "Write a post" button on the page uses).
   - `Blog/Feed` — next action: a "Write a post" button (the same shape; the
     view already has a `posts.write` button — reuse it).
   - `Search/Index` — next action: a link back to the feed or a prompt to
     clear the search (there is no "create" action — the next action is a
     "Back to the feed" link).
   - `Tag/ByTag` — next action: a "Write a post" button (same shape as
     `Posts/Index`; the view already carries the `posts.write` button).
   - `Tag/Index` — next action: a "Write a post" button (same shape).
   - `Notifications/Index` — next action: a link to the feed (no "create"
     action for notifications; the next action is a "Go to the feed" link).
   - `Home/Index` — next action: a "Write a post" button (same shape as
     `Posts/Index`). The view already has the `home.feed_empty` text at line
     318 — replace the bare text with the `.kmb-empty` block, keeping the
     `home.feed_empty` text as the title.
   - `Notifications/Subscriptions` — next action: a "Go to notifications"
     link (no "create" action; the next action is a link to `Notifications/Index`).
2. **Replace** the existing empty-state block (or **add** one for the 2
   NONE/structurally-empty views) with a `.kmb-empty` block following the
   idiom above. The title reuses an existing `<kw-l>` key where possible
   (e.g. `posts.empty`, `home.feed_empty`); the body reuses an existing key
   or adds **one new** key if no suitable one exists (the plan's lane rules
   allow new strings to en/de/fr/da; de/fr/da machine-written, flagged for
   the parked native review); the next action reuses the existing
   `posts.write` key (or the equivalent for the "Back to feed" shape) and
   the same `Url.Action(...)` the view already uses for that button.
3. **Do not** change the normal-list markup (the `@(foreach …)` block) —
   only the empty-state branch.
4. **Do not** add inline `on*` handlers or `<script>` blocks (SEC gate).
5. **Do not** introduce a new translation key for the **title** if one
   already exists on the page (`posts.empty`, `home.feed_empty`,
   `groups.empty`, etc.) — reuse it.

**Rules for all fixes (same as R24–R28):**

- The label text stays exactly as-is where it is already a `<kw-l key="…">`
  — you are **replacing** the container (`.alert alert-info` or bare text)
  with the `.kmb-empty` block, not rewording the text.
- Bootstrap classes and the grid layout unchanged (the `.kmb-empty` /
  `.kmb-empty-title` / `.kmb-empty-body` are the only added classes).
- **Only the 8 named R29 views.** Do not touch the 7 non-feed PLAIN views
  (R30–R33's scope) or the 33 NONE views (out of scope for the whole lane
  until a later batch).
- Do not edit `tools/ux/ux-measure.py` (risky; would require re-testing all
  61 list views).

## The re-baseline

After all views are handled:

```bash
python3 tools/ux/ux-measure.py            # read the new empty_plain count
```

Expect `empty_plain` to drop from **37** to **30** (the 6 currently-counted
PLAIN views leave the PLAIN bucket; the 2 structurally-empty views were never
counted and remain uncounted). In `tools/ux/ux-baseline.json`, set
`counts.empty_plain` to **30** and append a sentence to the `generated_from`
string recording the R29 re-base (house format — see the R28 entry as the
most recent precedent). Do **not** re-base `empty_none` (the 2 structurally-
empty views are not in the NONE bucket — the script excludes them). When
appending, watch the seam: a `+=` concatenation can leave a stray `..` at the
previous note's boundary (this happened on R26) — check and clean it.

## Scope rules

- **One commit:** `feat(ux): empty states batch 1 (feeds) — .kmb-empty on the 8 R29 feed views (IMPROVE-UX R29)`.
- **Files that should change:** `Posts/Index.cshtml`, `Blog/Feed.cshtml`,
  `Home/Index.cshtml`, `Search/Index.cshtml`, `Tag/ByTag.cshtml`,
  `Tag/Index.cshtml`, `Notifications/Index.cshtml`,
  `Notifications/Subscriptions.cshtml` (the 8 views) + `tools/ux/ux-baseline.json`.
  If a view needs a new body key, the 4 translation JSON files
  (`en.json`/`de.json`/`fr.json`/`da.json` under `Localization/Keys/`) may
  also change.
- **Read-only:** `Groups/Index.cshtml` (the `.kmb-empty` reference),
  `tools/ux/ux-measure.py`, the 7 non-feed PLAIN views (R30–R33's scope),
  and the 33 NONE views (out of scope).

## Parked follow-up (record in the handoff note, do not create a new lane)

- **7 non-feed PLAIN views (30 leads)** — the R30–R33 scope:
  - R30 (people/messages): `Directory/Index`, `Directory/Detail`,
    `FindPeople/Bio`, `FindPeople/Tag`, `Messages/Index`, `Messages/Thread`,
    `GroupMessages/Index`, `GroupMessages/Thread`.
  - R31 (projects): `Boards/BoardIndex`, `Boards/_BoardLanes`,
    `Boards/_BoardList`, `Projects/ProjectsIndex`, `Projects/ProjectDetail`,
    `Todos/TodosIndex`, `Todos/_TodoComments`, `Todos/_TodoDisplay`,
    `Shared/_BoardCard`.
  - R32 (content): `Document/Index`, `Document/Deleted`, `Page/Index`,
    `Page/_PageTreeItem`, `Inventory/List`, `Inventory/Detail`,
    `Bookmarks/Index`, `MyDrafts/Index`, `Notes/Index`.
  - R33 (details/groups/guardian): `Announcement/Detail`, `Event/Detail`,
    `Posts/Detail`, `Groups/Detail`, `Groups/_GroupMembers`,
    `Groups/_GroupPostReplies`, `Guardian/Detail`, `Guardian/Index`,
    `Guardian/_GuardianEventAttendance`, `Kiosk/Index`, `Locale/Index`,
    `Community/Manage`, `Community/Translations`.
- **33 NONE views** — out of scope for the whole lane until a later batch.

## Before committing

```bash
# per-view — the 8 R29 views must each report a .kmb-empty block.
python3 -c "
from pathlib import Path
for v in ['Posts/Index','Blog/Feed','Home/Index','Search/Index','Tag/ByTag','Tag/Index','Notifications/Index','Notifications/Subscriptions']:
    f = Path('src/Kumunita.Web/Views') / (v+'.cshtml')
    t = f.read_text(encoding='utf-8')
    has = 'kmb-empty' in t
    print(('OK  ' if has else 'FAIL'), v, ('kmb-empty' if has else 'NO .kmb-empty'))
"
python3 tools/ux/ux-measure.py            # empty_plain: 37 -> 30
python3 tools/ux/ux-measure.py --check    # MUST pass
dotnet build                              # 0 errors
```

Confirm: all 8 R29 views report `.kmb-empty`; `empty_plain` dropped
**37 → 30**; other categories unchanged; `--check` passes; `dotnet build`
is green. Then report: per-view before → after (PLAIN/NONE → `.kmb-empty`),
the exact `.kmb-empty` block added (file:line), the new `empty_plain` number,
the `generated_from` note, whether any new translation key was needed (and
which — expected: only the body keys, if any), and the commit hash. Also
confirm in the handoff note that the 30 remaining PLAIN + 33 NONE leads are
parked for R30–R33 (do not create a new lane).
