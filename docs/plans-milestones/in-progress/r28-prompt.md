---
id: r28-prompt
title: "Prompt: IMPROVE-UX R28 — a11y batch 5 (label-pair the 10 R28 resident views to 0; park the residual 7 + admin 9)"
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
> **R28** — a11y batch 5, the *last* a11y batch named in the plan. It drives
> the a11y leads in the **10 named R28 views** (11 files) to **0** — 9 leads
> across 8 views (3 of the 10 are already clean) — by giving each unlabeled
> control a programmatic label (WCAG 4.1.2), against the floor R27 left at
> **56**. The 2 dynamic shared partials in the card
> (`Shared/_GrantPickers`, `Shared/_NoteButton`) are correctly associated at
> runtime (Razor-concatenated id/for) and are recorded as **justified
> survivors** in the baseline, not edited. After R28, the measured `a11y`
> floor drops to **49**. The 7 residual resident views (20 leads) and the 9
> admin views (27 leads) are **out of scope** for R28 and are parked in the
> handoff note as a follow-up. R28 is self-contained.
> **Who is it for:** the next coding agent (fresh context). **When to read the
> rest:** when starting R28 (R27 has just landed — the `a11y` floor is now
> `56`; the R27 `Email` `asp-for`→`for` idiom is the one to internalize first).

# Fresh-context prompt: IMPROVE-UX R28

You are continuing the Kumunita repository on `development` (HEAD `f489a81b`,
R27 just landed — the eight R27 batch-4 views are paired and the `a11y` floor
is re-based to **`56`**). **R28** is the next unit. The plan card (in
`plan-improve-ux.md`, the R28 row):

> R28 — a11y batch 5 (remaining resident): `Community/Manage`,
> `Community/Translations`, `Locale/DateFormat`, `Notes/NoteRemoveTag`,
> `SecuritySettings/Mfa`, `Shared/_AccountNav`, `Shared/_GrantPickers`,
> `Shared/_NoteButton`, `Account/Portability/*`, `Languages/Translations`.
> Read first: those views.
> **Done when: their leads → 0.**

R28 is a **view-content** unit (same class as R24–R27). You do **not** touch
the script (`tools/ux/ux-measure.py`) or the baseline's other categories —
you make the 10 named views genuinely pass the a11y heuristic (or, for the 2
dynamic partials, justify them as survivors), so the measured `a11y` count
drops by exactly the leads these views carry.

## The state of the code (measured 2026-11-11, `development`, HEAD `f489a81b`)

- **What a "lead" is** — `count_a11y()` in `tools/ux/ux-measure.py`. A lead is
  a non-`hidden` `input`/`select`/`textarea` that has **none** of:
  `aria-label`, `aria-labelledby`, (`id` present **and** in some `<label
  for="…">`), or an open `<label>` ancestor (R23). Every `<img>` without an
  `alt` is also a lead.
- **The 56 total leads, split by scope:**

  | Scope | Views | Leads | In R28? |
  |---|---|---|---|
  | R28 card — to fix | 6 | 7 | **Yes** |
  | R28 card — already clean | 3 | 0 | read-only |
  | R28 card — dynamic partials | 2 | 2 | **Yes — survivors** |
  | Residual resident (not in card) | 7 | 20 | **No — parked** |
  | Admin/operator | 9 | 27 | **No — parked** |
  | **Total** | **29** | **56** | |

  R28's scope is the **10 named views** (11 files, since
  `Account/Portability/*` = 2 files). After R28, the floor drops **56 → 49**
  (7 fix-target leads leave; the 2 partial leads remain as justified
  survivors). The 49 remaining leads are:
  2 (the partials, justified survivors) + 20 (residual resident) + 27 (admin).

## The work

Work view-by-view; after each, re-run the per-view measure (see *Before
committing*) to confirm that view is at 0 before moving on.

### Shape A — `asp-for` inputs whose label uses `asp-for="X"` (4 leads, 2 views)

`Community/Manage` (1: `addUser`) and `Community/Translations` (2: `name`,
`description`).

Each input is `<input asp-for="X">` or `<input name="X">` with a sibling
`<label asp-for="X">` or `<label for="X">` that does **not** match. At
runtime the association may or may not be correct — verify each case. The
static heuristic only credits a **literal `for`** on the label that matches a
**literal `id`** on the control.

