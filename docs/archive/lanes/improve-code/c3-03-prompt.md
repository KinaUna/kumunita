---
id: c3-03-prompt
title: "C3-03 — the 5 god-services: real part-vs-whole splits + the group-(a) allowlist keeps their services unblock"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C3-03 — the 5 god-services: real part-vs-whole splits + the group-(a) allowlist keeps their services unblock

## What you are doing

You are the **fourth** agent on the **IMPROVE-CODE-3** lane (plan:
`docs/archive/lanes/improve-code/plan-improve-code-3.md`). Your unit is **C3-03**, the
fourth of 9. C3-00 (the `EventService` shrink), C3-01 (the `PageService`
shrink), and C3-02 (the `SampleDataSeeder` decomposition) are all done — read
their handoff entries before you start (below). Each had the **same honest
finding** that you must carry into this unit: the extraction shrinks the
service's file but **does not reliably get it under the 600 `cs_file` ceiling**
(C3-00: `EventService` 997 → 709, still over; C3-01: `PageService` 917 → 852,
still over; C3-02: `SampleDataSeeder` *grew* 1633 → 2060, still over — the
per-entity helper signatures are net-positive on code lines). **C3-03 is the
same class of risk for 5 services at once** — the plan's D1 claim that *"each
shrink that drops the service under 600 retires its `files` entry"** is a
**claim to verify against the gate, not a fact to assume**. The DoD for this
unit (D1): each of the 5 god-services (`BoardService`, `TodoService`,
`PostService`, `IdentityService`, `UserPortabilityService`) gets a **real
part-vs-whole split with its own unit test**; the `files` / `classes` /
`interfaces` entries retire **only if** the service actually drops under the
ceilings; the group-(a) allowlist keeps retire **only if** the owning service
drops under 600. **Measure every one of those against
`pwsh tools/code/code-check.ps1`; do not assume the plan's "retires" line
holds.**

