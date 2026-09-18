# Languages seeded (`LS`) — sealed unit register

> **Planned.** This is the **lane plan** (the secondary register tier) for the
> **Languages Seeded** lane — a first-boot instance ships **three** enabled
> languages (English, German, French) with **complete** UI-string and
> system-page (about / terms / help) baselines in all three, so the language
> feature is visibly working from first bootup. The **primary** reference tier
> is ADR **0042** (written in U00; the next free number after ADR 0041) plus
> the already-accepted seams this lane rides on: ADR 0005 (the multilingual
> seam), ADR 0015 (the registry + provider-floor mechanics), ADR 0039 / 0040
> (the system-page tree + the `PageTranslation` lane).
>
> **What this is:** a *data + seeder* lane. The whole read path already
> exists (preference → default → `en`, per-string fallback, `kw-l` TagHelper,
> the in-app editor, the completeness view). The lane adds **shipped initial
> values** for `de` and `fr` — catalog rows, `TranslationResource` rows,
> `PageTranslation` rows for the seeded system pages, and the `about` product
> surface wired through the registry — and pins the new first-boot state in
> tests. **No new `AccessAction`**, **no new `AccessVia`**, **no new
> authorization path**, **no schema change**, **no new doc type**, **no
> migration**. **M4/M5/M6 stay Events / Projects / Portability.**
>
> **Sizing:** units are sized for a ~32K-context fresh agent one at a time,
> each with its own exit criteria, in the `RC` / `RE` / `PG` style. **U00 is
> the sign-off gate** — it locks the ownership-semantics decision in ADR 0042
> before any data is written; every later unit codes against the *locked*
> text. **U01 adds the `about.*` keys to the registry** (the `EnValues`
> side); **U02 / U03 are the two baselines** (German, then French) and are
> **independent of each other** — either order; **U04** seeds the catalog +
> strings + system pages; **U05** wraps the `about` view in `kw-l`; **U06**
> pins the Web surface and runs the fresh-boot pass.

## Understanding (one paragraph)

A fresh instance today boots with exactly one enabled language: the seeder
writes the `en` catalog row, `LocaleSettings.DefaultLanguageCode = "en"`, and
one `TranslationResource` row per key **for `en` only** (plus the `en` bodies
of the seeded `terms` / `help` system pages). Every other language is
community-provided — an admin adds it under `/admin/languages` and a
GlobalAdmin / Translator fills the translations by hand. The result: the
platform's headline feature (multilingual UI) is invisible until somebody
sits down and types ~370 strings. This lane ships a **curated initial pack**
— German and French — inside the image: the first-boot seeder materializes
the `de` / `fr` catalog rows, the full UI-string baselines, and the `de` /
`fr` bodies of the system pages (including the `about` surface), so a
freshly deployed instance demonstrates the feature end-to-end: three
languages in the picker, full UI in any of them, per-string fallback
still protected by the `en` floor. English stays the source language and
the instance default; **the default language is `en`, exactly as it is
now.**

## The one thing every unit must respect

**Ownership semantics (locked in ADR 0042, U00):**

- **`en` is code-owned, forever.** `KnownTranslationKeys.EnValues` is the
  single source of truth; the seeder upserts `en` rows with **code-wins**
  semantics and the provider floor resolves any registered key to the
  registry's `en` text (ADR 0015 D1). Nothing in this lane changes that.
- **`de` / `fr` are seeded-once, then community-owned.** The baselines ship
  with the code as *initial values*; the seeder writes them **only on a
  pristine DB** (the `DbBootstrap.IsPristineAsync` gate — the seeder never
  runs on a warm instance). After first boot, the in-app editor
  (GlobalAdmin ∪ Translator, ADR 0021) is the only write path for `de` /
  `fr` rows, and **an admin edit is never overwritten** — the seeder's
  `de` / `fr` upserts are first-boot-only by construction.
