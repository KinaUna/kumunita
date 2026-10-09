# M30 — Admin onboarding — rolling handoff note

> **Milestone open (U00).** This is the **scratch tier** (rolling handoff
> note) of M30's three-tier contract. One `##` section per unit, appended
> (never rewritten), in order (U00, U01, … U07). Each unit writes exactly one
> short section before it exits; the next unit reads **only** that section +
> its own entry-reads list.
>
> - **Register** — `docs/plans-milestones/plan-m30-admin-onboarding.md`
> - **Design doc (primary)** — `docs/design/m30-admin-onboarding-design.md`
>   (U01/U02 author)
> - **ADR** — **ADR 0153** (the next free number after 0152 — the ADR index in
>   `docs/adr/README.md` confirms 0152 is the current highest; U00 verifies
>   0153 is free)
> - **Scope** — the seven most important initial settings the walk-through
>   links into (community name, languages, moderation, notifications, storage
>   limits, site content, issue escalation) + the `AdminOnboarding` **singleton**
>   doc (the SITE lane's shape, ADR 0150) + the `/admin/onboarding` GlobalAdmin
>   walk-through surface + the dismissible admin banner. The walk-through is a
>   **links-only guided shell** (M30·1 / M30·7) — it rides the existing
>   `GlobalAdmin`-gated lanes for every setting it surfaces; its **only** write
>   is the completion stamp (M30·4).
> - **Out of scope (named deferrals)** — per-step progress / resume-across-
>   sessions (the M22 D8·1 deferral) · a "you haven't set X" detection engine
>   (the M22 D8·2 deferral) · an admin "onboarding completion rate" metric (the
>   M22 D8·4 deferral) · and the `Milestones.cs` / README / `MilestonesTests`
>   trio until the milestone *ships* (U07 owns the close flip).
> - **Frozen base (unchanged)** — ADR 0005 B `LocaleSettings` singleton shape,
>   ADR 0150 `SiteContent` shape, ADR 0050 single-write-lane shape, ADR 0004
>   §B.1 additive doc type, ADR 0006 module-boundary contract, ADR 0015 D1
>   `kw-l` provider-floor discipline, the SITE / M22 test model.
> - **The seven admin surfaces (the walk-through's closed step set, by route,
>   in the README / `Milestones.cs` order):** community name + languages →
>   `/admin/languages` · moderation → `/admin/announcements/comments` ·
>   notifications → `/admin/quiet` · storage limits → `/admin/storage/settings`
>   · site content → `/admin/site` · issue escalation → `/admin/announcements`
>   (the **M32 placeholder** — M32 "issue submission & escalation" is
>   `StatusPlanned` on the roadmap, not yet shipped; the register flags this as
>   a **known deferral**, not a drift).
> - **M22 precedent shapes (the shapes M30 mirrors, by file):**
>   `Controllers/OnboardingController.cs` (the stateless `/onboarding` page +
>   the `CompleteOnboardingAsync` single-write-lane + the `FlashAsync` idiom) ·
>   `Views/Shared/_OnboardingBanner.cshtml` (the dismissible banner + the
>   `bannerEligible` read + the `sessionStorage` flag) · the closed
>   `onboarding.*` `kw-l` set (15 keys, en/de/fr/da) ·
>   `Core/SiteContent/SiteContentService.cs` (the best-effort read + the single
>   audited write-lane — the SITE/M29 shape M30 mirrors).
> - **New invariants (locked in ADR 0153, U00):** M30·1 guided shell, not a new
>   data surface · M30·2 completion is one singleton doc, `null` = not-yet-
>   guided · M30·3 read is a public admin surface (GlobalAdmin-gated, never
>   audited) · M30·4 write is the ADR 0150 single-write-lane shape (one
>   `AccessAudit` row) · M30·5 banner is the affordance, non-blocking ·
>   M30·6 closed `adminonboarding.*` `kw-l` set parity-pinned ×4 · M30·7
>   walk-through links into the seven roadmap settings (links-only) · M30·8
>   `Milestones.cs`/README/`MilestonesTests` trio untouched until ship (U07).

<!-- U00 appends its section below this line. One ## section per unit, in order
     (U00, U01, … U07). Never rewrite a prior section. -->

## U00 — Kickoff verified

- **The seven admin surfaces (closed step set, by route, in the README /
  `Milestones.cs` order) — all six concrete routes confirmed as real, shipped,
  `GlobalAdmin`-gated surfaces; the seventh is the register's own M32
  placeholder:**
  1. **Community name + languages** → `/admin/languages` — the language
     catalog + translated UI strings + static pages; the community name is
     edited within it (ADR 0026 / 0053 community-translation surface). Row in
     `Views/Admin/Platform.cshtml` (list-group item "Languages").
  2. **Moderation** → `/admin/announcements/comments` — the
     announcement-comment moderation toggle (the ADR 0101 shape, the closest
     shipped "moderation" surface). Linked as a card in
     `Views/Admin/Index.cshtml` (the `/admin` dashboard hub), **not** a
     `/admin/platform` row.
  3. **Notifications** → `/admin/quiet` — the notification-flush cadence the
     admin sets for held notification emails (ADR 0121 M20; residents set their
     own quiet window on `/settings/quiet`). Row in `Platform.cshtml`
     (list-group item "Quiet-time cadence").
  4. **Storage limits** → `/admin/storage/settings` — the admin-set per-file
     size limit + per-user total content quota (the ADR 0135 M25 surface).
     Linked as a card in `Views/Admin/Index.cshtml`, **not** a `/admin/platform`
     row.
  5. **Site content** → `/admin/site` — the landing surfaces' hero text + the
     show/hide toggles (the ADR 0150 SITE lane). Row in `Platform.cshtml`
     (list-group item "Site content").
  6. **Issue escalation** → `/admin/announcements` — the announcements surface,
     a **placeholder** because M32 "issue submission & escalation" is
     `StatusPlanned` (not yet shipped). The register flags this as a **known
     deferral**, not a drift; a future M32 close-flip re-points this step card's
     route to the M32 surface.
  - **⚠ Location note (handed to U01 / the design doc — see the drift note
    below):** the register's entry-read #2 and the M29-style "Milestone open"
    framing describe all seven as "`/admin/platform` list-group rows," but only
    **three** of the six concrete routes (`/admin/languages`, `/admin/quiet`,
    `/admin/site`) are actually list-group rows in `Platform.cshtml`. The other
    two (`/admin/announcements/comments`, `/admin/storage/settings`) are cards in
    the `/admin` dashboard hub (`Views/Admin/Index.cshtml`), and the seventh is
    the M32 placeholder (it has no admin-settings surface to link into yet).
    **The routes are correct and consistent across every register section —
    this is a location/framing imprecision in the entry-read, not a route
    error.** U04's walk-through links into the six concrete routes **by route**
    (invariant M30·7), which is route-agnostic about where the discoverability
    affordance lives, so this note does not block downstream work.

- **M22 precedent shapes (by file — all verified present and matching the
  register):**
  - `Controllers/OnboardingController.cs` — `[Authorize]` class gate (standard
    signed-in gate, not an onboarding gate); `GET /onboarding` seeds
    `OnboardingViewModel` from the owner-scope `Profile` read
    (`OnboardingCompletedAt` + per-step "set / not set" hints);
    `POST /onboarding/finish` is the **one** write (calls
    `IUserInfoService.CompleteOnboardingAsync`, `[ValidateAntiForgeryToken]`,
    `TempData["info"] = await FlashAsync("onboarding.flash_done")`, redirect
    home); `POST /onboarding/skip` routes to the **same** lane (`=> await
    Finish()`); `FlashAsync` is the `EffectiveLanguageCode.ResolveAsync` +
    `ITranslationProvider.GetAsync` chain with the `KnownTranslationKeys.EnValues`
    floor.
  - `Views/Shared/_OnboardingBanner.cshtml` — renders **iff** the signed-in
    resident's `OnboardingCompletedAt` is `null` (`bannerEligible = profile is
    not null && profile.OnboardingCompletedAt is null`); dismissible via the
    `data-dismiss-key="kumunita-onboarding-banner-dismissed"` button +
    `~/js/lib/onboarding-banner.js` (a `sessionStorage` flag, never a write to
    `OnboardingCompletedAt`); links to `/onboarding`; closed `onboarding.banner.*`
    `kw-l` set (text + action); a11y labels resolved server-side via
    `Translation.GetAsync`.
  - `Core/SiteContent/SiteContentService.cs` — the SITE write-lane shape M30
    mirrors: `GetAsync` best-effort (never throws / never null → in-code
    fallback), `SaveAsync` one session, doc + exactly **one** `AccessAudit` row
    (`Via = Admin`, action `site.save`, `TargetKind` "site") commit together
    (invariant C3, strong consistency C4), **no-op on a missing row** (never
    load-or-creates — SITE·6). `SiteContent.cs` confirms the
    `Id = "singleton"` sentinel + the field-set-ceiling shape.

- **ADR number:** **0153** — free. `docs/adr/README.md` line 176 ends at
  `| 0152 | Admin surface labels … |`; the `docs/adr/` directory's highest file
  is `0152-admin-surface-labels.md`; **no `0153-*.md` on disk** (confirmed via
  `file_search`). The next free number is 0153.

- **Precedent ADRs (all present, all Accepted):** **0150** `SiteContent`
  singleton shape (the `SiteContent` doc + `SiteContentService` best-effort
  read + single audited write-lane + the `AdminSiteController`
  `[Authorize(Roles = GlobalAdmin)]` surface) · **0050** `IsSignupOpenAsync`
  best-effort shape + `SetSignupOpenAsync` single-write-lane shape (the
  singleton-toggle audit shape M30's `admin_onboarding.complete` row mirrors) ·
  **0005 B** `LocaleSettings` singleton (the `Id = "singleton"` sentinel M30
  mirrors).

- **Drift check:** the register's locked content (the seven step routes, the
  `AdminOnboarding` singleton shape, the M22 / SITE precedent shapes, the ADR
  0153 number) all verify cleanly against source. **One location/framing
  imprecision recorded (not a blocker):** the register's entry-read #2 + the
  M29-style "Milestone open" framing call the seven surfaces "`/admin/platform`
  list-group rows," but only `/admin/languages`, `/admin/quiet`, and
  `/admin/site` are actually `Platform.cshtml` list-group rows;
  `/admin/announcements/comments` and `/admin/storage/settings` are `/admin`
  dashboard-hub cards (`Views/Admin/Index.cshtml`), and `/admin/announcements`
  is the M32 placeholder (no admin-settings surface yet). The **routes** are
  correct and consistent everywhere, so U01's design doc should name each step
  by its **route** (not by its list-group-row location) and U04/U05 link by
  route (M30·7) — the discoverability-affordance location is irrelevant to the
  walk-through. No route re-shape, no singleton re-shape, no ADR-number change
  is required by this note. **U01 reads only this `## U00` section + its own
  entry-reads list.**

## U01 — design doc Part 1

- **Design doc Part 1 authored** at
  `docs/design/m30-admin-onboarding-design.md` (~330 lines): the value chain,
  the context (the seven step cards **named by route**, in the README /
  `Milestones.cs` order — resolving U00's location/framing note by naming each
  step by route, not by list-group-row location), the scope (In / Out, incl.
  the M22 D8·1 / D8·2 / D8·4 deferrals + the `Milestones.cs` / README /
  `MilestonesTests` trio until ship), the **8 invariants** (M30·1–M30·8), the
  **8 FACES** (M30-1–M30-8), and the frozen-base assumptions (ADR 0132 M22 ·
  ADR 0150 SITE · ADR 0005 B · ADR 0050 · ADR 0004 §B.1 · ADR 0006 · ADR 0015
  D1). U02 can now pin by id.
- **The 8 invariants (by id):**
  - **M30·1** — the walk-through is a *guided shell*, not a new data surface
    (rides the six shipped `GlobalAdmin`-gated lanes; adds **no new write
    lane** for any of the seven settings; the only write is the completion
    stamp).
  - **M30·2** — the completion state is one **singleton** doc, `null` =
    not-yet-guided (`AdminOnboarding.CompletedAt: DateTimeOffset?`,
    `Id = "singleton"` sentinel, the `SiteContent` / `LocaleSettings` shape,
    ADR 0005 B / ADR 0150; registered in a new `AdminOnboardingDocTypes`
    surface, ADR 0004 §B.1; no EF migration; `SiteContent` +
    `LocaleSettings` docs untouched, ADR 0006).
  - **M30·3** — the read is a **public admin surface** (`GlobalAdmin`-gated
    by the standard `[Authorize(Roles = GlobalAdmin)]`, not a new
    authorization surface — the thin-token rule ADR 0001-B; never audited; a
    missing row degrades to `CompletedAt = null`).
  - **M30·4** — the write is the **ADR 0150 single-write-lane shape** (one
    `CompleteAsync` lane, one session, exactly one `AccessAudit` row —
    `Via = Admin`, action `admin_onboarding.complete`, `TargetKind`
    "admin-onboarding"; upserts the singleton; strong consistency).
  - **M30·5** — the banner is the **affordance, non-blocking** (a dismissible
    admin banner rendered on the admin surfaces **only while `CompletedAt` is
    `null`** AND the actor is a `GlobalAdmin`; links to `/admin/onboarding`;
    sign-in never gated; always dismissible via a `sessionStorage` flag —
    never a write to `CompletedAt`; the route always reachable).
  - **M30·6** — the closed **`adminonboarding.*` `kw-l` set is parity-pinned
    in four languages** (en/de/fr/da; pinned by
    `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`).
  - **M30·7** — the walk-through **links into the seven roadmap settings**
    (community name, languages, moderation, notifications, storage limits,
    site content, issue escalation — each linking into the existing admin
    surface by route; links-only: no inline control, no per-step POST, no
    persisted step cursor, no completion gate).
  - **M30·8** — the `Milestones.cs` / README / `MilestonesTests` trio is
    **untouched until the milestone ships** (U07 owns the close flip); the
    `WhatsNew.cs` registry gains one new entry (newest-first, the `0.46.0`
    row) naming M30 + ADR 0153.
- **The 8 FACES (by id, each bound to its invariant(s)):**
  - **M30-1** — a fresh `GlobalAdmin` sees the banner on every `/admin/*`
    page (banner renders iff `CompletedAt` is `null` AND the actor is a
    `GlobalAdmin`) (M30·5).
  - **M30-2** — the `/admin/onboarding` page walks the seven settings in the
    README / `Milestones.cs` order, each step linking into the existing admin
    surface that owns it (M30·1, M30·7).
  - **M30-3** — the "mark as complete" button stamps `CompletedAt = now` +
    writes exactly one `AccessAudit` row (`Via = Admin`, action
    `admin_onboarding.complete`, `TargetKind` "admin-onboarding") (M30·4).
  - **M30-4** — the banner clears on the next read after `CompletedAt` is
    stamped (strong consistency) (M30·4, M30·5).
  - **M30-5** — a non-`GlobalAdmin` never sees the banner (even if
    `CompletedAt` is `null`) (M30·3, M30·5).
  - **M30-6** — the walk-through's *only* write is `CompleteAsync` (no
    per-step POST, no persisted step cursor, no server-session step state)
    (M30·1, M30·7).
  - **M30-7** — the closed `adminonboarding.*` `kw-l` key set is
    parity-pinned in four languages (every key present, non-empty, in
    en/de/fr/da) (M30·6).
  - **M30-8** — the `AdminOnboarding` doc is a singleton (one row per
    instance, `Id = "singleton"` sentinel); the `SiteContent` +
    `LocaleSettings` docs are untouched (M30·2).
