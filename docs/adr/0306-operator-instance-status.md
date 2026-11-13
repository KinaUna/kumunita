---
id: adr-0306
title: "The operator's instance status: a read-only /admin/status page (version, uptime, /health checks, dead letters, mail alarms, storage, config warnings — no secret values) + a dashboard notice + an inbox alarm"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0136-platform-storage-limit.md
  - docs/adr/0156-storage-metrics-history.md
  - docs/adr/0162-analytics-history.md
  - docs/adr/0299-instance-export-operator-gated.md
  - docs/archive/lanes/improve-ux/plan-improve-ux.md
supersedes: []
superseded_by: []
lane: improve-ux
updated: 2026-11-13
---

# ADR 0306 — The operator's instance status: a read-only /admin/status page + a dashboard notice + an inbox alarm

Status: Accepted
Date: 2026-11-13
Builds on: [0136](0136-platform-storage-limit.md) (the platform storage
limit — the storage-metrics source the status page renders),
[0156](0156-storage-metrics-history.md) (storage-metrics history — the
metrics seam the page reuses), [0162](0162-analytics-history.md)
(the `/admin` trend-view precedent — the operator-admin plane convention
this page follows), [0299](0299-instance-export-operator-gated.md)
(the instance export is operator-gated — the GlobalAdmin-only
operator-plane standing the page's gate reuses). Amends: nothing — the
`/health` checks, the mail-alarm lane and the storage-metrics seams are
all **reused, unchanged**; this ADR adds one read seam above them.

> **Abstract:** IMPROVE-UX finding **E10** — the operator learns about
> trouble **from logs and email**, and the email may be what failed
> (SMTP down); first-boot config mistakes (`SeedAdmin__*` left set) are
> invisible until someone reads the docs. This ADR records decision
> **D8** of
> [plan-improve-ux](../archive/lanes/improve-ux/plan-improve-ux.md):
> **`/admin/status`** — a read-only GlobalAdmin view (version + uptime,
> the `/health` checks, dead-letter count, mail-alarm state, storage
> metrics, and a `ConfigWarningsPolicy` such as "SeedAdmin still set after
> an admin exists") that returns **no secret values** — plus a
> one-line dashboard notice when any row is red, and mail alarms also
> delivered to the GlobalAdmins' **inbox**. **Who is it for:** the
> platform operator, and the developer adding a status row or a config
> warning. **When to read the rest:** before touching the mail-alarm or
> `/health` lanes, or reviewing the operator surface.

## Context

The platform's operator surface before this decision was: `/health`
(a machine probe), the mail-alarm lane (alerts **by email only**, via the
SMTP relay — the very channel that may be what failed), and the runbooks
(first-boot: set `SeedAdmin__*`, read the logs, remember to remove the env).
IMPROVE-UX E10 names the dead end: when an operator's SMTP is down, the
alarm channel is the broken channel — the operator learns about it from
nothing in-app. E11 adds the first-boot case: `SeedAdmin__*` left set is a
real risk, but nothing warns about it until an audit finds it. E12 shows
the consequence — the runbook is stale precisely because there was no
in-app surface to keep it honest.

What already exists and is kept: the `/health` checks (DB reachability,
SMTP relay handshake, the version/build field), the dead-letter counter,
the mail-alarm T4 threshold policy, the storage-metrics seam (ADR
[0136](0136-platform-storage-limit.md),
[0156](0156-storage-metrics-history.md)), and the operator-plane
convention of GlobalAdmin-gated, read-only admin pages (the
`AdminStorageMetricsController` shape, ADR [0162](0162-analytics-history.md),
[0299](0299-instance-export-operator-gated.md)). What is **missing**: one
composed, human-readable read of all of it, and an in-app alarm channel.

The forces:

- **The alarm channel must not be the failed channel.** If the alert is
  email-only, an SMTP failure is invisible in-app.
- **A read must not leak.** The status read touches credentials
  (`SeedAdmin__*`, `Health__Token`) and key paths; the surface must
  report their *posture*, never their values.
- **Reuse, don't re-derive.** The checks already exist as seams; a second
  copy of the logic would drift (E12's failure mode).

## Decision

- **D1 — One Core read seam, a pure POCO, no secrets.**
  `IInstanceStatusService.GetStatusAsync` (the
  `Kumunita.Core/InstanceStatus` module) composes the `InstanceStatus`
  record from the **reused** seams, each called at most once: DB
  reachability, the SMTP relay handshake, the dead-letter count, the
  mail-alarm T4 state, the storage snapshot (ADR
  [0136](0136-platform-storage-limit.md) /
  [0156](0156-storage-metrics-history.md)), the GlobalAdmin count,
  version + build SHA + uptime, and the config warnings. The result is a
  **pure POCO** (no `IActionResult`, no `IHttpContext` — ADR
  [0006](0006-module-boundary-contracts.md) §D) and **never carries a
  secret value**: no credential, no token, no key path, no connection
  string. A failing sub-read **degrades that row** to an
  "unavailable" fact; the read still returns the rows that are available
  (the `/health` degraded-probe posture).

- **D2 — The `ConfigWarningsPolicy`: posture, not values.** A pure,
  unit-tested policy over a **boolean** input snapshot (the input carries
  only booleans — `SeedAdminCredentialsSet`, `AdminExists`,
  `SampleDataEnabled`, `IsProduction`, `HealthTokenSet`,
  `DataProtectionKeysPersisted`). The shipped rules:
  `SeedAdminStillSet` (fires iff `SeedAdmin__*` is set **and** an admin
  already exists — the first-boot env the operator forgot to remove, E11),
  `SampleDataInProduction` (sample data enabled in `Production`),
  `HealthTokenUnset` (the `/health` probe has no token), and
  `DataProtectionNotPersisted` (keys not persisted — session/auth break
  on container rebuild). Each warning is a stable **key** + a
  **secret-free reason** constant; the policy never sees the credential
  values, only their set/unset posture (pinned by
  `ConfigWarningsPolicyTests`).

- **D3 — The `/admin/status` page + the two notice channels.**
  `AdminStatusController` (`GET /admin/status`, GlobalAdmin-gated, a thin
  wrapper over the one O01 read — **read = no audit row**, the
  `AdminStorageMetricsController` precedent) renders the POCO's rows as-is.
  A **dashboard notice** (`_LayoutNotices`, admins only) appears when any
  status row is red, linking to `/admin/status`. And the mail-alarm lane
  also **emits an `ops.alarm` inbox notification** to the GlobalAdmins
  (`MailAlarmsHandler`, link `/admin/status`) — so an alarm reaches the
  operator **in-app**, not only by the possibly-broken email.

- **D4 — Rejected alternatives.** (a) *Config warnings only* — no health
  rows, no alarm channel; the operator still learns from logs; rejected on
  E10. (b) *Leave it to the deployment manager* (the provisioning
  lane) — the platform ships self-hosted with no manager by default;
  rejected on the closed-loop test (the operator's need must close
  in-platform).

## Consequences

- A **new Core module** (`Kumunita.Core/InstanceStatus`:
  `IInstanceStatusService`, `InstanceStatusService`,
  `InstanceStatus`, `ConfigWarning(s)`, `ConfigWarningsPolicy`,
  `IMailAlarmStateReader`, `InstanceStatusOptions`) + a **new Web
  controller** (`AdminStatusController`) + the dashboard notice + the
  `ops.alarm` inbox emit join the platform; **every underlying seam is
  reused unchanged** (the mail-alarm lane gains one side-effect, the inbox
  row, on top of its existing email send).
- **Read-only, no secrets**: the page carries zero write actions, zero
  audit rows, and zero credential values — the no-secret invariant is
  pinned by the policy's unit tests and the POCO's shape.
- **Resilient**: a downed DB or SMTP degrades the affected row, not the
  page — the operator always sees the rows that work, including the
  "SMTP unavailable" row that *is* the alarm.
- **Tests**: `ConfigWarningsPolicyTests` (the four rules + the no-secret
  posture), `AdminStatusControllerTests` (the render, the gate),
  `O03AdminStatusNoticeTests` (the red-row notice) and the O04
  `ops.alarm` emit tests.
- **Delivery:** O01 (the Core seam + policy), O02 (the page), O03 (the
  notice) and O04 (the inbox alarm) shipped on `development` ahead of this
  ADR; journey J11 (first boot to healthy) was walked live by the X02 cold
  read.
