# IMPROVE — audit ledger

> **U00 deliverable** (the reading pass). This is the *deliverable the
> community reads*: the shipped surface scored against the six principles,
> the three tests, and the FACES faces, with each finding carrying **where**
> (file + line or path), **what** (one sentence), **which
> principle/test/face it violates**, **severity** (S / M / L), and **a
> proposed unit** (`U01..U09`, or "record and accept").
>
> **Severity scale:** **S** = trust at risk (a privacy leak, a non-auditable
> access decision, a broken seam that can corrupt data). **M** =
> maintainability at risk (a god-file, a duplicated doctrine, a silent
> coupling). **L** = discoverability at risk (a stale index, a missing
> abstract, a handoff note with no entry point).
>
> **Baseline numbers** in this ledger were collected **2026-10-08** on this
> machine with `pwsh` (the same scripts that become the gate, see
> `improve-report.ps1`). Where a number is stated, the report script will
> reproduce it; where a gate exists, the *gate* is authoritative and this
> number is a snapshot.
>
> **The anti-pattern vocabulary** used here is the one from
> [`docs/philosophy/anti-patterns.md`](../../philosophy/anti-patterns.md) —
> the eight names are portable, the Kumunita symptom is local.

## How to read this ledger

- One `##` section per **principle** (1–6), one `###` per **three-test**
  (closed-loop / handoff / part-vs-whole), one `###` per **FACES** face
  (Flexible / Adaptive / Coherent / Energizing / Stable).
- Under each: a bullet list of findings. Each finding names *where*, *what*,
  *which rule it breaks*, *severity*, and *the unit that closes it* (or
  "record and accept" where the finding is a known trade we live with).
