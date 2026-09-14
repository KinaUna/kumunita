# Rich content (`RC`) — Markdown bodies + in-content images

> **Three-tier contract.** This file is the **primary** tier of the RC lane:
> it pins the invariant numbers (R·1–R·7), the FACES (R1–R8), the exact C#
> of every seam, the pinned seam-test names, the acceptance gate, and the
> drift guard. The register
> (`docs/plans-milestones/plan-rich-content.md`) is the **secondary** tier
> (unit-level deliverables + exit criteria).
> `docs/plans-milestones/done/rich-content-handoff-notes.md` is the
> **scratch** tier (one short section per unit, appended, never rewritten).
> When the three disagree, **this file wins for the pinned shapes**; the
> register wins for *which files exist* and *what each unit does*.

## Context

Posts, replies, announcements, and the static pages (about / terms / help)
are currently **raw plain text**: the body is stored verbatim as a `string`,
rendered with `whitespace-pre-line` or raw `@Model.Body`, and residents have
no way to bold a heading, start a list, or show a photo.

The platform already ships the renderer: `MarkdownRenderer`
(`Kumunita.Web.Security`) is an escape-first, XSS-safe by construction,
~200-line Markdown → HTML engine that renders `LocalizedPage.Body` (about /
terms / help) — headings, paragraphs, lists, bold/italic/code, and links —
but **every UGC surface deliberately ignores it**.

The ADR 0011 media store (`IMediaStore` / `MediaObject`, content-addressed,
5 MiB cap, `image/jpeg|png|webp|gif`) has exactly **one** consumer: the
profile avatar (`ProfileController.Avatar` / `AvatarUpload`). ADR 0011's
"not decided here" explicitly names "post attachments" as a follow-on lane,
gated on the owning resource's audience. **This lane is that follow-on lane.**

The constraints this lane must honor (all pre-existing, not new):

- **XSS bar** — the renderer's escape-first construction (R·2).
- **Audit-by-default** — every audience-restricted read is logged (R·4).
- **`Core` stays HTTP-free** (ADR 0006-D) — `IFormFile`/`Stream` never cross
  into Core (R·5).
- **Marten owns the domain documents** (ADR 0004 §B) — the `ImageIds`
  fields are additive POCO fields, delta-detected, idempotent (R·7).
- **Lean + Boring, one database** — no new rendering library (ADR 0025
  decision (a)); no new `AccessAction`, no new `IMediaStore` method (ADR 0025
  decision (b); C-MED·1).

## Scope

**In (this lane ships):**

- The four `ImageIds` additive fields: `Post.ImageIds`,
  `PostReply.ImageIds`, `LocalizedPage.ImageIds`, `Announcement.ImageIds`
  (all `IReadOnlyList<string>` of `MediaObject.Id` values).
- The `MarkdownRenderer` image extension: `![alt](src)` rendering under the
  `IsSafeImageSrc` predicate (R·2, R·3).
- The `GET /content-image/{id}` serving route: decision-deferred,
  404-orphan, no new `AccessAction` (R·4, R·5).
- The `POST /content-image` upload lane: ADR 0011's boundary verbatim (R·6).
- The composer image control on four surfaces: post, announcement,
  group-post, static-page editor (a `<textarea>` + "Insert image" button +
  live preview pane — **no WYSIWYG, no third-party editor**).
- The render switch: every UGC body → `MarkdownRenderer.RenderHtml` in
  `.rc-body`; every feed preview → `PlainTextPreview` (R·1, R·7).
- The `.rc-body` / `.rc-image` CSS (one CSS deliverable).

**Out (named deferrals for a future RC-2 lane, not a renumber):**

- Video / audio attachment.
- Server-side image transforms (crop / resize).
- Multi-image reordering UI.
- Any WYSIWYG or third-party editor (the `tsc`-only constraint stands).
- Tables / footnotes in the renderer (the "intentionally out of scope" set
  stays).

## Invariants (pinned for the RC lane)

Seven invariants, **R·1–R·7**. Each is one idea, pinned so every unit and
every FACES row references a stable number. Adding a new invariant (R·8+)
requires a design-doc edit in the same commit as the feature that earns it;
renumbering is a breaking change and is not allowed mid-lane.

