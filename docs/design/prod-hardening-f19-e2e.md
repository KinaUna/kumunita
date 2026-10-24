# F19 — TEST-03: E2E suite (signup → verify → login → write) — Part-0 design pin

> **F19.U1** design pin (Tier 1). F19.U2 and F19.U3 execute against this file;
> F19.U4 is the CI-wiring open item.
> No code, no build, no test run — the pin is the deliverable.
>
> Plan entry: `plan-prod-hardening.md` F19 (TEST-03, **`code`**, no external
> deps).
> Precedent: F18.U1 / F16.U1 / F15.U1 / F14.U1 design-pin idiom (§-numbered
> structure, "PIN:" prefix on each target, the U-split section, open items,
> handoff contract).
> **This fix is the only pure-`code` fix in the lane that does NOT touch
> `src/` at all** — the E2E suite is a *new* test concern with its own
> runner (Playwright), its own project location (see §4), and no C# seam to
> pin. The pin's job is to make the flow, the ports, the selectors, and the
> spec names concrete enough that U2/U3 are transcription, not design.

---

## §1 — The runner (PIN: Playwright against the docker-compose stack — a SEPARATE concern from the xunit suites)

**What runs it:** **Playwright** (TypeScript), driving a **real browser**
against the **docker-compose** app — not `dotnet test`, not
`Microsoft.Testing.Platform`, **not the `dotnet exec …Tests.dll` path**. The
E2E suite is a **fourth test tier** next to the existing two:

| Tier | Runner | Concern | Where |
|---|---|---|---|
| Unit + integration | **xunit.v3** via `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll` / `…Kumunita.Core.Tests.dll` (the AGENTS.md "Running the tests" quirk — `dotnet test` discovery is broken on this machine) | in-process seams, Testcontainers Postgres | `tests/Kumunita.*.Tests/` |
| **E2E (F19)** | **Playwright** (`npx playwright test`) against `http://localhost:5080` | the rendered HTTP surface: signup → verify → login → write, end to end, through the real browser cookie/anti-forgery pipeline | **a NEW top-level `e2e/` project** (see §4 — deliberately NOT under `tests/`, and NOT added to `Kumunita.slnx`) |

**Why separate (the invariant U2 must not violate):** the xunit path is the
repo's *pinned* test contract (F14's CI gate runs **exactly** those two
`dotnet exec` dlls — F14.U2 handoff). Wiring Playwright into `Kumunita.slnx`,
`dotnet test`, or the xunit discovery would (a) break the F14 gate's
"two dlls" shape and (b) drag a Node toolchain into a .NET build. The E2E
suite is **additive and standalone**: its own `package.json` +
`playwright.config.ts`, invoked as `npm --prefix e2e ci && npm --prefix e2e
run test` — nothing in the .NET solution knows it exists (until F14's gate is
extended in U4, which is an **open item**, not a landed coupling).

**The stack under test (grounded in `docker-compose.yml`, not invented):**

| Service | Compose file line | Host port | Role in the E2E flow |
|---|---|---|---|
| `app` | `ports: ["5080:8080"]` | **`http://localhost:5080`** | the app under test (all browser traffic) |
| `mailpit` | `ports: ["1025:1025", "8025:8025"]` | **`http://localhost:8025`** (web UI + its **HTTP API** at `/api/v1/*`) | the dev SMTP sink (`SMTP__Host: mailpit`, `SMTP__Port: 1025`, `SMTP__Secure: "None"`) — the verification email lands here and the **verify link is read from it** |
| `db` | (no host port) | — | Postgres; the E2E suite never talks to it directly (evidence is the rendered page + Mailpit, per the repo's "rendered HTML is the evidence" doctrine) |

> **Port note (drift-relevant for U2):** the plan's F19 entry and this pin's
> entry read both name **Mailpit on `:5099`**. The *actual* `docker-compose.yml`
> maps Mailpit's web UI to **`8025:8025`** (its API + web on 8025, SMTP on
> 1025), and `docker-compose.override.yml` (local-only) adds `SMTP__From`.
> **`:5099` is NOT in the committed compose stack** — it was a port observed
> live in this sandbox's shared browser pages (a *separate* running instance,
> per the repo memory `env-docker-ts7-and-portbind` "shared page is a
> different instance" note). **PIN: U2 MUST read the verify token from
> `http://localhost:8025/api/v1/messages`** (the compose Mailpit). If the
> sandbox's port-bind quirk (`Errno 98` for host binds) also affects 8025,
> U2 records a **Drift pause** and does not silently fall back to 5099 —
> 5099 is a different app and its mailbox is not this suite's evidence.
> (The compose file, not an observed shared page, is the ground truth.)

