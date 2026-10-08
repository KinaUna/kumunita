# SITE U00 — Kickoff verification

## Context (self-contained)

`SITE` is the **site content customization** lane: the platform's two
*landing* surfaces — `/home` (`Views/Home/Index`) and `/about`
(`Views/StaticPages/About`) — are currently **hard-coded**. Every word of
copy is a `kw-l`-wrapped English string in `KnownTranslationKeys.cs` (the
`home.*` / `about.*` / `platform.*` key families), and the *presence* of
each section is fixed in the Razor view. This lane adds a single
**platform-level `SiteContent` singleton** (a new doc in a new
`Kumunita.Core.SiteContent` context) with the per-section fields an admin
actually needs: **editable text** for the two heroes' eyebrow + lead, and
**show / hide** flags for every other section of both surfaces. Defaults
are byte-identical to the shipped `kw-l` text.

**What this unit does:** verify the surface + the precedent shapes, confirm
the ADR number is free, and author the handoff-note skeleton. **No code, no
build.** This is the sign-off gate — it locks the scope before any copy is
written.

**The precedents this lane rides on (frozen, verified):**
- ADR 0005 B — the `LocaleSettings` singleton (`Id = "singleton"` sentinel,
  the additive-field convention, one row per instance).
- ADR 0019 / ADR 0020 — the singleton-toggle admin surface shape
  (`/admin/timezone` / `/admin/dateformat`, `GlobalAdmin`-gated, one
  `AccessAudit` row per save).
- ADR 0050 — the single-write-lane shape (`AdminSignupController`, the
  `IsSignupOpen` best-effort read, the `SetSignupOpenAsync` audited write).
- ADR 0149 — the `Profile.HideHomeIntro` **per-resident** hide-intro
  preference (composable, not replaced — see the "one thing" section below).
- The `LS` / `SP` / `PG` seeder + test model (create-if-missing, idempotent,
  never-overwrites; the "seeded defaults match the shipped text" pin).

**The one thing every unit must respect (SITE lane, locked in ADR 0150):**
- The read is a **public landing surface** (world-readable, not an access
  decision, not a claim — the ADR 0001-B thin-token rule; ADR 0050
  strong-consistency shape). A missing singleton degrades to the
  **shipped defaults** (byte-identical `kw-l` text + every section shown) —
  never a blank page.
- The write is the **ADR 0050 single-write-lane shape** (one
  `ISiteContentService.SaveAsync` lane, one `AccessAudit` row per save,
  `Via = Admin`, action `site.save`, `TargetKind` "site").
- The defaults are **byte-identical to the shipped `kw-l` text** (the
  `en` source text). The `kw-l` registry entries **stay** (not removed, not
  re-shaped).
- The ADR 0149 `Profile.HideHomeIntro` is **composable, not replaced** —
  the two intro sections render when **both** the per-resident preference
  is off (`!HideIntro`) **and** the platform flag is on
  (`HomeShowFeatures`). The "What's new" feed + roadmap are governed only
  by the platform flag.
- The `SiteContent` doc is a **singleton** (one row per instance,
  `Id = "singleton"`), additive per ADR 0004 §B.1, **no EF migration**.
- `M4/M5/M6 stay Events / Projects / Portability` — no roadmap letter moves.

## Goal

Verify the surface (the two landing views, the `kw-l` keys, the ADR 0149 /
ADR 0050 precedent shapes, the `LocaleSettings` singleton shape), confirm
`0150` is the next free ADR number, and author the handoff-note skeleton
(the "Lane open" section). **No code, no build.**

## Entry reads (7 files)

1. `src/Kumunita.Web/Views/Home/Index.cshtml` — the `/home` landing view
   (the hero band, the three feature cards, the "What's new" feed, the
   "The plan" roadmap section; the ADR 0149 `@if (!Model.HideIntro)` wrap).
2. `src/Kumunita.Web/Views/StaticPages/About.cshtml` — the `/about` landing
   view (the hero, the three feature cards, the scope band, the
   FIG-philosophy band, the code/docs band, the "What's new" changelog, the
   contact CTA band).
3. `src/Kumunita.Web/Controllers/HomeController.cs` — the `/home`
   controller (the ADR 0149 `HideHomeIntro` read, the `HomeViewModel` shape).
4. `src/Kumunita.Web/Controllers/StaticPagesController.cs` — the `/about`
   controller (the `fallBackToProductStory` seam, the `HomeViewModel` shape).
5. `src/Kumunita.Core/Localization/LanguageCatalog.cs` — the
   `LocaleSettings` singleton (the `Id = "singleton"` sentinel shape, the
   additive-field convention: `IsSignupOpen` / `NotifyAdminsOnSignup` /
   `AnnouncementCommentsEnabled` / …).
6. `src/Kumunita.Web/Controllers/AdminSignupController.cs` — the ADR 0050
   `/admin/signup` surface (the `GlobalAdmin`-gated single-write-lane shape,
   the `AccessAudit` row, the `TempData["info"]` flash).
7. `docs/adr/README.md` — the ADR index. **Confirm `0149` is the current
   highest (so `0150` is next).** Read `docs/adr/0149-home-intro-hide-
   preference.md` + `docs/adr/0050-admin-managed-signup-gate.md` for the
   precedent shape.

## Deliverables (1 file, new)

`docs/plans-milestones/in-progress/site-handoff-notes.md` — the **skeleton
only** (the header + the "Lane open" section + the
`<!-- U00 appends its section below this line. One ## section per unit, in
order (U00, U01, … U08). Never rewrite a prior section. -->` marker). The
skeleton mirrors the `wysiwyg-handoff-notes.md` / `system-pages` handoff
shape (the "Lane open" section names the register, the design doc, the ADR,
the scope, the out-of-scope deferrals, and the frozen base). **No**
`## U<m> —` section yet (U00 appends its own section after this one).

## Exit

- The handoff-note skeleton is present.
- The `## Lane open` section names:
  (a) the two landing surfaces (`/home` + `/about`, the two views);
  (b) the `kw-l` key families (`home.*` / `about.*` / `platform.*` in
  `KnownTranslationKeys.cs`);
  (c) the frozen base (ADR 0005 B `LocaleSettings` singleton shape,
  ADR 0019 / ADR 0020 singleton-toggle, ADR 0050 single-write-lane, ADR 0149
  `Profile.HideHomeIntro` pin — **unchanged**);
  (d) the 13 `SiteContent` fields the register names (the 4 text fields +
  the 9 toggle fields — by name);
  (e) the **ADR 0150** (the next free number after 0149).
- Append a `## U00 — Kickoff verified` section to the handoff note with the
  view section counts + the `kw-l` key list + the ADR number (0150) + the
  precedent ADR list (0149 / 0050 / 0019 / 0020 / 0005 B).
- **No code, no build.** `git status` clean except the one new handoff-note
  file.
- Move this unit plan `in-progress/site-u00.md` → `done/site-u00.md` (move
  **last**).

## Notes / deviations

- The 13 `SiteContent` fields (the register's "one thing" section):
  `HomeHeroEyebrow`, `HomeHeroLead`, `HomeShowAboutButton`,
  `HomeShowFeatures`, `HomeShowRoadmap`, `AboutHeroEyebrow`,
  `AboutHeroLead`, `AboutShowFeatures`, `AboutShowScope`,
  `AboutShowPhilosophy`, `AboutShowProject`, `AboutShowWhatsNew`,
  `AboutShowContactCta`.
- The ADR 0150 filename is **`0150-site-content-customization.md`** (the
  register's close section names it); U02 verifies the name + number
  against the ADR index before accepting.
