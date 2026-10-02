# ADR 0130 — Sample corpus: whole-corpus create-if-missing backfill on warm boot

**Status:** Accepted
**Date:** 2026-09-12
**Amends:** **0060** D1 (the event-translation backfill is subsumed — the events'
de / fr / da rows are now one case of the whole-corpus reconciliation) and **0052**
+ **0047** D2 (the two earlier, narrow, named warm-boot backfill lanes — the UI-string
baselines and the four system pages' translations — are now also subsumed by, and
therefore re-applied through, the same generic mechanism; their invariants stand).
**Supersedes** none; `SeedAsync`'s public API is preserved (the cold-boot path is
untouched).

## Context

ADR 0129 moved the development sample neighborhood into an embedded
`Data/sample-data.json` and demoted `SampleDataSeeder` to a **generic
materializer**. Cold boot — a pristine database — materializes the whole corpus
once. But a deployment that was seeded *before* the corpus was last grown has no
path to pick up the new content: the corpus only ever appears on a first boot, and
a "warm" boot (a database that already holds some of the sample rows) leaves the
previously-absent rows absent.

ADR 0060 D1 solved this for exactly one surface — the events' de / fr / da
translation rows — with a dedicated `BackfillEventTranslationsAsync`. ADR 0052 and
ADR 0047 D2 each added their own narrow warm-boot backfill (the UI-string
baselines, then the four system pages' translations). Three separately-authored
warm-boot mechanisms, each matching by a different key, each with its own
"create-if-missing, never clobber" care. As the corpus kept growing, each *new*
entity kind that a developer added to `sample-data.json` had to be *remembered*
into whichever backfill would apply — an omission that silently left the warm
instance missing rows.

The user asked: *when a platform instance is set to use the demo sample data, it
should import the demo data on boot and add any missing entries from the sample
data.* The natural generalization is a **single, whole-corpus reconciliation** that
re-applies ADR 0056 D1's "create-if-missing, never clobber" rule to every
entity in the document, on every warm boot.

## Decision

### D1 — One generic whole-corpus reconciliation on every warm boot

A new `SampleDataSeeder.BackfillSampleCorpusAsync` is the **sole** warm-boot
reconciler, replacing the dedicated `BackfillEventTranslationsAsync` call in
`Program.cs` (the events' translations are now just one case of the loop). It is
gated exactly like the cold boot — `SampleData__Enabled` ∧ a warm boot (a database
that is not pristine) — and is **Development/deploy-agnostic in shape**: it
materializes every entity in `sample-data.json` that is missing, and leaves every
entity that already exists **bit-for-bit** untouched. It is a **no-op** on a fully
seeded instance (the common case), which is what keeps a normal warm boot cheap.

### D2 — Match by stable natural key; create-if-missing; never clobber

Each entity kind is matched against the existing rows by its **natural key** — the
same human-readable reference the cold-boot materializer already resolves — and,
when absent, created with a fresh id. Existing rows are never rewritten, so a
resident edit (a retitled post, a corrected description) is preserved:

| Entity                 | Natural key                                                        |
|------------------------|--------------------------------------------------------------------|
| accounts               | e-mail (the ADR 0078 closed set)                                   |
| tags                   | `Slug`                                                            |
| groups                 | `Name` (the `Group` doc carries no slug — ADR 0010)               |
| announcements          | `(Title, CommunityId)`                                            |
| posts                  | `(Title, lane)` — lane = `GroupId` for a group post, `ComponentId` for a community post (ADR 0013 exclusivity) |
| replies                | `(PostId, Body)`                                                  |
| events                 | `Title`                                                           |
| RSVPs                  | `(EventId, ResidentEmail)`                                        |
| pages                  | `Slug` (flat, globally unique; parent resolved by slug)           |
| goals                  | `Title`                                                           |
| projects               | `Title`                                                           |
| to-dos                 | `(ProjectId, Title)`                                              |
| translations (any)     | `(ParentId, LanguageCode)`                                        |

Child rows (translations, replies, RSVPs, to-dos) key on their **parent's resolved
id** + their own field, so they attach to the *existing* parent when the parent
already exists, and to the *just-created* parent when the parent was created this
run. Moderator assignments are re-applied as an in-memory `(Group, Resident)` pair
check. Mandatory-community membership is re-applied per the document's
`MandatoryComponents` through the existing `IUserInfoService` seam (itself
idempotent).

### D3 — One `mt` session, one commit (invariant C3)

All of the `mt` document writes — tags, groups, memberships, announcements, posts,
replies, events, RSVPs, pages, goals, projects, to-dos, and every translation
row — are staged into a **single** `IDocumentSession` and committed once. The
Identity-side work (the account ensures and the mandatory-community re-applications)
runs through `UserManager` / `IUserInfoService` as before, outside that session.
This is the ADR 0004 C3 "all `mt` writes commit in one `IDocumentSession`"
invariant, applied to the backfill exactly as to the cold boot.

### D4 — Two postures, as on the cold boot

The account reconciliation honours the ADR 0056 two-posture credential rule: a
deployed instance (mailer present, `seedAdminEmail` set) leaves the admin password
`null` (no weak credential — the setup-token lane) and generates random passwords
for the other sample accounts; a development instance applies the weak demo
credentials. `EnsureUserAsync` only adds a password to an account that has none, so
an account that already carries a (human-set) password is left exactly as-is —
never overwritten.

### D5 — Consequences

- **Additive corpus growth now "just works."** A developer adds a post, event,
  page, or translation to `sample-data.json` and any instance that has sample data
  enabled picks it up on its next warm boot — no per-entity backfill to remember.
  The ADR 0060 D1 / ADR 0052 / ADR 0047 D2 backfills still hold, now as special
  cases of the loop.
- **A renamed display string is a rename, not a create.** Natural keys are
  display strings (titles, names). The corpus contains a few **same-title posts
  within one group lane** (e.g. a photo-corner thread with two posts of the same
  name); the `(Title, lane)` key dedupes those deterministically, and a genuinely
  retitled post simply reads as a *new* post (the old one is preserved). That is
  the accepted trade for "no schema change": the cold-boot materializer already
  keyed the same way, so warm and cold stay in agreement.
- **Cheap in the steady state.** On a fully-seeded instance the reconciliation
  matches every row and writes nothing; the cost is the preload of the existing
  rows, not a write storm.
- **Tested.** `SampleDataCorpusBackfillTests` pins the three behaviours on a real
  database: a fresh database gets the whole corpus; a pre-existing human edit is
  never clobbered; a second run is a no-op (no new rows, no changed ids).

**Done.**
