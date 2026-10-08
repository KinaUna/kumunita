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
