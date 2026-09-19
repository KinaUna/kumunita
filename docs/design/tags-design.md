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

## Seams & contracts (Part 2, written by U2)

### 2.0 Preambles — what this section pins, and what wins on conflict

Every C# fragment below is **exact**: parameter lists, return types, and
namespaces are the contract U3–U12 must implement against. If a later unit
discovers an implemented signature that does not exist verbatim here, the
drift-guard (§2.6) applies: **this file wins**; the unit updates this file in
the same commit and appends a `## U<m> — Drift pause` note to
`docs/plans-milestones/in-progress/tags/tags-handoff-notes.md` (unit-series
rule §6 in the register).

Namespace conventions:

- New bounded context: **`Kumunita.Core.Tags`** (`Tag`, `TagTranslation`,
  `ITagService` / `TagService`, `TagItem`) — the ADR 0011 shared-id-doc
  shape, exactly the ADR 0011 `MediaObject` model (D1).
- Registration surface: `Kumunita.Core.TagDocTypes` (the `M3DocTypes` /
  `PageDocTypes` pattern; U3 lands it).
- Frozen surfaces the lane *calls but does not modify*:
  `Kumunita.Core.Authorization.IAuthorizationService`, the
  `Kumunita.Core.UserInfo.IUserInfoService` read seams, and
  `Kumunita.Core.Localization.ITranslationProvider` (display-name resolution,
  ADR 0005). **U2 adds no method to any frozen interface** (the
  `PostService_MakesNoNewModerateCall` pin, §2.4 #24).
- Web-side composition: `Kumunita.Web.Controllers` / `Kumunita.Web.Models`
  (never in `Kumunita.Core`).

**Count reconciliation (U1 → U2):** U1's body pins **12 invariants**
(nine TG-owned C-TG·1…C-TG·9 + C1, C3, ADR 0004 §B.1) and **12 FACES**
(F1–F12). U2 confirms both counts and pins the §2.4 test list accordingly —
**24 named tests**, each anchored to an invariant id and/or FACES row from
Part 1. No C-TG·10 is introduced by this section.

**Roles idiom:** the seam signatures below write `IReadOnlySet<Role>`
(register U2 verbatim); in this repo `Role` is the claim-string type
(`Kumunita.Core.Identity.Roles` constants — `Member` / `Moderator` /
`GlobalAdmin` / `Translator`), and the established codebase idiom is
`IReadOnlySet<string> actorRoles` (e.g.
`PostService.AddPostTranslationAsync`, `PageService.CheckEditStanding`).
U5 lands the signatures against the repo idiom; the *member set* (11 members)
and the *standing rules* are what §2.6 freezes.

### 2.1 Frozen seam list (exact C#)

Namespace `Kumunita.Core.Tags`. The `TagService` public surface — **11
members** (7 async lane methods + 3 standing probes + the `TagItem` record
type it returns), verbatim:

```csharp
namespace Kumunita.Core.Tags;

/// <summary>
/// The single feature service for the <c>TG</c> lane (ADR 0006-D: the service
/// owns the decision, not the Web). Composes **only** the frozen modules —
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> (the
/// content's existing <c>Read</c> decision, never a new branch), the frozen
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService"/> read seams, and
/// <see cref="Kumunita.Core.Localization.ITranslationProvider"/> (display-name
/// resolution, ADR 0005) — plus its own <c>IDocumentStore</c> for the write
/// lanes' C3 audit rows. Never opens a new seam on any frozen interface.
/// </summary>
public interface ITagService
{
    // ── Write lane (C-TG·9: one AccessAudit row per write; C-TG·5 standing) ──

    /// <summary>
    /// Attach / detach the <paramref name="slugs"/> set on a post in the
    /// caller's in-flight session (C3). Standing = the post's existing edit
    /// lane (ADR 0014 / 0016 author-only; ADR 0013 group lane — D4). A
    /// not-yet-existing <c>Slug</c> is created: the actor becomes
    /// <c>CreatedBy</c>, the typed string (lowercased + trimmed, C-TG·4) the
    /// base <c>Name</c>. Writes one <c>tag.attach</c> row (and one
    /// <c>tag.create</c> row per newly created tag) — nothing else.
    /// </summary>
    Task<IReadOnlyList<Tag>> AttachToPostAsync(
        string postId, IReadOnlyList<string> slugs,
        string actorId, IReadOnlySet<Role> roles, IDocumentSession session);

    /// <summary>
    /// Same shape for a <c>PageKind.User</c> blog page (the ADR 0040 author ∪
    /// GlobalAdmin standing). A <c>PageKind.System</c> page with a
    /// non-empty <paramref name="slugs"/> set is **refused**
    /// (<c>UnauthorizedAccessException</c> / <c>ArgumentException</c>) —
    /// C-TG·6, D6.
    /// </summary>
    Task<IReadOnlyList<Tag>> AttachToPageAsync(
        string pageId, IReadOnlyList<string> slugs,
        string actorId, IReadOnlySet<Role> roles, IDocumentSession session);

    /// <summary>
    /// Add / overwrite the <paramref name="languageCode"/> translation row of
    /// tag <paramref name="tagId"/> (the <c>(TagId, LanguageCode)</c>
    /// business key — overwrites on conflict). Standing: the tag's
    /// <c>CreatedBy</c> ∪ GlobalAdmin only (C-TG·5, D4). One
    /// <c>tagtranslation.add</c> row, <c>Via</c> = <c>Owner</c> / <c>Admin</c>.
    /// </summary>
    Task<TagTranslation> AddTagTranslationAsync(
        string tagId, string languageCode, string name,
        string actorId, IReadOnlySet<Role> roles, IDocumentSession session);

    // ── Read lane (C-TG·8: plain reads, no audit row; C-TG·2 base query) ──

    /// <summary>
    /// The tag list (F12 empty on a fresh instance): distinct
    /// <see cref="Tag"/> rows used on ≥ 1 post / <c>PageKind.User</c> page
    /// the actor may already read (the C-TG·2 base query), each with a
    /// use-count and the display name resolved to the actor's language
    /// (ADR 0005 preference order, D5). No <c>AccessAudit</c> row (C-TG·8).
    /// </summary>
    Task<IReadOnlyList<TagItem>> ListForActorAsync(string actorId);

    /// <summary>
    /// The by-tag post results (F3 / F4): the actor-readable posts whose
    /// <c>TagIds</c> contains the tag resolved from <paramref name="slug"/>;
    /// the post's own <c>Read</c> decision is applied **before** the post is
    /// returned (C-TG·3, D5). No <c>AccessAudit</c> row (C-TG·8).
    /// </summary>
    Task<IReadOnlyList<Post>> ListPostsByTagAsync(string slug, string actorId);

    /// <summary>
    /// The by-tag blog-page results: the actor-readable
    /// <c>PageKind.User</c> pages whose <c>TagIds</c> contains the tag.
    /// No <c>AccessAudit</c> row (C-TG·8).
    /// </summary>
    Task<IReadOnlyList<Page>> ListPagesByTagAsync(string slug, string actorId);

    /// <summary>
    /// Autocomplete (F9 / F10): the C-TG·2 base query filtered by
    /// <c>starts_with(displayName, prefix) OR starts_with(slug, prefix)</c>,
    /// where <c>displayName</c> is the name resolved in the viewer's language;
    /// **capped at ≤ 10**. No <c>AccessAudit</c> row (C-TG·8).
    /// </summary>
    Task<IReadOnlyList<TagItem>> SuggestAsync(string prefix, string actorId);

    // ── Standing probes (C-TG·5; a display pin, not a gate — the real deny
    //    is the write-lane re-check, the <c>PostService.CanAddTranslation</c>
    //    / <c>PageService.CanTranslatePage</c> idiom) ──

    bool CanAttachToPost(Post post, string actorId, IReadOnlySet<Role> roles);
    bool CanAttachToPage(Page page, string actorId, IReadOnlySet<Role> roles);
    bool CanTranslateTag(Tag tag, string actorId, IReadOnlySet<Role> roles);
}

/// <summary>
/// The read-lane result row (the tag list / autocomplete shape, D5): the tag,
/// its use-count over the actor's readable content, and the display name
/// resolved to the actor's language.
/// </summary>
public sealed record TagItem(Tag Tag, int UseCount, string DisplayedName);
```

**The two object services gain no new public methods.** The tag lane is
*inside* their existing edit lanes: U4 adds the `TagIds` field to the
`Post` / `Page` POCOs and threads it through `PostService`'s and
`PageService`'s existing create / update paths (plus the `System`-page
refusal on `PageService`'s write lane, C-TG·6) — **without adding any new
public method to `PostService` or `PageService`**. Standing for *attach* is
the object's own existing edit standing (D4); the tag-specific standing
(translate) lives in `TagService` only.

**Registration surface** (U3 lands it, mirroring `M3DocTypes` /
`PageDocTypes`):

```csharp
namespace Kumunita.Core;

/// <summary>
/// The <c>TG</c> bounded context's Marten-native document registration
/// surface (ADR 0004 §B.1, D1) — the parallel surface to
/// <see cref="M1DocTypes"/> / <see cref="M3DocTypes"/> /
/// <see cref="MediaDocTypes"/> / <see cref="PageDocTypes"/> for the new
/// <c>Kumunita.Core.Tags</c> context (ADR 0011 shared-id-doc shape). Both
/// documents use the conventional <c>string</c> <c>Id</c> identity; the
/// <c>(TagId, LanguageCode)</c> pair is the DB-enforced business key
/// (D1; the ADR 0026 <c>PostTranslation</c> / <c>PageTranslation</c>
/// convention).
/// </summary>
public static class TagDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        opts.Schema.For<Tags.Tag>();
        opts.Schema.For<Tags.TagTranslation>()
               .UniqueIndex("tg_tr_uidx_tag_lang",
                            t => t.TagId, t => t.LanguageCode);
    }
}
```

### 2.2 New TG-owned Core types (exact C#)

Namespace `Kumunita.Core.Tags`. Both documents are **Marten-native** POCOs
with the conventional `string Id` identity (ADR 0004 §B.1, D1; the
`FeatureSchemaBase` carve-out is **not** used — it is reserved for
operator-written tables).

```csharp
namespace Kumunita.Core.Tags;

/// <summary>
/// A tag (TG, D1/D3): a shared id-doc referenced by <see
/// cref="Kumunita.Core.Posts.Post"/> and <see
/// cref="Kumunita.Core.Pages.Page"/> via their <c>TagIds</c> fields (the ADR
/// 0011 <c>MediaObject</c> shape). <see cref="Slug"/> is the **business
/// key** — the label's language-neutral identity (C-TG·4, D3).
/// <see cref="Name"/> is the **base** display name (the creator's own
/// spelling); <see cref="LanguageCode"/> is the BCP-47 code the creator
/// **authored** it in (ADR 0018 authored-in idiom). <b>No
/// <c>Audience</c>, no standing surface of its own</b> (D5) — the tag's
/// visibility is derived from the content that references it.
/// </summary>
public sealed class Tag
{
    public string Id { get; set; } = string.Empty;            // surrogate PK
    public string Slug { get; set; } = string.Empty;          // business key (C-TG·4) — non-blank
    public string Name { get; set; } = string.Empty;          // base display name — non-blank
    public string LanguageCode { get; set; } = string.Empty;  // authored-in (ADR 0018) — non-blank
    public string CreatedBy { get; set; } = string.Empty;     // translation-standing owner (C-TG·5) — non-blank
    public DateTimeOffset Created { get; set; }
}

/// <summary>
/// A per-language display name for a tag (D1):
/// <b><c>GroupTranslation</c> (ADR 0026) minus <c>Description</c></b> — a tag
/// has a name only, no description. One row per <c>(TagId, LanguageCode)</c>
/// pair (the <c>TagDocTypes</c> unique index enforces it at the DB layer;
/// re-adding overwrites — the D4 translate lane).
/// </summary>
public sealed class TagTranslation
{
    public string Id { get; set; } = string.Empty;         // surrogate PK
    public string TagId { get; set; } = string.Empty;      // parent Tag — non-blank
    public string LanguageCode { get; set; } = string.Empty; // — non-blank
    public string Name { get; set; } = string.Empty;       // — non-blank
    public string AuthorId { get; set; } = string.Empty;   // the actor who set it — non-blank
    public DateTimeOffset Created { get; set; }
}
```

**Additive fields on existing documents** (ADR 0004 §B.1, D2 — the
`Post.Status` / `GroupId` / `LanguageCode` / `ImageIds` history):

```csharp
// Kumunita.Core.Posts.Post — additive, default empty; one field covers both
// the community and the group post lane (ADR 0013):
public IReadOnlyList<string> TagIds { get; set; } = [];

// Kumunita.Core.Pages.Page — additive, default empty; populated **only** on
// PageKind.User (blog) pages (C-TG·6, D6) — a PageKind.System page's
// TagIds is **always empty** (the shape permits it, the write lane refuses it).
public IReadOnlyList<string> TagIds { get; set; } = [];
```

> **Shape note:** the register's U2 §2.2 shorthand says `string[]`; this repo's
> additive array fields (`Post.ImageIds`, ADR 0025) are
> `IReadOnlyList<string>` with an empty-collection initializer — U4 lands the
> field in the repo idiom (the *additive, default-empty* pin is what §2.6
> freezes, not the literal array type).

### 2.3 Standing + privacy rule (the 5-state table)

**Attach / translate standing matrix** (D4 — "attach is free; a tag's
translations belong to its creator"). "Free" = the object's *existing* edit
standing; a denied actor throws `UnauthorizedAccessException` **before**
anything is stored (C-TG·9).

| # | Action | Standing that qualifies | Denied outcome | Via (audit tag) |
|---|---|---|---|---|
| 1 | Attach to a **community post** | The post's **author** (ADR 0014 / 0016 author-only lane) ∪ GlobalAdmin (the ADR 0016 edit-lane shape) | `UnauthorizedAccessException` | `Owner` / `Admin` |
| 2 | Attach to a **group post** | The post's **author** (ADR 0013 group lane); no component-moderator standing (ADR 0007) | `UnauthorizedAccessException` | `Owner` / `Admin` |
| 3 | Attach to a **`PageKind.User` blog page** | The page's **author** ∪ GlobalAdmin (ADR 0040) | `UnauthorizedAccessException` | `Owner` / `Admin` |
| 4 | Attach to a **`PageKind.System` page** with a non-empty `TagIds` set | **Refused outright** (C-TG·6, D6) — no standing qualifies | `UnauthorizedAccessException` (or `ArgumentException`) | — (no write, no row) |
| 5 | **Translate** (add / overwrite a `TagTranslation`) | The tag's **`CreatedBy`** ∪ **GlobalAdmin** (break-glass) — the ADR 0009 / 0026 "the name is the creator's artifact" rule (C-TG·5, D4) | `UnauthorizedAccessException` | `Owner` / `Admin` |

Attach on a *new* `Slug` additionally writes the `tag.create` row (the actor
becomes `CreatedBy`) — the C-TG·9 two-row shape pinned by F1.

**Reads — the 4-shape table** (C-TG·1 / C-TG·2 / C-TG·3, D5). All four
shapes reduce to the **one** base query: *"the set of `Tag` rows used on ≥ 1
`Post` / `PageKind.User` `Page` the actor may already read."* No shape emits
an `AccessAudit` row (C-TG·8); no shape widens a content's audience (C1).

| # | Surface | Shape (over the base query) |
|---|---|---|
| 1 | **Tag list** (`ListForActorAsync`) | Distinct `Tag` rows of the base query, each with a use-count and the display name resolved to the actor's language (ADR 0005 order: preferred → `TagTranslation` → base `Name`). Empty on a fresh instance (C-TG·7, F12). |
| 2 | **Posts by tag** (`ListPostsByTagAsync`) | The base query's posts whose `TagIds` contains the tag; each post passed through its **own** `Read` decision (the ADR 0013 group lane / ADR 0035 `PostReadDecision` seam) **before** it is returned (C-TG·3, F3/F4). |
| 3 | **Pages by tag** (`ListPagesByTagAsync`) | The base query's `PageKind.User` pages whose `TagIds` contains the tag; same per-page `Read` decision (C-TG·3). |
| 4 | **Autocomplete** (`SuggestAsync`) | The base query filtered by `starts_with(displayName, prefix) OR starts_with(slug, prefix)` — `displayName` resolved in the viewer's language (F9); **capped at ≤ 10** (F10). |

A tag used only on content the viewer cannot read is **absent from all four
shapes** — no title, no name, no "hidden" placeholder (F4, the anti-leak pin
of D5).

### 2.4 Pinned seam tests (exact names)

File: `tests/Kumunita.Core.Tests/TagServiceTests.cs`. All **24** names below
are **pinned** by this section (the register's U4–U10 units land them; none
may be renamed, renumbered, added to, or dropped). Each name carries the
FACES row and/or invariant anchor from Part 1:

1. `F1_AttachFreeOnOwnPost` — F1, C-TG·9, C-TG·4.
2. `F2_SameSlugSecondAuthorReusesTag` — F2, C-TG·4 (D3).
3. `F2_SameSlugSecondAuthorNotCreatedBy` — F2, C-TG·5 (D4).
4. `F3_GroupPostTagInvisibleToNonMember_ByTag` — F3, C-TG·3 (D5).
5. `F3_GroupPostTagInvisibleToNonMember_Suggest` — F3, C-TG·1, C-TG·3.
6. `F4_TagUsedOnlyOnUnreadContentInvisible_List` — F4, C-TG·2.
7. `F4_TagUsedOnlyOnUnreadContentInvisible_ByTag` — F4, C-TG·1, C-TG·2.
8. `F4_TagUsedOnlyOnUnreadContentInvisible_Suggest` — F4, C-TG·1, C-TG·2.
9. `F5_SystemPageRefusesNonEmptyTagIds` — F5, C-TG·6 (D6).
10. `F6_CreatorSetsTranslation` — F6, C-TG·5, C-TG·9.
11. `F7_NonCreatorAttacherCannotReword` — F7, C-TG·5.
12. `F8_GlobalAdminRewordsAnyTag` — F8, C-TG·5 (break-glass), C-TG·9.
13. `F9_AutocompleteMatchesViewerLanguage_DisplayName` — F9, C-TG·4 (D3/D5 display).
14. `F9_AutocompleteMatchesViewerLanguage_Slug` — F9, C-TG·4 (the slug fallback branch).
15. `F10_AutocompleteCappedAtTen` — F10, C-TG·2 (the ≤ 10 cap).
16. `F11_PreExistingPostTagIdsReadBackEmptyAfterReboot` — F11, ADR 0004 §B.1 (D2, no re-seed).
17. `F12_FreshInstanceHasZeroTags` — F12, C-TG·7 (D8).
18. `Attach_WritesOneAuditRow_tag_attach` — C-TG·9 (D7), F1.
19. `Create_WritesOneAuditRow_tag_create` — C-TG·9 (D7), F1, C-TG·4.
20. `Translate_WritesOneAuditRow_tagtranslation_add` — C-TG·9 (D7), F6/F8.
21. `List_EmitsNoAuditRow` — C-TG·8 (D7).
22. `Suggest_EmitsNoAuditRow` — C-TG·8 (D7).
23. `Slug_Derivation_Lowercase_Trim_Charset` — C-TG·4 (D3).
24. `PostService_MakesNoNewModerateCall` — the ADR 0006-D lane pin: the tag
    lane composes only `IAuthorizationService` + the frozen `IUserInfoService`
    read seams, never a new seam on a frozen interface.

> **Note to U4 → U12 (unit numbers from the plan register):** U4 lands the
> F5 / F11 / F12 group, U5 the F1 / F2 / F6 / F7 / F8 + audit-row +
> `Slug`-derivation group, U6 the F3 / F4 / F9 / F10 + no-audit-row + lane-pin
> group, and U10 re-verifies the full list at the Web layer. **Renaming any
> of the 24 after the owning unit lands it is a drift event** (§2.6).

### 2.5 Acceptance gate (U12 records)

The three-test shape (mirroring M3 §2.5):

| # | Test | Shape (the TG reading of the M2/M3 lane) |
|---|---|---|
| 1 | **Closed loop** | The author attaches `sanitation` to a post → the tag appears in the author's tag list **and** by-tag view on the next request; the audit trail gains exactly one `tag.attach` row + one `tag.create` row (C-TG·9, F1). |
| 2 | **Handoff** | A group member is **added after the post was created** and sees the tag on the **next** request — strong consistency (the ADR 0004 §B.1 / Marten live-read shape, F3's flip); the *creator reword* case (a second language name set by the creator surfaces to all viewers on their next read) is the "handoff to the creator" reading of the same pin (F6, C-TG·5). |
| 3 | **Part-vs-whole** | The 24-test list in §2.4 is the **whole**; closed-loop + handoff are the **parts**; all — plus the ADR 0006 (C1 / C3) and ADR 0004 §B.1 inherited anchors re-run unchanged — must pass together for the gate to record. U12's record cites the *actual* landed test names (drift lane: if a U2-pinned name did not land verbatim, U12 updates §2.4 in the same commit and records a one-line drift in the handoff note). |

**The gate is recorded by U12** (per the register: U11 is the fresh-boot
manual pass; U12 is "run + record the acceptance gate"; U13 is the docs-sync
close unit).

### 2.6 Drift-guard (frozen once written)

The following pins are **frozen** in this doc, and any mismatch is a
`## U<m> — Drift pause` per unit-series rule §6 (unit-series rules in the
register): **this file wins** — the unit that finds a mismatch pauses,
records the pause (what it found, the exact lines/files, and what it needs),
and does not guess.

- The **12** invariants from Part 1 (C-TG·1 … C-TG·9, C1, C3, ADR 0004 §B.1) —
  the *numbers* (not the prose) are stable for the rest of the lane; rename /
  renumber is a breaking change. Part 2's §2.4 / §2.5 names hang off them.
- The **12** FACES rows (F1–F12) in Part 1 — the *count* is a handoff field
  (U1 → U2, and forward); a new FACES row (F13+) is added **only** by the
  unit that ships the outcome it pins, in the same commit as the feature.
- The **`TagService` 11-member public surface** (§2.1: `AttachToPostAsync`,
  `AttachToPageAsync`, `AddTagTranslationAsync`, `ListForActorAsync`,
  `ListPostsByTagAsync`, `ListPagesByTagAsync`, `SuggestAsync`,
  `CanAttachToPost`, `CanAttachToPage`, `CanTranslateTag` + the `TagItem`
  record) — names + signatures + the `TagItem(Tag, int, string)` shape,
  frozen once U5 / U6 land them. Renaming a method or reshaping a record is a
  drift event (U5/U6 own the shapes; U7–U10 consume them, no re-shaping).
- The **`Tag`** 6-field shape (Id, Slug, Name, LanguageCode, CreatedBy,
  Created) + the **`TagTranslation`** 6-field shape (Id, TagId, LanguageCode,
  Name, AuthorId, Created) + the **absence of any `Audience` field** on both
  (D1/D5) — frozen once U3 lands them.
- The **`Post.TagIds` / `Page.TagIds`** additive-field pin (default empty;
  `PageKind.System` pages always empty — C-TG·6) + the **`TagDocTypes`**
  `(TagId, LanguageCode)` unique-index pin — frozen once U3 / U4 land them.
- The **§2.3 tables** (the 5-row standing matrix + the 4-shape read table) —
  frozen once written (U2's commit); a new row is a drift event **or** a new
  FACES row (F13+), whichever the unit's change actually pins, in the same
  commit + the same drift note.
- The **24 test names** in §2.4 — frozen once the owning unit (U4 / U5 / U6)
  lands the file. Renaming or re-scoping a name is a drift event; the unit
  updates §2.4 in the same commit and appends a drift note to the handoff.
- The **`PostService` / `PageService` no-new-methods** pin (§2.1) — the tag
  lane gains no public method on either service; a new method is a drift
  event (D4 — attach is *inside* their existing edit lanes).

> **U2 records, here, the frozen counts of Part 2:** the 12 invariants (not 11,
> not 13), the 12 FACES (F1–F12), the **11-member** `TagService` surface
> (7 lane methods + 3 probes + 1 record), the **24** test names (not a smaller
> or larger set), the **2** tables (5 + 4 rows), and the **3** acceptance-gate
> tests (closed-loop / handoff / part-vs-whole) are the *frozen counts* of
> Part 2.
