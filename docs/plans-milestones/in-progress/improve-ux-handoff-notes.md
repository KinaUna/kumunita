---
id: improve-ux-handoff-notes
title: "IMPROVE-UX — Handoff Notes"
tier: living
kind: handoff
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/adr/0302-shared-kiosk-link.md
  - docs/adr/0301-self-service-deletion-grace-period.md
  - docs/adr/0142-delete-account.md
  - docs/adr/0028-guardian-controls-account-scope-supervision.md
  - docs/adr/0143-guardian-delete-child.md
lane: improve-ux
updated: 2026-11-13
---

> **Abstract:** the running handoff log for the **IMPROVE-UX** lane — each
> unit appends its section here as it ships, so a successor agent (or the
> X02 cold read) gets the honest before → after, not the intended one. This
> note opens with **R15**, the ADR 0302 shared-kiosk-link Core seam
> (the `KioskLink` doc + the `IKioskLinkService` create/revoke/validate
> service + the P-1 gate count 82 → 83), then **R17** (the community-visible
> filter in `KioskItemCatalog`) and **R18** (the `/settings/kiosk`
> create/revoke surface), then **R19**, the **SEC review** of the ADR 0302
> lane (R15–R18): the R16 auth gap (the headline — a resident can mint a
> token that works nowhere), the three red `SecGate` findings (one fixed —
> the `KioskLink` eraser seam the ADR deferred to R19; the other two the
> prompt mis-attributed to `KioskLink`, and are filed as pre-existing SEC-lane
> drift), the code-check re-base of the four ADR-sanctioned kiosk edges,
> the confirmed by-design calls (256-bit token, copy-once in `TempData`,
> the revoke-mismatch `200`), and the open questions filed (the R16 auth,
> the R17 `ComponentId` scope, expiry, max-links). Then **R14**, the SEC
> review of the ADR 0301 grace period (R10–R13). The **empty-states batches
> R29 → R33 are closed out** (each section records its honest before → after;
> R33 is the final batch — the residual `empty_plain` = 3 is a measure
> artifact, not a forced 0).
> **Who is it for:** the next agent in this lane and the X02 cold read.
> **When to read the rest:** before starting a unit, or when a DoD line
> "looks done" and you want to know if it actually is.

# IMPROVE-UX — handoff notes

## R15 — ADR 0302 + KioskLink doc + create/revoke/validate service

**Scope:** the Core seam + ADR for the shared kiosk link (IMPROVE-UX R15 —
the first kiosk unit, a new lane). The Web surface (R16–R18) and the SEC
review (R19) are separate later units and were **not** started.

**Outcome:** ADR 0302 + the `KioskLink` doc + the
`IKioskLinkService`/`KioskLinkService` + the DI registration + 8 new Core
tests, all in one `feat(ux):` commit (`8de9751a`). The personal kiosk
(0164/0285) is unchanged — R15 is purely additive.

### What landed

- **ADR 0302** — the shared-kiosk-link decision. Six decisions:
  - **D1** — the `KioskLink` doc: `Id`, `PlaylistId`, `OwnerSubjectId`,
    `TokenHash` (SHA-256 hex digest only — **never** the raw token),
    `CreatedAt`, `LastUsedAt?`, `RevokedAt?`. Registered on the existing
    `CoreDocTypes` surface (the ADR 0004 §B.1 additive precedent — no DDL,
    no new `*DocTypes`). Carries `[DataClass(Secret, Delete, Never)]`
    (the ADR 0298 SecGate rule).
  - **D2** — token shape: `kskl_` + 32 CSPRNG bytes base64url (256 bits,
    the 0159 `kesc_` shape), shown exactly once at creation, stored only
    as the SHA-256 hex digest (the 0159 ESC·3 / F2 hash-at-rest rule).
  - **D3** — `IKioskLinkService` (3-method seam, pure Core — the R16
    middleware can call it): `CreateAsync(playlistId, ownerSubjectId)`,
    `RevokeAsync(linkId, ownerSubjectId)`, `ValidateAsync(tokenHash,
    ownerSubjectId)`. Audit: one `kiosk.link.create` + one
    `kiosk.link.revoke` row each (`Via: Owner`, `TargetKind = "kiosk"`);
    `ValidateAsync` writes **zero** rows (the M35·5 "read = config load"
    floor) and stamps `LastUsedAt` on a match (the ESC·9 read-then-write).
  - **D4** — invariants the R16–R18 units build against: revocable,
    read-only, `/kiosk` + slides only, community-visible items only,
    audited under the owner as via-kiosk-link.
  - **D5** — the personal kiosk (0164/0285) and all its invariants stand
    unchanged.
  - **D6** — open questions (the R19 SEC review settles): (a) token length
    (proposed 256 bits), (b) expiry (none in this ADR), (c) max links per
    playlist (no ceiling in this ADR).
- **Core files** (all new or modified in `src/Kumunita.Core/`):
  - `Kiosk/KioskLink.cs` — the `[DataClass]` doc.
  - `Kiosk/IKioskLinkService.cs` — the 3-method seam.
  - `Kiosk/KioskLinkService.cs` — the impl (CSPRNG token, SHA-256 hash at
    rest via `IdentityService.Sha256Hex`, audit rows, `ValidateAsync`
    zero-audit + `LastUsedAt` stamp, owner-scoped `RevokeAsync`).
  - `CoreDocTypes.cs` — `opts.Schema.For<Kiosk.KioskLink>()` added (after
    the `DeletionRequest` registration).
  - `Kiosk/DependencyInjection.cs` — `IKioskLinkService` →
    `KioskLinkService` registered.
- **Tests** — `tests/Kumunita.Core.Tests/Kiosk/KioskLinkServiceTests.cs`
  (8 tests, `KioskLinkServiceTests` class):
  1. `ADR0302_Create_ReturnsRawTokenOnce_AndStoresRow`
  2. `ADR0302_Store_HoldsHashNotRawToken` — the "hash only at rest" pin
  3. `ADR0302_Validate_AnotherOwnersLink_IsRefused` — owner-scope pin
  4. `ADR0302_Validate_UnknownToken_IsRefused`
  5. `ADR0302_Validate_RevokedToken_IsRefused`
  6. `ADR0302_Validate_MatchesOnlyValidOwnLink` — the full matrix
  7. `ADR0302_Revoke_OwnerScoped_Idempotent_Audited` — audit-row pin
  8. `ADR0302_Validate_WritesNoAuditRow_StampedLastUsed` — zero-audit +
     `LastUsedAt` pin
- **P-1 gate update** — `DataClassCoverageTests` (Core.Tests): registered-
  type count raised 82 → 83 for the new `KioskLink` row.

### Test results

- `KioskLinkServiceTests` (8) — **all green**
- `KioskServiceTests` (6) — **all green** (the existing `KioskPlaylist`
  seam is intact)
- `DataClassCoverageTests` (1) — **green** (83/83 after the count update)
- `dotnet build` — **0 errors**
- Translation parity — **unaffected** (R15 adds no localized keys)

### `// TODO ❓` open questions flagged for R19's SEC review

1. **Token length** — D2 proposes 256 bits (32 CSPRNG bytes). The R19
   SEC review may adjust.
2. **Expiry** — this ADR ships no expiry (a link lives until revoked).
   Whether a long-lived shared credential needs a max age is a SEC call.
3. **Max links per playlist** — this ADR ships no ceiling. The R18
   settings surface can list and revoke them; whether a hard limit is
   needed is a SEC call.

### Notes for R16

- The `ValidateAsync` seam takes the **hash** (not the raw token). The
  R16 middleware must hash the presented token with the same
  `IdentityService.Sha256Hex` algorithm before calling it (the
  `FindTokenAsync` local-constant pattern — the digest is a query
  constant, never inside a `.Where(...)` expression).
- The `KioskLinkService` does **not** carry `IAuthorizationService` —
  the owner-scope check is data-scoped (the `OwnerSubjectId` field), not
  a `Decide()` branch. The R16 restricted principal (owner + `kiosk-link`
  claim) is a Web-side concern.
- `LastUsedAt` is stamped on every successful validate — the R18 surface
  reads this field for the "last used" display.

## R17 — kiosk-link community-visible filter (group-private → denied slide)

