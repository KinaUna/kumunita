# ATT U1 — Design doc Part 1: Context / Scope / Invariants / FACES

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/plan-file-attachments.md`) is
> the cross-reference; when the two disagree, **this file wins for what to do**
> and the register wins for *which* files exist. **No code, no build** in this
> unit — you are authoring documentation only.

## Understanding

You are starting the **file-attachments lane**: users attach files (PDF/Office/
text/zip + raster) to **posts, announcements, and replies**. Files are
**downloads** (render as an `<a>` link, served with `Content-Disposition:
attachment`), deliberately a **separate lane** from the existing inline
**content-image** lane (ADR 0025): a different route (`/attachment/{id}` vs
`/content-image/{id}`), a different allowlist, and a different serve
semantics (download vs inline render) — while **reusing** the same
content-addressed byte store (ADR 0011). This lane **Amends** ADR 0025 (which
deferred "arbitrary downloads" as a follow-on) and **Amends** ADR 0011 (which
reserved the allowlist as the extension point for exactly this).

This unit authors **Part 1** of the design doc: the *what/why* half — the
three-tier header, Context, Scope (in/out), the 10 invariants (C-ATT·1–10), and
the 9 FACES (F1–F9). The exact C# seams (Part 2) are **U2's** job — do **not**
write §2.* sections in this unit.

## The 10 invariants (write these verbatim into the design doc)

- **C-ATT·1 — One store, two lanes.** The `IMediaStore` byte store (ADR 0011)
  is the single content-addressed volume for BOTH images and attachments. No
  second store, no second volume, no second catalog. The lanes differ only in
  route, allowlist, and serve semantics.
- **C-ATT·2 — Attachments are downloads, not renders.** An attachment in a
  body is `[label](/attachment/{id})` — an **`<a>` link**, never an `<img>`.
  The serve route sets `Content-Disposition: attachment`. The image lane keeps
  its `<img>` inline render. The two never bleed into each other.
- **C-ATT·3 — `IMediaStore` is unchanged.** `PutAsync` / `GetAsync` /
  `OpenReadAsync` / `RemoveAsync` / the `MediaObject` catalog are all
  **untouched**. Attachments ride the exact same seam. No new store method.
- **C-ATT·4 — Core never parses bodies.** `PostService` / `AnnouncementService`
  do **not** scan body Markdown for `/attachment/` links (Core stays
  body-parse-free, ADR 0006-D). The Web layer extracts attachment ids from the
  body and passes them in the draft; Core writes them verbatim, exactly the
  `ImageIds` idiom.
- **C-ATT·5 — `AttachmentIds` is its own field, not `ImageIds`.** Each owning
  POCO (`Post`, `PostReply`, `Announcement`) gains a **separate**
  `AttachmentIds` additive POCO field (ADR 0004 §B.1 — zero migrations). A
  body can carry both; a post's images live in `ImageIds`, its files in
  `AttachmentIds`. Never merge them.
- **C-ATT·6 — The allowlist is the only content gate.** Upload accepts a file
  iff its `ContentType` is in `MediaOptions.AttachmentAllowedContentTypes`
  (resolvable, default pinned in U2). Empty → 400, oversize → 413, disallowed
  → 415, **guards before any `PutAsync` write** (the ADR 0011 upload idiom).
  SVG stays **excluded**.
- **C-ATT·7 — Serve order is fixed and 404-before-decision.** `GET
  /attachment/{id}`: validate id shape → store miss = 404 → reverse-lookup
  owner (post → reply → announcement) → **audience decision** (the one
  `CanAsync(…Read…)`) → serve. Any miss/deny is a **404** (no 403 — no
  existence leak). UGC deny emits exactly **one** audit `Deny` row; every other
  404 emits **zero** audit rows.
- **C-ATT·8 — Reply serve resolves the parent.** Unlike the image lane's
  deliberate "reply 404 drift pause," the attachment lane **resolves the
  reply's parent post** for its audience decision. The parent is the unit of
  authorization; a member sees a member's reply-attachment, a non-member
  404s.
- **C-ATT·9 — The image lane is untouched.** Every existing `ImageIds` field,
  the `/content-image/` route, `IsSafeImageSrc`, the Image editor button, and
  `MarkdownRenderer`'s image handling are **unchanged**. This lane only
  **adds**. (`MarkdownRenderer.IsSafeUrl` already accepts the relative
  `/attachment/{id}` link shape — **zero renderer changes**.)
- **C-ATT·10 — Audit-by-default stands.** Access to an audience-restricted
  attachment (a post/reply a non-member must not see) is logged exactly as the
  image lane logs it. No new audit *kind*; reuse the existing Deny path.

## The 9 FACES (write these verbatim into the design doc)

- **F1 — Audience member downloads a post/reply attachment.** Serve 200,
  correct bytes, `Content-Disposition: attachment`, correct
  `Content-Type`. Pinned by: U11 `AttachServe_F1_AudienceMemberDownloads`.
- **F2 — Non-member requests a private post's attachment.** 404, one `Deny`
  audit row, **no** existence leak. Pinned by: U11 `AttachServe_F2_NonMember404`.
- **F3 — Orphan id (no owner anywhere).** 404, zero audit rows. Pinned by:
  U11 `AttachServe_F3_Orphan404`.
- **F4 — Reply attachment whose parent post denies the requester.** 404, one
  `Deny` row. Pinned by: U11 `AttachServe_F4_ReplyParentDeny404`.
- **F5 — Announcement attachment (always public scope gate passes).** Serve
  200 to any authenticated actor in scope. Pinned by: U11
  `AttachServe_F5_AnnouncementPublicServes`.
- **F6 — Upload guards.** Empty → 400, oversize → 413, disallowed type → 415;
  no write occurs on any guard failure. Pinned by: U11 `AttachUpload_F6_Empty400`,
  `AttachUpload_F6_Oversize413`, `AttachUpload_F6_WrongType415`.
- **F7 — A body with a **remote** attachment URL is not a download link.**
  Only the exact `/attachment/{id}` route shape is treated as an attachment;
  everything else renders as plain escaped text (no scheme, no redirect).
  Pinned by: U11 `AttachLink_F7_RemoteUrlRendersText`.
- **F8 — An attachment id that is not a valid media id (bad hex/length) 404s**
  without a store round-trip. Pinned by: U11 (or U9) serve tests — record the
  exact test name in Part 2 (U2) if a new one is added; otherwise this folds
  into F3.
- **F9 — Round-trip: `[label](/attachment/{id})` in the body survives
  editor → textarea → server parse → re-render as a working `<a href>`.**
  Pinned by: U11 `AttachRoundtrip_F9_HrefPreserved` (+ U10 editor test).

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/plans-milestones/plan-file-attachments.md` — **this lane's register**:
   the 10 invariants (above, verbatim source), the 9 FACES (above), the 12-unit
   register, and the handoff protocol. Read it top-to-bottom once.
