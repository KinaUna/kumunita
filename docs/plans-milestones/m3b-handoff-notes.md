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



## U2 — Design doc Part 2

- **Deliverable:** `docs/design/m3b-moderation.md` Part 2 (appended) —
  §2.0 preambles + count reconciliation (4 invariants / 6 FACES; no
  C-M3b·5+), §2.1 frozen seam list (verbatim `IAuthorizationService`,
  `AccessVia`, `AccessAction`, `Decision`, `IAuditableResource`, the
  `Report` POCO shape (unchanged), `CreateReplyAsync` (M3 U6 — not a M3b
  seam); `IUserInfoService.SetComponentModeratorAccessAsync` cited as the
  one frozen seam the flag-flip writes), §2.2 new Core types
  (2.2.1 `PostStatus` enum + `Post.Status` ADD; 2.2.2 `PostService.
  HidePostAsync` / `RemovePostAsync`; 2.2.3 new `Kumunita.Core.
  Moderation.ModerationService` with the four write lanes + the
  `Via = Report` read lane `CanReadWithReportAsync`; 2.2.4 the
  `POST /posts/{id}/replies` Web-only action delegating to M3 U6),
  §2.3 report-filing rule (the four `Via` / `Status` / no-partial-write pins +
  the "three write-lane `Via` literal pins + the read-branch `Via` literal pin"
  breakdown — four numbered items: 1 filing `Via` tag, 2 four `Status` literals,
  3 hide/remove `Via` tag, 4 no-partial-write),
  branch shape pin + the `SetComponentModeratorAccessAsync` call pin +
  the C5-unactivated pin), §2.5 the **16** pinned seam-test names
  (8 `ModerationServiceTests` + 5 `PostServiceTests` + 1
  `PostsControllerTests` + 2 `ModerationControllerTests`), §2.6 the
  three-test acceptance gate (G1 closed-loop / G2 handoff / G3
  part-vs-whole — M2 D2 fixture-throw either fixed by U10 or
  re-recorded, not silently re-deferred), §2.7 the drift-guard
  (this file wins; ADR-amendment path for `Via` literal or enum
  literal re-pins; no mid-M3b renumbering / renaming).
- **Count reconciliation (U1 → U2):** U1 pinned **4** M3b-owned
  invariants (`C-M3b·1..4`) and **6** FACES rows (F1–F6); confirmed by
  U2; **16** seam-test names pinned in §2.5; **no** new C-M3b
  invariant, **no** new `AccessVia` literal, **no** new
  `AccessAction` literal introduced by Part 2 — the M1 frozen
  vocabulary is sufficient (the C-M3b·1/·2/·3 `Via` tags all use
  `AccessVia.Admin`; the C-M3b·2 read branch uses the **existing**
  `AccessVia.Report` literal M1 registered for the *read* branch).
