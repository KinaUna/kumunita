# Design Doc — File & media storage (content-addressed local-volume store + profile-avatar reference lane)

> **Part 1 of 2 (authored by the plan author).** Part 1 pins **scope** (the
> content-addressed media store + the **profile-avatar reference lane** that
> exercises it end-to-end; group logos / post attachments / badge icons are
> *follow-on lanes*, out of scope), the **invariants** (`C-MED·1..8` + which
> ADR 0004 / 0006 / SECURITY.md §3 / OPS clause this feature must not break),
> and the **serving-lane contract**. Part 2 pins the **exact C#** U1–U10
> implement against (`MediaObject`, `MediaOptions`, `IMediaFileStore`,
> `LocalVolumeFileStore`, `IMediaStore`, `LocalVolumeMediaStore`,
> `MediaDocTypes`, `Profile.AvatarId`, `IUserInfoService.SetProfileAvatarAsync`,
> the two `ProfileController` avatar actions), the **pinned seam-test names**
> (U3 / U5 / U9), the **acceptance gate** (build + tests via the `dotnet exec`
> path AGENTS.md prescribes), and the **drift-guard** — mirroring M3b Part 2
> (`docs/design/m3b-moderation.md` § `## Seams & contracts (Part 2)`). This doc
> is the **primary** reference tier: a later unit codes against it, never
> re-derives it — the doc wins on any mismatch (drift-guard).

## Context

