# M10 handoff notes

One `## U#` section per unit, **appended, never rewritten** (the shared
scratch tier of the three-tier contract — see the register header). Each
entry: files written/touched, decisions locked or refined (with the
design-doc / ADR section it maps to), anything that drifted from the plan's
text, and the next unit's entry reads.

The register is `docs/plans-milestones/plan-m10-pwa-responsive.md`. Each
unit ships its own self-contained plan in `in-progress/pwa-responsive-uNN.md`;
when a unit is done its plan file moves to `done/`. The **next unit's** agent
reads its own unit plan + this file's most recent `## U#` section + its entry
reads — it does not re-derive the register.

## Kickoff — M10 is the in-progress milestone; register + unit plans authored

- **What this is.** M10 (PWA and responsive design) is already the single
  `StatusNext` milestone in `Milestones.cs` (the M9 U07 close moved it
  there; no M10 work has started — `in-progress/` is empty and there is no
  `src/Kumunita.Core` messaging-*adjacent* PWA surface, no manifest, no
  service worker, no `pwa-install.ts` anywhere: grep-confirmed greenfield).
  This kickoff records the register's authoring, not a milestone-status
  change (M10 is already `StatusNext` — the flip happens at **U07 close**,
  when M10 → `StatusDone` and M11 → `StatusNext`).
- **Files touched (this kickoff: the plan tier only):**
  - `docs/plans-milestones/plan-m10-pwa-responsive.md` — the unit register
    (U00–U07), [PROPOSED] D1–D10 + C-M10·1–8 + F1–F7, per-unit Goal /
    Entry reads / Deliverables / Pinned tests / Exit.
  - `docs/plans-milestones/in-progress/pwa-responsive-u00.md` …
    `pwa-responsive-u06.md` — the seven self-contained unit plans (U00–U06;
    U07's close is defined in the register's `## U07` section directly —
    the close unit reads it there, no separate in-progress file).
  - `docs/plans-milestones/m10-pwa-handoff-notes.md` — this file.
- **Open veto window:** D1–D10 in the register are the [PROPOSED] set the
  user can still change cheaply **before U00 runs**. After U00 they are
  locked by `docs/design/m10-pwa-responsive-design.md` + ADR 0107.
- **ADR number for M10:** **0107** (the index ran 0001–0106; `0107` was
  free — verified against `docs/adr/README.md`; `0106` is the to-do
  self-assign lane, the current highest). U00 will author
  `docs/adr/0107-pwa-and-responsive-design.md` + the `docs/adr/README.md`
  row.
- **Grounding facts verified at kickoff (U00's entry reads should re-verify,
  not trust this):** Bootstrap 5.3 precompiled dist CSS + the `.kmb-*`
  design-system layer in `site.css`; six existing
  `@media (max-width: 767.98px)` blocks at `site.css` lines 578 / 1058 /
  1407 / 1694 / 2105; CSP shipped in `Program.cs` (~line 486–512,
  `script-src 'self'`); the tsc-only pipeline (`package.json` "build": "tsc",
  `client/**/*.ts` → `wwwroot/js`); the logo PNGs are only 42×42 + 64×64
  today (the manifest's 192/512 pair are new assets); the Playwright harness
  config is `tests/Kumunita.Web.Tests/playwright.config.ts` (`baseURL`
  `http://localhost:5199`, the M3/M4 author-not-run precedent's specs live
  there); `Milestones.cs` M10 = `StatusNext`, M11–M14 `StatusPlanned`.
- **Not touched (historical records, per the "don't edit done records"
  discipline):** the M6/M9 deferral notes that name "push / PWA push (M9
  owns PWA)" (ADR 0076/0083/0084 + the `done/notifications/` handoff) —
  those are done-milestone records; M10's design doc *supersedes* the
  deferral by owning the substrate and re-naming the follow-on lane "PWA
  push" (each named in the register's Deferred lanes list).
- **Next:** U00 (see `in-progress/pwa-responsive-u00.md`).

## U00 — design doc + ADR 0107

