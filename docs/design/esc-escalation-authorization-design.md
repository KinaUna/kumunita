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

## Seams & contracts (Part 2, written by U2)

> **How to read this section.** The C# shapes below are **pinned verbatim** so
> that U03–U06 can implement against them without re-deriving a design
> decision. Each shape carries the invariant id (ESC·n) it realizes, and the
> **frozen base** it extends additively (the M32 `ErrorReport` 15-member doc,
> the M31/M32 `IErrorReportService` 4-method surface, the M32
> `EscalationForwarder` `ForwardAsync` signature, the 12 frozen `AccessVia`
> values). Nothing here re-shapes a frozen member — every addition is named as
> such. When a shape and the register disagree, **this file wins for the
> pinned shapes** (Part 1, "Three-tier contract").

### 2.1 frozen seam list (exact C#)

#### 2.1.1 the `IEscalationTokenService` seam (ESC·3 / ESC·9) + the
`EscalationTokenSummary` read-model

New seam, **Core** (namespace `Kumunita.Core.ErrorReports`). The M31/M32
`IErrorReportService` 4-method surface is **untouched** (ESC·1 — this is a
*separate* seam, not a re-shape of the existing one). The summary is a
**read-model record** — it is **never** the doc, and it exposes **no**
plaintext and **no** `TokenHash` (ESC·3 — the plaintext is shown once at
generation, never persisted; the `TokenPrefix` is the masked display form).

```csharp
// namespace Kumunita.Core.ErrorReports — a Core service seam, not a Web service
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

// The read-model (a Core record, NOT the doc — the plaintext / hash are
// excluded, ESC·3). The RevokedBy may be included for the detail render.
public sealed record EscalationTokenSummary(
    string Id,
    string Label,
    string TokenPrefix,          // the masked display form, e.g. "kesc_ab12…"
    DateTimeOffset Created,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    string? RevokedBy);
```

#### 2.1.2 the `AcceptInboundAsync` seam (ESC·5 / ESC·7) + the
`InboundReport` / `InboundResult` records

The **5th** method on `IErrorReportService` (the M31 3 + the M32 1 + the ESC 1
— the service surface is **5 methods** in ESC; the M31/M32 four are
**unchanged**, ESC·1). `InboundReport` is a **separate** record — it is **not**
a re-shape of the M31/M32 `ErrorReportDraft` (the inbound payload carries the
origin's fields + the receiving platform's identity, which the draft does not).

```csharp
// Added to IErrorReportService (the M32 4-method surface gains a 5th).
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

// The acceptance result.
public sealed record InboundResult(bool Created, ErrorReport Row, string? Error);
// Created == true  → a new Origin="escalated" row was written (+ one
//                    AccessAudit row, Via = Escalation, action
//                    "errorreport.inbound").
// Created == false (idempotent, ESC·7) → the existing row is returned, no
//                    audit row.
// Error non-null   → a validation failure (invalid token / blank
//                    description) — no row, no audit row (the Web endpoint
//                    maps the invalid-token case to a 401).
```

#### 2.1.3 the `EscalationForwarder` change (ESC·4 / ESC·6) + the
`EscalationResult` additive member

The M32 `ForwardAsync` **signature is unchanged** (ESC·6 — no new parameter;
the forwarder reads its own config). Only the **impl** changes, and one
**additive** member is added to `EscalationResult` (the M32 4 members are
**unchanged**). The 8-field payload is **unchanged** (the M32·1 pin —
additive-only).

