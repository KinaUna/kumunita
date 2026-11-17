---
id: r19-prompt
title: "Prompt: IMPROVE-UX R19 — SEC review of the ADR 0302 shared kiosk link (R15–R18)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0302-shared-kiosk-link.md
  - docs/adr/0298-data-classes-in-code.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent doing IMPROVE-UX R19,
> the **SEC review** of the ADR 0302 shared kiosk link (R15–R18).
> **Who is it for:** the next coding agent (fresh context). **When to read
> the rest:** when starting R19 (the review that ADR 0302 D6's three
> `// TODO ❓` open questions defer to, and that owns the three red
> `SecGate` + one red `ImproveHarness` tests the lane left behind).

# Fresh-context prompt: IMPROVE-UX R19

You are continuing the Kumunita repository on `development` (HEAD
`8531cd6d`). R15, R17 and R18 are already landed — the ADR 0302 shared kiosk
link is **built**, not built-by-you. Read the plan card (in
`plan-improve-ux.md`, the R19 row) verbatim:

> R19 — **SEC review** of R15–R18 (independent agent or the SEC lane).
> Done when: **review note in the handoff; findings fixed or filed.**

This is a **review, not an implementation**: the R15–R18 code is the subject
under review. You do not ship a new feature. You audit it, decide the open
questions, and either fix a finding minimally or file it with an owner — then
record the review in the handoff.

## The review subject (and one honesty caveat)

- **R15** (`8de9751a`) — ADR [0302](../../../adr/0302-shared-kiosk-link.md) + the
  `[DataClass(Secret, Delete, Never)]` `KioskLink` doc + the
  `IKioskLinkService` / `KioskLinkService` (create / revoke / validate;
  `kskl_` + 32 CSPRNG bytes base64url, SHA-256 hex at rest, one
  `kiosk.link.create` / one `kiosk.link.revoke` audit row, **zero** on
  validate) + the `CoreDocTypes` registration + 8 Core tests.
- **R16 — no commit exists.** The plan's R16 (the token→restricted-principal
  auth on `/kiosk` + slides, the rate-limit) **never landed as a unit**.
  `Roles.KioskLink` (`"kiosk-link"`, `src/Kumunita.Core/Identity/ThinPrincipal.cs:62`)
  is *defined* and *read* by R17's filter, but **no code ever assigns it** —
  there is no `/kiosk?link=<token>` handler, no middleware, no
  `IKioskLinkService.ValidateAsync` caller anywhere in `src/Kumunita.Web`.
  A kiosk-link **currently authenticates nothing**: the token a resident
  creates in R18 cannot be presented anywhere. State this plainly in the
  review; do not paper over it.
- **R17** (`0a15fd02`) — the community-visible filter in
  `KioskItemCatalog.ResolveAsync` (audience-bearing types denied when not
  community-visible, group-private + community-targeted Announcements +
  drafts denied) + 14 `KioskLinkCommunityFilterTests`.
- **R18** (`8531cd6d`) — the `/settings/kiosk` surface: `KioskLinkListReader`
  (owner-scoped, active-only, no token material) + the
  `KioskController.LinkCreate` / `LinkRevoke` + `SettingsGet` list &
  copy-once token, the view section, the `kiosk-settings.ts` copy
  affordance, 14 localized keys × en/de/fr/da, `KioskLinkSettingsTests`
  (10), `InMemoryTempDataProvider` retention, `ux-baseline.json`
  `bare_status` re-base.

## The review foci

Work top-down; the red tests and the R16 gap are the load-bearing findings.

1. **The R16 gap (headline).** The link is created, listed and revoked, but
   **presented nowhere**. Decide and record: is this an *accepted held
   surface* (file a scoped R16 follow-up with an owner + a note that the
   create/list/revoke surface is **not shippable to a resident** until the
   auth lands), or does R19 narrow R18's scope? A resident who can mint a
   token that works nowhere is a UX-and-SEC smell — say so.
