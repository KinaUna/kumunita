# TD U03 — Community view: chip row + variant swap + add-lane exclusion

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-translation-display.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.

## Goal

Restructure `src/Kumunita.Web/Views/Posts/Detail.cshtml` (the community lane)
so the authored-in language is the **first, default-visible chip** and the
title+body (post) / body (each reply) live in **hidden variant containers**
the swap toggles — and the "Add a …" lane **no longer offers the authored-in
language**. **Touch no C#, no TS, no group view.**

## Context you need (read these first, in this order)

1. `docs/design/translation-display-design.md` §**Pinned contract** — the
   `data-*` attribute set (`data-td-group` wrapper + chip + variant container),
   the **variant-container structure**, the **add-lane exclusion rule**, the
   **soft-delete gating** — match verbatim. Plus §**FACES** TD1/TD2/TD3/TD4/
   TD5/TD7/TD8 (what the restructure must visibly produce).
2. `src/Kumunita.Web/Views/Posts/Detail.cshtml` — the **full current markup**:
   the `@{ ... }` helpers block (`langNames`, `missingLanguages`, `LangName`);
   the post title+body (`<h4>` + `<div class="mb-3 rc-body">`); the post
   translation block (the "None yet" empty state, the chips, the `<details>`
   panels, the "Add a …" `missingLanguages` loop + its form); the **reply**
   translation block (parallel "none yet" / chips / `<details>` / add-lane);
   and the two **ADR 0024 soft-delete branches** (the post body placeholder
   and the per-reply body placeholder).
3. `src/Kumunita.Web/Models/PostDetailViewModel.cs` — confirm the U02 ADDs are
   present: `Model.OriginalLanguageCode` (the post's authored-in code) and
   `r.OriginalLanguageCode` (each `ReplyItem`'s authored-in code). If either
   is missing, **stop** and record `## U03 — Drift pause` (U02 should have
   landed it).

## Deliverables (1 file, modified)

**`src/Kumunita.Web/Views/Posts/Detail.cshtml`** — restructure per the pinned
contract:

1. **Helpers block** — add `var originalCode = Model.OriginalLanguageCode;`
   and redefine the candidate list to **exclude** the authored-in language:
   `var missingLanguages = Model.Languages.Where(l => !l.HasTranslation &&
   l.Code != originalCode).ToList();` (TD·4 / TD2). Keep `langNames` /
   `LangName` as-is.
2. **Post title+body → variant containers** — replace the single `<h4>` +
   `.rc-body` with a **group wrapper** `<div data-td-group="post">` containing:
   - the **original** variant container
     `<div class="td-variant" data-td-variant="@Model.OriginalLanguageCode">`
     (visible by default — **no** `style="display:none"`) holding the title
     (`<h4>` with the same `Title ?? PlainTextPreview(Body, 80)` fallback) +
     body (`<div class="rc-body">@Html.Raw(MarkdownRenderer.RenderHtml(Model.
     Post.Body))</div>`);
   - one hidden variant per `PostTranslation`:
     `<div class="td-variant" data-td-variant="@t.LanguageCode" style=
     "display:none">` holding `t.Title` (if any) +
     `MarkdownRenderer.RenderHtml(t.Body)`.
3. **Post chip row** — a chip per variant inside the same `data-td-group="post"`
   wrapper: the **original** chip first (always present — TD·1/TD2):
   `<button type="button" class="badge text-bg-light border" data-translation-
   chip data-td-group="post" data-td-variant="@Model.OriginalLanguageCode">
   @LangName(Model.OriginalLanguageCode)</button>`, then one chip per
   `PostTranslation` (`data-td-group="post" data-td-variant="@t.LanguageCode"`,
   label `LangName(t.LanguageCode)`). **Drop the "None yet" empty state** (TD1
   — the original chip is always there, so the row is never empty).
4. **Reply block (per `r`)** — the same structure in a group wrapper
   `<div data-td-group="reply-@r.Id">`:
   - the **original** body container
     `<div class="td-variant" data-td-variant="@r.OriginalLanguageCode">`
     (default-visible) holding `MarkdownRenderer.RenderHtml(r.Body)`;
   - one hidden variant per `ReplyTranslation` (`data-td-variant="@t.
     LanguageCode" style="display:none"`);
   - a chip row: the **original** chip first (`data-td-group="reply-@r.Id"
     data-td-variant="@r.OriginalLanguageCode"`, label `LangName(r.
     OriginalLanguageCode)`) then one chip per `ReplyTranslation`;
   - the reply "Add a …" lane iterates
     `Model.Languages.Where(l => !l.HasTranslation && l.Code != r.
     OriginalLanguageCode)` (TD·4/TD5 — exclude **this reply's** authored-in
     language; a reply can be authored in a different language than the post).
5. **Keep the existing "Add a …" form markup and ADR 0022 route actions
   verbatim** (the post form: title input + body textarea; the reply form:
   body textarea only; the `/posts/{postId}/translations` and `/posts/
   {postId}/replies/{r.Id}/translations` actions) — only the candidate
   **list** and the chip/variant structure change.
6. **Soft-delete gating (TD7)** — the **entire** translation section (chips +
   variant containers + add-lane) renders **only** when the row is live:
   the post section only when `Model.Post.DeletedAt is null`; each reply's
   section only when `r.DeletedAt is null`. A soft-deleted row shows its
   existing ADR 0024 placeholder and **no** chips/swap.

## Invariants honored

- **TD·1** (the authored-in language is the first, default-visible chip, no
  "None yet"), **TD·2** (the chip row is a selector), **TD·3** (variants are
  server-rendered, hidden containers — no client HTML), **TD·4/TD·5** (the
  add-lane excludes the authored-in language; each variant reads its own
  source), **TD·7** (soft-delete gating), **TD·8** (JS-off still shows the
  original — the default-visible container is server-rendered).

## Exit

`dotnet build` green. The community detail page renders the original chip +
variant containers + the excluded add-lane, and a soft-deleted post/reply
shows no swap. **No new test** (the FACES are the visual gate; U06 adds the C#
data-shape tests). Handoff note (append to `docs/plans-milestones/done/
translation-display-handoff-notes.md`): 5–6 lines starting `## U03 —
community view swap` — (a) the `data-td-group` targets used (`"post"`,
`"reply-{id}"`), (b) the `missingLanguages` exclusion line (verbatim) and the
per-reply exclusion (`l.Code != r.OriginalLanguageCode`), (c) the soft-delete
gating decision (section renders only when live), (d) a confirmation the
add-lane **form** markup and ADR 0022 route actions were unchanged, and (e) a
one-line summary of the exact markup U04 must mirror for the group lane.