M1–M3b shipped identity, directory, groups, posts, and moderation — but the
platform has **no way for a resident to attach an image or file**. The ask is a
file storage system for profiles, groups, icons/badges, and content attachments.
The lean, boring, privacy-first shape for a single-neighborhood, self-hosted
deployment is a **content-addressed byte store on a dedicated local volume**
(the user's chosen backend), with the *metadata* staying a Marten document in
`mt` so the reference/catalog survives a `pg_dump` restore and the byte payload
is the one thing that needs a **second restore surface** (the volume snapshot).
This design lands that primitive plus **one reference consumer** — the profile
avatar — that exercises the serving-lane contract end-to-end. Follow-on consumers
(group logos, post attachments, badge icons) **reuse the same seam** and are
explicitly deferred, not stubbed.

The backend choice is deliberate against the README principles (**Lean**,
**Boring**, **one database**): local volume is the leanest *serving* path (no S3
SDK, no MinIO, no signed-URL machinery) and, being raster-only + content-addressed
+ audit-gated, it stays within the privacy/audit model. The cost is honest and
nameable: the byte payload is one filesystem surface outside Postgres, so
`OPSDOC` gains a volume snapshot/restore step (C-MED·7).

## Scope

**In-scope (this feature, `Kumunita.Core.Media` module + the profile-avatar lane):**
1. **The store primitive.** `Kumunita.Core.Media`: a content-addressed byte
   store on a local volume — dedup by SHA-256, atomic writes, behind an
   `IMediaStore` seam testable without a live volume. The byte payload is the
   only thing not in Postgres; the `MediaObject` (its catalog reference) is a
   Marten doc in `mt`.
2. **The profile-avatar reference consumer** (end-to-end): `Profile.AvatarId`
   (UserInfo context), `IUserInfoService.SetProfileAvatarAsync` (single write
   lane), the **owner-scoped upload** action, and the **audited serving**
   endpoint on `ProfileController`. The serving endpoint is *the contract*
   follow-on lanes copy (C-MED·1/2/3).
3. **The governance artifacts** (authored in the close unit, U10): **ADR 0011**
   + `docs/adr/README.md` row, and the doc sync (ARCHITECTURE / SECURITY /
   OPS / README).

**Out-of-scope (follow-on lanes that *reuse* `IMediaStore` + the serving-lane
contract, each gated on its *owning* resource's audience):**
- **Group logos** — `Group` reference (+ `IUserInfoService` lane) + a **group**
  serving endpoint gated on the group's privacy.
- **Post / reply attachments** — `Post`/`PostReply` reference + a **post**
  serving endpoint gated on the post's audience.
- **Badge / icon catalog** — a `Badge` doc + a standing-community serving
  endpoint (badges are community-facing, a distinct audience, not a
  `Profile`-scoped gate).
- **Video / office docs / arbitrary downloads.** Raster images only in this
  increment; the `ContentKind`-by-allowlist seam (`MediaOptions.AllowedContentTypes`)
  is deliberately the extension point.
- **Multi-tenant / cross-community media** — no such data model exists (single
  community per instance per ADR 0002).

## Invariants

- **C-MED·1 — single decision path (ADR 0006-D).** Serving media calls **only**
  the frozen `IAuthorizationService.CanAsync(..., AccessAction.Read, resource)`,
  the same path `DirectoryService.PreviewAsAsync` + `ProfileController.Preview`
  already use. **No** new authorization module, and **no** new `AccessAction` /
  `AccessVia` id (U10's ADR must not add one either).
- **C-MED·2 — audit-by-default (SECURITY.md §3 / ADR 0006-C).** Every media
  **serving** request commits an `AccessAudit` row (Allow *and* Deny) — it
  goes through `CanAsync`, so the audit is the path's existing behavior. There
  is no unaudited serve and no Deny that skips the audit row.
- **C-MED·3 — app-endpoint serving, never a public folder.** Media is served
  **only** through the app endpoint; the volume is never mounted as a
  static/public path. Authorization + audit run per request (contrast the
  `MapStaticAssets()` public path, which is style-only and unauthenticated).
- **C-MED·4 — content-addressed, immutable bytes.** The payload is keyed by
  SHA-256 (`MediaObject.Id`): identical bytes → one volume file → **one**
  `MediaObject`. `PutAsync` is idempotent/deduping; serving never writes.
  The volume is the only add/delete surface.
- **C-MED·5 — lean types, SECURITY.md data class (e).** Raster images only
  (`jpeg|png|webp|gif`); **SVG is excluded** (XSS/XXE via `<script>`/`<foreignObject>`).
  Type + size are enforced at the **upload boundary** (Web) against
  `MediaOptions`; the serving endpoint serves the *stored* (already-validated)
  `ContentType`.
- **C-MED·6 — Core stays HTTP-free (ADR 0006-D).** `Kumunita.Core` references
  no ASP.NET/HTTP types. The store seam takes `byte[]` + `filename` +
  `contentType` + `actorId`, **not** `IFormFile` / `Stream` (the Web layer
  reads the upload into bytes and passes them in). Testable without a live
  volume (the file-store seam is injectable against a temp dir).
- **C-MED·7 — two restore surfaces, one story (OPS.md).** The doc (metadata)
  restores with `pg_dump`; the byte payload restores from the volume snapshot.
  OPS pins that a **volume snapshot without the matching Postgres snapshot
  (or vice versa) is an inconsistent restore** — the `MediaObject.Id`
  (content hash) is the integrity key: a doc with no file is *re-hydratable*
  (an owner re-uploads, or an admin re-imports); an orphan file (file with no
  doc) is *self-cleaning* because nothing references a bare hash.
- **C-MED·8 — owner-scoped write, single lane.** The *reference write*
  (the profile avatar) is owner-scoped at the **Web boundary** (`subject ==
  SubjectId(user)` self-only — never a foreign subject), then funneled through
  the single write lane `SetProfileAvatarAsync` (UserInfo context). The
  *media write* (`PutAsync`) carries the actor so the `MediaObject.CreatedById`
  records who first stored each unique file (audit of the payload).

## Observable outcomes (FACES) — pinned count: 6 serving + 3 upload guards

**Serving** (`GET /profile/avatar/{subjectId}`):
- **M1 — owner self-serve:** the profile owner loads their own avatar →
  `200`, correct `Content-Type`, `AccessAudit(Allow)` row (owner branch,
  C-MED·1) — no special-case self check needed when the actor is the owner.
- **M2 — authorized other:** a viewer the `Profile.Visibility` permits loads
  another's avatar → `200` + correct `Content-Type` + `AccessAudit(Allow)`.
- **M3 — denied other:** a viewer `Visibility` does *not* permit loads
  another's avatar → **`404`** (not `200`/`206`, fail-closed, no peeking) +
  `AccessAudit(Deny)` row (C-MED·2).
- **M4 — blocked profile:** the avatar of a `Blocked` profile → `404` for all
  (blocked supersedes audience).
- **M5 — unknown profile:** `GET /profile/avatar/{not-exists}` → `404`.
- **M6 — unsigned:** a non-signed-in viewer of an avatar → the auth challenge
  (redirect to the /account/login surface), never `200`.

**Upload guards** (`POST /profile/avatar`):
- **U7a — non-owner path:** an upload attempt that targets a subject other than
  the current user → `403` (defensive; the self-only form does not even render
  for other subjects).
- **U7b — oversize:** a valid-type file larger than `MediaOptions.MaxBytes` →
  `413`, no volume file written.
- **U7c — wrong type:** a non-raster / SVG / empty file → `415` (type) /
  `400` (empty), no volume file written.

## Seams & contracts (Part 2)

> **Pinned for U1–U9. A unit matches these verbatim; on any mismatch the doc
> wins — fix the code in the same commit + append a note to
> `docs/plans-milestones/in-progress/media-file-storage-handoff-notes.md`.**

### §2.1 Seam surface (added / reused)

| Seam | Kind | Owner unit | Note |
|------|------|-----------|------|
| `IMediaFileStore` + `LocalVolumeFileStore` | new Core, byte I/O | U1 | swappable; temp-dir testable (C-MED·6) |
| `IMediaStore` + `LocalVolumeMediaStore` | new Core, module seam | U2 | composes `IMediaFileStore` + host `IDocumentStore` |
| `MediaObject` (doc), `MediaDocTypes` | new Core, `mt` doc | U2 | POCO, `Id` = content hash (C-MED·4) |
| `MediaOptions` | new Core, options | U1 | `Media__*` section; raster allowlist (C-MED·5) |
| `Profile.AvatarId` | new Core, **additive** on `UserInfo.Profile` | U4 | nullable `string?` (M2/MED) |
| `IUserInfoService.SetProfileAvatarAsync` | new Core, **single write lane** | U4 | owner scope is enforced in Web (C-MED·8) |
| `ProfileController.AvatarUpload` (`POST`) | new Web action | U6 | owner-scoped self-only; delegates to `IMediaStore` + `SetProfileAvatarAsync` |
| `ProfileController.Avatar` (`GET`) | new Web action | U7 | **the serving-lane contract** (C-MED·1/2/3) |
| `IAuthorizationService.CanAsync(...Read...)` | **reused frozen seam** | U7 | no new id (C-MED·1) |

**No new seam on `IUserInfoService` / `IAuthorizationService`** beyond
`SetProfileAvatarAsync` (drift-guard rule 4). The serving lane reuses the
existing `Profile.ToAuditableResource()` shape — no new resource type.

### §2.2 New Core types (exact shapes)

`src/Kumunita.Core/Media/MediaObject.cs`:
```csharp
namespace Kumunita.Core.Media;

/// <summary>
/// A content-addressed media object (ADR 0011; C-MED·4). `Id` is the
/// lowercase-hex SHA-256 of the payload — the dedup key and the document
/// identity. The bytes live on the local volume at
/// `{root}/{Id[0..2]}/{Id}` (C-MED·3/7); this document is the surviving
/// catalog reference that a `pg_dump` restore always carries.
/// </summary>
public sealed class MediaObject
{
    public string Id { get; set; } = "";                    // = lowercase-hex SHA-256 of payload
    public string? Filename { get; set; }                   // original name, metadata ONLY — C-MED·3: never a path component
    public string ContentType { get; set; } = "";           // e.g. "image/png" (validated at the upload boundary, C-MED·5)
    public long SizeBytes { get; set; }
    public DateTimeOffset Created { get; set; }
    public string? CreatedById { get; set; }                // subjectId of the actor who first stored this unique file
}
```

`src/Kumunita.Core/Media/MediaOptions.cs` (mirrors `CommunityOptions`):
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Kumunita.Core.Media;

/// <summary>
/// Per-instance media store config (ADR 0011; OPS binds `Media__*`).
/// `RootPath` is the dedicated volume mount in production; the dev default
/// is `{baseDir}/media`. Raster-only allowlist (C-MED·5); SVG excluded.
/// </summary>
public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public string RootPath { get; set; } =
        System.IO.Path.Combine(AppContext.BaseDirectory, "media");

    /// <summary>Max payload bytes. 0 = unset (Web enforces the same constant).</summary>
    public long MaxBytes { get; set; } = 5L * 1024 * 1024; // 5 MiB

    /// <summary>Comma-separated allowed Content-Types (case-insensitive).</summary>
    public string? AllowedContentTypes { get; set; }

    public IEnumerable<string> ResolvedAllowedTypes =>
        (AllowedContentTypes ?? "image/jpeg,image/png,image/webp,image/gif")
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);

    public bool IsAllowed(string? contentType) =>
        !System.String.IsNullOrWhiteSpace(contentType)
        && ResolvedAllowedTypes.Any(t =>
            System.String.Equals(t, contentType.Trim(), System.StringComparison.OrdinalIgnoreCase));
}
```

`src/Kumunita.Core/Media/IMediaFileStore.cs`:
```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Kumunita.Core.Media;

/// <summary>
/// Byte I/O on the volume — swappable seam (C-MED·6); testable against a
/// temp dir. Paths are derived from the content id only (never a filename).
/// </summary>
public interface IMediaFileStore
{
    /// <summary>Atomically write the payload for this content id (C-MED·4); idempotent (dedup by id).</summary>
    Task PutAsync(string contentId, byte[] content, CancellationToken ct = default);

    Task<bool> ExistsAsync(string contentId, CancellationToken ct = default);

    /// <summary>Open the payload read-only (does not copy to memory).</summary>
    Task<Stream> OpenReadAsync(string contentId, CancellationToken ct = default);

    /// <summary>Delete the payload file (not the document). Fails closed if the file is missing.</summary>
    Task DeleteFileAsync(string contentId, CancellationToken ct = default);

    string RootPath { get; }
}
```

`src/Kumunita.Core/Media/LocalVolumeFileStore.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Media;

/// <summary>Local-volume byte store. Path = `{RootPath}/{id[0..2]}/{id}` (hex-only id).</summary>
public sealed class LocalVolumeFileStore : IMediaFileStore
{
    private readonly MediaOptions _opts;

    public LocalVolumeFileStore(IOptions<MediaOptions> options)
    {
        _opts = options.Value;
        RootPath = _opts.RootPath;
        Directory.CreateDirectory(RootPath); // idempotent
    }

    public string RootPath { get; }

    private string PathFor(string contentId)
    {
        var safe = string.Concat(contentId.Where(c => (c >= 'a' && c <= 'f') || (c >= '0' && c <= '9')));
        var dir = safe.Length >= 2 ? safe[..2] : "_";
        return Path.Combine(RootPath, dir, safe);
    }

    public Task<bool> ExistsAsync(string contentId, CancellationToken ct = default) =>
        Task.FromResult(File.Exists(PathFor(contentId)));

    public Task<Stream> OpenReadAsync(string contentId, CancellationToken ct = default) =>
        Task.FromResult<Stream>(new FileStream(PathFor(contentId), FileMode.Open, FileAccess.Read, FileShare.Read));

    public Task DeleteFileAsync(string contentId, CancellationToken ct = default)
    {
        var p = PathFor(contentId);
        if (!File.Exists(p)) throw new FileNotFoundException("media file not found (C-MED·7 orphan-safe: nothing references a bare hash)", p);
        File.Delete(p);
        return Task.CompletedTask;
    }

    public Task PutAsync(string contentId, byte[] content, CancellationToken ct = default)
    {
        var final = PathFor(contentId);
        Directory.CreateDirectory(Path.GetDirectoryName(final)!);
        var tmp = final + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(content, 0, content.Length);
            fs.Flush();
            fs.Flush(true); // fsync — durability before rename (C-MED·4)
        }
        File.Move(tmp, final, overwrite: true); // atomic replace on both Win/POSIX
        return Task.CompletedTask;
    }
}
```

`src/Kumunita.Core/Media/IMediaStore.cs`:
```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Kumunita.Core.Media;

