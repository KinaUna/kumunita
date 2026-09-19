# Design Doc — Tags (`TG`): free author-set subject labels for posts and blog pages

> Part 1 of 2 (U1). Part 2 (U2) will append "Seams & contracts (Part 2, written
> by U2)" with the exact C# shapes U3–U12 must match, the **pinned seam-test
> names**, the **three-test acceptance gate**, and the drift-guard — mirroring
> [`design/m3-posts-design.md`](m3-posts-design.md) Part 2 (§2.1–§2.7). Both
> parts pin the invariant / FACES numbers that every `TG` unit (U3–U12) must
> match.
>
> Decisions source: **ADR 0044** (`docs/adr/0044-tags.md`, Accepted
> 2026-09-19). Every invariant below cites a D# from that ADR; the ADR is the
> sign-off gate and this doc turns it into exact shapes. The lane plan is
> `plans-milestones/in-progress/tags/plan-tags.md` (the register).

## Context

M3 + M3b shipped the content surface: posts, one-level replies, the component
feeds (Safety / Maintenance / Social / Governance), the moderation loop, and
the group-post lane (ADR 0013 — community and group posts share the `Post`
doc). The `PG` (pages) lane shipped the resident knowledge tree — blog pages
(`PageKind.User`) and the operator `System` pages (ADR 0039 / 0040). The
frozen access model (ADR 0006) organizes everything by **audience**, by
**Component** (a feed — one per post, a posting-membership boundary), by
**Group** (a membership channel — one per post, the access gate), and by
**author**.

None of those says *what a post is about*. A "Maple Street flood" post is
simultaneously Safety and Maintenance and Social — yet it lives in exactly one
Component, and the group lanes don't apply. There is also no tag *vocabulary*:
a resident cannot see what subjects the neighborhood is actually posting
about, or browse *by* subject (ADR 0044, Context). The two are the same gap —
the absence of a lightweight, **multi-valued, non-access** organizational
label.

**The `TG` lane is that label.** It carries: `Tag` / `TagTranslation` in a new
`Kumunita.Core.Tags` context (the ADR 0011 shared-id-doc shape, exactly the
`MediaObject` model), the additive `Post.TagIds` / `Page.TagIds` fields, the
`TagService` (attach / translate / list / by-tag / suggest — all reads
computed over content the actor may already read), the composer tag input on
the post and blog-page editors, the `GET /tags` + `GET /tags/{slug}` browse,
the `POST /api/tags/suggest` endpoint, and the `client/lib` autocomplete
(ADR 0044 D1–D7).

A tag is deliberately **not** a new organizing *surface* the way a Component
is: it grants no membership, no access, no moderation scope (ADR 0044 D2/D8).
It is an **edge between existing content** — which is exactly why the lane
builds additively on the frozen access model without reopening ADR 0006, and
why the tag list, the by-tag browse, and the autocomplete can all be **one
access-scoped read seam** (ADR 0044 D5).

**The arrow moved (understanding → shared awareness):** a subject's posts
become findable *as a set*. Today a resident can understand one post in its
Component; after `TG` the same subject surfaces as a browsable set across
posts and blog pages — computed only over what each viewer may already read,
so the awareness is never purchased with a privacy leak (the anti-patterns
guard: this lane must not become *Distributed fragmentation* — access logic
re-implemented per tag surface — nor *Accidental integration* — the privacy
rule living in a comment instead of the invariant list below).

## Scope

**In scope**

- `Tag` / `TagTranslation` — new POCOs in the **new bounded context
  `Kumunita.Core.Tags`** (the ADR 0011 shared-id-doc shape, D1), registered in
  the **new `TagDocTypes.Configure(StoreOptions)`** surface (the `M3DocTypes` /
  `MediaDocTypes` pattern), wired into both boot paths next to the existing
  `M1DocTypes` / `M3DocTypes` / `MediaDocTypes` / `PageDocTypes` cluster.
- `Post.TagIds` / `Page.TagIds` — additive `string[]` fields (default empty),
  the ADR 0004 §B.1 additive pattern (D2). One field covers community *and*
  group posts (ADR 0013); `Page.TagIds` is populated only on `PageKind.User`
  pages (D6).
- `TagService` (`Kumunita.Core.Tags`) — the single feature service: **write
  lane** `AttachToPostAsync` / `AttachToPageAsync` / `AddTagTranslationAsync`
  + the standing probes `CanAttachToPost` / `CanAttachToPage` /
  `CanTranslateTag` (D4); **read lane** `ListForActorAsync` /
  `ListPostsByTagAsync` / `ListPagesByTagAsync` / `SuggestAsync` — all four
  callers of the one base query (D5).
