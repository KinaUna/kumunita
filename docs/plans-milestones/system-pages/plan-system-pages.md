# System pages shipped + discoverable (`SP`) — sealed unit register

> **Planned.** This is the **lane plan** (the secondary register tier) for the
> **System Pages Shipped** lane — a first-boot instance ships the full
> platform-page set (**About**, **Terms**, **Help**, **Privacy Policy**,
> **Code of Conduct**) with complete baselines in all three languages (en /
> de / fr), and the pages are **reachable**: a footer "Platform" column links
> all five for every visitor, and the `/admin` shell gets a "Platform pages"
> affordance for the edit lane. The **primary** reference tier is ADR
> **0043** (the next free number after ADR 0042) plus the already-accepted
> seams this lane rides on: ADR 0005 (the multilingual seam), ADR 0015 (the
> registry + `kw-l` provider-floor mechanics), ADR 0039 / 0040 (the page tree
> + the two kinds, `system/` root), the `LS` lane (ADR 0042 — the bundled
> initial pack: `en` code-owned, `de`/`fr` seeded-once-then-community-owned).
>
> **What this is:** a *data + routes + discoverability* lane. Two of the five
> pages are **new content** (`privacy`, `conduct` — no route and no `Page`
> doc exist today); three exist but are **unreachable from the normal UI**
> (`terms` / `help` are seeded but nothing links them; `about` renders the
> product-story view with no link anywhere). The lane adds the two new
> baselines (en body + de/fr `PageTranslation` rows), the two new hard-coded
> routes, the footer column, the admin affordance — and pins the whole state
> in tests. **No new `AccessAction`**, **no new `AccessVia`**, **no new
> authorization path**, **no schema change**, **no new doc type**, **no
> migration** (it reuses `Page` / `PageTranslation` and the existing
> `PageKind.System` standing matrix of ADR 0040). **M4/M5/M6 stay Events /
> Projects / Portability.**
>
> **Sizing:** units are sized for a ~32K-context fresh agent one at a time,
> each with its own exit criteria, in the `LS` / `PG` style. **U00 is the
> sign-off gate** — it locks the page-set and the discoverability decision in
> ADR 0043 before any copy is written; every later unit codes against the
> *locked* text. **U01** adds the `privacy` / `conduct` `en` bodies + the two
> new routes; **U02** adds the de/fr baselines (the `LS` U04 bar); **U03** is
> the discoverability surface (footer column + admin section) and is
> **independent of U02** (different assemblies — either order); **U04** pins
> the Web surface and runs the fresh-boot pass; **U05** ships the doc trio.

## Understanding (one paragraph)

Today a fresh instance serves three static-page routes: `/terms` and `/help`
read the seeded `system/terms` / `system/help` `Page` docs (en body + de/fr
`PageTranslation` rows, the `LS` U04 shape), and `/about` degrades to the
product-story view because **no `about` page is seeded** (the U05 drift pin —
deliberate, and this lane keeps it). But *nothing* links any of them: the
footer has no platform column, the navbar has no entry, and the only
incidental reach is the `/pages` tree browse. An admin finds the pages only
by knowing the tree exists; a resident finds them only by guessing a URL.
And two pages that any defensible self-hosted deployment needs — a Privacy
Policy and a Code of Conduct — do not exist at all. This lane closes both
gaps in one pass: it seeds the two new pages (in three languages, from the
platform's own `SECURITY.md` privacy model and the ADR 0040 standing
conventions), and it makes all five first-class citizens: a footer "Platform"
column linking About / Terms / Help / Privacy / Conduct for every visitor,
and a "Platform pages" section on the `/admin` shell that lists the seeded
pages with preview + edit links. English stays the source language; the
default language stays `en`; the `du` / `tu` register choice of ADR 0042
D2 is held for the new copy.

## The one thing every unit must respect

**Page-set + discoverability semantics (locked in ADR 0043, U00):**

- **The shipped set is exactly five surfaces**, rendered at five hard-coded
  routes: `/about` (the product-story **view**, *not* a `Page` doc — the
  `LS` U05 drift pin stays), `/terms`, `/help`, `/privacy`, `/conduct`
  (the four `Page` docs, `Kind = System`, under the `system` root,
  `Audience = null` public, `AuthorId` empty — the exact ADR 0040 §2
  standing matrix: GlobalAdmin-only edit/move/delete, GlobalAdmin ∪
  Translator for translations). No more, no fewer, on first boot.
- **Content ownership follows ADR 0042 D1 unchanged:** the `en` bodies are
  **code-owned** (the seeder refreshes them — the existing `SeedDefaultPagesAsync`
  "code wins" branch, unchanged); the `de` / `fr` bodies are
  **seeded-once, then community-owned** (`PageTranslation` rows,
  create-if-missing, never refreshed — the `LS` U04 `SeedPageTranslationsAsync`
  shape, unchanged). An admin edit is never overwritten.
