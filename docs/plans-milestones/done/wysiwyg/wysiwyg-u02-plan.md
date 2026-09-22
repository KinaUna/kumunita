# U2 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard) + ADR 0033

- **Lane:** WYSIWYG inline editing (`WY`)
- **Unit:** U2 (of U0–U9)
- **Kind:** design (doc-only — no code)

## Goal

Append `## Seams & contracts (Part 2, written by U2)` to the design
doc — the exact TS shapes U3–U7 must match, the DOM contract, the
serializer contract, the sanitizer contract, the **pinned seam-test
names**, the **acceptance gate**, and the **drift-guard**. Plus
**ADR 0033** (draft). **No code, no build.**

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/wysiwyg-editor-design.md` — U1's Part 1 (the
   invariant table is the primary source).
2. `docs/design/inline-editor-design.md` §Pinned contract — the
   shape to emulate.
3. `src/Kumunita.Web/client/lib/rich-editor.ts` — the `renderPreview`
   function (the inverse the serializer must be) + the 6 pure
   functions (the pattern the serializer follows).
4. `src/Kumunita.Web/Security/MarkdownRenderer.cs` — the C# renderer
   (the reference of record).
5. `docs/adr/0032-inline-editor-rendered-default-view.md` — the ADR
   shape to emulate for 0033.

## Deliverables (2 files, new)

1. **`docs/design/wysiwyg-editor-design.md`** (append Part 2).
   Sub-sections:
   - `### 2.1 frozen base (unchanged)` — RC R·1–R·7 + RE·1–RE·3 +
     IE·1 + `tsc`-only + no editor dependency + `Body` as a
     Markdown `string` — all **keep binding unchanged**.
   - `### 2.2 the DOM contract (exact)` — the pane
     (`data-rich-editor-preview`) carries `contenteditable="true"`
     (added by the binder at runtime). The textarea is **not**
     re-shaped. The toolbar is **not** re-shaped. The `data-ie-
     toggle` button is **not** re-shaped. The pane's initial state
     is `pane.innerHTML = renderPreview(textarea.value)`. The
     pane's `input` handler is `textarea.value = toMarkdown(pane.
     innerHTML)`. The toolbar's `click` handlers splice DOM (the
     Selection / Range API) into the pane, then call
     `textarea.value = toMarkdown(pane.innerHTML)`.
   - `### 2.3 the serializer contract (exact TS)` — the
     `toMarkdown(html: string): string` function in
     `client/lib/dom-to-markdown.ts`. **Pure** (no DOM, no side
     effects). **The subset** (the WY·3 subset): P, H1–H6, UL/OL,
     LI, STRONG, EM, CODE, A, IMG, PRE/CODE. **The mapping** (each
     element → its Markdown form, the inverse of `renderPreview`):
     P → inline + `\n\n`; H1–H6 → `#`–`######` + ` ` + inline +
     `\n\n`; UL/OL → `- `/`1. ` per LI + `\n\n`; LI → inline (no
     nesting); STRONG → `**` + inline + `**`; EM → `*` + inline +
     `*`; CODE → `` ` `` + text + `` ` ``; A → `[` + inline +
     `](` + href + `)`; IMG → `![` + alt + `](` + src + `)`;
     PRE/CODE → ` ```{lang}` + `\n` + text + `\n` + ` ``` `.
     **The escape rule** (the inverse of `htmlEscape`): `&amp;` →
     `&`, `&lt;` → `<`, `&gt;` → `>`, `&quot;` → `"`, `&#39;` →
     `'`. **The edge cases** (WY·3): blank P/H/LI → skip; A with
     no href → label as plain text; IMG with unsafe src → plain
     escaped text; A with unsafe href → label as plain text; PRE/
     CODE with no language → ` ``` ` + text + ` ``` `.
     **The round-trip property** (WY·10 / WY5): for any `md` in
     the RC-pinned subset, `toMarkdown(renderPreview(md)) === md`.
   - `### 2.4 the sanitizer contract (exact TS)` — the
     `sanitizeHtml(html: string): string` function in
     `client/lib/dom-to-markdown.ts`. **Pure** (no DOM, no side
     effects). **The rule:** strip every element/attribute
     **outside** the WY·3 subset; keep the WY·3 subset verbatim.
     **The reject list:** SPAN/STYLE, DIV, TABLE/TR/TD, SCRIPT,
     IFRAME, OBJECT, EMBED, FORM, INPUT, BUTTON, every `on*`
     event-handler, every `style`, every `class` (except
     `language-{lang}` on CODE), every `id`, every unsafe `href`
     (the `isSafeUrl` reject), every unsafe `src` (the
     `isSafeImageSrc` reject). **The construction:** a single pass
     over the HTML (a regex-based strip, the same construction as
     `htmlEscape` / `isSafeImageSrc` / `isSafeUrl` already in
     `rich-editor.ts`). **The output:** a string of HTML that
     contains **only** the WY·3 subset.
   - `### 2.5 the binder contract (exact TS)` — the
     `bindRichEditor` function in `client/lib/rich-editor.ts` is
     **extended** (not re-shaped) with a new **WY block**
     (`if (pane) { … }`) that: (a) sets
     `pane.contentEditable = 'true'`; (b) sets
     `pane.innerHTML = renderPreview(textarea.value)`; (c)
     installs the `input` handler; (d) installs the `paste`
     handler (the sanitizer + the insert); (e) reworks the
     toolbar's `click` handlers (the Selection / Range API); (f)
     reworks the `data-ie-toggle` button's click handler (the code
     view is now a read-only mirror). **The existing RE/IE wiring
     is untouched** — the WY block is **additive**.
   - `### 2.6 the CSS contract (exact)` — the **only** new CSS
     rule: `.rc-editor-pane[contenteditable="true"] { cursor:
     text; outline: 2px solid var(--bs-primary); outline-offset:
     1px; }` (the a11y focus ring). The existing `.rc-editor-pane`
     rule is **untouched**. The textarea's CSS is **untouched**.
   - `### 2.7 the pinned seam tests (exact names)` — file
     `tests/Kumunita.Web.Tests/WysiwygEditorTests.cs`:
     1. `WY10_RoundTrip_BoldHeadingListLinkImageCode`
     2. `WY3_Serializer_EmitsOnlyThePinnedSubset`
     3. `WY3_Serializer_SkipsBlankElements`
     4. `WY3_Serializer_RejectsUnsafeImageSrc`
     5. `WY3_Serializer_RejectsUnsafeLinkHref`
     6. `WY5_SavedBodyIsByteIdentical`
     7. `WY6_Sanitizer_StripsDisallowedElements`
     8. `WY6_Sanitizer_StripsDisallowedAttributes`
     9. `WY6_Sanitizer_StripsUnsafeHrefs`
     10. `WY7_CodeViewIsReadOnlyMirror`
     11. `WY8_TscOnly_NoEditorDependency`
     12. `WY9_PaneIsKeyboardOperable`
     13. `CompiledRichEditorJs_ContainsContentEditable`
     14. `CompiledRichEditorJs_ContainsToMarkdown`
     15. `CompiledRichEditorJs_ContainsSanitizer`
     16. `RichEditorTextarea_IsNotDisabled_OrRemoved`
     17. `RichEditorExports_AreIntact`
   - `### 2.8 acceptance gate (U8 records)` — the three WY-style
     tests: **closed loop**, **handoff**, **part-vs-whole**.
   - `### 2.9 drift-guard (frozen once written)` — the 9-invariant
     table (U1), the 10 FACES (U1), §2.2–§2.7 — all frozen pins;
     any mismatch is a `## U<m> — Drift pause`.
