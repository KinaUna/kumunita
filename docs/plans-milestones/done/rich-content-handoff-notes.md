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

## U03 — ImageIds + reverse lookup + serving route

- **Four `ImageIds` owners (ordinals as written):** `Post` = 5th additive (after `Status`/`GroupId`/`LanguageCode`/`DeletedAt`) · `PostReply` = 4th additive (after `Modified`/`LanguageCode`/`DeletedAt`) · `LocalizedPage` = 1st additive (base set `Id`/`Slug`/`LanguageCode`/`Title`/`Body`/`Updated`) · `Announcement` = **2nd** additive (after ADR 0018's `LanguageCode`, which is the 1st — the design doc's "1st" ordinal corrected to the real field order; the ordinal is documentation, not behavior). All four are `sealed class` POCOs (confirmed — no record).
- **Four reverse-lookup seams (names + exact service types):** `PostService.FindPostByImageIdAsync(string)` → `Post?` · `PostService.FindReplyByImageIdAsync(string)` → `PostReply?` · `IAnnouncementService.FindByImageIdAsync(string)` → `Announcement?` (impl `AnnouncementService`) · `ITranslationProvider.FindPageByImageIdAsync(string)` → `LocalizedPage?` (impl `TranslationProvider`). All un-audited (R·5), read-only, `OrderBy(Created|Updated)` ascending, null when absent.
- **Route `GET /content-image/{id}` (no `[Authorize]`), 5-step ordering:** 1) id must be 1–128 `[0-9a-f]` else **400** · 2) `IMediaStore.GetAsync` miss → **404** (0 rows) · 3) post → reply → announcement → page, all null → **404** (orphan inert, no GlobalAdmin branch) · 4) UGC post → one `CanAsync(Read, PostToAuditableResource)`, Deny → **404** (not 403); UGC reply / announcement → **404** (drift pause, below); platform page → serve directly (0 calls) · 5) `OpenReadAsync` → `File(stream, stored.ContentType)` + `X-Content-Type-Options: nosniff` (avatar idiom, verbatim — the header is set in the action, not a filter).
- **Audit-row pin:** one `Read` **Allow** *and* one `Read` **Deny** on the UGC-post branch only (via `PostToAuditableResource`, `TargetKind` "post"); **zero** on the platform-page owner; **zero** on any 404 (store-miss, owner-miss/orphan, reply/announcement fail-closed). No `[Authorize]`, no `Challenge()` — anonymous to a public post's image is served; a non-member gets 404 + one Deny row (the `Decision(Allowed=false)` short-circuits at step 4 before `OpenReadAsync`).
- **`LocalizedPage`-owning service (spot a — confirmed):** `ITranslationProvider` (impl `TranslationProvider`, `Kumunita.Core.Localization`) — `StaticPagesController`'s ctor (the plan's tie-break) injects `ITranslationProvider`; matches the design doc's own `ContentImageController` signature (`ITranslationProvider pages`). Recorded in the design doc's `§Pinned contract amendment (U03)` sub-line (append-only). **No drift.**
- **Adapter for the reply/announcement branch (spot b):** **neither** has one — the codebase has only `PostToAuditableResource`, `ProfileToAuditableResource`, and a private `ContactVisibilityResource`. C-M3·1 pins "a `PostReply`'s visibility IS the parent post's single `Read` decision" (no reply adapter by design); announcements use a flat `Scope`/role gate, never `IAuthorizationService`. The design doc names the *lookup* names for both but is **silent** on the adapter. Per the unit-series rule (stop, don't invent), the route serves these two branches **fail-closed 404, zero audit rows** (no `CanAsync` called) — see the drift pause below.
- **Smoke (app on `:5123`, killed after):** `GET /content-image/deadbeef` → **404** (store miss, no stack) · `GET /content-image/notahex` → **400** (id validation). Both green.
- **`npm run build` not-applicable** (no TS touched — the composer JS is U04's).
- **Drift pause:** see `## U03 — Drift pause` below.

## U03 — Drift pause

**Reply and announcement serving branches cannot emit the R·4 audit row — no `IAuditableResource` adapter exists for either owner, and the design doc is silent on the adapter (it pins only the *lookup* names).**

- **Why it's a real gap, not a gap U03 can fill in-scope:** R·4 requires "UGC owner: **one** frozen `CanAsync(Read)` on the owner (Allow **and** Deny each emit the seam's own one audit row, the owner's `TargetKind`)." A `CanAsync` call needs an `IAuditableResource`. `PostService.GetPostAsync` already shapes one (`PostToAuditableResource`) for the post — but for a **reply** there is no `PostReplyToAuditableResource` (C-M3·1: the reply's decision *is* the parent post's single `Read`; the parent must be loaded and *that* post is the `TargetKind` "post" — resolving the parent is a post read lane, not a reply read seam, and is outside U03's pinned deliverable list). For an **announcement** there is no `AnnouncementToAuditableResource` (the bounded context is documented as "not a call into `IAuthorizationService`" — a flat `Scope`/role gate, no per-resource audience, no `AccessAudit` lane on that context).
- **What U03 did (fail-closed, no invented seam):** the route's reply and announcement branches return `NotFound()` with **zero** `CanAsync` calls and **zero** audit rows. An image that is owned by a reply or announcement is therefore **inert** on this route — it 404s for every actor, including GlobalAdmin. This is *stricter* than R·4's intent (which would have served a member's reply image with one Allow row), but it is the only option that does not (a) invent an adapter the design doc did not name, or (b) double-call `CanAsync` (the "Read row emitted exactly once" pin). The route compiles, builds, and the smoke test is green; the two branches are simply inert until U04+ (or a future unit) authors the adapter.
- **What the lane owner must decide (before U04/U07 ship):**
  1. **Reply:** add a `PostReplyToAuditableResource` (TargetKind "postreply"?) and have the route load the parent post's decision, *or* have `FindReplyByImageIdAsync` also return the parent post id so the route can reuse the post branch's one `CanAsync`. Either way, a design-doc edit is required (the R·4 "the owner's `TargetKind`" line is silent on replies).
  2. **Announcement:** add an `AnnouncementToAuditableResource` (TargetKind "announcement"?) and a `CanAsync` on it — but this conflicts with the bounded context's documented "no `AccessAudit` lane" and the "flat role gate, never `IAuthorizationService`" contract. An ADR or ADR 0025 amendment is required to permit it.
  3. **Or** accept the inert-404 for reply/announcement-owned images as a documented lane limitation (the orphan-404 for GlobalAdmin, R·5 FACES, is already the pinned shape for these two owners, and the inert-404 is a *subset* of that: every actor, not just GlobalAdmin).
- **Impact on U07's pinned tests:** `R4_Member_Allows_200_OneAllowAuditRow` and `R4_NonMember_Denies_404_OneDenyAuditRow` assume a UGC owner with a working adapter — they pass for a **post**-owned image (the `PostToAuditableResource` path). They **cannot** pass for a reply- or announcement-owned image under this drift pause. U07 should scope those two tests to the **post** owner (or the lane owner resolves the drift pause first and U07 extends them). `R5_Orphan_404_ForGlobalAdmin_ZeroAuditRows` is unaffected (the orphan path is all-null-lookup → 404, zero rows — unchanged). `R4_PlatformPageOwner_ZeroCanAsyncCalls` is unaffected (the platform branch is unchanged).
- **What U03 does NOT change:** the four `ImageIds` fields, the four reverse-lookup seams (all four exist and are queryable — `FindReplyByImageIdAsync` and `FindByImageIdAsync` return the owner doc correctly; it is the *route's serving* of those two branches that is inert), the `LocalizedPage` service type (no drift), the route path, the 5-step ordering, the `nosniff` header, the id validation, and the smoke test (404/400 green). The build is green for both `Kumunita.Core` and `Kumunita.Web`.
