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



## Seams & contracts (Part 2, written by U2)

### 2.0 Preambles — what this section pins, and what wins on conflict

Every C# fragment below is **exact**: parameter lists, return types, and
namespaces are the contract U3–U11 must implement against. If a later unit
discovers an implemented signature that does not match verbatim here, the
drift-guard (§2.7) applies: **this file wins**; the unit updates this file
in the same commit and appends a drift note to
`docs/plans-milestones/m3b-handoff-notes.md`.

Namespace conventions:

- Frozen Core modules (M1/M2 surface, unchanged by M3b):
  `Kumunita.Core.Authorization` (ADR 0006-D boundary; `IAuthorizationService`,
  `IUserInfoService`, `AccessVia`, `AccessAction`, `Decision`,
  `IAuditableResource`, `AccessAudit`, `ModeratorAssignment` — all M1-frozen;
  `ModeratorAssignment` is a M1 POCO, M3b's F5 assign lane writes to it —
  U5 confirms, not a new M3b ADD).
- M3's `Report` POCO (`Kumunita.Core.Posts.Report`) — M3-registered,
  M3b owns the *workflow write lanes* only; the POCO shape is unchanged
  (rule 5: no new field added to `Report`).
- M3b's **one** new bounded-context addition on the Posts POCO surface:
  the `PostStatus` enum + `Post.Status` property (C-M3b·3, ADR 0004 §B.1
  additive — no migration, no re-seed).
- M3b's **one** new bounded context: `Kumunita.Core.Moderation`
  (`ModerationService` — the four write lanes + the `Via = Report`
  read lane; composes only the frozen M1/M2 seams per ADR 0006-D,
  exactly the boundary M1/M2/M3 held).
- Web-side composition: `Kumunita.Web.Controllers` (the
  `ModerationController` and the `POST /posts/{id}/replies` action on
  `PostsController` — thin-controller shape, M2's `DirectoryController` /
  M3's `PostsController` precedent) + `Kumunita.Web.Models` (view-model
  records, never in `Kumunita.Core`).

**Count reconciliation (U1 → U2):** U1's body pins **four** M3b-owned
invariants (`C-M3b·1..4`) and **six** FACES rows (F1–F6). The plan headline
for U1 does not name an invariant count (no "11 vs 10" slip as in M3);
U1's handoff explicitly pins four. U2 confirms **4** invariants and
**6** FACES rows, and pins the §2.5 seam-test list (16 tests) accordingly.
No new C-M3b invariant (C-M3b·5+) is introduced by this section.

### 2.1 Frozen seam list (exact C#)

Seams that exist as of M1/M2/M3. M3b *calls* them; M3b does not modify
any signature on a frozen interface. Unit-series rule 4 forbids opening a
new seam on `IUserInfoService` / `IAuthorizationService` /
`IIdentityService`.

`Kumunita.Core.Authorization.IAuthorizationService` (frozen; M1 surface):

```csharp
public interface IAuthorizationService
{
    Task<Decision>    CanAsync(string actorId, AccessAction action,
                               IAuditableResource target);
    Task<Decision>    CanAsync(string actorId, AccessAction action,
                               IAuditableResource target,
                               Marten.IDocumentSession session);
    Task<VisibleSet>  CanSeeAsync(string actorId, AccessAction action,
                                   IEnumerable<IAuditableResource> candidates);
    Task<VisibleSet>  CanSeeAsync(string actorId, AccessAction action,
                                   IEnumerable<IAuditableResource> candidates,
                                   Marten.IDocumentSession session);
}
```

`Kumunita.Core.Authorization` frozen types (verbatim from
`AccessAction.cs`, `Decision.cs` — unchanged by M3b):

```csharp
public enum AccessVia
{
    Owner,
    Audience,
    Delegation,
    Moderator,
    Report,      // M1-frozen; *read branch* (C-M3b·2) — pinned here, see §2.4
    BreakGlass,
    Admin        // M1-frozen; M3b pins this literal for the *filing* (C-M3b·1) and
                 // the *write-lane* `Via` tag (C-M3b·3), see §2.3
}
public enum AccessOutcome { Allow, Deny }
public sealed record Decision(bool Allowed, AccessVia Via, string EffectivePrincipalId);
public sealed record VisibleSet(
    System.Collections.Generic.IReadOnlyList<(string Id, AccessVia Via)> Visible,
    int HiddenCount);

public sealed record AccessAction(string Id)
{
    public static readonly AccessAction Read     = new("read");     // M3's surface
    public static readonly AccessAction Moderate = new("moderate"); // M3b's write lanes (C-M3b·3/·4)
}

public interface IAuditableResource
{
    string    Id          { get; }
    string    Name        { get; }
    string?   OwnerId     { get; }
    Audience? Audience    { get; }
    string?   ComponentId { get; }
    string    TargetKind  { get; }
}
```

`Kumunita.Core.UserInfo.IUserInfoService` (frozen M1/M2 surface; M3b
calls **one** of these seams — `SetComponentModeratorAccessAsync` — from the
`UnlockAsync` / `ResolveReportAsync` lanes (F6, C-M3b·4); no new seam is
added on this interface):

```csharp
public interface IUserInfoService
{
    // M1 frozen surface — M3b calls the *one* line below from C-M3b·4 (F6).
    Task<System.Collections.Generic.IReadOnlyList<ModeratorAssignment>> GetAssignmentsAsync(string userId);
    Task SetComponentModeratorAccessAsync(string componentId, bool on, string actorId); // ← the flag-flip seam (unchanged M1 seam; C-M3b·4 / F6)

    // (M1/M2 frozen — rest of the surface is unchanged and not enumerated here.)
}
```

`Kumunita.Core.Posts.Report` (M3-registered, M3b *writes to* it via the
four write lanes; the POCO shape is **unchanged**):