- **Drift check:** all Entry reads (the register's "one thing" section,
  U00's `## U00` section, the M22 design doc's FACES/invariant template, the
  SITE design doc's singleton + read-seam + write-lane shape, the design-doc
  template, `Platform.cshtml` + the M22 `Onboarding/Index.cshtml`) are
  consistent with the register. U00's location/framing note (only three of the
  seven routes are `/admin/platform` list-group rows; the rest are `/admin`
  dashboard-hub cards; the seventh is the M32 placeholder) is **resolved by
  naming each step by route** in the design doc's step table — consistent with
  M30·7 (route-agnostic), not a blocker. The invariants and FACES text is
  restated verbatim from the register's "one thing" section; none invented.

## U02 — design doc Part 2 + ADR 0153

- **Design doc Part 2 authored** (appended to
  `docs/design/m30-admin-onboarding-design.md`, sub-sections 2.1–2.8):
  the `AdminOnboarding` field set (exact C#), the read-seam contract
  (exact C#), the write-lane contract (exact C#), the banner-eligibility
  rule (exact), the 16 pinned seam-test names (exact), the acceptance gate
  (exact command list + regression pins), and the drift guard (frozen once
  written). The Part-2 text mirrors the `site-content-design.md` Part 2
  shape (the §2.1–§2.8 sub-section set) and pins against the Part-1
  invariants (M30·1–M30·8) + FACES (M30-1–M30-8) by id.
- **The `AdminOnboarding` field set (exact):** the **one** optional
  `DateTimeOffset?` field `CompletedAt` (default `null` = not-yet-guided,
  the floor); `Id = "singleton"` sentinel (the `SiteContent` /
  `LocaleSettings` shape, ADR 0005 B); the field set is the **ceiling** (the
  ADR 0153 D1 pin). A `Complete()` helper stamps
  `CompletedAt = DateTimeOffset.UtcNow`.
- **The read-seam contract (exact):**
  `IAdminOnboardingService.GetAsync(CancellationToken ct)` returns the
  singleton's `CompletedAt` value, **best-effort** (a missing store /
  missing row / read failure degrades to `null` = not-yet-guided, the floor);
  **never audited** (M30·3). The banner partial + the `/admin/onboarding`
  view call **one** helper (the read-seam) so they resolve to the **same**
  value (M30·5).
- **The write-lane contract (exact):**
  `IAdminOnboardingService.CompleteAsync(string actorBy, CancellationToken
  ct)` loads the singleton, stamps `CompletedAt = now`, saves in one session
  (invariant C3); exactly **one** `AccessAudit` row per stamp (`Via =
  Admin`, action `admin_onboarding.complete`, `TargetKind` "admin-onboarding"
  — the `site.save` shape, M30·4); **upserts** the singleton (ADR 0150 D6);
  **strong consistency** (live on the next `GetAsync` / banner read). The
  `AdminOnboardingController.Complete` action is the thin wrapper (the
  `AdminSiteController.SaveHome` shape — `GlobalAdmin`-gated
  `[ValidateAntiForgeryToken]` POST, `TempData["info"]` flash via
  `adminonboarding.flash_done`, `RedirectToAction(nameof(Index))`).
- **The banner-eligibility rule (exact):** the banner renders **iff** the
  actor is a `GlobalAdmin` AND `CompletedAt` is `null` (the M22
  `bannerEligible` read + the M29 `GlobalAdmin`-gated scope); **non-blocking**
  (sign-in never gated, always dismissible via a `sessionStorage` flag —
  never a write to `CompletedAt`, the route always reachable for a
  `GlobalAdmin`); the banner link points to `/admin/onboarding` (the M22
  `onboarding.banner.action` idiom).
- **The 16 pinned seam-test names (by class + method, exact):**
  - **Core.Tests `AdminOnboardingServiceTests`:**
    `GetAsync_MissingStore_ReturnsNull` /
    `GetAsync_MissingRow_ReturnsNull` /
    `CompleteAsync_WritesOneAccessAuditRow` /
    `CompleteAsync_StrongConsistency_LiveOnNextGetAsync` /
    `CompleteAsync_UpsertsSingleton_NoDuplicateRow`.
  - **Core.Tests `AdminOnboardingSeederTests`:**
    `FreshBoot_HasExactlyOneAdminOnboardingRow` /
    `FreshBoot_CompletedAtIsNull` /
    `SecondBoot_IsIdempotent_NoDuplicateRow`.
  - **Web.Tests `AdminOnboardingControllerTests`:**
    `GET_SeesCurrentCompletedAt` /
    `POST_Complete_StampsCompletedAt_WritesOneAccessAuditRow` /
    `GET_NonGlobalAdmin_IsDenied` /
    `POST_NonGlobalAdmin_IsDenied`.
  - **Web.Tests `AdminOnboardingBannerTests`:**
    `BannerRenders_ForGlobalAdmin_WhenNotCompleted` /
    `BannerDoesNotRender_ForNonGlobalAdmin` /
    `BannerDoesNotRender_WhenCompleted` /
    `BannerLinkPointsToAdminOnboarding`.
- **The acceptance gate (exact command list):**
  `dotnet build Kumunita.slnx -c Debug` + `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` +
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  + the `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests`
  pins + the `SiteContentServiceTests` / `AdminSiteControllerTests` /
  `OnboardingControllerTests` (M22) pins + the `MilestonesTests` pin + the
  `WhatsNewTests` pin. Runner: `dotnet exec` on the test assemblies (per
  AGENTS.md), **not** `dotnet test` / VS Test Explorer.
- **ADR 0153:** **created by U02** at `docs/adr/0153-admin-onboarding.md`
  with **`Status: Accepted`** (per the register's U02 entry — the register is
  authoritative for *what each unit does*). **U07 note:** the register's U07
  close-flip says "flip `Status: Draft` → `Status: Accepted`," but the ADR is
  **already `Accepted`** as created — so **U07's flip is a no-op** (the ADR
  is at its final status; U07 does not need to change it). The U07 close-flip
  that still applies: tag the `docs/adr/README.md` index row
  `**Done** (M30)` (the row currently reads `Accepted` and is **not yet**
  tagged `**Done** (M30)`).
