# ATT U8 — Core option: attachment allowlist + Web: `POST /attachment` upload route

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-file-attachments.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **Precondition:** U3 shipped (the `AttachmentIds` fields exist — not strictly
> needed for this unit, but confirms U3 landed). This unit is **code** — it ends
> with a green `dotnet build`.

## Understanding

This unit adds the **content gate** and the **write path** for attachments:
(a) the `MediaOptions` **attachment allowlist** (a Core-agnostic options member
alongside the existing image allowlist), and (b) the **`POST /attachment`
upload route** (a Web controller mirroring the image lane's
`POST /content-image`). Together they are the C-ATT·6 gate: *empty → 400,
oversize → 413, disallowed type → 415, guards **before** any write, then one
`IMediaStore.PutAsync`.* The `IMediaStore` itself is **untouched** (C-ATT·1/3)
— you are adding an option member and a Web route, nothing to the store seam.

## The invariants you are implementing

- **C-ATT·1** — the upload writes to the **same** `IMediaStore` the image lane
  uses. One store, one volume, one catalog.
- **C-ATT·3** — `IMediaStore` / `MediaObject` / `LocalVolumeFileStore` are
  **unchanged**. You call the existing `PutAsync`; you do not add a method.
- **C-ATT·6** — the **allowlist is the only content gate**. The three guards
  run in order, **before** any `PutAsync`. SVG is excluded from the default.
