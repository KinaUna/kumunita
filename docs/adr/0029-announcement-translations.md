# 0029 — Announcement user-added translations (the ADR 0022 lane carried to announcements)

Status: Accepted (2026-09-12)

## Context

Announcements (`Kumunita.Core.Announcements`) already ship the authored-in
language tag (ADR 0018), the edit lane (ADR 0017), and the ADR 0018 deferral
note ("translations: later lane, out of scope here"). Every other resident-facing
UGC surface in the platform now has **user-added** translations — posts and
replies (ADR 0022, display/swap via ADR 0027) and group / community name &
description (ADR 0026) — all following the same rule: a translation is a row
**added by a real resident** in the standing, never a machine translation.
Announcements are the one resident-facing surface left without the lane, so the
platform cannot yet be exercised in a second language end-to-end (the M6
multilingual goal — ADR 0005). This ADR carries the ADR 0022 lane over to the
Announcements bounded context.

## Decision

An **`AnnouncementTranslation`** POCO (`Kumunita.Core.Announcements`) stores a
user-added translation of an announcement — `Id`, `AnnouncementId`,
`LanguageCode`, `Title?` (a translation may title the announcement or not),
`Body`, `AuthorId`, `Created` — registered on the `KumunitaFeature` document
surface (the `M3DocTypes` registration) alongside the existing announcement and
post-translation types. The `IAnnouncementService` gains two seams:

- **read** — `GetAnnouncementTranslationsAsync(announcementId)` returns the
  rows ordered by language code. Like every other ADR 0022/0026 read, this is a
  **read, not a decision**: it performs no authorization and no audit. The
  announcement itself is still read through `IAnnouncementService.GetAsync`'s
  per-scope authorization (ADR 0017) before its translations are reachable at
  all.
- **write** — `AddAnnouncementTranslationAsync(announcementId, languageCode,
  title, body, actorId, actorRoles, session)` stores the translation **and its
  `AccessAudit` row in the caller's `IDocumentSession`** (C3, ADR 0006),
  throwing `UnauthorizedAccessException` on the denied lane and
  `KeyNotFoundException` for an unknown announcement. **Add-only**: no
  update/remove lane (matching the ADR 0022 post/reply surface).

**The standing (the ADR 0022 matrix, carried over; the only new case is the
community-moderator lane):**

- **GlobalAdmin and Translator** may add a translation of **any**
  announcement, on every scope. `AccessVia.Admin`.
- **A community Moderator** (a `moderator:{CommunityId}` claim for some
  `CommunityId`) may add a translation **only of an announcement targeted at a
  community they moderate** — i.e. the announcement is
  `Scope == Community` **and** its `CommunityId` is non-null **and** equals a
  community the actor moderates. A "flat" community announcement (`Scope ==
  Community`, `CommunityId == null`) and a `Scope == Public` announcement are
  **out of scope for the moderator branch** (they have no target to moderate) —
  those go to GlobalAdmin / Translator only. `AccessVia.Moderator`.
- **A plain member** (no such claim) may never add a translation.

The audit row records `Action = "announcementtranslation.add"`,
`TargetKind = "announcement"`, `TargetId = <announcementId>` — the announcement
the row is *about* (the target), not the actor (ADR 0017/0022 convention).

The Web detail page (`/announcements/{id}`) gains the add-translation form
gated on the same standing (GlobalAdmin / Translator / a qualifying community
Moderator) and, per ADR 0027, the **authored-in variant + click-to-swap
chips** over the authored-in and each translation. No new `AccessVia` member is
added: the community-moderator lane maps to `AccessVia.Moderator` (the existing
community-scope member), exactly as a community-moderator's post action does —
the new dimension is *which announcement*, not a new authorization kind.

## Consequences

- The platform's last UGC surface without the translation lane gains it, so a
  deployment can be exercised in more than one language end-to-end — the stated
  goal the M6 milestone pulled forward (ADR 0005) was for.
- **One row per (announcement, language) pair.** The
  `(AnnouncementId, LanguageCode)` unique index enforces this at the DB layer,
  mirroring the `PostTranslation` / `ReplyTranslation` / `GroupTranslation` /
  `CommunityTranslation` business-key convention. (The auto-derived index name
  exceeds Postgres's 64-char `NAMEDATALEN` limit, so the schema gives it an
  explicit short name — `ann_tr_uidx_ann_lang`.)
- **Add-only, same as posts/replies.** No edit/remove of a translation; a wrong
  translation is corrected by the same standing adding the right one, which the
  unique index prevents duplicating per language.
- **The standing is the ADR 0022 matrix plus one case.** The community-moderator
  branch is new (posts/replies have no moderator translation lane); it reuses
  the existing `moderator:{CommunityId}` claim and `AccessVia.Moderator`, so no
  role or `AccessVia` change is required. A flat-community or Public
  announcement excludes it — a moderator cannot translate a platform-wide or
  community-less announcement.
- **Audit-by-default** is preserved: every added translation writes its own
  `AccessAudit` row (the C3 write-lane convention) recording the announcement,
  the actor, the standing branch, and the language added.
- **Display is ADR 0027's, not a new one.** The detail page reuses the existing
  chip-swap mechanism (authored-in as the default visible variant, a chip per
  translation) — the announcements surface adds no new display rule.
