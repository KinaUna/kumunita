# F16 — UPL-02: scan uploads before serving — **Part-0 design pin**

**Lane:** prod-hardening (in-progress). **Fix F16** = the `code+ops` fix for
the 2026-10-22 audit row **UPL-02** ("no scan step; files served
`attachment` + `nosniff` — mitigation only"). **This doc is the U1 design pin
only** — U2 (the `IVirusScanner` seam + quarantine flag + serve-lane check, ≤3
files) and U3 (the pinned test) execute against the seams pinned here. No
code, no build, no package, no migration in U1.

**The one thing to internalize:** F16 is **not** a new feature and **not** a
new bounded context. It adds **one small Core seam** (`IVirusScanner`,
HTTP-free, ADR 0006-D) + **one bool on an existing doc** (`MediaObject.Quarantined`)
+ **one gate at the serve lane** (a 404 refusal, the same posture the serve
lane already uses for a store miss and a `Deny`). The repo today serves media
with `Content-Disposition: attachment` + `X-Content-Type-Options: nosniff` as
its **only** anti-malware mitigation — that stays (it is the browser-side
control); F16 adds the **content-side** control: refuse to serve a byte the
scanner flags. The scanner is **ClamAV via the store** — a local daemon, not a
third-party HTTP provider, because (a) the deployment is self-hosted/T1 and
ClamAV is the tool the audit already assumes (the owner-ops item is "run
ClamAV on the prod volume"), and (b) a provider would need an HTTP client in
Core, which violates ADR 0006-D (Core stays HTTP-free).

---

## 1. What UPL-02 asks, in one line

Today an uploaded byte is served as soon as its `MediaObject` catalog row
exists and the caller passes the per-owner read decision — nothing inspects
the **content** for malware. F16 adds a **scan-before-serve** gate at the
serve lane: a byte the scanner flags as infected is **quarantined**
(`MediaObject.Quarantined = true`) and **refused with 404** (not served, not
a 403 — the repo's no-leak posture), while clean bytes serve exactly as they
do today.

**Scope fence (what F16 does NOT do):**
- F16 is the **serve-lane enforcement** only. It is **not** the upload lane
  (the `PutAsync` write path is untouched — the invariant names the *serve*
  lane as the enforcement point, deliberately: scanning at upload is a
  *stronger* control, but it is a **separate decision** that changes the write
  path + the quota/orphan ordering (C-MED·7) and is **out of F16**).
- F16 adds **one bool** to the existing `MediaObject` — it is **not** a new
  doc type, **not** a new bounded context, and **not** a change to the
  `MediaDocTypes` *registration* (the surface stays `opts.Schema.For<MediaObject>()`;
  the POCO gains a property, Marten applies the new column idempotently at
  boot — ADR 0004 §B).
- F16 does **not** change the serve headers (the `attachment` + `nosniff`
  mitigation stays), the allowlist gates, the quota gate (M24), or the F15
  lifecycle sweep.

---

## 2. The scanner — pinned choice + seam shape

**Choice: ClamAV, driven via the store (a local daemon), NOT a third-party
HTTP provider.**

**Why (recorded, not guessed):**
- **Self-hosted / T1 topology (ADR 0002)** — the deployment is one community's
  own container + Postgres; a local `clamd` daemon on the volume is the
  natural fit, and it carries **no external dependency** (no API key, no
  network egress, no PII leaving the box — the privacy-first model in
  `SECURITY.md` is preserved). A provider API would send the community's
  uploaded bytes to a third party, which the repo's privacy stance does not
  assume.
- **ADR 0006-D — Core stays HTTP-free** — a provider client would need an HTTP
  call *in* `Kumunita.Core` (where the scanner seam lives). ClamAV-via-daemon
  is a **local process/socket** call; the seam stays a pure
  "bytes in → verdict out" contract with no `HttpClient` (the `IMediaFileStore`
  / `ISmtpSender` "Core I/O-seam / Web transport" split precedent — the seam is
  Core, the concrete transport is the thing that may touch the outside).
- **The audit already assumes ClamAV** — the F16 open item is "run ClamAV on
  the prod volume," so pinning ClamAV is what the owner's ops step points at.
  A provider would leave that ops item dangling (nothing to point at).

**The seam (a new `IVirusScanner` in `Kumunita.Core/Media/`, HTTP-free):**

```csharp
namespace Kumunita.Core.Media;

/// The scan verdict for one stored media byte. A signature match is a
/// *normal return value* (Infected), not an exception — the serve lane
/// branches on it exactly like it branches on a store miss.
public enum VirusScanStatus
{
    Clean,      // no signature match → the serve lane proceeds (unchanged path)
    Infected,   // a signature match → quarantine + 404 (not served)
}

/// <summary>
/// F16 / UPL-02 — the byte-level antivirus seam. <b>Core, HTTP-free
/// (ADR 0006-D)</b>: the contract is "bytes in → verdict out", with no
/// <c>HttpClient</c> and no Web types. The default implementation drives a
/// local ClamAV daemon; the <see cref="VirusScanStatus"/> return is the only
/// thing the serve lane cares about. Stubbable: U3's test injects a stub that
/// returns <see cref="VirusScanStatus.Infected"/> and asserts the serve lane
/// 404s (it does not need a real daemon).
/// </summary>
public interface IVirusScanner
{
    /// <summary>
    /// Scan the payload stream and return the verdict. A signature match
    /// returns <see cref="VirusScanStatus.Infected"/> (never throws); a clean
    /// byte returns <see cref="VirusScanStatus.Clean"/>. The caller owns and
    /// disposes <paramref name="content"/> — the scanner must not leave it
    /// open. See §2.1 for the fail-open/fail-closed default on a scanner
    /// that is unavailable.
    /// </summary>
    Task<VirusScanStatus> ScanAsync(System.IO.Stream content, CancellationToken ct = default);
}
```

> **Why `ScanAsync(Stream)` and not `ScanAsync(string contentId, IMediaFileStore)`:**
> taking a `Stream` keeps the scanner **store-agnostic and trivially stubbable**
> (U3's stub ignores the stream and returns `Infected` — no real byte, no temp
> dir, no `LocalVolumeFileStore` needed for the serve-lane test). The serve
> lane opens the stream from the **existing** `IMediaFileStore.OpenReadAsync`
> seam (`src/Kumunita.Core/Media/IMediaFileStore.cs:15`, implemented at
> `LocalVolumeFileStore.cs:44`) — so the scanner re-uses the store's read path,
> it does not add a new byte-read seam. **Do NOT** add a new byte-read method to
> `IMediaFileStore` for this — `OpenReadAsync` is the C-MED·6 read seam already.

### 2.1 The scanner-unavailable default — **pinned: fail-OPEN (recorded, owner may flip)**

ClamAV is **not yet on the prod volume** (that is the F16 owner-ops open item).
So at boot, before that step, the scanner is **unavailable**. Two sane
defaults exist, and this is a genuine pin-vs-reality tension:

- **Fail-OPEN (the pin's default):** an unavailable / errored scanner returns
  `Clean` (serving proceeds). **Why:** a missing optional daemon must not
  **DoS the serve lane** — consistent with the repo's C-MED·7 "orphan-safe"
  and "a missing byte degrades to a 404, not a crash" posture, and with the
  F10/F12 "off-by-default floor" idiom (a security *addition* that is absent
  does not break the thing it hardens). The gate becomes **real** only once the
  owner deploys ClamAV (open item below) — until then the browser-side
  `nosniff` + `attachment` mitigation (which stays) is the active control.
- **Fail-CLOSED (the alternative, NOT pinned):** an unavailable scanner returns
  `Infected` (every serve 404s). This is the security-strict choice (no silent
  false-pass), but it **DoS-es all media serving** on any box without ClamAV —
  including dev/scratch instances — which the repo's house shape avoids.

> **⚠ Recorded, NOT silently resolved (owner to confirm at U1-review):** the
> pin chooses **fail-open** so that (a) the pinned test
> `InfectedUpload_NotServed` (which drives the *Infected* path, not the
> unavailable path) is unambiguous, and (b) deploying ClamAV is what flips the
> gate from "no-op" to "real," not a code branch. **If the owner wants
> fail-closed**, that is a **one-line change in the default scanner impl + a
> decision**, made at U1-review (it changes U2's default-impl behavior, not the
> seam shape or the test) — recorded here per the unit-series rules, not guessed.
> Either way the **seam shape, the quarantine flag, and the serve-lane 404
> contract are unchanged** (that is the point of pinning the seam, not just the
> default).

### 2.2 The owner-ops open item (recorded, NOT assumed done)

**Run ClamAV on the prod volume** (install `clamd`, point the default scanner
impl at it, confirm it scans the upload volume). **Until this runs, the serve
gate is fail-open (a no-op on a missing daemon)** — the `nosniff` +
`attachment` mitigation remains the active control. This is the `code+ops`
half of F16; it is **recorded, not executed** (the owner runs it on the prod
box, mirroring the F10 `SENTRY_DSN` / F12 `SupportEmail` "recorded, not
assumed" idiom).

---

## 3. The quarantine state — pinned

**The flag: a new `bool Quarantined` on the existing `MediaObject` doc
(default `false`).** Not a new doc, not a new surface — a property added to the
POCO the serve lane already loads:

```csharp
public sealed class MediaObject
{
    public string Id { get; set; } = "";
    public string? Filename { get; set; }
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTimeOffset Created { get; set; }
    public string? CreatedById { get; set; }
    // F16 / UPL-02 — the quarantine flag. false = clean / never flagged (the
    // default, unchanged path). true = the scanner flagged this byte infected;
    // the serve lane refuses it (404). Set at SERVE time (§4), idempotent.
    public bool Quarantined { get; set; }
}
```

**Why a bool on the doc (not a side table / not a new doc type):** the
`MediaObject` catalog row is the surviving reference that a `pg_dump` restore
carries (C-MED·4, `MediaObject.cs:7`) — the quarantine state **must** survive a
restore, so it lives **on** the catalog doc, not in a separate store. A bool is
the minimum state the pinned test needs (`Infected → 404`), and it is the
smallest possible change to a frozen doc (a new property, Marten applies the
column idempotently at boot — ADR 0004 §B; the `MediaDocTypes.Configure`
registration `opts.Schema.For<MediaObject>()` is **unchanged**,
`src/Kumunita.Core/MediaDocTypes.cs:23`).

**When it is set: at SERVE time (the enforcement point, §4), NOT at upload
time.** On a serve where the scan returns `Infected`, the serve lane sets
`stored.Quarantined = true`, saves, and returns **404**. On a later serve of
the same id, the serve lane sees `Quarantined == true` **before** re-scanning
and **short-circuits to 404** (idempotent — no re-scan, no re-write, a stable
refusal). This keeps the write/upload lane (C-MED·7 orphan-safe ordering, the
M24 quota gate) **untouched** — the invariant names the serve lane as the
enforcement point deliberately, and upload-time scanning is the *out-of-F16*
scope fence in §1.

> **The `MediaDocTypes` GOTCHA (F1.U5 GOTCHA-1, carried verbatim):** `MediaObject`
> lives on the **`MediaDocTypes`** surface, **not** `CoreDocTypes`/`PostDocTypes`.
> U3's harness **must** call `MediaDocTypes.Configure(opts)` in the boot store
> setup (`tests/Kumunita.Core.Tests/Media/MediaLifecycleTests.cs:116` is the
> exact precedent) — otherwise the serve-lane query / the `Quarantined` read
> hits an unmanaged-table error. **Do NOT** register on the wrong surface.

---

## 4. The enforcement point — the serve lane (pinned, grounded in the real controllers)

The **serve lane is the enforcement point** (the invariant). The repo serves
media through **three** controllers, all with the same 5-step shape
(validate id → store miss ⇒ 404 → reverse-lookup/decision → `OpenReadAsync` →
`File(stream, ...)`):

| Serve controller | Route | Step-2 (store miss → 404) | Where F16's gate goes |
|---|---|---|---|
| `AttachmentController.Serve` | `GET /attachment/{id}` | `media.GetAsync(id)` → `if (stored is null) return NotFound();` (src/.../AttachmentController.cs, step 2) | **immediately after** that `return NotFound();`, **before** the step-3 `posts.FindPostByAttachmentIdAsync(id)` reverse-lookup |
| `ContentImageController.Serve` | `GET /content-image/{id}` | `media.GetAsync(id)` → miss ⇒ 404 (step 2) | **immediately after** the step-2 404, **before** the step-3 reverse-lookup |
| `DocumentController.Serve` | `GET /documents/{id}/download` | `media.GetAsync(result.Document.MediaId)` → `if (stored is null) return NotFound();` (the blob-miss check) | **immediately after** that blob-miss 404, **before** the `media.OpenReadAsync` serve (the document `Read` decision already ran in `documents.GetAsync`) |

**The 404-not-served contract (pinned, the repo's no-leak posture):** a
quarantined / infected byte is refused with **`NotFound()` (404), NOT `403`.**
This is the **exact** posture the serve lane already uses for a **store miss**
(404, zero audit rows) and for a **`Deny`** decision (404, not 403 — the
avatar / `PostReadDecision` precedent). It means the refusal does **not** leak
"this file exists but is malware" vs. "this file does not exist" — both are 404,
zero audit rows (a quarantine refusal is a store-level refusal, not an
audience-restricted read, so it emits **no** `AccessAudit` row — the same
reasoning as the store-miss and the announcement scope-deny paths,
`AttachmentController.cs` step-2/step-4 docs).

**U2's gate shape (pinned, so U3 asserts the right behavior) — in each serve
controller, between the step-2 404 and the serve/reverse-lookup:**

```csharp
// F16 / UPL-02 — the quarantine gate (serve lane is the enforcement point).
// Short-circuit if already quarantined (idempotent, no re-scan, §3):
if (stored.Quarantined) return NotFound();           // 404, zero audit rows (no-leak, §4)

// Otherwise scan (fail-open on an unavailable scanner, §2.1):
var verdict = VirusScanStatus.Clean;
try
{
    using var scanStream = await media.OpenReadAsync(id);   // the C-MED·6 read seam (IMediaFileStore.OpenReadAsync)
    verdict = await scanner.ScanAsync(scanStream, ct);
}
catch { /* fail-open: a scanner fault degrades to Clean (the §2.1 pin), not a crash */ }

if (verdict is VirusScanStatus.Infected)
{
    stored.Quarantined = true;                           // quarantine (persist, §3)
    await store.SaveChangesAsync(ct);                    // one commit (C3 single-write-lane, ADR 0006)
    return NotFound();                                    // 404, zero audit rows (no-leak, §4)
}
// verdict is Clean → fall through to the existing serve path (unchanged).
```

> **Why the gate is *before* the reverse-lookup / `Read` decision:** the
> quarantine is a **content-level** refusal, not an **audience-level** one. A
> malware byte must be refused for **everyone** (an anonymous visitor to a
> public post's attachment included — the same reason the store-miss 404 sits
> before the `CanAsync` decision). Placing it after the decision would let a
> *denied* caller and an *infected* caller be indistinguishable only by luck, and
> would burn an audit row for what is really a store refusal. **One commit,
> one flag write** (C3 single-write-lane, ADR 0006 — the F15 §5
> "catalog-row commits first" shape).
>
> **The scanner + the gate touch only `src/Kumunita.Core/**` (the seam + the
> `MediaObject` bool) and the three serve controllers (the gate). The upload
> lane, the `IMediaStore`/`IMediaFileStore` seams, and the F15 lifecycle job
> are all UNTOUCHED** (F16 is a *new consumer* of the read seam, the
> `DocumentController` D3 "new consumer, the media lane is untouched" precedent).

---

## 5. The serve-lane algorithm U2 implements (pinned, so U3 asserts the right shape)

For **each** of the three serve controllers, U2 inserts the §4 gate **exactly**
at the pin's insertion point (after the step-2 store-miss 404, before the
reverse-lookup / the blob serve):

1. **Read the catalog row** — already done by the existing step 2
   (`stored = await media.GetAsync(id)`); **no** new store read.
2. **Short-circuit if quarantined** — `if (stored.Quarantined) return NotFound();`
   (the idempotent refusal, §3 — no re-scan, no re-write, zero audit rows).
3. **Scan** — `using var scanStream = await media.OpenReadAsync(id);` →
   `var verdict = await scanner.ScanAsync(scanStream, ct);` (the C-MED·6 read
   seam `IMediaFileStore.OpenReadAsync`, `IMediaFileStore.cs:15` /
   `LocalVolumeFileStore.cs:44`; the scanner + the stream are both disposed by
   the `using`). **Fail-open on any scanner fault** (§2.1 — a fault degrades to
   `Clean`, the serve proceeds; the try/catch does **not** rethrow, the
   C-MED·7 "never let an I/O fault abort the lane" shape).
4. **Branch on the verdict:**
   - `Infected` → `stored.Quarantined = true; await store.SaveChangesAsync(ct);`
     (one commit, C3 single-write-lane) → `return NotFound();` (404, zero audit
     rows, §4).
   - `Clean` → **fall through to the existing serve path unchanged** (the
     reverse-lookup / `Read` decision / `OpenReadAsync` / `File(stream, ...)`
     + the `attachment` + `nosniff` headers — **all untouched**, §1 scope fence).
5. **The constructor** — each serve controller gains one `IVirusScanner`
   dependency (the `ContentImageController` / `AttachmentController` ctor is
   already the "widen the constructor as the lane needs it" shape,
   `AttachmentController.cs:39` — `IMediaStore` + `MediaOptions` + the owner
   services; adding `IVirusScanner` is the same widening). The
   `DocumentController` already takes `IMediaStore media` + `IDocumentStore
   store` — it gains `IVirusScanner` the same way.

> **The `IMediaStore` vs `IMediaFileStore` note (pinned):** the serve lanes
> today call `media.OpenReadAsync(id)` on the **`IMediaStore`** seam
> (`IMediaStore.cs:14`, `OpenReadAsync`). U2's scan uses the **same**
> `IMediaStore.OpenReadAsync` the serve lane already uses (not a new
> `IMediaFileStore` read) — the scanner takes that stream. **No new read seam,
> no `IMediaFileStore` change.** (F15 re-used `IMediaFileStore.DeleteIfExistsAsync`;
> F16 re-uses the **serve lane's own** `IMediaStore.OpenReadAsync` — each fix
> re-uses the seam it is in, not the other fix's seam.)

---

## 6. The pinned test name + shape (U3 implements)

**`InfectedUpload_NotServed`** — the **single** F16-pinned test (per the
plan's F16.U3 spec: "a stub scanner returns infected, the serve lane 404s").
It drives the **serve lane** (§4/§5) with a **stub scanner** and asserts the
**404** refusal:

- **Harness (Web.Tests, the "Web class → Web.Tests" precedent — F12.U3 /
  F9.U4 / F10.U3):** construct **one** serve controller directly (the
  `AttachmentController.Serve` lane is the pinned target — the literal
  "upload served as a download" path UPL-02 names; the other two controllers
  get the **same one-line gate** in U2 and U3 may assert any one of them, but
  **this one name + this lane is pinned**). Use a **real** `IMediaStore`
  (a `LocalVolumeMediaStore` over a temp-dir `LocalVolumeFileStore`, the
  F1.U5 / F15.U3 real-byte-store pattern — **not** a null-skip) so the step-2
  `GetAsync` returns a live `MediaObject` with `Quarantined = false`, and a
  **stub** `IVirusScanner`:

  ```csharp
  private sealed class InfectedScanner : IVirusScanner
  {
      public Task<VirusScanStatus> ScanAsync(System.IO.Stream content, CancellationToken ct = default)
          => Task.FromResult(VirusScanStatus.Infected);   // the stub: always Infected
  }
  ```

- **Plant:** a byte on the volume (`LocalVolumeFileStore.PutAsync`) + a
  `MediaObject` row with `Quarantined = false` (the not-yet-flagged shape).
  **The boot store MUST call `MediaDocTypes.Configure(opts)`** (the §3 GOTCHA —
  `MediaObject` is on the `MediaDocTypes` surface, `MediaLifecycleTests.cs:116`
  precedent). The `PostService` / `IAnnouncementService` / `IAuthorizationService`
  owner dependencies may be the **minimal / no-op** stand-ins (the quarantine
  gate fires **before** they are reached, §4 — a `Clean` verdict would be the
  only path that touches them, and the stub never returns `Clean`).

- **Assert (the pinned legs):**
  1. **The serve lane 404s** — the serve action returns a **404 / `NotFound`**
     (the `Infected` verdict → `return NotFound()`, §4). Assert the action's
     result is a 404 (not a 403, not a `FileResult` — the **not-served**
     contract).
  2. **The quarantine flag is set** — after the serve, the `MediaObject` row's
     `Quarantined` is **`true`** (the §3/§5.4 "persist the flag" step — a fresh
     `GetAsync` / `LoadAsync<MediaObject>` read shows `Quarantined == true`).
  3. **The refusal is idempotent** — a **second** serve of the same id returns
     **404** again **without re-invoking the scanner** (the §3 short-circuit —
     a recording stub that counts `ScanAsync` calls asserts the call count is
     still **1** after the second serve; the second serve hits
     `if (stored.Quarantined) return NotFound();` before any scan).
  4. **(Control, keeps-clean-intact)** — a **second** byte whose stub scanner
     returns `Clean` (a second stub, `CleanScanner`) is **served** (the
     action returns a `FileResult` / 200-shape) and its `Quarantined` stays
     `false` (the §5.4 `Clean → fall through unchanged` path). **Optional**
     finer leg — the **pinned** legs are 1–3; this one is the keeps-clean
     control F15.U3 used.

> **Why one test, not several:** the plan's F16.U3 names exactly one test
> (`InfectedUpload_NotServed`) and folds "stub-Infected → 404 + flag-set +
> idempotent" into it (the task's U3 exit wording: "a stub scanner returns
> infected, the serve lane 404s"). U3 may add **additional** named tests if it
> wants finer granularity (e.g. one per serve controller, or a
> `QuarantinedByte_ShortCircuitsWithoutRescan`), but **this one name is pinned**
> and must exist and pass. **Do not** rename it (the next audit run diffs
> against it).

> **The fail-open/fail-closed default (§2.1) is NOT in this test** — the pinned
> test drives the **Infected** path (the security-critical path), not the
> scanner-**unavailable** path. A separate optional test for the unavailable
> path (stub that throws → the serve **proceeds**, fail-open) is a U3 judgment
> call, not a pinned leg. The §2.1 default is a **recorded decision**, not a
> pinned assertion.

---

## 7. Invariants held by F16 (re-stated for U2/U3)

- **Core stays HTTP-free (ADR 0006-D)** — `IVirusScanner` is a pure
  "bytes in → verdict out" contract with **no** `HttpClient`, **no** Web types;
  the default impl drives a **local** ClamAV daemon (a process/socket), not an
  HTTP provider (the `ISmtpSender` "Core I/O-seam / Web transport" split — the
  seam is Core, the concrete daemon is the outside-touching part).
- **The serve lane is the enforcement point (NOT the upload lane)** — the gate
  + the flag-write + the 404 all live in the three serve controllers; the
  upload / `PutAsync` write path, the M24 quota gate, and the C-MED·7
  orphan-safe ordering are **untouched** (the §1 scope fence).
- **No new bounded context / no new doc type** — F16 adds **one bool** to the
  existing `MediaObject` (the `MediaDocTypes` registration
  `opts.Schema.For<MediaObject>()` is **unchanged**; Marten applies the new
  column idempotently at boot — ADR 0004 §B) + **one new seam**
  (`IVirusScanner`) + **three one-line gates**. No `MediaDocTypes` surface
  change, no new `*DocTypes` class.
- **C3 single-write-lane / one-commit (ADR 0006)** — the flag-write is **one**
  `SaveChangesAsync` in the serve lane (the F15 §5 / F1.U4 single-commit shape);
  a re-serve short-circuits **before** any write (idempotent, §3).
- **The no-leak 404 posture** — a quarantine refusal is **404, not 403**, zero
  audit rows (the store-miss / `Deny`-decision posture, §4) — it does not leak
  "exists but is malware."
- **The existing serve mitigation STAYS** — `Content-Disposition: attachment`
  + `X-Content-Type-Options: nosniff` are **untouched** (F16 is the
  content-side control *on top of* the browser-side mitigation, §1).
- **The fail-open default is recorded, not guessed** (§2.1) — a missing/errored
  ClamAV daemon does not DoS the serve lane; the owner's "run ClamAV on the
  prod volume" step is what flips the gate from no-op to real (the F10/F12
  "recorded, not assumed" idiom).
- **No new `<PackageReference>` in U1** — the seam is a plain interface + enum
  (no package). The **default ClamAV impl** (U2) may add a dependency (a thin
  `clamd` socket client or a `Process` call to `clamscan`) — that is a **U2
  judgment call** within the "≤3 files" budget, not a U1 pin; if U2 finds the
  repo already has a usable client, it re-uses it (the "re-use the existing
  seam" discipline).

---

## 8. Drift notes (recorded, not silently resolved)

1. **No scanner / quarantine / ClamAV / `Infected` exists anywhere in `src/`**
   (full-`src` grep for `ClamAV|Virus|IVirusScanner|Quarantine|IsInfected|
   Infected|AntiMalware` → **0 hits**). This is a **genuinely new** seam — there
   is no existing expectation to reconcile, and **no production defect found**
   (no `## F16.U1 — Drift pause` raised). The only "drift" is the §2.1
   fail-open/fail-closed default, which is a **pin-vs-reality reconciliation**
   (the scanner is not yet deployed), recorded for the owner — not a
   codebase-divergence stop.
2. **`MediaObject` is on the `MediaDocTypes` surface** (F1.U5 GOTCHA-1) — U3's
   harness must call `MediaDocTypes.Configure(opts)` in the boot store setup
   (`MediaLifecycleTests.cs:116` precedent). Recorded so U3 does not hit the
   unmanaged-table error F1.U5 hit.
3. **The serve lanes use `IMediaStore.OpenReadAsync`, not
   `IMediaFileStore`** — U2's scan re-uses the serve lane's **own** read seam
   (`IMediaStore.OpenReadAsync`, `IMediaStore.cs:14`), passed to
   `IVirusScanner.ScanAsync(Stream)`. It does **not** add a new
   `IMediaFileStore` read method (F15 re-used `IMediaFileStore.
   DeleteIfExistsAsync`; F16 re-uses the seam it is *in*). Recorded so U2 does
   not cross-fix the seam.
4. **Upload-time scanning is OUT of F16** (the §1 scope fence) — the invariant
   names the **serve** lane as the enforcement point. If the owner wants
   upload-time scanning (scan once at `PutAsync`, quarantine once, skip the
   per-serve scan), that is a **separate decision + a shape change** to the
   write path (it touches the M24 quota gate + the C-MED·7 orphan-safe
   ordering) — to be made at U1-review, **not** silently added. This is a
   pin-vs-reality reconciliation (the pin chooses the tighter
   serve-lane-enforcement per the invariant), recorded, not guessed.

---

## 9. Exit contract for U1 (this unit)

- **Landed:** this one file
  (`docs/design/prod-hardening-f16-scan.md`). **No code, no build, no package,
  no migration, no `src/**` change.**
- **Internally consistent:** the scanner choice + seam shape + the
  fail-open/fail-closed default pinned against the repo precedent (§2, §2.1);
  the quarantine state pinned (`MediaObject.Quarantined`, set-at-serve-time,
  idempotent, §3) with the `MediaDocTypes` GOTCHA named; the enforcement point
  pinned to the **three** real serve controllers + the exact insertion point +
  the 404-not-served / zero-audit-row contract (§4, §5); the pinned test
  `InfectedUpload_NotServed` + its legs + the stub-shape + the harness GOTCHA
  pinned (§6); the invariants re-stated (§7); the three drifts (the
  fail-open default, the `MediaDocTypes` surface, the `IMediaStore` read-seam
  choice, the upload-time-out-of-F16 scope) recorded, not guessed (§8).
- **F16 is now at U2** — the `IVirusScanner` seam + the `MediaObject.Quarantined`
  bool + the three serve-lane gates (≤3 files: the Core seam file, the
  `MediaObject` bool, and the serve-controller gate(s) — the Web glue the
  "serve lane is the enforcement point" invariant requires), then **U3** (the
  pinned test). **F16.U2–U3 + F17–F22 remain; the lane stays open — no
  6-member close-flip (that is F22, the lane's last unit).** The next unit is
  **F16.U2** — a fresh clean-slate agent reading §2 (the seam + the
  fail-open/fail-closed default + the owner-ops item), §3 (the quarantine flag
  + the `MediaDocTypes` GOTCHA), §4/§5 (the enforcement point + the algorithm +
  the insertion points in the three controllers), §6 (the test name to keep
  consistent), §7 (the invariants), §8 (the drift notes).