- **`about` stays a view, not a page.** No `about` slug enters
  `EnDefaultPages()` / `DeDefaultPages()` / `FrDefaultPages()`; no `about`
  `Page` doc is seeded. `/about` renders the product-story view on a fresh
  instance (the `fallBackToProductStory` pin of
  `StaticPagesController`), and an admin-created `system/about` page still
  wins when present (the existing route behavior, unchanged).
- **Discoverability is unconditional and floor-honest.** The footer links
  render for **every** visitor regardless of seed state (the footer is the
  canonical navigation surface); a link whose page is absent from the tree
  lands on the existing floor (`/about` → product story; `/privacy` /
  `/conduct` → the 404 page floor). No conditional hiding, no existence
  probe in the layout. On a fresh instance all five links resolve.
- **The navbar stays clean.** No navbar entry for these pages (they are
  low-frequency legal/policy content — the footer is their home). The
  `/pages` tree remains the browse surface. Recorded in ADR 0043, not a
  bug to "fix" later.
- **The `kw-l` hard exclusions hold (ADR 0015):** the footer links are
  element *content* only — new `footer.platform.*` keys in
  `EnValues` / `DeValues` / `FrValues`, full registry parity pinned by the
  existing test family. Attributes / JS strings stay hardcoded.

## Assumptions

