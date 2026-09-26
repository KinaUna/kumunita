# Kumunita — Architecture

Kumunita is a self-hosted community platform. One deployment serves exactly one
neighborhood; each community runs its own instance with its own Postgres. There is no
multi-tenant data model. This is the technical reference. Rationale lives in `docs/adr/`;
the public summary lives in `README.md`; the security & privacy threat model (the top
priority of this product) lives in `docs/SECURITY.md`; and the *why* — the development
philosophy behind all of it — lives in `docs/philosophy/`.

## Relationship to the philosophy

This document describes **how** the system is built; [`docs/philosophy/`](philosophy/)
describes **why**. The philosophy's one claim — that the platform's value is the *linkage*
between a neighborhood's differentiated parts, not the count of parts — is the lens this
architecture is organized through. Two concrete mappings are worth keeping in view:

- **The value chain drives the module order.** The roadmap is an ascent up the value
  chain from [`docs/philosophy/the-platform-as-integrator.md`](philosophy/the-platform-as-integrator.md)
  (signal → shared awareness → understanding → decision → coordination → outcome):

  | Milestone | Value-chain step it adds |
  |---|---|
  | **M0** scaffold | the substrate — no value yet, just a place the rest can link into |
  | **M1** identity, groups, delegation, authorization | the **access model** — the linkage that turns *signals* into *shared awareness* for the right audience |
  | **M2** directory & profiles | **shared awareness** — every resident is listed; each opt-in contact block is audience-gated |
  | **M3** posts, components, moderation | **understanding → decision** — a signal reaches its audience; a report links to a moderator |
  | **M4** events, RSVP, reminders | **coordination** — a decision becomes an owned, scheduled, reminded action |
  | **M5** projects (goals, tasks, contributors) | **coordination → outcome** — many signals re-linked into one goal with owners |
  | **M6** notifications | **shared awareness** — the platform reaches out to the resident where they are |
  | **M7** pagination and filtering | **navigation** — a growing archive stays browsable |
  | **M8** search | **understanding** — finding what already exists in the neighborhood's memory |
  | **M9** PWA and responsive design | **portability of the surface** — the same platform in the resident's pocket |
  | **M10** portability (import/export) | **outcome + world seams** — the loop closes *into* the residents' lives |
  | **M11** iCal | **outcome + world seams** — events land in the calendars residents already check |
  | **M12** logging and analytics | **feedback** — the operator sees how the platform is used |
  | **M13** integration of Events and Projects | **coordination** — the two coordination surfaces interlock |

  (Named lanes — `GP` group posts, media (ADR 0011), `ML` multilingual (ADR 0005), and `ML-UI` live-UI multilingual (ADR 0015) — ship on their own design docs and value-chain steps, not as M-letter rows in this table; `ML` and `ML-UI` are *shipped* lanes, `GP` and media likewise.)

- **The seams are the architecture.** The "modular monolith" in §3 is the
  integration discipline applied: few stable module interfaces over one process,
  with the **access model** (§4) as the most load-bearing contract. The philosophy
  names the failure modes this layout prevents (god part, parts-work-seams-don't,
  signals-without-loops) — see [`docs/philosophy/anti-patterns.md`](philosophy/anti-patterns.md).

Neither document overrides the other: ADRs and `SECURITY.md` remain authoritative for
specifics; the philosophy remains authoritative for *how to judge a design*.

## 1. Technology stack

| Layer         | Choice                     | Notes                                   |
|---------------|----------------------------|-----------------------------------------|
| Runtime       | .NET 10 (LTS)              |                                         |
| Web           | ASP.NET Core MVC + Razor   | server-rendered, classic                |
| Front-end     | Plain TypeScript           | `tsc` only; no bundler / dev server     |
| Data          | Marten + EF (Identity)     | Postgres: domain docs in `mt`; Identity tables in `identity` |
| Messaging/jobs| Wolverine                  | in-process; CQRS-lite                   |
| Event sourcing| — (not used)               | documents + projections instead         |
| Auth (now)    | ASP.NET Core Identity      | cookie                                  |
| Auth (later)  | OpenIddict                 | OIDC, cross-neighborhood federation     |
| Database      | PostgreSQL                 | one per instance                        |
| Packaging     | Docker (multi-stage)       | Coolify on a VPS                        |
| Email (dev)   | Mailpit                    | local SMTP + web inbox                  |

Rationale: ADR 0001 (stack); ADR 0004 (persistence split & schema evolution).

