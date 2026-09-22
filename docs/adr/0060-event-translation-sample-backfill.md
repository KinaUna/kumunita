# ADR 0060 — Warm-boot backfill of the sample events' `de` / `fr` / `da` translations

Status: Accepted
Date: 2026-09-20
Amends: **0056** (the sample-data opt-in — the pristine-boot seed path is
**unchanged**; this ADR adds a *warm-boot* lane the flag also gates) and
**0059** (the event-translation lane — the `EventTranslation` doc and the
author ∪ Translator ∪ GlobalAdmin standing it fixes; this ADR's backfill is
never the author/Translator lane, it is a boot-time no-audit fill). Builds on
**0047 D2** (the canonical pages' `PageTranslation` warm-boot backfill — the
first named warm-boot backfill lane), **0052** (the UI-string baselines'
warm-boot backfill — the second), and **0042 D1** (the create-if-missing,
never-overwrite an admin edit invariant all three lanes hold).

## Context

ADR 0055 / 0056 ship a first-boot, flag-gated mock neighborhood
(`SampleDataSeeder`): a scoped moderator, a Translator, several verified
residents, groups, announcements, community + group posts with replies,
published events with RSVPs, tags, and a resident blog. A fresh
`docker compose down -v && docker compose up --build` (dev) or a fresh
deployed demo instance therefore comes up immediately exercisable — including
four sample **events**, authored in `en`.

ADR 0059 then added the event-translation lane: an `EventTranslation` doc
(title + body) keyed `(EventId, LanguageCode)`, added/edited/removed by the
event's author, a Translator, or a GlobalAdmin — and the ADR 0049 /
0051 default-visible-variant display so a `de` / `fr` / `da`-speaking
resident sees the matching variant first, with the `en` authored-in variant
as a one-click swap.

But the sample seeder's pristine gate never re-runs (ADR 0056, same gate as
`FirstBootSeeder`), so an instance whose **first boot predates ADR 0059**
has the four sample events (each with its `en` title/body) but **no
`EventTranslation` rows** — a `de` / `fr` / `da`-speaking resident sees only
the English variant on the `/events` feed and the event detail, indefinitely,
until a Translator re-types each by hand. This is exactly the
residency-visible gap ADR 0047 D2 (pages) and ADR 0052 (UI strings) already
closed for their own surfaces, and the same "one registry, two lanes" shape
those two rely on: a code-owned baseline set that a fresh seed materializes
**and** a warm-boot backfill re-applies create-if-missing.

Two facts make widening the named-exception set to the sample events'
`EventTranslation` rows safe:

- **Create-if-missing is the whole write.** The backfill never refreshes an
  existing `(EventId, LanguageCode)` row, so a Translator's in-app edit
  (the ADR 0059 lane) is never clobbered by a later deploy — the exact
  invariant ADR 0047 D2 and ADR 0052 already accept.
- **The scope is the flag.** Unlike the platform-wide canonical pages and
  the UI-string catalog, the sample events exist *only* because
  `SampleData__Enabled` is set. The backfill is therefore **gated on the
  same flag** and matches events by the seeder's stable English titles, so
  on a real neighborhood — which never carries the flag — the lane is
  unreachable by construction (ADR 0055/0056). No real user's content is ever
  touched.

**What this is, in one line:** a warm boot (a flag-carrying instance that is
**not** pristine) now also backfills the missing `de` / `fr` / `da`
`EventTranslation` rows for the sample events — create-if-missing,
idempotent, never overwriting a Translator's edit, the `en` authored-in
row never read or written — so a deployment that ships the sample event
translations delivers them to an already-running demo instance on the next
redeploy instead of waiting for an in-app re-typing.

## Decision

### D1 — One registry, two lanes

`SampleDataSeeder.EventTranslationBaselines` is the single code-owned
`(English title → [ (code, title, body) × de/fr/da ])` registry. Both lanes
read the **same** set, so a fresh instance and a backfilled one carry
byte-identical `de` / `fr` / `da` rows:

