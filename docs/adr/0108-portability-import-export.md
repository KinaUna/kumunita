---
id: adr-0108
title: "M11: Portability (import/export) — whole-instance export + a fail-closed restore of a versioned `*.kumunita` archive (a new `Kumunita.Core/Portability/` operator context — the closed content inventory read from the `*DocTypes` surfaces, the no-secret identity graph (never the credential material), the content-addressed media, the instance identity + localizations via `config.json`; validate-then-apply fail-closed atomic import; the GlobalAdmin admin plane with one `AccessAudit` row per action; zero new authorization surface)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m11-portability-design.md, docs/design/adr-handoffs.md]
supersedes: [adr-0004]
superseded_by: []
updated: 2026-11-02
---

# ADR [0108](0108-portability-import-export.md) — M11: Portability (import/export) — whole-instance export + a fail-closed restore of a versioned `*.kumunita` archive

Status: Accepted | Date: 2026-09-27

> **Abstract:** M11 closes the "data leaves and comes back" world-seam: a
> GlobalAdmin exports the **whole instance** as one versioned `*.kumunita`
> archive and restores it fail-closed onto a fresh instance — content +
> media + the identity **graph** (never the credential material), with the
> `config.json` carrying the instance identity + localizations. **For** a
> developer touching `Kumunita.Core/Portability/`, the admin portability
> surface, or the round-trip / no-secret / fail-closed tests. **Read the
> rest** before changing the inventory, the no-secret boundary, or the
> validate-then-apply contract. Amends + deferred-lane catalog + test names
> in the [handoff note](../design/adr-handoffs.md#adr-0108--m11-portability-moved-detail).

## Context

M1–M10 and the named lanes grew a full resident surface — identity,
directory, posts, events + calendar, projects + boards, pages, search,
notifications, messaging, PWA. But a self-hosted single-neighborhood platform
lives or dies on one world-seam the roadmap row names (ARCHITECTURE.md value
chain: "the loop closes **into** the residents' lives"): **the
neighborhood's data must be able to leave the platform and come back.**

Today it cannot. There is **no import path at all** — a fresh instance is a
blank instance. And the only "export" is the `pg_dump` an operator runs by
hand (OPS.md), which carries the Postgres schema *and* the credential hashes
*and* the media volume as **three separate artifacts** that do not travel
together and do not verify against each other. `pg_dump` is a *schema +
state* snapshot, not a portable *content* artifact: it carries credential
material the platform's privacy posture forbids from being in an operator's
hands, and it is not self-describing (no format version, no manifest, no
media verification).

M11 closes that seam in the two directions the row names:

1. **Export** — a GlobalAdmin produces **one** portable, **versioned**
   archive (`*.kumunita`) holding the whole instance's *content* (every
   registered content domain document), the *media bytes* (content-addressed,
   dedup preserved), the *identity graph* (who-is-who + delegation + guardian
   + membership + moderator — **as data**, without the credential material),
   and the *instance identity + localizations*.
2. **Import (restore)** — a GlobalAdmin on a **fresh** (or current) instance
   ingests that archive: it **validates the whole archive first** (format
   version, per-document-type schema sanity, **referential integrity**, the
   media bytes), and only then **applies it in one commit**. A rejected
   archive writes **nothing**.

M11 is **greenfield and operator-scoped** (grep-confirmed: there is no
`Portability` context, no export/import code, no `manifest.json` shape, no
archive reader/writer anywhere in the tree). It builds entirely on frozen,
verified seams: the Marten domain documents across the registered `*DocTypes`
surfaces, the ASP.NET Identity tables (the **principals** — the credential
material lives here and **never travels**), the content-addressed media volume
(ADR [0011](0011-media-and-file-storage.md)), the GlobalAdmin admin-surface house pattern (ADR [0101](0101-announcement-comments-lane.md)/0105/0106),
the BCL `System.IO.Compression.ZipArchive` (in the .NET 10 framework — **no
new package**), and the `CommunityOptions` + `LocaleSettings` +
`LanguageCatalog` state (the `config.json` snapshot source).

This is a **milestone** (a roadmap letter, not a named lane): the close unit
(U07) flips `Milestones.cs` / the README Roadmap / [`docs/STATUS.md`](../STATUS.md) /
[`docs/ARCHITECTURE.md`](../ARCHITECTURE.md) + `MilestonesTests.cs` (the AGENTS.md doc↔code parity
contract, C-M11·8). The design doc [`docs/design/m11-portability-design.md`](../design/m11-portability-design.md)
(authored U00, **LOCKED**) is the primary tier; the register
[`docs/archive/lanes/m11/plan-m11-portability.md`](../archive/lanes/m11/plan-m11-portability.md) is the secondary tier.

## Decision

