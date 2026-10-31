# ADR 0275 — Portability import (KeepExisting): merge principals by email, keep the existing profile

Status: Accepted
Date: 2026-10-30
Extends the **ADR 0193 portability import modes** lane (the `KeepExisting`
merge semantic — "keep existing, add only what is new") and **ADR 0274**
(the `KeepExisting` tag merge — the same keep-first discipline, re-applied to
the `Tag` / `TagTranslation` pair). It also leans on the **UI lane**
(`DirectoryService` — the `/directory` page lists one row per
`Profile` document, 1:1) and the **M2 directory/profiles lane** (the `Profile`
document's identity is `SubjectId` — the account's `User.Id`). This ADR closes
the duplicate-directory failure a `KeepExisting` import produced over a live
instance: the sample accounts/profiles two independent instances already shared
— same person, same email, **different account id** — were imported as **two**
`Profile` rows (plus a second Identity account), so the `/directory` page
showed the same person twice.

## Context

ADR 0274's tag merge was correct for tags because a tag's business key
(`Slug`) is *carried on the document* and is stable across instances — two
instances' copies of the same tag share the slug, so the merge can match them.
A **principal** (an account + its `Profile` document) has no such stable,
cross-instance key on the document: the `Profile`'s identity is `SubjectId`,
which is the account's `User.Id`, and that id is **random per instance**. The
platform's sample seeder mints subject ids with `Guid.NewGuid().ToString("N")`
— so "the admin on instance A" and "the admin on instance B" are two different
`SubjectId`s even though they are the same person. A cross-instance backup
import therefore has no `SubjectId` to match on: the archive's admin and the
target's admin are, to the document store, two strangers.

The only stable, cross-instance identity a principal carries is its **email**
(the `PortabilityPrincipal.Email` / `NormalizedEmail` fields — the one field a
real-world operator would expect to be shared). ADR 0193's `KeepExisting`
already matches principals by email when it decides whether to re-create the
**Identity account** (an account with a matching email is skipped — kept
intact, roles re-applied additively). But it did **not** carry that decision to
the **document** layer: the archive's `Profile` row for that person (a
different `SubjectId`, the same person) was stored as a **second** `Profile`
document, and `DirectoryService` — which lists one row per `Profile` — rendered
the person **twice**. The content graph's principal references (every
`Target == "principal"` field — `AuthorId`, `OwnerId`, `UserId`, `GuardianId`,
`ChildId`, `RecipientId`, `SenderId`, `ParticipantA/B`, …, 35 fields across the
registry) were likewise left pointing at the archive's foreign `SubjectId`, a
dangling id the target store has no account for.

