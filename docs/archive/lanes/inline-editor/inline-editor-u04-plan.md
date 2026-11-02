# U4 — The parity + toggle seam tests + the acceptance gate

- **Lane:** Inline editor (`IE`)
- **Unit:** U4 (of U0–U5)
- **Kind:** test (the lane's acceptance gate — the IE FACES, executable)

## Goal

Land the IE lane's **pinned seam tests** (the 3 + the 1 artifact pin)
as an executable `Kumunita.Web.Tests` file, run the full
`Kumunita.Web.Tests` **and** `Kumunita.Core.Tests` suites via
`dotnet exec` (not `dotnet test`), and record the **acceptance gate**
in the handoff note — proving the toggle is in the compiled artifact,
the hidden-state class is emitted, the textarea is **not**
disabled/removed (the RE·1 single-source-of-truth pin at the artifact
level), and the RE pure-function surface is **byte-identical**
(no regression).

## Environment reality (pinned — read before writing tests)

This repo's test stack is **xunit.v3 in .NET** (`Kumunita.Web.Tests.dll`
/ `Kumunita.Core.Tests.dll`, run via `dotnet exec`); there is **no TS
test runner** (the `tsc`-only constraint, RE·3). The IE tests are a
**C# spec mirror** of the toggle's artifact contract (the pin is: the
`data-ie-toggle` attribute + the `rc-editor-source-hidden` class are
present in the compiled JS, the textarea is **not** disabled/removed,
and the RE pure functions are still exported) + a **file-existence +
export-presence** test on the compiled `wwwroot/js/lib/rich-editor.js`
(the same shape as RE U07's `CompiledRichEditorJs_Exists_And_Exports`).
**This is a test-harness choice, not a second renderer** (RE·2's "one
renderer" stance refers to the *read* path — `MarkdownRenderer` —
which is untouched).

**The test-runner quirk (AGENTS.md §Running the tests):** `dotnet test`
/ VS Test Explorer are **not** the reliable path on this machine. The
reliable path is:

```powershell
dotnet build Kumunita.slnx -c Debug
npm run build   # in src/Kumunita.Web — the JS artifact the tests read
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

(`Kumunita.Core.Tests` takes ~20 s — Testcontainers/postgres — and
leaves Docker containers behind if killed; `docker container prune`
if needed.)

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Web/client/lib/rich-editor.ts` (U1) — the binder
   extension (the `data-ie-toggle` selector + the
   `rc-editor-source-hidden` class + the `setView` function) — the
   strings the artifact pin asserts.
2. `tests/Kumunita.Web.Tests/RichEditorTests.cs` (RE U07) — the **10
   pinned RE tests** + the `CompiledRichEditorJs_Exists_And_Exports`
   artifact pin (the test-file convention: xunit.v3 `[Fact]`, the
   `File.ReadAllText` shape on `wwwroot/js/lib/rich-editor.js`, the
   assertion style) — the **template U4 mirrors**.
3. `docs/design/inline-editor-design.md` — the **pinned seam-test
   names** (the 3 + the 1 artifact pin) + the **acceptance gate**.
4. `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md` —
   the **`## U1`** / **`## U2`** / **`## U3`** sections (what shipped:
   the binder's toggle block, the 15 buttons, the 2 registered keys —
   the strings the tests assert).
5. `src/Kumunita.Web/wwwroot/js/lib/rich-editor.js` — the **compiled**
   output (confirm the `data-ie-toggle` + `rc-editor-source-hidden`
   strings are present post-`npm run build`; if **absent**, the U1
   build did not emit them — a `## U4 — Drift pause`, not a silent
   test-skip).

## Deliverables (closed set — 1 file)

- **`tests/Kumunita.Web.Tests/InlineEditorTests.cs`** (new) — the 3
  pinned IE artifact tests + the 1 RE-regression artifact pin (the design
  doc §Pinned seam tests, **exact names**). All four are
  **artifact-string pins**: each resolves the compiled JS via the same
  `CandidateArtifactPaths()` idiom RE U07 uses (a small private static
  helper in this file — **mirror `RichEditorTests.CandidateArtifactPaths()`**
  verbatim: walk up ≤ 8 levels from **both** `Directory.GetCurrentDirectory()`
  and `AppContext.BaseDirectory`, relative path
  `src/Kumunita.Web/wwwroot/js/lib/rich-editor.js`, pick the first that
  `File.Exists`) and then asserts string presence/absence on the content
  via `Assert.Contains(needle, content, StringComparison.Ordinal)` /
  `Assert.DoesNotContain(...)`:
  - `CompiledRichEditorJs_ContainsToggleButton` — content **contains**
    `data-ie-toggle` (the U1 binder's `querySelector("button[data-ie-toggle]")`
    selector is emitted in the compiled artifact — proof the toggle wiring
    shipped). **Fails loud** if the artifact is absent (mirror RE U07's
    `Assert.True(artifact is not null, ...)`; do **not** soften to
    try/catch).
  - `CompiledRichEditorJs_ContainsSourceHiddenClass` — content **contains**
    `rc-editor-source-hidden` (the U1 initial-state class is emitted —
    the `setView(false)` initial call + the `setView` function body both
    reference it, so the compiled JS must carry the literal string).
  - `RichEditorTextarea_IsNotDisabled_OrRemoved` — content does **not**
    contain the exact `tsc`-emitted disable/remove patterns (the IE·1 /
    RE·1 pin at the artifact level: the textarea is hidden via a **CSS
    class** — `classList` — never disabled/removed/`hidden`-attributed).
    Phrase the absence-needles against U1's actual compiled form (read
    `rich-editor.js` first; e.g. `textarea.disabled`, `textarea.remove()`,
    `textarea.hidden`) and record the exact needles in the handoff note.
  - `CompiledRichEditorJs_StillExportsRePureFunctions` — content **still
    contains** `export function renderPreview`, `export function
    applyToggle`, `export function applyBlock`, `export function
    applyLink`, `export function imageLink`, `export function
    isSafeImageSrc`, **and** `export function bindRichEditor` (the
    **same 7-name list** and the **same `export function {name}` needle
    shape** RE U07's `CompiledRichEditorJs_Exists_And_Exports` asserts —
    a byte-level regression guard: if U1 dropped or renamed a pure
    function, this fails **before** the user does. Do **not** shorten
    the list — it must be the full 7 RE U07 asserts, so the IE lane is
    provably **additive**).

  **`InlineEditorTests.cs` has *no* `RichEditorSpec`-style mirror.** RE
  U07's mirror re-encodes the 6 pure functions' *behavior*; IE adds
  **no** pure functions (U1 only extends `bindRichEditor`'s DOM
  behavior, which is not spec-able without a JS runner). So this file
  is exactly: the 4 `[Fact]` artifact-string tests + the one private
  `CandidateArtifactPaths()` helper. **Do not** re-encode RE behavior
  here (RE U07 already owns that and still passes).

