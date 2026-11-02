---
id: adr-0122
title: "Document management (a shared, per-access-controlled repository of official documents, served as downloads)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/attachment-inline-preview-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

## Context

The platform already ships **three** upload-and-serve capabilities over the
same content-addressed byte store (ADR [0011](0011-media-and-file-storage.md)): the **profile avatar**
(inline-rendered), **inline content images** (ADR [0025](0025-rich-content-markdown-and-content-images.md)), and **file
attachments** on posts / replies / announcements (ADR [0034](0034-file-attachments-posts-replies-announcements.md) — served as a
**download**). What the platform does *not* have is a **shared repository of
official documents**: a place to keep the neighborhood's contracts, minutes,
notices, and bylaws in one place, each readable **only by the people the
uploader chose**, each **audited when opened**, each **served as a download**
(not inline-rendered — an authority-published file is never rendered in the
browser).

The constraints the choice must honor (all pre-existing, not new):

- **Audit-by-default** (SECURITY.md §3, ADR [0006](0006-module-boundary-contracts.md)-C): every audience-restricted
  read is resolved per request and logged; the floor is **deny** (an empty
  audience denies everyone — ADR [0006](0006-module-boundary-contracts.md)-C1).
- **The `Audience` is the whole access story** (ADR [0006](0006-module-boundary-contracts.md)-D): "feature modules
  never re-derive access" — the document's `Audience` is the *only* access
  carrier (C-M21·1); a component is a *feed organizer*, never a gate
  (`ComponentId = null` on the adapter, C-M21·3).
- **`Core` stays HTTP-free** (ADR [0006](0006-module-boundary-contracts.md)-D): the bytes ride the frozen
  `IMediaStore` `byte[]/filename/contentType/actorId` seam (D3).
- **Marten owns the domain documents** (ADR [0004](0004-data-persistence-and-schema-evolution.md) §B): the `Document` row is a
  document in `mt` on a new additive `DocumentDocTypes` surface (D1).
- **Lean + Boring, one database** (README principles, ADR [0002](0002-deployment-topology.md)): no new byte
  store, no new catalog doc beyond the one `Document`, no new `AccessAction`,
  no new `IMediaStore` method, no new storage lane (D3, D8).

The shape that keeps M21 safe (C-M21·2, C-M21·6): **ride the frozen `Read`
path through one adapter, serve a download behind a decision, audit every
read** — and **add zero new authorization surface** (D8). The upload is a
**standing-gated write with no audit row** (D5, §1.a A2 — the
`PostService.CreatePostAsync` / `AttachmentController.Upload` /
`AnnouncementService` write-lane convention: writes are authenticated, not
audience-restricted reads, and the audit lane is the frozen `IAuthorizationService`
`Read`-decision path's job, not the write lane's).

## Decision

**D1 — A new `Document` document, on its own additive `DocumentDocTypes`
surface (ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1).** A new document `Kumunita.Core.Documents.Document`
(a **new bounded context** alongside `Posts` / `Events` / `Pages`):
`Id` (conventional string, `Guid "N"`), `Title`, `Summary?`, `MediaId` (the
ADR [0011](0011-media-and-file-storage.md) content-addressed blob id), `Filename?`, `ContentType`, `SizeBytes`,
`Audience` (non-null — the **only** access carrier), `OwnerId` (the uploader),
`Created`, `Modified?` (present, set only on a future replace — null in M21).
Registered on a **new parallel additive surface** `DocumentDocTypes.Configure`
(the `BookmarkDocTypes` / `MediaDocTypes` shape — delta-detected, idempotent, no
seeding), wired with one line in `Program.cs` (D1). *Forbids:* a new
relational table, a new `AccessAction`, a per-document `AccessVia`/standing, or
encoding per-document access in an identity claim (C-M21·1/C-M21·2).

**D2 — M21 rides the **frozen** `Read` path — one new adapter + one new
`TargetKind`, zero new `IAuthorizationService` surface.**
`DocumentToAuditableResource : IAuditableResource` (the
`PostToAuditableResource` shape verbatim) exposes `Id`, `Name` (= `Title`),
`OwnerId`, `Audience`, `ComponentId => null` (repository-level, not component-
scoped), and **`TargetKind => "document"`**. `DocumentService` composes **only**
the frozen `IAuthorizationService` + the frozen `IUserInfoService` read seams +
its own `IDocumentStore`, exactly like `PostService`: feed = one `CanSeeAsync`
(aggregate row), detail/download = one `CanAsync` (decision row), all
`AccessAction.Read`. *Forbids:* a new `AccessAction`, a new `Decide()` branch,
a new `IAuthorizationService` method, a new `AccessVia`, or a `DocumentService`
that reads `GroupMembership`/`DelegationGrant` for its own access decisions
(C-M21·2).

