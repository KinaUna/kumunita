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
  | **M2** directory & profiles | **shared awareness** — who is here, and who you can trust with what |
  | **M3** posts, components, moderation | **understanding → decision** — a signal reaches its audience; a report links to a moderator |
  | **M4** events, RSVP, reminders | **coordination** — a decision becomes an owned, scheduled, reminded action |
  | **M5** projects (goals, tasks, contributors) | **coordination → outcome** — many signals re-linked into one goal with owners |
  | **M6** portability, iCal, notifications, search, multilingual | **outcome + world seams** — the loop closes *into* the residents' lives (ADR 0005) |

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
    ├── README.md
    ├── docs/
    │   ├── ARCHITECTURE.md
    │   └── adr/
    ├── src/
    │   ├── Kumunita.Core/          # domain, services, Marten store, handlers
    │   │   ├── Identity/           # IdentityModule
    │   │   ├── UserInfo/           # UserInfoModule (profiles, groups, delegation)
    │   │   ├── Authorization/      # AuthorizationModule (audiences, policy, audit)
    │   │   ├── Directory/          # profiles / households
    │   │   ├── Posts/              # posts, announcements, components
    │   │   ├── Events/
    │   │   ├── Projects/
    │   │   └── Moderation/         # reports, moderator scope
    │   └── Kumunita.Web/           # MVC, Razor, client/ (TS), wwwroot
    │       ├── Areas/
    │       ├── Views/
    │       └── client/             # TS sources -> wwwroot/js via tsc
    └── tests/
        ├── Kumunita.Core.Tests/    # authorization + handler tests
        └── Kumunita.Web.Tests/     # Playwright e2e (later)

Two projects. `Core` holds all business logic behind interfaces and never references
ASP.NET HTTP types — keeping it testable and leaving the door open for a future API/MCP
layer. `Web` is a thin HTTP/Razor/TS shell.

## 3. Modular monolith & bounded contexts

In-process modules behind interfaces — not separate services today; the interfaces are
the seam for later extraction.

- **IdentityModule** — authentication; issues the thin principal.
- **UserInfoModule** — who people are: profiles, groups, delegation.
- **AuthorizationModule** — what they may do: audiences, policy, audit.
- **LocalizationModule** — language catalog, default language, translated UI
  strings and static pages (ADR 0005); consumed by the presentation layer, never
  by feature authorization.
- **Feature modules** — Directory, Posts, Events, Projects, Moderation. Directory and
  Posts are both *consumers* of the single bulk visibility capability (`CanSeeAsync`,
  §4.2) — list authorization is one platform primitive, not per-feature logic.

Dependency rule: feature modules depend on the three identity/access modules (and Marten),
never the reverse. AuthorizationModule may call UserInfoModule to resolve groups; it never
calls feature modules.

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
      // CreateGroup / AddMember / RemoveMember
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
    }

### 4.3 Access model

- **Audience** = grants to users and/or groups; combine **Any** (union, default) | **All** (intersection).
- **Groups** = the reuse unit.
- **Delegation** = scoped acting; resolves an effective principal.
- **Moderator access** to audience-restricted content = **off by default**; a report grants
  the assigned moderator audited access to that item; an admin can enable standing
  visibility per scope.
- **Audit** of access decisions = always on.
- **Candidate filters are not authorization** (§4.4). A rule about *who is in the
  candidate set* (e.g. the directory lists only verified residents; an unverified user
  sees themselves) is a product query, applied before `CanSeeAsync`, and is never
  audited as an access decision.
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
  // visibility is an Audience (same embedded structure as content). contactVisibility
  // gates the opt-in contact block (email/phone) and is evaluated only after
  // `visibility` allows the profile — never on a hidden profile.
  Profile          { subjectId, externalId?, householdId?, displayName, verified,
                     visibility: Audience, contactVisibility?: Audience, email, phone? }
  Group            { id, name, description, ownerId, created }
  GroupMembership  { groupId, userId, addedBy, at }
  // "owner" = the account whose standing is borrowed; "delegate" = the account acting.
  // (Deliberate vocabulary: in ASP.NET a "principal" is the *actor*, so the grant
  // fields avoid that word — see §4.2.)
  DelegationGrant  { id, ownerId, delegateId, scope: [action], from, to?, revokedBy? }

  // `householdId` is display/metadata ONLY (future group-helper: "family from
  // household", §10). The authorization path never reads it — household-based
  // visibility is expressed as a household *Group* the owner grants, per ADR 0001-B.

Content
  Component        { id, name, description, icon, sortOrder, enabled, moderatorAccess }
  Post             { id, kind: Announcement|Discussion, componentId?, authorId, title, body,
                     audience, pinned, hidden, created, updated }
  Reply            { id, postId, authorId, body, created }
  Audience         { mode: Any|All, grants: [ { kind: User|Group, id } ] }   (embedded)

Events
  Event            { id, title, description, componentId?, authorId, start, end, location?,
                     capacity?, audience, rsvpRequired }
  EventRsvp        { id, eventId, userId, status: Going|Maybe|No, at }

Projects
  Project          { id, title, description, componentId?, ownerId, status, audience, created }
  ProjectTask      { id, projectId, title, assigneeId?, done, order }
  ProjectMember    { id, projectId, userId, role }

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
                     via: Owner|Audience|Moderator|Report|Delegation|BreakGlass,
                     outcome: Allow|Deny }

Email outbox
  // Written by the durable email handler (§6.2) when all retries are exhausted.
  // The operator re-queues or discards from here (OPS.md §7).
  EmailDeadLetter  { id, idempotencyKey, recipient, subject, lastError, attempts,
                     createdAt, deadAt }

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
  display strings. Static pages (terms, about, help) are `LocalizedPage` Markdown rendered
  by a single page engine (§9, ADR 0005).

## 8. Deployment & configuration

One instance per neighborhood on a VPS via Coolify.

  compose (dev/local): app (multi-stage) + postgres:16 + mailpit

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

**Schema evolution is versioned, not auto-upgrade** (ADR 0004). Every domain schema change
is an ordered `IMigration` step registered in `StoreOptions.Migrations`, recorded in
`mt.migrations`; Identity schema changes are EF Core migrations recorded in
`identity.__EFMigrationsHistory`. Both are forward-only. Auto-upgrade (schema derived from
document shapes) is dev-only, if used at all — it is never run against production.

Ops: TLS via Coolify / Let's Encrypt; `/health` (reports **degraded** when the email
dead-letter count is non-zero — §6.2); scheduled `pg_dump` + offsite copy.

## 9. Localization (multilingual)

Design and rationale in ADR 0005; this is the operating shape.

- **What is translatable:** UI strings and platform static pages (terms, about,
  help) — §5 documents. UGC is always rendered **as authored**; machine
  translation is deferred and, if it ever ships, per-item opt-in with a
  third-party-boundary review (ADR 0005 C, SECURITY.md §6).
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
- **MCP / API**: `Core` has no HTTP dependency, so a minimal-API or MCP project can call the
  same services later.
- **Calendar**: an `events.ics` endpoint when needed.
- **Cross-neighborhood migration**: versioned JSON export/import service (not built yet).
