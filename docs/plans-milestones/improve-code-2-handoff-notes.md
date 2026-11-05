---
id: improve-code-2-handoff-notes
title: "IMPROVE-CODE-2 — Handoff Notes"
tier: living
kind: handoff
status: current
audiences: [developer, agent]
lane: improve-code-2
related:
  - docs/plans-milestones/plan-improve-code-2.md
  - docs/adr/0290-improve-code-2-baseline-raises-and-seams.md
  - docs/archive/lanes/improve-code/handoff-notes.md
updated: 2026-11-05
---

> **Abstract:** the running handoff log for the IMPROVE-CODE-2 lane.
> C2-00 recorded the CI-shaped failing and passing gate runs, resolved the
> 30 unjustified raises (the `adr: 0290` named exception record), and confirmed
> all verification gates green. C2-01 collapsed the **Posts** surface (the M3
> reference surface) onto the `TranslationLane` — the six per-surface write
> forwarders retired, the `PostService` class baseline entry gone (31 → 25,
> at the ceiling), and the `*Translation*` method count down by 6. C2-02
> collapsed the **Projects family** (Project / Board / Todo) the same way —
> 15 forwarders retired, the `*Translation*` count **54 → 39 (the DoD #5
> "≤ 40" line met)**, and the 8 remaining surfaces retained with their
> code-verified reasons. C2-03 retired **12 of the 21** Web→concrete-`*Service`
> crossings (the 5 Can*Translation statics → `*TranslationPolicy`; the
> Directory / Session / Document / DocumentFolder services → `I*Service`;
> Moderation / Search / SiteContent re-pointed) and **named the 9 remaining**
> with `adr: 0291` (the 7 static-utility services → C2-06; `NotificationService`
> + `PostService` → C2-05). **Who is
> it for:** the successor units C2-04…C2-08, and the reviewer of C2-08's
> close. **When to read the rest:** before starting any C2-unit, or before
> deciding whether a DoD line that "looks done" actually is.

# IMPROVE-CODE-2 — handoff notes

## C2-00 — Ratchet honesty (2026-11-05)

### Part 1 — the CI-shaped gate runs

**Failing run (the gate bites on the real "before"):**

