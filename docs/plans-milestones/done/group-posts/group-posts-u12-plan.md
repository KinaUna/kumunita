# U12 — Group posts: final consistency check + record `## Group posts — Closed (recorded)`

**Milestone:** group posts · **Register:** `docs/plans-milestones/plan-group-posts.md` · **Read first

## Goal
The milestone's **final consistency check** — verify the whole group-posts surface is coherent end-to-end (design doc pins ↔ code symbols ↔ the 19 test names ↔ the close docs), then **record** it into the design doc as the closing section `## Group posts — Closed (recorded)` (the U10 gate line is the "recorded" precedent — this is the milestone's last line, appended after it). This is the **last unit** (U1 → U12 strict order). No code changes, no new tests, no folder moves beyond this file's own exit.

## Entry reads
1. `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` — **only the U11 section** + then skim the whole note for any `## U<m> — Drift pause` header (a pause is a hard stop — see Exit)
2. `docs/design/group-posts-design.md` — §2.1–§2.7 (the frozen seams/test names/gate/drift-guard) + the `## Group posts — Gate (recorded by U10)` section (must be present and PASS)
3. `README.md` Roadmap line + `src/Kumunita.Web/Milestones.cs` entry + `docs/ARCHITECTURE.md` group-lines (the three U11 close edits — confirm they agree with each other and with the design doc)
4. `src/Kumunita.Core/Posts/Post.cs` — grep `GroupId` only (the U4 additive, one line of context) and `src/Kumunita.Core/Authorization/Decision.cs` — grep `AccessVia` enum only (the U5 `Group` value)
5. `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs` — the `[Fact]` method names only (grep — all 19 pinned names, none missing, none extra)

## Deliverables (1 file)
- `docs/design/group-posts-design.md` — **append** the `## Group posts — Closed (recorded)` section (after the U10 gate section): the date; the consistency checklist, each line PASS/FAIL:
  - **seams** — `Post.GroupId` present (U4), `AccessVia.Group` + the two `CanSeeGroupAsync` overloads (U5), the three `PostService` group methods + `GroupPostDraft` (U6) — the symbols grep'd, each named with its file, all present
  - **tests** — the 19 §2.5 names all present in `GroupPostServiceTests.cs`, none missing/extra; the U10 gate line (19/19 + Core/Web suites) still PASS per the recorded section
  - **close docs** — README Roadmap, `Milestones.cs`, `ARCHITECTURE.md` all carry the group-posts line and agree with §2 (the U11 checklist)
  - **folders** — `docs/plans-milestones/done/group-posts-u01…u11-plan.md` all present; the handoff note has one section each for U1–U11 (and this one, appended at exit)
  - **drift** — a count of `## U<m> — Drift pause` headers in the handoff note (0 = clean; **any >0 is a hard failure** — the milestone is not closed)

## Exit
Otherwise, handoff note (append to `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`): `## U12