## 2. Solution layout

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
    │   ├── adr/                    # 0001–0053 (the running decision ledger — append-only, highest number = newest)
    │   ├── design/                 # per-milestone design docs (M1: m1-identity-access.md)
    │   └── philosophy/             # development philosophy (START-HERE.md, templates/)
    ├── src/
    │   ├── Kumunita.Core/          # domain, services, Marten, the Wolverine-free side-effect business logic
    │   │   ├── CommunityOptions.cs # per-instance config (ADR 0002)
    │   │   ├── KumunitaFeature.cs  # first versioned `mt` storage feature (ADR 0004 §B)
    │   │   ├── M1DocTypes.cs       # M1 Marten-native doc registration (ADR 0004 §B.1)
    │   │   ├── M3DocTypes.cs       # M3 + M3b Marten-native doc registration (Post, PostReply, Report, Announcement)
    │   │   ├── MediaDocTypes.cs    # M4-adjacent Marten-native doc registration (MediaObject) — ADR 0011
    │   │   ├── Bootstrap/          # SchemaBootstrap, FirstBootSeeder
    │   │   ├── Identity/           # IdentityModule (M1) + DbBootstrap (first-boot pristine gate); also the side-effect seam: ISmtpSender/SmtpSender, IMailerStage/OutboxEmailStager, EmailDeadLetterWriter; AppDbContext lives here (EF Core, `identity` schema, ADR 0004); GA ✓ (ADR 0038) — `IIdentityService.FindSubjectByEmailAsync` (the email → subject id resolution; G-A·2 no-leak shape: null on unknown email, no auto-create)
    │   │   ├── UserInfo/           # UserInfoModule (M1) + M2 directory/profile-editor/groups surface: DirectoryService (list/detail/preview), Profile, Group, DelegationGrant, Component, IUserInfoService; GU ✓ (ADR 0028) — GuardianLink (account-scope supervision: suspend / membership curation / invitation approval; **no content read**, G·1) + the `AccessVia.Guardian` standing (the 9th value) + the IUserInfoService guardian seams (formation / suspend / dissolve); see design/guardian-controls-design.md § GU — Closed (recorded) (2026-09-14); GA ✓ (ADR 0038) — the Detail view's "other guardians" list: the `GuardianItem` record + the `MembershipEditorModel.GuardianItems` field (the assigned guardian's display name; G-A·3 identical-in-kind pin — the standing is identical to the creator's, no content read); see design/guardian-assignment-design.md § GA — Closed (recorded) (2026-09-17)
    │   │   ├── Authorization/      # AuthorizationModule (M1) — audiences, policy, audit; AuditPurgeService (Wolverine-free tiering); AdminOverride (break-glass read path)
    │   │   ├── Posts/              # M3 ✓ — Post / PostReply / Report docs + PostService (feed/detail/create/reply) + component-organized feeds; RC ✓ (ADR 0025) — `ImageIds` (R·7) + the serving route's owner-branch (R·4); see design/m3-posts-design.md § Run result (M3 acceptance gate — 2026-09-04)
    │   │   ├── Announcements/      # M3b ✓ — Announcement (public + community scope, flat two-way split) + AnnouncementService; RC ✓ (ADR 0025) — `ImageIds` (R·7) + the serving route's owner-branch (R·4); the "platform announcements" lane
    │   │   ├── Moderation/         # M3b ✓ — ModerationService (file/assign/unlock/resolve) + the `Via = Report` read branch + the hide/remove lanes; see design/m3b-moderation.md § M3b — Closed (recorded) (2026-09-09)
    │   │   ├── Localization/       # ADR 0005 ✓ (ML) — LanguageCatalog, LocaleSettings (M1 seed) + TranslationResource (the UI-string content doc) + ITranslationProvider (read) / ILocalizationService (admin) + LanguageCompleteness; ADR 0015 ✓ (ML-UI) adds KnownTranslationKeys (the closed en registry, D2) + the GetTranslationsForAsync batch read on ILocalizationService; the static-page content lane moved to the Pages context (ADR 0039 retired `LocalizedPage`); see design/multilingual-design.md § Multilingual — Closed (recorded) (2026-09-12)
    │   │   ├── Media/              # ADR 0011 ✓ — MediaObject catalog doc + IMediaStore / IMediaFileStore (content-addressed volume bytes, HTTP-free) + MediaOptions; the profile-avatar reference lane; ADR 0025 ✓ (RC) — the content-image lane (same store, same catalog); ADR 0034 ✓ (ATT) — the file-attachment (download) lane: a separate route + allowlist + `Content-Disposition: attachment` over the same store (one store, one volume, one catalog — C-ATT·1/3); see design/media-file-storage-design.md § Media — Closed (recorded) (2026-09-11) + design/file-attachments-design.md § File attachments — Closed (recorded) (2026-09-16)
    │   │   ├── Pages/              # ADR 0039–0043 ✓ (PG) — Page + PageTranslation docs (a hierarchical, audience-restricted, translatable knowledge tree) + PageService (mount / read / edit / translate) + PageToAuditableResource (reuses the Audience doc + the frozen IAuthorizationService); ADR 0040 ✓ (PG) — PageKind (System vs blog) + the read-only /blog feeds; ADR 0041 ✓ — the Audience.AllResidents flag; ADR 0043 ✓ (SP) — the four canonical system pages (/terms /help /privacy /conduct) as PageKind.System rows; absorbs + retires the LocalizedPage static-page lane; see design/pages-design.md
    │   │   ├── Tags/               # ADR 0044 ✓ (TG) — Tag + TagTranslation docs (the ADR 0011 shared-id-doc shape, `Slug` the language-neutral business key) + TagService (attach / translate / list / by-tag / suggest); referenced by the additive Post.TagIds / Page.TagIds fields (ADR 0004 §B.1, zero migrations); a tag is a label, never a gate — the one access-scoped read seam reuses the content's own Read decision (C-TG·1/2/3); see design/tags-design.md
    │   │   ├── Migrations/         # standard EF Core migrations for the `identity` schema only (ADR 0004); not the domain `mt` schema
    │   │   ├── Events/             # M4 ✓ (ADR 0054) — Event + EventRsvp docs + EventService (feed/detail/compose + last-write-wins RSVP) + EventToAuditableResource (reuses the Audience doc + the frozen IAuthorizationService) + the EventReminders §6.4 job (EventReminderService/Handler/Tick over the M1 durable-email trio) + the author ∪ GlobalAdmin standing matrix enforced **server-side** in the EventService (the edit/delete write lanes carry the principal's actorRoles, the AnnouncementService/PageService precedent — the GlobalAdmin override is exercised in the service, not deferred to the Web boundary; the audit row tags the branch: Owner/Admin); gate 2026-09-21: Core 668/668 + Web 351/351, EventControllerTests 19/19, the 23 M4 seam tests green together; re-verified after U13's GlobalAdmin-override seam fix: Core 670/670 + Web 351/351, the 25 M4 seam tests (T01–T25) green together; see design/m4-events-design.md § Run result (M4 acceptance gate — 2026-09-21)
    │   │   ├── Projects/           # M5 ✓ (ADR 0067) — TodoItem + KanbanBoard + KanbanLane + BoardItemPlacement docs + ProjectService (read / write / placement lanes) + TodoItemToAuditableResource (TargetKind "todo") + KanbanBoardToAuditableResource (TargetKind "board"); standing creator ∪ assignee ∪ GlobalAdmin (C-M5·6); see design/m5-projects-design.md; PL ✓ (ADR 0086) — ProjectGoal + Project docs (additive on M5DocTypes, zero migration) + ProjectGoalToAuditableResource (TargetKind "goal") + ProjectToAuditableResource (TargetKind "project") + the IProjectService goal / project / association / delete lanes (standing creator ∪ GlobalAdmin, the ADR 0070 matrix) + the additive ProjectId? feed-filter field on TodoItem + KanbanBoard (never a gate); see design/pl-goals-projects-design.md
    │   │   └── Notifications/      # M6 ✓ (ADR 0076) — Notification + NotificationPreference docs (the M6DocTypes surface) + NotificationService (the EmitAsync writer + the inbox / preference lanes, reusing the M1 durable-email trio, no IAuthorizationService — a personal read, not an AccessAction decision); see design/m6-notifications-design.md
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
    │       │   └── lib/            # api.ts (CSRF-aware fetch, §7), toasts, flash, insert-image.ts (RC 0025 upload lane), rich-editor.ts (RE 0031 / IE 0032 / WY 0033 composer surface: the toolbar, the rendered pane, the `contenteditable` editing surface, the read-only code-view mirror), dom-to-markdown.ts (WY 0033 ✓ live — `toMarkdown` serializer + `sanitizeHtml` sanitizer, the inverse of `renderPreview`; gate 167/167 green, closed-loop + handoff manual gates recorded as not-run with the automated floor covering the same contract)
    │       └── wwwroot/            # js/ (tsc output, compiled — not source), css/, lib/ (bootstrap + jQuery validation)
    └── tests/
        ├── Kumunita.Core.Tests/    # XUnit; PostgresFixture = one shared postgres:18 per class,
        │                           #   fresh scratch DB per test (matches prod db image)
        └── Kumunita.Web.Tests/     # XUnit; controller + view-model + config-binding tests, plus
                                      # Playwright e2e (e2e-m2.spec.ts, e2e-m3.spec.ts, §7)

