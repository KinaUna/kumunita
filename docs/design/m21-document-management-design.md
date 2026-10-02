# M21 — Document management (design doc)

> **Milestone M21 — Document management.** The README M21 line, verbatim:
> "**Document management** — a shared repository for official documents,
> contracts, etc., with per-document access controls." M21 ships a **shared
> repository** of **official** documents (contracts, minutes, notices, bylaws,
> …) for the community, where **each document carries its own access control**
> (the `Audience`, reused verbatim from ADR 0006) and is **served as a
> download** behind a per-document **`Read` decision**.
>
> **A milestone over the frozen authorization + media seams.** Zero new
> `IAuthorizationService` surface, zero new `AccessAction`, zero new
> `AccessVia`, zero new `Decide()` branch, zero new storage lane (C-M21·2, D8,
> D3). The **only** additions are **one new bounded context**
> (`Kumunita.Core.Documents`) + **one new document** (`Document`, D1) on a
> **new additive `DocumentDocTypes` surface** (ADR 0004 §B.1) + **one new
> adapter** (`DocumentToAuditableResource`, `TargetKind = "document"`, D2) +
> **one new service** (`DocumentService`, D2/D4/D5) + **one new controller**
> (`DocumentController`, D5/D6/D7) + **one additive `MediaOptions` lane**
> (`DocumentAllowedContentTypes` / `IsDocumentAllowed`, D3) + **the closed
> `kw-l` key set** (12 keys × en/de/fr/da, §8). The six GATE acceptance tests
> (GATE-1…GATE-6, §9) + the drift-guard (§10) are locked in **ADR 0122
> (Accepted, 2026-10-01)**.
>
> **The frozen `IAuthorizationService` `Read` path (ADR 0006), the ADR 0011
> `IMediaStore` content-addressed volume, the ADR 0034 serve-behind-decision
> pattern, and the ADR 0004 §B.1 additive-surface discipline are
> byte-identical** (C-M21·2/6). A `Document` is a *catalog row* (the authority
> for **access**, its `Audience`); the blob is the *content* (the authority
> for **bytes**, the ADR 0011 `IMediaStore`). An orphan blob (no `Document`
> row) is a 404 — the document, not the blob, is the authority for access.
>
> **Status.** **LOCKED.** The decisions D1–D9, the invariants
> C-M21·1…C-M21·7, the FACES F1–F5 + the named trade, the §8 `kw-l` key list,
> and the §9 gate test names are locked in **ADR 0122 (Accepted, 2026-10-01)**.
> The `[PROPOSED]` set in the register
> `docs/plans-milestones/in-progress/plan-m21-document-management.md` is the
> locked set this doc restates **verbatim**, with **two additive amendments**
> recorded in §1.a (D5's upload standing confirmed as `GlobalAdmin ∪
> Moderator`; **C-M21·7's upload audit-row amended to "no audit row"** — the
> codebase convention the unit plan flagged).
>
> **The one thing every unit must respect:** M21 **rides the frozen `Read`
> path** through **one** adapter (`DocumentToAuditableResource`,
> `TargetKind = "document"`) and **adds zero new authorization surface**
> (D2, D8, C-M21·2). The `Audience` is the **only** access carrier (C-M21·1);
> a document is a **download, never an inline render** (D6, C-M21·6); **Deny is
> 404, not 403** (D7, C-M21·4); the **bytes ride the ADR 0011 `IMediaStore`**
> (D3, C-M21·6); and the upload is a **standing-gated write with no audit row**
> (D5, C-M21·7, §1.a). It does **not** add a branch to `Decide()`, a new
> `IAuthorizationService` signature, a new `AccessAction`, a new `AccessVia`,
> a new storage lane, a relational document table, or an inline-render route
> (the §drift-guard, §10, is the exact set of traps that would break it).

## 0 — Scope & non-scope

**What M21 does (register's "Understanding", verbatim):** M21 = **document
management.** The roadmap line is the scope:
> "Document management — a shared repository for official documents, contracts,
> etc., with per-document access controls."

M21 ships a **shared repository** of **official** documents (contracts,
minutes, notices, bylaws, …) for the community, where **each document carries
its own access control** (the `Audience`, reused verbatim from ADR 0006) and is
**served as a download** behind a per-document **`Read` decision**. Three
things follow from the roadmap sentence:

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

**What M21 is NOT (the register's "What M21 is NOT", verbatim):**

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

## 1 — Decisions (D1–D9)

**D1 — A new `Document` document, on its own additive `DocumentDocTypes`
surface (ADR 0004 §B.1).** A new document `Kumunita.Core.Documents.Document`
(namespace `Kumunita.Core.Documents`, a **new bounded context** alongside
`Posts` / `Events` / `Pages`), registered on a **new parallel additive surface**
`DocumentDocTypes.Configure(StoreOptions)` (the `M17DocTypes` / `MediaDocTypes`
shape — Marten-native POCO, conventional `Id` string, delta-detected +
idempotent, **no seeding**), wired with **one line** in `Program.cs`'s
`AddMarten` lambda (next to `M17DocTypes.Configure(opts);`,
`SchemaBootstrap.cs` **not touched** — it calls no `*DocTypes.Configure`).
Shape (locked by U00's design doc):

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

**D2 — M21 rides the **frozen** `Read` path — one new adapter + one new
`TargetKind`, zero new `IAuthorizationService` surface.**
`DocumentToAuditableResource : IAuditableResource` (the
`PostToAuditableResource` shape verbatim) exposes `Id`, `Name` (= `Title`),
`OwnerId`, `Audience`, `ComponentId => null` (documents are repository-level,
not component-scoped), and **`TargetKind => "document"`** (the **exact**
string — the aggregate/decision audit discriminator, C-M21·3/C-M21·4). The
`DocumentService` composes **only** the frozen `IAuthorizationService` + the
frozen `IUserInfoService` read seams + its own `IDocumentStore`, exactly like
`PostService`. Feed = one `CanSeeAsync` (aggregate row), detail/download = one
`CanAsync` (decision row), all `AccessAction.Read`.

*Forbids:* a new `AccessAction`, a new `Decide()` branch, a new
`IAuthorizationService` method, a new `AccessVia`, or a `DocumentService` that
reads `GroupMembership`/`DelegationGrant` for its own access decisions (C-M21·2).

**D3 — Bytes ride the **frozen** ADR 0011 `IMediaStore`; M21 adds **no** new
storage lane.** `Document.MediaId` is the **content-addressed** id returned by
the frozen `IMediaStore.PutAsync(content, filename, contentType, actorId, ct)`
(SHA-256, orphan-safe store-first). Serving =
`IMediaStore.OpenReadAsync(mediaId)` **behind the `Read` decision** (D6),
streamed with the stored `ContentType` + the original `Filename`. `MediaOptions`
(the ADR 0011 / ADR 0034 allowlist) gains one **additive** document lane —
`DocumentAllowedContentTypes` / `IsDocumentAllowed(contentType)` — in the exact
shape of the existing `AttachmentAllowedContentTypes` / `IsAttachmentAllowed`
(a PDF/DOCX/XLSX/CSV/ZIP allowlist; the image lane is *not* the document lane).

*Forbids:* a new storage mechanism, in-DB blob bytes, a public/static serving
route, or a serving path that bypasses the `Read` decision (D6/D7).

**D4 — The repository feed is a **candidate filter over all documents**, never
a gate (C-M21·3).** `DocumentService.ListAsync(actorId, page)` returns **all**
documents whose `Audience` allows the caller (one `CanSeeAsync` over the
candidate set), in `Created` order, and emits **one aggregate** `AccessAudit`
row (`targetKind "document"`, `visibleCount`/`hiddenCount` set,
`targetId = null`) — the `PostService.ListFeedAsync` aggregate shape. A
**component is not a gate** (`ComponentId = null` on the adapter); the only
filter is each document's own `Audience`.

*Forbids:* a component gate, a per-author feed gate, an unaudited feed read, or
a feed that lists a document the caller cannot open (feed and detail/download
must agree — C-M21·5).

**D5 — [PROPOSED] The upload/compose standing is the **authority lane**:
`GlobalAdmin ∪ Moderator`.** Posting an **official** document is an
**authority act** — the Web boundary gates the compose form to the elevated
standing (`GlobalAdmin ∪ Moderator`), the ADR 0017 "author-of-record ∪
GlobalAdmin" shape generalized to the community's authority. The Core write
lane `DocumentService.UploadAsync(DocumentUpload, actorId, IDocumentSession)`
is **standing-agnostic** (it records the `OwnerId = actorId` and one audited
write row; it does **not** re-check roles — the Web boundary owns the standing
check, the ADR 0006/ADR 0017 convention). One audited write: the `Document` is
stored in the **caller's** session (ADR 0006-E same-transaction) alongside the
already-`PutAsync`'d blob (D3).

*Forbids:* any-resident upload (that would make "official" meaningless — the
roadmap says *official* documents), an unaudited direct `session.Store` from a
controller, or a Core seam that re-derives the standing (the Web boundary owns
it).

> **U00 may amend D5's exact standing in U00** (e.g. widen to
> "author-of-record ∪ GlobalAdmin" per ADR 0017, or narrow to GlobalAdmin-only).
> The *shape* — one authority-gated Web compose + one standing-agnostic audited
> Core write lane — is locked; only the standing *set* is the amendment surface.

**D6 — A document is a **download, not an inline render**
(`Content-Disposition: attachment`).** The serve route (the
`AttachmentController` / ADR 0034 download shape) streams
`IMediaStore.OpenReadAsync` **behind the `Read` decision**, with the stored
`ContentType` + `Content-Disposition: attachment; filename="<stored-or-{id}.bin>"`
(RFC 6266) + `X-Content-Type-Options: nosniff`. An official document is
*downloaded*, never rendered inline.

*Forbids:* inline `<img>`/`<embed>` of an official document, a public CDN/static
route, or a serve route with no per-call `Read` decision.

**D7 — Deny is **404, not 403** (no existence leak); feed and detail agree
(C-M21·4/C-M21·5).** The serve route is the `ContentImageController` /
`AttachmentController` 5-step: (1) validate the id (1–128 hex-ish) else
**400**; (2) `Document` miss → **404**; (3) blob (`IMediaStore`) miss →
**404** (orphan → the document is the authority); (4) **one**
`CanAsync(actorId, AccessAction.Read, new DocumentToAuditableResource(doc))`
→ **Deny → 404 not 403** (the caller cannot distinguish "not there" from "not
for you"); the **single** `AccessAudit` row is emitted *by the seam*
(in-transaction, C3), **not** the controller; (5) allow → stream the blob (D6).
A document in the **feed** is openable by the same caller; a document the feed
hides 404s on detail (the same `Read` decision governs both).

*Forbids:* a 403 that reveals existence, a feed listing a document that 404s on
detail, or an audit row written by the controller instead of the seam.

**D8 — **Zero new authorization surface** (the C-M21·2 pin).** M21 adds **no**
`AccessAction`, **no** `Decide()` branch, **no** `AccessVia`, **no**
`IAuthorizationService` method; the claim set (`ClaimTypes.All`) is
**unchanged**. It adds exactly **one** `IAuditableResource` adapter + **one**
`TargetKind` (`"document"`) + **one** new document + **one** `DocumentService`
+ **one** `DocumentController` + **one** additive `MediaOptions` lane.

*Forbids:* any extension of the frozen authorization seams (a §drift-guard stop
— the whole point of M21 is that it *rides* `Read`, it does not extend it).

**D9 — **Deferred lanes** (each a future ADR, not part of M21).** (1)
**Versioning / revision history** — a document's revision chain + diff. M21
keeps **one** current blob + `Modified?` (the field is present but M21 sets it
only on a replace, which is itself a future lane); a versioned history is a
larger design. (2) **Full-text search within document bodies** — M21 does not
index PDF/docx text; a search over *titles* may reuse the M8 `/search` lane (a
future decision), and body-text search is a future lane. (3) **Document
comments / discussion** — rides M3/M3b moderation; a future lane. (4) **A "new
document posted" notification** — rides M6 (ADR 0076); M21 stages **no** email.
A nudge lane is future. (5) **Per-kind document taxonomy** (contracts vs minutes
vs notices vs bylaws, each with its own standing) — M21 is **one** document
type; a taxonomy is a future lane. (6) **Scheduled expiry / auto-purge** — the
M20 §6.4 durable-job shape; a future lane (M21 has **no** `Expiry` field and
**no** purge job). (7) **Edit-by-replacement / author soft-delete** — the
M14/M24 lane shape; M21's write lane is **upload-only** (the uploader's own
draft is visible to them via the owner branch; a public replace/edit is a future
lane).

### 1.a — Amendments (additive; the register's original text is not rewritten)

> Per the register's §drift-guard rule and the M19/M20 precedent, an amendment
> is **additive**: the register's D# text above is preserved verbatim, and the
> amendment is recorded here. U02/U03 implement **whichever of the two surfaces
> is locked below**.

**A1 — D5's upload standing (locked: `GlobalAdmin ∪ Moderator`, confirmed).**
The register flags D5's exact standing as the amendment surface and permits U00
to widen ("author-of-record ∪ GlobalAdmin", per ADR 0017) or narrow
(GlobalAdmin-only). **Locked at `GlobalAdmin ∪ Moderator`**, the register's
proposed set, for three reasons verified against the live codebase:

1. **The roadmap word is *official*.** "official documents, contracts, etc." is
   an **authority act** — a post that any resident could file would not be
   "official". This is the same reasoning ADR 0017 uses to gate announcements
   (the platform's authority-published surface) to elevated standing rather
   than to any member.
2. **`Moderator` is the standing the repo already treats as the
   community-authority lane.** `PostService.CreatePostAsync` (verified) admits
   `GlobalAdmin` **or** a component-`Moderator` to post; ADR 0003 / ADR 0017 /
   ADR 0021 all treat `Moderator` as the delegated community-authority role.
   Documents are a **community-level** (not component-scoped) authority act, so
   the standing is the **unscoped** `Roles.Moderator` ∪ `Roles.GlobalAdmin` —
   the same claim-set the Web boundary already resolves (a claim-set-as-
   principal, not a DB read — the ADR 0006-D "feature modules never re-derive
   access" boundary holds: the Core write lane records `OwnerId` and does not
   re-check roles).
3. **Narrowing to GlobalAdmin-only would starve the lane on a one-neighborhood
   deployment** (a neighborhood with a few admins but no standing Moderator
   would have no one who is not a full admin to publish minutes — the exact
   "authority" the roadmap names). Widening to "author-of-record ∪ GlobalAdmin"
   per ADR 0017 would instead let *any resident who wrote a prior document*
   publish, which re-introduces the "any-resident upload" that D5's *Forbids*
   tail rules out. The `GlobalAdmin ∪ Moderator` set is the least widening that
   still reads as "authority". **U03 enforces this set at the Web compose
   boundary; `DocumentService.UploadAsync` remains standing-agnostic.**

**A2 — C-M21·7's upload audit-row (locked: one standing-gated write,
*no* `AccessAudit` row).** The register's C-M21·7 text says the upload
"appends **exactly one** `AccessAudit` row." That is **amended here** to match
the live codebase, where **writes do not emit audit rows — only `Read`
decisions do**. Verified against three precedents the unit plan named:

- **`PostService.CreatePostAsync`** (the closest write-lane precedent): stores
  the `Post` + notification rows and `SaveChangesAsync`, and its doc comment
  states verbatim *"the audit row is the caller's job; the service does not
  open a second session and does not append an audit row in the denial path"* —
  i.e. the write lane writes **no** `AccessAudit` row.
- **`AttachmentController.Upload`** (the ADR 0034 upload lane): its doc comment
  states verbatim *"**No audit row**: the write is authenticated, not an
  audience-restricted read (the image lane makes the same choice — carried
  over)."*
- **`AnnouncementService`** create lane (the ADR 0017 authority-write
  precedent): *"announcements have no audit lane at all"* on the write path.

The mechanism confirms it: `AccessAudit` rows are produced **only** by the
frozen `IAuthorizationService` `CanAsync` / `CanSeeAsync` seams (the
`Read`-decision path) — `AuthorizationService.cs` is the sole site that
`session.Store(new AccessAudit …)`s, and it does so inside those decision
overloads. The upload is a **standing-gated write** (the Web boundary checks
`GlobalAdmin ∪ Moderator`, A1), not a `Read` decision, so there is **no**
seam that would emit a row and **no** codebase precedent that would justify one.

**Resolution locked: the upload is a single standing-gated write with *no*
`AccessAudit` row.** C-M21·7 is therefore amended, in this unit, to:
> "Upload is a single audited-free write lane, standing-gated. `UploadAsync`
> stores **one** `Document` (+ the already-`Put`'d `MediaObject` via
> `IMediaStore`) in the **caller's** session and **appends no `AccessAudit`
> row** (the write is authenticated, not an audience-restricted read — the
> `PostService.CreatePostAsync` / `AttachmentController.Upload` /
> `AnnouncementService` write-lane convention); the Web boundary owns the
> standing check (`GlobalAdmin ∪ Moderator`, A1) and a non-eligible actor is
> refused **at the Web boundary** (the Core seam records the `OwnerId` and does
> not re-check roles)."

The **reads** are unchanged and remain audited: the feed is one aggregate
`AccessAudit` row (C-M21·3, GATE-2) and the detail/download is one decision
row (C-M21·4, GATE-3) — both emitted by the frozen `IAuthorizationService`
seams. **GATE-6** (below, §9) is amended to reflect the no-row upload.

## 2 — Invariants (C-M21·1 … C-M21·7)

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
- **C-M21·4 — Detail/download = one `Read` decision + one decision audit row
  (D6, D7).** `GetAsync`/serve = one `CanAsync` + **exactly one** `AccessAudit`
  row (`targetId = doc.Id`, counts null), emitted **by the seam** (in-
  transaction, C3); **Deny → 404 not 403** (no existence leak). A test pins the
  decision-row shape + the 404-on-deny.
- **C-M21·5 — Feed and detail/download agree (D7).** A document in the caller's
  feed is openable by that caller; a document the feed hides 404s on detail —
  the **same** `Read` decision governs both (no separate detail gate). A test
  asserts feed-inclusion == detail-allow for the same actor.
- **C-M21·6 — Bytes ride ADR 0011 (D3); the `Document` doc is the catalog
  (D1, D6).** `Document.MediaId` = the `IMediaStore` id; serving =
  `IMediaStore.OpenReadAsync` **behind the decision** with
  `Content-Disposition: attachment`; an **orphan** blob (no `Document` row) is a
  404 (the document is the authority for access). A test pins the `MediaId`
  reference + the download disposition.
- **C-M21·7 — Upload is a single standing-gated write lane (D5), amended per
  §1.a A2 to *no* `AccessAudit` row.** `UploadAsync` stores **one** `Document`
  (+ the already-`Put`'d `MediaObject` via `IMediaStore`) in the **caller's**
  session and **appends no `AccessAudit` row** (the write is authenticated, not
  an audience-restricted read — the `PostService.CreatePostAsync` /
  `AttachmentController.Upload` / `AnnouncementService` write-lane convention);
  the Web boundary owns the standing check (`GlobalAdmin ∪ Moderator`, A1) and
  a non-eligible actor is refused **at the Web boundary** (the Core seam records
  the `OwnerId` and does not re-check roles). A test pins the single write + the
  standing + the **absence** of an upload audit row.

## 3 — FACES (F1–F5) + the named trade

- **F1 — A resident sees the documents their audience allows** (C-M21·1, C-M21·3,
  C-M21·5): the repository feed lists only the documents the caller's `Audience`
  grants; one aggregate audit row.
- **F2 — A document is opened as a download behind its `Read` decision**
  (C-M21·4, C-M21·6, D6, D7): one decision audit row; Deny → 404 (no existence
  leak); `Content-Disposition: attachment`.
- **F3 — An authority uploads an official document with a chosen audience**
  (C-M21·7, D5): `GlobalAdmin ∪ Moderator` posts; the `OwnerId` is set; one
  audited write; the document's `Audience` is written **verbatim** (ADR 0001-B —
  the uploader's choice is absolute; no auto-add of grants).
- **F4 — An empty-audience document is visible to no one but its uploader**
  (C-M21·1, ADR 0006-C1): the deny-by-default floor — the lean default.
- **F5 — Delegated read flows** (C-M21·1, ADR 0006-C2): a delegate with `Read`
  in scope sees the documents their principal's audience allows; a delegate
  without `Read` sees nothing (the frozen `Decide()` C2 branch — M21 **rides**
  it, does not extend it).

**The named trade.** M21 buys *a shared, audited, per-access-controlled
repository for official documents, served as downloads* with **zero new
authorization surface + one new document + one new adapter + one new service +
one new controller + one additive `MediaOptions` lane + a closed `kw-l` set**,
in exchange for **no versioning, no full-text, no comments, no taxonomy, no
auto-expiry, no edit-by-replacement** (D9): M21 is *the document + its audience
+ its bytes + its download*, **not** a document-management *system*. The
deferred lanes (D9·1…D9·7) are deliberately *not* in M21: they are larger
designs that need their own ADRs, and M21's value (a neighborhood that keeps its
official contracts and minutes in one place, each readable only by the people
the uploader chose, each audited when opened) is delivered by the single
document + its `Audience` + its ADR 0011 bytes.

## 4 — The `Document` document + `DocumentDocTypes` (exact C#)

The `Document` POCO (D1) is the locked shape in §1, restated:

```csharp
namespace Kumunita.Core.Documents;

/// <summary>
/// A catalog row for an official document (M21, D1). The authority for
/// **access** is this row's <see cref="Audience"/> (ADR 0006-C1 deny-by-
/// default; C-M21·1); the authority for **bytes** is the ADR 0011 content-
/// addressed blob whose id is <see cref="MediaId"/> (D3, C-M21·6).
/// </summary>
public sealed class Document
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string MediaId { get; set; } = string.Empty;
    public string? Filename { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public Audience Audience { get; set; } = new();
    public string OwnerId { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
}
```

`DocumentDocTypes` (D1) — the new additive surface (the `M17DocTypes` /
`MediaDocTypes` shape — ADR 0004 §B.1, delta-detected, idempotent, no seeding):

```csharp
using Marten;

namespace Kumunita.Core.Documents;

/// <summary>
/// The M21 additive doc surface (ADR 0004 §B.1 — the M17DocTypes /
/// MediaDocTypes parallel-surface shape). Registered with one line in
/// Program.cs's AddMarten lambda, next to M17DocTypes.Configure(opts);.
/// SchemaBootstrap.cs is NOT touched (it calls no *DocTypes.Configure).
/// </summary>
public static class DocumentDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        opts.PatchDocuments<
            Document>(d => d.Audience);   // register the POCO + its Audience reference (delta-detected, idempotent)
    }
}
```

## 5 — The `DocumentToAuditableResource` adapter + the `DocumentService` (feed + detail + upload) (exact C#)

The adapter (D2) — the `PostToAuditableResource` shape verbatim, with
`ComponentId => null` (documents are repository-level, not component-scoped) and
`TargetKind => "document"`:

```csharp
using Kumunita.Core.Authorization;

