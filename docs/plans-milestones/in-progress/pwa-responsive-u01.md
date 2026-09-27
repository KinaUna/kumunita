# M10 U01 — Static assets: manifest + icons + the `_Layout` link + the one key

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/plan-m10-pwa-responsive.md`; the scratch handoff
note is `docs/plans-milestones/m10-pwa-handoff-notes.md`; the atomicity
contract and unit-series rule are in the register header. **Runs after
U00** — the design doc §manifest + §icons are the locked authority; copy
their values verbatim, do not re-derive.

## Goal

D1/D3/D4 rendered: `wwwroot/manifest.webmanifest` (the C-M10·1 shape,
the D3 locked values), the two new icon PNGs in `wwwroot/images/pwa/`
(192×192 + 512×512, the D4 provenance), the `<link rel="manifest">` in
`_Layout.cshtml` `<head>`, and the one `kw-l` key `pwa.install` (× 4
languages — the closed-key registry + the parity test).

## Context (the locked set — from the design doc, not the register)

- **C-M10·1 (the manifest is honest + complete):** `name`, `short_name`,
  `start_url`, `display`, `background_color`, `theme_color`, `id`, `scope`,
  and the 192 + 512 icon pair (512 also `maskable`). `start_url`/`scope`
  are same-origin `/`. The design doc §manifest locks the exact values.
- **D3 (static + honest):** `display: "standalone"`; **no** `shortcuts`,
  `screenshots`, `categories`.
- **D4 (icon provenance):** source = the existing logo PNG; targets =
  `wwwroot/images/pwa/icon-192.png` + `icon-512.png`; generation = the
  design doc §icons' locked script path (a re-runnable Node script under
  `.tmp/` per the AGENTS.md scratch discipline, or a one-shot `sharp`
  invocation — the design doc names which).
- **D10 (zero Core change, one new key):** the only new closed-key is
  `pwa.install` (× 4 languages: en/de/fr/da); the closed-key registry +
  `KnownTranslationKeys_ParityTests` enforce. **Do not add any other
  key** (unit-series rule).

## Entry reads (5)

1. `docs/design/m10-pwa-responsive-design.md` — §manifest (the locked
   field values) + §icons (the locked provenance). **The authority.**
2. `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the `<head>` block;
   the `<link rel="manifest">` goes next to the existing `<link rel="icon">`.
3. `src/Kumunita.Web/wwwroot/css/site.css` — the `:root` tokens: copy
   the exact `--kumunita-*` canvas + primary hex for
   `background_color` / `theme_color` (the design doc already locked them
   in U00 — verify they match; if U00's locked values differ from the
   `:root` reality, that is a `## U01 — Drift pause`, not a silent fix).
4. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the
   closed-key registry; find where the per-language string tables live
   (the en/de/fr/da insertion points for `pwa.install`).
5. `src/Kumunita.Web/wwwroot/images/logo/` — list the existing logo PNGs
   (42×42 + 64×64 today); pick the source file the design doc §icons
   names (it will be the 64×64 or a larger master; if the design doc
   named a master that does not exist, that is a `## U01 — Drift pause`).

## Deliverables (5, ≤ 5 files)

- `src/Kumunita.Web/wwwroot/manifest.webmanifest` — **new.** The C-M10·1
  field set, the D3/D4 locked values. JSON, valid, no trailing comma.
- `src/Kumunita.Web/wwwroot/images/pwa/icon-192.png` — **new** (192×192,
  generated per D4; the generated file is the artifact).
- `src/Kumunita.Web/wwwroot/images/pwa/icon-512.png` — **new** (512×512,
  `purpose: "any"` + `maskable` in the manifest, generated per D4).
- `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — one `<link
  rel="manifest" href="~/manifest.webmanifest" />` added in `<head>`,
  next to the existing `<link rel="icon">`. **Do not touch any other
  line of this file** (unit-series rule).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — one new
  key `pwa.install`, all four languages. **Do not touch any other key**
  (unit-series rule + D10).

## Pinned tests (3) — `tests/Kumunita.Web.Tests/PwaManifestTests.cs` (NEW — U01 creates, U02/U06 extend)

- `Manifest_Json_Parses_And_Has_Required_Fields` — read
  `wwwroot/manifest.webmanifest`, parse as JSON, assert the C-M10·1 field
  set is present with the D3 locked values (the design doc's exact
  values, not a substring guess).
- `Manifest_Icon_192_And_512_Exist_In_Repo` — assert both PNG files exist
  at the exact manifest `icons` paths.
- `Layout_Contains_Manifest_Link` — the ADR 0043 SP-U04 string-pin idiom:
  read `_Layout.cshtml` as text, assert the `<link rel="manifest"`
  substring + the `manifest.webmanifest` path are present. **No
  TestServer, no web harness — a pure file/string pin** (the M3/M4
  author-not-run precedent is for the *behavioral* Playwright half; this
  xUnit half runs in-process per AGENTS.md).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green; then
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green with the 3 new pins passing.
- **Icon provenance check:** the two PNGs are actually 192×192 and
  512×512 (read the PNG header bytes 16–24 for the `IHDR` width/height —
  a one-liner in the test or a `node -e` one-shot; do not add an
  image-decode dependency to the test project).
- Handoff entry: the manifest's exact field values as written, the two
  icon files' byte sizes (the provenance witness), the `pwa.install`
  key's four language strings (verbatim), the `_Layout` insertion line,
  and any drift (a `site.css` `:root` token the design doc locked that
  U01 found different — a `## U01 — Drift pause`; a logo source file the
  design doc named that does not exist — same).
- **Last action:** move this unit's own plan file
  (`docs/plans-milestones/in-progress/pwa-responsive-u01.md`) to
  `docs/plans-milestones/done/` (a `git mv`).
