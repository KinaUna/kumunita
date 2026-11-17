---
id: x06-prompt
title: "Prompt: IMPROVE-UX X06 — the lane close (archive + WhatsNew)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - src/Kumunita.Web/WhatsNew.cs
  - tests/Kumunita.Web.Tests/WhatsNewTests.cs
  - docs/archive/lanes/improve-sec/
  - docs/DOCS-CONVENTIONS.md
lane: improve-ux
updated: 2026-11-13
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **X06** — the lane close. X05 (`bb40cff6`, the close of F2 +
> F3) was the last finding unit; every journey row is settled, every DoD
> line is met or honestly recorded, and the only remaining open item is
> **F6** (the 6 `docs-check` gate failures in the `improve-sec` archive —
> another lane's docs, deferred by X02/X03/X04/X05). This unit does the
> housekeeping that a shipped lane gets: move the plan + handoff + all
> `*-prompt.md` files from `in-progress/` to
> `docs/archive/lanes/improve-ux/`, add the **WhatsNew 0.88.0** entry, and
> update the `WhatsNewTests.cs` pin (currently 0.87.0). **One commit.**
> **Who is it for:** the next coding agent (fresh context). **When to read
> the rest:** when starting X06 (X05 is committed at `bb40cff6`).

# Fresh-context prompt: IMPROVE-UX X06 (the lane close)

You are on `development` (HEAD **`bb40cff6`** — X05's close of F2 + F3,
committed 2026-11-13). Behind it: **every unit in the lane** has shipped
(X01 gate, R01–R33, A01–A15, O01–O06, X02 cold-read close, X03 F1+F4+F5,
X04 F7, X05 F2+F3). All 12 journey rows are settled (10 walked + 2
infra-verified), all 8 DoD lines are met or honestly recorded, and F6
(another lane's `improve-sec` archive docs) is the only deferred item.
**The lane is done.** This unit is the close.

**The one thing to get right.** X06 is **archive + WhatsNew + one test
pin** — not a code change, not a new feature, not a finding. One commit,
≤ ~6 files (the moved files + the WhatsNew row + the test pin). The moved
files keep their content byte-for-byte (a `git mv`, not a rewrite); the
front-matter `status:` field on the plan changes from `current` to `done`.
**Do not fix F6** (the `improve-sec` archive tier violations) — it is
another lane's docs and X02–X05 all deferred it; note it in the close
section as the sole deferred item.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) — the doctrine, including "the `Milestones.cs`
  + `WhatsNew.cs` + `roadmap.md` + `STATUS.md` keep in sync" and "lane
  history lives in `docs/archive/lanes/<lane>/`".
- **`improve-ux-handoff-notes.md` § X05** — the last section (the register
  the close section matches). Note: F6 is the only deferred item.
- **`docs/archive/lanes/improve-sec/`** (the directory listing) — the
  **shape** of a closed lane's archive: the plan + handoff + all `*-prompt.md`
  files live together under `docs/archive/lanes/<lane-id>/`. The `improve-ux`
  archive will have the same shape (the plan, the handoff, and ~40 prompt
  files).
- **`src/Kumunita.Web/WhatsNew.cs` lines 20–25** — the current head entry
  (`0.87.0`, IMPROVE-SEC). The new `0.88.0` row goes **above** it
  (newest-first). Match the prose shape: a resident-facing summary of what
  the lane shipped, the ADR range (0300–0306), and "the IMPROVE-UX lane".
- **`tests/Kumunita.Web.Tests/WhatsNewTests.cs` lines 60–75** — the pinned
  "the newest-first head is the 0.87.0 entry (IMPROVE-SEC …)" test. The
  assertion string + the `Version` value need to update to 0.88.0.
- **`git status --short`** + `git log --oneline -1` — confirm `bb40cff6` is
  intact; you are on `development`; the untracked prompt files under
  `in-progress/` are the lane's working files (they move with the lane).

## The work (all of it is one commit)

### 1. Archive the lane files (a `git mv` batch)

Move **every file** under `docs/plans-milestones/in-progress/` that belongs
to the `improve-ux` lane to `docs/archive/lanes/improve-ux/`:

```bash
mkdir -p docs/archive/lanes/improve-ux
git mv docs/plans-milestones/in-progress/plan-improve-ux.md    docs/archive/lanes/improve-ux/
git mv docs/plans-milestones/in-progress/improve-ux-handoff-notes.md  docs/archive/lanes/improve-ux/
# Every *-prompt.md file that is an improve-ux unit prompt (x02–x06, r01–r33, a01–a15, o01–o06):
# (x01-prompt.md does not exist — the X01 gate shipped without a prompt file.)
for f in docs/plans-milestones/in-progress/{x02,x03,x04,x05,x06}-prompt.md \
         docs/plans-milestones/in-progress/r{01..33}-prompt.md \
         docs/plans-milestones/in-progress/a{01..15}-prompt.md \
         docs/plans-milestones/in-progress/o{01..06}-prompt.md; do
  [ -f "$f" ] && git mv "$f" docs/archive/lanes/improve-ux/
done
```

**Do not move** files that are **not** improve-ux lane files (e.g. a
`r29-prompt.md` modification is the lane's — move it; but any file from a
different lane that happens to live in `in-progress/` is not yours to touch).
Check `git status --short` before and after the moves.

### 2. Update the plan front-matter (one field)

In `docs/archive/lanes/improve-ux/plan-improve-ux.md`:
- `status: current` → `status: done`
- `updated: 2026-11-13` (no change, it is already today)

The handoff notes front-matter: `status: current` → `status: done` (match).

### 3. Add the WhatsNew 0.88.0 entry

In `src/Kumunita.Web/WhatsNew.cs`, insert a new `Release` row **above** the
current `0.87.0` row:

```csharp
new("0.88.0", "2026-11-13", new List<string>
{
    "The IMPROVE-UX lane: close the loops, per audience. A resident who
reports a post sees the outcome on "My reports" and the moderator who
acts sees a nav badge with the open count; a resident who needs to leave
the platform schedules a deletion with a grace period (ADR 0301) instead
of asking an admin; a guest sees when their window ends + a link to reach
the admins; a kiosk on a wall screen runs on a revocable shared link
(ADR 0302) that shows community-visible items only; an admin invites a
neighbour with a single click while sign-up is closed (ADR 0304); and an
operator sees /admin/status (version, uptime, health, storage, dead
letters, mail alarms) instead of reading logs (ADR 0306). A friendly,
localized 404 / 403 / 500 page keeps the nav instead of a blank body
(ADR 0305). No new feature that the platform did not already ship — the
lane closed the loops the code already had but the journeys could not see
(ADRs 0300–0306).",
}),
```

**The prose shape** matches the IMPROVE-SEC / IMPROVE-CODE entries: a
resident-facing "what changed that you can feel" summary, the ADR range,
and "the IMPROVE-UX lane". Keep it to one paragraph.

### 4. Update the `WhatsNewTests.cs` pin

In `tests/Kumunita.Web.Tests/WhatsNewTests.cs`, the test
`The_ONB_OnboardingWALK_Entry_Is_Shipped` (the `DisplayName` currently reads
"The newest-first head is the 0.87.0 entry (IMPROVE-SEC security & privacy
integration) — 0.86.0 (IMPROVE-CODE-3) slides one row back") asserts
**three things** about the head and **two** about the second entry:

1. **Head version + date:** `head.Version == "0.87.0" && head.Date == "2026-11-09"`
   → change to `"0.88.0"` / `"2026-11-13"`.
2. **Head content keywords:** `head.Changes.Any(c => c.Contains("IMPROVE-SEC")
   && c.Contains("account deletion") && c.Contains("ADRs 0298"))`
   → change to `c.Contains("IMPROVE-UX") && c.Contains("close the loops")
   && c.Contains("ADRs 0300")` (match the prose you wrote in step 3).
3. **Second entry version + date:** `second.Version == "0.86.0" &&
   second.Date == "2026-11-06"` → change to `"0.87.0"` / `"2026-11-09"`
   (the IMPROVE-SEC entry is now the second entry).
4. **Second entry content keywords:** the `IMPROVE-CODE-3` assertions →
   `IMPROVE-SEC` assertions (the IMPROVE-SEC entry is now the second entry).
5. **`DisplayName`:** update to name 0.88.0 (IMPROVE-UX) as the head and
   0.87.0 (IMPROVE-SEC) as the second entry.

### 5. The `## X06` close section (a short append to the handoff)

Append a brief `## X06 — the lane close — **SHIPPED**` section at the end of
`docs/archive/lanes/improve-ux/improve-ux-handoff-notes.md` (match the
register of the X02/X03/X04/X05 sections): the lane is archived, the
WhatsNew 0.88.0 entry is added, the test pin is updated, and **F6** (the 6
`docs-check` gate failures in `docs/archive/lanes/improve-sec/`) is the
**sole deferred item** (another lane's archive docs — the `improve-sec`
lane owns those files; fixing them is a `fix(docs):` commit owned by that
lane, not this one).

### Commit

One commit: `docs(ux): X06 — lane close (archive + WhatsNew 0.88.0)
(IMPROVE-UX X06)`.

**Before committing:**
- `dotnet build` → **0 errors** (the WhatsNew row + test pin compile).
- `dotnet test --filter FullyQualifiedName~WhatsNewTests` → **green**.
- `pwsh tools/docs/docs-check.ps1` → the **6 F6 failures are
  pre-existing** (the `improve-sec` archive); confirm your moved files
  add **no new** failure.
- `python3 tools/ux/ux-measure.py --check` → **green** (counts unchanged).
- `git status --short` → only the lane files moved + the WhatsNew row +
  the test pin + the handoff append are staged.

Then report: the moved-file count, the WhatsNew entry (version + date),
the test pin update, the docs-check result (the 6 F6 pre-existing), the
gate result, the commit hash, and a one-line summary (e.g. "IMPROVE-UX X06
closes the lane — the plan + handoff + 40 prompt files are archived to
docs/archive/lanes/improve-ux/, the WhatsNew 0.88.0 entry is added, the
test pin is updated; F6 (the improve-sec archive tier violations) is the
sole deferred item, owned by that lane").