## Exit

- `dotnet build Kumunita.slnx -c Debug` **green** **and** `npm run
  build` **green** (the JS artifact the tests read is fresh — a stale
  `rich-editor.js` would make the 4 tests pass or fail against the
  **wrong** build; the `npm run build` step is part of the gate).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  **green** — the 4 new IE tests **plus** the 10 existing RE tests
  **plus** the existing RC/M3/etc. tests all pass (**no regression** —
  the RE U07 `CompiledRichEditorJs_Exists_And_Exports` test still
  passes against the U1-extended module: its 5-export assertion is a
  **subset** of U4's 6-export assertion, so both are consistent).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  **green** (the U3 `KnownTranslationKeys.cs` change — if a
  key-registry completeness test exists, it passes; if the two new
  keys broke a "closed set" assertion, this run surfaces it — a
  `## U4 — Drift pause` naming the failing test + the fix is the
  U3/U4 boundary, resolved in U4's section).
- Handoff note: 5–6 lines starting `## U4 — Seam tests + gate` — (a)
  the 3 IE test names + the artifact pin name, (b) the `dotnet exec`
  exit codes for **both** test projects (the **acceptance gate**:
  both 0), (c) confirmation the 10 existing RE tests still pass (no
  regression), (d) the `npm run build` timestamp (the artifact
  freshness), (e) any test that needed a deviation from the design
  doc's pin (name the test + the deviation + why).
- Move this plan file `in-progress/` → `done/` (**last**).

## Notes / deviations

- **The artifact-string pin is a *contract* pin, not a behavior
  test.** It cannot execute the toggle (no JS runner — RE·3). The
  behavior is pinned by the design doc's toggle contract + the code
  review of U1's binder. If the lane later wants an executable
  behavior test (e.g. a headless-browser test that clicks the toggle
  and asserts the class), that is a **future lane** (it would need a
  JS test runner — a `package.json` change, which RE·3 forbids in
  this lane).
- **The path to `rich-editor.js` is the load-bearing detail.** Mirror
  `RichEditorTests.CandidateArtifactPaths()` **exactly** (walk up ≤ 8
  levels from both `Directory.GetCurrentDirectory()` and
  `AppContext.BaseDirectory`; relative path
  `src/Kumunita.Web/wwwroot/js/lib/rich-editor.js`; pick the first
  `File.Exists`; assert `artifact is not null` with a loud failure
  listing the searched paths). If U4's first run fails with an
  artifact-not-found assertion, the helper is wrong — copy RE U07's
  helper verbatim, don't invent a new idiom. Record the working
  resolution in the handoff note so a future agent does not re-derive it.
- **The 4 tests are the *closed* IE test set.** Do **not** add a 5th
  (e.g. a "value of the two `<kw-l>` keys" test — that is a
  `Kumunita.Core.Tests` localization concern, not an IE behavior
  concern; or a behavior test that needs a JS runner). A 5th is a
  `## U4 — Drift pause`, not a silent addition.
- **`Assert.DoesNotContain` needles must be the *exact* `tsc`-emitted
  form and *scoped* to the textarea.** A needle that is too loose
  (e.g. bare `disabled`) could false-positive on an unrelated `disabled`
  elsewhere in the ~700-line module; scope with the `textarea.` prefix
  (e.g. `textarea.disabled`, `textarea.remove()`, `textarea.hidden`).
  Read U1's compiled `rich-editor.js` to confirm these substrings are
  genuinely **absent**, and record the exact needles in the handoff note.
