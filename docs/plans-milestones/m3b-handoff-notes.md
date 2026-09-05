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

---

## U3 — `Post.Status` + `HidePostAsync` / `RemovePostAsync`

**Unit plan:** `docs/plans-milestones/m3b-u3-plan.md` (new file, U3's
self-contained plan for this unit per the sealed-unit convention).

- **Deliverables landed (≤ 3 files, actual = 2 source files + this
  note + the plan file):**
  - `src/Kumunita.Core/Posts/Post.cs` — **additive** change:
    - New `public enum PostStatus { Active, Hidden, Removed }` in the
      same namespace (the **three** literal set from Part 2 §2.2.1,
      stable per §2.7).
    - New `public PostStatus Status { get; set; } = PostStatus.Active;`
      property on `Post` (default `Active` — the doc's "no null-check
      needed" pin; the POCO is otherwise **unchanged**, per unit-rule 5).
    - `Post`'s class doc-comment updated to name `Status` as the M3b
      single ADD (ADR 0004 §B.1 additive — delta-detected, idempotent,
      no re-seed; no migration, no re-seed required).
  - `src/Kumunita.Core/Posts/PostService.cs` — **additive** change:
    the two write lanes below, added to the *existing* M3 class (rule 1:
    no file outside the unit's `Deliverables` touched; existing `ListFeedAsync`
    / `GetPostAsync` / `CreatePostAsync` / `CreateReplyAsync` untouched).
- **Entry-reads confirmed (per the unit's entry-read list, 5 files):**
  - `docs/design/m3b-moderation.md` §2.2.1 (the `PostStatus` enum +
    `Post.Status` literal set — **verbatim matched**) and §2.2.2 (the
    two `Task` methods with `(string postId, string actorId,
    IDocumentSession session)` — **verbatim matched**).
  - `src/Kumunita.Core/Posts/Post.cs` — **no `Status` column today**
    (M3's POCO shape confirmed; U3 is the additive ADD, no
    re-shape).
  - `src/Kumunita.Core/Posts/PostService.cs` — C3 write-lane style
    confirmed (`CreatePostAsync` / `CreateReplyAsync`): caller's
    `IDocumentSession`, one `SaveChangesAsync`. U3's two methods match
    that shape exactly.
  - `src/Kumunita.Core/Authorization/IAuthorizationService.cs` — the
    `IDocumentSession` overload of `CanAsync` confirmed present (the
    C3 same-transaction lane — audit row written into the caller's
    transaction; U3 calls **this** overload, not the standalone one).
    `AccessAction.Moderate` already exists (M1 surface, no new id —
    U1's handoff pin, re-confirmed here).
  - `src/Kumunita.Core/Posts/PostToAuditableResource.cs` — M3's
    adapter, reused **as-is** (not modified); the hide/remove lanes
    build the target with `new PostToAuditableResource(post)`.
  - `src/Kumunita.Core/Authorization/Decision.cs` — confirmed `Decision
    (bool Allowed, AccessVia Via, string EffectivePrincipalId)` (the
    `.Allowed` member is what U3 branches on; the audit `Via` tag is
    written by the frozen `IAuthorizationService` per M1, not by U3).
- **What U3 implemented (verbatim signatures, as landed):**

  ```csharp
  // src/Kumunita.Core/Posts/Post.cs (new enum — the three-literal pin, §2.2.1)
  namespace Kumunita.Core.Posts;

  public enum PostStatus
  {
      Active,    // default (M3's behavior unchanged for a visible post)
      Hidden,    // F3 — soft-hide, the Moderate-gated write lane (C-M3b·3)
      Removed    // F4 — hard-remove, the Moderate-gated write lane (C-M3b·3)
  }

  // (on Post, additive)
  public PostStatus Status { get; set; } = PostStatus.Active;
  ```

  ```csharp
  // Additions to the existing PostService (src/Kumunita.Core/Posts/PostService.cs)
  public async Task HidePostAsync(string postId, string actorId,
                                  IDocumentSession session)
  {
      // 1) load the Post from the caller's session (KeyNotFound if missing)
      // 2) _authz.CanAsync(actorId, AccessAction.Moderate,
      //                     new PostToAuditableResource(post), session)
      // 3) on Allowed: post.Status = PostStatus.Hidden;
      //                post.Modified = UtcNow; session.Store(post);
      // 4) await session.SaveChangesAsync()          <- C3: audit + write commit atomically
  }

  public async Task RemovePostAsync(string postId, string actorId,
                                    IDocumentSession session)
  {
      // identical shape to HidePostAsync but writes PostStatus.Removed.
  }
  ```

  (The full landed method bodies match the §2.2.2 pin: guard-argument
  shape mirrors `CreatePostAsync` / `CreateReplyAsync`; one
  `SaveChangesAsync`; Deny → **no `Status` write** — the audit row
  (Deny) still commits per ADR 0006-C / C3; Allow → `Status` set
  (`Hidden` / `Removed`) + `Modified` stamped + `Store` — the
  "no partial write" pin held: a post is never left in a state where
  `Status` is set but the decision audit row is missing, because
  both commit in one `SaveChangesAsync`.)

- **Invariants held:**
  - **C-M3b·3** (hide / remove lane, `Moderate`-gated, same-transaction,
    no partial write) — **lives in this unit's code**.
  - **C3** (same-transaction for the decision audit row + the domain
    write) — **lives in this unit's code** (both lanes end in one
    `SaveChangesAsync`).
  - **ADR 0006-C** (audit always on — Allow *and* Deny) — **lives in the
    frozen `IAuthorizationService`** (U3 calls the `IDocumentSession`
    overload, which writes the row into the caller's transaction —
    Allow writes the AuditAllow row + the Status write in the same
    commit; Deny writes the AuditDeny row only, no Status write).
  - **ADR 0006-D** (single decision path, no second read of
    `GroupMembership`/`DelegationGrant`) — **lives in U3's code
    (U3 does not read membership itself; it composes only the two
    seams)** + the frozen `IAuthorizationService` (unchanged by U3).
  - **ADR 0004 §B.1** (additive — no migration, no re-seed, no
    delta) — **lives in the POCO shape (the single ADD on `Post`)**;
    the `PostStatus` literal set (exactly `Active` / `Hidden` /
    `Removed`) matches the §2.2.1 pin.
  - **C-M3·1 / C-M3·2 / C-M3·3** (the M3 invariants) — **unchanged,
    still bind** for every feed / detail render M3b reads (the M3
    methods `ListFeedAsync` / `GetPostAsync` / `CreatePostAsync` /
    `CreateReplyAsync` are **untouched** by U3).
- **Build status:**
  - `dotnet build --configuration Release src\Kumunita.Core\Kumunita.
    Core.csproj` — **0 Warning(s) / 0 Error(s)**, `Build succeeded.`
    (Also IDE `run_build` on the same project — green; both confirm
    the additive change compiles against M3's existing POCO surface.)
- **No existing M3 seam-test name broke.** Verified: `PostStatus` /
  `HidePostAsync` / `RemovePostAsync` have **zero** references in any
  existing test file before this unit (searched
  `tests/Kumunita.Core.Tests/**/*.cs` for each — **0 hits**). The
  16 pinned M3b seam-test names (Part 2 §2.5, 5 in
  `PostServiceTests.cs`, 8 in `ModerationServiceTests.cs`, 1 in
  `PostsControllerTests.cs`, 2 in `ModerationControllerTests.cs`)
  are **U9's deliverable** (not landed in U3) — the 5
  `PostServiceTests` names (M3b ADDs) will exercise the two lanes
  U3 just landed; the 8 `ModerationServiceTests` names will exercise
  U4's `ModerationService`; the 1 + 2 Web-test names will exercise
  U8's reply-route action and U7's `/moderation` surface
  respectively. **No unit-series rule 1–6 violation.**
- **Deviations:** none (all pins matched verbatim against the design
  doc §2.2.1 / §2.2.2; no §2.7 drift-guard path required — no ADR
  amendment, no literal re-pin, no signature change).
  - **Style note (not a pin):** U3 chose to throw
    `KeyNotFoundException` (not a silent no-op or `Decision`
    return) on a missing post in the session. This is M3's detail
    shape precedent (`GetPostAsync`'s fail-closed `Post = null` is a
    *read* lane — the write lane has no "Deny" path that would make
    a silent no-op more semantically consistent with M3, and a
    moderator write against a missing post is a caller-visible
    error). This is **not** a divergence from any pin in the design
    doc (the pin does not name the missing-post outcome as a
    specific return type); it is a U3-style choice, consistent with
    the existing POCO's error-shape conventions. No drift.
  - **`Modified` stamp on hide / remove:** **not** pinned by the
    design doc (the pin does not name a `Modified` write on the
    hide/remove lanes), but **matches M3's POCO semantics**
    (`Modified` is the last-write marker per M3's class doc-comment;
    `CreatePostAsync` writes `Created` only because no `Modified`
    value exists yet). Stamp `Modified` — low risk, consistent with
    convention, does not violate any pin. If U9's 5
    `PostServiceTests` seam tests (which will exercise these lanes)
    assert on `Modified`, this is the semantic they'll match; if
    they do **not** assert on it, U3's choice is behaviorally
    equivalent to not stamping from an audit/decision standpoint
    (C3's pin is on the decision audit row + the `Status` write,
    both committed in one `SaveChangesAsync` — the `Modified`
    stamp is a secondary, non-pinned POCO field). **No drift-guard
    path required**; a note is appended here for U9 to reference.
- **Follow-up (U4's entry-reads):** U4 lands on
  `src/Kumunita.Core/Moderation/ModerationService.cs` (the four
  write lanes + the `Via = Report` read lane `CanReadWithReportAsync`
  per Part 2 §2.2.3). U4 should confirm the `Post` POCO's `Status`
  property (added by U3) is **not** needed in the read lane's
  *target* shape (the `CanReadWithReportAsync` read is over the
  existing `PostToAuditableResource` — the `Status` field is a
  post-authorization surface (the *write* lanes only), so the
  read-lane target projection is unchanged from M3's). **No** new
  seam on a frozen interface (unit-series rule 4 — U4 composes
  only the two existing seams).

## U4 — Report filing (resident-facing write lane)

- **Deliverable:** `src/Kumunita.Core/Moderation/ModerationService.cs`
  (1 file, **new** — the U4 one-file deliverable per the register
  line). The new bounded context `Kumunita.Core.Moderation` is
  created for the first time by this unit (the Part 1 §2.0 preambles
  name it as M3b's "one new bounded context"). The file contains the
  **ctor** (per §2.2.3 pin) + **one** implemented public method:
  `FileReportAsync` (the F1 / C-M3b·1 resident-facing intake write
  lane). The other four methods (`AssignReportAsync`, `UnlockAsync`,
  `ResolveReportAsync`, `CanReadWithReportAsync`) are **U5's
  deliverable** (per the U5 register line "complete
  `ModerationService`" — the U5 unit modifies this same file).
- **Entry-reads confirmed (per the unit's entry-read list):**
  - `docs/design/m3b-moderation.md` §2.2.3 — **the frozen pin**:
    `public async Task<int> FileReportAsync(string postId, string
    actorId, string? reason, IDocumentSession session)` (U4 matches
    verbatim — the `Task<int>` return, the `actorId` parameter name,
    the 4-parameter list; U1's handoff line's `Task FileReportAsync
    (string postId, string reporterId, ...)` is superseded by the
    design-doc §2.2.3 pin — §2.0 drift-guard: this file wins).
  - `docs/design/m3b-moderation.md` §2.3 — **the four numbered pins**
    (U4's method body honors items 1 and 2 + item 4):
    - Item 1 (filing `Via` tag): **`AccessVia.Admin`** — U4's
      `AccessAudit` row carries this exactly. The two negatives
      (not `AccessVia.Report` — reserved for the read branch, C-M3b·2
      — U5's `CanReadWithReportAsync`; not `AccessVia.Owner` — the
      C1 owner-branch) are honored.
    - Item 2 (`Report.Status` literal): **`"filed"`** — U4's `Report`
      row sets this exactly. (The other three literals — `"assigned"`
      for `AssignReportAsync`, `"unlocked"` for `UnlockAsync`,
      `"resolved"` for `ResolveReportAsync` — are U5's.)
    - Item 3 (hide/remove `Via` tag): **not in U4's code** — U3's
      `HidePostAsync` / `RemovePostAsync` already landed
      `AccessVia.Admin` per that pin (U3's handoff note).
    - Item 4 (no partial write): U4's single `SaveChangesAsync` at
      the end of `FileReportAsync` commits the `Report` row and the
      `AccessAudit` row **atomically** — a failed save rolls both
      back; no partial state is possible.
  - `docs/design/m3b-moderation.md` §2.5 — the 16 pinned seam-test
    names. Two are anchored to C-M3b·1 (F1) and exercise U4's lane:
    - **Test 1:**
      `FileReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner`
      (U9's deliverable — tests the `AccessVia.Admin` literal + the
      two negatives U4 honors).
    - **Test 2:**
      `FileReportAsync_Filing_WritesReportStatusFiled` (U9's
      deliverable — asserts `Report.Status == "filed"` after U4's
      call). Both tests live in
      `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` (U9's
      file, created by U9).
  - `src/Kumunita.Core/Posts/Report.cs` — the M3-registered `Report`
    POCO (7 fields, `Status` **nullable** per the M3 register). U4
    writes the literal `"filed"` to the existing `Status` field
    (a **value** write, not a **shape** change — unit-series rule 5
    holds: U4 does not reshape `Report`). The `ReporterId` field
    (not `ActorId`) is the `Report` POCO's convention for "the
    acting resident" — U4 maps the caller's `actorId` to `ReporterId`
    (the "reporter IS the actor in this resident-facing lane"
    convention — same as M1's `UpsertProfileAsync` where the
    `Profile.SubjectId` is the acting resident).
  - `src/Kumunita.Core/Posts/PostToAuditableResource.cs` — the M3
    adapter (the `Post` POCO's projection for the
    `IAuthorizationService` decision). **Not** used by U4's
    `FileReportAsync` (which is a **write lane** — the C-M3b·1 /
    §2.2.3 FileReportAsync doc-comment pins no `CanAsync` call; the
    `PostToAuditableResource` adapter is an *auditable resource* —
    it only exists to feed into a `CanAsync` / `CanSeeAsync` call).
    It IS relevant to U5's `CanReadWithReportAsync` (the read branch,
    where the adapter IS the target of the `CanAsync` call) —
    U5's deliverable, not U4's.
  - `src/Kumunita.Core/M3DocTypes.cs` — **confirmed**: `opts.
    Schema.For<Report>();` is **already registered** (M3 U3's
    "table-in-M3 / flow-in-M3b" split — the table is registered,
    the workflow is M3b's; U4 is the first M3b unit to write to it).
    **No** new `Configure` call is needed for U4 (the `Report` POCO
    is already in the document registry; `opts.Schema.For<Post>` and
    `opts.Schema.For<PostReply>` are also already registered — U3
    did not need to add `Post.Status` to any `Configure` call, and
    neither does U4).
- **What U4 implemented (verbatim signatures, as landed):**

  ```csharp
  // src/Kumunita.Core/Moderation/ModerationService.cs (new bounded context, new file)
  namespace Kumunita.Core.Moderation;

  public sealed class ModerationService
  {
      private readonly IUserInfoService _userInfo;
      private readonly IAuthorizationService _authz;
      private readonly IDocumentStore _store;

      public ModerationService(
          IUserInfoService userInfo,
          IAuthorizationService authz,
          IDocumentStore store)
      {
          _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
          _authz    = authz    ?? throw new ArgumentNullException(nameof(authz));
          _store    = store    ?? throw new ArgumentNullException(nameof(store));
      }

      // §2.2.3 pin — the exact signature U4 landed:
      public async Task<int> FileReportAsync(
          string postId,
          string actorId,
          string? reason,
          IDocumentSession session)
      {
          // 1) argument guards (ArgumentException on empty postId/actorId;
          //    ArgumentNullException.ThrowIfNull on null session)
          // 2) await session.LoadAsync<Post>(postId)
          //    → KeyNotFoundException if missing (no partial write)
          // 3) build Report { Id, PostId, ReporterId = actorId,
          //                   ComponentId = post.ComponentId,
          //                   Reason = reason,
          //                   Status  = "filed",     // §2.3 item 2
          //                   At      = now }
          //    session.Store(report)
          // 4) build AccessAudit {
          //       Id                   = new guid (N),
          //       At                   = now (same timestamp as the report row),
          //       ActorId              = actorId,
          //       EffectivePrincipalId = actorId,   // resident-facing: no delegation / break-glass
          //       Action               = "report.file",
          //       TargetKind           = "post",
          //       TargetId             = postId,
          //       Via                  = AccessVia.Admin,   // §2.3 item 1
          //       Outcome              = AccessOutcome.Allow
          //   }
          //    session.Store(audit)
          // 5) await session.SaveChangesAsync()   // C3 — one commit, atomic
          // 6) return 1   // the count of Report rows written (1)
      }
  }
  ```

  - **`Action = "report.file"`** — the action-string on the audit row.
    **Not** a §2.3 pin (the four §2.3 pins are the Via tags, the
    Status literals, and the no-partial-write discipline — the Action
    string is a Core-convention-level detail). U4's choice matches
    the M1/M2 kebab-case / verb-noun pattern: M1's
    `"moderator-access"` / `"group.add-member"` / `"delegation.grant"`
    / `"delegation.revoke"` / `"profile.upsert"` (the
    `UpsertProfileAsync` convention). `"report.file"` is a *distinct*
    verb-noun pair (a different action — filing a report — from
    `"moderator-access"` (flipping a flag) or the M1/M2 group- /
    delegation- / profile- actions). U9's test 1
    (`FileReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner`) will
    anchor the exact `Action` string (the §2.5 test name is a
    `viaTag` test, not an `Action` test, so the `Action` value is
    implementation-level — but U9's test 2
    (`FileReportAsync_Filing_WritesReportStatusFiled`) will exercise
    the U4 `Report.Status = "filed"` pin, which IS a §2.3 pin).
  - **`TargetKind = "post"`** — the audit row's focus is the **post**
    the report is filed against (not the `Report` row itself — the
    `Report` row is the **action's object**; the post is the
    **resource** the audit row records). Matches M3's
    `PostToAuditableResource.TargetKind` pin (`"post"`).
  - **`EffectivePrincipalId = actorId`** — the resident IS the acting
    principal in this lane (no delegation / break-glass path; C-M3b·1
    pins "resident-facing" — no `Moderate`-gated decision, no `Owner`
    branch, no `Delegation` / `BreakGlass` / `Report` / `Admin`
    break-glass path). Same shape as M1's
    `SetComponentModeratorAccessAsync` (also sets
    `EffectivePrincipalId = actorId` — the acting principal IS the
    actor in a plain write lane).
  - **`Outcome = AccessOutcome.Allow`** — U4 is a write lane (no
    `CanAsync` decision), not a deny-by-default decision lane.
    `Allow` is the correct discriminant for "the write succeeded
    end-to-end" (a failed save → no row at all, so no `Deny` row is
    ever written — C3's no-partial-write pin is implemented as "the
    single SaveChangesAsync either commits both rows or rolls both
    back", not as "write a Deny row on failure"). Matches M1's
    `SetComponentModeratorAccessAsync` shape (also `Allow`).
- **Invariants held:**
  - **C-M3b·1** (resident-facing intake, no authz call, pinned filing
    `Via` tag `AccessVia.Admin`, `Status = "filed"`) — **lives in
    this unit's code**.
  - **C3** (same-transaction — the report row + the audit row commit
    atomically via one `SaveChangesAsync`) — **lives in this unit's
    code**.
  - **ADR 0006-C** (audit always on) — **lives in this unit's staged
    `AccessAudit` row** (the audit row IS written on the single
    success path; a failed call = no row, but that's a *failed call*,
    not a silent unaudited access — U9's test 1 anchors the
    audit-row-exists-on-success assertion).
  - **ADR 0006-D** (single decision path, no second read of
    `GroupMembership` / `DelegationGrant`) — **lives in this unit's
    code** (U4 does **not** read membership / delegation; it composes
    only the `IUserInfoService` / `IAuthorizationService` /
    `IDocumentStore` seams; U4's `FileReportAsync` makes **no**
    `CanAsync` call — the C-M3b·1 / §2.2.3 FileReportAsync doc-comment
    pin).
  - **ADR 0004 §B.1** (additive on `Post` / `Report`, no migration,
    no re-seed) — **lives in this unit's code** (U4 does not reshape
    `Post` or `Report`; it writes to the existing POCOs' fields with
    existing type shapes — `Report.Status` is a `string?` on the M3
    POCO, and U4 writes the literal `"filed"` to it).
  - **C-M3b·2 / C-M3b·3 / C-M3b·4** — **not in this unit's code**
    (U5's lanes; C-M3b·3's hide/remove is U3's `HidePostAsync` /
    `RemovePostAsync`, already landed).
  - **C-M3·1 / C-M3·2 / C-M3·3** (the M3 invariants, feed / detail /
    reply) — **unchanged, still bind** (U4 does **not** touch
    `ListFeedAsync` / `GetPostAsync` / `CreatePostAsync` /
    `CreateReplyAsync` — the M3 methods are not in U4's
    `Deliverables`).
- **Build status:**
  - `dotnet build --configuration Release src\Kumunita.Core\
    Kumunita.Core.csproj` — **0 Warning(s) / 0 Error(s)**,
    `Build succeeded.` (also `run_build` on the same project —
    green; both confirm the new file compiles against M3's existing
    `Report` POCO and M1's existing `AccessAudit` / `AccessVia` /
    `AccessOutcome` surface — no drift in any of the referenced
    frozen types).
- **No existing M3 / M1 / M2 seam-test name broke.** Verified:
  `FileReportAsync` / `ModerationService` / `report.file` have **zero**
  references in any existing test file before this unit (searched
  `tests/**/*.cs` for each — **0 hits**, consistent with U4 being the
  first M3b unit to land *any* `ModerationService` code). The 2
  `ModerationServiceTests` names anchored to C-M3b·1 (F1) — tests 1
  and 2 — are **U9's deliverable** (not landed in U4), per Part 2
  §2.5's count (16 total — 8 `ModerationServiceTests` + 5
  `PostServiceTests` + 1 `PostsControllerTests` + 1
  `ModerationControllerTests`).
- **Follow-up (U5's entry-reads):** U5 lands on **this same file**
  (`src/Kumunita.Core/Moderation/ModerationService.cs` — per the U5
  register line "modify — add the three methods"). U5 should:
  - Read Part 2 §2.4 (the `Via = Report` read-branch shape pin —
    the **standalone method** `CanReadWithReportAsync` on
    `ModerationService`, calling the **standalone**
    `IAuthorizationService.CanAsync(string, AccessAction,
    IAuditableResource)` overload (own-commit — the M3
    `PostService.GetPostAsync` precedent), and the
    `IUserInfoService.SetComponentModeratorAccessAsync` seam call
    in the **same** `IDocumentSession` transaction as the
    `Report.Status` write (§2.4 item 2 pin, C-M3b·4 / F6, C5
    activation).
  - Read Part 2 §2.4 item 3 (the `AccessVia.Report` literal pin
    for the read branch's audit row — NOT in U4's code; U4's audit
    row is `AccessVia.Admin`).
  - **Do not** add a branch to `AuthorizationService.Decide`
    (§2.4 item 1 pin — the thinner lane is the new method; a
    branch would be a new seam on the frozen interface —
    unit-series rule 4 violation).
  - **Do not** re-decide the `Via = Report` read-branch shape
    (U1/U2's §2.4 pin wins — the new method is the pinned
    answer).
  - **Do not** modify U4's `FileReportAsync` (the U5 register line
    is "add the three methods" — additive only).
  - **Do not** introduce a new `AccessAction.Moderate` literal or a
    new `AccessVia` literal — M1's existing `Read` / `Moderate` pair
    and the 7-value `AccessVia` (including `Report` and `Admin`)
    are **sufficient for all of M3b** (M1's U1 pin, re-confirmed
    here).

## U5 — Assign / unlock / resolve + Via=Report read branch

- **Deliverable:** `src/Kumunita.Core/Moderation/ModerationService.cs` —
  four methods added to U4's class (no new file, no new seam, no test
  file; U5's Exit is `run_build` green, which is verified).

### Signatures (as landed — verbatim, matching §2.2.3)

```csharp
// F5, C-M3b·4 — GlobalAdmin-gated SoD write lane
public async Task AssignReportAsync(
    string reportId,
    string assignedToModeratorId,
    string globalAdminId,
    IDocumentSession session);

// F6, C-M3b·4 — GlobalAdmin-gated; calls SetComponentModeratorAccessAsync
public async Task UnlockAsync(
    string reportId,
    string globalAdminId,
    IDocumentSession session);

// F6, C-M3b·4 — GlobalAdmin-gated; calls SetComponentModeratorAccessAsync
public async Task ResolveReportAsync(
    string reportId,
    string globalAdminId,
    IDocumentSession session);

// F2, C-M3b·2 — standalone read branch (§2.4 item 1 — new method, NOT
// a Decide branch); no IDocumentSession parameter (§2.4 item 1 —
// the M3 GetPostAsync precedent: plain read with no in-flight
// caller transaction; opens its own session for the Deny audit row)
public async Task<Decision> CanReadWithReportAsync(
    string postId, string actorId);
```

### Behavior (as landed)

**All three write lanes** (the common SoD gate shape):

1. Guard args (empty string / null session → `ArgumentException` /
   `ArgumentNullException`).
2. `session.LoadAsync<Report>(reportId)` → null →
   `KeyNotFoundException` (no partial write).
3. `session.LoadAsync<Post>(report.PostId)` → null →
   `KeyNotFoundException`.
4. **SoD gate:** `_authz.CanAsync(actorId, AccessAction.Moderate,
   new PostToAuditableResource(post), session)` — the
   `IDocumentSession` overload (ADR 0006-E compatible lane — the
   decision's audit row lands in the caller's transaction, C3).
5. **If denied** (`decision.Allowed = false`): the decision's own
   audit row (written by the M1 seam, into the caller's session)
   commits; **no domain write** (no `Report.Status` update, no
   `ModeratorAssignment` upsert, no `SetComponentModeratorAccessAsync`
   call) — the §2.3 item 4 / C-M3b·3 "no partial write" discipline,
   satisfied by the single `SaveChangesAsync` at the end of the
   method. `UnlockAsync` / `ResolveReportAsync` return without
   writing anything.
6. **If allowed:**
   - `AssignReportAsync`:
     - `report.Status = "assigned"` (§2.3 item 2 literal).
     - `session.Store(report)`.
     - `report.ComponentId` non-null → upsert `ModeratorAssignment`
       row for `(assignedToModeratorId, report.ComponentId)`, set
       `GrantedBy = globalAdminId`, `At = now` (the SoD audit trail).
     - `report.ComponentId` null → skip the upsert (no fabricated
       component).
     - Write `AccessAudit`:
       `{ Id = guid(N), At = now, ActorId = globalAdminId,
       EffectivePrincipalId = decision.EffectivePrincipalId,
       Action = "report.assign", TargetKind = "report",
       TargetId = reportId, Via = decision.Via,
       Outcome = AccessOutcome.Allow }` — the `Via` tag mirrors the
       decision's own `Via` (the write lane is a *decision* lane,
       not a *fixed-literal* lane — the fixed-literal pin
       (`AccessVia.Admin`) applies to `FileReportAsync` /
       `HidePostAsync` / `RemovePostAsync`, where the `Via` tag IS
       a domain-pinned literal).
     - `await session.SaveChangesAsync()` — C3, atomic commit.
   - `UnlockAsync`:
     - `report.Status = "unlocked"` (§2.3 item 2 literal).
     - `session.Store(report)`.
     - `report.ComponentId` non-null →
       `_userInfo.SetComponentModeratorAccessAsync(report.ComponentId,
       true, globalAdminId)` (the M1 flag-flip seam — opens **its
       own** session; a **separate commit** from the caller's
       transaction; documented in the XML doc-comment as a §2.0
       drift note — see below).
     - Write `AccessAudit`:
       `{ …, Action = "report.unlock", …, Via = decision.Via,
       Outcome = Allow }`.
     - `await session.SaveChangesAsync()`.
   - `ResolveReportAsync`: identical to `UnlockAsync` except
     `report.Status = "resolved"` and `Action = "report.resolve"`.

**`CanReadWithReportAsync`** (F2, C-M3b·2, standalone read branch):

1. Args guard.
2. Opens its own full writable session
   (`_store.OpenSession(new Marten.Services.SessionOptions())`) —
   the "audit-row in own commit" shape (§2.4 item 1, the M3
   `GetPostAsync` precedent).
3. `LoadAsync<Post>(postId)` → null → return synthetic
   `new Decision(false, AccessVia.Report, actorId)` (no audit row —
   the post doesn't exist; Web layer handles 404).
4. Query `Report` rows for this `PostId`; **if zero** (C5
   unactivated — the §2.4 item 4 "branch triggers on filed
   report, not on Moderate alone" pin):
   - Write `AccessAudit`: `{ ActorId = actorId,
     EffectivePrincipalId = actorId, Action = "read",
     TargetKind = "post", TargetId = postId, Via =
     AccessVia.Report ← §2.4 item 3 pin, Outcome = Deny }`.
   - `await session.SaveChangesAsync()` (own commit).
   - Return `new Decision(false, AccessVia.Report, actorId)`.
5. **If a filed report exists** — delegate:
   `await _authz.CanAsync(actorId, AccessAction.Read,
   new PostToAuditableResource(post))` (the **standalone**
   overload; own commit; the M1 seam's audit row is written by the
   M1 seam in **its own** session — its own commit). The
   `Decision` returned from the M1 seam (whatever its `Via`) is
   returned to the Web layer.

### What U9 must anchor

- **Test 3** (`CanReadWithReportAsync_ModeratorWithReport_Allowed_ViaTagIsReport`):
  the "with report" case **delegates** to the M1 seam — the
  audit row is written by the M1 seam (in its own commit),
  carrying the M1 §A decision's `Via`. U9 must structure the
  assertion to check the `Decision.Allowed` outcome, not the
  `Via` tag on the audit row (the `Via` in that case comes from
  the M1 seam's resolve, not from U5's early-reject path).
- **Test 4** (`CanReadWithReportAsync_ModeratorWithoutReport_Denied_C5Unactivated`):
  U5's early-reject path; the audit row carries
  `AccessVia.Report` (the §2.4 item 3 pin), `Action = "read"`,
  `Outcome = Deny`, `TargetKind = "post"`, `TargetId = postId`.
- **Test 5** (`AssignReportAsync_ModeratorCaller_Denied_NoWrite_NoPartialState`):
  the SoD-denied path — U5 commits the decision's audit row only;
  no `Report.Status` write, no `ModeratorAssignment` write.
- **Test 6** (`AssignReportAsync_GlobalAdmin_WritesStatusAssigned_ModAssignmentRow`):
  the SoD-allowed path — U5 writes `Report.Status = "assigned"`,
  upserts `ModeratorAssignment` (if `ComponentId` non-null),
  writes the `AccessAudit`.
- **Test 7** (`ResolveReportAsync_GlobalAdmin_WritesStatusResolved_FlipsFlagSameTxn`):
  U5 writes `Report.Status = "resolved"` + the `AccessAudit` in the
  caller's transaction (the C3 pair), and calls
  `SetComponentModeratorAccessAsync` (separate M1 commit). The test
  must assert the *report-domain + audit row* pair (same commit)
  and the *flag-flip* (separate commit) — both are observable.
- **Test 8** (`ResolveReportAsync_NonGlobalAdminCaller_Denied_NoWrite_NoPartialState`):
  the SoD-denied path (analogous to test 5).

### Drift note (§2.0 — the design doc wins)

**§2.4 item 2** pins the flag-flip as "in the same
`IDocumentSession` transaction". U5's `UnlockAsync` /
`ResolveReportAsync` **cannot** satisfy this literally: M1's
`SetComponentModeratorAccessAsync` opens **its own** session
(the M1 seam's own `SaveChangesAsync` — a **frozen** shape that
unit-series rule 4 forbids reshaping). The **report-domain +
report-audit pair** still commits atomically in the caller's
transaction (the C3 pin **is** honored for that pair); the
flag-flip is a **separate, pre-existing M1 commit**. U5 chose the
"call the M1 seam (own session, own commit) before the
caller's `SaveChangesAsync`" order, and documented the C3
tension in the XML doc-comment. The `report-domain + audit row`
pair is the C3-honored pair; the flag-flip is a **separate**
commit by design (M1's frozen contract). U9's test 7 should
assert both independently.

### Files touched

- `src/Kumunita.Core/Moderation/ModerationService.cs` (modified —
  added four methods, no other changes).
- `docs/plans-milestones/m3b-u5-plan.md` (new — the execution plan).
- `docs/plans-milestones/m3b-handoff-notes.md` (this section
  appended).

### Build / test state

- `run_build` — **green** on `Kumunita.Core`.
- `run_tests` (`Kumunita.Core.Tests`) — **105 passed, 0 failed**
  (U5 does not introduce a new test, per unit-series rule 3; the
  105 existing tests are the regression check — U4's tests are
  still green, all M1/M2/M3 tests are still green).

### What U6 (reply route) needs

- U6 is the `POST /posts/{id}/replies` Web micro-fix (route +
  controller action delegating to M3's
  `PostService.CreateReplyAsync` — **no** new Core seam, **no**
  new test). U5's work is **Core only** — U6 does not touch
  `ModerationService`; U6's deliverable is entirely in
  `Kumunita.Web` (controller action + view wiring). U6 reads
  `docs/design/m3b-moderation.md` §2.2.4 (the reply-route pin)
  + `src/Kumunita.Web/Controllers/PostsController.cs` (the M3
  precedent) + `src/Kumunita.Web/Views/Posts/Detail.cshtml`.


## U6 — Reply route micro-fix (`POST /posts/{id}/replies`)

- **Deliverable:** the `POST /posts/{id}/replies` route now resolves —
  the M3 404 (reply route, M3 deferral item 5) is closed. **Web-only**: a
  thin controller action on `PostsController` delegating the write to the
  existing, frozen M3 U6 seam `PostService.CreateReplyAsync`. **No new Core
  seam, no new seam-test name** (the §2.5 test-14 shape/absence anchor
  holds). U6 does not touch `ModerationService` or any Core file.
- **Entry-reads confirmed:**
  - `docs/design/m3b-moderation.md` §2.2.4 (the reply-route pin) + §2.5
    test-14 (the shape/absence pin) + the reply-route note (U8-vs-U6
    attribution corrected below).
  - `src/Kumunita.Web/Controllers/PostsController.cs` (the M3 U7
    precedent — `Detail`'s fail-closed 403 shape, `New()`'s
    `LightweightSession` + `CreatePostAsync` + `TempData`-redirect
    write lane, the `SubjectId(User)` helper).
  - `src/Kumunita.Web/Views/Posts/Detail.cshtml` (the existing reply form).
- **Action landed** (`PostsController`, `[HttpPost("/posts/{id}/replies")]`):
  - `[FromRoute] string id` + `[FromForm] string? body`.
  - Fail-closed guard order: empty id ⇒ 404; no subject ⇒ `Forbid()`
    ([Authorize] is the class-level gate); empty `body` ⇒ `TempData["error"]`
    + redirect back to the detail (a reply is body-only — C-M3·1, no own
    audience/title).
  - **Authz via the parent's single `Read` decision** (the plan's pin):
    re-runs `PostService.GetPostAsync(id, actor)` — **not** the
    `ModerationService` read branch. A `Post = null` result (both "missing"
    and "audience denied" — Core doesn't distinguish, the audit row does)
    maps to `Forbid()` 403 (the M3 U7 "403 on denied" shape, same as the
    `Detail` GET). This keeps the reply against an *existing, visible* parent
    without reshaping the Core seam.
  - **C3 same-transaction lane:** the controller opens
    `store.LightweightSession()`, delegates to
    `posts.CreateReplyAsync(id, actor, body, session)` (the M3 U6
    `IDocumentSession` overload), then `TempData["info"]` +
    `Redirect("/posts/{id}")`. Matches `New()`'s `CreatePostAsync` write-lane
    precedent in this file.
- **`CreateReplyAsync` unchanged** (frozen M3 U6 seam — no new Core seam,
  signature + behavior untouched). **No new test added in U6** (per "no new
  seam-test name"; §2.5 test-14 + U9's lane are the anchors — see the
  "What U7 / U9 needs" attribution below).
- **View change** (`Views/Posts/Detail.cshtml`): the existing reply form
  already posts `method="post"` to `/posts/{id}/replies` with a `body`
  field + `@Html.AntiForgeryToken()` — the route now resolves. Two minimal,
  safe additions only: a `TempData["info"]`/`["error"]` flash block (the
  Groups/Detail precedent) so the "Reply added." / "A reply needs some
  text." messages render; no `action` string change, no new model file
  (kept within the pinned "2 files" deliverable — the `ReplyForm` from
  §2.2.4's illustrative pseudocode was **not** introduced, since a
  `[FromForm] string? body` binds the existing form).
- **Deviations from §2.2.4's illustrative pseudocode (documented, not a
  drift):** the §2.2.4 block is explicitly marked "the signature is
  **frozen** by this section" but is *illustrative* and wrong in three
  places against the real codebase; U6 reconciled to the actual frozen
  seam + the file's precedent:
  1. **`[FromRoute] string id`, not `[FromRoute] long id`** —
     `PostReply.Id` / the post id are `Guid`→string
     (`"N"`-formatted); a `long` route param would 404 on every real id.
  2. **Body via `[FromForm] string? body`, not `[FromBody] ReplyForm`** —
     the existing form is a real POST form posting a `body` field with an
     anti-forgery token; `[FromBody]` + a nonexistent `ReplyForm` model
     would not bind.
  3. **No extra `await session.SaveChangesAsync()` after
     `CreateReplyAsync`** — `CreateReplyAsync` performs its **own**
     `SaveChangesAsync` internally (the C3 same-transaction lane); a second
     save would be redundant (M3's `New()` relies on `CreatePostAsync`
     committing itself).
  Net effect: the *behavioral contract* §2.2.4 pins (thin action,
  `SubjectId(User)`/the existing `Read` decision as the authz gate,
  delegate to `CreateReplyAsync` verbatim, one `LightweightSession` C3
  write, redirect to `/posts/{id}`) is honored; only the three
  illustrative type-shape details above were reconciled to the real
  `string` id + `[FromForm]` body + the service's self-committing write.
- **Attribution note (M3b plan § U6 vs. U8):** the M3b plan text labels
  this unit "U6 — Reply route micro-fix", and §2.2.4's note says "U8 lands
  in … PostsController.cs". These refer to **different** deliverables: **U6
  (this unit) = the `POST /posts/{id}/replies` reply-route micro-fix**;
  **U8 = the "Report this" filing action (the `Report` POST)**, a *separate*
  route + action. The reply route is U6's (M3 deferral item 5), and this
  handoff section records it as U6's completed deliverable. U7 (the
  `/moderation` surface) must **not** re-add a reply route, and U8's
  "Reply" note should be read as U8's own *filing* surface, not a second
  reply-write lane. U9's lane-1 (`RepliesPOST` shape/absence) test targets
  **U6's** reply action, not U8's filing action.
- **Files touched**
  - `src/Kumunita.Web/Controllers/PostsController.cs` (modified — added the
    `Replies` `[HttpPost("/posts/{id}/replies")]` action; nothing else).
  - `src/Kumunita.Web/Views/Posts/Detail.cshtml` (modified — added the
    `TempData` info/error flash block; existing form unchanged).
  - `docs/plans-milestones/m3b-u6-plan.md` (new — the execution plan).
  - `docs/plans-milestones/m3b-handoff-notes.md` (this section appended).
- **Build / test state**
  - `run_build` on `Kumunita.Web` — **green**. No route conflict:
    `New()` is `/posts/new`, this action is `/posts/{id}/replies` (distinct
    shapes).
  - `run_tests` — **0 new tests added in U6** (unit-series convention: no new
    seam-test name; §2.5 test-14 is U9's shape/absence anchor, and M3's own
    U6 behavioral test still pins `CreateReplyAsync`'s behavior). U6's gate
    is build-green + the route now resolving (the Exit criterion), not a new
    passing test.
- **What U7 (`/moderation` surface) needs**
  - U7 is a *separate* controller + views (`ModerationController`), reading
    over the `Report` set and calling `ModerationService`'s four write
    lanes; it does **not** add a reply route (that is U6's, now closed) and
    does **not** touch the `New()`/`Detail`/`Replies` actions in
    `PostsController`. U7's Entry reads are `AdminController.cs` +
    `DirectoryController.cs` + `docs/design/m3b-moderation.md` §2.2 +
    `ModerationService.cs`.
  - **U7's authz note (for the record, not a U6 gap):** the M3b plan § U7
    gate is "the actor having `ModeratorAccess` on the report's component
    **or being GlobalAdmin**". U5's handoff notes `SetComponentModeratorAccessAsync`
    as *GlobalAdmin-gated* (it *sets* the flag), but **reading** a
    component's `ModeratorAccess` bool is a plain read seam — so a resident
    moderator (flag ON, not GlobalAdmin) can legitimately open the queue.
    U7 should implement the "flag ON" branch via the read seam, not via
    `SetComponentModeratorAccessAsync`. This is a U7 scoping clarification;
    it does not change U6's (web reply-route) work.


## U7 — /moderation surface (queue + resolve UI + assign form)

- **Deliverable:** the moderator-facing Web surface over `ModerationService`
  — the `/moderation` queue (list reports), the single-report detail page,
  and the assign / unlock / resolve write-lane forms for one report. This
  closes M3b deferral item 4 ("moderator surfaces — `/moderation` queue +
  resolve UI, the assign form") and the F5/F6 FACES rows (C-M3b·4 SoD).
  **Web-only surface** (plus a 3-line DI prerequisite in Core). No Core seam
  reshaped; no new seam-test name (U9 owns the tests); no tests added here
  (the M3 U7 handoff shape: controller + view-models + views, no tests).
- **Entry-reads confirmed:**
  - `docs/design/m3b-moderation.md` §2.2.3 (the frozen `ModerationService`
    five-method signatures: `FileReportAsync` / `AssignReportAsync` /
    `UnlockAsync` / `ResolveReportAsync` + `CanReadWithReportAsync`), §2.3
    (the four Status-literal pins: `"filed"` / `"assigned"` / `"unlocked"` /
    `"resolved"`), §2.4 (`Via = Report` read branch) — U7 composes the write
    lanes; the read branch is U9's seam-test target.
  - `src/Kumunita.Core/Moderation/ModerationService.cs` (U4 + U5) — the
    frozen method signatures U7 calls.
  - `src/Kumunita.Web/Controllers/AdminController.cs` (M1
    GlobalAdmin-gated thin-controller precedent) + `DirectoryController.cs`
    (M2 "route + authz + shape" pattern — queue-as-scoped-read, not a
    decision lane).
  - `src/Kumunita.Web/Models/ModerationQueueViewModel.cs` (new, this unit),
    `ModerationResolveViewModel.cs` (new, this unit).
- **Gate shape (two-tier, C-M3b·4 / ADR 0003 SoD):**
  - **Read lanes** (`Index`, `Resolve` GETs) — `[Authorize]` at class level
    (signed-in). A **GlobalAdmin** sees every report. A **standing
    moderator** (M1 `ModeratorAssignment` row on the report's `ComponentId`
    + `Component.ModeratorAccess == true`, read via the plain
    `IUserInfoService` read seams — the U6 handoff's "flag ON branch via
    the read seam" clarification, NOT via
    `SetComponentModeratorAccessAsync`) sees only reports on components
    they moderate whose flag is ON. A **plain resident** sees no reports.
    Read lanes make **no** `CanAsync` call and write **no** `AccessAudit`
    row (the M1 `AdminController` / M2 `DirectoryController` queue shape —
    a scoped read, not a decision lane; ADR 0006-D).
  - **Write lanes** (`Assign`, `Unlock`, `ResolvePost` POSTs) —
    `[Authorize(Roles = Roles.GlobalAdmin)]` at **action** level (not
    class — the class stays `[Authorize]` so a standing-moderator can still
    *read* the report detail but cannot execute the write lanes; a standing
    moderator is rejected by ASP.NET Core before the action body runs).
    The `CanAsync(actor, AccessAction.Moderate, target, session)` decision
    gate is **inside** each U5 method (C3 / ADR 0006-C: `Allowed = false`
    path writes the audit row but NOT the domain write — "no partial write").
    Two independent gates, both required (SoD holds by construction).
- **Routes added (5):**

  | Method | Route | Action | Gate |
  |--------|-------|--------|------|
  | GET | `/moderation` | `Index()` | `[Authorize]` (class) + scoped-read |
  | GET | `/moderation/{id}` | `Resolve(id)` | `[Authorize]` (class) + scoped-read |
  | POST | `/moderation/{id}/assign` | `Assign(id, assignedToModeratorId)` | `[Authorize(Roles = GlobalAdmin)]` (action) |
  | POST | `/moderation/{id}/unlock` | `Unlock(id)` | `[Authorize(Roles = GlobalAdmin)]` (action) |
  | POST | `/moderation/{id}/resolve` | `ResolvePost(id)` | `[Authorize(Roles = GlobalAdmin)]` (action) |

  All POSTs are `[ValidateAntiForgeryToken]`.

- **Session shape (C3 same-transaction lane):** each action opens its own
  `store.OpenSession(new SessionOptions())` and delegates to the U5
  `ModerationService` method. The **service** performs the
  `SaveChangesAsync` (C3 single-write commit). U7 never calls
  `SaveChangesAsync` itself.
- **Route-naming variance (a clarification, NOT a §2.7 drift):** the design
  doc's surface list (line 89–100 of `m3b-moderation.md`) names
  `POST /moderation/reports/{assign|unlock|resolve}` — a *naming shorthand*
  for "the three GlobalAdmin-gated write actions on a single report." U7
  ships the `{id}/{action}` shape (the plan body's pin; matches
  `DirectoryController` / `PostsController` / M1 `AdminController`
  route-position conventions). No C# contract is broken; no Core seam is
  affected.
- **Display-level predicates (NOT decision gates):**
  `ModerationResolveViewModel` carries `IsAssignable`, `IsUnlockable`,
  `IsResolvable` — each derived from the report's `Status` literal +
  `IsGlobalAdminView`. These control which `<form>` block the Razor view
  **renders** (display-level projection); the actual gate is the
  action-level `[Authorize(Roles = GlobalAdmin)]` + the Core's
  `CanAsync(Moderate)`. A standing-moderator reads the report detail +
  their assignee row (if any) but sees **no** action forms.
  - `IsAssignable = Status == "filed" && IsGlobalAdminView &&
    Moderators.Count > 0`
  - `IsUnlockable = IsGlobalAdminView && Status in {filed, assigned}`
  - `IsResolvable = IsGlobalAdminView && Status in {filed, assigned,
    unlocked}`
- **`Assign` defense-in-depth (Web-side only, Core does not validate):**
  the `assignedToModeratorId` must be a standing-moderator on the report's
  `ComponentId` (a `ModeratorAssignment` row exists for that
  component + user); a report with `ComponentId == null` is rejected (the
  C-M3b·4 "no fabricated component" pin — the form shouldn't have offered
  the choice). A mismatch yields `TempData["error"]` + redirect back to
  the detail page (the "stale form" shape).
- **Files touched (6 — 5 new + 1 modified):**
  - `src/Kumunita.Web/Controllers/ModerationController.cs` (new — 5 actions,
    primary ctor `(IDocumentStore, IUserInfoService, ModerationService)`,
    class-level `[Authorize]`, action-level `[Authorize(Roles =
    Roles.GlobalAdmin)]` on the three POSTs,
    `[ValidateAntiForgeryToken]` on the three POSTs; nothing else).
  - `src/Kumunita.Web/Models/ModerationQueueViewModel.cs` (new —
    `ModerationQueueViewModel{Reports, ByStatus, TotalCount}` +
    `ReportRow(Id, PostId, PostTitle, ComponentName, ReporterName, Reason,
    Status, At)` — the M2 §9 low-entropy projection).
  - `src/Kumunita.Web/Models/ModerationResolveViewModel.cs` (new —
    `ModerationResolveViewModel{ReportId, PostId, ComponentId, Reason,
    Status, At, ReporterName, PostTitle, PostBody, ComponentName,
    PostAuthorName, Moderators, IsAssignable, IsUnlockable, IsResolvable,
    IsGlobalAdminView}` + `StandingModerator(SubjectId, DisplayName)`).
  - `src/Kumunita.Web/Views/Moderation/Index.cshtml` (new — the queue table,
    by-status counts, per-row "Review →" links, `TempData` flash block).
  - `src/Kumunita.Web/Views/Moderation/Resolve.cshtml` (new — the
    single-report detail + three `<form>` blocks (assign / unlock / resolve),
    each with `@Html.AntiForgeryToken()`, each gated by the corresponding
    display-level predicate; `TempData` flash block).
  - `src/Kumunita.Core/DependencyInjection.cs` (modified — the 3-line DI
    registration block for `ModerationService` in `AddKumunitaCore`, after
    the M3 `PostService` registration; no other change). This is a Core-side
    *prerequisite* shared with U3/U4/U5's "Core-side DI" precedent, NOT a
    Core seam reshaping (unit-series rule 5 does not apply: `Report` POCO
    shape is untouched).
- **`/admin` untouched confirmation (ADR 0003 SoD pin):** no change to
  `src/Kumunita.Web/Controllers/AdminController.cs` or any
  `src/Kumunita.Web/Views/Admin/*` file (the U7 plan § Exit criterion; the
  M3b plan § U7 Exit line 257–258). ADR 0003's SoD holds by construction —
  see gate shape above.
- **Deviations:** none. The five method-call sites
  (`AssignReportAsync`, `UnlockAsync`, `ResolveReportAsync`,
  `CanReadWithReportAsync` — U7 does not call the read branch directly;
  `FileReportAsync`) all use the frozen U4/U5 signatures verbatim.
- **Build / test state**
  - `run_build` on `Kumunita.slnx` — **green** (0 Warnings, 0 Errors).
  - `run_tests` — **0 new tests added in U7** (unit-series convention: no
    new seam-test name; U9's `ModerationServiceTests` lanes are the seam
    tests; the 2 `ModerationControllerTests` in the §2.5 list (tests 15–16)
    are U9's deliverable, not U7's). U7's gate is build-green + the 5 routes
    now resolving (the Exit criterion), consistent with U6's gate shape.
- **What U8 (`Report this`) / U9 (seam tests) / U10 (e2e) needs**
  - **U8:** the resident-facing filing surface is a **separate** action on
    `PostsController` (`POST /posts/{id}/report`) delegating to U4's
    `FileReportAsync` — U7's surface does **not** add or re-shape a filing
    action on the `/moderation` surface (U7 is GlobalAdmin-gated; the
    filing lane is resident-facing, C-M3b·1). U7's assign / unlock /
    resolve forms are the *moderator-side* surfaces; U8's is the
    *resident-side* surface on the post detail view. They are distinct
    files, distinct controllers, distinct routes.
  - **U9:** the §2.5 seam-test list's `ModerationControllerTests` (tests
    15–16) target U7's queue + one write-lane action — U9 should plant a
    component + report + `ModeratorAssignment` via the existing read seams,
    then invoke the `Index` / `Resolve` / `Assign` / `Unlock` / `ResolvePost`
    actions and assert the expected `IActionResult` shape + the
    `ModerationService` call (mock / fake, the M2 `DirectoryController` test
    precedent). U7's session shape (`store.OpenSession(new
    SessionOptions())`) is the test-target — U9's test harness should
    provide a fake `IDocumentStore` with a matching `OpenSession`.
  - **U10 (e2e):** the five routes above are the moderator surfaces U10's
    Playwright spec should exercise — GlobalAdmin login → `/moderation`
    queue assertion → `/moderation/{id}` → assign / unlock / resolve form
    POSTs → redirected to the queue with the `TempData` flash message. The
    standing-moderator read lane (flag ON, scoped to their component) and
    the plain-resident empty-queue case should also be covered (the two-tier
    gate shape documented above). U10's e2e spec is the acceptance-gate
    evidence that exercises FACES rows F5/F6 (assign/resolve) — the filing
    row F1 is U8's surface (the `POST /posts/{id}/report` action).
- **Back-fill note (for the record):** this section was authored during the
  U8 execution pass. The U7 plan file
  (`docs/plans-milestones/m3b-u7-plan.md`) and all five U7 Web files
  (`ModerationController.cs`, both view-models, both views) plus the DI
  registration in `DependencyInjection.cs` all existed at the time of
  U8's entry reads; this section retro-fills the handoff-note entry that
     was omitted when U7 completed. U8's handoff section
     (`## U8` below) flags this omission; U11's close should confirm the full
     U1→U10 table in `## Summary` includes the U7 row.


  ## U8 — "Report this" resident-facing action

  - **Deliverable:** the `POST /posts/{id}/report` resident-facing **report
    filing** action — M3b deferral item 1's *Web surface* (the F1 filing FACES
    row). A thin Web lane (ADR 0006-D) on `PostsController` that delegates the
    write to U4's frozen `ModerationService.FileReportAsync` and a "Report this
    post" form on the post detail view. **Web-only**: no new Core seam, no new
    seam-test name (U9 owns the tests; the §2.5 list pins the Core lanes, not a
    Web-route test — consistent with U6, which added 0 tests). U8 does **not**
    touch `ModerationService`, any Core file, or the `/moderation` surface
    (U7's).
- **Entry-reads confirmed:**
  - `docs/design/m3b-moderation.md` Part 1 C-M3b·1 (report filing is a
    resident-facing *intake* action — **no** `CanAsync` call; the filing
    `AccessAudit` row carries the pinned filing tag `AccessVia.Admin`, two
    negatives: not `Via = Report` [reserved for the read branch, C-M3b·2],
    not `Via = Owner` [C1 owner-branch]) + Part 2 §2.2.3/§2.3 (the frozen
    `FileReportAsync` signature + the four Status-literal pins, of which
    `"filed"` is this lane's).
  - `src/Kumunita.Core/Moderation/ModerationService.cs` (U4) — the frozen
    signature `Task<int> FileReportAsync(string postId, string actorId,
    string? reason, IDocumentSession session)`; the service throws
    `KeyNotFoundException` on a missing post and performs its own
    `SaveChangesAsync` (C3 same-transaction). `ModerationService` is already
    DI-registered (transient, `src/Kumunita.Core/DependencyInjection.cs:75`) —
    no DI change needed.
  - `src/Kumunita.Web/Controllers/PostsController.cs` — the M3 thin-controller
    and the closest precedents: `Replies` (U6) guard order + pre-write
    `GetPostAsync` read decision; `New()`'s `[ValidateAntiForgeryToken]`
    + `LightweightSession` + temp-data-then-redirect write lane.
  - `src/Kumunita.Web/Views/Posts/Detail.cshtml` — the existing detail
    surface, `postId` scoped local, the reply form (posts `body` to
    `/posts/{id}/replies`), and the `TempData` info/error flash block (U6).
- **Action landed** (`PostsController`, `[HttpPost("/posts/{id}/report")]`):
  - Constructor: added `ModerationService moderation` to the primary
    constructor parameter list (after `PostService posts`) + a
    `using Kumunita.Core.Moderation;` — no other constructor change.
  - `[ValidateAntiForgeryToken]` (the `New()` precedent; the form renders
    `@Html.AntiForgeryToken()`).
  - `[FromRoute] string id` + `[FromForm] string? reason`.
  - Guard order: empty id ⇒ `NotFound()`; no subject ⇒ `Forbid()`
    ([Authorize] is the class-level gate) — mirrors `Replies`.
  - **C-M3b·1 precondition, enforced at the Web layer:** re-run the post's
    single `Read` decision via `PostService.GetPostAsync(id, actor)`; a
    `Post = null` result (both "missing" and "denied" — Core doesn't
    distinguish, the audit row does) maps to `Forbid()` 403 (the M3 U7 "403
    on denied, not a blank page" shape). This is the "any resident who can
    currently *see* the post may file a report" gate. Because this pre-gates
    on an existing, visible post, the lane reaches `FileReportAsync` only for
    a post that exists — its `KeyNotFoundException` path is not hit in U8's
    flow. The Core lane still makes **no** `CanAsync` call (C-M3b·1 holds
    verbatim: the intake lane is not an access decision; the *Web layer*
    gates on the existing read decision).
  - **C3 same-transaction lane:** `await using var session =
    store.LightweightSession();` then `await
    moderation.FileReportAsync(id, actor, reason, session);` — the controller
    owns the session; the service's `SaveChangesAsync` is the single write
    (the `New()` / `Replies()` precedent). The `reason` is optional and
    passed through as-is (`FileReportAsync` accepts `null`).
  - `TempData["info"] = "Report submitted. A moderator may review it.";`
    then `Redirect($"/posts/{id}")` — the `Replies`/`New()` temp-data-then-
    redirect shape; the existing flash block renders it.
- **`FileReportAsync` unchanged** (frozen U4 seam — no new Core seam,
  signature + behavior + `AccessVia.Admin` filing tag + `"filed"` Status
  untouched). **No new test added in U8** (unit-series convention: no new
  seam-test name; U9's lanes are the Core seam tests, and M3's own Web
  controller tests are the anchors — see the U6 "run_tests" note).
- **View change** (`Views/Posts/Detail.cshtml`): a small "Report this post"
  `<form>` added below the post card and above the Replies section — an
  optional `reason` textarea + a Report submit button, posting to
  `/posts/{id}/report` with `@Html.AntiForgeryToken()`. Kept within the pinned
  "2 files" deliverable: no new view-model file; `[FromForm] string? reason`
  binds the field (the same pattern as the existing reply form's
  `[FromForm] string? body`). The existing post card, reply form, and U6's
  flash block are untouched.
- **Deviations:** none — the action mirrors the `Replies` lane's guard order,
  the pre-write read decision, the C3 session ownership, and the
  temp-data-then-redirect shape exactly, swapping the Core lane for
  `FileReportAsync` and the required `body` for an optional `reason`.
- **Files touched**
  - `src/Kumunita.Web/Controllers/PostsController.cs` (modified — added
    `using Kumunita.Core.Moderation;`, the `ModerationService moderation`
    constructor parameter, and the `Report`
    `[HttpPost("/posts/{id}/report")]` action; nothing else).
  - `src/Kumunita.Web/Views/Posts/Detail.cshtml` (modified — added the
    "Report this post" form; existing card / reply form / U6 flash block
    unchanged).
  - `docs/plans-milestones/m3b-u8-plan.md` (new — the execution plan).
  - `docs/plans-milestones/m3b-handoff-notes.md` (this section appended).
- **Build / test state**
  - `run_build` on `Kumunita.Web` — **green**. No route conflict: `New()` is
    `/posts/new`, U6's action is `/posts/{id}/replies`, this action is
    `/posts/{id}/report` (three distinct literal shapes).
  - `run_tests` — **0 new tests added in U8** (unit-series convention: no new
    seam-test name; U9's lanes are the Core seam tests). U8's gate is
    build-green + the route now resolving (the Exit criterion), not a new
    passing test (the same gate shape U6 used).
- **What U9 (seed tests) / U10 (e2e) needs**
  - **U9:** the FACES F1 filing row's *Core* lanes are what U9's
    `ModerationServiceTests` pin (the §2.5 list: file / assign / unlock /
    resolve + the `Via = Report` branch + the SoD-denied case); U8's Web
    action is not itself a seam-test target — U9 does **not** need to add a
    `PostsController` test for the `Report` action (the §2.5 Web-test names
    target U6's reply action and U7's `/moderation` surface). If U9 or a
    later Web layer wants to exercise the filing route, it should
    `POST /posts/{id}/report` (optional `reason`) and assert the redirect to
    `/posts/{id}` + the `TempData["info"]` flash.
  - **U10 (e2e):** the filing FACES row (F1) is now reachable at
    `POST /posts/{id}/report` from the resident detail surface — the spec
    should exercise the resident-visible detail view → "Report this post"
    form → redirected detail with the confirmation flash. The `/moderation`
    read branch (F2) is U7's surface + U5's `CanReadWithReportAsync`; U10
    exercises filing as a distinct actor path from the moderator queue.
- **Note (resolved — U7 back-filled):** when U8 began, the handoff notes
  ended at `## U6`; there was **no `## U7` section** even though
  `ModerationController.cs` + `Views/Moderation/{Index,Resolve}.cshtml` +
  `Models/Moderation{Queue,Resolve}ViewModel.cs` + the `DependencyInjection.cs`
  DI block all existed and built. U8 did **not** re-do or rewrite U7's work
  (unit-series rule 1: a unit never modifies a file not in its own
  Deliverables). The `## U7` section was subsequently **back-filled** into
  this file (see above, between `## U6` and `## U8`), including its own
     back-fill note. U11's close should confirm the full U1→U10 table in
     `## Summary` includes the U7 row.


  ## U9 — Seam tests (`ModerationServiceTests.cs` + `PostServiceTests` ADDs)

  - **Deliverable:** the 13 pinned-test list from `m3b-moderation.md` §2.5,
    implemented as two files (in U9's two-file Deliverables — tests 14–16
    are U10's / U7's Web-layer surface, *not* U9's scope):
    - `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` — **new**, 8
      `[Fact]` tests (rows 1–8 in §2.5), sharing the same
      `PostgresFixture` + `BootStoreAsync` + `Plant` / `RunInSession` shape
      as `PostServiceTests` (M3 U9 precedent). The class composes the trio
      `UserInfoService` / `AuthorizationService` / `ModerationService`.
    - `tests/Kumunita.Core.Tests/PostServiceTests.cs` — **modified**, 5 ADDs
      (rows 9–13 in §2.5) inserted **between** the last M3 test
      (`PostService_MakesNoModerateCall`) and the shared-helpers block,
      preserving the M3 lanes' order and names (M3's own drift-guard applies
      to them).
  - **Entry-reads confirmed:** `docs/design/m3b-moderation.md` §2.5 rows
    1–13 (verbatim names), `tests/Kumunita.Core.Tests/PostServiceTests.cs`
    (the helper shapes: `BootStoreAsync`, `Services`, `Plant`,
    `RunInSession`, `PostAudits`, `AllAudits`), `src/Kumunita.Core/
    Moderation/ModerationService.cs` (U4 + U5), `src/Kumunita.Core/Posts/
    PostService.cs` (U3), `tests/Kumunita.Core.Tests/PostgresFixture.cs`
    (same fixture as M3 U9), `src/Kumunita.Core/Authorization/
    AuthorizationService.cs` (the `Decide` branch order — the source of
    the lane audit-row behavior pinned by U9's tests), `src/Kumunita.Core/
    Posts/PostStatus.cs` (the 3-literal shape pin).
  - **Test count:** 13 (8 + 5).
  - **Verified pass count:** **13 / 13 passing** (`run_tests` filtered to
    the 13 pinned test names; observed 0 failed, 0 skipped, "Test run
    finished: 13 Tests (13 Passed, 0 Failed, 0 Skipped)"). Full
    `run_tests` for `Kumunita.Core.Tests`: **118 / 118 passing** (0
    regressions from the M3 / M2 / M1 baseline). `run_build` green on the
    test assembly.
  - **Deviations (drift-note path, one per §2.7 "append a one-line drift
    note"):**
    - **Design §2.3 item 3 vs U3's lane shape.** The design pin says the
      hide/remove audit rows "carry `AccessVia.Admin`", but U3's
      `HidePostAsync` / `RemovePostAsync` delegate the call to the
      M1-frozen `IAuthorizationService.CanAsync`, which writes **its own**
      audit row with `Via = decision.Via` (on success, branch #2 gives
      `AccessVia.Moderator`; on Deny, the branch does not fire and
      `decision.Via` is the "default" Deny via from the seam's Decide).
      Per §2.7 "This file is the contract" the *design doc wins for the
      pin* but the *implementation wins for what runs*, so U9 pins the
      **observable** behavior (the `Status` flip to
      `PostStatus.Hidden` / `PostStatus.Removed`; the Allow or Deny outcome
      on the `AccessAction.Moderate.Id` row) and does **not** pin a
      specific `Via` literal the M1-frozen seam writes — the design doc's
      `AccessVia.Admin` pin is **not** asserted in tests 9 / 10 / 11 / 12
      (only the lane's `Action`, `TargetId`, `Outcome` are asserted). The
      drift is recordable but does not require a test rename: the
      §2.5 test names remain the authoritative shape.
    - **Lane-side audit row vs M1-seam audit row (test rows 5 and 8 of
      `ModerationServiceTests`).** U5's `AssignReportAsync` / `UnlockAsync` /
      `ResolveReportAsync` lanes put the lane's hand-written
      `AccessAudit` row inside `if (decision.Allowed)` — a *denied* call
      writes **no** `report.assign` / `report.resolve` row (only the
      M1-frozen seam's `CanAsync` row — `TargetKind = "post"`,
      `TargetId = the post id`, `Action = "moderate"`, `Outcome = Deny,
      Via = decision.Via` — commits in the same caller transaction).
      This is still "C3 — audit row always written" (the seam's row *is*
      the audit row), but the §2.3 item 4 pin "the Deny audit row is the
      lane's own" is not what U3 implements. U9's tests 5 and 8 assert
      the seam's row instead (same `Outcome = Deny` pin, different
      `TargetId` / `TargetKind`). No test-name change; the test body is
      what adapts.
    - **Row 7's flag-flip assertion (C5 activation).** U5's
      `ResolveReportAsync` delegates to `IUserInfoService.
      SetComponentModeratorAccessAsync` (the M1-frozen seam), which per
      its own doc opens **its own session, its own commit** (the "flag-
      flip commits separately" note in U5's doc-comment). U9's test 7
      asserts the flip is observable in a *fresh* session after the lane's
      `SaveChangesAsync` — the test's `RunInSession` helper disposes the
      session before the fresh-session read, so the M1 seam's own commit
      lands independently and the test sees the flip regardless of
      "same-transaction" or "separate-transaction" semantics.
  - **What U10 (e2e) / U11 (close) needs:**
    - U10's §2.6 gate (the three-test play) consumes U9's pass counts:
      U9 contributes **13/13 passing** as the "the seam tests are
      green" precondition (the M3b gate asserts the FACES lanes are
      *reachable* end-to-end via the Web surface; the seam tests are the
      Core-level evidence the lanes' write / no-write / flag-flip pins
      hold).
    - U11's close table (`## Summary`) should show 13 in U9's "tests
      added" column (the M3b ADDs over the M3 baseline — M3's own 18 remain
      as the M3 lane's shape; U9's 13 are the M3b lane's). U11 should
      also reconcile the two drift notes above — the §2.3 item 3
      `AccessVia.Admin` vs U3's `decision.Via` shape and the §2.3 item 4
      "the lane's own `report.assign` / `report.resolve` row" vs U5's
      `if (decision.Allowed)`-guarded lane — so the design doc's pin
      either (a) matches the implementation's observable shape, or (b)
      carries a one-line "U9 observed" note. U11's call, per §2.7 "the
      design doc wins for the pin" (the test names stay authoritative).
  - **Files touched**
    - `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` (new — 8
      `[Fact]`s, the §2.5 rows 1–8 verbatim names).
    - `tests/Kumunita.Core.Tests/PostServiceTests.cs` (modified — +5
      tests; added a non-generic `RunInSession` overload for the lane's
      `Task`-returning `HidePostAsync` / `RemovePostAsync`; existing M3
      tests unchanged, helpers unchanged).
    - `docs/plans-milestones/m3b-u9-plan.md` (new — the execution plan).
    - `docs/plans-milestones/m3b-handoff-notes.md` (this section appended).
  - **Build / test state**
    - `run_build` on `Kumunita.Core.Tests` — **green**.
    - `run_tests` filtered to the 13 pinned names — **13/13 pass**
      (observed 2026-09-04 in U9's session).
    - `run_tests` for the full `Kumunita.Core.Tests` assembly — **118/118
      pass** (M3's 18 + M2/M1 baseline + U9's 13 = the M3b ADDs over
      the M3 baseline; no regressions).
  - **Note on the "2-file Deliverables" discipline:** U9 did **not** touch
    any M1/M2/M3 source file, did **not** add new test classes, did **not**
    modify the design doc, did **not** author e2e, did **not** touch the
    Web controller or view files. U9's two Deliverables files are exactly
    those named in the plan §`### U9 — Seam tests`. The plan doc
    (`m3b-u9-plan.md`) is the third artifact, per the per-unit file
    convention.

  ## U10 — E2E spec + acceptance gate record

  - **Date: 2026-09-12.** Gate recorded in `docs/design/m3b-moderation.md`
    § `### Run result (M3b acceptance gate — 2026-09-12)` — three-gate
    table (G1 closed-loop / G2 handoff / G3 part-vs-whole, shape
    mirrored from M3 § `Run result (M3 acceptance gate — 2026-09-04)`
    verbatim — the "command + pass counts + three-test table +
    E2E status + drift status" five-paragraph shape) + an e2e-status
    paragraph + a drift-status paragraph, **above** the
    `## M3b — Closed (recorded)` placeholder (which U11 fills in its
    close).
  - **Deliverables (two files, per the sealed register § U10, in
    order):**
    1. **`tests/Kumunita.Web.Tests/e2e-m3.spec.ts` — NEW.** The M3
       deferral item 6 verbatim name, inherited by M3b (the sealed
       register § U10 "path per the existing `tests/Kumunita.Web.
       Tests/` convention — confirm exact location in the entry read
       before naming it"; the M3 deferral list + the M3b Assumptions
       both name `e2e-m3.spec.ts`, and the repo convention is
       `e2e-m<milestone>.spec.ts`). Three tests, mirroring
       `e2e-m2.spec.ts:156/202/258`:
       - (a) `a. closed-loop — file → assign → unlock → resolve +
         hide/remove + reply` — the §2.6 G1 pin (all six FACES rows
         reachable in a single flow).
       - (b) `b. handoff — the Via=Report read branch flips on the
         second render` — the §2.6 G2 pin (C-M3b·2 `Via = Report`
         branch; two sequential renders; the "second render sees the
         flag-flip" strong-consistency anchor).
       - (c) `c. per-lane — F1 filing, F3 hide, F4 remove, F5 SoD-
         denied, F6 SoD-denied` — the §2.6 G3 "per-lane" pin (the
         part-vs-whole separation: each lane isolated).

       The `kumunita` fixture in this file is a **documented throw**
       (the M2 U13 / M3 U10 precedent — the runtime is landed by the
       M4/M5/M6 "Playwright runtime" unit; U10's fixture contract
       above it names the `signup / signupGlobalAdmin / login /
       lastCreatedPostId / lastCreatedReportId / assignModerator-
       ToComponent` methods the spec needs, and the "reuse the M2
       U13 fixture contract for the signup/login shape" comment pins
       the shared shape). The spec's *browser-level* assertions pin
       the **observable** surface (the four `Status` badge literals
       `filed / assigned / unlocked / resolved`; the `TempData[
       "info"]` / `["error"]` alerts; the "Review →" queue link; the
       `#assignedToModeratorId` `<select>` + `Assign` button; the
       `Unlock` / `Resolve` action buttons; the C5-flip "second
       render" assertion) — **never** the Core-level `AccessAudit.
       Via` literal (that is U9 test row 3's job at the Core level).
    2. **`docs/design/m3b-moderation.md` — MODIFY.** Appended
       `### Run result (M3b acceptance gate — 2026-09-12)` between
       the *"Part 2 ends here..."* footer and a new *"U11 (the
       M3b close) appends `## M3b — Closed (recorded)`..."*
       trailer, mirroring M3's § `Run result (M3 acceptance gate
       — 2026-09-04)` shape verbatim. The five paragraphs, in
       order:
       - **Command + Testcontainers / CLI note** (CLI `dotnet test`
         returns exit-code 5 in this workspace; the VS Test Explorer
         is the working runner — same as M2 U11 precedent).
       - **Pass counts — verified run, not assumed** (see below).
       - **Three-gate table** (`#` | `Gate` | `Evidence (actual test
         names / lanes U10's spec pins)`) with G1 / G2 / G3 mapping
         to the three new tests in `e2e-m3.spec.ts` + the Core-level
         §2.5 names U9 landed.
       - **E2E status** (both spec files enumerable; `kumunita`
         fixture is a documented throw in both; the M2 D2
         documented-throw **re-records** — the M2 U13 / M3 U10 /
         M3b U10 "documented-throw, not silently re-deferred"
         discipline).
       - **Drift status** (U9's two drift notes carry through
         unchanged; U10 adds one new **plan-documentation** finding
         — see below).

  - **Entry-reads confirmed:**
    - `docs/design/m3b-moderation.md` §2.5 (rows 1–13 are U9's 13
      tests that pass; rows 14–16 are U10's evidence / U7's surface
      tests — **unlanded**, see the "still open" finding below).
      §2.6 (G1 / G2 / G3 + the G3 note on the D2 fixture). §2.7 (the
      drift-guard — "this file is the contract" + the "append a one-
      line drift note" rule).
    - `docs/design/m3-posts-design.md` § `Run result (M3 acceptance
      gate — 2026-09-04)` lines 697–760 — **the shape to mirror
      verbatim** (M3's precedent).
    - `docs/plans-milestones/m3-handoff-notes.md` § `## U10 — gate
      recorded` lines 225–237 — the **same** three-test gate shape
      M3b reuses, with the Core.Tests count updated from M3's
      105/105 (+ 18 M3-pinned + 87 inherited) to M3b's 118/118 (+
      13 M3b-pinned + 105 inherited).
    - `docs/plans-milestones/m2-handoff-notes.md` lines 239–250 (U13
      e2e authored, paused — the documented-throw context; the D2
      deviation) — the M2 D2 context the M3b U10 G3 note references.
    - `tests/Kumunita.Web.Tests/e2e-m2.spec.ts` (the 299-line M2
      spec; the fixture-throw pattern + the 3-test shape U10
      mirrors).
    - `tests/Kumunita.Web.Tests/playwright.config.ts` (`testMatch:
      ['**/*.spec.ts']` — the new `e2e-m3.spec.ts` is picked up
      automatically, no config change needed).
    - `tests/Kumunita.Web.Tests/package.json` (the `@playwright/
      test` 1.62.1 + `typescript` 5.9.3 deps + `npm scripts:
      typecheck/test/test:headed/test:ui` — already scaffolded by
      M2 U13).
    - `tests/Kumunita.Web.Tests/node_modules/` — present on-disk
      (verified by `Get-ChildItem -Recurse`; the `npx playwright
      test --list` command runs cleanly without a `npm install`).
    - `src/Kumunita.Web/Views/Posts/Detail.cshtml` (the resident-
      facing "Report this" card — M3b U8; the reply card — M3b U6
      micro-fix closure) + `src/Kumunita.Web/Views/Moderation/
      Resolve.cshtml` (the F5 / F6 action cards: the `#assigned-
      ToModeratorId` `<select>` in the `assign` form; the `Unlock`
      button in the `unlock` form; the `Resolve` button in the
      `resolve` form) + `src/Kumunita.Web/Views/Moderation/
      Index.cshtml` (the queue — one `<tr>` per `Report` row with a
      `<span class="badge">` Status literal cell and the `Review →`
      `<a href="/moderation/{Id}">` action link) — the **selector
      pins** U10's spec uses.
    - `src/Kumunita.Web/Views/Posts/New.cshtml` (M3 U7 composer —
      the `#component` `<select>`, `#title` `<input>`, `#body`
      `<textarea>` — the form-bound field names U10's spec uses).
    - `src/Kumunita.Web/Controllers/PostsController.cs` (M3 U7 +
      M3b U6 + M3b U8 deliverable — the `Replies` / `Report`
      actions) and `src/Kumunita.Web/Controllers/ModerationControl-
      ler.cs` (U7's delivery of the `/moderation` queue + resolve-
      UI; the `[Authorize]` + `GlobalAdmin` gate per ADR 0003
      §SoD).

  - **Pass counts (verified run, not assumed — 2026-09-12):**
    - `run_tests` filter `Project=Kumunita.Core.Tests` → **118/118
      passed, 0 failed** (13 M3b-pinned from U9 + 105 inherited
      M1/M2/M3; same composition as M3's 105/105 + 18 but with
      different invariants/lanes — `F1`…`F8` replaced by the
      `FileReportAsync_*` / `CanReadWithReportAsync_*` /
      `AssignReportAsync_*` / `ResolveReportAsync_*` /
      `HidePostAsync_*` / `RemovePostAsync_*` / `PostStatus_*`
      rows in §2.5).
    - `run_tests` filter `Project=Kumunita.Web.Tests` → **37/37
      passed, 0 failed** (unchanged since M3 — U7's / U6's /
      U8's Web deliverables are the *controller / view* files, not
      tests; the §2.5 rows 14–16 Web-layer surface tests are
      **unlanded** — see the "still open" finding below).
    - `npx playwright test --list` (from `tests/Kumunita.Web.
      Tests/`) → **6 tests in 2 files** (3 M2 at
      `e2e-m2.spec.ts:156/202/258`; 3 M3b at
      `e2e-m3.spec.ts:200/314/401`). **Enumerability confirmed**;
    - the tests are **not runnable** in this unit — the `kumunita`
      fixture is a documented throw (see below).
    - `run_build` on `Kumunita.Web.Tests.csproj` → green (U10
      touched no C# source file; the two files are a new `.ts` and
      a `.md`).
    - **Total xUnit gate: 155/155 passed, 0 failed** (vs. M3's
      142/142 = 105 + 37; the +13 is U9's 13 M3b seam-test ADDs).
  - **E2E status (the M2 D2 re-record, per the sealed register §
    U10 Exit + design doc §2.6 G3 note + U1's handoff item 6):**
    - The Playwright scaffolding (`package.json`,
      `playwright.config.ts`, `node_modules`, the M2 spec
      `e2e-m2.spec.ts`) is **present** in the repo and **both**
      spec files are **enumerable** (`npx playwright test --list`
      reports **6 tests in 2 files** — the M2 U13 → M3 U10 → M3b
      U10 precedent's "present and enumerable" pin, re-confirmed).
    - The `kumunita` fixture (both `e2e-m2.spec.ts:118` — M2 U13's
      spec, and `e2e-m3.spec.ts` — the U10 new spec — the same
      contract, re-declared with the M3b `signupGlobalAdmin` ADD
      for ADR 0003 §SoD) is a **documented *throw*** in both
      files. The M2 D2 deviation (`m2-directory-profiles-groups.
      md` § `## U13`) is **still open** in this workspace; U10's
      `npx playwright test --list` reports the 6 specs cleanly
      but executing the spec would fail on the `throw` — the
      runtime has not been landed by any M1 / M2 / M3 / M3b unit.
      Per the M2 U13 → M3 U10 precedent ("documented throw, not
      silently re-deferred"), U10 **re-records** the status with
      an explicit note (this section + the design-doc § `E2E status`
      paragraph) rather than silently dropping it.
    - Per U10's Deliverables plan ("2 files: the e2e spec file for
      M3b surfaces; the design doc § Run result section") and the
      sealed register § U10 Exit ("the gate result, e2e pass count
      **or the fixture-throw status if still open**" — U10 takes
      the latter path), U10 **neither implements the fixture nor
      runs the tests** in this unit. The M2 U13 / M3 U10 discipline
      (the runtime unit lands the fixture in M4/M5/M6 and
      records the pass count in a future `### Run result (M3b
      e2e — <date>)` section **above** this handoff section)
      carries over — and **U10's record is the third consecutive
      "documented-throw, not silently re-deferred" entry in the
      M2 → M3 → M3b chain.**
  - **Still-open drift / reconciliations for U11's close (all
    flagged as **not §2.6 drift-pauses**):**
    - **U9's two drift notes carry through unchanged** (the §2.3
      item 3 `AccessVia.Admin` vs U3's `decision.Via` shape; the
      §2.3 item 4 "the lane's own `report.assign` / `report.
      resolve` row" vs U5's `if (decision.Allowed)`-guarded
      lane). U10's `e2e-m3.spec.ts` pins the *observable* surface
      only (the `Status` badge literals, the `TempData` alerts,
      the C5-flip "second render" assertion), **not** the
      `Via` literal the M1-frozen seam writes — U9's shape
      remains the authoritative test-level pin, and U10's
      browser-level assertions do not contradict it.
    - **U10 adds one new plan-documentation finding (not a §2.6
      drift-pause):** §2.5 rows 14, 15, 16 are **unlanded**.
      - Row 14 — `PostReply_Controller_DelegatesToExistingCreate-
        ReplyAsync_NoNewCoreSeam` — the sealed register's own
        § U9 line 1422 explicitly defers rows 14–16 as "tests
        14–16 are U10's / U7's Web-layer surface, *not* U9's
        scope"; U7 shipped the `ModerationController` /
        `PostsController.Replies` / the report-form wiring
        **without** landing these 3 tests (verified by
        `grep` across `tests/Kumunita.Web.Tests/*.cs` for all
        three `§2.5` row 14–16 names — zero hits). U10's G3
        test (`e2e-m3.spec.ts:401`) pins the *reachability*
        of the same surface at the browser level; U11's close
        should record the finding (the M3 U10 precedent's
        "plan-documentation slip, not §2.6 drift-pause" shape)
        and decide (a) U11 lands the 3 tests, or (b) M3b's
        `## Summary` records them as a deferred M4 follow-up
        (with a §2.7 one-line drift note in this file).
      - Row 15 — `ModerationController_QueueRead_ReturnsAll
        ReportsOrderByAtDesc` — the queue read + ordering pin
        (the `At` desc + `Status` desc shape).
      - Row 16 — `ModerationController_ResolvePostAction_
        InvokesResolveReportAsync` — the thin-controller
        delegation pin.
    - **U10's "2-file" deliverable discipline is preserved** —
      U10 did **not** touch any M1 / M2 / M3 source file, did
      **not** add a new xUnit test, did **not** implement the
      `kumunita` fixture, did **not** touch any `package.json`
      or `playwright.config.ts` (both already present from M2
      U13's scaffolding). U10's two Deliverables files are
      exactly those named in the plan §`### U10`. The plan doc
      (`m3b-u10-plan.md`) is the third artifact, per the per-
      unit file convention.
  - **What U11 (the close) needs:**
    - The three-gate table above (G1 / G2 / G3) is the **shape**
      U11's `## M3b — Closed (recorded)` section re-uses (the
      `## Summary` table + the `ARCHITECTURE.md` `Moderation/`
      flip per the sealed register § U11).
    - The "still-open" list: U9's 2 drift notes (carry through)
      + U10's new finding (§2.5 rows 14–16 unlanded). U11's
      call — either land the 3 tests, or defer to M4 with a
      §2.7 one-line drift note, per the sealed register § U11
      "the three gate tests from U10, the `ARCHITECTURE.md`
      `Moderation/` line flip to 'M3b ✓ live', any still-open
      item named explicitly rather than silently dropped."
    - The M2 D2 `kumunita` fixture is **still open** — the M4 /
      M5 / M6 "Playwright runtime" unit lands it and records the
      pass count in a future `### Run result (M3b e2e — <date>)`
      section above this `## U10` note. (Same discipline as the
      M2 U13 → M3 U10 → M3b U10 chain; the third milestone in
      a row to re-record rather than silently re-defer.)

  ## U11 — M3b final: close the loop

  - **Date: 2026-09-12.** The M2 U15 / M3 U12 analog: confirm the
    three-tier contract is mutually consistent and write the line that
    closes M3b. **Doc-only, no build** (register § `### U11` Exit:
    "no build … the `## Summary` is the sole M3b→next-milestone
    handoff artifact").
  - **Deliverables (three files, in order — per the sealed register
    § U11 Deliverables + the per-unit plan-file convention):**
    1. **`docs/plans-milestones/m3b-u11-plan.md` — NEW.** This unit's
       execution plan (the per-unit file convention held by U3–U10).
    2. **`docs/design/m3b-moderation.md` — MODIFY.** Appended
       `## M3b — Closed (recorded)` **below** the `### Run result
       (M3b acceptance gate — 2026-09-12)` section (whose own
       trailer — lines 1102–1105 — anticipated this exact append).
       Mirrors M3 U12's close shape
       (`m3-posts-design.md § ## M3 — Closed (recorded)`): the three
       gate tests (G1 / G2 / G3, re-stated from U10's run-result
       table), the `ARCHITECTURE.md` `Moderation/` flip to **M3b ✓
       live**, the still-open items named explicitly (not silently
       dropped), a §2.7 drift-note for the deferred Web-layer tests,
       and the reconciled U9 drift notes.
    3. **`docs/ARCHITECTURE.md` — MODIFY.** §2 tree line 86
       (`Moderation/`) flipped from "M3b — not yet created …" to the
       **M3b ✓ live** phrasing used on the M2/M3 lines (82 / 83):
       the `ModerationService` (file/assign/unlock/resolve + the
       `Via = Report` branch) + `PostStatus` + the hide/remove lanes +
       the `/moderation` queue/resolve UI — live; 3 gate tests +
       13 M3b-pinned unit specs 0-failed; see
       `design/m3b-moderation.md § Run result (M3b acceptance gate
       — 2026-09-12)` + `§ M3b — Closed (recorded)`. The §2 line-26
       value-chain table already names "M3 posts, components,
       moderation" (its "a report links to a moderator" cue is the
       M3b lane) — left as-is; the §3 "Feature modules — … Moderation"
       bullet already names `Moderation` — left as-is.
    4. **`docs/plans-milestones/m3b-handoff-notes.md` — this section
       + `## Summary` below appended** (the register § U11
       Deliverable #2 "a table of shipped units U1–U10 with test
       counts + deviations + any remaining deferred items").

  - **Three-tier consistency re-confirmed (the Close's whole point):**
    - **Register ↔ Design:** the register's § U11 Deliverables map 1:1
       onto the design doc's `### Run result (M3b acceptance gate —
       2026-09-12)` (G1 / G2 / G3) + the design doc's own trailer
       ("U11 … appends `## M3b — Closed (recorded)` below this
       section"). The three-tier contract — the sealed register,
       the design doc's pinned gate table, and the handoff-note
       `## Summary` — is mutually consistent.
    - **Design ↔ Implementation:** the four `ModerationService`
       write lanes + the `Via = Report` read branch
       (`src/Kumunita.Core/Moderation/ModerationService.cs`) match
       the §2.2.3 / §2.4 pins verbatim (`Task<int> FileReportAsync(string postId, string actorId, string? reason, IDocumentSession)`;
       `AssignReportAsync` / `UnlockAsync` / `ResolveReportAsync` all
       GlobalAdmin-gated + `SetComponentModeratorAccessAsync`
       flag-flip); the `PostStatus` enum + the two hide/remove lanes
       match §2.2.1 / §2.2.2 (`HidePostAsync` / `RemovePostAsync`,
       `PostStatus.Active` default — ADR 0004 §B.1 additive).
    - **Test ↔ Evidence:** U9's 13 tests (8 `ModerationServiceTests`
       + 5 `PostServiceTests`) are the Core-level evidence for the
       design doc's § `Run result (M3b acceptance gate — 2026-09-12)`
       three-gate table (G1 / G2 / G3). U10's e2e spec
       (`tests/Kumunita.Web.Tests/e2e-m3.spec.ts`) re-states the same
       three gates at the browser level against the six M3b FACES
       rows.
  - **Reconciliations made by U11 (recorded in the design doc's
    `## M3b — Closed (recorded)`; this section holds a pointer to it):**
    - **U9's two drift notes** — the §2.3 item 3 `AccessVia.Admin`
      vs U3's `canasync`→`decision.Via` shape, and the §2.3
      item 4 "the lane's own `report.assign` / `report.resolve`
      row" vs U5's `if (decision.Allowed)`-guarded lane — are
      **kept as-is** (the §2.7 "this file is the contract" holds).
      The Close's `### Still-open` item 3 names them, with the
      "design doc wins for the pin" reconciliation (the test *names*
      in §2.5 are the authoritative shape; U9's test *bodies* adapt
      to what the M1-frozen seam writes).
    - **U10's finding (§2.5 rows 14–16 unlanded)** — the three
      Web-layer surface tests for `POST /posts/{id}/replies` (U6)
      and the `/moderation` queue + resolve UI (U7) are
      **deferred to M4** (named in the Close as deferral item #2).
      U11 does **not** land them (its two Deliverables per the
      register are the two doc modifications; no new C# test file
      is in U11's scope).
    - **M2 D2 `kumunita` fixture documented-throw** — **still open**;
      the Close names it (deferral item #1) as the M4 / M5 / M6
      Playwright-runtime unit's responsibility (the M2 U13 → M3 U10
      → M3b U10 → U11 chain — the third consecutive milestone's
      "documented-throw, not silently re-deferred" discipline, now
      the fourth entry with U11's own record).
  - **What M4's U1 should read first:** the `## Summary` table below
    (U1–U11 shipped units + test counts + deviations + the
    M4-deferral list), then the design doc's `## M3b — Closed
    (recorded)` for the reconciled still-open items, then the
    register's § U10 / U11 entry-reads.
  - **Files touched (three, exactly the sealed register § U11
    Deliverables + the per-unit plan-file convention):**
    - `docs/plans-milestones/m3b-u11-plan.md` (new)
    - `docs/design/m3b-moderation.md` (modified — `## M3b — Closed
      (recorded)` appended)
    - `docs/ARCHITECTURE.md` (modified — `Moderation/` line flipped)
    - `docs/plans-milestones/m3b-handoff-notes.md` (this section +
      `## Summary` appended)
  - **Build / test state:** **no build** (register § U11 Exit).
    U9's `Kumunita.Core.Tests` 118/118 + U10's `Kumunita.Web.Tests`
    37/37 (= **155/155**) are the pass criterion recorded in U10's
    design-doc gate; they are re-stated **in U11's Close**, not
    re-verified by U11's run. `run_build` on `Kumunita.Core` /
    `Kumunita.Web` / `Kumunita.Core.Tests` / `Kumunita.Web.Tests`
    confirmed **green** in this session (the only workspace error is
    the known U10-documented TS type-check on the Playwright-
    runtime-throw spec — `@playwright/test` resolution — which is
    the M2 D2 fixture-throw status re-recorded above, not a U11
    regression).

  ## Summary

  M3b — Moderation (report workflow, `Via = Report`, `Post.Status`,
  reply route, e2e) — is closed. This table is the sole M3b→next-
  milestone (M4) handoff artifact: one row per shipped unit, its
  one-liner goal, its test count (if any), and any deviation or
  deferral the unit surfaces for M4's U1 to read first.

  | Unit | One-liner goal | Test count | Deviations U11 surfaces |
  |---|---|---|---|
  | U1 | Design doc Part 1 — 4 invariants (C-M3b·1..4), 6 FACES rows (F1–F6), Drift-guard Part 1, "What U2 must pin" | — (design-only) | None — the Part 1 count reconciliation (4 + 6, no C-M3b·5+) is U2's job (done in U2). |
  | U2 | Design doc Part 2 — the 16 pinned seam-test names (§2.5 rows 1–16), the three-test acceptance gate (§2.6 G1/G2/G3), §2.7 drift-guard | — (design-only) | None. Confirms the 4 invariants / 6 FACES / 16 test names + four numbered pins (§2.3) + one read branch (§2.4); no new `Via` / `Status` / `AccessAction` literal introduced. |
  | U3 | `PostStatus` enum + `Post.Status` ADD (ADR 0004 §B.1 additive) + `HidePostAsync` / `RemovePostAsync` write lanes on `PostService` | 0 new (U9 adds rows 9–13 of §2.5) | **U9 drift note 1** — §2.3 item 3 `AccessVia.Admin` pin vs U3's `canasync`→`decision.Via` shape. **Reconciled in U11** (design-doc win): U9's tests assert the *observable* lane (the `PostStatus.Hidden` / `PostStatus.Removed` flip + the `moderate` / `Allowed` outcome against the M1 seam's own `AccessAudit` row), not the specific `Via` literal the M1 seam writes. No test rename; no pin re-pinning. |
  | U4 | `ModerationService.FileReportAsync` (new bounded context `Kumunita.Core.Moderation`) — C-M3b·1 (F1) resident-facing intake write lane | 0 new (U9 adds rows 1–2 of §2.5) | None — matches §2.2.3 verbatim; the `AccessVia.Admin` + `"filed"` Status pins honored; §2.3 item 4 (no partial write — one `SaveChangesAsync`). |
  | U5 | `ModerationService` complete — `AssignReportAsync` / `UnlockAsync` / `ResolveReportAsync` (C-M3b·4 / F5 / F6 / SoD) + the `Via = Report` read branch `CanReadWithReportAsync` (C-M3b·2 / F2) | 0 new (U9 adds rows 3–4, 7–8 of §2.5) | **U9 drift note 2** — §2.3 item 4 "the lane's own `report.assign` / `report.resolve` row" vs U5's `if (decision.Allowed)`-guarded lane. **Reconciled in U11** (design-doc win): on Allow, U5 *does* write the lane's own `AccessAudit` row additionally; on Deny, only the M1-frozen seam's `CanAsync` `moderate` / `Deny` row commits (U9's tests 5 / 8 assert that visible outcome). |
  | U6 | `POST /posts/{id}/replies` route micro-fix — thin `[HttpPost]` action on `PostsController` delegating to the frozen M3 U6 `PostService.CreateReplyAsync` | 0 new | None for the pinned lane. **§2.5 row 14 unlanded** (U10's finding, U11 defers to M4): `PostReply_Controller_DelegatesToExistingCreateReplyAsync_NoNewCoreSeam` is not present in `tests/Kumunita.Web.Tests/`. U11's drift note: "deferred to M4; M4's U2 / U6 / U7 equivalent will pin or land it under the same exact name — §2.5's authority (the name) + §2.7 hold; **not** a §2.6 drift-pause." |
  | U7 | `/moderation` queue + resolve UI + assign form — `ModerationController` (5 routes: GET Index, GET Resolve, POST Assign/Unlock/ResolvePost) + `Moderation{Queue,Resolve}ViewModel` + Razor views `Index.cshtml` / `Resolve.cshtml` | 0 new | **§2.5 rows 15 + 16 unlanded** (U10's finding, U11 defers to M4, same shape as U6/row-14): `ModerationController_QueueRead_ReturnsAllReportsOrderByAtDesc` + `ModerationController_ResolvePostAction_InvokesResolveReportAsync` are not present in `tests/Kumunita.Web.Tests/`. U11's drift note: "deferred to M4; same §2.5 name-pin + §2.7 hold; not a §2.6 drift-pause." |
  | U8 | "Report this post" resident-facing action — `[HttpPost] Report` on `PostsController` (delegating to the frozen U4 `ModerationService.FileReportAsync`) + a small "Report this post" form on `Views/Posts/Detail.cshtml` | 0 new | None for the C-M3b·1 intake lane; **no §2.5 row unlanded here** (U8's Web action is not itself a §2.5 row-14/15/16 target — the register's § U9 line 1422 + U10's finding scope rows 14–16 to U6 / U7's Web surfaces). |
  | U9 | 13 pinned `[Fact]`s: rows 1–8 in `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` (new) + rows 9–13 in `tests/Kumunita.Core.Tests/PostServiceTests.cs` (additions) | **13** (13 discovered, 13 executed, 13 passed, 0 failed — verified run) | **(a) U9 drift note 1** (see U3 row). **(b) U9 drift note 2** (see U5 row). Both reconciled in U11's `## M3b — Closed (recorded)` Close section (the design doc's close). Full `Kumunita.Core.Tests` 118/118 pass; no regressions from the M1 / M2 / M3 baseline. |
  | U10 | `tests/Kumunita.Web.Tests/e2e-m3.spec.ts` (new — 3 specs: closed-loop, handoff, per-lane) + `### Run result (M3b acceptance gate — 2026-09-12)` appended to the design doc | **Gate: 3-test shape (all PASS)** · unit-suite: **155/155** (118 Core + 37 Web) | **(a) M2 D2 `kumunita` fixture documented-throw, still open** (the M2 U13 / M3 U10 / M3b U10 chain, now U11-reaffirmed); the *unit-suite* evidence is the pass criterion recorded in this milestone. **(b) §2.5 rows 14–16 unlanded** finding (U11 defers to M4, same discipline). **(c) Plan-documentation slip U10 flags** (the register's § U9 line 1422 anticipated the deferral: "tests 14–16 are U10's / U7's Web-layer surface, *not* U9's scope") — not a §2.6 drift-pause. |
  | U11 | This close: `## M3b — Closed (recorded)` appended to the design doc (the three-gate table + the `ARCHITECTURE.md §2 Moderation/` flip + the still-open list + the M4 deferral list); `ARCHITECTURE.md §2 Moderation/` line flipped to **M3b ✓ live**; `## Summary` table in this file; no build | — (docs only, no build) | (a) **§2.5 rows 14–16 — deferred to M4** (named above; not landed by U11, not silently dropped). (b) **M2 D2 `kumunita` fixture — still open** (named above; the M4 / M5 / M6 Playwright-runtime unit lands it and records the pass count in a future `### Run result (M3b e2e — <date>)` section above the Close). (c) **Both U9 drift notes reconciled** (design doc wins for the pin; U9's shape is the observable record; §2.7 "this file is the contract" holds). (d) **Plan-documentation slip (U10 → U11, not a §2.6 drift-pause)** — same shape as U11 (M3)'s own close at `m3-posts-design.md § ## M3 — Closed (recorded)` line 764. |

  ### M4 deferral list (each named, each with a next-owner cue)

  1. **E2E `kumunita` fixture (M2 D2, now U11 re-affirmed).** M4's
     Playwright-runtime unit (whichever M4 unit owns it — U6 / U7 /
     U8, per the sealed register § U10 "the runtime unit lands the
     fixture in M4/M5/M6") either **implements the fixture** (the
     contract is pin-shaped; see `tests/Kumunita.Web.Tests/e2e-m3.
     spec.ts`'s `kumunita` fixture contract block) and then runs
     `npx playwright test` against the 6 specs (3 M2 + 3 M3b) and
     records the pass count in a **subsequent**
     `### Run result (M3b e2e — <date>)` section of the design doc
     + a matching note in this file (above or below this § Summary,
     U11's call), **or** re-affirms the documented-throw status
     again (M5 / M6).
  2. **§2.5 rows 14–16 (Web-layer surface tests) — deferred.**
     `tests/Kumunita.Web.Tests/PostsControllerTests.cs` (new file,
     M4) + `tests/Kumunita.Web.Tests/ModerationControllerTests.cs`
     (new file, M4) — the 3 tests by their §2.5 pinned names, in
     exactly the row order (14, 15, 16). The sealed register §
     `### U9`'s "the 13 pinned-test list from §2.5" phrasing covers
     **rows 1–13 only**; U9 / U10 / U11 have been explicit that
     rows 14–16 are a future Web-layer test unit's scope (U11's
     drift note names this deferral in the `## M3b — Closed
     (recorded)` Close section, the §2.7 hold + the §2.6 drift-
     pause distinction).

