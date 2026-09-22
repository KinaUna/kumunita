# Rich content (`RC`) — Markdown bodies + in-content images — sealed unit register

## Understanding

Posts, replies, announcements, and the static pages (about / terms / help) are
currently **raw plain text**: the body is stored verbatim, rendered with
`whitespace-pre-line`, and residents have no way to bold a heading, start a
list, or show a photo. Meanwhile the *platform* already has a half-built
engine for rich text: `MarkdownRenderer` (U6 of multilingual) renders
`LocalizedPage.Body` (about / terms / help) as HTML — escape-first,
XSS-safe by construction — but every UGC surface deliberately ignores it,
and there is **no way to attach an image** anywhere except the profile
avatar (ADR 0011, a single-image, owner-scoped lane).

This lane closes that gap with the minimum that stays honest:

1. **One renderer, every body.** The existing `MarkdownRenderer` (the
   "single page engine" ADR 0005 A promises) becomes the renderer for **all**
   body content — UGC (`Post`, `PostReply`, `Announcement`, translations)
   and platform (`LocalizedPage`). UGC still renders as authored (M·3 is
   untouched: nothing is translated, ever); it just renders as *Markdown*
   rather than raw text.
2. **Images via the ADR 0011 store.** The `IMediaStore` / `MediaObject`
   content-addressed byte store already exists (avatars are its only
   consumer). This lane adds the second consumer the ADR anticipated:
   **content images** referenced from a body. References are a first-class
   document field (`ImageIds`, additive, ADR 0004 §B.1) so the serving
   route can fail closed: an image is served **only** if it is referenced
   by a resource the actor can read — "orphan file is inert" (C-MED·7) is
   extended to "orphan doc is inert".
3. **The editor stays boring.** A `<textarea>` with a Markdown hint plus a
   small "Insert image" control (upload → appends `![label](/content-image/{id})`
   into the text) and a live preview pane. **No rich-text WYSIWYG editor, no
   HTML input, no third-party editor dependency** — `tsc`-only is a pinned
   constraint of this codebase (package.json).

This is a **named lane** (`RC`), not a milestone letter — the media
(`M4-adjacent`, ADR 0011) and group posts (`GP`, ADR 0013) lanes are the
precedent. **M4/M5/M6 stay Events / Projects / Portability.** No roadmap
letter moves.

## Assumptions

- **Scope (per user):** formatting + images in announcements, posts,
  replies, and platform content (about page first, then the same lane for
  terms / help automatically). **Out of scope:** tables, footnotes, code
  highlighting (the renderer's documented "intentionally out of scope" set
  stays), drag-drop reordering, image cropping/resizing (bytes are stored
  as uploaded — `MediaOptions` already caps at 5 MiB), video/audio,
  non-raster images (ADR 0011's allowlist stands:
  `image/jpeg|png|webp|gif`), any re-encoding server-side.
- **`Body` stays a `string` of Markdown source.** No schema-shape change to
  any existing document — `Post`, `PostReply`, `Announcement`,
  `PostTranslation`, `ReplyTranslation`, `LocalizedPage` all keep their
  current fields. This means **existing content keeps rendering** (plain
  text is valid Markdown; the renderer's paragraph rule emits `<p>` around
  it, and a single blank line becomes a paragraph break — strictly better
  than today's raw text). **Migrations are zero.**
- **The only document ADDs are one field, on four owners:**
  - `Post.ImageIds` + `PostReply.ImageIds` + `LocalizedPage.ImageIds`
    + **`Announcement.ImageIds`** — all `IReadOnlyList<string>` of
    media-object ids, additive (ADR 0004 §B.1, the fifth/sixth/seventh
    additive-field pattern already established by `Status`, `GroupId`,
    `LanguageCode`, `DeletedAt`). The `Announcement` owner is named
    here because the serving branch below names the announcement as a
    UGC owner, so its reverse-lookup needs the field to read.
  - `ContentImage` — **not** a new stored document. The "content image"
    record **is** the `MediaObject` doc (ADR 0011, the catalog). What is
    new is the *reference* (`ImageIds`) and the *serving route* — the
    ADR's "follow-on lanes" non-decision is exactly what this lane ships.
- **Serving is always through the app endpoint** (C-MED·2, the avatar
  route's contract): `GET /content-image/{id}`. No static folder, no
  signed URLs. The route resolves the image's **owning resource** (the
  post/reply/announcement whose `ImageIds` contains the id — a bounded
  reverse lookup, see U03) and runs the **one frozen** `CanAsync(Read)`
  decision for UGC owners; platform pages (about/terms/help reference
  images the same way via `LocalizedPage.ImageIds`, also an ADD) are
  public by construction. No image ⇒ no owning resource visible ⇒ **404**
  (existence does not leak). **No new `AccessAction` id** (C-MED·1).
- **Upload boundary = ADR 0011's, verbatim.** Same allowlist, same 5 MiB
  cap, same `MediaOptions`, same "guards before any write" shape
  (empty → 400, oversize → 413, disallowed type → 415). The *one new
  Web lane* is the upload action; the store seam (`IMediaStore.PutAsync`)
  is **unchanged** — Core stays HTTP-free (C-MED·6 / ADR 0006-D).
- **`LocalizedPage.ImageIds` and `Announcement.ImageIds` are also ADDs**
  (the about page is the user's named platform-content consumer; the
  announcement is a UGC owner in the serving branch). Platform pages are
  admin-authored (ADR 0021 standing-gated, audited) and public on read —
  the image route's platform branch needs no authorization call.
- **Authorization model is unchanged.** This lane adds **zero** new
  `AccessAction`s, **zero** new audit *kinds* — the serving route emits
  the **same** `Read`-decision row the detail view does (same
  `TargetKind` as the owning resource: `"post"` / `"reply"` /
  `"announcement"` / `"localized_page"`), because *that is the decision it
  defers to*. AUC's audit-by-default holds: every served image is either
  public (platform page) or the product of an audited `Read` Allow.
- **The renderer extension is Web-layer and XSS-safe by the existing
  construction:** escape-first, URL scheme whitelist, and the image `src`
  is additionally restricted to the **platform route shape**
  (`/content-image/{hex-id}`) or a relative path with no scheme — a
  remote `<img src="https://…">` would be a tracking + exfiltration
  surface and is **rejected** (rendered as plain text), exactly like the
  `javascript:` link rejection already in `Inline()`.
- **Test model (unchanged).** xunit.v3; Web tests use the
  NSubstitute-controller harness (`AnnouncementControllerTests` shape);
  Core tests use the Postgres fixture. `MarkdownRenderer` is
  `Kumunita.Web.Security` — its tests live in `Kumunita.Web.Tests`
  (a new `MarkdownRendererTests.cs`, the first test file against a
  `Web.Security` type).

## Approach

**Track A (Core):** the four `ImageIds` ADDs (`Post`, `PostReply`,
`LocalizedPage`, **`Announcement`**) + the `ContentImageIds` reverse-lookup service method
(`PostService` / `AnnouncementService` / `ITranslationProvider` — one
read seam each, ADR 0006-E lane). **Track B (Web):** the renderer image
extension, the serving route, the upload lane, the composer control, the
view switch to `MarkdownRenderer`, the static-page editor preview.
**Track C (Tests):** the renderer tests (new file), the serving-route
tests (the C-MED·7 "orphan is inert" pin + the UGC decision-defer pin),
the composer-shape tests, and the closed-loop acceptance gate.

Every unit ends with **build green** (the `dotnet build` task) and, where
a TS file is touched, **`npm run build` green**.

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U01–U08 below),
one unit per fresh agent with a ~32K context window (smaller than M3's
~64K — the unit budgets below are sized accordingly: **≤ ~4 files /
~500 LOC each, entry reads ≤ 5 files / < ~300 lines each**).

