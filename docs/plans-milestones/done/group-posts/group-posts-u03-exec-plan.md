# U3 execution plan (working) — group posts: ADR 0013 (membership lane)

> My (unit U3's) working plan for this pass. The **authoritative spec** is
> `group-posts-u03-plan.md` (Goal / Entry reads / Deliverables / Exit) + the
> **master register** `plan-group-posts.md` (Assumptions G·1–G·8, invariants,
> the *directional* "Pinned contract" draft) + the **design doc**
> `docs/design/group-posts-design.md` Part 1 (invariants FACES, drift-guard)
> and Part 2 §2.1/§2.2 (the **frozen** C# the ADR cites — U2-A1/U2-A2
> adjusted). **No code, no build.**
> Prior section read: **U2** (in `group-posts-handoff-notes.md`) — the ADR
> points **at** Part 2 §2.1–§2.3, doesn't re-derive; break-glass is
> "unavailable, not deferral"; the ADD set the ADR 0006-E named-here list
> grows by at close = **4 methods** (not the draft's 2) + `AccessVia.Group`
> + the `Post.GroupId` additive + the `GroupPostDraft` record.

## Scope (in / out)
- **In (mine):** 2 files.
  1. **New** `docs/adr/0013-group-posts-membership-lane.md` (~120 lines) —
     Status / Context / Decision / Consequences, mirroring the single-ADR
     shape of `0010-private-groups.md` (the direct predecessor).
  2. **Modify** `docs/adr/README.md` — one register line (0013) next to 0012,
     same column style as the other rows.
- **Out:** everything from U4 onward (`Post.GroupId`, the lane impl, the
  `PostService` surface, the Web surface, the 19 tests, the gate). I write
  **no code**, touch **no** `.cs` file, run **no** build. I do **not** edit
  the design doc, the master register, or ADR 0006 itself (the 0006-E
  "named here" growth is U11/U12's close work — this ADR *foreshadows* it in
  Consequences, exactly the way the design doc says).

## Constraints I keep pinned while I write
- **Break-glass is "not available", not a deferral** (Part 1 drift-guard
  explicitly forbids me re-litigating it). In the ADR it appears under
  **Decision** as the stated privacy-first reason (how-it-works.md trust
  pitch, ADR 0003 default-OFF, invariant C5) and in Consequences it is
  **excluded** from the named deferral list.
- **Cite Part 2, don't re-derive.** The register's *directional* draft
  (two `CanSeeGroupAsync` overloads) is superseded by U2-A1/A2: the frozen
  set is **4 methods** (the single-target pair `CanSeeGroupAsync(actorId,
  groupId, targetPostId[, session])` + the feed pair
  `CanSeeGroupFeedAsync(actorId, groupId, candidateCount[, session])`). The
  ADR names the set and points at §2.1 for the exact C# — one side-by-side,
  nothing more.
- **Single additive, no doc-surface change** (U1's note): `Post.GroupId` is
  the one additive on the `Post` POCO (ADR 0004 §B.1, the M3b `Status` ADD
  precedent); no `M3DocTypes` change, `PostReply` unchanged, `PostService`
  ctor unchanged (ADR 0006-D: the lane owns every membership read).
- **Frozen signatures untouched.** The ADR must say the ADR 0006-E lane is
  *additive* (M2 `GetProfilesAsync` / M3b `Moderate` ADD precedent) — never
  "changes" the four `CanAsync`/`CanSeeAsync` contracts.
- **Wording authority:** invariant statements FACES are verbatim from the
  register/design doc (§1); the ADR links invariant ids (G·1–G·8) to its
  Decision bullets, mirroring how 0010 ties bullets to its own invariants.

## Entry reads (done)
| Read | Why |
|------|-----|
| `docs/plans-milestones/plan-group-posts.md` | master — Assumptions, invariants, the *directional* draft the ADR supersedes |
| `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` (U2 section) | the U2-A1/A2 adjustments + the ADD-set U2 hands forward |
| `docs/adr/README.md` | register format + that 0013 is the next number |
| `docs/adr/0010-private-groups.md` | template (closest analog — a group-lane decision) + the "private group can never be an audience" motivation I cite |
| `docs/adr/0006-module-boundary-contracts.md` | C1–C6, §D (dependency direction), §E (the ADD lane + the "named here" list this ADR grows at close) |
| `docs/design/group-posts-design.md` §1, §2.1, §2.2 (+ Scope lists) | invariants, the frozen seam list + new/changed types, the deferral list / explicitly-not-available list my Consequences names |

## Steps (no build)
1. Write this exec plan file (this step).
2. **Create** `docs/adr/0013-group-posts-membership-lane.md` in this order:
   - `# ADR 0013 — Group posts: a membership-scoped group channel` +
     `Status: Accepted` / `Date: 2026-09-12` (the 0010 header shape — no
     "Amends:" line; the relationship to 0006 is *compatible ADD*, stated in
     the body, and 0013's close work foreshadows the 0006-E name-additions).
   - `## Context` — (a) M2/M2b groups are a collection of users only
     (membership/ownership/invites/privacy — no channel; ADR 0010's family
     want); (b) M3 audience grants can't reach a private group (ADR 0010:
     never an audience option) → a group has **no** post channel today;
     (c) how-it-works.md already promises "post to a group, and when
     membership changes, past posts reach exactly the right people" — that
     promise only works if **membership is the access unit**; a new
     authorization lane ⇒ an ADR of its own (this one) + the design doc.
   - `## Decision` — one bolded bullet per decision (the 0010 shape), each
     citing its invariant ids:
     - **Visibility lane = current membership.** `Post.GroupId` non-empty
       ⇒ group post; the membership lane is the *sole* access decision
       (audience never evaluated; written non-null **empty**, G·8); live
       `GetGroupIdsAsync` ⇒ strong consistency C4 (an add/remove re-scopes
       the very next request, G3/G4 FACES).
     - **Members-only authoring.** the create gate **is** the group-lane
       decision; non-member create denies with a **persisted** Deny row then
       `UnauthorizedAccessException` (G6 FACES); Web renders 404 (no channel
       leakage — the M2 "plain member's POST 404s" precedent, G3 FACES).
     - **One ADR 0006-E ADD lane on `IAuthorizationService`.** 4 group-lane
       methods (point at §2.1 for the exact C#; U2-A1/A2 recorded) +
       `AccessVia.Group` (8th value, appended **after** `Admin` — the M1
       Admin-APPEND precedent, no stored row re-maps). Frozen
       `CanAsync`/`CanSeeAsync` byte-identity; `GetProfilesAsync` /
       `Moderate` ADD precedent.
     - **The data is one additive.** `Post.GroupId` per ADR 0004 §B.1 (M3b
       `Status` precedent — delta-detected, no re-seed, **no** `M3DocTypes`
       change); written only via `CreateGroupPostAsync`
       (`ComponentId = string.Empty`, `Audience = new Audience()`);
       `PostReply` unchanged (G·7 — replies inherit, lane-neutral).
     - **Lane exclusivity (G·2).** `GroupId ≠ ""` ⇒ `ComponentId` empty ⇒
       group posts structurally absent from `ListFeedAsync` /
       `ListAllFeedAsync` (both already filter on `ComponentId` — no filter
       added; §2.3(a)); cross-posting stays out.
     - **Delegation (G·6, C2).** in-scope `read` delegate acts with the
       **owner's** standing (`Via = Delegation`); out-of-scope denies.
     - **Audit per lane (G·5, C3).** feed = **one aggregate** row
       (TargetKind `"grouppost"`, TargetId null, counts); detail / create
       gate = **one decision** row (TargetId = post id / group id); Allow
       **and** Deny; session overloads keep the row in the caller's
       transaction; the deny row commits **before** the throw so it survives.
     - **No break-glass, no moderator peek — a standing rule, not a
       deferral.** a moderator / GlobalAdmin stands exactly as a
       non-member on this lane (C5, ADR 0003 default-OFF, how-it-works.md);
       tests G7/G8/#19 pin the absence.
   - `## Consequences` —
     - The family case lands: a private group is a real channel (the ADR
       0010 want, now with posts).
     - ADR 0006-E "named here" grows **at close (U11/U12)**: the 4 group
       -lane methods, `AccessVia.Group`, `Post.GroupId`, `GroupPostDraft` —
       foreshadow only (the design doc §1 says so), no 0006 edit this unit.
     - The 19 pinned seam tests in `GroupPostServiceTests.cs` (Part 2
       §2.5) + the three-test gate (U10) make the lane checkable; the seam
       surface is frozen — U4–U9 implement, none may re-derive.
     - Named deferrals (design doc Scope, each named): moderation/report on
       group posts; notifications (M6); search (M6); cross-posting
       (standing rule); pagination UI. Break-glass / moderator peek /
       non-member authoring are **not** on this list — they are the
       "explicitly not available" standing rules above.
     - Positive: the trust model is preserved — membership is the only
       key to a channel, and its audit is in-transaction (no silent,
       unaudited access, C3); strong consistency (C4) means a removed
       member loses the channel on the next request.
     - Negative / accepted risk: the lane is a *frozen* 4-method ADD on a
       module whose surface is deliberately small (0006-E "add few, add
       stable") — accepted: it is the minimum that produces the two
       required row shapes (U2-A1) and the two call sites (gate / detail).
3. **Modify** `docs/adr/README.md` — add `| 0013 | Group posts: a
   membership-scoped group channel | Accepted |` immediately after the 0012
   row, matching the table's column alignment.
4. **Exit** (in this order): append one section
   `## U3 — ADR 0013 (group posts are a membership lane)` to
   `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`;
   then move `group-posts-u03-plan.md` → `docs/plans-milestones/done/`
   (a plain file move — **not** staged, **not** committed; the user
   reviews everything first).

## Exit (per the unit plan's "Exit" + the master workflow template)
- The ADR exists with Status / Context / Decision / Consequences; every
  Decision bullet names its invariant ids; the break-glass bullet is a
  standing rule, **not** in the deferrals; the 4-method ADD set matches
  Part 2 §2.1; the register grew exactly one 0013 line.
- Handoff section appended **before** the plan file moved to `done/`.
- **No build**: a doc unit — no `run_build`, no test run, no project change.

## Verification
- Open the ADR; confirm the four sections in order, `Status: Accepted`,
  `Date: 2026-09-12`, that the method set is 4 (`CanSeeGroupAsync` ×2,
  `CanSeeGroupFeedAsync` ×2) and points at §2.1 for the exact C# (not the
  register's 2-overload draft).
- Confirm break-glass appears as "not available" and is **absent** from the
  named deferral list; the deferrals are the 5 named Scope items.
- `docs/adr/README.md` gained exactly one row (0013); 0001–0012 rows are
  byte-untouched.
- Confirm the handoff file gained exactly one `## U3` section and the U1/U2
  sections are byte-untouched.
- `git status --short` (no `git add`): ADR 0013 (new), README (modified),
  exec plan (new, untracked), u03 plan file (moved to `done/`) — **no**
  `.cs` files, **nothing** staged, **no** commit.
