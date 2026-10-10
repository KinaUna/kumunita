# ADR 0159 — Escalation authorization + receiving inbox (a token-gated acceptance lane + the receiving inbox + the outbound config page on the M32 ErrorReports surface)

Status: Draft
Date: 2026-10-10

## Context

M32 (ADR 0155) closed the **escalation** end of the error-report lifecycle: an
admin on an origin instance can forward a report to the
`KUMUNITA_ESCALATION_ENDPOINT` env-var URL (the `POST …/escalate` action → the
Web-layer `IEscalationForwarder` POSTs the 8-field payload — never persisted —
and a successful forward stamps `resolved`). But that forward is
**unauthenticated and blind at three ends**:

1. **The receiving platform has no surface to see the report.** M32's forward
   is fire-and-forget to an env-var URL; there is no receiving-side
   `ErrorReport` row, no receiving-side admin list entry, no way to read
   *what* arrived. The receiving platform is blind to its own inbox.
2. **The receiving platform has no way to say *which* platform it came
   from.** The forwarded payload carries the origin's fields, but there is no
   origin-platform identity tied to the report, and no authorization over
   *who* may push reports into the receiving queue.
3. **The receiving platform cannot stop an untrusted instance.** Because the
   forward is unauthenticated, any instance pointed at the receiving URL can
   dump arbitrary reports into its queue — there is no token, no allowlist,
   no revocable credential.

ESC (the `ESC` named lane — **not** a roadmap renumber; the M33 / M34 order is
untouched, the ADR 013 / 089 / 093 / 109 "named lane, not a renumber"
precedent) closes all three ends. The user's words, verbatim: "Issues
escalation should have a page where admins can view locally submitted issues,
close them or escalate them (if the instance is configured for escalation)
and a page to generate or use a token / access code for client platforms to
use for escalation. So only an authorized platform can escalate to another
platform. The admin of the receiving platform for escalation tells the admin
of the origin platform the access code / token so they can add it to their
platform setup, and when an admin escalates an issue it is only accepted by
the receiving platform if it has a valid token."

## Decision

- **ESC is a capability on the M31 / M32 `ErrorReports` surface, not a new
  context** (D1, ESC·1). ESC **reuses** the M32 `ErrorReport` doc + the
  `Kumunita.Core.ErrorReports` context + the `IErrorReportService` seam + the
  M32 `EscalationForwarder` + the `KUMUNITA_ESCALATION_ENDPOINT` env var and
  **extends them additively** (ADR 0004 §B.1). **No new bounded context** —
  the two new docs (`EscalationToken`, `EscalationOutboundConfig`) ride a
  **new** `EscalationDocTypes` parallel registration surface (the M31
  `ErrorReportDocTypes` / the Usage `UsageDocTypes` precedent), and the four
  additive `ErrorReport` fields ride the *existing*
  `.Schema.For<ErrorReport>()` (ADR 0004 §B.1 idempotent delta at boot, **no
  new surface for the additive fields**).
- **The `ErrorReport` doc field set is extended additively to a 19-member
  ceiling** (D2, ESC·2). M32's 15 (ADR 0155 D3) are **unchanged** (exact
  names, types, nullability); ESC adds **exactly four**, each null for
  locally-filed rows: `FromInstance` (`string?`, the accepting token's
  `Label`), `EscalationReceivedAt` (`DateTimeOffset?`, the receive instant),
  `EscalationSourceId` (`string?`, the origin's report id — the inbound
  idempotency key), `EscalationTokenId` (`string?`, which token accepted it).
  No field outside the 19-member set may appear in the doc. The `Origin`
  closed set extends from `{"error-page","general"}` to
  `{"error-page","general","escalated"}` (a string field — ADR 0004 §B.1, no
  migration). **No EF migration.**
- **Receiving-side tokens are the `EscalationToken` doc: multiple, labeled,
  individually revocable, hashed at rest** (D3, ESC·3). Only the **SHA-256
  hash** is stored — the **plaintext is shown exactly once** at generation,
  never persisted. Tokens are **CSPRNG-generated** (`RandomNumberGenerator`,
  the `IdentityService` / `PortabilityApplyIdentity` precedent), **32 random
  bytes base64url**, `kesc_`-prefixed (a `ghp_`-style high-entropy format —
  256 bits). The closed member set (9): `Id` · `Label` · `TokenHash` ·
  `TokenPrefix` · `Created` · `CreatedBy` · `LastUsedAt?` · `RevokedAt?` ·
  `RevokedBy?`.
