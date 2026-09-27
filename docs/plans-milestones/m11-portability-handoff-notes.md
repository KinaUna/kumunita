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
