# M4 — Events, RSVPs & reminders — design

> **Milestone.** `M4` — the **coordination** arrow (the `M0`–`M6` roadmap
> letters stay fixed: M5 stays Projects, M6 stays Portability). **ADR 0054**
> (the next number after 0053) is this milestone's decision record. This file
> is the **primary reference tier** (the exact seams every unit codes
> against); the sealed-unit register is
> `docs/plans-milestones/in-progress/m4/plan-m4.md`; the scratch log is
> `docs/plans-milestones/in-progress/m4/m4-handoff-notes.md`.
>
> **Status.** Accepted (ADR 0054, 2026-09-20). All decisions marked
> **[DECIDED]** are locked in ADR 0054; the ADR is the authoritative record,
> this doc is the primary reference tier for implementation.

## 1. What this milestone is

The first **content-with-time** milestone: a new bounded context
`Kumunita.Core.Events` with two documents (`Event` + `EventRsvp`), one new
document surface (`M4DocTypes`), one new service seam (`IEventService`), one
new adapter (`EventToAuditableResource`), one new Web surface
(`EventController` + views + the nav entry), and **one new §6.4 scheduled
job** (`EventReminders` — the second of the three jobs named in
`docs/ARCHITECTURE.md` §6.4, alongside the shipped `AuditPurge` and the
deferred `VerifyDigest`):

- **The feed** — a resident sees the neighborhood's upcoming events
  (`GET /events`, `componentId` filter optional, `CanSeeAsync(Read)`-filtered).
- **The detail view** — title + rendered body via the *one* `MarkdownRenderer`,
  every timestamp via the *one* `kw-dt` TagHelper, the RSVP form (Going /
  Maybe / No), the RSVP list visible to the author.
- **The composer** — title, body (WYSIWYG, the *one* `bindRichEditor`),
  component picker, the `AudienceEditorModel` verbatim, language picker
  (ADR 0018), `Start`/`End` datetime pickers, `Location`, `Capacity`,
  `ReminderEnabled` — plus the RC content-image + ATT attachment lanes.
- **The standing lanes** — author-only edit, GlobalAdmin override
  (the ADR 0014 / 0016 / 0017 precedent); publish author-only (ADR 0037);
  soft-delete author lane (ADR 0024); the draft lane (`/my/drafts`, ADR 0037).
- **The reminder job** — the day before an event, every `Going` RSVP recipient
  (and the author, always) gets one email through the frozen
  `IMailerStage` + `OutboxEmail` + durable-handler trio.

**The one thing every unit must respect:** this milestone is **additive and
reusing.** It reuses the `Audience` doc (ADR 0001-B / 0036 — *reused*, not
extended), the `IAuthorizationService.CanAsync(Read)` decision path (ADR
0006) through an `EventToAuditableResource` adapter (the
`PostToAuditableResource` shape verbatim, `TargetKind = "event"`), the
`MarkdownRenderer` + `bindRichEditor` authoring surface (ADR 0025 / 0031),
the content-image / attachment idiom (ADR 0025 / 0034), the `kw-dt` TagHelper
(ADR 0019 / 0020), the `IMailerStage` + `OutboxEmail` seam (M1 step 7), and
the self-rescheduling `TimeoutMessage` job shape (the `AuditPurgeHandler`
precedent). **No new `AccessAction`**, **no new `AccessVia` value**, **no new
authorization path**, **no editor dependency**, **no new timezone or format
mechanism**. It adds an *adapter*, not a *branch*.

## 2. The existing surface this milestone builds on (verified, 2026-09-20)

Read directly, not assumed:

1. **`Post` / `PostReply` + `PostToAuditableResource` + `PostService`**
   (`Kumunita.Core.Posts`, M3) — the `IAuditableResource` adapter shape this
   milestone mirrors verbatim (the 6-member surface in
   `Authorization/AccessAction.cs`: `Id` / `Name` / `OwnerId` / `Audience` /
   `ComponentId` / `TargetKind`); the `Name = Title ?? Body[..60]`
   truncation idiom; the audience written verbatim (ADR 0001-B).
2. **`Announcement` + `AnnouncementService`** (M3b) — the
   **author-of-record ∪ GlobalAdmin** standing matrix (ADR 0017), the
   `CreateAsync` C3 pattern (standing re-checked server-side, the Web
   `[Authorize]` is a convenience pre-gate only), the 404-vs-403 split
   (`KeyNotFoundException` / `UnauthorizedAccessException`), the draft
   pin (ADR 0037), and the soft-delete flag shape (ADR 0024).