- **D1 — Scope = whole-instance export + import (restore).** M11 exports and
  imports the **entire instance**: every registered content domain document
  type, the media bytes, the identity graph (principals + the
  delegation/guardian/membership/moderator domain docs), and the instance
  identity + localizations. The **import semantic is restore** (apply into a
  fresh instance; the current instance's state is the operator's pre-import
  backup, the rollback path). *Forbids:* shipping per-group migration now —
  the repo is explicitly single-neighborhood with no multi-tenant model, and
  cross-neighborhood federation is a named deferral; M11 owns the *whole*
  instance the value-chain row names, not the fragments.
- **D2 — The archive is a versioned ZIP, BCL-only (no new dependency).**
  `System.IO.Compression.ZipArchive` (in the .NET 10 framework — **no** new
  package). Layout: `manifest.json` (self-describing, the C-M11·1 shape) +
  `docs/{DocType}.json` (one JSON-array file per registered content domain
  document type) + `media/{Id[0..2]}/{Id}` (the **same** layout as the local
  volume, so import is a byte-copy and dedup-by-content-hash is preserved) +
  `identity/principals.json` (the C-M11·2 no-secret graph) + `config.json`
  (the instance identity + localizations). `*.kumunita` is the file extension
  (a content marker — the `format` field in `manifest.json` is the
  authority). *Forbids:* a new package, a format the `manifest.json`
  `format` version does not own, or a media layout different from the local
  volume.