- **New-code-key behavior on existing instances:** a key added to the
  registry later renders its `en` text on every instance (provider floor),
  but a `de` / `fr` baseline for that key does **not** appear in an existing
  instance's DB until an admin types it in-app (completeness view shows it
  missing). This is *asymmetric with `en` on purpose* — `en` is the floor,
  `de` / `fr` are community data. Recorded in ADR 0042, not a bug to fix.
- **The registry honesty invariant (ADR 0015) stays exact:** every key in
  `DeValues` / `FrValues` is a key in `EnValues` (parity, no dead weight),
  and `en` remains the one dictionary whose values are the *rendered*
  strings the views emit.
- **`about` is wired through the registry, not special-cased.** The
  `about` product surface is the one in-scope surface that is *not* yet
  behind `kw-l`; U01 registers its keys and U05 wraps the view. The TagHelper
  emits element *content* only — **HTML attributes (`placeholder` /
  `aria-label` / `title`), JS strings, and C#-built markup stay hardcoded**
  (the ADR 0015 hard exclusions, unchanged).

## Assumptions

- **Scope = data + seeder + `about` wrap.** In: the `about.*` keys added
  to the registry (`EnValues`), `DeValues` / `FrValues` baselines (full
  registry parity), the `de` / `fr` catalog rows in the seeder, the
  `de` / `fr` `TranslationResource` rows in the seeder, the `de` / `fr`
  bodies of the seeded system pages (`terms` / `help` via `PageTranslation`
  rows; `about` via the registry keys — it has no Markdown body), the
  `about` view wrapped in `kw-l`, and the test pins. **Out (→ future lanes):**
  machine translation of anything (ADR 0005 C — deferred trust boundary),
  additional languages (an admin does that in-app — that is the design),
  a warm-reseed / upgrade-push mechanism for `de` / `fr` (the asymmetry
  above is the decision), the `Milestones.cs` / README / `MilestonesTests`
  trio until the lane *ships* (U00 owns the ADR; the roadmap row lands at
  U06 with the rest of the docs sync — `Milestones.cs` currently pins
  **M4** as the single `StatusNext`, and `MilestonesTests` asserts that;
  an LS row added as `StatusDone` does not disturb that pin, so no re-order
  of the trio is needed to land LS).
- **The baseline quality bar is "a careful first pass, editable later."**
  The owner has accepted that an author (human or agent) produces the
  de/fr baselines as a reviewable first draft; the in-app editor + the
  completeness view are the review surface. The bar is *idiomatic* German /
  French UI copy (not word-for-word calques from the English), correct
  casing/punctuation per language convention, and consistency within each
  language (e.g. `du` vs. `Sie` picked **once per language** and held —
  default: German `du` (neighborhood register), French `tu` (same
  register); the ADR 0042 text records the choice).
- **The `terms` / `help` baselines are full translations of the *current*
  `EnDefaultPages()` bodies** — the exact Markdown, structure preserved,
  not a rewrite. (The bodies live in `FirstBootSeeder.EnDefaultPages()`,
  the single source; a baseline that paraphrases would drift.)
- **The `about` surface strings are the visible copy in
  `Views/StaticPages/About.cshtml`** (hero eyebrow/lead, the two CTAs, the
  three feature-card titles + bodies, the stats-band labels, the project
  section headings). The `TODO(counts)` stats band: U01 registers the four
  **labels** only (the values stay placeholder; the band's own TODO stays —
  it is out of scope).
- **`en` default is unchanged.** `LocaleSettings.DefaultLanguageCode`
  remains `"en"`; `SourceLanguage` remains `"en"`; the picker's first entry
  remains English (sort 0). This lane is about *available* languages, not
  about switching the default.

## The units

### U00 — Sign-off + ADR 0042  *(no code beyond the ADR)*

