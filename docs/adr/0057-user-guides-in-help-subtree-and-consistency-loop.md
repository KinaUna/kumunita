---
id: adr-0057
title: "User guides: resident-facing guides live in the `help/` subtree of the existing page tree; `en` floor + curated `de`/`fr`/`da` baselines (community-owned); a three-layer consistency loop keeps them honest as code changes (the `UG` named lane)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/adr-handoffs.md, docs/archive/lanes/user-guides/plan-user-guides.md]
supersedes: [adr-0039]
superseded_by: [adr-0127, adr-0160]
updated: 2026-11-02
---

# ADR 0057 — User guides: resident-facing guides live in the `help/` subtree of the existing page tree; `en` floor + curated `de`/`fr`/`da` baselines (community-owned); a three-layer consistency loop keeps them honest as code changes (the `UG` named lane)

Status: Accepted | Date: 2026-09-21

> **Abstract:** the resident-facing guides are `Page` docs nested under the
> canonical `help` page (not a new surface), with a code-owned `en` floor +
> community-owned `de`/`fr`/`da` baselines, kept honest by a three-layer
> consistency loop (registry + event-based DoD + OPS periodic review).
> **Superseded by** ADR 0127 / 0160 (later guides lanes build on this shape).
> **For** a developer touching `GuidePages()` seeding, the `help` page, or the
> guide-consistency tests. **Read the rest** before changing the registry,
> the backfill, or the standing matrix. Full table / test names / references
> in the [handoff note](../design/adr-handoffs.md#adr-0057--user-guides-moved-detail).

**Amends** **0039** (the page tree — the guides are `Page` docs nested under
the `help` page; the `help` page is promoted from a leaf to a *folder-with-index*,
a shape ADR 0039 §3.3 already names as valid), **0043** (the `help` page it
hangs from — this lane adds children, does not touch the five-surface set or
the `about` product-story pin), **0047 / 0049 / 0051** (page-localization +
variant-display — a guide renders in the viewer's effective language; this ADR
ships the `en` floor and delegates translations to those lanes), **additive**
on **0040** (`PageKind.System` + the `system/` namespace root — a guide inherits
the system-page standing matrix verbatim), **0042 D1** (the `en` code-owned /
non-`en` community-owned split), and **0005 C** (the "never
machine-translated" clause — a human Translator is the only writer of a non-`en`
body). **No amendment to any of those** — this ADR is additive on their
decisions exactly as written.

## Context

A fresh instance today serves **one** resident-facing help surface: the
`/help` page (ADR 0043 D1, the five-surface set). That page is a
**four-bullet summary** — "Post to a community feed …", "Groups let you
organize …" — it is *not* a guide: it does not explain **how to do a thing**
(how do I post to only my group? how do I save a draft and publish it later?
how do I pick who can see my post? how do I RSVP to an event?). A resident who
wants to *act* has to guess, or ask an admin in a side-channel — an
un-integrated seam in the platform's own value chain
(`the-platform-as-integrator.md` — "Diagnose confusion as missing
integration, not missing features").

The platform has now shipped a full resident-facing surface — posts + replies +
the audience picker, public/private groups, drafts, the multilingual picker,
timezone + date-format settings, events + RSVP + reminders, the WYSIWYG editor,
file attachments, tags, and the Translator role. **None of it has a resident
guide.** The operator's docs are for the *team*, not the *resident*; a resident
who reads "audience = deny-by-default" does not learn "how do I post to only my
group?".

**The fork that determined this ADR:** build a *new* resident-guide surface
(a separate "guides" module, its own routes, its own editor, its own
translations, its own standing) **or** hang the guides on the existing page
tree under the `help` page. **The existing tree was chosen** (user sign-off
2026-09-21): a new surface would duplicate the page engine, the WYSIWYG editor,
the translator lane, the authorization adapter, the draft/soft-delete idiom — a
bag of parallel lanes that ADR 0039's *absorb* decision explicitly rejected.
Hanging the guides on the tree is **free**: the guides are `Page` docs (the
existing doc), edited by the **one** `bindRichEditor` (ADR 0031/0033),
translated by the **one** `PageTranslation` lane (ADR 0022/0047/0049/0051),
read by the **one** `IAuthorizationService` (ADR 0006), and discovered by the
**one** `/pages` tree browse (ADR 0039 §3.8). The parts are cheap; the
integration (one engine, one editor, one translator, one reader, one tree) is
the value.

The second half is the **consistency** question the user asked: "keep user
guides consistent in the future … a periodic or event based check." A guide
that describes a feature the code no longer has (or a feature the code has
gained without the guide being updated) is a **broken seam**. ADR 0047/0049/0051
show the platform's existing answer to a similar question (UI-strings drift):
a *closed registry* + a *warm-boot backfill* + a *pinned parity test*. This ADR
applies the same shape to the guides.

## Decision

- **D1 — The guides are `Page` docs nested under the canonical `help` page.**
  The seeded set is **exactly twelve** `Page` docs, each a *direct child* of
  `help` (ADR 0043 D1, `system/help`), each `Kind = System` (ADR 0040),
  `Audience = null` (public, ADR 0039 §3.4), `LanguageCode = "en"`,
  `AuthorId = string.Empty` (platform content), `IsDraft = false`,
  `IsDeleted = false`. The **twelve slugs** (closed set, `en` only — the full
  "Covers / documents" table is in the [handoff note](../design/adr-handoffs.md#the-seeded-twelve-guide-table-d1))
  are `getting-started`, `posts`, `groups`, `drafts`, `audience`, `language`,
  `events`, `translator`, `child-accounts`, `being-a-child`, `admins`,
  `moderators`. `getting-started` is the *index* (the `help` page's first
  child; its body links to the other eleven). **A guide that documents a
  feature not yet shipped is not seeded** (M5 / M6 and the deferred items get
  their guides in their own lanes — a new guide is a registry row + the test
  picks it up, never a silent addition).
- **D2 — The `en` floor is code-owned; the non-`en` bodies are community-owned.**
  The `en` body of a guide is **code-owned** — the seeder's `GuidePages()`
  array is the single source; first-boot upserts (code-wins, ADR 0042 D1).
  **A non-`en` guide body ships as a curated `de`/`fr`/`da` baseline**
  (**amended 2026-09-21**, superseding the original "ship `en`-only") — 12
  guides × 3 languages = 36 `PageTranslation` rows, seeded
  create-if-missing only (the ADR 0042 D1 invariant), so a human
  Translator's (ADR 0021) or GlobalAdmin's in-app edit is never clobbered by a
  later deploy; a **machine translation is still never** the writer (ADR 0005 C).
  The standing on a guide is the ADR 0040 system-page matrix: **edit / move /
  delete = GlobalAdmin only**; **add a translation = GlobalAdmin ∪
  Translator**; a community Moderator has **no** standing; a guide is *not* a
  `PageKind.User` page.
- **D2.1 — Warm-boot backfill: guides appear on existing instances at
  upgrade.** `FirstBootSeeder.BackfillUserGuidesAsync` runs in the warm-boot
  branch of `SchemaBootstrap` on **every upgrade boot** — **create-if-missing
  only** (a community-edited or resident-created guide is *skipped, never
  refreshed*), **idempotent**, and does **not** resurrect a retired (soft-
  deleted) guide; the `de`/`fr`/`da` baselines have their own backfill
  (`BackfillGuideTranslationsAsync`). (Full mechanics in the [handoff
  note](../design/adr-handoffs.md#d21--warm-boot-backfill-detail).)
- **D3 — The consistency loop is three layers, each answering a different
  question** (the `in-code.md` "each layer must answer a different question,
  or it's ceremony" rule): **where** (the guides are in the *tree* — the closed
  loop, integrative not parallel); **when** (the event-based check — a
  resident-facing change ships *with* its guide update in the same lane; a
  feature-without-guide is a red test); **how often** (the periodic check — the
  OPS.md §13 quarterly review, the safety net for a missed Definition of Done).
  (Full layer detail in the [handoff
  note](../design/adr-handoffs.md#d3--the-consistency-loop-the-three-layers).)
- **D4 — The drift pin (the test that keeps the registry honest).** The guide
  registry is a single source in the seeder: `FirstBootSeeder.GuidePages()`;
  the test (`UG_U05_GuideRegistryTests`, in `Kumunita.Core.Tests`) pins the
  exact seeded set, the `help`-is-only-root-under-`system` shape, the
  `de`/`fr`/`da` baseline rows, the `en` body parity, and the feature↔guide
  drift (a feature-without-guide or a guide-without-feature is a red test).
  (Exact pin names in the [handoff note](../design/adr-handoffs.md#d4--the-drift-pin-test-detail).)
- **D5 — The registry is the single source.** `FirstBootSeeder.GuidePages()`
  (the `en` bodies) is the source the seeder *and* the tests read; the
  **conventions doc** (`docs/guides/CONVENTIONS.md`) is the *human-facing
  view* of the same set (a new seeder row is a new conventions row and vice
  versa); the drift pin (D4) keeps the two in sync.

## Consequences

- **One guide engine, one editor, one translator, one reader, one tree.** The
  lane *unifies* instead of *duplicates* (the ADR 0039 absorb-decision applied
  to documentation): the guides are `Page` docs, edited by the **one**
  `bindRichEditor`, translated by the **one** `PageTranslation` lane, read by
  the **one** `IAuthorizationService`, discovered by the **one** `/pages` tree
  browse. A new guide is a new registry row, not a new module.
- **The `help` page is promoted from a leaf to a folder-with-index** (ADR 0039
  §3.3 names this valid). Its body gains a **`## Guides` section** listing all
  twelve as absolute links (`/pages/system/help/{slug}`); the four existing
  bullets are unchanged (the ADR 0042 D2 "structure preserved" parity — the
  `de`/`fr`/`da` baseline arrays carry the matching translated section). The
  `/help` hard-coded route (ADR 0043 D2) is **unchanged**.
- **The `en` floor is code-owned; the non-`en` bodies are community-owned**
  (the ADR 0042 D1 / 0047 / 0049 / 0051 shape). A human Translator is the only
  writer of a non-`en` body (ADR 0005 C). The `en` floor is always present;
  the non-`en` body, once a human adds it, is community-owned and never
  clobbered by a later deploy.
- **The consistency loop is three layers, each answering a different question**
  (where / when / how-often). A loop without an actuator is decoration; each
  layer has an owner, a cadence, and a defined response (the OPS.md §13
  procedure is the safety net).
- **No new `AccessAction`, no new `AccessVia`, no new authorization branch.** A
  guide is a `Page` doc (ADR 0039 §3.4), read by the **frozen**
  `IAuthorizationService` (ADR 0006) via the existing
  `PageToAuditableResource` adapter — *reused*, not extended. The ADR 0040
  system-page standing matrix applies verbatim. **No schema change, no new doc
  type, no migration** (it reuses `Page` + `PageTranslation`).
- **The guides are *not* a second doc set.** The operator's docs are for the
  *team*; the guides are for the *resident*. The ADR is the *source* of the
  guide's `en` body (the code-owned floor); the guide's `en` body is the
  *resident-facing view* of the same integration. A guide that contradicts the
  ADR is a broken seam.
- **The `UG` lane is a *named lane*** (the `ML`/`GP`/`RC`/`RE`/`TG`/`PG`
  convention — a short ID, *not* a renumber); M5 / M6 stay Projects /
  Portability; no roadmap letter moves. The `UG` entry is added to
  `Milestones.All` **after `PG` and before `M5`** (`StatusDone`), and the
  README Roadmap gains the `UG` row in the same commit (the
  `MilestonesTests` pin updated in the same commit).
