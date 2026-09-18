# Kumunita

A self-hosted community platform for a single neighborhood. Residents can find and
get to know one another, share announcements and events, discuss topics in functional
areas, and work on projects together — with privacy-first, user-controlled access.

Each deployment serves **one neighborhood**. The same codebase is deployed
independently per community (its own container + its own Postgres), so there is no
multi-tenant data model: scaling to many neighborhoods is a *configuration* concern,
not a code concern.

**Naming.** *Kumunita* is the platform (this repository). Each deployed instance shows its own community name (e.g. "Maplewood Residents"), set via `Community__Name`.

## Status

**M3 complete** (`docs/design/m3-posts-design.md`,
`docs/design/m3b-moderation.md`). The live loop now includes M1 identity /
groups / delegation / authorization, M2 directory + profile editor + groups,
M3 posts + audience-scoped feeds + the M3b moderation lane
(file/assign/resolve + `PostStatus`) — all server-rendered MVC + Razor, with
a durable Wolverine outbox, retry + dead-letter, and the `/health` degraded
gate. The deployable surface has grown but the topology hasn't changed:
`Kumunita.slnx` (`Kumunita.Core`, `Kumunita.Web`, `Kumunita.Core.Tests`, `Kumunita.Web.Tests`),
multi-stage Docker image, the versioned schema boot (Marten feature + EF
Identity migration + first-boot seeder), a `/health` liveness probe, and a
`Coolify`-based deploy (Coolify `app` + dedicated Postgres 18, image parity
with `dev-db-init` + `docker-compose.yml`: **18**).
Profile **avatars** have also landed — the reference lane of the content-addressed
local-volume media store (ADR 0011, its own design doc); it adds a second restore
surface next to the Postgres dump (OPS.md §4/§5).
**Multilingual** has landed as **two lanes**. `ML` (ADR 0005) shipped the
**seam**: the admin-managed language catalog + instance default, the per-request
translation provider (preference cookie → default → `en`, per-string/per-page
fallback), the `/admin/languages` surface, and the `/terms` + `/help`
static-page routes. `ML-UI` (ADR 0015) then wired the **live UI**: every in-scope
view resolves per request, a seeded `en` floor is always present, the admin
edits the **closed** key list at `/admin/languages`, a signed-out visitor can
pick a language at `/language`, and `/about` is a static page.
**Rich content** (`RC`, ADR 0025) has landed — Markdown bodies + in-content
images on posts, replies, announcements & static pages: one escape-first
renderer extension (`![alt](src)` under a stricter `src` allowlist), the
`GET /content-image/{id}` serving route (decision-deferred to the owning
resource, Deny → 404 not 403, one `Read` audit row), the `POST /content-image`
upload lane (ADR 0011's boundary verbatim), and the composer control on all
four surfaces.
**GU is done** — guardian controls, the account-scope supervision of a child's
account (ADR 0028); **next is PG** — the hierarchical, audience-restricted,
translatable pages tree (ADR 0039); **M4**: events, RSVPs, reminders (per the
roadmap table in `docs/ARCHITECTURE.md`); **M5**: projects.

## Principles

- **Lean.** Smallest stack that meets the requirements. No premature services, no ceremony.
- **Privacy-first.** The author's choice of audience is absolute by default.
- **Audit by default.** Access to restricted content is always logged.
- **Boring where it can be.** Server-rendered pages, plain TypeScript, one database.
- **Integration over features.** A neighborhood's life is fragmented — people,
  knowledge, trust, problems. Kumunita's value is the *linkage* that turns those
  parts into a whole, not the count of parts. This is the spine of our
  development philosophy: see [`docs/philosophy/`](docs/philosophy/).

## Features

- Resident directory (profiles, opt-in contact details)
- Announcements & discussions, organized by **functional components** (Safety, Maintenance, Social, Governance, …)
- Events with RSVP and reminders *(planned — M4, deferred until after multilingual; see the "Roadmap" below)*
- Collaborative projects (goals, tasks, contributors) *(planned — M5, see the "Roadmap" below)*
- **Groups** — public groups power reusable access lists; **private groups**
  (ADR 0010) are a membership/organizing unit for a family or circle, and stay
  out of the audience pickers
- **Delegation** — owners grant family/caretakers scoped access
- **Guardian controls** — a parent adds an account for a child and supervises it
  at the account level: suspend/lock, curate the child's community & group
  memberships, and approve a group invitation sent to the child — with **no
  standing to read the child's private content** (ADR 0028; the child's account
  is handed over to independence when the child comes of age)
