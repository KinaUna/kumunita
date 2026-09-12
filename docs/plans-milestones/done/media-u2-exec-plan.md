# U2 execution plan (working) — `MediaObject` + `IMediaStore` + `LocalVolumeMediaStore` + `MediaDocTypes`

> My (unit U2's) working plan for this pass. The **authoritative spec** is
> `media-u2-plan.md` (the authored unit) + the **primary reference** — the
> exact C# to implement — in `../../design/media-file-storage-design.md` §2.2
> (the four type shapes at/under `IMediaStore`) + §2.7 rules 5/6 + invariants
> C-MED·1/4/6/7. Prior handoff section read: **U1** in
> `media-file-storage-handoff-notes.md`.

## Scope (in / out)

- **In (mine):** the store seam over U1's byte seam — `IMediaStore` +
  `LocalVolumeMediaStore` (composes `IMediaFileStore` + the host
  `IDocumentStore`), the `MediaObject` `mt` catalog doc, the
  `MediaDocTypes` ADR 0004 §B.1 surface, the `IMediaStore` DI registration,
  the `MediaDocTypes.Configure(opts)` call-site in `Program.cs`, and the
  `Media__*` options binding confirmed.
- **Out:** the U4+ lanes (`Profile.AvatarId`, `SetProfileAvatarAsync`, the Web
  upload/serving lanes), the §2.5 tests (U3/U5/U9). Drift-guard §2.7 rule 1: I
  only touch the five files below and nothing else.

## Constraints I keep pinned while writing

- **C-MED·6 (HTTP-free):** `IMediaStore.PutAsync` takes
  `byte[] / filename? / contentType / actorId?` — **not** `IFormFile`/`Stream`
  (the Web reads the upload into bytes). `OpenReadAsync` may return
  `Task<Stream>` (BCL read-only stream, per §2.2).
- **C-MED·4 (dedup lane):** the `Put` path is exactly
  `Sha256Hex(content)` → `LoadAsync<MediaObject>(id)` → **return the existing
  doc if present**, else bytes-first volume write → `Store(doc)` → one
  `SaveChangesAsync`. Never store-then-check (that races two writers).
- **C-MED·7 (orphan-safe / fail-closed):** `OpenReadAsync` throws
  `KeyNotFoundException` on a missing doc; `RemoveAsync` deletes the doc when
  present and self-cleans an orphan volume file via U1's fail-closed
  `ExistsAsync`/`DeleteFileAsync`.
- **`MediaObject` is a Marten doc in `mt`** (not EF, ADR 0004 §B); `Id` is the
  lowercase-hex SHA-256 (C-MED·4), so Marten's default Id-unique mapping IS
  the dedup — no business-key index.
- **`MediaDocTypes.Configure(opts)` MUST be called in `Program.cs`** next to
  `M1DocTypes`/`M3DocTypes` — otherwise the doc is invisible to Marten
  (C-MED·7 drift, the pinned risk in `media-u2-plan.md`).
- **Marten 9 (9.31.2) is async-only:** sessions are
  `await using var session = _store.LightweightSession();` — the codebase
  proven idiom (`PostsController` / `AnnouncementController`). The design doc's
  `using var session` line is the pre-9 shape; the async shape wins
  (AGENTS.md version-pin note) and the rest of §2.2 is matched verbatim.

## Entry reads (done)

| Read | Why |
|------|-----|
| design doc §2.2 + §2.7 + invariants | the exact type shapes to implement |
| handoff `## U1` (scratch tier) | `IMediaFileStore` + `MediaOptions` registrations to compose |
| `src/Kumunita.Core/M3DocTypes.cs` | `Configure(StoreOptions)` surface shape to mirror |
| `src/Kumunita.Core/DependencyInjection.cs` | where `IMediaStore` registers |
| `src/Kumunita.Web/Program.cs` (L44–88) | the `MediaDocTypes.Configure(opts)` call-site + the `Media__*` binding |
| `Kumunita.Core.csproj` | Marten 9.31.2 → the async-session idiom choice |

## Steps (each leaves the tree in a buildable state)

1. `src/Kumunita.Core/Media/MediaObject.cs` — the §2.2 POCO verbatim
   (`Id / Filename? / ContentType / SizeBytes / Created / CreatedById?`).
2. `src/Kumunita.Core/Media/IMediaStore.cs` — the §2.2 seam verbatim
   (`PutAsync` / `GetAsync` / `OpenReadAsync` / `RemoveAsync`).
3. `src/Kumunita.Core/Media/LocalVolumeMediaStore.cs` — §2.2 verbatim with
   the Marten 9 `await using` session idiom (see Constraints).
4. `src/Kumunita.Core/MediaDocTypes.cs` — `opts.Schema.For<MediaObject>();`.
5. edit `src/Kumunita.Core/DependencyInjection.cs` —
   `services.AddTransient<IMediaStore, LocalVolumeMediaStore>();`.
6. edit `src/Kumunita.Web/Program.cs` — `MediaDocTypes.Configure(opts);` after
   `M3DocTypes.Configure(opts);` + confirm
   `builder.Services.Configure<MediaOptions>(… GetSection(MediaOptions.SectionName))`
   (the `Media__*` binding).
7. **Exit:** `run_build` green on `Kumunita.slnx` (covers `Kumunita.Core` **and**
   `Kumunita.Web`); confirm the `## U2` handoff section is present and accurate;
   flip the Status-table row to done.

## Verification

- Build via `run_build` (the tool runs `dotnet build`).
- No tests authored in U2 (§2.5's byte-store/media-store tests are U3's).
- Re-run the fast suite (`Kumunita.Web.Tests.dll` via the AGENTS.md
  `dotnet exec` path — **not** `dotnet test`) as the no-regression proof; the
  heavy `Kumunita.Core.Tests` round was already run in U1's pass and is
  re-run only in U10 (plan §2.6) unless this pass touches behavior.
- Confirm no `System.Web`/`IFormFile`/HTTP type crept into the four new files
  (ADR 0006-D) and that `MediaObject` is not referenced by any EF model.
