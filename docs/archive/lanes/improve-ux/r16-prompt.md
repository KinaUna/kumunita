---
id: r16-prompt
title: "Prompt: IMPROVE-UX R16 — kiosk-link authentication on /kiosk (the Web surface that makes R15's seam reachable)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0302-shared-kiosk-link.md
  - docs/adr/0285-kiosk-playlist-any-item.md
  - docs/adr/0164-kiosk-playlist.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R16 — the Web authentication seam that makes a kiosk link actually work:
> the `/kiosk?link=<token>` URL that R18's `/settings/kiosk` surface hands
> a resident now reaches a live endpoint (R15's Core seam + R17's
> community-visible filter already exist; R16 is the missing auth that turns
> them into a usable wall-screen credential). **Who is it for:** the next
> coding agent (fresh context). **When to read the rest:** when starting R16
> (the unit R19's SEC review filed as its headline finding, F1: a resident
> can mint a token that authenticates nowhere until R16 lands).

# Fresh-context prompt: IMPROVE-UX R16

You are continuing the Kumunita repository on `development` (HEAD
`42ee1816`, R19 landed). The whole ADR 0302 kiosk-link lane is **built**
except the authentication: R15 (`8de9751a`, the Core
`IKioskLinkService` create/revoke/validate seam), R17 (`0a15fd02`, the
community-visible filter in `KioskItemCatalog` + the `Roles.KioskLink`
role value), and R18 (`8531cd6d`, the `/settings/kiosk` create/list/revoke
surface) are all landed; R19 (`42ee1816`) did the SEC review and filed **R16
as finding F1 — the headline** (a resident can mint, list and revoke a kiosk
token that authenticates **nowhere**). R16 is the unit that closes that gap:
the middleware that lets a presented token reach `/kiosk` + its slides under
a restricted owner principal.

**Implement IMPROVE-UX unit R16 only** — the kiosk-link authentication. The
plan card (in `plan-improve-ux.md`, the R16 row):

> R16 — Kiosk-link authentication: a handler/middleware that accepts the
> token **only** on `/kiosk` and `/kiosk/slide`, builds a restricted principal
> (owner + `kiosk-link` claim), refuses everything else; rate-limited;
> tests. Read first: `KioskController.cs:42-150`, `Program.cs` auth setup.
> Done when: **tests: token on `/posts` → refused; on `/kiosk` → playlist.**

Decision D4 (ADR 0302) is the shape you are making reachable: the shared
kiosk link is **revocable, read-only, reaches only `/kiosk` + its slides,
shows community-visible items only, and is audited under the owner as
via-kiosk-link.** R16 is the **authentication seam only** — R17 (the
community-visible filter) and R18 (the `/settings/kiosk` surface) are
**already built**; do not touch their filter logic or views, your principal
just has to make R17's filter engage (see the traps). A **SEC review of
R16's auth** is a separate later unit (R19 already reviewed R15–R18, not
this one) — leave the token-in-access-logs question as a `// TODO ❓` for it.

## Two traps that will bite you — read before you write

**(A) You cannot hash the token from the Web layer.** R15's
`IKioskLinkService.ValidateAsync(tokenHash, ownerSubjectId)` takes the token's
**SHA-256 hex digest**, and the shared hasher
`Kumunita.Core.Identity.IdentityService.Sha256Hex` is **`internal` to the
Core assembly** (not `public`, and there is no `InternalsVisibleTo` to the Web
host). So a Web middleware **cannot** compute the hash to pass in — the raw
token must be hashed *inside* Core. **Do not** re-implement the SHA-256 hex
digest in the Web layer (a second copy of the algorithm is a divergence risk,
and it breaks the "the hash is one fact" invariant the `EscalationTokenService`
keeps). The clean fix is a **thin Core overload**: add
`ValidatePresentedAsync(string presentedToken, string ownerSubjectId)` to
`IKioskLinkService`/`KioskLinkService` that hashes the raw token with the
existing `IdentityService.Sha256Hex` and delegates to the hash-based
`ValidateAsync`. This is a small, legitimate Core addition R16 needs (the
"one seam per unit" rule still holds — it's part of *this* unit's seam). Keep
the algorithm in **one** place.

**(B) `kiosk-link` must be a role VALUE, not a new claim TYPE.** The thin
principal is pinned by **invariant B** (`ClaimShapingInvariantBTests`,
ADR 0006 §B): *only* the claim types in `Kumunita.Core.Identity
.ClaimTypes.All` (`Kumunita.Sub` / `Kumunita.ExternalId` /
`Kumunita.Verified` / `Kumunita.Role`) may appear. A **new claim type**
(e.g. `Kumunita.KioskLink`) would break that pin. The house way to add a new
standing without touching the claim *set* is the **M19 `Roles.Guest`
precedent**: a new **role value** on the existing `Kumunita.Role` claim
("the only new claim string is this one; the `ClaimTypes.All` set is
unchanged"). So the restricted principal carries: `Kumunita.Sub` = the
**owner's** subject id (so the existing `KioskController`'s
`KumunitaPrincipal.SubjectId(User)` resolves to the owner, and every read the
link authenticates audits under the owner — the ADR 0302 D4 invariant),
`Kumunita.Verified` = `false`, and **no** `Member`/`Moderator`/
`GlobalAdmin` role (read-only — no write standing). The `kiosk-link` marker
is a **role value** — and it **already exists**: `Roles.KioskLink`
("kiosk-link") was added by R17 (`ThinPrincipal.cs:62`); your principal just
carries it on the `Kumunita.Role` claim (R17's filter
`roles.Contains(Roles.KioskLink)` is what switches on it). Confirm your
principal shape against `ClaimShapingInvariantBTests` before you finish —
it must not add a claim *type* (only values).