- **Files written:**
  - `docs/design/m10-pwa-responsive-design.md` — the primary tier,
    LOCKED. All sections: Context, D1–D10 (locked verbatim),
    §manifest, §SW (the 15-path allowlist + gates + fall-through line),
    §Responsive (the pinned breakpoint + closed split inventory),
    §a11y floors (Floor A + Floor B), §install, §icons, Invariants
    C-M10·1–8, FACES F1–F7, §pinned tests (9 Playwright + 7 xUnit),
    §drift-guard (7 frozen pins + drift log (a)–(e)), three acceptance
    tests.
  - `docs/adr/0107-pwa-and-responsive-design.md` — Accepted,
    2026-09-27, Amends 0031/0015/0076+0083+0084/0092/0105, D1–D10,
    4 rejected alternatives, Consequences (5 named deferred lanes).
  - `docs/adr/README.md` — one index row (0107), after the 0106 row.
  - `docs/plans-milestones/m10-pwa-handoff-notes.md` — this entry.

- **Decisions:** **D1–D10 all locked as user-approved; no veto was
  recorded.** The D-item rule text was carried into the design doc
  verbatim in substance; the refinements below are drift log entries,
  not changes of rule. No D-item text was rewritten.

- **Drift pauses flagged (all in the design doc §drift-guard log):**
  1. **(a) Media-block count:** the register/kickoff said "six
     `@media (max-width: 767.98px)` blocks at lines 578/1058/1407/
     1694/2105". Actual: **four** `max-width: 767.98px` blocks at lines
     **578** (pinned-announcement stack), **1407** (airy layouts single
     column), **1694** (events week-grid scroll), **2105** (kanban lane
     min-widths) + **one** `prefers-reduced-motion: reduce` at line
     **1058**. The kickoff miscounted the reduced-motion block as a
     width block. Corrected baseline pinned for U06's
     `Site_Css_Media_Block_Boundary_Pinned`.
  2. **(b) Allowlist narrower than register prose:** the register D2
     named "the fonts, the images" as allowlisted. The locked 15-path
     set **excludes** fonts + logo images (narrower = safer for the
     C-M10·2 privacy pin; additions live in the deferred "offline
     authenticated pages" lane). Direction: safe (fewer cached assets =
     less surface).
  3. **(c) `:root` hexes:** verified matching — `--bs-body-bg:
     #fbfaf7` at line 78, `--bs-primary: #1c4532` at line 106. No
     drift.
  4. **(d) Icon source:** the register did not name which logo PNG is
     the source. Resolved to the **64×64** (the `apple-touch-icon`
     source, line 19 of `_Layout.cshtml`; the 42×42 is the navbar
     brand's). Locked in §icons provenance.
  5. **(e) ADR number:** 0107 confirmed free (index ran 0001–0106).

- **SW allowlist (15 paths, exact, verbatim from the design doc §SW):**
  `/` · `/about` · `/css/site.css` · `/js/lib/audience-toggle.js` ·
  `/js/lib/avatar-upload.js` · `/js/lib/avatar.js` · `/js/lib/confirm.js`
  · `/js/lib/detach-menu.js` · `/js/lib/directory-card.js` ·
  `/js/lib/dom-to-markdown.js` · `/js/lib/expand-page.js` ·
  `/js/lib/flash-toast.js` · `/js/lib/rich-editor.js` ·
  `/js/lib/translation-swap.js` · `/manifest.webmanifest`
  - Four gates: same-origin, GET-only, exact-path (no prefix), no
    `Authorization` header.
  - Fall-through line: `event.respondWith(fetch(event.request));`
  - Cache name: `kumunita-shell-v1`.
  - No precache. Stale-while-revalidate. `activate` deletes other
    names. No `skipWaiting` / `clients.claim`.

- **Responsive U04 half (chrome + shared surfaces):**
  navbar (`.navbar-toggler` 44 px fix), account nav (toggle/bell/
  dropdown rows 44 px), flash toast (`.btn-close` 44 px), pinned
  announcement (extend the line-578 block, don't rewrite), footer
  (pin-only, existing `col-6` stacks).

- **Responsive U05 half (content + composer surfaces):**
  post/reply card + `.action-glyph-btn` 44 px floor (Detail.cshtml
  markup change allowed), group card (`.dname`/`.daddr`
  overflow-wrap), events calendar grid (line-1694 block is the fix;
  day-cell ≥ 32 px witness), Kanban board (line-2105 block is the fix;
  `.action-glyph-btn` floor), WYSIWYG split-view (1 column at 360 px),
  forms with audience editor (stack; checkbox rows 44 px at U05's
  judgment), tables (`.table-responsive` witness).

- **A11y Floor A (touch targets ≥ 44×44 px at 360 px viewport):**
  - U04: `.navbar-toggler`, `a#accountMenu`, `a#notifications-bell`,
    account-menu + language-picker `dropdown-item` rows, flash-toast
    `.btn-close`, pinned-announcement `.btn-close`.
  - U05: every `.action-glyph-btn`, audience-panel checkbox label rows.

- **A11y Floor B (focus-visible):**
  `.action-glyph-btn` ring fix (class home at site.css line 1828,
  viewport-independent), `.kmb-flash-toast` `.btn-close` (witness;
  drift pause if absent).

- **Icon provenance (locked):**
  - Source: `src/Kumunita.Web/wwwroot/images/logo/kumunita_k_logo_64x64.png`
  - Targets: `src/Kumunita.Web/wwwroot/images/pwa/icon-192.png` +
    `src/Kumunita.Web/wwwroot/images/pwa/icon-512.png`
  - Script: `.tmp/generate-pwa-icons.mjs` (dependency-free, re-runnable;
    the committed PNGs are the artifact, the script the provenance
    witness).

- **`pwa.install` locked strings (all four languages):**
  en `Install app` · de `App installieren` · fr `Installer
  l'application` · da `Installér app`.

- **Verified `:root` tokens:** `--bs-body-bg: #fbfaf7` at
  `site.css` line 78; `--bs-primary: #1c4532` at `site.css` line 106.

- **`@media` baseline for U06:** four `@media (max-width: 767.98px)`
  blocks (lines 578 / 1407 / 1694 / 2105) + one `@media
  (prefers-reduced-motion: reduce)` (line 1058).

- **Pinned tests — Playwright (9, `e2e-pwa-responsive.spec.ts`):**
  `Manifest_Fetch_And_Shape` · `Icons_200_And_Correct_Dimensions` ·
  `ServiceWorker_Registered_In_Chromium` ·
  `Offline_Shell_Revisit_Of_Root_Renders` ·
  `SignedIn_Route_Not_In_ServiceWorker_Cache` ·
  `Viewport_360px_Navbar_AccountNav_FlashToast_PinnedAnnouncement_
  Footer_No_Overflow` ·
  `Viewport_360px_PostReplyCard_GroupCard_Calendar_Kanban_WYSIWYG_
  No_Overflow` · `TouchTargets_44px_Floor_Holds_At_360px` ·
  `FocusVisible_Holds_On_Kmb_Custom_Pieces`.

- **Pinned tests — xUnit `PwaManifestTests` (7):**
  `Manifest_Json_Parses_And_Has_Required_Fields` (U01) ·
  `Manifest_Icon_192_And_512_Exist_In_Repo` (U01) ·
  `Layout_Contains_Manifest_Link` (U01) ·
  `ServiceWorker_File_Exists_And_Is_SameOrigin_Wwwroot` (U02) ·
  `ServiceWorker_Allowlist_Matches_Design_Doc_Verbatim` (U02) ·
  `Pwa_Install_Kw_L_Key_Registered_In_All_Four_Languages` (U06) ·
  `Site_Css_Media_Block_Boundary_Pinned` (U06).

- **ADR number confirmed free:** **0107** (the index ran 0001–0106;
  `0106` is the to-do self-assign lane).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green (docs only).
- **Next:** U01 (see `in-progress/pwa-responsive-u01.md`).