- **ADR index update:** `docs/adr/README.md` gained the `0153` row (after
  the `0152` row): `| 0153 | Admin onboarding: a guided walk-through for a
  new GlobalAdmin through the seven most important initial settings (an
  `AdminOnboarding` singleton on the M22 / SITE lane's shape — a links-only
  walk-through, not a new write path; additive on 0150 + 0004 §B.1 + 0005 B +
  0006 + 0132) | Accepted |`. **0153 was free** (the index ended at 0152;
  ADR 0132 confirmed present on disk at
  `docs/adr/0132-onboarding.md`).
- **Drift check:** all Entry reads (the register's "one thing" section,
  U01's Part-1 invariants + FACES, the `site-content-design.md` Part-2 shape,
  `SiteContentService.cs`, `AdminSiteController.cs`, `FirstBootSeeder.cs`,
  ADR 0150, ADR 0132, `docs/adr/README.md`) are consistent with the register.
  **No drift pause required.** One note: the register's U06 entry names
  "15 tests" (5 service + 3 seeder + 4 controller + 4 banner = 16), and the
  register's U02 §2.6 entry also lists 16 names (5 + 3 + 4 + 4). The **16
  pinned names** are the authoritative count; U06 should author **16** tests,
  not 15. This is a count-typo in the register's U06 entry ("15"), not a
  drift in the pinned-name list.

## U03 — AdminOnboarding Core

- **Core implementation authored** (5 new files + 2 modified files + 1 test
  file). **Build green** (`dotnet build Kumunita.slnx -c Debug` —
  "Build succeeded with 86 warning(s)", **zero** of the 86 on the new
  AdminOnboarding files — all 86 are pre-existing xUnit analyzer warnings
  in unrelated test files). No new seams, no Web change, no view, no banner,
  no `kw-l` keys, no admin surface. **U04 can now pin by file.**

- **(a) The one field name:** `CompletedAt` (a nullable
  `DateTimeOffset?`, default `null` = not-yet-guided, the floor, M30·2).
  The `AdminOnboarding` POCO (the `Kumunita.Core.AdminOnboarding` bounded
  context, ADR 0153 D1 / the ADR 0005 B / ADR 0150 singleton shape) carries
  **exactly** this one field + the `Id` sentinel (`"singleton"`) + a
  `Complete()` helper stamping `CompletedAt = DateTimeOffset.UtcNow`.
  Field set is the **ceiling** (the ADR 0153 D1 pin).

- **(b) The `AdminOnboardingDocTypes` line (one `.Schema.For` call):**
  `src/Kumunita.Core/AdminOnboarding/AdminOnboardingDocTypes.cs` —
  `public static void Configure(StoreOptions opts) {
  opts.Schema.For<AdminOnboarding.AdminOnboarding>(); }` (the
  `SiteContentDocTypes` / `SurfaceLabelsDocTypes` shape, verbatim; fully
  qualified as `AdminOnboarding.AdminOnboarding` for the type/namespace
  collision idiom — the type and its parent namespace share the name
  `AdminOnboarding`). ADR 0004 §B.1 additive, no EF migration.

