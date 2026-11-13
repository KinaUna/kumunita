---
id: x03-prompt
title: "Prompt: IMPROVE-UX X03 — the cold-read successor (F1 + F4 + F5)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0304-email-invitations.md
  - docs/DOCS-CONVENTIONS.md
lane: improve-ux
updated: 2026-11-13
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **X03** — the direct successor to X02 (the cold-read close).
> X02's honest held close filed **7 findings (F1–F7)**; this unit picks up
> the three the successor can act on in one commit: **F1** (the A09
> `/admin/invitations` 500 — a view-location fix that unblocks journey J9),
> **F4** (the R21 `jargon` residual record — a handoff append), and **F5**
> (the R08/R09 `notify_without_link` residual record — a handoff append).
> F2, F3, F6, F7 are explicitly **not in this unit's scope**. **Who is it
> for:** the next coding agent (fresh context). **When to read the rest:**
> when starting X03 (X02 is committed at `8719298b`).

# Fresh-context prompt: IMPROVE-UX X03 (the cold-read successor)

You are on `development` (HEAD **`8719298b`** — X02's honest held close,
committed 2026-11-13). Behind it: **every unit in the lane** (X01, R01–R33,
A01–A15, O01–O06, X02) has shipped, and X02's cold read **filed 7 findings
(F1–F7)** in the `## X02` section of
`improve-ux-handoff-notes.md`. This unit is the **direct successor**: it
closes the three findings that are the fastest, highest-value wins and that
a single agent with a small context can complete in one commit — F1 (the
A09 view-location bug, a real `fix(ux):` commit that unblocks journey J9),
and F4 + F5 (the two missing residual records, a doc-only append that
satisfies the lane's own residual-recording convention).

**The one thing to get right.** X03 is **three small, non-overlapping
fixes** — not a new feature, not a lane. One commit, ≤ ~4 files (the view
move + a handoff append), one honest `## X03` section at the end of the
handoff note. **Do not open F2, F3, F6, or F7 in this unit** (the prompt
names them explicitly so the agent does not try to "finish the lane" —
they are the *next* successor's scope, each with its own ADR / verification
question that needs its own unit).

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) — the doctrine, including "Don't pause
  mid-task to check in".
- **`docs/DOCS-CONVENTIONS.md`** — the shared doc rules (front-matter, the
  **linking rule**, the tier model). The handoff append is a doc change.
- **`improve-ux-handoff-notes.md` § X02** — the 7 findings, the honest
  before → after register, and the precedent for how a residual is
  recorded (the R28 / R33 "justified survivor" / "measure artifact"
  shape). X03's F4 + F5 sections should match that register.
- **`src/Kumunita.Web/Controllers/AdminInvitationsController.cs`** — the
  F1 target: `[Route("admin/invitations")]` on the class, `Views/Admin/
  Invitations/Index.cshtml` is where the view currently lives, and the
  controller's own summary block (ADR 0304 D4) that names the shape of
  the surface.
- **`src/Kumunita.Web/Views/Admin/Invitations/Index.cshtml`** — the view
  to move. Read it once; the move is a `git mv`, not a rewrite.
- **`conventional-routing-name-mismatch`** (the repo memory) — the house
  pattern for this exact class of bug: a conventional-routing controller
  whose view lives in a directory the view-location lookup cannot find.
  The two house fixes: move the view (smaller) or add a `[Route]` pin
  (larger). F1 takes the first.
- **`git log --oneline -3`** + `git status` — confirm `8719298b` is intact;
  you are on `development`; the untracked prompt files under `in-progress/`
  are not yours to touch.

## The work (all of it is one commit)

### F1 — the A09 `/admin/invitations` 500 (the journey unblock)

