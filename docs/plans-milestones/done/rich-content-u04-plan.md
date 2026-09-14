# RC U04 — `POST /content-image` upload lane + the composer control (posts first)

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained. Primary tier = `docs/design/rich-content-design.md`
> (§Pinned contract is authoritative); mismatch → record
> `## U04 — Drift pause` in the handoff note — do not silently pick.

## Goal

The **upload boundary** (R·6 — ADR 0011's guards **verbatim**, the
second consumer of the same `IMediaStore` chokepoint) and the
**composer control** for the post surface: `insert-image.ts` (the
plain-TS module — `tsc`-only, **no editor dependency enters
`package.json`**), the markup on the two post forms, and the
**`ImageIds` population** server-side (R·3 — parse the body's
`/content-image/{id}` links on write; the draft records carry the
ids, the service writes them verbatim — Core stays body-parse-free).
Precedent to study: the avatar upload (`ProfileController`
§AvatarUpload) and its composer (`client/lib/avatar-upload.ts`).

## Entry reads (≤ 5 files)

1. `docs/design/rich-content-design.md` — §Invariants R·3/R·6,
   §Pinned contract (the upload action, the JS surface + alt rule,
   the `PostDraft`/`GroupPostDraft` ADDs), §Pinned seam tests
   (the 4 `Upload_*` names in `ContentImageUploadTests.cs` — **do
   not write them yet**, U07 does).
2. `src/Kumunita.Web/Controllers/ContentImageController.cs` (U03) —
   the route you're extending; the id-validation check you'll
   reuse/reference.
3. `src/Kumunita.Web/Controllers/ProfileController.cs` §AvatarUpload
   (≈ 60 lines) — the guard ordering: empty → 400, oversize
   (`MediaOptions.MaxBytes`) → 413, disallowed content type
   (`MediaOptions.AllowedContentTypes`, `image/jpeg|png|webp|gif`)
   → 415, **all before any write**, then one `PutAsync`.
4. `src/Kumunita.Web/client/lib/avatar-upload.ts` + the `avatar`
   form's markup in the profile view (grep `data-avatar` or
   `avatar-upload`) — the TS module pattern (ES module,
   `querySelectorAll`, `addEventListener`, the CSRF convention —
   check `client/lib/api.ts` or the module itself for the
   `X-CSRFToken`-style header) and the form's `enctype` +
   `accept` attributes to mirror.
5. `src/Kumunita.Core/Posts/PostDraft.cs` (or wherever `PostDraft`
   + `GroupPostDraft` live — they are records per the design doc;
   find them) + the post create/edit controller actions that bind
   them (grep `PostDraft` in `src/Kumunita.Web/Controllers/`).
   **Pin the exact draft type names + the two post actions** (create
   and edit) in the handoff note — U05 spreads to the other three
   surfaces and reads your note.

## Deliverables (5 files)

### 1. `src/Kumunita.Web/Controllers/ContentImageController.cs` (modify)

The upload action (the pinned shape; the design doc's §Pinned
contract is authoritative):

```csharp
[Authorize]
[HttpPost("/content-image")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> Upload([FromForm] IFormFile? file)
```

