---
id: adr-0135
title: "Upload limits (M25): admin-set per-file limit + per-user content quota over the content-addressed store; `Media__MaxBytes` demoted to the fallback; a pure-Core decision + a single Web gate adopted by every upload lane; a resident self-usage view"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m25-upload-limits-design.md]
supersedes: [adr-0011]
superseded_by: []
updated: 2026-11-02
---

## Context

The platform already has **two** relevant surfaces, and M25 sits *between* them:
the **byte store + catalog** (ADR [0011](0011-media-and-file-storage.md) — `IMediaStore.PutAsync` + the
`MediaObject` doc every upload lane writes through) and the **per-lane size
gate (pre-M25)** — each Web upload action read `MediaOptions.MaxBytes` (env
`Media__MaxBytes`, default 10 MiB) and rejected an oversize payload with **413**
*before* `PutAsync`. That gate was **env-only** (operator-set at deploy time)
and **duplicated per lane**.

**M25 makes that gate (a) admin-set in-app, and (b) two-dimensional.** It
resolves three design questions at once: **who sets the limit** (GlobalAdmin,
in-app — **not** a deploy-time knob), **what is limited** (a per-*file* size
limit **and** a per-*resident* total content quota — both dimensions, with a
**distinct** reject reason), and **who sees what** (the admin sets both; the
resident sees their own usage / quota / remaining). The decision lives in
**Core** (pure), the gate in **Web** (the single `413` producer), and
enforcement runs **before** `PutAsync` on **every** upload lane. The one thing
the lane respects: **`Core` stays HTTP-free and the decision stays pure**
(ADR [0006](0006-module-boundary-contracts.md)-D). The full moved detail (the `CommunityStorageSettings` field
list, the `StorageLimits.Decide` shape, the 5-lane adoption table, the F2-
vs-F3 detail, the seven C-UP invariants, the ten FACES, the OPS.md note, the
M24 `statvfs_t` defect flag) lives in the [handoff
note](../design/adr-handoffs.md#adr-0135--upload-limits-m25-moved-detail).

### What M25 does *not* do (named non-decisions, pinned)

- **Not a new store or a new doc for the bytes** — the `MediaObject` catalog and
  the `IMediaStore` byte path are untouched.
- **Not per-resident quota overrides** — the quota is one community-wide number
  the admin sets; M25 does not let an admin pick a different quota for a single
  resident.
- **Not the admin *metrics* tables** — the per-user / total usage *dashboard* is
  M24's surface (ADR [0134](0134-storage-metrics.md)), which M25 **reuses** for its reads.
- **Not serve-time enforcement** — a file already stored is still served; M25
  only gates *new* uploads at the write boundary.
- **No `IMediaStore` / `MediaObject` reshape** — no new field on `MediaObject`,
  no new method on `IMediaStore`.

## Decision

**D1 — One community settings doc (admin-set).** A new Marten doc
**`CommunityStorageSettings`** (ns `Kumunita.Core.Usage`; stable string
`Id = "community"`) holds the two admin-set numbers (the **`MaxFileBytes`**
per-file override, `null`/absent ⇒ the env `Media__MaxBytes` **fallback**; and
the **`PerUserQuotaBytes`** per-user total, `0` = **unlimited**) + a
`Modified` / `ModifiedById` stamp, registered on a new parallel doc surface
`StorageSettingsDocTypes.Configure(StoreOptions)`. The sentinel semantics are
**pinned** (quota `0` = unlimited; per-file unset/`0` = the env default —
C-UP·5). *See the handoff note for the full field list.*

**D2 — A pure Core decision.** **`StorageLimits`** (a pure `static class` in
`Kumunita.Core.Usage`) — `EffectiveMaxFileBytes(settings, envMaxBytes)` and
**`Decide(incoming, currentUsage, settings, envMaxBytes) →
StorageDecision`** (`{ Allowed, Oversize, OverQuota }`), **size first** (C-
UP·2 ordering). **`IStorageSettingsService`** composes the pure decision with
the two reused reads, including the per-user usage read **delegated to the M24
C-SM·7 seam** (not a second copy of the query — C-UP·4). *See the handoff note
for the full decision shape.*

**D3 — A single Web gate.** **`IUploadGate`** is **Web-only**: it reads the
subject's usage (the async C-SM·7 seam), runs the **pure**
`StorageLimits.Decide` (size-first), and maps `Oversize` / `OverQuota` →
`413`, `Allowed` → `null`. This is the **single** place a `413` is produced —
the pre-M25 duplicated inline gates are deleted. All five upload lanes adopt it
**before** `PutAsync` (guards-before-write, C-UP·2), including the deliberate
**document re-upload** extension. The distinct reject reason lives in the Core
`StorageDecision` enum, not the HTTP carrier (a bare `413` in both cases) — the
F2-vs-F3 distinction. *See the handoff note for the 5-lane adoption table.*

**D4 — A resident self-view.** A signed-in resident sees **their own** usage
bytes, their per-user quota (or "Unlimited" when `0`), and how much remains —
a **read-only** `GET /account/storage`. The subject is the signed-in principal
(server-minted; never a route param), so a resident can never read another
resident's numbers. Read-only: **no** `AccessAudit` row (C-UP·7).

**D5 — The admin settings surface.** `GET`/`POST /admin/storage/settings`
(`AdminStorageController`, `[Authorize(Roles = GlobalAdmin)]`), **distinct
from** M24's `GET /admin/storage` metrics surface (ADR [0134](0134-storage-metrics.md)). The `POST` reads
the two values and calls `SetAsync` **in the caller's session** (one write,
C-UP·1) — the F4 strong-consistency contract.

## Consequences

- **`Core` stays HTTP-free and the decision stays pure** (ADR [0006](0006-module-boundary-contracts.md)-D):
  `IFormFile` / `Stream` / `IActionResult` never cross the seam; the Web layer
  owns the `413`. The `StorageLimits` decision is testable with no DB, no HTTP,
  no session.
- **One settings doc + one doc surface + one DI line.** **Zero migrations**,
  **zero new bounded context.** The per-user usage read is the M24 C-SM·7 seam,
  not a second query; the community total reuses the C-SM·2 `GetSnapshotAsync`
  seam.
- **The gate is the single `413` producer.** The pre-M25 duplicated inline
  `> MaxBytes → 413` lines are deleted from all four `Upload` lanes + the
  document `Edit` lane; a grep for the inline guard returns **zero**. F2/F3
  distinction lives in the Core `StorageDecision`; the HTTP carrier is a bare
  `413` in both.
- **OPS.md: `Media__MaxBytes` is now the *fallback*** — the per-file cap is
  primarily admin-set in-app; the env var remains the fallback when the admin
  has not set one. The per-user quota is **not** an env knob — admin-set in-app
  only. The admin settings surface is a distinct route from M24's metrics.
- **A pre-existing M24 defect is flagged, not fixed here** — the
  `LocalVolumeFileStore.Statvfs` P/Invoke intermittently
  `AccessViolationException`s under Linux-in-Docker (a `statvfs_t` struct-
  layout mismatch); it is a **separate lane**. *See the handoff note for the
  full consequence list, the seven C-UP invariants, and the ten FACES.*
