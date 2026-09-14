# RC U08 — acceptance gate + lane close + doc flips

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained. Primary tier = `docs/design/rich-content-design.md`
> (§Acceptance gate is authoritative); mismatch → record
> `## U08 — Drift pause` in the handoff note — do not silently pick.

## Goal

Run the lane's **acceptance gate** (the three-test shape from the
design doc: closed loop / handoff / part-vs-whole), **record** it in
the design doc, **close the lane** in the durable docs (the
`Milestones.cs` ↔ README Roadmap pair, `ARCHITECTURE.md`'s RC
mention if the design doc's Context named it, the ADR's status), and
**move every RC unit plan** from `in-progress/` to `done/`. This unit
**changes no production code** — if the gate fails, the fix belongs
to the unit whose drift caused it (record a `## U08 — Gate failure`
section naming the failing test + the suspected unit, and **stop** —
do not patch production code here; the next agent picks up from the
handoff note).

## Entry reads (≤ 5 items)

1. `docs/design/rich-content-design.md` — §Acceptance gate (the
   three-test shape), §Invariants (the 7 invariants the gate
   evidences), §FACES (R1–R8), §Drift guard.
2. `docs/plans-milestones/done/rich-content-handoff-notes.md` —
   **every** `## U<m>` section (U01–U07): the pinned outputs, the
   smoke results, and — critically — **every `Drift pause` /
   `Gate failure` / amendment sub-line** (these are the items the
   gate must either confirm or surface; a drift pause the gate does
   not address is recorded as an open item, not resolved).
3. `src/Kumunita.Web/Milestones.cs` — the home-page roadmap (the
   exact shape of a lane entry — the multilingual/media/group-posts
   lanes are the precedent for how a named lane appears here) + its
   doc-comment (the README-synchronization contract).