**D3 — Bytes ride the **frozen** ADR [0011](0011-media-and-file-storage.md) `IMediaStore`; M21 adds **no** new
storage lane.** `Document.MediaId` is the content-addressed id from
`IMediaStore.PutAsync` (SHA-256, store-first, orphan-safe). Serving =
`IMediaStore.OpenReadAsync` **behind the `Read` decision** (D6), with the
stored `ContentType` + the original `Filename`. `MediaOptions` gains one
**additive** document lane — `DocumentAllowedContentTypes` /
`IsDocumentAllowed` — in the exact shape of the existing
`AttachmentAllowedContentTypes` / `IsAttachmentAllowed` (a PDF/DOCX/XLSX/CSV/
ZIP allowlist; the image lane is *not* the document lane). *Forbids:* a new
storage mechanism, in-DB blob bytes, a public/static serving route, or a serving
path that bypasses the `Read` decision (D6/D7).

**D4 — The repository feed is a **candidate filter over all documents**, never
a gate (C-M21·3).** `DocumentService.ListAsync(actorId, page)` returns **all**
documents whose `Audience` allows the caller (one `CanSeeAsync` over the
candidate set), in `Created` order, and emits **one aggregate** `AccessAudit`
row (`targetKind "document"`, `visibleCount`/`hiddenCount` set,
`targetId = null`) — the `PostService.ListFeedAsync` aggregate shape. A
**component is not a gate** (`ComponentId = null` on the adapter). *Forbids:*
a component gate, a per-author feed gate, an unaudited feed read, or a feed that
lists a document the caller cannot open (feed and detail/download must agree —
C-M21·5).

**D5 — The upload/compose standing is the **authority lane**: `GlobalAdmin ∪
Moderator`.** (Locked per §Amendments A1.) Posting an **official** document is
an **authority act** — the Web boundary gates the compose form to the elevated
standing (`GlobalAdmin ∪ Moderator`), the ADR [0017](0017-announcement-edit-lane-author-or-globaladmin.md) "author-of-record ∪
GlobalAdmin" shape generalized to the community's authority. The Core write
lane `DocumentService.UploadAsync(DocumentUpload, actorId, IDocumentSession)`
is **standing-agnostic** (it records `OwnerId = actorId`; it does **not**
re-check roles — the Web boundary owns the standing check, the ADR [0006](0006-module-boundary-contracts.md)/ADR
0017 convention). One write: the `Document` is stored in the **caller's**
session (ADR [0006](0006-module-boundary-contracts.md)-E same-transaction) alongside the already-`PutAsync`'d blob
(D3). *Forbids:* any-resident upload, an unaudited direct `session.Store` from
a controller, or a Core seam that re-derives the standing (the Web boundary
owns it).

**D6 — A document is a **download, not an inline render**
(`Content-Disposition: attachment`).** The serve route (the
`AttachmentController` / ADR [0034](0034-file-attachments-posts-replies-announcements.md) download shape) streams
`IMediaStore.OpenReadAsync` **behind the `Read` decision**, with the stored
`ContentType` + `Content-Disposition: attachment; filename="<stored-or-{id}.bin>"`
(RFC 6266) + `X-Content-Type-Options: nosniff`. An official document is
*downloaded*, never rendered inline. *Forbids:* inline `<img>`/`<embed>` of an
official document, a public CDN/static route, or a serve route with no per-call
`Read` decision.

**D7 — Deny is **404, not 403** (no existence leak); feed and detail agree
(C-M21·4/C-M21·5).** The serve route is the `ContentImageController` /
`AttachmentController` 5-step: (1) validate the id else **400**; (2) `Document`
miss → **404**; (3) blob (`IMediaStore`) miss → **404** (orphan → the document
is the authority); (4) **one** `CanAsync(Read)` → **Deny → 404 not 403** (the
single `AccessAudit` row is emitted *by the seam*, not the controller); (5)
allow → stream the blob (D6). A document in the **feed** is openable by the
same caller; a document the feed hides 404s on detail (the same `Read` decision
governs both). *Forbids:* a 403 that reveals existence, a feed listing a
document that 404s on detail, or an audit row written by the controller instead
of the seam.

