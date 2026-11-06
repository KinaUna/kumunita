---
id: adr-0295
title: "IMPROVE-CODE-3 C3-05 — the Marten-allowlist clean retirements: 2 retirements (Goals, Account) + the IIdentityService interface raise + 5 honest keeps"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0290-improve-code-2-baseline-raises-and-seams.md
  - docs/adr/0291-improve-code-2-c2-03-carry-the-9-concrete-service-crossings.md
  - docs/adr/0293-improve-code-3-c3-03-god-service-seam-boundary-edge-raises.md
  - docs/adr/0294-improve-code-3-c3-04-partial-class-splits-boundary-raises.md
  - docs/plans-milestones/plan-improve-code-3.md
  - docs/philosophy/in-code.md
supersedes: []
superseded_by: []
updated: 2026-11-06
---

# ADR 0295 — IMPROVE-CODE-3 C3-05: the two clean allowlist retirements + the named interface raise

Status: **Proposed** (this ADR is the `adr:` named in the
`tools/code/code-baseline.json` `interfaces` entry for `IIdentityService`, which
C3-05 raised from 33 → 34. The gate's *"a `max` raised above the base without an
`adr:` fails"* rule is satisfied by that named entry. The two allowlist
**retirements** are pure reductions — always allowed, no `adr:` needed — and the
five **honest keeps** keep their entries with their reasons updated; none of
them is a raise.)

> **Abstract:** the named record for C3-05's one baseline **raise** (the
> `IIdentityService` interface, 33 → 34 public methods, the
> `LoadVerifyTokenAsync` seam added so `AccountController` retires its last
> Marten op) and the two **clean retirements** it enables — the
> `GoalsController` + `AccountController` entries leaving the
> `controllers_marten_allowlist` (20 → 18). **For** a reviewer of C3-05's close,
> and whoever picks up the C3-06 remainder. **When to read the rest:** before
> deciding whether a now-clean allowlist keep can retire, or when a gate failure
> on the `IIdentityService` interfaces entry is the starting point.

## Context

C3-05 (the `controllers_marten_allowlist` clean retirements, DoD #6
`20 → ≤ 5`) works the 20 allowlist entries and retires the ones whose owning
service is now ≤ 600 (the C3-04 partial-class splits shrank the group-(a) god
files, which is exactly the condition their "god-file at the ceiling" keep was
waiting for). The plan's own words: *"Retire each one only where the service has
headroom — moving the store op in must NOT push that file over 600. If a service
is at 582 and the op is large, that's an honest finding: record it and keep the
entry, do NOT force it."*

Two of the seven in-scope candidates (the six group-(a) god-file keeps + the
`GoalsController`) are genuinely clean; the other five are honest keeps. The
single baseline **raise** C3-05 makes is the `IIdentityService` interface
(method count), and this ADR is its named record so the gate stays green.

The ratchet is the arbiter: a baseline `max` only goes down; a value that must
go *up* needs an `adr:` reason (a named, defensible exception, not a silent one).
Shrinking a type under the ceiling and having its baseline entry retire is always
allowed. This lane moves only the ratchet down (2 allowlist retirements); its one
raise is named here.

## Decision

**The one named raise — `IIdentityService` 33 → 34 (`interfaces`, `adr: 0295`).**

`AccountController` performs exactly one Marten op: the verify-link token load
(`LoadUsableVerifyTokenAsync`, 16 code lines, reads the Identity-module
`IdentityToken` row by id). To retire its allowlist entry, that op moves to the
IdentityModule — the owning service of the `IdentityToken` document — as a new
public seam:

| Addition | What it is | Why it is honest |
|---|---|---|
| `IIdentityService.LoadVerifyTokenAsync(string tokenId) → Task<IdentityToken?>` | a by-id read of one `IdentityToken` row; the caller owns the `Kind` + `IsUsableAt` usability decision (a Web presentable-error concern) | the `IdentityToken` document is the IdentityModule's (`IdentityService` already owns + reads `IdentityToken` rows in the verify lanes); the read is a pure seam ADD (ADR 0006-E *compatible ADD* — the `VerifyWithTokenAsync` lane shape), not a ceremony type |

The interface gains one public method (33 → 34). This is the named exception:
the ratchet's "a `max` raised above the base without `adr:`" rule is satisfied by
this entry. No other baseline entry moves.

**The two clean retirements (the allowlist `20 → 18`):**