Two projects. `Core` holds all business logic behind interfaces and never references
ASP.NET HTTP types — keeping it testable and leaving the door open for a future API/MCP
layer. `Web` is a thin HTTP/Razor/TS shell. `Projects/` (M5) is now live —
ADR 0067 ships the `TodoItem` / `KanbanBoard` / `KanbanLane` /
`BoardItemPlacement` docs + the `ProjectService` surface. `Notifications/` (M6)
is now live — ADR 0076 ships the `Notification` + `NotificationPreference` docs
(the `M6DocTypes` surface) + the `NotificationService` (the `EmitAsync` writer +
the inbox / preference lanes, reusing the M1 durable-email trio, no
`IAuthorizationService` — a personal read, not an `AccessAction` decision).
(`Events/`, M4, is live — ADR 0054.) M7 (ADR 0090) shipped the shared pagination idiom
— the `PagedViewModel` record + the `_Pager` partial (`Views/Shared/_Pager.cshtml`) +
the `HasMore` signal on every paged Core seam. A new list surface that needs paging adds
the `HasMore` signal to its seam (the D1/D3 shape) and drops in the `_Pager` partial (the
D5 shape); it does not re-invent a pager.

## 3. Modular monolith & bounded contexts

In-process modules behind interfaces — not separate services today; the interfaces are
the seam for later extraction.

- **IdentityModule** — authentication; issues the thin principal.
- **UserInfoModule** — who people are: profiles, groups, delegation.
- **AuthorizationModule** — what they may do: audiences, policy, audit.
- **LocalizationModule** — language catalog, default language, and translated UI
  strings (ADR 0005); consumed by the presentation layer, never by feature
  authorization. (Static pages live in the `Pages` context now, ADR 0039.)
- **Feature modules** — Directory, Posts, Pages, Moderation, Media, Tags, Events (M4 ✓ — ADR 0054), Projects (M5 ✓ — ADR 0067), Notifications (M6 ✓ — ADR 0076).
  Directory and Posts are both *consumers* of the single bulk visibility
  capability (`CanSeeAsync`, §4.2) — list authorization is one platform
  primitive, not per-feature logic. Media (ADR 0011) is a byte-store module:
  content-addressed payloads on a dedicated volume behind the HTTP-free
  `IMediaStore` seam, cataloged in `mt`, served only through an audited app
  endpoint (the profile avatar is the reference lane). The same store now
  carries **two** lanes over **one** volume + **one** catalog (C-ATT·1/3):
  the **content-image** lane (ADR 0025, `GET /content-image/{id}`, inline
  `<img>`) and the **file-attachment** download lane (ADR 0034,
  `GET /attachment/{id}`, `Content-Disposition: attachment` + `nosniff`). The
  attachment lane is a **separate** lane — a separate route, a separate
  allowlist (`MediaOptions.AttachmentAllowedContentTypes`, distinct from the
  image `AllowedContentTypes`), and download semantics — over the **same**
  `IMediaStore` / `MediaObject` / `MaxBytes`. The owning doc carries an
  additive `AttachmentIds` POCO field on `Post` / `PostReply` / `Announcement`
  (ADR 0004 §B.1, **zero migrations**), **separate** from `ImageIds`; the
  ids are derived **server-side** from the body (Core never parses a body,
  C-ATT·4). The serve route reuses the owning resource's single
  `CanAsync(…Read…)` decision — a **reply resolves its parent post's** decision
  (C-ATT·8; a document-session load, **no new `PostService`** seam), a post
  its own, an announcement its flat scope gate (zero `AccessAudit` rows).
  Every miss is a 404; one `Deny` row on a UGC deny, zero elsewhere (C-ATT·7/10).

Dependency rule: feature modules depend on the three identity/access modules (and Marten),
never the reverse. AuthorizationModule may call UserInfoModule to resolve groups; it never
calls feature modules.

The M1 design for these modules is in [`docs/design/m1-identity-access.md`](design/m1-identity-access.md);
the interface set, the thin-principal shape, and change management are
frozen by [ADR 0006](adr/0006-module-boundary-contracts.md) — §4.2 is a
draft sketch; the ADR is authoritative for what changes and through what.

## 4. Identity & access

### 4.1 Thin token, fat authorization

Authentication issues a small principal and nothing else:

    principal = { subjectId, isVerifiedResident, roles: [ "moderator:maintenance" ] }

"Can this person see that post?" is never a claim — it is resolved per request by the
AuthorizationModule. This keeps the cookie small and the identity story trivial, and lets
authorization rules grow without touching the token. It also keeps the later OpenIddict
swap mechanical (the cookie simply becomes an OIDC `sub`).

### 4.2 Services

    // UserInfoModule
    interface IUserInfoService {
      Task<Profile> GetProfileAsync(string subjectId);
      // Reads GroupMembership documents directly — strong consistency. Membership
      // changes take effect on the very next request, with no projection lag.
      Task<HashSet<string>> GetGroupIdsAsync(string userId);   // loaded once per request
      Task<DelegationGrant?> GetActiveGrantAsync(string delegateId);
      // Returns the delegate's active grant (if any): { ownerId, scope, from, to? }.
      // Noun note: "delegate" is the actor; "owner" is the principal they act as.
      // Create / Read / membership-management lanes, plus the group
      // description and privacy seams (ADR 0009 / ADR 0010):
      // SetDescription, SetPrivacy, GetPublicGroups.
    }

    // AuthorizationModule
    interface IAuthorizationService {
      // Single-target — detail views ("may I read this post?")
      Task<Decision> CanAsync(string actorId, Action action, IAuditableResource target);
      // Decision = { Allowed, Via, EffectivePrincipalId }
      // Bulk — list views (feeds, directory, boards): one group-load, one matching
      // pass over all candidates, one aggregate audit row (§5)
      Task<VisibleSet> CanSeeAsync(string actorId, Action action,
                                   IEnumerable<IAuditableResource> candidates);
      // VisibleSet = { visible: [ { id, via: Owner|Audience|Delegation } ], hiddenCount }
      // Group lane (ADR 0013, group posts milestone) — membership is the SOLE
      // decision: CanSeeGroupAsync(actorId, groupId, targetPostId?) for
      // detail/create-gate (one decision audit row) and
      // CanSeeGroupFeedAsync(actorId, groupId, candidateCount) for the feed
      // (one aggregate row). Via = Group, or Delegation when an in-scope
      // `read` grant acts with the owner's membership. There is NO moderator
      // and NO break-glass branch (G·4 — "nobody peeks", standing not
      // available), and the audience lane is never evaluated.
    }

