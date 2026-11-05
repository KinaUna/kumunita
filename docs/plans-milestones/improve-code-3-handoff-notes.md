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
updated: 2026-11-05
---

> **Abstract:** the running handoff log for the IMPROVE-CODE-3 lane.
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