2. **The three red `SecGate` tests** (pre-existing at HEAD; the R19 SEC
   review owns the fix-or-file). `KioskLink` is
   `[DataClass(DataClassKind.Secret, Erasure.Delete, ExportScope.Never)]`
   (`src/Kumunita.Core/Kiosk/KioskLink.cs:30`), yet:
   - `DataClassCoverageTests.EveryDeleteOrPseudonymizeType_IsHandledByAnEraser`
     — a `Delete`-erasure type not routed to an account eraser.
   - `DataClassCoverageTests.EveryResidentExportType_IsInUserScopeInventory`
     — the type not in the export-scope inventory.
   - `DecisionFreeReadGateTests` "Every SEC-READ marker names a reason" —
     a `// SEC-READ:` on the kiosk-link read path without the required
     reason.
   Fix the wiring (an account eraser owns `KioskLink` — ADR 0302 Consequences
   says "the owner account's eraser owns it — the R19 SEC review confirms the
   seam") or, if the attribute set is wrong, correct it and re-pin. This is
   the finding the ADR most explicitly deferred to R19.
3. **The red code-check ratchet** (pre-existing at HEAD; `ImproveHarnessTests
   .CodeCheck_Gate_Passes` is red). Run `pwsh tools/code/code-check.ps1` and
   read the 10 violations (boundary edges: `Web->Kiosk:KioskLink` (4 files),
   `Kiosk->Identity:IdentityService`, `Identity->Kiosk:KioskLink`,
   `Kiosk->Authorization:AccessAudit`, plus the grown `Web->Identity:*` /
   `Web->ErrorReports` edges and `ModerationService.ListMyReportsAsync` CC=17).
   Decide per violation: is the `Kiosk`-context edge a real boundary breach
   (ADR 0006) that should be re-homed, or is the baseline simply stale and
   must be re-based with a stated reason (the ratchet doctrine: raising a
   number needs an `adr:` reason; `--tighten` regenerates)? Record the
   decision. Note `IKioskLinkService` is now *named* by `KioskLinkSettingsTests`
   (3 hits), so that seam-test pin is satisfied by R18.
4. **Token handling (ADR 0302 D1/D2).** Confirm the raw token appears only in
   the `CreateAsync` return + the one `TempData` round-trip; at rest it is the
   SHA-256 hex digest (`KioskLink.TokenHash`); it is **never** in the URL, an
   audit row, or a log template. The `TempData` store is transient — confirm
   that is acceptable for a copy-once credential, or file it.
5. **Owner-scope pins.** Create (actor = owner), validate (hash + owner
   match), revoke (`RevokeAsync` no-ops on mismatch). R18's `LinkRevoke`
   returns `200 { revoked:false }` on a mismatched/unknown link — decide
   whether a **404 / 403** is the more correct signal (it currently leaks
   nothing but is not a status-grade refusal). Record the call.
6. **Audit rows.** Exactly one `kiosk.link.create` / one `kiosk.link.revoke`
   (`Via: Owner`, `TargetKind "kiosk"`, PII-free); **zero** on
   `ValidateAsync`; `LastUsedAt` stamped read-then-write, no audit (the ESC·9
   precedent). Pin each.
7. **Revoke terminality + idempotency.** Revoke is terminal (no undo); a
   second revoke is a no-op returning false, no second audit row.
8. **The Web read seam.** `KioskLinkListReader` — owner-scoped,
   `RevokedAt == null` only, `OrderByDescending(CreatedAt)`, **no** token
   material returned, blank owner → `[]`, and C09 (controller off Marten —
   the read lives in a Web service). Confirm.
9. **The open `// TODO ❓` questions** — decide and record (a one-line
   constant, not an ADR edit, unless the decision changes the *mechanism*):
   - **ADR 0302 D6** — (a) token length (D2 proposes 256 bits); (b) an
     **expiry** (none today; a link lives until revoked); (c) **max links per
     playlist** (no ceiling today).
   - **R18's** four: the revoke-mismatch status (focus 5); "revoke all" was
     skipped (intentional?); the max-links ceiling (D6c); token-in-`TempData`
     (focus 4).
   - **R17's** — `KioskItemCatalog.cs:268`: the `Community` flag is evaluated
     without the resource's own `ComponentId` scope (ADR 0036 / 0102 refine it
     into a community-scoped shape). R17 treats `Community=true` as visible
     across all component scopes — decide whether the SEC reading should
     narrow to the resource's own `ComponentId` (stricter), and if so file it
     with an owner (it is a code change, not a review-note-only fix).

## Read first

`AGENTS.md`; ADR [0302](../../../adr/0302-shared-kiosk-link.md) (D1–D6 +
Consequences); the **R15 handoff section** in
[improve-ux-handoff-notes.md](improve-ux-handoff-notes.md); the R15–R18 diffs
(`git show 8de9751a 0a15fd02 8531cd6d`); `KioskLink.cs`,
`IKioskLinkService.cs` / `KioskLinkService.cs`,
`src/Kumunita.Web/Services/KioskLinkListReader.cs`,
`src/Kumunita.Web/Controllers/KioskController.cs` (link actions),
`KioskItemCatalog.cs` (the filter + the `// TODO ❓`); the red tests
(`tests/Kumunita.Web.Tests/SecGate/DataClassCoverageTests.cs`,
`.../DecisionFreeReadGateTests.cs`,
`tests/Kumunita.Web.Tests/KioskControllerTests.cs` → `KioskLinkSettingsTests`,
`ImproveHarnessTests.CodeCheck_Gate_Passes`); `tools/code/code-check.ps1`.

## The deliverable

A **review note in the handoff**, per the R14 precedent (commit `03369092`,
`docs(ux): R14 — SEC review of the ADR 0301 grace period (IMPROVE-UX R14)`):

- Append the R19 review note to
  [improve-ux-handoff-notes.md](improve-ux-handoff-notes.md) — and **also add
  the missing R17 and R18 sections** (the lane log jumps R15 → R14; R17's
  community filter and R18's settings surface have no entry yet).
- **Every finding fixed or filed** with an owner: red `SecGate` + red
  code-check findings fixed minimally (or an ADR-reasoned re-base); the R16
  auth gap and the R17 `ComponentId` question filed as held follow-ups.
- One commit, `docs(ux):` style (code fixes ride in the same commit when they
  are the review's fix, matching the house one-commit-per-unit rule).

## Scope rules

- Review only — no new kiosk feature, no ADR renumbering, no re-scope of the
  other lanes. A finding that needs a *mechanism* change (e.g. adding an
  expiry field, or the `ComponentId` guard) is **filed with an owner**, not
  built here, unless it is a one-line constant the ADR already anticipated.
- Keep the ratchet honest: any `code-baseline.json` change carries a stated
  reason; any `ux-baseline.json` change is measured against HEAD first.
- When done, report: each finding → **fixed** / **filed (owner + why)**, the
  three red `SecGate` + the red `ImproveHarness` gate state before/after, and
  the commit hash. Leave any genuinely open decision as `// TODO ❓`.
