# Pages lane (`PG`) — handoff notes (scratch tier)

> **Three-tier contract.** This lane has three written surfaces, in order of
> authority:
>
> 1. **Primary — the design doc** `docs/design/pages-design.md`. The source
>    of truth for *what* to build and *why* — the `Page` / `PageTranslation`
>    field sets, the hierarchy + audience + translation + mount-point
>    decisions, the standing matrix, the absorb-migration ordering. Every
>    decision marked **[DECIDED — ADR 0039]** is locked.
> 2. **Secondary — the register** `docs/plans-milestones/pages/plan-pages.md`.
>    Source of truth for *which* unit, in *which* order, touches *which*
>    files, the exit criteria, and the drift-pause policy.
> 3. **Scratch — this file.** A running log. Each unit **appends** a `## U#`
>    section to the end. It is never rewritten or reordered — later agents
>    read it top-to-bottom to see what the earlier agents actually did,
>    including anything that drifted from the plan.
>
> **Do not edit an earlier `## U#` section.** If you find a mistake, add a new
> section and note it. Append, don't amend.

## Protocol (every agent, every unit)

- This file + your **unit plan** (`docs/plans-milestones/pages/pages-uNN-plan.md`)
  is the whole context you need. **Do not** scan the whole repo.
- Read the unit plan's "entry reads" (a small fixed set), do the work, hit the
  unit's build gate, **then** append a `## U#` section here *before* doing
  anything else. The section must record:
  - **what you built** (files + the one-line purpose of each),
  - **what you verified** (the build command + its green result — remember:
    tests run via `dotnet exec` of the DLL, **not** `dotnet test`),
  - **any drift from the plan** (a deviation, an extra file, a skipped step) —
    say so explicitly; silence means "no drift,"
  - **what the next agent must know** that isn't already in the plan (a seam
    that turned out different, a test that needed a tweak, a follow-on to
    track).
