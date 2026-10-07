# SITE content customization (`SITE`) — rolling handoff notes

> **The scratch tier** of the SITE lane's three-tier contract (the design
> doc is primary, the register is secondary, this file is scratch).
> One section per unit, **appended, never rewritten**. Each unit writes
> exactly one short section before it exits; the next unit reads only that
> section + its own entry-reads list. A `## U<m> — Drift pause` section is a
> **blocker**: the next unit reads it first and either resolves it (recording
> the resolution in its own section) or carries it forward (naming it in its
> exit criteria).
>
> The skeleton below is the **only** pre-written content — every `##` section
> from here on is authored by a unit, in order.

## Lane open

- **Date:** 2026-10-07
- **Register:** `docs/plans-milestones/plan-site.md` (U00–U08)
- **Design doc (primary):** `docs/design/site-content-design.md` (U01/U02
  author)
- **ADR:** `docs/adr/0150-site-content-customization.md` (U02 authors; the
  next free number after 0149 — confirmed against `docs/adr/README.md`
  where 0149 is the current highest).
- **Scope:** the platform's two **landing surfaces** become
  admin-editable:
  - `/home` = `Views/Home/Index.cshtml` (4 sections: hero, feature cards,
    "What's new" feed, roadmap)
  - `/about` = `Views/StaticPages/About.cshtml` (7 sections: hero, feature
    cards, scope, FIG philosophy, code/docs, "What's new" changelog, contact
    CTA)
  
  The `SiteContent` singleton (a new `Kumunita.Core.SiteContent` context,
  `Id = "singleton"`) carries:
  - **4 text fields** (editable hero eyebrow + lead for each surface):
    `HomeHeroEyebrow`, `HomeHeroLead`, `AboutHeroEyebrow`, `AboutHeroLead`
  - **9 toggle fields** (show/hide for every other section):
    `HomeShowAboutButton`, `HomeShowFeatures`, `HomeShowRoadmap`,
    `AboutShowFeatures`, `AboutShowScope`, `AboutShowPhilosophy`,
    `AboutShowProject`, `AboutShowWhatsNew`, `AboutShowContactCta`
  
  Defaults are **byte-identical to the shipped `kw-l` text** (the `en` source
  text from `KnownTranslationKeys.cs`); every toggle defaults `true`.
  The `kw-l` registry entries **stay** (unchanged).
  One new admin surface: `/admin/site` (`AdminSiteController`,
  `GlobalAdmin`-gated, ADR 0050 single-write-lane shape).
  **No new route** for the read (the existing `/` and `/about` routes are
  unchanged). **No EF migration** (additive Marten doc type per ADR 0004
  §B.1). **No new `AccessAction` / `AccessVia` / authorization path.**