- **Avatars & media** — profile avatar upload + serving on a content-addressed
  local-volume byte store behind an HTTP-free seam (ADR 0011). Group logos and
  badge icons are **follow-on lanes reusing the same seam** — each with its own
  design doc, not this one. (Post / reply / announcement attachments have
  shipped — the **File attachments** bullet below.)
- **File attachments** — attach a file (PDF / Office docs / text / csv / zip +
  the raster image types) to a **post, group post, reply, or
  announcement**: a body link `[label](/attachment/{id})` that **downloads**
  (`Content-Disposition:
  attachment`, not an inline render) on the **same** content-addressed byte
  store as images, under a **separate** file allowlist (SVG excluded) and the
  owning resource's `Read` decision (a reply resolves its parent post's); an
  "Attach file" toolbar button on every composer (ADR 0034). Follow-on lanes
  (own design doc + ADR): attachments on static/about pages; video/audio;
  in-browser preview.
- **Group posts** — the post channel *inside* a group (`/groups/{id}/posts`):
  a membership-scoped feed, detail, composer and replies (ADR 0013). Members
  only — a non-member (moderator or admin alike) neither sees nor posts; the
  audience lane is never evaluated, and replies inherit the parent's single
  membership decision.
- **Drafts** — a post, group post, or announcement can be **saved but not
  made public yet**: a "Save as draft" option on every composer. A draft is
  the author's private scratchpad — excluded from every feed and the pinned
  list, invisible to everyone **including a `GlobalAdmin`** who is not the
  author (no audit row, not subject to the audience decision), and visible
  only to its author, who can edit it, find it at `/my/drafts`, and publish
  it with one action (publishing is author-only too). Editing a draft never
  publishes it (ADR 0037).
- Moderation with component-scoped moderators and full audit
- **Multilingual** — UI and platform texts (terms, help) are translatable,
  resolved per request (user preference → instance default → `en`, with
  per-string / per-page fallback). The admin manages the language catalog +
  instance default in `/admin/languages` (audited), and residents pick their
  language on the settings page (a cookie, never a claim). A non-English-speaking
  neighborhood can run its platform in its own language with no code change or
  deploy (ADR 0005). The `ML-UI` lane (ADR 0015) then makes it **real in the
  UI**: a resident **sees** the platform in their language (every in-scope view
  resolves per request, with a seeded `en` floor that is always present), the
  admin edits the **closed** key list per language (no hand-typed key), a
  **signed-out** visitor can pick a language at `/language`, and `/about` renders
  an admin-authored page or the product story. The `TR` lane (ADR 0021) then
  delegates the editing: a GlobalAdmin can grant a **`Translator`** role to a
  resident, who may update and add the UI strings and static pages but holds
  none of the GlobalAdmin's other standing (the catalog — add / enable /
  reorder / set-default / remove — stays GlobalAdmin-only). The `LS` lane
  (ADR 0042) then ships a **bundled initial pack**: a first-boot instance has
  German and French enabled with complete UI-string and about/terms/help
  baselines (English stays the default and the only code-owned language);
  per-string fallback still lands on the en floor.
- **Timezone** — the platform carries a **default time zone** the admin sets
  once (`/admin/timezone`, audited); each resident can override it in their
  own settings page (the time-zone section of `/settings/language`). Every
  timestamp renders in the
  effective zone (resident override → platform default → `UTC` floor) via the
  `kw-dt` TagHelper — the same per-request resolution shape as multilingual,
  data-driven, no rebuild (ADR 0019).
- **Date & time format** — the platform carries a **default date-time format**
  the admin sets once (`/admin/dateformat`, audited) and each resident can
  override in their own settings page (the date-format section of
  `/settings/language`). A short preset list (Long / Short / ISO / Day-first)
  plus a free-text **custom** format; the unambiguous Long format is the floor.
  Every timestamp renders in the effective format (resident override → platform
  default → Long floor) via the `kw-dt` TagHelper — the same per-request
  resolution shape as timezone and multilingual, data-driven, no rebuild
  (ADR 0020).
