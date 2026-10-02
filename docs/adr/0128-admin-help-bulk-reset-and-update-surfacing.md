# ADR 0128 — Admin bulk help-page reset and "newer shipped text" surfacing

**Status:** Accepted
**Date:** 2026-09-12
**Amends:** additive on **0058** (the per-page "reset to seeded text" lane — this
lane adds the *bulk* variant and a *read-only* "which pages have newer shipped
text" probe on top of the exact same seeded registries and reset applier),
**0057** (the `GuidePages` registry the seeded set is drawn from), **0042** /
**0043** (the en/de/fr/da seeded page baselines), and **0062** (the admin
surface split this new `/admin/help` surface joins).

## Context

ADR 0058 gave the platform a **per-page** "reset to seeded text" action: an admin
reverts *one* page's English body and its `de`/`fr`/`da` translation rows to the
code-owned seeded baseline. That is the right primitive, but it leaves two gaps
for the *bulk* case, which is the workflow most admins will actually hit:

1. **Most users leave the default help text alone.** The per-page reset button
   lives deep in each page's editor. An operator who wants "just make the whole
   help section match what the code ships now" has to open each seeded page and
   click Reset one at a time — tedious, and it hides the *set* of pages that are
   even affected.
2. **An admin has no way to see *which* pages have diverged from the shipped
   text.** After a release that improves the seeded help wording, the operator
   has to know *by hand* which pages changed. There is no surfaced signal.

The tempting fix for (2) is a **silent auto-apply**: at boot, detect that the
shipped baseline is newer and overwrite the stored pages. That is rejected here —
it is the same anti-pattern ADR 0058 D5 guards against (a destructive write with
no operator intent), and it violates the ADR 0042 D1 rule that a *human editor is
the only writer* of a non-`en` body. An admin who customised a help page for
their community would have that work silently clobbered on the next deploy.

So the shape this ADR takes is **"updates available, admin applies"**: surface a
read-only list of every seeded page, each flagged when the code carries newer
text than the DB holds, and offer a per-page Reset **and** a "Reset all" action —
both explicit, both destructive, both audited, both GlobalAdmin-only.

## Decision

### D1 — A new admin surface `/admin/help`, not a new page in the editor

A new `AdminHelpController` (route `admin/help`,
`[Authorize(Roles = Roles.GlobalAdmin)]`) with one GET and two POSTs. It is a
sibling to `AdminSignupController` / `AdminTimezoneController` and the other
`/admin/*` surfaces (ADR 0062), and it is deliberately a **dedicated controller**:
the `AdminController`'s constructor is pinned by the Web test harnesses, so a new
seam goes on its own controller — the established convention.

- **`GET /admin/help`** — lists every seeded page with a "newer shipped text"
  flag (read-only; D2). No writes, no audit row.
- **`POST /admin/help/reset`** — resets *one* seeded page (delegates to the
  existing ADR 0058 `PageService.ResetToSeededAsync` lane verbatim).
- **`POST /admin/help/reset-all`** — resets **every** seeded page (the new bulk
  lane, D3).

Both POSTs are `[ValidateAntiForgeryToken]` and each button sits behind an
explicit `confirm()` (D5).

### D2 — The "newer shipped text" probe: read-only, computed on visit, no schema

`PageService.GetSeededPageStatusAsync()` is a **read-lane** (opens
`_store.QuerySession()`, writes nothing, no audit row). It:

- enumerates the seeded slug set (`FirstBootSeeder.AllSeededSlugs()` — a pure
  registry read over `EnDefaultPages` + `GuidePages`, in seed order; no DB);
- loads the matching `Page` docs and their `PageTranslation` rows in one pass;
- compares the stored text (the `en` title/body, plus the `de`/`fr`/`da` rows the
  seeded baseline *carries*) against the code baseline.

A page is flagged `HasNewerShippedText` when **any** of these holds:

- the stored `en` title or body differs from the seeded `en` baseline, or
- a `de`/`fr`/`da` row the baseline defines is **missing** on the stored page, or
- a `de`/`fr`/`da` row the baseline defines **differs** from the baseline.

Crucially it does **not** flag languages the baseline does not carry (the ADR 0058
D4 skip rule): a community-authored `es` row, or any `de`/`fr`/`da` row for a
slug whose baseline has no such row, is not a divergence — it is custom content
the reset would never touch, and it must not read as "newer shipped text".

This is a **display probe**, not a decision: no doc type, no schema, no boot
hook, no projection. It is recomputed on each visit, so it is always honest about
the gap between the DB and the current code.

### D3 — The bulk lane: one audited write per page, one `SaveChangesAsync`

`PageService.ResetAllSeededPagesAsync(actorId, actorRoles, session)` is a
**write-lane** on the caller's session. It:

- re-checks **GlobalAdmin** standing (denied → `UnauthorizedAccessException` —
  the `[Authorize]` attribute is the first guard, this is the second);
- loads every seeded `Page`;
- delegates each reset to the **same** ADR 0058 applier
  (`FirstBootSeeder.ResetSeededTextAsync`) — so the bulk reset writes byte-for-byte
  the same text a per-page reset would;
- writes one `AccessAudit` row per page (`Action = "page.reset"`,
  `TargetKind = "page"`, `Via = Admin`, `Outcome = Allow`) in the same session;
- issues a **single** `SaveChangesAsync` and returns the count of pages reset.

There is **no** new audit action and **no** new failure shape: it reuses the
existing `page.reset` vocabulary and the existing
`UnauthorizedAccessException` / `InvalidOperationException` contracts.

### D4 — Standing: GlobalAdmin, verbatim (the stricter of the two lanes)

The per-page ADR 0058 lane allows **author ∪ GlobalAdmin** on a *blog* page. But
this bulk surface only ever operates on **seeded** pages, and every seeded page
is a `PageKind.System` page (platform pages + the user guides) — none of them is
a blog page. So the effective standing for every reset reachable from
`/admin/help` is **GlobalAdmin** (the `CheckEditStanding` helper still applies on
the per-page path; the bulk path checks GlobalAdmin directly). A community
Moderator has no standing here, matching ADR 0058 D3 (reset is a
platform-level operation).

### D5 — Destructive by design, opt-in, the default is non-destructive

This is the same defining property as ADR 0058 D5, applied to the bulk case. A
reset replaces hand-edited text and there is **no undo** (the prior text is gone
from the page; the audit row is the only trail). The guards are:

- **GlobalAdmin** standing (D4) — the right people only.
- **`confirm()`** on both forms, whose messages state plainly that hand-edited
  copy (the English body **and** the `de`/`fr`/`da` rows) is overwritten.
- **The admin must apply it.** There is no auto-apply. An admin who is unsure
  simply does not click; the reset is opt-in and the default (no reset) is always
  the safe choice.

### D6 — Web surface + localisation

- **Route:** `GET /admin/help`, `POST /admin/help/reset`, `POST /admin/help/reset-all`.
- **Link:** a `/admin/help` entry added to the Platform section list-group in
  `Views/Admin/Platform.cshtml`, alongside the other `/admin/*` surfaces.
- **View:** `Views/Admin/Help.cshtml` — a table of seeded pages (title, slug, an
  "Up to date" / "Newer text shipped" badge, a per-page Reset button) plus a
  single prominent "Reset all" action card. A warning banner names how many pages
  have newer shipped text, so the admin sees the affected *set* at a glance.
- **Localisation:** the admin shell is **plain English by local convention**
  (the `Platform.cshtml` list-group copy is deliberately non-`kw-l`, because an
  interpolated `kw-l` key there would break the `KwLRegistryConsistencyTests`
  static key scan). `/admin/help` follows that convention — no new `kw-l` keys.
- **Confirmations:** `TempData["info"]` (and `TempData["error"]` for a non-seeded
  slug on the per-page path) rendered by the shared `_FlashToast` partial, as with
  every other admin write lane.

### D7 — No new doc, schema, role, or failure shape

The lane adds: **one** admin controller (3 actions), **one** view, **one**
list-group link, **two** `PageService` methods (`GetSeededPageStatusAsync`,
`ResetAllSeededPagesAsync`), **one** `FirstBootSeeder.AllSeededSlugs()` registry
read, and **one** `SeededPageStatus` record. It reuses the ADR 0058
`ResetToSeededAsync` / `ResetSeededTextAsync` appliers verbatim. It adds **no**
new doc type, **no** schema change, **no** new `AccessAction` / `AccessVia`,
**no** new role, **no** new failure shape, and **no** new `kw-l` key.

## Consequences

**Positive.**

- An operator who wants the whole help section to match the current shipped text
  does it in **one click** (Reset all), instead of opening each page and clicking
  Reset — the workflow most users (who keep the defaults) will actually use.
- An admin can **see which pages diverged** from the shipped text (the
  "newer shipped text" badge + the affected-count banner) without knowing by hand
  what a release changed.
- The **choice stays with the admin**: keep the customisations (don't reset) or
  reset and re-customise. Both are first-class; the default is non-destructive.
- The bulk reset is **guaranteed correct** — it calls the exact same applier the
  per-page lane uses, so the result is byte-identical to resetting each page by
  hand.
- It is **small and auditable**: one `page.reset` row per page, in the same
  session as the change, `Via = Admin`.

**Neutral.**

- The probe is a **display** comparison — it flags *divergence from the shipped
  baseline*, which on a customised page is exactly the admin's own edits. That is
  the honest reading: the badge means "this page does not currently match the
  code baseline", which is what an admin needs to decide a reset. It is not
  asserting which is "better".
- A bulk reset **replaces** the admin's hand-edited `de`/`fr`/`da` rows, exactly
  as a per-page reset does (ADR 0058 D5). The `confirm()` on "Reset all" states
  this plainly across the whole set.
- The probe recomputes on every visit (a small read over a small, closed set of
  pages). That is the cost of "no schema, no cache, no boot hook" — acceptable at
  this scale, and it keeps the signal always current.

**Deferred (own lanes).**

- A **"restore my custom text"** undo — deliberately absent (D5), as in ADR 0058:
  the reset is destructive and the audit row is the only trail.
- Resetting the **language catalog** or **UI-string** baselines to the code-owned
  seed — a different surface, its own ADR.

## Follow-on

None — the lane is self-contained.

## References

- **ADR 0058** — the per-page "reset to seeded text" lane this lane builds on
  (the same applier, the same standing matrix, the same `page.reset` audit).
- **ADR 0057** — the `GuidePages` registry (part of the seeded slug set).
- **ADR 0042 / 0043** — the en/de/fr/da seeded page baselines.
- **ADR 0062** — the admin surface split this `/admin/help` surface joins.
