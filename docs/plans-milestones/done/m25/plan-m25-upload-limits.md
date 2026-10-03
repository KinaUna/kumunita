# M25 — Upload limits — sealed unit register

> **In progress.** This is the **plan** for M25 (Upload limits), split into
> **sealed units** sized for a **~32K-context fresh agent one at a time**,
> exactly like `done/m3/plan-m3-posts-components.md` and
> `done/file-attachments/plan-file-attachments.md`. The **primary** reference
> tier — the exact C# seams every unit codes against — is the design doc
> `docs/design/m25-upload-limits-design.md` (authored in U1/U2, **not yet
> implemented**). The **secondary** tier is this file (unit registry +
> deliverables + exit criteria). The **scratch** tier is
> `docs/plans-milestones/in-progress/m25-handoff-notes.md` (one appended
> `## U#` section per unit, never rewritten; created by U1, moved to
> `done/m25/` by the close unit U12).
>
> **What this is:** the README/Milestones M25 promise — **"admin-set per-file
> size limit and per-user total content quota; residents see their own usage
> and how much of their quota remains."** It is a **settings + enforcement**
> lane over the *existing* media byte store (ADR 0011): it does **not** add a
> new store, a new catalog doc, a new `AccessAction`, or a new bounded
> context. It **reuses** `IMediaStore` + `MediaObject` (content-addressed,
> `SizeBytes`, `CreatedById`) and the **`Kumunita.Core.Usage`** context
> (created for M13's `UsageAnalyticsService`, the natural home of M24's
> "storage metrics" and M25's per-user usage — **no new bounded context**,
> ADR 0006-D).
>
> **What changes the world:** (1) the per-file size limit and per-user quota
> become **GlobalAdmin-set in-app** (a stored community doc) that **override**
> the env `Media__MaxBytes` (which drops to a **fallback**); (2) a **pure
> Core decision** (size + quota) + a thin **Web gate** that enforces it across
> **all four upload lanes** (avatar, content-image, attachment, document)
> **before** `PutAsync` (guards-before-write, no byte written on a reject);
> (3) a **resident self-view** of their own usage / quota / remaining.
>
> **The one thing every unit must respect:** `Core` stays **HTTP-free**
> (ADR 0006-D, C-UP·3) — the size/quota decision is a pure function on
> `(incoming, usage, settings, envDefault)`; the `ActionResult` mapping is
> **Web-only**. And the **single-in-progress milestone** contract
> (C-UP·6): M25 starts **only after M24 is `StatusDone`**, and M25's close
> (U12) flips M25 → `StatusDone` and **promotes M26 → `StatusNext`**.

## Understanding

The platform already has **two** relevant surfaces, and M25 sits *between* them:

- **The byte store + catalog** (ADR 0011): `IMediaStore.PutAsync` (idempotent,
  content-addressed by SHA-256) + the `MediaObject` doc
  (`SizeBytes`, `CreatedById` = the subject who **first** stored that unique
  payload). Every upload lane (avatar / content-image / attachment / document)
  already writes through the *same* `IMediaStore` + volume. **M25 reuses this
  verbatim — no new store, no new catalog doc.**
- **The per-lane size gate** (today): each Web upload action reads
  `mediaOpts.Value.MaxBytes` (env `Media__MaxBytes`, default 5 MiB) and rejects
  oversize payloads with **413** *before* `PutAsync`. This gate is **env-only**
  (the operator sets it at deploy time) and is **duplicated per lane** (the
  `if (mediaOpts.Value.MaxBytes > 0 && file.Length > mediaOpts.Value.MaxBytes)`
  line appears in `AttachmentController`, `ContentImageController`,
  `DocumentController`, and `ProfileController.AvatarUpload`).

**M25 makes that gate (a) admin-set in-app, and (b) two-dimensional.** Concretely:

- **Admin-set, not env-only.** A new GlobalAdmin surface (`/admin/storage`) lets
  the community's admin set (1) a **per-file size limit** and (2) a **per-user
  total content quota** — as a single stored community doc
  (`CommunityStorageSettings`). The env `Media__MaxBytes` becomes the
  **fallback** when the admin has not set a limit (C-UP·5). This mirrors the
  existing "admin sets a community-wide setting" precedent — M9 messaging
  (`AdminMessagingController`) and M20 quiet times (`AdminQuietController`) —
  **not** a deploy-time knob.
- **Two-dimensional (size + quota).** The per-file limit is per-*upload*; the
  quota is per-*resident* (the total bytes a resident may have uploaded). A
  file can be within the per-file limit **and still** push a resident over
  their quota — both dimensions are checked, and the **reject reason is
  distinct** (oversize vs. over-quota) so the resident sees *why*.
- **Content-addressed usage (C-UP·4).** A resident's usage is
  `Σ MediaObject.SizeBytes WHERE CreatedById == subjectId`. Because the store
  is content-addressed (SHA-256), a byte is attributed to its **first-storer
  once** — so dedup is inherent and a file two residents both upload is counted
  for **each** first-storer only if they were each first. This is exactly the
  "space used per user" metric M24 surfaces to the admin; M25 reuses the same
  read shape for **enforcement**.
- **Enforcement is at the authenticated upload boundary (C-UP·7).** Every
  upload is already self-scoped and authenticated (the subject is the
  signed-in principal, minted server-side). The size/quota check runs **before**
  `PutAsync` and adds **no** `AccessAudit` row and **no** `AccessAction` — it
  only changes the *reject* shape (413, with a size-vs-quota distinction). This
  is the C-UP·2 "guards-before-write" posture: on a reject, **no byte is
  written**.
- **The decision lives in Core, the gate in Web (C-UP·3).** `Core` stays
  HTTP-free (ADR 0006-D): `StorageLimits` is a **pure** Core module that decides
  `(incoming, usage, settings, envDefault) → (Allowed | Oversize | OverQuota)`.
  A thin **Web** gate (`IUploadGate`) maps that decision to an `ActionResult`
  (413 + a distinct message) and is the single call-site each lane adopts.

**What is *not* M25 (named non-decisions, pinned in the design doc):**
- **Per-resident *individual* quota overrides** (a resident's admin setting a
  different quota for *one* user) — M25's quota is a single community-wide value
  the admin sets; individual overrides are a **future lane**.
