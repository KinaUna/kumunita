# ADR 0042 — Bundled initial language pack (de / fr ship on first boot)

Status: Accepted
Date: 2026-09-18
Amends: **0005 §B** (the "every other language is community-provided"
sentence — now "de / fr additionally ship as bundled initial values,
editable in-app; `en` remains the only code-owned language"; the amendment
note lives in ADR 0005, cross-referenced below). Builds on the frozen base
of **0015** (the `<kw-l>` TagHelper, the `KnownTranslationKeys` registry,
the provider floor, the hard exclusions), **0039 / 0040** (the system-page
tree + the `PageTranslation` lane), and **0021** (the GlobalAdmin ∪
Translator edit standing the baselines hand off to). No amendment to 0015,
0039, 0040, or 0021 — this ADR is additive on their decisions exactly as
written.

This ADR is the **sign-off gate** of the `LS` (Languages Seeded) lane — the
lane plan is `plans-milestones/done/languages-seeded/plan-languages-seeded.md`.
Every later `LS` unit (U01–U06) codes against the *locked* text here;
changing a decision below requires an amendment, not a unit-level override.

## Context

A fresh instance boots with exactly **one** enabled language: the seeder
writes the `en` catalog row, `LocaleSettings.DefaultLanguageCode = "en"`,
the `en` `TranslationResource` rows for every registered key (the
`en` floor, ADR 0015 D2), and the `en` bodies of the seeded `terms` /
`help` system pages. Every other language is community-provided (ADR 0005
§B) — an admin adds it under `/admin/languages` and a GlobalAdmin /
Translator fills the ~370 UI strings by hand. The result is that the
platform's headline feature (multilingual UI, ADR 0005 A) is **invisible
until somebody sits down and types a few hundred strings**. A first-boot
instance should *demonstrate* the feature end-to-end: three languages in
the picker, a full UI in any of them, and the completeness view showing
real work done rather than three empty columns.

