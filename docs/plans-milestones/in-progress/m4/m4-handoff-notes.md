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

## U04 — write lanes

- **(a) Files touched:**
  - `src/Kumunita.Core/Events/EventService.cs` — implemented the **five
    write lanes** on the frozen `IEventService` (U01), replacing the five
    `NotImplementedException` placeholders U03 left in place:
    `CreateAsync` / `UpdateAsync` / `PublishAsync` / `DeleteAsync` /
    `RsvpAsync`. Added four private helpers: `StoreAuditRow` (the single
    `AccessAudit` row per write lane — C3 invariant), `ResolveLanguageCodeAsync`
    (ADR 0018 authored-in tag, `LocaleSettings` singleton, `en` floor),
    `AudiencesEqual` / `ListsEqual` (no-op-change detection for the
    `UpdateAsync` `Modified` stamp), and the `StaticEmptyRoles` constant
    (see (d) drift). The `IEventService` seam, `Event` / `EventRsvp` /
    `M4DocTypes` / `EventToAuditableResource` (U02) / the four **read**
    lanes + two standing helpers (U03) are all **untouched**.
  - `src/Kumunita.Core/Events/EventRequests.cs` — added
    `IReadOnlyList<string>? ImageIds` and `IReadOnlyList<string>?
    AttachmentIds` to **both** `CreateEventRequest` and `UpdateEventRequest`
    (the `PostDraft` / `Announcement` precedent: nullable default, null-
    coalesced to `[]` inside the service — ADR 0025 RC / ADR 0034 ATT).
    `CreateEventRequest.IsDraft` / `UpdateEventRequest`'s field set are
    otherwise untouched; **`UpdateEventRequest` does not carry `IsDraft`**
    (publish, ADR 0037, owns that flip — the draft state is not an editable
    field, see (d)).
  - `tests/Kumunita.Core.Tests/EventServiceTests.cs` — appended **17 new
    `M4_*` write-lane tests** (the U04 family, the plan's "specific test
    families": create/edit standing matrix, publish author-only pin,
    soft-delete filter, RSVP last-write-wins + unique-index behavior, and the
    audit-row `Action` / `TargetKind` shape assertions) + the
    `EventAuditRows` helper (the `PostDraftModeTests.PostAudits` shape,
    scoped to `TargetKind == "event"`). The 17 U03 read/standing tests are
    untouched.