namespace Kumunita.Core.Documents;

/// <summary>
/// Adapter (M21, D2): presents a <see cref="Document"/> to the frozen
/// IAuthorizationService as an IAuditableResource. The PostToAuditableResource
/// shape verbatim, with two M21 differences: ComponentId => null (documents are
/// repository-level, not component-scoped — C-M21·2/C-M21·3) and TargetKind =>
/// "document" (the aggregate/decision audit discriminator — C-M21·3/C-M21·4).
/// Name = Title (the audit row's human-facing label); OwnerId = OwnerId (the
/// owner branch of the decision algorithm is the only lane that lets the
/// uploader see their own empty-audience document — C-M21·1); Audience =
/// Document.Audience projected verbatim (ADR 0001-B; the adapter never mutates
/// it).
/// </summary>
public sealed class DocumentToAuditableResource : IAuditableResource
{
    public DocumentToAuditableResource(Document doc) => Document = doc;
    public Document Document { get; }
    public string Id => Document.Id;
    public string Name => Document.Title;
    public string? OwnerId => Document.OwnerId;
    public Audience? Audience => Document.Audience;
    public string? ComponentId => null;          // repository-level, not component-scoped
    public string TargetKind => "document";
}
```

The service (D2/D4/D5, A1/A2) — the `PostService` "caller of the two frozen
seams" shape. Feed = one `CanSeeAsync` (one aggregate row, D4); detail = one
`CanAsync` (one decision row, D7); upload = one standing-agnostic write with **no
audit row** (D5, A1/A2 — the `PostService.CreatePostAsync` write-lane convention):

```csharp
using Kumunita.Core.Authorization;
using Kumunita.Core.Media;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Documents;

