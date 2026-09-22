# U1 — `Page` + `PageTranslation` docs + registration + DI seam

- **Lane:** Pages (`PG`)
- **Unit:** U1 (of U0–U07)
- **Kind:** structure (new docs + registration + DI — **zero behavior**)

## Goal

Create the `Kumunita.Core.Pages` bounded context with the two documents —
**`Page`** (the §3.2 field set) and **`PageTranslation`** (the ADR 0022 row
shape) — register them on a doc-types surface with the two unique indexes,
and wire `IPageService` → `PageService` into `DependencyInjection.cs`. No read
lanes, no write lanes, no controller yet. This is the lane's first *structure*
unit: after it, the schema delta applies idempotently and the two docs exist.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/pages-design.md` §3.2 — the **exact** `Page` +
   `PageTranslation` field set + the field-provenance table (every field named
   to an existing idiom).
2. `src/Kumunita.Core/M3DocTypes.cs` — the registration style + the
   `ann_tr_uidx_ann_lang` **short explicit index name** precedent (Postgres
   64-char `NAMEDATALEN`).
3. `src/Kumunita.Core/Announcements/AnnouncementTranslation.cs` — the
   `PageTranslation` row shape to mirror (the ADR 0022/0029 `<Parent>Translation`
   doc: `Id`/`PageId`/`LanguageCode`/`Title?`/`Body`/`AuthorId`/`Created`).
4. `src/Kumunita.Core/DependencyInjection.cs` — the "AddTransient with the
   store injected" DI shape (`IAnnouncementService` → `AnnouncementService`).
5. `src/Kumunita.Core/Authorization/Audience.cs` — the **existing** `Audience`
   doc the `Page.Audience` field references (reused verbatim, **not** extended).

## Deliverables (closed set)

1. **`src/Kumunita.Core/Pages/Page.cs`** — the doc: `Id`, `ParentId?`, `Slug`,
   `Title`, `Body`, `Audience?` (the `Kumunita.Core.Authorization.Audience`;
   `null` = public), `AuthorId`, `ComponentId?`, `LanguageCode`,
   `MountPoint?`, `Created`, `Modified?`, `IsDraft`, `IsDeleted` (U03's
   soft-delete flag — declared now so the doc shape is final), `ImageIds`,
   `AttachmentIds`.
2. **`src/Kumunita.Core/Pages/PageTranslation.cs`** — the doc: `Id`, `PageId`,
   `LanguageCode`, `Title?`, `Body`, `AuthorId`, `Created`.
3. **Registration** (pick **one** surface — recommend a new
   `src/Kumunita.Core/Pages/PageDocTypes.cs`, the `M3DocTypes.cs` shape):
   - `Page`: conventional `Id` + a **`(ParentId, Slug)`** unique index
     (root rows are `(null, Slug)`; the `GroupMembership` business-key
     convention).
   - `PageTranslation`: a **`(PageId, LanguageCode)`** unique index — use an
     **explicit short name** (e.g. `pg_tr_uidx_page_lang`) if the auto-derived
     name would exceed 64 chars (the `ann_tr_uidx_ann_lang` precedent).
4. **`src/Kumunita.Core/Pages/IPageService.cs`** + **`PageService.cs`** —
   the store-composing service seam (the `IAnnouncementService` shape). For
   this unit it may be an **empty shell** (the `IPageService` interface
   declared but with no read/write methods yet — U02/U03 add them); the DI
   registration is the load-bearing part.
5. **`src/Kumunita.Core/DependencyInjection.cs`** — `AddTransient<IPageService,
   PageService>()` (the store injected), the feature-registration call for the
   new `Pages` context (mirror how `Announcements` is registered).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green — the schema delta applies **idempotently** (ADR 0004 §B.1), the two
  unique indexes present, and the existing `LocalizedPage` surface is
  **untouched** (its `M1DocTypes` registration still there — U07 removes it).
- The `Page`/`PageTranslation` POCOs are reviewable against the §3.2
  field-provenance table (every field names an existing idiom; no field is
  invented without an ADR anchor).
- Append a `## U1 — docs + registration + DI` section to
  `pages-handoff-notes.md` (files built, the build green, the chosen
  registration surface + the index names, any drift) **before** moving this
  file.

## Notes / deviations

- **Zero behavior.** If you find yourself writing a `GetByPathAsync` or
  `CreateAsync` here, stop — those are U02/U03. This unit is docs + indexes +
  DI only.
- **Registration surface choice:** the register leaves `PageDocTypes` vs
  `M3DocTypes` open. A **new `PageDocTypes.cs`** is the cleaner fit (a new
  bounded context deserves its own surface — the `M3DocTypes` name implies
  M3-specific docs). Record whichever you pick in the handoff section.
- **`IsDeleted` is declared in U01** (part of the doc shape) but **acted on**
  in U03 (the soft-delete write lane) and U02 (the `CanSeeAsync` filter). The
  doc shape is final at U01 so later units don't drift it.
- The `Audience` reference is the **existing**
  `Kumunita.Core.Authorization.Audience` — do **not** create a new one, do
  **not** add a `Public` bool (public **is** `Audience == null`,
  `Decide()` branch 5).
