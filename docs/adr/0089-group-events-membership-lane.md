# ADR 0089 — Group events: a membership-scoped group events lane

Status: Accepted
Date: 2026-09-25

## Context

Two shipped truths leave a gap:

- **Events are scoped to Communities only.** M4 (ADR 0054) gave events a
  `ComponentId` feed organizer, an `Audience` boundary, drafts, RSVPs,
  reminders, and translations — but every event lives on the *community*
  surface. A group has **no** events surface: a group's page shows posts and
  members, and the "create an event" affordance only ever reaches the
  community feed. A small circle (a family, a committee, a project team)
  that already keeps its discussions *inside* a group (ADR 0013) has
  nowhere to put the things those discussions are about — the potluck, the
  shift handoff, the site visit.
- **The platform already has the right mechanism.** ADR 0013 solved exactly
  this for *posts*: a group-channel post is visible **only to current
  members**, membership is the sole decision, a non-member (including a
  GlobalAdmin) gets a 404, and membership changes re-scope the very next
  request. The *why* — "reach exactly the right people as membership
  moves" — is identical for events: a one-time audience grant cannot follow
  a membership add/remove the way live membership can. Reusing ADR 0013's
  frozen group-lane seams (`CanSeeGroupAsync` / `CanSeeGroupFeedAsync`)
  rather than inventing an audience-grant variant keeps the privacy model
  consistent and adds **zero** new authorization surface.