**Boot + readiness (the AGENTS.md "Getting a live server" doctrine, applied
to the E2E harness):**

- **Bring-up:** `docker compose build app && docker compose up -d app`
  (repo memory `docker-stale-container-quirk`: verify the *current* container
  with `docker inspect kumunita_app` before trusting a live check).
- **Readiness gate (U2 implements in the Playwright `webServer`/`globalSetup`):**
  poll `http://localhost:5080/health` (the `HealthController` route, already
  in the repo) until HTTP 200 — the app needs **~8 s** after container start
  (Marten schema + Wolverine leadership); **also** poll
  `http://localhost:8025/api/v1/info` until 200 (Mailpit up). **Fail with a
  clear message** if either does not answer within ~60 s — a wedged boot is a
  harness precondition failure, not a test failure.
- **The browser is the reliable verification path** (repo memory
  `env-docker-ts7-and-portbind`: curl login is fiddly — anti-forgery token
  extraction order, and rapid login POSTs **trip the ASP.NET Identity lockout
  (503)**). Playwright driving a real browser avoids both traps by
  construction: it reads the `__RequestVerificationToken` hidden input from
  the rendered DOM and submits the form natively. **PIN: U2 must NOT poll the
  app with scripted rapid login POSTs** — one login per spec run, and a 503 is
  recorded as a **precondition/lockout** failure, never retried in a loop
  (the failed-twice doctrine).

**Environment variables the suite reads (pinned, so U2/U3/F14 agree):**

| Env var | Default | Meaning |
|---|---|---|
| `KUMUNITA_APP_URL` | `http://localhost:5080` | the app under test (compose published port) |
| `KUMUNITA_MAILPIT_URL` | `http://localhost:8025` | the Mailpit web/API (compose published port) |

Both overridable so the suite runs against a non-default mapping without a
code change; **both default to the compose stack's actual ports**.

---

## §2 — The flows (PIN: concrete step lists, URLs, selectors, the Mailpit extraction)

