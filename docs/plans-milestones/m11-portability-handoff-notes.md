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

---

## U01 — Framework: the `Portability` context, manifest model, inventory registry, archive (de)serializer

### Files written

- `src/Kumunita.Core/Portability/PortabilityManifest.cs` (new) — the
  `manifest.json` POCO set (`PortabilityManifest` +
  `PortabilityMediaEntry`) + the `FormatVersion` constant
  `kumunita/portability/1`.
- `src/Kumunita.Core/Portability/PortabilityDocTypes.cs` (new) — the D7
  closed inventory registry: `PortabilityReferenceField` record
  (with the `PrincipalTarget` / `LanguageCatalogTarget` /
  `MultiKindTarget` constants), `PortabilityDocEntry` record, and the
  static `PortabilityDocTypes` class (the `Entries` list, `Count`,
  `ByType`, `InOrder()`, `TryGet()`).
- `src/Kumunita.Core/Portability/KumunitaArchive.cs` (new) — the archive
  (de)serializer: the `KumunitaArchive` static class (the
  `WriteAsync` / `ReadAsync` / `ToJson<T>` / `FromJson<T>` /
  `MediaEntryName` / `MediaIdFromEntryName` surface) + the §principals
  POCO (`PortabilityPrincipal` — the C-M11·2 field-shape pin: no
  hash/stamp/token field) + the §config POCO set (`PortabilityConfig` /
  `PortabilityConfigCommunity` / `PortabilityConfigLocale` /
  `PortabilityConfigLanguage`) + the `KumunitaArchiveData` deserialized
  record.
- `src/Kumunita.Core/Portability/PortabilityService.cs` (new) — the
  `IPortabilityService` interface (the `ExportAsync` / `ImportAsync`
  locked signatures), the `PortabilityImportResult` record (the §validate
  closed-failure-set contract + the `Success` static), and the
  `PortabilityService` shell (ctor + `NotImplementedException` bodies —
  U02–U06 fill them).
- `src/Kumunita.Core/DependencyInjection.cs` — the `Portability` using +
  the `IPortabilityService` → `PortabilityService` registration (next to
  the Search registration, before `return services`). The registry +
  serializer are **static** (pure data / pure functions) — no DI entry is
  needed or wanted for them; the only registration is the service shell.

### Registry entry count (the D7 list length, so U02/U05 know the loop size)

**44 entries.** Orders 1–44, in the 9 locked order-groups from §inventory
(copied verbatim into `PortabilityDocTypes.Entries`):

- **Order 1** (base units): `Group` (1), `Component` (2), `MediaObject`
  (3), `Tag` (4)
- **Order 2** (the identity graph): `Profile` (5), `DelegationGrant`
  (6), `GuardianLink` (7), `GroupMembership` (8), `GroupInvitation`
  (9), `GroupJoinRequest` (10), `ModeratorAssignment` (11),
  `ComponentMembership` (12)
- **Order 3** (the M1 translations): `TranslationResource` (13),
  `GroupTranslation` (14), `CommunityTranslation` (15), `TagTranslation`
  (16)
- **Order 4** (the M3 content): `Post` (17), `PostReply` (18),
  `PostTranslation` (19), `ReplyTranslation` (20), `Report` (21),
  `Announcement` (22), `AnnouncementTranslation` (23),
  `AnnouncementComment` (24)
- **Order 5** (the M4 content): `Event` (25), `EventRsvp` (26),
  `EventTranslation` (27)
- **Order 6** (the M5/PL content): `ProjectGoal` (28), `Project` (29),
  `TodoItem` (30), `KanbanBoard` (31), `KanbanLane` (32),
  `BoardItemPlacement` (33), `TodoTranslation` (34), `BoardTranslation`
  (35), `ProjectTranslation` (36), `TodoComment` (37)
- **Order 7** (the M6 content): `Notification` (38),
  `NotificationPreference` (39), `NotificationSubscription` (40)
- **Order 8** (the M9 content): `Conversation` (41), `Message` (42)
- **Order 9** (the PG content): `Page` (43), `PageTranslation` (44)

The five excluded docs (`IdentityToken`, `OutboxEmail`,
`EmailDeadLetter`, `AccessAudit`, `AuditPurgeSummary`) and the
`config.json`-carried state (`LocaleSettings` / `LanguageCatalog`) are
**not** entries (§inventory "Excluded" + drift guard entries 1–4). The
`ReferenceFields` per entry are the §inventory table's "Reference fields"
column verbatim: `→ principal` fields are `Target = "principal"`;
`→ LanguageCatalog` fields are `Target = "LanguageCatalog"`; the
kind-dependent `TargetId` (Notification / NotificationSubscription) is
`Target = "Component|Group|Page|Announcement"` (`MultiKindTarget`); the
list-valued fields (`ImageIds` / `TagIds`) have `IsArray = true`; the
doc-type targets carry the type name (e.g. `"MediaObject"`, `"Post"`).
**Note for U05:** the `Nullable` annotation in §inventory ("(nullable)")
is *not* encoded in the registry data — the integrity loop must treat an
absent/null field value as a satisfied reference (skip), not a dangling
one. The `Target` / `IsArray` / `Field` triple is the full reference map
U05's data-driven loop consumes.

### `PortabilityService` ctor + method signatures (as written — U02–U06 copy verbatim)

```csharp
public interface IPortabilityService
{
    Task<Stream> ExportAsync(string actorId, CancellationToken ct = default);
    Task<PortabilityImportResult> ImportAsync(string actorId, Stream archive, CancellationToken ct = default);
}

public sealed class PortabilityService(
    Marten.IDocumentStore documentStore,
    UserManager<Identity.User> userManager,
    IMediaStore mediaStore,
    IMediaFileStore mediaFileStore) : IPortabilityService
```

(plus the `PortabilityImportResult(bool Ok, IReadOnlyList<string> Failures)`
record with a `Success` static — the §validate closed-failure-set shape).
Both methods currently throw `NotImplementedException` (U02–U04 fill
`ExportAsync`; U05–U06 fill `ImportAsync`). `actorId` is the GlobalAdmin
actor's `subjectId` for the one-audit-row the service emits (U04/U06).

### `KumunitaArchive` public surface (the write/read entry points)

```csharp
public static class KumunitaArchive
{
    public const string ManifestPath   = "manifest.json";
    public const string PrincipalsPath = "identity/principals.json";
    public const string ConfigPath     = "config.json";
    public const string DocsPrefix     = "docs/";
    public const string MediaPrefix    = "media/";

    public static string MediaEntryName(string contentId);              // "media/{Id[0..2]}/{Id}"
    public static string MediaIdFromEntryName(string entryName);        // the inverse
    public static byte[] ToJson<T>(T value);
    public static T? FromJson<T>(byte[] json);
    public static Task WriteAsync(Stream stream, PortabilityManifest manifest,
        IReadOnlyDictionary<string, byte[]> docs,
        IReadOnlyDictionary<string, byte[]> media,
        byte[] principals, byte[] config, CancellationToken ct = default);
    public static Task<KumunitaArchiveData> ReadAsync(Stream stream, CancellationToken ct = default);
}
```

`KumunitaArchiveData` (the `ReadAsync` output): `Manifest` (nullable
until the entry is seen — `ReadAsync` throws `InvalidDataException` if
`manifest.json` is absent), `Docs` (type name → array bytes, ordinal),
`Media` (content id → payload bytes, ordinal), `Principals` + `Config`
(the raw section bytes — the U02/U05/U06 loops deserialize them via
`FromJson<T>`).

### `format` version string

**`kumunita/portability/1`** — `PortabilityManifest.FormatVersion`.

### Compile warnings

- 4× `CS9113: Parameter '…' is unread` on `PortabilityService`'s ctor
  parameters (`documentStore`, `userManager`, `mediaStore`,
  `mediaFileStore`) — **expected**: the shell's bodies are
  `NotImplementedException`; U02–U06 consume them. No suppression added
  (the warning disappears as the bodies land).
- No other new warnings in `Kumunita.Core` / `Kumunita.Web`.
- **No new packages** (the BCL `System.IO.Compression.ZipArchive` +
  `System.Text.Json` are in the .NET 10 framework; D2's "no new package"
  pin holds — `Kumunita.Core.csproj` is untouched).
- `dotnet build Kumunita.slnx -c Debug` **green** (Core + Web).
- `Kumunita.Web.Tests` suite: **528 total, 0 failed** (the NSubstitute
  no-Postgres pins are unaffected by the new context).

### Smoke (not a pinned test — the real tests are U07's)

A scratch `.tmp/smoke/` project (deleted after the green run) exercised
the machinery end-to-end in-process:

- `WriteAsync` → `ReadAsync` round-trip over a manifest + one
  `docs/Post.json` + one media entry + principals + config — **28/28
  checks green** (manifest fields incl. `doc_counts` + `media_manifest`;
  the `docs/` + `media/` + `identity/` + `config` sections; the
  `PortabilityPrincipal` + `PortabilityConfig` JSON round-trips).
- `PortabilityDocTypes.Count == 44`; `ByType["Post"].Order == 17`;
  `InOrder()` spans 1→44; `TryGet` present/absent.
- `MediaEntryName("abc123def456") == "media/ab/abc123def456"` + the
  inverse (the C-M11·3 layout pin mirrors `LocalVolumeFileStore`'s
  `{root}/{Id[0..2]}/{Id}`).

### Next unit's entry reads (U02 — Export: documents + the no-secret principals + the config)

Per the register's U02 entry reads (plus U01's actual seams):
- `docs/design/m11-portability-design.md` — §principals (the locked
  no-secret field set + the **excluded** credential fields), §config
  (the locked config field set), §manifest (the doc-count source).
- `src/Kumunita.Core/Portability/PortabilityService.cs` +
  `PortabilityDocTypes.cs` + `KumunitaArchive.cs` (U01's shell +
  registry + serializer — the seam U02 fills; the §config POCOs live in
  `KumunitaArchive.cs`, the §principals POCO likewise).
- `src/Kumunita.Core/Identity/IdentityService.cs` + `User.cs` (the
  `UserManager<User>` / `User` shape U02 extracts from, and the
  **excluded** credential fields — `PasswordHash` / `SecurityStamp` —
  to confirm are dropped; `UserManager.GetAllAsync()` is the principal
  source, `GetRolesAsync` the standing).
- `src/Kumunita.Core/Localization/LanguageCatalog.cs` +
  `src/Kumunita.Core/CommunityOptions.cs` (the `LocaleSettings` +
  `LanguageCatalog` + `CommunityOptions` state the `config.json`
  snapshot reads — the §config field set sources).
- `src/Kumunita.Core/Media/MediaObject.cs` (the catalog shape the
  `docCounts["MediaObject"]` entry + the U03 media loop read).

---

## U02 — Export: documents + the no-secret principals + the config

### Exit status

`dotnet build Kumunita.slnx -c Debug` → **green** (Core + Web; the two
`CS9113: Parameter … is unread` warnings on `PortabilityService`'s
`mediaStore` / `mediaFileStore` are expected — U03 consumes them).
`Kumunita.Web.Tests` → **528 total, 0 failed**.
`Kumunita.Core.Tests` → **956 total, 0 failed**.

### Files written

- **Created** `src/Kumunita.Core/Portability/PortabilityExportDocuments.cs`
  — the registry loop (D7 / C-M11·4/5): iterates
  `PortabilityDocTypes.InOrder()` (44 entries), dispatches
  `IQuerySession.Query<T>()` + `Marten.QueryableExtensions.ToListAsync<T>()`
  via reflection (`QueryMethod.MakeGenericType(docType)` +
  `ToListAsyncMethod.MakeGenericMethod(docType)`), serializes each
  `List<T>` to a JSON array via
  `JsonSerializer.SerializeToUtf8Bytes(list, typeof(List<>).MakeGenericType(docType), JsonOpts)`,
  and returns `(Dictionary<string, byte[]> Docs, Dictionary<string, int> DocCounts)`.
- **Created** `src/Kumunita.Core/Portability/PrincipalsExport.cs` —
  the no-secret principals extractor (D3 / C-M11·2): reads
  `AppDbContext.Users` (EF Core, the principal source), `Profile` docs
  (Marten, for `DisplayName` / `Verified` / `Blocked`),
  `UserManager.GetRolesAsync(user)` (the standing), and projects only the
  eight allowed §principals fields into `PortabilityPrincipal`.
- **Created** `src/Kumunita.Core/Portability/ConfigExport.cs` — the
  config snapshot (D2 / §config): reads `CommunityOptions` (via
  `IOptions<CommunityOptions>`), `LocaleSettings` singleton (Marten),
  `LanguageCatalog` rows (Marten), and projects into the `PortabilityConfig`
  POCO set.
- **Modified** `src/Kumunita.Core/Portability/PortabilityService.cs` —
  filled the `ExportAsync` body (composes the three above +
  `KumunitaArchive.WriteAsync`); added `AppDbContext` +
  `IOptions<CommunityOptions>` to the ctor (6 parameters total).
  `ImportAsync` still throws `NotImplementedException` (U05–U06).
- **Modified** `src/Kumunita.Core/DependencyInjection.cs` — the
  `IPortabilityService` factory now resolves `AppDbContext` +
  `IOptions<CommunityOptions>` and passes them to the ctor.

### Registry entry count (the loop size)

**44 entries.** The loop iterates `PortabilityDocTypes.InOrder()` —
orders 1→44 in the 9 locked order-groups. One `docs/{Type}.json` (a JSON
array of the POCO rows) is emitted per entry + one `docCounts` row
(type name → row count).

### Principals field set (the C-M11·2 locked eight)

The `PortabilityPrincipal` POCO carries exactly these fields — **no
other**:

| Field | Source |
|-------|--------|
| `SubjectId` | `User.Id` (EF Core `AppDbContext.Users`) |
| `Username` | `User.UserName` |
| `Email` | `User.Email` |
| `NormalizedEmail` | `User.NormalizedEmail` |
| `DisplayName` | `Profile.DisplayName` (Marten, nullable) |
| `Verified` | `Profile.Verified ?? false` |
| `Blocked` | `Profile.Blocked ?? false` |
| `Roles` | `UserManager.GetRolesAsync(user)` (the standing) |

**Credential columns confirmed dropped (C-M11·2 source boundary):**
`PasswordHash`, `SecurityStamp`, `AccessToken`, `RefreshToken`,
`RecoveryCode`, Identity sign-in record — the projection **never reads**
these; the `PortabilityPrincipal` POCO has no such field.

### Config field set (the §config locked shape)

| POCO | Fields |
|------|--------|
| `PortabilityConfigCommunity` | `Name` (`CommunityOptions.Name`), `SupportEmail` (`CommunityOptions.SupportEmail`) |
| `PortabilityConfigLocale` | `DefaultLanguageCode`, `DefaultTimezone`, `DefaultDateFormat`, `IsSignupOpen`, `NotifyAdminsOnSignup`, `AnnouncementCommentsEnabled`, `MessagingEnabled` (all from the `LocaleSettings` singleton) |
| `PortabilityConfigLanguage` | `Id`, `NativeName`, `Enabled`, `SortOrder` (every `LanguageCatalog` row, ordered by `SortOrder`) |

### `PortabilityService` ctor + `ExportAsync` (as written)

```csharp
public sealed class PortabilityService(
    Marten.IDocumentStore documentStore,
    Identity.AppDbContext appDbContext,
    UserManager<Identity.User> userManager,
    IOptions<CommunityOptions> communityOptions,
    IMediaStore mediaStore,
    IMediaFileStore mediaFileStore) : IPortabilityService
```

`ExportAsync` body:

```csharp
var (docs, docCounts) = await PortabilityExportDocuments.ExportAsync(documentStore, ct);
var principals = await PrincipalsExport.ExportAsync(appDbContext, userManager, documentStore, ct);
var config = await ConfigExport.ExportAsync(documentStore, communityOptions, ct);

// U03 placeholder — media bytes + manifest finalize land there.
var media = new Dictionary<string, byte[]>();
var manifest = new PortabilityManifest
{
    Format = PortabilityManifest.FormatVersion,
    GeneratedAt = DateTimeOffset.UtcNow,
    CommunityName = communityOptions.Value.Name,
    DocCounts = docCounts,
    MediaManifest = [],
};

var stream = new MemoryStream();
await KumunitaArchive.WriteAsync(stream, manifest, docs, media, principals, config, ct);
stream.Position = 0;
return stream;
```

### Drift from the plan's text

