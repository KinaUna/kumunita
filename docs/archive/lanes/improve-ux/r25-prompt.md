---
id: r25-prompt
title: "Prompt: IMPROVE-UX R25 — a11y batch 2 (label-pair the 5 named views to 0)"
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
> **R25** — a11y batch 2. It drives the a11y leads in five named views
> (`Event/Detail`, `Event/Create`, `Event/Edit`, `Announcement/Detail`,
> `Announcement/Index`) to **0** by giving each unlabeled control a programmatic
> label (WCAG 4.1.2), against the honest floor R23 established and R24 lowered.
> **Who is it for:** the next coding agent (fresh context). **When to read the
> rest:** when starting R25 (R23 + R24 have landed — the `a11y` heuristic is
> final and the floor is now `80`; R25 is self-contained, and R26–R28 are the
> remaining a11y batches that share the same R23 floor).

# Fresh-context prompt: IMPROVE-UX R25

You are continuing the Kumunita repository on `development` (HEAD
`82df39ba`, R24 just landed — the six R24 batch-1 views are paired and the
`a11y` floor is re-based to **`80`**). **R25** is the next unit. The plan card
(in `plan-improve-ux.md`, the R25 row):

> R25 — a11y batch 2: `Event/Detail`, `Event/Create`, `Event/Edit`,
> `Announcement/Detail`, `Announcement/Index`.
> Read first: those views.
> **Done when: their leads → 0.**

R25 is a **view-content** unit (same class as R24, not the R23 heuristic
change). You do **not** touch the script or the baseline's other categories —
you make the five named views genuinely pass the a11y heuristic, so the
measured `a11y` count drops by exactly the leads these five views carry.

## The state of the code (measured 2026-11-11, `development`)

- **What a "lead" is** — `count_a11y()` in `tools/ux/ux-measure.py`. A lead is
  a non-`hidden` `input`/`select`/`textarea` that has **none** of:
  `aria-label`, `aria-labelledby`, (`id` present **and** in some `<label
  for="…">`), or an open `<label>` ancestor (R23). Every `<img>` without an
  `alt` is also a lead.
- **The leads in this batch** — 10 total, **all unlabeled controls** (no
  missing-`alt` images in this batch). Per view: `Event/Detail` **4**,
  `Event/Create` **3**, `Event/Edit` **3**, `Announcement/Detail` **0**,
  `Announcement/Index` **0** (those two are already clean — read them to
  confirm, then move on; no edit expected).
