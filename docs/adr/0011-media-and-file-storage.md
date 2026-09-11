# ADR 0011 — Media & file storage: content-addressed local-volume bytes + a `mt` catalog

Status: Accepted
Date: 2026-09-11
Amends: 0004 (Consequences Positive — "single `pg_dump` backup story"; the
byte payload is the one surface that does not ride the dump — `OPS.md` gains
the volume as a second backup/restore surface, ADR 0004 §B otherwise unchanged)

## Context

The platform had no way for a resident to attach an image. The first
consumer — the profile avatar (`Profile.AvatarId`, the profile editor, the
directory/preview surfaces) — needs a place for the bytes and a rulebook for
how they are served. The constraints the choice has to honor are the ones
this codebase has already made:

- **Privacy/audit model** (SECURITY.md §3, ADR 0006-C): access to
  audience-restricted content is resolved per request and logged; a public
  path bypasses both.
- **`Core` stays HTTP-free** (ADR 0006-D): the store seam must be a plain
  `byte[]/filename/contentType/actorId` interface, not `IFormFile`/`Stream`.
- **Marten owns the domain documents** (ADR 0004 §B): the catalog entry must
  be a document in `mt`, so it `pg_dump`s with the rest of the data.
- **Lean + Boring, one database** (README principles; ADR 0002): no managed
  bucket service, no SDK, no signed-URL machinery, no extra stateful backend.

Two shapes were on the table. An **object store** (MinIO / S3-style behind
`Coolify`) would outgrow the deployment (a second stateful service, an SDK,
a key-rotation story — a B4/B5-style third-party boundary SECURITY.md §2
charges us with documenting) for a workload that at one-neighborhood scale
tops out at a few hundred small images. A **dedicated local volume** is
weaker on durability by itself — it must be backed up — but it is honest:
one more filesystem surface, no new class of component. The user chose the
local volume.

## Decision