| Controller | Why it is clean (measured, not assumed) |
|---|---|
| `GoalsController` | **0 Marten ops of its own.** Its goal actions (`GoalDetail` / `GoalNew` / `GoalCreate`) route entirely through the frozen `IProjectService` seam (`GetGoalAsync` / `ListProjectsForGoalAsync` / `CreateGoalAsync`, each opening its own write session + audit row). It held the store only to forward to the base's `SeedExistingTagSlugsAsync` helper — which no goal action calls. C3-05 made the base's `store` nullable + null-guarded the helper, and `GoalsController` drops the ctor param (passing `null`). The 3 sibling base call sites (`Boards`/`Todos`/`Projects`) still pass their real store; the 1 test call site drops the arg. |
| `AccountController` | **1 op** (the verify-token load above), now `IIdentityService.LoadVerifyTokenAsync`. The controller drops the `store` ctor param + `using Marten;`; its `LoadUsableVerifyTokenAsync` calls the seam. The `Web→Identity:IdentityToken` boundary edge **persists** (the controller still references the `IdentityToken` return type), so **no** boundary entry changes. 8 test call sites drop the `store:` arg (the 2 verify-path tests wire `identity.LoadVerifyTokenAsync` to the same real store they seeded, faithful to the old controller read). |

**The five honest keeps (the entry stays; the reason text is updated to the C3-05
measured rationale — the old "god-file at the ceiling" text is now false):**

| Controller | Owning service (code lines) | The measured keep reason (→ C3-06) |
|---|---|---|
| `AnnouncementController` | `AnnouncementService` 519 | caller-owns-transaction write lanes (the controller opens the session + passes it to `CreateAsync`/`UpdateAsync`/`DeleteAsync`/`PublishAsync`, which require a passed-in session) — ~152 code lines; 519 + 152 = 671 > 600, **no headroom**. C3-06: reshape the frozen seam to open its own sessions. |
| `DocumentController` | `DocumentService` 193 | **edge-minting** (a C3-06 D3 group-(c) decision, not a headroom keep): the tag-slug read (`Query<Tag>`) + the Media quarantine write (`MediaObject.Quarantined`) would MINT NEW `Documents→Tags:Tag` + `Documents→Media:MediaObject` edges (verified: neither is in the baseline; `DocumentService` references neither concrete type). Service has ample headroom (193) — the block is the minted edges, not size. |
| `TagController` | `TagService` 529 | the translation-write lane is caller-owns-transaction (passes the session to `AddTagTranslationAsync`), **and** the `SeedTranslationFormAsync` read is register-pinned ("no new seam" — the in-code comment names the U8c register pin forbidding an `ITagService` "get translations" method). C3-06: seam-reshape + a register amendment. |
| `PostsController` | `PostService` 583 | the post/reply write lanes are caller-owns-transaction (the prompt's own named example) + the `Tag`-read seeder; `PostService` has headroom of only 17, but the lanes are ~98+ code lines (Edit 26 + DeletePost 31 + Publish 31 + the reply lanes) — 583 + lanes > 600, **no headroom**. C3-06: the C3-04 `PostService.Media.cs` split already retired the media lane; the remainder is the seam-reshape. |
| `GuardianController` | `GuardianService` 555 | the 4 `GuardianLink` read lanes (ActiveChildren 20 + ActiveGuardians 20 + ActiveGuardianCount 8 + ActiveLink, ~48–56 code lines) read UserInfo-module rows; 555 + ~50 ≈ 605 > 600, **no headroom** (the C3-04 split already landed `GuardianService.Approvals.cs`, 226 lines). C3-06: a real read seam on `IGuardianService`, or a documented no-headroom keep. |

## Consequences

- **The allowlist `20 → 18`.** Two entries retire (`GoalsController`,
  `AccountController`); the gate detects both files as no longer on Marten
  (the pure-text scan for `IDocumentStore`/`IDocumentSession`/`IQuerySession`
  finds neither token in either file), so the stale-entry check passes and the
  "controller on Marten without an entry" check stays green.
- **One named raise** (`IIdentityService` 33 → 34, `adr: 0295`) — 0 silent
  raises. The `IIdentityService` `interfaces` entry is the only raised entry;
  the `classes`/`methods`/`files`/`boundary_violations` lines are unchanged
  (the `Web→Identity:IdentityToken` edge persists, so no boundary entry moves).
- **C3-06 inherits the 5 honest keeps** (the `Announcement`/`Document`/`Tag`/
  `Posts`/`Guardian` group-(a) keeps, with their measured reasons) **plus** the
  pre-existing group-(b/c/d) no-service / edge-minting / C08-cluster /
  diagnostics entries (the prompt's explicit C3-06 list). C3-06's job is to push
  the allowlist `18 → ≤ 5` — that is a seam-reshape + edge-decision +
  register-amendment class of work, not a clean-move class.
- **The six-member flip is still not landed** — C3-05 does not close the lane.
  DoD #6's `≤ 5` target is not met (18 > 5); the flip lands only when every
  carried-in DoD line holds at the close (C3-08). This ADR records the
  intermediate, honest position: the clean subset is retired, the rest is
  named and handed forward.
