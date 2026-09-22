# ADR 0057 — User guides: resident-facing guides live in the `help/` subtree of the existing page tree; `en` floor + curated `de`/`fr`/`da` baselines (community-owned); a three-layer consistency loop keeps them honest as code changes (the `UG` named lane)

Status: Accepted
Date: 2026-09-21
Amends: **0039** (the page tree — the guides are `Page` docs nested under the
canonical `help` page; the `help` page is promoted from a leaf to a
*folder-with-index*, a shape ADR 0039 §3.3 already names as valid — "a page is
either a *folder* … or a *leaf*, or both ('folder-with-index')"), **0043** (the
`help` page it hangs from — this lane adds children to it, does not touch the
five-surface set or the `about` product-story pin), **0047 / 0049 / 0051**
(the page-localization + variant-display lanes — a guide renders in the viewer's
effective language, so a seeded `de`/`fr`/`da` translation of a guide shows
first for a German / French / Danish reader; this ADR ships the `en` floor and
delegates the translations to those lanes). **Additive** on **0039** (the
`Page` doc + the frozen `IAuthorizationService` + the ADR 0027 chip-swap, all
*reused*), **0040** (`PageKind.System` + the `system/` namespace root — a guide
inherits the system-page standing matrix verbatim), **0042 D1** (the `en`
code-owned / non-`en` community-owned ownership split — a guide's `en` body is
code-owned; a non-`en` guide body, once added, is community-owned), and **0005 C**
(the "never machine-translated" clause — a guide, like UGC, is rendered as
authored, and a human Translator is the only writer of a non-`en` body). No
amendment to any of those — this ADR is additive on their decisions exactly as
written.

## Context