This lane ships a **curated initial pack** — German and French — inside
the image: the pristine-boot seeder materializes the `de` / `fr` catalog
rows, the full UI-string baselines, and the `de` / `fr` bodies of the
seeded system pages. The `about` product surface is the one in-scope
surface that is *not* yet behind `<kw-l>` (it is an ADR 0015 deliberate
exclusion: "the product-story landing — not platform UI yet"); this lane
pulls it in *through the existing mechanism* (a registry entry + a `<kw-l>`
wrap per ADR 0015's "Cost" paragraph) rather than inventing a new one.

**Out of scope (future lanes, not this ADR's decisions):** machine
translation of anything (ADR 0005 C — a deferred trust boundary, unchanged);
additional languages (an admin adds them in-app — that is the design); a
warm-reseed / upgrade-push mechanism for the baselines (the new-key
asymmetry in the Decision below *is* the decision about that); and the
`Milestones.cs` / README / `MilestonesTests` trio (lands when the lane
ships, `LS` as `StatusDone` beside M4's unchanged `StatusNext` pin).

## Decision

### D1 — Ownership semantics: `en` code-owned forever; `de` / `fr`
seeded once, then community-owned

- **`en` is code-owned, forever.** `KnownTranslationKeys.EnValues` is the
  single source of truth for the English strings. The seeder upserts `en`
  rows with **code-wins** semantics (ADR 0015 D2, unchanged), and the
  provider floor resolves any registered key to the registry's `en` text
  (ADR 0015 D1, unchanged). Nothing in this lane changes that path.
- **`de` / `fr` are seeded-once, then community-owned.** The baselines
  ship with the code as **initial values**. The seeder writes them **only
  on a pristine DB** (the `DbBootstrap.IsPristineAsync` gate — the seeder
  never runs on a warm instance; ADR 0015 "Cost"). After first boot, the
  in-app editor (GlobalAdmin ∪ Translator, ADR 0021) is the **only** write
  path for `de` / `fr` rows, and **an admin edit is never overwritten** —
  the seeder's `de` / `fr` upserts are first-boot-only *by construction*,
  not by an extra guard.
- **The new-key asymmetry (recorded, deliberate):** a key added to the
  registry in a later code release renders its `en` text on *every*
  instance (the provider floor — ADR 0015's upgrade path, unchanged). But a
  `de` / `fr` baseline for that key does **not** appear in an existing
  instance's database until a GlobalAdmin / Translator types it in-app
  (the M·12 completeness view shows it missing). This is **asymmetric with
  `en` on purpose**: `en` is the floor (code-owned, D1), `de` / `fr` are
  community data (D1). It is the *consequence* of "seeded once, then
  community-owned" meeting "no warm-reseed mechanism exists" — **a
  recorded decision, not a bug to fix**. The lane deliberately does not add
  a reseed path; the completeness view is the review surface.
  > **Narrow exception recorded 2026-09-28 (ADR 0047 D2):** the *four
  > canonical system pages'* `de` / `fr` / `da` `PageTranslation` rows
  > **are** backfilled on a warm boot (create-if-missing, idempotent, never
  > overwriting an admin edit or the `en` body) — the resident-visible seam
  > ADR 0043 / 0047 D1 makes user-visible in the effective language. This is
  > **deliberately scoped to those four pages' page-translation rows**: the
  > **UI-string** baselines (`TranslationResource` rows) are still **not**
  > warm-reseeded, and the new-key asymmetry above stands unchanged for
  > them. The "no warm-reseed mechanism" decision is held for everything
  > else; the four system pages are the only narrow, recorded exception.
### D2 — Register and style: German `du`, French `tu`, held per language

The baselines are a **careful first pass, editable later** — the
in-app editor + the M·12 completeness view are the review surface, and the
ADR 0021 standing (GlobalAdmin ∪ Translator) is the review authority. The
quality bar locked here:

- **Idiomatic, not calqued.** Word-for-word calques from the English are
  failures even when grammatical. The bar is idiomatic UI copy in each
  language.
- **The familiar register, chosen once per language and held everywhere:**
  German **`du`**, French **`tu`** — the neighborhood register (a
  resident-to-resident platform, not an institutional one). The choice is
  made **once per language** and held across the whole baseline; mixing
  `du` / `Sie` (or `tu` / `vous`) within a language is a defect.
- **Language-native casing and punctuation:** German sentence case on
  labels and no trailing period on button labels; `ß` allowed; French
  accents and typography per French convention.
- **Token-for-token data preservation:** any `{0}`-style placeholder or
  inlined data value must survive translation **exactly** — the English
  and each baseline agree on every non-translatable token.
- The `terms` / `help` baselines are **full translations of the current
  `EnDefaultPages()` bodies** — the exact Markdown, structure preserved,
  not a rewrite (a paraphrase would drift from the single source).

### D3 — `about` is wired through the registry, not special-cased

The `about` surface joins the translatable surface **through the existing
mechanism only**: its strings are `EnValues` entries (registered, exact
current English copy — the ADR 0015 invariant that registry values are the
exact rendered strings the view emits), and the view is wrapped in `<kw-l>`
against those keys (ADR 0015 D1). No new TagHelper, no new provider path,
no new document type, no new `AccessAction` / `AccessVia`, no authorization
change, no schema change, no migration. The ADR 0015 **hard exclusions
hold unchanged**: the TagHelper emits element *content* only — HTML
attributes (`placeholder` / `aria-label` / `title`), JS strings, and
C#-built markup fragments stay hardcoded.

### D4 — The default language stays `en`

`LocaleSettings.DefaultLanguageCode` remains `"en"`; the source language
remains `"en"` (ADR 0005 §B); the picker's first entry remains English
(sort 0). This lane is about *available* languages at first boot, **not**
about switching the default. The catalog seeded on first boot is exactly:
`en` (sort 0) / `de` (sort 1) / `fr` (sort 2), all enabled, default `en`.

### D5 — The `about.*` key list (the stable contract U01–U05 code against)

The keys below are the **closed contract** for the `about` surface: U01
registers exactly these in `EnValues` with the exact current English copy
of `Views/StaticPages/About.cshtml`, U02 / U03 translate exactly these,
and U05 wraps the view against exactly these names. The list is derived
from the view as of 2026-09-18; the in-scope copy is the **visible
English strings only** — the `TODO(counts)` placeholder values (and the
band's own TODO) are out of scope, the `@Model.CommunityName` hero heading
is a data value, the `@Model.SupportEmail`-containing contact strings are
C#-built fragments (D3 exclusions), and the `RepositoryInfo.Links` labels
are data.

| Key | Current English (the `EnValues` value) | View location |
| --- | --- | --- |
| `about.eyebrow` | `Private by default` | hero eyebrow |
| `about.lead` | `One home for everything your neighborhood does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours.` | hero lead |
| `about.cta_feed` | `See the feed` | hero primary CTA |
| `about.cta_notes` | `Read the pinned notes` | hero ghost CTA |
| `about.features.one.title` | `One feed for the street` | feature card 1 |
| `about.features.one.body` | `Posts and threads from your blocks and lanes, in one quiet place — no algorithm, no noise.` | feature card 1 |
| `about.features.groups.title` | `Groups that fit` | feature card 2 |
| `about.features.groups.body` | `Garden swap, book club, street watch — a group for whatever the neighbourhood already does.` | feature card 2 |
| `about.features.pinned.title` | `Pinned where it matters` | feature card 3 |
| `about.features.pinned.body` | `Water cuts, roadworks, the new speed bumps — notes that stay put instead of scrolling away.` | feature card 3 |
| `about.stats.neighbors` | `neighbors on board` | stats band, label 1 |
| `about.stats.groups` | `groups & communities` | stats band, label 2 |
| `about.stats.posts` | `posts & threads this month` | stats band, label 3 |
| `about.stats.pinned` | `pinned notes out now` | stats band, label 4 |
| `about.project.eyebrow` | `Open source` | project section eyebrow |
| `about.project.heading` | `The code, the decisions, the design docs` | project section heading |
| `about.project.lead` | `If you're curious how it works — or if you're about to host it for your neighbourhood — everything is public.` | project section lead |

**Explicitly not keys (recorded so they are not read as drift):** the four
stats-band *values* (placeholder `TODO(counts)`, the band's own TODO stays);
the hero `<h1>` (`@Model.CommunityName` — a data value); the contact-CTA
strings (they embed `@Model.SupportEmail` — C#-built fragments, D3); the
repository link labels (`RepositoryInfo.Links` — data); all SVG / class
markup (hardcoded by construction).

### D6 — Scope

**In:** the `about.*` keys (D5) registered in `EnValues`; `DeValues` /
`FrValues` baselines at **full registry parity** (every key in
`EnValues`, no dead weight — the ADR 0015 honesty invariant extended to
the two new dictionaries); the `de` / `fr` catalog rows in the seeder
(D4); the `de` / `fr` `TranslationResource` rows in the seeder (D1); the
`de` / `fr` bodies of the seeded `terms` / `help` system pages (D2, via
`PageTranslation` rows in the ADR 0039/0040 PG U06 lane shape, attached to
the **terms / help page's own `Id`** — the read path
`PageController` → `IPageService.GetTranslationsAsync(page.Id)` queries by
the page's own id, so the `system` root container's id is *not* a valid
parent; see the `U04` handoff in the lane plan); the `about` view wrapped
in `<kw-l>` (D3); and
the test pins (first-boot state, completeness 100%, provider resolution,
per-string fallback, warm-boot no-op, Web surface).

**Out:** machine translation (ADR 0005 C); additional languages (in-app by
an admin — the design); a warm-reseed / upgrade-push mechanism (D1
asymmetry); the `TODO(counts)` stats values; the `Milestones.cs` / README /
`MilestonesTests` trio until the lane ships.

## Consequences

- A **freshly deployed instance demonstrates the multilingual feature
  end-to-end**: three languages in the public picker, the full UI (nav,
  footer, Home, settings, `about`, `/terms`, `/help`) in any of them, and
  the M·12 completeness view showing `en` / `de` / `fr` each at 100% from
  first boot — instead of one language and two empty columns.
- **The `en` path is bit-for-bit unchanged**: same code-wins upsert, same
  provider floor, same registry-as-source-of-truth. The registry honesty
  invariant now holds for three dictionaries: `EnValues` values are the
  rendered strings; `DeValues` / `FrValues` keys equal `AllKeys` exactly.
- **Community ownership is preserved from day one.** The baselines are
  *initial values*, not *owned strings*: the first admin edit of any
  `de` / `fr` row supersedes the shipped text, and no later deploy can
  revert it (D1). The in-app editor and the completeness view are the
  standing review surface (ADR 0021).
- **Cost — first drafts are first drafts.** The baselines ship at the
  D2 bar (idiomatic, register-held, token-preserved), not at a published
  translation's polish. Accepted: the D1 ownership semantics make the
  draft status safe — it is editable, it is not overwritable, and it is
  visibly incomplete where it is incomplete.
- **Cost — the new-key asymmetry (D1) is a permanent property, not a
  transition.** Every future code release that adds a key is automatically
  complete in `en` and incomplete in `de` / `fr` on existing instances
  until a human types the translation. Recorded as a feature of the
  code-owned / community-owned split, not a gap to close.
- **The `about` surface exits the ADR 0015 deliberate-exclusions list**
  with no new mechanism — the registry + `<kw-l>` are the only tools used,
  and the ADR 0015 hard exclusions (attributes, JS, C#-built fragments)
  remain unchanged. The ADR 0015 exclusions list should be read with this
  ADR's D5 key list as the new scope of the `about` view.

## Revisit when

- A **warm-reseed or upgrade-push mechanism** for community-owned languages
  is wanted (D1 asymmetry becomes a complaint) — decide it as a *new*
  seeder path with its own ownership review, not as an `LS` extension.
- **MT is introduced** (ADR 0005 C): the baselines are the natural *seed
  material* for an opt-in, clearly-labeled suggestion surface — but that
  is a trust-boundary decision with its own ADR and SECURITY.md review.
- The community **adds a fourth language in-app** and wants *its* baseline
  bundled in a future image: re-run the D2 bar for that language and
  record the choice here by amendment (the register rule is per-language,
  D2, so a new language picks its own).
- **The `TODO(counts)` stats band lands** real counts: the band's *values*
  remain data (D5), but if the band is replaced or the labels reworded,
  the D5 list is the contract to amend.