- **Origin-side outbound config is the `EscalationOutboundConfig` doc,
  admin-managed** (D4, ESC·4). A singleton row (one per instance): `Id` ·
  `Endpoint` · `Token` (the plaintext the admin was handed — the *outgoing*
  secret the origin admin chose to keep, **not** a secret of the *receiving*
  platform) · `Enabled` (default `false`) · `Updated` · `UpdatedBy`. The M32·6
  env var `KUMUNITA_ESCALATION_ENDPOINT` remains as the **unauthenticated
  operator-override / fallback**: when the DB config is **absent/disabled**
  the forwarder falls back to the env-var endpoint (endpoint only, **no**
  token — the unauthenticated M32-style forward, backward compat); when the DB
  config is **present**, it is primary and the token is sent. This is a
  **deliberate evolution** of the M32·6 "env var, never persisted" pin — that
  pin was the M32-era *operator-config* channel; ESC supersedes it for the
  *admin-managed, token-authenticated* channel.
- **The `IEscalationTokenService` seam** (D5, ESC·3 / ESC·9). `ListAsync` /
  `GenerateAsync` / `RevokeAsync` / `ValidateAsync` (the exact C# shape is the
  design doc §2.1.1 pin) + the `EscalationTokenSummary` read-model record
  (never the doc; no plaintext / no hash). `GenerateAsync` + `RevokeAsync` are
  **single-write-lane** (the ADR 0006 C3 shape, the M32·8 precedent — one write
  session storing the doc + exactly one `AccessAudit` row, `Via = Admin`,
  action `escalation.token.generate` / `escalation.token.revoke`,
  `TargetKind` "escalation-token"). `ValidateAsync` is a constant-time hash
  compare that stamps `LastUsedAt` on a match (a read-then-write, **no** audit
  row — the M31·4 "read is not an access decision" precedent). `Revoke` is
  **immediate** (after revocation the token no longer validates) and
  **idempotent** (a no-op — no audit row, no state change — when the token is
  missing or already revoked).
- **The `AcceptInboundAsync` seam on `IErrorReportService`** (D6, ESC·5 /
  ESC·7). The M31/M32 4-method surface gains a **5th** method (the surface is
  **5 methods** in ESC — the M31 3 + the M32 1 + the ESC 1; the M31/M32 four
  are **unchanged**, ESC·1). `InboundReport` is a **separate** record (not a
  re-shape of `ErrorReportDraft`); `InboundResult` carries `Created` / `Row` /
  `Error?`. The impl is the M32 `MarkResolvedAsync` **idempotent-write-lane**
  shape (ADR 0006 C3 single-write-lane): validate the token → look up an
  existing row on `(EscalationTokenId, EscalationSourceId)` (the **ESC·7**
  idempotency key — a re-delivery returns the existing row, no duplicate, no
  second audit row) → else store a new `ErrorReport` (`Origin = "escalated"`)
  + **one** `AccessAudit` row (`Via = Escalation`, action
  `errorreport.inbound`, `TargetKind` "error-report") in **one** write session
  + stamp the token's `LastUsedAt`. An **invalid / revoked** token → `Error`
  non-null, **no** row, **no** audit row (the Web endpoint maps this to a
  401).
- **The inbound acceptance endpoint is public + token-gated** (D7, ESC·5 /
  ESC·6). `POST /escalations/inbound` is **not** inside a session `[Authorize]`
  gate (it is machine-to-machine; the token is the credential — the M31·2 /
  M32·4 "public, anonymous-safe" precedent applied to the M2M surface). It
  reads the `Authorization: Bearer <token>` header (ESC·6), builds the
  `InboundReport`, and calls `AcceptInboundAsync`. On **valid** → an
  `Origin = "escalated"` row + one `AccessAudit` row (200); on **invalid /
  revoked** → **401**, no row.
- **The token is presented + verified as `Authorization: Bearer <token>` in
  both directions; Core stays HTTP-free** (D8, ESC·6). The M32
  `EscalationForwarder` (Web-layer) `ForwardAsync` **signature is unchanged**
  (ESC·6); its impl is **extended** to read the DB `EscalationOutboundConfig`
  first (if present + enabled → send the `Bearer` token, `ConfiguredSource =
  "db"`), fall back to the M32·6 env var (endpoint only, no token,
  `ConfiguredSource = "env"`), or no-op when neither (the M32 no-op, FACES
  ESC-6). The **8-field payload is unchanged** (the M32·1 pin). The
  `EscalationResult` gains **one additive** member: `string?
  ConfiguredSource` (`"db"` / `"env"` / `null`) — the M32 4 members are
  **unchanged**. The **validation** (hash compare) + the **received-row
  write** are Core; the **HTTP** (outbound POST + inbound request parsing) is
  Web (ADR 0006-D — Core stays HTTP-free).
- **Received issues surface in the same admin list** (D9, ESC·8). The M31·4
  `/admin/error-reports` list is **unchanged** in shape; ESC adds the
  `Origin = "escalated"` chip + a `?filter=received|local` selector (the
  default shows all). The detail view shows `FromInstance` +
  `EscalationReceivedAt` for received rows. **No** separate receiving-inbox
  surface. The existing five admin actions (`Index` / `MarkTriaged` / `Detail`
  / `Resolve` / `Escalate`) are **unchanged** (ESC·1 — additive-only).
