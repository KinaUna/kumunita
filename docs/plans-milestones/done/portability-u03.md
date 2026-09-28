# M11 U03 — Export: the media bytes + the manifest finalization

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/done/plan-m11-portability.md`; the scratch handoff
note is `docs/plans-milestones/done/m11-portability-handoff-notes.md`; the
atomicity contract and unit-series rule are in the register header.

## Goal

D6 rendered: the export path completes — copy the **media bytes** into the
archive at the `{Id[0..2]}/{Id}` layout (the same as the local volume, the
C-M11·3 pin), build the **media manifest** (the file list + sizes + content
types, read from the `MediaObject` catalog), and **finalize the manifest**
(`manifest.json` — the `format` version, the `generated_at`, the community
name, the `docCounts` from U02, the media manifest). After this unit the
export produces a **complete, valid** `*.kumunita` archive (the U04 web
surface just streams it).

## Context (locked [PROPOSED] set — build against, don't re-derive)

- **D6 · The media travels content-addressed, with the catalog doc.** The
  bytes are copied **verbatim** into the archive at the **same**
  `{Id[0..2]}/{Id}` layout the local volume uses (ADR 0011 C-MED·3/4/7), so
  import is a byte-copy and the content-hash dedup is preserved
  (re-importing the same file is a no-op at the byte layer). The
  `MediaObject` catalog doc travels as a `docs/MediaObject.json` row (the
  surviving catalog reference a `pg_dump` carries, per its own
  doc-comment).
- **Invariant to honor: C-M11·3 · The media travels content-addressed +
  whole.** The bytes are copied verbatim (dedup-by-content-hash preserved);
  the `MediaObject` catalog doc + its bytes are either both present or the
  archive is rejected (that reject is U05's validate). Pinned by the U07
  round-trip test (the media leg) + the fail-closed test (the truncated
  bytes leg).
- **Invariant to honor: C-M11·1 · The archive is versioned +
  self-describing.** `manifest.json` carries the `format` version
  (`kumunita/portability/1`), the `generated_at`, the community name, the
  per-document-type counts, and the media manifest.

## Entry reads (4)

1. `docs/design/m11-portability-design.md` — §media (the D6 layout + the
   media-manifest field set), §manifest (the finalize field set).
2. `src/Kumunita.Core/Portability/PortabilityService.cs` +
   `KumunitaArchive.cs` — U02's export path + the serializer's media write
   seam.
3. `src/Kumunita.Core/Media/IMediaFileStore.cs` + `IMediaStore.cs` — the
   `OpenReadAsync` + the `MediaObject` catalog (the source of the bytes +
   the media manifest the U03 copies).
4. `src/Kumunita.Core/Media/MediaObject.cs` — the catalog shape (the `Id`
   (the content hash) + the `SizeBytes` / `ContentType` the media manifest
   carries).

## Deliverables (≤ 3 files)

- `src/Kumunita.Core/Portability/MediaExport.cs` (new) — the media byte
  copy: for each `MediaObject` in the catalog, `OpenReadAsync` the bytes +
  write them into the archive at the `{Id[0..2]}/{Id}` layout (the
  `KumunitaArchive` writer's media seam) + accumulate the media-manifest
  entry.
- `src/Kumunita.Core/Portability/ManifestFinalize.cs` (new, or a method) —
  the `manifest.json` finalize: the `format` version + the `generated_at` +
  the community name + the `docCounts` (from U02) + the media manifest
  (from U03).
- `src/Kumunita.Core/Portability/PortabilityService.cs` — the export method
  completes — the U02 doc/principal/config write + the U03 media + the
  manifest finalize, one `ExportAsync` producing a complete archive `Stream`.

## Exit

`dotnet build Kumunita.slnx -c Debug` green (Core + Web). A
`PortabilityService.ExportAsync` over a seeded store produces a complete
`*.kumunita` archive (the U07 round-trip test witnesses it end-to-end).
Handoff entry: the media-copy entry count (matches the catalog), the
**exact** media-manifest field set as written (U05's validator copies
verbatim), the **exact** finalized manifest field set as written, the
`ExportAsync` public signature (the U04 web surface + the U07 test target it
verbatim), any compile warnings.