```csharp
// The M32 ForwardAsync signature is UNCHANGED (ESC·6):
Task<EscalationResult> ForwardAsync(string reportId, CancellationToken ct = default);

// EscalationResult (the M32 4 members unchanged; ESC adds ONE additive
// member, ConfiguredSource, so the flash can name the source — the M32·3
// additive precedent):
record EscalationResult(
    bool Configured,
    bool Success,
    int? StatusCode,
    string? Error,
    string? ConfiguredSource = null);   // ESC·4 additive — "db" / "env" / null

// Impl change (Web-layer — ADR 0006-D, Core stays HTTP-free):
//  1) read the DB EscalationOutboundConfig singleton (via the read seam the
//     U03 design pins — the design doc §2.2 impl); if PRESENT + ENABLED → use
//     its Endpoint + send `Authorization: Bearer <Token>` (ESC·6), set
//     ConfiguredSource = "db";
//  2) if the DB config is ABSENT / DISABLED → fall back to the M32·6 env var
//     configuration["KUMUNITA:ESCALATION_ENDPOINT"] (endpoint only, NO token
//     — the unauthenticated M32-style forward, the ESC·4 fallback), set
//     ConfiguredSource = "env";
//  3) if NEITHER is present → the M32 no-op (Configured == false, FACES
//     ESC-6, ConfiguredSource = null).
```

### 2.2 new ESC-owned Core types (exact C#)

#### 2.2.1 the `EscalationToken` doc (the ESC·3 9-member ceiling)

Namespace `Kumunita.Core.ErrorReports`. **9 members** (the ESC·3 pin). The
**plaintext is never a member** (ESC·3 — only the SHA-256 hash is stored; the
plaintext is shown once at generation, never persisted). The token format:
`kesc_` + 32 CSPRNG bytes base64url (256 bits).

```csharp
// namespace Kumunita.Core.ErrorReports
// ESC·3 — the receiving-side token. Only the SHA-256 hash is stored; the
// plaintext is shown exactly once at generation, never persisted. Tokens are
// CSPRNG-generated (RandomNumberGenerator), 32 random bytes base64url,
// kesc_-prefixed (a ghp_-style high-entropy format — 256 bits). Multiple,
// labeled, individually revocable.
public sealed class EscalationToken
{
    public string Id { get; set; } = default!;        // conventional — Marten-generated
    public string Label { get; set; } = default!;     // the admin's name for the origin platform
    public string TokenHash { get; set; } = default!; // the SHA-256 hex digest of the plaintext — NEVER the plaintext
    public string TokenPrefix { get; set; } = default!; // the display prefix, e.g. "kesc_ab12…"
    public DateTimeOffset Created { get; set; }
    public string CreatedBy { get; set; } = default!; // the admin's subject
    public DateTimeOffset? LastUsedAt { get; set; }   // stamped on each successful inbound
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedBy { get; set; }
}
```

#### 2.2.2 the `EscalationOutboundConfig` doc (the ESC·4 6-member ceiling)

Namespace `Kumunita.Core.ErrorReports`. **6 members** (the ESC·4 pin). A
**singleton** (one row per instance, fixed `Id`). The `Token` here is the
**plaintext** the admin was handed — stored because it is the *outgoing*
secret the origin admin chose to keep, **not** a secret of the *receiving*
platform (contrast `EscalationToken.TokenHash`).

```csharp
// namespace Kumunita.Core.ErrorReports
// ESC·4 — the origin-side outbound config. A singleton row (one per
// instance). The Token is the plaintext the origin admin was handed — the
// OUTGOING secret (stored here, not on the receiving side). The M32·6 env
// var KUMUNITA_ESCALATION_ENDPOINT remains the unauthenticated
// operator-override fallback when this row is absent/disabled (ESC·4).
public sealed class EscalationOutboundConfig
{
    public string Id { get; set; } = default!;   // conventional — a fixed singleton id, e.g. "outbound-config"
    public string Endpoint { get; set; } = default!; // the receiving platform's inbound URL
    public string Token { get; set; } = default!;    // the plaintext token the origin admin was handed (the OUTGOING secret)
    public bool Enabled { get; set; }                // default false
    public DateTimeOffset Updated { get; set; }
    public string UpdatedBy { get; set; } = default!; // the admin's subject
}
```

