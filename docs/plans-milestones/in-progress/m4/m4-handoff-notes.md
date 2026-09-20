# M4 — Events, RSVPs & reminders — rolling handoff log

> One `## U#` section per unit, appended (never rewritten). Each unit writes
> exactly one short section before it exits; the next unit reads only that
> section + its own entry-read list.

## Lane open

M4 plan created 2026-09-20. The M4 milestone is the **coordination** arrow
(events + RSVP + reminders). 13 units (U00–U12), each sized for a ~32K-context
fresh agent. The design doc is `docs/design/m4-events-design.md` (U00 authors
it). ADR 0054 is the decision record (U00 locks it). The §6.4 scheduled job
(`EventReminders`) is U07/U08. The acceptance gate is U11. The close is U12.

**Open decisions for U00 (resolve in the design doc §3.1 + §3.2):**

1. **Event field naming drift vs `docs/ARCHITECTURE.md` §5.** The plan and
   the ADR use `Body` (rich content, mirroring `Announcement.Body`) and
   `ReminderEnabled` (bool, opt-out). `docs/ARCHITECTURE.md` §5's Events
   sketch uses `description` and `rsvpRequired`. The design doc is the
   primary tier and locks the canonical name; **U12 (close) must sync the
   ARCHITECTURE.md §5 `Events` block to whatever the design doc settles**
   (per the AGENTS.md doc↔code parity rule). If the design doc picks the
   ARCHITECTURE.md names, U01–U10 plans all shift (`Body` → `description`,
   `ReminderEnabled` → `rsvpRequired`); if it keeps the plan names, U12
   rewrites the ARCHITECTURE.md §5 block.
2. **Milestones pin handoff.** PG **already shipped** (its lane
   `U00–U07` is green and `LocalizedPage` is retired — see
   `docs/plans-milestones/pages/pages-handoff-notes.md`). The roadmap trio
   is stale: `Milestones.cs` still shows PG as `StatusNext` and M4 as
   `StatusPlanned`. U00's roadmap step **closes PG (`StatusDone`) and
   opens M4 (`StatusNext`)** in the same commit, and renames the
   single-in-progress pin from `PG_...` to `M4_...`. This is **not** a
   pull-forward over in-flight work — it is a clean close of the finished
   lane and the open of the next one.