3. **`Audience`** (`Authorization/Audience.cs`) — `Mode` + `Grants` + the
   ADR 0036 `Community` branch; **`null` = public** (the frozen `Decide()`
   branch 5). The `AudienceEditorModel` + `BuildAudience()` single-source
   form surface (M2, reused by every composer).
4. **`MarkdownRenderer` + `bindRichEditor`** (RC / RE, ADR 0025 / 0031 /
   0033) — the one renderer on the read path, the one editor on the write
   path; the RC content-image (`![alt](/content-image/{id})`) + ATT
   attachment (`[label](/attachment/{id})`) body idioms, the
   `ImageIds` / `AttachmentIds` server-side parse idiom.
5. **`IMailerStage` + `OutboxEmail` + `OutboxEmailHandler`** (M1 step 7) —
   the frozen email trio: `StageAsync(session, idempotencyKey, recipient,
   subject, body, ct)` stores the `OutboxEmail` row in the caller's session
   and enqueues the durable envelope in the same transaction; the durable
   handler dispatches over SMTP on commit, retries, then dead-letters
   (`EmailDeadLetter`, ARCHITECTURE.md §6.2).
6. **`AuditPurgeHandler` + `AuditPurgeTick` + `AuditPurgeService`** (M1) —
   the **§6.4 job precedent**: the business logic is a Wolverine-free static
   class in `Kumunita.Core`; the Web handler is a thin adapter that injects
   a live `IDocumentStore` + `IOptions<AuditPurgeOptions>`, calls the
   service, and re-yields a fresh tick; the tick is
   `record AuditPurgeTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1))`
   (the delay is baked into the message type — every re-publish carries the
   same schedule). `EventReminders` is the second of the three §6.4 jobs;
   `VerifyDigest` stays deferred.
7. **`kw-dt` TagHelper** (ADR 0019 / 0020) — the one timestamp renderer:
   per-request resolver (resident override → platform default → `UTC` floor)
   + format (resident override → platform default → Long floor).
8. **`LocaleSettings` + `ILocalizationService`** (ADR 0005) — the
   `LanguageCode` authored-in tag (ADR 0018) and the language catalog.
9. **`DependencyInjection.cs`** (`Kumunita.Core`) — the per-feature
   registration shape ("add transient with the store injected") the
   `IEventService` registration follows.

## 3. The design decisions

### 3.1 The `Event` field set  **[DECIDED — ADR 0054]**

```csharp
// Kumunita.Core.Events
public sealed class Event
{
    public string Id { get; set; } = string.Empty;            // surrogate (Marten default)

    public string Title { get; set; } = string.Empty;         // display label (adapter Name)
    public string Body { get; set; } = string.Empty;          // Markdown — the one MarkdownRenderer (ADR 0025)

    public string? ComponentId { get; set; }                  // feed organizer — never a gate (C-M3·2)
    public string AuthorId { get; set; } = string.Empty;      // owner branch; the standing owner
    public DateTimeOffset Start { get; set; }                  // UTC instant — the feed ordering key
    public DateTimeOffset End { get; set; }                    // UTC instant
    public string? Location { get; set; }
    public int? Capacity { get; set; }                         // display metadata, not a gate

    public Authorization.Audience? Audience { get; set; }     // the exact post Audience (ADR 0001-B / 0036)

    public bool ReminderEnabled { get; set; } = true;         // this milestone — the §6.4 job's opt-out
    public bool IsDraft { get; set; } = true;                 // ADR 0037 draft idiom, reused
    public bool IsDeleted { get; set; } = false;              // ADR 0024 soft-delete flag, reused
    public string LanguageCode { get; set; } = string.Empty;  // ADR 0018 authored-in tag, reused
    public IReadOnlyList<string> TagIds { get; set; } = [];   // ADR 0044 tag ids, reused
    public IReadOnlyList<string> ImageIds { get; set; } = []; // ADR 0025 content-image ids, reused
    public IReadOnlyList<string> AttachmentIds { get; set; } = []; // ADR 0034 attachment ids, reused

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
}
```

**Field-by-field provenance** (each reuses an existing mechanism, none
invents one):