**Fix (the R27 idiom):** for each flagged control, (a) ensure the input has
an explicit `id="X"` (add if missing), and (b) ensure the label has a literal
`for="X"` (convert `asp-for="X"` → `for="X"` if needed). Do **not** use
`aria-label` here — the real `<label for>` is the correct mechanism. Do
**not** introduce a duplicate `id` (check the same file for a second control
claiming the same id — the `GuardianConsent` trap in R27 is the warning).

### Shape B — inputs with an `id` but no matching `<label for>` (2 leads, 2 views)

`Locale/DateFormat` (1, `id="customFormat"`) and `Languages/Translations` (1,
`name="bundle"` file input, **no `id`**).

`Locale/DateFormat`: the input has `id="customFormat"` but the visible label
(likely a radio-group label or a `for="dateFormat"` label) does not match.
**Fix:** read the view to find which label should apply to `customFormat`;
if there is a heading/description that functions as the label, add a
visually-hidden `<label for="customFormat">` reusing an existing `<kw-l>` key.
If there is an existing `<label>` that should point at this input, add the
matching `for="customFormat"`.

`Languages/Translations`: the `bundle` file input has `name="bundle"` but no
`id` and no `<label for="bundle">`. **Fix:** add `id="bundle"` to the input
and a visually-hidden `<label for="bundle">` reusing an existing `<kw-l>` key
already on the page (the upload description text is a natural source). Keep it
inside the existing `<form>`. **No new translation string.**

### Shape C — file inputs with an `id` but no `<label>` (1 lead, 2 views)

`Account/Portability/Index` (1, `id="archive"`) and
`Account/Portability/ResolveReview` (1, `id="archive"`).

These have a heading/description but no `<label>` element. This is the R27
`Document/Index` shape.

**Fix:** add a **visually-hidden** `<label for="archive">` that reuses an
existing `<kw-l>` key already on the page (the import/export description text
is a natural source). Keep it inside the existing `<form>`. **No new
translation string.**

### Shape D — dynamic shared partials with Razor-concatenated id/for (2 leads, 2 views)

`Shared/_GrantPickers` (1) and `Shared/_NoteButton` (1). These build their
`id` **and** `for` via **Razor string concatenation** (e.g.
`allId = Model.EditorName + "-grants-" + kind.ToLowerInvariant()`, then
`id="…allId…"` and `for="…allId…"`). In source the id is `@($…)` — a Razor
expression — so the static heuristic cannot resolve it. **At runtime they are
correctly associated.** This is a heuristic limitation, not a markup bug.

**Fix:** **do not edit these files.** Record both as **justified survivors**
in `ux-baseline.json` — append a sentence to `generated_from` (house format)
stating that `Shared/_GrantPickers` and `Shared/_NoteButton` build `id`/`for`
via Razor concatenation and are correctly associated at runtime, and set
`counts.a11y` to the number you land at. This matches the R21 `jargon`
"each survivor justified in baseline" precedent. Do **not** add an
`aria-label` (a real `for`/`id` pair already exists at runtime — a second
accessible name would be redundant). Do **not** refine `ux-measure.py`
(risky; would require re-testing all 29 flagged views).

### The 3 already-clean views in the card (0 each) — read-only

`Notes/NoteRemoveTag`, `SecuritySettings/Mfa`, `Shared/_AccountNav` — read
each to confirm 0 unlabeled controls + 0 missing-`alt` images, then move on;
no edit expected.

**Rules for all fixes (same as R24–R27):**

- Label **text stays exactly as-is** — it is already a `<kw-l key="…">`. You
  are adding `id` / `for` attributes, or (Shape B/C) adding one
  `visually-hidden` `<label>` that reuses an existing key. **No new
  translation strings** in this batch.
- Do **not** use `aria-label` as a substitute for a real `for`/`id` (or a real
  `<label>`). Do **not** introduce a duplicate `id`.
- Keep Bootstrap classes and the grid layout unchanged (the `visually-hidden`
  label is the only added element). No inline `on*` handlers, no `<script>`
  blocks (SEC gate).