**Both open decisions are resolved by the Lane-open section above** (U00
resolves #1 in the design doc §3.1 + ADR 0054 Context — keeping the plan's
`Body` / `ReminderEnabled` names; and resolves #2 in this commit). No
further open decisions carried into U01.

## U00 — sign-off + ADR 0054

- **ADR:** **0054** — `docs/adr/0054-events-rsvp-reminders.md`, Status
  **Accepted**, dated **2026-09-20**. The ADR's **Context** records the
  roadmap order (PG SHIPPED / M4 opened, not a pull-forward) and the
  **`Body` / `ReminderEnabled` naming decision** (over
  ARCHITECTURE.md §5's `description` / `rsvpRequired` sketch — the close
  unit U12 syncs §5).
- **Design doc:** `docs/design/m4-events-design.md` (primary reference tier)
  — all §3.x sections marked **[DECIDED — ADR 0054]**; **zero `[PROPOSED]`
  markers remain** (verified by grep). §4 lists the exact C# seams
  (`IEventService`, `EventReminderService`, `EventReminderHandler`,
  `EventReminderTick`, `EventReminderOptions`, the `M4DocTypes` surface).
- **The 23 pinned seam test names** (design doc §3.7 = the master list, ADR
  0054 Decision): T01 `M4_MemberSeesUpcomingEventFeed` · T02
  `M4_NullAudienceEventIsPublic` · T03 `M4_CommunityAudienceSeesFeed` · T04
  `M4_GrantsAudienceOnlyGranteeSees` · T05 `M4_DraftInvisibleToNonAuthor`
  · T06 `M4_PlainMemberCreateAllowed` · T07 `M4_AuthorCanEditOwnEvent` ·
  T08 `M4_PlainMemberEditDenied` · T09 `M4_GlobalAdminOverrideEdit` · T10
  `M4_PublishAuthorOnly` · T11 `M4_SoftDeleteExcludesFromFeedAndDetail` ·
  T12 `M4_AuthorSoftDeleteOwnEvent` · T13 `M4_RsvpLastWriteWins` · T14
  `M4_RsvpUniqueIndexOneRowPerUser` · T15 `M4_RsvpListOwnerOnly` · T16
  `M4_RsvpWritesNoAccessAuditRow` · T17 `M4_AuditRowShape_Create` · T18
  `M4_EventToAuditableResourceShape` · T19 `M4_ReminderWindowFiltersOutsideEvents`
  · T20 `M4_ReminderGoingRsvpsOnly` · T21 `M4_ReminderAuthorAlwaysIncluded`
  · T22 `M4_ReminderIdempotencyKeyShape` · T23 `M4_ReminderWritesNoAccessAuditRow`.
- **The three-test acceptance gate** (design doc §3.8, ADR 0054): **1 —
  closed loop** (author creates a published event → feed shows it, the
  `TargetKind = "event"` aggregate row; author RSVPs Going → visible in the
  owner-only list); **2 — handoff** (a user added to `Audience.Grants` after
  creation sees the event on the next request; the `Delegation` branch is
  handoff-onto-a-delegate); **3 — part-vs-whole** (the 23 names are the
  whole; tests 1–2 are the parts; all pass together in the same
  `Kumunita.Core.Tests` run as the inherited M1–M3 / PG anchors).
- **Roadmap trio (this commit):** `src/Kumunita.Web/Milestones.cs` — **M4
  row = `StatusNext`**, **PG now `StatusDone`** (M5 / M6 stay
  `StatusPlanned` — the M-letter order is untouched); `README.md` — PG
  `**Next.**` → `**Done.**`, M4 `**Planned.**` → `**In progress.**`; the
  Events feature bullet now says "M4 — in progress"; the "next is" line now
  says **PG is done … next is M4** (ADR 0054). `MilestonesTests.cs` —
  `Shipped` set gains `"PG"`; the pin is renamed
  `PG_Is_The_Single_InProgress_Milestone` →
  `M4_Is_The_Single_InProgress_Milestone` (asserts `M4`); the ordered `Ids`
  list is **unchanged** (`… PG, M4, M5, M6`).
- **ADR index:** `docs/adr/README.md` — row **0054** added after the
  existing **0053** row (the index already carried 0051 / 0052 / 0053; this
  commit adds only 0054). Row **0054** present, `Accepted`; the full index
  is 0040→0054 with each row exactly once (verified by count).
- **Exit gate (all green):** `dotnet build Kumunita.slnx -c Debug` →
  **Build succeeded** (zero warnings); `dotnet exec Kumunita.Web.Tests.dll`
  → **Total: 332, Errors: 0, Failed: 0** (the renamed `M4_Is_The_Single_InProgress_Milestone` pin passes). The 23 seam tests themselves are
  implemented by **U09**, not U00 — the gate *names* them; U11 records the
  first green run of the full set.
- **Drift pauses:** **none.** Both open decisions resolved cleanly (naming →
  design doc §3.1 + ADR 0054 Context; pin → this commit). No new ADR was
  needed, no standing cell was inexpressible with existing role claims,
  and no existing seam (`IAuthorizationService` / `IUserInfoService` /
  `IIdentityService` / `IMailerStage`) was re-shaped.
- **Untouched (per scope):** `Post` / `Announcement` / `Page` surfaces,
  the translation lane (events are authored-in-language only in M4), and the
  M5 / M6 roadmap letters.

**U00 exit gate met. STOP — do NOT start U01.**

## U01 — Event/EventRsvp docs + M4DocTypes

- **(a) Files added:**
  - `src/Kumunita.Core/Events/Event.cs` — the §3.1 POCO (exact field set,
    byte-for-byte the design-doc shape; `Body` / `ReminderEnabled` naming).
  - `src/Kumunita.Core/Events/EventRsvp.cs` — the §3.2 POCO + the
    `RsvpStatus` enum (`Going` / `Maybe` / `No`).
  - `src/Kumunita.Core/M4DocTypes.cs` — the new parallel doc surface
    (the `M3DocTypes` pattern verbatim; root-level file, like
    `PageDocTypes.cs` / `TagDocTypes.cs`).
  - `src/Kumunita.Core/Events/IEventService.cs` — the §4 seam (exact
    signatures; U03/U04 implement the bodies).
  - `src/Kumunita.Core/Events/EventService.cs` — the store-composing
    skeleton (ctor composes the **frozen** `IDocumentStore` +
    `IAuthorizationService` + `IUserInfoService`; every method throws
    `NotImplementedException` — the "logic lands in U03/U04" pin).
  - `src/Kumunita.Core/Events/EventRequests.cs` — the `CreateEventRequest`
    / `UpdateEventRequest` records referenced by the §4 seam (the request
    payloads the write lanes take; the client never sends `ImageIds` /
    `AttachmentIds`).
  - **Modified:** `src/Kumunita.Core/DependencyInjection.cs` —
    `IEventService → EventService` transient registered next to
    `IAnnouncementService` / `IPageService` (the "AddTransient with the
    store injected" shape).
  - **Modified:** `src/Kumunita.Web/Program.cs` — `M4DocTypes.Configure(opts)`
    wired into the dev-loop path next to the `TagDocTypes.Configure(opts)`
    line (one call added). `SchemaBootstrap.cs` was **not** touched: it
    applies the *storage-feature* steps and never calls the doc surfaces
    directly — the doc surfaces are registered at the `StoreOptions` level
    (Program.cs), the same as M1/M3/Media/Page/Tag.
- **(b) The two indexes confirmed** (in `M4DocTypes.Configure`):
  - `Event` — a regular composite **feed-ordering** index
    `(ComponentId, Start)` (the design-doc §4 "feed ordering shape"; a
    filter-then-sort optimization, not an integrity constraint).
  - `EventRsvp` — a **unique** `(EventId, UserId)` index (the §3.2
    last-write-wins concurrency pin — exactly one RSVP row per resident
    per event, upsert semantics).
- **(c) Build + test result (U01 exit gate, both green):**
  - `dotnet build Kumunita.slnx -c Debug` → **Build succeeded, 0
    Warning(s), 0 Error(s)**.
  - `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
    → **Total: 615, Errors: 0, Failed: 0, Skipped: 0** (the new schema
    delta applies idempotently against a real Postgres via Testcontainers;
    the existing `Post` / `Announcement` / `Page` surfaces are untouched).
  - **One note (not a drift):** the multi-expression
    `.Index(e => e.ComponentId, e => e.Start)` form used by `UniqueIndex`
    does **not** exist on Marten 9's `DocumentMapping` (CS1061 — only
    `UniqueIndex` is `params`-based; the regular composite `Index` takes a
    single projection). The `(ComponentId, Start)` feed index is therefore
    declared via the anonymous-object projection
    `.Index(e => new { e.ComponentId, e.Start })` — the documented
    composite-index idiom, same column order, non-unique. The
    `(EventId, UserId)` **unique** index uses the `params` form
    `.UniqueIndex(r => r.EventId, r => r.UserId)` (the `M3DocTypes` /
    `PageDocTypes` precedent).
- **(d) Drift pauses:** **none.** No new `AccessAction` / `AccessVia` /
  authorization branch; no new audience mechanism (`Event.Audience` is the
  exact `Kumunita.Core.Authorization.Audience` type, nullable — `null` =
  public, ADR 0036); no new editor / renderer / email / timezone / tag /
  language / media mechanism — every field reuses an existing idiom per the
  §3.1 provenance table. No frozen seam
  (`IAuthorizationService` / `IUserInfoService` / `IIdentityService` /
  `IMailerStage`) re-shaped — the `EventService` ctor only *consumes* the
  existing seams. `Post` / `Announcement` / `Page` untouched. No code
  beyond the ADR / docs was changed (U00's scope).
- **Untouched (per scope):** `Milestones.cs` / README / `MilestonesTests`
  (U00 already flipped PG→done, M4→next), the translation lane (events are
  authored-in-language only in M4), the `EventToAuditableResource` adapter
  (U02), the 23 seam tests (U09), and the §6.4 reminder job (U07/U08).

**U01 exit gate met. STOP — do NOT start U02.**

## U02 — EventToAuditableResource adapter

- **(a) File added:** `src/Kumunita.Core/Events/EventToAuditableResource.cs` —
  the **only** new file; `public sealed class EventToAuditableResource :
  IAuditableResource`, the design doc §3.3 shape verbatim. Mirrors
  `Posts.PostToAuditableResource` / `Pages.PageToAuditableResource` — the
  constructor takes the `Event`, exposes the `Event` property (not owned), and
  projects the 6 members. **No** new `AccessAction` / `AccessVia` /
  authorization branch; compiles against the **frozen**
  `IAuthorizationService` (ADR 0006 §A) with **no** signature change.
- **(b) The 6-member projection confirmed** (byte-for-byte the §3.3 pin):
  `Id` = `Event.Id`; `Name` = `Event.Title ?? (Event.Body.Length < 60 ?
  Event.Body : Event.Body[..57] + "...")` (the 60-char truncation — 57 + "...");
  `OwnerId` = `Event.AuthorId`; `Audience` = `Event.Audience` (nullable —
  `null` = public, the frozen `Decide()` branch 5); `ComponentId` =
  `Event.ComponentId`; **`TargetKind` = `"event"`** (the **exact** string,
  not "Event" / "events" — the `AccessAudit.TargetKind` discriminator, C3). A
  single instance per `Event` is safe to pass into either overload
  (`CanAsync` detail, `CanSeeAsync` feed).
- **(c) Build + test result (U02 exit gate, both green, zero warnings):**
  - `dotnet build Kumunita.slnx -c Debug` → **Build succeeded in 13.7s, 0
    Warning(s), 0 Error(s)**.
  - `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
    → **Total: 615, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (the adapter
    compiles clean against the frozen `IAuthorizationService`; the existing
    `Post` / `Announcement` / `Page` surfaces and their tests are untouched).
- **(d) Drift pauses:** **none.** `TargetKind` is the exact `"event"` string;
  `Audience` is nullable (the `Post`/`Page` precedent — `Post` is non-null by
  construction, but `Event.Audience` is `Authorization.Audience?`, so the
  nullable projection is the correct §3.3 shape); no frozen seam
  re-shaped; `Event` / `EventRsvp` / `M4DocTypes` / `EventService` (U01),
  `Milestones.cs` / README / `MilestonesTests`, and the read/write lanes
  (U03/U04) all untouched.

**U02 exit gate met. STOP — do NOT start U03.**

## U03 — read lanes + standing matrix

- **(a) Files touched:**
  - `src/Kumunita.Core/Events/EventService.cs` — the **only** existing file
    edited. Implemented the four **read lanes** on the frozen `IEventService`
    (U01) + the two **standing-matrix** gate helpers. Restored the dropped
    `_store` / `_authorization` field declarations alongside `_userInfo` and
    the `PageSize = 30` constant. The five **write lanes** (`CreateAsync` /
    `UpdateAsync` / `PublishAsync` / `DeleteAsync` / `RsvpAsync`) are left as
    `NotImplementedException` — that is **U04** scope and was deliberately not
    touched.
  - `tests/Kumunita.Core.Tests/EventServiceTests.cs` — **new** file. 17 `M4_*`
    tests (read + standing authorization family only; **not** the full 23-test
    U09 list). Follows the `PostServiceTests` harness template:
    `PostgresFixture`, `BootStoreAsync` (registers `M4DocTypes.Configure`), a
    `Services(store)` tuple, a `Plant` helper, an `Audience(GrantKind, id)`
    helper, and `await_ThrowsUnauthorized`.
- **(b) The four read lanes + two standing helpers (signatures confirmed
  against the frozen `IEventService` / `IAuthorizationService`):**
  - `ListUpcomingAsync(string? componentId, string actorId, int page, ct)` —
    candidate set `!IsDeleted && !IsDraft`, optional `ComponentId` filter,
    `OrderBy(Start).Skip((page-1)*PageSize).Take(PageSize)`; feeds are
    authorization-filtered via `CanSeeAsync` (`Decision` per candidate is
    **not** used here — this is the feed path) and drafts are excluded
    unconditionally (ADR 0037).
  - `GetAsync(string eventId, string actorId, ct)` — missing id / deleted →
    `KeyNotFoundException` (404); **`IsDraft`** → pure
    `AuthorId == actorId` ordinal check (non-author is **404**, even a
    GlobalAdmin — the draft bypasses authorization entirely, ADR 0037, and
    writes **no** audit row); else published/audience-restricted → the
    `CanAsync` `Decision` (deny → **403** `UnauthorizedAccessException`).
  - `GetRsvpsAsync(string eventId, ct)` — **no actor param** (frozen seam);
    missing / deleted → 404; returns the RSVP rows ordered by `At`.
  - `GetMyRsvpAsync(string eventId, string actorId, ct)` — the same
    404-vs-403 split + draft gate as `GetAsync`; returns the actor's own RSVP
    row or `null`.
  - `CheckCreateStanding(string actorId, IReadOnlySet<string> actorRoles,
    Event? @event)` — null event → 404; empty `actorId` → 403; else **pass**
    (any signed-in resident may create). `actorRoles` is unused on this
    branch by design (create = any resident; the roles are reserved for the
    edit matrix).
  - `CheckEditStanding(string actorId, IReadOnlySet<string> actorRoles,
    Event? @event)` — null event → 404; empty `actorId` → 403; **GlobalAdmin**
    → pass; **author** (`@event.AuthorId == actorId`) → pass; else 403
    "Only the author or a GlobalAdmin may edit an event."
- **(c) Build + test result (U03 exit gate, both green):**
  - `dotnet build Kumunita.slnx -c Debug` → **Build succeeded, 0 Error(s)**
    (`Kumunita.Core` clean; `Kumunita.Core.Tests` compiles with 27
    house-style warnings — 26× xUnit1051 nullable-`CancellationToken`, present
    across the whole suite, and 1× CS8625 intentional null-actor in
    `M4_CheckCreateStanding_NoActor_Denies`).
  - Scoped: `dotnet exec
    tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll -class
    "Kumunita.Core.Tests.EventServiceTests"` → **Total: 17, Errors: 0,
    Failed: 0, Skipped: 0, Not Run: 0** (all 17 `M4_*` tests pass).
  - Full Core suite: `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
    → **Total: 632, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (no
    regressions in the existing `Post` / `Announcement` / `Page` / Identity /
    UserInfo surfaces).
  - Full Web suite: `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
    → **Total: 332, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (the
    milestone / roadmap trio untouched, as required).
  - **The 17 `M4_*` tests** (all PASS): `M4_FeedVisibleToAudienceMember`,
    `M4_FeedPublicEventVisibleToResident`, `M4_CommunityAudienceVisibleToMember`,
    `M4_GrantAudienceOnlyGranteeSees`, `M4_DraftInvisibleToNonAuthor`,
    `M4_FeedOrderedStartAscending`, `M4_SoftDeletedExcludedFromFeedAndDetail`,
    `M4_GetRsvps_ReturnsRsvps`, `M4_GetMyRsvp_ReturnsOwn`,
    `M4_GetMyRsvp_DraftNonAuthor_404`, `M4_CheckCreateStanding_SignedIn_Resident_Allows`,
    `M4_CheckCreateStanding_NoActor_Denies`, `M4_CheckCreateStanding_NullEvent_404`,
    `M4_CheckEditStanding_Author_Allows`, `M4_CheckEditStanding_GlobalAdmin_Allows`,
    `M4_CheckEditStanding_Stranger_Denies`, `M4_CheckEditStanding_NullEvent_404`.
- **(d) Drift pauses (frozen-seam shapes confirmed, no re-shaping):**
  - The frozen `IAuthorizationService` (ADR 0006 §A) has **no
    `CancellationToken` overloads** — the 4-param overloads take an
    `IDocumentSession`, not a `CancellationToken`. All three call sites
    therefore use the **3-arg standalone form** (`CanAsync` detail /
    `CanSeeAsync` feed), matching the `PostService` precedent (plain reads, no
    in-flight transaction) and passing **no** `ct`.
  - The frozen `IEventService.GetRsvpsAsync(string eventId,
    CancellationToken ct = default)` has **no actor param** — the read-RSVP
    list is a public detail read (the actor's own row is exposed separately by
    `GetMyRsvpAsync`), so no actor was added.
  - `Decision.Allowed` / `VisibleSet.Visible` are the correct accessors;
    `M4DocTypes` / `Event` / `EventRsvp` / `EventToAuditableResource` (U02) /
    `EventRequests` all untouched; **no** `Milestones.cs` / README /
    `MilestonesTests` change (U12's job); write lanes (U04) and the full
    U09 23-test list remain out of scope.

**U03 exit gate met. STOP — do NOT start U04.**