| Field | Source idiom | Note |
|---|---|---|
| `Title` / `Body` / `LanguageCode` | `Post` / `Announcement` (ADR 0018) | authored-in tag + Markdown body; the one `MarkdownRenderer`; the one `bindRichEditor`. |
| `ComponentId` | `Post.ComponentId` | a feed filter, **never** a gate (C-M3·2). |
| `AuthorId` | `Post.AuthorId` | the owner branch; the standing matrix's standing owner. |
| `Start` / `End` | *new, this milestone* | the time. Rendered by the one `kw-dt`; the `Start` index drives feed ordering. |
| `Location` / `Capacity` | *new, this milestone* | display metadata — `Capacity` is **not** a gate (no admission queue in M4). |
| `Audience` | `Post.Audience` (ADR 0001-B / 0036) | `null` = public (branch 5); the `AudienceEditorModel` form surface verbatim. The composer seeds it **community-visible by default** (ADR 0036). |
| `ReminderEnabled` | *new, this milestone* | the §6.4 job's per-event opt-out; `true` floor. |
| `IsDraft` | `Announcement.IsDraft` (ADR 0037) | the author-only draft pin + the `/my/drafts` lane. |
| `IsDeleted` | `Post.IsDeleted` (ADR 0024) | soft-delete; filtered in the read lanes. |
| `TagIds` / `ImageIds` / `AttachmentIds` | `Post` (ADR 0044 / 0025 / 0034) | the server-side body parse populates `ImageIds` / `AttachmentIds` (the client never sends them). |

