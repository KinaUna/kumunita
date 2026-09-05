# 🎯 U2 — M3b Part 2 (seams, seam-test names, acceptance gate, drift-guard)

## Understanding
Author `docs/design/m3b-moderation.md` Part 2 (append), mirroring M3's `m3-posts-design.md` § `## Seams & contracts (Part 2)` structure and U1's "What U2 must pin" block (item 1–4).

## Assumptions
- Filing-`Via` tag (C-M3b·1): U1's Part 1 forbids `Via=Report` and `Via=Owner`. M1's frozen `AccessVia` vocabulary is `{Owner, Audience, Delegation, Moderator, Report, BreakGlass, Admin}`. `Unit-series rule 4` forbids opening a new seam on `IUserInfoService` / `IAuthorizationService` / `IIdentityService` and the M1 design doc treats `AccessVia` as frozen vocabulary. The only M1 literal left that fits "resident's own intake write" is `AccessVia.Admin`. Pin that literal.
- `Via=Report` read lane (C-M3b·2): U1 offered (a) a new `ModerationService` method or (b) a direct branch on `AuthorizationService.Decide`. A branch on `AuthorizationService.Decide` would couple the authorization module to `Report` reads (ADR 0006-D single-decision-path) and would be a new seam on `IAuthorizationService` (rule 4 violation). The thinner *and* boundary-preserving lane is a new `ModerationService.CanReadWithReportAsync(...)` method on the ADR 0006-E `IDocumentSession` overload, returning `Task<Decision>`, writing its own `Via=Report` row.
- `Report.Status` literals: U1 pins only "nullable until M3b's write lane sets it." I pin the four string literals each lane writes (`"filed"`, `"assigned"`, `"unlocked"`, `"resolved"`), keeping the POCO untouched per rule 5 (no new field added).
- `PostStatus` enum: U1's Part 1 already names the expected literal set `Active` / `Hidden` / `Removed`; I pin it verbatim.
- The 16 pinned test names are derived from the 6 FACES rows + 4 invariants + 3 "shape / absence" tests.

## Approach
Append a single `## Seams & contracts (Part 2, written by U2)` block to `docs/design/m3b-moderation.md` (after Part 1's `## What U2 must pin` block, at the end of the file). Section numbering follows M3's Part 2: 2.0 / 2.1 / 2.2 / 2.3 / 2.4 / 2.5 / 2.6.

Then append a `## U2 — Design doc Part 2` handoff section to `docs/plans-milestones/m3b-handoff-notes.md` with the pinned signature count, seam-test count, and a one-liner telling U3 to touch `src/Kumunita.Core/Posts/Post.cs` first for the `PostStatus` enum + `Status` field.

## Key Files
- `docs/design/m3b-moderation.md` — append Part 2.
- `docs/plans-milestones/m3b-handoff-notes.md` — append the `## U2` section.
- `src/Kumunita.Core/Posts/PostService.cs` — entry read (shape reference).
- `src/Kumunita.Core/Posts/Post.cs` — entry read (shape reference; where U3 lands).

## Risks & Open Questions
- `AccessVia.Admin` as the filing tag: semantically a stretch, but it is the only M1-frozen literal that is not explicitly forbidden by U1's Part 1 and does not invent a new enum value. The drift-guard (§2.6 / §2.7 in this Part) lets U4 re-pin to a new `AccessVia.ReportFiling` literal in the same commit if U4 finds a stronger justification that `Admin` is a *break* (C-M3b·1's two negatives remain authoritative).

**Progress**: 0% [░░░░░░░░░░]

**Last Updated**: 2026-09-05 12:03:59

## 📝 Plan Steps
-  **Read the rest of `m3-posts-design.md` Part 2 (lines 560–842) to mirror the gate + drift-guard shape.**
- 🔄 **Append `## Seams & contracts (Part 2, written by U2)` to `docs/design/m3b-moderation.md` (sections 2.0–2.6), anchored to the decisions above.**
-  **Append `## U2 — Design doc Part 2` to `docs/plans-milestones/m3b-handoff-notes.md` (pinned signature count, seam-test count, the U3 pointer).**
-  **No build (doc-only unit). No follow-up actions.**