**D8 — **Zero new authorization surface** (the C-M21·2 pin).** M21 adds **no**
`AccessAction`, **no** `Decide()` branch, **no** `AccessVia`, **no**
`IAuthorizationService` method; the claim set (`ClaimTypes.All`) is
**unchanged**. It adds exactly **one** `IAuditableResource` adapter + **one**
`TargetKind` (`"document"`) + **one** new document + **one** `DocumentService`
+ **one** `DocumentController` + **one** additive `MediaOptions` lane.
*Forbids:* any extension of the frozen authorization seams (a §drift-guard stop
— the whole point of M21 is that it *rides* `Read`, it does not extend it).

**D9 — **Deferred lanes** (each a future ADR, not part of M21).** (1)
**Versioning / revision history.** (2) **Full-text search within document
bodies.** (3) **Document comments / discussion.** (4) **A "new document
posted" notification.** (5) **Per-kind document taxonomy.** (6) **Scheduled
expiry / auto-purge.** (7) **Edit-by-replacement / author soft-delete.** Each is
a larger design that needs its own ADR; M21 is the *document + its audience +
its bytes + its download*, **not** a document-management *system*.

## Consequences

**Positive:**

- **A shared, audited, per-access-controlled repository for official documents,
  served as downloads** (the README M21 line, end-to-end): an authority
  (`GlobalAdmin ∪ Moderator`) uploads a document with a chosen `Audience`
  (D5, F3), a resident sees the documents their audience allows (D4, F1, one
  aggregate audit row — GATE-2), a document is opened as a download behind its
  `Read` decision (D6, D7, F2, one decision row — GATE-3), and an empty-
  audience document is visible to no one but its uploader (F4, ADR [0006](0006-module-boundary-contracts.md)-C1 —
  GATE-1). No new authorization surface (C-M21·2, GATE-4).
- **Zero new authorization surface — the strongest form** (C-M21·2, D8): M21
  adds **no** new `AccessAction`, **no** new `AccessVia`, **no** branch in
  `Decide()`, **no** new `IAuthorizationService` method, **no** new claim
  *type*. The read verdict is the **frozen** `IAuthorizationService` `Read`
  decision path (D2) — the only new seam is **one** `IAuditableResource`
  adapter (`DocumentToAuditableResource`, `TargetKind = "document"`). The
  claim set (`ClaimTypes.All`) is **byte-identical** after M21 (the GATE-4 pin).
- **The bytes ride ADR [0011](0011-media-and-file-storage.md) — zero new storage lane** (C-M21·6, D3):
  `Document.MediaId` = the `IMediaStore` id; serving =
  `IMediaStore.OpenReadAsync` **behind the decision** with
  `Content-Disposition: attachment`; an **orphan** blob (no `Document` row) is a
  404 (the document is the authority for access — GATE-5).
- **Zero migrations** (D1): the new `Document` document on the additive
  `DocumentDocTypes` surface is delta-detected and applied idempotently at boot
  (the ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1 additive-surface shape) — every existing row is unchanged.

**Neutral / cost:**

- **The named trade — a *shared, per-access-controlled, download-served*
  repository for zero new authorization surface:** M21 buys *a shared, audited,
  per-access-controlled repository for official documents, served as
  downloads* with **zero new authorization surface + one new document + one new
  adapter + one new service + one new controller + one additive `MediaOptions`
  lane + a closed `kw-l` set**, in exchange for **no versioning, no full-text,
  no comments, no taxonomy, no auto-expiry, no edit-by-replacement** (D9): M21
  is *the document + its audience + its bytes + its download*, **not** a
  document-management *system*. The deferred lanes (D9·1…D9·7) are deliberately
  *not* in M21: they are larger designs that need their own ADRs.
- **The upload is a standing-gated write with no audit row** (D5, §Amendments
  A2, C-M21·7): the write is authenticated (the Web boundary checks
  `GlobalAdmin ∪ Moderator`), not an audience-restricted read — the
  `PostService.CreatePostAsync` / `AttachmentController.Upload` /
  `AnnouncementService` write-lane convention. The **reads** are audited (feed
  = one aggregate row, C-M21·3; detail/download = one decision row, C-M21·4);
  the **write** is not (GATE-6, amended).
- **A document is a download, never inline** (D6, C-M21·6): an official
  document is served with `Content-Disposition: attachment` +
  `X-Content-Type-Options: nosniff` — never an `<img>`/`<embed>`/inline blob.
  This is a deliberate security posture (never render an authority-published
  file inline in a session-carrying page).

**Follow-on lanes (each its own ADR — the design doc's §deferred):**
versioning / revision history (D9·1); full-text search within document bodies
(D9·2); document comments / discussion (D9·3); a "new document posted"
notification (D9·4); per-kind document taxonomy (D9·5); scheduled expiry /
auto-purge (D9·6); edit-by-replacement / author soft-delete (D9·7).