- **Pristine-boot lane (unchanged, ADR 0056):** `SampleDataSeeder.SeedAsync`
  seeds the four sample events **and** stores each event's three
  `EventTranslation` rows from the registry, in the seeder's single
  `IDocumentSession` (invariant C3).
- **Warm-boot lane (this D):**
  `SampleDataSeeder.BackfillEventTranslationsAsync(session, ct)` re-applies
  the same three rows per sample event, create-if-missing.

The two lanes are mutually exclusive by the pristine gate (a boot is
pristine **or** warm, never both), exactly the ADR 0047 D2 / ADR 0052 split.

### D2 — Scope, semantics, and invariants

- **The sample events' `de` / `fr` / `da` rows only.** For each registry key
  (a sample event's English title), the lane finds the matching
  non-deleted `Event` by that title and creates the row for each of
  `de` / `fr` / `da` **if — and only if — no row for that
  `(EventId, LanguageCode)` already exists** (the `M4DocTypes` unique index
  enforces one text per event per language). A registry key with no matching
  event (e.g. a sample event deleted in-app) is skipped — nothing to
  backfill for it.
- **The `en` authored-in row is never read or written.** The event's own
  `Title` / `Body` (its `en` authored-in text, ADR 0018) is the base; the
  backfill only adds the non-`en` variants. Refreshing `en` would clobber
  the author's text and is out of scope (mirrors ADR 0047 D2's "the `en`
  body is never read or written").
- **Create-if-missing, never overwrite (ADR 0042 D1, held).** The skip path
  is the invariant: an existing `(EventId, LanguageCode)` row — the seeder's
  own pristine-boot row **or** a Translator's in-app edit (the ADR 0059
  lane) — is left untouched. The Translator updates the row in place (its
  `Id` persists), so the skip branch finds the edit and leaves it alone.
- **Idempotent.** A second warm boot finds every row the first created and
  skips all of them; the row count per `(event, language)` is stable
  (exactly one). No tombstones, no deletes, no last-wins refresh.
- **`AuthorId` on a backfilled row is the empty string** (sample / platform
  content — no resident author), distinct from a Translator's lane row,
  which carries the acting resident's subject id.
- **No `AccessAudit` row.** As with the first-boot seed and the ADR 0047 D2
  / ADR 0052 backfills, a boot-time backfill is not an actor's action — the
  seeder is not a principal.
- **Gated on `SampleData__Enabled` and `!firstBoot`.** The call lives in
  `Program.cs`, in the **`else`** of the `sampleDataOpts.Enabled &&
  firstBoot` seed branch: `else if (sampleDataOpts.Enabled && !firstBoot)`.
  It runs in an async scope (the scoped `IDocumentStore` + a
  `Marten.Services.SessionOptions` session, the codebase convention),
  **after** `app.StartAsync()` for the same reason the seeder and the
  SchemaBootstrap warm branch do.

### D3 — Consequences

- **The residency-visible gap closes for a running demo instance.** A
  `de` / `fr` / `da`-speaking resident on an instance whose first boot
  predates ADR 0059 gets the matching variant on the `/events` feed and
  event detail from the next redeploy — no in-app re-typing, no manual
  database step. A pristine instance is unaffected (it seeds them directly).
- **The named warm-boot backfill exception set is now three, all
  create-if-missing, all "never clobber an edit / never touch the base /
  idempotent":** the four canonical pages' `PageTranslation` rows (ADR
  0047 D2), the UI-string `TranslationResource` baselines (ADR 0052), and
  the sample events' `EventTranslation` rows (this D). The third differs in
  one recorded way: it is **flag-gated** (sample-data-specific) rather than
  platform-wide, so it is unreachable on a real neighborhood by construction
  (ADR 0055/0056). Everything else — UGC translations, group/community
  translations, catalog rows — is still untouched by any warm boot.
- **The ADR 0059 Translator lane is unaffected.** The backfill writes only
  rows that do not yet exist; a Translator's add / edit / remove (ADR 0059,
  ADR 0048 shape) is the resident-facing lane and is never touched or
  reversed by a boot.