Group events therefore need the **membership lane** applied to the M4 event
surface. This is a new lane on the (frozen) `IEventService` surface — a
deliberate, reviewable ADD (ADR 0006-E's "add few, add stable" discipline),
and the ADR 0013 precedent dictates its one hard edge up front: the
strictest privacy lane the platform has. This ADR records the decision;
`docs/design/group-events-design.md` (Parts 1–2) freezes the invariants
**GE·1–GE·8**, the FACES, and the **exact C#** this ADR points at — the
design doc is the authority on shape; this ADR is the authority on *why
these shapes, and why nothing else*.

## Decision

- **The lane is membership — visibility and authoring alike.** An event with
  non-empty `Event.GroupId` is a group-channel event: who may see it is
  **exactly** the group's current members, resolved by the **existing**
  frozen group-lane seams (`IUserInfoService.GetGroupIdsAsync` live read,
  strong consistency, invariant C4 — via `CanSeeGroupAsync` /
  `CanSeeGroupFeedAsync`, unchanged). A membership add or remove re-scopes
  the very next feed/detail (FACES GE3/GE4). The audience lane is **never
  evaluated** for a group event (GE·1), and the event's `Audience` is
  written non-null **empty** (GE·8) — nothing about it is grantable.

- **Only members may author.** The create gate **is** the group-lane
  decision (GE·3, FACES GE5/GE6): Allow ⇒ the member may create; Deny ⇒ the
  gate's audit row is committed **before** `UnauthorizedAccessException`
  throws so the row survives, and the Web renders **404** (the group lane's
  "a non-member learns nothing" precedent). The owner may create **as** a
  member; the lane has no owner-skip.

- **One ADD lane on `IEventService` — nothing moves on the frozen surface.**
  The existing M4 `IEventService` signatures stay byte-identical; the group
  lane *adds* exactly four: `ListGroupEventsAsync(groupId, actorId, page)`,
  `GetGroupEventAsync(groupId, eventId, actorId)`,
  `CreateGroupEventAsync(draft, actorId, IDocumentSession session)`, and
  `UpdateGroupEventAsync(eventId, actorId, GroupEventUpdate, IDocumentSession
  session)` — each a **compatible** ADD on the owning module's surface per
  ADR 0006-E (the M2 `GetProfilesAsync` / ADR 0013 group-post precedent).
  **Publish, RSVP, and the translation lanes are not re-cut** — the group
  event reuses the existing lane-neutral `PublishAsync` (already author-only
  and idempotent), `RsvpAsync` / `GetMyRsvpAsync` / `GetRsvpsAsync`, and the
  ADR 0059 `EventTranslation` seams, all of which are keyed by `EventId` and
  carry no group-specific branch. **No** new `IAuthorizationService` method,
  **no** new `AccessAction`, **no** new `AccessVia`, **no** new `Decide()`
  branch: the group lane reuses the ADR 0013 frozen seams verbatim (the lane
  is *reused*, not *extended*).

- **The data is one additive, not a new doc — and no new index.**
  `Event.GroupId` (`string`, default `string.Empty`) is the **single** change
  to the `Event` POCO — the ADR 0004 §B.1 additive lane, the ADR 0013
  `Post.GroupId` precedent (delta-detected, idempotent, no re-seed). No
  `(GroupId)` index is added, **exactly as `Post.GroupId` has none** — at a
  single-neighborhood scale the group feed's candidate filter
  (`GroupId == groupId && !IsDeleted && !IsDraft`) range-scans fine without
  one, and keeping the `M4DocTypes` surface untouched is the conservative
  call. It is written **only** by `EventService.CreateGroupEventAsync`,
  which pins the write shape: `ComponentId = string.Empty` and
  `Audience = new Audience()` (GE·2, GE·8). `EventService`'s existing
  constructor is **unchanged** (ADR 0006-D: the lane owns every membership
  read; `EventService` never reads `GroupMembership` or `DelegationGrant`
  itself — it calls the frozen group seams, exactly as `PostService` does).

- **Lanes are exclusive.** One event is either a community feed entry **or**
  a group-channel event — never both (GE·2): a group event (`GroupId`
  non-empty) has `ComponentId = string.Empty` and a non-null empty
  `Audience` (GE·8), and a community event never carries a `GroupId`. The
  **global** community read seams are the ones that must keep a group event
  out — because, unlike the `Post` group lane (where group posts are
  structurally distinct from feed posts), a group event is *the same `Event`
  doc* a community event is, so `ListUpcomingAsync` / `ListInRangeAsync`
  (which today list *all* non-deleted, non-draft events, whether or not a
  component filter is passed) gain an **explicit** candidate filter
  `GroupId == string.Empty`. This is a **service-internal** change to those
  two M4 read seams' candidate predicates — no signature change, no new seam,
  and it is *required* (omitting it is the one way a group event would leak
  onto the community feed/calendar). The group feed's own candidate filter
  (`GroupId == groupId`) is the *only* path that reaches a group event.
  No cross-posting between the two lanes in this lane.

- **Delegation, action-scoped, as always (C2 / GE·6).** An in-scope `read`
  grant lets the delegate act with **the owner's** standing — they see the
  group's events iff the **owner** is a member — and the decision row audits
  `Via = Delegation`; an out-of-scope grant still denies. This is the only
  non-`Group` `Via` value on the lane, by design (the same shape as the
  group-post lane's `DecideGroupAsync`).

- **Edit / publish standing — author-only, like group posts (GE·4).** A
  group event's edit and publish lanes are **author-only** (the ADR 0016 /
  ADR 0037 precedent on the group lane): the sole decision is
  `AuthorId == actorId`; a non-author (including a GlobalAdmin or a
  group-member replier) is denied with `UnauthorizedAccessException`, mapped
  to a 404. There is **no** GlobalAdmin-override branch on the group-lane
  edit/publish (contrast the community lane's `author ∪ GlobalAdmin`) — the
  group lane's privacy-first posture (G·4's "strictest reading") extends to
  the write surface. **Soft-delete is a deferral** (below): a group event's
  removal is out of scope for this lane.

- **RSVP and reminders — available to members, inherited, not re-derived.**
  A group event's RSVP surface is the **same** `RsvpAsync` / `GetMyRsvpAsync`
  / `GetRsvpsAsync` the community lane uses (a group event is a member, so
  their RSVPs are the truth); the §6.4 reminder job already runs over
  `ReminderEnabled && !IsDraft && !IsDeleted` events and the Going-RSVP
  recipient set, so **group events are reminded with no change** — the
  recipient set is *membership-derived* (the Going RSVPs are group members
  by construction of the RSVP gate), which is exactly the "reach the right
  people" property. No new reminder surface, no new notification kind
  (reusing `event.reminder`), no audit row (the reminder is a side effect,
  not an access decision — ADR 0054 §3.6).

- **Translations — available, standing inherited from the author (GE·5).**
  A group event's user-added translations use the **existing** ADR 0059
  `EventTranslation` lanes (`AddEventTranslationAsync` /
  `UpdateEventTranslationAsync` / `RemoveEventTranslationAsync`): the
  standing matrix (author / Translator / GlobalAdmin) and the
  `eventtranslation.*` audit rows are unchanged — a group event is an `Event`
  and the translation row is keyed by `EventId`, so the lane is reused as-is
  with no group-specific branch. (This differs from the group-post lane,
  which excludes the component-moderator branch; events have no
  component-moderator standing in the first place, ADR 0054 §5, so the
  group-lane event simply reuses the same matrix.)

- **Audit — per lane, always on, in-transaction (C3 / GE·5).** A feed visit
  writes **one aggregate** `AccessAudit` row via
  `CanSeeGroupFeedAsync` (TargetKind `"grouppost"` — the frozen seam's
  existing discriminator, reused — TargetId null, counts); a detail view
  writes **one decision** row via `CanSeeGroupAsync` (TargetId = the event
  id); the create gate writes one decision row (TargetId = the group id);
  **Allow and Deny** are both audited; the `IDocumentSession` overloads
  commit the row **inside the caller's transaction** (same lane as
  `CreateGroupPostAsync`), and the create gate's Deny row is committed
  **before** the throw so it survives. The **write lanes** (create/edit/
  publish) additionally store their own `event.create`-style row via the
  existing `StoreAuditRow` helper (`TargetKind = "event"`, `Via Owner`) —
  two rows on a group-event create (the gate's membership row + the write's
  owner row), mirroring how the community create stores a write row and the
  group-post create stores a gate row.