2. **`docs/adr/0033-wysiwyg-inline-editing.md`** — the ADR.
   **Status: Draft (lands in U9)**. **Amends** 0031 (the
   "hard non-negotiable" non-decision — **reversed** by user
   approval 2026-09-15) + 0032 (the rendered-by-default view —
   **kept**, now **editable**). Settles **D1** (the pane is the
   editing surface; the textarea is the read-only sink), **D2**
   (one new pure function: `toMarkdown`), **D3** (a sanitizer for
   paste + raw HTML). States the `tsc`-only constraint **stands
   unchanged**. **No** editor dependency; **no** `.csproj`
   change; **no** new route; **no** new server surface; **no**
   second renderer on the read path. Numbers after 0032.

## Exit

- Both files present; the design doc's DOM contract + serializer
  contract + sanitizer contract + binder contract + CSS contract +
  test names are stated **concretely** (a fresh agent can implement
  U3–U7 from the doc alone without re-deriving the RC subset).
- ADR 0033 is **present** but its **status is "Draft (lands in
  U9)"** — U9 is the unit that moves it to "Accepted" + does the
  ADR-index + `ARCHITECTURE.md` close. U2 only *authored* it.
- **No code, test, CSS, or `.csproj` change in this unit.**
- Handoff note: a `## U2 — design doc Part 2 + ADR 0033 drafted`
  section listing (a) the 9 invariants (by id), (b) the 10 FACES
  (WY1–WY10), (c) the 17 test names (by id), (d) the ADR number
  (0033) + its status (Draft), (e) the frozen base (RC R·1–R·7 +
  RE·1–RE·3 + IE·1 — **unchanged**).
- Move this plan file `in-progress/` → `done/` (move **last**).

## Notes / deviations

- The serializer contract (§2.3) is the **load-bearing** artifact
  — U3 implements it, U4–U7 build on it. The round-trip property
  (WY·10 / WY5) is the **key invariant**: for any `md` in the
  RC-pinned subset, `toMarkdown(renderPreview(md)) === md`.
- The ADR 0033 filename is **`0033-wysiwyg-inline-editing.md`**;
  U9 verifies the name + number against the ADR index before
  accepting.
