# RC U01 — Design doc (`rich-content-design.md`) + ADR 0025

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the
> exit criteria. The register
> (`docs/plans-milestones/plan-rich-content.md`) is the cross-reference;
> when the two disagree, **this file wins for what to do** and the
> register wins for *which* files exist.

## Goal

Author the RC lane's **primary tier** — `docs/design/rich-content-design.md`
(authored in full, one file, like the multilingual design doc — NOT a
two-part split) and the ADR that settles the two open design questions
(*which renderer for UGC* and *how content images are referenced and
served*). **No code, no build.**

## Context you need (read these first, in this order — 6 files)

1. `src/Kumunita.Web/Security/MarkdownRenderer.cs` — the existing engine
   and the extension target. Note: escape-first construction, the
   `Inline()` link pass, the `IsSafeUrl` scheme whitelist, and the
   doc-comment's "intentionally out of scope" line (tables, images, raw
   HTML — **images leave that list in this lane; tables stay out**).
2. `docs/adr/0011-media-and-file-storage.md` — the store contract
   (`IMediaStore`, the `MediaObject` content-hash identity, the avatar
   serving idiom C-MED·1/2/3/6) and — critically — the **"Not decided
   here"** section whose "follow-on lanes … post attachments" is exactly
   what ADR 0025 resolves.
3. `src/Kumunita.Web/Controllers/ProfileController.cs` §Avatar/AvatarUpload
   (≈ lines 340–430) — the two idioms to copy: the **serving** route
   (404-before-decision, one `CanAsync`, `nosniff`) and the **upload**
   guards (empty → 400, oversize → 413, disallowed type → 415, guards
   before any write).
4. `docs/design/multilingual-design.md` §Invariants + §FACES +
   §Pinned contract — the **table shape** to emulate for R·1–R·7 / R1–R8
   (numbered invariants, each with a one-line "Pinned where" column;
   FACES table with a "Pinned by" column).
5. `src/Kumunita.Core/Posts/Post.cs` + `src/Kumunita.Core/Posts/PostReply.cs`
   — the additive-field doc-comment convention to mirror on `ImageIds`
   ("the Nth additive field after …" — `ImageIds` will be the 5th on
   `Post`, the 4th on `PostReply`).
6. `src/Kumunita.Core/Localization/LocalizedPage.cs` — the third
   `ImageIds` owner (one small file; its `Body` doc-comment says
   "Markdown (the single page engine)" — the RC lane makes that comment
   true for every body).

## Deliverables (2 files, both new)

### 1. `docs/design/rich-content-design.md` (~250 lines)

Header: the same three-tier contract note the multilingual design doc
has (this file = primary; the register = secondary;
`docs/plans-milestones/done/rich-content-handoff-notes.md` = scratch).
Then, in order:

- `## Context` — the plain-text status quo (bodies stored verbatim,
  rendered `whitespace-pre-line` or raw; no images anywhere except the
  avatar); the renderer that already exists but only serves
  `LocalizedPage`; the ADR 0011 store with exactly one consumer.
- `## Scope` — **In:** the four `ImageIds` ADDs; the renderer image
  extension; the `GET /content-image/{id}` serving route; the
  `POST /content-image` upload lane; the composer control on the four
  surfaces (post / announcement / group-post / static-page editor); the
  render switch (`RenderHtml` + `PlainTextPreview`); the `.rc-body` /
  `.rc-image` CSS. **Out (named deferrals, not a renumber):** video/
  audio, image transforms (crop/resize), multi-image reordering UI, any
  WYSIWYG or third-party editor, tables/footnotes in the renderer.
