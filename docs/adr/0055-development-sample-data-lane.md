# ADR 0055 — Development sample data: a first-boot, Development-only mock neighborhood

Status: Accepted (the **gate** is amended by [ADR 0056](0056-sample-data-opt-in-and-deploy-posture.md):
the seeder now runs on an explicit `SampleData__Enabled` opt-in rather than the `Development`
wall, and gains a deploy posture for a public demo site. The data model, content, and
idempotency decided here are unchanged.)
Date: 2026-09-21
Amends: **0012** (the `Component.Mandatory` flag + the sanctioned
`SetCommunityMandatoryAsync` write lane — *reused* as the visibility
mechanism, not re-invented), **0003** (the `Moderator` role +
`ModeratorAssignment` standing — the seeded moderator's scope rides it),
**0021 / 0022 / 0026 / 0029** (the translation surfaces — the seeded
translations author against them), **0040** (the user blog-root + page-hierarchy
idiom — the seeded resident blog nests under it), **0004 §B** (the
document surfaces the sample content is stored onto — untouched, additive
rows only).

## Context

The dev loop is `docker compose down -v && docker compose up --build` — a
pristine database every time. Out of the box a fresh instance is a login
page: one tokenless `GlobalAdmin` (the `SeedAdmin__*` lane, ADR 0001/OPS
§2), the four components, the language catalog, and the canonical static
pages. That is enough to *run*, but not enough to *work with* — there are no
residents, no posts, no groups, no events, no translations, and nothing to
exercise the feeds, the group-lane, the RSVP lane, the tag surfaces, or the
multilingual display against. A developer testing "does a community post
show for a non-author?" or "does a private group hide from a stranger?" had
to hand-build all of that through the UI first.

The need is strictly a **development convenience**: a realistic, multi-role,
multi-language neighborhood on first boot, so a fresh dev instance is
immediately exercisable. It must satisfy two hard constraints:

- **It must never ship.** A real neighborhood's deployment must be incapable
  of seeding demo residents, demo credentials, or a `test@…` support address.
- **It must not corrupt.** Re-running on a warm database (a restart, a
  `--build`) must not duplicate every group / announcement / post, and it
  must not clobber a developer's edits to the seeded content.

## Decision

- **A new static writer `Kumunita.Core.Bootstrap.SampleDataSeeder`** — the
  `FirstBootSeeder` posture (a static class that resolves `AppDbContext`,
  `IDocumentStore`, `UserManager<User>`, `RoleManager<IdentityRole>`,
  `IUserInfoService`, and `ILogger` from the call site). It is **not** a new
  bounded context and **not** a new document surface: every row it stores is
  an instance of an already-registered doc (`User`/`Role` on the
  `identity` side; `Profile`, `Group`, `GroupMembership`, `ModeratorAssignment`,
  `Tag`, `TagTranslation`, `Announcement`, `AnnouncementTranslation`, `Post`,
  `PostReply`, `PostTranslation`, `ReplyTranslation`, `GroupTranslation`,
  `Event`, `EventRsvp`, `Page`, `PageTranslation` on the `mt` side). **No
  migrations, no schema change, no new ADR-constrained surface.**
- **The gate is `Development` ∧ first-boot.** `Program.cs` captures
  `DbBootstrap.IsPristineAsync` **before** `SchemaBootstrap.ApplyAsync` runs
  `MigrateAsync` (which creates the `identity` schema and would flip the
  pristine check to false), and runs the seeder only when
  `app.Environment.IsDevelopment() && firstBoot`. Two independent walls:
  non-`Development` deployments *cannot* reach it, and a warm dev restart
  *cannot* re-run it. The accounts are find-or-create keyed by e-mail, so
  even an accidental double-run converges (the `FirstBootSeeder` idempotency
  precedent); the content rows are create-once, so the pristine gate is what
  actually keeps them from duplicating.
- **The admin is found, not created.** `FirstBootSeeder` already creates
  `SeedAdmin__Email` (the one-time token lane) on first boot. The seeder
  resolves that same address and, if its `PasswordHash` is empty, adds the
  demo password via `AddPasswordAsync` — so the tokenless first-boot admin
  becomes usable *and* keeps its setup-token lane. **`docker-compose.yml`
  `SeedAdmin__Email` and `SampleDataSeeder.AdminEmail` are the same string by
  design** — the seeder keys off the seeder's own `AdminEmail` constant to
  find the account, and the compose file must match or the seeder would
  create a second admin. They are paired.
- **The demo e-mail domain is `examplium.com`, not `kumunita.com`.**
  `kumunita.com` is reserved for real residents in real deployments; using a
  distinct, obviously-fictional domain (RFC 2606 `example` family, spelled
  `examplium`) keeps a developer's test logins from ever being mistaken for
  — or colliding with — production accounts. All seeded identities
  (admin, moderator, translator, the four residents) and the dev
  `Community__SupportEmail` use `@examplium.com`.
- **The visibility trick is ADR 0012, not a new mechanism.** The seeder
  marks all four components **`Mandatory`** through the sanctioned
  `IUserInfoService.SetCommunityMandatoryAsync(componentId, true, adminId,
  GlobalAdminRoles)` write lane (own session + its `AccessAudit` row), so
  every verified resident is a member of every board and a flat
  `Audience { Community = true }` post is visible to all of them with **no
  per-account grant rows**. No new `Audience` branch, no new `AccessVia`, no
  new `IAuthorizationService` surface — the frozen `Decide()` already
  resolves it.
- **The content is additive sample rows across the shipped lanes.** A
  private family group (`Kowalski Family`) + a public street group
  (`Street Green`) with memberships; a scoped moderator (`Maria`) with the
  `Moderator` EF role + two `ModeratorAssignment` rows (Safety + Social,
  ADR 0003); tags (+ de/fr `TagTranslation`s); three announcements (a pinned
  Public welcome, a Community-flat volunteer call, a Community-targeted
  safety alarm) with de/fr `AnnouncementTranslation`s; three community posts
  (Safety / Social / Governance) with replies, a de `PostTranslation` +
  `ReplyTranslation`, and tag assignments; a group-lane post under the
  public group; a de `GroupTranslation`; two published events (the `IsDraft =
  false` pin) with a Going/Maybe/No `EventRsvp` mix; and a resident blog
  (a `PageKind.User` blog root + one child page, ADR 0040) with a de
  `PageTranslation`. A single `IDocumentSession` stores all `mt`-side rows
  and commits once (invariant C3). **No `OutboxEmail` is staged** — the
  seeder is not a user-facing sign-up, so it has no `IMailerStage`
  dependency and sidesteps the Wolverine `IMessageContext` requirement the
  outbox lane needs.
- **The credentials are a documented, printed constant.** The demo
  e-mails/passwords are `public const` on `SampleDataSeeder` (weak by
  design, matching the dev-DB-password convention), printed to the boot log
  once, and **documented in `README.md` §Running and `docs/OPS.md`** so a
  developer never has to dig through the log to pick one up. They are
  `Development`-only and never reach a real deployment.

## Consequences

- **A fresh dev instance is immediately exercisable** — multi-role (admin /
  scoped-moderator / translator / four residents), multi-language (en +
  de + fr rows), multi-lane (groups, community + group posts, replies,
  events + RSVPs, tags, a resident blog, translations). The private group is
  a natural ADR 0010 test fixture; the mandatory components are a natural
  ADR 0012 / audience test fixture; the RSVP mix is a natural ADR 0054
  fixture.
- **Zero production risk by construction.** The `IsDevelopment()` wall is
  independent of the pristine wall; a `Production`/`Staging` deployment runs
  none of this and never sees the `examplium.com` identities or the
  `test@examplium.com` support address.
- **No drift surface.** It touches no doc shape, no authorization seam, no
  renderer/editor, no email trio — it only *stores instances* of existing
  docs through existing write lanes, so there is nothing new to keep in
  sync and no test to pin (it is deliberately untested: a dev convenience,
  validated end-to-end against a live `docker compose` instance rather than
  unit-tested — the `FirstBootSeeder` is the same posture).
- **One paired invariant to remember:** `docker-compose.yml
  SeedAdmin__Email` and `SampleDataSeeder.AdminEmail` must stay the same
  string (the seeder finds the first-boot admin by e-mail). Changing the dev
  admin address means changing both.
- **Out of scope (deliberate):** seeding media / avatars (needs the byte
  store + the `MediaObject` catalog, a heavier fixture), seeding a `Guardian`
  link (ADR 0028) or a `DelegationGrant`, and any e-mail (no outbox). Those
  are follow-ons if a specific lane ever needs a seeded fixture.