### 4.3 Access model

- **Audience** = grants to users and/or groups; combine **Any** (union, default) | **All** (intersection).
- **Groups** = the reuse unit (ADR 0010: **private** groups are a membership/organizing unit, hidden from the audience pickers).
- **Delegation** = scoped acting; resolves an effective principal.
- **Moderator access** to audience-restricted content = **off by default**; a report grants
  the assigned moderator audited access to that item; an admin can enable standing
  visibility per scope.
- **Audit** of access decisions = always on.
- **Candidate filters are not authorization** (§4.4). A rule about *who is in the
  candidate set* (e.g. a post feed only includes published posts; a list only includes
  non-archived items) is a product query, applied before `CanSeeAsync`, and is never
  audited as an access decision.
- **Show-everyone directory.** The platform is invitation-only and limited to one
  neighborhood's residents: the directory lists every non-blocked resident to every
  signed-in viewer (no verified/unverified filter, no `Profile.Visibility` gate), and
  is a pure catalog read — no `CanSeeAsync`, no `AccessAudit` row. Only the detail
  (and preview) surfaces run the single `ContactVisibility` audience decision.

### 4.4 Decision algorithm

Shared by both entry points below is the group-matching core, written once so the two
paths cannot drift:

    MatchGroups(target, mode, groups, effPrin) =
        if target.audience is empty: return False             // deny-by-default
        match   = g => (g.kind == User  && g.id == effPrin)
                    || (g.kind == Group && groups.Contains(g.id))
        return (mode == Any) ? target.audience.grants.Any(match)
                             : target.audience.grants.All(match)

**Single target — `CanAsync`** (detail views):

    groups   = userInfo.GetGroupIdsAsync(actor)   // source docs (strong consistency),
                                                  // once per request, cached in-request
    grant    = activeGrant(actor)
    effPrin  = grant ? grant.ownerId : actor                     // delegation

    if target.authorId in { actor, effPrin }:                   // owner branch
        if actor == effPrin                                      -> Allow (Via: Owner)
        if action in grant.scope                                 -> Allow (Via: Delegation)
        // out-of-scope action as a delegate: fall through to Deny (audited)
    if action is moderation:
        if scope.moderatorAccess == On and actor moderates scope   -> Allow (Via: Moderator)
        if actor holds an active Report grant on target            -> Allow (Via: Report)
    if actor has a consumed, unexpired AdminOverride:             // break-glass (§4.5)
        -> Allow (Via: BreakGlass)
    allowed = MatchGroups(target, target.audience.mode, groups, effPrin)
              ? Allow (Via: Audience) : Deny

    append AccessAudit { actor, effPrin, action, target, via, outcome }   // always

**Bulk — `CanSeeAsync`** (feeds, directory, boards):

    same group-load + grant resolution, once
    for each candidate: owner branch, then moderation branches, then MatchGroups
    -> VisibleSet { visible: [ { id, via } ], hiddenCount }

    // audit (§5): ONE aggregate row for the view, PLUS one row per visible
    // audience-restricted item; per-item denials are NOT logged

Both paths (single and bulk) share these invariants, enforced inside `MatchGroups` so
they cannot drift:
- **Empty audience denies.** In mode `All`, `grants.All(...)` over an empty grant list is
  vacuously true — without the empty check above, an `All`-mode resource with no grants
  would be readable by *everyone*. An empty audience always denies.
- **Delegation is action-scoped.** A delegated actor gets the owner's standing only for
  actions in the grant's `scope`; an out-of-scope action is a Deny (audited), even though
  the effective principal is the owner. `Via: Delegation` records the acting identity in
  the audit row.

Every decision on audience-restricted content is audited, Allow or Deny.

### 4.5 Roles

- **GlobalAdmin** — full control; manages moderators + their component scope; sets
  scope-level `moderatorAccess`; reads the audit log.
- **Moderator** — scoped to one or more functional components.
- **Member** — verified resident.

**GlobalAdmin trust management** (mitigates the single-admin concentration, ADR 0003):

- **Two admins as standing practice.** Promoting a second GlobalAdmin is normal, not an
  exception — each can demote the other, so no single account is a hard point of trust.
- **Break-glass elevation.** When the admin(s) are gone, locked out, or hostile, the
  *host operator* can grant a **time-limited, single-use, audited** GlobalAdmin
  elevation to an existing account via a direct DB write (`AdminOverride`, §5) —
  consumed in-app at `/admin/break-glass`. **No in-app endpoint can create an
  `AdminOverride`**, so a hostile admin cannot grant or extend one for themselves.
  The elevation lapses at `expiresAt` (checked inline at authorization — no job).
  Runbook: OPS.md §9.

## 5. Data model (Marten documents)

One Postgres per instance, two schemas (ADR 0004):
  - `mt`       — all domain documents below + Marten projections; schema via Marten versioned migrations
     - `identity` — stock ASP.NET Core Identity tables (`AspNet*`); schema via EF Core migrations
  Neither ORM touches the other schema; a single `pg_dump` captures both.
  Media (ADR 0011) is the one stored payload outside Postgres: the bytes are
  content-addressed on a dedicated volume (`Media__RootPath`), the catalog is a
  document on `mt` — a **second restore surface** that must be snapshotted
  alongside the DB dump (OPS.md §4/§5).

Identity (EF Core, `identity` schema — framework-managed, not hand-rolled)
  AspNetUsers, AspNetRoles, AspNetUserRoles, ...  (+ `ExternalId` reserved for future OIDC `sub`)

Authorization
  ModeratorAssignment { id, userId, componentId, grantedBy, at }
  // Break-glass (§4.5). Written ONLY by the host operator directly into Postgres
  // (psql, OPS.md §9) — never by any in-app endpoint. The target account consumes the
  // token once to become GlobalAdmin until expiresAt; elevation + consumption are
  // audited (via: BreakGlass).
  AdminOverride { id, userId, token, grantedAt, expiresAt, consumedAt? }

