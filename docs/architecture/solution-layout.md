---
id: arch-solution-layout
title: "Solution layout"
tier: reference
kind: architecture
status: current
audiences: [developer, agent, platform-maintainer]
related:
  - docs/ARCHITECTURE.md
  - docs/adr/0004-data-persistence-and-schema-evolution.md
  - docs/design/m1-identity-access.md
updated: 2026-11-02
---

# Solution layout

> **Abstract:** the canonical file-and-folder tree of the repo, annotated per bounded context with the ADRs that settled it and the design docs that own the lane. **Who is it for:** a developer onboarding to the layout, or an agent that needs to find the canonical home for a symbol before touching it. **When to read the rest:** before creating a new bounded context, a new DocTypes surface, or a new Web controller — the tree is the contract for where each belongs.


    kumunita/
    ├── Kumunita.slnx
    ├── README.md
    ├── Dockerfile                  # multi-stage (PowerShell, tsc → publish → runtime)
    ├── docker-compose.yml          # dev: postgres:18 + mailpit
    ├── dev-db-init/                # Postgres init scripts (app role, port parity with prod)
    ├── .github/workflows/          # CI
    ├── docs/
    │   ├── ARCHITECTURE.md
    │   ├── SECURITY.md             # threat model, data classes, control map
    │   ├── OPS.md                  # operations runbook
    │   ├── adr/                    # 0001 onward (the running decision ledger — append-only, highest number = newest)
    │   ├── design/                 # per-milestone design docs (M1: m1-identity-access.md)
    │   └── philosophy/             # development philosophy (START-HERE.md, templates/)
    ├── src/
    │   ├── Kumunita.Core/          # domain, services, Marten, the Wolverine-free side-effect business logic
    │   │   ├── CommunityOptions.cs # per-instance config (ADR [0002](../adr/0002-deployment-topology.md))
    │   │   ├── KumunitaFeature.cs  # first versioned `mt` storage feature (ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md) §B)
    │   │   ├── CoreDocTypes.cs       # M1 Marten-native doc registration (ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md) §B.1)
    │   │   ├── PostDocTypes.cs       # M3 + M3b Marten-native doc registration (Post, PostReply, Report, Announcement)
    │   │   ├── MediaDocTypes.cs    # M4-adjacent Marten-native doc registration (MediaObject) — ADR [0011](../adr/0011-media-and-file-storage.md)
    │   │   ├── Bootstrap/          # SchemaBootstrap, FirstBootSeeder
    │   │   ├── Identity/           # IdentityModule (M1) + DbBootstrap (first-boot pristine gate); also the side-effect seam: ISmtpSender/SmtpSender, IMailerStage/OutboxEmailStager, EmailDeadLetterWriter; AppDbContext lives here (EF Core, `identity` schema, ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md)); GA ✓ (ADR [0038](../adr/0038-guardian-assignment.md)) — `IIdentityService.FindSubjectByEmailAsync` (the email → subject id resolution; G-A·2 no-leak shape: null on unknown email, no auto-create); M19 ✓ (ADR [0120](../adr/0120-guest-accounts.md)) — the guest standing: the `GuestAccess` doc (the admin-settled, time-bounded allowance: `ValidFrom`/`ValidUntil` window + the closed `[Flags]` `GuestSurface` set, empty floor; one audited write lane `IIdentityService.SetGuestAccessAsync`, `Via = Admin`) registered on `CoreDocTypes` + the additive `Profile.IsGuest` flag (UserInfo) + the `Roles.Guest` claim mint at the Identity↔cookie seam (Web `KumunitaClaimsPrincipalFactory`) — a standing, not a new entity / bounded context; zero new authorization surface (C-M19·1/2/6); see design/m19-guest-accounts-design.md
    │   │   ├── UserInfo/           # UserInfoModule (M1) + M2 directory/profile-editor/groups surface: DirectoryService (list/detail/preview), Profile, Group, DelegationGrant, Component, IUserInfoService; GU ✓ (ADR [0028](../adr/0028-guardian-controls-account-scope-supervision.md)) — GuardianLink (account-scope supervision: suspend / membership curation / invitation approval; **no content read**, G·1) + the `AccessVia.Guardian` standing (the 9th value) + the IUserInfoService guardian seams (formation / suspend / dissolve); see design/guardian-controls-design.md § GU — Closed (recorded) (2026-09-14); GA ✓ (ADR [0038](../adr/0038-guardian-assignment.md)) — the Detail view's "other guardians" list: the `GuardianItem` record + the `MembershipEditorModel.GuardianItems` field (the assigned guardian's display name; G-A·3 identical-in-kind pin — the standing is identical to the creator's, no content read); see design/guardian-assignment-design.md § GA — Closed (recorded) (2026-09-17); GU-CA ✓ (ADR [0141](../adr/0141-guardian-community-approval-lane.md)) — the guardian community-approval lane: the `CommunityMembershipRequest` doc (Pending/Approved/Declined, one row per (component, child)) + the `RejectGroupInvitationAsync` / `ApproveCommunityMembershipRequestAsync` / `DeclineCommunityMembershipRequestAsync` / `GetPendingCommunityMembershipRequestsForChildAsync` seams (guardian-standing resolve; approve upserts the live `ComponentMembership` — C4) + the supervised-child branch of the invite/community-add lanes (pending row instead of live membership; the `guardian.group_invite` / `guardian.community_invite` notification kinds deep-link at `/me/children/{childId}`) + the `PendingCommunityRequestItem` record + the 6th `MembershipEditorModel.PendingCommunityRequests` field; the guardian's own curation add stays immediate (`Via: Guardian`, the ADR [0028](../adr/0028-guardian-controls-account-scope-supervision.md) precedent); M28 ✓ (ADR [0151](../adr/0151-guardian-time-limits.md)) — the guardian time-limit seam on the same GU surface (not a new bounded context): the `GuardianTimeLimitSchedule` doc (per-child, `Id = ChildId`, the `NotificationQuietSchedule` shape) + the `TimeLimitMode` enum registered on `CoreDocTypes` next to `GuardianLink`; the three additive `IUserInfoService` seams (`GetChildTimeLimitAsync` guardian-gated read / `SetChildTimeLimitAsync` guardian-gated write, one `guardian.time-limit.set` audit row, `null` deletes / `GetActiveTimeLimitAsync` the child-keyed enforcement read, no audit); the pure `GuardianTimeLimitEvaluator.IsAllowedNow` verdict (wall-clock-first, ADR [0019](../adr/0019-timezone-platform-default-and-resident-override.md); no `IAuthorizationService` call); the `TimeLimitMiddleware` sign-out gate (the `BlockedAccountMiddleware` analog, registered after it); zero new `AccessAction` / `AccessVia` / authorization branch (C-M28·5)
    │   │   ├── Authorization/      # AuthorizationModule (M1) — audiences, policy, audit; AuditPurgeService (Wolverine-free tiering); AdminOverride (break-glass read path)
    │   │   ├── Sessions/           # sessions ✓ live (the `sessions` named lane, ADR [0281](../adr/0281-session-doc-and-per-session-revocation.md), "M37-adjacent") — the **first session-data milestone**: a new bounded context holding the per-session model on top of F5's whole-account stamp rotation (the F5 `SecurityStamp` column + `RevocationStampMiddleware` are **unchanged**; the two mechanisms coexist and are independent — the stamp is the whole-account kill, `SessionDoc.RevokedAt` is the per-session kill). One new domain document `SessionDoc` (7 fields: `Id` GUID / `UserId` / `UserAgent` / `Ip` / `CreatedAt` / `LastActive` / `RevokedAt?` — the active/revoked discriminator; Marten-native on the new `SessionDocTypes` parallel surface, ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md) §B.1, **no seeding** — sessions are created on sign-in) + the **single** composition point `SessionService` (ctor `(IDocumentStore)`, the frozen 5-method surface `CreateSessionAsync` / `GetSessionsForUserAsync` / `RevokeSessionAsync` / `RevokeAllButCurrentAsync` / `TouchSessionAsync`; the middleware + controller are thin callers, the M3 `PostService` pattern). The `kumunita.sid` cookie item (a cookie item, not a claim — the F5 `kumunita.stamp` precedent) is set in the `OnSigningIn` handler; the `SessionRevocationMiddleware` (the per-session analog of `RevocationStampMiddleware`, registered **after** it) enforces per-request validity + the `LastActive` touch (the `Kumunita:Sessions:TouchIntervalSeconds` config key, default 60); the `SecuritySettingsController` extension is the thin HTTP surface (the `GET /sessions` list + `POST /sessions/{id}/revoke` + the kill-all extension). **Zero** new `AccessAction` / `AccessVia` / `Decide()` branch (the `session.revoke` / `session.revoke_all_but_current` audit `Action` strings are a new row shape, not a new enum member; no `IAuthorizationService` call — the C-S1·3 owner-scoped pin is a single `userId == currentSubject` check at the owner boundary). The one `C3` same-transaction relaxation (the kill-all extension's two commits land in separate transactions within the same request — the F5 Identity/EF path and the sessions Marten path do not share a transaction) is documented + frozen in the design doc §2.6. 13 tests (12 seam + 1 kill-all extension), **gate 13/13 green, 0 failed**; the F5 e2e is *recorded, not assumed done* (the [`STATUS.md`](../STATUS.md) prod-hardening owner-ops item). 5 `SL·D` deferrals (per-session `DeviceLabel` / `Kiosk` flag / admin "sign out all residents" / per-group-per-event session scoping / a session `Trust` / `Remember me` distinction). `Milestones.cs` / README Roadmap / `MilestonesTests.cs` **byte-identical** (a **named lane**, not a milestone — the ADR [0013](../adr/0013-group-posts-membership-lane.md) `GP` / ADR [0171](../adr/0171-least-privilege-db-role.md) `FDW` precedent); see design/sessions-per-session-revocation.md § Run result (sessions acceptance gate — 2026-11-01)
    │   │   ├── Posts/              # M3 ✓ — Post / PostReply / Report docs + PostService (feed/detail/create/reply) + component-organized feeds; RC ✓ (ADR [0025](../adr/0025-rich-content-markdown-and-content-images.md)) — `ImageIds` (R·7) + the serving route's owner-branch (R·4); see design/m3-posts-design.md § Run result (M3 acceptance gate — 2026-09-04)
    │   │   ├── Announcements/      # M3b ✓ — Announcement (public + community scope, flat two-way split) + AnnouncementService; RC ✓ (ADR [0025](../adr/0025-rich-content-markdown-and-content-images.md)) — `ImageIds` (R·7) + the serving route's owner-branch (R·4); the "platform announcements" lane
    │   │   ├── Moderation/         # M3b ✓ — ModerationService (file/assign/unlock/resolve) + the `Via = Report` read branch + the hide/remove lanes; see design/m3b-moderation.md § M3b — Closed (recorded) (2026-09-09)
    │   │   ├── Localization/       # ADR [0005](../adr/0005-multilingual-support.md) ✓ (ML) — LanguageCatalog, LocaleSettings (M1 seed) + TranslationResource (the UI-string content doc) + ITranslationProvider (read) / ILocalizationService (admin) + LanguageCompleteness; ADR [0015](../adr/0015-ui-view-localization-mechanics.md) ✓ (ML-UI) adds KnownTranslationKeys (the closed en registry, D2) + the GetTranslationsForAsync batch read on ILocalizationService; the static-page content lane moved to the Pages context (ADR [0039](../adr/0039-pages-hierarchy-audience-translations.md) retired `LocalizedPage`); see design/multilingual-design.md § Multilingual — Closed (recorded) (2026-09-12)
    │   │   ├── Media/              # ADR [0011](../adr/0011-media-and-file-storage.md) ✓ — MediaObject catalog doc + IMediaStore / IMediaFileStore (content-addressed volume bytes, HTTP-free) + MediaOptions; the profile-avatar reference lane; ADR [0025](../adr/0025-rich-content-markdown-and-content-images.md) ✓ (RC) — the content-image lane (same store, same catalog); ADR [0034](../adr/0034-file-attachments-posts-replies-announcements.md) ✓ (ATT) — the file-attachment (download) lane: a separate route + allowlist + `Content-Disposition: attachment` over the same store (one store, one volume, one catalog — C-ATT·1/3); see design/media-file-storage-design.md § Media — Closed (recorded) (2026-09-11) + design/file-attachments-design.md § File attachments — Closed (recorded) (2026-09-16)
    │   │   ├── Pages/              # ADR [0039](../adr/0039-pages-hierarchy-audience-translations.md)–0043 ✓ (PG) — Page + PageTranslation docs (a hierarchical, audience-restricted, translatable knowledge tree) + PageService (mount / read / edit / translate) + PageToAuditableResource (reuses the Audience doc + the frozen IAuthorizationService); ADR [0040](../adr/0040-pages-system-and-user-kinds-and-blog-feed.md) ✓ (PG) — PageKind (System vs blog) + the read-only /blog feeds; ADR [0041](../adr/0041-page-audience-scope-dropdown.md) ✓ — the Audience.AllResidents flag; ADR [0043](../adr/0043-system-pages-shipped-and-discoverable.md) ✓ (SP) — the four canonical system pages (/terms /help /privacy /conduct) as PageKind.System rows; absorbs + retires the LocalizedPage static-page lane; see design/pages-design.md
    │   │   ├── Tags/               # ADR [0044](../adr/0044-tags.md) ✓ (TG) — Tag + TagTranslation docs (the ADR [0011](../adr/0011-media-and-file-storage.md) shared-id-doc shape, `Slug` the language-neutral business key) + TagService (attach / translate / list / by-tag / suggest); referenced by the additive Post.TagIds / Page.TagIds fields (ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md) §B.1, zero migrations); a tag is a label, never a gate — the one access-scoped read seam reuses the content's own Read decision (C-TG·1/2/3); see design/tags-design.md
    │   │   ├── Migrations/         # standard EF Core migrations for the `identity` schema only (ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md)); not the domain `mt` schema
    │   │   ├── Events/             # M4 ✓ (ADR [0054](../adr/0054-events-rsvp-reminders.md)) — Event + EventRsvp docs + EventService (feed/detail/compose + last-write-wins RSVP) + EventToAuditableResource (reuses the Audience doc + the frozen IAuthorizationService) + the EventReminders §6.4 job (EventReminderService/Handler/Tick over the M1 durable-email trio) + the author ∪ GlobalAdmin standing matrix enforced **server-side** in the EventService (the edit/delete write lanes carry the principal's actorRoles, the AnnouncementService/PageService precedent — the GlobalAdmin override is exercised in the service, not deferred to the Web boundary; the audit row tags the branch: Owner/Admin); gate 2026-09-21: Core 668/668 + Web 351/351, EventControllerTests 19/19, the 23 M4 seam tests green together; re-verified after U13's GlobalAdmin-override seam fix: Core 670/670 + Web 351/351, the 25 M4 seam tests (T01–T25) green together; see design/m4-events-design.md § Run result (M4 acceptance gate — 2026-09-21); M14 ✓ (ADR [0115](../adr/0115-integration-of-events-and-projects.md)) — the event side of the Events↔Projects interlock: the event-detail "linked to-dos" section (the frozen GetAsync resolve for the chip + the U01 ListTodosForEventAsync reverse read seam, access-scoped + dangling-safe — C-M14·2) + the LinkedTodoRow row shape (drift entry 5) + the events.linked_todos kw-l key; no RRULE / recurrence (C-M14·6); see docs/design/m14-events-projects-design.md; M18 ✓ (ADR [0119](../adr/0119-recurring-events.md)) — recurring events: two additive `Event` fields (`RecurrenceHeadId?` / `RecurrenceRule?`, zero schema change), the pure `EventRecurrenceExpander`, materialized occurrences at create / head-edit time, the author-only `SkipOccurrenceAsync` / `UndeleteOccurrenceAsync` seams, the 14 `events.*` `kw-l` keys; read seams / RSVP / reminders / ICS unchanged (the per-occurrence `VEVENT` shape is what M18 implements); see docs/design/m18-recurring-events-design.md
    │   │   ├── Projects/           # M5 ✓ (ADR [0067](../adr/0067-m5-projects-todos-and-kanban.md)) — TodoItem + KanbanBoard + KanbanLane + BoardItemPlacement docs + ProjectService (read / write / placement lanes) + TodoItemToAuditableResource (TargetKind "todo") + KanbanBoardToAuditableResource (TargetKind "board"); standing creator ∪ assignee ∪ GlobalAdmin (C-M5·6); see design/m5-projects-design.md; PL ✓ (ADR [0086](../adr/0086-goals-projects-lane.md)) — ProjectGoal + Project docs (additive on ProjectDocTypes, zero migration) + ProjectGoalToAuditableResource (TargetKind "goal") + ProjectToAuditableResource (TargetKind "project") + the IProjectService goal / project / association / delete lanes (standing creator ∪ GlobalAdmin, the ADR [0070](../adr/0070-board-edit-lane-and-board-polish.md) matrix) + the additive ProjectId? feed-filter field on TodoItem + KanbanBoard (never a gate); see design/pl-goals-projects-design.md; M14 ✓ (ADR [0115](../adr/0115-integration-of-events-and-projects.md)) — the Projects side of the Events↔Projects interlock: the additive EventId? association on TodoItem (a feed filter / association, **never a gate** — C-M14·1) + the IProjectService.ListTodosForEventAsync reverse read seam (the ListTodosAsync shape filtered to one event, the ListBoardsForTodoAsync precedent) + the IProjectService.SetTodoEventAsync set-event write lane (standing creator ∪ assignee ∪ GlobalAdmin, C-M5·6; a non-visible eventId refused 404, null clears; one atomic todo.set_event audit row) + the BCL-only pure TodoIcsWriter VTODO emitter (no store, no clock, no library; a closed VTODO subset; UID kw-todo-{Id}@kumunita; no ORGANIZER / CREATED-BY / ATTENDEE / RRULE / X-… — the ADR [0028](../adr/0028-guardian-controls-account-scope-supervision.md) posture, C-M14·5) + the two GET /projects/todos.ics / GET /projects/todos/{id}.ics routes (text/calendar, no-store; no subscription token — rides the cookie); **zero new authorization surface** (C-M14·4 — the frozen AccessAction set unchanged; Read + C-M5·6 reused); no RRULE / recurrence (C-M14·6); see docs/design/m14-events-projects-design.md
    │   │   ├── Notifications/      # M6 ✓ (ADR [0076](../adr/0076-notifications-inbox-and-recipient-email.md)) — Notification + NotificationPreference docs (the NotificationDocTypes surface) + NotificationService (the EmitAsync writer + the inbox / preference lanes, reusing the M1 durable-email trio, no IAuthorizationService — a personal read, not an AccessAction decision); see design/m6-notifications-design.md; M20 ✓ (ADR [0121](../adr/0121-notification-quiet-times.md)) — notification quiet times: the `NotificationQuietSchedule` doc (the per-resident blocked/allowed hours-of-day + days-of-week schedule, `RecipientId` identity, registered on `NotificationDocTypes`) + the pure `QuietScheduleEvaluator.IsQuietNow` (the recipient's own effective zone, ADR [0019](../adr/0019-timezone-platform-default-and-resident-override.md)) + the two owner-scope seams (`GetQuietScheduleAsync` / `SetQuietScheduleAsync`, no audit row — the ADR [0019](../adr/0019-timezone-platform-default-and-resident-override.md) personal-preference shape); the additive `Notification.EmailDeferred` field + the `EmitAsync` quiet gate (the email nudge is deferred, **never the inbox row** — ADR [0076](../adr/0076-notifications-inbox-and-recipient-email.md) D7) + the distinct deferred idempotency key `notification:{kind}:{source-id}:deferred`; the [CQRS-lite §6.4](cqrs-lite-wolverine.md) `NotificationFlushService` (the Wolverine-free durable flush, the `EventReminderService` shape) + the `LocaleSettings.QuietCheckMinutes` additive field (admin-set cadence, the ADR [0050](../adr/0050-admin-managed-signup-gate-open-or-invitation-only.md)/0105 shape) + the audited `SetQuietCheckMinutesAsync` write lane (C-M20·6); zero new authorization surface (C-M20·5); see design/m20-notification-quiet-times-design.md
    │   │   ├── Search/             # M8 ✓ (ADR [0091](../adr/0091-search.md)) — no docs, no DocTypes surface (zero schema change, ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md) §B untouched) + ISearchService (SearchAsync `all` read + SearchSurfaceAsync paged single-surface read, the ADR [0090](../adr/0090-pagination-and-filtering.md) D1/D3 HasMore shape) + SearchService (store-composing; composes only the frozen IDocumentStore / IAuthorizationService / IUserInfoService; per-surface candidate predicates copied from the canonical reads, the case-insensitive substring match over Title + Body, the truncated context window); one aggregate audit row per (query, surface, scope) decision — TargetKind "search:<surface>" (search:posts / search:events / search:pages / search:announcements), TargetId null, zero-candidate visits emit no row; see design/m8-search-design.md
    │   │   ├── Messaging/          # M9 ✓ (ADR [0105](../adr/0105-messaging.md)) — Conversation + Message docs (the MessagingDocTypes surface; a unique (ParticipantA, ParticipantB) pair index + a (ConversationId, Created) thread index) + IMessagingService / MessagingService (the toggle seams IsMessagingEnabledAsync / SetMessagingEnabledAsync over the LocaleSettings.MessagingEnabled false-floor + open / send / list / thread / mark-read); participant-by-id access — no Audience, no IAuthorizationService call, zero new AccessAction / AccessVia / IAuditableResource adapter (a non-participant, GlobalAdmin included, gets a non-leaky 404); one in-transaction AccessAudit row per write (Action/TargetKind "message" on open + send, Via Owner; "messaging.toggle" on the admin flip, Via Admin), reads + mark-read emit no row; the D6 nudge reuses the M6 lane (EmitAsync, kind message.new, idempotency key notification:message.new:{messageId}); plain-text ≤ 2000 chars, immutable in M9 (rich content / edit / delete are the follow-on lanes' entry); see design/m9-messaging-design.md
    │   │   ├── GroupMessaging/     # GM ✓ (ADR [0278](../adr/0278-group-messaging-n1-lane.md) + the invitation-flow amendment) — the N:1 generalization of M9 (the group-messaging lane): GroupConversation + GroupMessage + GroupMessagingInvitation docs (the GroupMessagingDocTypes surface — a Participants membership index + a (ConversationId, Created) thread index + a unique (ConversationId, InvitedId) business-key index on the invitation row; **no** unique pair index on the conversation, a group is not canonical) + IGroupMessagingService / GroupMessagingService (the IsMessagingEnabledAsync read over the **same** LocaleSettings.MessagingEnabled false-floor as M9, one master gate — the M9 admin write is the sole writer; OpenGroupAsync (creator is the only initial participant) / InviteMembersAsync / AcceptInviteAsync / DeclineInviteAsync / GetMembersAsync / GetPendingInvitationsForUserAsync / SendAsync / ListGroupsAsync / GetConversationAsync / MarkReadAsync); the **invitation flow** (the creator is the only initial member; everyone else — picked at open time or added later — gets a Pending GroupMessagingInvitation row + a group.message.invite nudge with Accept / Decline deep-links, the community-group GroupInvitation shape minus the Cancelled state; an invitee accepts → appended to Participants on the next read, or declines → never a member); the any-participant-can-invite rule; a per-participant read marker (GroupMessage.ReadBy is a sorted **set** of reader ids, not a single id); participant-by-id access — no Audience, no IAuthorizationService call, zero new AccessAction / AccessVia / IAuditableResource adapter (a non-participant, GlobalAdmin included, gets the same non-leaky 404 as M9); one in-transaction AccessAudit row per write (Action/TargetKind "group.message" on open + send, Via Owner; "group.message.invite" / "group.message.invite.accept" / "group.message.invite.decline" on the invitation-flow seams, Via Owner), reads + mark-read emit no row; the nudge reuses the M6 lane (EmitAsync, kind group.message.new / group.message.mention / group.message.invite, idempotency key notification:group.message.new:{messageId}); plain-text ≤ 2000 chars, immutable (the M9 D7 cap carried); zero re-shape of the M9 context (a parallel bounded context, the M9 ADR [0105](../adr/0105-messaging.md) pins byte-identical)
    │   │   ├── Usage/              # M13 ✓ (ADR [0114](../adr/0114-logging-and-usage-analytics.md)) — UsageEvent doc (the UsageDocTypes surface) + the pure UsageCapturePolicy.Decide (skip no-endpoint / static-file) + the pure SurfaceKey closed-list mapper (26 top-level segments + "other") + IUsageAnalyticsService / UsageAnalyticsService (the 7/30/90-day window aggregation — Total / AuthenticatedTotal / AnonymousTotal / DistinctActors / SurfaceRanking; touches only UsageEvent, zero new authorization surface) + UsagePurgeService (the 365-day platform-constant batched delete) + UsagePurgeTick (the 1-day self-rescheduling TimeoutMessage); see design/m13-logging-analytics-design.md
    │   │   └── Logging/            # M13 ✓ (ADR [0114](../adr/0114-logging-and-usage-analytics.md)) — the BCL-only file sink: FileSinkOptions POCO + LogLine (JSON-lines writer, BCL-only escaping) + RollingFileSink (BuildFileName daily rotation + Retain boot deletion) + FileLoggerProvider / FileLogger (an ILoggerProvider over a per-category TextWriter) + the AddFileSink extension (registered on builder.Logging, additive to the console sink; the sink never logs request bodies / cookies / secrets — C-M13·1/2)
    │   └── Kumunita.Web/           # ASP.NET Core MVC + Razor, server-rendered
    │       ├── Program.cs          # composition root; dev-only MT boot, boot-block in all envs; Wolverine host (UseWolverine, retry/dead-letter policy)
    │       ├── Milestones.cs       # home-page roadmap (kept in sync with README's "Roadmap" — AGENTS.md)
    │       ├── RepositoryInfo.cs   # build sha/branch for `/health`
    │       ├── appsettings*.json
    │       ├── Models/             # Razor view-models per controller (Audit, BreakGlass, Directory, Feed, Group, Post, Profile, Moderation, ...)
    │       ├── Security/           # ClaimsSource, KumunitaPrincipal/KumunitaClaimsPrincipalFactory, BlockedAccountMiddleware
    │       ├── SideEffects/        # M1 step 7: OutboxEmailHandler (durable send + Fault<OutboxEmail> dead-letter hook), AuditPurgeHandler/Tick
    │       ├── Controllers/        # Home, Account, Admin, AdminSetup, Announcement, Directory, Groups, Moderation, Posts, Profile, Health
    │       ├── Views/              # Razor views + Layout; per-feature folders (Account, Admin, AdminSetup, Announcement, Directory, Groups, Home, Moderation, Posts, Profile, Shared)
    │       ├── package.json / tsconfig.json   # tsc-only TS build (no bundler)
    │       ├── client/             # plain TS sources
    │       │   └── lib/            # api.ts (CSRF-aware fetch, see [front-end](../ARCHITECTURE.md)), toasts, flash, insert-image.ts (RC 0025 upload lane), rich-editor.ts (RE 0031 / IE 0032 / WY 0033 composer surface: the toolbar, the rendered pane, the `contenteditable` editing surface, the read-only code-view mirror), dom-to-markdown.ts (WY 0033 ✓ live — `toMarkdown` serializer + `sanitizeHtml` sanitizer, the inverse of `renderPreview`; gate 167/167 green, closed-loop + handoff manual gates recorded as not-run with the automated floor covering the same contract)
    │       └── wwwroot/            # js/ (tsc output, compiled — not source), css/, lib/ (bootstrap + jQuery validation)
    └── tests/
        ├── Kumunita.Core.Tests/    # XUnit; PostgresFixture = one shared postgres:18 per class,
        │                           #   fresh scratch DB per test (matches prod db image)
        └── Kumunita.Web.Tests/     # XUnit; controller + view-model + config-binding tests, plus
                                      # in-process Playwright specs
                                      # (e2e-m2.spec.ts, e2e-m3.spec.ts,
                                      # e2e-m4.spec.ts — the per-milestone
                                      # happy-path specs)

  e2e/                              # F19 (E2E) — the standalone Playwright harness
                                    # (its own package.json + playwright.config.ts
                                    # + global-setup.ts readiness gate + specs/
                                    #  signup_verify_login.e2e.ts +
                                    #  post_write.e2e.ts), driving the compose app
                                    # on :5080 + Mailpit on :8025; NOT in
                                    # Kumunita.slnx, NOT invoked by `dotnet test`,
                                    # NOT a dependency of the .NET build; the
                                    # F14 CI gate's `dotnet exec` shape is
                                    # untouched — the e2e job is a separate step
                                    # in `.github/workflows/ci.yml`.