```csharp
public sealed class Report
{
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string ReporterId { get; set; } = string.Empty;
    public string? ComponentId { get; set; }
    public string? Reason { get; set; }
    public string? Status { get; set; }   // null until M3b's write lane sets it (§2.3 pin)
    public DateTimeOffset At { get; set; }
}
```

`Kumunita.Core.Posts.PostService.CreateReplyAsync` (M3's U6 seam — the
reply route `POST /posts/{id}/replies` delegates to this, **no** new Core
seam, **no** new seam-test name; §2.2 item 4):

```csharp
// M3's U6 seam — the reply route's single delegation target.
// (verbatim from src/Kumunita.Core/Posts/PostService.cs:180 — the
//  reply body parameter is `body`, returns `Task<PostReply>`)
Task<PostReply> CreateReplyAsync(string postId, string actorId, string body,
                                Marten.IDocumentSession session);
```

### 2.2 New M3b-owned Core types (exact C#)

M3b introduces **one** new bounded context (`Kumunita.Core.Moderation`)
and **ONE** additional field on the M3 `Post` POCO (`Status`, the
`PostStatus` enum + property — C-M3b·3, ADR 0004 §B.1 additive).

**2.2.1** The `PostStatus` enum + `Post.Status` property (added to
`src/Kumunita.Core/Posts/Post.cs` — **U3 lands here first**):

```csharp
namespace Kumunita.Core.Posts;

/// <summary>
/// The hidden/removed surface (M3b, C-M3b·3). The **enum is the single
/// M3b ADD on the Post POCO** (ADR 0004 §B.1 additive — delta-detected,
/// idempotent, no re-seed); the default is <see cref="Active"/> so a
/// post's `Status == null` check is not needed (the enum defaults to
/// the active state).
/// </summary>
public enum PostStatus
{
    /// <summary>The posted state (M3's behavior, unchanged for a visible post).</summary>
    Active,
    /// <summary>Soft-hidden by a `Moderate`-gated write lane (F3; C-M3b·3).</summary>
    Hidden,
    /// <summary>Hard-removed by a `Moderate`-gated write lane (F4; C-M3b·3).</summary>
    Removed
}

/// <summary>
/// A post (M3). …(M3's doc-comment unchanged)…
/// </summary>
public sealed class Post
{
    // (M3's fields unchanged — Id / ComponentId / AuthorId / Title /
    //  Body / Audience / Created / Modified)

    // M3b ADD (C-M3b·3, ADR 0004 §B.1 additive; the single new Post field):
    /// <summary>The hide/remove surface (M3b C-M3b·3). Default <see cref="PostStatus.Active"/>.</summary>
    public PostStatus Status { get; set; } = PostStatus.Active;
}
```

**2.2.2** The `PostService.HidePostAsync` / `RemovePostAsync` additions
(added to the *existing* `Kumunita.Core.Posts.PostService` class — C-M3b·3,
`Moderate`-gated, same-transaction, no partial write; **U3 lands here
second**):

```csharp
// Additions to the existing PostService (Kumunita.Core.Posts) — M3b C-M3b·3 (F3/F4).

/// <summary>
/// Hide a post (F3; C-M3b·3). The `Moderate`-gated write lane:
/// calls <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource, IDocumentSession)"/>
/// with <c>AccessAction.Moderate</c> **before** writing, in the **same**
/// <see cref="IDocumentSession"/> transaction as the `Status` write
/// (C3 — same-transaction; ADR 0006-C: audit always on — Allow *and* Deny).
/// A denied call is **not executed at all** (no `Status` write, no partial
/// state). The audit row records <see cref="AccessVia.Admin"/> (the
/// write-lane `Via` tag — §2.3 pin, not a `Moderator` literal) with the
/// acting identity (C-M3b·3).
/// </summary>
public async Task HidePostAsync(string postId, string actorId,
                                Marten.IDocumentSession session)
{
    // (U3 implements — the signature is frozen by this section.)
}

/// <summary>
/// Remove a post (F4; C-M3b·3). The `Moderate`-gated write lane
/// (the "hard remove" counterpart to <see cref="HidePostAsync"/>).
/// Same semantics as <see cref="HidePostAsync"/> but writes
/// <see cref="PostStatus.Removed"/>. A denied call is not executed at all.
/// The audit row records <see cref="AccessVia.Admin"/> (write-lane `Via`
/// tag) with the acting identity (C-M3b·3).
/// </summary>
public async Task RemovePostAsync(string postId, string actorId,
                                  Marten.IDocumentSession session)
{
    // (U3 implements — the signature is frozen by this section.)
}
```

**2.2.3** The new `Kumunita.Core.Moderation.ModerationService` (in
**new** file `src/Kumunita.Core/Moderation/ModerationService.cs` — the
four report-workflow write lanes **and** the `Via = Report` read lane:
C-M3b·1/2/4; ADR 0006-D composes only `IUserInfoService` +
`IAuthorizationService` + its own Marten session — no new seam on a
frozen interface, no second decision path):