#### 2.2.3 the four additive `ErrorReport` fields + the `Origin` "escalated"
value (the ESC·2 19-member ceiling)

The M32 **15 fields are unchanged** (ESC·2 — no re-shape). ESC adds
**exactly four** additive fields; each is null for locally-filed rows. The
`Origin` **closed set** extends from `{"error-page","general"}` to
`{"error-page","general","escalated"}` (a string field — ADR 0004 §B.1
idempotent delta, **no migration**). The re-pinned **19-member ESC ceiling** is
the M32 15 + the ESC 4 below.

```csharp
// namespace Kumunita.Core.ErrorReports — additive fields on the M32 15-member
// doc (the M32 15 keep their exact names, types, nullability; ESC·2).
public sealed class ErrorReport
{
    // … the M32 15 members, UNCHANGED (ESC·2) …
    // ESC adds EXACTLY four (the 19-member ESC ceiling, ESC·2) — each null
    // for locally-filed rows:
    public string? FromInstance { get; set; }            // the accepting token's Label (null for locally-filed)
    public DateTimeOffset? EscalationReceivedAt { get; set; } // the receive instant (null for locally-filed)
    public string? EscalationSourceId { get; set; }      // the origin's report id — the inbound idempotency key (null for locally-filed)
    public string? EscalationTokenId { get; set; }       // which token accepted it (null for locally-filed)
}
// The Origin closed set is extended additively (ESC·2):
//   {"error-page", "general", "escalated"}
// — the two M31/M32 values keep their exact strings; "escalated" marks a
//   received (not locally-filed) report.
```

#### 2.2.4 the `IEscalationTokenService` impl (the `EscalationTokenService`
additive class)

Namespace `Kumunita.Core.ErrorReports`. The `GenerateAsync` + `RevokeAsync`
lanes are **single-write-lane** (the ADR 0006 C3 shape — the M32
`MarkResolvedAsync` precedent: one write session storing the doc + exactly one
`AccessAudit` row). `ValidateAsync` is a constant-time hash compare that
stamps `LastUsedAt` on a match (a read-then-write, **no** audit row — the
M31·4 "read is not an access decision" precedent). `ListAsync` is a read that
projects to the `EscalationTokenSummary` (no plaintext / hash — ESC·3).

```csharp
// namespace Kumunita.Core.ErrorReports
public sealed class EscalationTokenService : IEscalationTokenService
{
    // ctor: (Marten.IDocumentStore store, …) — the IErrorReportService
    // registration shape (the design doc §2.1 U03 pins the exact ctor + the
    // DI line; the M32 optional-ctor-param precedent keeps the M31 test-
    // construction sites compiling).
    //
    // GenerateAsync(label, actorId, ct):
    //   plaintext  = "kesc_" + base64url(RandomNumberGenerator.GetBytes(32));
    //   tokenHash  = SHA256Hex(plaintext);
    //   prefix     = plaintext[..min(12, plaintext.Length)] + "…";
    //   store the EscalationToken row + ONE AccessAudit row (Via = Admin,
    //   action "escalation.token.generate", TargetKind "escalation-token") in
    //   ONE write session (the ADR 0006 C3 single-write-lane, the M32·8
    //   precedent);
    //   return (plaintext, summary).
    //
    // RevokeAsync(tokenId, actorId, ct):
    //   load the token; if missing OR RevokedAt != null → return false (no-op,
    //   NO audit row, the M32·8 idempotent precedent);
    //   else set RevokedAt = now, RevokedBy = actorId; store the row + ONE
    //   AccessAudit row (Via = Admin, action "escalation.token.revoke",
    //   TargetKind "escalation-token") in ONE write session; return true.
    //
    // ValidateAsync(plaintextToken, ct):
    //   tokenHash = SHA256Hex(plaintextToken);
    //   match     = query for EscalationToken where TokenHash == tokenHash &&
    //               RevokedAt == null;
    //   if none → return null (ESC·5 — invalid / revoked);
    //   else stamp LastUsedAt = now (a read-then-write, NO audit row, the
    //   M31·4 precedent) and return the token. The hash compare is
    //   constant-time (a DoS / timing floor).
    //
    // ListAsync(ct):
    //   query all EscalationToken ordered by Created desc, project each to
    //   EscalationTokenSummary (Id, Label, TokenPrefix, Created, LastUsedAt,
    //   RevokedAt, RevokedBy). NO plaintext, NO TokenHash (ESC·3).
}
```

