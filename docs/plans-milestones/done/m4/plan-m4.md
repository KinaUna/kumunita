# M4 — Events, RSVPs & reminders — sealed unit register

> **In progress.** This is the **lane plan** (the secondary register tier) for the
> **M4** milestone — the **coordination** arrow (a decision becomes an owned,
> scheduled, reminded action). The **primary reference tier** (the exact C#
> seams + the design decisions) is the design doc
> `docs/design/m4-events-design.md`; the **scratch tier** is
> `docs/plans-milestones/done/m4/m4-handoff-notes.md` (one appended
> `## U#` section per unit, never rewritten).
>
> **What this is:** the first **content-with-time** milestone: a new bounded
> context `Kumunita.Core.Events` (the `Event` + `EventRsvp` documents), one
> **new document surface** (`M4DocTypes`), a **new service seam**
> (`IEventService`), a **new Web surface** (`EventController` + views), and
> **one new §6.4 scheduled job** (`EventReminders`, the second of the three §6.4
> jobs — `AuditPurge` shipped in M1; `VerifyDigest` stays deferred) built on the
> **frozen** `IMailerStage` + `OutboxEmail` + `OutboxEmailHandler` trio.
>
> **The one thing every unit must respect:** this milestone is **additive and
> reusing.** It reuses the `Audience` doc (ADR 0001-B / 0036 — *reused*, not
> extended), the `IAuthorizationService.CanAsync(Read)` decision path
> (ADR 0006) through an **`EventToAuditableResource`** adapter (the
> `PostToAuditableResource` shape verbatim — the same 6-member
> `IAuditableResource` surface, `TargetKind = "event"`), the
> **`MarkdownRenderer` + `bindRichEditor`** authoring surface (ADR 0025 / 0031)
> for the event body, the **content-image / attachment** idiom (ADR 0025 / 0034)
> for in-body images and file references, the **`kw-dt`** TagHelper (ADR 0019 /
> 0020) for every timestamp, the **`IMailerStage` + `OutboxEmail`** seam
> (M1 step 7) for the reminder email, and the **self-rescheduling
> `TimeoutMessage`** job shape (the `AuditPurgeHandler` precedent — the
> sanctioned §6.4 recurring-job idiom). **No new `AccessAction`** (the existing
> `Read` is enough), **no new `AccessVia` value** (events use the settled
> `Owner` / `Audience` / `Delegation` branches), **no new authorization path**,
> **no editor dependency**, **zero migrations for new fields** (ADR 0004 §B.1
> additive — the two docs are new, not additive; the surface is additive).
> **M5 stays Projects; M6 stays Portability.**
>
> **Sizing:** units are sized for a ~32K-context fresh agent one at a time,
> each with its own exit criteria, in the `RC` / `RE` / `ATT` / `PG` style.
> **U00 is the sign-off gate** — it locks the **[PROPOSED]** decisions in the
> design doc into an accepted ADR before any code is written; every later unit
> codes against the *locked* text. **Sequencing invariant:** the §6.4 scheduled
> job (U07 the Wolverine-free service, U08 the handler + tick) lands **after**
> the read + write lanes are green (U04–U06), so the
> reminder email is a *side effect* on top of a working event — not the other
> way round. The M4 acceptance gate (U11) runs **after** all tests (U10) are
> green.

## Understanding (one paragraph)

M3 shipped **signal** (posts, replies, announcements, group posts, moderation).
M2 shipped **awareness** (directory, profile, groups). M4 is the next arrow —
**coordination**: a resident can see that a cleanup day is happening, RSVP
"going", and get a reminder email the day before. It introduces **one new
bounded context** (`Kumunita.Core.Events`), **two new documents** (`Event`,
`EventRsvp`), **one new service seam** (`IEventService`), **one new document
surface** (`M4DocTypes`), **one new Web controller** (`EventController`) +
views, and **one new §6.4 scheduled job** (`EventReminders`) — the second of
the three §6.4 jobs named in `docs/ARCHITECTURE.md` §6.4. The authorization
path is the **frozen** `IAuthorizationService` through a new
`EventToAuditableResource` adapter; the audience is the **exact** post
`Audience` (ADR 0001-B / 0036); the body is rendered by the **one**
`MarkdownRenderer` and edited by the **one** `bindRichEditor`; every
timestamp is rendered by the **one** `kw-dt` TagHelper; the reminder email
goes through the **frozen** `IMailerStage` + `OutboxEmail` + durable handler
trio. This is the first milestone that **introduces a new scheduled job** —
`EventReminders` is the second of the three §6.4 jobs, and it follows the
`AuditPurgeHandler` self-rescheduling `TimeoutMessage` precedent exactly
(same shape, same idiom, same durable-storage guarantee).

