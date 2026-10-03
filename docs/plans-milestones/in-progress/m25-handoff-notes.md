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
