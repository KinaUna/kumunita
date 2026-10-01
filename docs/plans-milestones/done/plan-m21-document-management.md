# M21 — Document management (the register)

> **This file is the register** — the milestone map, the locked [PROPOSED]
> decision set, the invariants, the FACES, the unit map, the workflow, the
> atomicity contract, and the §drift-guard. It is read first by **every** unit
> agent (U00–U05) before anything else. It is **not** a per-unit plan: each
> unit has its own self-contained file (`m21-uNN.md`) that a ~32K-context agent
> can execute without re-deriving this file.
>
> **M21 is NOT the last milestone.** It is the **penultimate** of the remaining
> horizon — after M21 comes **M22 (onboarding)**, the last. So M21 has **one**
> milestone-flip unit, a difference from M20 (which had **two** — U01 opened it
> and U08 closed it, because M20 was still `StatusPlanned` when M20 was
> authored). **M21 is *already* `StatusNext` in `Milestones.All`** — M20's
> close-flip (its U08) promoted it — and the live pin is
> `MilestonesTests.M21_Is_The_Single_InProgress_Milestone`. So M21 has **no
> open-flip unit**; its **single** flip is **U05 (the close)**: `StatusNext` →
> `StatusDone` (M21), **M22 promoted** `StatusPlanned` → `StatusNext`, the pin
> re-pinned to `M22_Is_The_Single_InProgress_Milestone`. **No unit before U05
> touches `Milestones.cs` or `MilestonesTests`** (the roadmap already reads
> "M21 in progress" — it stays that way until M21 ships).

## Tiering (three documents, like M18–M20)

| Tier | File | Who owns it | Lifetime |
| --- | --- | --- | --- |
| 1 — Register (this file) | `in-progress/plan-m21-document-management.md` | U00 (author) + all units (read) | moves to `done/` at U05 |
| 2 — Per-unit plans | `in-progress/m21-uNN.md` | the `U##` agent | moves to `done/` when the unit is done |
| 3 — Rolling handoff notes | `in-progress/m21-document-management-handoff-notes.md` | every agent appends its `## U##` section | moves to `done/` at U05 |

The handoff-notes file is created by **U00** (at runtime, not by this authoring
pass). Every unit appends a short `## U##` section before it moves its own plan
to `done/`.

## Atomicity contract (sized for ~32K context)

Each unit is one self-contained step a fresh agent can complete and hand off:

- **Entry reads:** 4–8 files, named in the unit plan (with a one-line "why"
  each).