- **(c) The two boot-path `Configure(opts)` lines added:**
  - **`src/Kumunita.Web/Program.cs:152`** — `AdminOnboardingDocTypes.Configure(opts);`
    immediately after the `SurfaceLabelsDocTypes.Configure(opts);` neighbor
    (the same host dev-loop `AddMarten` lambda that registers every other
    `*DocTypes` surface). This is the **only** line needed to register the
    doc for the host — the `SchemaBootstrap.ApplyAsync` all-env boot path
    picks it up automatically via
    `store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync()`
    (line 48 of `SchemaBootstrap.cs`), which delta-detects and applies the
    new `mt_doc_adminonboarding` table idempotently (the exact
    `SiteContent` / `SurfaceLabels` / `Media` / `Tag` precedent — those
    surfaces also have a single `Configure(opts)` call in `Program.cs`
    only, not a second call in `SchemaBootstrap.cs`). The register's
    "two boot paths" phrasing is a single registration site + an automatic
    schema-apply call, not two distinct lines to add.
  - **`src/Kumunita.Core/DependencyInjection.cs:131`** — the
    `AddTransient<AdminOnboarding.IAdminOnboardingService>(sp => new
    AdminOnboarding.AdminOnboardingService(sp.GetRequiredService<Marten.IDocumentStore>()));`
    registration (the `ISiteContentService` / `ISurfaceLabelsService`
    "AddTransient with the store injected" shape, the type/namespace-collision
    idiom). **No** `ITranslationProvider` arg needed (the M30 service is
    narrower than the M29 `SurfaceLabelsService` — it does not resolve
    `kw-l` keys itself; the Web-layer `FlashAsync` idiom handles that at
    the U04 controller boundary, the M22 / M29 `OnboardingController`
    shape).

- **(d) The `FirstBootSeeder` step:**
  `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs:153` — the call
  `await SeedAdminOnboardingAsync(mt, logger, ct);` (step **4d**, between
  step 4c `SeedSurfaceLabelsAsync` and step 5
  `SeedTranslationResourcesAsync`). The method body is at
  `FirstBootSeeder.cs:578` (the `public static async Task
  SeedAdminOnboardingAsync(IDocumentStore mt, ILogger logger,
  CancellationToken ct)` shape, mirroring
  `SeedSiteContentAsync` / `SeedSurfaceLabelsAsync` verbatim):
  **create-if-missing, idempotent, never-overwrites** — a fresh boot
  stores exactly one `AdminOnboarding` row (`Id = "singleton"`,
  `CompletedAt = null`); a warm boot is a no-op (an existing row's
  `CompletedAt` field is left untouched, so a GlobalAdmin's later
  completion stamp is honored). **No `AccessAudit` row** on the seed (the
  seeder is not a principal — the exact `SeedSiteContentAsync` /
  `SeedSurfaceLabelsAsync` shape). **Public** (not private) so the Core
  test can pin the fresh-boot state + idempotency without an
  `InternalsVisibleTo` (the repo's Core test constraint — the
  `SeedSiteContentAsync` / `SeedSurfaceLabelsAsync` precedent).

- **(e) Compile warnings on the new types:** **zero** (confirmed by
  `Select-String` on the build output for `AdminOnboarding` /
  `SeedAdminOnboarding` — no matches). The 86 build warnings are all
  pre-existing xUnit analyzer warnings (`xUnit1051` CancellationToken,
  `xUnit2017` / `xUnit2029` assert-style) in unrelated test files
  (`GuardianEventRsvpGateTests.cs`, `LocaleControllerQuietSectionTests.cs`,
  `MilestonesTests.cs`, `M17AcceptanceGateTests.cs`, …), untouched by
  U03.

