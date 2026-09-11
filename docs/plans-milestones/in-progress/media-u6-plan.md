# U6 — Web: `ProfileController.AvatarUpload` (self-only upload)

> Self-contained: this file + the **entry reads** below is the whole context.
> The exact action shape is `docs/design/media-file-storage-design.md` §2.2;
> the seam tests it will be locked by are §2.5 (U7a/b/c). A prior handoff
> section (if any) is **U5**.

## Understanding

Add the **upload action**: the `IFormFile` boundary (Web-only, C-MED·6 — **not**
in Core), owner-scoped to the **current user** (C-MED·8, self-only — `subject`
is never a path param), size/type validated against `MediaOptions`, then the
two-Core-lane write (`Put` → `SetProfileAvatarAsync`). This is the **write**
half of the avatar feature — the **serve** half is U7.

## Assumptions

- `Kumunita.Core` stays **HTTP-free** (ADR 0006-D, C-MED·6): `IFormFile` is
  **Web-only** — the action reads it into bytes, then calls
  `IMediaStore.Put` (bytes + metadata) + `IUserInfoService.SetProfileAvatarAsync`.
- **Self-only** (C-MED·8): the `subject` is the **current user** (the action
  reads `SubjectId(User)`, never a path param). A `subject`-param drift is a
  **fail-closed** design-doc violation.
- **Size/type guard** against `MediaOptions` (C-MED·5): the action
  validates `IFormFile.Length` + `ContentType` against
  `MediaOptions.MaxBytes` + `MediaOptions.ResolvedAllowedTypes` **before**
  calling `Put` (a `Put` with a disallowed type is a C-MED·5 violation).

## Approach

Add the `AvatarUpload` action (design doc §2.2 shape): read `IFormFile` →
guard size + type → `Put` (bytes + filename + content-type + `actorBy`) →
`SetProfileAvatarAsync` (the stored `MediaObject` id). Add `IMediaStore` +
`IOptions<MediaOptions>` to the `ProfileController` ctor (the Web-only
`IFormFile` boundary, C-MED·6).

## Key files (entry reads — no more)

- `docs/design/media-file-storage-design.md` — §2.2 (the `IFormFile` → bytes →
  `Put` → `SetProfileAvatarAsync` shape) + §2.5 (U7a/b/c test names) +
  §2.7 rules 4/5/6 (the C-MED·6 HTTP-free Core + C-MED·8 self-only owner).
- `docs/plans-milestones/in-progress/media-u2-plan.md` + `media-u4-plan.md`
  + the **U2** + **U4** handoff (the `IMediaStore` + `SetProfileAvatarAsync` +
  `MediaOptions` to inject).
- `src/Kumunita.Web/Controllers/ProfileController.cs` — the existing
  controller shape (the owner-scoped self-only + the `IFormFile`-free upload
  idiom to follow — verify the existing `Edit` / `Preview` idiom first).
- `src/Kumunita.Core/CommunityOptions.cs` — the `MediaOptions` shape (the
  size/type guard to apply).

## Deliverables (1 file, modify)

- `src/Kumunita.Web/Controllers/ProfileController.cs` — add
  `AvatarUpload` (design doc §2.2 shape: `IFormFile` → bytes → `Put` →
  `SetProfileAvatarAsync`, self-only + size/type guard), add `IMediaStore` +
  `IOptions<MediaOptions>` to the ctor (Web-only `IFormFile` boundary,
  C-MED·6).

## Risks & open questions

- **`subject` self-only:** the action reads `SubjectId(User)` — **never** a
  path param (C-MED·8). A `subject`-param drift is a **fail-closed**
  design-doc violation (the **upload** side of the C-MED·8 lane).
- **Size/type guard:** the action validates **before** `Put` (a `Put` with a
  disallowed type is a C-MED·5 violation; a `Put` with an oversize payload
  is a `MediaOptions.MaxBytes` violation — a C-MED·5 / config drift).
- **`IFormFile` in Core:** the action reads it into bytes, then calls
  `IMediaStore.Put` (bytes + metadata) — **not** passing the `IFormFile`
  object itself (C-MED·6 — `Kumunita.Core` stays HTTP-free).

## Steps

1. Add `IMediaStore` + `IOptions<MediaOptions>` to the `ProfileController`
   ctor (Web-only `IFormFile` boundary, C-MED·6).
2. Add the `AvatarUpload` action (self-only: `subject = SubjectId(User)`;
   size/type guard; `Put` → `SetProfileAvatarAsync`).
3. `run_build` → green on `Kumunita.Web`.
4. Append **`## U6`** to the handoff notes: the action signature (verbatim
   from the design doc §2.2), the two-Core-lane call order (`Put` →
   `SetProfileAvatarAsync`), a line confirming `IFormFile` is Web-only (not
   in Core — C-MED·6), and the §2.5 U7a/b/c test names the U9 test will
   target (so U9 can lock them).
