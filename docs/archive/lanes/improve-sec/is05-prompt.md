---
id: is05-prompt
title: "IS05 prompt — Sentry breadcrumb floor (prove, then fix)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS05 — Sentry breadcrumb floor (prove, then fix)

## What you are doing

You are the **fifth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS04 are committed (plan,
design Part 1/2a/2b, and the gate harness `tests/Kumunita.Web.Tests/SecGate/`).
Your unit is **IS05** — the first Phase A (S3) fix. Sentry's `ILogger`
integration ships **Information-level logs (which carry `{Email}`) as
breadcrumbs** that the existing `SentryPiiScrubber` cannot redact — the
code's own comments call this "vacuous". This unit **lowers the breadcrumb
floor to `Warning`** so Information logs stop reaching the third party, and
**proves it with a test before and after the change**.

**Deliverables (≤ 3 files + the handoff-notes append):**

1. `src/Kumunita.Web/Program.cs`, **the `UseSentry` block only** (the
   `builder.WebHost.UseSentry(o => { … })` lambda, ≈ lines 455–470): add
   `o.MinimumBreadcrumbLevel = LogLevel.Warning;` and
   `o.MinimumEventLevel = LogLevel.Error;`.
2. The **comments that call breadcrumbs "vacuous" are corrected** so they no
   longer claim the redaction is vacuous now that a floor exists: the
   `UseSentry` drift note (≈ `:443`), the `SentryPiiScrubber` header comment
   (≈ `:1364`), and the in-scrubber note (≈ `:1441`) —
   `grep -n vacuous src/Kumunita.Web/Program.cs` must return **0 hits**
   afterwards. Do **not** rewrite the scrubber logic; adjust the prose only.
3. `tests/Kumunita.Web.Tests/SentryBreadcrumbPiiTests.cs` — an in-memory
   `ITransport` (or the `F10U3` app-host idiom, see reading order) that
   captures a logged Information line carrying `a@example.test` and a
   captured exception, then asserts the breadcrumb floor and scrubber below.

**Tests (exact names, `§2.3`):**
- `InformationLog_IsNotABreadcrumb`
- `WarningLog_IsABreadcrumb_WithoutRequestPii`
- `ScrubberStillRedactsHeadersCookiesQuery`

**You are NOT** doing IS06+ (the `LogRedact` helper, the seeder redaction, the
log-PII gate), IS16+ (the `[DataClass]` attribute), or any other unit. You touch
**only** `src/Kumunita.Web/Program.cs` (the Sentry block + the three "vacuous"
comments), the one new test file, and the handoff notes. **Do not** touch
`src/Kumunita.Core`, `src/Kumunita.Web/Controllers`, `docs/adr/`, or any file
other than the deliverables. IS05 is this lane's **second** and final `Program.cs`
touch only alongside IS34b — merge it on its own.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- `Sentry.AspNetCore` is pinned to **6.12.1**
  (`src/Kumunita.Web/Kumunita.Web.csproj:46`); the `UseSentry` call is
  OFF-by-default (the DSN gate at `Program.cs:446`), so the block only runs
  when `SENTRY_DSN` is set.
- `dotnet build` is available; the test project is `tests/Kumunita.Web.Tests`.
- Commit style: `test(improve-sec): IS05 — <short description>`.

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS05** unit spec under "Phase A — S3" of
   `docs/plans-milestones/plan-improve-sec.md` — your contract.
3. `docs/design/improve-sec-design.md` **§2.2** (the `LogRedact`/Sentry seam
   notes), **§2.3** (the three test names above), and **§2.4** (the IS05
   drift-guard anchor: `o.MinimumBreadcrumbLevel = LogLevel.Warning;` inside
   the `UseSentry` block; `grep -n vacuous … → 0 hits`).
4. `grep -n "UseSentry" -A25 src/Kumunita.Web/Program.cs` — the current
   Sentry block (the config-action overload is `builder.WebHost.UseSentry(o =>
   …)` on `IWebHostBuilder`, **not** the `WebApplicationBuilder` receiver —
   that 7.x shape is not in 6.12.1).
5. `grep -n vacuous src/Kumunita.Web/Program.cs` — the three comments to
   correct (≈ `:443`, `:1364`, `:1441`); read each ±10 lines before editing.
6. `tests/Kumunita.Web.Tests/F10U3_ErrorTrackerTests.cs` lines 1–80 and 170–200 —
   the **harness idiom for 6.12.1**: build a real app host
   (`WebApplication.CreateBuilder()` → `builder.WebHost.UseSentry(…)` →
   `builder.Build()`) because `UseSentry` defers its SDK registrations into a
   `ConfigureLogging` delegate that only runs at host `Build()`. Reuse that
   pattern to register the in-memory transport, or drive the floor + scrubber
   directly.
7. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
   `## IS01` through `## IS04` sections (IS04 notes the in-process runner form
   for this machine).

## The work

### 1. `Program.cs` — the breadcrumb floor

- Add inside the existing `builder.WebHost.UseSentry(o => { … })` lambda:
  `o.MinimumBreadcrumbLevel = LogLevel.Warning;` and
  `o.MinimumEventLevel = LogLevel.Error;`.
- **Before editing, verify the member names exist in 6.12.1** — the plan says
  to check by reflection on `SentryAspNetCoreOptions` (6.12.1). If
  `MinimumBreadcrumbLevel` / `MinimumEventLevel` are absent, or are on a
  different type than the `o =>` receiver, **write a drift pause in the
  handoff note and stop** (do not invent a member, do not change the pin's
  contract). `LogLevel` is `Microsoft.Extensions.Logging.LogLevel` (already
  referenced elsewhere in `Program.cs`; add the `using` if needed).

### 2. Correct the three "vacuous" comments

- Each of the three `vacuous` notes currently asserts the breadcrumb-redaction
  surface is vacuous/infeasible. Update the prose to reflect the new floor
  (Information logs no longer become breadcrumbs; the scrubber still redacts
  headers/cookies/query on the event). Keep them accurate and short.
- **Do not** change `o.SendDefaultPii`, `o.DisableSentryHttpMessageHandler`,
  `o.SetBeforeSend`, `o.AddInAppExclude`, or any other line. Templates/logic
  stay; only the three comments' wording changes.

### 3. `SentryBreadcrumbPiiTests.cs`

- Namespace `Kumunita.Web.Tests;`.
- `InformationLog_IsNotABreadcrumb` — with `MinimumBreadcrumbLevel = Warning`,
  a logged **Information** line carrying `a@example.test` produces **no**
  breadcrumb (and its value is not observable anywhere on the captured event).
- `WarningLog_IsABreadcrumb_WithoutRequestPii` — a logged **Warning** line
  *does* become a breadcrumb, and the breadcrumb carries no request PII.
- `ScrubberStillRedactsHeadersCookiesQuery` — invoke
  `SentryPiiScrubber.Scrub` on a synthetic `SentryEvent` and assert the
  header/cookie/query PII is redacted (reuse the `F10U3` leg-3 finding:
  `SentryRequest.Headers` is getter-only backed by a redacting dictionary; the
  load-bearing contract is "the PII value is never observable", the exact
  token may differ by surface).

## Verification (all green before you finish)

- [ ] `dotnet build` — **0 errors**.
- [ ] `SentryBreadcrumbPiiTests` — **3 passed, 0 failed**. Run via the
      in-process runner the repo documents
      (`docs/dev/testing-and-ci.md`): build, then
      `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
      -class Kumunita.Web.Tests.SentryBreadcrumbPiiTests`.
      (On this machine `dotnet test -- -class` reports "Zero tests ran / exit
      5" — a known runner quirk, not a real failure.)
- [ ] `F10U3_ErrorTrackerTests` still green (the floor must not break the
      existing F10 seams).
- [ ] `grep -n vacuous src/Kumunita.Web/Program.cs` → **0 hits**.
- [ ] `git status` shows only `Program.cs` (the Sentry block + 3 comments),
      `SentryBreadcrumbPiiTests.cs`, and the handoff-notes append.

## Definition of done (IS05)

- [ ] `Program.cs` `UseSentry` block sets
      `o.MinimumBreadcrumbLevel = LogLevel.Warning;` (and the event floor),
      verified to compile against 6.12.1.
- [ ] All three "vacuous" comments corrected; `grep -n vacuous` → 0.
- [ ] The three `SentryBreadcrumbPiiTests` pass; `F10U3_ErrorTrackerTests`
      green; `dotnet build` 0 errors.
- [ ] Handoff note has a `## IS05 — Sentry breadcrumb floor` section (3–8
      lines: files touched, the two floor lines, the 3 tests + pass count, the
      `vacuous`-comment correction, next unit IS06).
- [ ] One commit:
      `test(improve-sec): IS05 — Sentry breadcrumb floor (prove, then fix)`.

**If a DoD line fails, stop and say which one and why — do not fix it
silently, and do not start IS06's work to make it pass.** A failed check is a
recoverable step, not a stopping point: read the error, adjust within the
unit's scope, continue. If `MinimumBreadcrumbLevel`/`MinimumEventLevel` are not
the real 6.12.1 member names, **that is a drift pause, not a DoD failure** —
record it and hand off.
