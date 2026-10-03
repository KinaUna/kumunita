# M25 — Upload limits · design

> **M25 (Upload limits)** is a **settings + enforcement** lane over the *existing*
> media byte store (ADR 0011). It does **not** add a new store, catalog doc,
> `AccessAction`, or bounded context — it reuses `IMediaStore` + `MediaObject`
> (content-addressed, `SizeBytes`, `CreatedById`) and the existing
> `Kumunita.Core.Usage` context.
>
> **This document has two parts.** Part 1 (below) is *what/why*: Context,
> Scope, the 7 invariants (C-UP·1–7), and the 10 FACES (F1–F10) — authored by
> U1. Part 2 (*how* — the exact C# seams, the four-lane adoption rule, the
> pinned test names, the acceptance gate, and the drift-guard) is authored by
> U2 and appended after the Part 1 boundary. The invariant / FACES ids and
> names below are **frozen once written**; do not re-derive or rename them in a
> later unit.

---

## Context

M24 (Storage metrics) ships an **admin view of storage** — total used space,
available space, user-content used space, and space used per user. It is a
*read* surface: it tells an admin *how much* is on the box, but it gives the
admin **no control** and the resident **no self-view**.

M25 closes that gap. It makes two numbers **admin-set in-app** (rather than
operator-tuned env vars only) and surfaces one number **to the resident**:

- **per-file size limit** — an admin can set the max bytes for a single upload
  from inside the app (today it is the operator-tuned env `Media__MaxBytes`,
  5 MiB default, per ADR 0011). The env value remains the **fallback** when the
  admin has not set one.
- **per-user total content quota** — an admin can set a resident's total
  content cap. Quota `0` means **unlimited**.
- **resident self usage** — a resident can see their own usage, their quota,
  and how much remains.

M25 builds on, and does not reshape, the content-addressed store: a resident's
usage is simply the sum of the `SizeBytes` of the unique files *they first
stored* (`CreatedById == subjectId`), so content-addressing dedup is inherent —
two residents who upload the *same* bytes each count toward their own usage
only once (and only the first storer counts the file at all).

### What M25 does *not* do (non-decisions)

- **Not a new store or a new doc for the bytes** — the `MediaObject` catalog
  and the `IMediaStore` byte path are untouched (C-MED·4/6 from ADR 0011 hold).
- **Not per-resident quota overrides** — the quota is one community-wide number
  the admin sets; M25 does not let an admin pick a different quota for a single
  resident. (That is a later, out-of-scope lane.)
- **Not the admin *metrics* tables** — the per-user / total usage *dashboard*
  is M24's surface. M25 reuses the same `Σ SizeBytes WHERE CreatedById` read but
  does not add the admin-facing metrics view.
- **Not serve-time enforcement** — a file already stored is still served; M25
  only gates *new* uploads at the write boundary.
- **No `IMediaStore` / `MediaObject` reshape** — no new field on `MediaObject`,
  no new method on `IMediaStore`.

---

## Scope

### In

- The **`CommunityStorageSettings`** Marten doc (the single community storage
  settings record) + the **`StorageSettingsDocTypes`** doc surface + its
  **boot-path wiring** (registered once, the same way as the existing doc
  surfaces).
- **`IStorageSettingsService`** — get-or-create the settings, the single admin
  write lane, the per-user usage read, and the **pure** decision function.
- The pure **`StorageLimits`** decision (size + quota, size checked first) —
  an HTTP-free Core function.
- The **`IUploadGate`** Web gate, adopted by **all four** upload lanes
  (avatar, content-image, attachment, document) **before** `PutAsync`, replacing
  each lane's inline `> MaxBytes → 413` check.
- The **`/admin/storage`** admin surface (set the per-file limit + the per-user
  quota).
- The **resident self-usage** view (own usage, own quota, remaining).
- The **pinned Core + Web tests** (names frozen in Part 2).
- The **ADR** (a new decision record) + **OPS / README / `Milestones.cs`**
  sync.

### Out (named non-decisions)