- **No break-glass, no moderator peek — a standing rule, not a deferral
  (GE·4).** A non-member moderator and a non-member GlobalAdmin are **denied**
  group events; there is no `Via = BreakGlass` branch, no
  `ModeratorAssignment` / `Component.ModeratorAccess` path, and no
  `AdminOverride` read on this lane — C5's "moderators are off by default"
  carried to its strictest reading for the platform's most
  privacy-sensitive lane (ADR 0003; the ADR 0013 G·4 precedent). This is
  **recorded here as a deliberate "not available"** and is deliberately
  **not** placed in the deferral list in Consequences: re-litigating it later
  requires an ADR 0089 amendment, per the design doc's drift-guard.

## Consequences

- **The group's "what's coming up" lands.** A small circle creates a group,
  invites the members, and now keeps its **events** (the potluck, the
  handoff, the site visit) *inside* that group — a real private events
  channel the rest of the neighborhood can neither see (FACES GE1/GE2) nor
  peek into (GE·4), and the group's membership is the only key to it, at all
  times. Events that used to be a community-wide announcement (or a group
  post that isn't really an event) get the time-aware surface they deserve:
  RSVPs, the day-before reminder, a calendar-grade `Start`/`End`.

- **The promise in ADR 0013's `how-it-works.md` extends to events.**
  Membership changes taking effect on the next request (C4) is what makes
  "when membership changes, past events reach exactly the right people"
  true, and the seam tests pin it on both sides (GE3: an added member sees;
  GE4: a removed member loses).

- **The frozen surface grows by exactly one lane, reusing the existing
  authorization.** The ADR 0006-E "named here (…)" list gains, at this lane's
  close: the four group-event methods on `IEventService`, the `Event.GroupId`
  additive, the **explicit** `GroupId == string.Empty` filter added to the
  existing `ListUpcomingAsync` / `ListInRangeAsync` candidate predicates
  (service-internal, no signature change), and the `GroupEventDraft` /
  `GroupEventUpdate` records (types, not seams) — each an ADD, each recorded
  here so the surface stays auditable, per 0006-E's "add few, add stable."
  The `M4DocTypes` surface and the `IAuthorizationService` surface gain
  **nothing** (no new index, per the `Post.GroupId` precedent; the ADR 0013
  group seams are reused verbatim).

- **The lane is checkable at its seams.** Named seam tests
  (`Kumunita.Core.Tests/GroupEventServiceTests.cs`) plus the Web controller
  tests cover every FACES row GE1–GE13, including the *absences* (no
  break-glass, no moderator branch, structurally-excluded community
  feeds/calendar, author-only edit, no GlobalAdmin peek). The invariant and
  FACES numbers (GE·1–GE·8, GE1–GE13) call on ADR 0006's **C1–C6** where they
  overlap, so the lane is checkable at its seams the same way the group-post
  lane's G·1–G·8 set is.

- **Named deferrals (carried forward; each deliberately out of scope):**
  - **Soft-delete on group events** — the community lane's
    `DeleteAsync` (author ∪ GlobalAdmin) is **not** mirrored on the group
    lane in this iteration; a group event's removal is a later concern. The
    author's only removal today is to let it pass (its `Start` moves past the
    upcoming window) — acceptable for a single-neighborhood scale, but
    named here so it is not mistaken for a feature.
  - **A group-scoped calendar** — `EV-CAL` remains community-scoped; a
    group's events appear in the group's feed (ordered by `Start`), not in
    the shared calendar. A dedicated group calendar is a later concern.
  - **A "my group events" cross-group surface** — `ListMineAsync` remains
    community-event-scoped (it reads the RSVP/authorship union over the
    community feed); a resident's group events are reached via each group's
    page. A cross-group "your group events" rollup is a later concern.
  - **Notifications on new group events** (an M6-scope item, like the
    group-post notification deferral).

- **Not deferrals — the "explicitly not available" standing rules of this
  ADR (GE·4):** break-glass on group events; moderator peek on group events;
  non-member authoring on group events; GlobalAdmin edit/publish override on
  group events. These are privacy-first answers (**no**, on purpose), not
  tickets to work off in a later milestone — changing any of them requires an
  ADR 0089 amendment, not a product decision.
