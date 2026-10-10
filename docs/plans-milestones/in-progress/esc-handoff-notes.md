# ESC — Escalation authorization + receiving inbox — rolling handoff note

> **Lane open (U00).** This is the **scratch tier** (rolling handoff note)
> of the ESC lane's three-tier contract. One `##` section per unit, appended
> (never rewritten), in order (U00, U01, … U09). Each unit writes exactly one
> short section before it exits; the next unit reads **only** that section +
> its own entry-reads list.
>
> - **Register** — `docs/plans-milestones/plan-esc-escalation-authorization.md`
> - **Design doc (primary)** — `docs/design/esc-escalation-authorization-design.md`
>   (U01/U02 author)
> - **ADR** — **ADR 0159** (the next free number after 0158 — the ADR index in
>   `docs/adr/README.md` confirms 0158 is the current highest: 0158 =
>   surface-label-translation `Accepted — **Done** (LBL-2)`, 0157 = SITE-2,
>   0156 = M33, 0155 = M32, 0154 = M31. **0159 is free**; U00 verified this at
>   kickoff.)
> - **Scope** — a **capability on the M31 / M32 `ErrorReports` surface**
>   (ESC·1): ESC **reuses** the M32 `ErrorReport` doc + the
>   `Kumunita.Core.ErrorReports` context + the `IErrorReportService` seam and
>   **extends them additively** — the **two new docs** (`EscalationToken`
>   the ESC·3 9-member ceiling + `EscalationOutboundConfig` the ESC·4 6-
>   member ceiling, on a **new** `EscalationDocTypes` registration surface) +
>   the **`IEscalationTokenService`** seam (the ESC·3 / ESC·9 `ListAsync` /
>   `GenerateAsync` / `RevokeAsync` / `ValidateAsync` read + write lanes) +
>   the **four additive `ErrorReport` fields** (`FromInstance?` /
>   `EscalationReceivedAt?` / `EscalationSourceId?` / `EscalationTokenId?`,
>   the **19-member ESC ceiling**, the M32 15 unchanged; ESC·2) + the
>   **additive `Origin` value** `"escalated"` (the closed set extends to
>   `{"error-page","general","escalated"}`) + the **`AcceptInboundAsync`**
>   seam on `IErrorReportService` (the ESC·5 / ESC·7 idempotent inbound write
>   lane; the service surface is **5 methods** in ESC: the M31 3 + the M32 1
>   + the ESC 1) + the **`AccessVia.Escalation`** additive value (ESC·10) +
>   the **`/admin/escalation`** outbound-config admin surface (the ESC·4
>   endpoint + token + enabled) + the **`/admin/escalation/tokens`**
>   receiving-token admin surface (the ESC·3 generate / list / revoke) + the
>   **`POST /escalations/inbound`** public token-gated endpoint (the ESC·5 /
>   ESC·6 / ESC·7 acceptance) + the **`EscalationForwarder`** `Bearer`-token +
>   DB-config + env-var-fallback change (ESC·4 / ESC·6) + the **received
>   filter + `escalated` chip + `FromInstance`/`EscalationReceivedAt` detail
>   fields** (the ESC·8 pin) + the **closed `escalation.*` `kw-l` key set** ×
>   en/de/fr/da (~24 keys, the ESC·11 pin) + the test pins. **No new bounded
>   context** (ESC·1).
> - **Out of scope (named deferrals)** — an **inbound rate limiter** (tokens
>   are 256-bit CSPRNG — brute-force is infeasible; a per-token / per-IP
>   throttle may be added later as hardening) · an **outbound retry queue**
>   (a failed forward is a no-op, the M32·7 precedent — no retry queue) · an
>   **escalation reply lane** (the receiving admin's resolution of a received
>   issue flowing *back* to the origin — M32 already ships a local resolve; a
>   future lane may pipe the resolution back over the token) · a **per-token
>   per-origin-report allowlist** · and the `Milestones.cs` / README /
>   `MilestonesTests` trio until the lane *ships* (U09 owns the six-member
>   close flip).
> - **Frozen base (reused, unchanged)** — the M32 `ErrorReport` doc
>   (`src/Kumunita.Core/ErrorReports/ErrorReport.cs`, the **15-member M32
>   ceiling** ESC **extends additively** to the 19-member ESC ceiling — the
>   M32 15 keep their exact names, types, and nullability; ESC·1 / ESC·2) ·
>   the M31/M32 `IErrorReportService` **4-method surface**
>   (`CreateAsync` / `MarkTriagedAsync` / `ListAsync` / `MarkResolvedAsync`,
>   ESC·1 — ESC **adds** `AcceptInboundAsync`, it does not re-shape the four)
>   · the M31/M32 `errorreport.*` / `issue.*` `kw-l` key set (ESC·11 — ESC
>   **adds** the new `escalation.*` namespace, it does not re-author the
>   M31/M32 sets) · the **M32 `EscalationForwarder`**
>   (`src/Kumunita.Web/Services/EscalationForwarder.cs`, the `ForwardAsync`
>   **signature is unchanged** — ESC·6 — the impl is **extended** for the
>   `Bearer` token + the DB-config read + the env-var fallback) · the
>   **`KUMUNITA_ESCALATION_ENDPOINT`** env var (the ESC·4 fallback — the
>   unauthenticated operator-override channel, **preserved** when the DB
>   config is absent/disabled; ESC·4) · and the **`AccessVia`** enum
>   (`src/Kumunita.Core/Authorization/Decision.cs`, the **12 frozen values**
>   ESC **appends to** — ESC·10, the 12 untouched).
> - **New invariants (locked in ADR 0159, U00):** ESC·1 a capability on the
>   M31/M32 surface, not a new context · ESC·2 the `ErrorReport` doc field set
>   is extended additively to the **19-member ESC ceiling** (the M32 15
>   unchanged + the ESC 4: `FromInstance?` / `EscalationReceivedAt?` /
>   `EscalationSourceId?` / `EscalationTokenId?`; `Origin` gains
>   `"escalated"`) · ESC·3 the receiving-side tokens are the `EscalationToken`
>   doc (multiple, labeled, individually revocable, **SHA-256-hashed at rest**,
>   the plaintext shown once + never persisted) · ESC·4 the origin-side
>   outbound config is the `EscalationOutboundConfig` doc (a singleton; the
>   env var remains the unauthenticated fallback) · ESC·5 the inbound
>   acceptance endpoint is **public + token-gated** (valid → an `Origin =
>   "escalated"` row + one `AccessAudit` row; invalid/revoked → 401, no row)
>   · ESC·6 the token is presented + verified as `Bearer <token>` in both
>   directions; **Core stays HTTP-free** · ESC·7 a successful inbound is
>   **idempotent** on `(EscalationTokenId, EscalationSourceId)` · ESC·8
>   received issues surface in the **same** `/admin/error-reports` list (a new
>   `escalated` chip + a local/received filter + the `FromInstance` on the
>   detail) · ESC·9 token lifecycle writes are **audited**; revocation is
>   **immediate** · ESC·10 `AccessVia` gains exactly one additive value:
>   `Escalation` (the 12 frozen values untouched) · ESC·11 the closed
>   `escalation.*` `kw-l` key set is **parity-pinned in four languages**
>   (en/de/fr/da) · ESC·12 the admin token/config surface is
>   **GlobalAdmin-gated**; no new authz surface · ESC·13 the **six-member
>   close flip** is U09's responsibility.

