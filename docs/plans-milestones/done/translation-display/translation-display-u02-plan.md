# TD U02 — Web projection: surface the authored-in language

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-translation-display.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.

## Goal

Add the authored-in code to the two post-detail VMs and to `ReplyItem`, and
populate it in the two detail controllers — the **data layer** the views
(U03/U04) and the tests (U06) both depend on. **Touch no view, no Core
document, no `LanguageOption` record, no test.**

## Context you need (read these first, in this order)

1. `docs/design/translation-display-design.md` §**Pinned contract** →
   `### VM ADDs (exact C#)` — the three ADDs to add verbatim (the two
   `string` properties + the `ReplyItem` trailing positional and its **exact
   position**, 11th after `DeletedAt?`) and the **population source**
   (`Post.LanguageCode` / `PostReply.LanguageCode`).
2. `src/Kumunita.Web/Models/PostDetailViewModel.cs` — the current
   `PostDetailViewModel` (add the property near `CanTranslate`), the
   positional `ReplyItem` (confirm its **10-arg** order ends in `DeletedAt?`
   so the new arg is the **11th**), and the shared `LanguageOption` record
   (do **not** touch — TD·6).
3. `src/Kumunita.Web/Models/GroupViewModel.cs` §`GroupPostDetailViewModel`
   — the group-lane twin; add the same property (it mirrors
   `PostDetailViewModel`'s `PostTranslations`/`Languages`/`CanTranslate`).
4. `src/Kumunita.Web/Controllers/PostsController.cs` §`Detail` — the
   `ReplyItem(...)` positional ctor call (append `reply.LanguageCode` as the
   11th arg) and the `new PostDetailViewModel { ... }` object initializer
   (add `OriginalLanguageCode = result.Post.LanguageCode`).
5. `src/Kumunita.Web/Controllers/GroupsController.cs` §`GroupPostDetail` —
   the **parallel** `ReplyItem(...)` ctor call (append `reply.LanguageCode`)
   and the `new GroupPostDetailViewModel { ... }` initializer (add
   `OriginalLanguageCode = result.Post.LanguageCode`).

## Deliverables (4 files, all modified)

- **`src/Kumunita.Web/Models/PostDetailViewModel.cs`**
  - Add to `PostDetailViewModel`: `public string OriginalLanguageCode { get;
    set; } = string.Empty;` (additive property; a doc-comment anchoring
    TD·1/TD·6 — "the authored-in language, ADR 0018; carried additively so the
    shared `LanguageOption` record — also the ADR 0026 shape — is untouched").
  - Append a trailing positional to `ReplyItem`: `string OriginalLanguageCode`
    as the **11th** position, **after** `DeletedAt?`. Doc-comment anchoring
    TD·1 ("the reply's authored-in language, ADR 0018 — `PostReply.
    LanguageCode`; the first/default variant on the detail surface").
  - **`LanguageOption` is not modified** (TD·6).
- **`src/Kumunita.Web/Models/GroupViewModel.cs`**
  - Add to `GroupPostDetailViewModel`: the same
    `public string OriginalLanguageCode { get; set; } = string.Empty;`
    (doc-comment mirroring the community VM; TD·6).
- **`src/Kumunita.Web/Controllers/PostsController.cs`**
  - In the `Detail` action's `new ReplyItem(...)` ctor call, append
    `reply.LanguageCode` as the **11th** argument (after `reply.DeletedAt`).
  - In the `Detail` action's `new PostDetailViewModel { ... }` initializer,
    add `OriginalLanguageCode = result.Post.LanguageCode,`.
- **`src/Kumunita.Web/Controllers/GroupsController.cs`**
  - In `GroupPostDetail`'s `new ReplyItem(...)` ctor call, append
    `reply.LanguageCode` as the **11th** argument (after `reply.DeletedAt`).
  - In `GroupPostDetail`'s `new GroupPostDetailViewModel { ... }` initializer,
    add `OriginalLanguageCode = result.Post.LanguageCode,`.

## Invariants honored

- **TD·6** — the shared `LanguageOption` record is **not** reshaped; the
  authored-in code is carried **additively** on the two VMs + `ReplyItem`.
- **TD·7** — **no** Core document is modified; the source is the existing
  ADR 0018 `Post.LanguageCode` / `PostReply.LanguageCode` already returned by
  the detail result. **No** `PostTranslation`/`ReplyTranslation` row change.
  The ADR 0022 add-translation **write** lane is untouched.

## Exit

`dotnet build` green (the `ReplyItem` 11-arg ctor and both object
initializers compile). The three ADDs exist and are populated in both lanes.
**No new test** (U06 authors the first TD test — the four pinned
`TranslationDisplayTests`). Handoff note (append to `docs/plans-milestones/
done/translation-display-handoff-notes.md`): 4–5 lines starting `## U02 —
projection ADDs` — (a) the three ADD names + the `ReplyItem` ordinal
(11th, after `DeletedAt?`), (b) the two controller call sites touched (file +
that `result.Post.LanguageCode` / `reply.LanguageCode` is the source), (c) an
explicit confirmation that `LanguageOption` and **all** Core documents were
**not** touched (TD·6 / TD·7), and (d) any compile warnings on the new
members.
