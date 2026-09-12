# ML-UI U1 — the canonical `en` registry + the seeder step

> **Lane.** `ML-UI` (the UI-wiring completion of the `ML` / ADR 0005 promise).
> **Unit.** U1 of 9 (the lane plan: `plan-multilingual-ui.md`).
> **Kind.** **Code unit** — exit gate is a green build.

## Goal

Create the canonical `en` string registry (`KnownTranslationKeys`) and a new
first-run seeder step that materializes the `en` `TranslationResource` rows (one
per key) and the `en` `LocalizedPage` rows for **terms + help only** — so the
M·9 `en` floor and the M·12 completeness view become real. **`about` is NOT
seeded** (a fresh instance's `/about` keeps its product-story view; an admin
creates an `about` page at runtime — the plan's pinned U1 decision (b)).

## Entry reads (start here)

- `plan-multilingual-ui.md` — the four verified gaps, the Assumptions (the two
  **pinned U1 decisions**), the Pinned contract (§U1 shape), the in-scope
  surface (the bounded key set), U1's row + entry-read list.
- `multilingual-ui-handoff-notes.md` — the **U0** section (frozen-seam list,
  only-allowed-ADDs, D1/D2).
- `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` — the
  `SeedLanguageCatalogAsync` step (~lines 225–260) + the `SeedAsync` call site.
- `src/Kumunita.Core/Localization/{TranslationResource,LocalizedPage}.cs` — the
  two POCOs (property names, `Id` = `Guid` string "N" convention).
- `src/Kumunita.Core/M1DocTypes.cs` — the two `UniqueIndex` pins (confirm they
  already exist — **U1 adds no index**).
- `src/Kumunita.Core/Localization/LocalizationService.cs` — `UpsertTranslationAsync`
  / `UpsertPageAsync` (the authoritative pair-upsert idiom to mirror: query-then-
  `Store`, one `SaveChangesAsync`, **no** `AccessAudit` on the seeder path).
- `Views` (read-only, string extraction): `Shared/{_Layout,_AccountNav}`,
  `Home/Index`, `Account/{Login,Signup}`, `Posts/{Index,Detail,New,Edit}`,
  `Groups/{Index,Detail,New,PostDetail}`, `Directory/Index`, `Profile/Edit`,
  `Admin/Index`.

## Deliverables (closed set — 2 files)

1. **New:** `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
   - `public static class KnownTranslationKeys` with
     `public static IReadOnlyDictionary<string, string> EnValues { get; }`
     (closed in-scope set) and `public static IReadOnlyCollection<string> AllKeys => EnValues.Keys;`.
   - Dotted keys, grouped by area (nav / footer / home / account / posts /
     groups / directory / profile / admin) with a short comment per group.
   - **No UGC keys** (M·3); **no** moderation-only / setup-flow / out-of-scope
     keys (drift pause if tempted).
2. **Modified:** `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs`
   - A new idempotent step (`SeedTranslationResourcesAsync`) called from
     `SeedAsync` **after** `SeedLanguageCatalogAsync`.
   - **Code-wins upsert for `en` only:** per key, query the `(Key, "en")` row
     and `Store` (create-if-absent with a fresh `Id`, overwrite text if present);
     upsert `terms` + `help` `LocalizedPage` rows by `(Slug, "en")`. **Never
     touch non-`en` rows.** One `SaveChangesAsync`; a `LogInformation` on success.

## Exit

- **Build green:** `dotnet build Kumunita.slnx -c Debug`.
- **Spot-check:** `KnownTranslationKeys.AllKeys` non-empty; the seeder step is
  wired in `SeedAsync` (no test — U8's job).
- **Handoff note:** append `## U1` to `multilingual-ui-handoff-notes.md`
  (what was added, the exact key count, the upsert semantics, deviations) —
  **before** the folder move.
- **Folder move:** this file → `docs/plans-milestones/done/` on exit.
- **Clean git status** — only the two deliverable files (+ the plan/handoff docs)
  changed.

## Notes / deviations

- (recorded here as U1 executes)