A fresh instance today serves **one** resident-facing help surface: the
`/help` page (ADR 0043 D1, the five-surface set — `system/help` `Page` doc,
`Kind = System`, public, `en` body + `de`/`fr`/`da` `PageTranslation` rows).
That page is a **four-bullet summary** — "Post to a community feed …", "Groups
let you organize …", "Directory shows the residents …", "Moderation lets an
admin …". It is *not* a guide: it does not explain **how to do a thing** (how
do I post to only my group? how do I save a draft and publish it later? how do
I pick who can see my post? how do I change my language? how do I RSVP to an
event?). A resident who wants to *act* has to guess, or ask an admin in a
side-channel — an un-integrated seam in the platform's own value chain
(`the-platform-as-integrator.md` — "Diagnose confusion as missing
integration, not missing features").

The platform has now shipped a full resident-facing surface — posts + replies +
the audience picker, public/private groups, drafts, the multilingual picker,
timezone + date-format settings, events + RSVP + reminders, the WYSIWYG editor,
file attachments, tags, and the Translator role. **None of it has a resident
guide.** The operator's docs (SECURITY.md, ARCHITECTURE.md, OPS.md, the ADRs)
are for the *team*, not the *resident*; a resident who reads "audience =
deny-by-default" does not learn "how do I post to only my group?". The
resident-facing guidance lives only in that one `help` summary page — and it
is a summary, not a guide.

**The fork that determined this ADR:** build a *new* resident-guide surface
(a separate "guides" module, its own routes, its own editor, its own
translations, its own standing) **or** hang the guides on the existing page
tree under the `help` page. **The existing tree was chosen** (user sign-off
2026-09-21): a new surface would duplicate the page engine, the WYSIWYG editor,
the translator lane, the authorization adapter, the draft/soft-delete
idiom — a bag of parallel lanes that ADR 0039's *absorb* decision explicitly
rejected for the static-page lane (the same "two editors, two renderers, two
translation lanes" smell). Hanging the guides on the tree is **free**: the
guides are `Page` docs (the existing doc), edited by the **one**
`bindRichEditor` (ADR 0031/0033), translated by the **one**
`PageTranslation` lane (ADR 0022/0047/0049/0051), read by the **one**
`IAuthorizationService` (ADR 0006, via the existing
`PageToAuditableResource` adapter), and discovered by the **one** `/pages`
tree browse (ADR 0039 §3.8). This is the platform's own spine applied to
documentation — the parts (the guides) are cheap; the integration (one engine,
one editor, one translator, one reader, one tree) is the value.

The second half of the lane is the **consistency** question the user asked:
"keep user guides consistent in the future, as more features are added and
things change … a periodic or event based check for user documentation."
This is an *integration* question, not a content one: a guide that describes
a feature the code no longer has (or that describes a feature the code has
gained without the guide being updated) is a **broken seam** in the
platform's value chain — the resident follows the guide, hits a wall, and
loses trust. ADR 0047/0049/0051 show the platform's existing answer to a
similar question (UI-strings drift) — a *closed registry* + a *warm-boot
backfill* + a *pinned parity test*. This ADR applies the same shape to the
guides: a **closed registry** (the seeder's `GuidePages()` array), a
**code-owned `en` floor** (the seeder writes it), a **pinned parity test**
(the guide registry is the single source the test reads), and a **periodic
review procedure** (the OPS.md §13 lane — the *event-based* check is the
lane's own Definition of Done; the *periodic* check is the OPS procedure).

## Decision

### D1 — The guides are `Page` docs nested under the canonical `help` page

- The seeded guide set is **exactly ten** `Page` docs, each a **direct
  child of the canonical `help` page** (ADR 0043 D1, `system/help`), each
  `Kind = System` (ADR 0040), each `Audience = null` (public — the ADR 0039
  §3.4 shape, world-readable; a resident's *first* question is "how do I …"
  and that answer is not audience-gated), each `LanguageCode = "en"`
  (authored-in `en`), each `AuthorId = string.Empty` (platform content, no
  resident author — the ADR 0043 D1 shape), each `IsDraft = false` and
  `IsDeleted = false`.
- The **ten** seeded guides (closed set, `en` only; a new guide is a new
  lane, not a silent addition — the ADR 0040 `PageKind` closed-set rule
  applied to the guide set):

  | Slug (under `help/`) | Covers | The ADR it documents |
  |---|---|---|
  | `getting-started` | the first ten minutes: sign in, the feed, the directory, the `/pages` tree | ADR 0039 / 0043 / 0001 |
  | `posts` | how a post works: the audience picker, replies, edit + soft-delete, drafts, file attachments | ADR 0014 / 0024 / 0037 / 0034 |
  | `groups` | public groups (the audience reuse unit), private groups (the membership unit), group posts | ADR 0010 / 0013 / 0012 |
  | `drafts` | save-as-draft, the author-only visibility, publishing, `/my/drafts` | ADR 0037 |
  | `audience` | choosing who sees a post (the audience picker, in plain language; what "community-visible" / "individual" / "group" each mean) | ADR 0001-B / 0036 / 0041 |
  | `language` | the language picker, the `Accept-Language` fallback, the per-resident override | ADR 0005 / 0015 / 0046 / 0019 / 0020 |
  | `events` | how an event works: the feed, the detail view, RSVP, the day-before reminder | ADR 0054 |
  | `translator` | what the `Translator` role may and may not do, in plain language | ADR 0021 |
  | `child-accounts` | the guardian's child accounts: what a guardian may and may not do (suspend, memberships, invitation approval, assign a second guardian, hand-over), and the privacy boundary (no reading the child's content) | ADR 0028 / 0038 |
  | `being-a-child` | the child's own view of a child account: what stays theirs (posts/replies/profile the guardian can't read), what the guardian handles (memberships, suspend), group invitations (decline always open, accept needs guardian approval), and hand-over restoring the child's own controls | ADR 0028 / 0038 |

- **`getting-started` is the index of the guides** — it is the `help` page's
  *first* child, and its body links to the other nine. A resident who
  arrives at `/pages/help` (the tree browse) sees `help` →
  `getting-started` → the rest; a resident who arrives at `/help` (the
  hard-coded route, ADR 0043 D2) reads the `help` page's *own* body (the
  ADR 0043 five-surface summary) and, if they want more, follow the link to
  `/pages/help/getting-started`. The `help` page's body gains a **single
  new bullet** (a pointer to the guides) in the same commit — the four
  existing bullets are unchanged (the `en` body parity test reads the
  baseline array, so the *new* bullet is additive on the code-owned `en`
  floor, and the `de`/`fr`/`da` baseline arrays gain the matching new bullet
  to keep the "structure preserved" parity — the ADR 0042 D2 bar).
- **A guide that documents a feature not yet shipped is not seeded.**
  The nine above are the current shipped surface (M0–M4 + the `ML`/`LS`/`SP`/
  `RC`/`RE`/`TG`/`PG`/`GU`/`GA`/`TR`/`TZ`/`DF` lanes). **M5 (Projects), M6
  (Portability, iCal, notifications, search), and the deferred items
  (SECURITY.md §6 A2, the invitation mechanism, group logos, in-browser
  preview) get their guides in their own lanes** — the `UG` lane ships the
  registry + the loop + the *current* set; a future lane adds a row to the
  registry and the test picks it up (D3). This is the ADR 0039 "no roadmap
  letter moves" discipline applied to the guide set.

### D2 — The `en` floor is code-owned; the non-`en` bodies are community-owned

- The **`en` body** of a guide is **code-owned** — the seeder's
  `GuidePages()` array is the single source. On the **first-boot** path the
  seeder upserts (code-wins, the ADR 0042 D1 shape) in place. On the
  **warm-boot (upgrade)** path the guide is written **create-if-missing
  only** — a guide that already exists there (a community edit, or a
  resident page that uses the slug) is **never refreshed**, so an admin's
  or a community's in-app edit is never clobbered by a later deploy (the
  ADR 0042 D1 invariant, the ADR 0047 D2 / ADR 0052 backfill shape —
  see **D2.1**). This is the **floor** of the guide's `en` text: on a
  fresh instance a resident reading a guide in `en` reads the code's
  current view of the feature.