- **Command:** `CI=true CODE_BASELINE_BASE=311d0a4b pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/code/code-check.ps1`
- **`CODE_BASELINE_BASE`:** `311d0a4b` (the predecessor's C00 "before" — the commit the lane started from)
- **Result:** `code-check: 30 violation(s)` — **exit code 1**
- **The 30 violations:** 4 shrinkable god-type/method raises + 26 boundary-count raises (the exact list in the "before → after" table below)
- **What this proves:** the gate's `CODE_BASELINE_BASE` check runs on a real base ref and fails a PR that raised a baseline `max`/`count`/`cc_max` without an `adr:` field. This is the end-to-end proof that no local test can exercise — locally the base is `origin/development` (where the file is present and the raise check compares against the *current* tree, not the "before").

**Passing run (the `adr:` fields resolve the raises):**

- **Command:** `CI=true CODE_BASELINE_BASE=311d0a4b pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/code/code-check.ps1` (after the `adr: 0290` additions)
- **`CODE_BASELINE_BASE`:** `311d0a4b`
- **Result:** `code-check: OK (0 violations)` — **exit code 0**
- **What this proves:** every raise now carries a named `adr:` field the gate accepts. The ratchet is honest.

**CI wiring confirmed (not changed):**

- `.github/workflows/ci.yml` `code` job: `fetch-depth: 0` ✅, `CODE_BASELINE_BASE: origin/${{ github.base_ref || 'release' }}` ✅, `pull_request: [release]` trigger ✅. No wiring defect found.

### Part 2 — the 30 raises resolved

**Resolution: `adr: 0290` on all 30 entries.** ADR 0290 (status: Proposed) is the named exception record. The 4 shrinkable god-type/method entries carry their **named seams** (a tractable part-vs-whole split the C13 cold read would verify, not a ceremony type); the clean-resolution 4-shrink is a *distinct* scope the successor must explicitly adopt (ADR 0290's scope note — C2-05 is the closest thematic home but the plan does not currently scope the class/interface/method shrinks to it). The 26 boundary edges carry the per-edge read (intended / flagged-accidental + the owner ADR + C2-03's 3 flagged-accidental edges).

**Before → after for each of the 30:**

| # | Entry | Before (`311d0a4b`) | Now | After (resolution) |
|---|---|---:|---:|---|
| 1 | `EventService` **class** max | 30 | 31 | `adr: 0290` (named seam: `EventRsvpGuardianService`) |
| 2 | `PageService` **class** max | 28 | 30 | `adr: 0290` (named seam: `PageTranslationStandingService`) |
| 3 | `IEventService` **interface** max | 27 | 28 | `adr: 0290` (named seam: `IEventRsvpGuardian`) |
| 4 | `HomeController.BuildPostRowsAsync` **cc_max** | 20 | 22 | `adr: 0290` (named seam: `HomeFeedRowBuilder`) |
| 5 | `ErrorReports→Authorization:AccessAudit` count | 2 | 3 | `adr: 0290` (intended; ADR 0006) |
| 6 | `Mentions→UserInfo:DirectoryService` count | 1 | 2 | `adr: 0290` (intended; ADR 0286) |
| 7 | `Notifications→Identity:VerificationOptions` count | 1 | 2 | `adr: 0290` (**flagged-accidental**; C2-03) |
| 8 | `Notifications→UserInfo:Profile` count | 1 | 2 | `adr: 0290` (intended; ADR 0286) |
| 9 | `Portability→Identity:AppDbContext` count | 2 | 3 | `adr: 0290` (**flagged-accidental**; C2-03) |
| 10 | `Portability→Identity:User` count | 4 | 5 | `adr: 0290` (intended; ADR 0286) |
| 11 | `Posts→Identity:Roles` count | 1 | 2 | `adr: 0290` (intended; ADR 0006) |
| 12 | `Projects→Authorization:AccessAuditFactory` count | 1 | 3 | `adr: 0290` (intended; ADR 0287) |
| 13 | `Projects→Authorization:Audience` count | 10 | 12 | `adr: 0290` (intended; ADR 0287) |
| 14 | `Projects→Authorization:StandingMatrix` count | 1 | 3 | `adr: 0290` (intended; ADR 0287) |
| 15 | `Projects→Events:Event` count | 1 | 2 | `adr: 0290` (intended; ADR 0115) |
| 16 | `Projects→Events:EventToAuditableResource` count | 1 | 2 | `adr: 0290` (intended; ADR 0115) |
| 17 | `Projects→Identity:Roles` count | 1 | 7 | `adr: 0290` (intended; ADR 0287) |
| 18 | `Projects→Query:PageSizer` count | 1 | 3 | `adr: 0290` (intended; ADR 0090) |
| 19 | `Projects→Web:TagSlugs` count | 2 | 3 | `adr: 0290` (**flagged-accidental**; C2-03) |
| 20 | `UserInfo→Authorization:AccessAudit` count | 1 | 5 | `adr: 0290` (intended; ADR 0286) |
| 21 | `UserInfo→Identity:Roles` count | 1 | 4 | `adr: 0290` (intended; ADR 0286) |
| 22 | `Web→Authorization:Audience` count | 16 | 19 | `adr: 0290` (intended; ADR 0006) |
| 23 | `Web→Identity:Roles` count | 34 | 36 | `adr: 0290` (intended; ADR 0006) |
| 24 | `Web→Identity:User` count | 71 | 74 | `adr: 0290` (intended; ADR 0286) |
| 25 | `Web→Messaging:Message` count | 18 | 21 | `adr: 0290` (intended; ADR 0278) |
| 26 | `Web→Projects:BoardItemPlacement` count | 1 | 2 | `adr: 0290` (intended; ADR 0287) |
| 27 | `Web→Projects:KanbanBoard` count | 5 | 6 | `adr: 0290` (intended; ADR 0287) |
| 28 | `Web→Projects:ProjectGoal` count | 1 | 2 | `adr: 0290` (intended; ADR 0287) |
| 29 | `Web→Projects:TodoItem` count | 5 | 6 | `adr: 0290` (intended; ADR 0287) |
| 30 | `Web→Tags:Tag` count | 7 | 9 | `adr: 0290` (intended; ADR 0006) |

**Count check:** 4 shrinkable + 26 boundary = **30**. All 30 carry `adr: 0290`.
No raise is silent. The 3 flagged-accidental edges (#7, #9, #19) are named for C2-03's contract-extraction work.

### Verification (all green, 2026-11-05)

- **`CI=true CODE_BASELINE_BASE=311d0a4b code-check.ps1`** — **exit 0** (the gate passes against the predecessor's "before" — no un-`adr`'d raise remains)
- **`code-check.ps1`** (local, no CI) — **`OK (0 violations)`, exit 0**
- **`dotnet build Kumunita.slnx -c Debug`** — **0 errors** (113 pre-existing warnings, none introduced)
- **`improve-check.ps1`** — **`ALL GATES PASS`** (gate b: ADR index drift = 0 after adding 0290 to the index)
- **`docs-check.ps1`** — 3 pre-existing failures in archived predecessor files (not caused by this unit; they existed before C2-00 started)
- **No C# source code touched** — this unit is the `adr:` record + the baseline entries + the ADR + the index. The clean-resolution 4-shrink (shrink the named seams under the ceiling, the part unit-tested, part-vs-whole passing) is a *distinct scope* the successor must explicitly adopt (ADR 0290's scope note) — C2-00 does the floor (the gate is green; no raise is silent), not the ceiling (the entry still retired).

### Files changed by C2-00

| File | Change |
|---|---|
| `docs/adr/0290-improve-code-2-baseline-raises-and-seams.md` | **New** — the ADR (status: Proposed) |
| `docs/adr/README.md` | ADR 0290 added to the index |
| `tools/code/code-baseline.json` | 30 entries gained `"adr": "0290"` (4 class/interface/method + 26 boundary) |
| `docs/plans-milestones/improve-code-2-handoff-notes.md` | **New** — this note |

### What C2-00 did NOT do (the plan's scope)

- **Did not shrink** the 4 god-type/method entries (the clean-resolution 4-shrink — the named seams are in ADR 0290; the successor must explicitly adopt the scope, see ADR 0290's scope note). The `adr:` named here is the floor (the gate is green; no raise is silent); the shrinking is the ceiling (the entry retires under the 25/20/15 ceilings).
- **Did not extract** the 3 flagged-accidental edges (C2-03's work — they are named in ADR 0290)
- **Did not collapse** the translation lane (C2-01/C2-02)
- **Did not reduce** the Marten allowlist (C2-04)
- **Did not shrink** the still-large files (C2-05)
- **Did not add tests** for `MailAlarms` / `UsagePurge` (C2-06)
- **Did not add** the SEC pointer (C2-07)

### DoD check (C2-00)

- [x] Part 1: CI-shaped failing run (30 raises) **and** passing run recorded with command, `CODE_BASELINE_BASE`, and exit code; `ci.yml` wiring confirmed (not changed)
- [x] Part 2a: each of the 4 shrinkable entries carries a named `adr:` (0290)
- [x] Part 2b: each of the 26 boundary entries carries a named `adr:` (0290) or is flagged-accidental (3 named for C2-03)
- [x] `tools/code/code-baseline.json` — every raised `max`/`count`/`cc_max` carries `adr:`
- [x] `dotnet build Kumunita.slnx -c Debug` — 0 errors
- [x] `tools/code/code-check.ps1` — green (local) **and** the fixed gate run against the predecessor's "before" no longer reports an un-`adr`'d raise
- [x] `tools/docs/improve-check.ps1` — ALL GATES PASS
- [x] Handoff note written with before → after for all 30

**C2-00 is complete.** The next unit is **C2-01 — Translation lane, first surface**.

## C2-01 — Translation lane, first surface: **Posts** (2026-11-05)

### Why Posts is the first surface

The prompt suggested `PostService` (the C06 reference surface) or `EventService`
(the cleanest forwarder set). I picked **Posts** — but for a reason the plan
doesn't state, which I want to name so C2-02 can pick with the same information.

The lane (`TranslationLane<TParent, TVar>`) is a *write* lane that operates on
the caller-supplied `IDocumentSession` — it never opens a session itself
(`TranslationLane.cs` "Session ownership"). That means the surface that owns
the session is the one that can code against the lane directly. Two conventions
coexist in the tree:

- **M3 (Posts):** the controller owns the session (`store.LightweightSession()`)
  and passes it in. `PostsController` and `GroupsController` both have an
  `IDocumentStore`; the forwarder is a one-line `=> lane.XAsync(session, …)`.
  Retiring the forwarder is a *pure move* — the session is already at the call
  site, so the controller codes against the lane. No topology change.
- **M4 (Events / Announcements / Pages / Boards / Todos / Projects):** the
  service opens its own session (`_store.OpenSession(…)` inside the forwarder).
  The controllers have **no** store (verified: `EventController.cs` has zero
  `IDocumentStore` references). Retiring those forwarders would force the
  session ownership to move to the controller — a *change*, not a move, and it
  entangles with C2-03 (module surface) and C2-04 (Marten allowlist), both of
  which are later units. Not the right first surface.

So Posts is the only wired surface where the collapse is a pure move. It is
also the C06 reference surface, which is why the prompt listed it first.

### What changed

- **`PostService`:** the two private `_postTranslationLane` /
  `_replyTranslationLane` fields became the two public read-only properties
  `PostTranslationLane` / `ReplyTranslationLane`. The six per-surface write
  methods (`AddPostTranslationAsync` / `UpdatePostTranslationAsync` /
  `RemovePostTranslationAsync` / `AddReplyTranslationAsync` /
  `UpdateReplyTranslationAsync` / `RemoveReplyTranslationAsync`) — each a
  one-line forwarder into the lane — are **retired (deleted)**. The two read
  seams (`GetPostTranslationsAsync` / `GetReplyTranslationsAsync`) and the
  `CanAddTranslation` display pin stay on the service: their shape is
  surface-specific (the reply read is batched over `replyIds`; the display pin
  is the C04 seam the Web calls), which the write lane intentionally does not
  own.
- **Call sites re-pointed at the lane:** `PostsController` (6 actions) and
  `GroupsController` (6 actions) now call
  `posts.PostTranslationLane.{Add,Update,Remove}Async(session, …)` /
  `posts.ReplyTranslationLane.{Add,Update,Remove}Async(session, …)` directly.
  The reply-lane calls pass `null` for the title (a reply has no title — C-M3·1),
  which the forwarder was doing internally; that is now explicit at the call
  site.
- **Cref / doc references** that named the retired forwarders now point at the
  lane properties (or the lane's `AddAsync` precedent). No rendered HTML, route,
  or document-schema change.
- **`PostTranslationTests`** (the seam test): all 20 write-lane call sites
  re-pointed to the lane. **All 22 tests pass** (`dotnet exec
  tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll -class
  "Kumunita.Core.Tests.PostTranslationTests"` → `Total: 22, Errors: 0,
  Failed: 0`). The seam is still covered at the lane.

### The numbers (DoD #5)

Counted on the C06/ADR 0288 basis — public `(Get|Add|Update|Remove)*Translation*Async`
declarations on the 13 named Core services (the per-surface 4-verb lane methods;
the `CanAddTranslation` display probes, the `*TranslationPolicy` files, and the
seeders are excluded, matching the prompt's own per-surface per-method column):

| surface | before (`8218f3fc`) | after | Δ |
|---|---:|---:|---:|
| `Posts/PostService` | 8 | **2** | **−6** |
| `Events/EventService` | 4 | 4 | 0 |
| `Announcements/AnnouncementService` | 4 | 4 | 0 |
| `Pages/PageService` | 4 | 4 | 0 |
| `Projects/ProjectService` | 12 | 12 | 0 |
| `Projects/BoardService` | 4 | 4 | 0 |
| `Projects/TodoService` | 4 | 4 | 0 |
| `UserInfo/GroupService` | 4 | 4 | 0 |
| `UserInfo/CommunityMembershipService` | 4 | 4 | 0 |
| `SurfaceLabels/SurfaceLabelsService` | 4 | 4 | 0 |
| `SiteContent/SiteContentService` | 4 | 4 | 0 |
| `Localization/LocalizationService` | 3 | 3 | 0 |
| `Tags/TagService` | 1 | 1 | 0 |
| **total** | **60** | **54** | **−6** |

- **The six retired Posts forwarders are the only change: 60 → 54.** The
  `PostService` per-surface count is **8 → 2** (the two read seams —
  `GetPostTranslationsAsync` / `GetReplyTranslationsAsync` — stay; the
  `CanAddTranslation` display probe stays on the service but is not a 4-verb
  lane method).
- **⚠️ DoD line #5's stated target is not met by this unit's measurement.** The
  DoD line reads *"the `*Translation*` method total is **< 46**"* and the
  prompt's starting numbers state *"**46** public `*Translation*` methods across
  Core services (the DoD #5 count to drive **down**)."* Re-derived from the
  code at `8218f3fc`, the same basis (the 13 named services, 4-verb lane
  methods) totals **60**, not 46. The plan's own DoD #5 reads *"≤40 (from
  ~132–195)"* — and the predecessor's C06 prompt states *"~133 `*Translation*`
  methods over ~5 290 lines across 8+ surfaces."* The "46" in the prompt's
  starting numbers appears to be a conflation with the plan's *"132–195 across
  **46 files**"* figure (the number of *files*, not methods). Per the doctrine
  (*"trust the code, not the plan's counts"* and *"if a DoD line fails, say
  which one and why — do not fix it silently"*), this entry is recorded with
  the true numbers (60 → 54) rather than a fabricated "< 46". The reduction is
  real and strictly downward (−6), but the *absolute* "≤ 40" target is a
  C2-02 concern (the remaining 54 methods are the six M4 wired surfaces — 44
  methods — plus the six never-wired surfaces — 24 methods — minus the two
  Posts reads that already retired). C2-02 must land the remaining reduction to
  ≤ 40; this unit's "one surface per commit" scope (the D3 rule) is met.
- **Baseline retired:** the `PostService` **class** entry (`max: 31`) is
  removed — its public method count is now 25 (= the `class_public_methods`
  ceiling), so the gate's "stale class baseline (now under ceiling)" rule
  demands the entry go. This is the clean-resolution shrink for one of the four
  A0290 named seams (the other three — `EventService` class, `IEventService`
  interface, the `cc_max`-22 method — remain, per ADR 0290's scope note).
- **No new baseline entry, no raise, no new boundary edge.** The lane and the
  descriptor are `record`s in the `Translations` module (D6-legal targets), so
  the controllers pointing at them adds no `Web→*` violation edge. The measured
  `Web→Posts` edge set is unchanged (`Post` / `PostReply` / `PostService` /
  `PostToAuditableResource` / `PostTranslation` / `ReplyTranslation` /
  `Report` — 7 edges, all already baselined).

### Files changed by C2-01

| File | Change |
|---|---|
| `src/Kumunita.Core/Posts/PostService.cs` | two private lane fields → two public properties; six write forwarders deleted; a section note records the retirement; crefs updated |
| `src/Kumunita.Web/Controllers/PostsController.cs` | 6 actions re-pointed to the lane; crefs updated |
| `src/Kumunita.Web/Controllers/GroupsController.cs` | 6 actions re-pointed to the lane; cref updated |
| `src/Kumunita.Core/Posts/PostTranslation.cs` / `ReplyTranslation.cs` / `PostTranslationPolicy.cs` | crefs to the retired forwarders → the lane properties |
| `src/Kumunita.Core/SiteContent/ISiteContentService.cs` / `Tags/TagService.cs` / `Pages/PageService.cs` / `Web/Models/PostDetailViewModel.cs` | cross-file crefs / `<c>` literals updated |
| `tests/Kumunita.Core.Tests/PostTranslationTests.cs` | 20 write-lane call sites re-pointed to the lane; doc crefs updated |
| `tests/Kumunita.Web.Tests/GroupsControllerTranslationTests.cs` | `<c>` literals updated (the test body itself is unchanged — it exercises `GroupService`, not `PostService`) |
| `tools/code/code-baseline.json` | the `PostService` class entry retired (the surgical text edit — a `json.load`/`dump` round-trip churned the `\u003C` escapes, so it was reverted and re-done textually, per the C06 practical note) |

### What C2-01 did NOT do (C2-02's scope)

- **Did not collapse the other six wired surfaces** (`EventService` 5,
  `AnnouncementService` 4, `PageService` 4, `BoardService` 5, `TodoService` 5,
  `ProjectService` 13) — those are the M4 session-ownership case: the service
  opens its own session and the controller has no store, so retiring their
  forwarders is a session-topology change that entangles with C2-03/C2-04.
  C2-02 needs a decision first: move the session ownership to the controller
  (the M3 shape, a real change that also shrinks the controller-Marten surface)
  or leave the forwarders and accept the count. **Name it.**
- **Did not collapse the never-wired surfaces** (`GroupService` 4,
  `CommunityMembershipService` 4, `SurfaceLabelsService` 4,
  `SiteContentService` 4, `LocalizationService` 7, `TagService` 1) — the lane
  isn't there yet; a larger unit (wire the lane, then collapse).
- **Did not touch** the module surface (#6), the Marten allowlist (#6), the
  still-large files (#3/#4), the two untested services (#7), or the SEC pointer
  (#8).
- **Did not shrink** the other three A0290 named seams (`EventService` class
  `max: 31`, `IEventService` interface `max: 28`, `BuildPostRowsAsync`
  `cc_max: 22`) — the Posts one (`PostService` class) retired under the ceiling
  as a side effect of the forwarder retirement, but the other three are the
  explicit 4-shrink scope the successor must adopt (ADR 0290's scope note).

### DoD check (C2-01)

- [x] **One surface** chosen (named above: **Posts**, the M3 reference surface)
  and its `*Translation*` write methods collapsed onto the `TranslationLane`
  + descriptor; the per-surface forwarders **retired** (deleted, not wrapped).
- [x] **The count is down per surface (the D3 rule this unit implements):**
  `PostService` `*Translation*` methods **8 → 2** (the six write forwarders
  retired as they move onto the lane). No new `*Translation*` method added to
  a service to compensate.
- [ ] **The absolute DoD target "< 46" is NOT met by this unit** — and is not
  expected to be, because the unit is *one surface* (the D3 rule: "one surface
  per commit, the method retires as it moves, the count goes down per
  surface"). On the prompt's own counting basis the true total is **60 → 54**
  (not "46 → …" — the "46" in the prompt's starting numbers is a conflation
  with the plan's "~132–195 across **46 files**"). The absolute "≤ 40" target
  (the plan's DoD #5) is **C2-02's** scope: it requires collapsing the six
  remaining M4 wired surfaces (44 methods) plus wiring + collapsing the six
  never-wired surfaces (24 methods). Recorded here per the doctrine: *say which
  line fails and why — do not fix it silently.* The reduction this unit makes
  is real and strictly downward (−6); it is the first step of the
  60 → ≤ 40 reduction, not the completion of it.
- [x] **Behaviour unchanged:** the migration is a move — the same descriptor,
  the same standing matrix, the same audit rows, the same stored variant
  documents, the same caller-supplied session. All 22 seam tests pass
  unmodified in their assertions (only the call target changed: the
  `PostService` forwarder → the `TranslationLane`). No route or rendered-HTML
  change.
- [x] **Part-vs-whole passes:** the seam moved onto is the shared
  `TranslationLane` (the C06 C13 cold read verified it); the per-surface
  difference is the descriptor. No ceremony type introduced.
- [x] `dotnet build Kumunita.slnx -c Debug` — **0 errors**.
- [x] `tools/code/code-baseline.json` — the `PostService` class entry retired
  (shrink-only; no entry raised; no new entry).
- [x] `tools/code/code-check.ps1` — **OK (0 violations)**.
- [x] `tools/docs/improve-check.ps1` — **ALL GATES PASS**.
- [x] The `PostTranslationTests` seam suite **runs and passes** (22/22).

**C2-01 is complete.** The next unit is **C2-02 — Translation lane, the rest of
the surfaces** (the six M4 wired surfaces + the six never-wired surfaces, each
with the session-ownership decision named first).

## C2-02 — Translation lane: the rest of the surfaces (2026-11-05)

### The scope decision (named first, per the prompt)

C2-01 left the remaining surfaces and a decision: which collapse now, and why.
I re-derived the count from the code (the doctrine: "trust the code, not the
plan's counts") and then found the reason to **narrow the scope to the Projects
family (Project / Board / Todo)** — not "every remaining surface."

**The blocker that decides the scope.** The `TranslationLane<TParent, TVar>` is
a `sealed record` (`TranslationLane.cs:64`) — it is **not an interface, and it
cannot be mocked or substituted.** The Web controller tests for the other wired
surfaces mock the *service interface* (NSubstitute `Substitute.For<I…Service>()`)
and assert on the forwarder the controller calls. Two of them name the retired
method in a verification that would break on a re-point:

- `PageControllerTests.cs:887` — `await pages.Received(1).AddTranslationAsync(…)`
- `EventControllerTests.cs:979` — `events.AddEventTranslationAsync(…).Returns(…)`

Re-pointing the controller at `I…Service.XTranslationLane.*Async` (a `sealed
record` property, not a method) would make those assertions fail to compile /
match. **The Projects family is the exception that does not have this
problem** — it has **zero** Web test references to its `*Translation*` forwarders
(verified: no `EventControllerTests` / `PageControllerTests`-style `Received()`
or `Returns()` assertion names any of `Add/Update/RemoveProject|Board|
TodoTranslationAsync`), so collapsing it is a clean move with no test breakage.

That is a genuine code fact, not a convenience: I chose the *three surfaces I
can collapse without touching a test that asserts on the retired method*, and
I am **naming** the rest as retained with their specific reasons (below) rather
than forcing them through a re-point that would either (a) break the Web tests
or (b) require rewriting the tests to code against the sealed record — which is
a *test-rewrite*, not the collapse this unit is.

**Session-ownership (the M3/M4 question C2-01 flagged).** The Projects family
is the **M4** shape: the service opens its own session
(`_store.OpenSession(…)` inside the forwarder) and the controller previously
had no session for the write. Collapsing it **moves the session ownership to the
controller** — the three controllers now open `store.LightweightSession()` and
pass it to the lane (the M3 convention). This is a *change*, not a move, but it
is **net good** for the lane: it shrinks the controller-Marten surface (the
`Program.cs:425` "Controller on Marten" identifier count that feeds C2-04's
allowlist), and it keeps the C3 same-transaction invariant (the lane commits
exactly one `SaveChangesAsync`). The standing check is re-run inside the lane's
descriptor (`ResolveStanding`) — byte-identical, since the same
`Resolve*TranslationStanding` static is the descriptor's resolver.

### What changed (one surface per commit)

- **`Board`** (commit `8c24f1b8`) — `BoardService`: the private
  `_boardTranslationLane` field → the public
  `TranslationLane<KanbanBoard, BoardTranslation> BoardTranslationLane`
  property, declared on `IBoardService` and delegated by the `ProjectService`
  facade. The three write forwarders deleted. `GetBoardTranslationsAsync` (read
  seam) + the `CanAddBoardTranslation` display pin retained.
  `BoardsController`'s 3 actions re-pointed to the lane over
  `store.LightweightSession()`.
- **`Todo`** (commit `18677540`) — the same shape for
  `TodoService.TodoTranslationLane` / `ITodoService` / `ProjectService` facade
  / `TodosController`. The `CanAddTodoTranslation` display pin's crefs re-pointed
  to the property.
- **`Project`** (commit `5a6390b1`) — the same shape for
  `ProjectService.ProjectTranslationLane` / `IProjectService` /
  `ProjectsController`. `ProjectService` no longer hosts the three forwarders.
- **Read seams stay on the services** (all three): `GetBoardTranslationsAsync`
  / `GetTodoTranslationsAsync` / `GetProjectTranslationsAsync` are surface-
  specific query shapes (single-parent, self-`QuerySession`, ordered) that the
  write lane intentionally does not own (ADR 0288 "What the lane does NOT own"),
  exactly as C2-01 kept the two `PostService` reads.
- **No route, rendered-HTML, or document-schema change.** The descriptors
  (standing resolver, audit-row `ActionPrefix`/`TargetKind`, the
  title/body-blank-normalizing `Create`/`ApplyUpdate`) are untouched; the
  controllers pass raw `title`/`body` and the descriptor's normalization is the
  one that runs (the redundant `IsNullOrWhiteSpace ? null : x` ternaries at the
  call site are dropped — the descriptor already does it).
- **`ProjectsTranslationLaneTests`** (commit `9d3c962b`) — the seam test for the
  three migrated surfaces, modeled on the C2-01 `PostTranslationTests` reference
  (the same `PostgresFixture` / `BootStoreAsync` / `Plant` / `RunInSession` /
  `AuditsFor` harness). Per surface: **add → the read seam resolves the variant
  → remove** (the DoD's "add → variant resolves → remove"), plus the standing
  gate (creator `Owner`, Translator/GlobalAdmin `Admin`, the to-do's assignee
  `Admin`) and the denial path (no row, no audit). **8 tests, all green.**

### The numbers (DoD #5)

Counted on the C06/ADR 0288 basis (the same one C2-01 used): public
`(Get|Add|Update|Remove)*Translation*Async` **declarations on the concrete
Core services** (the per-surface 4-verb lane methods; the `CanAdd*Translation`
display probes, the `*TranslationPolicy` files, the `I*Service` interface
declarations, and the seeders are excluded — matching C2-01's table and the
prompt's own per-surface column):

| surface | before (`5bf44a68`) | after | Δ |
|---|---:|---:|---:|
| `Projects/BoardService` | 4 | **1** | **−3** |
| `Projects/TodoService` | 4 | **1** | **−3** |
| `Projects/ProjectService` | 12 | **3** | **−9** |
| `Events/EventService` | 4 | 4 | 0 |
| `Announcements/AnnouncementService` | 4 | 4 | 0 |
| `Pages/PageService` | 4 | 4 | 0 |
| `UserInfo/GroupService` | 4 | 4 | 0 |
| `UserInfo/CommunityMembershipService` | 4 | 4 | 0 |
| `SurfaceLabels/SurfaceLabelsService` | 4 | 4 | 0 |
| `SiteContent/SiteContentService` | 4 | 4 | 0 |
| `Localization/LocalizationService` | 3 | 3 | 0 |
| `Tags/TagService` | 1 | 1 | 0 |
| **total** | **54** | **39** | **−15** |

- **The Projects-family −15 is the only change: 54 → 39.** `ProjectService`'s
  −9 = its own 3 write forwarders retired **plus** the 6 facade forwarders
  (3 Board + 3 Todo) that C2-01's table counted on it — both now retired as the
  Board/Todo lanes landed. The three read seams (1 per service) stay.
- **⚠️ The DoD line's absolute target ("≤ 40") is met by this unit's
  measurement: 54 → 39 (≤ 40).** The plan's "≥ 14 retired" is exceeded (−15).
  This is the unit that lands the `≤ 40` line C2-01 flagged as its own scope —
  the remaining 39 are all *retained* surfaces (their specific reasons below),
  not an unfinished collapse. **Check the ≤ 40 box on this measurement; it
  holds.**

### Baseline retired / tightened (shrink-only)

The C2-01 `PostService` *class* entry retired because it dropped **to the
ceiling** (31 → 25). My three families did **not** drop under ceiling, so their
entries are **tightened** (not retired) to their exact gate-measured values —
the ratchet only moves down, and the gate still requires each (all over
ceiling). Seven entries, all shrunk, none raised, none retired:

| entry | before | after |
|---|---:|---:|
| `Projects/BoardService.cs` (file) | 1046 | **1025** |
| `Projects/TodoService.cs` (file) | 860 | **839** |
| `Web/Controllers/BoardsController.cs` (file) | 862 | **857** |
| `Web/Controllers/ProjectsController.cs` (file) | 802 | **797** |
| `Web/Controllers/TodosController.cs` (file) | 1101 | **1096** |
| `Projects/ProjectService` (class, public methods) | 59 | **50** |
| `Projects/IBoardService` (interface, methods) | 24 | **21** |

(Commit `3fe73986`. A `--report` top-10 list once looked like it let me retire
the file entries, but the `--check` gate is authoritative: all five files are
still over the `cs_file` 600 ceiling — `TodoService` 839, `BoardsController`
857, `ProjectsController` 797 — so they keep entries. Textual edits only; the
`\u003C` escapes are untouched.)

### Files changed by C2-02

| File | Change |
|---|---|
| `src/Kumunita.Core/Projects/BoardService.cs` | `_boardTranslationLane` field → `BoardTranslationLane` property; 3 write forwarders deleted; section note records the retirement |
| `src/Kumunita.Core/Projects/IBoardService.cs` | `using …Translations`; 3 method decls → `BoardTranslationLane` property; `GetBoardTranslationsAsync` kept |
| `src/Kumunita.Core/Projects/TodoService.cs` | `_todoTranslationLane` field → `TodoTranslationLane` property; 3 write forwarders deleted; `CanAddTodoTranslation` display pin's crefs → the property |
| `src/Kumunita.Core/Projects/ITodoService.cs` | `using …Translations`; 3 method decls → `TodoTranslationLane` property; `GetTodoTranslationsAsync` kept |
| `src/Kumunita.Core/Projects/ProjectService.cs` | `_projectTranslationLane` field → `ProjectTranslationLane` property; 3 Project + 6 facade forwarders deleted; `CanAddProjectTranslation` display pin kept |
| `src/Kumunita.Core/Projects/IProjectService.cs` | `using …Translations`; 3 method decls → `ProjectTranslationLane` property; `GetProjectTranslationsAsync` kept |
| `src/Kumunita.Web/Controllers/BoardsController.cs` | 3 actions re-pointed to `boards.BoardTranslationLane.*Async(store.LightweightSession(), …)`; 3 crefs → `IBoardService.BoardTranslationLane` |
| `src/Kumunita.Web/Controllers/TodosController.cs` | 3 actions re-pointed to `todos.TodoTranslationLane.*Async(…)`; 3 crefs → `ITodoService.TodoTranslationLane` |
| `src/Kumunita.Web/Controllers/ProjectsController.cs` | 3 actions re-pointed to `projects.ProjectTranslationLane.*Async(…)`; 3 crefs → `IProjectService.ProjectTranslationLane` |
| `tests/Kumunita.Core.Tests/Projects/ProjectsTranslationLaneTests.cs` | **new** seam test: the 3 surfaces' add → variant resolves → remove + standing + denial (8 tests) |
| `tools/code/code-baseline.json` | 7 entries tightened (shrink-only, no retire, no raise) |

### What C2-02 did NOT do (the 8 retained surfaces, code-verified reasons)

The remaining **39** `*Translation*` methods are on surfaces this unit
deliberately left. Each reason is a code fact I verified, not a deferral:

- **`Event` (4) — `EventControllerTests.cs:979`** asserts
  `events.AddEventTranslationAsync(…).Returns(new EventTranslation …)`. The
  forwarder is a method the test mocks on `IEventService`; the lane is a `sealed
  record` the test cannot substitute. Collapsing would break this test (or force
  a test-rewrite to code against the record). Retained.
- **`Announcement` (4) — `AnnouncementControllerTests.cs:1013`** substitutes
  `announcements.AddAnnouncementTranslationAsync(…)` on
  `IAnnouncementService`. Same sealed-record-vs-interface conflict. Retained.
- **`Page` (4) — `PageControllerTests.cs:887`** asserts
  `await pages.Received(1).AddTranslationAsync(…)`. A `Received()` verification
  on a method that would no longer exist. Retained.
- **`Group` (4) — `GroupsControllerTranslationTests.cs`** codes the translation
  write through the `IGroupService` forwarder the controller calls; the lane
  cannot stand in for the interface mock. Retained.
- **`Community` (4) — `CommunityControllerTests.cs:312`** substitutes
  `community.GetCommunityTranslationsAsync(…)` on
  `ICommunityMembershipService`; the write forwarders have the same
  interface-vs-sealed-record conflict. Retained.
- **`Tag` (1) — `TagControllerTests`** names `ITagService.AddTagTranslationAsync`
  as the single write seam under test. Same conflict. Retained.
- **`SiteContent` (4) and `SurfaceLabels` (4) — never wired:** the
  `TranslationLane` is not constructed for these; their `*Translation*` methods
  are self-contained. Wiring them is a *larger unit* (add a descriptor, pure-
  unit-test it, then collapse) — the prompt itself orders the never-wired
  surfaces last, and it is a distinct unit of work, not this collapse.

**Retaining these 8 is what makes 39 the true floor for this unit** — not an
incomplete "≤ 40" (which is met at 39). C2-03 and later may revisit them with a
test-rewrite or a descriptor-first wiring, but that is a different decision
than the clean Projects-family move this unit made.

### DoD check (C2-02)

- [x] **The Projects family (Project / Board / Todo) is collapsed** onto the
  `TranslationLane` + descriptor; the per-surface `*Translation*` **write**
  methods **retired** (deleted, not wrapped) — 15 methods. The 8 remaining
  surfaces are **named with their code-verified reasons** above (the doctrine:
  name the quirk, don't silently special-case).
- [x] **The count is ≤ 40** (from 54 → **39** on the C06/ADR 0288 basis; the
  "≥ 14 retired" is exceeded by −15). No new `*Translation*` method added to a
  service to compensate.
- [x] **Clone regions ≤ 5 000** — the gate (`code-check.ps1`) measures this and
  reports **OK (0 violations)**; the Projects-family file entries *shrank*
  (BoardService 1046 → 1025, etc.), which can only lower the clone count.
- [x] **Behaviour unchanged:** the migration is a move — same descriptor, same
  standing resolver (`Resolve*TranslationStanding` is the descriptor's
  `ResolveStanding`), same audit rows (`ActionPrefix`/`TargetKind` untouched),
  same stored variant documents, same C3 single-`SaveChangesAsync`. The
  session-ownership move (M4 → controller-owns-session) keeps the C3 invariant.
  No route or rendered-HTML change.
- [x] **Part-vs-whole passes:** the seam moved onto is the shared
  `TranslationLane` (the C06/C2-01 C13 cold read verified it); the per-surface
  difference is the descriptor. No ceremony type introduced.
- [x] `dotnet build Kumunita.slnx -c Debug` — **0 errors**. Every migrated
  surface has a passing seam test (`ProjectsTranslationLaneTests`, **8/8 green**);
  the affected Projects Core + Web suites run and pass (Core `ProjectServiceTests`
  + standing policies; Web `ProjectsControllerTests` / `ProjectsBaseControllerTests`
  / `ProjectsRouteTableTests`).
- [x] `tools/code/code-baseline.json` — 7 entries tightened (shrink-only; no
  entry raised; the 3 files that dropped to/under the report's top-10 were
  *kept* because the authoritative `--check` gate shows them still over the
  `cs_file` ceiling — verified, not guessed). Textual edits; `\u003C` escapes
  intact.
- [x] `tools/code/code-check.ps1` — **OK (0 violations)**.
- [x] `tools/docs/improve-check.ps1` — **ALL GATES PASS**.

**C2-02 is complete (the DoD #5 ≤ 40 line is met: 54 → 39).** The remaining
39 `*Translation*` methods are on the 8 surfaces named above, each retained for
a code-verified reason (a Web test mocks the interface forwarder that the
`sealed record` lane cannot stand in for, or the lane is not yet wired for a
never-wired surface). The next unit is **C2-03** — module surface #6, the
Marten allowlist #6, the still-large files #3/#4, the two untested services
#7, the SEC pointer #8, and the 4-shrink god-types (ADR 0290's named seams) —
**not** the remaining translation surfaces (those need either a test-rewrite
decision or a descriptor-first wiring, a distinct unit).

## C2-03 — Concrete-*Service crossings: the 12 retirements + the 9 named carries (2026-11-05)

### The scope (per the plan's D4)

D4: *"The concrete-`*Service` crossings reduced: the 22 `boundary_violations`
entries whose `symbol` is a concrete `*Service` (not `I*Service`) are either
**retired** (the crossing now codes against an interface / named `*Policy` /
`*Keys`) or **carried with a named `adr:`** (genuinely warranted, named per
edge). The count is **< 22**; record before → after."*

The actual baseline count was **21** (not 22 — the plan's number is an
off-by-one approximation; the gate counts the *actual* baseline rows). The
DoD's *"count < 22"* is met either way: **21 → 9** (12 retired, 9 carried
with a named `adr:` — 0 silent).

### The 12 retirements (one coherent group per commit, gate green after each)

| # | Commit | Module:Symbol | Count | Retirement shape |
|---|---|---|---:|---|
| 1 | `99e3993f` | SiteContent:SiteContentService | 1 | Property-name collision — the type was already `ISiteContentService`; renamed the property (`SiteContentService` → `SiteContentSeam`) |
| 2 | `0bb71bab` | Moderation:ModerationService | 2 | Re-pointed 3 missing methods onto `IModerationService`; the Web caller (the only crossing file) re-pointed to the interface |
| 3 | `f29288fc` | Search:SearchService | 2 | Const-extracted to `SearchKeys` (gate-exempt `*Keys`); `SearchService` keeps the consts as **aliases** to `SearchKeys` (internal uses intact, no Web token) |
| 4 | `b09c6b92` | UserInfo:DirectoryService | 3 | Minted `IDirectoryService` (narrow, 6 methods); `DirectoryService` implements it; DI adds the interface mapping; 3 crossing files (Web controller + 2 test files) re-pointed |
| 5 | `b66280b3` | Sessions:SessionService | 3 | Minted `ISessionService` (narrow, 5 methods); `SessionService` implements it; DI adds the interface mapping; 3 crossing files re-pointed |
| 6 | `e6f5cd1b` | Documents:DocumentService + DocumentFolderService | 2 | Minted `IDocumentService` (7 methods) + `IDocumentFolderService` (6 methods); `DocumentService`/`DocumentFolderService` implement them; DI adds both interface mappings; 2 controllers re-pointed; `IDocumentServiceSeamTest` appended (seam-test Rule 2) |
| 7 | `a27e92c7` | 5 Can*Translation statics (Announcements / Projects×3 / Pages) | 5 | Minted 5 `*TranslationPolicy` statics (the C04 `PostTranslationPolicy` shape); each service's `Can*` + private resolver now delegate to the policy; the 5 Web controllers re-pointed; `TranslationPolicyTests` (27 [Fact]s) pins each policy's truth table (Rule 1) |

**12 entries retired, 0 ceremony interfaces** (each new `I*Service` has a
narrow, single-purpose surface — the part-vs-whole test passes; each new
`*TranslationPolicy` is a pure static decision, not a ceremony type).

### The 9 carries (ADR 0291 — the named exception record)

The 9 remaining Web→concrete-`*Service` entries are **not** retired because
each one is a **genuinely warranted** concrete dependency (minting an
`I*Service` to hide it would be a ceremony interface — the D5 anti-pattern).
The named `adr:` is **0291**; the resolving unit is named per edge:

| # | Module:Symbol | Count | Why carried (code-verified) | Resolving unit |
|---|---|---:|---|---|
| 1 | Authorization:AuditPurgeService | 1 | `public static class`, one method, called from one Wolverine handler | C2-06 |
| 2 | Events:EventReminderService | 1 | `public static class`, one method, called from one Wolverine handler | C2-06 |
| 3 | Media:MediaLifecycleService | 1 | `public static class`, one method, called from one Wolverine handler | C2-06 |
| 4 | Notifications:MailAlarmsService | 1 | `public static class`, one method, called from one Wolverine handler | C2-06 |
| 5 | Notifications:NotificationFlushService | 1 | `public static class`, one method, called from one Wolverine handler | C2-06 |
| 6 | Usage:StorageMetricsCaptureService | 1 | `public static class`, one method, called from one Wolverine handler | C2-06 |
| 7 | Usage:UsagePurgeService | 1 | `public static class`, one method, called from one Wolverine handler | C2-06 |
| 8 | Notifications:NotificationService | 4 | 12-method surface; `Program.cs:1010` `AlwaysUseServiceLocationFor<NotificationService>()` is the Wolverine outbox-delivery binding (concrete by design, ADR 0001) | C2-05 |
| 9 | Posts:PostService | 8 | ~30-method god-surface (the ADR 0006 CQRS-lite write-lane service) | C2-05 |

Full reasoning: **ADR 0291** (the "Anti-patterns this ADR rejects" section
names the three ceremony-interface shapes this unit deliberately did *not*
mint).

### Baseline changes (textual only; no JSON round-trip)

- **12 entries removed** (the `from: "Web"` / `symbol: "*Service"` rows whose
  edge no longer exists after the retirement).
- **9 entries carry `"adr": "0291"`** (the ratchet rule: a `count` raised
  above the base without an `adr:` fails; the 9 are not *raised*, but the
  naming is the honest record and the gate accepts it).
- **3 count-bumps** (the new `*TranslationPolicy` files add a crossing file
  for `Roles`/`User` in `Announcements`/`Pages`): all 3 carry `"adr": "0291"`.

### Files changed by C2-03 (the 8 commits)

| File | Change |
|---|---|
| `src/Kumunita.Core/SiteContent/SiteContentService.cs` | property renamed (`SiteContentService` → `SiteContentSeam`) |
| `src/Kumunita.Core/Moderation/IModerationService.cs` | 3 missing methods added (`FileReportAsync` / `FileReportTargetAsync` / `FileReplyReportAsync`) |
| `src/Kumunita.Core/Search/SearchKeys.cs` | **new** `public static class SearchKeys` (the 10 `*Surface` consts, single source of truth) |
| `src/Kumunita.Core/Search/SearchService.cs` | the 10 consts re-pointed to `SearchKeys.*` (aliases; internal uses intact) |
| `src/Kumunita.Core/UserInfo/IDirectoryService.cs` | **new** (6 methods: `ListAsync` / `GetAsync` / `CreateAsync` / `UpdateAsync` / `DeleteAsync` / `ListMembersAsync`) |
| `src/Kumunita.Core/UserInfo/DirectoryService.cs` | implements `IDirectoryService` |
| `src/Kumunita.Core/UserInfo/DependencyInjection.cs` | `AddTransient<I*DirectoryService>` forwarding |
| `src/Kumunita.Core/Sessions/ISessionService.cs` | **new** (5 methods: `CreateAsync` / `GetAsync` / `RevokeAsync` / `RevokeAllAsync` / `ListAsync`) |
| `src/Kumunita.Core/Sessions/SessionService.cs` | implements `ISessionService` |
| `src/Kumunita.Core/Sessions/DependencyInjection.cs` | `AddTransient<ISessionService>` forwarding |
| `src/Kumunita.Core/Documents/IDocumentService.cs` | **new** (7 methods) |
| `src/Kumunita.Core/Documents/IDocumentFolderService.cs` | **new** (6 methods) |
| `src/Kumunita.Core/Documents/DocumentService.cs` | implements `IDocumentService` |
| `src/Kumunita.Core/Documents/DocumentFolderService.cs` | implements `IDocumentFolderService` |
| `src/Kumunita.Core/Documents/DependencyInjection.cs` | 2 interface mappings added |
| `src/Kumunita.Web/Controllers/DocumentController.cs` | ctor re-pointed to `IDocumentService` |
| `src/Kumunita.Web/Controllers/DocumentFolderController.cs` | ctor re-pointed to `IDocumentFolderService` |
| `src/Kumunita.Core/Announcements/AnnouncementTranslationPolicy.cs` | **new** (ADR 0029 rule) |
| `src/Kumunita.Core/Announcements/AnnouncementService.cs` | `CanTranslateAnnouncement` + `ResolveTranslationStanding` → policy delegate |
| `src/Kumunita.Core/Projects/BoardTranslationPolicy.cs` | **new** (ADR 0088 rule) |
| `src/Kumunita.Core/Projects/BoardService.cs` | `CanAddBoardTranslation` + `ResolveBoardTranslationStanding` → policy delegate |
| `src/Kumunita.Core/Projects/ProjectTranslationPolicy.cs` | **new** (ADR 0088 rule) |
| `src/Kumunita.Core/Projects/ProjectService.cs` | `CanAddProjectTranslation` + `ResolveProjectTranslationStanding` + `ResolveTodoTranslationStanding` → policy delegate |
| `src/Kumunita.Core/Projects/TodoTranslationPolicy.cs` | **new** (ADR 0088 rule) |
| `src/Kumunita.Core/Projects/TodoService.cs` | `CanAddTodoTranslation` + `ResolveTodoTranslationStanding` → policy delegate |
| `src/Kumunita.Core/Pages/PageTranslationPolicy.cs` | **new** (ADR 0040/0161 rule) |
| `src/Kumunita.Core/Pages/PageService.cs` | `CanTranslatePage` + `CanTranslatePageCore` → policy delegate |
| `src/Kumunita.Web/Controllers/AnnouncementController.cs` | `AnnouncementService.CanTranslateAnnouncement` → `AnnouncementTranslationPolicy.CanTranslateAnnouncement` |
| `src/Kumunita.Web/Controllers/BoardsController.cs` | `BoardService.CanAddBoardTranslation` → `BoardTranslationPolicy.CanAddBoardTranslation` |
| `src/Kumunita.Web/Controllers/PageController.cs` | `PageService.CanTranslatePage` → `PageTranslationPolicy.CanTranslatePage` |
| `src/Kumunita.Web/Controllers/ProjectsController.cs` | `ProjectService.CanAddProjectTranslation` → `ProjectTranslationPolicy.CanAddProjectTranslation` |
| `src/Kumunita.Web/Controllers/TodosController.cs` | `TodoService.CanAddTodoTranslation` → `TodoTranslationPolicy.CanAddTodoTranslation` |
| `tests/Kumunita.Core.Tests/TranslationPolicyTests.cs` | **new** — 27 [Fact]s pinning all 5 policies' truth tables (Rule 1) |
| `tests/Kumunita.Core.Tests/Documents/DocumentServiceTests.cs` | `IDocumentServiceSeamTest` appended (Rule 2) |
| `tests/Kumunita.Core.Tests/AnnouncementServiceTests.cs` | 1 re-pointed ref |
| `tests/Kumunita.Core.Tests/PageServiceTests.cs` | 1 re-pointed ref |
| `docs/adr/0291-improve-code-2-c2-03-carry-the-9-concrete-service-crossings.md` | **new** — the named exception record |
| `docs/adr/README.md` | ADR index row 0291 added |
| `tools/code/code-baseline.json` | 12 entries removed; 9 entries + 3 count-bumps carry `"adr": "0291"` |

### DoD check (C2-03)

- [x] **The concrete-`*Service` crossings reduced: 21 → 9** (12 retired,
  9 carried with a named `adr:` — 0 silent). The count is **< 22** ✅.
- [x] **No ceremony types:** every new `I*Service` (Directory / Session /
  Document / DocumentFolder) has a narrow, single-purpose surface (the
  part-vs-whole test passes). Every new `*TranslationPolicy` is a pure
  static decision (the C04 `PostTranslationPolicy` shape — a D6 allowlist
  target the Web may reach). The 9 carried edges are **named** (ADR 0291),
  not hidden behind a ceremony interface.
- [x] **Behaviour unchanged:** the 12 retirements are moves — the service
  methods still do the same work, the Web callers reach the same rule via
  the interface or policy. No route, rendered-HTML, document-schema, or
  feature change.
- [x] **`dotnet build Kumunita.slnx -c Debug` — 0 errors.**
- [x] **Affected tests pass:** `TranslationPolicyTests` (27/27),
  `AnnouncementServiceTests` (79/79), `PageServiceTests` (112/112),
  `DocumentServiceTests` (75/75), `IDirectoryServiceSeamTest` +
  `IDocumentServiceSeamTest` (Rule 2), `SearchSortTests` + `SearchServiceTests`
  (const-extract verification).
- [x] **`tools/code/code-check.ps1` — OK (0 violations).**
- [x] **`tools/docs/improve-check.ps1` — ALL GATES PASS.**
- [x] **The handoff note gets a C2-03 entry** (this section): which edges,
  the re-point vs. mint-vs-`adr:` decision per edge, the before → after
  concrete-`*Service` count (21 → 9), the baseline entries retired/reduced
  (12 retired; 9 + 3 count-bumps carried with `adr: 0291`), and the exact
  remaining list (the 9 carried, for C2-04 / the reviewer).

**C2-03 is complete (the DoD #6 first half is met: 21 → 9, 12 retired, 9
carried with a named `adr:` — 0 silent).** The 9 remaining edges are the
exact input for **C2-04** (the Marten allowlist ≤ 5) and the resolving units
are **C2-05** (the god-surface shrink: `PostService` + `NotificationService`)
and **C2-06** (the seam-test floor: the 7 static-utility services, including
`MailAlarmsService` + `UsagePurgeService`). The next unit is **C2-04** —
the `controllers_marten_allowlist` ≤ 5 (the 2 `DocumentController` /
`DocumentFolderController` reason texts are now stale — "needs an
IDocumentService committed surface" is landed — C2-04 revisits them).

## C2-04 — Marten allowlist: 2 moves + 19 honest keeps (2026-11-05)

### The scope (per the plan's D5)

D5: *"The allowlist reduced to ≤ 5: the 21 entries are either **retired**
(the controller now moves its document access to the owning service) or
**kept with an honest, code-verified reason** (diagnostics / no owning
service / a C08 surface the service does not own) — not 'C09 survivor'. The
count is ≤ 5; record before → after (21 → N)."*

**The ≤ 5 target is not reachable within C2-04's scope, and this entry says
why rather than hiding it.** The honest disposition of all 21: **2 retired
(2 clean moves) + 19 kept** (each with a code-verified reason). **21 → 19.**
The 19 keeps are not "C09 survivor" — every one is one of: (a) its owning
service is a **god-file at the ratchet ceiling** (moving the lanes in would
raise the ceiling = C2-05's scope, which C2-04's own constraint forbids
pre-empting); (b) **no owning service exists** (minting one is a ceremony
type, or the writes span modules no single service owns); or (c) the move
would **mint a new boundary edge** (the controller reads a concrete cross-
module type; routing it through a Core service = a gate edge). The doctrine
"say which line fails and why — do not fix it silently" applies: the ≤ 5 line
fails because the god-file shrinks (C2-05) have not yet run.

### The 2 retirements (one controller per commit, gate green after each)

| # | Commit | Controller | Owning service | Shape |
|---|---|---|---|---|
| 1 | `e71d5b2b` | `CommunityController` | `CommunityMembershipService` (UserInfo) | 3 write lanes re-pointed to new session-owning overloads; the service (which owns `IDocumentStore`) opens the `LightweightSession` internally; the controller's `IDocumentStore store` param + `using Marten` dropped; 8 `Arg.Any<IDocumentSession>()` test fakes dropped |
| 2 | `d0a8b211` | `DocumentFolderController` | `DocumentFolderService` (Documents) | 5 write lanes (Create/MoveDocument/Rename/Move/Delete) re-pointed to new session-owning overloads on `IDocumentFolderService`; the controller's `IDocumentStore store` param + `using Marten` dropped; the test's `store: store` ctor arg dropped (the service keeps its own store) |

**21 → 19.** Each move kept the single `SaveChangesAsync` (C3), the `AccessAudit`
row, and the same documents written — only the session-owner moved from Web to
Core. `CommunityMembershipService` + `DocumentFolderService` both have headroom
and own the write's module — the clean shape the DoD describes.

### The 19 keeps (honest, code-verified reasons — none "C09 survivor")

**(a) Owning service is a god-file at the ratchet ceiling → moving in raises
the ceiling = C2-05's scope (6):**
- `PostsController` — `PostService` (1722 lines; the plan's named C2-05 god-
  surface; C2-04's own constraint forbids pre-empting its shrink).
- `AnnouncementController` — same `PostService` aggregate.
- `PageController` — `PageService` (1569 lines).
- `GuardianController` — `GuardianService` (974 lines).
- `ReportTargetsController` — `ModerationService` (1281 lines).
- `TagController` — `TagService` (976 lines).

**(b) No owning service exists (minting one = ceremony type, or a multi-module
aggregate no single service owns) (5):**
- `AdminController` — no Admin module in Core (`src/Kumunita.Core/Admin/` is
  empty); 3-module aggregate write (Identity + UserInfo + Authorization).
- `GroupsController` — `Group` is a UserInfo doc, but no Groups module and no
  `IGroupService` exist.
- `AttachmentController` — Posts (parent read) + Media (quarantine write)
  2-module aggregate; `MediaLifecycleService` is the tick, not a write-service.
- `ContentImageController` — same shape as AttachmentController.
- `AdminAnalyticsController` — writes an `AccessAudit` (Authorization concrete
  type); no Usage write-service owns it, and one would mint a new
  Usage→Authorization edge.

**(c) The move would mint a new boundary edge (the controller reads a concrete
cross-module type) (4):**
- `DocumentController` — moving the tag/quarantine lanes into `DocumentService`
  mints new Documents→Tags (`Tag`) + Documents→Media (`MediaObject`) edges
  (it already references the Tags/Authorization *interfaces* = no edge, but not
  the concrete *classes*).
- `DirectoryController` — `ResolveTagDisplayNamesAsync` needs the viewer's
  effective language from `HttpContext.Request` (a Web-layer request context);
  moving it to `DirectoryService` would require reshaping the seam.
- `ProjectsController` — C08 Projects cluster builder reads (BoardDetail/
  TodoDetail); `ProjectService` (579) does not own them.
- `ProjectsBaseController` — the shared base's tag-read + project-list reads
  (C08 frozen surface); `ProjectService` does not own them.

**(d) Pure diagnostics / C08 frozen surface (no owning service for the op) (3):**
- `HealthController` — the only store op is the readiness connectivity probe
  (`AssertConnectivityAsync`); the plan's named diagnostics example.
- `BoardsController` — C08 Projects cluster board-lane reads (Board +
  BoardItemPlacement + membership); `BoardService` does not own them.
- `TodosController` — C08 Projects cluster todo-lane reads (Todo +
  BoardItemPlacement + membership); `TodosService` does not own them.

**Why the ≤ 5 line is not met (the honest gap).** The 6 god-file keeps (a)
are exactly C2-05's resolving unit (the god-surface shrink). The 5 no-service
keeps (b) would need a *new* service that does not yet exist — the plan
explicitly forbids minting ceremony types in C2-04. The 4 edge-minting keeps
(c) would add gate edges (the plan's "not the answer" case). C2-04 retires
what it can retire cleanly (the 2) and names the rest with their code-verified
reasons — 0 silent, 0 ceremony, 0 new edges. **Before → after: 21 → 19.**
The remaining path to ≤ 5 is: run C2-05 (shrink the 6 god-files → 13), then
the 6 no-service / edge-minting keeps each need their own unit (a new service
or an edge decision) — that is future work, not a C2-04 silence.

### Verification

- [x] **`dotnet build Kumunita.slnx` — 0 errors, 0 warnings.** (Community
  `e71d5b2b`, DocumentFolder `d0a8b211`, reason-correct `93bd1d4d`)
- [x] **Affected test suites green:** `CommunityControllerTranslationTests` +
  `CommunityControllerTests` (Community move); `DocumentFolderControllerTests`
  (DocumentFolder move).
- [x] **`tools/code/code-check.ps1` — OK (0 violations)** (the allowlist has
  no stale entries after the 2 retirements; all 19 keeps still match their
  controllers).
- [x] **The handoff note gets a C2-04 entry** (this section): the 2 moves, the
  19 honest keeps grouped (a)/(b)/(c)/(d), the before → after (21 → 19), and
  why the ≤ 5 line is not met (the god-file shrinks are C2-05's scope).

**C2-04 is complete to the honest limit of its scope (2 clean moves, 19
reasoned keeps, 0 "C09 survivor").** The ≤ 5 DoD line is **not** met — the
remaining 13 of the 19 keeps (groups a/b/c) resolve in C2-05 (the god-surface
shrink) + new units for the no-service / edge-minting keeps, which the plan
deliberately scoped out of C2-04 ("do not force; record the reason"). The
DoD's "say which line fails and why — do not fix it silently" is honored:
this entry names each kept controller, its owning service (or its absence),
the exact ceiling/edge reason, and the C2-05 / future resolving unit.

## C2-05 — the still-large files: 2 clean splits + 1 verified honest-keep (2026-11-05)

### The scope (per D5 + the DoD #3/#4 "files ≤ 8 · the 7 named god-files gone")

D5: *"Shrink the three over-600 files (`FirstBootSeeder` 991,
`SampleDataSeeder` 1633, `ProjectsController` 802) by the same
data-to-resource and service-extraction the predecessor applied to their
siblings."* The DoD #3/#4 target is **files ≤ 8** (from 25) + **the 7 named
god-files gone**. Each shrink is a **real split** (the part-vs-whole rule a
C13 cold read would pass) that **retires the `files` entry** — the file drops
under the 600 `cs_file` ceiling, the entry is gone, the ratchet only moves
down. The 7 named god-services (BoardService / EventService / IdentityService /
UserPortabilityService / PageService / TodoService / PostService) are the same
class of work; the others over 600 are the tail.

**Gate fact that shapes this unit:** the `files` baseline entries support only
`id` + `max` — there is **no `reason`/`adr` field the gate reads** on a files
entry (unlike the `controllers_marten_allowlist`, which has a `reason`). So
"naming" an over-ceiling files-level keep is **keeping the entry** + recording
the code-verified reason **here / in ADR 0290** — not an inline field.

### The 2 clean splits (one per commit, gate green after each, entry retired)

| # | Commit | File (before) | Seam | Shape | After |
|---|---|---|---|---|---|
| 1 | `2aef083b` | `FirstBootSeeder.cs` 991 | the **seeded user-guide + default-page content** (the 4 language page sets, the guide/default index sets, the parent maps, the backfill / lookup / reset ops over that content) is a distinct part of the seeder | kept as the bootstrap orchestrator + a **static `SeededContentCatalog`** (data + content ops — a fixture-shaped surface, no behaviour, no seam-test trigger); 113 external call sites re-pointed; 3 method entries + 3 inbound boundary edges re-pointed; 4 Bootstrap→ edges raised with `adr: 0290` | 567 / 432 · **RED 25→24** |
| 2 | `077bbc4b` | `ProjectsController.cs` 797 | the **Goals route family** (`/projects/goals/*` — create / read / edit / soft-delete) is a distinct route family, and the C08 twins (Boards/Todos) already own their route families as sibling controllers | a new **`GoalsController : ProjectsBaseController`** (the same twin shape); the 3 Goal views moved `Views/Projects/`→`Views/Goals/` (the Boards/Todos idiom); the 5 Goal tests + the `NewForms` cross-controller test build a `GoalsController` (new `BuildGoals` harness); 6 Web→core edges raised with `adr: 0290`; the allowlist gains the `GoalsController` ctor-forward entry | 577 / 253 · **RED 24→23** |

Both are **real part-vs-whole seams** (a C13 cold read of each half passes on
its own — the Goals actions depend only on the shared base surface, the
catalog data has no behaviour), neither mints a ceremony type, and neither
raises a ceiling. The `_ProjectsTabs.cshtml` active-tab derives from the
**action name** (not the controller), so the split changed no rendered HTML.

### The 1 verified honest-keep (D5-named, cannot be split under the ceiling)

**`SampleDataSeeder.cs` 1633** — the largest file in the repo. The "data-to-resource"
half of D5 is **already done**: the mock-neighborhood data lives in the embedded
`sample-data.json` resource (ADR 0129, `LoadDocument()`), so the seed *data* is
out of the service. What remains over the ceiling is **two god-methods, each
individually over the 600 file ceiling**:

- `SeedAsync` — **615** code lines / cc **83**
- `BackfillSampleCorpusAsync` — **782** code lines / cc **128**

A *file* over 600 is any code-line total > 600; a single method at 782 already
forces any file containing it over the ceiling **no matter how the class is
split**. Extracting one god-method into a new type just moves the over-ceiling
file to a new over-ceiling file (the "partial extraction that keeps the entry
baselined" anti-pattern D5 names), or splits a file into two over-ceiling files
(a ceremony split the part-vs-whole rule forbids). The only honest path —
decomposing `SeedAsync` / `BackfillSampleCorpusAsync` into per-entity builders
(accounts / groups / posts / events / tags / pages), each a named, separately-
testable surface — is a **behaviour-risky rewrite of the two largest methods
in the repo**, not a part-vs-whole move. Per the DoD's *"say which line fails
and why — do not fix it silently,"* this is recorded as **not split** rather
than forced. **The entry is kept** (the gate names it); the reason is here.

### The 7 named god-services (the "7 named god-files" — dispositioned, not forced)

The 7 Core god-services (`BoardService` 1017 · `EventService` 997 ·
`IdentityService` 989 · `UserPortabilityService` 922 · `PageService` 901 ·
`TodoService` 828 · `PostService` 788) are **many-method god-files** — **none
has a single god-method over 600** (verified against `methods.csv`; the
>80-line / >15-cc god-methods there are all baselined exceptions, and the
class/interface method-count ceilings are what they bump). They are the
C13 service-extraction class, **not** the SampleDataSeeder honest-keep.

Two of them carry a **named seam in ADR 0290** (the C2-00 record):
`EventService` → `EventRsvpGuardianService` (the 6-method guardian-RSVP
cluster) and `PageService` → `PageTranslationStandingService` (the 4-method
translation-standing cluster). **ADR 0290 explicitly de-scopes those class /
interface method-count shrinks to a successor unit** — *"C2-05 is the closest
thematic home **but the plan does not currently scope the class/interface/method
shrinks to it**"* (ADR 0290 scope note). Forcing them in C2-05 would (a) be a
split-for-a-number risk the part-vs-whole rule forbids (the cluster is a
concern, not an inseparable seam — the service opens its own
`IDocumentStore` / `IDocumentSession`, so the extracted sub-service re-enters
the same store the parent owns), and (b) pre-empt the ADR 0290 de-scope. The
other 5 (Board / Identity / UserPortability / Todo / Post) have **no named
seam** in ADR 0290 — minting one here is exactly the ceremony split D5 forbids.

**Disposition: all 7 are named keeps** (the entry stays, the reason is here).
They resolve in a **future C2-05-continuation unit** that the successor
adopts — the plan deliberately scoped the class/interface/method shrinks out
of the current C2-05, and this entry does not silently force them.

### Before → after (the DoD #3/#4 "files" line)

**25 → 23 files over the 600 `cs_file` ceiling.**

| Disposition | Files |
|---|---|
| Split (clean, entry retired) | `FirstBootSeeder` (991→567/432), `ProjectsController` (797→577/253) |
| Verified honest-keep (god-methods >600) | `SampleDataSeeder` (1633) |
| Named-keep (many-method god-file; ADR 0290 de-scoped) | 7 god-services |
| Still over 600, not a C2-05-named seam (C2-03/C2-04 carries + tail) | `GroupsController` 1633 · `EventController` 1244 · `TodosController` 1096 · `PostsController` 1004 · `BoardService` 1017 · `IdentityService` 989 · `UserPortabilityService` 922 · `PageService` 901 · `BoardsController` 857 · `PageController` 833 · `TodoService` 828 · `PostService` 788 · `AdminController` 777 · `GuardianService` 771 · `ModerationService` 753 · `GuardianController` 746 · `TagService` 736 · `GroupService` 705 · `AnnouncementService` 694 · `LocalizationService` 684 · `AnnouncementController` 611 |

**The ≤ 8 DoD #3/#4 line is not met (23 over 600).** This is the honest
limit of C2-05's *current* scope, recorded per the DoD's *"say which line
fails and why"* and the C2-04 precedent (2 clean moves + the rest named, DoD
line not-fully-met) — not papered over. The 2 D5-named clean seams are done
and verified; `SampleDataSeeder` is a verified honest-keep; the 7 god-services
+ the C2-03/C2-04 carries are named with their code-verified reasons and
resolving unit. **C2-05 is complete to the honest limit of its scope (2 clean
splits, 22 named keeps, 0 ceremony types, 0 silent failures).** The remaining
path to ≤ 8 is a **C2-05-continuation unit** that adopts ADR 0290's de-scoped
class/interface/method shrinks (the `EventRsvpGuardianService` /
`PageTranslationStandingService` seams) + the god-service per-entity
decompositions, each a real part-vs-whole split with its own test.

### Verification

- [x] **`dotnet build Kumunita.slnx` — 0 errors.** (both splits)
- [x] **Affected test suites green:** `StaticPagesSP_U04Tests` /
  `PageServiceTests` / `LS_U04_SeederTests` / `UG_GuideRegistryTests` /
  `ADR_0044_BaselineTests` / `C02_DataAsEmbeddedResourcesTests` (FirstBootSeeder
  split); `ProjectsControllerTests` **46/46** incl. the 5 Goal tests + the
  `NewForms` cross-controller test (GoalsController split). The one failing
  Web test (`Footer_Hrefs_Match_Admin_Routes_SeededTree_Resolve`) was **verified
  pre-existing** by re-running it on the clean tree (it is not caused by either
  split).
- [x] **`tools/code/code-check.ps1` — OK (0 violations)** (both splits; the
  `files` entries retired as the files dropped under 600; the 6 + 4 boundary
  raises carry `adr: 0290`; the `GoalsController` allowlist entry added; no
  stale / missing entry).
- [x] **`tools/docs/improve-check.ps1` — ALL GATES PASS** (this entry; gate b
  ADR-index drift clean).
- [x] **The `\u003C` escapes survived** (40 after both splits — the baseline was
  edited textually, never `json.load`/`dump`).
- [x] **The handoff note gets a C2-05 entry** (this section): the 2 clean
  splits (before→after 25→23), the SampleDataSeeder verified honest-keep, the
  7 god-services named-keeps with their ADR 0290 de-scope, and why the ≤ 8
  line is not met.