Write `docs/adr/0042-bundled-initial-language-pack.md` (Accepted) locking:
the ownership semantics above (en code-owned / de-fr seeded-once-then-
community-owned / the new-key asymmetry), the **du / tu register choice**
(German `du`, French `tu`), the `about`-via-registry decision (wrapping is
a `kw-l` + registry change, not a new mechanism), the "default stays `en`"
pin, the `about.*` key list (the stable contract U01–U05 code against),
and the scope in/out. Record it as an **amendment to ADR 0005 §B**
("every other language is community-provided" → "de/fr additionally ship
as bundled initial values, editable in-app; en remains the only
code-owned language") with a cross-reference back. **Exit:** ADR 0042
accepted; ADR 0005 carries the amendment note; **no other file changed.**

### U01 — `about` keys in the registry (`EnValues` additions)

Register the `about.*` keys in `KnownTranslationKeys.EnValues` — the
**exact current English** copy from `Views/StaticPages/About.cshtml`: the
hero eyebrow + hero lead, the two CTA labels, the three feature-card
titles + bodies, the four stats-band labels, and the static platform text
of the "The project" section. The key names follow the ADR 0042 list (the
stable key contract U02/U03 translate against); U05 wraps the view against
these exact names. **Hard exclusions hold:** the `TODO(counts)` placeholder
values are not keys; attributes / JS strings / C#-built fragments stay
hardcoded (the ADR 0015 exclusions, unchanged). **No view change in this
unit** — the view still renders its hardcoded English, which equals the
registry values (the ADR 0015 invariant: the registry values are the *exact
current English* in the view). **Exit:** `dotnet build` green; a Core
parity test (this unit adds `KnownTranslationKeys_ParityTests`) pins:
`EnValues` keys == `AllKeys`, and every new `about.*` key's value is
non-empty; `dotnet exec …Kumunita.Core.Tests.dll` green.

### U02 — German baseline (`KnownTranslationKeys.DeValues`)

Author `DeValues` in `KnownTranslationKeys` — **one entry per key in
`AllKeys`** (registry as of U01, i.e. the existing keys **plus** the
`about.*` keys). Idioms: German UI register (`du`), sentence case, no
trailing period on button labels, `ß` allowed, embedded data values kept
token-for-token with the English (any `{0}`-style placeholder or inlined
data value must survive translation exactly). **Exit:** the parity test
family extended: `DeValues` keys == `AllKeys` exactly (no missing, no
extra, no empty values); `dotnet exec …Kumunita.Core.Tests.dll` green.

### U03 — French baseline (`KnownTranslationKeys.FrValues`)

Same shape as U02 for `FrValues`: one entry per key in `AllKeys`, idiomatic
French UI copy (`tu` register, accents/typography per French convention —
narrow no-break space before `:` `;` `?` `!` is the typographic ideal, a
plain space is acceptable if consistent — **pick one and hold it**),
placeholders preserved token-for-token. **Independent of U02** (either
order of U02/U03). **Exit:** the parity test family extended to
`FrValues`; `dotnet exec …Kumunita.Core.Tests.dll` green.

### U04 — Seeder: catalog rows + `de`/`fr` UI strings + system-page translations

Runs after U01–U03 (needs the baselines to exist). Extend `FirstBootSeeder`:

1. `SeedLanguageCatalogAsync` also upserts `de` ("Deutsch", enabled, sort
   1) and `fr` ("Français", enabled, sort 2) — same load-then-Store
   idempotent shape as the `en` row; the singleton default **stays** `en`.
2. `SeedTranslationResourcesAsync` upserts one `TranslationResource` row
   per key for `de` and `fr` from `KnownTranslationKeys.DeValues` /
   `FrValues` — **first-boot-only by construction** (the outer
   `IsPristineAsync` gate); the existing `en` code-wins loop is untouched
   and the `de` / `fr` loops are plain create-if-missing upserts (no
   refresh of an admin's later edit is even possible warm, but on a
   pristine DB "create" and "upsert" coincide — keep the same
   query-then-Store idiom for shape consistency).
3. The seeded system pages carry their `de` / `fr` bodies: seed a
   `PageTranslation` row (the ADR 0022/0026/0029 shape, the PG U06 lane)
   under each seeded system root (`terms`, `help`) for `de` and `fr`, from
   baselines authored in this unit (full translation of the *current*
   `EnDefaultPages()` bodies, structure preserved). `about` is the
   registry-key surface (U01/U02/U03), **not** a `PageTranslation` — it has
   no Markdown body. **Note for the unit's executor:** the seeded pages
   live under the `system` root with `PageKind.System` (ADR 0040); the
   `PageTranslation` rows attach to the **system root page's** `Id` (the
   PG U06 lane shape — verify the exact parentage against `PageService`
   when executing).