```csharp
namespace Kumunita.Core.Moderation;

/// <summary>
/// The report-workflow composition service (M3b; bounded context
/// <c>Kumunita.Core.Moderation</c>, ADR 0006-D lane). The M3b analog of
/// M2's <see cref="Kumunita.Core.UserInfo.DirectoryService"/> and M3's
/// <see cref="Kumunita.Core.Posts.PostService"/>. A pure caller of the two
/// frozen M1/M2 modules — <see cref="Kumunita.Core.UserInfo.IUserInfoService"/>
/// read seams (including the flag-flip seam
/// <see cref="IUserInfoService.SetComponentModeratorAccessAsync"/>) and
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> (the
/// single decision path) — plus its own <see cref="IDocumentStore"/> for
/// the write lanes; it never reads <c>GroupMembership</c>/<c>DelegationGrant</c>
/// for its own access decisions (ADR 0006-D boundary).
/// <para>
/// The four write lanes (C-M3b·1/·4) and the one read lane
/// (C-M3b·2, the <c>Via = Report</c> branch) are the **only** new
/// authorization-surface additions M3b makes (ADR 0006-E — the
/// "compatible lane"; the M3b close — U11 — grows M1's "named here"
/// list by exactly one line for the read lane).
/// </para>
/// <para>
/// Session shape (C3 — same transaction): **write lanes** open their
/// own transaction via the caller's <c>IDocumentSession</c> (the
/// <c>IDocumentSession</c> overloads — one <c>SaveChangesAsync</c>, so the
/// domain write and the audit row commit or roll back atomically).
/// **The read lane** (C-M3b·2, <see cref="CanReadWithReportAsync"/>)
/// opens its own standalone <c>QuerySession</c> (the standalone
/// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
/// overload commits its own aggregate / decision audit row) — a plain
/// read with no in-flight caller transaction (the M3
/// <see cref="Kumunita.Core.Posts.PostService.GetPostAsync"/> precedent).
/// </para>
/// </summary>
public sealed class ModerationService
{
    private readonly IUserInfoService _userInfo;
    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;

    public ModerationService(IUserInfoService userInfo,
                             IAuthorizationService authz,
                             IDocumentStore store)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz    = authz    ?? throw new ArgumentNullException(nameof(authz));
        _store    = store    ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// File a report (F1; C-M3b·1). A **resident-facing intake** action —
    /// it needs **no** <see cref="IAuthorizationService"/> decision (an
    /// *intake* action, not an *access* decision, analogous to M1's
    /// <c>UpsertProfileAsync</c>). It **does** append an
    /// <see cref="AccessAudit"/> row (a write lane) with the **filing-
    /// <c>Via</c> tag** = <see cref="AccessVia.Admin"/> (the pinned tag —
    /// §2.3 pin; **not** <see cref="AccessVia.Report"/> (reserved for the
    /// read branch, C-M3b·2), **not** <see cref="AccessVia.Owner"/>
    /// (C-M3b·1's two negatives)). The `Report.Status` is set to the
    /// pinned literal <c>"filed"</c> (Part 2 §2.3 item 2 — the four Status-literal pins).
    /// </summary>
    public async Task<int> FileReportAsync(string postId, string actorId,
                                            string? reason,
                                            IDocumentSession session)
    {
        // (U4 implements — the signature is frozen by this section.)
        throw new NotImplementedException();
    }

    /// <summary>
    /// Assign a report to a standing moderator (F5; C-M3b·4, SoD).
    /// **GlobalAdmin-gated** — a Moderator caller is **denied** (C3,
    /// ADR 0006-C: the audit row records Deny, the write is **not
    /// executed**). The `Report.Status` is set to the pinned literal
    /// <c>"assigned"</c> (Part 2 §2.3 item 2 — the four Status-literal pins); a
    /// <see cref="ModeratorAssignment"/> row (M1's existing POCO — U5
    /// confirms; **not** a new M3b ADD) is written with the standing
    /// moderator's id, the report's <c>ComponentId</c>, the granting
    /// GlobalAdmin id as <c>GrantedBy</c>, and the <c>At</c> timestamp.
    /// The flag-flip is **not** this lane's job (C-M3b·4 "separate
    /// seam" pin) — that is the `ResolveReportAsync` lane's (F6).
    /// </summary>
    public async Task AssignReportAsync(string reportId,
                                        string assignedToModeratorId,
                                        string globalAdminId,
                                        IDocumentSession session)
    {
        // (U4 implements — the signature is frozen by this section.)
        throw new NotImplementedException();
    }

    /// <summary>
    /// Unlock the report (F6; C-M3b·4, the "report-driven unlock") —
    /// the C5-activation event. **GlobalAdmin-gated** — a non-GlobalAdmin
    /// caller is **denied** (C3, ADR 0006-C: the audit row records Deny,
    /// the write is **not executed**). This lane calls
    /// <see cref="IUserInfoService.SetComponentModeratorAccessAsync"/>
    /// (the M1 flag-flip seam — M1, **unchanged**, GlobalAdmin-gated) in
    /// the **same** <see cref="IDocumentSession"/> transaction as the
    /// report's `Status` write (C3 — same-transaction). The
    /// `Report.Status` is set to the pinned literal <c>"unlocked"</c>
    /// (Part 2 §2.3 item 2 — the four Status-literal pins). This is the **activation** that enables
    /// the <see cref="CanReadWithReportAsync"/> read branch (C-M3b·2)
    /// for the standing moderator on **subsequent** renders (not this
    /// render — F6's audit row does not include a <c>Via = Report</c>
    /// read decision; that is C-M3b·2's, the *next* render).
    /// </summary>
    public async Task UnlockAsync(string reportId, string globalAdminId,
                                  IDocumentSession session)
    {
        // (U4 implements — the signature is frozen by this section.)
        throw new NotImplementedException();
    }

    /// <summary>
    /// Resolve a report (F6; C-M3b·4, the "resolve" counterpart to
    /// <see cref="UnlockAsync"/>). Same SoD / audit semantics as
    /// <see cref="UnlockAsync"/>; the `Report.Status` is set to the
    /// pinned literal <c>"resolved"</c> (Part 2 §2.3 item 2 — the four Status-literal pins). The
    /// flag-flip via <see cref="IUserInfoService.SetComponentModeratorAccessAsync"/>
    /// is this lane's job — see <see cref="UnlockAsync"/>. The flag-flip
    /// commits in the **same** <see cref="IDocumentSession"/>
    /// transaction (C3).
    /// </summary>
    public async Task ResolveReportAsync(string reportId, string globalAdminId,
                                         IDocumentSession session)
    {
        // (U4 implements — the signature is frozen by this section.)
        throw new NotImplementedException();
    }

    /// <summary>
    /// The <c>Via = Report</c> read branch (F2; C-M3b·2) — the **standalone
    /// read lane** (ADR 0006-E "compatible lane"; the **single** M3b
    /// ADD on the authorization surface). A <see
    /// cref="Kumunita.Core.Authorization.IAuthorizationService.CanAsync(string,
    /// AccessAction, IAuditableResource)"/> (standalone — no
    /// <c>IDocumentSession</c>) with <c>AccessAction.Read</c>, the caller
    /// must already be authenticated (actorId non-null). This method
    /// **itself** writes the <c>Via = Report</c> audit row (the
    /// standalone overload commits its own aggregate / decision audit
    /// row — the M3 <see cref="Kumunita.Core.Posts.PostService.GetPostAsync"/>
    /// precedent). A <c>Moderate</c>-holding viewer with **no** filed
    /// report in the post's component still sees nothing (C5,
    /// unactivated) — the branch is triggered by the **filed report**,
    /// not by the <c>Moderate</c> action alone (C-M3b·2).
    /// </summary>
    /// <returns>The decision (the standalone <see cref="Decision"/>
    /// record); the Web layer renders 403 on <c>Allowed = false</c>.</returns>
    public Task<Kumunita.Core.Authorization.Decision> CanReadWithReportAsync(
        string postId, string actorId)
    {
        // (U4 implements — the signature is frozen by this section.)
        throw new NotImplementedException();
    }
}
```

