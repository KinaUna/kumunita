# ADR 0135 — Upload limits (M25): admin-set per-file limit + per-user content quota over the content-addressed store; `Media__MaxBytes` demoted to the fallback; a pure-Core decision + a single Web gate adopted by every upload lane; a resident self-usage view

Status: Accepted
Date: 2026-10-03
Amends: 0011 (its "`Media__MaxBytes` … the extension point" / "`In-app upload size
enforcement` is the Web boundary's job reading `MediaOptions`" — the per-file cap is
now **admin-set in-app** as a stored community doc, and the env `Media__MaxBytes`
becomes the **fallback** when the admin has not set one; the per-user *quota* is
entirely new); 0034 (its D5 "the **same** `Media__MaxBytes` cap … guards-before-
write … ADR 0011's boundary verbatim" — the per-lane inline `> MaxBytes → 413` gate
is **generalized** into a single `IUploadGate` adopted by every upload lane, with the
size-vs-quota distinction added)
Builds on: 0134 (the M24 storage-metrics lane — its `IStorageMetricsService.
GetPerUserUsageBytesAsync` **C-SM·7** seam is **reused** for the per-user usage read,
and its `GetSnapshotAsync` **C-SM·2** seam for the community-total in the admin view;
M25 is the *enforcement* half, 0134 is the *metrics* half)

The README roadmap names M25 exactly: "**Upload limits** — admin-set limits on the
size of an individual file and on the total content a user may add; residents see
how much space they are using and how much of their quota remains." ADR 0134 (M24)
surfaces the **metrics** half — a read-only `GlobalAdmin` view of *how much* is on
the box. M25 closes the enforcement gap: it makes two numbers **admin-set in-app**
(they were previously only operator-tuned env / in-code) and surfaces one number
**to the resident**. It is a **settings + enforcement** lane over the *existing*
media byte store (ADR 0011): it adds **no** new store, **no** new catalog doc, **no**
new `AccessAction`, and **no** new bounded context — it reuses `IMediaStore` +
`MediaObject` (content-addressed, `SizeBytes`, `CreatedById`) and the existing
`Kumunita.Core.Usage` context (ADR 0006-D — a bounded context per concern; M25 adds a
second seam to the same context rather than opening one).

The one thing the whole lane respects, and the two seams it composes rather than
extends:

- **`Core` stays HTTP-free AND the decision stays pure** (ADR 0006-D, C-UP·3): the
  size/quota decision is a **pure function** on `(incoming, usage, settings,
  envDefault) → StorageDecision`; the `ActionResult` (the single `413`) is
  produced **only** in the Web layer. `IFormFile` / `Stream` / `IActionResult` never
  cross the seam.
- **The frozen ADR 0011 `IMediaStore` / `MediaObject` catalog** — M25 *reads*
  `SizeBytes` / `CreatedById` to derive usage; it adds **no** field, **no** method,
  **no** migration, **no** reshape (C-UP·1/4).
- **The M24 C-SM·7 seam** (`IStorageMetricsService.
  GetPerUserUsageBytesAsync(string, CancellationToken)`) — the
  `Σ MediaObject.SizeBytes WHERE CreatedById == subjectId` read is **reused /
  re-pointed to**, **not** re-implemented (the §2.6 drift-guard names it as the one
  cross-milestone seam M25 depends on).

## Context

The platform already has **two** relevant surfaces, and M25 sits *between* them:

- **The byte store + catalog** (ADR 0011): `IMediaStore.PutAsync` (idempotent,
  content-addressed by SHA-256) + the `MediaObject` doc (`SizeBytes`,
  `CreatedById` = the subject who **first** stored that unique payload). Every
  upload lane (avatar / content-image / attachment / document) already writes
  through the *same* `IMediaStore` + volume.
- **The per-lane size gate (pre-M25):** each Web upload action read
  `MediaOptions.MaxBytes` (env `Media__MaxBytes`, default 5 MiB) and rejected an
  oversize payload with **413** *before* `PutAsync`. That gate was **env-only**
  (operator-set at deploy time) and **duplicated per lane** — the
  `if (mediaOpts.Value.MaxBytes > 0 && file.Length > MaxBytes) return 413;` line
  lived independently in `ProfileController.AvatarUpload`,
  `ContentImageController.Upload`, `AttachmentController.Upload`, and
  `DocumentController.Upload` (+ `DocumentController.Edit`).

**M25 makes that gate (a) admin-set in-app, and (b) two-dimensional.** Concretely,
it resolves three design questions at once: **who sets the limit** (GlobalAdmin,
in-app — the M9 messaging / M20 quiet-times "admin sets a community setting"
precedent, **not** a deploy-time knob), **what is limited** (a per-*file* size
limit **and** a per-*resident* total content quota — both dimensions, with a
**distinct** reject reason), and **who sees what** (the admin sets both; the
resident sees their own usage / quota / remaining). The decision lives in **Core**
(pure), the gate in **Web** (the single `413` producer), and enforcement runs
**before** `PutAsync` on **every** upload lane.

### What M25 does *not* do (named non-decisions, pinned)

- **Not a new store or a new doc for the bytes** — the `MediaObject` catalog and
  the `IMediaStore` byte path are untouched (C-MED·4/6 from ADR 0011 hold).
- **Not per-resident quota overrides** — the quota is one community-wide number
  the admin sets; M25 does not let an admin pick a different quota for a single
  resident. (A later, out-of-scope lane.)
- **Not the admin *metrics* tables** — the per-user / total usage *dashboard* is
  M24's surface (ADR 0134), which M25 **reuses** for its reads. M25 adds the admin
  **settings** surface + the resident **self** usage view, not another metrics
  view.
- **Not serve-time enforcement** — a file already stored is still served; M25 only
  gates *new* uploads at the write boundary. Editing a *reference* does not move
  bytes, so it is not a quota event (the document **edit** lane is the one
  re-upload that *does* call `PutAsync`, and it adopts the gate — see Decision).
- **No `IMediaStore` / `MediaObject` reshape** — no new field on `MediaObject`, no
  new method on `IMediaStore`.

## Decision

**D1 — One community settings doc, admin-set, not an env knob (C-UP·1).** A new
Marten doc **`CommunityStorageSettings`** (ns `Kumunita.Core.Usage`; stable string
`Id = "community"` — one row per community, single-neighborhood platform, ADR
0002) holds the two admin-set numbers:

- **`long? MaxFileBytes`** — the admin **per-file** override. `null`/absent ⇒ the
  env `Media__MaxBytes` **fallback** is in force (C-UP·1/5); a non-blank value
  (including an explicit `0`) is the override (a `0` here means *unlimited file
  size*).
- **`long PerUserQuotaBytes`** — the **per-user** total content quota. `0` =
  **unlimited** (no per-user cap; the size limit is still enforced) (C-UP·5).
- **`DateTimeOffset Modified`** + **`string? ModifiedById`** — the audit of who
  last set it.

It is registered on a **new parallel** doc surface **`StorageSettingsDocTypes.
Configure(StoreOptions)`** (`opts.Schema.For<CommunityStorageSettings>()`) — the
ADR 0004 §B.1 shape (Marten registers the POCO; delta-detected, idempotent). It is
wired in the **single** doc-surface registration site (`Program.cs`
`AddMarten(opts => …)`, directly after `DocumentDocTypes.Configure(opts)` — the 14th
surface, mirroring the `UsageDocTypes` / `DocumentDocTypes` precedent). The
sentinel semantics are **pinned**: quota `0` = unlimited; per-file limit
unset/`0` = the env `Media__MaxBytes` default (C-UP·5) — so `0` is never read as
"no uploads."

The single **admin write** lane is `IStorageSettingsService.
SetAsync(maxFileBytes, perUserQuotaBytes, actorId, session)` — **one in-caller-
session** write (the C3 same-transaction convention; the Web controller owns the
session), stamped with the GlobalAdmin actor (the server-minted subject, never a
path param). The **read** lane is `GetOrCreateAsync()` — **create-if-missing**
with the sentinel defaults (`MaxFileBytes = null`, `PerUserQuotaBytes = 0`), the
M9/M20 community-setting seam.

**D2 — A pure Core decision; size checked first (C-UP·2 / C-UP·3).**
**`StorageLimits`** (a pure `static class` in `Kumunita.Core.Usage`, **no** HTTP,
**no** `IFormFile`) decides:

- **`EffectiveMaxFileBytes(settings, envMaxBytes)`** =
  `settings.MaxFileBytes ?? envMaxBytes` (the C-UP·1/5 fallback; `0` = unlimited
  file size).
- **`Decide(incoming, currentUsage, settings, envMaxBytes) → StorageDecision`**
  (`{ Allowed, Oversize, OverQuota }`): compute the effective max; **size first**
  (`effectiveMax > 0 && incoming > effectiveMax → Oversize`), **then** quota
  (`PerUserQuotaBytes > 0 && currentUsage + incoming > PerUserQuotaBytes →
  OverQuota`); else `Allowed`. Size is checked first because the per-file bound is
  the stronger, cheaper check — an input over *both* reports `Oversize`, not
  `OverQuota` (C-UP·2 ordering).

**`IStorageSettingsService`** (the M25 settings seam) composes the pure decision
with the two reused reads: `GetOrCreateAsync`, `SetAsync`, `GetPerUserUsageBytesAsync
(string, CancellationToken)` (**delegates to the M24 C-SM·7 seam** — the
`Σ MediaObject.SizeBytes WHERE CreatedById == subjectId` read, **not** a second copy
of the query, C-UP·4), and `Decide` (delegates to `StorageLimits`). It is
registered in `AddKumunitaCore` taking the `IDocumentStore` + the
`IStorageMetricsService` (the C-SM·7 seam-reuse factory shape).

**D3 — A single Web gate, the only `413` producer, adopted by every upload lane
(C-UP·3 / C-UP·7 / F10).** **`IUploadGate`** is **Web-only** (it returns
`ActionResult`, so it cannot cross into Core): `Task<ActionResult?> CheckUpload(
incomingBytes, subjectId, settings, envMaxBytes)` reads the subject's usage (the
async C-SM·7 seam), runs the **pure** `StorageLimits.Decide` (size-first), and maps
`Oversize` / `OverQuota` → `413`, `Allowed` → `null`. This is the **single** place
a `413` is produced — so the pre-M25 duplicated inline gates are deleted.

Every upload lane replaces its inline `> MaxBytes → 413` with the **three-line
adoption** **before** `PutAsync` (guards-before-write, C-UP·2): load-or-create the
settings → `uploadGate.CheckUpload(file.Length, subject, settings,
mediaOpts.Value.MaxBytes)` → `if (reject is not null) return reject;`. The **empty
→ 400** check stays *before* the gate; the **allowlist → 415** check stays *after*
it — M25 changes *what* the size gate reads (the admin doc + quota) and *how* it
reports (a **distinct** over-quota `StorageDecision`), **not where** it runs
(C-UP·7). The lanes that adopt it:

| Lane | File | Action |
|------|------|--------|
| avatar | `Controllers/ProfileController.cs` | `AvatarUpload` |
| content-image | `Controllers/ContentImageController.cs` | `Upload` |
| attachment | `Controllers/AttachmentController.cs` | `Upload` |
| document (upload) | `Controllers/DocumentController.cs` | `Upload` |
| document (edit / re-upload) | `Controllers/DocumentController.cs` | `Edit` |

**The document *edit* lane is a deliberate extension, not a pin violation.** The
pinned test list (§2.4) names the four `*Upload` lanes, but `DocumentController.
Edit` also calls `media.PutAsync` (a genuine re-upload). Leaving its inline `413`
would create a **second** `413` producer, violating C-UP·3 ("single place a `413` is
produced"). So the edit lane adopts the gate over `form.File.Length` too (its
optional-file wrapper and the allowlist → 415 are untouched). ADR 0034's per-lane
"guards-before-write" posture (empty → 400, oversize → 413, disallowed → 415) is
**preserved** — the gate is now the *generalized* oversize gate, with the
over-quota dimension added.

**The F2-vs-F3 distinction lives in the decision, not the HTTP carrier.** The
distinct reject reason (oversize vs over-quota) is carried by the **Core**
`StorageDecision` enum (`Oversize` vs `OverQuota`) + the M24 metric; the **HTTP
carrier** is deliberately a bare `413` in both cases (.NET 10's
`StatusCodeResult` carries no body, and the per-lane test suite pins the **exact**
`StatusCodeResult` type). The distinguishability is pinned at the decision level
(`UploadGate_OversizeAndOverQuota_HaveDistinctMessages` drives the gate directly
with one oversize and one over-quota input and pins `StorageDecision.Oversize` vs
`StorageDecision.OverQuota` + `Assert.NotEqual`) — which is the level the resident
would need it anyway.

**D4 — A resident self-view; self-only, no audit row (C-UP·4 / C-UP·7 / F7).**
A signed-in resident sees **their own** usage bytes, their per-user quota (or
"Unlimited" when `0`), and how much of their quota remains — a **read-only**
`GET /account/storage` on the resident `AccountController` surface. The subject is
the signed-in principal (server-minted; never a route/path param), so a resident
can never read another resident's numbers (F7). Quota `0` renders **Unlimited** for
the quota *and* the remaining figure (F5 / C-UP·5). It is read-only: **no**
`AccessAudit` row (C-UP·7), **no** new authorization surface.

**D5 — The admin settings surface, on a distinct route from M24's metrics (C-UP·1 /
F4).** The GlobalAdmin surface is `GET`/`POST /admin/storage/settings`
(`AdminStorageController`, `[Authorize(Roles = GlobalAdmin)]`), **distinct from**
M24's `GET /admin/storage` metrics surface (ADR 0134) — the two coexist: the
`/admin` hub carries a "Storage" card (metrics) **and** a "Storage settings" card
(the M25 set-lane). The `POST` (`[ValidateAntiForgeryToken]`) reads the two values
(blank per-file ⇒ `null` = env fallback; blank-or-`0` quota ⇒ `0` = unlimited),
calls `SetAsync` **in the caller's session** (one write, C-UP·1), and redirects
back. The community **total** read reuses M24's `GetSnapshotAsync` C-SM·2 seam (not
a fresh `Query<MediaObject>().Sum(…)`). This is the **F4** strong-consistency
contract: the admin raises the limit and the *next* within-limit upload succeeds,
because the decision reads the **live** community doc, one source of truth (C-UP·1).

The **seven invariants (C-UP·1–7)** and the **ten FACES (F1–F10)** (the design doc
`docs/design/m25-upload-limits-design.md` is the primary tier for their full text)
are the decision's enforceable core, and are **frozen** — this ADR references them
by id and does not renumber or rename them:

- **C-UP·1** — the per-file limit + per-user quota are **GlobalAdmin-set** as a
  single community doc; the env `Media__MaxBytes` is the **fallback**. Not
  per-request spoofable.
- **C-UP·2** — **guards before write**: size + quota are checked **before**
  `PutAsync`; on a reject, **no byte is written**.
- **C-UP·3** — **`Core` stays HTTP-free** (ADR 0006-D): the decision is a pure Core
  function; the `ActionResult` mapping is Web-only.
- **C-UP·4** — **content-addressed usage**: a user's usage =
  `Σ MediaObject.SizeBytes WHERE CreatedById == subjectId`; dedup inherent (a byte
  uploaded by A is not counted in B's usage).
- **C-UP·5** — **sentinel semantics**: quota `0` = **unlimited**; per-file limit
  unset/`0` = the env `Media__MaxBytes` default.
- **C-UP·6** — **single-in-progress milestone contract** (MilestonesTests): M25
  starts only after M24 is `StatusDone`; M25's close promotes M26.
- **C-UP·7** — **no new authorization surface**: the check is at the authenticated
  upload boundary (self-scoped); it adds **no** `AccessAudit` row / `AccessAction`;
  it only changes the *reject* shape (413, distinct).

- **F1** under-quota within-limit → success · **F2** over the per-file limit → 413
  **oversize** · **F3** pushes over quota → 413 **over-quota** (distinct) ·
  **F4** admin raises the limit; the next within-limit upload succeeds (strong
  consistency) · **F5** quota `0` → unlimited; size still enforced · **F6**
  admin-unset limit → env fallback · **F7** resident self-view: own usage/quota/
  remaining, never another's · **F8** a byte uploaded by A is not in B's usage
  (first-storer attribution) · **F9** no byte written on a size/quota reject ·
  **F10** enforcement is consistent across all upload lanes.

## Consequences

- **`Core` stays HTTP-free and the decision stays pure.** `IFormFile` / `Stream` /
  `IActionResult` never cross the seam; the Web layer owns the `413`. ADR 0006-D
  holds. The `StorageLimits` decision is testable with no DB, no HTTP, no session.
- **One settings doc + one doc surface + one DI line.** `CommunityStorageSettings`
  is the ADR 0004 §B.1 shape (Marten registers the POCO;
  `StorageSettingsDocTypes.Configure(opts)` is called once in `Program.cs`, the
  same call-site family as `M1DocTypes` / `M3DocTypes` / `UsageDocTypes`).
  **Zero migrations**, **zero new bounded context**.
- **The per-user usage read is the M24 C-SM·7 seam, not a second query.**
  `IStorageSettingsService.GetPerUserUsageBytesAsync` delegates to
  `IStorageMetricsService.GetPerUserUsageBytesAsync` — the "reuse, don't
  re-implement" rule. The community total reuses the C-SM·2 `GetSnapshotAsync`
  seam. M25 and M24 are the two halves (enforcement / metrics) of one storage
  concern.
- **The gate is the single `413` producer.** The pre-M25 duplicated inline
  `> MaxBytes → 413` lines are deleted from all four `Upload` lanes + the document
  `Edit` lane; a grep for the inline guard returns **zero**. F2/F3 distinction lives
  in the Core `StorageDecision`; the HTTP carrier is a bare `413` in both (the
  per-lane suite pins the exact `StatusCodeResult` type).
- **OPS.md: `Media__MaxBytes` is now the *fallback*.** The per-file cap is
  primarily admin-set in-app (`/admin/storage/settings`); `Media__MaxBytes`
  remains the fallback when the admin has not set one, and its `0` = no-cap
  sentinel now reads as *unlimited file size* under an admin override. The per-user
  quota is **not** an env knob — it is admin-set in-app only.
- **The admin settings surface is a distinct route from M24's metrics.** The two
  coexist on the same volume (settings vs metrics); the `/admin` hub carries both
  cards. M25 did not reshape M24's `AdminStorageMetricsController` /
  `StorageMetrics.cshtml`.
- **`Milestones.cs` / `MilestonesTests.cs` are U12's, not this ADR's.** M25 is
  still `StatusNext` (in progress) at the time this ADR is authored; the
  `StatusDone` flip + M26 promotion is the close unit's job (C-UP·6).
- **A pre-existing M24 defect is flagged, not fixed here.** The
  `LocalVolumeFileStore.Statvfs` P/Invoke (M24's available-space read) intermittently
  `AccessViolationException`s under Linux-in-Docker (a `statvfs_t` struct-layout
  mismatch for the target glibc). M25's admin settings view reaches it because it
  reuses the M24 `GetSnapshotAsync` seam; it is invisible in a pure-Windows
  `dotnet test` run. It is a **separate lane** (the `statvfs_t` sizing / a portable
  disk-usage read), out of this ADR's scope — recorded so it is not mistaken for an
  M25 regression.

## Not decided here (explicit non-decisions)

Each is a **future lane**, named — the ADR 0011 / 0034 precedent holds:

- **Per-resident quota overrides** — the quota is a single community-wide value the
  admin sets; a per-resident override (an admin picking a different quota for one
  user) is a later lane (would widen `CommunityStorageSettings` to a per-resident
  map).
- **The admin *metrics* tables** — M24's surface (ADR 0134); M25 reuses its reads,
  does not add another metrics view.
- **Serve-time / edit-reference enforcement** — the gate is at the *byte-write*
  boundary; serving an already-stored file and editing a *reference* are not quota
  events (the document **edit** re-upload is the one exception, and it adopts the
  gate).
- **Any `IMediaStore` / `MediaObject` reshape** — no new field, no new method.

## Revisit when

- A deployment wants a **per-resident** quota (not community-wide) — widen
  `CommunityStorageSettings` to a per-resident map; a new ADR, not an amendment to
  this one.
- The **`Media__MaxBytes`** env default needs to change (a different platform
  ceiling) — it is the **fallback** now; the admin surface is the primary. A deploy
  can still tune the floor via the env var.
- A resident asks for a **richer self-view** (a per-file list, a per-resident
  breakdown) — that is the M24 metrics half surfaced to the resident; a future
  lane.
- The **`statvfs`** M24 defect above is fixed (or the available-space read is
  re-pointed to a portable seam) — the C-SM·2 seam M25 reuses for the community
  total would then be stable under Linux containers.