- `## Invariants (pinned for the RC lane)` — **R·1–R·7**, exactly:
  - **R·1** — **One renderer:** `MarkdownRenderer.RenderHtml` is the
    single Markdown→HTML path for every body — UGC and platform alike.
    No second renderer; no `Html.Raw` of unrendered body content; no
    `whitespace-pre-line` on a body that is Markdown source. Pinned
    where: U02 (extension), U06 (the switch).
  - **R·2** — **Escape-first stands:** the image extension does not
    weaken the existing construction (escape before inline rules; the
    `src` allowlist is **stricter** than the link whitelist — platform
    route shape only, no remote `src`). Pinned where: U02.
  - **R·3** — **References are data, not URLs-in-prose:** an image in a
    body is `![alt](/content-image/{id})` where `id` is a
    `MediaObject.Id`, **and** the owning resource's `ImageIds` contains
    it. `ImageIds` is populated **server-side** by parsing the body's
    route-shaped links on every create/edit write lane (the Web layer's
    job — Core stays HTTP-free AND body-parse-free: the draft records
    carry `ImageIds`, the service writes them verbatim). A well-formed
    link with no matching owner renders as plain text (the renderer
    cannot know) and 404s if fetched (the route is the defense). Pinned
    where: U04 (the parse), U03 (the route).
  - **R·4** — **The serving route defers to the owning resource's
    decision:** `GET /content-image/{id}` → `IMediaStore.GetAsync` (miss
    ⇒ 404, **zero** audit rows) → bounded reverse lookup across the
    four owners (miss ⇒ 404, **zero** rows — **orphan is inert, there
    is no GlobalAdmin branch**) → UGC owner: **one** frozen
    `CanAsync(Read)` on the owner (Allow **and** Deny each emit the
    seam's own one audit row, the owner's `TargetKind`); platform-page
    owner: **no call, zero rows** (public by construction). Deny ⇒
    **404**, not 403 (existence doesn't leak — the avatar route's
    precedent). No new `AccessAction` id (C-MED·1). Pinned where: U03.
  - **R·5** — **Core stays HTTP-free (C-MED·6 / ADR 0006-D):** the
    reverse lookup is a read seam on the existing services (the
    `Post`/`PostReply` owner lives on `PostService`, the announcement
    owner on `AnnouncementService`, the page owner on the
    `LocalizedPage`-owning service); `IFormFile`/`Stream` never cross
    into Core; the lookup itself is **un-audited** (the audit row
    belongs to the route's `CanAsync` call). Pinned where: U03.
  - **R·6** — **Upload boundary is ADR 0011's, verbatim:** the same
    allowlist (`image/jpeg|png|webp|gif` — SVG excluded), the same
    `MediaOptions.MaxBytes` cap (5 MiB default), the same
    guards-before-write ordering (empty → 400, oversize → 413,
    disallowed type → 415 — no file written on any guard), one
    `IMediaStore.PutAsync` write. Pinned where: U04.
  - **R·7** — **Zero schema migrations:** `Body` stays a `string` on
    every document; the four `ImageIds` fields are additive (ADR 0004
    §B.1 — delta-detected, idempotent, no seed reset); existing
    plain-text content re-renders under the Markdown rules (plain text
    is valid Markdown; the paragraph rule wraps it — strictly better
    than raw text). Pinned where: U03 (the fields), U06 (the switch),
    U07 (the regression pin).
- `## FACES (pinned, 8)` — R1–R8, exactly (the "Pinned by" column
  names the invariants):
  - **R1** a resident writes a post with `**bold**` + a list + an
    attached image → all three render on the detail page. (R·1, R·3, R·6)
  - **R2** a body links a **remote** image (`https://…`) → it renders
    as plain escaped text, not an `<img>`. (R·2)
  - **R3** a non-member requests the audience-restricted post's image →
    **404** (no leak) + exactly one `Read` **Deny** audit row. (R·4)
  - **R4** a member requests the same image → **200** + the stored
    bytes + `nosniff` + exactly one `Read` **Allow** row; the `<img>`
    in the body loads it. (R·4, R·1)
  - **R5** an orphan image (in the store, referenced by **no**
    `ImageIds`) → **404 for every actor including GlobalAdmin**,
    **zero** audit rows (no owner branch exists to audit). (R·4, R·5)
  - **R6** a GlobalAdmin edits the **about** page with an image → it
    renders on `/about` for an **unauthenticated** visitor, **zero**
    `CanAsync` calls (the platform branch). (R·1, R·4)
  - **R7** a resident uploads a 6 MiB PNG → **413**, no file written,
    no `MediaObject` row. (R·6)
  - **R8** pre-existing plain-text posts (no Markdown, no images)
    render after the switch with every word preserved. (R·7, R·1)
- `## Pinned contract (exact C#)` — every unit matches verbatim:
  - The four fields, on all four owners (Post, PostReply,
    LocalizedPage, **Announcement**), **identical shape**:
    `public IReadOnlyList<string> ImageIds { get; set; } = [];` — with
    the additive-field doc-comment naming R·3/R·7 + the field's ordinal
    position (5th on `Post` after `Status`/`GroupId`/`LanguageCode`/
    `DeletedAt`; 4th on `PostReply` after `Modified`/`LanguageCode`/
    `DeletedAt`; 1st on `LocalizedPage`; ordinal on `Announcement` per
    its actual field order — the ordinal is documentation, not
    behavior). The announcement owner is named here (not deferred to
    U03) because R·4's serving branch already names the announcement
    as a UGC owner — the field must exist for that branch's reverse
    lookup to read it.
  - The draft-record ADDs (U04/U05): `PostDraft`, `GroupPostDraft`
    (both in `Kumunita.Core.Posts`) gain `IReadOnlyList<string>
    ImageIds = []` as a **new trailing record parameter with a default**
    (source-compatible — the M3 tests' `PostDraft` constructor calls
    keep compiling; **record positional-parameter order matters: append
    last, never insert mid-list**). The announcement draft shape and the
    `LocalizedPage` save path pin their exact binding points when
    U05's entry reads confirm them (the design doc records the
    confirmed names — if a name cannot be confirmed from the code, the
    unit that touches it records it here in a `§Pinned contract
    amendment (U<m>)` sub-line — an **append-only** amendment, the only
    permitted post-U01 edit to this section).
  - The serving route (U03): `public sealed class ContentImageController
    (IMediaStore media, IAuthorizationService authz, PostService posts,
    IAnnouncementService announcements, ITranslationProvider pages)` —
    `GET /content-image/{id}` (the id validated: 1–128 lowercase hex
    chars), and the 5-step ordering pinned in R·4 (store-miss → 404;
    owner-miss → 404; UGC → one `CanAsync`, Deny → 404; platform →
    direct serve; `File(stream, stored.ContentType)` +
    `X-Content-Type-Options: nosniff`).
  - The upload action (U04): `POST /content-image`, `[Authorize]`,
    `[FromForm] IFormFile? file` — the guard ordering pinned in R·6,
    then `PutAsync(bytes, file.FileName, file.ContentType, actorId)`,
    response `Json(new { id = stored.Id })` (the id is a content hash —
    not secret, but the route 404s until a referencing doc exists,
    R·4).
  - The reverse-lookup seams (U03) — **pinned names:**
    `Task<Post?> PostService.FindPostByImageIdAsync(string mediaId)`;
    `Task<PostReply?> PostService.FindReplyByImageIdAsync(string mediaId)`;
    `Task<Announcement?> IAnnouncementService.FindByImageIdAsync(string mediaId)`
    (add to the interface — a read seam, ADR 0006-E lane, mirroring
    `GetComponentsAsync`); the `LocalizedPage` owner's method name is
    pinned here **only once U03's entry reads confirm which service
    owns `GetPageAsync`** (record it in an amendment sub-line, same
    rule as the drafts).
  - The renderer rule (U02): the `src` predicate's **pinned name**
    `IsSafeImageSrc(string src)` — two accept branches: (a)
    `/content-image/` + 1–128 lowercase hex chars, (b) a relative path
    with no scheme and no leading `//`; every other input (any scheme —
    `http:`, `https:`, `data:`, `javascript:` — or a malformed id)
    rejects and the whole `![alt](src)` renders as **plain escaped
    text** (the link-rejection precedent). The emission is
    `<img src="{esc}" alt="{alt}" class="rc-image" loading="lazy" />`
    with the link-emission's attribute-escaping (`&` → `&amp;`, `"` →
    `&quot;`).
  - The preview helper (U06): `public static string
    MarkdownRenderer.PlainTextPreview(string? markdown, int maxLen =
    200)` — strip order pinned: images → their `alt`; links → their
    `label`; then `**`, `*`, `` ` `` markers; heading `#`s; list
    markers (`- `/`* `/`N.`); collapse all whitespace to single spaces;
    truncate to `maxLen` appending `…` when cut. Returns **text** (for
    a `<span>`/`<p>` context — no HTML).
  - The composer JS (U04): `src/Kumunita.Web/client/lib/insert-image.ts`
    exposing `bindInsertImage(root: HTMLElement)` (finds the
    `input[type="file"][data-insert-image]` +
    `textarea[data-image-target]` pair, wires upload → append). The
    **pinned alt rule:** the file name without extension, truncated to
    40 chars, appended as `![{alt}](/content-image/{id})` at the
    textarea's cursor position. The upload `fetch` is CSRF-aware per
    the `client/lib/api.ts` convention (U04 confirms the exact header
    and records any deviation in the handoff note).