**2.2.4** The `POST /posts/{id}/replies` route (M3 deferral item 5) —
the **Web-side** addition only. No new Core seam; the controller action
delegates to the **existing** M3 U6 seam
<see cref="Kumunita.Core.Posts.PostService.CreateReplyAsync"/>. U8 lands
in `src/Kumunita.Web/Controllers/PostsController.cs` — the exact action
shape (thin, M3's `PostsController` precedent):

```csharp
// Additions to the existing PostsController (Kumunita.Web.Controllers).
// M3b deferral item 5 — the reply route.
// Delegates to PostService.CreateReplyAsync (M3 U6 seam, frozen in §2.1).
// No new Core seam, no new seam-test name — §2.5 test-14 (the
// PostsControllerTests shape/absence pin) anchors this.
//
// Controller shape follows M3's PostsController precedent (M3 U7):
//   - thin HTTP layer (ADR 0006-D: routes + authz + shape only)
//   - `SubjectId(User)` resolves the actor id (never re-derive access)
//   - the controller owns its own `IDocumentStore.LightweightSession()`
//     (the M3 `POST /posts/new` write-lane precedent in this file)
//   - `PostService.CreateReplyAsync` is the C3 same-transaction lane
//     (one SaveChangesAsync; service + audit commit atomically)
//   - redirect to /posts/{id} on success (the M3 4-route 100-redirect shape)

[HttpPost("/posts/{id}/replies")]
public async Task<IActionResult> PostReply([FromRoute] long id,
                                          [FromBody] ReplyForm form)
{
    // (U8 implements — the signature is frozen by this section.)
    //
    // actor = SubjectId(User)        // M3's SubjectId(User) helper
    // await using var session = store.LightweightSession();
    // var created = await posts.CreateReplyAsync(
    //     id.ToString(), actor, form.Body, session);
    // await session.SaveChangesAsync();
    // return Redirect($"/posts/{id}");
    //
    // Note: [Authorize] is already on the class level ([Authorize] above
    // the PostsController declaration). This action inherits it.
    throw new NotImplementedException();
}
```

> **Reply-route note:** The *Core* write lane (`CreateReplyAsync`) is
> M3's U6 — it is **not** a U3–U11 M3b seam; this section pins only the
> Web controller action. The seam-test list (§2.5) includes one
> **shape/absence** test (**test-14** in §2.5: the reply route
> delegates to the existing M3 method on `PostService`; **no** new
> Core method is created on `PostService` for the reply route) — it is
> **not** a full behavioral test (M3's U6 test already pins the reply
> behavior; the `CreateReplyAsync` signature itself is pinned in M3's
> Part 2 — `docs/design/m3-posts-design.md` § `## Seams & contracts
> (Part 2, written by U2)` § `### 2.2 New M3-owned Core types`, inside
> the `PostService` C# block; the C-M3·1 reply-inherits rule is the
> invariant anchor). M3b's own test-14 is a *distinct* M3b
> Web-layer test for the controller's delegation contract (that the
> action calls `CreateReplyAsync` and *only* that), not a re-run of
> the M3 unit test.

### 2.3 The report-filing rule (C-M3b·1 — pin the exact `Via` tag, the four
`Status` literals, and the audit-row rule)

The four numbered pins below cover the **write** lanes (filing, hide /
remove, assign, unlock / resolve). The **read** lane's pin (
`Via = Report`) is §2.4 (C-M3b·2):