- **Deliverables:** ≤ 7 small files, named exactly (paths +, where locked, the
  exact C# / keys the design doc pins).
- **Exit gate:** **one** `dotnet build Kumunita.slnx -c Debug` **plus one**
  `dotnet exec` test assembly — either
  `tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` **or**
  `tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`. Both
  must be green.
- **A unit that needs *both* test assemblies is too big — split it.**
  (Core units exit on Core.Tests; Web units exit on Web.Tests. The close-flip
  unit — U05 — exits on Web.Tests, because it flips `Milestones.cs`, which is
  pinned by `MilestonesTests` in `Kumunita.Web.Tests`.)
- **No build/test exit for U00** — it is docs-only (design doc + ADR + index
  row). Its exit is "the files exist and the [PROPOSED] set is locked or
  amended in-unit."

> **Test-runner quirk (do not retry the broken paths):** `dotnet test` and VS
> Test Explorer discovery go wrong on this machine (xunit.v3 bridge). The
> reliable exit is the `dotnet build` + `dotnet exec … .dll` pair above.
> `Kumunita.Core.Tests` starts `postgres:18` via Testcontainers (~20 s) and
> leaves Docker containers behind if killed — `docker container prune` to clean
> up.

## Unit map

| Unit | Title | Track | Exit test assembly |
| --- | --- | --- | --- |
| U00 | Design doc + ADR 0122 (the sign-off gate) | docs-only | none (no build/test) |
| U01 | `Document` doc + `DocumentDocTypes` reg + `Program.cs` boot + `DocumentToAuditableResource` adapter + DI | Core | `Kumunita.Core.Tests` |
| U02 | `DocumentService` (feed + detail + upload write lane) + DTOs + the `MediaOptions` document allowlist + DI | Core | `Kumunita.Core.Tests` |
| U03 | `DocumentController` (index/detail/new + upload POST + download GET) + view models (serve-behind-decision, Deny→404, `Content-Disposition`) | Web | `Kumunita.Web.Tests` |
| U04 | `Views/Documents/*` + nav entry + **the closed `kw-l` key set** × en/de/fr/da (author) | Web | `Kumunita.Web.Tests` |
| U05 | **Close M21** — flip `StatusDone`, promote M22 to `StatusNext`, re-pin `MilestonesTests`, README/STATUS/ARCHITECTURE parity, `done/` move | Web + docs | `Kumunita.Web.Tests` |

## Understanding (what M21 is)

**M21 = document management.** The roadmap line is the scope:
> "Document management — a shared repository for official documents, contracts,
> etc., with per-document access controls."

M21 ships a **shared repository** of **official** documents (contracts,
minutes, notices, bylaws, …) for the community, where **each document carries
its own access control** (the `Audience`, reused verbatim from ADR 0006) and is
**served as a download** behind a per-document **`Read` decision**. Three things
follow from the roadmap sentence:

1. **A `Document` is a *new domain document*** (the M3/Events/Pages precedent):
   a catalog row — `Title`, `Summary?`, `MediaId`, `Filename?`, `ContentType`,
   `SizeBytes`, a **non-null `Audience`**, `OwnerId`, `Created`, `Modified?` —
   registered on a **new additive `DocumentDocTypes` surface** (ADR 0004 §B.1,
   the `M17DocTypes` / `MediaDocTypes` shape), in a **new bounded context**
   `Kumunita.Core.Documents`.
2. **"…with per-document access controls"** = the document's **`Audience`** is
   the *entire* access story (ADR 0006-C1 **deny-by-default**: an empty audience
   denies everyone; the uploader sees their own via the owner branch). M21
   **rides the frozen `Read` path** through a **new `DocumentToAuditableResource`
   adapter** (`TargetKind = "document"`) and a **new `DocumentService`** that
   composes the two frozen seams exactly like `PostService` — **zero new
   `IAuthorizationService` surface** (C-M21·2).
3. **"official documents, contracts, etc."** = real **file bytes** (PDFs,
   docx, …) served **as a download** (`Content-Disposition: attachment`). The
   bytes ride the **frozen ADR 0011 `IMediaStore`** (content-addressed local
   volume, `PutAsync`/`OpenReadAsync`) — **no new storage lane** (D3). The
   serving route mirrors `AttachmentController` (the ADR 0034 download lane):
   one `Read` decision, **Deny → 404 not 403** (no existence leak), exactly one
   audit row.

Everything else about M21 is *what it is not* — see "What M21 is NOT" below and
the §deferred lanes (D9).

## What M21 is NOT

- **Not a document-management *system*.** M21 ships **one** document type: a
  file + its title + its audience + its download. **No versioning/revision
  history, no full-text search within bodies, no comments/discussion, no
  per-kind taxonomy, no scheduled expiry/purge, no edit-by-replacement** — each
  is a *future ADR* (D9). M21 is the *document + its access + its bytes*, not a
  DMS.
- **Not inline-rendered content.** An official document is a **download**
  (`Content-Disposition: attachment`), **not** an `<img>`/`<embed>`/inline
  blob (D6). That is a deliberate *security* posture — never render an
  authority-published file inline in the browser.
- **Not a public / static surface.** There is **no unauthenticated** document
  route and **no CDN/static** serving. Every read is a per-call `Read` decision
  against the document's `Audience`; the floor is **deny** (an empty audience
  is a public *denial*, C-M21·1).
- **Not a component-scoped or author-scoped feed.** The repository feed lists
  **all** documents the caller's audience allows (one `CanSeeAsync`, one
  aggregate audit row). A **component is not a gate** for documents
  (`ComponentId = null` on the adapter, C-M21·2/C-M21·3); "which documents I
  can see" is decided by each document's `Audience`, never by a bucket.