- **A non-`en` guide body ships as a curated `de`/`fr`/`da` baseline**
  (**amended 2026-09-21** — superseding the original "ship `en`-only"
  decision). On a pristine DB the guides now carry a `de`/`fr`/`da`
  `PageTranslation` row each (10 guides × 3 languages = 30 rows), seeded
  by `FirstBootSeeder.SeedGuideTranslationsAsync` from the hand-curated
  registries `DeGuidePages()` / `FrGuidePages()` / `DaGuidePages()` — the
  **same shape** as the four-surface set's baselines
  (`DeDefaultPages()` / `FrDefaultPages()` / `DaDefaultPages()`), so the
  "deliberate asymmetry" the original text described is now gone: guides
  and the four-surface set both ship a `de`/`fr`/`da` floor.
  The ownership split is **unchanged** and is the load-bearing part:
  these baselines are seeded **create-if-missing only** (the ADR 0042 D1
  invariant), so once a **human Translator** (the ADR 0021 lane) or a
  GlobalAdmin edits a guide body in the in-app editor, no later deploy
  clobbers that edit. A **machine translation is still never** the writer
  of a non-`en` guide body (ADR 0005 C — the "never
  machine-translated" clause, applied to guides as to UGC); the baselines
  are hand-curated, not machine output. The `en` floor is always present
  (the `Page` doc body); the non-`en` body, whether the seeded baseline or
  a human's edit, is community-owned and never clobbered by a later
  deploy (the ADR 0042 D1 invariant, unchanged).
- The **standing** on a guide is the ADR 0040 system-page matrix: **edit /
  move / delete = GlobalAdmin only**; **add a translation = GlobalAdmin ∪
  Translator** (the ADR 0021 / 0022 / 0047 / 0048 lanes, unchanged). A
  community Moderator has **no** standing on a guide (the ADR 0040
  amendment, unchanged). A guide is *not* a `PageKind.User` page (the
  resident's blog lane) — it is a system page, and the ADR 0040 namespace
  guard (a `User` page is never nested under a `System` page, and vice
  versa) applies: a guide is a `System` page under the `System` `help`
  page, and a resident's blog page is never nested under `help`.

### D2.1 — Warm-boot backfill: guides appear on existing instances at upgrade

A deployment whose **first boot predates the UG lane** has the canonical
`help` page (the ADR 0043 D1 four-surface set) but **not** its guide
children — first-boot seeding runs once, on a pristine database, and never
again. Without a warm path, that instance would show `/help` with no
guides until its next fresh database. **This is the gap the backfill
closes**, on the same precedent the platform already set for the
identical drift question:

- **`FirstBootSeeder.BackfillUserGuidesAsync`** runs in the **warm-boot**
  branch of `SchemaBootstrap` (the same `else` that runs
  `BackfillPageTranslationsAsync` and `BackfillUiStringBaselinesAsync`),
  on **every upgrade boot** — not gated on first boot.
- **Create-if-missing only** (the ADR 0042 D1 invariant, the ADR 0047 D2 /
  ADR 0052 shape): a guide that already exists under `help` — whether a
  community-edited guide or a resident-created page that happens to use the
  same slug — is **skipped, never refreshed** (no code-wins clobber). Only
  the **absent** guides are created, from the same `GuidePages()` registry
  the first-boot seeder writes, so a fresh and a backfilled instance carry
  the same `en` floor.
- **Idempotent**: a second warm boot (a container restart, a repeated
  deploy) finds every guide it created and skips. No tombstones, no
  deletes — create-if-missing is what makes the re-run a no-op.
- **Scope limit (the deliberate non-decision)**: the backfill adds the
  *absent* guides. It does **not** resurrect a guide a community has
  *retired* (soft-deleted, the ADR 0024 shape) — the OPS.md §13 review
  owns that decision, and a retired guide stays retired across upgrades.
  The "retire" response in D3.3 is a **registry removal** (the code no
  longer claims to own that guide), which is what stops the backfill from
  re-adding it.

- **The `de`/`fr`/`da` baselines have their own backfill** (**amended
  2026-09-21**, following D2): **`FirstBootSeeder.BackfillGuideTranslationsAsync`**
  runs in the same warm-boot `else`, immediately after
  `BackfillUserGuidesAsync`, and creates the absent
  `de`/`fr`/`da` `PageTranslation` rows for the guides, create-if-missing.
  A deployment whose first boot predates the guide-translation baseline
  has the guide `Page` docs but no `de`/`fr`/`da` rows; this closes that
  gap. The same two invariants hold: **create-if-missing** (a row a
  community Translator or GlobalAdmin has already edited is skipped, never
  refreshed — the ADR 0042 D1 invariant) and **idempotent** (a second warm
  boot finds every baseline it created and skips).

### D3 — The consistency loop (the "keep it honest" lane)

The consistency loop is **three layers**, each answering a *different*
question (the `in-code.md` "each layer must answer a different question, or
it's ceremony" rule):

1. **The layer of *where* — the guides are in the tree.** The single source
   of truth for "what the platform does, in the resident's language" is the
   `Page` tree, not a second doc set. A guide that lives *outside* the tree
   (a separate module, a separate route, a separate editor) is a bag of
   parallel lanes — the ADR 0039 absorb-decision smell, applied to
   documentation. The tree is the *closed loop*: the resident reads the
   guide, follows the link, does the thing, and the *same* engine that
   rendered the guide rendered the thing they did. The *where* layer is
   what makes the guide *integrative* rather than *parallel*.

2. **The layer of *when* — the event-based check (the lane's Definition of
   Done).** A resident-facing change ships **with** its guide update, in
   the **same lane**: a lane that ships a feature that changes how a resident
   does a thing (a new audience option, a new RSVP flow, a new attachment
   type) **must** update the affected guide's `en` body (the code-owned
   floor) in the same commit, and **must** add a row to the guide registry
   (D5) if it is a *new* feature. This is the ADR 0042 D1 "code wins for
   `en`" shape applied to the guide's `en` body: the code is the single
   source, and the guide's `en` body is *part of the code* (the seeder's
   array is in `Kumunita.Core`, the same assembly the feature lives in).
   The *event* is the lane's Definition of Done (the ADR 0039 §3.7 C3
   invariant shape, applied to docs): a lane that ships a feature without
   its guide update is not done. The *response* is the test suite (D5):
   the guide registry is the single source the test reads, and a
   feature-without-guide or a guide-without-feature is a red test.

