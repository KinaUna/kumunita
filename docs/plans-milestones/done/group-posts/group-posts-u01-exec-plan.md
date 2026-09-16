# U1 execution plan (working) — group posts: design doc Part 1

> My (unit U1's) working plan for this pass. The **authoritative spec** is
> `group-posts-u01-plan.md` (the authored unit) + the **master register**
> `plan-group-posts.md` (Understanding / Assumptions / Invariants G·1–G·8 / FACES
> G1–G13) + the **template** — `docs/design/m3-posts-design.md` §1 (Context /
> Scope / Invariants / FACES / drift-guard shape to mirror). **No code, no build.**
> There is no prior handoff section to read — this unit **creates**
> `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`.

## Scope (in / out)

- **In (mine):** two doc files.
  1. `docs/design/group-posts-design.md` — Part 1 only (Context, Scope, Invariants
     G·1–G·8, FACES G1–G13, drift-guard Part 1).
  2. `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` — file header
     + the `## U1 — design doc Part 1` section.
- **Out:** everything from U2 onward (Part 2 seams, ADR 0013, `Post.GroupId`, the
  authorization lane, the `PostService` surface, the Web surface, the 19 tests, the
  gate). I write **no code**, build **nothing**, and touch **no** `.cs` file.

## Constraints I keep pinned while writing

- **Break-glass is not a deferral.** Record it as "not available" (G·4), not an
  item in the deferral list — so U3 (ADR 0013) / U5 (the lane impl) do not
  re-litigate it. The answer is *no by design*: how-it-works.md's trust pitch,
  ADR 0003 default-OFF, and invariant C5.
- **Verbatim invariants/FACES.** G·1–G·8 and the G1–G13 table are copied from the
  master register's `## Invariants` / `## FACES` blocks (the register is
  authoritative on wording); I add *pin-notes* (which unit lands the pin) without
  changing the statements.
- **Single additive, no doc-surface change.** `Post.GroupId` is the one additive
  on the `Post` POCO (ADR 0004 §B.1, the M3b `Status` ADD precedent); it needs no
  `M3DocTypes` change (Marten's delta picks it up) and `PostReply` is unchanged. I
  state this in Context/Scope so U4 does not add a second doc surface.
- **Two-part doc discipline.** Part 1 does *not* define exact C# (that is U2 /
  Part 2). I only *name* the seams and point at U2 for the frozen shapes — keeping
  the two-part split from m3-posts-design.md clean.

## Entry reads (done)

| Read | Why |
|------|-----|
| `docs/plans-milestones/plan-group-posts.md` | master — the source of truth for the invariants/FACES I pin |
| `docs/design/m3-posts-design.md` §1 + §Drift-guard | the exact section layout to emulate (two-part, pin-notes, FACES-count handoff) |
| `docs/philosophy/how-it-works.md` | the "post to a group" + "when membership changes, past posts follow" promise this settles; the moderator/privacy pitch |
| `docs/adr/0010-private-groups.md` | why a private group can never be an audience — the core motivation for a membership lane |
| `docs/adr/0003-roles-and-moderator-scoping.md` | moderator default-OFF, break-glass, C5 — what G·4 must respect |
| `src/Kumunita.Core/Posts/Post.cs` | the frozen `Post` POCO (M3b `Status` ADD = the precedent for `Post.GroupId`) |
| `src/Kumunita.Core/UserInfo/Group.cs` | `Group` / `GroupMembership` shapes (membership = the strong-consistency read) |

## Steps (no build)

1. `docs/design/group-posts-design.md` — write Part 1 in this order:
   - `# Design Doc — Group posts (membership-scoped group channel)` + the
     two-part intro note (points at U2 for Part 2, names G·1–G·8 / G1–G13).
   - `## Context` — M2+M2b groups as membership/organizing units; M3 posts +
     audience; how-it-works.md's two promises; the arrow moves (group = a channel
     its members post in, not just an access-reuse unit); the new lane earns ADR
     0013; named (media precedent), roadmap letters unchanged.
   - `## Scope` — **In** (the `Post.GroupId` additive; the lane `AccessVia.Group`
     + 2 `CanSeeGroupAsync`; the `PostService` group surface + `GroupPostDraft`;
     the Web channel surface; the 19 tests + recorded gate; ADR 0013) with a unit
     pointer each. **Out — named deferral list** (moderation/report on group
     posts, notifications, search, cross-posting, pagination UI). **Explicitly not
     available (privacy-first)** (break-glass, moderator peek, non-member
     authoring).
   - `## Invariants (pinned for group posts)` — intro that group posts *call*
     ADR 0006 C1–C6 and own G·1–G·8; the G·1–G·8 table (verbatim statement + a
     "pinned where (unit)" column).
   - `## FACES (pinned, 13)` — the G1–G13 table verbatim from the register + the
     count handoff line to U2.
   - `## Drift-guard & change policy (Part 1)` — doc-wins rule; invariant-number
     stability; FACES-count-as-handoff-field; the "break-glass is not a
     deferral" flag; the empty "## Group posts — Closed (recorded)" placeholder
     note.
2. `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` — one-line
   "rolling, appended never rewritten" header + the `## U1 — design doc Part 1`
   section (5–6 lines): the 8 invariants by id, the 13 FACES, the FACES count
   handed to U2, the `Post.GroupId` "no second doc surface" note, and the
   break-glass-is-not-a-deferral flag.
3. Exit — move the authored unit plan:
   `docs/plans-milestones/in-progress/group-posts-u01-plan.md` →
   `docs/plans-milestones/done/group-posts-u01-plan.md`.

## Exit (per the unit's "Exit" + AGENTS.md)

- Both files exist with every section; the handoff-note entry is written.
- **No build** (doc unit). Do not run `run_build` or any test.
- Move the unit plan file to `done/` (this is the unit's Exit, not a git stage).

## Verification

- Open the design doc and confirm the four required sections are present
  (Context / Scope / Invariants / FACES) plus the drift-guard, and that the
  invariants are G·1–G·8 (8 rows) and the FACES table is G1–G13 (13 rows) with the
  "count: 13" handoff line.
- Confirm the handoff note exists in `in-progress/` with the U1 section present
  and the header stating "appended, never rewritten".
- Confirm the unit plan file now lives under `docs/plans-milestones/done/`.
- `git status` shows new/modified docs only — no `.cs` file, no staged changes.
