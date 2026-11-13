---
id: adr-0302
title: "The shared kiosk link: a per-playlist, revocable, long-random-token read-only credential that reaches only the kiosk surface and shows community-visible items only"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0285-kiosk-playlist-any-item.md
  - docs/adr/0164-kiosk-mode.md
  - docs/adr/0159-escalation-authorization.md
  - docs/adr/0142-delete-account.md
  - docs/archive/lanes/improve-ux/plan-improve-ux.md
supersedes: []
superseded_by: []
lane: improve-ux
updated: 2026-11-10
---

# ADR 0302 — The shared kiosk link: a per-playlist, revocable, long-random-token read-only credential that reaches only the kiosk surface and shows community-visible items only

Status: Accepted
Date: 2026-11-10
Builds on: 0164 (the kiosk lane), 0285 (the per-resident `KioskPlaylist`
this link points at), 0159 (the `EscalationToken` hash-at-rest precedent —
the token-shape template, not the standing). Amends: nothing — the
personal-kiosk mode (0164/0285) is untouched.

> **Abstract:** IMPROVE-UX finding **E8** — `/kiosk` runs under a resident's
> session, so a wall display in a common room needs a signed-in resident and
> lands on the login screen when the session expires. This ADR adds the
> *shared kiosk link* (decision D4 of
> [plan-improve-ux](../archive/lanes/improve-ux/plan-improve-ux.md)): a
> **per-playlist, revocable, long-random-token** credential that authenticates
> **only** on `/kiosk` + its slides, is **read-only**, shows **community-
> visible items only** (anything else renders the existing denied slide),
> records **last-used**, and is **audited under the owner as
> via-kiosk-link**. The token is **CSPRNG-generated (256 bits), shown to the
> owner exactly once at creation, and stored only as its SHA-256 hex digest**
> (the 0159 / `IdentityToken` hash-at-rest rule). The Core seam
> (`KioskLink` doc + `IKioskLinkService` — create / revoke / validate) is
> pure Core (no Web coupling) so R16's middleware can call it. **Who is it
> for:** the developer implementing IMPROVE-UX R16–R18 and the SEC reviewer
> at R19. **When to read the rest:** before touching the kiosk auth surface,
> or when reviewing the kiosk-link code.

## Context

The kiosk (ADR [0164](0164-kiosk-mode.md), reworked by
ADR [0285](0285-kiosk-playlist-any-item.md)) is a **per-resident**
`KioskPlaylist` (one row per signed-in user, `Id` = the subject id) that the
`/kiosk` display resolves for the *viewer* — so the wall screen in a common
room must run under a resident's signed-in session. When that session
expires, the display lands on the login screen (IMPROVE-UX E8): a resident
dead-end for the *other* audience the kiosk serves — the people who merely
watch the wall screen in a shared space. The existing read-only doctrine
(0164 M35·6 / 0285: each slide iframes the item's own route, which re-runs
its own read decision on every load) is exactly what a link-authenticated
viewer needs — but there is no credential that can stand in for the
resident's session on `/kiosk` alone.

What already exists and is kept: the `KioskPlaylist` doc + the
`IKioskService` 2-method seam (0164 M35·1), the `KioskEntry` value object
(0285), the denied-slide shape the display already renders for items the
viewer cannot read (0285), and the hash-at-rest credential precedent
(0159 `EscalationToken`, the `IdentityToken` F2 rule — the SHA-256 hex
digest is the only form a token is kept in at rest).

## Decision

- **D1 — The `KioskLink` doc: one row per link.** Members: `Id`
  (Marten-generated, conventional string), `PlaylistId` (the `KioskPlaylist`
  the link plays), `OwnerSubjectId` (the resident who created it — the audit
  principal), `TokenHash` (the SHA-256 hex digest of the long random token —
  **never the raw token**, the 0159 / F2 rule), `CreatedAt`, `LastUsedAt?`
  (stamped on each successful validate — a read-then-write, **no** audit row,
  the 0159 ESC·9 precedent), `RevokedAt?` (set on revoke; null while valid).
  A **new doc type** on the **existing `CoreDocTypes`** surface (the ADR 0004
  §B.1 additive precedent — delta-detected by Marten, no DDL, no new
  `*DocTypes`). Carries `[DataClass]` (the ADR 0298 SecGate rule — an
  owner credential, `Delete` erasure, `Never` export).

