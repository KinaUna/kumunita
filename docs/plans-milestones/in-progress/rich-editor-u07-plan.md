# U7 — The parity + splice seam tests + the acceptance gate

- **Lane:** Rich editor (`RE`)
- **Unit:** U7 (of U0–U8)
- **Kind:** test (the lane's acceptance gate — the RE1–RE5 FACES, executable)

## Goal

Land the RE lane's **pinned seam tests** (RE1–RE5) as an executable
`Kumunita.Web.Tests` file, run the full `Kumunita.Web.Tests` suite via
`dotnet exec` (not `dotnet test`), and record the **acceptance gate**
(§Pinned seam tests / §Acceptance gate in the register) in the handoff note —
proving the toolbar's spliced Markdown renders the way RC's frozen renderer
does, and that the toolbar's image link is byte-identical to RC's `ImageIds`
parse.

## Environment reality (pinned — read before writing tests)

This repo's test stack is **xunit.v3 in .NET** (`Kumunita.Web.Tests.dll`,
run via `dotnet exec`); there is **no TS test runner** (no `vitest`/`jest`/
`node --test` in `package.json` — the `tsc`-only constraint, RE·3). So the
10 pinned RE behavior tests are implemented as a **C# spec mirror** of the
pure functions in the test file (the mirror *is* the executable pin of the
behavior the JS `rich-editor-core.ts` implements — the C# mirror and the TS
core encode the **same** pinned contract from the design doc, so the tests
anchor the client behavior without a JS runner). The client artifact itself
is pinned by a **file-existence + export-presence** test on the compiled
`wwwroot/js/lib/rich-editor.js`. **This is a test-harness choice, not a
second renderer** (RE·2's "one renderer" stance refers to the *read* path
— `MarkdownRenderer` — which is untouched; the preview is still U03's TS
`renderPreview`).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Web/client/lib/rich-editor.ts` + `rich-editor-core.ts`
   (U03) — the pure functions the C# mirror encodes + the `data-md` /
   `apply*` signatures the tests assert against.
2. `tests/Kumunita.Web.Tests/MarkdownRendererTests.cs` — the **7 pinned RC
   tests** (the parity baseline the RE tests assert against) + the test-file
   convention (xunit.v3, `[Fact]` / `[Theory]`, the assertion style).
3. `tests/Kumunita.Web.Tests/ContentImageUploadTests.cs` +
   `ContentImageServingTests.cs` — the RC `ImageIds` parse + the
   `/content-image` serving the `ImageLink_Is_RcByteIdentical` test pins
   against (the byte-identical claim).
4. `tests/Kumunita.Web.Tests/Kumunita.Web.Tests.csproj` — confirm the test
   project references `Kumunita.Web` (it does, per the RC lanes) + that
   `wwwroot/js/lib/rich-editor.js` is on disk post-`npm run build` (the
   artifact test's target).
5. `docs/plans-milestones/in-progress/rich-editor-handoff-notes.md` — the
   **`## U3`/`## U4`/`## U5`/`## U6`** sections (what shipped + the include
   decision + the core-split note) + the **`## U2`** mirror checklist (the
   marker set the parity asserts).

## Deliverables (closed set — 1 file)

