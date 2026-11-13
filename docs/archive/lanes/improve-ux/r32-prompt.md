---
id: r32-prompt
title: "Prompt: IMPROVE-UX R32 — empty states batch 4 (the 8 counted content views to .kmb-empty with one next action)"
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
> **R32** — empty states batch 4, the *fourth* empty-states batch named in the
> plan. It drives the **9 named content views** (`Document/Index`,
> `Document/Deleted`, `Page/Index`, `Page/_PageTreeItem`, `Inventory/List`,
> `Inventory/Detail`, `Bookmarks/Index`, `MyDrafts/Index`, `Notes/Index`) to the
> house `.kmb-empty` idiom (a title, a body, and **one next action**). **Only 8
> of the 9 are counted** — `Document/Index`, `Document/Deleted`, `Page/Index`,
> `Inventory/List`, `Inventory/Detail`, `Bookmarks/Index`, `MyDrafts/Index`,
> `Notes/Index` are PLAIN leads; `Page/_PageTreeItem` is a `_`-prefixed partial
> the measure excludes. So after R32 `empty_plain` drops **18 → 10** (not 18 → 9)
> and `empty_none` is **untouched** (stays **31** — all 8 counted views are PLAIN,
> 0 NONE). R32 is self-contained.
> **Who is it for:** the next coding agent (fresh context). **When to read the
> rest:** when starting R32 (R31 has just landed — `empty_plain` is **18**,
> `empty_none` is **31**; the house `.kmb-empty` idiom and the per-view shape are
> the R29/R30/R31 handoff notes' closest precedent).

# Fresh-context prompt: IMPROVE-UX R32

You are continuing the Kumunita repository on `development` (HEAD `520b9d86`,
R31 just landed — `empty_plain` is **18**, `empty_none` is **31**, `a11y` is
`49`). **R32** is the next unit. The plan card (in `plan-improve-ux.md`, the R32
row):

> R32 — Empty states batch 4 (content): `Document/Index`, `Document/Deleted`,
> `Page/Index`, `Page/_PageTreeItem`, `Inventory/List`, `Inventory/Detail`,
> `Bookmarks/Index`, `MyDrafts/Index`, `Notes/Index` — `.kmb-empty` + one next
> action. Read first: `Groups/Index.cshtml:144` (reference).
> **Done when: friendly.**

R32 is a **view-content** unit (same class as R24–R31). You do **not** touch the
script (`tools/ux/ux-measure.py`) or the baseline's other categories. You make
the named views render the house `.kmb-empty` idiom (a title, a body, and
**one next action**), so the measured `empty_plain` count drops by the counted
leads these views carry.

## The state of the code (measured 2026-11-11, `development`, HEAD `520b9d86`)

- **What a "PLAIN" lead is** — `count_empty_states()` in `tools/ux/ux-measure.py`.
  A list view (one containing a `@(foreach …)` or `@for …` loop) is a **PLAIN**
  lead when it carries an empty-check (`.Count == 0`, `.Any() == false`,
  `IsNullOrEmpty(…)`, etc.) **but no `.kmb-empty` block**, and is a **NONE** lead
  when it has neither a friendly empty state nor an empty-check. A view is
  **EXCLUDED** when it is a partial (`_`-prefixed), in an `Admin*` directory, one
  of the `non_list_views`, has no list-loop markup, or already has a `.kmb-empty`
  block. **Measure each view with the script's own logic before editing** — the
  R29 prompt's per-view labels were partly wrong; do not repeat that.

- **The 9 R32 views, classified (measured 2026-11-11):**

  **8 COUNTED views (PLAIN → `.kmb-empty`; these move `empty_plain` 18 → 10):**

  | View | Current empty state (key, container) | Natural next action (a target already in the view) |
  |---|---|---|
  | `Document/Index` (`:97`) | `documents.empty` in `.p-4 text-muted` | Upload a document → `/documents/new`, key `documents.upload` |
  | `Document/Deleted` (`:33`) | `documents.deleted_empty` in `.p-4 text-muted` | Back to documents (`/documents`, key `documents.title` — the view's own back link) |
  | `Page/Index` (`:63`) | `pages.none` in `.alert.alert-info` | New page → `/pages/new`, key `pages.new_button` |
  | `Inventory/List` (`:107`) | `inv.list.empty` in `.p-4 text-muted` | New item → `/inventory/new`, key `inv.create.title` |
  | `Inventory/Detail` (`:127`) | `inv.list.empty` in `.p-3 text-muted` (**a sub-section**: "Usage history") | see the caution below — no "new history" action exists |
  | `Bookmarks/Index` (`:89`) | `bm.list.empty` in `.p-4 text-muted` | New folder → `/bookmarks/folders/new`, key `bm.folder.new` |
  | `MyDrafts/Index` (`:19`, the `!any` branch) | `my_drafts.empty` in `.alert.alert-light.border` | the view has **no New button** — pick a sensible existing link (e.g. the community feed) and record the choice |
  | `Notes/Index` (`:110`) | `pn.list.empty` in `.p-4 text-muted` | New folder → `/notes/folders/new`, key `pn.folder.new` |

  **1 EXCLUDED partial (`_`-prefixed — the measure does NOT count it, so
  converting it does not change `empty_plain`/`empty_none`):**

  | Partial | Empty state? |
  |---|---|
  | `Page/_PageTreeItem` | a collapsible tree-item component (renders per page node); if it has no natural list-level empty state of its own, record it as a justified survivor (the R31 `_BoardCard` / `_TodoDisplay` precedent) rather than inventing an action. Convert it only if it genuinely carries its own empty list branch. |

  **How to handle `Page/_PageTreeItem`:** inspect it first. If it renders a
  per-node child list with a natural empty branch, convert that branch to
  `.kmb-empty` (title + body + one action where one fits). If it is just a
  card/row component with no list-level empty state, do **not** force a
  misleading block — record it as a justified survivor in the handoff note.

  **Re-baseline expectation:** the 8 counted views leave the PLAIN bucket, so
  `empty_plain` drops **18 → 10**. **`empty_none` stays 31** (all 8 counted
  views are PLAIN, 0 NONE — verify this and, if any counted view is actually
  NONE, re-base `empty_none` to the measured value and record it).

- **The house `.kmb-empty` idiom** — see `Groups/Index.cshtml:144` (and the
  R29/R30/R31 views): a **title** (`.kmb-empty-title`), a **body**
  (`.kmb-empty-body`), and **one next action** (a button or link to the most
  relevant thing the actor can do in that context). All three are `<kw-l>`-wrapped
  (no hard-coded English — the `l10n` gate requires all four languages). The
  `kmb-empty` / `kmb-empty-title` / `kmb-empty-body` classes are the styling hook;
  do not invent new ones.

- **Keys to reuse (confirmed present in en/de/fr/da):** the seven body keys
  (`documents.empty`, `documents.deleted_empty`, `pages.none`, `inv.list.empty`,
  `bm.list.empty`, `my_drafts.empty`, `pn.list.empty`) and the action keys
  (`documents.upload`, `documents.title`, `pages.new_button`, `inv.create.title`,
  `bm.folder.new`, `pn.folder.new`). **Add** one short `*_title` key per empty
  block that needs one (en/de/fr/da) the way R29–R31 did — none of the R32
  `*_empty_title` keys exist yet (e.g. `documents.empty_title`,
  `documents.deleted_empty_title`, `pages.none_title`, `inv.list.empty_title`,
  `bm.list.empty_title`, `my_drafts.empty_title`, `pn.list.empty_title`). Do not
  add a body key if the existing `*_empty` sentence already reads as a complete
  empty-state.

  **Note:** `inv.list.empty` is reused by **both** `Inventory/List` and
  `Inventory/Detail` (the detail page's "Usage history" sub-section). If you
  convert both, they may share the one `inv.list.empty_title` key — but if the
  detail sub-section is read-only (it is: history is append-only via
  check-in/out), prefer recording `Inventory/Detail` as a **justified survivor**
  (see the caution) or giving it a distinct title + a back-link action rather
  than a misleading "new history" affordance.

## The work

Work view-by-view; after each, re-run the per-view measure (see *Before
committing*) to confirm that view now carries a `.kmb-empty` block before moving
on.

For each of the 8 COUNTED R32 views (and `Page/_PageTreeItem` if it qualifies):

1. **Read the view** to find (a) the current empty-state block and (b) the most
   natural next action (see the table above — prefer a target the view already
   links to).
2. **Replace** the existing empty-state block with a `.kmb-empty` block: a title
   (add one short `*_title` key, en/de/fr/da), a body (reuse the existing
   `*_empty` key), and **one** next-action link/button reusing an existing action
   key.
3. **Do not** change the normal-list markup (the `@(foreach …)` block) — only the
   empty-state branch.
4. **Do not** add inline `on*` handlers or `<script>` blocks (SEC gate).
5. **Do not** reword existing text — you are swapping the *container*
   (`.p-4 text-muted` / `.alert …` → `.kmb-empty`), not the words (reusing the
   existing `<kw-l>` body keys).

**The two cautions (decide, then record):**

- **`Inventory/Detail`** — the empty branch is the "Usage history" sub-section
  (`inv.detail.history` heading). There is no "create a history entry" action
  (history is append-only via check-in/check-out), so a "new item" action would
  be misleading. Either (a) convert it with a **back-to-inventory** action
  (reusing the item's back link if present) + a distinct title, **or** (b) record
  it as a justified survivor (the R30 `Directory/Detail` / R31 `_BoardList`
  precedent). Do not invent a "new history" button.
- **`MyDrafts/Index`** — the page has **no New button** (drafts are created by
  saving a post/announcement/event elsewhere). Pick a sensible existing link as
  the single next action (e.g. the community feed) and **record** the choice in
  the handoff note; do not add a dead "new draft" route.

**Rules for all fixes (same as R24–R31):** Bootstrap classes and the grid layout
unchanged (only the `kmb-empty*` classes are added); **only the 9 named R32
views** (do not touch the R33 views or the remaining NONE views); do not edit
`tools/ux/ux-measure.py`.

**JSON-editing caution (learned in R29–R31):** the fr/da/de files store
non-ASCII as `\uXXXX` escapes and apostrophes as `\u0027`; there is **no single
canonical formatter** that reproduces every file. Do **not** re-`json.dumps` a
whole file (it churns every value line and breaks the diff). Insert a new key as
a **single line**, escaping its value to match the local style (non-ASCII →
`\u%04X` uppercase, `'` → `\u0027`). The last line of each file has **no trailing
comma**, so when appending N keys add a comma after the previous last key, put
commas between the new keys except after the final one. The repo-wide
`multi_replace_string_in_file` can collapse inserted lines — prefer a small
insert script or single-line `replace_string_in_file`, and **check the diff is
additions-only** (each file should read as valid JSON with `json.load`).

## The re-baseline

After all views are handled:

```bash
python3 tools/ux/ux-measure.py            # read the new empty_plain count
```

Expect `empty_plain` to drop **18 → 10** (the 8 counted views leave the PLAIN
bucket); `empty_none` **stays 31** (unless a counted view is actually NONE — then
re-base it to the measured value). In `tools/ux/ux-baseline.json`, set
`counts.empty_plain` to **10** and append a sentence to `generated_from` recording
the R32 re-base (house format — see the R31 entry as the most recent precedent;
append cleanly inside the existing JSON string, no stray `..` at the seam, and
verify the file still parses).

## Scope rules

- **One commit:** `feat(ux): empty states batch 4 (content) — .kmb-empty on the R32 views (IMPROVE-UX R32)`.
- **Files that should change:** the R32 views you actually convert (the 8 counted
  + `Page/_PageTreeItem` only if it qualifies) + `tools/ux/ux-baseline.json`
  + the handoff note. For each new `*_title` (or body) key, the relevant
  translation JSON files (`en`/`de`/`fr`/`da` under
  `src/Kumunita.Core/Localization/Keys/`) change (additions-only).
- **Read-only:** `Groups/Index.cshtml` (the `.kmb-empty` reference),
  `tools/ux/ux-measure.py`, the R33 views, and the remaining NONE views.

## Parked follow-up (record in the handoff note, do not create a new lane)

- **Remaining PLAIN leads after R32** — the R33 scope (details/groups/guardian):
  `Announcement/Detail`, `Event/Detail`, `Posts/Detail`, `Groups/Detail`,
  `Groups/_GroupMembers`, `Groups/_GroupPostReplies`, `Guardian/Detail`,
  `Guardian/Index`, `Guardian/_GuardianEventAttendance`, `Kiosk/Index`,
  `Locale/Index`, `Community/Manage`, `Community/Translations`.
- **Remaining NONE leads** (31) — out of scope for the whole lane until a later
  batch.

## Before committing

```bash
# per-view — the 8 COUNTED R32 views must each report a .kmb-empty block.
python3 -c "
from pathlib import Path
for v in ['Document/Index','Document/Deleted','Page/Index','Inventory/List','Inventory/Detail','Bookmarks/Index','MyDrafts/Index','Notes/Index']:
    f = Path('src/Kumunita.Web/Views') / (v+'.cshtml')
    has = 'kmb-empty' in f.read_text(encoding='utf-8')
    print(('OK  ' if has else 'FAIL'), v)
"
python3 tools/ux/ux-measure.py            # empty_plain: 18 -> 10; empty_none: 31 (unchanged)
python3 tools/ux/ux-measure.py --check    # MUST pass
dotnet build                              # 0 errors
```

Confirm: all 8 counted R32 views report `.kmb-empty`; `empty_plain` dropped
**18 → 10**; `empty_none` unchanged (or honestly re-based); other categories
unchanged; `--check` passes; `dotnet build` is green; the C02 four-language
key-parity test passes if you added keys (run the C02 class via the xunit.v3
in-process runner — `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.C02_DataAsEmbeddedResourcesTests"`).
Then report: per-view before → after (PLAIN → `.kmb-empty`), which of the
two caution views (`Inventory/Detail`, `MyDrafts/Index`) you converted vs.
recorded as a justified survivor (and why), how `Page/_PageTreeItem` was handled,
the new `empty_plain` number, the `generated_from` note, whether any new
translation key was needed (and which), and the commit hash. Also confirm in the
handoff note that the remaining PLAIN + NONE leads are parked for R33 (do not
create a new lane).
