---
id: adr-0144
title: "Guardian's event-attendance gate: the consent lane's missing promise made functional"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/guardian-event-rsvp-gate-design.md, docs/design/adr-handoffs.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR 0144 — Guardian's event-attendance gate: the consent lane's missing promise made functional

Status: Accepted | Date: 2026-10-05

> **Abstract:** the GU consent text binds a guardian to "review and approve or
> deny all group and event invitations," but the *event* half had no
> enforcement. This ADR adds a **7th GU supervisory action** — a
> `GuardianEventRequest` request-row + a `Profile.EventRsvpMode` posture gate
> on `EventService.RsvpAsync` (self-lane only) + the guardian's
> approve/deny/veto resolve lanes — reusing the GU standing gate exactly, with
> no new bounded context, no new `AccessAction`, no `IAuthorizationService`
> branch. **For** a developer touching `EventService.RsvpAsync`, the GU event
> resolve lanes, or the `EventDocTypes` / `Profile` shape. **Read the rest**
> before changing the posture default, the gate, or the audit verbs. Full
> code shapes + tables in the [handoff
> note](../design/adr-handoffs.md#adr-0144--guardian-event-attendance-gate-moved-detail).

## Context

ADR 0038's consent text binds a guardian to "review and approve or deny all
group and event invitations" before the child's account may exist. The
*group* half of that promise was functional the moment the GU lane landed
(ADR 0028, extended by ADR 0141's community-approval lane): a group invitation
on a supervised child is stored as a `Pending` `GroupInvitation` row, the
child's self-lane `AcceptGroupInvitationAsync` is refused, the active
guardian(s) get a `guardian.group_invite` notification, and
`ApproveGroupInvitationAsync` / `RejectGroupInvitationAsync` are the
guardian's resolve lanes (one `AccessAudit` row each, `Via: Guardian`).

The *event* half had **no** enforcement at all: a supervised child could RSVP
`Going` on any visible event with zero guardian involvement —
`EventService.RsvpAsync` was last-write-wins, self-service, audit-free, and
had no `GuardianLink` read at all. A guardian who accepted the consent text
had, in the event surface, none of the "review and approve or deny" power they
had promised to exercise.

The product want (2026-10-05), in the words it was given: **implement the
gate, but make it optional** — some guardians do not want to review every
event invitation manually. The three postures a guardian may pick, in order of
strictness:

1. **Guardian approves/denies every event** (the strictest; the consent text's
   literal promise) — the child's own RSVP self-lane is refused; a *request
   row* is stored instead; the guardian decides.
2. **Guardian is notified; default-approve with the power to veto later** —
   the child's own RSVP self-lane is allowed; the guardian gets a
   notification and may delete the RSVP afterwards (the "veto" window).
3. **Child decides for themselves** — the child's own RSVP self-lane is allowed
   outright; the guardian gets no notification, no veto.

The user chose posture 1 (**Guardian approves/denies every event**) as the
default — it matches the consent text the guardian already accepted, and is
the most protective. Postures 2 and 3 exist for the guardian who finds
posture 1 too heavy.

This is a **7th GU supervisory action** (alongside the 5 ADR 0028 froze and
ADR 0143's deletion), not a new bounded context. It reuses the GU standing gate
exactly, mirrors the group-invitation lane's request-row shape for events, and
adds no new bounded context, no new `AccessAction`, no branch in the frozen
`IAuthorizationService`.

## Decision

- **A — One new document on the `EventDocTypes` surface:
  `GuardianEventRequest`.** `Id` (surrogate), `EventId`, `ChildId`,
  `DesiredStatus` (the child's desired status at request time), `Status`
  (`Pending` / `Approved` / `Denied`), `RequestedAt`, `ResolvedAt`,
  `ResolvedBy`. A **unique `(EventId, ChildId)` index** enforces exactly
  **one** pending request per child per event at the DB layer (the
  `EventRsvp` `(EventId, UserId)` / `GroupInvitation` `(GroupId, UserId)`
  business-key precedent). A resolved request is **superseded** by a fresh
  `Pending` row on the child's next self-lane act (the `GuardianLink` "a
  re-assignment writes a fresh row, not a revival" precedent).
- **B — One new field on `Profile` (the `UserInfo` context):
  `EventRsvpMode`.** An `EventRsvpMode` enum — `GuardianApproves` (default),
  `GuardianNotifies`, `ChildDecides` — with **the default
  `GuardianApproves`** (a newly-formed GU link inherits the most protective
  posture, and a guardian who wants a looser posture must **deliberately**
  pick it — the "fail-protective" rule the `MessagingRestricted` flag's
  default already embodies). A supervised child with a **missing profile still
  requires** guardian approval (the gate resolves the posture as
  `childProfile?.EventRsvpMode ?? (supervised ? GuardianApproves :
  ChildDecides)` — the missing-profile case falls to the strictest supervised
  posture, not the child's freedom).
- **C — The gate is on `EventService.RsvpAsync`, self-lane only.** The gate
  reads the child's active `GuardianLink` rows + the child's
  `Profile.EventRsvpMode` (two read queries on the same session, before the
  existing last-write-wins upsert — no new `IAuthorizationService` call, no new
  `AccessAudit` row, the M4 pin preserved). **`GuardianApproves` ⇒ refused**
  (a `Pending` `GuardianEventRequest` row is stored, the active guardian(s)
  are notified, the write throws, **no** `EventRsvp` row is written);
  **`GuardianNotifies` ⇒ allowed** (the upsert proceeds, the guardian is
  notified — the veto window's entry point); **`ChildDecides` ⇒ allowed
  outright** (the upsert proceeds, no notification); **unsupervised (no active
  link) ⇒ never gated** (the ADR 0054 self-service shape — a mode field with
  no active link is a dormant field). The refusal throws the **same**
  `InvalidOperationException` the GU group-invitation lane uses; the Web layer
  catches it once and surfaces it as `TempData["error"]` + a redirect.
- **D — The guardian resolve lanes (additive seams on `IEventService`).**
  `ApproveEventRsvpAsync` (writes the child's `EventRsvp` with the request's
  `DesiredStatus` — "a guardian's approval IS the child's RSVP"),
  `DenyEventRsvpAsync`, and `VetoEventRsvpAsync` (the `GuardianNotifies`
  posture's veto — deletes the child's existing `EventRsvp`). Each: **standing
  gate first** (an active `GuardianLink` for the exact (guardian, child) pair,
  or `UnauthorizedAccessException` — the Web's non-leaky 404; the G·2/G·3
  deny-by-default), then the request-row resolution, then **one**
  `AccessAudit` row (`Via: Guardian`) — all in one session / one
  `SaveChangesAsync` (C3). A resolved request is a terminal state (a fresh act
  is the only path); a veto is always a fresh act over a current row. Two
  guardian reads (`GetPendingEventRsvpRequestsAsync`, `GetChildRsvpsAsync`)
  are plain queries with no audit row (the `GetMyRsvpAsync` posture). Audit
  verbs: `guardian.event_rsvp_approve` / `guardian.event_rsvp_deny` /
  `guardian.event_rsvp_veto` (all `TargetKind "event"`, `Via Guardian`).
- **E — The posture write lane:
  `IUserInfoService.SetChildEventRsvpModeAsync`.** One **additive** method on
  the frozen `IUserInfoService` surface (the `SetChildMessagingRestrictionAsync`
  shape **verbatim** — the guardian write-lane precedent, over a mode instead
  of a bool): standing gate first (the `GuardActiveLinkAsync` shape), then the
  `Profile.EventRsvpMode` field write, then **one** `AccessAudit` row
  (`guardian.event_rsvp_mode` / `profile` / `Guardian`) — one session, one
  `SaveChangesAsync`.

## Consequences

- **The consent lane's missing promise is functional.** The *event* half of
  the ADR 0038 "review and approve or deny all … event invitations" now has
  the same enforcement shape the *group* half already had — a request-row
  gate, a guardian resolve pair, a one-audit-row-per-action posture. A
  guardian who accepted the consent text now has the power they promised in
  the event surface, not just the group surface.
- **Optional by posture, protective by default.** The gate is **off** for an
  unsupervised child and for a guardian who deliberately picks
  `GuardianNotifies` / `ChildDecides`; it is **on** (the strictest posture) by
  default and for a missing profile. The fail-protective default is the same
  discipline as the `MessagingRestricted` flag.
- **Additive on the frozen seams, zero new authorization surface.** One new
  doc (`GuardianEventRequest`) on the existing `EventDocTypes` surface, one new
  `Profile` field, three new `IEventService` methods, one new
  `IUserInfoService` method — no new bounded context, no new `AccessAction`,
  no new `AccessVia` beyond the existing `Guardian` used for audit, no branch
  in the frozen `IAuthorizationService`, no `Decide()` change. The standing
  gate is the **same** `GuardActiveLinkAsync` the five ADR 0028 actions use.
- **This is a 7th GU supervisory action, not a 6th lane.** It sits alongside
  the five ADR 0028 froze (suspend, memberships, invitation approval, invite
  a second guardian, hand-over) and ADR 0143's deletion — the GU surface
  grows by one action, the bounded-context and authorization surfaces stay
  byte-identical.
- **The missing-profile case fails to the strictest supervised posture, not
  the child's freedom** — the gate is fail-protective by construction, the
  same invariant the `MessagingRestricted` default already holds.
