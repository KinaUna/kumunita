# M25 — Upload limits · rolling handoff notes

> Every unit appends a short `## U#` section before it moves its own plan to
> `done/`. This is the cross-unit memory: read the `## U#` sections before
> yours, append yours after. Do not re-derive what an earlier unit already
> settled (the invariant ids/names, the FACES F1–F10, the seam signatures, the
> pinned test names). This file is created by U1 and moved to `done/m25/` by
> the close unit (U12).

## U1 — design doc Part 1 (authored 2026-10-03, after M24 close)

**Outcome: U1 complete — Part 1 authored.** M24's close unit landed (M24 →
`StatusDone`, M25 → `StatusNext` in `Milestones.cs`), so Step 0's C-UP·6
precondition **passes** — this replaces the earlier `## U1 — Drift pause`
section (the BLOCKED record above; the blocker no longer holds). Authored
`docs/design/m25-upload-limits-design.md` Part 1: Context (what/why + the
named non-decisions), Scope (in/out), the **7 invariants** C-UP·1–7 (GlobalAdmin
single-doc settings + env fallback · guards-before-write · Core stays HTTP-free ·
content-addressed usage `Σ SizeBytes WHERE CreatedById` · sentinel `0`=unlimited ·
C-UP·6 single-in-progress contract · no new authorization surface), and the **10
FACES** F1–F10 (each bound to its invariant). No code, no build.

**C-UP·6 precondition outcome:** M24 `StatusDone` / M25 `StatusNext` confirmed
(read verbatim from `src/Kumunita.Web/Milestones.cs`, lines 66–67).

**Next unit:** `m25-u02.md` (Seams & contracts, design doc Part 2).

## U2 — design doc Part 2 (authored 2026-10-03)

**Outcome: U2 complete — Part 2 appended** to
`docs/design/m25-upload-limits-design.md` as `## Seams & contracts (Part 2,
written by U2)` with §2.1–§2.6. Builds on U1's Part 1 (reuses the frozen
C-UP·1–7 / F1–F10; no re-derivation). No code, no build.

- **(a) Sealed Core seams:** `CommunityStorageSettings` (Marten doc, `Id =
  "community"`, `MaxFileBytes?` / `PerUserQuotaBytes` / `Modified` /
  `ModifiedById`); `IStorageSettingsService` 4 methods —
  `GetOrCreateAsync`, `SetAsync`, `GetPerUserUsageBytesAsync`, `Decide`;
  `StorageLimits.Decide` (size-first, pure); `StorageSettingsDocTypes.Configure`.
- **(b) `IUploadGate`** (Web-only): `CheckUpload` → `ActionResult?`, distinct
  oversize/over-quota 413.
- **(c) 23 pinned test names by id:** Core §2.3 (1–10,
  `tests/Kumunita.Core.Tests/Usage/StorageLimitsTests.cs`); Web §2.4 (11–23,
  per-lane + `UploadGateTests` / `AdminStorageControllerTests` /
  `ResidentUsageViewTests`).
- **(d) Four-lane adoption rule** (§2.2) — the 4 pinned call-sites:
  `ProfileController.AvatarUpload` (L413), `ContentImageController.Upload`
  (L162), `AttachmentController.Upload` (L69), `DocumentController.Upload`
  (L302).
- **(e) C-UP·6 contract** (§2.6 drift-guard): M24 `StatusDone` / M25
  `StatusNext` at start; M25 `StatusDone` + M26 `StatusNext` at close.

**Drift flags recorded in §2.2/§2.6 (for U3+, not blockers now):**
- M24's `IStorageMetricsService.GetPerUserUsageBytesAsync` (the C-SM·7 handoff
  seam) **already exists** — M25's `IStorageSettingsService.
  GetPerUserUsageBytesAsync` **reuses/re-points to** it (named in the §2.6
  drift-guard); do not re-implement.
- `DocumentController` has a **second** inline size guard on the **edit** lane
  (L213, `form.File`) that is *not* in the pinned test list — U8 must decide
  explicitly whether it adopts the gate (else an F10 / test-list drift event).

**Next unit:** `m25-u03.md` (Core doc + doc-surface + boot-path wiring) —
**do not start it** unless instructed.

## U3 — Drift pause (2026-10-03)

**Outcome: U3 NOT executed — Drift pause recorded; no code authored, no build run.**
The guardrail's *named* BLOCKED triggers are **not** met: Part 2 exists (U2
appended §2.1–§2.6) and the C-UP·6 precondition holds (M24 `StatusDone` /
M25 `StatusNext`). But the unit's **deliverable #3 contradicts the codebase**,
so per the unit rule ("if reality contradicts the plan, record `## U3 — Drift
pause` and stop") U3 stops here rather than guessing around it.

- **Deliverable #3's premise is false.** `m25-u03.md` (entry read #3 +
  deliverable #3) says `SchemaBootstrap.cs` is "where the existing
  `*.Configure(opts)` doc-surface calls live (add the new one next to them)."
  The **entire** `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (164 lines)
  contains **no** `*.Configure(opts)` doc-surface call — it only calls
  `store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync()`. Its own
  doc-comment says "Document-shape auto-creation remains the dev-only loop in
  `Program.cs` (ADR 0004)." It is a generic applier, not a registration site.