<!-- U00 appends its section below this line. One ## section per unit, in order
     (U00, U01, … U09). Never rewrite a prior section. -->

## U00 — Kickoff verified

All 11 entry reads completed; **no drift** against the register. Facts the
next unit (U01 — design doc Part 1) needs:

- **The M32 `ErrorReport` doc shape (the 15-member ceiling ESC extends):**
  - `src/Kumunita.Core/ErrorReports/ErrorReport.cs` — namespace
    `Kumunita.Core.ErrorReports`, `sealed class ErrorReport`, **15 members**:
    `Id` (`string`) · `SubjectId` (`string`, `""` when anonymous) ·
    `Description` (`string`, required) · `ContactEmail` (`string?`) ·
    `RequestId` (`string`) · `ExceptionType` (`string?`) · `UserAgent`
    (`string?`) · `Created` (`DateTimeOffset`) · `TriageStatus` (`string`,
    default `"new"`) · `TriagedAt` (`DateTimeOffset?`) · `TriagedBy`
    (`string?`) · `Origin` (`string`, default `"error-page"`, closed set
    `{"error-page","general"}`) · `ResolvedAt` (`DateTimeOffset?`) ·
    `ResolvedBy` (`string?`) · `ResolutionNote` (`string?`). `Origin` is a
    **string** (the ESC·2 pin — a string field, so `"escalated"` rides ADR
    0004 §B.1, no migration). The M32 15 are the **frozen base** ESC extends
    additively to the 19-member ESC ceiling (the ESC 4: `FromInstance?` /
    `EscalationReceivedAt?` / `EscalationSourceId?` / `EscalationTokenId?`);
    **no M32 field is re-shaped** (ESC·1 / ESC·2).
