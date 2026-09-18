# ADR 0043 — System pages shipped + discoverable (SP)

Status: Accepted
Date: 2026-09-18
Amends: **0005 §B** (the "static-page coverage on a fresh instance" scope —
now the **five-surface set** — About view, Terms, Help, Privacy, Conduct —
not the three routes; the amendment note lives in ADR 0005, cross-referenced
below). Builds on the frozen base of **0042** (the ownership semantics this
ADR re-states for two new pages), **0040** (the `PageKind` model, the
`system/` root, and the §2 standing matrix the new pages inherit), **0015**
(the `<kw-l>` TagHelper, the `KnownTranslationKeys` registry, the provider
floor, the hard exclusions the new footer copy rides on), **0003** (the
report-gated, audited moderation lane the `conduct` body points at), and
**0021** (the GlobalAdmin ∪ Translator translation standing the baselines
hand off to). No amendment to 0042, 0040, 0015, 0003, or 0021 — this ADR is
additive on their decisions exactly as written.

This ADR is the **sign-off gate** of the `SP` (System Pages shipped) lane —
the lane plan is `plans-milestones/system-pages/plan-system-pages.md`.
Every later `SP` unit (U01–U05) codes against the *locked* text here;
changing a decision below requires an amendment, not a unit-level override.

## Context

A fresh instance today serves **three** static-page routes. Two of them read
seeded `Page` docs — `system/terms` and `system/help` (each an `en` body
plus `de` / `fr` `PageTranslation` rows, the ADR 0042 D1 / D2 shape) — and
one, `/about`, **degrades to the product-story view** because *no `about`
page is seeded* (the ADR 0039 U05 drift pin, carried into ADR 0040 and the
`LS` lane: `about` is a registry-key surface, the `about.*` keys in
`KnownTranslationKeys`, not a Markdown body).

But **nothing links any of them.** The footer has no platform column (the
`_Layout` footer is the brand block, the "Community" column, the "The
project" column, and a "Good to know" value statement — none of which is a
navigation surface for legal/policy content); the navbar has no entry; and
the only reach is the `/pages` tree browse, which a resident does not know
exists. An admin finds the pages only by knowing the tree; a resident finds
them only by guessing a URL. And two surfaces any defensible self-hosted
deployment needs — a **Privacy Policy** and a **Code of Conduct** — **do not
exist at all** (no route, no `Page` doc, no seed entry).

This lane closes both gaps in one pass. It is a **data + routes +
discoverability** lane, not a schema lane: it seeds the two missing pages
(in three languages, the `en` bodies derived from this instance's own
`SECURITY.md` privacy model and the ADR 0040 standing conventions), adds the
two missing routes, and makes all five surfaces **reachable** — an
unconditional footer "Platform" column for every visitor, and a "Platform
pages" affordance on the `/admin` shell for the edit lane. English stays the
source language (ADR 0042 D4, unchanged); the `du` / `tu` register of ADR
0042 D2 is held for the new copy.

**What this is, in one line:** a first-boot instance ships the full
platform-page set (**About**, **Terms**, **Help**, **Privacy Policy**,
**Code of Conduct**) with complete baselines in all three languages, and the
pages are **reachable** from the normal UI.

## Decision

### D1 — The five-surface set (four `Page` docs + the `about` view)

The shipped set is **exactly five surfaces**, rendered at five hard-coded
routes, on a fresh instance:

- **`/terms`**, **`/help`**, **`/privacy`**, **`/conduct`** — four seeded
  `Page` docs, each `Kind = System` (ADR 0040), under the `system` root,
  `Audience = null` (public), `AuthorId` empty (platform content, no
  resident author — the ADR 0040 §2 standing matrix and the ADR 0042 D1
  ownership semantics apply).
- **`/about`** — the product-story **view** (`Views/StaticPages/About`,
  driven by `HomeViewModel`), **not** a `Page` doc.

**The `LS` U05 drift pin is carried forward, explicitly:**