2. `docs/adr/0011-media-and-file-storage.md` — the store contract you are
   **reusing** (C-ATT·1/3/6/10 all rest on it). Note the "Not decided here"
   follow-on for attachments.
3. `docs/adr/0025-rich-content-markdown-and-content-images.md` — the
   content-image lane you are **mirroring** and **Amending** (this is the
   template for how a body-referenced media lane reads: the serve 5-step, the
   `ImageIds` idiom, the deferred "arbitrary downloads" line this lane
   resolves).
4. `docs/design/rich-content-design.md` — the **design-doc house style** to
   emulate: the three-tier header note, `## Context`, `## Scope` (In / Out
   named deferrals), the numbered-invariants table with a "Pinned where"
   column, the FACES table with a "Pinned by" column.
5. `docs/adr/0005-multilingual-support.md` + `docs/design/multilingual-design.md`
   — a second reference for the **Amends** header convention and the
   invariants/FACES table shape. (Skim for shape, not content.)
6. `docs/philosophy/templates/design-doc.md` — the design-doc template
   (section order, tone).

## Deliverables (1 file, new, Part 1 only)

### `docs/design/file-attachments-design.md` — **Part 1** (~120–160 lines)

In this order:

- **Header** — the same three-tier contract note the rich-content / multilingual
  design docs carry: *this file = primary (what + why); the register
  `docs/plans-milestones/plan-file-attachments.md` = secondary (which unit,
  which files, order); `docs/plans-milestones/file-attachments-handoff-notes.md`
  = scratch (append-only log).* Add an **Amends** line: "Amends ADR 0025
  (resolves its deferred 'arbitrary downloads' follow-on for post / reply /
  announcement) and ADR 0011 (uses its reserved allowlist extension point)."
