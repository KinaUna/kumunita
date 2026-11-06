---
id: adr-0296
title: "IMPROVE-CODE-3 C3-06 — the Marten-allowlist remainder: the 1 clean retirement (ReportTargets) + the honest-17 floor (DoD #6 ≤ 5 not met)"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0295-improve-code-3-c3-05-marten-allowlist-clean-retirements.md
  - docs/adr/0290-improve-code-2-baseline-raises-and-seams.md
  - docs/archive/lanes/improve-code/plan-improve-code-3.md
  - docs/archive/lanes/improve-code/improve-code-3-handoff-notes.md
  - docs/philosophy/in-code.md
supersedes: []
superseded_by: []
updated: 2026-11-06
---

# ADR 0296 — IMPROVE-CODE-3 C3-06: the allowlist remainder — 1 clean retirement + the honest-17 floor

Status: **Proposed** (this ADR is the named record for C3-06's work on the
`controllers_marten_allowlist` remainder — the 18 entries C3-05 (ADR 0295)
left. One entry retires (a pure reduction, always allowed, no `adr:` needed);
the other 17 are honestly **kept** with their reasons refreshed where they had
gone stale. **No baseline entry is raised** — the `files` / `classes` /
`interfaces` / `methods` / `boundary_violations` lines are all unchanged, and
the allowlist goes **down** 18 → 17. The DoD #6 `≤ 5` line is **not met**;
this ADR records that honestly and hands the finding to C3-08 the close.)