/// <summary>The media module seam (C-MED·6 HTTP-free): content-addressed byte store + catalog doc.</summary>
public interface IMediaStore
{
    /// <summary>Store (dedup by content hash) + catalog doc; returns the stored MediaObject. Idempotent (C-MED·4).</summary>
    Task<MediaObject> PutAsync(byte[] content, string? filename, string contentType, string? actorId, CancellationToken ct = default);

    Task<MediaObject?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Open the payload for serving. Throws KeyNotFoundException if the doc is missing.</summary>
    Task<Stream> OpenReadAsync(string id, CancellationToken ct = default);

    /// <summary>Delete the payload + doc (orphan-safe; C-MED·7).</summary>
    Task RemoveAsync(string id, string actorId, CancellationToken ct = default);
}
```

`src/Kumunita.Core/Media/LocalVolumeMediaStore.cs`:
```csharp
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Marten;

namespace Kumunita.Core.Media;

/// <summary>Composes the byte seam (`IMediaFileStore`) with the host `IDocumentStore` (C-MED·6: store-first, doc-second, orphan-safe, C-MED·7).</summary>
public sealed class LocalVolumeMediaStore : IMediaStore
{
    private readonly IMediaFileStore _files;
    private readonly IDocumentStore _store;

    public LocalVolumeMediaStore(IMediaFileStore files, IDocumentStore store)
    {
        _files = files;
        _store = store;
    }