**Shared state (three-tier contract):**

- **Primary — `docs/design/rich-content-design.md`** (U01) — pins the
  invariant numbers (**R·1–R·7**), the FACES (**R1–R8**), the exact C# of
  every seam, the **pinned seam-test names**, the **three-test acceptance
  gate**, and the **drift guard**.
- **Secondary — this file** — the unit register with each unit's
  deliverables and exit criteria.
- **Scratch — `docs/plans-milestones/done/rich-content-handoff-notes.md`**
  (skeleton pre-created; sections appended, never rewritten). Each unit
  writes exactly one short section before it exits.

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §Drift
guard; (3) never introduces a test whose exact name is not in the design
doc's seam list; (4) never opens a new seam on `IAuthorizationService` /
`IUserInfoService` / `IMediaStore` beyond what U01 pinned; (5) never
re-shapes `Post` / `PostReply` / `LocalizedPage` outside the ADD pin (no
new non-`ImageIds` field, no field renamed, no existing field retyped);
(6) if entry reads reveal the design doc is out of date, the unit pauses
and records `## U<m> — Drift pause` in the handoff note.

**When a unit is done:** the agent moves its plan file from
`docs/plans-milestones/in-progress/` to `docs/plans-milestones/done/`
**after** appending its handoff section (move last — the done/ folder is
the "this is finished" signal the next agent checks).

---

## Units (8 total)

### U01 — Design doc + ADR 0025
- **Goal:** author `docs/design/rich-content-design.md` (the full design doc
  — authored in full up front, like multilingual's, NOT a two-part split)
  and `docs/adr/0025-rich-content-markdown-and-content-images.md` (the ADR
  settling the two open design questions: *which renderer for UGC* and
  *how content images are referenced and served*). **No code, no build.**
- **Entry reads (≤ 5 files):** `src/Kumunita.Web/Security/MarkdownRenderer.cs`
  (the existing engine — the extension target), `docs/adr/0011-media-and-file-storage.md`
  (the store contract + the "follow-on lanes" non-decision this ADR
  resolves), `src/Kumunita.Web/Controllers/ProfileController.cs`
  (the `Avatar` serving route — the C-MED·1/2/3/6 idiom the content-image
  route copies), `docs/design/multilingual-design.md` §Invariants/§FACES
  (the table shape to emulate for R·1–R·7 / R1–R8), `src/Kumunita.Core/Posts/Post.cs`
  + `src/Kumunita.Core/Posts/PostReply.cs` (the existing additive-field
  comment convention to mirror on the new fields).
- **Deliverables (2 files, new):**
  - `docs/design/rich-content-design.md` (~250 lines). Sections:
    - `## Context` — the plain-text status quo, the renderer that already
      exists but only serves `LocalizedPage`, the ADR 0011 store that
      already has exactly one consumer (avatar).
    - `## Scope` — In: the `ImageIds` ADDs (4), the renderer image
      extension, the serving route, the upload lane, the composer
      control, the view switch, the static-page editor preview. Out:
      the "intentionally out of scope" renderer set (tables/footnotes),
      video/audio/non-raster, image transforms, any WYSIWYG editor,
      multi-image reordering UI.
    - `## Invariants (pinned for the RC lane)` — **R·1–R·7**:
      - **R·1** — **One renderer:** `MarkdownRenderer.RenderHtml` is the
        single Markdown→HTML path for every body — UGC and platform alike.
        No second renderer, no `Html.Raw` of unrendered content, no
        `whitespace-pre-line` on a Markdown body. (U02, U06.)
      - **R·2** — **Escape-first stands:** the extension adds image
        support **without** weakening the existing construction (escape
        before inline rules; URL scheme whitelist; the image `src`
        allowlist is stricter than links — platform route shape only,
        no remote `src`). (U02.)
      - **R·3** — **References are data, not URLs-in-prose:** an image in
        a body is an `![alt](/content-image/{id})` link whose `id` is a
        `MediaObject.Id`, **and** the owning resource's `ImageIds`
        contains the id (the reverse-lookup authorization surface). A
        well-formed link without a matching `ImageIds` entry renders as
        **plain text** (the renderer cannot know; the *route* 404s if a
        stray link is hand-typed — the defense is the route, not the
        renderer). (U02, U03.)
      - **R·4** — **The serving route defers to the owning resource's
        decision:** `GET /content-image/{id}` → find the owning resource
        (bounded `ImageIds` reverse lookup) → UGC: one frozen
        `CanAsync(Read)` on the owner (the audit row — same shape the
        detail view emits, same `TargetKind`); platform page: no call.
        **No owning resource found ⇒ 404** (orphan is inert — C-MED·7
        extended). No new `AccessAction` (C-MED·1). (U03.)
      - **R·5** — **Core stays HTTP-free (C-MED·6 / ADR 0006-D):** the
        reverse-lookup is a read seam on the existing services
        (`PostService`, `AnnouncementService`, the page provider) —
        `byte[]`/`IFormFile`/`Stream` never cross into Core. (U03.)
      - **R·6** — **Upload boundary is ADR 0011's, verbatim:** allowlist,
        5 MiB cap, guards-before-write (400/413/415), single `PutAsync`
        write lane. (U04.)
      - **R·7** — **Zero schema migrations:** `Body` stays a `string`;
        the four `ImageIds` fields are additive (ADR 0004 §B.1,
        delta-detected, idempotent, no seed reset); existing content
        re-renders as valid Markdown (plain text ⇒ paragraphs). (U05.)
    - `## FACES (pinned, 8)` — R1–R8, each bound to invariants:
      - **R1** a resident writes a post with `**bold**` + a list + an
        image → all three render on the detail page (R·1, R·3, R·6)
      - **R2** a resident writes a post that links a **remote** image
        (`https://…`) → it renders as plain text, not an `<img>` (R·2)
      - **R3** a non-member attempts `GET /content-image/{id}` of an
        audience-restricted post's image → **404** (no leak), and one
        `Read` **Deny** audit row is written (R·4)
      - **R4** a member opens the same image → **200** + the stored bytes
        + one `Read` **Allow** row; the browser's `<img>` in the post
        body loads it (R·4, R·1)
      - **R5** an orphan image (in the store, referenced by **no**
        `ImageIds`) → **404** for every actor including GlobalAdmin
        (R·4 — orphans are inert, not admin-readable: the route has no
        owner branch to find)
      - **R6** a GlobalAdmin edits the **about** page with an image → it
        renders on `/about` for an unauthenticated visitor (no authz
        call — platform branch) (R·1, R·4)
      - **R7** a resident uploads a 6 MiB PNG → **413**, no file written,
        no `MediaObject` row (R·6)
      - **R8** existing plain-text posts (no Markdown, no images) render
        after the switch — every word preserved, paragraphs intact (R·7,
        R·1)
    - `## Pinned contract (exact C#)` — the four `ImageIds` fields
      (exact property shape + the additive-field doc-comment convention),
      the `ContentImageController` route + action signatures, the
      reverse-lookup service method signatures (exact), the
      `MarkdownRenderer` image rule (the `src` allowlist predicate), the
      upload action signature, and the composer JS module's public
      surface (`attachImageTo(textareaEl, mediaId, alt)`).
    - `## Pinned seam tests (exact names)` — see U07's list (U01 names
      them all; U07 implements the Core-side ones, U03/U04/U05 the
      Web-side ones; the full list lives here so U07's entry-reads are
      one file).
    - `## Acceptance gate` — the three-test shape: **closed loop** (a
      post with a bold line + an attached image renders both on its
      detail page, image 200 for a member); **handoff** (a non-member
      gets 404 on the same image + a Deny audit row — the authorization
      handoff); **part-vs-whole** (the full pinned seam-test list passes
      together, and the existing `M3`/`GP` post tests still pass
      unmodified — the R·7 zero-migration pin).
    - `## Drift guard` — the 7 invariants, the 8 FACES, the pinned C#
      shapes, the test names — all frozen pins; any mismatch is a
      `## U<m> — Drift pause`.
  - `docs/adr/0025-rich-content-markdown-and-content-images.md` — the ADR
    (Status: Accepted; Date: 2026-09-14). **Context:** the two open
    questions (UGC rendering is raw text today; images exist nowhere but
    the avatar). **Decision:** (a) `MarkdownRenderer` is the single body
    renderer (the ADR 0005 "single page engine" becomes the platform's
    one renderer — the extension, not a second library; the "boring"
    constraint: no `Markdig`/`MarkdownSharp` dependency — the existing
    escape-first renderer is small enough to extend and audit); (b)
    content images are `MediaObject` docs referenced by `ImageIds` on
    the owning document, served by `GET /content-image/{id}` through the
    owning resource's `Read` decision — ADR 0011's "follow-on lanes"
    non-decision is resolved for the *post/reply/announcement/about*
    lane specifically (group logos etc. remain future lanes).
    **Consequences:** the `ImageIds` ADDs (ADR 0004 §B.1); the serving
    route's 404-orphan posture; the renderer's `src` allowlist; the
    upload boundary reuses ADR 0011's guards; **no new** `AccessAction`
    / `IMediaStore` seam. **Not decided here:** video, transforms,
    reordering UI, third-party editor.
