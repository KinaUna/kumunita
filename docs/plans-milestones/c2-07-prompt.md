# C2-07 — the missing SEC pointer on `ClaimTodoAsync`

## What you are doing

You are the **eighth** agent on the **IMPROVE-CODE-2** lane (plan:
`docs/plans-milestones/plan-improve-code-2.md`). Your unit is **C2-07, DoD #8 —
"the missing SEC pointer added."** The lane before you: C2-00 landed the honest
ratchet gate; C2-01 + C2-02 finished the translation-lane collapse (the `≤ 40`
line met); C2-03 + C2-04 closed DoD #6; C2-05 landed DoD #3/#4's clean seams
(`FirstBootSeeder` / `ProjectsController` split, `SampleDataSeeder` + the 7
god-services recorded as honest keeps, RED 25→23); **C2-06 just finished DoD
#7** — `MailAlarmsService` and `UsagePurgeService` each got a pure `*Policy`
decision surface (`MailAlarmThresholdPolicy` / `UsagePurgePolicy`) with a plain
unit test, `246c6d6c` + `23181100`, handoff `83a47edf`.

**D7** says it in one sentence: *"`TodoService.ClaimTodoAsync` (`TodoService.cs`)
gets the **`// TODO ❓ SEC` pointer** — the claim-to-do standing is an access
decision outside `IAuthorizationService` that E11 flagged. The Moderation half is
already done (`IModerationService.cs`). Behaviour unchanged; this is the pointer
only."* Your whole unit is **one comment**: add the **missing** E11 SEC pointer on
the `ClaimTodoAsync` standing decision, in the same shape as the Moderation twin
that is already in the tree. It names a *security question for the `SEC` lane* —
it does not answer it.

