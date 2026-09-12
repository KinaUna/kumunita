# U1 execution plan (working) — `MediaOptions` + `IMediaFileStore` + `LocalVolumeFileStore`

> My (unit U1's) working plan for this pass. The **authoritative spec** is
> `media-u1-plan.md` (the authored unit) + the **primary reference** — the exact
> C# to implement — in `../../design/media-file-storage-design.md` §2.2 (the four
> type shapes *above* `IMediaStore`) + §2.7 rules 5/6 + invariants C-MED·4/5/6/7.
> Prior handoff section read: **U0** (the plan author's) in
> `media-file-storage-handoff-notes.md`.

## Scope (in / out)

- **In (mine):** the byte-level half of the `Kumunita.Core.Media` module —
  `MediaOptions`, `IMediaFileStore`, `LocalVolumeFileStore` + the DI
  registration. No Marten. No `MediaObject` / `IMediaStore` / `MediaDocTypes`
  (those are U2). No `Profile.AvatarId` / `SetProfileAvatarAsync` (U4).
- **Out:** everything above U1 in the unit series. Drift-guard §2.7 rule 1:
  I only write the four files below and nothing else.

## Constraints I keep pinned while writing

- **C-MED·6 (HTTP-free):** `IMediaFileStore` is bytes + paths only — no
  `IFormFile`, no `Stream` return on the *write/exists/delete* surface
  (`OpenReadAsync` may return a read-only `Stream` per §2.2; that's the only
  `System.IO` surface and it's BCL, not Web).
- **C-MED·5 (raster allowlist):** `ResolvedAllowedTypes` default is exactly
  `{"image/jpeg","image/png","image/webp","image/gif"}` — SVG excluded. Do not
  drift the default set.
- **C-MED·4 (content-addressed, atomic):** `PutAsync` writes tmp → `fsync`
  (`Flush(flushToDisk: true)`) → atomic `File.Move(overwrite:true)`, idempotent
  by content id. Never a direct final write.
- **C-MED·7 (orphan-safe / fail-closed):** `ExistsAsync` returns false on a
  missing file; `OpenReadAsync` throws (`FileNotFoundException`) and
  `DeleteFileAsync` throws (`FileNotFoundException`) when the file is absent —
  nothing references a bare hash.
- **Path shape:** `{RootPath}/{id[0..2]}/{id}`, hex-only id; the sharded dir is
  `_` when the id is < 2 chars.

## Entry reads (done)

| Read | Why |
|------|-----|
| design doc §2.2 + §2.7 + invariants | the exact type shapes to implement |
| `src/Kumunita.Core/CommunityOptions.cs` | the options shape to mirror |
| `src/Kumunita.Core/DependencyInjection.cs` | the `AddOptions<…>` + `AddTransient<…>` registration idiom |
| `src/Kumunita.Core/M3DocTypes.cs` | doc-type-surface shape (context only — U1 does **not** add a doc surface) |
| `README.md` | "one database" + self-hosted principle (the volume fits the model) |

## Steps (each leaves the tree in a buildable state)

1. `src/Kumunita.Core/Media/MediaOptions.cs` — mirror `CommunityOptions`;
   `SectionName = "Media"`; `RootPath` default `{AppContext.BaseDirectory}/media`;
   `MaxBytes` 5 MiB; `ResolvedAllowedTypes` (default 4 raster types, CSV-split,
   trims + removes empty); `IsAllowed` (case-insensitive, trims input).
2. `src/Kumunita.Core/Media/IMediaFileStore.cs` — the seam, `Task`-based,
   `CancellationToken` on every surface, `RootPath { get; }`.
3. `src/Kumunita.Core/Media/LocalVolumeFileStore.cs` — ctor
   `IOptions<MediaOptions>`, ensures root dir (idempotent), `PathFor` sharded
   hex path, atomic `PutAsync`, fail-closed `Exists/Read/Delete`.
4. edit `src/Kumunita.Core/DependencyInjection.cs` — `using Kumunita.Core.Media;`,
   `services.AddOptions<MediaOptions>();` +
   `services.AddTransient<IMediaFileStore, LocalVolumeFileStore>();`.

## Exit (per the unit's "Exit" + AGENTS.md)

- `run_build` green on `Kumunita.Core` (full `Kumunita.slnx`).
- Append **`## U1`** to `media-file-storage-handoff-notes.md`: the exact
  registrations verbatim, the `ResolvedAllowedTypes` default verbatim, and a
  line confirming "no Marten used yet."
- Flip the handoff **Status** table's U1 row to **done**.

## Verification

- Build via `run_build` (the tool runs `dotnet build`).
- No new tests in U1 (this unit pins no seam-test names; §2.5 tests are U3/U9).
- Confirm `ResolvedAllowedTypes` compiles against the pinned default and that no
  `Marten` / `System.Web` / HTTP reference crept into the four files.
