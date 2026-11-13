---
id: o01-prompt
title: "Prompt: IMPROVE-UX O01 — the operator's instance-status read (the Core seam for /admin/status)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/dev/testing-and-ci.md
  - docs/ops/configuration-reference.md
lane: improve-ux
updated: 2026-11-12
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **O01** — the operator's in-app instance-status read. O01 is
> the **Core seam** of the **Operator lane** (the last lane to start): it
> gathers the facts an operator needs to know about their own instance —
> the `/health` checks, the mail dead-letter count, the mail-alarm state,
> storage metrics, version/uptime, and a small set of **config warnings**
> — into one read-only `IInstanceStatusService`, so the next unit (O02) can
> render `/admin/status` without re-deriving any of it. O01 is **pure Core,
> read-only, no secrets returned** — no page, no controller, no new doc type.
> **Who is it for:** the next coding agent (fresh context). **When to read
> the rest:** when starting O01 (the Resident / Admin-mod / kiosk / deletion
> lanes have all shipped; the Operator lane has not started).

# Fresh-context prompt: IMPROVE-UX O01

You are continuing the Kumunita repository on `development` (HEAD
`89f51248`, **A15 just closed** — the per-resident storage quota override,
the last unit of the Admin/moderator lane; before it A14 the "request more
space" nudge, A13 the guest-form account picker, A12–A07 the invitation
lane, and the Resident / kiosk / deletion lanes). O01 opens the
**Operator lane**: the plan's D8 decision ("`/admin/status` — version,
uptime, the `/health` checks, dead letters + mail alarms, storage, config
warnings such as `SeedAdmin__*` still set; read-only, no secrets") is
already made. O01 delivers only the **Core read seam** behind it — O02 is
the page, O03 the dashboard notice, O04 the inbox alarm. O01 has **no
dependencies**. The plan card (in `plan-improve-ux.md`, the O01 row):

> O01 — Core `IInstanceStatusService`: reuse the `/health` checks, dead-letter
> count, mail alarms, storage metrics, version/uptime, and a pure
> `ConfigWarningsPolicy` (`SeedAdmin__*` set after admin exists,
> `SampleData__Enabled` in Production, `Health__Token` unset, Data Protection
> keys not persisted); policy unit tests.
> Read first: `HealthController.cs`, `MailAlarmsService.cs`,
> `docs/ops/configuration-reference.md`. **Done when: policy tests green;
> no secret values returned.**

The problem it fixes (E10/E11): an operator today learns about trouble only
from **logs** and from **email** (`SupportEmailAlertSink`) — but SMTP may be
exactly what failed, so email is unreliable. There is no in-app surface that
answers "is this instance healthy, and what did I misconfigure?" in one
read. O01 gives the next unit a single Core seam to ask that — reusing the
seams that already know the answers, not re-deriving them.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the
  comment rule, the don't-pause-mid-task rule).
- **`plan-improve-ux.md`** (this lane) — the **O01 card** + the D8 decision
  row + the two Operator journeys ("First boot to healthy", "SMTP breaks") +
  DoD.
- **`src/Kumunita.Web/Controllers/HealthController.cs`** — the existing
  `/health` checks O01 **reuses**: DB connectivity
  (`IDocumentStore.Storage.Database.AssertConnectivityAsync`), the SMTP relay
  handshake (`ISmtpHealthCheck`, 60 s cached), and the dead-letter count
  (`IEmailDeadLetterCounter`). Note the **M4/SEC U16 split** — the full
  diagnostic is GlobalAdmin/token-gated; the minimal liveness probe is
  anonymous. O01 is an **authenticated-admin** read, so it may expose the
  full facts — but it must still **never return a secret value** (see below).
- **`src/Kumunita.Core/Notifications/MailAlarmsService.cs`** (+
  `MailAlarmsTick.cs`, and `tests/Kumunita.Core.Tests/MailAlarmsServiceTests.cs`
  for the idiom) — the F18 mail alarm: the T4 gate counts
  `SuppressedAddress` bounce/complaint rows and fires an `AlertEvent`
  through `IAlertSink`. O01 **reuses** the read (the dead-letter /
  suppressed-address counts + the threshold) rather than re-implementing the
  count; the alarm *firing* is unchanged (O04 adds the inbox leg, not O01).
- **`src/Kumunita.Core/Identity/HealthOptions.cs`** — the `Health__Token`
  setting (a `string`, the diagnostic gate). The config warning is about
  its **presence/absence**, not its value.
- **`docs/ops/configuration-reference.md`** — the authoritative env table.
  The config-warning inputs live here: `SeedAdmin__Email` / `SeedAdmin__Token`
  (one-time first-run credentials, "remove after first login"),
  `SampleData__Enabled` (mock-neighborhood seeder — a real deployment omits
  it), `Health__Token` (optional shared secret, "recommended for
  production"), `DataProtection__KeysDirectory` (omit = in-memory keyring —
  sessions die on redeploy). These are the four warnings the card names.