**The bug (from X02's `## X02` § J9 + F1):**
`GET /admin/invitations` returns **500** —
`The view 'Index' was not found. The following locations were searched:
/Views/AdminInvitations/Index.cshtml, /Views/Shared/Index.cshtml`.
The controller is `AdminInvitationsController` (conventional routing,
`[Route("admin/invitations")]` on the class) and the view lives at
`Views/Admin/Invitations/Index.cshtml` (the `Admin` folder + an `Invitations`
sub-folder). The conventional view-location lookup tries
`Views/<ControllerName>/Index.cshtml` first — for this controller that is
`Views/AdminInvitations/Index.cshtml`, which does not exist. The
`Views/Shared/Index.cshtml` fallback is also absent. **Journey J9 (admin
invites a neighbour) is blocked by this until the view is found.**

**The fix (one of the two house idioms — pick the smaller):**

- **Option A (preferred — the view move):** `git mv src/Kumunita.Web/
  Views/Admin/Invitations/Index.cshtml src/Kumunita.Web/
  Views/AdminInvitations/Index.cshtml` (the controller's conventional
  view-location lookup then finds it; the existing `Admin/Invitations/`
  directory is empty after the move and can be removed). Verify the view
  does not reference any sibling partials under `Views/Admin/Invitations/`
  (it does not — the X02 walk + the `ls` output confirm it is a single
  file).
- **Option B (the `[Route]` pin — only if Option A breaks a test):** add
  `[ViewDataKey]`/an explicit view-location on the controller's `Index`
  action pointing at `Views/Admin/Invitations/Index.cshtml`. This is the
  larger change (it couples the controller to a path that is not its
  conventional one) and is the fallback.

**Do not** rename the controller (the `AdminInvitationsController` name is
referenced by the A09 tests, the A12 approval-queue code, and the ADR 0304
D4 text — renaming is a larger change than the view move).

### F4 — the R21 `jargon` residual record (a handoff append)

**The finding (from X02's `## X02` § DoD line 2 + F4):**
The 2 residual `jargon` strings — `account.invite_pending_approval`
(`"Your invitation for {0} is pending approval. A GlobalAdmin will review
it."`) and `account.invite_off` (`"Resident invitations are not enabled for
this community. Please contact a GlobalAdmin."`) — are **legitimate**
admin-facing strings (the A11/A12 invitation-feature copy; `GlobalAdmin`
is the correct term for the admin reader). R28's handoff cites the
"R21 `jargon` 'each survivor justified' precedent" — but **no `## R21`
section exists in the handoff note** (verified by X02's grep). The
residual-recording convention (the R33 precedent) is not satisfied for
these two strings.

**The fix (a handoff append — one new `## X03` section, F4 sub-section):**
In the `## X03` section (see below), add an F4 sub-section that records
the 2 residual strings with their key, their file
(`src/Kumunita.Core/Localization/Keys/en/account.json`), the unit that
introduced them (A11/A12, the ADR 0304 D6 c approval-queue feature), and
the honest "legitimate term in an admin-facing string" justification
(mirroring the R28 `Shared/_GrantPickers` / `Shared/_NoteButton`
"justified survivor" shape). **Do not** re-baseline the `jargon` count in
`tools/ux/ux-baseline.json` — the X02 prompt's scope rule forbids it, and
the honest state is "2 strings, both legitimate, recorded here" (the
R33 "measure artifact, not a forced 0" register).

### F5 — the R08/R09 `notify_without_link` residual record (a handoff append)

**The finding (from X02's `## X02` § DoD line 2 + F5):**
The 4 residual `notify_without_link` kinds — `TodoAssign`, `ReportFiled`,
`ReportAssigned`, + the R08 `IssueFiled` anonymous-filer path (the `IssueFiled`
→ anonymous filer has no inbox row to link to; the R08 ADR 0300
"anonymous-filer hold" is the design) — are **not all defects**. R09
(commit `9f7e670c`) added `linkPath` to `TodoAssign` + `GroupAdded`
(driving the count from 7 → 4). The remaining 4 are the kinds where "no
link back" is the honest shape (the reporter-to-author `ReportFiled` link
goes to the *author's* report row, not the reporter's — the A05/A06 design;
the `ReportAssigned` link goes to the *moderator's* queue, not the
reporter's — the A06 badge design; the `IssueFiled` anonymous filer has no
inbox — the ADR 0300 hold). **No `## R08` or `## R09` section exists in
the handoff note** (verified by X02's grep). The residual-recording
convention is not satisfied.

**The fix (a handoff append — one new `## X03` section, F5 sub-section):**
In the `## X03` section, add an F5 sub-section that records the 4 residual
kinds with their kind name, the file (the `EmitAsync` call site), the unit
that introduced them (R08 / R09 / A05 / A06), and the honest "no-link is
the design" justification (mirroring the R33 "measure artifact, not a
forced 0" register). **Do not** re-baseline the `notify_without_link`
count in `tools/ux/ux-baseline.json` — same scope rule as F4.

### The `## X03` section shape (append to `improve-ux-handoff-notes.md`)

Match the register of the other entries (R28, R33, X02):

```
## X03 — the cold-read successor (F1 the A09 view-location fix, F4 + F5 the two missing residual records) — **SHIPPED**

**Scope:** the direct successor to X02 (the cold-read close, `8719298b`).
X02's honest held close filed 7 findings (F1–F7); this unit closes the
three the successor can act on in one commit — F1 (the A09
`/admin/invitations` 500, a view-location fix that unblocks journey J9),
F4 (the R21 `jargon` residual record), and F5 (the R08/R09
`notify_without_link` residual record). F2, F3, F6, F7 are explicitly
out of scope (each is a separate successor's unit).

**Outcome:** F1 fixed (the view is at its conventional location,
`/admin/invitations` renders 200); F4 + F5 recorded (the 2 `jargon`
strings + the 4 `notify_without_link` kinds, each with the unit that
introduced it + the honest justification, the R28/R33 "justified
survivor" / "measure artifact" register). `dotnet build` green;
`dotnet test` — `Kumunita.Core.Tests` all green, `Kumunita.Web.Tests`
the same 3 known-reds as X02 (no new reds); `ux-measure.py --check`
green (the 2 + 4 residuals are now *recorded*, not *unrecorded* — the
counts themselves are unchanged, per the scope rule).

### F1 — the A09 view-location fix (journey J9 unblocked)
- **Before:** `GET /admin/invitations` → 500 (`The view 'Index' was not
  found. … /Views/AdminInvitations/Index.cshtml`).
- **After:** the view is at `Views/AdminInvitations/Index.cshtml`
  (conventional location, `git mv` + the empty `Admin/Invitations/`
  directory removed); `GET /admin/invitations` → 200 (verified live).
- **Journey J9 (admin invites a neighbour)** is now walkable end-to-end
  (the A09 surface renders; the A08 sign-up gate consumes the `?invite=`
  link; the A10 acceptance notification fires).

### F4 — the R21 `jargon` residual record (2 strings)
- `account.invite_pending_approval` (`en/account.json`) — introduced by
  A11/A12 (ADR 0304 D6 c, the approval-queue feature); the `GlobalAdmin`
  reference is the correct term for the admin reader (the resident sees
  the same string in the A12 approval-queue surface).
- `account.invite_off` (`en/account.json`) — same unit, same
  justification.
- **Register:** justified survivor (the R28 `Shared/_GrantPickers` /
  `Shared/_NoteButton` precedent). The `jargon` count stays 2 (not
  re-based — the X02 scope rule).

### F5 — the R08/R09 `notify_without_link` residual record (4 kinds)
- `TodoAssign` — R09 added `linkPath: /projects/todos/{id}` (commit
  `9f7e670c`); the residual is the *anonymous* assignee path (no inbox
  row to link to — the design).
- `ReportFiled` — the reporter-to-author kind; the link goes to the
  author's row, not the reporter's (the A05/A06 design).
- `ReportAssigned` — the reporter-to-moderator kind; the link goes to
  the moderator's queue (the A06 badge design).
- `IssueFiled` (the anonymous-filer path) — the ADR 0300 "anonymous-filer
  hold" (no inbox row to link to — the design).
- **Register:** measure artifact (the R33 precedent). The
  `notify_without_link` count stays 4 (not re-based — the X02 scope rule).

### Parked follow-ups (F2, F3, F6, F7 — NOT in this unit)
- **F2 (J7 guest end-date):** verify R20 landed + re-walk with a seeded
  guest-access resident. Next successor's scope.
- **F3 (J10 request-more-space):** verify A14 landed + re-walk with a
  seeded quota. Next successor's scope.
- **F6 (docs-check 6 failures in `improve-sec` archive):** pre-existing
  tier-model violations, not this lane's scope.
- **F7 (D2 + D8 ADRs missing):** two `docs(adr):` commits (the next free
  numbers are 0305 + 0306). Next successor's scope.
```

### Front-matter + commit

- `improve-ux-handoff-notes.md`: no front-matter change needed (it is a
  living doc, append-only; `updated:` should be `2026-11-13` if not
  already).
- **One commit** (the house rule, per `commit-message-style`):
  `fix(ux): A09 view-location + X03 handoff append (F1 + F4 + F5)`
  (or `feat(ux):` if the view move is judged a feature — the `conventional-
  routing-name-mismatch` precedent used `fix(ux):`).
- Commit **only** `improve-ux-handoff-notes.md` + the moved view file
  (+ the empty `Admin/Invitations/` directory removal if `git mv` leaves
  it). Do **not** commit unrelated working-tree changes (the untracked
  prompt files under `in-progress/` are not yours to touch).

**Scope rules:**

- **F2, F3, F6, F7 are NOT in this unit.** Each is a separate successor's
  unit with its own verification question (F2 + F3 need a seeded
  resident; F6 is another lane's docs; F7 is two ADRs). Do not open
  them here.
- **One commit.** The view move + the handoff append are one logical
  change (the X03 successor's unit).
- **Do not re-baseline `ux-baseline.json`.** The X02 prompt's scope rule
  forbids it; the honest state is "2 + 4 residuals, both recorded in the
  handoff" (the R33 "measure artifact, not a forced 0" register).
- **Honest register.** A held line is not a failure. Cite the unit that
  introduced the residual (A11/A12 for F4; R08/R09/A05/A06 for F5).

**Before committing:** `python3 tools/ux/ux-measure.py --check` (must
pass — the counts are unchanged) + `dotnet build` (green) +
`dotnet test tests/Kumunita.Web.Tests --filter "FullyQualifiedName~AdminInvitations"`
(the A09 tests must be green — the view move must not break them) +
`curl -s http://localhost:5123/admin/invitations` (signed in as
`admin@examplium.com` / `Admin123!`) → 200. Then read your `## X03`
section top to bottom to confirm every line is honest and every held item
is cited.

Then report: the gate result, the build/test result, the F1 before →
after (the 500 → 200), the F4 + F5 residual records (the 2 + 4 strings),
the commit hash, and a one-line summary (e.g. "The IMPROVE-UX lane's
cold-read successor closes F1 (the A09 `/admin/invitations` 500 —
journey J9 is now walkable end-to-end) and F4 + F5 (the 2 `jargon` +
4 `notify_without_link` residuals, each with the honest justification)
— F2/F3/F6/F7 are the next successor's scope").