- `## Pinned seam tests (exact names)` — the **authoritative** list of
  **20** tests (7 renderer + 13 seam; U02 authors the first file; U07
  the other two):
  - `tests/Kumunita.Web.Tests/MarkdownRendererTests.cs` (U02):
    `Image_PlatformRouteSrc_RendersImgTag` ·
    `Image_RemoteSrc_RendersAsPlainText` ·
    `Image_DataUriSrc_RendersAsPlainText` ·
    `Image_MalformedHexId_RendersAsPlainText` ·
    `Image_InlineInParagraph_StaysInParagraph` ·
    `Bold_Italic_List_Heading_StillRender` ·
    `HostileMarkup_StillEscaped`
  - `tests/Kumunita.Core.Tests/ContentImageOwnershipTests.cs` (U07):
    `R3_ImageIdsPopulatedFromBodyLinks` · `R3_ImageIdsEmpty_WhenNoLinks` ·
    `R5_ReverseLookup_FindsOwningPost` ·
    `R5_ReverseLookup_FindsOwningReply` ·
    `R7_PostPoco_FieldSetUnmodifiedExceptImageIds`
  - `tests/Kumunita.Web.Tests/ContentImageServingTests.cs` (U07):
    `R4_Member_Allows_200_OneAllowAuditRow` ·
    `R4_NonMember_Denies_404_OneDenyAuditRow` ·
    `R5_Orphan_404_ForGlobalAdmin_ZeroAuditRows` ·
    `R4_PlatformPageOwner_ZeroCanAsyncCalls`
  - `tests/Kumunita.Web.Tests/ContentImageUploadTests.cs` (U07):
    `Upload_Empty_400_NoFileWritten` ·
    `Upload_Oversize_413_NoFileWritten` ·
    `Upload_DisallowedType_Svg_415_NoFileWritten` ·
    `Upload_Valid_200_ReturnsJsonId`
