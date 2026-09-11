# Media follow-on (U11) — close the open findings

Owner: the follow-up unit for the Media & file-storage feature (2026-09-11).
Input: `## Summary` of `media-file-storage-handoff-notes.md` — open findings
1 (U8 inline `onerror` vs SECURITY.md §6), 2 (prod media volume not wired in
`compose`/`Dockerfile`), 4 (OPS.md §3 `mt.migrations` claim vs ADR 0004 §B) —
plus the `docs/COOLIFY.md` check the handoff implies. C-MED invariants and
ADR 0011 hold as-is: no change to the raster allowlist, the serving endpoint,
the `IMediaStore` seam, or the audience model.

Baselines to re-run verbatim at close (from the U10 gate):

- `dotnet build Kumunita.slnx -c Debug` — 0 Warning(s), 0 Error(s)
- Web: `Total: 70, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`
- Core: `Total: 223, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`

No C# code changes are planned, so the expected close counts are the
baselines verbatim (explain any deviation if one appears).

## D1 — inline `onerror` → `client/*.ts` lane (SECURITY.md §6 wins)

SECURITY.md §6 pins "Razor views: no inline `on*` attributes — use the
`client/*.ts` modules with `addEventListener` (ARCHITECTURE.md §7)". The
`.ts` lane is preferred over a recorded exception:

- **New** `src/Kumunita.Web/client/lib/avatar.ts`: scans
  `img[data-avatar-fallback]`, attaches an `error` listener that hides the
  `<img>` and reveals its hidden `.avatar-mono` sibling (the exact shape the
  inline handler had), plus a `complete && naturalWidth === 0` check for the
  case the 404 already resolved before the deferred module runs.
- 4 U8 views (`Views/Profile/Edit.cshtml`, `Views/Profile/Preview.cshtml`,
  `Views/Directory/Index.cshtml`, `Views/Directory/Detail.cshtml`): drop the
  `onerror="…"` attribute, add `data-avatar-fallback` to each avatar `<img>`;
  everything else in the markup (`.avatar-mono` sibling, `style="display:none;"`)
  stays.
- `_Layout.cshtml`: load `~/js/lib/avatar.js` as an ES module after the
  bootstrap script (the ARCH §7 pattern); global because the fallback is a
  lib-level concern and future avatar surfaces shouldn't each re-wire it.
- `wwwroot/css/site.css`: the U8 comment block (line ~681) names the
  `onerror` — reword to the module.
- No SECURITY.md change: the inline attribute disappears, so no exception
  need ever is recorded.

## D2 — production media volume, `kumunita_dpkeys` precedent

The `kumunita_dpkeys` wiring is: `Dockerfile` `VOLUME /data/dataprotection-keys`
(floor comment) + compose `DataProtection__KeysDirectory: "/data/dataprotection-keys"`
+ `- kumunita_dpkeys:/data/dataprotection-keys` + named volume
`kumunita_dpkeys`. Mirror exactly:

- **`Dockerfile`**: add `VOLUME /data/media` with the same
  floor-not-replacement comment style (the default `MediaOptions.RootPath`
  `{BaseDirectory}/media` = `/app/media` inside the image layer; ADR 0011 /
  OPS §4–5 second restore surface).
