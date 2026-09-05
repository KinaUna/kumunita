# M3b — rolling handoff note

> One section per unit, **appended** (never rewritten), exactly the
> M2 / M3 convention (`m3-handoff-notes.md`). Each unit writes exactly one
> short section before it exits; the next unit reads only that section +
> its own entry-read list.

## U1 — Design doc Part 1

- **Deliverable:** `docs/design/m3b-moderation.md` Part 1 — Context, Scope
  (in / out, mirroring M3's Part 1 shape), Invariants (the four new
  `C-M3b·1..4` + the ADR 0006 / 0001-B / 0003 / 0004 §B.1 / ADR 0006-E /
  ADR 0006-D clauses that **still hold**), FACES table (pinned count: 6).
- **Entry-reads confirmed:**
  - `docs/design/m3-posts-design.md` §§ `## Context` / `## Scope` /
    `## Invariants (pinned for M3)` / `## FACES (pinned, 10)` + `## M3 —
    Closed (recorded)` / `### M3b deferral list` (the six-item deferral
    list, **verbatim** source of truth for M3b's in-scope).
  - `docs/plans-milestones/m3-handoff-notes.md` § `## Summary` (the same
    six items, reconciled — U11's close).
  - `src/Kumunita.Core/Authorization/AccessAction.cs` — **confirmed**
    `AccessAction.Moderate` exists (id `"moderate"`); **no new id needed**
    for M3b's hide / remove / assign / resolve lanes (M1's existing
    `Read` / `Moderate` pair is sufficient; the plan Assumption is
    confirmed).
  - `src/Kumunita.Core/UserInfo/Component.cs` — **confirmed**
    `ModeratorAccess` is `bool` (off-default, ADR 0003 / C5 carve-out,
    the doc-comment names the M3 report-driven unlock — which **is**
    M3b's), and `SetComponentModeratorAccessAsync` is the sole
    GlobalAdmin-seam for the flag-flip (M3b's resolve lane (C-M3b·4, F6)
    calls it — U1 pins that in the invariant, U2 pins the exact
    signature).
- **Invariants U1 pinned:** `C-M3b·1` (report filing — resident-facing,
  no authz call, pinned filing-`Via` tag — **not** `Via=Report`, **not**
  `Via=Owner`), `C-M3b·2` (`Via=Report` read branch — the C5 carve-out
  live; the "filed report" is the gate, not the `Moderate` action
  alone), `C-M3b·3` (hide / remove lane — `Moderate`-gated,
  same-transaction, no partial write; `Post.Status` additive),
  `C-M3b·4` (assign — GlobalAdmin-gated, SoD; `/admin` unchanged). The
  ADR 0006 `C1..C6` + `ADR 0001-B` + `ADR 0003 §SoD` + ADR 0004 §B.1 +
  ADR 0006-D / E / all **still hold** (re-pinned in the design-doc §
  "ADRs M3b must keep holding" list; C-M3·1/2/3 from M3 **still bind**
  unchanged for the feed / detail surfaces M3b renders on).
- **FACES count: 6** (F1 filing, F2 `Via=Report` read, F3 hide,
  F4 remove, F5 assign, F6 unlock / resolve + the flag-flip
  via `SetComponentModeratorAccessAsync`).
- **Deviations:** none.
  - **Plan-documentation clarification** (not a drift): M3's U1 flagged a
    "12 invariants" plan headline vs. 11 in the body (M3's U2 confirmed
    11; 11 is authoritative). M3b's U1 does **not** flag a count slip —
    the M3b plan § U1 headline does not name a count, and the M3b Part 1
    pins **four** M3b-owned invariants (C-M3b·1..4) plus the re-pinned
    ADR clauses that still hold. U2 confirms 4 against the body (the
    body is authoritative) and pins the §2.5 test names accordingly,
    mirroring exactly the M3 U1 → U2 reconciliation.
- **What U2 must pin:** the four seam signatures (U2's Part 2
  §2.2–§2.4) — (1) the **four `ModerationService` method signatures**
  (`FileReportAsync` / `AssignReportAsync` / `UnlockAsync` /
  `ResolveReportAsync`); (2) the **`Via = Report` read-lane shape**
  (either a new method on `ModerationService` or a direct branch on
  `AuthorizationService.Decide` — U2's §2.4 pin decides which, verbatim
  C#); (3) **`PostService.HidePostAsync` / `RemovePostAsync`** (exact
  C#, the `IDocumentSession` overload (M1's §E lane), the
  `AccessAction.Moderate` call, the `PostStatus` enum literal set
  (`Active` / `Hidden` / `Removed`), and the **exact** audit-`Via` tag
  for the hide / remove audits, U2's §2.3 pin); (4) the **reply route
  `POST /posts/{id}/replies`** (the exact controller action — thin, M3's
  `PostsController` shape, delegating to the existing
  `PostService.CreateReplyAsync` (M3 U6) — **no new** Core seam, no new
  seam-test name; per M3's deferral list item 5); (5) the **filing
  `Via` tag** (the exact `AccessVia` literal the filing audit row
  carries; U2's §2.3 pin, Part 1's Part-1-level pin is only the
  *negative* pin: not `Via=Report`, not `Via=Owner`). U2's Part 2 also
  pins the §2.5 seam-test list (names, in
  `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` + additions to
  `tests/Kumunita.Core.Tests/PostServiceTests.cs`, per M3's Part 1
  convention), the §2.6 three-test acceptance gate (closed-loop /
  handoff / part-vs-whole), and the §2.7 drift-guard (this doc wins, per
  Part 1 § drift-guard, above).
- **Note on the e2e spec (M3b deferral item 6):** the Playwright
  scaffolding is present and enumerable (M3 U10 confirmed the gap); M2's
  D2 `kumunita` fixture is a documented *throw* (still open). The e2e
  spec is **in-scope** for M3b (M3 U10's deferral list item 6 names it
  M3b's) but is **not** a FACES row — the six FACES rows (F1–F6) are the
  *outcomes*; the e2e spec (authored / run by M3b's U10) is the
  *acceptance-gate evidence* that exercises those six rows. U10 authors
  + runs the spec **or** re-records the documented-throw status (does not
  silently re-defer without a note — M3b plan § U10 Exit + this note's
  `## Summary`). U1 pins the e2e spec as in-scope here so U10's scope is
  not re-derived from the M3 handoff note alone.