- Per-resident quota overrides.
- The admin *metrics* tables (M24's surface).
- Serve-time enforcement.
- Any `IMediaStore` / `MediaObject` reshape.

---

## Invariants (pinned for M25)

The 7 invariants. Ids and names are **frozen** once written.

- **C-UP·1** — the per-file limit + per-user quota are **GlobalAdmin-set** as a
  single community doc; the env `Media__MaxBytes` is the **fallback**. Not
  per-request spoofable.
- **C-UP·2** — **guards before write**: size + quota are checked **before**
  `PutAsync`; on a reject, **no byte is written**.
- **C-UP·3** — **`Core` stays HTTP-free** (ADR 0006-D): the decision is a pure
  Core function; the `ActionResult` mapping is Web-only.
- **C-UP·4** — **content-addressed usage**: a user's usage =
  `Σ MediaObject.SizeBytes WHERE CreatedById == subjectId`; dedup inherent.
- **C-UP·5** — **sentinel semantics**: quota `0` = **unlimited**; per-file
  limit unset/`0` = the env `Media__MaxBytes` default.
- **C-UP·6** — **single-in-progress milestone contract**: M25 starts only
  after M24 is `StatusDone`; M25's close promotes M26.
- **C-UP·7** — **no new authorization surface**: the check is at the
  authenticated upload boundary (self-scoped); it adds **no** `AccessAudit`
  row / `AccessAction`; it only changes the *reject* shape (413, distinct).

---

## FACES (pinned, 10)

F1–F10, each bound to an invariant. Names are **frozen** once written.

- **F1** — under quota, within limit → success (behavior preserved) — C-UP·2
- **F2** — over the admin-set per-file limit → 413 **oversize** — C-UP·2, C-UP·5
- **F3** — upload pushes resident over quota → 413 **over-quota** (distinct) —
  C-UP·2, C-UP·4
- **F4** — admin raises the limit; the next within-limit upload succeeds — C-UP·1
- **F5** — quota `0` → unlimited; size still enforced — C-UP·5
- **F6** — admin-unset limit → env `Media__MaxBytes` fallback — C-UP·1, C-UP·5
- **F7** — resident self-view shows own usage/quota/remaining; never another's
  — C-UP·4
- **F8** — a byte uploaded by A is not in B's usage (first-storer attribution)
  — C-UP·4
- **F9** — no byte written on a size/quota reject — C-UP·2
- **F10** — enforcement is consistent across all four upload lanes — C-UP·2,
  C-UP·7

---

## Seams & contracts (Part 2, written by U2)

> *Part 2 is `how` — the exact C# shapes each later unit implements, the
> four-lane adoption rule, the pinned test names, the acceptance gate, and the
> drift-guard. The invariant / FACES ids are those frozen in Part 1; the C#
> shapes below are **frozen once written** (see §2.6). All new Core types live
> in the existing `Kumunita.Core.Usage` context (the M24 home) — M25 adds no
> new bounded context (Part 1, Context).*

### 2.1 New Core-owned types (exact C#) — namespace `Kumunita.Core.Usage`

All four Core types (`CommunityStorageSettings`, `IStorageSettingsService`,
`StorageDecision`, `StorageLimits`) are **HTTP-free** (C-UP·3 / ADR 0006-D) and
live in `Kumunita.Core.Usage`, mirroring the M24 `IStorageMetricsService` /
`StorageMetricsService` pair already in that context. `IUploadGate` is the
**Web-only** gate (it returns `ActionResult`, so it cannot cross into Core).

```csharp
namespace Kumunita.Core.Usage;

/// <summary>
/// The single community storage-settings doc (C-UP·1). Stable string
/// <c>Id = "community"</c> — one row per community (single-neighborhood
/// platform, ADR 0002). Sentinel semantics (C-UP·5): <see cref="MaxFileBytes"/>
/// <c>null</c> ⇒ env <c>Media__MaxBytes</c> fallback; <see
/// cref="PerUserQuotaBytes"/> <c>0</c> ⇒ unlimited.
/// </summary>
public sealed class CommunityStorageSettings
{
    public string Id { get; set; } = "community";

    /// <summary>Admin per-file override in bytes. <c>null</c>/absent ⇒ env
    /// fallback (C-UP·1/5).</summary>
    public long? MaxFileBytes { get; set; }

    /// <summary>Per-user total content quota in bytes. <c>0</c> = unlimited
    /// (C-UP·5).</summary>
    public long PerUserQuotaBytes { get; set; }

    public DateTimeOffset Modified { get; set; }
    public string? ModifiedById { get; set; }
}

public enum StorageDecision { Allowed, Oversize, OverQuota }

/// <summary>
/// The M25 settings seam (C-UP·1/3/4). <see cref="SetAsync"/> is the **single
/// admin** write lane — one write in the caller's session (C-UP·1),
/// GlobalAdmin-gated in Web. <see cref="GetPerUserUsageBytesAsync"/> is the
/// C-UP·4 read and **reuses the M24 C-SM·7 handoff seam** (§2.6) — it is not
/// re-implemented. <see cref="Decide"/> is the **pure** decision (C-UP·3).
/// </summary>
public interface IStorageSettingsService
{
    /// <summary>
    /// Read the community settings; **create-if-missing** with sentinel
    /// defaults (<c>MaxFileBytes = null</c>, <c>PerUserQuotaBytes = 0</c>).
    /// </summary>
    Task<CommunityStorageSettings> GetOrCreateAsync(CancellationToken ct);

    /// <summary>
    /// The single **admin** write lane: set the per-file override + the
    /// per-user quota, stamped with the actor, in the **caller's** session
    /// (C-UP·1: one in-caller-session write — the C3 same-transaction lane).
    /// </summary>
    Task SetAsync(long? maxFileBytes, long perUserQuotaBytes, string actorId,
        Marten.IDocumentSession session);

    /// <summary>
    /// The C-UP·4 read: <c>Σ MediaObject.SizeBytes WHERE CreatedById ==
    /// subjectId</c>. **Reuses** the M24 <see cref="IStorageMetricsService"/>
    /// C-SM·7 handoff seam of the identical signature (delegate or re-point —
    /// §2.6); do **not** duplicate the query.
    /// </summary>
    Task<long> GetPerUserUsageBytesAsync(string subjectId, CancellationToken ct);

    /// <summary>
    /// The **pure** decision (C-UP·3: no HTTP, no session). Delegates to
    /// <see cref="StorageLimits.Decide"/>.
    /// </summary>
    StorageDecision Decide(long incomingBytes, long currentUsageBytes,
        CommunityStorageSettings settings, long envMaxBytes);
}

/// <summary>
/// The **pure** M25 decision (C-UP·3). <see cref="Decide"/> is size-first
/// (C-UP·2 ordering). <c>0</c> for the effective max = **unlimited** file
/// size; <c>0</c> for the quota = **unlimited** quota (C-UP·5).
/// </summary>
public static class StorageLimits
{
    /// <summary>
    /// <c>settings.MaxFileBytes ?? envMaxBytes</c>; <c>0</c> = unlimited file
    /// size (C-UP·5).
    /// </summary>
    public static long EffectiveMaxFileBytes(CommunityStorageSettings settings,
        long envMaxBytes) => settings.MaxFileBytes ?? envMaxBytes;

    /// <summary>
    /// Size checked **first** (C-UP·2 ordering — test 10): over the effective
    /// max → <see cref="StorageDecision.Oversize"/> (F2); else over the
    /// per-user quota → <see cref="StorageDecision.OverQuota"/> (F3); else
    /// <see cref="StorageDecision.Allowed"/> (F1).
    /// </summary>
    public static StorageDecision Decide(long incomingBytes, long currentUsageBytes,
        CommunityStorageSettings settings, long envMaxBytes)
    {
        var effectiveMax = EffectiveMaxFileBytes(settings, envMaxBytes);
        if (effectiveMax > 0 && incomingBytes > effectiveMax)
            return StorageDecision.Oversize;
        if (settings.PerUserQuotaBytes > 0
            && currentUsageBytes + incomingBytes > settings.PerUserQuotaBytes)
            return StorageDecision.OverQuota;
        return StorageDecision.Allowed;
    }
}

/// <summary>
/// The M25 settings doc surface (ADR 0004 §B.1 — a parallel surface to
/// <c>UsageDocTypes</c> / <c>MediaDocTypes</c>, not additive on an existing
/// one: <see cref="CommunityStorageSettings"/> uses the conventional string
/// <c>Id</c>, so no non-default convention or business-key index is pinned).
/// Without the U3 boot-path call the doc is invisible to Marten (the
/// M3/Media/Usage precedent).
/// </summary>
public static class StorageSettingsDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        opts.Schema.For<CommunityStorageSettings>();
    }
}
```

And the **Web-only** gate (in `Kumunita.Web`, **not** Core — C-UP·3):

```csharp
namespace Kumunita.Web;

/// <summary>
/// The **single** `413`-producing call-site every upload lane adopts (C-UP·3,
/// C-UP·7): maps <see cref="StorageDecision.Oversize"/> /
/// <see cref="StorageDecision.OverQuota"/> → <c>StatusCode(413)</c> with a
/// **distinct** message (F2 vs F3), and <see cref="StorageDecision.Allowed"/>
/// → <c>null</c>. One `ActionResult` mapping, Web-only — Core never sees an
/// <c>IActionResult</c> (ADR 0006-D).
/// </summary>
public interface IUploadGate
{
    /// <summary>
    /// <c>Oversize</c> → 413 "oversize" (F2); <c>OverQuota</c> → 413
    /// "over-quota" (F3, **distinct** message); <c>Allowed</c> → <c>null</c>
    /// (F1). The reject is the *only* shape M25 changes (C-UP·7).
    /// </summary>
    ActionResult? CheckUpload(long incomingBytes, string subjectId,
        CommunityStorageSettings settings, long envMaxBytes);
}
```

**Note on `GetPerUserUsageBytesAsync` (reality check):** the M24 seam
`IStorageMetricsService.GetPerUserUsageBytesAsync(string, CancellationToken)`
**already exists** in `Kumunita.Core.Usage` (the C-SM·7 handoff seam, the
exact `Σ SizeBytes WHERE CreatedById == subjectId` shape). M25's
`IStorageSettingsService.GetPerUserUsageBytesAsync` **reuses / re-points to**
it (the §2.6 drift-guard names this). U4 wires it; it is *not* a second copy
of the query.

**Registration shape (U4):** add `IStorageSettingsService` to
`Kumunita.Core.DependencyInjection.ServiceCollectionExtensions.AddKumunitaCore`,
mirroring the M24 line:

```csharp
services.AddTransient<Usage.IStorageSettingsService>(sp => new Usage.StorageSettingsService(
    sp.GetRequiredService<Marten.IDocumentStore>(),
    sp.GetRequiredService<Usage.IStorageMetricsService>()));   // C-SM·7 seam reuse
```

### 2.2 The four-lane adoption rule (C-UP·7 / F10)

For **each** of the four upload lanes, the inline
`if (mediaOpts.Value.MaxBytes > 0 && file.Length > mediaOpts.Value.MaxBytes)
return 413;` is **replaced** by the three-line gate **before** `PutAsync`:

```csharp
var settings = await storageSettings.GetOrCreateAsync();
var usage = await storageSettings.GetPerUserUsageBytesAsync(subject);
var reject = gate.CheckUpload(file.Length, subject, settings,
    mediaOpts.Value.MaxBytes);
if (reject is not null) return reject;
```

- The **allowlist** check is **untouched** and keeps its position (after the
  size gate, before `PutAsync`).
- The **empty-file → 400** check is **untouched** and keeps its position
  (before the size gate).
- M25 changes *what* the size gate reads (the admin doc + quota) and *how* it
  reports (a **distinct** over-quota 413) — not *where* it runs (C-UP·7).
- `subject` is the signed-in principal already minted in each action
  (self-scoped; C-UP·7). `mediaOpts.Value.MaxBytes` is passed as
  `envMaxBytes` (the C-UP·1/5 fallback the pure `Decide` reads).

**Pinned call-sites (read verbatim; U8 replaces exactly these lines).** The
guard line is identical in every lane; the pinned identity is the file + the
413 `if`/`return` pair:

1. **avatar** — `ProfileController.AvatarUpload`,
   `src/Kumunita.Web/Controllers/ProfileController.cs` **line 413**:
   ```csharp
   if (mediaOpts.Value.MaxBytes > 0 && file.Length > mediaOpts.Value.MaxBytes)
       return StatusCode(StatusCodes.Status413RequestEntityTooLarge);
   ```
   (preceded by line 411–412 empty → 400; followed by line 415 disallowed-type
   → 415. `subject = SubjectId(User);`.)
2. **content-image** — `ContentImageController.Upload`,
   `src/Kumunita.Web/Controllers/ContentImageController.cs` **line 162**:
   same two-line guard (`subject = KumunitaPrincipal.SubjectId(User);`).
3. **attachment** — `AttachmentController.Upload`,
   `src/Kumunita.Web/Controllers/AttachmentController.cs` **line 69**:
   same two-line guard (`subject = KumunitaPrincipal.SubjectId(User);`).
4. **document (upload)** — `DocumentController.Upload`,
   `src/Kumunita.Web/Controllers/DocumentController.cs` **line 302**:
   same two-line guard over `file.Length` (`subject =
   KumunitaPrincipal.SubjectId(User);`).

   > **Second guard to decide (reality check):** `DocumentController.Edit`
   > (the document *edit* lane, same file **line 213**) carries an identical
   > inline guard over `form.File.Length`, nested in `if (form.File is not
   > null && form.File.Length > 0)`. It is **not** in the pinned test list
   > (§2.4 covers `DocumentUpload_*` only). U8 must decide explicitly whether
   > the edit-lane re-upload adopts the gate too (F10 "all four upload lanes"
   > suggests it should) — and if so, the §2.3/§2.4 test list is incomplete
   > and this is a drift-guard event (§2.6), not a silent scope-creep.

### 2.3 Pinned Core tests (exact names)

`tests/Kumunita.Core.Tests/Usage/StorageLimitsTests.cs` (new, U4/U5):

1. `Decide_Oversize_Rejects` — F2
2. `Decide_OverQuota_Rejects` — F3
3. `Decide_WithinBoth_Allows` — F1
4. `Decide_QuotaZero_Unlimited` — F5
5. `EffectiveMaxFileBytes_AdminOverrideBeatsEnv` — F4
6. `EffectiveMaxFileBytes_UnsetFallsBackToEnv` — F6
7. `GetPerUserUsage_SumsCreatedByIdOnly` — F7/F8
8. `GetPerUserUsage_OtherUsersExcluded` — F8
9. `SetAsync_PersistsInCallerSession` — C-UP·1
10. `Decide_SizeCheckedBeforeQuota` — C-UP·2 ordering

### 2.4 Pinned Web tests (exact names)

Placed in the **per-lane** test files (U9 extends them):

| # | Name | Lane / file |
|---|------|-------------|
| 11 | `AvatarUpload_Oversize_413` | `ProfileAvatarUploadTests.cs` |
| 12 | `AvatarUpload_OverQuota_413` | `ProfileAvatarUploadTests.cs` |
| 13 | `ContentImageUpload_Oversize_413` | `ContentImageUploadTests.cs` |
| 14 | `ContentImageUpload_OverQuota_413` | `ContentImageUploadTests.cs` |
| 15 | `AttachmentUpload_Oversize_413` | `AttachmentUploadTests.cs` |
| 16 | `AttachmentUpload_OverQuota_413` | `AttachmentUploadTests.cs` |
| 17 | `DocumentUpload_Oversize_413` | `DocumentControllerTests.cs` |
| 18 | `DocumentUpload_OverQuota_413` | `DocumentControllerTests.cs` |
| 19 | `UploadGate_OversizeAndOverQuota_HaveDistinctMessages` | `UploadGateTests.cs` |
| 20 | `AdminStorage_SetLimits_Persists` | `AdminStorageControllerTests.cs` |
| 21 | `AdminStorage_NonGlobalAdmin_Forbidden` | `AdminStorageControllerTests.cs` |
| 22 | `ResidentUsageView_SelfOnly` | `ResidentUsageViewTests.cs` |
| 23 | `ResidentUsageView_QuotaZeroShowsUnlimited` | `ResidentUsageViewTests.cs` |

Invariant bindings: 11/13/15/17 → F2; 12/14/16/18 → F3; 19 → F2/F3 (the
distinct-message contract); 20 → C-UP·1; 21 → C-UP·1; 22 → F7; 23 → F5.

**Admin surface (U5/U6):** `/admin/storage` mirrors `AdminTimezoneController` —
`[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]`, a thin wrapper
over `IStorageSettingsService.SetAsync` (the single audited write lane),
`[ValidateAntiForgeryToken]` on the save, `ActorId(User)` =
`KumunitaPrincipal.SubjectId(user)`.

**Resident self-usage view (U7):** self-scoped read (F7) — the resident sees
their own usage (`GetPerUserUsageBytesAsync`), their quota
(`CommunityStorageSettings.PerUserQuotaBytes`), and remaining; quota `0`
renders as **unlimited** (F5).

### 2.5 Acceptance gate (U10 records) — the three-test shape

- **closed loop:** admin sets a quota + a resident uploads within it → success;
  the resident's self-view shows the new usage.
- **handoff:** the *next* upload that would exceed the quota is rejected 413 —
  strong consistency, live doc (F4 / F3).
- **part-vs-whole:** the 23-test list (tests 1–23) is the **whole**; the
  closed-loop + handoff are the **parts**; all must pass **together** (a
  per-lane test passing in isolation does not prove the four-lane consistency
  of F10).

### 2.6 Drift-guard (frozen once written)

The following are **frozen**; any mismatch is a `## U<m> — Drift pause`
(unit-series rule §6), not an implementation choice:

- The **7 invariants** (C-UP·1–7) and the **10 FACES** (F1–F10) — Part 1.
- The **`CommunityStorageSettings`** shape (the four fields + `Id =
  "community"`).
- The **`IStorageSettingsService`** 4-method surface
  (`GetOrCreateAsync` / `SetAsync` / `GetPerUserUsageBytesAsync` / `Decide`).
- The **`StorageLimits`** / **`StorageDecision`** pure shape
  (`{ Allowed, Oversize, OverQuota }`; size-first `Decide`).
- The **`StorageSettingsDocTypes.Configure`** line
  (`opts.Schema.For<CommunityStorageSettings>()`).
- The **`IUploadGate`** shape (`CheckUpload` → `ActionResult?`, distinct
  413).
- The **four-lane adoption rule** (§2.2) — the four pinned call-sites (and the
  document *edit*-lane decision, §2.2 note).
- The **23 test names** (§2.3 tests 1–10; §2.4 tests 11–23).
- The **C-UP·6 milestone contract**: M24 `StatusDone` / M25 `StatusNext` at
  start; M25 `StatusDone` + M26 `StatusNext` at close.
- **The M24 C-SM·7 seam reuse:** `IStorageSettingsService.
  GetPerUserUsageBytesAsync` **delegates to / re-points to** the existing
  `IStorageMetricsService.GetPerUserUsageBytesAsync` (the M24 storage-metrics
  lane, ADR 0134). If M24's seam is removed or reshaped, or the per-user usage
  read is re-pointed *away* from it, that is a drift-guard event — it is the
  one cross-milestone seam M25 depends on, and it is **named here** so a later
  unit cannot silently re-implement it.