- **The M31/M32 `IErrorReportService` 4-method surface ESC adds to:**
  - `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` — `public
    interface`, **4 methods**: `CreateAsync(ErrorReportDraft, ct)` ·
    `MarkTriagedAsync(string reportId, string actorId, ct)` · `ListAsync(int
    maxCount = 100, ct)` · `MarkResolvedAsync(string reportId, string actorId,
    string? resolutionNote, ct)`. ESC adds the 5th, `AcceptInboundAsync
    (InboundReport, ct)` (the ESC·5 / ESC·7 idempotent inbound write lane).
    M31/M32's four are **never re-shaped** (ESC·1 — unit-series rule 5).
- **The M32 `EscalationForwarder` + `IEscalationForwarder` (the ESC·6
  extension target):**
  - `src/Kumunita.Web/Services/EscalationForwarder.cs` — `sealed class
    EscalationForwarder : IEscalationForwarder`, **1 method**
    `ForwardAsync(string reportId, ct)`. Reads
    `configuration["KUMUNITA:ESCALATION_ENDPOINT"]` (M32·6, never persisted)
    → absent = a no-op (`Configured == false`). Loads the report via
    `errorReports.ListAsync(100)` + filters to `reportId`. POSTs the closed
    **8-field** payload `{ id, subjectId, description, contactEmail,
    requestId, exceptionType, origin, created }` via `IHttpClientFactory`
    (10 s timeout). `record EscalationResult(bool Configured, bool Success,
    int? StatusCode, string? Error)`. ESC **extends** the impl (the `Bearer`
    token + the DB-config read + the env-var fallback) — the
    `ForwardAsync` **signature is unchanged** (ESC·6) and the 8-field payload
    is **unchanged** (the M32·1 pin — additive-only).
- **The `AccessVia` enum (the 12 values ESC appends to):**
  - `src/Kumunita.Core/Authorization/Decision.cs` — `enum AccessVia`,
    **12 values**: `Owner` · `Audience` · `Delegation` · `Moderator` · `Report`
    · `BreakGlass` · `Admin` · `Group` · `Guardian` · `Community` · `Resident`
    · `Anonymous`. ESC appends the 13th, `Escalation` (the ESC·10 pin — the
    M1 `Admin` / ADR 0013 `Group` / ADR 0028 `Guardian` / ADR 0041 `Resident`
    / M31 `Anonymous` append precedent; the **12 frozen values are
    unchanged**).