UserInfoModule
  // The directory lists every non-blocked resident's basic info (displayName/verified) to every
  // signed-in viewer — the show-everyone rule: `visibility` no longer hides a profile, and the
  // list runs no audience decision. `contactVisibility` is the *single* audience gate (the
  // detail/preview surface's opt-in contact block, email/phone) for *others*: `null` ⇒ not
  // opted in ⇒ no decision / no audit row; non-null ⇒ one `CanAsync` decision + one AccessAudit
  // row. A self-view (viewer == owner) always renders the owner's own contact block — a
  // short-circuit with no decision and no audit row (the audience controls others, not the owner).
  // `visibility` stays on the document + editor (author-controlled, ADR 0003) as the audience
  // for detailed non-contact fields once they exist — it takes no effect at the directory layer.
  Profile          { subjectId, externalId?, householdId?, displayName, verified, blocked,
                     visibility: Audience, contactVisibility?: Audience, email, phone? }
  Group            { id, name, description, ownerId, created }
  GroupMembership  { groupId, userId, addedBy, at }
  // "owner" = the account whose standing is borrowed; "delegate" = the account acting.
  // (Deliberate vocabulary: in ASP.NET a "principal" is the *actor*, so the grant
  // fields avoid that word — see §4.2.)
  DelegationGrant  { id, ownerId, delegateId, scope: [action], from, to?, revokedBy? }
  GuardianLink     { id, guardianId, childId, status: Active|Dissolved, createdAt, dissolvedAt?, dissolvedBy? }   // GU (ADR 0028): the account-scope supervision link (standing off an Active row, G·2); dissolve one-way (G·5)

  // `householdId` is display/metadata ONLY (future group-helper: "family from
  // household", §10). The authorization path never reads it — household-based
  // visibility is expressed as a household *Group* the owner grants, per ADR 0001-B.

Content
  Component        { id, name, description, icon, sortOrder, enabled, moderatorAccess }
  Post             { id, kind: Announcement|Discussion, componentId?, authorId, title, body,
                     audience, pinned, hidden, created, updated }
  // Group lane (ADR 0013, group posts milestone): a group post is a Post with
  // `groupId` non-empty — membership is the sole access decision, `audience`
  // is written non-null and empty (never evaluated), `componentId` empty (the
  // post is excluded from component + "all sections" feeds); PostReply is
  // unchanged (lane-neutral) — a reply inherits the parent's single
  // group-lane decision.
  PostReply        { id, postId, authorId, body, created }
  Audience         { mode: Any|All, grants: [ { kind: User|Group, id } ] }   (embedded)

Events (M4 ✓ — ADR 0054; the names below are the canonical field set the
shipped `Kumunita.Core.Events.Event` doc carries — design/m4-events-design.md §3.1)
  Event            { id, title, body, componentId?, authorId, start, end, location?,
                     capacity?, audience, reminderEnabled, isDraft, isDeleted,
                     languageCode, tagIds, imageIds, attachmentIds,
                     created, modified? }
  EventRsvp        { id, eventId, userId, status: Going|Maybe|No, at }   // (eventId, userId) unique — last-write-wins (ADR 0054 §3.2)

  // Events calendar (EV-CAL ✓ — ADR 0063): a second, display-only view over the
  // same data — `GET /events/calendar` renders a month-anchored, rolling 30-day
  // window (overlap pairs highlighted client-side, prev/next/today navigation)
  // served by the one additive read seam `IEventService.ListInRangeAsync`
  // (the candidate filter + `CanSeeAsync(Read)` gate + one aggregate
  // `AccessAudit` row mirroring `ListUpcomingAsync`). Display-only, zero
  // document changes — no new doc, schema, or seed; the M4 surface untouched.

  // Events calendar views (EV-DWM ✓ — ADR 0064): the same `GET /events/calendar`
  // surface turned into the three named views residents expect — Day,
  // Week (Monday-start, time-ruler), and Month (true calendar month) — over the
  // same already-authorized event set, the same `ListInRangeAsync` seam, the
  // same `CanSeeAsync(Read)` gate, and the same chip + client-side-overlap
  // model. One additive `?view=` selector (day|week|month) on the existing
  // route + a view-appropriate anchor window computed in the controller
  // (1 day / the anchor's Monday-start week / the anchor's calendar month);
  // Day + Week are time-ruler grids (hour rows + time-positioned blocks via a
  // new plain-TS module `client/lib/events-calendar-time.ts`, tsc-only, zero
  // dependencies) and Month reuses the existing `events-calendar.ts` untouched.
  // Zero Core change — the `ListInRangeAsync` seam is reused unchanged (it is
  // already window-agnostic); no new doc, schema, seed, seam, or dependency.