- The composer tag input on the **post editors** and the **blog-page
  composer** (free-text + autocomplete, backed by the suggest endpoint); the
  `System`-page composer does **not** render the field (D6).
- The `kw-l` registry keys (composer tag-field labels, browse-page headings,
  autocomplete empty-state) in `EnValues` / `DeValues` / `FrValues` — the
  ADR 0015 parity family.
- `GET /tags` (tag list: name + use-count, resolved in the viewer's
  language), `GET /tags/{slug}` (posts / blog pages tagged with it, each row
  linking to its own detail route), and `POST /api/tags/suggest` (the
  `SuggestAsync` seam; CSRF-aware `api.ts` fetch) (D5).
- The `client/lib` autocomplete (the `tsc`-only, no-editor-dependency
  convention).
- The seam tests (`Kumunita.Core.Tests/TagServiceTests.cs`), the Web pins
  (`Kumunita.Web.Tests/`), and the fresh-boot manual pass. Test *names* are
  pinned by Part 2 (U2); this part pins the invariants / FACES they anchor.

**Out of scope — future lanes (locked by ADR 0044 D8; this section is the
lane's M1-style close)**

- **No tag merge / rename.** `Slug` is the literal typed string (D3); two
  similarly-intended tags stay two tags. A merge/rename is a *future* lane if
  the community actually needs it — building it now would be a premature part
  with no closed loop (D8).
- **No tag-based access.** A tag grants no membership, no read, no moderation
  scope, on any object (D2 / D8 — the label, never a gate).
- **No tagging on replies, announcements, or `PageKind.System` pages** —
  `TagIds` is not added to `PostReply` or `Announcement` (D2), and the
  `System`-page write lane refuses a non-empty set (D6).
- **No tag moderation, no tag reporting** — a tag is not a content surface
  with a moderation lane (D8).
- **No seeded / curated default tags** — free-for-all; no seeder row, no
  "suggested tags" pack (D8).
- **No machine translation** of tag names (ADR 0005 §C boundary; D8) — a tag
  translation is a human-set display name, like a `GroupTranslation` (ADR
  0022 / 0026 idiom).
- **No usage analytics / auto-pruning** — the use-count in
  `ListForActorAsync` is a display aid, not a moderation signal (D8).

**Surfaces (resident-visible)**

1. Tag list (`/tags`) — the subjects a resident can see (the base query,
   D5), name + use-count, in their language.
2. By-tag (`/tags/{slug}`) — the posts / blog pages tagged with it that the
   resident may already read (the post's own `Read` decision is the gate).
3. Composer tag input (post + blog-page editors) — free-text attach with
   autocomplete as they type.

## Invariants (pinned for the lane)

The `TG` lane **owns** nine invariants (C-TG·1 … C-TG·9 — the behavioral
rules this lane is the first to state for tags) and **calls on** the frozen
ADR 0006 / ADR 0004 pins (C1, C3, ADR 0004 §B.1) that must keep holding. Every
pin cites its decision in ADR 0044 (D1–D8); the seam tests (pinned by Part 2,
U2) will name each by id.

| # | Pin | How the lane holds it |
|---|---|---|
| **C-TG·1** (TG-owned) | A tag is a **label, never a gate**: no tag grants any access on any object, and no tag reveals a subject's existence behind unread content. The access model is untouched — no new `AccessAction`, no new `AccessVia` value, no new `Decide()` branch, no audience on a tag (ADR 0044 D2, D5; Consequences). | The three read surfaces are all callers of the one base query (C-TG·2); the tag itself is computed *over* the content's existing `Read` decision, never a gate of it. Tests assert the absence (F3 / F4). |
| **C-TG·2** (TG-owned) | The **base read query** is *"the set of `Tag` rows used on at least one `Post` / `PageKind.User` `Page` the viewer may already read"*; the tag list, the by-tag results, and autocomplete all **derive from it**, and autocomplete is **capped at ≤ 10** (ADR 0044 D5). | Privacy is structural, not a per-surface check: because the base query is already scoped to readable content, a tag used only behind unread content never appears in any of the three surfaces (D5). Global tag enumeration is excluded by design (it would leak a subject's existence). |
| **C-TG·3** (TG-owned) | **By-tag group-lane exclusion:** a group post's `TagIds` never make it appear in a non-member's by-tag results; the post's **own** `Read` decision (the ADR 0013 membership lane; the ADR 0035 `PostReadDecision` seam) is applied **before** the post is returned (ADR 0044 D5; Consequences). | The by-tag read reuses the post's existing `Decide()` / `MatchGroups` pass — the tag grants nothing and adds no feed lane (ADR 0013 lane exclusivity unchanged). Test asserts the exclusion (F3). |
| **C-TG·4** (TG-owned) | **`Slug` is the business key** — the literal typed string **lowercased + trimmed** (not accent-folded, not URL-canonicalized, not cross-language-merged), validated to a safe charset (≤ 64 chars); `Name` / `TagTranslation.Name` are **display** values resolved per-viewer (ADR 0044 D3). | `sanitation` and `Hygiène` are two tags, both valid, both in the list — a tag is **one document with per-language names**, not N near-duplicates (D3). The write seam derives the `Slug` from the typed string; the author never types a slug directly. |
| **C-TG·5** (TG-owned) | **The creator owns the name:** a non-creator attacher **cannot** add / overwrite a `TagTranslation`; the tag's `CreatedBy` ∪ GlobalAdmin can (an `AccessVia.Owner` / `AccessVia.Admin` audit tag) (ADR 0044 D4). | Attach is free (the object's existing edit standing, D4); translate is the creator's lane — "the name is the creator's artifact," the ADR 0009 / 0026 rule carried to tags. Standing is resolved in Core (the `CanAttach*` / `CanTranslateTag` probes), never from a UI affordance. Tests F6 / F7 / F8. |
| **C-TG·6** (TG-owned) | **Blog-only on pages:** `Page.TagIds` is non-empty only on `PageKind.User` pages; the `PageKind.System` write lane **refuses** a non-empty `TagIds` set (the shape permits it, the write lane rejects it) (ADR 0044 D2, D6). | About / Terms / Help / Privacy *are* their subject (platform chrome, ADR 0040 / 0043) — tagging them is incoherent (D6). The `System`-page composer does not render the field; the `PageService` create / update lane rejects a non-empty set. Test F5. |
| **C-TG·7** (TG-owned) | **No seeding:** a fresh instance has **zero** tags; there is no seeder row, no "suggested tags" pack, no admin tag-management surface beyond the GlobalAdmin break-glass standing (ADR 0044 D8). | Free-for-all: tags come into existence only when an author attaches one (D4's create branch). The `/tags` page is empty on a fresh instance until content exists. Tests F12 + F11 (no re-seed on warm reboot). |
| **C-TG·8** (TG-owned) | **Reads emit no `AccessAudit` row:** `ListForActorAsync` / `ListPostsByTagAsync` / `ListPagesByTagAsync` / `SuggestAsync` are plain reads with **no authorization gate of their own and no audit row** — the ADR 0022 / 0026 "a read, not a decision" pin, C-M3·1 carried over (ADR 0044 D7). | The access decision is the *content's* existing `Read`, not the tag's; the row is only ever rendered on a surface that already required the referenced content's reach. A dedicated `Can` / audit on a tag read would be a redundant second decision (the C1 / C3 split, below). Tests `List_EmitsNoAuditRow` / `Suggest_EmitsNoAuditRow`. |
| **C-TG·9** (TG-owned) | **Writes audit exactly one row:** attach / create / translate each resolve standing (→ `UnauthorizedAccessException` when denied) and, on success, write **one** hand-written `AccessAudit` row in the caller's session, single `SaveChangesAsync` — `tag.attach` (`TargetKind = "post"` / `"page"`), `tag.create` (`TargetKind = "tag"`), `tagtranslation.add` (`TargetKind = "tag"`); `Via` = `Owner` / `Admin` (ADR 0044 D7; the C3 idiom). | The C3 idiom (audit-on-write in the same transaction) applied to the tag lane. Detach is part of the same edit lane (re-saving the object's `TagIds`) (D7). There is **no** "delete tag" seam — a tag whose content is all removed is simply unreachable by the base query (D7 / D8). Tests F1 + the three audit-row tests. |
| **C1** (ADR 0006) | Empty audience denies; a tag **never widens** a post's audience (ADR 0044 D2, D5). | The by-tag read reuses the post's existing `Decide()`, not a new branch; `Tag` carries no `Audience` (D1). |
| **C3** (ADR 0006) | Audit always on for the **write** lanes (attach / create / translate); **reads** carry no row (ADR 0044 D7, realized as C-TG·8 / C-TG·9). | One `AccessAudit` row per write in the same transaction (C-TG·9); zero rows on reads (C-TG·8). |
| **ADR 0004 §B.1** | `Tag` / `TagTranslation` are **Marten-native** POCOs, registered in `TagDocTypes` (new parallel surface), delta-detected, idempotent, **no seeding**, no EF migration; `Post.TagIds` / `Page.TagIds` are **additive fields** — a pre-existing post's `TagIds` read back **empty** after a warm re-boot (ADR 0044 D1, D2, D8). | The `M3DocTypes` / `MediaDocTypes` registration pattern exactly (D1 Consequences: additive, no migration). The `TagId, LanguageCode` pair is the DB-enforced business key (a `UniqueIndex`, the ADR 0026 convention). Test F11. |

**Count: 12** (nine TG-owned + three called-on pins). This count (and the
invariant-pin per row) is the input U2 needs to name the seam-test list and
the acceptance gate without re-deriving them.

## FACES (pinned, 12)

Each row is a *resident-visible outcome* (or an *absence* outcome) that the
seam tests (Part 2, U2) and the e2e pins must cover. "Pinned" means the
invariant(s) in the right column are the single authority for the outcome;
the test names Part 2 pins must reference those pins by id.

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| F1 | A post author attaches a tag to their own post — free (the object's existing edit standing); the audit trail gains `tag.attach` (and `tag.create` when the `Slug` is new) and **no** other row. | C-TG·9 (one audit row per write) + C-TG·4 (the `Slug` is derived from the typed string) |
| F2 | A **second** author attaches the same `Slug` to their own post — the existing `Tag` doc is **reused**; the second author is **not** its `CreatedBy` and gains no translate standing. | C-TG·4 (`Slug` business key) + C-TG·5 (creator ∪ GlobalAdmin only) |
| F3 | A **group post's** tag does **not** surface in a non-member's tag list, by-tag results, or autocomplete — the group post's `TagIds` never make the post appear in a non-member's by-tag view. | C-TG·1 (label, never a gate) + C-TG·3 (the post's own `Read` decision is applied first) |
| F4 | A tag used **only** on content a viewer cannot read is **invisible** to them in the tag list, in the by-tag results, and in autocomplete — no title, no name, no "hidden" placeholder. | C-TG·1 + C-TG·2 (the base query is the privacy boundary) |
| F5 | A `PageKind.System` page **refuses** a non-empty `TagIds` set — the `System`-page composer does not render the tag field and the write lane rejects one. | C-TG·6 (blog-only on pages) |
| F6 | The **creator** sets (or overwrites) a `TagTranslation` for their tag in another language — one `tagtranslation.add` audit row, `Via = Owner`. | C-TG·5 (creator owns the name) + C-TG·9 (one audit row per write) |
| F7 | A **non-creator** attacher (a resident who merely attached the tag to their own post) **cannot** reword the tag's display name — the "add a translation" affordance is absent for them. | C-TG·5 (the name is the creator's artifact) |
| F8 | A **GlobalAdmin** rewords any tag (the break-glass standing) — one `tagtranslation.add` row, `Via = Admin`. | C-TG·5 (creator ∪ GlobalAdmin) + C-TG·9 |
| F9 | A `de`-preferring actor typing `hy` in the composer gets the `de` name **`hygiène`** in the autocomplete even if the `Slug` is `sanitation` — the display name is resolved in the **viewer's language**; the `Slug` match is the fallback branch. | C-TG·2 (the base query, display resolution) + C-TG·4 (`Slug` is identity, `Name` is display) |
| F10 | Autocomplete is **capped** — at most 10 suggestions, regardless of how many readable tags match the prefix. | C-TG·2 (the ≤ 10 cap) |
| F11 | A **pre-existing** post's `TagIds` read back **empty** after a warm re-boot — the additive field landed with no re-seed and no data loss (the ADR 0004 §B.1 no-reseed pin). | ADR 0004 §B.1 (additive, idempotent, no seeding) |
| F12 | A **fresh instance** has **zero** tags and an **empty** `/tags` page — no seed rows, no "suggested tags" pack, nothing to browse until a resident attaches. | C-TG·7 (no seeding) |

**FACES count: 12.** This count (and the invariant-pin per row) is the input
U2 needs to name the 24 pinned seam tests and the three-test acceptance gate
(closed-loop / handoff / part-vs-whole) without re-deriving them.
