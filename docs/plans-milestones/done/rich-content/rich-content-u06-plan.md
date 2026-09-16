# RC U06 — the render switch: every body through the one renderer

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained. Primary tier = `docs/design/rich-content-design.md`
> (R·1 is the invariant this unit enforces: **one renderer** — no
> `Html.Raw` of unrendered body, no `whitespace-pre-line` on
> Markdown source); mismatch → record `## U06 — Drift pause` in the
> handoff note — do not silently pick.

## Goal

Make R·1 true at **every** surface: UGC bodies (post detail, group-
post detail, announcement detail + their replies) and platform pages
already render through `MarkdownRenderer.RenderHtml` — this unit
switches the UGC detail views, adds the `PlainTextPreview` helper for
every preview/list surface, and adds the `.rc-body` / `.rc-image` CSS
so the rendered output (headings, lists, code, **images**) looks
right in the site's existing palette. Edit textareas stay raw
Markdown (the author sees the source — R·3's body-is-source-of-truth
model).

## Entry reads (≤ 5 items)

1. `docs/design/rich-content-design.md` — §Invariants R·1/R·7,
   §FACES R1/R6/R8, §Pinned contract (the `PlainTextPreview` strip
   order + default).
2. `src/Kumunita.Web/Security/MarkdownRenderer.cs` — full (the
   `Inline()`/block structure you'll mirror in the preview helper;
   the image emission shape from U02).
3. **Pin the render + preview site list** — grep, in this order, and
   record every hit with file + line in the handoff note:
   (a) `whitespace-pre-line` in `src/Kumunita.Web/Views/` (the
   pre-RC body rendering — known: `Announcement/Detail.cshtml` ≈ L69;
   there may be an index/list variant too);
   (b) `@Model.Post.Body` / `@p.Body` / raw `Body` in
   `src/Kumunita.Web/Views/` (known: `Posts/Detail.cshtml` ≈ L86,
   `Groups/PostDetail.cshtml` ≈ L81, the **replies** block in the
   same views, any index excerpt);
   (c) `BodyPreview` across `src/Kumunita.Web/` (Views **and**
   `Models/`) (known: `Posts/Index.cshtml` ≈ L152 — find where the
   property is computed; there may be announcement + group index
   variants).
4. `src/Kumunita.Web/Views/StaticPages/Page.cshtml` — the existing
   `@Html.Raw(MarkdownRenderer.RenderHtml(...))` idiom to mirror
   exactly (the `Html.Raw` wrapper + any surrounding element/class).
5. The site stylesheet — from the layout's `<link rel="stylesheet">`
   (read the layout, find the CSS file, read its existing
   typography conventions — the `.rc-body` rules must match the
   site's font stack/spacing scale, not introduce a new one).

## Deliverables (3–6 files, following the site list in entry read 3)

### 1. `src/Kumunita.Web/Security/MarkdownRenderer.cs` (modify)

Add the pinned helper (the design doc's §Pinned contract is
authoritative for the order):

```csharp
public static string PlainTextPreview(string? markdown, int maxLen = 200)
```

**The pinned strip order** (each step before the next; the order
matters — image/label extraction must happen before marker
stripping, or an alt containing `**` would leak markers):
1. `![alt](src)` → `alt` (the same regex shape as the renderer's
   image pass — share the pattern; if the renderer keeps it
   private, make the image pattern a `private const string`
   referenced by both — one pattern, two consumers).
2. `[label](url)` → `label` (the link pattern, same sharing rule).
3. Strip `**` (bold markers) then `*` (italic) then `` ` `` (inline
   code markers) — **markers only**, the text between them stays.
4. Leading `#` heading markers (`^#{1,6}\s*` per line → the line
   text).
5. List markers: leading `- `, `* `, `N. ` (per line) → the item
   text.
6. Collapse **all** whitespace runs (including newlines) to a single
   space; trim.
7. If longer than `maxLen`: cut to `maxLen` on a **space boundary**
   (never mid-word) and append `…` (the single-char ellipsis `…`,
   not `...`). `null`/empty input → `""`.
