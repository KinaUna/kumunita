# M11 U02 — Export: documents + the no-secret principals + the config

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/plan-m11-portability.md`; the scratch handoff
note is `docs/plans-milestones/m11-portability-handoff-notes.md`; the
atomicity contract and unit-series rule are in the register header.

## Goal

D3/D7 rendered on the export side: the `PortabilityService` export path —
loop the registry (D7's data) and write each `docs/{Type}.json` (the
generic JSON round-trip over the POCOs, **not** per-type code); extract the
**no-secret principals** into `identity/principals.json` (the D3 field set,
the credential columns **excluded** — the C-M11·2 boundary enforced at the
source, the `UserManager` read that *selects* the allowed fields and drops
the hash/stamp/token columns); snapshot the **config** into `config.json`
(the D2 instance-identity + localizations set). The media bytes + the
manifest finalize are U03's.

## Context (locked [PROPOSED] set — build against, don't re-derive)

- **D3 · The identity travels as the graph, never the secrets.** The
  archive carries the **principals** (`subjectId`, `username`, `email`,
  `normalizedEmail`, `displayName`, `roles`, and the lockout/state flags —
  the data needed to re-create a signable principal) **and** the
  delegation / guardian / membership / moderator **domain documents**
  (those live in the M1 domain context, not the Identity schema, so they
  travel with the content). It **never** carries credential material: no
  password hash, no security stamp, no access-token / refresh-token /
  recovery-code, no Identity sign-in record. On import the principals are
  **re-created** (keyed by the exported `subjectId`) and their secrets are
  **reset**. **The no-secret boundary is the single most load-bearing M11
  invariant — enforce it at the source: the `UserManager` read projects
  only the allowed fields and drops the credential columns.**
- **D7 · The closed document inventory is data, not per-type code.** The
  registry loop iterates U01's data verbatim; do not branch per type.
- **D2 · Config = instance identity + localizations.** `config.json`
  carries the community name + the `LocaleSettings` + the language-catalog
  state (the D2 config field set — the exact set is pinned in the design
  doc §config; copy it verbatim).
- **Invariant to honor: C-M11·2 · The archive travels the data, never the
  secrets.** No password hash / security stamp / Identity token in the
  principals POCO. Pinned by the U07 no-secret test (the POCO field-shape +
  a byte-scan witness over the archive). **This unit is where the boundary
  is enforced at the source.**

## Entry reads (5)

1. `docs/design/m11-portability-design.md` — §principals (the locked
   no-secret field set + the **excluded** credential fields), §config (the
   locked config field set), §inventory (the D7 registry U02 loops).
2. `src/Kumunita.Core/Portability/PortabilityService.cs` +
   `PortabilityDocTypes.cs` + `KumunitaArchive.cs` — U01's shell + registry
   + serializer (the seam U02 fills).
3. `src/Kumunita.Core/Identity/UserManager.cs` (or the Identity `UserManager`
   setup in `DependencyInjection.cs`) — the principal read (the `UserManager`
   / `IdentityUser` shape U02 extracts from, and the **excluded** credential
   fields to confirm are dropped).
4. `src/Kumunita.Core/Localization/LanguageCatalog.cs` — the `LocaleSettings`
   + language-catalog state the `config.json` snapshot reads.
5. `src/Kumunita.Core/DependencyInjection.cs` — the `PortabilityService` ctor
   wiring (confirm the `UserManager` / `LocaleSettings` deps resolve).

## Deliverables (≤ 4 files)

- `src/Kumunita.Core/Portability/ExportDocumentsAsync.cs` (new, or a method
  on `PortabilityService`) — the registry loop: for each D7 entry, load all
  rows via `IDocumentStore` + write `docs/{Type}.json` through the
  `KumunitaArchive` writer; the `docCounts` the manifest carries.
- `src/Kumunita.Core/Portability/PrincipalsExport.cs` (new) — the no-secret
  principals extractor: the `UserManager` read that projects **only** the D3
  allowed fields (`subjectId`, `username`, `email`, `normalizedEmail`,
  `displayName`, `roles`, lockout/state) and **drops** the credential
  columns — the C-M11·2 boundary at the source.
- `src/Kumunita.Core/Portability/ConfigExport.cs` (new) — the `config.json`
  snapshot: the community name + the `LocaleSettings` + the language-catalog
  state, per the D2 config field set.
- `src/Kumunita.Core/Portability/PortabilityService.cs` — the export method
  bodies filled in, composing the three above + the
  `KumunitaArchive.WriteAsync`.

## Exit

`dotnet build Kumunita.slnx -c Debug` green (Core + Web). The export loop
writes one `docs/{Type}.json` per registry entry; the principals extraction
**drops** the credential columns (the C-M11·2 source boundary — the U07
no-secret test witnesses it end-to-end). Handoff entry: the export loop's
entry count (matches the registry), the **exact** principals field set as
written (U06's re-creator copies verbatim), the **exact** config field set
as written, the credential columns **confirmed dropped** (the C-M11·2 pin),
the `docCounts` shape, any compile warnings.
