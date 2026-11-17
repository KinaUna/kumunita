---
id: adr-0303
title: "A filed report reaches the moderators — the report.queued routing kind + the GlobalAdmin enumeration seam"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/archive/lanes/improve-ux/plan-improve-ux.md
  - docs/adr/0077-admin-account-signup-verify-notification.md
  - docs/adr/0076-notifications-inbox-and-recipient-email.md
  - docs/adr/0006-module-boundary-contracts.md
  - docs/adr/0003-roles-and-moderator-scoping.md
supersedes: []
superseded_by: []
updated: 2026-11-11
---

# ADR 0303 — A filed report reaches the moderators

## Context

IMPROVE-UX finding **E3** (severity M): a *new* report reached **no
moderator**. The `report.filed` emission (the 2026-09-25 disclosure-fix
shape) goes to the *content's author* — the person being reported — and
moderators only heard about a report after a GlobalAdmin unlocked + assigned
it. There was no "a report is pending" nudge on intake, and no queue badge.
The "Moderator: report filed in my community → act" journey row fails the
closed-loop test: the moderator has nothing to act *from* until a human
hands them the report.

The platform's notification vocabulary is a **closed, code-owned set**
(ADR 0076 D2 — `NotificationKinds.Known`), so a new kind is **not
additive**: it re-baselines the closed-set count pin
(`NotificationsControllerTests` `32 → 33`) and needs a
`notifications.kind.*` + `notifications.preference.*` label in all four
languages (the C02 / `KwL` parity pins watch the registry).

The admin-lane fan-out precedent is ADR 0077's
`IdentityService.EmitAccountNotificationAsync` (the `account.signup` /
`account.verified` emitters): resolve the GlobalAdmin set via
`userManager.GetUsersInRoleAsync(Roles.GlobalAdmin)`, loop, one inbox row per
admin with a per-admin idempotency key, best-effort (a notification-lane
failure never blocks the domain write). But `ModerationService` does **not**
hold `userManager` / `IIdentityService` — role enumeration is an **Identity**
concern, and `IUserInfoService` (the frozen profile/community seam, ADR 0006)
is the wrong home for it. A05 needs a seam it does not have.

## Decision

1. **One new kind, `report.queued`.** A **filed** report
   (`Report.Status = "filed"`) is routed to the component's **standing
   moderators** ∪ the **GlobalAdmin**s (the fallback recipients when a
   component has no standing moderator — the E3 gap), deduped. An
   **admin-lane** kind (the recipients are the moderators / GlobalAdmins,
   not the reporter — the ADR 0077 admin-lane precedent), in `Known`, opt-OUT
   default, grouped with the report cluster (`report.filed` /
   `report.assigned` / `report.resolved`).

2. **No target content in the body.** `body = null` — the localized
   `notification.report.queued.subject` is the row's text. No title, no body,
   no reporter's free-text reason (the 2026-09-25 disclosure-fix shape — the
   reporter's reason is a low-privilege, attacker-controlled string and is
   never forwarded). The `linkPath` carries the navigation.

3. **The `linkPath` is the scoped-read route.** `linkPath =
   /moderation/{reportId}` — the receiving moderator's `Read` decision on
   open **still applies** (thin token, fat authorization). A05 routes a
   nudge; it does **not** grant standing.

4. **One row per recipient, the reporter skipped, best-effort.** Per-recipient
   idempotency key `notification:report.queued:{reportId}:{recipientId}`
   (a re-filing of the same report does not re-emit — the ADR 0077 admin
   fan-out shape). A standing moderator who is also a GlobalAdmin gets one
   row, not two (the dedupe). The reporter is never routed a nudge about
   their own filing (the self-report skip guard, mirrored from the
   `report.filed`-to-author lane). A notification-lane failure is swallowed —
   the filing domain write still commits (the inbox row is the nudge, not
   the record). The `report.filed`-to-author emission is **unchanged**.

5. **The GlobalAdmin enumeration seam (the ADR 0006-E *compatible ADD*).**
   `IIdentityService.ListGlobalAdminsAsync()` — the
   `GetUsersInRoleAsync(Roles.GlobalAdmin)` body, `userId` projection only
   (no PII), a **read** (no audit row). This is a new method on the **owning
   module's** public surface (ADR 0006-E), named here — role enumeration
   lives with the module that owns the role source of truth
   (`UserManager<TUser>`). `IUserInfoService` is deliberately **untouched**
   (ADR 0006 frozen profile/community seam; role enumeration is not a
   profile concern — the Option B alternative, rejected).

6. **Optional wiring into `ModerationService`.** The
   `IIdentityService` is an **optional** constructor dependency (CS1736, the
   `_notifications` / `_userInfo` / `_posts` / `_translationProvider`
   precedent) so the existing positional test call sites keep compiling;
   production wiring (the DI factory) passes
   `sp.GetRequiredService<IIdentityService>()` (the Identity context is
   composed before moderation at the Core composition root). When null (a
   test harness that omits it) the GlobalAdmin half of the recipient set is
   empty — the standing-moderator half still routes.

7. **Standing moderators come from the same-transaction read.** The
   `report.assign` lane's shape — a direct `Query<ModeratorAssignment>()` on
   the caller's `IDocumentSession` (not `GetStandingModeratorsAsync`, which
   opens its own session + resolves display names the routing does not
   need). A `null` `ComponentId` → no standing set (the C-M3b·4 "no
   fabricated component" shape — only the GlobalAdmin fallback reaches it).

## Consequences

- The "Moderator: report filed in my community → act" journey row passes: a
  filed report produces one `report.queued` inbox row per standing moderator
  and per GlobalAdmin (deduped), each linking `/moderation/{reportId}`, and
  none for the reporter.
- Adding a kind re-baselines the closed-set count pin (`32 → 33`, annotated
  with this ADR) and requires the `notifications.kind.report.queued` +
  `notifications.preference.report.queued.label` labels in all four
  languages.
- The standing-moderator half of the routing can ship without the
  `IIdentityService` seam (the optional-dependency degrade) — but the
  "never ship only the standing half silently" rule (the E3 gap) is enforced
  by a test, not by the degrade: a filed report in a component with a
  standing moderator **and** a GlobalAdmin produces one row for each (deduped),
  and a report outside a component (null `ComponentId`) still reaches the
  GlobalAdmins.
- `IUserInfoService` is **not** widened (Option B rejected — the frozen
  profile/community seam stays; role enumeration is an Identity concern).
- The `report.filed`-to-author emission (the 2026-09-25 no-reason-snippet
  shape) is unchanged — the author still gets `report.filed`, never
  `report.queued`.
