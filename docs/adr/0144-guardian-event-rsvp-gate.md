# ADR 0144 — Guardian's event-attendance gate: the consent lane's missing promise made functional

Status: Accepted
Date: 2026-10-05
Builds on: 0028 (the GU standing — the `GuardianLink` document, the
`AccessVia.Guardian` value, the `GuardActiveLinkAsync` standing gate, and
the G·1 cardinal rule: the lane never reads the child's content); 0038 (§F —
the guardian consent text, `guardian.consent.duties_invitations`, which
promises the guardian the power to "approve or deny all group and **event**
invitations"; the consent gate is in place, but the *event* half of that
promise had no enforcement lane — the gap this ADR closes); 0054 (M4 — the
`Event` / `EventRsvp` documents, the `RsvpAsync` self-lane's last-write-wins
shape, and the "no `AccessAudit` row on a routine RSVP" pin); 0013 (the
group-invitation lane — the `GroupInvitation` `Pending → Accepted /
Declined` request-row precedent, the "a re-request writes a fresh row, not a
revival" shape, and the `guardian.group_invite` fan-out the new
`guardian.event_request` kind mirrors); 0006 (the §A frozen-service
rule — every new seam is an *additive* method on
`IUserInfoService` / `IEventService`; no new bounded context, no new
`AccessAction`, no branch in the frozen `IAuthorizationService`); 0004 (the
persistence rule — one new `GuardianEventRequest` document on the
`M4DocTypes` surface, one unique `(EventId, ChildId)` index, no EF); 0003
(audit-by-default — the three guardian resolve lanes each write one
`AccessAudit` row with `Via: Guardian`; the child's own self-lane writes
**no** audit row, preserving the M4 pin)

## Context

ADR 0038's consent text binds a guardian to "review and approve or deny all
group and event invitations" before the child's account may exist. The
*group* half of that promise was functional the moment the GU lane landed
(ADR 0028, extended by ADR 0141's community-approval lane): a group
invitation on a supervised child is stored as a `Pending`
`GroupInvitation` row, the child's self-lane `AcceptGroupInvitationAsync`
is refused, the active guardian(s) get a `guardian.group_invite`
notification, and `ApproveGroupInvitationAsync` /
`RejectGroupInvitationAsync` are the guardian's resolve lanes (one
`AccessAudit` row each, `Via: Guardian`).

The *event* half had **no** enforcement at all: a supervised child could
RSVP `Going` on any visible event with zero guardian involvement —
`EventService.RsvpAsync` was last-write-wins, self-service, audit-free, and
had no `GuardianLink` read at all. A guardian who accepted the consent text
had, in the event surface, none of the "review and approve or deny" power
they had promised to exercise.

The product want (2026-10-05), in the words it was given: **implement the
gate, but make it optional** — some guardians do not want to review every
event invitation manually. The three postures a guardian may pick, in order
of strictness:

1. **Guardian approves/denies every event** (the strictest; the consent
   text's literal promise) — the child's own RSVP self-lane is refused; a
   *request row* is stored instead; the guardian decides.
2. **Guardian is notified; default-approve with the power to veto later** —
   the child's own RSVP self-lane is allowed; the guardian gets a
   notification and may delete the RSVP afterwards (the "veto" window).
3. **Child decides for themselves** — the child's own RSVP self-lane is
   allowed outright; the guardian gets no notification, no veto.

The user chose posture 1 (**Guardian approves/denies every event**) as the
default — it matches the consent text the guardian already accepted, and is
the most protective. Postures 2 and 3 exist for the guardian who finds
posture 1 too heavy.

This is a **7th GU supervisory action** (alongside the 5 ADR 0028 froze and
ADR 0143's deletion), not a new bounded context. It reuses the GU standing
gate exactly, mirrors the group-invitation lane's request-row shape for
events, and adds no new bounded context, no new `AccessAction`, no branch
in the frozen `IAuthorizationService`.

## Decision

### A. The new document: `GuardianEventRequest`

One new document on the **`M4DocTypes`** surface (it is an event-attendance
artifact, not an identity or profile artifact — the ADR 0004 §B
persistence rule):

```csharp
public sealed class GuardianEventRequest
{
    public string Id { get; set; }                       // surrogate (Marten default)
    public string EventId { get; set; }                  // part of the unique (EventId, ChildId) index
    public string ChildId { get; set; }                  // part of the unique (EventId, ChildId) index
    public RsvpStatus DesiredStatus { get; set; }        // the child's desired status at request time
    public GuardianEventRequestStatus Status { get; set; } // Pending / Approved / Denied
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }              // the guardian who resolved it
}

public enum GuardianEventRequestStatus { Pending, Approved, Denied }
```

The **unique `(EventId, ChildId)` index** (the `M4DocTypes.Configure`
surface) enforces exactly **one** pending request per child per event at
the DB layer — the `EventRsvp` `(EventId, UserId)` / `GroupInvitation`
`(GroupId, UserId)` business-key precedent. A resolved (Approved / Denied)
request is **superseded** by a fresh `Pending` row on the child's next
self-lane act (the `GuardianLink` "a re-assignment writes a fresh row, not
a revival" precedent — the child's re-act is a deliberate new act, not an
undo; the guardian's resolution is not a revocable decision).

### B. The posture field: `Profile.EventRsvpMode`

One new field on `Profile` (bounded context `UserInfo`, the
`MessagingRestricted` field's shape — a guardian-only write, a child-affecting
read):

```csharp
public enum EventRsvpMode
{
    GuardianApproves = 0,   // default — the consent text's literal promise
    GuardianNotifies,       // auto-approve + the veto window
    ChildDecides            // no gate, no notification, no veto
}

// Profile.cs
public EventRsvpMode EventRsvpMode { get; set; } = EventRsvpMode.GuardianApproves;
```

The **default is `GuardianApproves`** — a newly-formed GU link inherits the
most protective posture, and a guardian who wants a looser posture must
**deliberately** pick it (the "fail-protective" rule the
`MessagingRestricted` flag's default already embodies). A supervised child
with a missing profile **still requires** guardian approval (the
`EventService.RsvpAsync` gate resolves the posture as
`childProfile?.EventRsvpMode ?? (supervised ? GuardianApproves : ChildDecides)`
— the missing-profile case falls to the strictest supervised posture, not
the child's freedom).

### C. The gate: `EventService.RsvpAsync`

The **self-lane** is the only lane gated. The gate reads the child's active
`GuardianLink` rows + the child's `Profile.EventRsvpMode` (two read queries
on the same session, before the existing last-write-wins upsert — no new
`IAuthorizationService` call, no new `AccessAudit` row, the M4 pin
preserved):

| Posture              | Self-lane outcome                                                                                                    |
|----------------------|----------------------------------------------------------------------------------------------------------------------|
| `GuardianApproves`   | **Refused** (`InvalidOperationException` — the Web's user-presentable `TempData["error"]` shape): a `GuardianEventRequest` `Pending` row is stored (or refreshed to `Pending` with the child's current desired status), the active guardian(s) are notified (`guardian.event_request`, one row per active `GuardianLink`, `LinkPath = /me/children/{childId}`), and the write throws. **No** `EventRsvp` row is written. |
| `GuardianNotifies`   | **Allowed**: the last-write-wins upsert proceeds unchanged; the active guardian(s) are notified (`guardian.event_rsvp`, `LinkPath = /events/{eventId}` — the veto window's entry point). |
| `ChildDecides`       | **Allowed outright**: the last-write-wins upsert proceeds, no guardian notification. |
| **Unsupervised** (no active link) | **Never gated** — the ADR 0054 self-service shape, regardless of the stored mode (a mode field with no active link is a dormant field). |

The `InvalidOperationException` is the **same** exception type the GU
group-invitation lane uses for its self-lane refusal (`AcceptGroupInvitationAsync`
on a supervised child) — the Web layer catches it once (the existing
`EventController.Rsvp` and `GroupsController` group-event RSVP surfaces) and
surfaces it as `TempData["error"]` + a redirect back to the event page.

### D. The guardian resolve lanes

Three **additive** seams on `IEventService` (ADR 0006 §A/§E), each with the
same shape: **standing gate first** (an active `GuardianLink` for the exact
(guardian, child) pair, or `UnauthorizedAccessException` — the Web's
non-leaky 404; the G·2/G·3 deny-by-default), then the request-row
resolution, then one `AccessAudit` row (`Via: Guardian`) — all in one
session / one `SaveChangesAsync` (C3):

```csharp
// The GuardianApproves posture's resolve pair (the GU approve/reject shape,
// re-expressed over events). The approve lane writes the child's EventRsvp
// with the request's DesiredStatus (the "a guardian's approval IS the child's
// RSVP" shape — the child's own self-lane would have written the same row).
Task<GuardianEventRequest> ApproveEventRsvpAsync(string eventId, string childId, string guardianId, CancellationToken ct = default);
Task<GuardianEventRequest> DenyEventRsvpAsync(string eventId, string childId, string guardianId, CancellationToken ct = default);

// The GuardianNotifies posture's veto (the "undo an auto-approved attendance"
// window) — deletes the child's existing EventRsvp.
Task VetoEventRsvpAsync(string eventId, string childId, string guardianId, CancellationToken ct = default);

// Two guardian reads (plain queries, no audit row — the GetMyRsvpAsync
// posture; the standing gate is the Web layer's Detail GET ActiveLinkAsync
// check, the MessagingRestricted read precedent).
Task<IReadOnlyList<GuardianEventRequest>> GetPendingEventRsvpRequestsAsync(string childId, CancellationToken ct = default);
Task<IReadOnlyList<EventRsvp>> GetChildRsvpsAsync(string childId, CancellationToken ct = default);
```

| Lane            | Refuse (no request row / no RSVP row)      | State-machine pin (a resolved request)  |
|-----------------|--------------------------------------------|------------------------------------------|
| `ApproveEventRsvpAsync` | `KeyNotFoundException` (no request) → the Web's 404 | `InvalidOperationException` (already `Approved` / `Denied`) — a fresh act is the only path |
| `DenyEventRsvpAsync`   | `KeyNotFoundException` (no request) → the Web's 404 | `InvalidOperationException` (already `Approved` / `Denied`) |
| `VetoEventRsvpAsync`   | `KeyNotFoundException` (no RSVP row to remove) → the Web's 404 | n/a (a veto is always a fresh act over a current row) |

Each resolve lane writes **one** `AccessAudit` row:

| Lane            | `Action` verb                    | `TargetKind` | `Via`        |
|-----------------|----------------------------------|--------------|--------------|
| `ApproveEventRsvpAsync` | `guardian.event_rsvp_approve`  | `event`      | `Guardian`   |
| `DenyEventRsvpAsync`    | `guardian.event_rsvp_deny`     | `event`      | `Guardian`   |
| `VetoEventRsvpAsync`    | `guardian.event_rsvp_veto`     | `event`      | `Guardian`   |

The posture write lane (below) writes its own:

| Seam                                 | `Action` verb             | `TargetKind` | `Via`      |
|--------------------------------------|---------------------------|--------------|------------|
| `IUserInfoService.SetChildEventRsvpModeAsync` | `guardian.event_rsvp_mode` | `profile`    | `Guardian` |

### E. The posture write lane: `IUserInfoService.SetChildEventRsvpModeAsync`

One **additive** method on the frozen `IUserInfoService` surface (the
`SetChildMessagingRestrictionAsync` shape **verbatim** — the guardian
write-lane precedent, over a mode instead of a bool):

```csharp
Task SetChildEventRsvpModeAsync(string childId, EventRsvpMode mode, string guardianId);
```

Standing gate first (the `GuardActiveLinkAsync` shape), then the
`Profile.EventRsvpMode` field write, then the one audit row — one session,
one `SaveChangesAsync`. The enforcement **read** lives in
`EventService.RsvpAsync` (the §C gate) — the write lane never touches the
event surface (the ADR 0028 G·1 cardinal rule: the lane never reads the
child's content; the enforcement read is the gate's own query, not the
write lane's).

### F. The Web surface

`GuardianController` gains **one nullable ctor param**
(`IEventService? events = null` — the `ProjectsController.events` optional
precedent; the five existing test constructions keep compiling) and
**four actions**, all `POST` + `[ValidateAntiForgeryToken]`, all with the
`SetChildMessaging` idiom (standing gate → the seam call → the `catch`
blocks → the `RedirectToAction(nameof(Detail), new { childId })`):

| Action             | Route                                             | Delegate seam                                    |
|--------------------|---------------------------------------------------|--------------------------------------------------|
| `SetEventRsvpMode`   | `me/children/{childId}/eventrsvp`                 | `SetChildEventRsvpModeAsync` (the mode write)    |
| `ApproveEventRsvp`   | `me/children/{childId}/eventrsvp/{eventId}/approve` | `ApproveEventRsvpAsync`                        |
| `DenyEventRsvp`      | `me/children/{childId}/eventrsvp/{eventId}/deny`     | `DenyEventRsvpAsync`                           |
| `VetoEventRsvp`      | `me/children/{childId}/eventrsvp/{eventId}/veto`     | `VetoEventRsvpAsync`                           |

The `Detail` GET populates three `ViewData` keys (the `MessagingRestricted`
`ViewData` precedent — `MembershipEditorModel` is a pinned projection, new
data rides `ViewData`): `EventRsvpMode` (the current posture, drives the
three mode-switch buttons), `PendingEventRsvpRequests` (the
`GuardianApproves` posture's live request list, the approve/deny forms),
and `ChildEventRsvps` (the `GuardianNotifies` posture's live RSVP list, the
veto forms). A non-guardian is a 404 (the `ActiveLinkAsync` standing
shape, the `Detail` GET's existing gate).

The two child-facing RSVP surfaces (`EventController.Rsvp` + the
`GroupsController` group-event RSVP) catch the new `InvalidOperationException`
from `RsvpAsync` and surface it as `TempData["error"]` + a redirect back to
the event page (the "the child's self-lane was refused, the request is with
your guardian" message — the `guardian.eventrsvp.refused` key, localized in
all four languages).

### G. The notification kinds

Two **additive** kinds on `NotificationKinds` (both in the **opt-OUT**
`Known` set — the resident-facing posture, the `GuardianGroupInvite`
precedent):

| Constant                 | Value                    | When emitted                              | Recipient | `LinkPath`                  |
|--------------------------|--------------------------|-------------------------------------------|-----------|------------------------------|
| `GuardianEventRequest`   | `guardian.event_request` | the `GuardianApproves` posture's refusal  | the child's active guardian(s), one row per active `GuardianLink` | `/me/children/{childId}` |
| `GuardianEventRsvp`      | `guardian.event_rsvp`    | the `GuardianNotifies` posture's allowance | the child's active guardian(s), one row per active `GuardianLink` | `/events/{eventId}`    |

The idempotency key is per-`(guardian, event, child)` (the
`NotificationService` dedups on it; a re-RSVP within the same window
refreshes the child's row / re-stores the request row, not the guardian's
inbox).

### H. What this lane deliberately does **not** do

- **No blanket "event veto-all"** — the `VetoEventRsvpAsync` lane is
  per-event (it deletes one `EventRsvp` row). A "remove this child from all
  events at once" control is a distinct lane (a future ADR, not this one).
- **No removal of a resolved request row** — the
  `GuardianEventRequest` row persists after resolution (the audit trail
  the `GroupInvitation` lane keeps: the *history* of what the guardian
  decided about each event, visible to the guardian). A child's re-act
  writes a fresh `Pending` row over the same `(EventId, ChildId)` key
  (the unique index's upsert semantics), not a deletion.
- **No `AccessAudit` row on the child's own self-lane** — the ADR 0054
  M4 pin ("no audit row on a routine RSVP") is preserved: the gate's
  refusal is a *refusal*, not an access decision (the M4 pin's "the
  self-lane is not an access decision, it is a routine resident action"
  rationale). Only the **guardian's** resolve lanes write audit rows.
- **No new `AccessAction` / `AccessVia` value / `IAuthorizationService`
  branch** — the lane reuses the existing `AccessVia.Guardian` value and
  the existing GU standing gate (`GuardActiveLinkAsync` for the write
  lanes; the active-link existence check for the gate itself); the frozen
  `IAuthorizationService` surface is untouched (ADR 0006 §A).
- **No EF Core involvement** — the `GuardianEventRequest` document is on
  the `M4DocTypes` Marten surface (ADR 0004 §B); EF Core remains
  Identity-only.

## Consequences

- **The consent promise is now enforceable.** A guardian who accepts the
  ADR 0038 consent text has a functional lane to exercise "approve or deny
  all event invitations" (the default posture) or a lighter one (the
  notify + veto posture) or to waive it (the child-decides posture). The
  `guardian.consent.duties_invitations` text and the code now agree.
- **The M4 self-lane is unchanged for the unsupervised.** A resident with
  no active `GuardianLink` over them is never gated, regardless of any
  stored `EventRsvpMode` — the ADR 0054 self-service shape is preserved
  for the (majority) unsupervised case.
- **The GU surface is now 7 actions** (suspend, community curation, group
  curation, invitation approval, dissolution, **event-attendance
  approval**, deletion). The "no content reads" cardinal rule (G·1)
  is preserved: the resolve lanes write the child's `EventRsvp` on their
  behalf but never read the child's content; the gate itself is a
  standing check + a posture read, not a content read.
- **One new document, one new profile field, five new service seams, two
  new notification kinds, four new controller actions, two new `ViewData`
  keys** — all additive (ADR 0006 §E compatible-ADD shape); no existing
  seam's signature changed; no existing test construction broke (the
  nullable `IEventService` ctor param kept the five `GuardianController`
  test call sites compiling).
- **The "fail-protective default" rule is extended.** A new GU link
  inherits `GuardianApproves`; a supervised child with a missing profile
  still requires guardian approval; a non-guardian on any resolve lane is
  a 404 (the G·2/G·3 deny-by-default).
- **Testable without a new fixture.** The Core tests reuse the
  `GuardianApprovalLaneTests` harness (the `NotificationService` wiring,
  the `CreateGuardianLinkAsync` GU formation, the `NotificationsFor` /
  `AuditsFor` query helpers) with the `M4DocTypes` surface added for the
  event documents — the 15 new Core tests + 8 new Web tests run in the
  same ~15-second window as the existing GU lane tests.

## Amendment history

- 2026-10-05 — initial. The user asked (after the consent-text audit
  surfaced the gap) to implement the event-attendance gate with **three
  optional postures** and chose `GuardianApproves` as the default (the
  consent text's literal promise, the most protective). The three postures
  are the `EventRsvpMode` enum; the default is `GuardianApproves`.