Projects (M5 ✓ — ADR 0067; the names below are the canonical field set the
shipped `Kumunita.Core.Projects.TodoItem` doc carries — design/m5-projects-design.md §2.2)
  TodoItem           { id, title, body?, componentId?, authorId, assigneeId?, status?, parentId?,
                     audience, isDeleted, languageCode, tagIds, imageIds, attachmentIds,
                     created, modified? }
  KanbanBoard        { id, title, description?, componentId?, authorId, audience, isDeleted,
                     languageCode, created, modified? }
  KanbanLane         { id, boardId, title, status?, maxItems?, order, created, modified? }        // (boardId, order) unique
  BoardItemPlacement { id, todoItemId, boardId, laneId, order, created, modified? }               // (boardId, laneId, order) + (todoItemId, boardId) unique

  // A to-do is standalone (C-M5·2 — no BoardId / LaneId / Order on TodoItem); its
  // position on a board is a BoardItemPlacement row (zero or many per to-do).
  // Standing is creator ∪ assignee ∪ GlobalAdmin (C-M5·6), re-checked server-side
  // in ProjectService; read is each doc's own Audience (C-M5·3); the two adapters
  // are TodoItemToAuditableResource (TargetKind "todo") + KanbanBoardToAuditableResource
  // (TargetKind "board"). A BoardItemPlacement is not itself an auditable resource.

  // PL ✓ (ADR 0086; design/pl-goals-projects-design.md) — the higher-level goals +
  // projects page on top of M5, additive on this same context + M5DocTypes
  // (zero migration): a goal is the organizing container; a project points at
  // its goal (GoalId?) and is what a to-do / board associates to (ProjectId?).
  ProjectGoal        { id, title, description?, componentId?, authorId, audience, isDeleted,
                     languageCode, created, modified? }                                          // no dates, no IsDraft, no ProjectId (D2)
  Project            { id, title, description?, goalId?, status?, startAt?, dueAt?, componentId?,
                     authorId, audience, isDeleted, languageCode, created, modified? }           // status is a string, not an enum (C-PL·4); dates optional (ADR 0079)
  // TodoItem + KanbanBoard each gain one additive string? ProjectId (D4) — a feed
  // filter, never a gate (C-M3·2); their own Audience stays the access boundary.
  // Standing over a goal / project is creator ∪ GlobalAdmin (ADR 0070, no assignee
  // branch), re-checked server-side in ProjectService; read is each doc's own
  // Audience through the two new adapters ProjectGoalToAuditableResource
  // (TargetKind "goal") + ProjectToAuditableResource (TargetKind "project").
  // The IProjectService surface gains additively: the goal read / write lanes
  // (ListGoalsAsync / GetGoalAsync / CreateGoalAsync / UpdateGoalAsync) + the
  // project read / write lanes (ListProjectsAsync / GetProjectAsync /
  // ListProjectsForGoalAsync / CreateProjectAsync / UpdateProjectAsync) + the
  // association lanes (SetTodoProjectAsync / SetBoardProjectAsync + the additive
  // projectId filter param on ListTodosAsync / ListBoardsAsync) + the delete
  // lanes (DeleteGoalAsync / DeleteProjectAsync — soft, D6 dangling-association,
  // no hard delete). M5DocTypes registers the two new docs (indexes:
  // (ComponentId, Created) on each, GoalId on Project) + the ProjectId indexes
  // on TodoItem + KanbanBoard.

  // TBD ✓ (ADR 0087; design/tbd-todo-dependency-design.md) — the "waiting on"
  // dependency lane on top of M5, additive on this same context + M5DocTypes
  // (zero migration). A to-do points at the other to-do it is waiting on
  // (BlockedByTodoId?) — a hint, never a gate (C-TBD·2): it never changes the
  // to-do's own Audience decision, a write, or a hard-delete cascade. Standing
  // over the to-do stays creator ∪ assignee ∪ GlobalAdmin (C-M5·6, the
  // AssignTodoAsync shape), re-checked server-side in the write lane. Read of
  // the blocker's title / status goes through the existing
  // TodoItemToAuditableResource (no new adapter) — the chip is access-scoped:
  // an unreadable / absent / soft-deleted blocker degrades to the generic
  // label (the C3 404-vs-403 split idiom). The only refusal this lane adds is
  // one cycle guard (self + transitive, the C-M5·7 ParentId-guard shape;
  // clearing to null is always allowed). The IProjectService surface gains
  // additively: the ListPickerTodosAsync seam + the BlockedByTodoId field on
  // CreateTodoRequest / UpdateTodoRequest (with the ClearBlockedBy flag) + the
  // additive blockedOnly feed filter param on ListTodosAsync (a filter, never
  // a gate — the unassignedOnly / projectId discipline). M5DocTypes registers
  // the BlockedByTodoId index on TodoItem.

Localization (ADR 0005 — languages and translations are data, not env)
  LanguageCatalog     { code, nativeName, enabled, sortOrder }                 # one row per supported language
  LocaleSettings      { defaultLanguageCode }                                  # singleton document
  TranslationResource { key, languageCode, text }                              # UI strings
  LocalizedPage       { slug, languageCode, title, body: Markdown, updated }   # terms, about, help

Moderation / audit
  Report           { id, targetKind, targetId, reporterId, reason, assignedModeratorId?,
                     grantsRead, resolvedBy?, resolvedAt? }
  // aggregate rows (list views, §5) use targetKind "component"/"directory" and carry
  // visibleCount/hiddenCount instead of a single targetId
  AccessAudit      { id, at, actorId, effectivePrincipalId?, action, targetKind, targetId?,
                     visibleCount?, hiddenCount?,
                     via: Owner|Audience|Moderator|Report|Delegation|BreakGlass|Admin|Group,
                     outcome: Allow|Deny }

Email outbox
  // Written by the durable email handler (§6.2) when all retries are exhausted.
  // The operator re-queues or discards from here (OPS.md §7).
  EmailDeadLetter  { id, idempotencyKey, recipient, subject, lastError, attempts,
                     createdAt, deadAt }

Media (ADR 0011 — content-addressed byte store; catalog here, bytes on the volume)
  // Registered via MediaDocTypes (one doc surface per feature, M1/M3 pattern).
  // The payload itself is NOT a document: it is the file at
  // {Media__RootPath}/{id[0..2]}/{id}; the hash (sha256 of the payload) is the id.
  // Served only through the app endpoint (audited CanAsync(Read)); C-MED invariants
  // in design/media-file-storage-design.md.
  MediaObject      { id = sha256(payload, lowercase hex), filename?, contentType,
                     sizeBytes, created, createdById? }

Conventions: UUIDv7 (time-ordered) where order matters, else GUID; every document carries
`created` / `updated`; `Audience` is embedded (small, always read with the resource).

**Optimistic concurrency (Marten concurrency token).**
- Every domain document carries Marten's concurrency token (`System.Version`); handlers
  read via `Store.Load` (registering the token) and `Store.Update` fails on mismatch.
