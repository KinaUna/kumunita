# M30 — Admin onboarding (design doc)

> **Milestone M30 — Admin onboarding.** The README / `Milestones.cs` line,
> verbatim: "**Admin onboarding** — a guided walk-through for a new
> GlobalAdmin through the most important initial settings (community name,
> languages, moderation, notifications, storage limits, site content, issue
> escalation)." M30 ships **the M22 onboarding lane extended to the admin
> surface** — the *same shape* M22 shipped for a new **resident** (a
> **stateless** guided walk-through that **rides the frozen lanes** for every
> setting it surfaces, a **single completion-stamp** as its *only* write, a
> **non-blocking dismissible banner**, and a **closed `kw-l` key set** in
> en/de/fr/da) — now aimed at a new **GlobalAdmin** and pointing at the
> seven admin surfaces that already own those settings instead of the
> resident's profile/settings.
>
> **Three-tier contract.** This file is the **primary** tier of M30's
> contract: it pins the **invariants (M30·1–M30·8)**, the **FACES
> (M30-1–M30-8)**, the **closed seven-step walk-through set**, and (in
> Part 2) the exact `AdminOnboarding` field set, the read-seam contract, the
> write-lane contract, the banner-eligibility rule, the pinned seam-test
> names, the acceptance gate, and the drift guard. The register
> (`docs/plans-milestones/plan-m30-admin-onboarding.md`) is the **secondary**
> tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/m30-handoff-notes.md` is the **scratch**
> tier (one short section per unit, appended, never rewritten). When the
> three disagree, **this file wins for the pinned shapes**; the register wins
> for *which files exist* and *what each unit does*.
>
> **Part 1 (this file, U01):** the value chain, the context, the scope
> (In / Out, incl. the named M22 D8 deferrals), the **eight invariants**
> (M30·1–M30·8), and the **eight FACES** (M30-1–M30-8), plus the frozen-base
> assumptions. **Part 2 (U02):** the seams & contracts (the exact
> `AdminOnboarding` field set, the read-seam contract, the write-lane
> contract, the banner-eligibility rule, the pinned seam-test names, the
> acceptance gate, the drift guard) + **ADR 0153**.
>
> **The frozen base.** M30 is built **on top of** the M22 resident
> onboarding (ADR 0132 — the stateless walk-through shape, the dismissible
> banner shape, the closed-`kw-l`-set shape, the single-write-lane shape),
> the `SiteContent` singleton (ADR 0150 — the `Id = "singleton"` sentinel,
> the best-effort read, the single audited write-lane), the
> `LocaleSettings` singleton (ADR 0005 B — the singleton doc shape), the
> single-write-lane / singleton-toggle audit shape (ADR 0050), the
> additive-doc-type convention (ADR 0004 §B.1), the module-boundary contract
> (ADR 0006), and the `kw-l` provider-floor discipline (ADR 0015 D1). All of
> these still bind **unchanged**. M30 adds **one new singleton doc**
> (`AdminOnboarding`, a `CompletedAt` flag), **one new bounded context**
> (`Kumunita.Core.AdminOnboarding`), **one new registration surface**
> (`AdminOnboardingDocTypes`), **one new GlobalAdmin surface**
> (`/admin/onboarding`), and **one admin banner** — but it adds **no**
> re-shape of the `SiteContent` / `LocaleSettings` docs, **no** re-shape of
> the M22 `Profile.OnboardingCompletedAt` field, **no** removal of a `kw-l`
> registry entry, and **no** new `AccessAction` / `AccessVia` /
> `Decide()` branch / `IAuthorizationService` method. It is **additive**.
> **No EF migration** (a new Marten doc type is additive per ADR 0004 §B.1;
> the delta is applied idempotently at boot).
>
> **The `AdminOnboarding` field set is the ceiling** (the one optional
> `DateTimeOffset?` field `CompletedAt` — no field outside the field set may
> appear in the doc, the ADR 0153 D1 pin). **The `kw-l` registry entries are
> the floor** (the `adminonboarding.*` keys stay, the ADR 0153 D3 pin).
> **The `SiteContent` + `LocaleSettings` singletons are untouched** (M30 adds
> a *new* doc in a *new* context, not a new field on an existing one, the
> ADR 0150 D6 / ADR 0006 pin). **The seven admin routes are unchanged** (the
> walk-through *links into* the surfaces by route, it does not *move* them,
> M30·7).
>
> **The one thing every unit must respect:** M30 **rides the frozen
> GlobalAdmin-gated admin lanes** and **adds zero new write lane** for any of
> the seven settings (M30·1, M30·7). The completion state is **one new
> singleton doc** (`null` = not-yet-guided, M30·2); the read is a **public
> admin surface** (`GlobalAdmin`-gated, never audited, M30·3); the completion
> stamp is **the ADR 0150 single-write-lane shape** (one `AccessAudit` row,
> M30·4); the banner is **the affordance, non-blocking** (M30·5); every
> string is a **closed `adminonboarding.*` key in four languages** (M30·6).
> The `Milestones.cs` / README / `MilestonesTests` trio is **untouched until
> the milestone ships** (U07 owns the close flip, M30·8). It does **not** add
> a branch to `Decide()`, a new `IAuthorizationService` signature, a new
> `AccessAction`, a new `AccessVia`, a claim-encoded "is an admin who hasn't
> finished" flag, a second write lane for any of the seven settings, a
> per-step persisted cursor, or a blocking wall (the register's §drift-guard
> is the exact set of traps that would break it).

## Value chain

M22 (ADR 0132) shipped a guided walk-through for a **new resident**: a
stateless `/onboarding` page that walks display name → avatar → language →
time zone → date format → email language → contact, links each step into the
owner-scope lane that already owns it, stamps a single
`Profile.OnboardingCompletedAt` completion flag as its *only* write, and
shows a dismissible home/nav banner while that flag is `null`. SITE (ADR
0150) shipped the **admin** shape of the same pattern at the surface level: a
`SiteContent` singleton (the `Id = "singleton"` doc), a best-effort read + a
single audited write-lane, and a dedicated `GlobalAdmin`-gated `/admin/site`
surface.

M30 completes the arc **for a new GlobalAdmin who takes over the instance for
the first time**. That admin faces the *exact same* problem M22 solved for
the resident, one level up: they must discover, in a sprawling `/admin`
surface (the `/admin/platform` list + the `/admin` dashboard hub), the seven
most important initial settings — **community name, languages, moderation,
notifications, storage limits, site content, issue escalation** — and there is
**no guided path** that walks them through those seven *in order*, and **no
completion record** so a second GlobalAdmin (or a returning first one) doesn't
see the same walk-through again.

M30 buys, for a new GlobalAdmin:

- **A guided, in-order walk of the seven settings** — the `/admin/onboarding`
  page walks community name → languages → moderation → notifications →
  storage limits → site content → issue escalation, each step linking into
  the existing admin surface that already owns it (M30·7).
- **A single completion record** — the `AdminOnboarding` singleton's
  `CompletedAt` flag (`null` = not-yet-guided), stamped by the one
  `CompleteAsync` write (M30·2, M30·4).
- **A non-blocking admin banner** — a dismissible banner on the admin
  surfaces while `CompletedAt` is `null` AND the actor is a `GlobalAdmin`,
  linking to `/admin/onboarding`; it clears on completion (M30·5).

The value chain moves one arrow: from *"the admin configures how the
platform behaves"* (`LocaleSettings`) and *"the admin edits the landing
surfaces"* (SITE) to **"the admin is walked through the initial settings in
order and records that they've been through it"** (M30) — the same
"admin-settled instance value" pattern M22 applied to the resident's account
setup, now applied to the admin surface. The walk-through's *only* write is
the completion stamp; every setting it surfaces is already writable through
the existing `GlobalAdmin`-gated lane it links into (M30·1).

## Context

The gap this milestone closes is the **guided path + the completion record**
for a new GlobalAdmin. M22 (ADR 0132) gave a **new resident** a short guided
tour of the account-setup surface that already exists, a single completion
stamp, and a dismissible banner. SITE (ADR 0150) gave a **GlobalAdmin** a
single-surface `/admin/site` edit page. But the **seven most important initial
settings** are scattered across **six shipped admin surfaces** (the seventh —
issue escalation — is a **placeholder**, see below), and there is **no
guided path** that walks a new GlobalAdmin through those seven *in order*,
and **no completion record** so a second GlobalAdmin doesn't see the same
walk-through again.

**The seven step cards are the closed set (named by route, in the README /
`Milestones.cs` order).** Each step links into the existing admin surface that
already owns the setting — **by route** (U00's location/framing note: some of
these are `/admin/platform` list-group rows, some are `/admin` dashboard-hub
cards, and the walk-through links by route, not by list-group-row location —
M30·7 is route-agnostic about where the discoverability affordance lives):

| # | Setting (step) | Route (link target) | Owning surface |
| --- | --- | --- | --- |
| 1 | **Community name** | `/admin/languages` | the community's display name + description, edited within the languages surface (ADR 0026 / 0053 community-translation surface). |
| 2 | **Languages** | `/admin/languages` | the language catalog + the translated UI strings + the static pages. |
| 3 | **Moderation** | `/admin/announcements/comments` | the announcement-comment moderation toggle (the ADR 0101 shape — the closest shipped admin surface for "moderation" on the current admin surface). |
| 4 | **Notifications** | `/admin/quiet` | the notification-flush cadence the admin sets for held notification emails (residents set their own quiet window on `/settings/quiet`). |
| 5 | **Storage limits** | `/admin/storage/settings` | the admin-set per-file size limit + the per-user total content quota (the ADR 0135 M25 surface). |
| 6 | **Site content** | `/admin/site` | the landing surfaces' hero text + the show/hide toggles (the ADR 0150 SITE lane). |
| 7 | **Issue escalation** | `/admin/announcements` | **placeholder** — the announcements surface, the closest shipped admin surface; M32 "issue submission & escalation" is `StatusPlanned` (not yet shipped), so the walk-through links into the announcements surface as a **known deferral, not a drift** (a future M32 close-flip re-points this step card's route to the M32 surface). |

The step set is the **complete** admin onboarding surface for the current
scope — no hidden steps, no reserved steps. A future lane **adds** steps
(additive per ADR 0004 §B.1, if the walk-through ever grows a per-step state),
it does not re-shape the existing seven.

### The precedent shapes (frozen, verified by U00)

- **ADR 0132 (M22)** — the resident onboarding lane: the stateless
  `OnboardingController` (`GET /onboarding` + the single
  `CompleteOnboardingAsync` write-lane + the `FlashAsync` idiom), the
  dismissible `_OnboardingBanner` partial (the `bannerEligible` read + the
  `sessionStorage` flag, never a write to the completion flag), the closed
  `onboarding.*` `kw-l` set (15 keys × en/de/fr/da), and the
  `Profile.OnboardingCompletedAt` additive field. M30 mirrors this shape at
  the admin scope.
- **ADR 0150 (SITE)** — the `SiteContent` singleton (the `Id = "singleton"`
  sentinel, the field-set ceiling), the best-effort read + the single audited
  write-lane (`SiteContentService`), and the `GlobalAdmin`-gated
  `AdminSiteController` (`/admin/site`). M30's `AdminOnboarding` doc +
  `IAdminOnboardingService` + `AdminOnboardingController` mirror this shape.
- **ADR 0005 B** — the `LocaleSettings` singleton (the `Id = "singleton"`
  sentinel, the one-row-per-instance shape). The `AdminOnboarding` doc is a
  **new** singleton in the same shape, **not** a new field on
  `LocaleSettings` (ADR 0006 module-boundary contract).
- **ADR 0050** — the singleton-toggle admin surface shape (the
  `IsSignupOpenAsync` best-effort read + the `SetSignupOpenAsync` audited
  write + the `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST + the
  one `AccessAudit` row per save). M30's `CompleteAsync` audit row
  (`Via = Admin`, action `admin_onboarding.complete`, `TargetKind`
  "admin-onboarding") mirrors this shape.