- **Storage primitive: a content-addressed byte store on a dedicated volume,
  behind an HTTP-free seam.** `Kumunita.Core.Media`:
  - `MediaObject` (a Marten doc in `mt` — registered on its own
    `MediaDocTypes` doc surface, ADR 0004 §B.1): `Id` = the **lowercase-hex
    SHA-256 of the payload** (the identity, the dedup key, and the catalog —
    one thing, not three), plus `Filename?` / `ContentType` / `SizeBytes` /
    `Created` / `CreatedById?` (who first stored each unique file — the
    payload's audit).
  - `IMediaStore` (`PutAsync(byte[], filename?, contentType, actorId?)` /
    `GetAsync` / `OpenReadAsync` / `RemoveAsync`) — the only sanctioned
    write/read path to the bytes (C-MED·6). `PutAsync` is **idempotent**:
    load-by-id first, return the existing doc if present, else write the file
    then store the doc (bytes-first, so a crash leaves at worst a referable-
    from-nothing orphan file, never a doc with no file in the normal path).
  - The physical layout is `{Media__RootPath}/{Id[0..2]}/{Id}` — sharded,
    hash-only names, no tenant/subject component (content-addressed identity
    is the boundary; per-resident scoping lives on the *references*, ADR
    C-MED·8).
- **Serving is always through the app endpoint, never a static folder.**
  The reference consumer is `ProfileController.Avatar`
  (`GET /profile/avatar/{subjectId}`): profile load → `Blocked`/unknown →
  `404` **before** the decision, then **one** frozen call to
  `IAuthorizationService.CanAsync(…, AccessAction.Read, …)` (the audit row —
  Allow *and* Deny — is the seam's own behavior), then `IMediaStore` read and
  `File(stream, storedContentType)` with `X-Content-Type-Options: nosniff`.
  **No new `AccessAction` / `AccessVia` id, no new authorization module**
  (C-MED·1); the serving route is the contract follow-on lanes copy.
- **Upload is owner-scoped at the Web boundary, single write lane.**
  `ProfileController.AvatarUpload` (`POST /profile/avatar`) — self-only (the
  subject is the signed-in principal, minted server-side; never a path
  param), guards before any write (empty → `400`, `> Media__MaxBytes` →
  `413`, disallowed type → `415`, no file written), then `PutAsync` →
  `SetProfileAvatarAsync` (the one `IUserInfoService` lane; ADR 0006-E
  compatible addition).
- **Type + size boundaries at the upload edge.** Raster only
  (`image/jpeg|png|webp|gif` — **SVG excluded**: it is a document format
  carrying executable content, i.e. an XSS/XXE vector into a page that also
  carries the resident's session cookie); `MediaOptions.AllowedContentTypes`
  and `Media__MaxBytes` (default 5 MiB) are operator-tunable (OPS.md) and
  are the extension point for follow-on lanes.
- **Two restore surfaces, one story (C-MED·7).** The catalog (`MediaObject`)
  lives in Postgres and restores with the `pg_dump`; the payload restores
  from the **volume snapshot**, which OPS.md adds to backup (procedures 4/5)
  as the second surface. The integrity key is the content hash: a doc with
  no file is *re-hydratable* (an owner re-uploads the same bytes → the same
  id → the same catalog row matches), and an orphan file is *inert* (nothing
  references a bare hash) — so a half-restored state degrades to "missing
  avatar" 404s, never to cross-contamination.
- **Not decided here (explicit non-decisions):**
  - **S3/MinIO/object-store backend** — rejected at this scale (the boundary
    cost above); the `IMediaFileStore` seam is the seam through which it
    could be swapped in as a `LocalVolumeFileStore` substitute if a
    deployment ever needs off-box bytes.
  - **Dedup *enforcement* beyond identity** — `Put` is
    load-return-or-write; there is no content scan, no dedup across
    *different* payloads, no MIME sniffing of the payload's actual bytes
    (the boundary trusts the form `Content-Type` after the allowlist + the
    size cap; serving nosniffs).
  - **In-app upload size enforcement** is the Web boundary's job reading
    `MediaOptions`; Core enforces nothing about the wire.
  - **Follow-on lanes are a separate feature** — group logos / post
    attachments / badge icons each reuse `IMediaStore` + the avatar serving
    route, but are gated on their *owning* resource's audience. This ADR
    does not register their seams, routes, or doc types.

## Consequences

- **Avatar is the reference consumer.** Every follow-on lane copies the
  C-MED·1/2/3/6 idiom (frozen `CanAsync(…Read…)` + audit-by-default +
  app-endpoint serving + HTTP-free seam); the serving route is pinned in the
  design doc `§2.3` and is the contract — drift from it is a drift-guard
  failure, not an implementation choice.
- **`Core` stays HTTP-free.** `IFormFile`/`Stream` never cross the seam; the
  Web layer is responsible for `byte[]` / `filename` / `contentType` /
  `actorId`. ADR 0006-D holds; the `Postman-free` unit test surface is the
  `IMediaFileStore` (temp-dir) + `IMediaStore` (Postgres + temp dir) pair.
- **`mt` catalog + volume payload = two restore surfaces.** OPS.md
  procedures 4 and 5 each gain the volume (snapshot + restore), and the
  integrity key is the content hash — so a *half*-restore (one surface
  missing) degrades to avatar 404s, not data corruption. ADR 0004's
  "one `pg_dump` story" is amended in that one respect (see Amends).
- **The upload boundary is a single chokepoint.** Type/size are decided
  once in `MediaOptions` (operator-tunable via `Media__*`) and enforced in
  `AvatarUpload` before any byte is written; the serving endpoint serves the
  *stored* `ContentType` (already validated at write) + nosniff — no second
  guess at read time.
- **ADR 0006-D / ADR 0004 are not touched** for this lane — the `MediaObject`
  doc and the `MediaDocTypes` surface are the ADR 0004 §B.1 shape (Marten
  registers the POCO; `MediaDocTypes.Configure(opts)` is called once in
  `Program.cs`, the same call-site as `M3DocTypes`).
- **Rejected: per-resident volume partition** (one subfolder per subject)
  — the content hash *is* the partition; scoping the read gate is an
  authorization job (C-MED·1), not an FS job. Rejected: **signed URLs /
  time-limited public links** — the app endpoint + per-request audit is the
  whole story; a signed URL bypasses the audit (C-MED·2).

## Revisit when

- A deployment wants **bytes off-box** (a shared VPS, a storage cost ceiling,
  or a compliance requirement that the payload not sit on the app's disk) —
  `IMediaFileStore` is the seam; a bucket-backed implementation replaces
  `LocalVolumeFileStore` without touching `IMediaStore`, `MediaObject`, or
  the serving route.
- **Video / office docs / arbitrary downloads** ship (a follow-on lane) — the
  `AllowedContentTypes` / `MaxBytes` boundary and the `ContentKind`-by-
  content-type extension point are the seam this ADR is deliberately sized to
  leave open, and the follow-on design doc should name them explicitly.
- The **audit policy on Deny** changes (e.g. a follow-on lane wants to *not*
  audit avatar denies) — C-MED·2 is the invariant; a change is itself a new
  ADR, not a code change.