Two projects. `Core` holds all business logic behind interfaces and never references
ASP.NET HTTP types — keeping it testable and leaving the door open for a future API/MCP
layer. `Web` is a thin HTTP/Razor/TS shell. `Projects/` (M5) is now live —
ADR [0067](../adr/0067-m5-projects-todos-and-kanban.md) ships the `TodoItem` / `KanbanBoard` / `KanbanLane` /
`BoardItemPlacement` docs + the `ProjectService` surface. `Notifications/` (M6)
is now live — ADR [0076](../adr/0076-notifications-inbox-and-recipient-email.md) ships the `Notification` + `NotificationPreference` docs
(the `NotificationDocTypes` surface) + the `NotificationService` (the `EmitAsync` writer +
the inbox / preference lanes, reusing the M1 durable-email trio, no
`IAuthorizationService` — a personal read, not an `AccessAction` decision);
M20 (ADR [0121](../adr/0121-notification-quiet-times.md)) adds notification quiet times on the same surface — the
`NotificationQuietSchedule` per-resident doc + the pure timezone-aware
`QuietScheduleEvaluator` (the recipient's own effective zone, ADR [0019](../adr/0019-timezone-platform-default-and-resident-override.md)), the
`EmitAsync` quiet gate that defers the **email nudge** (never the inbox row,
ADR [0076](../adr/0076-notifications-inbox-and-recipient-email.md) D7) + the distinct deferred idempotency key, the [CQRS-lite §6.4](cqrs-lite-wolverine.md)
`NotificationFlushService` durable flush (the `EventReminderService` shape) that
delivers held emails when quiet lifts (exactly once, idempotent), and the
`LocaleSettings.QuietCheckMinutes` admin-set cadence (the audited
`SetQuietCheckMinutesAsync` write lane) — zero new authorization surface
(C-M20·5).
(`Events/`, M4, is live — ADR [0054](../adr/0054-events-rsvp-reminders.md).) `Search/` (M8) is now live —
ADR [0091](../adr/0091-search.md) ships the `ISearchService` seam (`SearchAsync` + `SearchSurfaceAsync`)
+ the store-composing `SearchService` over the **frozen** `IDocumentStore` /
`IAuthorizationService` / `IUserInfoService` (zero new authorization surface,
zero schema change; one aggregate audit row per (query, surface, scope)
decision, `TargetKind = "search:<surface>"`, zero-candidate visits emit no
row; deferred lanes: language-scoped search — ADR [0018](../adr/0018-ugc-authored-in-language-tag.md)'s consequence — and
`tsvector` full-text). `Messaging/` (M9) is now live —
ADR [0105](../adr/0105-messaging.md) ships the `Conversation` + `Message` docs (the `MessagingDocTypes` surface)
+ the `IMessagingService` seam (`IsMessagingEnabledAsync` /
`SetMessagingEnabledAsync` + `OpenConversationAsync` / `SendAsync` /
`ListConversationsAsync` / `GetConversationAsync` / `MarkReadAsync`) on the
**participant-by-id** access story — zero new authorization surface (no
`Audience`, no `IAuthorizationService` call, no new `AccessAction` /
`AccessVia` / `IAuditableResource` adapter; a non-participant — GlobalAdmin
included — gets a non-leaky 404) — and the write audit rows
`TargetKind = "message"` (open / send, `Via = Owner`) and
`TargetKind = "messaging.toggle"` (the admin flip, `Via = Admin`); reads and
mark-read emit no row; the new-message nudge rides the M6 `Notification` lane
(kind `message.new`). `GroupMessaging/` (GM) is now live —
ADR [0278](../adr/0278-group-messaging-n1-lane.md) ships the `GroupConversation` + `GroupMessage` + `GroupMessagingInvitation` docs (the
`GroupMessagingDocTypes` surface) + the `IGroupMessagingService` seam
(`IsMessagingEnabledAsync` over the **same** `LocaleSettings.MessagingEnabled`
false-floor as M9 — one master gate, the M9 admin write is the sole writer —
+ `OpenGroupAsync` / `InviteMembersAsync` / `AcceptInviteAsync` /
`DeclineInviteAsync` / `GetMembersAsync` /
`GetPendingInvitationsForUserAsync` / `SendAsync` /
`ListGroupsAsync` / `GetConversationAsync` / `MarkReadAsync`) on the
**participant-by-id** access story (the M9 C-M9·5 shape generalized to the
N:1 participant set — zero new authorization surface, no `Audience`, no
`IAuthorizationService` call, no new `AccessAction` / `AccessVia` /
`IAuditableResource` adapter; a non-participant — GlobalAdmin included — gets
the same non-leaky 404 as M9) — and the write audit rows
`TargetKind = "group.message"` (open / send, `Via = Owner`) and
`TargetKind = "group.message"` (`Action = "group.message.invite"` /
`"group.message.invite.accept"` / `"group.message.invite.decline"`, the
invitation-flow seams, `Via = Owner`); reads and mark-read emit no row; the
new-message nudge rides the M6 `Notification` lane (kind `group.message.new` /
`group.message.mention` / `group.message.invite` — the three kinds join the
closed `NotificationKinds.Known` set, the 28 → 31 re-baseline). The
**invitation flow** (the ADR [0278](../adr/0278-group-messaging-n1-lane.md) amendment): the creator is the only
initial participant; everyone else — picked at open time or added later —
gets a `GroupMessagingInvitation` Pending row (business key
`(ConversationId, InvitedId)`, the community-group `GroupInvitation` shape
minus the `Cancelled` state) + a `group.message.invite` nudge with Accept /
Decline deep-links; an invitee accepts (becomes a participant on the next
read, the invariant C4 carry) or declines (never becomes a member); the
thread's `GetMembersAsync` roster lists the accepted participants + the
pending invitees (marked "(pending)"). The lane is a
**parallel bounded context** — the M9 `Kumunita.Core.Messaging` context, the
`MessagingService`, the `MessagesController`, and the M9 ADR [0105](../adr/0105-messaging.md) pins are
byte-identical (the N:1 is not a re-shape of the 1:1). `Usage/` + `Logging/` (M13) are now live —
ADR [0114](../adr/0114-logging-and-usage-analytics.md) ships the BCL-only file sink (the `Logging/` surface —
`FileLoggerProvider` / `FileLogger` / the `LogLine` JSON-lines writer / the
`RollingFileSink` daily rotation + boot retention, additive to the console
sink, registered on `builder.Logging`; the sink never logs request bodies,
cookies, or secrets) and the `Usage/` bounded context — the `UsageEvent` doc
(the `UsageDocTypes` surface) + the pure `UsageCapturePolicy` (skip no-endpoint
/ static-file) + the pure `SurfaceKey` closed-list mapper + the
`IUsageAnalyticsService` seam (7/30/90-day windows over `Total` /
`AuthenticatedTotal` / `AnonymousTotal` / `DistinctActors` /
`SurfaceRanking`) — with the thin Web `UsageCaptureMiddleware` (a capture
failure never fails the request) and the `/admin/analytics` GlobalAdmin surface
(the surface-rank table + the CSV export with one `AccessAudit` row);
**zero new authorization surface** (C-M13·6), **zero per-account rendered data**
(C-M13·3), **zero third-party telemetry** (C-M13·1). M7 (ADR [0090](../adr/0090-pagination-and-filtering.md)) shipped the shared pagination idiom
— the `PagedViewModel` record + the `_Pager` partial (`Views/Shared/_Pager.cshtml`) +
the `HasMore` signal on every paged Core seam. A new list surface that needs paging adds
the `HasMore` signal to its seam (the D1/D3 shape) and drops in the `_Pager` partial (the
D5 shape); it does not re-invent a pager.

