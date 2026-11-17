---
id: o03-prompt
title: "Prompt: IMPROVE-UX O03 — the admin dashboard notice when a status row is red"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0176-notice-strip.md
  - docs/dev/testing-and-ci.md
  - docs/dev/razor-verification.md
lane: improve-ux
updated: 2026-11-12
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **O03** — a one-line, **GlobalAdmin-only** dashboard notice that
> appears **when any instance-status row is red** and links to
> `/admin/status`. O02 (just closed) rendered the O01 read as a page; O03
> makes a bad read *visible without navigating there*. O03 **depends on O01**
> (the seam it reads) and O02 (the page it links to). **Who is it for:** the
> next coding agent (fresh context). **When to read the rest:** when starting
> O03 (O02 has shipped; the page exists, the notice does not).

# Fresh-context prompt: IMPROVE-UX O03

You are continuing the Kumunita repository on `development` (HEAD
`d785dba6`, **O02 just closed** — the `/admin/status` page + the admin hub
card). Before it: **O01** delivered the operator's **Core read seam**
(`IInstanceStatusService` → the `InstanceStatus` POCO + the pure
`ConfigWarningsPolicy`) in `src/Kumunita.Core/InstanceStatus/`, registered by
`AddInstanceStatus()` in the Core composition root; **O02** rendered that POCO
as a GlobalAdmin-gated, read-only page (`AdminStatusController` +
`Views/AdminStatus/Index.cshtml`). O03 is the **last visible piece of D8** that
lands in this round before the docs units: a **one-line dashboard notice,
admins only, that shows when any status row is red**, linking to
`/admin/status`. **O03 depends on O01 (the seam it reads) and O02 (the page it
links to).** The plan card (in `plan-improve-ux.md`, the O03 row):

> O03 — Dashboard notice (notice strip, admins only) when any status row is
> red, linking to `/admin/status`.
> Read first: `notice strip partial (ADR 0174)`, O01. **Depends:** O02.
> **Done when: red row → notice.**

⚠️ **The card's ADR citation is a mis-cite.** The "notice strip" is
**ADR 0176** — `src/Kumunita.Web/Views/Shared/_LayoutNotices.cshtml` (the
`.kmb-notices` strip). ADR 0174 is the MFA QR-code / Security-tab ADR (unrelated).
Use **0176**.

The problem it fixes (E10): today an operator learns about trouble only from
**logs** and **email** (`SupportEmailAlertSink`) — but SMTP may be exactly what
failed, so email is unreliable, and logs are not where an operator looks. O02
gave a page; O03 surfaces a bad read **on the dashboard itself**, so a red
instance announces itself. O04 (the inbox alarm) is a later unit — **O03 is
only the notice.**

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (the ratchet, the comment rule, the don't-pause-mid-task rule,
  the Razor verification doctrine in `docs/dev/razor-verification.md`).
- **`plan-improve-ux.md`** (this lane) — the **O03 card** + the D8 decision row
  + the two Operator journeys ("First boot to healthy", "SMTP breaks") + DoD.
- **`src/Kumunita.Core/InstanceStatus/InstanceStatus.cs`** — the POCO the
  notice keys off. Note the **red/degraded booleans**:
  `DatabaseReachable`, `Mail.Available` / `Mail.Reachable`,
  `MailAlarm.Available` / `MailAlarm.Firing`, `Storage.Available`, and
  `Warnings.Count`. These are the same facts O02's view renders — the
  **single source of truth**; the notice must key off **these** so the page
  and the notice never disagree.
- **`src/Kumunita.Web/Views/AdminStatus/Index.cshtml`** (O02, just shipped) —
  the **exact red/amber rendering** for each section. O03's "is any row red?"
  predicate must mirror this view's green/red/amber logic (so a row O02 shows
  red/amber is what triggers the notice).
- **`src/Kumunita.Web/Views/Shared/_LayoutNotices.cshtml`** (ADR 0176) — the
  **notice strip** the card names: the `.kmb-notices` container, the
  **audience-scoped** notice branches (the guest notice is the precedent for a
  notice that only renders for one standing, and only runs its reads for that
  standing), and the dismissible `data-kmb-notice` idiom.