- A stale write surfaces as **HTTP 409** with a friendly message ("This was just changed
  by someone else — reload and re-apply your edit"), never a silent overwrite.
  Domain state and its audit row commit atomically (one Marten transaction), so a
  concurrency failure rolls back *both* — no audit row for a write that didn't happen.
- **Exception (last-write-wins is safe):** appended per-user collections where a
  conflicting write is a no-op or self-converging — e.g. `EventRsvp`, keyed per user, so
  a resident's latest status is simply the truth. Decide per-document in M4/M5.

**Audit retention & scope.** "Always on" means every decision is evaluated and
restricted-content decisions are logged — it does not mean the table grows without bound:

- **What is logged:** all decisions on audience-restricted resources (Allow *and* Deny);
  all moderator and report-driven access (Allow or Deny); all admin/moderation actions
  (promote, hide, resolve, …). Routine public-content reads are **not** logged — they are
  not access decisions.
- **List views** (feeds, directory, boards): one aggregate row per view
  (`action: list`, `visibleCount`, `hiddenCount`, `via`), **plus** one row per *visible
  restricted* item (so the log answers "which restricted items did this person actually
  see?"). Unrestricted items in the list and per-item *denials* are **not** logged —
  the aggregate row's `hiddenCount` records that a view filtered something.
- **Retention:** a scheduled purge job (Wolverine, §6) expires old rows by tier —
  routine Allow/Deny on restricted content after **~90 days**; rows tied to an open or
  unresolved **Report** and all moderator/admin-access rows are kept **indefinitely**
  until the report resolves (+90 days). Tunable per instance via config.
- **Deletion:** the purge job is the *only* writer that deletes audit rows; deletion is
  itself logged (a `AuditPurge` summary row: count, cutoff, at).
- **Deletion-of-account interaction:** a departing resident's rows are **pseudonymized**
  (actor id replaced by a tombstone), not deleted — see OPS.md §9.

## 6. CQRS-lite & side effects (Wolverine)

Writes go through commands + handlers; reads through Marten queries/projections. Side
effects are handlers, not controller code.

### 6.1 Command handlers — domain state + audit only

  Commands (examples): CreatePost, UpdatePostAudience, HidePost, RsvpToEvent,
    CreateGroup, AddGroupMember, GrantDelegation, RevokeDelegation, FileReport, ResolveReport

A command handler performs **all load-bearing writes** (domain documents + `AccessAudit`
rows) in one Marten transaction. It does **not** send email. Instead, it publishes an
`OutboxEmail` message (Wolverine transactional outbox — only dispatched if the commit
succeeds). This means:

- A failed SMTP connection can never roll back or delay a domain write.
- Audit rows are never gated behind a network call.
- The report-assignment + audit write commits even when mail is down; the moderator
  is notified asynchronously (at most delayed).

### 6.2 Durable email handler

`OutboxEmail` messages are processed by a single Wolverine durable handler
(`AsDurable()`, invocation state in Postgres):

- **Retry:** exponential backoff, capped at **6 attempts over ~24 hours**, then the
  message is dead-lettered.
- **Dead-letter:** on final failure the handler writes an `EmailDeadLetter` document
  (see §5) with the recipient, idempotency key, last error, and attempt count.
- **Crash-safe:** because invocation state lives in Postgres, a process restart resumes
  pending sends instead of dropping them.
- **Idempotency key** per email (`report:<id>`, `verify:<userId>:<attempt>`) ensures
  retries are bounded and re-verify (new attempt) is distinct from a replay.

Email kinds (all through the same handler):

| Kind                    | Criticality | Failure tolerance                    |
|-------------------------|-------------|--------------------------------------|
| Verification (signup)   | Load-bearing| Dead-letter + `/health` degraded; admin manual verify is the safety valve (M1 scope) |
| Report notification     | Load-bearing| Dead-letter + `/health` degraded     |
| Audience notification   | Best-effort | Dead-letter is fine; feed is the primary channel |
| Event reminder          | Best-effort | Re-runnable scheduled job; dead-letter if SMTP truly down |

### 6.3 Projections

  Projections: PerUserGroupSet (userId -> [groupId]); FeedIndex (componentId -> recent posts)

Projections serve **ordering and trimming** (recent-first, pinned-first, page size) only.
The visibility filter is **always** applied post-fetch via `CanSeeAsync` against the
source documents — never encoded in a projection, so projection lag can never leak or
hide access.

### 6.4 Scheduled jobs

  EventReminders  — scheduled email (best-effort; re-runnable)
  VerifyDigest    — optional: digest of unverified accounts (admin awareness)
  AuditPurge      — tiered expiry of AccessAudit rows (see §5)

Explicitly not used: event sourcing, distributed workflows.

## 7. Front-end

- Razor Layout + per-page views.
- One TS module per page under `client/` (e.g. `client/posts.ts`), plus `client/lib/`
  (`api.ts` with CSRF-aware fetch, toasts, flash).
- Build: `tsc` -> `wwwroot/js`; dev: `tsc --watch`. No bundler, no HMR (accepted tradeoff).
- Loaded via `<script type="module" src="~/js/posts.js">`; imports are relative with
  `.js` extensions (browser ESM).
- **CSRF:** every mutating request from `api.ts` sends the anti-forgery token; the server
  validates it on all non-GET endpoints (enforced, not opt-in — see OPS.md §10).
- **Strings:** all user-facing text is resolved server-side through the localization
  provider (Razor tag helper over `TranslationResource`); `client/*.ts` holds logic, not
  display strings. Static pages (terms, help, privacy, conduct) are `PageKind.System`
  page docs rendered by the single page engine (§9, ADR 0039–0043).

## 8. Deployment & configuration

One instance per neighborhood on a VPS via Coolify.

  compose (dev/local): app (multi-stage) + postgres:18 + mailpit

Env contract:
  Community__Name            # per-instance display name (NOT "Kumunita")
  Community__SupportEmail
  SMTP__Host / Port / Secure / User / Pass
  SeedAdmin__Email           # initial GlobalAdmin address
  SeedAdmin__Token           # ONE-TIME setup token — consumed and invalidated on first
                             # login ("set your password" doubles as first login); env
                             # never holds a reusable admin credential. See OPS.md §2
  ConnectionStrings__Kumunita

First run (versioned migrations + seeder): apply Marten migrations -> apply Identity
migrations -> create GlobalAdmin for SeedAdmin__Email with the token as the one-time
setup credential (invalidated on first use) + send verification email -> seed default
Components (Safety, Maintenance, Social, Governance) -> seed the language catalog
(source language `en` enabled and set as default; source-language UI strings
materialized from the image, ADR 0005). Re-running the seeder is a no-op if the admin
already exists.

**Schema evolution is versioned, not auto-upgrade** (ADR 0004). Every domain
schema change is a Marten storage feature — a `FeatureSchemaBase` subclass
(Weasel objects — tables, indexes, etc.) registered in `StoreOptions.Storage`;
`KumunitaFeature` is the first (`mt.community`, ADR 0004 §B). Applied idempotently by
`ApplyAllConfiguredChangesToDatabaseAsync()` (each object delta-detected against the
live catalog, so a second run over an existing database is a no-op); forward-only.
(In Marten 9 the pre-9.x `IMigration`/`StoreOptions.Migrations` step model no longer
exists; the DDL is exportable for review via `WriteMigrationFileAsync()` — see
`KumunitaFeature`. The `mt` schema has no operator-visible applied-step ledger —
idempotency is delta-detection, not a recorded sequence. Identity schema changes are
EF Core migrations recorded in `identity.__EFMigrationsHistory`.) Both are forward-only.
Auto-upgrade (schema derived from document shapes) is dev-only, if used at all — it
is never run against production.

Ops: TLS via Coolify / Let's Encrypt; `/health` (reports **degraded** when the email
dead-letter count is non-zero — §6.2); scheduled `pg_dump` + offsite copy.

## 9. Localization (multilingual)

Design and rationale in ADR 0005; this is the operating shape. **Current state: the
`ML` lane is shipped** (2026-09-12 — see `design/multilingual-design.md`
§ Multilingual — Closed) — the two content documents (`TranslationResource` /
`LocalizedPage`), the `ITranslationProvider` read seam, the `ILocalizationService`
admin seam, the `LocaleCookie` preference, and the `/admin/languages` + settings +
`/terms`/`/help` Web surface are all live. **`ML-UI` (ADR 0015, 2026-09-12) then
wired that seam into the views** — the full platform UI surface (251 keys,
the registry's completeness universe equals exactly what the views emit)
resolves per request through a `<kw-l>` TagHelper against the provider, the
`en` floor is the `KnownTranslationKeys` registry's own source text (code is
the floor — the first-boot seeder's `en` rows are a stored copy, no reseed is
ever needed), the admin editor is key-managed (a closed list, no hand-typed
key), and the picker is public (signed-out residents can choose a language) —
closing the `/about` follow-on the `ML` record had left open. The `SP` lane
(ADR 0043) widened the static-page routes from three to the **five-surface
set**: `/privacy` + `/conduct` join the hard-coded routes (404 floor, ADR
0043 D2) alongside `/terms` / `/help`; the four `Page`-backed surfaces (terms
/ help / privacy / conduct) are seeded under the `system/` root, `/about`
stays the product-story **view** (not a seeded page, ADR 0043 D1), and the
footer now carries an unconditional "Platform" column linking all five (ADR
0043 D4).

- **What is translatable:** UI strings and platform static pages (terms, help,
  privacy, conduct — the `PageKind.System` pages, §9) — §5 documents. UGC is always rendered **as authored**; machine
  translation is deferred and, if it ever ships, per-item opt-in with a
  third-party-boundary review (ADR 0005 C, SECURITY.md §6). Separately, each UGC
  document carries an **authored-in language tag** (`LanguageCode` on `Post` /
  `PostReply` / `Announcement`, ADR 0018) — a BCP-47 metadata field for future
  search and reader-added language versions, resolved to a concrete code at
  write time (instance default → `en`); it translates nothing. The **TD lane**
  (ADR 0027) builds on that tag at the **display** layer only: on the
  post/reply detail surface the authored-in code is surfaced **additively**
  (as `OriginalLanguageCode` on `PostDetailViewModel` /
  `GroupPostDetailViewModel` / `ReplyItem`, read from the ADR 0018 field) and
  rendered as the first, default-visible variant chip, toggled by a small
  server-rendered / client-`display`-toggle swap. It adds **no** document and
  **no** migration (ADR 0004 §B.1 is not engaged — the field already exists)
  and leaves the ADR 0022 add-translation write lane untouched; the swap is a
  presentation concern, not a data, schema, or authorization one (TD·7).
- **Storage:** languages, the default, and all translations are data in `mt` —
  not env, not image config. Admins add/remove languages and set the default
  in-app; the change is effective on the next request, no redeploy.
- **Resolution order:** user preference (cookie + settings page) → instance
  default (`LocaleSettings`) → source language (`en`). Fallback is per-string /
  per-page, so a partially translated UI degrades gracefully. The preference is
  a cookie, never a claim (thin-token rule, §4.1).
- **Admin surface** (`/admin/languages`, GlobalAdmin): add (BCP-47 code +
  native name), enable/disable, reorder; set the default; edit/preview
  static-page translations per language with a per-language completeness view.
  Removing the default language is blocked (set a different default first);
  removed languages keep their `LocalizedPage` rows so re-adding restores the
  work. All changes are audited admin actions (§5).
- **Source language:** `en` ships in the image and is seeded on first run (§8);
  every other language is community-provided.

## 9. Testing

- `Kumunita.Core.Tests` (xUnit): the authorization table (the "hedge post" case) is a
  first-class test; handler side-effects via Marten TestWidgets. Explicit rows for the
  §4.4 invariants: an **empty `All`-mode audience denies** (no vacuous truth), and a
  **delegate with an out-of-scope action is denied** even when the effective principal is
  the owner. Bulk/single agreement: `CanSeeAsync` and `CanAsync` **agree on the
  empty-`All`-audience invariant** (shared `MatchGroups`, §4.4). Directory: uses
  `CanSeeAsync`; **`contactVisibility` is never evaluated on a profile hidden by
  `visibility`** (no rows, no log entries for the contact block). Side effects:
  **a command handler's domain + audit writes commit even when the email send fails**
  (a failed SMTP must not roll back `FileReport` or drop the `AccessAudit` row — §6.2).
- Concurrency: a **stale `Store.Update` raises `StaleConcurrencyError` and the handler
  maps it to 409** (no domain write, no orphaned audit row — §5 convention).
- `Kumunita.Web.Tests`: a few Playwright e2e flows (register -> verify -> post -> audience gate).
- Target: authorization + delegation exhaustively unit-tested; happy paths e2e-tested.

## 10. Extension seams (deferred, by design)

- **Federation**: `Profile.externalId` reserved; IdentityModule is the only place that knows
  the identity source, so adding OpenIddict is additive. Global identity, local authorization.
- **Geo zones**: metadata on resources/residents; display filtering only, never core access.
- **Group helpers**: new UserInfoModule methods (SuggestNeighbors, SuggestFamily) that
  populate groups — a convenience, not a new authorization concept.
- **MCP / API**: `Core` has no HTTP dependency, so a minimal-API or MCP project can call 

### M4 → M5 deferrals (carried forward — design/m4-events-design.md §5)

M4 shipped the events surface **additively and reusing** (ADR 0054). The
following were **deliberately not** in M4's own scope and are follow-on lanes
(own design doc + ADR each) or M6 work — the M5 close should carry this list
so the out-of-scope boundary stays honest. (One item below — event
translations — has since shipped as its own lane, ADR 0059; it is listed here
to keep the M4 boundary honest, not as a live deferral.)

- **Event translations** — the `Event` is authored-in-language (ADR 0018);
  the ADR 0022/0026/0029 user-added translation lane **now** extends to events
  as a separate named lane (ADR 0059 — `EventTranslation` + the add / update /
  remove seams + the ADR 0027/0049/0051 display).
- **Group events** — a `Group`-scoped event channel (the ADR 0013
  membership-lane precedent would be the shape).
- **Per-resident reminder settings** — "remind me N hours before" is a
  follow-on lane; the single 24-hour-before email is the M4 surface.
- **iCal export** — the `events.ics` endpoint stays M6 (Portability).
- **No admission queue** — `Capacity` is display metadata; `Going` RSVPs are
  the truth (a waitlist/spot-release would be a new ADR).the
  same services later.
- **Calendar**: an `events.ics` endpoint when needed.
- **Cross-neighborhood migration**: versioned JSON export/import service (not built yet).
