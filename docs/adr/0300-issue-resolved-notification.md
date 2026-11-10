---
id: adr-0300
title: "The resolved platform-issue loop closes — issue.resolved kind + the anonymous-filer hold"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/adr/0154-production-error-handling.md
  - docs/adr/0076-notifications-inbox-and-recipient-email.md
  - docs/adr/0005-multilingual-support.md
supersedes: []
superseded_by: []
updated: 2026-11-10
---

# ADR 0300 — The resolved platform-issue loop closes

## Context

IMPROVE-UX finding **E5** (severity M): a *resolved* platform-issue
(`ErrorReport`, ADR 0154) came back to its filer as **nothing**. The E5
finding: "`ResolutionNote` stored, never shown or sent; contact email
'never mailed'." The content-report loop was closed by IMPROVE-UX R07
(the `report.resolved` body + `linkPath`), but the **platform-issue** loop
stayed open — `ErrorReportService.MarkResolvedAsync` stamped the doc + one
audit row and sent nothing (the closed-loop ✗ / handoff ✗ test failure for
the "Report an issue → hear back" journey).

The platform's notification vocabulary is a **closed, code-owned set**
(ADR 0076 D2 — `NotificationKinds.Known`), so a new kind is **not
additive**: it re-baselines the closed-set count pin
(`NotificationsControllerTests` `31 → 32`) and needs a
`notifications.kind.*` label in all four languages (the C02 / `KwL` parity
pins watch the registry).

## Decision — the `issue.resolved` kind (D1)

1. **One new kind, `issue.resolved`.** A resolved `ErrorReport` notifies the
   **signed-in filer** (`ErrorReport.SubjectId`) with a short body (the
   `notifications.issue.resolved_body` sentence + the moderator's
   `ResolutionNote` verbatim when present) and a `linkPath` of
   `/reports/mine#i-{reportId}` (the R06 issue-row anchor `id="i-{Id}"`).
   The kind is resident-facing, opt-OUT (not in `OptInKinds`), grouped with
   the other report/issue kinds in `Known`.

2. **Anonymous filer = no emission (D2 — the held `// TODO ❓`).** A blank
   `SubjectId` has no recipient, so `MarkResolvedAsync` emits **nothing**.
   The filer's `ContactEmail` is **never mailed** — that is a held SEC
   decision ("never mailed" by design, ADR 0154), **not** made here. The
   decision to mail anonymous filers' contact email (and what it means for
   the privacy model) belongs to the SEC lane.

3. **Optional seams, graceful degrade.** The emission uses the existing
   `INotificationEmitter` `EmitAsync` overload that carries `linkPath` /
   `targetId`; the body localizes to the filer's `Profile.EmailLanguage`
   (the `IUserInfoService` + `ITranslationProvider` R07 precedent). Both
   seams are optional (null on a test harness) — the body degrades to the
   `en` floor, never throws, and an absent emitter skips the emission
   (the pre-R08 behaviour).

## Consequences

- The "Report an issue → hear back" journey row passes on a running instance
  (the signed-in filer gets an inbox row linking to their row on "My
  reports").
- Adding a kind re-baselines the closed-set count pin
  (`31 → 32`, annotated with this ADR) and requires the
  `notifications.kind.issue.resolved` label in all four languages.
- The anonymous-filer mailing path stays **out of scope** (a held SEC
  decision); the `// TODO ❓` marks it in the emission site so it is not
  silently assumed.
- `issue.triaged` (the R08 card's optional ❓) is **not** added — a later
  unit.