- **Deliverables authored (7 files, 5 new + 2 modified):**
  1. **`src/Kumunita.Core/AdminOnboarding/AdminOnboarding.cs`** (new) — the
     `AdminOnboarding` doc (the one `CompletedAt` field, the `Id =
     "singleton"` sentinel, the `Complete()` helper — the exact C# from the
     design doc §2.2, verbatim).
  2. **`src/Kumunita.Core/AdminOnboarding/IAdminOnboardingService.cs`**
     (new) — the `IAdminOnboardingService` interface (the `GetAsync(ct)`
     best-effort read + the `CompleteAsync(actorBy, ct)` audited write —
     the exact C# from the design doc §2.3, verbatim).
  3. **`src/Kumunita.Core/AdminOnboarding/AdminOnboardingService.cs`**
     (new) — the `AdminOnboardingService` implementation (the
     `GetAsync` best-effort try/catch → `null` + the `CompleteAsync`
     upsert-singleton + exactly one `AccessAudit` row (`Via = Admin`,
     action `admin_onboarding.complete`, `TargetKind` "admin-onboarding")
     in one session — the exact C# from the design doc §2.3/§2.4,
     verbatim).
  4. **`src/Kumunita.Core/AdminOnboarding/AdminOnboardingDocTypes.cs`**
     (new) — the `AdminOnboardingDocTypes.Configure(StoreOptions opts)`
     surface (the `SiteContentDocTypes` shape, verbatim).
  5. **`src/Kumunita.Core/DependencyInjection.cs`** (modify) — the
     `AddTransient<AdminOnboarding.IAdminOnboardingService>` registration
     (immediately after the `ISurfaceLabelsService` neighbor, mirroring
     the `ISiteContentService` / `ISurfaceLabelsService` shape).
  6. **`src/Kumunita.Web/Program.cs`** (modify) — the
     `AdminOnboardingDocTypes.Configure(opts);` host line (immediately
     after the `SurfaceLabelsDocTypes.Configure(opts);` neighbor, mirroring
     the `SiteContentDocTypes.Configure(opts);` /
     `SurfaceLabelsDocTypes.Configure(opts);` shape).
  7. **`src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs`** (modify) — the
     `SeedAdminOnboardingAsync` step (the call at line 153 + the method at
     line 578, mirroring `SeedSiteContentAsync` / `SeedSurfaceLabelsAsync`
     verbatim).
  8. **`tests/Kumunita.Core.Tests/AdminOnboardingSeederTests.cs`** (new) —
     the 3 seeder pins (names verbatim from the design doc §2.6):
     `FreshBoot_HasExactlyOneAdminOnboardingRow` /
     `FreshBoot_CompletedAtIsNull` /
     `SecondBoot_IsIdempotent_NoDuplicateRow`. The harness wires
     `AdminOnboardingDocTypes.Configure(opts)` in its own
     `DocumentStore.For` lambda (the `SiteContentSeederTests.BootStoreAsync`
     / `SurfaceLabelsSeederTests` idiom — without it the doc is invisible
     to Marten).

- **Test execution:** the 3 seeder tests are **not yet run** (U06 owns the
  acceptance gate — the design doc §2.7). The register's U03 exit criteria
  is "build green + `AdminOnboarding.cs` exists + the
  `AdminOnboardingService` + `AdminOnboardingDocTypes` compile" — **met**.
  The 5 service tests (the `AdminOnboardingServiceTests` class) + the 4
  controller tests (the `AdminOnboardingControllerTests` class) + the 4
  banner tests (the `AdminOnboardingBannerTests` class) are U06's
  deliverables — **not** authored by U03 (the unit-series rule: a unit
  never introduces a test whose exact name is not in the §pinned-test-names
  list for *its own* deliverables; U03's deliverable is the 3 seeder tests
  only).

- **Drift check:** all Entry reads (the register's U03 entry, U02's handoff
  `## U02` section, `SiteContent.cs` + `SiteContentService.cs` +
  `SiteContentDocTypes.cs` + `ISiteContentService.cs`, `DependencyInjection.cs`,
  `FirstBootSeeder.cs` (the `SeedSiteContentAsync` /
  `SeedSurfaceLabelsAsync` shapes), `SurfaceLabelsSeederTests.cs` (the
  `BootStoreAsync` harness idiom), `AccessAudit.cs` + `Decision.cs` (the
  `AccessAudit` field shape + the `AccessVia.Admin` / `AccessOutcome.Allow`
  enum values), `SchemaBootstrap.cs` (the all-env boot path),
  `Program.cs` (the host `AddMarten` lambda), the design doc §2.2–§2.4
  (the exact `AdminOnboarding` shape + the read/write contracts), ADR 0153
  (the decision record, read-only)) are consistent with the register.
  **No drift pause required.** One note: the register's U03 entry (c) says
  "the two boot-path lines added (file + line numbers)" — but the
  `SchemaBootstrap.cs` all-env path does **not** have its own
  `AdminOnboardingDocTypes.Configure(opts)` line (per the
  `SiteContent` / `SurfaceLabels` / `Media` / `Tag` precedent, all
  `*DocTypes.Configure` calls live in the host's `Program.cs` `AddMarten`
  lambda; `SchemaBootstrap.ApplyAsync` picks them up via
  `ApplyAllConfiguredChangesToDatabaseAsync`). The "two boot paths" is a
  **single** `Program.cs:152` line + an automatic schema-apply call in
  `SchemaBootstrap.cs:48` (the existing call, not a new one). This is a
  **register-imprecision, not a drift** — the actual code matches the
  SITE / M29 precedent exactly.

## U04 — AdminOnboardingController + ViewModel

- **Web implementation authored** (2 new files, 1 verify — **no views, no
  banner, no `kw-l` keys, no new test**). **Build green** (`dotnet build
  Kumunita.slnx -c Debug` — EXIT=0, 0 errors, **zero** warnings on the two
  new AdminOnboarding files — confirmed by `Select-String` on a
  `--no-incremental` build output). U05 can now pin by file.

- **(a) The 2 routes (exact paths, for U05's views):**
  - `GET /admin/onboarding` — the `AdminOnboardingController.Index` action
    (the `AdminSiteController.Index` / `OnboardingController.Index` shape —
    seeds the `AdminOnboardingViewModel` with the `Completed` state (the
    `IAdminOnboardingService.GetAsync` read's inverse, M30·3 — best-effort:
    a missing store / missing row / read failure degrades to
    `Completed = false`, the floor) + the closed seven-step list
    (`AdminOnboardingViewModel.ClosedSteps`, the M30·7 pin)). The U05
    `Views/AdminOnboarding/Index.cshtml` view renders from this model.
  - `POST /admin/onboarding/complete` — the `AdminOnboardingController.Complete`
    action (the `AdminSiteController.SaveHome` / `OnboardingController.Finish`
    shape — the **one** write action: calls
    `IAdminOnboardingService.CompleteAsync(actor)` for the signed-in
    GlobalAdmin, `[ValidateAntiForgeryToken]`, `TempData["info"] = await
    FlashAsync("adminonboarding.flash_done")`, `RedirectToAction(nameof(Index))`).
    The U05 view's "mark as complete" button is a
    `[ValidateAntiForgeryToken]` POST to this route.

- **(b) The seven `Step` entries (by key + route, in the README /
  `Milestones.cs` order):**
  | # | Key | LabelKey | Route | DescriptionKey |
  |---|-----|----------|-------|----------------|
  | 1 | `communityname` | `adminonboarding.step_communityname` | `/admin/languages` | `adminonboarding.desc_communityname` |
  | 2 | `languages` | `adminonboarding.step_languages` | `/admin/languages` | `adminonboarding.desc_languages` |
  | 3 | `moderation` | `adminonboarding.step_moderation` | `/admin/announcements/comments` | `adminonboarding.desc_moderation` |
  | 4 | `notifications` | `adminonboarding.step_notifications` | `/admin/quiet` | `adminonboarding.desc_notifications` |
  | 5 | `storage` | `adminonboarding.step_storage` | `/admin/storage/settings` | `adminonboarding.desc_storage` |
  | 6 | `sitecontent` | `adminonboarding.step_sitecontent` | `/admin/site` | `adminonboarding.desc_sitecontent` |
  | 7 | `escalation` | `adminonboarding.step_escalation` | `/admin/announcements` | `adminonboarding.desc_escalation` |

  **Note for U05:** the `LabelKey` + `DescriptionKey` columns name the
  `adminonboarding.*` `kw-l` keys that U05 must author (M30·6, the closed
  set parity-pinned in four languages, the M22 D7 "closed set" shape,
  admin-scope). The register's §"closed `kw-l` key set" table names 14
  keys: `adminonboarding.title` / `adminonboarding.intro` / the seven
  `adminonboarding.step_*` / `adminonboarding.visit` /
  `adminonboarding.complete` / `adminonboarding.flash_done` /
  `adminonboarding.banner.text` / `adminonboarding.banner.action`. The
  **seven `adminonboarding.desc_*` keys** (one per step) are **not** in the
  register's 14-key list — they are the per-step one-line description
  labels the design doc §2.4 / the U04 view model reference. U05 should
  confirm with the register whether the `desc_*` keys are in-scope (the
  register's 14-key set names only the `step_*` labels, not per-step
  descriptions). **This is a register-vs-design-doc mismatch, not a drift**
  — the register is authoritative for *which files exist* and *what each
  unit does*; the design doc is authoritative for the *pinned shapes*. U05
  should author the keys the register's 14-key set names, and **add** the
  seven `desc_*` keys if the design doc's `Step.DescriptionKey` field is
  kept (the design doc's `Step` record carries `DescriptionKey`, so U05
  must author all 21 keys: 14 + 7). If U05 decides the `desc_*` keys are
  out of scope, the `Step.DescriptionKey` field should be dropped (a
  drift pause, not a silent edit).

- **(c) The `Completed` property shape (the `GetAsync` read's inverse):**
  `public bool Completed { get; init; }` — the
  `IAdminOnboardingService.GetAsync` read's inverse
  (`CompletedAt != null`). `true` = `CompletedAt` is non-null (the banner
  clears); `false` = `CompletedAt` is `null` (the banner shows, the floor).
  The U05 banner partial reads the **same** `GetAsync` seam (M30·5 — the
  read is the **single** seam the banner + the view call, so they resolve
  to the **same** value). Never a claim (the thin-token rule, ADR 0001-B).

- **(d) Compile warnings on the new types:** **zero** (confirmed by
  `Select-String` on the `--no-incremental` build output for
  `AdminOnboarding` — 0 matches). The 69 build warnings are all
  pre-existing xUnit analyzer warnings (`xUnit1051` CancellationToken,
  `xUnit2017` / `xUnit2029` assert-style) + the pre-existing CS8604 /
  CS8603 nullable-reference warnings in unrelated files, untouched by U04.

- **Deliverables authored (2 files, new):**
  1. **`src/Kumunita.Web/Controllers/AdminOnboardingController.cs`**
     (new) — the `AdminOnboardingController` (the ADR 0150
     `AdminSiteController` shape, the M22 `OnboardingController` shape):
     `[Route("admin/onboarding")]` + `[Authorize(Roles =
     Kumunita.Core.Identity.Roles.GlobalAdmin)]`. Two actions:
     - `GET /admin/onboarding` (`Index`) — composes the read-seam
       (`IAdminOnboardingService.GetAsync`) for the `CompletedAt` state +
       the closed seven-step list
       (`AdminOnboardingViewModel.ClosedSteps`). The model is built from
       the **admin-scope read** (the `CompletedAt` value + the static
       seven-step list — the M22 `OnboardingViewModel` shape, admin-scope).
     - `POST /admin/onboarding/complete` (`Complete`) — the **one** write
       action: calls U03's `IAdminOnboardingService.CompleteAsync(actor)`
       for the signed-in GlobalAdmin, `[ValidateAntiForgeryToken]`,
       `TempData["info"] = await FlashAsync("adminonboarding.flash_done")`,
       `RedirectToAction(nameof(Index))` (the
       `AdminSiteController.SaveHome` / `OnboardingController.Finish` shape).
     The controller's **only** write is `CompleteAsync` (M30·1, M30·6);
     every step it surfaces **links into** the existing admin surface that
     already owns it (the M22 D3 "rides frozen lanes" pin, admin-scope).
     The `FlashAsync` idiom (the `OnboardingController.FlashAsync` shape,
     the M22 precedent for M30): guards both `localization` and
     `translationProvider` with a single `if (localization is null ||
     translationProvider is null)` → the `KnownTranslationKeys.EnValues`
     floor; otherwise the `EffectiveLanguageCode.ResolveAsync` +
     `ITranslationProvider.GetAsync` chain.
  2. **`src/Kumunita.Web/Models/AdminOnboardingViewModel.cs`** (new) —
     the `AdminOnboardingViewModel` (the M22 `OnboardingViewModel` shape,
     admin-scope): `Completed` (bool, the `GetAsync` read's inverse) +
     `IReadOnlyList<Step> Steps` (the closed seven-step set, each a
     `(Key, LabelKey, Route, DescriptionKey)` record, in the README /
     `Milestones.cs` order) + `ClosedSteps` (the static readonly list) +
     the `Step` record (the four-field tuple). The seven `Step` entries are
     the closed set (the register's §"closed `kw-l` key set" table, the
     seven tuples in the README / `Milestones.cs` order).

- **Verify (DI visibility):** **confirmed** — the
  `IAdminOnboardingService` registration is at
  `src/Kumunita.Core/DependencyInjection.cs:131` (U03's
  `AddTransient<AdminOnboarding.IAdminOnboardingService>` — the
  `ISiteContentService` / `ISurfaceLabelsService` "AddTransient with the
  store injected" shape). The Web project calls
  `builder.Services.AddKumunitaCore()` at `Program.cs:262`, which
  registers the service into the Web's DI container. The
  `AdminOnboardingController`'s constructor injection
  (`IAdminOnboardingService adminOnboarding`) resolves at the Web layer
  via the Core DI registration. **No Web-layer registration needed** (the
  `src/Kumunita.Web/DependencyInjection.cs` file does **not** exist — the
  Web DI is in `Program.cs`, which already calls `AddKumunitaCore()`).

- **Drift check:** all Entry reads (the register's U04 entry, U03's
  handoff `## U03` section, `AdminSiteController.cs` (the ADR 0150
  `/admin/site` surface — the `GlobalAdmin`-gated dedicated controller +
  the `[ValidateAntiForgeryToken]` POST + the `TempData["info"]` flash +
  the `RedirectToAction(nameof(Index))` redirect), `OnboardingController.cs`
  (the M22 `OnboardingController` — the stateless `/onboarding` page + the
  `CompleteOnboardingAsync` single-write-lane + the `FlashAsync` idiom +
  the `KumunitaPrincipal.SubjectId(user)` read),
  `IAdminOnboardingService.cs` (U03's read-seam + write-lane — the
  `GetAsync` / `CompleteAsync` shape), `AdminQuietController.cs` (the
  sibling admin controller with the `FlashAsync` idiom — the canonical
  pattern for the admin-scope `FlashAsync`), `ITranslationProvider.cs`
  (the `GetAsync(key, preferredLanguageCode)` signature),
  `EffectiveLanguageCode.cs` (the `ResolveAsync(request, localization,
  provider)` signature), `OnboardingViewModel.cs` (the M22 view model
  shape to mirror), `Program.cs` (the `AddKumunitaCore()` call at line
  262 — the Web DI visibility), `DependencyInjection.cs` (U03's
  `AddTransient` registration at line 131 — the Web DI visibility), the
  design doc §2.3–§2.5 (the exact `AdminOnboardingViewModel` shape + the
  banner-eligibility rule)) are consistent with the register.
  **One register-vs-design-doc mismatch noted (not a drift, not a blocker
  for U04 — U05 should confirm):** the register's 14-key `kw-l` set does
  not include the seven `adminonboarding.desc_*` keys (one per step
  description), but the design doc's `Step.DescriptionKey` field + the
  U04 `AdminOnboardingViewModel.ClosedSteps` reference them. U05 should
  author all 21 keys (14 + 7) if the `Step.DescriptionKey` field is kept,
  or drop the field if the `desc_*` keys are out of scope (a drift pause,
  not a silent edit). **U04's deliverable is the controller + view model
  only — the `kw-l` keys are U05's.

## U05 — view + banner + kw-l keys

- **Web implementation authored** (2 new view files + 2 modified files —
  **no controller change, no Core change, no new test**). **Build green**
  (`dotnet build Kumunita.slnx -c Debug` — EXIT=0, 0 errors). U05 resolves
  U04's register-vs-design-doc mismatch (the `desc_*` keys) by **authoring
  all 21 keys** (the register's 14 + the 7 `adminonboarding.desc_*` keys the
  `Step.DescriptionKey` field references), per U04's note.

- **(a) The view path + the banner partial path + the `_AdminNav` render
  line:**
  - **`src/Kumunita.Web/Views/AdminOnboarding/Index.cshtml`** (new) — the
    `/admin/onboarding` walk-through view (the M22
    `Views/Onboarding/Index.cshtml` shape, admin-scope). `@model
    AdminOnboardingViewModel` + `ViewData["Title"] = "Admin onboarding"` +
    the `<h1>` (`adminonboarding.title`) + the intro paragraph
    (`adminonboarding.intro`) + the seven step cards (each a
    `list-group-item` with a title [`@step.LabelKey` = the
    `adminonboarding.step_*` key] + a one-line description [`@step.DescriptionKey`
    = the `adminonboarding.desc_*` key] + a "visit this setting" link
    [`adminonboarding.visit`] pointing at `@step.Route`) + the ONE write
    (the "mark as complete" button, `adminonboarding.complete`, a
    `[ValidateAntiForgeryToken]` POST to `/admin/onboarding/complete`).
    The copy is the closed `adminonboarding.*` set (M30·6) — the only
    user-visible copy (the M22 D7 "closed set" pin, admin-scope).
  - **`src/Kumunita.Web/Views/Shared/_AdminOnboardingBanner.cshtml`** (new)
    — the admin onboarding banner (the M22 `_OnboardingBanner` partial's
    admin sibling). Renders **only** when the signed-in actor is a
    `GlobalAdmin` (`KumunitaPrincipal.IsGlobalAdmin(User)`) AND
    `AdminOnboarding.CompletedAt` is `null` (`@inject IAdminOnboardingService
    AdminOnboarding` + `await AdminOnboarding.GetAsync()` — the **same**
    read-seam U04's controller calls, M30·5). Links to `/admin/onboarding`
    (`adminonboarding.banner.action` CTA label); copy is
    `adminonboarding.banner.text`. Dismissible via the M22
    `onboarding-banner.js` `sessionStorage` flag (`data-dismiss-key` on the
    `.btn-close`; reuses the shared `.kumunita-onboarding-banner` class so
    the one shared JS module dismisses it — the M22 D5 "non-blocking" pin,
    admin-scope; **never** a write to `CompletedAt`).
  - **`src/Kumunita.Web/Views/Admin/_AdminNav.cshtml`** (modify) — the
    render line `@await Html.PartialAsync("~/Views/Shared/_AdminOnboardingBanner")`
    added at the top of the admin sub-nav, **before the nav-tabs**, so it
    is visible on every `/admin/*` page (the `_AdminNav` scope — the M22
    `_Layout` `_OnboardingBanner` render shape, admin-scope). Explicit
    `~/Views/...` path (the codebase's cross-folder partial idiom, e.g.
    `~/Views/Admin/_AdminNav.cshtml` from `AdminHelp/` / `AdminStorage/`).
    **Note:** the register's U05 entry names the admin nav partial
    `Views/Shared/_AdminNav.cshtml`, but the actual file lives at
    `Views/Admin/_AdminNav.cshtml` (a register location imprecision — the
    correct file was located via `file_search` and modified there).

- **(b) The 21 `kw-l` keys (by name, all authored × en/de/fr/da, non-empty,
  in all four languages):** `adminonboarding.title` ·
  `adminonboarding.intro` · `adminonboarding.step_communityname` ·
  `adminonboarding.step_languages` · `adminonboarding.step_moderation` ·
  `adminonboarding.step_notifications` · `adminonboarding.step_storage` ·
  `adminonboarding.step_sitecontent` · `adminonboarding.step_escalation` ·
  `adminonboarding.desc_communityname` · `adminonboarding.desc_languages` ·
  `adminonboarding.desc_moderation` · `adminonboarding.desc_notifications` ·
  `adminonboarding.desc_storage` · `adminonboarding.desc_sitecontent` ·
  `adminonboarding.desc_escalation` · `adminonboarding.visit` ·
  `adminonboarding.complete` · `adminonboarding.flash_done` ·
  `adminonboarding.banner.text` · `adminonboarding.banner.action`. The 14
  are the register's named set; the 7 `adminonboarding.desc_*` are the
  per-step one-line descriptions U04's `Step.DescriptionKey` references
  (the register's 14-key list omitted them — resolved by authoring all 21,
  per U04's note + the user's instruction). The `a11y.onboarding_region` /
  `a11y.onboarding_action` / `a11y.onboarding_dismiss` accessible names are
  **reused** from the existing closed `a11y.*` set (present in all four
  languages) — not new keys (the M22 banner's a11y idiom, admin-scope).
  The `en` values are the source text (the ADR 0015 D1 kw-l
  provider-floor discipline); `de` / `fr` / `da` are the translations.
  **Danish terminology aligned to the repo's established word for
  "community" (`Fællesskab`, per `nav.community` / `community.browse`), not
  a coinage.**

- **(c) The banner-eligibility read (the `GlobalAdmin`-gated +
  `CompletedAt = null` pin):** `isGlobalAdmin = User.Identity?.IsAuthenticated
  == true && KumunitaPrincipal.IsGlobalAdmin(User)` (the standard
  role read — the thin-token rule, ADR 0001-B; a non-`GlobalAdmin` never
  sees the banner, M30·3/M30·5). `bannerEligible = (await
  AdminOnboarding.GetAsync()) is null` (the best-effort read — a missing
  store / row / read failure degrades to `null` = not-yet-guided, the floor;
  **the same seam U04's controller + the `/admin/onboarding` view call**, so
  the banner + the view resolve to the **same** value, M30·5). Never a
  claim. Non-blocking: sign-in never gated, the route always reachable,
  dismissal a `sessionStorage` flag — never a write to `CompletedAt`.

- **(d) Compile warnings on the new types:** **zero** (build EXIT=0, 0
  errors; the new view / partial / key lines introduce no warnings).

- **Exit pins (U05's own — all GREEN):**
  - `dotnet build Kumunita.slnx -c Debug` → **EXIT=0, 0 errors**.
  - `KnownTranslationKeys_ParityTests` (Core.Tests) → **7 tests, 0 failed**
    (the 21 keys present, non-empty, in all four languages — the de/fr/da
    baselines at full registry parity).
  - `KwLRegistryConsistencyTests` (Web.Tests) → **1 test, 0 failed** (every
    `kw-l key="…"` in the new view + banner is registered in
    `KnownTranslationKeys`).
  - `git status` clean except: the 2 new Web view files, the 2 modified
    files (`_AdminNav.cshtml` + `KnownTranslationKeys.cs`), the moved unit
    plan (`m30-u05.md` → `done/m30/`), and this appended handoff section.

- **One pre-existing close-gate red — recorded, not a U05 unit-pin failure
  (U06/U07 to consume at close):** the full `Kumunita.Web.Tests` suite runs
  961 tests with **1 failed** — `ImproveHarnessTests.ImproveCheck_Gate_Passes`
  (the IMPROVE lane's `improve-check.ps1` **close** gate, not a U05 unit pin).
  It is red on exactly two counts, **neither a U05 unit deliverable**, and
  was **already red before U05**:
  - **gate (a)** — `src/*.cs over 2000 lines (grown past baseline)`:
    `KnownTranslationKeys.cs = 9744` (baseline was 9617 — **U05's intended
    21-key deliverable**) **and** `FirstBootSeeder.cs = 4744` (baseline was
    4663 — **U03's** seeder step, untouched by U05). The `improve-check.ps1`
    comment says the baseline is re-set at the milestone close ("Re-baselined
    at the M29 close … the next reduction lane will measure against it") —
    i.e. **U07's M30 close re-baselines it**; U05 must not touch the baseline
    (a different unit's file / the register's own U07 close).
  - **gate (g)** — `docs/design/m30-admin-onboarding-design.md (900 lines, no
    Abstract)` — a **U01/U02** design-doc artifact U05 did not touch (outside
    U05's Deliverables).
  - U05's own exit pins (build + `KwLRegistryConsistencyTests` +
    `KnownTranslationKeys_ParityTests`) are **all green** — the single red is
    the close gate, which the register assigns to the close (U07) and which
    was pre-existing (FirstBootSeeder.cs growth is U03's). **No drift pause
    required; no improvisation** — the register's U07 close + the
    `improve-check.ps1` baseline re-set own the resolution.

- **Drift check:** all Entry reads (the register's U05 entry, U04's handoff
  `## U04` section, `Views/Onboarding/Index.cshtml` (the M22 walk-through
  shape), `Views/Shared/_OnboardingBanner.cshtml` (the M22 banner shape),
  `wwwroot/js/lib/onboarding-banner.js` (the dismissal idiom),
  `Views/Admin/_AdminNav.cshtml` (the admin sub-nav — the actual location,
  not the register's `Views/Shared/_AdminNav.cshtml`),
  `KnownTranslationKeys.cs` (the `onboarding.*` set to mirror),
  `AdminOnboardingViewModel.cs` (U04's `Step` record + `ClosedSteps`),
  `LocalizeTagHelper.cs` (the `<kw-l>` key attribute),
  `KwLRegistryConsistencyTests.cs` (the view-scan pin),
  `KnownTranslationKeys_ParityTests.cs` (the registry-parity pin),
  the design doc §2.5–§2.6 (the banner-eligibility rule + the closed
  `kw-l` key set), `KumunitaPrincipal.cs` (the `IsGlobalAdmin` helper)) are
  consistent with the register. **One register location imprecision recorded
  (not a blocker):** the register names the admin nav partial at
  `Views/Shared/_AdminNav.cshtml`, but it actually lives at
  `Views/Admin/_AdminNav.cshtml` (located via `file_search`, modified there).
  **U04's register-vs-design-doc mismatch (the 7 `desc_*` keys) is resolved
  by authoring all 21 keys** (per U04's note + the user's explicit
  instruction) — **not** a drift pause. The single full-suite red is the
  pre-existing IMPROVE close gate (above), not a U05 unit pin. **U06 reads
  only this `## U05` section + its own entry-reads list.****

## U06 — seam tests (13 new) + gate recorded

**Deliverable (U06's unit):** authored the **13 remaining M30 seam tests**
(5 service + 4 controller + 4 banner), ran the acceptance gate, and recorded
the gate result. The 3 `AdminOnboardingSeederTests` are **U03's** (present,
untouched by U06). Together the M30 seam surface is **16 tests** (the
register's §2.6 pin — U03's count-typo of "15" resolved per U04's note: the
**pinned names** are authoritative, and they are 16). U06 authored **only**
the 13 that were not already present (U03 owns the 3 seeder tests); U06 did
**not** create or modify `AdminOnboardingSeederTests.cs`.

**(a) New test files authored (3):**

- `tests/Kumunita.Core.Tests/AdminOnboardingServiceTests.cs` (5) — direct
  construction of `AdminOnboarding.AdminOnboardingService` over a fresh
  Testcontainers `postgres:18` store (`NewDatabaseAsync` + `M1DocTypes` /
  `M3DocTypes` / `AdminOnboardingDocTypes` + `ApplyAllConfiguredChangesToDatabaseAsync`),
  mirroring `AdminOnboardingSeederTests.BootStoreAsync`. Pins: `GetAsync`
  best-effort null (missing store / missing row), `CompleteAsync` writes
  exactly **one** `AccessAudit` row (`Via = Admin`, action
  `admin_onboarding.complete`, `TargetKind = "admin-onboarding"`, `TargetId
  = "singleton"`, `Outcome = Allow`), strong consistency (live on the next
  `GetAsync`), and upsert-singletone (two `CompleteAsync` → one row).
- `tests/Kumunita.Web.Tests/AdminOnboardingControllerTests.cs` (4) — the
  `AdminSiteControllerTests` shape (direct construction, NSubstitute
  `IAdminOnboardingService` seam, `NoOpTempDataProvider`). Pins: GET seeds
  the view model from the current `CompletedAt` state + the closed
  seven-step list (routes in README / `Milestones.cs` order); POST
  `/admin/onboarding/complete` stamps completion (exactly one
  `CompleteAsync` call for the signed-in GlobalAdmin) + redirects to
  `Index` + sets the `adminonboarding.flash_done` flash (`TempData["info"]`
  = the `KnownTranslationKeys.EnValues` floor, the null-localization test
  construction); and the surface is `GlobalAdmin`-gated
  (`[Authorize(Roles = GlobalAdmin)]`, the `POST` action carries no weaker
  gate of its own).
- `tests/Kumunita.Web.Tests/AdminOnboardingBannerTests.cs` (4) — the house
  "structural string pin, no TestServer" idiom (the
  `BookmarkButtonTests` / `NavMoreFoldTests` / M22 `_OnboardingBanner`
  precedent). Reads `Shared/_AdminOnboardingBanner.cshtml`, strips the
  `@*…*@` author-comment block, and pins: the banner renders **iff**
  GlobalAdmin AND `CompletedAt` is null (the `KumunitaPrincipal.IsGlobalAdmin`
  + `User.Identity?.IsAuthenticated` gate ANDed with the
  `AdminOnboarding.GetAsync` `completedAt is null` read, the `@if
  (bannerEligible)` single render branch); does **not** render for a
  non-GlobalAdmin (and does not fall back to the M22 resident
  `OnboardingCompletedAt` / `IUserInfoService` read — M22 / M30 distinction);
  does not render when completed; and the CTA link points to
  `/admin/onboarding` (`href="/admin/onboarding"`, the closed
  `adminonboarding.banner.*` `kw-l` set, the shared
  `onboarding-banner.js` dismissal, the `data-dismiss-key` sessionStorage
  flag).

**Existing seeder file (unchanged by U06):** `AdminOnboardingSeederTests.cs`
(U03's, 3 tests) — **not** created or modified here.

**(b) The 16 pinned test names (design doc §2.6, verbatim):**

- `AdminOnboardingServiceTests` (5): `GetAsync_MissingStore_ReturnsNull` ·
  `GetAsync_MissingRow_ReturnsNull` · `CompleteAsync_WritesOneAccessAuditRow`
  · `CompleteAsync_StrongConsistency_LiveOnNextGetAsync` ·
  `CompleteAsync_UpsertsSingleton_NoDuplicateRow`
- `AdminOnboardingSeederTests` (3, U03's):
  `FreshBoot_HasExactlyOneAdminOnboardingRow` ·
  `FreshBoot_CompletedAtIsNull` · `SecondBoot_IsIdempotent_NoDuplicateRow`
- `AdminOnboardingControllerTests` (4): `GET_SeesCurrentCompletedAt` ·
  `POST_Complete_StampsCompletedAt_WritesOneAccessAuditRow` ·
  `GET_NonGlobalAdmin_IsDenied` · `POST_NonGlobalAdmin_IsDenied`
- `AdminOnboardingBannerTests` (4): `BannerRenders_ForGlobalAdmin_WhenNotCompleted`
  · `BannerDoesNotRender_ForNonGlobalAdmin` ·
  `BannerDoesNotRender_WhenCompleted` · `BannerLinkPointsToAdminOnboarding`

**(c) Pass/fail counts (all 16 PASS):**

- `AdminOnboardingServiceTests` + `AdminOnboardingSeederTests` (8 Core) —
  **green in isolation**: `dotnet exec Kumunita.Core.Tests.dll` filtered to
  the two classes → `Total: 8, Errors: 0, Failed: 0`.
- `AdminOnboardingControllerTests` + `AdminOnboardingBannerTests` (8 Web) —
  **green in isolation**: `dotnet exec Kumunita.Web.Tests.dll` filtered to
  the two classes → `Total: 8, Errors: 0, Failed: 0` (`EXIT_CODE=0`).

**(d) Gate status (2026-10-09):**

- **Build** — `dotnet build Kumunita.slnx -c Debug` → **green**
  (`Build succeeded. 0 Error(s)`).
- **Full Web suite** — `Total: 969, Errors: 0, Failed: 1` — the single red
  is the **expected** `ImproveHarnessTests.ImproveCheck_Gate_Passes` (the
  pre-existing IMPROVE close gate, see (e)); all other 968 pass, including
  the 8 M30 Web pins, `KwLRegistryConsistencyTests`,
  `KnownTranslationKeys_ParityTests`, `SiteContentServiceTests`,
  `AdminSiteControllerTests`, `OnboardingControllerTests`, `MilestonesTests`,
  `WhatsNewTests`.
- **Full Core suite** — `Total: 1392, Errors: 0, Failed: 4` — **all 4 are
  Events-domain flakies, none an `AdminOnboarding` test**:
  `EventServiceCascadeTests.Head_Edit_Rule_Change_Rematerializes_The_Series`
  · `EventServiceCascadeTests.Head_Edit_Cascades_Text_Fields_To_All_NonDeleted_Occurrences`
  (both `Expected: 5, Actual: 4`) ·
  `EventServiceSkipUndeleteTests.Skip_Occurrence_Sets_IsDeleted_On_That_Row_Only`
  · `EventServiceCreateRecurrenceTests.Create_With_A_Weekly_Rule_Materializes_All_Occurrences`.
  **NOT M30's** — U06's change is purely additive (3 new test files in the
  `AdminOnboarding` namespaces; no existing Core test / service / seeder /
  Events code touched) and U06's 8 M30 Core tests are green in isolation.
  The signature (4 Events tests, `Expected: 5 / Actual: 4`, deep Testcontainers
  `pg_isready` readiness churn, ~233 s) is the known flaky-under-parallel-load
  form (the M29 close recorded Core as `1384 / 0`; the same Events flakies
  surface intermittently under load). Carried forward to U07 (the close), not
  U06's defect.

**(e) Still-open drift / the single expected red (recorded, do NOT fix):**

- **The one expected red — `ImproveHarnessTests.ImproveCheck_Gate_Passes`**
  (the **IMPROVE lane's close gate**, `improve-check.ps1`), red on exactly
  two sub-gates, **neither a U06 unit deliverable**, and **already red
  before U06**:
  - **gate (a)** — `src/*.cs` over 2000 lines grown past its U00 baseline: 2
    (`KnownTranslationKeys.cs = 9744`, baseline 9617 — U05's intended 21-key
    deliverable; `FirstBootSeeder.cs = 4744`, baseline 4663 — U03's seeder
    step). The `improve-check.ps1` baseline is re-set at the milestone close
    — **U07's M30 close re-baselines it**; U06 must not touch the baseline.
  - **gate (g)** — `docs/design/m30-admin-onboarding-design.md (900 lines, no
    Abstract)` — a **U01/U02** design-doc artifact, outside U06's Deliverables.
  - All other sub-gates (`b` ADR index · `c` shared headings · `d` handoff
    TL;DR · `e` `client/*.ts` · `f` views `.cshtml`) are **OK**.

  U06 **records** this red and **does not fix it** (per the U06 spec +
  U05's handoff); the resolution is U07's M30 close (the `WhatsNew` `0.46.0`
  entry, the `Milestones` / README / STATUS / ARCHITECTURE close flip, the
  `done/m30/` move, and the `improve-check.ps1` baseline re-set).

**Drift check:** all Entry reads (the register's U06 entry, U04's / U05's
handoff `## U04` / `## U05` sections, `AdminOnboardingController.cs` (the
`FlashAsync` null-localization floor → `KnownTranslationKeys.EnValues`),
`AdminOnboardingViewModel.cs` (`ClosedSteps` — the 7 step routes in order),
`_AdminOnboardingBanner.cshtml` (the `KumunitaPrincipal.IsGlobalAdmin` /
`isGlobalAdmin` / `bannerEligible` / `@if (bannerEligible)` gate + the
`href="/admin/onboarding"` CTA + the `data-dismiss-key` dismissal),
`KnownTranslationKeys.cs` (the `adminonboarding.flash_done` key present in
`EnValues`), `KumunitaPrincipal.cs` (`SubjectId` = the `Kumunita.Sub` claim
the controller tests set), `ClaimTypes` (`Subject = "Kumunita.Sub"`,
`Role = "Kumunita.Role"`), `AdminSiteControllerTests.cs` (the controller-test
shape to mirror), `AdminOnboardingSeederTests.cs` (the `BootStoreAsync`
harness to mirror — **read only, not modified**), the design doc §2.6 (the
16 pinned names) + §2.7 (the acceptance gate)) are **consistent with the
register. No drift pause required; no improvisation** — the one register-vs-
reality nuance (the M30 full-Core suite red on 4 Events-domain flakies) is
**not M30's** (U06's 8 M30 Core tests are green in isolation; the 4 are
pre-existing Events tests U06 did not touch) and is recorded, not silent,
for U07.

**U07 reads only this `## U06` section + its own entry-reads list + the
design doc's `### Run result (M30 acceptance gate — 2026-10-09)` section.**