**You are NOT** doing C3-04 (the still-large-file tail — `GroupsController`
1639, `EventController` 1249, `TodosController` 1096, `BoardsController` 857,
`AdminController` 777, `GuardianController` 747, `GroupService` 705,
`AnnouncementService` 704, `LocalizationService` 684, `AnnouncementController`
611, `ModerationService` 753, `GuardianService` 771, `TagService` 736 — those
are C3-04's, not this unit's), C3-05 / C3-06 (the other allowlist retirements
— the group-(b) no-service, group-(c) edge-minting, group-(d) diagnostics keeps),
C3-07 (the 3 flagged-accidental boundary edges), or C3-08 (the close). You are
NOT re-opening the met lines (#1 ratchet, #5 translation, #6 concrete-edges,
#7 untested services, #8 SEC pointers) — those hold and stay. You are NOT
touching routes, rendered HTML, document schemas, or adding a feature. You are
NOT doing the other god-services (`GuardianService` / `ModerationService` /
`TagService`) — those are C3-04's tail.

**The ratchet is honest — do not re-fix the gate.** A unit that *shrinks* never
raises; a unit that must *raise* names the ADR it amends or creates in the same
commit. **This unit may need to raise** — if a service's file *grows* under
the extraction (the C3-02 pattern: the extracted seam's signatures are
net-positive on code lines), the `files` entry is **raised with a new ADR**,
not papered over with a ceremony split. The DoD's *"total over-ceiling surface
strictly smaller, 0 silent raises"* still holds — a raise is allowed, but it
must be named.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `b09dc8b4` (the C3-02 close).
- Commit style: `feat(improve-code-3): C3-03 — <short description>` (conventional
  commits, em dash, **no ADR in the subject** — if a raise is needed, the ADR
  goes in the body, as C3-02 did with ADR 0292).
- Read `AGENTS.md` and `.github/copilot-instructions.md` first (the doctrine).
- Read the plan: `docs/archive/lanes/improve-code/plan-improve-code-3.md` — **Why now**,
  **D1** (the god-service shrinks), **D3** (the Marten allowlist, the
  group-(a) half), and the **C3-03** row are your spec.
- Read the handoff: `docs/archive/lanes/improve-code/improve-code-3-handoff-notes.md` —
  the **C3-00**, **C3-01**, and **C3-02** entries (all three honest findings:
  `files` tightened or raised, not retired; the C3-02 entry records the
  `SampleDataSeeder` file *grew* 1633 → 2060 under the per-entity
  decomposition, which is the exact risk this unit faces for 5 services).
- Read ADR 0290 (the 4 named seams, 2 already landed) and ADR 0292 (the
  C3-02 files-entry honest-keep raise, the precedent for the *raise* path
  this unit may need to take for a god-service whose file grows under
  extraction).

### The 5 god-services and their baseline entries (current, at HEAD `b09dc8b4`)

| Service | `files` entry | `classes` entry | `interfaces` entry | `methods` entries (over 80/15) | group-(a) allowlist keep it unblocks |
|---|---|---|---|---|---|
| `BoardService` | **1025** | — (not on classes) | `IBoardService` **21** (over 20) | 3 (MoveTodoToAdd 62/cc19, MoveTodoToLane 77/cc22, UpdateLaneAsync 33/cc17) | — (BoardsController is on the allowlist but is a group-(d) C08 keep, not a group-(a) keep — it does not unblock on `BoardService`) |
| `TodoService` | **839** | — (not on classes) | — (not on interfaces) | 3 (BuildTodoAssi 28/cc17, ComputeTodoEd 20/cc20, CreateTodoCom 68/cc18) | — (TodosController is a group-(d) C08 keep, not a group-(a) keep) |
| `PostService` | **821** | — (not on classes) | — (not on interfaces) | 2 (CreatePostAsync 90/cc17, CreateReplyAsync 65/cc16) | **`PostsController`** (group-(a) — *"the post/reply create/edit/delete lanes write the Post + its Translations… the owning PostService is a god-file at its ratchet ceiling"*) |
| `IdentityService` | **989** | **33** (over 25) | `IIdentityService` **33** (over 20) | 0 (no over-ceiling methods) | — (no allowlist keep names IdentityService) |
| `UserPortabilityService` | **922** | — (not on classes) | — (not on interfaces) | 6 (4 in the 38–46/cc 16–20 band, 1 at 24/cc16, 1 at 39/cc20) | — (no allowlist keep names UserPortabilityService) |

**The DoD totals at the start of C3-03 (measured at HEAD `b09dc8b4`):**
`files` **23** · `methods` **89** · `classes` **5** · `interfaces` **8** ·
`controllers_marten_allowlist` **20** · `boundary_violations` **465**.

**The DoD targets (the plan's D1 + D3):** the 5 god-services each get a real
part-vs-whole split; the `files` / `classes` / `interfaces` entries retire as
each service drops under the ceilings; the `PostsController` group-(a) allowlist
keep retires (if `PostService` drops under 600); **methods** strictly smaller
(the over-ceiling method entries retire as their service drops under the
ceiling); **files** strictly smaller (the 5 god-service files retire as they
drop under 600 — the plan's *"the 23 → ≤ 8"* target is the C3-04 close, not
this unit's; this unit's honest target is *"as many of the 5 god-service files
as the gate confirms dropped under 600, retired; the rest tightened or raised
with an ADR"*).

## The work

### Part 1 — the 5 god-service splits (in dependency order)

D1: *"…then the remaining 5 god-services (`BoardService`, `TodoService`,
`PostService`, `IdentityService`, `UserPortabilityService`) each as a real
part-vs-whole split with its own unit test."*

**The order matters** (the dependency demand — `PostService` unblocks the
`PostsController` allowlist keep, which is the one DoD #6 line that moves in
this unit): `PostService` first (the only one that unblocks an allowlist keep),
then `IdentityService` (the only one with a `classes` + `interfaces` entry),
then `UserPortabilityService` (6 over-ceiling methods — the largest method
surface), then `BoardService` + `TodoService` (the two with `interfaces` /
over-ceiling method entries but no allowlist keep).

**The shape of each split:** a **real part-vs-whole seam** (a C13 cold read
would pass on the extracted type on its own — the concern extracted is a
named domain concept with its own standing / policy / guard, not a mechanical
"extract N methods" ceremony). The extracted type is a **new, named type**
(a `*Policy` / `*Resolver` / `*Evaluator` / `*Builder` static class, or an
`I*Service` + `*Service` + DI registration if the concern is a multi-call-site
service surface) with **its own unit test** (the part-vs-whole rule requires
it — ≥2 callers, or its own unit test, or an owned route family / aggregate).
The extracted type must be **under every ceiling** (the file < 600, the class
< 25 public methods, the interface < 20 methods, the methods < 80 lines / 15
cc) — otherwise it inherits the god-service's over-ceiling status and the
split is a ceremony, not a reduction.

**The service that remains** (after the extraction) must drop **under the
600 `cs_file` ceiling** for its `files` entry to retire. If it does not, the
`files` entry is **tightened** (max → measured) **or raised with an ADR** (if
it grew under the extraction, the C3-02 pattern). **Say which for each of the
5 services** — the gate is the oracle, not the plan's "retires" claim.

**The extracted seam's test:** the part-vs-whole rule requires a **unit test**
for the new type. If the seam is a pure decision helper (no DB, no session),
a **plain unit test** (no `PostgresFixture`) satisfies the gate's seam-test
Rule 1 (which requires `*Policy` / `*Resolver` / `*Evaluator` / `*Builder`
classes to be named by a plain unit test). If the seam needs a DB, the
`PostgresFixture` harness is the same harness C3-00 / C3-01 used for
`EventRsvpGuardianService` / `PageStandingPolicy` — but those were static
decision helpers (no DB), so check the actual shape of each seam before
assuming the test shape.

**No new boundary edge.** The extracted seam lives in the same module as the
god-service it comes out of (e.g. `PostService` → the `Posts` module;
`BoardService` / `TodoService` → the `Projects` module; `IdentityService` →
the `Identity` module; `UserPortabilityService` → the `Portability` module).
It references the same cross-module types the god-service already referenced.
Verify against the gate — the C3-01 boundary-grow trap (minting a file in a
module that crosses a new edge) applies here.

**Comment rule** (what/when/throws + the ADR that names the seam) on each
extracted type.

### Part 2 — retire/tighten/raise the baseline entries the splits free

After each split, run the authoritative gate (`pwsh tools/code/code-check.ps1`)
and **measure** (do not assume the plan's "retires" line — C3-00, C3-01, C3-02
all proved the plan's file claim can be wrong):

- **The 5 `files` entries** — for each service, if the extraction drops it
  **under** 600 → the `files` entry **retires** (the DoD #3/#4 "files" line
  moves 23 → 22 → 21 … per retirement). **If it is still over 600** → the
  `files` entry is **tightened** (max → measured), **not retired**. **If it
  grew under the extraction** (the C3-02 pattern) → the `files` entry is
  **raised with a new ADR** (the C3-02 ADR 0292 precedent). **Say which for
  each of the 5.**
- **The 5 services' `classes` / `interfaces` entries** — `IdentityService`
  (33, over 25) + `IIdentityService` (33, over 20) + `IBoardService` (21,
  over 20). If the extraction drops the public-method count **under** the
  ceiling (25 for classes, 20 for interfaces) → the entry **retires**. If
  still over → **tightened**. **Say which for each.**
- **The 5 services' over-ceiling `methods` entries** (14 total: 3 BoardService
  + 3 TodoService + 2 PostService + 0 IdentityService + 6 UserPortabilityService)
  — if the extraction moves the method to the extracted seam (which is under
  the ceiling) → the `methods` entry **retires** (stale → gone). If the method
  stays in the god-service and is still over the ceiling → **tightened** (max
  → measured). **Say which for each of the 14.**
- **The `PostsController` allowlist entry** (DoD #6 group-(a)) — **retires**
  **only if** `PostService` drops under 600. If `PostService` is still over
  600 after the extraction, the allowlist entry **stays** (the reason text is
  updated to name C3-03's measurement — not a forced retirement, not a silent
  keep). The DoD #6 allowlist line moves 20 → 19 **only in that case**.
  **Say which.**

**The `--report` top-10 is a preview; `--check` is authoritative.** Verify
every retire/tighten/raise decision against `pwsh tools/code/code-check.ps1`
before touching `tools/code/code-baseline.json`.

**Edit the baseline TEXTUALLY** — a `json.load`/`dump` round-trip churns the
`\u003C` escapes (C2-01…C3-02 all hit this).

### Part 3 — (no Part 3 for this unit)

C3-03 does not unblock the other allowlist entries (the group-(b) no-service,
group-(c) edge-minting, group-(d) diagnostics keeps are C3-05 / C3-06's). The
DoD #6 allowlist line moves **at most** 20 → 19 in this unit (the
`PostsController` group-(a) keep, if `PostService` drops under 600).

## Verification (all green before the next unit)

- [ ] **`dotnet build Kumunita.slnx -c Debug` — 0 errors.**
- [ ] **The affected test suites pass** (the existing unit tests for each of
  the 5 god-services + the new unit tests for the 5 extracted seams + any
  cross-service integration tests that touch the split surface — the seam is
  a real part-vs-whole surface, so its unit test is a new file, not a
  re-point of an existing one).
- [ ] **`pwsh tools/code/code-check.ps1` — OK (0 violations).** Every
  retire/tighten/raise decision matches the measured value. **State the
  before → after for each of the 5 services' `files` / `classes` /
  `interfaces` / `methods` entries** — retired, tightened, or raised-with-ADR,
  and why. **State whether the `PostsController` allowlist entry retired**
  (DoD #6 line 20 → 19) or stayed.
- [ ] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS** (if a new ADR is
  created for a raise, the ADR-index gate (b) needs the row added to
  `docs/adr/README.md` — C3-02 hit this; add it in the same commit).
- [ ] **The `\u003C` escapes survived** (the baseline was edited textually,
  never `json.load`/`dump`).
- [ ] **The handoff note gets a C3-03 entry** (the scope, the before → after
  for each of the 5 services' entries, the extracted seam shape per service,
  the `PostsController` allowlist decision, and the gate runs).

## Definition of done (C3-03)

- [ ] **The 5 god-services are each split into a real part-vs-whole seam**
  (a C13 cold read of each extracted type passes on its own — the concern is
  a named domain concept with its own standing / policy / guard; the service
  that remains is a thin orchestrator, or at least strictly smaller).
- [ ] **Each extracted seam has its own unit test** (the part-vs-whole rule;
  a plain unit test for pure decision helpers, the `PostgresFixture` harness
  for DB-dependent seams — match the shape of each seam).
- [ ] **The 5 services' baseline entries are resolved against the
  measurement** (the `files` / `classes` / `interfaces` / `methods` entries
  each retired, tightened, or raised-with-ADR to the true measured value — the
  ratchet only moved down or named, total over-ceiling surface strictly
  smaller, 0 silent raises).
- [ ] **The `PostsController` group-(a) allowlist entry retired or stayed**
  (DoD #6 line 20 → 19 or 20 → 20) — the decision is named against the
  `PostService` measurement, not assumed.
- [ ] **No new boundary edge** (the gate's `boundary_violations` count does
  not rise; the 5 extracted seams live in their god-service's module).
- [ ] **No route, rendered-HTML, document-schema, or feature change.**
- [ ] **All verification gates green** (build 0 errors, code-check OK,
  improve-check ALL GATES PASS, the 5 new seam tests + the affected suites
  pass).
- [ ] **The handoff note gets a C3-03 entry** (the shape above, with the
  honest before → after per service).

**If a DoD line fails, stop and say which one and why — do not fix it
silently.** C3-00, C3-01, and C3-02 all proved the plan's headline claim can
be wrong (the `files` entry tightened or raised, not retired) — C3-03's
*"each shrink that drops the service under 600 retires its `files` entry"* is
a claim to verify against the gate, not a fact to assume. **The one DoD line
that genuinely moves in this unit is the `methods` line** (the 14 over-ceiling
method entries across the 5 services retire or tighten as their service drops
under the ceiling) **and, if `PostService` drops under 600, the DoD #6
allowlist line** (20 → 19, the `PostsController` group-(a) keep). Everything
else in the plan's D1 for this unit (the `files` line 23 → ≤ 8) is a
**multi-unit effort** spanning C3-03 + C3-04, not a single-unit reduction.