## Before writing, read (in this order)

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** — the
  doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the comment
  rule, the don't-pause-mid-task rule).
- **`docs/adr/0302-shared-kiosk-link.md`** (the R15 ADR, D4 invariants + D6
  open questions) and the **R15 section of
  `improve-ux-handoff-notes.md`** (it lists exactly what R15 left for you,
  including trap A: "the R16 middleware must hash the presented token" — and
  the `ValidateAsync` seam shape).
- **`src/Kumunita.Web/Controllers/KioskController.cs`** (the `[Authorize]`
  `Index`/`Slide` actions, ~lines 42-150) — the two surfaces the link must
  reach; note `Index`/`Slide` resolve the owner via
  `KumunitaPrincipal.SubjectId(User)` and `KumunitaPrincipal.RoleSet(User)` —
  your principal must feed those.
- **`src/Kumunita.Web/Program.cs`** — the auth setup:
  `builder.Services.AddAuthentication(CookieAuthenticationDefaults…)`
  (~831), `builder.Services.AddAuthorization()` (~952), the
  `builder.Services.AddRateLimiter(…)` policy block (~598, the
  `AddWindow(name, limit, window)` helper + the shared `OnRejected` shape),
  and the middleware pipeline order (~1160-1223:
  `UseRateLimiter` → `UseAuthentication` → the
  `*Middleware` family → `UseAuthorization`).
- **`src/Kumunita.Web/Security/KumunitaClaimsPrincipalFactory.cs`** +
  **`src/Kumunita.Core/Identity/ThinPrincipal.cs`** — how the admissible claim
  set is minted and what `ClaimTypes.All` freezes (trap B).
- **`src/Kumunita.Web/Security/PrivilegedStampMiddleware.cs`** — the
  existing per-request middleware shape to mirror (how to read
  `context.User`, when to short-circuit, the "only when it matters" fast
  path).
- **`src/Kumunita.Core/Kiosk/IKioskLinkService.cs`** +
  **`KioskLinkService.cs`** — the R15 seam you are calling (and extending
  per trap A).
- **`docs/dev/testing-and-ci.md`** — the test-runner quirk and the
  in-process `dotnet exec` invocation.
- **`git status`** + the branch (R15's commit must be intact; you are on
  `development`).

## The work

1. **The Core seam addition (trap A):** extend
   `IKioskLinkService` + `KioskLinkService` with
   `Task<KioskLink?> ValidatePresentedAsync(string presentedToken,
   string ownerSubjectId, CancellationToken ct = default)` — it
   hashes `presentedToken` with `IdentityService.Sha256Hex` (the one existing
   hasher) and delegates to the hash-based `ValidateAsync`. A blank/empty
   presented token returns `null` (the `ValidateAsync` guard). Add a unit test
   in `KioskLinkServiceTests` proving the presented-token overload and the
   hash-based seam agree on the same token (so the two cannot drift).
