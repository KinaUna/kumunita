# U3 — The serializer + sanitizer + its unit tests (the load-bearing artifact)

- **Lane:** WYSIWYG inline editing (`WY`)
- **Unit:** U3 (of U0–U9)
- **Kind:** code (the first code unit; everything after U3 builds on this)

## Goal

Implement the **pure serializer** (`toMarkdown`) and the **pure
sanitizer** (`sanitizeHtml`) in `client/lib/dom-to-markdown.ts`, and
author the **9 pure-function tests** (WY1–WY6) in
`tests/Kumunita.Web.Tests/WysiwygEditorTests.cs`. **No** binder
wiring (that is U4); **no** toolbar rework (that is U5); **no**
paste handler (that is U6); **no** code-view rework (that is U7).
This unit is the **load-bearing** artifact — U4–U7 all import from
it.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/wysiwyg-editor-design.md` §2.3 (the serializer
   contract — the **primary** source: the subset, the mapping,
   the escape rule, the edge cases, the round-trip property) +
   §2.4 (the sanitizer contract — the reject list) + §2.7 (the
   17 pinned test names — the 9 pure-function tests this unit
   authors).
2. `src/Kumunita.Web/client/lib/rich-editor.ts` — the
   `renderPreview` function (the **inverse** the serializer must be)
   + `htmlEscape` / `isSafeImageSrc` / `isSafeUrl` (the construction
   to reuse) + the 6 pure functions (the pattern the serializer
   follows).
3. `src/Kumunita.Web/Security/MarkdownRenderer.cs` — the C# renderer
   (the **reference of record** for the subset the serializer must
   emit; the `FullSrcRe` regex the serializer must stay byte-
   compatible with).
4. `tests/Kumunita.Web.Tests/InlineEditorTests.cs` — the
   artifact-string pin pattern (the `CompiledRichEditorJs_*` test
   shape the 8 artifact-string tests in U4–U7 follow).
5. `tests/Kumunita.Web.Tests/RichEditorTests.cs` — the
   pure-function test pattern (the `renderPreview` / `applyToggle`
   / `applyBlock` test shape the 9 pure-function tests follow).

## Deliverables (2 files, new)

1. **`src/Kumunita.Web/client/lib/dom-to-markdown.ts`** (~180 lines).
   **Two** pure functions (no DOM, no side effects, no self-wire —
   the binder imports them):
   - `toMarkdown(html: string): string` — the **serializer**
     (the inverse of `renderPreview`). **The subset** (WY·3): P,
     H1–H6, UL/OL, LI, STRONG, EM, CODE, A, IMG, PRE/CODE — and
     **nothing more**. **The mapping** (each element → its
     Markdown form):
     - `<p>` → inline content + `\n\n`
     - `<h1>`–`<h6>` → `#`–`######` + ` ` + inline content + `\n\n`
     - `<ul>` → `- ` per `<li>` + `\n\n`; `<ol>` → `1. ` / `2. ` /
       `3. ` per `<li>` + `\n\n`
     - `<li>` → inline content (no nesting — WY·3)
     - `<strong>` → `**` + inline + `**`
     - `<em>` → `*` + inline + `*`
     - `<code>` → `` ` `` + text + `` ` ``
     - `<a href="…">` → `[` + inline + `](` + href + `)`
     - `<img src="/content-image/{id}" alt="…">` → `![` + alt +
       `](` + src + `)`
     - `<pre><code class="language-{lang}">` → ` ```{lang}` + `\n`
       + text + `\n` + ` ``` ` + `\n\n`
     - **The escape rule** (the inverse of `htmlEscape`): `&amp;`
       → `&`, `&lt;` → `<`, `&gt;` → `>`, `&quot;` → `"`, `&#39;`
       → `'`.
     - **The edge cases** (WY·3): blank `<p></p>` → `""`; blank
       `<h1></h1>` → `""`; empty `<ul></ul>` → `""`; `<a>label</a>`
       (no href) → `label` (plain text); `<img src="unsafe">`
       (the `isSafeImageSrc` reject) → the escaped alt text;
       `<a href="unsafe">` (the `isSafeUrl` reject) → the label as
       plain text; `<pre><code>` (no language) → ` ``` ` + `\n` +
       text + `\n` + ` ``` `.
     - **The round-trip property** (WY·10 / WY5): for any `md` in
       the RC-pinned subset, `toMarkdown(renderPreview(md)) === md`.
   - `sanitizeHtml(html: string): string` — the **sanitizer** (the
     WY·6 invariant: strip pasted HTML to the WY·3 subset). **The
     reject list:** SPAN, DIV, TABLE, TR, TD, SCRIPT, IFRAME,
     OBJECT, EMBED, FORM, INPUT, BUTTON, every `on*` event-handler,
     every `style`, every `class` (except `language-{lang}` on
     `<code>`), every `id`, every unsafe `href` (the `isSafeUrl`
     reject), every unsafe `src` (the `isSafeImageSrc` reject).
     **The construction:** a single pass over the HTML (a
     regex-based strip, the same construction as `htmlEscape` /
     `isSafeImageSrc` / `isSafeUrl` already in `rich-editor.ts`).
     **The output:** a string of HTML that contains **only** the
     WY·3 subset.