- **Reality: doc-surface registration lives only in `Program.cs`.** All 13
  existing surfaces — M1 (L107), M3 (L113), Media (L119), Page (L125), Tag
  (L131), M4 (L140), M5 (L149), M6 (L158), M9 (L168), M16 (L180), M17 (L190),
  `UsageDocTypes` (L198), `DocumentDocTypes` (L208) — are registered inside
  `builder.Services.AddMarten(opts => { … })` in `src/Kumunita.Web/Program.cs`.
  Corroborated by the tags / M13 / M21 handoff notes ("1 line in `Program.cs`;
  `SchemaBootstrap.cs` **not** modified").
- **Consequence:** U3's Exit expects "the two boot-path lines added (file +
  line numbers)," but only **one** is addable (`Program.cs`). The correct
  single location is unambiguous (13 precedents + 3 handoff notes), yet the
  plan as written names an infeasible second site — a plan↔reality
  contradiction on a stated deliverable.
- **Fix before U3 proceeds (one-line plan edit, then re-run U3):** re-target
  deliverable #3 (and entry read #3) to **`Program.cs` only** — add
  `StorageSettingsDocTypes.Configure(opts);` next to
  `DocumentDocTypes.Configure(opts);` (L208) — and change the Exit's "two
  boot-path lines" to "one." Then U3 = 2 Core files
  (`Usage/CommunityStorageSettings.cs` + `Usage/StorageSettingsDocTypes.cs`)
  + 1 `Program.cs` line + build green, mirroring M13 (`UsageDocTypes`) / M21
  (`DocumentDocTypes`) exactly.

**Next unit (after the plan fix):** `m25-u04.md` (`IStorageSettingsService` +
pure `StorageLimits.Decide` + 10 pinned Core tests) — **not started.**

## U3 — doc + registration surface + boot (authored 2026-10-03, after the plan fix)

**Outcome: U3 complete — build green.** Step 0 applied first: `m25-u03.md`'s
deliverable #3 + entry read #3 re-targeted to **`Program.cs` only** (the single
doc-surface registration site), and the Exit's "two boot-path lines" → "one".
Then the (now-corrected) unit was executed — **2 Core files + 1 `Program.cs`
line, mirroring M13 (`UsageDocTypes`) / M21 (`DocumentDocTypes`) exactly.** No
service, no test added (U4 does that); `IMediaStore` / `MediaObject` /
`MediaOptions` untouched.

- **(a) `StorageSettingsDocTypes` line:** `opts.Schema.For<CommunityStorageSettings>();`
  (`src/Kumunita.Core/Usage/StorageSettingsDocTypes.cs`; POCO at
  `src/Kumunita.Core/Usage/CommunityStorageSettings.cs`, both
  `namespace Kumunita.Core.Usage`, exact §2.1 shape: `Id = "community"`,
  `long? MaxFileBytes`, `long PerUserQuotaBytes`, `DateTimeOffset Modified`,
  `string? ModifiedById`).
- **(b) the single boot-path line added:**
  `src/Kumunita.Web/Program.cs` **L216** `StorageSettingsDocTypes.Configure(opts);`,
  directly after `DocumentDocTypes.Configure(opts);` (L208), inside
  `AddMarten(opts => { … })` — the 14th surface.
- **(c) compile warnings on the new POCO:** **none** — `dotnet build` on
  `Kumunita.Core` (9 pre-existing warnings, all in `SmtpSender.cs` /
  `SampleDataSeeder.cs`, none on the new files) + `Kumunita.Web` both
  **succeeded**.

