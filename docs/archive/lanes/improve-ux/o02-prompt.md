---
id: o02-prompt
title: "Prompt: IMPROVE-UX O02 — the /admin/status page + admin hub card (render O01's read)"
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
> IMPROVE-UX **O02** — the `/admin/status` page that renders O01's read.
> O01 (just closed) delivered the **Core read seam**
> (`IInstanceStatusService` → the `InstanceStatus` POCO) and the pure
> `ConfigWarningsPolicy`; O01 deliberately built **no page**. O02 is the
> first Operator-lane Web surface: a **GlobalAdmin-gated, read-only**
> `/admin/status` page that composes one `GetStatusAsync` call into the
> rows the operator needs (DB, SMTP, storage, mail-alarm, version/uptime,
> the config warnings), plus a **hub card** on the `/admin` dashboard that
> links to it. O02 **depends on O01** (the seam it renders). **Who is it
> for:** the next coding agent (fresh context). **When to read the rest:**
> when starting O02 (O01 has shipped; the seam exists, the page does not).

# Fresh-context prompt: IMPROVE-UX O02

You are continuing the Kumunita repository on `development` (HEAD
`4b0f71c8`, **O01 just closed** — the operator's Core read seam:
`IInstanceStatusService` + the `InstanceStatus` POCO + the pure
`ConfigWarningsPolicy`, all in `src/Kumunita.Core/InstanceStatus/`,
registered by `AddInstanceStatus()` in the Core composition root, and the
two host-posture booleans bound in `Program.cs` beside the existing options
`Configure` calls). Before it, the Resident / kiosk / deletion /
Admin-moderator lanes all shipped (A15 the per-resident quota override is
the last Admin unit). O02 opens the **Operator lane's Web surface**: the
D8 decision ("`/admin/status` — version, uptime, the `/health` checks, dead
letters + mail alarms, storage, config warnings; read-only, no secrets") is
already made and its **data is already available** in O01's seam. O02
renders it. **O02 depends on O01.** The plan card (in `plan-improve-ux.md`,
the O02 row):

> O02 — `/admin/status` page (GlobalAdmin) + admin hub card; keys.
> Read first: `Views/Admin/Index.cshtml`, `AdminStorageMetricsController.cs`.
> **Done when: page shows all rows.**

The problem it fixes (E10/E11): an operator today learns about trouble only
from **logs** and from **email** (`SupportEmailAlertSink`) — but SMTP may be
exactly what failed, so email is unreliable. O01 gave a single read that
answers "is this instance healthy, and what did I misconfigure?" in one
call; O02 makes it visible in-app. O03 (the dashboard notice) and O04 (the
inbox alarm) are later units — **O02 is only the page + the hub card.**

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the
  comment rule, the don't-pause-mid-task rule, the Razor verification
  doctrine in `docs/dev/razor-verification.md`).
- **`plan-improve-ux.md`** (this lane) — the **O02 card** + the D8 decision
  row + the two Operator journeys ("First boot to healthy", "SMTP breaks") +
  DoD.
- **`src/Kumunita.Core/InstanceStatus/IInstanceStatusService.cs`** +
  **`InstanceStatus.cs`** — the POCO O02 renders. Read the **full shape** so
  the page shows *every* section (DB reachability, SMTP relay, the
  dead-letter / mail-alarm state, the storage snapshot, version + uptime,
  and the `ConfigWarning` list — each a stable key + a secret-free reason).
  This is the **single source of truth** for what the page shows; do not
  re-derive any of it in the Web layer.
- **`src/Kumunita.Core/InstanceStatus/ConfigWarningsPolicy.cs`** +
  **`ConfigWarning.cs`** — the four warning keys and their stable identity,
  so the page can map a warning key → its human reason (and so O03's red-row
  test can key off the same booleans).
- **`src/Kumunita.Web/Controllers/AdminStorageMetricsController.cs`** — the
  **closest page precedent** to copy: a **GlobalAdmin-gated**, thin
  read-only controller over **one** Core read seam, no audit row (read = no
  write), the `AdminXyzController` → `Views/AdminXyz/Index.cshtml`
  convention, and the `IStorageMetricsService` "one read" discipline.