## Assumptions

- **Scope = events + RSVP + reminders.** In: the `Event` + `EventRsvp`
  documents, the `M4DocTypes` surface, the `IEventService` read + write
  surface, the `EventToAuditableResource` adapter, the `EventReminders`
  §6.4 job (the `EventReminderService` + `EventReminderHandler` +
  `EventReminderTick` trio), the `EventController` + views + nav entry, the
  23 pinned seam tests, and the acceptance gate. **Out (→ M5 / M6 or a
  follow-on lane):** iCal export (M6, per the README roadmap),
  notifications-as-a-lane (M6 owns notification surfaces), group events
  (a follow-on lane, own ADR), event translations (a follow-on lane, own
  ADR — the UGC-translation lane (ADR 0022/0026/0029) is not extended to
  events in this milestone), per-event reminders-as-a-lane (the single
  24-hour-before reminder is the M4 surface; a "remind me N hours before"
  resident setting is a follow-on lane), and the `VerifyDigest` §6.4 job
  (stays deferred — M1 shipped only `AuditPurge`).
- **The `Event` document is a new bounded context**, not an additive field on
  an existing one — the same shape as `Post` / `Announcement` / `Page`
  (its own POCO, its own `EventService`, its own `EventToAuditableResource`
  adapter, its own `EventDocTypes` surface registered next to the existing
  surfaces).
- **The `Audience` doc is reused verbatim** — the `Event.Audience` field is
  the *exact* `Post.Audience` shape (ADR 0001-B / 0036; the
  `AudienceEditorModel` + `BuildAudience()` form surface is reused from M2,
  the same partial the post / announcement / group-post composers already
  use). **No new audience mechanism.**
- **The `EventRsvp` is last-write-wins, keyed per user** (the
  `docs/ARCHITECTURE.md` §5 concurrency-token exception — a conflicting RSVP
  write is a no-op or self-converging: the resident's latest status is simply
  the truth). **No `AccessAudit` row on an RSVP** (a routine resident action,
  not an access decision — the same posture as a profile edit).