- **Entry-reads confirmed:**
  - `src/Kumunita.Core/Posts/Post.cs` — **confirmed** the `Status`
    field is currently **absent** (the M3 comment names it
    "M3b deferral"); Part 2 §2.2.1 pins the `PostStatus` enum
    literal set (`Active` / `Hidden` / `Removed`) + the default
    `Active`, additive (ADR 0004 §B.1 — no migration, no re-seed).
  - `src/Kumunita.Core/Posts/PostService.cs` — **confirmed** the
    `IAuthorizationService` / `IUserInfoService` composition (U2 §2.2.2
    mirrors the existing `IDocumentSession` overload shape for the
    `HidePostAsync` / `RemovePostAsync` additions — **not** a new
    seam on `PostService`, only two method additions).
  - `src/Kumunita.Core/Posts/Report.cs` — **confirmed** the
    `Status` field is **nullable** (M3-registered) and the POCO has
    no `ComponentId` re-shape (the POCO is **unchanged** by M3b —
    rule 5); Part 2 §2.1 cites the POCO verbatim (unchanged), §2.3
    pins the four `Status` string literals (`"filed"` /
    `"assigned"` / `"unlocked"` / `"resolved"`).
  - `src/Kumunita.Core/Authorization/Decision.cs` — **confirmed**
    the `AccessVia` literal set (Owner / Audience / Delegation /
    Moderator / Report / BreakGlass / Admin); the three `Via` literals
    for M3b's write lanes (filing, hide, remove) are **pinned to
    `Admin`** (Part 2 §2.3 items 1 and 3); the `Via = Report` literal
    is **reserved** for the read branch (C-M3b·2; Part 2 §2.4 item 3).
  - `src/Kumunita.Core/Authorization/IAuthorizationService.cs` —
    **confirmed** the `CanAsync` / `CanSeeAsync` overload set
    (standalone + `IDocumentSession`); Part 2 §2.4 pins the read
    branch on the **standalone** `CanAsync` overload (own-commit
    audit row — the M3 `GetPostAsync` precedent) — not a
    `IDocumentSession` overload.
  - `docs/design/m3-posts-design.md` § `## Seams & contracts (Part 2)`
    — **confirmed** the M3 Part 2 shape is mirrored (2.0 preambles,
    count reconciliation; 2.1 frozen seam list; 2.2 new Core types;
    2.3–2.4 rule pins; 2.5 pinned seam-test names; 2.6 acceptance
    gate; 2.7 drift-guard); Part 2 of M3b uses the same 2.0–2.7
    numbering.
  - `docs/plans-milestones/m3-handoff-notes.md` § `## U2 — Design
    doc Part 2` (the M3 U2 handoff-note section) — **confirmed** the
    handoff-note shape U1's M3b U1 section already mirrors
    (`- **Deliverable:**` / `- **Entry-reads confirmed:**` /
    `- **Deviations:**` / `- **What U[n+1] must pin:**` bullets).
- **Invariants U2 re-pins (against U1's body, Part 1):**
  - `C-M3b·1` (filing lane — the `Via` tag **pinned** to
    `AccessVia.Admin` — Part 2 §2.3 item 1; the *two negatives*
    remain from Part 1: **not** `AccessVia.Report`, **not**
    `AccessVia.Owner`).
  - `C-M3b·2` (`Via = Report` read branch — the lane **pinned** to a
    new method `ModerationService.CanReadWithReportAsync(string
    postId, string actorId)` that returns
    `Task<Decision>`, calling the **standalone**
    `IAuthorizationService.CanAsync(string, AccessAction,
    IAuditableResource)` overload (own-commit audit row — Part 2
    §2.4 item 1); the `Via` literal on the audit row is **pinned**
    to `AccessVia.Report` (the existing M1 literal — Part 2 §2.4
    item 3)).
  - `C-M3b·3` (hide / remove lane — the `Via` tag **pinned** to
    `AccessVia.Admin` (Part 2 §2.3 item 3); the `PostStatus` enum
    literal set **pinned** to `Active` / `Hidden` / `Removed`
    (Part 2 §2.2.1 item 1); the **no-partial-write** rule is
    **pinned** (Part 2 §2.3 item 4 — C3, ADR 0006-C)).
  - `C-M3b·4` (assign lane — **GlobalAdmin-gated**, SoD; the
    flag-flip **pinned** to the existing
    `IUserInfoService.SetComponentModeratorAccessAsync(string
    componentId, bool on, string actorId)` call, **same-transaction**
    with the report's `Status` write (Part 2 §2.4 item 2 — C-M3b·4's
    "separate seam" pin; ADR 0006-C same-transaction)).
