---
id: r22-prompt
title: "Prompt: IMPROVE-UX R22 — how-it-works.md matches the shipped behaviour"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/philosophy/how-it-works.md
  - docs/DOCS-CONVENTIONS.md
lane: improve-ux
updated: 2026-11-12
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **R22** — bring
> `docs/philosophy/how-it-works.md`
> (the resident entry point) up to date with the shipped platform.
> Two stale "not built yet" notes (events, projects), a
> GitHub-only feedback channel, and the absence of the self-service
> deletion and the in-app report-outcome loop — all of which
> shipped in this lane (A05/R05–R08 for reports; R10–R13 for
> deletion) — are now doc drift (E13, DoD #5).
> **Who is it for:** the next coding agent (fresh context).
> **When to read the rest:** when starting R22 (all R22 deps — A05
> and R13 — have shipped; the doc is the only thing left).

# Fresh-context prompt: IMPROVE-UX R22

You are continuing the Kumunita repository on `development` (HEAD
`6ff67ae0`, **O04 just closed** — the last visible piece of the D8
operator surface). Before it: the whole A-lane (A01–A15) shipped,
the R-lane (R01–R21, R23–R33) shipped, the O-lane (O01–O04) shipped.
R22 is the **docs-only** tail: `how-it-works.md` (the resident entry
point — the first thing a non-technical neighbour reads) still
carries two "not built yet" notes, routes feedback to GitHub issues
only, and does not mention self-service account deletion or the
reporter's outcome loop. The plan card:

> R22 — `how-it-works.md`: events/projects shipped, feedback →
> `/issues/new` + "My reports", reports reach moderators (after A05),
> deletion is self-service (after R13) — docs only; doc-check passes.
> Read first: `docs/philosophy/how-it-works.md`.
> Depends: A05, R13 (or mark pending).
> **Done when:** no "not built yet"; no GitHub-only feedback.

The problem it fixes (E13): the resident entry point — the page a
non-technical neighbour reads *first* — says events and projects are
"not built yet" (both shipped long ago, the M1–M3 surface), routes
feedback to "issues in this repository" (a GitHub repo; residents
don't file GitHub issues — the in-app `/issues/new` + "My reports"
loop shipped in R06/R08), and omits self-service account deletion
(R10–R13) and the moderator's report-outcome loop (A01–A06 + R03–R08).
Every one of those is now shipped; the doc is the last place they
aren't.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) — the doctrine (the ratchet, the
  don't-pause-mid-task rule).
- **`docs/DOCS-CONVENTIONS.md`** — the shared doc rules (front-matter,
  abstract, ADR cap, linking). R22 is a doc change; these rules apply.
- **`plan-improve-ux.md`** — the **R22 card** + the **E13** evidence row
  + the two Resident journeys ("Report a post → see the outcome",
  "Leave the platform") + DoD #5.
- **`docs/philosophy/how-it-works.md`** — the file you are editing.
  Read it in full. It is the resident entry point; the tone is plain
  English, no technical jargon, written for someone who can use a
  website but not read code. Preserve that register.
- **`docs/plans-milestones/in-progress/improve-ux-handoff-notes.md`** —
  skim the R06/R07/R08 (My reports), A05 (report routing), and
  R13 (self-service deletion) sections for the honest before → after
  that the doc should now reflect.
- **`git status`** + the branch (O04's `6ff67ae0` must be intact; you
  are on `development`).

**The one thing to get right.** R22 is a **docs-only** unit — no code,
no test, no migration. The file is the resident entry point; the
register is plain English for a non-technical neighbour. Two
invariants are load-bearing:

1. **No "not built yet" survives.** Every feature the platform ships
   (events with RSVP + reminders + iCal, projects with boards +
   to-dos + goals, messaging, groups, guardians, kiosk, storage,
   translations) is either described or absent — never marked
   "planned" or "not built yet". The M1–M3 surface is the shipped
   surface (the AGENTS.md must-know).
2. **Feedback is in-app, not GitHub-only.** The "How to give
   feedback" section must point residents to the in-app
   `/issues/new` + the "My reports" page (`/reports/mine`) where
   they can see the outcome. The GitHub repo is a *developer*
   channel, not the resident channel. A resident who reads this
   page and wants to say "something is broken" should be pointed
   at a button in the platform, not at a URL with a `/` in it.

**⚠️ Do NOT touch any `.cs`, `.cshtml`, `.json`, or test file.**
R22 is `docs/philosophy/how-it-works.md` only. The DoD line is
"no 'not built yet'; no GitHub-only feedback" + "doc-check passes".
If you find a feature that is genuinely not built (check the handoff
notes), mark it "planned" with a lane reference — do not delete the
line; the honest "not built yet" is fine, the *stale* "not built
yet" is the bug.

The **work** (all in `how-it-works.md`):

1. **Events** — find the line:
   > *(Planned — later, after the platform is multilingual; not built yet.)*
   under "Plan events." Replace it with a plain description:
   events with sign-ups (RSVP), a day-before reminder, and an
   iCal link you can add to your phone's calendar. The event →
   RSVP → reminder → iCal loop is one of the closed loops the plan
   lists as "hold" (the last "✓" row in the Evidence table).
   No technical jargon. One or two sentences.

2. **Projects** — find the line:
   > *(Planned for M5 — not built yet.)*
   under "Work on projects together." Replace it with a plain
   description: a goal, a list of tasks (to-dos), and the people
   helping — you can see who's doing what and pick up a task.
   The board / to-do / goal surface is shipped (the M5 surface;
   R26's a11y batch covered `Projects/ProjectDetail`,
   `Todos/TodoDetail`, `Boards/BoardNew`). One or two sentences.

3. **Feedback** — find the "### Where to put it" section. The
   first bullet currently says:
   > **Issues in this repository** are the main channel.
   Rewrite the section to lead with the **in-app** channel:
   - **Something's not working?** Tell us from inside the platform —
     the "Report an issue" link (in your account menu) opens a
     short form. When someone looks at it, you'll see the outcome
     in your "My reports" page — no need to come back and ask.
   - Keep the translation / moderation-help / in-person bullets
     (they are still accurate).
   - The GitHub repo is the *developer* channel — if you read code,
     the repo is where the source of truth lives; but for "this
     button doesn't do what I expected", the in-app form is the
     right place. (One sentence, optional; the register is plain
     English, not "file an issue".)
   - The goal: a resident who finishes this page and wants to say
     "something is broken" should know to click a link in the
     platform, not to open a browser tab to a code host.

4. **Reports reaching moderators** — the "What happens when you
   report something" section (items 1–4) is *mostly* accurate, but
   item 4 says "it's noted" without saying the reporter *sees* the
   outcome. Add a brief sentence (or revise item 4) to note:
   when the moderator resolves it, you get a notification with the
   outcome — you can also find it any time on your "My reports"
   page. (The R07 `ReportResolved` notification links to
   `/reports/mine#r-{id}`; the A05 `report.queued` kind means
   moderators hear about new reports in their inbox, not just after
   a GlobalAdmin unlocks — the E3 loop is closed.)

5. **Self-service account deletion** — the doc currently has no
   mention of leaving the platform. Add a short paragraph (one or
   two sentences, plain register) in a natural place (near the end,
   before the "If you want to go deeper" section, or as a new
   bullet under "What you can do"):
   - **Leave when you're ready.** You can close your own account
     from your account settings. There's a short waiting period
     (so a change of heart can cancel it); after that, your
     content is removed from the platform.
   (The R13 surface: schedule → show date → cancel; the ADR 0301
   grace period; child accounts under a guardian cannot
   self-delete — but that detail is too granular for this page;
   keep it to two sentences.)

6. **Doc-check** — run `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/docs-check.ps1` (the CI gate, `docs/dev/testing-and-ci.md`) and confirm it exits 0.
   The front-matter `updated` date should be today
   (`2026-11-12`). If the front-matter `related` list needs the
   ADR 0301 / 0302 / 0304 references, add them; if not, leave the
   list as-is.

**Scope rules:**

- **Docs only.** One file: `docs/philosophy/how-it-works.md`.
  No `.cs`, no `.cshtml`, no `.json`, no test, no ADR.
- **One commit** (the house rule): `docs(ux): how-it-works.md
  matches the shipped behaviour — events/projects/feedback/deletion
  (IMPROVE-UX R22)`.
- **Register:** plain English, no technical jargon, no `//`
  comments, no ADR numbers in the resident-facing prose (the
  `related` front-matter can carry them; the body cannot).
- **The ux gate** (`tools/ux/ux-measure.py --check`) must not
  regress (it counts jargon + a11y leads in *views*, not docs —
  but run it to be safe; a new jargon string in a doc is not a
  regression, but a *broken* doc is).
- **Do not** add a new "## What's new" or "## What's changed"
  section — the platform's version history lives in
  `src/Kumunita.Web/WhatsNew.cs` (ADR 0110); the resident doc
  describes what the platform *is*, not what changed since the
  last release.
- **Do not** restate the roadmap. `Milestones.cs` is the canonical
  list (AGENTS.md convention); the resident doc describes
  capabilities, not the development timeline.

**Before committing:** `python3 tools/ux/ux-measure.py --check`
(must not regress) + `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/docs-check.ps1` (must exit 0) + read the edited file top-to-bottom once, in the
plain-English register, to confirm no jargon leaked in and no
"not built yet" survives. If you find a feature that is genuinely
not built (check the handoff notes — the E-table's last "✓" row
lists the closed loops that hold), mark it "planned" with the lane
reference; do not delete the line.

Then report: the lines you changed (before → after for each of the
five edits), the `docs-check.ps1` result, the ux-gate result, the commit
hash, and a one-line summary of the resident-facing change
("The resident entry point no longer says events/projects are
'not built yet'; feedback routes to the in-app form; deletion is
self-service"). Do not commit unrelated work.