**Scope:** ADR 0302 D4's "shows community-visible items only" (IMPROVE-UX
R17 — the second kiosk unit). When the catalog resolves a slide **in
kiosk-link mode** (the viewer's principal carries the `kiosk-link` role —
the role R16 will mint; R17 adds the *filter* it will read), any item the
whole community cannot see renders the existing `kiosk.slide_denied` shape
instead of being offered. Personal-kiosk mode (the owner's full visibility)
is **unchanged** — the filter is a no-op for a signed-in owner (ADR 0302 D5).

**Outcome:** the filter + the `Roles.KioskLink` role constant + 14 tests, in
one `feat(ux):` commit (`0a15fd02`). The personal kiosk and the R15 Core
seam are untouched — R17 is purely additive to the catalog.

**What landed**

- **`Roles.KioskLink`** (`"kiosk-link"`) — the role value a kiosk-link
  principal carries, defined in `ThinPrincipal.cs` (the R15 "no role on the
  Core seam" rule kept — the role is a Web/Identity surface concern; R16 will
  mint it). The catalog reads it to switch into kiosk-link mode.
- **`KioskItemCatalog.ResolveAsync` kiosk-link branch** — a `kioskLink` flag
  (`roles.Contains(Roles.KioskLink)`) threaded through each resolver. In
  kiosk-link mode:
  - **Group items** (a type that has no `Audience`) are denied when the
    group is private (`KioskLinkGroupIsNonPrivateAsync` — a non-private
    group's items are community-visible; a private group's are not).
  - **`Audience`-bearing types** (Post / Board / Todo / Project / Page /
    Event) are denied when the audience is *grant-restricted* — the pure
    predicate `KioskLinkAudienceIsCommunity(aud)`: `null` (public) /
    `Community` / `AllResidents` ⇒ visible; a user/group grant list without
    a community flag ⇒ denied.
  - **Announcements** are denied when a draft (`IsDraft`) or community-
    targeted (`CommunityId != null`).
  - **Drafts** of any type are denied (in kiosk-link mode the owner is not
    the author, so a draft is not community-visible).
- **Tests** — `KioskLinkCommunityFilterTests` (14): the pure
  `KioskLinkAudienceIsCommunity` matrix (null / Community / AllResidents /
  grant-restricted) + the per-type resolver denials (group-private → denied
  slide, private group → denied, public group → visible, draft → denied,
  community-targeted announcement → denied, etc.).

**`// TODO ❓` left for R19's SEC review** (see the R19 Findings, F5): the
`Community` flag is evaluated **without the resource's own `ComponentId`
scope** — R17 treats `Community=true` as visible across all component scopes.
ADR 0036 / 0102 refine the flag into a community-scoped shape; the stricter
reading (narrow to the resource's own community) is a code change, not a
review-note-only fix, so it is filed, not built.

**Test results (before R19's touch):** `KioskLinkCommunityFilterTests` (14)
**all green**; `KioskServiceTests` (6) **all green** (personal kiosk intact).

## R18 — the `/settings/kiosk` shared-link surface (create / list / revoke)

**Scope:** ADR 0302 D3/D4's Web surface (IMPROVE-UX R18 — the third kiosk
unit). The signed-in resident's **create / list / revoke** on
`/settings/kiosk`, built on the R15 Core seam (`IKioskLinkService`) and the
R17 catalog. The raw token is **shown exactly once** at creation (a copy-once
reveal), then gone — the store holds only the hash. **The R16 auth is not
here** (the link authenticates nowhere yet — see R19 Focus 1). Personal-kiosk
settings (`SettingsGet` / `SettingsPost` / the item picker) are **unchanged**.

**Outcome:** the surface + `KioskLinkListReader` (the Web read seam) + the
copy-once affordance + 10 `KioskLinkSettingsTests` + 14 localized keys
(× en/de/fr/da), in one `feat(ux):` commit (`8531cd6d`). The R15 Core seam is
**frozen** — R18 adds no method to it (the "Web layer owns the read"
precedent; the `KioskController` stays off direct Marten, the C09 shape).

**What landed**

- **`IKioskLinkService` DI** — `Program.cs` registers
  `IKioskLinkService → KioskLinkService` (R15's Core seam now reachable from
  the Web).
- **`POST /settings/kiosk/link`** (`LinkCreate`) — mints the link via
  `CreateAsync(actorId, actorId)` (the personal playlist's `Id` = the owner's
  subject id, ADR 0164). The **raw token** + the new row's metadata ride
  `TempData` for the **single** redirect round-trip (never in the URL — the
  token must not ride a referer / history entry), then are read + cleared by
  `SettingsGet`, which hands it to the view for the copy-once reveal.
- **`POST /settings/kiosk/link/revoke`** (`LinkRevoke`) — revokes via
  `RevokeAsync(linkId, actorId)` (owner-scoped, idempotent — a missing /
  already-revoked / another-owner's link is a no-op). Returns
  `200 { revoked:false }` on a mismatch (a no-op, not a status-grade refusal —
  see R19 Focus 5, the call R19 records).
- **`GET /settings/kiosk`** (`SettingsGet`) — the owner's **active** links
  (via `KioskLinkListReader`, `RevokedAt == null` only, newest first, **no**
  token material) + the copy-once token reveal for the just-created one.
- **`KioskLinkListReader`** (`IKioskLinkListReader` / `KioskLinkListReader`)
  — the Web read seam the `KioskController` uses to stay off direct Marten
  (the C09 shape; the `EscalationForwarder` `store.QuerySession()` idiom — the
  session is disposed, the singleton store is not). Blank owner → `[]`.
- **`KioskSettingsViewModel`** — `SharedLinks` (the active rows) +
  `SharedLinkToken` (the one-shot reveal).
- **View + TS** — the `Settings.cshtml` shared-link section (list + copy-once
  reveal + revoke) + `kiosk-settings.ts`'s copy affordance (the
  `/kiosk?link=<token>` URL is **assembled client-side**, the R16 middleware
  is what will parse it — so the token never rides a server-rendered `href`).
- **Localization** — 14 `kiosk.settings.*` keys × en/de/fr/da (de/fr/da
  machine-written, flagged for the parked native review).
- **`ux-baseline.json`** — `bare_status` re-based **704 → 708** (stated in the
  file's `generated_from`): R18's `LinkCreate` / `LinkRevoke` owner guards
  (`return Forbid()` / `BadRequest()`) + R17's +1 that shipped without a
  re-base. The guards are belt-and-braces (the class-level `[Authorize]` +
  the form's `linkId` make them unreachable in production); re-based rather
  than deleted.

**Test results (before R19's touch):** `KioskLinkSettingsTests` (10) **all
green**; `KioskControllerTests` **all green**; `KioskLinkServiceTests` (8)
**all green**.

## R19 — SEC review of the ADR 0302 shared kiosk link (R15–R18)

**Scope reviewed:** the ADR [0302](../../adr/0302-shared-kiosk-link.md)
kiosk-link lane — R15 (`8de9751a`, the ADR + `KioskLink` doc +
`IKioskLinkService`/`KioskLinkService`), **R16 (no commit — see Focus 1)**,
R17 (`0a15fd02`, the community-visible filter) and R18 (`8531cd6d`, the
`/settings/kiosk` surface). R19 is a **review**, not an implementation:
R15–R18 are preserved, not rewritten.

**Outcome (honest, and it diverges from the R19 prompt):** **one of the three
red `SecGate` findings is a genuine ADR-0302 deferral — the `KioskLink`
eraser seam — and R19 fixes it; the other two are pre-existing SEC-lane
drift that the prompt mis-attributed to `KioskLink`, and R19 files them
rather than fixing them in this unit.** The **R16 auth gap** (the headline)
is recorded plainly: a resident can mint, list and revoke a kiosk token that
**authenticates nowhere** — the link is not shippable to a resident until R16
lands. The four ADR-sanctioned kiosk boundary edges are re-based under
`adr: 0302`; the six non-kiosk code-check violations (R05 + four grown
aggregate edges) are filed. No finding is left without an owner. R19 makes
**two code fixes** (the `KioskLink` eraser seam + the one mis-typed SEC-READ
marker) and **one baseline re-base** (the four kiosk edges); everything else
is a decision or a filed finding.

### Focus 1 — The R16 gap — **FILED (headline)**

The ADR 0302 shared link is **built** (R15 Core seam, R17 filter, R18
surface) but **authenticates nothing**. Verified at HEAD:

- `Roles.KioskLink` (`"kiosk-link"`, `ThinPrincipal.cs:62`) is **defined**
  (R17) and **read** (R17's `KioskItemCatalog` filter) but **never assigned** —
  no code mints a principal carrying it.
- **No `/kiosk?link=<token>` handler, no middleware, no `ValidateAsync`
  caller** anywhere in `src/` — `IKioskLinkService.ValidateAsync` (the seam
  R15 built specifically so R16's middleware could call it) has **zero**
  callers. The `kiosk?link=` URL the R18 view/TS *assembles client-side* has
  no server endpoint that accepts it.

**R19 verdict: this is an accepted *held surface*, not a defect of R15–R18.**
The create / list / revoke surface (R18) is **not shippable to a resident**
until R16's auth lands — a resident who mints a token that works nowhere is a
UX-and-SEC smell (it invites the "I shared my link" report with nothing
behind it). **Filed (F1)** with the owner = the next IMPROVE-UX unit that
implements R16 (the token→restricted-principal middleware on `/kiosk` +
`/kiosk/slide`, the rate limit). The R18 surface should be **hidden or
gated** (e.g. an `#if` feature flag or a "coming" state) until R16 ships —
see F1.

### Focus 2 — The three red `SecGate` tests — **one fixed, two filed (prompt mis-attribution)**

R19 ran the three. The R19 prompt attributes all three to `KioskLink`;
ground truth shows only one is.

- **`EveryDeleteOrPseudonymizeType_IsHandledByAnEraser`** — red on **14**
  unhandled `Delete` types. `KioskLink` is one, but so are `KioskPlaylist`
  (the pre-existing ADR 0164 sibling) and 12 others (`AccessAudit`,
  `EmailDeadLetter`, `InventoryCheckout`, `SuppressedAddress`, `Report`,
  `ComponentMembership`, `GroupInvitation`, `GroupJoinRequest`,
  `GroupMembership`, `GuardianLink`, `ModeratorAssignment`, `Profile`). This
  is the **recorded IS24 SEC-lane drift** (the "gate is the pin, red is the
  finding" state, `improve-sec-known-reds`).
  **R19 fixes the one ADR 0302 explicitly deferred to it** — "the owner
  account's eraser owns it — the R19 SEC review confirms the seam" — by
  wiring `KioskLink` into `ResidentAccountRowsEraser` (a new block-14
  `EraseKioskLinksAsync`, matched on `OwnerSubjectId`, mirroring the
  `DeletionRequest` block-13 shape). After the fix, `KioskLink` is **gone**
  from the unhandled list (14 → 13). The **other 13 are filed (F2)** with the
  SEC lane — they are not ADR 0302's to close, and fixing them here would be
  a scope slip into IS21–IS23 / IS31–IS32's recorded drift.
- **`EveryResidentExportType_IsInUserScopeInventory`** — red on
  `Announcement`, `AnnouncementTranslation`, `TagTranslation`. **`KioskLink`
  is not in this failure** — it is `ExportScope.Never` and so is never
  checked against the resident-export inventory. The R19 prompt's
  "the type not in the export-scope inventory" **mis-attributes** this
  finding. **Filed (F2)** with the SEC lane (the `Announcement*` /
  `TagTranslation` export rows are the IS31/IS32 M2 drift the known-reds
  memory records).
- **`Every SEC-READ marker names a reason`** — red at one marker:
  `ModerationService.cs:688` (R05's `ListMyReportsAsync`). **Not the kiosk
  read path** — the kiosk code (`KioskLinkService`, `KioskLinkListReader`,
  `KioskItemCatalog`) carries **no** `// SEC-READ:` marker at all (verified
  by scan). The R19 prompt's "a `// SEC-READ:` on the kiosk-link read path
  without the required reason" **mis-locates** this finding. The marker is a
  real (if minor) gate miss — it lacked the required taxonomy word
  (`owner-only` / `caller-gated` / `no disclosure` / `report-gated`).
  **R19 fixes it minimally** by prepending `owner-only` (the reporter reads
  their own report rows — the correct taxonomy word, the read already has a
  reason after the em-dash). The test now passes.

### Focus 3 — The red code-check ratchet — **four kiosk edges re-based, six non-kiosk filed**

R19 ran `pwsh tools/code/code-check.ps1` (10 violations at HEAD, a known-red
per `improve-sec-known-reds`). Each, per the ratchet doctrine (a raised
number needs an `adr:` reason; a re-base is a *decision*, not a paper-over):

**Fixed (re-based under `adr: 0302` in `tools/code/code-baseline.json`)** —
the four ADR-sanctioned kiosk edges (the ADR 0302 Consequences + the R15–R18
units *are* the reason; `Kiosk` is a legitimate bounded context, so these are
not boundary *breaches* in the ADR 0006 sense — they are the sanctioned seams
crossing contexts):

- `Kiosk->Authorization:AccessAudit` (1 → **2 files**, `KioskService` + the
  new `KioskLinkService` audit row) — raised, `adr: 0302`.
- `Identity->Kiosk:KioskLink` (**2 files** — `ThinPrincipal.cs`'s
  `Roles.KioskLink` const + R19's `ResidentAccountRowsEraser` eraser seam) —
  added, `adr: 0302`. *(Note: the first of those two is a **name collision** —
  `Roles.KioskLink` is the role-string const, not the `KioskLink` doc — the
  gate counts by symbol token; both are ADR-sanctioned.)*
- `Kiosk->Identity:IdentityService` (**1 file** — `KioskLinkService` uses
  `IdentityService.Sha256Hex` for the hash-at-rest) — added, `adr: 0302`.
- `Web->Kiosk:KioskLink` (**4 files** — `KioskLinkListReader`,
  `KioskItemCatalog`, `KioskController`, `KioskSettingsViewModel`) — added,
  `adr: 0302`.

After the re-base the gate shows **6 violations, all non-kiosk** — filed
(F3), not re-based under ADR 0302 (which would mis-attribute them):

- `ListMyReportsAsync` **cc=17** (ceiling 15) — R05's "My reports" read
  (the method the Focus 2 marker lives in). **Filed (F3)**: a code-shape
  finding the IMPROVE-CODE lane owns (split the method / extract the target-
  title read), not ADR 0302's to re-base.
- `Moderation->Posts:PostService` (2 files) — R05's `ListMyReportsAsync`
  read seam. **Filed (F3)** (same owner, same reason).
- `Web->Authorization:Audience` (22 vs 21), `Web->ErrorReports:ErrorReport`
  (4 vs 3), `Web->Identity:Roles` (40 vs 39), `Web->Identity:User` (82 vs 81)
  — four **aggregate** file-count edges, each +1 over the base ref. None is
  cleanly attributable to R15–R18 (verified: no kiosk Web file references
  `ErrorReport`; `KioskItemCatalog.cs` — the only kiosk file that touches
  `Roles`/`Audience`/`User` — was **already in the baseline** before R17).
  **Filed (F3)** with the SEC lane / the unit that added each file (the
  "drift found in a previous unit" the gate doctrine describes). Re-basing
  them under `adr: 0302` would falsely claim ADR 0302 as their reason.

**Gate state before → after (R19):** **10 violations → 6**; the four
kiosk edges cleared, the six non-kiosk remain (filed, owners assigned). The
`ImproveHarnessTests.CodeCheck_Gate_Passes` test is therefore **still red at
R19's HEAD** on the six non-kiosk violations — an honest, filed state, not a
silence.

### Focus 4 — Token handling (ADR 0302 D1/D2) — **CONFIRMED**

Verified the raw token appears **only** in the `CreateAsync` return + the one
`TempData` round-trip, and **nowhere else**:

- **At rest it is the SHA-256 hex digest** (`KioskLink.TokenHash`, via
  `IdentityService.Sha256Hex`) — the raw token is never stored.
- **Never in the URL** (the R18 reveal is a server-rendered read-only input +
  the `data-kiosk-link-token` attribute; the `/kiosk?link=<token>` URL is
  **assembled client-side** by `kiosk-settings.ts`, never a server `href` —
  the token does not ride a referer or history entry the server writes).
- **Never in an audit row** (`kiosk.link.create` / `kiosk.link.revoke` carry
  only `TargetId` = the link's id, `Via: Owner`, `TargetKind "kiosk"` — PII-
  and token-free) **or a log template** (no `kiosk.link.*` log line carries a
  token placeholder).

**TempData for a copy-once credential — DECIDED (acceptable at HEAD).** The
ASP.NET Core `TempData` store (the `SessionState` provider by default here)
is **transient** (one round-trip, then cleared — R18's `SettingsGet` reads +
`TempData.Remove`s it), **server-side** (not in the URL, not in the browser
history, not in a referer). A *persistent* store (a file / DB-backed
`ITempDataProvider`) is **not** used in production, so the raw token does not
survive on disk. This is the correct shape for a copy-once credential (the
token is shown once and gone — the store holds only the hash). **No finding.**
*(The test-harness `InMemoryTempDataProvider` retention is a test-only
concern, not a production leak.)*

### Focus 5 — Owner-scope pins — **CONFIRMED, one call recorded**

- **Create** (actor = owner): `LinkCreate` passes `actorId` as both the
  `playlistId` (the personal playlist's `Id` = the owner's subject id, ADR
  0164) and the `ownerSubjectId`. A resident can only create a link over
  their own playlist. **Pinned.**
- **Validate** (hash + owner match): `ValidateAsync` matches
  `TokenHash == hash && OwnerSubjectId == ownerSubjectId && RevokedAt == null`
  — a valid hash for **another** owner's link is refused (R15 test
  `ADR0302_Validate_AnotherOwnersLink_IsRefused`). **Pinned.**
- **Revoke** (`RevokeAsync` no-ops on mismatch): a missing / already-revoked /
  another-owner's link returns `false`, no audit row (R15 test
  `ADR0302_Revoke_OwnerScoped_Idempotent_Audited`). **Pinned.**
- **Revoke-mismatch status — DECIDED (acceptable at HEAD).** `LinkRevoke`
  returns `200 { revoked:false }` on a mismatched / unknown link. It leaks
  nothing (the owner-scope pin is enforced in the Core seam; a non-owner
  cannot learn another owner's link id exists, because the id is a
  server-generated `Guid` not enumerable, and the response is the same
  `200 {revoked:false}` a self-`false` no-op would be). The prompt asks
  whether a **404/403** is more correct: **R19 records `200 {revoked:false}`
  as the better choice** — a 403 would disclose "the link exists but you
  don't own it" (an oracle for a guesser probing another owner's link id); a
  404 would disclose "the link does not exist" (equally an oracle). The
  uniform `200 {revoked:false}` gives the least information. The owner UX
  (the "revoked" flash) is already handled by the client for the happy path.
  **No finding.**

### Focus 6 — Audit rows — **CONFIRMED**

Verified against R15's `KioskLinkService` + its tests:

- Exactly **one** `kiosk.link.create` row (`Via: Owner`, `TargetKind "kiosk"`,
  `TargetId` = the link's id, PII-free) on `CreateAsync`. **Pinned** (test
  `ADR0302_Create_ReturnsRawTokenOnce_AndStoresRow`).
- Exactly **one** `kiosk.link.revoke` row (same shape) on a successful
  `RevokeAsync`; **zero** on a no-op (missing / revoked / another-owner's).
  **Pinned** (test `ADR0302_Revoke_OwnerScoped_Idempotent_Audited`).
- **Zero** on `ValidateAsync`; `LastUsedAt` stamped read-then-write (the ESC·9
  "the read is not an access decision" floor — the owner-scope read decision
  is R16's job, and every read the link authenticates is audited by the
  item's own read lane under the owner, ADR 0302 D4). **Pinned** (test
  `ADR0302_Validate_WritesNoAuditRow_StampedLastUsed`).

### Focus 7 — Revoke terminality + idempotency — **CONFIRMED**

- Revoke is **terminal** (no undo): `RevokedAt` set; the row drops out of
  `KioskLinkListReader`'s active list (`RevokedAt == null` filter); the
  `KioskLink` doc's state machine is `created (RevokedAt null) → revoked
  (RevokedAt set)` with no forward edge. **Pinned.**
- A second revoke is a **no-op returning `false`**, **no second audit row**
  (the `stored.RevokedAt is not null` guard returns before the `Store` of the
  audit row). **Pinned** (test `ADR0302_Revoke_OwnerScoped_Idempotent_Audited`
  asserts exactly one audit row after two revoke calls).

### Focus 8 — The Web read seam (`KioskLinkListReader`) — **CONFIRMED**

- **Owner-scoped** (`OwnerSubjectId == ownerSubjectId`), **`RevokedAt == null`
  only** (active links), **`OrderByDescending(CreatedAt)`** (newest first),
  **blank owner → `[]`** (no throw). **Pinned.**
- **No token material returned** — the `TokenHash` is on the `KioskLink` row
  but the `KioskSettingsViewModel` / view only render `Id`, `CreatedAt`,
  `LastUsedAt` (the revoke form posts `Id`; there is no "show the hash again"
  and no raw-token re-read — the store holds only the hash, so a re-read
  cannot recover the token). **Pinned.**
- **C09 (controller off Marten)** — the read lives in the
  `KioskLinkListReader` Web service (the `store.QuerySession()` idiom — the
  session is disposed, the singleton store is not); the `KioskController`
  never touches `IDocumentStore` directly for the shared-link list. **Pinned.**

### ADR 0302 D6 open questions — **RESOLVED (one-line decisions, no ADR edit)**

- **(a) Token length — CONFIRMED 256 bits.** `NewToken()` = `kskl_` +
  **32 CSPRNG bytes** base64url (256 bits, the `EscalationTokenService`
  `kesc_` shape). 256 bits is the standard high-entropy floor for a
  brute-force-resistant credential (a 32-byte `RandomNumberGenerator` fill);
  the `kskl_` prefix distinguishes the lane. **No change** (the number is not
  frozen by the ADR; R19 confirms it, so it now stands).
- **(b) Expiry — FILED (not decided in this unit).** ADR 0302 ships **no
  expiry** (a link lives until revoked). Whether a long-lived shared
  credential needs a max age is a **mechanism change** (an `ExpiresAt` field
  + a validate-time check) beyond a one-line constant, so R19 **files it
  (F5)** with the owner = a follow-up IMPROVE-UX unit / the SEC lane (decide
  the policy: none / a fixed max age / an admin-set expiry), not built here.
- **(c) Max links per playlist — FILED (not decided in this unit).** ADR 0302
  ships **no ceiling**. A hard limit is a **mechanism change** (a create-time
  count + refusal) beyond a one-line constant, so R19 **files it (F6)** with
  the owner = a follow-up IMPROVE-UX unit (the R18 surface already lists +
  revokes them, so a ceiling is additive on top; decide the policy: none /
  N per playlist).

### R18's four `// TODO ❓` — **RESOLVED**

- **Revoke-mismatch status** — see Focus 5: **`200 {revoked:false}`
  confirmed** (the least-information choice over 404/403).
- **"Revoke all" skipped** — **intentional, confirmed.** R18 ships
  per-link revoke (the owner revokes the specific link); a bulk "revoke all"
  is a convenience, not a security control (the owner can revoke each link
  individually, and the count is unbounded — see F6). **No finding.**
- **Max-links ceiling** — see D6(c) above: **filed (F6)**.
- **Token-in-`TempData`** — see Focus 4: **confirmed acceptable** (the
  production `TempData` store is transient + server-side; the token does not
  ride the URL / history / referer / disk).

### Findings

Each is **fixed** (with the fix in this commit) or **filed** with a named
owner. Nothing is left without an owner.

- **F1 — The R16 auth gap (the headline).**
  *Where:* the entire R16 unit (the token→restricted-principal middleware on
  `/kiosk` + `/kiosk/slide`, the rate limit) — **no commit exists**.
  *What:* the kiosk link is created / listed / revoked (R15–R18) but
  **authenticates nothing** (`ValidateAsync` has zero callers;
  `Roles.KioskLink` is defined + read but never assigned; no `/kiosk?link=`
  endpoint). A resident who mints a token that works nowhere is a UX-and-SEC
  smell.
  *R19 verdict:* **accepted held surface** (not a defect of R15–R18 — R16 was
  never in scope of this unit). The R18 surface is **not shippable to a
  resident** until R16 lands.
  *Owner:* the next IMPROVE-UX unit that implements R16 (a scoped R16
  follow-up). *Interim:* hide or gate the R18 `/settings/kiosk` shared-link
  section (a feature flag or a "coming" state) until R16 ships, so a resident
  cannot mint a token with nothing behind it. *(The R18 `ux-baseline.json`
  re-base already records that the guards are belt-and-braces; the gate hides
  the surface.)*

- **F2 — Two red `SecGate` tests that are pre-existing SEC-lane drift, not
  ADR 0302's.**
  *Where:* (a) `EveryDeleteOrPseudonymizeType_IsHandledByAnEraser` — 13
  unhandled `Delete` types beyond `KioskLink` (`KioskPlaylist` + 12 others).
  (b) `EveryResidentExportType_IsInUserScopeInventory` — `Announcement`,
  `AnnouncementTranslation`, `TagTranslation`.
  *What:* the R19 prompt attributes both to `KioskLink`; ground truth shows
  (a) is the recorded IS24 SEC-lane drift (14 types, `KioskLink` is 1 of 14)
  and (b) does **not** involve `KioskLink` at all (it is `ExportScope.Never`,
  never checked against the resident-export inventory). These are the IS21–
  IS23 / IS31–IS32 M2 "gate is the pin, red is the finding" state the
  `improve-sec-known-reds` memory records.
  *R19 verdict:* **filed, not fixed in this unit** (fixing them here is a
  scope slip into the SEC lane's recorded drift; R19 fixed only the one ADR
  0302 explicitly deferred — the `KioskLink` eraser seam, Focus 2).
  *Owner:* the SEC lane (IS21–IS23 / IS31–IS32 close — the 13 unhandled
  `Delete` types + the 3 missing export rows).

- **F3 — Six non-kiosk code-check violations (the ratchet red at R19's HEAD).**
  *Where:* `ListMyReportsAsync` **cc=17** (ceiling 15) + `Moderation->Posts:
  PostService` (both R05's "My reports" read seam); `Web->Authorization:
  Audience` (22/21), `Web->ErrorReports:ErrorReport` (4/3), `Web->Identity:
  Roles` (40/39), `Web->Identity:User` (82/81) (four aggregate file-count
  edges, each +1 over the base ref, none cleanly attributable to R15–R18).
  *What:* the gate is a known-red at HEAD (`improve-sec-known-reds`); the
  six are R05's + other units' drift. R19 re-based the four ADR-sanctioned
  kiosk edges (Focus 3) but **not** these — re-basing them under `adr: 0302`
  would falsely claim ADR 0302 as their reason.
  *R19 verdict:* **filed, not re-based in this unit.**
  *Owner:* (cc=17 + `Moderation->Posts`) the IMPROVE-CODE lane (split
  `ListMyReportsAsync` / extract the target-title read); (the four grown
  edges) the SEC lane / the unit that added each file (the "drift found in a
  previous unit" the gate doctrine describes).

- **F4 — The R17 `ComponentId` scope (the stricter reading).**
  *Where:* `KioskItemCatalog.KioskLinkAudienceIsCommunity` (`KioskItemCatalog.
  cs:268` `// TODO ❓`) — the `Community` flag is evaluated **without the
  resource's own `ComponentId` scope**.
  *What:* R17 treats `Community=true` as visible across **all** component
  scopes; ADR 0036 / 0102 refine the flag into a **community-scoped** shape
  (a non-empty `ComponentId` on the Post / Event / Project / Board / Todo
  means only that community's members see it). The `Audience` type does not
  carry the component (it is on the resource), so the guard cannot distinguish
  "all communities" from "this specific community" from the audience alone.
  *R19 verdict:* **filed, not built in this unit** (the stricter reading is a
  **code change** — a per-type guard that reads the resource's `ComponentId`
  and checks community membership — not a review-note-only fix; R19's scope
  rule bars building it here).
  *Owner:* a follow-up IMPROVE-UX / SEC unit (the per-type `ComponentId`
  guard; the SEC reading should narrow to the resource's own community).

- **F5 — Expiry (ADR 0302 D6b).** See the D6 section: **filed** with the SEC
  lane (a mechanism change — an `ExpiresAt` field + validate-time check —
  decide the policy: none / a fixed max age / an admin-set expiry).

- **F6 — Max links per playlist (ADR 0302 D6c).** See the D6 section:
  **filed** with a follow-up IMPROVE-UX unit (a mechanism change — a
  create-time count + refusal; the R18 surface already lists + revokes them,
  so a ceiling is additive).

### DoD line 8 — honest before → after (R19)

- **Code changed in R19: 2 seams + 1 baseline.** (a) the `KioskLink` eraser
  seam (new block-14 in `ResidentAccountRowsEraser` — the ADR 0302-deferred
  finding, Focus 2); (b) the R05 `ListMyReportsAsync` SEC-READ marker
  (prepended the `owner-only` taxonomy word — Focus 2); (c) the four
  ADR-sanctioned kiosk edges re-based under `adr: 0302` in
  `tools/code/code-baseline.json` (Focus 3). No other code changed; R15–R18
  are preserved.
- **`SecGate` before → after:** `EveryDeleteOrPseudonymizeType_IsHandledByAn-
  Eraser` **14 → 13** (the `KioskLink` row fixed; the other 13 filed, F2);
  `EveryResidentExportType_IsInUserScopeInventory` **unchanged** (3 missing
  export rows, **not** `KioskLink` — filed, F2); `Every SEC-READ marker names
  a reason` **red → green** (the R05 marker fixed, Focus 2).
- **code-check gate before → after:** **10 → 6 violations**; the four
  ADR-sanctioned kiosk edges cleared (re-based under `adr: 0302`), the six
  non-kiosk remain (filed, F3). `ImproveHarnessTests.CodeCheck_Gate_Passes`
  is **still red at R19's HEAD** on the six non-kiosk violations — an honest,
  filed state, not a silence.
- **Kiosk tests (before → after R19's fix):** `KioskLinkServiceTests` (8)
  **all green** (unchanged); `KioskLinkCommunityFilterTests` (14) **all
  green** (unchanged); `KioskLinkSettingsTests` (10) **all green** (unchanged);
  `KioskControllerTests` **all green** (unchanged); Core deletion tests
  (`DeleteAccountRowsTests` / `IdentityServiceDeleteAccountTests` /
  `DataClassCoverageTests`, 24) **all green** (the new `KioskLink` eraser
  block causes no regression); `dotnet build` **0 errors**.
- **Findings filed (all with owners): 6** — F1 (MEDIUM, the R16 auth gap —
  the headline; a resident can mint a token that works nowhere), F2
  (MEDIUM, the two red `SecGate` tests that are pre-existing SEC-lane drift,
  not ADR 0302's — the R19 prompt mis-attributed them), F3 (MEDIUM, the six
  non-kiosk code-check violations — R05 + four grown aggregate edges), F4
  (MEDIUM, the R17 `ComponentId` scope — the stricter reading is a code
  change), F5 (LOW, expiry), F6 (LOW, max-links per playlist).
- **Nothing held silently** — the R16 gap is stated plainly (Focus 1); the
  two mis-attributed `SecGate` findings are named and filed (Focus 2, F2);
  the six non-kiosk violations are named and filed (Focus 3, F3); the R17
  `ComponentId` question is filed (F4). Every decision (256-bit token,
  copy-once `TempData`, `200 {revoked:false}`) is recorded with its
  reasoning.

## R14 — SEC review of the ADR 0301 grace period (R10–R13)

**Scope reviewed:** the ADR [0301](../../adr/0301-self-service-deletion-grace-period.md)
grace-period lane — R10 (`5218c9de`, the ADR), R11 (`ce7ad89d`,
`DeletionRequest` doc + `IDeletionScheduler`/`DeletionScheduler`), R12
(`269693e7`, the `DeletionDueTick`/`DeletionDueHandler` due-job) and R13
(`71827dc2`, the `/account/delete` Web surface). R14 is a **review**, not an
implementation: R10–R13 are preserved, not rewritten.

**Outcome:** **all four foci pass** (or are by-design decisions R14 records),
**both ADR 0301 open questions resolve to "confirm as written,"** and **four
observations are filed** with owners (three LOW, one MEDIUM — the
admin-removal path leaves a child's `GuardianLink` row dangling, a real gap
but a pre-existing ADR 0142 issue, not something R10–R13 introduced). None
required a code change in this unit, so per the R14 prompt the build was
**not** run — a scope slip to avoid. No finding is left without an owner.

### Focus 1 — Audit rows — **PASSES**

ADR 0301 D1 says the self-serve audit shape is "exactly one `AccessAudit`
row, action `account.delete`, `Via: Owner`." The lane is now **two phases**,
so the check is "one row **per phase**, distinct, PII-free" — verified:

- **Distinct verbs.** The schedule phase writes exactly one
  `account.delete.scheduled` row (`DeletionScheduler.ScheduleDeletionAsync`,
  the `IdentityService.AuditRow` store at `DeletionScheduler.cs:147`); the
  execution phase writes exactly one `account.delete` row
  (`IdentityService.AccountOps.cs` `DeleteCoreAsync` step 9, `AuditRow(…, "account.delete", …)`).
  The two facts (the request and the deletion) are separate rows with
  distinct `Action` values — the "audit-of-the-lane" shape `DeleteCoreAsync`
  already uses.
- **`Via` correct per phase.** Both rows are `Via: Owner` on the self-serve
  branch (actor == target == the resident's subject id) — the schedule row
  is the resident acting on their own account; the execution row is the
  same self-serve branch run by the due-job (`DeletionDueHandler` calls
  `DeleteAccountAsync(row.SubjectId, adminSubjectId: row.SubjectId)`).
- **No PII in any log/audit template (invariant P-3).** Both rows carry only
  id-shaped values (actor id, target id `account:{subjectId}`, a `Guid` row
  id). The only log templates in the lane use `{Resident}`
  (`DeletionScheduler.cs:155`) and `{ResidentId}` (`DeletionDueHandler.StopRefusedRowAsync`)
  — neither is a bare PII word, so the IS08
  `LogTemplatePiiGateTests.NoLogTemplate_CarriesAPiiPlaceholder` rule
  (PII iff the placeholder is exactly `Email`/`Ip`/`Phone`/`Address`/
  `Recipient`/`Body`) does not flag them.

### Focus 2 — Re-auth before scheduling / cancelling — **PASSES**

Both write lanes verify the resident's current password **before** the
`IDeletionScheduler` seam is called, and the scheduled-state branch is
read-only:

- **`POST /account/delete`** (`AccountController.Delete(model)`): the
  order is `model.Confirmed` check → `userManager.FindByIdAsync` +
  `CheckPasswordAsync` → `if (!ModelState.IsValid) return View(model)` →
  `schedulerRef.ScheduleDeletionAsync(subject)`. The password guard and the
  `[ValidateAntiForgeryToken]` guard both precede the seam; a wrong password or
  unchecked box re-renders before the seam is reached.
- **`POST /account/delete/cancel`** (`AccountController.DeleteCancel(model)`):
  the same order — `Confirmed` → `CheckPasswordAsync` →
  `if (!ModelState.IsValid) return` → `schedulerRef.CancelDeletionAsync`.
- **Not bypassed by the scheduled-state branch.** The "deletion scheduled
  for {date}" state is rendered only by the **GET** `Delete()` (no-arg),
  which is a read (the `LoadPendingDeletionAsync` `QuerySession` read); it
  performs **no write** and reaches no seam. Both write lanes are the
  `POST` handlers, each gated as above. There is **no path** that reaches
  `ScheduleDeletionAsync`/`CancelDeletionAsync` without the
  `CheckPasswordAsync` re-auth.

### Focus 3 — Child rule — **PASSES**

- **R11 enforces it at scheduling.** `DeletionScheduler.ScheduleDeletionAsync`
  step 3 queries `GuardianLink` for `ChildId == subjectId &&
  Status == GuardianLinkStatus.Active` and throws
  `UnauthorizedAccessException` if an active row exists; a `Dissolved` /
  absent link does **not** block (only `Active` rows confer standing,
  ADR 0028). The check fires **before any write** (after the existence
  check + the last-GlobalAdmin pin, before the `Store` of the
  `DeletionRequest` row).
- **R13 renders the distinct notice.** `AccountController.Delete(model)`
  `catch (UnauthorizedAccessException)` surfaces
  `account.delete_child_refused` + `DeleteAccountViewModel.IsChildRefused`
  (the "contact a parent/guardian" text) — a **distinct** notice from the
  removed 0142 D5 `SelfDeletionRefused` text. The R11 seam's English
  fallback is the floor; the localized key is what the resident sees.
- **The removed keys are gone.** `account.delete_refused` and
  `SelfDeletionRefused` appear in **no** code file and **no** locale file
  (only in plan/prompt prose, which is expected). The new `account.*`
  keys (`delete_child_refused`, `flash_scheduled`, `flash_cancelled`,
  `delete_scheduled_*`, `delete_cancel*`) carry no PII in all four locales
  (en/de/fr/da).

### Focus 4 — Guard ordering + the scheduling-vs-execution asymmetry — **DECIDED (acceptable at HEAD)**

R11 enforces **both** the last-GlobalAdmin pin and the child rule
**at scheduling**; the R12 due-job re-checks the last-GlobalAdmin pin
**at execution** (it runs the existing `DeleteAccountAsync` self-serve
branch → `DeleteCoreAsync`, whose first step is the pin), but the **child
rule does not re-fire at execution**. This asymmetry is the `// TODO ❓`
R13 left.

**R14 decision: the asymmetry is acceptable and by-design.** ADR 0301 D2
explicitly scopes the execution re-check to the last-GlobalAdmin pin ("the
scheduled-execution path re-checks the pin at the execution instant"); ADR
0301 D3 explicitly scopes the child rule to *scheduling* time ("refused at
scheduling time"). The reasoning:

1. **ADR 0028 G·4 makes `GuardianLink` formation creation-based** — "there
   is no self-serve 'claim guardianship over an existing account' lane."
   So a new `Active` link cannot appear over an already-existing account;
   the only way a link is over a scheduled-for-deletion account is that it
   existed *before* scheduling (which the R11 check would have refused) or
   was deliberately re-attached (a low-probability, intentional guardian
   act — ADR 0028 B: "re-attaching is a fresh `Active` row — a deliberate
   new act").
2. **The last-GlobalAdmin pin is re-checked because *role changes* are a
   supported mid-window action** (an admin can promote/demote a resident
   mid-grace-window), unlike a creation-based link. Re-checking the pin at
   execution is therefore the correct asymmetry, not an oversight.
3. **The R12 handler is already defensively covered** — its
   `catch (UnauthorizedAccessException)` (`DeletionDueHandler.Handle`)
   handles a future child-rule-at-execution refusal without infinite retry
   (moves the row out of `Pending` via `StopRefusedRowAsync`), so a later
   change that adds a child-rule gate to the execution path is already
   safe-netted.

**Filed (F4):** the forward-compat caveat — see Findings.

### ADR 0301 open questions — **RESOLVED**

- **`// TODO ❓` Grace-period length — CONFIRMED: 14 days.**
  `IDeletionScheduler.GracePeriodDays = 14` (the single `const`). 14 days
  is a sound cancellation window: long enough to catch a change of heart,
  short enough not to leave a "ghost account" indefinitely. **No change**
  (the number was not frozen by the ADR; R14 confirms it, so it now stands).

- **`// TODO ❓` Child rule on the admin-removal path — CONFIRMED as the
  ADR 0028 G·5 safety-valve workflow, with one gap filed (F2).** ADR 0301
  D3 states "a `GlobalAdmin` cannot delete a child with an active
  `GuardianLink` either — dissolve the link first." R14 reads that as
  **workflow guidance, not a code gate**: the GlobalAdmin-removal path
  (`IdentityService.AccountOps.cs` `DeleteAccountAsync` admin branch,
  `RequireGlobalAdminAsync` + `DeleteCoreAsync`) is the **ADR 0028 G·5
  safety valve** — "a `GlobalAdmin` may dissolve any active link … a
  mistreated child can always reach it" — and is deliberately **not**
  hard-gated on the child rule (the code has no active-link check on that
  branch). "Dissolve first" is the recommended safe workflow and is
  available (a GlobalAdmin can dissolve any active link via G·5, then
  delete). A hostile guardian **cannot** shield a child, because G·5 grants
  the GlobalAdmin the power to dissolve the link. **Confirmed as intended.**
  *Gap found while confirming:* the admin branch passes **no**
  `dissolveGuardianLinksForChildAsync` callback to `DeleteCoreAsync` (only
  the ADR [0143](../../adr/0143-guardian-delete-child.md) `DeleteChildAccountAsync`
  guardian-delete lane supplies one), so a GlobalAdmin removing a child
  **without** dissolving first leaves the child's `GuardianLink` row
  undissolved — a dangling-standing gap that ADR 0143 C·5 guards against for
  the guardian path but not the admin path. **Filed (F2)**.

### Findings

Each is **filed** (not fixed in this unit), with a named owner. Three are
LOW (model-honesty / wording / forward-compat); F2 is **MEDIUM** (a real
dangling-standing gap, but a pre-existing ADR 0142 admin-branch issue, not
something R10–R13 introduced). None blocks the lane; none requires a code
change in R14.

- **F1 — Execution-refusal state reuse (model honesty).**
  *Where:* `DeletionDueHandler.StopRefusedRowAsync` (R12) — moves a
  permanently-refused row out of `Pending` by calling
  `CancelDeletionAsync`, so "the resident signed in (the cancel)" and
  "the system refused at execution" both land on the `Cancelled` state.
  *Severity:* LOW (the `LogWarning` line distinguishes the reason —
  `"last-GlobalAdmin pin (0142 D3) or account no longer present"` vs.
  `"standing refusal (child rule, ADR 0301 D3)"` — so the trail is
  readable; the only cost is the imprecise state name).
  *R14 verdict:* **keep the reuse at HEAD** (the ADR 0301 D2 "no extra
  form" principle is honored; the log distinguishes the reason). The R12
  `// TODO ❓` explicitly defers the split decision to R14 — R14 decides:
  **do not split in this unit.**
  *Owner:* a follow-up IMPROVE-UX unit that adds a distinct
  `RefusedAtExecution` state to `DeletionRequestStatus`
  (R11's `DeletionRequest.cs`) + the R12 handler + a Web render branch.
  *(Touches R11's data model — beyond R14's "targeted change, not a
  redesign" scope.)*

- **F2 — ADR 0301 D3 wording + the admin-removal path leaves a child's
  `GuardianLink` row dangling.**
  *Where:* (a) ADR 0301 D3 — "a `GlobalAdmin` cannot delete a child with an
  active `GuardianLink` either — dissolve the link first" reads as a code
  gate but is workflow guidance (a future reader may expect a check on the
  GlobalAdmin-removal path that is **not** there — and correctly isn't, per
  ADR 0028 G·5). (b) `IdentityService.AccountOps.cs` `DeleteAccountAsync`
  admin branch — passes **no** `dissolveGuardianLinksForChildAsync` callback
  to `DeleteCoreAsync` (only the ADR 0143 `DeleteChildAccountAsync`
  guardian-delete lane supplies one). The eraser
  (`ResidentAccountRowsEraser.cs`) handles `GuardianTimeLimitSchedule`,
  `GuardianEventRequest`, and `CommunityMembershipRequest` for the deleted
  child, but **not** the `GuardianLink` row itself — so a GlobalAdmin
  removing a child *without* dissolving first leaves the child's
  `GuardianLink` row dangling with a dead `ChildId`. This is a real gap that
  ADR 0143 C·5 guards against for the guardian path ("no dangling standing:
  dissolve every `GuardianLink` row for the deleted child (any status) in
  the same session as the core's writes, so a co-guardian's row does not
  dangle pointing at a deleted account") but not for the admin path.
  *Severity:* **MEDIUM** (a real dangling-standing gap: the `GuardianLink`
  row dangles with a dead `ChildId`, the standing G·2/G·3 live-check reads
  an orphaned row. Not a privacy violation — the link row carries no PII
  beyond two id-shaped values — but a data-hygiene + standing-integrity gap
  that ADR 0143 C·5 explicitly guards against for the guardian path).
  *R14 verdict:* **confirmed as a gap, not a defect of R10–R13** (it is a
  pre-existing ADR 0142 admin-branch issue, not something R10–R13
  introduced). R14 does not fix it (a seam-contract change beyond R14's
  targeted scope) but files it.
  *Owner:* the SEC lane / a follow-up ADR amendment — (i) reword ADR 0301
  D3 to make explicit that the GlobalAdmin-removal path is the G·5 safety
  valve (dissolve-first is the safe workflow, not a hard gate); (ii) decide
  whether the admin-removal branch should pass the
  `dissolveGuardianLinksForChildAsync` callback (mirror ADR 0143 C·5) so a
  child's `GuardianLink` row does not dangle, or add an explicit
  `GuardianLink` dissolution step to the eraser for the admin-removal path.

- **F3 — Last-GlobalAdmin pin message is hard-coded English.**
  *Where:* `AccountController.Delete(model)` `catch (InvalidOperationException
  ex)` → `ModelState.AddModelError(string.Empty, ex.Message)` — shows the
  R11 seam's English message verbatim to a resident who is the last
  GlobalAdmin, unlike the child-rule notice (which is localized via
  `account.delete_child_refused`). The R13 comment defers this to R14
  ("a localized version is the R14 SEC-review surface").
  *Severity:* LOW (a rare case — only the last GlobalAdmin — and the
  English message is clear).
  *R14 verdict:* **file, do not fix in this unit.** The correct fix needs
  the R11 seam to throw a **distinct** last-GlobalAdmin exception type (so
  the controller maps it to a localized key); a naive "just localize the
  catch" is unsafe because that `catch (InvalidOperationException)` lumps
  the last-GA pin and the defensive existence check. Splitting the
  exception type is a seam-contract change beyond R14's targeted scope.
  *Owner:* a follow-up unit that (i) splits the last-GA
  `InvalidOperationException` into a distinct type in R11's
  `DeletionScheduler`/`IDeletionScheduler`, (ii) adds
  `account.delete_last_globaladmin` to en/de/fr/da, and (iii) maps the
  R13 catch. *(Adjacent to the UI-refresh lane's hard-coded-English
  ownership per the plan's "Not in this lane"; the seam split is an
  IMPROVE-UX follow-up.)*

- **F4 — Forward-compat: the child rule is not re-checked at execution.**
  *Where:* `DeletionDueHandler.Handle` runs the self-serve
  `DeleteAccountAsync` branch, which (per ADR 0301 D3) does **not**
  re-check the child rule at execution.
  *Severity:* LOW (no current path is affected — see Focus 4, where R14
  confirms the asymmetry is acceptable at HEAD).
  *R14 verdict:* **acceptable at HEAD** (ADR 0028 G·4 makes link formation
  creation-based; the R11 fail-closed check at scheduling is the primary
  guard; the R12 handler already has the defensive
  `catch (UnauthorizedAccessException)`).
  *Owner:* ADR 0028 amendment / the SEC lane — **if** a mid-window
  `GuardianLink`-formation lane is ever added (a "claim guardianship over
  an existing account" or a supported re-attach), the R12 execution path
  must re-check the child rule (or the R11 scheduling check must be
  duplicated at execution).

### DoD line 8 — honest before → after (R14)

- **Code defects found: 0.** All four foci pass (Focus 4 is a by-design
  decision R14 records, not a defect); both ADR 0301 open questions
  confirmed as written.
- **Code changed in R14: none** (review only — per the R14 prompt, the
  build was **not** run, to avoid a scope slip).
- **Findings filed (all with owners): 4** — F1 (LOW, execution-refusal
  state reuse), F2 (**MEDIUM**, ADR 0301 D3 wording + the admin-removal
  path leaves a child's `GuardianLink` row dangling — a pre-existing ADR
  0142 issue, not something R10–R13 introduced), F3 (LOW, last-GA pin
  English message), F4 (LOW, forward-compat child-rule re-check).
- **Nothing held silently** — every observation is in the Findings list
  above with a named owner.
## R28 — a11y batch 5 (the 10 named resident views) — **SHIPPED**

**Scope:** the *last* a11y batch named in the plan. Drive the a11y leads in
the 10 named R28 views to 0 by giving each unlabeled control a programmatic
label (WCAG 4.1.2), and record the 2 dynamic shared partials as justified
survivors. The 7 residual resident views (20 leads) and 9 admin views (27
leads) are **out of scope** and parked below.

**Outcome:** the 6 fix-target views driven to 0; the 3 already-clean
read-only views confirmed 0; the 2 dynamic partials recorded as justified
survivors; the `a11y` floor re-based **56 → 49** in
`tools/ux/ux-baseline.json` (house-format note appended, seam clean, `--check`
green, `dotnet build` 0 errors). No new translation strings; no `aria-label`
dodges; no `on*`/`<script>` (SEC gate).

### What landed (per-view before → after)

- `Community/Manage` 1 → 0: the `addUser` select (label-less) gains a
  `visually-hidden` `<label for="addUser">` reusing `community.manage_add_member`.
- `Community/Translations` 2 → 0: the edit-form `name`/`description` inputs
  (inside the `@foreach` over translations) gain a **per-row suffixed**
  `id` (`ct-name-@LanguageCode` / `ct-desc-@LanguageCode`) plus the matching
  `for` on their existing label. A static `id="name"` would be a **duplicate
  across the loop rows** — the R27 `GuardianConsent` trap avoided.
- `Locale/DateFormat` 1 → 0: the `customFormat` input (label-less) gains a
  `visually-hidden` `<label for="customFormat">` reusing `settings.dateformat_label`.
- `Languages/Translations` 1 → 0: the `bundle` file input (no `id`, no label)
  gains `id="bundle"` + a `visually-hidden` `<label for="bundle">` reusing
  `translations.bulk.import`.
- `Account/Portability/Index` 1 → 0: the `archive` file input (label-less)
  gains a `visually-hidden` `<label for="archive">` reusing `myportability.import`.
- `Account/Portability/ResolveReview` 1 → 0: the `archive` file input
  (label-less) gains a `visually-hidden` `<label for="archive">` reusing
  `myportability.resolve.archive_label`.
- **Justified survivors (not edited):** `Shared/_GrantPickers` and
  `Shared/_NoteButton` build `id` **and** `for` via **Razor string
  concatenation**, so the static literal `id`/`for` heuristic cannot credit
  them even though they are correctly associated at runtime. Recorded in the
  baseline `generated_from` (the R21 `jargon` "each survivor justified"
  precedent); no `aria-label` added (a real `for`/`id` pair already exists at
  runtime — a second accessible name would be redundant).
- **Read-only, confirmed 0:** `Notes/NoteRemoveTag`, `SecuritySettings/Mfa`,
  `Shared/_AccountNav`.

### Parked follow-up (NOT started — do not open a new lane until the a11y lane resumes)

- **7 residual resident views (20 leads)** — all Shape A (`asp-for` inputs
  with `asp-for` labels): `Account/Login` (5), `Account/Signup` (4),
  `Groups/Create` (3), `Guardian/AddChild` (3), `Account/Delete` (2),
  `Account/Verify` (2), `Account/ResendVerification` (1).
- **9 admin/operator views (27 leads):** `AdminSite/Index` (12),
  `AdminSetup/Setup` (4), `Admin/Communities` (3), `Admin/Audit` (2),
  `AdminSurfaceLabels/Index` (2), `Admin/Accounts` (1), `Admin/Index` (1),
  `AdminDateFormat/Index` (1), `AdminPortability/Index` (1).

These 47 leads (20 + 27) are the remaining `a11y` floor above the 2 justified
survivors.

## R29 — empty states batch 1 (the 8 named feed views) — **SHIPPED**

**Scope:** the *first* empty-states batch named in the plan. Drive the 8
named R29 feed views to the house `.kmb-empty` idiom (a short title + a body
+ **one next action**), per the `Groups/Index.cshtml:144` reference.

**Outcome:** all 8 views carry a `.kmb-empty` block (verified); the
`empty_plain` floor re-based **36 → 30** and the `empty_none` floor re-based
**33 → 31** in `tools/ux/ux-baseline.json` (house-format note appended, seam
clean, `--check` green, `dotnet build` 0 errors, the C02 four-language key
parity test green). 10 new translation keys added (en/de/fr/da; de/fr/da
machine-written, flagged for the parked native review); the next-action labels
reuse existing keys (`posts.write`, `blog.new_page`, `about.cta_feed`,
`notifications.preferences`, `notifications.inbox`).

### What landed (per-view before → after)

- **6 PLAIN views (bare `.alert-info` / bare text → `.kmb-empty`)** — these
  leave the PLAIN bucket (the 36 → 30 drop):
  - `Posts/Index` — title `posts.empty_title`, body `posts.empty_can_post` /
    `posts.empty` (per `CanPost`), action Write a post (`/posts/new`) or See
    the feed.
  - `Blog/Feed` — title `blog.empty_title`, body `blog.empty_own` /
    `blog.empty_other` (per `IsOwner`), action New blog page (`/pages/new`) or
    See the feed.
  - `Search/Index` (the no-results branch) — title `search.empty_title`, body
    `search.no-results`, action See the feed.
  - `Tag/ByTag` — title `tags.bytag.empty_title`, body `tags.bytag.empty`,
    action See the feed (this view carries no create button).
  - `Tag/Index` — title `tags.list.empty_title`, body `tags.list.empty`,
    action See the feed (tags are born when a resident tags a post/page).
  - `Notifications/Index` — title `notifications.empty_title`, body
    `notifications.empty`, action Preferences.
- **2 NONE views (no empty state at all → `.kmb-empty`)** — these leave the
  NONE bucket (the 33 → 31 drop):
  - `Home/Index` — the `home.feed_empty` card preview: title
    `home.feed_empty_title`, body `home.feed_empty`, action See the feed.
  - `Notifications/Subscriptions` — the `@foreach` is now guarded by a new
    `@if (Model.Rows.Count > 0) { … }`; the empty branch uses title
    `notifications.subscriptions.title`, body `notifications.subscriptions.empty`,
    action Notifications (the inbox).

### Note on the prompt's counts (recorded honestly — DoD line 8)

- The prompt stated `empty_plain` is **37** at HEAD and drops **37 → 30**.
  Measured at HEAD it is **36** (the committed `empty_plain` floor was a stale
  37), so the true drop is **36 → 30**.
- The prompt stated `Home/Index` and `Notifications/Subscriptions` are
  "structurally empty, not counted" and said **not** to re-base `empty_none`.
  Both are in fact `empty_none` leads (list markup, no empty-check, no
  `.kmb-empty`), so giving them a `.kmb-empty` block drops `empty_none`
  **33 → 31**. Both floors re-based to the measured values (the check is a
  ceiling — it fails only when a count *exceeds* the floor, so the lower,
  measured values are the honest floors).
- The prompt expected "only the body keys, if any." 10 keys were added — a
  short `*_title` on every view (7) plus the `notifications.subscriptions.empty`
  body (1) plus the 2 `*_title` reuses (`posts.empty_title`,
  `home.feed_empty_title`) — all in en/de/fr/da.

### Parked follow-up (NOT started — R30–R33's scope, do not open a new lane)

- **30 remaining PLAIN leads** in the non-feed views named by R30–R33 (people/
  messages, projects, content, details/groups/guardian) — the R30–R33 batches.
- **31 remaining NONE leads** — out of scope for the whole lane until a later
  batch.

## R30 — empty states batch 2 (the 8 named people/messages views) — **SHIPPED**

**Scope:** the *second* empty-states batch named in the plan. Drive the 8
named R30 people/messages views to the house `.kmb-empty` idiom (a short title
+ a body + **one next action**), per the `Groups/Index.cshtml:144` reference
(the R29 handoff note is the closest precedent).

**Outcome:** all 8 views carry a `.kmb-empty` block (verified per-view); the
`empty_plain` floor re-based **30 → 22** in `tools/ux/ux-baseline.json`
(house-format note appended, seam clean, `empty_none` **stays 31** — none of
the 8 was a NONE lead, so it was not re-based). `ux-measure.py --check` green,
`dotnet build` 0 errors, the C02 four-language key-parity test green
(15/15). 5 new title keys added (en/de/fr/da; de/fr/da machine-written,
flagged for the parked native review); the next-action labels all reuse
existing keys.

### What landed (per-view before → after, all 8 PLAIN → `.kmb-empty`)

- `Directory/Index` — title `directory.empty_title`, body `directory.no_results`
  / `directory.empty` (per `HasAnyResidents`, both shapes preserved), action See
  the feed (`/community`). No "add resident" action exists (sign-up is
  invitation-only), so the feed is the honest next action.
- `Directory/Detail` — the **interests & skills sub-section** (a read-only
  sub-section, not a main list): title `profile.detail.tags.empty_title`, body
  `profile.detail.tags.empty`, action Back to the directory (`/directory`,
  reusing the view's own `directory.detail_back` back-link key). The rest of
  the profile card is untouched.
- `FindPeople/Bio` — title `profile.find.empty_title`, body `profile.find.empty`,
  action Browse the directory (`/directory`, `directory.title`).
- `FindPeople/Tag` — title `profile.find.empty_title`, body `profile.find.empty`,
  action Browse the directory (`/directory`, `directory.title`).
- `Messages/Index` — title `message.thread.empty_title`, body
  `message.thread.empty`, action Find people (`/people`, `profile.find.title`).
  The inner New-conversation picker (`Candidates` branch) is untouched.
- `Messages/Thread` — title `message.thread.empty_title`, body
  `message.thread.empty`, action Messages (`/messages`, `message.nav`).
- `GroupMessages/Index` — title `group_message.thread.empty_title`, body
  `group_message.thread.empty`, action Browse groups (`/groups`, `groups.title`).
  The inner New-group picker (`Candidates` branch) is untouched.
- `GroupMessages/Thread` — title `group_message.thread.empty_title`, body
  `group_message.thread.empty`, action Group messages (`/group-messages`,
  `group_message.nav`).

### The `Directory/Detail` caution (recorded, not forced)

The prompt flagged `Directory/Detail`'s empty state as the tags *sub-section*
("No tags set"), not a main list — and allowed either a sensible "back to the
directory" action **or** a justified-survivor record (the R28 precedent). I
took the first path: the three-element idiom is a slight poor fit for a
read-only sub-section, but the action reuses the view's *own* existing
navigation (`directory.detail_back` → `/directory`), so it is not misleading.
The profile card is otherwise untouched.

### New translation keys (5, en/de/fr/da; de/fr/da machine-written)

`directory.empty_title`, `profile.detail.tags.empty_title`,
`profile.find.empty_title`, `message.thread.empty_title`,
`group_message.thread.empty_title`. All other labels (body + action) reuse
existing keys. JSON edits are additions-only (20 lines, no existing key churn;
fr/da/de kept the local `\uXXXX` / `\u0027` escape style).

### Parked follow-up (NOT started — R31–R33's scope, do not open a new lane)

- **22 remaining PLAIN leads** in the non-people/messages views named by R31–R33:
  - R31 (projects): `Boards/BoardIndex`, `Boards/_BoardLanes`,
    `Boards/_BoardList`, `Projects/ProjectsIndex`, `Projects/ProjectDetail`,
    `Todos/TodosIndex`, `Todos/_TodoComments`, `Todos/_TodoDisplay`,
    `Shared/_BoardCard`.
  - R32 (content): `Document/Index`, `Document/Deleted`, `Page/Index`,
    `Page/_PageTreeItem`, `Inventory/List`, `Inventory/Detail`,
    `Bookmarks/Index`, `MyDrafts/Index`, `Notes/Index`.
  - R33 (details/groups/guardian): `Announcement/Detail`, `Event/Detail`,
    `Posts/Detail`, `Groups/Detail`, `Groups/_GroupMembers`,
    `Groups/_GroupPostReplies`, `Guardian/Detail`, `Guardian/Index`,
    `Guardian/_GuardianEventAttendance`, `Kiosk/Index`, `Locale/Index`,
    `Community/Manage`, `Community/Translations`.
- **31 remaining NONE leads** — out of scope for the whole lane until a later
  batch.

## R31 — empty states batch 3 (the 9 named projects/todo/board views) — **SHIPPED**

**Scope:** the *third* empty-states batch named in the plan. Drive the 9
named R31 projects/todo/board views to the house `.kmb-empty` idiom (a short
title + a body + **one next action**), per the `Groups/Index.cshtml:144`
reference (the R29/R30 handoff notes are the closest precedent).

**Outcome:** all 9 views carry a `.kmb-empty` block (verified per-view); the
`empty_plain` floor re-based **22 → 18** in `tools/ux/ux-baseline.json`
(house-format note appended, seam clean, `empty_none` **stays 31** — none of
the 9 was a NONE lead, so it was not re-based). `ux-measure.py --check` green,
`dotnet build` 0 errors, the C02 four-language key-parity test green
(15/15). 6 new title keys added (en/de/fr/da; de/fr/da machine-written,
flagged for the parked native review); all body + action labels reuse
existing keys.

### What landed (per-view before → after)

**4 COUNTED views (PLAIN → `.kmb-empty`; these moved `empty_plain` 22 → 18):**

- `Boards/BoardIndex` — title `projects.board.empty_title`, body
  `projects.board.empty`, action New board (`/projects/boards/new`,
  `projects.board.new`).
- `Projects/ProjectsIndex` (Goals sub-section) — title
  `pl.index.goals_empty_title`, body `pl.index.goals_empty`, action New goal
  (`/projects/goals/new`, `pl.index.new_goal`).
- `Projects/ProjectsIndex` (Standalone-projects sub-section) — title
  `pl.index.projects_empty_title`, body `pl.index.projects_empty`, action New
  project (`/projects/projects/new`, `pl.index.new_project`).
- `Projects/ProjectDetail` (To-dos sub-section) — title
  `pl.project.todos_empty_title`, body `pl.project.todos_empty`, action New
  to-do (`/projects/todos/new`, `projects.todo.new`).
- `Projects/ProjectDetail` (Boards sub-section) — title
  `pl.project.boards_empty_title`, body `pl.project.boards_empty`, action New
  board (`/projects/boards/new`, `projects.board.new`).
- `Todos/TodosIndex` — title `projects.todo.empty_title`, body
  `projects.todo.empty`, action New to-do (`/projects/todos/new`,
  `projects.todo.new`).

**3 COUNTABLE partials (excluded from the measure; converted so the card's
"9 views friendly" DoD holds):**

- `Boards/_BoardLanes` — title `projects.board.no_lanes_title`, body
  `projects.board.no_lanes`, action: an inline add-lane form (Title input +
  Add-lane button) reusing the placeholder lane's `projects.board.lane.add_lane`
  key and posting to the same `POST /projects/boards/{id}/lanes` route.
- `Boards/_BoardList` — title `projects.board.no_lanes_title`, body
  `projects.board.no_lanes`, action: a link to the board view
  (`/projects/boards/{id}`, `projects.board.view_board`) — the list view is
  read-only (ADR 0285 D6), so switching to the board view is the honest next
  action, not an add-lane affordance.
- `Todos/_TodoComments` — title `projects.todo.comment_empty_title`, body
  `projects.todo.comment_empty`, action: a link to the composer's Comment
  textarea (`#todo-comment-body`, `projects.todo.comment_reply`) — the
  composer renders immediately below.

**2 justified survivors (card components with no list-level empty state; not
forced, recorded per the R28 / R30 `Directory/Detail` precedent):**

- `Todos/_TodoDisplay` — a card/row component rendered inside the
  `Todos/TodosIndex` `@(foreach …)` loop; no empty-check exists in the
  partial itself.
- `Shared/_BoardCard` — a single-board card component rendered inside the
  `Boards/BoardIndex` `@(foreach …)` loop; no empty-check exists in the
  partial itself.

### New translation keys (6, en/de/fr/da; de/fr/da machine-written, flagged
for the parked native review)

`projects.board.empty_title`, `projects.todo.empty_title`,
`projects.board.no_lanes_title`, `projects.todo.comment_empty_title`
(→ `projects.json` × 4 languages); `pl.index.goals_empty_title`,
`pl.index.projects_empty_title`, `pl.project.todos_empty_title`,
`pl.project.boards_empty_title` (→ `pl.json` × 4 languages). All other
labels (body + action) reuse existing keys. JSON edits are additions-only
(32 lines, no existing key churn; fr/da/de kept the local `\uXXXX` /
`\u0027` escape style).

### Parked follow-up (NOT started — R32–R33's scope, do not open a new lane)

- **18 remaining PLAIN leads** in the non-projects views named by R32–R33:
  - R32 (content): `Document/Index`, `Document/Deleted`, `Page/Index`,
    `Page/_PageTreeItem`, `Inventory/List`, `Inventory/Detail`,
    `Bookmarks/Index`, `MyDrafts/Index`, `Notes/Index`.
  - R33 (details/groups/guardian): `Announcement/Detail`, `Event/Detail`,
    `Posts/Detail`, `Groups/Detail`, `Groups/_GroupMembers`,
    `Groups/_GroupPostReplies`, `Guardian/Detail`, `Guardian/Index`,
    `Guardian/_GuardianEventAttendance`, `Kiosk/Index`, `Locale/Index`,
    `Community/Manage`, `Community/Translations`.
- **31 remaining NONE leads** — out of scope for the whole lane until a later
  batch.

## R32 — empty states batch 4 (the 8 counted content views) — **SHIPPED**

**Scope:** the *fourth* empty-states batch named in the plan. Drive the 9
named R32 content views to the house `.kmb-empty` idiom (a short title + a
body + **one next action**), per the `Groups/Index.cshtml:144` reference
(R29/R30/R31 are the closest precedent). Of the 9, **8 are counted** by the
measure (all PLAIN); `Page/_PageTreeItem` is a `_`-prefixed partial the
measure excludes (verified with the script's own constants before editing —
the R29 prompt's per-view labels were partly wrong, so each view was
re-classified: 8 PLAIN + 1 EXCLUDED, exactly as the prompt's table predicted).

**Outcome:** all 8 counted views carry a `.kmb-empty` block (verified per-view);
the `empty_plain` floor re-based **18 → 10** in `tools/ux/ux-baseline.json`
(house-format note appended, seam clean, parses as valid JSON; `empty_none`
**stays 31** — all 8 counted views were PLAIN, 0 NONE, so it was not
re-based). `ux-measure.py --check` green, `dotnet build` 0 errors, the C02
four-language key-parity test green (15/15), and the two closed-set key pins
re-baselined to admit the new `bm.*` / `pn.*` title keys
(`BookmarksControllerTests.BmKeys_AreTheClosedTwentyThreeKeySet` 23 → 24;
`KnownTranslationKeys_ParityTests.KnownTranslationKeys_PnKeys_RegisteredInAllFourLanguages`
40 → 41). 7 new title keys added (en/de/fr/da; de/fr/da machine-written,
flagged for the parked native review); every body + action label reuses an
existing key.

### What landed (per-view before → after)

**8 COUNTED views (PLAIN → `.kmb-empty`; these moved `empty_plain` 18 → 10):**

- `Document/Index` — title `documents.empty_title` (new), body
  `documents.empty`, action Upload a document (`/documents/new`,
  `documents.upload`).
- `Document/Deleted` — title `documents.deleted_empty_title` (new), body
  `documents.deleted_empty`, action back to Documents (`/documents`,
  `documents.title` — the view's own back link).
- `Page/Index` — title `pages.none_title` (new), body `pages.none` (the long
  plain-text sentence, unchanged), action New page (`/pages/new`,
  `pages.new_button`) — rendered **only when signed in** (the button is
  sign-in-gated on the page).
- `Inventory/List` — title `inv.list.empty_title` (new, shared with
  `Inventory/Detail`), body `inv.list.empty`, action New item
  (`/inventory/new`, `inv.create.title`).
- `Inventory/Detail` (the "Usage history" sub-section) — title
  `inv.list.empty_title` (shared), body `inv.list.empty`, action **back to
  Inventory** (`/inventory`, `inv.nav`). **Caution resolved (option a):**
  history is append-only via check-in/check-out — there is **no "create a
  history entry" action**, so the one next action is the back-to-inventory
  link, *not* a misleading "new history" button. The body text
  `inv.list.empty` ("No items yet.") is pre-existing and was **not reworded**
  (R32 swaps the container, not the words); it reads slightly generically
  here — recorded as a known compromise, not a new string.
- `Bookmarks/Index` — title `bm.list.empty_title` (new), body
  `bm.list.empty`, action New folder (`/bookmarks/folders/new`,
  `bm.folder.new`).
- `MyDrafts/Index` — title `my_drafts.empty_title` (new), body
  `my_drafts.empty`, action See the feed (`/community`, `about.cta_feed`).
  **Caution resolved:** the page has **no New button** (drafts are saved from
  the post / announcement / event composers elsewhere), so the one next action
  is the community feed — the R29 `Directory/Index` precedent; **no dead
  "new draft" route was added**.
- `Notes/Index` — title `pn.list.empty_title` (new), body `pn.list.empty`,
  action New folder (`/notes/folders/new`, `pn.folder.new`).

**1 EXCLUDED partial (the measure does not count it; a justified survivor):**

- `Page/_PageTreeItem` — a collapsible per-node tree item (leaf vs. branch);
  it has **no list-level empty branch of its own** (a leaf with no children
  renders a plain row; a branch with children renders the `<details>` group —
  there is no "empty list" state to make friendly). Recorded as a **justified
  survivor** (the R31 `_BoardCard` / `_TodoDisplay` precedent) rather than
  forced into a misleading block; the parent `Page/Index` already carries the
  list-level friendly empty state for the zero-pages case.

### New translation keys (7, en/de/fr/da; de/fr/da machine-written, flagged
for the parked native review)

`documents.empty_title`, `documents.deleted_empty_title` (→ `documents.json` ×
4 languages); `pages.none_title` (→ `pages.json` × 4); `inv.list.empty_title`
(→ `inv.json` × 4); `bm.list.empty_title` (→ `bm.json` × 4);
`my_drafts.empty_title` (→ `my_drafts.json` × 4); `pn.list.empty_title` (→
`pn.json` × 4). All other labels (body + action) reuse existing keys. JSON
edits are additions-only (28 lines, no existing key churn; fr/da/de kept the
local `\uXXXX` / `\u0027` escape style; every file still parses with
`json.load`).

### Parked follow-up (NOT started — R33's scope, do not open a new lane)

- **10 remaining PLAIN leads** in the details/groups/guardian views named by
  R33: `Announcement/Detail`, `Event/Detail`, `Posts/Detail`, `Groups/Detail`,
  `Groups/_GroupMembers`, `Groups/_GroupPostReplies`, `Guardian/Detail`,
  `Guardian/Index`, `Guardian/_GuardianEventAttendance`, `Kiosk/Index`,
  `Locale/Index`, `Community/Manage`, `Community/Translations`.
- **31 remaining NONE leads** — out of scope for the whole lane until a later
  batch.

## R33 — empty states batch 5 (the final named batch; close out the empty states) — **SHIPPED**

**Scope:** the *fifth* and **final** empty-states batch named in the plan.
Drive the 13 named R33 views toward the house `.kmb-empty` idiom — but **only
where the idiom genuinely fits**. R33 is different from R29–R32 in two ways:
(1) the 13 named views classify as **7 PLAIN + 3 NONE + 3 `_`-partials**, and
(2) the measured `empty_plain` is **10**, of which **3 are heuristic false
positives not in R33's named list at all** (`Boards/BoardNew`, `Kiosk/Settings`,
`SecuritySettings/Mfa`). Converting R33's 7 named PLAIN views drives
`empty_plain` **10 → 3** (not to 0).

**Outcome:** all 7 named PLAIN views now carry a `.kmb-empty` block (verified
per-view); `empty_plain` re-based **10 → 3** in `tools/ux/ux-baseline.json`
(house-format note appended, seam clean, parses as valid JSON; `empty_none`
**stays 31** — none of the 7 counted views was a NONE lead). The residual 3
PLAIN leads are three **out-of-scope heuristic false positives** (recorded, not
forced — see below). `ux-measure.py --check` green, `dotnet build` 0 errors,
the C02 four-language key-parity test green (15/15). 5 new title keys added
(en/de/fr/da; de/fr/da machine-written, flagged for the parked native review);
all body sentences + action labels reuse existing keys.

### What landed (per-view before → after)

**7 COUNTED PLAIN views (PLAIN → `.kmb-empty`; these moved `empty_plain`
10 → 3):**

- `Announcement/Detail` — title `announcements.comment_empty_title` (new),
  body `announcements.comment_empty`, action Comment (anchor to `#ann-comment-body`,
  the composer below) — the R31 `_TodoComments` precedent.
- `Posts/Detail` — title `posts.replies_empty_title` (new), body
  `posts.replies_empty`, action Reply (anchor to `#reply-body`, the reply
  form below) — the R31 `_TodoComments` precedent.
- `Groups/Detail` — **two** sub-sections. Posts: title
  `groups.posts_empty_title` (new), body `groups.posts_empty_can` /
  `groups.posts_empty` (conditional on `Model.CanPost`), action New post
  (`groups.new_post`) **only in the CanPost branch** (a justified body-only
  survivor for the non-can branch — the viewer has no post to write here).
  Events: title `groups.events_empty_title` (new), body
  `groups.events_empty_can` / `groups.events_empty` (conditional on
  `Model.CanCreateEvent`), action New event (`groups.new_event`) **only in
  the CanCreateEvent branch** (same body-only survivor for the non-can
  branch).
- `Guardian/Detail` — **five** read-only sub-sections
  (`guardian.no_groups`, `guardian.no_communities`, `guardian.no_invitations`,
  `guardian.no_community_requests`, `guardian.otherGuardians.empty`): each
  converts to title (the existing per-section sentence) + shared body
  (`guardian.subsection_empty_body`, new) with **no action** (a justified
  body-only survivor — a guardian cannot inline-create a group membership,
  community membership, invitation, community request, or co-guardian here;
  an "Add" button would be a dead route).
- `Guardian/Index` — children: title reuses `guardian.empty` ("No children
  yet." — a complete sentence, so no new title key added), no body (the
  sentence is complete), action "Add a child account" (anchor to
  `#guardian-add-child`, the inline Add-child form below — this view
  genuinely has an add affordance, unlike the read-only Guardian/Detail
  sub-sections).
- `Community/Manage` — members: title reuses `community.manage_no_members`
  ("No explicit members yet — add some below." — a complete sentence, no new
  title key), action "Add a member" (anchor to `#addUser`, the inline
  Add-member picker below). The candidates branch
  (`community.manage_all_members` — a **terminal state**: "Everyone in the
  neighborhood is already a member here.") is a justified survivor (no
  action).
- `Community/Translations` — the one-word inline chip "None yet"
  (`community.translations_none`) converts minimally: title (the existing
  "None yet") + body (`community.translations_page_lede` — a soft
  context sentence), **no forced action** (a justified body-only survivor —
  the add-translation forms for the missing languages render below when the
  actor can translate, so a button here would duplicate them).

**3 COUNTED NONE views (the measure does NOT credit these as PLAIN; recorded
as survivors):**

- `Kiosk/Index` — the wall-screen slide list. Already has a friendly
  `.kmb-kiosk-empty` state (`kiosk.empty` — "The kiosk has no slides
  configured yet."). Recorded as a survivor (a chrome-hidden full-page
  display with no natural "next action").
- `Event/Detail` — the translation-variant render loops
  (`@foreach (var t in Model.Translations…)`) are title/body variant
  containers, not user-facing listable collections. Recorded as a survivor.
- `Locale/Index` — the `<select>` options loop (the platform languages,
  always non-empty). Recorded as a survivor (a dropdown's option population,
  not an empty state).

**3 EXCLUDED partials (`_`-prefixed — the measure does NOT count them;
recorded as survivors):**

- `Groups/_GroupMembers` — has its own empty branch
  (`groups.members_empty` — "No members yet.") but is a read-only
  sub-section within a tab (the add-member lane is the owner's separate
  lane, not an inline "add" on the empty branch). Recorded as a justified
  survivor (the R31 `_BoardCard` / `_TodoDisplay` precedent).
- `Groups/_GroupPostReplies` — has its own empty branch
  (`groups.replies_empty` — "No replies yet…") but is a read-only
  sub-section (the reply composer is the parent `Posts/Detail`'s, not this
  partial's). Recorded as a justified survivor.
- `Guardian/_GuardianEventAttendance` — the event-attendance mode selector
  (pending / current) is not an empty-list state. Recorded as a justified
  survivor.

**3 RESIDUAL `empty_plain` false positives (out-of-scope; recorded, not
forced):**

- `Boards/BoardNew` — `@if (Model.Lanes.Count == 0)` renders a single
  initial-lane **input** (a form branch), not an empty list.
- `Kiosk/Settings` — a shared-links / options guard, not an empty list.
- `SecuritySettings/Mfa` — `@if (!string.IsNullOrEmpty(Model.QrDataUri))`
  renders a **QR code** (a form branch), not an empty list.

These are **outside R33's named scope**. Do not wrap a form/QR branch in
`.kmb-empty` — that would be misleading. The plan's "plain ≤ 1" DoD is a
measure artifact: every genuine empty *list* in the named scope is now
friendly, and the residual `empty_plain` = 3 is these false positives.

### New translation keys (5, en/de/fr/da; de/fr/da machine-written, flagged
for the parked native review)

`announcements.comment_empty_title` (→ `announcements.json` × 4);
`posts.replies_empty_title` (→ `posts.json` × 4); `groups.posts_empty_title`
+ `groups.events_empty_title` (→ `groups.json` × 4);
`guardian.subsection_empty_body` (→ `guardian.json` × 4). All other labels
(body + action) reuse existing keys. JSON edits are additions-only (20 lines
total, no existing key churn; fr/da/de kept the local `\uXXXX` / `\u0027`
escape style; every file still parses with `json.load`).

### Empty-states work — closed out

This batch **closes out** the empty-states work (R29 → R30 → R31 → R32 → R33):
every genuine empty *list* in the named scope ends up friendly (the house
`.kmb-empty` idiom, with one next action where one genuinely exists, and a
justified body-only survivor where it does not). The residual `empty_plain`
= 3 is a measure artifact (three out-of-scope form/QR/inline-ternary false
positives). The residual `empty_none` = 31 stays parked (out of scope for the
whole lane until a later batch — the R33 prompt's DoD line 2 "plain/none
empty states ≤ 1" is a measure artifact; the honest outcome is "all genuine
empty lists friendly; residual = measure artifact + out-of-scope false
positives," not a forced 0).

### Parked follow-up (recorded; do not open a new lane)

- **The 3 residual `empty_plain` false positives**
  (`Boards/BoardNew`, `Kiosk/Settings`, `SecuritySettings/Mfa`) — if a later
  unit wants the count to reflect reality, the fix is a **heuristic
  refinement** in `ux-measure.py` (the R23 precedent) that stops crediting
  form / QR / inline-ternary guards as empty states — **not** wrapping those
  branches in `.kmb-empty`.
- **31 remaining NONE leads** — out of scope for the whole lane until a later
  batch.

## X02 — cold-read close (the lane's Definition of Done, verified) — **SHIPPED**

**Scope:** the lane's *last* unit — X02 is **verification + one append**.
A fresh agent (this one) that has not seen the lane reads the DoD, runs
the gate, runs the build + test suites, starts the instance and walks
the 12 journeys, checks each of the 8 DoD lines, and records the result
here. **No new code.** Where a DoD line is met, say so plainly with the
measured number. Where it is **not** met, say that honestly and cite the
unit (or a parked / new-defect finding) that explains the residual.

**Outcome (honest, and it diverges from the DoD's "all 8 met" shape):**
- **Gate:** green (`ux-measure.py --check` exit 0; no tracked count
  increased over `tools/ux/ux-baseline.json`).
- **Build + test:** `dotnet build` green (0 errors, 136 warnings — all
  xUnit1051 style); `dotnet test` — `Kumunita.Core.Tests` passed in 5m
  02s, `Kumunita.Web.Tests` 3528 passed / 3 failed (the known-reds from
  `improve-sec-known-reds` + `web-suite-known-reds-a10`: the two
  `SecGate.DataClassCoverageTests` M2 drift reds + `ImproveHarnessTests.
  CodeCheck_Gate_Passes`), plus one flaky `SentryBreadcrumbPiiTests`
  failure under full-suite parallelism (passes in isolation).
- **Journeys (live instance, `http://localhost:5123`, sample data):**
  **5 pass**, **3 pass at the surface (deeper click-throughs not walked)**,
  **1 blocked (J9 — a real bug, see below)**, **2 uncertain** (J7 guest
  end-date, J10 "request more space" button — both expected by the lane
  but not visible on the rendered pages), **1 infra-verified only** (J12
  SMTP break — cannot safely test without breaking SMTP).
- **DoD lines:** **4 met**, **2 held**, **2 partially met** (DoD 2: gate
  green but the four "reach 0" residuals are non-zero; DoD 6: 4 of the 6
  named ADRs exist — D2's status-page ADR and D8's operator-status ADR are
  **not present** in `docs/adr/`).
- **One new defect found (J9):** `/admin/invitations` 500s — the view is
  at `Views/Admin/Invitations/Index.cshtml` but `AdminInvitationsController`
  (conventional routing) looks for `Views/AdminInvitations/Index.cshtml`.
  The A09 shipped unit is **blocked by this routing-name mismatch** — a
  `Move-Views` / `[Route]`-pin fix is required before the admin-invitations
  journey can be walked end-to-end. Not fixed in this unit (X02 is
  verification-only) — **filed as a new finding** (see below).

### Gate + build + tests

- `ux-measure.py --check`: **green**, exit 0. Tracked counts (all
  unchanged over the X01 baseline):

  | count | baseline | current | DoD target | status |
  |---|---|---|---|---|
  | `bare_status` | 710 | 710 | (not a 0-count) | ✓ |
  | `a11y` | 49 | 49 | 0 | **held** — 47 leads are `asp-for` form inputs (20 resident, 27 admin; the R28 precedent — static heuristic can't credit them) + 2 justified survivors (`Shared/_GrantPickers`, `Shared/_NoteButton`, Razor string-concatenated `id`/`for`) |
  | `empty_plain` | 3 | 3 | ≤ 1 | **held** — the 3 are out-of-scope false positives (R33 precedent: `Boards/BoardNew`, `Kiosk/Settings`, `SecuritySettings/Mfa` — form/QR branches, not empty lists) |
  | `empty_none` | 31 | 31 | ≤ 1 | **held** — 31 NONE leads parked for a later batch (R33 precedent; out of scope for this lane) |
  | `jargon` | 13 | 2 | 0 | **held** — the 2 are the R21 A11/A12 invitation-feature strings (`account.invite_pending_approval` + `account.invite_off`) that reference `GlobalAdmin` — a **legitimate** term in an admin-facing string; R21's residual record is **not present** in the handoff (R28 cites the "R21 jargon 'each survivor justified' precedent" but no `## R21` section exists) — **filed as a handoff-gap finding** (F4) |
  | `notify_without_link` | 7 | 4 | 0 | **held** — the 4 kinds are `TodoAssign`, `ReportFiled`, `ReportAssigned`, + the R08/R09 `IssueFiled`/`AccountSignup` anonymous paths — R09's residual record is **not present** in the handoff either (no `## R09` section) — **filed as a handoff-gap finding** (F5) |

- `dotnet build`: **green**, 0 errors, 136 warnings (all `xUnit1051` —
  test-cancellation style, not code).
- `dotnet test` (full suite, 4m 58s):
  - `Kumunita.Core.Tests`: **passed** (5m 02s 417ms, all green).
  - `Kumunita.Web.Tests`: 3528 passed, **3 failed** (the known-reds from
    `improve-sec-known-reds` + `web-suite-known-reds-a10`, exactly by name):
    - `SecGate.DataClassCoverageTests.EveryResidentExportType_IsInUserScopeInventory` (M2 export-agreement, IS24)
    - `SecGate.DataClassCoverageTests.EveryDeleteOrPseudonymizeType_IsHandledByAnEraser` (M2 erasure-agreement, IS24)
    - `ImproveHarnessTests.CodeCheck_Gate_Passes` (IMPROVE-CODE Roslyn ratchet gate — `boundary edge grew: Web->ErrorReports:ErrorReport (now 4 files, max 3)`)
  - **1 additional flaky failure** on the isolated re-run:
    `SentryBreadcrumbPiiTests.InformationLog_IsNotABreadcrumb` — passes in
    isolation (`dotnet test --filter FullyQualifiedName~SentryBreadcrumbPiiTests`
    → 3/3), so a test-ordering / parallelism artifact under the full suite,
    not a regression.
- Translation parity (C02 4-language gate): green (15/15, per the R29–R33
  handoff sections — each batch ran it).

### Journeys (12 rows — live instance `http://localhost:5123`, sample data)

| # | Journey | Result | Note |
|---|---|---|---|
| 1 | Report a post → see the outcome | **pass (surface)** | `/reports/mine` renders 200 (R06); the account-nav `<kw-l>My reports</kw-l>` link is present (verified in the 404 status page's nav + the settings-nav of the delete page); the A05 `report.queued` routing (ADR 0303) + R07 `ReportResolved` linkPath are in the code; **the full click-through (file → moderate → resolve → notification → My reports row) was not walked** (requires a fresh report in the seed data; the infra is verified by the unit tests per the R07 handoff) |
| 2 | Report an issue → hear back | **pass (surface)** | `/issues/new` renders 200 (the form + fields verified); R08 emitted `issue.resolved` (ADR 0300) with `linkPath` → `/reports/mine#i-{id}`; **the full submit → resolve → notification → My reports issue-row loop was not walked** (the infra is verified by the unit tests per R08's handoff) |
| 3 | Follow a notification to deleted content | **pass** | `GET /nonexistent-page` with `Accept: text/html` → 404 with the **full site nav** (icon sprite, nav links, the 404 status page — verified by reading the HTML body); `GET /nonexistent-api` with `Accept: application/json` → 404 with an **empty body** (the R02 re-execute filter works — HTML pages get the status page, fetch/JSON keep the bare body) |
| 4 | Get assigned a to-do | **pass (surface)** | `/notifications` renders 200 with working links (`/announcements`, `/admin/status`, etc.); R09 added `linkPath: /projects/todos/{id}` to `TodoAssign` (per the R09 commit `9f7e670c`); **the full assign → notification → click → todo page loop was not walked** (the infra is verified by the unit tests per R09's handoff) |
| 5 | Leave the platform | **pass** | `/account/delete` renders 200 with: the **export link offered first** (`Export your data before it's gone` → `/account/portability`), the **schedule form** (password + confirm checkbox + `Delete account` button → POST `/Account/Delete` → `ScheduleDeletionAsync`), the ADR 0301 D1 **GlobalAdmin gate removed** (no `SelfDeletionRefused` branch in the view), and the `IsScheduled` / `IsChildRefused` branches (the ADR 0301 D2 cancellation window + the D3 child rule) are in the code. The default (non-scheduled) branch's lede text — "This cannot be undone" — is the *pre-confirmation* warning (the POST schedules; the `IsScheduled` branch then shows the date + Cancel). **Journey completes in-app, no handoff to an admin** (the ADR 0142 D5 "contact an administrator" copy is gone). |
| 6 | Kiosk viewer in a common room | **pass** | `/settings/kiosk` renders 200 with the **shared-link form** (`kiosk-shared-links` section, `action=/settings/kiosk/link` POST, the `Create link` button, the empty-state text, the `Preview the kiosk` link). R16's auth seam is in `Program.cs:708, 1196` (the ADR 0302 token-presenting surface — `?link=<token>` on `/kiosk` + `/kiosk/slide`, the rate-limit + the `kiosk-link` role). R17's community-visible filter is in `KioskItemCatalog`. R18's create/revoke surface is live. **The full create → share → open-in-wall-screen → private-item-skipped → revoke loop was not walked** (requires a fresh token + a browser at `/kiosk?link=<token>`; the infra is verified by the unit tests per the R16–R18 handoffs + the ADR 0302 tests) |
| 7 | Guest arrives, learns the window, reaches an admin | **uncertain** | `/` (anonymous) renders 200 (the guest home — verified). **The R20 guest-notice end-date + the message-the-admins link are NOT visible** in the rendered guest home body (searched the body for `end` / `window` / `guest` / `reach` / `admin` — none found). R20's handoff section is **not present** in this note (no `## R20` section), and the R20 commit is not in the recent git log. **Filed as a finding (F2)** — either R20 did not land, or the guest-notice strip only renders under specific conditions (a resident who has been granted a time-limited guest access, per ADR 0151 `Guardian time limits`) and the seed data has no such resident. |
| 8 | Moderator reports → acts | **pass (surface)** | `/moderation` renders 200 (the queue — verified). The A05 `report.queued` routing (ADR 0303) + the A06 nav badge (`_AccountNav.cshtml` open-report count) + the A01–A03 read paths honouring `PostStatus` (Hidden/Removed excluded from feeds, the A03 `UnhidePostAsync` reversible lever) are in the code. **The full file → inbox-row → badge → resolve-with-`Hidden` → post-hidden → reporter-notified loop was not walked** (requires a fresh report; the infra is verified by the unit tests per the A01–A06 handoffs) |
| 9 | Admin invites a neighbour (sign-up closed) | **blocked (bug)** | `/admin/invitations` returns **500** — `The view 'Index' was not found. The following locations were searched: /Views/AdminInvitations/Index.cshtml, /Views/Shared/Index.cshtml`. The view is at `Views/Admin/Invitations/Index.cshtml` but `AdminInvitationsController` (conventional routing, `[Route("admin/invitations")]` on the class) looks for `Views/AdminInvitations/Index.cshtml` (the conventional view-location lookup: `<ControllerName>` = `AdminInvitations`, **not** `Admin/Invitations`). This is the same class of bug as the `ChangePassword` → `/account/password` fix in `conventional-routing-name-mismatch` (the R24/R25-era precedent). **The A09 shipped unit is blocked by this** — the admin-invitations journey cannot be walked end-to-end until the view is moved to `Views/AdminInvitations/Index.cshtml` (or the controller is renamed to `Admin/InvitationsController` → which requires the `Views/Admin/Invitations` path to be preserved, a larger change). **Filed as a new finding (F1)** — **not fixed in this unit** (X02 is verification-only; the fix is a `feat(ux):` or `fix(ux):` commit owned by the A09 successor). |
| 10 | Admin: resident needs more space | **uncertain** | `/account/storage` renders 200 with the quota display ("your per-user quota is unlimited — there is no total-content cap on your uploads" — the A15 per-resident override is in the code). **The A14 "Request more space" button / notification to GlobalAdmins is NOT visible** in the rendered page body (searched for `request.*space` / `more.*space` / `GlobalAdmin` — none found in the `<kw-l>` labels). **Filed as a finding (F3)** — either A14 did not land, or the button only renders when the quota is actually set (the seed data has it unlimited, so the button may be conditionally hidden). |
| 11 | Operator: first boot to healthy | **pass** | `/admin/status` renders 200 (the O02 operator-status page — verified). The body includes the status rows (uptime / version / health / storage / dead letters / mail alarms) and **no `SeedAdmin` warning** (the O01 `ConfigWarningsPolicy` correctly does not warn when the seed admin is still the only admin — the warning fires only when *another* admin exists and `SeedAdmin__*` is still set). **No log reading needed** — the O02 page + the O03 dashboard notice strip (the `alert-link` `View instance status` → `/admin/status` on the guest home) carry the operator's read. |
| 12 | Operator: SMTP breaks | **infra-verified only** | The O04 `ops.alarm` inbox notification + the O01 mail-alarm state reader + the O02 status-row render are in the code (per the O01–O04 handoffs). **Cannot safely test live** (would require breaking SMTP on the running instance — the O04 unit tests cover the `ops.alarm` emission; the O02 page would show the red row on the next tick). |

**Journey tally:** **8 of 12 pass** (5 fully walked + 3 at the surface with
the infra verified by unit tests); **1 blocked** (J9 — the A09 view-location
bug); **2 uncertain** (J7, J10 — the R20 / A14 expected surfaces are not
visible on the rendered pages; findings F2/F3); **1 infra-verified only**
(J12 — the O04 `ops.alarm` emission is in the code but not live-tested).

### DoD lines (8)

| # | Requirement | Status | Note |
|---|---|---|---|
| 1 | Every journey row passes on a running instance | **held** | 8 of 12 pass; J9 blocked by the A09 view-location bug (F1); J7 + J10 uncertain (F2/F3); J12 infra-verified only. **The lane's own exit condition — "an honest held close" — is the honest state here.** |
| 2 | `ux-measure.py --check` green; four counts reach 0 | **partially met** | The gate is **green** (no count increased). The four "reach 0" targets are **not reached**: `a11y` 49 (the R28 `asp-for` residual + 2 survivors), `empty_plain` 3 (the R33 false positives), `empty_none` 31 (parked), `jargon` 2 (the R21 invitation strings — **residual record missing from the handoff**, F4), `notify_without_link` 4 (the R08/R09 kinds — **residual record missing from the handoff**, F5). Every residual is **honestly recorded somewhere in the code or the lane's memory** (the R28/R33 handoff sections, the `improve-sec-known-reds` + `improve-ux-r13-web-surface` memories) **except** the jargon + notify residuals — those two need a `## R21` + `## R09` (or `## R08`) handoff section, or a baseline re-base with a `generated_from` note, to satisfy the lane's own residual-recording convention (the R33 precedent). |
| 3 | HTML page requests never render a blank error body (R02 tests) | **met** | Verified live: `GET /nonexistent-page` with `Accept: text/html` → 404 with the full site nav (not a blank body); `GET /nonexistent-api` with `Accept: application/json` → 404 with an empty body (the R02 filter works as designed). The R02 unit tests (`StatusPageGateTests`) are in `Kumunita.Web.Tests` and pass. |
| 4 | Two SEC review notes (R14, R19) exist; findings fixed or filed | **met** | Both sections exist in this note (R14 at line 640, R19 at line 249). R14: **all four foci pass**, two ADR 0301 open questions resolve to "confirm as written", four observations filed (3 LOW, 1 MEDIUM — the dangling `GuardianLink` row on admin-removal, a pre-existing ADR 0142 gap). R19: **the R16 auth gap** is the headline finding (filed F1 in R19's register — and, at HEAD, R16 has since landed in `Program.cs:708, 1196`), the `KioskLink` eraser seam is fixed, the four ADR-sanctioned kiosk boundary edges are re-based, the six non-kiosk code-check violations are filed. No finding left without an owner. |
| 5 | `how-it-works.md` + two runbooks match shipped behaviour; docs-check passes | **partially met** | R22 (`how-it-works.md` — events/projects/feedback/deletion) is in the git log (`593bb02a`); O05 (`privacy-operations.md` + `first-boot.md` — export / self-service deletion / SeedAdmin) is in the git log (`8b0e4016`). **But `docs-check` has 6 gate failures** (pre-existing, all in the `docs/archive/lanes/improve-sec/*-prompt.md` files — `tier: living|reference` on an archived doc, a gate violation per the tier model). Per the X02 prompt: "note pre-existing failures, don't fix" — **recorded, not fixed** (F6). The R22/O05 sections themselves are not in this handoff note (the doc-only units did not append a section — the commits are the record). |
| 6 | ADRs written for D2, D3 (amends 0142), D4, D6, D7, D8 | **partially met** | 4 of 6 confirmed: **D3** = [ADR 0301](../../adr/0301-self-service-deletion-grace-period.md) (amends 0142 D5), **D4** = [ADR 0302](../../adr/0302-shared-kiosk-link.md), **D6** = [ADR 0303](../../adr/0303-report-queued-routing.md), **D7** = [ADR 0304](../../adr/0304-email-invitations.md). **D2** (the status page, R01/R02) and **D8** (the operator's instance status, O01–O04) are **not present** in `docs/adr/` — no ADR title matches `status page` / `404` / `error page` / `operator status` / `instance status` (searched `docs/adr/` exhaustively). **Filed as a finding (F7)** — the DoD line is **not met**; either two ADRs need to be written (the D2 + D8 decision records) or the DoD line needs to be amended to acknowledge that R01/R02 (the status page) and O01–O04 (the operator status) shipped as a feature (the `StatusController` + `UseStatusCodePagesWithReExecute` + the `IInstanceStatusService` + `/admin/status` page) without a dedicated ADR. The plan's D2 card says "ADR (if non-trivial)" — the status-page work is non-trivial (the HTML-only re-execute filter, the 6 status codes, the nav) and the O01–O04 work is a new Core seam (`IInstanceStatusService`) + a new Web page — both should carry an ADR per the repo's ADR convention ("a new capability that settles a design question gets an ADR"). |
| 7 | `dotnet build`, unit + integration tests green; translation parity green | **met with known-reds** | `dotnet build` green. `dotnet test`: `Kumunita.Core.Tests` **all green**; `Kumunita.Web.Tests` 3528 passed / **3 failed** (the known-reds — the two `DataClassCoverageTests` M2 drift reds + the `CodeCheck_Gate_Passes` ratchet gate, all in `improve-sec-known-reds` + `web-suite-known-reds-a10` — not regressions, not fixed in this unit per the X02 prompt). Translation parity (C02 4-language) green. |
| 8 | Handoff note records before → after numbers, honestly | **met** | This section. Every journey row, every DoD line, and every residual is recorded with the honest result + the unit (or a new finding F1–F7) that explains it. The lane's own exit condition — "an honest held close" — is the register. |

### New findings (filed, not fixed — X02 is verification-only)

- **F1 (blocked journey J9):** `AdminInvitationsController` (A09, the
  `/admin/invitations` admin surface) 500s — the view is at
  `Views/Admin/Invitations/Index.cshtml` but the conventional view-location
  lookup (the controller name `AdminInvitations`) looks for
  `Views/AdminInvitations/Index.cshtml`. **Fix:** move the view to
  `Views/AdminInvitations/Index.cshtml` (or rename the controller to
  `InvitationsController` under `Views/Admin/` — the former is the smaller
  change). This is the same class of bug as the `ChangePassword` →
  `/account/password` fix in `conventional-routing-name-mismatch` — the
  house pattern is to add a `[Route]` pin or move the view. The A09 shipped
  unit is **blocked by this** until the view is relocated.
- **F2 (uncertain journey J7):** the R20 guest-notice end-date +
  message-the-admins link is **not visible** on the rendered guest home
  page. Either R20 did not land (no `## R20` section in this note; no
  R20 commit in the recent git log), or the notice strip only renders
  under specific conditions (a resident with a time-limited guest access,
  per ADR 0151) and the seed data has no such resident. **Action for the
  successor:** either verify R20 landed (check the `GuestAccess` model +
  the notice-strip partial) or re-run the unit with a seeded guest-access
  resident.
- **F3 (uncertain journey J10):** the A14 "Request more space" button is
  **not visible** on the rendered `/account/storage` page (the seed data
  has the quota unlimited — the button may be conditionally hidden). Either
  A14 did not land, or the button only renders when the quota is set. **Action
  for the successor:** check the `Account/Storage.cshtml` view for the
  conditional render + set a quota on a seed resident to verify.
- **F4 (DoD line 2 residual — jargon):** the 2 residual jargon strings
  (`account.invite_pending_approval` + `account.invite_off`, both R21
  A11/A12 invitation-feature strings that reference `GlobalAdmin` — a
  **legitimate** term in an admin-facing string) have **no residual record**
  in the handoff (no `## R21` section). R28's handoff cites the "R21 jargon
  'each survivor justified' precedent" — the precedent is real, but the
  section it points to is missing. **Action for the successor:** append a
  `## R21` section recording the 2 residuals (or re-base the baseline with
  a `generated_from` note, the R33 precedent).
- **F5 (DoD line 2 residual — notify_without_link):** the 4 residual
  no-link kinds (`TodoAssign`, `ReportFiled`, `ReportAssigned`, + the
  R08/R09 `IssueFiled`/`AccountSignup` anonymous paths) have **no residual
  record** in the handoff (no `## R09` section; no `## R08` section). **Action
  for the successor:** append a `## R09` (or `## R08`) section recording the
  4 residuals (the R09 commit `9f7e670c` added `linkPath` to `TodoAssign` +
  `GroupAdded`, driving the count from 7 → 4 — the remaining 4 are the
  anonymous-filer + the reporter-to-author kinds that by design carry no
  link).
- **F6 (pre-existing, not fixed):** `docs-check` has 6 gate failures, all in
  `docs/archive/lanes/improve-sec/*-prompt.md` (tier: `living|reference` on
  an archived doc — a tier-model violation per the X02 prompt's "note
  pre-existing failures, don't fix"). **Not fixed in this unit.**
- **F7 (DoD line 6 — 2 missing ADRs):** D2 (the status page, R01/R02) and
  D8 (the operator's instance status, O01–O04) have **no ADR** in
  `docs/adr/`. Per the repo's ADR convention ("a new capability that
  settles a design question gets an ADR under `docs/adr/`") and the DoD
  line's explicit list (D2, D3, D4, D6, D7, D8), **two ADRs need to be
  written** (the next free numbers are 0305 + 0306) — or the DoD line needs
  to be amended to acknowledge that these two units shipped as features
  without a dedicated ADR (the plan's D2 card says "ADR (if non-trivial)" —
  the status-page work is non-trivial, so an ADR is expected).

### Summary (one paragraph)

The IMPROVE-UX lane closes with **8 of 12 journeys passing on a running
instance** (5 fully walked — J3 the 404 status page, J5 the self-serve
deletion with grace period, J6 the shared kiosk link, J11 the operator's
instance status, J12 the SMTP alarm infra — + 3 at the surface with the
infra verified by unit tests — J1 the report loop, J4 the to-do
assignment, J8 the moderation loop), **1 blocked** (J9 the
admin-invitations journey, by the A09 view-location bug F1), **2 uncertain**
(J7 the guest end-date + J10 the request-more-space button — both expected
by the lane but not visible on the rendered pages; F2/F3), and **4 of 8 DoD
lines fully met** (DoD 3 the status page, DoD 4 the SEC reviews, DoD 7 the
build/tests with known-reds, DoD 8 this note) and **2 partially met** (DoD 2
the gate green but the 4 "reach 0" residuals non-zero — 4 of the 6 residuals
honestly recorded in the handoff, 2 missing [F4/F5]; DoD 6 4 of the 6 ADRs
present — D2 + D8 missing [F7]). **The lane's own exit condition — "an
honest held close" — is the honest state here.** The 7 new findings (F1–F7)
are the successor's scope: F1 (the A09 view-location fix, a small
`feat(ux):` commit) unblocks J9; F4 + F5 (the R21 + R09/R08 residual
records, a doc-only append) satisfy the DoD line 2 residual-recording
convention; F7 (the 2 missing ADRs, a `docs(adr):` commit) satisfies the
DoD line 6. The handoff note is the cold reader's entry point — every
journey, every DoD line, and every residual is recorded with the honest
result + the unit (or finding) that explains it.

