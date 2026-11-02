---
id: adr-0043
title: "System pages shipped + discoverable: two new `Page` docs (privacy + conduct), their `/privacy` + `/conduct` routes, the footer "Platform" column, and the `/admin` "Platform pages" section — server-rendered, GlobalAdmin ∪ Translator standing, ADR 0040 §2 matrix unchanged"
tier: reference
kind: adr
status: superseded
superseded_by: [adr-0047]
audiences: [developer, agent, platform-maintainer]
related: [docs/design/adr-handoffs.md]
supersedes: []
superseded_by_note: "Superseded by ADR 0047 (static-page localization + backfill)."
updated: 2026-11-02
---

# ADR 0043 — System pages shipped + discoverable: two new `Page` docs (privacy + conduct), their `/privacy` + `/conduct` routes, the footer "Platform" column, and the `/admin` "Platform pages" section — server-rendered, GlobalAdmin ∪ Translator standing, ADR 0040 §2 matrix unchanged

Status: Superseded (by ADR 0047) | Date: 2026-09-17

> **Abstract:** two new **system pages** (`privacy` + `conduct`) ride the
> existing `Page` / `PageTranslation` / `PageKind` documents (ADR 0040) and
> the existing `SeedDefaultPagesAsync` / `SeedPageTranslationsAsync`
> seeder paths (ADR 0042 D1) — the new content flows from two new
> `EnDefaultPages()` / `DeDefaultPages()` / `FrDefaultPages()` array entries
> (two new `PageTranslation` rows per page) and two new routes in
> `StaticPagesController`. The footer gains a **"Platform"** column with
> six `footer.platform.*` registry keys (full `EnValues` / `DeValues` /
> `FrValues` parity, the `KwLRegistryConsistencyTests` pin). The `/admin`
> shell gains a **"Platform pages"** section. The standing is the ADR 0040
> §2 matrix **unchanged**: create/edit/move/delete GlobalAdmin only; add a
> translation GlobalAdmin ∪ Translator; **no** `Moderator` standing.
> **No** schema change, **no** migration, **no** new doc type, **no** new
> `AccessAction` / `AccessVia` / `IAuthorizationService` method. The
> `Milestones.cs` / README / `MilestonesTests.cs` trio lands at the lane's
> ship unit (U05). **Superseded** by ADR 0047 (static-page localization +
> backfill) — the "en-body by contract" record is now the degradation
> case. **For** a developer touching the system-page seed, the
> `StaticPagesController` routes, the footer, the `/admin` shell, or the
> `Page` / `PageTranslation` standing. The content scope (the two `en`
> bodies' structure), the scope in/out list, and the superseded record
> are in the [handoff note](../design/adr-handoffs.md#adr-0043--system-pages-shipped--discoverable-moved-detail).

## Context

The platform ships two system pages (`terms` + `help`) — the ADR 0040
`PageKind = System` shape, the ADR 0042 D1 seeder path. The ADR 0005 B
cookie (the locale preference) and the `SECURITY.md` privacy model both
need a *surface* — a page the resident can read. The *natural* shape is to
add two more system pages (`privacy` + `conduct`) to the existing seed,
the existing routes, the existing standing, the existing surface — **not**
to invent a new page type, a new route family, a new standing, or a new
surface. The ADR 0040 §2 matrix (the standing) is **frozen**; this lane
rides it unchanged. The `Milestones.cs` / README / `MilestonesTests.cs`
trio is **out of scope** for the ADR (the ship unit U05 owns it).

## Decision

- **D1 — The `privacy` + `conduct` `en` bodies are in
  `EnDefaultPages()` (the ADR 0042 D1 seeder path).** Two new `Page` docs
  (`Kind = System`, the ADR 0040 shape) + two new `PageTranslation` rows
  per page (the ADR 0042 D3 shape). The `LS` test pins that assert the
  exact seeded set move from two to four pages. The `/about` route still
  renders the product-story view (the D1 drift pin, regression-guarded).
  See the handoff note for the full content scope (the two `en` bodies'
  heading, intro, bullet topics, and closing line).
- **D2 — The `/privacy` + `/conduct` routes are in
  `StaticPagesController` (the ADR 0040 §2 route shape).** The two new
  routes resolve the seeded docs on a fresh DB and 404 when absent (the
  D2 floor pin). The hard-coded routes are **en-body by contract** (the
  2026-09-18 U05 record, now the **degradation case** under ADR 0047).
- **D3 — The `de` / `fr` baselines are in `DeDefaultPages()` /
  `FrDefaultPages()` at the ADR 0042 D2 bar.** Two new `PageTranslation`
  rows per page (the ADR 0042 D3 shape), the ADR 0042 D6 parentage pin
  extended to the two new pages.
- **D4 — The footer gains a "Platform" column with six
  `footer.platform.*` registry keys.** Full `EnValues` / `DeValues` /
  `FrValues` parity (the `KwLRegistryConsistencyTests` pin). The footer
  renders the five links unconditionally (the D4 pin). The `/admin` shell
  gains a **"Platform pages"** section listing the five rows with four
  edit links (`/about` preview-only) (the D4 pin).
- **D5 — Content scope.** The two new `en` bodies are **platform-level
  statements, not legal advice**. The ADR fixes the structure (heading,
  intro, bullet topics, closing line); the final copy is U01/U02's at the
  ADR 0042 D2 bar. See the handoff note for the full body specs.
- **D6 — Standing matrix unchanged (ADR 0040 §2).** Create/edit/move/
  delete: GlobalAdmin only. Add a translation: GlobalAdmin ∪ Translator.
  **No** `Moderator` standing on either page (the ADR 0040
  retired-`Moderator` lane stands). **No** matrix change, **no** new
  `AccessAction`, **no** new `AccessVia`, **no** new authorization path.
  The new pages ride the existing `PageService` lanes and the existing
  `PageToAuditableResource` adapter (ADR 0006) unchanged.
- **D7 — Scope.** In: the `privacy` + `conduct` `en` bodies, their
  `de` / `fr` baselines, the two new `PageTranslation` rows per page, the
  `/privacy` + `/conduct` routes, the footer "Platform" column with its
  six `footer.platform.*` keys, the `/admin` "Platform pages" section, and
  the test pins. Out: machine translation (ADR 0005 C), a dedicated
  contact page, a separate cookie policy, additional system pages, and
  the `Milestones.cs` / README / `MilestonesTests.cs` trio (U05 owns it).
  See the handoff note for the full scope list.

## Consequences

- **No schema change, no migration, no new doc type.** The lane reuses
  the existing `Page` / `PageTranslation` / `PageKind` documents (ADR
  0040) and the existing seeder paths (ADR 0042 D1). A fresh instance's
  seed surface is four `Page` docs + eight `PageTranslation` rows (two
  per page).
- **The `LS` test pins move from two to four pages** (terms / help → +
  privacy / conduct), and the new pages each carry a `de` and a `fr`
  `PageTranslation` (the ADR 0042 D6 parentage pin, extended).
- **`/privacy` + `/conduct` resolve the seeded docs on a fresh DB** and
  404 when absent; `/about` still renders the product-story view; the
  footer renders the five links unconditionally; the admin section lists
  the five rows with four edit links.
- **The `Milestones.cs` / README / `MilestonesTests.cs` trio lands at the
  lane's ship unit (U05)**, not in this ADR. `MilestonesTests.cs` pins
  **`M4` as the single `StatusNext`** milestone — an `SP` row added as
  `StatusDone` leaves that pin intact.
- **The `privacy` body is a starting statement, not a legal opinion.**
  It states what the platform *is* and deliberately withholds the
  operator-specific facts. The in-app editor is the review surface.
- **The ADR 0015 hard exclusions hold unchanged:** the footer's new links
  are element content only; the `kw-l` registry gains six
  `footer.platform.*` keys at full parity.
- **Superseded by ADR 0047** (static-page localization + backfill): the
  "en-body by contract" record is now the **degradation case** (an absent
  translation still renders the `en` body); the normal case is the
  localized body (the ADR 0047 D1 decision). See the handoff note for
  the full superseded record.