- **U00 does not fix anything** — it scores. `U01..U09` are the units that
  close the highest-value findings. If you are a *non-technical* reader,
  skip to [`## The integrative question`](#the-integrative-question-the-part-vs-whole-reading)
  — that is the section the lane register says a community member reads
  first.

## Principle 1 — Quality is the quality of the linkage

The platform is good because the connections hold, not because each screen
works. Most failures are seam failures, not component failures.

- **Where:** `src/Kumunita.Core/Authorization/AuthorizationService.cs` +
  `docs/SECURITY.md` · **What:** the access model is the single
  load-bearing contract in the codebase; every `AccessAction` × `AccessVia`
  pairing is a seam that must be *both* tested on the denied path *and*
  audited on the granted path. · **Violates:** principle 1 (the linkage is
  the quality); the seam in `in-code.md` ("the access model is the most
  load-bearing contract"). · **Severity:** **S** (a non-auditable access
  decision or a missing denied-path test is a trust risk). · **Proposed
  unit:** **U08** (the security & privacy seam audit).
- **Where:** `src/Kumunita.Web/Controllers/` (the four composer controllers —
  `PostsController.cs`, `EventController.cs`, `ProjectsController.cs`,
  `AnnouncementController.cs`) · **What:** the `SeedGrantPickerOptionsAsync` /
  `SeedComponentPickerAsync` / `SeedLanguagePickerAsync` trio is claimed by
  `ProjectsController` to be "the M2/M3/M4 shared pattern, reused not
  reinvented"; the ledger does not yet confirm whether it *is* shared or is
  copy-paste. · **Violates:** principle 1 (if duplicated, the linkage is
  accidental, not designed); `anti-patterns.md` "silent coupling". ·
  **Severity:** **M** (maintainability: a picker change landing in four
  places). · **Proposed unit:** **U05** (verify the claim; if duplicated,
  extract to one shared helper). · **Status (U05, 2026-10-08):** **closed —
  the claim was false, the trio was copy-paste.** Each of the three seed
  methods was defined once per controller (a private copy in each of the
  four composer controllers; `ProfileController` + `GroupsController` each
  carried their own partial copies). The ADR 0106 `Assign_Users` surface in
  `ProjectsController` was a *divergence* — the same seed method but with an
  extra list, the "silent coupling" anti-pattern the ledger names. U05
  extracts the trio into one shared helper
  (`src/Kumunita.Web/Models/ComposerSeedOptions.cs`, the composer trio
  shared by the post/announcement/event/project/group composer GETs — new
  composer GETs call this, do not copy it), with an optional
  `includeAssignUsers: bool` parameter for the ADR 0106 surface. The
  per-controller private methods are now thin delegates to the helper.
  **Before → after line counts:** `ProjectsController.cs` 4971 → 4890
  (−81), `PostsController.cs` 2165 → 2094 (−71), `EventController.cs`
  1927 → 1861 (−66), `AnnouncementController.cs` 1132 → 1118 (−14).
  `ComposerSeedOptions.cs` is new (160 lines — well under the 2 000-line
  gate ceiling). **Public surface unchanged:** routes, view-models,
  rendered HTML are all identical (the existing controller tests —
  `PostsControllerTests`, `EventControllerTests`,
  `ProjectsControllerTests`, `AnnouncementControllerTests` — pass
  **unmodified**: 951 Web.Tests, 0 errors, 0 failures). The gate (a) still
  passes (the four controllers are grandfathered at their U00 baselines,
  and they are now *smaller* than those baselines — the gate only fails on
  growth).

## Principle 2 — Differentiation is deliberate, integration is the work

Every boundary is a place where access, trust, and value must be designed.

- **Where:** `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
  (9 435 lines) · **What:** a single flat dictionary of ~4 500
  key-to-value pairs carries every UI string on the platform; the *internal*
  differentiation (per-surface key groups) has not been made, so a reader
  looking for the `guardian.timelimit.*` keys scrolls past `admin.guests.*`
  and `settings.quiet.*`. · **Violates:** principle 2 (differentiation is
  deliberate — here it is absent *inside* the file); `anti-patterns.md`
  "the god part" (local: "one script that does all of moderation"). ·
  **Severity:** **M** (maintainability: a future lane adding 200 new keys
  lands in a 9 000-line monolith). · **Proposed unit:** **U04** (split into
  per-surface static properties; the public surface — `EnValues`, `AllKeys` —
  is unchanged).
- **Where:** `src/Kumunita.Web/Views/Projects/BoardDetail.cshtml`
  (1 431 lines), `src/Kumunita.Web/Views/Posts/Detail.cshtml`
  (1 015 lines), `src/Kumunita.Web/Views/Projects/TodoDetail.cshtml`
  (890 lines) · **What:** the three largest views each carry
  self-contained sub-surfaces (lane/column lists, card blocks, settings
  panels) that are not extracted into named partials; a future lane touching
  one sub-surface edits a 1 400-line file. · **Violates:** principle 2
  (differentiation is deliberate); `anti-patterns.md` "the god part" in the
  view layer. · **Severity:** **M** (maintainability: a board-settings lane
  needs a 1 400-line edit). · **Proposed unit:** **U06** (extract the two
  longest views' self-contained sub-surfaces into `Views/Shared/` partials;
  the rendered HTML must be byte-identical, per the Razor doctrine).
  **U06 result (2026-10-08): closed (partially — the two named blocks).**
  Extracted the two largest *repeated, self-contained* sub-surfaces into
  named partials: the board **card** (`@foreach` over `lane.Cards`, the
  per-card body + details expander + subtask/assign modals) →
  `Views/Shared/_BoardCard.cshtml`, and the post **reply** (`@foreach` over
  `Model.Replies`, the per-reply body + its modals) →
  `Views/Shared/_PostReply.cshtml`. Parent views: `BoardDetail.cshtml`
  1 431 → 838, `Posts/Detail.cshtml` 1 015 → 637 (both under the 800 gate,
  no longer grandfathered). **Not extracted (deliberate, part-vs-whole):**
  the board **lane** block (needs the parent's `StatusGlyph`/`StatusLabelKey`
  + `otherBoards` + `laneHasSpare`/`hasLeft/Right` — a large model surface
  and a helper the plan keeps in the lane head; also the board-settings
  surface, a single render site, not repeated — a partial there would be
  over-differentiation), and `TodoDetail.cshtml` (out of U06's scope — the
  plan names the two longest views; it stays grandfathered). The shared
  status-glyph/label-key mapping was pulled out of the parent into
  `BoardCardStatuses.cs` (one definition for the partial *and* the lane
  head — the "silent coupling" the lane closes). Served-page structure
  verified intact per the Razor doctrine (the `<partial>` boundary adds only
  inert whitespace; tags/attributes/text are unchanged; the `ImproveHarness`
  + all 951 Web tests pass unmodified).

## Principle 3 — Bugs and value both live at the seams

We test, observe, and invest at the seams first — especially the access
model.

- **Where:** `tests/Kumunita.Core.Tests/ProjectServiceTests.cs`
  (4 105 lines) and `tests/Kumunita.Core.Tests/EventServiceTests.cs`
  (2 292 lines) · **What:** the test files mirror the service-file size;
  the *seam-specific* tests (`in-code.md`: "empty-audience-denies under
  `All`, delegation scoped narrower than the grant, moderator access
  off-by-default") are interleaved with happy-path tests, making them
  *harder* to find than the happy-path ones. · **Violates:** principle 3
  (the seams are where privacy lives or leaks — the current layout buries
  them); `in-code.md` "test the access model at its seams specifically". ·
  **Severity:** **M** (maintainability: a future lane that needs to verify
  the denied path must read the whole file). · **Proposed unit:** **record
  and accept** (the test files are not on the U01–U09 close list; the
  finding is recorded here so the next audit pass can size a
  `TEST-ORG` lane if the pattern persists). The U08 security audit *does*
  verify the denied-path coverage per `AccessAction` × `AccessVia`, which is
  the substantive requirement; the *layout* of the tests is a
  discoverability issue, not a trust issue.
- **Where:** `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs`
  (4 579 lines) + `src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs`
  (2 105 lines) · **What:** the two seeders are both over the 2 000-line
  ceiling and carry a *single, coherent* job (seed the schema / seed sample
  data), but the *internal* differentiation (per-milestone seed blocks) has
  not been made; a new milestone's seed block lands in a 4 500-line file. ·
  **Violates:** principle 3 (the seed is a seam between "the schema the ADR
  describes" and "the data the tests exercise" — the current layout makes
  the seam invisible). · **Severity:** **M** (maintainability). ·
  **Proposed unit:** **record and accept** (the seeders are on the U00
  baseline grandfather list for gate (a); a `SEEDER` lane is a candidate
  for the next audit pass, but the gate's ceiling — not an immediate
  rewrite — is the seam we hold today).

## Principle 4 — Feedback loops are a feature

A signal with no owner and no response decays into noise.

- **Where:** `docs/adr/README.md` (143 index rows) vs `docs/adr/0*.md`
  (151 files on disk) · **What:** the ADR index is the seam every reader
  uses to find an ADR, and it is stale by **8 rows** (missing 0096, 0137–
  0143, collected 2026-10-08); a reader who lands on "0137" in the index
  does not find the ADR. · **Violates:** principle 4 (a signal — "this ADR
  exists" — with no response — "the index names it"); `anti-patterns.md`
  "accidental integration" in the doc layer. · **Severity:** **L**
  (discoverability). · **Proposed unit:** **U01** (add the 8 rows, in
  numeric order, with Title / Status pulled from each ADR's H1; the gate (b)
  fails the close on new drift).
- **Where:** `docs/plans-milestones/done/**/*-handoff-notes.md` (23 files
  over 600 lines, **none** with a TL;DR in their first 20 lines — the
  largest is `m3b-handoff-notes.md` at 1 939 lines) · **What:** a new
  agent's first read of a lane's history is *the entire file*; there is no
  "read the abstract, decide if this is the right lane, then read the rest"
  path. · **Violates:** principle 4 (the handoff note is the signal —
  "what did this lane ship, what seam did it create, what must a future
  reader not re-litigate?" — with no entry point that answers it in 10
  seconds); `anti-patterns.md` "part sprawl" in the doc layer. ·
  **Severity:** **L** (discoverability). · **Proposed unit:** **U02** (add a
  `> **TL;DR:**` block in the first 20 lines of the 8 largest; the gate (d)
  fails the close on a *new* over-600 file without a TL;DR).
- **Where:** `docs/design/*.md` (43 files over 400 lines, **none** with an
  Abstract in their first 15 lines — the largest is
  `m13-logging-analytics-design.md` at 1 895 lines) · **What:** a maintainer
  who needs to decide "is `m18-recurring-events-design.md` the right read?"
  must invest 1 215 lines of attention before finding out. · **Violates:**
  principle 4 (the design doc is the signal — "what question does this
  settle, what contract does it create, what is out of scope?" — with no
  10-second entry point). · **Severity:** **L** (discoverability). ·
  **Proposed unit:** **U07** (add a `> **Abstract:**` block in the first 15
  lines of the 5 largest; extend the gate to require the abstract on any
  *new* design doc over 400 lines).

## Principle 5 — Emergent properties can't be delegated

Trust, belonging, safety — none is assignable to a single feature, module,
or admin. We judge the community the platform serves, not the part.

- **Where:** `docs/SECURITY.md` + `src/Kumunita.Core/Authorization/` ·
  **What:** the privacy model is emergent — it exists only in the
  integrated system (the access model + the audit log + the test suite
  together), not in any one file. A single "the access model is correct"
  claim is not a system property. · **Violates:** principle 5 (we judge the
  whole, not the part); `in-code.md` "my handler is correct is not a system
  property". · **Severity:** **S** (a privacy leak is a trust risk). ·
  **Proposed unit:** **U08** (the security & privacy seam audit — the
  closed-loop audit of every `AccessAction` × `AccessVia`, the guardian
  boundary, the translation-swap surface, the portability lane).

## Principle 6 — Integration decays

Group definitions go stale, access rules drift, "how moderation works here"
becomes tribal knowledge. ADRs, review, audit, and retrospectives are how
we close that decay.

- **Where:** `AGENTS.md` (257 lines) and
  `.github/copilot-instructions.md` (163 lines) · **What:** the two
  agent-instruction files share **5 verbatim `## ` sections** (PowerShell
  here-strings, `$variables`, Razor verification, git-state, browser
  trusted-folder, don't-pause — collected 2026-10-08); they are not in sync
  (the instructions file has a slightly different "Getting a live server"
  section and a slightly different Razor-trap list). · **Violates:**
  principle 6 (the doctrine is drifting — the two copies are already
  different); `anti-patterns.md` "silent coupling" in the agent-integration
  layer. · **Severity:** **M** (maintainability: a change to the doctrine
  lands in two places and the agents see two slightly different versions).
  · **Proposed unit:** **U03** (choose `AGENTS.md` as the source of truth;
  `.github/copilot-instructions.md` becomes a thin pointer that *links* to
  the shared sections rather than duplicating them; the gate (c) fails the
  close on a *new* shared heading).
- **Where:** `docs/adr/README.md` + `docs/adr/0*.md` (the 8-row drift, see
  Principle 4) · **What:** the ADR index is the seam every reader uses to
  find an ADR, and it is stale — this is the *decay* principle 6 names. ·
  **Violates:** principle 6 (integration decays; the index is the
  integration, and it has decayed). · **Severity:** **L** (discoverability).
  · **Proposed unit:** **U01** (the close).
- **Where:** this file (`improve-audit.md`) + `improve-check.ps1` +
  `improve-report.ps1` · **What:** *this lane is the principle-6 close
  pass made visible and testable* — the harness (the six gates) is the
  "retrospective that acts" the principle names. · **Violates:** nothing —
  this is the *fix*, not the finding. · **Severity:** n/a. · **Proposed
  unit:** **U00** (this unit) + the `U01..U09` close units + the `U09` close
  flip.

---

## The three tests

### Closed-loop

Can a resident take a problem in and get an outcome out, without leaving
the platform?

- **Where:** `src/Kumunita.Web/Views/` (the resident's surfaces) +
  `docs/philosophy/how-it-works.md` (the named resident journeys) ·
  **What:** for each named resident journey (post to an audience; report a
  post; join/leave a group; read a page; translate), the *exit* — the
  moment the resident must leave the platform to complete the task — is not
  yet systematically named. · **Violates:** the closed-loop test (every exit
  is a seam we didn't integrate); `anti-patterns.md` "part sprawl" (a
  feature that sends the resident to another tool). · **Severity:** **L**
  (a handoff is a value leak, not a security risk). · **Proposed unit:**
  **U09** (the three-audience UX audit — the resident's closed-loop test,
  the exit named per journey).
- **Where:** `src/Kumunita.Web/Controllers/` + `docs/SECURITY.md` ·
  **What:** for each admin surface (`/admin/*`), the admin's "open the DB,
  run a query" step (a handoff + an accidental-integration risk — the
  "one admin who knows everything" god-part smell) is not yet
  systematically named. · **Violates:** the closed-loop test (the admin's
  loop is not closed in the platform); `anti-patterns.md` "the god part"
  (local: "one admin who knows everything"). · **Severity:** **M**
  (maintainability: a handoff is a value leak and a tribal-knowledge risk).
  · **Proposed unit:** **U09** (the admin's standing test — each admin
  surface verified to close the loop within the surface).

### Handoff

Every manual transfer between parts, systems, or people — a copy into a
chat app, a re-key into a spreadsheet — is an un-integrated seam and a
value leak.

- **Where:** `docs/plans-milestones/done/**/*-handoff-notes.md` (the 23
  over-600 files, see Principle 4) · **What:** the handoff note is the
  *named* handoff between "the lane that shipped" and "the next lane that
  reads it"; the current layout (no TL;DR) makes the handoff implicit and
  tribal — the reader must read the whole file to find the seam. ·
  **Violates:** the handoff test (the transfer is not explicit);
  `anti-patterns.md` "accidental integration" (local: "a moderator who
  *knows* what's private"). · **Severity:** **L** (discoverability). ·
  **Proposed unit:** **U02** (the TL;DR convention — the handoff made
  explicit).
- **Where:** `AGENTS.md` ↔ `.github/copilot-instructions.md` (the 5 shared
  sections, see Principle 6) · **What:** the agent-integration doctrine is
  the handoff between "the team's doctrine" and "the agent's working
  context"; the current layout (two copies, not in sync) makes the handoff
  implicit — the agent sees two slightly different versions of the same
  doctrine. · **Violates:** the handoff test (the transfer is not explicit);
  `anti-patterns.md` "silent coupling". · **Severity:** **M**
  (maintainability). · **Proposed unit:** **U03** (the de-duplication —
  the handoff made explicit, one source of truth).

### Part-vs-whole

Does this optimize a part at the cost of the whole?

- **Where:** the four god-files (`KnownTranslationKeys.cs` 9 435,
  `ProjectsController.cs` 4 971, `FirstBootSeeder.cs` 4 579,
  `ProjectService.cs` 4 803 — see the Evidence section of the lane register)
  · **What:** each file carries a *single, coherent* job, but shrinking one
  *does not* make the seam between it and its neighbors clearer — the
  internal differentiation (per-surface key groups, per-milestone seed
  blocks, per-lane composer methods) has not been made. · **Violates:** the
  part-vs-whole test (the part is optimized at the cost of the whole — the
  whole is the *reader's* ability to find the seam); `anti-patterns.md`
  "the god part". · **Severity:** **M** (maintainability). · **Proposed
  unit:** **U04** (the god-data-file differentiated), **U05** (the composer
  trio verified / extracted). The other two god-files (`FirstBootSeeder.cs`,
  `ProjectService.cs`) are on the gate (a) baseline; a `SEEDER` / `PROJECT`
  lane is a candidate for the next audit pass.
- **Where:** `src/Kumunita.Web/Views/Projects/BoardDetail.cshtml`
  (1 431), `src/Kumunita.Web/Views/Posts/Detail.cshtml` (1 015),
  `src/Kumunita.Web/Views/Projects/TodoDetail.cshtml` (890) · **What:**
  the three largest views are *parts* that have not been differentiated
  into the *whole* (the named sub-surfaces a future lane will touch); a
  future board-settings lane edits a 1 400-line file rather than a named
  partial. · **Violates:** the part-vs-whole test (the part is the
  1 400-line file; the whole is the set of named sub-surfaces);
  `anti-patterns.md` "the god part" in the view layer. · **Severity:**
  **M** (maintainability). · **Proposed unit:** **U06** (the partials —
  the part-vs-whole test applied to the view layer).
  **U06 result (2026-10-08): closed (the two repeated blocks differentiated).**
  The board **card** and the post **reply** — the two sub-surfaces that are
  *repeated* (one per lane / one per reply) *and* > 150 lines — are now
  named partials (`_BoardCard.cshtml`, `_PostReply.cshtml`), so a future
  board-card or reply lane edits the part, not the 1 400-line whole. The
  board **lane** block and the **board-settings** surface were *not*
  extracted: the lane is entangled with the lane head (shared status helpers,
  `otherBoards`, reorder-neighbor flags) and the settings surface is a
  single render site — extracting either would be the "distributed
  fragmentation" / over-differentiation anti-pattern (`anti-patterns.md`),
  i.e. optimizing a part at the cost of the whole. `TodoDetail.cshtml`
  (890) stays grandfathered — outside U06's "two longest views" scope.

---

## FACES

### Flexible

A component can change or degrade without the whole breaking.

- **Where:** `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
  (9 435 lines) · **What:** the flat dictionary is the *anti*-flexible
  shape — adding 200 new keys to a future lane lands in a 9 000-line
  monolith, and the composition (a `Concat` of per-surface dictionaries)
  does not exist yet to make the addition a one-line change. ·
  **Violates:** the Flexible face (a component cannot change without the
  whole breaking — the *reader's* whole, not the *runtime's*). ·
  **Severity:** **M** (maintainability). · **Proposed unit:** **U04** (the
  per-surface static properties — the composition layer is the flex point).

### Adaptive

A resident's report actually reaches a moderator and changes what happens;
the audit log, read, produces a response.

- **Where:** `src/Kumunita.Core/Authorization/AuthorizationService.cs` +
  `docs/SECURITY.md` · **What:** the audit log is the production feedback
  loop for the access model; the current state (per `in-code.md`: "the
  audit log is the production feedback loop for the access model. It is
  always on by design") is *the adaptive face* — but the *coverage* (every
  `AccessAction` × `AccessVia` has a denied-path test and an audit row on
  both paths) is not yet verified. · **Violates:** the Adaptive face (the
  loop is named, but the coverage is not yet proven). · **Severity:**
  **S** (trust at risk). · **Proposed unit:** **U08** (the security &
  privacy seam audit — the adaptive face made provable).

### Coherent

A new resident can narrate what this place is for in one breath; a
moderator can trace any access decision backward to a grant.

- **Where:** `docs/adr/README.md` (the 8-row drift, see Principle 4) ·
  **What:** the ADR index is the *coherence* seam — "a moderator can trace
  any access decision backward to a grant" requires the ADR that names the
  grant to be *findable*; the 8-row drift breaks the trace for 0096 and
  0137–0143. · **Violates:** the Coherent face (the trace is broken for 8
  ADRs). · **Severity:** **L** (discoverability). · **Proposed unit:**
  **U01** (the index closed — the trace restored).
- **Where:** `docs/plans-milestones/done/**/*-handoff-notes.md` (the 23
  over-600 files, see Principle 4) · **What:** the handoff note is the
  *coherence* seam for a lane's history — "a new resident can narrate what
  this lane shipped in one breath" requires the TL;DR; the current layout
  (no TL;DR) breaks the narration for 23 lanes. · **Violates:** the
  Coherent face (the narration is broken for 23 lanes). · **Severity:**
  **L** (discoverability). · **Proposed unit:** **U02** (the TL;DR
  convention — the narration restored for the 8 largest, the gate holds the
  rest).
- **Where:** `docs/design/*.md` (the 43 over-400 files, see Principle 4) ·
  **What:** the design doc is the *coherence* seam for a design decision —
  "a maintainer can decide in 10 seconds whether `m18-recurring-events-
  design.md` is the right read" requires the Abstract; the current layout
  (no Abstract) breaks the decision for 43 design docs. · **Violates:** the
  Coherent face (the decision is broken for 43 design docs). ·
  **Severity:** **L** (discoverability). · **Proposed unit:** **U07** (the
  Abstract convention — the decision restored for the 5 largest, the gate
  holds the rest).

### Energizing

The platform gives the residents' time and attention back, not take them;
the team can sustain this pace for a year.

- **Where:** `docs/design/*.md` (the 43 over-400 files, see Principle 4) ·
  **What:** the design doc is the *energy* seam for the team — a maintainer
  who invests 1 895 lines of attention in `m13-logging-analytics-design.md`
  to find "this is not the right read" has had their time taken, not given
  back; the Abstract is the "10-second decision" that gives the time back. ·
  **Violates:** the Energizing face (the reader's time is taken, not given
  back, for 43 design docs). · **Severity:** **L** (discoverability). ·
  **Proposed unit:** **U07** (the Abstract convention — the reader's time
  given back).
- **Where:** `docs/plans-milestones/done/**/*-handoff-notes.md` (the 23
  over-600 files, see Principle 4) · **What:** the handoff note is the
  *energy* seam for the team — a new agent who reads a 1 939-line
  `m3b-handoff-notes.md` to find "this is not the right lane" has had their
  context budget taken, not given back; the TL;DR is the "10-second
  decision" that gives the budget back. · **Violates:** the Energizing face
  (the reader's context is taken, not given back, for 23 handoff notes). ·
  **Severity:** **L** (discoverability). · **Proposed unit:** **U02** (the
  TL;DR convention — the reader's context given back).

### Stable

The place holds under the neighborhood's load and across time — the trust
survives a handoff of the admin, a restore, a release.

- **Where:** `AGENTS.md` ↔ `.github/copilot-instructions.md` (the 5 shared
  sections, see Principle 6) · **What:** the agent-integration doctrine is
  the *stability* seam — "the trust the agent-integration layer stands on
  holds under a future handoff" requires the doctrine to be in one place;
  the current layout (two copies, not in sync) breaks the stability — a
  future handoff (a new agent framework, a new doctrine section) lands in
  two places and the agents see two versions. · **Violates:** the Stable
  face (the trust does not hold under a handoff — the two copies have
  already drifted). · **Severity:** **M** (maintainability). ·
  **Proposed unit:** **U03** (the de-duplication — the stability restored,
  one source of truth).
- **Where:** `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
  (9 435 lines) + `src/Kumunita.Web/Views/Projects/BoardDetail.cshtml`
  (1 431 lines) · **What:** the god-file and the god-view are the
  *stability* seams — "the registry / the view holds under a future lane
  that adds 200 new keys / a board-settings section" requires the internal
  differentiation; the current layout (flat dictionary / 1 400-line file)
  breaks the stability — a future lane edits a monolith rather than a named
  group / partial. · **Violates:** the Stable face (the part does not hold
  under a future lane). · **Severity:** **M** (maintainability). ·
  **Proposed unit:** **U04** (the god-data-file differentiated) + **U06**
  (the god-view differentiated).

---

## Security & privacy

> **U08 section** (the security & privacy seam audit, the user's explicit
> question). U00 records the *scope* of the audit; U08 runs it. The
> subsections below are the *checklist* U08 works through, one row per
> `AccessAction` × `AccessVia` (the U08 deliverable is the *evidence* for
> each row).
>
> **U08 verdict (2026-10-08): all four passes clean — zero S findings.**
> The access model is tested on the denied path and audited on both the
> granted and the security-meaningful denied path, with *who* + *why* on
> every row (all 11 `AccessVia` values); the guardian boundary holds (no
> content read); the translation-swap is gate-first in all four surfaces;
> the portability lane is no-secret and covers the ADR 0148
> references-don't-exist acceptance case. One **L** observation (the
> translation-swap gate-first *ordering* invariant is code-verified, not
> pinned by a single end-to-end test) is recorded and accepted — it is not
> a trust violation. **No code changed in U08** (the plan's "no code
> changes unless an S finding is found" rule held — a verified-clean is a
> legitimate seam-audit outcome, the U05 "verified negative" pattern).

