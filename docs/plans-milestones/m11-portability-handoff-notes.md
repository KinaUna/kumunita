# M11 handoff notes

One `## U#` section per unit, **appended, never rewritten** (the shared
scratch tier of the three-tier contract — see the register header). Each
entry: files written/touched, decisions locked or refined (with the
design-doc / ADR section it maps to), anything that drifted from the plan's
text, and the next unit's entry reads.

The register is `docs/plans-milestones/plan-m11-portability.md`. Each unit
ships its own self-contained plan in `in-progress/portability-uNN.md`; when
a unit is done its plan file moves to `done/`. The **next unit's** agent
reads its own unit plan + this file's most recent `## U#` section + its
entry reads — it does not re-derive the register.

## Kickoff — M11 is the in-progress milestone; register + unit plans authored

- **What this is.** M11 (Portability — import/export) is already the single
  `StatusNext` milestone in `Milestones.cs` (the M10 U07 close moved it
  there; no M11 work has started — `in-progress/` is empty and there is no
  `src/Kumunita.Core` `Portability/` context, no export/import code, no
  `manifest.json` shape, no archive reader/writer anywhere in the tree:
  grep-confirmed greenfield). This kickoff records the register's authoring,
  not a milestone-status change (M11 is already `StatusNext` — the flip
  happens at **U07 close**, when M11 → `StatusDone` and M12 → `StatusNext`).
- **Files touched (this kickoff: the plan tier only):**
  - `docs/plans-milestones/plan-m11-portability.md` — the unit register
    (U00–U07), [PROPOSED] D1–D10 + C-M11·1–8 + F1–F5, per-unit Goal /
    Entry reads / Deliverables / Exit, + the Deferred lanes.
  - `docs/plans-milestones/in-progress/portability-u00.md` …
    `portability-u06.md` — the six self-contained unit plans (U00–U06;
    U07's close is defined in the register's `## U07` section directly —
    the close unit reads it there, no separate in-progress file).
  - `docs/plans-milestones/m11-portability-handoff-notes.md` — this file.
- **Open veto window:** D1–D10 in the register are the [PROPOSED] set the
  user can still change cheaply **before U00 runs**. After U00 they are
  locked by `docs/design/m11-portability-design.md` + ADR 0108.
- **ADR number for M11:** **0108** (the index ran 0001–0107; `0108` is free
  — verified against `docs/adr/README.md`; `0107` is the M10 PWA lane, the
  current highest). U00 will author
  `docs/adr/0108-portability-import-export.md` + the `docs/adr/README.md`
  row.
- **Grounding facts verified at kickoff (U00's entry reads should re-verify,
  not trust this):** the closed document inventory spans nine `*DocTypes`
  surfaces (`M1DocTypes` / `M3DocTypes` / `M4DocTypes` / `M5DocTypes` /
  `M6DocTypes` / `M9DocTypes` / `MediaDocTypes` / `TagDocTypes` /
  `PageDocTypes`); the media volume is content-addressed
  (`MediaObject.Id` = the content hash, bytes at `{root}/{Id[0..2]}/{Id}`,
  `IMediaFileStore` / `IMediaStore`); the operator-surface house pattern is
  `[Authorize(Roles = GlobalAdmin)]` + one `AccessAudit` row `Via = Admin`
  (ADR 0101/0105/0106, `AdminMessagingController` the shape to mirror,
  `AttachmentController` the `Content-Disposition: attachment` streaming
  shape the `export` action reuses); `System.IO.Compression.ZipArchive` is
  in the .NET 10 BCL (no new package — the lean-stack + tsc-only discipline
  holds); the `LocaleSettings` + language-catalog state lives in
  `Localization/LanguageCatalog.cs` (the `config.json` snapshot source);
  `Milestones.cs` M11 = `StatusNext`, M12 iCal = `StatusPlanned` (so the
  U07 flip is M11 → `StatusDone`, M12 → `StatusNext`).
- **Not touched (historical records, per the "don't edit done records"
  discipline):** the ARCHITECTURE.md value-chain row ("outcome + world
  seams — the loop closes into the residents' lives") + line 784 deferral
  ("Cross-neighborhood migration: versioned JSON export/import service (not
  built yet)") + the README §Deferred "Cross-neighborhood federation"
  deferral — those are the *source* of M11's scope (whole-instance now,
  per-slice / cross-instance deferred); M11's design doc + ADR 0108
  *supersede* the deferral by owning the whole instance and re-naming the
  follow-on lanes (per-slice / cross-instance, import-merge,
  backup-automation) in the register's Deferred lanes list.