**Next unit:** `m25-u04.md` (`IStorageSettingsService` + pure
`StorageLimits.Decide` + the 10 pinned Core tests) — **not started.**

## U4 — service + decision + Core tests (authored 2026-10-03)

**Outcome: U4 complete — build green + the 10 Core tests pass.** Executed the
(now-corrected) unit — **3 Core files + 1 test file + 1 DI line**, mirroring the
M24 `IStorageMetricsService` / `StorageMetricsService` pair exactly. Core stays
HTTP-free (C-UP·3 / ADR 0006-D): no `IFormFile` / `Stream` / `ActionResult` in
any new file. `IMediaStore` / `MediaObject` / `MediaOptions` untouched. No Web
gate or Web test added (U8 does that).

- **(a) The 10 Core test names (by id) that passed** (design §2.3, items 1–10,
  all in `tests/Kumunita.Core.Tests/Usage/StorageLimitsTests.cs`):
  `Decide_Oversize_Rejects` (F2) · `Decide_OverQuota_Rejects` (F3) ·
  `Decide_WithinBoth_Allows` (F1) · `Decide_QuotaZero_Unlimited` (F5) ·
  `EffectiveMaxFileBytes_AdminOverrideBeatsEnv` (F4) ·
  `EffectiveMaxFileBytes_UnsetFallsBackToEnv` (F6) ·
  `GetPerUserUsage_SumsCreatedByIdOnly` (F7/F8) ·
  `GetPerUserUsage_OtherUsersExcluded` (F8) ·
  `SetAsync_PersistsInCallerSession` (C-UP·1) ·
  `Decide_SizeCheckedBeforeQuota` (C-UP·2 ordering). The pure `Decide` /
  `EffectiveMaxFileBytes` tests (1–6, 10) are no-DB; the 3 service tests
  (7–9) use the `PostgresFixture` + `MediaDocTypes` +
  `StorageSettingsDocTypes` scratch-schema harness (the `StorageMetricsTests`
  shape). Whole Core suite: **Total 1150, Errors 0, Failed 0, Skipped 0**.
- **(b) `GetPerUserUsageBytesAsync` read shape (the `Where` / `Sum`):** the
  service **reuses** the M24 C-SM·7 seam —
  `IStorageMetricsService.GetPerUserUsageBytesAsync` →
  `session.Query<MediaObject>().Where(o => o.CreatedById == subjectId)`
  then `rows.Sum(o => o.SizeBytes)` — not a second copy of the query
  (design §2.6 drift-guard). The service ctor takes the `IDocumentStore` + the
  `IStorageMetricsService`; `GetOrCreateAsync` is read-then-create-if-missing
  (sentinel defaults `MaxFileBytes = null` / `PerUserQuotaBytes = 0`);
  `SetAsync` is one in-caller-session `Store` + `SaveChanges`.
- **(c) `Decide` ordering (size-before-quota confirmed):**
  `effectiveMax = settings.MaxFileBytes ?? envMaxBytes`;
  `if (effectiveMax > 0 && incoming > effectiveMax) → Oversize;` **then**
  `if (PerUserQuotaBytes > 0 && currentUsage + incoming > PerUserQuotaBytes)
  → OverQuota;` else `Allowed` — `Decide_SizeCheckedBeforeQuota` (test 10)
  proves an input over **both** reports `Oversize`, not `OverQuota`.
- **(d) the DI registration line** (in
  `src/Kumunita.Core/DependencyInjection.cs`, directly after the M24
  `IStorageMetricsService` line):
  `services.AddTransient<Usage.IStorageSettingsService>(sp => new
  Usage.StorageSettingsService(sp.GetRequiredService<Marten.IDocumentStore>(),
  sp.GetRequiredService<Usage.IStorageMetricsService>()));` — the C-SM·7 seam
  reuse (design §2.1 registration shape).

**Build:** `dotnet build Kumunita.slnx -c Debug` **succeeded** (5 warnings, all
pre-existing; none in the new files). Tests run via the reliable path —
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
(**not** `dotnet test`).

**Next unit:** `m25-u05.md` (GlobalAdmin `GET /admin/storage` view + nav link;
the set lane is U6) — **not started.**

## U5 — Drift pause (2026-10-03)

