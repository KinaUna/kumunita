---
id: adr-handoffs
title: "ADR handoff notes — detail moved from ADRs during the D11 trim"
tier: reference
kind: design
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/TEMPLATE.md
updated: 2026-11-02
---

> **Abstract:** the destination for the content that moved *out* of ADRs during
> the D11 cap trim (the 34 ADRs that were over 250 lines). Each `## ADR NNNN`
> section holds the options analysis, test lists, implementation notes,
> amendment records, and reference lists that the ADR body now points to.
> **Who:** a developer who needs the *why* or the *exact test names* behind an
> ADR's decision. **When to read:** after reading the ADR's Status / Decision /
> Consequences and finding a pin name (D1, C-3, etc.) that needs the full
> context.

---

## ADR 0057 — User guides (moved detail)

*Source: `docs/adr/0057-user-guides-in-help-subtree-and-consistency-loop.md`*
*Superseded by: ADR 0127, ADR 0160. Supersedes: ADR 0039.*

### The seeded twelve-guide table (D1)

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
| `child-accounts` | the guardian's child accounts: what a guardian may and may not do (suspend, memberships, invitation approval, invite a second guardian who must accept with the same consent or decline, hand-over), and the privacy boundary (no reading the child's content) | ADR 0028 / 0038 §F |
| `being-a-child` | the child's own view of a child account: what stays theirs (posts/replies/profile the guardian can't read), what the guardian(s) handle (memberships, suspend; more than one adult may share the controls), group invitations (decline always open, accept needs guardian approval), and hand-over restoring the child's own controls | ADR 0028 / 0038 §F |
| `admins` | what a global admin does (accounts, communities, the platform pages incl. reset-to-seeded, the sign-up gate, the platform defaults) and what keeps the role in check (audit trail; no reading of residents' content) | ADR 0062 / 0050 / 0019 / 0020 / 0040 / 0058 |
| `moderators` | what a standing moderator may do (see their scoped report queue) and may not (act on a report — assign/unlock/resolve stay with an admin; see outside their part; read anyone's content) | ADR 0003 / 0030 |

`getting-started` is the index of the guides — it is the `help` page's *first*
child, and its body links to the other eleven. A guide that documents a
feature not yet shipped is not seeded: the eleven above are the current
shipped surface (M0–M4 + the `ML`/`LS`/`SP`/`RC`/`RE`/`TG`/`PG`/`GU`/`GA`/`TR`/
`TZ`/`DF` lanes). M5 (Projects), M6 (Portability, iCal, notifications,
search), and the deferred items (SECURITY.md §6 A2, the invitation mechanism,
group logos, in-browser preview) get their guides in their own lanes — the
`UG` lane ships the registry + the loop + the *current* set; a future lane
adds a row to the registry and the test picks it up (D3).

### D2.1 — Warm-boot backfill detail

A deployment whose **first boot predates the UG lane** has the canonical
`help` page (the ADR 0043 D1 four-surface set) but **not** its guide
children — first-boot seeding runs once, on a pristine database, and never
again. Without a warm path, that instance would show `/help` with no
guides until its next fresh database. **This is the gap the backfill
closes**, on the same precedent the platform already set for the identical
drift question:

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

### D3 — The consistency loop (the three layers)

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

### D4 — The drift pin (test detail)

The **guide registry** is a single source in the seeder:
`FirstBootSeeder.GuidePages()` (the `en` bodies, the same shape as
`EnDefaultPages()` — a public static array of `(Slug, Title, Body)`
tuples). The **test** (`UG_U05_GuideRegistryTests`, in
`Kumunita.Core.Tests`) pins:

- **The exact seeded set** — the guides are seeded as direct children of
  the canonical `help` page, **exactly one per slug** in `GuidePages()`
  (no duplicates, no missing), **`Kind = System`**, **`Audience = null`**,
  **`LanguageCode = "en"`**, **`AuthorId` empty**, **not a draft**,
  **not deleted**.
- **The `help` page is the only root under `system`** — the ADR 0043 D1
  exact-set pin (the four-surface set) is **unchanged**: `system`'s
  direct children are still `{terms, help, privacy, conduct}`, and
  `system` is still the **only** root (the guides are *grandchildren* of
  `system`, not new roots — the ADR 0039 §3.3 forest shape, unchanged).
- **The `de`/`fr`/`da` baselines are attached, one per guide per
  language** (**amended 2026-09-21**, following D2) — the guides ship a
  `de`/`fr`/`da` `PageTranslation` row each (12 guides × 3 languages =
  36 rows), on the guide's *own* `Id` (the ADR 0042 D6 parentage
  distinction, unchanged), authored by the platform (`AuthorId` empty),
  and the *first-boot* state has exactly that set. The test that
  previously pinned "no `PageTranslation` row is attached to a guide"
  (`UG_Guides_NoPageTranslationRows_AtFirstBoot`) is now flipped to
  `UG_Guides_DaFrDaBaselines_AtFirstBoot` (asserts 12 × 3 rows, one per
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
  registry: the taxonomy (the twelve slugs + the ADR each one documents),
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

### Amended (2026-09-23) — two admin-facing guides

- The registry grows from ten to **twelve** guides: **`admins`** (what a
  global admin does — accounts, communities, the platform pages incl.
  reset-to-seeded, the sign-up gate, the platform defaults — and what
  keeps the role in check: the audit trail and the no-reading-of-residents'
  content boundary) and **`moderators`** (what a standing moderator may
  do — see their scoped report queue — and may not: act on a report, see
  outside their part, or read anyone's content). Both document
  already-shipped surfaces (`UG`, ADR 0057 D1/D4, the ADR 0040 §3.7
  matrix; ADR 0062 / 0050 / 0019 / 0020 / 0058 / 0003 / 0030) —
  not new lanes, so the "no roadmap letter moves" discipline is untouched.
  The `en` floor, the four `de`/`fr`/`da` baseline rows, the `help` page's
  **Guides** discovery block, and the `getting-started` index all gain the
  two rows in this commit; the warm-boot backfill (ADR 0060) creates them
  for an existing instance at its next upgrade without clobbering a
  community edit.

### Follow-on (deferred to their own lanes)

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

### References

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


---

## ADR 0108 — M11 Portability (moved detail)

*Source: `docs/adr/0108-portability-import-export.md`. Supersedes: ADR 0004.*

### Amends (the frozen-surface ledger)

**0004** (§B.1 — M11 is a **reader/writer over the frozen documents**: it adds
**no** document, **no** index, **no** migration; the `Portability` POCOs are
not Marten documents and create no `mt` tables), **0006** (the frozen
`IAuthorizationService` / `IAuditableResource` surface — **no new adapter**, no
new `AccessAction`, no new `AccessVia`, no `Decide()` branch, no `Audience` —
portability is an **operator action over the instance** decided by the
GlobalAdmin role gate, the ADR 0105 operator-plane shape applied to an operator
lane), **0011** (the content-addressed media — the archive reuses the
`{root}/{Id[0..2]}/{Id}` layout verbatim, so import is a byte-copy and the
dedup-by-content-hash is preserved; the `MediaObject` catalog doc + its bytes
travel as a pair), **0005** (the `LocaleSettings` + `LanguageCatalog` state —
the `config.json` snapshot source; the `LanguageCode` on every `*Translation`
row resolves against the `config.json` catalog), **0028** (the GU "no standing
to carry a secret" precedent — the C-M11·2 no-secret boundary applied to the
operator's own archive: the archive travels the identity **graph** never the
credential material, and on import the principals are re-created with their
secrets **reset**), **0034** (the `Content-Disposition: attachment` streaming
shape the `export` action reuses), **0015** (the closed `KnownTranslationKeys`
registry — the `portability.*` keys join it, all four languages, the
`KnownTranslationKeys_ParityTests` invariant holds), **0101 / 0105** (the
GlobalAdmin admin-surface house pattern — `[Authorize(Roles = GlobalAdmin)]` +
one `AccessAudit` row `Via = Admin`, the `AdminMessagingController` shape to
mirror; the `messaging.toggle` one-audit-row shape the `export` / `import` rows
reuse).

### D7 — The closed inventory (pinned in the design doc from source)

U00 reads **every** `*DocTypes` surface (`CoreDocTypes` / `PostDocTypes` /
`EventDocTypes` / `ProjectDocTypes` / `NotificationDocTypes` /
`MessagingDocTypes` / `MediaDocTypes` / `TagDocTypes` / `PageDocTypes`) and
locks the **exact** closed inventory — each entry: the document `Type`, its
archive file (`docs/{Type}.json`), its **import order** (parents before
children), and its **reference fields** (which of its fields point at another
entry's id) — that U01's registry implements as **data**. The inventory is data
the U02/U05 loops iterate generically, **not** per-type code — that is what
keeps the export/import units atomic despite ~40 document types. **Refined in
favor of the source** (the design doc drift-guard entries 1–4): the closed
inventory is the **content + identity-graph + localization** set — **44 content
docs** — and it **excludes** the five credential/operational documents
(`IdentityToken` — a high-entropy secret; `OutboxEmail` / `EmailDeadLetter` /
`AccessAudit` / `AuditPurgeSummary` — operational state) by name, and carries
the `LocaleSettings` + `LanguageCatalog` + `CommunityOptions` via
`config.json` (D2/D7 resolved) rather than as `docs/` rows.

### D9 — The test pins

The **three acceptance tests** (the closed-loop / handoff / part-vs-whole
shape, per the design-doc template): (a) **round-trip** — plant a
representative instance (a group, a post + reply + a translation, an event +
RSVP, a board + lane + todo, a conversation + message, a page + a translation,
a media object + its bytes, a delegation grant, a guardian link, a couple of
principals with roles) → **export** → import into a **fresh** instance → the
content graph, the media, and the role assignments are preserved and
referentially intact (the "handoff" — the data moves between two instances);
(b) **no-secret** — the exported archive contains **no** password hash /
security stamp / Identity token (the principals POCO has no such field + a
byte-scan witness over the archive) (the "closed-loop" on the trust boundary);
(c) **fail-closed** — a wrong-`format` manifest / a truncated media file / a
dangling reference is rejected **before any write** — the fresh instance has
**zero** rows after the failed import (the "part-vs-whole" — the whole import
is all-or-nothing, not the parts). Plus Web-surface pins (the GlobalAdmin
gate, the one-audit-row per action, the kw-l keys resolve).

### D10 — kw-l keys

The operator-facing strings (`portability.index.title` /
`portability.export` / `portability.import` / `portability.confirm.import` /
`portability.status.*`) × 4 languages (the closed-key registry +
`KnownTranslationKeys_ParityTests` enforces).

### The deferred lanes (each a named follow-on, own ADR)

- **Per-slice / cross-instance portability** — one group's posts out, or
  neighborhood A → B (ARCHITECTURE.md line 784 "cross-neighborhood migration:
  versioned JSON export/import service (not built yet)" + the README §Deferred
  "Cross-neighborhood federation — a standalone OpenIddict IdP; global
  identity, local authorization"). M11 owns the *whole* instance; the
  fragments are the follow-on.
- **Import-merge** — applying an archive on top of a *diverged* existing
  instance (the reconciliation problem: duplicate posts, conflicting roles,
  diverged translations). M11's semantic is **restore** (a fresh instance);
  the merge is the harder lane, own ADR.
- **The backup-automation / cron surface** — the operator-run script / the
  scheduled export (OPS.md). M11 keeps the "operator in the app" house shape;
  the automation is the OPS.md lane.
- **The other world-seam exports** — iCal (M12 stays iCal), the other calendar
  / contacts exports. M11 is the *data* portability; the *format* world-seams
  are their own milestones.

### Supersedes

the ARCHITECTURE.md value-chain row ("outcome + world seams — the loop closes
into the residents' lives") + line 784 deferral ("Cross-neighborhood migration:
versioned JSON export/import service (not built yet)") + the README §Deferred
"Cross-neighborhood federation" deferral — these are the *source* of M11's
scope (whole-instance now, per-slice / cross-instance deferred). ADR 0108
**supersedes** the deferral by owning the whole instance and re-naming the
follow-on lanes (per-slice / cross-instance, import-merge, backup-automation)
in this ADR's Consequences (the kickoff handoff note's "Not touched" record —
those three are *source*, not *target*, of this decision).


---

## ADR 0044 — Tags (moved detail)

*Source: `docs/adr/0044-tags.md`.*

### D4 — Standing (the author's stated reasoning)

The author's stated reasoning — *the post author is the only one in a
position to judge what the tag means, because admins/translators may not have
access to the content* — is realized as **the creator** (the author who
attached it first and read the content) owning the translations, not as "any
attacher may retranslate." A GlobalAdmin may still reword any tag (the
break-glass standing), but no *arbitrary* attacher may. This is the same
"the name is the creator's artifact" rule as ADR 0009 (a group's name is the
owner's, not a member's) and ADR 0026 (a group/community name is the owner's /
GlobalAdmin's, not a member's) — carried to tags. Standing is resolved in
Core (a `TagService` write seam), never from a UI affordance. Two public
display probes mirror `PostService.CanAddTranslation`:
`TagService.CanAttachToPost(post, actorId, actorRoles)` and
`TagService.CanTranslateTag(tag, actorId, actorRoles)`.

### D5 — The read seam (implementation detail)

There is **one load-bearing read contract**, and the three surfaces (tag
list, "posts/pages tagged X", autocomplete) are all callers of it:

> **the base query:** the set of `Tag` rows used on **at least one** `Post` /
> `PageKind.User` `Page` that the **viewer may already read**.

- **Tag list** ("what subjects exist") — `TagService.ListForActorAsync`:
  distinct tags from the base query, with a use-count and the display name
  resolved to the viewer's language (D4 display).
- **"Posts / blog pages tagged `X`"** — `TagService.ListPostsByTagPagedAsync`
  / `ListPagesByTagPagedAsync` (the M7-pair; the non-paged shapes were retired
  after M7 — ADR 0090): the readable posts/pages whose `TagIds` contains that
  tag. A post that carries the tag but is **not** readable by the viewer is
  excluded (the post's own `Read` decision is the gate — the tag grants
  nothing).
- **Autocomplete** — `TagService.SuggestAsync(prefix, actor)`: the base query
  filtered by **`starts_with(displayName, prefix) OR starts_with(slug,
  prefix)`**, capped (≤ 10), where `displayName` is the name resolved **in the
  viewer's language** (a `de`/`fr`-speaking user typing `hy` gets `hygiène`
  even if the `Slug` is `sanitation`).

**Privacy is structural, not a per-surface check.** Because the base query is
already scoped to "content the viewer may read," a tag used only behind
content the viewer cannot read **never appears** in the tag list, in the
by-tag results, or in autocomplete. A private family group's internal tag is
invisible to a non-member. This is what keeps rule 4 ("tags do not touch
access") from leaking a *subject's existence* — the tag grants nothing, and it
also reveals nothing. (A global tag enumeration, by contrast, would leak "the
topic `divorce` exists on this instance" to every visitor — that is excluded
by design.)

**Display resolution** reuses the existing preference order (the
`ITranslationProvider` / ADR 0005 idiom): the viewer's preferred language →
the `TagTranslation` for that language → the tag's base `Name`. Nothing new is
built; a tag's name resolves exactly the way a `PostTranslation` name does.

---

## ADR 0144 — Guardian event-attendance gate (moved detail)

*Source: `docs/adr/0144-guardian-event-rsvp-gate.md`.*

### A. The new document: `GuardianEventRequest`

```csharp
public sealed class GuardianEventRequest
{
    public string Id { get; set; }                       // surrogate (Marten default)
    public string EventId { get; set; }                  // part of the unique (EventId, ChildId) index
    public string ChildId { get; set; }                  // part of the unique (EventId, ChildId) index
    public RsvpStatus DesiredStatus { get; set; }        // the child's desired status at request time
    public GuardianEventRequestStatus Status { get; set; } // Pending / Approved / Denied
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }              // the guardian who resolved it
}

public enum GuardianEventRequestStatus { Pending, Approved, Denied }
```

The **unique `(EventId, ChildId)` index** (the `EventDocTypes.Configure`
surface) enforces exactly **one** pending request per child per event at the
DB layer — the `EventRsvp` `(EventId, UserId)` / `GroupInvitation`
`(GroupId, UserId)` business-key precedent. A resolved (Approved / Denied)
request is **superseded** by a fresh `Pending` row on the child's next
self-lane act (the `GuardianLink` "a re-assignment writes a fresh row, not a
revival" precedent — the child's re-act is a deliberate new act, not an undo;
the guardian's resolution is not a revocable decision).

### B. The posture field: `Profile.EventRsvpMode`

```csharp
public enum EventRsvpMode
{
    GuardianApproves = 0,   // default — the consent text's literal promise
    GuardianNotifies,       // auto-approve + the veto window
    ChildDecides            // no gate, no notification, no veto
}

// Profile.cs
public EventRsvpMode EventRsvpMode { get; set; } = EventRsvpMode.GuardianApproves;
```

The **default is `GuardianApproves`** — a newly-formed GU link inherits the
most protective posture, and a guardian who wants a looser posture must
**deliberately** pick it (the "fail-protective" rule the
`MessagingRestricted` flag's default already embodies). A supervised child
with a missing profile **still requires** guardian approval (the
`EventService.RsvpAsync` gate resolves the posture as
`childProfile?.EventRsvpMode ?? (supervised ? GuardianApproves : ChildDecides)`
— the missing-profile case falls to the strictest supervised posture, not the
child's freedom).

### C. The gate: `EventService.RsvpAsync`

| Posture              | Self-lane outcome |
|----------------------|------------------------------------------------------------------|
| `GuardianApproves`   | **Refused** (`InvalidOperationException`): a `GuardianEventRequest` `Pending` row is stored (or refreshed to `Pending` with the child's current desired status), the active guardian(s) are notified (`guardian.event_request`, one row per active `GuardianLink`, `LinkPath = /me/children/{childId}`), and the write throws. **No** `EventRsvp` row is written. |
| `GuardianNotifies`   | **Allowed**: the last-write-wins upsert proceeds unchanged; the active guardian(s) are notified (`guardian.event_rsvp`, `LinkPath = /events/{eventId}` — the veto window's entry point). |
| `ChildDecides`       | **Allowed outright**: the last-write-wins upsert proceeds, no guardian notification. |
| **Unsupervised** (no active link) | **Never gated** — the ADR 0054 self-service shape, regardless of the stored mode (a mode field with no active link is a dormant field). |

The `InvalidOperationException` is the **same** exception type the GU
group-invitation lane uses for its self-lane refusal — the Web layer catches
it once (the existing `EventController.Rsvp` and `GroupsController` group-event
RSVP surfaces) and surfaces it as `TempData["error"]` + a redirect back to the
event page.

### D. The guardian resolve lanes

```csharp
// The GuardianApproves posture's resolve pair (the GU approve/reject shape,
// re-expressed over events). The approve lane writes the child's EventRsvp
// with the request's DesiredStatus (the "a guardian's approval IS the child's
// RSVP" shape — the child's own self-lane would have written the same row).
Task<GuardianEventRequest> ApproveEventRsvpAsync(string eventId, string childId, string guardianId, CancellationToken ct = default);
Task<GuardianEventRequest> DenyEventRsvpAsync(string eventId, string childId, string guardianId, CancellationToken ct = default);

// The GuardianNotifies posture's veto (the "undo an auto-approved attendance"
// window) — deletes the child's existing EventRsvp.
Task VetoEventRsvpAsync(string eventId, string childId, string guardianId, CancellationToken ct = default);

// Two guardian reads (plain queries, no audit row).
Task<IReadOnlyList<GuardianEventRequest>> GetPendingEventRsvpRequestsAsync(string childId, CancellationToken ct = default);
Task<IReadOnlyList<EventRsvp>> GetChildRsvpsAsync(string childId, CancellationToken ct = default);
```

| Lane            | Refuse | State-machine pin (a resolved request) |
|-----------------|--------|------------------------------------------|
| `ApproveEventRsvpAsync` | `KeyNotFoundException` (no request) → the Web's 404 | `InvalidOperationException` (already `Approved` / `Denied`) — a fresh act is the only path |
| `DenyEventRsvpAsync`   | `KeyNotFoundException` (no request) → the Web's 404 | `InvalidOperationException` (already `Approved` / `Denied`) |
| `VetoEventRsvpAsync`   | `KeyNotFoundException` (no RSVP row to remove) → the Web's 404 | n/a (a veto is always a fresh act over a current row) |

Each resolve lane writes **one** `AccessAudit` row:

| Lane | `Action` verb | `TargetKind` | `Via` |
|------|---------------|--------------|-------|
| `ApproveEventRsvpAsync` | `guardian.event_rsvp_approve` | `event` | `Guardian` |
| `DenyEventRsvpAsync` | `guardian.event_rsvp_deny` | `event` | `Guardian` |
| `VetoEventRsvpAsync` | `guardian.event_rsvp_veto` | `event` | `Guardian` |

The posture write lane writes its own: `IUserInfoService.SetChildEventRsvpModeAsync` → `guardian.event_rsvp_mode` / `profile` / `Guardian`.

### E. The posture write lane: `IUserInfoService.SetChildEventRsvpModeAsync`

```csharp
Task SetChildEventRsvpModeAsync(string childId, EventRsvpMode mode, string guardianId);
```

Standing gate first (the `GuardActiveLinkAsync` shape), then the
`Profile.EventRsvpMode` field write, then the one audit row — one session,
one `SaveChangesAsync` (the `SetChildMessagingRestrictionAsync` shape
**verbatim** — the guardian write-lane precedent, over a mode instead of a
bool).

---

## ADR 0085 — Notification item link + reply highlight (moved detail)

*Source: `docs/adr/0085-notification-item-link-and-reply-highlight.md`.
Supersedes: ADR 0076.*

### Amends (the frozen-surface ledger)

**0076** (the M6 notifications context — this ADR **adds one optional field**
to the `Notification` doc (`LinkPath`, additive on the `NotificationDocTypes`
surface, the ADR 0084 `NotificationSubscription`-doc precedent) and **adds one
optional parameter** to the frozen `EmitAsync` writer (`linkPath`, default
`null`) — the same frozen-surface-amendment precedent ADR 0084 set for its
7-arg overload, and the ADR 0078 additive-gate precedent: the existing
signature's *required* params and return shape are unchanged, the new param
is additive and null-defaulted so every existing emitter and the existing
test call sites keep compiling and behaving), and **0084** (the per-target
subscriptions lane — the three `announcement` / `community.post` /
`page.child` emitters it wired inherit `LinkPath` for free in this ADR, as do
the three kinds ADR 0076 already wired (`post.reply` / `group.post` /
`event.rsvp`)); **0015** (the closed `KnownTranslationKeys` registry — the one
new UI key `notifications.view` is added in all four languages, en floor "View"
/ de "Ansehen" / fr "Voir" / da "Se"; the registry parity test enforces the
delta), and **0061** (the email-and-notification-language / outbound-channel
ADR — the absolute-link precedent this ADR reuses: the verification email's
one-time link is `BaseUrl + relative path` exactly as the notification email's
view link now is). Additive on **0005** (the per-recipient language
resolution — the new `notifications.view` label resolves in the recipient's
`Profile.EmailLanguage` exactly as the other notification copy does) and
**0039** (the page lane — the `page.child` emitter's link is the page's
`/pages/{a/b/c}` ancestor-slug chain, the `GET /pages/{**path}` show route's
address, derived by a new `PageService.GetPathAsync` read seam that walks the
`ParentId` chain). `Milestones.cs` / the README Roadmap /
`MilestonesTests.cs` **untouched** (a named lane within the M6 notifications
surface, not a milestone — the ADR 0083 / 0084 named-lane precedent).

### D4 — The six content/reply emitters

| kind | emitter | `linkPath` |
|------|---------|-----------|
| `post.reply` | `PostService.CreateReplyAsync` | `/posts/{postId}#reply-{replyId}` |
| `community.post` | `PostService.CreatePostAsync` | `/posts/{postId}` |
| `group.post` | `PostService` (the group-post lane) | `/groups/{groupId}/posts/{postId}` |
| `announcement` | `AnnouncementService` (community + flat, create + edit) | `/announcements/{id}` |
| `event.rsvp` | `EventService.RsvpAsync` | `/events/{eventId}` |
| `page.child` | `PageService.CreateAsync` | `/pages/{a/b/c}` (D5) |

Each uses the id already in scope at the call site (`postId` / `reply.Id` /
`draft.GroupId` / `post.Id` / `announcement.Id` / `@event.Id` / `page.Id`).
The `post.reply` link carries the `#reply-{id}` fragment (D5); the others link
to the item itself. *Forbids:* an emitter passing an absolute URL (D2), a
`linkPath` for a kind the resident cannot read (the audience gate still
governs the click-through, D1), and an emitter that omits `linkPath` on a
content/reply kind it otherwise has the id for (the gap this ADR closes is
exactly "the id is in scope but the resident is not handed the address").

### D5 — The post-detail reply anchor + scroll-and-highlight (implementation)

`Views/Posts/Detail.cshtml` sets `id="reply-@r.Id"` on each reply `<li>` (the
id that the `post.reply` link's `#reply-{replyId}` fragment points at). A new
`@section Scripts` module (`client/lib/reply-highlight.ts`, tsc-built — the
app's CSP is `script-src 'self'` with no 'unsafe-inline' (OPS §10), so this
is a self-hosted module, not an inline `<script>`; the same `client/*.ts`
pattern as `translation-swap.ts`) reads `location.hash`; when it is a
`#reply-{id}` anchor, it finds the matching element,
`scrollIntoView({ behavior: "smooth", block: "center" })` it, and adds a
`.reply-highlight` class (removed after 4 s so a re-click can re-trigger). The
`.reply-highlight` CSS (in `wwwroot/css/site.css`) is a tinted background + a
soft primary outline that fades over ~1.5 s via a `@keyframes`, with a
`prefers-reduced-motion: reduce` fallback to a static tint (the fade is gated;
the color still lands). *Forbids:* a server-rendered highlight that hardcodes a
specific reply, a highlight that persists (the 4 s removal + the fade keep it a
*flash*, not a sticky state), and a highlight for a reply that does not exist
(the script no-ops when `getElementById` returns null — the inbox never 404s
on an old notification, and the detail page never throws on a stale hash).