- **Unlike R24, the leads here are NOT all the same shape** — there are three
  distinct cases, and the fix differs for each. Get this right or you will
  "fix" a control with an `aria-label` to dodge the real fix.
  1. **`Event/Detail` (4) — sibling-label-without-`for`** (exactly the R24
     idiom). Two translation modals, each with a `title` `<input>` and a
     `body` `<textarea>`; each sits below a `<label class="form-label">` that
     has **no `for`** and the control has **no `id`**. Fix: pair `id` + `for`,
     unique per control (the two modals' controls share `name`s).
  2. **`Event/Create` + `Event/Edit` `Title` (1 each) — a `<div>` label, not a
     `<label>`.** The `Title` `<input>` already has `id="title"`, but its
     visible label is a `<div class="form-label">` (not a `<label>`), so there
     is **no** `<label for="title">` anywhere and it is still flagged. Fix:
     make that label a real association — convert the
     `<div class="form-label">…` to `<label class="form-label" for="title">…`
     (or add a matching `<label for="title">`). No new string.
  3. **`Event/Create` + `Event/Edit` `EndsAfterCount` + `EndsOnDate` (1 each)**
     — **label-less** controls. Each is a bare `<input>` (`type="number"`
     `min="2"`, and `type="date"`) whose only nearby text is a muted `<span>`
     hint ("occurrences" / "until") and an adjacent radio's `form-check-label`
     ("Ends after" / "Ends on"). There is **no `<label>`** for the input. The
     hint text already exists as keys — `events.recurrence.count` and
     `events.recurrence.until` — so the fix is to **associate the input with
     its existing visible text**, not to invent a string: give the input an
     `id` and either convert its hint `<span>` to a `<label class="form-label"
     for="…">` (inline, minimal visual change) or add a matching `<label>`.

## The work

For each of the five views, remove its leads. Work view-by-view; after each,
re-run the per-view measure (see the snippet in *Before committing*) to confirm
that view is at 0 before moving on.

1. **`Event/Detail` (4)** — the two translation modals' `title` + `body`
   controls. Give each control a unique `id` (e.g.
   `event-trans-title-{lang}` / `event-trans-body-{lang}`, `-{lang}` from the
   edit modal's `existing.LanguageCode` and the add modal's `lang.Code`) and
   add the matching `for` to each existing `<label>`. Confirm no two `id`s
   collide in the same document.
2. **`Event/Create` (3)** — (a) the `Title` `<div class="form-label">` → a real
   `<label class="form-label" for="title">` (the input's `id="title"` is
   already there); (b) the `EndsAfterCount` number input → pair it with its
   "occurrences" hint (`events.recurrence.count`); (c) the `EndsOnDate` date
   input → pair it with its "until" hint (`events.recurrence.until`).
3. **`Event/Edit` (3)** — same three shapes as `Event/Create` (Title,
   `EndsAfterCount`, `EndsOnDate`). Mirror the exact same fix so the two
   stay consistent.
4. **`Announcement/Detail` (0)** — verify clean; no edit expected.
5. **`Announcement/Index` (0)** — verify clean; no edit expected.

**Rules for the fix:**

- The label **text stays exactly as-is** — it is already a localized `<kw-l
  key="…">` (or an existing hint `<span>`). You are adding `id` / `for`
  attributes, or promoting an existing `<div class="form-label">` / hint
  `<span>` to a real `<label for>`. **No new translation strings** are
  introduced by this batch (every accessible name reuses an existing visible
  label or hint). If you hit a control with truly no text anywhere near it,
  then and only then add a new key in **en/de/fr/da** (de/fr/da
  machine-written, flagged for the parked native review) — call it out in the
  report.
- Keep the Bootstrap markup/classes and the grid layout unchanged apart from
  the added `id` / `for` (or the `<div>`/`<span>` → `<label>` promotion). Do
  not restructure the surrounding `<div>`/`<form>` nesting, do not add
  `aria-label` to hide a real fix, and do not add inline `on*` handlers or
  `<script>` blocks (the SEC gate forbids both — see `AGENTS.md` § Conventions).
- **Only these five views.** Do **not** touch the other a11y-batch views
  (`Projects/*`, `Boards/*`, `Todos/*`, `Document/*`, `Directory/*`,
  `Profile/*`, `Guardian/*`, `Community/*`, `Notes/*`, `SecuritySettings/*`,
  etc. — those are R26–R28). Do **not** touch `tools/ux/ux-measure.py` or the
  `counts` in `tools/ux/ux-baseline.json` other than the `a11y` re-baseline
  below — R23 owns the heuristic.

## The re-baseline

After all five views are at 0, re-run and read the new `a11y` number:

```bash
python3 tools/ux/ux-measure.py            # report-only — read the new a11y count
```

Expect `a11y` to drop from **80 to 70** (the 10 leads in these five views
leave; every other view's leads remain). In `tools/ux/ux-baseline.json`, set
`counts.a11y` to the new number and append a sentence to the `generated_from`
string recording the R25 re-base (house format precedent: the R18 / A03 / R23 /
R24 entries — "…Re-based 2026-11-11 for IMPROVE-UX R25 — a11y 80 -> 70: the
five R25 batch-2 views (Event/Detail, Event/Create, Event/Edit,
Announcement/Detail, Announcement/Index) now carry an explicit label
association — the two Event/Detail translation modals' title/body pair each
control with `<label for>`/`id`, the Event Create/Edit `Title` `<div>` label
becomes a real `<label for="title">`, and the `EndsAfterCount`/`EndsOnDate`
inputs are paired with their existing 'occurrences'/'until' hint text; the two
Announcement views were already clean (read-only).").

## Scope rules

- **One commit** (the house rule): `feat(ux): a11y batch 2 — label-pair the
  five R25 views to 0 + re-baseline (IMPROVE-UX R25)`.
- **Files that should change:** `Event/Detail.cshtml`, `Event/Create.cshtml`,
  `Event/Edit.cshtml` (the three that carry edits) + `tools/ux/ux-baseline.json`
  (`counts.a11y` → new number + `generated_from` note). `Announcement/Detail`
  and `Announcement/Index` are expected to be read-only. Translation JSON only
  if a control truly had no nearby text (rare — call it out in the report).
- **Do not** edit `tools/ux/ux-measure.py`, do not change any other `counts.*`,
  do not touch other views, do not add `aria-label` as a substitute for a real
  `for`/`id` (or a real `<label>`), do not commit unrelated work.

## Before committing

Verify each of the five named views is at 0 (per-view, not just the total):

```bash
python3 -c "
import importlib.util
spec = importlib.util.spec_from_file_location('uxm','tools/ux/ux-measure.py')
m = importlib.util.module_from_spec(spec); spec.loader.exec_module(m)
for name in ['Event/Detail.cshtml','Event/Create.cshtml','Event/Edit.cshtml','Announcement/Detail.cshtml','Announcement/Index.cshtml']:
    f = m.ROOT/'src/Kumunita.Web/Views'/name
    c = m.TagCollector(); c.feed(m.strip_comments(f.read_text()))
    bad = [c for c in c.controls if c.get('type','').lower()!='hidden'
           and not (c.get('aria-label') or c.get('aria-labelledby') or (c.get('id') and c['id'] in c.labels_for) or int(c.get('_label_depth','0'))>0)]
    imgs = sum(1 for i in c.images if 'alt' not in i)
    print(name, 'unlabeled:', len(bad), 'img-missing-alt:', imgs)
"
python3 tools/ux/ux-measure.py            # the five named views are each at 0
python3 tools/ux/ux-measure.py --check    # MUST pass — a11y (new) <= baseline (new); all else unchanged
```

Confirm: each of the five named views now reports **0** leads; the total
`a11y` dropped by exactly the sum of their prior leads (80 → 70, or 80 → N if a
view had more/fewer than the spot-count above — report the real number); the
other five categories are unchanged (`bare_status` 710, `empty_plain` 36,
`empty_none` 33, `jargon` 0, `notify_without_link` 4); the `--check` gate
prints "UX baseline check passed"; and the `dotnet build` is still green (Razor
views compile). Then report: the per-view before → after, the exact `id`/`for`
( or `<div>`/`<span>` → `<label>` promotion) pairing you added per view
(file:line), the new `a11y` number (old → new) and the `generated_from` note,
whether any new translation key was needed, and the commit hash.