## Tech stack

- **ASP.NET Core 10** — MVC + Razor, server-rendered
- **Plain TypeScript** — compiled with `tsc` only (no bundler, no dev server)
- **Marten** — Postgres document store (domain); **EF Core** strictly for ASP.NET Identity tables
- **Wolverine** — in-process messaging, scheduled jobs, CQRS-lite
- **No event sourcing** — documents + projections instead
- **ASP.NET Core Identity** (cookie) now; **OpenIddict** (OIDC) later, for cross-neighborhood federation

## Architecture

A **modular monolith**. Identity and access are split into three in-process bounded
contexts behind interfaces — extractable later, not separate services today:

- **IdentityModule** — lean authentication; issues a thin principal (`subjectId`,
  `isVerifiedResident`, base roles). Nothing relational in the token.
- **UserInfoModule** — who people are: profiles, groups, delegation grants.
- **AuthorizationModule** — what they may do: audience evaluation + policy. Always audits.

The guiding rule: **thin token, fat authorization service.** "Can this person see that
post?" is never a claim — it's a query resolved per request — so the identity story
stays trivial and the authorization rules can grow freely.

### Access model

- An **audience** is a set of grants to **users** and/or **groups**, combined with
  **Any** (union, default) or **All** (intersection).
- **Groups** are the reuse unit — grant a post to a group once; membership changes ripple everywhere. **Private groups** (ADR 0010) are a membership/organizing unit *not* in the reuse unit — they do not appear in the audience pickers (decluttering) and are intended for the group's own members, e.g. a family or a close circle.
- **Delegation** lets an owner grant another person scoped access; the system resolves an *effective principal* for that actor.
- **Moderator access** to audience-restricted content is **off by default**. A filed **report** grants the assigned moderator audited access to that item; an admin can enable standing moderator visibility per scope.
- **Audit** of access decisions is always on.

### Roles

- **GlobalAdmin** — full control; manages moderators, the language catalog, and every other control-plane action.
- **Moderator** — scoped to one or more functional components.
- **Translator** (ADR 0021) — may edit the platform's UI strings and static pages; holds none of the GlobalAdmin's other standing.
- **Member** — verified resident.

## Deployment

- One instance per neighborhood, on a **VPS via Coolify**.
- Docker multi-stage build (compile TS with `tsc`, publish, runtime) + **Postgres**.
- Config via environment: community name, SMTP, seeded admin.
- TLS via Coolify / Let's Encrypt; `/health` endpoint; scheduled Postgres backups.

## Roadmap

- **M0** — Deployable scaffold: solution, Docker, Coolify deploys "hello" with a live DB.
- **M1** — Identity, groups, delegation, and the authorization model above.
- **M2** — Directory & profiles with visibility rules.
- **M3** — Posts/announcements in components; moderation + reports.
- **Multilingual** (`ML`, ADR 0005) — UI + platform texts (terms, help)
  translatable; admin-managed language catalog & default; `/admin/languages`
  surface + `/terms`/`/help` static pages. **Done.**