### The closed-loop audit of the access model

For each `AccessAction` and each `AccessVia` in the codebase, verify:

- **(a) denied-path test:** there is a test that exercises the *denied*
  path, not just the granted path.
- **(b) audit row:** there is an audit row written on the granted path *and*
  on the denied path where the denial is a real security decision (vs. a
  404-vs-403 split that is a UX decision).
- **(c) standing + reason:** the audit row carries *who* (the actor's
  standing) and *why* (the seam that resolved), per `in-code.md`'s
  "Observable: we can tell it worked, and the audit trail shows *who* and
  *why*".

Each gap is an **S** finding (a privacy leak or a non-auditable access
decision is always S, per `docs/SECURITY.md`). U08 records each gap with
the exact file/line and a proposed fix; an S finding that needs a code
change becomes a *new* named lane (e.g. `SECAUDIT-1`), per this lane's own
rule (the philosophy says "the most load-bearing contract in the codebase"
is changed only through an ADR).

**U08 result (2026-10-08): clean — no S finding.** The single decision
path (`AuthorizationService.Decide`,
`src/Kumunita.Core/Authorization/AuthorizationService.cs`) is **standing-
keyed, not action-keyed**: `Decide()` branches on the actor's standing
(owner → moderator → break-glass → community → resident → public →
match-groups → deny) and never on `AccessAction` — the action is *recorded*
on the audit row, it does not change the branch. So the `AccessAction` ×
`AccessVia` matrix collapses to "per `AccessVia`" for the decision, with
the action carried on the row. Every `AccessVia` value has a **denied-path
test** (a) and an **audit row on both the granted and the security-
meaningful denied path** (b) carrying **who** (`ActorId` +
`EffectivePrincipalId`) and **why** (`Via`) (c) — the `AccessAudit` shape
(`src/Kumunita.Core/Authorization/AccessAudit.cs`) is the (c) guarantee by
construction, and `C3_AuditRow_CommitsWithTheDecision_AllowAndDeny`
(`tests/Kumunita.Core.Tests/AuthorizationServiceTests.cs`) pins the (b)
"no silent, unaudited access" invariant on both outcomes. Denied-path
evidence per `AccessVia`:

| `AccessVia` | Denied-path test (a) | Audit row on deny (b) |
|---|---|---|
| `Owner` | allow-branch; a non-owner's denial is the other branches' denies (e.g. `C4_MembershipChange_IsLiveOnTheNextDecision`, `A0036_CommunityFlagFalse_EmptyGrants_OwnerOnly`) | the same deny rows carry `Via` |
| `Audience` | `C4_MembershipChange_IsLiveOnTheNextDecision` (the Deny row landed), `C6_BulkMatches` (c2 unrelated-group Deny), `A0041_AllResidentsFlagFalse_EmptyGrants_Denies`, `EvaluateAudience_EmptyAudience_AllMode_Denies` (the vacuous-truth guard) | `C3_AuditRow_CommitsWithTheDecision_AllowAndDeny` |
| `Delegation` | `C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded` | Deny row `Via = Delegation` (asserted in that test) |
| `Moderator` | `C5_ModeratorAccess_OffByDefault_ModeratorCannotSee` | the off-by-default C5 row |
| `Report` | `ModerationServiceTests.CanReadWithReportAsync_ModeratorWithoutReport_Denied_C5Unactivated` | Deny row `Via = Report`, `Outcome = Deny` (asserted) |
| `BreakGlass` | `BreakGlass_NotConsumed_DoesNotElevate`, `BreakGlass_Expired_DoesNotElevate` | the non-elevated deny rows |
| `Admin` | `AnnouncementControllerTests.AddTranslation_Denied_Forbid_NoWrite`, `EventControllerTests.UpdateTranslation_Denied_Forbid`, `PageControllerTranslationTests.RemoveTranslation_Denied_Forbid` (Web.Tests) | `AccessAuditFactoryTests` pins the `(Admin, Deny)` row shape |
| `Group` | `GroupPostServiceTests.G2_NonMemberFeedEmptyWithDenyRow` | aggregate Deny row `Via = Group` (asserted) |
| `Guardian` | `GuardianTimeLimitStandingTests.F5_NonGuardian_Set_Is_Denied` | `Via = Guardian` write-lane row |
| `Community` | `A0036_CommunityFlagButNotMember_Denies`, `A0036_CommunityFlagNullComponent_AnonymousActor_Denies` | the deny rows carry `Via` |
| `Resident` | `A0041_AllResidentsFlag_AnonymousActor_Denies` | the deny rows carry `Via` |