- `## Acceptance gate (U08 records)` — the three-test shape:
  **closed loop** (a post created with a bold line + an attached image
  renders both on its detail page; the image serves 200 for a member);
  **handoff** (a non-member requesting the same image gets 404 + a
  Deny audit row — the authorization handoff); **part-vs-whole** (the
  22 pinned tests above pass together **and** the pre-existing
  `PostServiceTests` / announcement / multilingual suites pass
  **unmodified** — the R·7 zero-migration pin made executable).
- `## Drift guard` — the 7 invariants, the 8 FACES, every pinned C#
  shape above, and the 20 test names are frozen once this file is
  written. Any mismatch found by a later unit is a
  `## U<m> — Drift pause` section in the handoff note (unit-series
  rule 6), **not** a silent edit. The **only** permitted post-U01
  edit to this file is the append-only `§Pinned contract amendment`
  sub-line mechanism above (used when a U03/U05 entry-read confirms a
  name U01 could not).

### 2. `docs/adr/0025-rich-content-markdown-and-content-images.md`

Header: `Status: Accepted`, `Date: 2026-09-14`, `Amends: 0011`
(the "follow-on lanes" non-decision is resolved for the
post/reply/announcement/about lane — group logos etc. remain future
lanes, as ADR 0011 said).

- **Context** — the two open questions: (a) UGC bodies are raw text
  today with no formatting; the platform already ships an escape-first
  Markdown renderer for static pages that UGC deliberately ignores;
  (b) images exist nowhere but the avatar (ADR 0011's single consumer);
  the ADR's "Not decided here" named post attachments as a follow-on
  lane. The constraints: the XSS bar (the renderer's escape-first
  construction), the audit model (every audience-restricted read is
  logged), `Core` HTTP-free, Marten-owns-documents, boring + one
  database (no new dependency for rendering is the bar to clear —
  `Markdig`/`MarkdownSharp` would be a new library for a subset the
  existing ~200-line renderer already covers except images).
- **Decision** —
  (a) **`MarkdownRenderer` is the one body renderer.** It is extended
  with `![alt](src)` images under a `src` allowlist **stricter** than
  the link whitelist (platform route shape only — a remote `src` is a
  tracking/exfiltration surface and is rejected to plain text). A
  second rendering library is **not** adopted: the existing renderer
  is small, auditable, and its escape-first construction is the
  security story; the subset it lacks (tables, footnotes) is the
  subset a neighborhood platform does not need.
  (b) **Content images are `MediaObject` docs referenced by `ImageIds`
  on the owning document** (`Post`, `PostReply`, `LocalizedPage` —
  additive, ADR 0004 §B.1), populated server-side by parsing the body's
  `/content-image/{id}` links, served by `GET /content-image/{id}`
  through the owning resource's single `Read` decision (UGC) or
  directly (platform pages), 404 on every miss (store-miss,
  owner-miss, Deny) so orphan bytes are inert and existence never
  leaks. **No new `AccessAction`, no new `IMediaStore` seam, no
  signature on the frozen `IAuthorizationService`** — the route
  reuses the existing avatar idiom (C-MED·1/2/3/6) verbatim.
- **Consequences** — the four `ImageIds` ADDs ride the ADR 0004 §B.1
  additive path (delta-detected, idempotent, no seed reset — zero
  migrations); the serving route's 404-orphan posture is the C-MED·7
  "orphan file is inert" rule extended to "orphan doc is inert
  **and unfindable**"; the renderer gains one rule and one stricter
  allowlist; the upload boundary is ADR 0011's guards verbatim (the
  `POST /content-image` lane is the second consumer of the same
  chokepoint); the composer is a textarea + a plain-TS module
  (`tsc`-only constraint held — **no editor dependency enters
  `package.json`**).
