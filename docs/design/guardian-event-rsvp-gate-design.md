# Design Doc — Guardian's event-attendance gate (the consent lane's missing promise made functional)

ADR: [0144](../adr/0144-guardian-event-rsvp-gate.md) — the authority for
every seam name, default, and refuse shape in this document.
Builds on: ADR 0028 (GU), ADR 0038 (consent), ADR 0054 (M4 events), ADR
0013 (group invitations), ADR 0141 (GU community-approval lane — the
closest precedent, the "request-row + guardian resolve pair" shape).

## Context

The consent text a guardian accepts before a child account may exist
(`guardian.consent.duties_invitations`, ADR 0038 §F) promises the guardian
"review and approve or deny all group **and event** invitations." The group
half works (ADR 0028/0141: the supervised child's self-lane is refused, the
guardian gets the request, the guardian decides). The event half had no
enforcement lane at all — a supervised child could RSVP to any visible event
with zero guardian involvement. A guardian who accepted the terms could not
functionally comply with them on the event surface.

Product want (2026-10-05): implement the gate, but make it **optional** —
three postures, the guardian picks, defaulting to the strictest (the consent
text's literal promise):

1. `GuardianApproves` — the child's RSVP self-lane is refused; a request row
   is stored; the guardian approves or denies.
2. `GuardianNotifies` — the child RSVPs freely; the guardian is notified and
   may veto (delete) the attendance afterwards.
3. `ChildDecides` — the child decides for themselves; no gate, no
   notification, no veto.

## Goals / Non-goals

**Goals.**
- A functional, testable "approve or deny event invitations" lane for
  guardians (the consent promise made enforceable).
- Three postures, guardian-selectable at any time from the manage-child page.
- The strictest posture by default (fail-protective — the
  `MessagingRestricted` default precedent).
- Additive-only: no frozen-surface break, no existing test construction
  broken, no EF, no new bounded context.

**Non-goals.**
- No blanket "veto all events at once" control (per-event only; a
  separate future lane if wanted).
- No removal of resolved request rows (the audit trail persists; a child's
  re-act writes a fresh `Pending` row over the same `(EventId, ChildId)` key).
- No `AccessAudit` row on the child's own self-lane (the ADR 0054 M4 pin —
  "a routine RSVP is not an access decision" — is preserved; only the
  guardian's resolve lanes are audited, `Via: Guardian`).
- No `AccessAction` / `AccessVia` / `IAuthorizationService` changes (the
  existing GU standing is reused verbatim).
- No group-event special-casing: the gate lives in
  `EventService.RsvpAsync`, and both child-facing surfaces (the standalone
  `EventController.Rsvp` and the `GroupsController` group-event RSVP) delegate
  to it — one gate covers both.

## Parts affected

| Part | Change |
|---|---|
| `Kumunita.Core/UserInfo/Profile.cs` | New `EventRsvpMode` enum + `Profile.EventRsvpMode` field (default `GuardianApproves`) |
| `Kumunita.Core/Events/GuardianEventRequest.cs` | **New file** — the request-row document + its 3-state machine |
| `Kumunita.Core/M4DocTypes.cs` | Registers `GuardianEventRequest` + the unique `(EventId, ChildId)` index |
| `Kumunita.Core/Notifications/NotificationKinds.cs` | Two new opt-OUT kinds: `guardian.event_request`, `guardian.event_rsvp` |
| `Kumunita.Core/UserInfo/IUserInfoService.cs` + impl | `SetChildEventRsvpModeAsync` (the `SetChildMessagingRestrictionAsync` shape verbatim) |
| `Kumunita.Core/Events/IEventService.cs` + impl | The gate in `RsvpAsync`; `ApproveEventRsvpAsync` / `DenyEventRsvpAsync` / `VetoEventRsvpAsync`; `GetPendingEventRsvpRequestsAsync` / `GetChildRsvpsAsync` |
| `Kumunita.Web/Controllers/GuardianController.cs` | Nullable `IEventService?` ctor param (kept the 5 existing test call sites compiling); 4 actions; `Detail` GET `ViewData` (mode, pending requests, child's RSVPs) |
| `Kumunita.Web/Controllers/EventController.cs` + `GroupsController.cs` | Catch the self-lane refusal (`InvalidOperationException`) → `TempData["error"]` + redirect |
| `Kumunita.Web/Models/GuardianViewModels.cs` | `EventRsvpRequestItem` / `EventRsvpVetoItem` records |
| `Kumunita.Web/Views/Guardian/Detail.cshtml` | The "Event attendance" section (mode switch + per-posture live lists) |
| `Kumunita.Core/Localization/KnownTranslationKeys.cs` | `guardian.eventrsvp.*` + the two notification keys ×4 languages |
| `Kumunita.Core/Bootstrap/FirstBootSeeder.cs` | Help-page prose (child-accounts + being-a-child) ×4 languages |

## Seams & contracts

The ADR 0144 §B–§G tables are the contract. The load-bearing pins:

- **The gate fires on the child's link existence, not the actor's standing.**
  `RsvpAsync` asks "does this *actor* have an active `GuardianLink` where
  they are the *child*?" — an unsupervised actor is never gated, regardless
  of any stored mode. The guardian's standing on the resolve lanes is a
  *different* check: an active link where they are the *guardian*
  (`GuardianEventLinkAsync`), or `UnauthorizedAccessException` (the Web 404).
- **The mode read is fail-protective.**
  `childProfile?.EventRsvpMode ?? (supervised ? GuardianApproves :
  ChildDecides)` — a supervised child with a missing profile is *stricter*,
  not freer.
- **A resolved request is terminal.** Re-approve / re-deny is an
  `InvalidOperationException`; the only path past a resolution is the
  child's fresh act (a new `Pending` row, the unique-index upsert).
- **The approve lane writes the child's RSVP with the request's
  `DesiredStatus`** — the "a guardian's approval IS the child's RSVP"
  shape; the deny lane writes no RSVP row; the veto lane deletes one.
- **The two reads (`GetPendingEventRsvpRequestsAsync` /
  `GetChildRsvpsAsync`) are plain queries** — no audit row (the
  `GetMyRsvpAsync` posture); the standing gate is the Web layer's `Detail`
  GET `ActiveLinkAsync` check (the `MessagingRestricted` read precedent —
  `MembershipEditorModel` is a pinned projection, so the new data rides
  `ViewData`).
- **Notifications are opt-OUT** (the `GuardianGroupInvite` resident-facing
  posture) and per-guardian (one row per active `GuardianLink`, distinct
  idempotency keys).

## Pinned contract (the tests)

`tests/Kumunita.Core.Tests/GuardianEventRsvpGateTests.cs` (15 tests) +
`tests/Kumunita.Web.Tests/GuardianEventRsvpGateTests.cs` (8 tests):

- Default posture → self-lane refused (`InvalidOperationException`), **no**
  `EventRsvp` row, one `Pending` `GuardianEventRequest` row, one
  `guardian.event_request` notification per active guardian (child got
  nothing).
- Two active guardians → one notification each, distinct idempotency keys.
- Re-request over a resolved row → one row, back to `Pending`, fresh
  `DesiredStatus`.
- `GuardianNotifies` → RSVP lands + `guardian.event_rsvp` row, no request row.
- `ChildDecides` → RSVP lands, **no** guardian notification.
- Unsupervised → RSVP lands unchanged (the ADR 0054 shape), no gate effect.
- Approve → child's RSVP row with the desired status, request `Approved` +
  `ResolvedBy`, `guardian.event_rsvp_approve` audit `Via: Guardian`.
- Deny → no RSVP row, request `Denied`, `guardian.event_rsvp_deny` audit.
- Veto → RSVP row deleted, `guardian.event_rsvp_veto` audit; veto over
  nothing → `KeyNotFoundException`.
- Non-guardian on all three resolve lanes → `UnauthorizedAccessException`.
- Re-approve a resolved request → `InvalidOperationException`; approve with
  no request → `KeyNotFoundException`.
- `SetChildEventRsvpModeAsync` → profile field live on the next read,
  `guardian.event_rsvp_mode` audit `Via: Guardian`; non-guardian →
  `UnauthorizedAccessException`.
- Web: the `SetEventRsvpMode` standing gate (non-guardian → 404, no write),
  the three-mode happy path (all three postures, the C4 strong-consistency
  read, the audit row), the resolve-lane delegation (approve writes the
  child's RSVP + the request resolution + the audit row), and the
  null-safe shape (no `IEventService` → the resolve lanes 404, not throw).

## Rollout & rollback

- **Rollout:** the `GuardianEventRequest` table + the
  `profile.eventrsvpmode` column are additive (Marten's
  `ApplyAllConfiguredChangesToDatabaseAsync` picks them up on the next boot,
  the `M4DocTypes` / M1-profile surfaces); existing rows are unaffected
  (the new profile field defaults to `GuardianApproves` on read — the
  `??` fallback in the gate covers any row written before the column
  existed, so a migration-ordering race is fail-protective by
  construction).
- **Rollback:** remove the four controller actions + the `Detail.cshtml`
  section + the two `ViewData` keys, drop the gate block from
  `RsvpAsync`, and the `IEventService` / `IUserInfoService` seams go away
  with the ADR. The `GuardianEventRequest` table + the
  `Profile.EventRsvpMode` column are inert (an unread column + an empty
  table) — no data is lost, a later re-land is clean.

## Risks

- **A guardian who never opens the manage-child page** does not see the
  default posture; the child's first RSVP attempt is refused with the
  "your guardian needs to approve" message. Mitigation: the refusal
  message + the `guardian.event_request` notification both point the
  guardian at the manage-child page (the `LinkPath`), and the default is
  the one the consent text already describes.
- **The `GuardianNotifies` veto window has no expiry** — the guardian may
  veto long after the event. Deliberate (a simpler invariant than a TTL,
  and the consent text's "deny at any time" reading); a later ADR can add
  an expiry if the product wants it.
- **The `Profile.EventRsvpMode` field is on the shared `Profile` document** —
  any profile write lane that copies/creates profiles must preserve the
  field. The `SetChildEventRsvpModeAsync` lane mutates in place (the
  `MessagingRestricted` precedent — the write lanes already treat `Profile`
  as a mutable document, not a copy source).

## Three tests (run before "ready")

1. **The consent loop closes.** A fresh GU link + the child's first
   `Going` RSVP → the child sees the refusal, the guardian's inbox has the
   `guardian.event_request` row, the guardian's approve click writes the
   child's RSVP, the event's RSVP list shows the child. (The
   `SupervisedChildDefaultMode_RsvpRefused_RequestStored_GuardianNotified`
   + `GuardianApproves_ChildRsvpLands_RequestApproved_AuditRow` pair.)
2. **The looser postures are strictly looser.** `GuardianNotifies` → the
   child's RSVP lands *and* the guardian gets the veto row; `ChildDecides`
   → the child's RSVP lands *and* the guardian gets nothing. (The
   `SupervisedChild_NotifiesPosture_*` + `SupervisedChild_ChildDecidesPosture_*`
   pair.)
3. **The non-guardian learns nothing.** A stranger on any of the four
   resolve/mode lanes → a 404, no state change, no audit row. (The
   `SetEventRsvpMode_NonGuardian_Returns404_NoWrite` +
   `NonGuardian_ApproveDenyVetoAllRefused` pair.)

## GU — Closed (recorded) (2026-10-05)

- The consent gap is closed: the event-attendance gate is live in all
  three postures, the default matches the consent text, and the
  "non-guardian learns nothing" invariant holds on every lane.
- 15 Core + 8 Web tests pin the behavior; both assemblies build and run
  green (the one unrelated flake —
  `ProjectServiceTests.CreateTodoComment_UnreadableTodoRefused` — passes
  in isolation; a Docker test-container pressure flake, not a regression).
- ADR 0144 + this design doc are the permanent record; the CONVENTIONS.md
  child-accounts row + the ADR README index are updated alongside.
