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

## U03

- **Delivered (3 plan deliverables + 1 form class + 1 test file — all new):**
  (1) `src/Kumunita.Web/Models/DocumentIndexViewModel.cs` — the feed record
  (`Visible`/`HasMore`/`Page`/`CanUpload`; **no `HiddenCount`** — F1: the feed
  never leaks "how many you cannot see"). (2)
  `src/Kumunita.Web/Models/DocumentDetailViewModel.cs` — the detail record
  (`Document`/`CanDownload`/`DownloadUrl`). (3)
  `src/Kumunita.Web/Models/DocumentUploadViewModel.cs` — the upload form (a
  `class`, `Title`/`Summary?`/`File: IFormFile?`/`Audience:
  AudienceEditorModel = new()`, the `PostComposeViewModel.Audience` idiom, not
  a JSON string; not in the unit plan's explicit 3-deliverable list, but the
  controller's `[FromForm]` shape requires it). (4)
  `src/Kumunita.Web/Controllers/DocumentController.cs` — the five routes
  (`Index`/`Detail`/`New`/`Upload`/`Serve`), all `[Authorize]` (plain, no
  `[Authorize(Roles=…)]` — the standing is the per-call `Read` decision for the
  three read surfaces; the D5 upload standing is an in-method `GlobalAdmin ∪
  Moderator` gate returning **404 not 403**). Serve = the ADR 0034 /
  `AttachmentController` 5-step (validate id → 400; `GetAsync` null → 404;
  blob miss → 404; `OpenReadAsync` → `File(stream, stored.ContentType)` +
  `X-Content-Type-Options: nosniff` + `Content-Disposition: attachment;
  filename=…; filename*=UTF-8''…`). Upload = the ADR 0011
  guards-before-write (empty → 400, oversize → 413, disallowed type → 415,
  **no file written on any guard**), then one `IMediaStore.PutAsync`
  (store-first, orphan-safe — D3/C-MED·7), then one
  `DocumentService.UploadAsync` in the caller's `store.LightweightSession()`
  (one `Document` row, one `SaveChangesAsync`, **no `AccessAudit` row** — A2),
  flash `documents.flash_uploaded` via `TempData["info"]` + redirect to the
  detail. (5) `tests/Kumunita.Web.Tests/DocumentControllerTests.cs` — 14 seam
  tests (GATE-1/2/5/6 + the D3 allowlist + the no-audit-row A2 pin + the
  no-`PutAsync`-on-guard pins + the `FileStreamResult` serve shape + the
  `ClaimTypes.Subject`/`.Role` principal fixture).
- **Exit (both green):** `dotnet build Kumunita.slnx -c Debug` → Build
  succeeded, **0 errors / 13 warnings** (the pre-existing baseline — none from
  the new src/test files; the 11 compile errors that surfaced in the test file
  were all test-side: `FileContent` → `FileStreamResult.FileContents`, the
  4-tuple destructure in the four guard tests, the `await` on the void
  `Store(...)` NSubstitute call, the ambiguous `ClaimTypes` (fully-qualified),
  the `ITempDataProvider` using for the upload's flash write).
  `dotnet exec …\Kumunita.Web.Tests.dll` → **`Total: 699, Errors: 0, Failed:
  0, Skipped: 0`** (the 14 new `DocumentControllerTests` are green; the
  pre-existing suite is untouched). **No new drift; zero new authorization
  surface** (C-M21·2 / D8) — the controller *rides* the frozen `Read` path
  (`CanSeeAsync` feed / `CanAsync` detail/serve) through U02's `DocumentService`;
  no new `AccessAction` / `Decide()` / `AccessVia` / `IAuthorizationService`
  method, no `AccessAudit` row written by the controller (the upload lane is
  the single `Document` store, A2).
- **Deviation (flag for U05's doc-parity pass):** the **unit plan's `Serve`
  sketch** says "No `[Authorize]` — an anonymous visitor to a public document
  must be served," but the **design doc §7 (locked surface)** — which is the
  authoritative source per the unit plan's own rule ("the codebase wins for
  mechanics; the design doc wins for the locked surface") — **is `[Authorize]`**
  and notes "the C-M21·1 floor is deny, so a public document is reachable only
  via the owner branch or a delegated Read." The shipped shape is `[Authorize]`
  (plain) + a defensive `actorId is null → Unauthorized()` — the correct
  locked-surface resolution. U05 should reconcile the unit-plan sketch to the
  shipped shape so the three sources agree.
- **Deviation (resolved, no action needed):** the unit plan's `TODO(U03)`
  `ParseAudience(string audienceJson)` → resolved to the codebase's
  `PostComposeViewModel.Audience.BuildAudience()` idiom (the `AudienceEditorModel`
  form-bound class, non-nullable, `BuildAudience()` → the frozen `Audience`
  shape — the M2 single-deserialization-site precedent). The unit plan's
  `TODO(U03)` `store.OpenTransactionSession()` → resolved to `store
  .LightweightSession()` (the `PostService.CreatePostAsync` C3 same-transaction
  lane; the `IDocumentStore` interface exposes `LightweightSession()`, not
  `OpenTransactionSession()` — the codebase wins for mechanics).
- **Deviation (test-only, no action needed):** the **feed** tests (GATE-1)
  drive a **real** scratch-Postgres store (the `PostgresFixture` in this
  assembly, the `ProjectsControllerTests` / `M14InterlockTests` precedent)
  because Marten 9's `ToListAsync()` casts to the internal
  `MartenLinqQueryable` and cannot be NSubstituted — the documented seam gap.
  The **detail / serve / upload** tests are pure NSubstitute (the
  `IQuerySession.LoadAsync` / `IDocumentStore.LightweightSession()` seams are
  directly stubbable).
- **Open question:** none blocking. The upload lane writes **no
  `AccessAudit` row** (U00's §1.a A2 lock, the `PostService.CreatePostAsync` /
  `AttachmentController.Upload` convention) — the `Upload_Valid_Redirects
  _OneWrite_NoAuditRow` test pins **zero** `AccessAudit` rows on the upload
  write. The flash key `documents.flash_uploaded` is set via
  `TempData["info"]` (the `M17AcceptanceGateTests` idiom); U04 authors the
  `KnownTranslationKeys` entry in all four languages.
- **Next unit (U04) entry point:** the Razor views (`Index.cshtml` /
  `Detail.cshtml` / `New.cshtml`) + the `kw-l` key authoring (the closed 12-key
  set from the design doc §8) + the `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` pin updates. U04 **consumes** the flash
  key `documents.flash_uploaded` and the `CanUpload` / `CanDownload` view-model
  flags shipped here.

## U04

- **Delivered (5 plan deliverables):**
  (1) `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the **12-key
  closed set** (the exact names from the register/design-doc §8, no renames)
  added to **all four** dictionaries (`EnValues` / `DeValues` / `FrValues` /
  `DaValues`) under a `── M21 (ADR 0122) ──` section banner, en source text
  authored + plausible de/fr/da. `AllKeys` is `EnValues.Keys.ToList()` — no
  change (the 12 keys flow in automatically). (2)
  `src/Kumunita.Web/Views/Documents/Index.cshtml` (new) — the feed: the
  `documents.title` h1, the upload button gated on `@Model.CanUpload` →
  `/documents/new`, the `documents.empty` empty-state, the feed rows (detail
  link `/documents/@d.Id` + download link `/documents/@d.Id/download`), and a
  "next" link iff `@Model.HasMore` (reusing the existing shared
  `pagination.next` key — the feed is newest-first, so page+1 is "Older").
  (3) `src/Kumunita.Web/Views/Documents/Detail.cshtml` (new) — the title (UGC,
  not a key), the optional summary, the meta block (`documents.detail.type_size`
  + `documents.detail.updated` with the `kw-dt` tag on
  `@Model.Document.Modified ?? @Model.Document.Created`), and the download
  button gated on `@Model.CanDownload` → `@(Model.DownloadUrl)`. (4)
  `src/Kumunita.Web/Views/Documents/New.cshtml` (new) — the compose form bound
  to U03's **actual** `DocumentUploadViewModel` (`Title` / `Summary?` /
  `File: IFormFile?` / `Audience: AudienceEditorModel`), `multipart/form-data`
  + anti-forgery, reusing the M2 audience editor (mode radios
  `Audience.Mode`, the `Audience.AllResidentsVisible` checkbox, and the shared
  `_GrantPickers` partial whose hidden `Audience.Grants` textarea is the only
  form-bound grant field — agreeing with U03's `form.Audience.BuildAudience()`).
  (5) `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the `More` dropdown nav
  entry → `/documents` with `documents.title` (reuses the key — the closed set
  stays at 12, no 13th `nav.documents`).
- **Exit (both green):** `dotnet build Kumunita.slnx -c Debug` → Build
  succeeded, **0 errors / 13 warnings** (the pre-existing baseline — none from
  the new files). `dotnet exec …\Kumunita.Web.Tests.dll` → **`Total: 699,
  Errors: 0, Failed: 0, Skipped: 0`** (the 14 U03 `DocumentControllerTests`
  green incl. the `TempData["info"] == "documents.flash_uploaded"` pin, plus
  `KwLRegistryConsistencyTests` — every static `kw-l` key in the new views is
  registered). Separately (Core.Tests, the assembly that guards
  `KnownTranslationKeys`): `dotnet exec …\Kumunita.Core.Tests.dll -class
  KnownTranslationKeys_ParityTests -class KnownTranslationKeysClosureTests
  -class SurfaceKeyTests` → **`Total: 11, Errors: 0, Failed: 0`** — the
  four-dictionary **parity** (en/de/fr/da all carry the identical 12-key
  M21 block, non-empty), the **closure** (`AllKeys` == `EnValues.Keys`), and
  the **surface-key** invariants all green. **No new drift; zero new
  authorization surface** (C-M21·2 / D8).
- **Deviation (flag for U05's doc-parity pass):** U03's `New()` action returns
  `View("New")` with **no model** (the GET form is blank), but U03's
  `Upload` re-render path passes the bound form (`return View(form)`). The New
  view therefore reads the model **null-safely** (`@Model?.Title` /
  `@Model?.Summary` / `Model?.Audience`) so both paths render — a non-null
  `@Model` read would NRE on the GET. The **audience-picker option seeding**
  (`ViewData["Audience_Users"]` / `Audience_Groups`, the M2/M3/M4 composer
  idiom) is done **in the view's `@{}` block** (injecting
  `IUserInfoService`) because U03's `New()` does not seed it and the `_GrantPickers`
  partial reads it — keeping the 5-deliverable scope (no controller change).
  U05 may optionally move the seeding into the controller if it prefers the
  `PostsController`-owned seam; the shape is identical either way.
- **Open question:** none blocking. The flash **renders as the raw key**
  `documents.flash_uploaded` (U03 stores the *key* in `TempData["info"]`,
  `_FlashToast` renders `@info` verbatim, and `DocumentControllerTests` pins
  `TempData["info"] == "documents.flash_uploaded"`). I authored the key in all
  four languages (my scope) and **did not** touch the controller or that test —
  localizing the toast (e.g. `await T("documents.flash_uploaded")`) is a
  controller change outside U04's 5 deliverables and would break the pin.
  **U05's decision surface** if it wants a localized flash.
- **Next unit (U05) entry point:** the **close-flip** — the **only** unit
  touching `Milestones.cs` / `MilestonesTests`. U05 flips M21 `StatusNext` →
  `StatusDone`, promotes M22 `StatusPlanned` → `StatusNext`, re-pins
  `MilestonesTests` (`M22_Is_The_Single_InProgress_Milestone` + the
  `Shipped_Milestones_Are_Marked_Done` done-list append), does the
  README/STATUS/ARCHITECTURE parity pass, tags ADR 0122's index row
  `**Done** (M21)`, and moves all M21 artifacts flat to `done/`. U05 should
  **reconcile design-doc §4 (U01) + §5 (U02) + the §7 `Serve` sketch (U03)**
  to the shipped shapes (recorded in the U01/U02/U03 sections above), and
  **decide the flash-localization question** (above). U05 exits on
  `Kumunita.Web.Tests`.

## U05

- **Delivered (the close-flip — the single flip for M21):**
  (1) `src/Kumunita.Web/Milestones.cs` — the two-line flip: M21
  `StatusNext → StatusDone`, M22 `StatusPlanned → StatusNext` (M21 is now the
  single `StatusDone` at the tail of the done list; M22 is the single
  `StatusNext`). (2)
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` — appended `"M21"` to the
  `Shipped_Milestones_Are_Marked_Done` done-list and renamed
  `M21_Is_The_Single_InProgress_Milestone` →
  `M22_Is_The_Single_InProgress_Milestone` (rewritten to pin M22 as the single
  `StatusNext` and M21 as `StatusDone`; the old "M22 is `StatusPlanned`"
  assertion is dropped — M22 is the last milestone, so there is no M-letter
  milestone past it). The other two tests
  (`Roadmap_Covers_M0_Through_M22_Plus_Named_Lanes_In_Order`,
  `No_Milestone_Has_Blank_Title`) are **unchanged**. (3)
  `README.md` — the intro paragraph now reads "M21 is done (…; ADR 0122). M22
  is in progress — onboarding" and the Roadmap bullets are M21 **Done** (ADR
  0122) / M22 **In progress**. (4) `docs/STATUS.md` — the long status line now
  reads "M21 is done (…; ADR 0122). M22 is in progress — onboarding". (5)
  `docs/adr/README.md` — the ADR 0122 row's **prose cell** tail is tagged
  `**Done** (M21).` (the status column stays `Accepted` — the M18/M19/M20
  row convention).
- **Flash question (U04's flagged open question) — DECISION: leave as a known
  deferred item.** The upload flash writes the raw key
  `documents.flash_uploaded` into `TempData["info"]` and `_FlashToast` renders
  it verbatim, so the toast shows the literal key, not translated text;
  `DocumentControllerTests.Upload_Valid_Redirects…` pins that raw-key value.
  Localizing it (e.g. `await T("documents.flash_uploaded")` before the
  redirect) would require touching **U03's `DocumentController`** (a code change
  outside U05's "flip + docs + archive" scope) **and re-writing the U03 test
  pin** — both ruled out by U05's hard constraint ("do not add/modify any M21
  code beyond the flip + docs + archive") and the §drift-guard (U05 is the
  close-flip, not a code unit). The key **is** authored in all four languages
  (U04's scope), so the text exists and is reachable — this is purely a
  render-as-raw-key cosmetic gap, not a missing translation. **Deferred as a
  known item:** the flash-localization follow-up is a small controller change
  (localize the key before the `TempData["info"]` write) + the test pin, a
  candidate for M22 onboarding or a named-lane touch-up — it does not block the
  M21 close.
- **Design-doc reconciliation (flagged by U01/U02/U03): NOT forced here.** The
  U01/U02/U03 sections flag that the *archived* design doc §4/§5/§7 sketches
  diverge from the shipped Core/Web shapes (namespace + `Schema.For`, the
  three-dep `DocumentService` ctor, the `[Authorize]` serve route). U05's scope
  is the close-flip + doc parity (README/STATUS/ADR-index) + archive — the
  archived design doc is a historical artifact in `done/` and is **not** the
  source of truth the code is built against (the codebase + ADR 0122 are). I
  left the design doc untouched rather than editing a moved artifact; the
  deviations are fully recorded in the U01/U02/U03 sections above. This is a
  §verify-don't-force call in service of the "no M21 code beyond the flip"
  constraint.
- **`ARCHITECTURE.md` parity (verify, don't force): NO CHANGE.** Its feature-
  module list (§3) stops at M13 and its value-chain table stops at M17 — the
  M18/M19/M20 close-flips did **not** extend it either (the M18/M19/M20
  feature-module additions, if any, post-date the file's last pass and were
  not back-filled). Per the register's "verify, don't force" and the M18/M19/M20
  precedent (which likewise closed without an `ARCHITECTURE.md` context edit),
  I did not add `Kumunita.Core.Documents` to the list — that would be
  introducing a new doc-edit convention, not parity. The M21 context
  (`Kumunita.Core.Documents`) is fully documented in ADR 0122, the design doc
  (archived), and the `Milestones.cs` / `README.md` / `STATUS.md` / ADR-index
  rows this flip updates.
- **Exit (both green):** `dotnet build Kumunita.slnx -c Debug` → Build
  succeeded. `dotnet exec …\Kumunita.Web.Tests.dll` → **`Total: 699, Errors:
  0, Failed: 0, Skipped: 0`** — the re-pinned `MilestonesTests` (M22 single
  in-progress, M21 done, the done-list incl. M21) **and** the whole assembly
  (the U03/U04 `DocumentControllerTests` + `KwLRegistryConsistencyTests` + the
  `KnownTranslationKeys` parity all still pass).
- **Archive (flat, no `done/m21/` subfolder — the M19/M20 flat precedent):**
  moved the four remaining in-progress M21 files to
  `docs/plans-milestones/done/` (the `m21-u01.md`…`m21-u04.md` unit plans were
  already moved by their own units per the register's workflow step 7):
  `plan-m21-document-management.md`, `m21-u00.md`, `m21-u05.md`, and this
  handoff-notes file. `in-progress/` now holds **no** M21 files. **M21 is
  shipped; M22 (onboarding) is open.**
