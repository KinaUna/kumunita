---
id: a15-prompt
title: "Prompt: IMPROVE-UX A15 — the per-resident quota override (the admin half of the storage loop)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-12
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **A15** — the per-resident quota override. A14 (just closed)
> gave a resident a "Request more space" button that notifies the
> GlobalAdmins with a link to their account; the admin can now *reach* the
> request, but the platform still only has **one global** per-user quota —
> there is no seam to actually raise one resident's cap without changing
> everyone's. A15 adds that seam: a per-resident quota override on the
> admin account-detail page, and the upload quota check resolves
> `override ?? global`. A15 is the **next unit in the Admin/moderator
> sequence** — it **depends on A14** (A14's `linkPath` is exactly the
> surface A15 extends). **Who is it for:** the next coding agent (fresh
> context). **When to read the rest:** when starting A15 (A14 has shipped;
> the "request more space" loop is in, the override is not).

# Fresh-context prompt: IMPROVE-UX A15

You are continuing the Kumunita repository on `development` (HEAD
`b879dc9c`, **A14 just closed** — the resident "Request more space" button
on `/account/storage` now notifies the GlobalAdmins with a link to
`/admin/accounts/{subjectId}`; before it A13 closed the guest-form account
picker, A12 the resident "Invite a neighbour" surface + approval queue,
A11 the resident-invite setting, A10 the acceptance notify, A09 the
`/admin/invitations` surface, A08 the `?invite=` gate, A07 the `Invitation`
doc + `IInvitationService`). A15 is the **admin half of the storage loop**:
it adds the per-resident quota override the A14 request is nudging toward,
and wires the upload quota check to resolve `override ?? global`. A15
**depends on A14** (A14's `linkPath` — `/admin/accounts/{subjectId}` — is
the exact surface A15 adds the override control to). The plan card (in
`plan-improve-ux.md`, the A15 row):

> A15 — Per-resident quota override on the admin account page; quota check
> uses override ?? global; tests.
> Read first: `AdminStorageController.cs`, quota check in Media.
> **Done when: override honoured.**

The problem it fixes: the platform stores **one** per-user quota — the
`CommunityStorageSettings` doc's `PerUserQuotaBytes` (sentinel: `0` =
unlimited, C-UP·5). Every upload is gated against that single number
(`UploadGate.CheckUpload` → `StorageLimits.Decide`). When a resident's A14
"request more space" lands, the only way to actually give them more is to
raise the **global** quota (hurting everyone) — there is no per-resident
override. A15 adds: (1) a per-resident override the admin sets on the
account-detail page, and (2) the quota check resolving the **effective**
quota = `override ?? global` for that resident. That is the closed loop:
resident requests → admin gets the nudge (A14) → admin raises just that
resident's cap (A15) → the resident's next upload is allowed against their
own number.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the
  comment rule, the don't-pause-mid-task rule).
- **`plan-improve-ux.md`** (this lane) — the **A15 card** + the "What this
  lane is" + DoD + the admin journey ("Admin — Resident needs more space:
  request arrives; override set in-app").
- **`src/Kumunita.Core/Usage/IStorageSettingsService.cs`** +
  **`CommunityStorageSettings.cs`** + **`StorageLimits.cs`** +
  **`StorageSettingsService.cs`** — the existing storage seam.
  `CommunityStorageSettings` is the **single** settings doc (`Id =
  "community"`, `PerUserQuotaBytes` `0` = unlimited, the C-UP·5 sentinel).
  `IStorageSettingsService` has the read (`GetOrCreateAsync`), the
  **global** write (`SetAsync(long? maxFileBytes, long perUserQuotaBytes,
  string actorId, session)`), the usage read (`GetPerUserUsageBytesAsync`),
  and the **pure** decision (`Decide`). A15's lane mirrors the `SetAsync`
  write shape for the per-resident override (one in-caller-session write,
  the C-UP·1 / C3 same-transaction shape).
- **`src/Kumunita.Web/Security/UploadGate.cs`** (`CheckUpload`) — the
  **single** place the per-user quota is enforced at upload time (the
  C-UP·3 "one 413 producer"). It reads `GetPerUserUsageBytesAsync(subjectId)`
  then calls the pure `StorageLimits.Decide(incoming, usage, settings,
  envMax)`. **This is where A15 resolves `override ?? global`** — the
  effective per-user quota for the subject replaces `settings.PerUserQuotaBytes`
  in the decision (the global `MaxFileBytes` per-file cap is untouched —
  A15 is a *per-user total* override, not a per-file one).