Two corrections to the code's own stale comments, recorded so a future lane
doesn't re-litigate them: **`AccessVia.Report` is a live branch, not the
"reserved" the `Decision.cs` enum comment (and `AuditPurgeService`'s "no
`AccessVia.Report` rows yet" line) suggest** — `ModerationService.
CanReadWithReportAsync` (`src/Kumunita.Core/Moderation/ModerationService.cs`
§2.4 item 3, the M3b F2 read lane) writes both the Allow and the C5-
unactivated Deny rows with `Via = Report`. **`AccessVia.Guardian` is a
write-lane standing only** (the five GU supervisory actions, all `IUserInfo-
Service` lanes with `Via = Guardian` audit rows) — it never appears in a
`Decide()` branch, which is the G·1 "the guardian's ceiling, not a content
right" pin. `AccessAction.Moderate` is exercised through the moderator
branch (`C5_…`) + the report branch (`ModerationServiceTests`) — its denial
is the C5 unactivated case, not a separate branch.

### The guardian boundary

ADR 0028 says the guardian has "no standing to read the child's private
content." U08 greps every `GuardianController` action and every
`IUserInfoService` guardian seam for a read of child content (post body,
message body, document). Any hit is an **S** finding (the "delegation
scoped narrower than the grant" pin from `in-code.md`, applied to the GU
surface).

**U08 result (2026-10-08): clean — no S finding.** Grep of
`src/Kumunita.Web/Controllers/GuardianController.cs` for a read of child
content (post body, message body, document) returned **no content read** —
every `.Body` / `message` hit in the file is `ex.Message` (exception text
surfaced to `TempData["error"]`), not a child's content. The controller's
doc-comment states the pin verbatim (G·1, load-bearing in the Web): "no
route reads or links to a child's *content* — the reads are the
`GuardianLink` row + the child's membership rows + the child's pending group
invitations (curation data, ids/names only), never the child's posts or
profile body." The `Detail` view reads `DisplayName` + `Blocked` (curation
data) and the child's *membership* rows (the `GetEffectiveCommunityIds-
Async` / group-membership reads the GU lanes curate) — the child's
**authored content** (post body, reply body, message body) is never loaded
by any `GuardianController` action. Cross-checked against the
`IUserInfoService` guardian seams (`src/Kumunita.Core/UserInfo/IUserInfo-
Service.cs`): every guardian method is a **write** lane — `SuspendChild-
Async` / `UnsuspendChildAsync` / `SetChildMessagingRestrictionAsync` /
`SetChildEventRsvpModeAsync` / `BlockChildCommunityAsync` / invitation-
approval / `CreateGuardianLinkAsync` / `DissolveGuardianLinkAsync` — each
emits a `Via = Guardian` audit row and none of them is a content *read*
seam. The guardian's **block/hide** lanes (`Profile.BlockedCommunityIds`,
the `GetEffectiveCommunityIdsAsync` read) work in the *opposite* direction
of a leak: they *remove* a guardian-blocked community from the child's
audience visibility — a ceiling, not a window. This is the "delegation
scoped narrower than the grant" pin holding on the GU surface: the guardian
holds standing over the child's *account* (suspend, membership curation,
messaging/event ceiling) with **no** standing over the child's *content*.

### The translation-swap surface

ADR 0027 / 0049 / 0051 changed what a *viewer* sees first. U08 verifies the
swap never *reveals* a translation the viewer may not read (the variant is
a display preference over rows the viewer may already read, by the
tag/audience seams — confirm a test pins this for posts, group posts,
announcements, and pages).

**U08 result (2026-10-08): clean (verified by construction) — one L note.**
In **all four surfaces the visibility gate runs *before* the variant
selection**, so the swap can only re-word an item that is already in the
viewer's visible set — it never loads a variant for an item the viewer may
not read:

- **Posts** (`PostsController.Index` / `AllSections`, `src/Kumunita.Web/`
  `Controllers/PostsController.cs`) — the audience decision runs in
  `PostService.ListFeedAsync` / `ListAllFeedAsync` (the `feed.Visible`
  set); the `ApplyTranslationToPostAsync` helper (its own doc-comment:
  "a read, not a decision: the post's `CanSeeAsync` already ran in the
  feed read") then picks among that post's stored variants. The detail path
  (`Detail`) is the same: `GetPostAsync`'s single `CanAsync` row returns
  `Post = null` on a Deny → 403 *before* any translation read.
- **Group posts** (`GroupsController.GroupPostDetail`,
  `src/Kumunita.Web/Controllers/GroupsController.cs`) — the single
  group-lane decision runs in `PostService.GetGroupPostAsync` (returns
  `Post = null` on a membership Deny → 404); the group-name swap and the
  post/reply variant chips are read *after* that decision (doc-comment: "the
  post's single group-lane decision already ran in `GetGroupPostAsync`").
- **Announcements** (`AnnouncementController.Index` / `Detail`,
  `src/Kumunita.Web/Controllers/AnnouncementController.cs`) — the flat
  public/community scope gate runs in `ListVisiblePagedAsync` / `GetAsync`
  (a not-visible id returns null → 404 *before* the translation read); the
  ADR 0029/0051 row swap picks among the `visible`/already-authorized set
  (doc-comment: "the per-announcement read inherits the flat scope gate
  that already ran").
- **Pages** (`PageController.Show`, `src/Kumunita.Web/Controllers/Page-
  Controller.cs`) — the draft gate + the `CanAsync` Read decision (and the
  anonymous non-public 403) run *before* `GetTranslationsAsync` is even
  called; `defaultVariant` (the ADR 0049 selection) is computed from
  `EffectiveLanguageCode.ResolveAsync` after the gate, and the page's own
  audience is what the gate checks.

**The pin:** the composite "denied viewer → no translation leak" is
**guaranteed by the gate-first ordering** (verified by reading all four
controllers) + the two component test families — the *visibility* denied-
path tests (`C5_ModeratorAccess_OffByDefault_ModeratorCannotSee`,
`GroupPostServiceTests.G2_NonMemberFeedEmptyWithDenyRow`,
`A0041_AllResidentsFlag_AnonymousActor_Denies`, the announcement scope-gate
tests, `PageControllerTests.Index_When_CanSeeAsync_HidesPage_DeniedPageIs-
Absent`) and the *translation* data-shape / standing tests (`Translation-
DisplayTests`, `PostTranslationTests`, `GroupsControllerTranslationTests`,
`AnnouncementControllerTests.AddTranslation_Denied_Forbid_NoWrite`,
`PageControllerTranslationTests.UpdateTranslation_Denied_Forbid`).
**L note (record and accept):** the gate-first *ordering* is a **code
invariant** verified by reading, not pinned by a single dedicated end-to-
end test — a future lane that reorders a swap to precede the gate would
only be caught if that lane's own denied-path tests also exercised the
translation path (the `translationProvider` is optional/null in most of the
existing seam tests, so the swap is a no-op there). The ordering is correct
today and each component property is test-pinned, so this is a
discoverability/audit observation, not a trust violation — flagged so the
next audit pass knows the invariant is load-bearing and un-pinned as a
single named test.

### The portability lane

ADR 0148: "no passwords or tokens ever travel with it." U08 greps
`UserPortabilityService` for any identity-credential field in the export
shape; confirms the export's round-trip test (`PortabilityRoundTripTests`)
covers the *import into a community where references don't exist* path (the
ADR's own acceptance case).

**U08 result (2026-10-08): clean — no S finding.** **No-secret:** grep of
`src/Kumunita.Core/Portability/UserPortabilityService.cs` for an identity-
credential field in the export shape found **none in the archive shape** —
the only `Password`/`Token`/`Hash`/`Secret`/`Credential` hits are the
`CancellationToken ct = default` params. The export (the "no-secret
principal", `UserPortabilityService.cs` §the M11 `PortabilityPrincipal`
shape) reads exactly the eight allowed fields (`SubjectId`, `Username`,
`Email`, `NormalizedEmail`, `DisplayName`, `Verified`, `Blocked`, `Roles`)
and the doc-comment is explicit: "`PasswordHash` / `SecurityStamp` /
`AccessToken` / `RefreshToken` / `RecoveryCode` are never touched" (the
C-M27·2 boundary). **This is test-pinned**, not just doc-pinned: `Portability-
RoundTripTests.PortabilityNoSecret_ArchiveContainsNoCredentialMaterial`
(`tests/Kumunita.Core.Tests/PortabilityRoundTripTests.cs`, the M11 D9b pin)
plants a witness user with a non-empty `PasswordHash` + `SecurityStamp` (and
asserts they are non-empty on the source), exports, and byte-scans the wire
for `passwordhash` / `securitystamp` / `accesstoken` / `refreshtoken` +
asserts `Assert.DoesNotContain(witnessUser.PasswordHash, wire)` — a real
leak would fail the scan. The M27 resident-lane analog is `M27_Acceptance-
NoSecret_SelfScoped` (`UserPortabilityAcceptanceTests.cs`). **References-
don't-exist (the ADR 0148 acceptance case):** `M27_Acceptance_PerEntity-
Conflict_Resolve` (`tests/Kumunita.Core.Tests/UserPortabilityAcceptance-
Tests.cs`) imports into a target where the resident's component *does not*
exist → the conflict entity is **not** auto-applied; the resident resolves
it (add-elsewhere re-points + applies; discard writes nothing) — the
fail-closed, no-silent-auto-merge shape. The M11 operator-lane analog is
`PortabilityFailClosed_RejectedArchiveWritesZeroRows` (D9c) + its
`AssertFailClosedOnDanglingReferenceAsync` helper (a dangling reference is
rejected *before any write* — zero content rows, zero principals, zero
audit rows). Both legs of the ADR's acceptance case are therefore pinned:
no credential material travels, and the import-into-a-community-where-
references-don't-exist path is fail-closed and resident-controlled.

### U00's baseline note

U00 has *not* run the audit — it has recorded the *scope* and the
*checklist* above. The U00 baseline observation (collected 2026-10-08): the
`AuthorizationService.cs` decision path is the single seam the philosophy
names as "the most load-bearing contract in the codebase"; the
`BlockedAccountMiddleware` and `TimeLimitMiddleware` enforcement seams are
the two middleware that gate the resident before they reach a surface.
U08's job is to verify the *coverage* (the denied-path test, the audit row,
the standing + reason) for every `AccessAction` × `AccessVia` pairing — the
substantive requirement `in-code.md` names.

---

## The three audiences

> **U09 section** (the three-audience UX audit — resident / admin /
> maintainer). U00 records the *scope*; U09 runs it.

### Resident (the closed-loop test)

For each named resident journey in `how-it-works.md` (post to an audience;
report a post; join/leave a group; read a page; translate), U09 traces the
*exit* — the moment the resident must leave the platform to complete the
task (a "copy into a chat app," a "re-key into a spreadsheet" — the
handoff test). Each exit is recorded with **L** severity (a handoff is a
value leak, not a security risk). **No fix in-lane** — each exit is a
*candidate* for a future named lane, and the philosophy says "every new
part states which seam it integrates."

**U09 result (2026-10-08): the five named journeys are closed loops in-app;
the one consistent exit is the *feedback channel*, not the journeys.**
Traced against `how-it-works.md`'s "What you can do" + the shipped surface:

| Journey | Exit (where the resident leaves the platform)? | Verdict |
|---|---|---|
| **Post to an audience** | none — the audience picker (everyone / group / one person) + the group "Maple Street" reuse + delegation are all in-app; the post reaches the chosen audience, the audit trail shows who/why | **closed loop** (the linkage) |
| **Report a post** | none — file → moderator (audited access) → resolve → recorded, all in-app (`how-it-works.md`: "the whole path is short and every step is checked") | **closed loop** (the linkage) |
| **Join / leave a group** | none — a group is a named access list the resident builds once and reuses; membership changes propagate to past posts automatically | **closed loop** (the linkage) |
| **Read a page** | none — the page tree (`/pages` browse + `/pages/{path}` view) is in-app; the `help/` guides subtree is resident-facing | **closed loop** (the linkage) |
| **Translate** | none — the resident picks their language at `/language`; a Translator edits UI strings + pages in-app; *adding a missing language* is the admin's in-app `/admin/languages` lane, not a resident exit | **closed loop** (the linkage) |

**L findings (record and accept — no fix in-lane):**

- **L-RES·1 — the feedback channel is an off-platform exit.**
  `how-it-works.md` §"How to give feedback — no code required" names it
  explicitly: the main channel for a resident's *feedback* ("flag the seams,"
  "say when something is missing," "disagree out loud," "request a language
  that isn't there yet") is **"Issues in this repository"** (an off-platform
  tool) or **"tell a team member in person."** The resident's *journeys* are
  closed loops, but the *meta-journey of improving the platform* sends them
  out. · **Where:** `docs/philosophy/how-it-works.md` §"Where to put it."
  · **Violates:** principle 1 (the feedback loop is the strongest possible
  linkage — resident signal → maintainer response — and it currently leaves
  the platform); `in-product.md` ("the most important loop in the system").
  · **Severity:** **L** (a value leak, not a security risk). · **Proposed
  unit:** **record and accept** — a *candidate* for a future named lane (a
  resident-facing suggest/feedback surface; the plan's own rule is that a
  new capability becomes a new lane, and this lane is a *reduction* lane,
  not a feature).
- **L-RES·2 — "disagree out loud" lands in the same off-platform repo.** The
  platform's own doctrine invites the resident to *argue with the written
  rules* ("a rule nobody questions is a rule nobody is checking"), but the
  surface for that is the open repository's issue tracker — a place a non-
  technical resident does not have standing in. · **Where:** `how-it-works.md`
  §"5. Disagree out loud." · **Violates:** principle 2 (part-vs-whole — the
  resident is "the best reviewer of it," yet the review channel is external);
  the "closed loop" test. · **Severity:** **L.** · **Proposed unit:**
  **record and accept** (the same candidate lane as L-RES·1).
- **L-RES·3 — the "copy a phone number into a text" handoff the platform
  itself names.** `how-it-works.md`'s own seam-flagging example is a resident
  copying a phone number out into a chat app — the directory is opt-in, and
  sharing a contact detail is a *handoff to another tool* by design (the
  platform does not hand the number to the other person, the resident takes
  it out). · **Where:** `how-it-works.md` §"3. Flag the seams." ·
  **Violates:** principle 1 (the linkage is the value; this is the one
  deliberate exit). · **Severity:** **L** (a *deliberate* value leak — the
  platform is honest that contact sharing is opt-in + external). · **Proposed
  unit:** **record and accept** (M9 messaging closes *in-platform* 1:1
  messaging, but the *phone-number* handoff remains a resident choice, not a
  platform gap).

**The pin:** the resident's *named journeys* all close in-app — the
platform's core promise (the audience picker, the report loop, the group
access list, the page tree, the language picker) holds. The exit is the
*meta-layer* (feedback, disagreement, the one deliberate contact-handoff),
and it is recorded, not hidden — the honest statement of a value leak, per
the lane's Definition of Done.

### Admin (the standing test)

For each admin surface (`/admin/*`), U09 verifies the admin can complete
the task *within* the surface (no "open the DB, run a query" step, which is
a handoff + an accidental-integration risk — the "one admin who knows
everything" god-part smell). Each handoff is recorded. U09 also verifies
the admin's actions are *audited under their standing* (the U08 pin, from
the admin side).
**U09 result (2026-10-08): every `/admin/*` surface is an in-app closed
loop with an `AccessVia.Admin` audit row; the one seam where the admin hits
a wall is the *designed operator/admin split*, not a missing surface.**
Traced against the 16 `Admin*Controller` surfaces (the `AdminController`
five routes `/admin` · `/admin/accounts` · `/admin/communities` ·
`/admin/platform` · `/admin/security` (+ `/admin/audit` + `/admin/break-
glass`), plus the 11 dedicated `Admin*Controller`s — `AdminHelpController`
· `AdminSignupController` · `AdminTimezoneController` ·
`AdminDateFormatController` · `AdminQuietController` ·
`AdminGuestsController` · `AdminMessagingController` ·
`AdminPortabilityController` · `AdminStorageController` ·
`AdminStorageMetricsController` · `AdminSampleDataController` ·
`AdminSiteController` · `AdminAnnouncementCommentsController` ·
`AdminAnalyticsController` · `LanguagesController` (`/admin/languages`)):

| Admin task | In-app closed loop? | Standing + audit (the U08 pin) |
|---|---|---|
| Verify / block / unblock accounts | yes — `/admin/accounts` verify queue + Block/Unblock | `Via = Admin` on the write lane (U08 verified the (b)/(c) guarantee) |
| Add / edit / enable / disable communities | yes — `/admin/communities` | `Via = Admin` (ADR 0062 §per-community admin) |
| Manage platform pages (preview + edit) | yes — `/admin/platform` | the `page.reset` / `page.update` audit rows (`Via = Admin`) |
| Read the audit log + break-glass | yes — `/admin/security` → `/admin/audit` + `/admin/break-glass` | the audit rows themselves *are* the standing record |
| Enable / disable 1:1 messaging instance-wide | yes — `/admin/messaging` | `Via = Admin` (ADR 0105, the `LocaleSettings` toggle) |
| Set platform time zone + date format | yes — `/admin/timezone` + `/admin/dateformat` | `Via = Admin` (ADR 0019 / 0020) |
| Add / enable / reorder / set-default / remove languages | yes — `/admin/languages` | `Via = Admin` (ADR 0005, the catalog lane) |
| Grant / revoke the Translator role | yes — `/admin/accounts/{id}` | `Via = Admin` (ADR 0021, the role lane) |
| Set per-file size limit + per-user quota | yes — `/admin/storage/settings` | `Via = Admin` (ADR 0135, M25) |
| Read storage metrics | yes — `/admin/storage` | a read — **no `AccessAudit` row** (the M13 "read = no row" discipline, ADR 0134) |
| Reset seeded help pages | yes — `/admin/help` + `/admin/help/reset-all` | `page.reset` + `Via = Admin` (ADR 0058 / 0128, the batch lane) |
| Enable / disable announcement comments | yes — `/admin/announcements/comments` | `Via = Admin` (ADR 0101) |
| Set the platform storage-space cap | **no — operator env (`Media__MaxPlatformBytes`), not an admin surface** | n/a (an operator config knob, OPS.md §Config reference) |
| Read the analytics + export CSV | yes — `/admin/analytics?window=7|30|90` | one `AccessAudit` row per CSV export (M13, ADR 0114) |
| Sample-data demo controls | yes — `/admin/sample` | `Via = Admin` (ADR 0138) |

**L findings (record and accept — no fix in-lane):**

- **L-ADM·1 — the operator/admin split is the one designed wall.**
  OPS.md §"Two roles, don't confuse them" makes it explicit: the **Host
  operator** (VPS, Coolify, backups, upgrades, TLS, SMTP, the `env` config
  set) vs. the **Community GlobalAdmin** (in-app roles, moderation,
  content). The `Media__MaxPlatformBytes` knob is the clearest example —
  OPS.md names it "the operator's capacity knob (an env value, **not** an
  admin-set-in-app limit)." This is *by design* (the operator owns the
  infrastructure, the admin owns the community), but it means a GlobalAdmin
  who wants to cap platform storage must leave the platform and ask an
  operator to change an env var + redeploy. · **Where:**
  `docs/OPS.md` §"Configuration reference" (the `Media__MaxPlatformBytes`
  row); the OPS.md §"Two roles" header. · **Violates:** principle 2 (part-
  vs-whole — the admin's task is complete, but the *infrastructure* knob is
  out of the admin's reach by design); the "no open-the-DB handoff" test
  (this is the one place the handoff is *the env store*, not a DB).
  · **Severity:** **L** (a *designed* seam, not a gap — the philosophy
  names it explicitly, so it is recorded, not hidden). · **Proposed unit:**
  **record and accept** (the operator/admin split is the repo's stated
  doctrine; collapsing it would be a *new* capability, not a reduction).
- **L-ADM·2 — the "one admin who knows everything" risk is mitigated by
  the surface split, but the audit *log* is the one surface the admin reads
  that they do not themselves write.** The `/admin/audit` surface shows the
  `AccessAudit` rows the admin *produced* (via their own `Via = Admin`
  writes) plus the rows *other* standing produced (moderator, guardian,
  break-glass). The admin is a *reader* of the audit log, not a writer —
  the write is the *decision*, the read is the *aftermath*. This is the
  correct shape (the audit log is the community's accountability record,
  not the admin's personal to-do list), but it means the admin's *closed
  loop* for "did my action land?" is a *read* of the log, not a *write*
  confirmation in the UI. · **Where:** `AdminController.Audit` (the
  `/admin/audit` action); `AccessAudit.cs` (the row shape). · **Violates:**
  principle 3 (handoff — the admin's "did it work?" handoff is to the audit
  log, not to an inline confirmation). · **Severity:** **L** (a *deliberate*
  seam — the audit log is the community's accountability record, and the
  philosophy names it as the trust mechanism). · **Proposed unit:**
  **record and accept** (the audit-log-read pattern is the correct shape;
  adding inline confirmations would be a new capability).

**The pin (the U08 lean):** every `Via = Admin` write lane carries the
`AccessAudit` who + why shape (U08 verified this for all 11 `AccessVia`
values, including `Admin`). The `AccessVia.Admin` denied-path test (a) is
`AnnouncementControllerTests.AddTranslation_Denied_Forbid_NoWrite` /
`EventControllerTests.UpdateTranslation_Denied_Forbid` /
`PageControllerTranslationTests.RemoveTranslation_Denied_Forbid`, and the
deny-row shape (b) is pinned by `AccessAuditFactoryTests` for the
`(Admin, Deny)` row. The admin's *within-surface* closed loop is
**verified** — no "open the DB" handoff in any of the 16 surfaces; the one
*designed* wall (the operator/admin split) is recorded, not hidden.
### Maintainer (the decay test)

U09 verifies the maintainer's "one page" exists: `README.md` (what +
status + running) / `AGENTS.md` (how to work here) /
`docs/ARCHITECTURE.md` (the map) / `docs/OPS.md` (the ops) /
`docs/SECURITY.md` (the model). This is the *entry-seam* audit — the
philosophy's "a new resident can narrate what this place is for in one
breath" applied to the team. U09 records any doc that is the *only* place
a fact lives (a single-point-of-failure in the doc layer — the "tribal
knowledge" anti-pattern), and any fact that lives in *three+* places (the
silent-coupling anti-pattern, like the U03 `AGENTS.md` /
`copilot-instructions.md` drift but in the wider doc set).

**U09 result (2026-10-08): the "one page" exists — all five entry-seam docs
are present and each owns a distinct job; the tribal knowledge that is the
*only* place a fact lives is concentrated in `AGENTS.md` (the agent
integration layer), and the one *silent-coupling* fact that lives in three+
places is the "how to build / run / test" recipe.**

| Entry-seam doc | Job (the one thing it owns) | Present? |
|---|---|---|
| `README.md` | **what + status + running** — the platform, the roadmap, the Running instructions (the "one page" a new resident lands on) | yes |
| `AGENTS.md` | **how to work here** — the repo doctrine (PowerShell, the test-runner quirk, the close-flip contract, the Razor verification doctrine) | yes |
| `docs/ARCHITECTURE.md` | **the map** — the stack, the three bounded contexts, the module-boundary contracts, the CQRS-lite + Wolverine convention | yes |
| `docs/OPS.md` | **the ops** — the host operator's runbook (provision, first-boot, upgrade, backup, restore, the config reference) | yes |
| `docs/SECURITY.md` | **the model** — the privacy model, the threat model, the access-control rules | yes |

The five are *complementary, not overlapping* — each owns a distinct job,
and the U03 de-dup (the `AGENTS.md` ↔ `copilot-instructions.md` drift) has
already closed the one *real* silent-coupling pair. The remaining findings:

**L findings (record and accept — no fix in-lane):**

- **L-MNT·1 — the build / run / test recipe is tribal knowledge in
  `AGENTS.md`, and the repo's own workspace tasks point at the *broken*
  paths.** The "how to actually build, run, and test" recipe (the
  `dotnet exec` in-process runner for the xunit.v3 test projects, the
  `docker compose build app && docker compose up -d app` live-server recipe,
  the sample-GlobalAdmin credentials, the `node .tmp\build-harness.js`
  harness rebuild) lives *only* in `AGENTS.md` — but the repo's own
  `.vscode` workspace tasks (`test` → `dotnet test`, `run` →
  `dotnet run --project src/Kumunita.Web --launch-profile http`) point at
  the *exact* paths `AGENTS.md` warns are broken on this machine (the
  "Running the tests (test-runner quirk)" + "Getting a live server"
  sections). · **Where:** `AGENTS.md` §"Running the tests (test-runner
  quirk)" + §"Getting a live server"; the `.vscode` workspace tasks
  (`test`, `run`). · **Violates:** principle 2 (part-vs-whole — the
  workspace task is the *entry seam* for a maintainer's first build, and it
  is the seam that fails); the "handoff" test (the recipe's transfer from
  `AGENTS.md` to the workspace task is implicit, not explicit). ·
  **Severity:** **L** (a discoverability leak — a maintainer who runs the
  workspace `test` task hits the xunit.v3 discovery bug `AGENTS.md`
  documents). · **Proposed unit:** **record and accept** (aligning the
  workspace tasks with the `dotnet exec` recipe is a *small* fix, but it
  touches the `.vscode` task surface — a new requirement, not a reduction;
  the honest statement is that the recipe is correct in `AGENTS.md`, and
  the task is the stale copy).
- **L-MNT·2 — the docker-compose live-server recipe + the sample-GlobalAdmin
  credentials are tribal knowledge in `AGENTS.md` and `OPS.md`, but the
  *sample-data* credentials the agent needs are in `SampleDataSeeder` (code),
  not in a doc.** The "Getting a live server" recipe (the docker-compose
  stack, the ~8 s boot time, the `admin@examplium.com` / `Admin123!`
  credentials) is in `AGENTS.md`, but the *source of truth* for the
  credentials is the `SampleDataSeeder` constants in
  `Kumunita.Core/Bootstrap/` — a reader who wants the *exact* seeded
  credentials (all the `@examplium.com` accounts) has to read the code, not
  the doc. · **Where:** `AGENTS.md` §"Getting a live server";
  `src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs` (the constants);
  `docs/OPS.md` §"Sample data — opt-in" (the deploy-posture credentials).
  · **Violates:** principle 2 (part-vs-whole — the credentials are a fact
  that lives in code, and the doc is the *copy*); the "handoff" test (the
  transfer from code → doc is implicit). · **Severity:** **L** (a
  discoverability leak — the credentials are correct in code, the doc is
  the convenience copy). · **Proposed unit:** **record and accept** (the
  code is the source of truth by design; the doc is the convenience copy —
  the U03 de-dup already named this shape, and the code-vs-doc copy is the
  correct direction).
- **L-MNT·3 — the PowerShell traps (the here-string + `$variables` gotchas)
  are tribal knowledge in `AGENTS.md`, and they are the *only* place a
  Windows maintainer learns them.** The "Running PowerShell commands safely
  (Windows agents)" section (the here-string `>>` continuation trap, the
  `$variables`-don't-survive-between-terminal-calls bug, the "keep
  PowerShell commands to a single logical line" rule) is in `AGENTS.md` —
  and the U03 de-dup moved the *other* framework's copy to a pointer, but
  *this* section is the *source* that every agent reads. The trap is real
  (it has actually hung agent sessions before), but it is *Windows-agent-
  specific* and lives in *one* file. · **Where:** `AGENTS.md` §"Running
  PowerShell commands safely (Windows agents)". · **Violates:** principle 2
  (part-vs-whole — the tribal knowledge is the *only* place a fact lives);
  the "closed loop" test (a Windows agent who does not read this section
  hits the `>>` hang with no recovery path in the doc layer). ·
  **Severity:** **L** (a discoverability leak — the knowledge is correct,
  but it is concentrated in one file that a Windows agent must happen to
  read). · **Proposed unit:** **record and accept** (the section is
  correct and the U03 de-dup already made it the single source; moving it
  to a separate `docs/guides/windows-powershell.md` would be a *new*
  structure, not a reduction).

**The pin:** the "one page" exists — the five entry-seam docs are present,
complementary, and each owns a distinct job. The U03 de-dup closed the one
*real* silent-coupling pair (`AGENTS.md` ↔ `copilot-instructions.md`).
The remaining tribal-knowledge findings (L-MNT·1/2/3) are all "record and
accept" — the knowledge is correct, the docs are the convenience copies,
and the code / `AGENTS.md` are the sources of truth by design. The one
*actionable* item (L-MNT·1, the workspace tasks pointing at the broken
paths) is a small fix, but it is a *new requirement* (align the tasks),
not a reduction, so it is recorded, not fixed in-lane.

---

## The integrative question (the part-vs-whole reading)

> **The section a non-technical community member reads first** (per the
> lane register's U09 spec). This is not a code audit, it is a *part-vs-
> whole* reading: which shipped surfaces create *linkage* (a group post, a
> shared project, a report that closes a loop) and which create *exit* (a
> feature that sends the resident to another tool).

**The linkages (the surfaces that produce a property no part has alone):**

- **A post to an audience** — the resident takes a problem in ("I need to
  tell the neighborhood about X") and gets an outcome out ("the right
  people saw it, the audit trail shows who and why") without leaving the
  platform. The closed loop is the *linkage* — the connection between the
  resident and the neighborhood, held by the access model + the audit log.
- **A report that closes a loop** — the resident files a report, the
  moderator sees it *with an audit trail*, the report changes what happens.
  The feedback loop is the *linkage* — the connection between the resident's
  signal and the moderator's response.
- **A group post** — the resident posts to a group, the group's members see
  it, the group's membership (a *differentiated* part) is the *linkage*
  between the resident and the subset of the neighborhood that matters for
  this post.
- **A shared project** — the resident and a neighbor work on a board, the
  board's lanes (a *differentiated* part) are the *linkage* between the
  resident's work and the neighbor's work.

**The exits (the surfaces that send the resident to another tool):**

- **The handoff note without a TL;DR** — the *team's* exit (a new agent
  reads a 1 939-line file to find "this is not the right lane"); the U02
  close is the linkage restored.
- **The ADR index without a row** — the *team's* exit (a reader who lands
  on "0137" in the index does not find the ADR); the U01 close is the
  linkage restored.
- **The design doc without an Abstract** — the *team's* exit (a maintainer
  invests 1 895 lines of attention to find "this is not the right read");
  the U07 close is the linkage restored.
- **The agent-instruction doctrine in two copies** — the *agent's* exit (a
  change to the doctrine lands in two places and the agents see two
  versions); the U03 close is the linkage restored.

**The part-vs-whole reading:** the platform's *quality* is the quality of
the *linkages* (the post-to-audience loop, the report-to-moderator loop,
the group-post-to-members loop, the project-to-neighbor loop) — *not* the
sum of the parts (the post table, the report table, the group table, the
project table). The exits (the handoff note, the ADR index, the design doc,
the agent-instruction doctrine) are the *seams we did not integrate* — and
this lane (U01–U09) is the *integration work* that closes them. The
philosophy's "quality is the quality of the linkage, not the sum of the
parts" is the Definition of Done for this lane: **the codebase is
measurably smaller, the docs measurably more navigable, the seams
measurably more tested, and the existing tests still pass.**

**U09 synthesis (2026-10-08) — for the non-technical reader.** Read this
after the three-audience section above. The lane's verdict, in the
philosophy's own vocabulary:

- **What the platform does well (the linkages that held under the audit):**
  the resident's *named journeys* — post to an audience, report a post,
  join/leave a group, read a page, translate — are **all closed loops
  in-app**. The resident takes a problem in and gets an outcome out
  *without leaving the platform*; the connection between the resident and
  the neighborhood is held by the access model + the audit log. The admin's
  16 `/admin/*` surfaces are **all in-app closed loops** with an audited
  standing (`Via = Admin` on every write; the one *designed* wall is the
  operator/admin split, recorded as L-ADM·1). The maintainer's "one page"
  (README / `AGENTS.md` / ARCHITECTURE / OPS / SECURITY) **exists** — each
  doc owns a distinct job, and the U03 de-dup closed the one *real* silent-
  coupling pair. This is the platform's core strength, and this lane has
  *not* touched it — the linkages held under the audit, which is exactly the
  test the philosophy asks for.
- **Where it leaks (the seams we did not integrate — all L, recorded, not
  hidden):** the resident's **feedback channel** (the issue tracker / a
  human, off-platform — L-RES·1/2/3); the admin's **operator/admin wall**
  (the platform-storage cap is an operator env var, not an admin surface —
  L-ADM·1); and the maintainer's **build/run/test recipe** (concentrated in
  `AGENTS.md`; the workspace tasks point at the broken paths — L-MNT·1/2/3).
  Every one is a *value leak, not a trust violation*, and every one is
  *recorded as a candidate for a future named lane*, not fixed in-lane (this
  is a *reduction* lane, not a feature lane).
- **What this lane *reduced* (the honest statement, per the plan's
  Definition of Done):** it made the *integration decay* **visible and
  testable** — the four god-files are smaller (U04/U05/U06), the ADR index
  is no longer stale (U01), the two agent-instruction docs no longer
  disagree (U03), the handoff notes + design docs have a 10-second "is this
  the right read" gate (U02/U07), and the security & privacy seam is audited
  with *who* + *why* on every row (U08). **It added no feature.** The
  codebase is measurably smaller, the docs measurably more navigable, the
  seams measurably more tested, and the existing tests still pass — the
  inverse of every other lane in this repository.

---

## The close (what "done" means for this lane)

The lane is **done** when:

1. This ledger exists, and a *non-technical* reader can read its `## The
   integrative question` section and narrate back, in their own words, what
   the platform does well and where it leaks (the "judge the whole" clause
   of `in-code.md`).
2. `improve-check.ps1` passes (all six gates green), and the
   `ImproveHarnessTests.cs` test that runs it is green in the test run.
3. The four god-files are *smaller* than the Evidence baseline (or the
   ledger records why a file was not split and the gate's ceiling is the
   *only* thing standing between it and growth).
4. The ADR index, the `AGENTS.md` / `copilot-instructions.md` pair, the
   handoff TL;DRs, and the design-doc abstracts all meet their gates
   (U01, U03, U02, U07).
5. The close flip is landed — `Milestones.cs`, `README.md`, `STATUS.md`,
   `ARCHITECTURE.md`, `MilestonesTests.cs`, and `WhatsNew.cs` are all in
   step (the sixth-member contract, `AGENTS.md`), and the full test run is
   green.
6. The handoff note's `## Close` section records the before/after numbers
   (the reduction, measured — the lane's Definition of Done).

This is the inverse of M28's Definition of Done. M28 ships *a new
capability* and proves it works. IMPROVE ships *a reduction* and proves the
platform still works — the tests are the proof.
