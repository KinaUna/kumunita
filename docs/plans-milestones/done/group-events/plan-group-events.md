# Group events — membership-scoped group events lane — sealed unit register

## Understanding
M2 (ADR 0010/0013) shipped groups with a **posts** channel — a private channel
whose access is *membership* (the ADR 0013 lane: members see, a non-member —
including a GlobalAdmin — 404s; a membership add/remove re-scopes the next
request). M4 (ADR 0054) shipped a full **event** surface (community-scoped:
a `ComponentId` organizer, an `Audience` boundary, drafts, RSVPs,
day-before reminders, translations) — but **only on the community surface**.
A group's page shows posts and members, and "create an event" only ever
reaches the community feed. The gap: a small circle that keeps its
discussions *inside* a group has nowhere to put the *events* those
discussions are about. **Group events = the ADR 0013 membership lane applied
to the M4 event surface**, reusing the frozen group seams verbatim (zero new
authorization surface). This is a new **named lane — `GE`** — no milestone
letter (the group-posts precedent; the roadmap M-letters stay
Events/Projects/Portability).

**Scope decisions (user, 2026-09-25 — "proceed with A"):** Option A,
membership-scoped, mirroring ADR 0013. Visibility = current members only;
membership the sole decision; lane exclusivity; members-only authoring;
author-only edit/publish (ADR 0016/0037 precedent); 404 fail-closed; group
events on the group page; excluded from the community feed/calendar.

## Assumptions (pinned; the design doc makes them exact)
- **Membership, not audience (GE·1, C4).** A group event's visibility =
  `Event.GroupId` membership, via the **frozen** ADR 0013 group seams
  (`CanSeeGroupAsync` / `CanSeeGroupFeedAsync`, live `GetGroupIdsAsync`).
  **Zero new authorization surface.** The audience lane is **never
  evaluated**; the event's `Audience` is written non-null **empty** (GE·8).
- **Data is one additive `Event` field, no new index (GE·2/GE·8).**
  `Event.GroupId` (`string`, default `string.Empty`) — ADR 0004 §B.1
  additive, the `Post.GroupId` precedent (delta-detected, idempotent, no
  re-seed, **no** `M4DocTypes` index). Written only by
  `CreateGroupEventAsync`, which pins `ComponentId = string.Empty` and
  `Audience = new Audience()`. `EventService`'s constructor is **unchanged**.
- **The community read seams gain an explicit filter (GE·2).**
  `ListUpcomingAsync` / `ListInRangeAsync` candidate predicates gain
  `GroupId == string.Empty` — **service-internal, no signature change.** This
  is *required* (unlike posts, an event and a group event are the same doc —
  the only way a group event stays off the community feed/calendar).
- **Members-only authoring (GE·3).** The create gate **is** the group-lane
  decision (Allow ⇒ may create; Deny ⇒ `UnauthorizedAccessException`, the
  Deny row committed **before** the throw so it survives; Web 404).
- **No break-glass, no moderator, author-only write (GE·4, C5 / G·4
  strictest).** Edit and publish are **author-only** (the ADR 0016 / ADR 0037
  group-lane precedent); a non-author (incl. GlobalAdmin) is denied (Web 404).
  **Explicitly not available**, not a deferral (ADR 0089).
- **RSVP + reminders inherited (GE·7).** Reuse `RsvpAsync` /
  `GetMyRsvpAsync` / `GetRsvpsAsync` as-is (keyed by `EventId`); the §6.4
  reminder job already covers group events (the Going-RSVP set is
  membership-derived by construction). No new surface, no new notification
  kind, no audit row.
- **Translations reused (GE·5).** The ADR 0059 `EventTranslation` seams are
  reused as-is (keyed by `EventId`, no group branch; events have no
  component-moderator standing, ADR 0054 §5).
- **Audit (GE·5, C3).** Feed = one **aggregate** row (TargetKind `"grouppost"`
  — the frozen seam's existing discriminator, reused — TargetId null,
  counts); detail = one **decision** row (TargetId = the event id); the
  create gate = one decision row (TargetId = the group id). Always (Allow
  **and** Deny), in-transaction via the session overload. The write lanes
  additionally store an `event.create`-style row via `StoreAuditRow`
  (TargetKind `"event"`, `Via Owner`) — mirroring the community create.
- **Delegation (GE·6, C2).** An in-scope `read` delegate acts with the
  owner's standing (visible iff the owner is a member; `Via` = Delegation);
  out-of-scope denies. Reused from the ADR 0013 `DecideGroupAsync`.
- **Web surface (B track).** The group page grows an events channel: an
  events section on the group detail (feed + "New event") + routes mirroring
  the group-post routes under `/groups/{id}/events` (composer, detail, edit,
  publish, RSVP). Thin `GroupsController` actions (the M2 group lanes), plain
  form posts, **no** new TS. Non-member: 404.
- **kw-l (C track).** All new strings in **all four** active languages
  (`en`/`de`/`fr`/`da`) — the ADR 0005 "all keys in every language" rule.
- **Out of scope (carried forward, named — ADR 0089 Consequences):**
  soft-delete on group events (the community `DeleteAsync` is not mirrored on
  the group lane this iteration); a group-scoped calendar; a cross-group "my
  group events" surface; notifications on new group events (M6).
