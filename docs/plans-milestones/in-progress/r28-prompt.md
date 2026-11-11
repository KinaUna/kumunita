---
id: r28-prompt
title: "Prompt: IMPROVE-UX R28 — a11y batch 5 (label-pair the 13 resident views to 0; record the 2 dynamic partials as survivors)"
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
> **R28** — a11y batch 5, the *last* a11y batch. It drives the **27
> straightforward resident a11y leads** (13 named views) to **0**, and records
> the **2 dynamic shared partials** (`Shared/_GrantPickers` /
> `Shared/_NoteButton`) as **justified survivors** in the baseline, against the
> floor R27 left at **56**. It is deliberately *not* a "total a11y → 0" unit:
> the remaining **27** leads live in 9 admin/operator views that are outside
> this batch's "resident" scope and are parked for a follow-up. R28 is
> self-contained and is the last a11y batch in the lane.
> **Who is it for:** the next coding agent (fresh context). **When to read the
> rest:** when starting R28 (R27 has just landed — the `a11y` floor is now
> `56`, and R27's `Email` `asp-for`→`for` fix is the one idiom to internalize
> first).

# Fresh-context prompt: IMPROVE-UX R28

You are continuing the Kumunita repository on `development` (HEAD `f489a81b`,
R27 just landed — the eight R27 batch-4 views are paired and the `a11y` floor
is re-based to **`56`**). **R28** is the next and **last** a11y batch. The
plan card (in `plan-improve-ux.md`, the R28 row):

> R28 — a11y batch 5 (remaining resident): `Community/Manage`,
> `Community/Translations`, `Locale/DateFormat`, `Notes/NoteRemoveTag`,
> `SecuritySettings/Mfa`, `Shared/_AccountNav`, `Shared/_GrantPickers`,
> `Shared/_NoteButton`, `Account/Portability/*`, `Languages/Translations`.
> **Done when: resident leads → 0.**

Two things about that card you must not take literally — the rest of this
prompt exists because of them:

1. **The card's view list is stale.** R23 refined the heuristic and R24–R27
   drove six batches to 0, so several views in the R28 list (`Notes/NoteRemoveTag`,
   `SecuritySettings/Mfa`, `Shared/_AccountNav`) are **already 0** — reading
   them to confirm is correct, editing them is not. The *real* set of resident
   views that still carry leads is the 15 measured below.
2. **This is not a "total a11y → 0" unit.** R28's DoD is "resident leads → 0."
   The 27 admin/operator-view leads (e.g. `AdminSite/Index` alone carries 12
   hero-textarea leads) are **outside this batch's scope** and are parked for
   a follow-up. Chasing them is out of scope for this unit.

## The state of the code (measured 2026-11-11, `development`, HEAD `f489a81b`)

- **What a "lead" is** — `count_a11y()` in `tools/ux/ux-measure.py`. A lead is
  a non-`hidden` `input`/`select`/`textarea` that has **none** of:
  `aria-label`, `aria-labelledby`, (`id` present **and** in some `<label
  for="…">`), or an open `<label>` ancestor (R23). Every `<img>` without an
  `alt` is also a lead.
- **The 56 leads, split by audience and shape:**

  | Group | Views | Leads |
  |---|---|---|
  | Resident — straightforward (the 13 you fix) | 13 | 27 |
  | Resident — dynamic partials (record as survivors) | 2 | 2 |
  | Admin/operator — **out of scope** | 9 | 27 |
  | **Total** | **24** | **56** |

  **The 13 straightforward resident views (27 leads), in three micro-shapes:**

  - **Shape A — `asp-for` inputs whose label uses `asp-for="X"` (20 leads, 7 views).**
    `Account/Login` (5), `Account/Signup` (4), `Groups/Create` (3: `Name`,
    `Description`, `IsPrivate`), `Guardian/AddChild` (3), `Account/Delete` (2),
    `Account/Verify` (2), `Account/ResendVerification` (1). The input is
    `<input asp-for="X">` and its label is `<label asp-for="X">`. At runtime
    both are correctly associated, but the static heuristic only credits a
    **literal** `for` on the label — not `asp-for="X"` — so it flags the input.
    This is exactly the R27 `Email` shape. **Fix (the R27 idiom, two changes
    per control):** add the explicit `id="X"` to the input **and** convert the
    label `asp-for="X"` → `for="X"`. Both render identically; together they make
    the association visible in source. Do **not** use `aria-label` here — the
    real `<label for>` is the correct mechanism.
  - **Shape B — file inputs with an `id` but no matching `<label>` (3 leads, 3 views).**
    `Account/Portability/Index` (1, `id="archive"`),
    `Account/Portability/ResolveReview` (1, `id="archive"`),
    `Languages/Translations` (1, `name="bundle"`, **no `id`** — add one). These
    have only a heading/description and no `<label>`. This is the R27
    `Document/Index` shape. **Fix:** add a **visually-hidden**
    `<label for="…">` that reuses an existing `<kw-l>` key already on the page
    (the import/export description text is a natural source — do **not** invent
    a new translation string). Keep it inside the existing `<form>`.
  - **Shape C — inputs with an `id`/`name` but no `for` on their label (4 leads, 3 views).**
    `Community/Translations` (2: `name`, `description`), `Community/Manage` (1,
    `id="addUser"`), `Locale/DateFormat` (1, `id="customFormat"`). The label
    exists but has no `for` (or the `for` points at a different field, as with
    `Locale/DateFormat`). This is the R24/R25 sibling-label idiom. **Fix:**
    give each input a unique `id` (if missing) and put the matching `for` on
    its existing `<label>`; if a control has no `<label>` at all, add a
    visually-hidden one reusing an existing key. Verify each view's actual
    markup before editing.

  **The 2 dynamic partials (2 leads) — record as justified survivors, do not edit.**
  `Shared/_GrantPickers` (1) and `Shared/_NoteButton` (1) build their `id`
  **and** `for` via **Razor string concatenation** (e.g. `allId =
  Model.EditorName + "-grants-" + kind.ToLowerInvariant()`, then
  `id="…allId…"` and `for="…allId…"`; `note-modal-textarea-{inst}` likewise).
  In source the id is `@($…)` — a Razor expression — so the static heuristic can
  neither resolve it nor match it to a `for`. **At runtime they are correctly
  associated.** This is a heuristic limitation, not a markup bug, and the
  `aria-label` dodge is the **wrong** fix (a real `for`/`id` pair already
  exists at runtime). **Resolution:** record both as **justified survivors** in
  `ux-baseline.json` — append a sentence to `generated_from` (house format)
  stating that these two partials build `id`/`for` via Razor concatenation and
  are correctly associated at runtime, and set `counts.a11y` to the number you
  land at. This matches the R21 `jargon` "each survivor justified in baseline"
  precedent. (A heuristic refinement to credit Razor-concatenated id/for pairs
  is possible but riskier — it would touch the stable `count_a11y()` ratchet —
  so default to the survivor route unless you are comfortable re-testing all 24
  other flagged views.)

