# U3 — Multilingual: `ILocalizationService` admin seam + `LanguageCompleteness`

**Milestone:** multilingual (`ML`, ADR 0005) · **Read first (5 min):**
`docs/design/multilingual-design.md` §Pinned contract §3 (the **exact** C# of
`ILocalizationService`, `LanguageCompleteness`, and the audit-row shape table —
this unit matches the interface and record verbatim) +
`docs/plans-milestones/done/plan-multilingual.md` (master register — invariants
M·1–M·9, FACES M1–M13, unit-series rules) + the **U2** section of
`docs/plans-milestones/done/multilingual-handoff-notes.md`. **No
repo-wide scan.**

## Goal
Ship the admin-management seam — `ILocalizationService` (interface),
`LanguageCompleteness` (record), and `LocalizationService` (implementation) —
in `src/Kumunita.Core/Localization/`. Every mutating method appends exactly
one `AccessAudit` row (`Via = Admin`, `Outcome = Allow`) in the same
transaction as the domain write (M·6, the `UserInfoService` admin-action
idiom). `RemoveLanguageAsync` on the current default **throws**
`InvalidOperationException` before any write (M·7, fail-closed — no audit
row). **Code unit — build must be green.**

## Entry reads (the minimal set — read in this order)
1. `docs/design/multilingual-design.md` §Pinned contract §3 only — the exact
   interface + record + audit-row shape table (verbatim contract).
2. `docs/plans-milestones/done/multilingual-handoff-notes.md` — the U2
   section (what U2 shipped: `ITranslationProvider` + `TranslationProvider` —
   the provider and the service are **independent**, no coupling).
3. `src/Kumunita.Core/UserInfo/UserInfoService.cs` (the audit-row write
   idiom: one session, one `SaveChangesAsync`, `AccessAudit` shape with
   `Via`/`Outcome`/`Action`/`TargetKind`/`TargetId` — the pattern to
   mirror).
4. `src/Kumunita.Core/Authorization/AccessAudit.cs` +
   `src/Kumunita.Core/Authorization/Decision.cs` (the `AccessAudit` POCO,
   `AccessVia` enum, `AccessOutcome` enum — the exact member names and
   enum values).
5. `src/Kumunita.Core/Localization/` (U1's two content docs + U2's
   `ITranslationProvider` / `TranslationProvider` — match doc-comment style;
   the `LanguageCatalog` / `LocaleSettings` seed surface to read from).

## Deliverables (3 files)
1. **New:** `src/Kumunita.Core/Localization/ILocalizationService.cs` —
   **verbatim** from the design doc §Pinned contract §3: 12 methods across
   three groups (catalog / UI strings / static pages) + the completeness
   read. Every mutating method's doc-comment pins the exact `Action` string,
   `TargetKind`, and `TargetId`. `RemoveLanguageAsync` carries the
   `<exception>` tag for M·7's fail-closed throw.
2. **New:** `src/Kumunita.Core/Localization/LanguageCompleteness.cs` —
   **verbatim** from the design doc §Pinned contract §3: a `sealed record`
   with 5 positional parameters (`LanguageCode`, `PresentKeys`,
   `MissingKeys`, `PresentPageSlugs`, `MissingPageSlugs`). The doc-comment
   anchors M·9 (the `en` universe) and M·12 FACES.
3. **New:** `src/Kumunita.Core/Localization/LocalizationService.cs` — the
   implementation (takes `Marten.IDocumentStore`):
   - **Read methods** (`ListLanguagesAsync`, `GetTranslationAsync`,
     `GetPageAsync`, `GetCompletenessAsync`): `QuerySession`, live rows
     (M·4 — no projection, no cache). `GetCompletenessAsync` computes
     present/missing against the `en` universe (M·9).
   - **Catalog mutations** (`AddLanguageAsync`, `SetLanguageEnabledAsync`,
     `ReorderLanguagesAsync`, `RemoveLanguageAsync`,
     `SetDefaultLanguageAsync`): one `OpenSession` + one
     `SaveChangesAsync`; each stores exactly one `AccessAudit` row
     (`Via = Admin`, `Outcome = Allow`, `EffectivePrincipalId = actorId`).
     `RemoveLanguageAsync` checks the default **in the same transaction**
     before any write — throws `InvalidOperationException` (M·7), no audit
     row. Content rows (`TranslationResource` / `LocalizedPage`) are
     **retained** (M·7).
   - **Translation / page mutations** (`UpsertTranslationAsync`,
     `UpsertPageAsync`): upsert by business key (pair idiom), one
     `AccessAudit` row, one `SaveChangesAsync`. `UpsertPageAsync` sets
     `Updated` to server time.

**Out of this unit (unit-series rule 1):** no `LocaleCookie` (U4), no
`LanguagesController` (U5), no settings/static-page routes (U6), no
`DependencyInjection.cs` registration (U4 or the host), no tests (U7),
no README / ADR edits (U9).

## Exit
- `dotnet build Kumunita.slnx -c Debug` **green**.
- The U3 handoff-note section appended to
  `docs/plans-milestones/done/multilingual-handoff-notes.md` **before**
  the folder move.
- This unit plan file moved to `docs/plans-milestones/done/multilingual-u03-plan.md`.