**You are NOT** doing C2-08 (the close + the six-member flip — the *next* unit,
the successor to this one), or re-opening any prior unit. You are **not** changing
who may do what (the pointer changes no behaviour), **not** resolving the SEC
question (that is the `SEC` lane's call — the pointer only *names* it, matching
the predecessor's E11 intent), **not** touching routes / rendered HTML / document
schemas, **not** adding a feature, and **not** adding or removing any type, method,
or test. This is a **comment-only** change to one production file.

**Do not re-fix the gate.** The ratchet is honest (C2-00). A comment-only change
should move **no** gate number: no new `*Policy`/`I*Service` (so seam-test is
untouched), no baseline entry raised (so the ratchet is untouched), no file over
the `cs_file` ceiling (so the files list is untouched). If any gate number moves
after you add a *comment*, you have changed something besides the comment — stop
and find out what.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `83a47edf` (C2-06 close + handoff).
- Commit style: `feat(improve-code-2): C2-07 — <short description>` (conventional
  commits, em dash, no ADR in the subject) — **one commit** for the pointer, then a
  second `docs(improve-code-2): C2-07 — handoff entry ...` commit for the note.
  The gate is green after the code commit.
- Read `AGENTS.md` and `.github/copilot-instructions.md` first — the doctrine.
  Read the plan: `docs/plans-milestones/plan-improve-code-2.md` — the **D7**
  decision and the **C2-07** row are your spec, and **DoD #8** ("the missing SEC
  pointer added").
- Read the code-shape doctrine: `docs/philosophy/in-code.md` — the **comment rule**
  (what/when/throws + ADR number) applies to how you *word* the pointer; the
  ratchet is unaffected by a comment.

### The concrete starting numbers (measured at `83a47edf` — trust these + the code)

- **DoD #8 (the plan's carried-in line):** *Both* E11 items carry a
  `// TODO ❓ SEC` pointer with the reference. **Before:** the Moderation half is
  **done**; the `ClaimTodoAsync` pointer is **missing**.
- **The Moderation twin (already in the tree — the model to mirror):**
  `src/Kumunita.Core/Moderation/IModerationService.cs`, on the
  `IsInActorScopeAsync` doc — a `**TODO ❓ SEC (E11):**` block that reads
  (paraphrased): *"this is an access decision outside
  `IAuthorizationService` — routing it through the authorization seam is the
  `SEC` lane's decision (the IMPROVE-CODE plan's **E11** handoff, 'the
  authorization path is unique' **ADR 0006 §D**). … and does **not** change who
  may do what."* **Match this wording** (the `TODO ❓ SEC (E11):` marker + the
  `IAuthorizationService` / E11 / ADR 0006 §D refs + the "does not change who may
  do what" close).
- **The target (the missing half):** `src/Kumunita.Core/Projects/TodoService.cs`,
  method `ClaimTodoAsync` (the plan's line ref was `~1024`; the line has drifted —
  find the method). The **access decision** inside it is the standing call
  `var via = await ClaimStandingAsync(todo, actorId, ct);` (the "unassigned + a
  group or community membership standing, ADR 0073" gate). Put the
  `// TODO ❓ SEC (E11):` pointer **right at that standing decision** (the most
  honest spot — it marks the decision itself, not the whole method), or in the
  method's XML doc summary if you prefer the doc location. Either is acceptable;
  the marker + refs must be present on `ClaimTodoAsync`.

## The work (add one pointer, then record it)

- **Add the pointer** on `TodoService.ClaimTodoAsync`'s standing decision — a
  `// TODO ❓ SEC (E11): …` comment mirroring the Moderation `IModerationService.cs`
  E11 block: name that the **claim-standing** (`ClaimStandingAsync`) is an access
  decision outside `IAuthorizationService`, that routing it through the
  authorization seam is the **`SEC` lane's** decision (the IMPROVE-CODE **E11**
  handoff, "the authorization path is unique" **ADR 0006 §D**), and that this
  pointer **does not change who may do what**. This is the *second* of the two E11
  items — the first (Moderation) is already present; do not re-edit it.
- **Leave every other byte of `TodoService.cs` unchanged.** No signature change,
  no logic change, no reformat of the surrounding method. The pointer is the only
  diff.
- **Verify the Moderation twin is still present** (`IModerationService.cs` E11) —
  it should be untouched; confirm both E11 items now carry the pointer (that is
  the DoD #8 "both E11 items" line).
- **Record before → after** in the handoff note
  (`docs/plans-milestones/improve-code-2-handoff-notes.md`) — a C2-07 entry in the
  same shape as C2-06's: the two E11 items (Moderation = pre-existing, unchanged;
  `ClaimTodoAsync` = added here, at the standing call), the exact wording/refs used,
  and why DoD #8 is now met.

## Definition of done (C2-07)

- [ ] **The pointer is present on `ClaimTodoAsync`:** a `// TODO ❓ SEC (E11): …`
      on `TodoService.ClaimTodoAsync`'s standing decision (or its doc summary),
      carrying the `IAuthorizationService` / **E11** / **ADR 0006 §D** refs + the
      "does not change who may do what" close, in the shape of the Moderation twin.
- [ ] **Both E11 items carry the pointer:** the Moderation twin
      (`IModerationService.cs`) **and** the new `ClaimTodoAsync` pointer — the
      DoD #8 "both E11 items" line met.
- [ ] **Comment-only, behaviour unchanged:** the only diff in `src/` is the
      pointer (no type / method / signature / logic / test change).
      `dotnet build Kumunita.slnx` — 0 errors.
- [ ] **No gate number moved:** `tools/code/code-check.ps1` — OK (0 violations)
      (no new `*Policy`/`I*Service`, no baseline raise, no ceiling change — a
      comment moved none of them).
- [ ] **The docs gate is green:** `tools/docs/improve-check.ps1` — ALL GATES PASS
      (the C2-07 handoff entry recorded; gate b ADR-index drift clean).

**If any DoD line fails, stop and say which one and why — do not fix it
silently.** This unit is the smallest on the lane (one comment); if the standing
decision is not actually in `ClaimTodoAsync` (e.g. the standing logic lives
entirely in `ClaimStandingAsync` and `ClaimTodoAsync` is only a forwarder), put the
pointer where the access decision genuinely lives and record that in the handoff —
do not force the marker onto the wrong line. The DoD's *"say which line fails and
why"* is honored by the handoff entry, exactly as C2-05's and C2-06's were.
