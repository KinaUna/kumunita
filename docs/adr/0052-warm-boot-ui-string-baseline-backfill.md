# ADR 0052 — Warm-boot backfill of the UI-string `de` / `fr` / `da` baselines

Status: Accepted
Date: 2026-09-20
Amends: **0042 D1** (the "no warm-reseed mechanism" decision and its
recorded "new-key asymmetry" — the UI-string baselines **are** now
backfilled on a warm boot, the same narrow-exception shape ADR 0047 D2
already gave the four canonical pages' translation rows) and
**0047 D2** (the exception is widened from "the four canonical pages'
`PageTranslation` rows only" to "those rows **plus** the UI-string
catalog's `TranslationResource` baselines"). Builds on **0042** (the
ownership semantics: `en` code-owned forever, `de` / `fr` / `da`
seeded-once-then-community-owned, an admin edit never overwritten),
**0045** (the `da` baseline row), **0015** (the provider floor — why new
keys' English already reaches a warm instance, isolating the gap to the
non-`en` baselines), and **0048** (which surfaces gained edit/remove
lanes — UI strings did not, a load-bearing difference for this ADR's
safety argument).

## Context

The UI-string catalog resolves per string through the provider's
fallback chain (ADR 0005 / 0015): `(key, preferred)` → `(key, default)`
→ `(key, en)` → the key's `en` source text from the
`KnownTranslationKeys` registry (the **provider floor**). Two of those
four steps come from the database and one from code.

The first-boot seeder materializes the full baselines on a pristine DB
(ADR 0042 D1 / 0043 / 0045), but the pristine gate never re-runs — and
ADR 0042 D1 records the *consequence* as a deliberate decision, not a
bug to fix:

> a `de` / `fr` baseline for a key added to the registry in a later code
> release does **not** appear in an existing instance's database until a
> GlobalAdmin / Translator types it in-app … *a recorded decision, not a
> bug to fix.*

That decision was then **already narrowed once**: ADR 0047 D2 carved a
recorded exception for the *four canonical system pages'* `de` / `fr` /
`da` `PageTranslation` rows, backfilled on every warm boot
(create-if-missing, idempotent, never overwriting an admin edit or the
`en` body) — because ADR 0047 D1 made those routes render in the
effective language, so a missing baseline was resident-visible. The
rationale applies to the UI strings **just as strongly**, and the
residency-visible gap is the same one the platform hit the day ADR 0052's
lane was requested: the login page's "Remember me" / "Complete setup"
hints landed in the registry with `de` / `fr` / `da` values, yet an
existing instance (whose first boot predates them) renders the English
floor for those strings on every boot, indefinitely — exactly the seam
ADR 0047 D2 closed for `/terms` / `/help` / `/privacy` / `/conduct`.

Two facts make the widening safe:

- **Create-if-missing is the whole write.** The backfill never refreshes
  an existing row, so an admin's in-app edit (the GlobalAdmin ∪
  Translator lane, ADR 0021; the localization editor updates the row in
  place, keeping its `Id`) is never clobbered — the exact invariant ADR
  0047 D2 already accepts for pages.
- **There is no UI-string remove lane.** ADR 0048 lifted the add-only
  pin on posts, replies, announcements, group/community
  name/description, and pages — but **not** on the UI-string catalog. A
  deliberately-deleted row cannot be resurrected by a backfill, because
  deletion is not a reachable state on this surface. (The create-
  if-missing rule still holds as belt and braces, identical in shape to
  ADR 0047 D2.)

**What this is, in one line:** a warm boot now also backfills the
missing `de` / `fr` / `da` `TranslationResource` baseline rows for the
closed registry — create-if-missing, idempotent, `en` never touched —
so a deployment that ships new baseline strings delivers them on the
next redeploy instead of waiting for an in-app re-typing.

## Decision

### D1 — A new public seeder primitive, run on every warm boot

`FirstBootSeeder.BackfillUiStringBaselinesAsync(session, ct)` runs in the
**`else` branch** of the pristine gate in `SchemaBootstrap` — the same
warm-boot branch ADR 0047 D2's
`FirstBootSeeder.BackfillPageTranslationsAsync` already occupies, in the
same session, immediately after it. The pristine-boot path
(`if (firstBoot)`) is **unchanged**: a fresh instance still materializes
the full baselines exactly as ADR 0042 / 0043 / 0045 describe, and the
two lanes are mutually exclusive by the gate (a boot is pristine or
warm, never both).

### D2 — Scope, semantics, and invariants