    public async Task<MediaObject> PutAsync(byte[] content, string? filename, string contentType, string? actorId, CancellationToken ct = default)
    {
        var id = Sha256Hex(content);
        using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<MediaObject>(id, ct).ConfigureAwait(false);
        if (existing is not null)
            return existing; // dedup: identical bytes → same id → no second volume file (C-MED·4)

        var doc = new MediaObject
        {
            Id = id,
            Filename = filename,
            ContentType = contentType,
            SizeBytes = content.Length,
            Created = DateTimeOffset.Now,
            CreatedById = actorId
        };
        await _files.PutAsync(id, content, ct).ConfigureAwait(false); // bytes first (orphan-safe, C-MED·7)
        session.Store(doc);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return doc;
    }

    public Task<MediaObject?> GetAsync(string id, CancellationToken ct = default)
    {
        using var session = _store.LightweightSession();
        return session.LoadAsync<MediaObject>(id, ct);
    }

    public async Task<Stream> OpenReadAsync(string id, CancellationToken ct = default)
    {
        if (await GetAsync(id, ct).ConfigureAwait(false) is null)
            throw new KeyNotFoundException($"media object missing: {id} (C-MED·7)");
        return await _files.OpenReadAsync(id, ct).ConfigureAwait(false);
    }

    public async Task RemoveAsync(string id, string actorId, CancellationToken ct = default)
    {
        using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<MediaObject>(id, ct).ConfigureAwait(false);
        if (doc is not null)
        {
            session.Delete(doc);
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        if (await _files.ExistsAsync(id, ct).ConfigureAwait(false))
            await _files.DeleteFileAsync(id, ct).ConfigureAwait(false); // orphan file: self-cleaning (C-MED·7)
    }

    private static string Sha256Hex(byte[] b) =>
        Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
}
```

`src/Kumunita.Core/MediaDocTypes.cs` (parallel surface to `M1DocTypes` / `M3DocTypes`):
```csharp
using Kumunita.Core.Media;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// Media module's Marten-native document surface (ADR 0004 §B.1). `MediaObject`
/// is a POCO with the conventional `string` <c>Id</c> identity — its `Id` *is*
/// the content hash (C-MED·4), so Marten's default Id-unique mapping is the
/// dedup; no business-key index required (M3's "string Id" convention).
/// </summary>
public static class MediaDocTypes
{
    public static void Configure(StoreOptions opts) =>
        opts.Schema.For<MediaObject>();
}
```

`src/Kumunita.Core/UserInfo/Profile.cs` — **additive** (C-MED·8):
```csharp
    /// <summary>
    /// The profile's avatar media object id (ADR 0011; C-MED·8) →
    /// <c>MediaObject.Id</c> (a content hash). Nullable: no avatar set. This is
    /// an *additive* field (ADR 0004 §B.1), like M3's `Post.Status`.
    /// </summary>
    public string? AvatarId { get; set; }
```

`src/Kumunita.Core/UserInfo/IUserInfoService.cs` — **single write lane** (C-MED·8):
```csharp
    /// <summary>
    /// Point a profile's avatar at a media object (or clear it when `avatarId`
    /// is null; C-MED·8). The owner-scope check happens at the Web boundary;
    /// this lane writes `Profile.AvatarId` only.
    /// </summary>
    Task SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy);