The guard ordering is **pinned** (R·6, verbatim from the avatar
lane — mirror it, don't reinvent):
1. `file` null/empty (`Length == 0`) → `BadRequest()` (400).
2. `file.Length > media.MaxBytes` (whatever the `IMediaStore`/
   `MediaOptions` seam exposes — the avatar action shows the exact
   property; mirror it) → `StatusCode(413)` (413).
3. content type not in the allowlist (the avatar action's exact
   check — `file.ContentType` against the allowlist, case-insensitive
   if that's what the avatar does) → `StatusCode(415)` (415).
4. **all guards passed, no write yet** → read
   `await file.OpenReadStream()` into a `byte[]` (or the seam's
   exact call shape), `var stored = await media.PutAsync(bytes,
   file.FileName, file.ContentType, actorId)` (the actor id from
   `User.Identity?.Name` — the avatar action shows the exact source;
   mirror it).
5. `return Json(new { id = stored.Id });` (the design doc's exact
   response — the id is a content hash, not secret; the route 404s
   until a referencing doc exists, R·4).

**No audit call** in the upload action (the write is authenticated,
not an audience-restricted read — the avatar upload makes the same
choice; if the avatar lane **does** audit, mirror that instead and
record the deviation).

### 2. `src/Kumunita.Web/client/lib/insert-image.ts` (new)

The pinned surface (the design doc is authoritative):

```ts
export function bindInsertImage(root: HTMLElement): void
```

Behavior (the pinned alt rule + flow):
- Finds **within `root`**: the `input[type="file"][data-insert-image]`
  and the `textarea[data-image-target]` (the two data-attributes are
  **pinned** — U05 spreads to three more surfaces and relies on
  these exact attribute names; if the avatar form uses a different
  convention, **keep these** and note it — the attribute names are
  part of the FACES contract).
- On the file input's `change`: if no file, return. `fetch(
  "/content-image", { method: "POST", body: <FormData with the file,
  key "file">, headers: <the CSRF header per the convention you
  found in entry read 4>, ... })` — the upload `fetch` is
  **CSRF-aware per the `client/lib/api.ts` convention** (U04
  confirms the exact header — read `client/lib/api.ts` first; if it
  exposes a helper, use it; record the header name in the handoff
  note).
- Response `ok` + JSON `{ id }` → build `![{alt}](/content-image/{id})`
  where **alt = the file name without extension, truncated to 40
  chars** (the pinned alt rule; no user prompt for alt — keep the
  lane small) → insert at the textarea's current cursor position
  (`selectionStart`/`selectionEnd`), preserving the rest of the text.
- Response not ok → show the status text next to the file input
  (reuse the avatar form's error-display element pattern if it has
  one; otherwise a sibling `<span data-insert-image-error>` you
  add to the form markup below — record which you did).
- **No new npm dependency.** The module is plain ES, same as
  `avatar-upload.ts` (check its import/export style and match).

### 3. The two post forms (modify) — **pin the exact view files**

The entry read 5 names the two post actions (create + edit); their
views are the deliverable. Add to **each** form body (the exact
markup is **not** pinned — the data-attributes are; shape it after
the avatar form's control so the two surfaces look alike):

```html
<div class="rc-insert-image">
  <input type="file" accept="image/jpeg,image/png,image/webp,image/gif" data-insert-image />
  <span data-insert-image-error></span>
</div>
```

and change the body textarea to
`<textarea … data-image-target></textarea>`. Add the module's load
call wherever the layout/scripts block for the page includes the
other `client/lib` modules (the avatar page does this — mirror the
exact mechanism: a `<script type="module">` or the layout's existing
script include; record it).

### 4. `PostDraft.cs` + `GroupPostDraft.cs` (modify — the records)

Append a **trailing** parameter with a default (the design doc's
pinned shape; **append last, never insert mid-list** — source-
compatibility):

```csharp
// PostDraft: append after the last existing parameter
IReadOnlyList<string> ImageIds = []
```

and the same on `GroupPostDraft` (the design doc's §Pinned contract
names both). If either record uses `init`-only properties or a
different shape, match the existing shape and record the deviation.

### 5. The post controller actions (modify) — **the server-side parse (R·3)**

In **both** post actions (create + edit, whichever shape binds
`PostDraft`/`GroupPostDraft`): after binding, compute the ids from
the **body** — the body is the source of truth (the client never
sends the ids as a form field; the form field would be spoofable).
A **shared helper** (the design doc's U05 names it
`ExtractContentImageIds`; author it **here** in U04 so U05's three
surfaces reuse it — put it in a small static class
`src/Kumunita.Web/Security/ContentImageIds.cs` or as a `private
static` on the controller if the codebase's convention prefers
that — check the `MarkdownRenderer` namespace/shape as the
convention anchor, and record the chosen home):

```csharp
// Regex over the body's route-shaped image links (the renderer's
// accept-branch-1 shape, verbatim: /content-image/[0-9a-f]{1,128}):
public static IReadOnlyList<string> ExtractContentImageIds(string? body)
```

- `null`/empty body → `[]`.
- Deduplicate (a body that references the same image twice — the
  renderer renders both — stores one id), **preserve first-
  occurrence order** (the serving route's reverse lookup returns
  the first owner; deterministic order keeps the field stable).
- Set `draft.ImageIds = <parsed>` (records are immutable → construct
  the draft with the ids **before** passing to the service, i.e.
  `with { ImageIds = ContentImageIds.ExtractContentImageIds(draft.Body) }`
  or the record's equivalent — the service writes `draft.ImageIds`
  verbatim onto the `Post`/`PostReply` doc's new field; **confirm
  the service's write path actually copies the field** — if the
  service builds the doc field-by-field (likely, given the
  additive-field convention), add `ImageIds = draft.ImageIds` to
  that mapping — the service file is a **6th** file in this unit
  only if the mapping is explicit; if the service maps the whole
  draft by convention, note "not needed" in the handoff note).

## Exit criteria

- `dotnet build` green; `npm run build` (the `ts:build` task) green
  (the new TS module compiles — the `client/` → `wwwroot/js/` emit).
- **No** test files (U07's 4 `Upload_*` tests assume exactly this
  guard ordering + response shape — do not pre-write them).
- A manual smoke (record in the handoff note): with the app
  running (the `run` task, background) + a logged-in session if the
  CSRF/auth path allows it via curl (otherwise note
  "verified by build + the guard code reads verbatim-identical to
  the avatar action"): `curl -i -X POST
  http://localhost:PORT/content-image` with no body → **400**; with
  a small valid PNG (`--data-binary
  @file.png; -H "Content-Type: image/png"` + the CSRF header) →
  **200** with `{"id":"…"}`; then `curl -i
  http://localhost:PORT/content-image/<that-id>` → **404** (no
  owner yet — R·4's orphan posture, the exact behavior R5 will pin).
  If auth blocks the curl, note exactly which step stopped and
  why — do not chase the browser.
- **Handoff note** (append): `## U04 — upload lane + post composer`,
  6–8 lines: (a) the two post view file names (exact paths) + the
  two action names + the two draft type names; (b) the helper's
  chosen home (file path) + its regex (the exact pattern string);
  (c) the service mapping change (file + the one line added, or
  "not needed — <reason>"); (d) the CSRF header name + the JS
  mechanism (script include vs module import); (e) the smoke
  results (400 / 200+id / 404-orphan, or the stopping point);
  (f) the drift pause, if any.
- **Then move this file:** `Move-Item
  docs\plans-milestones\in-progress\rich-content-u04-plan.md
  docs\plans-milestones\done\rich-content-u04-plan.md`.