- `## Context` — the plain-text status quo: the store + catalog exist (ADR
  0011); the image lane is live (ADR 0025: `![alt](/content-image/{id})`,
  `/content-image/{id}` serve, `ImageIds` on Post/PostReply/Announcement, Image
  editor button); **files cannot be attached anywhere yet** — the "Attach file"
  affordance does not exist and the ADR 0025 deferred-lane is the exact open
  question this design closes.
- `## Scope` — **In:** the three `AttachmentIds` ADDs; the
  `Find*ByAttachmentIdAsync` reverse-lookup seams; the `POST /attachment`
  upload lane; the `GET /attachment/{id}` serve route (with reply parent
  resolution); the `AttachmentAllowedContentTypes` option; the `AttachmentIds`
  Web parse helper; the four call-site wirings (post create, reply create/edit,
  announcement create/edit); the `attachLink` editor fn + "Attach file" button
  (incl. reply composers); the `Content-Disposition: attachment` serve header.
  **Out (named deferrals, not a renumber):** video/audio streaming (files are
  download-only, never streamed/previewed), any file **preview** in-browser,
  drag-and-drop multi-upload UI, file **transformations** (thumbnail/generation),
  attachments on `LocalizedPage` (static pages) in this pass, any change to the
  image lane or the renderer's image handling.
- `## Invariants (pinned for the ATT lane)` — **C-ATT·1–10**, exactly the ten
  above, each with a one-line "Pinned where" column pointing at the unit that
  enforces it (U1 doc / U3 fields / U4–U5 write lanes / U6 Core tests / U7 parse
  + wiring / U8 option + upload / U9 serve + reply-parent / U10 editor / U11 Web
  tests / U12 ADR+docs).
- `## FACES (pinned for the ATT lane)` — **F1–F9**, exactly the nine above,
  each with a "Pinned by" column naming the exact test id (the U11/U10 test
  names are already pinned in the register — copy them).
- `## Assumptions` — copy the register's assumptions section (single community,
  self-hosted volume, `IMediaStore` unchanged, `AttachmentIds` separate field,
  download-only, no renderer change, Core stays HTTP- and body-parse-free).

**Stop here.** Do **not** write `## 2.` / exact-C# / serve-5-step / test-list
sections — those are **U2 (Part 2)**. Do **not** write or build any code.

## Risks & open questions

- **Don't write the C# in this unit.** If you find yourself writing method
  signatures, route handlers, or `MediaOptions` members — stop, that's U2.
  Part 1 is prose + tables only.
- **Don't invent an 11th invariant or a 10th FACE.** The register pins
  exactly 10 + 9. If you think one is missing, note it in the handoff section
  as an open question, don't add it to the table.
- **The FACES "Pinned by" column must use the exact test ids from the
  register** (they are already named: `AttachServe_F1_…` … `AttachRoundtrip_F9_HrefPreserved`).
  If a test name isn't in the register, mark the cell "_(test to be named in
  U2 Part 2)_" rather than inventing one here.

## Steps

1. Read the six entry reads above (register first).
2. Write `docs/design/file-attachments-design.md` **Part 1** in the exact
   section order above (header / Context / Scope / Invariants table / FACES
   table / Assumptions). No `## 2.` sections.
3. Re-read the file top-to-bottom: confirm the 10 invariants and 9 FACES are
   present **exactly** (count them), the Amends line names ADR 0025 + 0011, and
   no C# code has leaked in.
4. Append a `## U1` section to
   `docs/plans-milestones/file-attachments-handoff-notes.md` recording:
   "Built `docs/design/file-attachments-design.md` Part 1 (header + Context +
   Scope + C-ATT·1–10 + F1–F9 + Assumptions). No code, no build (doc-only
   unit). Drift: <none / describe>. Next agent (U2) authors Part 2 — the exact
   C# seams, serve 5-step, pinned test names, gate, drift-guard — into the
   **same** file, immediately after Part 1."
5. Done.
