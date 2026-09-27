# M11 U04 — Export: the web surface + the `portability.export` audit + the kw-l keys

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/plan-m11-portability.md`; the scratch handoff
note is `docs/plans-milestones/m11-portability-handoff-notes.md`; the
atomicity contract and unit-series rule are in the register header.

## Goal

D5 rendered on the export side: the GlobalAdmin web surface —
`GET /admin/portability` (the index: the export button + the import upload
form + a status area) + `GET /admin/portability/export` (streams the
`*.kumunita` archive, the `Content-Disposition: attachment` the ADR 0034
attachment lane's shape). The **one** `portability.export` `AccessAudit` row
(`TargetKind "portability"`, `Via = Admin`, verb `export`) — emitted by the
service, the controller adds none (the ADR 0105 `messaging.toggle` shape).
The **kw-l keys** for the operator-facing strings (the D10 list, × 4
languages, the closed-key registry + the parity test).

## Context (locked [PROPOSED] set — build against, don't re-derive)

- **D5 · The surface is the admin web plane (GlobalAdmin), audited,
  quiet.** `GET /admin/portability` (the index: the export button + the
  import upload form + a status area) + `GET /admin/portability/export`
  (streams the `*.kumunita` archive). All three routes are
  `[Authorize(Roles = GlobalAdmin)]`; **export** and **import** each emit
  exactly **one** `AccessAudit` row (`TargetKind "portability"`, `Via =
  Admin`, verb `"export"` / `"import"`). A plain resident — including a
  moderator — never sees the surface (the ADR 0101 / 0105 admin-surface
  shape, no break-glass read). U04 does the export half; U06 does the
  import half + reuses the shared kw-l keys.
- **Invariant to honor: C-M11·6 · The surface is GlobalAdmin + audited +
  quiet.** Export + import are `[Authorize(Roles = GlobalAdmin)]`, each
  emits **one** `AccessAudit` row (`TargetKind "portability"`, `Via =
  Admin`, verb `export` / `import`), and both are hidden from a
  non-GlobalAdmin. Pinned by the U07 Web-surface pins (the gate + the
  one-audit-row assertions).
- **Invariant to honor: C-M11·7 · Zero new authorization surface.** No new
  `AccessAction` / `AccessVia` / `Audience`. The controller gate is the
  GlobalAdmin role; the service owns the audit row.
- **D10 · kw-l keys + the deferred lanes.** The operator-facing strings
  (`portability.index.title` / `portability.export` / `portability.import` /
  `portability.confirm.import` / `portability.status.*`) × 4 languages (the
  closed-key registry + `KnownTranslationKeys_ParityTests` enforces). U04
  registers the shared keys; U06 adds any import-specific ones.

## Entry reads (5)

1. `docs/design/m11-portability-design.md` — §surface (the D5 route + action
   set + the one-audit-row shape), §kw-l (the locked key list).
2. `src/Kumunita.Web/Controllers/AdminMessagingController.cs` — the
   ADR 0105 admin-surface shape to mirror (the `[Authorize(Roles = GlobalAdmin)]`,
   the one-audit-row, the `KumunitaPrincipal.SubjectId` actor extraction, the
   `TempData["info"]` + redirect).
3. `src/Kumunita.Web/Views/Admin/Messaging.cshtml` (or the matching ADR 0105
   view) — the admin-view shape to mirror (the card + the form + the status
   area).
4. `src/Kumunita.Web/Controllers/AttachmentController.cs` — the
   `Content-Disposition: attachment` + the `File(...)` streaming shape the
   `export` action reuses.
5. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the closed-key
   registry (the `portability.*` insertion point + the 4-language parity
   shape).

## Deliverables (≤ 5 files)

- `src/Kumunita.Web/Controllers/AdminPortabilityController.cs` (new) — the
  `[Authorize(Roles = GlobalAdmin)]` controller: `GET Index` (the index view),
  `GET Export` (the `PortabilityService.ExportAsync` → `File(stream,
  "application/octet-stream", "kumunita.kumunita")`, the one
  `portability.export` audit row emitted by the service — the controller
  adds none).
- `src/Kumunita.Web/Views/Admin/Portability.cshtml` (new) — the index view:
  the export button (a `<a href="~/admin/portability/export">`), the import
  upload form (U06's POST target — the form + the file input + the confirm,
  rendered now so the surface is whole), the status area (`TempData`).
- `src/Kumunita.Core/Portability/PortabilityService.cs` — the
  `portability.export` audit row emission in the export path — the one
  `AccessAudit` row, `Via = Admin`, verb `export`, the ADR 0105 shape.
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the
  `portability.*` kw-l keys × 4 languages — the D10 list, the closed-key
  registry + the parity.
- `src/Kumunita.Web/Views/Shared/_AdminNav.cshtml` (or the matching
  admin-nav partial) — the one nav link to `/admin/portability`, the ADR 0105
  nav-entry precedent.

## Exit

`dotnet build Kumunita.slnx -c Debug` green (Web); the `portability.*`
parity test passes (the `KnownTranslationKeys_ParityTests` enforces the
4-language set). A GlobalAdmin `GET /admin/portability/export` streams a
complete `*.kumunita` archive + emits the one `portability.export` audit row
(the U07 Web-surface pin witnesses it). Handoff entry: the exact route +
action names as written (U06's import + the U07 pins target verbatim), the
one-audit-row shape (`TargetKind` / `Via` / verb) as written, the **exact**
`portability.*` kw-l keys + their 4-language strings as written (U06 reuses
the shared ones), the nav-link insertion, any compile warnings.
