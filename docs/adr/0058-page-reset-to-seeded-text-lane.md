# ADR 0058 — Page "Reset to seeded text" lane

**Status:** Accepted
**Date:** 2026-09-12
**Amends:** additive on **0039** (the `Page` + `PageTranslation` docs, the page
standing matrix, the `page.*` audit vocabulary), **0040** (the standing matrix —
reset reuses the **edit standing** verbatim), **0042** / **0043** (the en/de/fr/da
seeded page baselines — `EnDefaultPages` / `GuidePages` and the per-language
registrar methods the reset pulls from), and **0057** (the `GuidePages` registry —
the same code-owned seed text, backfilled on upgrade, that this lane *reverts to*
on demand).

## Context

A fresh instance is seeded by the first-boot seeder (`FirstBootSeeder`) with a
small, closed set of **code-owned** platform pages — the canonical platform
pages (terms, help, privacy, code of conduct, about) and the user guides
(ADR 0057) — in English, plus a `de` / `fr` / `da` baseline for each (the
`De/Fr/DaDefaultPages` and `De/Fr/DaGuidePages` registries). That seeded text is
the **source of truth** for what a page *says* on a brand-new deployment, and it
lives in code, so it can be improved and re-shipped over time as features land.

Once an instance is live, though, the same pages become **admin-editable in
place** (ADR 0039 / 0040 standing matrix; the WYSIWYG editor, ADR 0031/0032/0033).
Two consequences collide:

1. **An admin's local edits and the shipped text diverge.** An operator will
   customise a help page for their own community (their own data-controller
   contact, their own policy wording, a local FAQ). Over time the *shipped*
   baseline improves — a new feature needs documenting, a policy clarifies, a bug
   is fixed and the text should change with it. The in-app copy now carries both
   the admin's custom wording **and** the older, now-stale shipped wording,
   welded together in one body.
2. **There is no supported way to pull in the new shipped text.** The only
   options are (a) hand-edit the body to fold the update in (slow, error-prone,
   and the admin has to *find* what changed), or (b) wipe the row and let the
   seeder recreate it (loses the admin's customisations and the non-`en`
   translation rows that were hand-authored). Neither is good, and neither is
   what an admin actually wants.

The upgrade-time backfill (ADR 0057 `BackfillUserGuidesAsync`, and the analogous
page-translation warm-boot backfill, ADR 0047/0052) is deliberately
**create-if-missing** and **never overwrites** — it must never clobber an admin's
work. That's the right default for *automatic* behaviour. But it means an admin
who wants the *new* shipped text has to do it **by hand, on purpose**, and the
platform gives them no tool to do it.

This lane adds that tool: an explicit, **confirmed**, **destructive** action in
the page edit lane that reverts one page — its English body and its `de`/`fr`/`da`
translation rows — to the current seeded baseline from code. The choice stays
with the admin: keep the customisations, or pull the latest shipped text and
re-customise on top of it.

## Decision

### D1 — A new lane on the existing page edit surface, not a new surface

The reset is a **sibling action to "Save"** in the page edit lane (the edit form
in `Views/Page/_PageForm.cshtml`), gated to edit-lane standing and to pages that
*have* a seeded baseline. It is not a new route surface, a new controller, or a
new doc type — it is one more POST on the existing `PageController`, reusing the
existing `Page` / `PageTranslation` docs, the existing `PageService` seam, and
the existing `IDocumentSession`. This keeps the lane as small as the feature is:
one service method, one controller action, one button, one audit action.

### D2 — The seam: service lane + a pure applier on the seeder

The seam is a two-method addition, split by concern:

- **`FirstBootSeeder.ResetSeededTextAsync(session, page, now, ct)`** — a pure
  applier on the **seeder** (the code that owns the seeded text) that takes the
  already-loaded `Page`, overwrites its `Title`/`Body` with the seeded English
  baseline, and upserts (create-or-overwrite) the `de`/`fr`/`da`
  `PageTranslation` rows from the seeded baselines. The seeder is the single
  source of the seeded text (the `EnDefaultPages` / `GuidePages` / per-language
  registries), so the reset reads **the same** registries the seeder writes on
  first boot — there is exactly one definition of "the seeded text", and the
  reset can never drift from what a fresh instance gets. It throws
  `InvalidOperationException` if the page's slug is not a seeded slug (there is
  nothing to reset *to*).
- **`PageService.ResetToSeededAsync(pageId, actorId, actorRoles, session)`** — the
  **service lane** that owns standing, the 404 contract, the audit row, and the
  single `SaveChangesAsync`. It loads the page (null → `KeyNotFoundException`),
  checks the edit standing (denied → `UnauthorizedAccessException`), delegates the
  text applier to the seeder, writes the audit row in the same session, and saves
  once.