- **The reminder email is best-effort** (the `docs/ARCHITECTURE.md` §6.2
  email-kind table — "Event reminder / Best-effort / Re-runnable scheduled
  job; dead-letter if SMTP truly down"). The `IMailerStage` +
  `OutboxEmailHandler` trio is reused verbatim; the §6.4 job is the
  `AuditPurgeHandler` self-rescheduling `TimeoutMessage` precedent (the
  sanctioned recurring-job idiom). **No new email channel, no new SMTP
  config, no new `IMailerStage` method.**
- **The `Event` body is rich content** (ADR 0025 / 0031) — the same
  `MarkdownRenderer` + `bindRichEditor` surface the post / announcement /
  group-post / page composers already use. **No new renderer, no new editor,
  no new TS module.**
- **The `Event` timestamps are `kw-dt`** (ADR 0019 / 0020) — the same
  per-request resolver (resident override → platform default → `UTC` floor)
  and format (resident override → platform default → Long floor) the other
  surfaces already use. **No new timezone or format mechanism.**
- **Standing is the existing role matrix** (GlobalAdmin ∪ author) re-checked
  server-side in the `EventService` (the `AnnouncementService.CreateAsync`
  C3 pattern); the Web `[Authorize]` is a convenience pre-gate only. **The
  author is the standing owner** (ADR 0014 / 0016 / 0017 — the
  post / group-post / announcement edit-lane precedent: author-only edit,
  GlobalAdmin override).
- **The `Event` is draftable** (ADR 0037 — the `IsDraft` flag, the
  author-only pin, the `/my/drafts` lane) — the same shape as the post /
  group-post / announcement draft lane. **No new draft mechanism.**
- **The `Event` is in the authored-in language** (ADR 0018 — the
  `LanguageCode` field, the same shape as the other UGC surfaces). **No new
  language mechanism.**
- **The `Event` is tagged** (ADR 0044 — the `TagIds` field, the same shape
  as the other UGC surfaces). **No new tag mechanism.**
- **The `Event` is media-attached** (ADR 0025 / 0034 — the `ImageIds` +
  `AttachmentIds` fields, the same shape as the other UGC surfaces). **No new
  media mechanism.**
- **The `Event` is audience-restricted** (ADR 0001-B / 0036 — the
  `Audience` field, the same shape as the other UGC surfaces). **No new
  audience mechanism.**
- **The `Event` is soft-deletable** (ADR 0024 — the `IsDeleted` flag, the
  same shape as the other UGC surfaces). **No new delete mechanism.**
- **The `Event` is translation-eligible** (ADR 0022 / 0026 / 0029 — the
  `EventTranslation` row shape, the same shape as the other UGC surfaces).
  **This milestone does NOT extend the translation lane to events** (the
  follow-on lane owns that); the `Event` is authored-in-language only in
  M4.
- **The `Event` is reminder-eligible** (this milestone — the
  `ReminderEnabled` flag + the `EventReminders` §6.4 job). **No new
  notification mechanism** (the email is the one channel; the
  `IMailerStage` + `OutboxEmailHandler` trio is reused verbatim).

## The one thing every unit must respect

**Additive and reusing.** Each unit adds **no** second audience mechanism,
**no** second authorization path, **no** second renderer, **no** second
editor binding, **no** new `AccessAction` / `AccessVia` / authorization
branch, **no** new email channel, **no** new timezone or format mechanism,
**no** new draft mechanism, **no** new tag mechanism, **no** new media
mechanism, **no** new language mechanism, **no** new delete mechanism, **no**
editor dependency. The event's `Audience` *is* the post `Audience`; the
event's body is rendered by the *one* `MarkdownRenderer` and edited by the
*one* `bindRichEditor`; the event's timestamps are rendered by the *one*
`kw-dt` TagHelper; the event's reminder email goes through the *frozen*
`IMailerStage` + `OutboxEmail` + `OutboxEmailHandler` trio; the event's
draft / tag / media / language / delete lanes reuse the *frozen*
post / announcement / page surfaces. The **frozen** `IAuthorizationService`
is the only decision path — M4 adds an *adapter*, not a *branch*.

## The units

### U00 — Sign-off + ADR 0054 + the `M4` milestone row  *(no code beyond the ADR)*

Lock the **[PROPOSED]** decisions in `docs/design/m4-events-design.md` into
the accepted **ADR 0054** (`docs/adr/0054-events-rsvp-reminders.md`): the
`Event` field set (§3.1), the `EventRsvp` shape + last-write-wins
concurrency exception (§3.2), the **`EventToAuditableResource`** adapter
(§3.3), the standing matrix (author-only edit, GlobalAdmin override — §3.4),
the draft / tag / media / language / delete lane reuse (§3.5), the
`EventReminders` §6.4 job shape (the `AuditPurgeHandler` self-rescheduling
`TimeoutMessage` precedent, the `IMailerStage` + `OutboxEmail`
idempotency-key scheme `remind:{eventId}:{userId}` — §3.6), the **23
pinned seam test names** (§3.7), and the **three-test acceptance gate**
(§3.8). Add the `M4` row to `Milestones.All` (a named lane,
`StatusNext` when work starts / `StatusDone` on ship — **not** a renumber)
**and** the README Roadmap in the *same* commit; keep `MilestonesTests.cs`
in step (the order + single-in-progress pin). **Roadmap order:** the
**completed** `PG` (Pages) lane is closed (`PG → StatusDone`) *and* `M4` is
opened (`M4 → StatusNext`) in the *same* commit — Pages already shipped
(`PG` U00–U07 green, `LocalizedPage` retired; the lane's handoff notes record
"No next unit in this lane"), so M4 is simply the **next** lane, *not* a
pull-forward over in-flight work. `Milestones.cs` + README + `MilestonesTests`
all move together: `PG` joins the `StatusDone` set, `M4` becomes the single
`StatusNext`, and the in-progress pin is renamed to name `M4` (not `PG`).
ADR 0054's *Context* records this (coordination is the next resident-facing
arrow; the single-in-progress pin moves to M4 as PG closes).
**Exit:** ADR 0054 accepted + the design doc's `[PROPOSED]` markers resolved
to `[DECIDED]` in the ADR's decision section; `Milestones.cs` + README +
`MilestonesTests` green; **no other code changed.**