- If you hit a real blocker (a failing build you can't resolve, a missing
  file, an ambiguous requirement, a standing-matrix cell the `PageService`
  can't express without a new ADR), **stop and say so** in a
  `## U# — BLOCKED` section instead of guessing.
- **Sequencing invariant (drift-pause (e)):** U07 (the destructive
  `LocalizedPage` retirement) runs **only after** U01–U06 are green. A fresh
  agent picking up this lane starts at **U01** (U00 is already done by lock).

## Lane open

- **Lane:** `PG` (Pages) — a named lane (the `ML`/`GP`/`RC`/`RE` convention),
  **not** a roadmap renumber. M4/M5/M6 stay Events / Projects / Portability.
- **ADR:** **0039** (`docs/adr/0039-pages-hierarchy-audience-translations.md`),
  **Accepted** 2026-09-17. Adds the `Page` + `PageTranslation` docs, a new
  `Kumunita.Core.Pages` context, the `PageToAuditableResource` adapter, the
  `MountPoint` string, and the absorb/retire of `LocalizedPage`. Amends
  0005/0018/0022/0026/0027/0037; additive on 0001-B/0006/0036/0025/0034.
- **Locked decisions** (the 4 the user signed off, all now **[DECIDED]** in the
  design doc):
  1. **Absorb** the existing static-page lane (`LocalizedPage`) into one tree —
     not a parallel lane. `/about`/`/terms`/`/help` become ordinary pages.
  2. **Pages default public** (`Audience = null`) — the one place pages differ
     from posts (posts default community-visible, ADR 0036).
  3. **`MountPoint`** = one nullable string column on `Page` + one resolver
     (`GetByMountPointAsync`) for UI slots (`footer/community`, `help/account`).
  4. **Delete = soft-delete** (`IsDeleted` flag + `CanSeeAsync` filter, ADR
     0024 shape).
- **U00 (sign-off + ADR + roadmap trio) is DONE by lock** (this session):
  ADR 0039 Accepted; `Milestones.cs` gains the `PG` row (`StatusPlanned`,
  after `RE`, before `M4`); README Roadmap gains the `PG` row;
  `MilestonesTests.cs` `Ids` array gains `"PG"`; build + Web tests (203) green.
- **Next unit: U01** (`Page` + `PageTranslation` docs + registration + DI —
  zero behavior).

## Log

_(The first `## U1` section appears when the U01 agent ships the `Page` /
`PageTranslation` docs + registration + DI. U00 was done by the lock, not as a
fresh-agent unit, so it has no `## U0` log section here.)_

## U1 — `Page` + `PageTranslation` docs + registration + DI

**What I built** (all new, except the two one-line boot/DI additions):

- `src/Kumunita.Core/Pages/Page.cs` — the `Page` POCO. The §3.2 field set
  verbatim: `Id`, `ParentId?` (hierarchy), `Slug` (unique per parent), `Title`,
  `Body` (Markdown), `Audience?` (**the existing**
  `Kumunita.Core.Authorization.Audience` — reused, not extended; `null` =
  public), `AuthorId`, `ComponentId?`, `LanguageCode` (ADR 0018 authored-in
  tag), `MountPoint?`, `Created`, `Modified?`, `IsDraft` (ADR 0037 idiom),
  `IsDeleted` (ADR 0024 soft-delete flag — declared now so the doc shape is
  final at U01; U02 filters it, U03 sets it), `ImageIds` (RC ADR 0025),
  `AttachmentIds` (ATT ADR 0034). Every field's doc-comment names the existing
  idiom it reuses (the §3.2 provenance table).
- `src/Kumunita.Core/Pages/PageTranslation.cs` — the `PageTranslation` POCO.
  The ADR 0022/0026/0029 row shape (`Id`/`PageId`/`LanguageCode`/`Title?`/
  `Body`/`AuthorId`/`Created`), mirroring `AnnouncementTranslation` verbatim
  (add-only lane, no own audience, the standing doc-comment carries ADR 0039
  §3.7 over the ADR 0029 announcement matrix).
- `src/Kumunita.Core/Pages/PageDocTypes.cs` — **the chosen registration
  surface** (a new surface, per the plan's recommendation — a new bounded
  context gets its own `*DocTypes`, the `M1DocTypes`/`M3DocTypes`/
  `MediaDocTypes` parallel-shape precedent). Two registrations, two unique
  indexes:
  - `Page`: conventional `Id` + a **`(ParentId, Slug)`** unique index
    (auto-derived name — `mt_doc_page_uidx_parent_idslug`, well under the
    64-char limit, so no explicit name needed; the `GroupMembership`
    business-key convention; root rows are `(null, Slug)`).
  - `PageTranslation`: conventional `Id` + a **`(PageId, LanguageCode)`**
    unique index with the **explicit short name `pg_tr_uidx_page_lang`**
    (convention-consistency with the `ann_tr_uidx_ann_lang` precedent in
    `M3DocTypes`; the auto-derived name is ~48 chars so it is not *required*,
    but the repo idiom for the `<Parent>Translation` doc family is an explicit
    short name, so this one gets one too).
- `src/Kumunita.Core/Pages/IPageService.cs` — the service seam (the
  `IAnnouncementService` shape: a store-composing service kept behind an
  interface so the U04 `PageController` can be tested with NSubstitute). **An
  empty shell** — U02 adds the read lanes, U03 the write lanes. The interface
  + DI registration are the load-bearing part of U01.
- `src/Kumunita.Core/Pages/PageService.cs` — the implementation seam. **An
  empty shell** that resolves the `IDocumentStore` (the store-injection shape
  U02/U03 build on); no read or write methods yet (U02/U03).
- `src/Kumunita.Core/DependencyInjection.cs` — **one addition** in
  `AddKumunitaCore`: `services.AddTransient<Pages.IPageService>(sp => new
  Pages.PageService(sp.GetRequiredService<Marten.IDocumentStore>()))` — the
  same factory-form shape as `Posts.PostService` above (store injected from
  the host-registered `IDocumentStore`).
- `src/Kumunita.Web/Program.cs` — **one addition** in the `AddMarten` lambda,
  immediately after `MediaDocTypes.Configure(opts)`: `PageDocTypes.Configure(opts);`.
  Without this call the two new docs are invisible to Marten (the M3/Media
  precedent — the C-MED·7 drift the Media handoff flagged).

**What I verified** (the test-runner quirk in `AGENTS.md` applies — never
`dotnet test` / VS Test Explorer):

- `dotnet build Kumunita.slnx -c Debug` — **green, zero warnings** (after the
  nullable-key pragma below; see drift notes).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  — **Total: 433, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (41.3 s —
  the Testcontainers spin-up, the normal Core.Tests wall clock). The schema
  delta (the two new tables + the two unique indexes) applied idempotently
  against the live Postgres in every test fixture that boots a store with
  `M1DocTypes.Configure` + `M3DocTypes.Configure` — the existing test suite is
  the idempotency + no-regression proof for this structure unit (there is no
  dedicated U01 test yet; U02's `PG_*` family is the first behavioral test).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  — **Total: 203, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (9.4 s) —
  confirms the `Program.cs` boot-wiring addition (the `PageDocTypes.Configure`
  call in the `AddMarten` lambda) didn't regress the Web host's Marten setup.
- `git --no-pager status --short` — the change set is exactly the closed U01
  deliverable set: `M src/Kumunita.Core/DependencyInjection.cs`,
  `M src/Kumunita.Web/Program.cs`, `?? src/Kumunita.Core/Pages/`. **`M1DocTypes.cs`
  is untouched** (the `LocalizedPage` registration + its `(Slug, LanguageCode)`
  index are still there — U07 removes them), and **no `LocalizedPage` file was
  modified** (the `grep` for `LocalizedPage` in `src/` shows the same
  pre-existing references as before U01 — the `M1DocTypes` registration + the
  `ITranslationProvider.GetPageAsync` / `ILocalizationService.UpsertPageAsync`
  seams U07 retires).

**Drift from the plan** (two, both minor and recorded per protocol):

1. **The `(ParentId, Slug)` unique index is the repo's first *nullable*
   business-key column** — every existing business-key index (`GroupMembership`
   `(GroupId, UserId)`, `GuardianLink` `(GuardianId, ChildId)`,
   `PostTranslation` `(PostId, LanguageCode)`, `GroupInvitation` `(GroupId,
   UserId)`, `ComponentMembership` `(ComponentId, UserId)`, `TranslationResource`
   `(Key, LanguageCode)`) has a **non-nullable** first key, so there is no
   in-repo precedent for the nullable-key pattern. Marten's `UniqueIndex` API
   signature is `Expression<Func<T, object>>` (non-nullable `object`), so the
   nullable `ParentId` key raises CS8600 + CS8603. This is a **true false
   positive** (a null key is the *intended, valid* index key for root rows —
   `(null, Slug)`), so I suppressed it with a **tightly-scoped pragma**
   (`#pragma warning disable CS8600, CS8603` … `restore`) on exactly that one
   call, with a comment explaining why. An `(object)` cast was tried first
   and rejected — it produces the *same* two warnings (the nullable→object
   conversion is the issue, not the property type) and is noisier. **The
   semantic consequence to remember for U03:** in Postgres a regular `UNIQUE`
   index treats NULL values as *distinct*, so this index enforces
   "one page per slug under a given non-null parent" but does **not** prevent
   two roots sharing a slug. The write lane's own validation (U03's
   `CreateAsync`/`MoveAsync`) must be the authoritative guard for root slugs —
   do not rely on the index for root-level uniqueness.
2. **The `PageTranslation` index uses an explicit short name
   (`pg_tr_uidx_page_lang`) even though the auto-derived name is under the
   64-char limit** — a deliberate convention-consistency choice with the
   `ann_tr_uidx_ann_lang` precedent in `M3DocTypes` (the plan's "pick an
   explicit short name if the auto-derived name would exceed 64 chars" is a
   *necessity* trigger; I chose the explicit name anyway so the
   `<Parent>Translation` doc family reads uniformly: `ann_tr_uidx_ann_lang`,
   `pg_tr_uidx_page_lang`). The `Page` index (`(ParentId, Slug)`) uses the
   auto-derived name (no explicit name needed — it is the only non-translation
   index on the surface, and `mt_doc_page_uidx_parent_idslug` is short and
   unambiguous).

**What the next agent (U02) must know** that isn't already in the plan:

- The **two unique indexes are live in the schema** now (the Core test fixtures
  that boot a store with `M1DocTypes` + `M3DocTypes` + `PageDocTypes`
  configured — once U02's harness adds the `PageDocTypes.Configure` call to its
  `BootStoreAsync`, the same 433-test green run becomes the U02 structural
  gate; the `LocalizedPage` surface is **still registered** alongside
  `Page`/`PageTranslation` — both coexist until U07 retires
  `LocalizedPage`).
- **The `PageDocTypes.Configure` call is in `Program.cs` only, not in
  `SchemaBootstrap`** — `SchemaBootstrap.ApplyAsync` calls
  `ApplyAllConfiguredChangesToDatabaseAsync` against the store registered by
  `Program.cs`'s `AddMarten` lambda, so the doc types flow through the same
  store; there is no second registration site to update (the M3/Media
  precedent — both are `Program.cs`-only too).
- **The `IPageService`/`PageService` are empty shells** — U02 adds the read
  lanes + the `PageToAuditableResource` adapter + the standing matrix; U03
  adds the write lanes. The `PageService` constructor already resolves the
  `IDocumentStore` (the store-injection shape is wired); U02 will add the
  `IAuthorizationService` + `IUserInfoService` constructor params (the
  `AnnouncementService(IDocumentStore, IUserInfoService)` shape) when the
  `CanSeeAsync`-filtered read lanes land.
- **`IsDeleted` is declared but not yet acted on** — U02's `CanSeeAsync` filter
  must exclude `IsDeleted` pages from `GetTreeAsync`/`GetByPathAsync`/
  `GetByMountPointAsync`; U03's `DeleteAsync` sets it. The doc shape is final
  at U01 so these two units don't drift the field set.
- **`MountPoint` is a plain nullable `string`** (not an enum, not a doc
  reference) — the resolver (U02's `GetByMountPointAsync(slot)`) is a string
  equality match. The two known slots are `"footer/community"` and
  `"help/account"`; U04's mount-point resolver in the layout reads these.
- **The `Audience` field's null-ability is load-bearing** — `Page.Audience`
  is `Authorization.Audience?` (nullable, unlike `Post.Audience` which is
  `null!` non-nullable with the C1 empty-audience-denies invariant). U02's
  `PageToAuditableResource` adapter must pass `Audience` (null allowed) to the
  frozen `IAuthorizationService.CanAsync` — the `Decide()` branch 5
  (`Audience == null` → public) is the public-page path. Do not accidentally
  make it non-nullable (the `Post` shape) — that would break the "pages
  default public" decision (ADR 0039).

**Status: U01 GREEN.** Build clean (zero warnings), Core tests 433/433 green,
Web tests 203/203 green, `LocalizedPage`/`M1DocTypes` untouched. **Next unit:
U02** (the read lanes + `PageToAuditableResource` + the standing matrix —
the first behavioral unit of the lane).

## U2 — `PageService` read lanes + `PageToAuditableResource` + the standing matrix

**What I built** (all in the `Kumunita.Core.Pages` context; no Web code — U04):

- `src/Kumunita.Core/Pages/PageToAuditableResource.cs` (new) — **the adapter**
  (the only new authorization surface, ADR 0039 §3.4). Mirrors
  `Posts.PostToAuditableResource` verbatim: `Id` = `Page.Id`; `Name` =
  `Page.Title` or a 60-char-truncated `Page.Body` (the same `57 + "..."`
  fallback); `OwnerId` = `Page.AuthorId`; `Audience` = `Page.Audience`
  (**null allowed** — the one place pages differ from posts; `Post`'s is
  `null!`); `ComponentId` = `Page.ComponentId`; `TargetKind` = `"page"`.
  `sealed`. Compiles against the **frozen** `IAuthorizationService`
  (ADR 0006 §A) with **no** signature change — no new `AccessAction`, no new
  `AccessVia`, no new `Decide()` branch.
- `src/Kumunita.Core/Pages/IPageService.cs` (modified) — added the five read-lane
  signatures: `GetByPathAsync(string)`, `GetBySlugUnderParentAsync(string?,
  string)`, `GetTreeAsync()`, `GetTranslationsAsync(string)`,
  `GetByMountPointAsync(string)` (returns `Page?`). The U03 write lanes are a
  comment placeholder (unchanged).
- `src/Kumunita.Core/Pages/PageService.cs` (modified) — the implementation.
  - **Read lanes:** `GetByPathAsync` (walks the `(ParentId, Slug)` chain
    root→leaf; `KeyNotFoundException` on a missing segment);
    `GetBySlugUnderParentAsync` (one level; `KeyNotFoundException` on absent);
    `GetTreeAsync` (**`IsDeleted` filtered here** — the ADR 0024 soft-delete
    read filter lives with the read, not the write, per the plan);
    `GetTranslationsAsync` (loads the page first, `KeyNotFoundException` if
    absent; rows ordered by `LanguageCode`); `GetByMountPointAsync` (string
    equality match on `MountPoint`, `IsDeleted` filtered, returns `null` when
    the slot is unmounted or empty — a display concern, not a 404).
  - **Standing-matrix gate helpers** (`CheckCreateStanding` /
    `CheckEditStanding` / `CheckTranslateStanding`) — **pure / static** role-claim
    checks (the `AnnouncementService` C3 server-side re-check shape, but
    store-free so they are directly testable and callable from any U03 write
    lane). A `null` page → `KeyNotFoundException` (404); a denied actor →
    `UnauthorizedAccessException` (403). Reuses the **existing** role claims
    (`Roles.GlobalAdmin` / `Roles.Translator` / `Roles.ModeratorComponent`) —
    no new ADR needed, so **no drift-pause (a)**.
  - **Hierarchy guards** (`GetDepthAsync` / `EnsureNoCycleAsync` /
    `EnsureDepthWithinLimitAsync`, `MaxDepth = 8`) — DB-backed (Marten has no
    FK, so the guard is the service's). Exercised now (U02) so the guard is
    pinned before U03's `MoveAsync`/`CreateAsync` call them.
- `tests/Kumunita.Core.Tests/PageServiceTests.cs` (new) — **the `PG_*` family
  (42 tests)** mirroring the `A0036_*` / `PostServiceTests` harness shape:
  (1) the adapter projection (3); (2) the frozen `Decide()` branches through a
  `Page` target — null-audience-public, `Community` flag+member, grants, owner
  (5); (3) `GetByPath` root→leaf / absent / deleted-segment (3); (4)
  `GetBySlugUnderParent` resolved / absent (2); (5) `GetTree`
  IsDeleted-filtered / all-live (2); (6) `GetTranslations` ordered / missing /
  empty (3); (7) `GetByMountPoint` resolved / unmounted / deleted (3); (8) the
  standing matrix — create/edit/translate × GlobalAdmin / Moderator / Member,
  incl. the flat-page denial + the null-page 404 (16); (9) the hierarchy
  guards — cycle (self / descendant / sibling) + depth-cap (within / exceeds)
  + `GetDepth` (7).

**What I verified** (the test-runner quirk in `AGENTS.md` applies — never
`dotnet test` / VS Test Explorer):

- `dotnet build Kumunita.slnx -c Debug` — **green, zero warnings**.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  — **Total: 475, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (45.2 s).
  U01's run was **433**; the delta is exactly the **42 new `PG_*` tests**
  (433 + 42 = 475) — the full existing suite stayed green, so the U01
  `Page`/`PageTranslation` schema + the U02 read/standing/guard code introduced
  no regression.
- **Adapter against the frozen seam:** `PageToAuditableResource` references
  only `IAuditableResource` + `Authorization.Audience` (both pre-existing) and
  adds **no** `AccessAction` / `AccessVia` / `Decide()` branch — confirmed by
  the `PG_NullAudience_Stranger_Reads_Public` (branch 5),
  `PG_CommunityFlagAndMember_Allows_ViaCommunity` (branch 4),
  `PG_GrantsBranch_UserGrant_Allows` (branch 6), and
  `PG_GrantsBranch_OwnerBranchStillAllows` (branch 1) tests all passing through
  the real `AuthorizationService`.

**Drift from the plan** (two, both minor and recorded per protocol):

1. **Key type is `string`, not `Guid`.** The plan's read-lane signatures read
   `GetBySlugUnderParentAsync(Guid? parentId, …)` /
   `GetTranslationsAsync(Guid pageId)`, but the U01 handoff note (and the U01
   `Page`/`PageTranslation` POCOs) established the **`string Id`** Marten
   conventional-identity convention for this context (the M3 "string Id"
   convention). I followed the **implemented** doc shape (`string` /
   `string?`), which the U01 note already flagged as the correction to the
   plan's `Guid` wording. Consistent, no behavior difference.
2. **The standing helpers are `static` and take `(actorId, actorRoles, page)`**
   rather than an instance method on `(actor, page)`. The plan's
   `CheckEditStanding(actor, page)` etc. are a *server-side* re-check, so the
   helper needs the actor's **role claims** (the `IReadOnlySet<string>`), not
   just an id. Making them `static` keeps them store-free and pure (directly
   testable, callable from any U03 write lane), which is the
   `AnnouncementService` C3 re-check shape minus the store. The U03 write lanes
   will call `PageService.CheckEditStanding(actorId, actorRoles, page)` and
   throw the 403/404 it raises. This is the *intended* U03 call shape, recorded
   here so U03 does not re-decide it.

**What the next agent (U03) must know** that isn't already in the plan:

- **The `PageService` constructor is `PageService(IDocumentStore store)`** —
  **one param only.** The standing helpers are `static` (no store) and the read
  lanes need no `IUserInfoService`/`IAuthorizationService` (the read decision
  is made by the **Web layer** through the `PageToAuditableResource` adapter,
  not inside `PageService`). The U01 handoff note's expectation that U02 would
  add `IUserInfoService`/`IAuthorizationService` constructor params **did not
  materialize** — U02's read lanes are pure document reads, so the frozen seams
  stay out of `PageService`. U03's write lanes add their own audit-write path
  (the `AnnouncementService` C3 caller-session `AccessAudit` row) and can
  compose `IAuthorizationService`/`IUserInfoService` **in U03** if it needs them.
- **`IsDeleted` filtering lives in the U02 read lanes**
  (`GetTreeAsync` / `GetByPathAsync` / `GetByMountPointAsync` / the private
  `LoadByParentAndSlugAsync`), **not** in a `CanSeeAsync` call. The `Page`
  doc's `IsDeleted` flag is set by U03's `DeleteAsync`. Do not duplicate the
  filter in U03's `Get*` read paths — it is already here.
- **`MaxDepth = 8`** is a `public const` on `PageService`. `GetDepthAsync`
  (root = depth 1) / `EnsureNoCycleAsync` (walk up from the new parent; self- or
  descendant-parent ⇒ `InvalidOperationException`) /
  `EnsureDepthWithinLimitAsync` (`GetDepthAsync(parent) + 1 > MaxDepth` ⇒
  `InvalidOperationException`) are the **write-lane guard seams** — U03's
  `CreateAsync`/`MoveAsync` call them before committing. The depth-cap test
  boundary: a chain of 8 pages (depth 1..8) **exceeds** when a new node is
  placed under the depth-8 node (would be depth 9); a chain of 7 allows a
  depth-8 child.
- **The `(ParentId, Slug)` unique index does NOT prevent two roots sharing a
  slug** (the U01 drift note — Postgres treats NULLs as distinct). U03's
  `CreateAsync`/`MoveAsync` must be the authoritative root-slug guard.
- **`GetByMountPointAsync` returns `Page?`** (`null` = unmounted), not
  `Page` — the Web layer (U04) skips the link on `null`. The two known slots
  are `"footer/community"` and `"help/account"`.
- **No `LocalizedPage`/`M1DocTypes` was touched** (U07 retires it), and **no
  Web code** was added (U04).

**Status: U02 GREEN.** Build clean (zero warnings), Core tests **475/475** green
(433 pre-existing + 42 new `PG_*`), the adapter compiles against the **frozen**
`IAuthorizationService` with no signature change, `LocalizedPage`/`M1DocTypes`
untouched, no Web code. **Next unit: U03** (the write lanes
`CreateAsync`/`UpdateAsync`/`PublishAsync`/`MoveAsync`/`DeleteAsync`/
`AddTranslationAsync`, each with its C3 `AccessAudit` row, `TargetKind =
"page"` — reusing the U02 standing helpers + hierarchy guards).

## U3 — write lanes + C3 audit

**What I built** (all in `Kumunita.Core.Pages` + the U02 test file; no Web
code — U04, no `LocalizedPage`/`M1DocTypes` touched — U07):

- `src/Kumunita.Core/Pages/IPageService.cs` (modified) — added the six
  write-lane signatures (the C3 caller-session `IDocumentSession` shape, the
  `AnnouncementService.CreateAsync` / `PostService.CreatePostAsync` convention):
  `CreateAsync(Page, actorId, actorRoles, session)`, `UpdateAsync(Page updated,
  actorId, actorRoles, session)`, `PublishAsync(pageId, actorId, session)`
  (author-only — no `actorRoles` param, the ADR 0037 pin), `MoveAsync(pageId,
  newParentId, newSlug, actorId, actorRoles, session)`, `DeleteAsync(pageId,
  actorId, actorRoles, session)` (soft-delete), and `AddTranslationAsync(pageId,
  languageCode, title, body, actorId, actorRoles, session)`. Added the `using
  Marten;` for `IDocumentSession`. The U02 read-lane signatures + the standing
  helpers' "not on the interface" comment are unchanged.

- `src/Kumunita.Core/Pages/PageService.cs` (modified) — the six write-lane
  implementations + the two **new** private standing resolvers the move/delete
  and translation audit rows need (distinct from U02's `Check*Standing`
  helpers, which throw — the resolvers return the `AccessVia` tag for the
  audit row). **The `PageService` constructor is unchanged**
  (`PageService(IDocumentStore store)` — one param; the write lanes compose
  the audit row directly in the caller's `IDocumentSession`, the C3 shape;
  no new constructor params were needed). The write lanes:
  - **`CreateAsync`** — `CheckCreateStanding` (U02) → **root-slug guard**
    (the `(ParentId, Slug)` unique index does NOT cover it, the U01 drift
    note) → mint id, set `AuthorId = actorId`, `Created = now`, normalize
    `ImageIds`/`AttachmentIds` via `?? []` (RC ADR 0025 / ATT ADR 0034 — the
    caller parses the body, Core normalizes the POCO's fields) → store the
    `page.create` `AccessAudit` row (`TargetKind = "page"`, `Via = Admin` for
    GlobalAdmin / `Moderator` for a component-moderator) in the caller's
    session → `SaveChangesAsync` → return the page.
  - **`UpdateAsync`** — load the stored page (404 on missing) →
    `CheckEditStanding` (U02, author/admin/mod) → **capture the stored
    `AuthorId` + `ComponentId` before the field-copy** (the standing decision
    and the audit `Via` tag are based on the resource the actor has standing
    over — the stored page — not the incoming `updated`, which may carry a
    null `ComponentId` on a content-only edit) → copy
    `Title`/`Body`/`Audience`/`ComponentId`/`LanguageCode`/`MountPoint`/
    `ImageIds`/`AttachmentIds` → store the `page.update` `AccessAudit` row
    (`Via = Owner` if the actor is the stored author, else `Moderator` /
    `Admin`) → stamp `Modified = now` → `SaveChangesAsync` → return the page.
  - **`PublishAsync`** — load (404) → **author-only gate** (ADR 0037 pin:
    `AuthorId == actorId` ordinal; a non-author is denied **even at
    GlobalAdmin** — the ADR 0037 pin) → idempotent (a second publish on an
    already-live page does not stamp `Modified`) → set `IsDraft = false` →
    store the `page.publish` `AccessAudit` row (`Via = Owner`, the ADR 0037
    author-pin) → `SaveChangesAsync` → return the page.
  - **`MoveAsync`** — load (404) → **admin/mod only** (the new
    `ResolveMoveDeleteStanding` resolver — *not* `CheckEditStanding`, which
    allows the author; §3.7: move is platform content, not a personal note)
    → `EnsureNoCycleAsync` + `EnsureDepthWithinLimitAsync` (the U02 hierarchy
    guards) → **root-slug guard** (if the move makes the page a root and a
    `newSlug` is specified, no other root page may already carry it) → set
    `ParentId` / `Slug` (the derived path is rewritten by the single-column
    write — paths are not stored) → store the `page.move` `AccessAudit` row
    (`Via = Admin` / `Moderator`) → `SaveChangesAsync` → return the page.
  - **`DeleteAsync`** — load (404) → **admin/mod only** (same resolver) → set
    `IsDeleted = true` (soft-delete, the ADR 0024 shape — the row is NOT
    removed, children are NOT orphaned; the U02 read lanes filter the flag)
    → store the `page.delete` `AccessAudit` row (`Via = Admin` / `Moderator`)
    → `SaveChangesAsync`.
  - **`AddTranslationAsync`** — load the page (404) → `CheckTranslateStanding`
    (U02, admin/translator/mod) → mint the `PageTranslation` row → store the
    `page.translation.add` `AccessAudit` row (`Via = Admin` for GlobalAdmin /
    Translator, `Moderator` for a component-moderator) → `SaveChangesAsync`
    → return the row. The `(PageId, LanguageCode)` unique index (U01) is the
    add-only duplicate guard — a second add of the same pair is rejected by
    the DB (Marten wraps it in a Postgres exception).
  - **`ResolveMoveDeleteStanding`** (new, private static) — the move/delete
    standing resolver (§3.7): `Moderator` if the actor holds a
    `ModeratorComponent(page.ComponentId)` claim (most specific first), else
    `Admin` if GlobalAdmin, else `null` (deny). A flat/public page
    (`ComponentId` null) has no community to moderate, so only GlobalAdmin
    qualifies. Distinct from `CheckEditStanding` (which allows the author) —
    move/delete are admin/mod-only per §3.7.
  - **`ResolveTranslationStandingVia`** (new, private static) — the
    translation standing `Via` tag: `Moderator` if component-scoped, else
    `Admin` (GlobalAdmin or Translator — both map to `Admin`). The
    *decision* is still `CheckTranslateStanding` (U02); this is only the
    audit tag.
  - **`ResolveWriteStandingVia`** (new, private static) — the create/edit
    standing `Via` tag (narrowest standing first): `Owner` if the actor is
    the author (edit-lane only — a create has no stored author), else
    `Moderator` (component-scoped), else `Admin` (GlobalAdmin). The caller
    has already verified the standing before calling this — it is a pure
    tag-resolution, not a gate.

- `tests/Kumunita.Core.Tests/PageServiceTests.cs` (modified) — **the `PG3_*`
  family (30 tests)** added after the U02 `PG_*` family, mirroring the
  `AnnouncementServiceTests` write-lane harness shape (fresh scratch Postgres
  per test, `Plant` seeding, the caller's in-flight `IDocumentSession` via
  `newSession(store)`, the `AccessAudit` read-back via `AuditRows(store)` /
  `AuditsFor(store, targetId, action)`):
  - **Create** (5): GlobalAdmin → `Admin` audit; CommunityModerator →
    `Moderator` audit; PlainMember denied (no row); root-slug collision
    (`InvalidOperationException`); `ImageIds`/`AttachmentIds` non-null after
    save (the `?? []` guard).
  - **Update** (5): Author → `Owner` audit; GlobalAdmin → `Admin` audit;
    CommunityModerator → `Moderator` audit (the bug I caught — the `Via`
    must be resolved from the **stored** page's `ComponentId`, not the
    incoming `updated` which may carry a null `ComponentId`); non-author
    Member denied; missing page → `KeyNotFoundException`.
  - **Publish** (4): Author → `IsDraft` cleared, `Owner` audit; GlobalAdmin
    denied (ADR 0037 pin — *not* the author); Moderator denied; already-live
    → idempotent (no `Modified` stamp).
  - **Move** (5): GlobalAdmin re-parents → `Admin` audit; Author denied
    (not the author lane); CommunityModerator → `Moderator` audit; moving
    under a descendant → cycle-guard throws; moving under a depth-8 node →
    depth-cap throws.
  - **Delete** (4): GlobalAdmin → `IsDeleted = true`, hidden from
    `GetByPathAsync` + `GetTreeAsync`, `Admin` audit; Author denied (not the
    author lane); CommunityModerator → `Moderator` audit; missing page →
    `KeyNotFoundException`.
  - **AddTranslation** (6): GlobalAdmin → `Admin` audit; Translator →
    `Admin` audit; CommunityModerator → `Moderator` audit; PlainMember
    denied; flat-page + moderator-of-other-comp denied; duplicate
    `(PageId, LanguageCode)` → DB unique-index rejection
    (`ThrowsAnyAsync<Exception>`); missing page → `KeyNotFoundException`.
  - **Helpers added**: `newSession(store)` (the
    `AnnouncementServiceTests.newSession` shape), `AuditRows(store)` (all
    `AccessAudit` rows), `AuditsFor(store, targetId, action)` (the filtered
    "assert the audit-row shape" helper).

**What I verified** (the test-runner quirk in `AGENTS.md` applies — never
`dotnet test` / VS Test Explorer):

- `dotnet build Kumunita.slnx -c Debug` — **green, zero warnings** (after
  fixing the two xUnit analyzer issues: the un-awaited
  `Assert.ThrowsAsync` in the delete test, and the missing
  `CancellationToken` on `CountAsync`; and the CS8625 nullable warning in
  the `ImageIds`/`AttachmentIds` test — simplified to verify non-null after
  save rather than assigning `null` to a non-nullable POCO field).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  — **Total: 505, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (45.6 s).
  U02's run was **475**; the delta is exactly the **30 new `PG3_*` tests**
  (475 + 30 = 505) — the full existing suite stayed green, so the U03 write
  lanes + audit rows introduced no regression.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  — **Total: 203, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (10.3 s) —
  confirms the U03 changes (Core-only) did not regress the Web host.
- `git --no-pager status --short` — the change set is exactly the closed U03
  deliverable set: `M src/Kumunita.Core/Pages/IPageService.cs`,
  `M src/Kumunita.Core/Pages/PageService.cs`,
  `M tests/Kumunita.Core.Tests/PageServiceTests.cs`. **`LocalizedPage`/
  `M1DocTypes` untouched** (U07 retires it), and **no Web code** was added
  (U04).

**Drift from the plan** (three, all minor and recorded per protocol):

1. **The `ImageIds`/`AttachmentIds` "server-side parse" is the
   caller-parses-and-normalizes shape, not a Core-side regex parse.** The
   plan's deliverable #7 ("Server-side body parse — `ImageIds` /
   `AttachmentIds` derived from the `Body` in the write lane") reads as if
   Core calls the regex helper. The **actual** repo idiom (the
   `PostService.CreatePostAsync` / `AnnouncementService.UpdateAsync`
   shape, the `RC R·3` / `ATT U4` comments) is that the **Web layer**
   (the `ContentImageIds.ExtractContentImageIds` /
   `AttachmentIds.ExtractAttachmentIds` helpers in
   `Kumunita.Web.Security`) parses the body and sets the POCO's fields
   before calling the Core service; Core then **normalizes** the POCO's
   fields via `?? []` (null-coalesce to the non-null empty list). I
   followed the implemented idiom (the POCO's fields are already set by the
   caller; Core's `?? []` is the defensive null-coalesce). The U04
   `PageController` will call the Web-layer parse helpers and set
   `page.ImageIds` / `page.AttachmentIds` before calling
   `CreateAsync`/`UpdateAsync` — the Core write lanes do **not** parse the
   body. **This is not a drift-pause (a) — it is the existing idiom, not a
   new standing or branch.**
2. **The `UpdateAsync` audit `Via` tag is resolved from the *stored* page's
   `AuthorId`/`ComponentId`, not the incoming `updated` POCO.** The test
   `PG3_Update_ByCommunityModerator_Allows_WithModeratorAudit` caught a
   real bug: if the incoming `updated` carries a null `ComponentId` (a
   content-only edit that doesn't re-state the component scope), and Core
   copies `existing.ComponentId = updated.ComponentId` *before* resolving
   the `Via` tag, the `Via` would be `Admin` (GlobalAdmin fallback) instead
   of `Moderator` (component-scoped). The fix: capture
   `storedAuthorId`/`storedComponentId` **before** the field-copy, and
   resolve the `Via` from those. The standing decision itself
   (`CheckEditStanding`) was already correct (it runs against the stored
   page before the copy); only the audit tag needed the fix. Recorded here
   so U04's `PageController` does not re-introduce the bug.
3. **The `PageService` constructor did NOT gain
   `IUserInfoService`/`IAuthorizationService` params.** The U02 handoff
   note said "U03's write lanes … can compose
   `IAuthorizationService`/`IUserInfoService` **in U03** if it needs
   them." The U03 write lanes **do not need them** — the standing
   re-check is a pure role-claim check (U02's static helpers), and the
   audit row is written directly in the caller's `IDocumentSession` (the
   C3 shape, the `AnnouncementService` precedent — no separate
   `IAuthorizationService.CanAsync` call needed for the write-lane audit;
   the read decision is the Web layer's job through the adapter). The
   constructor stays `PageService(IDocumentStore store)` — one param. This
   is consistent with the U02 note's "if it needs them" (it didn't).

**What the next agent (U04) must know** that isn't already in the plan:

- **The `PageService` constructor is `PageService(IDocumentStore store)`** —
  **one param only**, unchanged from U02. The write lanes do not need
  `IUserInfoService`/`IAuthorizationService` (the standing re-check is a
  pure role-claim check, the audit row is written in the caller's session).
  The U04 `PageController` resolves `IPageService` from DI (the
  `DependencyInjection.cs` registration is unchanged from U01) and passes
  the caller's `IDocumentSession` to the write lanes (the C3 shape — the

---

## U4 — PageController + tree + post view + composer

**What was built (all under `src/Kumunita.Web/` + `tests/Kumunita.Web.Tests/`; `Kumunita.Core` untouched, verified by `git status`):**

- `Controllers/PageController.cs` — the six route groups (tree browse,
  post view, composer GET/POST, edit GET/POST, publish, delete, move).
- `Security/PagePaths.cs` — pure `Derive`/`Href` (the `Page` → derived-path
  projection, 64-step cycle guard; Web-only, no store / HTTP / authorization).
- `Security/PageMountResolver.cs` — `ResolveAsync(IPageService, string slot)
  → Task<(string Href, string Title)?>`; null when unmounted / slot blank /
  mounted page absent from the live tree; runs NO access decision.
- `Models/PageViewModels.cs` — `PageNode`, `PageTreeViewModel`,
  `PageShowViewModel`, `PageComposeViewModel` (`IsPublic = true` default,
  `[BindNever]` pickers, `IsValid`).
- `Views/Pages/{Index,Show,New,Edit,_PageForm}.cshtml` — the composer (New)
  and edit (Edit) **share `_PageForm.cshtml`** (one editor, one renderer);
  Index flattens the forest to a `List<(PageNode, int Depth)>` and renders one
  `foreach` (a `@functions { async Task ... }` block with HTML does not compile).
- `Views/Shared/_MountSlot.cshtml` — slot-generic partial; the layout's footer
  wires `{ ViewData["MountSlot"] = "footer/community"; }` +
  `<partial name="_MountSlot" />` (replacing the old `/about` link).

**Locked decisions (recorded here so U05+ does not re-derive them):**

- **Slug = `Slugify(Title)` server-side; the slug is NOT a form field** — the
  path is derived from the `(ParentId, Slug)` chain (ADR 0039 §3.2/§3.3), never
  stored from a POST. A non-ASCII-only title falls back to the slug `"page"`.
- **`IsPublic = true` → `Audience = null` AND `ComponentId = null`** (the public
  shape — no audience object, no community scope). `IsPublic = false` →
  `model.Audience.BuildAudience()` + the posted `CommunityId` (the ADR 0036
  single-source: the editor is the ONLY deserialization site).
- **The draft gate is a Web-layer pin** (a non-author never sees a
  `Page.IsDraft` page — it is filtered from the tree browse AND 403s on the
  post view). It runs *before* the audience `Read` decision; the `Read`
  decision (via `PageToAuditableResource` + the frozen
  `IAuthorizationService.CanAsync`/`CanSeeAsync`) is the audience pin. The two
  are distinct and both run.
- **404 vs 403 are DISTINCT and MUST NOT collapse** (the page-specific split,
  unlike posts/announcements which 404 both): `GetByPathAsync` KNE → **404**;
  page-exists-but-`CanAsync(Read)`-deny / draft-not-author / anonymous-non-public
  → **403**. `UnauthorizedAccessException` from a write lane → **403** (never
  folded into a 404).
- **Anonymous non-public → `ForbidResult` (403)** — not a 404 (the page exists;
  the caller has no standing).
- **`[Authorize(Roles = "GlobalAdmin,Moderator")]` is SAFE** on New/Edit/Move/
  Delete (the role claim type is `"Kumunita.Role"` at identity mint; component
  moderators carry base `Moderator` + `moderator:{componentId}`). Publish is
  plain `[Authorize]` (author-only, re-checked by the service — ADR 0037, NO
  `actorRoles` param).
- **Edit GET loads via `store.QuerySession().LoadAsync<Page>(id)`** (the read
  session, the `AnnouncementController` idiom); **Edit POST loads the existing
  row via `store.LightweightSession().LoadAsync<Page>(id)`** (the write
  session, then `UpdateAsync` in a second `LightweightSession`). Both use the
  **no-CT** overload (Marten's `LoadAsync` has an *optional* CT — a single
  method; the tests stub `Arg.Any<CancellationToken>()` which matches both and
  satisfies xUnit1051).
- **ImageIds/AttachmentIds are set Web-side before the write lane** (the U03
  invariant): `ContentImageIds.ExtractContentImageIds` / `AttachmentIds
  .ExtractAttachmentIds` scan the body for **hex** route-shaped ids
  (`/content-image/{hex}` / `/attachment/{hex}`, 1–128 hex); Core normalizes
  `?? []` and never parses the body.

**What is unit-tested (NSubstitute, no live Postgres) vs live-run verified:**

- Unit-tested: the 404≠403 split (absent / denied / draft-gate), the tree
  browse filter (a denied page is *absent*, not blanked; a non-author's draft
  is absent), the mount resolver (mounted / unmounted / soft-deleted-mounted),
  the composer audience round-trip (IsPublic=true → null audience + null
  component; IsPublic=false → `BuildAudience` output + community; the
  `ImageIds`/`AttachmentIds` extraction before `CreateAsync`), and the standing
  re-check (Delete UAAE → 403, Delete KNE → 404, Publish UAAE → 403).
- Live-run only (not unit-pinned here): the `_PageForm` render, the
  `_MountSlot` partial in the footer, the `MarkdownRenderer` over the body, and
  the ADR 0027 chip-swap across `PageTranslation` rows — these exercise the
  view layer / DI / Marten read path and are exercised by the running app, not
  by the NSubstitute harness.

**Drifts / notes for U05 (StaticPagesController retarget):**

- The **`help/account` mount slot has NO existing Web surface** — the layout
  only wires `footer/community`. Do **NOT** invent a `help/account` page or
  surface for U05; if a design asks for one, that is a NEW UI surface and a
  drift-pause trigger.
- `StaticPagesController.cs` is **untouched** (U05 retargets it onto this
  `Page` surface). `_AudienceEditor.cshtml` is untouched (the page composer
  uses `_GrantPickers` + inline radios instead — the page's audience editor is
  a distinct, simpler shape than the profile's two-audience editor).
- **NSubstitute gotcha (cost one build cycle):** for `Task<T>`-returning lanes
  (`CreateAsync`/`UpdateAsync`/`PublishAsync`) a multi-parameter lambda
  (`.Returns((T p, string a, ...) => ...)`) does **not** bind — use the
  `call => { var p = call.ArgAt<T>(0); ...; return Task.FromResult(p); }` form.
  And `Task.FromException` needs the generic parameter
  (`Task.FromException<T>(...)`) to match a `Task<T>` return.
- `AccessVia` has **no `None`/`Public`** members (use `Audience`/`Community`);
  `Audience.Mode` is the `AudienceMode` **enum** (not a string) and
  `Audience.Grants` is `List<AudienceGrant>` (record `(GrantKind, string)`),
  while `AudienceEditorModel.Mode` **is** a string ("Any"/"All"). `Page` is a
  **sealed class** (no `with` expressions). `Kumunita.Core.Identity.ClaimTypes`
  is ambiguous with `System.Security.Claims.ClaimTypes` in the test file —
  fully-qualify it.

**Exit-gate status (this unit):** (1) `dotnet build Kumunita.slnx -c Debug`
green, zero warnings — ✅; (2) `dotnet exec ...Kumunita.Web.Tests.dll` green
— 218/218 (203 baseline + 15 PageControllerTests) — ✅; (3) `LocalizedPage` /
`M1DocTypes` / `Kumunita.Core` untouched — ✅ (verified by `git status`); (4)
this handoff section appended — ✅.
  Web layer's `DocumentStore.LightweightSession()`).
- **The `ImageIds`/`AttachmentIds` are set by the Web layer before calling
  the write lanes** (the `ContentImageIds.ExtractContentImageIds` /
  `AttachmentIds.ExtractAttachmentIds` idiom — `Kumunita.Web.Security`).
  Core's write lanes normalize them via `?? []` but do **not** parse the
  body. The U04 composer must call the parse helpers and set the POCO's
  fields before calling `CreateAsync`/`UpdateAsync`.
- **The `UpdateAsync` audit `Via` is resolved from the stored page's
  `AuthorId`/`ComponentId`, captured before the field-copy.** Do not
  re-introduce the bug (resolve `Via` from `existing.ComponentId` *after*
  `existing.ComponentId = updated.ComponentId` — that's wrong; the `Via`
  must be based on the resource the actor has standing over, the stored
  page).
- **`MoveAsync`/`DeleteAsync` are admin/mod-only, NOT author** (the
  `ResolveMoveDeleteStanding` resolver, not `CheckEditStanding`). A
  `PageController` route `[Authorize(Roles = GlobalAdmin)]` is a
  convenience pre-gate, not the source of truth — the service re-checks
  standing server-side.
- **`PublishAsync` is author-only (ADR 0037)** — a GlobalAdmin cannot
  publish someone else's draft. The `PageController` route should be
  `[Authorize]` (any authenticated user) — the service enforces the
  author-only gate.
- **The soft-delete filter lives in the U02 read lanes** — U04's
  `GetByPathAsync` / `GetTreeAsync` / `GetByMountPointAsync` already filter
  `IsDeleted` pages out. U03's `DeleteAsync` only sets the flag. Do not
  duplicate the filter in U04.
- **The `(ParentId, Slug)` unique index does NOT prevent two roots sharing
  a slug** (the U01 drift note). U03's `CreateAsync`/`MoveAsync` are the
  authoritative root-slug guard. U04's composer should surface a clear
  error if the user tries to create a root page with a slug that already
  exists at the root level.
- **No `LocalizedPage`/`M1DocTypes` was touched** (U07 retires it), and
  **no Web code** was added (U04).

**Status: U03 GREEN.** Build clean (zero warnings), Core tests
**505/505** green (475 pre-existing + 30 new `PG3_*`), Web tests
**203/203** green (no regression), `LocalizedPage`/`M1DocTypes`
untouched, no Web code. **Next unit: U04** (the `PageController` + the
tree browse + the post view + the composer — the Web surface).

## U5 — Reference-from-UGC + the seeded default pages + the `/about` retarget

**What I built** (closed set of three deliverables; two `src` files + test
files):

- `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (modified) — **the absorb
  seed (deliverable #2).** The step-5 lane now writes the **new** `Page` docs
  for the seeded default pages, in the **same atomic session** as the legacy
  `LocalizedPage` rows (one store transition, not two), then a single
  `SaveChangesAsync`.
  - `EnDefaultPages()` flipped `private` → **`public`** so the Core test can
    read the seeded set without an `InternalsVisibleTo` (the repo's Core-test
    constraint — only `public` members are reachable across the assembly
    boundary). Returns the closed `(Slug, Title, Body)[]` for **`terms` +
    `help`** (the exact original body text — the single source the legacy rows
    and the new `Page` docs both carry, so `/terms` + `/help` render
    byte-identically from either store).
  - **`SeedDefaultPagesAsync(IDocumentSession, IReadOnlyList<(Slug,Title,Body)>,
    DateTimeOffset, CancellationToken)`** added as a **`public static`**
    seam (extracted from the inline loop) — the idempotent root-`Page`
    upsert (query by `(Slug, ParentId == null)`; store a new root with
    `Audience = null` / `LanguageCode = "en"` / `AuthorId = ""` /
    `ParentId = null`, or refresh `Title`/`Body` in place). `SeedTranslationResourcesAsync`
    calls it within its existing session, so the new `Page` docs and the
    legacy `LocalizedPage` rows commit atomically.
- `src/Kumunita.Web/Controllers/StaticPagesController.cs` (modified) — **the
  retarget (deliverable #3).** Constructor gains `IPageService pages` as the
  first param (`StaticPagesController(IPageService, ITranslationProvider,
  IOptions<CommunityOptions>)`). Each route (`/terms`/`/help`/`/about`) is now
  **tree-first**: try `IPageService.GetByPathAsync(slug)` (catch
  `KeyNotFoundException` → absent); on a hit, project the `Page` to a
  throwaway `LocalizedPage` (Title/Body/`Updated = Modified ?? Created`) and
  `View("Page", …)` — the **same** single view + `MarkdownRenderer` as today,
  so a tree-present page renders byte-identically (no new view file). On a
  miss, fall through to the legacy `ITranslationProvider.GetPageAsync` (the
  `LocalizedPage` store, **retired in U07** — the "absorb, don't yank"
  contract); then for `/about` the existing product-story
  `View("About", HomeViewModel)` fallback, else `NotFound`.
- `tests/Kumunita.Web.Tests/PageControllerTests.cs` (modified) — **deliverable
  #1 (the `PG5_*` Web tests).** Two new `PG5_*` tests pinned after the 404-vs-403
  split: (a) `PG5_ReferenceFromUgc_LinkInAuthorizedPost_RendersFree` — confirms
  the frozen `MarkdownRenderer.RenderHtml("[About](/pages/about) …")` yields a
  live `<a href="/pages/about">About</a>` (the "link present" half is free and
  does not depend on the target's audience — **confirm, not re-implement**);
  (b) `PG5_ReferenceFromUgc_LinkPresentButTargetDenied_Forbid_NotAllowed` — a
  caller who can read an authorized post containing the link opens
  `/pages/about` and gets a `ForbidResult` (the target's `Read` decision is
  **separate** from the link's presence; "I see a link to it" ≠ "I may open it").
- `tests/Kumunita.Web.Tests/StaticPagesControllerPgTests.cs` (new) — the
  **retarget** tests: (1) tree-present → `Page` view with the projected model
  (Title/Body/`Updated`) and the legacy provider **not consulted** (one store,
  not two); (2) tree-absent + legacy-present → the legacy `LocalizedPage`
  renders verbatim (the absorb, don't yank); (3) tree-absent + legacy-absent on
  `/about` → the product-story view (the U05 drift pin — `about` is not
  seeded); (4) same on `/help` → the 404 floor (not the product-story).
- `tests/Kumunita.Core.Tests/PageServiceTests.cs` (modified) — **the `PG5_*`
  Core tests.** Three new tests (live scratch Postgres, two sessions):
  (1) `PG5_Seeder_TermsAndHelp_AreRootPages_NoDuplicates_AcrossTwoBoots` —
  seed into two separate sessions, assert exactly the two root `Page` docs
  (`terms` + `help`), one each, no duplicate (the idempotency pin);
  (2) `PG5_Seeder_About_IsNotSeeded_ProductStoryStaysAuthoritative` — assert
  **zero** `about` root `Page` docs (the U05 drift pin — a fresh `/about` is
  the full-bleed product-story view, not a Markdown page);
  (3) `PG5_Seeder_SeededPages_ArePublic_AudienceNull_LanguageEn_EmptyAuthor`
  — the seeded page shape: `Audience = null` (public — the one place pages
  differ from posts, ADR 0039 §3.4), `LanguageCode = "en"`, `ParentId = null`
  (a root), `AuthorId = ""` (platform content), not draft / not deleted,
  `Body`/`Title` carried verbatim (the byte-identical gate).
- `tests/Kumunita.Web.Tests/MLUI_FacesTests.cs` + `PublicLocaleAndAboutTests.cs`
  (modified) — the two `StaticPagesController` construction sites now pass the
  new `IPageService` first param. These L9/`/about` tests exercise the **legacy**
  `LocalizedPage` fallback, so the substitute's `GetByPathAsync` faults with a
  `KeyNotFoundException` (NSubstitute: `Returns(Task.FromException<Page>(new
  KeyNotFoundException()))` — the tree is absent, the controller falls through
  to the provider) — preserving the pre-U05 branches (a/b/c) exactly.

**What I verified** (the test-runner quirk in `AGENTS.md` applies — never
`dotnet test` / VS Test Explorer):

- `dotnet build Kumunita.slnx -c Debug` — **green, zero warnings** (after
  fixing the CA2017 log-template warning — the seeded-page log line used
  `{Keys}`/`{Pages}`×2 named placeholders (3 named, 2 args); switched to
  `{0}`/`{1}`/`{2}` with three args).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  — **Total: 224, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (9.1 s).
  U04's run was **218**; the delta is exactly the **6 new tests** (2 `PG5_*`
  reference-from-UGC in `PageControllerTests` + 4 retarget tests in
  `StaticPagesControllerPgTests`) — the full existing suite (including the
  U7 L9 a/b/c `/about` branches and the `/terms` 404 floor, re-pointed onto the
  new tree-first constructor) stayed green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  — **Total: 508, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (48.9 s).
  U04's run was **505**; the delta is exactly the **3 new `PG5_*` tests**
  (505 + 3 = 508) — the full existing suite stayed green, so the seeder's
  new `Page` docs introduced no regression.
- `git --no-pager status --short` — the only Core `src` change is
  `M src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (+ the Core test file);
  `M src/Kumunita.Web/Controllers/StaticPagesController.cs`; and the four Web
  test files (+ one new). **`IPageService`/`PageService`/`Page`/`PageTranslation`/
  `LocalizedPage`/`M1DocTypes`/`PageDocTypes`/registration are all untouched**
  (the frozen seams) — `git status` confirms no Core `Pages/` file changed.

**Drift from the plan** (one, the load-bearing one, recorded per protocol —
this is the U05 drift-pause (e) decision, not a silent deviation):

- **`about` is NOT seeded — the `/about` product-story view stays
  authoritative on a fresh instance.** The plan's deliverable #2 reads "seed
  `about`/`terms`/`help` … as `Page` docs" *and* the exit gate reads "a fresh
  instance is byte-identical to today." Those two conflict: today's `/about`
  is a **full-bleed product-story HTML view** (`Views/StaticPages/About`, driven
  by `HomeViewModel`), **not** a Markdown body. Seeding an `about` `Page` doc
  would (a) change what `/about` renders (the product story → a Markdown page),
  and (b) mount the `footer/community` slot (the mount slot's resolver would now
  find a page), breaking the "byte-identical to today" gate. So I took **option
  (b)** from the plan's own ⚠️: seed **only `terms` + `help`** as `Page` docs
  (the two that ARE Markdown static pages today), and leave `about` unseeded
  with the existing product-story fallback intact. The `StaticPagesController`
  is still tree-first for all three slugs — it just happens that a fresh
  instance's `about` resolves tree-absent → legacy-absent → product-story. This
  deviates from the plan's "seed all three" wording **in favor of the lane's
  byte-identical exit gate**, exactly the drift-pause (e) the plan anticipated
  ("if 'byte-identical' and 'seed all three' can't both be satisfied … STOP and
  record the decision"). **U07 (the `LocalizedPage` retirement) must not seed
  an `about` page** — the product-story view is the fresh-instance `/about`
  until an admin creates a real `about` `Page` at runtime.

**What the next agent (U06) must know** that isn't already in the plan:

- **`StaticPagesController` is now tree-first with a legacy fallback.** The
  constructor is `(IPageService, ITranslationProvider, IOptions<CommunityOptions>)`.
  A tree hit projects the `Page` to a throwaway `LocalizedPage`
  (Title/Body/`Updated = Modified ?? Created`) for the **shared**
  `Views/StaticPages/Page.cshtml` (so no new view file). U07 retires the
  `ITranslationProvider.GetPageAsync`/`LocalizedPage` path — at that point the
  tree-miss branches (legacy fallback) simply disappear and `/about`'s
  product-story fallback + the `/terms`/`/help` 404 floor remain. Do **not**
  leave the legacy fallback after U07 — the whole point of the "absorb, don't
  yank" two-step is that U05 lands the tree read and U07 removes the old one.
- **The seeder's `SeedDefaultPagesAsync` is `public static` and
  `EnDefaultPages()` is `public`** — they exist so the Core test can pin
  idempotency + the exact set without an `InternalsVisibleTo`. If U06/U07
  change the seeded set, update `EnDefaultPages()` (the single source) — the
  legacy `LocalizedPage` rows and the new `Page` docs both read from it, so
  they stay byte-identical by construction.
- **`about` is deliberately absent from `EnDefaultPages()`** — do not add it
  back (the U05 drift pin). A fresh `/about` is the product-story view; an
  admin-created `about` `Page` (or legacy `LocalizedPage`) is the only way
  `/about` becomes a Markdown page. The `footer/community` mount slot is
  unmounted on a fresh instance (the U04 layout's `{ ViewData["MountSlot"] =
  "footer/community" }` + `_MountSlot` partial resolves to `null` → no link).
- **The `PG5_*` Web reference-from-UGC tests are in `PageControllerTests.cs`,
  not `StaticPagesControllerPgTests.cs`** — the "link in an authorized post"
  half is about the **post** view's rendering (the single `MarkdownRenderer`)
  and the **page** `Read` decision (the `PageController.Show` 403), both of
  which live on the `PageController`/`MarkdownRenderer` surface. The
  `StaticPagesControllerPgTests.cs` file is the `/terms`/`/help`/`/about`
  legacy-route retarget (tree-first + legacy fallback + product-story). Keep
  them separate (they pin different surfaces).
- **NSubstitute gotcha (re-confirmed this unit):** for a `Task<Page>`-returning
  lane (`IPageService.GetByPathAsync`), make it fault with
  `.Returns(Task.FromException<Page>(new KeyNotFoundException()))` — **not**
  `.ThrowsAsync(...)` (which does not bind to a `Task<T>` return). The U04
  note flagged the same for the write lanes; it applies to the read lanes too.
- **The seeder log line uses `{0}`/`{1}`/`{2}` positional placeholders**
  (3 distinct, 3 args) — CA2017 counts *total placeholder occurrences* vs arg
  count, so reusing one named placeholder twice (`{Pages}` ×2) is a warning.
  If U06/U07 add a new placeholder, keep it positional and distinct.

**Status: U05 GREEN.** Build clean (zero warnings), Web tests **224/224** green
(218 pre-existing + 6 new `PG5_*`/retarget), Core tests **508/508** green (505
pre-existing + 3 new `PG5_*`), `LocalizedPage`/`M1DocTypes`/`IPageService`/
`PageService`/`Page`/registration **untouched** (the frozen seams — verified by
`git status`), the `about`-unseeded drift pin recorded. **Next unit: U06**
(per the register — the remaining unit before U07's destructive
`LocalizedPage` retirement; U07 may **not** seed an `about` page and **must**
remove the `StaticPagesController` legacy fallback).

## U6 — The translation lane live on pages (standing + display)

U06 makes the multilingual lane **live on pages** — the Web "add a
translation" surface for a `Page`, plus the **display** pin that decides
whether that affordance renders at all. It closes with four deliverables, all
additive and reusing the established announcement/post translation patterns
(`AnnouncementService.CanTranslateAnnouncement` /
`PostService.CanAddTranslation` are the exact shapes this mirrors):

1. A **non-throwing `CanTranslatePage` display gate** on `PageService` that
   shares **one decision body** with `CheckTranslateStanding` — so the display
   flag and the write-lane deny can never drift apart.
2. A **`PageShowViewModel.CanTranslate`** flag (the 13th positional arg) +
   the **gated add-form** in `Show.cshtml`.
3. A **`[HttpPost("{id}/translations")]`** POST route in `PageController`.
4. A **`PG6_*` Web test family** + **Core tests** pinning `CanTranslatePage`
   as a pure allow/deny matrix.

**What I changed** (the exact `git --no-pager status --short` set — nothing
else touched, and **none of the frozen seams** — `IPageService`/`Page`/
`PageTranslation`/`PageDocTypes`/`LocalizedPage`/registration):

- `src/Kumunita.Core/Pages/PageService.cs` (**the only Core `src` change**) —
  added `public static bool CanTranslatePage(string actorId,
  IReadOnlySet<string> actorRoles, Page? page)` and the private shared
  decision body `CanTranslatePageCore(IReadOnlySet<string>, Page)`;
  **refactored `CheckTranslateStanding` to delegate to `CanTranslatePage`**
  (it still throws `UnauthorizedAccessException` when the decision is `false`).
  The decision body is the single source of truth:

  ```csharp
  private static bool CanTranslatePageCore(IReadOnlySet<string> actorRoles, Page page)
  {
      if (actorRoles.Contains(Roles.GlobalAdmin)) return true;
      if (actorRoles.Contains(Roles.Translator)) return true;
      if (page.ComponentId is not null && actorRoles.Contains(Roles.ModeratorComponent(page.ComponentId)))
          return true;
      return false;
  }
  ```

  **ADR 0029 standing carried to pages:** a GlobalAdmin or a Translator
  qualifies on **any** page (scoped or flat/public); a community Moderator
  (`Roles.ModeratorComponent(page.ComponentId)`) qualifies **only** when the
  page is scoped to a community they moderate — a flat/public page
  (`ComponentId == null`) has no community to moderate, so that branch never
  qualifies; a plain Member never qualifies. **No new `IPageService` method,
  no new `AccessAction`/`AccessVia`/authorization branch, no new editor.**
- `src/Kumunita.Web/Models/PageViewModels.cs` — `PageShowViewModel` gains the
  13th positional `bool CanTranslate` (documented as the ADR 0029 display
  affordance flag / pin).
- `src/Kumunita.Web/Controllers/PageController.cs` — (a) `Show` now computes
  `var canTranslate = PageService.CanTranslatePage(actorId ?? string.Empty,
  KumunitaPrincipal.RoleSet(User), page);` and passes it as the 13th arg;
  (b) the new `AddTranslation` POST action (`[HttpPost("{id:guid}/translations")]`,
  `[ValidateAntiForgeryToken]`, `[Authorize(Roles = "GlobalAdmin,Moderator,Translator")]`)
  that loads the page **by id from the store** (the Edit GET lane's pattern —
  the route is keyed by `{id:guid}`, the same key the Edit/Publish/Delete/Move
  lanes use), re-checks `Read`, calls `AddTranslationAsync`, and maps
  `UnauthorizedAccessException` → `ForbidResult`, `KeyNotFoundException` →
  `NotFound`; plus two private helpers, `DerivePathAsync` (redirect target via
  `PagePaths.Href(byId, page)`, matching the existing `Publish` redirect) and
  `SeedLanguageName` (a `ListLanguagesAsync` lookup for the `TempData["info"]`
  message, falling back to the raw code).
- `src/Kumunita.Web/Views/Pages/Show.cshtml` — the `@{}` block computes
  `originalCode` + `missingLanguages` (`Model.Languages` that lack a
  translation, excluding the original), and a **gated add-a-translation form**
  (inside the existing chip-row `<div class="mb-3 border-top pt-3">`) renders
  per-`missingLanguages` language as a `<details>` + `<form method="post"
  action="@($"/pages/{Model.Id}/translations")">` with a hidden `languageCode`,
  a `title` input, the `rc-editor` toolbar + `textarea[name=body][data-rich-editor]`,
  the `_RichEditorToggle` partial, and a submit — **gated `@if (Model.CanTranslate)`**,
  mirroring `Views/Announcement/Detail.cshtml`'s `@if (Model.CanTranslate)`
  block. The existing ADR 0027 chip-swap markup is **unchanged**.

**Tests** (the test-runner quirk in `AGENTS.md` applies — never `dotnet
test` / VS Test Explorer):

- `tests/Kumunita.Web.Tests/PageControllerTests.cs` (modified) — **7 `PG6_*`
  tests** + a `WireStoreAndAllow(Page)` helper (stores the page loadable by id
  from `QuerySession().LoadAsync<Page>`, makes `AddTranslationAsync` return a
  fresh `PageTranslation`, and `CanAsync` return allowed):
  (a) `PG6_AddTranslation_Translator_Allowed_CallsService_AndRedirects`;
  (b-scoped) `PG6_AddTranslation_CommunityModerator_ScopedPage_Allowed`
  (`ComponentId="community-001"`); (b-flat)
  `PG6_AddTranslation_CommunityModerator_FlatPage_Denied` (`ComponentId=null`,
  service faults → `ForbidResult`); (c)
  `PG6_AddTranslation_PlainMember_Denied`; (d)
  `PG6_AddTranslation_AbsentPage_ReturnsNotFound` (default Build store's
  `LoadAsync` returns `null` → the absent shape); (e)
  `PG6_Show_CanTranslateFlag_SetForTranslator`; (e)
  `PG6_Show_CanTranslateFlag_UnsetForPlainMember`.
- `tests/Kumunita.Core.Tests/PageServiceTests.cs` (modified) — **5
  `PG6_CanTranslatePage_*` tests** pinning the pure matrix (the repo tests the
  bool probes directly — `PostService.CanAddTranslation` /
  `AnnouncementService.CanTranslateAnnouncement` are tested as allow/deny
  matrices, not only via their write lanes): GlobalAdmin any page; Translator
  any page; community-Moderator **allows** the scoped page, **denies** the flat
  page + denies a page in a **different** community they don't moderate; plain
  Member denies; null-page/blank-actor denies (the probe is pure — the
  write-lane gate is the one that throws for those shapes).

**What I verified:**

- `dotnet build Kumunita.slnx -c Debug` — **green, zero warnings**.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  — **Total: 231, Errors: 0, Failed: 0** (10.2 s). U05's run was **224**; the
  delta is exactly the **7 new `PG6_*` tests** (224 + 7 = 231) — the full
  existing suite stayed green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  — **Total: 513, Errors: 0, Failed: 0** (47.9 s). U05's run was **508**; the
  delta is exactly the **5 new `PG6_CanTranslatePage_*` tests** (508 + 5 = 513).
  The existing `PG3_Translate_*` standing family (GlobalAdmin / Translator /
  community-Moderator / plain-Member / flat-page-mod-of-other-comp /
  duplicate-language) **stayed green** — `CheckTranslateStanding` now delegates
  to `CanTranslatePage`, so those pins exercise the refactored decision body.
- `git --no-pager status --short` — **exactly six files**: the four above + the
  two test files. **No `IPageService`/`Page`/`PageTranslation`/`PageDocTypes`/
  `LocalizedPage`/`DependencyInjection` registration change** (the frozen
  seams) — `git status` confirms no Core `Pages/` file changed beyond
  `PageService.cs`.

**Drift from the plan:** **none.** No `IPageService`/Core seam was needed, the
ADR 0029 standing is fully expressible by the existing claims (`Roles.Moderator`,
`Roles.ModeratorComponent`, `Roles.Translator`, `Roles.GlobalAdmin`), and the
chip-swap shape was left untouched. **No drift-pause triggered.**

**What the next agent (U07) must know** that isn't already in the plan:

- **`CanTranslatePage` is `public static`** — the Web layer calls it directly
  (no `IPageService` registration change). If U07 touches `PageService`
  standing, the single decision body to change is **`CanTranslatePageCore`** —
  both the display probe and the `CheckTranslateStanding` write-lane gate flow
  through it, so they can't drift.
- **The `PG6_*` Web tests live in `PageControllerTests.cs`** (not a new file) —
  the `WireStoreAndAllow` helper is the pattern for stubbing a store whose
  `QuerySession().LoadAsync<Page>(id)` resolves a real `Page` (the absent
  shape, by contrast, is the default Build store's `LoadAsync` → `null` → the
  `NotFound` branch).
- **The NSubstitute `Task<T>` gotcha re-applies** — for the
  `Task<Page>`-returning store load, the absent shape is
  `Task.FromResult<Page?>(null)`, and a faulting store is
  `.Returns(Task.FromException<Page>(new ...))` (**not** `.ThrowsAsync(...)`) —
  CS1061 otherwise.
- **U07 is the destructive `LocalizedPage` retirement** — it may **not** seed an
  `about` page (the U05 drift pin) and **must** remove the `StaticPagesController`
  legacy fallback. U06's translation surface is **orthogonal** to that — it
  operates on `Page`/`PageTranslation` docs (the frozen seams), which U07 does
  not touch.

**Status: U06 GREEN.** Build clean (zero warnings), Web tests **231/231** green
(224 pre-existing + 7 new `PG6_*`), Core tests **513/513** green (508
pre-existing + 5 new `PG6_CanTranslatePage_*`), `IPageService`/`Page`/
`PageTranslation`/`PageDocTypes`/`LocalizedPage`/registration **untouched** (the
frozen seams — verified by `git status`), **no drift-pause triggered**. **Next
unit: U07** (the destructive `LocalizedPage` retirement — the last unit of the
`PG` lane).

## U7 — Absorb complete: retire `LocalizedPage` (destructive, last)

U07 is the **destructive** unit — the last of the `PG` lane. U01–U06 put the
new `Page`/`PageTranslation` tree in front of every seam that used to read
`LocalizedPage`, so U07 simply **deletes the legacy document and every service
seam that still touches it**, and leaves the routes (`/about`/`/terms`/`/help`)
pointing at the tree. No new surface, no new ADR — it's a removal.

**What I changed:**

- **`src/Kumunita.Core/Localization/LocalizedPage.cs` — DELETED.** The legacy
  static-page document is gone.
- **`src/Kumunita.Core/M1DocTypes.cs`** — removed the `LocalizedPage`
  unique-index registration (the doc-type registration surface no longer lists
  it).
- **`src/Kumunita.Core/Localization/ITranslationProvider.cs` +
  `TranslationProvider.cs`** — removed `GetPageAsync` and
  `FindPageByImageIdAsync` (the two page-string / page-image seams).
- **`src/Kumunita.Core/Localization/ILocalizationService.cs` +
  `LocalizationService.cs`** — removed `GetPageAsync` + `UpsertPageAsync`.
  **`GetCompletenessAsync` now returns the 3-prop `LanguageCompleteness`** — the
  `PresentPageSlugs` / `MissingPageSlugs` pair is gone, so the record and the
  call site both dropped to `(LanguageCode, PresentKeys, MissingKeys)` (the one
  compile fix of the unit — `LanguageCompleteness` no longer takes 5 args).
- **`src/Kumunita.Core/Localization/LanguageCompleteness.cs`** — trimmed to the
  3 props above.
- **`src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs`** — removed the
  `LocalizedPage` seed loop; the seeder now writes only `TranslationResource`
  (string keys) + the `en` `Page`/`PageTranslation` docs (`SeedDefaultPagesAsync`
  / `EnDefaultPages`). **`about` is still NOT seeded** (the U05 drift pin) — a
  fresh `/about` renders the full-bleed product-story view, never a Markdown
  page. Log message reworded to "(`{0}` keys) + `en` terms/help pages (`{1}`
  Page docs); `about` is not seeded".
- **`src/Kumunita.Web/Controllers/StaticPagesController.cs`** — rewritten
  **tree-only**: the `LocalizedPage` fallback branch is deleted; `Page()` now
  reads the `Page`/`PageTranslation` tree (catching `KeyNotFoundException` →
  `NotFound`) and returns a new nested
  **`StaticPageViewModel(string Slug, string Title, string Body,
  DateTimeOffset Updated)`**. The ctor dropped `ITranslationProvider` — it is now
  **2-arg** `(IPageService pages, IOptions<CommunityOptions> community)`. The
  `/about`/`/terms`/`/help` **routes remain**; only the old store path is gone.
- **`src/Kumunita.Web/Views/StaticPages/Page.cshtml`** — `@model` →
  `StaticPagesController.StaticPageViewModel`.
- **`src/Kumunita.Web/Controllers/LanguagesController.cs`** — removed the ML-UI
  admin **page editor** (`PreviewPage` / `SavePage` actions + `PageEditorViewModel`)
  and dropped `PresentPageSlugs` / `MissingPageSlugs` from `LanguageRowViewModel`.
- **`src/Kumunita.Web/Views/Languages/Index.cshtml`** — removed the "Pages"
  column + "Edit pages" link + page-slug cells; **`Views/Languages/PreviewPage.cshtml`
  — DELETED.**
- **`src/Kumunita.Web/Controllers/ContentImageController.cs`** — removed the
  `ITranslationProvider pages` ctor param, the platform-page
  `FindPageByImageIdAsync` serve branch, and the `Kumunita.Core.Localization`
  using. The content-image owner chain is now **post → reply → announcement**
  (a page is no longer a possible image owner).
- **Comment / cref scrubs** across `MarkdownRenderer.cs`, `PageController.cs`,
  `AttachmentController.cs`, `HomeController.cs`, and `Pages/Page.cs` — any
  lingering `LocalizedPage` reference in a doc-comment/cref is gone.
- **`src/Kumunita.Web/Milestones.cs`** — the `PG` label reworded to "absorbs and
  retires the legacy static-page lane" (dropped the literal `LocalizedPage` name
  from the label; `MilestonesTests` pins Ids + status, not label text). Status
  **left at `StatusPlanned`** — the single-in-progress pin forces M4 as the only
  `StatusNext`, so `PG` does not move to `StatusDone`/`StatusNext`.
- **Tests** (6 files): `LocalizationServiceTests.cs` (removed the
  `LocalizedPage`-backed tests incl. `Admin_SavePage_AuditRowShape_ViaAdmin`,
  trimmed the completeness asserts, dropped the `UpsertPage` helper);
  `MLUI_FacesTests.cs` (Core + Web — dropped the page-slug completeness cells);
  `PageServiceTests.cs` (comment scrub); `StaticPagesControllerPgTests.cs`
  (rewritten tree-only); `PublicLocaleAndAboutTests.cs` (`BuildAbout` reworked
  tree-based; kept the about-absent + terms-404 pins);
  `ContentImageUploadTests.cs` (5-arg ctor, dropped the Localization using);
  `ContentImageServingTests.cs` (the retired platform-page cell recorded as
  RETIRED — see drift below).

**What I verified:**

- `dotnet build Kumunita.slnx -c Debug` — **green, zero warnings**.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  — **Total: 229, Errors: 0, Failed: 0**. U06's run was **231**; the delta is
  exactly the **retired `LocalizedPage` page tests** (the two ML-UI page-slug
  cells + the platform-page image-serve cell) — the full remaining suite is green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  — **Total: 510, Errors: 0, Failed: 0**. U06's run was **513**; the delta is
  exactly the **three removed `LocalizationService` page tests** (M5 + M13 +
  `Admin_SavePage_AuditRowShape_ViaAdmin`).
- **Zero `LocalizedPage` in `src/`** — `Select-String` over every `src/**/*.cs`
  and `src/**/*.cshtml` returns nothing; the only remaining mentions in the repo
  are the ADR / design-doc / plan historical references (expected — they are the
  historical record, not live code).
- `git --no-pager status --short` — **21 modified + 2 deleted**; **no frozen-seam
  file touched** — `Page`/`PageTranslation` docs, `IPageService`/`PageService`,
  `PageDocTypes`, the U06 translation surface (`CanTranslatePage` /
  `CheckTranslateStanding`), and the `DependencyInjection` registrations are all
  clean in `git status`.
- The **`about`-unseeded drift pin is preserved** — no `about` `Page` is seeded
  (the U05 pin); a fresh `/about` still mounts the product-story view, not a
  Markdown page.

**Drift from the plan:** two **drift-pause decisions** (recorded here, per the
repo's convention — a pause is documented, not silently worked around):

1. **`ContentImageController`'s platform-page serve branch retired with the doc.**
   `IPageService` has no image reverse-lookup seam, so a page was never a
   possible content-image owner under the new tree; the branch was removed with
   `LocalizedPage`. `ContentImageServingTests`' item 16 is now the **closed
   (RETIRED)** cell that had pinned it.
2. **The ML-UI admin page editor retired.** `PreviewPage` / `SavePage` /
   `PageEditorViewModel` are gone; page editing and translation now flow through
   the **PG U04 composer** + the **U06 translation surface** (the
   `CanTranslatePage` / chip-swap lane) — the two dedicated page-authoring paths
   are the tree, not the legacy ML-UI page tab.

**What the next agent must know** that isn't already in the plan:

- **U07 is the LAST unit of the `PG` lane — there is no U08.** The `PG` lane is
  **complete**. The roadmap's next in-progress milestone is **M4** (events /
  RSVPs). Do not expect a following pages unit.
- **`Milestones.cs` `PG` status is deliberately `StatusPlanned`** — the
  `MilestonesTests.M4_Is_The_Single_InProgress_Milestone` pin requires **exactly
  one** `StatusNext` (M4). Do **not** flip `PG` to `StatusDone`/`StatusNext`;
  that would break the single-in-progress pin.
- **`LanguageCompleteness` is now 3-prop** — any code that reads the
  completeness row must use `(LanguageCode, PresentKeys, MissingKeys)`; the
  `PresentPageSlugs`/`MissingPageSlugs` pair no longer exists.
- **The `StaticPagesController` ctor is 2-arg** — a test or caller that still
  passes an `ITranslationProvider` will not compile.

**Status: U07 GREEN — the `PG` lane is complete.** Build clean (zero warnings),
Web tests **229/229** green, Core tests **510/510** green, **zero
`LocalizedPage` references in `src/`**, `Page`/`PageTranslation`/
`IPageService`/`PageDocTypes`/registration **untouched** (the frozen seams), the
**`about`-unseeded pin preserved**, and the two drift-pause decisions
(documented above) recorded. **The `PG` lane (U00–U07) is now fully absorbed —
`LocalizedPage` is retired.** **No next unit in this lane.**
