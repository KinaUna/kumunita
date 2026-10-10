# ESC — Escalation authorization + receiving inbox (design doc)

> **Abstract:** This design closes the **authorization + receiving** gap that
> M32 left open — M32 made escalation *possible* (a GlobalAdmin forwards a
> report to the `KUMUNITA_ESCALATION_ENDPOINT` env-var URL) but the forward
> is **unauthenticated and blind**: the receiving platform has no surface to
> see the report, no way to say *which* platform it came from, and no way to
> stop an untrusted instance from dumping arbitrary reports into its queue.
> ESC closes that: the receiving admin generates **labeled tokens** (one per
> authorized origin platform, each individually revocable, SHA-256-hashed at
> rest, shown once) and hands the plaintext to the origin admin out-of-band;
> the origin admin records their endpoint + token on the **outbound config
> page**; when they escalate, the forwarder presents the token as a `Bearer`
> header; the receiving platform's **inbound endpoint** validates the token
> (SHA-256 compare) and, only on a match, creates an `Origin = "escalated"`
> `ErrorReport` row + one `AccessAudit` row (`Via = Escalation`). The
> receiving admin sees received issues in the **same** `/admin/error-reports`
> list (a new `escalated` chip + a local/received filter + the origin's
> `FromInstance` on the detail). The one contract it creates is **additive on
> the M31 / M32 `ErrorReports` surface**: the two new docs (`EscalationToken`
> + `EscalationOutboundConfig`), the `IEscalationTokenService` seam, the
> `AcceptInboundAsync` seam (the 5th method on `IErrorReportService`), the
> four additive `ErrorReport` fields (the 19-member ESC ceiling), the
> `AccessVia.Escalation` value, and the closed `escalation.*` `kw-l` key set.
> Out of scope: the inbound rate limiter, the outbound retry queue, the
> escalation reply lane, the per-token allowlist, and the six-member close
> flip (U09's).

> **Lane ESC — Escalation authorization + receiving inbox.** ESC is a
> **named lane** (the `ESC` id — **not** a roadmap renumber; the M33 / M34
> order is untouched, the ADR 013 / 089 / 093 / 109 "named lane, not a
> renumber" precedent). It is a **capability on the M31 / M32 `ErrorReports`
> surface** (ADR 0154 + ADR 0155): it **reuses** the M32 `ErrorReport` doc +
> the `Kumunita.Core.ErrorReports` context + the `IErrorReportService` seam
> + the M32 `EscalationForwarder` + the `KUMUNITA_ESCALATION_ENDPOINT` env
> var and **extends them additively** (the two new docs + the
> `IEscalationTokenService` seam + the `AcceptInboundAsync` seam + the four
> additive fields + the `Origin` "escalated" value + the
> `AccessVia.Escalation` value). ESC ships **four lanes** the M32 register
> named as its deferrals: **token authorization** (the `EscalationToken` doc
> + the `IEscalationTokenService` seam, ESC·3), **the receiving inbox** (the
> `Origin = "escalated"` rows + the received filter / chip / detail fields,
> ESC·8), **the outbound config page** (the `EscalationOutboundConfig` doc +
> the `/admin/escalation` surface, ESC·4), and **the token-gated inbound
> endpoint** (the `POST /escalations/inbound` endpoint + the forwarder
> `Bearer` change, ESC·5 / ESC·6).
>
> **Three-tier contract.** This file is the **primary** tier of ESC's
> contract: it pins the **invariants (ESC·1–ESC·13)**, the **FACES
> (ESC-1–ESC-10)**, and (in Part 2) the exact `EscalationToken` +
> `EscalationOutboundConfig` doc shapes, the `IEscalationTokenService` seam,
> the `AcceptInboundAsync` seam, the four additive `ErrorReport` fields, the
> `EscalationForwarder` token + DB-config change, the closed `escalation.*`
> `kw-l` key set, the pinned seam-test names, the acceptance gate, and the
> drift guard. The register
> (`docs/plans-milestones/plan-esc-escalation-authorization.md`) is the
> **secondary** tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/esc-handoff-notes.md` is the **scratch**
> tier (one short section per unit, appended, never rewritten). When the
> three disagree, **this file wins for the pinned shapes**; the register wins
> for *which files exist* and *what each unit does*.
>
> **Part 1 (this file, U01):** the context, the scope (In / Out, incl. the
> named deferrals), the **thirteen invariants** (ESC·1–ESC·13), and the
> **ten FACES** (ESC-1–ESC-10), plus the frozen-base assumptions.
> **Part 2 (U02):** the seams & contracts (the exact `EscalationToken` +
> `EscalationOutboundConfig` doc shapes, the `IEscalationTokenService` seam,
> the `AcceptInboundAsync` seam, the four additive `ErrorReport` fields, the
> `EscalationForwarder` token + DB-config change, the closed `escalation.*`
> `kw-l` key set, the pinned test names, the acceptance gate, the drift guard)
> + **ADR 0159**.
>
> **The frozen base (reused, unchanged).** ESC is built **on top of** the
> M32 `ErrorReports` surface (ADR 0155 — the `ErrorReport` 15-member doc, the
> `IErrorReportService` 4-method surface, the `ErrorReportDocTypes`
> registration surface, the M32 `EscalationForwarder`, and the
> `KUMUNITA_ESCALATION_ENDPOINT` env-var fallback) and the M31 `ErrorReports`
> surface (ADR 0154). It also rides the ADR 0004 §B.1 Marten-native /
> additive-field pattern (the ESC four additive fields **ride** the existing
> `.Schema.For<ErrorReport>()` — the idempotent delta at boot, **no new
> registration surface for the additive fields**; the two new docs ride a
> **new** `EscalationDocTypes` parallel surface), the ADR 0001-B thin-token
> rule (the admin surface is `[Authorize(Roles = GlobalAdmin)]`), the ADR
> 0006-C3 single-write-lane rule (each token generate / revoke and each inbound
> acceptance commits its doc + exactly one `AccessAudit` row together), and
> the ADR 0006-D dependency-direction rule (**`Kumunita.Core` references no
> ASP.NET HTTP types** — the forwarder + the inbound endpoint are
> **Web-layer**). All of these still bind **unchanged**. ESC adds **two new
> docs** (`EscalationToken`, `EscalationOutboundConfig` — ESC·3 / ESC·4),
> **one new service** (`IEscalationTokenService`, ESC·3 / ESC·9), **one new
> registration surface** (`EscalationDocTypes`, ESC·1), **four additive
> `ErrorReport` fields** + the `Origin` "escalated" value (the 19-member ESC
> ceiling, ESC·2), **one additive `IErrorReportService` seam**
> (`AcceptInboundAsync`, the 5th method — ESC·5 / ESC·7), **one additive
> `AccessVia` value** (`Escalation`, ESC·10), **two admin surfaces**
> (`/admin/escalation` + `/admin/escalation/tokens`, ESC·12), **one
> token-gated inbound endpoint** (`POST /escalations/inbound`, ESC·5 / ESC·6 /
> ESC·7), and the **closed `escalation.*` `kw-l` key set** × en/de/fr/da
> (ESC·11) — but it adds **no** re-shape of the M32 15 `ErrorReport` fields
> (ESC·2), **no** re-shape of the M31 / M32 4-method surface (ESC·1), **no**
> re-shape of the M32 `EscalationForwarder` `ForwardAsync` signature (ESC·6),
> **no** re-shape of the 12 frozen `AccessVia` values (ESC·10), **no** new
> bounded context (ESC·1), **no** new `AccessAction` / `Decide()` branch /
> `IAuthorizationService` surface (ESC·12), and **no** persistence of the
> receiving-side token plaintext (ESC·3). It is **additive**. **No EF
> migration** (the additive fields are a string / nullable string /
> `DateTimeOffset?` on an existing doc — ADR 0004 §B.1 idempotent delta at
> boot).
>
> **The `ErrorReport` field set ceiling is re-pinned** (the ESC 19-member set
> — the M32 15 + the ESC 4 additive — pinned by the register's Assumptions and
> re-pinned in Part 2 §2.2; no field outside the set may appear in the doc,
> and no M32 field is re-shaped). **The `Origin` closed set is extended
> additively** from `{"error-page", "general"}` to
> `{"error-page", "general", "escalated"}` in ESC — the two M31 / M32 values
> keep their exact strings, and `"escalated"` marks a received (not
> locally-filed) report. **The M31 / M32 `errorreport.*` / `issue.*` `kw-l`
> key set is the floor** — ESC **adds** the new `escalation.*` namespace, it
> does not re-author the M31 / M32 sets (Part 2 §2.3 pins the exact ESC set).
>
> **The one thing every unit must respect:** ESC is **token authorization +
> the receiving inbox + the outbound config page** on the M31 / M32
> `ErrorReports` surface (ESC·1). The receiving admin generates a **labeled
> token** (the `EscalationToken` doc — multiple, individually revocable,
> SHA-256-hashed at rest, the plaintext shown once + never persisted, ESC·3)
> and hands it out-of-band to the origin admin, who records their endpoint +
> token on the **outbound config page** (the `EscalationOutboundConfig`
> singleton, ESC·4). When the origin admin escalates, the Web-layer
> `EscalationForwarder` presents the token as a `Bearer` header (ESC·6); the
> receiving **inbound endpoint** validates it (Core stays HTTP-free, ESC·6)
> and, only on a valid non-revoked token, creates an `Origin = "escalated"`
> `ErrorReport` row + one `AccessAudit` row (`Via = Escalation`) idempotently
> on `(token, origin-report-id)` (ESC·5 / ESC·7); a **revoked** token is
> invalid (ESC·9). The receiving admin sees received issues in the **same**
> `/admin/error-reports` list (the `escalated` chip + a local/received filter
> + the `FromInstance` on the detail, ESC·8). Every new user-visible string is
> a **closed `escalation.*` key in four languages** (ESC·11). The admin
> token/config surface is **GlobalAdmin-gated** with **no new authz surface**
> (ESC·12). The `KUMUNITA_ESCALATION_ENDPOINT` env var remains the
> **unauthenticated operator-override fallback** when the DB config is absent
> or disabled (ESC·4). `AccessVia` gains **exactly one** additive value
> (`Escalation`) and the 12 frozen values are untouched (ESC·10). The
> `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
> `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip is **U09's**
> (ESC·13). There is **no new bounded context** (ESC·1).

## Context

M32 closed the **escalation** end of the error-report lifecycle: an admin on
an origin instance can forward a report to a configurable endpoint (the
`POST …/escalate` action → the Web-layer `IEscalationForwarder` POSTs the
8-field payload to the `KUMUNITA_ESCALATION_ENDPOINT` env-var URL — never
persisted — and a successful forward stamps `resolved`). But that forward is
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

ESC closes all three ends with a **capability on the M31 / M32 surface**
(ESC·1): (1) the escalation becomes **token-authenticated** — only a platform
holding a valid token the receiving admin issued can be accepted (the
`EscalationToken` doc + `IEscalationTokenService` + the `POST
/escalations/inbound` token-gated endpoint, ESC·3 / ESC·5); (2) a **receiving
inbox** — accepted reports land as `Origin = "escalated"` rows in the *same*
`/admin/error-reports` list, distinguished by an `escalated` chip + a
local/received filter + the origin's `FromInstance` on the detail (the
`AcceptInboundAsync` seam + the four additive `ErrorReport` fields,
ESC·2 / ESC·8); and (3) a **token page** (the receiving admin generates,
lists, and revokes labeled tokens — shown once, hashed at rest) + an
**outbound config page** (the origin admin sets their endpoint + token — the
`EscalationOutboundConfig` doc), with the `EscalationForwarder` extended to
send the token as a `Bearer` header and read the DB config first (ESC·4 /
ESC·6).

The boundary with M31 / M32 is explicit and pinned: **M31 is intake +
triage**, **M32 is resolution + escalation forwarding** (the resident may
file a general issue, the admin fixes it locally or forwards it to the
operator's env-var endpoint); **ESC is escalation authorization + the
receiving inbox** (the receiving admin issues revocable tokens, the origin
admin configures their endpoint + token, and only an authorized forward is
accepted as an `Origin = "escalated"` row). ESC **reuses** the M31 / M32
`ErrorReport` doc + `IErrorReportService` seam + `EscalationForwarder` +
`KUMUNITA_ESCALATION_ENDPOINT` env var and **extends them additively**; it
**reuses** the M31 / M32 `errorreport.*` / `issue.*` `kw-l` key set and
**adds** the new `escalation.*` namespace. ESC does **not** re-shape any M31
/ M32 field, any M31 / M32 method, any M31 / M32 key, or the M32
`EscalationForwarder` `ForwardAsync` signature; it does **not** add an inbound
rate limiter, an outbound retry queue, an escalation reply lane, or a
per-token allowlist (the named deferrals, §Scope). The
`KUMUNITA_ESCALATION_ENDPOINT` env var is **preserved** as the
unauthenticated operator-override fallback for the *forwarding* side when the
admin-managed DB config is absent or disabled (ESC·4 — a deliberate evolution
of the M32·6 "env var, never persisted" pin, which was the M32-era *operator
config* channel; ESC supersedes it for the *admin-managed,
token-authenticated* channel).

## Scope

**In (ESC's closed surface):**

- the **two new docs on the `ErrorReports` context** — the
  **`EscalationToken`** doc (the ESC·3 9-member ceiling: `Id` / `Label` /
  `TokenHash` / `TokenPrefix` / `Created` / `CreatedBy` / `LastUsedAt?` /
  `RevokedAt?` / `RevokedBy?`; the plaintext is **never** a member) + the
  **`EscalationOutboundConfig`** doc (the ESC·4 6-member singleton: `Id` /
  `Endpoint` / `Token` / `Enabled` / `Updated` / `UpdatedBy`) — both on a
  **new** `EscalationDocTypes` registration surface (the ESC·1 pin; the
  Usage `UsageDocTypes` parallel-surface precedent);
- the **`IEscalationTokenService`** seam (the ESC·3 / ESC·9 `ListAsync` /
  `GenerateAsync` / `RevokeAsync` / `ValidateAsync` read + write lanes — the
  9-member `EscalationTokenSummary` read-model, never the doc);
- the **`EscalationDocTypes`** registration surface (the ESC·1 new parallel
  surface for the two new docs);
- the **four additive `ErrorReport` fields** (`FromInstance?` /
  `EscalationReceivedAt?` / `EscalationSourceId?` / `EscalationTokenId?`) +
  the **additive `Origin` value** `"escalated"` — the **19-member ESC
  ceiling** (the M32 15 unchanged + the ESC 4 additive; ESC·2);
- the **`AcceptInboundAsync`** seam on `IErrorReportService` (the ESC·5 /
  ESC·7 idempotent inbound write lane — the service surface is **5 methods**
  in ESC: the M31 3 + the M32 1 + the ESC 1; the `InboundReport` record is a
  **separate** record, not a re-shape of `ErrorReportDraft`);
- the **`AccessVia.Escalation`** additive value (the ESC·10 pin — the 13th
  value, the 12 frozen values untouched);
- the **`/admin/escalation`** outbound-config admin surface (the ESC·4
  endpoint + token + enabled) + the **`/admin/escalation/tokens`**
  receiving-token admin surface (the ESC·3 generate / list / revoke) — both
  `[Authorize(Roles = GlobalAdmin)]` (ESC·12);
- the **`POST /escalations/inbound`** public token-gated endpoint (the ESC·5 /
  ESC·6 / ESC·7 acceptance — valid → an `Origin = "escalated"` row + one
  `AccessAudit` row; invalid / revoked → **401**, no row);
- the **`EscalationForwarder`** change (the ESC·6 `Bearer` token header + the
  ESC·4 DB-config read + the env-var fallback; the `ForwardAsync` signature
  is **unchanged** and the 8-field payload is **unchanged**);
- the **received filter + `escalated` chip** on the existing
  `/admin/error-reports` list + the **`FromInstance` / `EscalationReceivedAt`**
  detail-view fields (the ESC·8 pin — the existing five admin actions are
  **unchanged**, extended additively);
- the **closed `escalation.*` `kw-l` key set** × en/de/fr/da (~24 keys, the
  ESC·11 pin — authored by U07, consumed by U05's pages + U06's list / detail
  + the inbound flashes);
  and
- the **test pins** (the `EscalationTokenTests` + `EscalationInboundTests`
  Core classes + the `EscalationTokenPageTests` + `EscalationInboundEndpoint-
  Tests` Web classes — the pinned names, Part 2 §2.4).

**Out (named deferrals):**

- **The inbound rate limiter** — tokens are 256-bit CSPRNG, so brute-force
  validation is infeasible; a per-token / per-IP throttle may be added later
  as hardening.
- **The outbound retry queue** — a failed forward is a no-op (the M32·7
  precedent). M32 ships a **single HTTP POST with the report payload**, no
  retry queue; ESC preserves that.
- **The escalation reply lane** — the receiving admin's resolution of a
  received issue flowing *back* to the origin. M32 already ships a local
  resolve; a future lane may pipe the resolution back over the token.
- **The per-token per-origin-report allowlist** — a token authorizes a
  platform, not specific reports; a per-report allowlist may be added later.
- **The `Milestones.cs` / README / `MilestonesTests` trio** until the lane
  *ships* — U09 owns the six-member close flip (the `WhatsNew.cs` entry is
  appended by U09, not by an earlier unit; ESC·13).

## Invariants (pinned for ESC)

- **ESC·1 — ESC is a capability on the M31 / M32 surface, not a new context.**
  The two new docs (`EscalationToken`, `EscalationOutboundConfig`) live in
  the `Kumunita.Core.ErrorReports` context (the ADR 0154 / 0155 surface),
  registered on a **new** `EscalationDocTypes` surface (the M31
  `ErrorReportDocTypes` / the Usage `UsageDocTypes` parallel-surface
  precedent). ESC **reuses** the M32 `ErrorReport` doc + `IErrorReportService`
  and **extends** them additively; it adds **no new bounded context**.
- **ESC·2 — The `ErrorReport` doc field set is extended additively to a
  19-member ESC ceiling.** M32's 15 (ADR 0155 D1) are unchanged; ESC adds
  **exactly four**: `FromInstance` (string?, the accepting token's `Label`),
  `EscalationReceivedAt` (DateTimeOffset?, the receive instant),
  `EscalationSourceId` (string?, the origin's report id — the inbound
  idempotency key), `EscalationTokenId` (string?, which token accepted it) —
  each null for locally-filed rows. The `Origin` closed set extends from
  `{"error-page","general"}` to `{"error-page","general","escalated"}` (a
  string field — ADR 0004 §B.1 idempotent delta, **no migration**).
- **ESC·3 — Receiving-side tokens are the `EscalationToken` doc: multiple,
  labeled, individually revocable, hashed at rest.** Only the **SHA-256 hash**
  is stored — the **plaintext is shown exactly once** at generation, never
  persisted. Tokens are **CSPRNG-generated** (`RandomNumberGenerator`), **32
  random bytes base64url**, `kesc_`-prefixed (a `ghp_`-style high-entropy
  format — 256 bits).
- **ESC·4 — Origin-side outbound config is the `EscalationOutboundConfig`
  doc, admin-managed.** A singleton row (one per instance): `Endpoint` /
  `Token` (the plaintext the admin was handed — the *outgoing* secret) /
  `Enabled` / `Updated` / `UpdatedBy`. The M32·6 env var
  `KUMUNITA_ESCALATION_ENDPOINT` remains as an **unauthenticated operator-
  override / fallback**: when the DB config is absent/disabled the forwarder
  falls back to the env-var endpoint (endpoint only, **no** token); when
  present, the DB config is primary and the token is sent.
- **ESC·5 — The inbound acceptance endpoint is public + token-gated.**
  `POST /escalations/inbound` is **not** inside a session `[Authorize]` gate
  (it is machine-to-machine; the token is the credential). It validates the
  presented token (SHA-256 compare vs the stored hash); on **valid** → creates
  an `Origin = "escalated"` `ErrorReport` row + **one** `AccessAudit` row
  (`Via = Escalation`, action `errorreport.inbound`); on **invalid** →
  **401**, **no** row, **no** audit row. A **revoked** token is invalid
  (ESC·9).
- **ESC·6 — The token is presented + verified as
  `Authorization: Bearer <token>` in both directions; Core stays HTTP-free.**
  The M32 `EscalationForwarder` (Web-layer) sends the outbound config's token
  as a `Bearer` header; the inbound endpoint (Web-layer) reads it. The
  **validation** (hash compare) + the **received-row write** are Core
  (`IEscalationTokenService.ValidateAsync` + `IErrorReportService.
  AcceptInboundAsync`); the **HTTP** (outbound POST + inbound request
  parsing) is Web (M32·5 / ADR 0006-D — Core stays HTTP-free).
- **ESC·7 — A successful inbound is idempotent on (token, origin-report-id).**
  A re-delivery of the **same** origin report under the **same** token returns
  the **existing** row (200, idempotent), not a duplicate. The key is
  `(EscalationTokenId, EscalationSourceId)`.
- **ESC·8 — Received issues surface in the same admin list.** The M31·4
  `/admin/error-reports` list is **unchanged** in shape; ESC adds the
  `Origin = "escalated"` chip + a `?filter=received|local` selector (the
  default shows all). The detail view shows `FromInstance` +
  `EscalationReceivedAt` for received rows. **No** separate receiving-inbox
  surface (the user chose the same-list option).
- **ESC·9 — Token lifecycle writes are audited; revocation is immediate.**
  `Generate` + `Revoke` each write exactly **one** `AccessAudit` row
  (`Via = Admin`, actions `escalation.token.generate` /
  `escalation.token.revoke`, `TargetKind` "escalation-token"). `Revoke` sets
  `RevokedAt` / `RevokedBy`; after revocation the token **no longer
  validates** (immediate). `LastUsedAt` is stamped on each successful inbound
  (a read-then-write on the token row, no audit row).
- **ESC·10 — `AccessVia` gains exactly one additive value: `Escalation`.**
  The M1 `Admin` / ADR 013 `Group` / ADR 0028 `Guardian` / ADR 0036
  `Community` / ADR 0041 `Resident` / M31 `Anonymous` append precedent — the
  **12 frozen values are untouched**. The inbound audit row is the **only**
  row that carries `Via = Escalation` (the M2M standing — the receiving admin
  is not the actor, the origin platform is).
- **ESC·11 — The closed `escalation.*` `kw-l` key set is parity-pinned in
  four languages.** Every new user-visible string is a `KnownTranslationKeys`
  entry present, **non-empty, in all four** languages (en/de/fr/da), pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`. The
  `en` values are the source text (ADR 0015 D1 `kw-l` provider-floor
  discipline); the `de` / `fr` / `da` values are U07's to author (the M30·6
  four-language pin).
- **ESC·12 — The admin token/config surface is GlobalAdmin-gated; no new
  authz surface.** `/admin/escalation` + `/admin/escalation/tokens` are
  `[Authorize(Roles = GlobalAdmin)]` (the M31·4 / M31·9 / M32·10 precedent).
  **No new `AccessAction`, no new `Decide()` branch, no new
  `IAuthorizationService` surface.** The inbound endpoint is **public**
  (token-gated, ESC·5).
- **ESC·13 — The six-member close flip is U09's responsibility.** The
  `Milestones.cs` **lane row** (added as `StatusDone`, **after** the M32 row,
  without disturbing the M33 / M34 order or the single-in-progress pin — M34
  stays `StatusNext`) / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` (a new entry, newest-first, naming ESC
  + ADR 0159) lands in U09. The M27 "shipped with no entry until caught in
  review" lesson (AGENTS.md) is held.

## FACES (pinned, 10)

- **ESC-1 — The receiving admin generates a labeled token.** The plaintext is
  shown **once**, the `EscalationToken` row (hash) is stored + one
  `AccessAudit` row (`escalation.token.generate`) (ESC·3, ESC·9).
- **ESC-2 — The receiving admin hands the token to the origin admin
  (out-of-band); the origin admin sets endpoint + token at
  `/admin/escalation`.** The `EscalationOutboundConfig` row is stored
  (ESC·4).
- **ESC-3 — The origin admin escalates a report (valid token + configured
  endpoint).** The forwarder POSTs the payload + the `Bearer` token → the
  receiving inbound validates the token → creates an `Origin = "escalated"`
  `ErrorReport` row + one `AccessAudit` row (`Via = Escalation`) → the origin
  marks it `resolved` (ESC·3, ESC·5, ESC·6).
- **ESC-4 — The origin admin escalates with an **invalid** token.** The
  receiving inbound returns **401**, no row; the forwarder reports a failure
  (no local `resolved`) (ESC·5, ESC·7).
- **ESC-5 — The origin admin escalates with a **revoked** token.** The same
  as ESC-4 (401, no row) (ESC·5, ESC·9).
- **ESC-6 — The origin admin escalates with **no** token / endpoint
  configured.** The forwarder is a no-op (a "not configured" flash), no HTTP
  (ESC·4, ESC·6).
- **ESC-7 — The receiving admin revokes a token.** Immediate (subsequent
  inbounds with that token → 401), one `AccessAudit` row
  (`escalation.token.revoke`) (ESC·9).
- **ESC-8 — The receiving admin views `/admin/error-reports?filter=received`.**
  Only `Origin = "escalated"` rows, each showing `FromInstance` (ESC·8).
- **ESC-9 — A re-delivery of the same origin report under the same token.**
  Idempotent (the existing row, no duplicate, no second audit row) (ESC·7).
- **ESC-10 — A non-GlobalAdmin visits `/admin/escalation` or
  `/admin/escalation/tokens`.** They get a **403** (ESC·12).

---

*Part 2 (U02) follows below.*