- **The `de` / `fr` / `da` baselines only.** For every key in
  `KnownTranslationKeys`, the backfill creates the row for each of
  `DeValues` / `FrValues` / `DaValues` **if — and only if — no row for
  that `(Key, LanguageCode)` already exists**. The created row is
  byte-identical to what a fresh seed would have produced (same
  baseline dictionaries, same `(Key, LanguageCode)` business key,
  surrogate `Id` — the pair idiom, M1DocTypes' unique index enforces one
  text per key per language).
- **The `en` row is never read or written.** It is the floor, owned by
  code (the code-wins upsert lives on the pristine-boot path, ADR 0042
  D1), and a new key's English already renders on a warm instance via
  the provider floor (ADR 0015 D1) — so the **only** gap this lane
  serves is the missing non-`en` baseline rows. Refreshing `en` here
  would clobber an in-place edit and is out of scope, mirroring ADR
  0047 D2's "the `en` `Page.Body` is never read or written."
- **Create-if-missing, never overwrite (ADR 0042 D1, held).** The skip
  path is the invariant: an existing `(Key, LanguageCode)` row — the
  seeder's own pristine-boot row **or** an admin's in-app edit (ADR
  0021) — is left untouched. The localization editor updates rows in
  place (its `Id` persists), so the skip branch finds admin edits and
  leaves them alone.
- **Idempotent.** A second warm boot finds every row the first created
  and skips all of them; the row count per `(key, language)` is stable
  (exactly one). No tombstones, no deletes, no last-wins refresh — the
  create-if-missing shape is what makes the re-run a no-op.
- **No `AccessAudit` row.** As with the first-boot seed and ADR 0047
  D2's backfill, a boot-time backfill is not an actor's action — the
  seeder is not a principal.

### D3 — Consequences

- **The "new-key asymmetry" (ADR 0042 D1) is closed for the UI-string
  surface.** A baseline added to the registry in a code release now
  reaches every existing instance on the next boot — no in-app
  re-typing, no manual database step. The `en` half of the asymmetry
  was never a gap (provider floor, ADR 0015); this D closes the
  non-`en` half, matching the outcome ADR 0047 D2 already accepts for
  pages.
- **The M·12 completeness view becomes a post-boot guarantee, not a
  post-typing one.** After one warm boot following a baseline release,
  `de` / `fr` / `da` are 100% present from the baseline's own content
  (admins may then edit any row in place, which the backfill honors and
  never reverses). The completeness view remains the *review* surface
  for baseline quality (ADR 0042 D2's "careful first pass, editable
  later" bar, unchanged).
- **The standing "no warm-reseed mechanism" is now precisely two named
  exceptions, both create-if-missing:** the four canonical pages'
  `PageTranslation` rows (ADR 0047 D2) and the UI-string baselines
  (this D). Everything else — UGC translations, group/community
  translations, catalog rows — is still untouched by any warm boot. The
  exception set is small, named, and both are the same invariant
  (never clobber an admin edit; never touch `en`; idempotent).
- **The `da` baseline (ADR 0045) rides the same loop.** A `da`
  row is created-if-missing regardless of the catalog row's enable
  state — the strings exist whether or not a GlobalAdmin has enabled
  Danish, exactly as the first-boot path already behaves.

### D4 — What this ADR is *not*

- **Not a reseed of `en`.** The `en` rows stay pristine-boot-only,
  code-wins (ADR 0042 D1 unchanged). If an operator needs the `en` text
  refreshed after an `en` registry edit on an existing instance, that is
  a deliberate operator action (wiping the volume for a fresh seed, or a
  one-off migration), not a boot-time behavior — boot-time `en`
  refreshes would race admin in-place edits and are refused by design.
- **Not a warm-reseed of any other translation surface** (posts,
  replies, announcements, groups, communities, tags). Those are
  community data owned by their authors (ADR 0005 B), not bundled
  baselines, and the two named exceptions above remain the only
  warm-boot write lanes on the translation rows.

## Tests

`LS_U04_SeederTests.BackfillUiStrings_CreatesMissingDeFrDa_SkipsExisting_
Idempotent` (Core): a warm-deployment state (the `en` floor for every
key + all baselines except two simulated gaps — one key missing all
three, one key missing only `fr` and carrying an admin-edited `de`
row) → the backfill adds exactly the missing rows with the baseline
text, leaves the admin-edited `de` row byte-identical, never touches
the `en` rows, creates no duplicates, and a re-run is a no-op. The
existing page-backfill test (ADR 0047 D2) is unchanged and still green.