The split is deliberate: the **seeder** owns *what the seeded text is* (it already
does — that is its job), and the **service** owns *the lane's contract* (standing,
audit, save — the C3 invariant: one in-flight session, one save, the audit row in
that same session). No new doc type, no schema change, no new `AccessAction` or
`AccessVia`.

### D3 — Standing: the **edit standing**, verbatim

Reset is a **write** to a page, so it is governed by the **page edit standing**
(ADR 0040 §3.7), exactly as "Save" is:

| Page kind | Reset standing |
|---|---|
| `PageKind.System` (terms / help / privacy / CoC / about + the user guides) | **GlobalAdmin** only |
| `PageKind.User` (blog) | **author ∪ GlobalAdmin** |

This is the same standing the edit lane already enforces (the `CheckEditStanding`
helper), so there is **no new standing branch** — a resident who can edit a page
can reset it, and one who cannot edit it cannot reset it. (A `Moderator` on a
system page, or a `Member` who is not the author of a blog page, is denied, with
**no** audit row written — matching the existing lane's "denied writes produce no
audit" convention.)

### D4 — Scope: English body + the `de`/`fr`/`da` rows, from the seeded baseline

The reset overwrites, **per slug**:

- the page's `en` (source-language) `Title` and `Body` (the `Page` doc itself),
  plus
- the page's `de`, `fr`, and `da` `PageTranslation` rows.

Behaviour per language:

- **A row that exists is overwritten** with the seeded baseline for that
  language (the admin's customised wording for that language is *replaced*).
- **A row that is missing is created** (the seeded baseline fills it in) — so a
  page that was seeded with an `en` body but no non-`en` rows gets all three
  created, matching what a fresh instance ships.
- **Languages the seeded baseline does not carry are untouched.** The reset
  only ever writes the languages the code's seeded registries define for that
  slug; any other `PageTranslation` row on the page (a community-authored `es`
  row, say) is **left alone**.

The reset is **idempotent**: running it twice yields the same page (the baseline
is the baseline), and a second run does not create duplicate rows (the per-row
`Id` is stable — overwrite, don't re-create).

### D5 — Destructive by design, guarded by a confirm + the edit standing

This is the lane's defining property and it is **deliberate**: a reset is a
**destructive write** — the admin's customised English body and their customised
`de`/`fr`/`da` translations are **replaced**, not merged, and there is **no
undo** (the reset is not tracked as a soft-delete; the prior text is simply gone
from the page). Two guards stand in front of it:

- The **edit standing** (D3) — the right people only.
- A **`confirm()`** on the button's form `onsubmit` (the UI guard), whose message
  states plainly that the English body **and** the German / French / Danish
  translations will be overwritten.

The audit row is the only trail — see D6. An admin who is unsure can simply
*not* reset and keep their customisations; the reset is opt-in, per page, and the
default (no reset) is always the safe choice.

### D6 — Audit

One new **audit action** on the existing `page.*` vocabulary:

| action | meaning |
|---|---|
| `page.reset` | a page's `en` body + `de`/`fr`/`da` rows were reset to the seeded baseline (GlobalAdmin on a system page, or the author ∪ GlobalAdmin on a blog page) |

The row is written **in the same session** as the text change (C3), with the
target `page` id and the `Via` standing resolver the edit lane already uses
(`Owner` for the author of a blog page, `Admin` for a GlobalAdmin). A **denied**
reset (standing, or a non-seeded slug) writes **no** audit row — the write never
happens, and a no-op is not an auditable event.

### D7 — Web surface

- **Route:** `POST /pages/{id:guid}/reset-seeded` on the existing `PageController`,
  `[Authorize]` + `[ValidateAntiForgeryToken]`. It loads a lightweight session,
  calls `PageService.ResetToSeededAsync`, and redirects back to the edit page
  with a `TempData["info"]` confirmation.
- **Mapping:** `UnauthorizedAccessException` → `403`, `KeyNotFoundException` →
  `404`, `InvalidOperationException` (non-seeded slug) → **`400`** with the
  exception message added to the model state (a hand-crafted POST to a page that
  has no seeded baseline is a *bad request*, not a silent success — there is
  nothing to reset to).
- **Button:** a single outline-danger "Reset to seeded text" form button in the
  page edit form's action bar, rendered **only when** the form is the edit view
  (`isEdit`) **and** `Model.CanReset` is true. It carries the `confirm()` guard
  (D5) and an anti-forgery token.
- **`CanReset`:** a `[BindNever]` view-model flag set by the Edit GET to
  `FirstBootSeeder.HasSeededText(page.Slug)` — a **pure registry probe** (does
  this slug exist in the seeded en/guide registries?) that needs no session. A
  page with no seeded baseline (a hand-authored blog page, or a slug the seed
  never ships) simply hides the button — there is nothing to reset *to*. (The
  standing itself is enforced by the lane, D3; `CanReset` only gates the
  *availability*, not the *permission*.)
- **Localisation:** the button label uses the existing `kw-l` key
  (`pages.resetSeeded`), added to the en/de/fr/da `KnownTranslationKeys`
  registry so it round-trips through the admin language catalog like every
  other UI string.

### D8 — No new doc, schema, role, or failure shape

The lane adds: **one** service method, **one** pure applier on the seeder, **one**
controller action, **one** `[BindNever]` view-model flag, **one** button, **one**
audit action (`page.reset`), and **one** kw-l key. It adds **no** new doc type,
**no** schema change, **no** new `AccessAction` / `AccessVia`, **no** new role,
**no** new failure shape (it reuses the existing `KeyNotFoundException` /
`UnauthorizedAccessException` / `InvalidOperationException` → 404/403/400
mapping), and **no** new route beyond the one POST. The reset reads the **same**
seeded registries the seeder already exposes as `public static` — the seeder is
the single source of the seeded text, and the reset is a new *caller* of it, not
a second copy of it.

## Consequences

**Positive.**

- An admin can pull the **latest shipped** seeded text for any seeded page in one
  click — the exact content a fresh instance would have — then re-customise on top
  of it. This is the "update the help page to match the new feature" workflow
  the feature request asked for, and it is the *supported* way to do it.
- The **choice stays with the admin**: keep the customisations (don't reset) or
  reset and re-customise. Both are first-class; the default is non-destructive.
- The reset is **guaranteed correct**: it reads the same registries the seeder
  writes, so the reset text is byte-identical to what a fresh instance ships —
  no drift, no transcription, no "which version is the seeder using now?"
- It is **small and auditable**: one `page.reset` row per reset, in the same
  session as the change, with the standing `Via` the edit lane already emits.

**Neutral.**

- A **non-seeded** slug (a hand-authored blog page) has nothing to reset to —
  the button is hidden (`CanReset` is false), and a hand-crafted POST is a 400
  form error. That is the correct, honest behaviour for "there is no seeded
  baseline for this page".
- A reset **replaces** the admin's customised `de`/`fr`/`da` rows (D5). An admin
  who has hand-translated a page into those languages and then resets it loses
  those translations in favour of the seeded baselines — the `confirm()` message
  states this plainly, and the admin can simply not reset.
- The reset is **idempotent** (D4): a second run is a no-op (same baseline, no
  duplicate rows), so it is safe to click twice if the first felt "off".

**Deferred (own lanes).**

- Resetting a page's **non-`en`, non-seeded** community translations — out of
  scope by design (D4: only the seeded languages are written; the reset is
  "revert to the *shipped* baseline", not "wipe translations").
- A **"restore my custom text"** undo — deliberately absent (D5): the reset is
  destructive and the audit row is the only trail. If an admin wants the old
  text back, it has to come from their own copy; that is the honest trade-off of
  a destructive re-seed.
- Resetting the **language catalog** or **UI-string** baselines to the code-owned
  seed — the same *kind* of lane could be built on the ADR 0042/0052 warm-boot
  backfill registries, but that is a different surface and its own ADR.

## Follow-on

None — the lane is self-contained. (The deferred items above are *separate*
lanes, not follow-ons: they each settle their own design question and get their
own ADR.)

## References

- **ADR 0039** — the `Page` / `PageTranslation` docs, the page standing matrix,
  the `page.*` audit vocabulary (the lane this builds on).
- **ADR 0040** — the page standing matrix (D3 reuses its edit standing verbatim).
- **ADR 0042 / 0043** — the en/de/fr/da seeded page baselines (the `de`/`fr`/`da`
  rows the reset writes come from these registries).
- **ADR 0057** — the `GuidePages` registry (the user guides the reset can revert;
  the same code-owned seed text, backfilled on upgrade, that this lane reverts
  to on demand).
- **ADR 0047 / 0052** — the warm-boot page-translation / UI-string backfill lanes
  (the *create-if-missing, never-overwrite* default this lane is a deliberate,
  confirmed *exception* to).
- **Code:** `FirstBootSeeder.ResetSeededTextAsync` + `FirstBootSeeder.HasSeededText`
  (the applier + the pure probe), `PageService.ResetToSeededAsync` (the service
  lane), `IPageService.ResetToSeededAsync` (the seam contract),
  `PageController.ResetToSeeded` (the route), `PageComposeViewModel.CanReset`
  (the `[BindNever]` flag), `Views/Page/_PageForm.cshtml` (the button),
  `KnownTranslationKeys` `pages.resetSeeded` (the UI string).
- **Tests:** `PageServiceTests` `PG6_…` suite (the four-surface overwrite, the
  create-absent-row case, the non-seeded-slug 400, the standing matrix,
  the blog-author standing, idempotence, the missing-page 404, and the
  `HasSeededText` probe).