#### 2.2.5 the `AcceptInboundAsync` impl (the `ErrorReportService` additive
method)

The M31 3 + the M32 1 methods are **unchanged** (ESC·1). `AcceptInboundAsync`
is the M32 `MarkResolvedAsync` **idempotent-write-lane** shape (the ADR 0006
C3 single-write-lane): validate the token → idempotent lookup → store the
received row + **one** `AccessAudit` row → stamp the token's `LastUsedAt` —
in **one** write session.

```csharp
// namespace Kumunita.Core.ErrorReports — additive method on the M32 4-method
// surface (the M31 3 + the M32 1 are UNCHANGED, ESC·1).
public sealed class ErrorReportService : IErrorReportService
{
    // ctor gains the IEscalationTokenService param (the M32
    // IEscalationForwarder optional-ctor-param precedent — so any test-
    // construction site that builds the service without the token service
    // keeps compiling; the AcceptInboundAsync impl then returns
    // Error: "token service absent" when the seam is null, the M31
    // localization / translationProvider floor precedent).
    //
    // AcceptInboundAsync(draft, ct):
    //   1) token = tokenService.ValidateAsync(/* the presented plaintext,
    //      resolved by the Web inbound endpoint before the call — the design
    //      doc §2.1.2 seam shape */); if token is null
    //      → return new InboundResult(Created: false, Row: null!, Error:
    //        "invalid token") (the Web endpoint maps this to a 401);
    //   2) existing = query for ErrorReport where EscalationTokenId ==
    //      draft.TokenId && EscalationSourceId == draft.SourceReportId;
    //      if found → return new InboundResult(Created: false, Row: existing,
    //        Error: null) (the ESC·7 idempotent path — the existing row, no
    //        second audit row);
    //   3) blank-description guard: if string.IsNullOrWhiteSpace(draft.
    //      Description) → return new InboundResult(Created: false, Row: null!,
    //        Error: "description required") (no row, no audit row, the M32·4
    //      pin);
    //   4) store a new ErrorReport in ONE write session (the ADR 0006 C3
    //      single-write-lane shape, the M32·8 precedent):
    //        report = new ErrorReport {
    //            Origin              = "escalated",
    //            FromInstance        = draft.FromInstance,
    //            EscalationReceivedAt = DateTimeOffset.UtcNow,
    //            EscalationSourceId  = draft.SourceReportId,
    //            EscalationTokenId   = draft.TokenId,
    //            SubjectId           = draft.OriginSubjectId,
    //            Description         = draft.Description,
    //            ContactEmail        = draft.ContactEmail,
    //            RequestId           = draft.RequestId,
    //            ExceptionType       = draft.ExceptionType,
    //            Created             = draft.OriginCreated,
    //            TriageStatus        = "new",   // the M31 default
    //        };
    //      + ONE AccessAudit row (Via = Escalation, action
    //        "errorreport.inbound", TargetKind "error-report")
    //      + stamp the token's LastUsedAt (the read-then-write on the token
    //        row — folded into the same write session; NO extra audit row);
    //   5) return new InboundResult(Created: true, Row: report, Error: null).
}
```

#### 2.2.6 the `EscalationDocTypes` registration surface (ESC·1)

A **new** parallel surface (the M31 `ErrorReportDocTypes` / the Usage
`UsageDocTypes` parallel-surface precedent). It registers the **two new
docs**; the M32 `ErrorReportDocTypes` surface is **untouched** (ESC·1 — the
four additive `ErrorReport` fields **ride** the existing
`.Schema.For<ErrorReport>()` — ADR 0004 §B.1 idempotent delta at boot, no new
surface for the additive fields).

