---
id: design-notifications-subsystem
title: "Notifications subsystem (M6 + quiet times)"
tier: reference
kind: design
status: current
audiences: [developer, agent]
related:
  - docs/adr/0061-email-and-notification-language.md
  - docs/adr/0121-notification-quiet-times.md
  - docs/design/m6-notifications-design.md
  - docs/design/m20-notification-quiet-times-design.md
updated: 2026-11-02
---

> **Abstract:** the **one entry point into the notifications subsystem** — the
> capability chain built on the frozen **M6** notification lane: the base is the
> `Notification` inbox row (authoritative, always delivered in-app) + the
> **durable email half** (staged, flushed by a tick), and the **quiet-times
> extension (M20)** adds a per-resident time dimension on the email half only.
> **What this is:** the map from the base (M6) to the one extension that rides
> on it, with each layer's ADR, design doc, and archived lane. **Who is it for:**
> a developer touching *any* of the notification or email-delivery surfaces; an
> agent that needs the inbox-vs-email boundary. **When to read the rest:** before
> touching `NotificationService`, the email staging/flush tick, the
> `NotificationQuietSchedule` doc, or the deferral evaluator.

# Notifications subsystem

The notification surface is a **capability chain**: M6 is the base (the
`Notification` inbox row + the durable email half); M20 is the *extension on
top* — it adds a per-resident quiet schedule that defers the **email half only**
(the inbox row is never deferred). This hub is the single place that names the
chain; each row links the layer's own design doc (the current truth), its ADR,
and its archived lane.

| Layer | What it adds | Design doc (current truth) | ADR | Archived lane |
|---|---|---|---|---|
| **M6 — base** | the `Notification` inbox row (authoritative) + the **durable email half** (staged, flushed by a tick); the `NotificationKinds` closed set; the idempotency-key table | [`m6-notifications-design.md`](m6-notifications-design.md) | [ADR 0061](../adr/0061-email-and-notification-language.md) | [`archive/lanes/notifications/`](../archive/lanes/notifications/plan-m6-notifications.md) |
| **M20 — quiet times** | a **per-resident quiet schedule** (allowed/blocked hours + days) on the email half only, evaluated in the resident's own zone; one additive `bool` on `Notification` + one `NotificationQuietSchedule` doc | [`m20-notification-quiet-times-design.md`](m20-notification-quiet-times-design.md) | [ADR 0121](../adr/0121-notification-quiet-times.md) | [`archive/lanes/m20/`](../archive/lanes/m20/plan-m20-notification-quiet-times.md) |

**The one fact to keep straight:** M20 **defers the email, never the inbox
row** (C-M20·1), and the quiet verdict is a **pure function** of
(schedule, instant, effective zone) — not an authorization decision. So a change
to M6's inbox row is a change to both rows; a change to M20's deferral is local
to the email half.
