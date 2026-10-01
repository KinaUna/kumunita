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