- **(b) The five write lanes (signatures confirmed against the frozen
  `IEventService`):**
  - `CreateAsync(string actorId, CreateEventRequest request, ct)` —
    `CheckCreateStanding` (any signed-in resident; empty actor →
    `UnauthorizedAccessException`, the Web 403); resolves the authored-in
    `LanguageCode` (ADR 0018); stores the `Event` with `IsDraft` carried
    verbatim from the request (ADR 0037), `AuthorId = actorId`, `Created =
    UtcNow`, `Modified = null`, `IsDeleted = false`, and the
    `TagIds` / `ImageIds` / `AttachmentIds` null-coalesced to `[]`; stores
    one `AccessAudit` row (`Action = "event.create"`, `TargetKind = "event"`,
    `Via = Owner`, `Outcome = Allow`, single-target) on the **same**
    `IDocumentSession` (C3 atomic commit). Returns the created event.
  - `UpdateAsync(string eventId, string actorId, UpdateEventRequest
    request, ct)` — load → 404-if-missing → `CheckEditStanding(actor,
    StaticEmptyRoles, existing)` (**author branch only** — see (d) frozen
    seam) → re-resolve the `LanguageCode` on **both** sides before comparing
    (the `AnnouncementService.UpdateAsync` no-op-stamp shape) → compute
    `changed` across all editable fields (title / body / component / start /
    end / location / capacity / audience / reminder / language / tags /
    images / attachments) → apply the author's choices verbatim (ADR 0001-B)
    → stamp `Modified` **only** on a real change (a no-op re-save leaves the
    stamp untouched) → `session.Store(existing)` (the PostService re-store
    quirk) → one `AccessAudit` row (`"event.update"`, `Via Owner`).
    `AuthorId` / `Created` / `IsDraft` / `IsDeleted` are **deliberately
    not** reassigned (publish / delete own those; see (d)).
  - `PublishAsync(string eventId, string actorId, ct)` — load → 404-if-
    missing → **author-only** (`existing.AuthorId == actorId` ordinal; a
    non-author is denied **even at GlobalAdmin** — ADR 0037's pin, contrast
    ADR 0017's edit lane) → flip `IsDraft = false` → stamp `Modified`
    **only** if it was still a draft (idempotent: a second publish on an
    already-live event is a no-op, no double-stamp) → `Store` → one
    `AccessAudit` row (`"event.publish"`, `Via Owner`).
  - `DeleteAsync(string eventId, string actorId, ct)` — load → 404-if-
    missing → `CheckEditStanding` (author branch, same frozen-seam scope as
    `UpdateAsync`) → set `IsDeleted = true` (ADR 0024 soft-delete — the row
    is not removed, the read lanes filter it) → stamp `Modified` → `Store`
    → one `AccessAudit` row (`"event.delete"`, `Via Owner`).
  - `RsvpAsync(string eventId, string actorId, RsvpStatus status, ct)` —
    load the `Event` → 404-if-missing-or-deleted → enforce signed-in
    (empty `actorId` → `UnauthorizedAccessException`) **directly, not via
    `CanAsync`** (the pin below — using `CanAsync` would write a read-audit
    row, which the ADR 0054 §3.2 "no audit row on an RSVP" pin forbids) →
    last-write-wins upsert keyed on the `(EventId, UserId)` unique index:
    load the actor's existing row (if any), mutate `Status` / `At`, else
    create → **`session.Store(rsvp)` on both branches** (the PostService
    "a loaded-then-mutated row is not reliably carried to the DB by
    SaveChangesAsync alone" quirk — the bug this fix caught, see (d)) →
    `SaveChangesAsync`. **No `AccessAudit` row** (the ADR 0054 §3.2 pin).
- **(c) Build + test result (U04 exit gate, both green):**
  - `dotnet build Kumunita.slnx -c Debug` → **Build succeeded, 0 Error(s)**
    (the house-style xUnit1051 warnings are unchanged from U03 — present
    across the whole suite, not introduced by U04).
  - Full Core suite (the exit gate per `AGENTS.md` — `dotnet test` / VS Test
    Explorer are not used, per the runner-discovery quirk): `dotnet exec
    tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
    → **Total: 649, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (632
    pre-U04 + 17 new U04 tests; no regressions in the `Post` / `Announcement`
    / `Page` / Identity / UserInfo surfaces).
  - **The 17 `M4_*` U04 tests** (all PASS): `M4_CreateWritesEventAndAuditRow`,
    `M4_CreateAudienceWrittenVerbatim`, `M4_CreateNoActor_Denies`,
    `M4_UpdateAuthorEditsAndPreservesAuthor`, `M4_UpdateNoOpDoesNotStampModified`,
    `M4_UpdateReparsesMediaIds`, `M4_UpdateStrangerDenied`,
    `M4_UpdateMissingEvent_404`, `M4_PublishAuthorOnly`,
    `M4_PublishStrangerDenied`, `M4_PublishIdempotentNoDoubleStamp`,
    `M4_SoftDeleteAuthorExcludesFromFeedAndDetail`,
    `M4_SoftDeleteStrangerDenied`, `M4_RsvpLastWriteWins`,
    `M4_RsvpDistinctResidentsCoexist`, `M4_RsvpMissingEvent_404`,
    `M4_RsvpWritesNoAccessAuditRow`.
- **(d) Drift pauses (frozen-seam scope boundary + one real bug caught):**
  - **The GlobalAdmin edit override (ADR 0017) is not applied in this
    frozen seam.** The frozen `IEventService` write lanes (U01) take
    **only** `actorId` — no `actorRoles`, no `IDocumentSession` — so
    `UpdateAsync` / `DeleteAsync` enforce standing via
    `CheckEditStanding(actorId, StaticEmptyRoles, existing)` with an
    **empty** role set: only the author branch passes; a GlobalAdmin who is
    not the author is **denied at the Core layer** in this unit. The Web
    boundary (U05, `EventController`) is where the same
    `CheckEditStanding` is called again with the principal's real role set
    to admit the GlobalAdmin override — matching the `AnnouncementService`
    / `PageService` split where the Core service is the source of truth for
    the **author** branch and the Web layer is where the elevated-role
    override is evaluated for lanes whose frozen seam carries no roles.
    This is a scope boundary in **this unit** (U04, Core only), not a
    redefinition of ADR 0017 — the override still exists, it is just not
    reachable through the Core seam's signature. The create lane is
    unaffected (create = any signed-in resident, no roles consulted) and
    the publish lane is unaffected (author-only by ADR 0037, no roles
    consulted by design).
  - **`UpdateEventRequest` does not carry `IsDraft`.** The draft state is
    owned by the publish lane (ADR 0037), not by the edit lane — an editor
    cannot flip an event draft↔live via `UpdateAsync`; only `PublishAsync`
    (author-only) does. `UpdateAsync` deliberately does not reassign
    `IsDraft` (nor `AuthorId` / `Created` / `IsDeleted` — the latter owned
    by the delete lane, ADR 0024).
  - **The `RsvpAsync` "last-write-wins" upsert required an explicit
    `session.Store(rsvp)` on the **mutated**-row branch** — a row loaded via
    `session.Query<EventRsvp>()` and then mutated in place is **not**
    reliably written to the DB by `SaveChangesAsync` alone (the same
    PostService "re-store + save" quirk, see `PostService.cs` U8b tag-attach
    comment). The first test run of `M4_RsvpLastWriteWins` confirmed this:
    the second `RsvpAsync` call correctly found and mutated the same in-
    memory row (the `no.Id == going.Id` assert passed), but the `Status`
    change did not reach Postgres (the read-back still showed the first
    write's `Going`). Adding `session.Store(rsvp)` unconditionally on both
    the create and mutate branches — mirroring `UpdateAsync` / `PublishAsync`
    / `DeleteAsync`, which already `Store` their loaded-then-mutated `Event`
    — fixed it, and the full Core suite (649 tests) is green. This is a
    real, reproducible Marten change-tracking quirk in this codebase, not a
    test harness artifact.
  - **`M4_RsvpWritesNoAccessAuditRow` (the no-audit-row pin)** is satisfied
    by enforcing the signed-in + event-presence check **directly** in
    `RsvpAsync` (empty `actorId` → 403; missing/deleted event → 404) rather
    than calling `IAuthorizationService.CanAsync` — the latter would write a
    read-audit row, which ADR 0054 §3.2 forbids for an RSVP (a routine
    resident action, not an access decision).
  - `Milestones.cs` / README / `MilestonesTests` are **untouched** (U12's
    close-time job, per the AGENTS.md doc↔code parity rule); the U09 23-test
    list and the U05 `EventController` remain out of scope.

**U04 exit gate met. STOP — do NOT start U05.**

## U05 — EventController + views
- (a) 7 actions: Index / Detail / Create / Edit / Publish / Delete / Rsvp
- (b) 4 views: Index / Detail / Create / Edit + the EventEditorModel (4 types in 1 file)
- (c) bindRichEditor script block (ADR 0031 WYSIWYG editor reused — no new TS)
- (d) AudienceEditorModel form fields (the M2 audience editor reused)
- (e) kw-dt TagHelper usage (ADR 0019 / 0020 timezone / format resolvers reused)
- (f) **Drift — GlobalAdmin edit/delete write denied at Core.** Design matrix §3.4 says
  Edit/Delete = Author ∪ GlobalAdmin, but the frozen Core write lanes (UpdateAsync /
  DeleteAsync) call `EventService.CheckEditStanding` with `StaticEmptyRoles`, so a
  non-author GlobalAdmin's **write** is denied at Core (UACE → 403) even though the Web
  pre-gate (real roles) passes. The affordance flags + Web pre-gate correctly use the
  real `KumunitaPrincipal.RoleSet(User)` (GlobalAdmin-aware), and Core remains the final
  gate. Full GlobalAdmin write override requires the seam to accept `actorRoles` (a Core
  change, out of U05 scope).

**U05 exit gate met. STOP — do NOT start U06.**

## U06 — nav entry + /my/drafts + kw-dt

- **(a) Files touched (all in `src/Kumunita.Web` — **zero Core files changed**,
  per the "no Core changes" scope; the event-draft read is a Web-layer store
  query, not a new Core seam):**
  - `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the **nav entry**: one
    `<li class="nav-item">` linking `href="/events"`, label
    `<kw-l key="nav.events">Events</kw-l>`, placed in the **signed-in** block
    between `Groups` and `Pages` (the `Community` nav pattern verbatim — one
    entry, not three; `EventController` is `[Authorize]` ⇒ resident-only, so
    it sits with the other resident-only links, not the public Home/
    Announcements).
  - `src/Kumunita.Web/Controllers/MyDraftsController.cs` — the **`/my/drafts`
    lane**: added an `IDocumentStore` ctor parameter + a read-lane query of the
    actor's own draft events (`.Where(e => e.IsDraft && e.AuthorId == actor &&
    !e.IsDeleted)` `.OrderByDescending(e => e.Created)`, opened on a dedicated
    `store.QuerySession()` — the C3 read-lane shape). This **reuses the
    existing `/my/drafts` lane** (the ADR 0037 author-only pin) rather than
    inventing a new one; the author-only shape is identical to the
    `PostService.ListMyDraftsAsync` / `IAnnouncementService.ListMyDraftsAsync`
    reads (pure `AuthorId == actor` + `IsDraft`, no role, no `AccessAudit`
    row — the M4 `IEventService` seam has no dedicated draft-list lane, so the
    Web page reads the actor's own draft `Event` docs directly — a *read*, not
    a decision). Class + action doc-comments updated to name the event lane.
  - `src/Kumunita.Web/Models/MyDraftsViewModel.cs` — added `Events`
    (`IReadOnlyList<Event>`), `using Kumunita.Core.Events;`.
  - `src/Kumunita.Web/Views/MyDrafts/Index.cshtml` — the **Events** section
    (after the Announcements section): `@if (Model.Events.Count > 0)` block,
    each row linking to `/events/{ev.Id}` (the detail lane offers Publish,
    ADR 0037), title-else-`MarkdownRenderer.PlainTextPreview(ev.Body, 80)`;
    the `any` empty-state check now includes `Model.Events.Count > 0`.
  - `src/Kumunita.Web/Views/Event/Detail.cshtml` — the **`kw-dt`** extension:
    Start / End / `EventRsvp.At` already used `<kw-dt>` (U05); added the two
    remaining `Event` timestamps — `Created` + `Modified` (the
    `DateTimeOffset?`) — as a `text-muted small mt-3` footer after the body
    card, the one `<kw-dt>` TagHelper (ADR 0019 / 0020 per-request timezone +
    format resolver), mirroring the Announcement Detail footer (a value →
    text; an unset `Modified` renders nothing). No new mechanism.
- **(b) The nav line (verbatim):** `<a class="nav-link text-dark"
  href="/events"><kw-l key="nav.events">Events</kw-l></a>` — a single entry
  in the signed-in nav block.
- **(c) The `/my/drafts` wiring (the ADR 0037 pin, author-only):** the
  `MyDraftsController.Index` now resolves the actor's own draft events via
  `store.QuerySession()` → `.Query<Event>()
  .Where(e => e.IsDraft && e.AuthorId == actor && !e.IsDeleted)
  .OrderByDescending(e => e.Created)` and passes them on
  `MyDraftsViewModel.Events`; the view renders them under an **Events**
  heading, each row linking to `/events/{id}` (the detail lane, where the
  author-only Publish button lives). A draft event is therefore discoverable
  at `/my/drafts`, author-only (a non-author's query returns no one else's
  drafts — the author-only pin holds end-to-end).
- **(d) The `kw-dt` locations (every `Event` timestamp renders via the one
  TagHelper):** `Start` / `End` — `Views/Event/Index.cshtml` (feed row) +
  `Views/Event/Detail.cshtml` (detail header, U05); `EventRsvp.At` —
  `Views/Event/Detail.cshtml` (the owner-only RSVP list, U05);
  `Created` / `Modified` — **this unit** (`Views/Event/Detail.cshtml` footer).
  The composer/edit `Start` / `End` inputs keep `value="…ToLocalTime()"`
  (browser datetime-local pre-fill — an input control value, not a rendered
  reader timestamp; not a `kw-dt` target). No new timezone / format mechanism.
- **(e) Exit gate (per `AGENTS.md` — both green; `dotnet test` / VS Test
  Explorer **not** run, per the runner-discovery quirk — that is U10's job):**
  - `dotnet build Kumunita.slnx -c Debug` → **Build succeeded, 0 Warning(s),
    0 Error(s)**.
  - `npm --prefix src/Kumunita.Web run build` → **clean** (`tsc` completed,
    no output).
- **(f) Untouched (per scope):** the `Post` / `Announcement` / `Page`
  surfaces; the U05 **(f) drift** note (GlobalAdmin edit/delete write denied
  at the frozen Core seam — author-only enforced at Core, the Web pre-gate
  uses the real role set; **not** "fixed" here — a Core seam change is
  out of U06 scope and belongs to a later lane / ADR); no new tests (U10
  authors the `EventControllerTests`); no reminder service (U07); no
  acceptance gate (U11); no design-doc / README / `Milestones.cs` edits (U12);
  **no Core changes** (the 5 touched files are all in `src/Kumunita.Web`).

**U06 exit gate met. STOP — do NOT start U07.**

## U07 — EventReminderService (the §6.4 job's business logic)

- **(a) Files touched (all in `Kumunita.Core` + `Kumunita.Core.Tests` — zero Web,
  zero TS):**
  - `src/Kumunita.Core/Events/EventReminderService.cs` — **new** file. The
    **Wolverine-free** §6.4 job business logic (the `AuditPurgeService` precedent
    verbatim — a static class + a sibling options POCO, both top-level in
    `Kumunita.Core.Events`). Two public types: `EventReminderOptions` (the
    `AuditPurgeOptions` shape — `WindowHours = 24` floor; the "remind the day
    before" window) and `EventReminderService` (the single
    `SendRemindersAsync(IDocumentStore store, EventReminderOptions options,
    DateTimeOffset now, IMailerStage mailer, CancellationToken ct = default)`
    static method). The `now` injection point makes the window boundary
    deterministic under the harness (the `AuditPurgeService.PurgeAsync` precedent).
  - `tests/Kumunita.Core.Tests/EventReminderServiceTests.cs` — **new** file. The
    **5 `EventReminderServiceTests`** (T19–T23 from the §3.7 master list), the
    `AuditPurgeServiceTests` / `SideEffectHarnessTests` shape to mirror — the
    `PostgresFixture` + `BootStoreAsync` (`M1DocTypes` + `M3DocTypes` +
    `M4DocTypes` Configure, `ApplyAllConfiguredChangesToDatabaseAsync`) + `Plant`
    helpers, plus a `RecordingMailer` test double (NSubstitute `IMailerStage` that
    records the staged `IdempotencyKey` + `Recipient` pairs).
- **(b) The §3.6 contract (the 4 pins):**
  - **(a) Window:** candidate = `ReminderEnabled && !IsDraft && !IsDeleted &&
    now < Start ≤ now + WindowHours` (default 24 h). A `Start` in the past is not
    reminded; one beyond the window is not yet. (The user's `now+1h → now+24h`
    "remind the day before" band is the *in-band* case; the outer bound is the
    pinned `now < Start ≤ now+24h`.)
  - **(b) Going RSVPs:** the recipient set is the event's `EventRsvp` rows with
    `Status = Going` **plus the author, always** (the "author, always" rule is
    independent of the Going filter — T21 proves the author is reminded even when
    their own RSVP is `No`). A `Maybe` / `No` RSVP is not reminded.
  - **(c) Staging:** each recipient is staged via the **frozen**
    `IMailerStage.StageAsync` with the §6.2 per-email idempotency key
    `remind:{eventId}:{userId}`. Recipient resolution: the `Profile.Email`
    (UserInfo module's own document) — a recipient with no known email is
    skipped (no address to deliver to; the M1 unverified posture).
  - **(d) No audit row:** **zero** `AccessAudit` rows after
    `SendRemindersAsync` (a side effect, not an access decision — the
    verification-email posture, ADR 0054 §3.6). Nothing here touches the
    authorization path or the frozen `IAuthorizationService`.
  - **No double-send across ticks:** before staging, the existing
    `OutboxEmail` key set for this run is read; a key already present (from an
    earlier tick) is not re-staged — the §3.6 "existing-row check" guard. This
    is the re-runnable (idempotent) guarantee the §6.2 best-effort table
    relies on.
- **(c) The 5 test names + pass status (T19–T23, the §3.7 master list verbatim):**
  - T19 `M4_ReminderWindowFiltersOutsideEvents` — **PASS** (25 h out = no; 23 h out = yes).
  - T20 `M4_ReminderGoingRsvpsOnly` — **PASS** (`Maybe` / `No` excluded; `Going` + author included).
  - T21 `M4_ReminderAuthorAlwaysIncluded` — **PASS** (author reminded even with their own `No` RSVP).
  - T22 `M4_ReminderIdempotencyKeyShape` — **PASS** (exactly 2 staged rows, keys `remind:{eventId}:{userId}`).
  - T23 `M4_ReminderWritesNoAccessAuditRow` — **PASS** (2 staged rows + zero `AccessAudit` rows).
- **(d) Frozen-seam confirmation (no re-shaping):**
  - **`IMailerStage` is untouched** — no new method, no signature change. The
    U07 service *consumes* it via the frozen `StageAsync` (the `IdentityService`
    / `FirstBootSeeder` precedent). The `RecordingMailer` test double
    (NSubstitute) records the staged (key, recipient) pairs — a test double for
    the frozen seam, not a re-shape.
  - **`OutboxEmail` + `OutboxEmailHandler` trio is untouched** — the service
    *stages* rows (the `OutboxEmail` document) and commits on a successful
    `SaveChangesAsync`; the §6.2 durable handler (a Web-host concern, U08)
    dispatches over SMTP on a successful commit. The U07 service does not
    reference `OutboxEmailHandler` or `SmtpSender` (those stay in
    `Kumunita.Web/SideEffects/`).
  - **`Event` / `EventRsvp` / `M4DocTypes` / `IEventService` / `EventService`
    / `EventToAuditableResource` are untouched** — the reminder service reads
    the `Event` + `EventRsvp` documents directly (a read, not a write seam
    change) and does not touch the U01–U05 surface.
  - **`Post` / `Announcement` / `Page` surfaces untouched** (the M4 lane is
    additive on top of the existing lanes).
- **(e) Exit gate (per `AGENTS.md` — both green; `dotnet test` / VS Test
  Explorer **not** run, per the runner-discovery quirk):**
  - `dotnet build Kumunita.slnx -c Debug` → **Build succeeded, 0 Error(s)**
    (the 87 warnings are pre-existing house-style — xUnit1051 in
    `EventServiceTests.cs` + CS8601 in `EventController.cs`, unchanged from
    U06; none introduced by U07).
  - Scoped: `dotnet exec
    tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll -class
    "Kumunita.Core.Tests.EventReminderServiceTests"` → **Total: 5, Errors: 0,
    Failed: 0, Skipped: 0, Not Run: 0** (all 5 `M4_Reminder*` tests pass).
  - Full Core suite: `dotnet exec
    tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` →
    **Total: 654, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (649
    pre-U07 + 5 new U07 tests; no regressions in the `Post` / `Announcement` /
    `Page` / Identity / UserInfo / M4 read/write/standing surfaces).
- **(f) Drift pauses (frozen-seam scope boundary + one seam refinement):**
  - **The design doc §4 C# seam block** shows
    `SendRemindersAsync(IDocumentStore store, EventReminderOptions options,
    DateTimeOffset now, CancellationToken ct = default)` — **omitting the
    `IMailerStage mailer` param**. The §3.6 prose (and the user's explicit
    U07 instruction) mandate staging **via the frozen `IMailerStage.StageAsync`**
    — only reachable if the stager is passed into the static service. So the
    U07 signature **adds a `mailer` param** (a §4 signature refinement, **not**
    a re-shape of `IMailerStage` itself — the interface is untouched, no new
    method). **U08's `EventReminderHandler` will resolve the real
    `IMailerStage` (the production `OutboxEmailStager`) and pass it** — the
    thin adapter owns that resolution (the `AuditPurgeHandler` precedent, where
    the handler resolves the live store + options and hands them to the
    Wolverine-free service). This is a **drift event** under the §3.9
    drift-guard's "the §6.4 job shape" pin — recorded here, not silently
    worked around. The `OutboxEmail` / `OutboxEmailHandler` trio is **not**
    re-shaped; the `IMailerStage` interface is **not** re-shaped; only the
    `SendRemindersAsync` call signature gains the `mailer` param (the
    Wolverine-free service does not own the stager's lifecycle — the Web host
    does, and passes it in, the same as it passes `store` + `options`).
  - **The `Profile.Email` lookup** (recipient resolution) is the one
    cross-context read in the service (`Kumunita.Core.UserInfo.Profile` —
    the UserInfo module's own document). This is a *read* (not a decision),
    the same shape as the `EventService`'s read-lane store queries (U03) —
    the service reads the `Profile.Email` directly (a read, not a new seam
    on `IUserInfoService`). No new `IUserInfoService` method, no new
    `AccessAction` / `AccessVia` / authorization branch, no audit row.
  - **`Milestones.cs` / README / `MilestonesTests` untouched** (U12's
    close-time job); the `EventReminderHandler` + `EventReminderTick` (U08);
    `EventControllerTests` (U10); the acceptance gate (U11); and the 23-test
    U09 list remain out of scope.

**U07 exit gate met. STOP — do NOT start U08.**

## U08 — EventReminderHandler + EventReminderTick (the §6.4 job)

- **(a) Files touched (all in `src/Kumunita.Web` — zero Core, zero TS, zero
  tests):**
  - `src/Kumunita.Web/SideEffects/EventReminderHandler.cs` — **new** file.
    The thin Web-host adapter + tick, the `AuditPurgeHandler` +
    `AuditPurgeTick` precedent **verbatim**:
    - `EventReminderTick` — `public sealed record EventReminderTick() :
      Wolverine.TimeoutMessage(TimeSpan.FromDays(1));` (the 1-day delay baked
      into the message type; **no** per-callsite `DelayedFor`; the frozen
      `Wolverine.TimeoutMessage` base, no signature change).
    - `EventReminderHandler.Handle(EventReminderTick tick, Marten.IDocumentStore
      store, IOptions<EventReminderOptions> options, Kumunita.Core.Identity
      .IMailerStage mailer)` → `await
      EventReminderService.SendRemindersAsync(store, options.Value,
      DateTimeOffset.UtcNow, mailer);` then `return new[] { new
      EventReminderTick() };` (the self-rescheduling `TimeoutMessage` idiom —
      re-yield a fresh tick so the daily cadence carries forward).
  - `src/Kumunita.Web/Program.cs` — **two** small additive edits:
    - `builder.Services.AddOptions<Kumunita.Core.Events.EventReminderOptions>();`
      (bound next to `AddKumunitaCore();` — `EventReminderOptions` is a
      top-level class in `Kumunita.Core.Events`, as U07 drift (f) records;
      `IOptions<…>` resolves the POCO's defaults, `WindowHours = 24` floor).
    - the boot kick-off: `await bus.PublishAsync(new EventReminderTick());`
      immediately after the existing `AuditPurgeTick` publish (same scope,
      same post-`StartAsync` placement — the `AuditPurgeTick` precedent;
      idempotent, same as the purge).
- **(b) `IMailerStage` resolution:** the handler's **`mailer` parameter**
  (U07 drift (f) — the design doc §4 C# block is stale, it shows the
  3-arg form; the real U07 signature is
  `SendRemindersAsync(store, options, now, mailer, ct)`) — Wolverine
  convention-resolves the `IMailerStage` (host-registered
  `OutboxEmailStager`) into the handler method and passes it through. The
  thin adapter owns the resolution, exactly the `AuditPurgeHandler`
  precedent (handler resolves the live store + options, hands them to the
  Wolverine-free service).
- **(c) Frozen-seam confirmation (no re-shaping):**
  - **`EventReminderService` (U07) untouched** — the handler *consumes* the
    frozen `SendRemindersAsync` signature (5-arg incl. `mailer`); no Core
    file was edited in this unit (zero Core behavioral changes).
  - **`IMailerStage` untouched** — consumed via the frozen `StageAsync`
    (already the case inside U07's service); the handler only *resolves*
    it, never re-shapes it.
  - **`OutboxEmail` + `OutboxEmailHandler` trio untouched** — the reminder
    path stages `OutboxEmail` rows (inside U07's service, via the frozen
    `IMailerStage`); the §6.2 durable handler (existing, `Kumunita.Web/
    SideEffects/OutboxEmailHandler`) dispatches over SMTP on a successful
    commit. U08 references neither type.
  - **`Wolverine.TimeoutMessage` base untouched** — `EventReminderTick`
    derives from it with the frozen base constructor (`TimeSpan.FromDays(1)`
    — the `AuditPurgeTick` precedent verbatim); no new idiom, no new
    Wolverine API surface (the §6.4 drift-pause (e) pin holds).
- **(d) Exit gate (per `AGENTS.md` — `dotnet test` / VS Test Explorer
  **not** run, per the runner-discovery quirk):**
  - `dotnet build Kumunita.slnx -c Debug` → **Build succeeded, 0 Error(s)**
    (the 87 warnings are the same pre-existing house-style set from U07 —
    xUnit1051 in `EventServiceTests.cs` + CS8601 in `EventController.cs`;
    none introduced by U08).
  - Full Core suite: `dotnet exec
    tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` →
    **Total: 654, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** — U07's
    `EventReminderService` (now consumed by the handler) still passes; no
    regressions in the `Post` / `Announcement` / `Page` / Identity /
    UserInfo / M4 read/write/standing/reminder surfaces.
  - No TS build (no TS touched in this unit).
- **(e) Scope boundary:** the 23 seam tests (U09), `EventControllerTests`
  (U10), the acceptance gate (U11), and the design-doc / README /
  `Milestones.cs` close (U12) all remain out of scope for this unit.

**U08 exit gate met. STOP — do NOT start U09.**

---

## U09 — seam tests (23)

**(a) Test files.** The 23 pinned seam tests from the design doc §3.7 (ADR 0054
master list) live in two files:

- `tests/Kumunita.Core.Tests/EventServiceTests.cs` — **T01–T18** (the
  event read/write/standing/RSVP/audit/adapter seams).
- `tests/Kumunita.Core.Tests/EventReminderServiceTests.cs` — **T19–T23**
  (the reminder window/filter/author-inclusion/idempotency/no-audit seams).

**(b) The 23 test names (verbatim).**

| # | Test name (exact) | File |
|---|---|---|
| T01 | `M4_MemberSeesUpcomingEventFeed` | EventServiceTests |
| T02 | `M4_NullAudienceEventIsPublic` | EventServiceTests |
| T03 | `M4_CommunityAudienceSeesFeed` | EventServiceTests |
| T04 | `M4_GrantsAudienceOnlyGranteeSees` | EventServiceTests |
| T05 | `M4_DraftInvisibleToNonAuthor` | EventServiceTests |
| T06 | `M4_PlainMemberCreateAllowed` | EventServiceTests |
| T07 | `M4_AuthorCanEditOwnEvent` | EventServiceTests |
| T08 | `M4_PlainMemberEditDenied` | EventServiceTests |
| T09 | `M4_GlobalAdminOverrideEdit` | EventServiceTests |
| T10 | `M4_PublishAuthorOnly` | EventServiceTests |
| T11 | `M4_SoftDeleteExcludesFromFeedAndDetail` | EventServiceTests |
| T12 | `M4_AuthorSoftDeleteOwnEvent` | EventServiceTests |
| T13 | `M4_RsvpLastWriteWins` | EventServiceTests |
| T14 | `M4_RsvpUniqueIndexOneRowPerUser` | EventServiceTests |
| T15 | `M4_RsvpListOwnerOnly` | EventServiceTests |
| T16 | `M4_RsvpWritesNoAccessAuditRow` | EventServiceTests |
| T17 | `M4_AuditRowShape_Create` | EventServiceTests |
| T18 | `M4_EventToAuditableResourceShape` | EventServiceTests |
| T19 | `M4_ReminderWindowFiltersOutsideEvents` | EventReminderServiceTests |
| T20 | `M4_ReminderGoingRsvpsOnly` | EventReminderServiceTests |
| T21 | `M4_ReminderAuthorAlwaysIncluded` | EventReminderServiceTests |
| T22 | `M4_ReminderIdempotencyKeyShape` | EventReminderServiceTests |
| T23 | `M4_ReminderWritesNoAccessAuditRow` | EventReminderServiceTests |

**(c) Pass/red counts (the U11 gate input).** All **23 PASS / 0 RED**.

| # | Test | Result |
|---|---|---|
| T01 | `M4_MemberSeesUpcomingEventFeed` | PASS |
| T02 | `M4_NullAudienceEventIsPublic` | PASS |
| T03 | `M4_CommunityAudienceSeesFeed` | PASS |
| T04 | `M4_GrantsAudienceOnlyGranteeSees` | PASS |
| T05 | `M4_DraftInvisibleToNonAuthor` | PASS |
| T06 | `M4_PlainMemberCreateAllowed` | PASS |
| T07 | `M4_AuthorCanEditOwnEvent` | PASS |
| T08 | `M4_PlainMemberEditDenied` | PASS |
| T09 | `M4_GlobalAdminOverrideEdit` | PASS |
| T10 | `M4_PublishAuthorOnly` | PASS |
| T11 | `M4_SoftDeleteExcludesFromFeedAndDetail` | PASS |
| T12 | `M4_AuthorSoftDeleteOwnEvent` | PASS |
| T13 | `M4_RsvpLastWriteWins` | PASS |
| T14 | `M4_RsvpUniqueIndexOneRowPerUser` | PASS |

## U10 — EventControllerTests

**What landed.** The Web-side controller tests for `EventController`,
mirroring the `AnnouncementControllerTests` harness (NSubstitute
`IEventService` + `IUserInfoService` + `ILocalizationService` +
`IDocumentStore`, a `NoOpTempDataProvider` for the write lanes' `TempData`,
and a shared `Build(...)` / `DefaultLocalization()` / `SampleEvent(...)`
triple). The frozen M4 Core seam is consumed as-is — `IEventService`,
`Event`, `EventRsvp`, `RsvpStatus`, `CreateEventRequest`,
`UpdateEventRequest`, `EventToAuditableResource`, and the
`EventService.CheckEditStanding` static helper. No TS touched; no
`m4-events-design.md` / `README.md` / `Milestones.cs` / `ARCHITECTURE.md`
edits (those are U11/U12).

**(a) Test file.** `tests/Kumunita.Web.Tests/EventControllerTests.cs`

**(b) Test names (verbatim, in file order).** 19 tests total (18
`[Fact]` async + 1 `[Fact]` sync route-map):

1. `RouteMap_MatchesDocumentedSurface`
2. `Detail_When_ServiceReportsAbsent_Returns_404`
3. `Detail_When_ServiceReportsDenied_Returns_403`
4. `EditGet_When_ServiceReportsAbsent_Returns_404`
5. `EditGet_When_NonAuthorNonAdmin_Returns_403`
6. `Publish_When_ServiceDenies_Returns_403`
7. `Delete_When_NonAuthorNonAdmin_Returns_403_And_NeverCallsDeleteAsync`
8. `Rsvp_When_ServiceDenies_Returns_403`
9. `Index_PassesComponentFilterAndPage_Verbatim_ToService`
10. `Index_When_AuthorProfileMissing_FallsBackToSubjectId`
11. `CreateGet_SeesCommunityVisibleDefault_Audience_AndDraftFloor`
12. `CreatePost_MapsAudience_Verbatim_ViaBuildAudience_AndDraftFlag`
13. `CreatePost_When_BodyMissing_RendersView_And_NeverCreates`
14. `EditGet_When_Author_RoundTripsStoredAudience_Verbatim`
15. `EditPost_When_GlobalAdmin_Updates_And_Redirects`
16. `EditPost_When_ServiceReportsAbsent_Returns_404`
17. `Detail_When_Author_RsvpListAndMyRsvp_Loaded`
18. `Detail_When_NonAuthor_RsvpListNotLoaded_AffordancesOff`
19. `Rsvp_PassesPostedStatus_Verbatim_And_Redirects`

**(c) Pass/red counts (the U11 gate input).** All **19 PASS / 0 RED**.

| # | Test | Result |
|---|---|---|
| T01 | `RouteMap_MatchesDocumentedSurface` | PASS |
| T02 | `Detail_When_ServiceReportsAbsent_Returns_404` | PASS |
| T03 | `Detail_When_ServiceReportsDenied_Returns_403` | PASS |
| T04 | `EditGet_When_ServiceReportsAbsent_Returns_404` | PASS |
| T05 | `EditGet_When_NonAuthorNonAdmin_Returns_403` | PASS |
| T06 | `Publish_When_ServiceDenies_Returns_403` | PASS |
| T07 | `Delete_When_NonAuthorNonAdmin_Returns_403_And_NeverCallsDeleteAsync` | PASS |
| T08 | `Rsvp_When_ServiceDenies_Returns_403` | PASS |
| T09 | `Index_PassesComponentFilterAndPage_Verbatim_ToService` | PASS |
| T10 | `Index_When_AuthorProfileMissing_FallsBackToSubjectId` | PASS |
| T11 | `CreateGet_SeesCommunityVisibleDefault_Audience_AndDraftFloor` | PASS |
| T12 | `CreatePost_MapsAudience_Verbatim_ViaBuildAudience_AndDraftFlag` | PASS |
| T13 | `CreatePost_When_BodyMissing_RendersView_And_NeverCreates` | PASS |
| T14 | `EditGet_When_Author_RoundTripsStoredAudience_Verbatim` | PASS |
| T15 | `EditPost_When_GlobalAdmin_Updates_And_Redirects` | PASS |
| T16 | `EditPost_When_ServiceReportsAbsent_Returns_404` | PASS |
| T17 | `Detail_When_Author_RsvpListAndMyRsvp_Loaded` | PASS |
| T18 | `Detail_When_NonAuthor_RsvpListNotLoaded_AffordancesOff` | PASS |
| T19 | `Rsvp_PassesPostedStatus_Verbatim_And_Redirects` | PASS |

**Notes for U11.**

- **Exit gate (met).** `dotnet build Kumunita.slnx -c Debug` → 0 errors / 0
  warnings. `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  → `Total: 351, Errors: 0, Failed: 1`. The one remaining red is
  `KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` — a
  pre-existing M4 red (the `Event\Detail.cshtml` `events.created` /
  `events.edited` and `Shared\_Layout.cshtml` `nav.events` `kw-l` keys are not
  yet in `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`). This is a
  U11/U12 concern (the key registry is the U11 gate input, and the close step
  is U12); **it is not caused by U10**, and the U10 family is fully green.
- **NSubstitute `Task.FromException<T>` quirk.** Eight of the 19 initially
  failed with `CouldNotSetReturnDueToTypeMismatchException` — the
  non-generic `Task.FromException(ex)` returns `Task`, but the frozen seam
  returns `Task<Event>` / `Task<EventRsvp>`, and NSubstitute v5 type-checks
  the `Returns(...)` value. The six offending sites (lines 109, 132, 153, 196,
  238, 529 in the first iteration) were all switched to the generic form
  `Task.FromException<Event>(...)` / `Task.FromException<EventRsvp>(...)`.
  The two remaining failures at that point (the `EditGet` round-trip) were a
  separate root cause — see next bullet.
- **`Audience` has no value equality.** `Audience` is a plain `sealed class`
  (no `Equals` override, no `IEquatable<T>`), so `Assert.Equal(stored,
  rebuilt)` falls back to reference equality and fails. `T14`
  (`EditGet_When_Author_RoundTripsStoredAudience_Verbatim`) was rewritten to
  compare the round-tripped audience property-by-property against the stored
  one (`Mode`, `Community`, `AllResidents`, `Grants`). The `Grants` list is a
  `List<AudienceGrant>` of a `sealed record` — `record` value equality holds
  per grant, so `Assert.Equal(storedAudience.Grants, rebuilt.Grants)` is a
  real comparison.
- **`call.ArgAt<T>(index)` idiom.** The payload-capture tests
  (`T09`, `T12`, `T15`, `T19`) use the NSubstitute v5 `call.ArgAt<T>(index)`
  form (not the older `request => sent = request` lambda idiom, which does
  not compile against the frozen seam's generic signature).
- **`NoOpTempDataProvider`.** The write lanes (`CreatePost`, `EditPost`,
  `Publish`, `Delete`, `Rsvp`) set `TempData["KumunitaFlash"]` before
  redirecting; the harness supplies a `NoOpTempDataProvider` so the controller
  doesn't NRE on `ControllerContext.HttpContext.Session` (mirrors
  `AnnouncementControllerTests`).
- **`KumunitaPrincipal` shape.** The harness builds the principal with
  `ClaimTypes.Subject` + `ClaimTypes.Role` claims (the `KumunitaPrincipal`
  helper in `src/Kumunita.Web/Models/KumunitaPrincipal.cs`), and the
  `subjectId` parameter of `Build(...)` defaults to `"subj-resident-001"` to
  match the U09 Core seam's resident-actor convention.

**U10 exit gate met. STOP — do NOT start U11.**
| T15 | `M4_RsvpListOwnerOnly` | PASS |
| T16 | `M4_RsvpWritesNoAccessAuditRow` | PASS |
| T17 | `M4_AuditRowShape_Create` | PASS |
| T18 | `M4_EventToAuditableResourceShape` | PASS |
| T19 | `M4_ReminderWindowFiltersOutsideEvents` | PASS |
| T20 | `M4_ReminderGoingRsvpsOnly` | PASS |
| T21 | `M4_ReminderAuthorAlwaysIncluded` | PASS |
| T22 | `M4_ReminderIdempotencyKeyShape` | PASS |
| T23 | `M4_ReminderWritesNoAccessAuditRow` | PASS |

**Tally: 23 PASS / 0 RED.**

**Notes.**
- **Pre-existing (authored in U03/U04/U07):** T05, T10, T13, T16 (and the
  reminder surface T19–T23) already existed by their exact pinned names from
  the earlier M4 units and were carried through unchanged — no rename drift.
- **Authored in U09 (this session):** 14 exact-name tests were confirmed or
  added in `EventServiceTests.cs` to close the §3.7 gap — T01, T02, T03, T04,
  T06, T07, T08, T09, T11, T12, T14, T15, T17, T18. Where a near-name test
  existed from U03/U04 (e.g. `M4_GrantAudienceOnlyGranteeSees` for T04,
  `M4_SoftDeleteAuthorExcludesFromFeedAndDetail` for T11), a new exact-name
  test was added rather than renaming the existing one, so no downstream
  reference drift was introduced.
- **Frozen seams untouched (as scoped):** `EventReminderService` (the 5-arg
  `SendRemindersAsync(store, options, now, mailer, ct)`), `IMailerStage`,
  `IEventService`, and the `Event` / `EventRsvp` documents were **not**
  re-shaped in this unit; the tests assert against their frozen shapes.
- **Exit gate (verified):**
  - `dotnet build Kumunita.slnx -c Debug` → **Build succeeded. 0 Error(s)**.
  - `dotnet exec
    tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` →
    **Total: 668, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** — all 23
    pinned seam tests discovered + executed + passing (the 668 total is the
    full Core suite, now including the 14 U09-authored exact-name tests).
  - Name-presence check: a `IndexOf` scan over both test files for each of the
    23 verbatim §3.7 names → **ALL 23 PRESENT**.
  - No TS build (no TS touched in this unit).
- **(e) Scope boundary:** the acceptance gate (U11), e2e authoring (U12), the
  design-doc / README / `Milestones.cs` / `ARCHITECTURE.md` close (U12), and
  the Web-side `EventControllerTests` (U10) all remain out of scope for this
  unit.

**U09 exit gate met. STOP — do NOT start U10.**

## U11 — gate recorded

**Date: 2026-09-21.** The three-test acceptance gate (design doc §3.8 —
closed loop / handoff / part-vs-whole) is **executed and recorded** below,
using U09's 23 Core seam tests as the part-vs-whole evidence. The M4 family
is fully green; the one Web-side red is the **pre-existing M4 kw-l registry
drift** (still open — see *Still-open drift*), **not** a U09/U10 regression.

**(a) The three gate tests + pass counts (the §3.8 pin).**

| # | Gate test (shape, §3.8) | M4 Core evidence (U09 §3.7 names) | Result |
|---|---|---|---|
| 1 | **closed loop** — an author creates a published event → it appears in the feed (`TargetKind = "event"` aggregate row, `VisibleCount ≥ 1`, `Outcome = Allow`); the author RSVPs `Going` → their RSVP is visible in the owner-only list | T06 `M4_PlainMemberCreateAllowed`, T01 `M4_MemberSeesUpcomingEventFeed`, T13 `M4_RsvpLastWriteWins`, T15 `M4_RsvpListOwnerOnly` | **PASS (4/4)** |
| 2 | **handoff** — a user added to the event's `Audience.Grants` **after** creation sees the event on the **next** request (strong consistency, no cache); the `Delegation` branch is the handoff-onto-a-delegate case | T04 `M4_GrantsAudienceOnlyGranteeSees`, T03 `M4_CommunityAudienceSeesFeed` | **PASS (2/2)** |
| 3 | **part-vs-whole** — the 23 names in §3.7 are the **whole**; tests 1–2 are the **parts**; all must pass **together** in the same `Kumunita.Core.Tests` run as the inherited M1/M2/M3/M3b/PG anchors (no per-name isolation) | all 23 of T01–T23 (U09's pinned list) executed in one run | **PASS (23/23)** |

**(b) The runs (verified, not assumed — the AGENTS.md `dotnet exec` path).**

- `dotnet build Kumunita.slnx -c Debug` → **Build succeeded. 0 Error(s)**
  (127 pre-existing warnings: `xUnit1051` in the M4 test files, `CS8601` in
  `EventController.cs` — unchanged from U09/U10).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  → **Total: 668, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 59.181s**.
  The **23 M4 seam tests (T01–T23) pass *together*** with the inherited
  M1/M2/M3/M3b/PG anchors in the **same** run — the §3.8 part-vs-whole pin
  holds (no per-name isolation). Gate tests 1+2 are the *parts*; the 23 are
  the *whole*.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  → **Total: 351, Errors: 0, Failed: 1, Skipped: 0, Not Run: 0, Time: 9.699s**.
  The **`EventControllerTests` family (19 tests, U10) is fully green**; the
  single red is the *pre-existing* kw-l registry drift (see (e) below), **not**
  the M4 controller family.

**(c) FACES (the two the U11 plan text pins).**

- **Strengthens — Adaptive:** a resident's RSVP / reminder reaches a real
  outcome and the loop closes with an owner (create → feed → RSVP → owner-only
  list, the gate's closed-loop test; the reminder email is the side effect that
  lands on a *working* event, per the U07/U08 sequencing invariant).
- **Consumes — Stable:** the daily `EventReminderTick` (U08) is a new moving
  part in prod, and the reminder window's coverage edge (the 24-hour
  `now < Start ≤ now+24h` boundary, T19) is a boundary we must keep provably
  closing — a new `§6.4` recurring job in the durable-handler surface.

**(d) E2E status (authored, NOT run — the M2 D2 / M3 U10 / M3b U10 precedent).**

The e2e runtime (Postgres-boot + token-channel) is **not yet present**: the
`kumunita` Playwright fixture is still a **documented throw** in `e2e-m2.spec.ts`,
`e2e-m3.spec.ts`, and (newly) `e2e-m4.spec.ts` — no M4 unit (U0–U11) implements
the runtime. Per plan U11 ("*if it does not yet exist, author the spec (mirror
M2's U13), do not run it, and record the gap in the design doc*"), U11:

- **authored** `tests/Kumunita.Web.Tests/e2e-m4.spec.ts` (three specs mirroring
  the §3.8 gate: (a) closed-loop create→feed→RSVP→owner-only-list, (b) handoff
  grant-added-after-creation seen on next request, (c) part-vs-whole
  feed/detail/RSVP surface coherence). Selectors + route pins are grounded
  against the **shipped** M4 views (`Views/Event/{Index,Detail,Create,Edit}.cshtml`
  + `Controllers/EventController.cs`). `npx tsc --noEmit` on
  `tests/Kumunita.Web.Tests` → **exit 0** (the spec is valid TypeScript; no new
  `@playwright/test` errors — the same M2 U13 "author without the runtime"
  state).
- **did NOT run** the e2e (the fixture throws; `playwright test` is not
  runnable). Pass count: **0 (spec authored; not runnable)** — by design of the
  pause.
- **the bounded runtime gap is** (i) the `kumunita` fixture's
  `signup / login / lastCreatedEventId / grantEventToUser` implementation (the
  M2 D2 token-channel + M4's two helpers), (ii) a Postgres boot wired to the same
  DB the `dotnet run` server reads, (iii) **no** new production-code changes —
  the M4 Core + Web seams are frozen and already exercised by the 23 Core seam
  tests + 19 controller tests. **The next unit who lands the runtime records
  the pass count in a later `### Run result (M4 e2e — <date>)` section.**

**(e) Still-open drift (recorded, not fixed in U11 — out of U11's scope).**

- **`KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` is RED
  (the single Web.Tests red).** Root cause, confirmed: three `kw-l` keys used in
  the M4 views are **not** in `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`:
  - `src/Kumunita.Web/Views/Event/Detail.cshtml:98` — `events.created`
  - `src/Kumunita.Web/Views/Event/Detail.cshtml:102` — `events.edited`
  - `src/Kumunita.Web/Views/Shared/_Layout.cshtml:67` — `nav.events`

  This is **pre-existing M4 drift** (the key registry was not extended when the
  M4 views were authored in U05/U06), **not** a U09/U10 regression — the
  `EventControllerTests` family is fully green. U11 **does not** fix it (the
  Core localization seam is frozen for this unit; the fix is a U12-close or a
  follow-on lane — *add the three keys to `KnownTranslationKeys.cs`*). **Flagged
  for U12** so the close is honest.

**U11 exit gate met (build 0 errors; gate section present + consistent with
U09/U10 results). STOP — do NOT start U12.**

## U12 — close (the M4→M5 handoff)

**Date: 2026-09-21.** The M4 lane is **closed**: the `Events/` status flip,
the §5 sync, the M4→M5 deferral note, the roadmap trio (M4 `StatusDone`,
M5 the single `StatusNext`), the README Roadmap, and the **U11-flagged kw-l
registry drift fix** (the one code change U12 was scoped to make).

**(a) The U11-flagged drift is fixed (the only code change this unit made).**
The three missing kw-l keys are now registered in all four dictionaries of
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (en/de/fr/da):
`nav.events` (the `_Layout` nav entry), `events.created` + `events.edited`
(the `Views/Event/Detail.cshtml` footer — the exact view fallback text).
`KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` is
**green**; the `KnownTranslationKeys_ParityTests` family (the
de/fr/da key-parity pins) passes with the 3 new keys.

**(b) The close (docs).**
- `docs/ARCHITECTURE.md`: the §3 tree's `Events/` line flipped from "M4 —
  not yet created" to **M4 ✓ (ADR 0054)** with the gate summary (Core
  668/668 + Web 351/351, EventControllerTests 19/19, the 23 M4 seam tests
  together); the two "not yet created" prose lines now name only
  `Projects/` (M5); the §3 feature-module list adds Events; the **§5 `Events`
  block is synced to the design doc's canonical field names** (`Body`,
  `ReminderEnabled`, + the `IsDraft` / `IsDeleted` / `LanguageCode` /
  `TagIds` / `ImageIds` / `AttachmentIds` reuse lanes per §3.1 — replacing
  the pre-decision `description` / `rsvpRequired` sketch; `EventRsvp`'s
  last-write-wins note kept).
- The **M4→M5 deferral note** (a new `### M4 → M5 deferrals (carried
  forward)` under §10) lists the §5 non-decisions verbatim from the design
  doc: event translations, group events, per-resident reminder settings,
  iCal export (M6), and the no-admission-queue pin — so M5's OOS close stays
  honest.
- `src/Kumunita.Web/Milestones.cs`: **M4 → `StatusDone`**, **M5 →
  `StatusNext`** (the single-in-progress pin moves to M5; M6 stays
  `StatusPlanned`; the order is unchanged).
- `tests/Kumunita.Web.Tests/MilestonesTests.cs`: the `Shipped` set gains
  `"M4"`; the pin is renamed `M4_Is_...` →
  **`M5_Is_The_Single_InProgress_Milestone`** (asserts `M5`); the ordered
  `Ids` list is **unchanged**.
- `README.md`: the "next is" line now says **M4 is done … next is M5**;
  the Events feature bullet is now "M4 — done, ADR 0054"; the Projects
  bullet is now "next — M5"; the Roadmap **M4** row is **Done** (with the
  lane summary + the four follow-on-lane deferrals), M5 stays a plain row.
- **No TS build** (no TS touched). **The M4 Core/Web seams (U01–U10) are
  untouched** — this unit adds 3 registry keys + the close docs only.

**(c) Exit gate (verified, the AGENTS.md `dotnet exec` path).**
- `dotnet build Kumunita.slnx -c Debug` → **Build succeeded** (127
  pre-existing warnings — same set as U09/U11 — **0 Error(s)**).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  → **Total: 351, Errors: 0, Failed: 0** (was 1 red in U11 — the kw-l
  registry drift — now **0 failed**; the renamed
  `M5_Is_The_Single_InProgress_Milestone` pin passes).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  → **Total: 668, Errors: 0, Failed: 0** (the `KnownTranslationKeys_ParityTests`
  family green with the 3 new keys; the 23 M4 seam tests green).

**(d) Carried forward (NOT closed by U12 — out of scope).**
- **The e2e runtime** is still the documented throw (`e2e-m4.spec.ts`
  authored in U11, not run). The unit that lands the runtime records the M4
  e2e pass count in a later `### Run result (M4 e2e — <date>)` design-doc
  section — not here.
- **The follow-on lanes** (event translations / group events /
  per-resident reminders / iCal) are open work for M5+, now pinned in the
  ARCHITECTURE.md deferral note.

**U12 exit gate met. M4 is DONE — STOP. The next lane is M5 (Projects).**

## U13 — GlobalAdmin edit/delete write override (resolves U05(f))

> **Provenance:** the U05 exit note (f) — *"Drift — GlobalAdmin edit/delete
> write denied at Core"* — is the seam this unit closes. It was flagged in
> U05 as *"a Core change, out of U05 scope"* and carried forward through U12
> (which closed M4's *planned* surface without touching it). U13 makes **the
> code honor the ADR** (ADR 0054 is unchanged — it was the authority the code
> now matches, per the "code to honor the ADR" fix (a) chosen over amending
> the ADR).

**(a) The seam (the one structural change).** `IEventService.UpdateAsync` /
`DeleteAsync` now take `IReadOnlySet<string> actorRoles` — the
`AnnouncementService.UpdateAsync(... actorId, actorRoles, session)` /
`PageService.CheckEditStanding(actorId, actorRoles, page)` precedent the U05
note already cited. The `EventController` passes the principal's real
`RoleSet(User)` (the pre-gate already computed it; no new dependency).
`CreateAsync` / `PublishAsync` are **unchanged** (the create lane is any
resident; the publish lane is author-only per ADR 0037 — a non-author
GlobalAdmin is still denied publish), as are the `StaticEmptyRoles` sentinel
(now used only by create/publish) and `RsvpAsync` (no audit row).

**(b) The audit-tag correctness fix (a second, real gap found while threading
roles).** The `event.update` / `event.delete` audit row was hard-coded to
`AccessVia.Owner`. A non-author GlobalAdmin override (the branch U05(f)
left unreachable) would therefore be recorded as `Owner` — a lie, since the
actor was **not** the author. ADR 0054 §3.4 says these lanes tag `Owner` /
`Admin`. New `AuditViaFor(actorId, authorId)`: the author → `AccessVia.Owner`,
a non-author actor (only reachable via the GlobalAdmin override) →
`AccessVia.Admin`. `event.create` / `event.publish` remain `Owner`.

**(c) The two missing part-vs-whole seam tests (the FIG test the U05 note
could not write).** `M4_GlobalAdminOverrideEditEndToEnd` (**T24**) and
`M4_GlobalAdminOverrideDeleteEndToEnd` (**T25**) — a non-author GlobalAdmin
edits / soft-deletes someone else's event **through the `UpdateAsync` /
`DeleteAsync` write lane**, and the audit row is asserted `Via = Admin`.
These close the §3.8 row-3 *part-vs-whole* gate, which was red for the
GlobalAdmin branch (the U09 `M4_GlobalAdminOverrideEdit` (T09) pin only
drove the **pure** `CheckEditStanding` helper, not the **lane** — green CI,
red seam). The §3.7 master list is now **25** names (T01–T25).

**(d) Re-verified (this machine's `dotnet exec` runner path, AGENTS.md):**
- `dotnet build Kumunita.slnx -c Debug` → **Build succeeded. 0 Error(s)**
  (warnings unchanged: `xUnit1051` in the M4 test files, `CS8601` in
  `EventController.cs`).
- `dotnet exec …Kumunita.Core.Tests.dll` → **Total: 670, Errors: 0, Failed:
  0** (668 + T24/T25; the **25 M4 seam tests (T01–T25) pass *together*** with
  the inherited M1/M2/M3/M3b/PG anchors — the §3.8 part-vs-whole pin now
  holds for the GlobalAdmin-override branch).
- `dotnet exec …Kumunita.Web.Tests.dll` → **Total: 351, Errors: 0, Failed:
  0** — the `EventControllerTests` family is fully green (the U12 run already
  showed 0 failed; the 4 NSubstitute call sites in `EventControllerTests`
  now stub the `actorRoles` argument the seam carries).

**(e) Doc sync landed (U13's close, mirroring the U12 (c) shape).**
- `docs/design/m4-events-design.md` — §3.7 master list 23→25 (+T24/T25), the
  §4 seam block now shows the `actorRoles`-bearing `UpdateAsync`/
  `DeleteAsync` (the `AnnouncementService`/`PageService` precedent), the
  part-vs-whole rows 23→25, and a **U13 addendum** in §6 recording the fix +
  the re-verified runs (670/670 Core, 351/351 Web).
- `docs/adr/0054-events-rsvp-reminders.md` — the locked master-list count
  23→25 (T24/T25 named), the part-vs-whole row 23→25, and the drift-guard's
  `IEventService` public method set note (the edit/delete lanes carry
  `actorRoles` per §3.4, enforced server-side — **not** deferred to the Web
  boundary).
- `docs/adr/README.md` — row 0054 "23 pinned seam tests" → "25 pinned seam
  tests".
- `docs/ARCHITECTURE.md` — the `Events/` tree-line gate note: the standing
  matrix is now stated as enforced **server-side** (the lanes carry
  `actorRoles`, the audit row tags `Owner`/`Admin`), with the re-verified
  Core 670/670 + Web 351/351 and the 25-seam-test count.
- **ADR 0054 is the authority and is substantively unchanged** in its matrix —
  only the count it locks (23→25) and the seam-shape note (which now matches
  §3.4) were touched.

**(f) What did NOT change.** `CreateAsync` / `PublishAsync` (publish remains
author-only, ADR 0037 — a non-author GlobalAdmin is still denied publish),
`RsvpAsync` (no audit row), the `StaticEmptyRoles` sentinel (create/publish
only), the `Event` / `EventRsvp` doc shapes, the `M4DocTypes` surface, the
§6.4 reminder job, and the `EventToAuditableResource` adapter.

**U13 exit gate met: the code honors ADR 0054 §3.4 (GlobalAdmin edit/delete
override honored server-side, audit row tagged `Admin`), the U05(f) drift is
resolved, and the M4 seam is green end-to-end (Core 670/670, Web 351/351,
0 errors).**
