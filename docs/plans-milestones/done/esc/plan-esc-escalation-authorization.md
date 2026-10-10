# ESC — Escalation authorization + receiving inbox — lane register

> **In progress.** This is the **lane register** (secondary tier of the lane's
> three-tier contract) for the **ESC** lane. The capability, in the user's own
> words:
>
> > "Issues escalation should have a page where admins can view locally
> > submitted issues, close them or escalate them (if the instance is
> > configured for escalation) and a page to generate or use a token / access
> > code for client platforms to use for escalation. So only an authorized
> > platform can escalate to another platform. The admin of the receiving
> > platform for escalation tells the admin of the origin platform the access
> > code / token so they can add it to their platform setup, and when an admin
> > escalates an issue it is only accepted by the receiving platform if it has
> > a valid token."
>
> ESC is a **named lane** (it takes the `ESC` id — **not** a roadmap renumber;
> the M33 / M34 order is untouched, the ADR 013 / 089 / 093 / 109 "named lane,
> not a renumber" precedent). It is a **capability on the M31 / M32
> `ErrorReports` surface** (ADR 0154 + ADR 0155): it **reuses** the M32
> `ErrorReport` doc + `IErrorReportService` + `/admin/error-reports` list +
> detail + resolve actions and **extends them additively**. It adds **two new
> docs to the `ErrorReports` context** (`EscalationToken`,
> `EscalationOutboundConfig`), **one new service** (`IEscalationTokenService`),
> **one new additive `IErrorReportService` seam** (`AcceptInboundAsync`),
> **one new public token-gated endpoint** (`POST /escalations/inbound`),
> **two new admin surfaces** (`/admin/escalation` outbound config +
> `/admin/escalation/tokens` receiving tokens), **one new outbound-HTTP change**
> (the M32 `EscalationForwarder` now sends a `Bearer` token + reads the DB
> config), and **one additive `AccessVia` value** (`Escalation`). It adds
> **no new bounded context** (ESC·1 — both new docs ride the M31 / M32
> `ErrorReports` context + a new `EscalationDocTypes` registration surface).
>
> **What already ships** (M31 + M32, ADR 0154 / 0155) that ESC builds on:
> the `/admin/error-reports` list + the `/{id}` detail + `POST …/resolve` +
> `POST …/escalate`. The M32 `EscalationForwarder` already POSTs an 8-field
> payload to the env-var `KUMUNITA_ESCALATION_ENDPOINT`. What ESC **adds**:
> (1) the escalation becomes **token-authenticated** — only a platform holding
> a valid token the receiving admin issued can be accepted; (2) a **receiving
> inbox** — received issues land as `Origin = "escalated"` rows in the *same*
> admin list, distinguished + filterable; (3) a **token page** — the receiving
> admin generates labeled, individually-revocable tokens (shown once, hashed
> at rest) and hands them out-of-band to the origin admin; (4) an **outbound
> config page** — the origin admin sets their endpoint + token.
>
> **U00** verifies the surface and authors the handoff-note skeleton.
> **U01 / U02** author the primary-tier design doc (invariants ESC·1–ESC·13 +
> FACES ESC-1–ESC-10 + the two new doc shapes + the `IEscalationTokenService`
> seam + the `AcceptInboundAsync` seam + the forwarder token change + the
> closed `escalation.*` `kw-l` key set + the pinned test names + the
> acceptance gate + the drift guard) and draft **ADR 0159**. **U03 / U04**
> implement the Core (the tokens + config docs + service; then the additive
> `ErrorReport` fields + `AcceptInboundAsync` + the `AccessVia.Escalation`
> value). **U05 / U06** ship the Web (the token + config admin pages; then the
> inbound endpoint + forwarder token change + the received filter + chip).
> **U07** authors the closed `escalation.*` `kw-l` key set × en/de/fr/da.
> **U08** runs + records the acceptance gate. **U09** flips the close (the
> six-member close flip).

## Understanding (one paragraph)

M32 made escalation possible: an admin on an origin instance can forward a
report to a configurable endpoint. But that forward is **unauthenticated and
blind** — the receiving platform has no surface to see the report, no way to
say *which* platform it came from, and no way to stop an untrusted instance
from dumping arbitrary reports into its queue. ESC closes that: the receiving
admin generates **labeled tokens** (one per authorized origin platform, each
individually revocable, hashed at rest, shown once) and hands the plaintext to
the origin admin out-of-band; the origin admin records their endpoint + token
on the **outbound config page**; when they escalate, the forwarder presents
the token as a `Bearer` header; the receiving platform's **inbound endpoint**
validates the token (SHA-256 compare) and, only on a match, creates an
`Origin = "escalated"` `ErrorReport` row (idempotent on token + origin-report-id)
+ one `AccessAudit` row (`Via = Escalation`). The receiving admin then sees
received issues in the **same** `/admin/error-reports` list (a new
`escalated` chip + a local/received filter + the origin's `FromInstance` on
the detail view). The boundary is explicit: ESC **reuses** the M31 / M32
`ErrorReport` doc + context + service and extends them additively; the
`KUMUNITA_ESCALATION_ENDPOINT` env var remains as an **unauthenticated
operator-override fallback** (the admin-managed DB config is primary, ESC·4).

## The one thing every unit must respect

**Escalation-authorization semantics (locked in ADR 0159, U02):**

- **ESC is a capability on the M31 / M32 surface, not a new context (ESC·1).**
  The two new docs (`EscalationToken`, `EscalationOutboundConfig`) live in the
  `Kumunita.Core.ErrorReports` context (the ADR 0154 / 0155 surface), registered
  on a **new** `EscalationDocTypes` surface (the M31 `ErrorReportDocTypes` /
  the Usage `UsageDocTypes` parallel-surface precedent). ESC **reuses** the
  M32 `ErrorReport` doc + `IErrorReportService`; it adds **no new bounded
  context**.
- **The `ErrorReport` doc field set is extended additively to a 19-member ESC
  ceiling (ESC·2).** M32's 15 (ADR 0155 D1) are unchanged; ESC adds **exactly
  four**: `FromInstance` (string?, null for locally-filed — the accepting
  token's `Label`), `EscalationReceivedAt` (DateTimeOffset?, the receive
  instant; null for locally-filed), `EscalationSourceId` (string?, the
  origin's report id — the inbound idempotency key; null for locally-filed),
  `EscalationTokenId` (string?, which token accepted it; null for
  locally-filed). The `Origin` closed set is extended additively from
  `{"error-page","general"}` to `{"error-page","general","escalated"}`
  (a string field — ADR 0004 §B.1 idempotent delta, **no migration**). ADR 0159
  re-pins the **19-member ESC ceiling**.
- **Receiving-side tokens are the `EscalationToken` doc: multiple, labeled,
  individually revocable, hashed at rest (ESC·3).** Only the **SHA-256 hash**
  is stored — the **plaintext is shown exactly once** at generation, never
  persisted. Tokens are **CSPRNG-generated** (`RandomNumberGenerator`, the
  `IdentityService` / `PortabilityApplyIdentity` precedent), **32 random bytes
  base64url**, `kesc_`-prefixed (a `ghp_`-style high-entropy format — 256 bits,
  so brute-force validation is infeasible; the real protections are the high
  entropy + the immediate revocability). The closed member set (the ESC·3 pin,
  the design doc §2.2): `Id` · `Label` (string, the admin's name for the origin
  platform) · `TokenHash` (string, SHA-256 hex) · `TokenPrefix` (string, the
  first few chars for display, e.g. `kesc_ab12…`) · `Created` (DateTimeOffset)
  · `CreatedBy` (string, the admin's subject) · `LastUsedAt` (DateTimeOffset?)
  · `RevokedAt` (DateTimeOffset?) · `RevokedBy` (string?).
- **Origin-side outbound config is the `EscalationOutboundConfig` doc,
  admin-managed (ESC·4).** A singleton row (one per instance): `Endpoint`
  (string), `Token` (string, the plaintext the admin was handed — **stored**
  here because it is the *outgoing* secret the origin admin chose to keep, not
  a secret of the *receiving* platform), `Enabled` (bool), `Updated`
  (DateTimeOffset), `UpdatedBy` (string). The M32·6 env var
  `KUMUNITA_ESCALATION_ENDPOINT` remains as an **unauthenticated
  operator-override / fallback**: when the DB config is **absent/disabled**,
  the forwarder falls back to the env-var endpoint (endpoint only, **no** token
  → an unauthenticated M32-style forward, preserved for backward compat); when
  the DB config is **present**, it is primary and the token is sent. This is a
  **deliberate evolution** of the M32·6 "env var, never persisted" pin — that
  pin was the M32-era *operator-config* channel; ESC supersedes it for the
  *admin-managed, token-authenticated* channel (recorded in ADR 0159).
- **The inbound acceptance endpoint is public + token-gated (ESC·5).**
  `POST /escalations/inbound` is **not** inside a session `[Authorize]` gate
  (it is machine-to-machine; the token is the credential — the M31·2 / M32·4
  "public, anonymous-safe" precedent for resident-facing surfaces, applied to
  the M2M surface). It validates the presented token (SHA-256 compare vs the
  stored hash); on **valid** → creates an `Origin = "escalated"` `ErrorReport`
  row + **one** `AccessAudit` row (`Via = Escalation`, action
  `errorreport.inbound`, `TargetKind` "error-report"); on **invalid** → **401**,
  **no** row, **no** audit row. A **revoked** token is invalid (ESC·9).
- **The token is presented + verified as `Authorization: Bearer <token>` in
  both directions; Core stays HTTP-free (ESC·6).** The M32 `EscalationForwarder`
  (Web-layer) now sends the outbound config's token as a `Bearer` header on the
  forward. The inbound endpoint (Web-layer) reads it. The **validation** logic
  (hash compare) + the **received-row write** are Core (the
  `IEscalationTokenService.ValidateAsync` + `IErrorReportService.AcceptInboundAsync`
  seams); the **HTTP** (both the outbound POST and the inbound request parsing)
  is Web (M32·5 / ADR 0006-D — Core stays HTTP-free).
- **A successful inbound is idempotent on (token, origin-report-id) (ESC·7).**
  A re-delivery of the **same** origin report under the **same** token returns
  the **existing** row (200, idempotent), not a duplicate — the M31·6 / M32·8
  idempotency precedent. The key is `(EscalationTokenId, EscalationSourceId)`.
- **Received issues surface in the same admin list (ESC·8).** The M31·4
  `/admin/error-reports` list is **unchanged** in shape; ESC adds the
  `Origin = "escalated"` chip + a `?filter=received|local` selector (the
  default shows all). The detail view shows `FromInstance` +
  `EscalationReceivedAt` for received rows. **No** separate receiving-inbox
  surface (the user chose the same-list option).
- **Token lifecycle writes are audited; revocation is immediate (ESC·9).**
  `Generate` + `Revoke` each write exactly **one** `AccessAudit` row (`Via =
  Admin`, actions `escalation.token.generate` / `escalation.token.revoke`,
  `TargetKind` "escalation-token"). `Revoke` sets `RevokedAt` / `RevokedBy`;
  after revocation the token **no longer validates** (immediate). `LastUsedAt`
  is stamped on each successful inbound (a read-then-write on the token row,
  no audit row — the M31·4 "read is not an access decision" precedent).
- **`AccessVia` gains exactly one additive value: `Escalation` (ESC·10).** The
  M1 `Admin` / ADR 013 `Group` / ADR 0028 `Guardian` / ADR 0036 `Community` /
  ADR 0041 `Resident` / M31 `Anonymous` append precedent — the **12 frozen
  values are untouched**. The inbound audit row is the **only** row that
  carries `Via = Escalation` (the M2M standing — the receiving admin is not the
  actor, the origin platform is).
- **The closed `escalation.*` `kw-l` key set is parity-pinned in four
  languages (ESC·11).** Every new user-visible string is a
  `KnownTranslationKeys` entry present, **non-empty, in all four** languages
  (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests` (the M31·7 / M32·9 precedent). The `en`
  values are the source text (ADR 0015 D1 `kw-l` provider-floor discipline);
  the `de` / `fr` / `da` values are U07's to author (the M30·6 four-language
  pin).
- **The admin token/config surface is GlobalAdmin-gated; no new authz surface
  (ESC·12).** `/admin/escalation` + `/admin/escalation/tokens` are
  `[Authorize(Roles = GlobalAdmin)]` (the M31·4 / M31·9 / M32·10 precedent).
  **No new `AccessAction`, no new `Decide()` branch, no new
  `IAuthorizationService` surface** (ESC·12). The inbound endpoint is
  **public** (token-gated, ESC·5).
- **The six-member close flip is U09's responsibility (ESC·13).** The
  `Milestones.cs` **lane row** (added as `StatusDone`, **after** the M32 row,
  without disturbing the M33 / M34 order or the single-in-progress pin — M34
  stays `StatusNext`) / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` (a new entry, newest-first, naming ESC +
  ADR 0159) six-member close flip lands in U09. The M27 "shipped with no entry
  until caught in review" lesson (AGENTS.md) is held.

## Assumptions

- **Scope (in):** the **`EscalationToken` doc** (the ESC·3 receiving-side
  tokens — multiple, labeled, individually revocable, SHA-256-hashed at rest)
  + the **`EscalationOutboundConfig` doc** (the ESC·4 origin-side endpoint +
  token + enabled) + the **`IEscalationTokenService`** seam (the ESC·3 / ESC·9
  `ListAsync` / `GenerateAsync` / `RevokeAsync` / `ValidateAsync` read + write
  lanes) + the **`EscalationDocTypes`** registration surface + the **four
  additive `ErrorReport` fields** (`FromInstance?` / `EscalationReceivedAt?` /
  `EscalationSourceId?` / `EscalationTokenId?`, the ESC·2 19-member ceiling) +
  the **additive `Origin` value** (`escalated`, ESC·2) + the
  **`AcceptInboundAsync` seam** on `IErrorReportService` (the ESC·5 / ESC·7
  idempotent inbound write lane) + the **`AccessVia.Escalation`** additive
  value (ESC·10) + the **`/admin/escalation`** outbound-config admin surface
  (the ESC·4 endpoint + token + enabled) + the **`/admin/escalation/tokens`**
  receiving-token admin surface (the ESC·3 generate / list / revoke) + the
  **`POST /escalations/inbound`** public token-gated endpoint (the ESC·5 /
  ESC·6 / ESC·7 acceptance) + the **`EscalationForwarder`** change (the ESC·6
  `Bearer` token header + the ESC·4 DB-config read + env-var fallback) + the
  **received filter + `escalated` chip** on the existing `/admin/error-reports`
  list + the **`FromInstance` / `EscalationReceivedAt`** on the detail view
  (the ESC·8 pin) + the **closed `escalation.*` `kw-l` key set** × en/de/fr/da
  (the ESC·11 pin) + the test pins.
  **Out (named deferrals):** an **inbound rate limiter** (tokens are 256-bit
  CSPRNG — brute-force is infeasible; a per-token / per-IP throttle may be
  added later as hardening), an **outbound retry queue** (a failed forward is a
  no-op, the M32·7 precedent — no retry queue), an **escalation reply lane**
  (the receiving admin's resolution of a received issue flowing *back* to the
  origin — M32 already ships a local resolve; a future lane may pipe the
  resolution back over the token), a **per-token per-origin-report allowlist**,
  and the **`Milestones.cs` / README / `MilestonesTests`** trio until the lane
  *ships* (U09 owns the six-member close flip).
- **The `EscalationToken` doc member set (locked by the design doc §2.2 pin).**
  The doc carries exactly these **9** members (the **ESC·3 pin**): `Id`
  (string, conventional — Marten-generated) · `Label` (string, the admin's
  name for the origin platform) · `TokenHash` (string, the SHA-256 hex digest
  of the plaintext — **never** the plaintext) · `TokenPrefix` (string, the
  display prefix, e.g. `kesc_ab12…`) · `Created` (DateTimeOffset) ·
  `CreatedBy` (string, the admin's subject) · `LastUsedAt` (DateTimeOffset?)
  · `RevokedAt` (DateTimeOffset?) · `RevokedBy` (string?). The plaintext is
  **never** a member (ESC·3). The token format: `kesc_` + 32 CSPRNG bytes
  base64url (256 bits).
- **The `EscalationOutboundConfig` doc member set (locked by the design doc
  §2.2 pin).** The doc carries exactly these **6** members (the **ESC·4
  pin**): `Id` (string, conventional — a fixed singleton id, e.g.
  `"outbound-config"`) · `Endpoint` (string, the receiving platform's inbound
  URL) · `Token` (string, the plaintext token the origin admin was handed —
  the *outgoing* secret) · `Enabled` (bool, default `false`) · `Updated`
  (DateTimeOffset) · `UpdatedBy` (string, the admin's subject). One row per
  instance (a singleton).
- **The `IEscalationTokenService` seam (locked by the design doc §2.1 pin).**
  ```csharp
  // namespace Kumunita.Core.ErrorReports (a Core service seam, not a Web service)
  public interface IEscalationTokenService
  {
      // The receiving admin's token list (newest first). A read — no audit row
      // (the M31·4 "read is not an access decision" precedent). Never exposes
      // the plaintext (the TokenPrefix + Label + status only, ESC·3 / ESC·9).
      Task<IReadOnlyList<EscalationTokenSummary>> ListAsync(CancellationToken ct = default);

      // Generate a new labeled token. Returns the plaintext (shown ONCE) + the
      // stored row. Writes the EscalationToken row (hash) + exactly ONE
      // AccessAudit row (Via = Admin, action "escalation.token.generate",
      // TargetKind "escalation-token") in one write session (the ADR 0006 C3
      // single-write-lane shape, the M32·8 precedent).
      Task<(string Plaintext, EscalationTokenSummary Stored)> GenerateAsync(
          string label, string actorId, CancellationToken ct = default);

      // Revoke a token (set RevokedAt / RevokedBy). Idempotent (a no-op — no
      // audit row, no state change — when the token is missing or already
      // revoked, the M32·8 precedent). One AccessAudit row (Via = Admin, action
      // "escalation.token.revoke") on an effective revoke.
      Task<bool> RevokeAsync(string tokenId, string actorId, CancellationToken ct = default);

      // Validate a presented plaintext token. Returns the matching non-revoked
      // token (or null when absent / revoked). Stamps LastUsedAt on a match
      // (a read-then-write on the token row, NO audit row — the M31·4
      // precedent). The hash compare is constant-time (a DoS / timing floor).
      Task<EscalationToken?> ValidateAsync(string plaintextToken, CancellationToken ct = default);
  }
  ```
  `EscalationTokenSummary` is a read-model record (the design doc §2.2):
  `Id` · `Label` · `TokenPrefix` · `Created` · `LastUsedAt?` · `RevokedAt?`
  (the `RevokedBy` may be included). It is **never** the doc — the plaintext /
  hash are excluded (ESC·3).
- **The `AcceptInboundAsync` seam (locked by the design doc §2.1 pin).**
  ```csharp
  // Added to IErrorReportService (the M32 4-method surface gains a 5th)
  Task<InboundResult> AcceptInboundAsync(InboundReport draft, CancellationToken ct = default);
  // InboundReport (a Core record, NOT the ErrorReportDraft — the inbound
  // payload carries the origin's fields + the receiving platform's identity):
  public sealed record InboundReport(
      string TokenId,            // which token accepted it (ESC·7 idempotency key)
      string SourceReportId,     // the origin's report id (ESC·7 idempotency key)
      string FromInstance,       // the accepting token's Label (ESC·8 display)
      string Description,        // the origin's description (required)
      string? ContactEmail,      // the origin's contact email (optional)
      string OriginSubjectId,    // the origin's SubjectId ("" for anonymous)
      string RequestId,          // the origin's request id
      string? ExceptionType,     // the origin's exception type (null for a general issue)
      DateTimeOffset OriginCreated); // the origin's Created
  public sealed record InboundResult(bool Created, ErrorReport Row, string? Error);
  // Created == true → a new Origin="escalated" row was written (+ one
  // AccessAudit row, Via = Escalation, action "errorreport.inbound").
  // Created == false (idempotent, ESC·7) → the existing row is returned, no
  // audit row. Error is non-null only on a validation failure (blank
  // description) — no row, no audit row.
  ```
  The impl: `ValidateAsync` the token (via `IEscalationTokenService`) → if
  null → `Error = "invalid token"`, `Created = false` (the Web endpoint maps
  this to a 401); else look up an existing row on `(EscalationTokenId =
  TokenId, EscalationSourceId = SourceReportId)` → if found → return it
  (`Created = false`, the ESC·7 idempotent path); else store a new
  `ErrorReport` (`Origin = "escalated"`, `FromInstance`, `EscalationReceivedAt
  = now`, `EscalationSourceId`, `EscalationTokenId`, `SubjectId =
  OriginSubjectId`, …) + **one** `AccessAudit` row (`Via = Escalation`, action
  `errorreport.inbound`) in **one** write session (the ADR 0006 C3
  single-write-lane shape) + stamp the token's `LastUsedAt`. The
  `IErrorReportService` surface is **5 methods** in ESC (the M31 3 + the M32 1 +
  the ESC 1).
- **The `EscalationForwarder` change (locked by the design doc §2.1 pin).** The
  M32 `ForwardAsync(reportId)` **signature is unchanged** (the M32·5 pin — no
  new parameter; the forwarder reads its own config). The impl change: (1) read
  the **DB** `EscalationOutboundConfig` first (via a new `IOutboundConfigReader`
  or directly through `IEscalationTokenService`'s config read — the design doc
  §2.1 pins the exact read seam); if **present + enabled** → use its
  `Endpoint` + send `Authorization: Bearer <Token>`; (2) if the DB config is
  **absent / disabled** → fall back to the M32·6 env var
  `configuration["KUMUNITA:ESCALATION_ENDPOINT"]` (endpoint only, **no** token
  — the unauthenticated M32-style forward, backward compat); (3) if **neither**
  is present → the M32 no-op (`Configured == false`, FACES ESC-6). The payload
  (the M32 8-field shape) is **unchanged** (the M32·1 pin — additive-only). The
  `EscalationResult` gains one additive member: `string? ConfiguredSource`
  (`"db"` / `"env"` / `null`) so the flash can name the source (the M32·3
  additive precedent).
- **The closed `escalation.*` `kw-l` key set (~24 keys).** Authored by U07,
  consumed by U05's pages + U06's list / detail + inbound flashes. The closed
  set (the design doc §2.3 table): `escalation.title` / `escalation.intro` /
  `escalation.outbound.endpoint.label` / `escalation.outbound.endpoint.placeholder`
  / `escalation.outbound.token.label` / `escalation.outbound.token.placeholder`
  / `escalation.outbound.enabled.label` / `escalation.outbound.save` /
  `escalation.outbound.saved` / `escalation.tokens.title` /
  `escalation.tokens.generate.button` / `escalation.tokens.label.label` /
  `escalation.tokens.show_once` / `escalation.tokens.copy` /
  `escalation.tokens.revoke.button` / `escalation.tokens.revoked` /
  `escalation.tokens.none` / `escalation.tokens.last_used` /
  `escalation.list.status.escalated` / `escalation.list.filter.all` /
  `escalation.list.filter.received` / `escalation.list.filter.local` /
  `escalation.detail.from_instance` / `escalation.inbound.invalid_token` /
  `escalation.inbound.received`
  Every key is present, non-empty, in **all four** languages (en/de/fr/da); the
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` closure
  pins it (the ESC·11 pin).
- **The test model (pinned in the design doc §2.4).** The `Core.Tests` pins (the
  `EscalationTokenTests` + `EscalationInboundTests` classes, in
  `tests/Kumunita.Core.Tests/`): (a) `GenerateAsync` stores a row whose
  `TokenHash` is the SHA-256 of the returned plaintext (and the plaintext is
  **not** a doc field — ESC·3), (b) `ValidateAsync` with the correct plaintext
  returns the token, with a wrong plaintext returns `null` (ESC·3), (c)
  `RevokeAsync` → the token no longer validates (ESC·9), (d) `ListAsync`
  exposes no plaintext / hash (only the `TokenPrefix` — ESC·3 / ESC·9), (e)
  `AcceptInboundAsync` with a valid token creates an `Origin = "escalated"` row
  + one `AccessAudit` row (`Via = Escalation`) (ESC·5), (f) `AcceptInboundAsync`
  with an **invalid** token returns `Created == false`, no row, no audit row
  (ESC·5), (g) `AcceptInboundAsync` re-delivery of the same
  `(TokenId, SourceReportId)` is **idempotent** (the existing row, no second
  audit row — ESC·7), (h) the `ErrorReport` doc field set is the **19-member
  ESC ceiling** (the ESC·2 drift pin — the M32 15 unchanged + the ESC 4
  additive), (i) `AccessVia.Escalation` is present + the 12 frozen values are
  unchanged (ESC·10). The `Web.Tests` pins (the `EscalationTokenPageTests` +
  `EscalationInboundEndpointTests` classes, in `tests/Kumunita.Web.Tests/`):
  (a) a `GlobalAdmin` sees the token list at `/admin/escalation/tokens`, (b)
  generating shows the plaintext **once** + a masked row, (c) revoking removes
  the token from the list, (d) a non-`GlobalAdmin` gets a 403 on
  `/admin/escalation` + `/admin/escalation/tokens` (ESC·12), (e) the outbound
  config save persists `Endpoint` + `Token` + `Enabled` (ESC·4), (f)
  `POST /escalations/inbound` with a valid token creates a row + a 200, (g)
  with an **invalid** token → **401**, no row (ESC·5), (h) the forwarder with a
  **DB** config sends the `Bearer` token (the ESC·6 pin — the forwarder is
  stubbed / the HTTP asserted), (i) the forwarder with **no** DB config + the
  env var falls back (the ESC·4 pin), (j) the forwarder with **no** config + no
  env var is a no-op (FACES ESC-6), (k) the list with `?filter=received` shows
  only `Origin = "escalated"` rows (ESC·8), (l) the detail view shows
  `FromInstance` for a received row (ESC·8).
- **The terminal constraints in `AGENTS.md` and `copilot-instructions.md`
  bind** — no here-strings, no multi-line terminal commands, `$`-variables
  don't survive between commands, the `dotnet test` discovery bug on this
  machine (use the in-process `dotnet exec tests/…/bin/Debug/net10.0/*.dll`
  path).

## Approach

One track, **Core tokens/config + Core inbound + Web pages + Web wire + kw-l +
tests + close**, sequenced. **U00** verifies the surface (the M32
`EscalationForwarder` + `IEscalationForwarder` + the `ErrorReport` 15-member
doc + the `IErrorReportService` 4-method surface + the `AccessVia` enum + the
`ErrorReportDocTypes` + the `/admin/error-reports` list / detail / resolve /
escalate + the M32 `errorreport.*` `kw-l` keys + the ADR index — confirm
**0159** is free) + authors the handoff-note skeleton. **U01 / U02** author the
primary-tier design doc (invariants ESC·1–ESC·13 + FACES ESC-1–ESC-10 + the two
new doc shapes + the `IEscalationTokenService` seam + the `AcceptInboundAsync`
seam + the forwarder token change + the closed `escalation.*` `kw-l` key set +
the pinned test names + the acceptance gate + the drift guard) and draft
**ADR 0159**. **U03** implements the Core tokens + config (the two docs +
`IEscalationTokenService` + `EscalationDocTypes` + DI + `AccessVia.Escalation`).
**U04** implements the Core inbound (the four additive `ErrorReport` fields +
the `Origin` "escalated" value + the `AcceptInboundAsync` seam + the inbound
audit row). **U05** ships the Web token + config admin pages. **U06** ships the
Web inbound endpoint + forwarder token change + the received filter + chip +
detail fields. **U07** authors the closed `escalation.*` `kw-l` key set ×
en/de/fr/da. **U08** runs + records the acceptance gate. **U09** flips the
close (the six-member close flip).

Every code unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug`). The last unit (U09) appends the final handoff section so the lane is
honest.

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U00–U09 below), one
unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/esc-escalation-authorization-design.md`,
  U01/U02 author) — pins the invariants (ESC·1–ESC·13), the FACES
  (ESC-1–ESC-10), the two new doc shapes, the `IEscalationTokenService` seam,
  the `AcceptInboundAsync` seam, the forwarder token change, the closed
  `escalation.*` `kw-l` key set, the pinned test names, the acceptance gate,
  and the drift guard.
- **Secondary — this file**
  (`docs/plans-milestones/plan-esc-escalation-authorization.md`) — the unit
  registry with each unit's deliverables and exit criteria. (The flat-lane
  convention — the main plan sits at the top of `docs/plans-milestones/`, the
  unit plans sit in `docs/plans-milestones/in-progress/` as `esc-u00.md` …
  `esc-u09.md`, and move to `docs/plans-milestones/done/esc/` as each unit
  completes.)
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/esc-handoff-notes.md`) — one section per
  unit, appended (never rewritten).

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list, 4–8
files < ~300 lines each, no full-repo scan; the design-doc section cited is
named); **Deliverables** (a closed set of new/modified files, ≤ ~5 files /
~500 LOC, no misc cleanups); **Exit** (`dotnet build Kumunita.slnx -c Debug`
green for the touched projects; handoff-note entry appended *before* any
follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard;
(3) never introduces a test whose exact name is not in the §pinned-test-names
list; (4) never re-shapes the M31 / M32 `ErrorReport` doc's field set outside
the design doc §2.2 pin (the ESC·2 / ADR 0159 D1 pin — the M32 15 are never
re-shaped, the ESC 4 are additive-only); (5) never touches the M31
`CreateAsync` / `MarkTriagedAsync` / `ListAsync` or the M32
`MarkResolvedAsync` seams or the M31 / M32 `errorreport.*` / `issue.*` `kw-l`
key sets (the ESC·1 pin — ESC **adds** to the M31 / M32 surface, it does not
**re-shape** it); (6) never stores a token **plaintext** on the receiving side
(the `EscalationToken.TokenHash` is the SHA-256 digest; the plaintext is shown
once, never persisted — the ESC·3 pin); (7) never puts an outbound `HttpClient`
call in `Kumunita.Core` (the ESC·6 pin — Core stays HTTP-free, ADR 0006-D; the
forwarder + the inbound endpoint are **Web-layer**); (8) never creates a
received `ErrorReport` row without a **valid** token (the ESC·5 pin — invalid /
revoked token → 401, no row); (9) never writes a second row on an idempotent
re-delivery (the ESC·7 pin — the `(TokenId, SourceReportId)` key); (10) never
adds a new `AccessVia` value other than `Escalation`, and never re-shapes the
12 frozen `AccessVia` values (the ESC·10 pin); (11) if entry reads reveal the
design doc is out of date, the unit pauses and records `## U<m> — Drift pause`
in the handoff note.

---

## Units (10 total: U00–U09)

### U00 — Kickoff verification + handoff-note skeleton

- **Goal:** verify the surface (the M32 `EscalationForwarder` +
  `IEscalationForwarder` + the `ErrorReport` 15-member doc + the
  `IErrorReportService` 4-method surface + the `AccessVia` enum (12 values) +
  the `ErrorReportDocTypes` + the `/admin/error-reports` list / detail /
  resolve / escalate + the M32 `errorreport.*` `kw-l` keys + the ADR index —
  confirm **0159** is free) and author the handoff-note skeleton (the "Lane
  open" section). **No code, no build, no test.**
- **Entry reads:**
  `src/Kumunita.Core/ErrorReports/ErrorReport.cs` (the M32 15-member doc — the
  frozen base ESC extends additively; confirm the `Origin` field + the
  `TriageStatus` value set);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (the M31 3 + the M32
  `MarkResolvedAsync` 4-method surface ESC adds to);
  `src/Kumunita.Core/ErrorReports/ErrorReportDraft.cs` (the M31/M32 draft
  record — confirm ESC's `InboundReport` is a **separate** record, not a
  re-shape);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (the M32
  `MarkResolvedAsync` idempotent-write-lane shape ESC mirrors for
  `AcceptInboundAsync`);
  `src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs` (the M31
  registration — confirm the `EscalationDocTypes` surface is a **new**
  parallel surface, the Usage `UsageDocTypes` precedent);
  `src/Kumunita.Web/Services/EscalationForwarder.cs` (the M32 forwarder — the
  impl ESC modifies for the `Bearer` token + the DB-config read);
  `src/Kumunita.Web/Services/IEscalationForwarder.cs` (the M32 forwarder
  seam — confirm `ForwardAsync(reportId)` is **unchanged**);
  `src/Kumunita.Core/Authorization/Decision.cs` (the `AccessVia` enum — the 12
  values ESC appends to);
  `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (the M31/M32
  admin surface — the shape ESC's received filter + detail fields mirror);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the M32
  `errorreport.escalate.*` keys — confirm the `escalation.*` namespace is
  **free**; the ESC closed set is a **new** namespace);
  `docs/adr/README.md` (the ADR index — confirm **0159** is free after the
  0158 row; 0158 = surface-label-translation `Accepted — **Done** (LBL-2)`);
  `docs/plans-milestones/done/m32/plan-m32-issue-submission-escalation.md`
  (the M32 register — the structural template for this register + the
  `KUMUNITA_ESCALATION_ENDPOINT` M32·6 pin ESC evolves).
- **Deliverables (1 file, new):**
  `docs/plans-milestones/in-progress/esc-handoff-notes.md` — the **skeleton
  only** (the header + the "Lane open" section + the
  `<!-- U00 appends its section below this line. One ## section per unit, in
  order (U00, U01, … U09). Never rewrite a prior section. -->` marker). The
  skeleton mirrors the `m32-handoff-notes.md` shape (the "Lane open" section
  names the register, the design doc, the ADR, the scope, the out-of-scope
  deferrals, and the frozen base — the M32 `ErrorReport` doc is **reused**
  (ESC·1), the M32 `EscalationForwarder` is **extended** (ESC·6), the
  `KUMUNITA_ESCALATION_ENDPOINT` env var is the **fallback** (ESC·4), the
  `AccessVia` enum is the ESC·10 append target, and the `escalation.*` `kw-l`
  namespace is the ESC·11 new surface).
- **Exit:** the handoff-note skeleton is present. The `## Lane open` section
  names (a) the M32 `ErrorReport` doc (the 15-member ceiling ESC extends
  additively to 19 — the ESC·1 / ESC·2 pins), (b) the M31/M32
  `IErrorReportService` 4-method surface (the ESC·1 pin — ESC adds
  `AcceptInboundAsync`), (c) the M32 `EscalationForwarder` (the ESC·6
  `Bearer`-token extension target), (d) the `KUMUNITA_ESCALATION_ENDPOINT` env
  var (the ESC·4 fallback), (e) the `AccessVia` 12-value set (the ESC·10
  append target), (f) the **ADR 0159** (the next free number after 0158 — the
  ADR index confirms 0158 is the current highest). Handoff note: a
  `## U00 — Kickoff verified` section with the current M32 surface shape (the
  `ErrorReport` field count [15], the `IErrorReportService` method count [4],
  the `AccessVia` value count [12], the `EscalationForwarder` payload
  field count [8], the `ErrorReportAdminController` action count [5: `Index` +
  `MarkTriaged` + `Detail` + `Resolve` + `Escalate`]), the `escalation.*`
  `kw-l` namespace **free** flag, the ADR number (0159) + the precedent ADR
  list (0004 §B.1 [additive fields + parallel doc surfaces], 0001-B
  [thin-token], 0006-C3 [single-write-lane], 0006-D [Core HTTP-free],
  0154 [the M31 surface], 0155 [the M32 surface ESC reuses + evolves]). Move
  this unit plan `in-progress/esc-u00.md` → `done/esc/` (move **last**).
  `git status` clean except the one new handoff-note file.

### U01 — Design doc Part 1 (context, scope, invariants, FACES)

- **Goal:** author `docs/design/esc-escalation-authorization-design.md`
  Part 1 — **Context, Scope (in/out incl. the named deferral list), Invariants
  pinned for ESC (ESC·1–ESC·13), FACES (10)**. **No code, no build.**
- **Entry reads:**
  `docs/plans-milestones/plan-esc-escalation-authorization.md` (this register —
  the Understanding, the "one thing" section, the Assumptions);
  `docs/plans-milestones/done/m32/plan-m32-issue-submission-escalation.md`
  (the M32 register — the structural template + the M32·6 env-var pin ESC
  evolves);
  `docs/design/m32-issue-submission-escalation-design.md` (the M32 design doc —
  the FACES/invariant template to emulate + the `EscalationForwarder` shape);
  `src/Kumunita.Core/ErrorReports/ErrorReport.cs` (the M32 15-member doc — the
  frozen base ESC extends);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (the M31/M32 4-method
  surface — the frozen base ESC adds to);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (the M32 impl — the
  `MarkResolvedAsync` idempotent-write-lane shape ESC mirrors);
  `src/Kumunita.Web/Services/EscalationForwarder.cs` (the M32 forwarder — the
  impl ESC modifies);
  `src/Kumunita.Core/Authorization/Decision.cs` (the `AccessVia` enum — the
  ESC·10 append target);
  `docs/adr/0155-issue-submission-escalation.md` (the M32 ADR — the ESC
  boundary context + the format to mirror for ADR 0159);
  `docs/adr/0006-module-boundary-contracts.md` (the §C3 single-write-lane +
  the §D "Core stays HTTP-free" rule — the ESC·6 pin).
- **Deliverables (1 file, new):**
  `docs/design/esc-escalation-authorization-design.md` (~240 lines). Sections:
  - `## Context` — M32 made escalation possible but unauthenticated + blind;
    ESC adds the token authorization + the receiving inbox + the outbound
    config page. ESC reuses the M31/M32 `ErrorReports` surface (ESC·1) and
    extends it additively (ESC·2 / ESC·3 / ESC·4). The env var remains the
    unauthenticated operator fallback (ESC·4).
  - `## Scope` — **In:** the `EscalationToken` + `EscalationOutboundConfig`
    docs, the `IEscalationTokenService` seam, the `EscalationDocTypes` surface,
    the four additive `ErrorReport` fields + the `Origin` "escalated" value
    (the ESC·2 19-member ceiling), the `AcceptInboundAsync` seam, the
    `AccessVia.Escalation` value, the `/admin/escalation` +
    `/admin/escalation/tokens` admin surfaces, the `POST /escalations/inbound`
    endpoint, the forwarder `Bearer`-token + DB-config change, the received
    filter + chip + detail fields, the closed `escalation.*` `kw-l` key set ×
    en/de/fr/da, the test pins. **Out (named deferrals):** the inbound rate
    limiter, the outbound retry queue, the escalation reply lane, the per-token
    allowlist, and the `Milestones.cs` / README / `MilestonesTests` trio until
    the lane *ships* (U09 owns it).
  - `## Invariants (pinned for ESC)` — ESC·1 through ESC·13, each with a one-
    line note (verbatim from the register's "one thing" section).
  - `## FACES (pinned, 10)` — ESC-1 through ESC-10, each bound to invariants:
    - **ESC-1** the receiving admin generates a labeled token → the plaintext
      is shown **once**, the `EscalationToken` row (hash) is stored + one
      `AccessAudit` row (`escalation.token.generate`) — ESC·3, ESC·9
    - **ESC-2** the receiving admin hands the token to the origin admin
      (out-of-band); the origin admin sets endpoint + token at
      `/admin/escalation` → the `EscalationOutboundConfig` row is stored —
      ESC·4
    - **ESC-3** the origin admin escalates a report (valid token + configured
      endpoint) → the forwarder POSTs the payload + the `Bearer` token → the
      receiving inbound validates the token → creates an `Origin =
      "escalated"` `ErrorReport` row + one `AccessAudit` row (`Via =
      Escalation`) → the origin marks it `resolved` — ESC·3, ESC·5, ESC·6
    - **ESC-4** the origin admin escalates with an **invalid** token → the
      receiving inbound returns **401**, no row; the forwarder reports a
      failure (no local `resolved`) — ESC·5, ESC·7
    - **ESC-5** the origin admin escalates with a **revoked** token → the
      same as ESC-4 (401, no row) — ESC·5, ESC·9
    - **ESC-6** the origin admin escalates with **no** token / endpoint
      configured → the forwarder is a no-op (a "not configured" flash), no
      HTTP — ESC·4, ESC·6
    - **ESC-7** the receiving admin revokes a token → immediate (subsequent
      inbounds with that token → 401), one `AccessAudit` row
      (`escalation.token.revoke`) — ESC·9
    - **ESC-8** the receiving admin views
      `/admin/error-reports?filter=received` → only `Origin = "escalated"`
      rows, each showing `FromInstance` — ESC·8
    - **ESC-9** a re-delivery of the same origin report under the same token →
      idempotent (the existing row, no duplicate, no second audit row) — ESC·7
    - **ESC-10** a non-GlobalAdmin visits `/admin/escalation` or
      `/admin/escalation/tokens` → **403** — ESC·12
- **Exit:** file exists with all sections. **No build.** Handoff note: 5–6
  lines starting `## U01 — design doc Part 1`, listing the **13 invariants**
  (by id) and the **10 FACES** (ESC-1–ESC-10) so U02 can pin them by id.

### U02 — Design doc Part 2 (seams, contracts, test names, gate, drift-guard) + ADR 0159

- **Goal:** append `## Seams & contracts (Part 2, written by U2)` to the
  design doc — the exact C# shapes U03–U06 must match, the closed
  `escalation.*` `kw-l` key set, the **pinned seam-test names**, the
  **three-test acceptance gate**, and the **drift-guard**. Draft **ADR 0159**.
  **No code, no build.**
- **Entry reads:**
  U01's Part 1 (the invariant table is the primary source);
  `src/Kumunita.Core/ErrorReports/ErrorReport.cs` (the M32 15-member doc — the
  frozen base to extend);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (the M31/M32 surface
  — the frozen base to add to);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (the M32 impl — the
  `MarkResolvedAsync` idempotent-write-lane shape to mirror for
  `AcceptInboundAsync`);
  `src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs` (the M31
  registration — the `EscalationDocTypes` parallel-surface shape to author);
  `src/Kumunita.Core/DependencyInjection.cs` (where the M31/M32
  `IErrorReportService` is registered — confirm the new
  `IEscalationTokenService` DI line + the `AcceptInboundAsync` impl needs no
  new registration of its own);
  `src/Kumunita.Web/Services/EscalationForwarder.cs` (the M32 forwarder — the
  impl to modify for the `Bearer` token + the DB-config read);
  `src/Kumunita.Web/Program.cs` (the Web DI — confirm no new Web registration
  is needed for the inbound endpoint, only the forwarder's existing
  singleton);
  `src/Kumunita.Core/Authorization/Decision.cs` (the `AccessVia` enum — the
  `Escalation` value to append);
  `docs/adr/0155-issue-submission-escalation.md` (the M32 ADR — the format to
  mirror for ADR 0159);
  `docs/adr/README.md` (the ADR index — the 0159 row to add).
- **Deliverables (2 files: 1 append + 1 new):**
  - `docs/design/esc-escalation-authorization-design.md` (append). Sub-sections:
    - `### 2.1 frozen seam list (exact C#)` — the `IEscalationTokenService`
      (verbatim from the register's Assumptions: `ListAsync` /
      `GenerateAsync` / `RevokeAsync` / `ValidateAsync` + the
      `EscalationTokenSummary` record). The `AcceptInboundAsync` seam (the
      `InboundReport` + `InboundResult` records, verbatim). The
      `EscalationForwarder` change (the `ForwardAsync` signature is
      **unchanged**; the impl reads the DB `EscalationOutboundConfig` first,
      sends the `Bearer` token, falls back to the env var, no-ops when
      neither; the `EscalationResult` gains the `ConfiguredSource` member).
      The M31/M32 `IErrorReportService` 4-method surface is **unchanged** (the
      ESC·1 pin — additive-only).
    - `### 2.2 new ESC-owned Core types (exact C#)` — the `EscalationToken`
      doc (the ESC·3 9-member ceiling) + the `EscalationOutboundConfig` doc
      (the ESC·4 6-member ceiling) + the four additive `ErrorReport` fields
      (the ESC·2 19-member ceiling — the M32 15 + the ESC 4: `FromInstance?` /
      `EscalationReceivedAt?` / `EscalationSourceId?` / `EscalationTokenId?`)
      + the `Origin` value extension (`"escalated"`, the closed set
      `{"error-page","general","escalated"}`). The `IEscalationTokenService`
      impl (the `EscalationTokenService` additive class — the `GenerateAsync`
      + `RevokeAsync` single-write-lane shape, the `ValidateAsync` hash
      compare + `LastUsedAt` stamp, the `ListAsync` read). The
      `AcceptInboundAsync` impl (the `ErrorReportService` additive method —
      the `MarkResolvedAsync` idempotent-write-lane shape, the token
      validation + the idempotent lookup + the received-row store + the
      `AccessAudit` row `Via = Escalation`). The `EscalationDocTypes`
      registration (the new parallel surface — the `ErrorReportDocTypes`
      shape, the `EscalationToken` + `EscalationOutboundConfig` `.Schema.For`
      calls + the `(RevokedAt, Created)` index for the token list ordering).
      The `AccessVia.Escalation` additive value (the ESC·10 pin — the 12
      frozen values untouched).
    - `### 2.3 the closed `escalation.*` `kw-l` key set` — the ~24 keys
      (verbatim from the register's Assumptions), each with the en value (the
      de/fr/da values are U07's to author). The `escalation.list.status.
      escalated` key is the new status chip (the M32
      `errorreport.list.status.*` precedent).
    - `### 2.4 pinned seam tests (exact names)` — file
      `tests/Kumunita.Core.Tests/EscalationTokenTests.cs`:
      1. `ESC_3_Token_Generate_Stores_Hash_Not_Plaintext`
      2. `ESC_3_Token_Validate_Valid_Returns_Token`
      3. `ESC_3_Token_Validate_Invalid_Returns_Null`
      4. `ESC_9_Token_Revoke_Immediate_NoValidate`
      5. `ESC_9_Token_List_Excludes_Plaintext_And_Hash`
      File `tests/Kumunita.Core.Tests/EscalationInboundTests.cs`:
      6. `ESC_5_AcceptInbound_ValidToken_Creates_Escalated_Origin`
      7. `ESC_5_AcceptInbound_InvalidToken_Returns_Error_NoRow`
      8. `ESC_7_AcceptInbound_ReDelivery_Idempotent_NoDuplicate`
      9. `ESC_2_ErrorReport_Doc_FieldSet_ESC_Ceiling`
      10. `ESC_10_AcceptInbound_AuditRow_Via_Escalation`
      File `tests/Kumunita.Web.Tests/EscalationTokenPageTests.cs`:
      11. `ESC_1_Token_Page_GlobalAdmin_Sees_TokenList`
      12. `ESC_1_Token_Generate_Plaintext_VisibleOnce`
      13. `ESC_7_Token_Revoke_Succeeds`
      14. `ESC_10_Token_Page_NonGlobalAdmin_Denied`
      15. `ESC_2_Outbound_Config_Save_Persists_Endpoint_Token_Enabled`
      File `tests/Kumunita.Web.Tests/EscalationInboundEndpointTests.cs`:
      16. `ESC_5_Inbound_Endpoint_ValidToken_Creates_Row`
      17. `ESC_4_Inbound_Endpoint_InvalidToken_401_NoRow`
      18. `ESC_6_Forwarder_DB_Config_Sends_Bearer_Token`
      19. `ESC_4_Forwarder_NoDB_EnvVar_Fallback`
      20. `ESC_6_Forwarder_NoConfig_NoHTTP`
      21. `ESC_8_Received_Filter_Sees_Escalated_Only`
      22. `ESC_8_Detail_Sees_FromInstance`
    - `### 2.5 acceptance gate (U08 records)` — the three-test shape:
      **closed loop** (the receiving admin generates a token → the origin
      admin sets endpoint + token → the origin escalates → the receiving
      inbound creates an `Origin = "escalated"` row + the audit row),
      **handoff** (the receiving admin revokes the token → a subsequent
      inbound with that token → 401, no row), **part-vs-whole** (the 22-test
      list is the whole; closed-loop + handoff are the parts; all must pass
      together).
    - `### 2.6 drift-guard (frozen once written)` — the 13-invariant table
      (U01), the 10 FACES (U01), the `IErrorReportService` 5-method surface
      (the M31 3 + the M32 1 + the ESC 1), the `ErrorReport` doc field set
      (the 19-member ESC ceiling — the M32 15 unchanged + the ESC 4 additive),
      the `EscalationToken` + `EscalationOutboundConfig` doc shapes, the
      `IEscalationTokenService` seam shape, the `AcceptInboundAsync` seam
      shape, the `AccessVia` 13-value set (the 12 frozen + the ESC 1), the
      `EscalationDocTypes` registration shape, the §2.3 `kw-l` key set, and
      the 22 test names — all frozen pins; any mismatch is a `## U<m> — Drift
      pause` per unit-series rule §11.
  - `docs/adr/0159-escalation-authorization.md` (new, ~100 lines) — the ADR in
    the `Status: Draft` state, following the M32 ADR 0155 format: Context
    (the M32 unauthenticated + blind escalation gap, the ESC boundary — ESC
    reuses the M31/M32 surface, ESC·1; the env var is the fallback, ESC·4),
    Decision (the `EscalationToken` + `EscalationOutboundConfig` docs, the
    `IEscalationTokenService` seam, the `AcceptInboundAsync` seam, the
    `POST /escalations/inbound` endpoint, the forwarder `Bearer`-token change,
    the four additive `ErrorReport` fields + the `Origin` "escalated" value,
    the `AccessVia.Escalation` value, the closed `escalation.*` `kw-l` key
    set, the received filter + chip), Consequences (the named deferrals, the
    M31/M32 `errorreport.*` / `issue.*` `kw-l` key sets are **untouched**
    (ESC adds the `escalation.*` namespace, ESC·1), the `ErrorReport` doc is
    the 19-member ESC ceiling (ESC·2), Core stays HTTP-free (ESC·6), the
    receiving-side token plaintext is **never persisted** (ESC·3), the env var
    is the unauthenticated fallback (ESC·4)). Plus the `docs/adr/README.md`
    index row (the 0159 row, `Status: Draft`).
- **Exit:** the design doc has all Part 2 sub-sections. The ADR 0159 is
  `Draft` + the index row is present. **No build.** Handoff note: 6–8 lines
  starting `## U02 — design doc Part 2 + ADR 0159`, listing (a) the sealed
  seam signatures (the `IEscalationTokenService.GenerateAsync` /
  `RevokeAsync` / `ValidateAsync` / `ListAsync` method names + the
  `AcceptInboundAsync` method name), (b) the 22 test names by id, (c) the
  three-test gate (by name), (d) the ADR 0159 number + the M32 surface
  **reused** flag (ESC·1) + the env-var **fallback** flag (ESC·4).

### U03 — Core: `EscalationToken` + `EscalationOutboundConfig` docs + `IEscalationTokenService` + `AccessVia.Escalation`

- **Goal:** add the two new docs (`EscalationToken` the ESC·3 9-member
  ceiling + `EscalationOutboundConfig` the ESC·4 6-member ceiling) to the
  `ErrorReports` context, add the `IEscalationTokenService` seam (the
  `ListAsync` / `GenerateAsync` / `RevokeAsync` / `ValidateAsync` read + write
  lanes) + the `EscalationTokenService` impl + the `EscalationDocTypes`
  registration surface + the `AccessVia.Escalation` additive value + the DI
  registration. **No Web change, no inbound endpoint yet** (U04).
- **Entry reads:**
  `docs/design/esc-escalation-authorization-design.md` §2.1 + §2.2 (the exact
  shapes);
  `src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs` (the M31
  registration — the `EscalationDocTypes` parallel-surface shape to mirror);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (the M32
  `MarkResolvedAsync` single-write-lane shape to mirror for
  `GenerateAsync` / `RevokeAsync`);
  `src/Kumunita.Core/Authorization/Decision.cs` (the `AccessVia` enum — the
  `Escalation` value to append);
  `src/Kumunita.Core/DependencyInjection.cs` (the M31/M32 `IErrorReportService`
  registration — the `IEscalationTokenService` DI line to add);
  `src/Kumunita.Core/Authorization/AccessAudit.cs` (the audit-row shape — the
  `Via` / `Action` / `TargetKind` fields);
  `src/Kumunita.Core/Identity/IdentityService.cs` lines 1385–1400 (the
  `RandomNumberGenerator` CSPRNG precedent — the token generation shape);
  `src/Kumunita.Core/Usage/UsageDocTypes.cs` (a parallel doc-surface
  precedent — the `EscalationDocTypes` shape).
- **Deliverables (≤ 6 files):**
  - `src/Kumunita.Core/ErrorReports/EscalationToken.cs` (new) — the ESC·3
    9-member doc (`Id` / `Label` / `TokenHash` / `TokenPrefix` / `Created` /
    `CreatedBy` / `LastUsedAt?` / `RevokedAt?` / `RevokedBy?`). A doc-comment
    pins the ESC·3 invariants (the hash-only-at-rest + the CSPRNG + the
    `kesc_` prefix).
  - `src/Kumunita.Core/ErrorReports/EscalationOutboundConfig.cs` (new) — the
    ESC·4 6-member doc (`Id` / `Endpoint` / `Token` / `Enabled` / `Updated` /
    `UpdatedBy`). A doc-comment pins the ESC·4 invariants (the singleton + the
    env-var fallback).
  - `src/Kumunita.Core/ErrorReports/IEscalationTokenService.cs` (new) — the
    seam (the design doc §2.1 verbatim: `ListAsync` / `GenerateAsync` /
    `RevokeAsync` / `ValidateAsync` + the `EscalationTokenSummary` record).
    Doc-comments anchored to the ESC·3 / ESC·9 invariants.
  - `src/Kumunita.Core/ErrorReports/EscalationTokenService.cs` (new) — the
    impl (the `GenerateAsync` + `RevokeAsync` single-write-lane shape — the
    `MarkResolvedAsync` precedent, the CSPRNG token generation + the SHA-256
    hash, the `ValidateAsync` constant-time hash compare + the `LastUsedAt`
    stamp, the `ListAsync` read that excludes the plaintext / hash).
  - `src/Kumunita.Core/ErrorReports/EscalationDocTypes.cs` (new) — the
    registration surface (the `EscalationToken` + `EscalationOutboundConfig`
    `.Schema.For` calls + the `(RevokedAt, Created)` index for the token list
    ordering — the `ErrorReportDocTypes` shape).
  - `src/Kumunita.Core/Authorization/Decision.cs` (modify) — append the
    `AccessVia.Escalation` value (the ESC·10 pin — the M1 `Admin` / ADR 013
    `Group` / ADR 0028 `Guardian` / ADR 0041 `Resident` append precedent; the
    12 frozen values are **unchanged**). A doc-comment names the ESC lane.
  - `src/Kumunita.Core/DependencyInjection.cs` (modify) — add the
    `services.AddTransient<IEscalationTokenService>(sp => new
    EscalationTokenService(sp.GetRequiredService<Marten.IDocumentStore>()))`
    line (the `IErrorReportService` registration shape). The
    `EscalationDocTypes.Configure` is wired into the existing Marten
    `StoreOptions` bootstrap (the `ErrorReportDocTypes.Configure` wire point).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The `EscalationToken`
  + `EscalationOutboundConfig` docs compile; the `IEscalationTokenService` +
  the `EscalationTokenService` impl compile; the `EscalationDocTypes` surface
  compiles; the `AccessVia.Escalation` value compiles; the DI line compiles.
  **No new test** (U08's seam tests are the first ESC tests). Handoff note:
  5–6 lines starting `## U03 — Core (tokens + config docs + service)` — (a)
  the two doc names + their member counts (9 + 6), (b) the four seam method
  names, (c) the `EscalationDocTypes` surface name, (d) the
  `AccessVia.Escalation` value, (e) the DI line, (f) any compile warnings.

### U04 — Core: additive `ErrorReport` fields + `Origin` "escalated" + `AcceptInboundAsync`

- **Goal:** add the four additive fields (`FromInstance?` /
  `EscalationReceivedAt?` / `EscalationSourceId?` / `EscalationTokenId?`) to
  the `ErrorReport` doc (the ESC·2 19-member ceiling — the M32 15 unchanged),
  extend the `Origin` closed set with `"escalated"` (the ESC·2 pin), and add
  the `AcceptInboundAsync` seam to `IErrorReportService` + the
  `ErrorReportService` impl (the ESC·5 / ESC·7 idempotent inbound write lane).
  **No Web change yet** (U05/U06).
- **Entry reads:**
  `docs/design/esc-escalation-authorization-design.md` §2.1 + §2.2 (the exact
  shapes);
  `src/Kumunita.Core/ErrorReports/ErrorReport.cs` (the M32 15-member doc — the
  frozen base to extend);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (the M31/M32 4-method
  surface — the `AcceptInboundAsync` seam to add);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (the M32 impl — the
  `MarkResolvedAsync` idempotent-write-lane shape to mirror);
  `src/Kumunita.Core/ErrorReports/IEscalationTokenService.cs` (U03's seam — the
  `ValidateAsync` method the impl calls);
  `src/Kumunita.Core/Authorization/AccessAudit.cs` (the audit-row shape — the
  `Via = Escalation` tag to write);
  `src/Kumunita.Core/Authorization/Decision.cs` (U03's `AccessVia` enum — the
  `Escalation` value to use).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Core/ErrorReports/ErrorReport.cs` (modify) — add the four
    additive fields (the §2.2 ESC·2 shape): `FromInstance` (string?, null for
    locally-filed — the accepting token's `Label`), `EscalationReceivedAt`
    (DateTimeOffset?, null for locally-filed), `EscalationSourceId` (string?,
    null for locally-filed — the origin's report id), `EscalationTokenId`
    (string?, null for locally-filed). The `Origin` field's doc-comment
    updates the closed set to `{"error-page","general","escalated"}` (the
    ESC·2 pin). The M32 15 fields are **unchanged** (the ESC·2 pin — no
    re-shape). A doc-comment pins the ESC·2 invariant.
  - `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (modify) — add the
    **exact** ESC seam (verbatim from the design doc §2.1):
    `Task<InboundResult> AcceptInboundAsync(InboundReport draft,
    CancellationToken ct = default);` with a doc-comment anchored to the ESC·5
    / ESC·7 invariants + the M31/M32 precedent. The M31 3 + the M32 1 methods
    are **unchanged** (the ESC·1 pin).
  - `src/Kumunita.Core/ErrorReports/InboundReport.cs` (new) — the `InboundReport`
    record (the design doc §2.1 verbatim: `TokenId` / `SourceReportId` /
    `FromInstance` / `Description` / `ContactEmail?` / `OriginSubjectId` /
    `RequestId` / `ExceptionType?` / `OriginCreated`) + the `InboundResult`
    record (`Created` / `Row` / `Error?`). A doc-comment pins the ESC·5 /
    ESC·7 invariants.
  - `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (modify) — add the
    `AcceptInboundAsync` impl (the M32 `MarkResolvedAsync` idempotent-write-
    lane shape): `ValidateAsync` the token (via the injected
    `IEscalationTokenService`) → if null → return `InboundResult(Created:
    false, Row: null!, Error: "invalid token")` (the Web endpoint maps this to
    a 401); else look up an existing row on `(EscalationTokenId = draft.
    TokenId, EscalationSourceId = draft.SourceReportId)` → if found → return
    it (`Created: false`, the ESC·7 idempotent path); else store a new
    `ErrorReport` (`Origin = "escalated"`, `FromInstance = draft.FromInstance`,
    `EscalationReceivedAt = now`, `EscalationSourceId = draft.SourceReportId`,
    `EscalationTokenId = draft.TokenId`, `SubjectId = draft.OriginSubjectId`,
    `Description = draft.Description`, `ContactEmail = draft.ContactEmail`,
    `RequestId = draft.RequestId`, `ExceptionType = draft.ExceptionType`,
    `Created = draft.OriginCreated`) + **one** `AccessAudit` row (`Via =
    Escalation`, action `"errorreport.inbound"`, `TargetKind` "error-report")
    in **one** write session (the ADR 0006 C3 single-write-lane shape) + stamp
    the token's `LastUsedAt` + return `InboundResult(Created: true, Row:
    report, Error: null)`. The M31 3 + the M32 1 methods are **unchanged**
    (the ESC·1 pin). The `ErrorReportService` ctor gains the
    `IEscalationTokenService` param (the M32 `IEscalationForwarder`
    optional-ctor-param precedent — so any test-construction site that builds
    the service without the token service keeps compiling; the
    `AcceptInboundAsync` impl then returns `Error: "token service absent"`
    when the seam is null, the M31 `localization` / `translationProvider`
    floor precedent).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The `ErrorReport` doc
  compiles with the 19-member field set; the `IErrorReportService` compiles
  with 5 methods (the M31 3 + the M32 1 + the ESC 1); the `InboundReport` +
  `InboundResult` records compile; the `ErrorReportService.AcceptInboundAsync`
  compiles. **No new test** (U08's seam tests are the first ESC tests).
  Handoff note: 5–6 lines starting `## U04 — Core (additive fields +
  AcceptInboundAsync)` — (a) the 4 additive field names, (b) the `Origin`
  "escalated" value, (c) the `AcceptInboundAsync` method signature, (d) the
  `InboundReport` + `InboundResult` record names, (e) the
  `ErrorReportService` ctor param (the `IEscalationTokenService` optional),
  (f) any compile warnings.

### U05 — Web: the `/admin/escalation` outbound-config + `/admin/escalation/tokens` receiving-token admin surfaces

- **Goal:** ship the two admin surfaces — the **outbound config** (the
  `GET /admin/escalation` form + the `POST /admin/escalation` save, the
  `EscalationConfigController` + the `EscalationConfigFormModel` + the
  `Views/Admin/Escalation/Index.cshtml`) and the **receiving tokens** (the
  `GET /admin/escalation/tokens` list + the `POST …/tokens/generate` + the
  `POST …/tokens/{id}/revoke` actions, the `EscalationTokenController` + the
  `EscalationTokenViewModel` + the `Views/Admin/Escalation/Tokens.cshtml`).
  Both are `[Authorize(Roles = GlobalAdmin)]` (the ESC·12 pin). The outbound
  config save writes the `EscalationOutboundConfig` doc; the token generate /
  revoke call the `IEscalationTokenService` seam.
- **Entry reads:**
  `docs/design/esc-escalation-authorization-design.md` §2.1 (the
  `IEscalationTokenService` seam) + §2.2 (the two doc shapes) + §2.3 (the
  `escalation.*` `kw-l` key set — the form labels);
  `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (the M31/M32
  admin controller — the `[Authorize(Roles = GlobalAdmin)]` gate + the
  optional-ctor-param precedent to mirror);
  `src/Kumunita.Web/Models/AdminErrorReportViewModel.cs` (the M31 list view
  model — the shape to mirror for the `EscalationConfigFormModel`);
  `src/Kumunita.Web/Views/Admin/ErrorReports/Index.cshtml` (the M31 list view
  — the form markup to mirror, the `kw-l` TagHelper usage);
  `src/Kumunita.Core/ErrorReports/IEscalationTokenService.cs` (U03's seam —
  the `ListAsync` / `GenerateAsync` / `RevokeAsync` methods to call);
  `src/Kumunita.Core/ErrorReports/EscalationOutboundConfig.cs` (U03's doc — the
  `Endpoint` / `Token` / `Enabled` fields to bind);
  `src/Kumunita.Web/Views/Shared/_ValidationScriptsPartial.cshtml` (the
  validation partial to include in the form).
- **Deliverables (≤ 6 files):**
  - `src/Kumunita.Web/Controllers/EscalationConfigController.cs` (new) — the
    **outbound config** admin controller (the ESC·4 pin — the endpoint + token
    + enabled). `[Route("admin/escalation")]` + `[Authorize(Roles =
    GlobalAdmin)]` (the ESC·12 pin). `GET /admin/escalation` → load the
    `EscalationOutboundConfig` singleton (a read), bind to
    `EscalationConfigFormModel` (the `Endpoint` + `Token` + `Enabled` fields +
    a `Saved` flag), `View`. `POST /admin/escalation` → bind
    `EscalationConfigFormModel`, validate (the `Endpoint` is required + a
    valid URL when `Enabled`; the `Token` is optional when `Enabled == false`),
    write the `EscalationOutboundConfig` doc (a single-write-lane — the
    `MarkResolvedAsync` shape, one `AccessAudit` row `Via = Admin`, action
    `escalation.outbound.save`), re-render with the `Saved` flag (the
    `escalation.outbound.saved` `kw-l` key). Inject `IEscalationTokenService`
    (for the config read / write) + the `IErrorReportService` is **not**
    needed here.
  - `src/Kumunita.Web/Models/EscalationConfigFormModel.cs` (new) — the form
    model (the M31 `ErrorReportFormModel` shape): `Endpoint` (string,
    `[Required]`, `[Url]` when enabled), `Token` (string?, `[Required]` when
    enabled), `Enabled` (bool, default `false`), `Saved` (bool, default
    `false`).
  - `src/Kumunita.Web/Views/Admin/Escalation/Index.cshtml` (new) — the
    outbound-config form view. The `escalation.outbound.endpoint.label` /
    `escalation.outbound.endpoint.placeholder` /
    `escalation.outbound.token.label` / `escalation.outbound.token.placeholder`
    / `escalation.outbound.enabled.label` / `escalation.outbound.save` `kw-l`
    keys (the U07 closed set). A `<input type="url">` for the endpoint, a
    `<input type="password">` for the token (the admin's *outgoing* secret —
    the ESC·4 pin), a `<input type="checkbox">` for `Enabled`, a submit
    button. The form POSTs to `/admin/escalation` with the anti-forgery token.
    When `Model.Saved` is `true`, render the `escalation.outbound.saved`
    confirmation.
  - `src/Kumunita.Web/Controllers/EscalationTokenController.cs` (new) — the
    **receiving tokens** admin controller (the ESC·3 pin — the generate /
    list / revoke). `[Route("admin/escalation/tokens")]` +
    `[Authorize(Roles = GlobalAdmin)]` (the ESC·12 pin). `GET
    /admin/escalation/tokens` → `ListAsync` the tokens (a read), `View` with
    the `EscalationTokenViewModel`. `POST …/tokens/generate` → bind the
    `Label` (string, required), call `GenerateAsync(label, actor)` (the
    `IEscalationTokenService` seam — the ESC·3 single-write-lane), render the
    list view with the **plaintext shown once** (a `TempData["newToken"]`
    flash, the `escalation.tokens.show_once` `kw-l` key — the plaintext is
    **never** re-rendered on a subsequent GET, the ESC·3 pin). `POST
    …/tokens/{id}/revoke` → call `RevokeAsync(id, actor)` (the ESC·9
    idempotent write lane), redirect to the list with a `TempData["info"]`
    flash (the `escalation.tokens.revoked` `kw-l` key).
  - `src/Kumunita.Web/Models/EscalationTokenViewModel.cs` (new) — the list view
    model: `public sealed class EscalationTokenViewModel { public
    IReadOnlyList<EscalationTokenSummary> Tokens { get; init; } =
    Array.Empty<EscalationTokenSummary>(); public string? NewTokenPlaintext
    { get; init; } public string? Flash { get; init; } public string? Error
    { get; init; } }`.
  - `src/Kumunita.Web/Views/Admin/Escalation/Tokens.cshtml` (new) — the
    receiving-token list view. The `escalation.tokens.title` /
    `escalation.tokens.generate.button` /
    `escalation.tokens.label.label` / `escalation.tokens.show_once` /
    `escalation.tokens.copy` / `escalation.tokens.revoke.button` /
    `escalation.tokens.revoked` / `escalation.tokens.none` /
    `escalation.tokens.last_used` `kw-l` keys (the U07 closed set). When
    `Model.NewTokenPlaintext` is non-null, render the plaintext **once** (a
    `<code>` block + a copy button, the `escalation.tokens.show_once`
    warning). The token list: each row shows the `Label` + the
    `TokenPrefix` (the masked form, e.g. `kesc_ab12…`) + the `Created` + the
    `LastUsedAt?` + the `RevokedAt?` (a revoked chip) + a "Revoke" button
    (a `POST …/tokens/{id}/revoke` with the anti-forgery token). The generate
    form: a `<input>` for the `Label` + a "Generate" button (a `POST
    …/tokens/generate` with the anti-forgery token). When `Model.Tokens.Count
    == 0`, render the `escalation.tokens.none` empty state.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `EscalationConfigController` compiles (the `GET` + `POST` actions); the
  `EscalationConfigFormModel` compiles; the `Views/Admin/Escalation/
  Index.cshtml` renders (the `kw-l` keys are consumed by the view). The
  `EscalationTokenController` compiles (the `GET` + the `generate` + the
  `revoke` actions); the `EscalationTokenViewModel` compiles; the
  `Views/Admin/Escalation/Tokens.cshtml` renders. Handoff note: 5–6 lines
  starting `## U05 — Web (outbound config + receiving tokens pages)` — (a)
  the two admin routes (`/admin/escalation` + `/admin/escalation/tokens`),
  (b) the `EscalationConfigFormModel` fields (4), (c) the
  `EscalationTokenViewModel` fields (4), (d) the token generate / revoke
  action names, (e) the `plaintext-shown-once` pin (the ESC·3 pin), (f) any
  compile warnings.

### U06 — Web: the `POST /escalations/inbound` endpoint + forwarder `Bearer` token + received filter / chip / detail

- **Goal:** ship the wire + the received view — the **inbound acceptance
  endpoint** (`POST /escalations/inbound`, the `EscalationInboundController`
  + the token-gated `AcceptInboundAsync` call), the **`EscalationForwarder`**
  change (the `Bearer` token header + the DB-config read + the env-var
  fallback), the **received filter + `escalated` chip** on the existing
  `/admin/error-reports` list (the `?filter=received|local` selector + the
  `escalation.list.status.escalated` chip), and the **`FromInstance` /
  `EscalationReceivedAt`** on the detail view. The inbound endpoint is
  **public** (no session gate — the token is the credential, the ESC·5 pin).
- **Entry reads:**
  `docs/design/esc-escalation-authorization-design.md` §2.1 (the
  `AcceptInboundAsync` seam + the forwarder change) + §2.2 (the four additive
  `ErrorReport` fields);
  `src/Kumunita.Web/Services/EscalationForwarder.cs` (the M32 forwarder — the
  impl to modify for the `Bearer` token + the DB-config read);
  `src/Kumunita.Web/Services/IEscalationForwarder.cs` (the M32 forwarder
  seam — confirm `ForwardAsync(reportId)` is **unchanged**);
  `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (the M31/M32
  admin controller — the list + detail views to extend with the filter + chip
  + `FromInstance`);
  `src/Kumunita.Web/Models/AdminErrorReportViewModel.cs` (the M31 list view
  model — the `Filter` + the `escalated` chip to add);
  `src/Kumunita.Web/Views/Admin/ErrorReports/Index.cshtml` (the M31 list view
  — the filter + chip to add);
  `src/Kumunita.Web/Views/Admin/ErrorReports/Detail.cshtml` (the M32 detail
  view — the `FromInstance` + `EscalationReceivedAt` to add);
  `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` (U04's interface —
  the `AcceptInboundAsync` seam to call);
  `src/Kumunita.Core/ErrorReports/InboundReport.cs` (U04's record — the
  `InboundReport` + `InboundResult` shapes to bind);
  `src/Kumunita.Web/Program.cs` (the Web DI — confirm the
  `EscalationForwarder` singleton is already registered, no new registration
  needed for the inbound endpoint).
- **Deliverables (≤ 6 files):**
  - `src/Kumunita.Web/Controllers/EscalationInboundController.cs` (new) — the
    **public** token-gated inbound endpoint (the ESC·5 pin — **no** `[Authorize]`
    session gate; the token is the credential). `[Route("escalations")]`.
    `POST /escalations/inbound` → read the `Authorization: Bearer <token>`
    header (the ESC·6 pin — parse the `Bearer` scheme, the token is the
    plaintext), read the JSON body (the M32 8-field payload — the M31/M32
    forwarder shape), validate the `Description` (required, non-blank — a
    400 re-render, the M32·4 pin), build the `InboundReport` record (the
    U04 shape — the `TokenId` is resolved from the `ValidateAsync` match, the
    `SourceReportId` is the origin's `id`, the `FromInstance` is the matching
    token's `Label`, the `OriginSubjectId` / `ContactEmail` / `RequestId` /
    `ExceptionType` / `OriginCreated` are the origin's fields), call
    `IErrorReportService.AcceptInboundAsync(report)` (the ESC·5 / ESC·7 seam),
    **if `result.Error is not null`** (invalid token) → `Unauthorized()` (the
    401, the ESC·5 pin — no row, no audit row), **if `result.Created ==
    true`** → `Ok(new { id = result.Row.Id, received = true })` (the 200, the
    ESC·5 pin), **if `result.Created == false`** (idempotent) → `Ok(new { id =
    result.Row.Id, received = false })` (the 200, the ESC·7 pin — the existing
    row, no duplicate). Inject `IErrorReportService` + `IEscalationTokenService`
    (for the `ValidateAsync` + the `TokenId` / `FromInstance` resolution).
  - `src/Kumunita.Web/Services/EscalationForwarder.cs` (modify) — the
    forwarder change (the ESC·4 / ESC·6 pins). `ForwardAsync` **signature is
    unchanged** (the M32·5 pin). The impl: (1) read the **DB**
    `EscalationOutboundConfig` singleton (via a new `IOutboundConfigReader`
    seam on `IEscalationTokenService` or a direct `IDocumentStore` read — the
    design doc §2.1 pins the exact read seam); **if present + enabled** →
    use its `Endpoint` + send the `Authorization: Bearer <Token>` header (the
    ESC·6 pin), set `ConfiguredSource = "db"`; (2) **if absent / disabled** →
    fall back to the M32·6 env var `configuration["KUMUNITA:
    ESCALATION_ENDPOINT"]` (endpoint only, **no** token — the unauthenticated
    M32-style forward, the ESC·4 pin), set `ConfiguredSource = "env"`; (3)
    **if neither** is present → the M32 no-op (`Configured == false`, FACES
    ESC-6). The payload (the M32 8-field shape) is **unchanged** (the M32·1
    pin). The `EscalationResult` gains the `ConfiguredSource` member (the
    ESC·4 pin). The ctor gains the `IEscalationTokenService` param (the M32
    `IEscalationForwarder` optional-ctor-param precedent — so any
    test-construction site that builds the forwarder without the token service
    keeps compiling; the forwarder then falls back to the env-var-only path
    when the seam is null, the M31 floor precedent).
  - `src/Kumunita.Web/Services/IEscalationForwarder.cs` (modify) — the
    `EscalationResult` record gains the additive `ConfiguredSource` member
    (the ESC·4 pin — the M32 4 members unchanged). The `ForwardAsync` method
    is **unchanged** (the M32·5 pin).
  - `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` (modify) —
    the **received filter + chip** on the list + the **`FromInstance` /
    `EscalationReceivedAt`** on the detail. The `Index` action gains a
    `?filter=` query param (`"all"` (default) / `"received"` / `"local"`);
    when `filter == "received"` → filter the `ListAsync` result to
    `Origin == "escalated"` rows; when `filter == "local"` → filter to
    `Origin != "escalated"` rows (the ESC·8 pin — the M31·4 list shape is
    **unchanged**, the filter is additive). The `AdminErrorReportViewModel`
    gains a `Filter` field (the bound `?filter=` value) + the `escalated`
    chip is rendered per row (the `escalation.list.status.escalated` `kw-l`
    key, the M32 `errorreport.list.status.*` chip precedent). The `Detail`
    action + view gain the `FromInstance` + `EscalationReceivedAt` fields
    (the `escalation.detail.from_instance` `kw-l` key, rendered when
    non-null — the ESC·8 pin). The existing M31/M32 actions (`Index` /
    `MarkTriaged` / `Detail` / `Resolve` / `Escalate`) are **unchanged** (the
    ESC·1 pin — additive-only).
  - `src/Kumunita.Web/Models/AdminErrorReportViewModel.cs` (modify) — the
    list view model gains the `Filter` field (string, default `"all"` — the
    bound `?filter=` value, the ESC·8 pin). The M31 `Reports` field is
    **unchanged** (the ESC·1 pin).
  - `src/Kumunita.Web/Views/Admin/ErrorReports/Index.cshtml` (modify) — the
    list view gains the **filter selector** (a `<select name="filter">` with
    the `escalation.list.filter.all` / `escalation.list.filter.received` /
    `escalation.list.filter.local` `kw-l` keys, the ESC·8 pin) + the
    **`escalated` chip** per row (the `escalation.list.status.escalated` `kw-l`
    key, the `text-bg-info` Bootstrap class — the M32 `new` / `triaged` chip
    precedent). The existing `new` / `triaged` / `resolved` chips are
    **unchanged** (the ESC·1 pin — additive-only).
  - `src/Kumunita.Web/Views/Admin/ErrorReports/Detail.cshtml` (modify) — the
    detail view gains the **`FromInstance`** field (the
    `escalation.detail.from_instance` `kw-l` key, rendered when non-null — the
    ESC·8 pin) + the **`EscalationReceivedAt`** field (the `kw-dt` TagHelper,
    rendered when non-null). The existing M32 fields (the 15-member field set)
    are **unchanged** (the ESC·1 pin — additive-only).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `EscalationInboundController` compiles (the `POST /escalations/inbound`
  action); the `EscalationForwarder` compiles with the `Bearer`-token +
  DB-config + env-var-fallback logic; the `IEscalationForwarder.
  EscalationResult` compiles with the `ConfiguredSource` member; the
  `ErrorReportAdminController` compiles with the `?filter=` param + the
  `FromInstance` / `EscalationReceivedAt` detail fields; the
  `AdminErrorReportViewModel` compiles with the `Filter` field; the
  `Views/Admin/ErrorReports/Index.cshtml` renders the filter + the
  `escalated` chip; the `Views/Admin/ErrorReports/Detail.cshtml` renders the
  `FromInstance` + `EscalationReceivedAt`. Handoff note: 5–6 lines starting
  `## U06 — Web (inbound endpoint + forwarder token + received view)` — (a)
  the inbound endpoint route + the token-gate pin (the ESC·5 pin), (b) the
  forwarder `Bearer`-token + DB-config + env-var-fallback pins (the ESC·4 /
  ESC·6 pins), (c) the `ConfiguredSource` member, (d) the `?filter=` values
  (`all` / `received` / `local`), (e) the `escalated` chip + the
  `FromInstance` / `EscalationReceivedAt` detail fields, (f) any compile
  warnings.

### U07 — closed `escalation.*` `kw-l` key set × en/de/fr/da

- **Goal:** author the **closed `escalation.*` `kw-l` key set** × en/de/fr/da
  (the ~24 keys from the design doc §2.3). The `KnownTranslationKeys.cs`
  gains the new keys (the M31/M32 `errorreport.*` / `issue.*` keys are
  **unchanged** — the ESC·1 pin, additive-only). The `escalation.*` namespace
  is **new** (the U00 "free" confirmation).
- **Entry reads:**
  `docs/design/esc-escalation-authorization-design.md` §2.3 (the closed
  `escalation.*` `kw-l` key set — the ~24 keys + the en values);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the `kw-l` key
  registry — where the new keys go; the M31/M32 `errorreport.*` / `issue.*`
  keys to leave unchanged);
  `docs/plans-milestones/done/m32/plan-m32-issue-submission-escalation.md`
  §U06 (the M32 `kw-l` key authoring unit — the en/de/fr/da value pattern to
  mirror);
  `src/Kumunita.Web/Models/EscalationConfigFormModel.cs` (U05's form model —
  the `escalation.outbound.*` keys to bind);
  `src/Kumunita.Web/Models/EscalationTokenViewModel.cs` (U05's token view
  model — the `escalation.tokens.*` keys to bind);
  `src/Kumunita.Web/Views/Admin/ErrorReports/Index.cshtml` (U06's list view —
  the `escalation.list.*` keys to bind);
  `src/Kumunita.Web/Views/Admin/ErrorReports/Detail.cshtml` (U06's detail
  view — the `escalation.detail.*` keys to bind).
- **Deliverables (1 file, modify):**
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (modify) — add
    the **closed `escalation.*` `kw-l` key set** × en/de/fr/da (the ~24 keys
    from the design doc §2.3, the M32 U06 shape): every key is present,
    non-empty, in **all four** languages (en/de/fr/da) — the
    `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`
    closure pins it (the ESC·11 pin). The `en` values are the source text
    (the ADR 0015 D1 `kw-l` provider-floor discipline); the `de` / `fr` / `da`
    values are the translations (the M30·6 four-language pin). The M31/M32
    `errorreport.*` / `issue.*` keys are **unchanged** (the ESC·1 pin —
    additive-only). The closed set (the §2.3 table, verbatim):
    `escalation.title` / `escalation.intro` /
    `escalation.outbound.endpoint.label` / `escalation.outbound.endpoint.placeholder` /
    `escalation.outbound.token.label` / `escalation.outbound.token.placeholder` /
    `escalation.outbound.enabled.label` / `escalation.outbound.save` /
    `escalation.outbound.saved` /
    `escalation.tokens.title` / `escalation.tokens.generate.button` /
    `escalation.tokens.label.label` / `escalation.tokens.show_once` /
    `escalation.tokens.copy` / `escalation.tokens.revoke.button` /
    `escalation.tokens.revoked` / `escalation.tokens.none` /
    `escalation.tokens.last_used` /
    `escalation.list.status.escalated` / `escalation.list.filter.all` /
    `escalation.list.filter.received` / `escalation.list.filter.local` /
    `escalation.detail.from_instance` /
    `escalation.inbound.invalid_token` / `escalation.inbound.received`
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The
  `KnownTranslationKeys.cs` compiles with the ~24 new keys (the M31/M32
  `errorreport.*` / `issue.*` keys unchanged — the ESC·1 pin); the
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` closure
  passes (the ESC·11 pin — the ~24 new keys are present, non-empty, in all
  four languages). Handoff note: 5–6 lines starting `## U07 — kw-l keys` —
  (a) the ~24 `kw-l` key names (verbatim), (b) the four-language status
  (en/de/fr/da all present), (c) the M31/M32 `errorreport.*` / `issue.*` keys
  unchanged flag (the ESC·1 pin), (d) the `escalation.*` namespace **new**
  flag (the U00 "free" confirmation), (e) any compile warnings.

### U08 — Seam tests (the 22 pinned names) + run + record the acceptance gate

- **Goal:** implement the 22 tests from the design doc §2.4 in
  `tests/Kumunita.Core.Tests/EscalationTokenTests.cs` (5 tests),
  `tests/Kumunita.Core.Tests/EscalationInboundTests.cs` (5 tests),
  `tests/Kumunita.Web.Tests/EscalationTokenPageTests.cs` (5 tests), and
  `tests/Kumunita.Web.Tests/EscalationInboundEndpointTests.cs` (7 tests). Run
  the three-test acceptance gate (closed-loop / handoff / part-vs-whole) and
  record it in the design doc.
- **Entry reads:**
  `docs/design/esc-escalation-authorization-design.md` §2.4 (the 22 test
  names, exact — the *primary* source for this unit) + §2.5 (the gate's
  three test names and their definitions);
  `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the test harness);
  `tests/Kumunita.Core.Tests/ErrorReportResolveTests.cs` (the M32 Core test
  file — the shape to mirror for `EscalationTokenTests` +
  `EscalationInboundTests`);
  `tests/Kumunita.Web.Tests/ErrorReportPageTests.cs` (the M31 Web page test
  file — the shape to mirror for `EscalationTokenPageTests`);
  `tests/Kumunita.Web.Tests/AdminErrorReportDetailTests.cs` (the M32 Web admin
  test file — the shape to mirror for `EscalationInboundEndpointTests`);
  `src/Kumunita.Core/ErrorReports/EscalationTokenService.cs` (U03's impl —
  the code under test);
  `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` (U04's impl — the
  code under test);
  `src/Kumunita.Web/Controllers/EscalationConfigController.cs` (U05's
  controller — the code under test);
  `src/Kumunita.Web/Controllers/EscalationTokenController.cs` (U05's
  controller — the code under test);
  `src/Kumunita.Web/Controllers/EscalationInboundController.cs` (U06's
  controller — the code under test);
  `src/Kumunita.Web/Services/EscalationForwarder.cs` (U06's forwarder — the
  code under test, the ESC·6 pin);
  `docs/plans-milestones/in-progress/esc-handoff-notes.md` (U03–U07's
  sections — the implementation notes that may inform the test setup).
- **Deliverables (5 files: 4 new + 1 modify):**
  - `tests/Kumunita.Core.Tests/EscalationTokenTests.cs` (new) — **5 tests**,
    one per pinned name (ESC_3_Token_Generate_Stores_Hash_Not_Plaintext
    through ESC_9_Token_List_Excludes_Plaintext_And_Hash). The
    `GenerateAsync` test asserts the stored `TokenHash` is the SHA-256 of the
    returned plaintext + the plaintext is **not** a doc field (the ESC·3 pin).
    The `ValidateAsync` tests assert the correct plaintext returns the token
    + the wrong plaintext returns `null` (the ESC·3 pin). The `RevokeAsync`
    test asserts the token no longer validates (the ESC·9 pin). The
    `ListAsync` test asserts the summary exposes no plaintext / hash (only
    the `TokenPrefix` — the ESC·3 / ESC·9 pin).
  - `tests/Kumunita.Core.Tests/EscalationInboundTests.cs` (new) — **5 tests**,
    one per pinned name (ESC_5_AcceptInbound_ValidToken_Creates_Escalated_
    Origin through ESC_10_AcceptInbound_AuditRow_Via_Escalation). The
    valid-token test asserts the stored `ErrorReport` row has `Origin =
    "escalated"` + `FromInstance` + `EscalationReceivedAt` +
    `EscalationSourceId` + `EscalationTokenId` + one `AccessAudit` row
    (`Via = Escalation`) (the ESC·5 pin). The invalid-token test asserts
    `Created == false` + `Error` is non-null + no row + no audit row (the
    ESC·5 pin). The re-delivery test asserts the existing row is returned +
    no second audit row (the ESC·7 pin). The field-set test asserts the 19-
    member field set (the M32 15 + the ESC 4 — the ESC·2 pin). The audit-row
    test asserts `Via = Escalation` + the 12 frozen `AccessVia` values are
    unchanged (the ESC·10 pin).
  - `tests/Kumunita.Web.Tests/EscalationTokenPageTests.cs` (new) — **5 tests**,
    one per pinned name (ESC_1_Token_Page_GlobalAdmin_Sees_TokenList through
    ESC_2_Outbound_Config_Save_Persists_Endpoint_Token_Enabled). The token-
    list test asserts the `EscalationTokenSummary` rows are rendered (the
    ESC·3 pin). The generate test asserts the plaintext is shown **once**
    (a `TempData["newToken"]` flash, the `escalation.tokens.show_once` `kw-l`
    key) + a masked row (the ESC·3 pin). The revoke test asserts the token
    is removed from the list (the ESC·9 pin). The non-GlobalAdmin test
    asserts a 403 (the ESC·12 pin). The outbound-config-save test asserts the
    `EscalationOutboundConfig` doc is stored with the `Endpoint` + `Token` +
    `Enabled` fields (the ESC·4 pin).
  - `tests/Kumunita.Web.Tests/EscalationInboundEndpointTests.cs` (new) —
    **7 tests**, one per pinned name (ESC_5_Inbound_Endpoint_ValidToken_
    Creates_Row through ESC_8_Detail_Sees_FromInstance). The valid-token
    inbound test asserts a 200 + the `ErrorReport` row exists with `Origin =
    "escalated"` (the ESC·5 pin). The invalid-token test asserts a **401** +
    no row (the ESC·5 pin). The forwarder DB-config test asserts the
    `Bearer` token header is sent (the ESC·6 pin — the forwarder is stubbed /
    the HTTP asserted). The forwarder no-DB env-var test asserts the
    fallback (the ESC·4 pin). The forwarder no-config test asserts a no-op
    (FACES ESC-6). The received-filter test asserts only `Origin =
    "escalated"` rows are shown (the ESC·8 pin). The detail test asserts
    `FromInstance` is rendered (the ESC·8 pin).
  - `docs/design/esc-escalation-authorization-design.md` (modify) — append
    `### Run result (ESC acceptance gate — <date>)`: the three test names,
    their pass/red status, the 22-test count, and one line per any `## U<m>
    — Drift pause` section in the handoff note (each resolved or still open).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. The 22 tests
  compile + are discovered. The gate section is present and consistent with
  the test results. Handoff note: 4–5 lines starting `## U08 — seam tests
  (22) + gate recorded` — (a) the 4 test file paths, (b) the 22 test names
  (verbatim), (c) the 22 pass/red counts, (d) the three-test gate status
  (closed-loop / handoff / part-vs-whole), (e) any still-open drift.

### U09 — Close: `Milestones.cs` lane row + README/STATUS/ARCHITECTURE parity + ADR 0159 → `Accepted` + `done/esc/` move

- **Goal:** add the `Milestones.cs` **ESC lane row** (as `StatusDone`,
  **after** the M32 row, without disturbing the M33 / M34 order or the
  single-in-progress pin — M34 stays `StatusNext`), append the README Roadmap
  ESC line (the `**Done.** (ADR 0159)` tail), append the `STATUS.md` ESC line,
  append the `ARCHITECTURE.md` `ErrorReports/` ESC line (the lane extension),
  append the `WhatsNew.cs` `0.52.0` entry (newest-first, naming ESC + ADR
  0159), tag the ADR 0159 index row `**Done** (ESC)`, flip ADR 0159 →
  `Accepted`, and move all ESC artifacts to `done/esc/`. **No code change.**
  **Exit: `dotnet build` clean + `Kumunita.Web.Tests` green (the
  `MilestonesTests` + `WhatsNewTests` pins green).**
- **Entry reads (10):**
  1. `docs/plans-milestones/plan-esc-escalation-authorization.md` — the
     register (the §gate, the §drift-guard, the ESC·13 pin).
  2. U08's handoff-note `## U08 — seam tests (22) + gate recorded` section
     (the 22 pass/red counts + the gate status).
  3. `src/Kumunita.Web/Milestones.cs` — the `M32` row (the anchor for the ESC
     lane row) + the `M33` / `M34` rows (the order to preserve).
  4. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the single-in-progress
     pin (M34 stays `StatusNext`) + the done-list to append `"ESC"` to.
  5. `src/Kumunita.Web/WhatsNew.cs` — the `0.52.0` entry to append,
     newest-first.
  6. `README.md` — the ESC Roadmap line to append the `**Done.** (ADR 0159)`
     tail.
  7. `docs/STATUS.md` — the ESC line to append.
  8. `docs/ARCHITECTURE.md` — the `ErrorReports/` line to append (the ESC
     extension).
  9. `docs/adr/0159-escalation-authorization.md` — the ADR 0159 to flip to
     `Accepted` + the index row to tag `**Done** (ESC)`.
  10. `docs/adr/README.md` — the ADR 0159 index row to tag `**Done** (ESC)`.
- **Deliverables (7 files, modify + 1 move):**
  1. **`src/Kumunita.Web/Milestones.cs`** (modify) — add the **ESC lane row**
     (after the M32 row, as `StatusDone`): `new("ESC", "Escalation
     authorization + receiving inbox — a receiving GlobalAdmin generates
     labeled, individually-revocable tokens (shown once, hashed at rest) and
     hands them to origin platforms; an origin GlobalAdmin sets their endpoint
     + token; when they escalate, the receiving platform validates the token
     and only then accepts the issue as an 'escalated' row in the same admin
     list (the M32 ErrorReports surface extended additively — the
     EscalationToken + EscalationOutboundConfig docs + the
     IEscalationTokenService seam + the AcceptInboundAsync seam + the
     POST /escalations/inbound endpoint + the EscalationForwarder Bearer-token
     change + the closed escalation.* kw-l key set × en/de/fr/da)",
     StatusDone)`. The M33 / M34 rows are **unchanged** (the order preserved,
     M34 stays `StatusNext`).
  2. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** (modify) — append
     `"ESC"` to the `Shipped_Milestones_Are_Marked_Done` done-list. The
     single-in-progress pin (M34 stays `StatusNext`) is **unchanged** (the
     ESC row is `StatusDone`, not `StatusNext`).
  3. **`src/Kumunita.Web/WhatsNew.cs`** (modify) — append the `0.52.0` entry
     (newest-first, naming ESC + ADR 0159):
     `new("0.52.0", "2026-10-10", new List<string> { "Escalation
     authorization + receiving inbox — a receiving GlobalAdmin generates
     labeled, individually-revocable tokens (shown once, hashed at rest) and
     hands them to origin platforms; an origin GlobalAdmin sets their endpoint
     + token at /admin/escalation; when they escalate, the receiving platform
     validates the token (SHA-256 compare) and only then accepts the issue as
     an 'escalated' row in the same /admin/error-reports list (a new
     'escalated' chip + a local/received filter + the FromInstance on the
     detail view): the M32 ErrorReports surface extended additively (the
     EscalationToken + EscalationOutboundConfig docs + the
     IEscalationTokenService seam + the AcceptInboundAsync seam + the
     AccessVia.Escalation value + the POST /escalations/inbound public
     token-gated endpoint + the EscalationForwarder Bearer-token change + the
     closed escalation.* kw-l key set × en/de/fr/da) (ADR 0159)." })`.
  4. **`README.md`** (modify) — the ESC Roadmap line: append the `**Done.**
     (ADR 0159)` tail (the M32 `**Done.** (ADR 0155)` shape).
  5. **`docs/STATUS.md`** (modify) — the ESC line: append the `**ESC is
     done** — escalation authorization + receiving inbox (a receiving
     GlobalAdmin generates labeled tokens + an origin GlobalAdmin sets
     endpoint + token + the receiving platform validates the token before
     accepting an issue as an 'escalated' row in the same admin list; the M32
     ErrorReports surface extended additively — the EscalationToken +
     EscalationOutboundConfig docs + the IEscalationTokenService seam + the
     AcceptInboundAsync seam + the AccessVia.Escalation value + the POST
     /escalations/inbound endpoint + the EscalationForwarder Bearer-token
     change + the closed escalation.* kw-l key set; ADR 0159)` line (the M32
     shape).
  6. **`docs/ARCHITECTURE.md`** (modify) — the `ErrorReports/` line: append
     the `**ErrorReports/** (ESC extension) — the ESC escalation
     authorization + receiving inbox lane (ADR 0159): the
     EscalationToken + EscalationOutboundConfig docs + the
     IEscalationTokenService seam (the ListAsync / GenerateAsync /
     RevokeAsync / ValidateAsync read + write lanes) + the AcceptInboundAsync
     seam on IErrorReportService + the AccessVia.Escalation additive value +
     the four additive ErrorReport fields (the FromInstance /
     EscalationReceivedAt / EscalationSourceId / EscalationTokenId — the 19-
     member ESC ceiling) + the Origin "escalated" value + the
     EscalationDocTypes registration surface` line (the M32 `ErrorReports/`
     shape, extended).
  7. **`docs/adr/0159-escalation-authorization.md`** (modify) — the ADR 0159:
     flip `Status: Draft` → `Status: Accepted` + the `docs/adr/README.md`
     index row: tag the `0159` row `**Done** (ESC)` (the M32 `0155` row
     shape). **Plus** the `done/esc/` move: `git mv
     docs/plans-milestones/plan-esc-escalation-authorization.md
     docs/plans-milestones/done/esc/` + `git mv
     docs/plans-milestones/in-progress/esc-uNN.md
     docs/plans-milestones/done/esc/esc-uNN.md` (for each U00–U08 unit plan)
     + `git mv docs/plans-milestones/in-progress/esc-handoff-notes.md
     docs/plans-milestones/done/esc/esc-handoff-notes.md` (the `done/m32/`
     subfolder convention).
- **Exit:**
  - `dotnet build Kumunita.slnx -c Debug` green.
  - `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.
    Tests.dll` green (the `MilestonesTests` + `WhatsNewTests` pins green —
    the order + single-in-progress pin is intact [M34 stays `StatusNext`],
    the new `0.52.0` entry is present, newest-first).
  - The `Milestones.cs` ESC row is `StatusDone` (after the M32 row). The M33
    / M34 order is **unchanged**. The README / `STATUS.md` /
    `ARCHITECTURE.md` parity is held. The ADR 0159 is `Accepted` + the index
    row is tagged `**Done** (ESC)`. The `done/esc/` subfolder is present (the
    register + the unit plans + the handoff notes).
  - Handoff note: a `## U09 — close` section — (a) the `Milestones.cs` ESC
    lane row (the `StatusDone` position after M32), (b) the `MilestonesTests`
    done-list append (`"ESC"`) + the single-in-progress pin unchanged (M34
    stays `StatusNext`), (c) the `WhatsNew.cs` `0.52.0` entry (newest-first),
    (d) the README / `STATUS.md` / `ARCHITECTURE.md` parity (the three line
    appends), (e) the ADR 0159 `Accepted` + the index row `**Done** (ESC)`,
    (f) the `done/esc/` move (the `git mv` commands).
