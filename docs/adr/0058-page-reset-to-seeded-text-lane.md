---
id: adr-0058
title: "Page \"Reset to seeded text\" lane"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/archive/lanes/improve/improve-audit.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0058](0058-page-reset-to-seeded-text-lane.md) — Page "Reset to seeded text" lane

**Status:** Accepted
**Date:** 2026-09-12
**Amends:** additive on **0039** (the `Page` + `PageTranslation` docs, the
page standing matrix, the `page.*` audit vocabulary), **0040** (the
standing matrix — reset reuses the **edit standing** verbatim),
**0042** / **0043** (the en/de/fr/da seeded page baselines the reset
pulls from), and **0057** (the `GuidePages` registry — the same code-owned
seed text, backfilled on upgrade, that this lane *reverts to* on demand).

> **Abstract:** an explicit, **confirmed**, **destructive** action in the
> page edit lane that reverts one page — its English body and its
> `de`/`fr`/`da` translation rows — to the current seeded baseline from
> code. The choice stays with the admin: keep the customisations, or pull
> the latest shipped text and re-customise on top of it. It is a **sibling
> action to "Save"** in the page edit lane (not a new route surface,
> controller, or doc type), gated to the **edit standing** (verbatim),
> with a `confirm()` UI guard + a single `page.reset` audit row. The seam
> is a **pure applier on the seeder** (`ResetSeededTextAsync` — the seeder
> is the single source of the seeded text, so the reset reads **the same**
> registries the seeder writes on first boot) + a **service lane**
> (`PageService.ResetToSeededAsync` — standing, the 404 contract, the audit
> row, the single `SaveChangesAsync`). **No** new doc type, **no** schema
> change, **no** new `AccessAction` / `AccessVia`. **For** a developer
> touching the `FirstBootSeeder` applier, the `PageService` reset seam,
> the `PageController.ResetToSeeded` route, or the `tests/*` page-reset
> family. The seam split, the per-language behaviour, the Web surface, and
> the follow-on / references are in the [handoff
> note](../design/adr-handoffs.md#adr-0058--page-reset-to-seeded-text-lane-moved-detail).

## Context

A fresh instance is seeded by the first-boot seeder (`FirstBootSeeder`)
with a small, closed set of **code-owned** platform pages (terms, help,
privacy, code of conduct, about) and the user guides (ADR [0057](0057-user-guides-in-help-subtree-and-consistency-loop.md)) — in
English, plus a `de` / `fr` / `da` baseline for each. That seeded text is
the **source of truth** for what a page *says* on a brand-new deployment,
and it lives in code, so it can be improved and re-shipped over time as
features land.

Once an instance is live, though, the same pages become **admin-editable in
place** (ADR [0039](0039-pages-hierarchy-audience-translations.md) / 0040 standing matrix; the WYSIWYG editor). Two
consequences collide: (1) **An admin's local edits and the shipped text
diverge** — the in-app copy now carries both the admin's custom wording
**and** the older, now-stale shipped wording, welded together in one body.
(2) **There is no supported way to pull in the new shipped text** — the
only options are (a) hand-edit the body to fold the update in, or (b) wipe
the row and let the seeder recreate it (loses the admin's customisations
and the hand-authored non-`en` translation rows). Neither is good, and
neither is what an admin actually wants.

The upgrade-time backfill (ADR [0057](0057-user-guides-in-help-subtree-and-consistency-loop.md) `BackfillUserGuidesAsync`, and the
analogous page-translation warm-boot backfill, ADR [0047](0047-static-page-localization-and-backfill.md)/0052) is
deliberately **create-if-missing** and **never overwrites** — it must never
clobber an admin's work. That's the right default for *automatic*
behaviour. But it means an admin who wants the *new* shipped text has to do
it **by hand, on purpose**, and the platform gives them no tool to do it.
This lane adds that tool.

## Decision

- **D1 — A new lane on the existing page edit surface, not a new surface.**
  The reset is a **sibling action to "Save"** in the page edit lane (the
  edit form in `Views/Page/_PageForm.cshtml`), gated to edit-lane standing
  and to pages that *have* a seeded baseline. One service method, one
  controller action, one button, one audit action.
- **D2 — The seam: service lane + a pure applier on the seeder.**
  `FirstBootSeeder.ResetSeededTextAsync(session, page, now, ct)` (the pure
  applier — overwrites the `Title`/`Body` with the seeded English baseline
  and upserts the `de`/`fr`/`da` `PageTranslation` rows from the seeded
  baselines; throws `InvalidOperationException` if the slug is not a
  seeded slug) + `PageService.ResetToSeededAsync(pageId, actorId,
  actorRoles, session)` (the service lane — standing, the 404 contract, the
  audit row, the single `SaveChangesAsync`). The split is deliberate: the
  **seeder** owns *what the seeded text is*; the **service** owns *the
  lane's contract* (standing, audit, save). **No** new doc type, **no**
  schema change, **no** new `AccessAction` or `AccessVia`. See the handoff
  note for the full seam split.
- **D3 — Standing: the **edit standing**, verbatim.** Reset is a
  **write** to a page, so it is governed by the **page edit standing**
  (ADR [0040](0040-pages-system-and-user-kinds-and-blog-feed.md) §3.7), exactly as "Save" is: `PageKind.System` →
  **GlobalAdmin** only; `PageKind.User` (blog) → **author ∪ GlobalAdmin**.
  This is the same standing the edit lane already enforces (the
  `CheckEditStanding` helper), so there is **no new standing branch** — a
  resident who can edit a page can reset it, and one who cannot edit it
  cannot reset it. A **denied** reset writes **no** audit row.
- **D4 — Scope: English body + the `de`/`fr`/`da` rows, from the seeded
  baseline.** Per slug: the page's `en` `Title` and `Body` (the `Page` doc
  itself) + the page's `de`, `fr`, and `da` `PageTranslation` rows. A row
  that exists is **overwritten**; a row that is missing is **created**;
  languages the seeded baseline does not carry are **untouched** (a
  community-authored `es` row, say, is **left alone**). The reset is
  **idempotent**: running it twice yields the same page; a second run does
  not create duplicate rows (the per-row `Id` is stable). See the handoff
  note for the full per-language behaviour.