- **`docker-compose.yml`**: `Media__RootPath: "/data/media"` on the app
  service (the path OPS.md's config row and ADR 0011 name for prod mounts),
  `- kumunita_media:/data/media` in the app `volumes:` list, `kumunita_media:`
  in the top-level `volumes:` block.
- `LocalVolumeFileStore` already runs `Directory.CreateDirectory(RootPath)`
  idempotently, so a fresh empty named volume works out of the box (same as
  the keyring surface).
- **Verification:** `docker compose up` → upload avatar → `docker compose
  up --build` → the bytes at `kumunita_media` survive (only attempted if
  Docker is usable on this box; otherwise recorded as a documented manual
  step, flagged in the report).

## D3 — OPS.md §3 `mt.migrations` drift (the ADR wins)

Truth confirmed in shipped code: `Program.cs` `AddMarten` registers Weasel
storage features (`opts.Storage.Add<KumunitaFeature>` /
`AuthorizationFeature`, `M1DocTypes` / `M3DocTypes` / `MediaDocTypes`) and
`Bootstrap/SchemaBootstrap.cs:48` applies them via
`ApplyAllConfiguredChangesToDatabaseAsync()` — delta detection against the
live `mt` catalog, no ledger. `StoreOptions.Migrations` / `IMigration` /
`mt.migrations` appear nowhere in `src/`. So **ADR 0004 §B is truth** and
OPS.md §3 step 4 is stale. Fix: reword the Marten clause of that step to the
Weasel-feature / delta-detection description (keeping the forward-only,
in-image, superset sentence and the Identity `__EFMigrationsHistory` clause),
and cite ADR 0004 §B. ADR 0004 itself: no change.

## D4 — COOLIFY.md production surface (gap confirmed)

`COOLIFY.md`'s §5 env table ends at `DataProtection__KeysDirectory`; §5.2
covers only the keyring. Coolify **can** express a named volume on the app
container (its own §5.2 documents the mechanism on the keyring path), so a
named volume at `/data/media` plus `Media__RootPath` is the faithful fix:

- §5 env table: `Media__RootPath` row (mirrors the keyring row's phrasing;
  omit = in-image default `/app/media` ⇒ uploads lost on redeploy).
- New **§5.2A — Media volume persistence (required in production)** after
  §5.2 (two steps: Coolify named volume at `/data/media` + `Media__RootPath`
  env; verify via upload-survives-redeploy + `docker inspect`; backup
  alignment points to OPS §4/§5's second restore surface).
- Troubleshooting table: one row — all avatars 404 / monograms returned
  after a redeploy ⇒ media volume not attached or env drift.
- No change made where it would contradict OPS.md: OPS already requires an
  operator-provided volume at `Media__RootPath` — §5.2A names the path
  `/data/media` identically. A host-path bind is *not* the pattern for this
  topology (the dpkeys precedent is the authoritative one); it is mentioned
  only as a fallback if a host operator refuses named volumes.

## Doc sync (per house rules)

- `media-file-storage-handoff-notes.md`: addendum pointer under `## Summary`
  (findings 1/2/4 now closed) + `## U11` section appended (dispositions,
  gate counts — filled in after the gate runs).
- Check ADR 0011 + the design doc's close section for lines that now
  contradict (expected: none — the three findings were open items recorded
  in the handoff, not ADR/design-doc claims; the "follow-on lane" list
  stands).

## Gate (record verbatim in the exec plan)

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

Module-compile proof (already run, passed): `dotnet build` drives tsc via
`Microsoft.TypeScript.MSBuild` (Kumunita.Web.csproj) + the project-root
`tsconfig.json` — `client/lib/avatar.ts` → `wwwroot/js/lib/avatar.js`
(`wwwroot/js/` is gitignored build output), and `dotnet publish` was
verified to carry the compiled module into the publish output
(`wwwroot/js/lib/avatar.js` + the U8 `site.js` lane) — the same path the
Dockerfile's `dotnet publish` uses, so the module ships in the image.
(Note: this tree has no `npm` build — the tsc lane is MSBuild-driven.)
### Recorded (this unit, after the gate ran)
- dotnet build Kumunita.slnx -c Debug — **0 Warning(s), 0 Error(s)**
- Web: Total: 70, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0 (unchanged from the recorded baseline — U11 adds no new tests)
- Core: Total: 223, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0 (unchanged from the recorded baseline)
- D2 live survival check ran for real: signup → login → POST /profile/avatar (70 B PNG) → GET /profile/avatar/{id} = 200 image/png, SHA-256 35227EEA…DAC7D, stored content-addressed on the kumunita_kumunita_media volume (/data/media/35/35227…); then docker compose up --force-recreate -d (app + db + mailpit all recreated) → re-login → **byte-identical avatar served back** from the same volume.
- D4 shipped as planned (COOLIFY.md gained the Media__RootPath env row, the §5.2A media-volume step mirroring the §5.2 dpkeys mechanism, and the troubleshooting row; backup/restore aligned with OPS.md §4/§5 — no OPS/COOLIFY contradiction remains).
