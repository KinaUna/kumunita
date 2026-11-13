---
id: r30-prompt
title: "Prompt: IMPROVE-UX R30 — empty states batch 2 (the 8 people/messages views to .kmb-empty with one next action)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/dev/razor-verification.md
lane: improve-ux
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> **R30** — empty states batch 2, the *second* empty-states batch named in the
> plan. It drives the **8 named people/messages views** (`Directory/Index`,
> `Directory/Detail`, `FindPeople/Bio`, `FindPeople/Tag`, `Messages/Index`,
> `Messages/Thread`, `GroupMessages/Index`, `GroupMessages/Thread`) to the
> house `.kmb-empty` idiom (a title, a body, and **one next action**). All 8
> are currently **PLAIN** leads (measured with the script's own logic) and all
> 8 count in `empty_plain` (measured **30** at HEAD, floor **30**); after R30
> `empty_plain` drops to **22** and `empty_none` is **untouched** (stays **31**).
> R30 is self-contained.
> **Who is it for:** the next coding agent (fresh context). **When to read the
> rest:** when starting R30 (R29 has just landed — the `empty_plain` floor is
> now `30`, `empty_none` is `31`; the house `.kmb-empty` idiom and the per-view
> shape are the R29 handoff note's closest precedent).

# Fresh-context prompt: IMPROVE-UX R30

You are continuing the Kumunita repository on `development` (HEAD `9c2fbfeb`,
R29 just landed — `empty_plain` is **`30`**, `empty_none` is **`31`**, `a11y`
is `49`). **R30** is the next unit. The plan card (in `plan-improve-ux.md`, the
R30 row):

> R30 — Empty states batch 2 (people/messages): `Directory/Index`,
> `Directory/Detail`, `FindPeople/Bio`, `FindPeople/Tag`, `Messages/Index`,
> `Messages/Thread`, `GroupMessages/Index`, `GroupMessages/Thread` — `.kmb-empty`
> + one next action.
> Read first: `Groups/Index.cshtml:144` (reference).
> **Done when: 8 views friendly.**

R30 is a **view-content** unit (same class as R24–R29). You do **not** touch the
script (`tools/ux/ux-measure.py`) or the baseline's other categories. You make
the 8 named views render the house `.kmb-empty` idiom (a title, a body, and
**one next action**), so the measured `empty_plain` count drops by the leads
these 8 views carry.

## The state of the code (measured 2026-11-11, `development`, HEAD `9c2fbfeb`)

- **What a "PLAIN" lead is** — `count_empty_states()` in `tools/ux/ux-measure.py`.
  A list view (one containing a `@(foreach …)` or `@for …` loop) is a **PLAIN**
  lead when it carries an empty-check (`.Count == 0`, `.Any() == false`,
  `IsNullOrEmpty(…)`, etc.) **but no `.kmb-empty` block**, and is a **NONE** lead
  when it has neither a friendly empty state nor an empty-check. A view is
  **EXCLUDED** when it is a partial (`_`-prefixed), in an `Admin*` directory, one
  of the `non_list_views`, has no list-loop markup, or already has a `.kmb-empty`
  block. **Measure each view with the script's own logic before editing** — the
  R29 prompt's per-view labels were partly wrong; do not repeat that.

- **The 8 R30 views, all classified PLAIN (measured 2026-11-11):**

  | View | Current empty state | Natural next action (a target already in the view) |
  |---|---|---|
  | `Directory/Index` | `directory.empty` in a `.alert alert-info` | See the feed (`/community`) — no "add resident" action exists |
  | `Directory/Detail` | `profile.detail.tags.empty` ("No tags set") — a *sub-section* empty | back to the directory (`Url.Action("Index","Directory")`) — see the caution below |
  | `FindPeople/Bio` | `profile.find.empty` in a `.alert alert-info` | browse the directory (`/directory`) instead of searching |
  | `FindPeople/Tag` | `profile.find.empty` in a `.alert alert-info` | browse the directory (`/directory`) instead of searching |
  | `Messages/Index` | `message.thread.empty` in a `.alert alert-secondary` | find people to message (`/people`) |
  | `Messages/Thread` | `message.thread.empty` in a `.alert alert-secondary` | back to your messages (`/messages`) |
  | `GroupMessages/Index` | `group_message.thread.empty` in a `.alert alert-secondary` | browse groups (`/groups`) |
  | `GroupMessages/Thread` | `group_message.thread.empty` in a `.alert alert-secondary` | back to group messages (`/group-messages`) |

  **Caution — `Directory/Detail`:** its only empty state is the *tags sub-section*
  ("No tags set"), not a main list. If the three-element idiom (title + body +
  one next action) is a genuine poor fit for a read-only sub-section, still
  produce title + body + a sensible "back to the directory" action, **or** — if
  you judge it truly out of place — record it in the handoff note as a justified
  survivor (the R28 precedent) rather than forcing a misleading action. Do not
  touch the rest of the profile card.

  **Re-baseline expectation:** all 8 are PLAIN, so `empty_plain` drops **30 → 22**.
  **`empty_none` stays 31** (none of the 8 is a NONE lead — verify this; if any is
  actually NONE, re-base `empty_none` to the measured value too and record it).

- **The house `.kmb-empty` idiom** — see `Groups/Index.cshtml:144` (and the R29
  views): a **title** (`.kmb-empty-title`), a **body** (`.kmb-empty-body`), and
  **one next action** (a button or link to the most relevant thing the actor can
  do in that context). All three are `<kw-l>`-wrapped (no hard-coded English —
  the `l10n` gate requires all four languages). The `kmb-empty` /
  `kmb-empty-title` / `kmb-empty-body` classes are the styling hook; do not invent
  new ones.

- **Keys to reuse (confirmed present in en/de/fr/da):** `directory.empty`,
  `profile.find.empty`, `message.thread.empty`, `group_message.thread.empty`,
  `profile.detail.tags.empty`, `directory.detail_back`, and `about.cta_feed`
  (the "See the feed" action). Where a view has only a short sentence and no
  distinct short **title** key, add **one** short `*_title` key (en/de/fr/da) the
  way R29 did — do not add a body key if the existing sentence already reads as a
  complete empty-state.

## The work

Work view-by-view; after each, re-run the per-view measure (see *Before
committing*) to confirm that view now carries a `.kmb-empty` block before moving on.

For each of the 8 R30 views:

1. **Read the view** to find (a) the current empty-state block and (b) the most
   natural next action (see the table above — prefer a target the view already
   links to).
2. **Replace** the existing empty-state block with a `.kmb-empty` block: a title
   (reuse the existing sentence key, or add one short `*_title` key), a body
   (reuse the existing sentence key, or a new one if the title and body must
   differ), and **one** next-action link/button reusing an existing action key
   where one fits (e.g. `about.cta_feed`, `directory.detail_back`) or the route
   the view already uses.
3. **Do not** change the normal-list markup (the `@(foreach …)` block) — only the
   empty-state branch. `Messages/Index` and `GroupMessages/Index` may have an
   inner `Candidates`/`Groups == 0` branch — leave that inner logic; convert the
   outer list-empty state to `.kmb-empty`.
4. **Do not** add inline `on*` handlers or `<script>` blocks (SEC gate).
5. **Do not** reword existing text — you are swapping the *container*
   (`.alert …` → `.kmb-empty`), not the words (reusing the existing `<kw-l>` keys).

**Rules for all fixes (same as R24–R29):** Bootstrap classes and the grid layout
unchanged (only the `kmb-empty*` classes are added); **only the 8 named R30
views** (do not touch the R31–R33 views or the remaining NONE views); do not edit
`tools/ux/ux-measure.py`.

**JSON-editing caution (learned in R29):** the fr/da/de files store non-ASCII as
`\uXXXX` escapes and apostrophes as `\u0027`; there is **no single canonical
formatter** that reproduces every file. Do **not** re-`json.dumps` a file (it
churns every value line and breaks the diff). Insert a new key as a **single
line**, escaping its value to match the local style (non-ASCII → `\u%04X`
uppercase, `'` → `\u0027`). The repo-wide `multi_replace_string_in_file` collapses
inserted lines onto the previous line — prefer a small insert script or single-line
`replace_string_in_file`, and check the diff is additions-only.

## The re-baseline

After all views are handled:

```bash
python3 tools/ux/ux-measure.py            # read the new empty_plain count
```

Expect `empty_plain` to drop **30 → 22** (all 8 leave the PLAIN bucket);
`empty_none` **stays 31** (unless a view is actually NONE — then re-base it to the
measured value). In `tools/ux/ux-baseline.json`, set `counts.empty_plain` to
**22** and append a sentence to `generated_from` recording the R30 re-base (house
format — see the R29 entry as the most recent precedent; append cleanly, no stray
`..` at the seam).

## Scope rules

- **One commit:** `feat(ux): empty states batch 2 (people/messages) — .kmb-empty on the 8 R30 views (IMPROVE-UX R30)`.
- **Files that should change:** the 8 views above + `tools/ux/ux-baseline.json` +
  the handoff note. If a view needs a new `*_title` (or body) key, the 4
  translation JSON files (`en`/`de`/`fr`/`da` under `src/Kumunita.Core/Localization/Keys/`)
  may also change.
- **Read-only:** `Groups/Index.cshtml` (the `.kmb-empty` reference),
  `tools/ux/ux-measure.py`, the R31–R33 views, and the remaining NONE views.

## Parked follow-up (record in the handoff note, do not create a new lane)

- **22 remaining PLAIN leads** — the R31–R33 scope:
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
- **Remaining NONE leads** — out of scope for the whole lane until a later batch.

## Before committing

```bash
# per-view — the 8 R30 views must each report a .kmb-empty block.
python3 -c "
from pathlib import Path
for v in ['Directory/Index','Directory/Detail','FindPeople/Bio','FindPeople/Tag','Messages/Index','Messages/Thread','GroupMessages/Index','GroupMessages/Thread']:
    f = Path('src/Kumunita.Web/Views') / (v+'.cshtml')
    has = 'kmb-empty' in f.read_text(encoding='utf-8')
    print(('OK  ' if has else 'FAIL'), v)
"
python3 tools/ux/ux-measure.py            # empty_plain: 30 -> 22; empty_none: 31 (unchanged)
python3 tools/ux/ux-measure.py --check    # MUST pass
dotnet build                              # 0 errors
```

Confirm: all 8 R30 views report `.kmb-empty`; `empty_plain` dropped **30 → 22**;
`empty_none` unchanged (or honestly re-based); other categories unchanged;
`--check` passes; `dotnet build` is green; the C02 four-language key-parity test
passes if you added keys. Then report: per-view before → after (PLAIN →
`.kmb-empty`), the exact `.kmb-empty` block added (file:line), the new
`empty_plain` number, the `generated_from` note, whether any new translation key
was needed (and which), and the commit hash. Also confirm in the handoff note that
the remaining PLAIN + NONE leads are parked for R31–R33 (do not create a new lane).