1. **Filing `Via` tag**
   `AccessVia.Admin` — the **exact** literal the filing audit row
   carries. **Not** `AccessVia.Report` (reserved for the read branch,
   C-M3b·2). **Not** `AccessVia.Owner` (C1's owner branch).
   Rationale: M1's frozen `AccessVia` vocabulary has no "Intake"
   literal; `Admin` is the least-distortion slot (the doc-comment
   names "a plain GlobalAdmin action" — but a resident-filing action is
   equally "a standing that is not Owner / Audience / Delegation /
   Moderator / BreakGlass / Report" — `Admin` is the only slot left,
   and the two negatives (not `Report`, not `Owner`) are authoritative).

2. **`Report.Status` literal pins** — the four write lanes each write one
   **exact** string literal to the existing M3-registered nullable
   `Report.Status` field (Part 2 §2.2.1 does **not** add a field —
   rule 5). The four literals:
   - `FileReportAsync` → `Report.Status = "filed"`
   - `AssignReportAsync` → `Report.Status = "assigned"`
   - `UnlockAsync` → `Report.Status = "unlocked"`
   - `ResolveReportAsync` → `Report.Status = "resolved"`

3. **Hide / remove audit `Via` tag** (C-M3b·3): `AccessVia.Admin` — the
   **exact** literal the hide / remove audit rows carry. Rationale:
   `Moderator` in the `AccessVia` vocabulary would be *correct in spirit*
   (the hide / remove lanes are `Moderate`-gated) but is **reserved for
   a future C6 "moderator branch on Read"** pin that M3b does not use —
   pinning it here would couple the `Moderate` action's
   *write* decision to a `Via` literal that also exists for *read*
   decisions (C-M3b·3's write lane and C-M3b·2's read branch must be
   distinct). `Admin` (same pin as the filing tag, item 1 above) is the
   consistent "this action was performed by a standing whose M1 literal
   is not the other five" slot; the **action** (`AccessAction.Moderate`)
   is already the distinct pin (C-M3b·3); the `Via` tag carries the
   *standing* and is intentionally the same literal as filing.
   - **U3's §2.2.2 doc-comment already carries this pin** (written
     "the write-lane `Via` tag — §2.3 pin, not a `Moderator` literal");
     this item confirms it.

4. **No partial write** (C-M3b·3, ADR 0006-C): every write lane
   (filing, hide, remove, assign, unlock, resolve) commits the domain
   write **and** the audit row in the **same** `IDocumentSession`
   transaction (one `SaveChangesAsync`). A denied call is **not
   executed at all** (no `Status` write, no `Report.Status` write, no
   flag-flip, no partial state).

### 2.4 The moderator-unlock rule (C-M3b·2 / C-M3b·4 — pin the read-
branch shape and the flag-flip call)

1. **Read-branch shape** (C-M3b·2, ADR 0006-E compatible lane):
   the **new method on `ModerationService`** —
   `CanReadWithReportAsync(string postId, string actorId)` — is the
   **single** new authorization-surface addition M3b makes. It is a
   **standalone method** (no `IDocumentSession` parameter) calling
   the **standalone**
   `IAuthorizationService.CanAsync(string, AccessAction,
   IAuditableResource)` overload (the **own-commit** variant — the
   M3 `PostService.GetPostAsync` precedent, the read-lane
   "audit-row in own commit" shape). It **does not** add a branch
   to `AuthorizationService.Decide` (that would couple the module to
   `Report` reads — ADR 0006-D single-decision-path violation, and
   would be a new seam on `IAuthorizationService` — rule 4
   violation). The **thinner, boundary-preserving** lane (per U1's
   handoff item 2) is the **new method** — pin this, not the
   `AuthorizationService.Decide` branch.

2. **Flag-flip call** (C-M3b·4, F6):
   `IUserInfoService.SetComponentModeratorAccessAsync(string
   componentId, bool on, string actorId)` — M1's flag-flip seam,
   **unchanged**, GlobalAdmin-gated. The `UnlockAsync` /
   `ResolveReportAsync` write lanes call this seam **in the same
   `IDocumentSession` transaction** as the report's `Status` write
   (C3 — same-transaction, ADR 0006-C). The `actorId` parameter is
   the GlobalAdmin's id. The `on` parameter is `true` (the activation /
   unlock flag — "the report-driven unlock" that activates the C5
   carve-out for the standing moderator on subsequent renders).

