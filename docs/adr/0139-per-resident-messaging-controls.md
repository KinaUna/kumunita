---
id: adr-0139
title: "Per-resident messaging controls (opt-in + guardian's ceiling on the M9 lane)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: []
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR 0139 — Per-resident messaging controls (opt-in + guardian's ceiling on the M9 lane)

Status: Accepted | Date: 2026-10-04

> **Abstract:** M9's messaging toggle is **instance-level**; this ADR adds the
> **per-resident** half — the resident's own opt-in and a guardian's ceiling
> over a child — as a *read* seam + two additive `Profile` fields, extending
> no frozen seam. **For** a developer touching `IMessagingService`; read the
> rest before changing the gate's fail-closed composition or the opt-in /
> ceiling write lanes.

## Context

M9 (ADR 0105) ships an **instance-level** messaging toggle + the 1:1
conversation / message seams; the ADR 0028 GU surface ships the guardian's
standing over a supervised child. What neither has is the **per-resident**
control — a resident's opt-out and a guardian's ceiling. **Compose the frozen
seams, never extend them** — the per-actor gate is a *read* seam on top of the
master gate; both new controls are additive `Profile` fields; the instance
toggle is the master gate, enforced at the Web boundary, never a new
authorization surface. **Composition rule:** master gate off ⇒ **no**; opt-in
off ⇒ **no**; ceiling on ⇒ **no** (ceiling wins); else ⇒ **yes**.

## Decision

- **D1 — The per-actor gate is a *read* seam, not a write seam.**
  `IsMessagingAllowedForAsync(actorId)` is a read-only composite of the
  instance master gate, `Profile.MessagingOptIn`, and
  `Profile.MessagingRestricted` (via the frozen `GetProfileAsync` read); a
  missing profile **fails closed**; no new `AccessAction` /
  `IAuthorizationService` call (a read, not an access decision).
- **D2 — The resident's opt-in is an additive `Profile` field + a
  single-write owner-scope lane.** `Profile.MessagingOptIn` (bool, default
  `false`, **zero migrations**); the write lane
  (`SetMessagingOptInAsync`) is the `CompleteOnboardingAsync` shape — one
  `SaveChangesAsync`, **no `AccessAudit` row**.
- **D3 — The opt-in surface is a new `/settings/messaging` tab** (the ADR 0080
  settings-surface-split idiom): a fresh `MessagingSettingsController` (not a
  `LocaleController` extension — its constructor is pinned by tests), with
  `GET`/`POST` + one `_SettingsTabs` row.
- **D4 — The per-actor gate is enforced at the *Web boundary*, not the
  service.** The `MessagesController` actions + the account nav read
  `IsMessagingAllowedForAsync` alongside the **preserved** instance-toggle
  read; the service keeps its `EnsureEnabledAsync` backstop, gains **no**
  per-actor gate inside it (the C-M9·2 pin).
- **D5 — The guardian's ceiling is an additive `Profile` field + a
  single-write guardian-scope lane.** `Profile.MessagingRestricted` (bool,
  default `false`) + `SetChildMessagingRestrictionAsync` (the
  `SuspendChildAsync` shape — `GuardActiveLinkAsync` gate, flag write, one
  `guardian.messaging_restrict` audit row; a **state** (one audit verb, both
  directions), not a decision.
- **D6 — The ceiling surface is two buttons on the existing
  `/me/children/{childId}` curation view** (the Suspend / Unsuspend idiom).
- **D7 — The instance-off + guardian-restricted notices are *presentation*, not
  *enforcement*.** The `/settings/messaging` page renders even when the
  instance is off; when the ceiling is on the checkbox is `disabled` and the
  POST is refused as a **user-presentable error** (a 404 would leak "you're
  restricted" — the ADR 0028 G·3 pin).

## Consequences

- **Frozen-surface, additive:** the ADR 0105 service contract, the ADR 0028
  guardian standing, and the frozen `IMessagingService` are **byte-identical**.
- **Two additive `Profile` fields, zero migrations:** a fresh instance has both at the `false` floor.
- **One new controller, one new settings tab, no new `*DocTypes` surface** —
  the only new Web surface; the composition is **fail-closed** (floor = "no
  messaging") and the ceiling always wins over the child's own opt-in.