/// <summary>
/// The documents-side composition service (M21, D2/D4/D5). A pure caller of the
/// two frozen modules — IAuthorizationService (the single Read decision path)
/// and the ADR 0011 IMediaStore (D3) — plus its own IDocumentStore for the
/// read/write lanes; it never reads GroupMembership/DelegationGrant for its own
/// access decisions (the ADR 0006-D "feature modules never re-derive access"
/// boundary). The PostService shape verbatim (C-M21·2).
/// </summary>
public sealed class DocumentService
{
    private static readonly int PageSize = 30;

    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;
    private readonly IMediaStore _media;

    public DocumentService(IAuthorizationService authz, IDocumentStore store, IMediaStore media)
    {
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _media = media ?? throw new ArgumentNullException(nameof(media));
    }

    /// <summary>
    /// The repository feed (D4, C-M21·3): all documents whose Audience allows the
    /// caller — one CanSeeAsync over the paged candidate set (Created order),
    /// one aggregate AccessAudit row (targetKind "document", visibleCount/
    /// hiddenCount set, targetId = null). A component is NOT a gate (D4).
    /// </summary>
    public async Task<DocumentListResult> ListAsync(string actorId, int page)
    {
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        var candidates = await session
            .Query<Document>()
            .OrderByDescending(d => d.Created)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        if (candidates.Count == 0)
            return new DocumentListResult(Visible: Array.Empty<Document>(), HiddenCount: 0, Page: page, Total: 0, HasMore: false);

        int total = await session.Query<Document>().CountAsync();

        // C-M21·3 — one shared matching pass over the candidate set; one aggregate
        // audit row (the PostService.ListFeedAsync shape); TargetKind "document".
        var visibleSet = await _authz.CanSeeAsync(
            actorId, AccessAction.Read,
            candidates.Select(d => new DocumentToAuditableResource(d)));

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        var visible = candidates.Where(d => visibleIds.Contains(d.Id)).ToList();

        return new DocumentListResult(Visible: visible, HiddenCount: visibleSet.HiddenCount, Page: page, Total: total, HasMore: candidates.Count == PageSize);
    }