- **No `about` slug ever enters** `EnDefaultPages()` / `DeDefaultPages()` /
  `FrDefaultPages()`. No `about` `Page` is seeded, on a pristine DB or
  otherwise.
- **`/about` renders the product-story view on a fresh instance** (the
  `fallBackToProductStory` branch of `StaticPagesController`), and an
  admin-created `system/about` `Page` **wins when present** (the existing
  `StaticPagesController` behavior, unchanged by this lane).
- The `about` surface's translatable strings remain the `about.*` registry
  keys (ADR 0042 D3 / D5), not a Markdown body.

No more, no fewer, on first boot. A GlobalAdmin may still add **further**
`system/` pages at runtime (the ADR 0040 §2 design — an admin authors under
the `system` namespace); that is the standing capability this lane does not
touch, and it is *not* part of the five-surface shipped set.

### D2 — Route contract (`/privacy` + `/conduct` join the hard-coded set)

- `/privacy` and `/conduct` join the hard-coded static routes in
  `StaticPagesController`. The `Slugs` allow-list (`{ "terms", "help", "about" }`
  today) gains `"privacy"` + `"conduct"` — the **route-spoofing guard**
  (`Slugs.Contains`) remains the single allow-list; a slug not in it is a
  404.
- Both routes are **404-floor** routes: `Page(slug)` with the *page floor*,
  **no product-story fallback seam**. The `fallBackToProductStory` seam is
  **`about`'s and stays `about`'s** (D1). A truly-absent `/privacy` or
  `/conduct` is a clean 404, exactly as a truly-absent `/terms` or `/help`
  is today.
- The `system/{slug}` primary + bare-`{slug}` legacy fallback resolution
  (ADR 0040's re-parent story) is **unchanged**: the canonical pages live at
  `system/privacy` / `system/conduct`, and the bare-slug fallback keeps a
  legacy seam for pre-ADR-0040 instances.

### D3 — Ownership semantics (re-state ADR 0042 D1 for the two new pages)

The two new pages inherit ADR 0042 D1 exactly (cross-referenced, not
re-derived):

- **`en` bodies are code-owned, forever.** The `en` body of `privacy` and
  `conduct` lives in `EnDefaultPages()` (the single source, per the ADR
  0042 / `LS` U04 pattern). The seeder's **existing** "code wins" upsert
  branch (`SeedDefaultPagesAsync`) refreshes them on a pristine DB; on a
  warm instance the `en` body is governed by the code-wins / provider-floor
  path unchanged. **No new seeder branch** is added for this lane.
- **`de` / `fr` bodies are seeded-once, then community-owned.** Their
  `PageTranslation` rows (one `de`, one `fr`, per page) ship as **initial
  values** from `DeDefaultPages()` / `FrDefaultPages()`, materialized by the
  **existing** `SeedPageTranslationsAsync` loop (which is *generic over the
  baseline arrays* — the new entries flow from the new array entries, no
  loop change). They are **create-if-missing, never refreshed** (the ADR
  0042 D1 / `LS` U04 shape), attached to the **page's own `Id`** (the read
  path `IPageService.GetTranslationsAsync(page.Id)` queries by the page's
  own id — the ADR 0042 D6 distinction, not the `system` root's).
- **An admin edit is never overwritten.** After first boot, the in-app
  editor (GlobalAdmin ∪ Translator, ADR 0021) is the **only** write path
  for the `de` / `fr` rows and for any `en` body; the seeder's `de` / `fr`
  upserts are first-boot-only *by construction* (the `IsPristineAsync`
  gate), and the `en` code-wins branch refreshes the `en` body in place on
  a warm re-seed (the ADR 0042 D1 asymmetry, unchanged).

### D4 — Discoverability (footer column + admin affordance; navbar untouched)

- **An unconditional footer "Platform" column** links all **five** routes —
  `/about`, `/terms`, `/help`, `/privacy`, `/conduct` — for **every**
  visitor, rendered **unconditionally** in `_Layout.cshtml`. The footer is
  the canonical navigation surface; a link whose page is absent from the
  tree lands on the page's **existing floor** (floor-honesty — no
  existence probe, no conditional hiding): `/about` on the product-story
  view, the other four on the 404 page floor. On a fresh instance all five
  links resolve.