```csharp
// namespace Kumunita.Core.ErrorReports
// ESC·1 — a NEW parallel registration surface for the two new docs (the
// ErrorReportDocTypes / the UsageDocTypes parallel-surface precedent). The
// M32 ErrorReportDocTypes surface is UNTOUCHED (ESC·1); the four additive
// ErrorReport fields ride its existing .Schema.For<ErrorReport>() (ADR
// 0004 §B.1 idempotent delta at boot).
public static class EscalationDocTypes
{
    public static void Configure(Marten.Schema.IStoreOptions options)
    {
        options.Schema.For<EscalationToken>()
                .Index(t => t.TokenHash)             // the validate lookup
                .Index((t) => (t.RevokedAt, t.Created)); // the (RevokedAt, Created) token-list ordering
        options.Schema.For<EscalationOutboundConfig>();  // the singleton
    }
}
// Wired into the existing Marten StoreOptions bootstrap (the
// ErrorReportDocTypes.Configure wire point — the U03 DI pins the exact line).
```

#### 2.2.7 the `AccessVia.Escalation` additive value (ESC·10)

The **12 frozen values are unchanged** (ESC·10). ESC appends exactly one. The
inbound audit row is the **only** row that carries `Via = Escalation` (the
M2M standing — the receiving admin is not the actor, the origin platform is).

```csharp
// namespace Kumunita.Core.Authorization (Decision.cs)
// ESC·10 — append EXACTLY ONE additive value; the 12 frozen values are
// UNCHANGED (the M1 Admin / ADR 0013 Group / ADR 0028 Guardian / ADR 0036
// Community / ADR 0041 Resident / M31 Anonymous append precedent).
public enum AccessVia
{
    // … the 12 frozen values, UNCHANGED (ESC·10) …
    Escalation,   // ESC·10 — the M2M standing: the origin platform, not the
                  // receiving admin, is the actor (the inbound audit row is
                  // the only row that carries this value).
}
```

### 2.3 the closed `escalation.*` `kw-l` key set