> **Abstract:** the named record for C3-06 — the lane's
> `controllers_marten_allowlist` remainder. **One clean retirement** lands
> (`ReportTargetsController` — its keep reason had gone stale: `ModerationService`
> is **398 code lines**, not the 1281 the reason claimed; C3-03/C3-04 split it.
> The seam-reshape is the `FileReportTargetAsync` service lane opening its own
> session, so the controller's caller-owns-transaction shape disappears). **The
> other 17 are genuinely hard** and are honestly kept: 5 no-headroom
> caller-owns-transaction seams (the C3-05 keeps, re-measured), 1 still-god-file
> (`PageService` 851), 4 no-service multi-module aggregates (`Admin` — whose
> reason is refreshed: the 2 ops are *reads*, not writes — `Groups` — whose
> reason is refreshed: `IGroupService` now *exists* (C07, ADR 0286) — `Attachment`
> + `ContentImage`), 2 edge-minting (`AdminAnalytics`, `Document`), 1
> request-context (`Directory`), 4 C08-frozen cluster reads (`Boards`, `Todos`,
> `Projects`, `ProjectsBase`), and 1 pure-diagnostics (`Health`). **The honest
> floor is 17** — the `≤ 5` DoD line is not met because the remainder is
> precisely the no-service / edge-minting / diagnostics / C08-frozen class the
> plan expected to be hard, and forcing any of them would breach the ceiling,
> mint a boundary edge, or fight a frozen seam. **For** a reviewer of C3-06's
> close, and C3-08 (which decides the six-member flip on the true number).
> **When to read the rest:** before deciding whether an allowlist keep can
> retire, or when C3-08 is weighing the flip on DoD #6.

## Context

C3-05 (ADR 0295) retired the two clean entries (`GoalsController`,
`AccountController`) — 20 → 18 — and deliberately left the 18 hard ones to
C3-06. This unit works the remainder toward the lane's `≤ 5` DoD target,
retiring every genuinely-retire-able entry and **honestly recording** the rest.
The rule, carried from both predecessors: *"If any DoD line fails, stop and say
which one and why — do not fix it silently."*

The ratchet is the arbiter: a baseline `max` only goes down; a value that must
go *up* needs an `adr:` reason. A pure retirement (a file dropping off the
allowlist) is always allowed. This lane moves the ratchet **down** (1
allowlist retirement); it makes **no raise** — the `files` 8 · `classes` 3 ·
`methods` 89 · `interfaces` 8 · `boundary_violations` 465 lines are all
unchanged.

The gate mechanic (the arbiter of "on Marten"): `ControllersWithMarten` greps
each file in `src/Kumunita.Web/Controllers/` for the substring
`IDocumentStore` / `IDocumentSession` / `IQuerySession` (including doc `cref`s).
Retiring an entry = removing all three tokens from the file **and** the baseline
entry; keeping one = the file retains ≥ 1 token + the entry has a `reason`.

## Decision

**The one clean retirement — `ReportTargetsController` (the allowlist `18 → 17`).**

The entry's C3-05 reason was **stale**: it claimed *"the owning ModerationService
(src/Kumunita.Core/Moderation/ModerationService.cs, **1281 lines**) is a god-file
at its ratchet ceiling"*. That is false at this tree — C3-03 (ADR 0293) / C3-04
(ADR 0294) split `ModerationService` down; it is now **398 code lines** (the
report-intake lane extracted to `ModerationService.ReportIntake.cs`, **363 code
lines**). The no-headroom keep no longer stands. The controller's 5 report-target
actions (event / project / board / to-do / group-post) all use the same
caller-owns-transaction shape: the controller opens `store.LightweightSession()`
and passes it to `IModerationService.FileReportTargetAsync(..., session)`.

The seam-reshape (the prompt's named resolution for the C3-05 keeps'
caller-owns-transaction class, applied here where it is clean):

| Change | What it is | Why it is honest |
|---|---|---|
| `IModerationService.FileReportTargetAsync` — drop the trailing `IDocumentSession session` param | the seam now opens its own session; the ADR 0006-C no-partial-write discipline is unchanged (the `Report` row + the filing `AccessAudit` row are still staged into the same session and committed in one `SaveChangesAsync`) | a pure seam signature change (ADR 0006-E compatible change); `IModerationService` is **not** in the `interfaces` baseline (11 public methods, under the 20 ceiling), so no baseline entry moves; the only caller of the 7-arg form was the 5 actions being reshaped |
| `ModerationService.ReportIntake.cs` impl — open `await using var session = _store.LightweightSession();` at the top, drop the `session is null` guard | the service opens its own session (the `FileReportAsync` / `FileReplyReportAsync` "the service opens its own session" precedent); the rest of the body (`session.Store(report)`, `_notifications.EmitAsync(session, …)`, `session.SaveChangesAsync()`) is unchanged | `ModerationService` already holds `_store` and opens its own sessions elsewhere; no new dependency, no new boundary edge (the lane already references `Report` / `AccessAudit` / `INotificationEmitter`) |
| `ReportTargetsController` — drop the `IDocumentStore store` ctor param, the `using Marten;`, the line-34 doc `cref`, and the 5 `store.LightweightSession()` opens + 5 `session: session` args | the controller no longer touches the store | the gate's pure-text scan finds **none** of the three tokens in the file → the stale-entry check passes and the retirement lands |
| `ReportTargetsControllerTests` + `ModerationServiceTests` — the test sites drop the `session` arg (the service now opens + commits its own session; the 9 `FileReportTargetAsync` tests and the 1 empty-id guard test verify via a fresh read session) | the test construction stays faithful to the new seam | the service's self-session commits before the test's read session opens, so the assertions observe the committed rows (the old caller-session "staged, not yet committed" shape no longer applies) |

**Boundary: unchanged (465).** The seam-reshape mints **no** new edge — the
service already references every type the controller's old read/write touched
(`Report`, `AccessAudit`, `INotificationEmitter`), so moving the lane in does
not add a `Moderation→*` or `Web→*` reference. The `boundary_violations` line is
the measured 465 (unchanged).

**The 17 honest keeps (the entry stays; the reason is refreshed where it had
gone stale).** Each is genuinely hard — retiring it would breach the `cs_file`
600 ceiling, mint a boundary edge, mint a ceremony type, or fight a frozen seam:

| Controller | Owning surface (measured, this tree) | The honest keep reason |
|---|---|---|
| `AnnouncementController` | `AnnouncementService` 518 | caller-owns-transaction write lanes (~152 code lines) → 518 + 152 > 600, **no headroom**; reshaping the frozen `IAnnouncementService` seam to open its own sessions is a larger seam change than this unit |
| `PostsController` | `PostService` 582 | caller-owns-transaction post/reply write lanes (~98+ code lines) → 582 + lanes > 600, **no headroom** (the C3-04 `PostService.Media.cs` split already retired the media lane; the remainder is the seam-reshape) |
| `GuardianController` | `GuardianService` 554 | the 4 `GuardianLink` read helpers (the **standing gate** used by 8+ actions) are read-only but join the UserInfo module (`Profile`) + the Guardian module; 554 + ~50 > 600, **no headroom** (the C3-04 split already landed `GuardianService.Approvals.cs`) |
| `TagController` | `TagService` 528 | the translation-write lane is caller-owns-transaction (passes the session to `AddTagTranslationAsync`), **and** the `SeedTranslationFormAsync` read is register-pinned ("no new seam" — the U8c register pin forbids an `ITagService` "get translations" method) |
| `PageController` | `PageService` **851** | **still a god-file** over the `cs_file` 600 ceiling — moving the lanes in would still raise the ratchet; the entry retires only when `PageService` drops under 600 (a later C3 unit) |
| `AdminController` | no Admin module (reads) | **reason refreshed** — the 2 store ops are **reads** (the admin audit-log list `Query<AccessAudit>` paged + the break-glass candidate list `Query<AdminOverride>`), not the "multi-document writes" the prior text claimed. Both docs are **Authorization-module** concrete types (`AccessAudit`, `AdminOverride`); there is no Admin module / no `IAdminService`, and minting one to carry two single-module admin view-model reads is a ceremony type |
| `GroupsController` | `IGroupService` (exists) | **reason refreshed** — `IGroupService` now **exists** (C07, ADR 0286, 25 methods; the prior "no IGroupService" text was stale). The group create/edit/leave + membership-write lanes (19 store sessions) are a `Group` + Translations + membership aggregate the seam does not own (its 25 methods are the description/translation/membership-read lanes); moving the write lanes in needs a `IGroupService` write-seam reshape or a ceremony `GroupWriteService` |
| `AttachmentController` | Posts + Media 2-module | the attach/retrieve/delete lanes span the Posts module (the parent `Post` read) + the Media module (the `MediaObject` quarantine write) — a 2-module aggregate no single service owns |
| `ContentImageController` | Posts + Media 2-module | same shape as `AttachmentController` — a 2-module aggregate no single owning service owns |
| `AdminAnalyticsController` | no Usage write-service | the CSV export writes **one** `AccessAudit` row (an **Authorization**-module concrete type). Routing it through the Usage-module `UsageAnalyticsService` (117 code lines, ample headroom) would **mint a new `Usage→Authorization:AccessAudit` edge** (verified absent from the 465 baseline); keeping it on the store names that edge |
| `DocumentController` | `DocumentService` 193 | **edge-minting** (a headroom keep is *not* the block — the service has ample headroom). The tag-slug read (`Query<Tag>`) + the Media quarantine write (`MediaObject.Quarantined`) would **mint new `Documents→Tags:Tag` + `Documents→Media:MediaObject` edges** (verified absent; `DocumentService` references neither concrete type) |
| `DirectoryController` | needs `HttpContext.Request` | the `ResolveTagDisplayNamesAsync` read resolves the profile's `TagIds` to `Tag` + `TagTranslation`, but the display-name resolution needs the **viewer's effective language from `HttpContext.Request`** (a Web-layer request context) — moving it to a Core service would require reshaping the seam to pass the language in |
| `BoardsController` | C08 frozen surface | the board lanes read `Board` + `BoardItemPlacement` + membership — C08's frozen cluster; the builder extraction was deferred as C08's exit |
| `TodosController` | C08 frozen surface | the todo lanes read `Todo` + `BoardItemPlacement` + membership — C08's frozen cluster; the builder extraction was deferred as C08's exit |
| `ProjectsController` | C08 frozen surface | the `BoardDetail`/`TodoDetail` builders + the `ProjectsBase` store reads are C08's frozen cluster; the builder extraction was deferred as C08's exit |
| `ProjectsBaseController` | C08 frozen surface | the shared base's tag-read + project-list reads are C08's frozen cluster; the builder extraction was deferred as C08's exit |
| `HealthController` | pure diagnostics | the only store op is the readiness connectivity probe (`Storage.Database.AssertConnectivityAsync`) — the plan's named diagnostics keep; there is no owning service to route a health probe through (minting an `IHealthService` is the ceremony type the plan names as the anti-pattern) |

**The DoD #6 `≤ 5` line is not met — the honest floor is 17.** This is the
honest-finding, not a failure: the remainder is precisely the no-service /
edge-minting / diagnostics / C08-frozen class the plan expected to be hard, and
each of the 17 would force a ceiling breach, a minted edge, a ceremony type, or
a fight with a frozen seam if retired. The plan's own words: *"If the honest
outcome is 8 or 9 (not 5) because the remainder is genuinely the no-service /
edge-minting / diagnostics / C08-frozen class, that is a correct honest-finding,
not a failure."* C3-08 the close decides the six-member flip on the true number.

## Consequences

- **The allowlist `18 → 17`.** One entry retires (`ReportTargetsController`);
  the gate detects the file as no longer on Marten (the pure-text scan finds
  none of the three tokens), so the stale-entry check passes and the
  "controller on Marten without an entry" check stays green.
- **No baseline line is raised.** `files` 8 · `classes` 3 · `interfaces` 8 ·
  `methods` 89 · `boundary_violations` 465 are all unchanged (the seam-reshape
  mints no edge; `IModerationService` is under the interfaces ceiling). This
  ADR names no raise because there is no raise.
- **Two keep reasons refreshed** (`AdminController` — reads not writes;
  `GroupsController` — `IGroupService` exists) so the baseline's reasons are
  true at this tree, matching the C3-05 precedent of correcting stale reason
  text.
- **The six-member flip is still not landed.** DoD #6's `≤ 5` target is not
  met (17 > 5); the flip lands only when every carried-in DoD line holds at the
  close (C3-08). This ADR records the intermediate, honest position: the clean
  remainder is retired, the rest is named and handed forward.