- **D2 — The token shape.** CSPRNG-generated (`RandomNumberGenerator`),
  **32 random bytes base64url, `kskl_`-prefixed** (a 0159 `kesc_`-style
  high-entropy format — 256 bits, so brute-force validation is infeasible),
  shown to the owner **exactly once** at creation (returned by
  `CreateAsync`; the caller shows it and it is gone — the store holds only
  the hash, so the raw token is **unrecoverable by design**, the 0159 ESC·3
  rule).
  `// TODO ❓` the token length (proposed: 256 bits as stated) — the R19
  SEC review settles it.

- **D3 — The `IKioskLinkService` seam (pure Core — no Web coupling, so
  R16's middleware can call it):**
  - `CreateAsync(playlistId, ownerSubjectId)` → the row + the raw token.
    Exactly one `kiosk.link.create` `AccessAudit` row (`Via: Owner`,
    `TargetKind = "kiosk"` — the `KioskPlaylist` one-row convention).
  - `RevokeAsync(linkId, ownerSubjectId)` → sets `RevokedAt`, exactly one
    `kiosk.link.revoke` audit row (same shape). Missing / already-revoked is
    a **no-op returning false**, no audit row (the 0159 ESC·9 idempotent
    precedent). The owner-scope check (the link's
    `OwnerSubjectId == ownerSubjectId`) is enforced — a non-owner cannot
    revoke.
  - `ValidateAsync(tokenHash, ownerSubjectId)` → the row if valid + not
    revoked + the owner matches, else `null` (the caller renders the denied
    shape). Stamps `LastUsedAt` on a match (read-then-write, **no** audit
    row — the M35·5 / 0159 "the read is not an access decision" floor).
    The **hash** (not the raw token) is the lookup key (the
    `IdentityService.FindTokenAsync` precomputed-constant pattern — the hash
    is a local constant, never inside a `.Where(...)` expression).
  - `ValidatePresentedAsync(presentedToken)` → the R16 seam: hashes the
    presented token **inside Core** with the one shared
    `IdentityService.Sha256Hex` digest and looks up the matching non-revoked
    row, owner-less. **Trap A:** the Web host *cannot* hash the presented
    token — `IdentityService.Sha256Hex` is `internal` to the Core assembly
    (no `InternalsVisibleTo` to the Web host), and re-implementing the digest
    in the Web layer would break the "the hash is one fact" invariant the
    `EscalationTokenService` keeps; so the hashing lives in Core and the
    Web middleware hands the raw token to this overload. **Owner-less by
    design** (the `EscalationTokenService.ValidateAsync(plaintext)` precedent
    this ADR names as its template): the wall screen has no owner session —
    the owner is a *property of the link*, discovered by the lookup, not a
    caller input — so the overload returns the `KioskLink` row (which carries
    `OwnerSubjectId`, the principal the R16 middleware mints). Stamps
    `LastUsedAt` on a match (no audit row); a blank / unknown / revoked token
    is `null`. The R16 `KioskLinkMiddlewareTests` pin that the two paths
    agree on the same token (the `KioskLinkServiceTests` R16 overload test).
- **D4 — Invariants (what the R16–R18 units build against).** The link is
  **revocable** (revoke takes effect on the next validate), **read-only**
  (the link never confers a write standing — R16's restricted principal
  carries no write claims), reaches **only** `/kiosk` + its slides (R16
  enforces; a token presented anywhere else is refused), shows
  **community-visible items only** (R17 — the catalog's `ResolveAsync`
  returns the denied slide for anything not visible to the whole
  community), and is **audited under the owner as via-kiosk-link** (the
  item reads the link authenticates are the owner's reads, `Via: Owner` —
  the 0164/0285 "the kiosk re-runs the item's own read decision" doctrine,
  with the owner as the principal). Every read is auditable to the owner
  who created the link.
- **D5 — What is NOT changed.** The **personal kiosk** (0164/0285 — signed
  in, the owner's full visibility, the existing per-entry options) is
  untouched (the plan's D4 "two modes"). The `KioskPlaylist` doc, the
  `IKioskService` seam, the `KioskEntry` value object and the
  `KioskItemCatalog` are all unchanged by this ADR — R15 is the **Core seam
  + ADR only**; R16 (auth on `/kiosk` + `/kiosk/slide`), R17 (the
  community-visible filter), R18 (the `/settings/kiosk` surface) and R19
  (the SEC review) are separate later units. R16's only touch to the
  `KioskController` surface is a `[EnableRateLimiting("kiosk-link")]`
  attribute on `Index`/`Slide` (the "rate-limited" line of the R16 card) —
  the actions' read/authorize behaviour is unchanged.
- **D6 — Open questions (the R19 SEC review settles).** `// TODO ❓`
  (a) the token length (D2 proposes 256 bits); (b) an **expiry** (this ADR
  ships none — a link lives until revoked; whether a long-lived shared
  credential needs a max age is a SEC call); (c) the **max links per
  playlist** (this ADR ships no ceiling — the R18 surface can list and
  revoke them). Recorded here as held, not decided.
- **D7 — R16's auth surface (the R16 SEC review settles — R19 reviewed
  R15–R18, not R16's auth).** R16 lands the `KioskLinkMiddleware` (Web):
  on `/kiosk` + `/kiosk/slide`, a presented `?link=<token>` (R18's carrier)
  is validated through `ValidatePresentedAsync` and, on a hit, a restricted
  owner principal is minted — `Kumunita.Sub` = the owner, `Kumunita.Verified`
  = `false`, the single role value `Roles.KioskLink` (a role **value**, not a
  claim **type** — the `ClaimShapingInvariantBTests` pin stays green), and
  no `Member`/`Moderator`/`GlobalAdmin` (read-only). On any other path the
  token is inert (the surface's own gate denies it). The principal is signed
  in as the host cookie (`kumunita.auth`, the scheme real residents are minted
  on) because `kiosk.ts`'s slide-refresh loop re-fetches `/kiosk/slide` on a
  cookie-carrying, token-less fetch. `// TODO ❓` (for the R16 SEC review)
  (i) an already-minted cookie is **not invalidated** by
  `RevokeAsync(linkId)` — it lapses at the 14-day cookie span or on sign-out
  / browser clear (the standing is read-only, so the blast radius is bounded;
  a short cookie span or a per-request re-validate is a SEC call); (ii) the
  token in the URL also lands in access logs (the residual note — the site's
  `Referrer-Policy: no-referrer` already suppresses the `Referer` leak).
  Both recorded here as held, not decided.

## Consequences

- A **new Marten doc type** (`KioskLink`) registered on the existing
  `CoreDocTypes` surface (the ADR 0004 §B.1 additive delta — no migration,
  no new `*DocTypes`); the `DataClassCoverageTests` registered-type count
  rises by one (82 → 83) and the new row is `Delete`-erased (the owner
  account's eraser owns it — the R19 SEC review confirms the seam).
- Two new audit verbs (`kiosk.link.create` / `kiosk.link.revoke`) join the
  `kiosk.save` family — the audit trail shows the link's lifecycle
  (create → revoke) with the owner as the actor.
- The token is a **credential** in the 0159 sense (hash-at-rest, shown once,
  revocable) but a **different standing**: it confers only read access to
  the kiosk surface under the owner's principal, never a write standing.
- The personal-kiosk mode and every 0164/0285 invariant stand unchanged;
  the link is an *additional* way to reach the same display, not a new one.
- **Delivery:** R15 (this ADR + the doc + the service + the tests) lands
  first; R16–R19 build on it.
