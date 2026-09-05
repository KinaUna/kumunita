# Design Doc — M3b: Moderation (report workflow, `Via = Report`, `Post.Status`, reply route, e2e)

> **Part 1 of 2 (U1 — doc-only, no build).** Part 1 pins **scope** (the six-item
> M3b deferral list = M3b's complete in-scope, nothing else), **invariants**
> (the new `C-M3b·1..4` + which ADR 0006 / ADR 0001-B / ADR 0003 clause still
> holds), and the **FACES table (pinned count: 6)**. Part 2 (U2) will append
> `## Seams & contracts (Part 2, written by U2)` with the exact C# shapes
> U3–U10 must implement against (the four `ModerationService` method
> signatures; the `Via = Report` read-lane shape; `PostService.HidePostAsync`
> / `RemovePostAsync`; the `PostStatus` enum), the **pinned seam-test names**
> (U9's test list), the **three-test acceptance gate** (closed-loop /
> handoff / part-vs-whole), and the **drift-guard** — mirroring M3's Part 2
> (`docs/design/m3-posts-design.md` § `## Seams & contracts (Part 2, written
> by U2)`). Part 1 holds the invariant / FACES numbers Part 2's test names
> anchor to.

## Context

M1 shipped the access model and the `Moderate` / `Via = Report` vocabulary —
**reserved, never exercised**. M3's F3/F8 are the "absence" tests that prove
the vocabulary is dormant (a moderator sees nothing beyond the author's
audience, even with `ModeratorAccess` flagged ON, because M3 ships no
`Moderate`-on-post call and no `Via = Report` branch).

M3b closes the gap M3 deliberately left open. The scope is the six-item deferral
list M3 recorded in `docs/design/m3-posts-design.md` § `## M3 — Closed (recorded)`
→ `### M3b deferral list`, mirrored verbatim in `docs/plans-milestones/
m3-handoff-notes.md` § `## Summary` (U11's close) and in the plan's own
Assumptions. That list **is** M3b's in-scope. Nothing else — events, projects,
notifications, export/iCal/federation/MCP/API — **is not M3b's**; U11 closes
that explicitly in `## M3b — Closed (recorded)`.

M3b is also the **first milestone that exercises the reserved M1 vocabulary**:
`AccessAction.Moderate` (id `"moderate"`) and `AccessVia.Report`. U1 confirms
both exist as of M3 (`src/Kumunita.Core/Authorization/AccessAction.cs`,
`Component.cs` `ModeratorAccess`) and that no new ids are needed to ship
M3b's lanes.

## Scope

**Complete in-scope (the six M3b deferral items, verbatim, in M3's order):**

1. **Report workflow — file / assign / unlock / resolve.** The `Report`
   *table* is registered (M3 U3, 7 fields, `Status` nullable, no index, no
   surface, no tests); M3b owns the *workflow*: the `FileReportAsync` /
   `AssignReportAsync` / `UnlockAsync` / `ResolveReportAsync` command surface
   + the assignment UI + the resolve-UI.
2. **The `Via = Report` read branch on a post.** A filed report in a post's
   component unlocks that post **for a standing moderator with
   `ModeratorAccess` scope on that component** (M1 branch #2) — the C5
   carve-out (the pin F3 / F8's "absence" tests name) goes live. The
   resident who files the report is **not** the viewer; the viewer is the
   standing moderator (C-M3b·2, F2).
3. **Moderator surfaces — the queue, the resolve UI, the "assign to a
   moderator" form.** `/admin` (M1's admin surface) is **unchanged** in M3
   (ADR 0003 SoD pins this — U7 confirms it is not touched again).
4. **The `Post.Status` field (hidden / removed) and the M3b removal path.**
   M3's `Post` POCO has **no** `Status` column (registered *absent*, not
   nullable-not-set). M3b adds the enum + the two `Moderate`-gated write
   lanes + the C5 `Moderate` action it exercises.
5. **The reply `POST /posts/{id}/replies` route** — M3's 4-route set (3 GET +
   `POST /posts/new`) does not include it; M3's `Detail.cshtml` links to it
   and it currently 404s. The Core write lane
   (`PostService.CreateReplyAsync`, M3 U6) is present and the *only* write
   seam (C3's single-write pin) — M3b's route is a micro-fix (controller
   action + view only), no new Core seam, no new seam-test name.
6. **The E2E spec (`e2e-m3.spec.ts`).** The Playwright scaffolding is present
   and enumerable; M2's D2 fixture-throw is still open. M3b's U10 either
   authors + runs the spec and records the pass count, or re-records the
   documented-throw status (does not silently re-defer without a note).

**Out-of-scope (M3b's M1-style close, verbatim from M3's Part 1 "Out of
scope" block — nothing else is in M3b):**

- Export, iCal, federation, MCP/API (as in M1 / M2 / M3).
- Events, projects, notifications (M4/M5/M6 per M1's original OOS close).
- M3's own resident-visible surfaces (feed / detail / composer) — **unchanged**
   by M3b except where M3b adds on top (the report action, the `Via=Report`
   branch, the `Post.Status` render, the reply route). M3's 10 pinned FACES
   (F1–F10, `m3-posts-design.md` § FACES) **still bind** for every feed /
   detail render in M3b; M3b's FACES (F1–F6 below) pin the six new
   M3b-added outcomes, not the six M3 ones.
- The M3 design-doc's `## M3 — Closed (recorded)` and `### M3b deferral
  list` sections — **unchanged**; M3b's close is its own
  `## M3b — Closed (recorded)` section in *this* doc.

**Surfaces (M3b-added, resident- and moderator-visible):**

1. `POST /moderation/reports/file` (resident-facing) — the report-filing
   action, wired to a post/reply view surface.
2. `POST /moderation/reports/assign` (GlobalAdmin-gated) — the "assign to
   a moderator" form.
3. `POST /moderation/reports/unlock` (GlobalAdmin-gated) — the
   report-driven unlock of the `ModeratorAccess` flag.
4. `POST /moderation/reports/resolve` (GlobalAdmin-gated) — the resolve-UI.
5. `/moderation` — the moderated queue (read over `Report` ordered
   `At` desc + `Status` desc) + the resolve-UI + the assign-form, mirroring
   `AdminController`'s / `DirectoryController`'s thin-controller shape.
6. `POST /posts/{id}/replies` — the M3 404 (reply route) closed by a
   thin controller action delegating to the existing `CreateReplyAsync`.

## Invariants (pinned for M3b)

M3b is a *caller* of the ADR 0006 invariants (not an owner), **plus** the
first caller to actually *exercise* two of the reserved M1 vocabulary
entries (`AccessAction.Moderate`, `AccessVia.Report`) and the
`ModeratorAccess` flag-flip seam. M3b owns **four** new invariants
(`C-M3b·1..4`) — the four behavioral rules M3b is the *first* milestone to
need: the resident-facing report-filing lane, the
`Via = Report` read branch, the `Moderate`-gated write lane (hide / remove),
and the SoD on assign.

| # | ADR 0006 / M3b pin | How M3b uses it |
|---|---|---|
| **C-M3b·1** (M3b-owned) | **Report filing is a resident-facing intake action, not an access decision.** A resident who can currently *see* a post (their `Read` decision is `Allowed`) may file a report against it. Filing needs **no** `CanAsync` call (it is an *intake* action, analogous to M1's `UpsertProfileAsync` — a write seam, not an access decision). Filing **does** append an `AccessAudit` row (a write lane), but the row's `Via` tag is the **filing tag** (pinned by Part 2 §2.3, C-M3b·1) — **not** `Via = Report` (reserved for the *read* branch, C-M3b·2), and **not** `Via = Owner` (that is the C1 owner branch). The exact tag / `AccessVia` literal is U2's Part 2 §2.3 pin; U1 pins only the two negatives here. | The report-filing action (F1) is the first M3b write lane that **does not need** a `CanAsync` authorization decision — it is an *intake* action, not an *access* decision. The audit row's `Via` tag is the pinned filing tag (Part 2 §2.3), distinct from `Via = Report` (the read branch, C-M3b·2). The "filing ≠ read branch" split is U1's pin; U2's Part 2 §2.3 carries the exact tag. |
| **C-M3b·2** (M3b-owned) | **`Via = Report` read branch.** A **filed report** in a post's component unlocks that post **for a standing moderator with `ModeratorAccess` scope on that component** (M1 branch #2, the C5 carve-out U7 will exercise) — the post's own `Read` decision is no longer the sole gate; the *filed report* is the gate. The `AccessAudit` row records `Via = Report` with the **acting identity** (the standing moderator, not the resident who filed). The resident who files the report is **not** the viewer on this branch — *filing* (C-M3b·1) and *unlock via filing* (C-M3b·2) are two distinct lanes. This is the C5 carve-out going live — the **exact inverse** of M3's F3 / F8 "absence" tests (a `Moderate`-holding viewer sees nothing beyond the author's audience). | The `Via = Report` branch (F2) is the M3b read-lane addition: it is either a new method on `ModerationService` (e.g. `CanReadWithReportAsync`) **or** a direct branch on `AuthorizationService.Decide` — U2's Part 2 §2.4 pin decides which (the *thinner lane* per the M3 handoff, the M3 deferral list item 2's candidate wording). ADR 0006-E "compatible lane" applies (M3b's close — U11 — grows M1's *named here* list by exactly one line). A `Moderate`-holding viewer with **no** filed report in the post's component still sees nothing (C5, unactivated) — the branch is triggered by the *filed report*, not by the `Moderate` action alone. |
| **C-M3b·3** (M3b-owned) | **`Moderate`-gated write lane, same-transaction, no partial writes.** `HidePostAsync` / `RemovePostAsync` (the two moderation write lanes): both call `IAuthorizationService.CanAsync(actorId, AccessAction.Moderate, target, idDocumentSession)` **before** writing, in the **same** `IDocumentSession` transaction as the domain write (C3, ADR 0006-C: audit always on — Allow *and* Deny); a denied call is **not executed at all** (no `Hidden` status write, no `Removed` status write, no partial state). `Post.Status ∈ { Active, Hidden, Removed }` is the *only* state the write lane touches — **additive** (ADR 0004 §B.1), not a breaking reshape. Both lanes are `Moderate`-gated (a non-moderator caller is denied; the `AccessAudit` row records Deny). | The two moderation write lanes (F3 / F4) are the **first** code path that ever passes `AccessAction.Moderate` into `CanAsync`. The `Post.Status` enum is the **single M3b ADD** on the `Post` POCO (ADR 0004 §B.1 additive, no migration, no re-seed). A non-moderator caller is denied (C3, ADR 0006-C) — the audit row records Deny with the acting identity and the pinned `Via` tag. The **same** `IDocumentSession` overload (M1's §E lane) is the *only* way the write lane and its audit row commit atomically. |
| **C-M3b·4** (M3b-owned) | **SoD on assign (ADR 0003):** only a **GlobalAdmin** can invoke `AssignReportAsync` (the "assign to a moderator" form). A **Moderator** caller is **denied** (the `AccessAudit` row records Deny; the write is **not executed**). The `ModeratorAccess` flag-flip (`IUserInfoService.SetComponentModeratorAccessAsync` — M1, unchanged, GlobalAdmin-gated) is a **separate** seam: the assign lane (F5) does **not** call it directly; the flag-flip is the **unlock / resolve** lane's (F6) job — the "report-driven unlock" that activates the C5 carve-out for the standing moderator on subsequent renders (not the F5 render). `/admin` (M1's admin surface) is **unchanged** in M3b (ADR 0003 SoD pin; U7 confirms it is not touched). | `AssignReportAsync` (F5) is GlobalAdmin-gated (SoD). A Moderator caller is denied (C3, ADR 0006-C). **The `ModeratorAccess` flag-flip** (`SetComponentModeratorAccessAsync`, M1, unchanged, GlobalAdmin-gated) is a **separate** seam — M3b's resolve / unlock lane (F6) calls it (the "report-driven unlock"), and it commits in the **same** `IDocumentSession` transaction as the report's `Status` write (C3, ADR 0006-C). A non-GlobalAdmin caller is denied; the audit row records Deny. **`/admin` is unchanged** (ADR 0003 SoD, M1's admin surface). |

**ADRs M3b must keep holding (pinned in the invariant rows above, and not
violated elsewhere):**

- **ADR 0006 C1** (C5 carve-out, default-OFF) — still binding: a
  `Moderate`-holding viewer sees **nothing** on a post the author has not
  shared with them *unless* a filed report is present (C-M3b·2, the
  "filed report" gate).
- **ADR 0006 C3** (audit always on — Allow *and* Deny) — still binding:
  every M3b write lane (filing, hide, remove, assign, resolve) and read
  branch (`Via = Report`) commits an `AccessAudit` row in the same
  `IDocumentSession` transaction (C-M3b·1/2/3/4).
- **ADR 0006 C4** (strong-consistency membership resolution against live
  documents) — still binding: a membership change (a group add, an
  audience-grant add, a `ModeratorAccess` flag-flip) takes effect on the
  **very next** render — no projection in the access path (M3's F5 e2e pin
  still holds in M3b).
- **ADR 0006 C6** (one matching pass — `MatchGroups` — shared by `CanAsync`
  and `CanSeeAsync`) — still binding: the `Via = Report` branch (C-M3b·2)
  **is** the single matching pass; it cannot drift from the owner /
  audience / delegation passes.
- **ADR 0001-B** (author's choice is absolute) — still binding: the
  composer writes the chosen `Audience` verbatim; M3b does **not**
  auto-add group grants, does **not** second-guess a "Safety only" choice
  with a "but the author is friendly" branch, and does **not** inject a
  community-wide audience (the C1 owner branch is the only "extra" the
  author gets).
- **ADR 0004 §B.1** (Marten-native document registration, POCOs,
  conventional `string Id`, delta-detected, idempotent, no seeding) —
  still binding: the `Post.Status` field (C-M3b·3, the single M3b ADD on
  `Post`) is additive (delta-detected, idempotent, no re-seed).
- **ADR 0003 §Separation of duties** — still binding: a `GlobalAdmin`
  calling `AssignReportAsync` is allowed (C-M3b·4); a Moderator caller is
  denied (C3, ADR 0006-C). `/admin` is unchanged (M3's ADR 0003 SoD pin
  still holds — U7 confirms).
- **ADR 0006-E** (compatible lane) — the `Via = Report` branch (C-M3b·2) is
  the **single M3b ADD** on the authorization surface (ADR 0006-E "named
  here" list grows by exactly one line in M3b's close — U11). The
  `Post.Status` field (C-M3b·3) is a Core POCO change (ADR 0004 §B.1), not
  an authorization-surface change.
- **ADR 0006-D** (dependency direction) — still binding: `ModerationService`
  (Core) composes **only** `IUserInfoService` + `IAuthorizationService` +
  its own Marten session (C3's single-write, ADR 0006-E compatible lane);
  the `Kumunita.Web` layer is a thin controller (mirroring M2's
  `DirectoryController` shape, M3's `PostsController` shape).

**ADRs NOT pinned for M3b** (explicit): C-M3·1 / C-M3·2 / C-M3·3 (M3's
three owned invariants — reply-inheritance, component-candidate isolation,
feed/detail audit-shape) **still bind unchanged** for every M3 surface M3b
renders on (the queue is a read over `Report`, not a feed render;
`Via = Report` does not reshape the feed shape). M3b does **not** re-pin
C-M3·*, does **not** add C-M3·4+, and does **not** renumber M3's invariants
mid-M3b (the drift-guard below applies — this doc wins on conflict).

## FACES (pinned, 6)

Each row is a *resident-visible* or *moderator-visible* outcome (or a
*moderator-absence* outcome) that M3b's seam tests (Part 2 §2.5) and the M3b
e2e (U10) must cover. "Pinned" means the invariant in the right column is the
single authority for the outcome; the test names Part 2 pins must reference
that pin by id.

| # | Outcome (what a resident / moderator sees) | Pinned by |
|---|---|---|
| C-M3b·1 (filing lane — resident-facing intake, no authz call; the `Via` tag is part-2-pinned, part 1's two negatives only: not `Via=Report`, not `Via=Owner`) |
| F2 | A `Moderate`-holding moderator with a *filed report* on a post in their component **can read** that post — the post's own `Read` decision is no longer the sole gate; the *filed report* is the gate. The `AccessAudit` row records `Via = Report` with the acting identity (the moderator). A `Moderate`-holding viewer with **no** filed report in the post's component still sees nothing (C5, unactivated) — the branch is triggered by the *filed report*, not the `Moderate` action alone. | C-M3b·2 (`Via = Report` read branch, C5 carve-out live; ADR 0006-E compatible lane) |
| F3 | A `Moderate`-holding moderator who calls `HidePostAsync` on a post in their scope: the post's `Status` flips to `Hidden`; the `AccessAudit` row records the write outcome with the pinned **write-lane `Via` tag** (Part 2 §2.3, C-M3b·3) and the acting identity. A non-moderator caller is **denied** (C3, ADR 0006-C); the audit row records Deny, and the write is **not executed at all** (no partial state). | C-M3b·3 (hide lane, `Moderate`-gated, same-transaction, no partial write; the `AccessVia` tag is part-2-pinned, not a literal in part 1) |
| F4 | A `Moderate`-holding moderator who calls `RemovePostAsync` on a post in their scope: the post's `Status` flips to `Removed`; the `AccessAudit` row records the write outcome with the pinned **write-lane `Via` tag** (Part 2 §2.3, C-M3b·3) and the acting identity. A non-moderator caller is **denied** (C3, ADR 0006-C); no partial write. **`/admin` is unchanged** (M1's admin surface; ADR 0003 SoD; U7 confirms it is not touched). | C-M3b·3 (remove lane) + ADR 0003 SoD (`/admin` unchanged) |
| F5 | A **GlobalAdmin** who calls `AssignReportAsync` on a report: the report's `Status` flips to `Assigned` (the pinned assign state, Part 2 §2.3); a `ModeratorAssignment` row is written (the standing-moderator's id, the report's `ComponentId`, the granting `GlobalAdmin` id as `GrantedBy`, the `At` timestamp). A **Moderator** caller is **denied** (ADR 0003 SoD; C3 audit row records Deny). The `ModeratorAssignment` POCO is M1's existing type (U5 confirms; U1 does **not** add or reshape). | C-M3b·4 (assign lane, GlobalAdmin-gated, SoD; `ModeratorAssignment` is M1's existing POCO — not a new M3b ADD) |
| F6 | A **GlobalAdmin** who calls `UnlockAsync` / `ResolveReportAsync` on a report: the report's `Status` flips to `Resolved` (pinned, Part 2 §2.3); the report's `ComponentId` → the component's `ModeratorAccess` flag is flipped (via the existing `IUserInfoService.SetComponentModeratorAccessAsync` seam — M1, **unchanged**, GlobalAdmin-gated) **in the same transaction** (C3 — same-transaction as the report's `Status` write). A non-GlobalAdmin caller is **denied** (C3 row records Deny). **This is the C5-activation event**: the flag-flip is what enables the `Via = Report` read branch (C-M3b·2) for the **standing moderator** on **subsequent** renders — not the same render (F6's audit row does not include the `Via = Report` read decision; that decision is C-M3b·2's, the *next* render). | C-M3b·4 (unlock / resolve lane, flag-flip via the M1 seam — the **only** way to flip `ModeratorAccess`, C-M3b·4's "separate seam" pin; ADR 0006-C same-transaction) |

> **F6 note for U2:** the F6 row's "C5-activation event" is a **semantic
> note** (the flag-flip *enables* the read branch), not a new invariant pin
> or a new ADR clause. U2's Part 2 pins the *exact* flag-flip call (the
> `SetComponentModeratorAccessAsync` seam, M1, unchanged) and the *exact*
> `Status` value the resolve lane writes to the `Report` doc (pinned in
> §2.2/§2.3). The invariant ids in the FACES right-column are the *single
> authority* for each row — Part 2's test names reference the id, not the
> prose in this table.

**FACES count: 6.** This count (and the invariant pin per row) is the input
the next unit (U2) needs to name the seam-test list and the acceptance gate
without re-deriving them. A **new** FACES row (F7+) is added **only** by a
unit that ships the outcome it pins, in the same commit as the feature —
the count is a **handoff field** (U1 → U2, and forward), exactly as in M3's
Part 1.

## Drift-guard & change policy (Part 1)

- If a later unit (U2–U11) finds a mismatch between an implemented signature
  and the pin in **this** Part, **this doc wins**. The unit updates this file
  in the same commit and appends a one-line drift note to
  `docs/plans-milestones/m3b-handoff-notes.md`.
- The invariant *numbers* — the four M3b-owned **C-M3b·1..4**; ADR 0006's
  **C1, C2, C3, C4, C5, C6** (still binding, re-pinned in the "ADRs M3b must
  keep holding" list above); ADR **0001-B**; ADR **0003 §SoD**; ADR
  **0004 §B.1** — are **stable for the rest of M3b**. Renaming or
  renumbering an existing one is a **breaking change** and is **not allowed
  mid-M3b**. Adding a new M3b-owned invariant (C-M3b·5+) requires an ADR
  amendment plus a design-doc edit in the same commit.
- A new FACES row (F7+) is added only by a unit that ships the outcome it
  pins, in the same commit as the feature. The FACES count is a
  **handoff field** (U1 → U2, and forward): every unit that touches FACES
  updates the count in the handoff note.
- **Seam/test names are not pinned in Part 1.** Every test name and the
  three-test acceptance gate (closed-loop / handoff / part-vs-whole) land in
  Part 2 (U2) at §2.5 / §2.6 — mirroring M3's structure. The FACES table
  above is the *input* to Part 2; Part 2's test names are the *output*.
- **The plan § U1 "FACES table (pinned count)"** vs. **this doc's FACES
  "pinned, 6"** is a **plan-documentation clarification**, not a
  pinned-invariant drift (M3's U1 had the same slip: 12 in the plan headline
  vs. 11 in the body — M3's Part 1 flagged it, M3's U2 confirmed and pinned
  11). M3b's U2 confirms **6** (F1–F6) against this body and pins the
  §2.5 test names against the invariant ids in the right column.
- **The `Via` tag for the filing lane is a Part 2 §2.3 pin** (C-M3b·1).
  Part 1 (this doc, U1) pins *only* that the filing tag is **not** `Via =
  Report` (reserved for the read branch, C-M3b·2) and **not** `Via = Owner`
  (the C1 owner branch, M3). U2 pins the exact tag and the exact
  `AccessVia` literal it maps to — the drift-guard above applies.
- **The `PostStatus` enum shape** (the `Post.Status` field, C-M3b·3) is a
  Part 2 §2.2 pin. Part 1 pins only the *additive* nature (ADR 0004 §B.1,
  no re-seed, no migration) and the *single* `AccessAction` id that
  exercises it (`Moderate`). U2 pins the exact C# enum literal set
  (`Active` / `Hidden` / `Removed`, or the M3b-final spelling) and the
  **exact** `IAuthorizationService.CanAsync` overload the hide / remove
  lanes call (M1's §E lane, the `IDocumentSession` overload).
- **The `Via = Report` read branch** (C-M3b·2) is **either** a new method
  on `ModerationService` **or** a direct branch on
  `AuthorizationService.Decide` — **U2's §2.4 pin decides which**. M3b's
  U5 implements against the pin; it does **not** re-decide.
- **`/admin` is unchanged** (ADR 0003 SoD, M1's admin surface). M3b's
  `AssignReportAsync` (C-M3b·4, F5) is a *new* GlobalAdmin-gated surface
  (a **Moderator** / **GlobalAdmin** surface, not a `/admin` surface). U7
  (the `/moderation` surface) confirms `/admin` was not touched.
- **`ARCHITECTURE.md` § `Moderation/` flip** (the "M3b ✓ live" status line)
  is a **U11** deliverable (the close), not a U1 / U2 deliverable, and is **not
  pinned** in this Part. U11 flips it in `## M3b — Closed (recorded)`.

## What U2 must pin (entry to Part 2)

The plan § U1 exit names **four** pins to freeze in Part 2
(§2.2 new Core types, §2.3 filing rule + pinned `Via` tags, §2.2/§2.4 unlock
read lane, §2.3 reply route) — the four items below:

1. **The `ModerationService` seam signatures** (C-M3b·1/4, F1/F5/F6) — the
   four write-lane methods verbatim C#:
   - `Task FileReportAsync(string postId, string reporterId, string? reason,
     IDocumentSession session)` — the resident-facing intake lane (F1).
   - `Task AssignReportAsync(string reportId, string actorId, IDocumentSession
     session)` — the GlobalAdmin-gated "assign to a moderator" lane (F5;
     SoD, ADR 0003).
   - `Task UnlockAsync(string reportId, string actorId, IDocumentSession
     session)` — the GlobalAdmin-gated "report-driven unlock" lane (F6;
     the sole caller of `IUserInfoService.SetComponentModeratorAccessAsync`
     in M3b — the C5 activation event).
   - `Task ResolveReportAsync(string reportId, string actorId, IDocumentSession
     session)` — the GlobalAdmin-gated "resolve" lane (F6; flips the
     `Report.Status` to the pinned "resolved" state).

   (U2's §2.2 / §2.3 freeze the exact param order, return type,
   exception surface, and whether each writes its `AccessAudit` row in the
   same `IDocumentSession` as the domain write — yes, per
   C-M3b·3 / ADR 0006-C.)

2. **The `Post.Status` enum + the `HidePostAsync` / `RemovePostAsync`
   signatures on `PostService`** (C-M3b·3, F3 / F4) — the additive enum
   (ADR 0004 §B.1; the *single* M3b ADD on the `Post` POCO) with the
   exact literal set (expected `Active` / `Hidden` / `Removed`; U2 may
   rename, the *shape* — nullable-with-default — is the pin), the two
   `Task HidePostAsync(string postId, string actorId, IDocumentSession
   session)` / `Task RemovePostAsync(string postId, string actorId,
   IDocumentSession session)` methods (each calls
   `IAuthorizationService.CanAsync(actorId, AccessAction.Moderate, target,
   session)` *before* writing, same-transaction, no partial write), and
   the **pinned write-lane `Via` tag** (U2's §2.3 — the tag that is
   *not* `Via = Report` and *not* `Via = Owner`; Part 1's two negatives
   are the only part-1-level pins on the tag).

3. **The `Via = Report` read-lane shape** (C-M3b·2, F2) — U2's §2.4 pin
   decides **which** lane: (a) a new method on `ModerationService`
   (e.g. `CanReadWithReportAsync(string actorId, string postId,
   IDocumentSession session)`) **or** (b) a direct branch on
   `AuthorizationService.Decide` — verbatim C#. U5 implements against the
   pin; U5 does **not** re-decide. U2's §2.3 also pins the **exact
   `AccessVia.Report`** literal the read branch's `AccessAudit` row
   carries (the one *allowed* value for `Via` on that branch; the
   "pinned filing tag" of C-M3b·1 is a **different** lane).

4. **The reply route `POST /posts/{id}/replies`** (M3's deferral item 5,
   micro-fix) — the exact controller action (thin, M3's
   `PostsController` shape) delegating to the existing
   `PostService.CreateReplyAsync` (M3 U6) — **no new** Core seam,
   **no new** seam-test name. U2's §2.3 may name the controller action
   (e.g. `RepliesPOST`); the Core seam name is *fixed* by M3 and is not
   a U2 pin.

U2's Part 2 also pins: the §2.5 seam-test list (names, in
`tests/Kumunita.Core.Tests/ModerationServiceTests.cs` + additions to
`tests/Kumunita.Core.Tests/PostServiceTests.cs`, per M3's Part 1
convention); the §2.6 three-test acceptance gate (closed-loop / handoff /
part-vs-whole); and the §2.7 drift-guard (this doc wins, per Part 1 §
drift-guard, above).

---

*Part 1 ends here. U2 appends `## Seams & contracts (Part 2, written by
U2)` — the seam signatures, the pinned seam-test names, the acceptance
gate, and the drift-guard.*
