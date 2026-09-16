# U7 — the 19 pinned seam tests (`LocalizationServiceTests.cs`)

> **ML lane · U7 (of 9).** Sealed unit — sized for a ~64K-context fresh agent,
> one at a time. The **primary** reference is the design doc
> `docs/design/multilingual-design.md` §Pinned seam tests (19 exact names) +
> §Pinned contract (the exact C# of every seam U1–U6 shipped) + the 13 FACES
> rows. **Secondary** is the unit register
> `docs/plans-milestones/done/plan-multilingual.md` (this unit's row). **Scratch**
> is `docs/plans-milestones/done/multilingual-handoff-notes.md` (read
> the **U6** section — it confirms the Core seams U7's tests consume are the
> final U1–U6 state: two content docs, two interfaces + `LanguageCompleteness`,
> both registered in the DI graph).

## Goal

Ship U7's closed set — **one new test file**
`tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` — carrying the **19
pinned `[Fact]` names** verbatim from the design doc §Pinned seam tests table
(13 FACES M1–M13 + 6 audit-row-shape). Each test drives the **shipped** Core
seams (`TranslationProvider`, `LocalizationService`) over a fresh scratch
Postgres database (the `PostgresFixture` harness — the same template
`UserInfoServiceTests` uses). No Core or Web code changes; no new seams; the
design doc's 19 names are the **whole** and the **part-vs-whole** gate is U8's.

## Entry reads (minimal)

1. `docs/design/multilingual-design.md` §Pinned seam tests (19 names) +
   §Pinned contract (the exact seam C# U1–U6 shipped) + §Acceptance gate
   (the runner quirk — `dotnet exec` on the built assembly, **not**
   `dotnet test`). *(Authoritative.)*
2. `tests/Kumunita.Core.Tests/UserInfoServiceTests.cs` — the `BootStoreAsync`
   harness (fresh scratch DB, `KumunitaFeature` + `AuthorizationFeature` +
   `M1DocTypes.Configure`, `ApplyAllConfiguredChangesToDatabaseAsync`) and
   the audit-row-assertion idiom (`session.Query<AccessAudit>()` + the pinned
   `Via`/`Outcome`/`TargetKind`/`Action`/`TargetId` shape).
3. `src/Kumunita.Core/Localization/ITranslationProvider.cs` +
   `src/Kumunita.Core/Localization/TranslationProvider.cs` — U2's shipped read
   seam (4 methods; per-string / per-page fallback; the key-itself floor; the
   `null`-when-truly-absent page floor).
4. `src/Kumunita.Core/Localization/ILocalizationService.cs` +
   `src/Kumunita.Core/Localization/LocalizationService.cs` — U3's shipped
   admin seam (12 methods; the M·7 fail-closed throw on
   `RemoveLanguageAsync`; the row-retention on `LocalizedPage`; the audit-row
   shape: `Action` / `TargetKind` / `TargetId` / `Via = Admin` /
   `Outcome = Allow`).
5. `src/Kumunita.Core/Localization/LanguageCatalog.cs` — the two M1 seed
   documents (`LanguageCatalog` with `Id = BCP-47 code`, `LocaleSettings`
   with `Id = "singleton"`, `DefaultLanguageCode`) and the shipped `en` row
   shape the seeder writes (U1's handoff note confirms: `en` row enabled,
   `SortOrder = 0`, default `en` — the M1 seed, already shipped).

## Deliverables (closed set — 1 file)

| # | File | What ships |
|---|------|-----------|
| 1 | **New** `tests/Kumunita.Core.Tests/LocalizationServiceTests.cs` | The **19 pinned `[Fact]` names** verbatim. Class `public class LocalizationServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>`. A **private** `BootStoreAsync()` (the `PostServiceTests` template: fresh scratch DB → `DocumentStore.For` with `KumunitaFeature` + `AuthorizationFeature` + `M1DocTypes.Configure` **+ `M3DocTypes.Configure`** — M6's `Post` plant needs the M3 surface — → `ApplyAllConfiguredChangesToDatabaseAsync`). A **private** `Plant(store, document)` helper (the `PostServiceTests` idiom — a plain write-session store, for test-fixture seeding that is not a service write seam). A **private** `SeedM1RowAsync(store)` helper — mirrors `FirstBootSeeder.SeedLanguageCatalogAsync` exactly (upserts the `en` catalog row + the `LocaleSettings` singleton) so every test that needs the M·9 floor has it without re-running the full host seeder. **19 `[Fact]`s** — the 13 FACES tests (one per M1–M13) + the 6 audit-row-shape tests, named exactly per the design doc table. Each test asserts the pinned outcome (the FACES row) and, where applicable, the audit-row shape (`Via = Admin`, `Outcome = Allow`, `Action`, `TargetKind`, `TargetId` per method). No new Core or Web code; no new seams; the file is the **whole** (part-vs-whole gate — U8 runs it together with the inherited M1/M2/M3/M3b/media/group-posts anchors). |

### The 19 names (verbatim — a rename is a drift event)

The 13 FACES:

1. `M1_PreferenceCookie_ResolvesPolishUIString` — seed `en` + `pl` (enabled),
   `en` string for `nav.home`; a `pl` string for `nav.home`; assert
   `provider.GetAsync("nav.home", "pl")` == the `pl` text; and
   `provider.GetAsync("nav.home", null)` == the `en` text. (M·1, M·2.)
2. `M2_MissingStringInPreferredFallsBackPerString` — seed `en` + `pl`; only
   `pl` for `a`, only `en` for `b`; assert `provider.GetAsync("a", "pl")` ==
   the `pl` text for `a`; `provider.GetAsync("b", "pl")` == the `en` text for
   `b` — *per-string* fallback, not a view-flip. (M·2.)
3. `M3_NoPreference_UsesInstanceDefault` — seed `en` + `pl` (both enabled);
   set default to `pl` (`LocaleSettings` singleton); seed `pl` string for
   `nav.home`; assert `provider.GetAsync("nav.home", null)` == the `pl`
   text. (M·1.)
4. `M4_FreshInstance_DefaultIsEnglish` — seed the M1 row only (the `en` catalog
   row + the `en` default); seed an `en` string for `nav.home`; assert
   `provider.GetAsync("nav.home", null)` == the `en` text; and
   `provider.ResolveEffectiveLanguageAsync(null)` == `"en"`. (M·1, M·9 — the
   `en` floor.)
5. `M5_StaticPageLocalizesPerPage` — seed `en` + `pl`; a `terms` page in `en`;
   **no** `terms` page in `pl` (per-page fallback path); a `help` page in
   `pl` (preferred wins); assert
   `provider.GetPageAsync("terms", "pl")` returns the `en` `terms` page
   (per-page fallback), and `provider.GetPageAsync("help", "pl")` returns the
   `pl` `help` page. (M·2, M·7.)
6. `M6_UgcRendersAsAuthored_NotTranslated` — seed `en` + `pl`; a `Post` with
   an `en` `Body`; a `TranslationResource` for `nav.home` in `pl`; a
   `LocalizedPage` for `terms` in `pl`; assert the `Post`'s `Body` is the
   original `en` text (the provider **never** reads a `Post` body — M·3); the
   provider's `nav.home` resolves to the `pl` text; the provider's `terms`
   resolves to the `pl` page. The test is a **negative pin**: the provider's
   return values are *exactly* the platform-text rows it was given, and the
   `Post`'s `Body` is untouched. (M·3.)
7. `M7_PreferenceChange_TakesEffectNextRequest` — seed `en` + `pl`; `en`
   string for `nav.home`, **no** `pl` for `nav.home`; a `pl` string for
   `feed.reply`; (a) call `provider.GetAsync("nav.home", "en")` → `en` text;
   (b) call `provider.GetAsync("nav.home", "pl")` → key-itself floor (`pl`
   has no row for `nav.home` → falls back to `en` text — **not** the floor);
   (c) call `provider.GetAsync("feed.reply", "pl")` → the `pl` text. (M·1, M·5
   — the cookie → provider handoff is the Web layer's; this test pins the
   provider's per-preference resolution.)
8. `M8_PreferenceAtRemovedLanguage_FallsBackToDefault` — seed `en` + `pl`
   (both enabled, `pl` preferred); set default to `en`; **remove** `pl` from
   the catalog via the service; assert
   `provider.GetAsync("nav.home", "pl")` resolves to the `en` text
   (removed ⇒ disabled ⇒ default), **not** a blank, **not** an error.
   (M·1, M·7.)
9. `M9_AdminSavesTranslation_VisibleNextRequest` — seed the M1 row;
   `service.UpsertTranslationAsync("nav.home", "pl", "Start", actor)`; assert
   `provider.GetAsync("nav.home", "pl")` == `"Start"` (M·4 — live on the next
   request); and the `AccessAudit` row for `translation.save` exists with the
   pinned shape. (M·4, M·6.)
10. `M10_AdminSetsDefault_ResidentSeesNewDefault` — seed `en` + `pl` (both
    enabled); seed `pl` string for `nav.home`; set default to `pl` via the
    service; assert `provider.GetAsync("nav.home", null)` == the `pl` text
    (M·1 — the default is picked up **live**); and the `language.set-default`
    audit row exists. (M·1, M·6.)
11. `M11_RemoveDefaultLanguage_Blocked` — seed the M1 row (default `en`);
    seed a second enabled language `pl`; attempt
    `service.RemoveLanguageAsync("en", actor)`; assert it
    **throws** (`InvalidOperationException` — M·7's fail-closed); the `en`
    catalog row is **unchanged**; **no** `language.remove` audit row was
    committed. (M·7, M·6.)
12. `M12_CompletenessView_ShowsMissingKeys` — seed the M1 row; `en` strings
    for `a`, `b`, `c`; `pl` strings for `a`, `b` (no `pl` for `c`); an `en`
    `terms` page; a `pl` `terms` page; **no** `pl` `help` page (and no `en`
    `help` — that's out of the `en` universe); assert
    `service.GetCompletenessAsync("pl")` returns `MissingKeys = ["c"]` and
    `PresentPageSlugs` includes `"terms"`. (M·2, M·6.)
13. `M13_RemovedLanguagePageRows_Retained` — seed `en` + `pl` (both enabled);
    a `pl` `terms` page; **remove** `pl` via the service; **re-add** `pl`;
    assert `service.GetPageAsync("terms", "pl")` returns the **same** page
    row (M·7's row-retention — re-adding restores it; the `LocalizedPage` row
    was **not** deleted by `RemoveLanguageAsync`). (M·7.)

The 6 audit-row-shape tests (each: the seed + the mutation + the audit-row
assertion on the single committed row — `Via = Admin`, `Outcome = Allow`,
`Action`, `TargetKind`, `TargetId` per the design doc §Pinned contract §3
table):

14. `Admin_AddLanguage_AuditRowShape_ViaAdmin` — `AddLanguageAsync("pl",
    "Polski", actor)`; assert one `language.add` row: `TargetKind = "language"`,
    `TargetId = "pl"`, `Via = Admin`, `Outcome = Allow`, `ActorId = actor`.
15. `Admin_RemoveLanguage_AuditRowShape_ViaAdmin` — seed `en` (default) + `pl`
    (enabled); `RemoveLanguageAsync("pl", actor)` (removing the non-default
    `pl` — allowed); assert one `language.remove` row with the pinned shape.
16. `Admin_SetDefaultLanguage_AuditRowShape_ViaAdmin` — seed `en` + `pl`;
    `SetDefaultLanguageAsync("pl", actor)`; assert one `language.set-default`
    row with the pinned shape.
17. `Admin_SaveTranslation_AuditRowShape_ViaAdmin` —
    `UpsertTranslationAsync("nav.home", "pl", "Start", actor)`; assert one
    `translation.save` row with the pinned shape.
18. `Admin_SavePage_AuditRowShape_ViaAdmin` —
    `UpsertPageAsync("terms", "pl", "Warunki", "body", actor)`; assert one
    `page.save` row with the pinned shape.
19. `Admin_RemoveDefaultLanguage_Blocked_NoAuditRow` — seed `en` (default);
    attempt `RemoveLanguageAsync("en", actor)`; assert it **throws** AND the
    `AccessAudit` table is **empty** (the fail-closed **absence** — M·7).

### Test-harness conventions (matching `UserInfoServiceTests`)

- **Fresh scratch DB per test** (`PostgresFixture.NewDatabaseAsync`), boot via
  the `DocumentStore.For` template: `KumunitaFeature` + `AuthorizationFeature`
  + `M1DocTypes.Configure` **+ `M3DocTypes.Configure`** (M6's `Post` plant
  needs the M3 surface) + `ApplyAllConfiguredChangesToDatabaseAsync`.
- **M1 seed via helper** — a private `SeedM1RowAsync(store)` that upserts
  exactly the two rows `FirstBootSeeder.SeedLanguageCatalogAsync` writes (the
  `en` catalog row + the `LocaleSettings` singleton). The seeder's full
  `SeedAsync` pipeline is not needed for the seam tests (the other steps —
  roles, components — are not consumed by the `Localization` seams).
- **Audit-row assertions** — `session.Query<AccessAudit>()` + the pinned field
  shape. The "no audit row" assertions (tests 11 and 19) use the same query
  and assert `Assert.Empty` / a count of 0.
- **No new seams, no new documents, no new packages.** The test file is the
  **only** new file in this unit. The `Post` / `PostReply` / `Group` documents
  used in M6 are seeded via a plain write session (they are already in
  `M3DocTypes`, registered by U1's `M1DocTypes` surface — no schema change).
- **CancellationToken** — `TestContext.Current.CancellationToken` (the
  `UserInfoServiceTests` idiom).

## Exit

1. `dotnet build Kumunita.slnx -c Debug` is **green** (the new test file
   compiles; no Core or Web code touched — the build is a no-op for the
   shipped projects and adds the one new test file to `Kumunita.Core.Tests`).
2. `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
   reports **all 19** `LocalizationServiceTests` `[Fact]`s passing (the
   runner quirk per AGENTS.md — `dotnet test` is not the reliable path on
   this machine; the in-process `dotnet exec` assembly runner is). The
   `Kumunita.Web.Tests` assembly also passes (unchanged, but verified as part
   of the exit).
3. The **U7** section is appended to
   `docs/plans-milestones/done/multilingual-handoff-notes.md` **before**
   the folder move.
4. This plan file is moved `in-progress/` → `done/`.

## Out of scope (U8 / U9)

- The **acceptance-gate run** (the three-test shape: closed loop / handoff /
  part-vs-whole) — U8 owns the *recording* of the gate run; U7's 19 tests
  are the **part-vs-whole** input to that gate.
- The `Milestones.cs` `ML` → done / `M4` → next bump, the README Roadmap
  sync, `ARCHITECTURE.md` §8/§9 sync, and the folder moves — U9 owns the
  close.
- The `/about` route localization follow-on (U6's note: a one-line
  `HomeController.About` change to render `LocalizedPage.Body` when present)
  — U9's decision, not U7's.