- **`src/Kumunita.Web/Views/Shared/_AdminOnboardingBanner.cshtml`** — the
  **GlobalAdmin-gated admin-notice** idiom (`KumunitaPrincipal.IsGlobalAdmin`
  as the gate, a `kw-l` closed key for the copy, server-resolved a11y names).
  This is the model if you place the notice on the admin surface rather than
  the global strip.
- **`src/Kumunita.Web/Controllers/AdminStatusController.cs`** (O02) — the
  **one seam call** (`IInstanceStatusService.GetStatusAsync(ct)`). O03's
  notice uses the **same seam**; it does **not** re-derive any of the POCO's
  facts.
- **`src/Kumunita.Core/Localization/`** — the key idiom + the four-language
  `Keys/en|de|fr|da/*.json` files (O03's new strings go here; **C02
  four-language parity** must stay green).
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 in-process runner
  (`dotnet exec …/Kumunita.Web.Tests.dll -class "…"`) and the Web test idiom.
- **`git status`** + the branch (O02's `d785dba6` must be intact; you are on
  `development`).

**The one thing to get right.** O03 is a **thin, GlobalAdmin-gated, read-only
notice** over **one** O01 seam call. Two invariants are load-bearing:

1. **Read O01's POCO, don't re-derive it.** One
   `IInstanceStatusService.GetStatusAsync(ct)`. The "is any row red?" decision
   is computed from the **POCO's booleans** — the same ones O02's view renders
   (DB / Mail / MailAlarm / Storage / Warnings) — so the page and the notice
   can never disagree. Do **not** call `IStorageMetricsService`,
   `ISmtpHealthCheck`, `IDocumentStore`, or the mail seams directly.
2. **GlobalAdmin-only + cheap for everyone else.** The read runs **only** when
   the actor is a GlobalAdmin (the `KumunitaPrincipal.IsGlobalAdmin` claim-only
   short-circuit first, exactly the guest-notice pattern in
   `_LayoutNotices.cshtml`: the reads never run for a Member / Moderator /
   ordinary resident). No audit row (a read is a read), no POST, no form. The
   notice carries **no secret value** (the POCO guarantees a stable key + a
   secret-free reason; the notice only links to `/admin/status`).

**⚠️ Do NOT touch O01's seam or its tests.** O03 is Web-only: the notice
(branch or partial), the red-row predicate (Web layer), and keys. It must not
change `IInstanceStatusService`, `InstanceStatus`, `ConfigWarningsPolicy`, or
any Core file. It must not change the `AddInstanceStatus()` registration.

The **work**:

1. **The red-row predicate** — a small Web-layer helper (a static in the notice
   partial, or a tiny pure function the notice and its test share) that takes
   the `InstanceStatus` POCO and returns **true iff any row is red**:
   `!DatabaseReachable` **or** `!Mail.Available` **or** `!Mail.Reachable`
   **or** `MailAlarm.Firing` **or** `!Storage.Available` **or**
   `Warnings.Count > 0`. It must mirror O02's view green/red/amber logic.
   `// TODO ❓` whether a non-firing alarm with `DeadLetters > 0` is also
   "red" (O02 renders it as a count, not a red row) — default to *no*, and
   note it.
