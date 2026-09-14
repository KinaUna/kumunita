# TD U04 — Group view parity

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-translation-display.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.

## Goal

Apply the **identical** restructure U03 made to the community view onto
`src/Kumunita.Web/Views/Groups/PostDetail.cshtml` (the group lane), using
`Model.OriginalLanguageCode` and `r.OriginalLanguageCode` — byte-for-byte the
same contract, the only differences being the group lane's existing
form-action routes and the already-present `Model.GroupId`. **Touch no C#, no
TS, no community view.**

## Context you need (read these first, in this order)

1. `docs/design/translation-display-design.md` §**Pinned contract** (the same
   contract U03 applied: `data-td-group` wrapper + chip + variant container,
   variant-container structure, add-lane exclusion, soft-delete gating) +
   §**FACES** TD6 (the group lane behaves **identically** to the community
   lane).
2. `src/Kumunita.Web/Views/Groups/PostDetail.cshtml` — the **full current
   markup** (the helpers block, the post title+body render, the post
   translation block, the reply translation block, and the ADR 0024 soft-
   delete branches) — the group-lane mirror of the community view's blocks,
   with `Model.GroupId` in the form actions.
3. The just-landed `src/Kumunita.Web/Views/Posts/Detail.cshtml` (U03's
   result) — the **exact** chip + variant-container + exclusion markup to
   replicate; adjust **only** the group-lane form-action routes /
   `Model.GroupId`.
4. `src/Kumunita.Web/Models/GroupViewModel.cs` §`GroupPostDetailViewModel` —
   confirm `Model.OriginalLanguageCode` is present (from U02). If it is
   missing, **stop** and record `## U04 — Drift pause` (U02 should have
   landed it).

## Deliverables (1 file, modified)

**`src/Kumunita.Web/Views/Groups/PostDetail.cshtml`** — the U03 contract
applied verbatim for the group lane:

1. **Helpers block** — add `var originalCode = Model.OriginalLanguageCode;`
   and the same `missingLanguages` exclusion
   (`l.Code != originalCode`), plus the per-reply exclusion
   (`l.Code != r.OriginalLanguageCode`). Keep `langNames` / `LangName`.
2. **Post** — the `<div data-td-group="post">` wrapper with the original
   variant container (default-visible title+body) + one hidden variant per
   `PostTranslation`; the chip row with the **original** chip first (always
   present) then one chip per `PostTranslation`; the "None yet" empty state
   dropped.
3. **Reply (per `r`)** — the `<div data-td-group="reply-@r.Id">` wrapper with
   the original body container (default-visible) + one hidden variant per
   `ReplyTranslation`; the chip row (original first) + the reply add-lane
   excluding `r.OriginalLanguageCode`.
4. **Soft-delete gating (TD7)** — the post section renders only when
   `Model.Post.DeletedAt is null`; each reply's section only when
   `r.DeletedAt is null` (matching the group view's existing ADR 0024
   placeholder branches).
5. **Keep the group lane's existing form-action routes and `Model.GroupId`
   verbatim** (the `/groups/{id}/posts/{postId}/translations` and reply
   translation actions) — only the chip/variant structure and the candidate
   **list** change.

## Invariants honored

**TD·1–TD·5** as U03 (the authored-in language first/default-visible, the chip
selector, server-rendered variants, add-lane exclusion, one source per
variant) plus **TD6** (the group lane is structurally identical to the
community lane) and **TD7/TD·8** (soft-delete gating; JS-off still shows the
original).

## Exit

`dotnet build` green. The group-lane post/reply detail behaves **exactly** as
the community lane (TD6). **No new test.** Handoff note (append to
`docs/plans-milestones/done/translation-display-handoff-notes.md`): 4–5 lines
starting `## U04 — group view parity` — (a) a confirmation the markup is
**structurally identical** to U03's (same `data-td-group` targets, same
variant-container shape, same exclusion), (b) the group-lane form-action
routes preserved, (c) **any** deliberate divergence from U03 (there should be
none beyond the routes / `GroupId`), and (d) a confirmation both lanes now
share the contract U05's TS toggles against.