- **Multilingual — live UI** (`ML-UI`, ADR 0015) — in-scope views resolve per request; seeded en floor; key-managed admin editor; public language picker; /about static page. **Done.**
- **Languages seeded** (`LS`, ADR 0042) — a first-boot instance ships German and French enabled with complete UI-string and about/terms/help baselines (English stays the default and the only code-owned language); per-string fallback still lands on the en floor. **Done.**
- **Timezone** (`TZ`, ADR 0019) — platform-default time zone (admin-set); per-resident override in personal settings; all timestamps rendered in the effective zone (`kw-dt`). **Done.**
- **Date & time format** (`DF`, ADR 0020) — platform-default date-time format (admin-set); per-resident override in personal settings; presets (Long / Short / ISO / Day-first) + custom format; all timestamps rendered in the effective format (`kw-dt`). **Done.**
- **Translator** (`TR`, ADR 0021) — a GlobalAdmin can grant the `Translator` role to a resident, who then may edit the platform's UI strings and static pages; catalog management (add/enable/reorder/set-default/remove) stays GlobalAdmin-only. **Done.**
- **Rich content** (`RC`, ADR 0025) — Markdown bodies + in-content images on posts, replies, announcements & static pages. **Done.**
- **Translation display** (`TD`, ADR 0027) — the authored-in language (ADR 0018) is the first, default-visible variant chip on the post/reply detail surface (both lanes); added translations are clickable chips that swap the title+body / body in place; the "add a …" lane excludes the authored-in language; soft-deleted rows show no swap. Display-only — no data, schema, or auto-translation change. **Done.**
- **Guardian controls** (`GU`, ADR 0028) — a parent adds an account for a child and supervises it at the account level: suspend/lock, curate the child's community & group memberships, and approve a group invitation sent to the child — with **no standing to read the child's private content**; the child's account is handed over to independence when the child comes of age. **Done.**
- **Guardian assignment** (`GA`, ADR 0038) — an existing guardian assigns a **second** guardian to a child's account (email-driven; one `IIdentityService` ADD + one `GuardianController` action + the Detail view's assign form + the "other guardians" list); the assigned guardian's standing is **identical in kind** to the creator's (the five GU actions, no content read); a duplicate assignment is a no-op (idempotent); self-assignment is refused. **Done.**
- **Rich editor** (`RE`, ADR 0031) — a WYSIWYG authoring surface over the RC Markdown lane: a split-view live preview beside the source `<textarea>` and a Markdown-splice toolbar (bold / italic / code / headings / lists / link / image) in one `tsc`-only module. No editor dependency, no second renderer, no new route — the saved body is byte-identical Markdown the RC read path already renders. **Done.**
- **Inline editor** (`IE`, ADR 0032) — the rendered view is the default editor; the Markdown source is hidden behind a single toolbar toggle (and stays a one-click split view when revealed). Additive and client-only — no new dependency, no new route, no re-shape of the RE pure functions; the saved body is byte-identical Markdown the RC read path already renders. **Done.**
- **File attachments** (`ATT`, ADR 0034) — attach files (PDF / Office docs / text / csv / zip + the raster image types) to **posts, group posts, replies, and announcements**: a body link `[label](/attachment/{id})` that **downloads** (`Content-Disposition: attachment`, not an inline render); a **separate** file allowlist (`Media__AttachmentAllowedContentTypes`, SVG excluded) under the **same** content-addressed byte store as the image lane (one store, one volume, one catalog); served only through an audited app endpoint under the owning resource's `Read` decision (a reply resolves its **parent post's**); an "Attach file" toolbar button on every composer (incl. reply composers). Follow-on lanes (own design doc + ADR): attachments on **static/about pages**; video/audio; in-browser preview. **Done.**
- **Drafts** (`DM`, ADR 0037) — a post, group post, or announcement can be **saved but not made public yet**: a "Save as draft" option on every composer. A draft is the author's private scratchpad — excluded from every feed and the pinned list, invisible to **everyone else (a `GlobalAdmin` included)** (no audit row, not run through the audience decision), visible only to its author, who edits it, finds it at `/my/drafts`, and publishes it (publishing is author-only too); editing a draft never publishes it. **Done.**
- **Pages** (`PG`, ADR 0039) — a **hierarchical, audience-restricted, translatable** knowledge tree: a `Page` doc with a `ParentId` + `Slug` (a forest of roots, a derived path — not a stored one), the **exact** post `Audience` (`null` = public; non-null = grants / community — ADR 0001-B / 0036, *reused*, not extended), an authored-in language (ADR 0018) + `PageTranslation` rows (the ADR 0022/0026/0029 shape), a `MountPoint` string for UI slots (`footer/community` for the about page, `help/account` for a change-password help page), and a standing matrix (GlobalAdmin ∪ community-Moderator ∪ author). Served by `PageController` (`/pages` tree browse + `/pages/{path}` post view + WYSIWYG composer); the **one** `MarkdownRenderer` + the **one** `bindRichEditor` render/edit it; the **one** `IAuthorizationService` decides access (via a `PageToAuditableResource` adapter — *no* new `AccessAction`, *no* new `AccessVia`, *no* new authorization branch). **Absorbs and retires** the `LocalizedPage` static-page lane (ADR 0005 A) — the old `/about`/`/terms`/`/help` routes keep resolving (now from the tree); the seeded default pages carry the same `en` floor a fresh instance has today. The old `LocalizedPage` surface is retired last (U07), after the new surface is proven. The `LocalizedPage` retirement is the **only** destructive step in the lane, and it is sequenced last. **Next.**
- **M4** — Events, RSVPs, reminders. **Planned.**
- **M5** — Projects (goals, tasks, contributors).
- **M6** — Portability (export/import), iCal, notifications, search, responsive pass.