- **D5 — Destructive by design, guarded by a confirm + the edit
  standing.** A reset is a **destructive write** — the admin's customised
  English body and their customised `de`/`fr`/`da` translations are
  **replaced**, not merged, and there is **no undo** (the prior text is
  simply gone from the page). Two guards stand in front of it: the **edit
  standing** (D3) and a **`confirm()`** on the button's form `onsubmit`
  (the UI guard), whose message states plainly that the English body
  **and** the German / French / Danish translations will be overwritten.
  The audit row is the only trail (D6).
- **D6 — Audit.** One new **audit action** on the existing `page.*`
  vocabulary: `page.reset` (a page's `en` body + `de`/`fr`/`da` rows were
  reset to the seeded baseline). The row is written **in the same
  session** as the text change (C3), with the target `page` id and the
  `Via` standing resolver the edit lane already uses (`Owner` for the
  author of a blog page, `Admin` for a GlobalAdmin). A **denied** reset
  writes **no** audit row.
- **D7 — Web surface.** `POST /pages/{id:guid}/reset-seeded` on the
  existing `PageController`, `[Authorize]` +
  `[ValidateAntiForgeryToken]`; `UnauthorizedAccessException` → `403`,
  `KeyNotFoundException` → `404`, `InvalidOperationException` (non-seeded
  slug) → **`400`** with the exception message added to the model state. A
  single outline-danger "Reset to seeded text" form button in the page
  edit form's action bar, rendered **only when** the form is the edit view
  **and** `Model.CanReset` is true (a `[BindNever]` view-model flag set to
  `FirstBootSeeder.HasSeededText(page.Slug)` — a **pure registry probe**).
  The button label uses the `kw-l` key `pages.resetSeeded` (added to the
  en/de/fr/da `KnownTranslationKeys` registry). See the handoff note for
  the full Web surface.
- **D8 — No new doc, schema, role, or failure shape.** The lane adds:
  **one** service method, **one** pure applier on the seeder, **one**
  controller action, **one** `[BindNever]` view-model flag, **one** button,
  **one** audit action (`page.reset`), and **one** kw-l key. **No** new doc
  type, **no** schema change, **no** new `AccessAction` / `AccessVia`,
  **no** new role, **no** new failure shape (it reuses the existing
  `KeyNotFoundException` / `UnauthorizedAccessException` /
  `InvalidOperationException` → 404/403/400 mapping), **no** new route
  beyond the one POST.

## Consequences

**Positive.** An admin can pull the **latest shipped** seeded text for any
seeded page in one click — the exact content a fresh instance would have —
then re-customise on top of it. The **choice stays with the admin**: keep
the customisations (don't reset) or reset and re-customise; both are
first-class, the default is non-destructive. The reset is **guaranteed
correct**: it reads the same registries the seeder writes, so the reset
text is byte-identical to what a fresh instance ships — no drift, no
transcription. It is **small and auditable**: one `page.reset` row per
reset, in the same session as the change, with the standing `Via` the edit
lane already emits.

**Neutral.** A **non-seeded** slug (a hand-authored blog page) has nothing
to reset to — the button is hidden (`CanReset` is false), and a
hand-crafted POST is a 400 form error. A reset **replaces** the admin's
customised `de`/`fr`/`da` rows (D5); the `confirm()` message states this
plainly, and the admin can simply not reset. The reset is **idempotent**
(D4): a second run is a no-op, so it is safe to click twice if the first
felt "off".

**Deferred (own lanes).** Resetting a page's **non-`en`, non-seeded**
community translations (out of scope by design — D4: only the seeded
languages are written). A **"restore my custom text"** undo (deliberately
absent — D5: the reset is destructive and the audit row is the only trail).
Resetting the **language catalog** or **UI-string** baselines to the
code-owned seed (the same *kind* of lane, but a different surface and its
own ADR).

**Follow-on.** **ADR [0128](0128-admin-help-bulk-reset-and-update-surfacing.md)** — the **bulk** variant on top of this lane: an
`/admin/help` surface that surfaces *which* seeded pages have newer shipped
text and resets **every** seeded page in one audited action. It reuses this
lane's `ResetSeededTextAsync` applier and `page.reset` audit verbatim; this
per-page lane is the primitive it builds on. See the handoff note for the
full references + code + test list.
