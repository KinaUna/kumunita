# M11 U01 — Framework: the `Portability` context, manifest model, inventory registry, archive (de)serializer

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/done/m11/plan-m11-portability.md`; the scratch handoff
note is `docs/plans-milestones/done/m11/m11-portability-handoff-notes.md`; the
atomicity contract and unit-series rule are in the register header.

## Goal

D2/D7 rendered as code: the `Kumunita.Core/Portability/` context — the
**manifest model** (the `manifest.json` POCO set, the D2 field set), the
**document-inventory registry** (D7's closed `Type` / file / order /
reference-fields list, as **data** the U02–U06 loops iterate), and the
**archive (de)serializer** (the `System.IO.Compression.ZipArchive` read +
write over the D2 layout — `manifest.json`, `docs/{Type}.json`,
`media/{Id[0..2]}/{Id}`, `identity/principals.json`, `config.json`). No
export/import *service* yet (U02–U06 own that); this unit is the uniform
machinery they compose.

## Context (locked [PROPOSED] set — build against, don't re-derive)

- **D2 · The archive is a versioned ZIP, BCL-only (no new dependency).**
  `System.IO.Compression.ZipArchive` (in the .NET 10 framework — the
  lean-stack + tsc-only discipline holds; **no** new package). Layout:
  `manifest.json` (self-describing) + `docs/{DocType}.json` (one
  JSON-array file per registered domain document type — the per-type
  isolation that keeps the round-trip testable) + `media/{Id[0..2]}/{Id}`
  (the content-addressed bytes, the **same** layout as the local volume,
  so import is a byte-copy and dedup-by-content-hash is preserved) +
  `identity/principals.json` (the no-secret graph) + `config.json` (the
  instance identity + localizations). `*.kumunita` is the file extension
  (a content marker, not a format claim — the `format` field in
  `manifest.json` is the authority). The `format` version constant is
  `kumunita/portability/1`.
- **D7 · The closed document inventory + import order is pinned in the
  design doc from source.** The registry is the **exact** closed inventory
  U00 pinned — each entry: the document `Type`, its archive file
  (`docs/{Type}.json`), its **import order** (parents before children), and
  its **reference fields** (which of its fields point at another entry's id)
  — implemented as **data**. The inventory is data the U02/U05 loops iterate
  generically, **not** per-type code — that is what keeps the export/import
  units atomic despite ~40 document types. **U01 turns U00's pinned list
  into the registry data; do not re-derive the list from source — copy it
  verbatim from `docs/design/m11-portability-design.md` §inventory.**
- **D8 · One new `Portability` context in Core — an operator lane, not a
  content-decision lane.** `Kumunita.Core/Portability/` holds the manifest
  model, the document-inventory registry, the archive (de)serializer, and
  the `PortabilityService` (export / validate / apply). It is registered in
  `DependencyInjection.cs` like its siblings. **No** new `AccessAction`,
  **no** new `AccessVia`, **no** `IAuthorizationService` branch, **no**
  `Audience` (C-M11·7). U01 provides the `PortabilityService` **shell**
  (ctor + method signatures only) — the seam the Web layer + the tests
  target.
- **Invariant to honor: C-M11·7 · Zero new authorization surface.** No new
  `AccessAction` / `AccessVia` / `IAuthorizationService.Decide()` branch /
  `Audience`. Portability reuses the GlobalAdmin role gate (the
  ADR 0101/0105 shape); the content's audiences/grants/delegations travel
  **as data**, the decision engine is untouched.

## Entry reads (5)

1. `docs/design/m11-portability-design.md` — §manifest (the locked field
   set), §inventory (the locked `Type` / file / order / reference-fields
   list), §layout (the D2 archive layout).
2. `src/Kumunita.Core/M1DocTypes.cs` + `M3DocTypes.cs` — the document types
   the registry enumerates (U00 pinned the list; the `M*DocTypes` surfaces
   confirm each type's namespace + the business-key fields the reference
   map needs).
3. `src/Kumunita.Core/Media/IMediaFileStore.cs` — the content-addressed
   path derivation the media (de)serializer reuses (`{root}/{Id[0..2]}/{Id}`).
4. `src/Kumunita.Core/DependencyInjection.cs` — the registration shape (the
   `PortabilityService` + the registry register next to its siblings).
5. `src/Kumunita.Core/Kumunita.Core.csproj` — confirm `System.IO.Compression`
   needs no package (it is in the BCL); record if a `System.IO.Packaging` /
   `ZipArchive` using is needed.

## Deliverables (≤ 5 files)

- `src/Kumunita.Core/Portability/PortabilityManifest.cs` (new) — the
  `manifest.json` POCO set: `format`, `generated_at`, `community_name`,
  `docCounts` (a `Dictionary<string,int>`), `mediaManifest` (the file list +
  sizes + content types), + the `format` version constant
  `kumunita/portability/1`.
- `src/Kumunita.Core/Portability/PortabilityDocTypes.cs` (new) — the D7
  closed inventory registry: the ordered list of
  `{ Type, FileName, Order, ReferenceFields }`, **data** read from U00's
  pinned list, with a `ByType` lookup + the ordered iteration U02/U05
  consume.
- `src/Kumunita.Core/Portability/KumunitaArchive.cs` (new) — the archive
  (de)serializer: `WriteAsync(Stream, …)` + `ReadAsync(Stream)` over the D2
  layout using `ZipArchive`; the `docs/{Type}.json` round-trip via
  `System.Text.Json`; the media-path derivation reused from `IMediaFileStore`'s
  convention.
- `src/Kumunita.Core/DependencyInjection.cs` — register the registry + the
  serializer next to its siblings.
- `src/Kumunita.Core/Portability/PortabilityService.cs` (new) — the service
  **shell**: the ctor taking `IDocumentStore` + the Identity `UserManager` +
  `IMediaStore`/`IMediaFileStore` + the registry, with the export / validate /
  apply method **signatures** only, `NotImplemented` or empty bodies U02–U06
  fill in — the seam the Web layer + the tests target.

## Exit

`dotnet build Kumunita.slnx -c Debug` green (Core + Web). The `Portability`
context compiles; the registry enumerates the D7 list; the archive
round-trips a trivial `manifest.json` + one `docs/*.json` (a smoke, not a
pinned test — the real tests are U07's). Handoff entry: the registry's entry
count (the D7 list length, so U02/U05 know the loop size), the
`PortabilityService` ctor + method signatures as written (U02–U06 copy
verbatim), the `KumunitaArchive` public surface (the write/read entry
points), the `format` version string, any compile warnings.
