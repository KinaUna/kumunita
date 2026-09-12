# U1 — Multilingual: the two content documents + `M1DocTypes` indexes

**Milestone:** multilingual (`ML`, ADR 0005) · **Read first (5 min):**
`docs/design/multilingual-design.md` §Pinned contract §1 (the **exact** C# of both
POCOs and the registration snippet — this unit matches it verbatim) +
`docs/plans-milestones/plan-multilingual.md` (master register — invariants M·1–M·9,
FACES M1–M13, unit-series rules) + the **U0** section of
`docs/plans-milestones/in-progress/multilingual-handoff-notes.md`. **No repo-wide scan.**

## Goal
Ship the two ADR 0005 B content documents — `TranslationResource` (UI strings)
and `LocalizedPage` (static pages) — as Marten POCOs in
`src/Kumunita.Core/Localization/`, registered on the **existing** `M1DocTypes`
surface with the pair-idiom unique indexes. **Code unit — build must be green.**

## Entry reads (the minimal set — read in this order)
1. `docs/design/multilingual-design.md` §Pinned contract §1 only — the exact
   POCOs + the `M1DocTypes` registration snippet (verbatim contract).
2. `docs/plans-milestones/in-progress/multilingual-handoff-notes.md` — the U0
   section (what's already shipped: `LanguageCatalog` / `LocaleSettings`, the
   seeder, the already-bumped roadmap — **do not re-do any of that**).
3. `src/Kumunita.Core/Localization/LanguageCatalog.cs` (the existing seed
   surface in the same folder — match doc-comment style; M·2/M·4/M·7 anchors).
4. `src/Kumunita.Core/M1DocTypes.cs` (the registration surface — note the
   `GroupMembership` / `ComponentMembership` unique-index idiom and the
   Localization comment block that names this lane's arrival).
5. `src/Kumunita.Core/Authorization/` (the `GroupMembership` pair idiom is the
   pattern — only if the shape needs a cross-check; not expected).

## Deliverables (3 files)
1. **New:** `src/Kumunita.Core/Localization/TranslationResource.cs` —
   **verbatim** from the design doc §Pinned contract §1 (4 properties:
   surrogate `Id`, `Key`, `LanguageCode`, `Text`; `sealed`; the doc-comment
   anchoring M·1/M·2/M·4/M·9).
2. **New:** `src/Kumunita.Core/Localization/LocalizedPage.cs` — **verbatim**
   from the design doc §Pinned contract §1 (6 properties: `Id`, `Slug`,
   `LanguageCode`, `Title`, `Body`, `Updated`; `sealed`; the doc-comment
   anchoring M·2/M·4 and the M·7 retention note).
3. **Modified:** `src/Kumunita.Core/M1DocTypes.cs` — append, immediately after
   the existing `opts.Schema.For<LanguageCatalog>(); opts.Schema.For<LocaleSettings>();`
   block, the two registrations with the business-key unique indexes:
   - `opts.Schema.For<TranslationResource>().UniqueIndex(t => t.Key, t => t.LanguageCode);`
   - `opts.Schema.For<LocalizedPage>().UniqueIndex(p => p.Slug, p => p.LanguageCode);`
   — with a comment naming the pair idiom (one text per key per language / one
   page per slug per language) and M·7's retention (no delete here — retention
   is the service's job in U3). Update the Localization comment block in the
   same file so it no longer says the surface "lands with M6" — it lands **now**
   (`ML`, this lane).

**Out of this unit (unit-series rule 1):** no `ITranslationProvider` (U2), no
`ILocalizationService` / `LanguageCompleteness` (U3), no `LocaleCookie` (U4), no
seeder changes (the `en` row + default are already shipped — M1), no Web
changes, no tests (U7), no README / ADR edits (U9).

## Exit
- `dotnet build Kumunita.slnx -c Debug` **green**.
- The U1 handoff-note section appended to
  `docs/plans-milestones/in-progress/multilingual-handoff-notes.md` **before**
  the folder move.
- This unit plan file moved to `docs/plans-milestones/done/multilingual-u01-plan.md`.
