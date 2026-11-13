---
id: r24-prompt
title: "Prompt: IMPROVE-UX R24 — a11y batch 1 (label-pair the 6 named views to 0)"
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
> **R24** — a11y batch 1. It drives the a11y leads in six named views
> (`Posts/Detail`, `Posts/Index`, `Shared/_PostReply`, `Groups/PostDetail`,
> `Groups/_GroupPostReplies`, `Groups/_GroupTranslationModals`) to **0** by
> giving each unlabeled control a programmatic label (WCAG 4.1.2), against the
> honest floor R23 just established. **Who is it for:** the next coding agent
> (fresh context). **When to read the rest:** when starting R24 (R23 has
> landed — the `a11y` heuristic now credits implicit `<label>` wraps and the
> count is honest; R25–R28 are the remaining a11y batches and depend on the
> same R23 floor, so this batch is self-contained).

# Fresh-context prompt: IMPROVE-UX R24

You are continuing the Kumunita repository on `development` (HEAD
`95bd405d`, R23 just landed — `tools/ux/ux-measure.py`'s `TagCollector` now
credits a control wrapped in an open `<label>` as labeled, and the `a11y`
floor is re-based to an **honest** `95`). **R24** is the next unit. The plan
card (in `plan-improve-ux.md`, the R24 row):

> R24 — a11y batch 1: `Posts/Detail`, `Posts/Index`, `Shared/_PostReply`,
> `Groups/PostDetail`, `Groups/_GroupPostReplies`,
> `Groups/_GroupTranslationModals`.
> Read first: those views.
> **Done when: their leads → 0.**

R24 is a **view-content** unit (the opposite of R23, which was the heuristic).
You do **not** touch the script or the baseline's other categories — you make
the six named views genuinely pass the a11y heuristic, so the measured `a11y`
count drops by exactly the leads these six views carry.

## The state of the code (measured 2026-11-11, `development`)

- **What a "lead" is** — `count_a11y()` in `tools/ux/ux-measure.py`. A lead is
  a non-`hidden` `input`/`select`/`textarea` that has **none** of:
  `aria-label`, `aria-labelledby`, (`id` present **and** in some `<label
  for="…">`), or an open `<label>` ancestor (R23). Every `<img>` without an
  `alt` is also a lead.
- **The leads in this batch** — 15 total, **all unlabeled controls** (no
  missing-`alt` images in this batch). Per view: `Posts/Detail` 4,
  `Groups/PostDetail` 4, `Groups/_GroupTranslationModals` 4,
  `Groups/_GroupPostReplies` 2, `Shared/_PostReply` 1, `Posts/Index` **0**
  (already clean — read it to confirm, then move on).
- **The shape of every lead** — a control sitting **as a sibling** below a
  `<label>` that has **no `for`** and a control with **no `id`** (the
  Bootstrap idiom: `<div class="mb-3"><label class="form-label">…</label>
  <input … name="…"/></div>`). The label *text* already exists (it is a
  `<kw-l key="…">`), but there is no programmatic association, so a screen
  reader announces the input with no accessible name. This is the WCAG 4.1.2
  (and 1.3.1) gap.
- **The fix the heuristic accepts** — the R23 `TagCollector` credits a control
  as labeled when it has `aria-label`, `aria-labelledby`, an `id` that some
  `<label for="…">` targets, **or** an open `<label>` ancestor. The
  cleanest, most consistent fix for the sibling idiom already in these views
  is to **pair** the existing label and control: give the control an `id` and
  add the matching `for` to its existing `<label>`. That is a real explicit
  association (correct per WCAG) and is exactly what the heuristic credits.

## The work

For each of the six views, remove its leads. Work view-by-view; after each,
re-run the measure to confirm that view is at 0 before moving on.

1. **`Shared/_PostReply` (1 lead)** — the reply `<textarea name="body">`.
   Give it an `id` (e.g. `reply-body`) and add `for="reply-body"` to the
   existing `<label>`.
2. **`Posts/Detail` (4 leads)** — two translation modals, each with a
   `title` `<input>` and a `body` `<textarea>`. The two modals' controls
   share `name`s, so the `id`s must be **unique per control** (e.g.
   `post-trans-title-{lang}` / `post-trans-body-{lang}`) and each `<label>`
   gets the matching `for`. Re-check that no two `id`s collide in the same
   document.
