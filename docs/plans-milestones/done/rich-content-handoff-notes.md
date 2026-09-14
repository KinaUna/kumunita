# Rich content (`RC`) — rolling handoff notes

> **The scratch tier** of the RC lane's three-tier contract (the design
> doc is primary, the register is secondary, this file is scratch).
> One section per unit, **appended, never rewritten**. Each unit writes
> exactly one short section before it exits; the next unit reads only
> that section + its own entry-reads list. A `## U<m> — Drift pause`
> section is a **blocker**: the next unit reads it first and either
> resolves it (recording the resolution in its own section) or carries
> it forward (naming it in its exit criteria).
>
> The skeleton below is the **only** pre-written content — every `##`
> section from here on is authored by a unit, in order.

## Lane open

- **Date:** 2026-09-14
- **Register:** `docs/plans-milestones/plan-rich-content.md` (U01–U08)
- **Design doc (primary):** `docs/design/rich-content-design.md` (U01 authors)
- **ADR:** `docs/adr/0025-rich-content-markdown-and-content-images.md` (U01 authors)
- **Scope:** `Body` stays a Markdown `string` on every document (zero
  migrations); the four `ImageIds` ADDs (`Post`, `PostReply`,
  `LocalizedPage`, **`Announcement`**); the `MarkdownRenderer` image extension; the
  `GET /content-image/{id}` serving route (decision-deferred, 404-orphan);
  the `POST /content-image` upload lane (ADR 0011's boundary, verbatim);
  the composer image control (post/announcement/group-post/static-page);
  the render switch (every UGC body → `MarkdownRenderer` in `.rc-body`;
  every preview → `PlainTextPreview`).
- **Out of scope (the named deferrals for a future RC-2 lane, if one
  comes):** video/audio, image transforms (crop/resize), multi-image
  reordering UI, any WYSIWYG/third-party editor, tables/footnotes in the
  renderer.

<!-- U01 appends its section below this line. One `##` section per unit, in
     order (U01, U02, … U08). Never rewrite a prior section. -->

## U01 — design doc + ADR 0025

- **Invariants (7):** R·1 one renderer · R·2 escape-first stands · R·3 references are data, not URLs-in-prose · R·4 the serving route defers to the owning resource's decision · R·5 Core stays HTTP-free · R·6 upload boundary is ADR 0011's verbatim · R·7 zero schema migrations.
- **FACES (8):** R1 bold+list+image render · R2 remote `src` → plain text · R3 non-member → 404 + one Deny row · R4 member → 200 + one Allow row · R5 orphan → 404 for everyone including GlobalAdmin, zero rows · R6 about-page image public, zero `CanAsync` · R7 6 MiB → 413, no write · R8 pre-existing plain text renders.
- **`ImageIds` owners (4):** `Post`, `PostReply`, `LocalizedPage`, `Announcement` — all `public IReadOnlyList<string> ImageIds { get; set; } = [];` (ADR 0004 §B.1 additive; zero migrations, R·7).
- **Routes:** serving `GET /content-image/{id}` (5-step: store-miss → 404; owner-miss → 404; UGC one `CanAsync`, Deny → 404; platform direct; `File` + `nosniff`) · upload `POST /content-image` (`[Authorize]`, guards 400→413→415, one `PutAsync`, `Json(new { id })`).
- **20 pinned tests, 4 files:** `tests/Kumunita.Web.Tests/MarkdownRendererTests.cs` (7, U02) · `tests/Kumunita.Core.Tests/ContentImageOwnershipTests.cs` (5, U07) · `tests/Kumunita.Web.Tests/ContentImageServingTests.cs` (4, U07) · `tests/Kumunita.Web.Tests/ContentImageUploadTests.cs` (4, U07).
- **ADR 0025 (Amends: 0011), two decisions:** (a) `MarkdownRenderer` is the one body renderer — extended with `![alt](src)` under the stricter `IsSafeImageSrc` allowlist (platform route shape only), no second library · (b) content images are `MediaObject` docs referenced by `ImageIds` on the owning doc, served by `GET /content-image/{id}` through the owner's single `Read` decision (UGC) or directly (platform), 404 on every miss, no new `AccessAction` / `IMediaStore` seam.
- **Drift pause:** none — all entry reads matched the pinned shapes (`Post`/`PostReply` additive-field ordinals confirmed; `MarkdownRenderer`'s "intentionally out of scope" line confirmed to list images, which U02 will amend).

## U02 — renderer image extension

- **`IsSafeImageSrc` (2 accept branches):** (1) `/content-image/{id}` where `id` is 1–128 lowercase hex `[0-9a-f]` (exact route shape — no query/trailing slash); (2) a relative path — no `:`, no leading `//`, no whitespace. Any scheme (`http:`/`https:`/`data:`/`javascript:`), a malformed/empty id, or a non-hex route id **rejects**.
- **`<img>` emission (exact):** `<img src="{esc}" alt="{alt}" class="rc-image" loading="lazy" />` — `src` escaped per the link rule (`&`→`&amp;`, `"`→`&quot;`); **no** `width`/`height` (U06 CSS owns sizing).
- **Tests — 7/7 green:** `Image_PlatformRouteSrc_RendersImgTag` · `Image_RemoteSrc_RendersAsPlainText` · `Image_DataUriSrc_RendersAsPlainText` · `Image_MalformedHexId_RendersAsPlainText` · `Image_InlineInParagraph_StaysInParagraph` · `Bold_Italic_List_Heading_StillRender` · `HostileMarkup_StillEscaped` (class run 7/7; full `Kumunita.Web.Tests` 118/118, no regressions).
- **Alt simplification:** `alt` is `HtmlEscape`d **verbatim** — inline rules (bold/italic/code) would emit tags inside the attribute and break it, so the label stays escaped plain text (deliberate, per the pinned contract).
- **Doc-comment amendment:** images **left** the "intentionally out of scope" list (now in scope, R·1); **tables, footnotes, and raw HTML stay out**. Image+link extraction merged into one document-order pass (`(!?)\[...\]\(...\)`) so a link never double-consumes the `[alt](src)` inside an `![alt](src)`.
- **Drift pause:** none — the pinned `IsSafeImageSrc` shape, the exact `<img>` attribute set, and all 7 seam-test names matched the design doc verbatim. `npm run build` not-applicable (no TS touched this unit).