- **`src/Kumunita.Web/Controllers/AdminController.Communities.cs`** (the
  `Manage` action at `[Route("admin/accounts/{subjectId}")]` ≈line 236) +
  **`Models/AdminAccountManageViewModel.cs`** + **`Views/Admin/Manage.cshtml`**
  — the admin **account-detail** page A14's `linkPath` lands on. A15 adds a
  "storage quota override" control here (set a bytes/MB value or clear it;
  show the current effective quota = override ?? global, and the resident's
  current usage via `GetPerUserUsageBytesAsync`). This is the surface A14's
  notification links to, so the admin lands here and acts in one step.
- **`src/Kumunita.Web/Controllers/AdminStorageController.cs`** (`Save` ≈
  line 156, calls `settings.SetAsync(...)`) — the **global** quota write
  lane; A15's per-resident write is its sibling (same "one in-caller-session
  write, GlobalAdmin-gated in Web" shape — the ADR 0050 split: the Core
  lane does not re-check standing, the Web boundary does).
- **`src/Kumunita.Web/Views/Account/Storage.cshtml`** +
  **`Models/ResidentStorageViewModel.cs`** — the resident's "My storage"
  page (A14 added the "Request more space" button here). A15's `GET
  /account/storage` read should show the **effective** quota (override ??
  global) so the resident sees the cap the admin raised — the loop's
  feedback comes back to the resident. (The `QuotaUnlimited` /
  `PerUserQuotaBytes` model fields already carry the display.)
- **`src/Kumunita.Core/Usage/CommunityStorageSettings.cs`**'s `[DataClass]`
  attribute — **⚠️ the gotcha below.**
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 test-runner quirk and the
  in-process `dotnet exec` invocation.
- **`git status`** + the branch (A14's `b879dc9c` must be intact; you are
  on `development`).

**The one thing to get right.** A15 is the **effective-quota resolution
`override ?? global`**, done in **one place** (the upload gate's decision
path), with the **C-UP·5 sentinel preserved end-to-end**: a per-resident
override of `0` = **unlimited for that resident**; **no** override = fall
back to the global `PerUserQuotaBytes` (which is itself `0` = unlimited).
The override is **per-resident** (keyed by `subjectId`), **stored at
rest**, and **only the admin writes it** (GlobalAdmin-gated in Web, the
ADR 0050 split — the Core lane does not re-check standing). The resident
can **read** their effective quota (their own `Storage` page) but never
write it. The global quota and the per-file cap are **unchanged** — A15
adds a per-resident *total* override on top, it does not alter the
platform-wide settings.

**⚠️ If A15 adds a new doc type** (the recommended shape — a
`ResidentQuotaOverride` doc keyed by `subjectId`, mirroring
`CommunityStorageSettings`), it **must carry a `[DataClass]` attribute**
(AGENTS.md SEC-integration rule: "a new registered doc type carries
`[DataClass]`"; `CommunityStorageSettings` is the precedent —
`[DataClass(DataClassKind.Operational, Erasure.Keep, ExportScope.Never,
reason: …)]`). The two `SecGate.DataClassCoverageTests` are **already red
(see the known-reds below)** — do **not** add a fourth one by forgetting
the attribute. (A map of overrides on the existing `CommunityStorageSettings`
doc is an acceptable alternative that avoids a new doc type; your call —
but if you go that route, the `SetAsync` / read shapes must still keep one
in-caller-session write.)

The **work**:

1. **The Core override lane (the seam).** A per-resident override read +
   write on **`IStorageSettingsService`** (or a sibling seam — your call,
   but it must be NSubstitutable and keep the C-UP·1 "one in-caller-session
   write" shape), mirroring the existing `SetAsync` / `GetOrCreateAsync`
   pair:
   - `Task<long?> GetResidentQuotaOverrideAsync(string subjectId,
     CancellationToken ct)` — the resident's override in bytes, or
     `null` when no override is set (the `override ?? global` fallback
     signal). `null` (unset) is distinct from `0` (unlimited override).
   - `Task SetResidentQuotaOverrideAsync(string subjectId, long?
     quotaBytes, string actorId, Marten.IDocumentSession session)` — the
     **admin** write lane: `quotaBytes` `null` = **clear** the override
     (back to global); `0` = unlimited for that resident; `> 0` = the
     cap. One in-caller-session write (the C-UP·1 / C3 shape, the
     `SetAsync` precedent). An optional committed-wrapper
     `SetResidentQuotaOverrideAsync(string subjectId, long? quotaBytes,
     string actorId)` (the `SetAsync` committed-lane C09 precedent) lets the
     Web drop the `IDocumentStore` dependency.
   - (If new doc type: register it in the Usage module's `*DocTypes`
     surface the same way `StorageSettingsDocTypes` is, and carry the
     `[DataClass]` — see the gotcha.)
2. **The effective-quota resolution (the loop's heart).** In
   **`UploadGate.CheckUpload`** (`Security/UploadGate.cs`): after the
   platform-space check + the usage read, resolve the **effective**
   per-user quota for the subject = `override ?? settings.PerUserQuotaBytes`
   and run the pure `StorageLimits.Decide` against **that** number.
   Cleanest: add a **pure** helper (the `StorageLimits.EffectiveMaxFileBytes`
   precedent) — e.g.
   `StorageLimits.EffectivePerUserQuota(long? override, CommunityStorageSettings
   settings) => override ?? settings.PerUserQuotaBytes;` — and pass the
   effective value into the decision (either a small `Decide` overload that
   takes an explicit quota, or an adjusted settings struct — keep the
   existing `Decide(incoming, usage, settings, envMax)` signature intact or
   add a sibling; do not break the existing `StorageLimitsTests` pins). The
   **per-file** cap (`MaxFileBytes ?? envMaxBytes`) is untouched.
   - The override read in the gate is **best-effort-tolerant**: if the
     override read throws (a test harness with a null seam), fall back to
     the global quota (the decision still runs; the override is a nudge on
     top, not a hard dependency).
3. **The admin surface (the loop's action).** On the **`Manage`** action
   (`AdminController.Communities.cs`, `[Route("admin/accounts/{subjectId}")]`)
   + **`AdminAccountManageViewModel`** + **`Views/Admin/Manage.cshtml`**:
   - A "storage quota override" card: the current **effective** quota
     (override ?? global, humanized + the unlimited sentinel), the
     resident's current **usage** (`GetPerUserUsageBytesAsync`), and a
     **set / clear** form (a bytes or MB number, or blank to clear → back to
     global). `[ValidateAntiForgeryToken]`, GlobalAdmin-gated at the Web
     boundary (the ADR 0050 split), the actor is `SubjectId(User)` (admin
     self), the target is the route `subjectId`. On save, call
     `SetResidentQuotaOverrideAsync` and flash back (the
     `AdminStorageController.Save` `TempData["info"]` +
     `RedirectToAction` shape).
4. **The resident feedback (the loop closes back).** In the **`GET
   /account/storage`** read (`AccountController.Storage` ≈line 160–195) +
   `ResidentStorageViewModel`: show the **effective** quota (override ??
   global) instead of the raw global, so the resident sees the cap the admin
   raised (the `QuotaUnlimited` = effective `== 0`; `QuotaHuman` /
   `RemainingHuman` from the effective quota). A14's "Request more space"
   button stays (limited branch only).
5. **Keys (4 languages).** Add the admin-surface labels (`admin.
   account_storage_override.*` or the surface's existing key family —
   `en` first, then `de`/`fr`/`da` machine-written + flagged for the parked
   native review) to the relevant `account.json` / `admin.json` surface
   file(s). The **C02 four-language parity**
   (`C02_DataAsEmbeddedResourcesTests`) must stay green.

**Tests:**
- **Core** (the `IStorageSettingsService` override lane, the
  `StorageSettingsService` / `StorageLimits` test idiom):
  (a) `SetResidentQuotaOverrideAsync` stores the override +
  `GetResidentQuotaOverrideAsync` reads it back (and returns `null` when
  cleared / never set — the unset vs. `0`-unlimited distinction);
  (b) **`StorageLimits` effective-quota resolution**: `override ?? global`
  — an override of `5` GB wins over a `2` GB global; a `null` override falls
  back to the global; an override of `0` = unlimited for that resident (the
  sentinel preserved); the per-file cap is independent.
- **Web** (the `UploadGate` decision, the `AttachmentUploadTests` /
  `ContentImageUploadTests` 413-pin idiom): an upload that would **exceed
  the global** quota is **allowed** when the resident has a higher
  override (the override is honoured) and **refused** when it has none
  (falls back to global) — the `CheckUpload` → `413` / `null` mapping.
- **Web** (the admin `Manage` form, the `AdminStorageControllerTests` /
  `AdminAccountManage` view-pin idiom, the house "string pin, no
  TestServer" of `BookmarksControllerTests`): the admin detail page renders
  the override card (set/clear form + the effective quota + usage) and the
  POST save calls `SetResidentQuotaOverrideAsync` with the resident's
  `subjectId` + redirects back + flashes.
- Keep the existing **`StorageLimitsTests`** / **`StorageMetricsTests`** /
  **`AdminStorageControllerTests`** green (A15 must not regress the global
  quota / per-file cap behaviour — `override ?? global` only *adds* a path).
- Keep the **`C02_DataAsEmbeddedResourcesTests`** four-language parity
  (the new keys in all four languages) green.

**Scope rules:**

- **Do not change the global quota / per-file cap semantics** — A15 adds a
  per-resident *total* override on top; the `CommunityStorageSettings`
  doc's `PerUserQuotaBytes` + `MaxFileBytes` (and the `Media__MaxBytes` /
  `Media__MaxPlatformBytes` env lanes) keep their current behaviour.
  `override ?? global` only.
- **Do not change A14's surface** (the "Request more space" button, the
  `storage.request` notify kind) — A15 is the *action* half A14's nudge
  points to; the two are separate commits.
- **Do not add a resident self-serve override** — the override is
  **admin-written only** (the resident's A14 request is the *ask*; the admin
  is the *decision* — the thin-token / fat-authorization + "the admin raises
  it" model). The resident only *reads* their effective quota.
- **No ADR** unless the effective-quota resolution settles a design question
  the existing ADRs don't cover — A15 is a closed loop the plan card already
  decided on (D9's "request more space + per-resident quota override" item);
  the storage / quota model is extended, not re-decided. If you find a
  genuine new decision, write or amend an ADR in the same commit; otherwise
  do not fabricate one.
- **One commit** (the house rule): `feat(ux): per-resident storage quota
  override — the admin half of the storage loop (IMPROVE-UX A15)`.
- **New strings go to en/de/fr/da** (de/fr/da machine-written, flagged for
  the parked native review). **C02 four-language parity**
  (`C02_DataAsEmbeddedResourcesTests`) must stay green.
- **If you add a new doc type**, it carries `[DataClass]` (the
  `CommunityStorageSettings` precedent) — do not add a third red to
  `SecGate.DataClassCoverageTests`.
- The **ux gate** (`tools/ux/ux-measure.py --check`) must not regress — A15
  closes a handoff (not a tracked anti-pattern); confirm it before
  committing.
- ⚠️ **Known pre-existing reds (not yours):** the full Web suite has a
  small set of pre-existing failures that pre-date this lane (the two
  `SecGate.DataClassCoverageTests` + `ImproveHarnessTests.CodeCheck_Gate_Passes`
  — the IMPROVE-CODE Roslyn ratchet + ADR-index drift). Do **not** attribute
  these to A15 or try to fix them here; your A15 suite must be green.

**Before committing:** `dotnet build`, then run the narrowest tests via the
in-process runner (see `testing-and-ci.md`): your new A15 storage tests +
the `StorageLimitsTests` / `StorageMetricsTests` / `AdminStorageControllerTests`
(regression) + the `UploadGate` 413 tests + `C02_DataAsEmbeddedResourcesTests`
(four-language parity). If a test fails, read the error, adjust, continue — a
failed test is a recoverable step, not a stopping point.

Then report: where the override lives (the Core seam you added + the doc
shape you chose — new `ResidentQuotaOverride` doc or a map on
`CommunityStorageSettings`, and the `[DataClass]` if a new doc), where the
effective-quota `override ?? global` is resolved (the `UploadGate` /
`StorageLimits` seam + the sentinel handling), the admin surface you added
(the `Manage` action + view-model + view form), the resident feedback you
wired (the `GET /account/storage` effective-quota read), the keys you
added/updated (en/de/fr/da) and that C02 parity stayed green, the new test(s)
+ count, the commit hash, and the global-quota / per-file-cap surface you
confirmed you did **not** change. Do not commit unrelated work.