## Deferred (future, by design)

- **Machine translation of user-generated content** — optional and opt-in if it
  ever ships: per-item, clearly labeled, and the MT provider becomes a third-party
  boundary (ADR 0005 C, SECURITY.md §6). Until then UGC is rendered as authored.
- **Geographic zones** as metadata + display filtering (not part of the core access model).
- **Cross-neighborhood federation** — a standalone OpenIddict IdP; global identity, local authorization.
- **Group helpers** — suggest/populate groups (neighbors from addresses, family from household).
- **MCP**, calendar integration, cross-neighborhood data migration.
- **Invitation-only sign-up** — self-service sign-up (with the verification
  email + admin manual-verify valve) is deliberately left open for now so
  the team can create development accounts without an admin. The long-term
  default should be **invitation-only accounts** (an invited resident
  self-serves their password from an admin-sent invitation link; open
  sign-up is an opt-in, not the default). Decide and land before the
  community is open beyond the development circle (SECURITY.md §6 open
  items — the control that answers adversary A2, the signup bot).

## Running

*Dev (Docker):* `docker compose up -d --build` — Postgres on
`localhost:5433`, the app on `http://localhost:5080` (built from `Dockerfile`,
running `Development` so the `mt` schema auto-applies on a fresh database —
ADR 0004), and Mailpit for SMTP (`localhost:1025`, UI at `http://localhost:8025`).
Logins survive container rebuilds: the data-protection keyring persists in the
`kumunita_dpkeys` named volume (same `/data/dataprotection-keys` container
path as prod — COOLIFY §5.2), so `--build` recreations don't wipe session/
antiforgery state.
Smoke test: `GET /health` → `{"status":"ok","database":"ok","build":"<sha>",…}` and `/`
renders the configured `Community__Name`. On a **fresh** database the versioned boot block
initializes the `mt` schema with no operator step (ADR 0004) — the log shows **First boot**
exactly once, and subsequent boots are a no-op.

*Dev (no app container):* `docker compose up -d db`,
`npm run build` in `src/Kumunita.Web/`, then
`dotnet run --project src/Kumunita.Web` (settings from
`appsettings.Development.json`, DB on `localhost:5433`).

*Prod:* one neighborhood per instance — identical image + dedicated Postgres +
env per the [OPS.md configuration reference](docs/OPS.md), TLS via
Coolify/Let's Encrypt, `/health` monitored, scheduled Postgres backups.

## Documentation

- `docs/philosophy/how-it-works.md` — **how the platform works, in plain
  language** — for residents who aren't technical collaborators: what it does,
  how privacy and moderation actually behave, and how to give feedback that
  helps. No code background needed.
- `docs/philosophy/` — **our development philosophy**: why the platform exists (it links a
  neighborhood's fragmented life into a whole) and how we build accordingly. Start at
  [`docs/philosophy/START-HERE.md`](docs/philosophy/START-HERE.md)
- `docs/SECURITY.md` — **security & privacy: the top priority** — threat model, data classes, control map
- `docs/ARCHITECTURE.md` — detailed stack, data model, module boundaries
- `docs/OPS.md` — operations runbook: provisioning, upgrades, backups, restore, security
- `docs/COOLIFY.md` — Coolify setup: one-time VPS install, per-neighborhood Postgres + app, verify
- `docs/adr/` — architecture decision records (0001–0027)
- `docs/design/` — per-milestone design docs (M1: [`docs/design/m1-identity-access.md`](docs/design/m1-identity-access.md) — identity, groups, delegation, authorization; media: [`docs/design/media-file-storage-design.md`](docs/design/media-file-storage-design.md) — the media & file-storage lane, ADR 0011, profile avatar as the reference lane)