| # | Invariant | Pinned where |
|---|-----------|--------------|
| **R·1** | **One renderer:** `MarkdownRenderer.RenderHtml` is the single Markdown → HTML path for every body — UGC and platform alike. No second renderer; no `Html.Raw` of unrendered body content; no `whitespace-pre-line` on a body that is Markdown source. | **U02** (extension) · **U06** (the switch) |
| **R·2** | **Escape-first stands:** the image extension does not weaken the existing construction (escape before inline rules; the `src` allowlist is **stricter** than the link whitelist — platform route shape only, no remote `src`). | **U02** |
| **R·3** | **References are data, not URLs-in-prose:** an image in a body is `![alt](/content-image/{id})` where `id` is a `MediaObject.Id`, **and** the owning resource's `ImageIds` contains it. `ImageIds` is populated **server-side** by parsing the body's route-shaped links on every create/edit write lane (the Web layer's job — Core stays HTTP-free AND body-parse-free: the draft records carry `ImageIds`, the service writes them verbatim). A well-formed link with no matching owner renders as plain text (the renderer cannot know) and 404s if fetched (the route is the defense). | **U04** (the parse) · **U03** (the route) |
| **R·4** | **The serving route defers to the owning resource's decision:** `GET /content-image/{id}` → `IMediaStore.GetAsync` (miss ⇒ 404, **zero** audit rows) → bounded reverse lookup across the four owners (miss ⇒ 404, **zero** rows — **orphan is inert, there is no GlobalAdmin branch**) → UGC owner: **one** frozen `CanAsync(Read)` on the owner (Allow **and** Deny each emit the seam's own one audit row, the owner's `TargetKind`); platform-page owner: **no call, zero rows** (public by construction). Deny ⇒ **404**, not 403 (existence doesn't leak — the avatar route's precedent). No new `AccessAction` id (C-MED·1). | **U03** |
| **R·5** | **Core stays HTTP-free (C-MED·6 / ADR 0006-D):** the reverse lookup is a read seam on the existing services (`PostService` for `Post`/`PostReply`, `AnnouncementService` for `Announcement`, the `LocalizedPage`-owning service for `LocalizedPage`); `IFormFile`/`Stream` never cross into Core; the lookup itself is **un-audited** (the audit row belongs to the route's `CanAsync` call). | **U03** |
| **R·6** | **Upload boundary is ADR 0011's, verbatim:** the same allowlist (`image/jpeg\|png\|webp\|gif` — SVG excluded), the same `MediaOptions.MaxBytes` cap (5 MiB default), the same guards-before-write ordering (empty → 400, oversize → 413, disallowed type → 415 — no file written on any guard), one `IMediaStore.PutAsync` write. | **U04** |
| **R·7** | **Zero schema migrations:** `Body` stays a `string` on every document; the four `ImageIds` fields are additive (ADR 0004 §B.1 — delta-detected, idempotent, no seed reset); existing plain-text content re-renders under the Markdown rules (plain text is valid Markdown; the paragraph rule wraps it — strictly better than raw text). | **U03** (the fields) · **U06** (the switch) · **U07** (the regression pin) |

## FACES (pinned, 8)

Eight resident- / admin-facing scenarios, **R1–R8**, each exercising one or
more invariants. The seam tests (§Pinned seam tests) cover these 1:1.

| # | Outcome (what a resident / admin sees / can do) | Pinned by |
|---|---|---|
| **R1** | a resident writes a post with `**bold**` + a list + an attached image → all three render on the detail page. | R·1, R·3, R·6 |
| **R2** | a body links a **remote** image (`https://…`) → it renders as plain escaped text, not an `<img>`. | R·2 |
| **R3** | a non-member requests the audience-restricted post's image → **404** (no leak) + exactly one `Read` **Deny** audit row. | R·4 |
| **R4** | a member requests the same image → **200** + the stored bytes + `nosniff` + exactly one `Read` **Allow** row; the `<img>` in the body loads it. | R·4, R·1 |
| **R5** | an orphan image (in the store, referenced by **no** `ImageIds`) → **404 for every actor including GlobalAdmin**, **zero** audit rows (no owner branch exists to audit). | R·4, R·5 |
| **R6** | a GlobalAdmin edits the **about** page with an image → it renders on `/about` for an **unauthenticated** visitor, **zero** `CanAsync` calls (the platform branch). | R·1, R·4 |
| **R7** | a resident uploads a 6 MiB PNG → **413**, no file written, no `MediaObject` row. | R·6 |
| **R8** | pre-existing plain-text posts (no Markdown, no images) render after the switch with every word preserved. | R·7, R·1 |

## Pinned contract (exact C#)

Every unit matches these shapes verbatim. A mismatch is a `## U<m> — Drift
pause`, not a silent edit.

### The four `ImageIds` fields (U03)

All four owners — `Post`, `PostReply`, `LocalizedPage`, `Announcement` —
carry the **identical** shape:

```csharp
public IReadOnlyList<string> ImageIds { get; set; } = [];
```

Additive-field doc-comment convention (mirrors the existing `Status` /
`GroupId` / `LanguageCode` / `DeletedAt` comments):

| Owner | Ordinal (documentation, not behavior) |
|---|---|
| `Post` | 5th additive field after `Status` (M3b), `GroupId` (ADR 0013), `LanguageCode` (ADR 0018), `DeletedAt` (ADR 0024) |
| `PostReply` | 4th additive field after `Modified` (ADR 0016), `LanguageCode` (ADR 0018), `DeletedAt` (ADR 0024) |
| `LocalizedPage` | 1st additive field (the existing set is `Id`/`Slug`/`LanguageCode`/`Title`/`Body`/`Updated`) |
| `Announcement` | 1st additive field (the existing set is `Id`/`AuthorId`/`Title`/`Body`/`Scope`/`CommunityId`/`Pinned`/`Created`/`Modified`/`LanguageCode`) |

Each doc-comment names R·3/R·7 and the ADR 0004 §B.1 additive path.

### Draft-record ADDs (U04/U05)

`PostDraft` and `GroupPostDraft` (both in `Kumunita.Core.Posts`) gain:

```csharp
IReadOnlyList<string> ImageIds = []
```

as a **new trailing record parameter with a default** (source-compatible —
the M3 tests' `PostDraft` constructor calls keep compiling; **record
positional-parameter order matters: append last, never insert mid-list**).

The announcement draft shape and the `LocalizedPage` save path pin their
exact binding points when U05's entry reads confirm them. If a name cannot
be confirmed from the code, the unit that touches it records it here in a
`§Pinned contract amendment (U<m>)` sub-line — an **append-only** amendment,
the only permitted post-U01 edit to this section.

### The serving route (U03)

```csharp
public sealed class ContentImageController(
    IMediaStore media,
    IAuthorizationService authz,
    PostService posts,
    IAnnouncementService announcements,
    ITranslationProvider pages)
```

`GET /content-image/{id}` — the `id` is validated: 1–128 lowercase hex
chars. The 5-step ordering (pinned in R·4):

1. `IMediaStore.GetAsync(id)` → `null` ⇒ **404** (zero audit rows).
2. Bounded reverse lookup across the four owners (see below) → no owner
   found ⇒ **404** (zero rows; **orphan is inert — no GlobalAdmin branch**).
3. UGC owner: **one** `CanAsync(actorId, AccessAction.Read, ownerAdapter)`
   → Deny ⇒ **404** (not 403); Allow ⇒ continue.
4. Platform-page owner: skip step 3 (public by construction).
5. `IMediaStore.OpenReadAsync(id)` → `File(stream, stored.ContentType)` +
   `Response.Headers["X-Content-Type-Options"] = "nosniff"`.

### The upload action (U04)

```csharp
[HttpPost("/content-image")]
[Authorize]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Upload([FromForm] IFormFile? file)
```

Guard ordering (pinned in R·6): empty → 400, oversize → 413, disallowed
type → 415 (no file written on any guard), then
`IMediaStore.PutAsync(bytes, file.FileName, file.ContentType, actorId)`,
response `Json(new { id = stored.Id })`.

### The reverse-lookup seams (U03) — pinned names

| Seam | Exact signature |
|---|---|
| Post owner | `Task<Post?> PostService.FindPostByImageIdAsync(string mediaId)` |
| Reply owner | `Task<PostReply?> PostService.FindReplyByImageIdAsync(string mediaId)` |
| Announcement owner | `Task<Announcement?> IAnnouncementService.FindByImageIdAsync(string mediaId)` (added to the interface — a read seam, ADR 0006-E lane) |
| `LocalizedPage` owner | **Not pinned here** — the owning service is confirmed by U03's entry reads (which type owns `GetPageAsync`); recorded in a `§Pinned contract amendment (U03)` sub-line if the name differs from the obvious candidate |

> **`§Pinned contract amendment (U03)`** — the `LocalizedPage`-owning service
> read seam is **`ITranslationProvider`** (impl `TranslationProvider`,
> `Kumunita.Core.Localization`), confirmed by U03's entry reads:
> `StaticPagesController`'s ctor (the plan's tie-break) injects
> `ITranslationProvider` and reads pages through
> `ITranslationProvider.GetPageAsync`. The `LocalizedPage` reverse-lookup seam
> is therefore `Task<LocalizedPage?>
> ITranslationProvider.FindPageByImageIdAsync(string mediaId)` (added to the
> interface + its implementation). This **matches** the obvious candidate the
> design doc's own `ContentImageController` signature names (`ITranslationProvider
> pages`) — recorded here to close the "Not pinned here" row per the
> append-only amendment mechanism (the only permitted post-U01 edit).

### The renderer image rule (U02)

The `src` predicate's **pinned name**: `IsSafeImageSrc(string src)`.

Two accept branches:

1. `/content-image/` followed by 1–128 lowercase hex chars
   (`[0-9a-f]{1,128}`).
2. A relative path with no scheme and no leading `//`.

Every other input (any scheme — `http:`, `https:`, `data:`,
`javascript:` — or a malformed id) **rejects** and the whole
`![alt](src)` renders as **plain escaped text** (the link-rejection
precedent, verbatim pattern).

The `<img>` emission (exact):

```html
<img src="{esc}" alt="{alt}" class="rc-image" loading="lazy" />
```

Attribute-escaping: `&` → `&amp;`, `"` → `&quot;` (the link-emission's
existing rule). **No** `width`/`height` (CSS in U06 owns sizing).

### The preview helper (U06)

```csharp
public static string MarkdownRenderer.PlainTextPreview(string? markdown, int maxLen = 200)
```

Strip order (pinned):

1. `![alt](src)` → the `alt` text.
2. `[label](url)` → the `label` text.
3. `**bold**` / `*italic*` / `` `code` `` → the inner text (markers stripped).
4. Heading `#`s stripped.
5. List markers (`- ` / `* ` / `N. `) stripped.
6. Collapse all whitespace to single spaces.
7. Truncate to `maxLen`, appending `…` when cut.

Returns **text** (for a `<span>`/`<p>` context — no HTML).

### The composer JS module (U04)

`src/Kumunita.Web/client/lib/insert-image.ts` exposing:

```ts
export function bindInsertImage(root: HTMLElement): void
```

Finds the `input[type="file"][data-insert-image]` +
`textarea[data-image-target]` pair within `root`, wires the file input's
`change` → upload `fetch(POST /content-image, FormData)` → on 200,
appends `![{alt}](/content-image/{id})` at the textarea's cursor position.

**Pinned alt rule:** the file name without extension, truncated to 40 chars.

The upload `fetch` is CSRF-aware per the `client/lib/api.ts` convention
(U04 confirms the exact header and records any deviation in the handoff
note).

## Pinned seam tests (exact names)

The **authoritative** list of **20** tests (7 renderer + 13 seam; U02
authors the first file; U07 the other three).

### `tests/Kumunita.Web.Tests/MarkdownRendererTests.cs` (U02) — 7 tests

1. `Image_PlatformRouteSrc_RendersImgTag`
2. `Image_RemoteSrc_RendersAsPlainText`
3. `Image_DataUriSrc_RendersAsPlainText`
4. `Image_MalformedHexId_RendersAsPlainText`
5. `Image_InlineInParagraph_StaysInParagraph`
6. `Bold_Italic_List_Heading_StillRender`
7. `HostileMarkup_StillEscaped`

### `tests/Kumunita.Core.Tests/ContentImageOwnershipTests.cs` (U07) — 5 tests

8. `R3_ImageIdsPopulatedFromBodyLinks`
9. `R3_ImageIdsEmpty_WhenNoLinks`
10. `R5_ReverseLookup_FindsOwningPost`
11. `R5_ReverseLookup_FindsOwningReply`
12. `R7_PostPoco_FieldSetUnmodifiedExceptImageIds`

### `tests/Kumunita.Web.Tests/ContentImageServingTests.cs` (U07) — 4 tests

13. `R4_Member_Allows_200_OneAllowAuditRow`
14. `R4_NonMember_Denies_404_OneDenyAuditRow`
15. `R5_Orphan_404_ForGlobalAdmin_ZeroAuditRows`
16. `R4_PlatformPageOwner_ZeroCanAsyncCalls`

### `tests/Kumunita.Web.Tests/ContentImageUploadTests.cs` (U07) — 4 tests

17. `Upload_Empty_400_NoFileWritten`
18. `Upload_Oversize_413_NoFileWritten`
19. `Upload_DisallowedType_Svg_415_NoFileWritten`
20. `Upload_Valid_200_ReturnsJsonId`

## Acceptance gate (U08 records)

The three-test shape:

- **closed loop** — a post created with a bold line + an attached image
  renders both on its detail page; the image serves 200 for a member.
- **handoff** — a non-member requesting the same image gets 404 + a Deny
  audit row (the authorization handoff).
- **part-vs-whole** — the 20 pinned tests above pass together **and** the
  pre-existing `PostServiceTests` / announcement / multilingual suites pass
  **unmodified** (the R·7 zero-migration pin made executable).

## Drift guard

The 7 invariants (R·1–R·7), the 8 FACES (R1–R8), every pinned C# shape
above, and the 20 test names are **frozen** once this file is written. Any
mismatch found by a later unit is a `## U<m> — Drift pause` section in the
handoff note (unit-series rule 6), **not** a silent edit.

The **only** permitted post-U01 edit to this file is the append-only
`§Pinned contract amendment (U<m>)` sub-line mechanism (used when a
U03/U05 entry-read confirms a name U01 could not). All other edits to this
file after U01 are a drift-guard failure.