```
Impl (`UserInfoService`): load `Profile` by `subjectId` (`KeyNotFoundException` if
missing), set `profile.AvatarId = avatarId`, `Store` + `SaveChangesAsync` — the
same shape as `UpsertProfileAsync` / `UpdateGroupDescriptionAsync`.

### §2.3 Serving-lane rule (the contract follow-on lanes copy)

`src/Kumunita.Web/Controllers/ProfileController.cs` — the avatar serving action
(mirrors `ProfileController.Preview`'s `profile.ToAuditableResource()` +
`_authz.CanAsync(...Read...)` idiom exactly — the exact pinned shape for U7):
```csharp
    [Authorize]
    [HttpGet("/profile/avatar/{subjectId}")]
    public async Task<IActionResult> Avatar([FromRoute] string subjectId)
    {
        var viewer = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (viewer is null) return Challenge();

        var profile = await _userInfo.GetProfileAsync(subjectId);
        if (profile is null) return NotFound();                 // M5
        if (profile.Blocked) return NotFound();                 // M4 (blocked supersedes)
        if (string.IsNullOrEmpty(profile.AvatarId)) return NotFound(); // no avatar set → 404 (fail-closed)

        var decision = await _authz.CanAsync(viewer, AccessAction.Read, profile.ToAuditableResource());
        if (!decision.Allowed) return NotFound();              // M3 (Deny → 404; audit already committed, C-MED·2)
        // M1 (owner) auto-allows here via the owner branch of the same call.

        var media = await _media.GetAsync(profile.AvatarId);   // MediaObject? (catalog in `mt`, C-MED·7)
        if (media is null) return NotFound();                  // byte gone / doc missing → fail-closed
        var stream = await _media.OpenReadAsync(profile.AvatarId);
        Response.TryAddHeader("X-Content-Type-Options", "nosniff");
        return File(stream, media.ContentType);                // correct stored Content-Type (C-MED·5)
    }