- **The navbar is deliberately untouched** — *recorded as a decision, not a
  bug to fix later.* Rationale: these are low-frequency legal/policy
  surfaces; the footer is their home; the `/pages` tree remains the browse
  surface. Adding navbar entries would crowd the primary navigation with
  content residents visit rarely. This decision is locked here so a later
  unit does not "fix" it by adding navbar links.
- **A "Platform pages" section on the `/admin` shell** (`Views/Admin/Index`)
  lists the five surfaces. Each row carries a **preview** link (the route);
  the **four** `Page`-backed surfaces (`terms` / `help` / `privacy` /
  `conduct`) additionally carry an **edit** link (the id-based
  `/pages/{id}/edit` route — the `AdminController` resolves the seeded
  slugs to page ids at render time, `GetByPathAsync("system/{slug}")`,
  absence-tolerant: a missing slug renders preview-only). **`/about` is
  preview-only** — it is a view, not a page (D1), so it has no edit link.
- **The `kw-l` hard exclusions hold (ADR 0015):** the footer links are
  element *content* only — new `footer.platform.*` registry keys in
  `EnValues` / `DeValues` / `FrValues` (full registry parity pinned by the
  existing `KwLRegistryConsistencyTests` family); HTML attributes and JS
  strings stay hardcoded.

### D5 — Content scope (what the two new bodies say)

The two new `en` bodies are **platform-level statements, not legal
advice**. They are the **shape contract** for U01 (`en`) and U02 (`de` /
`fr`): the ADR fixes the structure (heading, intro, the bullet topics, the
closing line) and the topics each bullet covers; the final copy is
U01 / U02's, at the ADR 0042 D2 bar (idiomatic, register-held `du` / `tu`,
token-for-token, structure preserved exactly).

**`privacy` — a platform-level statement derived from `SECURITY.md`:**

- **Heading:** "Privacy" (page title).
- **Intro:** this is a self-hosted platform for **one** neighborhood — the
  operator runs the instance, and the operator is the **data controller**
  for everything on it.
- **Bullet topics:**
  - **What the platform stores:** the neighborhood's accounts, posts,
    groups, pages, and media — the database (Marten documents) plus the
    media volume. What we don't store, we can't leak (SECURITY.md §1, rule
    5).
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

### D6 — Standing matrix unchanged (ADR 0040 §2)

The two new pages are `Kind = System` and **inherit the ADR 0040 §2 matrix
exactly**:

- **Create / edit / move / delete:** GlobalAdmin only.
- **Add a translation:** GlobalAdmin ∪ Translator.
- **No Moderator standing** on either page (the ADR 0040 retired-Moderator
  lane stands; the `AccessVia.Moderator` tag is never written for a page).

**No matrix change, no new `AccessAction`, no new `AccessVia`, no new
authorization path.** The new pages ride the existing `PageService`
lanes and the existing `PageToAuditableResource` adapter (ADR 0006)
unchanged; nothing in this lane touches the `CanAsync` / `CanSeeAsync`
signatures (ADR 0006 frozen base).

### D7 — Scope

**In (this lane):** the `privacy` + `conduct` `en` bodies in
`EnDefaultPages()` (D5); their `de` / `fr` baselines in `DeDefaultPages()`
/ `FrDefaultPages()` at the ADR 0042 D2 bar (D5, D3); the two new
`PageTranslation` rows per page (D3); the `/privacy` + `/conduct` routes in
`StaticPagesController` (D2); the footer "Platform" column with its six
`footer.platform.*` registry keys in `EnValues` / `DeValues` / `FrValues`
(D4); the `/admin` shell "Platform pages" section (D4); and the test pins
(the Core parity / seed family moving 2 → 4 pages; the Web surface pins for
the routes, the floor, the footer, and the admin section).

