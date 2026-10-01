# M21 — Document management (rolling handoff notes)

> **The cross-unit memory.** Every unit reads the `## U##` sections before it
> and appends its own after. A unit does not re-derive what an earlier unit
> already settled (a D# amendment, the key set, a seam shape, the `MediaId`
> reference convention, the 404-on-deny posture). Created by U00 (the docs +
> ADR sign-off gate).

## U00

- **Delivered:** the design doc `docs/design/m21-document-management-design.md`
  (all 12 sections, 0–11, incl. §1.a) + the ADR
  `docs/adr/0122-document-management.md` (Status: Accepted) + the one ADR 0122
  index row in `docs/adr/README.md` (after the 0121 row). The register's
  [PROPOSED] set (D1–D9, C-M21·1…7, FACES F1–F5, the closed 12-key `kw-l` set,
  GATE-1…6, the §drift-guard) is **locked** in the design doc + ADR.
- **D#s locked / amended (the two named surfaces, both verified against the
  live codebase):**
  - **D5's upload standing — locked `GlobalAdmin ∪ Moderator`** (the register's
    proposed set, confirmed — §1.a A1). "official" is an authority act (the
    ADR 0017 announcements precedent); `Moderator` is the repo's community-
    authority lane (`PostService.CreatePostAsync` admits `GlobalAdmin` ∪
    component-`Moderator` to post); narrowing to GlobalAdmin-only would starve a
    one-neighborhood deployment, widening to "author-of-record ∪ GlobalAdmin"
    would re-introduce the "any-resident upload" D5 forbids. U03 enforces the
    set at the Web compose boundary; `UploadAsync` stays standing-agnostic.
  - **C-M21·7's upload audit-row — amended to "one standing-gated write, *no*
    `AccessAudit` row"** (row vs no-row → **no row** — §1.a A2). Verified:
    `PostService.CreatePostAsync` ("does not append an audit row"),
    `AttachmentController.Upload` ("**No audit row**: the write is
    authenticated, not an audience-restricted read"), `AnnouncementService`
    create ("announcements have no audit lane at all"); the mechanism confirms
    it — `AccessAudit` rows are produced **only** by the frozen
    `IAuthorizationService` `CanAsync`/`CanSeeAsync` seams. **No seam produces
    an upload row** (there is nothing to point at), so the amendment matches
    the codebase. The **reads** stay audited: feed = one aggregate row
    (C-M21·3), detail/download = one decision row (C-M21·4).
- **FACES:** F1 (feed by audience), F2 (download behind the `Read` decision),
  F3 (authority upload, standing-gated, no audit row), F4 (empty-audience =
  owner-only), F5 (delegated read flows).
- **§gate:** GATE-1 (empty-audience deny/allow), GATE-2 (feed one aggregate
  row), GATE-3 (detail/download one decision row + 404-on-deny), GATE-4 (zero
  new authorization surface), GATE-5 (bytes ride ADR 0011 + download
  disposition + orphan 404), GATE-6 (upload = one write, **no audit row**,
  standing at the Web boundary).
- **`TargetKind` pin:** `"document"` (the exact string — the
  aggregate/decision audit discriminator).
- **`MediaId` convention:** `Document.MediaId` = the ADR 0011
  content-addressed blob id (the `IMediaStore` id, SHA-256). The `Document`
  row is the **catalog** (the authority for *access*); the blob is the
  **content** (the authority for *bytes*). An **orphan** blob (no `Document`
  row) is a **404** (the document, not the blob, is the authority for access).
  Serving = `IMediaStore.OpenReadAsync` behind the `Read` decision with
  `Content-Disposition: attachment`.
- **U01 next** — read the design doc §4 (the `Document` POCO +
  `DocumentDocTypes` exact C#) + §5 (the `DocumentToAuditableResource` adapter
  + the `DocumentService` ctor/DTOs) + the ADR 0122 §Decision (D1/D2). U01
  owns the `Document` doc + the `DocumentDocTypes` registration + the
  `Program.cs` boot line + the adapter + the DI; it exits on
  `Kumunita.Core.Tests`.

## U01

- **Delivered (4 plan deliverables + 1 pin test):**
  (1) `src/Kumunita.Core/Documents/Document.cs` — the `Document` POCO (D1,
  namespace `Kumunita.Core.Documents`, `Audience` non-null `= new()`,
  `MediaId`/`OwnerId` per the locked surface). (2)
  `src/Kumunita.Core/DocumentDocTypes.cs` — the additive Marten surface
  (namespace `Kumunita.Core`, `opts.Schema.For<Document>()`, conventional
  `string Id` — no non-default convention). (3) `Program.cs` — one
  `DocumentDocTypes.Configure(opts);` boot line after `UsageDocTypes.Configure(opts);`
  with the matching doc-comment; `SchemaBootstrap.cs` **not touched**.
  (4) `src/Kumunita.Core/Documents/DocumentToAuditableResource.cs` — the frozen
  `IAuthorizationService` Read-path adapter (D2), `TargetKind => "document"`
  (exact pin), `ComponentId => null`, `Audience` projected verbatim. (5)
  `tests/Kumunita.Core.Tests/DocumentDocTypesDdlTests.cs` — the M16/M17 U01
  "doc types are registered" precedent: a live-Postgres catalog pin that the
  `Document` row materializes (`mt_doc_document`, `id`+`data`) **plus** an
  in-memory pin of the frozen 6-member `IAuditableResource` surface with the
  **exact** `"document"` `TargetKind`, `ComponentId` null, and the `Audience`
  projected **by reference** (verbatim — the adapter never mutates it).
- **Exit (both green):** `dotnet build Kumunita.slnx -c Debug` → Build
  succeeded (no warnings from the new files; the 13 pre-existing Web.Tests
  warnings untouched) + `dotnet exec …\Kumunita.Core.Tests.dll` →
  `Total: 1062, Errors: 0, Failed: 0`. **No new drift; zero new authorization
  surface** (C-M21·2 / D8) — the adapter *rides* the frozen `Read` path, no new
  `AccessAction` / `Decide()` / `AccessVia` / `IAuthorizationService` method.
- **Deviation (flag for U05's doc-parity pass):** design doc **§4**'s
  `DocumentDocTypes` block shows it in namespace `Kumunita.Core.Documents`
  using `opts.PatchDocuments<Document>(d => d.Audience)`. That is **not** the
  codebase shape: `PatchDocuments` is used **nowhere** in `src/`, and every
  sibling `*DocTypes` (`UsageDocTypes` / `M17DocTypes` / `PageDocTypes` /
  `MediaDocTypes`) lives at top-level `src/Kumunita.Core/` in namespace
  `Kumunita.Core` and calls `opts.Schema.For<T>()`. The **unit plan (authoritative)
  + ADR 0122 D1** both specify `Schema.For<Document>()` + `Kumunita.Core`, so
  that is what shipped (codebase wins for *mechanics*). **U05 should reconcile
  design doc §4** to the shipped shape (`Schema.For<Document>()`,
  `Kumunita.Core` namespace) so the doc matches the code.
- **Open question:** none blocking. `Document`'s `Audience` is non-nullable
  (`= new()`) — matching the `Post.Audience` (`null!`) convention — so the
  adapter returns `public Audience Audience` (non-nullable, like
  `PostToAuditableResource`) rather than `Audience?`, avoiding a
  non-nullable→nullable conversion under warnings-as-errors. `DocumentService`
  / DTOs / the `MediaOptions` document lane are **U02's**, not shipped here.
- **Next unit (U02) entry point:** the design doc **§5** `DocumentService`
  (feed = one `CanSeeAsync` aggregate row / detail = one `CanAsync` decision
  row / upload = one standing-agnostic write, **no audit row** per §1.a A2) +
  the `DocumentUpload` DTO + the additive `MediaOptions`
  `DocumentAllowedContentTypes`/`IsDocumentAllowed` lane (D3). U02 composes
  U01's `Document` + `DocumentToAuditableResource` against the frozen
  `IAuthorizationService` Read path + the ADR 0011 `IMediaStore`; it exits on
  `Kumunita.Core.Tests`.

## U02

- **Delivered (7 plan deliverables — 5 new + 2 additive):**
  (1) `src/Kumunita.Core/Documents/DocumentListResult.cs` — the feed DTO
  (`Visible`/`HiddenCount`/`Page`/`Total`/`HasMore`; **no `ComponentId`** —
  community-level, D2). (2)
  `src/Kumunita.Core/Documents/DocumentDetailResult.cs` — the detail DTO
  (`Document?`, null on **both** the missing and the deny fail-closed cases —
  D7: feed and detail agree). (3)
  `src/Kumunita.Core/Documents/DocumentUpload.cs` — the upload draft (`Audience`
  is `Authorization.Audience`, matching U01's `Document.Audience`). (4)
  `src/Kumunita.Core/Documents/DocumentService.cs` — `ListAsync` (feed, one
  `CanSeeAsync`, one aggregate row, 0-candidate early return + `CountAsync`
  candidate count), `GetAsync` (detail, one `CanAsync`, one decision row,
  Deny→`Document = null`), `UploadAsync` (caller's `IDocumentSession`,
  `session.Store` + one `SaveChangesAsync`, **no audit row**). Ctor is exactly
  the three frozen deps (userInfo, authz, store) — M21 has no tag/notification
  seams, so no optional params (unlike `PostService`). (5)
  `src/Kumunita.Core/Media/MediaOptions.cs` — the additive `Document*` lane
  (`DocumentAllowedContentTypes` / `ResolvedDocumentAllowedTypes` /
  `IsDocumentAllowed`) **beside** the `Attachment*` lane; the image + attachment
  lanes are **untouched**; the default document allowlist is the attachment set
  **minus** the raster image types (a document is a download, not a photo — D3/D6).
  (6) `src/Kumunita.Core/DependencyInjection.cs` — the `DocumentService`
  `AddTransient` registration in `AddKumunitaCore`, immediately after the
  `PostService` registration (same factory shape). (7)
  `tests/Kumunita.Core.Tests/Documents/DocumentServiceTests.cs` — 6 seam tests
  mirroring `PostServiceTests` (scratch-Postgres `PostgresFixture`,
  `BootStoreAsync`/`Services`/`Plant`/`RunInSession` helpers, `DocumentAudits`
  re-pointed at the `"document"` `TargetKind` pin): GATE-4 feed (one aggregate
  row, `TargetKind = "document"`), C-M21·1 empty-audience (owner-allow /
  non-author-deny, owner's id never the actor), C-M21·5 feed+detail agree,
  GATE-3 detail (one decision row, deny still audited, missing ⇒ no row), and
  C-M21·4 upload (standing-agnostic, **zero audit rows**, `Audience` verbatim,
  `OwnerId` = actor, `Modified = null`).
- **Exit (both green):** `dotnet build Kumunita.slnx -c Debug` → Build
  succeeded, **0 warnings / 0 errors** (an initial xUnit2029 on an `Assert.Empty`
  over a filtered LINQ query in my test was fixed to `Assert.DoesNotContain`,
  matching `PostServiceTests`'s idiom). `dotnet exec …\Kumunita.Core.Tests.dll`
  → **`Total: 1068, Errors: 0, Failed: 0, Skipped: 0`** (U01 had 1062 — my 6
  new tests are green). **No new drift; zero new authorization surface**
  (C-M21·2 / D8) — the service *rides* the frozen `Read` path (`CanSeeAsync` /
  `CanAsync`), no new `AccessAction` / `Decide()` / `AccessVia` /
  `IAuthorizationService` method.
- **Deviation (flag for U05's doc-parity pass):** design doc **§5**'s
  `DocumentService` shows the ctor as `(IAuthorizationService authz,
  IDocumentStore store, IMediaStore media)` and `GetAsync` returning
  `Task<DocumentDetailResult?>` (nullable result, `null` on both missing and
  deny). The **unit plan (authoritative) + `PostService` (codebase wins for
  mechanics)** both specify the ctor as `(IUserInfoService, IAuthorizationService,
  IDocumentStore)` and `GetAsync` returning `Task<DocumentDetailResult>` with
  `Document = null` on both fail-closed cases — that is what shipped. The
  `IMediaStore` is **not** a `DocumentService` dependency in the unit plan (the
  Web layer — U03 — owns `IMediaStore.PutAsync`/`OpenReadAsync`, the
  `AttachmentController` lane shape); U03 does **not** need a Core service seam
  for the bytes. **U05 should reconcile design doc §5** to the shipped shape
  (three-dep ctor incl. `IUserInfoService`, non-nullable `DocumentDetailResult`
  with `Document?`, no `IMediaStore` dep in Core) so the doc matches the code.
- **Open question:** none blocking. The upload lane writes **no `AccessAudit`
  row** (U00's §1.a A2 lock, the `PostService.CreatePostAsync` /
  `AttachmentController.Upload` convention) — the C-M21·4 test pins **zero**
  document audit rows on upload. `Document`'s `Modified` is `null` in M21 (set
  only on a future replace — D9·1).
- **Next unit (U03) entry point:** the `DocumentController` (index/detail/new +
  upload POST + download GET) + view models, serving behind the `Read` decision
  with Deny→404 (D7) and `Content-Disposition: attachment` (D6), composing U02's
  `DocumentService` (`ListAsync`/`GetAsync`/`UploadAsync`) + the frozen
  `IMediaStore` (the `AttachmentController` lane shape). It **consumes** the
  `MediaOptions.IsDocumentAllowed` gate U02 added. It exits on
  `Kumunita.Web.Tests`.
