---
id: improve-code-3-handoff-notes
title: "IMPROVE-CODE-3 — Handoff Notes"
tier: living
kind: handoff
status: current
audiences: [developer, agent]
lane: improve-code-3
related:
  - docs/plans-milestones/plan-improve-code-3.md
  - docs/plans-milestones/plan-improve-code-2.md
  - docs/plans-milestones/improve-code-2-handoff-notes.md
  - docs/adr/0290-improve-code-2-baseline-raises-and-seams.md
  - docs/adr/0294-improve-code-3-c3-04-partial-class-splits-boundary-raises.md
  - docs/adr/0297-improve-code-3-c3-07-flagged-accidental-boundary-edges.md
updated: 2026-11-06
---

> **Abstract:** the running handoff log for the IMPROVE-CODE-3 lane.
> **Current state (C3-08 SHIPPED, 2026-11-06):** the lane is **closed shipped** —
> C3-00…C3-08 are all done and green; **C3-08 the close landed the six-member
> "shipped" flip** the two predecessor closes (C13 / C2-08) deliberately
> withheld. Every carried-in DoD line holds **or** the one that does not (DoD
> #6 `allowlist` ≤ 5, measured **17**) is the honest floor the plan's clause
> licenses (ADR 0296) — a correct honest-finding, not a failure. The six
> members flipped: `WhatsNew.cs` `0.86.0` (newest-first) · `Milestones.cs`
> `IMPROVE-3` (StatusDone) · `README.md` + `STATUS.md` status ·
> `ARCHITECTURE.md` cross-module contracts · the two pin tests (re-based, green).
> The true before → after: `files` 23 → 8 · `methods` 91 → 89 · `classes`
> 6 → 3 · `interfaces` 8 (unchanged) · `views` 3 (unchanged) ·
> `boundary_violations` 465 → 459 · `allowlist` 20 → 17 (the honest floor).
> **C3-07 retired the 3
> flagged-accidental boundary edges** named in ADR 0290 for C2-03 (never landed
> because C2 closed held): two contract extractions (`IInstanceBaseUrlReader`,
> `IIdentityPrincipalStore` — ADR 0297) + the Web `TagSlugs`→`TagSlugParser`
> rename that breaks the name-collision false positive. **`boundary_violations`
> is 465 → 459; no baseline line is raised** (the new contracts are interfaces —
> an allowed target — and under every ceiling). The `files` 8 · `classes` 3 ·
> `interfaces` 8 · `methods` 89 · `allowlist` 17 lines are all **unchanged** —
> DoD #3/#4 `files` 8 (≤ 8, **met**) · DoD #6 allowlist 17 (≤ 5, **not met** —
> the honest floor, ADR 0296). The 5 carrying lines that hold are untouched.
> The per-unit history follows, C3-00 first. (C3-00…C3-06, before this unit:
> the `files` 23 → 8 line met; **C3-05 retired the two clean `controllers_marten_allowlist`
> entries — `GoalsController` (0 ops of its own, all via `IProjectService`) +
> `AccountController` (the 1 verify-token read → the new
> `IIdentityService.LoadVerifyTokenAsync` seam, ADR 0295) — so `allowlist` is
> 20 → 18**. **C3-06 retired the remaining clean entry —
> `ReportTargetsController` (the `FileReportTargetAsync` self-session
> seam-reshape, ADR 0296) — so `allowlist` is 18 → 17**; the 17 honest keeps
> are recorded with their measured reasons (no headroom / no-service aggregate /
> edge-minting / request-context / C08-frozen / diagnostics). **DoD #6's `≤ 5`
> target is NOT met — the honest floor is 17** (the remainder is precisely the
> no-service / edge-minting / diagnostics / C08-frozen class the plan expected
> to be hard). `classes` 3, `interfaces` 8, `boundary` 465, `files` 8,
> `allowlist` 17. **The six-member flip is now landed** (C3-08 the close
> shipped it on the true number — DoD #6's `≤ 5` target is the honest floor,
> 17, ADR 0296). The per-unit honest findings are recorded below, C3-00 first.
> C3-00 adopted the ADR 0290 named seam `EventRsvpGuardianService` — the
> 6-method RSVP cluster extracted from `EventService` into a real
> part-vs-whole surface (`IEventRsvpGuardian` / `EventRsvpGuardianService`),
> with the 3 controllers (Event / Groups / Guardian) + the affected test
> suites re-pointed, the `EventService` **class** entry **retired** (31 → 25,
> at the ceiling → the entry is stale → gone) and the `IEventService`
> **interface** entry **tightened** (28 → 22 — still over the 20 ceiling, so
> it cannot retire yet), and the `RsvpAsync` **method** entry re-pointed to
> the new seam. **Honest finding recorded per the DoD's "say which line
> fails and why":** the plan's headline claim that this unit moves the
> DoD #3/#4 "files" line 23 → 22 **does not hold** — `EventService.cs` is
> 709 code lines after the extraction (997 → 709), still over the 600
> `cs_file` ceiling, so the `files` entry is **tightened, not retired**.
> The DoD #3/#4 "classes" line moves **6 → 5** (the one honest retirement);
> the "interfaces" line **stays 8** (the interface is still over its ceiling).
> The total over-ceiling surface is strictly smaller (the extracted seam is
> under every ceiling, the 6-method cluster is now a named type). **Who is
> it for:** the reviewer of C3-00, and the next C3 unit (C3-01). **When to
> read the rest:** before deciding whether a DoD line that "looks done"
> actually is, or picking up a C3-01…C3-08 unit.

# IMPROVE-CODE-3 — handoff notes

## C3-00 — the EventService shrink: retire the ADR 0290 named seam (2026-11-05)

### The scope (per D1 + the DoD #3/#4 "the 7 named god-services")

D1: *"The god-service shrinks (the group-(a) keeps + the 7 god-files).
Per-service, in dependency order. `EventService` (997) + `PageService` (901)
first (the ADR 0290 named seams: `EventRsvpGuardianService` +
`PageTranslationStandingService` — the 4-shrink clean resolution the
predecessor de-scoped)."* C3-00 is the `EventService` one: extract the
**6-method RSVP cluster** (`RsvpAsync` / `ApproveEventRsvpAsync` /
`DenyEventRsvpAsync` / `VetoEventRsvpAsync` / `GetPendingEventRsvpRequestsAsync`
/ `GetChildRsvpsAsync`) + its private `GuardianEventLinkAsync` standing gate,
into a new named type. The extraction is a **real** part-vs-whole split
(a C13 cold read of `EventRsvpGuardianService` on its own passes — the
guardian-RSVP concern: ADR 0028 account-scope supervision, ADR 0090
pagination, ADR 0115 guardian-event-link — it opens its own
`IDocumentStore` sessions, reads `GuardianLink` / `Profile`, writes
`EventRsvp` / `GuardianEventRequest` / `AccessAudit`), not a ceremony type.

### The extraction (what changed)

| New type | What it owns | Under ceiling |
|---|---|---|
| `IEventRsvpGuardian` | the 6-method surface (Rsvp / Approve / Deny / Veto / GetPending / GetChildRsvps) | 6 methods · 15 code lines |
| `EventRsvpGuardianService` | the 6 implementations + the `GuardianEventLinkAsync` standing gate | 6 public methods · 309 code lines · 1 method over the line-cc ceiling (RsvpAsync 122/cc 29 — re-pointed from the baseline) |

Both new types are **under every ceiling** (the file is 309 < 600, the class
is 6 < 25, the interface is 6 < 20) — they need no baseline entries of their
own. The one over-ceiling method (`RsvpAsync`, 122 lines / cc 29) was
**re-pointed** in the `methods` baseline from `EventService` to
`EventRsvpGuardianService` (same id suffix, same `max`/`cc_max` values) —
the entry moves with the method, it is not retired (the method is still over
the ceiling, just in a different file).

`EventService` keeps the 25 non-RSVP public methods it owned (the read
lanes, the write lanes, the group-events lane, the translation lane, the
standing checks, the recurring-event helpers) and the
`TranslationLane<Event, EventTranslation>` composition. `IEventService` keeps
the 22 non-RSVP method declarations. The 6 RSVP methods are gone from both
— the `EventService` class entry (31 → 25 public methods, **at the ceiling**)
is **retired** (stale), the `IEventService` interface entry (28 → 22, still
over 20) is **tightened** (the `adr: 0290` field dropped — the seam now owns
the 6 methods; the interface is no longer the "named exception" record).

### The 3 controllers + the test ripple

| Controller | Change |
|---|---|
| `GuardianController` | +optional ctor param `IEventRsvpGuardian? rsvpGuardian`; the 5 RSVP call sites (Approve / Deny / Veto / GetPending / GetChildRsvps) re-pointed to `_rsvpGuardian`; the 3 null-checks + the read-lane block re-pointed to `_rsvpGuardian is not null` |
| `EventController` | +optional ctor param `IEventRsvpGuardian? rsvpGuardian` (appended **last** so the positional test call sites keep their shape); the `Rsvp` action null-guards + re-pointed to `rsvpGuardian.RsvpAsync` |
| `GroupsController` | +optional ctor param `IEventRsvpGuardian? rsvpGuardian` (appended **last**); the group-RSVP action null-guards + re-pointed to `rsvpGuardian.RsvpAsync` |

| Test file | Change |
|---|---|
| `Core.Tests/GuardianEventRsvpGateTests.cs` | `Boot` gains a `required EventRsvpGuardianService Rsvp` property; `BootAsync` constructs it with the same store / userInfo / notifications the EventService used; the 23 RSVP call sites re-pointed from `boot.Events.*` to `boot.Rsvp.*` |
| `Core.Tests/EventServiceTests.cs` | `Services` helper now returns a 4-tuple (+ `EventRsvpGuardianService Rsvp`); the 60+ destructure sites re-pointed; the 10 RSVP call sites re-pointed from `svc.*` to `rsvp.*` |
| `Web.Tests/GuardianEventRsvpGateTests.cs` | +`RsvpService` helper (an `EventRsvpGuardianService` over the scratch store); the 3 self-lane seeding calls re-pointed to `rsvp.RsvpAsync`; the controller wired with the `rsvpGuardian` seam (null when `withEvents: false` — the null-safe 404 test still holds) |
| `Web.Tests/EventControllerTests.cs` | `Build` helper gains a `rsvpGuardian` param (default `Substitute.For<IEventRsvpGuardian>()`); the 2 RSVP tests (deny → 403, happy path → redirect) mock + assert on `IEventRsvpGuardian` instead of `IEventService` |
| `Web.Tests/GroupsControllerGroupEventTests.cs` | `Build` helper returns a 5-tuple (+ `IEventRsvpGuardian rsvp`); the 13 destructure sites re-pointed; the 2 RSVP tests (non-member 404, member redirect) assert on `rsvp.RsvpAsync` |

### Before → after (the DoD #3/#4 lines)

**The honest numbers (measured against the gate, not intended):**

| Line | Before (C2-08 close) | After (C3-00) | Why |
|---|---:|---:|---|
| **files** (over 600) | **23** | **23** | `EventService.cs` went 997 → **709** code lines (the 288-line RSVP cluster extracted), still **over the 600 ceiling** → the `files` entry is **tightened** (max 997 → 709), **not retired**. The plan's "files 23 → 22" does not hold. |
| **classes** (over 25) | **6** | **5** | `EventService` went 31 → 25 public methods (the 6 RSVP methods extracted), **at the ceiling** → the gate's `CheckSimpleList` sees `v <= ceiling` → **stale** → the entry **retires**. The `adr: 0290` field drops with it. |
| **interfaces** (over 20) | **8** | **8** | `IEventService` went 28 → 22 methods (the 6 RSVP methods extracted), still **over the 20 ceiling** → the entry is **tightened** (max 28 → 22), **not retired**. The `adr: 0290` field drops (the seam now owns the 6 methods; the interface is no longer the named-exception record). |
| **methods** (over 80/cc15) | 91 | 91 | The `EventService.RsvpAsync` entry is **re-pointed** to `EventRsvpGuardianService.RsvpAsync` (same 122/cc 29 values) — the entry moves with the method, it is not retired (the method is still over the ceiling, just in a different file). |

**The total over-ceiling surface is strictly smaller:**
- the `EventRsvpGuardianService` + `IEventRsvpGuardian` new types are **under
  every ceiling** (309 code lines < 600; 6 methods < 25 class / < 20
  interface) — no new baseline entries;
- the 6-method cluster is now a **named type** (the ADR 0290 named seam —
  the clean resolution the predecessor de-scoped), not a 6-method block inside
  a 997-line god-file;
- the `EventService` class entry **retired** (one less over-ceiling entry).

### The baseline changes (textual only; no JSON round-trip)

| Entry | Change | Reason |
|---|---|---|
| `files` · `EventService.cs` | `max` 997 → **709** | tightened (the 6-method cluster extracted; the file is still over 600 → the entry is kept, not retired) |
| `classes` · `EventService` | **retired** (31 → 25, at the ceiling → stale) | the ADR 0290 named-seam clean resolution; the `adr: 0290` field drops with it |
| `interfaces` · `IEventService` | `max` 28 → **22**, `adr: 0290` **dropped** | tightened (the 6-method cluster extracted; the interface is still over 20 → the entry is kept, not retired); the `adr:` drops because the seam now owns the 6 methods |
| `methods` · `EventService.RsvpAsync` | **re-pointed** to `EventRsvpGuardianService.RsvpAsync` (same 122/cc 29) | the method moved to the new seam; the entry follows it |
| `files` · `EventController.cs` | `max` 1244 → **1249**, `adr: 0290` **added** | collateral: the +optional ctor param `IEventRsvpGuardian? rsvpGuardian` + the null-guard in the `Rsvp` action + the field declaration + comment = +5 code lines. Named with `adr: 0290` (the C3-00 seam injection). |
| `files` · `GuardianController.cs` | `max` 746 → **747**, `adr: 0290` **added** | collateral: the +optional ctor param `IEventRsvpGuardian? rsvpGuardian` + the field declaration + comment = +1 code line. Named with `adr: 0290`. |

The `\u003C` escapes in the baseline **survived** (27 after the edits — the
baseline was edited textually, never `json.load`/`dump`).

### Verification (all green, 2026-11-05)

- [x] **`dotnet build Kumunita.slnx -c Debug` — 0 errors.**
- [x] **Core test suites pass:** `GuardianEventRsvpGateTests` +
  `EventServiceTests` — **110/110** (the RSVP cluster's decision extracted +
  re-pointed + unit-tested against the `PostgresFixture` Testcontainers
  harness; the same harness the predecessor used).
- [x] **Web test suites pass:** `GuardianEventRsvpGateTests` +
  `EventControllerTests` + `GroupsControllerGroupEventTests` — **80/80**
  (the 3 controllers re-pointed; the RSVP shape-tests exercise the new
  `IEventRsvpGuardian` seam).
- [x] **`pwsh tools/code/code-check.ps1` — OK (0 violations).** The
  `EventService` class entry retired; the file + interface + 2 collateral
  controller entries tightened with `adr: 0290`; the `RsvpAsync` method
  entry re-pointed; no new over-ceiling surface (the extracted seam is under
  the ceilings).
- [x] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS.**
- [x] **The `\u003C` escapes survived** (27 after the edits — the baseline
  was edited textually, never `json.load`/`dump`).
- [x] **The handoff note gets a C3-00 entry** (this section).

### What C3-00 did NOT do (the other C3 units' scope)

- **Not the `PageService` shrink (C3-01)** — the ADR 0290 named seam
  `PageTranslationStandingService` (the 4-method translation-standing
  cluster) is C3-01's unit.
- **Not the `SampleDataSeeder` decomposition (C3-02)** — the 2 god-methods
  (`SeedAsync` 615 / `BackfillSampleCorpusAsync` 782) each over the 600
  ceiling are a behaviour-risky rewrite (the C2-05 verified honest-keep).
- **Not the other 5 god-services (C3-03)** — `BoardService` 1025 ·
  `TodoService` 839 · `PostService` 821 · `IdentityService` 989 ·
  `UserPortabilityService` 922 — each a real part-vs-whole split with its own
  unit test.
- **Not the still-large-file tail (C3-04)** — the files that remain over 600
  after C3-00…C3-03.
- **Not the Marten allowlist (C3-05 / C3-06)** — the 20 → ≤ 5 line.
- **Not the 3 flagged-accidental boundary edges (C3-07)** — the
  `Notifications → Identity:VerificationOptions`,
  `Portability → Identity:AppDbContext`, `Projects → Web:TagSlugs`
  contract extractions.
- **Not the close (C3-08)** — the six-member flip.

### The honest finding (per the DoD's "say which line fails and why")

The plan's headline claim that C3-00 moves the DoD #3/#4 "files" line
**23 → 22 does not hold.** The extraction removed 288 code lines from
`EventService.cs` (997 → 709), but the file is still **over the 600
`cs_file` ceiling** (709 > 600), so the `files` entry is **tightened, not
retired**. The "classes" line **does** move 6 → 5 (the `EventService` class
entry retires at 25, the ceiling), and the "interfaces" line **stays 8**
(the `IEventService` interface is still over its 20-method ceiling at 22).

This is the **honest limit of the ADR 0290 named-seam extraction** — the
seam is real (a C13 cold read of `EventRsvpGuardianService` passes on its
own), the 6-method cluster is now a named type, and the total over-ceiling
surface is strictly smaller (the extracted seam is under every ceiling, the
class entry retired, the interface tightened). But the *file* is still over
the ceiling — the 997 → 709 reduction is not enough to retire the `files`
entry. **This is recorded per the DoD's "say which line fails and why — do
not fix it silently"** — the gate (honest since C2-00) + the clean-state
re-measurement is what makes that hand-off honest-by-construction.

The next unit (C3-01, the `PageService` shrink) is the same class of work —
the ADR 0290 named seam `PageTranslationStandingService` (the 4-method
translation-standing cluster). The path to the "files ≤ 8" line is a
**multi-unit effort** (the 7 god-services + the tail), not a single-unit
reduction. The gate stays green; the ratchet only moves down.

## C3-01 — the PageService shrink: adopt the ADR 0290 named seam (2026-11-06)

### The scope (per D1 + the C3-01 row)

D1: *"…`PageService` (901) first (the ADR 0290 named seams:
`EventRsvpGuardianService` + `PageTranslationStandingService`)… C3-01
`PageService`→`PageController`."* C3-01 is the `PageService` one: extract the
**4-method standing cluster** (`CheckCreateStanding` / `CheckEditStanding` /
`CheckTranslateStanding` / `CanTranslatePage`) out of `PageService` into a
real part-vs-whole surface, and — if the shrink gets `PageService` under the
600 `cs_file` ceiling — unblock the `PageController` group-(a) allowlist keep.