**Outcome: U5 NOT executed — Drift pause recorded; no code authored, no build
run.** The unit's guardrail BLOCKED triggers are **not** met (U4's
`IStorageSettingsService` + the build are green, the design seam is present),
but **deliverable #1's route is already owned by a shipped, distinct, ADR'd
surface** — a plan↔reality contradiction on a stated deliverable, so per the
unit rule ("if reality contradicts the plan, record `## U5 — Drift pause` and
stop") U5 stops rather than guessing around it. **No `AdminStorageController.cs`,
no `AdminStorageViewModel.cs`, no `Storage.cshtml`, no nav edit, no build** were
made. `IMediaStore` / `MediaObject` / `MediaOptions` / M24's metrics surface all
untouched.

- **The route `/admin/storage` is already taken by M24 (metrics, not
  settings).** `src/Kumunita.Web/Controllers/AdminStorageMetricsController.cs`
  **L31** is `[Route("admin/storage")]` + a `GET Index` action rendering
  `Views/Admin/StorageMetrics.cshtml` — M24's `StatusDone`, ADR 0134,
  *read-only metrics* surface (total used / available / per-user table). U5's
  deliverable #1 is a **new** `AdminStorageController` at the **same**
  `GET /admin/storage` for a *different* thing (the settings display). Two
  attribute-route `GET` actions on one template throw `Ambiguous match found`
  at startup, so U5's own exit ("build green + `GET /admin/storage` renders")
  is unsatisfiable as written.
- **The nav card U5 would add already exists.**
  `src/Kumunita.Web/Views/Admin/Index.cshtml` **L121** already carries a
  "Storage" card → `href="/admin/storage"` (commented "M24 (storage metrics)").
  Deliverable #4 ("add the Storage nav link row") would create a **duplicate**
  pointing at M24's metrics page.
- **M24 anticipated this and deferred the decision to M25; M25 never resolved
  it.** `done/m24/m24-u05.md` L38–44 states the two surfaces are distinct
  (M24 = *metrics*, M25 = *settings*) and leaves routing to the M25 agent
  ("coexist on the same route **if M25's U5 extends M24's view**, or a distinct
  route like `/admin/storage/settings` — the M25 agent's call, not M24's"). The
  M25 design doc (§Scope) names `/admin/storage` for the *settings* surface as
  though the route were free, yet lists M24's metrics tables as **Out of scope
  (retained)** — so the route cannot be reassigned to M25.
- **U6's plan corroborates the false premise.** `m25-u06.md` names the POST
  lane `POST /admin/storage/limits` — a *sub-route of M24's template*. The whole
  M25 admin-surface plan (U5+U6) presupposes M24 is **not** on `admin/storage`;
  in reality it is. (This is the same "reuse, don't re-implement" drift class as
  the M24 `IStorageMetricsService` seam the user's carry-forward flag named —
  except here the *route* is the reused artifact.)

**The open decision (for the plan author / next agent, not a U5 guess):**
- **(A) Distinct route — recommended, keeps M24 untouched.** Retarget U5/U6 to
  `GET/POST /admin/storage/settings` (or `/admin/storage-limits`): new
  `AdminStorageController`, `AdminStorageViewModel`, `Views/Admin/Storage.cshtml`,
  and a *new* "Storage settings" nav card (the existing "Storage" card stays the
  M24 metrics link). U6's `POST` then becomes `POST /admin/storage/settings`.
  This is the only option that keeps M24's ADR'd surface intact (M25 design
  §Scope lists it as retained).
- **(B) Coexist by extending M24's surface.** Fold the settings display + form
  into `AdminStorageMetricsController` + `StorageMetrics.cshtml` (the
  `done/m24/m24-u05.md` "extend M24's view" option). But this reshapes M24's
  shipped controller/view and contradicts M25's "M24's surface is out of scope"
  non-decision — a larger, less defensible change.

Either way it is an **architectural routing decision the plan never made**;
neither matches U5's literal deliverables (a fresh controller + fresh nav card
at the same already-claimed route). Per the unit rule, U5 records the drift and
stops.

**Next unit:** **none started.** `m25-u06.md` is the *next* unit **only after**
the routing decision above is resolved (A vs B) and `m25-u05.md`/`m25-u06.md` +
the design §Scope route are corrected to match. **Do not start U6** — it
inherits the same `/admin/storage` premise and will hit the identical
ambiguity on its `POST /admin/storage/limits`.

## U5 — admin GET surface (authored 2026-10-03, routing resolved → option A)

**Outcome: U5 complete — build green + live-rendered under a GlobalAdmin session.**
The `## U5 — Drift pause`'s **open decision (A vs B) is resolved: option A
(distinct route, keeps M24 untouched)** was executed. The prior U5 drift was a
plan↔reality contradiction on the *route* (M24's ADR'd metrics surface already
owns `GET /admin/storage`); this run chose a **different** route so the two
coexist, leaving M24's `AdminStorageMetricsController` + `StorageMetrics.cshtml`
**byte-for-byte untouched**. `IMediaStore` / `MediaObject` / `MediaOptions`
unchanged; Core stays HTTP-free (C-UP·3 / ADR 0006-D) — all changes are Web-only.

**The routing decision (A) + why, recorded for U6 to inherit:**
- **M25's settings surface = `GET /admin/storage/settings`** (new
  `AdminStorageController`); U6's set-lane becomes **`POST /admin/storage/settings`**
  (not the `POST /admin/storage/limits` that `m25-u06.md` currently names —
  **U6 must re-target to `/admin/storage/settings`** to keep one controller, one
  route, one view, and to stay off M24's `admin/storage` template).
- **Why A over B:** (a) it honors the design §Scope non-decision "M24's metrics
  are out of scope (retained)" — B would reshape M24's shipped controller/view;
  (b) it is the option `done/m24/m24-u05.md` (L38–44) explicitly deferred to the
  M25 agent and recommended as the route that keeps M24's ADR 0134 surface intact;
  (c) the `/admin` hub's "Storage" card (L121) already points at M24's metrics
  page, so A adds a *new* "Storage settings" card rather than repurposing the
  existing one (B would have made the two cards both mean "metrics").
- **Route collision is genuinely avoided (not just assumed):** two attribute-route
  `GET` actions on one template throw `Ambiguous match found` at startup; here the
  templates differ (`admin/storage` vs `admin/storage/settings`) and the app
  **boots cleanly** with both registered — verified by `docker compose up -d app`
  reaching a 200 on `/`.

**(a) Route + view model shape:** `AdminStorageController` at
`[Route("admin/storage/settings")]` + `[Authorize(Roles = GlobalAdmin)]` (C-UP·1).
Single `GET Index` (read-only — no form/POST, the set-lane is U6) loads the
settings (`IStorageSettingsService.GetOrCreateAsync(CancellationToken.None)`, the
create-if-missing sentinel shape) + the community total, and renders
`Views/Admin/Storage.cshtml`. `AdminStorageViewModel`
(`src/Kumunita.Web/Models/AdminStorageViewModel.cs`) fields: `MaxFileBytes?`
(admin override, `null` = env fallback), `PerUserQuotaBytes` (`0` = unlimited),
`TotalUsedBytes`, `AsOf`, `EnvMaxFileBytes` (the `MediaOptions.MaxBytes` 5 MiB
default — the same cap the four upload-lane guards use), `EffectiveMaxFileBytes`
(`StorageLimits.EffectiveMaxFileBytes(settings, env)` = `MaxFileBytes ?? env`,
`0` = unlimited), `QuotaUnlimited` (`PerUserQuotaBytes == 0`).

**(b) Nav link added:** `Views/Admin/Index.cshtml` **L136** (the card; the label
at **L138**) — a new "Storage settings" card → `href="/admin/storage/settings"`,
inserted directly **after** the existing M24 "Storage" (metrics) card (L121–127),
mirroring the hub's section-card idiom and its "Storage"/"Announcement
comments"/"Direct messaging" neighbours. A back-link from the settings view to
`/admin/storage` makes the metrics↔settings relationship explicit.

**(c) Community-total read (drift-guard):** it **reuses M24's
`IStorageMetricsService.GetSnapshotAsync`** C-SM·2 seam — the same
`StorageMetricsSnapshot.TotalUsedBytes` (`Σ SizeBytes`) + `AsOf` shape M24's
controller reads — **not** a fresh `Query<MediaObject>().Sum(…)` (the U4/U5
"reuse the same seam" rule). The settings read and the snapshot read are started
concurrently and awaited explicitly (the `AdminStorageMetricsController.Index`
pattern; `Task.WhenAll` isn't usable across the two distinct result types).

**Exit evidence:** `dotnet build Kumunita.slnx -c Debug` **succeeded** (all
warnings pre-existing — `SmtpSender` / `SampleDataSeeder` / test-file `xUnit1051`;
none in the new files). Live-verified via `docker compose build app &&
docker compose up -d app`, signed in as the sample GlobalAdmin
(`admin@examplium.com`) in the integrated browser: **`GET /admin/storage/settings`
rendered** with effective per-file = **5.24 MiB**, admin override = **"Not set
(using platform default)"**, platform default (env) = **5.24 MiB**, per-user
quota = **"Unlimited"**, total used = **0 B**, and an as-of (UTC) timestamp —
matching the sentinel settings (`MaxFileBytes = null` ⇒ env `MediaOptions.MaxBytes`
5 MiB in force; `PerUserQuotaBytes = 0` ⇒ unlimited) and the seeded (empty) media
catalog. **No new test** (U6's `AdminStorageControllerTests` are the first
admin-surface tests). The `.tmp\verify-u5.ps1` scratch probe was used only to
isolate the defect below and is a gitignored throwaway.

**⚠ Pre-existing M24 defect found (NOT a U5 change — flagged for the plan
author, out of U5's scope):** the volume-stat read `LocalVolumeFileStore.Statvfs`
(`src/Kumunita.Core/Media/LocalVolumeFileStore.cs` L93–110) **crashes the whole
process** with a `System.AccessViolationException` (read/write protected memory)
on this Linux-in-Docker environment. It is **intermittent** (the settings page
rendered once, a later request crashed) and lives in **M24's shipped code** — the
**unmodified M24 metrics page `GET /admin/storage` reproduces the identical
crash** (`docker logs`: `System.AccessViolationException … at
Kumunita.Core.Media.LocalVolumeFileStore.Statvfs`), so it is not introduced by
U5. U5 only reaches it because the U4/U5 spec mandates reusing `GetSnapshotAsync`
(which calls `GetTotalSpaceBytesAsync`/`GetFreeSpaceBytesAsync` → `statvfs`).
Suspect root cause: the `[DllImport("libc", SetLastError = true)]` `statvfs`
P/Invoke with `[StructLayout(LayoutKind.Sequential)]` `statvfs_t` (10 × `ulong`)
— on the target glibc, `f_flag` is a 16-bit `unsigned short` followed by a 16-bit
`f_spare[2]` before `f_namemax`, so the struct is **not** 8 fields of 8 bytes and
the 80-byte buffer is mis-mapped (the `out` buffer is written with the wrong
size/alignment → AV). It is Linux-only (Windows uses `DriveInfo`), so it is
invisible in a pure-Windows `dotnet test` run — the `StorageMetricsTests` Core
harness likely stubs/avoids the real volume read. **Recommendation (a separate
lane, not U5):** fix the `statvfs_t` layout for the target glibc (add the `f_flag`
+ `f_spare` sizing) or, more portably, replace the `statvfs` P/Invoke with
`System.IO.OperatingSystem`-aware / `DriveInfo`-equivalent disk-usage reads that
work on both the Linux container and Windows. U5's own surface is otherwise
complete and correct; it is gated only by this M24 defect for full runtime
stability.

**Next unit:** `m25-u06.md` (the `POST /admin/storage/settings` set-lane + form +
the 2 pinned admin Web tests) — **adopt the option-A route recorded above
(`/admin/storage/settings`, not `/admin/storage/limits`)**. Not started.

## U6 — admin POST set-lane (authored 2026-10-03)

**Outcome: U6 complete — build green + the 2 pinned admin Web tests pass.**
Executed the unit on the **inherited option-A route** (the routing decision
resolved in the `## U5` section above). Reused U4's `IStorageSettingsService.
SetAsync` seam (no re-implementation); Core stays HTTP-free (C-UP·3 / ADR
0006-D) — all changes are Web-only + the test file. `Decide` / the four-lane
wiring untouched; M24's metrics surface (`AdminStorageMetricsController` @
`GET /admin/storage`) untouched.

- **(a) Route adopted (option A):** the set-lane is **`POST /admin/storage/
  settings`** — **not** the `POST /admin/storage/limits` that `m25-u06.md` still
  names. It shares U5's one controller (`AdminStorageController`,
  `[Route("admin/storage/settings")]`), one route, and one view
  (`Views/Admin/Storage.cshtml`), so the GET display and the POST set-lane
  live on the same surface (the option-A "one controller / one route / one
  view" shape recorded in `## U5`). The `Decide` / four-lane wiring is
  unchanged (C-UP·1 set-lane only).
- **(b) `POST` action + `SetAsync` call-site:** `AdminStorageController.Save(
  string? maxFileBytesInput, string? perUserQuotaBytesInput)`,
  `[HttpPost]` + `[ValidateAntiForgeryToken]`, added beside U5's `GET Index`
  (one new ctor dep: `Marten.IDocumentStore store` — the C3 session-owner,
  mirroring `CommunityController` / `TagController`). Value parsing (C-UP·5):
  blank per-file ⇒ **`null`** (env fallback — **not** coerced to `0`; a
  non-blank value incl. explicit `"0"` = unlimited file size is the override);
  blank-or-`"0"` quota ⇒ `0` (unlimited). The **`SetAsync` call-site**
  (`AdminStorageController.cs` L161):
  `await using var session = store.LightweightSession();` → `await
  settings.SetAsync(maxFileBytes, quota, actor, session);` — **one
  in-caller-session write** (C-UP·1 / C3 same-transaction lane; the controller
  owns the session, the service's `SaveChanges` is the single write). The
  subject `actor = KumunitaPrincipal.SubjectId(User)` (server-side, never a
  path param).
- **(c) 2 test names that passed** (design §2.4, items 20–21, in
  `tests/Kumunita.Web.Tests/AdminStorageControllerTests.cs`):
  **`AdminStorage_SetLimits_Persists`** (item 20 — a GlobalAdmin POST sets both
  values; the `SetAsync` seam is called with `SetAsync(12345, 67890, Admin,
  session)` — the right values, the server-minted actor, and the
  **caller's** session — and the action returns a
  `RedirectToActionResult` to `Index`; a supporting sentinel-branch pin
  `AdminStorage_SetLimits_BlankPerFile_IsNull` also passes, nailing the
  blank-per-file ⇒ `null` + quota-`0` ⇒ unlimited arm on the same seam) and
  **`AdminStorage_NonGlobalAdmin_Forbidden`** (item 21 — the
  `[Authorize(Roles = GlobalAdmin)]` role gate is asserted exactly, and a
  Member principal is shown not to carry `GlobalAdmin`). **Whole Web suite:
  Total 771, Errors 0, Failed 0, Skipped 0** (run via the reliable path —
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.
  dll`, **not** `dotnet test`).
- **(d) Redirect target:** `RedirectToAction(nameof(Index))` — back to
  `GET /admin/storage/settings` (the U5 read view; the
  `AdminMessagingController.Save` / `AdminTimezoneController.Save`
  redirect-after-save shape). On success `TempData["info"] = "Upload limits
  saved."`.
- **(e) Blank-per-file ⇒ `null` handling:** in `Save`,
  `string.IsNullOrWhiteSpace(maxFileBytesInput)` ⇒ `maxFileBytes = null`
  (the env `MediaOptions.MaxBytes` 5 MiB fallback is in force — **not**
  coerced to `0`, which would mean "unlimited file size"); a non-blank value
  is `long.TryParse`'d (negative / non-numeric ⇒ `TempData["error"]` +
  redirect, no write). The `GET` re-seeds the form (`MaxFileBytesInput =
  MaxFileBytes?.ToString()` → blank when no override;
  `PerUserQuotaBytesInput = PerUserQuotaBytes == 0 ? "" : "…"` → blank when
  unlimited).

**View / model:** `Views/Admin/Storage.cshtml` now carries the **form** (two
`type="number"` inputs — per-file limit [optional, blank = platform default] +
per-user quota — posting to `POST /admin/storage/settings`,
`@Html.AntiForgeryToken()`, the `AdminTimezone`/`AdminMessaging` form idiom)
above U5's read-only display (kept intact). `AdminStorageViewModel` extended
with `MaxFileBytesInput` / `PerUserQuotaBytesInput` (raw-string form fields,
`{ get; set; }` — the display-only U5 fields stay `init`). The test harness
follows the `AdminDateFormatControllerTests` save-lane idiom (a directly
constructed controller needs a `TempDataDictionary` + a no-op
`ITempDataProvider`, or the `Save` lane's `TempData["info"]` write NREs).

**Next unit:** `m25-u07.md` (the **resident self-usage view** — own usage /
quota / remaining + the 2 resident Web tests `ResidentUsageView_SelfOnly` /
`ResidentUsageView_QuotaZeroShowsUnlimited`) — **not started.** U7 adopts the
same `/admin/storage` family route decision (its resident surface is
`AccountController` / `ProfileController`, self-only, C-UP·4/F7).
