# U2 — Core module: `MediaObject` + `IMediaStore` + `LocalVolumeMediaStore` + `MediaDocTypes`

> Self-contained: this file + the **entry reads** below is the whole context.
> The exact C# is `docs/design/media-file-storage-design.md` §2.2 (the
> `MediaObject` / `IMediaStore` / `LocalVolumeMediaStore` / `MediaDocTypes`
> shapes) + §2.7 rules 5/6. A prior handoff section (if any) is **U1**.

## Understanding

Add the **store seam** over the byte seam: the `IMediaStore` module seam, its
`LocalVolumeMediaStore` implementation (composes `IMediaFileStore` +
`IDocumentStore`), the `MediaObject` `mt` catalog doc, and the
`MediaDocTypes` Marten registration surface. This is the **single decision
path** (C-MED·1) the whole feature serves through — the Web layer never
touches the disk (C-MED·6).

## Assumptions

- `MediaObject.Id` **is** the lowercase-hex SHA-256 of the payload (C-MED·4);
  dedup is `LoadAsync(id) → exists? return it : store`.
- `MediaObject` is a **Marten doc in `mt`** (C-MED·7) — NOT EF (ADR 0004 §B).
- `Kumunita.Core` stays **HTTP-free**: `Put` takes `byte[]` + `filename` +
  `contentType` + `actorId`, **not** `IFormFile`/`Stream` (C-MED·6).
- `MediaDocTypes` is a **new doc-type surface** (ADR 0004 §B.1), mirroring
  `M1DocTypes` / `M3DocTypes` — its own `Configure(StoreOptions)` extension.

## Approach

Add the `MediaObject` POCO (C-MED·7), the `IMediaStore` seam + its
`LocalVolumeMediaStore` (composes `IMediaFileStore` + `IDocumentStore`,
dedup-by-id, `Put` = compute hash → load → return-or-store), and
`MediaDocTypes.Configure(StoreOptions opts) => opts.Schema.For<MediaObject>()`.
Register `IMediaStore` in `AddKumunitaCore()` and call
`MediaDocTypes.Configure(opts)` in `Program.cs` next to the existing
`M1DocTypes` / `M3DocTypes` lines, binding `Media__*` (mirror the
`CommunityOptions` binding).

## Key files (entry reads — no more)

- `docs/design/media-file-storage-design.md` — §2.2 (`MediaObject`,
  `IMediaStore`, `LocalVolumeMediaStore`, `MediaDocTypes`) + §2.7 rules 5/6.
- `docs/plans-milestones/in-progress/media-u1-plan.md` + the **U1** handoff
  (the `IMediaFileStore` + `MediaOptions` to compose).
- `src/Kumunita.Core/M3DocTypes.cs` — the `MediaDocTypes.Configure(StoreOptions)`
  shape to mirror.
- `src/Kumunita.Core/DependencyInjection.cs` — where to add `IMediaStore`.
- `src/Kumunita.Web/Program.cs` (L52–93) — where `MediaDocTypes.Configure(opts)`
  is called + the `Media__*` binding.

## Deliverables (3 new files + 2 edits)

- `src/Kumunita.Core/Media/MediaObject.cs`
- `src/Kumunita.Core/Media/IMediaStore.cs`
- `src/Kumunita.Core/Media/LocalVolumeMediaStore.cs`
- `src/Kumunita.Core/MediaDocTypes.cs`
- edit `src/Kumunita.Core/DependencyInjection.cs` —
  `services.AddTransient<IMediaStore, LocalVolumeMediaStore>()`.
- edit `src/Kumunita.Web/Program.cs` — `MediaDocTypes.Configure(opts)` next to
  `M1DocTypes.Configure(opts)`, and confirm
  `Services.Configure<MediaOptions>(configuration.GetSection("Media"))`.

## Risks & open questions

- **Dedup correctness:** the `Put` path must be `hash → LoadAsync → return-or-store`;
  a "store then check" order races two writers (violates C-MED·4).
- **`MediaDocTypes` surface:** must be registered in `Program.cs` or the
  `MediaObject` doc is invisible to Marten (a C-MED·7 drift). The unit must
  add the `MediaDocTypes.Configure(opts)` call — a unit that skips it leaves
  the doc unregistered.
- `LocalVolumeMediaStore` ctor: it takes `IMediaFileStore` + `IDocumentStore`.
  Register it transient; both deps are already singletons/transients in Core's
  DI graph (verify `IDocumentStore` is resolved — it is the Marten `IDocumentStore`).

## Steps

1. Add `MediaObject.cs` (C-MED·7 fields).
2. Add `IMediaStore.cs` (the seam: `Put/Get/OpenRead/Remove`).
3. Add `LocalVolumeMediaStore.cs` (composes U1's `IMediaFileStore` +
   `IDocumentStore`; dedup-by-id `Put`).
4. Add `MediaDocTypes.cs` (`opts.Schema.For<MediaObject>()`).
5. Register `IMediaStore` in `DependencyInjection.cs`.
6. In `Program.cs`, call `MediaDocTypes.Configure(opts)` next to the existing
   `M1DocTypes` / `M3DocTypes` lines; confirm the `Media__*` options binding.
7. `run_build` → green on **both** `Kumunita.Core` and `Kumunita.Web`.
8. Append **`## U2`** to the handoff notes: the `MediaDocTypes` call-site line,
   the DI registrations, and the `Media__*` binding confirmed.
