# U3 — Group posts: ADR 0013 (group posts are a membership lane)

**Milestone:** group posts · **Register:** `docs/plans-milestones/plan-group-posts.md` · **Read first

## Goal
Author `docs/adr/0013-group-posts-membership-lane.md` — the durable record that group posts are a **membership-scoped channel** with a **new authorization lane beyond audience**, members-only authoring, **no break-glass**. Mirrors the single-ADR style of `0010-private-groups.md` (the closest analog — a group-lane decision).

## Entry reads
1. `docs/adr/README.md` (the register — how ADRs are listed + the status vocab)
2. `docs/adr/0010-private-groups.md` (template: private groups as a membership unit — the direct predecessor)
3. `docs/adr/0006-module-boundary-contracts.md` (the frozen-surface rules this ADR must explicitly respect: one ADD lane, frozen signatures untouched)
4. `docs/design/group-posts-design.md` §1 + §2.1/§2.2 (the invariants + seams the ADR cites)

## Deliverables (2 files)
1. **New:** `docs/adr/0013-group-posts-membership-lane.md` (~120 lines): **Status** (Accepted 2026-09), **Context** (groups = collections of users only; audience grants can't reach private groups; how-it-works.md's "post to a group" promise), **Decision** (membership lane: visibility = current membership; authoring = members only; one `IAuthorizationService` ADD + `AccessVia.Group`; `Post.GroupId` additive; lane exclusivity with components; **no break-glass / no moderator peek — privacy-first is the stated reason, not a deferral**), **Consequences** (the deferred list from the design doc Scope, each named; the 19-test seam pin; the C3 audit shapes; strong-consistency C4).
2. **Modify:** `docs/adr/README.md` — add the 0013 line (one line, next to 0012).

## Exit
Handoff note (append to `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`): `## U3