- **`src/Kumunita.Web/Views/AdminStorageMetrics/Index.cshtml`** — the
  **page idiom to mirror**: the `~/Views/Admin/_AdminNav.cshtml` sub-nav,
  the overview **card** grid (`col-6 col-md-3` `card text-bg-light`),
  a local `@functions` byte/format helper, and the plain-English admin-shell
  copy convention (this view is "mostly non-kw-l English").
- **`src/Kumunita.Web/Views/Admin/Index.cshtml`** — the **admin hub** where
  the O02 card goes: the overview-card block + the section-card block
  (`col-md-6 col-lg-4` `<a class="card …" href="/admin/…">` cards — the
  "Storage" card is the one to add "Status" next to).
- **`src/Kumunita.Core/Localization/`** — the key idiom + the four-language
  `Keys/en|de|fr|da/*.json` files (O02's new strings go here; **C02
  four-language parity** must stay green).
- **`src/Kumunita.Web/Program.cs`** — confirm `AddInstanceStatus()` is
  already registered (O01 did this); O02 must **not** add a second
  registration or change the seam.
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 in-process runner
  (`dotnet exec …/Kumunita.Core.Tests.dll -class "…"` / the Web.Tests
  equivalent) and the Web `run_tests` discovery quirk.
- **`git status`** + the branch (O01's `4b0f71c8` must be intact; you are
  on `development`).

**The one thing to get right.** O02 is a **thin, GlobalAdmin-gated,
read-only Web surface** over **one** O01 seam call. Two invariants are
load-bearing:

1. **Render O01's read, don't re-derive it.** One
   `IInstanceStatusService.GetStatusAsync(ct)`; the page shows the POCO's
   sections as-is. Do **not** call `IStorageMetricsService`,
   `ISmtpHealthCheck`, `IDocumentStore`, or the mail seams directly in the
   Web layer — O01 already composed them, and a second copy would drift
   (the `AdminStorageMetricsController` "one read" precedent is the model).
2. **GlobalAdmin + read-only, no audit row.** The gate is the
   `Roles.GlobalAdmin` attribute (the `AdminStorageMetricsController`
   precedent). A read is a read — **no** `AccessAudit` row, **no** POST
   action, **no** form (the C-SM·6 / F8 "read = no audit row" discipline).
   The page carries **no secret value** — O01's POCO already guarantees
   that (warnings are a stable key + a secret-free reason), and the view
   just renders it.

**⚠️ Do NOT touch O01's seam or its tests.** O02 is Web-only: a controller,
a view, the hub card, and keys. It must not change
`IInstanceStatusService`, `InstanceStatus`, `ConfigWarningsPolicy`, or any
Core file. It must not change the `AddInstanceStatus()` registration.

The **work**:

1. **The controller** — `AdminStatusController` (the
   `AdminStorageMetricsController` shape: `[Route("admin/status")]`,
   `[Authorize(Roles = GlobalAdmin)]`), one `GET Index` action that calls
   `IInstanceStatusService.GetStatusAsync(ct)` and renders
   `Views/AdminStatus/Index.cshtml` with the `InstanceStatus` POCO (or a
   thin view-model wrapping it — your call; keep it thin). No POST action.
   No audit row.
