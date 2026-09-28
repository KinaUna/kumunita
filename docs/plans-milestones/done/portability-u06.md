# M11 U06 — Import: the identity re-creation (secrets reset) + the config apply + the web surface + the `portability.import` audit + the kw-l keys

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/plan-m11-portability.md`; the scratch handoff
note is `docs/plans-milestones/m11-portability-handoff-notes.md`; the
atomicity contract and unit-series rule are in the register header.

## Goal

D3/D5 rendered on the import side: the import path completes — the
**identity re-creation** (from `identity/principals.json`: re-create each
principal keyed by the exported `subjectId`, **reset** the secrets — a fresh
non-portable password + a fresh security stamp — and re-apply the roles, the
C-M11·2 + D3 boundary on the import side); the **config apply** (from
`config.json`: the community name + the `LocaleSettings` + the
language-catalog state, per the D2 config field set); the **web surface**
completes — `POST /admin/portability/import` (applies the uploaded archive,
the `portability.import` audit row, the D5 surface) + the **kw-l keys** for
the import-side strings (the D10 list, × 4 languages). After this unit the
import produces a **complete, coherent** instance (the U07 round-trip test
witnesses it end-to-end).

## Context (locked [PROPOSED] set — build against, don't re-derive)

- **D3 · The identity travels as the graph, never the secrets — and is
  re-created, not replayed.** The archive carries the principals (`subjectId`,
  `username`, `email`, `normalizedEmail`, `displayName`, `roles`, the
  lockout/state flags) **without** credential material. On import the
  principals are **re-created** (keyed by the exported `subjectId`) and
  their secrets are **reset** (a fresh, non-portable password + a fresh
  security stamp; the resident re-authenticates). The role assignments are
  re-applied. The "principal already exists" case is the D1 restore semantic
  (a fresh instance — a collision is a validate-phase failure, the C-M11·4
  pin).
- **D2 · Config = instance identity + localizations.** `config.json`
  carries the community name + the `LocaleSettings` + the language-catalog
  state (the D2 config field set — the exact set is pinned in the design
  doc §config; mirror U02's `ConfigExport` verbatim).
- **D5 · The surface is the admin web plane (GlobalAdmin), audited,
  quiet.** `POST /admin/portability/import` applies the uploaded archive +
  emits exactly **one** `AccessAudit` row (`TargetKind "portability"`,
  `Via = Admin`, verb `import`) — the controller adds none.
- **D10 · kw-l keys.** The import-side strings (the D10 list, × 4 languages,
  the closed-key registry + the parity test). The U04 export-side keys are
  **reused, not duplicated**.
- **Invariant to honor: C-M11·2 · the secrets are never replayed.** The
  import re-creates the principals and **resets** the secrets — it never
  restores a password hash / security stamp / Identity token (there is none
  in the archive, by C-M11·2). **This unit is where the boundary is enforced
  on the import side.**

## Entry reads (5)

1. `docs/design/m11-portability-design.md` — §principals (the locked
   no-secret field set the re-creator consumes), §config (the locked config
   field set), §surface (the D5 `import` route + action + the one-audit-row
   shape), §kw-l (the locked key list).
2. `src/Kumunita.Core/Portability/PortabilityService.cs` +
   `PrincipalsExport.cs` + `ConfigExport.cs` — U02's principals/config
   extractors (the **exact** field set U06's re-creator + applier mirror,
   verbatim).
3. `src/Kumunita.Core/Identity/UserManager.cs` (or the Identity `UserManager`
   setup) — the `UserManager.CreateAsync` + the `AddToRoleAsync` + the
   **reset** shape (the `PasswordHasher` / the fresh password + the
   security-stamp reset the C-M11·2 import boundary enforces).
4. `src/Kumunita.Web/Controllers/AdminPortabilityController.cs` — U04's
   export surface (the `import` action + the view form U06 completes, the
   D5 shape).
5. `src/Kumunita.Web/Views/Admin/Portability.cshtml` — U04's index view (the
   import upload form U04 rendered, U06 wires the `POST` + the status
   render).

## Deliverables (≤ 5 files)

- `src/Kumunita.Core/Portability/ApplyIdentityAsync.cs` (new, or a method)
  — the identity re-creation: for each `identity/principals.json` entry, the
  `UserManager.CreateAsync` (keyed by the exported `subjectId`, the
  **reset** secrets — a fresh non-portable password + a fresh security stamp
  — the C-M11·2 import boundary) + the `AddToRoleAsync` for the exported
  roles; the "principal already exists" case is the D1 restore semantic (a
  fresh instance — a collision is a validate-phase failure, the C-M11·4
  pin).
- `src/Kumunita.Core/Portability/ApplyConfigAsync.cs` (new, or a method) —
  the config apply: the community name + the `LocaleSettings` + the
  language-catalog state, per the D2 config field set — the U02 `ConfigExport`
  mirror, verbatim.
- `src/Kumunita.Core/Portability/PortabilityService.cs` — the `ImportAsync`
  completes — the U05 `ValidateAsync` + the apply documents + apply media +
  the U06 `ApplyIdentityAsync` + `ApplyConfigAsync`, one `ImportAsync` that
  validate-fails-closed else applies the whole instance; the
  `portability.import` audit row emission — the one `AccessAudit` row,
  `Via = Admin`, verb `import`, the ADR 0105 shape.
- `src/Kumunita.Web/Controllers/AdminPortabilityController.cs` — the `POST
  Import` action completes — the uploaded file → `PortabilityService.ImportAsync`
  → the success / the closed-failure-set render, the one
  `portability.import` audit row emitted by the service.
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the import-side
  `portability.*` kw-l keys × 4 languages — the D10 list, the closed-key
  registry + the parity; the U04 export-side keys are reused, not duplicated.

## Exit

`dotnet build Kumunita.slnx -c Debug` green (Core + Web); the
`portability.*` parity test passes (the `KnownTranslationKeys_ParityTests`
enforces the 4-language set). A GlobalAdmin `POST /admin/portability/import`
over a valid archive produces a complete, coherent instance (the principals
re-created + the secrets reset + the roles re-applied + the config applied —
the U07 round-trip test witnesses it end-to-end); a failed import renders the
closed failure set + the instance is unchanged (the U07 fail-closed test
witnesses it). Handoff entry: the **exact** identity re-creation shape (the
`subjectId` keying + the secret reset + the role re-apply) as written, the
**exact** config-apply field set as written (matches U02's `ConfigExport`,
verbatim), the one-audit-row shape (`TargetKind` / `Via` / verb) as written,
the `POST Import` + the closed-failure-set render as written (the U07 pins
target verbatim), the **exact** import-side `portability.*` kw-l keys + their
4-language strings as written, any compile warnings.
