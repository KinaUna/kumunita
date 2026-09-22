# Tags (`TG`) — sealed unit register

## Understanding

A resident who wants an overview of "everything we've posted about `sanitation`"
or "`budget`" has no way to get it today. The platform organizes content by
**Component** (a feed; one per post, a posting-membership boundary), by
**Group** (a membership channel; one per post, the access gate), and by
author — but none of those says *what a post is about*. A "Maple Street flood"
post is Safety and Maintenance and Social at once, yet lives in exactly one
Component, and the group lanes don't apply. Tags close that gap with a
lightweight, **multi-valued, non-access** label. They are deliberately *not*
a new organizing *surface* in the way a Component is: they grant no membership,
no access, no moderation scope — they are **edges between existing content**.
That is why the whole feature builds additively on the frozen access model
without reopening ADR 0006, and why the tag list, the by-tag browse, and
autocomplete can all be **one access-scoped read seam**: a tag is computed
*over* the content a viewer may already read, never a gate of it.

This is the first **shared-id-doc context after ADR 0011's `Media`**: `Tag` is
referenced by two other contexts (`Posts` and `Pages`) the way `MediaObject`
is, so it lives in its own `Kumunita.Core.Tags` context + `TagDocTypes`
surface, exactly the M3 `M3DocTypes` registration pattern (the ADR 0004 §B.1
additive lane — delta-detected, idempotent, **no seeding**, no EF migration).
Like M3, this lane therefore begins with a two-part design doc (U1/U2) that
pins every seam, invariant, and test name before any unit implements — the
guard against *Distributed fragmentation* and *Accidental integration*.

## Assumptions

- **Scope (per user, locked in ADR 0044):** free author-set tags on **posts**
  (community + group — they share the `Post` doc, ADR 0013) and on
  **`PageKind.User` (blog) pages** (ADR 0040); per-language display names set
  by the tag's **creator**; one access-scoped read seam (tag list / by-tag /
  autocomplete); the composer tag input; the `/tags` browse. **Out (→ future
  lanes, ADR 0044 D8):** no merge/rename, no moderation/reporting, no seeded
  defaults, no machine translation, no reply / announcement /
  `PageKind.System` tagging, no usage analytics.
- **The access model is untouched (ADR 0044 D2/D5):** no new `AccessAction`,
  no new `AccessVia` value (only the existing `Owner` / `Admin` used for audit
  rows), no new `Decide()` branch, no audience on a tag. The single
  load-bearing contract stays the *content's* existing `Read` decision; the
  tag is computed over it, never a gate of it.
- **Standing split (ADR 0044 D4):** *attach* = the object's existing edit
  standing (free — the post's author; the blog page's author ∪ GlobalAdmin);
  *translate* (add/overwrite a `TagTranslation`) = the tag's **creator** ∪
  GlobalAdmin only. A later author who merely attaches the tag does **not**
  get to reword it (the name is the creator's artifact — the ADR 0009 / 0026
  rule carried to tags).
- **Identity (ADR 0044 D3):** a tag's **business key is `Slug`** — a
  language-neutral identity, the literal typed string lowercased + trimmed
  (not accent-folded, not merged); `Name` / `TagTranslation.Name` are
  **display** values resolved per-viewer (the ADR 0005 preference order).
  `sanitation` and `Hygiène` are two tags, both valid, both in the list.
