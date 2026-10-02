# ADR 0127 — Every navbar surface has a resident guide: the guide set is expanded to cover announcements, directory, people, inventory, bookmarks, documents, pages, and tags (one guide per remaining navbar item); the `help` page links all of them (the `UG` lane continues)

Status: Accepted
Date: 2026-10-03
Amends: **0057** (the guide set + the `UG` consistency loop — this lane
**adds eight** resident-facing guides to the registry and adds a matching
bullet for each to the `help` page's `## Guides` list in all four languages.
0057's D1 "exactly twelve" / "the eleven above" counts are now stale — the
set has already grown past twelve through the `notifications` and `projects`
lanes; this ADR supersedes those counts with the **current** registry and
restates the "a new guide is a new lane" rule as the governing discipline,
not a frozen number).

## Context

ADR 0057 established the `UG` lane: resident-facing guides are `Page` docs
nested under the canonical `help` page, an `en` code-owned floor plus curated
`de`/`fr`/`da` baselines, held honest by a three-layer consistency loop
(registry ↔ drift-pin test ↔ the `help` page's `## Guides` link list). Its
D1 pinned the seeded set as a **"closed set"** — "a new guide is a new lane,
not a silent addition" — and applied the ADR 0040 `PageKind` closed-set rule
to the guide set. The counts in that ADR ("twelve", "the eleven above") were
correct *when it shipped* and have since grown as each capability shipped its
own guide lane (`notifications`, `projects`); that growth is the lane
discipline working as intended.

The platform's navbar now exposes **eight more resident-facing surfaces**
that have shipped but have **no guide**: **announcements**, **directory**,
**people** (find by tag / bio), **inventory** (shared & private items, check
out / in), **bookmarks**, **documents** (community file store), **pages**
(the `system`/community page tree), and **tags**. A resident who reaches any
of these from the navbar has no "how do I …?" answer in the `help/` subtree —
exactly the seam ADR 0057 opened (a surface with no resident guide).

Two further gaps, both on the *discovery* side rather than the *content*
side:

- **The `help` page's `## Guides` list did not link the `notifications`
  guide.** The guide existed (seeded in its own lane) but the root `/help`
  page — the entry point a resident actually lands on — never listed it, so
  it was reachable only by typing the URL. The eight new guides have the same
  defect by construction: added to the registry but absent from the `## Guides`
  list in all four languages.

**The fork:** either (a) treat the guide set as frozen at whatever it was and
leave these eight surfaces un-guided, or (b) continue the `UG` lane — add the
eight guides (in all four languages, following the exact cross-link and
ownership conventions of the existing set) and wire each one into the `help`
page's `## Guides` list so the root page is the honest index. This ADR takes
**(b)** and, while doing so, restates the discipline as *every navbar surface
gets a guide* so the "closed set" phrasing no longer reads as a count to
police.

## Decision

### D1 — Eight new guides, one per remaining navbar surface

Each new guide follows the ADR 0057 D1 shape exactly (so the existing drift-pin
tests — which count *dynamically* — stay green): a direct child of the
canonical `help` page, `Kind = System`, `Audience = null` (public), authored-in
`en` with a curated `de`/`fr`/`da` baseline (never machine-translated),
`AuthorId = string.Empty`, not draft, not deleted. Each carries the same
cross-link convention as the existing set: the `en` floor uses **relative**
sibling links (`[posts](posts)`); the `de`/`fr`/`da` baselines use **absolute**
links (`[posts](/pages/system/help/posts)`) because the canonical URL is
`/pages/system/help/{slug}`.

| Slug (under `help/`) | Covers |
|---|---|
| `announcements` | pinned notes; public vs resident-scoped; what a resident can and cannot do with one |
| `directory` | the residents on the platform and the details each has chosen to share (per-field contact opt-in) |
| `people` | finding people by tag or by bio over the actor-visible resident set (sign-in required; a profile you cannot see never surfaces) |
| `inventory` | shared and private items; checking one out and in; the usage history |
| `bookmarks` | the things a resident has saved (posts, events, to-dos, announcements, pages) and what "no longer available" means |
| `documents` | the community file store: upload, download, audience, and who may edit |
| `pages` | the `system` / community page tree; community pages vs platform pages; reset-to-seeded |
| `tags` | what a tag is; one-line descriptions; where a tag may appear; who may translate one |

### D2 — The `help` page's `## Guides` list links every guide

The `"help"` body in all four `*DefaultPages()` methods gains a bullet for
**each** of the eight new guides **and** for the previously-unlinked
`notifications` guide, so the root `/help` page is a complete index of the
`help/` subtree. The four-language `help` bodies remain structurally
parallel (same bullets, same order) — the ADR 0042 D2 / ADR 0043 parity bar
the baseline tests read. The `## Getting started` summary and the other three
surfaces (`terms` / `privacy` / `conduct`) are unchanged.

### D3 — The "closed set" rule restated as a discipline, not a count

ADR 0057's "exactly twelve" / "the eleven above" counts are superseded. The
governing rule is the one that was *behind* them, now stated plainly:

> **Every resident-facing surface reachable from the navbar has a guide in the
> `help/` subtree, and the `help` page's `## Guides` list links it.** Adding a
> guide is a new lane (ADR 0040 closed-set discipline) — not a silent edit —
> and a shipped navbar surface with no guide is a seam to close, not an
> accepted gap.

The drift-pin tests count dynamically (`guideSlugs.Length`, `guideIds.Count *
3`), so they track the set without a hard-coded number; this ADR keeps that
true. No test in the `UG` / `LS` / `SP` suites pins a literal guide count.

## Consequences

- The registry grows from 14 to **22** guides in each of the four languages
  (en / de / fr / da), all additive on the code-owned `en` floor and the
  community-owned baselines.
- The `help` page (the resident's entry point) now lists all 22 guides; a
  resident who lands on `/help` can reach every navbar surface's guide in one
  hop, including `notifications` for the first time.
- Re-seeding an existing instance is idempotent: the seeder/backfill walks the
  registry arrays and creates the eight guides + baselines if missing,
  refreshing only the `en` floor on re-seed (the non-`en` baselines are
  community-owned and are never overwritten once set — ADR 0042 D1 / ADR 0057).
- The `## Guides` list is the single source of "what is a guide" for the
  resident; if a future surface ships, its lane must add a guide **and** a
  bullet here (D3), or the seam ADR 0057 closed reopens.
