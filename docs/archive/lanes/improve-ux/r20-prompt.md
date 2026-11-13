---
id: r20-prompt
title: "Prompt: IMPROVE-UX R20 — the guest notice gains its window end + a way to reach the admins"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0120-guest-accounts.md
  - docs/adr/0019-timezone-platform-default-and-resident-override.md
  - docs/adr/0020-date-format-platform-default-and-resident-override.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R20 — close the guest's handoff loop (finding E18): the guest shell notice
> today says "for the window they set" with no end date and no way to reach a
> human, and a guest whose window has lapsed is left on a bare shell. R20 makes
> the notice show **when the window ends (in the guest's zone)** and **a link
> to message the admins**, and gives the lapsed state a **friendly page**
> instead of a dead end. **Who is it for:** the next coding agent (fresh
> context). **When to read the rest:** when starting R20 (the first "smaller
> items" unit in `D9` of the plan — no dependencies, self-contained).

# Fresh-context prompt: IMPROVE-UX R20

You are continuing the Kumunita repository on `development` (HEAD
`547e44a4`, R16 landed — the whole ADR 0302 kiosk-link lane R15–R19 is now
shipped). The next resident-facing unit is **R20**: the guest notice. The plan
card (in `plan-improve-ux.md`, the R20 row):

> R20 — Guest notice shows access end (resident's zone) + link to message the
> admins; friendly page when the window has passed; keys.
> Read first: `GuestAccess.cs`, notice strip partial (ADR 0174).
> Done when: **guest sees end date.**

This is a small **L**-severity "closer" unit (the `D9` smaller-items row): one
seam, ≤ ~4 files (plus translation JSON), no ADR — it is an honest-wording and
handoff fix to an existing surface (the `E18` finding: "Guest notice gives no
end date or next step"). The guest *standing* (M19 / ADR 0120) is already
shipped and is **unchanged** — you are touching the *notice* and the *lapsed*
experience, not the authorization.

## The shape you are building

Two states of a signed-in guest (their account exists; `Profile.IsGuest` is
`true`; the admin set a bounded `ValidFrom`/`ValidUntil` window):

1. **Window live (the current notice, improved).** The guest sees the existing
   shell notice (the `account.guest_welcome` block in
   `Views/Shared/_LayoutNotices.cshtml`, the `data-kmb-notice="guest"` strip).
   Today it reads "…for the window they set" with **no date** and **no next
   step**. R20 makes it show **when the window ends** (rendered in the guest's
   own effective zone) and a **"message the admins"** affordance that reaches
   a human.
2. **Window lapsed (the dead end, fixed).** Today, once `now ≥ ValidUntil` the
   `Guest` role is **not** minted, the notice simply *disappears*, and the
   guest is left on the shell with no explanation. R20 gives this state a
   **friendly page/notice**: "your guest access has ended — here is how to
   reach the admins" (the same "message the admins" affordance).

The **"message the admins" affordance** is the resident feedback surface
`GET /issues/new` (the `IssueController` — `GET /issues/new` has **no**
`[Authorize]` gate and `POST /issues/new` records under
`KumunitaPrincipal.SubjectId(User)`, so a signed-in guest reaches it fine — the
R08/R22 feedback channel). Do **not** invent a new contact surface; reuse it.

## One trap that will bite you — read before you write

**The `Guest` role is only present while the window is LIVE.** The current
partial is gated on `KumunitaPrincipal.HasRole(User, "Guest")`, and that role
is minted by `KumunitaClaimsPrincipalFactory` **only** when
`Profile.IsGuest == true` **and** the window is live (`ValidFrom ≤ now <
ValidUntil`). Consequences:

- **The end date is not a claim** — it is the `GuestAccess.ValidUntil` document
  value. To show it you must read the document via
  `IIdentityService.GetGuestAccessAsync(subjectId)` (a pure Core read) for the
  guest's subject, and format it with `EffectiveTimezoneResolver` +
  `EffectiveDateFormatResolver` (ADR 0019/0020) in the **guest's** effective
  zone — the same seam that renders every other timestamp. **Gate that read on
  the guest branch** so ordinary residents never pay for it (one neighborhood,
  one or two guests — the cost is negligible, but the read must not run for a
  Member/Moderator/GlobalAdmin).
- **The lapsed state has NO `Guest` role**, so a role-based gate can never see
  it. Detect the lapsed guest by `Profile.IsGuest == true` **and** the window
  already past (`now ≥ ValidUntil`) — i.e. read the profile + the
  `GuestAccess` doc, not the role. This is the whole of the "friendly page"
  branch.
- The notice is a **site-wide layout partial** (it renders on every page). Keep
  both branches cheap and non-blocking: a null/absent document degrades to the
  existing text (the en-floor, no crash — the `account.guest_welcome`
  `kw-l` pattern already does this for the label).

## Before writing, read (in this order)

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** — the
  doctrine (the comment rule, the ratchet, the don't-pause-mid-task rule,
  Razor verification).
- **`src/Kumunita.Core/Identity/GuestAccess.cs`** — the doc: `SubjectId`,
  `ValidFrom`, `ValidUntil`, `AllowedSurfaces`, `SetByAdmin`. And
  **`src/Kumunita.Core/Identity/IIdentityService.cs`** — the
  `GetGuestAccessAsync(string subjectId)` read seam (`Task<GuestAccess?>`).
- **`src/Kumunita.Web/Views/Shared/_LayoutNotices.cshtml`** — the exact block
  you are changing (the `data-kmb-notice="guest"` strip, the
  `account.guest_welcome` `kw-l`), and the `_noticesMore` label-resolution
  pattern for how an async partial resolves a localized string safely.
- **`src/Kumunita.Web/Security/KumunitaClaimsPrincipalFactory.cs`** — the
  `BuildRoleListAsync` guest branch (how `Roles.Guest` is minted; the
  `IsGuest` + window check) — so you mirror its lapsed-detection, not the
  role, for the "window passed" state.
- **`src/Kumunita.Web/Localization/EffectiveTimezoneResolver.cs`** +
  **`EffectiveDateFormatResolver.cs`** — the zone/format seams (ADR
  0019/0020). Find one existing call site that renders a `DateTimeOffset` in
  the effective zone (search `EffectiveTimezoneResolver` usages in a
  controller/view) and copy that idiom rather than reinventing the format.
- **`docs/adr/0120-guest-accounts.md`** — the guest model (D2/D3: the bounded
  window; D4: the closed `AllowedSurfaces` set) — so your wording matches what
  a guest is allowed.
- **`docs/dev/testing-and-ci.md`** — the test-runner quirk + the in-process
  `dotnet exec` invocation.
- **`git status`** (R16's commit must be intact; you are on `development`).

## The work

1. **The live notice — end date + reach the admins.** Extend the existing
   `data-kmb-notice="guest"` block in `_LayoutNotices.cshtml`: when the guest
   branch is active, read `GetGuestAccessAsync(KumunitaPrincipal.SubjectId(User))`,
   format `ValidUntil` in the guest's effective zone (`EffectiveTimezoneResolver`
   + `EffectiveDateFormatResolver`), and render it into the notice next to the
   existing text. Add a **"message the admins"** link to `GET /issues/new` (the
   existing feedback surface — the R08/R22 channel; a labelled link, not a form).
   A `null`/absent document degrades to the current text (en-floor, no crash).
2. **The lapsed state — the friendly page.** Add a branch for the signed-in
   guest whose window has passed (`Profile.IsGuest == true` and
   `now ≥ ValidUntil`, detected via the profile + the `GuestAccess` doc —
   **not** the `Guest` role, which is absent here): a clear "your guest access
   has ended" message + the same "message the admins" link. Keep it in the
   notice strip (consistent with the other notices) unless a dedicated page is
   clearly better — if you add a page, gate it so it only renders for a
   lapsed guest and never blocks a resident.
3. **Keys.** New/changed strings go to **en/de/fr/da** (de/fr/da
   machine-written, flagged for the parked native review in the UI-refresh
   lane). Reuse `account.guest_welcome` where the wording still holds; add
   sibling keys (e.g. `account.guest_window_end`,
   `account.guest_reach_admins`, `account.guest_window_expired`) rather than
   overloading one key. Update the en floor in the `kw-l` tag.

## Scope rules

- **Do not touch** the guest *authorization* (`KumunitaClaimsPrincipalFactory`
  guest minting, `GuestSurface`, `AllowedSurfaces`, the `TimeLimitMiddleware`,
  or the `GuestAccess` write lane `SetGuestAccessAsync`) — R20 is the **notice
  + the lapsed experience** only. ADR 0120 stands unchanged; no new ADR.
- **Do not touch** the other notices (flash toast, pinned announcement,
  onboarding banner, what's-new toast) or `notice-strip.js` — you are editing
  the single `data-kmb-notice="guest"` block (and, if needed, one new block
  for the lapsed state), the layout's notice container otherwise stays as is.
- **One commit per change** (the house rule): the partial + the lapsed branch +
  the translation JSON land in **one** `feat(ux):` commit
  (`feat(ux): the guest notice shows the window end + a way to reach the
  admins (IMPROVE-UX R20)`).
- **The claim-shape invariant B and the translation parity test** must stay
  green (you add keys, not claims — run the parity test).

**Before committing:** run the narrowest tests via the in-process runner (see
`testing-and-ci.md`): the Web test that pins the guest notice (or add one in
`tests/Kumunita.Web.Tests/` following the house "string pin" idiom the
`KioskControllerTests` / `F7F8_HardeningGateTests` use — read the partial as a
string and assert the end-date + the `/issues/new` link are present in the
guest block, and that the lapsed branch renders), the translation parity test,
and `dotnet build`. If a test fails, read the error, adjust, continue. Note:
`Kumunita.Web.Tests` is expected to fail **3** known-reds (the two SecGate
`DataClassCoverageTests` M2 gates + `ImproveHarnessTests.CodeCheck_Gate_Passes`)
— recorded drift (SEC-lane M2 + the ratchet), **not** regressions; don't "fix"
them (different unit's scope).

Then report: the two states you handled (live / lapsed) and how each is
detected (the role vs. the `Profile.IsGuest` + window read), the exact key(s)
you added and their en floor, the zone/format seam you used for `ValidUntil`,
the route the "message the admins" link targets, the test class name + the
number of tests, the commit hash, and which of the 3 known Web reds (if any)
your run reproduced. Do not commit unrelated work.