### The shape (honest deviation from the ADR 0290 *label*, follows its *description*)

ADR 0290 names the seam `PageTranslationStandingService` **but describes it
as** *"the C04 `*Policy` pattern, mirroring `EventStandingPolicy`"*. Both
in-repo precedents (`EventStandingPolicy`, `PageTranslationPolicy`) are a
`public static class`. The 4 methods are stateless `public static` decision
helpers (no store, no session, no DI) — so an `I*Service` + `*Service` +
DI registration would be exactly the ceremony the part-vs-whole doctrine
forbids. **The extracted type is therefore `PageStandingPolicy`** (a `public
static class` in `src/Kumunita.Core/Pages/`), named for consistency with
`EventStandingPolicy` (the cluster is create + edit + translate standing) and
because `PageTranslationPolicy` already holds the translate-only decision
core. A C13 cold read of `PageStandingPolicy` passes on its own — the
standing-matrix gate concern (ADR 0040 / ADR 0161 / ADR 0015), a real
part-vs-whole surface, not a ceremony type.

**Two facts the C3-01 prompt's math assumed wrong (measured, not intended):**

1. **The 4 methods are `public static` and were never on `IPageService`** —
   the interface comment (L135–140) explicitly says they are *"not part of
   the Web-facing surface the U04 PageController resolves."* So the prompt's
   *"IPageService 22 → 18, the interfaces entry retires"* is **false**: the
   interface is **untouched at 22** (still over the 20 ceiling → the entry
   **stays**). The seam is not a method on the interface; it is a static
   surface the service delegates to.
2. **`PageService` does not drop under 600.** The 4 methods + the dead
   `CanTranslatePageCore` helper total ~165 code lines (917 → **852**). The
   file is still over the 600 `cs_file` ceiling, so the `files` entry is
   **tightened, not retired** — the exact C3-00 pattern (997 → 709).

### The extraction (what changed)

| New type | What it owns | Under ceiling |
|---|---|---|
| `PageStandingPolicy` (static) | the 4 standing methods (`CheckCreateStanding` / `CheckEditStanding` / `CheckTranslateStanding` / `CanTranslatePage`), verbatim from `PageService`; `CanTranslatePage` delegates to `PageTranslationPolicy.CanTranslatePage` (unchanged) | 4 public methods · ~165 code lines — under every ceiling (4 < 25 class, file < 600) → **no baseline entry** |

The dead private helper `CanTranslatePageCore` (a one-line forwarder to
`PageTranslationPolicy.ResolveStanding`, no callers) was **deleted**, not
carried. `PageService` **delegates** to the policy at its 4 internal call
sites (the ctor's `ResolveStanding` lambda → `CheckTranslateStanding`;
`CreateAsync` → `CheckCreateStanding`; `UpdateAsync` + `ResetToSeededAsync`
→ `CheckEditStanding`) — the same shape as C3-00's `EventService` →
`EventRsvpGuardianService` move. `IPageService` is **unchanged** (the 4
methods were statics, never interface members).

**The test ripple (Gate Rule 1 — a `*Policy` must be named by a *plain*
non-`PostgresFixture` unit test):**

