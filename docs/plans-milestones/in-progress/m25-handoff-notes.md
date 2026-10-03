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
