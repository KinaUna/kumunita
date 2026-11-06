---
id: adr-0294
title: "IMPROVE-CODE-3 C3-04 — the still-large-file tail: 15 partial-class splits, 31 boundary-edge raises"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0290-improve-code-2-baseline-raises-and-seams.md
  - docs/adr/0293-improve-code-3-c3-03-god-service-seam-boundary-edge-raises.md
  - docs/archive/lanes/improve-code/plan-improve-code-3.md
  - docs/philosophy/in-code.md
supersedes: []
superseded_by: []
updated: 2026-11-06
---

# ADR 0294 — IMPROVE-CODE-3 C3-04: the 31 boundary-edge raises from partial-class splits

Status: **Proposed** (this ADR is the `adr:` named in `tools/code/code-baseline.json`
boundary_violations entries that were raised in C3-04. The gate's *"a `count`
raised above the base without an `adr:` fails"* rule is satisfied by the named
entries.)

> **Abstract:** the named, defensible exception record for the 31 boundary
> edge count raises caused by C3-04's partial-class splits. Each partial
> file (e.g. `PostService.Media.cs`, `GuardianService.Approvals.cs`) lands on
> an **already-existing** module edge (the god-service already crossed that
> edge before the split). The edge's file count rises because the split minted
> a new file on the same legitimate edge — the C3-01 `Pages→Identity:Roles`
> 2→3 / C3-03 `Posts→*` split-raise precedent. **0 new edges**, **0 silent
> raises**. **For** a reviewer of C3-04's close. **When to read the rest:**
> before any future split that touches these edges, or when a gate failure on
> one of these entries is the starting point.

## Context

C3-04 (the still-large-file tail, DoD #3/#4 target `files 23 → ≤ 8`) split
15 files over the 600 `cs_file` ceiling into partial-class files (5
controllers + 10 services). Each partial file references the same cross-module
types the god-service already referenced. The boundary metric counts **files
per edge**, so a new partial file on an existing edge raises that edge's count
by 1.

This is the exact same pattern as C3-01 (`PageStandingPolicy.cs` added a
third file to the already-existing `Pages→Identity:Roles` edge) and C3-03
(each of the 5 god-service seams landed on already-existing edges, raising
12 counts). The edge **set** is unchanged; the **count** is a file-count
reflection of the real new file on a sanctioned edge.

## Decision

The 31 boundary_violations entries raised in C3-04, each carrying
`"adr": "0294"` in `code-baseline.json`:

| Edge (from → to : symbol) | Before → Now | Reason |
|---|---|---|
| `Announcements→Localization:LocaleSettings` | 1 → 2 | `AnnouncementService.Comments.cs` reads `LocaleSettings` |
| `Events→Authorization:AccessAuditFactory` | 1 → 2 | `EventService.Group.cs` writes `AccessAudit` |
| `Events→Authorization:Audience` | 5 → 6 | `EventService.Group.cs` reads `Audience` |
| `Events→Query:PageSizer` | 1 → 2 | `EventService.Group.cs` reads `PageSizer` |
| `Identity→Authorization:AccessAudit` | 2 → 3 | `IdentityService.AccountOps.cs` writes `AccessAudit` |
| `Identity→UserInfo:ModeratorAssignment` | 1 → 2 | `IdentityService.AccountOps.cs` reads `ModeratorAssignment` |
| `Identity→UserInfo:Profile` | 2 → 3 | `IdentityService.AccountOps.cs` reads `Profile` |
| `Moderation→Posts:Post` | 1 → 2 | `ModerationService.ReportIntake.cs` reads `Post` |
| `Moderation→Posts:PostReply` | 1 → 2 | `ModerationService.ReportIntake.cs` reads `PostReply` |
| `Moderation→Posts:PostToAuditableResource` | 1 → 2 | `ModerationService.ReportIntake.cs` reads `PostToAuditableResource` |
| `Moderation→Posts:Report` | 1 → 2 | `ModerationService.ReportIntake.cs` reads `Report` |
| `Moderation→UserInfo:ModeratorAssignment` | 1 → 2 | `ModerationService.ReportIntake.cs` reads `ModeratorAssignment` |
| `Portability→UserInfo:GroupMembership` | 2 → 3 | `UserPortabilityService.Standing.cs` reads `GroupMembership` |
| `Posts→Web:AttachmentIds` | 6 → 7 | `PostService.Media.cs` reads `AttachmentIds` |
| `Projects→Authorization:AccessAuditFactory` | 5 → 7 | `BoardService.Lanes.cs` + `TodoService.Lifecycle.cs` write `AccessAudit` |
| `Projects→Authorization:Audience` | 12 → 14 | `BoardService.Lanes.cs` + `TodoService.Lifecycle.cs` read `Audience` |
| `Projects→Authorization:StandingMatrix` | 4 → 6 | `BoardService.Lanes.cs` + `TodoService.Lifecycle.cs` read `StandingMatrix` |
| `Projects→Web:TagSlugs` | 3 → 4 | `TodoService.Lifecycle.cs` reads `TagSlugs` |
| `Tags→Documents:Document` | 2 → 3 | `TagService.Listing.cs` reads `Document` |
| `Tags→Identity:Roles` | 1 → 2 | `TagService.Suggest.cs` reads `Roles` |
| `Tags→Pages:Page` | 3 → 5 | `TagService.Listing.cs` + `TagService.Suggest.cs` read `Page` |
| `Tags→Posts:Post` | 3 → 5 | `TagService.Listing.cs` + `TagService.Suggest.cs` read `Post` |
| `Tags→Projects:KanbanBoard` | 2 → 3 | `TagService.Listing.cs` reads `KanbanBoard` |
| `Tags→Projects:TodoItem` | 2 → 3 | `TagService.Listing.cs` reads `TodoItem` |
| `UserInfo→Authorization:AccessAudit` | 5 → 7 | `GroupService.Invitations.cs` + `GuardianService.Approvals.cs` write `AccessAudit` |
| `Web→Authorization:Audience` | 20 → 21 | `BoardsController.Translations.cs` reads `Audience` |
| `Web→Identity:Roles` | 37 → 39 | `GuardianController.RsvpCurate.cs` + `BoardsController.Translations.cs` read `Roles` |
| `Web→Identity:User` | 75 → 81 | `AnnouncementController.Translations.cs` + `GuardianController.RsvpCurate.cs` + `AdminController.Communities.cs` + `BoardsController.Lanes.cs` + `PageController.Translations.cs` read `User` |
| `Web→Messaging:Message` | 22 → 25 | `GuardianController.RsvpCurate.cs` + `BoardsController.Lanes.cs` + `AdminController.Communities.cs` read `Message` |
| `Web→Pages:Page` | 17 → 19 | `AdminController.Communities.cs` + `PageController.Translations.cs` read `Page` |
| `Web→Pages:PageToAuditableResource` | 3 → 4 | `PageController.Translations.cs` reads `PageToAuditableResource` |

**Count check:** 31 edges, each with a named owner ADR. **0 new edges** (the
edge set is unchanged from the C3-03 close).

## Consequences

- **Positive:** the gate's *"a raise without an `adr:` fails"* rule is
  satisfied. The 31 are no longer silent. The 15 file baselines retired
  (files 23 → 8) are the one real reduction of this unit.
- **Negative:** 31 boundary edges carry `adr: 0294` as a *named exception*,
  not a *shrinking*. A future unit that consolidates the partial files (or
  adds a new partial file on the same edge) must account for these counts.