- **`src/Kumunita.Core/Usage/IStorageMetricsService.cs`** (+
  `StorageMetricsService.cs`, `StorageMetricsTests.cs`) — the storage
  snapshot O01 reuses (`GetSnapshotAsync` → total used / free / as-of).
- **`src/Kumunita.Core/Usage/StorageLimits.cs`** +
  **`src/Kumunita.Core/Usage/UsagePurgePolicy.cs`** — the **pure policy**
  precedent O01's `ConfigWarningsPolicy` mirrors (pure, no DB, no session,
  no HTTP; a small input snapshot in → a result out; unit-tested without a
  `PostgresFixture`).
- **`src/Kumunita.Core/Usage/DependencyInjection.cs`** — the module
  self-registration shape (`UsageModule.AddUsage`) O01's seam registration
  follows (or a sibling module / the Core composition root — your call, but
  it must be registered where the Web host resolves it).
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 test-runner quirk and the
  in-process `dotnet exec` invocation.
- **`git status`** + the branch (A15's `89f51248` must be intact; you are
  on `development`).

**The one thing to get right.** O01 is a **read-only Core seam** that
**reuses** the existing health / mail / storage seams (do **not** re-derive
their reads) and returns a **single POCO** the next unit can render. Two
invariants are load-bearing:

1. **No secret values are ever returned.** The config warnings are about a
   setting's **presence / absence / posture** — `SeedAdmin__*` is still set,
   `Health__Token` is unset, `DataProtection__KeysDirectory` is not
   persisted, `SampleData__Enabled` is on in Production. The service
   **never echoes the credential or key value** (not the token, not the
   connection string, not the SMTP password, not a key path's contents).
   A warning is a `bool` / a short reason, full stop.
2. **Core stays HTTP-free (ADR 0006-D).** `IInstanceStatusService` returns a
   POCO (counts, booleans, version, a `DateTimeOffset` uptime, a warning
   list). No `ActionResult`, no `IHttpContext`, no `IFormFile`. The Web
   page (O02) is the only place it becomes HTTP.

**⚠️ Do NOT add a new doc type.** O01 is read-only — it composes existing
docs / settings, it stores nothing. Adding a `[DataClass]`-attributed
registered type would break the Core.Tests
`DataClassCoverageTests.EveryRegisteredDocType_HasADataClass` gate
(**hard-pins the registered count at 84**) and add a third red to the
already-red Web `SecGate.DataClassCoverageTests`. There is nothing to
annotate here — that is correct, not an omission.

The **work**:

1. **The pure `ConfigWarningsPolicy`** (the `UsagePurgePolicy` /
   `StorageLimits` precedent — pure, no DB, no session, no HTTP). A small
   input POCO carrying the four warning inputs **as booleans/posture** (e.g.
   `seedAdminCredentialsSet`, `sampleDataEnabled`, `isProduction`,
   `healthTokenSet`, `dataProtectionKeysPersisted`) → a list of
   warnings (a stable key + a short, secret-free reason per warning). The
   four rules the card names, each a pure branch:
   - `SeedAdmin__*` still set **and** a GlobalAdmin already exists →
     "remove the one-time setup credentials from env" (the
     `SeedAdminOptions` / `AdminSetupController` step-4 precedent —
     `SeedAdmin__Email` / `SeedAdmin__Token` are one-time).
   - `SampleData__Enabled` true in `Production` → "the mock-neighborhood
     seeder is on in a real deployment".
   - `Health__Token` unset → "the full `/health` payload is open to any
     GlobalAdmin session; set a shared secret for production".
   - `DataProtection__KeysDirectory` not persisted → "sign-in + antiforgery
     state is in-memory and dies on redeploy".
   (The "admin already exists" input is a read the service supplies — count
   of GlobalAdmins, or a `bool` — never the identities.)
2. **`IInstanceStatusService`** (+ impl) — the read-only seam. One
   `GetStatusAsync(CancellationToken)` returning the POCO, composed from the
   **reused** seams (no second copies of the reads):
   - `/health` checks: DB reachable, SMTP relay healthy (the
     `ISmtpHealthCheck` shape — best-effort / cached, a failure is a
     degraded fact, not a throw), dead-letter count (`IEmailDeadLetterCounter`).
   - mail alarms: the F18 T4 gate state (`MailAlarmsService` read — the
     bounce/complaint count vs the `AlertThreshold`, and whether an alarm is
     currently firing). The alarm **firing** is unchanged.
   - storage: the `IStorageMetricsService.GetSnapshotAsync` total/free/as-of.
   - version + uptime: the running assembly version (build SHA where
     available, the `HealthController` `build` field precedent) + process
     uptime (a process-start `DateTimeOffset` — inject or expose so tests
     are deterministic, the `MailAlarmsService` `now`-injection precedent).
   - config warnings: the pure `ConfigWarningsPolicy` over the inputs the
     service reads (the four booleans above; the "admin exists" read).
   - **Resilient:** a failing sub-read (no DB, no SMTP, an un-registered
     seam in a minimal harness) degrades that row to a "degraded/unavailable"
     fact rather than throwing the whole read — the operator still gets the
     rows that *are* available (the `/health` degraded-probe posture).
3. **Register the seam** (the `UsageModule.AddUsage` module-self-registration
   shape, or the Core composition root — your call). It is a new transient
   read seam; compose it from the seams it reuses (the
   `IStorageMetricsService` / mail / identity reads the host already
   registers). Do **not** change any of those reused seams.

**Tests** (Core.Tests; the `MailAlarmsServiceTests` / `StorageMetricsTests`
/ `StorageLimitsTests` idioms):
- **`ConfigWarningsPolicy` (pure — no `PostgresFixture`):** each of the
  four rules, one test each (fires when the posture is bad, stays quiet
  when it is good), **and** a test that a warning carries **no secret value**
  (the reason string is stable and contains none of the credential/token/key
  material even when the input "value" is set). A combined test: a
  misconfigured instance returns exactly the expected warning set.
- **`IInstanceStatusService`:** with the reused seams NSubstituted —
  (a) the happy read returns the composed POCO (DB ok, SMTP ok, storage
  snapshot, version/uptime, zero warnings) and **calls each reused seam
  once** (the "reuse, don't re-derive" pin); (b) a sub-read failure
  (dead-letter counter throws / no store) degrades that row to a fact, the
  read still returns; (c) the returned POCO **carries no secret** — assert
  the fields are counts / booleans / version / warning-reasons, and that a
  set credential/token input never appears in the result (the "no secret
  values returned" DoD).

**Scope rules:**

- **Read-only, no writes.** O01 composes existing reads; it adds no doc, no
  migration, no `[DataClass]`, no schema. (The IS20h 84-count pin + the two
  known-red `SecGate` gates must stay exactly as they are.)
- **Do not change the reused seams** — `HealthController`, `MailAlarmsService`
  (and its T4 firing), `IStorageMetricsService`, the mail
  `IAlertSink` binding. O01 *reads* their state; it does not alter it.
- **Do not build the page or the notice.** O02 is `/admin/status`, O03 the
  dashboard notice, O04 the inbox alarm. O01 is the seam + the pure policy
  + their tests.
- **No ADR unless a decision is genuinely open** — D8 (the operator's view)
  is already decided in the plan, and O01 adds a read seam + a pure policy,
  not a new data model or a new decision. If you find O01 actually settles a
  design question the ADRs don't cover, write or amend an ADR in the same
  commit; otherwise do not fabricate one.
- **One commit** (the house rule): `feat(ux): instance-status Core seam —
  the operator's read of their own instance (IMPROVE-UX O01)`.
- **New strings** (the warning reasons, if surfaced) go to en/de/fr/da —
  **C02 four-language parity** (`C02_DataAsEmbeddedResourcesTests`) must stay
  green. (If the warnings are plain-English Core constants rather than UI
  keys, that is acceptable — your call — but a resident/admin-facing reason
  on the eventual page should be a key.)
- **No secret values returned** (the DoD) — pin it in a test.
- **Core stays HTTP-free** (ADR 0006-D) — no Web types in the seam.
- The **ux gate** (`tools/ux/ux-measure.py --check`) must not regress — O01
  is Core-only (no views), so it should be untouched; confirm before
  committing.
- ⚠️ **Known pre-existing reds (not yours):** the full Web suite has a small
  set of pre-existing failures that pre-date this lane (the two
  `SecGate.DataClassCoverageTests` + `ImproveHarnessTests.CodeCheck_Gate_Passes`
  — the IMPROVE-CODE Roslyn ratchet + ADR-index drift). Do **not** attribute
  these to O01 or try to fix them here; your O01 suite must be green.

**Before committing:** `dotnet build`, then run the narrowest tests via the
in-process runner (see `testing-and-ci.md`): your new `ConfigWarningsPolicy`
tests + your `IInstanceStatusService` tests + the `MailAlarmsServiceTests`
/ `StorageMetricsTests` / `StorageLimitsTests` regression suites + the
`C02_DataAsEmbeddedResourcesTests` (four-language parity) + the
`DataClassCoverageTests` (confirm the 84-count pin is **un**broken — it must
not move to 85). If a test fails, read the error, adjust, continue — a failed
test is a recoverable step, not a stopping point.

Then report: where the seam lives (the `IInstanceStatusService` + the module
you registered it in), exactly which existing seams you **reused** (DB / SMTP
/ dead-letter / mail-alarm / storage / version-uptime) and which you did
**not** re-derive, the pure `ConfigWarningsPolicy` (the four rules + the
input/posture shape), the **no-secret-value** test + the **degraded-read**
test, the new test(s) + count, the commit hash, and the confirmed-unbroken
registered-doc-type count (84) + the C02 parity result. Do not commit
unrelated work.