- **Not a new authorization surface.** M21 adds **no** `AccessAction`, **no**
  `Decide()` branch, **no** `AccessVia`, **no** `IAuthorizationService`
  method. It adds **one** `IAuditableResource` adapter + **one** `TargetKind`
  (`"document"`) + **one** new document + **one** `DocumentService` + **one**
  `DocumentController` (C-M21·2, D8).
- **Not a new storage lane.** The bytes are the **frozen ADR 0011 `IMediaStore`**
  (content-addressed local volume). M21 adds **no** new storage mechanism, no
  in-DB blob, no second volume (D3). The `Document` row is the **catalog**
  (the authority for *access*); the blob is the **content** (the authority for
  *bytes*). An orphan blob (no `Document` row) is a 404 — the document is the
  authority for access, not the blob.
- **Not a new notification lane.** Uploading a document stages **no** email
  (M6/ADR 0076 is untouched); M21 is intake-and-serve, not notify. (A "new
  document posted" nudge is a future lane, D9·3.)

## Decisions (D1–D9) — [PROPOSED], locked or amended by U00

> Each D# carries a `*Forbids:*` tail — the anti-pattern it rules out. U00
> locks these verbatim in the design doc, or amends them **in U00** (recorded
> in the design doc's §1.a + the handoff notes). A later unit may not amend a
> D#; if a D# is wrong once the code starts, that is a §drift-guard stop.

### D1 — A new `Document` document, on its own additive `DocumentDocTypes` surface (ADR 0004 §B.1)

A new document `Kumunita.Core.Documents.Document` (namespace
`Kumunita.Core.Documents`, a **new bounded context** alongside `Posts` /
`Events` / `Pages`), registered on a **new parallel additive surface**
`DocumentDocTypes.Configure(StoreOptions)` (the `M17DocTypes` / `MediaDocTypes`
shape — Marten-native POCO, conventional `Id` string, delta-detected + idempotent,
**no seeding**), wired with **one line** in `Program.cs`'s `AddMarten` lambda
(next to `M17DocTypes.Configure(opts);`, `SchemaBootstrap.cs` **not touched** —
it calls no `*DocTypes.Configure`). Shape (locked by U00's design doc):

```csharp
public sealed class Document
{
    public string Id { get; set; } = string.Empty;          // conventional string Id (ADR 0004 §B.1)
    public string Title { get; set; } = string.Empty;       // human-facing name (a label, never a gate)
    public string? Summary { get; set; }                    // optional one-line description (display-only)
    public string MediaId { get; set; } = string.Empty;     // the ADR 0011 content-addressed blob id (IMediaStore)
    public string? Filename { get; set; }                   // original filename (the RFC 6266 download name)
    public string ContentType { get; set; } = string.Empty; // e.g. "application/pdf"
    public long SizeBytes { get; set; }
    public Audience Audience { get; set; } = new();         // per-document access control (ADR 0006-C1 deny-by-default; the WHOLE access story)
    public string OwnerId { get; set; } = string.Empty;     // the uploader (the "author" standing)
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }           // set on an eventual replace/edit (D9·1); null in M21
}
```

*Forbids:* a new **relational** table, a **new `AccessAction`**, a per-document
**`AccessVia`/standing**, or encoding per-document access in an **identity
claim** (the `Audience` is the only access carrier — C-M21·1/C-M21·2).

### D2 — M21 rides the **frozen** `Read` path — one new adapter + one new `TargetKind`, zero new `IAuthorizationService` surface

`DocumentToAuditableResource : IAuditableResource` (the `PostToAuditableResource`
shape verbatim) exposes `Id`, `Name` (= `Title`), `OwnerId`, `Audience`,
`ComponentId => null` (documents are repository-level, not component-scoped), and
**`TargetKind => "document"`** (the **exact** string — the aggregate/decision
audit discriminator, C-M21·3/C-M21·4). The `DocumentService` composes **only**
the frozen `IAuthorizationService` + the frozen `IUserInfoService` read seams +
its own `IDocumentStore`, exactly like `PostService`. Feed = one `CanSeeAsync`
(aggregate row), detail/download = one `CanAsync` (decision row), all
`AccessAction.Read`.

*Forbids:* a new `AccessAction`, a new `Decide()` branch, a new
`IAuthorizationService` method, a new `AccessVia`, or a `DocumentService` that
reads `GroupMembership`/`DelegationGrant` for its own access decisions (C-M21·2).

### D3 — Bytes ride the **frozen** ADR 0011 `IMediaStore`; M21 adds **no** new storage lane

`Document.MediaId` is the **content-addressed** id returned by the frozen
`IMediaStore.PutAsync(content, filename, contentType, actorId, ct)` (SHA-256,
orphan-safe store-first). Serving = `IMediaStore.OpenReadAsync(mediaId)` **behind
the `Read` decision** (D6), streamed with the stored `ContentType` + the original
`Filename`. `MediaOptions` (the ADR 0011 / ADR 0034 allowlist) gains one
**additive** document lane — `DocumentAllowedContentTypes` /
`IsDocumentAllowed(contentType)` — in the exact shape of the existing
`AttachmentAllowedContentTypes` / `IsAttachmentAllowed` (a PDF/DOCX/XLSX/CSV/ZIP
allowlist; the image lane is *not* the document lane).

*Forbids:* a new storage mechanism, in-DB blob bytes, a public/static serving
route, or a serving path that bypasses the `Read` decision (D6/D7).

### D4 — The repository feed is a **candidate filter over all documents**, never a gate (C-M21·3)

`DocumentService.ListAsync(actorId, page)` returns **all** documents whose
`Audience` allows the caller (one `CanSeeAsync` over the candidate set), in
`Created` order, and emits **one aggregate** `AccessAudit` row
(`targetKind "document"`, `visibleCount`/`hiddenCount` set, `targetId = null`) —
the `PostService.ListFeedAsync` aggregate shape. A **component is not a gate**
(`ComponentId = null` on the adapter); the only filter is each document's own
`Audience`.

*Forbids:* a component gate, a per-author feed gate, an unaudited feed read, or a
feed that lists a document the caller cannot open (feed and detail/download must
agree — C-M21·5).

### D5 — [PROPOSED] The upload/compose standing is the **authority lane**: `GlobalAdmin ∪ Moderator`

Posting an **official** document is an **authority act** — the Web boundary gates
the compose form to the elevated standing (`GlobalAdmin ∪ Moderator`), the
ADR 0017 "author-of-record ∪ GlobalAdmin" shape generalized to the community's
authority. The Core write lane `DocumentService.UploadAsync(DocumentUpload,
actorId, IDocumentSession)` is **standing-agnostic** (it records the
`OwnerId = actorId` and one audited write row; it does **not** re-check roles —
the Web boundary owns the standing check, the ADR 0006/ADR 0017 convention). One
audited write: the `Document` is stored in the **caller's** session (ADR 0006-E
same-transaction) alongside the already-`PutAsync`'d blob (D3).

*Forbids:* any-resident upload (that would make "official" meaningless — the
roadmap says *official* documents), an unaudited direct `session.Store` from a
controller, or a Core seam that re-derives the standing (the Web boundary owns it).

> **U00 may amend D5's exact standing in U00** (e.g. widen to "author-of-record ∪
> GlobalAdmin" per ADR 0017, or narrow to GlobalAdmin-only). The *shape* — one
> authority-gated Web compose + one standing-agnostic audited Core write lane —
> is locked; only the standing *set* is the amendment surface.

### D6 — A document is a **download, not an inline render** (`Content-Disposition: attachment`)

The serve route (the `AttachmentController` / ADR 0034 download shape) streams
`IMediaStore.OpenReadAsync` **behind the `Read` decision**, with the stored
`ContentType` + `Content-Disposition: attachment; filename="<stored-or-{id}.bin>"`
(RFC 6266) + `X-Content-Type-Options: nosniff`. An official document is *downloaded*,
never rendered inline.

*Forbids:* inline `<img>`/`<embed>` of an official document, a public CDN/static
route, or a serve route with no per-call `Read` decision.

### D7 — Deny is **404, not 403** (no existence leak); feed and detail agree (C-M21·4/C-M21·5)

The serve route is the `ContentImageController`/`AttachmentController` 5-step:
(1) validate the id (1–128 hex-ish) else **400**; (2) `Document` miss → **404**;
(3) blob (`IMediaStore`) miss → **404** (orphan → the document is the authority);
(4) **one** `CanAsync(actorId, AccessAction.Read, new DocumentToAuditableResource(doc))`
→ **Deny → 404 not 403** (the caller cannot distinguish "not there" from "not for
you"); the **single** `AccessAudit` row is emitted *by the seam* (in-transaction,
C3), **not** the controller; (5) allow → stream the blob (D6). A document in the
**feed** is openable by the same caller; a document the feed hides 404s on detail
(the same `Read` decision governs both).

*Forbids:* a 403 that reveals existence, a feed listing a document that 404s on
detail, or an audit row written by the controller instead of the seam.

### D8 — **Zero new authorization surface** (the C-M21·2 pin)

M21 adds **no** `AccessAction`, **no** `Decide()` branch, **no** `AccessVia`,
**no** `IAuthorizationService` method; the claim set (`ClaimTypes.All`) is
**unchanged**. It adds exactly **one** `IAuditableResource` adapter + **one**
`TargetKind` (`"document"`) + **one** new document + **one** `DocumentService` +
**one** `DocumentController` + **one** additive `MediaOptions` lane.

*Forbids:* any extension of the frozen authorization seams (a §drift-guard stop
— the whole point of M21 is that it *rides* `Read`, it does not extend it).

### D9 — **Deferred lanes** (each a future ADR, not part of M21)

1. **Versioning / revision history** — a document's revision chain + diff. M21
   keeps **one** current blob + `Modified?` (the field is present but M21 sets it
   only on a replace, which is itself a future lane); a versioned history is a
   larger design.
2. **Full-text search within document bodies** — M21 does not index PDF/docx
   text; a search over *titles* may reuse the M8 `/search` lane (a future
   decision), and body-text search is a future lane.
3. **Document comments / discussion** — rides M3/M3b moderation; a future lane.
4. **A "new document posted" notification** — rides M6 (ADR 0076); M21 stages
   **no** email. A nudge lane is future.
5. **Per-kind document taxonomy** (contracts vs minutes vs notices vs bylaws,
   each with its own standing) — M21 is **one** document type; a taxonomy is a
   future lane.
6. **Scheduled expiry / auto-purge** — the M20 §6.4 durable-job shape; a future
   lane (M21 has **no** `Expiry` field and **no** purge job).
7. **Edit-by-replacement / author soft-delete** — the M14/M24 lane shape; M21's
   write lane is **upload-only** (the uploader's own draft is visible to them via
   the owner branch; a public replace/edit is a future lane).

## Invariants (C-M21·1 … C-M21·7)

- **C-M21·1 — Per-document access is the document's `Audience`, deny-by-default
  (ADR 0006-C1).** An **empty** `Audience` denies **all** non-owners; the
  uploader still sees their own via the **owner** branch. A test asserts an
  empty-audience document is denied to a non-author and allowed to the owner.
- **C-M21·2 — Zero new authorization surface (D8).** No new `AccessAction` /
  `Decide()` branch / `AccessVia` / `IAuthorizationService` method;
  `ClaimTypes.All` unchanged. M21 adds **one** `IAuditableResource` adapter
  (`TargetKind = "document"`) that *rides* the frozen `Read` path. A pin test
  asserts the seam surface + the claim set are unchanged.
- **C-M21·3 — The feed is a candidate filter, not a gate (D4).**
  `DocumentService.ListAsync` = one `CanSeeAsync` + **one** aggregate
  `AccessAudit` row (`targetKind "document"`, `visibleCount`/`hiddenCount` set,
  `targetId = null`); `ComponentId = null` on the adapter. A test pins the
  aggregate row shape + **zero** individual rows for public candidates.
- **C-M21·4 — Detail/download = one `Read` decision + one decision audit row (D6,
  D7).** `GetAsync`/serve = one `CanAsync` + **exactly one** `AccessAudit` row
  (`targetId = doc.Id`, counts null), emitted **by the seam** (in-transaction,
  C3); **Deny → 404 not 403** (no existence leak). A test pins the decision-row
  shape + the 404-on-deny.
- **C-M21·5 — Feed and detail/download agree (D7).** A document in the caller's
  feed is openable by that caller; a document the feed hides 404s on detail — the
  **same** `Read` decision governs both (no separate detail gate). A test asserts
  feed-inclusion == detail-allow for the same actor.
- **C-M21·6 — Bytes ride ADR 0011 (D3); the `Document` doc is the catalog (D1,
  D6).** `Document.MediaId` = the `IMediaStore` id; serving =
  `IMediaStore.OpenReadAsync` **behind the decision** with
  `Content-Disposition: attachment`; an **orphan** blob (no `Document` row) is a
  404 (the document is the authority for access). A test pins the `MediaId`
  reference + the download disposition.
- **C-M21·7 — Upload is a single audited write lane, standing-gated (D5).**
  `UploadAsync` stores **one** `Document` (+ the already-`Put`'d `MediaObject`
  via `IMediaStore`) in the **caller's** session and appends **exactly one**
  `AccessAudit` row; the Web boundary owns the standing check (GlobalAdmin ∪
  Moderator, D5) and a non-eligible actor is refused **at the Web boundary** (the
  Core seam records the `OwnerId` and does not re-check roles). A test pins the
  single write + the one audit row + the standing.

## FACES (F1–F5) + the named trade

- **F1 — A resident sees the documents their audience allows** (C-M21·1, C-M21·3,
  C-M21·5): the repository feed lists only the documents the caller's `Audience`
  grants; one aggregate audit row.
- **F2 — A document is opened as a download behind its `Read` decision**
  (C-M21·4, C-M21·6, D6, D7): one decision audit row; Deny → 404 (no existence
  leak); `Content-Disposition: attachment`.
- **F3 — An authority uploads an official document with a chosen audience**
  (C-M21·7, D5): GlobalAdmin ∪ Moderator posts; the `OwnerId` is set; one
  audited write; the document's `Audience` is written **verbatim** (ADR 0001-B —
  the uploader's choice is absolute; no auto-add of grants).
- **F4 — An empty-audience document is visible to no one but its uploader**
  (C-M21·1, ADR 0006-C1): the deny-by-default floor — the lean default.
- **F5 — Delegated read flows** (C-M21·1, ADR 0006-C2): a delegate with `Read` in
  scope sees the documents their principal's audience allows; a delegate without
  `Read` sees nothing (the frozen `Decide()` C2 branch — M21 **rides** it, does
  not extend it).

**The named trade.** M21 buys *a shared, audited, per-access-controlled
repository for official documents, served as downloads* with **zero new
authorization surface + one new document + one new adapter + one new service +
one new controller + one additive `MediaOptions` lane + a closed `kw-l` set**, in
exchange for **no versioning, no full-text, no comments, no taxonomy, no
auto-expiry, no edit-by-replacement** (D9): M21 is *the document + its audience +
its bytes + its download*, **not** a document-management *system*. The deferred
lanes (D9·1…D9·7) are deliberately *not* in M21: they are larger designs that
need their own ADRs, and M21's value (a neighborhood that keeps its official
contracts and minutes in one place, each readable only by the people the
uploader chose, each audited when opened) is delivered by the single document +
its `Audience` + its ADR 0011 bytes.

## §gate (acceptance tests, named — U00 locks them in the design doc)

- **GATE-1 — Per-document access, deny-by-default.** An **empty**-audience
  document is denied to a non-author and allowed to its uploader (the owner
  branch). *(C-M21·1.)*
- **GATE-2 — The feed is a candidate filter, one aggregate row.**
  `DocumentService.ListAsync` returns only audience-allowed documents and emits
  **exactly one** aggregate `AccessAudit` row (`targetKind "document"`,
  `visibleCount`/`hiddenCount` set, `targetId = null`). *(C-M21·3.)*
- **GATE-3 — Detail/download = one decision row + 404-on-deny.** `GetAsync` / the
  serve route emit **exactly one** decision `AccessAudit` row (`targetId` set,
  counts null) and return **404 (not 403)** on deny (no existence leak).
  *(C-M21·4, C-M21·5.)*
- **GATE-4 — Zero new authorization surface.** The seam/claim pin passes: no new
  `AccessAction` / `Decide()` branch / `AccessVia` / `IAuthorizationService`
  method; `ClaimTypes.All` is unchanged. *(C-M21·2.)*
- **GATE-5 — Bytes ride ADR 0011 + download disposition.**
  `Document.MediaId` = the `IMediaStore` id; serving streams the blob behind the
  `Read` decision with `Content-Disposition: attachment`; an **orphan** blob (no
  `Document` row) is a 404. *(C-M21·6.)*
- **GATE-6 — Upload is a single audited write lane, standing-gated.**
  `UploadAsync` stores **one** `Document` (+ one `MediaObject`) in the caller's
  session, appends **exactly one** `AccessAudit` row, and a non-eligible actor is
  refused at the Web boundary. *(C-M21·7.)*

## Workflow (every unit, 7 steps)

1. **Read this register** (the Understanding, the [PROPOSED] D# set, the
   invariants, the FACES, the §gate, the §drift-guard, the closed `kw-l` key
   set).
2. **Read the unit plan** (`m21-uNN.md`) — the Goal, the Entry reads, the
   Deliverables, the Exit.
3. **Read the Entry reads** named in the unit plan (4–8 files, the design-doc
   sections the unit implements, the ADR, the code seams it touches).
4. **Execute** the Deliverables (≤ 7 files; implement the locked C# / keys
   exactly; do not amend a D#).
5. **Run the Exit gate** — one `dotnet build Kumunita.slnx -c Debug` + one
   `dotnet exec` test assembly, both green (U00: no build/test).
6. **Append a `## U##` section** to the handoff notes (5 lines: what was
   delivered, any open question, the next unit's entry point, no new drift).
7. **Move the unit plan** `in-progress/m21-uNN.md` → `done/m21-uNN.md` (flat,
   directly under `done/` — the M13–M20 convention, **not** a `done/m21/`
   subfolder).

## Unit-series rules

- **Order is U00 → U05.** U00 (docs + ADR) must land before any code unit,
  because the design doc's "Seams & contracts (Part 2)" section pins the exact
  C# U01–U04 implement. M21 is **already** `StatusNext` (M20's close opened it),
  so **there is no open-flip unit** — U01 goes straight to the first code unit.
  A later unit may read an earlier unit's code, but never re-derive a D#.
- **One write lane per unit.** U01 owns the `Document` doc + the
  `DocumentDocTypes` registration + the boot line + the `DocumentToAuditableResource`
  adapter + the DI registration. U02 owns the `DocumentService` (feed + detail +
  upload) + the DTOs + the `MediaOptions` document lane + the DI. U03 owns the
  `DocumentController` + the view models. U04 owns the views + nav + the **full**
  `kw-l` set (author). No unit both *reads* and *writes* a seam another unit
  owns.
- **One test-assembly exit per unit.** A Core unit (U01, U02) exits on
  Core.Tests; a Web unit (U03, U04) exits on Web.Tests. The close-flip unit
  (U05) exits on Web.Tests (it flips `Milestones.cs`, pinned by
  `MilestonesTests`). If a unit needs both, it is too big — split it (see the
  Atomicity contract).
- **The close flip is bracketed by U05 ONLY, and ONLY it touches
  `Milestones.cs` / `MilestonesTests`.** U05: M21 `StatusNext` → `StatusDone` +
  **promote M22** `StatusPlanned` → `StatusNext` + append `"M21"` to the
  `Shipped_Milestones_Are_Marked_Done` done-list + replace
  `M21_Is_The_Single_InProgress_Milestone` with
  `M22_Is_The_Single_InProgress_Milestone` + README/STATUS/ARCHITECTURE parity +
  tag the ADR 0122 index row `**Done** (M21)` + move all M21 artifacts flat to
  `done/`. **No other unit touches the roadmap.**
- **`kw-l` parity is authored in U04 and consumed by U03 + U04.** Every new
  user-visible string M21 introduces is a `KnownTranslationKeys` entry present,
  non-empty, in **all four** languages (en/de/fr/da); the
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` pin the
  closure. **U04 authors the COMPLETE M21 key set (the locked set below)**; U03
  *consumes* its keys and **adds none** (U03 ships the controllers + view models
  referencing the `kw-l` keys U04 authors — the views are U04's).

### The closed `kw-l` key set (D4/D5/D6) — locked by U00, authored by U04

| Key | Purpose (surface) |
| --- | --- |
| `documents.title` | Page title, the repository feed |
| `documents.empty` | Empty-state text (no documents visible to you) |
| `documents.upload` | The "Upload a document" button (authority standing) |
| `documents.upload_title` | Compose form title |
| `documents.upload_summary` | The optional one-line description field label |
| `documents.upload_file` | The file input label |
| `documents.upload_audience` | The audience-picker label (reuses the M2 audience editor) |
| `documents.upload.submit` | The compose submit button |
| `documents.download` | The "Download" action label (feed row + detail) |
| `documents.detail.type_size` | The file type / size meta label (detail) |
| `documents.detail.updated` | The "last updated" label (detail, `kw-dt`) |
| `documents.flash_uploaded` | "Document uploaded" flash |

## §drift-guard

**A unit stops (does not improvise) when it hits any of:**

- A D# it needs is not in the [PROPOSED] set, or two D#s contradict — **stop,
  report to the user**; do not pick one silently. (A D# is locked by U00; a
  post-U00 D# change is a design-doc §1.a amendment + a handoff-note line, and
  only U00 makes it.)
- A unit needs *both* test assemblies green to exit — **it is too big**; split
  it per the Atomicity contract rather than running both.
- A deliverable would require a **new `IAuthorizationService` signature, a new
  `Decide()` branch, a new `AccessVia`/`AccessAction`, or a new `AccessVia`
  member** — that violates C-M21·2 / D8; stop and report (the whole point of M21
  is that it *rides* `Read` via an adapter, it does **not** extend the seams).
- A unit is about to **encode per-document access in an identity claim, a new
  relational table, a standing, or a per-kind `AccessAction`** — that violates
  C-M21·1 / C-M21·2; stop and report (the `Audience` is the *only* access
  carrier).
- The serve route is about to **return 403 on deny** (an existence leak) or a
  feed is about to **list a document that 404s on detail** — that violates
  C-M21·4 / C-M21·5 / D7; stop and report (Deny → 404, feed and detail agree).
- A unit is about to **introduce a new storage lane** (anything other than the
  frozen ADR 0011 `IMediaStore`) or **inline-render an official document** — that
  violates D3 / D6 / C-M21·6; stop and report.
- A user-visible string that is **not** already in `KnownTranslationKeys` for all
  four languages is about to be rendered — add it to U04's closed set first; do
  not inline a string (the parity pin will fail, and that is the point).
- A unit other than **U05** is about to touch `Milestones.cs` or
  `MilestonesTests.cs` — stop; only the close-flip owns the roadmap (M21 has
  **one** flip — the close).
- A unit is about to **stage an email / notification** on upload — that is a
  D9·3 future lane, not M21; stop and report.

**The handoff-notes file is the cross-unit memory.** Every unit reads the
`## U##` sections before it and appends its own after; a unit does not re-derive
what an earlier unit already settled (a D# amendment, the key set, a seam shape,
the `MediaId` reference convention, the 404-on-deny posture).

**The register is the map, not the code.** If a unit is tempted to "just add a
`Decide()` branch for documents" or "serve the file on a public route" or "let
any resident upload," that is the §drift-guard firing — M21's value is precisely
that it does *not*: it adds a document + its audience + its ADR 0011 bytes, rides
the frozen `Read` path through one adapter, serves a download behind a decision,
and audits every read — with **zero new authorization surface**.