- **ADR 0004 §B.1** — the additive-doc-type convention (delta-detected,
  idempotent, **no re-seed, no EF migration**). The `AdminOnboarding` doc is
  registered in a new `AdminOnboardingDocTypes` surface (the
  `SiteContentDocTypes` parallel), and the delta is applied idempotently at
  boot.
- **ADR 0006** — the module-boundary contract (a new bounded context
  `Kumunita.Core.AdminOnboarding`; the `SiteContent` + `LocaleSettings`
  contexts are untouched).
- **ADR 0015 D1** — the `kw-l` provider-floor discipline (the en source text
  is the floor; every key present, non-empty, in all four languages, pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`).

### The constraints that still bind

- **The `SiteContent` + `LocaleSettings` docs are untouched** (M30 adds a
  *new* doc in a *new* context — the ADR 0150 D6 / ADR 0006 pin).
- **The M22 `Profile.OnboardingCompletedAt` field is untouched** (M22 is
  resident-scope, M30 is admin-scope; the two completion flags are independent).
- **The seven admin routes are unchanged** (the walk-through *links into* the
  surfaces by route; the walk-through adds **one new route**,
  `/admin/onboarding`, for the walk-through + the completion stamp — it does
  not re-route the seven).
- **Zero new authorization surface** (the read is `GlobalAdmin`-gated by the
  standard `[Authorize(Roles = GlobalAdmin)]` — not a new
  `AccessAction` / `AccessVia` / `Decide()` branch / `IAuthorizationService`
  method; the thin-token rule, ADR 0001-B).
- **The `Milestones.cs` / README / `MilestonesTests` close-flip trio is
  untouched until the milestone ships** (U07 owns it; M30·8).

## Scope

**In (M30's closed surface):**

- the `AdminOnboarding` **singleton doc** (a new `Kumunita.Core.AdminOnboarding`
  bounded context) — the one optional `DateTimeOffset?` field `CompletedAt`,
  defaulting to `null` (= not-yet-guided, the floor), `Id = "singleton"`
  sentinel (the exact `SiteContent` / `LocaleSettings` shape, ADR 0005 B);
- the `IAdminOnboardingService` + `AdminOnboardingService` (the ADR 0150
  single-write-lane shape — the `GetAsync` best-effort read + the
  `CompleteAsync` audited write-lane);
- the `AdminOnboardingDocTypes` (the ADR 0004 §B.1 additive doc type, the
  `SiteContentDocTypes` parallel surface);
- the `FirstBootSeeder` default (a `CompletedAt = null` row — the
  "not-yet-guided" floor, the `SeedSiteContentAsync` create-if-missing /
  idempotent / never-overwrites shape);
- the `AdminOnboardingController` (`GET /admin/onboarding` walk-through +
  `POST /admin/onboarding/complete` stamping completion) + the
  `AdminOnboardingViewModel` (the completion-state read + the closed
  seven-step list);
- the `Views/AdminOnboarding/Index.cshtml` view (the seven step cards, the
  "visit this setting" link-label idiom, the "mark as complete" action — the
  M22 `Views/Onboarding/Index.cshtml` shape, admin-scope);
- the **admin onboarding banner** (the M22 `_OnboardingBanner` partial's
  admin sibling, the `_AdminNav` partial's scope — rendered only while
  `CompletedAt` is `null` AND the actor is a `GlobalAdmin`);
- the closed `adminonboarding.*` `kw-l` key set × en/de/fr/da (14 keys — the
  title + the intro + the seven step labels + the "visit" link + the "mark as
  complete" button + the flash + the banner copy + the banner CTA); and
- the test pins (the `AdminOnboardingServiceTests` / `AdminOnboardingSeederTests`
  / `AdminOnboardingControllerTests` / `AdminOnboardingBannerTests` classes).

**Out (named deferrals for future lanes, if one comes):**

- **Per-step progress / resume-across-sessions** (the M22 D8·1 deferral,
  verbatim) — M30's page is stateless; a persisted per-step cursor + "pick up
  where you left off" is a future lane (a new data surface, a future ADR).
- **A "you haven't set X" detection engine** (the M22 D8·2 deferral, verbatim)
  — M30 links into the existing surfaces and shows a static "visit this
  setting" hint; an automatic recommendation engine is a future lane.
- **An admin "onboarding completion rate" metric** (the M22 D8·4 deferral,
  verbatim) — an operator analytics surface (the M13 logging lane); M30 ships
  **no** admin metric view.
- **The `Milestones.cs` / README / `MilestonesTests` trio** until the
  milestone *ships* (U07 owns the close flip — the `WhatsNew.cs` `0.46.0`
  entry is appended by U07, not by an earlier unit).

## Invariants (pinned for M30)

- **M30·1 — The walk-through is a *guided shell*, not a new data surface.**
  Every setting it links into is **already writable** through an existing
  `GlobalAdmin`-gated lane (`/admin/languages` / `/admin/announcements/comments`
  / `/admin/quiet` / `/admin/storage/settings` / `/admin/site` /
  `/admin/announcements`). M30 **rides those surfaces verbatim** — it adds
  **no new write lane** for any of the seven settings; the walk-through's
  *only* write is the completion stamp.
- **M30·2 — The completion state is one singleton doc, `null` = not-yet-guided.**
  The `AdminOnboarding` doc is a **singleton** (one row per instance,
  `Id = "singleton"` sentinel, the exact `SiteContent` / `LocaleSettings`
  shape, ADR 0005 B / ADR 0150) with exactly **one** additive member —
  `CompletedAt: DateTimeOffset?`, `null` = the floor (banner shows), non-null
  = completed (banner clears). It is registered in a new
  `AdminOnboardingDocTypes` surface (ADR 0004 §B.1); the delta is applied
  idempotently at boot; **no EF migration**. The `SiteContent` +
  `LocaleSettings` docs are **untouched** (M30 adds a *new* doc in a *new*
  context, ADR 0006).
- **M30·3 — The read is a public admin surface.** The completion-state read is
  `GlobalAdmin`-gated by the `[Authorize(Roles = GlobalAdmin)]` on the
  controller (the standard admin-scope gate, **not** a new authorization
  surface — the thin-token rule, ADR 0001-B), and is **never audited** (a
  read, not an access decision). A missing `AdminOnboarding` row (a fresh boot
  before the seeder ran, or a test construction with no store) degrades to
  `CompletedAt = null` (not-yet-guided, the banner shows) — never a blank
  walk-through, never an error.
- **M30·4 — The write is the ADR 0150 single-write-lane shape.** One
  `IAdminOnboardingService.CompleteAsync(actorBy)` lane that loads the
  singleton, stamps `CompletedAt = now`, and saves in one session; exactly
  **one** `AccessAudit` row per stamp (`Via = Admin`, action
  `admin_onboarding.complete`, `TargetKind` "admin-onboarding" — the
  `site.save` / `signup.set-open` / `timezone.set-default` singleton-toggle
  shape). The lane **upserts** the singleton (the `SiteContent` "one row per
  instance" shape, ADR 0150 D6); **strong consistency** — the new value is
  live on the very next `GetAsync` / banner read.
- **M30·5 — The banner is the *affordance*, and it is non-blocking.** A
  dismissible admin banner (the M22 `_OnboardingBanner` partial's admin
  sibling, the `_AdminNav` partial's scope) is rendered on the admin surfaces
  **only while `CompletedAt` is `null`** AND the signed-in actor is a
  `GlobalAdmin`. It links to `/admin/onboarding`. It is **non-blocking**: the
  admin sign-in is never gated, the banner is always dismissible (a
  `sessionStorage` flag — never a write to `CompletedAt`), and the
  `/admin/onboarding` route is always reachable for a `GlobalAdmin` regardless
  of completion state.
- **M30·6 — The closed `adminonboarding.*` `kw-l` key set is parity-pinned in
  four languages.** Every new user-visible string M30 introduces is a
  `KnownTranslationKeys` entry present, **non-empty, in all four** languages
  (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests`. The closed set (the title + the intro +
  the seven step cards + the "visit" link + the "mark as complete" button +
  the flash + the banner copy + the banner CTA) is authored by U05 and
  consumed by U04's view model.
- **M30·7 — The walk-through links into the seven roadmap settings.** The
  seven step cards are the **closed** set named in the README / `Milestones.cs`
  line — **community name, languages, moderation, notifications, storage
  limits, site content, issue escalation** — each linking into the existing
  admin surface that already owns it (by route). The walk-through is
  **links-only**: it does **not** inline a control for any of the seven, does
  **not** add a per-step POST, does **not** persist a step cursor, and does
  **not** gate the admin surface on completion.
- **M30·8 — The `Milestones.cs` / README / `MilestonesTests` trio is untouched
  until the milestone *ships*.** U07 owns the close flip. The `WhatsNew.cs`
  registry gains one new entry (newest-first, the `0.46.0` row) naming M30 +
  ADR 0153 — the M27 "shipped with no entry until caught in review" lesson
  (AGENTS.md) is held.

## FACES (pinned, 8)

- **M30-1 — A fresh GlobalAdmin sees the banner on every `/admin/*` page.**
  The banner renders **iff** `CompletedAt` is `null` AND the actor is a
  `GlobalAdmin` (M30·5).
- **M30-2 — The `/admin/onboarding` page walks the seven settings in the README
  / `Milestones.cs` order.** community name → languages → moderation →
  notifications → storage limits → site content → issue escalation, each step
  linking into the existing admin surface that already owns it (M30·1, M30·7).
- **M30-3 — The "mark as complete" button stamps `CompletedAt = now` + writes
  exactly one `AccessAudit` row.** `Via = Admin`, action
  `admin_onboarding.complete`, `TargetKind` "admin-onboarding" (M30·4).
- **M30-4 — The banner clears on the next read after `CompletedAt` is
  stamped.** Strong consistency — the new value is live on the very next
  `GetAsync` / banner read (M30·4, M30·5).
- **M30-5 — A non-`GlobalAdmin` never sees the banner (even if `CompletedAt`
  is `null`).** The banner's scope is `GlobalAdmin`-gated (M30·3, M30·5).
- **M30-6 — The walk-through's *only* write is `CompleteAsync`.** No per-step
  POST, no persisted step cursor, no server-session step state (M30·1, M30·7).
- **M30-7 — The closed `adminonboarding.*` `kw-l` key set is parity-pinned in
  four languages.** Every key is present, non-empty, in en/de/fr/da (M30·6).
- **M30-8 — The `AdminOnboarding` doc is a singleton.** One row per instance,
  `Id = "singleton"` sentinel; the `SiteContent` + `LocaleSettings` docs are
  untouched (M30·2).

---

*Part 2 (U02) appends: the `AdminOnboarding` field set (exact), the read-seam
contract, the write-lane contract, the banner-eligibility rule, the pinned
seam-test names, the acceptance gate, the drift guard — plus **ADR 0153**.*
