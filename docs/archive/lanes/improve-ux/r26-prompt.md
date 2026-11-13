---
id: r26-prompt
title: "Prompt: IMPROVE-UX R26 — a11y batch 3 (label-pair the 8 named views to 0)"
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
> **R26** — a11y batch 3. It drives the a11y leads in eight named views
> (`Projects/ProjectDetail`, `Projects/ProjectEdit`, `Projects/ProjectNew`,
> `Boards/BoardNew`, `Todos/TodoDetail`, `Todos/TodosIndex`,
> `Todos/_TodoStatusAssignEvent`, `Shared/_BoardCard`) to **0** by giving each
> unlabeled control a programmatic label (WCAG 4.1.2), against the honest floor
> R23 established and R24 + R25 lowered (now **70**).
> **Who is it for:** the next coding agent (fresh context). **When to read the
> rest:** when starting R26 (R23 + R24 + R25 have landed — the `a11y` heuristic
> is final and the floor is now `70`; R26 is self-contained, and R27–R28 are the
> remaining a11y batches that share the same R23 floor).

# Fresh-context prompt: IMPROVE-UX R26

You are continuing the Kumunita repository on `development` (HEAD
`3df113b0`, R25 just landed — the five R25 batch-2 views are paired and the
`a11y` floor is re-based to **`70`**). **R26** is the next unit. The plan card
(in `plan-improve-ux.md`, the R26 row):

> R26 — a11y batch 3: `Projects/ProjectDetail`, `ProjectEdit`, `ProjectNew`,
> `Boards/BoardNew`, `Todos/TodoDetail`, `Todos/TodosIndex`,
> `Todos/_TodoStatusAssignEvent`, `Shared/_BoardCard`.
> Read first: those views.
> **Done when: their leads → 0.**

R26 is a **view-content** unit (same class as R24 and R25). You do
**not** touch the script or the baseline's other categories — you make the
eight named views genuinely pass the a11y heuristic, so the measured `a11y`
count drops by exactly the leads these eight views carry.

## The state of the code (measured 2026-11-11, `development`)

- **What a "lead" is** — `count_a11y()` in `tools/ux/ux-measure.py`. A lead is
  a non-`hidden` `input`/`select`/`textarea` that has **none** of:
  `aria-label`, `aria-labelledby`, (`id` present **and** in some `<label
  for="…">`), or an open `<label>` ancestor (R23). Every `<img>` without an
  `alt` is also a lead.