### U01 — `Event` + `EventRsvp` docs + `M4DocTypes` registration + boot wiring

Add `Kumunita.Core.Events`: `Event` (the §3.1 field set) + `EventRsvp`
(the §3.2 shape: `Id`/`EventId`/`UserId`/`Status: Going|Maybe|No`/`At`).
Register them on a new **`M4DocTypes`** surface (the `M3DocTypes` pattern
verbatim): `Event` (conventional `Id`) + `(ComponentId, Start)` index (the
feed ordering shape) + `EventRsvp` `(EventId, UserId)` **unique** index
(the last-write-wins concurrency exception — a conflicting RSVP is a
no-op, the resident's latest status is the truth). Add
`IEventService` → `EventService` (a store-composing service, the
`IAnnouncementService` shape) to `DependencyInjection.cs` (the
"AddTransient with the store injected" shape). **Zero behavioral change** —
the milestone's first *structure* unit.
**Exit:** `dotnet build` green; `dotnet exec …Kumunita.Core.Tests.dll` green
(schema delta applied idempotently, the two indexes present, the existing
`Post` / `Announcement` / `Page` surfaces **untouched**); the
`Event`/`EventRsvp` POCOs + registration are reviewed for the §3.1 provenance
table (every field named to an existing idiom).

### U02 — `EventToAuditableResource` adapter

Add the one adapter from `Event` to the frozen `IAuditableResource` — the
M4 analog of the `PostToAuditableResource` / `PageToAuditableResource`
adapter. A single instance per `Event` is safe to pass into either
`IAuthorizationService` overload. `TargetKind = "event"` (the **exact**
string — matches the aggregate audit row shape in `AccessAudit`; C3).
**No new `AccessAction`** (the existing `Read` is enough); **no new
`AccessVia` value** (events use the settled `Owner` / `Audience` /
`Delegation` branches).
**Exit:** `dotnet build` green; the adapter compiles against the
**frozen** `IAuthorizationService` (no signature change, ADR 0006 §A).

### U03 — `EventService` read lanes + the standing matrix

Implement the read surface on `IEventService`:
`ListUpcomingAsync(componentId, actorId, page)` (the feed — the
`componentId` is a *filter*, never a gate — C-M3·2; the `CanSeeAsync`
over the survivors — C6, C3), `GetAsync(eventId, actorId)` (one
`CanAsync(actorId, Read, adapter)`; **404** on absent, **403** on denied
the `Announcement` 404-vs-403 split), `GetRsvpsAsync(eventId)` (the RSVP
list — the **owner-only** read, the author sees their own event's RSVPs),
`GetMyRsvpAsync(eventId, actorId)` (the actor's own RSVP — the
last-write-wins read). Implement the **standing matrix** (§3.4) on the
write lanes' gate: `CheckCreateStanding` / `CheckEditStanding` (author-only
edit, GlobalAdmin override — the ADR 0014 / 0016 / 0017 precedent) — each
throwing `UnauthorizedAccessException` (403) / `KeyNotFoundException`
(404) exactly like `AnnouncementService`. **DB-backed `EventServiceTests`**:
the `null`-audience-public branch, the `Community` branch (the
`A0036_*` family shape, a `M4_*` family), the grants branch, the
RSVP last-write-wins, the standing matrix.
**Exit:** `dotnet exec …Kumunita.Core.Tests.dll` green with the `M4_*`
authorization family + the RSVP/standing tests; the adapter compiles against
the **frozen** `IAuthorizationService` (no signature change, ADR 0006 §A);
**no Web code yet.**

### U04 — `EventService` write lanes (create / edit / publish / delete + RSVP)

Implement on `IEventService`: `CreateAsync` (the author's choice is written
verbatim — ADR 0001-B; the `IsDraft` flag is the ADR 0037 pin),
`UpdateAsync` (author-only edit, GlobalAdmin override — the ADR 0014 /
0016 / 0017 precedent; `AuthorId`/`Created` preserved untouched;
`Modified` stamped on a real change), `PublishAsync` (Author-only, the
ADR 0037 pin — flips `IsDraft = false`), `DeleteAsync` (**soft-delete**:
`IsDeleted = true`, the ADR 0024 author-lane shape, filter in the read
lanes), and `RsvpAsync` (the **last-write-wins** concurrency exception —
the resident's latest status is the truth; **no** `AccessAudit` row — a
routine resident action, not an access decision). Each stores its
`AccessAudit` row **in the caller's session** (C3) — `TargetKind = "event"`,
`Action` `event.create`/`event.update`/`event.publish`/`event.delete`
(**no** `event.rsvp` action — the RSVP is not an access decision).
Server-side parse of the body for `ImageIds` / `AttachmentIds` (the
RC U04/U05 + ATT U5 idiom — the client never sends them).
**`EventServiceTests`** additions: the create/edit standing matrix, the
publish author-only pin, the soft-delete filter (a deleted event is absent
from `ListUpcomingAsync` / `GetAsync`), the RSVP last-write-wins +
unique-index behavior.
**Exit:** `dotnet exec …Kumunita.Core.Tests.dll` green; **every** write lane
re-checks standing server-side (the C3 single-source pin — a Web
`[Authorize]` is not the source of truth); the audit-row `Action`/`TargetKind`
shape is asserted in tests.

### U05 — `EventController` + the feed + the detail view + the composer

Add `Kumunita.Web/Controllers/EventController.cs`:
- `GET /events` — the **feed** (upcoming events, `componentId` filter
  optional), `CanSeeAsync(Read)`-filtered (the C6 aggregate row, no
  private-event leakage).
- `GET /events/{id}` — the **detail view** (title + rendered body via the
  *one* `MarkdownRenderer` + the ADR 0027 chip-swap over the
  `EventTranslation` rows — **the translation lane is NOT extended to events
  in M4**; the `Event` is authored-in-language only), `CanAsync(Read)`-gated;
  **404** on absent, **403** on denied (the announcement 404-vs-403 split,
  the RC serving-route convention). The RSVP form (Going/Maybe/No —
  the `EventRsvp.Status` enum) is **owner-only** visible (the author sees
  their own event's RSVPs; a non-author sees only their own RSVP).
- `GET /events/new` + `POST /events/new` — compose (title, body,
  component picker, the **`AudienceEditorModel`** verbatim, the ADR 0018
  language picker, the `Start`/`End` datetime pickers, the `Location` text
  field, the `Capacity` int field, the `ReminderEnabled` checkbox) —
  **WYSIWYG** (`bindRichEditor` + RC image + ATT attachment, the
  `PreviewPage.cshtml` wiring pointed at an `Event` body).
- `GET /events/{id}/edit` + `POST /events/{id}/edit` — the edit lane
  (round-trips the audience verbatim, the ADR 0036
  `FromAudience`/`BuildAudience` shape).
- `POST /events/{id}/publish` / `delete` — the standing-gated actions.
- `POST /events/{id}/rsvp` — the RSVP lane (the **last-write-wins**
  concurrency exception; **no** `AccessAudit` row).
- **`EventControllerTests`** (NSubstitute `IEventService`, no live Postgres —
  the `AnnouncementControllerTests` shape): the route map, the 404-vs-403
  split, the feed filter, the composer/edit audience round-trip, the RSVP
  form.
**Exit:** `dotnet exec …Kumunita.Web.Tests.dll` green; a resident can browse
the feed, open an event (detail view), compose + edit an event in the WYSIWYG
editor, set an audience (public/community/grants), RSVP Going/Maybe/No, and
the RSVP is visible to the author; **the `Post` / `Announcement` / `Page`
surfaces are still untouched.**

### U06 — The nav entry + the `/my/drafts` lane + the `kw-dt` timestamps

Add the **nav entry** (one line, the `Community` nav pattern — a single
entry, not three) to the layout; add the **`/my/drafts`** lane (the
ADR 0037 pin — the author's private scratchpad, the same shape as the
post / announcement / page draft lane; the `Event` draft is included in the
`/my/drafts` list); add the **`kw-dt`** TagHelper to every `Event`
timestamp (the `Start` / `End` / `Created` / `Modified` fields — the
ADR 0019 / 0020 per-request resolver; the `kw-dt` is the *one* timestamp
renderer, no new mechanism).
**Exit:** a signed-in resident sees the `Events` nav entry; a draft event
is visible at `/my/drafts` (author-only); every `Event` timestamp renders
in the effective zone + format (the `kw-dt` TagHelper).

### U07 — `EventReminderService` (the Wolverine-free business logic)

Implement the **`EventReminderService`** in `Kumunita.Core.Events`
(the `AuditPurgeService` precedent — the Wolverine-free business logic
that the §6.4 job adapter calls). The service's `SendRemindersAsync`
method: (a) load the events whose `Start` is within the **24-hour
window** (the `now + 1 hour` to `now + 24 hours` range — the
"remind the day before" semantics; the `Start` is the `DateTimeOffset`
UTC instant); (b) for each event, load the `EventRsvp` rows with
`Status = Going` (the RSVPs are the reminder recipients — the author is
always included, even if they did not RSVP); (c) for each recipient,
stage an `OutboxEmail` via the **frozen** `IMailerStage.StageAsync`
(idempotency key `remind:{eventId}:{userId}` — the §6.2 per-email key
scheme; the `OutboxEmailHandler` durable handler dispatches it over SMTP
on a successful commit); (d) **no** `AccessAudit` row (the reminder is a
side effect, not an access decision — the same posture as the
verification email). **`EventReminderServiceTests`** (the
`AuditPurgeServiceTests` shape — the Wolverine-free harness): the
24-hour window filter (an event 25 hours out is not reminded; an event
23 hours out is), the `Going` RSVP filter (a `Maybe` RSVP is not
reminded; a `No` RSVP is not), the author-inclusion rule (the author is
reminded even if they did not RSVP), the idempotency-key shape
(`remind:{eventId}:{userId}`), the **no-audit-row** pin (zero
`AccessAudit` rows after `SendRemindersAsync`).
**Exit:** `dotnet exec …Kumunita.Core.Tests.dll` green with the
`EventReminderServiceTests` family (the 5 pinned tests: window filter,
Going filter, author inclusion, idempotency key, no audit row); the
`IMailerStage` is **frozen** (no signature change, no new method); the
`OutboxEmail` + `OutboxEmailHandler` trio is **untouched** (the handler
already dispatches `OutboxEmail` over SMTP — the §6.2 durable-email
laneship).

### U08 — `EventReminderHandler` + `EventReminderTick` (the §6.4 job)

Add `Kumunita.Web/SideEffects/EventReminderHandler.cs`
(the `AuditPurgeHandler` precedent verbatim — the self-rescheduling
`TimeoutMessage` idiom, the sanctioned §6.4 recurring-job shape). The
handler's `Handle` method: (a) inject the live `IDocumentStore` + the
`IOptions<EventReminderOptions>` (the 24-hour window config — the
`AuditPurgeOptions` precedent); (b) call the **Wolverine-free**
`EventReminderService.SendRemindersAsync(store, options, now)` (the
business logic is in `Kumunita.Core` — the `AuditPurgeService`
precedent); (c) **re-schedule the next tick** (return a fresh
`EventReminderTick` — the self-rescheduling `TimeoutMessage` idiom; the
1-day delay is baked into the `EventReminderTick` type, the
`AuditPurgeTick` precedent). The `EventReminderTick` record is a
**`TimeoutMessage(TimeSpan.FromDays(1))`** (the `AuditPurgeTick`
precedent verbatim — the `Wolverine.TimeoutMessage` base constructor
bakes the delay into the message type, so every re-publish of the same
message type carries the same schedule; **no** per-callsite
`DelayedFor` needed). **Wiring:** the `Program.cs` adds the
`EventReminderTick` to the Wolverine host (the `AuditPurgeTick`
wiring precedent — the `UseWolverine` + `IntegrateWithMarten` +
`ApplyAllConfiguredChangesToDatabaseAsync` trio; the `EventReminderTick`
is a **new** message type, the `AuditPurgeTick` is the precedent).
**Exit:** `dotnet build` green; the `EventReminderHandler` compiles
against the **frozen** `Wolverine.TimeoutMessage` base (no signature
change); the `EventReminderTick` is a **`TimeoutMessage(TimeSpan.FromDays(1))`**
(the `AuditPurgeTick` precedent verbatim); the `Program.cs` wiring is
green (the `EventReminderTick` is registered next to the `AuditPurgeTick`);
the `EventReminderService` (U07) is **frozen** (no signature change).

### U09 — The 23 pinned seam tests (the `EventServiceTests` + `EventReminderServiceTests` + `EventToAuditableResourceTests` families)

Implement the **23 pinned seam tests** from the design doc §3.7 (the M4
analog of the M3 18-test list + the M4 `EventReminderServiceTests`
family) in `tests/Kumunita.Core.Tests/EventServiceTests.cs` +
`tests/Kumunita.Core.Tests/EventReminderServiceTests.cs` (the M3
`PostServiceTests.cs` + the `AuditPurgeServiceTests.cs` shape to mirror —
the test harness, the fixture, the assertion style). **This unit does
NOT run the acceptance gate** (that is U11) and **does NOT author the e2e**
(that is U12) — each is a distinct unit for atomic handoff.
**Exit:** `dotnet build` green. `dotnet exec …Kumunita.Core.Tests.dll`
reports **23 tests discovered, 23 executed** — the pass/red status of each
is recorded (for U11's gate). **No gate recorded** (U11), **no e2e
authored** (U12), **no design-doc edits** (U11/U12). Handoff note: 3
lines starting `## U09 — seam tests (23)` — (a) the test file paths,
(b) the 23 names (verbatim), (c) the 23 pass/red counts (for U11 to
consume).

### U10 — `EventControllerTests` (the Web-side seam tests)

Implement the **`EventControllerTests`** in
`tests/Kumunita.Web.Tests/EventControllerTests.cs` (the
`AnnouncementControllerTests` shape to mirror — the NSubstitute
`IEventService` harness, the route map, the 404-vs-403 split, the feed
filter, the composer/edit audience round-trip, the RSVP form). **This
unit does NOT run the acceptance gate** (that is U11) and **does NOT
author the e2e** (that is U12) — each is a distinct unit for atomic
handoff.
**Exit:** `dotnet build` green. `dotnet exec …Kumunita.Web.Tests.dll`
reports the `EventControllerTests` family green — the pass/red status of
each is recorded (for U11's gate). **No gate recorded** (U11), **no e2e
authored** (U12), **no design-doc edits** (U11/U12).

### U11 — run + record the M4 acceptance gate

Execute and **record** the three-test acceptance gate (closed-loop /
handoff / part-vs-whole) from the design doc §3.8, *using* U09's 23
tests as the part-vs-whole evidence. The **e2e** is also executed in
this unit (the M2 three-test shape: (a) closed loop — an author creates
an event → it appears in the feed; the RSVP is visible to the author;
(b) handoff — an author creates an event → a group member added after
the event sees it on the next request — strong consistency; the delegate
branch is the "handoff to a delegate" case; (c) part-vs-whole — the
23-test list passes together) and recorded alongside the gate. **If
the e2e runtime (Postgres-boot + token-channel) is not yet present, the
spec is authored (mirroring M2's U13) and *not run* — the gap is
recorded in the design doc and the next unit who lands the runtime
records the pass count.** This is the M2 U11 analog (the "run the
record" step), split from U09 so the test-authoring unit stays atomic.
**FACES:** record one face this milestone **strengthens** (**Adaptive** — a
resident's RSVP / reminder reaches a real outcome and the loop closes with an
owner) and one it **consumes** (**Stable** — the daily `EventReminderTick` is
a new moving part in prod, and the reminder window's coverage edge is a
boundary we must keep provably closing).
**Exit:** the gate section is present and consistent with U09's
results. Handoff note: 4–5 lines starting `## U11 — gate recorded` —
the three test names + pass counts + the date + any still-open drift.

### U12 — close: ARCHITECTURE.md flip + README + `Milestones`

Flip the `Events/` status line in `ARCHITECTURE.md` from
"M4 — not yet created" to **M4 ✓ live** with the gate summary, and write
the **M4→M5 deferral note** so the next milestone's OOS close is honest.
The M2 analog is U12's "close the loop" step. Update the `Milestones.cs`
`M4` row to `StatusDone`; update the README Roadmap `M4` row to
`**Done.**`; update `MilestonesTests.cs` (the `M4` row is now
`StatusDone`, **`M5`** becomes the single `StatusNext` — the
single-in-progress pin moves to the next milestone; `PG` is already
`StatusDone` from U00). **The `Post` / `Announcement` / `Page` surfaces
are untouched** (the M4 lane is additive on top of the existing lanes).
**Exit:** `dotnet build` + **both** test assemblies green;
`ARCHITECTURE.md`'s `Events/` line is flipped; the `Milestones.cs` `M4`
row is `StatusDone`; the README Roadmap `M4` row is `**Done.**`; the
`MilestonesTests` family is green (the `M4` row is `StatusDone`, the
`M5` row is the single `StatusNext`, `PG` is `StatusDone`); the `Post` /
`Announcement` / `Page` surfaces are **untouched** (the M4 lane is
additive on top of the existing lanes).

## Register

| Unit | Deliverable | Exit (green) | Status |
|---|---|---|---|
| U00 | ADR 0054 + `M4` milestone + README + `MilestonesTests` | docs/adr green, no other code | ☐ |
| U01 | `Event`/`EventRsvp` docs + `M4DocTypes` + DI | Core tests green, indexes present | ☐ |
| U02 | `EventToAuditableResource` adapter | adapter compiles, frozen `IAuthorizationService` | ☐ |
| U03 | read lanes + standing matrix | `M4_*` auth family green | ☐ |
| U04 | write lanes (create/edit/publish/delete + RSVP) + C3 audit | write-lane tests green | ☐ |
| U05 | `EventController` + feed + detail + composer (WYSIWYG) | Web tests green, E2E browse/compose | ☐ |
| U06 | nav entry + `/my/drafts` + `kw-dt` timestamps | nav + drafts + timestamps green | ☐ |
| U07 | `EventReminderService` (Wolverine-free) | `EventReminderServiceTests` green | ☐ |
| U08 | `EventReminderHandler` + `EventReminderTick` (§6.4 job) | §6.4 job green, frozen `Wolverine` | ☐ |
| U09 | 23 pinned seam tests | 23 tests discovered + executed | ☐ |
| U10 | `EventControllerTests` | Web tests green | ☐ |
| U11 | M4 acceptance gate recorded | gate section present + consistent | ☐ |
| U12 | ARCHITECTURE.md flip + README + `Milestones` | all green, `Events/` line flipped | ☐ |

## Drift-pause policy (the repo's convention)

A unit **pauses** (does not force) on: (a) a standing-matrix cell the
`EventService` cannot express with the *existing* role claims (a new
standing would be a **new ADR**, not a silent addition); (b) a
RSVP-concurrency rule the `EventRsvp` cannot express with the
*last-write-wins* exception (a new concurrency rule would be a **new
ADR**, not a silent addition); (c) a reminder-window rule the
`EventReminderService` cannot express with the *24-hour* window (a new
window would be a **new ADR**, not a silent addition); (d) a
translation-standing that is not the ADR 0022/0029 matrix (a **new
ADR**); (e) a §6.4 job shape that requires a *new* `Wolverine` idiom not
already in the `AuditPurgeHandler` precedent (a **new unit**, not a
silently-omitted idiom); (f) the destructive step (if any) running before
U01–U11 are green (**forbidden** — the sequencing invariant). A pause is
recorded in `m4-handoff-notes.md`, not silently worked around.

## Handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U12
below), one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/m4-events-design.md`) — the
  exact C# signatures of every seam U01–U12 must match; the 23 pinned seam
  test names; the three-test acceptance gate; the drift-guard.
- **Secondary — this file** (`docs/plans-milestones/done/m4/plan-m4.md`)
  — the unit registry with each unit's deliverables and exit criteria.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/done/m4/m4-handoff-notes.md`). One section
  per unit, appended (never rewritten). Each unit writes exactly one short
  section before it exits; the next unit reads only that section + its own
  entry-read list.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list,
3–5 files <~300 lines each, no full-repo scan; the design-doc section cited
is named); **Deliverables** (a closed set of new/modified files, ≤ ~4 files /
~600 LOC, no misc cleanups); **Exit** (`dotnet build` green for the touched
projects; handoff-note entry appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §3.9
drift-guard; (3) never introduces a test whose exact name is not in the
§3.7 seam list; (4) never opens a *new* seam on `IAuthorizationService` /
`IUserInfoService` / `IIdentityService` beyond what U00 pinned; (5) never
re-shapes a document (`Event`, `EventRsvp`) outside the §3.1 pin; (6) if
entry reads reveal the design doc is out of date, the unit pauses and
records `## U<m> — Drift pause` in the handoff note.