## The work

Work view-by-view; after each, re-run the per-view measure (see *Before
committing*) to confirm that view is at 0 before moving on.

**For each of the 13 straightforward views:** read the view, confirm the
flagged controls, apply the fix for its shape (A/B/C above), and re-measure.
Rules (same as R24–R27):

- Label **text stays exactly as-is** — it is already a `<kw-l key="…">`. You
  are adding `id`/`for` attributes, or (Shape B) adding one
  `visually-hidden` `<label>` that reuses an existing key. **No new
  translation strings** in this batch.
- Do **not** use `aria-label` as a substitute for a real `for`/`id` (or a real
  `<label>`). Do **not** introduce a duplicate `id` (the `GuardianConsent` trap
  in R27 is the warning: a second control can already claim an id in the same
  file).
- Keep Bootstrap classes and the grid layout unchanged (the `visually-hidden`
  label is the only added element). No inline `on*` handlers, no `<script>`
  blocks (SEC gate).
- **Only the 15 resident views.** Do **not** touch the 9 admin-view leads.
  Do **not** edit `Shared/_GrantPickers` / `Shared/_NoteButton` — they are
  recorded as survivors, not changed.

## The re-baseline

After all views are handled:

```bash
python3 tools/ux/ux-measure.py            # read the new a11y count
```

Expect `a11y` to drop from **56** to **29** (the 27 straightforward-view leads
leave; the 2 dynamic partials remain as justified survivors). In
`tools/ux/ux-baseline.json`, set `counts.a11y` to the new number and append a
sentence to the `generated_from` string recording the R28 re-base **and** the
2 justified survivors (house format — see the R27 entry as the most recent
precedent). When appending, watch the seam: a `+=` concatenation can leave a
stray `..` at the previous note's boundary (this happened on R26) — check and
clean it.

## Scope rules

- **One commit:** `feat(ux): a11y batch 5 — label-pair the 13 resident R28 views to 0 + record the 2 dynamic partials as survivors + re-baseline (IMPROVE-UX R28)`.
- **Files that should change:** the 13 straightforward resident views
  (`Account/Login`, `Account/Signup`, `Account/Delete`, `Account/Verify`,
  `Account/ResendVerification`, `Groups/Create`, `Guardian/AddChild`,
  `Account/Portability/Index`, `Account/Portability/ResolveReview`,
  `Languages/Translations`, `Community/Translations`, `Community/Manage`,
  `Locale/DateFormat`) + `tools/ux/ux-baseline.json`.
- **Read-only:** `Shared/_GrantPickers`, `Shared/_NoteButton` (survivors), and
  the 9 admin-view leads (out of scope, parked for a follow-up).

## Before committing

```bash
# per-view — the 13 straightforward resident views must each report 0; the 2
# dynamic partials stay at 1 each (recorded as survivors).
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
straight = ['Account/Delete.cshtml','Account/Login.cshtml',
            'Account/Portability/Index.cshtml','Account/Portability/ResolveReview.cshtml',
            'Account/ResendVerification.cshtml','Account/Signup.cshtml',
            'Account/Verify.cshtml','Community/Manage.cshtml',
            'Community/Translations.cshtml','Groups/Create.cshtml',
            'Guardian/AddChild.cshtml','Languages/Translations.cshtml',
            'Locale/DateFormat.cshtml']
partials = ['Shared/_GrantPickers.cshtml','Shared/_NoteButton.cshtml']
for v in straight:
    b, a = measure(v); print(('OK ' if (b,a)==(0,0) else 'FAIL'), v, (b,a))
for v in partials:
    b, a = measure(v); print('SURVIVOR', v, (b,a))
"
python3 tools/ux/ux-measure.py            # total a11y: 56 -> 29
python3 tools/ux/ux-measure.py --check    # MUST pass
```

Confirm: each of the 13 straightforward resident views reports **0**; the 2
dynamic partials remain at 1 each and are recorded as justified survivors in
the baseline; the 27 admin-view leads are unchanged; total `a11y` dropped
56 → 29; other categories unchanged; `--check` passes; `dotnet build` is green.
Then report: per-view before → after, the exact `id`/`for` pairings (file:line),
the new `a11y` number, the `generated_from` note (including the survivor
justification), whether any new translation key was needed (expected: none),
and the commit hash. Also confirm in the handoff note that the 27 admin-view
leads are parked for a follow-up (do not create a new lane).