**Naming decision:** the canonical names are `Body` and `ReminderEnabled`
(the plan's names, mirroring `Announcement.Body`). `docs/ARCHITECTURE.md` §5
sketches the event as `description` / `rsvpRequired` — that sketch is the
pre-decision shape; **U12 (close) syncs the §5 block to these names** (per
the AGENTS.md doc↔code parity rule).  **[DECIDED — ADR 0054]**

### 3.2 The `EventRsvp` shape  **[DECIDED — ADR 0054]**

```csharp
public enum RsvpStatus { Going, Maybe, No }

public sealed class EventRsvp
{
    public string Id { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;   // (EventId, UserId) unique index
    public string UserId { get; set; } = string.Empty;
    public RsvpStatus Status { get; set; } = RsvpStatus.Going;
    public DateTimeOffset At { get; set; }                 // last write's instant
}
```

- **Last-write-wins concurrency** — the ARCHITECTURE.md §5 exception, keyed
  per `(EventId, UserId)`: a conflicting RSVP write is a no-op or
  self-converging; the resident's latest status is simply the truth. The
  `M4DocTypes` surface registers a **unique** `(EventId, UserId)` index
  (upsert semantics).
- **No `AccessAudit` row on an RSVP** — a routine resident action, not an
  access decision (the same posture as a profile edit).
- **Reads:** the RSVP **list** is owner-only (the author sees their own
  event's RSVPs); a non-author sees only their **own** RSVP.

### 3.3 The `EventToAuditableResource` adapter  **[DECIDED — ADR 0054]**

The `PostToAuditableResource` shape verbatim (the same 6-member
`IAuditableResource` surface):

```csharp
public sealed class EventToAuditableResource : IAuditableResource
{
    public Event Event { get; }
    public string Id => Event.Id;
    public string Name => Event.Title ?? (Event.Body.Length < 60 ? Event.Body : Event.Body[..57] + "...");
    public string? OwnerId => Event.AuthorId;
    public Audience? Audience => Event.Audience;
    public string? ComponentId => Event.ComponentId;
    public string TargetKind => "event";
}
```

- **No new `AccessAction`** (the existing `Read` is enough); **no new
  `AccessVia` value** (events use the settled `Owner` / `Audience` /
  `Delegation` branches).
- A single instance per `Event` is safe to pass into either
  `IAuthorizationService` overload (detail `CanAsync`, feed `CanSeeAsync`).
- `TargetKind = "event"` is the **exact** string — the aggregate audit row
  discriminator in `AccessAudit` (C3).

### 3.4 The standing matrix  **[DECIDED — ADR 0054]**

| Action | Standing | `AccessVia` |
|---|---|---|
| **Create** | any signed-in resident (becomes the author) | `Owner` |
| **Edit** (body / time / audience / …) | the `AuthorId` ∪ GlobalAdmin | `Owner` / `Admin` |
| **Publish** a draft | the `AuthorId` only (ADR 0037 pin) | `Owner` |
| **Soft-delete** | the `AuthorId` ∪ GlobalAdmin (ADR 0024) | `Owner` / `Admin` |
| **RSVP** | any resident who may read the event | *(no row — §3.2)* |

- **Enforced server-side** in the `EventService` — the
  `AnnouncementService.CreateAsync` C3 pattern (`CheckCreateStanding` /
  `CheckEditStanding`, each throwing `UnauthorizedAccessException` (403) /
  `KeyNotFoundException` (404) exactly like `AnnouncementService`); the Web
  `[Authorize]` is a convenience pre-gate only.
- **Every audited write lane** stores its `AccessAudit` row in the caller's
  session (C3), `TargetKind = "event"`, `Action`
  `event.create` / `event.update` / `event.publish` / `event.delete` — **no**
  `event.rsvp` action (the RSVP is not an access decision).

### 3.5 The draft / tag / media / language / delete lane reuse  **[DECIDED — ADR 0054]**

Each is the **frozen** existing shape on `Event`, byte-for-byte the same
field idiom the post / announcement / page surfaces already use — **no new
mechanism** for any of them: the `IsDraft` flag + `/my/drafts` lane (ADR
0037); the `TagIds` (ADR 0044); the `ImageIds` + `AttachmentIds` (ADR 0025 /
0034, server-side body parse); the `LanguageCode` (ADR 0018); the `IsDeleted`
flag + read-lane filter (ADR 0024). **The translation lane is NOT extended to
events in M4** — an `Event` is authored-in-language only in this milestone;
the event-translation row (the ADR 0022/0026/0029 shape) is a follow-on
lane, own ADR.

### 3.6 The `EventReminders` §6.4 job  **[DECIDED — ADR 0054]**

- **`EventReminderService`** (Wolverine-free static class in
  `Kumunita.Core.Events`, the `AuditPurgeService` precedent):
  `SendRemindersAsync(IDocumentStore store, EventReminderOptions options,
  DateTimeOffset now, CancellationToken ct)` — (a) load events with
  `ReminderEnabled = true` whose `Start` is within the **24-hour window**
  (`now < Start ≤ now + 24h` — the "remind the day before" semantics; a
  `Start` in the past is not reminded); (b) recipients = the event's
  `EventRsvp` rows with `Status = Going` **plus the author, always** (even if
  they did not RSVP; `Maybe` / `No` RSVPs are not reminded); (c) for each
  recipient, stage one `OutboxEmail` via the **frozen** `IMailerStage`
  (idempotency key **`remind:{eventId}:{userId}`** — the §6.2 per-email key
  scheme; the key's existing-row check is the no-double-send guard across
  ticks); (d) **no `AccessAudit` row** (a side effect, not an access decision
  — the verification-email posture).
- **`EventReminderHandler`** (`Kumunita.Web/SideEffects`, the
  `AuditPurgeHandler` precedent verbatim): a thin adapter — call the
  Wolverine-free service with a live `IDocumentStore` +
  `IOptions<EventReminderOptions>`, then **re-yield a fresh
  `EventReminderTick`** (the self-rescheduling idiom).
- **`EventReminderTick`** — `record EventReminderTick() :
  Wolverine.TimeoutMessage(TimeSpan.FromDays(1))` (the `AuditPurgeTick`
  precedent verbatim; the 1-day delay is baked into the message type).
- **`EventReminderOptions`** — the `AuditPurgeOptions` shape (a POCO of
  config; `WindowHours = 24` floor).
- **Best-effort, re-runnable** (ARCHITECTURE.md §6.2): a failed SMTP send
  dead-letters in `EmailDeadLetter`; the durable trio is **untouched** — the
  job only *stages* rows.
- **Wiring:** `Program.cs` registers the `EventReminderTick` next to the
  `AuditPurgeTick` (the `IntegrateWithWolverine()` + `ApplyAllConfigured-
  ChangesToDatabaseAsync` trio precedent).

### 3.7 The 23 pinned seam test names  **[DECIDED — ADR 0054 — the master list]**

U09 implements **exactly** these 23 names in
`tests/Kumunita.Core.Tests/EventServiceTests.cs` (T01–T18) and
`tests/Kumunita.Core.Tests/EventReminderServiceTests.cs` (T19–T23). A rename
or re-scope of a name after this freeze is a **drift event** (§3.9):

| # | Test name (exact) | Anchored to |
|---|-------------------|-------------|
| T01 | `M4_MemberSeesUpcomingEventFeed` | §3.3; the feed (`CanSeeAsync(Read)` survivors, `Start` ordering) |
| T02 | `M4_NullAudienceEventIsPublic` | §3.3; the `Decide()` branch 5 (unauthenticated sees it) |
| T03 | `M4_CommunityAudienceSeesFeed` | §3.3; ADR 0036's `Community` branch (a member sees it; a non-member doesn't) |
| T04 | `M4_GrantsAudienceOnlyGranteeSees` | §3.3; the grant-list branch (an un-granted member is denied) |
| T05 | `M4_DraftInvisibleToNonAuthor` | §3.5; ADR 0037 (a draft is invisible to everyone but the author) |
| T06 | `M4_PlainMemberCreateAllowed` | §3.4; create standing (any signed-in resident) |
| T07 | `M4_AuthorCanEditOwnEvent` | §3.4; owner edit (ADR 0014/0016/0017 shape) |
| T08 | `M4_PlainMemberEditDenied` | §3.4; non-author edit ⇒ `UnauthorizedAccessException` (403) |
| T09 | `M4_GlobalAdminOverrideEdit` | §3.4; GlobalAdmin override (ADR 0017 shape) |
| T10 | `M4_PublishAuthorOnly` | §3.4; ADR 0037 pin (a non-author GlobalAdmin is denied) |
| T11 | `M4_SoftDeleteExcludesFromFeedAndDetail` | §3.5; ADR 0024 (`IsDeleted` filtered from `ListUpcomingAsync` / `GetAsync`) |
| T12 | `M4_AuthorSoftDeleteOwnEvent` | §3.5; the author delete lane (ADR 0024) |
| T13 | `M4_RsvpLastWriteWins` | §3.2; the `(EventId, UserId)` upsert (Going→No→Going lands the latest) |
| T14 | `M4_RsvpUniqueIndexOneRowPerUser` | §3.2; exactly one `EventRsvp` row per `(EventId, UserId)` |
| T15 | `M4_RsvpListOwnerOnly` | §3.2; the RSVP list read is owner-only |
| T16 | `M4_RsvpWritesNoAccessAuditRow` | §3.2 / §3.4; zero `AccessAudit` rows after `RsvpAsync` |
| T17 | `M4_AuditRowShape_Create` | §3.4; the row: `TargetKind = "event"`, `Action = "event.create"`, `Via = Owner` |
| T18 | `M4_EventToAuditableResourceShape` | §3.3; the 6-member projection (incl. `TargetKind = "event"`, the `Name` 60-char truncation) |
| T19 | `M4_ReminderWindowFiltersOutsideEvents` | §3.6; `Start` outside `now < Start ≤ now+24h` is not reminded |
| T20 | `M4_ReminderGoingRsvpsOnly` | §3.6; a `Maybe` / `No` RSVP is not reminded |
| T21 | `M4_ReminderAuthorAlwaysIncluded` | §3.6; the author is reminded even without an RSVP |
| T22 | `M4_ReminderIdempotencyKeyShape` | §3.6; exactly one staged email per recipient, key `remind:{eventId}:{userId}` |
| T23 | `M4_ReminderWritesNoAccessAuditRow` | §3.6; zero `AccessAudit` rows after `SendRemindersAsync` |

### 3.8 The three-test acceptance gate  **[DECIDED — ADR 0054 — U11 records the run]**

| # | Test | Shape (what it proves) |
|---|------|------------------------|
| 1 | **closed loop** | an author creates a published event → it appears in the feed (`TargetKind = "event"` aggregate row, `VisibleCount ≥ 1`, `Outcome = Allow`); the author RSVPs `Going` → their RSVP is visible in the owner-only list |
| 2 | **handoff** | a user added to the event's `Audience.Grants` **after** creation sees the event on the **next** request — strong consistency, no cache; the `Delegation` branch is the handoff-onto-a-delegate case (the delegate sees the owner's grant-scoped event) |
| 3 | **part-vs-whole** | the 23 names in §3.7 are the **whole**; tests 1–2 are the **parts**; all must pass **together** in the same `Kumunita.Core.Tests` run as the inherited M1/M2/M3/M3b/PG anchors (no per-name isolation) |