- **Test model (unchanged).** xunit.v3: on this machine the discovery path
  (VS Test Explorer / `dotnet test`) is a known-broken bug — the reliable
  runner is `dotnet build` + `dotnet exec <test-assembly>.dll` (AGENTS.md).
  Three-test acceptance gate (closed loop / handoff / part-vs-whole) +
  invariant-anchored seam list (19 tests, pinned in design doc §seams).

## Approach
Three tracks, sequenced — exactly like group posts. **Track A (Core + ADR):**
ADR 0089 (done), design doc (done), the `Event.GroupId` additive + the new
records + the `IEventService` ADDs + the `EventService` group lanes (U04).
**Track B (Web):** `GroupsController` group-event actions + view models +
views (U05), the kw-l strings (U06). **Track C (Tests + close):** the 19
pinned seam tests + the Web 404 tests (U07), run + record the gate (U08),
docs close + folder move (U08).

Each **code** unit ends **build green**. Each **test** unit verifies with the
`dotnet exec` assemblies runner (not `dotnet test`). Doc units (ADR/design
doc) never build.

## Pinned contract (directional — the design doc §seams is authoritative)

```
Event.GroupId : string, default string.Empty        // non-empty ⇒ group-event lane (GE·2/GE·8)

// IEventService ADDs (frozen M4 signatures untouched — ADR 0006-E lane):
Task<GroupEventFeedResult> ListGroupEventsAsync(string groupId, string actorId, int page, CancellationToken ct = default);
Task<Event?>              GetGroupEventAsync(string groupId, string eventId, string actorId, CancellationToken ct = default);
Task<Event>               CreateGroupEventAsync(GroupEventDraft draft, string actorId, IDocumentSession session, CancellationToken ct = default);
Task<Event>               UpdateGroupEventAsync(string eventId, string actorId, GroupEventUpdate update, IDocumentSession session, CancellationToken ct = default);
// Publish reuses EventService.PublishAsync; RSVP reuses RsvpAsync/GetMyRsvpAsync/GetRsvpsAsync;
// translations reuse the ADR 0059 EventTranslation seams. No new IAuthorizationService method.
// ListUpcomingAsync / ListInRangeAsync gain: ... && e.GroupId == string.Empty

// Records (Kumunita.Core.Events):
public sealed record GroupEventFeedResult(IReadOnlyList<Event> Visible, int HiddenCount, int Page, int Total);
public sealed record GroupEventDraft(string GroupId, string Title, string Body, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? Location = null, int? Capacity = null, string? Color = null, string? LanguageCode = null, bool? ReminderEnabled = null);
public sealed record GroupEventUpdate(string Title, string Body, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? Location = null, int? Capacity = null, string? Color = null, string? LanguageCode = null, bool? ReminderEnabled = null);
```

## Invariants (pinned for group events)
GE·1 — membership sole decision (C4 strong consistency); the audience lane is never evaluated; `Audience` written empty (GE·8).
GE·2 — lane exclusivity: a group event has `GroupId` non-empty, `ComponentId = string.Empty`, excluded from the community feed/calendar by the explicit filter.
GE·3 — members-only authoring: create gate = group-lane decision; Deny row survives the throw; Web 404.
GE·4 — nobody peeks, author-only write: no break-glass, no moderator, no GlobalAdmin override; edit/publish author-only.
GE·5 — audit per lane: feed aggregate / detail decision / create-gate decision; Allow **and** Deny; in-transaction.
GE·6 — delegation action-scoped (C2): in-scope `read` ⇒ owner's standing; `Via` Delegation.
GE·7 — RSVP + reminders inherited (reuse the lane-neutral seams; no new surface/audit row).
GE·8 — write shape pinned: `CreateGroupEventAsync` writes `GroupId`, `ComponentId = string.Empty`, `Audience = new Audience()`; `EventService` ctor unchanged.

## FACES (pinned, 13) — see the design doc table GE1–GE13.

## Units (5 total — one plan file each, or inline for this single-agent run)

| U | Goal (one line) |
|---|---|
| U03 | ADR 0089 + design doc + this register (done) |
| U04 | Core: `Event.GroupId` + `GroupEventFeedResult`/`GroupEventDraft`/`GroupEventUpdate` + the 4 `IEventService` ADDs + the `EventService` group lanes + the community-feed filter **(done)** |
| U05 | Web: `GroupsController` group-event actions + `GroupEventRow`/`GroupEventComposeViewModel`/`GroupEventDetailViewModel` + the group-detail events section + composer/detail views **(done)** |
| U06 | kw-l strings × 4 languages (`en`/`de`/`fr`/`da`) **(done)** |
| U07 | Tests: `GroupEventServiceTests.cs` (19) + Web 404 controller tests **(done)** |
| U08 | Build green + `dotnet exec` both assemblies + record gate + doc sync (README roadmap lane mention; ADR status) **(done — gate recorded in the design doc `## Group events — Gate (recorded)`; Core 851/851, Web 466/466)** |

## Workflow
Single fresh agent for this run. Doc units (U03) never build. Each code unit
(U04/U05/U06) ends build green (`dotnet build Kumunita.slnx -c Debug`). U07
verifies with the `dotnet exec` assemblies runner (NOT `dotnet test`). U08
records the gate and closes the docs.
