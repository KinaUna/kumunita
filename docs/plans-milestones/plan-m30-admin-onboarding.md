# M30 — Admin onboarding — sealed unit register

> **Planned.** This is the **lane register** (secondary tier of the milestone's
> three-tier contract) for **M30** — the README / `Milestones.cs` line, verbatim:
> "**Admin onboarding — a guided walk-through for a new GlobalAdmin through
> the most important initial settings (community name, languages, moderation,
> notifications, storage limits, site content, issue escalation).**" M30 is a
> **milestone** (it takes the `M30` letter; the roadmap order is unchanged —
> M31–M34 stay as-is), not a named lane. It is **the M22 onboarding lane
> extended to the admin surface** (the README's own framing): the same shape
> M22 shipped for a new **resident** (a **stateless** guided walk-through that
> **rides the frozen lanes** for every field it surfaces, a **single
> completion-stamp** as the *only* write, a **non-blocking dismissible
> banner**, and a **closed `kw-l` key set** in en/de/fr/da) — now aimed at a
> new **GlobalAdmin** and pointing at the admin surfaces instead of the
> resident's profile/settings. M30 is a **guided shell**, not a new data
> model: it adds **one singleton doc** (`AdminOnboarding`, one `CompletedAt`
> field — the ADR 0005 B / ADR 0150 `LocaleSettings` / `SiteContent` shape),
> **one read + audited-write seam** (`IAdminOnboardingService`), **one
> dedicated GlobalAdmin controller** (`/admin/onboarding`, the
> `AdminSiteController` / `AdminSurfaceLabelsController` shape), **one
> dismissible admin banner** (the M22 `_OnboardingBanner` partial's admin
> sibling), and the **six-member close flip** — but it is a **links-only
> walk-through** (a list of the seven roadmap settings, each linking into the
> existing admin surface that already owns it), **not a new write path**. The
> completion state is a **`null` = not-yet-guided** flag on the singleton —
> a fresh GlobalAdmin sees the banner; a completed one does not.
>
> **U00** verifies the surface (the admin surfaces the walk-through will
> link into, the M22 precedent shapes — the controller / banner /
> closed-`kw-l`-set / completion-stamp — the ADR index — confirm **0153** is
> free) and authors the handoff-note skeleton. **U01/U02** author the
> primary-tier design doc (invariants + FACES + the closed seven-step
> walk-through set + the pinned test names + the acceptance gate + the
> drift guard) and draft **ADR 0153** (the next free number after 0152).
> **U03** implements the Core (`AdminOnboarding` doc +
> `IAdminOnboardingService` + `AdminOnboardingService` +
> `AdminOnboardingDocTypes` + the DI registration). **U04** ships the
> `AdminOnboardingController` (`GET /admin/onboarding` walk-through +
> `POST /admin/onboarding/complete` stamping completion) + the
> `AdminOnboardingViewModel` (the completion-state read + the closed
> seven-step list). **U05** ships `Views/AdminOnboarding/Index.cshtml` +
> the **admin onboarding banner** (the M22 `_OnboardingBanner` shape,
> admin-scope) + the **closed `adminonboarding.*` `kw-l` key set** ×
> en/de/fr/da (author). **U06** runs + records the acceptance gate. **U07**
> flips the close (the `Milestones.cs` / README / `STATUS.md` /
> `ARCHITECTURE.md` / `MilestonesTests.cs` / `WhatsNew.cs` six-member close
> flip).
>
> **Sizing:** units are sized for a **~32K-context fresh agent one at a
> time**, each with its own exit criteria, in the M22 / SITE style (≤ ~5
> files / ~500 LOC, 4–8 entry reads, one build + test run). **No new
> `AccessAction`**, **no new `AccessVia`**, **no new `Decide()` branch**,
> **no new `IAuthorizationService` surface** (the read is a public admin
> surface; the write is the ADR 0050/0150 `GlobalAdmin`-gated single-write-
> lane shape, one `AccessAudit` row per stamp — the `site.save` /
> `signup.set-open` / `timezone.set-default` singleton-toggle shape). **One
> new doc type** (`AdminOnboarding`, a *singleton* — one row per instance,
> `Id = "singleton"` sentinel, the exact `SiteContent` / `LocaleSettings`
> shape, ADR 0005 B / ADR 0150). **One new bounded context**
> (`Kumunita.Core.AdminOnboarding`) + **one new registration surface**
> (`AdminOnboardingDocTypes`). **No EF migration** (a new Marten doc type is
> additive per ADR 0004 §B.1; the delta is applied idempotently at boot).
> **Zero new admin surface for the settings themselves** (the walk-through
> *links into* the existing `/admin/languages` / `/admin/site` /
> `/admin/storage/settings` / … — it adds **one new route**,
> `/admin/onboarding`, for the walk-through + the completion stamp). **No
> roadmap letter moves** (M30 stays the milestone it is; M31–M34 are
> untouched).

## Understanding (one paragraph)

M22 shipped a guided walk-through for a **new resident** — a stateless
`/onboarding` page that links into the seven frozen profile/settings lanes
that already own the fields it surfaces (display name, avatar, language,
time zone, date format, email language, contact), stamps a single
`Profile.OnboardingCompletedAt` completion flag as its *only* write, and
shows a dismissible home/nav banner while that flag is `null`. A new
**GlobalAdmin** who takes over the instance for the first time faces the
*exact same* problem at the admin surface — they must discover, in a
sprawling `/admin/platform` list, the seven most important initial settings
(community name, languages, moderation, notifications, storage limits, site
content, issue escalation), and there is **no guided path** that walks them
through those seven in order, and **no completion record** so a second
GlobalAdmin (or a returning first one) doesn't see the same walk-through
again. M30 adds **one more singleton doc in the SITE shape**
(`AdminOnboarding`, a `CompletedAt` flag) + **one more GlobalAdmin surface
in the SITE shape** (`/admin/onboarding`), and the walk-through's seven
steps **link into the existing admin surfaces** that already own each
setting — the walk-through's *only* write is the completion stamp, and a
`null` completion shows the admin banner on every `/admin/*` page (the M22
`_OnboardingBanner` partial's admin sibling). The completion default is
**`null`** (not-yet-guided), so a fresh instance that never touches the
surface looks exactly the same as it does today (the banner is the
affordance, not a wall); a completed one has the banner gone.

## The one thing every unit must respect

**Admin-onboarding semantics (locked in ADR 0153, U00):**

- **The walk-through is a *guided shell*, not a new data surface (M30·1).**
  Every setting it links into is **already writable** through an existing
  `GlobalAdmin`-gated lane (`/admin/languages` / `/admin/site` /
  `/admin/labels` / `/admin/storage/settings` / `/admin/quiet` /
  `/admin/messaging` / the community-name surface). M30 **rides those
  surfaces verbatim** — it adds **no new write lane** for any of the seven
  settings. The walk-through is the *sequence + the copy + the completion
  record*, not a second write path.
- **The completion state is one singleton doc, `null` = not-yet-guided
  (M30·2).** The `AdminOnboarding` doc is a **singleton** (one row per
  instance, `Id = "singleton"` sentinel, the exact `SiteContent` /
  `LocaleSettings` shape, ADR 0005 B / ADR 0150) with exactly **one**
  additive member — `CompletedAt: DateTimeOffset?`, `null` = the floor
  (banner shows, the walk-through is available), a non-null value =
  completed (banner clears, the walk-through is still reachable but the
  "you haven't finished setup yet" affordance is gone). The doc is
  registered in a new `AdminOnboardingDocTypes.Configure(opts)` surface
  (the `SiteContentDocTypes` parallel surface, ADR 0004 §B.1); the delta is
  applied idempotently at boot. **No EF migration.** The `SiteContent` and
  `LocaleSettings` docs are **untouched** — M30 adds a *new* doc in a *new*
  context, not a new field on an existing one (the ADR 0150 D6 pin, the
  ADR 0006 module-boundary contract).
- **The read is a public admin surface (M30·3).** The completion-state read
  is `GlobalAdmin`-gated by the `[Authorize(Roles = GlobalAdmin)]` on the
  controller (the standard admin-scope gate, not a new authorization
  surface — the thin-token rule, ADR 0001-B), and is **never audited**
  (a read, not an access decision). A missing `AdminOnboarding` row (a
  fresh boot before the seeder ran, or a test construction with no store)
  degrades to **`CompletedAt = null`** (not-yet-guided, the banner shows)
  — never a blank walk-through, never an error.
- **The write is the ADR 0150 single-write-lane shape (M30·4).** One
  `IAdminOnboardingService.CompleteAsync(actorBy)` lane that loads the
  singleton, stamps `CompletedAt = now`, and saves in one session
  (invariant C3); exactly **one** `AccessAudit` row per stamp
  (`Via = Admin`, action `admin_onboarding.complete`, `TargetKind`
  "admin-onboarding" — the `site.save` / `signup.set-open` /
  `timezone.set-default` singleton-toggle shape). The lane **upserts** the
  singleton (the `SiteContent` "one row per instance" shape, ADR 0150 D6);
  **strong consistency** — the new value is live on the very next
  `GetAsync` / banner read.
- **The banner is the *affordance*, and it is non-blocking (M30·5).** A
  dismissible admin banner (the M22 `_OnboardingBanner` partial's admin
  sibling, the `_AdminNav` partial's scope) is rendered on the admin
  surfaces **only while `CompletedAt` is `null`** AND the signed-in actor
  is a `GlobalAdmin` (the M22 `bannerEligible` read + the M29
  `GlobalAdmin`-gated scope). It links to `/admin/onboarding`. It is
  **non-blocking**: the admin sign-in is never gated, the banner is always
  dismissible (the M22 `onboarding-banner.js` `sessionStorage` flag shape —
  never a write to `CompletedAt`), and the `/admin/onboarding` route is
  always reachable for a `GlobalAdmin` regardless of completion state.
- **The closed `adminonboarding.*` `kw-l` key set is parity-pinned in four
  languages (M30·6).** Every new user-visible string M30 introduces is a
  `KnownTranslationKeys` entry present, **non-empty, in all four**
  languages (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests`. The closed set (the seven step
  cards + the title + the intro + the "go to this setting" link + the
  "mark as complete" button + the flash + the banner copy + the banner CTA)
  is authored by U05 and consumed by U04's view model.
- **The walk-through links into the seven roadmap settings (M30·7).** The
  seven step cards are the **closed** set named in the README /
  `Milestones.cs` line — **community name, languages, moderation,
  notifications, storage limits, site content, issue escalation** — each
  linking into the existing admin surface that already owns it (the
  `AdminPlatform` list-group row shape, the M22 "visit this setting"
  link-label idiom). The walk-through is **links-only**: it does **not**
  inline a control for any of the seven (the M22 D3 "rides frozen lanes"
  pin), it does **not** add a per-step POST, it does **not** persist a
  step cursor (the M22 D4 "single stateless page" pin), and it does **not**
  gate the admin surface on completion (the M22 D5 "non-blocking" pin).
- **The `Milestones.cs` / README / `MilestonesTests` trio is untouched
  until the milestone *ships* (M30·8 — U07 owns the close flip).** The
  `WhatsNew.cs` registry gains one new entry (newest-first, the `0.46.0`
  row) naming M30 + ADR 0153 — the M27 "shipped with no entry until caught
  in review" lesson (AGENTS.md) is held.

## Assumptions

- **Scope = the seven roadmap settings + the walk-through shell.** In: the
  `AdminOnboarding` doc (a new `Kumunita.Core.AdminOnboarding` context),
  the `IAdminOnboardingService` + `AdminOnboardingService` (the ADR 0150
  single-write-lane shape), the `AdminOnboardingDocTypes` (the ADR 0004
  §B.1 additive doc type), the `FirstBootSeeder` default (a
  `CompletedAt = null` row — the "not-yet-guided" floor, the
  `SeedSiteContentAsync` create-if-missing / idempotent / never-overwrites
  shape), the `AdminOnboardingController` (`GET /admin/onboarding` +
  `POST /admin/onboarding/complete`), the `AdminOnboardingViewModel`
  (the completion-state read + the closed seven-step list), the
  `Views/AdminOnboarding/Index.cshtml` view, the **admin onboarding
  banner** (the M22 `_OnboardingBanner` partial's admin sibling, the
  `_AdminNav` partial's scope), the closed `adminonboarding.*` `kw-l`
  key set × en/de/fr/da, and the test pins. **Out (named deferrals for
  future lanes, if one comes):** per-step progress / resume-across-sessions
  (M30's page is stateless — the M22 D8·1 deferral, verbatim), a
  "you haven't set X" detection engine (M30 links into the existing
  surfaces and shows a static "visit this setting" hint — the M22 D8·2
  deferral, verbatim), an admin "onboarding completion rate" metric (the
  M13 logging lane, the M22 D8·4 deferral, verbatim), and the
  `Milestones.cs` / README / `MilestonesTests` trio until the milestone
  *ships* (U07 owns it).
- **The seven step cards are the closed set (the register's "one thing"
  section).** The `AdminOnboardingViewModel` carries exactly **seven**
  step entries (one per roadmap setting), each a `(string key, string
  labelKey, string route, string descriptionKey)` tuple, **in the
  README / `Milestones.cs` order** (community name, languages, moderation,
  notifications, storage limits, site content, issue escalation):
  - **Community name** (fallback `adminonboarding.step_communityname`) —
    the community's display name + description (the ADR 0026 / ADR 0053
    community-translation surface — `/admin/languages` is the canonical
    entry, the community name is edited within it).
  - **Languages** (fallback `adminonboarding.step_languages`) —
    `/admin/languages` (the language catalog + the translated UI strings +
    the static pages).
  - **Moderation** (fallback `adminonboarding.step_moderation`) —
    `/admin/announcements/comments` (the announcement-comment moderation
    toggle — the ADR 0101 shape; the closest shipped admin surface for
    "moderation" on the current admin surface — the M29 "label, not
    re-route" pin: the walk-through *links* into the surface, it does
    not *move* it).
  - **Notifications** (fallback `adminonboarding.step_notifications`) —
    `/admin/quiet` (the notification-flush cadence — the quiet-time
    cadence the admin sets for held notification emails; residents set
    their own quiet window on `/settings/quiet`).
  - **Storage limits** (fallback `adminonboarding.step_storage`) —
    `/admin/storage/settings` (the admin-set per-file size limit + the
    per-user total content quota — the M25 upload-limits surface).
  - **Site content** (fallback `adminonboarding.step_sitecontent`) —
    `/admin/site` (the landing surfaces' hero text + the show/hide
    toggles — the ADR 0150 SITE lane).
  - **Issue escalation** (fallback `adminonboarding.step_escalation`) —
    `/admin/announcements` (the announcements surface — the closest
    shipped admin surface for "issue escalation" on the current admin
    surface; the M32 "issue submission & escalation" lane is *not yet
    shipped* (it is `StatusPlanned` on the roadmap), so the walk-through
    links into the announcements surface as a **placeholder** — the M30
    design doc's §drift-guard names this as a **known deferral**, not a
    drift, and a future M32 close-flip will re-point this step card's
    route to the M32 surface).
  The step set is the **complete** admin onboarding surface for the
  current scope — no hidden steps, no reserved steps. A future lane
  **adds** steps (additive per ADR 0004 §B.1 to the `AdminOnboarding`
  doc's field set, if the walk-through ever grows a per-step state), it
  does not re-shape the existing seven.
- **The `AdminOnboarding` doc is a singleton with one field.** The doc
  carries exactly **one** optional `DateTimeOffset?` field (`CompletedAt`),
  defaulting to `null` (= not-yet-guided, the floor). The `Id` is the
  sentinel `"singleton"` (the exact `SiteContent` / `LocaleSettings`
  shape, ADR 0005 B). The field set is the **ceiling** — no field outside
  the field set may appear in the doc (the ADR 0153 D1 pin). A future
  lane **adds** fields (additive per ADR 0004 §B.1), it does not re-shape
  the existing one.
- **The `kw-l` key set is closed and four-language.** The
  `adminonboarding.*` keys are authored by U05 and consumed by U04's view
  model + U05's view + the banner partial. The closed set (the register's
  §"closed `kw-l` key set" table below) is **14 keys**:
  `adminonboarding.title` / `adminonboarding.intro` /
  `adminonboarding.step_communityname` /
  `adminonboarding.step_languages` /
  `adminonboarding.step_moderation` /
  `adminonboarding.step_notifications` /
  `adminonboarding.step_storage` /
  `adminonboarding.step_sitecontent` /
  `adminonboarding.step_escalation` / `adminonboarding.visit` /
  `adminonboarding.complete` / `adminonboarding.flash_done` /
  `adminonboarding.banner.text` + `adminonboarding.banner.action` (the
  banner CTA link label — the M22 D7 "closed set" shape, the 15-key M22
  set collapsed to the admin's 14: the M22 seven step keys → M30's seven,
  the M22 banner two keys → M30's banner two keys, the M22 `visit` /
  `complete` / `flash_done` → M30's `visit` / `complete` / `flash_done`).
  Every key is present, non-empty, in **all four** languages (en/de/fr/da);
  the `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
  closure pins it.
- **The test model is the M22 / SITE shape.** The `Core.Tests` pins
  (the `AdminOnboardingServiceTests` class): (a) a fresh boot has exactly
  one `AdminOnboarding` row (`Id = "singleton"`), (b) `CompletedAt` is
  `null` (not-yet-guided), (c) a second boot is idempotent (no duplicate
  row, no field change), (d) `CompleteAsync` stamps `CompletedAt = now`,
  writes exactly **one** `AccessAudit` row (`Via = Admin`, action
  `admin_onboarding.complete`, `TargetKind` "admin-onboarding"), and is
  **strongly consistent** (the new value is live on the very next
  `GetAsync`), (e) `CompleteAsync` **upserts** the singleton (no
  duplicate row, the M29·6 / ADR 0150 D6 pin). The `Web.Tests` pins
  (the `AdminOnboardingControllerTests` class): (a) `GET` seeds the view
  model with the current `CompletedAt` + the closed seven-step list, (b)
  `POST /admin/onboarding/complete` stamps `CompletedAt = now` + writes
  exactly **one** `AccessAudit` row, (c) `GET` + `POST` are
  `GlobalAdmin`-gated (a non-`GlobalAdmin` is denied, the M29·7 pin).
  The `Web.Tests` pins (the `AdminOnboardingBannerTests` class): (a) the
  banner renders **iff** the signed-in actor is a `GlobalAdmin` AND
  `CompletedAt` is `null` (the M30·5 pin), (b) the banner does **not**
  render for a non-`GlobalAdmin` (even if `CompletedAt` is `null`), (c)
  the banner does **not** render when `CompletedAt` is non-null (even if
  the actor is a `GlobalAdmin`), (d) the banner link points to
  `/admin/onboarding` (the M22 `onboarding.banner.action` idiom).
- **The PowerShell / terminal constraints in `AGENTS.md` and
  `copilot-instructions.md` bind** — no here-strings, no multi-line
  terminal commands, `$`-variables don't survive between commands, the
  `dotnet test` discovery bug on this machine (use the in-process
  `dotnet exec tests\…\bin\Debug\net10.0\*.dll` path).

## Approach

One track, **data + walk-through shell + banner + admin surface**,
sequenced. **U00** verifies the surface (the seven admin surfaces the
walk-through will link into, the M22 precedent shapes, the ADR index) +
authors the handoff-note skeleton. **U01/U02** author the primary-tier
design doc (invariants + FACES + the closed seven-step walk-through set +
the pinned test names + the acceptance gate + the drift guard) and draft
**ADR 0153**. **U03** implements the Core (`AdminOnboarding` doc +
`IAdminOnboardingService` + `AdminOnboardingService` +
`AdminOnboardingDocTypes` + the DI registration + the `FirstBootSeeder`
default + the seeder pin). **U04** ships the `AdminOnboardingController`
+ the `AdminOnboardingViewModel` (the M22 `OnboardingController` /
`OnboardingViewModel` shape, admin-scope). **U05** ships
`Views/AdminOnboarding/Index.cshtml` + the **admin onboarding banner**
(the M22 `_OnboardingBanner` partial's admin sibling) + the **closed
`adminonboarding.*` `kw-l` key set** × en/de/fr/da (author). **U06** runs
+ records the acceptance gate. **U07** flips the close (the six-member
close flip).

Every code unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug`). The last unit (U07) appends the final handoff section so the
milestone is honest.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U07
below), one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/m30-admin-onboarding-design.md`,
  U01/U02 author) — pins the invariants (M30·1–M30·8), the FACES
  (M30-1–M30-8), the closed seven-step walk-through set, the read-seam
  contract, the write-lane contract, the banner-eligibility rule, the
  pinned test names, the acceptance gate, and the drift guard.
- **Secondary — this file** (`docs/plans-milestones/plan-m30-admin-onboarding.md`)
  — the unit registry with each unit's deliverables and exit criteria.
  (The M22 / SITE flat-lane convention — the main plan sits at the top of
  `docs/plans-milestones/`, the unit plans sit in
  `docs/plans-milestones/in-progress/` as `m30-u00.md` … `m30-u07.md`, and
  move to `docs/plans-milestones/done/m30/` as each unit completes.)
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m30-handoff-notes.md`) — one section
  per unit, appended (never rewritten). Each unit writes exactly one short
  section before it exits; the next unit reads only that section + its own
  entry-reads list.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal
file list, 4–8 files < ~300 lines each, no full-repo scan; the design-doc
section cited is named); **Deliverables** (a closed set of new/modified
files, ≤ ~5 files / ~500 LOC, no misc cleanups); **Exit** (`dotnet build
Kumunita.slnx -c Debug` green for the touched projects; handoff-note entry
appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard;
(3) never introduces a test whose exact name is not in the §pinned-test-names
list; (4) never re-shapes the `AdminOnboarding` field set (the ADR 0153 D1
pin — the one optional `DateTimeOffset?` field) outside the design doc;
(5) never re-shapes a route (the "walk-through is a guided shell, not a
re-route" pin — the seven admin routes are unchanged, only the
walk-through *links into* them); (6) never touches the `SiteContent` doc or
the `LocaleSettings` doc (the ADR 0150 D6 / ADR 0006 module-boundary pin —
M30 adds a *new* doc in a *new* context); (7) never adds a per-step POST,
a persisted step cursor, or a server-session step state (the M22 D4 "single
stateless page" pin); (8) never adds a second write lane for any of the
seven settings (the M22 D3 "rides frozen lanes" pin — the walk-through's
*only* write is `CompleteAsync`); (9) if entry reads reveal the design doc
is out of date, the unit pauses and records `## U<m> — Drift pause` in the
handoff note.

---

## Units (8 total: U00–U07)

### U00 — Kickoff verification + handoff-note skeleton

- **Goal:** verify the surface (the seven admin surfaces the walk-through
  will link into, the M22 precedent shapes, the ADR index — confirm
  **0153** is free) and author the handoff-note skeleton (the "Milestone
  open" section). **No code, no build.**
- **Entry reads:** `src/Kumunita.Web/Views/Admin/Platform.cshtml` (the
  `/admin/platform` list-group rows — the seven admin surfaces the
  walk-through will link into: `/admin/languages` [community name +
  languages], `/admin/announcements/comments` [moderation], `/admin/quiet`
  [notifications], `/admin/storage/settings` [storage limits], `/admin/site`
  [site content], `/admin/announcements` [issue-escalation placeholder]);
  `src/Kumunita.Web/Controllers/OnboardingController.cs` (the M22
  `OnboardingController` — the stateless `/onboarding` page + the
  `CompleteOnboardingAsync` single-write-lane + the `FlashAsync` idiom);
  `src/Kumunita.Web/Views/Shared/_OnboardingBanner.cshtml` (the M22
  `_OnboardingBanner` partial — the dismissible banner shape, the
  `sessionStorage` flag, the `bannerEligible` read);
  `src/Kumunita.Core/SiteContent/SiteContent.cs` +
  `src/Kumunita.Core/SiteContent/SiteContentService.cs` (the SITE
  singleton + the best-effort read + the single audited write lane — the
  shape M30 mirrors); `docs/adr/0150-site-content-customization.md` (the
  precedent ADR this milestone builds on); `docs/adr/README.md` (the ADR
  index — confirm **0153** is free after the 0152 row; 0152 = M29,
  0151 = M28, 0150 = SITE).
- **Deliverables (1 file, new):**
  `docs/plans-milestones/in-progress/m30-handoff-notes.md` — the **skeleton
  only** (the header + the "Milestone open" section + the
  `<!-- U00 appends its section below this line. One ## section per unit,
  in order (U00, U01, … U07). Never rewrite a prior section. -->` marker).
  The skeleton mirrors the `m29-handoff-notes.md` /
  `m22-onboarding-handoff-notes.md` shape (the "Milestone open" section
  names the register, the design doc, the ADR, the scope, the out-of-scope
  deferrals, and the frozen base). **No** `## U<m> —` section yet (U00
  appends its own section after this one).
- **Exit:** the handoff-note skeleton is present. The `## Milestone open`
  section names (a) the seven admin surfaces (the `/admin/languages` /
  `/admin/announcements/comments` / `/admin/quiet` /
  `/admin/storage/settings` / `/admin/site` / `/admin/announcements` rows
  in `/admin/platform`), (b) the M22 precedent shapes (the
  `OnboardingController` + the `_OnboardingBanner` partial + the
  `CompleteOnboardingAsync` single-write-lane + the closed `onboarding.*`
  `kw-l` set), (c) the frozen base (ADR 0005 B `LocaleSettings` singleton
  shape, ADR 0150 `SiteContent` shape, ADR 0050 single-write-lane shape —
  **unchanged**), (d) the new invariants (M30·1–M30·8), (e) the **ADR
  0153** (the next free number after 0152 — the ADR index in
  `docs/adr/README.md` confirms 0152 is the current highest). Handoff note:
  a `## U00 — Kickoff verified` section with the seven admin surfaces (by
  route), the M22 precedent shapes (by file), the ADR number (0153) + the
  precedent ADR list (0150 / 0050 / 0005 B). Move this unit plan
  `in-progress/` → `done/` (move **last**). `git status` clean except the
  one new handoff-note file.

### U01 — Design doc Part 1 (invariants + FACES + closed seven-step set)

- **Goal:** author `docs/design/m30-admin-onboarding-design.md` Part 1 — the
  value chain, the **invariants (M30·1–M30·8)**, the **FACES
  (M30-1–M30-8)**, and the **assumptions** (the ADR 0153 walk-through
  semantics + the M22 non-conflict). Mirrors the `site-content-design.md` /
  `m22-onboarding-design.md` shape. **No code, no build.**
- **Entry reads:** U00's handoff-note `## U00 — Kickoff verified` section (the
  seven admin surfaces + the M22 precedent shapes),
  `docs/design/m22-onboarding-design.md` (the M22 design doc — the
  FACES/invariant template to emulate, the D1–D8 decision set, the
  C-M22·1–C-M22·6 invariants, the F1–F5 FACES, the §gate GATE-1–GATE-6),
  `docs/design/site-content-design.md` (the SITE design doc — the
  singleton-doc + read-seam + write-lane shape),
  `docs/philosophy/templates/design-doc.md` (the required section set),
  `src/Kumunita.Web/Views/Admin/Platform.cshtml` (the seven admin
  surfaces — the exact routes the walk-through will link into),
  `src/Kumunita.Web/Views/Onboarding/Index.cshtml` (the M22 walk-through
  page — the seven step-card shape, the "visit this setting" link-label
  idiom, the finish/skip action shape).
- **Deliverables (1 file, new):**
  `docs/design/m30-admin-onboarding-design.md` (~250 lines). Sections:
  - `## Value chain` — the M22 lane shipped a guided walk-through for a new
    **resident**; M30 ships the **same shape** for a new **GlobalAdmin** —
    the admin can now walk through the seven most important initial
    settings (community name, languages, moderation, notifications, storage
    limits, site content, issue escalation) in order, link into the existing
    admin surface that already owns each one, stamp a single completion
    flag, and see the banner clear.
  - `## Context` — the gap (a new GlobalAdmin who takes over the instance
    for the first time must discover, in a sprawling `/admin/platform`
    list, the seven most important initial settings — there is no guided
    path that walks them through those seven in order, and no completion
    record so a second GlobalAdmin doesn't see the same walk-through
    again); the precedent shapes (ADR 0005 B `LocaleSettings` singleton,
    ADR 0150 `SiteContent` shape, ADR 0050 single-write-lane, the M22
    D1–D8 decision set); the constraints that still bind (ADR 0004 §B.1
    additive doc type, ADR 0006 module-boundary contract, ADR 0015 D1
    `kw-l` provider-floor discipline, the M22 / SITE test model, the
    `Milestones.cs` / README / `MilestonesTests` close-flip trio).
  - `## Scope` — **In:** the `AdminOnboarding` doc (the new
    `Kumunita.Core.AdminOnboarding` context), the
    `IAdminOnboardingService` + `AdminOnboardingService` (the ADR 0150
    single-write-lane shape), the `AdminOnboardingDocTypes` (the ADR 0004
    §B.1 additive doc type), the `FirstBootSeeder` default (a
    `CompletedAt = null` row — the "not-yet-guided" floor), the
    `AdminOnboardingController` (`GET /admin/onboarding` +
    `POST /admin/onboarding/complete`), the `AdminOnboardingViewModel`
    (the completion-state read + the closed seven-step list), the
    `Views/AdminOnboarding/Index.cshtml` view, the **admin onboarding
    banner** (the M22 `_OnboardingBanner` partial's admin sibling), the
    closed `adminonboarding.*` `kw-l` key set × en/de/fr/da, and the test
    pins. **Out (named deferrals for future lanes, if one comes):**
    per-step progress / resume-across-sessions (the M22 D8·1 deferral,
    verbatim), a "you haven't set X" detection engine (the M22 D8·2
    deferral, verbatim), an admin "onboarding completion rate" metric (the
    M22 D8·4 deferral, verbatim), and the `Milestones.cs` / README /
    `MilestonesTests` trio until the milestone *ships* (U07 owns it).
  - `## Invariants (pinned for M30)` — **M30·1–M30·8**, each with a
    one-line M30 note (the register's "one thing" section, restated):
    - **M30·1** — the walk-through is a *guided shell*, not a new data
      surface (every setting it links into is already writable through an
      existing `GlobalAdmin`-gated lane; M30 adds **no new write lane** for
      any of the seven settings).
    - **M30·2** — the completion state is one singleton doc, `null` =
      not-yet-guided (the `AdminOnboarding` doc is a singleton, one row per
      instance, `Id = "singleton"` sentinel, the exact `SiteContent` /
      `LocaleSettings` shape, ADR 0005 B / ADR 0150; the doc is registered
      in a new `AdminOnboardingDocTypes` surface; no EF migration; the
      `SiteContent` + `LocaleSettings` docs are untouched).
    - **M30·3** — the read is a public admin surface (the
      completion-state read is `GlobalAdmin`-gated by the
      `[Authorize(Roles = GlobalAdmin)]` on the controller — the standard
      admin-scope gate, not a new authorization surface; never audited; a
      missing row degrades to `CompletedAt = null`).
    - **M30·4** — the write is the ADR 0150 single-write-lane shape (one
      `CompleteAsync` lane, one `AccessAudit` row, `Via = Admin`, action
      `admin_onboarding.complete`, `TargetKind` "admin-onboarding"; strong
      consistency).
    - **M30·5** — the banner is the *affordance*, and it is non-blocking
      (a dismissible admin banner is rendered on the admin surfaces **only
      while `CompletedAt` is `null`** AND the signed-in actor is a
      `GlobalAdmin`; it links to `/admin/onboarding`; the admin sign-in is
      never gated; the banner is always dismissible; the
      `/admin/onboarding` route is always reachable for a `GlobalAdmin`).
    - **M30·6** — the closed `adminonboarding.*` `kw-l` key set is
      parity-pinned in four languages (every new user-visible string M30
      introduces is a `KnownTranslationKeys` entry present, non-empty, in
      all four languages en/de/fr/da, pinned by
      `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`).
    - **M30·7** — the walk-through links into the seven roadmap settings
      (the seven step cards are the **closed** set named in the README /
      `Milestones.cs` line — community name, languages, moderation,
      notifications, storage limits, site content, issue escalation — each
      linking into the existing admin surface that already owns it; the
      walk-through is **links-only**: it does **not** inline a control,
      does **not** add a per-step POST, does **not** persist a step
      cursor, does **not** gate the admin surface on completion).
    - **M30·8** — the `Milestones.cs` / README / `MilestonesTests` trio is
      untouched until the milestone *ships* (U07 owns the close flip); the
      `WhatsNew.cs` registry gains one new entry (newest-first, the
      `0.46.0` row) naming M30 + ADR 0153.
  - `## FACES (pinned, 8)` — **M30-1–M30-8**, each bound to an invariant:
    - **M30-1** — a fresh `GlobalAdmin` sees the banner on every
      `/admin/*` page (the banner renders iff `CompletedAt` is `null` AND
      the actor is a `GlobalAdmin`) (M30·5)
    - **M30-2** — the `/admin/onboarding` page walks the seven settings in
      the README / `Milestones.cs` order (community name → languages →
      moderation → notifications → storage limits → site content → issue
      escalation), each step linking into the existing admin surface that
      already owns it (M30·1, M30·7)
    - **M30-3** — the "mark as complete" button stamps `CompletedAt = now`
      + writes exactly one `AccessAudit` row (`Via = Admin`, action
      `admin_onboarding.complete`, `TargetKind` "admin-onboarding")
      (M30·4)
    - **M30-4** — the banner clears on the next read after
      `CompletedAt` is stamped (strong consistency — M30·4, M30·5)
    - **M30-5** — a non-`GlobalAdmin` never sees the banner (even if
      `CompletedAt` is `null`) (M30·3, M30·5)
    - **M30-6** — the walk-through's *only* write is `CompleteAsync` (no
      per-step POST, no persisted step cursor, no server-session step
      state) (M30·1, M30·7)
    - **M30-7** — the closed `adminonboarding.*` `kw-l` key set is
      parity-pinned in four languages (every key is present, non-empty, in
      en/de/fr/da) (M30·6)
    - **M30-8** — the `AdminOnboarding` doc is a singleton (one row per
      instance, `Id = "singleton"` sentinel); the `SiteContent` +
      `LocaleSettings` docs are untouched (M30·2)
- **Exit:** the file exists with all sections. **No build.**
  Handoff note: a `## U01 — design doc Part 1` section listing the **8
  invariants** (by id) and the **8 FACES** (M30-1–M30-8) so U02 can pin
  them by id. Move this unit plan `in-progress/` → `done/` (move **last**).

### U02 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard) + ADR 0153

- **Goal:** append `## Seams & contracts (Part 2, written by U02)` to the design
  doc — the exact `AdminOnboarding` field set, the read-seam contract, the
  write-lane contract, the banner-eligibility rule, the **pinned seam-test
  names**, the **acceptance gate**, and the **drift-guard**. Plus **ADR
  0153** (draft) + one `docs/adr/README.md` index row. **No code, no
  build.**
- **Entry reads:** U01's Part 1 (the invariant table is the primary source),
  `docs/design/site-content-design.md` §Pinned contract (the shape to
  emulate), `src/Kumunita.Core/SiteContent/SiteContentService.cs` (the
  best-effort read + the single audited write lane — the shape M30
  mirrors), `src/Kumunita.Web/Controllers/AdminSiteController.cs` (the
  ADR 0150 `/admin/site` surface — the `GlobalAdmin`-gated dedicated
  controller + the `/admin/platform` link shape),
  `src/Kumunita.Web/Controllers/OnboardingController.cs` (the M22
  `OnboardingController` — the stateless `/onboarding` page + the
  `CompleteOnboardingAsync` single-write-lane + the `FlashAsync` idiom),
  `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (the seeder shape — the
  `SeedSiteContentAsync` create-if-missing, idempotent, never-overwrites
  pattern), `docs/adr/0150-site-content-customization.md` +
  `docs/adr/README.md` (the ADR 0153 shape to emulate + the ADR index —
  confirm 0153 is free).
- **Deliverables (3 files, 2 new + 1 modify):**
  1. **`docs/design/m30-admin-onboarding-design.md`** (append Part 2).
     Sub-sections:
     - `### 2.1 frozen base (unchanged)` — ADR 0005 B `LocaleSettings`
       singleton shape + ADR 0150 `SiteContent` shape + ADR 0050
       single-write-lane shape + ADR 0004 §B.1 additive doc type + ADR 0006
       module-boundary contract + ADR 0015 D1 `kw-l` provider-floor
       discipline + the M22 / SITE test model — all **keep binding
       unchanged**.
     - `### 2.2 the `AdminOnboarding` field set (exact)` — the **one**
       optional `DateTimeOffset?` field (`CompletedAt`), defaulting to
       `null` (= not-yet-guided, the floor). The `Id` is the sentinel
       `"singleton"` (the exact `SiteContent` / `LocaleSettings` shape,
       ADR 0005 B). The field set is the **ceiling** (the ADR 0153 D1
       pin).
     - `### 2.3 the read-seam contract (exact C#)` — the
       `IAdminOnboardingService.GetAsync(CancellationToken ct)` read
       (returns the singleton's `CompletedAt` value, **best-effort** — a
       missing row / read failure degrades to `null` = not-yet-guided —
       the ADR 0050 `IsSignupOpenAsync` best-effort shape, the in-code
       fallback is the `CompletedAt = null` singleton). The banner partial
       + the `/admin/onboarding` view call **one** helper (the read-seam)
       so they resolve to the **same** value (M30·5). The read is
       **never audited** (M30·3).
     - `### 2.4 the write-lane contract (exact C#)` — the
       `IAdminOnboardingService.CompleteAsync(string actorBy,
       CancellationToken ct)` write (loads the singleton, stamps
       `CompletedAt = now`, saves in one session — invariant C3; exactly
       one `AccessAudit` row per stamp, `Via = Admin`, action
       `admin_onboarding.complete`, `TargetKind` "admin-onboarding" — the
       `site.save` shape; the lane upserts the singleton (the
       `SiteContent` "one row per instance" shape, ADR 0150 D6); strong
       consistency — the new value is live on the very next `GetAsync` /
       banner read). The `AdminOnboardingController.Complete` action is
       the thin wrapper (the `AdminSiteController.SaveHome` shape — the
       `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST, the
       `TempData["info"]` flash, the `RedirectToAction(nameof(Index))`
       redirect).
     - `### 2.5 the banner-eligibility rule (exact)` — the banner renders
       **iff** the signed-in actor is a `GlobalAdmin` AND
       `CompletedAt` is `null` (the M22 `bannerEligible` read + the M29
       `GlobalAdmin`-gated scope). The banner is **non-blocking**: the
       admin sign-in is never gated, the banner is always dismissible
       (the M22 `onboarding-banner.js` `sessionStorage` flag shape — never
       a write to `CompletedAt`), and the `/admin/onboarding` route is
       always reachable for a `GlobalAdmin` regardless of completion
       state. The banner link points to `/admin/onboarding` (the M22
       `onboarding.banner.action` idiom).
     - `### 2.6 the pinned seam-test names (exact)` — the `Core.Tests`
       pins (the `AdminOnboardingServiceTests` class — the
       `GetAsync_MissingStore_ReturnsNull` /
       `GetAsync_MissingRow_ReturnsNull` /
       `CompleteAsync_WritesOneAccessAuditRow` /
       `CompleteAsync_StrongConsistency_LiveOnNextGetAsync` /
       `CompleteAsync_UpsertsSingleton_NoDuplicateRow` pins) + the
       `Core.Tests` seeder pins (the `AdminOnboardingSeederTests` class —
       the `FreshBoot_HasExactlyOneAdminOnboardingRow` /
       `FreshBoot_CompletedAtIsNull` /
       `SecondBoot_IsIdempotent_NoDuplicateRow` pins) + the `Web.Tests`
       pins (the `AdminOnboardingControllerTests` class — the
       `GET_SeesCurrentCompletedAt` /
       `POST_Complete_StampsCompletedAt_WritesOneAccessAuditRow` /
       `GET_NonGlobalAdmin_IsDenied` / `POST_NonGlobalAdmin_IsDenied`
       pins) + the `Web.Tests` pins (the
       `AdminOnboardingBannerTests` class — the
       `BannerRenders_ForGlobalAdmin_WhenNotCompleted` /
       `BannerDoesNotRender_ForNonGlobalAdmin` /
       `BannerDoesNotRender_WhenCompleted` /
       `BannerLinkPointsToAdminOnboarding` pins).
     - `### 2.7 the acceptance gate (exact)` — the
       `dotnet build Kumunita.slnx -c Debug` green + the
       `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
       green + the `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
       green + the `KwLRegistryConsistencyTests` /
       `KnownTranslationKeys_ParityTests` pins green (the registry
       entries are untouched) + the `SiteContentServiceTests` /
       `AdminSiteControllerTests` pins green (the SITE precedent shapes
       are unchanged) + the `OnboardingControllerTests` (M22) pins green
       (the M22 precedent shapes are unchanged) + the `MilestonesTests`
       pin green (the order + single-in-progress pin is intact) + the
       `WhatsNewTests` pin green (the new `0.46.0` entry is present,
       newest-first).
     - `### 2.8 the drift guard (exact)` — the `AdminOnboarding` field
       set is the **ceiling** (the one optional `DateTimeOffset?` field —
       no field outside the field set may appear in the doc, the ADR 0153
       D1 pin); the `SiteContent` + `LocaleSettings` docs are
       **untouched** (the ADR 0150 D6 / ADR 0006 module-boundary pin —
       M30 adds a *new* doc in a *new* context); the seven admin routes
       are **unchanged** (the "walk-through is a guided shell, not a
       re-route" pin — the walk-through *links into* the surfaces, it
       does not *move* them); the `kw-l` registry entries are the
       **floor** (the `adminonboarding.*` keys stay, the ADR 0153 D3 pin);
       the M22 `Profile.OnboardingCompletedAt` field is **untouched**
       (the M22 / M30 distinction — M22 is resident-scope, M30 is
       admin-scope, the two completion flags are independent).
  2. **`docs/adr/0153-admin-onboarding.md`** (new, Status: Accepted) — the
     ADR 0153 (the next free number after 0152 — the ADR index in
     `docs/adr/README.md` confirms 0152 is the current highest). Sections:
     `## Context` (a new GlobalAdmin who takes over the instance for the
     first time must discover, in a sprawling `/admin/platform` list, the
     seven most important initial settings — community name, languages,
     moderation, notifications, storage limits, site content, issue
     escalation — and there is no guided path that walks them through
     those seven in order, and no completion record so a second
     GlobalAdmin doesn't see the same walk-through again); `## Decision`
     (the 8 decisions — the walk-through is a guided shell, not a new data
     surface, the completion state is one singleton doc `null` =
     not-yet-guided, the read is a public admin surface, the write is the
     ADR 0150 single-write-lane shape, the banner is non-blocking, the
     closed `adminonboarding.*` `kw-l` key set is parity-pinned in four
     languages, the walk-through links into the seven roadmap settings,
     the `AdminOnboarding` doc is a singleton in a new context);
     `## Consequences` (the seven admin surfaces are now walk-able in
     order; the `/admin/onboarding` surface is the GlobalAdmin's
     walk-through page; the admin banner is the affordance; the
     `SiteContent` + `LocaleSettings` docs are untouched; the M22
     `Profile.OnboardingCompletedAt` field is untouched; the
     `Milestones.cs` / README / `MilestonesTests` trio is untouched until
     the milestone *ships*; the `WhatsNew.cs` registry gains one new
     entry (the `0.46.0` row); the per-step progress / resume-across-
     sessions / "you haven't set X" detection / admin completion-rate
     metric are named deferrals).
  3. **`docs/adr/README.md`** (modify) — the ADR index gains one row:
     `| 0153 | Admin onboarding: a guided walk-through for a new GlobalAdmin through the seven most important initial settings (a `AdminOnboarding` singleton on the M22 / SITE lane's shape — a links-only walk-through, not a new write path; additive on 0150 + 0004 §B.1 + 0005 B + 0006 + 0132) | Accepted |`.
- **Exit:** the design doc Part 2 is present with all sub-sections. The ADR
  0153 is present (Status: Accepted). The ADR index has the 0153 row.
  **No build.** Handoff note: a `## U02 — design doc Part 2 + ADR 0153`
  section listing the **`AdminOnboarding` field set** (the one
  `CompletedAt` field), the **pinned test names** (by class + method), the
  **acceptance gate** (the exact command list), and the **ADR 0153**
  number (0153) + the **ADR index update** (the `docs/adr/README.md`
  table gained the `0153` row). Move this unit plan `in-progress/` →
  `done/` (move **last**).

### U03 — Core: `AdminOnboarding` doc + `IAdminOnboardingService` + `AdminOnboardingService` + `AdminOnboardingDocTypes` + DI + seeder

- **Goal:** implement the Core — the `AdminOnboarding` doc (the one optional
  `DateTimeOffset?` field, defaulting to `null`), the
  `IAdminOnboardingService` interface (the `GetAsync` / `CompleteAsync`
  shape), the `AdminOnboardingService` implementation (the ADR 0150
  single-write-lane shape, the `AccessAudit` row), the
  `AdminOnboardingDocTypes` (the ADR 0004 §B.1 additive doc type), the DI
  registration, the `FirstBootSeeder` default (a `CompletedAt = null`
  row — the "not-yet-guided" floor), and the seeder pin. **No Web change,
  no view change, no admin surface.**
- **Entry reads:** U02's handoff-note `## U02 — design doc Part 2 + ADR 0153`
  section (the `AdminOnboarding` field set + the read-seam contract + the
  write-lane contract), `src/Kumunita.Core/SiteContent/SiteContent.cs`
  (the SITE singleton — the `Id = "singleton"` sentinel shape, the
  field-set ceiling), `src/Kumunita.Core/SiteContent/SiteContentService.cs`
  (the best-effort read + the single audited write lane — the shape M30
  mirrors), `src/Kumunita.Core/SiteContent/SiteContentDocTypes.cs` (the
  `opts.Schema.For` shape — the M30 `AdminOnboardingDocTypes` mirrors
  this), `src/Kumunita.Core/DependencyInjection.cs` (the DI registration
  — the `AddTransient<ISiteContentService>` shape),
  `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (the seeder shape —
  the `SeedSiteContentAsync` create-if-missing, idempotent,
  never-overwrites pattern),
  `docs/design/m30-admin-onboarding-design.md` §2.2–§2.4 (the exact
  `AdminOnboarding` shape + the read/write contracts).
- **Deliverables (6 files, new / modified + 1 test):**
  1. **`src/Kumunita.Core/AdminOnboarding/AdminOnboarding.cs`** (new) —
     the `AdminOnboarding` doc (the one optional `DateTimeOffset?` field
     `CompletedAt`, defaulting to `null`, the `Id = "singleton"`
     sentinel). The field set is the register's "one thing" section —
     `CompletedAt` (default `null` = not-yet-guided, the floor). A
     `Complete()` helper (stamps `CompletedAt = DateTimeOffset.UtcNow`).
  2. **`src/Kumunita.Core/AdminOnboarding/IAdminOnboardingService.cs`**
     (new) — the `IAdminOnboardingService` interface (the
     `GetAsync(ct)` read — best-effort, never audited, a missing row
     degrades to `null` — + the `CompleteAsync(actorBy, ct)` write — the
     ADR 0150 single audited write-lane shape).
  3. **`src/Kumunita.Core/AdminOnboarding/AdminOnboardingService.cs`**
     (new) — the `AdminOnboardingService` implementation. `GetAsync`:
     load the singleton (best-effort — a missing row / read failure
     degrades to `null` = not-yet-guided), return the `CompletedAt`
     value. `CompleteAsync`: the ADR 0150 single audited write-lane
     shape — one write session, the doc + exactly one `AccessAudit` row
     (`Via = Admin`, action `admin_onboarding.complete`, `TargetKind`
     "admin-onboarding") commit together (invariant C3, strong
     consistency C4); the lane upserts the singleton — it never creates a
     second row (M30·2).
  4. **`src/Kumunita.Core/AdminOnboarding/AdminOnboardingDocTypes.cs`**
     (new) — `public static class AdminOnboardingDocTypes { public static
     void Configure(StoreOptions opts) { opts.Schema.For<AdminOnboarding>();
     } }` — the `SiteContentDocTypes` shape, verbatim.
  5. **`src/Kumunita.Core/DependencyInjection.cs`** (modify) — add the
     `AdminOnboardingService` registration:
     `services.AddTransient<IAdminOnboardingService>(sp => new
     AdminOnboardingService(sp.GetRequiredService<IDocumentStore>()));` —
     mirror the `ISiteContentService` registration shape. **Plus** the
     `AdminOnboardingDocTypes.Configure(opts)` call in both boot paths
     (the dev-loop in `Program.cs` and the all-env `SchemaBootstrap`) —
     the `SiteContentDocTypes.Configure` neighbor.
  6. **`src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs`** (modify) — add a
     `SeedAdminOnboardingAsync` step (the `SeedSiteContentAsync` shape —
     create-if-missing, idempotent, never overwrites a resident edit): on
     a fresh boot, store exactly one `AdminOnboarding` row
     (`Id = "singleton"`, `CompletedAt = null`). A warm boot is a no-op
     (the row exists, no field change).
  7. **`tests/Kumunita.Core.Tests/AdminOnboardingSeederTests.cs`** (new)
     — the seeder pin (the M29 `SurfaceLabelsSeederTests` / M22
     `OnboardingSeederTests` shape): (a) a fresh boot has exactly one
     `AdminOnboarding` row (`Id = "singleton"`), (b) `CompletedAt` is
     `null` (not-yet-guided), (c) a second boot is idempotent (no
     duplicate row, no field change).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green.
  `AdminOnboarding.cs` exists; the `AdminOnboardingService` +
  `AdminOnboardingDocTypes` compile. **No new test** beyond the seeder
  pin (U06's service tests are the first M30 service tests). Handoff note:
  a `## U03 — AdminOnboarding Core` section — (a) the one field name
  (`CompletedAt`), (b) the `AdminOnboardingDocTypes` line (one
  `.Schema.For` call), (c) the two boot-path lines added (file + line
  numbers), (d) the `FirstBootSeeder` step (the `SeedAdminOnboardingAsync`
  line), (e) any compile warnings on the new types. Move this unit plan
  `in-progress/` → `done/` (move **last**).

### U04 — `AdminOnboardingController` (`GET /admin/onboarding` + `POST /admin/onboarding/complete`) + the `AdminOnboardingViewModel`

- **Goal:** ship the stateless `/admin/onboarding` page's **controller + view
  model**: a `GET` that composes the read-seam (the `CompletedAt` state +
  the closed seven-step list) and a `POST /admin/onboarding/complete`
  that stamps completion through **U03's** `CompleteAsync` lane — with
  **zero new field-write lanes** (M30·1), **zero new authorization surface**
  (M30·3), and a **non-blocking** shape (M30·5). **No views, no banner, no
  `kw-l` keys** (U05 owns those; U04 *consumes* U05's keys in its view
  model). **Exit: `dotnet build` clean + `Kumunita.Web.Tests` green.**
- **Entry reads:** U03's handoff-note `## U03 — AdminOnboarding Core`
  section (the `AdminOnboarding` field set + the read-seam contract + the
  write-lane contract), `src/Kumunita.Web/Controllers/AdminSiteController.cs`
  (the ADR 0150 `/admin/site` surface — the `GlobalAdmin`-gated dedicated
  controller + the `[ValidateAntiForgeryToken]` POST + the
  `TempData["info"]` flash + the `RedirectToAction(nameof(Index))`
  redirect), `src/Kumunita.Web/Controllers/OnboardingController.cs` (the
  M22 `OnboardingController` — the stateless `/onboarding` page + the
  `CompleteOnboardingAsync` single-write-lane + the `FlashAsync` idiom +
  the `KumunitaPrincipal.SubjectId(user)` read),
  `src/Kumunita.Core/AdminOnboarding/IAdminOnboardingService.cs` (U03's
  read-seam + write-lane — the `GetAsync` / `CompleteAsync` shape),
  `docs/design/m30-admin-onboarding-design.md` §2.3–§2.5 (the exact
  `AdminOnboardingViewModel` shape + the banner-eligibility rule).
- **Deliverables (2 files, new + 1 modify):**
  1. **`src/Kumunita.Web/Controllers/AdminOnboardingController.cs`**
     (new) — the `AdminOnboardingController` (the ADR 0150
     `AdminSiteController` shape, the M22 `OnboardingController` shape):
     `[Route("admin/onboarding")]` + `[Authorize(Roles =
     Kumunita.Core.Identity.Roles.GlobalAdmin)]`. Two actions:
     - `GET /admin/onboarding` (`Index`) — composes the read-seam
       (`IAdminOnboardingService.GetAsync`) for the `CompletedAt` state +
       the closed seven-step list (the `AdminOnboardingViewModel.Steps`,
       the register's §"closed `kw-l` key set" table, the seven
       `(string key, string labelKey, string route, string
       descriptionKey)` tuples in the README / `Milestones.cs` order).
       The model is built from the **admin-scope read** (the
       `CompletedAt` value + the static seven-step list — the M22
       `OnboardingViewModel` shape, admin-scope).
     - `POST /admin/onboarding/complete` (`Complete`) — the **one** write
       action: calls U03's `IAdminOnboardingService.CompleteAsync` for
       the signed-in `GlobalAdmin` and redirects to `/admin/onboarding`
       with the `adminonboarding.flash_done` flash (the
       `AdminSiteController.FlashAsync` idiom — resolve a `kw-l` key in
       the admin's effective language, fall back to the
       `KnownTranslationKeys.EnValues` floor). `[ValidateAntiForgeryToken]`.
     The controller's **only** write is `CompleteAsync` (M30·1); every
     step it surfaces **links into** the existing admin surface that
     already owns it (the seven routes — `/admin/languages` /
     `/admin/announcements/comments` / `/admin/quiet` /
     `/admin/storage/settings` / `/admin/site` / `/admin/announcements` —
     the M22 D3 "rides frozen lanes" pin, admin-scope). The controller
     **never** re-implements a write lane (C-M30·1). The `[Authorize]`
     here is the standard `GlobalAdmin` gate (the surface is admin-scope,
     owner = the signed-in `GlobalAdmin`), **not** an onboarding gate
     (M30·3).
  2. **`src/Kumunita.Web/Models/AdminOnboardingViewModel.cs`** (new) —
     the `AdminOnboardingViewModel` (the M22 `OnboardingViewModel` shape,
     admin-scope): `public sealed class AdminOnboardingViewModel { public
     bool Completed { get; set; }  // CompletedAt is non-null (the M30·5
     banner-eligibility read — the inverse)  public IReadOnlyList<Step>
     Steps { get; set; } = [];  public sealed record Step(string Key,
     string LabelKey, string Route, string DescriptionKey);  }` — the
     seven `Step` entries are the closed set (the register's §"closed
     `kw-l` key set" table, the seven `(key, labelKey, route,
     descriptionKey)` tuples in the README / `Milestones.cs` order:
     community name → `/admin/languages`, languages → `/admin/languages`,
     moderation → `/admin/announcements/comments`, notifications →
     `/admin/quiet`, storage limits → `/admin/storage/settings`, site
     content → `/admin/site`, issue escalation → `/admin/announcements`).
     The `Completed` property is the `GetAsync` read's inverse
     (`CompletedAt != null`); the banner partial reads the **same**
     `GetAsync` seam (M30·5 — the read is the **single** seam the banner
     + the view call, so they resolve to the **same** value).
  3. **`src/Kumunita.Web/DependencyInjection.cs`** (modify, if
     needed) — confirm the `IAdminOnboardingService` registration is
     visible to the `Kumunita.Web` project (the `Kumunita.Core` DI
     registration is sufficient — the `Kumunita.Web` project references
     `Kumunita.Core` — but verify the `AdminOnboardingController`'s
     constructor injection resolves at the Web layer; if a Web-layer
     registration is needed, add it here, mirroring the
     `AdminSiteController`'s registration shape).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green.
  `AdminOnboardingController.cs` exists; the
  `AdminOnboardingViewModel.cs` compiles. **No new test** (U06's
  controller tests are the first M30 controller tests). Handoff note: a
  `## U04 — AdminOnboardingController + ViewModel` section — (a) the 2
  routes (exact paths, for U05's views), (b) the seven `Step` entries
  (by key + route), (c) the `Completed` property shape (the
  `GetAsync` read's inverse), (d) any compile warnings on the new types.
  Move this unit plan `in-progress/` → `done/` (move **last**).

### U05 — `Views/AdminOnboarding/Index.cshtml` + the **admin onboarding banner** + the **closed `adminonboarding.*` `kw-l` key set** × en/de/fr/da (author)

- **Goal:** ship the `/admin/onboarding` **view** (the seven step cards,
  each a list-group row linking into the existing admin surface — the
  M22 `Views/Onboarding/Index.cshtml` shape, admin-scope), the **admin
  onboarding banner** (the M22 `_OnboardingBanner` partial's admin
  sibling, the `_AdminNav` partial's scope — rendered **only** while
  `CompletedAt` is `null` AND the signed-in actor is a `GlobalAdmin`),
  and **author the closed `adminonboarding.*` `kw-l` key set** ×
  en/de/fr/da (the 14 keys: the title + the intro + the seven step
  labels + the "visit" link + the "mark as complete" button + the flash +
  the banner copy + the banner CTA). **No controller change, no Core
  change.** **Exit: `dotnet build` clean + `Kumunita.Web.Tests` green
  (the `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
  pins green — the registry entries are present in all four languages).**
- **Entry reads:** U04's handoff-note `## U04 — AdminOnboardingController +
  ViewModel` section (the 2 routes + the seven `Step` entries),
  `src/Kumunita.Web/Views/Onboarding/Index.cshtml` (the M22 walk-through
  page — the seven step-card shape, the "visit this setting" link-label
  idiom, the finish/skip action shape — the template to mirror),
  `src/Kumunita.Web/Views/Shared/_OnboardingBanner.cshtml` (the M22
  `_OnboardingBanner` partial — the dismissible banner shape, the
  `sessionStorage` flag, the `bannerEligible` read, the
  `onboarding.banner.text` / `onboarding.banner.action` key shape),
  `src/Kumunita.Web/Views/Shared/_AdminNav.cshtml` (the admin sub-nav
  partial — the scope the admin banner renders in),
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the
  `KnownTranslationKeys` registry — the `onboarding.*` key set to mirror
  for the `adminonboarding.*` set),
  `docs/design/m30-admin-onboarding-design.md` §2.5–§2.6 (the
  banner-eligibility rule + the closed `kw-l` key set).
- **Deliverables (4 files, new / modified):**
  1. **`src/Kumunita.Web/Views/AdminOnboarding/Index.cshtml`** (new) —
     the `/admin/onboarding` view (the M22 `Views/Onboarding/Index.cshtml`
     shape, admin-scope): `@model
     Kumunita.Web.Models.AdminOnboardingViewModel` + the
     `ViewData["Title"] = "Admin onboarding"` + the `<h1>` (the
     `adminonboarding.title` `kw-l` key) + the intro paragraph (the
     `adminonboarding.intro` `kw-l` key) + the seven step cards (each a
     `list-group-item` with a title [the step's `labelKey` `kw-l` key] +
     a one-line description [the step's `descriptionKey` `kw-l` key] + a
     "visit this setting" link [the `adminonboarding.visit` `kw-l` key,
     pointing to the step's `Route`]) + the "mark as complete" button
     (the `adminonboarding.complete` `kw-l` key, a
     `[ValidateAntiForgeryToken]` POST to `/admin/onboarding/complete`).
     The copy is the closed `adminonboarding.*` `kw-l` key set (M30·6) —
     the **only** user-visible copy on the page (the M22 D7 "closed
     set" pin, admin-scope).
  2. **`src/Kumunita.Web/Views/Shared/_AdminOnboardingBanner.cshtml`**
     (new) — the admin onboarding banner (the M22 `_OnboardingBanner`
     partial's admin sibling): renders **only** when the signed-in actor
     is a `GlobalAdmin` AND `CompletedAt` is `null` (the M30·5 pin — the
     `bannerEligible` read + the `GlobalAdmin`-gated scope). The banner
     links to `/admin/onboarding` (the `adminonboarding.banner.action`
     `kw-l` key, the CTA label). The copy is the
     `adminonboarding.banner.text` `kw-l` key. The banner is
     **dismissible** (the M22 `onboarding-banner.js` `sessionStorage`
     flag shape — a `sessionStorage` flag, never a write to
     `CompletedAt`; the M22 D5 "non-blocking" pin, admin-scope). The
     banner is rendered in the `_AdminNav` partial's scope (the admin
     sub-nav — the M29 `_AdminNav` shape, the `kmb-tab-under-navbar`
     treatment).
  3. **`src/Kumunita.Web/Views/Shared/_AdminNav.cshtml`** (modify) — add
     the `_AdminOnboardingBanner` partial render (the M22
     `_Layout.cshtml` `_OnboardingBanner` render shape, admin-scope — the
     partial is rendered at the top of the admin sub-nav, before the
     nav-tabs, so it is visible on every `/admin/*` page).
  4. **`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`**
     (modify) — add the **closed `adminonboarding.*` `kw-l` key set** ×
     en/de/fr/da (the 14 keys, the M22 D7 "closed set" shape, admin-
     scope): `adminonboarding.title` / `adminonboarding.intro` /
     `adminonboarding.step_communityname` /
     `adminonboarding.step_languages` /
     `adminonboarding.step_moderation` /
     `adminonboarding.step_notifications` /
     `adminonboarding.step_storage` /
     `adminonboarding.step_sitecontent` /
     `adminonboarding.step_escalation` / `adminonboarding.visit` /
     `adminonboarding.complete` / `adminonboarding.flash_done` /
     `adminonboarding.banner.text` + `adminonboarding.banner.action`.
     Every key is present, non-empty, in **all four** languages
     (en/de/fr/da) — the `KwLRegistryConsistencyTests` +
     `KnownTranslationKeys_ParityTests` closure pins it (M30·6). The
     `en` values are the source text (the ADR 0015 D1 `kw-l`
     provider-floor discipline); the `de` / `fr` / `da` values are the
     translations (the M22 D7 four-language pin, admin-scope).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green.
  `Views/AdminOnboarding/Index.cshtml` exists; the
  `_AdminOnboardingBanner.cshtml` partial exists; the `_AdminNav.cshtml`
  renders the banner; the `adminonboarding.*` `kw-l` key set is present
  in `KnownTranslationKeys.cs` in all four languages. **No new test**
  (U06's banner tests are the first M30 banner tests). Handoff note: a
  `## U05 — view + banner + kw-l keys` section — (a) the view path + the
  banner partial path + the `_AdminNav` render line, (b) the 13 `kw-l`
  keys (by name), (c) the banner-eligibility read (the `GlobalAdmin`-
  gated + `CompletedAt = null` pin), (d) any compile warnings. Move this
  unit plan `in-progress/` → `done/` (move **last**).

### U06 — seam tests (the 15 pinned names) + run + record the acceptance gate

- **Goal:** implement the **15 tests** from the design doc §2.6 (the
  `Core.Tests` pins: the `AdminOnboardingServiceTests` class [5 tests] +
  the `AdminOnboardingSeederTests` class [3 tests]; the `Web.Tests` pins:
  the `AdminOnboardingControllerTests` class [4 tests] + the
  `AdminOnboardingBannerTests` class [4 tests]) in
  `tests/Kumunita.Core.Tests/AdminOnboardingServiceTests.cs` +
  `tests/Kumunita.Core.Tests/AdminOnboardingSeederTests.cs` (U03's seeder
  pin is the first of the three — U06 adds the other two + the five
  service tests) + `tests/Kumunita.Web.Tests/AdminOnboardingControllerTests.cs`
  + `tests/Kumunita.Web.Tests/AdminOnboardingBannerTests.cs`. **Then**
  execute and **record** the acceptance gate (the design doc §2.7): the
  `dotnet build Kumunita.slnx -c Debug` green + the
  `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green + the `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green + the `KwLRegistryConsistencyTests` /
  `KnownTranslationKeys_ParityTests` pins green + the
  `SiteContentServiceTests` / `AdminSiteControllerTests` pins green +
  the `OnboardingControllerTests` (M22) pins green + the
  `MilestonesTests` pin green + the `WhatsNewTests` pin green. **If the
  gate is red, the unit records the red + the cause and pauses (the
  §drift-guard stop) — it does not improvise a fix.**
- **Entry reads:** U05's handoff-note `## U05 — view + banner + kw-l keys`
  section (the view path + the banner partial path + the 13 `kw-l` keys),
  `docs/design/m30-admin-onboarding-design.md` §2.6 (the 15 test names,
  exact — the *primary* source for this unit) + §2.7 (the acceptance
  gate), `tests/Kumunita.Core.Tests/SurfaceLabelsSeederTests.cs` (the M29
  seeder-test shape to mirror — the `PostgresFixture` usage, the
  seed-and-assert pattern), `tests/Kumunita.Web.Tests/AdminSiteControllerTests.cs`
  (the M29 `GlobalAdmin`-gated controller-test shape to mirror — the
  `GET_SeesCurrentSingleton` / `POST_Save_SavesLabel_WritesOneAccessAuditRow`
  / `POST_NonGlobalAdmin_IsDenied` pins),
  `docs/plans-milestones/in-progress/m30-handoff-notes.md` (U03–U05's
  sections — the `AdminOnboarding` field set + the read-seam contract +
  the write-lane contract + the seven `Step` entries + the 13 `kw-l`
  keys).
- **Deliverables (4 files, new + 1 modify):**
  1. **`tests/Kumunita.Core.Tests/AdminOnboardingServiceTests.cs`** (new)
     — the **5 service tests** (the M29 `SurfaceLabelsServiceTests` shape,
     admin-scope):
     - `GetAsync_MissingStore_ReturnsNull` — a test construction with no
       `IDocumentStore` returns `null` (not-yet-guided, the floor —
       M30·2, M30·3).
     - `GetAsync_MissingRow_ReturnsNull` — a fresh store (no
       `AdminOnboarding` row) returns `null` (M30·2, M30·3).
     - `CompleteAsync_WritesOneAccessAuditRow` — `CompleteAsync` writes
       exactly **one** `AccessAudit` row (`Via = Admin`, action
       `admin_onboarding.complete`, `TargetKind` "admin-onboarding")
       (M30·4).
     - `CompleteAsync_StrongConsistency_LiveOnNextGetAsync` — after
       `CompleteAsync`, the very next `GetAsync` returns the stamped
       `CompletedAt` value (strong consistency — M30·4).
     - `CompleteAsync_UpsertsSingleton_NoDuplicateRow` — `CompleteAsync`
       **upserts** the singleton (no duplicate row, the M29·6 / ADR 0150
       D6 pin — M30·2).
  2. **`tests/Kumunita.Core.Tests/AdminOnboardingSeederTests.cs`**
     (modify — U03 added the first test; U06 adds the other two) — the
     **3 seeder tests** (the M29 `SurfaceLabelsSeederTests` shape, admin-
     scope):
     - `FreshBoot_HasExactlyOneAdminOnboardingRow` — a fresh boot has
       exactly one `AdminOnboarding` row (`Id = "singleton"`) (M30·2).
     - `FreshBoot_CompletedAtIsNull` — a fresh boot has `CompletedAt =
       null` (not-yet-guided, the floor — M30·2).
     - `SecondBoot_IsIdempotent_NoDuplicateRow` — a second boot is
       idempotent (no duplicate row, no field change) (M30·2).
  3. **`tests/Kumunita.Web.Tests/AdminOnboardingControllerTests.cs`**
     (new) — the **4 controller tests** (the M29
     `AdminSurfaceLabelsControllerTests` shape, admin-scope):
     - `GET_SeesCurrentCompletedAt` — the `GET` seeds the view model
       with the current `CompletedAt` state (the `Completed` property is
       the `GetAsync` read's inverse — M30·3).
     - `POST_Complete_StampsCompletedAt_WritesOneAccessAuditRow` — the
       `POST /admin/onboarding/complete` stamps `CompletedAt = now` +
       writes exactly **one** `AccessAudit` row (M30·4).
     - `GET_NonGlobalAdmin_IsDenied` — the `GET` is `GlobalAdmin`-gated
       (a non-`GlobalAdmin` is denied — M30·3, the M29·7 pin).
     - `POST_NonGlobalAdmin_IsDenied` — the `POST` is `GlobalAdmin`-gated
       (a non-`GlobalAdmin` is denied — M30·3, the M29·7 pin).
  4. **`tests/Kumunita.Web.Tests/AdminOnboardingBannerTests.cs`** (new)
     — the **4 banner tests** (the M22 `OnboardingBannerTests` shape,
     admin-scope):
     - `BannerRenders_ForGlobalAdmin_WhenNotCompleted` — the banner
       renders **iff** the signed-in actor is a `GlobalAdmin` AND
       `CompletedAt` is `null` (M30·5).
     - `BannerDoesNotRender_ForNonGlobalAdmin` — the banner does **not**
       render for a non-`GlobalAdmin` (even if `CompletedAt` is `null`)
       (M30·3, M30·5).
     - `BannerDoesNotRender_WhenCompleted` — the banner does **not**
       render when `CompletedAt` is non-null (even if the actor is a
       `GlobalAdmin`) (M30·5).
     - `BannerLinkPointsToAdminOnboarding` — the banner link points to
       `/admin/onboarding` (the M22 `onboarding.banner.action` idiom,
       admin-scope — M30·5).
  5. **`docs/design/m30-admin-onboarding-design.md`** (modify) — append
     `### Run result (M30 acceptance gate — <date>)`: the gate's status
     (green/red), the 15-test count (the 5 service + 3 seeder + 4
     controller + 4 banner), the `KwLRegistryConsistencyTests` /
     `KnownTranslationKeys_ParityTests` pins status (the registry
     entries are present in all four languages), the
     `SiteContentServiceTests` / `AdminSiteControllerTests` /
     `OnboardingControllerTests` (M22) pins status (the precedent shapes
     are unchanged), the `MilestonesTests` pin status (the order +
     single-in-progress pin is intact), the `WhatsNewTests` pin status
     (the new `0.46.0` entry is present, newest-first), and one line per
     any `## U<m> — Drift pause` section in the handoff note (each
     resolved or still open). **No code, no build** (the build + test
     runs are the gate, recorded here).
- **Exit:** the 15 tests are present (5 + 3 + 4 + 4). The gate section is
  present and consistent with the 15-test results. Handoff note: a
  `## U06 — seam tests (15) + gate recorded` section — (a) the 4 test
  files (by path), (b) the 15 test names (verbatim), (c) the 15
  pass/red counts (for U07 to consume), (d) the gate status (green/red)
  + the date, (e) any still-open drift. Move this unit plan
  `in-progress/` → `done/` (move **last**).

### U07 — close: `Milestones.cs` flip + README/STATUS/ARCHITECTURE parity + ADR 0153 → `Accepted` + `done/m30/` move

- **Goal:** flip the `Milestones.cs` `M30` row from `StatusPlanned` to
  `StatusDone`, promote `M31` from `StatusPlanned` to `StatusNext` (the
  **order unchanged** — `…"M29","M30","M31"`, the ADR 013/089/093/109
  "named lane, not a renumber" precedent), re-pin
  `MilestonesTests.M30_Is_The_Single_InProgress_Milestone` →
  `M31_Is_The_Single_InProgress_Milestone` + append `"M30"` to the
  `Shipped_Milestones_Are_Marked_Done` done-list, append the README
  Roadmap `M30` line (the `**Done.** (ADR 0153)` tail), append the
  `STATUS.md` `M30` line, append the `ARCHITECTURE.md` `AdminOnboarding/`
  line, append the `WhatsNew.cs` `0.46.0` entry (newest-first, naming M30
  + ADR 0153), tag the ADR 0153 index row `**Done** (M30)`, flip ADR 0153
  → `Accepted`, and move all M30 artifacts to `done/m30/`. **No code
  change** (the `Milestones.cs` flip + the docs parity + the `done/`
  move are the close). **Exit: `dotnet build` clean + `Kumunita.Web.Tests`
  green (the `MilestonesTests` + `WhatsNewTests` pins green — the order +
  single-in-progress pin is intact, the new `0.46.0` entry is present,
  newest-first).**
- **Entry reads:** U06's handoff-note `## U06 — seam tests (15) + gate
  recorded` section (the 15 pass/red counts + the gate status),
  `src/Kumunita.Web/Milestones.cs` (the `M30` row to flip + the `M31`
  row to promote — the order unchanged),
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the
  `M30_Is_The_Single_InProgress_Milestone` pin to re-pin + the
  `Shipped_Milestones_Are_Marked_Done` done-list to append `"M30"` to),
  `src/Kumunita.Web/WhatsNew.cs` (the `0.46.0` entry to append,
  newest-first), `README.md` (the `M30` Roadmap line to append the
  `**Done.** (ADR 0153)` tail), `docs/STATUS.md` (the `M30` line to
  append), `docs/ARCHITECTURE.md` (the `AdminOnboarding/` line to
  append), `docs/adr/0153-admin-onboarding.md` (the ADR 0153 to flip to
  `Accepted` + the index row to tag `**Done** (M30)`),
  `docs/adr/README.md` (the ADR 0153 index row to tag `**Done** (M30)`).
- **Deliverables (7 files, modify + 1 move):**
  1. **`src/Kumunita.Web/Milestones.cs`** (modify) — the `M30` row:
     `new("M30", "Admin onboarding — …", StatusDone)` (was
     `StatusPlanned`) + the `M31` row: `new("M31", "Production error
     handling — …", StatusNext)` (was `StatusPlanned`). The order is
     **unchanged** (`…"M29","M30","M31"`) — the ADR 013/089/093/109
     "named lane, not a renumber" precedent.
  2. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** (modify) —
     append `"M30"` to the `Shipped_Milestones_Are_Marked_Done` done-list
     + **replace** `M30_Is_The_Single_InProgress_Milestone` with
     `M31_Is_The_Single_InProgress_Milestone`.
  3. **`src/Kumunita.Web/WhatsNew.cs`** (modify) — append the `0.46.0`
     entry (newest-first, naming M30 + ADR 0153): `new("0.46.0",
     "2026-10-08", new List<string> { "Admin onboarding — a guided
     walk-through for a new GlobalAdmin through the seven most important
     initial settings (community name, languages, moderation,
     notifications, storage limits, site content, issue escalation): a
     links-only walk-through on the M22 / SITE lane's shape (a singleton
     completion flag, not a new write path), and a fresh GlobalAdmin who
     never touches the surface sees the admin banner (the affordance, not
     a wall) (ADR 0153)." })`.
  4. **`README.md`** (modify) — the `M30` Roadmap line: append the
     `**Done.** (ADR 0153)` tail (the M29 `**Done.** (ADR 0152)` shape).
  5. **`docs/STATUS.md`** (modify) — the `M30` line: append the
     `**M30 is done** — admin onboarding (a guided walk-through for a
     new GlobalAdmin through the seven most important initial settings —
     community name, languages, moderation, notifications, storage limits,
     site content, issue escalation — a links-only walk-through on the
     M22 / SITE lane's shape, not a new write path; ADR 0153)` line (the
     M29 shape).
  6. **`docs/ARCHITECTURE.md`** (modify) — the `AdminOnboarding/` line:
     append the `**AdminOnboarding/** — the M30 admin onboarding lane
     (ADR 0153): the `AdminOnboarding` singleton doc + the
     `IAdminOnboardingService` read + audited-write seam + the
     `AdminOnboardingDocTypes` registration surface + the
     `AdminOnboardingController` (`/admin/onboarding`) + the admin
     onboarding banner (the M22 `_OnboardingBanner` partial's admin
     sibling) + the closed `adminonboarding.*` `kw-l` key set` line (the
     M29 `SurfaceLabels/` shape).
  7. **`docs/adr/0153-admin-onboarding.md`** (modify) — the ADR 0153:
     flip `Status: Draft` → `Status: Accepted` + the
     `docs/adr/README.md` index row: tag the `0153` row `**Done**
     (M30)` (the M29 `0152` row shape). **Plus** the `done/m30/` move:
     `git mv docs/plans-milestones/plan-m30-admin-onboarding.md
     docs/plans-milestones/done/m30/` + `git mv
     docs/plans-milestones/in-progress/m30-uNN.md
     docs/plans-milestones/done/m30/m30-uNN.md` (for each U00–U06 unit
     plan) + `git mv
     docs/plans-milestones/in-progress/m30-handoff-notes.md
     docs/plans-milestones/done/m30/m30-handoff-notes.md` (the
     `done/m22/` / `done/m29/` subfolder convention, matching the real
     tree).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green.
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green (the `MilestonesTests` + `WhatsNewTests` pins green — the order +
  single-in-progress pin is intact, the new `0.46.0` entry is present,
  newest-first). The `Milestones.cs` `M30` row is `StatusDone` + the
  `M31` row is `StatusNext` (the order unchanged). The README /
  `STATUS.md` / `ARCHITECTURE.md` parity is held. The ADR 0153 is
  `Accepted` + the index row is tagged `**Done** (M30)`. The
  `done/m30/` subfolder is present (the register + the unit plans + the
  handoff notes). Handoff note: a `## U07 — close` section — (a) the
  `Milestones.cs` flip (the `M30` row `StatusDone` + the `M31` row
  `StatusNext`), (b) the `MilestonesTests` re-pin (the
  `M31_Is_The_Single_InProgress_Milestone` pin), (c) the `WhatsNew.cs`
  `0.46.0` entry (newest-first), (d) the README / `STATUS.md` /
  `ARCHITECTURE.md` parity (the three lines), (e) the ADR 0153
  `Accepted` + the index row `**Done** (M30)`, (f) the `done/m30/` move
  (the register + the unit plans + the handoff notes). Move this unit
  plan `in-progress/` → `done/` (move **last**). `git status` clean.

---

## §drift-guard (frozen once U00 locks it)

**A unit stops (does not improvise) when it hits any of:**

- **The `AdminOnboarding` field set is not the ceiling** (the one
  optional `DateTimeOffset?` field `CompletedAt` — no field outside the
  field set may appear in the doc, the ADR 0153 D1 pin).
- **The `SiteContent` + `LocaleSettings` docs are touched** (the ADR 0150
  D6 / ADR 0006 module-boundary pin — M30 adds a *new* doc in a *new*
  context, it does not re-shape the existing ones).
- **The M22 `Profile.OnboardingCompletedAt` field is touched** (the M22 /
  M30 distinction — M22 is resident-scope, M30 is admin-scope, the two
  completion flags are independent; M30 does not re-shape the M22
  resident onboarding).
- **The seven admin routes are re-shaped** (the "walk-through is a guided
  shell, not a re-route" pin — the walk-through *links into* the surfaces,
  it does not *move* them; the seven routes — `/admin/languages` /
  `/admin/announcements/comments` / `/admin/quiet` /
  `/admin/storage/settings` / `/admin/site` / `/admin/announcements` — are
  unchanged).
- **A per-step POST, a persisted step cursor, or a server-session step
  state is added** (the M22 D4 "single stateless page" pin — the
  walk-through's *only* write is `CompleteAsync`, the M22 D3 "rides
  frozen lanes" pin, admin-scope).
- **A second write lane for any of the seven settings is added** (the M22
  D3 "rides frozen lanes" pin — the walk-through never re-implements a
  write lane, admin-scope).
- **The closed `adminonboarding.*` `kw-l` key set is not parity-pinned in
  four languages** (the M30·6 pin — every key is present, non-empty, in
  en/de/fr/da, the M22 D7 "closed set" shape, admin-scope).
- **The `Milestones.cs` / README / `MilestonesTests` trio is touched
  before U07** (the M30·8 pin — the trio is untouched until the milestone
  *ships*, U07 owns the close flip).
- **The `WhatsNew.cs` registry is touched before U07** (the M30·8 pin —
  the `0.46.0` entry is appended by U07, not by an earlier unit).
- **A new `AccessAction`, a new `AccessVia`, a new `Decide()` branch, or a
  new `IAuthorizationService` surface is added** (the M30·3 pin — the
  read is a public admin surface, the write is the ADR 0150
  single-write-lane shape, the `GlobalAdmin` gate is the standard admin-
  scope gate, not a new authorization surface).
- **A new bounded context other than `Kumunita.Core.AdminOnboarding` is
  added** (the M30·2 pin — the `AdminOnboarding` doc is in a new context,
  the `SiteContent` + `LocaleSettings` contexts are untouched).
- **A new `*DocTypes` surface other than `AdminOnboardingDocTypes` is
  added** (the M30·2 pin — the `AdminOnboarding` doc is registered in a
  new `AdminOnboardingDocTypes` surface, the `SiteContentDocTypes` +
  `LocaleSettingsDocTypes` surfaces are untouched).
- **The design doc is out of date** (a `## U<m> — Drift pause` in the
  handoff note — the unit pauses, records the drift, and hands off; it
  does not improvise a fix).

## §gate (acceptance tests, named — U00 locks them in the design doc)

- **GATE-1 — One singleton doc, `null` = not-yet-guided.**
  `AdminOnboarding.CompletedAt` exists, is a nullable
  `DateTimeOffset?`, defaults to `null`, and is picked up by the existing
  delta-detected Marten boot (no new DocTypes surface / boot line / EF
  migration — the ADR 0004 §B.1 additive doc type, the ADR 0150 D6 pin).
  *(M30·2.)*
- **GATE-2 — The completion stamp is one `GlobalAdmin`-gated write, one
  audit row, strong consistency.** `CompleteAsync` stamps **only**
  `CompletedAt = now`, one `SaveChangesAsync`, **exactly one
  `AccessAudit` row** (`Via = Admin`, action `admin_onboarding.complete`,
  `TargetKind` "admin-onboarding"), and is **strongly consistent** (the
  new value is live on the very next `GetAsync`). *(M30·4.)*
- **GATE-3 — Zero new field-write lanes; the walk-through rides the
  frozen lanes.** The `/admin/onboarding` page's only write is
  `CompleteAsync`; the walk-through adds no new `IAdminOnboardingService`
  write member beyond it (+ the read seam); community name / languages /
  moderation / notifications / storage limits / site content / issue
  escalation all ride the frozen admin surfaces (M30·1, M30·7).
- **GATE-4 — Zero new authorization surface.** The seam/claim pin
  passes: no new `AccessAction` / `Decide()` branch / `AccessVia` /
  `IAuthorizationService` method; `ClaimTypes.All` unchanged; no new
  `IAuditableResource` adapter, no new `*DocTypes` surface (the
  `AdminOnboardingDocTypes` is the M30·2 pin, not a new authorization
  surface). *(M30·3.)*
- **GATE-5 — The banner is gated on the `GlobalAdmin` + `CompletedAt`
  read and is non-blocking.** The banner renders **iff** the signed-in
  actor is a `GlobalAdmin` AND `CompletedAt` is `null`; it never blocks
  the admin sign-in; the banner is always dismissible; the
  `/admin/onboarding` route is always reachable for a `GlobalAdmin`.
  *(M30·5.)*
- **GATE-6 — The closed `adminonboarding.*` set is parity-pinned in four
  languages.** Every `adminonboarding.*` key is present, non-empty, in
  en/de/fr/da; the closure + parity pins hold. *(M30·6.)*