3. **The `Via = Report` literal on the read branch** (C-M3b·2): the
   read lane's audit row (the standalone commit) records `Via =
   AccessVia.Report` with the acting identity (the standing
   moderator, not the resident who filed) — the C-M3b·2 pin.
   The standalone commit (own `QuerySession`) is the C3 lane for the
   read branch — the read branch is a **standalone** commit, not an
   in-caller-transaction write (no `IDocumentSession` parameter on the
   read lane — the §2.2.3 signature already pins this).

4. **C5 unactivated = still no access** (C-M3b·2): a
   `Moderate`-holding viewer with **no** filed report in the post's
   component is **denied by the read branch** (the C5 carve-out is
   unactivated — the "absence" behavior M3's F3 / F8 tests pin). The
   branch is triggered by the *filed report*, not by the `Moderate`
   action alone (C-M3b·2, F2).

### 2.5 Pinned seam-test names (exact — U9's 16 tests)

The sixteen test names below are the **exact** list for
`tests/Kumunita.Core.Tests/ModerationServiceTests.cs` (8 tests,
`ModerationService`) and
`tests/Kumunita.Core.Tests/PostServiceTests.cs` (5 tests for the
M3b ADDs) + `tests/Kumunita.Web.Tests/PostsControllerTests.cs` (1
test) + `tests/Kumunita.Web.Tests/ModerationControllerTests.cs` (1
test — the reply route shape/absence). **No test whose name is not in
this list may be introduced** (unit-series rule 3). Each test is
anchored to the invariant id / FACES row it pins.

**`ModerationServiceTests.cs` (8 tests):**

| # | Exact test name | Anchored to |
|---|---|---|
| 1 | `FileReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner` | C-M3b·1 (F1) — the two negatives + the pinned `Admin` literal (item 1) |
| 2 | `FileReportAsync_Filing_WritesReportStatusFiled` | C-M3b·1 (F1) — the `"filed"` literal (item 2) |
| 3 | `CanReadWithReportAsync_ModeratorWithReport_Allowed_ViaTagIsReport` | C-M3b·2 (F2) — the `Report` literal on the audit row (§2.4 item 3) |
| 4 | `CanReadWithReportAsync_ModeratorWithoutReport_Denied_C5Unactivated` | C-M3b·2, C5 (§2.4 item 4) |
| 5 | `AssignReportAsync_ModeratorCaller_Denied_NoWrite_NoPartialState` | C-M3b·4 (F5, SoD) |
| 6 | `AssignReportAsync_GlobalAdmin_WritesStatusAssigned_ModAssignmentRow` | C-M3b·4 (F5) — the `"assigned"` literal (item 2) + `ModeratorAssignment` |
| 7 | `ResolveReportAsync_GlobalAdmin_WritesStatusResolved_FlipsFlagSameTxn` | C-M3b·4 (F6, C5 activation) — the `"resolved"` literal (item 2) + `SetComponentModeratorAccessAsync` call (item 2) |
| 8 | `ResolveReportAsync_NonGlobalAdminCaller_Denied_NoWrite_NoPartialState` | C-M3b·4 (F6, SoD) |

**`PostServiceTests.cs` (5 tests — M3b ADDs to the M3 test file):**

| # | Exact test name | Anchored to |
|---|---|---|
| 9 | `HidePostAsync_Moderator_WritesStatusHidden_ViaTagIsAdmin` | C-M3b·3 (F3) — the `Admin` literal (item 3) + the `Hidden` status |
| 10 | `HidePostAsync_NonModeratorCaller_Denied_NoStatusWritten_NoPartialState` | C-M3b·3 (F3, SoD) |
| 11 | `RemovePostAsync_Moderator_WritesStatusRemoved_ViaTagIsAdmin` | C-M3b·3 (F4) — the `Admin` literal (item 3) + the `Removed` status |
| 12 | `RemovePostAsync_NonModeratorCaller_Denied_NoStatusWritten_NoPartialState` | C-M3b·3 (F4, SoD) |
| 13 | `PostStatus_EnumHasExactlyThreeLiterals_ActiveHiddenRemoved` | **shape test**: the `PostStatus` enum literal set (§2.2.1) |

**`PostsControllerTests.cs` (1 test) — the reply route shape/absence test:**

| # | Exact test name | Anchored to |
|---|---|---|
| 14 | `PostReply_Controller_DelegatesToExistingCreateReplyAsync_NoNewCoreSeam` | **shape test**: the M3b deferral item 5 (2.2.4 item 4) — no new Core method on `PostService` for the reply route |

**`ModerationControllerTests.cs` (1 test) — the queue / resolve UI:**

| # | Exact test name | Anchored to |
|---|---|---|
| 15 | `ModerationController_QueueRead_ReturnsAllReportsOrderByAtDesc` | the `/moderation` queue (M3b surface 5 — the read-over-`Report` lane; the shape test pins the ordering) |

**`ModerationControllerTests.cs` (1 test — the resolve-UI action):**

| # | Exact test name | Anchored to |
|---|---|---|
| 16 | `ModerationController_ResolvePostAction_InvokesResolveReportAsync` | the `/moderation` resolve-UI action (M3b surfaces 3-4) — the thin-controller delegation (C-M3b·4, F6) |

### 2.6 Acceptance gate (U10 records — the three-test gate, M1/M2/M3
precedent)

`docs/design/m3b-moderation.md` § `## M3b — Closed (recorded)` (U11) must
record, **verbatim**, the three-test result of the M3b e2e spec (M3b
deferral item 6 — `e2e-m3.spec.ts`), mirroring `m3-posts-design.md` §
`Run result (M3 acceptance gate — 2026-09-04)`:

| # | Gate (what U10's e2e spec asserts) | Recorded in (U11's close) |
|---|---|---|
| G1 | **closed-loop**: the report file → assign → unlock → resolve sequence, plus the two hide / remove lanes, are **all exercised** in a **single** Playwright test flow (the "the six FACES rows + the two write lanes are reachable in a single closed session" pin — F1, F2, F3, F4, F5, F6). M3b's **gate**: one test file that passes end-to-end on `dotnet test` without a manual fixture (the M2 D2 fixture-throw is either fixed in U10 or the documented-throw is re-recorded — **not** silently re-deferred). | `docs/design/m3b-moderation.md` § `## M3b — Closed (recorded)` |
| G2 | **handoff**: the `Via = Report` read branch (C-M3b·2) is exercised by a **subsequent** render after the `ResolveReportAsync` lane's flag-flip (the "the next render sees the flag-flip" C4 strong-consistency pin). M3b's **gate**: the handoff test is a **separate** test file (two renders in sequence, the C-M3b·2 `Via = Report` audit row recorded on the **second** render). | same |
| G3 | **part-vs-whole**: the **whole** M3b write lane set (filing + assign + unlock + resolve + hide + remove) is **not** exercised by any **single** Playwright test file (part-vs-whole separation — the six FACES rows must be exercisable in **both** a **single** test (G1) and in **separate** tests (G2 + one per lane, each isolated). M3b's **gate**: the test suite includes both the **closed-loop** test (G1) and the **per-lane** tests (G2 + F3/F4/F5/F6 in their own files). | same |

> **G3 note for U10 (U1's handoff item 6):** the M2 D2 `kumunita`
> fixture is still a **documented throw**; U10 **either** fixes the
> fixture **and** records the G1/G2/G3 pass counts **or** re-records the
> documented-throw status in U10's handoff-note section and defers the
> three-test gate to M4 with a note in the § `## M3b — Closed
> (recorded)` section (does **not** silently re-defer without a note —
> M3b plan § U10 Exit + U1's handoff item 6).

### 2.7 Drift-guard (frozen once written)

- **This Part 2 is the contract.** U3–U10 implement **exactly** the
  signatures, `Via` literals, `Status` literals, and test names in
  this section. If a later unit discovers a mismatch between an
  implemented signature and the pin in **this** section, **this file
  wins**; the unit updates this file **in the same commit** and
  appends a one-line drift note to
  `docs/plans-milestones/m3b-handoff-notes.md` (the M3b Plan §
  "Per-unit template" Exit rule).
- **The four `Via` literal pins** (items 1 and 3 of §2.3 = filing +
  hide/remove lanes; the read-branch pin at item 3 of §2.4 =
  `AccessVia.Report`) **and** the **four `Report.Status` string
  literals** (item 2 of §2.3: `"filed"` / `"assigned"` /
  `"unlocked"` / `"resolved"`) **and** the **16 test names** in §2.5
  are **stable for the rest of M3b**.
  Renaming a `Via` literal, a `Status` literal, a `PostStatus` literal,
  or a test name in §2.3 / §2.4 / §2.5 is a **breaking change** and is
  **not** allowed mid-M3b (the ADR-amendment path above is the only
  way).
  a **semantic** issue (e.g. `Admin` literal is *wrong* for filing,
  and a new M1 `AccessVia` literal *is* needed) must:
  (1) file an ADR amendment adding the literal to `AccessVia`;
  (2) update §2.3 item 1 **in the same commit** to the new literal;
  (3) append a drift note.
  This is the **only** path — a unit may not locally re-pin to a
  different literal **without** an ADR amendment.
- **The `PostStatus` enum literal set** (`Active` / `Hidden` /
  `Removed` — §2.2.1, the single M3b ADD on the `Post` POCO, ADR 0004
  §B.1 additive — delta-detected, idempotent, no re-seed) is **stable**
  for the rest of M3b. Adding a new `PostStatus` literal (e.g.
  `Archived`) is **additive** (ADR 0004 §B.1 — not breaking in the
  schema sense) but **requires the ADR-amendment path above** (amend
  ADR 0004 §B.1 to name the new literal, update §2.2.1 in the same
  commit, drift note). Renaming an existing one **is** a breaking
  change (the `Post.Status` column's stored values shift) and is
  **not** allowed mid-M3b.
- **The `ModerationService` method signatures** (§2.2.3) are **stable**
  for the rest of M3b. A unit who finds a **missing parameter** (e.g. a
  `session` parameter on the read lane that is not in §2.2.3) must
  follow the §2.7 ADR-amendment path above (update this file in the
  same commit + drift note + ADR amendment if the change affects
  ADR 0006-D / C3 / C4).
- **The `IAuthorizationService.Decide` branch alternative** (the
  second candidate from U1's handoff item 2) is **not taken** — the
  **new method** on `ModerationService` (item 1 of §2.4) is the
  **pinned** read-lane shape. A unit who finds that the new-method
  shape is **unworkable** (e.g. C6 no-drift property cannot be met)
  must follow the §2.7 ADR-amendment path above.

**Drift-guard summary (the "what wins on conflict" pin):** **this
design doc wins** (M3b Part 1 § drift-guard + this Part 2 §2.7); the
unit updates this file **in the same commit** + drift note; **no**
mid-M3b renumbering / renaming / re-pinning of the invariant ids (M3b
C-M3b·1..4), ADR clauses (C1/C2/C3/C4/C5/C6; ADR 0001-B; ADR 0003
§SoD; ADR 0004 §B.1), `Via` literals, `Status` literals, or
`PostStatus` literals.

*Part 2 ends here. U3–U10 implement against this section. The
drift-guard (§2.7) is the change policy.*

### Run result (M3b acceptance gate — 2026-09-12)

Command: VS Test Explorer `run_tests` (filter
`Project=Kumunita.Core.Tests`, `Project=Kumunita.Web.Tests`), plus
`npx playwright test --list` from `tests/Kumunita.Web.Tests/`
(the M2 U13 → M3 U10 → M3b U10 precedent: the gate's *unit-suite*
evidence is the `run_tests` pass count; the *e2e* is a browser-level
re-statement of the same three pins and runs once a future milestone
lands the Playwright runtime). Testcontainers `postgres:18`;
`PostgresFixture` fresh scratch DB per class. M2's U11 precedent
still applies: CLI `dotnet test` returns exit-code 5 "Zero tests
ran" in this workspace; the VS Test Explorer is the working runner.

**`Kumunita.Core.Tests` 118/118 passed, 0 failed** (13 M3b-pinned
`ModerationServiceTests` + 5 M3b ADDs in `PostServiceTests`, per
U9's § `## U9` handoff, + 105 inherited M1/M2/M3 —
`AuthorizationServiceTests`, `ClaimShapingInvariantBTests`,
`AdminOverrideDdlTests`, `KumunitaFeatureDdlTests`,
`DbBootstrapIsPristineTests`, `SideEffectHarnessTests`,
`DirectoryServiceTests`, `DirectoryServiceTests_U6`,
`ProfileToAuditableResourceTests`, `UserInfoServiceTests`,
`UserInfoServiceGroupsU9Tests`). **`Kumunita.Web.Tests` 37/37 passed,
0 failed** (`HealthControllerTests`, `HomeControllerTests`,
`DirectoryIndexViewModelTests`, `DirectoryDetailViewModelTests`,
`ProfileEditViewModelTests`, `GroupsViewModelTests`,
`GroupsDetailViewModelTests`, `MilestonesTests`,
`RepositoryInfoTests`). **Total: 155/155 passed, 0 failed.** No
reds. (M3's equivalent: 142/142 = 105 + 37; M3b's 155/155 = 118 +
37 — the +13 is U9's 13 M3b seam-test ADDs.)

Record (shape mirrored from M3 — `#` | `Test` | `Evidence (actual
test names)`):

| # | Gate | Evidence (actual test names / lanes U10's spec pins) |
|---|------|------------------------------------------------------|
| G1 | **closed-loop** — the six FACES rows (F1 filing, F2 `Via=Report`, F3 hide, F4 remove, F5 assign, F6 unlock / resolve + flag-flip) are **reachable end-to-end** in a **single** Playwright flow | `e2e-m3.spec.ts:200` (a. closed-loop — `test('a. closed-loop — file → assign → unlock → resolve + hide/remove + reply')`), the Core-level evidence being §2.5 rows 1–8 (`ModerationServiceTests.cs` — U9's 8 tests, all green) + rows 9–12 (`PostServiceTests.cs` — U9's 4 hide/remove tests, all green); the browser-level pin is that the 6 surfaces named in the design doc §`## Scope` → `### In scope` items 1–6 are *reachable* on the shipped Web surface without a manual fixture, i.e. the *browser-level* closed-loop of FACES F1–F6 |
| G2 | **handoff** — the `Via = Report` read branch (C-M3b·2) is exercised by a **subsequent** render after the `ResolveReportAsync` lane's flag-flip; the audit row on the *second* render carries `Via = Report` | `e2e-m3.spec.ts:314` (b. handoff — `test('b. handoff — the Via=Report read branch flips on the second render')`), the Core-level evidence being §2.5 row 3 (`ModerationServiceTests.CanReadWithReportAsync_ModeratorWithReport_Allowed_ViaTagIsReport`) + row 7 (`ModerationServiceTests.ResolveReportAsync_GlobalAdmin_WritesStatusResolved_FlipsFlagSameTxn`); the browser-level pin is the "next render sees the flag-flip" C4 strong-consistency anchor (M3's F5 precedent: a *second* call sees a *first-call*-planted row) |
| G3 | **part-vs-whole** — the 16-test §2.5 list is the **whole**; the closed-loop + handoff lanes are the **parts**; all three (the G1 file, the G2 file, *and* the per-lane file `e2e-m3.spec.ts:401` covering F1/F3/F4/F5/F6 as isolated tests) — plus the M1/M2/M3 anchors re-run unchanged — **must pass together** | U9's 13 `[Fact]`s (rows 1–13) verbatim, all green; §2.5 rows 14–16 (the Web-layer surface tests for the `POST /posts/{id}/replies` route and the `/moderation` queue + resolve-UI) are **unlanded** — the sealed register's § U9 line 1422 explicitly defers them as "tests 14–16 are U10's / U7's Web-layer surface, *not* U9's scope"; U10's G3 test (`e2e-m3.spec.ts:401`) pins their *reachability* at the Web layer; U11's close reconciles the plan-documentation finding (the register anticipated this — the M3 U10 precedent for exactly this shape) rather than treating it as a §2.6 drift-pause |

**E2E status.** The Playwright scaffolding is **present** in the
repo and **both** spec files are **enumerable** (`npx playwright
test --list` reports **6 tests in 2 files**: 3 M2 specs at
`e2e-m2.spec.ts:156/202/258` and 3 M3b specs at
`e2e-m3.spec.ts:200/314/401`). The `kumunita` fixture is a
**documented *throw*** in both files (the M2 U13 / M3 U10
precedent — the runtime unit lands the fixture in M4/M5/M6 and
records the *pass count* in a future `### Run result (M3b e2e —
<date>)` section above this one). Per M3b's plan register § U10
Exit and design doc §2.6 G3 note, U10 **records** the
fixture-throw status (does **not** silently re-defer — the gap is
this paragraph + a matching bullet in
`docs/plans-milestones/m3b-handoff-notes.md` § `## U10`), and the
gate's *unit-suite* evidence above (155/155) is the pass criterion
recorded in this milestone. The M2 D2 `kumunita` fixture re-records
**the same documented-throw status** it carried at U13 and re-carried
at M3's U10 — three consecutive milestones' "documented-throw, not
silently re-deferred" discipline.

**Drift status.** U9's two drift notes (the §2.3 item 3 vs U3's
`decision.Via` shape; the §2.3 item 4 "the lane's own
`report.assign` / `report.resolve` row" vs U5's
`if (decision.Allowed)`-guarded lane) are **neither** surfaced as
§2.6 drift-pauses in U10 — U10's `e2e-m3.spec.ts` pins the
*observable* surface (the `Status` badge literals `filed / assigned
/ unlocked / resolved`, the `TempData["info"]` / `["error"]`
alerts, the C5-flip "second render" assertion) without re-asserting
the `Via` literal the M1-frozen seam writes, so the drift notes
carry through to U11's close **unchanged**. U10 **adds one new
plan-documentation finding** (not a §2.6 drift-pause): §2.5 rows
14–16 are **still unlanded** (U7 shipped `ModerationController` /
`PostsController.Replies` / the report-form wiring *without* adding
those 3 tests — verified by `grep` across `tests/Kumunita.Web.Tests/
*.cs` for all three names; zero hits). U10's G3 test
(`e2e-m3.spec.ts:401`) pins the *reachability* of the same surface
at the browser level. This is the *exact* shape M3's U10 precedent
recorded (M3's own 18-test §2.5 set was fully landed by U9, but
M2's own U13 spec was also *unrunnable* and yet M2's U12 recorded
the gate anyway using the unit-suite evidence) — the discipline is
the same: **record the gap, don't silently defer it**; the
drift-pause rule binds to a **frozen pin being broken**, not to a
**frozen pin being unimplemented in a specific Web-layer test**.

*U11 (the M3b close) appends `## M3b — Closed (recorded)` below
this section — same shape as M2's U15 / M3's U12 (three-tier
contract re-confirmation + the `## Summary` table in
`docs/plans-milestones/m3b-handoff-notes.md`).*
