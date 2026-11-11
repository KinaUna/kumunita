---
id: r31-prompt
title: "Prompt: IMPROVE-UX R31 — empty states batch 3 (the 9 projects/todo/board views to .kmb-empty with one next action)"
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
> **R31** — empty states batch 3, the *third* empty-states batch named in the
> plan. It drives the **9 named projects/todo/board views** (`Boards/BoardIndex`,
> `Boards/_BoardLanes`, `Boards/_BoardList`, `Projects/ProjectsIndex`,
> `Projects/ProjectDetail`, `Todos/TodosIndex`, `Todos/_TodoComments`,
> `Todos/_TodoDisplay`, `Shared/_BoardCard`) to the house `.kmb-empty` idiom
> (a title, a body, and **one next action**). **Only 4 of the 9 are counted** —
> `Boards/BoardIndex`, `Projects/ProjectsIndex`, `Projects/ProjectDetail`,
> `Todos/TodosIndex` are PLAIN leads; the other 5 are `_`-prefixed partials the
> measure excludes. So after R31 `empty_plain` drops **22 → 18** (not 22 → 13)
> and `empty_none` is **untouched** (stays **31**). R31 is self-contained.
> **Who is it for:** the next coding agent (fresh context). **When to read the
> rest:** when starting R31 (R30 has just landed — the `empty_plain` floor is
> now `22`, `empty_none` is `31`; the house `.kmb-empty` idiom and the per-view
> shape are the R29/R30 handoff notes' closest precedent).

# Fresh-context prompt: IMPROVE-UX R31

You are continuing the Kumunita repository on `development` (HEAD `feb0b159`,
R30 just landed — `empty_plain` is **`22`**, `empty_none` is **`31`**, `a11y`
is `49`). **R31** is the next unit. The plan card (in `plan-improve-ux.md`, the
R31 row):

> R31 — Empty states batch 3 (projects): `Boards/BoardIndex`,
> `Boards/_BoardLanes`, `Boards/_BoardList`, `Projects/ProjectsIndex`,
> `Projects/ProjectDetail`, `Todos/TodosIndex`, `Todos/_TodoComments`,
> `Todos/_TodoDisplay`, `Shared/_BoardCard` — `.kmb-empty` + one next action.
> Read first: `Groups/Index.cshtml:144` (reference).
> **Done when: friendly.**

R31 is a **view-content** unit (same class as R24–R30). You do **not** touch the
script (`tools/ux/ux-measure.py`) or the baseline's other categories. You make
the named views render the house `.kmb-empty` idiom (a title, a body, and
**one next action**), so the measured `empty_plain` count drops by the counted
leads these views carry.

## The state of the code (measured 2026-11-11, `development`, HEAD `feb0b159`)

- **What a "PLAIN" lead is** — `count_empty_states()` in `tools/ux/ux-measure.py`.
  A list view (one containing a `@(foreach …)` or `@for …` loop) is a **PLAIN**
  lead when it carries an empty-check (`.Count == 0`, `.Any() == false`,
  `IsNullOrEmpty(…)`, etc.) **but no `.kmb-empty` block**, and is a **NONE** lead
  when it has neither a friendly empty state nor an empty-check. A view is
  **EXCLUDED** when it is a partial (`_`-prefixed), in an `Admin*` directory, one
  of the `non_list_views`, has no list-loop markup, or already has a `.kmb-empty`
  block. **Measure each view with the script's own logic before editing** — the
  R29 prompt's per-view labels were partly wrong; do not repeat that.

- **The 9 R31 views, classified (measured 2026-11-11):**

  **4 COUNTED views (PLAIN → `.kmb-empty`; these move `empty_plain` 22 → 18):**

  | View | Current empty state (key, container) | Natural next action (a target already in the view) |
  |---|---|---|
  | `Boards/BoardIndex` (`:79-81`) | `projects.board.empty` in `.p-4 text-muted` | New board → `/projects/boards/new`, key `projects.board.new` |
  | `Projects/ProjectsIndex` (`:71-73`, `:129-131`) | **two** sub-sections: `pl.index.goals_empty` (Goals) + `pl.index.projects_empty` (Standalone projects), both in `.p-4 text-muted` | New goal → `/projects/goals/new` (`pl.index.new_goal`); New project → `/projects/projects/new` (`pl.index.new_project`) |
  | `Projects/ProjectDetail` (`:503-506`, `:551-554`) | **two** sub-sections: `pl.project.todos_empty` (To-dos) + `pl.project.boards_empty` (Boards), both in `.kanban-board-empty p-4 text-muted border rounded` | New to-do → `/projects/todos/new` (`projects.todo.new`); New board → `/projects/boards/new` (`projects.board.new`) |
  | `Todos/TodosIndex` (`:158-160`) | `projects.todo.empty` in `.p-4 text-muted` | New to-do → `/projects/todos/new`, key `projects.todo.new` |

  **5 EXCLUDED partials (`_`-prefixed — the measure does NOT count them, so
  converting them does not change `empty_plain`/`empty_none`):**

  | Partial | Empty state? |
  |---|---|
  | `Boards/_BoardLanes` | has an empty-check (a natural lane-empty block) |
  | `Boards/_BoardList` | has an empty-check (a natural list-empty block) |
  | `Todos/_TodoComments` | has an empty-check (a natural comment-empty block) |
  | `Todos/_TodoDisplay` | **no** empty-check (a card/row component) |
  | `Shared/_BoardCard` | **no** empty-check (a card component) |

  **How to handle the 5 partials:** convert the three that carry a natural
  list-level empty state (`_BoardLanes`, `_BoardList`, `_TodoComments`) to the
  `.kmb-empty` idiom too (so "9 views friendly" holds for the card's DoD) —
  title + body + one next action where one genuinely fits the sub-section. For
  the two card components with **no** list-level empty state (`_BoardCard`,
  `_TodoDisplay`), do **not** force a misleading empty block: record each as a
  justified survivor in the handoff note (the R28 / `Directory/Detail` R30
  precedent) rather than inventing an action.

  **Re-baseline expectation:** the 4 counted views leave the PLAIN bucket, so
  `empty_plain` drops **22 → 18**. **`empty_none` stays 31** (none of the 9 is a
  counted NONE lead — the partials are excluded regardless; verify this and, if
  a counted view is actually NONE, re-base `empty_none` to the measured value
  and record it).

- **The house `.kmb-empty` idiom** — see `Groups/Index.cshtml:144` (and the
  R29/R30 views): a **title** (`.kmb-empty-title`), a **body** (`.kmb-empty-body`),
  and **one next action** (a button or link to the most relevant thing the actor
  can do in that context). All three are `<kw-l>`-wrapped (no hard-coded English
  — the `l10n` gate requires all four languages). The `kmb-empty` /
  `kmb-empty-title` / `kmb-empty-body` classes are the styling hook; do not
  invent new ones.

- **Keys to reuse (confirmed present in en/de/fr/da):** the six body keys
  (`projects.board.empty`, `pl.index.goals_empty`, `pl.index.projects_empty`,
  `pl.project.todos_empty`, `pl.project.boards_empty`, `projects.todo.empty`) and
  the four action keys (`projects.board.new`, `pl.index.new_goal`,
  `pl.index.new_project`, `projects.todo.new`). **Add** one short `*_title` key
  per empty block that needs one (en/de/fr/da) the way R29/R30 did — none of the
  six `*_empty_title` keys exist yet (`projects.board.empty_title`,
  `pl.index.goals_empty_title`, `pl.index.projects_empty_title`,
  `pl.project.todos_empty_title`, `pl.project.boards_empty_title`,
  `projects.todo.empty_title`). Do not add a body key if the existing `*_empty`
  sentence already reads as a complete empty-state.

## The work

Work view-by-view; after each, re-run the per-view measure (see *Before
committing*) to confirm that view now carries a `.kmb-empty` block before moving
on.

For each of the 4 COUNTED R31 views (and the 3 countable partials):

1. **Read the view** to find (a) the current empty-state block(s) and (b) the
   most natural next action (see the table above — prefer a target the view
   already links to).
2. **Replace** each existing empty-state block with a `.kmb-empty` block: a
   title (add one short `*_title` key, en/de/fr/da), a body (reuse the existing
   `*_empty` key), and **one** next-action link/button reusing an existing action
   key (`projects.board.new`, `pl.index.new_goal`, `pl.index.new_project`,
   `projects.todo.new`).
3. **Do not** change the normal-list markup (the `@(foreach …)` block) — only the
   empty-state branch. `Projects/ProjectsIndex` and `Projects/ProjectDetail` each
   carry **two** empty sub-sections — convert both, each with its own natural
   action.
4. **Do not** add inline `on*` handlers or `<script>` blocks (SEC gate).
5. **Do not** reword existing text — you are swapping the *container*
   (`.p-4 text-muted` / `.kanban-board-empty …` → `.kmb-empty`), not the words
   (reusing the existing `<kw-l>` body keys).

**Rules for all fixes (same as R24–R30):** Bootstrap classes and the grid layout
unchanged (only the `kmb-empty*` classes are added); **only the 9 named R31
views** (do not touch the R32–R33 views or the remaining NONE views); do not
edit `tools/ux/ux-measure.py`.

**JSON-editing caution (learned in R29/R30):** the fr/da/de files store
non-ASCII as `\uXXXX` escapes and apostrophes as `\u0027`; there is **no single
canonical formatter** that reproduces every file. Do **not** re-`json.dumps` a
file (it churns every value line and breaks the diff). Insert a new key as a
**single line**, escaping its value to match the local style (non-ASCII →
`\u%04X` uppercase, `'` → `\u0027`). The repo-wide `multi_replace_string_in_file`
collapses inserted lines onto the previous line — prefer a small insert script or
single-line `replace_string_in_file`, and check the diff is additions-only.

## The re-baseline

After all views are handled:

```bash
python3 tools/ux/ux-measure.py            # read the new empty_plain count
```

Expect `empty_plain` to drop **22 → 18** (the 4 counted views leave the PLAIN
bucket); `empty_none` **stays 31** (unless a counted view is actually NONE — then
re-base it to the measured value). In `tools/ux/ux-baseline.json`, set
`counts.empty_plain` to **18** and append a sentence to `generated_from` recording
the R31 re-base (house format — see the R30 entry as the most recent precedent;
append cleanly, no stray `..` at the seam).

## Scope rules

- **One commit:** `feat(ux): empty states batch 3 (projects) — .kmb-empty on the R31 views (IMPROVE-UX R31)`.
- **Files that should change:** the R31 views you actually convert (the 4 counted
  + whichever of the 3 countable partials you convert) + `tools/ux/ux-baseline.json`
  + the handoff note. For each new `*_title` (or body) key, the 4 translation
  JSON files (`en`/`de`/`fr`/`da` under `src/Kumunita.Core/Localization/Keys/`)
  change (additions-only).
- **Read-only:** `Groups/Index.cshtml` (the `.kmb-empty` reference),
  `tools/ux/ux-measure.py`, the R32–R33 views, and the remaining NONE views.

## Parked follow-up (record in the handoff note, do not create a new lane)

- **Remaining PLAIN leads after R31** — the R32–R33 scope:
  - R32 (content): `Document/Index`, `Document/Deleted`, `Page/Index`,
    `Page/_PageTreeItem`, `Inventory/List`, `Inventory/Detail`,
    `Bookmarks/Index`, `MyDrafts/Index`, `Notes/Index`.
  - R33 (details/groups/guardian): `Announcement/Detail`, `Event/Detail`,
    `Posts/Detail`, `Groups/Detail`, `Groups/_GroupMembers`,
    `Groups/_GroupPostReplies`, `Guardian/Detail`, `Guardian/Index`,
    `Guardian/_GuardianEventAttendance`, `Kiosk/Index`, `Locale/Index`,
    `Community/Manage`, `Community/Translations`.
- **Remaining NONE leads** — out of scope for the whole lane until a later batch.

## Before committing

```bash
# per-view — the 4 COUNTED R31 views must each report a .kmb-empty block.
python3 -c "
from pathlib import Path
for v in ['Boards/BoardIndex','Projects/ProjectsIndex','Projects/ProjectDetail','Todos/TodosIndex']:
    f = Path('src/Kumunita.Web/Views') / (v+'.cshtml')
    has = 'kmb-empty' in f.read_text(encoding='utf-8')
    print(('OK  ' if has else 'FAIL'), v)
"
python3 tools/ux/ux-measure.py            # empty_plain: 22 -> 18; empty_none: 31 (unchanged)
python3 tools/ux/ux-measure.py --check    # MUST pass
dotnet build                              # 0 errors
```

Confirm: all 4 counted R31 views report `.kmb-empty`; `empty_plain` dropped
**22 → 18**; `empty_none` unchanged (or honestly re-based); other categories
unchanged; `--check` passes; `dotnet build` is green; the C02 four-language
key-parity test passes if you added keys (run the C02 class via the xunit.v3
in-process runner — `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.C02_DataAsEmbeddedResourcesTests"`).
Then report: per-view before → after (PLAIN → `.kmb-empty`), which of the 5
partials you converted vs. recorded as a justified survivor (and why), the new
`empty_plain` number, the `generated_from` note, whether any new translation key
was needed (and which), and the commit hash. Also confirm in the handoff note
that the remaining PLAIN + NONE leads are parked for R32–R33 (do not create a
new lane).