- **`Via` literal pins (Part 2 §2.3 items 1 and 3 + §2.4 item 3 — four pins):**
  - Filing (`FileReportAsync`) → **`AccessVia.Admin`** (not `Report`,
    not `Owner`).
  - Hide (`HidePostAsync`) → **`AccessVia.Admin`** (not `Moderator` —
    `Moderator` is reserved for the future C6 read-branch pin, not
    the write-lane pin).
  - Remove (`RemovePostAsync`) → **`AccessVia.Admin`** (same).
  - Read branch (`CanReadWithReportAsync`) → **`AccessVia.Report`**
    (the M1-frozen literal for the *read* branch; C-M3b·2).
- **`Report.Status` literal pins (Part 2 §2.3 item 2):**
  - `FileReportAsync` → `"filed"`
  - `AssignReportAsync` → `"assigned"`
  - `UnlockAsync` → `"unlocked"`
  - `ResolveReportAsync` → `"resolved"`
- **`PostStatus` enum literal pins (Part 2 §2.2.1):** `Active`
  (default) / `Hidden` / `Removed` — **exactly three** literals; the
  `Post.Status` property defaults to `PostStatus.Active` (so a
  `Status == null` check is not needed — no new nullable field added
  to the `Post` POCO).
- **Seam-test names (Part 2 §2.5):** **16** pinned tests in `tests/
  Kumunita.Core.Tests/ModerationServiceTests.cs` (8: 1-8), `tests/
  Kumunita.Core.Tests/PostServiceTests.cs` (5: 9-13, the M3b ADDs),
  `tests/Kumunita.Web.Tests/PostsControllerTests.cs` (1: 14, the
  reply-route shape/absence test), `tests/Kumunita.Web.Tests/
  ModerationControllerTests.cs` (2: 15-16, the queue + the
  resolve-UI action). No test whose name is not in this list may be
  introduced (unit-series rule 3). **U3 lands first on `Posts/Post.
  cs`** (the `PostStatus` enum + the `Post.Status` property ADD) and
  **then** on `Posts/PostService.cs` (`HidePostAsync` /
  `RemovePostAsync`); **U4** lands on `Moderation/
  ModerationService.cs` (the four write lanes + the read lane);
  **U8** lands on `Controllers/PostsController.cs` (the reply-route
  action); **U9** lands on the four `Tests` files above (the 16
  seam tests); **U10** authors + runs the e2e spec (the M2 D2
  fixture-throw either fixed or re-recorded — **not** silently
  re-deferred); **U11** closes M3b.