```
Notes: `ClaimTypes` here is the repo's custom claim names (same as `Edit()`);
`_media` is `IMediaStore` (added to the ctor), `_authz` is
`IAuthorizationService` (added to the ctor). The `NotFound()` on Deny (not
`403`) is the repo's fail-closed idiom (matches `PreviewAsAsync` returning
`null` → the caller's 404). **This** action is C-MED·1/2/3 in code.

### §2.4 Upload rule (U6)

`ProfileController.AvatarUpload` (owner-scoped, self-only — C-MED·8):
```csharp
    [Authorize]
    [HttpPost("/profile/avatar")]
    public async Task<IActionResult> AvatarUpload([FromForm] Microsoft.AspNetCore.Http.IFormFile? file)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (subject is null) return Unauthorized();             // U7a defensive

        if (file is null || file.Length == 0)
            return BadRequest("Choose an image.");             // U7c empty
        if (_mediaOpts.MaxBytes > 0 && file.Length > _mediaOpts.MaxBytes)
            return StatusCode(StatusCodes.Status413RequestEntityTooLarge); // U7b
        if (!_mediaOpts.IsAllowed(file.ContentType))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);  // U7c type (incl. SVG)

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var media = await _media.PutAsync(ms.ToArray(), file.FileName, file.ContentType, subject);

        await _userInfo.SetProfileAvatarAsync(subject, media.Id, subject); // C-MED·8 single lane
        return RedirectToAction("Edit");
    }
