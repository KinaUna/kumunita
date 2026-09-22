# U12 exec-plan — final consistency check + record `## Group posts — Closed (recorded)`

Working note for this unit (media-exec-plan precedent). Appended to, never rewritten.

## Steps (in order)

1. **Entry reads** (done — all PASS, no drift):
   - handoff note — U11 section last; `## U<m> — Drift pause` header count = **0**
     (the "Drift pause" text mentions at lines 107/554 are inline prose — U8's
     `New`-view route note and U7's 403→404 stale-plan line — not `## U<m>` headers;
     both self-described "not a drift pause").
   - design doc §2.1–§2.7 present; U10's `## Group posts — Gate (recorded by U10)`
     section PASS (344/344, 19 names verbatim); `## Group posts — Closed` placeholder
     at file tail (line 760).
   - README.md line 59 `**Group posts** — the post channel *inside* a group … (ADR 0013)`;
     `Milestones.cs` line 27 `new("GP", "Group posts — … (ADR 0013)", StatusDone)`;
     `ARCHITECTURE.md` §4.2 group-lane comment (lines 186–190) + §5 `Post.groupId` /
     via-list `…|BreakGlass|Admin|Group` — all three agree and match §2.
   - `src/Kumunita.Core/Posts/Post.cs:63` `public string GroupId { get; set; } = string.Empty;` (U4).
   - `src/Kumunita.Core/Authorization/Decision.cs:31` `AccessVia.Group` (8th value) (U5);
     `IAuthorizationService.cs:101/111/125/132` — the two `CanSeeGroupAsync` overloads +
     the two `CanSeeGroupFeedAsync` overloads (U2-A1/A2 freeze, 4 methods total).
   - `src/Kumunita.Core/Posts/PostService.cs:405/460/520` — `ListGroupFeedAsync` /
     `GetGroupPostAsync` / `CreateGroupPostAsync`; `src/Kumunita.Core/Posts/GroupPostDraft.cs:20`
     `public sealed record GroupPostDraft(string GroupId, string? Title, string Body);` (U6).
   - `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs` — 19 `[Fact]`s, names 1:1 with §2.5
     (lines 45–644); no extras.

2. **Deliverable** — append the filled `## Group posts — Closed (recorded)` content to
   `docs/design/group-posts-design.md`, replacing the placeholder paragraph under the
   existing `## Group posts — Closed` heading (after the U10 gate section): date, then the
   five-line checklist (seams / tests / close docs / folders / drift), each PASS/FAIL.
   All five lines PASS — no hard failure, milestone closes.

3. **Exit** — append `## U12 — final consistency + close record` to
   `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` (the note stays in
   `in-progress/` — living scratch, never archived; this file, the exec-plan, moves to
   `done/` with the spec).