- **Acceptance gate (Part 2 §2.6):** the three-test gate (G1
  closed-loop / G2 handoff / G3 part-vs-whole) is recorded in `docs/
  design/m3b-moderation.md` § `## M3b — Closed (recorded)` (U11);
  the M2 D2 fixture-throw is either **fixed** by U10 (and the G1 /
  G2 / G3 pass counts are recorded) or **re-recorded** with a note
  (U10's handoff-note section) — **not** silently re-deferred. The
  three-test gate is the **acceptance evidence** that exercises the
  six FACES rows (F1–F6) + the two write lanes (hide / remove) in a
  single closed Playwright flow (G1), a handoff render (G2), and
  per-lane isolation (G3).
- **Drift-guard (Part 2 §2.7):** this design doc wins (M3b Part 1
  § drift-guard + this Part 2 §2.7). A unit who finds a
  **semantic** issue with the `Via` literal pins (e.g. `Admin` is
  *wrong* for filing, a new M1 `AccessVia` literal *is* needed)
  must: (1) file an ADR amendment adding the literal to
  `AccessVia`; (2) update Part 2 §2.3 item 1 **in the same commit**
  to the new literal; (3) append a drift note to this file
  (M3b handoff note). This is the **only** path — a unit may not
  locally re-pin to a different literal **without** an ADR
  amendment. The `PostStatus` enum literal set is **stable** for
  the rest of M3b (renaming / renumbering a literal is a breaking
  change — not allowed mid-M3b). The four `ModerationService` write
  lane signatures **and** the read-lane `CanReadWithReportAsync`
  signature in Part 2 §2.2.3 are **stable** (the unit-series rules
  1-5 all bind). No mid-M3b renumbering / renaming / re-pinning of
  the invariant ids (`C-M3b·1..4`), ADR clauses (C1/C2/C3/C4/C5/C6;
  ADR 0001-B; ADR 0003 §SoD; ADR 0004 §B.1), `Via` literals
  (items 1/3 of §2.3 + item 3 of §2.4), `Report.Status` literals
  (item 2 of §2.3), or `PostStatus` literals (item 1 of §2.2.1).
- **Deviations:** none.
  - **U1 → U2 reconciliation** (the M3 U1 → U2 "12 vs 11" slip
    analogue): U1's M3b Part 1 body pinned **four** M3b-owned
    invariants (`C-M3b·1..4`) and **six** FACES rows (F1–F6);
    U1's handoff (above) confirms four. U2 confirms **4** against
    the body (the body is authoritative) and pins the §2.5 test
    list accordingly (the 16 named tests), mirroring exactly the
    M3 U1 → U2 reconciliation. **No** new invariant (C-M3b·5+) is
    introduced by Part 2 (the plan's "four" count is confirmed
    against the body).
- **Exit:** no build (doc-only unit). Follow-up: **U3** touches
  `src/Kumunita.Core/Posts/Post.cs` **first** (the `PostStatus`
  enum + the `Post.Status` property ADD — additive, ADR 0004 §B.1,
  no migration, no re-seed), and then `src/Kumunita.Core/Posts/
  PostService.cs` (`HidePostAsync` / `RemovePostAsync` — both
  call `IAuthorizationService.CanAsync(..., AccessAction.Moderate,
  target, session)` before writing, same-transaction per
  C-M3b·3, audit `Via` tag = `AccessVia.Admin`). U3's handoff note:
  `## U3 — Post.Status + HidePostAsync / RemovePostAsync` (the two
  method signatures verbatim + the confirmation that no existing
  M3 seam-test name broke).

**Re-check (post-U2-exit, within U2's own unit):** the re-check
pass caught and corrected the following Part 2 corruptions
(documented here per §2.0 "this file wins" — the unit updates this
file in the same commit **and** records the correction here).
**No substantive pin was altered** — all changes are content-shape
and cross-reference fixes, not literal re-pins;

1. **§2.0 fabricated type name.** §2.0 cited `ReporterAssignment` as
   M1-frozen, but no such type exists in `src/Kumunita.Core` (no
   grep hit in `*.cs`). The real M1 POCO is `ModeratorAssignment`
   (confirmed at `src/Kumunita.Core/UserInfo/Component.cs:43`,
   registered in `M1DocTypes.cs:57`). Fixed to cite
   `ModeratorAssignment` (and added `AccessAudit`, which Part 2's
   doc-comments reference throughout) to the §2.0 bullet.
2. **§2.1 CreateReplyAsync signature wrong.** M3's actual U6 seam
   returns `Task<PostReply>` and names the body parameter `body`
   (confirmed at `src/Kumunita.Core/Posts/PostService.cs:180`). The
   original §2.1 cited `Task` + `string replyBody`. Fixed to
   match the real signature — **this is a substantive fix**, since
   a unit implementing against the original §2.1 would have failed
   the §2.7 drift-guard at U3 build time.
3. **§2.2.4 (the `POST /posts/{id}/replies` action shape).** The
   original §2.2.4 pinned `[HttpPost]` (no explicit route string)
   + local `[Authorize]` + `User.Identity.Name` for the actor + no
   `LightweightSession` / `SaveChangesAsync` write-lane shape — none
   of which matched the M3 PostsController shape (which uses
   **explicit** route strings like
   `[HttpGet("/community/{componentId}")]` and inherits `[Authorize]`
   from the class per M3's U7 write-lane precedent,
   `src/Kumunita.Web/Controllers/PostsController.cs`). Fixed to
   `[HttpPost("/posts/{id}/replies")]`, dropped the redundant
   action-level `[Authorize]`, switched to `SubjectId(User)` (the
   M3 helper), and showed the C3 same-transaction `LightweightSession`
   / `SaveChangesAsync` write-lane shape matching the M3
   `POST /posts/new` write-lane precedent (so U8 does not invent a
   different session shape). Also fixed the in-line test reference
   from "test-16" to "test-14" (the correct §2.5 index).
4. **§2.2.3 `Report.Status` literal cites.** The four
   `ModerationService` method doc-comments cited "§2.3 pin,
   item 3" for the four `Report.Status` literals (`"filed"` /
   `"assigned"` / `"unlocked"` / `"resolved"`). Those are actually
   **item 2** of §2.3 (the four-`Status`-literal pin), not item 3
   (the hide/remove `Via`-tag pin). Fixed all four cites to
   "item 2".
5. **§2.5 test-3 anchor.** The test-3 row anchored
   `CanReadWithReportAsync_ModeratorWithReport_Allowed_ViaTagIsReport`
   to "the `Admin` literal on the read branch". The read branch
   uses `AccessVia.Report` (the M1-frozen *read-branch* literal,
   §2.4 item 3), not `Admin`. The test name already said
   `ViaTagIsReport`; only the anchor text was wrong. Fixed
   to "the `Report` literal on the audit row (§2.4 item 3)".
6. **§2.5 test-13 anchor.** The test-13 row cited "2.2.1 item 1"
   — there is no "item 1" inside §2.2.1 (it is a single unnumbered
   subsection — the whole §2.2.1 is the `PostStatus` +
   `Post.Status` ADD). Fixed to "§2.2.1".
7. **§2.7 test-name count.** §2.7 body said "six test names" when
   §2.5 pins **16**. Fixed to 16 (and clarified that the
   "stable pins" set is the four `Via` literal pins + the four
   `Report.Status` string literals + the three `PostStatus` enum
   literals + the sixteen test names — not just a subset).
8. **§2.7 PostStatus cite + additive-vs.-breaking disambiguation.**
   §2.7 cited the `PostStatus` literal set as "item 1 of §2.3"
   (the actual pin is §2.2.1 — the `PostStatus` ADD). Disambiguated
   "ADD a new `PostStatus` literal = ADR 0004 §B.1 additive (not
   breaking in the schema sense)" from "rename an existing
   `PostStatus` literal = breaking change" — the original §2.7
   conflated these. Fixed.