- **`AccessVia` gains exactly one additive value: `Escalation`** (D10,
  ESC·10). The M1 `Admin` / ADR 0013 `Group` / ADR 0028 `Guardian` / ADR 0036
  `Community` / ADR 0041 `Resident` / M31 `Anonymous` append precedent — the
  **12 frozen values are untouched**. The inbound audit row is the **only**
  row that carries `Via = Escalation` (the M2M standing — the receiving admin
  is not the actor, the origin platform is).
- **The two new admin surfaces are GlobalAdmin-gated; no new authz surface**
  (D11, ESC·12). `/admin/escalation` (outbound config) +
  `/admin/escalation/tokens` (receiving tokens) are
  `[Authorize(Roles = GlobalAdmin)]` (the M31·4 / M31·9 / M32·10 precedent —
  the ADR 0001-B thin-token rule). **No new `AccessAction`, no new `Decide()`
  branch, no new `IAuthorizationService` surface.** The inbound endpoint is
  **public** (token-gated, ESC·5).
- **The closed `escalation.*` `kw-l` key set is parity-pinned in four
  languages** (D12, ESC·11). **~24 keys** (the design doc §2.3 table), each
  present, **non-empty, in all four** languages (en/de/fr/da), pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` (the
  M31·7 / M32·9 precedent). The `en` values are the source text (the ADR 0015
  D1 `kw-l` provider-floor discipline). The M31 / M32 `errorreport.*` /
  `issue.*` key sets are the **floor** — **untouched** (ESC·1).
- **The six-member close flip is the lane's close unit (ESC·13)** — the
  `Milestones.cs` lane row (added `StatusDone`, after the M32 row, without
  disturbing the M33 / M34 order or the single-in-progress pin — M34 stays
  `StatusNext`) / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` (a new entry, newest-first, naming ESC
  + ADR 0159). The M27 "shipped with no entry until caught in review" lesson
  (AGENTS.md) is held.

## Consequences

- **Escalation is now token-authorized** — only a platform holding a valid,
  non-revoked token the receiving admin issued is accepted as an
  `Origin = "escalated"` row. An untrusted instance can no longer dump
  arbitrary reports into the receiving queue (the ESC·5 / ESC·9 closure).
- **The receiving platform now sees its inbox** — received issues land in the
  **same** `/admin/error-reports` list, distinguished by an `escalated` chip +
  a local/received filter + the origin's `FromInstance` on the detail (ESC·8).
- **The origin admin now configures their endpoint + token** — the
  `EscalationOutboundConfig` singleton on the `/admin/escalation` page (ESC·4);
  the forwarder sends the token as a `Bearer` header when the DB config is
  present, and falls back to the unauthenticated env-var forward when it is
  absent (the M32 backward-compat path preserved, ESC·4).
- **The `ErrorReport` doc is the 19-member ESC ceiling** — the M32 15
  unchanged + the ESC 4 additive (ESC·2). **No EF migration** (the additive
  fields + the `Origin` "escalated" value are a string / nullable string /
  `DateTimeOffset?` on an existing doc — ADR 0004 §B.1 idempotent delta at
  boot).
- **One new Core service** (`IEscalationTokenService` /
  `EscalationTokenService`) + **one new registration surface**
  (`EscalationDocTypes`) + **one additive `IErrorReportService` seam**
  (`AcceptInboundAsync`) + **one additive `AccessVia` value**
  (`Escalation`). **`Kumunita.Core` stays HTTP-free** (ESC·6, ADR 0006-D);
  the forwarder + the inbound endpoint are **Web-layer**.
- **The receiving-side token plaintext is never persisted** (ESC·3) — only the
  SHA-256 hash is stored; the plaintext is shown exactly once at generation.
  The **origin-side** token **is** stored (the *outgoing* secret the origin
  admin chose to keep, on the `EscalationOutboundConfig` doc — the D4 pin).
- **`No new `AccessAction` / `Decide()` branch / `IAuthorizationService`
  surface** (ESC·12) — the admin surface is the standard
  `[Authorize(Roles = GlobalAdmin)]` gate; the inbound endpoint is **public**
  (token-gated).
- **The 12 frozen `AccessVia` values are untouched** (ESC·10) — `Escalation`
  is the only additive value; the inbound audit row is the only row that
  carries it.
- **No roadmap letter moves** — ESC is a named lane; M33 / M34 are untouched
  (the M34 single-in-progress pin is preserved).
- **Named deferrals (a future lane, if it comes):** the **inbound rate
  limiter** (tokens are 256-bit CSPRNG — brute-force is infeasible; a
  per-token / per-IP throttle may be added later as hardening) · the
  **outbound retry queue** (a failed forward is a no-op, the M32·7 precedent)
  · the **escalation reply lane** (the receiving admin's resolution of a
  received issue flowing *back* to the origin — M32 already ships a local
  resolve; a future lane may pipe the resolution back over the token) · the
  **per-token per-origin-report allowlist** (a token authorizes a platform,
  not specific reports).