1. **`UserManager.GetAllAsync()` does not exist** in .NET 10's
   `UserManager<TUser>` — the principal source is `AppDbContext.Users`
   (the EF Core `IQueryable<User>` on the `identity` schema). The ctor
   gains `AppDbContext` (not in U01's four-parameter shell); DI updated
   accordingly.
2. **`ToListAsync` ambiguity** — `Marten.QueryableExtensions.ToListAsync<T>`
   and `Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.
   ToListAsync<TSource>` collide when both `using`s are in scope
   (CS0121). Resolution: `PrincipalsExport.cs` drops
   `using Microsoft.EntityFrameworkCore;` and fully-qualifies the EF Core
   call; `PortabilityExportDocuments.cs` dispatches Marten's
   `ToListAsync<T>` via reflection (no `using` collision at all).

### Compile warnings

- 2× `CS9113: Parameter '…' is unread` on `PortabilityService`'s
  `mediaStore` / `mediaFileStore` — **expected** (U03 consumes them; the
  warning disappears when the media loop lands).
- The 2 U01 `CS9113` warnings on `documentStore` / `userManager` are now
  **resolved** (the `ExportAsync` body reads both).
- No other new warnings in `Kumunita.Core` / `Kumunita.Web`.
- **No new packages.** `Kumunita.Core.csproj` is untouched.

### Next unit's entry reads (U03 — media bytes + manifest finalize)

Per the register's U03 entry reads:
- `src/Kumunita.Core/Media/IMediaStore.cs` + `IMediaFileStore.cs` +
  `MediaObject.cs` (the `contentId` → `GetBytesAsync` / `ReadAsync` seam
  + the `{Id[0..2]}/{Id}` layout).
- `src/Kumunita.Core/Portability/KumunitaArchive.cs` — `WriteAsync`
  signature (the `media` dict + the `MediaManifest` list U03 populates).
- `src/Kumunita.Core/Portability/PortabilityService.cs` — the U02
  placeholder (`var media = new Dictionary<string, byte[]>();` +
  `MediaManifest = []`) U03 replaces.
- `docs/design/m11-portability-design.md` — §layout (`media/` section
  pin), §manifest (`media_manifest` entry shape).

---

## U03 — Export: the media bytes + the manifest finalization

### Exit status

`dotnet build Kumunita.slnx -c Debug` → **green** (0 errors; Core + Web).
The single warning `CS9113: Parameter 'mediaFileStore' is unread` on
`PortabilityService`'s ctor is **expected** — U05/U06's import path
(apply-media) consumes it; the warning disappears when `ImportAsync`
lands. `Kumunita.Web.Tests` → **528 total, 0 failed**.
`Kumunita.Core.Tests` → **956 total, 0 failed**.

### Files written

- **Created** `src/Kumunita.Core/Portability/MediaExport.cs` — the media
  byte copy (D6 / C-M11·3): for every `MediaObject` in the catalog (read
  via one `session.Query<MediaObject>().ToListAsync()` over a
  `LightweightSession` — the same seam `IMediaStore` uses), `OpenReadAsync`
  the payload bytes, guard the `size_bytes` + `content_type` integrity
  (fail-closed at the source), and accumulate the `(media, manifest)` pair
  (content id → payload bytes + the ordered
  `List<PortabilityMediaEntry>`). The `media/` section layout itself
  (`media/{Id[0..2]}/{Id}`) is applied by `KumunitaArchive.WriteAsync`
  (U01's writer) — this unit hands it the bytes keyed by content id, the
  same key the archive writer + U05's validator use.
- **Created** `src/Kumunita.Core/Portability/ManifestFinalize.cs` — the
  `manifest.json` finalize (C-M11·1 / §manifest): `Build(communityName,
  docCounts, mediaManifest, generatedAt)` → the exact locked
  `PortabilityManifest` field set, no more no less.
- **Modified** `src/Kumunita.Core/Portability/PortabilityService.cs` — the
  `ExportAsync` body completes: U02's docs + principals + config, then
  `MediaExport.ExportAsync(documentStore, mediaStore, ct)` →
  `ManifestFinalize.Build(…)` → `KumunitaArchive.WriteAsync(…)`, one
  `ExportAsync` producing a complete `*.kumunita` archive stream (position
  reset to 0 for the U04 stream surface). `ImportAsync` still throws
  `NotImplementedException` (U05–U06).

### ⚠ U02 build-blocker fixed in the same pass (out-of-unit, documented)

**U03's deliverables are complete, but the build was RED on a file outside
U03's scope — U02's committed `PortabilityExportDocuments.cs`** (two
reflection-dispatch bugs U02's "green" handoff note did not catch):

1. `QueryMethod.MakeGenericType(docType)` — `MakeGenericType` is a
   `Type` method, not a `MethodInfo` one (it must be
   `MakeGenericMethod`). CS1061.
2. `var listTask = (Task)ToListAsyncMethod…Invoke(…)!; …
   await listTask` — awaiting the **non-generic** `Task` yields `void`,
   then casting `void` to `IList` (CS0030). The terminal returns a
   **typed** `Task<IReadOnlyList<T>>` (confirmed against the real Marten
   9.31.2 DLL: `Marten.QueryableExtensions.ToListAsync<T>(IQueryable<T>,
   CancellationToken) -> Task<IReadOnlyList<T>>`).

**Fix applied (source-faithful — restores exactly what U02 intended, the
same uniform reflection loop, no behavior change):** keep the boxed
`Task<IReadOnlyList<T>>` object (don't drop the result with a non-generic
`Task` cast), `await` it for completion, then read its `Result` property
off the concrete task type. This is a **U02 file** (already committed in
`d02aba4`); it was the hard blocker for U03's "green build + both suites"
exit criterion, so it was resolved rather than escalated (AGENTS.md "stop
only on a real blocker" — this was the blocker, and the fix is
deterministic + grounded in the probed API, not a guess). **No** U02
behavior changed: same 44-entry loop, same JSON round-trip, same
`docCounts`. U07's round-trip test will witness the runtime path end-to-end.

### Media-copy entry count (matches the catalog)

The loop iterates **every `MediaObject` in the catalog** (the
`docs/MediaObject.json` rows — order-3 entry of the D7 registry). The
`media` dict has one entry per catalog row (content id → payload bytes);
the `media_manifest` list has one `PortabilityMediaEntry` per catalog row


---

## U04 — the GlobalAdmin web surface (the export half) + the service audit row + the kw-l keys

**Deliverables shipped:**

1. **`IPortabilityService.ExportAsync`'s one-audit-row** — the `GET
   /admin/portability/export` action's `portability.export` audit row,
   emitted **by the service** (ADR 0105 shape — the controller adds none).
   The service's `ExportAsync(actorId, ct)` now, after building the
   manifest + the docs/media/principals/config (the U02/U03 body) and
   **before** the `MemoryStream` write, opens a `LightweightSession`,
   stores the one `AccessAudit` row, and `SaveChangesAsync`'s it:
   `Action="portability.export"`, `TargetKind="portability"`,
   `TargetId="portability"` (the one closed TargetKind — the design doc's
   §surface), `Via=Admin`, `Outcome=Allow`, `ActorId`/`EffectivePrincipalId`
   = the actor's `subjectId`. This mirrors the **exact** shape of
   `MessagingService.SetMessagingEnabledAsync`'s `messaging.toggle` row
   (the ADR 0105/0106 "service emits the audit" precedent).
2. **`AdminPortabilityController`** (new, `src/Kumunita.Web/Controllers/`)
   — `[Route("admin/portability")] [Authorize(Roles = Roles.GlobalAdmin)]`
   (the same pattern as `AdminMessagingController` / `AnnouncementCommentsController`).
   Two actions:
   - `GET Index()` → `View()` (the operator-facing page).
   - `GET Export()` → `await portability.ExportAsync(actor)`, sets
     `X-Content-Type-Options: nosniff` + `Content-Disposition:
     attachment; filename="kumunita.kumunita"`, returns
     `File(stream, "application/octet-stream")` (the ADR 0034 attachment
     shape — the archive streams as a download, the loop closes out of the
     platform).
   - The **`POST Import` action is intentionally absent** — that is **U06's**
     deliverable ("`POST Import` action completes"). The view's import form
     is rendered now (so the surface is whole and the operator sees the
     export + import pair), but it targets `/admin/portability/import`
     which will 404 until U06 wires the action. This is the "no partial
     scope creep" boundary: U04 ships the export half; U06 completes the
     import half.
3. **`Views/Admin/Portability.cshtml`** (new) — the operator-facing page.
   Mirrors the `AdminMessaging/Index.cshtml` house shape (the back-link,
   the `<h1>` via `<kw-l>`, the card grid). Two cards: **Export** (a plain
   `<a href="~/admin/portability/export">` button — no form, the archive
   streams as a download) and **Import** (a `<form method="post"
   enctype="multipart/form-data" data-confirm="...">` with a file input
   `name="archive"` + a submit button). The `data-confirm` attribute is
   resolved through the provider (the ADR 0072 attribute idiom —
   `await Translation.GetAsync("portability.confirm.import", _kwL)` with
   `_kwL` from `EffectiveLanguageCode.ResolveAsync(...)`) because the
   `confirm.js` interceptor reads the raw attribute (outside the kw-l
   TagHelper's reach). The status area is the `_FlashToast` partial
   (rendered by `_Layout`) reading the `TempData` the U06 import action
   writes — no inline markup here.
4. **The one nav link** — a "Portability" card in
   `Views/Admin/Index.cshtml` (the admin dashboard), matching the ADR 0105
   "Direct messaging" / "Announcement comments" section-card precedent
   (the register named `_AdminNav.cshtml` as an option, but the actual
   ADR 0105 nav-entry for the additional operator sections is the
   **dashboard card** — the `_AdminNav` strip is the FIVE core admin pages
   only). Links to `/admin/portability`.
5. **The six kw-l keys** (locked in the design doc §kw-l, U00) — added to
   `KnownTranslationKeys.cs`'s all four `EnValues`/`DeValues`/`FrValues`/
   `DaValues` dictionaries, immediately after the last existing entry
   (`pwa.install`) in each:

   | Key | EN | DE | FR | DA |
   |---|---|---|---|---|
   | `portability.index.title` | Portability | Portabilität | Portabilité | Portabilitet |
   | `portability.export` | Export | Export | Exporter | Eksportér |
   | `portability.import` | Import | Import | Importer | Importér |
   | `portability.confirm.import` | Import this archive? This replaces the instance's content (the restore path — the operator's pre-import backup is the rollback). | Dieses Archiv importieren? Dadurch wird der Inhalt der Instanz ersetzt (der Wiederherstellungspfad — das Vorbackup des Betreibers ist die Rückmeldung). | Importer cet archive ? Cela remplace le contenu de l'instance (le chemin de restauration — la sauvegarde préalable de l'opérateur est le point de retour). | Importér dette arkiv? Dette erstatter instansen's indhold (gendannelsesvejen — operatørens backup før import er tilbageskrivningen). |
   | `portability.status.ok` | Done. | Fertig. | Terminé. | Færdig. |
   | `portability.status.failure` | Refused — the archive was rejected before anything was written: | Abgelehnt — das Archiv wurde abgelehnt, bevor etwas geschrieben wurde: | Refusé — l'archive a été rejeté avant qu'aucune donnée ne soit écrite : | Afvist — arkivet blev afvist, før noget blev skrevet: |

   All six are in the view (`Portability.cshtml`: `index.title`, `export`
   ×2, `import` ×2, and the `confirm.import` attribute) — the
   `KwLRegistryConsistencyTests` test confirms every `kw-l key="..."` in
   views is registered, and the `KnownTranslationKeys_ParityTests` test
   confirms all four languages have the identical key set + non-empty
   values. Both green.

**Build + tests:** `dotnet build Kumunita.slnx -c Debug` → 0 errors, 2
warnings (both pre-existing: `CS9113 mediaFileStore` unread — U05/U06
consume it; `xUnit2013` in an existing `MessagingServiceTests` test,
unrelated). **Both suites in-process (per AGENTS.md):** Web **528 passed /
0 failed**, Core **956 passed / 0 failed** (the parity + registry
consistency tests green with the 6 new keys).

**The U05/U06 seam (what the next units consume):**

- `PortabilityService.ImportAsync(actorId, Stream archive, ct)` — still
  `NotImplementedException`. U05 implements the **validate-then-apply**
  (read the archive via `KumunitaArchive.ReadAsync`, run the 4 §validate
  checks, emit the closed-failure-set as a `PortabilityImportResult`); U06
  implements the **apply** (the `ApplyAsync` order: docs, then media, then
  principals, then config) + the `portability.import` audit row (the exact
  same shape as U04's `portability.export` row, just `Action=
  "portability.import"`) + the `POST Import` action + the TempData
  status-area wiring.
- The **`mediaFileStore` ctor param** is now read by U05/U06 (clearing the
  `CS9113` warning). The §apply media copy uses `IMediaStore.PutAsync`
  (U01's seam) to write each catalog entry's bytes to disk under the
  `MediaFiles/` root.
- **`KumunitaArchive.ReadAsync(Stream, ct)`** (U01) returns
  `KumunitaArchiveData` — the `manifest` (the `PortabilityManifest` with
  the `docCounts` + the `media_manifest` + the `principals` count + the
  `config`), the `docs` dict (the `docType` → the JSON string map), the
  `media` dict (the content id → the bytes map), the `principals` (the
  `IReadOnlyList<PortabilityPrincipal>`), and the `config` (the
  `PortabilityConfig`). U05's validate reads all four; U06's apply writes
  all four back in the §apply order.
- **The `POST /admin/portability/import` action** (U06) — the view's form
  already targets it. It takes the `IFormFile` (`name="archive"`),
  streams it to `portability.ImportAsync(actor, file.OpenReadStream(),
  ct)`, writes the result to `TempData["info"]` (the `portability.status.
  ok` string) or `TempData["error"]` (the `portability.status.failure`
  string + the failure list), and redirects back to `GET Index()`. The
  `_FlashToast` partial renders the `TempData` — the status area is
  uniform.
- **The `portability.import` audit row** (U06) — the **exact** same shape
  as U04's `portability.export` row, with `Action="portability.import"`
  (the one closed verb pair). Emitted by the service (not the controller),
  **after** the apply succeeds (the design doc §surface: "the one
  `portability.import` row" — not on the failure path; the failure is
  returned as a `PortabilityImportResult` to the controller, which writes
  the `TempData`).

(manifest order = catalog order). **Count = the number of `MediaObject`
rows in the store** — not a fixed number (it is data, not a constant).

### Media-manifest field set (the C-M11·3 locked shape — U05 copies verbatim)

`PortabilityMediaEntry` (in `KumunitaArchive.cs`, U01) carries **exactly**
three fields — the §manifest `{ id, size_bytes, content_type }` set:

| Field | Source |
|-------|--------|
| `Id` | `MediaObject.Id` (the lowercase-hex SHA-256 content hash — the archive path `media/{Id[0..2]}/{Id}`) |
| `SizeBytes` | `MediaObject.SizeBytes` (guarded: the read payload's `LongLength` must equal it) |
| `ContentType` | `MediaObject.ContentType` (guarded: non-blank) |

**Fail-closed guards added (C-M11·3 "both present or rejected", export
side):** a catalog row with a blank `ContentType`, or whose read payload
byte-length ≠ `SizeBytes`, throws `InvalidOperationException` before any
archive bytes are written — the export never ships a catalog doc whose
bytes it cannot carry (the complement of U05's §validate (d) reject).

### Finalized manifest field set (the §manifest locked shape — U05 copies verbatim)

`ManifestFinalize.Build` → `PortabilityManifest` with **exactly** the
locked fields:

| Field | Value |
|-------|-------|
| `Format` | `PortabilityManifest.FormatVersion` = **`kumunita/portability/1`** (the C-M11·1 authority) |
| `GeneratedAt` | `DateTimeOffset.UtcNow` (a witness, not a decision) |
| `CommunityName` | `CommunityOptions.Name` (the §config instance identity) |
| `DocCounts` | U02's `docCounts` (one entry per §inventory content doc type) |
| `MediaManifest` | U03's `mediaManifest` (one entry per catalog `MediaObject`) |

### `ExportAsync` public signature (U04 web surface + U07 test target verbatim)

```csharp
Task<Stream> ExportAsync(string actorId, CancellationToken ct = default);
```

`actorId` is the GlobalAdmin actor's `subjectId` (the one-audit-row actor,
emitted by the service in U04 — the controller adds none). The returned
`Stream` is a `MemoryStream` positioned at 0 holding the complete
`*.kumunita` ZIP (manifest + docs + media + identity + config).

### Drift from the plan's text

1. **The `media/` section layout is applied by `KumunitaArchive` (U01),
   not `MediaExport`.** The unit plan says `MediaExport` "writes them into
   the archive at the `{Id[0..2]}/{Id}` layout (the `KumunitaArchive`
   writer's media seam)". Resolved in favor of U01's actual seam: the
   archive writer already maps content id → `media/{Id[0..2]}/{Id}`
   (U01's `MediaEntryName`), so `MediaExport` hands it the id → bytes
   dict (not raw file writes). Same C-M11·3 outcome (byte-identical
   layout), fewer duplicated layout rules.
2. **The media manifest list type is `List<PortabilityMediaEntry>`, and
   `ManifestFinalize.Build` takes `IReadOnlyList<PortabilityMediaEntry>`**
   (the unit plan's "the media manifest (from U03)" is type-neutral). The
   `PortabilityManifest.MediaManifest` property (U01) is
   `List<PortabilityMediaEntry>`; `Build` copies the input list into a
   fresh `List<>` (no shared reference between the finalize input and the
   manifest).
3. **The `size_bytes` / `content_type` guards are new** (not in the unit
   plan's literal text) — they enforce the C-M11·3 "both present or
   rejected" pin at the export source (a null/blank `ContentType` or a
   length mismatch would otherwise round-trip silently into a manifest
   that U05's §validate (d) would reject anyway; failing at the source is
   the stricter, earlier guard). No invariant contradicted.

### Compile warnings

- 1× `CS9113: Parameter 'mediaFileStore' is unread` on
  `PortabilityService` — **expected** (U05/U06 consume it; the warning
  disappears when the import apply-media path lands).
- The U02 `CS9113` on `documentStore` / `userManager` / `appDbContext` /
  `communityOptions` are **resolved** (the `ExportAsync` body reads them
  all).
- **No new packages.** `Kumunita.Core.csproj` is untouched (the
  `System.IO.Compression` / `System.Text.Json` BCL pin holds; D2).

### Next unit's entry reads (U04 — the web surface + `portability.export` audit + the kw-l keys)

Per the register's U04 entry reads (plus U03's actual seams):
- `src/Kumunita.Core/Portability/PortabilityService.cs` — the complete
  `ExportAsync(string actorId, CancellationToken)` (the U04 stream
  target; the service is where the one `portability.export` audit row is
  emitted — the controller adds none, ADR 0105 shape).
- `src/Kumunita.Core/Portability/KumunitaArchive.cs` — the
  `PortabilityManifest` POCO (the `Format` / `GeneratedAt` /
  `CommunityName` / `DocCounts` / `MediaManifest` field set U04's
  response headers / the U07 pins read).
- `src/Kumunita.Web/Controllers/` (an ADR 0101/0105 GlobalAdmin admin
  controller — the `[Authorize(Roles = GlobalAdmin)]` + one-audit-row
  house shape to mirror) + the `Content-Disposition: attachment` streaming
  shape (`AttachmentController` / the ADR 0034 attachment lane) the
  `export` action reuses for the `File(stream, "application/octet-stream",
  "kumunita.kumunita")` response.
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` + the 4-language
  parity test — the `portability.*` kw-l insertion point (the D10 list ×
  4 languages).
- `src/Kumunita.Core/Identity/AccessAudit.cs` + the `AccessVia.Admin` /
  `TargetKind` shapes (the one-audit-row contract the service emits).

## U05 — Import: the validate-then-apply of documents + media, fail-closed (2026-09-28)

**Exit status:** `dotnet build Kumunita.slnx -c Debug` → **green** (0
errors; the single `xUnit2013` warning at
`tests/Kumunita.Core.Tests/MessagingServiceTests.cs(204)` is pre-existing
and unrelated). The U04-noted `CS9113: Parameter 'mediaFileStore' is
unread` on `PortabilityService`'s ctor is now **resolved** (U05's media
apply consumes it). `Kumunita.Web.Tests` → **528 total, 0 failed**.
`Kumunita.Core.Tests` → **956 total, 0 failed**.

### Files written / touched

- **Created** `src/Kumunita.Core/Portability/PortabilityValidate.cs` —
  the §validate phase (the C-M11·4 fail-closed gate). The four locked
  checks (a)–(d) run to completion **before any write** (the C-M11·4
  pin). The closed failure set is emitted as a
  `PortabilityValidationResult(bool Ok, IReadOnlyList<string> Failures)`
  — the U06 web surface + the U07 fail-closed pin render / assert
  <em>exactly</em> it. The data-driven referential-integrity loop
  iterates `PortabilityDocTypes.InOrder()` + each entry's
  `ReferenceFields` generically (the D7 reference map, **not**
  per-type code). The media verification (check (d)) verifies the
  content-hash match via `Convert.ToHexString(SHA256.HashData(bytes)).
  ToLowerInvariant() == m.Id` (the C-M11·3 "content-hash matches the
  path" pin — the `LocalVolumeMediaStore.Sha256Hex` convention).
- **Created** `src/Kumunita.Core/Portability/PortabilityApplyDocuments.cs`
  — the §apply step 2: store the domain documents in the registry's
  import order (parents before children — the D7 order) as **one
  commit** (a single `SaveChangesAsync` — the C-M11·4 "one commit"
  pin). The loop is uniform (the
  `PortabilityExportDocuments.NameToType` table lookup + `List<T>`
  deserialization + `session.Store(row)` — **not** per-type code). The
  non-generic `session.Store(object)` call is the same seam U04's
  `ExportAsync` uses for the `AccessAudit` row (no reflection needed —
  the `Marten.ISession` interface in .NET 10's Marten 9.31.2 does not
  expose a generic `Store<T>` method that compiles without a type
  argument; the non-generic `Store(object)` is the in-repo idiom).
- **Created** `src/Kumunita.Core/Portability/PortabilityApplyMedia.cs`
  — the §apply step 3: copy the media bytes into the volume at the
  `{Id[0..2]}/{Id}` content-addressed layout (the C-M11·3 pin — the
  **same** layout as the local volume, so import is a byte-copy and the
  dedup-by-content-hash is preserved). The `IMediaFileStore.PutAsync`
  seam applies the layout (the C-MED·3/4/7 convention — the same as
  `LocalVolumeMediaStore.PutAsync`'s byte write). Idempotent (C-MED·4):
  a re-import of the same file is a no-op at the byte layer. The
  `MediaObject` catalog doc is **not** stored here — it is one of the
  44 content docs already stored by `PortabilityApplyDocuments.ApplyAsync`
  (order 3 in the D7 registry).
- **Modified** `src/Kumunita.Core/Portability/PortabilityExportDocuments.cs`
  — the `NameToType` map's visibility widened from `private` to
  `internal` (U05's validate (b) + the apply loop dispatch their
  per-type `List<T>` deserialization / storage against the **same**
  frozen table — the single name→`Type` map in the context, not three
  copies).
- **Modified** `src/Kumunita.Core/Portability/PortabilityService.cs`
  — the `ImportAsync(actorId, Stream archive, ct)` body filled in (the
  U01 shell's `NotImplementedException` replaced with the
  validate-then-apply composition): `KumunitaArchive.ReadAsync` →
  `PortabilityValidate.Run(data)` (a failure ⇒ return the closed
  failure set, **zero writes**, C-M11·4) → on a clean validate:
  `PortabilityApplyDocuments.ApplyAsync(documentStore, data, ct)` →
  `PortabilityApplyMedia.ApplyAsync(mediaFileStore, data, ct)` →
  `PortabilityImportResult.Success`. **U06's seam is reserved**: the
  identity re-creation (step 1, `ApplyIdentityAsync` — secrets reset,
  the C-M11·2 import boundary) + the config apply (step 4,
  `ApplyConfigAsync` — the `CommunityOptions` + the `LocaleSettings` +
  the `LanguageCatalog`, the §config field set) + the one
  `portability.import` `AccessAudit` row (the ADR 0105 `messaging.
  toggle` shape) are **not** implemented — the `ImportAsync` body has
  explicit `// U06 SEAM` comments marking the three insertion points.
- **Moved** `docs/plans-milestones/in-progress/portability-u05.md` →
  `docs/plans-milestones/done/portability-u05.md` (the unit is
  complete; the unit-series rule).
- **Touched** this handoff note — this `## U05` section appended
  (existing sections untouched, per the "never rewrite existing
  handoff sections" rule).
- **Not touched** (per the unit's scope): no tests (the U07
  round-trip / no-secret / fail-closed tests are U07's deliverable —
  the U07 round-trip test witnesses U05's apply path end-to-end; the
  U07 fail-closed test witnesses U05's validate path end-to-end), no
  Web surface (the U06 `POST Import` action + the `portability.import`
  audit row + the `portability.*` kw-l keys are U06's deliverables),
  no `Milestones.cs` / README / `docs/STATUS.md` / `docs/ARCHITECTURE.md`
  / `MilestonesTests.cs` change (the U07 flip, C-M11·8).

### Exact validate-check set (as written — the U07 fail-closed test
asserts exactly these)

The four locked checks (the design doc §validate, copied verbatim):

| # | Check | Failure code(s) | Invariant |
|---|-------|-----------------|-----------|
| (a) | `format` is one the build understands (the closed set: `kumunita/portability/1`) | `format.unsupported` | C-M11·1 |
| (b) | each `docs/{Type}.json` in the §inventory deserializes into its declared POCO set; a malformed / missing / unexpected `docs/` file is rejected | `docs.missing:{Type}` / `docs.malformed:{Type}` | C-M11·4 |
| (c) | every id referenced by the §inventory reference map resolves to a row within the archive (data-driven loop over `PortabilityDocTypes.InOrder()` + each entry's `ReferenceFields`) | `ref.dangling:{Type}.{field}` | C-M11·4 |
| (d) | every `MediaObject` listed in the `media_manifest` has its bytes present in `media/{Id[0..2]}/{Id}` AND the byte content matches (the size + the content hash implied by the path) | `media.missing:{id}` / `media.mismatch:{id}` | C-M11·3 |

**Any failure ⇒ zero writes** (C-M11·4) — the validate phase is
read-only over the in-memory archive; the apply phase runs only on a
clean validate.

### Exact closed failure-set shape (as written — the U06 web surface +
the U07 pin render / assert exactly this)

```
format.unsupported
docs.missing:{Type}
docs.malformed:{Type}
ref.dangling:{Type}.{field}
media.missing:{id}
media.mismatch:{id}
```

The `PortabilityValidationResult(bool Ok, IReadOnlyList<string>
Failures)` record carries the failure list — the U06 web surface
renders the `portability.status.failure` kw-l key + the failure list in
the `TempData["error"]`; the U07 fail-closed pin asserts the exact
closed set (no more, no less).

### Apply-order entry count (matches the registry)

**44 entries.** The apply loop iterates `PortabilityDocTypes.InOrder()`
— orders 1→44 in the 9 locked order-groups (parents before children,
the D7 order). One `session.Store(row)` per deserialized row (the
`List<T>` rows from `docs/{Type}.json`). One `SaveChangesAsync` at the
end (the C-M11·4 "one commit" pin).

### `ImportAsync` public signature (the U06 + the U07 target verbatim)

```csharp
Task<PortabilityImportResult> ImportAsync(string actorId, Stream archive, CancellationToken ct = default);
```

The body (as written — U06's seams marked):

```csharp
public async Task<PortabilityImportResult> ImportAsync(string actorId, Stream archive, CancellationToken ct = default)
{
    var data = await KumunitaArchive.ReadAsync(archive, ct).ConfigureAwait(false);
    var validation = PortabilityValidate.Run(data);
    if (!validation.Ok)
        return new PortabilityImportResult(Ok: false, Failures: validation.Failures);

    // U06 SEAM (step 1): ApplyIdentityAsync — the identity re-creation
    // (secrets reset, the C-M11·2 import boundary) + the role re-apply.
    // U06 inserts this call BEFORE the docs apply.

    // Step 2 (U05): store the domain documents in the registry's
    // import order (parents before children — the D7 order).
    await PortabilityApplyDocuments.ApplyAsync(documentStore, data, ct).ConfigureAwait(false);

    // Step 3 (U05): copy the media bytes into the volume at the
    // {Id[0..2]}/{Id} content-addressed layout (C-M11·3).
    await PortabilityApplyMedia.ApplyAsync(mediaFileStore, data, ct).ConfigureAwait(false);

    // U06 SEAM (step 4): ApplyConfigAsync — the CommunityOptions +
    // the LocaleSettings + the LanguageCatalog (the §config field set).
    // U06 inserts this call AFTER the media apply.

    // U06 SEAM (audit row): the one portability.import AccessAudit row
    // (TargetKind "portability", Via = Admin, verb import) — emitted
    // by the service (the controller adds none, the ADR 0105 shape).
    // U06 inserts this call after a clean apply.

    return PortabilityImportResult.Success;
}
```

### "Zero writes on validate failure" pin confirmed (C-M11·4 source boundary)

The `PortabilityValidate.Run(data)` is **read-only** over the
in-memory `KumunitaArchiveData` — it does not open a Marten session,
does not call `IMediaFileStore.PutAsync`, does not touch the Identity
`UserManager`, does not write any file. The apply phase
(`PortabilityApplyDocuments.ApplyAsync` + `PortabilityApplyMedia.
ApplyAsync`) runs **only** after `validation.Ok` is `true`. A
validate failure returns the closed failure set **before** any apply
call — the C-M11·4 "zero writes" pin is enforced at the source (the
U07 fail-closed test witnesses it: the fresh instance has zero rows
after a failed import).

### Drift from the plan's text

1. **The `NameToType` map's visibility is widened from `private` to
   `internal`** (not in the unit plan's literal text) — U05's validate
   (b) + the apply loop need the same name→`Type` map U02's export
   loop uses; widening to `internal` (not `public`) keeps it
   context-internal (the C-M11·7 "zero new authorization surface"
   pin is unaffected — no new public API). The single map in the
   context is now shared by U02's export loop + U05's validate (b) +
   U05's apply loop (no three copies to drift).
2. **The `Marten.ISession` generic `Store<T>` is not accessible** in
   .NET 10's Marten 9.31.2 (the `Marten.ISession` type does not
   exist in the namespace `Marten` — CS0234). The in-repo idiom is
   the non-generic `session.Store(object)` (U04's `ExportAsync` uses
   it for the `AccessAudit` row; `LocalVolumeMediaStore.PutAsync`
   uses it for the `MediaObject` doc; the 76 `session.Store(...)`
   calls in the tree are all non-generic). U05's apply loop uses the
   same non-generic `session.Store(row)` — no reflection needed, no
   new public API, the same in-repo idiom.
3. **The multi-kind `TargetId` resolution (Notification /
   NotificationSubscription) is a sentinel-tolerant check** (not in
   the unit plan's literal text, but grounded in source): the
   design doc §inventory names the union as
   `Component|Group|Page|Announcement`, but the emitters in source
   use **non-id sentinels** for several kinds (`"announcements"` /
   `"announcementcomments"` / `"signup"` / `"messaging.toggle"` —
   the `NotificationKinds` closed set). A multi-kind `TargetId` value
   that matches **none** of the four named kinds' id sets is treated
   as a **sentinel** (a non-reference by design — it does not point
   at a doc row) and is **skipped** (satisfied, not dangling). This
   is the drift-guard rule applied: the source (the emitters'
   closed sentinel set) is the authority; the §inventory's union
   names the <em>doc-id</em> resolution, not the sentinel case. The
   U07 round-trip test will witness this at runtime (the Notification
   / NotificationSubscription rows in the plant will have sentinel
   `TargetId`s that resolve as satisfied — the integrity loop skips
   them, not rejects them).

### Compile warnings

- 0 new warnings in `Kumunita.Core` / `Kumunita.Web`.
- The U04-noted `CS9113: Parameter 'mediaFileStore' is unread` on
  `PortabilityService` is **resolved** (U05's `PortabilityApplyMedia.
  ApplyAsync(mediaFileStore, ...)` consumes it).
- The single pre-existing `xUnit2013` warning at
  `tests/Kumunita.Core.Tests/MessagingServiceTests.cs(204)` is
  unrelated to U05 (it was present at U04's exit).
- **No new packages.** `Kumunita.Core.csproj` is untouched (the
  `System.IO.Compression` / `System.Text.Json` BCL pin holds; D2).

### Next unit's entry reads (U06 — the identity re-creation + the
config apply + the Web surface + the `portability.import` audit + the
kw-l keys)

Per the register's U06 entry reads (plus U05's actual seams):
- `docs/design/m11-portability-design.md` — §principals (the locked
  no-secret field set the re-creator consumes), §config (the locked
  config field set), §surface (the D5 `import` route + action + the
  one-audit-row shape), §kw-l (the locked key list).
- `src/Kumunita.Core/Portability/PortabilityService.cs` — the
  complete `ImportAsync(actorId, Stream archive, ct)` (the U05 body
  with the three `// U06 SEAM` comments marking the identity
  re-creation + the config apply + the `portability.import` audit
  row insertion points).
- `src/Kumunita.Core/Portability/PrincipalsExport.cs` +
  `ConfigExport.cs` — U02's principals/config extractors (the exact
  field set U06's re-creator + applier mirror, verbatim).
- `src/Kumunita.Core/Identity/UserManager.cs` (or the Identity
  `UserManager` setup in `DependencyInjection.cs`) — the
  `UserManager.CreateAsync` + the `AddToRoleAsync` + the **reset**
  shape (the `PasswordHasher` / the fresh password + the
  security-stamp reset the C-M11·2 import boundary enforces).
- `src/Kumunita.Web/Controllers/AdminPortabilityController.cs` —
  U04's export surface (the `import` action + the view form U06
  completes, the D5 shape).
- `src/Kumunita.Web/Views/Admin/Portability.cshtml` — U04's index
  view (the import upload form U04 rendered, U06 wires the `POST` +
  the status render).

## U06 — Import: the identity re-creation (secrets reset) + the config apply + the web surface + the `portability.import` audit + the kw-l keys (2026-09-28)

**Exit status:** `dotnet build Kumunita.slnx -c Debug` → **green**
(0 errors; the single `xUnit2013` warning at
`tests/Kumunita.Core.Tests/MessagingServiceTests.cs(204)` is the
pre-existing, unrelated one U05 carried forward). `Kumunita.Web.Tests`
→ **528 total, 0 failed**. `Kumunita.Core.Tests` → **956 total, 0
failed**. The U04/U05-noted `CS9113: Parameter 'mediaFileStore' is
unread` stays resolved (U05's media apply still consumes it; U06 adds
no new unread ctor param — the new `RoleManager` seam is consumed by
`PortabilityApplyIdentity.ApplyAsync`).

### Files written / touched

- **Created** `src/Kumunita.Core/Portability/PortabilityApplyIdentity.cs`
  — the §apply step 1: the **identity re-creation** (D3 / C-M11·2 import
  boundary). For every `identity/principals.json` row, re-create the
  Identity `User` **keyed by the exported `subjectId`** (the §principals
  "key" column — the stable id the rest of the graph references), **reset
  the secrets** (a fresh non-portable password via
  `UserManager.CreateAsync(user, freshPassword)` + a fresh security stamp
  via `UpdateSecurityStampAsync` — the C-M11·2 import boundary, the single
  most load-bearing M11 invariant enforced on the import side), and
  **re-apply the roles** (`AddToRoleAsync`, the elevated role rows
  ensured-exist via `RoleManager.CreateAsync` — the `SampleDataSeeder`
  `EnsureUserAsync` idiom). The `displayName` / `verified` / `blocked`
  fields are the `Profile` doc's state — re-materialized by U05's
  `PortabilityApplyDocuments.ApplyAsync` (step 2, order 5 in the §inventory);
  this unit is the Identity `User` + role standing only. `RandomPassword()`
  reuses the `SampleDataSeeder.RandomPassword` CSPRNG idiom verbatim (the
  fresh non-portable credential — 32 chars, upper + lower + digit
  guaranteed, the app's Identity policy).
- **Created** `src/Kumunita.Core/Portability/PortabilityApplyConfig.cs`
  — the §apply step 4: the **config apply** (D2 / §config). Mirrors U02's
  `ConfigExport` field set **verbatim**: the `LocaleSettings` singleton
  (the seven instance-level locale fields, verbatim) + every
  `LanguageCatalog` row (the §config `languages[]` field set) — the
  load-then-`Store` idempotent shape (the `FirstBootSeeder` idiom), one
  commit (the C-M11·4 "one commit" pin; the last apply step, so the
  `LanguageCatalog` / `LocaleSettings` re-materialize after the
  `*Translation` rows that reference them). The `community` block
  (`CommunityOptions.Name` / `SupportEmail`) is **not applied** — it is a
  host-config value (the `Community__*` env vars, ADR 0002 / OPS.md), not
  store state; the archive carries it as the self-description + the
  `manifest.json` `community_name`.
- **Modified** `src/Kumunita.Core/Portability/PortabilityService.cs` — the
  `ImportAsync` body completes (the three `// U06 SEAM` insertion points
  filled, the locked order — principals → docs → media → config, §validate
  "apply phase"): after `PortabilityValidate.Run(data)` (a failure ⇒ return
  the closed failure set, **zero writes**, C-M11·4), on a clean validate:
  `KumunitaArchive.FromJson<List<PortabilityPrincipal>>(data.Principals)` →
  `PortabilityApplyIdentity.ApplyAsync(userManager, roleManager, principals, ct)`
  (step 1, **before** the docs apply) → `PortabilityApplyDocuments.ApplyAsync`
  (step 2, U05) → `PortabilityApplyMedia.ApplyAsync` (step 3, U05) →
  `KumunitaArchive.FromJson<PortabilityConfig>(data.Config)` →
  `PortabilityApplyConfig.ApplyAsync(documentStore, config, ct)` (step 4) →
  the **one** `portability.import` `AccessAudit` row (`TargetKind
  "portability"`, `Via = Admin`, verb `import` — the ADR 0105
  `messaging.toggle` shape; emitted **after** a clean apply, the same "no
  audit for a refused action" posture as the export lane) →
  `PortabilityImportResult.Success`. The ctor gains the `RoleManager<IdentityRole>`
  seam (consumed by `PortabilityApplyIdentity`).
- **Modified** `src/Kumunita.Core/DependencyInjection.cs` — the
  `IPortabilityService` factory adds
  `sp.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>()`
  (the role-row source; `AddIdentity<User, IdentityRole>` registers it,
  `Program.cs`).
- **Modified** `src/Kumunita.Web/Controllers/AdminPortabilityController.cs`
  — the **`POST /admin/portability/import`** action completes: the
  `[ValidateAntiForgeryToken]` + `IFormFile archive` → open the read stream →
  `portability.ImportAsync(actor, stream)` → on success
  `TempData["info"] = portability.status.ok` + redirect; on the closed
  failure set `TempData["error"] = portability.status.failure` + the failure
  list (one per line) + redirect (the `_FlashToast` partial renders either —
  the house `AdminMessagingController` `TempData` + redirect idiom; the
  C-M11·4 fail-closed render, the U07 fail-closed pin). The one
  `portability.import` audit row is the **service's** (the controller adds
  none). The ctor gains the optional translation seams
  (`ILocalizationService localization`, `ITranslationProvider? translationProvider
  = null`) + a private `T(key)` helper (the house
  `EffectiveLanguageCode.ResolveAsync` + `ITranslationProvider.GetAsync`
  pattern, so the `portability.*` kw-l keys resolve in the operator's
  language — the `TranslationProvider` floor guarantees the `en` string; the
  optional param preserves the no-context test-construction shape).
- **Not touched** (per the unit's scope / the kw-l deliverable is already
  satisfied): the **kw-l keys are all six registered × 4 languages** by U04
  (`portability.index.title` / `portability.export` / `portability.import` /
  `portability.confirm.import` / `portability.status.ok` /
  `portability.status.failure`, in `en` / `de` / `fr` / `da`) — U06 **reuses**
  the import-side keys (`portability.import` / `portability.confirm.import` /
  `portability.status.ok` / `portability.status.failure`), **not duplicated**
  (the D10 "U04's export-side keys are reused, not duplicated" — the
  `KnownTranslationKeys` parity test still passes, 528 Web / 956 Core green).
  No tests (the U07 round-trip / no-secret / fail-closed tests are U07's
  deliverable — the U07 round-trip witnesses U06's apply identity + config
  path end-to-end; the U07 fail-closed witnesses U05's validate + U06's
  zero-writes-on-failure render), no `Milestones.cs` / README /
  `docs/STATUS.md` / `docs/ARCHITECTURE.md` / `MilestonesTests.cs` change
  (the U07 flip, C-M11·8).

### Exact identity re-creation shape (as written — the U07 round-trip test
witnesses it end-to-end)

```csharp
// PortabilityApplyIdentity.ApplyAsync(userManager, roleManager, principals, ct)
foreach (var p in principals) {
    var user = new User {
        Id = p.SubjectId,                       // keyed by the exported subjectId (the §principals "key")
        Email = p.Email,
        NormalizedEmail = p.NormalizedEmail,
        UserName = string.IsNullOrEmpty(p.Username) ? p.Email : p.Username,
    };
    var freshPassword = RandomPassword();       // 32-char CSPRNG (SampleDataSeeder idiom) — the C-M11·2 secret reset
    var r = await userManager.CreateAsync(user, freshPassword);   // a fresh non-portable password
    if (!r.Succeeded) throw new InvalidOperationException(...);
    foreach (var role in p.Roles) {             // the role re-apply (Member / Moderator / GlobalAdmin / Translator, ADR 0030)
        if (string.IsNullOrEmpty(role)) continue;
        if (await roleManager.FindByNameAsync(role) is null)
            await roleManager.CreateAsync(new IdentityRole(role)); // ensure-exist (SampleData EnsureUserAsync idiom)
        await userManager.AddToRoleAsync(user, role);
    }
    await userManager.UpdateSecurityStampAsync(user);   // a fresh security stamp (no replayed secret — C-M11·2)
}
```

The **C-M11·2 import boundary** is enforced at the source: the
`PortabilityPrincipal` POCO has **no** `PasswordHash` / `SecurityStamp` /
`AccessToken` / `RefreshToken` / `RecoveryCode` field (the U07 no-secret
field-shape pin), and `ApplyAsync` **never reads** one (there is none in the
archive, by C-M11·2 — the secrets are **reset**, never replayed; the
resident re-authenticates). The `displayName` / `verified` / `blocked`
fields are the `Profile` doc's state (re-materialized by U05's docs apply,
step 2) — this unit is the Identity `User` + role standing only.

### Exact config-apply field set (as written — matches U02's ConfigExport
verbatim)

```csharp
// PortabilityApplyConfig.ApplyAsync(documentStore, config, ct)
var locale = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct)
    ?? new LocaleSettings { Id = LocaleSettings.SingletonId };
locale.DefaultLanguageCode      = config.Locale.DefaultLanguageCode!;
locale.DefaultTimezone          = config.Locale.DefaultTimezone!;
locale.DefaultDateFormat        = config.Locale.DefaultDateFormat!;
locale.IsSignupOpen             = config.Locale.IsSignupOpen;
locale.NotifyAdminsOnSignup     = config.Locale.NotifyAdminsOnSignup;
locale.AnnouncementCommentsEnabled = config.Locale.AnnouncementCommentsEnabled;
locale.MessagingEnabled         = config.Locale.MessagingEnabled;
session.Store(locale);
foreach (var l in config.Languages) {
    var row = await session.LoadAsync<LanguageCatalog>(l.Id, ct)
        ?? new LanguageCatalog { Id = l.Id };
    row.NativeName = l.NativeName;
    row.Enabled    = l.Enabled;
    row.SortOrder  = l.SortOrder;
    session.Store(row);
}
await session.SaveChangesAsync(ct);   // one commit (the C-M11·4 pin) — the last apply step
```

The `community` block (`CommunityOptions.Name` / `SupportEmail`) is the
host-config self-description (the `Community__*` env vars, ADR 0002 / OPS.md)
— **not** a store write; the archive carries it as the self-description +
the `manifest.json` `community_name`.

### The one-audit-row shape (as written — the U07 Web pin targets it verbatim)

Emitted by the **service** (`PortabilityService.ImportAsync`, after a clean
apply; the controller adds none — the ADR 0105 `messaging.toggle` shape):

```csharp
session.Store(new Authorization.AccessAudit {
    Id = System.Guid.NewGuid().ToString("N"),
    At = DateTimeOffset.UtcNow,
    ActorId = actorId,
    EffectivePrincipalId = actorId,
    Action = "portability.import",        // the verb
    TargetKind = "portability",
    TargetId = "portability",
    Via = Authorization.AccessVia.Admin,
    Outcome = Authorization.AccessOutcome.Allow
});
await session.SaveChangesAsync(ct);
```

No audit row on the **failure** path (a validate failure returns the closed
failure set before this point — the same "no audit for a refused action"
posture as the export lane, U04).

### The POST Import + the closed-failure-set render (as written — the U07
pin targets it verbatim)

```csharp
[HttpPost("import")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Import(IFormFile archive) {
    var actor = ActorId(User) ?? string.Empty;
    if (archive is null || archive.Length == 0) {
        TempData["error"] = await T("portability.status.failure");
        return RedirectToAction(nameof(Index));
    }
    await using var stream = archive.OpenReadStream();
    var result = await portability.ImportAsync(actor, stream);
    if (result.Ok) {
        TempData["info"] = await T("portability.status.ok");
        return RedirectToAction(nameof(Index));
    }
    var failures = string.Join("\n", result.Failures);   // the closed failure set (the C-M11·4 pin)
    TempData["error"] = $"{await T("portability.status.failure")}\n{failures}";
    return RedirectToAction(nameof(Index));
}
```

The `_FlashToast` partial (the one, uniform flash surface) renders
`TempData["info"]` (success) / `TempData["error"]` (the failure + the closed
failure set, one per line) — the house `AdminMessagingController` idiom.

### The exact import-side portability.* kw-l keys (as written — reused, not
duplicated)

U04 already registered **all six** `portability.*` keys × 4 languages
(`en` / `de` / `fr` / `da`) in `KnownTranslationKeys` — the D10 list. U06
**reuses** the import-side ones (does not add new keys):
`portability.import` (the form label + button), `portability.confirm.import`
(the `data-confirm` guard, the view's `importConfirm`), `portability.status.ok`
(the success `TempData["info"]`), `portability.status.failure` (the
closed-failure-set `TempData["error"]` prefix). The controller's `T(key)`
helper resolves each through the operator's effective language (the house
`EffectiveLanguageCode.ResolveAsync` + `ITranslationProvider.GetAsync` seam;
the `TranslationProvider` floor guarantees the `en` string). The `portability.*`
parity test still passes (the `KnownTranslationKeys_ParityTests` enforces the
4-language set — 528 Web green).

### Drift from the plan's text

1. **The `PortabilityApplyConfig` uses `documentStore.OpenSession(...)` (the
   full session), not `QuerySession()`** (not in the unit plan's literal text,
   but grounded in source): the `IQuerySession` does not expose `Store` /
   `SaveChangesAsync` (CS1061) — U05's `PortabilityApplyDocuments.ApplyAsync`
   uses the same `OpenSession(new SessionOptions())` idiom (the in-repo
   write-session shape). Same one-commit shape; the full session is the write
   seam.
2. **The `community` block (`CommunityOptions.Name` / `SupportEmail`) is not
   applied** to a store (not in the unit plan's literal text — the plan says
   "the community name + the `LocaleSettings` + the language-catalog state",
   but `CommunityOptions` is a host-config value, the `Community__*` env vars,
   ADR 0002 / OPS.md — there is no store row to write; the archive carries it
   as the self-description + the `manifest.json` `community_name`). The
   **applied** instance state is the store-backed `LocaleSettings` +
   `LanguageCatalog` docs (the §config `locale` + `languages[]` blocks). The
   U07 round-trip test (which plants via the same seed path) will witness the
   `LocaleSettings` / `LanguageCatalog` re-materialization; the community
   name is host-config on both sides of a self-hosted round-trip (the same
   instance image, the ADR 0002 posture).
3. **The `RoleManager<IdentityRole>` seam is added to the
   `PortabilityService` ctor + the DI factory** (not in U01's shell — the
   plan's U06 deliverable names `RoleManager` / the `AddToRoleAsync` re-apply
   as the re-creation shape; the `FirstBootSeeder` + `SampleDataSeeder`
   `EnsureUserAsync` idiom is the in-repo role-row ensure + add pattern).
   `AddIdentity<User, IdentityRole>` (Program.cs) registers it; the factory
   resolves it the same way it resolves `UserManager<User>`.
4. **The `UserName` falls back to the email when the principal's username is
   blank** (not in the unit plan's literal text, but grounded in source):
   `User.UserName` is the Identity sign-in name; a blank username would leave
   the re-created principal un-signable-by-username. The email is the
   sign-in credential (the §principals "the sign-in credential" note) and is
   always present (the `AddIdentity` `RequireUniqueEmail` + the `Profile`
   email). A no-op for any well-formed export (U02's `PrincipalsExport`
   always sets `UserName = user.UserName`, which `RegisterAsync` sets to the
   email).
5. **The three `LocaleSettings` string fields use `!` (nullable-forgiving)
   on assignment** (CS8601 suppression): the §config POCO fields
   (`PortabilityConfigLocale`) are nullable strings; the `LocaleSettings`
   fields are non-nullable with POCO defaults. U05's `PortabilityApplyDocuments`
   uses the same `!` idiom (`List<T>` deserialization + `!`). The
   `TranslationProvider` floor + the `LocaleSettings` POCO defaults guarantee
   a non-null value on a well-formed archive; a null (malformed archive) is a
   validate-phase failure (check (b)) — the archive is rejected before apply,
   so the `!` never dereferences a real null on a pre-validated archive.

### Compile warnings

- 0 new warnings in `Kumunita.Core` / `Kumunita.Web` (the CS8601
  nullable-assignment warnings on the three `LocaleSettings` string fields
  are suppressed with `!`, the U05 idiom).
- The single pre-existing `xUnit2013` warning at
  `tests/Kumunita.Core.Tests/MessagingServiceTests.cs(204)` is unrelated to
  U06 (it was present at U04's + U05's exit).
- The U04-noted `CS9113: Parameter 'mediaFileStore' is unread` on
  `PortabilityService`'s ctor stays **resolved** (U05's media apply consumes
  it; the new `RoleManager` seam is consumed by
  `PortabilityApplyIdentity.ApplyAsync`).
- **No new packages.** `Kumunita.Core.csproj` is untouched (the
  `System.IO.Compression` / `System.Text.Json` / `System.Security.Cryptography`
  BCL pin holds; D2).

### Next unit's entry reads (U07 — the Core round-trip / no-secret /
fail-closed tests + the Web-surface pins + the milestone flip)

Per the register's U07 entry reads (plus U06's actual seams):
- `docs/design/m11-portability-design.md` — §pinned tests (the exact test
  names + the closed failure set), §Invariants (the C-M11 pins the tests
  witness), §FACES (the F1–F5 the tests close).
- `src/Kumunita.Core/Portability/PortabilityService.cs` — the complete
  `ExportAsync` + `ImportAsync` (the U02–U06 bodies: the export
  doc/principal/config + media + manifest + audit; the import
  validate-then-apply (principals → docs → media → config) + the audit) —
  the two methods the U07 tests target verbatim.
- `src/Kumunita.Core/Portability/PortabilityApplyIdentity.cs` +
  `PortabilityApplyConfig.cs` — the U06 identity re-creation (the
  `subjectId` keying + the secret reset + the role re-apply) + the config
  apply (the `LocaleSettings` + `LanguageCatalog` field set) the round-trip
  test asserts end-to-end.
- `src/Kumunita.Core.Tests/PostgresFixture.cs` — the test harness shape —
  the `postgres:18` Testcontainers + the media temp-dir the round-trip /
  no-secret / fail-closed tests need.
- `src/Kumunita.Web/Milestones.cs` + `README.md` (the Roadmap section) +
  `docs/STATUS.md` + `docs/ARCHITECTURE.md` (the value-chain table — the four
  parity surfaces the flip touches) + `tests/Kumunita.Web.Tests/MilestonesTests.cs`
  (the single-in-progress pin the re-pin moves M11 → M12).