9. **Handoff-note cross-references.** Fixed two `§2.2.1 item 1`
   references → `§2.2.1`; fixed "three `Via` literal pins" → "four
   `Via` literal pins"; fixed "three `Via` / `Status` literal pins"
   in the Deliverable summary → "four numbered `Via` / `Status` /
   no-partial-write pins"; fixed §2.3 opener "three literal/
   rule pins" → "four numbered pins"; removed a fabricated M3
   test name (`CreateReplyAsync_WritesReply_AuditsActorViaOwner_InheritsParentStatus`
   does **not** exist in M3's 18-test list per
   `docs/design/m3-posts-design.md` §2.5) and replaced with a
   citation to M3's Part 2 §2.2 (where the `CreateReplyAsync`
   signature pin actually lives) + the C-M3·1 reply-inherits rule
   as the invariant anchor.

**Status:** all the above are content-and-reference corrections
(plus one substantive signature fix in §2.1 — item 2 above), not
literal re-pins. The substantive pins (the four `Via` literal
pins, the four `Report.Status` string literals, the three
`PostStatus` enum literals, the sixteen test names, the
`ModerationService` method signatures, the
`SetComponentModeratorAccessAsync` flag-flip seam, the "branch
on `AuthorizationService.Decide` not-taken" pin) are **unchanged**
and remain **stable for the rest of M3b** (per §2.7). This
re-check does **not** open a §2.7 drift-guard path: **no** ADR
amendment is required for any of the corrections above.