- **D3 — The identity travels as the graph, never the secrets.** The archive
  carries the **principals** (`subjectId`, `username`, `email`,
  `normalizedEmail`, `displayName`, `roles`, and the lockout/state flags — the
  data needed to re-create a signable principal) **and** the delegation /
  guardian / membership / moderator **domain documents** (those live in the
  M1 domain context, not the Identity schema, so they travel with the
  content). It **never** carries credential material: no password hash, no
  security stamp, no access-token / refresh-token / recovery-code, no Identity
  sign-in record. On import the principals are **re-created** (keyed by the
  exported `subjectId`) and their secrets are **reset** (a fresh,
  non-portable password; the resident re-authenticates). This is the privacy
  pin (C-M11·2) and the ADR [0028](0028-guardian-controls-account-scope-supervision.md) GU precedent ("no standing to carry a
  secret"). **The no-secret boundary is the single most load-bearing M11
  invariant.**
- **D4 — Import is validate-then-apply, fail-closed, atomic.** The
  **validate phase** runs to completion **before any write**: (a) the
  `manifest.json` `format` is one the build understands (else reject — the
  C-M11·1 pin); (b) each `docs/{Type}.json` deserializes into its declared
  POCO set; (c) **referential integrity** — every id referenced by the
  inventory's reference map resolves to a row **within the archive**; (d) the
  media manifest's bytes are all present and their content matches their
  path. **Any failure ⇒ zero writes.** The **apply phase** is one commit:
  re-create the Identity principals → store the domain documents in the pinned
  dependency order → copy the media bytes → apply the config. On a mid-apply
  failure the instance's state is the **documented restore path** — M11 never
  silently accepts a half-import.
- **D5 — The surface is the admin web plane (GlobalAdmin), audited, quiet.**
  `GET /admin/portability` (the index: export button + import upload form + a
  status area) + `GET /admin/portability/export` (streams the archive) +
  `POST /admin/portability/import` (applies the uploaded archive). All three
  are `[Authorize(Roles = GlobalAdmin)]`; **export** and **import** each
  emit exactly **one** `AccessAudit` row (`TargetKind "portability"`,
  `Via = Admin`, verb `"export"` / `"import"`), emitted by the service (the
  ADR [0105](0105-messaging.md) `messaging.toggle` shape). A plain resident — including a
  moderator — never sees the surface.
- **D6 — The media travels content-addressed, with the catalog doc.** The
  bytes are copied **verbatim** into the archive at the **same**
  `{Id[0..2]}/{Id}` layout the local volume uses (ADR [0011](0011-media-and-file-storage.md)), so import is a
  byte-copy and the content-hash dedup is preserved. The `MediaObject`
  catalog doc travels as a `docs/MediaObject.json` row. A `MediaObject`
  listed in the manifest whose bytes are missing / mismatched is a
  **validate-phase failure** (fail-closed, C-M11·3) — the archive never
  imports a doc pointing at a phantom byte.
- **D7 — The closed document inventory + import order is pinned in the design
  doc from source.** U00 reads **every** `*DocTypes` surface and locks the
  **exact** closed inventory — each entry: the document `Type`, its archive
  file, its **import order** (parents before children), and its **reference
  fields** — that U01's registry implements as **data**; the U02/U05 loops
  iterate it generically (that is what keeps the export/import units atomic
  despite ~40 document types). The closed inventory is the **content +
  identity-graph + localization** set — **44 content docs** — and it
  **excludes** the five credential/operational documents (`IdentityToken`,
  `OutboxEmail` / `EmailDeadLetter` / `AccessAudit` / `AuditPurgeSummary`) by
  name, carrying `LocaleSettings` + `LanguageCatalog` + `CommunityOptions`
  via `config.json` (D2) rather than as `docs/` rows. (Full inventory + the
  source-refinement in the [handoff
  note](../design/adr-handoffs.md#d7--the-closed-inventory-pinned-in-the-design-doc-from-source).)
- **D8 — One new `Portability` context in Core — an operator lane, not a
  content-decision lane.** `Kumunita.Core/Portability/` holds the manifest
  model, the document-inventory registry, the archive (de)serializer, and the
  `PortabilityService` (export / validate / apply). The service composes
  `IDocumentStore` (Marten), the Identity `UserManager` / `RoleManager`, and
  `IMediaStore` / `IMediaFileStore`; registered in `DependencyInjection.cs`
  like its siblings. **No** new `AccessAction`, **no** new `AccessVia`, **no**
  `IAuthorizationService` branch, **no** `Audience` — portability is an
  operator action over the instance, decided by the GlobalAdmin role gate
  (C-M11·7).
- **D9 — Tests: a Core Postgres round-trip + the no-secret pin + the
  fail-closed pin + the Web surface pins.** The **three acceptance tests**
  (closed-loop / handoff / part-vs-whole): (a) **round-trip** — plant a
  representative instance, **export**, import into a **fresh** instance, the
  content graph + media + role assignments are preserved and referentially
  intact; (b) **no-secret** — the exported archive contains **no** password
  hash / security stamp / Identity token (the principals POCO has no such
  field + a byte-scan witness over the archive); (c) **fail-closed** — a
  wrong-`format` manifest / a truncated media file / a dangling reference is
  rejected **before any write** (zero rows after the failed import). Plus
  Web-surface pins (the GlobalAdmin gate, one-audit-row per action, the kw-l
  keys resolve). (Exact pins in the [handoff
  note](../design/adr-handoffs.md#d9--the-test-pins).)
- **D10 — kw-l keys + the deferred lanes.** The operator-facing strings
  (`portability.index.title` / `portability.export` / `portability.import` /
  `portability.confirm.import` / `portability.status.*`) × 4 languages (the
  closed-key registry + `KnownTranslationKeys_ParityTests` enforces).
  **Deferred lanes** (own ADRs): per-slice / cross-instance portability,
  import-merge, the backup-automation / cron surface (OPS.md), and the other
  world-seam exports (M12 stays iCal). (Full catalog in the [handoff
  note](../design/adr-handoffs.md#the-deferred-lanes-each-a-named-follow-on-own-adr).)

**The invariants to lock (C-M11·1–8):** the archive is versioned +
self-describing (C-M11·1); the archive travels the data, never the secrets
(C-M11·2 — the single most load-bearing M11 invariant); the media travels
content-addressed + whole (C-M11·3); import is fail-closed + atomic
(C-M11·4); whole-instance, not per-slice (C-M11·5); the surface is GlobalAdmin
+ audited + quiet (C-M11·6); zero new authorization surface (C-M11·7); docs
parity holds at the flip (C-M11·8). The **FACES (F1–F5)** are locked in the
design doc §FACES: the neighborhood's life is portable out (F1); the archive
comes back intact (F2); the secrets never travel (F3); a corrupt archive is
refused wholesale (F4); the operator surface is quiet + audited (F5).

## Consequences

- **Positive:** the neighborhood's data can leave and come back — the world-
  seam the roadmap row names, in the strongest form (the *whole* instance,
  not a fragment). The archive is **structurally** secret-free (C-M11·2), so a
  stolen or mishandled file is data without the credentials — strictly
  stronger than `pg_dump` (which carries the hashes). The fail-closed restore
  (C-M11·4) means a corrupt or inconsistent archive is refused wholesale — the
  "parts-work-seams-don't" failure is structurally impossible on the import
  path. The surface is quiet (one audit row per action) and invisible to a
  plain resident (C-M11·6). The BCL-only archive (D2) holds the lean-stack +
  tsc-only discipline (no new package).
- **Negative / cost:** M11 deliberately adds **no** resident facing, **no**
  engagement surface, **no** feed — it is a quiet operator lane, and that is
  the point (C-M11·6/7). The cost is the operator's only, and it is one
  click. The **operator is a GlobalAdmin resident** (the house pattern) —
  there is no non-resident operator model, and the backup-automation / cron
  surface is a follow-on lane (D10), so an operator who wants a scheduled
  export must run the app surface today.
- **The deferred lanes** (each a named follow-on, own ADR) are listed in the
  [handoff note](../design/adr-handoffs.md#the-deferred-lanes-each-a-named-follow-on-own-adr):
  per-slice / cross-instance portability, import-merge, the backup-automation
  / cron surface, and the other world-seam exports.