The constraint that keeps the merge safe is the one ADR 0193 and ADR 0274
already enforce: **the existing row is kept intact, never overwritten; only new
rows are added.** This ADR re-applies that same keep-first discipline to the
`Profile` / principal pair, using a merge key the natural-identity skip cannot
express (it is the principal's email — a function of the archive's `principals`
rows + the target's existing accounts, not of a unique index).

## Decision

**D1 — A KeepExisting-only principal merge, computed once before the docs apply.**
`PortabilityApplyIdentity.ApplyAsync` — which already walks the archive's
`principals` and, in `KeepExisting`, matches each to an existing account by
email (falling back to `SubjectId`) — now **returns the merge map** it finds:
a `Dictionary<string, string>` mapping the archive principal's `SubjectId` to
the **existing** account's `User.Id`, recorded only for the principals that
matched an existing account (a principal that did not match is created fresh
under its own `SubjectId`, so it never appears in the map). `CleanSlate`
returns an empty map (the wipe removed the target's accounts — there is nothing
to merge into).

`PortabilityApplyDocuments.ApplyAsync` gains a `principalMergeMap` parameter
(ADR 0274's `defaultLanguageCode` parameter already threads the archive's config
into the docs apply — the principal map is the same "computed upstream, applied
here" shape). In `KeepExisting` mode (only), before the per-type loop, the
service:

1. **Re-points the archive's content references** (data-driven, from the
   registry's `Target == "principal"` fields — the 35 `AuthorId` / `OwnerId` /
   `UserId` / `GuardianId` / `ChildId` / `RecipientId` / `SenderId` /
   `ParticipantA/B` / … fields; the registry's closed field set drives it, no
   per-type code): every archived reference to a merged principal is re-pointed
   at the existing account's id, so the archive's content lands on the
   **target's** account — not a dangling foreign `SubjectId`. A reference to a
   principal that was **not** merged (stored fresh under its own `SubjectId`)
   is left as-is; a reference to an id the archive does not carry (an untouched
   target-local account) is left as-is. The `Profile` rows themselves are
   excluded from the re-point (their `SubjectId` is their own identity, not a
   reference to another principal).
2. **Skips the store** for each `Profile` row whose `SubjectId` is in the merge
   map (the existing profile is kept intact, never overwritten — the
   `/directory` entry is not doubled).

This is the ADR 0274 tag merge's two-step shape, re-applied: a merge map
(archive id → existing id) + a content re-point (the registry's closed field
set) + a store-skip (the merged entity's own row). The `Tag` / `TagTranslation`
and the `Profile` / principal pair share the same "identity guard is not a
unique index" reason for existing at all — the `Profile`'s identity is
`SubjectId` (a cross-instance random id), and the principal's identity guard is
the email (a cross-instance stable field) — neither is a unique index the ADR
0193 natural-identity skip can see.

**D2 — The keep-vs-add boundary is the email.** A principal the archive carries
that the target already holds — **same email** — is the **same person** and is
merged (the existing account + profile kept intact, the content re-pointed at
it). A principal with a **different email** is a **different person** and is
created as its own new account + profile (no false merge). This is the ADR
0193 principal-skip design carried to the document layer: one email, one
person. The merge map is the only place the "same person" decision is made;
everything else (the skip, the remap, the store-if-absent) follows from it.

**D3 — `CleanSlate` is unchanged.** The merge is `KeepExisting`-only.
`CleanSlate` (the default — the D1 restore semantic) wipes then applies, so the
archive's principals + profiles re-materialize onto the fresh store exactly as
before; the merge would be a no-op there (the wipe removed the target's
accounts, so there is nothing to merge into). The `principalMergeMap` parameter
is passed but empty in `CleanSlate` (the merge block is gated on
`mode == KeepExisting`).

## Consequences

- A `KeepExisting` import over a live instance that already held the archive's
  principals **no longer doubles them**: a shared person (same email) appears
  once — the target's existing account + profile, kept intact; the archive's
  content lands on it (its `AuthorId` / `OwnerId` / … references re-pointed at
  the target's account id — not a dangling foreign `SubjectId`). A principal
  with a different email is added as its own new account + profile (two emails,
  two people).
- The default-restore (`CleanSlate`) path is byte-for-byte unchanged.
- The merge is data-driven (the registry's `Target == "principal"` reference
  fields drive the content re-pointing; the `Profile` row is the only
  special-cased type, and only because its identity guard — `SubjectId` — is
  not a unique index the ADR 0193 natural-identity skip can see, the same
  reason ADR 0274's `Tag` is special-cased).
- **Live-instance repair:** an instance that already accumulated duplicate
  directory entries from a pre-fix `KeepExisting` import still has them — the
  merge is forward only (a re-import merges new principals into existing ones;
  it does not retroactively de-duplicate rows the target already holds, which
  the keep-first discipline forbids). The operator's path to a clean
  `/directory` page is a `CleanSlate` import (the default — it wipes then
  applies the archive's profiles as the single set) or the operator's
  pre-import backup (the ADR 0108 D1 rollback path).
- **The ADR 0274 `Tag` / `TagTranslation` merge and this ADR's `Profile` /
  principal merge are independent and composable:** a `KeepExisting` import
  over a live instance that already held both the archive's tags and the
  archive's principals merges both — the tags by slug + default-language name
  (ADR 0274) and the principals by email (this ADR) — in the same apply pass,
  with no interaction (the two merge maps are computed upstream, applied
  independently, and the content re-point is data-driven from the registry's
  closed field set for each `Target` separately).
