# RC U03 — `ImageIds` on the four owners + reverse lookup + `GET /content-image/{id}`

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained. Primary tier = `docs/design/rich-content-design.md`
> (§Pinned contract is authoritative for shapes); on a mismatch,
> record `## U03 — Drift pause` in the handoff note — do not silently
> pick.

## Goal

Give the serving route its foundations: the four **additive**
`ImageIds` fields (R·7 — zero migrations, ADR 0004 §B.1), the
**reverse-lookup seams** on the existing services (R·5 — Core stays
HTTP-free, un-audited reads), and the **`GET /content-image/{id}`**
route (R·4 — store-miss → 404, owner-miss → 404, one `CanAsync` on
the UGC owner, zero calls on the platform owner, **Deny → 404**,
`nosniff`). No upload yet (U04); no composer (U04/U05); the
`ImageIds` fields start empty (populated server-side in U04/U05).

## Entry reads (≤ 5 files)

1. `src/Kumunita.Core/Posts/Post.cs` + `PostReply.cs` — the
   additive-field doc-comment convention ("the Nth additive field
   after …") to mirror. `ImageIds` is the **5th** additive on `Post`
   (after `Status`, `GroupId`, `LanguageCode`, `DeletedAt` — confirm
   the actual field order by reading), **4th** on `PostReply`.
2. `src/Kumunita.Core/Localization/LocalizedPage.cs` — the third
   owner; confirm its fields and the service that owns it (grep
   `GetPageAsync` in `src/Kumunita.Core/Localization/` — the design
   doc's §Pinned contract pins the reverse-lookup name **here**, in
   an amendment sub-line, once you confirm which type declares the
   page seam; if two candidates exist, pick the one the Web layer
   injects — `StaticPagesController`'s constructor is the tie-break).
3. `src/Kumunita.Core/Posts/PostService.cs` (or wherever the
   `PostService`/`IPostService` read seam lives) — find the existing
   read method (e.g. `GetAsync`) and mirror its query shape for the
   two new `Find*ByImageIdAsync` methods. Note the `IMartenStore`
   query idiom used (LinqToQueries or a direct `Documents<T>`
   query — mirror whatever is already there).
4. `src/Kumunita.Web/Controllers/ProfileController.cs` §Avatar +
   §AvatarUpload (≈ 90 lines) — the **verbatim idiom to copy**: the
   404-before-decision ordering, the one `CanAsync`, `File(stream,
   contentType)` + the `nosniff` header, and the guard ordering.
5. `src/Kumunita.Core/Media/IMediaStore.cs` +
   `src/Kumunita.Web/Security/MarkdownRenderer.cs` §`IsSafeUrl` —
   the store seam (note `OpenReadAsync` returns the stored content
   type) and the id validation style (the route id is the same
   1–128 lowercase-hex shape the renderer's `IsSafeImageSrc` accepts
   — reuse the **same** character test; if it's private in the
   renderer, duplicate the 8-line check in the controller rather than
   making it public — the two are independent layers by design).

## Deliverables (6 files)

### 1. `src/Kumunita.Core/Posts/Post.cs` (modify) + `PostReply.cs` (modify) +
### 2. `src/Kumunita.Core/Localization/LocalizedPage.cs` (modify) +
#### `src/Kumunita.Core/Announcements/Announcement.cs` (modify)

The pinned field on **all four** owners, identical shape (the design
doc's §Pinned contract names **all four** — Post, PostReply,
LocalizedPage, **Announcement** — the announcement owner being named
there because R·4's serving branch names it as a UGC owner, and this
unit's reverse-lookup (deliverable 3) reads the field; this unit
*implements* that spec, it does not amend it — the only
`§Pinned contract amendment (U03)` sub-line this unit writes is for
the `LocalizedPage`-owning service's type name, the same append-only
mechanism U01 sanctioned for exactly that unknown):

```csharp
/// <summary>
/// The content images referenced by <see cref="Body"/> — the
/// <c>MediaObject</c> ids appearing as
/// <c>/content-image/{id}</c> links in the rendered body.
/// Populated server-side by the owning write lane (RC U04/U05);
/// the serving route's reverse lookup reads this (RC R·3/R·4).
/// The Nth additive field after … (ADR 0004 §B.1 — additive,
/// delta-detected, idempotent, no seed reset; RC R·7).
/// </summary>
public IReadOnlyList<string> ImageIds { get; set; } = [];
```

Name the actual ordinal in each comment (Post: 5th; PostReply: 4th;
LocalizedPage: 1st additive; Announcement: per the field you actually
see — adjust to the real field order in each file; the ordinal is
documentation, not behavior). **No constructor changes** (POCOs, not
records — confirm for all four; if any is a record, record a drift
pause and stop).

### 3. The reverse-lookup seams (Core — modify existing service types)

- On the `Post`/`PostReply` service (whichever type already exposes
  the post read seam):
  - `Task<Post?> FindPostByImageIdAsync(string mediaId)` — the
    owner of the first body whose `ImageIds` contains `mediaId`.
    Order by `Created` ascending so the result is deterministic;
    return null when none (the route 404s).
  - `Task<PostReply?> FindReplyByImageIdAsync(string mediaId)` —
    same shape for replies.
- On the announcement service interface + implementation (the type
  that already exposes `GetComponentsAsync` or the equivalent read
  seam — confirm by reading; if the interface is `IAnnouncementService`,
  add the method to **both** the interface and its implementation):
  - `Task<Announcement?> FindByImageIdAsync(string mediaId)`.
- On the `LocalizedPage`-owning service (per entry read 2):
  - the pinned name **`FindPageByImageIdAsync(string mediaId)`** —
    record the confirming type name in the design doc's
    `§Pinned contract amendment (U03)` sub-line (append-only; this is
    the mechanism U01 sanctioned for exactly this).

All four: **un-audited** (R·5 — the audit row belongs to the route's
`CanAsync`), read-only, null-when-absent. If the codebase has an
existing "find by any-list-field" idiom for Marten queries, mirror
it; the query shape itself is **not** pinned (implementation
detail — the names and null-contract are pinned).

### 4. `src/Kumunita.Web/Controllers/ContentImageController.cs` (new)

```csharp
public sealed class ContentImageController(
    IMediaStore media,
    IAuthorizationService authz,
    /* the Post/PostReply service type */,
    /* the announcement service type */,
    /* the LocalizedPage service type */) : Controller
```

One action — the 5-step ordering is **pinned** (R·4; the design
doc's exact shape):

1. **Validate the id**: 1–128 lowercase hex chars (`[0-9a-f]`).
   Reject (400) anything else — a route-shaped id that can't be a
   `MediaObject.Id` will miss the store anyway, but 400 is
   cheaper than a doomed store read.
2. `var stored = await media.GetAsync(id)` — **miss → 404**
   (`NotFound()`). **Zero `CanAsync` calls, zero audit rows.**
3. **Reverse lookup** in owner order: the post/reply service
   (post first, then reply), the announcement service, the page
   service — first non-null wins. **All null → 404** (orphan is
   inert — R·4; there is **no GlobalAdmin branch**). **Zero
   `CanAsync` calls so far.**
4. **Branch by owner:**
   - UGC owner (post/reply/announcement) → **exactly one**
     `await authz.CanAsync(User.Identity?.Name, AccessAction.Read,
     owner)` (the owner's existing `IAuditableResource` — the
     service already shapes one; if the service's existing read
     path calls `CanAsync` itself, **do not** double-call — read
     the owner's read path first and record in the handoff note
     whether the route or the service performs the check, exactly
     once). Allow → serve; Deny → **404** (not 403 — R·4).
   - Platform owner (page) → **no `CanAsync`**, serve directly
     (public by construction — R·4).
5. `var stream = await media.OpenReadAsync(id)` →
   `File(stream, stored.ContentType)` + set
   `Response.Headers["X-Content-Type-Options"] = "nosniff"`.
   (Mirror the avatar route's exact response shape — if it sets the
   header in the action, do the same; if it's in a shared filter,
   record that and skip the manual header.)

Route: `[HttpGet("/content-image/{id}")]` — **exact** path string
(R·3/R·4 name it). No `[Authorize]` on the route (the authorization
is the per-owner `CanAsync`, the avatar idiom — an anonymous
visitor to a public post's image must be served).

**Do not add the upload action** (U04 owns it — the controller
grows by one action, not a new controller).

## Exit criteria

- `dotnet build` green; **no** test files created (the serving tests
  are U07's — U07's pinned names assume exactly this route shape).
- The four fields compile on the four owners (Post, PostReply,
  LocalizedPage, Announcement); the four
  `Find*ByImageIdAsync` methods exist with the pinned names; the
  design doc carries the `§Pinned contract amendment (U03)` sub-line
  naming the `LocalizedPage` service type.
- A manual smoke (record in the handoff note): start the app
  (the `run` task, background), `curl -i
  http://localhost:PORT/content-image/deadbeef` → **404** (store
  miss, no stack), and
  `curl -i http://localhost:PORT/content-image/notahex` → **400**.
  Kill the app after. If the DB is unavailable, note it — do not
  chase it.
- **Handoff note** (append): `## U03 — ImageIds + reverse lookup +
  serving route`, 6–8 lines: (a) the four owners' field ordinals
  (Post, PostReply, LocalizedPage, Announcement — each ordinal as
  written); (b) the four lookup method names + their service
  types (exact type names as found); (c) the route path + the 5-step
  ordering in one compact line each; (d) the smoke result (404/400
  or "skipped — no DB"); (e) the avatar-idiom deviations, if any
  (audit performed by route vs service); (f) the drift pause, if
  any.
- **Then move this file:** `Move-Item
  docs\plans-milestones\in-progress\rich-content-u03-plan.md
  docs\plans-milestones\done\rich-content-u03-plan.md`.