The return is **text** for a text context (a `<span>`/`<p>`) — the
helper does **not** HTML-escape (the Razor view will encode it at
`@` interpolation — do not double-escape; if the call site needs
`Html.Raw`, that's a drift pause, not a silent choice).

### 2. The site stylesheet (modify)

Append the RC block (the **minimum** set — boring, no shadows, no
new palette; match the site's existing scale):

```css
/* RC — rich-content rendered bodies (U06) */
.rc-body { line-height: 1.55; overflow-wrap: break-word; }
.rc-body > * + * { margin-top: 0.75em; }
.rc-body .rc-image, .rc-image { max-width: 100%; height: auto; }
.rc-body code { font-family: var(--font-mono, ui-monospace, monospace);
  background: rgba(127, 127, 127, 0.15); padding: 0.05em 0.3em;
  border-radius: 3px; }
.rc-body pre { background: rgba(127, 127, 127, 0.12);
  padding: 0.6em 0.8em; border-radius: 4px; overflow-x: auto; }
.rc-body pre code { background: none; padding: 0; }
.rc-body a { text-underline-offset: 2px; }
```

(Adjust `var(--font-mono, …)` and the neutral alpha values to the
site's actual custom properties/conventions per entry read 5 — the
**structure** of the block is pinned, the exact tokens follow the
site. If the site is dark-mode, the alphas must still read on the
existing background — verify against the site's existing code-block
styling if it has one and **match it** rather than inventing a new
shade.)

### 3. The UGC detail views (modify — the count follows entry read 3)

For **every** body render site found (post detail, group-post
detail, announcement detail, the replies block in the post/group
detail views, and any other hit): switch the raw-body rendering to
the `StaticPages/Page.cshtml` idiom —

```html
<div class="rc-body">@Html.Raw(MarkdownRenderer.RenderHtml(@Model.Post.Body))</div>
```

(mirror the view's actual model property per site) and **remove**
the `whitespace-pre-line` (or equivalent raw-text) class/attribute
from that element. The `@using Kumunita.Web.Security` (or the
renderer's actual namespace) must be importable in the view — the
`Page.cshtml` shows the exact `@using`/tag-helper mechanism; mirror
it. **Edit views are untouched** — their `<textarea>` keeps the raw
Markdown (the author's source, R·3).

### 4. The preview sites (modify — the count follows entry read 3)

Wherever a list/index surface shows a body excerpt
(`p.BodyPreview` etc.): the **computation** moves to the helper —
if `BodyPreview` is a view-model property computed as a substring,
change its computation to
`MarkdownRenderer.PlainTextPreview(body)` (keep the property name —
the views don't need to change); if it's computed inline in the view,
switch the expression. Every preview site ends up showing the
**same** `maxLen` (200) behavior — no per-site custom lengths (boring;
record any site that needs a different length as a drift pause).

## Exit criteria

- `dotnet build` green. **`npm run build` not required** (no TS) —
  record "not-applicable".
- **Grep proof** (record the results in the handoff note):
  (a) `whitespace-pre-line` in `src/Kumunita.Web/Views/` → **zero**
  hits on body elements (a hit on a non-body element is fine — name
  it); (b) raw `@Model.Post.Body` / `@p.Body` (without
  `RenderHtml`) → **zero** hits; (c) the edit forms' textareas still
  bind the raw body (the create/edit views still `@Model.…Body` on
  the `<textarea>` — the switch must not have touched them; name
  the two you verified).
- **No** test files (the R8 regression is U07's
  `HostileMarkup`/`Bold_Italic_…` pins + the acceptance gate).
- A manual smoke (record in the handoff note): start the app (the
  `run` task, background), load an existing post detail (any seeded/
  dev post) — the body renders as a paragraph (plain text wrapped in
  `<p>`, R·8), the index shows a ≤200-char single-line preview, and
  the about page still renders (R·1's platform path). Kill the app.
- **Handoff note** (append): `## U06 — render switch`, 6–8 lines:
  (a) the full render-site list switched (file + view + property,
  one line each); (b) the full preview-site list + the
  `BodyPreview` computation site (file); (c) the `PlainTextPreview`
  strip order as implemented (7 steps, compact); (d) the CSS file
  path + the token adjustments made (or "none"); (e) the three grep
  proofs (a/b/c results); (f) the smoke result; (g) the drift pause,
  if any.
- **Then move this file:** `Move-Item
  docs\plans-milestones\in-progress\rich-content-u06-plan.md
  docs\plans-milestones\done\rich-content-u06-plan.md`.