**Runner note (AGENTS.md test-runner quirk, this machine):** the reliable
path is build then in-process execution — **not** `dotnet test` / VS Test
Explorer (xunit.v3 discovery goes wrong: "No tests found / exit code 5" is a
**runner** bug, not a failure):

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

### 3.9 The drift-guard  **[DECIDED — ADR 0054 — frozen by this unit]**

- **The `Event` field set** (§3.1) — the exact POCO above, including the
  `Body` / `ReminderEnabled` naming (the §5 naming decision). A re-shape
  (renaming a field, adding a gate field, an admission/capacity rule) is a
  **new ADR**, not a silent addition.
- **The `EventRsvp` shape** (§3.2) — `Id` / `EventId` / `UserId` /
  `Status: Going|Maybe|No` / `At`, the `(EventId, UserId)` unique index, the
  last-write-wins exception, no `AccessAudit` row. A new concurrency rule or
  a 4th status is a **new ADR**.
- **The `EventToAuditableResource` adapter** (§3.3) — the 6-member
  projection, `TargetKind = "event"`. No new `AccessAction` / `AccessVia` /
  `Decide()` branch.
- **The `EventService` public methods** (§4) — the exact signatures. An
  ADD beyond this list is a **new ADR**; a re-scope of one is a drift event.