- **Exit:** both files exist with all named sections. **No build.**
  Handoff note: 6–8 lines starting `## U01 — design doc + ADR 0025` —
  listing (a) the 7 invariants by id, (b) the 8 FACES by id, (c) the
  four `ImageIds` owners (`Post`, `PostReply`, `LocalizedPage`,
  **`Announcement`**),
  (d) the serving route path (exact string), (e) the ADR number
  (0025) and its two decisions (a) and (b).

### U02 — `MarkdownRenderer` image extension + renderer tests
- **Goal:** extend `MarkdownRenderer` to render `![alt](src)` images
  under the **R·2** rules (escape-first preserved, `src` allowlist
  stricter than links), and author the first test file against
  `Kumunita.Web.Security`. **No other file's behavior changes** —
  `LocalizedPage` rendering (about/terms/help) picks up image support
  automatically (the R1/R8 FACES are partially satisfied here — the
  about page with an image works once U05 adds the editor surface).
- **Entry reads:** `src/Kumunita.Web/Security/MarkdownRenderer.cs` (full
  — the extension target; the `Inline()` link extraction is the pattern
  the image rule copies), `docs/design/rich-content-design.md`
  §Pinned contract + §Pinned seam tests (the image rule's exact shape +
  the test names), `tests/Kumunita.Web.Tests/AnnouncementControllerTests.cs`
  (the Web-test file convention to mirror).
