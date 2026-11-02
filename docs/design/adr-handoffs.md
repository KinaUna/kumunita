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
