# U10 exec plan — governance + close (work log)

## Context snapshot (entry reads done 2026-09-11)

- Design doc: invariants C-MED·1–8 + FACES (6 serving + U7a/b/c) + §2.6 gate
  (`dotnet exec` path, recorded counts required) + §2.7 rule 6 (ADR 0011 must
  not contradict the doc; if it does, the ADR is corrected).
- Handoff U1–U9: U1 `MediaOptions`/`IMediaFileStore`/`LocalVolumeFileStore`;
  U2 `MediaObject`/`IMediaStore`/`LocalVolumeMediaStore`/`MediaDocTypes` +
  `Program.cs` wiring; U3 7 Core tests (220/220); U4 `Profile.AvatarId` +
  `SetProfileAvatarAsync` (doc-wins: `KeyNotFoundException`; `actorBy`
  accepted-not-persisted); U5 3 lane tests (223/223); U6 `AVATARUPLOAD`
  (`POST /profile/avatar`, guards 400/413/415,
  `[ValidateAntiForgeryToken]`, store-first → set-profile); U7 serving
  `Avatar` (FACES M1–M6, `nosniff`, frozen `CanAsync(…Read…)`); U8 form +
  `<img src="/profile/avatar/{subject}">` + `.avatar-mono` onerror fallback;
  U9 10 Web tests (70/70).
- MediaOptions defaults (verified in code): `RootPath` =
  `{AppContext.BaseDirectory}/media`; `MaxBytes` = 5 MiB;
  `AllowedContentTypes` default `image/jpeg,image/png,image/webp,image/gif`.
- Dev compose mounts only Postgres + `kumunita_dpkeys`; Dockerfile
  `VOLUME /data/dataprotection-keys`. **No media volume is defined yet** —
  the OPS write-up must say production binds `Media__RootPath` to an
  operator-provided persistent mount (see findings below).
- ADR shape (0004/0010 precedent): Status / Date / (Amends) / Context /
  Decision / Consequences / (Revisit when). ADR index is 0011.

## Planned touch set (exactly the register Deliverables)

1. `docs/adr/0011-media-and-file-storage.md` — **new**.
2. `docs/adr/README.md` — the 0011 row.
3. `docs/SECURITY.md` — row **(e)** in the §3 data-class table + §5 control
   rows for the C-MED controls.
4. `docs/OPS.md` — `Media__*` rows in the config reference + the second
   restore surface in §4/§5.
5. `docs/ARCHITECTURE.md` — `Media/` in the §2 solution tree + a §5
   note (the `MediaObject` doc + `MediaDocTypes` surface + one-DB+1-volume).
6. `README.md` — Status note + the media feature bullet (follow-on lanes
   named).
7. `docs/design/media-file-storage-design.md` — append `## Media — Closed
   (recorded)` after §2.7.
8. `docs/plans-milestones/done/media-file-storage-handoff-notes.md`
   — flip U10's Status row to done + append `## U10` + `## Summary`.

## §2.6 gate (run before writing the close records)

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

Expected (per U3/U5/U9 handoff baselines): Web ≥ 70 (60 baseline + U9's 10);
Core ≥ 223 (213 + U3's 7 + U5's 3). Record actual counts verbatim.

**Actual (2026-09-11, ran first, before writing any doc):**
`dotnet build Kumunita.slnx -c Debug` → **Build succeeded — 0 Warning(s), 0 Error(s)**
(10.07 s). `Kumunita.Web.Tests` → **Total: 70, Errors: 0, Failed: 0,
Skipped: 0, Not Run: 0** (0.799 s) = U9's 70/70, zero drift.
`Kumunita.Core.Tests` → **Total: 223, Errors: 0, Failed: 0, Skipped: 0,
Not Run: 0** (27.061 s, Testcontainers) = U5's 223/223, zero drift.

## Findings to surface (not fixed — U10 is no-code)

Recorded in the `## U10` / `## Summary` sections as open items:

1. **`onerror` inline handler (U8) vs SECURITY.md §6 CSP discipline**
   (no inline `on*` attributes). Shipped U8 code uses
   `onerror="…style.display=…"`. Either the fallback needs a `client/*.ts`
   module (the documented pattern) or a scoped decision; out of U10's
   file set — flagged for the operator/human.
2. **Production media mount is undefined infra.** `MediaOptions.RootPath`
   defaults inside the image's base directory; dev compose has no media
   volume and the Dockerfile has no `VOLUME` for it. The OPS write-up
   documents what we *do* have (the `Media__*` keys) and pins the
   operator step: mount a named volume at `Media__RootPath`; a bare
   container would lose uploads on redeploy. Compose/Dockerfile change
   itself is a code change → not in U10.
3. **Stale ADR range "0001–0009"** in `README.md` §Documentation and
   `ARCHITECTURE.md` §2 tree (already missing 0010 before this feature).
   U10's legitimate pass through both files fixes it to 0011 (doc drift,
   not code).
4. **OPS.md §3 names an `mt.migrations` ledger** — ADR 0004 §B states the
   Weasel-feature pattern intentionally has none (delta detection).
   Pre-existing doc drift, not media's; flagged only.

## Deviations log (append as they happen)

- (none yet)