- **Not decided here (explicit non-decisions)** — video/audio
  attachment; server-side image transforms (crop/resize); multi-image
  reordering UI; any WYSIWYG/third-party editor; tables/footnotes in
  the renderer; group logos and other follow-on ADR 0011 lanes
  (each is a future lane gated on its owning resource, as ADR 0011
  already said).

## Exit criteria

- Both files exist with every section named above.
- The design doc's §Pinned contract contains: the field shape (once,
  applied to four owners), the two draft-record ADDs, the route
  signature + 5-step ordering, the upload action, the four
  reverse-lookup names, the renderer rule (name + 2-branch predicate +
  exact `<img>` emission), the `PlainTextPreview` strip order, the JS
  surface + alt rule.
- The design doc's §Pinned seam tests names **exactly** the 20 tests
  in the four files above (no more, no fewer).
- The ADR names ADR 0011 as `Amends:` and its "Not decided here"
  section lists the five deferrals.
- **No build** (no code touched). **No test file created.**
- **Handoff note** (append to
  `docs/plans-milestones/done/rich-content-handoff-notes.md`): a
  section starting `## U01 — design doc + ADR 0025`, 6–8 lines:
  (a) the 7 invariants by id (R·1–R·7); (b) the 8 FACES by id (R1–R8);
  (c) the four `ImageIds` owners (Post, PostReply, LocalizedPage,
  Announcement); (d) the serving route path (exact
  string `/content-image/{id}`); (e) the upload route (exact); (f) the
  20 test names' four files (7 in U02's file; 5 + 4 + 4 = 13 in U07's
  three files — write the four file→count lines verbatim from
  §Pinned seam tests);
  (g) the ADR's two decisions (a)/(b) in
  one line each.
- **Then move this file:** `Move-Item
  docs\plans-milestones\in-progress\rich-content-u01-plan.md
  docs\plans-milestones\done\rich-content-u01-plan.md` — the move is
  the last action of the unit (done/ is the completion signal).
