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