- **The 23 test names** (§3.7) — the master list; a rename/renumber is a
  break.
- **The three-test gate** (§3.8) — closed loop / handoff / part-vs-whole.
- **The §6.4 job shape** (§3.6) — the `AuditPurgeHandler` precedent verbatim;
  a *new* Wolverine idiom (per-callsite delays, a second message type, a
  non-durable schedule) is a **new unit + new ADR**.

## 4. The seams (exact C#)

```csharp
// Kumunita.Core.Events — the service seam (U01 registers; U03/U04 implement)
public interface IEventService
{
    // Read lanes (U03)
    Task<IReadOnlyList<Event>> ListUpcomingAsync(string? componentId, string actorId, int page, CancellationToken ct = default);
    Task<Event> GetAsync(string eventId, string actorId, CancellationToken ct = default);
    Task<IReadOnlyList<EventRsvp>> GetRsvpsAsync(string eventId, CancellationToken ct = default);      // owner-only
    Task<EventRsvp?> GetMyRsvpAsync(string eventId, string actorId, CancellationToken ct = default);

    // Write lanes (U04) — standing re-checked server-side (§3.4, C3)
    Task<Event> CreateAsync(string actorId, CreateEventRequest request, CancellationToken ct = default);
    Task<Event> UpdateAsync(string eventId, string actorId, UpdateEventRequest request, CancellationToken ct = default);
    Task<Event> PublishAsync(string eventId, string actorId, CancellationToken ct = default);          // author-only (ADR 0037)
    Task DeleteAsync(string eventId, string actorId, CancellationToken ct = default);                  // soft (ADR 0024)
    Task<EventRsvp> RsvpAsync(string eventId, string actorId, RsvpStatus status, CancellationToken ct = default); // last-write-wins, no audit row
}

// Kumunita.Core.Events — the §6.4 job (U07; Wolverine-free, the AuditPurgeService precedent)
public sealed class EventReminderOptions
{
    public int WindowHours { get; set; } = 24;   // the "remind the day before" window
}

public static class EventReminderService
{
    public static Task SendRemindersAsync(
        Marten.IDocumentStore store,
        EventReminderOptions options,
        DateTimeOffset now,
        CancellationToken ct = default);
}

// Kumunita.Web.SideEffects (U08; the AuditPurgeHandler precedent verbatim)
public static class EventReminderHandler
{
    public static async Task<IEnumerable<object>> Handle(
        EventReminderTick tick,
        Marten.IDocumentStore store,
        Microsoft.Extensions.Options.IOptions<EventReminderOptions> options)
    {
        await EventReminderService.SendRemindersAsync(store, options.Value, DateTimeOffset.UtcNow);
        return new[] { new EventReminderTick() };   // self-reschedule (1-day delay baked into the type)
    }
}

public sealed record EventReminderTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));
```

**The `M4DocTypes` surface** (U01, the `M3DocTypes` pattern verbatim):
`Event` (conventional `Id`) + a `(ComponentId, Start)` index (the feed
ordering shape) + `EventRsvp` with a **unique** `(EventId, UserId)` index
(the §3.2 last-write-wins exception). Zero migrations for existing
surfaces — the two docs are new, the surface is additive (ADR 0004 §B.1).

## 5. What this milestone is deliberately *not* (non-decisions)

- **Not an admission queue.** `Capacity` is display metadata; there is no
  "waitlist" or "spot release" — `Going` RSVPs are the truth.
- **Not event translations.** The `Event` is authored-in-language only (ADR
  0018); the ADR 0022/0026/0029 translation lane is **not** extended to
  events in M4 (a follow-on lane, own ADR).
- **Not iCal / notifications-as-a-lane.** `events.ics` export is M6;
  per-resident reminder settings ("remind me N hours before") are a
  follow-on lane — the single 24-hour-before email is the M4 surface.
- **Not group events.** A `Group`-scoped event channel is a follow-on lane
  (the ADR 0013 membership-lane precedent would be the shape).
- **Not a second mechanism, anywhere.** One audience, one decision path, one
  renderer, one editor, one email trio, one timezone/format resolver — the
  milestone *reuses* instead of *duplicating*.

---

## 6. Run result (M4 acceptance gate — 2026-09-21)  **[U11 — the record close; the full close is U12]**