4. Update the `FirstBootSeeder` class-doc steps 4 + 5 to name the new seed
   content, and the log lines ("First boot: … `de`+`fr` baselines …").

**Tests (this unit adds the first-boot state pins, Core):**

- After a seeded boot: the catalog is exactly `en` (sort 0) / `de` (sort 1)
  / `fr` (sort 2), all enabled; `LocaleSettings.DefaultLanguageCode == "en"`.
- The completeness view (M·12) reports **100% present** for `de` and `fr`
  (0 missing keys) on a fresh boot — the direct pin for "it clearly shows
  how the language features work from the first bootup".
- `TranslationProvider.GetAsync` resolves **every** key to the baseline
  value (not the `en` floor) under a `de` preference and under a `fr`
  preference.
- Per-string fallback still lands on `en`: delete one `de` row, the rest
  of the `de` page stays `de`, that one key renders the `en` text.
- Warm-boot no-op: a second `SeedAsync`-style re-run (the test mirrors the
  seeder shape, the `SeedEnFloor` precedent) leaves an admin-edited `de`
  value **unchanged**.

**Exit:** `dotnet build` green; `dotnet exec …Kumunita.Core.Tests.dll`
green (Testcontainers ~20 s); **no view or Web code changed.**

### U05 — `about` view wrapped in `kw-l`

Wrap the `about.*` strings in `Views/StaticPages/About.cshtml` in `kw-l`
against the keys U01 registered (the ADR 0042 key list is the stable
contract; the view and the registry must use the **same** key names).
**Hard exclusions hold:** the `TODO(counts)` placeholder values are not
wrapped; attributes / JS / C#-built fragments stay hardcoded. **Exit:**
the `about` view renders **byte-identical English** as before for a
cookie-less visitor (same strings, now emitted via the provider — the
`en` DB row or the code floor, which are equal by the ADR 0015 invariant);
with a `de` / `fr` preference the whole surface is in that language
(U02/U03 baselines + the U04 seeder rows); `dotnet build` + both test
assemblies green.

### U06 — Web surface pins + fresh-boot manual verification

- **Web tests:** the public `/language` picker and the settings language
  surface list the catalog rows in `SortOrder` — pin a catalog with
  `en`/`de`/`fr` (the `PublicLocaleAndAboutTests` shape) and assert the
  three options in order; the completeness admin view renders `de` / `fr`
  at 100%.
