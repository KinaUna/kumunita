---
id: adr-0290
title: "IMPROVE-CODE-2 C2-00 — the 30 baseline raises: 4 named seams (named, pending split) + 26 intended-coupling edges (named, C2-03 to extract where accidental)"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0006-module-boundary-contracts.md
  - docs/adr/0286-iuserservice-access-core-split.md
  - docs/adr/0287-projects-module-split.md
  - docs/adr/0288-one-translation-variant-lane.md
  - docs/plans-milestones/plan-improve-code-2.md
  - docs/philosophy/in-code.md
supersedes: []
superseded_by: []
updated: 2026-11-05
---

# ADR 0290 — IMPROVE-CODE-2 C2-00: the 30 baseline raises

Status: **Proposed** (this ADR is the `adr:` named in `tools/code/code-baseline.json`
entries that were raised between the predecessor's "before" `311d0a4b` and the
predecessor's close `219ba33c`. The gate's *"a `max`/`cc_max`/`count` raised above
the base without an `adr:` fails"* rule is satisfied by the named entries; the
*shrinking* of the 4 god-type / god-method entries (the plan's D2 clean resolution)
is the follow-up work this ADR explicitly **defers** to the named successor units.)

> **Abstract:** the named, defensible exception record for the 30 baseline
> entries the predecessor's C06–C09 splits raised without an `adr:` — 4
> shrinkable god-type / god-method entries (the named seams + the unit that
> will shrink them) and 26 cross-module edge counts (the intended coupling the
> C06/C07/C08 splits created, with the accidental-integration edges named for
> C2-03). **For** a reviewer of C2-00's close and any successor unit touching
> the 4 seams or the 26 edges. **When to read the rest:** before shrinking any
> of the 4 seams, before extracting any of the 26 edges, or when a gate
> failure on one of these entries is the starting point.

## Context

The predecessor (`IMPROVE-CODE`, `C00`–`C13`, archived at
[`docs/archive/lanes/improve-code/`](../archive/lanes/improve-code/)) built the
ratchet gate but left **30 baseline entries raised between `311d0a4b`
(the C00 "before") and `219ba33c` (the C13 close)** without an `adr:` field.
The gate-fix commit (`ebf5b3b5`) made the *"a raise without an `adr:` fails"*
check real in CI; running it against the "before" reports exactly those 30 as
failures. The predecessor's C13 cold read classified them:

- **4 shrinkable god-type / god-method raises** — the class / interface /
  method surface went up, and the type is *over the ceiling* (class 25 /
  interface 20). Shrinking them under the ceiling retires the entry.
- **26 boundary-count raises** — cross-module edge *counts* went up. Not a
  size problem; the fix is a named `adr:` (the intended coupling) or a
  C2-03 contract extraction (the accidental integration).

The predecessor's instruction to its successor — *"If any DoD line fails,
stop and say which one and why — do not fix it silently"* — is carried into
C2-00's DoD: **no raise is silent**. This ADR is the named exception record;
it does **not** do the shrinking (that is the named successor's job).

## Decision

### The 4 shrinkable god-type / god-method entries (the `adr:` half of D2)

| Baseline entry (id, in `code-baseline.json`) | Raised | Ceiling | This ADR's named seam |
|---|---|---|---|
| `Core/Events/EventService.cs` **class** (public methods) | 30 → **31** | 25 | `EventRsvpGuardianService` (the 6-method `RsvpAsync` / `ApproveEventRsvpAsync` / `DenyEventRsvpAsync` / `VetoEventRsvpAsync` / `GetPendingEventRsvpRequestsAsync` / `GetChildRsvpsAsync` cluster — ADR 0028's account-scope supervision, ADR 0090's pagination, ADR 0115's guardian-event-link) |
| `Core/Pages/PageService.cs` **class** (public methods) | 28 → **30** | 25 | `PageTranslationStandingService` (the 4-method translation standing cluster: `CheckCreateStanding` / `CheckEditStanding` / `CheckTranslateStanding` / `CanTranslatePage` — the C04 `*Policy` pattern, mirroring `EventStandingPolicy`) |
| `Core/Events/IEventService.cs` **interface** (methods) | 27 → **28** | 20 | `IEventRsvpGuardian` (the 6-method surface the `EventRsvpGuardianService` owns — the C03 `INotificationEmitter` pattern: a named seam the Web consumer codes against, the service behind it) |
| `Web/Controllers/HomeController.cs::HomeController.BuildPostRowsAsync` **method** `cc_max` | 20 → **22** | 15 | `HomeFeedRowBuilder` (a `*Policy` per the C04 pattern — the per-column "build the feed rows" decision extracted as a pure, session-free helper with its own unit test; the C04 `EventStandingPolicy` is the precedent) |

**Why this ADR names the seams rather than shrinking them:** the plan's D2 is
*"shrink under the ceiling, retiring the baseline entries"* — the *clean*
resolution. The *floor* is the `adr:` named here. C2-00 does the floor (the
gate goes green; no raise is silent) and **names the clean resolution** (the
named seams above) so the shrinking is a real, tractable part-vs-whole split,
not a mechanical ceremony. A unit that must raise anything names the ADR it
amends or creates first — this ADR is that naming. The 4 entries keep their
`max` value (the ceiling is not raised) until a successor unit shrinks them
under the ceiling; at that point `--tighten` retires the entry (the `adr:`
field is dropped with it).

> **Scope note (honest hand-off):** the 4-shrink clean-resolution is a
> **distinct reduction scope** — none of the plan's C2-01…C2-08 units is
> *scoped* to it. C2-05 ("the still-large files") is the closest thematic
> home (it already shrinks god-files like `EventService.cs` / `PageService.cs`),
> but the plan names only `FirstBootSeeder` / `SampleDataSeeder` /
> `ProjectsController` for C2-05. **The successor must explicitly adopt the
> 4-shrink scope** — either by extending C2-05 to include the
> `EventService` / `PageService` class shrinks + the `IEventService` /
> `BuildPostRowsAsync` extracts, or by naming it in the close. It must not be
> silently dropped because no unit *currently* owns it. The named seams above
> are what make it tractable (a real part-vs-whole split the C13 cold read
> would verify, not a ceremony type).

### The 26 boundary-count edges (the `adr:` half of D1)

Each of the 26 carries `"adr": "0290"` in `code-baseline.json`. The
per-edge read is below. **"Intended"** = the C06/C07/C08 splits
*deliberately* cross that edge (the coupling is the point of the split).
**"Flagged-accidental"** = the edge is a concrete `*Service` crossing where
an interface / `*Policy` would do — **C2-03's** territory; the file is named
so C2-03 can do the contract extraction.

| Edge (from → to : symbol) | Count before → now | Intended / flagged-accidental | Owner ADR | Note |
|---|---|---|---|---|
| `ErrorReports → Authorization : AccessAudit` | 2 → 3 | intended | ADR 0006 | the M31 report-an-issue write lane's audit row (the C04 `AccessAudit` pattern) |
| `Mentions → UserInfo : DirectoryService` | 1 → 2 | intended | ADR 0286 | the C07 `IProfileSettingsService` directory read the mention-resolver uses |
| `Notifications → Identity : VerificationOptions` | 1 → 2 | flagged-accidental | ADR 0286 (UserInfo split) | `NotificationService` reads the Identity verification config directly — a concrete `IdentityService` where a `IVerificationOptionsReader` would do; C2-03 |
| `Notifications → UserInfo : Profile` | 1 → 2 | intended | ADR 0286 | the C07 `IProfileSettingsService` read the notification emitter needs |
| `Portability → Identity : AppDbContext` | 2 → 3 | flagged-accidental | ADR 0286 (Identity) | the C10 `Portability` export reads `AppDbContext` directly (a concrete `IdentityService` seam) — C2-03 |
| `Portability → Identity : User` | 4 → 5 | intended | ADR 0286 | the C10 portability archive reads the `User` document (the ADR 0006 module-boundary contract) |
| `Posts → Identity : Roles` | 1 → 2 | intended | ADR 0006 | the post standing check reads `Roles` (the ADR 0006 frozen seam) |
| `Projects → Authorization : AccessAuditFactory` | 1 → 3 | intended | ADR 0006 / ADR 0287 | the C08 `Projects` split's audit rows via the `AccessAuditFactory` |
| `Projects → Authorization : Audience` | 10 → 12 | intended | ADR 0287 | the C08 `IProjectService` / `IBoardService` / `ITodoService` audience reads (ADR 0099 / 0100) |
| `Projects → Authorization : StandingMatrix` | 1 → 3 | intended | ADR 0287 | the C08 split's standing decisions via the `StandingMatrix` |
| `Projects → Events : Event` | 1 → 2 | intended | ADR 0115 | the M15 events ↔ projects integration (ADR 0115 is the ADR) |
| `Projects → Events : EventToAuditableResource` | 1 → 2 | intended | ADR 0115 | the ADR 0115 integration's audit adapter |
| `Projects → Identity : Roles` | 1 → 7 | intended | ADR 0006 / ADR 0287 | the C08 split's role reads across `IProjectService` / `IBoardService` / `ITodoService` |
| `Projects → Query : PageSizer` | 1 → 3 | intended | ADR 0090 | the ADR 0090 pagination helper across the C08 sub-interfaces |
| `Projects → Web : TagSlugs` | 2 → 3 | flagged-accidental | ADR 0287 | the C08 `TodoService` / `BoardService` reads `TagSlugs` directly (a concrete `TagService` seam) — C2-03 |
| `UserInfo → Authorization : AccessAudit` | 1 → 5 | intended | ADR 0286 | the C07 split's audit rows across `IGroupService` / `ICommunityMembershipService` / `IGuardianService` / `IProfileSettingsService` |
| `UserInfo → Identity : Roles` | 1 → 4 | intended | ADR 0006 / ADR 0286 | the C07 split's role reads (the ADR 0006 frozen seam) |
| `Web → Authorization : Audience` | 16 → 19 | intended | ADR 0006 | the Web controllers' audience reads (the ADR 0006 module-boundary contract) |
| `Web → Identity : Roles` | 34 → 36 | intended | ADR 0006 | the Web controllers' role reads (the ADR 0006 frozen seam) |
| `Web → Identity : User` | 71 → 74 | intended | ADR 0006 / ADR 0286 | the Web controllers' `User` document reads (the ADR 0006 module-boundary contract) |
| `Web → Messaging : Message` | 18 → 21 | intended | ADR 0278 | the ADR 0278 group-messaging N1 lane (the `Message` document read) |
| `Web → Projects : BoardItemPlacement` | 1 → 2 | intended | ADR 0287 | the C08 `IBoardService` placement read (the ADR 0074 board-lane precedent) |
| `Web → Projects : KanbanBoard` | 5 → 6 | intended | ADR 0287 | the C08 `IBoardService` board read (the ADR 0067 / 0068 / 0069 / 0070 boards) |
| `Web → Projects : ProjectGoal` | 1 → 2 | intended | ADR 0287 | the C08 `IProjectService` goal read (the ADR 0086 goals lane) |
| `Web → Projects : TodoItem` | 5 → 6 | intended | ADR 0287 | the C08 `ITodoService` todo read (the ADR 0067 / 0073 / 0074 / 0100 todos) |
| `Web → Tags : Tag` | 7 → 9 | intended | ADR 0006 | the Web controllers' `Tag` document reads (the ADR 0006 module-boundary contract) |

**Count check:** 26 edges, each with a named owner ADR. **3 flagged-accidental**
(`Notifications → Identity : VerificationOptions`, `Portability → Identity :
AppDbContext`, `Projects → Web : TagSlugs`) are named for C2-03 — the
contract-extraction unit. The remaining **23 intended** carry the `adr:` named
here.

## Consequences

- **Positive:** the gate's *"a raise without an `adr:` fails"* rule is
  satisfied — running the fixed gate against the predecessor's "before"
  (`311d0a4b`) no longer reports an un-`adr`'d raise. The 30 are no longer
  silent. The clean resolution (the 4 shrinks + the 3 accidental-extractions)
  is named and owned by a successor unit, not deferred to "later."
- **Negative:** 23 of the 26 edges carry `adr: 0290` as a *named exception*,
  not a *shrinking*. The C2-03 / C2-05 successors must do the shrinking or the
  `adr:` stays — the ratchet is the teeth, not the memory.
- **Note:** the ADR status is **Proposed** (not **Accepted**) because the
  shrinking it defers has not landed. When C2-05 lands the 4 shrinks (the
  entries retire) and C2-03 lands the 3 accidental-extractions (the counts
  drop), this ADR is amended to **Accepted** and the `adr:` fields on the
  retired entries are dropped by `--tighten`.
