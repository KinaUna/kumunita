# ADR 0045 — Danish (da) pre-seeded, disabled by default

Status: Accepted
Date: 2026-09-24
Amends: **0042** (the bundled initial pack — `de` / `fr` ship on first boot) —
adds `da` to the bundled baseline set, with the one behavioral difference that
it ships **disabled**. Builds on the frozen base of **0042 / 0043** (the
curated baseline + the four system pages), **0005 §B** (non-`en` languages are
community-provided; the bundled pack is the narrow, deliberate exception), and
**0015** (the `<kw-l>` TagHelper, the `KnownTranslationKeys` registry, the
provider floor). No amendment to 0015, 0042, or 0043 — this ADR is additive on
their decisions exactly as written.

## Context

ADR 0042 ships a curated initial pack — German (`de`) and French (`fr`) — on
first boot: their catalog rows (enabled), the full UI-string baselines, and the
`de` / `fr` bodies of the four seeded system pages (terms / help / privacy /
conduct). The platform thereby *demonstrates* its headline multilingual feature
from the first bootup (three languages in the picker, a full UI in any of them,
the completeness view showing real work).

Danish is requested the same way: add `da` to the pre-seeded set, **disabled by
default**, available in the supported-language catalog, with a complete
UI-string translation baseline and the four system-page baselines. The point is
that Danish is *ready the moment an admin enables it* — but it does not appear
in resident pickers at first boot, and the default language does not change.

This is the same "available at first boot, not the default" stance ADR 0042
D4 took for the default (`en` always stays the instance default), extended to
one more language. The one difference from `de` / `fr` is the
`LanguageCatalog.Enabled` flag: `da` is seeded `false`, not `true`.

## Decision

### D1 — `da` is a bundled baseline language, seeded once, then community-owned

- `da` joins `de` / `fr` in the **bundled baseline** set: the code carries a
  full `KnownTranslationKeys.DaValues` (key-for-key parity with `AllKeys`, the
  ADR 0015 honesty invariant) and `FirstBootSeeder.DaDefaultPages()` (the four
  system pages, the ADR 0043 D1 set). These are **initial values**.
- The seeder writes the `da` UI-string + page-baseline rows **only on a
  pristine DB** (the `DbBootstrap.IsPristineAsync` gate — the seeder never runs
  on a warm instance; ADR 0042 D1), with the same **create-if-missing**
  semantics: an admin edit is never overwritten. After first boot, the in-app
  editor (GlobalAdmin ∪ Translator, ADR 0021) is the only write path.
- `da` is a bundled code for the ADR 0044 read/seed seam:
  `LocalizationService.GetBundledBaselineAsync("da")` returns the baseline, and
  `AddLanguageAsync` / `RemoveLanguageAsync` seed/reset the `da` rows exactly as
  they do for `de` / `fr`. A non-bundled code still returns `null` (admin-
  authored, no code baseline).

### D2 — Register and style: Danish, the familiar `dig` register, held

- **Idiomatic, not calqued.** Word-for-word calques from the English are
  avoided; the Danish reads as native UI copy.
- **Register held** — the familiar, informal `dig`/`du` register is used
  throughout (matching the `du` / `tu` register ADR 0042 D2 held for `de` /
  `fr`). Sentence case, no trailing period on button labels.
- **Tokens preserved** — inlined data tokens are kept token-for-token with the
  `en` value (the `yyyy-MM-dd HH:mm` format sample, the `§6.4` reference, the
  `Allow` / `Deny` outcomes, the `Select all` on-screen label the `<kw-l>`
  TagHelper cannot reach, and the `rc.editor.*` glyph labels).
- These are a **careful first pass, editable later** — the in-app editor + the
  M·12 completeness view are the review surface, and the ADR 0021 standing is
  the review authority.

### D3 — `da` is seeded DISABLED; the default stays `en`

- `SeedLanguageCatalogAsync` writes a `da` catalog row
  (`Id = "da"`, `NativeName = "Dansk"`, `SortOrder = 3`) with **`Enabled =
  false`** — last in the selector, off by default.
- Because the `TranslationProvider` resolves a preference **only when the
  catalog row is enabled** (ADR 0015 M·1), a disabled `da` preference falls back
  to `en` — so `da` is present in the data and the supported-language catalog
  (the `/admin/languages` Index lists enabled + disabled rows with completeness)
  but is **not** surfaced to residents in the picker until an admin enables it.
- The instance default (`LocaleSettings.DefaultLanguageCode`) stays `en`
  (ADR 0042 D4). This lane is about making Danish *available*, not about
  switching the default.

### D4 — The new-key asymmetry extends to `da`

A key added to the registry in a later release renders its `en` text on every
instance (the provider floor, ADR 0015 D1). A `da` baseline for that key does
not appear in an existing instance's database until a GlobalAdmin / Translator
types it in-app (the completeness view shows it missing). This is the same
`en`-is-floor, others-are-community-data asymmetry ADR 0042 D1 recorded for
`de` / `fr`, extended to `da` — a recorded decision, not a bug.

## Consequences

- `KnownTranslationKeys` gains a `DaValues` dictionary (full `AllKeys` parity,
  no empty values — pinned by the parity tests).
- `FirstBootSeeder` gains `DaDefaultPages()`, a `da` catalog row (disabled,
  sort 3), and `da` in the baseline + page-translation seed loops.
- `LocalizationService.GetBundledBaselineAsync` gains a `case "da"`.
- Tests: the LS U04 seeder pins (catalog now `en/de/fr/da`, `da` disabled;
  completeness 100% for `de`/`fr`/`da`; the four pages each carry a `da`
  `PageTranslation`); the `DaValues` parity fact; the ADR 0044 parity theory
  gains `[InlineData("da", "Dansk")]`.
- A resident never sees Danish until an admin enables it; enabling is a single
  catalog flag flip (the `/admin/languages` Enable action) — the rows already
  exist, so it takes effect immediately.