3. **The layer of *how often* — the periodic check (the OPS.md §13
   procedure).** A **GlobalAdmin** (the owner) runs a **quarterly** (or
   per-release, whichever is sooner) review procedure in OPS.md §13: for
   each row in the guide registry, open the guide's *current* `en` body in
   the in-app editor, follow it *as a resident would* (sign in as a demo
   resident, do the steps), and mark the row **current** / **needs update**
   / **feature retired**. The *response* to a "needs update" is a lane
   (the ADR 0057 §D3.2 shape — a lane ships the guide update); the
   *response* to a "feature retired" is a soft-delete of the guide (the
   ADR 0024 author-lane shape, applied to a guide by a GlobalAdmin) and a
   removal of the row from the registry (the ADR 0044 D7 / 0052 shape —
   the warm-boot backfill's "reset" clause, applied to the guide registry).
   The *cadence* is recorded in OPS.md §13 with a **Last tested: <date>**
   stamp (the OPS.md §12 convention), and the *owner* is the GlobalAdmin
   (the same owner as the other OPS procedures). The periodic check is the
   *safety net* for the event-based check: a lane that *should* have
   updated a guide but didn't (a missed Definition-of-Done) is caught by
   the next quarterly review, not by a resident hitting a wall.

### D4 — The drift pin (the test that keeps the registry honest)

- The **guide registry** is a single source in the seeder:
  `FirstBootSeeder.GuidePages()` (the `en` bodies, the same shape as
  `EnDefaultPages()` — a public static array of
  `(Slug, Title, Body)` tuples). The **test**
  (`UG_U05_GuideRegistryTests`, in `Kumunita.Core.Tests`) pins:
  - **The exact seeded set** — the guides are seeded as direct children of
    the canonical `help` page, **exactly one per slug** in
    `GuidePages()` (no duplicates, no missing), **`Kind = System`**,
    **`Audience = null`**, **`LanguageCode = "en"`**, **`AuthorId`
    empty**, **not a draft**, **not deleted**.
  - **The `help` page is the only root under `system`** — the ADR 0043 D1
    exact-set pin (the four-surface set) is **unchanged**: `system`'s
    direct children are still `{terms, help, privacy, conduct}`, and
    `system` is still the **only** root (the guides are *grandchildren* of
    `system`, not new roots — the ADR 0039 §3.3 forest shape, unchanged).
  - **The `de`/`fr`/`da` baselines are attached, one per guide per
    language** (**amended 2026-09-21**, following D2) — the guides ship a
    `de`/`fr`/`da` `PageTranslation` row each (10 guides × 3 languages =
    30 rows), on the guide's *own* `Id` (the ADR 0042 D6 parentage
    distinction, unchanged), authored by the platform (`AuthorId` empty),
    and the *first-boot* state has exactly that set. The test that
    previously pinned "no `PageTranslation` row is attached to a guide"
    (`UG_Guides_NoPageTranslationRows_AtFirstBoot`) is now flipped to
    `UG_Guides_DaFrDaBaselines_AtFirstBoot` (asserts 10 × 3 rows, one per
    language, platform-authored) plus an idempotency pin
    (`UG_GuideBaselineIdempotency_AcrossTwoBoots`) and a backfill pin
    (`UG_BackfillGuideTranslations_CreatesAbsent_NeverClobbers_HumanEdit`).
  - **The `en` body parity** — the guide's seeded `Page.Body` equals
    `GuidePages()[slug].Body` exactly (the ADR 0042 D1 "code wins for
    `en`" shape, pinned by the test).
  - **The feature↔guide drift pin** — the guide registry's slugs are the
    **single source** the test reads (the ADR 0042 D1 "the registry is the
    single source both the seeder and these tests read, so mirroring is
    exact" shape, applied to the guides). A lane that adds a feature
    without a guide row, or a guide row without a feature, is a red test.

### D5 — The registry (the single source)

- `FirstBootSeeder.GuidePages()` — the `en` guide bodies, a public static
  array of `(Slug, Title, Body)` tuples (the same shape as
  `EnDefaultPages()`, D4). The **conventions doc**
  (`docs/guides/CONVENTIONS.md`) is the **human-facing** view of the
  registry: the taxonomy (the ten slugs + the ADR each one documents),
  the *writing rules* (plain language, the resident's steps, no code
  jargon, the ADR 0042 D2 bar for the `en` floor), the *consistency loop*
  (D3, in the resident's words), and the *review procedure* (the OPS.md
  §13 checklist). The **conventions doc and the seeder's registry are the
  same set** — a new guide row in the seeder is a new row in the
  conventions doc, and vice versa (the ADR 0042 D1 "code wins" shape,
  applied to the doc: the doc is the *view* of the code, not a separate
  source). The **drift pin** (D4) is the test that keeps the two in
  sync: a row in the seeder that is not in the conventions doc, or a row
  in the conventions doc that is not in the seeder, is a red test.

## Consequences

- **One guide engine, one editor, one translator, one reader, one tree.**
  The lane *unifies* instead of *duplicates* — the ADR 0039 absorb-decision
  applied to documentation. The guides are `Page` docs (the existing doc),
  edited by the **one** `bindRichEditor` (ADR 0031/0033), translated by the
  **one** `PageTranslation` lane (ADR 0022/0047/0049/0051), read by the
  **one** `IAuthorizationService` (ADR 0006, via the existing
  `PageToAuditableResource` adapter), and discovered by the **one**
  `/pages` tree browse (ADR 0039 §3.8). A new guide is a new row in the
  registry (D5), not a new module.
- **The `help` page is promoted from a leaf to a folder-with-index.**
  ADR 0039 §3.3 already names this shape as valid ("a page is either a
  *folder* … or a *leaf*, or both ('folder-with-index')"). The `help`
  page's body gains a **`## Guides` section** listing all ten guides as
  absolute links (`/pages/system/help/{slug}`); the four existing bullets
  are unchanged (the ADR 0042 D2 "structure preserved" parity — the
  `de`/`fr`/`da` baseline arrays in `DeDefaultPages()` /
  `FrDefaultPages()` / `DaDefaultPages()` carry the matching translated
  `## Guides` section, so the en/de/fr/da `help` bodies stay in parity).
  The `/help` hard-coded route (ADR 0043 D2) is **unchanged** — it still
  resolves to the `help` page's *own* body; the guides are reachable at
  `/pages/system/help/{slug}` (the ADR 0039 §3.8 tree browse — the
  `system` root is part of the derived path) and cross-linked from the
  `help` index and the `getting-started` guide (D1).
- **The `en` floor is code-owned; the non-`en` bodies are community-owned.**
  The ADR 0042 D1 / 0047 / 0049 / 0051 shape, unchanged: the `en` body is
  the code's view of the feature, the non-`en` body is the community's
  view, and a human Translator is the only writer of a non-`en` body
  (ADR 0005 C — the "never machine-translated" clause, applied to guides).
  This is a *deliberate* asymmetry with the four-surface set (which ships
  `de`/`fr`/`da` baselines): the four-surface set is *policy* (the
  platform's own statement); the guides are *how-to* (the resident's own
  steps). The `en` floor is always present; the non-`en` body, once a
  human adds it, is community-owned and never clobbered by a later deploy.
- **The consistency loop is three layers, each answering a different
  question** (the `in-code.md` rule): *where* (the guides are in the
  tree — the closed loop), *when* (the lane's Definition of Done — a
  resident-facing change ships with its guide update), *how often* (the
  OPS.md §13 procedure — the quarterly review, the safety net). A loop
  without an actuator is decoration (the ADR 0042 D1 / 0047 / 0049 / 0051
  "every loop has an owner, a cadence, and a defined response" shape,
  applied to docs).
- **The drift pin is the test that keeps the registry honest** (D4): the
  guide registry is the single source the test reads (the ADR 0042 D1
  "the registry is the single source both the seeder and these tests read"
  shape, applied to the guides). A feature-without-guide or a
  guide-without-feature is a red test. The *event* is the lane's
  Definition of Done; the *response* is the test suite; the *safety net*
  is the OPS.md §13 procedure.
- **The `UG` lane is a *named lane* (the `ML`/`GP`/`RC`/`RE`/`TG`/`PG`
  convention — a short ID, *not* a renumber).** M5 / M6 stay
  Projects / Portability. No roadmap letter moves. The `UG` entry is added
  to `Milestones.All` **after `PG` and before `M5`** (a `StatusDone`
  entry — the guides + the loop + the registry ship in this lane; the
  "single in-progress milestone" pin (M5) is unchanged), and the README
  Roadmap gains the `UG` row in the same commit (the ADR 0039 §3.9 /
  ADR 0043 D4 "keep `Milestones.cs` and the README Roadmap together"
  discipline, the `MilestonesTests` pin updated in the same commit).
- **No new `AccessAction`, no new `AccessVia`, no new authorization
  branch.** A guide is a `Page` doc (the ADR 0039 §3.4 shape), read by
  the **frozen** `IAuthorizationService` (ADR 0006) via the existing
  `PageToAuditableResource` adapter (ADR 0039 §3.5) — *reused*, not
  extended. The ADR 0040 system-page standing matrix (edit / move /
  delete = GlobalAdmin only; add a translation = GlobalAdmin ∪ Translator)
  applies verbatim. The ADR 0037 draft idiom, the ADR 0024 soft-delete
  idiom, the ADR 0025/0031/0033 WYSIWYG editor, the ADR 0025/0034 media
  idiom, the ADR 0047/0049/0051 page-localization + variant-display lanes
  all apply verbatim. **No schema change, no new doc type, no migration**
  (it reuses `Page` + `PageTranslation` and the existing `PageKind.System`
  standing matrix of ADR 0040).
- **The guides are *not* a second doc set.** The operator's docs
  (SECURITY.md, ARCHITECTURE.md, OPS.md, the ADRs) are for the *team*; the
  guides are for the *resident*. The two are *different surfaces of the
  same integration* — the team's docs describe the *system*; the
  resident's guides describe the *resident's steps*. The ADR 0042 D1 /
  0047 / 0049 / 0051 shape (a closed registry + a code-owned floor + a
  pinned parity test + a periodic review) is the *integration* that keeps
  the two in sync: the team's ADR is the *source* of the guide's `en`
  body (the code-owned floor), and the guide's `en` body is the
  *resident-facing view* of the same integration. A guide that
  contradicts the ADR is a broken seam (the ADR 0039 §3.9 / ADR 0043 D4
  "the two are the same set, not two separate sources" shape, applied to
  the team's docs and the resident's guides).

## Follow-on (deferred to their own lanes)

- **A guide for each feature not yet shipped** (M5 Projects, M6
  Portability / iCal / notifications / search, the deferred items in
  SECURITY.md §6) — the `UG` lane ships the registry + the loop + the
  *current* set; a future lane adds a row to the registry (D5) and the
  test picks it up (D4). This is the ADR 0039 "no roadmap letter moves"
  discipline applied to the guide set.
- **A `de`/`fr`/`da` baseline for the guides** (the ADR 0042 D2 / 0047 D2
  shape, applied to the guides) — a future lane that ships a
  community-owned `de`/`fr`/`da` guide baseline (the ADR 0042 D1 / 0047
  D2 / 0052 shape, a *new* warm-boot backfill lane for the guides'
  translation rows, the ADR 0047 D2 / 0052 "create-if-missing, idempotent,
  never overwriting an admin edit" shape, unchanged). The `en` floor is
  always present (D2); the non-`en` baseline is a follow-on lane.
- **A guide for the invitation mechanism** (SECURITY.md §6 A2, the
  ADR 0050 "what remains to land" item) — the `UG` lane ships the
  registry + the loop + the *current* set; the invitation mechanism is a
  follow-on lane, and its guide is a row in the registry (D5) when it
  ships.

## References

- ADR 0039 — the page tree (the guides are `Page` docs under `help/`).
- ADR 0040 — the `PageKind` model + the `system/` namespace root (a guide
  inherits the system-page standing matrix verbatim).
- ADR 0042 D1 / D2 / D6 — the `en` code-owned / non-`en` community-owned
  ownership split + the "structure preserved" parity + the parentage
  distinction (the ADR 0042 D1 / 0047 / 0049 / 0051 shape, applied to the
  guides).
- ADR 0043 D1 / D2 — the five-surface set + the `help` page it hangs from
  (the ADR 0043 D1 "about stays a view" pin is unchanged; the ADR 0043 D2
  route contract is unchanged).
- ADR 0047 D2 / ADR 0052 — the warm-boot backfill + the "create-if-missing,
  idempotent, never overwriting an admin edit" shape (the ADR 0047 D2 /
  0052 "every loop has an owner, a cadence, and a defined response" shape,
  applied to the guides).
- ADR 0005 C — the "never machine-translated" clause (the ADR 0005 C
  "a human Translator is the only writer of a non-`en` body" shape,
  applied to the guides).
- `in-code.md` — "each layer must answer a different question, or it's
  ceremony" (the ADR 0057 D3 "three layers, each answering a different
  question" shape, applied to the consistency loop).
- `the-platform-as-integrator.md` — "Diagnose confusion as missing
  integration, not missing features" (the ADR 0057 D1 "hang the guides on
  the tree" shape, applied to the resident-facing guidance).
- OPS.md §13 — the periodic review procedure (the ADR 0057 D3.3 "the
  safety net" shape, applied to the guides).