- **The M31/M32 admin surface (5 actions — the ESC·8 mirror shape):**
  - `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` —
    `[Route("admin/error-reports")]` + `[Authorize(Roles = GlobalAdmin)]`,
    ctor `(IErrorReportService errorReports, ILocalizationService?
    localization = null, ITranslationProvider? translationProvider = null,
    IEscalationForwarder? escalationForwarder = null)` (the optional-ctor-
    param precedent ESC mirrors). **5 actions**: `Index()` (`GET` read-only
    listing) · `MarkTriaged(string id)` (`POST …/{id}/triage`, idempotent) ·
    `Detail(string id)` (`GET …/{id}`) · `Resolve(string id)` (`POST
    …/{id}/resolve`) · `Escalate(string id)` (`POST …/{id}/escalate`). ESC's
    received filter + chip + `FromInstance`/`EscalationReceivedAt` detail
    fields **extend** `Index` + `Detail` additively (ESC·8) — the existing
    five actions are **unchanged** (ESC·1 — additive-only).
- **The `escalation.*` `kw-l` namespace is FREE:**
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the M31/M32
    `errorreport.*` / `issue.*` sets are present (e.g. `errorreport.escalate.
    button` / `flash_success` / `flash_failure` / `not_configured` in all
    four languages). A repo-wide grep for `"escalation.` finds **no** `kw-l`
    key — the **`escalation.*` namespace is a new surface** (the ESC·11 pin;
    the M31/M32 sets are **reused, not re-authored** — ESC·1).
- **The `ErrorReportDocTypes` registration surface (ESC·1 — a new parallel
  surface):**
  - `src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs` — `static class
    ErrorReportDocTypes`, one `.Schema.For<ErrorReports.ErrorReport>()` + the
    (TriageStatus, Created) indexes. ESC adds a **new** `EscalationDocTypes`
    surface (the Usage `UsageDocTypes` parallel-surface precedent — the
    `EscalationToken` + `EscalationOutboundConfig` `.Schema.For` calls), it
    does **not** edit this one (ESC·1).
- **The ADR index (`docs/adr/README.md`) — 0159 is free:**
  - The current highest row is **0158** = surface-label-translation,
    `Accepted — **Done** (LBL-2)` (line 169). 0157 = SITE-2, 0156 = M33,
    0155 = M32 `Accepted — **Done** (M32)`, 0154 = M31. No 0159 / 016x row
    exists. **0159 is the next free number**; U01/U02 draft ADR 0159 (the ESC
    invariants ESC·1–ESC·13). Precedent ADRs the ESC surface rides: **ADR
    0004 §B.1** (idempotent additive schema delta + the parallel doc-type
    surface precedent) · **ADR 0001-B** (the thin-token rule — the admin
    surface is `[Authorize(Roles = GlobalAdmin)]`) · **ADR 0006-C3** (the
    single-write-lane — the token generate/revoke + the inbound acceptance
    each commit doc + one `AccessAudit` row together) · **ADR 0006-D** (Core
    stays HTTP-free — the forwarder + the inbound endpoint are Web-layer) ·
    **ADR 0154** (the M31 `ErrorReports` surface) · **ADR 0155** (the M32
    surface ESC reuses + evolves — the `ErrorReport` doc + the
    `IEscalationForwarder` + the `KUMUNITA_ESCALATION_ENDPOINT` env-var
    fallback).

## U01 — design doc Part 1

`docs/design/esc-escalation-authorization-design.md` Part 1 authored
(Context, Scope incl. named deferrals, Invariants, FACES). **No code, no
build.** The 13 invariants + 10 FACES are listed **by id** so U02 pins them
by id:

- **Invariants (13, by id):** ESC·1 (capability on the M31/M32 surface, not a
  new context) · ESC·2 (19-member ESC ceiling — the M32 15 unchanged + the ESC
  4 additive + `Origin` gains `"escalated"`) · ESC·3 (receiving-side
  `EscalationToken` doc — multiple, labeled, individually revocable, SHA-256-
  hashed at rest, plaintext shown once) · ESC·4 (origin-side
  `EscalationOutboundConfig` singleton + the env-var fallback) · ESC·5
  (inbound endpoint public + token-gated; invalid/revoked → 401, no row) ·
  ESC·6 (token as `Bearer` in both directions; Core stays HTTP-free) · ESC·7
  (inbound idempotent on `(token, origin-report-id)`) · ESC·8 (received issues
  in the same `/admin/error-reports` list + chip + filter + detail) · ESC·9
  (token lifecycle audited; revocation immediate) · ESC·10 (`AccessVia` gains
  exactly one additive value `Escalation`; the 12 frozen values untouched) ·
  ESC·11 (closed `escalation.*` `kw-l` key set parity-pinned in en/de/fr/da) ·
  ESC·12 (admin token/config surface GlobalAdmin-gated; no new authz surface)
  · ESC·13 (six-member close flip is U09's).
- **FACES (10, by id):** ESC-1 (generate token → plaintext once + hash row +
  one audit row) · ESC-2 (hand token out-of-band + origin sets endpoint/token
  → config row) · ESC-3 (valid escalation → `escalated` row + one audit row +
  origin `resolved`) · ESC-4 (invalid token → 401, no row, no local
  `resolved`) · ESC-5 (revoked token → 401, no row) · ESC-6 (no token/endpoint
  → forwarder no-op, no HTTP) · ESC-7 (revoke → immediate + one audit row) ·
  ESC-8 (`?filter=received` shows only `escalated` rows with `FromInstance`) ·
  ESC-9 (re-delivery same token+report → idempotent, no duplicate/second
  audit) · ESC-10 (non-GlobalAdmin → 403 on both admin surfaces).

## U02 — design doc Part 2 + ADR 0159

`docs/design/esc-escalation-authorization-design.md` Part 2 appended
(`## Seams & contracts (Part 2, written by U2)` — §2.1 frozen seam list,
§2.2 new ESC-owned Core types, §2.3 the closed `escalation.*` `kw-l` key set,
§2.4 the 22 pinned seam tests, §2.5 the three-test acceptance gate, §2.6 the
drift-guard) + **ADR 0159** drafted (`docs/adr/0159-escalation-authorization.md`,
`Status: Draft`, Context / Decision D1–D13 / Consequences) + the **0159 index
row** (`docs/adr/README.md`, `Draft`). **No code, no build, no test.** The
sealed seams + test names U03–U08 implement against:

- **Sealed seam signatures (the exact C# in design doc §2.1 / §2.2):** the
  `IEscalationTokenService` seam — `ListAsync()` / `GenerateAsync(label,
  actorId)` → `(string Plaintext, EscalationTokenSummary Stored)` /
  `RevokeAsync(tokenId, actorId)` → `bool` / `ValidateAsync(plaintextToken)`
  → `EscalationToken?` (the `EscalationTokenSummary` read-model: `Id` /
  `Label` / `TokenPrefix` / `Created` / `LastUsedAt?` / `RevokedAt?` /
  `RevokedBy?` — no plaintext, no hash) · the `IErrorReportService` **5th**
  method `AcceptInboundAsync(InboundReport draft)` → `InboundResult` (the
  `InboundReport` 9-member record + the `InboundResult` `Created`/`Row`/`Error?`
  record — the M31/M32 4-method surface **unchanged**, ESC·1) · the
  `EscalationForwarder` change — `ForwardAsync(reportId)` signature
  **unchanged** (ESC·6) + the `EscalationResult` additive member
  `string? ConfiguredSource` (`"db"` / `"env"` / `null`, the M32 4 members
  unchanged) · the `EscalationToken` 9-member doc + the
  `EscalationOutboundConfig` 6-member doc + the four additive `ErrorReport`
  fields (`FromInstance?` / `EscalationReceivedAt?` / `EscalationSourceId?` /
  `EscalationTokenId?`, the **19-member ESC ceiling**, ESC·2) + the
  `EscalationDocTypes` registration (the two new docs on a **new** parallel
  surface; the M32 `ErrorReportDocTypes` untouched) + the
  `AccessVia.Escalation` additive value (the 12 frozen values untouched,
  ESC·10).
- **The 22 pinned test names (design doc §2.4, verbatim — U08 implements
  exactly these):** 1 `ESC_3_Token_Generate_Stores_Hash_Not_Plaintext` · 2
  `ESC_3_Token_Validate_Valid_Returns_Token` · 3
  `ESC_3_Token_Validate_Invalid_Returns_Null` · 4
  `ESC_9_Token_Revoke_Immediate_NoValidate` · 5
  `ESC_9_Token_List_Excludes_Plaintext_And_Hash` · 6
  `ESC_5_AcceptInbound_ValidToken_Creates_Escalated_Origin` · 7
  `ESC_5_AcceptInbound_InvalidToken_Returns_Error_NoRow` · 8
  `ESC_7_AcceptInbound_ReDelivery_Idempotent_NoDuplicate` · 9
  `ESC_2_ErrorReport_Doc_FieldSet_ESC_Ceiling` · 10
  `ESC_10_AcceptInbound_AuditRow_Via_Escalation` · 11
  `ESC_1_Token_Page_GlobalAdmin_Sees_TokenList` · 12
  `ESC_1_Token_Generate_Plaintext_VisibleOnce` · 13
  `ESC_7_Token_Revoke_Succeeds` · 14 `ESC_10_Token_Page_NonGlobalAdmin_Denied`
  · 15 `ESC_2_Outbound_Config_Save_Persists_Endpoint_Token_Enabled` · 16
  `ESC_5_Inbound_Endpoint_ValidToken_Creates_Row` · 17
  `ESC_4_Inbound_Endpoint_InvalidToken_401_NoRow` · 18
  `ESC_6_Forwarder_DB_Config_Sends_Bearer_Token` · 19
  `ESC_4_Forwarder_NoDB_EnvVar_Fallback` · 20
  `ESC_6_Forwarder_NoConfig_NoHTTP` · 21 `ESC_8_Received_Filter_Sees_Escalated_Only`
  · 22 `ESC_8_Detail_Sees_FromInstance`.
- **The three-test acceptance gate (design doc §2.5, by name):** **closed
  loop** (generate token → origin sets endpoint+token → origin escalates →
  receiving inbound creates an `Origin="escalated"` row + the `Via=Escalation`
  audit row → origin `resolved`) · **handoff** (receiving admin revokes the
  token → a subsequent inbound with that token → **401**, no row) · **part-vs-
  whole** (the 22-test list is the whole; closed-loop + handoff are the parts;
  all must pass together).
- **ADR 0159** (the next free number after 0158) is **`Draft`** + the index
  row is present. The **M32 surface is reused** (ESC·1 — the `ErrorReport`
  doc + `IErrorReportService` + `EscalationForwarder` + `ErrorReports` context
  are extended additively, **not** re-shaped; no new bounded context) and the
  **`KUMUNITA_ESCALATION_ENDPOINT` env var is the unauthenticated
  fallback** (ESC·4 — preserved when the DB config is absent/disabled; a
  deliberate evolution of the M32·6 "env var, never persisted" pin). The
  `escalation.*` `kw-l` namespace is the **new** ESC·11 surface (the M31/M32
  `errorreport.*` / `issue.*` floor untouched). **Drift note:** the register's
  Assumptions name "~24 keys" but the U05/U06/U07 deliverables consume **25**
  (the `escalation.inbound.received` confirmation is the 25th); design doc
  §2.3 resolves the set to the exact 25-key list (additive — it never re-
  shapes the M31/M32 floor). U03–U07 implement against the pinned C# in §2.1 /
  §2.2 without re-deriving a decision.

## U03 — Core (tokens + config docs + service)

`dotnet build Kumunita.slnx -c Debug` **green (0 warning, 0 error)**. No new
tests (U08's), no Web surface / inbound endpoint yet (U04/U06), no kw-l keys
(U07). Deliverables landed:

- **Docs (2, both `Kumunita.Core.ErrorReports`):** `EscalationToken` — the
  **9-member** ESC·3 doc (`Id`/`Label`/`TokenHash`/`TokenPrefix`/`Created`/
  `CreatedBy`/`LastUsedAt?`/`RevokedAt?`/`RevokedBy?`; **hash-only-at-rest**,
  plaintext never a member) · `EscalationOutboundConfig` — the **6-member**
  ESC·4 singleton (`Id`/`Endpoint`/`Token`/`Enabled`/`Updated`/`UpdatedBy`;
  the `Token` is the plaintext *outgoing* secret).
- **Seam + impl:** `IEscalationTokenService` + the `EscalationTokenSummary`
  read-model record (`Id`/`Label`/`TokenPrefix`/`Created`/`LastUsedAt?`/
  `RevokedAt?`/`RevokedBy?` — no plaintext, no hash) · `EscalationTokenService`
  impl — four lanes `ListAsync` / `GenerateAsync(label, actorId)` /
  `RevokeAsync(tokenId, actorId)` / `ValidateAsync(plaintextToken)`. Generate
  + Revoke are single-write-lane (doc + one `AccessAudit` row, `Via = Admin`,
  `TargetKind` "escalation-token"); Validate is a constant-time SHA-256 hash
  compare that stamps `LastUsedAt` (no audit row); List projects to the
  summary (no plaintext/hash). Token = `kesc_` + 32 CSPRNG bytes base64url
  (256 bits, the `IdentityService.NewSecret` CSPRNG precedent).
- **Registration surface:** `EscalationDocTypes` (`Kumunita.Core`) — the two
  new docs on a **new** parallel surface; the M32 `ErrorReportDocTypes`
  surface is **untouched** (ESC·1). **Wire point deviation (recorded):** the
  `EscalationDocTypes.Configure(opts)` call is wired in
  `src/Kumunita.Web/Program.cs` (next to the `ErrorReportDocTypes.Configure(opts)`
  line at ~264) — **not** `DependencyInjection.cs` as the register's U03
  Deliverables bullet named; that bullet conflated the two `Configure` sites
  (the register's own U00 handoff note + the design doc §2.2.6 both name the
  `ErrorReportDocTypes` wire point, and the only site that calls it is
  `Program.cs`). The DI line for the **seam** (`IEscalationTokenService`) is
  in `DependencyInjection.cs` as pinned.
- **`AccessVia.Escalation`** appended as the **13th** value in
  `src/Kumunita.Core/Authorization/Decision.cs` (ESC·10 — the 12 frozen values
  unchanged; doc-comment names the ESC lane + the ADR 0159 standing).
- **DI line:** `services.AddTransient<ErrorReports.IEscalationTokenService>(sp
  => new ErrorReports.EscalationTokenService(sp.GetRequiredService<Marten.
  IDocumentStore>()))` in `src/Kumunita.Core/DependencyInjection.cs` (the
  `IErrorReportService` registration shape, the type/namespace-collision
  idiom).
- **Compile warnings:** none (the build log's `0 Warning(s)` / `0 Error(s)`).
  **Drift note (non-blocking):** the design doc §2.2.6 wrote the
  multi-column index as the tuple form `.Index((t) => (t.RevokedAt, t.Created))`;
  the repo's Marten 9.31.2 / Weasel 9.29.0 does not accept that form (the
  M5DocTypes U02 NOTE documents the same CS1501 hit + the workaround). U03
  used the anonymous-object form `.Index(t => new { t.RevokedAt, t.Created })`
  (the M5/M6/M17 precedent — the M17 Bookmark `(OwnerId, Created)` / M6
  Notification `(RecipientId, Created)` feed-ordering-index shape). The
  semantic intent (index the `(RevokedAt, Created)` pair for the token-list
  ordering) is unchanged; the auto-derived name stays under Postgres'
  64-char NAMEDATALEN cap (the M5 NOTE's idiom). U04–U06 should continue to
  mirror this idiom if any future ESC surface needs a multi-column index.