```
`_mediaOpts` = `IOptions<MediaOptions>` (added to the ctor). The self-only
scoping (U7a) is structural: the form's `subject` is the current user, never a
path param — a foreign-subject upload is simply unreachable (and the `[Authorize]`
+ self-only shape keeps ADR 0003 intact, same as `ProfileController.Edit()`).

### §2.5 Pinned seam-test names

**Core — `tests/Kumunita.Core.Tests/Media/LocalVolumeFileStoreTests.cs`** (temp-dir `RootPath`):
- `LocalVolumeFileStore_Put_open_read_roundtrips`
- `LocalVolumeFileStore_Put_idempotent_same_id_no_second_writer`
- `LocalVolumeFileStore_OpenRead_missing_throws_FileNotFound`
- `LocalVolumeFileStore_Delete_missing_throws_FileNotFound`

**Core — `tests/Kumunita.Core.Tests/Media/LocalVolumeMediaStoreTests.cs`** (temp-dir file store + a `Marten` doc store over `MediaObject`):
- `LocalVolumeMediaStore_Put_dedups_by_content_hash`
- `LocalVolumeMediaStore_OpenRead_missing_doc_throws_KeyNotFound`
- `LocalVolumeMediaStore_Put_sets_CreatedById_and_SizeBytes`

**Core — `tests/Kumunita.Core.Tests/UserInfo/ProfileAvatarLaneTests.cs`** (or fold into an existing `UserInfoService` test file):
- `UserInfoService_SetProfileAvatar_sets_id`
- `UserInfoService_SetProfileAvatar_null_clears`
- `UserInfoService_SetProfileAvatar_missing_profile_throws_KeyNotFound`

**Web — `tests/Kumunita.Web.Tests/ProfileAvatarServingTests.cs`** (FACES M1–M6):
- `Serving_SignedAuthorizedOwner_Returns200_CorrectContentType`      (M1)
- `Serving_SignedAuthorizedOther_Returns200`                          (M2)
- `Serving_SignedDeniedAudience_Returns404_And_Audits`               (M3)
- `Serving_BlockedProfile_Returns404`                                 (M4)
- `Serving_UnknownProfile_Returns404`                                 (M5)
- `Serving_Unsigned_Challenges_No200`                                 (M6)

**Web — `tests/Kumunita.Web.Tests/ProfileAvatarUploadTests.cs`** (U7a/b/c):
- `Upload_OwnerValidRaster_SetsAvatarAndServes`                       (M1 roundtrip)
- `Upload_Oversize_Returns413_NoFileWritten`                          (U7b)
- `Upload_WrongType_Returns415_NoFileWritten`                         (U7c)
- `Upload_Empty_Returns400`                                          (U7c empty)

> The Web tests are written against the existing `Kumunita.Web.Tests` harness
> shape (see `DirectoryServiceTests` / any existing controller test for the
> fixture). If a route/authz fixture does not yet exist for `ProfileController`,
> U6/U7/U9 establish the minimal one in their deliverables and note it in the
> handoff file.

### §2.6 Acceptance gate (per AGENTS.md — the xunit.v3 `dotnet exec` path)

Every unit's **Exit** is `run_build` **green** for the touched projects. The
feature's close (U10) additionally runs the full suite via the *only reliable*
path (AGENTS.md § "Running the tests"):
```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll   # ~<2s
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll # ~20s (Testcontainers)
```
Record a `## Media — Closed (recorded)` section in **this** doc (mirroring M3's
`## M3 — Closed (recorded)`) with the per-suite pass counts, and a `## Summary`
in the handoff notes. A `413`/`415`/`404`/`200` in these suites is a *real*
pass/fail, not a discovery artifact (the `run_tests` bridge bug AGENTS.md warns
about is a discovery issue, not a result).

### §2.7 Drift-guard (unit-series rules)

1. A unit never modifies a file not in its own `Deliverables`.
2. **Never reshapes** `MediaObject` / `Profile` / the store seams beyond the
   additive shapes pinned in §2.2; on a mismatch **the doc wins** — fix the code
   in the same commit + append a note to the handoff file.
3. Never introduces a test whose exact name is not in the §2.5 list (a unit may
   *rename only with a note*; no silent test drift).
4. Never opens a *new* seam on `IAuthorizationService` / `IUserInfoService`
   beyond `SetProfileAvatarAsync`; never adds a new `AccessAction` / `AccessVia`
   id (C-MED·1).
5. Never serves media from a public/static folder or mounts the volume as a
   static path (C-MED·3); never writes a volume file that bypasses the
   `IMediaStore` seam (C-MED·6).
6. ADR 0011 (U10) must not contradict this doc — if it does, **A1011 is corrected**,
   not this doc.
7. Follow-on lanes (group logos / post attachments / badge icons) are a
   **separate feature** with its own design doc + units; this feature does not
   stub them (Scope, Out-of-scope).

---

*This doc is the **primary** reference tier. U1–U9 code against it. Each unit's
entry-read list (in `docs/plans-milestones/in-progress/plan-media-file-storage.md`)
cites the § it implements. The doc is written once (by the plan author) and
only ever touched per the §2.7 drift-guard — never by a unit deciding a shape
twice.*
