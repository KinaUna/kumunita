# Pages (`PG`) — sealed unit register

> **In progress.** This is the **lane plan** (the secondary register tier) for the
> **Pages** lane — a **hierarchical, audience-restricted, translatable knowledge
> tree** that absorbs the existing `LocalizedPage` static-page lane. The
> **primary** reference tier (the exact C# seams + the design decisions) is the
> design doc `docs/design/pages-design.md`; the **scratch** tier is
> `docs/plans-milestones/done/pages/pages-handoff-notes.md` (one appended `## U#`
> section per unit, never rewritten).
>
> **What this is:** a follow-on lane over the existing static-page / audience /
> translation / rich-content lanes. It **reuses** the `Audience` doc
> (ADR 0001-B / 0036), the `IAuthorizationService.CanAsync(Read)` decision path
> (ADR 0006), the user-added-translation row shape (ADR 0022/0026/0029), the
> `MarkdownRenderer` + `bindRichEditor` authoring surface (ADR 0025 / 0031), the
> content-image / attachment idiom (ADR 0025 / 0034), and the ADR 0004 §B.1
> additive-field rule. **No new `AccessAction`**, **no new `AccessVia`**, **no
> new authorization path**, **no editor dependency**, **zero migrations for new
> fields**. **M4/M5/M6 stay Events / Projects / Portability.** ADR **0039** is
> this lane's decision record.
>
> **Sizing:** units are sized for a ~32K-context fresh agent one at a time, each
> with its own exit criteria, in the `RC` / `RE` / `ATT` style. **U00 is the
> sign-off gate** — it locks the **[PROPOSED]** decisions in the design doc
> before any code is written; every later unit codes against the *locked* text.
> **Sequencing invariant:** the destructive step (absorbing `LocalizedPage` →
> `Page`, U07) is **last** and only runs after the new surface (U01–U06) is green
> — the "no destructive step until the replacement is proven" discipline.

## Understanding (one paragraph)

The platform already renders **public, flat, admin-only** static pages
(`about`/`terms`/`help`) in the WYSIWYG editor. This lane turns that into a
**single page tree**: a `Page` doc with a **parent** (`ParentId` + `Slug`), an
**audience** (the *exact* post `Audience` — `null` = public, non-null =
grants/community), an **authored-in language** + `PageTranslation` rows (the
*exact* `PostTranslation` shape), a **mount point** (where a page appears in the
UI — `footer/community`, `help/account`), and an **author** with the standing
matrix (GlobalAdmin / community-Moderator / author). It is served by
`PageController` (`/pages` tree + `/pages/{path}` post view + compose/edit/
publish/move/delete), reusing the `PostToAuditableResource` adapter pattern
(`PageToAuditableResource`) so the **frozen** `IAuthorizationService` decides
access. The existing `StaticPagesController` routes become thin readers over the
tree, and `LocalizedPage` is retired *last*. **Every new field is ADR 0004 §B.1
additive; the only data move is the seeded-page absorb, and it is the final
unit.**

## The one thing every unit must respect

**Additive and reusing.** Each unit adds **no** second audience mechanism, **no**
second translation-row shape, **no** second renderer, **no** second editor
binding, **no** new `AccessAction` / `AccessVia` / authorization path, and **no**
editor dependency. The page's `Audience` *is* the post `Audience`; the
`PageTranslation` row *is* the `PostTranslation` shape; the body is rendered by
the *one* `MarkdownRenderer` and edited by the *one* `bindRichEditor`. The
**frozen** `IAuthorizationService` is the only decision path — `PG` adds an
*adapter*, not a *branch*. The **destructive** absorb is **U07 and only after
green**.

## Assumptions

- **Scope = one page forest** (`Kumunita.Core.Pages` + `Kumunita.Web`'s
  `PageController` + views). In: the `Page` + `PageTranslation` docs, the
  hierarchy + audience + translation + mount-point surface, the
  create/edit/publish/move/delete + translation write lanes, the `/pages` tree
  + `/pages/{path}` post view, the seeded `about`/`terms`/`help` pages, the
  mount-point resolver, and the reference-from-UGC link (free — the RC link
  idiom). **Out (→ future lane, own design doc + ADR):** full-text search over
  the tree (M6 owns search), version history / revision diffing, per-community
  separate trees, comments-on-a-page (discussion belongs in posts that *link*
  the page).
- **Absorb is the model** (design doc §3.1, [DECIDED]); the parallel retreat is
  documented but **not** planned as units.
- **The WYSIWYG editor + RC image + ATT attachment surface is complete** for a
  `Page` body (it is — `PreviewPage.cshtml` already hosts it; the composer
  reuses the same `textarea[data-rich-editor]`), so **no new editor work** is a
  unit.
- **Standing is the existing role matrix** (GlobalAdmin / Moderator /
  Translator) re-checked server-side in the `PageService` (the
  `AnnouncementService.CreateAsync` C3 pattern); the Web `[Authorize]` is a
  convenience pre-gate only.

## The units

### U00 — Sign-off + ADR 0039 + the `PG` milestone row  *(no code beyond the ADR)*

Lock the **[PROPOSED]** decisions in `docs/design/pages-design.md` into the
accepted **ADR 0039** (`docs/adr/0039-pages-hierarchy-audience-translations.md`):
the absorb (§3.1), the `Page` field set (§3.2), the hierarchy +
derived-path + cycle-guard + depth-cap model (§3.3), the page-audience default
(**non-public, community-visible — consistent with posts, §3.4**; amended
2026-09-17, originally "public, the one place pages differ from posts"),
the **soft-delete** (`IsDeleted` flag + `CanSeeAsync` filter, §3.7), the
standing matrix (§3.7), the mount-point-as-string resolver (§3.2/§3.8), and the
absorb-migration ordering (§3.9). Add the `PG` row to `Milestones.All` (a named
lane, `StatusNext` when work starts / `StatusDone` on ship — **not** a renumber)
**and** the README Roadmap in the *same* commit; keep `MilestonesTests.cs` in
step (the order + single-in-progress pin).
**Exit:** ADR 0039 accepted + the design doc's `[PROPOSED]` markers resolved to
`[DECIDED]` in the ADR's decision section; `Milestones.cs` + README +
`MilestonesTests` green; **no other code changed.**

### U01 — `Page` + `PageTranslation` docs + registration + DI seam

Add `Kumunita.Core.Pages`: `Page` (the §3.2 field set) + `PageTranslation`
(the ADR 0022 shape: `Id`/`PageId`/`LanguageCode`/`Title?`/`Body`/`AuthorId`/
`Created`). Register them on a new **`PageDocTypes`** surface (or `M3DocTypes`
— pick one, the design doc leaves it open): `Page` (conventional `Id`) +
`(ParentId, Slug)` unique index (the `GroupMembership` business-key convention;
root rows are `(null, Slug)`) + `PageTranslation` `(PageId, LanguageCode)`
unique index (the `PostTranslation` convention; an explicit short index name if
the auto-derived name exceeds Postgres's 64-char `NAMEDATALEN` limit, the
`ann_tr_uidx_ann_lang` precedent). Add `IPageService` → `PageService` (a
store-composing service, the `IAnnouncementService` shape) to
`DependencyInjection.cs` (the "AddTransient with the store injected" shape).
**Zero behavioral change** — the lane's first *structure* unit.
**Exit:** `dotnet build` green; `dotnet exec …Kumunita.Core.Tests.dll` green
(schema delta applied idempotently, the two unique indexes present, the
existing `LocalizedPage` surface **untouched**); the `Page`/`PageTranslation`
POCOs + registration are reviewed for the §3.2 provenance table (every field
named to an existing idiom).

### U02 — `PageService` read lanes + `PageToAuditableResource` + the standing matrix

Implement the read surface on `IPageService`: `GetByPathAsync(path)` (resolve
the derived path → the `Page`), `GetBySlugUnderParentAsync(parentId, slug)`
(tree browse), `GetTreeAsync()` (the forest), `GetTranslationsAsync(pageId)`
(ordered by `LanguageCode`, the ADR 0022 read), and `GetByMountPointAsync(slot)`
(the mount-point resolver). Add **`PageToAuditableResource`**
(`Kumunita.Core.Pages`, the `PostToAuditableResource` adapter verbatim:
`Id`/`Name`/`OwnerId = AuthorId`/`Audience` (**null allowed**)/`ComponentId`/
`TargetKind = "page"`). Implement the **standing matrix** (§3.7) on the write
lanes' gate: `CheckCreateStanding` / `CheckEditStanding` /
`CheckTranslateStanding` (GlobalAdmin ∪ Translator ∪ community-Moderator
scoped by `ComponentId`, the ADR 0029 lane) — each throwing
`UnauthorizedAccessException` (403) / `KeyNotFoundException` (404) exactly like
`AnnouncementService`. **DB-backed `PageServiceTests`**: the
`null`-audience-public branch, the `Community` branch (the `A0036_*` family
shape, a `PG_*` family), the grants branch, the cycle-guard + depth-cap, the
`GetByPath` resolution, the translation standing (the ADR 0029 matrix).
**Exit:** `dotnet exec …Kumunita.Core.Tests.dll` green with the `PG_*`
authorization family + the hierarchy/translation standing tests; the adapter
compiles against the **frozen** `IAuthorizationService` (no signature change,
ADR 0006 §A); **no Web code yet.**

### U03 — Write lanes (create / edit / publish / move / delete + translation)

Implement on `IPageService`: `CreateAsync`, `UpdateAsync`, `PublishAsync`
(Author-only, the ADR 0037 pin), `MoveAsync` (reparent — cycle-guard +
`Slug`-rewrite of the derived path), `DeleteAsync` (**soft-delete**: `IsDeleted
= true`, the ADR 0024 author-lane shape, filter in the read lanes), and
`AddTranslationAsync` (the ADR 0029 standing, the unique-index add-only rule).
Each stores its `AccessAudit` row **in the caller's session** (C3) —
`TargetKind = "page"`, `Action` `page.create`/`page.update`/`page.publish`/
`page.move`/`page.delete`/`page.translation.add`. Server-side parse of the body
for `ImageIds` / `AttachmentIds` (the RC U04/U05 + ATT U5 idiom — the client
never sends them). **`PageServiceTests`** additions: the create/edit standing
matrix, the publish author-only pin, the move cycle-guard, the soft-delete
filter (a deleted page is absent from `GetTreeAsync` / `GetByPathAsync`), the
translation add-only + unique-index behavior.
**Exit:** `dotnet exec …Kumunita.Core.Tests.dll` green; **every** write lane
re-checks standing server-side (the C3 single-source pin — a Web
`[Authorize]` is not the source of truth); the audit-row `Action`/`TargetKind`
shape is asserted in tests.

### U04 — `PageController` + the tree browse + the post view + the composer

Add `Kumunita.Web/Controllers/PageController.cs`:
- `GET /pages` — the **tree browse** (folders/files), `CanSeeAsync(Read)`-
  filtered (the C6 aggregate row, no private-page leakage).
- `GET /pages/{**path**}` — the **post view** (title + rendered body via the
  *one* `MarkdownRenderer` + the ADR 0027 chip-swap over the
  `PageTranslation` rows), `CanAsync(Read)`-gated; **404** on absent, **403**
  on denied (the announcement 404-vs-403 split, the RC serving-route
  convention).
- `GET /pages/new` + `POST /pages/new` — compose (title, body, parent picker,
  the **`AudienceEditorModel`** verbatim, the ADR 0018 language picker) —
  **WYSIWYG** (`bindRichEditor` + RC image + ATT attachment, the
  `PreviewPage.cshtml` wiring pointed at a `Page` body).
- `GET /pages/{id}/edit` + `POST /pages/{id}/edit` — the edit lane (round-trips
  the audience verbatim, the ADR 0036 `FromAudience`/`BuildAudience` shape).
- `POST /pages/{id}/publish` / `delete` / `move` — the standing-gated actions.
- The **mount-point resolver** in the layout: `footer/community` (the about
  slot), `help/account` (the account-help slot) resolve
  `GetByMountPointAsync(slot)` and `<a href>` it.
- **`PageControllerTests`** (NSubstitute `IPageService`, no live Postgres — the
  `AnnouncementControllerTests` shape): the route map, the 404-vs-403 split, the
  tree filter, the mount-point resolution, the composer/edit audience
  round-trip.
**Exit:** `dotnet exec …Kumunita.Web.Tests.dll` green; a resident can browse the
tree, open a page (post view), compose + edit a page in the WYSIWYG editor, set
an audience (public/community/grants), and the about slot renders the mounted
page; **`LocalizedPage` is still untouched.**

### U05 — Reference-from-UGC + the seeded default pages + the `/about` fallback

Wire the **reference** surface (free, but *named*): confirm a
`[About](/pages/about)` link in a post/reply/announcement body renders as a
link (it does — `MarkdownRenderer` + `IsSafeUrl` accept the relative `/pages/…`
path) and that **opening** it is a separate `CanAsync(Read)` decision
(a link in an authorized post does not imply the target page is authorized —
add a `PG_*` test for the link-present-but-target-denied case). **Seed the
three default pages** — `about`, `terms`, `help` — as `Page` docs carrying the
**current** `LocalizedPage` `en` bodies/titles (the seeder's existing
first-boot idiom, idempotent), `Audience = null` (public), `MountPoint` set
(`about` → `footer/community`), `LanguageCode = "en"`, so a fresh instance is
byte-identical to today for those three pages. **Retarget
`StaticPagesController`** — `/terms`, `/help`, `/about` read from the **tree**
(the `GetByPathAsync` shape) with the *existing* `/about` product-story
fallback when truly absent — **one store, not two**, but the old surface still
works against `LocalizedPage` as a **fallback** (not yet removed).
**Exit:** a fresh instance's `/about`/`/terms`/`/help` are byte-identical to
today; a post body can link a page and the link is gated separately; the seeded
pages are idempotent (boot twice, no duplicate rows).

### U06 — The translation lane live on pages (standing + display)

Make the **user-added translation** lane fully live on a `Page`: the
`AddTranslationAsync` form (the ADR 0029 standing: GlobalAdmin ∪ Translator ∪
community-Moderator scoped by `ComponentId`, the ADR 0029 matrix) on the post
view, the **ADR 0027 chip-swap** display (authored-in default + a chip per
`PageTranslation`, click to swap) reusing the existing post/announcement
translation-swap partial verbatim, and the **add-only** unique-index behavior
(the `ann_tr_uidx_ann_lang` short-name precedent). This is the lane's
multilingual completion — the M6 multilingual goal (ADR 0005) is "the platform
exercised in more than one language"; pages are the last UGC surface to gain
the lane.
**Exit:** a Translator can add a `fr`/`de`/… row on a page, the chip-swap
renders it, a second add of the same `(PageId, LanguageCode)` is rejected by
the unique index, and a community-Moderator's standing is the ADR 0029 matrix
(allowed on their community's page, denied on a flat/public page).

### U07 — Absorb complete: retire `LocalizedPage` (destructive, last)

**Only after U01–U06 are green:** delete the `LocalizedPage` doc + its
`M1DocTypes` registration + `ITranslationProvider.GetPageAsync` /
`FindPageByImageIdAsync` + `ILocalizationService.UpsertPageAsync` + the
`StaticPagesController`'s *fallback-to-`LocalizedPage`* path (it now reads the
tree) + any `KnownTranslationKeys` page-string entries that were page-specific.
The old `StaticPagesController` routes **remain** (they now resolve to the
tree); only the *old store path* is removed. **The data migration is a no-op
in effect** — the seeded pages (U05) already carry the `en` bodies, so removing
`LocalizedPage` loses nothing a resident can see.
**Exit:** `dotnet build` + **both** test assemblies green; `grep` shows zero
remaining `LocalizedPage` references in `src/` (only the ADR/design-doc
historical mentions); `/about`/`/terms`/`/help` still resolve and render; the
about slot still mounts the page; the ADR 0039 "Consequences" note
("`LocalizedPage` retired in U07") is accurate.

## Register

| Unit | Deliverable | Exit (green) | Status |
|---|---|---|---|
| U00 | ADR 0039 + `PG` milestone + README + `MilestonesTests` | docs/adr green, no other code | ☐ |
| U01 | `Page`/`PageTranslation` docs + registration + DI | Core tests green, indexes present | ☐ |
| U02 | read lanes + `PageToAuditableResource` + standing matrix | `PG_*` auth family green | ☐ |
| U03 | write lanes (create/edit/publish/move/delete + translation) + C3 audit | write-lane tests green | ☐ |
| U04 | `PageController` + tree + post view + composer (WYSIWYG) | Web tests green, E2E browse/compose | ✓ (E2E browse/compose is live-run only) |
| U05 | reference-from-UGC + seeded pages + `/about` fallback | fresh instance byte-identical, link gated | ✓ (`about` unseeded — product-story fallback kept; `terms`+`help` seeded) |
| U06 | translation lane live (standing + ADR 0027 display) | Translator add + chip-swap green | ✓ (Web 231 + Core 513 green; no drift) |
| U07 | **absorb complete — retire `LocalizedPage`** (last) | zero `LocalizedPage` refs in `src/`, all green | ✓ (Web 229 + Core 510 green; zero `LocalizedPage` in `src/`; `about` unseeded; ML-UI page editor + platform-page image branch retired) |

## Drift-pause policy (the repo's convention)

A unit **pauses** (does not force) on: (a) a standing-matrix cell the
`PageService` cannot express with the *existing* role claims (a new standing
would be a **new ADR**, not a silent addition); (b) a hierarchy guard the
`PageService` cannot validate without a new authorization branch (forbidden —
`PG` adds an *adapter*, not a branch); (c) a translation standing that is not
the ADR 0022/0029 matrix (a **new ADR**); (d) a mount-point slot that requires
a *new UI surface* not already in a layout partial (a **new unit**, not a
silently-omitted slot); (e) the destructive U07 running before U01–U06 are
green (**forbidden** — the sequencing invariant). A pause is recorded in
`pages-handoff-notes.md`, not silently worked around.