2. **The middleware** (`src/Kumunita.Web/Security/KioskLinkMiddleware.cs`,
   mirroring `PrivilegedStampMiddleware`'s shape): on a request to **`/kiosk`**
   or **`/kiosk/slide`** that is **not already authenticated** and presents a
   kiosk-link token, resolve `IKioskLinkService`, call
   `ValidatePresentedAsync(token, ownerScope)` → on a hit, build the
   restricted owner principal (trap B shape: `Kumunita.Sub` = owner,
   `Kumunita.Verified` = `false`, a `kiosk-link` role value, **no** other
   roles) and sign it in for the request (cookie `SignInAsync` or
   `context.SignIn`) so the existing `[Authorize]` `KioskController` actions
   pass and resolve the owner. **On any other path** the token is **inert** —
   the middleware does not mint a principal, so the surface's own
   `[Authorize]`/read decision denies it. **The carrier is already decided
   by R18:** the `/settings/kiosk` surface hands the resident a bare token and
   the `kiosk-settings.ts` copy affordance assembles `/kiosk?link=<token>`
   **client-side** — so R16's middleware must parse the **`?link=` query
   param** (that is exactly what makes R18's client-built URL reach a live
   endpoint and closes R19's F1 gap). The site sets `Referrer-Policy:
   no-referrer` (ADR 0285 D7), so the query token does not leak via `Referer`;
   the residual note (a URL token also lands in access logs) is a `// TODO ❓`
   for the later R16 SEC review, not a blocker.
3. **Use the existing `kiosk-link` role value** — `Roles.KioskLink`
   ("kiosk-link") already exists (R17, `ThinPrincipal.cs:62`); your
   restricted principal just carries it on the `Kumunita.Role` claim (the
   M19 role-value precedent — **not** a new claim type; `ClaimTypes.All` is
   unchanged). Confirm `ClaimShapingInvariantBTests` stays green (it asserts
   claim *types*, not values — so this is safe).
4. **Rate limiting:** apply the existing `AddRateLimiter` policy shape (a new
   named window, or a reasonable fixed window) to the token-presenting
   surface(s) so a leaked/brute-forced token is throttled — the plan's
   "rate-limited" line. Use the `AddWindow(…)` helper's existing
   `OnRejected` shape.
5. **DI registration:** the middleware is added to the pipeline in
   `Program.cs` (place it after `UseAuthentication`, consistent with the other
   `Security/*Middleware` family); `IKioskLinkService` is already registered
   by R15 (`Kiosk/DependencyInjection.cs`) — resolve it in the middleware
   from the per-request scope.
6. **Tests** in `tests/Kumunita.Web.Tests/` — a `KioskLinkMiddlewareTests`
   class hitting the plan card's "Done when" verbatim:
   (a) a **valid token on `/kiosk`** → the owner's playlist renders (not a
   login redirect); (b) the **same valid token on `/posts` (or any
   non-kiosk path) → refused** (no principal minted, the surface's own gate
   denies); (c) a **revoked / unknown token on `/kiosk` → refused** (falls
   through to the existing denied/empty shape, not a crash); (d) a **token on
   `/kiosk/slide`** validates the same way as on `/kiosk`; (e) the minted
   principal carries the owner's `Kumunita.Sub` and the `kiosk-link` role and
   **no** `Member`/`Moderator`/`GlobalAdmin` role (the read-only pin); (f) the
   rate-limit policy is present on the surface. Follow the existing Web test
   harness for `KioskController` (see `KioskControllerTests`) for how to boot
   the host + call the actions.

## Scope rules

- **Do not touch** `IKoSikItemCatalog` / the community-visible filter logic /
  the `/settings/kiosk` editor views — R17 and R18 own those. R16 is the
  **authentication seam + the thin Core overload** (trap A) + the role value
  (trap B) + rate limit + tests only.
- **Do not touch R17/R18** (the community-visible filter in
  `KioskItemCatalog` + the `/settings/kiosk` views/TS) — they are built; your
  principal just has to make R17's filter engage (carry the `kiosk-link`
  role + the owner's `Kumunita.Sub`). A **SEC review of R16's auth** is a
  later unit (R19 already reviewed R15–R18, not this one).
- **One commit per change** (the house rule): the Core overload + the
  `kiosk-link` role value + the middleware + the rate-limit wiring + the tests
  land in **one** `feat(ux):` commit
  (`feat(ux): kiosk-link authentication on /kiosk — the shared link reaches
  the display (IMPROVE-UX R16)`). If the ADR needs an amendment for the
  token carrier (trap A's `// TODO ❓`), that is a `docs(ux):` line in the
  **same** commit (R15 precedent: ADR + code together).
- **New strings go to en/de/fr/da** — but R16 is Web + a Core role constant,
  so the only user-visible addition (if any) is a refusal/empty message; if
  you add one, add it to en/de/fr/da (de/fr/da machine-written, flagged for
  the parked native review). Prefer reusing the existing
  `kiosk.empty` / `kiosk.slide_denied` keys over new ones.
- **The claim-shape invariant B and the translation parity test** must stay
  green (invariant B is the trap-B pin — run it).

**Before committing:** run the narrowest tests via the in-process runner
(see `testing-and-ci.md`): `KioskLinkMiddlewareTests` (your new Web class) +
`KioskLinkServiceTests` (the R15 Core seam incl. the new overload) +
`ClaimShapingInvariantBTests` (the trap-B pin) +
`KioskControllerTests` (the existing kiosk surface intact) + the translation
parity test, and `dotnet build`. If any test fails, read the error, adjust,
continue — a failed test is a recoverable step, not a stopping point. Note:
`Kumunita.Web.Tests` is expected to fail **3** known-reds (the two SecGate
`DataClassCoverageTests` M2 gates — eraser + export — +
`ImproveHarnessTests.CodeCheck_Gate_Passes`); the SEC-READ marker gate is
**green as of R19**. Those 3 are recorded drift (SEC-lane M2 + the ratchet),
**not** regressions — don't "fix" them (different unit's scope).

Then report: the Core overload signature you added and why (trap A), the
restricted principal's exact claim set and how it stays inside
`ClaimTypes.All` (trap B), how the token is presented + the `// TODO ❓`
carrier question for R19, the rate-limit policy name + window, the test class
name + the number of tests, the commit hash(es), and which of the 3 known
Web reds (if any) your run reproduced. Do not commit unrelated work.