    /// <summary>
    /// Detail (D7, C-M21·4): one CanAsync(Read) → Allow/Deny. A Deny returns a
    /// null result (the Web layer 404s — no existence leak, C-M21·4); the single
    /// decision audit row is emitted by the seam (in-transaction, C3), not here.
    /// A missing document returns null (the Web layer 404s, zero rows).
    /// </summary>
    public async Task<DocumentDetailResult?> GetAsync(string docId, string actorId)
    {
        if (string.IsNullOrEmpty(docId)) throw new ArgumentException(nameof(docId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException(nameof(actorId));

        await using var session = _store.QuerySession();
        var doc = await session.LoadAsync<Document>(docId);
        if (doc is null) return null;                    // document miss → Web 404 (zero rows)

        var decision = await _authz.CanAsync(actorId, AccessAction.Read, new DocumentToAuditableResource(doc));
        if (!decision.Allowed) return null;              // Deny → Web 404 (one Deny row, by the seam)

        return new DocumentDetailResult(Document: doc);
    }

    /// <summary>
    /// Upload (D5, A1/A2): one standing-agnostic write in the caller's session.
    /// The blob is ALREADY Put by the Web layer (the IMediaStore is store-first,
    /// orphan-safe — C-MED·7); this lane stores the Document catalog row +
    /// SaveChangesAsync. **No AccessAudit row** (A2 — the write is authenticated,
    /// not an audience-restricted read; the PostService.CreatePostAsync /
    /// AttachmentController.Upload / AnnouncementService write-lane convention).
    /// Standing-agnostic: the Web boundary owns the GlobalAdmin ∪ Moderator check
    /// (A1); this seam records OwnerId and does not re-check roles.
    /// </summary>
    public async Task<Document> UploadAsync(DocumentUpload upload, string actorId, IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(upload);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException(nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var doc = new Document
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = upload.Title,
            Summary = upload.Summary,
            MediaId = upload.MediaId,       // the IMediaStore id (D3, C-M21·6)
            Filename = upload.Filename,
            ContentType = upload.ContentType,
            SizeBytes = upload.SizeBytes,
            Audience = upload.Audience,     // ADR 0001-B — written verbatim; never mutated here
            OwnerId = actorId,
            Created = DateTimeOffset.UtcNow
        };

        session.Store(doc);
        await session.SaveChangesAsync();    // one SaveChangesAsync (C3)
        return doc;
    }
}

/// <summary>The feed result (D4, C-M21·3) — the FeedResult shape.</summary>
public sealed record DocumentListResult(IReadOnlyList<Document> Visible, int HiddenCount, int Page, int Total, bool HasMore);

/// <summary>The detail result (D7, C-M21·4).</summary>
public sealed record DocumentDetailResult(Document Document);

/// <summary>The upload DTO (D5, A1/A2) — the Web boundary resolves the blob id first.</summary>
public sealed record DocumentUpload(string Title, string? Summary, string MediaId, string? Filename, string ContentType, long SizeBytes, Audience Audience);
```

## 6 — The `MediaOptions` document lane (D3) (exact C#)

The additive document lane on `MediaOptions` (D3) — in the exact shape of the
existing `AttachmentAllowedContentTypes` / `ResolvedAttachmentAllowedTypes` /
`IsAttachmentAllowed` (the image lane is *not* the document lane; the attachment
lane is *not* the document lane either — a distinct positive-only gate):

```csharp
    /// <summary>
    /// Comma-separated allowed Content-Types for the document lane
    /// (case-insensitive; D3). Distinct from AttachmentAllowedContentTypes
    /// (C-ATT·6) and AllowedContentTypes (C-MED·5) — the document lane has its
    /// own gate; the config key is Media:DocumentAllowedContentTypes.
    /// Positive-only; SVG excluded (a document is a download, never inline — D6).
    /// </summary>
    public string? DocumentAllowedContentTypes { get; set; }

    /// <summary>
    /// The resolved document allowlist (D3). A neighborhood set of OFFICIAL
    /// document types — PDF / Office docs / text / csv / zip (the attachment
    /// set minus the raster image types — an official document is not a photo).
    /// Reuses MaxBytes (not a second size cap). The attachment + image lanes are
    /// untouched.
    /// </summary>
    public IEnumerable<string> ResolvedDocumentAllowedTypes =>
        (DocumentAllowedContentTypes ??
         "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/plain,text/csv,application/zip")
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

    /// <summary>
    /// Whether contentType is on the document allowlist (case-insensitive; D3).
    /// Mirrors IsAttachmentAllowed over the document set — the attachment lane's
    /// IsAttachmentAllowed is untouched.
    /// </summary>
    public bool IsDocumentAllowed(string? contentType) =>
        !System.String.IsNullOrWhiteSpace(contentType)
        && ResolvedDocumentAllowedTypes.Any(t =>
            System.String.Equals(t, contentType.Trim(), System.StringComparison.OrdinalIgnoreCase));
```

## 7 — The `DocumentController` serve + upload (D5, D6, D7) (exact route/action shape)

The controller (D5/D6/D7) — the `AttachmentController` serve-behind-decision
5-step + the ADR 0011 guards-before-write ordering, with **one** deliberate
difference: the standing gate is on the **compose** surface (the Web boundary
owns the `GlobalAdmin ∪ Moderator` check, A1), and the upload writes **no**
audit row (A2). The 5-step serve order (frozen, C-M21·4/5/6, D6/D7):

1. **Validate the id** (1–128 hex-ish) else **400** — `BadRequest()`.
2. **`Document` miss** → **404** (`NotFound()`, zero audit rows).
3. **Blob (`IMediaStore`) miss** → **404** (orphan → the document is the
   authority; `IMediaStore.GetAsync` miss → `NotFound()`, zero audit rows).
4. **One `CanAsync(actorId, AccessAction.Read, new
   DocumentToAuditableResource(doc))`** → **Deny → 404 not 403** (no existence
   leak). The **single** `AccessAudit` row is emitted *by the seam* (in-
   transaction, C3), **not** the controller.
5. **Allow** → `IMediaStore.OpenReadAsync(mediaId)` →
   `File(stream, stored.ContentType)` + `X-Content-Type-Options: nosniff` +
   `Content-Disposition: attachment; filename="<stored-or-{id}.bin>"` (RFC 6266,
   D6).

```csharp
// GET /documents — the repository feed (D4, C-M21·3). [Authorize] (the Web
// layer enforces the standing for *composing*; the feed is read by any resident
// their Audience allows — F1).
[HttpGet("/documents")]
[Authorize]
public async Task<IActionResult> Index(int page = 1) { /* DocumentService.ListAsync → view */ }

// GET /documents/{id} — the detail page (D7, C-M21·4). One Read decision; Deny
// → 404 not 403 (no existence leak); a feed-visible document is openable (C-M21·5).
[HttpGet("/documents/{id}")]
[Authorize]
public async Task<IActionResult> Detail(string id) { /* DocumentService.GetAsync → view or NotFound() */ }

// GET /documents/new — the compose form (D5, A1). The Web boundary checks
// GlobalAdmin ∪ Moderator FIRST; a non-eligible actor is refused (the ADR 0017
// compose-standing shape). Renders the M2 audience editor.
[HttpGet("/documents/new")]
[Authorize(Roles = "GlobalAdmin,Moderator")]
public IActionResult New() { /* render the compose form (U04 view) */ }

// POST /documents — the upload write lane (D5, A1/A2, D6). [Authorize(Roles =
// "GlobalAdmin,Moderator")] (A1) + [ValidateAntiForgeryToken]. Guards-before-
// write (ADR 0011 verbatim): empty → 400; > MaxBytes → 413; disallowed type
// (IsDocumentAllowed) → 415; NO file written on any guard. Then one
// IMediaStore.PutAsync (store-first, orphan-safe — C-MED·7), then one
// DocumentService.UploadAsync (one Document row, SaveChangesAsync, NO audit row
// — A2). The standing check is the Web boundary's (A1); the Core seam records
// OwnerId and does not re-check roles.
[HttpPost("/documents")]
[Authorize(Roles = "GlobalAdmin,Moderator")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Upload([FromForm] DocumentUploadForm form)
{
    // Guards (ADR 0011 verbatim):
    if (form.File is null || form.File.Length == 0) return BadRequest("Choose a file.");       // 400
    if (mediaOpts.Value.MaxBytes > 0 && form.File.Length > mediaOpts.Value.MaxBytes)
        return StatusCode(StatusCodes.Status413RequestEntityTooLarge);                          // 413
    if (!mediaOpts.Value.IsDocumentAllowed(form.File.ContentType))
        return StatusCode(StatusCodes.Status415UnsupportedMediaType);                           // 415
    using var ms = new MemoryStream(); await form.File.CopyToAsync(ms);
    var stored = await media.PutAsync(ms.ToArray(), form.File.FileName, form.File.ContentType, subject); // store-first
    await docs.UploadAsync(new DocumentUpload(form.Title, form.Summary, stored.Id, form.File.FileName, form.File.ContentType, form.File.Length, form.Audience), subject, session); // one write, NO audit row (A2)
    return /* flash documents.flash_uploaded (U04 key) + redirect to the detail page */;
}

// GET /documents/{id}/download — the serve route (D6, D7, C-M21·4/5/6). The
// 5-step order above. [Authorize] (the standing is NOT the gate — the per-
// document Read decision is; an anonymous visitor to a public document would be
// served — but the C-M21·1 floor is deny, so a public document is reachable only
// via the owner branch or a delegated Read). No [Authorize(Roles=…)] here — the
// per-call Read decision governs (the ADR 0034 serve posture).
[HttpGet("/documents/{id}/download")]
[Authorize]
public async Task<IActionResult> Download(string id)
{
    // Step 1: validate the id (1–128 hex-ish) → 400
    if (!IsValidDocId(id)) return BadRequest();
    // Step 2: Document miss → 404 (zero rows)
    var doc = await docs.GetAsync(id, actorId);   // GetAsync runs one CanAsync (the Read decision)
    if (doc is null) return NotFound();            // Deny OR missing → 404 (no existence leak)
    // Step 3: blob miss → 404 (orphan → the document is the authority; zero rows)
    var stored = await media.GetAsync(doc.Document.MediaId);
    if (stored is null) return NotFound();
    // Step 4: (the single CanAsync + one Deny row already ran inside GetAsync — by the seam)
    // Step 5: serve (D6) — Content-Disposition: attachment + nosniff + stored ContentType
    var stream = await media.OpenReadAsync(doc.Document.MediaId);
    Response.Headers["X-Content-Type-Options"] = "nosniff";
    var filename = SanitizeFilename(doc.Document.Filename, id);
    Response.Headers["Content-Disposition"] =
        "attachment; filename=\"" + filename + "\"; filename*=UTF-8''" + Uri.EscapeDataString(filename);
    return File(stream, stored.ContentType);
}
```

> **Note on the feed/detail agreement (C-M21·5):** the detail page (`Detail`)
> and the download (`Download`) both call `DocumentService.GetAsync`, which runs
> the **same** single `CanAsync(Read)` — so a document in the feed (F1) is
> openable by the same caller, and a document the feed hides 404s on both detail
> and download (the same `Read` decision governs all three read surfaces). The
> `Document` miss and the blob (orphan) miss are the two pre-decision 404
> branches (zero audit rows), per D7's 5-step order.

## 8 — The `kw-l` key list (the closed key set × en/de/fr/da)

The exact closed key set from the register (U04 authors the
`KnownTranslationKeys` entries in **all four** languages; U03/U04 consume —
the `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` pin the
closure):

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

## 9 — §gate (the six acceptance tests)

- **GATE-1 — Per-document access, deny-by-default.** An **empty**-audience
  document is denied to a non-author and allowed to its uploader (the owner
  branch). *(C-M21·1.)* — pin: `DocumentDetail_When_Audience_Is_Empty_Denies_Non_Author_Allows_Owner`.
- **GATE-2 — The feed is a candidate filter, one aggregate row.**
  `DocumentService.ListAsync` returns only audience-allowed documents and
  emits **exactly one** aggregate `AccessAudit` row (`targetKind "document"`,
  `visibleCount`/`hiddenCount` set, `targetId = null`). *(C-M21·3.)* — pin:
  `DocumentFeed_Emits_One_Aggregate_Audit_Row_And_Filters_By_Audience`.
- **GATE-3 — Detail/download = one decision row + 404-on-deny.** `GetAsync` /
  the serve route emit **exactly one** decision `AccessAudit` row (`targetId`
  set, counts null) and return **404 (not 403)** on deny (no existence leak).
  *(C-M21·4, C-M21·5.)* — pin:
  `DocumentDetail_Deny_Returns_404_And_One_Decision_Row` (plus
  `DocumentFeed_And_Detail_Agree_For_Same_Actor`).
- **GATE-4 — Zero new authorization surface.** The seam/claim pin passes: no
  new `AccessAction` / `Decide()` branch / `AccessVia` / `IAuthorizationService`
  method; `ClaimTypes.All` is unchanged. *(C-M21·2.)* — pin:
  `Document_Adds_No_New_Authorization_Surface`.
- **GATE-5 — Bytes ride ADR 0011 + download disposition.**
  `Document.MediaId` = the `IMediaStore` id; serving streams the blob behind
  the `Read` decision with `Content-Disposition: attachment`; an **orphan**
  blob (no `Document` row) is a 404. *(C-M21·6.)* — pin:
  `DocumentDownload_Streams_Adr0011_Blob_As_Attachment_And_404s_Orphan`.
- **GATE-6 — Upload is a single standing-gated write lane, no audit row
  (amended per §1.a A2).** `UploadAsync` stores **one** `Document` (+ one
  `MediaObject`) in the caller's session, appends **no** `AccessAudit` row
  (the write is authenticated, not an audience-restricted read — the
  `PostService.CreatePostAsync` / `AttachmentController.Upload` /
  `AnnouncementService` write-lane convention), and a non-eligible actor is
  refused at the Web boundary (the `GlobalAdmin ∪ Moderator` standing, A1).
  *(C-M21·7, amended.)* — pin:
  `DocumentUpload_Stores_One_Row_No_Audit_Row_And_Web_Boundary_Enforces_Standing`.

## 10 — §drift-guard

**A unit stops (does not improvise) when it hits any of:**

- A D# it needs is not in the [PROPOSED] set, or two D#s contradict — **stop,
  report to the user**; do not pick one silently. (A D# is locked by U00; a
  post-U00 D# change is a design-doc §1.a amendment + a handoff-note line, and
  only U00 makes it.)
- A unit needs *both* test assemblies green to exit — **it is too big**; split
  it per the Atomicity contract rather than running both.
- A deliverable would require a **new `IAuthorizationService` signature, a new
  `Decide()` branch, a new `AccessVia`/`AccessAction`, or a new `AccessVia`
  member** — that violates C-M21·2 / D8; stop and report (the whole point of
  M21 is that it *rides* `Read` via an adapter, it does **not** extend the
  seams).
- A unit is about to **encode per-document access in an identity claim, a new
  relational table, a standing, or a per-kind `AccessAction`** — that violates
  C-M21·1 / C-M21·2; stop and report (the `Audience` is the *only* access
  carrier).
- The serve route is about to **return 403 on deny** (an existence leak) or a
  feed is about to **list a document that 404s on detail** — that violates
  C-M21·4 / C-M21·5 / D7; stop and report (Deny → 404, feed and detail agree).
- A unit is about to **introduce a new storage lane** (anything other than the
  frozen ADR 0011 `IMediaStore`) or **inline-render an official document** —
  that violates D3 / D6 / C-M21·6; stop and report.
- A unit is about to **emit an `AccessAudit` row on the upload write** — that
  violates §1.a A2 (the codebase write-lane convention — the write is
  authenticated, not an audience-restricted read); stop and report (reads audit;
  writes do not — the `PostService.CreatePostAsync` / `AttachmentController.Upload`
  / `AnnouncementService` precedent).
- A user-visible string that is **not** already in `KnownTranslationKeys` for
  all four languages is about to be rendered — add it to U04's closed set first;
  do not inline a string (the parity pin will fail, and that is the point).
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
audits every *read* — with **zero new authorization surface** and **no audit
row on the write**.

## 11 — §deferred (the D9 lanes)

The seven deferred lanes (each a future ADR, listed so a unit does not reach for
them — D9, verbatim):

1. **Versioning / revision history** — a document's revision chain + diff.
   *Why deferred:* M21 keeps one current blob + `Modified?` (the field is
   present but set only on a replace, itself a future lane); a versioned history
   is a larger design that needs its own ADR (the M14/M24 lane shape).
2. **Full-text search within document bodies** — M21 does not index PDF/docx
   text. *Why deferred:* body-text search needs a text-extraction + index lane
   (a new dependency or a new seam); a *title* search may reuse the M8 `/search`
   lane (a future decision).
3. **Document comments / discussion** — rides M3/M3b moderation. *Why
   deferred:* a comments surface is the M3/M3b moderation lane carried to
   documents (a future ADR), not a document-management primitive.
4. **A "new document posted" notification** — rides M6 (ADR 0076). *Why
   deferred:* M21 is intake-and-serve, not notify; a nudge is the M6
   notification lane applied to a new event kind (a future lane, D9·3).
5. **Per-kind document taxonomy** (contracts vs minutes vs notices vs bylaws,
   each with its own standing). *Why deferred:* M21 is **one** document type; a
   per-kind taxonomy with per-kind standing is a larger authorization design
   (a new ADR), and M21's "official" standing is the authority lane, not a kind.
6. **Scheduled expiry / auto-purge** — the M20 §6.4 durable-job shape. *Why
   deferred:* M21 has no `Expiry` field and no purge job; a scheduled-purge
   lane is the ADR 0121 §6.4 durable-job shape applied to documents (a future
   lane).
7. **Edit-by-replacement / author soft-delete** — the M14/M24 lane shape.
   *Partially resolved — see **ADR 0125** (2026-10-03):* the **edit-by-replacement**
   half of this lane now ships (the owner re-chooses who can access it and
   replaces the file; title/summary editable alongside; ownership immutable,
   `Modified` stamped; owner-only gate, non-owner → 404 — the ADR 0122 D7
   posture). The **author soft-delete** half remains **deferred**: M21's write
   lane is **upload-only** (the uploader's own document is visible to them via
   the owner branch); a soft-delete is the M14/M24 edit-lane carried to
   documents (a future ADR).
