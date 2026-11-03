---
id: adr-0084
title: "Per-target notification subscriptions: opt-in/opt-out per (recipient, kind, target)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/design/m20-notification-quiet-times-design.md
supersedes: [adr-0076]
superseded_by: []
updated: 2026-11-02
---

# ADR [0084](0084-per-target-notification-subscriptions.md) — Per-target notification subscriptions: opt-in/opt-out per (recipient, kind, target)

Status: Accepted
Date: 2026-09-25
Amends **0076** (the M6 surface), **0078** (the frozen-surface-amendment precedent),
**0015** (the `kw-l` registry); the full emitter / gate / route detail and test list
live in the linked design doc.

> **Abstract:** M6's toggle was **kind-level** (one bit per kind, shared across
> every target). This ADR adds the **per-target** shape a kind-level toggle
> cannot express — "a new post *in the community I'm in*" — for the three lanes
> M6 left unwired. **Who is it for:** a developer touching `NotificationService`
> or `/notifications/subscriptions`. **When to read the rest:** before adding a
> per-target kind or reordering the emit gate.

## Context

M6 (ADR [0076](0076-notifications-inbox-and-recipient-email.md)) shipped a **shared awareness** arrow — a durable inbox, a best-effort
email nudge, a closed kind vocabulary, and a settings toggle that is **kind-level**
(one bit per kind, shared across every target). Four of the most common "hear about
*this specific thing*" moments have a **per-target** shape a kind-level toggle cannot
express, and in three of the four the content lane already exists — M6 left
per-target scoping as a follow-on. **This ADR executes that follow-on**: it completes
the wiring for the two unwired lanes (announcement, community post), re-gates group
posts per-target, and adds the never-wired page-child lane, plus the settings-page
list and the per-page subscribe toggle. It is a **lane within the M6 surface** (the
ADR [0083](0083-group-add-and-invite-notifications.md) / 0077 shape); no roadmap letter moves.

## Decision

- **D1 — Three new wired kinds (closed set 13 → 16):** `announcement`, `community.post`,
  `page.child`. Every shape that reads `Known` (the toggle list, the `kw-l` table,
  the count test) follows.
- **D2 — The emitters sit on the content-lane write seams**, in the caller's
  transaction (C3 single-commit invariant), recipient = the affected resident:
  `announcement` → the community's members (or every verified resident for flat/public,
  D4); `community.post` → the community's members, excluding the author; `page.child`
  → the parent's subscribers, excluding the author.
- **D3 — A 7-arg `EmitAsync` overload + a gate consulted *first*.** The 6-arg
  signature is **unchanged** (ADR [0078](0078-sample-account-notification-suppression-in-production.md)); with `targetId` set the recipient's effective
  choice is consulted **before** dedup / profile / sample-suppression and a `false`
  returns `null`; with it null (legacy emitters) the gate is skipped, so the 13
  pre-0084 emitters are byte-for-byte unchanged.
- **D4 — A `"announcements"` sentinel** (distinct from any `Component.Id`) is the
  flat/public-announcement target.
- **D5 — Opt-in / opt-out is code-owned:** `OptInKinds = { Announcement, PageChild }`
  are **opt-IN** (broad universe); `community.post` + the re-gated `group.post` are **opt-OUT**.
- **D6 — A new `NotificationSubscription` doc**, key `(RecipientId, Kind, TargetId)`,
  **upsert-only, never deleted**; a row's `Enabled` is the last explicit choice (a fresh
  install falls back to the kind's default).
- **D7 — The settings list** (`GET` + `POST /notifications/subscriptions`) surfaces
  exactly the subscribable (kind, target) pairs; the POST upserts and **rejects (400)**
  a kind outside the four or an empty target.
- **D8 — The per-page toggle** (`POST /pages/{id}/subscribe` +
  `PageShowViewModel.Subscribed`) flips the resident's `page.child` row.

## Consequences

- Four "hear about this specific thing" moments become expressible — a resident in
  five communities can opt out of three without losing the rest.
- **Frozen-surface, additive:** the 6-arg `EmitAsync`, the return type, and the 13
  legacy emitters are unchanged; the gate consults first so a disabled recipient leaves no dedup row.
- The preference lane ("email for this kind at all") and the subscription lane
  ("anything for this (kind, target)") are **distinct**; both reads + the write are
  **personal — no `AccessAudit` row** (D7's 400 gate keeps a client from minting a
  subscription the emitters don't consult).