**Out (→ future lanes):** machine translation of anything (ADR 0005 C — a
deferred trust boundary, unchanged); a dedicated **contact** page (the
`help` body names the operator's `SupportEmail` as the concern — the design
already covers it); a separate **cookie** policy (folded into the `privacy`
body, D5); **additional** system pages (a GlobalAdmin adds them under
`system/` at runtime — the ADR 0040 §2 design, not this lane's shipped set);
and the `Milestones.cs` / README / `MilestonesTests` trio **until the lane
ships** (U05 owns it — the Consequences below).

## Consequences

- **No schema change, no migration, no new doc type.** The lane reuses the
  existing `Page` / `PageTranslation` / `PageKind` documents (ADR 0040) and
  the existing `SeedDefaultPagesAsync` / `SeedPageTranslationsAsync` seeder
  paths (ADR 0042 D1) — the new content flows from two new array entries and
  two new routes. A fresh instance's seed surface is four `Page` docs
  (terms / help / privacy / conduct) + eight `PageTranslation` rows (two per
  page), not two + four.
- **The `LS` test pins that assert the exact seeded set move from two to
  four pages** (terms / help → + privacy / conduct), and the new pages each
  carry a `de` and a `fr` `PageTranslation` on the page's own `Id` (the
  ADR 0042 D6 parentage pin, extended to the two new pages).
- **`/privacy` + `/conduct` resolve the seeded docs on a fresh DB** and 404
  when absent (the D2 floor pin); `/about` still renders the product-story
  view (the D1 drift pin, regression-guarded); the footer renders the five
  links unconditionally (the D4 pin); the admin section lists the five rows
  with four edit links (`/about` preview-only) (the D4 pin).
- **The `Milestones.cs` / README / `MilestonesTests` trio lands at the lane's
  ship unit (U05),** not in this ADR. `MilestonesTests.cs` pins **`M4` as
  the single `StatusNext`** milestone — an `SP` row added as `StatusDone`
  (after the `LS` row) **leaves that pin intact**; the ordered-`Ids` list
  and the shipped-done list gain `"SP"` after `"LS"`.
- **Cost — the `privacy` body is a starting statement, not a legal opinion.**
  It states what the platform *is* (self-hosted, single-neighborhood,
  operator-as-controller, the one cookie) and deliberately withholds the
  operator-specific facts. Accepted: the in-app editor is the review surface
  and the operator is expected to complete it — the D3 ownership semantics
  make that safe (an admin edit is never overwritten).
- **The ADR 0015 hard exclusions hold unchanged:** the footer's new links
  are element content only; the `kw-l` registry gains six `footer.platform.*`
  keys at full `EnValues` / `DeValues` / `FrValues` parity (the
  `KwLRegistryConsistencyTests` pin holds).
- **2026-09-18 (U05) — the hard-coded routes are en-body by contract.** A
  reader of D2 should not expect translated bodies on the hard-coded routes:
  under a `de` / `fr` preference, `/privacy` / `/terms` / `/help` / `/conduct`
  render the **`en` body with no `td-variant` chip-swap containers** (nav /
  footer / "last updated" localize, but the body stays `en`), because
  `StaticPageViewModel` carries no translations and reads no cookie — exactly
  as designed. The ADR 0027 chip-swap is a **Show-surface
  (`/pages/{path}`) mechanic**, not a property of the hard-coded routes.

## Revisit when

- A **contact page** or a **separate cookie policy** is wanted — decide it
  as a *new* lane (the D5 / D7 scope-out note is the standing position).
- A **third-party cookie** is introduced (a future integration lane) — the
  D5 "no third-party cookies exist or are planned" record must be amended
  and SECURITY.md §2 / §5 re-reviewed (the ADR 0005 C trust-boundary
  precedent).
- The community **adds further `system/` pages at runtime** and wants them
  in the footer column — the D4 "exactly five" record is the standing
  position; extend it by amendment rather than by a unit-level override.