- **Next:** U00 (see `in-progress/portability-u00.md`).

## U00 — Lock the design (2026-09-27)

**Exit status:** `dotnet build Kumunita.slnx -c Debug` → **green** (docs-only
change; the single `xUnit2013` warning at
`tests/Kumunita.Core.Tests/MessagingServiceTests.cs(204)` is pre-existing and
unrelated to docs).

### Files written / touched

- **Created** `docs/design/m11-portability-design.md` — the **primary tier**
  (Status **LOCKED**; ADR 0108; every template section incl. the mandatory
  **Seams & contracts**; §manifest / §layout / §inventory / §principals /
  §config / §validate / §surface; **Invariants C-M11·1–8**; **§pinned
  tests**; §kw-l; §drift-guard).
- **Created** `docs/adr/0108-portability-import-export.md` — the **decision
  record** (Status **Accepted**; Amends 0004/0006/0011/0005/0028/0034/0015/
  0101/0105; **D1–D10** each with *Forbids*; Consequences incl. the deferred
  lanes + the supersedes record).
- **Touched** `docs/adr/README.md` — **one** index row added after the
  `0107` row: `| 0108 | M11: Portability (import/export) — … | Accepted |`.
- **Touched** this handoff note — this `## U00` section appended (existing
  sections untouched, per the "never rewrite existing handoff sections"
  rule).