4. `README.md` — the **Roadmap** section (the `Milestones.cs`
   counterpart — the two must land in the same commit's wording) +
   the **current-status** line near the top (the M3 + group-posts
   "shipped" sentence — the RC lane lands in the same voice).
5. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the test that
   **pins the exact order + the single-in-progress milestone**
   (AGENTS.md names it): read it to know exactly what the
   `Milestones.cs` edit must satisfy (if the RC lane entry changes
   the in-progress count or order, this test's expectations must
   match — read its current assertions and record what the RC entry
   must look like for it to pass; if it would need a change, that
   change is **in scope for this unit** — it is a test, not
   production code, and the gate unit is where doc↔test parity is
   closed out; record the before/after).

## Deliverables (4 files modified, 0 new, 8 files moved)

### 1. The acceptance gate — run + record

**The three tests** (the design doc's §Acceptance gate is
authoritative; execute in this order, record each):

1. **Closed loop** (FACES R1): with the app running (the `run`
   task, background) + a logged-in resident (the dev login — the
   same one the U04/U05 smokes used; if no session is available via
   curl, record "closed loop verified by the U04/U05 smoke curls
   (400/200+id/orphan-404) + this gate's browser step" and use the
   browser — `open_browser_page` → the post-creation form → type a
   body with `**bold**` + a list → insert an image via the control →
   submit → the detail page shows the bold, the list, **and** the
   `<img>` that **loads** (the 200 half of R4)). Record: the post's
   body as authored, the rendered detail (a `screenshot_page` is
   acceptable evidence — save the reference in the note), and the
   `curl -i` of the image URL showing **200** + the stored content
   type + `nosniff`.
2. **Handoff** (FACES R3, the authorization seam): the **same**
   image URL, now as a **non-member** (log out / a second account /
   the anonymous case if the post is community-audience and the
   actor is a non-member — the exact actor setup the
   `R4_NonMember_Denies_404_OneDenyAuditRow` test used is the
   reference; for the live check, the simplest faithful setup is the
   post's audience + a resident not in it — record which actor
   pair you used) → **404** (not 403 — R·4) + **exactly one**
   `Read` **Deny** audit row (the audit surface — if the app logs
   audit rows, grep the log; if the audit seam is an in-memory
   substitute in tests only, the *test* `
   R4_NonMember_Denies_404_OneDenyAuditRow` is the executable
   evidence and the live 404 is the surface evidence — record which
   you used and why).
3. **Part-vs-whole** (R·7 + the 20-test list): the exact command
   from AGENTS.md (the **authoritative** runner path — the
   `run_tests`/VSTestBridge quirk is a known bug, do not retry it):
   ```powershell
   dotnet build Kumunita.slnx -c Debug
   dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
   dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
   ```
   Record: the pass/fail counts **per assembly** (e.g. "Web: N
   passed, 0 failed — includes the 7 `MarkdownRendererTests` + 8
   `ContentImage*Serving/Upload*Tests` + the **unmodified**
   pre-existing suite; Core: M passed, 0 failed — includes the 5
   `ContentImageOwnershipTests` + the unmodified
   `PostServiceTests`/announcement/multilingual suites — the
   R·7 "pre-existing suites pass unmodified" half"). **Any failure
   is a `## U08 — Gate failure`** (record the failing test name +
   the suspected unit from the handoff note's drift-pause index,
   then **stop** — no production fix here).

**Record the gate** in the design doc (the only edit to the design
doc permitted in this unit — an **append**): a section
`## Acceptance gate — run result (<date>)`, one line per test (1/2/3)
with the evidence reference (the curl status lines, the actor pair,
the pass counts) + a closing line: "Gate **pass** — the RC lane is
accepted" (or the failure reference).

### 2. `docs/plans-milestones/done/rich-content-handoff-notes.md` (modify)

Append `## Summary (lane close, <date>)` — 8–12 lines:
(a) the lane in one sentence (what it shipped: the one-renderer
   extension + the content-image lane + the composer on 4 surfaces +
   the render switch — the design doc's §Scope In list, compressed);
(b) the 7 invariants by id (R·1–R·7) with the unit that pins each
   (the U<m> that first made it executable);
(c) the gate result (the §Acceptance gate — run result ref);
(d) the **open items** (every drift pause / amendment sub-line from
   U01–U07 that the gate did not close — the exact list, or
   "none");
(e) the next-lane pointers (the design doc's §Scope Out / the
   ADR's "Not decided here" — the named deferrals, one line each);
(f) the 8 unit plans' final state (all in `done/` — the
   move-to-done is step 3 below; this line is written **after** the
   moves).

### 3. Move every RC unit plan to `done/` (8 files)

The unit plans U01–U07 move **themselves** as their last action
(each plan's own `Move-Item` line) — by the time U08 runs, U02–U07
should already be in `done/`. **U08's job is the sweep + its own
move**: verify all 8 are present in `docs/plans-milestones/done/`
(`rich-content-u01-plan.md` … `rich-content-u08-plan.md`) — if
any unit plan is **still** in `in-progress/` (a unit that did not
self-move), move it **and** record why it was left (the handoff
note's summary, item (f), gains a parenthetical). Then move U08:

```powershell
Move-Item docs\plans-milestones\in-progress\rich-content-u08-plan.md docs\plans-milestones\done\rich-content-u08-plan.md
```

**This is the last action of the lane** — `in-progress/` contains no
RC file; `done/` contains all 8 + the handoff notes with the Summary.

### 4. The durable-doc flips (3 files — **only** if the design doc's
§Drift guard + the handoff note's Summary say the lane is accepted,
i.e. after step 1's gate pass; a failed gate stops before this step)

- **`src/Kumunita.Web/Milestones.cs`** — add the RC lane entry in
  the same voice/shape as the existing named lanes (multilingual /
  media / group-posts are the precedents — read one and mirror the
  **exact** entry shape: the lane id, the one-line description, the
  status). **Keep the single-in-progress-milestone invariant the
  `MilestonesTests` pins** (entry read 5 — the test's expectations
  are the contract; if the RC entry is "done" and the multilingual
  entry is the in-progress one, the test's in-progress count must
  still read exactly one — adjust the test **only** if the entry
  shape forces it, and record the before/after).
- **`README.md`** — the **Roadmap** section: the RC lane line
  (**verbatim** the same wording as the `Milestones.cs` entry — the
  doc-comment's synchronization contract) + the current-status line
  (the "shipped" voice — RC lands next to M3/group-posts in the
  same sentence or the next).
- **`docs/adr/0025-rich-content-markdown-and-content-images.md`** —
  confirm `Status: Accepted` (U01 set it; if U01 set `Proposed`,
  flip it to `Accepted` **now** — the gate is the acceptance
  evidence; record the flip). **Do not** edit the ADR's decisions
  (a decision change is a new ADR — the drift guard's rule).

## Exit criteria

- The design doc carries `## Acceptance gate — run result (<date>)`
  with the three tests' evidence + the pass/fail verdict.
- The handoff note carries `## Summary (lane close, <date>)` with
  the 8 items (a–f, the drift index included in (d)).
- `docs/plans-milestones/in-progress/` contains **no** `rich-content-*`
  file (verify with a directory listing — record it); `done/`
  contains all 8 plans + the notes file.
- `Milestones.cs` + `README.md` Roadmap carry the RC entry in
  **identical wording** (record the exact line); `MilestonesTests`
  passes under the in-process runner (the gate's Core/Web run
  already covers it if the test is in the Web assembly — record
  which assembly it ran in).
- ADR 0025's `Status:` line reads `Accepted` (record the before
  value).
- **No production code modified by this unit** (the 4 modified files
  are: the design doc, the handoff note, `Milestones.cs`
  [+`MilestonesTests` only if the entry shape forced it], the
  README, the ADR's status line — verify with `git --no-pager
  status` and record the file list; any production file in the diff
  is a `## U08 — Gate failure` alarm + stop).
- The last action is the 8-file `in-progress/` → `done/` state,
  verified by listing — **the lane is closed.**