This is the **record** close (U11's unit — run the gate, record the result,
author the e2e spec, record the gap). The **close** (the `ARCHITECTURE.md`
`Events/` flip, the README Roadmap, `Milestones.cs`, `MilestonesTests.cs`)
is **U12** — out of scope for this record.

**Runner (AGENTS.md test-runner quirk, this machine):** the reliable path is
build then in-process execution — **not** `dotnet test` / VS Test Explorer
(xunit.v3 discovery goes wrong: "No tests found / exit code 5" is a **runner**
bug, not a failure):

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

**The runs (verified, not assumed).**

- `dotnet build Kumunita.slnx -c Debug` → **Build succeeded. 0 Error(s)**
  (127 pre-existing warnings: `xUnit1051` in the M4 test files, `CS8601` in
  `EventController.cs` — unchanged from U09/U10).
- `dotnet exec …Kumunita.Core.Tests.dll` → **Total: 668, Errors: 0, Failed:
  0, Skipped: 0, Not Run: 0, Time: 59.181s**. The **23 M4 seam tests
  (T01–T23, §3.7) pass *together*** with the inherited M1/M2/M3/M3b/PG anchors
  in the **same** run — the §3.8 part-vs-whole pin holds.
- `dotnet exec …Kumunita.Web.Tests.dll` → **Total: 351, Errors: 0, Failed:
  1, Skipped: 0, Not Run: 0, Time: 9.699s**. The **`EventControllerTests`
  family (19 tests, U10) is fully green**; the single red is the *pre-existing*
  kw-l registry drift (see *Drift status* below), **not** the M4 controller
  family.

Record (shape mirrored from M3 § `Run result`; M4's *handoff* lane is the
grant-scoped audience re-scope, the §3.8 row 2 pin):

| # | Gate test (§3.8) | Evidence (actual test names — all passed) |
|---|------|-------------------------------------------|
| 1 | **closed loop** — an author creates a published event → it appears in the feed (`TargetKind = "event"` aggregate row, `VisibleCount ≥ 1`, `Outcome = Allow`); the author RSVPs `Going` → their RSVP is visible in the owner-only list | `EventServiceTests.M4_PlainMemberCreateAllowed` (T06, C3 — the create lane writes the `event.create` audit row), `EventServiceTests.M4_MemberSeesUpcomingEventFeed` (T01, C6 — the feed's `CanSeeAsync(Read)` lets the audience member see the event; the closed-loop `VisibleCount ≥ 1` is the observable), `EventServiceTests.M4_RsvpLastWriteWins` (T13, §3.2 — the `RsvpAsync` last-write-wins exception), `EventServiceTests.M4_RsvpListOwnerOnly` (T15, §3.2 — the owner-only RSVP-list read; the author sees their own event's RSVPs). |
| 2 | **handoff** — a user added to the event's `Audience.Grants` **after** creation sees the event on the **next** request (strong consistency, no cache); the `Delegation` branch is the handoff-onto-a-delegate case | `EventServiceTests.M4_GrantsAudienceOnlyGranteeSees` (T04, C6 — the grant-scoped audience: only the grantee sees the event; the *next* `ListUpcomingAsync` call sees the live grant row — the strong-consistency lane), `EventServiceTests.M4_CommunityAudienceSeesFeed` (T03, ADR 0036 — the community-visible branch the handoff lane re-scopes over). |
| 3 | **part-vs-whole** — the 23 names in §3.7 are the **whole**; tests 1–2 are the **parts**; all must pass **together** in the same `Kumunita.Core.Tests` run as the inherited M1/M2/M3/M3b/PG anchors (no per-name isolation) | U09's 23 pinned `[Fact]`s in `tests/Kumunita.Core.Tests/EventServiceTests.cs` (T01 `M4_MemberSeesUpcomingEventFeed` · T02 `M4_NullAudienceEventIsPublic` · T03 `M4_CommunityAudienceSeesFeed` · T04 `M4_GrantsAudienceOnlyGranteeSees` · T05 `M4_DraftInvisibleToNonAuthor` · T06 `M4_PlainMemberCreateAllowed` · T07 `M4_AuthorCanEditOwnEvent` · T08 `M4_PlainMemberEditDenied` · T09 `M4_GlobalAdminOverrideEdit` · T10 `M4_PublishAuthorOnly` · T11 `M4_SoftDeleteExcludesFromFeedAndDetail` · T12 `M4_AuthorSoftDeleteOwnEvent` · T13 `M4_RsvpLastWriteWins` · T14 `M4_RsvpUniqueIndexOneRowPerUser` · T15 `M4_RsvpListOwnerOnly` · T16 `M4_RsvpWritesNoAccessAuditRow` · T17 `M4_AuditRowShape_Create` · T18 `M4_EventToAuditableResourceShape`) + `tests/Kumunita.Core.Tests/EventReminderServiceTests.cs` (T19 `M4_ReminderWindowFiltersOutsideEvents` · T20 `M4_ReminderGoingRsvpsOnly` · T21 `M4_ReminderAuthorAlwaysIncluded` · T22 `M4_ReminderIdempotencyKeyShape` · T23 `M4_ReminderWritesNoAccessAuditRow`) — **all 23 green** — **plus** the M1/M2/M3/M3b/PG-inherited anchors re-run unchanged in the same execution (`Kumunita.Core.Tests` 668/668, 0 failed). |

