# ADR 0274 — Portability import (KeepExisting): merge tags by slug + default-language name

Status: Accepted
Date: 2026-10-30
Extends the **ADR 0193 portability import modes** lane (the `KeepExisting`
merge semantic — "keep existing, add only what is new") and the **TG lane**
(ADR 0044 — the `Tag` / `TagTranslation` documents, the `Slug` business key,
the write-lane create-or-reuse identity guard C-TG·4) and the **ADR 0005**
display-name preference order (the default-language `TagTranslation` row,
falling back to the tag's base `Name`). This ADR closes the duplicate-tag
failure a `KeepExisting` import produced over a live instance: tags that two
independent instances already shared — same slug, same default-language name —
were imported as **two** `Tag` rows, so the `/tags` page showed the same tag
twice.

## Context

ADR 0193's `KeepExisting` (merge) import skips an archived row when the target
already holds it, keyed on the type's **natural identity** — the union of the
surrogate id and the member set of **every unique index** on the type's table
(derived from the Marten mapping). That guard is correct for the doc types it
was built for — `TranslationResource` `(Key, LanguageCode)`,
`GroupMembership` `(GroupId, UserId)`, the `*Translation`
`(ParentId, LanguageCode)` rows — because each carries a **unique business-key
index** the skip can read.

A `Tag` is the exception. The `Slug` is its business key (ADR 0044 D3, the
C-TG·4 pin) but it is **not** a unique index: `TagDocTypes` registers **no**
index on `Tag`, on the grounds that the write lane's create-or-reuse
(`TagService.AttachTo*Async`, "a second author attaching the same Slug reuses
the Tag doc") is the identity guard. And the `TagTranslation`'s
`(TagId, LanguageCode)` unique index is keyed on the **surrogate** `TagId`, not
on the name — so two instances' copies of the same tag (different surrogate
ids) and their `TagTranslation` rows **never collide on a unique index**; they
are stored as two `Tag` rows under two `TagId`s, each with its own
`TagTranslation` set. The natural-identity skip (which can only see unique
indexes) therefore cannot see the `Tag` / `TagTranslation` pair, and the merge
import silently **doubled** every shared tag.

The user's report is explicit: *"some of the tags are showing twice on the
`/tags` page. The same tag should only show once, and when importing backups
existing tags should be merged with imported tags if they are the same in the
default language."* Two constraints:

- **The same tag must appear once.**
- **The merge key is the default-language name** — a tag is "the same" when it
  is the same slug **and** the same display name in the instance's default
  language. A different default-language name (even the same slug) is a
  **different** tag (the ADR 0044 D3 design — two names, two tags).

The constraint that keeps the merge safe is the one ADR 0193 already enforces:
**the existing row is kept intact, never overwritten; only new rows are added.**
This ADR re-applies that same keep-first discipline to the `Tag` /
`TagTranslation` pair, using a merge key the natural-identity skip cannot
express (it is a function of the archive's `Tag` rows + the target's existing
tags, not of a unique index).

## Decision

**D1 — A KeepExisting-only tag merge, computed once before the apply loop.**
`PortabilityApplyDocuments.ApplyAsync` gains a `defaultLanguageCode` parameter
(the archive's `config.json` `locale` block, `null` if the archive carried
none — `PortabilityService` parses the config before the docs apply and passes
it through). In `KeepExisting` mode (only — `CleanSlate` is unchanged), before
the per-type loop, the service:

1. **Builds the merge map** (archive `Tag` id → the existing tag's id): for
   each archive `Tag` row, it matches an existing target `Tag` on
   **`Slug` (normalized the way the write lane derives it: `Trim` +
   `ToLowerInvariant`) AND the default-language display name** (case-insensitive).
   The display name is the tag's `TagTranslation` row for the default language,
   falling back to the tag's base `Name` (the ADR 0005 D5 preference order —
   the `/tags` page's displayed name). A tag that matches no existing tag on
   both is **not** in the map (it is stored as its own new tag — D2).
2. **Re-points the archive's content references** (data-driven, from the
   registry's `Target == "Tag"` fields — the `TagIds` array fields on
   `Post` / `TodoItem` / `KanbanBoard` / `Page`; the registry's closed field
   set drives it, no per-type code): every archived reference to a merged tag
   is re-pointed at the existing tag's id, so the archive's content lands on
   the **target's** tag — not a second one. A reference to a tag that was **not**
   merged (stored fresh under its own id) is left as-is; a reference to a tag id
   the archive does not carry (an untouched target-local row) is left as-is.
3. **Remaps the archive's `TagTranslation` rows**: for each archived
   `TagTranslation` whose parent tag was merged, its `TagId` is re-pointed at
   the existing tag's id. The existing tag's `(TagId, LanguageCode)` rows stay
   intact (the natural-identity skip keeps them); a language the existing tag
   does not yet have is **added** (the row is not skipped — it is the
   "translations fold in" half). One row per `(existing tag, language)` — the
   ADR 0044 D1 shape, now satisfied by the existing tag, not a duplicate.

The apply loop then, for each `Tag` row whose id is in the merge map, **skips
the store** (the existing tag is kept intact, never overwritten); the merged
`TagTranslation` rows store against the existing tag (the `TagId` remap above)
and the natural-identity skip keeps the existing `(TagId, LanguageCode)` rows.
No new document, no new index, no new bounded context — the `Tag` /
`TagTranslation` documents and their indexes are unchanged; the merge is a
pure add-on to the `KeepExisting` apply path.

**D2 — The keep-vs-add boundary is the default-language name.** A tag the
archive carries that the target already holds — same slug **but a different
default-language display name** — is a **different** tag and is stored as its
own new tag (no false merge). This is the ADR 0044 D3 design carried to the
import: two names, two tags. The merge map is the only place the "same tag"
decision is made; everything else (the skip, the remap, the store-if-absent)
follows from it.

**D3 — `CleanSlate` is unchanged.** The merge is `KeepExisting`-only.
`CleanSlate` (the default — the D1 restore semantic) wipes then applies, so the
archive's tags re-materialize onto the fresh store exactly as before; the merge
would be a no-op there (the wipe removed the target's tags, so there is nothing
to merge into). The `defaultLanguageCode` parameter is passed but ignored in
`CleanSlate` (the merge block is gated on `mode == KeepExisting`).

## Consequences

- A `KeepExisting` import over a live instance that already held the archive's
  tags **no longer doubles them**: a shared tag (same slug + same
  default-language name) appears once — the target's existing tag, kept intact;
  the archive's content lands on it; the archive's `TagTranslation` rows fold
  in as its translations (a new language is added; an existing one is left
  untouched). A tag that differs in the default-language name is added as its
  own new tag (two names, two tags).
- The default-restore (`CleanSlate`) path is byte-for-byte unchanged.
- The merge is data-driven (the registry's `Target == "Tag"` reference fields
  drive the content re-pointing; the `Tag` / `TagTranslation` pair is the only
  special-cased type, and only because it is the only type whose identity guard
  is the write lane rather than a unique index — the same reason it is the only
  type the ADR 0193 natural-identity skip cannot see).
- **Live-instance repair:** an instance that already accumulated duplicate tags
  from a pre-fix `KeepExisting` import still has them — the merge is forward
  only (a re-import merges new tags into existing ones; it does not
  retroactively de-duplicate rows the target already holds, which the
  keep-first discipline forbids). The operator's path to a clean `/tags` page
  is a `CleanSlate` import (the default — it wipes then applies the archive's
  tags as the single set) or the operator's pre-import backup (the ADR 0108 D1
  rollback path).
