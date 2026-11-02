---
id: adr-0054
title: "M4: Events, RSVPs & reminders (a new `Kumunita.Core.Events` context reusing the frozen authorization / audience / renderer / editor / email / §6.4-job seams)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/events-calendar-design.md]
supersedes: [adr-0001]
superseded_by: [adr-0059, adr-0063, adr-0065, adr-0165]
updated: 2026-11-02
---

## Context

The roadmap arrows so far: **awareness** (M2 directory / groups), **signal**
(M3 posts / announcements / group posts / moderation), **knowledge** (the
`PG` pages lane). M4 is the **coordination** arrow — a decision becomes an
owned, scheduled, **reminded** action: a resident can see that a cleanup day
is happening, RSVP "going", and get a reminder email the day before. It is the
platform's first **content-with-time** surface.

**Roadmap order (recorded here, per the sign-off gate):** the **`PG` (pages)
lane is SHIPPED** — its register (U00–U07) is green, `LocalizedPage` is fully
retired. M4 is opened as the **next** lane: this same commit closes `PG`
(`StatusNext` → `StatusDone`) and opens `M4` (`StatusPlanned` → `StatusNext`)
in `Milestones.cs` + the README Roadmap + `MilestonesTests.cs`. **M5 stays
Projects; M6 stays Portability** — no roadmap letter moves.

**The field-naming decision (recorded here):** the [data
model](../architecture/data-model.md) sketches the event document as
`{ … description …, rsvpRequired }`. This ADR settles the canonical names as
**`Body`** (rich Markdown content, mirroring `Announcement.Body`) and
**`ReminderEnabled`** (a bool, `true` floor — a per-event opt-*out* of the
reminder, the reminder being the default). **The M4 close unit (U12) syncs
the [data model](../architecture/data-model.md) `Events` block to these names**
(per the AGENTS.md doc↔code parity rule).

**The one constraint:** the milestone is **additive and reusing.** The
`Audience` doc, the `IAuthorizationService.CanAsync(Read)` decision path, the
`MarkdownRenderer` + `bindRichEditor`, the content-image / attachment idiom,
the `kw-dt` TagHelper, the `IMailerStage` + `OutboxEmail` + `OutboxEmailHandler`
trio (M1 step 7), and the self-rescheduling `TimeoutMessage`
[CQRS-lite §6.4 job](../architecture/cqrs-lite-wolverine.md) shape are all
**frozen seams** — M4 adds **an adapter, not a branch**. The full moved detail
(the two documents' field sets, the standing matrix, the Web surface, the
`EventReminders` §6.4 job, the 25 pinned test names, the three-test gate, the
drift-guard) lives in the [handoff
note](../design/adr-handoffs.md#adr-0054--m4--events-rsvps--reminders-moved-
detail).

## Decision

- **A new bounded context `Kumunita.Core.Events`** with two documents —
  **`Event`** (`Id`; `Title`; **`Body`** Markdown; `ComponentId?`; `AuthorId`;
  **`Start` / `End`**; `Location?`; `Capacity?`; **`Audience`**;
  **`ReminderEnabled`**; `IsDraft`; `IsDeleted`; `LanguageCode`; `TagIds`;
  `ImageIds`; `AttachmentIds`; `Created` / `Modified?`) and **`EventRsvp`**
  (`Id`; `EventId`; `UserId`; **`Status`** enum `Going | Maybe | No`; `At`,
  with a **unique `(EventId, UserId)` index** — last-write-wins; **no
  `AccessAudit` row on an RSVP**) — registered on a new surface
  **`EventDocTypes`**. **Zero migrations for existing surfaces** (ADR 0004
  §B.1). *See the handoff note for the full field sets + index shapes.*
- **The audience is the post `Audience`, reused verbatim.** **No new
  `Audience` field, no new `AudienceMode`, no new `AccessVia` value.** The
  frozen `Decide()` algorithm already covers every standing an event needs.
  The composer seeds it **community-visible by default** (ADR 0036) via the
  `AudienceEditorModel` form surface verbatim.
- **The decision path is the frozen `IAuthorizationService` through a new
  `EventToAuditableResource` adapter** (the `PostToAuditableResource` shape
  verbatim: `Id = Event.Id`; `Name = Event.Title ?? Body[..60]`;
  `OwnerId = Event.AuthorId`; `Audience = Event.Audience`; `ComponentId =
  Event.ComponentId`; **`TargetKind = "event"`**). **No new `AccessAction`**
  (the existing `Read` is enough), **no new authorization branch**, **no new
  `AccessVia`** — the adapter is the **only** new authorization surface.
