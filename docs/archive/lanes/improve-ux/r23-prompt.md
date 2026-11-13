---
id: r23-prompt
title: "Prompt: IMPROVE-UX R23 — refine the a11y heuristic (credit implicit labels) + re-baseline"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> **R23** — the resident-block a11y prerequisite. It refines the
> `tools/ux/ux-measure.py` `a11y` heuristic so a control wrapped in a
> `<label>` (implicit label association, WCAG 4.1.2) is no longer counted as
> an unlabeled lead, and re-baselines the `a11y` count so the five a11y view
> batches that follow (R24–R28) can each drive their named views to 0 against
> a *honest* floor. **Who is it for:** the next coding agent (fresh
> context). **When to read the rest:** when starting R23 (the admin/mod
> block A01–A06 is shipped; resident R01–R21 is shipped; the only remaining
> resident units are R22 (docs) and the a11y/empty-state batches R23–R33 —
> no blockers).

# Fresh-context prompt: IMPROVE-UX R23

You are continuing the Kumunita repository on `development` (HEAD
`f479c439`, A01–A06 all landed — the read lane honours `PostStatus`, the
resolve lane executes the outcome for post + grouppost targets,
`UnhidePostAsync` exists, `report.queued` routes on intake, the moderation
nav badge + the queue's friendly empty state are live). **R23** is the next
unit. The plan card (in `plan-improve-ux.md`, the R23 row):

> R23 — a11y heuristic refinement in the script (ignore `@* *@` comments,
> nested `<label>` wraps) + re-baseline.
> Read first: `tools/ux/ux-measure.py`.
> **Done when: false positives removed, count recorded.**

R23 is the **prerequisite** for R24–R28 — all five a11y view batches list
`Depends: R23`. R23 does **not** touch any view; it makes the measured
`a11y` number honest (so the batches target real leads, not false positives)
and records that honest number as the new floor.

## The state of the code (measured 2026-11-11, `development`)

- **The script** — `tools/ux/ux-measure.py`. The `a11y` count is
  `count_a11y()` (line ~66): for every `*.cshtml` under
  `src/Kumunita.Web/Views`, a `TagCollector` (an `html.parser.HTMLParser`
  subclass) feeds `strip_comments(text)` and then counts a lead for each
  non-`hidden` `input`/`select`/`textarea` that has **none** of
  `aria-label`, `aria-labelledby`, or (`id` present **and** in
  `labels_for`). `labels_for` is populated in `handle_starttag` **only**
  from a `<label for="…">` (an *explicit* association). Every `<img>`
  without an `alt` is also a lead.
- **The current number** — `a11y = 95` (the `ux-baseline.json` pin), spread
  across **40 views** (top: `AdminSite/Index` 12, `Account/Login` 5, then a
  spread of 4s — `Posts/Detail`, `Groups/PostDetail`, `Event/Detail`,
  `Profile/Edit`, `Projects/ProjectDetail`, `Groups/_GroupTranslationModals`
  — and 2s/3s below).
- **The one real gap R23 closes** — a control **wrapped inside a `<label>`
  element** (the HTML *implicit* label, e.g.
  `<label>Email <input type="email" …></label>`) is **not** credited by the
  current heuristic: it has no `aria-*` and no `for`/`id` pair, so it is
  counted as a lead even though it is correctly labeled. This is the
  "nested `<label>` wraps" refinement.
- **The "ignore `@* *@` comments" half is already done** — `strip_comments()`
  (line ~30) already removes `@\* … \*@` (Razor comments), `<!-- … -->` (HTML
  comments), and `// …` line comments before the `TagCollector` runs. Do not
  re-do it; note it in the handoff so the next agent knows it is covered.
- **The gate is only `ux-measure.py --check`** — there is **no C# test** that
  pins `a11y == 95` (confirmed: no test asserts the value; the
  `ModerationControllerTests.cs` reference is only a comment about
  `empty_plain`). CI (`.github/workflows/ci.yml:113`) runs
  `python3 tools/ux/ux-measure.py` **report-only** (no `--check`). So R23's
  re-baseline is a JSON edit, not a test edit.

## The work

1. **Credit the implicit label in `count_a11y`.** Track the `<label>`
   nesting depth in `TagCollector`: push a marker in `handle_starttag` when
   `tag == "label"`, pop in `handle_endtag` for `label`; a control is
   labeled when the existing condition holds **or** when it is seen while
   inside at least one `<label>` (implicit association). Keep the
   `aria-label` / `aria-labelledby` / `id`-in-`labels_for` conditions
   unchanged (they are correct — a control with an *explicit* label or
   `aria-*` is labeled regardless of nesting). The `<img>` / `alt` rule is
   unchanged.
   - **Guard against the one HTML gotcha:** a `<label>` that *also* carries
     a `for` pointing at a control *outside* the label (rare) still labels
     that outside control via the existing `labels_for` path; the
     nested control inside it is a separate (still legitimately labeled)
     association. You do not need to model that edge — crediting a control
     that is a descendant of a `<label>` is correct per WCAG 4.1.2 either
     way.
   - **Do not** credit a control as labeled merely because a `<label>`
     appears earlier in the file — the depth must be *open* at the moment
     the control is read (the push/pop stack gives this for free).
2. **Re-run and read the new number.** `python3 tools/ux/ux-measure.py`
   (report-only). Note the new `a11y` count (expect it to be **< 95** — the
   nested-`<label>` false positives drop out; every other category
   `bare_status` 710 / `empty_plain` 36 / `empty_none` 33 / `jargon` 0 /
   `notify_without_link` 4 is unchanged — R23 only touches `a11y`).
3. **Re-baseline.** In `tools/ux/ux-baseline.json`, set `counts.a11y` to the
   new number and append a sentence to the `generated_from` string recording
   the R23 re-base (the house format precedent is the R18 + A03 entries:
   "…Re-based 2026-11-11 for IMPROVE-UX A03 — …"). State **why** the count
   moved (implicit-`<label>` false positives removed) and the old → new value
   (`a11y 95 -> N`).
4. **Sanity-check the drop is honest.** Spot-check **one** of the views whose
   lead count dropped (e.g. a view with a `<label>Email <input …></label>`
   wrap) and confirm the control genuinely sits inside a `<label>` (a real
   implicit label), so R23 is removing a false positive and not masking a
   real unlabeled input. The R24–R28 batches will still drive the
   *remaining* true leads to 0 — R23 must not hide any.

## Scope rules

- **Only `tools/ux/ux-measure.py` (the `TagCollector` / `count_a11y` a11y
  path) + `tools/ux/ux-baseline.json` (the `a11y` re-baseline + the
  `generated_from` note).** Do **not** edit any `.cshtml` view (that is the
  R24–R28 work, and each of those batches must still see its view's *true*
  leads). Do **not** change the `<img>` / `alt` rule, the `aria-*` rule, or
  any other category (`bare_status` / `empty_plain` / `empty_none` / `jargon`
  / `notify_without_link`). Do **not** re-do the Razor-comment stripping
  (`strip_comments` already handles `@* *@`). Do **not** add or remove any
  `a11y` view leads by editing views — R23 is the *heuristic*, not the
  content.
- **One commit** (the house rule): `feat(ux): a11y heuristic credits implicit <label> wraps + re-baseline (IMPROVE-UX R23)`.

## Files that should change

- `tools/ux/ux-measure.py` — `TagCollector` (push/pop a `<label>` depth
  stack) + the `count_a11y` lead predicate (credit the implicit label).
- `tools/ux/ux-baseline.json` — `counts.a11y` → the new number; a
  `generated_from` sentence recording the R23 re-base (old → new + the why).

(2 files — the smallest possible unit; the a11y floor move is the whole
deliverable.)

## Before committing

```bash
python3 tools/ux/ux-measure.py            # read the new a11y number (report-only)
python3 tools/ux/ux-measure.py --check    # MUST pass — a11y (new) <= baseline (new); all else unchanged
```

Confirm: the new `a11y` count is **strictly less** than 95 (the nested-`<label>`
false positives dropped out); the other five categories are byte-identical to
the previous report (`bare_status` 710, `empty_plain` 36, `empty_none` 33,
`jargon` 0, `notify_without_link` 4); the `--check` gate prints "UX baseline
check passed"; and one dropped view was spot-checked as a genuine implicit
`<label>` wrap. Then report: the exact `count_a11y` change (file:line in
`ux-measure.py`, the push/pop + the credited condition), the new `a11y`
number (old → new) and the `generated_from` note, and the commit hash. Do not
commit unrelated work.