1. **`tests/Kumunita.Web.Tests/RichEditorTests.cs`** (new) — the 10 pinned
   RE behavior tests (the register §Pinned seam tests, **exact names**):
   - `RenderPreview_BoldItalicCode_StillRender` — the C# mirror's
     `RenderPreview` of `**b** *i* ` + `` `c` `` → `<strong>`/`<em>`/`<code>`
     (RE·2 parity vs `MarkdownRenderer`).
   - `RenderPreview_HeadingsLists_StillRender` — `# H`, `- a`, `1. b` →
     `<h1>`/`<ul><li>`/`<ol><li>` (parity).
   - `RenderPreview_ImagePlatformRoute_RendersImgTag` —
     `![x](/content-image/deadbeef)` → one `<img class="rc-image">` (parity +
     the `src` allowlist accept).
   - `RenderPreview_ImageRemoteSrc_RendersAsPlainText` —
     `![x](https://evil/i.png)` → **no** `<img>`; escaped text (the client
     `IsSafeImageSrc` mirror rejects; RE·2; mirrors RC R·2).
   - `RenderPreview_HostileMarkup_StillEscaped` — `<script>`/`onerror=` →
     escaped, no raw tag survives (client R·2).
   - `ApplyToggle_Bold_WrapsSelection_AndPreservesCaret` —
     `ApplyToggle("hello world", (6,11), "bold")` → `"hello **world**"`,
     selection `(7,12)` (RE1/RE2 FACES; the toggle is a pure, caret-preserving
     splice).
   - `ApplyToggle_Bold_TogglesOff_WhenAlreadyBold` — selecting an already-
     bolded word + toggling removes the markers (the RE2 "clicking B again"
     FACES).
   - `ApplyBlock_H1_InsertsHeading_AtCaret` — `ApplyBlock("", 0, "h1")` →
     `"# "` (a block kind inserts its marker + a trailing space at the caret).
   - `ApplyLink_WrapsSelection_WithUrl` — `ApplyLink("hello", (0,5),
     "https://x")` → `"[hello](https://x)"` (the RE3 link FACES).
   - `ImageLink_Is_RcByteIdentical` — `ImageLink("fence","deadbeef")` ===
     `"![fence](/content-image/deadbeef)"` (RC R·3 byte-identity —
     `ContentImageIds.ExtractContentImageIds` picks it up unchanged).
   - Plus **one artifact pin**: `CompiledRichEditorJs_Exists_And_Exports` —
     `wwwroot/js/lib/rich-editor.js` (the `tsc` build output) exists on disk
     **and** contains the 5 exported function names + `bindRichEditor`
     (guards RE·3: the client artifact is built + the pure cores are the
     exported surface the C# mirror encodes).
   The C# mirror (`RenderPreview` / `ApplyToggle` / `ApplyBlock` / `ApplyLink`
   / `ImageLink`) is a **small internal static class** in the test file (≤ ~120
   lines) that **verbatim-encodes** the design doc's pinned behavior — it is
   the executable spec, not a second product renderer.

## Exit

- `dotnet build Kumunita.slnx -c Debug` **green** **and** `npm run build`
  green (the JS artifact the artifact-pin test reads is fresh).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  **all-green** (the 10 RE tests + the artifact pin + the inherited RC
  `MarkdownRendererTests` (7) + `ContentImageUploadTests` (4) +
  `ContentImageServingTests` (4) — the RE lane **regresses nothing** RC
  shipped).
- The **acceptance gate** is recorded in the handoff note (the register
  §Acceptance gate: closed-loop `apply*` + `renderPreview` produce the
  expected rendered tags; the handoff `imageLink` byte-identity; the
  part-vs-whole full-suite green).
- Append a `## U7 — Seam tests + gate` section (the `dotnet exec` run's
  pass/fail counts + the gate) **before** the folder move.
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the new test file + the handoff append.

## Notes / deviations

- **The 10 test names are pinned** (the design doc + this file name them
  exactly) — a rename is a `## U7 — Drift pause`, not a local edit.
- **The C# mirror must match the TS core.** If U03's `rich-editor-core.ts`
  and the C# mirror disagree on any marker/caret/allowlist detail, the
  **design doc is the record of truth** (RE·2) — fix whichever side drifted,
  and if the *TS core* was wrong, that's a U03 re-visit (`## U7 — Drift
  pause` naming the marker), not a silent mirror edit.
- **The artifact-pin test reads a build artifact** (`wwwroot/js/lib/
  rich-editor.js`) — it requires `npm run build` to have run (the Exit
  criterion orders it before the `dotnet exec` run). If the artifact is
  absent, the test fails loud (the RE·3 "the client artifact is built +
  exported" pin) — do **not** soften the assertion to a try/catch.
- **The image-gated surfaces** (U04 Post Edit, U05 Group Edit + both
  Announcement composers, U06 reply composers) are covered by these same
  **pattern** tests — they use the identical `rich-editor-core.ts` pure
  functions; the one difference (the **image button is gated off** via
  `data-rich-editor-no-image`) is a view/attribute concern, not a
  pure-function behavior, so it needs no extra test here.
- **Runner (AGENTS.md):** `dotnet build` + `dotnet exec`, **not**
  `dotnet test` / VS Test Explorer (the xunit.v3 discovery quirk on this
  machine). If `dotnet exec` reports "Zero tests ran / Exit code: 5", that's
  the known bug — the tests are not failing; re-run the assembly directly.