- **The standing matrix** (enforced server-side in the `EventService`):
  Create any signed-in resident / `Owner`; Edit `AuthorId` ∪ GlobalAdmin /
  `Owner` / `Admin`; Publish the `AuthorId` only / `Owner`; Soft-delete
  `AuthorId` ∪ GlobalAdmin / `Owner` / `Admin`; RSVP any resident who may read
  / *no row*. `CheckCreateStanding` / `CheckEditStanding` throw
  `UnauthorizedAccessException` (403) / `KeyNotFoundException` (404) exactly
  like `AnnouncementService`; audited lanes store `TargetKind = "event"`,
  `Action` `event.create` / `event.update` / `event.publish` / `event.delete`
  — **no `event.rsvp` action**. *See the handoff note for the full table.*
- **The draft / tag / media / language / delete lanes are reused, not
  reinvented:** the `IsDraft` flag + `/my/drafts` lane (ADR 0037); the
  `TagIds` (ADR 0044); the `ImageIds` + `AttachmentIds` (ADR 0025 / 0034); the
  `LanguageCode` (ADR 0018); the `IsDeleted` flag + read-lane filter (ADR 0024).
  **The translation lane is NOT extended to events in M4's own scope** — the
  user-added event-translation lane ships as a separate named lane under
  **ADR 0059**.
- **The Web surface** — `EventController`: `GET /events` (the feed,
  `CanSeeAsync(Read)`-filtered so a private event's *existence* does not leak);
  `GET /events/{id}` (the detail view, the RSVP form, the RSVP list
  **owner-only**); `GET/POST /events/new` (compose); `GET/POST /events/{id}/
  edit`; `POST /events/{id}/publish` / `delete`; `POST /events/{id}/rsvp`
  (no audit row). The `Post` / `Announcement` / `Page` surfaces stay
  **untouched**. *See the handoff note for the full route list.*
- **The `EventReminders` §6.4 job** (the second of the three jobs named in
  [CQRS-lite & side effects](../architecture/cqrs-lite-wolverine.md) §6.4)
follows the `AuditPurgeHandler` precedent **verbatim**: **`EventReminderService
`** (a Wolverine-free static class) loads events with
  `ReminderEnabled = true` whose `Start` is within the **24-hour window**,
  reminds the `Going` RSVPs **plus the author always**, stages one
  `OutboxEmail` each via the **frozen** `IMailerStage.StageAsync` with key
  **`remind:{eventId}:{userId}`**, and writes **no `AccessAudit` row**.
  **`EventReminderHandler`** is a thin adapter that re-yields a fresh
  `EventReminderTick` (a `TimeoutMessage(TimeSpan.FromDays(1))`). The
  reminder's "when" renders in each **recipient's** own time zone + date-time
  format (ADR 0019 / 0020). *See the handoff note for the full job shape.*
- **Pinned and gated.** The **25 pinned seam test names** (the design doc
  §3.7 is the master list, locked here) + the **three-test acceptance gate**
  (closed loop, handoff, part-vs-whole) + the **drift-guard** (the design doc
  §3.9 — the `Event` field set, the `EventRsvp` shape, the adapter, the
  `IEventService` public method set, the 25 test names, the gate, and the
  §6.4 job shape are **frozen**). *See the handoff note for the full test-
  name list, the gate, and the drift-guard.*

## Consequences

- **One authorization path, one audience, one renderer, one editor, one email
  trio, one timezone/format resolver** — the milestone *reuses* instead of
  *duplicating*: the `Event` gets private/community/public visibility, the
  correct `AccessVia` audit tag, and delegation for free (the `Decide()`
  algorithm is unchanged; only the `EventToAuditableResource` adapter is new).
  The reminder's "when" renders through the **same** ADR 0019 / 0020
  resolution order the `kw-dt` TagHelper uses.
- **The reminder email is a side effect on a working event, not the other way
  round** — the §6.4 job lands after the read + write lanes are green, and it
  touches no seam: the frozen `IMailerStage` trio already owns dispatch,
  retry, and dead-letter.
- **The RSVP is deliberately light** — one row per user, last-write-wins, no
  audit row: a neighborhood RSVP is a routine action.
- **`Post` / `Announcement` / `Page` stay untouched** — M4 is additive on top
  of the shipped lanes.
- **The [data model](../architecture/data-model.md) `Events` block is out of
  date by design right now** — the close unit (U12) syncs it.
- **Out of scope (deliberate non-decisions):** iCal export (M6),
  notifications-as-a-lane (M6), group events (a follow-on lane), per-resident
  reminder settings (a follow-on lane), and the `VerifyDigest` §6.4 job (stays
  deferred). The event-translation lane shipped under **ADR 0059**.
