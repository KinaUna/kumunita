# M11 U05 — Import: the validate-then-apply of documents + media, fail-closed

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/done/plan-m11-portability.md`; the scratch handoff
note is `docs/plans-milestones/done/m11-portability-handoff-notes.md`; the
atomicity contract and unit-series rule are in the register header.

## Goal

D4 rendered: the `PortabilityService` import path — the **validate phase**
(runs to completion **before any write**, the C-M11·4 pin): (a) the
`manifest.json` `format` is one the build understands (else reject —
C-M11·1); (b) each `docs/{Type}.json` deserializes into its declared POCO
set (the registry's `Type`); (c) **referential integrity** — every id
referenced by the registry's `ReferenceFields` resolves to a row within the
archive (the D7 reference map, data-driven, the uniform loop — **not**
per-type code); (d) the media manifest's bytes are all present + their
content-hash matches the path (the C-M11·3 pin). **Any failure ⇒ zero
writes.** The **apply phase** (one commit): store the domain documents in
the registry's **import order** (parents before children, the D7 order) +
copy the media bytes into the volume (the C-M11·3 pin). The identity
re-creation + the config apply are U06's.

## Context (locked [PROPOSED] set — build against, don't re-derive)

- **D4 · Import is validate-then-apply, fail-closed, atomic.** The
  **validate phase** runs to completion **before any write**: (a) the
  `format` is understood (else reject — C-M11·1); (b) each `docs/{Type}.json`
  deserializes into its declared POCO set; (c) **referential integrity** —
  every id referenced by any document (an author's `subjectId`, a post's
  `ComponentId`, a reply's `PostId`, an RSVP's `EventId`, a message's
  `ConversationId`, a media `Id`, …) resolves to a row **within the
  archive** (the closed reference map, data-driven over the registry's
  `ReferenceFields` — **not** per-type code); (d) the media manifest's bytes
  are all present and their content-hash matches their path. **Any failure
  ⇒ zero writes.** The **apply phase** is one commit in the pinned
  dependency order. A mid-apply failure is the documented restore path
  (OPS.md), never a silently-accepted half-import.
- **D7 · The reference map is data, not per-type code.** The integrity loop
  iterates the registry's `ReferenceFields` generically.
- **D6 · The media travels content-addressed.** The apply copies the bytes
  into the volume at the `{Id[0..2]}/{Id}` layout; the `MediaObject` catalog
  doc is stored.
- **Invariant to honor: C-M11·4 · Import is fail-closed + atomic.** The
  validate phase runs to completion **before** any write (format + per-type
  sanity + referential integrity + media verification); any failure ⇒
  **zero writes**. Pinned by the U07 fail-closed test (the fresh instance
  has **zero** rows after a failed import). **This unit is where the
  boundary is enforced at the source.**
- **Invariant to honor: C-M11·1 · reject an unknown `format`.** And
  **C-M11·3 ·** a missing/mismatched media byte is a validate-phase failure.

## Entry reads (5)

1. `docs/design/m11-portability-design.md` — §validate (the locked validate
   checks + the C-M11·1/3/4 pins), §apply-order (the D7 registry order),
   §inventory (the D7 reference map the integrity loop drives).
2. `src/Kumunita.Core/Portability/PortabilityService.cs` +
   `KumunitaArchive.cs` + `PortabilityDocTypes.cs` — U01's shell + serializer
   + registry (the seam U05 fills; the registry's `Order` + `ReferenceFields`
   the apply + the integrity loop consume).
3. `src/Kumunita.Core/Media/IMediaFileStore.cs` — the `PutAsync` + the
   content-addressed layout the media apply writes to.
4. `src/Kumunita.Core/M1DocTypes.cs` — the `Profile.SubjectId` identity + the
   `GroupMembership` business key (the referential relationships the
   integrity loop checks *against*, confirming the D7 reference map matches
   the source).
5. `src/Kumunita.Core.Tests/PostgresFixture.cs` — the test harness the U07
   round-trip / fail-closed tests use (U05's apply path is what they
   exercise).

## Deliverables (≤ 4 files)

- `src/Kumunita.Core/Portability/ValidateAsync.cs` (new, or a method) — the
  validate phase: the `format` check (C-M11·1) + the per-type deserialize
  sanity + the data-driven referential-integrity loop over the registry's
  `ReferenceFields` + the media-manifest byte verification (C-M11·3) —
  returns a `ValidationResult` (ok / the closed failure set); **zero
  writes**.
- `src/Kumunita.Core/Portability/ApplyDocumentsAsync.cs` (new, or a method)
  — the apply phase: store the domain documents in the registry's **import
  order** (the D7 order, the one commit) — the uniform loop, **not**
  per-type code.
- `src/Kumunita.Core/Portability/ApplyMediaAsync.cs` (new, or a method) —
  the media byte copy into the volume at the `{Id[0..2]}/{Id}` layout (the
  C-M11·3 pin), the `MediaObject` catalog doc stored.
- `src/Kumunita.Core/Portability/PortabilityService.cs` — the `ValidateAsync`
  + the apply methods composed — the `ImportAsync` shape: `ValidateAsync`
  first (a failure ⇒ return the closed failure set, **no apply**), else the
  apply documents + apply media — the identity re-creation + the config
  apply are U06's, the seam is reserved.

## Exit

`dotnet build Kumunita.slnx -c Debug` green (Core + Web). A `ValidateAsync`
over a corrupted archive (wrong `format` / a dangling reference / a
truncated media byte) returns the closed failure set + writes **zero** rows
(the U07 fail-closed test witnesses it); a `ValidateAsync` over a valid
archive + the apply stores the documents in order + the media bytes (the
U07 round-trip test witnesses it). Handoff entry: the exact validate-check
set as written (the U07 fail-closed test asserts exactly those), the
**exact** closed failure-set shape (the U06 web surface + the U07 pin render
it), the apply-order entry count (matches the registry), the `ImportAsync`
public signature (the U06 + the U07 target verbatim), the "zero writes on
validate failure" pin confirmed (the C-M11·4 source boundary), any compile
warnings.