- **C-ATT·9** — the image lane (`AllowedContentTypes` / `ResolvedAllowedTypes`
  / `IsAllowed`, the `ContentImageController`) is **untouched**. You add
  **alongside**.

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/design/file-attachments-design.md` — **§2.5** (the
   `MediaOptions` attachment-members shape — the instance-style
   `AttachmentAllowedContentTypes` / `ResolvedAttachmentAllowedTypes` /
   `IsAttachmentAllowed`) and **§2.6** (the upload route's exact guard order +
   status codes + the `PutAsync` call). Read both fully.
2. `src/Kumunita.Core/Media/MediaOptions.cs` — the **mirror source** for §2.5
   (read the whole file, ~35 lines). Note the **instance** shape: `string?
   AllowedContentTypes { get; set; }`, the **instance** `IEnumerable<string>
   ResolvedAllowedTypes` property (a `Split` over the pinned default), and the
   **instance** `bool IsAllowed(string?)`. You add the three attachment twins
   in the **same instance style**.
3. `src/Kumunita.Core/Media/IMediaStore.cs` — the **real** `PutAsync`
   signature (read the whole file). It is
   `Task<MediaObject> PutAsync(byte[] content, string? filename, string
   contentType, string? actorId, CancellationToken ct = default)` — **five**
   params, `filename` and `actorId` nullable, trailing optional
   `CancellationToken`. Your `AttachmentController.Upload` call must match it
   **exactly** (pass `ms.ToArray(), file.FileName, file.ContentType, subject` —
   let `ct` default). (Do **not** invent a 1-arg overload; do **not** change
   this interface — C-ATT·3.)
4. `src/Kumunita.Web/Controllers/ContentImageController.cs` — the **mirror
   source** for the upload route. Read the `Upload` action (≈ L150–L185) + the
   class constructor (the DI shape: `IMediaStore`, `IOptions<MediaOptions>`,
   and the principal-shaping `KumunitaPrincipal.SubjectId`). Note:
   `[Authorize]` + `[ValidateAntiForgeryToken]` + `[HttpPost("/content-image")]`,
   the three guards (empty → `BadRequest("Choose an image.")`, oversize →
   `StatusCode(413…)`, disallowed → `StatusCode(415…)`), the
   `file.CopyToAsync(ms)` → `media.PutAsync(ms.ToArray(), file.FileName,
   file.ContentType, subject)` → `return Json(new { id = stored.Id })` shape.
5. `src/Kumunita.Web/Program.cs` or `src/Kumunita.Web/DependencyInjection.cs` —
   **where** `IOptions<MediaOptions>` is registered (grep for
   `AddOptions<MediaOptions>` or `services.Configure<MediaOptions>`). You are
   **not** adding a new registration — the attachment options live on the same
   `MediaOptions` instance, so the existing `IOptions<MediaOptions>` binding
   (OPS binds `Media__*`) already covers the new `AttachmentAllowedContentTypes`
   member. This read is to **confirm** you don't need a new registration.

## Deliverables (2 edits: 1 Core option member, 1 new Web controller)

### 1. `src/Kumunita.Core/Media/MediaOptions.cs` — the attachment allowlist (C-ATT·6)
Add **after** the existing `IsAllowed` member, in the **same instance style**:

- `public string? AttachmentAllowedContentTypes { get; set; }` — with a
  doc-comment: "Comma-separated allowed Content-Types for the attachment lane
  (case-insensitive). Distinct from <see cref="AllowedContentTypes"/> (the
  image lane) — the attachment lane has its own gate (C-ATT·6); the config key
  is <c>Media:AttachmentAllowedContentTypes</c>."
- `public IEnumerable<string> ResolvedAttachmentAllowedTypes =>
  (AttachmentAllowedContentTypes ??
   "application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/plain,text/csv,application/zip,image/jpeg,image/png,image/webp,image/gif")
   .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);`
  (the same `Split` idiom as `ResolvedAllowedTypes`, with the **pinned default**
  from §2.5; SVG excluded, raster included).
- `public bool IsAttachmentAllowed(string? contentType) =>
  !System.String.IsNullOrWhiteSpace(contentType)
  && ResolvedAttachmentAllowedTypes.Any(t =>
      System.String.Equals(t, contentType.Trim(), System.StringComparison.OrdinalIgnoreCase));`
  (the real `IsAllowed` body, verbatim, over the new
  `ResolvedAttachmentAllowedTypes`).
- **Do NOT touch** the existing `AllowedContentTypes` /
  `ResolvedAllowedTypes` / `IsAllowed` / `MaxBytes` / `RootPath` (C-ATT·9).
- **No new `IOptions<>` registration** — the new members live on the same
  instance; OPS binds them under the existing `Media__*` prefix (confirm in
  entry read 5; if the binding is explicit per-member rather than whole-object,
  add the member to that list and record it in the handoff note).

### 2. `src/Kumunita.Web/Controllers/AttachmentController.cs` (new) — `POST /attachment`
A **new** `sealed class AttachmentController(IMediaStore media,
IOptions<MediaOptions> mediaOpts) : Controller` mirroring
`ContentImageController`'s upload action:

- `[HttpPost("/attachment")]` + `[Microsoft.AspNetCore.Authorization.Authorize]`
  + `[ValidateAntiForgeryToken]`.
- `public async Task<IActionResult> Upload([FromForm] IFormFile? file)`:
  - `var subject = KumunitaPrincipal.SubjectId(User); if (subject is null)
    return Unauthorized();` (the image lane's defensive line).
  - **Guard 1 (empty → 400):** `if (file is null || file.Length == 0) return
    BadRequest("Choose a file.");`
  - **Guard 2 (oversize → 413):** `if (mediaOpts.Value.MaxBytes > 0 &&
    file.Length > mediaOpts.Value.MaxBytes) return
    StatusCode(StatusCodes.Status413RequestEntityTooLarge);`
  - **Guard 3 (disallowed → 415):** `if
    (!mediaOpts.Value.IsAttachmentAllowed(file.ContentType)) return
    StatusCode(StatusCodes.Status415UnsupportedMediaType);`
  - **Write (after all guards):** `using var ms = new MemoryStream(); await
    file.CopyToAsync(ms); var stored = await media.PutAsync(ms.ToArray(),
    file.FileName, file.ContentType, subject);` (the real `PutAsync` — 4 args
    passed, the trailing `CancellationToken` left to default; match entry read
    3's signature exactly).
  - `return Json(new { id = stored.Id });`
- Doc-comment: mirror the image `Upload` action's, restating C-ATT·6 (guards
  before write, SVG excluded, the attachment allowlist is the gate) and C-ATT·1
  (same `IMediaStore`, same volume) + C-ATT·3 (`PutAsync` unchanged, 4-arg).
- **No `[Authorize]` bypass, no audit row** — the write is authenticated, not
  an audience-restricted read (the image lane's choice; carry it over).

## Build gate (must be green before you finish)

```
dotnet build Kumunita.slnx -c Debug
```
Green on **Core** and **Web**. The two compile-break risks: (a) the
`PutAsync` arg count/order — match the real 4-arg signature; (b) the
`StatusCodes` / `BadRequest` / `StatusCode` usings — copy the image controller's
`using Microsoft.AspNetCore.Mvc;` + `Microsoft.AspNetCore` (for
`StatusCodes`).

## Risks & open questions

- **The `PutAsync` signature is the top compile risk.** Read `IMediaStore.cs`
  (entry read 3) and match the real param order/names. If it's
  `(bytes, fileName, contentType, subject)`, your call is
  `media.PutAsync(ms.ToArray(), file.FileName, file.ContentType, subject)`.
  If the real signature differs (e.g. a `CancellationToken` trailing param),
  match **it**, not this plan, and record the drift in the handoff note.
- **Do NOT create a second `IMediaStore` or a new store method.** C-ATT·1/3 —
  the attachment lane rides the existing seam. If you're tempted to add a
  `PutAttachmentAsync`, stop — that is exactly the drift the invariants forbid.
- **Do NOT touch the image `Upload` action or the image allowlist members**
  (C-ATT·9). Your `AttachmentController` is a **new** file; the
  `MediaOptions` additions go **after** the existing members.
- **The default allowlist is pinned** (the 11 types in §2.5). If you add or
  drop a type from the default, you drift C-ATT·6 and the U11
  `AttachUpload_F6_WrongType415` test's expectations. Copy the §2.5 default
  verbatim.
- **No new DI registration** (the attachment options are on the existing
  `MediaOptions` instance). If entry read 5 shows the binding is per-member
  (not whole-object), add the member to that list and record it.
- **Do NOT write tests.** U11 owns the upload tests
  (`AttachUpload_F6_Empty400`, `AttachUpload_F6_Oversize413`,
  `AttachUpload_F6_WrongType415`). If you add a throwaway test, delete it
  before finishing.

## Steps

1. Read the 5 entry reads (design doc §2.5/2.6 first, then `MediaOptions.cs` +
   `IMediaStore.cs` in full, then the `ContentImageController.Upload` region,
   then the DI registration confirm).
2. Edit `MediaOptions.cs`: add the three attachment members
   (`AttachmentAllowedContentTypes`, `ResolvedAttachmentAllowedTypes`,
   `IsAttachmentAllowed`) after the existing `IsAllowed`, instance-style.
3. Create `src/Kumunita.Web/Controllers/AttachmentController.cs` with the
   `Upload` action (the three guards + the 4-arg `PutAsync` + the `Json`
   return), mirroring the image upload.
4. `dotnet build Kumunita.slnx -c Debug` → green on Core + Web. Fix the
   `PutAsync` arg order if it fails.
5. Re-read the 2 edits: confirm the image allowlist members + the image `Upload`
   action are unchanged, the three attachment members are instance-style with
   the pinned default, and the `PutAsync` call matches the real signature.
6. Append a `## U8` section to
   `docs/plans-milestones/file-attachments-handoff-notes.md` recording:
   "`MediaOptions` gained the attachment allowlist
   (`AttachmentAllowedContentTypes` / `ResolvedAttachmentAllowedTypes` /
   `IsAttachmentAllowed`, instance-style, pinned default, SVG excluded);
   added `AttachmentController` with `POST /attachment` (three guards before
   write, 4-arg `PutAsync`, `Json(id)`). Build green (Core+Web). Image lane
   untouched (C-ATT·9). Drift: <none / describe — esp. the real `PutAsync`
   signature and whether a DI registration was needed>. Next agent (U9) adds
   the **serve** route `GET /attachment/{id}` — the 5-step ordering, the reply
   parent-resolution (C-ATT·8), and the `Content-Disposition: attachment`
   header."
7. Done.