- **Scope = two new pages + routes + footer column + admin section.** In:
  the `privacy` + `conduct` `en` bodies in `EnDefaultPages()`, their de/fr
  baselines in `DeDefaultPages()` / `FrDefaultPages()` (full translation,
  structure preserved — the `LS` U04 bar), the two `PageTranslation`
  baselines per page, the `/privacy` + `/conduct` routes in
  `StaticPagesController`, the footer "Platform" column (five `kw-l`
  links), the `/admin` shell "Platform pages" section (a list of the
  seeded pages with preview + edit links), and the test pins. **Out (→
  future lanes):** machine translation of anything (ADR 0005 C — deferred
  trust boundary), additional pages (an admin adds them under `system/`
  at runtime — that is the design; ADR 0040 §2), a "contact" page (the
  operator's `Community__SupportEmail` already renders on the product-story
  view and the home hero — the help body names it as the operator concern;
  a dedicated contact page is a future lane, not this one), a cookie
  policy (folded into the privacy body's "what this platform stores in
  your browser" section — the only cookie is the locale preference), and
  the `Milestones.cs` / README / `MilestonesTests` trio until the lane
  *ships* (U05 owns it; the `MilestonesTests` pin that **M4** is the
  single `StatusNext` stays intact — an `SP` row added as `StatusDone`
  does not disturb it).
- **The baseline quality bar is the `LS` U04 bar (ADR 0042 D2):**
  idiomatic German / French UI copy (the `du` / `tu` register held,
  sentence case, no word-for-word calques), structure preserved exactly
  (heading, intro, the bullets, the closing line), placeholders token-for-
  token. The in-app editor (GlobalAdmin ∪ Translator, ADR 0021) + the
  `/pages` tree are the review surface.
- **The `privacy` body is a platform-level statement, not legal advice.**
  It states what the platform *is* from `docs/SECURITY.md`: self-hosted,
  single-neighborhood; the operator is the data controller; what the
  platform stores (accounts, posts, groups, pages, media — the Marten docs
  + the media volume), audience enforcement as the privacy mechanism,
  audit-by-default, backups/migration/retirement are the operator's job,
  and the one browser cookie (locale preference). It deliberately does
  *not* fabricate operator-specific facts (retention periods, DPO
  contacts, sub-processors) — those are operator-decided and the body says
  so. The operator is expected to edit it in-app after first boot; that is
  the design, not a gap.
- **The `conduct` body is short and enforcement-neutral.** It states the
  expectation (a bounded neighborhood, treat neighbors how you'd want to be
  treated on your street), the lines (no harassment, no doxxing, no spam),
  and that enforcement is the operator's / moderators' judgment (the
  moderation lane exists — ADR 0003). Two paragraphs + a short list,
  not a policy document.
- **Route wiring is additive and floor-honest.** `StaticPagesController.Slugs`
  gains `"privacy"` + `"conduct"`; both routes are `Page(slug)` with the
  *404 floor* (no product-story fallback — that seam is `about`'s). The
  `system/{slug}` primary + bare-`{slug}` legacy fallback resolution
  (ADR 0040's re-parent story) is unchanged. The route-spoofing guard
  (`Slugs.Contains`) stays the single allow-list.
- **The `about` surface needs no content work.** The product-story view is
  already complete and three-language-ready (`LS` U01–U05 shipped the
  `about.*` keys + the de/fr baselines + the `kw-l` wrap). This lane only
  adds its **link** (footer + admin).

## The units

### U00 — Sign-off + ADR 0043  *(no code beyond the ADR)*

Write `docs/adr/0043-system-pages-shipped-and-discoverable.md` (Accepted)
locking: the **five-surface set** (the four `Page` docs `terms` / `help` /
`privacy` / `conduct` + the `about` **view**, and why `about` stays a view —
the `LS` U05 drift pin carried forward); the **route contract** (`/privacy`
+ `/conduct` join the hard-coded set, 404 floor, no fallback seam); the
**ownership semantics** (re-stating ADR 0042 D1 for the new pages — `en`
code-owned, de/fr seeded-once-then-community-owned); the
**discoverability decision** (unconditional footer "Platform" column for
all five, navbar deliberately *not* touched, the `/admin` shell affordance,
floor-honesty when a page is absent); the **content scope** (privacy =
platform-level statement from `SECURITY.md`, no fabricated operator facts;
conduct = short enforcement-neutral statement; the cookie notice folded
into privacy); and the **standing matrix** (ADR 0040 §2 unchanged — the new
pages are `Kind = System` and inherit it; no matrix change). Record it as
an **amendment to ADR 0005 §B** alongside the ADR 0042 note ("static-page
coverage" is now five surfaces, not three) with a cross-reference back.
**Exit:** ADR 0043 accepted; ADR 0005 carries the amendment note;
**no other file changed.**

### U01 — `en` bodies + routes (`EnDefaultPages` + `StaticPagesController`)

1. `EnDefaultPages()` gains two entries — `("privacy", "Privacy", …)` and
   `("conduct", "Code of conduct", …)` — the `en` bodies per the
   assumptions above (the full source text lives in the ADR 0043 text or
   this unit's handoff notes; the array is the single source, per the
   `LS` U04 pattern).
2. `StaticPagesController`: `Slugs` gains `"privacy"` + `"conduct"`; two
   `[HttpGet]` actions (`Privacy()` → `Page("privacy")`,
   `Conduct()` → `Page("conduct")`) with the same doc-comment style as
   `Terms()` / `Help()` (the 404 floor, no fallback seam — contrast
   `About()`'s `fallBackToProductStory`).
3. **`about` is NOT added** to `EnDefaultPages()` (the drift pin — the
   existing comment says so; leave it).
4. **Tests (Core):** the `PageServiceTests` / `LS_U04_SeederTests` pins
   that assert the *exact* seeded set update from two to **four** pages
   (`terms` / `help` / `privacy` / `conduct`), and a route-floor pin:
   `GET /privacy` + `GET /conduct` on a tree missing those slugs → 404
   (Web — this may move to U04's Web pins if the Core assembly is the
   cleaner home; decide at execution, but the pin *exists*).

**Exit:** `dotnet build` green; `dotnet exec …Kumunita.Core.Tests.dll`
green; `/privacy` + `/conduct` resolve the seeded docs on a fresh DB and
404 when absent. **No footer/admin change in this unit.**

### U02 — de/fr baselines (`DeDefaultPages` / `FrDefaultPages`)

Extend `DeDefaultPages()` and `FrDefaultPages()` with the `privacy` +
`conduct` entries — full translations of the U01 `en` bodies (structure
preserved, the ADR 0042 D2 register held: `du` / `tu`, sentence case,
placeholder token-for-token). The `SeedPageTranslationsAsync` loop is
generic over the baseline arrays, so **no seeder-loop change** is needed —
the new rows flow from the new array entries (verify at execution; if the
defensive slug-agreement `continue` needs the new slugs, it already keys
on the arrays, not a hard-coded list).

**Independent of U03** (Core vs. Web — either order). **Exit:** the Core
parity/seed tests (the U01 pins + the `LS` U04 completeness shape) extended:
`privacy` + `conduct` each carry a `de` and a `fr` `PageTranslation` on the
page's own `Id` (the `LS` U04 parentage pin), body non-empty, parity with
the `De/FrDefaultPages()` sources; `dotnet exec …Kumunita.Core.Tests.dll`
green. **No view change in this unit.**

### U03 — Discoverability: footer column + `/admin` section

1. **Registry:** `KnownTranslationKeys` gains `footer.platform.heading`
   ("Platform") and five link labels — `footer.platform.about` ("About"),
   `footer.platform.terms` ("Terms of use"), `footer.platform.help`
   ("Help"), `footer.platform.privacy` ("Privacy"),
   `footer.platform.conduct` ("Code of conduct") — in `EnValues`, and the
   same six keys in `DeValues` + `FrValues` (registry parity; the `LS`
   U01 parity family pins it).
2. **Footer:** `_Layout.cshtml` gains a "Platform" column — the five links
   (`/about`, `/terms`, `/help`, `/privacy`, `/conduct`) in
   `kw-l`-wrapped `<a>` elements, unconditionally rendered (the
   floor-honesty invariant: no existence probe). Column order: after
   "Community", before "The project" (the policy content sits between
   the community and the project). The existing "Good to know" column is
   **untouched** (its copy stays — it is a value statement, not
   navigation).
3. **Admin:** `Views/Admin/Index.cshtml` Platform section gains a
   "Platform pages" affordance — a short list (About → the product-story
   view; Terms / Help / Privacy / Conduct → their routes), each row with
   a *preview* link (the route) and an *edit* link. **Edit-link
   mechanics:** the edit route is id-based
   (`/pages/{id}/edit`), so the controller (`AdminController`) resolves
   the seeded slugs to page ids at render time (a
   `GetByPathAsync("system/{slug}")` per slug, absence-tolerant — a
   slug's row renders preview-only if its page is somehow missing;
   `/about` has no edit link — it is a view, not a page). The resolved
   list rides in `AdminIndexViewModel` (additive field; the view-model is
   Web-only, no Core seam).
4. **Tests (Web):** a layout-pin (the footer renders the five links in
   order, localized per the `en`/`de`/`fr` catalog — the
   `PublicLocaleAndAboutTests` shape) and an admin-shell pin (the section
   lists the five rows with the right preview hrefs, and four edit hrefs
   — `/about` preview-only).

**Exit:** `dotnet build` green; both test assemblies green via
`dotnet exec`; a cookie-less `GET /` shows the footer column with all five
links. **No Core change in this unit.**

### U04 — Web surface pins + fresh-boot manual pass

- **Web tests (the full pin set):** `/privacy` + `/conduct` render the
  seeded `en` body for a cookie-less visitor and the `de` / `fr` body
  under a `de` / `fr` preference (the ADR 0027 `td-variant` chip-swap rows
  present, display not auto-switched); `/about` still renders the
  product-story view (the drift pin, regression-guarded); the 404 floor on
  `/privacy` for a tree missing the slug; the footer + admin pins from
  U03 hold against a *seeded* DB (the `LS` U06 first-boot catalog shape
  reused).
- **Fresh-boot manual pass** (the lane's real exit): `docker compose
  down -v`-equivalent wipe of the `mt` volume per OPS, fresh boot, and:
  (a) the First-boot log shows the page-seed line updated to name
  **four** Page docs and **eight** `PageTranslation` rows, exactly once;
  (b) cookie-less `GET /` renders English and the footer's Platform
  column shows all five links; (c) each of `/about`, `/terms`, `/help`,
  `/privacy`, `/conduct` renders fully in `en`, and with a `de` / `fr`
  preference the page bodies are in that language (the chip-swap
  variants present); (d) as GlobalAdmin, `/admin` shows the Platform
  pages section — all five rows, four with edit links, each edit landing
  on the right page in the composer; (e) editing one `de` privacy string
  in-app persists and survives a warm reboot (`docker stop` + `docker
  start`, **0 "First boot" lines**, the edit intact — the ADR 0042 D1
  no-resseed invariant on the new content); (f) `docker compose up -d
  --build` again is a warm boot — **no reseed of de/fr**, the `en` bodies
  refresh in place (the code-wins branch) if the code changed. Record the
  pass in the handoff notes below.
- **Exit:** both test assemblies green (the `dotnet exec …dll` path, per
  AGENTS.md — **not** `dotnet test` / Test Explorer on this machine); the
  manual pass recorded.

### U05 — Docs sync (same commit as the pass)

- `README.md`: the multilingual / platform-features note gains the
  five-surface set (privacy + conduct named), and the Roadmap gains the
  lane's one-line entry.
- `Milestones.cs`: the `SP` row → `StatusDone` (after the `LS` row).
- `MilestonesTests.cs`: `"SP"` inserted after `"LS"` in the ordered-ids
  list + the shipped-done list; `M4_Is_The_Single_InProgress_Milestone`
  stays green (SP is `StatusDone`).
- `docs/ARCHITECTURE.md`: the static-page routes section (if it names the
  three routes) gains the two new ones + the footer-column note.
- **Exit:** the trio + ARCHITECTURE note consistent; both test assemblies
  green; the ADR 0043 amendment note in ADR 0005 is final (U00 left it as
  a forward pointer if it had to).

## Handoff notes

(One appended `## U#` section per unit, never rewritten — the scratch tier.)
