---
id: r33-prompt
title: "Prompt: IMPROVE-UX R33 — empty states batch 5 (the final named batch; close out the empty states)"
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
> **R33** — empty states batch 5, the *final* empty-states batch named in the
> plan (R29 → R30 → R31 → R32 → R33). It drives the **13 named
> details/groups/guardian views** toward the house `.kmb-empty` idiom (a title,
> a body, and **one next action**) — but **only where the idiom genuinely
> fits**. R33 is different from R29–R32 in two ways the agent must not
> gloss over: (1) the 13 named views classify as **7 PLAIN + 3 NONE + 3
> `_`-partials**, and (2) the measured `empty_plain` is **10**, of which **3
> are heuristic false positives not in R33's named list at all**
> (`Boards/BoardNew`, `Kiosk/Settings`, `SecuritySettings/Mfa` — form /
> QR / inline-ternary branches the measure mis-credits). So converting R33's
> 7 named PLAIN views drives `empty_plain` **10 → 3** (not to 0), and several
> named branches are **read-only sub-sections** where a title+body+action is a
> poor fit and the honest outcome is a **justified-survivor record** (the R30
> `Directory/Detail` / R31 `_BoardList` precedent). This batch *closes out* the
> empty-states work: every genuine empty *list* in the named scope ends up
> friendly, and the residual `empty_plain` is recorded as a measure artifact +
> out-of-scope false positives, honestly (DoD line 8).
> **Who is it for:** the next coding agent (fresh context). **When to read the
> rest:** when starting R33 (R32 has just landed — `empty_plain` is **10**,
> `empty_none` is **31**; the house `.kmb-empty` idiom and the per-view shape
> are the R29–R32 handoff notes' closest precedent, and the R31 "justified
> survivor" precedent is the closest for this batch's read-only sub-sections).

# Fresh-context prompt: IMPROVE-UX R33

You are continuing the Kumunita repository on `development` (HEAD `1e1c472d`,
R32 just landed — `empty_plain` is **10**, `empty_none` is **31**, `a11y` is
`49`). **R33** is the next and **final** empty-states unit. The plan card (in
`plan-improve-ux.md`, the R33 row):

> R33 — Empty states batch 5 (details/groups/guardian): `Announcement/Detail`,
> `Event/Detail`, `Posts/Detail`, `Groups/Detail`, `Groups/_GroupMembers`,
> `Groups/_GroupPostReplies`, `Guardian/Detail`, `Guardian/Index`,
> `Guardian/_GuardianEventAttendance`, `Kiosk/Index`, `Locale/Index`,
> `Community/Manage`, `Community/Translations` — `.kmb-empty` + one next action.
> Read first: `Groups/Index.cshtml:144` (reference).
> **Done when:** `empty` plain count ≤ 1 (`Moderation/Index` is A06's).

R33 is a **view-content** unit (same class as R24–R32). You do **not** touch the
script (`tools/ux/ux-measure.py`) or the baseline's other categories. The plan's
"plain ≤ 1" DoD was written expecting the named views to close the count, but the
**true** state of the code makes the honest outcome **`empty_plain` → 3** (the 3
residual are out-of-scope false positives) — see *The state of the code*.

## The state of the code (measured 2026-11-11, `development`, HEAD `1e1c472d`)

- **What a "PLAIN" lead is** — `count_empty_states()` in `tools/ux/ux-measure.py`.
  A list view (one containing a `@(foreach …)` or `@for …` loop) is a **PLAIN**
  lead when it carries an empty-check (`.Count == 0`, `.Any() == false`,
  `IsNullOrEmpty(…)`, etc.) **but no `.kmb-empty` block**, and a **NONE** lead
  when it has a loop but **neither** a friendly empty state **nor** an
  empty-check. A view is **EXCLUDED** when it is a partial (`_`-prefixed), in an
  `Admin*` directory, one of the `non_list_views`, has no list-loop markup, or
  already has a `.kmb-empty` block. **Measure each view with the script's own
  logic before editing.**

- **The 13 R33 named views, classified (measured 2026-11-11):**

  **7 COUNTED PLAIN (a real empty branch; convert where the idiom fits, else
  record as a justified survivor):**

  | View | Empty branch (key, container) | Fit? (natural next action) |
  |---|---|---|
  | `Announcement/Detail` (`:487`) | comments sub-section — `announcements.comment_empty`, `.text-body-tertiary` | **Convert** — anchor to the comment composer (`announcements.comment_reply`), the R31 `_TodoComments` precedent |
  | `Posts/Detail` (`:586`) | replies sub-section — `posts.replies_empty`, `.text-muted.kmb-discussion` | **Convert** — anchor to the reply form (the R31 `_TodoComments` precedent) |
  | `Groups/Detail` (`:280`, `:353`) | **two** sub-sections — `groups.posts_empty` / `groups.posts_empty_can` (`:280`) and `groups.events_empty` / `groups.events_empty_can` (`:353`), both `.kmb-group-empty`, **conditional on `Model.CanPost` / `Model.CanCreateEvent`** | **Convert** — New post (`groups.new_post`) / New event (`groups.new_event`), but show the action **only in the `_can` branch**; the non-can branch is body-only (a justified body-only survivor for that branch) |
  | `Guardian/Detail` (`:80,120,156,200,534`) | **five** read-only sub-sections — `guardian.no_groups`, `guardian.no_communities`, `guardian.no_invitations`, `guardian.no_community_requests`, `guardian.otherGuardians.empty`, all `.text-muted` / `.alert-info` | **Mostly survivors** — these are read-only sub-sections (a guardian does not inline-create a membership / invitation / other-guardian here). Convert one only where a genuine in-view action exists; otherwise record each as a justified survivor (the R30 `Directory/Detail` / R31 `_BoardList` precedent) rather than inventing an action |
  | `Guardian/Index` (`:120`) | children — `guardian.empty`, `.alert-info` | **Decide** — if the view has an "add child" affordance, convert to it; otherwise a justified survivor |
  | `Community/Manage` (`:96`, `:137`) | members (`community.manage_no_members`, "add some below") + candidates (`community.manage_all_members`, a **terminal** "everyone is already a member") | **Convert the members branch** (the Add-member picker below is the honest action, `community.manage_add_member`); the candidates branch is a **terminal state** → justified survivor (no action) |
  | `Community/Translations` (`:66`) | a **one-word inline chip** — `community.translations_none` ("None yet"), `.text-muted.small` | **Survivor** — a 1-word inline chip is not a list; forcing a title+body+action is misleading. Record it |

  **3 COUNTED NONE (a loop, but no empty branch — mostly dropdown / variant
  loops that are NOT real listable collections):**

  | View | The loop | Fit? |
  |---|---|---|
  | `Kiosk/Index` (`:146`) | `@foreach (var slide in Model.Slides)` — a **wall-screen slide list** | **Only named NONE view that may warrant a friendly empty state** (a zero-slide board). Convert to `.kmb-empty` (a title + body; no action fits) **or** record as a survivor — your call, record it |
  | `Event/Detail` (`:220`, `:271`) | `@foreach (var t in Model.Translations…)` — a **translation-variant** render (title/body variants), not a user-facing list | **Survivor** — not a listable collection; do not force an empty block |
  | `Locale/Index` (`:41`, `:102`) | `@foreach (var lang in Model.Languages…)` — a **`<select>` options** loop (the platform languages, always non-empty) | **Survivor** — a dropdown's option population, not an empty state; do not force an empty block |

  **3 EXCLUDED partials (`_`-prefixed — the measure does NOT count them):**

  `Groups/_GroupMembers`, `Groups/_GroupPostReplies`,
  `Guardian/_GuardianEventAttendance`. Convert one **only** if it carries its
  own natural list-level empty branch (title + body + one action where one
  fits); otherwise record it as a justified survivor (the R31 `_BoardCard` /
  `_TodoDisplay` precedent).

- **The 3 residual `empty_plain` leads NOT in R33's named list (false
  positives — do NOT convert, record them):**

  The measured `empty_plain` is **10**, but only **7** are R33's named views.
  The other **3** are heuristic false positives the measure mis-credits because
  an `IsNullOrEmpty`/`.Count == 0` guard sits in a **form / QR / inline-ternary**
  branch, not an empty *list*:
  - `Boards/BoardNew` (`:146`) — `@if (Model.Lanes.Count == 0)` renders a single
    initial-lane **input** (a form branch), not an empty list.
  - `Kiosk/Settings` — a shared-links / options guard, not an empty list.
  - `SecuritySettings/Mfa` (`:157`) — `@if (!string.IsNullOrEmpty(Model.QrDataUri))`
    renders a **QR code** (a form branch), not an empty list.

  These are **outside R33's named scope**. Do not wrap a form/QR branch in
  `.kmb-empty` — that would be misleading. Record all three as justified
  false-positives in the handoff note (the R23 heuristic-refinement / R31
  survivor precedent) and note that the plan's "plain ≤ 1" DoD is a measure
  artifact: every genuine empty *list* in the named scope ends up friendly, and
  the residual `empty_plain` = 3 is these false positives.

  **Re-baseline expectation (honest):** converting R33's 7 named PLAIN views
  drives `empty_plain` **10 → 3**. `empty_none` is **31** and stays **31**
  unless you give `Kiosk/Index` a friendly empty state (a NONE view that gains a
  `.kmb-empty` block leaves the NONE bucket → **31 → 30**); `Event/Detail` and
  `Locale/Index` are dropdown/variant loops and should be recorded as survivors
  (NONE unchanged). **Set the baseline to the measured value** and record the
  outcome honestly.

- **The house `.kmb-empty` idiom** — see `Groups/Index.cshtml:144` (and the
  R29–R32 views): a **title** (`.kmb-empty-title`), a **body**
  (`.kmb-empty-body`), and **one next action** (a button or link to the most
  relevant thing the actor can do). All three are `<kw-l>`-wrapped (no
  hard-coded English — the `l10n` gate requires all four languages). When the
  idiom is a poor fit for a read-only / one-word / terminal sub-section, the
  honest outcome is a **justified-survivor record**, not a forced block.

- **Keys to reuse (confirmed present in en/de/fr/da):** the named body keys
  (`announcements.comment_empty`, `posts.replies_empty`, `groups.posts_empty`,
  `groups.posts_empty_can`, `groups.events_empty`, `groups.events_empty_can`,
  `guardian.no_groups`, `guardian.no_communities`, `guardian.no_invitations`,
  `guardian.no_community_requests`, `guardian.otherGuardians.empty`,
  `guardian.empty`, `community.manage_no_members`, `community.manage_all_members`,
  `community.translations_none`) and the action keys
  (`announcements.comment_reply`, `groups.new_post`, `groups.new_event`,
  `community.manage_add_member`). **Add** one short `*_title` key (en/de/fr/da)
  per empty block you actually convert and that needs one — none of the R33
  `*_title` keys exist yet. **Do not** add a body key if the existing body
  sentence already reads as a complete empty-state, and **do not** add any key
  for a survivor (a survivor reuses nothing new).

## The work

Work view-by-view; after each, re-run the per-view measure (see *Before
committing*) to confirm the intended outcome (a `.kmb-empty` block where
converted, or a recorded survivor) before moving on.

For each of R33's 13 named views:

1. **Read the view** to find the empty branch and whether a natural next action
   exists in that view.
2. **Convert** a branch to `.kmb-empty` (title + body + one next action) **only**
   where the idiom genuinely fits a listable empty state — `Announcement/Detail`
   (comments), `Posts/Detail` (replies), `Groups/Detail` (posts / events, action
   in the `_can` branch only), `Community/Manage` (members branch), and
   `Kiosk/Index` (the wall-screen slide list, if you judge it warranted).
3. **Record a justified survivor** (do not force a block) where the branch is a
   read-only sub-section, a one-word chip, a terminal state, or a dropdown/
   variant loop — `Guardian/Detail` (the five read-only sub-sections),
   `Guardian/Index` (if no add-child affordance), `Community/Manage` (the
   candidates terminal branch), `Community/Translations` (the one-word chip),
   `Event/Detail`, `Locale/Index`, and any of the three partials with no natural
   empty branch.
4. **Do not** change the normal-list markup (the `@(foreach …)` block) — only the
   empty-state branch.
5. **Do not** add inline `on*` handlers or `<script>` blocks (SEC gate).
6. **Do not** reword existing text — you are swapping the *container* to
   `.kmb-empty` (or recording a survivor), reusing the existing `<kw-l>` body
   keys.

**Rules for all fixes (same as R24–R32):** Bootstrap classes and the grid layout
unchanged (only the `kmb-empty*` classes are added); **only the 13 named R33
views** (do not touch the 3 out-of-scope false-positive views, the remaining NONE
views, or anything else); do not edit `tools/ux/ux-measure.py`.

**JSON-editing caution (learned in R29–R32):** the fr/da/de files store
non-ASCII as `\uXXXX` escapes and apostrophes as `\u0027`; there is **no single
canonical formatter** that reproduces every file. Do **not** re-`json.dumps` a
whole file. Insert a new key as a **single line**, escaping its value to match
the local style (non-ASCII → `\u%04X` uppercase, `'` → `\u0027`). The last line
of each file has **no trailing comma** — when appending N keys, add a comma after
the previous last key and between the new keys except after the final one. The
repo-wide `multi_replace_string_in_file` can collapse inserted lines — prefer a
small insert script or single-line `replace_string_in_file`, and **check the
diff is additions-only** (each file still parses with `json.load`).

## The re-baseline

After all views are handled:

```bash
python3 tools/ux/ux-measure.py            # read the new empty_plain / empty_none
```

Expect `empty_plain` to drop **10 → 3** (the 7 named PLAIN views leave the PLAIN
bucket; the 3 residual are the out-of-scope false positives
`Boards/BoardNew` / `Kiosk/Settings` / `SecuritySettings/Mfa`). `empty_none` is
**31** and stays **31** unless `Kiosk/Index` is given a friendly empty state
(then **30**). In `tools/ux/ux-baseline.json`, set `counts.empty_plain` and
`counts.empty_none` to the **measured** values and append a sentence to
`generated_from` recording the R33 close-out (house format — see the R32 entry as
the most recent precedent; append cleanly inside the existing JSON string, no
stray `..` at the seam, and verify the file still parses). **Record the residual
3 PLAIN leads and any survivor decisions honestly** — this is the batch that
closes the empty-states work, and the honest outcome is "all genuine empty lists
friendly; residual = measure artifact + out-of-scope false positives," not a
forced 0.

## Scope rules

- **One commit:** `feat(ux): empty states batch 5 (details/groups/guardian) — close out the empty states (IMPROVE-UX R33)`.
- **Files that should change:** the R33 views you actually convert + the
  `tools/ux/ux-baseline.json` + the handoff note. For each new `*_title` (or
  body) key, the relevant translation JSON files (`en`/`de`/`fr`/`da` under
  `src/Kumunita.Core/Localization/Keys/`) change (additions-only).
- **Read-only:** `Groups/Index.cshtml` (the `.kmb-empty` reference),
  `tools/ux/ux-measure.py`, the 3 out-of-scope false-positive views, and the
  remaining NONE views.

## Parked follow-up (record in the handoff note, do not create a new lane)

- **The 3 residual `empty_plain` false positives** (`Boards/BoardNew`,
  `Kiosk/Settings`, `SecuritySettings/Mfa`) are recorded as out-of-scope
  measure artifacts. If a later unit wants the count to reflect reality, the
  fix is a **heuristic refinement** in `ux-measure.py` (the R23 precedent) that
  stops crediting form / QR / inline-ternary guards as empty states — **not**
  wrapping those branches in `.kmb-empty`.
- **Remaining NONE leads** (31, or 30 if `Kiosk/Index` converts) — out of scope
  for the whole lane until a later batch.

## Before committing

```bash
# per-view — each R33 named view should report its intended outcome.
python3 -c "
from pathlib import Path
named = ['Announcement/Detail','Event/Detail','Posts/Detail','Groups/Detail',
         'Guardian/Detail','Guardian/Index','Kiosk/Index','Locale/Index',
         'Community/Manage','Community/Translations']
for v in named:
    f = Path('src/Kumunita.Web/Views') / (v+'.cshtml')
    print(('kmb-empty' if 'kmb-empty' in f.read_text(encoding='utf-8') else 'survivor '), v)
"
python3 tools/ux/ux-measure.py            # empty_plain: 10 -> 3; empty_none: 31 (or 30)
python3 tools/ux/ux-measure.py --check    # MUST pass
dotnet build                              # 0 errors
```

Confirm: each R33 named view reports its intended outcome (`.kmb-empty` where
converted, a recorded survivor otherwise); `empty_plain` dropped **10 → 3**;
`empty_none` unchanged (or honestly 30 if `Kiosk/Index` converted); other
categories unchanged; `--check` passes; `dotnet build` is green; the C02
four-language key-parity test passes if you added keys (run the C02 class via
the xunit.v3 in-process runner —
`dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.C02_DataAsEmbeddedResourcesTests"`).
Then report: per-view before → after (converted vs. justified survivor, and why),
the new `empty_plain` and `empty_none` numbers, the `generated_from` note, the
3 residual false-positive PLAIN leads (recorded, not forced), whether any new
translation key was needed (and which), and the commit hash. Confirm in the
handoff note that the empty-states work is **closed out** (all genuine empty
lists in the named scope friendly; residual = measure artifact + out-of-scope
false positives) and that the residual NONE leads stay parked (do not create a
new lane).
