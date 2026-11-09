---
id: is38c-prompt
title: "IS38c prompt — extract the Document folder-picker to a tsc module, and land the inline-script-block gate (E14)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS38c — Document folder-picker extraction + the inline-script-block gate (E14)

## What you are doing

You are the **fortieth working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS38 are committed
(IS38 = `dda2dd9e`).

**Finding E14** (recorded in the IS38 handoff as a *new* finding, not in the
E9 baseline) is the **2 live inline `<script type="module">` blocks** that
IS38's gate detected but could not ship green on:

| View | block | what it does |
|---|---|---|
| `src/Kumunita.Web/Views/Document/New.cshtml` :246 | `<script type="module">` | `fetch('/documents/folders')` and append each folder to the `<select id="folder">` |
| `src/Kumunita.Web/Views/Document/Edit.cshtml` :245 | `<script type="module">` | same, **plus** it restores `select.value` after populating (the doc's current folder is pre-selected server-side) |

Both are **silently blocked by `script-src 'self'`** (CSP :1083, no
`'unsafe-inline'`) — so the folder `<select>` **never populates** (a real
latent bug: the folder picker is broken in both views). The fix is to extract
the shared logic to a tsc-built module + a declarative `data-*` attribute
(the `reset-form.ts` / `confirm.ts` / `inline-free.ts` precedent), then
**un-skip** the `NoView_HasAnInlineScriptBlock` gate so it lands green (2 → 0).

The **IS38 gate** (`tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs`)
already exists. `NoView_HasAnInlineEventHandler` is **green**.
`NoView_HasAnInlineScriptBlock` is written but
`[Fact(…, Skip = "IS38c: …")]`. **IS38c's one test job is to remove that
`Skip`** — after the 2 blocks are extracted, the test must **pass** (0
inline executable script blocks remain).

**No CSP change. No `confirm.ts` / `reset-form.ts` / `inline-free.ts` edit.
No new `application/json` data blocks.** The only new executable surface is
one tsc module + one global include.

## The one thing to get right

**One new tsc ES module** (`client/lib/*.ts` → `wwwroot/js/lib/*.js`,
`rootDir: client`, `outDir: wwwroot/js`, `strict: true`, `module: ES2022`,
`lib: [ES2022, DOM, DOM.Iterable]`), **two view swaps**, **one include line**,
**one one-line gate un-skip**. The module is a single self-contained IIFE that
handles **both** views with one code path (Edit's "restore the current value"
is a strict superset of New's — capturing `select.value` first and restoring
it if the option still exists is a no-op for New). After the swap, neither
view carries an inline executable `<script>`; the gate's `InlineScriptTag`
regex (no `src=`, non-empty body, not `application/json`) sees **0** and the
test goes green.

## The contract (exact)

### `client/lib/document-folder-picker.ts` (ONE file)

Declarative seam: a `<select data-document-folder-picker>` (the "Unfiled"
default option is already server-rendered — the module only appends the
dynamic folders). The `GET /documents/folders` endpoint
(`DocumentFolderController.Index`) returns
`{ folders: [{ Id, string Name, string? ParentId }, …] }` (a **flat** list,
children after roots). Match the inline blocks exactly: 2-space indent for
`ParentId` folders, `option.value = Id`, `option.textContent = depth + Name`.

```ts
/**
 * Document folder picker (SECURITY.md §6 no-inline-script rule; IS38c / E14).
 * Replaces the 2 inline <script type="module"> blocks that
 * Document/New.cshtml + Document/Edit.cshtml carried — both were silently
 * blocked by `script-src 'self'` (the folder <select> never populated).
 * Declarative seam: a <select data-document-folder-picker> (the "Unfiled"
 * default is already server-rendered). This module finds every such select,
 * fetches the flat folder tree from GET /documents/folders
 * (DocumentFolderController.Index — { folders: [{ Id, Name, ParentId }] }),
 * appends the folders as options (2-space indent for children, matching the
 * old inline blocks), and restores the select's pre-populated value (the Edit
 * view pre-selects the document's folder server-side; for New this is a no-op
 * because the default option is still present). The fetch runs after the
 * element is in the DOM: _LayoutScripts.cshtml renders after </body>, the
 * same assumption as confirm.ts / reset-form.ts / inline-free.ts.
 */
(() => {
  'use strict';
  for (const select of document.querySelectorAll<HTMLSelectElement>('select[data-document-folder-picker]')) {
    const current = select.value;
    fetch('/documents/folders', { credentials: 'same-origin' })
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => {
        if (!data || !Array.isArray(data.folders)) return;   // leave the "Unfiled" option only
        for (const f of data.folders) {
          const opt = document.createElement('option');
          opt.value = f.Id;
          const depth = f.ParentId ? '  ' : '';
          opt.textContent = depth + f.Name;
          select.appendChild(opt);
        }
        if (current && Array.from(select.options).some((o) => o.value === current)) {
          select.value = current;   // restore the Edit view's server-selected folder
        }
      })
      .catch(() => { /* leave the "Unfiled" option only */ });
  }
})();
```

### The two view swaps (pure Razor, no logic)

**`Document/New.cshtml`** — the `<select>` is at **:190**; the inline block at
**:245–262** (the `@section Scripts` block, after the `tag-suggest.js`
include):

```diff
-                        <select class="form-control" id="folder" name="FolderId">
+                        <select class="form-control" id="folder" name="FolderId" data-document-folder-picker>
```

and delete the whole inline block (and its Razor comment), e.g.:

```diff
     @* The folder picker — a small client-side script that fetches the
-       folder tree from /documents/folders and populates the <select> … *@
-    <script type="module">
-        (async () => {
-            const select = document.getElementById('folder');
-            …
-        })();
-    </script>
+       folder tree (the PostController / PageController "seed the picker"
+       idiom — the tree is dynamic). IS38c / E14: the picker now lives in the
+       tsc-built document-folder-picker.ts module (data-document-folder-picker
+       on the <select> above); the inline <script> it replaced was silently
+       blocked by script-src 'self'. *@
```

**`Document/Edit.cshtml`** — the `<select>` is at **:183**; the inline block at
**:244–262** (identical treatment; its block additionally restores
`select.value`, which the module's `current` restore covers). **Keep the
`tag-suggest.js` include and its Razor comment untouched** in both views —
they are out of scope.

### `_LayoutScripts.cshtml` — one include line

After the IS38 `inline-free.js` include (**:27**), in the same style as the
neighbouring modules:

```html
<!-- Document folder picker (SECURITY.md §6; IS38c / E14 — the 2 inline
     <script> blocks in Document/New + Document/Edit, which CSP was blocking) -->
<script type="module" src="~/js/lib/document-folder-picker.js"></script>
```

### `CspInlineDisciplineGateTests.cs` — the one-line un-skip (IS38c-b)

The `NoView_HasAnInlineScriptBlock` method currently carries
`Skip = "IS38c: …"` on its `[Fact]`. **Delete the `Skip = "…"` argument** so
the test runs; it must now **pass** (the 2 E14 blocks are gone, so
`flagged.Count == 0` and the method returns before the `Assert`). Also trim
the now-stale "IS38 ships it skipped" wording in the method body's comment
(the `if (flagged.Count == 0) return;` early-return stays). **Do not touch the
`E14DocumentBlocks` field, the handler test, or the allowlist (still empty).**

## Constraints

- **One new TS file** (`document-folder-picker.ts`). No split, no helpers.
- **One new script include** (`document-folder-picker.js`), in
  `_LayoutScripts.cshtml`. No per-view `<script src>` added (the existing
  `tag-suggest.js` include stays).
- **No CSP change.** `script-src 'self'` stays.
- **No `confirm.ts` / `reset-form.ts` / `inline-free.ts` logic edit.**
  **No `_Layout.cshtml` change.** **No controller/endpoint change**
  (the `GET /documents/folders` shape is fixed; the module consumes it as-is).
- **The gate allowlist stays empty** (IS08 doctrine) — the 2 blocks are *fixed*,
  not allowlisted.
- `wwwroot/js/` is gitignored (build output) — only `client/lib/*.ts` is
  committed; `npm run build` regenerates the `.js`.

## Deliverables (5 files = IS38c 4 + IS38c-b 1)

| # | File | Change |
|---|---|---|
| 1 | `src/Kumunita.Web/client/lib/document-folder-picker.ts` | **new** — the folder-picker IIFE |
| 2 | `src/Kumunita.Web/Views/Document/New.cshtml` | `data-document-folder-picker` on the `<select>` (:190) + delete the inline block |
| 3 | `src/Kumunita.Web/Views/Document/Edit.cshtml` | `data-document-folder-picker` on the `<select>` (:183) + delete the inline block |
| 4 | `src/Kumunita.Web/Views/Shared/_LayoutScripts.cshtml` | +1 `<script>` include line (after :27) |
| 5 | `tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs` | **IS38c-b** — remove the `Skip = "…"` from `NoView_HasAnInlineScriptBlock` so it runs green (2 → 0) |

## Entry reads (only these — no whole-file reads)

- `src/Kumunita.Web/Views/Document/New.cshtml` lines 185–265 (the `<select>` +
  the `@section Scripts` inline block).
- `src/Kumunita.Web/Views/Document/Edit.cshtml` lines 178–263 (same; note the
  `select.value` restore).
- `src/Kumunita.Web/client/lib/inline-free.ts` (whole — the IIFE +
  `querySelectorAll` + module-include precedent IS38 just set).
- `src/Kumunita.Web/client/lib/reset-form.ts` (whole — the closest analog:
  wire a declarative attribute on a form element).
- `src/Kumunita.Web/Views/Shared/_LayoutScripts.cshtml` lines 22–30 (the
  include block, for the new include's position + style).
- `src/Kumunita.Web/Controllers/DocumentFolderController.cs` lines 44–76
  (the `GET /documents/folders` response shape the module must consume).
- `src/Kumunita.Web/tsconfig.json` (whole — `rootDir`, `outDir`, `strict`,
  `lib`, `module`).
- `tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs` lines
  130–185 (the `NoView_HasAnInlineScriptBlock` method + the un-skip target).
- The D8 per-unit contract: `docs/plans-milestones/plan-improve-sec.md`
  (the **IS38** section + the **D8** row + the **DoD-8/P-8** row).
- Test-runner form (SEC U3 — xunit.v3 in-process `-class`, **not**
  `dotnet test --filter`): `docs/archive/lanes/sec/sec-handoff-notes.md`.

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `tsc` green (`cd src/Kumunita.Web && npm run build`);
      `wwwroot/js/lib/document-folder-picker.js` emitted (gitignored).
- [ ] `grep -rn "<script type=\"module\">" src/Kumunita.Web/Views` →
      **0 hits** (the 2 Document blocks are gone; the `tag-suggest.js`
      `src=` include is a different form and does not match).
- [ ] `grep -rn "data-document-folder-picker" src/Kumunita.Web/Views` →
      **2 hits** (New + Edit `<select>`).
- [ ] `_LayoutScripts.cshtml` carries the
      `<script type="module" src="~/js/lib/document-folder-picker.js"></script>`
      line.
- [ ] **`NoView_HasAnInlineScriptBlock` runs and is GREEN** (2 → 0) —
      `dotnet exec Kumunita.Web.Tests.dll -class
      "Kumunita.Web.Tests.SecGate.CspInlineDisciplineGateTests"` →
      **Total: 2, Failed: 0, Skipped: 0**.
- [ ] `client/lib/confirm.ts`, `reset-form.ts`, `inline-free.ts`,
      `_Layout.cshtml`, the 2 `application/json` data blocks, and
      `DocumentFolderController.cs` are **untouched**.
- [ ] Full test suite green outside the pre-existing baseline (the 3
      pre-existing failures — `DataClassCoverageTests` ×2 +
      `ImproveHarnessTests.CodeCheck_Gate_Passes` — are the IS31/IS32/IS24
      drift, unchanged by IS38c).
- [ ] **Live check (optional but recommended):** start the app
      (`dotnet run --project src/Kumunita.Web --launch-profile http`), open a
      Document **New** and **Edit** page, and confirm the folder `<select>`
      now populates (previously empty) and that on Edit the document's current
      folder stays selected.
- [ ] One `## IS38c` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`.

## Definition of done (IS38c)

- [ ] The 2 E14 inline `<script type="module">` blocks are gone; each
      `<select>` carries `data-document-folder-picker`; the shared logic lives
      in one tsc module; the include is global.
- [ ] No new TS beyond `document-folder-picker.ts`; no CSP change; no
      controller change; no `inline-free.ts` / `confirm.ts` / `reset-form.ts`
      edit; no `_Layout.cshtml` change; no `application/json` data block added.
- [ ] Build green; `tsc` green; **`NoView_HasAnInlineScriptBlock` green
      (2 → 0)** (no longer skipped); `NoView_HasAnInlineEventHandler` still
      green (0); allowlist still empty.
- [ ] One commit (the 5 deliverables + the handoff section); the prompt file
      is **not** part of the unit commit (lane convention, the IS38 precedent
      `dda2dd9e`).
- [ ] **Next unit: IS39** (control-map rewrite — traceability;
      `docs/security/control-map.md` gets the columns **Control · Answers ·
      Seam · Pinned by · ADR**; the stamp row moves to
      `RevocationStampMiddleware` + `PrivilegedStampMiddleware`; new rows for
      data classes, erasure, retention, operator-gated export,
      decision-free-read markers, log redaction + Sentry floor, CSP discipline,
      admin-read audit; every code row names an existing test — IS40 then
      gates the traceability, and may now reference the
      `NoView_HasAnInlineScriptBlock` gate as the CSP-discipline pin).
