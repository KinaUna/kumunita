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


---

## ADR 0194 — Sample-data MFA lock (moved detail)

*Source: `docs/adr/0194-sample-data-mfa-lock.md`. Supersedes: ADR 0138.*

### Amends (the frozen-surface ledger)

**0138** (its `IsChangePasswordLockedForAsync` four-conjunct decision, its
`LocaleSettings.SamplePasswordChangeLocked` additive-field-on-the-singleton
shape, and its "surface-replaced-by-notice" posture all become the *template*
this ADR re-applies to the MFA surface — ADR 0138 is the source of the rule;
this ADR does not restate it). Amends: **0056** (the `SampleData__Enabled`
config-flag existence gate — the same "unreachable by construction" posture
the MFA lock is gated on). Amends: the **F3/U4 login leg 1.5b** (the
"GlobalAdmin-with-MFA-off is redirected to /settings/security/mfa to enroll"
enforcement, ADR 0174 / the `AccountController.Login` seam) — the sample
`GlobalAdmin` is **exempt** from that redirect on a `SampleData__Enabled`
instance (the `IsSampleDataAccountAsync` seam, conjuncts 1+3 only, so a
locked instance still lets the sample admin sign in with the shared
password; a real, non-sample `GlobalAdmin` is still forced to enroll).

### Tests

The ADR 0138 test pin (the `AccessAudit` shape + the live-on-next-read
invariant + the four-conjunct decision) is re-applied to the MFA surface in
`tests/Kumunita.Core.Tests/IdentityServiceSampleMfaLockTests.cs`
(15 tests: the `false` floor, the audit-row shape for the MFA lock, the
re-unlock, the two-locks-are-independent, the four-conjunct decision for
the MFA lock, the two-conjunct login-exemption decision, and the
login-exemption-is-independent-of-the-admin-toggle-gate). The ADR 0138 test
pin for the admin surface (the `AdminSampleDataController` thin seam) is
re-applied to the MFA toggle in
`tests/Kumunita.Web.Tests/AdminSampleDataControllerTests.cs` (6 new tests:
the Index `MfaLocked` seed, the Index `MfaLocked` floor, the Save MFA-lock
call, the Save MFA-unlock call, the Save MFA-lock 404 on a real instance,
and the Save pre-0194 single-arg form shape). The ADR 0138 test pin for the
resident surface (the `ChangePasswordLocked` notice shape) is re-applied to
the MFA surface in
`tests/Kumunita.Web.Tests/SecuritySettingsMfaLockTests.cs` (5 tests: the
Mfa GET renders the `MfaLocked` notice when locked, the Mfa GET renders
the enrollment surface when unlocked, the MfaEnable / MfaRotateRecovery /
MfaDisable POSTs are refused before any MFA write). The ADR 0138 test pin
for the login leg (the `F3U5_MfaLoginTests` harness) is extended with the
`IsSampleDataAccountAsync` exemption (2 new tests: the
`SampleAdminLogin_SkipsForcedMfaEnrollment` exemption fires for a sample
account, the `NonSampleGlobalAdminLogin_StillForcedToMfaEnrollment` control
does not fire for a non-sample account).

### References

- ADR 0138 (the template this ADR re-applies) — the sample-data
  change-password lock: the four-conjunct decision, the
  `LocaleSettings.SamplePasswordChangeLocked` additive field, the
  "surface-replaced-by-notice" posture, the `GlobalAdmin` exemption.
- ADR 0056 (the `SampleData__Enabled` existence gate) — the
  "unreachable by construction" posture the MFA lock is gated on.
- ADR 0078 / ADR 0129 (the closed `SampleAccountEmails` set) — the
  single source of truth the MFA lock, the password lock, and the
  notification-suppression gate all compare against.
- ADR 0174 (the MFA enrollment surface + the pairing QR) — the
  `SecuritySettingsController` MFA actions the MFA lock guards.
- ADR 0004 §B.1 (the additive-field delta-detection) — the
  `LocaleSettings.SampleMfaLocked` field is delta-detected, no migration.
- ADR 0006 §A (the `IIdentityService` frozen surface) — the four
  additive methods this ADR gains.
- The F3/U4 login leg 1.5b (the `AccountController.Login` seam) — the
  `GlobalAdmin`-with-MFA-off enforcement the `IsSampleDataAccountAsync`
  exemption exempts the sample admin from.

---

## ADR 0117 — M16 Inventory (moved detail)

*Source: `docs/adr/0117-inventory.md`.*

### D7 — The close flip (U06 detail)

`Milestones.cs` M16 `StatusNext` → `StatusDone` + M17 `StatusPlanned` →
`StatusNext`; `MilestonesTests.cs` re-pinned to
`M17_Is_The_Single_InProgress_Milestone` + the shipped list gains M16 +
M18 stays planned; the README **Roadmap** (M16 → done, M17 → in
progress) + the M16 **Status** line; `docs/STATUS.md`; and
`docs/ARCHITECTURE.md`'s value-chain table gains the **M16 row** (the
table currently ends at the M15 row). All four surfaces ship **in the
same unit** (the M15/U10 docs-parity precedent, C-M16·7).

### Consequences — "Not chosen" (the full list)

- **A third "Usage" document or context for the history** (C-M16·3 — the
  usage history **is** the `InventoryCheckout` records; a separate
  "Usage" store would be a second copy to drift, reconcile, and
  authorize — the D1 *Forbids* pin).
- **A new `AccessAction` / `AccessVia` / `Decide()` branch / method on
  `IAuthorizationService`** (C-M16·4 / D2 — the adapter is the **only**
  new authorization surface; a branch would be a new decision path the
  frozen seam does not own).
- **`OwnerKind` as an enum or a read gate** (D3 / C-M16·1 — the label is
  a **string** (the M5 `KanbanStatuses` shape) and drives only the UI
  grouping + the write-standing breadth; the read decision is **always**
  the frozen Audience).
- **A notification on check-out/check-in** (D8 — the M6
  `INotificationService` emitter idiom is a **new trust boundary** — an
  outbound nudge on a state transition — that M16 deliberately does not
  take; the README's "optionally" is the usage history, F3 — own ADR,
  §deferred).
- **An off-by-default admin toggle** (D6 / C-M16·6 — M16 is a
  **milestone**, not a **lane**; M9's `LocaleSettings` toggle was the
  *lane* shape — M16 is the M5 *milestone* shape, always available).

### Deferred to their own lanes (each a named follow-on, own ADR)

- **The "nudge the owner on checkout" notification lane** (D8): the M6
  `INotificationService` emitter idiom + a Wolverine handler — a **new
  outbound surface** on a state transition that M16 deliberately does
  not take (the "optionally" is the **usage history** — F3). Own ADR as
  a new trust boundary.
- **The per-item translation lane** (the M5 `TodoTranslation` shape):
  the item's `LanguageCode` tag (ADR 0018) is present for display; a
  full translation record (one row per item × language, with its own
  author standing matrix) is a **different inventory** — an own lane,
  own ADR. M16's item is **single-language** by design (the README row
  does not name translations).
- **The media / attachments-on-items lane** (the ADR 0025 / ADR 0034
  shape): an image / a file on an item is a **media lane** over the
  ADR 0011 storage substrate — a new surface with its own standing
  matrix. M16's item is **text + ownership** (the README row names the
  *thing* and its *use*, not its *picture*). Own ADR.
- **The recurring / reserved check-out lane** (a pre-booked slot, a
  hold): a **reservation** is a second state (booked-but-not-checked-out)
  the F1 transition does not model — a new document, a new transition, a
  new standing matrix (who may book vs. who may check out). Own ADR as
  a new surface on top of the F1 shape.
- **A third "Usage" document or context** (C-M16·3 **forbids**): the
  usage history **is** the `InventoryCheckout` records (D4 / F3) — a
  separate "Usage" doc/context is the anti-pattern the D1 *Forbids:*
  names. No ADR is needed for what is forbidden.

---

## ADR 0043 — System pages shipped + discoverable (moved detail)

*Source: `docs/adr/0043-system-pages-shipped-and-discoverable.md`.
Supersedes: ADR 0005. Superseded by: ADR 0047.*

### D5 — Content scope (the two new `en` bodies)

The two new `en` bodies are **platform-level statements, not legal
advice**. They are the **shape contract** for U01 (`en`) and U02
(`de` / `fr`): the ADR fixes the structure (heading, intro, the bullet
topics, the closing line) and the topics each bullet covers; the final
copy is U01 / U02's, at the ADR 0042 D2 bar (idiomatic, register-held
`du` / `tu`, token-for-token, structure preserved exactly).

**`privacy` — a platform-level statement derived from `SECURITY.md`:**

- **Heading:** "Privacy" (page title).
- **Intro:** this is a self-hosted platform for **one** neighborhood — the
  operator runs the instance, and the operator is the **data controller**
  for everything on it.
- **Bullet topics:**
  - **What the platform stores:** the neighborhood's accounts, posts,
    groups, pages, and media — the database (Marten documents) plus the
    media volume. What we don't store, we can't leak (SECURITY.md §1,
    rule 5).
  - **Audience enforcement is the privacy mechanism:** content is
    deny-by-default; the author chooses each post's audience, and the
    platform enforces it on every request (SECURITY.md §4.4, ARCHITECTURE
    §4.4).
  - **Audit-by-default:** access to audience-restricted content and
    moderation/admin actions are always logged (SECURITY.md §1, rule 3).
  - **Backups, migration, and retirement are the operator's job:** the
    database and the uploaded files belong to the operator to back up,
    migrate, and retire (the same closing line the `terms` body already
    carries — the two bodies agree).
  - **The one browser cookie:** the **locale preference** (the ADR 0005 B
    cookie). **No third-party cookies exist or are planned** — recorded
    here; a future integration lane may amend by re-opening this decision.
- **Closing line:** the platform deliberately does **not** state
  operator-specific facts (retention periods, DPO contact, sub-processors)
  — those are the operator's, to edit in-app after first boot. The operator
  is expected to complete this page in the in-app editor; that is the
  design, not a gap.

**`conduct` — short and enforcement-neutral:**

- **Heading:** "Code of conduct".
- **Intro:** a bounded neighborhood — treat your neighbors the way you'd
  want to be treated on your street.
- **The lines (short list):** no harassment; no doxxing (revealing private
  details); no spam.
- **Closing line:** enforcement is the operator's / moderators' judgment —
  the moderation lane exists and is report-gated and audited (ADR 0003);
  this page states the expectation, not the procedure.

Two paragraphs + a short list, not a policy document. A dedicated **contact
page** is *out of scope* (the operator's `Community__SupportEmail` already
renders on the product-story view and the home hero — the `help` body names
it as the operator concern); a separate **cookie policy** is *out of scope*
(the single locale cookie is folded into the `privacy` body's cookie
section, D5 above).

### D7 — Scope (in/out list)

**In (this lane):** the `privacy` + `conduct` `en` bodies in
`EnDefaultPages()` (D5); their `de` / `fr` baselines in `DeDefaultPages()`
/ `FrDefaultPages()` at the ADR 0042 D2 bar (D5, D3); the two new
`PageTranslation` rows per page (D3); the `/privacy` + `/conduct` routes in
`StaticPagesController` (D2); the footer "Platform" column with its six
`footer.platform.*` registry keys in `EnValues` / `DeValues` / `FrValues`
(D4); the `/admin` shell "Platform pages" section (D4); and the test pins
(the Core parity / seed family moving 2 → 4 pages; the Web surface pins
for the routes, the floor, the footer, and the admin section).

**Out (→ future lanes):** machine translation of anything (ADR 0005 C — a
deferred trust boundary, unchanged); a dedicated **contact** page (the
`help` body names the operator's `SupportEmail` as the concern — the design
already covers it); a separate **cookie** policy (folded into the `privacy`
body, D5); **additional** system pages (a GlobalAdmin adds them under
`system/` at runtime — the ADR 0040 §2 design, not this lane's shipped set);
and the `Milestones.cs` / README / `MilestonesTests` trio **until the lane
ships** (U05 owns it — the Consequences below).

### Superseded record (2026-09-19)

> **Superseded 2026-09-19 by [ADR 0047](../adr/0047-static-page-localization-and-backfill.md)
> (D1).** The "en-body by contract" record above was the state at U05 and
> remains the **floor** (an absent translation still renders the `en`
> body, and the ADR 0027 chip-swap is still *not* on the hard-coded
> routes). But the hard-coded routes now **render the matching
> `PageTranslation` in the request's effective language** (the ADR 0046
> chain), and a warm-boot backfill (ADR 0047 D2) adds the missing
> `de` / `fr` / `da` rows. So under a `de` / `fr` / `da` preference the
> body is now localised — this "en-body" line is the degradation case, not
> the normal case. See ADR 0047 for the locked decision.

---

## ADR 0132 — Onboarding (moved detail)

*Source: `docs/adr/0132-onboarding.md`.*

### D7 — The closed `onboarding.*` kw-l key set (15 keys × 4 languages)

The locked closed set (15 keys): `onboarding.title`, `onboarding.intro`,
`onboarding.step_displayname`, `onboarding.step_avatar`,
`onboarding.step_language`, `onboarding.step_timezone`,
`onboarding.step_dateformat`, `onboarding.step_email`,
`onboarding.step_contact`, `onboarding.visit`, `onboarding.finish`,
`onboarding.skip`, `onboarding.flash_done`, `onboarding.banner.text`,
`onboarding.banner.action`.

### D8 — Deferred lanes (each a future ADR, not part of M22)

(1) Per-step progress / resume-across-sessions; (2) "you haven't set X"
detection; (3) onboarding for elevated/guest accounts; (4) an admin
"onboarding completion rate" metric; (5) I18n beyond the four-language
floor.

### The acceptance tests (GATE-1…GATE-6)

GATE-1 — one additive field, `null` = not completed (C-M22·1, D1).
GATE-2 — the completion stamp is one owner-scope write, no audit row, no
load-or-create (C-M22·3, D2). GATE-3 — zero new field-write lanes; the
walk-through rides the frozen lanes (C-M22·4, D3). GATE-4 — zero new
authorization surface (C-M22·2, D6). GATE-5 — the banner is gated on the
completion read and non-blocking (C-M22·5, D5). GATE-6 — the closed
`onboarding.*` set is parity-pinned in four languages (C-M22·6, D7).

---

## ADR 0083 — Group membership notifications (moved detail)

*Source: `docs/adr/0083-group-add-and-invite-notifications.md`.
Supersedes: ADR 0007.*

### Amends (the frozen-surface ledger)

**0007** (the group-management lane — owner ∪ GlobalAdmin is the standing
that *adds* / *invites*; the two emitters sit exactly on the two write
seams that ADR 0007's SoD gate protects, and the recipient is always the
*other* resident, never the actor), **0008** (the self-leave lane — its
write seam `RemoveGroupMemberAsync` deliberately does **not** emit: a
self-leave is not something to be notified *about*), **0010** (private
groups — the Privacy tab this ADR re-homes; a private group's add/invite
carries the same notification as a public one), **0013** (the group-post
lane — its "notifications on new group posts" deferral was settled by ADR
0076's `group.post` kind + wired emitter; this ADR is the sibling: the
*membership-change* kinds, not the *content* kind), **0026** (group
name/description translations — the Translations tab this ADR re-homes,
unchanged in behavior), **0076** (the M6 notifications context — this ADR
**adds two kinds** to the closed `NotificationKinds` vocabulary D2 froze,
**reuses verbatim** the `NotificationService.EmitAsync` seam D1/D4/D5/D7
pinned, the frozen `IMailerStage.StageAsync` trio, and the ADR 0061
per-recipient language resolution; **no new context, no new document, no
new surface, no new service**, the settings toggle list simply grows by
two), **0077** (the admin-lane kinds — the precedent for "a kind whose
recipient is not the acting resident"; here the recipient *is* the affected
resident, so it is the resident lane, not the admin lane), and **0078**
(sample-account suppression in production — inherited by the two new kinds
for free: the `EmitAsync` suppression gate is kind-agnostic).

### D3 — The circular-dependency seam (full detail)