Two specs, two flows. The **signup email is generated per run** (a unique
local-part + the `examplium.com` domain the seeder already uses —
`carla@examplium.com` et al., so the domain is known-good and **not** in the
F9 `DisallowedEmailDomains` blocklist) — this makes the suite **re-runnable**
against the same compose volume without "account already exists" collisions,
and it makes the Mailpit extraction unambiguous (filter by the run's email).

### §2.1 — Flow A: `signup → verify → login → one write` (spec `signup_verify_login.e2e.ts`)

**Precondition (asserted first, fail-fast with a named error — do NOT loop):**
GET `http://localhost:5080/account/signup` returns **200 AND renders the
signup form** (an `<input name="Email">` is present). **If the
`SignupClosed` notice renders instead (the ADR 0050 gate is closed — the
**live compose instance is currently in this state**, verified 2026-10-24:
the form's `Email` input is absent), the suite fails with the explicit
message *"signup gate is closed — open it via the admin settings surface
before running the E2E suite."* The gate is an **owner/admin action, not a
suite action** (opening it is a write lane with its own admin-auth surface;
the suite asserts, it does not mutate platform config). This is the **single
known precondition** and it is recorded in §5/§7, not hidden in the spec.

**Step list (U2 transcribes; selectors are the real view field names,
grep-verified in `Views/Account/Signup.cshtml` / `Login.cshtml` /
`Views/Posts/New.cshtml` — this repo's Razor convention is
`<input asp-for="X">` → `name="X"`):**

1. **Sign up.** Navigate to `{APP}/account/signup` (route: `AccountController.Signup`
   `GET` — conventional route `/{controller=Account}/{action=Signup}` →
   `/account/signup`, the same convention as the verified
   `/account/login` path in repo memory). Fill `DisplayName`, `Email` (the
   run-unique address), `Password`, `ConfirmPassword` (all four are
   `name="…"` inputs on the form). Submit. Expect: redirect to
   `/account/login` (the controller's `RedirectToAction(nameof(Login))`) with
   the flash *"Account created. Check your inbox for the verification link."*
2. **Read the verify link from Mailpit.** Poll `GET {MAILPIT}/api/v1/messages`
   (the Mailpit HTTP API — verified live, returns `{"total":N,…,"messages":[…]}`)
   until a message with `To` containing the run-unique email appears
   (bounded poll, ~30 s — the durable outbox handler stages + sends the
   verification email; a miss is a **real failure** of the mail pipeline,
   which is exactly what this flow must catch). Extract the link from the
   message body with the **pinned regex**
   `https?://[^\s]+/account/verify\?id=[A-Za-z0-9-]+` — the link shape is
   **pinned by the source**: `IdentityService.VerificationLink` builds
   `{Verification__BaseUrl}/account/verify?id={tokenRowId}` (the compose
   `Verification__BaseUrl` is `http://localhost:5080`, and the `id` is the
   token **row id** — the high-entropy secret is never in the URL).
3. **Verify.** Navigate the browser to that extracted URL. The self-serve
   lane (the account **has** a password — set at signup) runs
   `ConfirmAndSignInAsync` → **the app signs the resident in and redirects to
   `/profile/edit`** (verified in `AccountController.SignInAfterVerifyAsync`).
   **Assert:** the URL is `/profile/edit` (or a subsequent
   `/account/…` authenticated surface) AND the session cookie is present.
   *This is the "verify" step's success signal — no separate login is needed
   after the self-serve verify signs the user in.*
4. **Login (the explicit leg the flow name pins).** Sign out
   (`{APP}/account/logout` — `AccountController.Logout`), then navigate to
   `{APP}/account/login`, fill `Email` + `Password` (the same credentials
   from step 1), submit. Assert: redirect to an authenticated surface
   (`/profile/edit` per the controller's `Redirect("/profile/edit")`).
   **This leg exists so the suite covers the *login* path independently of
   the verify-sign-in path** (the two go through different
   `SignInManager` calls); it also re-confirms the credential is durable
   after verify.
5. **One write (the flow's terminal assertion).** Navigate to
   `{APP}/posts/new` (route pinned: `PostsController` `HttpGet("/posts/new")`).
   The form (`Views/Posts/New.cshtml`) is `#new-post-form`,
   `action="/posts/new"`, with required `select name="ComponentId"` (seeded —
   the sample-data neighborhood has components), `input name="Title"`
   (optional), `textarea name="Body"` (**required** — the controller adds a
   ModelState error if empty), `select name="LanguageCode"`, and the
   `Audience.*` checkboxes (the model defaults `Mode="Any"`,
   `CommunityVisible=true` — the **defaults are sufficient; do not require
   the spec to touch the audience fields**). Fill `Title` + `Body` (a
   run-unique body string, e.g. `E2E {timestamp}`), submit. **Assert:** the
   response is a redirect (not a validation re-render) AND a subsequent GET
   of the composed post (the redirect target, or the community feed at
   `/community` — the `PostsController` paged-feed route) contains the
   run-unique `Title`. **This is the "one write" the flow name pins — the
   suite's terminal evidence is the rendered post, per the
   rendered-HTML-is-the-evidence doctrine.**

**Negative-leg assertion (cheap, in the same spec):** after step 5, sign out
and GET `{APP}/posts/new` unauthenticated → assert it is **not** a 200
compose form (redirect to `/account/login`) — the write surface is
auth-gated. (One extra `expect`, no extra browser round-trip cost.)

### §2.2 — Flow B: `post_write.e2e.ts` (the write-flow spec, standalone)

The plan's F19 entry splits the suite into two specs; **Flow B is the
"create a post, assert it appears" spec on its own**, re-using the
already-verified resident from Flow A is **not** required — Flow B is
**self-contained**: it signs in with a **known sample account** (the compose
`SampleData__Enabled: "true"` seeder's credentials — the repo memory
`AGENTS.md` names the GlobalAdmin `admin@examplium.com` / `Admin123!`; the
seeder's other accounts use "documented demo credentials" — **U2 must read
`src/Kumunita.Core/Bootstrap/SampleDataSeeder.cs` to pick one
*resident* (non-admin) account's email+password**, and record the choice in
the handoff note) and then performs §2.1 step 5 verbatim.

**Why a second, standalone spec (pinned rationale, not just a plan echo):**
Flow A couples the write to the signup/verify/login chain — if the signup
gate is closed (the §2.1 precondition) or the mail leg is broken, the write
leg never runs and the failure is attributed to the wrong cause. Flow B
**isolates the write lane**: a green Flow B + a red Flow A localizes the
failure to the identity/mail chain; a red Flow B + a green Flow A localizes
it to the write lane. Two specs, one write-leg body (shared as a helper,
see §4).

**Step list:** sign in (sample resident) → §2.1 step 5 verbatim → assert the
rendered post. **No signup, no Mailpit** in this spec.

---

## §3 — The pinned spec names (the next audit run diffs against these)

| Spec file (relative to the `e2e/` project, §4) | Pinned name | Flow |
|---|---|---|
| `e2e/specs/signup_verify_login.e2e.ts` | **`signup_verify_login.e2e.ts`** | §2.1 Flow A (signup → Mailpit verify-link → verify → login → one write) |
| `e2e/specs/post_write.e2e.ts` | **`post_write.e2e.ts`** | §2.2 Flow B (sign in → one write, standalone) |

**PIN:** these two filenames are the **exact** spec names the F19 handoff,
the F14 CI gate (U4 open item), and the next prod-check audit all reference.
The test *titles* inside each spec are also pinned (so a `npx playwright
test --grep` or the audit's "did the spec run" check is unambiguous):

- `signup_verify_login.e2e.ts`: **`"resident signs up, verifies via the Mailpit link, logs in, and publishes one post"`**
- `post_write.e2e.ts`: **`"a signed-in resident publishes one post and it appears"`**

(U2 keeps the titles as the single `test(...)` name in each file; the file
name is the load-bearing pin, the title is the human-readable one.)

---

## §4 — The U2 / U3 / U4 split (pinned)

- **F19.U2 (the harness + Flow A):** create the **`e2e/` project** —
  `e2e/package.json` (Playwright + a browser, `npm --prefix e2e ci`),
  `e2e/playwright.config.ts` (base URL from `KUMUNITA_APP_URL`, the §1
  readiness `globalSetup`/`webServer` gate on `:5080/health` + `:8025/api/v1/info`,
  **no `dotnet` involvement**), and **`e2e/specs/signup_verify_login.e2e.ts`**
  (§2.1 verbatim, the §1 Mailpit regex, the §2.1 precondition fail-fast).
  A small shared helper `e2e/helpers/` (the Mailpit message poller + the
  verify-link extractor + the §2.1 step 5 write-body) so U3 re-uses, not
  re-implements, the write leg. **≤ 6 files, all under `e2e/`, NO `src/**`
  change, NO `Kumunita.slnx` change.** Exit: `npm --prefix e2e ci` resolves
  AND a **manual** `npm --prefix e2e run test` against a live compose stack
  (signup gate opened by the operator for the run — see §7 precondition)
  passes **both** specs.
- **F19.U3 (Flow B + the shared-helper extraction):** add
  **`e2e/specs/post_write.e2e.ts`** (§2.2, the sample-resident sign-in + the
  shared write leg), and if U2 inlined the write leg, **extract it into
  `e2e/helpers/`** and have both specs call it (one body, two callers — the
  DRY the split's point is). **≤ 2 files.** Exit: both specs green, **and
  the two specs' write-leg bodies are the SAME function** (a reviewer can
  point at one `helpers/` file).
- **F19.U4 (CI wiring — OPEN ITEM, owner/CI step):** extend the **F14 CI gate**
  (`.github/workflows/ci.yml`, F14.U2) with an E2E job: `docker compose up
  -d` (the stack), wait for readiness (§1), `npm --prefix e2e ci &&
  npm --prefix e2e run test`, **and** record the §2.1 signup-gate
  precondition as a **documented CI step** (a setup step that opens the gate
  via the admin surface *for CI only*, or a pinned note that the E2E job
  requires the gate open). **This is an open item — recorded in the handoff
  note, NOT executed in U2/U3** (the unit-series rule 5: `code+ops`-shaped
  end state; F19 is `code` but the CI leg is the F14 gate's surface, and the
  gate's *shape* — "two `dotnet exec` dlls" — is F14's landed contract, so
  U4 **adds** a third job rather than reshaping F14's).

**The split's invariant (held by all three):** each unit touches **only**
`e2e/**` (+ the one handoff-note append); **no `src/**`, no `<PackageReference>`,
no migration, no `Kumunita.slnx`** (the .NET solution stays the F14 gate's
"two dlls" shape — the E2E project is deliberately outside it).

---

## §5 — Invariants (held by F19.U2/U3/U4)

1. **Design-only U1:** this unit lands **exactly one** doc file — no `src/**`
   change, no `<PackageReference>`, no migration, no `.slnx` change, no build,
   no test run, **no `e2e/` directory created** (that is U2).
2. **E2E is a SEPARATE concern from the xunit suites** (§1) — **not** added to
   `Kumunita.slnx`, **not** invoked by `dotnet test` / the `dotnet exec` path,
   **not** a dependency of the .NET build. The F14 gate's landed shape
   (two `dotnet exec` dlls) is **unchanged** until U4 (open item) **adds** a
   third CI job; it is never reshaped.
3. **Grounded in the REAL compose stack** (§1): `:5080` app, **`:8025`
   Mailpit** (the compose-published port; `:5099` is a *different instance*
   per repo memory and is **not** the suite's evidence), `/health` readiness,
   the `~8 s` boot, the real view field names (`name="Email"`,
   `name="Password"`, `name="Body"`, `name="ComponentId"`, etc.), the real
   verify-link shape (`/account/verify?id={rowId}`, `IdentityService`), the
   real routes (`/account/signup`, `/account/login`, `/posts/new`,
   `/profile/edit`, `/community`).
4. **Re-runnable + unambiguous evidence:** a **unique email per run**
   (local-part randomized, `examplium.com` domain — known-good, not in the F9
   blocklist) + a **unique post Title/Body per run** (timestamp-suffixed) →
   the Mailpit extraction (`To` filter) and the post assertion (Title match)
   are unambiguous on a re-used compose volume. No `docker compose down -v`
   between runs (the volume + seeded sample data persist — the repo's
   "volumes persist" boot idiom).
5. **The suite asserts; it does not mutate platform config** (§2.1
   precondition): the ADR 0050 signup gate is **checked, not opened**, by the
   suite. Opening it is an owner/admin step (recorded as a §7 open item + a
   U4 CI-setup concern). The suite's only writes are the *resident's own*
   account + post — the flow's subject, not the platform.
6. **No lockout-tripping login loops** (§1): one login POST per spec run; a
   **503 (ASP.NET Identity lockout)** is a **precondition/environment
   failure**, recorded with the doctrine's "STOP retrying" note, never
   retried in a loop (repo memory `env-docker-ts7-and-portbind`).
7. **The rendered HTML is the evidence** (the repo's Razor-verification
   doctrine): every flow's terminal assertion is on the **rendered** page
   (the `/profile/edit` URL, the `/community`/post-page Title), **not** on a
   `.cshtml` source read or a DB query. The suite never opens Postgres.
8. **Owner/CI steps recorded, not assumed done** (§4 U4, §7) — the CI wiring
   and the signup-gate open step are **open items** in the handoff note.

---

## §6 — Open items (owner/CI, recorded, NOT assumed done)

1. **The ADR 0050 signup gate must be OPEN for Flow A to run.** The live
   compose instance is **currently closed** (verified 2026-10-24: the
   `/account/signup` GET returns the `SignupClosed` notice, the form's
   `Email` input is absent). Flow A **fails fast with a named message**
   (§2.1 precondition) until the operator opens the gate (the admin
   settings surface — the `SetSignupOpenAsync` write lane). This is the
   **single blocking precondition** for the whole lane's green; it is
   recorded, not assumed.
2. **F19.U4 — the CI wiring** (extend the F14 gate with the E2E job, §4) —
   an owner/CI step; recorded, not executed in U2/U3.
3. **The sandbox port-bind quirk** (repo memory `env-docker-ts7-and-portbind`:
   the agent sandbox cannot bind a *new* localhost port, `Errno 98`) — if
   `:5080`/`:8025` are not reachable from this sandbox's browser/Playwright
   (the "different instance" / `+1`-port-rewrite quirks), U2 records a
   **Drift pause** and does **not** silently re-target `:5099` (a different
   app) — the compose file is the ground truth (§1).

---

## §7 — Handoff-note contract for F19.U1

This unit appends **exactly one** `## F19.U1` section to
`docs/plans-milestones/in-progress/prod-hardening/hardening-handoff-notes.md`
recording: what landed (this one doc, nothing else), the **five pin targets**
(§1 the runner — Playwright against `:5080` + Mailpit **`:8025`**, a
**separate** concern from the xunit `dotnet exec` path; §2 the two flows with
the concrete steps + the **pinned Mailpit regex** + the **`:8025`-not-`:5099`**
port correction; §3 the **pinned spec names** `signup_verify_login.e2e.ts` /
`post_write.e2e.ts` + titles; §4 the U2/U3/U4 split, all under a new `e2e/`
project, **no `.slnx`/`src/**` change**; §5 the invariants), the **open
items re-stated** (§6 — the signup-gate precondition, the U4 CI wiring, the
sandbox port quirk), and the forward pointer: **F19.U2 next** (the Playwright
harness + `signup_verify_login.e2e.ts`, reads §4 + §2.1 + §3) — the lane stays
**open** (F19.U2–U4 + F20–F22 remain; **no 6-member close-flip**, that is
F22, the lane's last unit).