- **Out of scope (named deferrals for a future SITE-2 lane, if one comes):**
  machine translation of the landing-surface copy (a
  `SiteContentTranslation` row shape, the `PageTranslation` /
  `PostTranslation` precedent); per-section editable text (the feature
  cards' titles/bodies, the scope band's body, the FIG-philosophy band's
  body, the code/docs band's lead — the current scope is show/hide only);
  per-section custom CTA buttons (an editable URL + label); the
  `Milestones.cs` / README / `MilestonesTests` close-flip entries until the
  lane *ships* (U08 owns them).
- **Frozen base (unchanged):**
  - ADR 0005 B — the `LocaleSettings` singleton (`Id = "singleton"` sentinel,
    the additive-field convention, one row per instance) — this lane adds a
    *new* doc, not a new field on the existing one.
  - ADR 0019 / ADR 0020 — the singleton-toggle admin surface shape
    (`/admin/timezone` / `/admin/dateformat`, `GlobalAdmin`-gated, one
    `AccessAudit` row per save).
  - ADR 0050 — the single-write-lane shape (`AdminSignupController`, the
    `IsSignupOpen` best-effort read, the `SetSignupOpenAsync` audited write).
  - ADR 0149 — the `Profile.HideHomeIntro` **per-resident** hide-intro
    preference (composable, not replaced — the two intro sections render
    when **both** `!HideIntro` **and** the platform flag are true).
  - ADR 0004 §B.1 — additive doc type (delta applied idempotently at boot,
    no EF migration).
  - ADR 0006 — module-boundary contract.
  - ADR 0015 D1 — `kw-l` provider-floor discipline.
  - `Milestones.cs` / README / `MilestonesTests.cs` — the M4
    single-`StatusNext` pin stays intact until the lane ships.
- **New invariants (SITE·1–SITE·9):** locked in ADR 0150, U02.

<!-- U00 appends its section below this line. One ## section per unit, in
order (U00, U01, … U08). Never rewrite a prior section. -->

## U00 — Kickoff verified

- **Date:** 2026-10-07
- **View section counts (verified by reading both views):**
  - `Views/Home/Index.cshtml` — **4 sections**:
    1. Hero (eyebrow + lead + scope + FIG + "What it is" button)
    2. Feature cards (3 cards)
    3. "What's new" feed (signed-in only)
    4. "The plan" (roadmap)
    Sections 1+2 are wrapped in `@if (!Model.HideIntro)` (ADR 0149).
    Section 3 is wrapped in `@if (Model.Feed is { } feed)`.
    Section 4 is unconditional.
  - `Views/StaticPages/About.cshtml` — **7 sections**:
    1. Hero (eyebrow + lead + two CTA buttons)
    2. Feature cards (3 cards)
    3. Scope band
    4. FIG philosophy band
    5. Code/docs band
    6. "What's new" changelog
    7. Contact CTA band
    All unconditional (no `@if` wraps).
- **`kw-l` key families (the canonical `en` source text in
  `KnownTranslationKeys.cs`):**
  - `home.*` — `home.intro_eyebrow`, `home.intro_lead`, `home.about_link`,
    `home.feature_feed_title/body`, `home.feature_groups_title/body`,
    `home.feature_pinned_title/body`, `home.feed_title`, `home.feed_empty`,
    `home.feed_posts`, `home.feed_announcements`, `home.feed_pages`,
    `home.feed_view_all`, `home.feed_badge_pinned`, `home.eyebrow`,
    `home.roadmap_heading`, `home.lead`, `home.roadmap.show_more_earlier`,
    `home.roadmap.show_more_upcoming`, `home.roadmap.status.done/next/planned`
  - `about.*` — `about.eyebrow`, `about.lead`, `about.cta_feed`,
    `about.cta_notes`, `about.features.one.title/body`,
    `about.features.groups.title/body`, `about.features.pinned.title/body`,
    `about.project.eyebrow`, `about.project.heading`, `about.project.lead`
  - `platform.*` — `platform.scope_home`, `platform.fig_home`,
    `platform.scope_eyebrow`, `platform.scope_heading`,
    `platform.scope_body1`, `platform.scope_body2`, `platform.fig_eyebrow`,
    `platform.fig_heading`, `platform.fig_body1`, `platform.fig_body2`,
    `platform.fig_body3`
  - `whatsnew.*` — `whatsnew.eyebrow`, `whatsnew.heading`, `whatsnew.lead`,
    `whatsnew.version`, `whatsnew.show_more_remaining`
  - `a11y.*` — `a11y.about_features`, `a11y.about_audience`,
    `a11y.about_philosophy`, `a11y.about_project`, `a11y.about_contact`
- **ADR number confirmed:** `0149` is the current highest in
  `docs/adr/README.md` (the index table ends at 0149) and the `docs/adr/`
  directory listing confirms no `0150-*.md` file exists. **0150 is the next
  free number.** The filename is `0150-site-content-customization.md`
  (per the register's close section).
- **Precedent ADRs (frozen, verified):**
  - ADR 0149 — `Profile.HideHomeIntro` per-resident preference (the
    composability pin: the two intro sections render when **both**
    `!HideIntro` and the platform flag are true; the "What's new" feed +
    roadmap are governed only by the platform flag).
  - ADR 0050 — single-write-lane shape (`AdminSignupController`, the
    `IsSignupOpen` best-effort read, the `SetSignupOpenAsync` audited write,
    the `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST, the
    `TempData["info"]` flash, the `RedirectToAction(nameof(Index))`
    redirect).
  - ADR 0019 / ADR 0020 — singleton-toggle admin surface shape
    (`/admin/timezone` / `/admin/dateformat`).
  - ADR 0005 B — `LocaleSettings` singleton (`Id = "singleton"` sentinel,
    the additive-field convention, one row per instance).
- **`LocaleSettings` shape confirmed:** `LanguageCatalog.cs` has
  `LocaleSettings` with `Id = "singleton"` sentinel, fields:
  `DefaultLanguageCode`, `DefaultTimezone`, `DefaultDateFormat`,
  `IsSignupOpen`, `NotifyAdminsOnSignup`, `AnnouncementCommentsEnabled`,
  `MessagingEnabled`, `QuietCheckMinutes`, `SamplePasswordChangeLocked`.
  The `SiteContent` doc will be a **new** singleton in the same shape,
  not a new field on `LocaleSettings` (ADR 0006 module-boundary contract).
- **`AdminSignupController` shape confirmed:** dedicated controller,
  `[Route("admin/signup")]`, `[Authorize(Roles = GlobalAdmin)]`,
  `IIdentityService` seam, `Save(bool isOpen)` POST + `SaveNotify` POST,
  `TempData["info"]` flash, `RedirectToAction(nameof(Index))`. The
  `AdminSiteController` will mirror this shape.
- **`HomeViewModel` shape confirmed:** `HomeViewModel(CommunityName,
  SupportEmail, Feed, HideIntro)` — U05 adds `SiteContent? Site`.
- **`StaticPagesController.About` confirmed:** calls
  `Page("about", fallBackToProductStory: true)` which falls through to
  `View("About", new HomeViewModel(...))` when no `about` Page exists.
  U06 will add the `SiteContent` read to this seam.
- **No code, no build.** The handoff-note skeleton is present.