The `NotificationService` constructor takes `IUserInfoService` (for the
recipient's `Profile.EmailLanguage` + `Email`, ADR 0061), so injecting a
`NotificationService?` **directly** into `UserInfoService` would create a
construction-time cycle (`IUserInfoService → UserInfoService →
NotificationService → IUserInfoService`). The `IdentityService` emitters
(ADR 0077) only avoid this because `IIdentityService` is a *different*
registration than the `IUserInfoService` the `NotificationService`
consumes. The fix here: `UserInfoService`'s primary-ctor gains a
**trailing, optional** `IServiceProvider? services = null`, and the two
emitters resolve the `NotificationService` **at emission time** via
`services?.GetService<Notifications.NotificationService>()` — deferred to
runtime, which breaks the construction-time cycle (the service graph is
only assembled when an emission actually happens, by which point both ends
of the would-be cycle are already constructed). The `null` default preserves
the **181 pre-ADR 0083 direct-construction test call sites** that build
`UserInfoService(store)` positionally: they get `services = null`, the
`GetService` call is a no-op, and the emission is **silently skipped** (the
write still commits; no notification row) — exactly the pre-M6 behavior, so
no existing test's expectations change and no harness is forced to wire the
`NotificationService`. The DI container (which *does* register the
`NotificationService`) resolves `IServiceProvider` and supplies it, so the
production emitters are live.

### D6 — The kw-l key table (32 entries)

Per kind the registry gains exactly the four keys the parity test pins, in
**en / de / fr / da**: `notifications.kind.{kind}` (the settings + inbox
label), `notifications.preference.{kind}.label` (the preferences toggle),
`notification.{kind}.subject` (the email subject), `notification.{kind}.body`
(the email body template — the UGC snippet, the group name, is appended
after one space, so the templates carry a trailing space). That is 8 new
keys × 4 languages = 32 entries added to `KnownTranslationKeys`, and the
`KwLRegistryConsistencyTests` parity check (exact key-set equality across
the four dictionaries) is what enforces the count.

### D7 — The group detail page tab layout (full detail)

`Views/Groups/Detail.cshtml` restructures the existing sections into the
Bootstrap `nav nav-tabs` idiom (the `_ProjectsTabs.cshtml` precedent, ADR
0062) with the header (back link, `<h1>` group name, "Owned by …" + the
owner/private badges, the **description read**) above the tab bar:

- **Feed** — the membership-scoped posts (ADR 0013): the "New post" button
  (when `Model.CanPost`), the empty state, and the posts `list-group`. This
  is the default tab and the one every member lands on.
- **Members** — the member list (with the per-row **Remove** for
  `canManage` / **Leave** on the actor's own row, ADR 0007/0008), the
  owner's **pending invitations** list, the **invite** form (owner), and
  the **add-member** form (`canManage`).
- **Settings** — rendered **only when `canManage`**
  (`Model.IsOwner || GlobalAdmin`, the C-M2·3 owner ∪ GlobalAdmin standing
  the controller's `TryResolveOwnerSurface` gates already enforce): the
  **About** (description *edit*) form, the **Privacy** toggle (ADR 0010,
  `isPrivate`), and the **Translations** block (ADR 0026, the chips +
  per-translation `<details>` + the add-translation forms when
  `Model.CanTranslate`).

The **active tab is derived server-side from the `?tab=` query string**
(`?tab=feed|members|settings`), so a deep link `/groups/{id}?tab=settings`
lands on Settings and the "back to the group" links from a post's detail
page drop the query and fall back to the default (Feed) — no client-side
tab JS, no new dependency (the ADR 0031 tsc-only / no-dependency pin).
The description **read** stays in the header (a group's blurb sits above
the tabs, like a profile's bio); only the description **edit** form moves
into Settings.


---

## ADR 0107 — M10 PWA + responsive design (moved detail)

*Source: `docs/adr/0107-pwa-and-responsive-design.md`. Supersedes: ADR 0031.*

### D2 — The SW allowlist (the 15-path closed set)

The SW intercepts **only** same-origin `GET` responses whose path is on a
closed 15-path allowlist (the app-shell floor: `/`, `/about`, the layout
CSS, the manifest, and the signed-out shell's `client/lib` module subset).
**Every other request — all signed-in pages, all `POST`s, `HEAD`/`OPTIONS`,
any request carrying an `Authorization` header — passes through
untouched** (the amended rule — a `return` without calling
`event.respondWith()`, so the request never routes through the worker's
own `fetch`; the original `event.respondWith(fetch(event.request))` form
broke form submissions). This is the privacy pin (C-M10·2): the SW's cache
partition can never hold a resident's feed, a conversation, an inbox, or a
board. The negative pin (a signed-in route is not in the cache after a
visit) is the U06 Playwright spec's
`SignedIn_Route_Not_In_ServiceWorker_Cache`.

### D3 — The manifest shape

`wwwroot/manifest.webmanifest`: `name` / `short_name` / `start_url: "/"` /
`display: "standalone"` / `background_color: "#fbfaf7"` +
`theme_color: "#1c4532"` (read from `site.css` `:root` — `--bs-body-bg`
line 78, `--bs-primary` line 106) / `icons` (192×192 `purpose: "any"` +
512×512 `purpose: "any maskable"` with a safe-zone) / `id: "/"` /
`scope: "/"`. **No `shortcuts`, no `screenshots`, no `categories`** —
honesty over feature-creep. Linked from `_Layout.cshtml` `<head>` via
`<link rel="manifest" href="~/manifest.webmanifest" />`.

### D9 — The test split (the locked names)

(a) `e2e-pwa-responsive.spec.ts` (U06) — the nine locked test names:
manifest fetch + shape, the two icon files 200 + correct dimensions
(asserted by reading the PNG IHDR bytes — no image-decode dependency), the
SW registered in Chromium, the offline-shell revisit of `/`, the **negative
pin** (a signed-in route is not in the SW cache after a visit), the 360 px
viewport pass over the pinned surfaces with no horizontal overflow, and the
two a11y floors. (b) One xUnit class `PwaManifestTests` —
`manifest.webmanifest` parses as JSON with the required fields at the
locked values, both icon paths exist in the repo, the `<link
rel="manifest">` is in `_Layout.cshtml`, the `sw.js` allowlist
string-matches the design doc §SW verbatim, the `pwa.install` key is
registered in all four languages, and the `site.css` `@media` block
boundary + count hold (four `max-width: 767.98px` blocks + one
`prefers-reduced-motion` block).

### D8 — The a11y floors (the closed element lists)

(a) **touch targets** — the closed list: the `.navbar-toggler`, the
`a#accountMenu` toggle, the `a#notifications-bell` anchor, the
account-menu + language-picker `dropdown-item` rows, the flash toast's +
the pinned announcement's `.btn-close`s (U04's half), and every
`.action-glyph-btn` (ADR 0092 — U05's half) — each has a rendered hit area
≥ **44×44 px** at the 360 px viewport. (b) **focus** — the closed list:
the `.action-glyph-btn` `:focus-visible` ring + the `.kmb-flash-toast`
`.btn-close` witness — a visible `:focus-visible` on every element on it.

### Deferred lanes (each a named follow-on, own ADR)

- **PWA push notifications** — a `PushManager` registration + the M6
  `EmitAsync` lane's push branch + the admin's push-key management. Own
  ADR, under ADR 0076. **This ADR supersedes the deferral's *substrate*
  claim** and **re-names the follow-on lane "PWA push notifications"**;
  the push itself stays deferred.
- **Tablet responsive pass** — the `768–991 px` tablet range (Bootstrap's
  `md` edge) is a follow-on polish lane. Own ADR.
- **Offline authenticated pages** — a per-resident offline cache of
  *their* feed, keyed on the `ActorId` + a per-device encryption envelope.
  A follow-on lane with its own threat model + ADR.
- **`maskable` icon refinement** — a per-OS adaptive icon set (Android's
  `any` + `maskable` split, iOS's `apple-touch-icon` 180×180). A follow-on
  polish lane.
- **`screenshots` / `shortcuts` manifest enrichment** — the W3C optional
  fields require per-language screenshot assets + the shortcut's target
  routes. A follow-on lane.

---

## ADR 0156 — M33 Storage metrics history (moved detail)

*Source: `docs/adr/0156-storage-metrics-history.md`.*

### D2 — The `StorageMetricsSample` (the 8-member ceiling)

The doc carries **exactly** 8 members (the `Id` + the 7-member sample
shape mirroring the M24 `StorageMetricsSnapshot`, with `SampleDate`
replacing `AsOf`): `Id` (string, **deterministic** — `"smh-" + yyyy-MM-dd`
from the UTC `SampleDate`) · `SampleDate` (DateTimeOffset, UTC) ·
`TotalUsedBytes` (long) · `TotalVolumeBytes` (long) · `FreeVolumeBytes`
(long) · `UserContentUsedBytes` (long) · `TotalUniqueFiles` (int) ·
`TotalDistinctUsers` (int). One sample **per UTC day**; the
**deterministic `Id`** means a same-day re-capture **overwrites** the row
(idempotent-by-construction; **no** dedup query needed). **Not** an
`IAuditableResource` (the M13 C-M13·6 precedent, M33·4).

### D5 — The capture lane (the full code shape)

The `StorageMetricsCaptureService` (a **Wolverine-free static class** in
`Kumunita.Core.Usage`, the `UsagePurgeService` house shape) carries
`public const int RetentionDays = 365` +
`public static async Task<int> CaptureAndPurgeAsync(IDocumentStore store,
IStorageMetricsService metrics, DateTimeOffset now, CancellationToken ct =
default)`. The impl: (1) computes the M24 snapshot via
`metrics.GetSnapshotAsync(ct)`; (2) builds the `StorageMetricsSample` (the
deterministic `Id` = `"smh-" + yyyy-MM-dd` from `now`'s UTC day; the 7
data members mirroring the M24 `StorageMetricsSnapshot`, `SampleDate`
replacing `AsOf`); (3) in **one write session** stores the sample +
**purges** rows with `SampleDate < now − RetentionDays` (batched
id-collection + delete, one `SaveChangesAsync`); (4) returns the purge
count. **Zero** `AccessAudit` rows (M33·4). The
`StorageMetricsCaptureTick`: `public sealed record
StorageMetricsCaptureTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));`.
The `StorageMetricsCaptureHandler` (Web `SideEffects/`):
`public static async Task<IEnumerable<object>> Handle(
StorageMetricsCaptureTick tick, IDocumentStore store,
IStorageMetricsService metrics) { await
StorageMetricsCaptureService.CaptureAndPurgeAsync(store, metrics,
DateTimeOffset.UtcNow); return new[] { new StorageMetricsCaptureTick() }; }`.
The **Program.cs boot seed**: next to
`await bus.PublishAsync(new UsagePurgeTick());`,
`await bus.PublishAsync(new StorageMetricsCaptureTick());`.

### Named deferrals (each a future lane, own ADR)

- **per-instance retention knob** (a `/admin/storage/settings` retention
  field — M33 ships the `RetentionDays = 365` platform constant, M33·7).
- **CSV export of the history** (the M13 "export = one audited row"
  precedent — M33 v1 has **no** export lane, so **zero** `AccessAudit`
  rows).
- **capacity-projection / forecast** (a linear-regression "days until
  full" estimate — M33 ships the trend, the operator reads the slope).
- **per-user over-time history** (a per-user **over-time** breakdown).
- **resident-facing history** (M33 is `GlobalAdmin`-gated — no resident
  surface).
- **the `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip** until the
  milestone *ships* (U08 owns it — M33·12).

---

## ADR 0058 — Page "Reset to seeded text" lane (moved detail)

*Source: `docs/adr/0058-page-reset-to-seeded-text-lane.md`.*

### D2 — The seam (service lane + pure applier)

- **`FirstBootSeeder.ResetSeededTextAsync(session, page, now, ct)`** — a
  pure applier on the **seeder** that takes the already-loaded `Page`,
  overwrites its `Title`/`Body` with the seeded English baseline, and
  upserts (create-or-overwrite) the `de`/`fr`/`da` `PageTranslation` rows
  from the seeded baselines. The seeder is the single source of the
  seeded text, so the reset reads **the same** registries the seeder
  writes on first boot. It throws `InvalidOperationException` if the
  page's slug is not a seeded slug.
- **`PageService.ResetToSeededAsync(pageId, actorId, actorRoles, session)`**
  — the **service lane** that owns standing, the 404 contract, the audit
  row, and the single `SaveChangesAsync`. It loads the page (null →
  `KeyNotFoundException`), checks the edit standing (denied →
  `UnauthorizedAccessException`), delegates the text applier to the seeder,
  writes the audit row in the same session, and saves once.

### D4 — Per-language behaviour

- **A row that exists is overwritten** with the seeded baseline for that
  language (the admin's customised wording is *replaced*).
- **A row that is missing is created** (the seeded baseline fills it in).
- **Languages the seeded baseline does not carry are untouched.** The reset
  only ever writes the languages the code's seeded registries define for
  that slug; any other `PageTranslation` row (a community-authored `es`
  row, say) is **left alone**.

The reset is **idempotent**: running it twice yields the same page; a second
run does not create duplicate rows (the per-row `Id` is stable).

### D7 — The Web surface

- **Route:** `POST /pages/{id:guid}/reset-seeded` on the existing
  `PageController`, `[Authorize]` + `[ValidateAntiForgeryToken]`.
- **Mapping:** `UnauthorizedAccessException` → `403`,
  `KeyNotFoundException` → `404`, `InvalidOperationException` (non-seeded
  slug) → **`400`** with the exception message added to the model state.
- **Button:** a single outline-danger "Reset to seeded text" form button
  in the page edit form's action bar, rendered **only when** the form is
  the edit view (`isEdit`) **and** `Model.CanReset` is true. It carries
  the `confirm()` guard (D5) and an anti-forgery token.
- **`CanReset`:** a `[BindNever]` view-model flag set by the Edit GET to
  `FirstBootSeeder.HasSeededText(page.Slug)` — a **pure registry probe**.
  A page with no seeded baseline simply hides the button.
- **Localisation:** the button label uses the existing `kw-l` key
  (`pages.resetSeeded`), added to the en/de/fr/da `KnownTranslationKeys`
  registry.

### Follow-on + References

- **ADR 0128** — the **bulk** variant on top of this lane: an
  `/admin/help` surface that surfaces *which* seeded pages have newer
  shipped text and resets **every** seeded page in one audited action.
  It reuses this lane's `ResetSeededTextAsync` applier and `page.reset`
  audit verbatim.
- **Code:** `FirstBootSeeder.ResetSeededTextAsync` +
  `FirstBootSeeder.HasSeededText` (the applier + the pure probe),
  `PageService.ResetToSeededAsync` (the service lane),
  `PageController.ResetToSeeded` (the route),
  `PageComposeViewModel.CanReset` (the `[BindNever]` flag),
  `Views/Page/_PageForm.cshtml` (the button), `KnownTranslationKeys`
  `pages.resetSeeded` (the UI string).
- **Tests:** `PageServiceTests` `PG6_…` suite (the four-surface overwrite,
  the create-absent-row case, the non-seeded-slug 400, the standing matrix,
  the blog-author standing, idempotence, the missing-page 404, and the
  `HasSeededText` probe).

---

## ADR 0115 — Integration of Events and Projects (M14) (moved detail)

*Source: `docs/adr/0115-integration-of-events-and-projects.md`.*

### D4 — The `VTODO` emitter (the closed pinned subset)

The **closed, pinned `VTODO` subset**: the `VCALENDAR` envelope (the ADR
0112 envelope verbatim — `VERSION:2.0`,
`PRODID:-//Kumunita//community calendar//EN`, `CALSCALE:GREGORIAN`,
`METHOD:PUBLISH`) + one `VTODO` per **dated** to-do (`DueAt != null ||
StartAt != null`; neither ⇒ **skipped**) with the pinned order `UID` /
`DTSTAMP` / `DUE` (cond.) / `DTSTART` (cond.) / `SUMMARY` /
`DESCRIPTION` (cond.) / `STATUS:CANCELLED` (cond.). **`UID`** =
`kw-todo-{todo.Id}@kumunita` (**stable**). **`DTSTAMP`** = the
caller-passed `nowUtc`. **`SUMMARY`** = `Title`; **`DESCRIPTION`** = `Body`
as **Markdown source** (not rendered HTML). **Never emitted** (the
"never" list): `ORGANIZER` / `CREATED-BY` / `ATTENDEE`, `RRULE` /
`RECURRENCE-ID`, `Audience` / grant / membership internals, `AuthorId` /
`AssigneeId` / `ComponentId` / `ProjectId` / `EventId` / `ParentId` /
`BlockedByTodoId` / `Status` / `LanguageCode` / `TagIds` / `ImageIds` /
`AttachmentIds` / `Created`, and **any** `X-…` vendor property. CRLF
endings; ≤ 75-octet fold (UTF-8-safe boundary + leading-space
continuation); escaping (`\` → `\\` first, `;` → `\;`, `,` → `\,`,
CR/LF → `\n`).

### Consequences — "Not chosen" (the full list)

- a **many-to-many `EventTodoLink` doc** (D1's 1:1 field is simpler +
  matches the `ProjectId` "a to-do is *in* one project" precedent).
- a **`Kumunita.Core.Events` → `Kumunita.Core.Projects` dependency** (the
  ADR 0006 "a bounded context per concern" rule — the composition point is
  the `EventController`).
- a **new authorization surface** (C-M14·4 — the frozen `Read` decisions
  already cover both surfaces).
- a **subscription token** (a standing credential the resident would share
  — the M6 link-lane posture, ADR 0085 / ADR 0095, wins).
- an **`ORGANIZER` / `ATTENDEE`** property (it would leak the author /
  assignee identity into a file that travels — the ADR 0028 posture wins).
- a **`RRULE` / recurrence field** (C-M14·6 — **M18's** home, the source
  of truth is `Milestones.cs`).

### Superseded (stale forward-refs)

ADR 0112's Consequences "Deferred … **recurring events** (`RRULE` —
**M14** is the natural home)" and the `IcsWriter.cs` doc-comment "M14's
home" ref. Both are superseded by this ADR: **`RRULE` / recurrence is
M18's home** (D6), **not** M14's — M14's scope is the interlock, not
recurrence.

### Deferred lanes (each a named follow-on, own ADR / own milestone)

- **recurring events / `RRULE`** (**M18** — the source of truth is
  `Milestones.cs`).
- **`RRULE` on the `VTODO` surface** (a to-do's recurrence rides M18's
  recurrence concept).
- **ICS import** (round-tripping calendar files **into** Kumunita).
- **per-component calendar feeds** (a `?component=` scoping of the `VTODO`
  feed).

---

## ADR 0064 — The events calendar DWM views (moved detail)

*Source: `docs/adr/0064-events-calendar-dwm-views.md`. Supersedes: ADR 0063.*

### D2 — The per-view window (the controller computation)

`?view=day|week|month` on the **existing** `GET /events/calendar` route.
The window is computed **in the controller**, in the effective zone (ADR
0019): **`day`** — `[anchorLocalMidnightUtc, +1d)`; **`week`** — the
anchor's **Monday-start** week: 7 days from the Monday on or before the
anchor; **`month`** — the anchor's **calendar month**: from local midnight
of the 1st to local midnight of the 1st of the next month (a real,
variable span — the controller's `AddMonths(1)` handles it). **`month` is
the default** — a missing `?view` renders exactly the shipped EV-CAL
behavior reframed to the calendar month. An unparseable / out-of-set
`?view` value **falls back to `month`** — a display fallback, not an error.

### The invariants (C-DWM·1–C-DWM·9, pinned verbatim)

- **C-DWM·1** — Zero Core change. No seam added, no `Event` / `EventRsvp` /
  `EventTranslation` field, no `EventDocTypes` / boot-path change, no EF,
  no new audit row shape. The only additive C# is the additive view-model
  fields (`View` + `WindowDays`, `MonthLabel`→`Label`) and the `?view=`
  param on the existing `Calendar` action.
- **C-DWM·2** — Same visibility as EV-CAL. Each view shows *exactly* what
  `ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows for that window.
- **C-DWM·3** — `?view=` is a display selector, never an access input. An
  unparseable / missing / out-of-set `?view` **falls back to `month`**.
- **C-DWM·4** — Overlap + time-positioning are client-side display concerns
  over already-authorized data.
- **C-DWM·5** — Week starts Monday; the zone is display-only, never an
  authorization input.
- **C-DWM·6** — Navigation is plain GET links, pre-rendered by the server;
  no POST, no client state, no round-trip JS for nav.
- **C-DWM·7** — Plain TS, zero deps; the existing
  `client/lib/events-calendar.ts` is **not modified**.
- **C-DWM·8** — Backward-compatible; `?view=month` is the default; the
  list view is unchanged.
- **C-DWM·9** — Localization parity: the 3 new `kw-l` keys
  (`events.calendar.view.day/.week/.month`) are present in **all four**
  seeded languages.

### The 9 pinned tests

**2 Core additive pins**
(`tests/Kumunita.Core.Tests/EventCalendarSeamTests.cs`):
`EV_Range_OneDayWindow_OnlyThatDay`, `EV_Range_SevenDayWindow_OnlyThoseDays`.

**7 Web pins** (appended to
`tests/Kumunita.Web.Tests/EventControllerTests.cs`):
`Calendar_DefaultViewIsMonth_BackwardCompat`,
`Calendar_ViewDay_WindowIsAnchorDayOnly`,
`Calendar_ViewWeek_WindowIsAnchorWeek_MondayStart`,
`Calendar_ViewMonth_WindowIsAnchorCalendarMonth`,
`Calendar_InvalidViewFallsBackToMonth`,
`Calendar_ViewRidesAlongInPrevNextNavLinks`,
`Calendar_Label_IsViewAppropriate`.

**The three-test acceptance gate** (recorded by the lane's U07):
**closed loop**, **handoff**, and **part-vs-whole**.


---

## ADR 0142 — Delete account (moved detail)

*Source: `docs/adr/0142-delete-account.md`. Supersedes: ADR 0006.*

### The seam detail (the two branches + both-branch invariants)

`IIdentityService.DeleteAccountAsync(string targetSubjectId, string
adminSubjectId)` is the single write lane. Two branches, one seam (the
ADR 0138 self-serve/admin distinction):

1. **Self-deletion** (`adminSubjectId == targetSubjectId`) — the resident
   deletes their own account (the Web `POST /account/delete`, after
   verifying their current password). **ADR 0142 D5 gate:** only a
   `GlobalAdmin` may self-delete (a non-GlobalAdmin's invocation throws
   `UnauthorizedAccessException` — the fail-closed pin). The audit row is
   `Via: Owner`.
2. **Admin-initiated** (`adminSubjectId != targetSubjectId`) — a
   `GlobalAdmin` removes another resident (the Web `POST /admin/delete`).
   The `RequireGlobalAdminAsync` gate applies. The audit row is `Via:
   Admin`.

**Both branches:**

- **Last-GlobalAdmin guard** (the lockout pin): if the target holds the
  `GlobalAdmin` role and they are the *only* `GlobalAdmin` on the
  instance, the lane throws `InvalidOperationException` **before any
  write** (the `OPS.md` §9 "Hand over admin" precedent). A lone
  `GlobalAdmin` cannot self-delete; no admin may delete the last
  `GlobalAdmin`. The recovery path is to promote a second `GlobalAdmin`
  first. *Design consequence:* on the admin-initiated branch, the actor
  must be a `GlobalAdmin` to pass the admin gate, so when the target is
  also a `GlobalAdmin` there are at least two `GlobalAdmin`s and the guard
  never fires — the guard is only reachable on the self branch. The Core
  test `DeleteAccountAsync_AdminInitiated_TargetIsGlobalAdmin_GuardPasses_
  BecauseActorIsAlsoGA` pins that the admin branch *can* delete a
  `GlobalAdmin` target; `DeleteAccountAsync_SelfDeletion_LastGlobalAdmin_
  ThrowsAndDoesNotWrite` pins the guard on the self branch.
- **Pseudonymization** (the `OPS.md` §9 / data-model policy): the target's
  `AccessAudit` rows *remain*, but their `ActorId` and
  `EffectivePrincipalId` (when equal to the target) are rewritten to the
  deterministic `deleted:{targetSubjectId}` tombstone. The tombstone is
  *deterministic* (derived from the subject id, not a fresh GUID) so a
  re-deletion re-attaches to the same tombstone.
- **Membership removal** (the strong-consistency C4 invariant): the
  target's `GroupMembership`, `ComponentMembership`, and
  `ModeratorAssignment` rows are deleted. The next
  `GetGroupIdsAsync` / `GetCommunityIdsAsync` read misses them.
- **Profile deletion**: the target's `Profile` row is removed (their PII is
  gone). The directory and every author-name resolution already falls back
  to the raw subject id when the profile is absent (the
  `profile?.DisplayName ?? authorId` idiom).
- **Identity account deletion**: the target's ASP.NET Identity account is
  deleted. The `PrivilegedStampMiddleware`'s existing "user was deleted
  while signed in" branch is the *consequence* path — this lane is the
  *cause* path.
- **Exactly one audit row** (the `AuditPurgeSummary` "one summary, many
  rows" shape): action `"account.delete"`, `TargetKind` "account",
  `TargetId` the subject, `Via: Owner` or `Via: Admin` per the branch,
  `Outcome: Allow`.
- **Fail-closed / idempotency**: a missing target throws
  `InvalidOperationException` before any write. A second call for the same
  subject throws the same `InvalidOperationException` (the Identity row is
  gone).

### The cross-store ordering

The lane commits the Marten writes (the pseudonymization + membership
removal + profile deletion + the summary audit row) **first**, then deletes
the Identity account (EF Core, the `identity` schema). The ordering
inverts the register/verify lanes' ordering (the `mt` cleanup commits
first, the identity row is removed second) so that a failure *between* the
two leaves the account still present (a recoverable state) rather than the
account gone with the trail still carrying the identity (an unrecoverable
privacy violation).

### The Web surfaces

- **`GET /account/delete`** + **`POST /account/delete`** (the resident
  self-serve surface, on the `AccountController` beside
  `ChangePassword`). The form asks for the resident's current password
  (verified server-side via `CheckPasswordAsync`) and an explicit
  acknowledgment checkbox. **ADR 0142 D5 gate:** when the signed-in
  resident is not a `GlobalAdmin`, the view renders the
  `SelfDeletionRefused` notice (the ADR 0138
  `ChangePasswordLockedViewModel` "surface-replaced-by-notice" shape,
  generalized). On success the resident is signed out and redirected to
  the login page with a positive confirmation.
- **`POST /admin/delete`** (the GlobalAdmin removal surface, on the
  `AdminController` beside `Block` / `Unblock`). A `GlobalAdmin` deleting
  their own account through this surface is refused up-front — they use
  `/account/delete` instead. The button is hidden on the admin's own row in
  both the `Accounts.cshtml` list and the `Manage.cshtml` detail.

### The translation keys

One closed `account.delete.*` + `admin.delete_account.*` +
`nav.delete_account` `kw-l` set × en/de/fr/da (the ADR 0015 closed-registry
shape, the ADR 0138 `account.change_password.*` precedent), registered in
`KnownTranslationKeys` and seeded by the `FirstBootSeeder`'s existing
`en`-floor + `de`/`fr`/`da` baselines.

---

## ADR 0105 — M9: 1:1 resident messaging (moved detail)

*Source: `docs/adr/0105-messaging.md`. Supersedes: ADR 0004.
Superseded by: ADR 0165. Amended by: ADR 0139.*

### D5 — The audit table

| seam | `Action` | `TargetKind` | `TargetId` | `Via` | `Outcome` |
|---|---|---|---|---|---|
| `OpenConversationAsync` | `message.open` | `message` | conversation id | `Owner` | `Allow` |
| `SendAsync` | `message.send` | `message` | conversation id | `Owner` | `Allow` |
| `SetMessagingEnabledAsync` | `messaging.toggle` | `messaging.toggle` | `messaging.toggle` | `Admin` | `Allow` |

**Reads emit no audit row** (list / thread-read / mark-read — the ADR
0076 F11 personal-read shape).

### The seam (the locked shape — the design doc §2.2 verbatim text)

```csharp
public interface IMessagingService
{
    Task<bool> IsMessagingEnabledAsync();
    Task SetMessagingEnabledAsync(bool enabled, string actorId);

    Task<ConversationRef> OpenConversationAsync(string actorId, string otherId);
    Task<ConversationDetail> GetConversationAsync(string conversationId, string actorId, int page);
    Task<ConversationList> ListConversationsAsync(string actorId, int page);

    Task SendAsync(string conversationId, string actorId, string body);
    Task MarkReadAsync(string conversationId, string actorId);
}
```

`OpenConversationAsync` is **idempotent on the unordered pair** (the
sorted `(ParticipantA, ParticipantB)` unique index is the witness).
`MessagingService` composes `IDocumentStore` + the optional
`NotificationService` (the ADR 0077/0083 optional-nudge-param idiom) +
`IUserInfoService.GetProfileAsync` (frozen, the `OtherDisplayName`
resolution, null-safe to the raw subject id) and is registered in
`DependencyInjection.cs` in the `AddTransient<IMessagingService>` factory
shape.

### The tests (the pinned set — design doc §2.5 authoritative)

Core: `MessagingServiceTests` over the `PostgresFixture` harness — 14 pins
(2 toggle + 12 behavior; the leak pins
`GetConversation_NonParticipant_404_Not403` /
`GetConversation_GlobalAdminNonParticipant_404` /
`ListConversations_OnlyOwn_NoLeak` are the D3 witnesses,
`ToggleOff_AllSeamsRefuse_403` the D2 witness,
`Send_OtherParticipant_GetsMessageNewNotification_Once` the D6 dedup
witness). Web: `MessagesControllerTests` over NSubstitute
(`IMessagingService` substituted, no Postgres — the
`AnnouncementControllerTests` harness) — 10 pins.

---

## ADR 0047 — Static-page localization + backfill (moved detail)

*Source: `docs/adr/0047-static-page-localization-and-backfill.md`.
Supersedes: ADR 0043, ADR 2026.*

### D1 — The read path (the four hard-coded routes)

- **The four `Page`-backed routes only** — `/terms`, `/help`, `/privacy`,
  `/conduct` — now render the body of the **matching** translation. The
  `/about` route is **unchanged** (the product-story view, not a `Page`).
- **One consistent read, then a single pick.** The controller reads the
  page *and* its translations in one call —
  `IPageService.ResolvePageAsync(path)` — and selects the
  `PageTranslation` whose `LanguageCode` equals the request's **effective
  language** (the ADR 0046 chain, resolved by the existing
  `ITranslationProvider.ResolveEffectiveLanguageAsync` overloads).
- **The floor is the `en` body.** When no `PageTranslation` row matches the
  effective language, the route renders `page.Title` / `page.Body`. So
  **the worst case is exactly today's behaviour** (the `en` body); the best
  case is the matching translation.
- **`en` is still the source of truth and the floor, forever (ADR 0042
  D1, unchanged).** The ADR 0027 chip-swap display surface is **untouched**
  (the `/pages/{path}` show surface keeps its variant chips).

### D2 — The warm-boot backfill (create-if-missing)

- **A new public seeder primitive, run on every warm boot.**
  `FirstBootSeeder.BackfillPageTranslationsAsync(session, ct)` runs in the
  **`else` branch** of the pristine gate in `SchemaBootstrap`. The
  pristine-boot path is **unchanged**.
- **Scoped to the four canonical pages, the `de` / `fr` / `da` rows
  only.** For each of the `EnDefaultPages()` slugs (`terms` / `help` /
  `privacy` / `conduct` — `about` excluded), the backfill creates the
  `PageTranslation` row from the baseline if — and only if — no row for
  that `(PageId, LanguageCode)` already exists. The created row is
  byte-identical to what a fresh seed would have produced.
- **Create-if-missing, never overwrite (ADR 0042 D1, held).** The `en`
  `Page.Body` is never read or written by the backfill.
- **Idempotent.** A second warm boot finds every row the first created and
  skips all of them.
- **This is the narrow exception to ADR 0042 D1's "no warm-reseed
  mechanism," recorded as a decision, not a drift.**
  > **Amended 2026-09-20 (ADR 0052):** the UI-string baselines **are** now
  > backfilled on a warm boot too — the same create-if-missing, idempotent,
  > never-overwrite shape.

### D4 — The new `IPageService` read seam

`IPageService.ResolvePageAsync(string path)` returns
`(Page Page, IReadOnlyList<PageTranslation> Translations)` in **one
document-session read**: the page resolved by the path plus the page's
`PageTranslation` rows ordered by `LanguageCode`. A missing page is
`KeyNotFoundException`; an existing page with no translations returns the
page with an **empty** translation list. **Core stays HTTP-free (M·8 / ADR
0001-B, held)** — the seam reads no cookie, no header, no `HttpContext`.

### Revisit when

- A **fifth canonical page** is added (a new `system/` slug in
  `EnDefaultPages()`): D1's render and D2's backfill are both **generic
  over the `EnDefaultPages()` slug set**, but the new page's `de` / `fr` /
  `da` baselines must exist in the baseline registries first.
- The **chip-swap (ADR 0027) surface** is wanted on the hard-coded routes:
  that is a *new* display lane.
- A **fifth language** joins the seeded baselines: D2's backfill is generic
  over the baseline arrays, but the skip path must be re-confirmed for the
  new code.

---

## ADR 0162 — Analytics history (M34) (moved detail)

*Source: `docs/adr/0162-analytics-history.md`.*

### The "derived, not captured" distinction (M34·1)

M33 (ADR 0156) added a **capture lane** (a daily `StorageMetricsCaptureTick`
+ a `StorageMetricsSample` doc + a `StorageHistoryDocTypes` surface + a
retention tick) because the M24 storage metrics are **point-in-time and
non-derivable** — nothing was stored that would let you reconstruct a past
day. **M13 is the opposite**: it already stores a `UsageEvent` row **per
recognized request** with an `At` timestamp (C-M13·2), and already prunes
them past a **365-day** platform constant (the M13 `UsagePurgeService`,
D5). M34's over-time half is therefore **purely derived** — a new read that
`GROUP`s the existing `UsageEvent` rows by UTC day. **M34 adds no new doc,
no new capture tick, no new retention constant, and no new doc-type
surface.**

### D2 — The two new DTOs (the 2-member M34·1 ceilings)

The `UsageDailyTotal` POCO (namespace `Kumunita.Core.Usage`) carries
**exactly** 2 members: `Date` (DateTimeOffset, **UTC**) + `Total` (int —
the count of `UsageEvent` rows whose `At` falls on that UTC day). The
`UsageHistoryResult` record carries **exactly** 2 members: `WindowDays`
(int) + `Points` (`IReadOnlyList<UsageDailyTotal>` — **ascending** by
`Date`, **only the days present** in the window). **Not** an
`IAuditableResource`; **not** a Marten document.

### Named deferrals (each a future lane, own ADR)

- **per-surface over-time chart** (the (day, surface) breakdown re-rendered
  as a chart — M34 v1 renders the **total per day**).
- **CSV export of the history** (M34 v1 has **no** export lane for the
  history).
- **capacity / growth projection** (a linear-regression "days to X"
  estimate).
- **resident-facing history** (M34 is `GlobalAdmin`-gated — no resident
  surface).
- **multi-week / month aggregation** (M34 v1 is daily granularity).
- **the six-member close flip** until the milestone *ships* (U08 owns it —
  M34·12).

---

## ADR 0167 — M37: Personal notes (moved detail)

*Source: `docs/adr/0167-personal-notes.md`.*

### D6 — The folder tree (the bounded-depth shape)

Each resident has their own tree of `NoteFolder` rows, keyed on `OwnerId`
+ a bounded `ParentId` (depth ≤ 2: root → child → grandchild). A folder's
`Name` is unique within the owner's tree (the `(OwnerId, ParentId, Name)`
unique index is the witness). A note's `FolderId` is the **owner's** folder
id. A folder's deletion **moves** its notes to the root (`FolderId =
null`) — the note body is never lost.

### D9 — The close flip (the six-member close flip, U06)

M37 is the **last** milestone on the roadmap (no `M38` to promote). The M37
close (U06) flips the roadmap parity **together**: `Milestones.cs` M37
`StatusNext` → `StatusDone` (and **no** `M38` row); `MilestonesTests.cs`
re-pinned to the **"last milestone, no successor"** pin; the README
**Roadmap** (M37 → done) + the M37 **Status** line; `docs/STATUS.md`;
`docs/ARCHITECTURE.md`'s value-chain table gains the **M37 row**; **and**
`src/Kumunita.Web/WhatsNew.cs` gains the **M37 entry** (the **required
sixth member** — ADR 0110). All six ship **in the same unit** (the M36
close-flip precedent). *Forbids:* reordering M37; adding an `M38` row;
editing a shipped milestone's text; breaking the `MilestonesTests` pins;
flipping one surface and not the others; **omitting the `WhatsNew.cs`
entry**.

### Consequences — Neutral / cost (the full list)

- **The named trade — the note body's permanence vs. the dangling-target
  leak surface**: the row (and its body) survives its target's death
  instead of being silently removed; the invariant that keeps that safe is
  **C-M37·5** — the closed `pn.list.degraded` label with no title, link, or
  id, + the author's own body.
- **The list resolution is one extra read per row**, bounded by the
  resident's own note count; each row resolves through the owning
  surface's **existing** read seam (D7 — no new branch).
- **The closed `TargetKind` set is deliberately nine-wide, not open**
  (D1): adding a tenth kind is a **drift event** (the design doc's
  §drift-guard).
- **The folder tree is bounded in depth (≤ 2)** (D6): the "nested" shape,
  not an arbitrary tree.

### Follow-on lanes (each its own ADR)

the "nudge the author on note" notification lane; note **sharing** /
per-surface "top noted" stats; note **search**; the **platform-wide tag
vocabulary** (the M26 `Tag` doc lane); **folder drag-and-drop
reordering**; **note export**.

### Amends (the frozen-surface ledger)

Additive on **0118** (the M17 `Bookmark` personal-by-id precedent — a
pointer generalized to a body + a folder tree + personal tags) + **0105**
(the participant-by-id shape — the *primary* precedent for the zero-audit
personal read + the non-owner non-leaky 404) + **0004 §B.1** (the
`*DocTypes` parallel-surface shape) + **0006-C3** (the audit-always-on pin
— the **only** audited call in M37 is the D3 write-lane check) + **0001-B**
(the author's choice is absolute — a non-owner gets a non-leaky 404 and no
audit row) + **0015** (the `kw-l` registry — the closed `pn.*` key block ×
en/de/fr/da).

## ADR 0119 — M18: Recurring events (moved detail)

From `docs/adr/0119-recurring-events.md` (the M18 ADR body is condensed; the
Status/Context/Decision/Consequences record stays there with pointers here).

**D2 — Closed rule shape (full).** `EventRecurrenceRule` is a closed struct:
`Recurrence` (a `Recurrence` enum — `Daily` / `Weekly` / `Monthly` /
`Yearly`), `Interval: int` (≥ 1, default 1), `Count: int?` (optional; the
total number of occurrences including the head), `Ends: DateTimeOffset?`
(optional; the last occurrence's `Start` must be ≤ `Ends`). `Count` and
`Ends` are mutually exclusive in intent (the author picks one in the
composer); if both are set, `Count` wins (the composer disables one when
the other is set — D7). Forbids: a unit reaching for `BYDAY` /
`BYMONTHDAY` / `BYSETPOS` / `EXRULE` or a general RFC 5545 `RRULE` parser.

**D4 — Cascade on head edit (full).** Editing the head (via the existing
`IEventService.UpdateAsync`) cascades the text / `Location` / `Capacity` /
`Color` / `Audience` / `ReminderEnabled` / `LanguageCode` fields to all
non-deleted occurrences (head included) in a single transaction. If the
rule itself (`Recurrence` / `Interval` / `Count` / `Ends`) or the head's
`Start` / `End` changes, the unit re-materializes the series: soft-delete
all existing non-head occurrences, then expand the new rule from the new
head `Start` and insert the new set (the head row itself is updated in
place — its `Id` is stable). Forbids: a unit implementing a per-occurrence
override surface, a unit that leaves stale occurrences after a rule change,
or a unit that reassigns `Id`s on re-materialization.

**D5 — Skip / undelete lanes (full).** A new author-only pair of seams on
`IEventService` — `SkipOccurrenceAsync(eventId, actorId, ct)` (soft-delete
one occurrence: `IsDeleted = true`; the head's `RecurrenceRule` is
untouched, siblings untouched) and `UndeleteOccurrenceAsync(eventId,
actorId, ct)` (un-soft-delete: `IsDeleted = false`). Both ride the frozen
`EventToAuditableResource` + `IAuthorizationService` seams with the
existing `event.edit`-shaped standing (the ADR 0014 / 0016 / 0017
author-only edit precedent). A non-author actor gets a 404 (the frozen
seam's "absent" shape — no leak). Forbids: a unit adding a new
`AccessAction`, a unit that lets a non-author skip, or a unit that
hard-deletes.

**D6 — ICS unchanged (full).** The `IcsWriter` (M12) gains no `RRULE`
emission. Because occurrences are materialized concrete rows (D1), the
per-event file (`GET /events/{id}.ics`) and the subscription feed
(`GET /events.ics`) each emit one `VEVENT` per materialized occurrence row
in scope — exactly as they already do for any concrete event. The M12 ADR's
"no `RRULE`" pin is superseded in scope (M18 is the `RRULE` home) but the
shape it pinned (per-occurrence `VEVENT`, no rule line) is what M18
implements. Forbids: a unit reaching for an `RRULE` line in the ICS
output, or a unit that special-cases the head row in the ICS feed.

**D7 — Composer + detail-page affordances (full).** The event composer
(the `CreateAsync` / `UpdateAsync` form) gains a recurrence picker: a
`Recurrence` select (None / Daily / Weekly / Monthly / Yearly), an
`Interval` number input (≥ 1, default 1), and a radio for "Ends after"
(`Count` number) vs. "Ends on" (`Ends` date). The event detail page gains
a series chip when the row is part of a series (`RecurrenceHeadId != null`
or the head row with `RecurrenceRule != null`): "Repeats {daily|weekly|
monthly|yearly}, every {N} {day|week|month|year}" + (if `Count` set)
"{Count} occurrences" + (if `Ends` set) "until {date}". For an author, the
detail page gains two buttons when the row is a non-head occurrence: "Skip
this occurrence" (`SkipOccurrenceAsync`) and — when `IsDeleted` — "Restore
this occurrence" (`UndeleteOccurrenceAsync`). Forbids: a unit reaching for
a series-level "edit all" affordance, a per-occurrence override editor, or
a "delete entire series" button (a head edit that changes the rule
re-materializes — D4 — but there is no separate "delete series" button).

**D9 — `kw-l` key list (14 keys × en/de/fr/da).** All under `events.*`
(the M4 events namespace — matching the existing `events.created` /
`events.edited` keys; no new namespace): `events.recurrence.none` /
`.daily` / `.weekly` / `.monthly` / `.yearly` (5 — the picker labels),
`events.recurrence.interval` (1 — the "every N" label),
`events.recurrence.ends_after` / `.ends_on` (2 — the radio labels),
`events.recurrence.count` (1 — the "occurrences" noun),
`events.recurrence.until` (1 — the "until {date}" prefix),
`events.series.repeats` (1 — the detail-page series chip prefix),
`events.series.skip` / `.restore` (2 — the author's skip / undelete
buttons), `events.series.part_of` (1 — the "Part of a series" chip on
non-head rows). **14 keys × 4 languages = 56 strings**, added to all four
of the closed dictionaries in
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — `EnValues` (the
`en` floor, the canonical source text), `DeValues`, `FrValues`, `DaValues`
(the ADR 0005 / 0015 `en`-floor / per-key shape: the seeder materializes
each key's `en` row on first boot; the non-`en` rows are the localized
text). Forbids: a unit reaching for a new `kw-l` namespace, an English-
only key, or a key that leaks a non-localized "RRULE" / "iCal" term into a
user-visible string.

**D10 — Reminders unchanged (full).** The `EventReminderService` (M4 §6.4)
already fires per concrete `Event` row in the window (`now < Start ≤ now +
WindowHours`). Because M18 materializes occurrences as concrete rows (D1),
the reminder job works unchanged — each occurrence is its own candidate,
its own `remind:{eventId}:{userId}` idempotency key, its own email. No
`EventReminderService` change in M18. Forbids: a unit "fixing" the
reminder service to be series-aware, a unit that dedups reminders across a
series, or a unit that changes the `remind:{eventId}:{userId}` key shape.

**D11 — Deferred lanes (each gets its own ADR).** Per-occurrence overrides
(change one occurrence's fields without touching the series); group-event
recurrence (the M7 `CreateGroupEventAsync` surface); `EXRULE` / exception
rules; richer RFC 5545 `RRULE` (`BYDAY` / `BYMONTHDAY` / `BYSETPOS`);
cross-month / cross-year edge cases (the "31st of each month" / "last
Friday" shapes).

**Consequences — neutral / cost (full).** The named trade — write-time
expansion for zero read-time complexity: M18 trades write-time work (expand
the rule into N rows at create/edit) for zero read-time complexity (every
read seam, RSVP, reminder, ICS, calendar, search, bookmark surface keeps
working unchanged). The cost is that a long series (e.g. "weekly for 52
weeks") materializes 52 rows at write time, and a rule change
re-materializes them (the D4 cascade's branch). The alternative (a
`RecurrenceRule` on the head + read-time expansion) would have touched
every read seam, the RSVP model, the reminder job, the ICS writer, the
calendar, the search, and the bookmarks — a far larger surface for the
same user-visible result. M18 takes the write-time cost; the read surface
stays frozen. The closed `Recurrence` set is deliberately four-wide, not
open (D2): a new recurrence type joins the world with one enum value + one
expansion branch; adding a `BYDAY` / `BYMONTHDAY` / `BYSETPOS` / `EXRULE`
richness is a drift event. The cross-month / cross-year simplification is
intentional: a `Monthly` rule on the 31st clamps to the target month's
last day; a `Yearly` rule on Feb 29 clamps to Feb 28 in non-leap years.

**Consequences — follow-on lanes (each its own ADR — the design doc's
§deferred).** Per-occurrence overrides; group-event recurrence (the M7
`CreateGroupEventAsync` surface); `EXRULE` / exception rules; richer RFC
5545 `RRULE` (`BYDAY` / `BYMONTHDAY` / `BYSETPOS`); cross-month /
cross-year edge cases (the "31st of each month" / "last Friday" shapes).

## ADR 0169 — First day of week (moved detail)

From `docs/adr/0169-first-day-of-week.md` (the ADR body is condensed; the
Status/Context/Decision/Consequences record stays there with pointers here).

**D1 — the preference: additive `Profile.FirstDayOfWeek` (full).** `Profile`
(the existing `UserInfo` context doc) gains one field: `public int?
FirstDayOfWeek { get; set; }` (0 = Sunday … 6 = Saturday, the .NET
`DayOfWeek` index; `null` = the platform default, Monday). Additive on the
existing doc — delta-detected, idempotent, zero migration (ADR 0004 §B.1).
`null` means "use the default" — the exact `PageSize` shape. Marten's
delta-detector adds the column on the next
`ApplyAllConfiguredChangesToDatabaseAsync`; existing rows read back as
`null`. No EF migration (not an Identity table — ADR 0004 §B).

**D2 — the resolver: pure `WeekAnchor` (full code).** A small, testable,
dependency-free type in `Kumunita.Core.Query` shared by the view, the Quiet
picker, the Guardian picker, and the calendar:

```csharp
public static class WeekAnchor
{
    public const int Default = 1;   // Monday
    public static int Resolve(int? preferred) =>
        preferred is >= 0 and <= 6 ? preferred.Value : Default;
    public static int[] OrderDays(int firstDay)
    {
        var days = new int[7];
        for (var i = 0; i < 7; i++) days[i] = (firstDay + i) % 7;
        return days;
    }
}
```

`Resolve` is total (any input, including `null` and out-of-range, yields a
valid first-day). `OrderDays` is a pure permutation of `0`–`6` — the same
set, a different order, so the submitted value of any picker is unchanged by
the reorder. `Default = 1` is Monday, the audience's convention.

**D3 — the write lane: `SetProfileFirstDayOfWeekAsync` (full).**
`IUserInfoService` gains one seam mirroring `SetProfilePageSizeAsync`
exactly: `Task SetProfileFirstDayOfWeekAsync(string subjectId, int?
firstDayOfWeek, string actorBy)` (pass the default Monday, 1, to clear the
preference back to the platform default). Owner-scope, fail-closed
`KeyNotFoundException` on a missing profile, **zero** audit rows, **zero**
new authorization surface.

**D4 — the UI (full).** Set on `/settings/dateformat` (the natural home
beside the date/time format it orders). The view gains a "First day of week"
radio group rendered in the resident's own preferred order:

```html
<fieldset>
  <legend>First day of week</legend>
  @foreach (var d in Kumunita.Core.Query.WeekAnchor.OrderDays(Model.FirstDayOfWeek))
  {
    <label>
      <input type="radio" name="firstDayOfWeek" value="@d"
             @(Model.FirstDayOfWeek == d ? "checked" : "") />
      @DayName(d)   @* localized name via the effective language's DateTimeFormatInfo *@
    </label>
  }
</fieldset>
```

`LocaleController.BuildModel()` populates
`FirstDayOfWeek = WeekAnchor.Resolve(profile?.FirstDayOfWeek)`. `SaveDateFormat`
posts `firstDayOfWeek` and writes only when a valid `0`–`6` value is posted.
The **reset** form posts `clear=1` **without** a day value, so the preference
is left untouched — the two are independent preferences on independent fields.

**D5 — the day pickers: reordered, named (full).** Both day pickers (Quiet-
hours `daysOfWeek`, Guardian time-limit days) now render the **localized day
name** (via the effective language's `DateTimeFormatInfo.GetDayName` /
`CultureInfo.CurrentCulture.DateTimeFormat.GetDayName`) and **order** their
options via `WeekAnchor.OrderDays(firstDay)` — the resident themselves for
`/settings/quiet`, and the **child's** preference for the Guardian time-limit
page (`ViewData["ChildFirstDayOfWeek"] = WeekAnchor.Resolve(childProfile?.
FirstDayOfWeek)`). The value submitted is unchanged (the same `int` set,
reordered) — the existing write lanes, validation, and stored data are all
untouched.

**D6 — the calendar: re-anchored, default byte-identical (full).**
`EventController.Calendar` anchors its week and month grids to the *viewer's*
first day. Week view: `anchorDate.AddDays(-WeekOffset(anchorDate))` where
`WeekOffset(d) = ((int)d.DayOfWeek - firstDay + 7) % 7`. Month view: the grid
spans the weeks covering the anchor month. **The default (Monday) path is
byte-identical to the pre-existing formula** — the pre-existing month-grid
code produced a *ragged* grid (e.g. 34 days for Sep-2026, not a clean 35),
and the pinned test `Calendar_ViewMonth_WindowIsAnchorCalendarMonth` asserts
`WindowDays[0] == gridMonday` + the exact count. To keep that invariant
passing, the month case keeps the **original formula verbatim** when
`firstDay` is the Monday default, and uses the clean full-weeks formula only
for a non-Monday preference:

```csharp
if (firstDay == WeekAnchor.Default)
{
    // original formula, verbatim — ragged-month invariant + pinned test preserved
    gridStart = monthStart.AddDays(-(((int)monthStart.DayOfWeek + 6) % 7));
    gridEndExclusive = monthEndInclusive.AddDays(1 + ((6 - (int)monthEndInclusive.DayOfWeek) % 7));
}
else
{
    gridStart = monthStart.AddDays(-WeekOffset(monthStart));
    // …clean full-weeks formula, firstDay-anchored
}
```

**Consequences — the close-flip entries (moved).** `WhatsNew.cs` 0.65.0 (the
required close-flip member, ADR 0110): "First day of week — a per-resident
display preference (default Monday) that orders the Quiet-hours and Guardian
time-limit day pickers and anchors the events calendar's week/month grid;
additive `Profile` field + `WeekAnchor` resolver + owner-scope write lane;
zero migration; zero new authorization surface" (2026-10-15). **A named
lane, not a milestone:** the M-letter milestone ordering (`Milestones.cs`,
README Roadmap, `MilestonesTests.cs`) is **byte-identical** (the ADR 0013
`GP` / ADR 0011 `M4-adjacent` precedent); the close flips the doc-sync pair
(README Roadmap + `STATUS.md` + `ARCHITECTURE.md`) + the `WhatsNew.cs` entry.

## ADR 0067 — M5: Projects (to-dos + Kanban boards) (moved detail)

From `docs/adr/0067-m5-projects-todos-and-kanban.md` (the ADR body is
condensed; the Status/Context/Decision/Consequences record stays there with
pointers here).

**The four documents' full field sets (moved).** `TodoItem`: `Id`; `Title`
(non-empty — the card label + adapter `Name`); `Body?` (optional Markdown —
the one `MarkdownRenderer`, ADR 0025); `ComponentId?` (a feed filter, never a
gate — C-M3·2); `AuthorId` (the standing owner); `AssigneeId?` (a `SubjectId`
— display + standing, **never** an access gate); `Status?` (a **nullable
string** — the state label; no status enum / registry / seeded vocabulary);
`ParentId?` (the **sole** hierarchy mechanism — a subtask is a full to-do);
`Audience?` (the **exact** post `Audience`, ADR 0001-B / 0036 — `null` =
public, the frozen `Decide()` branch 5); `IsDeleted` (ADR 0024);
`LanguageCode` (ADR 0018); `TagIds` (ADR 0044); `ImageIds` (ADR 0025);
`AttachmentIds` (ADR 0034); `Created` / `Modified?`. **No** `BoardId` /
`LaneId` / `Order` (a placement is a separate `BoardItemPlacement` record)
and **no** `IsDraft`. `KanbanBoard`: `Id`; `Title`; `Description?` (Markdown,
ADR 0025 shape); `ComponentId?`; `AuthorId`; `Audience?`; `IsDeleted`;
`LanguageCode`; `Created` / `Modified?`; **no** `IsDraft`. `KanbanLane`:
`Id`; `BoardId`; `Title`; `Status?` (the status a lane **imparts** on a to-do
moved into it); `MaxItems?` (nullable int — the lane's capacity, **advisory**,
refused not dropped); `Order` (0-based column order); `Created` / `Modified?`;
**no** `Audience` (a lane's visibility is the board's).
`BoardItemPlacement`: `Id`; `TodoItemId`; `BoardId`; `LaneId`; `Order`
(0-based card order); `Created` / `Modified?`; **no** `Audience` (the
placement's visibility is the to-do's **and** the board's; it is **not**
itself an auditable resource).

**`ProjectDocTypes` (moved).** A new document surface (the `EventDocTypes`
pattern verbatim): `TodoItem` (conventional `Id` + a `(ComponentId, Created)`
feed-ordering index + a `ParentId` index); `KanbanBoard` (conventional `Id` +
a `(ComponentId, Created)` feed-ordering index); `KanbanLane` (conventional
`Id` + a **unique** `(BoardId, Order)` index — the business-key convention,
the `EventRsvp` shape); `BoardItemPlacement` (conventional `Id` + a **unique**
`(BoardId, LaneId, Order)` index + a **unique** `(TodoItemId, BoardId)` index
— a to-do appears on a board at most once). Zero migrations for existing
surfaces.

**The standing matrix (full table, moved).**

| Action | Standing | `AccessVia` |
|---|---|---|
| **Create** (to-do / board) | any signed-in resident (becomes the author) | `Owner` |
| **To-do mutations** (edit / assign / add-subtask / delete / reorder / move / copy) | **creator ∪ assignee ∪ GlobalAdmin** | `Owner` / `Admin` |
| **Board / lane mutations** (lane CRUD, board delete) | **creator ∪ GlobalAdmin** (the lane is not its own standing surface) | `Owner` / `Admin` |

`CheckCreateStanding` / `CheckEditStanding`-shaped helpers throw
`UnauthorizedAccessException` (403) / `KeyNotFoundException` (404) exactly
like `AnnouncementService`. The `actorRoles` parameter carries the principal's
real role set. **`AssigneeId` is display + standing, never an access gate.**
Every audited write lane stores its `AccessAudit` row in the caller's session
(C3), `TargetKind = "todo"` / `"board"`, `Action` `todo.create` /
`todo.update` / `todo.assign` / `todo.add_subtask` / `todo.delete` /
`todo.move_within_lane` / `todo.move_to_lane` / `todo.move_to_board` /
`todo.copy_to_board` / `board.create` / `board.update_lane` / `board.delete`.

**The `IProjectService` seam (full list, moved).** Read lanes
(`ListTodosAsync` / `GetTodoAsync` + subtasks / `ListBoardsAsync` /
`GetBoardAsync` + lanes + per-lane cards); write lanes (`CreateTodoAsync` /
`UpdateTodoAsync` / `AssignTodoAsync` / `AddSubtaskAsync` / `DeleteTodoAsync`
/ `CreateBoardAsync` / `UpdateLaneAsync` / `DeleteBoardAsync`); placement +
reorder lanes (`MoveTodoWithinLaneAsync` / `MoveTodoToAdjacentLaneAsync` /
`MoveTodoToBoardAsync` / `CopyTodoToBoardAsync`). **Copy-to duplicates** (a
new `TodoItem` — the copy's `AuthorId` is the actor — + a placement on the
target board's first lane; the original is untouched); **move-to relocates**
(the placement is removed from the source board and added to the target
board's first lane). **No translation lane in M5's own scope** (a follow-on
lane with its own ADR — the ADR 0022 / 0029 / 0048 / 0059 shape).

**The board state machine (full, moved).** Lane-status auto-update — moving a
to-do into a lane whose `Status` is non-null sets `TodoItem.Status` to that
lane's status **in the same transaction** as the placement move (audited —
C3); a lane with null `Status` leaves the to-do's status unchanged; the status
icon is a client-side display mapping, never persisted. Lane limits are
advisory backstops, refused not dropped — a move / copy **into** a lane at its
`MaxItems` limit is **refused** (`InvalidOperationException` with the lane's
`Title`; **nothing is written**); a reorder within a lane and a move *out* of
a lane never trip the source limit. Hierarchy is `ParentId` only — a subtask
is a full to-do; **delete cascades** to the descendant subtree; a `ParentId`
that would create a cycle is refused.

**The Web surface (full, moved).** `ProjectsController`
(`Kumunita.Web.Controllers`): the to-do routes (`/projects/todos` feed —
`CanSeeAsync(Read)`-filtered so a restricted to-do's *existence* does not
leak; `/projects/todos/new` compose; `/projects/todos/{id}` detail — 404 on
absent / 403 on denied) + the to-do action endpoints (edit / assign /
add-subtask / delete); the board routes (`/projects/boards` feed;
`/projects/boards/new` compose; `/projects/boards/{id}` detail — lanes as
columns, cards with the status icon + assignee avatar + title + the dropdown
menu) + the lane CRUD + placement endpoints (reorder / move left-right /
move-to / copy-to). The nav entry is one line (the `Community` nav pattern).
The `Post` / `Announcement` / `Page` / `Event` surfaces stay **untouched**.
The board module is plain `client/lib` TS, tsc-only, zero dependencies —
`client/lib/projects-board.ts`: every action is an **explicit POST**,
CSRF-aware, then a **full re-render** — no optimistic local reordering, no
HTML5 drag events (keyboard / menu driven — the ADR 0031 "explicit,
server-authoritative" posture).

**The localization + test + drift-guard pins (moved).** The new `projects.*`
`kw-l` key block (design doc §2.6 — 19 keys) is present in all four seeded
languages (en/de/fr/da). The **24 pinned seam test names** (design doc §2.7 /
§2.8): 16 in `tests/Kumunita.Core.Tests/ProjectServiceTests.cs` (F1–F10, run
over `PostgresFixture`) + 8 appended to
`tests/Kumunita.Web.Tests/ProjectsControllerTests.cs` (NSubstitute, no
Postgres). The **three-test acceptance gate** (design doc §2.9, gate unit
U13): **closed loop** (create → list + board card + one aggregate row),
**handoff** (assignee sees it + has standing; the non-assignee's view /
standing exclude it), **part-vs-whole** (the 24 names pass together with the
inherited M1–M4 anchors). The **drift-guard** (design doc §2.10): the four
POCOs' field sets (including the no-`BoardId`/`LaneId`/`Order` / no-`IsDraft`
pins), the two adapters (`TargetKind = "todo"` / `"board"` — the **exact**
strings), the `IProjectService` full surface, the DTOs, the `ProjectDocTypes`
surface, the standing matrix, the `kw-l` keys, the 16 Core + 8 Web test names,
and the three-test gate — all frozen pins.

**The canonical-names decision (moved).** The data model sketch
(`Project` / `ProjectTask` / `ProjectMember`) is the pre-decision shape; the
settled names are **`TodoItem`** / **`KanbanBoard`** / **`KanbanLane`** /
**`BoardItemPlacement`** — "project" is not a distinct document in M5 (a to-do
is the unit, a board is the container). The M5 close unit (U13) rewrites the
[data model — Projects](../architecture/data-model.md) block + the
[solution layout](../architecture/solution-layout.md) tree's `Projects/` line
to these names.

## ADR 0039 — Pages: a hierarchical, audience-restricted, translatable knowledge tree (moved detail)

From `docs/adr/0039-pages-hierarchy-audience-translations.md` (the ADR body
is condensed; the Status/Context/Decision/Consequences record stays there
with pointers here).

**The `Page` / `PageTranslation` field sets (moved).** `Page`: `Id`,
`ParentId?` (hierarchy — `null` = a root), `Slug` (unique per parent),
`Title`, `Body` (Markdown, authored-in language), **`Audience?`** (the
**existing** `Kumunita.Core.Authorization.Audience` — *not* a new scope;
`null` = public, the frozen `Decide()` branch 5), `AuthorId`,
`ComponentId?` (moderator scoping), `LanguageCode` (ADR 0018 authored-in
tag), `MountPoint?` (a nullable string tag — where a page appears in the
UI), `Created` / `Modified?`, `IsDraft` (ADR 0037 idiom), `ImageIds` (RC
ADR 0025 idiom), `AttachmentIds` (ATT ADR 0034 idiom). `PageTranslation`:
`Id`, `PageId`, `LanguageCode`, `Title?`, `Body`, `AuthorId`, `Created` —
`(PageId, LanguageCode)` unique index (the `PostTranslation` business-key
convention; an explicit short index name if the auto-derived name exceeds
Postgres's 64-char `NAMEDATALEN` limit, the `ann_tr_uidx_ann_lang`
precedent).

**The hierarchy mechanics (moved).** A page's canonical path is the chain
of ancestor `Slug`s: `Page("/a/b/c")` ⇒ `a` (root) → `b` → `c`. The tree is
a **forest** (any number of roots). A page is either a *folder* (has
children, `Body` may be empty) or a *leaf* (has a `Body`), or both.
"Folder" is not a type, it is "has ≥1 child". The `(ParentId, Slug)` unique
index enforces one page per slug per parent (the `GroupMembership` business-
key convention). A **cycle-guard + depth-cap** (e.g. 8) is validated in the
write lane (the `PageService`), not a schema constraint (Marten has no FK).
**Renaming a page** rewrites the *derived* paths of its subtree (paths are
not stored, so a `Slug` change is a single-column write). **References from
posts break by path, not by id** — the same property the RC content-image
lane has.

**The standing matrix (full table, moved).** Enforced server-side in the
`PageService` (the `AnnouncementService.CreateAsync` C3 pattern):

| Action | Standing | `AccessVia` |
|---|---|---|
| **Create** a page | GlobalAdmin ∪ (community Moderator, `ComponentId` = a community they moderate) | `Admin` / `Moderator` |
| **Edit** a page's body / audience / hierarchy | its `AuthorId` ∪ GlobalAdmin ∪ (a Moderator of its `ComponentId`) | `Owner` / `Admin` / `Moderator` |
| **Add a translation** | GlobalAdmin ∪ Translator ∪ (community Moderator, community-scoped) | `Admin` / `Moderator` |
| **Publish** a draft | its `AuthorId` only (ADR 0037 pin) | `Owner` |
| **Move / rename / delete** | GlobalAdmin ∪ (a Moderator of its `ComponentId`) — *not* a plain author | `Admin` / `Moderator` |

**Every write lane** stores its `AccessAudit` row in the caller's session
(invariant C3, ADR 0006), `TargetKind = "page"`, `Action` `page.create` /
`page.update` / `page.translation.add` / `page.publish` / `page.delete` /
`page.move`.

**The translation standing + display (moved).** A `PageTranslation` row (the
ADR 0022/0026/0029 shape) carries a user-added translation. **Standing** =
GlobalAdmin ∪ Translator (ADR 0021/0022) **plus the community-Moderator lane
for a community-scoped page** (`ComponentId` set and equal to a community the
actor moderates — the ADR 0029 announcement lane, carried over). **Add-only**
(ADR 0022); a wrong translation is corrected by the same standing adding the
right one, the unique index preventing a duplicate. **Display** = the ADR 0027
chip-swap, reused verbatim: the authored-in variant is the default visible
one, a chip per `PageTranslation`, click to swap.

**The Web surface (moved).** `PageController` (`Kumunita.Web.Controllers`):
`GET /pages` (the tree browse, `CanSeeAsync(Read)`-filtered), `GET
/pages/{**path**}` (the post view — title + rendered body via the *one*
`MarkdownRenderer` + the ADR 0027 chip-swap, `CanAsync(Read)`-gated, 404 on
absent, 403 on denied), `GET /pages/new` + `POST /pages/new` (compose — title,
body, parent picker, the **`AudienceEditorModel`** verbatim, the ADR 0018
language picker — WYSIWYG via `bindRichEditor`), `GET /pages/{id}/edit` +
`POST /pages/{id}/edit` (the edit lane), `POST /pages/{id}/publish` /
`delete` / `move` (the standing-gated actions). **`StaticPagesController`
becomes a thin reader over the tree** — `/terms`, `/help`, `/about` resolve
to the seeded page at that path (or the existing `/about` product-story
fallback when truly absent); the three hard-coded routes are **kept**
(backward-compatible) but **read from the tree**, so there is one store, not
two. **Mount points** — the layout partials resolve "the page mounted at
`X`" and render / `<a href>` it. **The editor** reuses
`Views/Languages/PreviewPage.cshtml`'s existing `bindRichEditor` + RC content-
image + ATT attachment wiring.

**The data migration (absorb) — the 4-step sequence (moved).** (1) create the
`Page` + `PageTranslation` tables (new doc types, ADR 0004 §B.1); (2) **seed
the three default pages** (`about`, `terms`, `help`) as `Page` docs carrying
the *current* `LocalizedPage` `en` bodies/titles, `Audience = null` (public),
`MountPoint` set (`about` → `footer/community`), `LanguageCode = "en"` —
**idempotent**; (3) **keep `LocalizedPage` read-only + non-destructive for
one release** (the old `GetPageAsync` / `UpsertPageAsync` seam still works, so
the old `StaticPagesController` fallback is *available* if the new route
misbehaves); then (4) **delete the `LocalizedPage` surface** in U07, once the
new route is green. **No seed reset, no schema-file change** for the new
*fields*.

**Consequences — full (moved).** The "mount point" concept is new and small
(one nullable string column + one resolver), and it is the mechanism that
makes the "about is in `footer/community`" requirement expressible without a
special case. The only irreversible step is the absorb migration, and it is
sequenced *last* (U07). The default-audience now matches posts (amended
2026-09-17): posts default community-visible (ADR 0036); **pages now do too**
(composer seeds `IsPublic = false`, `Audience.Community = true`, the first
reachable `ComponentId`); ~~Previously "pages default public
(`Audience = null`)"~~ — reversed to keep pages consistent with posts. A page
author who wants a **public** page sets `IsPublic = true` (audience `null`),
and one who wants a specific grant set picks that community / users / groups.
The standing matrix is the existing role matrix re-checked server-side — no
new role, no new `AccessVia`, no new `AccessAction`; a community Moderator's
standing on a page is the *same* `moderator:{CommunityId}` claim the
announcement translation lane (ADR 0029) already uses. The `LocalizedPage`
retirement is the last unit and the only destructive step; a fresh instance's
`/about` / `/terms` / `/help` are byte-identical to today (the seeded pages
carry the same `en` bodies).

**The `PG` lane pin (moved).** The lane is `PG` (a named lane, the `ML` / `GP`
/ `RC` / `RE` convention — a short ID, *not* a renumber). M4 / M5 / M6 stay
Events / Projects / Portability; no roadmap letter moves. The `PG` entry is
added to `Milestones.All` **after `RE`** (a `StatusPlanned` entry — the
"single in-progress milestone" pin (M4) is unchanged; the test's `Ids`
ordered list gains `"PG"` in the same commit). The README Roadmap gains the
`PG` row in the same commit.

## ADR 0088 — Projects / to-dos / Kanban-board user-added translations (moved detail)

From `docs/adr/0088-projects-todos-boards-translations.md` (the ADR body is
condensed; the Status/Context/Decision/Consequences record stays there with
pointers here).

**D1 — The three translation rows (full, moved).** Three POCOs in
`Kumunita.Core.Projects`, each mirroring `EventTranslation` (ADR 0059) but
with the **Projects body-optional** adjustment: `TodoTranslation` (parent a
`TodoItem` / `TodoItemId`), `BoardTranslation` (parent a `KanbanBoard` /
`BoardId`), `ProjectTranslation` (parent a `Project` / `ProjectId`). Each
row: `Id` (surrogate key), `<Parent>Id`, `LanguageCode` (the **target**
language), `Title?` (a translation may re-title the parent or not — blank →
`null`; the parent's own title is the display fallback), `Body?` (the
translated body — the parent's `Body` for a to-do, its `Description` for a
board / project. **Optional here — unlike `EventTranslation`, whose `Body` is
required — because the M5 surfaces are title-usable**; the ADR 0079 / ADR 0070
title-only precedent. At least one of `Title` / `Body` must be non-blank at
write time — enforced server-side; the DB unique index only pins (parent,
language)), `AuthorId` (the last actor to add / edit the row), `Created` (the
last add / edit stamp). Registered on the **`ProjectDocTypes`** document
surface with a **`(ParentId, LanguageCode)` unique index** per POCO,
mirroring the `PostTranslation` / `AnnouncementTranslation` /
`EventTranslation` business-key convention. The auto-derived index names stay
under Postgres's 64-char `NAMEDATALEN` limit. **Zero migration for existing
rows** (ADR 0004 §B.1 delta-detect).

**D2 — Twelve `IProjectService` seams (full table, moved).** `IProjectService`
gains exactly **12 methods** (3 entities × 4 seams), mirroring the add / read
/ update / remove shape ADR 0048 / ADR 0059 settled. For `<E>` ∈ {`Todo`,
`Board`, `Project`}: **read** `Get<E>TranslationsAsync(<e>Id)` →
`IReadOnlyList<<E>Translation>` (rows ordered by `LanguageCode`; a **read, not
a decision** — no authorization, no audit; the parent is still read through
the existing `GetTodoAsync` / `GetBoardAsync` / `GetProjectAsync`); **add**
`Add<E>TranslationAsync(<e>Id, languageCode, title?, body?, actorId,
actorRoles, ct)` (stores the row + its `AccessAudit` row;
`UnauthorizedAccessException` on a denied actor, `KeyNotFoundException` for an
unknown parent, `ArgumentException` for a blank (title, body) pair);
**update** `Update<E>TranslationAsync(...)` (edits the `(<e>Id, languageCode)`
row; re-stamps `Created` + `AuthorId`; `KeyNotFoundException` for a missing
row, `ArgumentException` for a blank pair); **remove**
`Remove<E>TranslationAsync(...)` (deletes the row; `KeyNotFoundException` for
a missing row). **Seam shape — the M5/PL convention (ADR 0067 §4), not the M3
one:** like the existing `ProjectService` write lanes (and the M4
`EventService`), these write lanes **self-compose** their own C3 session
(`store.OpenSession(...)`); they carry **no `IDocumentSession` parameter**.
The domain write and the `AccessAudit` row commit atomically in that one
self-composed session (invariant C3).

**D3 — The standing (full, moved).** A **pure role-claim check** (the M5 §3.4
shape), enforced server-side by three new pure helpers
`ProjectService.ResolveTodoTranslationStanding` /
`ResolveBoardTranslationStanding` / `ResolveProjectTranslationStanding` (the
C3 single-source pin). The matrix is the **translation** matrix, *not* the
edit standing — the ADR 0059 author ∪ Translator ∪ GlobalAdmin matrix
**plus the to-do's existing ADR 0067 assignee branch**: **The parent's
creator** (`AuthorId`) — `AccessVia.Owner`; **The to-do's assignee** (to-do
only) — `AccessVia.Admin` (the ADR 0067 assignee branch, the
`TodoAuditViaFor` "least-distortion slot" precedent); **A Translator** (ADR
0021) of **anyone's** row — `AccessVia.Admin`; **A GlobalAdmin** (ADR 0017)
of **anyone's** row — `AccessVia.Admin`; **A plain Member** — **no** standing.
This is *broader than* the existing edit standing (the M5 edit lanes are
creator ∪ assignee (to-do) ∪ GlobalAdmin and **do not** include Translator):
a Translator who cannot *edit* a to-do's title may still *translate* it. No
new `AccessVia` member, no new role. A **pure display helper**
`ProjectService.CanAdd<E>Translation(<parent>, actorId, actorRoles)` returns
the matching resolver's `... is not null`, so the Web renders the add / edit
/ remove controls with the exact matrix the write lanes re-check.

**D4 — Audit (full, moved).** Each write lane stores one `AccessAudit` row in
the self-composed session, committing atomically with the write (C3).
`TargetId = <parent id>`, `Outcome = AccessOutcome.Allow`, and `Via` from the
D3 standing. The existing `TargetKind` constants are reused verbatim
(`TargetKindTodo` = `"todo"`, `TargetKindBoard` = `"board"`,
`TargetKindProject` = `"project"`). The action strings are the ADR 0059
`eventtranslation.*` idiom per entity: `todotranslation.add` / `.update` /
`.remove` (Via `Owner` for creator, `Admin` for assignee / Translator /
GlobalAdmin); `boardtranslation.add` / `.update` / `.remove` (Via `Owner` for
creator, `Admin` for Translator / GlobalAdmin); `projecttranslation.add` /
`.update` / `.remove` (same as board). The **read** lane writes **no** audit
row. **No new `AccessAction`** is introduced.

**D5 — The Web surface (full, moved).** `ProjectsController` gains **nine**
thin POST lanes (3 entities × 3), all `[ValidateAntiForgeryToken]`:
`POST /projects/todos/{id}/translations[/update|/remove]`,
`POST /projects/boards/{id}/translations[/update|/remove]`,
`POST /projects/projects/{id}/translations[/update|/remove]`. Each: an empty
`id` → `NotFound()`; an unauthenticated caller → `ForbidResult`; a blank
`languageCode` or a blank (title, body) pair → a shape error + redirect with
**no** service write; a `KeyNotFoundException` → `NotFound()`; an
`UnauthorizedAccessException` → `ForbidResult`; a success → `TempData["info"]`
+ redirect. The parent `GetAsync` precondition (C-M3·1) re-runs the row's read
decision. **Display:** **Detail** — the detail view models carry the parent's
translations + the enabled-language catalog (`LanguageOption`s with
`HasTranslation`) + `CanTranslate` + `OriginalLanguageCode`; the title + body
are wrapped in the ADR 0027 chip-swap markup (the to-do and board wrap their
title + body in a single `data-td-group`; the project uses two groups —
`"project-title"` / `"project-description"`); the authored-in variant is the
first / default-visible one; the add / edit / remove controls are presented
with the ADR 0082 idiom (the shared `⋮` action dropdown + Bootstrap modal).
**ADR 0049** applies: when the viewer's current language has a translation,
that variant is the default-visible one. **Feed** — per **ADR 0051**, each
feed row shows the title / body in the viewer's current language when a
translation exists, else the authored-in text. **Dependencies:**
`ProjectsController` gains an optional `ITranslationProvider?` constructor
parameter (last position, defaulting to `null`) + a private
`SeedLanguageName(code)` helper + a `SeedLanguagePickerAsync()` mirror.

## ADR 0063 — The events calendar (`EV-CAL`) (moved detail)

From `docs/adr/0063-events-calendar-view.md` (the ADR body is condensed; the
Status/Context/Decision/Consequences record stays there with pointers here).

**D2 — The one seam: `ListInRangeAsync` (full, moved).** Declared directly
after `ListUpcomingAsync`:

```csharp
Task<IReadOnlyList<Event>> ListInRangeAsync(
    DateTimeOffset windowStartUtc,
    DateTimeOffset windowEndUtc,
    string? componentId,
    string actorId,
    CancellationToken ct = default);
```

- **Parameter order + types (locked):** `(windowStartUtc, windowEndUtc,
  componentId, actorId, ct)` — the window bounds lead (the query's primary
  predicate), with the feed's own two args (`componentId`, `actorId`)
  trailing; the bounds are `DateTimeOffset`, matching `Event.Start` /
  `Event.End`. The window replaces `ListUpcomingAsync`'s `page`.
- **Predicate:** `Start >= windowStartUtc && Start < windowEndUtc` —
  inclusive start, exclusive end; an event is in the window on the day it
  *starts* (a multi-day event's chip repetition across columns is a *display*
  concern — D5).
- **Candidate filter + gate (verbatim the `ListUpcomingAsync` body, C-EV·1):**
  `!IsDeleted && !IsDraft`, the optional `componentId` filter (C-M3·2 — a
  filter, never a gate), `OrderBy(Start)`, the **single**
  `CanSeeAsync(Read, …)` over `EventToAuditableResource` (standalone form),
  then the `visibleIds` filter.
- **Cap:** `Take(30)` via a private `const int WindowCap = 30;` on
  `EventService` next to `PageSize`. The **30-day span lives in the
  controller** (`windowEndUtc = windowStartUtc.AddDays(30)`) — the service's
  `Take(30)` is a **backstop**, not a policy (the service stays window-span-
  agnostic).
- **Audit (C-EV·2):** one aggregate `AccessAudit` row per render,
  `TargetKind = "event"`, from the single `CanSeeAsync` call — verbatim the
  feed's audit shape. No other `IEventService` / `EventService` method
  changes.

**D3 — The route + anchor + window (full, moved).** `GET
/events/calendar` with `?from=YYYY-MM-DD` (the month anchor **in the viewer's
effective timezone**, the `EffectiveTimezoneResolver`'s zone, ADR 0019;
default = the zone's today) and `?componentId=…` (the feed's filter reused
verbatim — a filter, never a gate, C-M3·2). Window = `[anchorLocalStartUtc,
anchorLocalStartUtc + 30d)` where `anchorLocalStartUtc` = the anchor date at
**local midnight** in the effective zone, converted to UTC (the server-side
math, in the controller — C-EV·5). Prev/next = the anchor shifted **±1
month**, pre-rendered as plain GET links by the server (C-EV·7 — no client
nav code). Unparseable / missing `from` falls back to the zone's today (a
display fallback, not an error).

**D4 — Overlap is client-side display-only (full, moved).** Two events
overlap iff their `[startUtc, endUtc)` half-open intervals intersect. Computed
in the TS module **over the already-authorized row list** the view ships (each
chip carries `data-start-utc` / `data-end-utc`); the flag drives a CSS class
(a ring / highlight) + the `kw-l` "overlaps" hint (`title` attribute) — the
hint is **registry-localized**: `Calendar.cshtml` resolves the existing
`events.calendar.overlap_hint` key server-side (the `kw-l` provider path) and
ships it as `data-overlap-hint` on `#events-calendar`; the TS module reads it
and falls back to the key's en source text when the attribute is absent (the
2026-09-23 follow-up replaced the U06 hardcoded-English-only hint). It writes
**no** row, calls **no** seam, is **never persisted**, and is **never an
access decision** (C-EV·4).

**D5 — The view shape (full, moved).** `Views/Event/Calendar.cshtml` over
`EventCalendarViewModel`: a **7-column day grid** of the 30 window-days
(Bootstrap shape, mirroring `Index.cshtml`); each event = **one chip per
day-column it touches** (the TS module distributes by local day via
`Intl.DateTimeFormat` + the effective zone id the view ships as a **display
value only** — C-EV·5); a chip = the event title (link to the detail page) +
the `kw-dt`-rendered time; empty days show the date label only. The header
carries the month label, the prev/next/today nav links, the `componentId`
filter form (reused from the feed), the "List" cross-link (C-EV·8), and an
empty state when the window has no visible events.

**D6 — The `kw-l` keys (moved).** Eight new keys under `events.calendar.*` in
**all four** seeded languages (en/de/fr/da) in `KnownTranslationKeys.cs`:
`events.calendar.title`, `.prev`, `.next`, `.today`, `.overlap_hint`, `.empty`,
`.list_view`, `.from` — each with an en floor text. The `events.*` block is
the host; the `tags.*` block is the precedent (ADR 0015).

**D7 — Out of scope (future lanes, moved).** No year view; no event creation
from the calendar; no RSVP from the calendar (the detail page owns it); no
drag-to-reschedule; no iCal export (**M6** owns iCal); no recurring-event
model; no new notifications; no server-side overlap API (overlap stays a D4
display flag). Any of these landing later is a **new named lane**, not an
extension of this one.

**The Web shape (full, moved).** **`EventRow`** gains exactly **two
additive, defaulted** fields: `StartUtc` (`DateTimeOffset`, `= default`) +
`EndUtc` (`DateTimeOffset`, `= default`) — the overlap + day-distribution
inputs (C-EV·6). The feed action's existing call site compiles unchanged
(the fields default); the calendar path sets them explicitly.
**`EventCalendarViewModel`** (a new sealed record next to
`EventIndexViewModel`): `(IReadOnlyList<EventRow> Events, string FromAnchor,
string? PrevAnchor, string? NextAnchor, string MonthLabel, string?
CurrentComponentId, IReadOnlyList<(string Id, string Name)> Components,
string TimeZoneId)` — `PrevAnchor` / `NextAnchor` are `yyyy-MM-dd` strings
(the ±1-month nav targets), `MonthLabel` is a UI-culture display string, and
`TimeZoneId` is the effective zone id — a **display** input only, never an
authorization input (C-EV·5). **`EventController.Calendar(string? from,
string? componentId)`** — `[HttpGet("/events/calendar")]` under the
controller's `[Authorize]`, thin: parse `from` in the effective zone (default
today) → local midnight → UTC → `+30d`; call `ListInRangeAsync`; map rows to
`EventRow`s setting `StartUtc` / `EndUtc` explicitly + the display names
(read lookups, never a gate); build the view model. **No other action
changes.**

**The 13 pinned tests (full list, moved).** **10 seam tests**
(`tests/Kumunita.Core.Tests/EventCalendarSeamTests.cs`):
`EV_Range_IncludesEventStartingInWindow`,
`EV_Range_ExcludesEventStartingBeforeWindow`,
`EV_Range_ExcludesEventStartingOnWindowEnd_Exclusive`,
`EV_Range_DraftInvisibleToNonAuthor`, `EV_Range_DraftVisibleToAuthor`,
`EV_Range_DeletedExcludedForEveryone`, `EV_Range_AudienceMemberSeesEvent`,
`EV_Range_NonMemberDenied_NoLeak`,
`EV_Range_AggregateAuditRowShape_TargetKindEvent`,
`EV_Range_ComponentFilterIsFilterNotGate`. **3 Web pins** (appended to
`tests/Kumunita.Web.Tests/EventControllerTests.cs`):
`Calendar_DefaultFromIsTodayInEffectiveZone`,
`Calendar_FromShiftsWindow_AndPrevNextLinks`,
`Calendar_PassesComponentFilter_ToSeam`. **The three-test acceptance gate**
(recorded by the lane's U09): **closed loop** (an author's published event in
the window appears in their calendar with the right chip + one aggregate
row), **handoff** (a group member added to the audience sees the event on the
next render — the non-member's calendar still excludes it), and **part-vs-
whole** (the 13 pinned tests pass together with the full M4
`EventServiceTests` suite still green — the lane is additive, nothing
regressed).

## ADR 0116 — M15: Translation bulk (moved detail)

From `docs/adr/0116-translation-bulk.md` (the ADR body is condensed; the
Status/Context/Decision/Consequences record stays there with pointers here).

**D1 — The bundle is a projection over the closed set (full, moved).** Export
reads the *stored* `TranslationResource` rows (via the frozen
`GetTranslationsForAsync` per language + `KnownTranslationKeys.AllKeys`) and
renders the **registry keys × catalog languages** matrix: a key with no stored
row for a language is an **empty cell** (the completeness split, the M·12
floor — never null, never a synthetic row). Import writes back **only rows the
file presents** (present key ∩ catalog language × non-blank text). The store
shape (`TranslationResource`, the unique index, the seeder floor) is untouched.
*Forbids:* a bundle document, a bundle table, a second translation store.

**D2 — One plain-UTF-8 CSV bundle (full, moved).** Line 1 is the marker
`# kumunita-translation-bundle/1`; line 2 the column header
`key,source,en,<code1>,<code2>,…` — `source` is the **registry's `en` floor
text** (`KnownTranslationKeys.EnValues`, read-only reference — the import
**ignores** it), `en` is the **stored** `en` row, then each **enabled +
disabled** catalog language in `SortOrder`. Body rows =
`KnownTranslationKeys.AllKeys` in declaration order. CRLF, UTF-8 (no BOM),
commas quoted per RFC 4180, built with plain BCL string code (no CSV package —
the `IcsWriter` "hand-written, closed subset" discipline). Import **requires**
the exact marker — a file without it is **refused** (the M11 `manifest.json`
`format` pin, ADR 0108 D1). *Forbids:* a new dependency (no CSV package), a
format the marker does not own, and a bundle whose columns are not the
catalog's.

**D3 — Standing (full, moved).** The three new routes join
`LanguagesController`'s existing `[Authorize(Roles = "GlobalAdmin,
Translator")]` class gate (ADR 0021). No new `AccessAction`, no new
`AccessVia`, no `Decide()` branch, no new role, no new adapter — the surface
is an **operator action over platform text** the way the existing editor is
(the ADR 0108 "operator plane, zero new authorization surface" posture).

**D4 — Audit (full, moved).** `translation.export` (`TargetId` = the language
codes joined — the M13 analytics-CSV precedent, ADR 0114), `translation.import`
(`TargetId` = the count of upserted rows, as a string — the *aggregate* the
ADR 0005 M·6 `translation.save` per-key row becomes in batch form), and
`translation.save_all` (the batch editor's one row, `TargetId` = the language
code) — each **exactly one** row, `Via = AccessVia.Admin`, `Outcome = Allow`,
committed in the same session as the writes (C3). A **refused** import writes
**no row** (the M11 fail-closed pin). *Forbids:* N audit rows for N upserts,
and an audit row on a rejected file.

**D5 — Import is validate-then-apply, fail-closed, never destructive (full,
moved).** **Validate** (pure, over the parsed model, **before any write**):
the format marker is exact; the file is non-empty; every key in the body is in
`KnownTranslationKeys.AllKeys`; every language column is a catalog code; the
`source` column is ignored. **Any failure ⇒ zero writes, no audit row**; the
controller surfaces the refusal as a **422** with the first offending row (the
M11 "rejected archive writes nothing" posture, ADR 0108 D4). **Apply**: each
present row (non-blank text, key ∩ catalog) upserts **through the frozen
`UpsertTranslationAsync`'s row write** (the batch lane is a loop over the
*same* row store in one session, not a second store) and commits with the one
audit row. *Forbids:* a "best-effort partial import", and a blank cell that
erases (the ADR 0048 pin — C-M15·4).

**D6 — The batch editor (full, moved).** `GET
/admin/languages/{code}/translations?batch=true` renders the same closed key
list (D1) with **one input per row + one save button** (a batch form,
`translation.save_all`); without the flag the existing per-row editor renders
**unchanged** (the `SaveTranslation` route + the ML-UI U6 view stay byte-
identical — C-M15·7). A blank input in the batch form is the *same* no-op as
the import (C-M15·4). *Forbids:* a second `Translations` view file, a re-shape
of `SaveTranslation`, and a batch form that can blank a row.

**D7 — "Extend with new languages" (full, moved).** A GlobalAdmin adds a
language through the **existing** catalog surface (ADR 0005/0021/0042); the
next **export** shows the new column; the translator fills the file **or** the
batch editor; the **import** / **save_all** lands the rows. M15 adds **zero**
catalog surface — the "new language" story is *composition of the existing
seams*. *Forbids:* a "new language + fill it in one step" mega-lane.

**D8 — The six `kw-l` keys (moved).** `translations.bulk.export` ("Download
translations (CSV)"), `translations.bulk.import` ("Upload translations (CSV)"),
`translations.bulk.import_hint` (the blank-no-op + refused-file rule in one
line), `translations.bulk.save_all` ("Save all"),
`translations.bulk.mode_batch` ("Batch editing"),
`translations.bulk.mode_single` ("Edit one at a time") — registered in
`KnownTranslationKeys` (en/de/fr/da) so the
`KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests` pins extend
automatically (ADR 0015 / ADR 0052).

**D9 — Zero schema change, frozen seams (full, moved).** The
`TranslationResource` doc, its `(Key, LanguageCode)` index, the seeder floors,
and the frozen `ILocalizationService` one-row lanes are **byte-identical**
after M15. The exporter/importer are **pure functions** (in
`Kumunita.Core/Localization/`, the `IcsWriter` / `TodoIcsWriter` "BCL-only,
pure, closed subset" discipline — no session, no audit, no HTTP: the service
composes them). The two new service seams (`UpsertManyTranslationsAsync` +
`SaveAllTranslationsAsync`, plus the read seam `GetBulkTranslationMatrixAsync`
+ the audit seam `RecordTranslationExportAsync`) are **additive** on
`ILocalizationService` (the ADR 0005 M·4 ADD shape).

**D10 — Docs parity at the flip (U06, moved).** `Milestones.cs` M15 →
`StatusDone` + M16 → `StatusNext`; README Roadmap (M15 → done, M16 → in
progress); `docs/STATUS.md`; `docs/ARCHITECTURE.md` (the value-chain table
gains the M15 row); `MilestonesTests.cs` re-pin (rename
`M15_Is_The_Single_InProgress_Milestone` →
`M16_Is_The_Single_InProgress_Milestone` + extend the shipped list with M15).
All in **one** unit (the C-M11·8 precedent).

**Consequences — Neutral / cost (moved).** **Precision for batch** (the named
FACES trade): one save covers the whole language, so a mistaken paste is N rows
in one commit, not one row per commit. Bounded: the audit row records the
*count* (D4), the per-row lane stays one commit away (D6), and the previous
export is the rollback (C-M15·2). One more read seam + two write seams + one
audit seam on `ILocalizationService` (the ADR 0084/0085 additive-frozen-surface
idiom honoured). The **marker is the format authority** (D2) — a future version
that changes the marker refuses old bundles (fail-closed), and hand-edited
files that drop the marker are refused with a named row (F5).

**Consequences — Not chosen (moved).** A second translation store / a bundle
document (C-M15·1). A new authorization surface (C-M15·5). N audit rows for N
upserts (C-M15·6). A blank-erases semantic (C-M15·4). A CSV package (C-M15·8).

**Consequences — Deferred to their own lanes (moved).** **UGC translation
bulk** (the D2 scope pin — M15's bundle is **platform text only**; posts /
replies / announcements / events / pages / group+community name+description
translations are **per-parent rows with their own standing matrix** — own lane,
own ADR). **A remove/blank lane in the bundle** (the D5 blank-no-op pin).
**Machine translation** (the README §Deferred pin, ADR 0005 C). **A "new
language + fill it in one step" mega-lane** (the D7 composition note). **Other
i18n formats** (XLIFF / PO / …). **Per-key ownership / review workflow** (ADR
0021 "Revisit when").

## ADR 0285 — Kiosk playlist: any item, per-entry duration, scroll and comments (moved detail)

From `docs/adr/0285-kiosk-playlist-any-item.md` (the ADR body is condensed; the
Status/Context/Decision/Consequences record stays there with pointers here).
**No design doc exists for this ADR** — the handoff note is the moved-detail
home.

**D1 — Item types (full table, moved).** `KioskItemType` is a closed set:
`Board`, `Todo`, `Project`, `Calendar`, `Event`, `Page`, `Announcement`,
`CommunityPost`, `GroupPost`. The old `Todos` list kind is dropped (no live
deployments, so no migration; old playlist rows load as empty playlists
because the `Views` field is gone).

| Type | Item | Extra | Slide route |
|---|---|---|---|
| Board | board id | Board or List view | `/projects/boards/{id}` (`?view=list`) |
| Todo | to-do id | | `/projects/todos/{id}` |
| Project | project id | | `/projects/projects/{id}` |
| Calendar | component id, or none = All | Day / Week / Month | `/events/calendar?view=…&componentId=…` |
| Event | event id (+ group id for a group event) | | `/events/{id}` or `/groups/{g}/events/{id}` |
| Page | page id | | `/pages/{path}` |
| Announcement | announcement id | | `/announcements/{id}` |
| CommunityPost | post id | | `/posts/{id}` |
| GroupPost | post id + group id | | `/groups/{g}/posts/{id}` |

The calendar stays the community calendar ("All" or one component). Group
calendars and a combined calendar are a possible follow-up: no group calendar
view exists, and the calendar's query excludes group events.

**D2 — The item picker (full, moved).** After choosing a type, the resident
picks the item from a search box with suggestions. `GET
/settings/kiosk/items?type=…&q=…` returns at most 20 items the resident can
see: the most recent when `q` is blank, a case-insensitive title / body match
otherwise. The endpoint composes existing reads only (`IKioskItemCatalog`, a
Web-side service): the M8 search surfaces
(`ISearchService.SearchSurfaceAsync`), a new
`ISearchService.BrowseSurfaceAsync` (the same per-surface read with no text
filter — same canonical pre-filter, same frozen decision, same
`"search:<surface>"` audit row), and the enabled component list for the
calendar. Event searches both the community and the group scope. No new
`AccessAction`, `Decide()` branch or adapter.

**D4 — Scroll behaviour (full, moved).** Each entry has `Scroll`: `None` or
`Auto`. Auto scrolls the framed page at a fixed readable speed, with a pause
at the top and the bottom. The duration is a **minimum**: if the scroll needs
longer, the slide stays until it has finished. If the scroll finishes early,
the page scrolls back to the top and stays there until the duration ends (no
second pass, which could overrun the duration). The speed is one playlist-
wide setting, `ScrollSpeed`: Slow / Medium / Fast, because a readable speed
depends on the screen and the viewing distance, not on the item. A content
change detected while a slide is scrolling is applied when the slide next
comes round, so the scroll is never reset half-way.

**D5 — Comments and RSVP lists (full, moved).** Each entry has `ShowComments`
(default off). When off, comment threads, replies and event RSVP lists are
hidden on the slide; comment composers are always hidden in kiosk mode. The
flag travels to the framed page as a query parameter that `_Layout` turns into
a body class, and site.css hides the `.kmb-discussion` sections in kiosk mode
unless the class is present. No detail controller changes.

**D6 — Board list view (full, moved).** `/projects/boards/{id}?view=list`
renders the board's to-dos as rows grouped under their lane headings, from the
same `GetBoardAsync` data (so the kiosk's board fingerprint applies unchanged).
It is an ordinary view mode of the board page, also reachable outside the
kiosk.

**D7 — Kiosk detection by URL signal, not by referrer (full, moved).** The
kiosk signal that lets `_Layout` mark a framed page `body.kmb-kiosk` is the
**`?kiosk=1` query parameter the kiosk appends to every slide href**
(`KioskController.KioskQueryKey`). It is read from the request URL — not from
the `Referer` header — because the site sets `Referrer-Policy: no-referrer`
globally (the F7 prod-hardening floor, kept), which means a slide iframe
**sends no `Referer` header at all**: the original `Sec-Fetch-Dest: iframe` +
`Referer.Contains("/kiosk")` test in `_Layout` therefore **never fired in a
real browser**, so `body.kmb-kiosk` was never applied and **none of the kiosk
CSS activated** (all the control shells stayed visible on a wall display).
The bug was invisible to `curl`-based checks, which set the `Referer` header
manually. Concretely: `Classify` (the one path both `Index` and `Slide` build
their slides through) always appends `?kiosk=1` to the resolved href
(`AppendQuery(baseHref, "kiosk", "1")`), ahead of the D5 `?kioskComments=1`
flag. `_Layout` now sets `kmb-kiosk` from `kmbEmbed && Query["kiosk"]=="1"`,
with the referrer test retained only as a harmless fallback. The onboarding
walk-through modal (which iframes **interactive** settings pages) does **not**
append `?kiosk=1`, so its framed pages keep only the shared `kmb-embed` chrome
floor and stay fully interactive. `KioskControllerTests` pins both ends.

**Consequences — full (moved).** `KioskView` / `KioskViewKind` /
`ViewIntervalSeconds` are removed; `KioskEntry` + the new playlist members
replace them. `IKioskService.SavePlaylistAsync` takes a `KioskPlaylistDraft`.
`/settings/kiosk` becomes a JavaScript editor (add, remove, reorder, search).
Without scripts the stored entries still render and save, but the item search
does not work. Every refresh tick still calls the item's read seam (one
decision row per tick for audited types). New translation keys under
`kiosk.settings.*` (en / de / fr / da; de / fr / da machine-written, native
review pending). The `improve-check.ps1` gate (a) baseline re-baselined on
2026-11-01: `KnownTranslationKeys.cs` **14905 → 15157** and
`ProjectsController.cs` **5370 → 5373** (annotated in `improve-check.ps1` gate
(a) and here).

**Delivery (moved).** Patches 110–113: item search (110), board list view
(111), playlist model and editor (112), playback and the comments toggle
(113). Each compiles on its own.

**Amendment (2026-11-02) — hide the control-only shells on a slide (full,
moved).** A wall display is read-only: it shows the item's **content**, not
the chrome that does something. Three cards are composed **only** of a
heading plus a control, so once the generic rules hide the control an
**orphaned heading band** is left: the event's **"Add a to-do to this event"**
card; the event's **RSVP** card; and the **"Translations:"** selector band.
Three shell-hide rules were added in `site.css`:
`body.kmb-kiosk .kmb-detail-card:has(> .card-body > form)` (the pure-composer
shape — deliberately the **direct-child** `> .card-body > form`, **not**
`:has(form)`, so a content card carrying the ⋮ dropdown's menu forms is not
hidden); `body.kmb-kiosk .kmb-detail-card:has(form.kmb-rsvp)` (the RSVP card,
covering the comments-OFF case); and `body.kmb-kiosk .d-flex:has(>
[data-translation-chip])` (only the chip row, never the surrounding content
card). `KioskControllerTests.ADR0285_PlaybackAndCommentsWiring_IsInPlace` pins
the three selectors.

**Amendment (2026-11-02) — calendar slide: hide the control clusters, fit the
grid to the frame (full, moved).** The calendar page groups its controls inside
non-interactive wrappers whose bands remain on the wall display. Four cluster-
hide rules were added (all scoped to `body.kmb-kiosk`, so the other ~20 pages
that reuse `.kmb-tab-under-navbar` / `.d-flex.mb-3` are unaffected):
`body.kmb-kiosk .lede-row .d-flex` (the **Full page / Full screen** toggle
pair); `body.kmb-kiosk .head-row > .d-flex` (the right header cluster —
**Day/Week/Month** view toggle + **New event** + **Calendar feed (iCal)**);
`body.kmb-kiosk .d-flex.mb-3` (the **Prev / Today / Next** nav links + the
**Filter** form); `body.kmb-kiosk .kmb-tab-under-navbar` (the **Events /
Calendar** tab strip). The grid height: `body.kmb-kiosk #events-calendar
.events-time-body { height: auto !important }` re-pins the Day/Week 24-hour
body to `auto`, so a frame shorter than 72rem does not leave a vertical
scrollbar (the M35 amendment doctrine — no JS change).
`KioskControllerTests.ADR0285_PlaybackAndCommentsWiring_IsInPlace` pins the
four cluster selectors + the height rule.

## ADR 0278 — Group messaging: multi-participant chat threads (the N:1 generalization of M9) (moved detail)

From `docs/adr/0278-group-messaging-n1-lane.md` (the ADR body is condensed;
the Status/Context/Decision/Consequences record stays there with pointers
here).

**The two documents' full field sets (moved).** **`GroupConversation`** —
`Id` (surrogate, the M6 convention), `CreatorId`, `Title` (an **optional**
user-chosen name for the group — null/blank means the group is unnamed and
the UI falls back to the participant-count label; set at open time, capped at
`GroupMessagingService.MaxTitleChars` = 120 chars), `Created`,
`LastMessageAt` (stamped by `SendAsync`), and `Participants` (a sorted,
de-duped `List<string>` — the creator is always one of them; two or more
entries are invariant, a one-person "group" is a 1:1 the M9 surface covers).
**No unique pair index** — a group is *not* canonical (the same members may
legitimately form two groups: a work group and a social group), so the F1
idempotency witness the M9 `convo_uidx_pair` index provides has no N:1
analogue; the de-dup is social, not DB-level. A `Participants` membership
index supports the `ListGroupsAsync` participant-set query (the array-contains
shape, the Postgres `@>` clause). **`GroupMessage`** — the M9 `Message` shape
generalized: `Id`, `ConversationId`, `SenderId`, `Body` (≤ 2000 chars, the M9
D7 cap), `Created`, `LanguageCode` (the ADR 0018 authored-in tag), and
`ReadBy` (a **set** of reader ids, sorted — the per-participant read marker;
a message is *unread for the actor* when `ReadBy` does not contain the actor's
id and `SenderId != actorId`). The `(ConversationId, Created)` thread-ordering
index matches the `GetConversationAsync` thread read's order (the M9
`Message` index shape).

**`IGroupMessagingService` — full seam list (moved).** The M9
`IMessagingService` shape generalized: `IsMessagingEnabledAsync` (the same
`LocaleSettings.MessagingEnabled` read — the shared master gate),
`OpenGroupAsync(actorId, recipientIds, title = null)` (creates the group with
the actor + the recipients, sorted, and the optional user-chosen `Title`;
fewer than two distinct participants is an `ArgumentException` — a one-person
group is a 1:1; a title over `MaxTitleChars` is the same `ArgumentException`
shape), `AddMembersAsync(conversationId, actorId, newMemberIds)` (the
any-participant-can-add rule; a non-participant actor is the non-leaky 404;
one `group.members.add` audit row), `GetConversationAsync` (the non-leaky 404
for a non-participant), `ListGroupsAsync` (the actor's groups, newest-
activity-first, the ADR 0090 `HasMore` paging signal), `SendAsync` (every
participant except the sender gets exactly one `group.message.new`
notification, deduped by the stable idempotency key
`notification:group.message.new:{messageId}`; one `group.message.send` audit
row), and `MarkReadAsync` (the per-participant read marker — appends the
actor's id to `ReadBy` on the actor's unread messages only; the sender's own
messages and the other participants' read state are never touched).

**`GroupMessagesController` — full surface (moved).** The M9
`MessagesController` shape generalized: `GET /group-messages` (the group list
— each row shows the group's name, falling back to a "N people" label when it
has none — + the new-group multi-recipient picker with the optional
**group-name** field), `POST /group-messages/open` (open a new group with the
picked residents + the optional name), `GET /group-messages/{id}` (the thread
— the header shows the group's name, falling back to "N people" — + composer
+ unread markers + the add-member picker), `POST /group-messages/{id}/send`,
and `POST /group-messages/{id}/members` (the any-participant-can-add rule).
The F5 toggle gate + the per-actor gate run **before any list call** on every
action (the M9 U04 shape carried to the N:1 surface); the blank body on
`Send` renders the error state **without any service call** (the M9 U06 shape
carried); the rate limiter is the existing `"message"` window (20 req / 15
min — the M9 composer's limiter, reused).

**The messages tab strip (moved).** `_MessagesTabs.cshtml`, the Bootstrap
nav-tabs idiom from `_ProjectsTabs.cshtml` / ADR 0062 — the resident's entry
point into the two messaging surfaces. The account nav keeps a single
**Messages** entry (`/messages`, the M9 1:1 surface, the
`IsMessagingEnabledAsync` + `IsMessagingAllowedForAsync` gate); the group
lane is **not** a separate account-menu entry — it lives as a **Direct ·
Group** tab strip rendered on all four messaging views (`Messages/Index`,
`Messages/Thread`, `GroupMessages/Index`, `GroupMessages/Thread`). The strip
self-gates on the **same** `IsMessagingEnabledAsync` +
`IsMessagingAllowedForAsync` composite the account menu used (a disallowed
resident sees no tabs, the same non-leaky shape as an off instance — the
C-M9·2 "enforced in the service, not only the view" pin carried to the N:1
surface), and the active tab is derived from the current controller name
(Messages → Direct, GroupMessages → Group) so the two surfaces stay
differentiated.

**Notification kinds (moved).** Two new notification kinds
(`NotificationKinds.GroupMessageNew` / `.GroupMessageMention`, the
`group.message.new` / `group.message.mention` constants) — the M9
`message.new` / `message.mention` kinds generalized to the N:1 surface,
resident-facing opt-OUT (the M9 / M36 posture), joined to the closed
`NotificationKinds.Known` set (the 28 → 30 re-baseline, the
`NotificationsControllerTests` closed-set pin annotated). **Pins re-based**
(the same re-baseline-and-annotate doctrine): the `KnownTranslationKeys.cs`
gate baseline (14707 → 14815, the 15 `group_message.*` UI keys + the 8
`notification*.group.message.*` kind keys × en/de/fr/da); the
`NotificationsControllerTests` closed-set pin (28 → 30, the two new kinds).

**Amendment — the invitation flow (2026-10-30) (full, moved).** The first
shape added "add members" directly (the `AddMembersAsync` seam appended to
`Participants` immediately; the recipients picked at open time were added to
`Participants` too). The user's follow-up ask reshapes that: *the thread
should list its members, and adding a resident should be treated like a group
invitation — the recipient gets a nudge to accept or reject joining.* Three
design choices (confirmed with the user): (1) **The invitation flow is always
on** — including when opening a new group. The **creator is the only initial
participant**; everyone else (picked at open time *or* added later) gets a
**pending invitation** to accept or decline before joining the thread. (2)
**Pending invitees are visible** — the thread's member roster lists the
**accepted** participants plus the **pending** invitees (marked "(pending)").
A pending invitee is *not* a participant: they cannot read / send in the
thread until they accept. (3) **No cancel action in v1** — an invitation
stays pending until the recipient responds (the community-group
`GroupInvitation` posture minus the `Cancelled` state).

The reshaped seams (the community-group `UserInfoService` invitation pattern,
ADR 0083, carried to the N:1 surface): **`GroupMessagingInvitation`** (a
third document in the context, the `UserInfo/GroupInvitation.cs` shape
generalized) — `Id`, `ConversationId`, `InvitedId`, `InvitedBy`, `Status` (a
`GroupMessagingInvitationStatus` enum: `Pending` / `Accepted` / `Declined` —
**no** `Cancelled` state), `InvitedAt`, `ResolvedAt?`, `ResolvedBy?`.
Business key `(ConversationId, InvitedId)` — a **unique** pair index (one row
per (thread, invitee); a re-invite after a resolution resets the existing row
to a fresh Pending, the C-M2b·3 re-stamp convention).
**`GroupConversation.Participants`** is now the **accepted** member set (the
authorization boundary for read / send). It holds the creator alone at open
time, and grows as invitees accept. A pending invitee is **not** in
`Participants`. **`OpenGroupAsync`** — the creator is the only participant;
each distinct recipient gets a `GroupMessagingInvitation` Pending row + a
`group.message.invite` nudge (the accept / decline deep-links, the ADR 0095
`AcceptPath` / `DeclinePath` shape). **`InviteMembersAsync`** (replaces
`AddMembersAsync`) — the any-participant-can-invite rule; each invitee (de-
duped against the participant set) gets a Pending row + nudge; one
`group.message.invite` audit row. **`AcceptInviteAsync(conversationId,
actorId)`** — the self-lane (the actor must be the invitee; an already-
resolved row is the C-M2b·3 wall). The row moves Pending → Accepted; the actor
is appended to `Participants` (sorted, de-duped) — they become a participant
on the very next read (invariant C4); one `group.message.invite.accept` audit
row. **`DeclineInviteAsync(conversationId, actorId)`** — the self-lane. The
row moves Pending → Declined; **no** participant set is touched (the invitee
never becomes a member); one `group.message.invite.decline` audit row.
**`GetMembersAsync(conversationId, actorId)`** — the thread's member roster:
the accepted participants + the pending invitees (each with a `Pending` flag
+ resolved display name). A non-participant actor is the non-leaky 404 (a
non-participant cannot see the roster — that would leak the pending invitees'
presence). **`GetPendingInvitationsForUserAsync(userId)`** — the self-lane
gate seam the Web uses to map a non-invitee to a non-leaky 404 before calling
accept / decline (the `UserInfoService.GetPendingInvitationsForUserAsync`
precedent). **`GroupMessagesController`** — the new surface:
`GET|POST /group-messages/{id}/invitations/accept` and
`GET|POST /group-messages/{id}/invitations/decline` (the GET forms are the
inbox accept / decline-button deep-links, no anti-forgery token — the M1
`/account/verify` one-time-link precedent; the POST forms carry the token).
The `Thread` view renders a **Members** section (the accepted + pending
roster, pending marked "(pending)") and the add-member picker becomes the
**invite-people** picker. The "Send" nudge universe is the **accepted**
participants only (a pending invitee is not nudged until they accept).
**`NotificationKinds.GroupMessageInvite`** (the `group.message.invite`
constant) — the invitation nudge kind, resident-facing opt-OUT, joined to the
closed `NotificationKinds.Known` set (the 30 → 31 re-baseline, the
`NotificationsControllerTests` closed-set pin annotated). **Pins re-based**
(the same doctrine): the `KnownTranslationKeys.cs` gate baseline (14823 →
14863, the 7 new `group_message.*` UI keys — `members` / `pending` /
`invite_sent` / `invite_accepted` / `invite_declined` /
`invite_not_pending` — + the 3 `notification*.group.message.invite.*` kind
keys × en/de/fr/da); the `NotificationsControllerTests` closed-set pin (30 →
31, the new kind).

## ADR 0123 — Extended user profiles (moved detail)

From `docs/adr/0123-extended-profiles.md` (the ADR body is condensed; the
Status/Context/Decision/Consequences record stays there with pointers here).

**D1 — `Profile` gains two additive fields (full, moved).** The M1-registered
`Profile` doc (`Kumunita.Core.UserInfo`) gains exactly two **additive POCO
fields** (the `Post.Status` / `Post.TagIds` / `Profile.AvatarId` /
`Profile.TimeZone` history — delta-detected, idempotent, **no re-seed**, **no
EF migration**, **no new `*DocTypes` surface** — `Profile` already registers in
`CoreDocTypes`): `public string? Bio { get; set; }` (null/empty ⇒ no bio
block) and `public string[] TagIds { get; set; } = []` (a `string[]` of
**`Tag.Id`** values referencing the **frozen ADR 0044 `Tag`** — the
`Post.TagIds` / `Page.TagIds` idiom verbatim; M23 adds `Profile` as a **third
referencer**, creating **no** new tag type). *Forbids:* a new `ProfileBio` /
`ProfileTag` document, a new `M23DocTypes` surface, a new relational table, a
second `Tag` doc type for profiles, or storing the bio as a separate doc.

**D2 — Bio + tags gated by the existing `Profile.Visibility` audience (full,
moved).** Bio + tags render on the directory detail **only** when the viewer's
`Visibility` decision allows (one `CanAsync` via the **existing**
`ProfileToAuditableResource`; the **owner branch** lets the author always see
their own). The contact block (email/phone/address) **stays** on the
**existing** `ContactVisibility` audience + `ContactVisibilityResource`
decision — **two independent gates, two `CanAsync` calls, two `AccessAudit`
rows**; a **self-view** short-circuits both (owner branch); a `null`/absent
`ContactVisibility` short-circuits the contact gate with no row (the existing
C-M2·1 shape). *Forbids:* a **new** `AccessAction` / `AccessVia` / `Decide()`
branch, a new `IAuditableResource` adapter (the two existing ones are reused),
a new `TargetKind` (`"directory"` is reused), a **per-field** audience (bio
and tags share the *one* `Visibility` gate), or folding the contact gate into
the bio/tags gate.

**D3 — Write through the existing single `UpsertProfileAsync` lane (full,
moved).** `ProfileUpdate` gains **two additive optional trailing fields**
(`string? Bio = null`, `IReadOnlyCollection<string>? TagIds = null` — the
`Address` precedent, "appended with a default after the frozen shape so
existing positional call sites compile unchanged"; `null` ⇒ don't touch).
`UpsertProfileAsync` writes `Profile.Bio` **verbatim** (ADR 0001-B; a `null`
patch leaves it) and resolves `Profile.TagIds` to `Tag.Id` values via the
**frozen ADR 0044 create-or-get idiom** (inlined in the `UserInfoService`
session — the `TagService.AttachToPostAsync` shape; a missing `Slug` **
creates** a `Tag` with `CreatedBy = actorId` + one `tag.create` row; a present
`Slug` **reuses** the existing `Tag`, no row). The profile write itself emits
**no** `AccessAudit` row (a Profile field write, not an access decision); the
**only** audit rows are the **frozen TG-lane** `tag.create` rows, one per
newly created tag (C-TG·9). **All in the caller's session, one
`SaveChangesAsync`** (C3). *Forbids:* a **new** `IUserInfoService` write
method for profile tags/bio, a **new** `ITagService` profile method, a
separate "profile tag editor" controller, or an unaudited direct
`session.Store` from a controller.

**D4 — The new composition service `IProfileFindService` (full, moved).**
`IProfileFindService` + `ProfileFindService` live in `Kumunita.Core.UserInfo`
(alongside `DirectoryService`), registered in `DependencyInjection.cs` the
same way `DirectoryService` is. It composes **only** the frozen seams —
`IUserInfoService` (the candidate set via the existing `GetProfilesAsync`),
`IAuthorizationService` (the per-profile `Visibility` decision), the **frozen
ADR 0044 `ITagService`** (tag display-name resolution) + its own
`IDocumentStore`. Two **paged** read methods (`PageSize = 30`, the M7/ADR
0090 D6 idiom): `FindPeopleByTagAsync(slug, actorId, page)` (the C-TG·2 base
query, carried to profiles) and `FindPeopleByBioAsync(q, actorId, page)` (a
case-insensitive bio substring, the M8 D4 engine). Both: candidate set =
`GetProfilesAsync(false)` (non-blocked); filter by the match; **gate the
candidate set in ONE `CanSeeAsync` pass** over
`candidates.Select(p => new ProfileToAuditableResource(p))` (the frozen bulk
decision — **not** a per-profile `CanAsync` loop; the owner branch resolves
internally); return survivors + `HasMore`; the **one aggregate
`AccessAudit` row** (`targetKind "directory"`, `visibleCount`/`hiddenCount`
set, `targetId = null`) is **emitted by `CanSeeAsync` itself** (its own
commit), **not hand-written**. A blank `q` / a missing tag returns an empty
page with **no** decision and **no** row. *Forbids:* a new bounded context, a
new DocTypes surface, a new document, a new `AccessAction`/`AccessVia`, a
**per-profile `CanAsync` loop**, a **hand-written aggregate `AccessAudit`
row**, a find-people read that surfaces a profile the viewer cannot see, or a
find-people read that emits **no** aggregate audit row.

**D5 — Rich-content bio + reused editor (full, moved).** The profile editor
(`/profile/edit`) adds a **bio** field (the ADR 0031/0033 WYSIWYG editor, or
a Markdown textarea, over a `string` field — exactly the surface
posts/announcements/pages use) + a **tags** input (the **frozen ADR 0044 tag
input** — the composer's tag picker + `ITagService.SuggestAsync` autocomplete).
The existing two audience editors (`Visibility` + `ContactVisibility`) are
**unchanged**. The directory detail renders the bio via the **existing**
`MarkdownRenderer.RenderHtml` helper (the "single Markdown engine") —
`null`/empty bio ⇒ no bio block (no placeholder). Profile tags render as
chips (the ADR 0044 tag display-name idiom, resolved per-viewer). *Forbids:*
a **new** editor subsystem, a **new** tag-management surface for profiles,
inline WYSIWYG editing on the detail view (the ADR 0032/0033 lane — deferred,
D8·1), or a bio rendered as plain text.

**D6 — Privacy-pin (full, moved).** Find-people results are computed over
profiles the viewer may **already see**; a match is a *feed organizer*, never
a gate; a profile the viewer cannot see never surfaces and its bio/tag never
leaks (the ADR 0044 D5 global-enumeration exclusion carried to profiles).
Both reads filter the candidate set by the match, **then** gate each survivor
by its `Visibility`; a denied profile **never** appears and its `Bio`/`TagIds`
are never a match artifact. The tag's *display name* may surface (it is a
label, ADR 0044 D5) but only as part of a profile the viewer may already see.
A **global tag enumeration** over profiles is excluded by design. Each read
emits **one** aggregate `AccessAudit` row. *Forbids:* a bio/tag match that
returns a profile the viewer cannot see, a global profile-tag enumeration that
leaks a subject, or an unaudited find-people read.

**D8 — Deferred lanes (full list, moved).** (1) **Inline WYSIWYG editing of
the bio on the detail view** (the ADR 0032/0033 inline-edit lane). (2) **Bio
full-text / semantic search** (beyond substring). (3) **A people-discovery
*surface*** (facet browse, "people in common," a `/people/skills/{tag}`
taxonomy, shared-interests). (4) **A "new bio/tags set" notification** — rides
M6 (ADR 0076); M23 stages **no** email. (5) **Profile-tag curation by an
admin** (a moderator/global-admin manages another resident's tags) — M23 is
author-self-edit only (the ADR 0003 SoD lane is untouched). (6) **Bio image
alt-text / captioning** — rides the ADR 0025/0011 image lane; M23 reuses it
as-is.

**Consequences — Neutral / cost (moved).** **The named trade — a *resident
biography + free author-set tags + a find-people read* for zero new
authorization surface:** M23 buys *a resident's biography + free author-set
tags (skills/interests/knowledge/expertise) and a find-people read over them —
the "find people with something in common" promise* with **zero new
authorization surface + two additive fields on the existing `Profile` doc +
one composition service + one extended write lane + a closed `kw-l` set**
(12 keys × en/de/fr/da), in exchange for **no new document, no new context,
no new DocTypes surface, no inline-edit, no full-text search, no people-
discovery surface, no admin curation** (D8): M23 is *the profile's bio + its
tags + a find-people read*, **not** a profile management system or a people
network. **Two independent gates** (D2, C-M23·1): bio + tags are gated by the
**one** existing `Visibility` audience; the contact block **stays** on the
**one** existing `ContactVisibility` audience — **two audiences, two gates,
zero new ones**. **The find-people read is over visible profiles only**
(D6, C-M23·4): a match is a feed organizer, never a gate; a denied profile's
bio/tag never leaks; one aggregate audit row per read (emitted by the frozen
`CanSeeAsync`, not hand-written).
