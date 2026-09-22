# RC U02 — `MarkdownRenderer` image extension + the renderer tests

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: every entry read, deliverable, and exit criterion is
> named. The design doc (`docs/design/rich-content-design.md`, U01)
> is the **primary tier** — when this file and the design doc's
> §Pinned contract disagree on a shape, **the design doc wins and you
> record a `## U02 — Drift pause`** in the handoff note instead of
> silently picking one.

## Goal

Extend `src/Kumunita.Web/Security/MarkdownRenderer.cs` to render
`![alt](src)` images under invariant **R·2** (escape-first preserved;
the `src` allowlist **stricter** than the link whitelist), and author
`tests/Kumunita.Web.Tests/MarkdownRendererTests.cs` — the first test
file against a `Kumunita.Web.Security` type. **No other file's
behavior changes.** The about/terms/help pages pick up image support
the moment this lands (their `Body` already renders through
`RenderHtml` — see `Views/StaticPages/Page.cshtml`); U05 adds the
admin editor's image control, U06 switches the UGC surfaces.

## Entry reads (3 files, in order)

1. `src/Kumunita.Web/Security/MarkdownRenderer.cs` — **full** (≈ 230
   lines). The extension target. Study: the block loop (fences,
   headings, lists, paragraphs), `Inline()` (the link-extraction-first
   pass + `InlineText`'s escape-then-rules order), `IsSafeUrl`,
   `HtmlEscape`, and the class doc-comment's "intentionally out of
   scope" sentence (tables, images, raw HTML).
2. `docs/design/rich-content-design.md` — §Invariants (R·2, R·3) +
   §Pinned contract (the `IsSafeImageSrc` shape + the exact `<img>`
   emission) + §Pinned seam tests (the 7 names for this file).
3. `tests/Kumunita.Web.Tests/AnnouncementControllerTests.cs` — the
   first ~80 lines: the Web-test file conventions (xunit.v3 `[Fact]`,
   the class doc-comment's "pins this harness owns" style).

## Deliverables (2 files)

### 1. `src/Kumunita.Web/Security/MarkdownRenderer.cs` (modify)

**Add the image pass to `Inline()`** — the design doc's pinned shape:
image extraction happens **before** the link pass (an image link is a
link-shaped match the link regex would otherwise partially consume —
`![alt](src)` contains `[alt](src)`; extracting images first and
advancing `lastEnd` past them is the correct order, mirroring how the
link pass handles text between links).

- `private static bool IsSafeImageSrc(string src)` — the pinned
  predicate, two accept branches:
  1. `src` **starts with** `/content-image/` (ordinal) and the
     remainder is **1–128 lowercase hex chars** (`[0-9a-f]`) — the
     platform route shape. Nothing after the id (no `?query`, no
     trailing `/` — the route is exact).
  2. a relative path: no `:`, no leading `//`, and no whitespace —
     defensive (a body is author- or admin-authored, but the renderer
     should not assume the idiom is the only producer).
  - **Everything else rejects** — any scheme (`http:`, `https:`,
    `data:`, `javascript:` — a scheme is the presence of `:` before
    the first `/`), a malformed id, an empty string.
- On accept: emit `<img src="{escUrl}" alt="{escAlt}"
  class="rc-image" loading="lazy" />` — `escUrl` / `escAlt` use the
  **same** attribute-escaping the link emission uses (`HtmlEscape` +
  `&` → `&amp;` + `"` → `&quot;` on the URL; `HtmlEscape` on the
  alt). `alt` is the raw label text (it may contain Markdown that
  `InlineText` would have processed — but inside an `alt` attribute
  that would produce attribute-breaking characters, so **alt is
  escaped verbatim, no inline rules** — record this in the
  handoff note as a deliberate simplification).
- On reject: emit the **whole** `![alt](src)` as **plain escaped
  text** via `InlineText(...)` (the label's inline rules still apply
  to the *visible* text; the URL is escaped, not rendered — the
  `IsSafeUrl`-reject precedent in the link pass, verbatim pattern).
- **Amend the class doc-comment's "intentionally out of scope"
  sentence:** images leave the out-of-scope list (they are now
  in scope, R·1); tables, footnotes, and raw HTML stay out. This is
  the **only** permitted doc-comment edit in this unit — it is inside
  the drift guard's scope (R·1 names this renderer as the single
  engine; this is its extension, not a second engine).

### 2. `tests/Kumunita.Web.Tests/MarkdownRendererTests.cs` (new)

The **7 pinned tests** (the design doc's names are authoritative —
if a name below differs from the design doc, the design doc wins +
drift pause). Test style: pure-function tests — call
`MarkdownRenderer.RenderHtml(input)` and assert on the output string
(contains / not-contains). No DI, no fixture.

1. `Image_PlatformRouteSrc_RendersImgTag` — input `![fence](/content-image/deadbeefcafe)` →
   output contains exactly one `<img`, with `src="/content-image/deadbeefcafe"`,
   `alt="fence"`, `class="rc-image"`, `loading="lazy"`.
2. `Image_RemoteSrc_RendersAsPlainText` — input
   `![x](https://evil.example/img.png)` → output contains **no**
   `<img`; the literal `https://evil.example/img.png` appears
   **escaped** (`&` → `&amp;` if present — assert the scheme text is
   present as plain text and no `src=` attribute exists).
3. `Image_DataUriSrc_RendersAsPlainText` — input
   `![x](data:image/png;base64,AAA)` → same assertions as (2).
4. `Image_MalformedHexId_RendersAsPlainText` — input
   `![x](/content-image/notahexid)` → no `<img`; also
   `![x](/content-image/)` (empty id) → no `<img>` (a second assert
   in the same test or fold into (2)'s pattern — the design doc's 7
   names are the count; one test per name).
5. `Image_InlineInParagraph_StaysInParagraph` — input
   `Hello ![pic](/content-image/ab) world` → the `<img` appears
   **inside** a single `<p>…</p>` (not a new block element; the
   paragraph's open tag precedes the `<img` and its close tag
   follows).
6. `Bold_Italic_List_Heading_StillRender` — **the regression pin**
   (R·1/R·8): one input exercising `**bold**`, `*italic*`, a `-`
   list item, a `## ` heading, and a `` `code` `` span → assert each
   of `<strong>`, `<em>`, `<li>`, `<h2>`, `<code>` appears. This
   test must pass **unchanged by semantics** — it proves the
   extension did not regress the existing subset.
7. `HostileMarkup_StillEscaped` (R·2) — inputs
   `<script>alert(1)</script>` in a paragraph, and
   `![onerror="x"](/content-image/ab)` → assert no raw `<script`
   survives (it is `&lt;script`), and no `onerror=` **attribute** is
   emitted on the `<img` (the alt is escaped verbatim; the string
   `onerror=` may appear **inside** the escaped alt value as text —
   assert specifically that the emitted `<img` tag has no
   `onerror=` between its `<img` and `>` outside the alt attribute…
   the simplest robust assertion: the output contains no substring
   `onerror=` **followed by** `"` at an attribute position — if this
   is over-precise, assert the alt is fully `HtmlEscape`d and move
   on; record the final assertion shape in the handoff note).

## Exit criteria

- `dotnet build` (the `build` task) green for `Kumunita.Web`.
- The 7 tests exist with the pinned names. **Run them** via the
  in-process xunit.v3 path from `AGENTS.md` (build, then
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  filtered, or just run the whole assembly — it is fast) and record
  the pass count. **The `run_tests` / VS Test Explorer "No tests
  found" quirk is a known runner bug — do not retry it, do not "fix"
  the tests** (AGENTS.md §Running the tests).
- **No other source file modified.** `npm run build` is not required
  (no TS touched) — record "not-applicable" in the handoff note.
- **Handoff note** (append to
  `docs/plans-milestones/done/rich-content-handoff-notes.md`): a
  section starting `## U02 — renderer image extension`, 5–6 lines:
  (a) the `IsSafeImageSrc` predicate (the 2 accept branches, one line
  each); (b) the exact `<img>` attribute set emitted; (c) the 7 test
  names + pass count (e.g. "7/7 green"); (d) the alt-is-escaped-
  verbatim simplification (one line); (e) the doc-comment amendment
  (images in, tables/footnotes/raw-HTML out); (f) any drift pause.
- **Then move this file:** `Move-Item
  docs\plans-milestones\in-progress\rich-content-u02-plan.md
  docs\plans-milestones\done\rich-content-u02-plan.md` — the move is
  the last action of the unit.