- **Only the 10 named R28 views.** Do not touch the 7 residual resident views
  or the 9 admin-view leads. Do not edit `Shared/_GrantPickers` /
  `Shared/_NoteButton` (they are recorded as survivors, not changed).

## The re-baseline

After all views are handled:

```bash
python3 tools/ux/ux-measure.py            # read the new a11y count
```

Expect `a11y` to drop from **56** to **49** (the 7 fix-target leads leave;
the 2 dynamic partials remain as justified survivors). In
`tools/ux/ux-baseline.json`, set `counts.a11y` to the new number and append a
sentence to the `generated_from` string recording the R28 re-base **and** the
2 justified survivors (house format — see the R27 entry as the most recent
precedent). When appending, watch the seam: a `+=` concatenation can leave a
stray `..` at the previous note's boundary (this happened on R26) — check and
clean it.

## Scope rules

- **One commit:** `feat(ux): a11y batch 5 — label-pair the 8 R28 resident views to 0 + record 2 partials as survivors + re-baseline (IMPROVE-UX R28)`.
- **Files that should change:** `Community/Manage.cshtml`,
  `Community/Translations.cshtml`, `Locale/DateFormat.cshtml`,
  `Languages/Translations.cshtml`, `Account/Portability/Index.cshtml`,
  `Account/Portability/ResolveReview.cshtml` (the 6 that carry edits) +
  `tools/ux/ux-baseline.json`.
- **Read-only:** `Notes/NoteRemoveTag`, `SecuritySettings/Mfa`,
  `Shared/_AccountNav` (already clean), `Shared/_GrantPickers`,
  `Shared/_NoteButton` (survivors), and all 7 residual + 9 admin views (out
  of scope, parked in the handoff note).

## Parked follow-up (record in the handoff note, do not create a new lane)

- **7 residual resident views (20 leads):** `Account/Login` (5),
  `Account/Signup` (4), `Groups/Create` (3), `Guardian/AddChild` (3),
  `Account/Delete` (2), `Account/Verify` (2), `Account/ResendVerification` (1).
  All Shape A (`asp-for` inputs with `asp-for` labels).
- **9 admin/operator views (27 leads):** `AdminSite/Index` (12),
  `AdminSetup/Setup` (4), `Admin/Communities` (3), `Admin/Audit` (2),
  `AdminSurfaceLabels/Index` (2), `Admin/Accounts` (1), `Admin/Index` (1),
  `AdminDateFormat/Index` (1), `AdminPortability/Index` (1).

## Before committing

```bash
# per-view — the 8 fix-target views must each report 0; the 3 clean views
# confirm 0; the 2 partials stay at 1 each (survivors).
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
fix = ['Community/Manage.cshtml','Community/Translations.cshtml',
       'Locale/DateFormat.cshtml','Languages/Translations.cshtml',
       'Account/Portability/Index.cshtml','Account/Portability/ResolveReview.cshtml']
clean = ['Notes/NoteRemoveTag.cshtml','SecuritySettings/Mfa.cshtml','Shared/_AccountNav.cshtml']
partials = ['Shared/_GrantPickers.cshtml','Shared/_NoteButton.cshtml']
for v in fix:
    b, a = measure(v); print(('OK  ' if (b,a)==(0,0) else 'FAIL'), v, (b,a))
for v in clean:
    b, a = measure(v); print(('OK  ' if (b,a)==(0,0) else 'WARN'), v, (b,a))
for v in partials:
    b, a = measure(v); print('SURVIVOR', v, (b,a))
"
python3 tools/ux/ux-measure.py            # total a11y: 56 -> 49
python3 tools/ux/ux-measure.py --check    # MUST pass
```

Confirm: all 6 fix-target views report **0**; the 3 already-clean views
confirm **0**; the 2 dynamic partials remain at **1** each and are recorded as
justified survivors in the baseline; the 20 residual + 27 admin leads are
unchanged; total `a11y` dropped **56 → 49**; other categories unchanged;
`--check` passes; `dotnet build` is green. Then report: per-view before →
after, the exact `id`/`for` pairings (file:line), the new `a11y` number, the
`generated_from` note (including the survivor justification), whether any new
translation key was needed (expected: none), and the commit hash. Also confirm
in the handoff note that the 20 residual + 27 admin leads are parked for a
follow-up (do not create a new lane).