- **The leads in this batch** — 7 total, **all unlabeled controls** (no
  missing-`alt` images in this batch). Per view:
  - `Projects/ProjectDetail` **4** (two translation modals — see shape 1 below)
  - `Projects/ProjectEdit` **1** (Title — shape 2)
  - `Projects/ProjectNew` **1** (Title — shape 2)
  - `Boards/BoardNew` **1** (first lane's title — shape 3)
  - `Todos/TodoDetail` **0**, `Todos/TodosIndex` **0**,
    `Todos/_TodoStatusAssignEvent` **0**, `Shared/_BoardCard` **0**
    (read them to confirm clean, then move on; no edit expected)
- **Three distinct shapes** — same two as R25 (sibling-label-without-`for`,
  and `<div>` label not a `<label>`), plus one new case in `Boards/BoardNew`
  (a `<div>` label with a separate input that has **no `id` yet** — you add
  both `id` and `for`).

## The work

### 1. `Projects/ProjectDetail.cshtml` (4 leads) — sibling-label-without-`for`

Exactly the R25 `Event/Detail` idiom. Two translation modals (the **edit**
modal iterates `existing in Model.Translations` and the **add** modal iterates
missing languages), each with a `title` `<input name="title">` and a `body`
`<textarea name="body">`. Each sits below a `<label class="form-label">` with
**no `for`**, and the control has **no `id`**.

Fix: give each control a unique `id` and add the matching `for` to its
existing `<label>`. Suggested id pattern (mirrors the existing
`proj-trans-edit-{lang}` / `proj-trans-add-{lang}` naming):

- edit modal title: `id="proj-trans-edit-title-{existing.LanguageCode}"`
- edit modal body:  `id="proj-trans-edit-body-{existing.LanguageCode}"`
- add modal title:  `id="proj-trans-add-title-{lang.Code}"`
- add modal body:   `id="proj-trans-add-body-{lang.Code}"`

Each `<label class="form-label">` gains the matching `for`. Confirm no two
`id`s collide in the same document (the existing modal/form ids
`proj-trans-edit-{lang}` / `proj-trans-add-{lang}` are for the `<div>`/`<form>`,
not the inputs — they won't collide).

### 2. `Projects/ProjectEdit.cshtml` + `Projects/ProjectNew.cshtml` (1 lead each) — `<div>` label, not a `<label>`

Same shape as R25's `Event/Create` + `Event/Edit` Title. The `Title` `<input>`
already has `id="title"`, but its visible label is a
`<div class="form-label">` (not a `<label>`), so no `<label for="title">`
exists anywhere and the input is still flagged.

Fix: convert `<div class="form-label" …>` →
`<label class="form-label" for="title" …>`. No new string, no new `id`.
The same edit is made in both files.

### 3. `Boards/BoardNew.cshtml` (1 lead) — `<div>` label **and** no `id` on the input

The "first lane" title input (`name="Lanes[0].Title"`) has a
`<div class="form-label">Lane title</div>` label (line 148) and **no `id`**
on the input (line 149). The other lane inputs (inside the `@foreach` loop)
already have `id="lane-title-@i"` — this standalone first-lane input does not.

Fix: give the input `id="first-lane-title"` and convert the `<div>` to
`<label class="form-label" for="first-lane-title" …>`. No new string.

### 4. `Todos/TodoDetail.cshtml`, `Todos/TodosIndex.cshtml`, `Todos/_TodoStatusAssignEvent.cshtml`, `Shared/_BoardCard.cshtml` (0 each)

Read each to confirm it has 0 unlabeled controls and 0 missing-`alt`
images. No edit expected.

**Rules for the fix (same as R25):**

- Label **text stays exactly as-is** — already a `<kw-l key="…">`. You are
  adding `id` / `for` attributes, or promoting a `<div class="form-label">`
  to a real `<label for>`. **No new translation strings** in this batch.
- Keep Bootstrap classes and grid layout unchanged. Do not add `aria-label`
  as a substitute. No inline `on*` handlers or `<script>` blocks (SEC gate).
- **Only these eight views.** Do not touch `tools/ux/ux-measure.py` or any
  other `counts.*`. Do not touch the R27/R28 views.

## The re-baseline

After all eight views are at 0:

```bash
python3 tools/ux/ux-measure.py            # read the new a11y count
```

Expect `a11y` to drop from **70 to 63** (7 leads leave; every other view's
leads remain). In `tools/ux/ux-baseline.json`, set `counts.a11y` to the new
number and append a sentence to the `generated_from` string recording the R26
re-base (house format — see the R25 entry as the most recent precedent).

## Scope rules

- **One commit:** `feat(ux): a11y batch 3 — label-pair the eight R26 views to 0 + re-baseline (IMPROVE-UX R26)`.
- **Files that should change:** `Projects/ProjectDetail.cshtml`,
  `Projects/ProjectEdit.cshtml`, `Projects/ProjectNew.cshtml`,
  `Boards/BoardNew.cshtml` (the four that carry edits) +
  `tools/ux/ux-baseline.json`. The four `Todos/*` + `_BoardCard` views are
  read-only.

## Before committing

```bash
# per-view — each of the eight named views must report 0 unlabeled + 0 img-missing-alt
python3 -c "
import importlib.util
spec = importlib.util.spec_from_file_location('uxm','tools/ux/ux-measure.py')
m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
def measure(name):
    f = m.ROOT/'src/Kumunita.Web/Views'/name
    col = m.TagCollector(); col.feed(m.strip_comments(f.read_text()))
    bad = [c for c in col.controls if c.get('type','').lower()!='hidden'
           and not (c.get('aria-label') or c.get('aria-labelledby') or (c.get('id') and c['id'] in col.labels_for) or int(c.get('_label_depth','0'))>0)]
    return len(bad), sum(1 for i in col.images if 'alt' not in i)
for v in ['Projects/ProjectDetail.cshtml','Projects/ProjectEdit.cshtml','Projects/ProjectNew.cshtml',
          'Boards/BoardNew.cshtml','Todos/TodoDetail.cshtml','Todos/TodosIndex.cshtml',
          'Todos/_TodoStatusAssignEvent.cshtml','Shared/_BoardCard.cshtml']:
    print(v, measure(v))
"
python3 tools/ux/ux-measure.py            # total a11y dropped by exactly 7
python3 tools/ux/ux-measure.py --check    # MUST pass
```

Confirm: each of the eight named views reports **0** leads; total `a11y`
dropped by exactly 7 (70 → 63); other categories unchanged; `--check` passes;
`dotnet build` is green. Then report: per-view before → after, the exact
`id`/`for` pairings (file:line), the new `a11y` number, the `generated_from`
note, whether any new translation key was needed (expected: none), and the
commit hash.