- **Deliverables (2 files):**
  - `src/Kumunita.Web/Security/MarkdownRenderer.cs` — **modify only.**
    Extend `Inline()` (or add a parallel `Image` extraction before the
    link pass — the design doc's pinned shape wins) to recognize
    `![alt](src)`:
    - The `src` allowlist predicate (pinned name: `IsSafeImageSrc`):
      accepts **only** (a) `/content-image/` followed by a
      lowercase-hex id (1–128 hex chars) — the platform route shape, and
      (b) a relative path with no scheme and no `//` (defensive —
      UGC bodies are admin- or author-authored, but the renderer
      shouldn't assume). **Rejects** every scheme (`http:`, `https:`,
      `data:`, `javascript:`) — a rejected `src` renders the whole
      `![alt](src)` as **plain escaped text** (the link-rejection
      precedent, verbatim pattern).
    - The `<img>` emission: `<img src="{src}" alt="{alt}"
      class="rc-image" loading="lazy" />` — `alt` and `src`
      attribute-escaped (the `HtmlEscape` + `&`/`"` rule the link
      emission already uses). **No** `width`/`height` (CSS in U03's
      deliverable owns sizing — content-width, not pixel-fixed).
    - The doc-comment's "intentionally out of scope" line is **amended**
      (images are now in scope; tables/footnotes stay out) — this is
      the one permitted doc-comment edit, inside the drift guard's
      scope (the design doc R·1 names the renderer as the single
      engine; this is its extension, not a second engine).
  - `tests/Kumunita.Web.Tests/MarkdownRendererTests.cs` — **new**, the
    pinned tests (exact names from the design doc; the set below is the
    minimum — U01's list is authoritative):
    - `Image_PlatfromRouteSrc_RendersImgTag` — `![fence](/content-image/deadbeef…)` →
      one `<img>` with that `src`, `alt="fence"`, `class="rc-image"`.
    - `Image_RemoteSrc_RendersAsPlainText` — `![x](https://evil/img.png)` →
      **no** `<img>`; the raw `![x](https://evil/img.png)` text,
      escaped.
    - `Image_DataUriSrc_RendersAsPlainText` — the `data:` rejection.
    - `Image_MalformedHexId_RendersAsPlainText` — `/content-image/xyz` →
      plain text (the allowlist is the id **shape**, not just the prefix).
    - `Image_BetweenParagraphs_StaysInParagraph` — an inline image
      mid-sentence renders inside the `<p>`, not a new block.
    - `Bold_Italic_List_Heading_StillRender` — the **regression** pin:
      every feature the renderer had before this unit still renders
      (R·1 / R·8 — the extension must not regress the existing subset).
    - `HostileMarkup_StillEscaped` — `<script>` / `onerror=` in text and
      in an `alt` → escaped, no raw tag survives (R·2 — the escape-first
      construction is intact).
- **Exit:** `dotnet build` green; `npm run build` green (no TS touched
  — this is a no-op check, recorded in the handoff note as
  "not-applicable"). The 7 named tests exist. **No other file modified.**
  Handoff note: 5 lines starting `## U02 — renderer image extension` —
  (a) the `IsSafeImageSrc` predicate (2-branch: route-shape + relative),
  (b) the `<img>` attribute set (exact), (c) the 7 test names + pass
  count, (d) confirmation the existing `Bold_Italic_List_Heading`
  features still pass (the regression pin), (e) any deviation from the
  design doc's pinned shape (a drift pause if so).

### U03 — `ImageIds` Core ADDs + serving route + reverse-lookup seams
- **Goal:** the four `ImageIds` additive fields (`Post`, `PostReply`,
  `LocalizedPage`, **`Announcement`**), the **reverse-lookup read seams** on
  `PostService` / `AnnouncementService` / the page provider (the
  "which resource owns this media id" question, ADR 0006-E read lanes —
  **no audit row on the lookup itself**; the audit row comes from the
  route's `CanAsync` call, R·4), and the `ContentImageController`
  serving route (the C-MED·2 contract: app-endpoint, decision-deferred,
  404-orphan).
- **Entry reads:** `src/Kumunita.Core/Posts/Post.cs` +
  `src/Kumunita.Core/Posts/PostReply.cs` (the additive-field convention —
  the fifth/sixth/sixth fields; the doc-comment style to mirror),
  `src/Kumunita.Core/Localization/LocalizedPage.cs` (a field owner),
  `src/Kumunita.Core/Announcements/Announcement.cs` (the fourth field
  owner — the UGC announcement in the serving branch),
  `src/Kumunita.Web/Controllers/ProfileController.cs` §Avatar (the
  serving idiom to copy verbatim — the 404-before-decision ordering, the
  one `CanAsync` call, the `nosniff` header), `src/Kumunita.Core/Posts/PostService.cs`
  (where the Post/PostReply reverse-lookup method lands — the existing
  method set to extend), `src/Kumunita.Core/Announcements/AnnouncementService.cs`
  (the announcement reverse-lookup home), `docs/design/rich-content-design.md`
  §Pinned contract (the exact signatures).
- **Deliverables (≤ 6 files):**
  - `src/Kumunita.Core/Posts/Post.cs` — ADD `ImageIds` (the exact
    pinned shape: `public IReadOnlyList<string> ImageIds { get; set; }
    = [];` with the additive-field doc-comment naming R·3/R·7).
  - `src/Kumunita.Core/Posts/PostReply.cs` — ADD `ImageIds` (same shape).
  - `src/Kumunita.Core/Localization/LocalizedPage.cs` — ADD `ImageIds`
    (same shape; the about/terms/help owner).
  - `src/Kumunita.Core/Announcements/Announcement.cs` — ADD `ImageIds`
    (same shape; the UGC announcement owner in the serving branch —
    the field its `FindByImageIdAsync` reverse-lookup reads).
  - `src/Kumunita.Core/Posts/PostService.cs` — ADD the reverse-lookup
    (pinned name + signature from the design doc, e.g.
    `Task<Post?> FindPostByImageIdAsync(string mediaId)` — the
    Post/PostReply owners, one method returning a small sealed result
    record `ImageOwner { Kind ("post"|"reply"), PostId, ReplyId? }`);
    **no audit row** on this read (R·5 — it is a lookup, the *route*
    audits via `CanAsync`).
  - `src/Kumunita.Core/Announcements/AnnouncementService.cs` — ADD the
    matching announcement reverse-lookup (the same result record, or its
    own — the design doc pins which).
  - `src/Kumunita.Core/Localization/ITranslationProvider.cs` (or the
    `LocalizedPage`-owning service — confirm by entry-read which type
    owns `GetPageAsync`) — ADD the page reverse-lookup.
  - `src/Kumunita.Web/Controllers/ContentImageController.cs` — **new.**
    `GET /content-image/{id}` — the route:
    1. Validate the `id` shape (lowercase hex, 1–128 — the
       `IsSafeImageSrc` predicate's id rule, re-used as a static —
       **no** `using Kumunita.Web.Security` is required; the predicate
       is small enough to be a `private static bool` here, OR the design
       doc pins a shared location — **entry-reads decide, drift-guard
       pins**).
    2. `IMediaStore.GetAsync(id)` → `null` ⇒ **404** (no audit — the
       store seam is HTTP-free and un-audited; the *decision* is the
       route's job).
    3. Reverse-lookup across the four owners (Post/PostReply →
       `PostService`; Announcement → `AnnouncementService`; page → the
       provider). **No owner found ⇒ 404** (R·4 — orphans are inert;
       **no** GlobalAdmin branch, R·5's FACES pin).
    4. UGC owner: **one** `IAuthorizationService.CanAsync(actorId, Read,
       the owner's adapter)` — the existing `PostToAuditableResource`
       (or the reply/announcement adapter — confirm by entry-read which
       exists; if a reply/announcement adapter does **not** exist, the
       design doc's pin names what to construct — **drift pause** if the
       doc is silent here, this is exactly the kind of gap §6 catches).
       Deny ⇒ **404** (not 403 — existence doesn't leak, the avatar
       route's `Blocked`/unknown → 404 precedent).
    5. `IMediaStore.OpenReadAsync(id)` → `File(stream, stored.ContentType)`
       + `X-Content-Type-Options: nosniff` (the avatar route's exact
       headers). Platform page owner: skip step 4, go to 5 (public by
       construction).
- **Exit:** `dotnet build` green (Core + Web). The four `ImageIds`
  fields compile (Marten picks them up on next boot — **no migration
  file authored**, R·7). The route exists. **No test authored here**
  (U07 is the seam-test unit — the split keeps this unit's entry-reads
  and diff small for a 32K window). Handoff note: 7 lines starting
  `## U03 — ImageIds + serving route` — (a) the four `ImageIds` file
  paths + field names (all `ImageIds`), (b) the three reverse-lookup
  method names + their owner types, (c) the route path (exact) + the
  404-orphan ordering (store-miss → 404, owner-miss → 404, deny → 404),
  (d) the audit-row pin (exactly one `Read` row on the UGC Allow **and**
  Deny paths; **zero** on the platform-page path; **zero** on any 404
  path), (e) which adapter the reply/announcement branch uses (name it —
  this is the drift-guard's sharpest pin for U07), (f) any compile
  warnings.

### U04 — Upload lane + composer image control (one surface: posts)
- **Goal:** the **upload** action (ADR 0011's boundary, verbatim — the
  R·6 pin) and the **composer image control** for the **post** composer
  (the one surface U04 owns; announcements/group-posts/static-pages
  reuse the same JS module in U05). The control: a file input + a small
  "Insert" button that, on upload success, appends
  `![{alt}](/content-image/{id})` into the textarea at the cursor —
  **no** WYSIWYG, **no** preview in this unit (U06 adds the preview
  pane). The upload returns the `MediaObject.Id` (JSON: `{ "id": "…" }`)
  so the JS can build the link — the id is not secret (it is a content
  hash) but the *route* still 404s until a post referencing it exists
  (R·4).
- **Entry reads:** `src/Kumunita.Web/Controllers/ProfileController.cs`
  §AvatarUpload (the upload idiom to copy verbatim — the guard
  ordering, the `IFormFile` → `byte[]` read, the `MediaOptions`
  references), `src/Kumunita.Web/client/lib/avatar-upload.ts` (the
  `client/*.ts` module convention — `addEventListener`, no framework),
  `src/Kumunita.Web/Views/Posts/New.cshtml` (the post composer — the
  surface the control lands in), `src/Kumunita.Web/Models/PostComposeViewModel.cs`
  (the composer's model — does it need a field? **No** — the images are
  referenced by the Markdown link text, not a separate form field; the
  `ImageIds` document field is populated **server-side** by parsing the
  body's `/content-image/{id}` links on create/edit — **this is the
  design doc's pinned rule R·3's enforcement point; confirm it is
  pinned; if it is not, drift pause**), `docs/design/rich-content-design.md`
  §Pinned contract (the upload action signature + the JS module surface).
- **Deliverables (≤ 5 files):**
  - `src/Kumunita.Web/Controllers/ContentImageController.cs` — **modify
    U03's file** (one file, two units — permitted: the unit-series rule
    (1) forbids touching a file *outside* one's deliverables, not
    revisiting a file a prior unit created). ADD `POST
    /content-image` (the upload action, `[Authorize]`, the R·6 guard
    ordering verbatim from `AvatarUpload`: empty → 400, oversize → 413,
    disallowed type → 415, then `IMediaStore.PutAsync(bytes, filename,
    contentType, actorId)` → `Json(new { id = stored.Id })`).
  - `src/Kumunita.Web/client/lib/insert-image.ts` — **new.** The composer
    control (the pinned public surface from the design doc, minimum:
    `bindInsertImage(form: HTMLFormElement)` — finds the
    `input[type="file"][data-insert-image]` + the target
    `textarea[data-image-target]`, wires a file input's `change` →
    upload `fetch(POST /content-image, FormData)` → on 200 append
    `![{alt}](/content-image/{id})` at the textarea's cursor (alt =
    filename without extension, truncated to 40 chars — a reasonable
    default the resident can edit in the text; the design doc pins the
    exact alt rule). The upload `fetch` is CSRF-aware (the `api.ts`
    pattern — **entry-read `client/lib/api.ts`** to confirm the exact
    header/cookie convention, then copy it).
  - `src/Kumunita.Web/Views/Posts/New.cshtml` — **modify.** Add the
    file input + button (a `<div data-insert-image>` block near the body
    textarea) + the `<script type="module"
    src="/js/lib/insert-image.js">` include (the layout's existing
    module-load pattern — confirm by entry-read of
    `Views/Shared/_Layout.cshtml`'s script includes). **No** textarea
    placeholder change (the Markdown hint is U05's, along with the
    localized key — U04 keeps the diff tight).
  - `src/Kumunita.Web/Views/Posts/Edit.cshtml` — **modify.** The same
    block + the same script include (the edit composer needs the same
    control — one line each, mirroring New.cshtml).
  - `src/Kumunita.Web/Program.cs` or the controller's model-binding —
    **the `ImageIds` population rule** (R·3's enforcement): on
    `CreatePostAsync` / `UpdatePostAsync` (the Web layer's existing
    POST handlers — **entry-read `PostsController.cs`** to find the
    exact actions), before calling the service, **parse** the body's
    `/content-image/([0-9a-f]{1,128})` matches into the draft's
    `ImageIds` (the `PostDraft`/`GroupPostDraft` records gain an
    `ImageIds` field — **modify** those two records, additive, R·7) and
    the service writes it verbatim (the service's `Post` construction
    gains `ImageIds = draft.ImageIds ?? []` — a **one-line** change in
    `PostService.cs`, named in the deliverables so the drift guard
    sees it).
- **Exit:** `dotnet build` green + `npm run build` green (the new TS
  module compiles). The upload action exists (400/413/415 guards,
  `PutAsync` call, JSON id response). The post composer (New + Edit)
  has the control. **No test authored** (U07). Handoff note: 7 lines
  starting `## U04 — upload + composer (posts)` — (a) the upload route
  (exact: `POST /content-image`) + the guard order (400→413→415→write),
  (b) the JS module path + its `bindInsertImage` public function + the
  alt rule (filename-stem, 40 chars), (c) the two composer view files
  touched (New.cshtml, Edit.cshtml), (d) the `ImageIds` population
  point (the exact controller action name + the regex used, verbatim),
  (e) the two draft records modified (`PostDraft`, `GroupPostDraft`) +
  the `PostService` construction line, (f) `tsc` warnings (if any),
  (g) any drift pause (the alt rule / the population point — name
  exactly what was ambiguous).

### U05 — Composer spread (announcements, group posts, static pages) + Markdown hint
- **Goal:** the same image control on the **announcement** composer
  (New + Edit), the **group-post** composer (New + Edit), and the
  **static-page** editor (the about/terms/help admin editor — the
  PreviewPage view, U6 of multilingual), plus the **Markdown hint**
  (a one-line localized placeholder/hint under each body textarea:
  "**Bold**, *italic*, [link](…), and ![image](…)" — the key registered
  in `KnownTranslationKeys` per the ML-UI convention). The `ImageIds`
  population rule (U04) is **re-used verbatim** on the announcement +
  group-post create/edit actions and the static-page save action (the
  three owners' write lanes — one `ImageIds` parse each, the same
  regex). **This is the last unit that touches a composer.**
- **Entry reads:** `src/Kumunita.Web/Views/Announcement/New.cshtml` +
  `Edit.cshtml` (the announcement composer — the block to copy from
  U04's post composer), `src/Kumunita.Web/Views/Groups/New.cshtml` +
  `Edit.cshtml` (the group-post composer), `src/Kumunita.Web/Views/Languages/PreviewPage.cshtml`
  (the static-page editor — the `LocalizedPage` save surface),
  `src/Kumunita.Web/Controllers/AnnouncementController.cs` +
  `GroupsController.cs` (the create/edit actions the `ImageIds` parse
  lands in — confirm the exact action names + the draft records they
  bind), `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
  (the `EnValues` set + the key-naming convention for the new hint key),
  `docs/design/rich-content-design.md` §Pinned contract (the hint key's
  name + text — pinned, not free-form).
- **Deliverables (≤ 8 files, ≤ ~500 LOC):**
  - `src/Kumunita.Web/Views/Announcement/New.cshtml` + `Edit.cshtml` —
    the image block + the script include (mirroring U04's post block,
    verbatim pattern) + the Markdown hint line.
  - `src/Kumunita.Web/Views/Groups/New.cshtml` + `Edit.cshtml` — the
    same (group posts — the `GroupPostDraft` already gained `ImageIds`
    in U04; confirm the group-post create action binds it).
  - `src/Kumunita.Web/Views/Languages/PreviewPage.cshtml` — the image
    block + the script include + the hint (the static-page editor — the
    about page's admin surface; the R6 FACES' authoring side).
  - `src/Kumunita.Web/Controllers/AnnouncementController.cs` — the
    `ImageIds` parse on create + edit (the same regex, U04's — a
    **shared** private/static helper is the clean move; if U04 inlined
    it in `PostsController`, this unit **extracts** it to a small
    `static string[] ExtractContentImageIds(string body)` in
    `Kumunita.Web.Security` (a new one-method file, `ContentImageIds.cs`
    — **new file**, the drift-guard sees it) and re-points U04's call
    site (one line in `PostsController` — the permitted revisit).
  - `src/Kumunita.Web/Controllers/GroupsController.cs` — the same parse
    on the group-post create/edit.
  - The static-page save action (the `Languages` controller —
    **entry-read** to name it exactly) — the same parse on
    `LocalizedPage` save.
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — ADD the
    hint key (the design doc's pinned name, e.g.
    `rc.markdown_hint`, the pinned `en` text). **No other keys.**
- **Exit:** `dotnet build` green + `npm run build` green. Every composer
  (post New/Edit, announcement New/Edit, group-post New/Edit,
  static-page editor) has the image control + the hint. The `ImageIds`
  parse is **one** shared helper (no copy-paste of the regex across
  three controllers — the drift-guard's copy-paste smell check).
  **No test authored** (U07). Handoff note: 6 lines starting
  `## U05 — composer spread + hint` — (a) the 7 view files touched
  (exact list), (b) the shared helper's location + name (the new
  `ContentImageIds.cs` file if extracted) + the 3 controller call
  sites (exact action names), (c) the hint key (name + `en` text,
  verbatim), (d) the `GroupPostDraft` / `Announcement` draft binding
  confirmation (did the group-post create action already bind
  `ImageIds` from U04? yes/no — if no, what was added), (e) any drift
  pause.

### U06 — Render switch: all UGC bodies + previews through `MarkdownRenderer`
- **Goal:** the **rendering** switch — every UGC body view that today
  emits `@Model.Body` / `@r.Body` raw (or with `whitespace-pre-line`)
  instead emits `@Html.Raw(MarkdownRenderer.RenderHtml(…))` inside a
  `<div class="rc-body">` container (the existing `StaticPages/Page.cshtml`
  wraps in `<div class="markdown">` — this unit adds a **shared**
  `.rc-body` CSS class so post/reply/announcement bodies get the same
  base styling as the about page, and the `<img class="rc-image">`
  sizing rule (max-width 100%, rounded, margin) is defined here — the
  one CSS deliverable of the lane). **Feed previews** (the one-line
  "what's it about" on list pages) are **Markdown-stripped plain text**
  (a new `MarkdownRenderer.PlainTextPreview(string, int maxLen)` — the
  renderer's own inverse: strips the known Markdown markers, collapses
  whitespace, truncates — **not** a regex-scraped-HTML hack). The
  **edit** views (`Posts/Edit`, `Announcement/Edit`, `Groups/Edit`,
  the reply edit forms, the translation edit forms) **keep the raw
  Markdown in the `<textarea>`** — they are the authoring surface, not
  the render surface (the R·1 pin: one renderer for *rendering*, the
  textarea is the *source*).
- **Entry reads:** `src/Kumunita.Web/Views/Posts/Detail.cshtml` (the
  post detail + the reply render + the reply-edit form — the
  render-vs-source split to respect), `src/Kumunita.Web/Views/Groups/PostDetail.cshtml`
  (the group-post detail + replies), `src/Kumunita.Web/Views/Announcement/Detail.cshtml`
  + `Index.cshtml` (the announcement detail + the pinned-announcement
  preview), `src/Kumunita.Web/Views/Shared/_PinnedAnnouncement.cshtml`
  (the pinned preview — the `PlainTextPreview` consumer),
  `src/Kumunita.Web/Views/Posts/Index.cshtml` +
  `src/Kumunita.Web/Views/Groups/Detail.cshtml` (the feed previews —
  the `BodyPreview` field, the `PlainTextPreview` consumer),
  `src/Kumunita.Web/Security/MarkdownRenderer.cs` (the `PlainTextPreview`
  home — the inverse of `RenderHtml`, pinned shape),
  `src/Kumunita.Web/wwwroot/css/site.css` (the `.rc-body` / `.rc-image`
  home — the existing `.markdown` class to mirror for consistency).
- **Deliverables (≤ 8 files):**
  - `src/Kumunita.Web/Security/MarkdownRenderer.cs` — ADD
    `public static string PlainTextPreview(string? markdown, int
    maxLen = 200)` — the pinned algorithm (strip `![alt](…)` → the `alt`
    text; strip `[label](…)` → the `label`; strip the inline markers
    (`**`, `*`, `` ` ``); strip heading `#`s; strip list markers;
    collapse all whitespace to single spaces; truncate to `maxLen`
    with `…` if longer). **No HTML output** — it returns text for a
    `<span>`/`<p>` context.
  - `src/Kumunita.Web/wwwroot/css/site.css` — ADD the `.rc-body` block
    (the `.markdown` class's existing rules, mirrored — line-height,
    `p` margins, `ul`/`ol` padding, `pre`/`code` styling) + the
    `.rc-image` rule (`max-width: 100%; height: auto; border-radius:
    6px; margin: 0.5rem 0; display: block;`). **One** CSS deliverable,
    named.
  - `src/Kumunita.Web/Views/Posts/Detail.cshtml` — the post body div →
    `<div class="rc-body">@Html.Raw(MarkdownRenderer.RenderHtml(Model.Post.Body))</div>`
    (replacing the raw `@Model.Post.Body`); every reply body the same;
    every translation body the same (the `@t.Body` lines); **the
    reply-edit + reply-translation-edit textareas stay raw** (source
    surface — do not touch them). The title-fallback line (the
    `Model.Post.Body[..80]` truncation when there is no title) →
    `MarkdownRenderer.PlainTextPreview(Model.Post.Body, 80)` (a
    plain-text preview, not raw Markdown with a `**` artifact).
  - `src/Kumunita.Web/Views/Posts/Index.cshtml` — the feed item's
    `BodyPreview` usage → the `FeedViewModel`'s `BodyPreview` is now
    computed via `PlainTextPreview` (the **view model** gains the
    computation — **modify `FeedViewModel.cs`** at the site where
    `BodyPreview` is set, one line: `PlainTextPreview(post.Body, 200)`
    — the view's `@p.BodyPreview` stays as-is).
  - `src/Kumunita.Web/Views/Groups/PostDetail.cshtml` + `Detail.cshtml`
    — the group-post body + replies + translations (the same switch as
    `Posts/Detail`), the group feed's `BodyPreview` (the
    `GroupViewModel`'s preview field, the same one-line change).
  - `src/Kumunita.Web/Views/Announcement/Detail.cshtml` — the body →
    the `rc-body` render (replacing the `whitespace-pre-line` raw
    text). `src/Kumunita.Web/Views/Announcement/Index.cshtml` +
    `_PinnedAnnouncement.cshtml` — the preview truncation →
    `PlainTextPreview` (the `a.Body[..250]` / `pinned.Body[..150]`
    lines, one each).
  - `src/Kumunita.Web/Models/FeedViewModel.cs` (+ the group/announcement
    preview VMs as entry-reads name them) — the `BodyPreview`
    computation switch (one line each, the `PlainTextPreview` call).
- **Exit:** `dotnet build` green. **Every** UGC body render site goes
  through `MarkdownRenderer.RenderHtml` inside `.rc-body` (a grep for
  `whitespace-pre-line` + `@Model.Body` / `@r.Body` / `@t.Body` on a
  render line — not a textarea — returns **zero** hits; a drift guard
  check U08 re-runs). **Every** preview site goes through
  `PlainTextPreview`. The edit textareas are untouched (source
  surface). Handoff note: 7 lines starting `## U06 — render switch` —
  (a) the full list of render sites switched (view + line purpose,
  e.g. "Posts/Detail.cshtml — post body, 2 reply sites, 3 translation
  sites"), (b) the full list of preview sites switched (view + field),
  (c) the `PlainTextPreview` algorithm (the 6 strip rules, verbatim
  order), (d) the `.rc-body` / `.rc-image` CSS (one line each), (e) the
  confirmation "edit textareas untouched" (name the 4+ files checked),
  (f) any site **not** switched + why (a drift pause if one was missed
  and then found), (g) `tsc` no-op (no TS).

### U07 — Seam tests (the pinned list) + the R·4 authorization pins
- **Goal:** author the **pinned seam tests** from the design doc — the
  UGC serving-route authorization pins (R3/R4/R5 FACES — the
  404-orphan + the Deny-row + the Allow-row shapes), the `ImageIds`
  population-shape pins (a body with two `/content-image/` links
  produces a 2-element `ImageIds` on the stored doc; a body with a
  remote `src` produces an **empty** `ImageIds` — the parse is the
  route-shape regex, not "any URL"), and the **regression** pin
  (R·7: the existing `PostServiceTests` M3/GP tests pass unmodified —
  the `ImageIds` ADD broke nothing). **This unit does NOT author the
  renderer tests** (U02) and **does NOT author the upload-guard tests**
  (U04's guards are the `AvatarUpload`-mirror; a single
  `ContentImageUploadTests.cs` with the 3 guard shapes is the
  Web-side pin, authored **here** so the test-authoring surface is one
  unit). **This is the last unit that writes a test.**
- **Entry reads:** `docs/design/rich-content-design.md` §Pinned seam
  tests (the **authoritative** name list — this unit implements exactly
  that list, no more, no less), `tests/Kumunita.Core.Tests/PostServiceTests.cs`
  (the Core-test harness shape — the `PostgresFixture` seed-and-assert
  pattern to mirror for the `ImageIds` population tests),
  `tests/Kumunita.Web.Tests/ProfileAvatarUploadTests.cs` (the
  upload-guard test shape to mirror for the content-image upload
  guards), `src/Kumunita.Web/Controllers/ContentImageController.cs`
  (U03+U04's route — the actions under test), `src/Kumunita.Core/Posts/PostService.cs`
  (the reverse-lookup method U03 added — the seam under test).
- **Deliverables (≤ 4 test files):**
  - `tests/Kumunita.Core.Tests/ContentImageOwnershipTests.cs` — **new.**
    The pinned Core-side tests (the design doc's names, minimum set —
    U01's list is authoritative):
    - `R3_ImageIdsPopulatedFromBodyLinks` — create a post via the
      service with a body containing two valid `/content-image/{hex}`
      links + one remote `https://` link → the stored `Post.ImageIds`
      has exactly the 2 platform ids (the remote one is **not** in
      `ImageIds` — the parse is route-shape, R·3).
    - `R3_ImageIdsEmpty_WhenNoLinks` — a plain-text body → `ImageIds`
      is empty (the R·7 regression: existing content's shape is
      `[]`, not `null`).
    - `R5_ReverseLookup_FindsOwningPost` — the
      `FindPostByImageIdAsync`-shaped method returns the post for a
      referenced id, `null` for an unreferenced id (the orphan — the
      route's 404 branch, R·4).
    - `R5_ReverseLookup_ReplyOwner` — the same for a
      `PostReply.ImageIds` owner (the reply-branch pin, U03's
      sharpest drift-guard).
    - `R1_Existing_PostService_Tests_Unmodified` — **the regression
      meta-test:** this test **does not** re-run the M3 suite (that is
      the part-vs-whole gate's job); it asserts the `Post` POCO's
      **existing** field set is intact (the 4 pre-RC additive fields
      + `ImageIds` = the full set — a shape pin, not a behavior
      re-test).
  - `tests/Kumunita.Web.Tests/ContentImageServingTests.cs` — **new.**
    The R3/R4/R5 FACES' Web-side pins (the controller-harness shape,
    `NSubstitute` for `IMediaStore` + `IAuthorizationService` +
    `PostService`):
    - `R4_MemberAllowsOneReadAllowRow` — the Allow path: one
      `CanAsync(Read)` call, one audit row (the seam emits it), 200 +
      the stored bytes + `nosniff`.
    - `R4_NonMemberDenies_404_AndOneDenyRow` — the Deny path: 404 (not
      403 — existence doesn't leak), exactly one Deny audit row.
    - `R5_Orphan_404_ForEveryone_IncludingAdmin` — no owner found →
      404, **zero** audit rows (the lookup is un-audited, R·5), the
      GlobalAdmin claim does **not** bypass (the FACES pin — there is
      no owner branch to find).
    - `R4_PlatformPage_NoAuditRow` — the `LocalizedPage` owner branch:
      200, **zero** `CanAsync` calls (the public-by-construction pin).
  - `tests/Kumunita.Web.Tests/ContentImageUploadTests.cs` — **new.**
    The R·6 guard pins (mirroring `ProfileAvatarUploadTests`' shape):
    - `Upload_Empty_400_NoFileWritten` — the empty-file guard.
    - `Upload_Oversize_413_NoFileWritten` — the `Media__MaxBytes` guard.
    - `Upload_DisallowedType_415_NoFileWritten` — the allowlist guard
      (an SVG is the ADR 0011's named exclusion — use it).
    - `Upload_Valid_ReturnsJsonId` — the happy path: 200, the
      `MediaObject.Id` in the JSON body, **and** the id is the
      SHA-256 hex of the bytes (the content-addressed identity,
      C-MED·4).
- **Exit:** `dotnet build` green. The **full** pinned test list exists
  (Core: the `ContentImageOwnershipTests` set; Web: the
  `ContentImageServingTests` + `ContentImageUploadTests` sets; plus
  U02's `MarkdownRendererTests`). **The pass/red status of each is
  recorded** (for U08's gate). **No gate recorded** (U08). Handoff
  note: 5 lines starting `## U07 — seam tests` — (a) the 3 new test
  files + their test counts, (b) the full pinned-list pass/red tally
  (e.g. "15/15 green" or the exact red names), (c) the two sharpest
  pins confirmed (the `R5_Orphan_…_IncludingAdmin` + the
  `R4_NonMemberDenies_…_Row` — name their exact assertion), (d) any
  test the design doc named that was **not** authored + why (a drift
  pause if so), (e) the `PostServiceTests` M3/GP suite re-run result
  (the R·7 regression — "22/22 green" or the red names).

### U08 — Acceptance gate + close: ARCHITECTURE/README flip + handoff `## Summary`
- **Goal:** run + **record** the three-test acceptance gate (closed
  loop / handoff / part-vs-whole) from the design doc §Acceptance
  gate, **using** U07's seam tests + the full existing suite as the
  part-vs-whole evidence; then the **close** — the
  `ARCHITECTURE.md` §2 `Posts/` / `Announcements/` / `Localization/`
  status lines gain the RC-lane note (the `ImageIds` ADDs + the
  serving route + the renderer's new scope), the `README.md` Roadmap
  gains the `RC` named-lane row (the **named-lane** convention, not a
  renumber — M4/M5/M6 untouched, the GP/ML precedent), and the
  handoff note's `## Summary` is written (the table of shipped units
  U01–U08, each's one-liner + test count + deviations + the
  out-of-scope deferral list for a **future** RC-2 lane, if any —
  video, transforms, reordering UI are the named deferrals).
- **Entry reads:** `docs/design/rich-content-design.md` §Acceptance
  gate (the three test names + definitions) + §Drift guard (the
  consistency checklist to run), `docs/plans-milestones/done/rich-content-handoff-notes.md`
  (U01–U07's sections — the pass/red tallies the gate references),
  `docs/ARCHITECTURE.md` §2 (the context-status table — the lines to
  amend), `README.md` §Roadmap (the named-lane row to add — the GP/ML
  row shape to mirror), `tests/Kumunita.Core.Tests/` +
  `tests/Kumunita.Web.Tests/` (the full suite — the part-vs-whole
  invocation).
- **Deliverables (3 files, modify — **no new test**):**
  - `docs/design/rich-content-design.md` — append `### Run result
    (RC acceptance gate — <date>)` — the three test names, their
    pass/red status, the seam-test tally (from U07), the full-suite
    tally (the part-vs-whole — **all** of `Kumunita.Core.Tests` +
    `Kumunita.Web.Tests`, the exact counts), one line per any
    `## U<m> — Drift pause` section in the handoff note (each resolved
    or still open).
  - `docs/ARCHITECTURE.md` — the §2 context table: `Posts/` gains
    "RC: `ImageIds` (R·7) + the serving route's owner-branch" (one
    line); `Announcements/` the same (one line); `Localization/`
    gains "RC: `LocalizedPage.ImageIds` (R·7)" (one line); the
    §7/§8 module-pattern or ADR-index section gains the ADR 0025 row
    (the ADR index — confirm by entry-read of the exact section
    numbering). **No other line moved.**
  - `README.md` — the Roadmap: the `RC` named-lane row (the GP/ML
    shape: a short ID, the lane's one-line scope, "done" status),
    **and** the `Milestones.cs` check (the `MilestonesTests.cs` pin —
    **if** the roadmap's in-progress/done ordering is test-pinned,
    confirm the `RC` row does not break the single-in-progress
    milestone invariant; **if it does**, the row is added as
    *done* (the lane ships closed in U08 — the gate is green), which
    is the safe side. A drift pause if the test is ambiguous.
  - `docs/plans-milestones/done/rich-content-handoff-notes.md` — append
    `## Summary` — the table of U01–U08 (one-liner goal + test count +
    deviations) + the deferral list (video / transforms / reordering
    UI / third-party editor — each named, each with a one-line "not
    in RC; a future lane" note).
- **Exit:** the gate section is present + consistent with U07's
  results. `ARCHITECTURE.md` + `README.md` + `Milestones.cs` (if
  touched) are in step (the AGENTS.md doc↔code parity rule). The
  `## Summary` section is present. **The last handoff note U08 writes
  is for the RC-2 agent** (if one comes). **Then the move:** all nine
  unit plan files (U01–U08, this register excluded — the register
  stays in `docs/plans-milestones/` as the sealed record, like
  `plan-m3-posts-components.md`) that are still in `in-progress/` are
  moved to `done/`.

---

## The move rule (every unit, restated)

A unit's plan file lives in `docs/plans-milestones/in-progress/` while
the unit is open. **On exit** — after the handoff section is appended
and the build is green — the unit's agent moves its own plan file to
`docs/plans-milestones/done/`. U08 (the close) performs the final
sweep: any unit plan still in `in-progress/` (a prior unit that
finished but forgot the move) is moved then. **`done/` is the signal
of completion** — a unit plan in `in-progress/` is, by definition,
not yet done.
