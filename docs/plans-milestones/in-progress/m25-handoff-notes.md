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