- **The admin *metrics* view** (total used / available / per-user *tables*) —
  that is **M24** (storage metrics). M25 reuses M24's **per-user usage read
  shape** for enforcement and adds the admin **settings** UI + the resident
  **self** usage view. (If M24's `IStorageMetricsService` has landed by the
  time M25's core units run, M25 *reuses* its per-user seam; if not, M25 ships a
  minimal `GetPerUserUsageBytesAsync` on `IStorageSettingsService` and
  reconciles with M24's seam at U12 close — see the **drift-guard**.)
- **Enforcement of the limit on *serve* or *edit/reupload* of an existing
  file** — the gate is at **upload time** (the byte-write boundary); editing a
  reference does not move bytes, so it is not a quota event.
- **Any change to `IMediaStore`, `MediaObject`, or the content-addressed
  identity** (C-UP·1/4) — M25 reads them, never reshapes them.

**The one thing to internalize before writing code:** the **single-in-progress
milestone** contract. `MilestonesTests.cs` pins that **exactly one** milestone
is `StatusNext` (in-progress) and names it (currently `M24`). M25 is
`StatusPlanned`. So **M25 cannot begin** until M24's close unit has flipped
M24 → `StatusDone` and M25 → `StatusNext`. **U1 (start) enforces this as a
precondition check**; **U12 (close)** flips M25 → `StatusDone` and promotes
M26 → `StatusNext` and updates the two `MilestonesTests.cs` tests to match.

## Assumptions

- **Scope (per the README/Milestones M25 title):** In: the admin-set
  **per-file size limit** + **per-user total content quota** (a single stored
  community doc, GlobalAdmin-set, `Media__MaxBytes` as fallback); the **pure
  Core** size+quota decision (`StorageLimits`) + the **Web gate** (`IUploadGate`)
  adopted by **all four upload lanes** (avatar, content-image, attachment,
  document) **before** `PutAsync`; the **resident self-view** (own usage /
  quota / remaining); the `CommunityStorageSettings` doc + a registration
  surface; the admin `/admin/storage` surface; the pinned Core + Web tests; the
  ADR + OPS/README/Milestones docs sync. **Out (named non-decisions above):**
  per-resident quota overrides, the admin *metrics* tables (M24), serve-time
  enforcement, any `IMediaStore`/`MediaObject` reshape.
- **Admin-set = a stored community doc, not an env knob.** `Media__MaxBytes`
  (env) remains the **fallback** when the admin has not set a limit (C-UP·5).
  The admin surface is the M9/M20 "GlobalAdmin sets a community setting"
  precedent — **not** a new deploy-time config.
- **`Core` stays HTTP-free AND the decision is pure** (ADR 0006-D; C-UP·3):
  `StorageLimits.Decide(incoming, usage, settings, envDefault)` returns an
  enum — no `IFormFile`, no `ActionResult`, no HTTP. The Web gate is the only
  place a `413`/`ActionResult` is produced.
- **Usage is content-addressed and first-storer-scoped (C-UP·4):**
  `Σ SizeBytes WHERE CreatedById == subjectId`. Dedup is inherent; a byte is
  counted for its first-storer once. This is the same per-user metric M24
  surfaces; M25 reuses the read shape.
- **Sentinel semantics are explicit (C-UP·5):** quota `0` = **unlimited** (no
  per-user cap; size still enforced); per-file limit **unset/0** = the env
  `Media__MaxBytes` default. Pinned so "0" is never read as "no uploads."
- **No new authorization surface (C-UP·7):** the upload is already
  authenticated + self-scoped; the size/quota check is a *reject-shape* change
  (413 with a distinct message), **not** a new `AccessAction` / `AccessVia` /
  `AccessAudit` row.
- **No new bounded context (ADR 0006-D):** the settings + usage + decision live
  in the existing **`Kumunita.Core.Usage`** context (M13's
  `UsageAnalyticsService` home; M24's storage-metrics home). One new doc
  (`CommunityStorageSettings`) + one new service (`IStorageSettingsService`) +
  one pure decision module (`StorageLimits`) — all in `Kumunita.Core.Usage`.
- **Versioned storage (ADR 0004 §B.1):** `CommunityStorageSettings` is
  **Marten-native** (POCO, a stable string `Id` = `"community"`), registered on
  a new parallel doc surface `Usage.StorageSettingsDocTypes.Configure(StoreOptions)`
  (mirroring `M1DocTypes`/`M3DocTypes`), delta-detected + idempotent,
  **create-if-missing on read** (no hand-rolled seed; the read lane is the
  create-if-missing seam, mirroring the M9/M20 community-setting precedent).
- **Test model (unchanged).** The invariant-anchored seam-test list pinned in
  the design doc Part 2 + the **three-test acceptance gate** (closed-loop /
  handoff / part-vs-whole), recorded in the rolling handoff note.
- **Milestone contract (C-UP·6):** M25 begins with M24 `StatusDone` and M25
  `StatusNext`; U12 closes M25 (`StatusDone`) and promotes M26 (`StatusNext`),
  updating `MilestonesTests.cs` (`Shipped_Milestones_Are_Marked_Done` gains
  M25; the `M24_Is_The_Single_InProgress_Milestone` test is re-pointed to M26).

## Approach

Two tracks, sequenced — **Track A (Core + docs)** then **Track B (Web + tests +
close)**, each unit **build-green** before handoff. **Track A (U1–U4):** the
design doc (U1/U2), the `CommunityStorageSettings` doc + registration surface +
boot wiring (U3), and `IStorageSettingsService` + the pure `StorageLimits`
decision + the Core usage/decision tests (U4). **Track B (U5–U12):** the admin
surface (U5 view, U6 set-limits lane), the resident self-usage view (U7), the
Web gate + the four-lane enforcement wiring (U8), the per-lane Web enforcement
tests (U9), the acceptance gate + FACES tests (U10), the ADR/OPS/README docs
(U11), and the close: `Milestones.cs` + `MilestonesTests.cs` status flip +
design-doc close section + handoff close + move to `done/` (U12).

Every unit ends with **build green** (and, for test-bearing units, the pinned
tests passing via the repo's reliable runner — see the *Running the tests*
section). **U1** appends the first handoff section and enforces the
milestone-start precondition; **U12** appends the final handoff section and the
"M25 — Closed (recorded)" design-doc section and moves the plan + handoff note
to `done/m25/`.

## Workflow — handoff protocol for fresh-context agents

Executed as a sequence of **sealed units** (U1–U12 below), one unit per fresh
agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/m25-upload-limits-design.md`,
  authored in U1/U2) pins the exact C# signatures of every seam U3–U11 must
  match.
- **Secondary — this file** (`docs/plans-milestones/plan-m25-upload-limits.md`)
  — the unit registry with each unit's deliverables and exit criteria.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m25-handoff-notes.md`). One section per
  unit, **appended** (never rewritten). Each unit writes exactly one short
  section before it exits; the next unit reads only that section + its own
  entry-read list.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list, 3–5
files <~300 lines each, no full-repo scan; the design-doc section cited is
named); **Deliverables** (a closed set of new/modified files, ≤ ~4 files /
~600 LOC, no misc cleanups); **Exit** (build green for the touched projects;
handoff-note entry appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §2.7 drift-guard;
(3) never introduces a test whose exact name is not in the §2.5 seam list;
(4) never opens a *new* seam on `IAuthorizationService` / `IUserInfoService` /
`IIdentityService` / `IMediaStore` beyond what U1/U2 pinned; (5) never
re-shapes `MediaObject` / the `IMediaStore` surface outside the §2.2 pin;
(6) if entry reads reveal the design doc or the milestone contract is out of
date (e.g. M24 not `StatusDone`, or M24's metrics seam landed and the per-user
usage seam should be re-pointed to it), the unit **pauses** and records
`## U<m> — Drift pause` in the handoff note.

**Running the tests (test-runner quirk — read before U4/U9/U10).** Both test
projects use **xunit.v3**; on this machine `dotnet test` / VS Test Explorer
reliably reports "No tests found to run" — that is a **runner bug, not a
failure**. The reliable path (per `AGENTS.md`):

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

`Kumunita.Core.Tests` takes ~20 s (Testcontainers `postgres:18`; clean up with
`docker container prune` if killed).

---

## Units (12 total)

### U1 — Start + design doc Part 1 (+ milestone-start precondition)
- **Goal:** author `docs/design/m25-upload-limits-design.md` Part 1 —
  **Context, Scope (in/out incl. the named non-decisions), Invariants
  (C-UP·1–C-UP·7), FACES (F1–F10)** — and **create the handoff note** with the
  first section. **Before writing, enforce the C-UP·6 precondition** (below).
  **No code, no build.**
- **Precondition check (C-UP·6):** read `src/Kumunita.Web/Milestones.cs`. If
  M24 is **not** `StatusDone`, or M25 is **not** `StatusNext` (i.e. M24's close
  unit has not promoted M25), **stop and report BLOCKED** — M25 cannot begin
  until M24 closes. Record the exact current M24/M25 status in the handoff note
  and **do not** author the design doc. (This is the drift-guard for the
  single-in-progress contract.)
- **Entry reads:** `docs/plans-milestones/done/m3/plan-m3-posts-components.md`
  (the FACES/invariant template + the three-tier handoff contract),
  `docs/design/media-file-storage-design.md` (the C-MED invariant style to
  emulate), `docs/adr/0011-media-and-file-storage.md` (C-MED·4/5/6/7; the
  content-addressed store + `MaxBytes` boundary), `docs/adr/0034-file-attachments-posts-replies-announcements.md`
  (the per-lane `MaxBytes` gate + guards-before-write),
  `src/Kumunita.Core/Media/MediaOptions.cs` (the `MaxBytes` + allowlist shape —
  read-only, M25 does **not** change it), `src/Kumunita.Core/Media/MediaObject.cs`
  (the `SizeBytes` / `CreatedById` the usage metric keys off),
  `src/Kumunita.Web/Controllers/AttachmentController.cs` (the per-lane 413 gate
  to replace), `src/Kumunita.Web/Milestones.cs` (the M24/M25 status; the C-UP·6
  precondition), `docs/plans-milestones/done/m22/*` (the "M22 close unit promoted
  M24" precedent for the status-flip contract), `docs/adr/0120-guest-accounts.md`
  or `docs/plans-milestones/done/*/` for the M9 `AdminMessagingController` /
  M20 `AdminQuietController` "admin sets a community setting" precedent (the
  surface shape M25's `/admin/storage` mirrors).
- **Deliverables (2 files, new):**
  - `docs/design/m25-upload-limits-design.md` (~200 lines). Sections:
    - `## Context` — M24 surfaces *metrics*; M25 makes the size limit
      admin-set and adds the per-user **quota** + the resident **self** usage
      view; what M25 does *not* do (the non-decisions).
    - `## Scope` — **In:** the `CommunityStorageSettings` doc +
      `StorageSettingsDocTypes` surface + boot wiring; `IStorageSettingsService`
      (get/set/per-user-usage); the pure `StorageLimits` decision; the `IUploadGate`
      Web gate adopted by the four upload lanes (avatar, content-image,
      attachment, document) **before** `PutAsync`; the `/admin/storage` surface;
      the resident self-usage view; the pinned Core + Web tests; the ADR +
      OPS/README/Milestones sync. **Out (named non-decisions):** per-resident
      quota overrides, the admin *metrics* tables (M24), serve-time enforcement,
      any `IMediaStore`/`MediaObject` reshape.
    - `## Invariants (pinned for M25)` — the 7 invariants, each with a one-line
      M25 note (the exact ids/names are **frozen** once written):
      - **C-UP·1** — the per-file limit + per-user quota are **GlobalAdmin-set**
        as a single community doc; the env `Media__MaxBytes` is the **fallback**
        (C-UP·5). Not per-request spoofable.
      - **C-UP·2** — **guards before write**: size + quota are checked
        **before** `PutAsync`; on a reject, **no byte is written**.
      - **C-UP·3** — **`Core` stays HTTP-free** (ADR 0006-D): the decision is a
        pure Core function; the `ActionResult` mapping is Web-only.
      - **C-UP·4** — **content-addressed usage**: a user's usage =
        `Σ MediaObject.SizeBytes WHERE CreatedById == subjectId`; dedup inherent.
      - **C-UP·5** — **sentinel semantics**: quota `0` = **unlimited**;
        per-file limit unset/`0` = the env `Media__MaxBytes` default.
      - **C-UP·6** — **single-in-progress milestone contract** (MilestonesTests):
        M25 starts only after M24 is `StatusDone`; M25's close promotes M26.
      - **C-UP·7** — **no new authorization surface**: the check is at the
        authenticated upload boundary (self-scoped) and adds **no**
        `AccessAudit` row / `AccessAction`; it only changes the *reject* shape.
    - `## FACES (pinned, 10)` — F1–F10, each bound to an invariant (the
      exact names are **frozen** once written):
      - **F1** resident under quota uploads within the size limit → success
        (existing behavior preserved) — C-UP·2
      - **F2** upload over the admin-set per-file limit → 413 **oversize** — C-UP·2, C-UP·5
      - **F3** upload that pushes the resident over quota → 413 **over-quota** (distinct) — C-UP·2, C-UP·4
      - **F4** admin raises the limit; the next upload within it succeeds (strong consistency) — C-UP·1
      - **F5** quota `0` → unlimited; size limit still enforced — C-UP·5
      - **F6** admin-unset limit → env `Media__MaxBytes` fallback — C-UP·1, C-UP·5
      - **F7** resident self-view shows own usage / quota / remaining (or "unlimited"); never another resident's numbers — C-UP·4
      - **F8** a byte uploaded by A is not counted in B's usage (first-storer attribution) — C-UP·4
      - **F9** no byte is written on a size/quota reject — C-UP·2
      - **F10** enforcement is consistent across all four upload lanes — C-UP·2, C-UP·7
  - `docs/plans-milestones/in-progress/m25-handoff-notes.md` — the first section
    `## U1 — design doc Part 1` (see Exit).
- **Exit:** file exists with all sections; handoff note exists with the U1
  section. **No build.** Handoff note (5–7 lines): the **7 invariants** (by id)
  + the **10 FACES** (F1–F10) + the **C-UP·6 precondition outcome** (M24
  `StatusDone` confirmed / or the BLOCKED state).

### U2 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard)
- **Goal:** append `## Seams & contracts (Part 2, written by U2)` to the design
  doc — the exact C# shapes U3–U11 must match, the four-lane adoption rule,
  the **pinned test names**, the **three-test acceptance gate**, and the
  **drift-guard**. **No code, no build.**
- **Entry reads:** U1's Part 1 (the invariant/FACE table is the primary source);
  `src/Kumunita.Core/Media/MediaOptions.cs` (the `MaxBytes` fallback — read-only);
  `src/Kumunita.Core/Media/IMediaStore.cs` + `MediaObject.cs` (the unchanged
  store seam + the `SizeBytes`/`CreatedById` fields); `src/Kumunita.Core/Usage/UsageAnalyticsService.cs`
  (the `Kumunita.Core.Usage` context home — the namespace M25's new types live in;
  mirror its registration in `Kumunita.Core/DependencyInjection.cs`);
  `src/Kumunita.Core/DependencyInjection.cs` (the `AddTransient` + factory
  registration shape); `src/Kumunita.Core/M3DocTypes.cs` + `Bootstrap/SchemaBootstrap.cs`
  + `src/Kumunita.Web/Program.cs` (the doc-surface + two boot-path shape U3 mirrors);
  `src/Kumunita.Web/Controllers/AttachmentController.cs` +
  `ContentImageController.cs` + `DocumentController.cs` +
  `ProfileController.AvatarUpload` (the **four** per-lane 413 gates U8 replaces —
  read each to pin the exact call-site + the guard order to preserve);
  `src/Kumunita.Web/Controllers/AdminMessagingController.cs` or
  `AdminQuietController.cs` (the "admin sets a community setting" surface U5/U6
  mirrors).
- **Deliverables (1 append, same file):** `docs/design/m25-upload-limits-design.md`.
  Sub-sections:
  - `### 2.1 new Core-owned types (exact C#)` — namespace
    `Kumunita.Core.Usage`:
    - `CommunityStorageSettings` (a Marten doc; stable string `Id` =
      `"community"`): `long? MaxFileBytes` (admin per-file override; `null`/absent
      ⇒ env fallback; C-UP·1/5), `long PerUserQuotaBytes` (`0` = unlimited;
      C-UP·5), `DateTimeOffset Modified`, `string? ModifiedById`.
    - `IStorageSettingsService`: `Task<CommunityStorageSettings>
      GetOrCreateAsync(CancellationToken ct)` (read; **create-if-missing** with
      sentinel defaults — the M9/M20 community-setting seam); `Task
      SetAsync(long? maxFileBytes, long perUserQuotaBytes, string actorId,
      IDocumentSession session)` (the single **admin** write lane — one
      in-caller-session write, C-UP·1; GlobalAdmin-gated in Web);
      `Task<long> GetPerUserUsageBytesAsync(string subjectId, CancellationToken
      ct)` (the C-UP·4 read: `Σ SizeBytes WHERE CreatedById == subjectId`);
      `StorageDecision Decide(long incomingBytes, long currentUsageBytes,
      CommunityStorageSettings settings, long envMaxBytes)` (**pure**, the C-UP·3
      decision).
    - `enum StorageDecision { Allowed, Oversize, OverQuota }`.
    - `static class StorageLimits` (pure, C-UP·3): the `Decide` logic —
      `effectiveMax = settings.MaxFileBytes ?? envMaxBytes` (0 = unlimited file
      size, C-UP·5); `if incoming > effectiveMax → Oversize`; `if
      settings.PerUserQuotaBytes > 0 && currentUsage + incoming >
      settings.PerUserQuotaBytes → OverQuota`; else `Allowed`. (Size checked
      **first** — the per-file bound is the stronger, cheaper check.)
    - `StorageSettingsDocTypes` (ns `Kumunita.Core.Usage`):
      `public static void Configure(StoreOptions opts) { opts.Schema.For<CommunityStorageSettings>(); }`.
    - `IUploadGate` (**Web-only**, `Kumunita.Web.Security` or
      `Kumunita.Web`): `ActionResult? CheckUpload(long incomingBytes, string
      subjectId, CommunityStorageSettings settings, long envMaxBytes)` — maps
      `Oversize`/`OverQuota` → `StatusCode(413)` with a **distinct** message
      ("oversize" vs. "over quota"), `Allowed` → `null`. The single call-site
      each lane adopts (C-UP·3: the only place a `413` is produced).
  - `### 2.2 the four-lane adoption rule (C-UP·10/F10)` — for **each** of the
    four upload lanes (avatar / content-image / attachment / document), the
    lane's inline `if (mediaOpts.Value.MaxBytes > 0 && file.Length >
    mediaOpts.Value.MaxBytes) return 413;` is **replaced** by:
    1. `var settings = await storageSettings.GetOrCreateAsync();`
    2. `var usage = await storageSettings.GetPerUserUsageBytesAsync(subject);`
    3. `var reject = gate.CheckUpload(file.Length, subject, settings,
       mediaOpts.Value.MaxBytes); if (reject is not null) return reject;`
    — **before** `PutAsync`. The **allowlist** check (each lane's own
    `IsAllowed` / `IsAttachmentAllowed` / `IsDocumentAllowed`) is **untouched**
    and keeps its position; the empty-file → 400 check is **untouched**. The
    size gate's position (before the type gate, before `PutAsync`) is
    **preserved** — M25 changes *what* the size gate reads (admin doc + quota)
    and *how* it reports (a distinct over-quota 413), not *where* it runs.
    Pin the **exact** current line + surrounding context of each lane (from the
    entry reads) so U8 replaces the right lines.
  - `### 2.3 pinned Core tests (exact names)` — file
    `tests/Kumunita.Core.Tests/Usage/StorageLimitsTests.cs` (+
    `StorageSettingsServiceTests.cs` for the service seam):
    1. `Decide_Oversize_Rejects` (F2)
    2. `Decide_OverQuota_Rejects` (F3)
    3. `Decide_WithinBoth_Allows` (F1)
    4. `Decide_QuotaZero_Unlimited` (F5)
    5. `EffectiveMaxFileBytes_AdminOverrideBeatsEnv` (F4)
    6. `EffectiveMaxFileBytes_UnsetFallsBackToEnv` (F6)
    7. `GetPerUserUsage_SumsCreatedByIdOnly` (F7/F8)
    8. `GetPerUserUsage_OtherUsersExcluded` (F8)
    9. `SetAsync_PersistsInCallerSession` (C-UP·1)
    10. `Decide_SizeCheckedBeforeQuota` (C-UP·2 ordering)
  - `### 2.4 pinned Web tests (exact names)` — placed in the **per-lane** test
    files (U9 extends them): items 11–12 in
    `tests/Kumunita.Web.Tests/ProfileAvatarUploadTests.cs`, items 13–14 in
    `ContentImageUploadTests.cs`, items 15–16 in `AttachmentUploadTests.cs`,
    items 17–18 in `DocumentControllerTests.cs`, item 19 in
    `UploadGateTests.cs`; plus `AdminStorageControllerTests.cs` (items 20–21) +
    `ResidentUsageViewTests.cs` (items 22–23):
    11. `AvatarUpload_Oversize_413`
    12. `AvatarUpload_OverQuota_413`
    13. `ContentImageUpload_Oversize_413`
    14. `ContentImageUpload_OverQuota_413`
    15. `AttachmentUpload_Oversize_413`
    16. `AttachmentUpload_OverQuota_413`
    17. `DocumentUpload_Oversize_413`
    18. `DocumentUpload_OverQuota_413`
    19. `UploadGate_OversizeAndOverQuota_HaveDistinctMessages` (F2/F3)
    20. `AdminStorage_SetLimits_Persists` (C-UP·1)
    21. `AdminStorage_NonGlobalAdmin_Forbidden` (C-UP·1)
    22. `ResidentUsageView_SelfOnly` (F7)
    23. `ResidentUsageView_QuotaZeroShowsUnlimited` (F5)
  - `### 2.5 acceptance gate (U10 records)` — the three-test shape: **closed
    loop** (admin sets a quota + a resident uploads within it → success; the
    resident's self-view shows the new usage), **handoff** (the *next* resident
    or the same resident's *next* upload that would exceed the quota is rejected
    413 — strong consistency, live doc; F4/F3), **part-vs-whole** (the 23-test
    list is the whole; closed-loop + handoff are the parts; all must pass
    together).
  - `### 2.6 drift-guard (frozen once written)` — the 7 invariants (C-UP·1–7),
    the 10 FACES (F1–F10), the `CommunityStorageSettings` shape, the
    `IStorageSettingsService` 4-method surface, the `StorageLimits`/`StorageDecision`
    pure shape, the `StorageSettingsDocTypes.Configure` line, the `IUploadGate`
    shape, the four-lane adoption rule (§2.2), the 23 test names, and the
    **C-UP·6 milestone contract** (M24 `StatusDone` / M25 `StatusNext` at start;
    M25 `StatusDone` + M26 `StatusNext` at close) — all frozen pins. Any
    mismatch (e.g. M24's `IStorageMetricsService` landed and the per-user usage
    seam should be re-pointed to it, or M24 is not yet `StatusDone`) is a
    `## U<m> — Drift pause` per unit-series rule §6.
- **Exit:** file exists with all Part 2 sub-sections. **No build.** Handoff
  note (6–8 lines, `## U2 — design doc Part 2`): (a) the sealed Core seam
  signatures (the `IStorageSettingsService` 4 methods + the `StorageLimits`
  `Decide`), (b) the `IUploadGate` shape, (c) the 23 test names by id, (d) the
  four-lane adoption rule (the 4 call-sites), (e) the C-UP·6 contract.

### U3 — `CommunityStorageSettings` doc + `StorageSettingsDocTypes` + boot wiring
- **Goal:** create the `CommunityStorageSettings` POCO (ns
  `Kumunita.Core.Usage`) + a new `StorageSettingsDocTypes.Configure(StoreOptions)`
  registration surface + wire it into both boot paths (the dev loop in
  `Program.cs` and the all-env `SchemaBootstrap`). Mirrors the `M3DocTypes`
  pattern exactly. **Build green.**
- **Entry reads:** `src/Kumunita.Core/M3DocTypes.cs` (the shape to mirror);
  `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (where the existing
  `*.Configure(opts)` calls live — add `StorageSettingsDocTypes.Configure(opts)`
  next to them); `src/Kumunita.Web/Program.cs` (the dev-loop `Configure` call —
  add it next to the existing doc-surface calls); `docs/design/m25-upload-limits-design.md`
  §2.1 (the exact `CommunityStorageSettings` shape + the `StorageSettingsDocTypes`
  line); `src/Kumunita.Core/Usage/UsageAnalyticsService.cs` (confirm the
  `Kumunita.Core.Usage` namespace is where M25's types live).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Usage/CommunityStorageSettings.cs` — POCO per §2.1 (stable
    string `Id` = `"community"`; `long? MaxFileBytes`; `long PerUserQuotaBytes`;
    `DateTimeOffset Modified`; `string? ModifiedById`).
  - `src/Kumunita.Core/Usage/StorageSettingsDocTypes.cs` —
    `public static void Configure(StoreOptions opts) { opts.Schema.For<CommunityStorageSettings>(); }`.
  - `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` — add
    `StorageSettingsDocTypes.Configure(opts);` next to the existing doc-surface
    calls (one line added).
  - `src/Kumunita.Web/Program.cs` — add the `StorageSettingsDocTypes.Configure(...)`
    call in the dev-loop path, next to the existing calls (one line added).
- **Exit:** `dotnet build` on `Kumunita.Core` + `Kumunita.Web` green; the POCO
  + the doc-surface compile. **No new test** (U4's Core tests are the first M25
  tests). Handoff note (5 lines, `## U3 — doc + registration surface + boot`):
  (a) the `StorageSettingsDocTypes` line, (b) the two boot-path lines added
  (file + line numbers), (c) any compile warnings on the new POCO.

### U4 — `IStorageSettingsService` + the pure `StorageLimits` + the Core tests
- **Goal:** implement `IStorageSettingsService` (get-or-create / set /
  per-user-usage) + the pure `StorageLimits.Decide` in `Kumunita.Core.Usage`,
  register the service in `AddKumunitaCore`, and add the **pinned Core tests**
  (§2.3, items 1–10). **Build green + Core tests pass.**
- **Entry reads:** `src/Kumunita.Core/Usage/CommunityStorageSettings.cs`
  (U3's POCO); `docs/design/m25-upload-limits-design.md` §2.1 (the exact
  `IStorageSettingsService` + `StorageLimits` shapes);
  `src/Kumunita.Core/DependencyInjection.cs` (the `AddTransient` + factory
  registration shape to add); `src/Kumunita.Core/Usage/UsageAnalyticsService.cs`
  (the usage-context service shape to mirror for the `GetPerUserUsageBytesAsync`
  read — a `QuerySession` `Query<MediaObject>().Where(o => o.CreatedById ==
  subjectId)`); `tests/Kumunita.Core.Tests/Usage/` (the existing usage-context
  test harness to mirror) + `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the
  Testcontainers harness shape); `docs/plans-milestones/plan-m25-upload-limits.md`
  §2.5 (the *Running the tests* quirk).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Usage/IStorageSettingsService.cs` — the 4-method seam
    per §2.1 (get-or-create / set / per-user-usage / pure `Decide`).
  - `src/Kumunita.Core/Usage/StorageSettingsService.cs` — the impl:
    `GetOrCreateAsync` (read; **create-if-missing** with sentinel defaults —
    `MaxFileBytes = null` (env fallback), `PerUserQuotaBytes = 0` (unlimited));
    `SetAsync` (one in-caller-session write, `session.Store(...)` + `session
      .SaveChanges()`); `GetPerUserUsageBytesAsync`
    (`store.QuerySession()` — a **read** — `Query<MediaObject>()
    .Where(o => o.CreatedById == subjectId).Sum(o => o.SizeBytes)`); `Decide`
    (delegates to the pure `StorageLimits`).
  - `src/Kumunita.Core/Usage/StorageLimits.cs` — the **pure** static
    `Decide(long incoming, long currentUsage, CommunityStorageSettings s,
    long envMaxBytes)` per §2.1 (size first, then quota; `null`/`0` sentinels
    per C-UP·5). **No HTTP types, no `IFormFile`** (C-UP·3).
  - `tests/Kumunita.Core.Tests/Usage/StorageLimitsTests.cs` (+
    `StorageSettingsServiceTests.cs`) — the 10 pinned Core tests (§2.3, items
    1–10).
  - `src/Kumunita.Core/DependencyInjection.cs` — add
    `services.AddTransient<IStorageSettingsService, StorageSettingsService>();`
    (or the factory form if it needs the store — mirror
    `UsageAnalyticsService`'s registration).
- **Exit:** `dotnet build` green; `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` passes
  (the 10 new tests + the existing suite). Handoff note (5–6 lines, `## U4 —
  service + decision + Core tests`): (a) the 10 Core test names (by id) that
  passed, (b) the `GetPerUserUsageBytesAsync` read shape (the `Where`/`Sum`),
  (c) the `Decide` ordering (size-before-quota confirmed), (d) the DI
  registration line.

### U5 — Admin surface: `AdminStorageController` GET (settings + totals view) + nav link
- **Goal:** add the **GlobalAdmin-gated** `GET /admin/storage` view (the current
  admin-set per-file limit + per-user quota + the community total usage, read
  via `IStorageSettingsService`) + the view + the nav/`/admin` link. **No write
  lane yet** (U6). **Build green.**
- **Entry reads:** `src/Kumunita.Web/Controllers/AdminMessagingController.cs`
  or `AdminQuietController.cs` (the "admin sets a community setting" surface to
  mirror — the `[Authorize(Roles = Roles.GlobalAdmin)]` + the `GET`/`POST` pair
  + the view shape); `src/Kumunita.Web/Controllers/AdminController.cs` (the
  `/admin` hub + the section-link list U5 adds a row to);
  `docs/design/m25-upload-limits-design.md` §2.1 (the `IStorageSettingsService`
  read seam); `src/Kumunita.Web/Views/Admin/` (the existing admin view + the
  layout/CSHTML style); `src/Kumunita.Core/Usage/IStorageSettingsService.cs`
  (U4's read seam).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Controllers/AdminStorageController.cs` —
    `[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]`;
    `GET /admin/storage` → loads the settings (get-or-create) + the community
    total usage (the Σ over all `MediaObject.SizeBytes`, or reuses M24's metrics
    seam if present — see the drift-guard) + renders the view. **Read-only in
    U5** (the set lane is U6).
  - `src/Kumunita.Web/Models/AdminStorageViewModel.cs` — the view model
    (`MaxFileBytes?`, `PerUserQuotaBytes`, `TotalUsedBytes`, the effective
    per-file value, the quota-unlimited flag).
  - `src/Kumunita.Web/Views/Admin/Storage.cshtml` — the settings display (the
    effective per-file limit, the per-user quota or "unlimited", the community
    total used). The **form** (U6) is not required in U5 — the display is.
  - `src/Kumunita.Web/Views/Admin/Index.cshtml` (or the `/admin` hub) — add the
    "Storage" nav link row (mirror the existing section links).
- **Exit:** `dotnet build` on `Kumunita.Web` green; `GET /admin/storage`
  renders (GlobalAdmin session). **No new test** (U6's `AdminStorageControllerTests`
  are the first admin-surface tests). Handoff note (4–5 lines, `## U5 — admin
  GET surface`): (a) the `AdminStorageController` route + the view model shape,
  (b) the nav link added (file + line), (c) whether the community-total reads
  via M24's seam or a local Σ (the drift-guard note).

### U6 — Admin surface: `AdminStorageController` POST set-limits lane + admin Web tests
- **Goal:** add the **GlobalAdmin-gated** `POST /admin/storage/limits` lane (the
  admin sets the per-file limit + the per-user quota) that calls
  `IStorageSettingsService.SetAsync` **in the caller's session** (one write,
  C-UP·1) + the form in `Storage.cshtml` + the **pinned admin Web tests**
  (§2.4, items 20–21). **Build green + the 2 admin tests pass.**
- **Entry reads:** U5's `AdminStorageController.cs` (add the `POST` action
  beside the `GET`); `docs/design/m25-upload-limits-design.md` §2.1 (the
  `SetAsync` shape) + §2.4 (the admin Web tests);
  `src/Kumunita.Web/Controllers/AdminMessagingController.cs` or
  `AdminQuietController.cs` (the `POST` set-lane + the `[ValidateAntiForgeryToken]`
  + the redirect-after-save shape to mirror);
  `src/Kumunita.Core/Usage/IStorageSettingsService.cs` (the `SetAsync` seam);
  `tests/Kumunita.Web.Tests/` (the existing admin-controller test harness to
  mirror — the GlobalAdmin principal + the NSubstitute store shape).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Controllers/AdminStorageController.cs` — add
    `POST /admin/storage/limits` (`[ValidateAntiForgeryToken]`): read the two
    values (per-file limit — optional/blank ⇒ `null`; per-user quota — `0` ⇒
    unlimited), call `SetAsync(maxFileBytes, perUserQuotaBytes, subject,
    session)` in the caller's session, redirect back to `GET /admin/storage`.
  - `src/Kumunita.Web/Views/Admin/Storage.cshtml` — add the **form** (two inputs
    + submit) that posts to `POST /admin/storage/limits` (the U5 display stays).
  - `src/Kumunita.Web/Models/AdminStorageViewModel.cs` — extend with the form
    field values (`MaxFileBytesInput`, `PerUserQuotaBytesInput`) if not already
    present.
  - `tests/Kumunita.Web.Tests/AdminStorageControllerTests.cs` — the 2 pinned
    admin Web tests (§2.4, items 20–21: `AdminStorage_SetLimits_Persists`,
    `AdminStorage_NonGlobalAdmin_Forbidden`).
- **Exit:** `dotnet build` green; `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` passes
  (the 2 new admin tests + the existing suite). Handoff note (5 lines, `## U6 —
  admin POST set-lane`): (a) the `POST` action + the `SetAsync` call-site, (b)
  the 2 admin test names that passed, (c) the redirect target, (d) the
  blank-per-file ⇒ `null` (env fallback) handling.

### U7 — Resident self-usage view (own usage / quota / remaining) + its Web tests
- **Goal:** add the **resident self-view** — a signed-in resident sees **their
  own** usage bytes, their per-user quota (or "unlimited"), and how much of
  their quota remains — via a read action on an existing resident surface (the
  profile/account controller) + the view model + the view + the **pinned
  resident Web tests** (§2.4, items 22–23). **Build green + the 2 resident
  tests pass.**
- **Entry reads:** `src/Kumunita.Web/Controllers/AccountController.cs` or
  `ProfileController.cs` (the resident surface to add the read action to — the
  self-scoped principal is already minted there);
  `docs/design/m25-upload-limits-design.md` §2.1 (the
  `GetPerUserUsageBytesAsync` + the settings read) + §2.4 (the resident Web
  tests); `src/Kumunita.Core/Usage/IStorageSettingsService.cs` (the read seams);
  `src/Kumunita.Web/Views/Account/` or `Views/Profile/` (the resident view
  style); `tests/Kumunita.Web.Tests/` (the resident-controller test harness to
  mirror — the signed-in resident principal + the NSubstitute store shape).
- **Deliverables (≤ 4 files):**
  - The resident controller (U7's chosen one — `AccountController`/`ProfileController`)
    — add a **read-only** action (e.g. `GET /account/storage` or
    `GET /profile/storage`): loads **the subject's own** usage
    (`GetPerUserUsageBytesAsync(subject)`) + the community quota setting
    (`GetOrCreateAsync()`), computes the remaining (`quota - usage`, or
    "unlimited" when `quota == 0`), renders the view. **Self-only** — the
    subject is the signed-in principal (minted server-side), never a path param
    (C-UP·4/F7; never another resident's numbers).
  - `src/Kumunita.Web/Models/ResidentStorageViewModel.cs` — the view model
    (`MyUsageBytes`, `PerUserQuotaBytes`, `RemainingBytes` (or `null` ⇒
    unlimited), the human-readable sizes).
  - `src/Kumunita.Web/Views/Account/Storage.cshtml` (or `Views/Profile/Storage.cshtml`)
    — the resident's usage / quota / remaining display (or "unlimited" when the
    quota is `0`).
  - `tests/Kumunita.Web.Tests/ResidentUsageViewTests.cs` — the 2 pinned resident
    Web tests (§2.4, items 22–23: `ResidentUsageView_SelfOnly`,
    `ResidentUsageView_QuotaZeroShowsUnlimited`).
- **Exit:** `dotnet build` green; `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` passes
  (the 2 new resident tests + the existing suite). Handoff note (4–5 lines,
  `## U7 — resident self-view`): (a) the route + the view model shape, (b) the
  2 resident test names that passed, (c) the self-only (subject = signed-in
  principal) pin, (d) the "unlimited" rendering (quota `0`).

### U8 — The Web gate (`IUploadGate`) + the four-lane enforcement wiring
- **Goal:** add the **Web-only** `IUploadGate` (maps the pure Core decision to a
  `413` `ActionResult` with a distinct oversize/over-quota message) + **adopt
  it in all four upload lanes** (avatar / content-image / attachment /
  document), replacing each lane's inline `MaxBytes` 413 check with the
  get-or-create + per-user-usage + gate sequence (the §2.2 adoption rule),
  **before** `PutAsync`. **No new test yet** (U9). **Build green.**
- **Entry reads:** `docs/design/m25-upload-limits-design.md` §2.1 (the
  `IUploadGate` shape) + §2.2 (the exact four-lane adoption rule + the pinned
  current lines per lane); `src/Kumunita.Web/Controllers/ProfileController.cs`
  (`AvatarUpload` — the first lane); `src/Kumunita.Web/Controllers/ContentImageController.cs`
  (the second); `src/Kumunita.Web/Controllers/AttachmentController.cs` (the
  third); `src/Kumunita.Web/Controllers/DocumentController.cs` (the fourth);
  `src/Kumunita.Core/Usage/IStorageSettingsService.cs` (the get-or-create +
  per-user-usage + `Decide` seams); `src/Kumunita.Web/Program.cs` /
  `DependencyInjection` (where to register `IUploadGate` — the Web host).
- **Deliverables (≤ 4 files — the four controllers + the gate; the gate is one
  small file, the four controller edits are the rest):**
  - `src/Kumunita.Web/Security/IUploadGate.cs` (or `Kumunita.Web/UploadGate.cs`)
    — the Web-only gate per §2.1: `CheckUpload(long incoming, string subject,
    CommunityStorageSettings settings, long envMaxBytes) → ActionResult?`
    (maps `Oversize`/`OverQuota` → `StatusCode(413)` with a **distinct**
    message; `Allowed` → `null`). **The only place a `413` is produced**
    (C-UP·3). Register in the Web host DI.
  - `src/Kumunita.Web/Controllers/ProfileController.cs` — in `AvatarUpload`,
    replace the inline `MaxBytes` 413 check with the §2.2 sequence (get-or-create
    → per-user-usage → gate → `if (reject is not null) return reject;`)
    **before** `PutAsync`; keep the empty-file → 400 + the allowlist check
    **untouched** and in their original positions.
  - `src/Kumunita.Web/Controllers/ContentImageController.cs` — the same
    replacement in the content-image upload action (same guard order preserved).
  - `src/Kumunita.Web/Controllers/AttachmentController.cs` — the same
    replacement in the attachment upload action (same guard order preserved).
  - `src/Kumunita.Web/Controllers/DocumentController.cs` — the same replacement
    in the document upload action (same guard order preserved).
- **Exit:** `dotnet build` on `Kumunita.Web` green; the four lanes each call the
  gate **before** `PutAsync`; the allowlist + empty-file checks are untouched.
  **No new test** (U9's per-lane tests are the enforcement tests). Handoff note
  (5 lines, `## U8 — gate + four-lane wiring`): (a) the `IUploadGate` shape +
  the DI registration, (b) the four call-sites replaced (file + the action
  name + the line where the old `MaxBytes` check was), (c) confirmation the
  allowlist + empty-file checks are untouched, (d) the distinct oversize/
  over-quota messages.

### U9 — Per-lane enforcement Web tests (size + quota, all four lanes)
- **Goal:** add the **pinned Web enforcement tests** (§2.4, items 11–19) — each
  of the four lanes rejects **oversize** → 413 and **over-quota** → 413, with a
  **distinct** message — via the existing per-lane test harness (the
  `TestFormFile` byte-backed `IFormFile` carrier the codebase already uses).
  **Build green + the 9 enforcement tests pass.**
- **Entry reads:** U8's four controllers (the gate call-sites);
  `docs/design/m25-upload-limits-design.md` §2.4 (the 9 enforcement test names)
  + §2.2 (the adoption rule the tests drive);
  `tests/Kumunita.Web.Tests/ProfileAvatarUploadTests.cs` +
  `ContentImageUploadTests.cs` + `AttachmentUploadTests.cs` +
  `DocumentControllerTests.cs` (the **existing** per-lane oversize-413 tests to
  extend with the over-quota case + the `TestFormFile` carrier + the
  `BuildUploadController(..., maxBytes: N)` shape);
  `src/Kumunita.Web/Security/IUploadGate.cs` (U8's gate — the messages to
  assert).
- **Deliverables (≤ 4 files — one test file per lane, or a single shared file
  if the harness is shared):**
  - `tests/Kumunita.Web.Tests/ProfileAvatarUploadTests.cs` — add
    `AvatarUpload_Oversize_413` + `AvatarUpload_OverQuota_413` (item 11–12).
  - `tests/Kumunita.Web.Tests/ContentImageUploadTests.cs` — add
    `ContentImageUpload_Oversize_413` + `ContentImageUpload_OverQuota_413`
    (item 13–14).
  - `tests/Kumunita.Web.Tests/AttachmentUploadTests.cs` — add
    `AttachmentUpload_Oversize_413` + `AttachmentUpload_OverQuota_413`
    (item 15–16).
  - `tests/Kumunita.Web.Tests/DocumentControllerTests.cs` — add
    `DocumentUpload_Oversize_413` + `DocumentUpload_OverQuota_413` (item 17–18);
    and `UploadGate_OversizeAndOverQuota_HaveDistinctMessages` (item 19) in the
    gate's own test file (`tests/Kumunita.Web.Tests/UploadGateTests.cs`).
- **Exit:** `dotnet build` green; `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` passes
  (the 9 new enforcement tests + the existing suite). Handoff note (5 lines,
  `## U9 — per-lane enforcement tests`): (a) the 9 test names (by lane) that
  passed, (b) the distinct oversize/over-quota messages asserted, (c) the
  over-quota fixture shape (a pre-seeded `MediaObject` usage + a settings doc
  with a small quota), (d) any lane whose guard order had to be confirmed
  against U8's call-site.

### U10 — Acceptance gate (three-test shape) + FACES tests + the recorded close
- **Goal:** add the **acceptance gate** (§2.5) as three named end-to-end tests
  (closed-loop / handoff / part-vs-whole) + the remaining **FACES**-anchored
  tests not yet covered (F1 success path, F4 admin-raises-limit, F5
  quota-`0`-unlimited, F6 env-fallback, F9 no-byte-written-on-reject) and
  **record the M25 acceptance** in the handoff note. **Build green + the
  acceptance + FACES tests pass.**
- **Entry reads:** `docs/design/m25-upload-limits-design.md` §2.5 (the
  acceptance gate) + the FACES table (U1's Part 1);
  `tests/Kumunita.Core.Tests/Usage/StorageLimitsTests.cs` + the four
  per-lane test files U9 extended (`tests/Kumunita.Web.Tests/ProfileAvatarUploadTests.cs`,
  `ContentImageUploadTests.cs`, `AttachmentUploadTests.cs`,
  `DocumentControllerTests.cs`) + `tests/Kumunita.Web.Tests/UploadGateTests.cs`
  (the seams the acceptance tests drive);
  `tests/Kumunita.Web.Tests/M15AcceptanceTests.cs` or a comparable milestone
  acceptance-test file (the **three-test shape** to mirror — closed-loop / handoff / part-vs-whole);
  `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the Testcontainers harness the
  acceptance tests need for the live-doc strong-consistency case, F4).
- **Deliverables (≤ 3 files):**
  - `tests/Kumunita.Core.Tests/Usage/StorageLimitsAcceptanceTests.cs` — the
    three acceptance tests (closed-loop: admin sets a quota + a resident uploads
    within it → success + the usage is reflected; handoff: the next upload that
    would exceed the quota is rejected 413 — strong consistency, live doc, F4/F3;
    part-vs-whole: the 23-test list is the whole; all pass together).
  - `tests/Kumunita.Core.Tests/Usage/StorageLimitsTests.cs` — add the
    FACES-anchored Core tests not yet covered (F1 `Decide_Allows_WithinBoth`
    already as item 3; add F5/F6 if not already the `QuotaZero_Unlimited` /
    `UnsetFallsBackToEnv` items 4–6; add F9 no-byte-written at the Web level
    below).
  - One representative per-lane test file (e.g.
    `tests/Kumunita.Web.Tests/ProfileAvatarUploadTests.cs`) — add the F9
    **no-byte-written** assertion (an over-quota / oversize reject writes **no**
    volume file — assert the `IMediaStore.PutAsync` was **not** called / no new
    `MediaObject` row); one representative lane is sufficient to pin C-UP·2/F9 —
    note **which** lane in the handoff note.
- **Exit:** `dotnet build` green; `dotnet exec tests\Kumunita.Core.Tests\...
  dll` + `dotnet exec tests\Kumunita.Web.Tests\... dll` both pass (the
  acceptance + FACES tests + the full suites). Handoff note (6–8 lines, `## U10
  — acceptance gate + FACES`): (a) the three acceptance test names (by name)
  that passed, (b) the FACES covered (F1/F4/F5/F6/F9) + which lane pinned F9,
  (c) the strong-consistency (live-doc) assertion (F4), (d) the total M25 test
  count (Core + Web) that now pass.

### U11 — ADR 0134 + OPS.md + README Roadmap (the docs sync, *except* the milestone flip)
- **Goal:** author **ADR 0134** (Upload limits: admin-set per-file limit +
  per-user quota over the content-addressed store; `Media__MaxBytes` as
  fallback; the pure-Core decision + the Web gate; the resident self-view; the
  named non-decisions) + update **OPS.md** (the `Media__MaxBytes` row: it is
  now the **fallback** for the admin-set per-file limit, still the per-user
  cap is admin-set in-app) + update the **README Roadmap** M25 entry (still
  **in progress** — the **status flip is U12's**, not this unit's). **No code,
  no build.**
- **Entry reads:** `docs/adr/0133-dark-theme-forest.md` (the current highest ADR
  number — ADR 0134 is next); `docs/adr/0011-media-and-file-storage.md` (the
  C-MED invariants + the `MaxBytes` boundary ADR 0134 amends);
  `docs/adr/0034-file-attachments-posts-replies-announcements.md` (the per-lane
  gate ADR 0134 generalizes); `docs/adr/0120-guest-accounts.md` or the M9/M20 ADR
  (the "admin sets a community setting" precedent ADR 0134 mirrors);
  `docs/OPS.md` (the `Media__MaxBytes` row to annotate); `README.md` (the M25
  Roadmap entry); `docs/design/m25-upload-limits-design.md` (the full M25
  decision record the ADR distills); the handoff note (U1–U10's recorded
  outcomes, especially the test count + the four-lane wiring + the non-decisions).
- **Deliverables (≤ 3 files):**
  - `docs/adr/0134-upload-limits.md` — the full ADR (Context / Decision /
    Consequences, mirroring ADR 0011's style). The Decision: the
    `CommunityStorageSettings` doc (the admin-set per-file limit + per-user
    quota) + the env `Media__MaxBytes` as **fallback** (C-UP·5) + the **pure
    Core** `StorageLimits` decision + the **Web** `IUploadGate` adopted by the
    four upload lanes **before** `PutAsync` (guards-before-write, C-UP·2) + the
    **content-addressed** per-user usage (C-UP·4) + the **resident self-view**
    + the **named non-decisions** (per-resident overrides; the admin metrics
    tables = M24; serve-time enforcement; no `IMediaStore`/`MediaObject`
    reshape). Amends: 0011 (the `MaxBytes` boundary becomes the fallback), 0034
    (the per-lane gate is generalized).
  - `docs/OPS.md` — annotate the `Media__MaxBytes` row: it is now the
    **fallback** for the admin-set per-file limit (the admin surface is the
    primary); note the per-user quota is admin-set in-app (not an env knob).
  - `README.md` — update the M25 Roadmap entry text if the scope/naming needs it
    (the **status** stays as-is — the flip is U12's).
- **Exit:** the ADR + OPS + README edits exist and are consistent with the
  design doc. **No build.** Handoff note (4–5 lines, `## U11 — ADR + OPS +
  README`): (a) the ADR number (0134) + the amends (0011/0034), (b) the OPS
  `Media__MaxBytes` annotation, (c) the README M25 entry change, (d) confirmation
  the **status flip is deferred to U12**.

### U12 — Close: `Milestones.cs` + `MilestonesTests.cs` flip + design-doc close + move to `done/`
- **Goal:** **Close M25** — flip `Milestones.cs` (M25 → `StatusDone`, promote
  M26 → `StatusNext`), update `MilestonesTests.cs` (the done-list gains M25;
  the single-in-progress test is re-pointed to M26), append the
  **"M25 — Closed (recorded)"** section to the design doc, append the final
  handoff section, **move the plan + handoff note to `done/m25/`**, and clean
  up any `.tmp/` scratch. **Build green + `MilestonesTests` pass + the full
  suites pass.**
- **Entry reads:** `src/Kumunita.Web/Milestones.cs` (the M25 `StatusPlanned` →
  `StatusDone` + the M26 `StatusPlanned` → `StatusNext` flip — the C-UP·6
  contract); `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the
  `Shipped_Milestones_Are_Marked_Done` done-list + the
  `M24_Is_The_Single_InProgress_Milestone` test to re-point to M26);
  `docs/design/m25-upload-limits-design.md` (append the "## M25 — Closed
  (recorded)" section **last**, mirroring the M3/M22 close);
  `docs/plans-milestones/in-progress/m25-handoff-notes.md` (append the final
  `## U12 — close` section); `docs/plans-milestones/done/m22/` (the close-unit
  precedent — the `plan-*.md` + `*-handoff-notes.md` both move to the lane
  folder); the handoff note (U10's recorded test count + the four-lane wiring
  — the honest "Closed" close).
- **Deliverables (≤ 4 files + the moves):**
  - `src/Kumunita.Web/Milestones.cs` — flip M25 to `StatusDone` + M26 to
    `StatusNext` (the C-UP·6 close; the M25 title is unchanged).
  - `tests/Kumunita.Web.Tests/MilestonesTests.cs` — add M25 to the
    `Shipped_Milestones_Are_Marked_Done` done-list; re-point the
    `M24_Is_The_Single_InProgress_Milestone` test to assert the single
    `StatusNext` is now **M26** (and M25 `StatusDone`), keeping the same
    two-test shape + the order assertion (`M24, M25, M26, …` unchanged).
  - `docs/design/m25-upload-limits-design.md` — append the
    `## M25 — Closed (recorded)` section **last** (the M24-reuse note, the four
    non-decisions, the test count, the ADR 0134 pointer, the M26-handoff note).
  - `docs/plans-milestones/in-progress/m25-handoff-notes.md` — append the final
    `## U12 — close` section (the M25 close: the milestone flip, the test count,
    the docs sync, the handoff to M26).
  - **Moves:** `docs/plans-milestones/plan-m25-upload-limits.md` →
    `docs/plans-milestones/done/m25/plan-m25-upload-limits.md`;
    `docs/plans-milestones/in-progress/m25-handoff-notes.md` →
    `docs/plans-milestones/done/m25/m25-handoff-notes.md`; the **12 unit plans**
    `docs/plans-milestones/in-progress/m25-u0*.md` … `m25-u12.md` →
    `docs/plans-milestones/done/m25/` (the completed unit register). (This is
    the "move to `done/`" step the user specified — the lane folder
    `done/m25/` matches the repo's `done/<lane>/` convention.)
- **Exit:** `dotnet build` green; `dotnet exec tests\Kumunita.Web.Tests\...
  dll` passes (including the re-pointed `MilestonesTests`); `dotnet exec
  tests\Kumunita.Core.Tests\... dll` passes; the plan + handoff note + unit
  plans are in `done/m25/`; `in-progress/` no longer holds the M25 files.
  Handoff note (6–8 lines, `## U12 — close`): (a) the `Milestones.cs` flip
  (M25 → done, M26 → next) + the `MilestonesTests.cs` re-point, (b) the "Closed"
  section appended, (c) the files moved to `done/m25/`, (d) the **handoff to
  M26** (Sorting — the next milestone; its start precondition is that M25 is
  `StatusDone` and M26 is `StatusNext`, per C-UP·6).

---

## Open questions (to resolve in U1/U2, before any unit implements)

1. **Per-user quota model — community-wide single value vs. per-resident
   override.** The plan pins a **single community-wide per-user quota** the
   admin sets (all residents share it); per-resident *individual* overrides are
   a **named non-decision** (future lane). If the user wants per-resident
   overrides in M25, U2 must widen `CommunityStorageSettings` to a per-resident
   map (a `PerUserQuota` doc keyed by subjectId) — a **scope expansion** that
   changes U3/U4/U6/U7. **Default: single community-wide value.**
2. **The per-user usage seam — M24's `IStorageMetricsService` or M25's own
   `GetPerUserUsageBytesAsync`.** The plan defaults to M25 shipping its own
   `GetPerUserUsageBytesAsync` (C-UP·4) and **re-pointing** to M24's seam if it
   has landed (the drift-guard, unit-series rule §6). **Default: M25's own
   seam, reconciled at U12 close.**
3. **The resident self-view surface — `AccountController` vs. `ProfileController`.**
   The plan defers the choice to U7 (entry reads). **Default: `AccountController`**
   (the account-level surface), unless the profile surface is the resident's
   canonical "my usage" home.
4. **The admin total-usage in U5 — M24's metrics seam or a local Σ.** The plan
   defers to U5 (entry reads). **Default: a local `Σ SizeBytes` over all
   `MediaObject`** if M24's seam is not yet present (the drift-guard).

These are **defaults, not blockers** — U1/U2 resolve them into the design doc's
pin, and the drift-guard (unit-series rule §6) is the escape valve if reality
disagrees with a pin.
