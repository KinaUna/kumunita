# U4 — Group posts: `Post.GroupId` additive field

**Milestone:** group posts · **Register:** `docs/plans-milestones/plan-group-posts.md` · **Read first

## Goal
Add the single additive field `string GroupId { get; set; } = string.Empty;` to `Post` (ADR 0004 §B.1 — the **exact same lane as M3b's `PostStatus` ADD**: delta-detected, idempotent, no re-seed, **no** `M3DocTypes.cs` change — Marten's already-registered `Schema.For<Post>()` delta picks the field up). This unit is deliberately tiny and atomic: one file, one field, one comment.

## Entry reads
1. `docs/design/group-posts-design.md` §2.2 (U2's frozen `Post.GroupId` line + comment)
2. `src/Kumunita.Core/Posts/Post.cs` (the POCO + M3b's `Status` ADD comment style to mirror — the "single ADD on the Post POCO" doc-comment pattern)
3. `docs/adr/0004-data-persistence-and-schema-evolution.md` §B.1 only (the Marten-native additive rule)
4. `src/Kumunita.Core/M3DocTypes.cs` (read-only verify: `Schema.For<Post>()` is registered — confirm **no change needed** here; if the post doc is registered somewhere else, that registration is the file to cite in the handoff note, not to modify)

## Deliverables (1 file)
- `src/Kumunita.Core/Posts/Post.cs` — add `GroupId` with a doc-comment pinning: (a) G·2 (non-empty ⇒ group-post lane, `ComponentId` empty, excluded from component feeds), (b) G·8 (audience written empty on group posts), (c) ADR 0013 + the ADR 0004 §B.1 additive precedent (M3b `Status`). One field, nothing else in this file.

## Exit
Handoff note (append to `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`): `## U4
