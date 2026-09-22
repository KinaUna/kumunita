# U2 — Multilingual: the `ITranslationProvider` read seam + `TranslationProvider` impl

**Milestone:** multilingual (`ML`, ADR 0005) · **Read first (5 min):**
`docs/design/multilingual-design.md` §Pinned contract §2 (the **exact** C# of
`ITranslationProvider` — this unit matches it verbatim) +
`docs/plans-milestones/done/plan-multilingual.md` (master register — invariants M·1–M·9,
FACES M1–M13, unit-series rules) + the **U1** section of
`docs/plans-milestones/done/multilingual-handoff-notes.md` (the two content
docs + `M1DocTypes` indexes are already in place — the provider reads them).
**No repo-wide scan.**

## Goal
Ship the per-request read seam — the `ITranslationProvider` interface and the
`TranslationProvider` implementation — in `src/Kumunita.Core/Localization/`.
**Code unit — build must be green.**

## Entry reads (the minimal set — read in this order)
1. `docs/design/multilingual-design.md` §Pinned contract §2 only — the exact
   interface signature + doc-comments (verbatim contract).
2. `docs/plans-milestones/done/multilingual-handoff-notes.md` — the U1
   section (the two content docs + `M1DocTypes` indexes are shipped; the `en`
   floor + `LocaleSettings` seed are already in place — **do not re-do any of
   that**).
3. `src/Kumunita.Core/Localization/TranslationResource.cs` (the UI-string doc —
   the provider reads `Key` / `LanguageCode` / `Text`).
4. `src/Kumunita.Core/Localization/LocalizedPage.cs` (the static-page doc — the
   provider reads `Slug` / `LanguageCode` / `Title` / `Body` / `Updated`).
5. `src/Kumunita.Core/Localization/LanguageCatalog.cs` (the catalog +
   `LocaleSettings` — the provider resolves effective language against these).
6. `src/Kumunita.Core/Identity/EmailDeadLetterCounter.cs` (the
   store-composing read-service idiom — `IDocumentStore` + `QuerySession`).
7. `src/Kumunita.Core/M1DocTypes.cs` (confirm the two content docs are registered
   with unique indexes — the provider's queries depend on the pair idiom).

## Deliverables (2 files)
1. **New:** `src/Kumunita.Core/Localization/ITranslationProvider.cs` —
   **verbatim** from the design doc §Pinned contract §2 (4 methods:
   `ResolveEffectiveLanguageAsync`, `GetAsync`, `GetManyAsync`, `GetPageAsync`;
   the doc-comments anchoring M·1/M·2/M·3/M·8/M·9).
2. **New:** `src/Kumunita.Core/Localization/TranslationProvider.cs` — the
   implementation:
   - Constructor takes `Marten.IDocumentStore`.
   - **HTTP-free** (M·8): no ASP.NET/`System.Web`/`HttpRequest` types — the
     preferred language arrives as a plain `string?`.
   - Reads **only** the two content docs + the M1 seed (`LanguageCatalog` /
     `LocaleSettings`) — **never** a `Post` / `PostReply` / `Group` body (M·3).
   - `ResolveEffectiveLanguageAsync`: preferred (if enabled in `LanguageCatalog`)
     → `LocaleSettings.DefaultLanguageCode` (if enabled) → `"en"` (M·1, M·9 —
     never null/empty).
   - `GetAsync`: per-string fallback chain — `(key, effective)` → `(key,
     default)` → `(key, "en")` → **the key itself** (M·2, M·1, M·9 — a resident
     never sees a blank label).
   - `GetManyAsync`: **one** query for all keys; each key falls back
     independently (per-string, M·2). Returns `IReadOnlyDictionary<string,
     string>` mapping each requested key to its resolved text.
   - `GetPageAsync`: per-page fallback — `(slug, effective)` → `(slug, default)`
     → `(slug, "en")` → `null` (M·2 — `null` means the page truly does not exist
     in any language; the Web renders a 404).
   - Uses `IQuerySession` / `LightweightSession` against the store (the
     `EmailDeadLetterCounter` read idiom).

**Out of this unit (unit-series rule 1):** no `ILocalizationService` /
`LanguageCompleteness` (U3), no `LocaleCookie` (U4), no `LanguagesController`
(U5), no settings/static-page routes (U6), no DI registration in
`DependencyInjection.cs` (the provider is registered by U4 or the host — U2 only
ships the two files), no seeder changes, no Web changes, no tests (U7), no README
/ ADR edits (U9).

## Exit
- `dotnet build Kumunita.slnx -c Debug` **green**.
- The U2 handoff-note section appended to
  `docs/plans-milestones/done/multilingual-handoff-notes.md` **before**
  the folder move.
- This unit plan file moved to `docs/plans-milestones/done/multilingual-u02-plan.md`.
