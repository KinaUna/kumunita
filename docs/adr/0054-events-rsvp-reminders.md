# ADR 0054 — M4: Events, RSVPs & reminders (a new `Kumunita.Core.Events` context reusing the frozen authorization / audience / renderer / editor / email / §6.4-job seams)

Status: Accepted
Date: 2026-09-20
Amends: **0001-B** (the `Audience` doc — *reused*, not extended),
**0006** (the frozen `IAuthorizationService` / `IAuditableResource` surface
— the new `EventToAuditableResource` adapter plugs into it, no signature
change), **0036** (the `Audience.Community` branch — the composer seeds it
`true` by default, verbatim the post convention), **0025** (the
`MarkdownRenderer` + content-image idiom — the event `Body` is rendered by
the *one* renderer), **0031** (the `bindRichEditor` — the event composer is
the *one* editor), **0037** (the `IsDraft` flag + `/my/drafts` lane —
carried onto `Event.IsDraft`), **0024** (the `IsDeleted` author
soft-delete flag — carried onto `Event.IsDeleted`), **0018** (the authored-in
language tag — carried onto `Event.LanguageCode`), **0044** (the `TagIds`
idiom — carried onto `Event.TagIds`), **0034** (the attachment idiom —
carried onto `Event.AttachmentIds`), **0019 / 0020** (the `kw-dt` TagHelper
— every event timestamp renders through it), and **0014 / 0016 / 0017**
(the author-only / author-of-record ∪ GlobalAdmin standing precedent — the
event standing matrix reuses it).

## Context

The roadmap arrows so far: **awareness** (M2 directory / groups),
**signal** (M3 posts / announcements / group posts / moderation),
**knowledge** (the `PG` pages lane). M4 is the **coordination** arrow — a
decision becomes an owned, scheduled, **reminded** action: a resident can
see that a cleanup day is happening, RSVP "going", and get a reminder email
the day before. It is the platform's first **content-with-time** surface.

**Roadmap order (recorded here, per the sign-off gate):** the **`PG`
(pages) lane is SHIPPED** — its register (U00–U07) is green, `LocalizedPage`
is fully retired (zero references in `src/`), and
`docs/plans-milestones/pages/pages-handoff-notes.md` records "the PG lane
is complete … No next unit in this lane." M4 is therefore opened **not** as
a pull-forward over in-flight work but as the **next** lane: this same
commit closes `PG` (`StatusNext` → `StatusDone`) and opens `M4`
(`StatusPlanned` → `StatusNext`) in `Milestones.cs` + the README Roadmap +
`MilestonesTests.cs` (the single-in-progress pin is renamed from `PG` to
`M4`). **M5 stays Projects; M6 stays Portability** — no roadmap letter
moves.

**The field-naming decision (recorded here):** `docs/ARCHITECTURE.md` §5
sketches the event document as `{ id, title, description, componentId?,
authorId, start, end, location?, capacity?, audience, rsvpRequired }`.
This ADR settles the canonical names as **`Body`** (rich Markdown content,
mirroring `Announcement.Body` and the RC/RE surface) and
**`ReminderEnabled`** (a bool, `true` floor — a per-event opt-*out* of the
reminder, the reminder being the default). The §5 sketch is the
pre-decision shape; **the M4 close unit (U12) syncs the ARCHITECTURE.md §5
`Events` block to these names** (per the AGENTS.md doc↔code parity rule),
so no unit in U01–U11 has to touch that file.

**The one constraint:** the milestone is **additive and reusing.** The
`Audience` doc, the `IAuthorizationService.CanAsync(Read)` decision path,
the `MarkdownRenderer` + `bindRichEditor`, the content-image / attachment
idiom, the `kw-dt` TagHelper, the `IMailerStage` + `OutboxEmail` +
`OutboxEmailHandler` trio (M1 step 7), and the self-rescheduling
`TimeoutMessage` §6.4-job shape (the `AuditPurgeHandler` precedent) are all
**frozen seams** — M4 adds **an adapter, not a branch**.

## Decision