**~24 keys** (the ESC·11 pin — the M31/M32 `errorreport.*` / `issue.*` key
sets are the **floor**, **untouched**; ESC **adds** this new `escalation.*`
namespace). Every key is present, **non-empty, in all four** languages
(en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
`KnownTranslationKeys_ParityTests` (the M31·7 / M32·9 precedent). The `en`
values are the source text (ADR 0015 D1 `kw-l` provider-floor discipline); the
`de` / `fr` / `da` values are U07's to author (the M30·6 four-language pin).
`escalation.list.status.escalated` is the new status chip (the M32
`errorreport.list.status.*` chip precedent).

| # | key | en value (source text) |
|---|-----|------------------------|
| 1 | `escalation.title` | Escalation |
| 2 | `escalation.intro` | Authorize other platforms to escalate issues to this instance, and generate the access tokens they present. |
| 3 | `escalation.outbound.endpoint.label` | Receiving platform endpoint |
| 4 | `escalation.outbound.endpoint.placeholder` | https://receiving.example/escalations/inbound |
| 5 | `escalation.outbound.token.label` | Access token (issued by the receiving admin) |
| 6 | `escalation.outbound.token.placeholder` | kesc_… |
| 7 | `escalation.outbound.enabled.label` | Enable escalation |
| 8 | `escalation.outbound.save` | Save escalation settings |
| 9 | `escalation.outbound.saved` | Escalation settings saved. |
| 10 | `escalation.tokens.title` | Receiving access tokens |
| 11 | `escalation.tokens.generate.button` | Generate a new token |
| 12 | `escalation.tokens.label.label` | Label (e.g. the origin platform's name) |
| 13 | `escalation.tokens.show_once` | Copy this token now — it is shown only once and never stored in plain text. |
| 14 | `escalation.tokens.copy` | Copy |
| 15 | `escalation.tokens.revoke.button` | Revoke |
| 16 | `escalation.tokens.revoked` | Token revoked. It can no longer be used to escalate. |
| 17 | `escalation.tokens.none` | No access tokens yet. Generate one to authorize an origin platform. |
| 18 | `escalation.tokens.last_used` | Last used |
| 19 | `escalation.list.status.escalated` | Escalated |
| 20 | `escalation.list.filter.all` | All |
| 21 | `escalation.list.filter.received` | Received |
| 22 | `escalation.list.filter.local` | Local |
| 23 | `escalation.detail.from_instance` | Received from |
| 24 | `escalation.inbound.invalid_token` | Invalid access token. |
| 25 | `escalation.inbound.received` | Escalation received. |

> **Note on count.** The register's Assumptions name **~24 keys**; the table
> above lists the **25 keys** the U05 / U06 / U07 unit deliverables consume
> (the `escalation.inbound.received` confirmation is the 25th). U07 authors
> the **full set actually consumed** by U05's pages + U06's list / detail + the
> inbound flashes; the parity tests pin whichever closed set lands. This is the
> **only** place the "~24" approximation is resolved to an exact list, and the
> list is **additive** (it never re-shapes the M31/M32 `errorreport.*` /
> `issue.*` floor, ESC·1 / ESC·11).

### 2.4 pinned seam tests (exact names)

The **22 test names** (the ESC·2 / ESC·3 / ESC·5 / ESC·6 / ESC·7 / ESC·8 /
ESC·9 / ESC·10 / ESC·12 pins, exercised by the four test classes in the
register's Assumptions). U08 implements **exactly these names** — no test
whose exact name is not in this list (unit-series rule 3).

**`tests/Kumunita.Core.Tests/EscalationTokenTests.cs` (5 tests):**

1. `ESC_3_Token_Generate_Stores_Hash_Not_Plaintext`
2. `ESC_3_Token_Validate_Valid_Returns_Token`
3. `ESC_3_Token_Validate_Invalid_Returns_Null`
4. `ESC_9_Token_Revoke_Immediate_NoValidate`
5. `ESC_9_Token_List_Excludes_Plaintext_And_Hash`

**`tests/Kumunita.Core.Tests/EscalationInboundTests.cs` (5 tests):**

6. `ESC_5_AcceptInbound_ValidToken_Creates_Escalated_Origin`
7. `ESC_5_AcceptInbound_InvalidToken_Returns_Error_NoRow`
8. `ESC_7_AcceptInbound_ReDelivery_Idempotent_NoDuplicate`
9. `ESC_2_ErrorReport_Doc_FieldSet_ESC_Ceiling`
10. `ESC_10_AcceptInbound_AuditRow_Via_Escalation`

**`tests/Kumunita.Web.Tests/EscalationTokenPageTests.cs` (5 tests):**

11. `ESC_1_Token_Page_GlobalAdmin_Sees_TokenList`
12. `ESC_1_Token_Generate_Plaintext_VisibleOnce`
13. `ESC_7_Token_Revoke_Succeeds`
14. `ESC_10_Token_Page_NonGlobalAdmin_Denied`
15. `ESC_2_Outbound_Config_Save_Persists_Endpoint_Token_Enabled`

**`tests/Kumunita.Web.Tests/EscalationInboundEndpointTests.cs` (7 tests):**

16. `ESC_5_Inbound_Endpoint_ValidToken_Creates_Row`
17. `ESC_4_Inbound_Endpoint_InvalidToken_401_NoRow`
18. `ESC_6_Forwarder_DB_Config_Sends_Bearer_Token`
19. `ESC_4_Forwarder_NoDB_EnvVar_Fallback`
20. `ESC_6_Forwarder_NoConfig_NoHTTP`
21. `ESC_8_Received_Filter_Sees_Escalated_Only`
22. `ESC_8_Detail_Sees_FromInstance`

### 2.5 acceptance gate (U08 records)

The **three-test acceptance gate** (U08 runs + records it in `### Run result
(ESC acceptance gate — <date>)`):

- **Closed loop** — the receiving admin generates a labeled token (the
  plaintext shown once, the hash row stored) → the origin admin sets the
  endpoint + token at `/admin/escalation` → the origin admin escalates a
  report (the forwarder POSTs the 8-field payload + the `Bearer` token) → the
  receiving inbound validates the token → creates an `Origin = "escalated"`
  `ErrorReport` row + one `AccessAudit` row (`Via = Escalation`) → the origin
  marks it `resolved`. (ESC·3, ESC·4, ESC·5, ESC·6 — the FACES ESC-1 →
  ESC-2 → ESC-3.)
- **Handoff** — the receiving admin revokes a token (immediate, one
  `AccessAudit` row `escalation.token.revoke`) → a subsequent inbound with that
  token → **401**, no row, no audit row. (ESC·5, ESC·9 — the FACES ESC-7.)
- **Part-vs-whole** — the **22-test** list (§2.4) is the **whole**; the
  closed-loop + handoff tests are the **parts**. All must pass **together** in
  a single `dotnet exec` run of `Kumunita.Core.Tests` + `Kumunita.Web.Tests`
  (the AGENTS.md in-process runner path — the `dotnet test` discovery bug on
  this machine is the known runner quirk, not a failure).

### 2.6 drift-guard (frozen once written)

The following are **frozen pins** — any mismatch between the implemented code
and one of them is a `## U<m> — Drift pause` per unit-series rule §11 (pause +
record, do not silently re-shape):

- the **13-invariant** table (Part 1 — ESC·1 through ESC·13);
- the **10 FACES** (Part 1 — ESC-1 through ESC-10);
- the **`IErrorReportService` 5-method surface** (the M31 3 + the M32 1 + the
  ESC 1 — the M31/M32 four are **unchanged**, ESC·1);
- the **`ErrorReport` doc field set** (the **19-member ESC ceiling** — the M32
  15 **unchanged** + the ESC 4 additive: `FromInstance?` /
  `EscalationReceivedAt?` / `EscalationSourceId?` / `EscalationTokenId?`;
  the `Origin` closed set `{"error-page","general","escalated"}`, ESC·2);
- the **`EscalationToken`** (9-member) + **`EscalationOutboundConfig`**
  (6-member) doc shapes (ESC·3 / ESC·4);
- the **`IEscalationTokenService`** seam shape (`ListAsync` / `GenerateAsync`
  / `RevokeAsync` / `ValidateAsync` + the `EscalationTokenSummary` record,
  ESC·3 / ESC·9);
- the **`AcceptInboundAsync`** seam shape (`InboundReport` + `InboundResult`
  records, ESC·5 / ESC·7);
- the **`AccessVia`** 13-value set (the **12 frozen** + the ESC 1
  `Escalation`, ESC·10);
- the **`EscalationDocTypes`** registration shape (the two new docs on a new
  parallel surface; the M32 `ErrorReportDocTypes` untouched, ESC·1);
- the **§2.3 `escalation.*` `kw-l` key set** (additive over the M31/M32
  `errorreport.*` / `issue.*` floor, ESC·1 / ESC·11);
- the **22 test names** (§2.4 — no test whose exact name is not in this list,
  unit-series rule 3);
- the **M32 `EscalationForwarder` `ForwardAsync` signature** (unchanged, ESC·6)
  + the **8-field payload** (unchanged, the M32·1 pin) + the **`EscalationResult`**
  4-members (unchanged; `ConfiguredSource` is the single additive member,
  ESC·4);
- the **`KUMUNITA_ESCALATION_ENDPOINT`** env-var fallback (preserved, ESC·4).