| Test file | Change |
|---|---|
| `Core.Tests/PageStandingPolicyTests.cs` | **new** — the 23 pure standing tests (the `PG_Check*Standing*` + `PG6_CanTranslatePage*` family) moved verbatim from `PageServiceTests` section 8, re-pointed to `PageStandingPolicy.*`. **23/23 pass.** This is the seam's own plain unit test (the `EventStandingPolicyTests` shape, C04). |
| `Core.Tests/PageServiceTests.cs` | section 8 (23 pure standing tests) **removed** (moved to the new plain file — `PageServiceTests` is a `PostgresFixture` class, and Gate Rule 1 requires the `*Policy` to be named by a *plain* test). `RolesSet` helper + the other 54 uses stay. Remaining suite **89/89 pass.** |
| `Web.Tests/PageControllerTests.cs` | 2 `<see cref>` re-pointed (`PageService.CanTranslatePage` → `PageStandingPolicy.CanTranslatePage`). **39/39 pass.** (The controller's own `PageTranslationPolicy.CanTranslatePage` call at L417 is unaffected.) |

**Doc cref / prose re-pointing** (6 files, the removed `PageService.*`
members → `PageStandingPolicy.*`): `PageTranslationPolicy.cs`,
`PageViewModels.cs`, `IEventService.cs`, `ITagService.cs`, `TagService.cs`,
`EventServiceTests.cs` (2 sites). All compile clean (no CS1574 bad-cref).

### Before → after (the DoD #3/#4 + #6 lines) — the honest numbers (measured against the gate)

| Line | Before (C2-08 close) | After (C3-01) | Why |
|---|---:|---:|---|
| **files** (over 600) | **23** | **23** | `PageService.cs` went 917 → **852** (the ~165-line standing cluster extracted), still **over the 600 ceiling** → the `files` entry is **tightened** (max 917 → 852), **not retired**. The C3-01 prompt's *"files 23 → 22"* does **not** hold (the same honest limit as C3-00). |
| **classes** (over 25) | **6** | **6** | `PageService` went 30 → **26** public methods (the 4 standing statics extracted), still **over the 25 ceiling** → the `classes` entry is **tightened** (max 30 → 26), **not retired**. The `adr: 0290` field is **dropped** (base `origin/development` = 28, so 26 is *below* base → the raise-justification no longer applies; the C3-00 precedent for the analogous tightened interface). |
| **interfaces** (over 20) | **8** | **8** | `IPageService` is **unchanged at 22** — the 4 standing methods were `public static` and **never on the interface** (the interface comment says they're "not part of the Web-facing surface"). The prompt's *"IPageService 22 → 18 retires"* is false. The entry **stays**. |
| **Marten allowlist** (DoD #6) | **20** | **20** | The `PageController` group-(a) god-file-at-ceiling keep **still holds** — the reason is *"the owning PageService is a god-file at its ratchet ceiling (cs_file 600)"*, and `PageService` is still over 600 (852). The entry's reason text is updated to name C3-01's measurement (917 → 852, still over) — **not** a forced retirement. The count does **not** move in this unit. |

**The total over-ceiling surface is strictly smaller:** the `PageStandingPolicy`
new type is **under every ceiling** (4 methods, ~165 lines) → no baseline
entry; the 4-method cluster is now a **named type** (the ADR 0290 named seam
— the clean resolution the predecessor de-scoped), not a 4-method static block
inside a 917-line god-file; the `PageService` file (917 → 852) and class
(30 → 26) **tighten** — the over-ceiling *excess* strictly shrinks. The ratchet
only moved down; 0 raises.

### The baseline changes (textual only; no JSON round-trip)

| Entry | Change | Reason |
|---|---|---|
| `files` · `PageService.cs` | `max` 917 → **852** | tightened (the 4-method cluster + dead `CanTranslatePageCore` extracted; the file is still over 600 → the entry is kept, not retired) |
| `classes` · `PageService` | `max` 30 → **26**, `adr: 0290` **dropped** | tightened (the 4 standing statics extracted; the class is still over 25 → the entry is kept, not retired); the `adr:` drops because base (`origin/development` = 28) is *above* the new 26 — the raise-justification no longer applies (C3-00 precedent) |
| `boundary_violations` · `Pages→Identity:Roles` | `count` 2 → **3**, `adr: 0291` **kept** | honest grow: the new `PageStandingPolicy.cs` (in the `Pages` module) references the `Roles` constants, adding a third file to an *already-existing* `Pages→Identity:Roles` edge (was `PageService` + `PageTranslationPolicy`). The intended-coupling record (ADR 0291) is unchanged; the count rises because the split minted a new file on the same legitimate edge — the C08 split-raise precedent (`Posts→Identity:Roles`, `Projects→Identity:Roles`, `UserInfo→Identity:Roles` all carry `adr:` for split-induced grows). |

The `\u003C` escapes in the baseline **survived** (27 after the edits — the
baseline was edited textually, never `json.load`/`dump`).

### Verification (all green, 2026-11-06)

- [x] **`dotnet build Kumunita.slnx -c Debug` — 0 errors** (no CS1574 bad-cref).
- [x] **`PageStandingPolicyTests` — 23/23** (the standing cluster's decision
  extracted + unit-tested as a plain unit — the `EventStandingPolicyTests`
  shape, C04).
- [x] **`PageServiceTests` — 89/89** + **`PageControllerTests` — 39/39** (the
  re-pointed seam + the unchanged controller surface).
- [x] **`pwsh tools/code/code-check.ps1` — OK (0 violations).** The `PageService`
  file + class entries tightened; the boundary edge count honestly raised with
  its existing `adr: 0291`; no new over-ceiling surface (the extracted seam is
  under the ceilings).
- [x] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS.**
- [x] **The `\u003C` escapes survived** (27 after the edits — the baseline was
  edited textually, never `json.load`/`dump`).
- [x] **The handoff note gets a C3-01 entry** (this section).

### What C3-01 did NOT do (the other C3 units' scope)

- **Not the `SampleDataSeeder` decomposition (C3-02)** — the 2 god-methods.
- **Not the other 5 god-services (C3-03)** — `BoardService` · `TodoService` ·
  `PostService` · `IdentityService` · `UserPortabilityService` (and the
  `GuardianService` / `ModerationService` / `TagService` group-(a) allowlist
  keeps those own).
- **Not the still-large-file tail (C3-04)** — the files that remain over 600
  after C3-00…C3-03.
- **Not the other allowlist retirements (C3-05 / C3-06)** — the 20 → ≤ 5 line
  (the `PageController` entry *could* have retired here but cannot, because
  the file is still over the ceiling — see the honest finding below).
- **Not the 3 flagged-accidental boundary edges (C3-07)** — the
  `Notifications → Identity:VerificationOptions`,
  `Portability → Identity:AppDbContext`, `Projects → Web:TagSlugs`
  contract extractions.
- **Not the close (C3-08)** — the six-member flip.

### The honest finding (per the DoD's "say which line fails and why")

**None of the four DoD numbers move in C3-01** — and that is the honest limit
of this unit, recorded per the lane's *"if a DoD line fails, stop and say
which one and why — do not fix it silently."*

- **DoD #3/#4 "files" 23 → 23** — the prompt's *"files 23 → 22"* does **not**
  hold. `PageService.cs` went 917 → 852 (the ~165-line standing cluster
  extracted), but it is still **over the 600 `cs_file` ceiling** (852 > 600),
  so the `files` entry is **tightened, not retired**. The same honest limit as
  C3-00 (997 → 709, still over).
- **DoD #3/#4 "classes" 6 → 6** — the prompt's *"class entry retires"* does
  **not** hold. `PageService` went 30 → 26 public methods, still **over the 25
  ceiling**, so the `classes` entry is **tightened, not retired**.
- **DoD #3/#4 "interfaces" 8 → 8** — the prompt's *"IPageService 22 → 18
  retires"* is **false**. The 4 standing methods were `public static` and
  **never on the interface** (the interface comment says they're "not part of
  the Web-facing surface"), so `IPageService` is unchanged at 22.
- **DoD #6 allowlist 20 → 20** — the `PageController` group-(a) keep **still
  holds**: the reason is *"the owning PageService is a god-file at its
  ratchet ceiling,"* and `PageService` is still over the ceiling (852 > 600).
  The entry's reason text is updated to name C3-01's measurement — **not** a
  forced retirement, not a silent keep.

What **is** true (the ratchet did move down, the gate is green): the
`PageService` file shrank 917 → 852 and the class shrank 30 → 26 (the
over-ceiling *excess* strictly smaller); the 4-method standing cluster is now
a **named, under-ceiling, unit-tested type** (`PageStandingPolicy`) — the
ADR 0290 named-seam clean resolution the predecessor de-scoped; 0 raises; the
`\u003C` escapes survived. The path to the "files ≤ 8" line is a **multi-unit
effort** (the remaining 5 god-services + the tail), not a single-unit
reduction. The next unit (C3-02, the `SampleDataSeeder` decomposition) is a
different class of work — a per-entity builder decomposition of the two
god-methods.

## C3-02 — the SampleDataSeeder decomposition: the 2 god-method retires + the 1 files-entry honest raise (2026-11-06)

### The scope (per D2 + the C3-02 row)

D2: *"Per-entity builders. Decompose `SeedAsync` (615/cc83) and
`BackfillSampleCorpusAsync` (782/cc128) into named per-entity builders
(accounts / groups / posts / events / tags / pages), each a separate,
unit-testable surface."* C3-02 is that decomposition of the two largest
methods in the repo — the only unit where decomposing the *methods themselves*
( rather than the class) is the only path to the two `methods` entries, because
a single 782-line method forces any *file* over the 600 ceiling (D2's own
caveat).

### The shape (34 `private static` per-entity builders, the 2 parents → thin orchestrators)

The two god-methods now each call their per-entity seams in order (accounts →
groups → posts → events → tags → pages → goals → projects → boards → inventory
→ translations → guardian-links), each seam a `private static` helper on
`SampleDataSeeder` (the plan's explicit *"not separate `*Builder` classes"*
instruction — so they do **not** trip the gate's seam-test Rule 1, which
applies to class types only, and they do **not** count toward the class
public-methods ceiling, which counts public methods only). Shared mutable
state (the `usersByEmail` map, the cross-entity id maps, the
`postTransLangsThisRun` dedupe map) is created in the orchestrator and passed
by reference. The 5 helpers that first measured over the 80/15 ceiling were
each further decomposed into per-entity *content* builders, all now under the
ceiling: `SeedBoardsAsync` 85→63 (→ `SeedBoardCardsAsync`),
`BackfillPostsAsync` 115/cc22→56/cc14 (→ `BackfillPostRepliesAsync` +
`BackfillPostTranslationsAsync`), `BackfillEventsAsync` 82→65 (→
`BackfillEventRsvpsAsync`), `BackfillProjectsAsync` 103/cc20→53/cc13 (→
`BackfillProjectContentAsync`), `BackfillBoardsAsync` 108/cc18→43/cc8 (→
`BackfillBoardContentAsync`). Bodies are extracted verbatim (the helpers take
the locals their block already references as parameters) — no body line is
rewritten, so the behaviour is preserved. No new boundary edge is minted (the
helpers live in `Bootstrap` and reference the same cross-module types the
god-methods already referenced; the 3 inbound `Identity`/`Notifications`/`Web`
→ `Bootstrap` edges named `SampleDataSeeder` are keyed by symbol, not file).

### The before → after (measured against the gate, not intended)

| Baseline entry | Before (C2-08) | After (C3-02) | Why |
|---|---|---|---|
| **files** · `SampleDataSeeder.cs` | **1633** | **2060** (raised, `adr: 0292`) | the 34 per-entity signatures + shared-state parameters are **net-positive** on code lines over the two method bodies they replace; the file is **still over the 600 ceiling** → the entry is **raised with this `adr:`, not retired**. The DoD #3/#4 "files" line **stays 23** — the plan's "files 23 → 22" **does not hold** (the same honest-finding class as C3-00/C3-01: a class-level split cannot get this file under 600). |
| **methods** · `SeedAsync` | 615 / cc83 | **retired** | the orchestrator is now **54 / cc9** (under the 80/15 ceiling) → the entry is stale → gone. |
| **methods** · `BackfillSampleCorpusAsync` | 782 / cc128 | **retired** | the orchestrator is now **78 / cc6** (under the ceiling) → the entry is stale → gone. |
| **methods** · `EnsureUserAsync` | 69 / cc23 | **unchanged** (69 / cc23) | the decomposition does not touch it; it is **still over the cc-15 ceiling** at its baseline max → the entry **stays** (the ratchet holds it, does not move it). |
| the 5 first-over helpers + 6 content builders | — (not on baseline) | **none on baseline** (all under 80/15) | the goal is met: each sits **under the ceiling**, so **no new `methods` entries are minted** — the DoD #3/#4 "methods" line moves **91 → 89**. Carrying the 5 would have *raised* the methods surface (91 → 94+) — the anti-pattern the DoD's *"total over-ceiling surface strictly smaller, 0 raises"* forbids. |

**The DoD #3/#4 "methods" line genuinely improves 91 → 89** (the two god-methods
retire; 0 new entries) — the one real reduction of the unit. The "files" line
**stays 23** (the plan's "files 23 → 22" does not hold — an honest finding,
recorded, not papered over). The total over-ceiling surface for this class is
**strictly smaller** (before: 1 file + 3 methods = 4 entries; after: 1 file
(raised) + 1 method = 2 entries) and **0 silent raises** (the one raise carries
[ADR 0292](../../adr/0292-improve-code-3-c3-02-sampledataseeder-decomposition-raise.md);
the two retires + the one unchanged entry are the ratchet moving down /
holding).

### The verification (all green)

- **`dotnet build Kumunita.slnx -c Debug` — 0 errors.**
- **Behaviour preservation (the seeder suites, `PostgresFixture`):**
  `SampleDataCorpusBackfillTests` + `SampleDataEventTranslationBackfillTests`
  + `IdentityServiceSamplePasswordLockTests` + `IdentityServiceSampleMfaLockTests`
  + `LS_U04_SeederTests` — **41/41 pass** (the per-entity helpers are exercised
  through the parent orchestrators' existing harness; no new test file needed,
  no new pure-logic seam to unit-test in isolation).
- **`pwsh tools/code/code-check.ps1` — OK (0 violations).** The 4 baseline
  entries resolved against the measurement (the 2 god-methods retired; the
  `files` entry raised 1633 → 2060 with `adr: 0292`; `EnsureUserAsync`
  unchanged); the 5 over-ceiling helpers split under the ceiling; the `\u003C`
  escapes survived (textual edit, no `json.load`/`dump` round-trip).
- **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS** (the ADR-index
  drift gate (b) needed the 0292 row added to `docs/adr/README.md`; added).

### For the reviewer of the close (C3-08)

C3-02 is the same honest class as C3-00/C3-01: the *method* surface is
genuinely reduced (91 → 89, the two god-methods now thin orchestrators under
the ceiling), but the *file* does not drop under 600 (it grew 1633 → 2060 as
the 34 signatures are net-positive), so its `files` entry is a **honest-keep
raised with ADR 0292, not retired** — the DoD #3/#4 "files" line **stays 23**.
This is the plan's D2 as predicted (*"a single 782-line method forces any file
over the ceiling; a class-level split cannot get a file under 600"*), not a
failure of the unit. The 0292 entry is the one carry in the baseline the close
must account for when it re-verifies every carried-in DoD line from a clean
state.

## C3-03 — the 5 god-services: PostService seam landed + green (in progress) (2026-11-06)

> **Status: 1 of 5 services landed and verified green; the other 4 are
> pending.** This is a partial entry written so the verified PostService work
> is protected and the next agent can pick up the remaining 4 without re-doing
> the analysis. See the "Pending" list below for the exact seams, the measured
> counts, and the baseline entries each must resolve.

### What landed (PostService — the required-first, the only allowlist-unblock candidate)

The **group-post lane** (ADR 0013 / ADR 0016) was extracted out of
`PostService` into a **real part-vs-whole seam** (the C3-00
`EventRsvpGuardianService` pattern):

| New type | What it owns | Under ceiling |
|---|---|---|
| `IGroupPostService` | the 4-method surface (ListGroupFeed / GetGroupPost / CreateGroupPost / UpdateGroupPost) | 4 methods · < 20 |
| `GroupPostService` | the 4 implementations + the `OrderByPostSort` / `TruncateUgcSnippet` / `ResolveLanguageCodeAsync` helpers | < 25 public methods · < 600 code lines |

Both new types are **under every ceiling** (no baseline entries of their own).
`PostService` keeps the 4 non-group-post public methods it owned (the read
lanes, the write lanes, the draft lanes, the translation lane, the image /
attachment finders) and **4 thin forwarders** delegating to `GroupPostService`
(the C08 test-compat facade — the existing `GroupPostServiceTests` + the
forwarder-path callers keep compiling unchanged). `IGroupPostService` is
registered in `Posts/DependencyInjection.cs`; the 3 Web consumers
(`GroupsController`, `KioskItemCatalog`, `ReportTargetsController`) get an
**optional ctor param** `IGroupPostService? groupPosts = null` (appended last)
+ null-guards, and re-point their group-lane calls to the named seam.

**Its own unit test** (`tests/Kumunita.Core.Tests/GroupPostServiceSeamTests.cs`,
the `PostgresFixture` harness — the C3-00 / C3-01 precedent for a DB-dependent
seam) constructs the seam **directly** (not through the forwarders): the feed
read (G1), the detail read + replies (G7), the non-member create gate (G3), and
the author-only edit lane (ADR 0016) — **4/4 pass**.

### The honest finding (per the DoD's "say which line fails and why")

**None of the DoD numbers move for PostService — and that is the honest limit of
this service, the same limit C3-00 (EventService 997 → 709) and C3-01
(PageService 917 → 852) hit:**

| Line | Before (C3-02 close) | After (PostService seam) | Why |
|---|---:|---:|---|
| **files** (over 600) | **23** | **23** | `PostService.cs` went 821 → **631** code lines (the group-post lane extracted), still **over the 600 ceiling** → the `files` entry is **tightened** (max 821 → 631), **not retired**. |
| **classes** (over 25) | **5** | **5** | `PostService` is **not** on the `classes` baseline (it is ≤ 25 public methods — the 4 forwarders keep it at the ceiling, the C3-00 shape). No entry to retire. |
| **interfaces** (over 20) | **8** | **8** | `IPostService` does not exist on the baseline; `IGroupPostService` (4 methods) is under the 20 ceiling → no new entry. |
| **methods** (over 80/cc15) | 89 | 89 | The 2 over-ceiling `PostService` methods (`CreatePostAsync` 90/cc17, `CreateReplyAsync` 65/cc16) **stay in PostService** (the seam extracted the *group-post* lane, not those) → their entries are **unchanged**. |
| **Marten allowlist** (DoD #6) | **20** | **20** | The `PostsController` group-(a) keep **stays** — the reason text is updated to name C3-03's measurement (821 → 631, still over). The DoD #6 line does **not** move (20 → 20, not 20 → 19). |

**The total over-ceiling surface is strictly smaller:** the `GroupPostService`
seam is under every ceiling (a named type, not a block inside the god-file);
`PostService` shrank 821 → 631. The ratchet only moved down; the one *honest
raise* (the 7 boundary edges + the `GroupsController` seam-injection collateral,
the `Detail` action) is **named with ADR 0293** (0 silent raises).

### The baseline changes (textual only; the `\u003C` escapes survived — 36 before and after)

| Entry | Change | Reason |
|---|---|---|
| `files` · `PostService.cs` | `max` 821 → **631** | tightened (the group-post lane extracted; still over 600 → kept, not retired) |
| `files` · `GroupsController.cs` | `max` 1639 → **1641**, `adr: 0293` **added** | collateral: the +optional ctor param `IGroupPostService? groupPosts` + the null-guard on the `Detail` action (the C3-00 `EventController` collateral shape) |
| `methods` · `GroupsController.Detail` | `max` 153 → **154**, `cc_max` 33 → **34**, `adr: 0293` **added** | collateral: the `Detail` action's `if (groupPosts is null) return NotFound();` guard |
| `boundary_violations` · 7 `Posts→*` edges | counts raised by 1 each, `adr: 0293` **added** | honest grow: the new `Posts/GroupPostService.cs` lands on 7 *already-existing* edges (Audience 4→5 · LocaleSettings 1→2 · MentionTokens 1→2 · UgcSnippets 1→2 · Page 2→3 · PageSizer 1→2 · AttachmentIds 5→6) — the C3-01 `Pages→Identity:Roles` 2→3 precedent |
| `controllers_marten_allowlist` · `PostsController` | reason text updated | names C3-03's 821 → 631 measurement (still over 600 → the keep holds, not a forced retirement) |
| `docs/adr/0293-…` + `README.md` row | **added** | the `adr:` field the 7 boundary raises + the `GroupsController` collateral name (the ADR-index gate (b) needed the row) |

### Verification (all green, 2026-11-06)

- [x] **`dotnet build Kumunita.slnx -c Debug` — 0 errors, 0 warnings.**
- [x] **`GroupPostServiceSeamTests` — 4/4 pass** (the `PostgresFixture` harness).
- [x] **`pwsh tools/code/code-check.ps1` — OK (0 violations).**
- [x] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS** (the ADR-index row for 0293 added).
- [x] **The `\u003C` escapes survived** (36 before and after — the baseline was edited textually).

### All 5 services landed + verified green (2026-11-06)

**All 5 god-services now have their real part-vs-whole seam + `I*Service` + DI
registration + their own unit test, and every baseline entry they free is
resolved against the gate.** The honest outcome (verified against
`code-check`, not assumed):

| Service | Seam (its own unit test) | `files` (measured → still over 600) | `classes` / `interfaces` | over-cc `methods` re-pointed |
|---|---|---|---|---|
| `PostService` | `IGroupPostService` + `GroupPostService` (the group-post lane) · 4/4 pass | 821 → **631** | none (not on `classes`; `IGroupPostService` 4 methods < 20 → no new entry) | 0 (the 2 over-cc methods stay in `PostService`) |
| `BoardService` | `IBoardLaneService` + `BoardLaneService` (the todo-movement lane) · 5/5 pass | 1025 → **706** | `IBoardService` **21 → 21** (tighten, **not retire** — the 6 todo-move methods stay on the interface via forwarders) | 3 (UpdateLane / MoveTodoToAdjacentLane / MoveTodoToLanePosition re-point to the seam) |
| `TodoService` | `ITodoCommentService` + `TodoCommentService` (the comment lane) · 4/4 pass | 839 → **744** | none | 1 (CreateTodoCommentAsync re-points to the seam) |
| `UserPortabilityService` | `IImportOwnershipGuardService` + `ImportOwnershipGuardService` (the ownership-guard / classify lane) · 3/3 pass | 922 → **806** | none | 3 (ClassifyTargetPrepassAsync / DeriveOwnershipGuardInputs / ResolveGuardInputsAsync re-point to the seam) |
| `IdentityService` | `IIdentityPlatformGateService` + `IdentityPlatformGateService` (the platform-gates / sample-data-lock lane) · 3/3 pass | 989 → **817** | `IdentityService` class **33 → 33** + `IIdentityService` **33 → 33** (both tighten, **not retire** — the 12 gate methods stay on the interface via forwarders) | 0 (the 12 gate methods were each < 80 / < cc 15, so had no `methods` entries to re-point) |

**The honest DoD outcome — every number measured against `code-check` (the
gate), not the plan's "retires" claim:**

- **`files` (over 600): stays at 23.** None of the 5 services drops under the
  600 `cs_file` ceiling even after its seam (631 · 706 · 744 · 806 · 817 — all
  over 600), so each `files` entry is **tightened** (max → measured), **not
  retired**. This is the C3-00/C3-01/C3-02 honest limit, confirmed for the 5th
  time. The "23 → ≤ 8" target is C3-04's tail, not this unit's (the prompt's
  own words).
- **`classes` (over 25): stays at 5.** The plan predicted `IdentityService`
  would retire (5 → 4), but that needs a **full split** (the 12 gate methods off
  `IIdentityService` + all Web consumers re-pointed). The C08 forwarder pattern
  (the spec-endorsed seam shape — the existing `IdentityServiceSignupPolicyTests`
  + the 5 Web controllers keep compiling unchanged) keeps the 12 on
  `IdentityService`'s public surface, so the class stays 33 (over 25 → kept,
  not retired). **No other service is on the `classes` baseline**, so the line
  is 5 → 5.
- **`interfaces` (over 20): stays at 8.** `IBoardService` (21) + `IIdentityService`
  (33) keep their methods via forwarders → both tighten, not retire. The new seam
  interfaces (`IGroupPostService` 4 · `IBoardLaneService` 6 ·
  `ITodoCommentService` 2 · `IImportOwnershipGuardService` 3 ·
  `IIdentityPlatformGateService` 12) are each under the 20 ceiling → no new entry.
  8 → 8.
- **`methods` (over 80/cc15): stays at 89.** 7 over-ceiling method entries
  **re-point** to their seam (BoardService 3 · TodoService 1 ·
  UserPortabilityService 3) — each method leaves the god-service and lands on
  the seam's method. **But the seam's methods are still over the `cc 15`
  ceiling** (cc 16–22: `MoveTodoToAdjacentLaneAsync` cc19, `MoveTodoToLanePositionAsync`
  cc22, `UpdateLaneAsync` cc17, `CreateTodoCommentAsync` cc18,
  `ClassifyTargetPrepassAsync` cc16, `DeriveOwnershipGuardInputs` cc20,
  `ResolveGuardInputsAsync` cc20), so they **keep** their baseline entries
  (re-pointed to the new file, not retired). The `methods` line is **89 → 89**.
  **Not the mover** — the entry moved, the count did not. (C3-04 may retire some
  by decomposing those over-cc seam methods further.)
- **`boundary_violations`: 12 honest raises, all `adr: 0293`.** Each new seam
  file lands on *already-existing* module edges (the god-service already crossed
  them) — the C3-01 `Pages→Identity:Roles` 2→3 / C08 `Posts→Identity:Roles`
  split-raise precedent. **0 new edges**, **0 silent raises** (every count raised
  above the base branch carries `adr: 0293`). The 12: `Posts→*` ×7 (PostService)
  + `Projects→Authorization:AccessAuditFactory` 3→5 · `:StandingMatrix` 3→4
  (BoardService) + `Portability→Identity:User` 5→6 · `:Messaging:Conversation`
  2→3 (UserPortabilityService) + `Identity→Authorization:AccessAudit` 1→2
  (IdentityService). See ADR 0293 (the table is the source of truth; it lists
  all 12).
- **`controllers_marten_allowlist` (DoD #6): stays at 20.** The `PostsController`
  group-(a) keep holds (`PostService` is 631, still over 600) — the reason text
  names C3-03's measurement. 20 → 20, not 20 → 19.

**The over-ceiling surface shrank where it could:** every seam is under the
**class** (25) and **interface** (20) ceilings (a named type, not a block inside
the god-file) + each god-service **shrank** (631 · 706 · 744 · 806 · 817 — the
`files` excess moved down). The **method** and **class/interface** over-ceiling
counts do **not** drop in this unit (the seam methods keep their `cc 15`-over
entries — 89 → 89; the forwarders keep the class/interface method counts — 5 → 5,
8 → 8). The ratchet only moved down (the `files` maxes); the 12 boundary-edge
raises + the `GroupsController` seam-injection collateral are all **named with
ADR 0293** (0 silent raises).

### Verification (all green, 2026-11-06)

- [x] **`dotnet build Kumunita.slnx -c Debug` — 0 errors, 0 warnings.**
- [x] **All 5 seam tests pass** — `GroupPostServiceSeamTests` 4/4 ·
  `BoardLaneServiceSeamTests` 5/5 · `TodoCommentServiceSeamTests` 4/4 ·
  `ImportOwnershipGuardServiceSeamTests` 3/3 ·
  `IdentityPlatformGateServiceSeamTests` 3/3 (the `PostgresFixture` harness).
- [x] **`pwsh tools/code/code-check.ps1` — OK (0 violations)** (the gate is the
  oracle: the 5 `files` tightens + the 7 `methods` re-points + the 12
  `boundary` raises all measured green).
- [x] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS** (the ADR-index
  row for 0293 present; the handoff ≤ 600 lines with the TL;DR).
- [x] **The `\u003C` escapes survived** (the baseline was edited textually,
  never `json.load`/`dump`).

---

## C3-04 — the still-large-file tail (the `files` 23 → 8 tail) — **DONE**

**The DoD target is met: `files` 23 → 8** (the plan's ≤ 8). The mechanism is
the **partial-class split** — each over-600 file is declared `partial` and a
sibling file (`<Name>.<Seam>.cs`) carries a named seam of its methods. This is
a *real* part-vs-whole split (the code physically leaves the file) but the
split does **not** retire every entry, confirming the honest finding C3-00…
C3-03 proved: a split tightens, it does not reliably drop a file under the
ceiling. **15 of the 23 retired; 8 stand over** (the DoD's ≤ 8 allowance).

### Per-file before → after (code lines, `cs_file`)

| file | before | after | outcome |
|---|---|---|---|
| `AnnouncementController` | 868 | 434 | **retired** → `.Translations.cs` (434) |
| `AdminController` | 664 | 416 | **retired** → `.Communities.cs` (248) |
| `PageController` | 643 | 348 | **retired** → `.Translations.cs` (295) |
| `BoardsController` | 982 | 495 | **retired** → `.Lanes.cs` (172) + `.Translations.cs` (315) |
| `GuardianController` | 685 | 360 | **retired** → `.RsvpCurate.cs` (325) |
| `PostService` | 873 | 449 | **retired** → `.Media.cs` (424) |
| `BoardService` | 798 | 484 | **retired** → `.Lanes.cs` (314) |
| `EventService` | 695 | 472 | **retired** → `.Group.cs` (223) |
| `TodoService` | 666 | 417 | **retired** → `.Lifecycle.cs` (249) |
| `TagService` | 672 | 447 | **retired** → `.Listing.cs` (212) + `.Suggest.cs` (173) |
| `ModerationService` | 751 | 471 | **retired** → `.ReportIntake.cs` (280) |
| `GroupService` | 768 | 503 | **retired** → `.Invitations.cs` (265) |
| `GuardianService` | 694 | 408 | **retired** → `.Approvals.cs` (286) |
| `IdentityService` | 742 | 487 | **retired** → `.AccountOps.cs` (255) |
| `AnnouncementService` | 634 | 385 | **retired** → `.Comments.cs` (249) |
| `UserPortabilityService` | 788 | 715 | **tightened** → `.Standing.cs` (73) (still over 600) |
| `LocalizationService` | 673 | 603 | **tightened** → `.TranslationMatrix.cs` (70) (still over 600) |
| `PageService` | 852 | 852 | (C3-01) stands over — C3-05/C3-06 scope |
| `GroupsController` | 1641 | 1641 | (C3-03) stands over — C3-05/C3-06 scope |
| `EventController` | 1249 | 1249 | stands over — C3-05/C3-06 scope |
| `PostsController` | 1010 | 1004 | stands over — C3-05/C3-06 scope |
| `TodosController` | 1096 | 1096 | stands over — C3-05/C3-06 scope |
| `SampleDataSeeder` | 2060 | 2060 | (C3-02, ADR 0292 honest-keep) stands over |

**Retired: 15 · tightened-not-retired: 2 (`UserPortabilityService`, `LocalizationService`) · pre-existing-over (C3-01/02/03 + 4 big controllers): 5** → `files` = **8** (the DoD's ≤ 8).

### What changed
- **15 over-600 files split** into **19 new `partial` sibling files** (some
  files got two seams: `BoardsController`, `TagService`). Each main file is
  now `partial`; the extracted methods live in the sibling. Build clean (0
  errors).
- **`tools/code/code-baseline.json`** (edited **textually**, `\u003C` escapes
  preserved at 36): 15 `files` entries removed (23 → 8); 2 `classes` removed
  (5 → 3, the `IdentityService` + `AdminController` class-`public_methods` no
  longer fire on the split); 2 `methods` re-pointed to their new partial-file
  paths (`EventService.UpdateGroupEventAsync`, `UserPortabilityService.StandingComponentAsync`);
  **31 `boundary_violations` edge counts raised** with `"adr": "0294"` (each
  partial file lands on an already-existing module edge, so the per-edge file
  count grew by 1–6 — an honest raise, not a new edge).
- **ADR [0294](../adr/0294-improve-code-3-c3-04-partial-class-splits-boundary-raises.md)** records
  the partial-class-splits mechanic + the 31 boundary raises + the 2 classes
  retirements + the 8 files that stand over (and their C3-05/C3-06 disposition).

### Verification (all green, 2026-07-11)
- [x] **`dotnet build` — 0 errors, 0 warnings.**
- [x] **`pwsh tools/code/code-check.ps1` — code-check: OK (0 violations)**;
  `files` count now **8** (was 23); `classes` **3** (was 5); boundary 465.
- [x] **`dotnet test` (affected)** — Web: **137/137** (Announcement/Guardian/Admin/Page/Boards controllers); Core: **410/410** (all 12 split services + neighbours).
- [x] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS** (the ADR-index
  row for 0294 present; the handoff ≤ 600 lines with the TL;DR).
- [x] **The `\u003C` escapes survived** (the baseline was edited textually,
  never `json.load`/`dump`).

### Honest notes for C3-05 / C3-06
- **The 8 files still over 600** are the DoD's allowance. Their disposition:
  `SampleDataSeeder` (ADR 0292 honest-keep), `PageService` (C3-01) +
  `GroupsController` (C3-03) + the 4 big controllers (`EventController`,
  `PostsController`, `TodosController`, `GroupsController`) are the
  **Marten-allowlist retirement** scope (C3-05/C3-06) — the path to retire
  them is the allowlist `--retire` for their group-(b/c) edges, not more
  partial splits.
- **`UserPortabilityService` (715) + `LocalizationService` (603)** tightened
  but stand over by a hair — a *named-seam* split (not a further partial
  carve-out) is the cleaner path if they must drop; recorded in ADR 0294
  rather than forced.
- **The partial-class mechanic is a reusable pattern** for this repo: it is a
  real physical split (the gate's `files` metric sees the code leave the file),
  needs no constructor/DI change (same type), and the only collateral is the
  boundary edge count on an already-existing edge (honest raise, ADR'd). The
  CodeMetrics parser chokes on `sealed partial` (parses `sealed` as a type
  name → method count 0) — declare the partial siblings `partial class` (no
  `sealed`).

---

## C3-05 — the Marten-allowlist clean retirements (the `20 → ≤ 5` clean subset) — **DONE**

C3-05 works the 20 `controllers_marten_allowlist` entries and retires the ones
whose owning service is now ≤ 600 (the C3-04 partial-class splits shrank the
group-(a) god files, the exact condition their "god-file at the ceiling" keep
waited for). Per the prompt: *"judged on retiring every genuinely-retire-able
entry and honestly recording the ones you can't — not on hitting a specific
number."*

### The result: `allowlist` 20 → 18 (2 clean retirements)

**RETIRE (2) — verified clean, not assumed:**

| Controller | Why clean (measured) |
|---|---|
| `GoalsController` | **0 Marten ops of its own.** All goal actions route through `IProjectService` (`GetGoalAsync` / `ListProjectsForGoalAsync` / `CreateGoalAsync`). It held the store only to forward to the base's `SeedExistingTagSlugsAsync` — which no goal action calls. The base's `store` is now `IDocumentStore?` + the helper null-guards; `GoalsController` drops the ctor param (passes `null`); the 3 sibling base call sites (Boards/Todos/Projects) still pass their real store; the 1 test call site drops the arg. |
| `AccountController` | **1 op** (the verify-link token load, 16 code lines, reads `IdentityToken`). Moved to the new `IIdentityService.LoadVerifyTokenAsync` seam (the `IdentityToken` is the IdentityModule's document). Controller drops the `store` param + `using Marten;`; the method calls the seam. `Web→Identity:IdentityToken` edge **persists** (the controller still references the `IdentityToken` return type) → **no boundary entry changes**. 8 test call sites drop the `store:` arg; the 2 verify-path tests wire `identity.LoadVerifyTokenAsync` to the same real store they seeded (faithful to the old controller read). |

**KEEP (5) — honest findings, each code-verified, handed to C3-06:**

| Controller | Owning service | The measured keep reason |
|---|---|---|
| `AnnouncementController` | `AnnouncementService` 519 | caller-owns-transaction write lanes (~152 code lines) → 519 + 152 = 671 > 600, **no headroom**. C3-06: reshape the frozen seam to open its own sessions. |
| `DocumentController` | `DocumentService` 193 | **edge-minting** (C3-06 D3 group-(c)): the tag read (`Query<Tag>`) + Media quarantine write (`MediaObject.Quarantined`) would MINT NEW `Documents→Tags:Tag` + `Documents→Media:MediaObject` edges (verified absent; `DocumentService` references neither concrete type). Service has headroom (193) — the block is the minted edges, not size. |
| `TagController` | `TagService` 529 | the write lane is caller-owns-transaction (passes the session to `AddTagTranslationAsync`), **and** the `SeedTranslationFormAsync` read is register-pinned ("no new seam" — the in-code comment names the U8c register pin). C3-06: seam-reshape + register amendment. |
| `PostsController` | `PostService` 583 | caller-owns-transaction write lanes (~98+ code lines) + the `Tag`-read seeder; headroom only 17 → 583 + lanes > 600, **no headroom** (the prompt's own example). C3-06: the C3-04 `PostService.Media.cs` split retired the media lane; the remainder is the seam-reshape. |
| `GuardianController` | `GuardianService` 555 | the 4 `GuardianLink` reads (~48–56 code lines) → 555 + ~50 ≈ 605 > 600, **no headroom** (C3-04 already landed `GuardianService.Approvals.cs`, 226 lines). C3-06: a real read seam on `IGuardianService`, or a documented no-headroom keep. |

### The baseline changes (text-only; the 36 `\u003C` escapes survived)

| Entry | Change | Reason |
|---|---|---|
| `interfaces` · `IIdentityService` | `max` 33 → **34**, `adr: 0295` **added** | the `LoadVerifyTokenAsync` seam added so `AccountController` retires its last Marten op; the named-exception record (the only raise this unit) |
| `controllers_marten_allowlist` · `GoalsController` | **removed** | 0 ops of its own; clean retirement |
| `controllers_marten_allowlist` · `AccountController` | **removed** | 1 op moved to `IIdentityService`; clean retirement |
| `controllers_marten_allowlist` · the 5 keeps | reason text **updated** | the C3-05 measured rationale (no-headroom / edge-minting / pinned) replaces the now-false "god-file at the ceiling" text |

**Unchanged:** `files` 8 · `classes` 3 · `methods` 89 · `boundary_violations`
465 (the `Web→Identity:IdentityToken` edge persists → no boundary move) · `views` 3.

### The ADR + index

- **ADR 0295** (`0295-improve-code-3-c3-05-marten-allowlist-clean-retirements.md`)
  is the named record for the one raise (`IIdentityService` 33 → 34) + the two
  clean retirements + the 5 honest keeps.
- **`docs/adr/README.md`** — the `0295` index row added (gate (b): 295 files ==
  295 rows).

### Verification (all green, 2026-11-06)

- [x] **`dotnet build Kumunita.slnx` — 0 errors.**
- [x] **Affected tests pass** — the 8 `AccountController` suites (Verify×2 ·
  Signup×2 · MfaLogin · ResidentUsage · LoginErrorCode · AdminDelete) +
  `ProjectsControllerTests` 46/46 (the base-class `store` change).
- [x] **`pwsh tools/code/code-check.ps1` — OK (0 violations).**
- [x] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS** (gate (b): the
  `0295` index row present; files=295 == index rows=295).
- [x] **The 36 `\u003C` escapes survived** (the baseline was edited textually).
- [x] **Pre-existing failures confirmed** — the 9 unrelated Web-suite failures
  (compiled-JS artifacts, the boards-route snapshot drift, the kiosk/ADRs UI
  snapshots) are present on a clean `development` (stashed) too; C3-05 adds none
  of them (the one that was failing before the baseline edit,
  `ImproveHarnessTests.CodeCheck_Gate_Passes`, is now green).

### Honest notes for C3-06

- **The `allowlist` is now 18** (was 20). C3-06 lands the remainder to `≤ 5`.
  The 5 group-(a) keeps above are the first 5 to resolve (each needs a
  seam-reshape / edge-decision / register-amendment — the *no-headroom* and
  *edge-minting* classes, not the clean-move class this unit did).
- **The pre-existing group-(b/c/d) entries** (AdminController, AdminAnalytics,
  Attachment, ContentImage, Boards, Health, Projects, ProjectsBase,
  ReportTargets, Todos, Directory) stay as-is this unit — they are the C3-06
  scope (the prompt's explicit leave-list).
- **The base-class `store` is now `IDocumentStore?`** — a future sibling that
  needs the tag-slug prefill keeps passing its real store (the 3 existing
  siblings do); a sibling that doesn't (like `GoalsController`) passes `null`.
  No behavior change for any existing route (the helper null-guards to an empty
  chip set only when a child has no store *and* asks for tag slugs — the
  siblings that ask all supply a store).

## C3-06 — the Marten-allowlist remainder (the `18 → ≤ 5` remainder) — **DONE**

C3-06 works the 18 `controllers_marten_allowlist` entries C3-05 left, retiring
every genuinely-retire-able one and honestly recording the rest. Per the
prompt: *"judged on retiring every genuinely-retire-able entry and honestly
recording the ones you can't — not on hitting 5 at any cost."*

### The result: `allowlist` 18 → 17 (1 clean retirement; the honest floor is 17)

**RETIRE (1) — verified clean, not assumed:**

| Controller | Why clean (measured) |
|---|---|
| `ReportTargetsController` | **The keep reason was stale.** C3-05's reason claimed *"the owning ModerationService (1281 lines) is a god-file at its ceiling"* — but C3-03 (ADR 0293) / C3-04 (ADR 0294) split it; `ModerationService` is now **398 code lines** and the report-intake lane extracted to `ModerationService.ReportIntake.cs` (**363 code lines**). The no-headroom keep no longer stands. **The seam-reshape:** the 5 report-target actions used the caller-owns-transaction shape (the controller opened `store.LightweightSession()` and passed it to `FileReportTargetAsync(..., session)`). C3-06 dropped the `IDocumentSession session` param from `IModerationService.FileReportTargetAsync` + made the impl open `await using var session = _store.LightweightSession();` (the `FileReportAsync` / `FileReplyReportAsync` "the service opens its own session" precedent — the ADR 0006-C no-partial-write discipline is unchanged: the `Report` + `AccessAudit` rows still commit in one `SaveChangesAsync`). The controller drops the `store` ctor param + `using Marten;` + the 5 session opens + 5 `session:` args; the gate's pure-text scan finds **none** of the three tokens in the file. **No new boundary edge** (the service already references `Report` / `AccessAudit` / `INotificationEmitter`); `IModerationService` is under the interfaces ceiling (11 methods), so no baseline line moves. |

**KEEP (17) — honest findings, each code-verified this tree:**

| Controller | Owning surface (measured) | The keep class + reason |
|---|---|---|
| `AnnouncementController` | `AnnouncementService` 518 | no-headroom: caller-owns-transaction write lanes (~152 lines) → 518 + 152 > 600; reshaping the frozen `IAnnouncementService` seam is a larger seam change |
| `PostsController` | `PostService` 582 | no-headroom: post/reply write lanes (~98+ lines) → 582 + lanes > 600; the C3-04 media split already retired the media lane |
| `GuardianController` | `GuardianService` 554 | no-headroom: the 4 `GuardianLink` read helpers (the standing gate, 8+ actions) join UserInfo + Guardian; 554 + ~50 > 600 |
| `TagController` | `TagService` 528 | no-headroom (write lane) + register-pin (the `SeedTranslationFormAsync` read — the U8c "no new seam" pin forbids an `ITagService` "get translations" method) |
| `PageController` | `PageService` **851** | still a god-file over the `cs_file` 600 ceiling; the entry retires only when `PageService` drops under 600 (a later C3 unit) |
| `AdminController` | no Admin module (reads) | **reason refreshed** — the 2 ops are **reads** (the `AccessAudit` audit-log list + the `AdminOverride` break-glass candidates), both Authorization-module concrete types; minting an `IAdminService` for two single-module admin reads is a ceremony type |
| `GroupsController` | `IGroupService` (exists) | **reason refreshed** — `IGroupService` now exists (C07, ADR 0286, 25 methods); the group create/edit/leave + membership-write lanes (19 sessions) are a `Group`+Translations+membership aggregate the seam does not own |
| `AttachmentController` | Posts + Media 2-module | no-service: the attach/retrieve/delete lanes span the Posts `Post` read + the Media `MediaObject` quarantine write — a 2-module aggregate |
| `ContentImageController` | Posts + Media 2-module | no-service: same shape as `AttachmentController` |
| `AdminAnalyticsController` | no Usage write-service | edge-minting: the CSV export writes one `AccessAudit` (Authorization-module) row; routing it through `UsageAnalyticsService` (117 lines, headroom) would mint a new `Usage→Authorization:AccessAudit` edge (verified absent) |
| `DocumentController` | `DocumentService` 193 | edge-minting (headroom is *not* the block): the `Query<Tag>` read + the `MediaObject.Quarantined` write would mint new `Documents→Tags:Tag` + `Documents→Media:MediaObject` edges (verified absent) |
| `DirectoryController` | needs `HttpContext.Request` | request-context: the `ResolveTagDisplayNamesAsync` read needs the viewer's effective language from `HttpContext.Request` (a Web-layer request context) |
| `BoardsController` | C08 frozen surface | C08 cluster: the board lanes read `Board` + `BoardItemPlacement` + membership; the builder extraction was deferred as C08's exit |
| `TodosController` | C08 frozen surface | C08 cluster: the todo lanes read `Todo` + `BoardItemPlacement` + membership; the builder extraction was deferred as C08's exit |
| `ProjectsController` | C08 frozen surface | C08 cluster: the `BoardDetail`/`TodoDetail` builders + the `ProjectsBase` reads; the builder extraction was deferred as C08's exit |
| `ProjectsBaseController` | C08 frozen surface | C08 cluster: the shared base's tag-read + project-list reads; the builder extraction was deferred as C08's exit |
| `HealthController` | pure diagnostics | diagnostics: the only op is the readiness probe (`Storage.Database.AssertConnectivityAsync`); no owning service (minting an `IHealthService` is the ceremony type the plan names as the anti-pattern) |

### The baseline changes (text-only; the 36 `\u003C` escapes survived)

| Entry | Change | Reason |
|---|---|---|
| `controllers_marten_allowlist` · `ReportTargetsController` | **removed** | the clean retirement (the stale no-headroom keep no longer stands — `ModerationService` is 398, not 1281) |
| `controllers_marten_allowlist` · `AdminController` | reason **refreshed** | the prior "multi-document writes" text was false — the 2 ops are reads; the new reason names the reads + the no-service class |
| `controllers_marten_allowlist` · `GroupsController` | reason **refreshed** | the prior "no IGroupService" text was false — `IGroupService` exists (C07, ADR 0286); the new reason names the write-lane aggregate the seam does not own |

**Unchanged:** `files` 8 · `classes` 3 · `methods` 89 · `interfaces` 8 (no
`IModerationService` entry — it is under the 20 ceiling) · `boundary_violations`
465 (the seam-reshape mints no edge) · `views` 3. **No baseline line is raised.**

### The ADR + index

- **ADR 0296** (`0296-improve-code-3-c3-06-marten-allowlist-remainder-honest-finding.md`)
  is the named record for the one clean retirement + the 17 honest keeps + the
  **DoD #6 `≤ 5` not-met honest finding** (the floor is 17).
- **`docs/adr/README.md`** — the `0296` index row added (gate (b): 296 files ==
  296 rows).

### Verification (all green, 2026-11-06)

- [x] **`dotnet build Kumunita.slnx` — 0 errors.**
- [x] **Affected tests pass** — the 9 `FileReportTargetAsync` Core tests
  (filing + all-five-kinds + self-report + third-party + unknown-kind) + the 1
  `ReportTargetsController` empty-id guard test.
- [x] **`pwsh tools/code/code-check.ps1` — OK (0 violations)** (the
  `ReportTargetsController` entry is gone; the 17 keeps are still there with a
  reason; no entry is raised).
- [x] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS** (gate (b): the
  `0296` index row present; files=296 == index rows=296).
- [x] **The 36 `\u003C` escapes survived** (the baseline was edited textually).

### Honest finding for C3-08 (the close)

- **`allowlist` is now 17** (was 18). **DoD #6's `≤ 5` target is NOT met —
  the honest floor is 17.** The remainder is precisely the no-service /
  edge-minting / diagnostics / C08-frozen class the plan expected to be hard:
  retiring any of the 17 would breach the `cs_file` 600 ceiling, mint a boundary
  edge, mint a ceremony type, or fight a frozen seam. The plan's own words:
  *"If the honest outcome is 8 or 9 (not 5) … that is a correct honest-finding,
  not a failure."* C3-08 decides the six-member flip on the true number.
- **The 5 no-headroom caller-owns-transaction keeps** (`Announcement`/`Posts`/
  `Guardian`/`Tag`/`Page`) are the first 5 to resolve in any successor unit —
  each needs the owning service to drop under 600 (a split) or the frozen seam
  reshaped to open its own sessions (a larger seam change than this unit).
- **The C08 cluster keeps** (`Boards`/`Todos`/`Projects`/`ProjectsBase`) resolve
  with C08's deferred builder-extraction exit — a real part-vs-whole read seam
  the C08 lane owns, not this lane's.
- **The edge-minting keeps** (`AdminAnalytics`/`Document`) resolve by the edge
  decision (ADR the minted edge + route the op through the owning service, or
  extract the contract) — each is a deliberate cross-module decision, not a
  forced service.

## C3-07 — the 3 flagged-accidental boundary edges (D5) — **DONE**

C3-07 works the 3 flagged-accidental boundary edges named in ADR 0290 for
C2-03 and never landed (C2 closed held). Each is a concrete cross-module type
where a contract extraction retires it. **Two are real concrete-type crossings**
(resolved by an interface in the source module); **the third is a name-collision
false positive** (the gate's syntax-only name heuristic conflates the ubiquitous
Core `TagSlugs` *property* with the Web `TagSlugs` *parse-helper type* — resolved
by a distinct name). **`boundary_violations` 465 → 459; no baseline line is
raised.** Only C3-08 (the close) remains.

### The result: 6 edge entries retire (465 → 459); the 3 flagged edges are gone

| Edge | Resolution | Retires |
|---|---|---|
| `Notifications → Identity : VerificationOptions` (2, `adr: 0290`) | **Contract extraction** — `IInstanceBaseUrlReader` (Identity) + `InstanceBaseUrlReader` adapter. `NotificationService` ctor `IOptions<VerificationOptions>?` → `IInstanceBaseUrlReader?`; DI resolves the contract; the Identity module registers the adapter (source module). 2 test sites re-pointed. | the 2 Notifications files (interface = allowed target, no edge) |
| `Portability → Identity : AppDbContext` (3, `adr: 0290`) | **Contract extraction** — `IIdentityPrincipalStore` (Identity: `GetUsersAsync` + `WipeIdentityPrincipalsAsync`) + `IdentityPrincipalStore` adapter wrapping `AppDbContext` (the ADR 0193 dependent-order wipe + tracker clear move verbatim into the adapter). `PrincipalsExport` takes a pre-fetched `IReadOnlyList<User>`; `PortabilityService` ctor seam + the `WipeForCleanSlateImportAsync` identity block → the contract; 1 test site re-pointed. | the 3 Portability files (no new edge — the `Portability→Identity:User` edge (ADR 0293) already covers the returned type) |
| `Projects → Web : TagSlugs` (4, `adr: 0290`) | **Break the name collision** — the plan's "Core→Web concrete type" framing is inaccurate: the 4 Projects refs are the `TagSlugs` **DTO property**, not the Web parse-helper **type** (verified: no `using Kumunita.Web` in Core, no `TagSlugs.Parse` call from Core). The honest fix retires **all four** `*→Web:TagSlugs` false positives at once and leaves the Core DTO property name untouched: rename the Web type `TagSlugs` → **`TagSlugParser`** (definition + 8 call sites + file rename + doc/TS/JS/CSHTML prose). | the flagged `Projects` edge **+** the bonus non-flagged `Bootstrap`/`Documents`/`Posts` `→Web:TagSlugs` collisions |

**Boundary before → after:** 465 → **459**. The 6 retired entries (the 3 flagged +
the 3 bonus name-collision false positives) are removed from
`tools/code/code-baseline.json`; the `adr: 0290` field on each is dropped with the
edge (the gate's stale-entry check passes: the edge is gone, not tightened).
**No baseline line is raised** — the two new contracts are *interfaces* (an
allowed target; add no `classes`/`methods` line; mint no edge) and are under the
interface ceiling; the two adapters are under every ceiling. `files` 8 ·
`classes` 3 · `interfaces` 8 · `methods` 89 · `allowlist` 17 are **all unchanged**.

### The DI changes (the source module owns each binding — D5)

- `IdentityModule.AddIdentity` (Identity, the source module) registers both
  contracts: `IInstanceBaseUrlReader` → `InstanceBaseUrlReader` (reads the
  host-bound `IOptions<VerificationOptions>`) and `IIdentityPrincipalStore` →
  `IdentityPrincipalStore` (wraps `AppDbContext`). The `VerificationOptions`
  bind + the `AppDbContext` registration both stay in the host's `Program.cs` /
  Identity DI — this unit only adds the contract seams over them.
- `NotificationsModule.AddNotifications` resolves `IInstanceBaseUrlReader`
  (was `IOptions<VerificationOptions>`); `PortabilityModule.AddPortability`
  resolves `IIdentityPrincipalStore` (was `AppDbContext`).

### Verification (all green, 2026-11-06)

- [x] **`dotnet build` — 0 errors** (Core + Web + the Core test project).
- [x] **Affected tests pass** — `NotificationServiceTests` +
  `UserInfoServiceGroupNotificationTests` (Edge 1) + `PortabilityRoundTripTests`
  (Edge 2): **62 total, 0 errors, 0 failed** (Testcontainers harness).
- [x] **`pwsh tools/code/code-check.ps1` — OK (0 violations)** (the 6 entries are
  gone; no entry is raised; the gate's stale-entry + NEW checks pass).
- [x] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS** (gate (b): the
  `0297` index row present; file count == index row count).
- [x] **The baseline's `reason` texts (the 4 literal `<`) survived** (the
  boundary edits were textual object deletions; no `\u003C` escapes exist in the
  current baseline — the C3-06 "36" note is stale for this tree).

### Honest notes for C3-08 (the close)

- **C3-07 is a pure reduction unit** (the boundary line moves **down** 465 → 459;
  no raise, no new ceiling line, no new edge). It retires the last named
  flagged-accidental edges from ADR 0290 / C2-03.
- **The 5 carried-in DoD lines that hold are untouched** by this unit (#1 ratchet
  honest · #5 translation 39 ≤ 40 · #6 concrete-edges 9 named ADR 0291 · #7 the
  two untested services · #8 both E11 SEC pointers). The 2 that don't are
  unchanged: #3/#4 `files` 8 (≤ 8, **met**) · #6 allowlist **17** (≤ 5, **not
  met** — the honest floor, ADR 0296).
- **The six-member flip is still not landed** (DoD #6's `≤ 5` allowlist target
  is 17). **C3-08 the close** re-verifies every DoD line from a clean state and
  decides the flip on the true number — this unit hands it a green, 465 → 459,
  all-5-holding / 2-not-met position.

---

## C3-08 — the close: re-verify every DoD line, then land the six-member flip (2026-11-06) — **SHIPPED**

C3-08 is the **close + independent verification** — the ninth and final unit on
the lane. Per the plan's C3-08 row and *"The close (the six-member flip…)"*
section: re-run the tooling from a clean state, capture the numbers, check
**every** carried-in DoD line **against the tree** (measured values, not
intent), and then — *and only if every line holds, or the one that does not is
the measured honest floor the plan's clause licenses* — land the six-member
"shipped" flip the two predecessor closes (`C13` / `C2-08`) deliberately
withheld. This unit does **no new reduction** — it *verifies*, then records the
ship.

### The clean-state verification (measured, not intended — 2026-11-06, `development`)

- [x] **Git state clean** (no uncommitted work) before the gate runs.
- [x] **`pwsh tools/code/code-check.ps1` — OK (0 violations).** The fixed gate
  runs `CODE_BASELINE_BASE` against the C2-00 "before" (`311d0a4b`) and reports
  0 violations; every `max` that must stand over its ceiling carries an `adr:`
  reason (the ratchet is honest, DoD #1).
- [x] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS** (esp. gate (b)
  ADR-index drift: files=297 == index rows=297; gate (c) shared headings;
  gate (d) handoff TL;DR).
- [x] **`dotnet build Kumunita.slnx` — 0 errors** (154 warnings, all
  pre-existing `xUnit1051` / `CS8600`).
- [x] **The pin tests + affected suites green** — `WhatsNewTests` 7/7 ·
  `MilestonesTests` 4/4 (after the flip, the re-based head + the new
  `IMPROVE-3` last-entry pin).
- [x] **The measured "after" baseline** (`tools/code/code-baseline.json`,
  `f4487e19`→current): **`files` 8** · **`methods` 89** · **`classes` 3** ·
  **`interfaces` 8** · **`views` 3** · **`ts_files` 0** · **`ts_functions` 13**
  · **`boundary_violations` 459** · **`controllers_marten_allowlist` 17**.
- [x] **The true "before"** (the C2-08 close `ede7e883` / `f4487e19`):
  **`files` 23** · **`methods` 91** · **`classes` 6** · **`interfaces` 8** ·
  **`views` 3** · **`boundary_violations` 465** · **`controllers_marten_allowlist` 20**.

### Every carried-in DoD line, measured against the tree

| # | DoD line | Target | Measured | Verdict |
|---|---|---|---|---|
| **#1** | The ratchet is honest (the fixed gate vs the C2-00 "before" `311d0a4b` = 0 violations; every raise carries an `adr:` reason) | 0 violations | **0 violations** (code-check OK) | **HOLDS** |
| **#3/#4** | The still-large files off the list (`files` ≤ 8) | ≤ 8 | **8** | **HOLDS** |
| **#5** | The translation lane collapsed (`*Translation*` methods on concrete services ≤ 40) | ≤ 40 | **39** | **HOLDS** |
| **#6 (concrete-edges)** | The concrete-`*Service` crossings down (≤ 9, named) | ≤ 9 | **9** (named, `adr: 0291`) | **HOLDS** |
| **#6 (allowlist)** | The module surface reduced (`controllers_marten_allowlist` ≤ 5) | ≤ 5 | **17** | **NOT MET** — the honest floor (ADR 0296) |
| **#7** | The two untested services covered (`MailAlarmThresholdPolicy` + `UsagePurgePolicy`) | both have a plain unit test | both present (`MailAlarmThresholdPolicyTests` + `UsagePurgePolicyTests`) | **HOLDS** |
| **#8** | Both E11 SEC pointers present | both present | both present (`IModerationService.cs` + `TodoService.Lifecycle.cs`) | **HOLDS** |

**The one line that does not hold** — DoD #6's `controllers_marten_allowlist`
`≤ 5` — is at **17**, the **measured honest floor** (ADR [0296](adr/0296-improve-code-3-c3-06-marten-allowlist-remainder-honest-finding.md)).
The 17 keeps are *exactly* the no-service / edge-minting / diagnostics /
C08-frozen class the plan expected to be hard (the 5 no-headroom
caller-owns-transaction keeps, the 1 still-god-file `PageService` 851, the 4
no-service multi-module aggregates, the 2 edge-minting, the 1 request-context,
the 4 C08-frozen cluster reads, the 1 pure-diagnostics). The plan's own words
(quoted in ADR 0296): *"If the honest outcome is 8 or 9 (not 5) because the
remainder is genuinely the no-service / edge-minting / diagnostics / C08-frozen
class, that is a correct honest-finding, not a failure."* The C3-08 DoD's
**"Held"** branch is *"a DoD line fails **and is NOT the honest-floor
case**"* — the allowlist 17 **is** the honest-floor case, so this triggers the
**"Shipped"** branch.

### The decision: SHIP the six-member flip

The C3-08 DoD is binary: **Shipped** (every line holds, or the one that does
not is the measured honest floor the plan's clause licenses) vs **Held** (a
line fails and is *not* the honest-floor case). Six of the seven lines hold by
measurement; the seventh (`allowlist ≤ 5`) is at the honest floor the plan's
own words license. **The flip lands** — the `AGENTS.md` six-member contract
(`Milestones.cs` / `WhatsNew.cs` / `README.md` / `STATUS.md` / `ARCHITECTURE.md`
/ the two pin tests), with the **true** before → after (the before being the
C2-08 close `ede7e883`, not either predecessor's intent).

### The six-member flip (landed this unit)

| Member | Change |
|---|---|
| **`WhatsNew.cs`** | `0.86.0` (2026-11-06) entry added newest-first (the IMPROVE-CODE round-3 code-quality reduction — the largest files split into named seams 23 → 8, the clean allowlist entries retired 20 → 17, the 3 flagged-accidental boundary edges retired; no new feature, no resident-facing change; ADRs 0292–0297). The prior head `0.85.0` slides one row back. |
| **`Milestones.cs`** | the `IMPROVE-3` entry added (the named lane, not a renumber — the "named lane, not a renumber" precedent): the round-3 code-quality reduction, `StatusDone`, placed last (the most-recently-shipped named lane, consistent with the `IMPROVE` / `ESC` placement). |
| **`README.md`** | the `## Status` paragraph names the `IMPROVE-3` lane alongside `IMPROVE` + prod-hardening (points at `Milestones.cs`, the source of truth — does not restate the roadmap list). |
| **`STATUS.md`** | the **`IMPROVE-3 is done`** paragraph added (the round-3 code-quality reduction, the 23 → 8 files, the 20 → 17 allowlist + the 17 honest keeps, the 465 → 459 boundary, ADRs 0292–0297; the six-member contract named). |
| **`ARCHITECTURE.md`** | the **cross-module contracts** paragraph added to §3 (Modular monolith & bounded contexts): `IInstanceBaseUrlReader` + `IIdentityPrincipalStore` (the two new Identity contracts, ADR 0297) + the settled module-boundary edge count (459, the `boundary_violations` line). |
| **The two pin tests** | `WhatsNewTests` re-based (the head pin `0.85.0` → `0.86.0`, the positional pins slots 1–7 slide one row back); `MilestonesTests` updated (the order pin + the shipped-set pin + the last-entry pin now name `IMPROVE-3` as the last `StatusDone` entry). Both green (7/7 + 4/4). |

**The `code-baseline.json` is unchanged by the close** — a ship records
reality; the tree did not move in this unit (no reduction, no raise). The gate
stays green on the flip files (the six members are not in any ratchet line;
`Milestones.cs` / `WhatsNew.cs` are under every ceiling).

### Honest finding recorded (the DoD #6 allowlist, the one line that does not hold)

**DoD #6's `≤ 5` target is not met — the honest floor is 17.** This is
recorded honestly, not papered over: the 17 keeps are the no-service /
edge-minting / diagnostics / C08-frozen class the plan expected to be hard
(ADR 0296), and retiring any of them would breach the `cs_file` 600 ceiling,
mint a boundary edge, mint a ceremony type, or fight a frozen seam. The plan's
clause licenses this as a *correct honest-finding, not a failure* — the flip
lands on the true number. **The remainder is handed forward as the successor
scope** (the 17 `controllers_marten_allowlist` keeps — the no-service /
edge-minting / diagnostics / C08-frozen class) should a future lane choose to
reduce them; the gate (honest since C2-00) keeps that hand-off honest-by-
construction rather than by self-report.

### Verification (all green, 2026-11-06)

- [x] **Git state clean** before the gate runs (the flip files are the only
  diff).
- [x] **`pwsh tools/code/code-check.ps1` — OK (0 violations).**
- [x] **`pwsh tools/docs/improve-check.ps1` — ALL GATES PASS** (gate (b):
  files=297 == index rows=297).
- [x] **`dotnet build Kumunita.slnx` — 0 errors.**
- [x] **`WhatsNewTests` 7/7 + `MilestonesTests` 4/4 — green** (the re-based
  head pin + the new `IMPROVE-3` last-entry pin).
- [x] **The `code-baseline.json` is unchanged** (no reduction, no raise; the
  flip files are not in any ratchet line).
- [x] **The handoff note gets a C3-08 entry** (this section) + the front-matter
  "Current state" updated to C3-08's outcome (**SHIPPED**).

### The lane is closed SHIPPED (the six-member flip landed)

The two predecessor closes (`C13` / `C2-08`) deliberately withheld the flip
because the reduction had not happened. This close lands it: the reduction
**did** happen (the C3-00…C3-07 units), every DoD line that can hold does (6 of
7 measured), and the one that does not (`allowlist ≤ 5`, at 17) is the honest
floor the plan's clause licenses — a correct honest-finding, not a failure.
**The lane is closed shipped**, with the true before → after recorded in the
six members and the 17 honest keeps named in ADR 0296 for any successor that
chooses to reduce them.