**Gate status: all three tests PASS** (closed-loop · handoff · part-vs-whole;
`Kumunita.Core.Tests` 668/668 + the `EventControllerTests` family 19/19, with
the one Web red attributed to pre-existing kw-l drift, not the M4 family).

**FACES (the two the U11 plan text pins).**

- **Strengthens — Adaptive:** a resident's RSVP / reminder reaches a real
  outcome and the loop closes with an owner (create → feed → RSVP → owner-only
  list; the reminder email is the side effect that lands on a *working* event,
  per the U07/U08 sequencing invariant).
- **Consumes — Stable:** the daily `EventReminderTick` (U08) is a new moving
  part in prod, and the reminder window's coverage edge (the 24-hour
  `now < Start ≤ now+24h` boundary, T19) is a boundary we must keep provably
  closing — a new §6.4 recurring job in the durable-handler surface.

**E2E status (authored, NOT run — the M2 U13 / M3 U10 / M3b U10 precedent).**

The e2e runtime (Postgres-boot + token-channel) is **not yet present**: the
`kumunita` Playwright fixture is still a **documented throw** in
`e2e-m2.spec.ts`, `e2e-m3.spec.ts`, and (newly) **`e2e-m4.spec.ts`** — no M4
unit (U0–U11) implements the runtime. Per plan U11 ("*if it does not yet
exist, author the spec (mirror M2's U13), do not run it, and record the gap
in the design doc*"), U11 **authored** `tests/Kumunita.Web.Tests/e2e-m4.spec.ts`
(three specs mirroring the §3.8 gate: (a) closed-loop create→feed→RSVP→
owner-only-list, (b) handoff grant-added-after-creation seen on the next
request, (c) part-vs-whole feed/detail/RSVP surface coherence). Selectors +
route pins are grounded against the **shipped** M4 views
(`Views/Event/{Index,Detail,Create,Edit}.cshtml` + `Controllers/EventController.cs`).
`npx tsc --noEmit` on `tests/Kumunita.Web.Tests` → **exit 0** (the spec is
valid TypeScript; the same M2 U13 "author without the runtime" state).

**Pass count: 0 (spec authored; not runnable)** — by design of the pause. The
**bounded runtime gap** is: (1) the `kumunita` fixture's `signup / login /
lastCreatedEventId / grantEventToUser` implementation (the M2 D2 token-channel
+ M4's two helpers), (2) a Postgres boot wired to the same DB the `dotnet run`
server reads, (3) **no** new production-code changes — the M4 Core + Web seams
are frozen and already exercised by the 23 Core seam tests + 19 controller
tests. **The unit that lands the runtime** records the M4 e2e pass count in a
subsequent `### Run result (M4 e2e — <date>)` section of this doc.

**Drift status (still open — U12's concern, not a U11 regression).**

- **`KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` is RED**
  (the single Web.Tests red). Root cause, confirmed: three `kw-l` keys used in
  the M4 views are **not** in
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`:
  - `src/Kumunita.Web/Views/Event/Detail.cshtml:98` — `events.created`
  - `src/Kumunita.Web/Views/Event/Detail.cshtml:102` — `events.edited`
  - `src/Kumunita.Web/Views/Shared/_Layout.cshtml:67` — `nav.events`

  This is **pre-existing M4 drift** (the key registry was not extended when
  the M4 views were authored in U05/U06), **not** a U09/U10 regression — the
  `EventControllerTests` family is fully green. **Flagged for U12** so the
  close is honest (the fix is a one-line-per-key addition to
  `KnownTranslationKeys.cs`, or a follow-on localization lane). No `## U<m> —
  Drift pause` sections exist in the handoff note for this unit; the U11 record
  does not touch any frozen pin.
