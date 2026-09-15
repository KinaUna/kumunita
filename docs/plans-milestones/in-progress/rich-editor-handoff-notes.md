# Rich editor (`RE`) — rolling handoff notes

> **The scratch tier** of the RE lane's three-tier contract (the design
> doc is primary, the register is secondary, this file is scratch).
> One section per unit, **appended, never rewritten**. Each unit writes
> exactly one short section before it exits; the next unit reads only that
> section + its own entry-reads list. A `## U<m> — Drift pause` section is a
> **blocker**: the next unit reads it first and either resolves it (recording
> the resolution in its own section) or carries it forward (naming it in its
> exit criteria).
>
> The skeleton below is the **only** pre-written content — every `##` section
> from here on is authored by a unit, in order.

## Lane open

- **Date:** 2026-09-15
- **Register:** `docs/plans-milestones/plan-rich-editor.md` (U0–U8)
- **Design doc (primary):** `docs/design/rich-editor-design.md` (U01 authors)
- **ADR:** `docs/adr/0031-wysiwyg-editor-and-toolbar.md` (U08 authors; Amends
  0025)
- **Scope:** a **WYSIWYG authoring surface** over the RC Markdown lane — a
  **split-view live preview** (`renderPreview` mirroring the RC pinned
  subset) + a **toolbar** (`bold`/`italic`/`code`/`h1`–`h3`/`ul`/`ol`/
  `quote`/`link`/`image`) in one shared `tsc`-only TS module
  (`client/lib/rich-editor.ts`) + the `.rc-editor-*` CSS + the toolbar/preview
  markup on the post / group-post / announcement / static-page composers
  **and the reply composers** (`Posts/Detail`, `Groups/PostDetail`,
  `Announcement/Detail`). The **text toolbar + live preview are on every
  surface** (all those bodies already render via the one `MarkdownRenderer`).
  The **image button is on only where RC's image lane is complete** — Post
  New (U04), Group New (U05), static page (U06); **gated off** (via U03's
  `data-rich-editor-no-image`) on Post Edit (U04), Group Edit + both
  Announcement composers (U05), and the reply composers (U06) — each a
  pre-existing RC drift pause (see the register's image-button matrix).
  **Client-only and additive:** no Core field, no route, no `AccessAction`,
  no `IMediaStore` method, no second server renderer, **no editor dependency
  in `package.json`**. `Body` stays a Markdown `string`; RC's
  `ContentImageIds` parse is byte-identical to the toolbar's image link.
- **Out of scope (named deferrals for a future RE-2 lane, if one comes):**
  undo/redo history stack, spellcheck/dictionary, drag-drop image reordering
  (RC stance), **contenteditable / third-party editor** (hard non-negotiable,
  D1), tables/footnotes in the toolbar or preview (RC out-of-scope set), and
  the **gated image lanes** (un-gating the image button on Post Edit / Group
  Edit / Announcement / replies — a **RC** follow-up per surface: the missing
  RC write-lane `ImageIds` seam [drift pause (a)/(c) analogs] and/or the
  missing RC serve-branch adapter [RC U03 inert-404]; until then those
  surfaces' image button stays gated via `data-rich-editor-no-image`, and
  un-gating is a one-attribute removal, no RE code).
- **Frozen base (RC, unchanged):** `MarkdownRenderer` · `ContentImageIds` ·
  `IMediaStore` · `MediaObject` · the four `ImageIds` fields · the
  `GET`/`POST /content-image` routes · the `.rc-body`/`.rc-image` CSS ·
  `insert-image.ts` · RC R·1–R·7 invariants.

<!-- U0 appends its section below this line. One `##` section per unit, in
     order (U0, U1, … U8). Never rewrite a prior section. -->

## U0 — Kickoff verified

**Date:** 2026-09-15

**Gap 1 — every composer shows raw Markdown only (no preview pane, no toolbar):**

| Surface | Body `<textarea>` (file + line) | Preview pane? | Toolbar? |
|---|---|---|---|
| Post New | `Views/Posts/New.cshtml:89` | ❌ | ❌ |
| Post Edit | `Views/Posts/Edit.cshtml:93` | ❌ | ❌ |
| Group New | `Views/Groups/New.cshtml:61` | ❌ | ❌ |
| Group Edit | `Views/Groups/Edit.cshtml:62` | ❌ | ❌ |
| Announcement New | `Views/Announcement/New.cshtml:109` | ❌ | ❌ |
| Announcement Edit | `Views/Announcement/Edit.cshtml:109` | ❌ | ❌ |
| Static page | `Views/Languages/PreviewPage.cshtml:42` | ❌ | ❌ |
| Reply New (Posts) | `Views/Posts/Detail.cshtml:423` | ❌ | ❌ |
| Reply Edit (Posts) | `Views/Posts/Detail.cshtml:351` | ❌ | ❌ |
| Reply New (Groups) | `Views/Groups/PostDetail.cshtml:379` | ❌ | ❌ |
| Reply Edit (Groups) | `Views/Groups/PostDetail.cshtml:327` | ❌ | ❌ |

**Grep proof:** `data-rich-editor\|rc-editor\|rich-editor` in `src/Kumunita.Web/**` → **0 hits**. No `rc-editor-*` CSS block in `site.css`. No `client/lib/rich-editor.ts` file. The only body-adjacent control on any surface is the RC `rc-insert-image` block (a file-input for uploading an image and splicing a Markdown link) — **not** a preview pane or a formatting toolbar.

**Gap 2 — the frozen RC base is intact at its claimed locations:**

| Seam | Confirmed location | Status |
|---|---|---|
| `MarkdownRenderer` | `src/Kumunita.Web/Security/MarkdownRenderer.cs` | ✅ present |
| `ContentImageIds` | `src/Kumunita.Web/Security/ContentImageIds.cs:25` (`public static class ContentImageIds`, same namespace as `MarkdownRenderer`) | ✅ present |
| `.rc-body` CSS | `src/Kumunita.Web/wwwroot/css/site.css:733` (`.rc-body` through `.rc-body a` at line 782) | ✅ present |
| `.rc-image` CSS | `src/Kumunita.Web/wwwroot/css/site.css:783` | ✅ present |
| `insert-image.ts` | `src/Kumunita.Web/client/lib/insert-image.ts` | ✅ present |
| `package.json` (tsc-only) | `src/Kumunita.Web/package.json` — `devDependencies`: `typescript: ^7.0.2` **only**; `scripts.build`: `tsc`; no bundler, no editor dep | ✅ `typescript`-only (RE·3 holds at lane open) |

**RC drift pauses carried forward (determine per-surface image-button gating via `data-rich-editor-no-image`):**

| Surface | RC drift pause (from RC handoff notes) | Image button (RE) |
|---|---|---|
| Post New | — (RC lane complete: write `PostsController:692`, serve post branch) | **ON** |
| Post Edit | U04 (c): `UpdatePostAsync` never sets `ImageIds` (discrete-field shape) | **OFF** (U04 gates) |
| Group New | — (RC lane complete: U05 wired `CreateGroupPostAsync`; serve post branch) | **ON** |
| Group Edit | U05 drift pause: `UpdateGroupPostAsync` never sets `ImageIds` (discrete-field shape) | **OFF** (U05 gates) |
| Announcement New | U03 drift pause: no `AnnouncementToAuditableResource` → serve branch inert-404 | **OFF** (U05 gates) |
| Announcement Edit | U03 drift pause (same) | **OFF** (U05 gates) |
| Static page | — (RC lane complete: write `LanguagesController:271`, serve page branch, public) | **ON** (U06) |
| Reply New/Edit (all 3 Detail views) | U04 (a): `CreateReplyAsync`/`UpdateReplyAsync` never set `PostReply.ImageIds` + U03: serve branch inert-404 | **OFF** (U06 gates) |

**Known RC debt (flagged, not folded into `rc.editor.*`):** `rc.markdown_hint` is **not registered** in `KnownTranslationKeys` (RC U05's hint deferral — the RC design doc never named the key). U08 will register `rc.editor.*` keys; `rc.markdown_hint` remains an **RC debt** to be resolved by the RC lane owner.

**Drift pause:** none — both gaps hold as stated in the register; the frozen RC base is intact at every claimed location. U03 can build against the base as pinned.

## U1 — Design doc + ADR 0031 drafted

**Date:** 2026-09-15

- **Primary tier authored:** `docs/design/rich-editor-design.md` — the RE·1–RE·3 invariants, the FACES RE1–RE5, the **exact toolbar marker set** (the *ceiling*), the **`renderPreview` subset**, the **client `isSafeImageSrc` mirror** (+ the client link-URL mirror of `IsSafeUrl`), the 5 pure-function + binder export signatures, the 10 pinned seam-test names, the acceptance gate, the drift guard, and the named deferrals. It re-anchors **RC R·1–R·7** as the frozen base (unchanged).
- **ADR 0031 drafted (status "Draft (lands in U08)"):** `docs/adr/0031-wysiwyg-editor-and-toolbar.md` — settles **D1** (split-view live preview, not `contenteditable`), **D2** (toolbar-as-Markdown-splice in one `tsc`-only TS module), **D3** (client preview mirrors the RC subset + `IsSafeImageSrc` semantics). States the `tsc`-only constraint **stands unchanged**; only the composer surface changes. **Amends** ADR 0025 (does not supersede); numbers after 0030. U08 owns the ADR-accept + ADR-index update.
- **The two load-bearing pins** (U03 must code against these without re-deriving the RC subset):
  1. **The exact toolbar marker set (ceiling)** — `bold` `**` · `italic` `*` · `code` `` ` `` · `h1`–`h3` `#`–`###` · `ul` `- ` · `ol` `1. ` · `link` `[label](url)` · `image` `![alt](/content-image/{id})`. **No** blockquote button (see the drift pause below). Every button's marker is a strict subset of what `renderPreview` renders.
  2. **The client `isSafeImageSrc` mirror** — accept **only** (a) `/content-image/` + 1–128 lowercase hex `[0-9a-f]{1,128}`, or (b) a schemeless relative (no `:`, no leading `//`, no whitespace); **reject every scheme** (`http:`/`https:`/`data:`/`javascript:`/…) and every malformed id — a rejected `src` renders the whole `![alt](src)` as plain escaped text. Verbatim mirror of `MarkdownRenderer.IsSafeImageSrc`; the client link-URL predicate mirrors `IsSafeUrl` (http/https/mailto/relative).
- **The `renderPreview` subset** (and only that) — headings 1–6, paragraphs, `- `/`* ` lists, `1. ` lists, fenced code blocks, inline code / bold / italic, `[label](url)` links (under the client `IsSafeUrl` mirror), and `![alt](src)` images (under the client `isSafeImageSrc` mirror, `rc-image` class). **Out on both toolbar and preview:** tables, footnotes, raw HTML, **blockquote**.
- `rc.markdown_hint` left **out** of `rc.editor.*` (it is RC debt, per U0 — U08 registers only the `rc.editor.*` set).
- **U03 can now build the module from the design doc alone.** The pure-function signatures, the `data-md` kinds, the `data-rich-editor` / `data-image-target` / `data-rich-editor-preview` / `data-rich-editor-no-image` attribute contract, and the two load-bearing pins are all pinned concretely.

## U1 — Drift pause (blockquote / `> `)

**Status: recorded, not resolved — carried to U08 as a named deferral.**

The register (`plan-rich-editor.md`) and U0's work order named a **`quote` toolbar button** and `> ` in the **`renderPreview` subset**. U1's entry read of the frozen base — `src/Kumunita.Web/Security/MarkdownRenderer.cs` — shows **`MarkdownRenderer` has no blockquote branch**: its block dispatch loop handles only fenced code, headings (1–6), unordered lists, ordered lists, and paragraphs, and `PlainTextPreview` has **no blockquote strip step** (its pinned 7-step strip order is image/link → inline markers → heading `#`s → list markers → collapse whitespace → truncate). A resident-typed `> quote` therefore renders on the **read path** as a **plain paragraph** (the leading `> ` left in the text), not a `<blockquote>`.

This directly collides with **RE·2** ("*do not add a marker to the preview subset the server renderer lacks*") and the unit's hard constraint ("*the marker set is a ceiling, not a wishlist*"). **Resolution (per the constraint, a `cut the button` outcome, not a silent feature):** blockquote is **removed from both sides** in the design doc — **no `quote` button** in the toolbar marker set and **no `> `** in the `renderPreview` subset. `applyBlock`'s `kind` union is therefore `'h1'|'h2'|'h3'|'ul'|'ol'` (no `'quote'`). Adding blockquote is a **future lane** that must extend `MarkdownRenderer` **and** the client `renderPreview` **in the same commit** (RE·2's parity holds only if both move together) — recorded in the design doc's §Named deferrals and ADR 0031's "Not decided here."

**Carried to:** U08 (the close — keep blockquote out of the `rc.editor.*` key set and out of any roadmap/README copy of the marker list). No U03/U04/U05/U06 action is blocked by this pause; the remaining 9-button toolbar + preview mirror are fully codeable from the design doc.

## U2 — Mirror checklist (pinned)

**Date:** 2026-09-15

**Purpose (RE·2 made checkable before any TS exists):** this is the verbatim
pin U03's `renderPreview` implements *from this section alone* — the exact
marker ceiling, the exact `src` allowlist predicate, and the exact CSS
classes the preview output must carry. Every line below was read directly
against the frozen base on 2026-09-15 (`MarkdownRenderer.cs` full read;
`site.css` `.rc-body`/`.rc-image`; `ContentImageIds.cs`; the 7 pinned
`MarkdownRendererTests`). **Drift pause: none** — all seams intact and
matching RC's close + U1's design doc. The one carried constraint: **no
blockquote** (`> `) on either side (U1 drift pause; `MarkdownRenderer` has no
blockquote branch — see below).

**1. The marker set `MarkdownRenderer` renders (the CEILING).** `renderPreview`
must render **exactly** this set and **nothing more** — a toolbar button that
emits a marker outside this set is a drift pause (RE·2). Source:
`MarkdownRenderer.RenderHtml` block dispatch + `Inline`/`InlineText`.

- Headings **1–6** — `#` through `######` (a space after the `#`s, non-empty
  content) → `<h1>`…`<h6>` (`MatchHeading`).
- **Paragraph** — consecutive non-blank, non-special lines collapse into one
  `<p>…</p>`.
- **Unordered list** — `- ` / `* ` lines → `<ul><li>…</li></ul>`.
- **Ordered list** — `1. ` / `2. ` … (`\d+\. `) → `<ol><li>…</li></ol>`.
- **Fenced code block** — ` ``` ` … ` ``` ` → `<pre><code …>…</code></pre>`
  (content escaped verbatim, no inline rules; optional `language-{lang}` class).
- `**bold**` → `<strong>…</strong>`.
- `*italic*` → `<em>…</em>`.
- `` `code` `` → `<code>…</code>` (applied first, so a backtick inside bold is
  not re-processed).
- `[label](url)` → `<a href="…">label</a>` (under the link-URL allowlist).
- `![alt](src)` → `<img src="…" alt="…" class="rc-image" loading="lazy" />`
  (under the image `src` allowlist below).

**Out on both toolbar and preview (the ceiling is a cap, not a wishlist):**
blockquote / `> `, tables, footnotes, raw HTML. `MarkdownRenderer` has **no
blockquote branch** (block dispatch = fenced code, headings, ul, ol, paragraph
only; `PlainTextPreview` has no blockquote strip step) — a resident-typed
`> ` renders as a plain paragraph on the read path, so RE·2 forbids adding it
to either side (U1 drift pause; carried to U08).

**2. The client `IsSafeImageSrc` mirror (verbatim predicate).** `renderPreview`'s
image branch accepts **only** these two shapes and rejects everything else — a
rejected `src` renders the **whole** `![alt](src)` as plain escaped text (no
`<img>`, no `src=`). This is a verbatim copy of
`MarkdownRenderer.IsSafeImageSrc` (RC R·2):

- **Branch 1 — the platform route shape:** `src` starts with the literal
  `/content-image/`, and the remainder (the `{id}`) is **1–128 lowercase hex**
  chars, `[0-9a-f]{1,128}` (exact — a query string or trailing slash is a
  malformed id → reject, **not** a fallback to branch 2).
- **Branch 2 — a schemeless relative path:** no `:` anywhere, **no** leading
  `//` (protocol-relative), and **no** whitespace.
- **Rejected (always):** every URL scheme — `http:`, `https:`, `data:`,
  `blob:`, `javascript:` (i.e. any `://` or bare `scheme:` form) — and any
  empty/malformed id. (`data:` URIs are the XSS vector the 7 pinned tests pin
  to plain-text.)
- The `alt` is HTML-escaped **verbatim** (no inline rules inside the attribute).

(The client **link-URL** predicate mirrors `IsSafeUrl`, not `IsSafeImageSrc`:
accept `http`/`https`/`mailto`/relative, reject `javascript:`/`data:`/any
other scheme. Keep the two distinct — images are strictly route/relative; links
are the broader scheme whitelist.)

**3. The CSS classes the preview output must carry (so it looks like the
read path).** The preview pane is the renderer's output wrapped so it inherits
the RC body typography:

- A wrapper `<div class="rc-body rc-editor-pane">` around the whole
  `renderPreview` output — `.rc-body` (site.css, the RC U06 block: Lora body
  serif, type scale, `h1`–`h6` / `ul` / `ol` / `code` / `pre` / `a` rules)
  gives the preview body copy identical styling to the read path.
- Every accepted image carries `class="rc-image"` (the RC rule: `max-width:
  100%; height: auto; border-radius: 6px;` block) — the same `<img>` attribute
  set the read path emits (`src`, `alt`, `class="rc-image"`,
  `loading="lazy"`).

**4. The escape-first stance (client-side R·2).** `renderPreview` **escapes
HTML entities before** applying the marker→HTML map — every input character is
entity-escaped (`&` `<` `>` `"` `'`) **first**, and the inline rules
(code, then bold, then italic) run on the **already-escaped** result, exactly
as `MarkdownRenderer.HtmlEscape` + `InlineText` do. A hostile `<script>` /
`onerror=` typed in the textarea must therefore appear in the preview as
escaped text, never as a live tag/attribute. The one ordering nuance to keep:
images/links are extracted **before** the surrounding text is escaped, so their
`src`/`url` values survive the allowlist check intact (the
`ImageOrLinkPattern` single document-order pass), while the *text around* them
is escaped-first.

**5. The preview is a *function of* the textarea value (RE·1).** The textarea
is the **single source of truth**; the preview is a pure render of its current
value, **re-rendered on every `input`**. `renderPreview` is pure (no hidden
state, no `contenteditable` mirror, no `body.innerHTML` as a source) — a
toolbar button is a pure text splice **into the textarea**, and the form still
submits the textarea's `value` exactly as RC does.