- **A new bounded context `Kumunita.Core.Events`** with two documents:
  - **`Event`** — `Id`; `Title`; **`Body`** (Markdown — the one
    `MarkdownRenderer`, ADR 0025); `ComponentId?` (a feed filter, *never* a
    gate — C-M3·2); `AuthorId`; **`Start` / `End`** (`DateTimeOffset` UTC
    instants — the time); `Location?`; `Capacity?` (int — display metadata,
    **not** a gate); **`Audience`** (the **exact** post `Audience`, ADR
    0001-B / 0036 — `null` = public, the frozen `Decide()` branch 5);
    **`ReminderEnabled`** (bool, `true` floor — the §6.4 job's opt-out);
    `IsDraft` (bool, `true` default — ADR 0037); `IsDeleted` (bool, `false`
    default — ADR 0024); `LanguageCode` (ADR 0018 authored-in tag);
    `TagIds` (ADR 0044); `ImageIds` (ADR 0025); `AttachmentIds` (ADR 0034);
    `Created` / `Modified?`. Every field reuses an existing idiom; `Start` /
    `End` / `Location` / `Capacity` / `ReminderEnabled` are the only new
    concepts, and none of them is an access mechanism.
  - **`EventRsvp`** — `Id`; `EventId`; `UserId`;
    **`Status`** (enum `Going | Maybe | No`); `At`. Registered with a
    **unique `(EventId, UserId)` index** — the
    ARCHITECTURE.md §5 **last-write-wins concurrency exception**: a
    conflicting RSVP write is a no-op or self-converging; the resident's
    latest status is simply the truth (upsert semantics). **No
    `AccessAudit` row on an RSVP** — a routine resident action, not an
    access decision (the profile-edit posture).
  - **`M4DocTypes`** — a new document surface (the `M3DocTypes` pattern
    verbatim): `Event` (conventional `Id`) + a `(ComponentId, Start)`
    index (the feed ordering shape) + `EventRsvp` (the unique
    `(EventId, UserId)` index). **Zero migrations for existing surfaces** —
    the two docs are new, the surface is additive (ADR 0004 §B.1).
- **The audience is the post `Audience`, reused verbatim.** **No new
  `Audience` field, no new `AudienceMode`, no new `AccessVia` value.** The
  frozen `Decide()` algorithm already covers every standing an event needs
  (`Owner` / `Audience` / `Community` / `Delegation` / …). The composer
  seeds it **community-visible by default** (ADR 0036) via the
  `AudienceEditorModel` form surface verbatim — the same partial the post /
  announcement / group-post / page composers already use.
- **The decision path is the frozen `IAuthorizationService`** through a new
  **`EventToAuditableResource`** adapter — the `PostToAuditableResource`
  shape verbatim: `Id = Event.Id`; `Name = Event.Title ?? Body[..60]` (the
  57 + "..." truncation idiom); `OwnerId = Event.AuthorId`;
  `Audience = Event.Audience` (null allowed); `ComponentId =
  Event.ComponentId`; **`TargetKind = "event"`** (the **exact** string —
  the `AccessAudit` aggregate-row discriminator). A single instance per
  `Event` is safe to pass into either overload (`CanAsync` detail,
  `CanSeeAsync` feed). **No new `AccessAction`** (the existing `Read` is
  enough), **no new authorization branch**, **no new `AccessVia`** — the
  adapter is the **only** new authorization surface in this milestone.
- **The standing matrix** (enforced server-side in the `EventService` — the
  `AnnouncementService.CreateAsync` C3 pattern; the Web `[Authorize]` is a
  convenience pre-gate only):

  | Action | Standing | `AccessVia` |
  |---|---|---|
  | **Create** | any signed-in resident (becomes the author) | `Owner` |
  | **Edit** (body / time / audience / …) | the `AuthorId` ∪ GlobalAdmin | `Owner` / `Admin` |
  | **Publish** a draft | the `AuthorId` only (ADR 0037 pin) | `Owner` |
  | **Soft-delete** | the `AuthorId` ∪ GlobalAdmin (ADR 0024) | `Owner` / `Admin` |
  | **RSVP** | any resident who may read the event | *(no row — the RSVP is not an access decision)* |

  `CheckCreateStanding` / `CheckEditStanding` throw
  `UnauthorizedAccessException` (403) / `KeyNotFoundException` (404)
  exactly like `AnnouncementService`. Every audited write lane stores its
  `AccessAudit` row in the caller's session (C3), `TargetKind = "event"`,
  `Action` `event.create` / `event.update` / `event.publish` /
  `event.delete` — **no `event.rsvp` action**.
- **The draft / tag / media / language / delete lanes are reused, not
  reinvented:** the `IsDraft` flag + `/my/drafts` lane (ADR 0037); the
  `TagIds` (ADR 0044); the `ImageIds` + `AttachmentIds` (ADR 0025 / 0034 —
  the server-side body parse populates them; the client never sends them);
  the `LanguageCode` (ADR 0018); the `IsDeleted` flag + read-lane filter
  (ADR 0024). **The translation lane (ADR 0022/0026/0029) is NOT extended
  to events in M4** — an `Event` is authored-in-language only; the
  event-translation row is a follow-on lane with its own ADR.
- **The Web surface** — `EventController` (`Kumunita.Web.Controllers`):
  `GET /events` (the feed — upcoming events, `componentId` filter optional,
  `CanSeeAsync(Read)`-filtered so a private event's *existence* does not
  leak); `GET /events/{id}` (the detail view — title + rendered body via
  the *one* `MarkdownRenderer`, every timestamp via the *one* `kw-dt`
  TagHelper, the RSVP form Going/Maybe/No, the RSVP list **owner-only** —
  a non-author sees only their own RSVP; 404 on absent, 403 on denied, the
  announcement 404-vs-403 split); `GET/POST /events/new` (compose — title,
  body in the **one** `bindRichEditor`, component picker, the
  `AudienceEditorModel` verbatim, the language picker, `Start`/`End`,
  `Location`, `Capacity`, the `ReminderEnabled` checkbox — WYSIWYG with the
  RC image + ATT attachment lanes); `GET/POST /events/{id}/edit` (the edit
  lane — audience round-trips verbatim via `FromAudience`/`BuildAudience`);
  `POST /events/{id}/publish` / `delete` (the standing-gated actions);
  `POST /events/{id}/rsvp` (the last-write-wins lane, no audit row). The
  **nav entry** is one line (the `Community` nav pattern), and a draft
  event is included in the author's `/my/drafts` list. The
  `Post` / `Announcement` / `Page` surfaces stay **untouched**.
- **The `EventReminders` §6.4 job** (the second of the three jobs named in
  ARCHITECTURE.md §6.4 — `AuditPurge` shipped in M1, `VerifyDigest` stays
  deferred) follows the `AuditPurgeHandler` precedent **verbatim**:
  - **`EventReminderService`** (a Wolverine-free static class in
    `Kumunita.Core.Events`, the `AuditPurgeService` precedent):
    `SendRemindersAsync(store, options, now, ct)` — (a) load events with
    `ReminderEnabled = true` and not deleted whose `Start` is within the
    **24-hour window** (`now < Start ≤ now + 24h` — the "remind the day
    before" semantics; a past `Start` is not reminded); (b) recipients =
    the `EventRsvp` rows with `Status = Going` **plus the author, always**
    (even if they did not RSVP; `Maybe` / `No` are not reminded); (c) for
    each recipient, stage one `OutboxEmail` via the **frozen**
    `IMailerStage.StageAsync` with idempotency key
    **`remind:{eventId}:{userId}`** (the §6.2 per-email key scheme — the
    existing-row check is the no-double-send guard across ticks); (d) **no
    `AccessAudit` row** (a side effect, not an access decision — the
    verification-email posture).
  - **`EventReminderHandler`** (`Kumunita.Web/SideEffects`): a thin
    adapter — call the Wolverine-free service with a live `IDocumentStore`
    + `IOptions<EventReminderOptions>`, then **re-yield a fresh
    `EventReminderTick`** (the self-rescheduling idiom).
  - **`EventReminderTick`** — `record EventReminderTick() :
    Wolverine.TimeoutMessage(TimeSpan.FromDays(1))` (the `AuditPurgeTick`
    precedent verbatim — the delay is baked into the message type, so every
    re-publish carries the same schedule; no per-callsite `DelayedFor`).
  - **`EventReminderOptions`** — the `AuditPurgeOptions` shape
    (`WindowHours = 24` floor). **Wiring:** `Program.cs` registers the
    tick next to the `AuditPurgeTick` (durable scheduling —
    `IntegrateWithWolverine()`).
  - **Best-effort, re-runnable** (ARCHITECTURE.md §6.2 — "Event reminder /
    Best-effort / Re-runnable scheduled job; dead-letter if SMTP truly
    down"): a failed SMTP send dead-letters in `EmailDeadLetter`; the
    `IMailerStage` + `OutboxEmail` + `OutboxEmailHandler` trio is
    **untouched** (no new method, no new email channel, no new SMTP
    config) — the job only *stages* rows.
- **The 23 pinned seam test names** (the design doc §3.7 is the master
  list, locked here) — 18 in `EventServiceTests` (T01–T18: the feed
  family, the `null`-audience-public branch, the `Community` + grants
  branches, the draft pin, the standing matrix, the publish author-only
  pin, the soft-delete filter, the RSVP last-write-wins + unique-index +
  owner-only-list + no-audit-row pins, the audit-row shape, the adapter
  shape) + 5 in `EventReminderServiceTests` (T19–T23: the window filter,
  the `Going`-RSVP filter, the author-inclusion rule, the
  idempotency-key shape, the no-audit-row pin).
- **The three-test acceptance gate** (the design doc §3.8, recorded by the
  gate unit): **closed loop** (an author creates a published event → it
  appears in the feed with the `TargetKind = "event"` aggregate row; the
  author RSVPs → their RSVP is visible in the owner-only list), **handoff**
  (a user added to the audience's grants after creation sees the event on
  the next request — strong consistency; the `Delegation` branch is the
  handoff-onto-a-delegate case), **part-vs-whole** (the 23 names are the
  whole; all pass together in the same run as the inherited M1–M3 / PG
  anchors).
- **The drift-guard** (the design doc §3.9): the `Event` field set
  (including the `Body` / `ReminderEnabled` naming decision), the
  `EventRsvp` shape, the `EventToAuditableResource` adapter, the
  `IEventService` public method set, the 23 test names, the three-test
  gate, and the §6.4 job shape are **frozen** — a re-shape of any of them
  is a new ADR, a rename of a test name is a drift event, and a new
  Wolverine idiom is a new unit, not a silently-omitted one.

## Consequences

- **One authorization path, one audience, one renderer, one editor, one
  email trio, one timezone/format resolver** — the milestone *reuses*
  instead of *duplicating*: the `Event` gets private/community/public
  visibility, the correct `AccessVia` audit tag, and delegation for free
  (the `Decide()` algorithm is unchanged; only the
  `EventToAuditableResource` adapter is new).
- **The reminder email is a side effect on a working event, not the other
  way round** — the §6.4 job lands after the read + write lanes are green
  (the register's sequencing invariant), and it touches no seam: the
  frozen `IMailerStage` trio already owns dispatch, retry, and dead-letter.
- **The RSVP is deliberately light** — one row per user, last-write-wins,
  no audit row: a neighborhood RSVP is a routine action, and the
  milestone gains nothing from treating it like an access decision.
- **`Post` / `Announcement` / `Page` stay untouched** — M4 is additive on
  top of the shipped lanes; the new surface is a new context + a new doc
  surface, so nothing existing is re-shaped.
- **The ARCHITECTURE.md §5 `Events` block is out of date by design
  right now** (`description` / `rsvpRequired` vs. the settled
  `Body` / `ReminderEnabled`) — the close unit (U12) syncs it, per the
  AGENTS.md doc↔code parity rule.
- **Out of scope (deliberate non-decisions):** iCal export (M6),
  notifications-as-a-lane (M6), group events (a follow-on lane, own ADR),
  event translations (a follow-on lane, own ADR), per-resident reminder
  settings (a follow-on lane), and the `VerifyDigest` §6.4 job (stays
  deferred).

## Tests

- **Core (`Kumunita.Core.Tests`, DB-backed)** — `EventServiceTests`
  (T01–T18, §3.7): the feed + audience branch family (`M4_*`, the
  `A0036_*` family shape), the draft pin, the standing matrix, the
  publish/delete lanes, the RSVP pins, the audit-row + adapter shapes.
  `EventReminderServiceTests` (T19–T23): the Wolverine-free harness shape
  of `AuditPurgeServiceTests` — window filter, `Going` filter, author
  inclusion, idempotency-key shape, no-audit-row.
- **Web (`Kumunita.Web.Tests`)** — `EventControllerTests` (NSubstitute
  `IEventService`, no live Postgres — the `AnnouncementControllerTests`
  shape): the route map, the 404-vs-403 split, the feed filter, the
  composer/edit audience round-trip, the RSVP form.
- **`MilestonesTests.cs`** — the `Ids` ordered list is **unchanged**
  (`… PG, M4, M5, M6`); the `Shipped` set gains `"PG"`; the
  single-in-progress pin is **renamed** `PG_Is_The_Single_InProgress_Milestone`
  → `M4_Is_The_Single_InProgress_Milestone` (asserting `M4`) in the same
  commit as the `Milestones.cs` + README status flips.