- **Not touched** (per the unit's scope): no code, no tests, no unit-plan
  edit, no file move; `Milestones.cs` / README / `docs/STATUS.md` /
  `docs/ARCHITECTURE.md` / `MilestonesTests.cs` are left for the **U07
  flip** (C-M11·8).

### Decisions locked (verbatim) → design-doc / ADR mapping

The D1–D10 set is **locked verbatim** (no veto, no re-order); four D-item
framings were refined in favor of the source (see the Drift section below,
recorded in the design doc §drift-guard):

| D-item | One-liner | Design-doc § | ADR § |
|--------|-----------|--------------|-------|
| D1 | Scope = whole-instance export + import (restore) | §context, §goals/§non-goals, §world-seams | D1 |
| D2 | Versioned ZIP, BCL-only (no new package) | §manifest, §layout, §seams | D2 |
| D3 | Identity travels as the graph, never the secrets (C-M11·2) | §principals, §validate, §invariants | D3 |
| D4 | Validate-then-apply, fail-closed, atomic (C-M11·4) | §validate, §feedback-loops | D4 |
| D5 | GlobalAdmin admin plane, audited, quiet (C-M11·6/7) | §surface | D5 |
| D6 | Media travels content-addressed + whole (C-M11·3) | §layout, §inventory (`MediaObject`) | D6 |
| D7 | Closed inventory + import order pinned from source | §inventory | D7 |
| D8 | New `Portability` context — operator lane, zero new auth (C-M11·7) | §parts, §seams, §invariants | D8 |
| D9 | Tests: Core round-trip + no-secret pin + fail-closed pin + Web pins | §pinned tests | D9 |
| D10 | kw-l keys + the deferred lanes | §kw-l, §world-seams | D10 |

**Invariants locked:** C-M11·1–8 (C-M11·2 no-secret = the single most
load-bearing M11 invariant; C-M11·7 zero new authorization surface = the
operator-plane pin). **FACES locked:** F1–F5.

### The locked artifacts a later unit copies verbatim

- **The closed document inventory (D7) — 44 content docs in 9 order-groups**,
  parents before children, with the reference map. **U01 copies this as
  data** into the registry. Order 1 base units: `Group`, `Component`,
  `MediaObject`, `Tag`. Order 2 identity graph: `Profile`,
  `DelegationGrant`, `GuardianLink`, `GroupMembership`, `GroupInvitation`,
  `GroupJoinRequest`, `ModeratorAssignment`, `ComponentMembership`. Order 3
  M1 translations: `TranslationResource`, `GroupTranslation`,
  `CommunityTranslation`, `TagTranslation`. Order 4 M3 content: `Post`,
  `PostReply`, `PostTranslation`, `ReplyTranslation`, `Report`,
  `Announcement`, `AnnouncementTranslation`, `AnnouncementComment`. Order 5
  M4: `Event`, `EventRsvp`, `EventTranslation`. Order 6 M5/PL:
  `ProjectGoal`, `Project`, `TodoItem`, `KanbanBoard`, `KanbanLane`,
  `BoardItemPlacement`, `TodoTranslation`, `BoardTranslation`,
  `ProjectTranslation`, `TodoComment`. Order 7 M6: `Notification`,
  `NotificationPreference`, `NotificationSubscription`. Order 8 M9:
  `Conversation`, `Message`. Order 9 PG: `Page`, `PageTranslation`. The full
  reference map + the excluded-docs table are in the design doc §inventory.
- **The manifest format (C-M11·1)** — `format`
  (`kumunita/portability/1`), `produced_by`, `produced_at`,
  `source_version?`, `community_name`, `principals`, `content_docs`,
  `media_objects`, `config`. See design doc §manifest.
- **The no-secret principals shape (C-M11·2)** — `subjectId` / `username` /
  `email` / `normalizedEmail` / `displayName` / `roles` / lockout+state
  flags; **excluded** credential fields (password hash, security stamp,
  access/refresh tokens, recovery codes, Identity sign-in records). See
  design doc §principals.
- **The config shape (D2/D7)** — `community` (name / supportEmail),
  `locale` (defaultLanguageCode / defaultTimezone / defaultDateFormat /
  the resident flags), `languages` (the catalog). See design doc §config.
- **The validate checks + apply order (C-M11·4)** — (a) the `format` is one
  the build understands, (b) each `docs/{Type}.json` deserializes into its
  declared POCO set, (c) **referential integrity** — every referenced id
  resolves to a row within the archive, (d) the media bytes are all present
  and content-match their path; **any failure ⇒ zero writes**. Apply order:
  Identity principals → domain docs (inventory order) → media bytes →
  config. See design doc §validate.

### Drift from the plan's text (resolved in favor of the source, per the unit's rule)

The D1–D10 set was locked verbatim **except** where the *source*
(the `*DocTypes` surfaces, `LanguageCatalog`, `CommunityOptions`)
contradicted a D-item's "every registered domain doc type" / "the community
name" framing. All resolved in favor of the source, recorded in the design
doc §drift-guard:

1. **The D7 inventory is the content + identity-graph + localization set
   (44 docs), not literally "every registered doc type."** `IdentityToken`
   is excluded — it holds a high-entropy secret `Token` (C-M11·2); see
   §drift-guard 1.
2. **`OutboxEmail` / `EmailDeadLetter` / `AccessAudit` /
   `AuditPurgeSummary` are excluded** — operational state, not resident
   content (the D1 restore semantic); see §drift-guard 2.
3. **`LocaleSettings` + `LanguageCatalog` travel via `config.json`, not as
   `docs/` rows** (D2 claims them for config; D7's "every doc type" would
   double-travel them); validate's integrity loop reads `config.json` for
   `LanguageCode` targets; the apply order is unchanged; see §drift-guard 3.
4. **`community_name` = `CommunityOptions.Name`** (a config object — ADR
   0002 `Community__*` env vars); there is no `CommunityName` domain doc, so
   D2's "the community name" is grounded in the config object; see
   §drift-guard 4.

**Net:** the inventory is closed + explicit + source-verified; the five
excluded docs are **named** (not an open-ended "everything"); `config.json`
is the single home of the instance identity + localizations;
`MediaObject` **is** a content doc (its own doc-comment names the
"surviving catalog reference" role) and travels with the content.

### ADR number + index

- **ADR number 0108 confirmed free** (the index ran 0001–0107; `0107` = the
  M10 PWA lane, the current highest).
- **One index row** added to `docs/adr/README.md` after the `0107` row — the
  only other `0108` touch.

### Next unit's entry reads (U01 — the closed document inventory + order, data)

Per the register's U01 entry reads:
- `docs/design/m11-portability-design.md` — §manifest, §layout, §inventory,
  §drift-guard (the data U01 implements verbatim).
- `src/Kumunita.Core/DependencyInjection.cs` — the `*DocTypes` registration
  surfaces (`M1DocTypes` / `M3DocTypes` / … / `PageDocTypes`).
- `src/Kumunita.Core/Identity/*.cs` — the M1 domain document POCOs (the
  `subjectId` / `AvatarId` / `OwnerId` / `ComponentId` field shapes the
  reference map uses).
- `src/Kumunita.Core/Posts/*.cs` — the M3 content POCOs (`AuthorId` /
  `ComponentId` / `GroupId` / `ImageIds` / `TagIds`).
- `src/Kumunita.Core/Media/*.cs` — `IMediaFileStore` / `IMediaStore` (the
  `RootPath` + `{Id[0..2]}/{Id}` layout D6 pins).
- `src/Kumunita.Core/Kumunita.Core.csproj` — confirm **no new package** is
  needed (the BCL `System.IO.Compression` is in the framework; D2's "no new
  package" pin).