3. **`Groups/PostDetail` (4 leads)** — same shape as `Posts/Detail` (two
   translation modals, `title` + `body` each). Unique `id`s per control,
   paired `for` on each `<label>`.
4. **`Groups/_GroupTranslationModals` (4 leads)** — two group-translation
   modals with `name` + `description` fields (a `name` `<input>` and a
   `description` `<textarea>` each). Same pairing; unique `id`s.
5. **`Groups/_GroupPostReplies` (2 leads)** — the reply textarea(s)
   (`name="body"`). Pair `id` + `for` on each; if two appear in one document,
   give them distinct `id`s.
6. **`Posts/Index` (0 leads)** — verify it is clean; no edit expected.

**Rules for the fix:**

- The label **text stays exactly as-is** — it is already a localized `<kw-l
  key="…">`. You are only adding `id` / `for` attributes. **No new
  translation strings** are introduced by this batch (the accessible names
  reuse the existing visible labels). If you encounter a control that has
  *no* label element at all (a bare input with no `<label>`), then and only
  then add one, reusing an existing key where the label already exists
  elsewhere, otherwise a new key in **en/de/fr/da** (de/fr/da
  machine-written, flagged for the parked native review).
- Keep the Bootstrap markup/classes unchanged apart from the added `id` /
  `for`. Do not restructure the `<div>`/`<label>`/`<input>` nesting, do not
  add `aria-label` to hide a real fix, and do not add inline `on*` handlers
  or `<script>` blocks (the SEC gate forbids both — see
  `AGENTS.md` § Conventions).
- **Only these six views** (plus translation JSON if you genuinely must add a
  key). Do **not** touch the other a11y-batch views (`Event/*`, `Announcement/*`,
  `Projects/*`, `Boards/*`, `Todos/*`, `Document/*`, `Directory/*`,
  `Profile/*`, `Guardian/*`, etc. — those are R25–R28). Do **not** touch
  `tools/ux/ux-measure.py` or the `counts` in `tools/ux/ux-baseline.json`
  other than the `a11y` re-baseline below — R23 owns the heuristic.

## The re-baseline

After all six views are at 0, re-run and read the new `a11y` number:

```bash
python3 tools/ux/ux-measure.py            # report-only — read the new a11y count
```

Expect `a11y` to drop from **95 to 80** (the 15 leads in these six views
leave; every other view's leads remain). In `tools/ux/ux-baseline.json`, set
`counts.a11y` to the new number and append a sentence to the `generated_from`
string recording the R24 re-base (house format precedent: the R18 / A03 / R23
entries — "…Re-based 2026-11-11 for IMPROVE-UX R24 — a11y 95 -> 80: the six
R24 batch 1 views (Posts/Detail, Posts/Index, Shared/_PostReply,
Groups/PostDetail, Groups/_GroupPostReplies, Groups/_GroupTranslationModals)
now pair each control with an explicit `<label for>`/`id` …").

## Scope rules

- **One commit** (the house rule): `feat(ux): a11y batch 1 — label-pair the
  six R24 views to 0 + re-baseline (IMPROVE-UX R24)`.
- **Files that should change:** the six named `.cshtml` views (five carry
  edits; `Posts/Index` is likely read-only) + `tools/ux/ux-baseline.json`
  (`counts.a11y` → new number + `generated_from` note). Translation JSON only
  if a control lacked a label entirely (rare — call it out in the report).
- **Do not** edit `tools/ux/ux-measure.py`, do not change any other
  `counts.*`, do not touch other views, do not add `aria-label` as a
  substitute for a real `for`/`id` pair, do not commit unrelated work.

## Before committing

```bash
python3 tools/ux/ux-measure.py            # the six named views are each at 0
python3 tools/ux/ux-measure.py --check    # MUST pass — a11y (new) <= baseline (new); all else unchanged
```

Confirm: each of the six named views now reports **0** leads (verify with the
per-view measure, not just the total); the total `a11y` dropped by exactly the
sum of their prior leads (95 → 80, or 95 → N if a view had more/fewer than the
spot-count above — report the real number); the other five categories are
unchanged (`bare_status` 710, `empty_plain` 36, `empty_none` 33, `jargon` 0,
`notify_without_link` 4); the `--check` gate prints "UX baseline check
passed"; and the `dotnet build` is still green (Razor views compile). Then
report: the per-view before → after, the exact `id`/`for` pairing you added per
view (file:line), the new `a11y` number (old → new) and the `generated_from`
note, whether any new translation key was needed, and the commit hash.