2. **The notice** — GlobalAdmin-only, **one line**, shown when the predicate is
   true, **linking to `/admin/status`** (`<a href="/admin/status">`), a
   dismissible `alert` in the ADR 0176 strip idiom (the `data-kmb-notice`
   pattern). Placement: **recommend the ADR 0176 notice strip**
   (`_LayoutNotices.cshtml`) as a new GlobalAdmin-gated branch (the guest-notice
   audience-scoping precedent); if the global strip is awkward for an admin-only
   notice, use the admin surface instead (the `_AdminOnboardingBanner`
   precedent, wired into `_AdminNav.cshtml`). One of the two — not both. The
   copy is a `kw-l` key; the accessible name is server-resolved (the
   TagHelper can't wrap an attribute).
   - **Do not** call the seam unless the actor is a GlobalAdmin (the cheap
     short-circuit). Do **not** add it to the resident home for non-admins.
3. **Keys** — the notice line (+ any "view status" affordance label) to the
   four-language `Keys/{en,de,fr,da}/*.json`. **C02 four-language parity**
   (`C02_DataAsEmbeddedResourcesTests`) must stay green.

**Tests** (Web.Tests; the `AdminStatusControllerTests` /
`AdminOnboardingBannerTests` idiom — direct-construction + structural
view-source pin, no `RazorPage`/`RenderViewToString` harness in this suite):
- **the predicate** (the pure red-row helper): each single red fact trips it
  (DB down; Mail unavailable; Mail unreachable; alarm firing; Storage
  unavailable; one warning) and a fully-healthy POCO does not (a
  parameterized set of cases over the POCO).
- **the notice is GlobalAdmin-gated**: the notice branch reads
  `KumunitaPrincipal.IsGlobalAdmin` (structural pin on the source, the
  `_AdminOnboardingBanner` precedent) and does **not** run the
  `IInstanceStatusService` read for a non-GlobalAdmin (the cheap short-circuit).
- **red row → notice, healthy → no notice**: the notice markup links to
  `/admin/status` and is present only when the predicate is true (structural
  pin on the source + the predicate unit test).
- **no secret value**: the notice carries only the `kw-l` key + the
  `/admin/status` link — assert the source has **no** warning *reason* text
  inlined (the reasons live on the `/admin/status` page, not the notice).
- **the four-language parity** test stays green with O03's new keys.

**Scope rules:**

- **Web-only.** The notice (branch or partial), the red-row predicate, and
  keys. **Do not change O01's Core seam, the POCO, the policy, the DI
  registration, or any Core test.** Do not add a new doc type, migration, or
  `[DataClass]` (the **84-count** `DataClassCoverageTests` pin must not move).
- **Read-only, no POST, no form, no audit row** (the O02
  `AdminStatusController` precedent).
- **Do not build O04.** O04 is the inbox alarm. O03 is the notice.
- **One commit** (the house rule): `feat(ux): dashboard notice when a status
  row is red — link to /admin/status (IMPROVE-UX O03)`.
- **No ADR** unless a decision is genuinely open — D8 is already decided in
  the plan, and O03 adds a read notice over an existing seam, not a new
  decision. If O03 settles a design question the ADRs don't cover, write or
  amend an ADR in the same commit; otherwise do not fabricate one.
- **New strings** go to en/de/fr/da — **C02 four-language parity** must stay
  green.
- **Razor verification** (`docs/dev/razor-verification.md`): the rendered HTML
  is the evidence. Confirm the notice renders for a GlobalAdmin when a row is
  red and is absent when healthy (a live server or the Razor-compilation
  build, whichever the repo uses for admin views — the O02 precedent is the
  build + structural pin) before committing.
- The **ux gate** (`tools/ux/ux-measure.py --check`) must not regress.
- ⚠️ **Known pre-existing reds (not yours):** the full Web suite has a small
  set of pre-existing failures that pre-date this lane (the two
  `SecGate.DataClassCoverageTests` + `ImproveHarnessTests.CodeCheck_Gate_Passes`
  — the IMPROVE-CODE Roslyn ratchet + ADR-index drift). Do **not** attribute
  these to O03 or try to fix them here; your O03 suite must be green.

**Before committing:** `dotnet build`, then run the narrowest tests via the
in-process runner (see `testing-and-ci.md`): your new O03 notice/predicate
tests + the **C02 four-language parity** test + the **`DataClassCoverageTests`**
(confirm the **84-count pin is un**broken — it must not move) + the O02
`AdminStatusControllerTests` (confirm O03 didn't disturb the page). If a test
fails, read the error, adjust, continue — a failed test is a recoverable step,
not a stopping point.

Then report: where the notice lives (strip vs. admin surface) and the exact
GlobalAdmin-gate + cheap-short-circuit, the red-row predicate you wrote (and
the `// TODO ❓` answer), the keys you added (en/de/fr/da), the new test(s) +
count, the commit hash, the confirmed-unbroken **84** registered-doc-type count
+ the C02 parity result, and the ux-gate result. Do not commit unrelated work.