2. **The page** — `Views/AdminStatus/Index.cshtml` (mirror
   `AdminStorageMetrics/Index.cshtml`): the `~/Views/Admin/_AdminNav.cshtml`
   sub-nav, an `<h1>` + lede, then one **card/section per POCO section** so
   *every* row shows —
   - **Health**: DB reachable (green/red), SMTP relay healthy (green/red/degraded).
   - **Mail**: the dead-letter count + the mail-alarm firing state (the T4 gate).
   - **Storage**: total used / free / as-of (the `Bytes()` idiom from
     `AdminStorageMetrics/Index.cshtml`).
   - **Instance**: version (+ build SHA where present) + uptime.
   - **Config warnings**: the `ConfigWarning` list rendered as a small table
     (a human reason per warning; an empty state — "no warnings" — when the
     list is empty). This is the row that answers E11 ("nothing warns if
     `SeedAdmin__*` is left set").
   - **Degraded rows** render as an explicit "unavailable/degraded" fact
     (O01's `…Unavailable` statics), not a blank cell and not a thrown 500 —
     the operator still sees the rows that *are* available.
3. **The hub card** — on `/admin` (`Views/Admin/Index.cshtml`), a section
   card next to the existing "Storage" card linking to `/admin/status`
   (title + a one-line description, the same `col-md-6 col-lg-4` card shape).
4. **Keys** — the **four config-warning reasons** (the resident-relevant
   strings) go to the four-language `Keys/{en,de,fr,da}/*.json` (the O01
   scope rule: "a resident/admin-facing reason on the eventual page should
   be a key"). The page chrome may follow the admin-shell plain-English
   convention (the `AdminStorageMetrics` precedent). Whichever you choose,
   **C02 four-language parity** (`C02_DataAsEmbeddedResourcesTests`) must
   stay green.

**Tests** (Web.Tests; the `AdminStorageMetrics` / the Web test idiom for a
GlobalAdmin-gated read page):
- **`/admin/status` is GlobalAdmin-gated**: an anonymous (or non-GlobalAdmin)
  request is refused; a GlobalAdmin session renders the page (a 200 + the
  expected row markers present).
- **the page shows all rows** with a real / faked `InstanceStatus` POCO —
  DB, SMTP, storage, mail-alarm, version/uptime, and a **non-empty**
  `ConfigWarning` list (render the "misconfigured" case: at least one
  warning row is present with its reason).
- **a degraded row degrades, doesn't throw**: feed a POCO where one section
  is the `…Unavailable` static and assert the page renders the "unavailable"
  fact for that section and still returns 200 with the other rows.
- **no audit row** is emitted for the read (the "read = no audit row" pin —
  mirror whatever the `AdminStorageMetrics` read test asserts).
- **the four-language parity** test stays green with O02's new keys.

**Scope rules:**

- **Web-only.** A controller, a view, the hub card, and keys. **Do not
  change O01's Core seam, the POCO, the policy, the DI registration, or any
  Core test.** Do not add a new doc type, migration, or `[DataClass]`
  (the 84-count `DataClassCoverageTests` pin must not move to 85).
- **Read-only, no POST, no form, no audit row** (the
  `AdminStorageMetricsController` precedent).
- **Do not build O03 or O04.** O03 is the dashboard notice, O04 the inbox
  alarm. O02 is the page + the hub card.
- **One commit** (the house rule): `feat(ux): /admin/status page + admin hub
  card — render the operator's instance-status read (IMPROVE-UX O02)`.
- **No ADR** unless a decision is genuinely open — D8 is already decided in
  the plan, and O02 adds a read page over an existing seam, not a new
  decision. If O02 settles a design question the ADRs don't cover, write or
  amend an ADR in the same commit; otherwise do not fabricate one.
- **New strings** go to en/de/fr/da — **C02 four-language parity**
  (`C02_DataAsEmbeddedResourcesTests`) must stay green.
- **Razor verification** (`docs/dev/razor-verification.md`): the rendered
  HTML is the evidence. Confirm the page renders with all rows (a live
  server or the Razor-compilation test, whichever the repo uses for admin
  views) before committing.
- The **ux gate** (`tools/ux/ux-measure.py --check`) must not regress.
- ⚠️ **Known pre-existing reds (not yours):** the full Web suite has a small
  set of pre-existing failures that pre-date this lane (the two
  `SecGate.DataClassCoverageTests` + `ImproveHarnessTests.CodeCheck_Gate_Passes`
  — the IMPROVE-CODE Roslyn ratchet + ADR-index drift). Do **not** attribute
  these to O02 or try to fix them here; your O02 suite must be green.

**Before committing:** `dotnet build`, then run the narrowest tests via the
in-process runner (see `testing-and-ci.md`): your new `/admin/status` page
tests + the **C02 four-language parity** test + the
**`DataClassCoverageTests`** (confirm the **84-count pin is un**broken — it
must not move to 85) + whatever Web.Tests suite covers the
`AdminStorageMetrics` read (confirm O02 didn't disturb it). If a test fails,
read the error, adjust, continue — a failed test is a recoverable step, not
a stopping point.

Then report: the controller + view you added, the hub card's placement on
`/admin`, the **exact POCO sections** the page renders (confirm *all* of
them show, including a degraded row and the config-warning list), the keys
you added (en/de/fr/da), the new test(s) + count, the commit hash, the
confirmed-unbroken **84** registered-doc-type count + the C02 parity result,
and the ux-gate result. Do not commit unrelated work.
