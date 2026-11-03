---
id: adr-0286
title: "IUserInfoService back to the access core: four sibling interfaces for groups, community, guardian, and profile settings"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0006-module-boundary-contracts.md
  - docs/plans-milestones/plan-improve-code.md
supersedes: []
superseded_by: []
updated: 2026-11-03
---

# ADR [0286](0286-iuserservice-access-core-split.md) — IUserInfoService back to the access core

Status: Accepted
Date: 2026-11-03
Amends: ADR [0006](0006-module-boundary-contracts.md) (the UserInfoModule public surface)

> **Abstract:** `IUserInfoService` grew from 8 methods (the ADR 0006 freeze)
> to 86 by M38. This ADR returns it to the access-core surface (15 methods)
> and splits the rest into four sibling interfaces: `IGroupService`,
> `ICommunityMembershipService`, `IGuardianService`,
> `IProfileSettingsService`. **For** a developer touching the UserInfo
> module. **When to read the rest:** before adding a method to the surface
> or migrating a caller.

## Context

ADR [0006](0006-module-boundary-contracts.md) froze `IUserInfoService` at 8 methods. Since then, 78
methods were added as "ADR 0006-E compatible additions" — group management
(ADR [0007](0007-group-management-lane-owner-or-globaladmin.md) / 0009 / 0010 / [0093](0093-group-delete-lane.md)), community membership
(ADR [0012](0012-community-membership-mandatory-and-moderator.md)), guardian links (ADR [0028](0028-guardian-controls-account-scope-supervision.md) / 0038 / [0151](0151-guardian-time-limits.md)), profile
settings (ADR [0019](0019-timezone-platform-default-and-resident-override.md) / 0020 / [0169](0169-first-day-of-week.md) / [0149](0149-home-intro-hide-preference.md)), translations
(ADR [0026](0026-group-community-name-description-translations.md)). The interface now has 86 methods;
`UserInfoService` is 4 287 code lines. The IMPROVE-CODE plan (C07) requires
`IUserInfoService` ≤ 20 methods.

## Decision

- **`IUserInfoService` returns to the access core (15 methods):** the 8
  frozen methods (profile read, group membership, delegation grant) plus the
  community-id read/write lanes that feature modules use for access
  decisions (`GetCommunityIdsAsync`, `GetEffectiveCommunityIdsAsync`, the
  community membership / mandatory lanes).
- **Four sibling interfaces**, each satisfying the part-vs-whole test
  (≥ 2 callers or owns a route family / aggregate):
  - **`IGroupService`** — group CRUD, descriptions, privacy, invitations,
    join requests, group / community translations. Callers:
    `GroupsController`, `NotificationsController`, `ModerationController`.
  - **`ICommunityMembershipService`** — community CRUD, member lanes,
    community translations, membership-request resolve. Callers:
    `CommunityController`, `AdminController`, `GuardianController`.
  - **`IGuardianService`** — guardian link lifecycle, suspend/unsuspend,
    messaging / RSVP / community-block restrictions, time limits. Callers:
    `GuardianController`, `TimeLimitMiddleware`, `BlockedAccountMiddleware`.
  - **`IProfileSettingsService`** — profile settings (timezone, page size,
    first-day-of-week, home prefs, date format, email language), onboarding,
    messaging opt-in, directory reads, component / assignment reads.
    Callers: `ProfileController`, `AccountController`, `DirectoryController`,
    `OnboardingController`, `AdminController`.
- **Migration story.** Each caller switches its DI registration from
  `IUserInfoService` to the specific sibling interface it uses. The
  implementation moves verbatim cluster by cluster (C07). **Audit rows are
  unchanged**: the `Via` values (`Owner`, `Admin`, `Guardian`,
  `Delegation`, `BreakGlass`, `Moderator`) do not move — they are recorded
  by the `AuthorizationService`, not by the service being split. No method
  is renamed; the split is a move, not a rename.

## Consequences

- **Positive:** each sibling has a clear caller set and can be unit-tested
  in isolation; the access-core surface is small and stable again.
- **Negative:** four more DI registrations; `UserInfoService` splits into
  four implementation classes (C07); shared private helpers go to an
  internal `UserInfoStore`.
- **Note:** `IGroupService` (~26 methods) exceeds the D2 interface ceiling
  (20); C07 baselines it and shrinks it in subsequent commits.