- **Privacy-pin (ADR 0044 D5):** the base read query is *"tags used on at
  least one post / blog page the viewer may already read"* — the tag list,
  the by-tag results, and autocomplete all derive from it. A tag used only on
  content a viewer cannot read **never surfaces** to them. A global tag
  enumeration is excluded by design (it would leak a subject's existence).
- **Versioned storage (ADR 0004 §B.1):** `Tag` / `TagTranslation` are
  **Marten-native** POCOs registered in a new parallel surface
  `TagDocTypes.Configure(StoreOptions)` (analogous to `M3DocTypes` /
  `MediaDocTypes`), wired into the existing boot paths **next to** the
  `M1DocTypes` / `M3DocTypes` / `MediaDocTypes` / `PageDocTypes` calls in
  `Kumunita.Web/Program.cs` (~L75–93) + `Kumunita.Core/Bootstrap/SchemaBootstrap.cs`.
  No hand-rolled `FeatureSchemaBase` (that carve-out is reserved for
  operator-written tables like `AdminOverride`). `Post.TagIds` /
  `Page.TagIds` are additive `string[]` fields (default empty) — the
  `Post.Status` / `GroupId` / `LanguageCode` / `ImageIds` history on the
  POCOs.
- **The write lane is the object's existing edit seam.** Attaching /
  detaching a tag is part of editing that object — `PostService`
  (the ADR 0014 / 0016 author-only lane; the ADR 0013 group lane) and
  `PageService` (the ADR 0040 author ∪ GlobalAdmin lane) each gain the
  `TagIds` field in their create/update path + the `TagService` attach /
  translate standing seam. There is **no** separate "tag editor" controller —
  the tag is a field on the object, edited where the object is edited. The
  audit idiom is `PostService.AddPostTranslationAsync` (action
  `posttranslation.add`, `Via = Owner` / `Admin`, one `AccessAudit` row,
  single `SaveChangesAsync`, C3).
- **Test model (unchanged).** xunit.v3, run via `dotnet exec …dll` per
  AGENTS.md (**not** `dotnet test` / Test Explorer on this machine);
  `Kumunita.Core.Tests` = `PostgresFixture` (one shared `postgres:18` per
  class, fresh scratch DB per test). The invariant-anchored seam-test list is
  pinned in the design doc's Part 2 (U2).

## Approach

Three tracks, sequenced — exactly like M3. **Track A (Core):**
`Kumunita.Core/Tags/` — `Tag` / `TagTranslation`, the `TagDocTypes`
registration, the `Post.TagIds` / `Page.TagIds` additive fields, and
`TagService` (attach / translate / list / by-tag / suggest — all reads
computed over the actor's readable content). **Track B (Web):** `TagController`
(`GET /tags`, `GET /tags/{slug}`, `POST /api/tags/suggest`), the composer tag
input (the post + blog-page editors), the `kw-l` registry keys, and the
`client/lib` autocomplete. **Track C (Tests):** the invariant-anchored seam
list (pinned in Part 2) + the Web pin set + the fresh-boot manual pass.

Every unit ends with **build green**. The last unit (U13) appends the final
handoff section + the doc trio + the ADR-index backfill so the
`README` / `Milestones.cs` / `MilestonesTests` pin is honest at ship time.

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U1–U13 below), one
unit per fresh agent with a **~32K context window**.

**Shared state (three-tier contract):**

- **Primary — the design doc** (`docs/design/tags-design.md`, authored U1/U2):
  pins the exact C# signatures of every seam U3–U12 must match, the
  invariant table, the FACES table, and the pinned seam-test names. U1/U2 are
  the **sign-off gate** — ADR 0044 (Accepted) is the decisions source; the
  design doc turns it into exact shapes.
- **Secondary — this file** (`docs/plans-milestones/in-progress/tags/plan-tags.md`)
  — the unit registry with each unit's deliverables and exit criteria.
- **Scratch — the rolling handoff note** (`docs/plans-milestones/in-progress/tags/tags-handoff-notes.md`).
  One section per unit, appended (never rewritten). Each unit writes exactly
  one short section before it exits; the next unit reads only that section +
  its own entry-read list.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list,
3–5 files <~300 lines each, no full-repo scan; the design-doc section cited
is named); **Deliverables** (a closed set of new/modified files, ≤ ~4 files /
~600 LOC, no misc cleanups); **Exit** (`dotnet build` green for the touched
projects; handoff-note entry appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §2.7 drift-guard;
(3) never introduces a test whose exact name is not in the §2.5 seam list;
(4) never opens a *new* seam on `IAuthorizationService` / `IUserInfoService` /
`IIdentityService` beyond what U1/U2 pinned; (5) never re-shapes a document
(`Tag`, `TagTranslation`, the `Post.TagIds` / `Page.TagIds` fields) outside the
§2.2 pin; (6) if entry reads reveal the design doc is out of date, the unit
pauses and records `## U<m> — Drift pause` in the handoff note.

---

## Units (13 total)

### U1 — Design doc Part 1

- **Goal:** author `docs/design/tags-design.md` Part 1 — **Context, Scope
  (in/out incl. the D8 not-in-scope list), Invariants pinned for the lane,
  FACES (12)**. Mirrors M2/M3's design-doc §1. **No code, no build.**
- **Entry reads:** `docs/adr/0044-tags.md` (the decisions source — every
  invariant below must cite a D#), `docs/philosophy/templates/design-doc.md`
  (the required section set), `docs/philosophy/anti-patterns.md` (the named
  failure modes), `docs/design/m3-posts-design.md` Part 1 (the template to
  emulate), `docs/adr/0026-group-community-name-description-translations.md`
  (the `TagTranslation` shape to mirror).
- **Deliverables (1 file, new):** `docs/design/tags-design.md` (~200 lines).
  Sections:
  - `## Context` — M3/M3b shipped signals + a moderation loop; `PG` shipped the
    knowledge tree; what's missing is *what a post is about* (the multi-valued,
    non-access label); what the `TG` lane carries; the arrow moved
    (understanding → shared awareness — a subject's posts become findable as a
    set).
  - `## Scope` — **In:** `Tag` / `TagTranslation` (new `Kumunita.Core.Tags`
    context + `TagDocTypes` surface), `Post.TagIds` / `Page.TagIds` (additive),
    `TagService` (attach / translate / list / by-tag / suggest), the composer
    tag input (post + blog-page editors), the `kw-l` registry keys, the
    `GET /tags` + `GET /tags/{slug}` browse, the `POST /api/tags/suggest`
    endpoint, the `client/lib` autocomplete, the seam tests + Web pins + the
    fresh-boot pass. **Out (→ future lanes, D8):** merge/rename, moderation,
    seeding, machine translation, analytics, reply / announcement /
    `System`-page tagging.
  - `## Invariants (pinned for the lane)` — 12 invariants, each with a one-line
    TG note:
    - **C-TG·1** — a tag is a **label, never a gate**: no tag grants any access
      on any object; no tag reveals a subject's existence behind unread
      content (ADR 0044 D2/D5). *(TG-owned.)*
    - **C-TG·2** — the base read query is *"tags used on ≥ 1 post / blog page
      the actor may read"*; list / by-tag / suggest all derive from it
      (ADR 0044 D5). *(TG-owned.)*
    - **C-TG·3** — **by-tag group-lane exclusion:** a group post's `TagIds`
      never make it appear in a non-member's by-tag results; the post's own
      `Read` decision (the ADR 0013 membership lane / the ADR 0035
      `PostReadDecision` seam) is applied **before** the post is returned
      (ADR 0044 D5 + Consequences). *(TG-owned.)*
    - **C-TG·4** — **`Slug` is the business key**, the literal typed string
      lowercased + trimmed; `Name` is display (ADR 0044 D3). *(TG-owned.)*
    - **C-TG·5** — **creator owns the name:** a non-creator attacher cannot
      add/overwrite a `TagTranslation`; the creator ∪ GlobalAdmin can
      (ADR 0044 D4). *(TG-owned.)*
    - **C-TG·6** — **blog-only on pages:** `Page.TagIds` is non-empty only on
      `PageKind.User` pages; the `PageKind.System` write lane refuses it
      (ADR 0044 D6). *(TG-owned.)*
    - **C-TG·7** — **no seeding:** a fresh instance has zero tags; there is no
      "suggested tags" pack, no seeder row (ADR 0044 D8). *(TG-owned.)*
    - **C-TG·8** — **reads are not decisions:** `ListForActorAsync` /
      `ListPostsByTagAsync` / `ListPagesByTagAsync` / `SuggestAsync` emit
      **no** `AccessAudit` row of their own (the ADR 0022 / 0026 "a read, not
      a decision" pin, C-M3·1 carried over; ADR 0044 D7). *(TG-owned.)*
    - **C-TG·9** — **audit on writes:** attach / create / translate each write
      **one** hand-written `AccessAudit` row (`tag.attach` / `tag.create` /
      `tagtranslation.add`, `Via = Owner` / `Admin`) in the caller's
      transaction (the C3 idiom; ADR 0044 D7). *(TG-owned.)*
    - **C1** (ADR 0006) — empty audience denies; a tag never widens a post's
      audience. *(TG: the by-tag read reuses the post's existing `Decide()`,
      not a new branch.)*
    - **C3** (ADR 0006) — audit always on for the **write** lanes (attach /
      create / translate); reads carry no row (C-TG·8).
    - **ADR 0004 §B.1** — `Tag` / `TagTranslation` are Marten-native,
      registered in `TagDocTypes`, delta-detected, idempotent, **no seeding**;
      `Post.TagIds` / `Page.TagIds` are additive fields, no re-seed.
  - `## FACES (pinned, 12)` — F1–F12, each bound to an invariant:
    - **F1** post author attaches a tag to their own post — no audit row beyond
      `tag.attach` / `tag.create`, C-TG·9
    - **F2** a second author attaches the same `Slug` — reuses the `Tag` doc,
      does **not** become `CreatedBy`, C-TG·4
    - **F3** a group post's tag does **not** surface in a non-member's
      by-tag / list / suggest, C-TG·1, C-TG·3
    - **F4** a tag used only on unread content is **invisible** in
      list / by-tag / suggest, C-TG·1, C-TG·2
    - **F5** a `PageKind.System` page **refuses** a non-empty `TagIds` set,
      C-TG·6
    - **F6** the creator sets / overwrites a `TagTranslation` — one
      `tagtranslation.add` row, C-TG·5, C-TG·9
    - **F7** a non-creator attacher **cannot** reword the tag, C-TG·5
    - **F8** a GlobalAdmin rewords any tag (break-glass), C-TG·5
    - **F9** a `de`-preferring actor typing `hy` gets the `de` name `hygiène`
      in autocomplete even if the `Slug` is `sanitation`, C-TG·2, C-TG·4
    - **F10** autocomplete is **capped** (≤ 10), C-TG·2
    - **F11** a pre-existing post's `TagIds` reads back **empty** after a
      warm re-boot (the ADR 0004 §B.1 additive no-reseed pin), ADR 0004 §B.1
    - **F12** a fresh instance has **zero** tags and an empty `/tags` page,
      C-TG·7

### U2 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard)

- **Goal:** append `## Seams & contracts (Part 2, written by U2)` to the
  design doc — the exact C# shapes U3–U12 must match, the pinned seam-test
  names, the acceptance gate, and the drift-guard. **No code, no build.**
- **Entry reads:** U1's Part 1 (the invariant table is the primary source),
  `docs/design/m3-posts-design.md` Part 2 (§2.1–§2.7, to emulate),
  `Kumunita.Core/M3DocTypes.cs` (the registration shape U3 mirrors),
  `Kumunita.Core/Posts/Post.cs` (the additive-field history U4 mirrors),
  `Kumunita.Core/Posts/PostService.cs` §`AddPostTranslationAsync` (the
  audit idiom U5 mirrors), `Kumunita.Core/Pages/PageService.cs` (the
  `PageKind.User` / `System` standing split U4/U6 respect),
  `Kumunita.Core/Pages/PageDocTypes.cs` (the `UniqueIndex` convention U3
  mirrors for `TagDocTypes`).
- **Deliverables (1 append, same file):** `docs/design/tags-design.md`.
  Sub-sections:
  - `### 2.1 frozen seam list (exact C#)` — the one **`TagService`** public
    surface (verbatim):
      - `Task<IReadOnlyList<Tag>> AttachToPostAsync(string postId, IReadOnlyList<string> slugs, string actorId, IReadOnlySet<Role> roles, IDocumentSession session);`
      - `Task<IReadOnlyList<Tag>> AttachToPageAsync(string pageId, IReadOnlyList<string> slugs, string actorId, IReadOnlySet<Role> roles, IDocumentSession session);`
      - `Task<TagTranslation> AddTagTranslationAsync(string tagId, string languageCode, string name, string actorId, IReadOnlySet<Role> roles, IDocumentSession session);`
      - `Task<IReadOnlyList<TagItem>> ListForActorAsync(string actorId);`
      - `Task<IReadOnlyList<Post>> ListPostsByTagAsync(string slug, string actorId);`
      - `Task<IReadOnlyList<Page>> ListPagesByTagAsync(string slug, string actorId);`
      - `Task<IReadOnlyList<TagItem>> SuggestAsync(string prefix, string actorId);`
      - `bool CanAttachToPost(Post post, string actorId, IReadOnlySet<Role> roles);`
      - `bool CanAttachToPage(Page page, string actorId, IReadOnlySet<Role> roles);`
      - `bool CanTranslateTag(Tag tag, string actorId, IReadOnlySet<Role> roles);`
      + `TagItem` record: `(Tag Tag, int UseCount, string DisplayedName)`.
      + the frozen `PostService` / `PageService` public surfaces (U1/U2
      **add** nothing to them — the tag lane is *inside* their existing
      edit lanes; U4 wires the `TagIds` field through their create / update
      paths without adding new public methods).
  - `### 2.2 new TG-owned Core types (exact C#)` — `Tag` (ns
    `Kumunita.Core.Tags`): `Id`, `Slug` (non-blank, the business key), `Name`
    (non-blank), `LanguageCode` (non-blank), `CreatedBy` (non-blank),
    `Created`. `TagTranslation`: `Id`, `TagId`, `LanguageCode`, `Name`
    (non-blank), `AuthorId`, `Created`. `Post.TagIds` (`string[]`, default
    empty). `Page.TagIds` (`string[]`, default empty; `PageKind.System`
    pages **always** empty).
  - `### 2.3 standing + privacy rule (the 5-state table)` — the attach /
    translate standing matrix (creator ∪ GlobalAdmin for translate; the
    object's existing edit standing for attach; the `System`-page refusal)
    + the 4-shape table for reads (actor-readable ⇒ visible; unreadable ⇒
    absent; group-lane ⇒ the post's own `Read` decision; suggest ⇒
    `starts_with(displayName, prefix) OR starts_with(slug, prefix)`, ≤ 10).
  - `### 2.4 pinned seam tests (exact names)` — file
    `tests/Kumunita.Core.Tests/TagServiceTests.cs`:
    1. `F1_AttachFreeOnOwnPost`
    2. `F2_SameSlugSecondAuthorReusesTag`
    3. `F2_SameSlugSecondAuthorNotCreatedBy`
    4. `F3_GroupPostTagInvisibleToNonMember_ByTag`
    5. `F3_GroupPostTagInvisibleToNonMember_Suggest`
    6. `F4_TagUsedOnlyOnUnreadContentInvisible_List`
    7. `F4_TagUsedOnlyOnUnreadContentInvisible_ByTag`
    8. `F4_TagUsedOnlyOnUnreadContentInvisible_Suggest`
    9. `F5_SystemPageRefusesNonEmptyTagIds`
    10. `F6_CreatorSetsTranslation`
    11. `F7_NonCreatorAttacherCannotReword`
    12. `F8_GlobalAdminRewordsAnyTag`
    13. `F9_AutocompleteMatchesViewerLanguage_DisplayName`
    14. `F9_AutocompleteMatchesViewerLanguage_Slug`
    15. `F10_AutocompleteCappedAtTen`
    16. `F11_PreExistingPostTagIdsReadBackEmptyAfterReboot`
    17. `F12_FreshInstanceHasZeroTags`
    18. `Attach_WritesOneAuditRow_tag_attach`
    19. `Create_WritesOneAuditRow_tag_create`
    20. `Translate_WritesOneAuditRow_tagtranslation_add`
    21. `List_EmitsNoAuditRow`
    22. `Suggest_EmitsNoAuditRow`
    23. `Slug_Derivation_Lowercase_Trim_Charset`
    24. `PostService_MakesNoNewModerateCall` (the ADR 0006-D lane pin —
        the tag lane composes only `IAuthorizationService` + the frozen
        `IUserInfoService` read seams, never a new seam)
  - `### 2.5 acceptance gate (U12 records)` — the three M2/M3-style tests:
    **closed loop** (author attaches `sanitation` to a post → it appears in
    the author's tag list + by-tag view; one `tag.attach` + one
    `tag.create` audit row), **handoff** (a group member added after the
    post sees the tag on the next request — strong consistency; the creator
    reword case is the "handoff to the creator" case), **part-vs-whole**
    (the 24-test list is the whole; closed-loop + handoff are the parts;
    all must pass together).
  - `### 2.6 drift-guard (frozen once written)` — the 12-invariant table
    (U1), the 12 FACES (U1), the `TagService` 11-member surface, the
    `Tag` / `TagTranslation` / `Post.TagIds` / `Page.TagIds` shapes, the
    §2.3 tables, and the 24 test names — all frozen pins; any mismatch is a
    `## U<m> — Drift pause` per unit-series rule §6.

### U3 — `Tag` / `TagTranslation` POCOs + `TagDocTypes` + boot wiring

- **Goal:** create the two POCOs (namespace `Kumunita.Core.Tags`) + the
  `TagDocTypes.Configure(StoreOptions)` registration surface + wire it into
  both boot paths (the dev loop in `Program.cs` and the all-env
  `SchemaBootstrap`), mirroring the M3 `M3DocTypes` pattern exactly.
- **Entry reads:** `Kumunita.Core/M3DocTypes.cs` (the shape to mirror),
  `Kumunita.Core/Pages/PageDocTypes.cs` (the `UniqueIndex` convention to
  mirror for `(TagId, LanguageCode)`), `Kumunita.Core/Bootstrap/SchemaBootstrap.cs`
  (where `M1DocTypes` / `M3DocTypes` / `MediaDocTypes` / `PageDocTypes` are
  called — U3 adds `TagDocTypes` next to them), `Kumunita.Web/Program.cs`
  (~L75–93, the dev-loop path), `docs/design/tags-design.md` §2.2 (the
  exact shapes).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Tags/Tag.cs` — POCO with the §2.2 shape; `Slug`
    non-blank (the C-TG·4 business key); `Name` non-blank; `CreatedBy`
    non-blank; `LanguageCode` non-blank (the ADR 0018 authored-in idiom).
  - `src/Kumunita.Core/Tags/TagTranslation.cs` — POCO with the §2.2 shape;
    `TagId` + `LanguageCode` business key (the `(TagId, LanguageCode)`
    unique index enforced by `TagDocTypes`).
  - `src/Kumunita.Core/Tags/TagDocTypes.cs` — `public static class TagDocTypes
    { public static void Configure(StoreOptions opts) { opts.Schema.For<Tag>();
    opts.Schema.For<TagTranslation>()
      .Index(t => t.TagId)
      .Index(t => t.LanguageCode)
      .UniqueIndex(t => new { t.TagId, t.LanguageCode }); } }` (the
    `(TagId, LanguageCode)` unique-index convention from `PageDocTypes`).
  - `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` — add the `using` + the
    `TagDocTypes.Configure(opts);` line next to the existing
    `M1DocTypes.Configure(opts);` / `M3DocTypes.Configure(opts);` /
    `MediaDocTypes.Configure(opts);` / `PageDocTypes.Configure(opts);`
    cluster (one line added).
  - `src/Kumunita.Web/Program.cs` — add the `TagDocTypes.Configure(...)`
    call in the dev-loop path, next to the existing `M1DocTypes.Configure` /
    `M3DocTypes.Configure` / `MediaDocTypes.Configure` / `PageDocTypes.Configure`
    cluster (~L75–93, one line added).
- **Exit:** `dotnet build` green; `TagDocTypes.cs` exists; the two POCOs
  compile; **no new test** (U4's seam tests are the first TG tests).
  Handoff note: 5 lines starting `## U3 — Tag/TagTranslation + TagDocTypes + boot`
  — (a) the `TagDocTypes` line count (2 `.Schema.For` calls + the
  `(TagId, LanguageCode)` unique index), (b) the two boot-path lines added
  (file + line numbers), (c) the `Slug` / `Name` / `CreatedBy` /
  `LanguageCode` non-blank invariants (the C-TG·4 pin), (d) any compile
  warnings on the new POCOs.

### U4 — `Post.TagIds` + `Page.TagIds` additive fields + the `System`-page refusal

- **Goal:** add the two additive `string[]` fields (the ADR 0004 §B.1
  pattern — the `Post.Status` / `GroupId` / `LanguageCode` / `ImageIds`
  history) + the `PageKind.System` write-lane refusal (the C-TG·6 pin).
- **Entry reads:** `Kumunita.Core/Posts/Post.cs` (the additive-field
  history U4 mirrors), `Kumunita.Core/Pages/Page.cs` (the `PageKind` field
  U4 respects), `Kumunita.Core/Pages/PageService.cs` §`Create` /
  §`Update` (the standing split U4 refuses on), `docs/design/tags-design.md`
  §2.2 (the exact shapes) + §2.3 (the standing table), `Kumunita.Core.Tests/PostgresFixture.cs`
  (the test harness U4 uses).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Posts/Post.cs` — add `public string[] TagIds { get;
    set; } = Array.Empty<string>();` (the ADR 0004 §B.1 additive pattern —
    next to `Status` / `GroupId` / `LanguageCode` / `ImageIds`).
  - `src/Kumunita.Core/Pages/Page.cs` — add `public string[] TagIds { get;
    set; } = Array.Empty<string>();` (same pattern; the `PageKind.System`
    write lane refuses a non-empty value, D6).
  - `src/Kumunita.Core/Pages/PageService.cs` — the `Create` / `Update`
    standing seam gains a **refusal** on `PageKind.System` pages with a
    non-empty `TagIds` set (C-TG·6 — the `System`-page write lane refuses,
    mirroring the existing `PageKind.System` standing split).
  - `tests/Kumunita.Core.Tests/TagServiceTests.cs` (new file) — the
    F5 test (`F5_SystemPageRefusesNonEmptyTagIds`) + the F11 test
    (`F11_PreExistingPostTagIdsReadBackEmptyAfterReboot`) + the F12 test
    (`F12_FreshInstanceHasZeroTags`).
- **Exit:** `dotnet build` green; `dotnet exec …Kumunita.Core.Tests.dll`
  green (the F5 / F11 / F12 pins pass); a post and a blog page each accept a
  non-empty `TagIds` set and read it back; a `System` page rejects one.
  Handoff note: 5 lines starting `## U4 — TagIds additive + System refusal`
  — (a) the two POCO fields added (file + line numbers), (b) the `System`-
  page refusal branch (file + line numbers), (c) the F5 / F11 / F12 test
  names (by id), (d) the `TagIds` default value (empty array).

### U5 — `TagService` write lane (attach / create / translate) + the standing split

- **Goal:** the `TagService` write surface — `AttachToPostAsync` /
  `AttachToPageAsync` / `AddTagTranslationAsync` + the three standing
  probes (`CanAttachToPost` / `CanAttachToPage` / `CanTranslateTag`) + the
  `Slug` derivation (lowercase + trim + charset-validate). Mirrors the
  `PostService.AddPostTranslationAsync` audit idiom exactly.
- **Entry reads:** `Kumunita.Core/Posts/PostService.cs` §
  `AddPostTranslationAsync` (the audit idiom to mirror — action
  `posttranslation.add`, `Via = Owner` / `Admin`, one `AccessAudit` row,
  single `SaveChangesAsync`, C3), `Kumunita.Core/Authorization/AccessVia.cs`
  (the `Owner` / `Admin` values U5 uses), `Kumunita.Core/Authorization/AccessAudit.cs`
  (the audit-row shape), `docs/design/tags-design.md` §2.1 (the exact
  signatures) + §2.3 (the standing table), `Kumunita.Core.Tests/PostgresFixture.cs`
  (the test harness U5 uses).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Tags/ITagService.cs` — the `TagService` public
    surface (the §2.1 11-member list) + the `TagItem` record.
  - `src/Kumunita.Core/Tags/TagService.cs` — the impl: `AttachToPostAsync`
    (resolves the object's edit standing — the ADR 0014 / 0016 author-only
    lane / the ADR 0013 group lane; creates missing `Tag` docs — the actor
    becomes `CreatedBy`, their typed string the base `Name`; sets
    `Post.TagIds`; writes `tag.attach` / `tag.create` `AccessAudit` rows);
    `AttachToPageAsync` (the ADR 0040 standing; refuses `PageKind.System`
    (C-TG·6)); `AddTagTranslationAsync` (the creator ∪ GlobalAdmin standing
    (C-TG·5); overwrites the row; writes `tagtranslation.add`); the `Slug`
    derivation (lowercase + trim + charset-validate) lives here; the
    `CanAttachToPost` / `CanAttachToPage` / `CanTranslateTag` probes
    (public, the ADR 0006-D lane pin).
  - `src/Kumunita.Core/DependencyInjection.cs` — register `ITagService`
    → `TagService` (mirror the `PostService` / `PageService` registration).
  - `tests/Kumunita.Core.Tests/TagServiceTests.cs` (append) — the F1 / F2 /
    F6 / F7 / F8 tests (the standing split) + the audit-row tests
    (`Attach_WritesOneAuditRow_tag_attach`, `Create_WritesOneAuditRow_tag_create`,
    `Translate_WritesOneAuditRow_tagtranslation_add`) + the `Slug` derivation
    test (`Slug_Derivation_Lowercase_Trim_Charset`).
- **Exit:** `dotnet build` green; `dotnet exec …Kumunita.Core.Tests.dll`
  green (the F1 / F2 / F6 / F7 / F8 + audit-row + `Slug`-derivation pins
  pass). Handoff note: 6 lines starting `## U5 — TagService write lane + standing split`
  — (a) the `TagService` line count (the 11-member surface), (b) the three
  audit actions written (`tag.attach` / `tag.create` / `tagtranslation.add`),
  (c) the `Slug` derivation (lowercase + trim + charset), (d) the F1 / F2 /
  F6 / F7 / F8 test names (by id), (e) the `DependencyInjection.cs`
  registration line (file + line number).

### U6 — `TagService` read lane (list / by-tag / suggest) + the privacy-pin

- **Goal:** the `TagService` read surface — `ListForActorAsync` /
  `ListPostsByTagAsync` / `ListPagesByTagAsync` / `SuggestAsync` — all four
  callers of the same base query (C-TG·2) + the privacy-pin (C-TG·1, C-TG·3)
  + the autocomplete contract (C-TG·2, C-TG·4). Mirrors the
  `PostService.GetPostTranslationsAsync` read idiom (no audit row, C-TG·8).
- **Entry reads:** `Kumunita.Core/Posts/PostService.cs` §
  `GetPostTranslationsAsync` (the read idiom to mirror — no audit row,
  C-TG·8), `Kumunita.Core/Authorization/IAuthorizationService.cs` (the
  frozen 4-method surface U6 composes), `Kumunita.Core/Localization/ITranslationProvider.cs`
  (the display-name resolution order U6 reuses), `docs/design/tags-design.md`
  §2.1 (the exact signatures) + §2.3 (the 4-shape table for reads),
  `Kumunita.Core.Tests/PostgresFixture.cs` (the test harness U6 uses).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/Tags/TagService.cs` (append) — the read surface:
    `ListForActorAsync` (the base query — distinct `Tag` rows used on ≥ 1
    post / blog page the actor may read, each with a use-count + the display
    name resolved to the actor's language); `ListPostsByTagAsync` /
    `ListPagesByTagAsync` (the readable posts / blog pages whose `TagIds`
    contains the tag — the post's own `Read` decision applied **before** the
    post is returned (C-TG·3)); `SuggestAsync` (the base query filtered by
    `starts_with(displayName, prefix) OR starts_with(slug, prefix)`
    (C-TG·2, C-TG·4), capped at 10 (C-TG·2)).
  - `src/Kumunita.Core/Tags/TagService.cs` (append) — the `TagItem`
    record (the §2.1 shape: `(Tag Tag, int UseCount, string DisplayedName)`).
  - `tests/Kumunita.Core.Tests/TagServiceTests.cs` (append) — the F3 / F4 /
    F9 / F10 tests (the privacy-pin + the autocomplete contract) + the
    no-audit-row tests (`List_EmitsNoAuditRow`, `Suggest_EmitsNoAuditRow`).
  - `tests/Kumunita.Core.Tests/TagServiceTests.cs` (append) — the
    `PostService_MakesNoNewModerateCall` test (the ADR 0006-D lane pin —
    the tag lane composes only `IAuthorizationService` + the frozen
    `IUserInfoService` read seams, never a new seam).
- **Exit:** `dotnet build` green; `dotnet exec …Kumunita.Core.Tests.dll`
  green (the F3 / F4 / F9 / F10 + no-audit-row + ADR 0006-D lane pins pass).
  Handoff note: 6 lines starting `## U6 — TagService read lane + privacy-pin`
  — (a) the base-query shape (C-TG·2), (b) the four read methods + the
    `TagItem` record, (c) the `SuggestAsync` filter clause
    (`starts_with(displayName, prefix) OR starts_with(slug, prefix)`, ≤ 10),
    (d) the F3 / F4 / F9 / F10 test names (by id), (e) the no-audit-row
    pin (C-TG·8).

### U7 — `TagController` (browse + suggest endpoint) + view models + `kw-l` keys

- **Goal:** the `TagController` (the two browse actions + the suggest
  endpoint) + the `TagViewModels` (Web-only) + the `kw-l` registry keys
  (the composer tag-field labels + the browse-page headings + the
  autocomplete empty-state) — in `EnValues`, `DeValues`, `FrValues` (the
  ADR 0015 parity family). The `kw-l` hard exclusions hold (ADR 0015).
- **Entry reads:** `Kumunita.Web/Controllers/PostController.cs` (the
  controller shape to mirror — the thin Web shell), `Kumunita.Web/Models/PostViewModels.cs`
  (the view-model shape to mirror), `Kumunita.Web/Controllers/LanguagesController.cs`
  (the `kw-l` registry-key idiom U7 reuses), `docs/design/tags-design.md`
  §2.1 (the exact signatures) + §2.3 (the 4-shape table for reads),
  `Kumunita.Web/appsettings.Development.json` (the route conventions U7
  follows).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Controllers/TagController.cs` — `GET /tags` (the tag
    list — name + use-count, resolved in the viewer's language),
    `GET /tags/{slug}` (the posts / blog pages tagged with it — each row
    linking to its own detail route), `POST /api/tags/suggest` (the
    `SuggestAsync` seam; CSRF-aware `api.ts` fetch, ADR 0015 §7). Both
    browse actions are **access-scoped** by construction (they render only
    what the read seam returns); a 404-floor when the slug is unknown or
    used only on unread content. The tag rows are `<a>` links to
    `GET /tags/{slug}`.
  - `src/Kumunita.Web/Models/TagViewModels.cs` — `TagListViewModel`
    (`IReadOnlyList<TagItem> Tags`), `TagByTagViewModel` (`string Slug`,
    `string DisplayedName`, `IReadOnlyList<Post> Posts`,
    `IReadOnlyList<Page> Pages`), `TagSuggestViewModel`
    (`IReadOnlyList<TagItem> Suggestions`).
  - `src/Kumunita.Web/Views/Tag/Index.cshtml` — the tag list (the
    `kw-l`-wrapped headings + the tag rows).
  - `src/Kumunita.Web/Views/Tag/ByTag.cshtml` — the by-tag view (the
    `kw-l`-wrapped headings + the posts / pages rows).
  - `src/Kumunita.Web/Localization/EnValues.cs` / `DeValues.cs` /
    `FrValues.cs` — the new `kw-l` keys (the composer tag-field labels
    `tag.input.placeholder` / `tag.input.hint`, the browse-page headings
    `tags.list.heading` / `tags.bytag.heading` / `tags.bytag.empty`, the
    autocomplete empty-state `tag.suggest.empty`) — full parity pinned by
    the existing `LS_U01` parity family.
- **Exit:** `dotnet build` green; `dotnet exec …Kumunita.Web.Tests.dll`
  green (the `kw-l` registry-parity pin passes). Handoff note: 6 lines
  starting `## U7 — TagController + view models + kw-l keys` — (a) the
  three controller actions (the routes), (b) the three view models, (c) the
  two views, (d) the six `kw-l` keys (in `EnValues` / `DeValues` /
  `FrValues`), (e) the 404-floor pin (the slug-unknown case).

### U8 — Composer tag input (post + blog-page editors) + `client/lib` autocomplete

- **Goal:** the composer tag input (the post editor `Views/Posts/Edit` +
  `Views/Posts/Create` and the blog-page composer `Views/Page/_PageForm`)
  gains a **tag field**: a free-text input with an autocomplete dropdown
  (client TS, `tsc`-only — no editor dependency, the `client/lib` idiom),
  backed by `POST /api/tags/suggest` (the `SuggestAsync` seam; CSRF-aware
  `api.ts` fetch, ADR 0015 §7). Typed tags are submitted as the `TagIds`
  set (the `Slug` the write seam derives). The `System`-page composer does
  **not** render the field (C-TG·6).
- **Entry reads:** `Kumunita.Web/Views/Posts/Edit.cshtml` (the composer
  shape to extend), `Kumunita.Web/Views/Page/_PageForm.cshtml` (the
  blog-page composer shape to extend — the `System`-page refusal branch),
  `Kumunita.Web/client/lib/rich-editor.ts` (the `client/lib` idiom U8
  follows — the `tsc`-only, no-editor-dependency convention),
  `Kumunita.Web/client/lib/api.ts` (the CSRF-aware fetch idiom U8 reuses),
  `docs/design/tags-design.md` §2.1 (the `SuggestAsync` signature U8 calls).
- **Deliverables (≤ 4 files):**
  - `Kumunita.Web/client/lib/tag-suggest.ts` — the autocomplete dropdown
    (the `tsc`-only, no-editor-dependency convention; the
    `starts_with(displayName, prefix) OR starts_with(slug, prefix)` filter
    is server-side, the client just renders the dropdown + the
    `starts_with` client-side hint).
  - `Kumunita.Web/Views/Posts/Edit.cshtml` — the tag field (the
    `kw-l`-wrapped label + the input + the dropdown mount point).
  - `Kumunita.Web/Views/Posts/Create.cshtml` — the tag field (same as
    Edit).
  - `Kumunita.Web/Views/Page/_PageForm.cshtml` — the tag field **only** on
    `PageKind.User` pages (the `System`-page refusal branch, C-TG·6).
  - `Kumunita.Web/package.json` / `tsconfig.json` — the `tag-suggest.ts`
    entry (the `tsc` build picks it up).
- **Exit:** `dotnet build` green; `npm --prefix src/Kumunita.Web run build`
  green (the `ts:build` task); a cookie-less `GET /posts/new` +
  `GET /posts/{id}/edit` render the tag field; the blog-page composer
  renders the tag field on a `User` page, **not** on a `System` page.
  Handoff note: 6 lines starting `## U8 — Composer tag input + client/lib autocomplete`
  — (a) the `tag-suggest.ts` line count, (b) the four composer files touched
    (file + the `kw-l` keys used), (c) the `System`-page refusal branch
    (file + line numbers), (d) the `ts:build` output (the `wwwroot/js/`
    path).

### U9 — `.tmp` harness refresh + the autocomplete pin

- **Goal:** re-run the `.tmp\build-harness.js` harness (the trusted-folder
  rule in AGENTS.md) + pin the autocomplete end-to-end (the client-side
  dropdown + the `POST /api/tags/suggest` endpoint + the `SuggestAsync`
  seam) — the F9 / F10 pins (the viewer-language + the cap).
- **Entry reads:** `.tmp\build-harness.js` (the harness shape to refresh),
  `.tmp\harness.html` (the harness output to re-verify),
  `Kumunita.Web/client/lib/tag-suggest.ts` (the autocomplete U9 pins),
  `docs/design/tags-design.md` §2.1 (the `SuggestAsync` signature U9 pins)
  + §2.3 (the 4-shape table for reads), `Kumunita.Web.Tests/e2e-m3.spec.ts`
  (the Playwright e2e shape U9 reuses for the autocomplete pin).
- **Deliverables (≤ 4 files):**
  - `.tmp\harness.html` (regenerated by `node .tmp\build-harness.js` —
    the trusted-folder rule in AGENTS.md; the harness mirrors the real
    toolbar / pane / textarea markup from `Views/Posts/Edit.cshtml`).
  - `Kumunita.Web.Tests/e2e-tags.spec.ts` (new file) — the autocomplete
    pin (the F9 / F10 tests — the viewer-language + the cap; the
    `starts_with(displayName, prefix) OR starts_with(slug, prefix)` filter
    visible in the dropdown).
  - `Kumunita.Web.Tests/e2e-tags.spec.ts` (new file) — the composer-input
    pin (the field renders on the post + blog composer, **not** on the
    `System`-page composer).
  - `Kumunita.Web.Tests/e2e-tags.spec.ts` (new file) — the browse-page
    pin (the list + by-tag views render over a mixed-audience fixture —
    the privacy-pin visible in the HTML).
- **Exit:** `node .tmp\build-harness.js` green (the harness reflects the
  latest `wwwroot/js/` output); `dotnet exec …Kumunita.Web.Tests.dll`
  green (the e2e pins pass); a cookie-less `GET /tags` +
  `GET /tags/sanitation` render the scoped sets. Handoff note: 6 lines
  starting `## U9 — .tmp harness refresh + autocomplete pin` — (a) the
  harness refresh (the `node .tmp\build-harness.js` output), (b) the
  three e2e pin groups (the F9 / F10 + the composer-input + the
  browse-page), (c) the `wwwroot/js/` path (the `ts:build` output),
  (d) the privacy-pin visible in the HTML (the mixed-audience fixture).

### U10 — Web surface pins (the full pin set) + the ADR 0006-D lane pin

- **Goal:** the full Web pin set — the composer + browse + autocomplete
  pins from U8/U9 hold against a *seeded* DB; the privacy-pin across all
  three surfaces (a tag behind unread content is absent from list / by-tag
  / suggest); the group-lane by-tag exclusion; the autocomplete language
  pin; the ADR 0006-D lane pin (the tag lane composes only
  `IAuthorizationService` + the frozen `IUserInfoService` read seams,
  never a new seam).
- **Entry reads:** `Kumunita.Web.Tests/e2e-m3.spec.ts` (the Playwright
  e2e shape U10 reuses), `Kumunita.Web.Tests/e2e-tags.spec.ts` (U9's
  pins — U10 extends them), `Kumunita.Core.Tests/TagServiceTests.cs`
  (the `PostService_MakesNoNewModerateCall` test U10 re-verifies at the
  Web layer), `docs/design/tags-design.md` §2.1 (the `TagService`
  11-member surface U10 pins) + §2.6 (the drift-guard),
  `Kumunita.Web.Tests/MilestonesTests.cs` (the single-in-progress pin U10
  respects).
- **Deliverables (≤ 4 files):**
  - `Kumunita.Web.Tests/e2e-tags.spec.ts` (append) — the full pin set
    (the composer + browse + autocomplete + the privacy-pin across all
    three surfaces + the group-lane by-tag exclusion + the autocomplete
    language pin + the ADR 0006-D lane pin).
  - `Kumunita.Core.Tests/TagServiceTests.cs` (append) — the
    `PostService_MakesNoNewModerateCall` test (the ADR 0006-D lane pin —
    re-verified at the Core layer).
- **Exit:** `dotnet build` green; both `dotnet exec …dll` assemblies
  green (the full pin set passes). Handoff note: 6 lines starting
  `## U10 — Web surface pins + ADR 0006-D lane pin` — (a) the full pin
  set (the list), (b) the privacy-pin across all three surfaces (the
  C-TG·1 / C-TG·2 / C-TG·3 pins), (c) the group-lane by-tag exclusion
  (the C-TG·3 pin), (d) the ADR 0006-D lane pin (the
  `PostService_MakesNoNewModerateCall` test), (e) the drift-guard
  (§2.6).

### U11 — Fresh-boot manual pass

- **Goal:** the lane's real exit — the fresh-boot pass (the ADR 0004 §B.1
  additive no-reseed pin, the C-TG·7 no-seeding pin, the C-TG·1 / C-TG·2 /
  C-TG·3 privacy pins, the C-TG·5 standing split, the C-TG·6
  `System`-page refusal, the C-TG·4 `Slug` derivation, the C-TG·8
  no-audit-row pin, the C-TG·9 audit-on-writes pin, the C-TG·2 cap) —
  the F1–F12 faces end-to-end.
- **Entry reads:** `docs/OPS.md` (the `docker compose down -v`-equivalent
  wipe of the `mt` volume), `docker-compose.yml` (the dev topology),
  `Kumunita.Web/appsettings.Development.json` (the dev config U11
  verifies), `docs/design/tags-design.md` §2.5 (the acceptance gate U11
  records) + §2.6 (the drift-guard U11 respects),
  `Kumunita.Core/Bootstrap/SchemaBootstrap.cs` (the `TagDocTypes.Configure`
  call U11 verifies).
- **Deliverables (≤ 4 files):**
  - `docs/plans-milestones/in-progress/tags/tags-handoff-notes.md` (append)
    — the U11 handoff section (the fresh-boot pass recorded).
- **Fresh-boot manual pass** (the lane's real exit): `docker compose down
  -v`-equivalent wipe of the `mt` volume per OPS, fresh boot, and:
  (a) the First-boot log shows the `mt` schema bootstrap naming the **two
  new doc types** (`Tag`, `TagTranslation`) exactly once (the ADR 0004
  §B.1 additive, idempotent shape); (b) a fresh instance has **zero** tags
  (no seed — C-TG·7) and an empty `/tags` page; (c) as a resident, attaching
  `sanitation` to a community post + a blog page creates the `Tag` doc (you
  are `CreatedBy`) and both surfaces list it; (d) as a *second* resident,
  attaching `sanitation` reuses the doc (you are **not** `CreatedBy`) and
  you **cannot** reword it (the C-TG·5 split), but a GlobalAdmin can;
  (e) a `de`-preferring resident sees the `de` name in the list and in
  autocomplete; (f) a tag used only inside a private group's posts is
  **invisible** to a non-member in list / by-tag / suggest (the privacy-pin,
  observed end-to-end); (g) editing a tag's `de` name in-app persists and
  survives a warm reboot (`docker stop` + `docker start`, **0 "First boot"
  lines**, the edit intact). Record the pass in the handoff notes below.
- **Exit:** both test assemblies green (the `dotnet exec …dll` path per
  AGENTS.md — **not** `dotnet test` / Test Explorer on this machine); the
  manual pass recorded. Handoff note: 8 lines starting
  `## U11 — Fresh-boot manual pass` — (a) the First-boot log line (the
  `Tag` / `TagTranslation` doc types named), (b) the zero-tags pin
  (C-TG·7), (c) the attach / reuse / reword / GlobalAdmin pins
  (C-TG·4 / C-TG·5), (d) the `de`-name pin (C-TG·2 / C-TG·4), (e) the
  privacy-pin (C-TG·1 / C-TG·2 / C-TG·3), (f) the warm-reboot pin
  (the ADR 0004 §B.1 no-reseed), (g) the no-audit-row + audit-on-writes
  pins (C-TG·8 / C-TG·9), (h) the cap pin (C-TG·2).

### U12 — Acceptance gate (the three M2/M3-style tests)

- **Goal:** the acceptance gate (the §2.5 pin) — the three M2/M3-style
  tests: **closed loop** (author attaches `sanitation` to a post → it
  appears in the author's tag list + by-tag view; one `tag.attach` + one
  `tag.create` audit row), **handoff** (a group member added after the post
  sees the tag on the next request — strong consistency; the creator
  reword case is the "handoff to the creator" case), **part-vs-whole**
  (the 24-test list is the whole; closed-loop + handoff are the parts; all
  must pass together).
- **Entry reads:** `docs/design/tags-design.md` §2.5 (the acceptance gate
  U12 records) + §2.4 (the 24-test list U12 re-verifies),
  `Kumunita.Core.Tests/TagServiceTests.cs` (the 24-test list U12 re-runs),
  `Kumunita.Web.Tests/e2e-tags.spec.ts` (the Web pins U12 re-runs),
  `docs/philosophy/START-HERE.md` (the three tests U12 records — the
  closed-loop / handoff / part-vs-whole).
- **Deliverables (≤ 4 files):**
  - `Kumunita.Core.Tests/TagServiceTests.cs` (append) — the three
    acceptance-gate tests (the closed-loop / handoff / part-vs-whole).
  - `docs/plans-milestones/in-progress/tags/tags-handoff-notes.md` (append)
    — the U12 handoff section (the acceptance gate recorded).
- **Exit:** `dotnet build` green; `dotnet exec …Kumunita.Core.Tests.dll`
  green (the three acceptance-gate tests pass); the 24-test list (U2's
  §2.4 pin) all green. Handoff note: 6 lines starting
  `## U12 — Acceptance gate` — (a) the closed-loop pin (the
  `tag.attach` + `tag.create` audit rows), (b) the handoff pin (the
  strong-consistency + the creator reword case), (c) the part-vs-whole
  pin (the 24-test list), (d) the three-test gate (by name), (e) the
  drift-guard (§2.6), (f) the ADR 0004 §B.1 no-reseed pin (F11).

### U13 — Docs sync (same commit as the pass)

- **Goal:** the doc trio + the ARCHITECTURE note + the ADR-index backfill
  (the 0042 / 0043 / 0045 / 0046 rows + the 0044 row) — so the
  `README` / `Milestones.cs` / `MilestonesTests` pin is honest at ship
  time, and the ADR index and the `adr/` directory agree again.
- **Entry reads:** `README.md` (the features note + the Roadmap U13
  extends), `Kumunita.Web/Milestones.cs` (the `TG` row U13 adds — the
  ADR 0013 `GP` precedent, not a new M-letter), `Kumunita.Web.Tests/MilestonesTests.cs`
  (the single-in-progress pin U13 respects), `docs/ARCHITECTURE.md`
  (the bounded-context list U13 extends + the `Post.TagIds` /
  `Page.TagIds` additive fields), `docs/adr/README.md` (the ADR index
  U13 backfills — the 0042 / 0043 / 0045 / 0046 rows + the 0044 row).
- **Deliverables (≤ 4 files):**
  - `README.md` — the features note gains the tags capability (posts +
    blog pages, free author-set, per-language names, access-scoped browse
    + autocomplete), and the Roadmap gains the lane's one-line entry.
  - `Kumunita.Web/Milestones.cs` — the `TG` row → `StatusDone` (a named
    lane, after the existing lane rows — the ADR 0013 `GP` precedent, not
    a new M-letter).
  - `Kumunita.Web.Tests/MilestonesTests.cs` — `"TG"` inserted in the
    ordered-ids + shipped-done lists; the single-in-progress milestone pin
    stays green (TG is `StatusDone`).
  - `docs/ARCHITECTURE.md` — the bounded-context list gains the
    `Kumunita.Core.Tags` context (the ADR 0011 shared-id-doc shape) + the
    `Post.TagIds` / `Page.TagIds` additive fields; the ADR index table
    (ADR 0044) is final.
  - `docs/adr/README.md` — the ADR 0044 row (added in U00 or here,
    whichever the execution chose) is consistent with the ADR text + the
    0042 / 0043 / 0045 / 0046 rows (the pre-existing drift, backfilled).
- **Exit:** the trio + ARCHITECTURE note consistent; both test assemblies
  green; the ADR 0044 text and the ADR index agree; the 0042 / 0043 /
  0045 / 0046 rows backfilled. Handoff note: 6 lines starting
  `## U13 — Docs sync` — (a) the `README.md` features note + the Roadmap
  entry, (b) the `Milestones.cs` `TG` row, (c) the `MilestonesTests.cs`
  pin, (d) the `ARCHITECTURE.md` bounded-context list + the `Post.TagIds`
  / `Page.TagIds` additive fields, (e) the `adr/README.md` index (the
  0044 row + the 0042 / 0043 / 0045 / 0046 backfill), (f) the
  single-in-progress milestone pin (the `MilestonesTests` pin).

## Handoff notes

(One appended `## U#` section per unit, never rewritten — the scratch tier.)

> **Note (2026-09-19):** ADR **0044** is the free number (0043 and
> 0045/0046 exist on disk; 0044 is taken by this lane). The ADR index in
> `docs/adr/README.md` was observed to end at **0041** even though 0042 /
> 0043 / 0045 / 0046 ADR files exist — pre-existing drift, **not**
> introduced by this lane. The U13 doc trio should backfill the index rows
> for 0042 / 0043 / 0045 / 0046 (each to its ADR's one-line title +
> Accepted status) alongside the 0044 row, so the index and the `adr/`
> directory agree again. Confirm before rewriting four rows that are not
> this lane's to own.