- **Fresh-boot manual pass** (the lane's real exit): `docker compose
  down -v`-equivalent wipe of the `mt` volume per OPS, fresh boot, and:
  (a) the First-boot log shows the new seed lines exactly once; (b)
  cookie-less `/` renders **English** (default unchanged); (c) the picker
  shows English / Deutsch / Français and selecting each renders nav,
  footer, Home, settings, **about**, `/terms`, `/help` fully in that
  language; (d) the completeness view shows `en`/`de`/`fr` each 100%;
  (e) editing one `de` string in-app persists and survives a warm reboot;
  (f) `docker compose up -d --build` again is a warm boot — **no reseed**,
  the edit survives. Record the pass in the handoff notes below.
- **Docs sync (same commit as the pass):** the README multilingual note +
  Roadmap (the lane's one-line entry), `Milestones.cs` (the `LS` row →
  `StatusDone`), and `MilestonesTests` in step (its pin is that **M4** is
  the single `StatusNext`; adding LS as `StatusDone` leaves that pin
  intact — no re-order needed), and the ADR 0005 amendment note if U00
  left it as a forward pointer. **Exit:** both test assemblies green
  (the `dotnet exec …dll` path, per AGENTS.md — **not** `dotnet test` /
  Test Explorer on this machine); the manual pass recorded.

## Handoff notes

(One appended `## U#` section per unit, never rewritten — the scratch tier.)

## U04

The seeder (`FirstBootSeeder`) now ships the bundled initial pack (ADR 0042 D1/D2/D4):

- **Catalog** (`SeedLanguageCatalogAsync`): `de` ("Deutsch", enabled, sort 1) and
  `fr` ("Français", enabled, sort 2) added alongside the existing `en` (sort 0) row —
  same load-then-Store idempotent shape. `LocaleSettings.DefaultLanguageCode` stays
  `en` (D4).
- **UI strings** (`SeedTranslationResourcesAsync`): the existing `en` code-wins loop is
  untouched; new `de` / `fr` loops upsert one `TranslationResource` row per key from
  `KnownTranslationKeys.DeValues` / `FrValues`, **create-if-missing** (existing rows are
  skipped, never refreshed — an admin edit is never overwritten, ADR 0042 D1; first-boot
  only by construction under the outer `IsPristineAsync` gate).
- **System pages** (`SeedDefaultPagesAsync` + new `SeedPageTranslationsAsync`): the
  `terms` / `help` pages now carry `de` / `fr` bodies as `PageTranslation` rows (the PG
  U06 lane shape), from new `DeDefaultPages()` / `FrDefaultPages()` public statics
  (full translations of the `EnDefaultPages()` bodies, structure preserved).
  `about` is NOT a `PageTranslation` (the registry-key surface — no Markdown body).

**Parentage (auditable):** `PageTranslation.PageId` is the **terms / help page's own
`Id`** — the `Page` doc under the `system` root — *not* the `system` root container's
`Id`. `SeedDefaultPagesAsync` now returns a `slug → page.Id` map (captured in the
new-page, orphan-re-parent, and existing-page branches) which `SeedPageTranslationsAsync`
attaches the rows to. The read path (`PageController` → `IPageService
.GetTranslationsAsync(page.Id)`) queries `PageTranslation.PageId == page.Id`, so the
system-root id is *not* a valid parent. The U06 test pins this directly (including a
`count == 0` for any row on the system root itself).

**Body lengths** (terms / help, chars): `en` 522 / 638; `de` 610 / 740; `fr` 647 / 715.

**ADR 0042 D6** was corrected to record the page-own-Id parentage (it had said "the
system root page's id" — the ambiguous reading; now explicit).

**Tests added** (`tests/Kumunita.Core.Tests/LS_U04_SeederTests.cs`, 6 pins): catalog
`en`/`de`/`fr` (sort 0/1/2, all enabled, default stays `en`); completeness 100% for `de`
and `fr` on a fresh boot; `TranslationProvider.GetAsync` resolves every key to the
`de` / `fr` baseline (not the `en` floor); per-string fallback lands on `en` when one
`de` row is deleted (the rest stays `de`); warm re-run leaves an admin-edited `de` value
unchanged (no duplicate row); `terms` + `help` each have a `de` and a `fr`
`PageTranslation` on the page's own `Id` (not the system root), body non-empty,
parity with the `De/FrDefaultPages()` sources.

**Exit (green):** `dotnet build Kumunita.slnx -c Debug` → 0 errors / 0 warnings.
`dotnet exec …Kumunita.Core.Tests.dll -class LS_U04_SeederTests` → 6/6 pass. Full Core
assembly → **Total: 529, Errors: 0, Failed: 0** (Testcontainers, ~53 s). No view or Web
code changed; no new doc type; no schema/migration.