2. **`tests/Kumunita.Web.Tests/WysiwygEditorTests.cs`** (~120 lines,
   the **9 pure-function tests** only — the 8 artifact-string
   tests are added by U4–U7). Test names (from §2.7):
   1. `WY10_RoundTrip_BoldHeadingListLinkImageCode`
   2. `WY3_Serializer_EmitsOnlyThePinnedSubset`
   3. `WY3_Serializer_SkipsBlankElements`
   4. `WY3_Serializer_RejectsUnsafeImageSrc`
   5. `WY3_Serializer_RejectsUnsafeLinkHref`
   6. `WY5_SavedBodyIsByteIdentical`
   7. `WY6_Sanitizer_StripsDisallowedElements`
   8. `WY6_Sanitizer_StripsDisallowedAttributes`
   9. `WY6_Sanitizer_StripsUnsafeHrefs`

   **Test construction** (each test is a pure-function call — no
   browser, no DOM):
   - `WY10_RoundTrip_*` — for a corpus of 10 RC-subset Markdown
     snippets (bold, heading, list, link, image, code, mixed),
     assert `toMarkdown(renderPreview(md)) === md`.
   - `WY3_Serializer_EmitsOnlyThePinnedSubset` — assert the
     serializer's output for a full-subset input contains **only**
     the WY·3 Markdown forms (no stray HTML, no `on*` handlers,
     no `style`).
   - `WY3_Serializer_SkipsBlankElements` — assert
     `toMarkdown("<p></p>") === ""`,
     `toMarkdown("<h1></h1>") === ""`,
     `toMarkdown("<ul></ul>") === ""`.
   - `WY3_Serializer_RejectsUnsafeImageSrc` — assert
     `toMarkdown('<img src="javascript:alert(1)">')` does **not**
     contain `javascript:`.
   - `WY3_Serializer_RejectsUnsafeLinkHref` — assert
     `toMarkdown('<a href="javascript:alert(1)">x</a>')` does **not**
     contain `javascript:`.
   - `WY5_SavedBodyIsByteIdentical` — assert the serializer's
     output for a typical post body is **byte-identical** to the
     Markdown a resident would hand-type (the C#
     `ContentImageIds.FullSrcRe` regex is the reference: the
     serializer's `<img>` → `![alt](/content-image/{id})` form
     must match the regex).
   - `WY6_Sanitizer_StripsDisallowedElements` — assert
     `sanitizeHtml("<div><span style='x'>hi</span></div>") === "hi"`
     (the DIV + SPAN + style are stripped; the text is kept).
   - `WY6_Sanitizer_StripsDisallowedAttributes` — assert
     `sanitizeHtml('<a href="https://ok" onmouseover="evil()">x</a>')`
     strips the `onmouseover` + keeps the `href`.
   - `WY6_Sanitizer_StripsUnsafeHrefs` — assert
     `sanitizeHtml('<a href="javascript:alert(1)">x</a>')` strips
     the `href` + keeps the label.

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `cd src/Kumunita.Web; npm run build` (tsc) green — the compiled
  `wwwroot/js/lib/dom-to-markdown.js` is present + contains the
  `toMarkdown` + `sanitizeHtml` exports.
- The **9 pure-function tests** pass (run via
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.
  Web.Tests.dll` — the in-process path; the `dotnet test` / VS
  Test Explorer discovery is broken on this machine, per
  `AGENTS.md`).
- Handoff note: a `## U3 — serializer + sanitizer + 9 tests`
  section — (a) the 2 pure functions (the names + the subset +
  the edge cases), (b) the 9 test names + their pass status,
  (c) any `tsc` warnings, (d) the **round-trip** result (WY·10 /
  WY5 — the key invariant: `toMarkdown(renderPreview(md)) === md`
  holds for the 10-corpus).
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the 2 new files (the TS module + the
  test file) + the rebuilt `wwwroot/js/lib/dom-to-markdown.js`.

## Notes / deviations

- The serializer is the **inverse of `renderPreview`** — the same
  subset, the same escape-first construction, the same `isSafeImageSrc`
  / `isSafeUrl` semantics. If the round-trip test (WY10) fails,
  the serializer's mapping is **wrong** — fix the mapping, not
  the test.
- The sanitizer is a **DOM-shape normalizer, not a renderer** — it
  strips pasted HTML to the WY·3 subset. It does **not** add a
  second renderer or a new dependency.
- **No** binder wiring in this unit (that is U4). **No** toolbar
  rework (that is U5). **No** paste handler (that is U6). **No**
  code-view rework (that is U7). This unit is **pure** — the 2
  functions + the 9 tests.
