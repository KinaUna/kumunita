# U1 — Core module: `MediaOptions` + `IMediaFileStore` + `LocalVolumeFileStore`

> Self-contained: this file + the **entry reads** below is the whole context
> you need to complete the unit. **Do not** scan the whole repo. The primary
> reference — the exact C# you are implementing — is
> `docs/design/media-file-storage-design.md` §2.2 (the four new type shapes
> *above* `IMediaStore`) + §2.7 rules 5/6. A prior handoff section (if any)
> is **U0** (the plan author's) in `media-file-storage-handoff-notes.md`.

## Understanding

Add the **byte-level** part of the `Kumunita.Core.Media` module: the options
shape (`MediaOptions`, Core-agnostic, a `CommunityOptions` twin), the
file-I/O seam (`IMediaFileStore`), and the local-volume implementation
(`LocalVolumeFileStore`, sharded `{root}/{id[0..2]}/{id}`, atomic
tmp-rename-fsync, **fail-closed on missing** reads/deletes so orphan-safe).
This unit has **no Marten dependency** — it composes nothing yet; everything
else in the feature (U2+) builds on it.

## Assumptions

- `Kumunita.Core` stays **HTTP-free** (ADR 0006-D): no `IFormFile`/`Stream` in
  the seam — `IMediaFileStore` takes/returns bytes + paths only (C-MED·6).
- `MediaOptions` is a **`public static class`** twin of
  `src/Kumunita.Core/CommunityOptions.cs` (`SectionName` const + a static
  `ResolvedAllowedTypes` getter defaulting to `jpeg|png|webp|gif`). SVG is
  **excluded** (C-MED·5) — the allowlist is the extension point for follow-on
  lanes.
- The **one thing this unit must respect:** the volume is written/removed
  **only** via `IMediaFileStore` — no controller/helper touches the disk
  directly (C-MED·6). `OpenReadAsync` / `DeleteFileAsync` / `ExistsFile` all
  **fail closed** if the file is absent (orphan-safe).

## Approach

Mirror `CommunityOptions` for the options shape; implement `IMediaFileStore`
as a pure seam; implement `LocalVolumeFileStore` against `IMediaFileStore`.
Register both in `AddKumunitaCore()` (add `AddOptions<MediaOptions>()` +
`AddTransient<IMediaFileStore, LocalVolumeFileStore>()`).

## Key files (entry reads — no more)

- `docs/design/media-file-storage-design.md` — §2.2 (`MediaOptions`,
  `IMediaFileStore`, `LocalVolumeFileStore`) + §2.7 rules 5/6.
- `src/Kumunita.Core/CommunityOptions.cs` — options shape to mirror.
- `src/Kumunita.Core/DependencyInjection.cs` — where to register.
- `src/Kumunita.Core/M3DocTypes.cs` — the doc-type-surface shape (context only
  — U1 does **not** add a doc-type surface; U2 does).
- `README.md` — "one database" + self-hosted principle (the volume fits here).

## Deliverables (3 new files + 1 edit)

- `src/Kumunita.Core/Media/MediaOptions.cs`
- `src/Kumunita.Core/Media/IMediaFileStore.cs`
- `src/Kumunita.Core/Media/LocalVolumeFileStore.cs`
- edit `src/Kumunita.Core/DependencyInjection.cs` — add
  `services.AddOptions<MediaOptions>()` +
  `services.AddTransient<IMediaFileStore, LocalVolumeFileStore>()`.

## Risks & open questions

- **`ResolvedAllowedTypes` default** must be exactly `{"image/jpeg","image/png","image/webp","image/gif"}`
  — a unit deviating from the design-doc default drifts the C-MED·5 allowlist.
- **Atomic write:** tmp file → `fsync` → rename. A direct `File.WriteAllBytes`
  final write defeats crash-safety on the volume.
- If `LocalVolumeFileStore` needs a constructor (e.g. to read `MediaOptions`),
  keep it **dependency-agnostic** — the design doc pins the seam; a ctor that
  takes `IOptions<MediaOptions>` is acceptable (the Core-agnostic options are
  fine, only HTTP types are banned).

## Steps

1. Add `MediaOptions.cs` (mirror `CommunityOptions`).
2. Add `IMediaFileStore.cs` (bytes + paths only).
3. Add `LocalVolumeFileStore.cs` (sharded path, atomic write, fail-closed
   read/delete/exists).
4. Register in `DependencyInjection.cs` (`AddOptions<MediaOptions>()` +
   `AddTransient<IMediaFileStore, LocalVolumeFileStore>()`).
5. `run_build` → green on `Kumunita.Core`.
6. Append **`## U1`** to `media-file-storage-handoff-notes.md`: the exact
   registrations, the `ResolvedAllowedTypes` default (verbatim), and a line
   confirming "no Marten used yet."
