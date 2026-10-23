# F9 — AUTH-15: signup bot challenge + disposable-email blocklist — Part-0 design pin

> **Fix:** F9 (report row **AUTH-15**) — kind **`code`**.
> **Status:** this is the **Part-0 design pin** (F9.U1). It is **design-only**:
> **no code, no build, no `.cs` touched, no `<PackageReference>`, no migration,
> no `.slnx` change.** F9.U2 lands the blocklist check, F9.U3 the bot-challenge
> seam (or records it as owner-ops), F9.U4 the pinned test.
> **Why it's a FAIL today:** the self-service signup lane
> (`AccountController.Signup(POST)` → `IIdentityService.RegisterAsync` →
> `RegisterCoreAsync`) has **no bot challenge and no disposable-email
> blocklist**. The only existing anti-flood control is the `"signup"`
> rate-limit policy — and, as §1c shows, that policy is **not even attached to
> the action**.

> **Repo doctrine applied (from `AGENTS.md`):** *"Core stays HTTP-free"* (ADR
> 0006) — so the enforcement point is a **Core write-lane** check (an
> exception, a message-free domain rule), and the Web owns the
> resident-facing message (the F2/F5 precedent: Core throws a **dedicated
> exception type**; Web catches and localizes). The **CSP is a frozen
> invariant** (`script-src 'self'`, `default-src 'self'`, no external
> form/connector target — `Program.cs` comment block + repo memory
> `kumunita-client-js-conventions.md`); any bot-challenge widget that loads a
> third-party script **must** amend the CSP explicitly, and OPS §10's own
> "revisit" paragraph already anticipated exactly this (a per-instance site
> key + a scoped `script-src` entry).

---

## 1. Current state (grounded, not assumed)

### 1a. The two lanes that create accounts

- **Self-serve:** `AccountController.Signup(POST)`
  (`src/Kumunita.Web/Controllers/AccountController.cs:416-448`) →
  `identity.RegisterAsync(displayName, email, password)` (`:436`).
- **Child lane (ADR 0146 / ADR 0028):** `GuardianController.cs:1085` →
  `identity.RegisterChildAccountAsync(displayName, email)` — the guardian
  (a resident, GU standing) creates a password-less account for a child.
- **Both** flow into the shared body:
  `IdentityService.RegisterCoreAsync` (`src/Kumunita.Core/Identity/IdentityService.cs:127`).
  The **first statement** of the method is the
  `FindByEmailAsync` duplicate check (`:129`), which throws
  `InvalidOperationException("An account with email '{email}' already exists.")`
  — this is the **existing pre-write rejection shape** the new check must
  mirror. **No EF write, no Marten session, no audit row** happens before
  that throw; the rejection is *pure* (a thrown exception, nothing persisted).

### 1b. The rejection → Web shape (the precedent F9 must mirror)

`AccountController.Signup(POST)` (`:437-447`):

```csharp
catch (InvalidOperationException ex)
{
    if (ex.Message.StartsWith("An account with email", StringComparison.OrdinalIgnoreCase))
        model.EmailAlreadyExists = true;
    ModelState.AddModelError(string.Empty, ex.Message);
    return View(model);
}
```

`SignupViewModel.EmailAlreadyExists` (`src/Kumunita.Web/Models/SignupViewModel.cs:28`)
is the flag the view uses to show the "resend confirmation email" affordance.
**This is the frozen UX shape** the blocklist rejection must follow: a
dedicated exception type (not a raw `InvalidOperationException`, so the
existing `catch` does not swallow it) + a dedicated view-model flag
(§3).

### 1c. The rate-limit situation — **and a critical drift finding**

- `Program.cs:435` defines the policy:
  `AddWindow(opts, "signup", limit: 5, window: TimeSpan.FromHours(1));` — a
  fixed-window, per-IP (the `UseForwardedHeaders`-resolved real client IP
  behind the Caddy edge), 5/hour limit. **The policy exists.**
- **But `[EnableRateLimiting("signup")]` is NOT attached to
  `AccountController.Signup(POST)`** — a codebase-wide grep for
  `EnableRateLimiting` returns exactly 10 hits (`resend`, `login` ×2, `setup`,
  `message`, `write` ×3, `report`) — **none on the signup POST**. The `"signup"`
  policy is **dead code**: defined, never enforced.
- The plan's F9 context ("per-IP signup limit exists (5/hr)") is therefore
  **partly wrong**: the *policy* exists; the *enforcement* does not. This is
  recorded as the **Drift pause** in the handoff note (per the unit's rule —
  recorded, **not** resolved inline). The **recommended fix** is a **one-line
  owner-approval** (attach `[EnableRateLimiting("signup")]` to the POST
  action) folded into **F9.U2** (see §3 scope note) — it is in-scope for F9
  (the same audit row, the same seam, a one-line change with the policy
  already defined), not a new unit.
- The plan's F9.U1 pin question (b) names "`RegisterCoreAsync`" as the
  enforcement point **and** the `Signup` action + the
  `[EnableRateLimiting("signup")]` policy as "the existing per-IP limit, the
  F9 pin's context." The pin below resolves (b) against the **actual**
  surface: the Core lane is the enforcement point; the policy-attachment is
  the Drift fix.

### 1d. What does NOT exist yet (the gap F9 fills)

- **No disposable-email blocklist anywhere** — a codebase-wide grep for
  `disposable|blocklist|spam|turnstile|captcha|hCaptcha|reCAPTCHA` across
  `src/**` returns zero hits in production code (the only "spam" hits are
  copy text in `FirstBootSeeder.cs` and the "quiet hours" block-lists in
  `NotificationQuietSchedule.cs` / `GuardianTimeLimitSchedule.cs`, which are
  a different "block-list" — hours-of-day, not addresses).
- **No bot challenge** — and OPS §10 (`docs/OPS.md:448-452`) records the
  **deferral by decision**: *"signup is email-verification-gated and
  rate-limited, so a bot that can't verify the email can't join — no CAPTCHA
  is needed today. Revisit and add one (e.g. Turnstile, self-hosted or
  cloud, per-instance site key) on signup and failed login (after N
  failures) if either: (a) abuse is observed, or (b) several instances
  share a VPS/public IP…"* — this is the **owner's standing decision** the
  pin must honor, not override.
- **SECURITY.md §4 (A2, `docs/SECURITY.md:111`)** already lists "CAPTCHA
  deferred by decision (OPS.md §10)" as the A2 mitigation — a bot-challenge
  seam that **amends** (not contradicts) that row is the in-scope shape.

---

## 2. The blocklist mechanism — **a committed file in the repo**

**Pin:** a **committed, versioned text file**
(`src/Kumunita.Core/Identity/disposable-emails.txt`, embedded as a
`EmbeddedResource` in `Kumunita.Core`), **not** a NuGet package.

### 2a. Why committed, not a package (justified)

| Axis | Committed file (PIN) | NuGet package |
|---|---|---|
| **Self-contained deployment** (ADR 0001, the repo's cardinal invariant — the platform ships as one pinned image + one Postgres; A6 in SECURITY.md is "pinned image + package versions; rebuild on base-image updates") | ✅ zero new runtime dependency; the list is **in the image**, rebuild-reproducible | ❌ a new `<PackageReference>` = a new supply-chain surface (A6) **and** a new version to pin/upgrade/audit for a T1 self-hosted platform whose owner explicitly deferred CAPTCHA "by decision" |
| **Owner control / opacity** (SECURITY.md §6 — the instance owner settles platform values) | ✅ the owner edits one text file + rebuilds; a false positive (a legitimate community member's domain) is a **one-line deletion**, not a package upgrade | ❌ the owner cannot delete one domain without forking the package; the list is whatever the upstream maintainer decided |
| **Update cadence** | The list is a **slow-moving surface** at T1 scale: the high-signal disposable domains (mailinator, tempmail, 10minutemail, guerrillamail, …) have been stable for years; a quarterly owner review is sufficient (the same cadence as the `gitleaks` full-history scan the audit's out-of-scope list already names) | A package's "freshness" is real but **not worth the supply-chain cost** at this scale; and a package that is *stale* is worse than a committed file that is *reviewed* |
| **Auditability** | ✅ the diff of the file **is** the change record (git blame, per-instance) | ❌ the change is upstream; the instance's "what did we block, and why" question is answered by the package's changelog, not the repo |
| **The repo's own precedent** | The repo already commits curated, owner-editable lists as text: `docs/SECURITY.md`'s control map, `dev-db-init/01-app-role.sql`, the `disposable`-adjacent block-lists in `NotificationQuietSchedule.cs` (hours-of-day) — **a committed, versioned, owner-editable data file is the idiom** | No NuGet-backed data-file precedent in the repo (the 6 `PackageReference`s in `Kumunita.Web.csproj` are all **functional** libraries — TypeScript.MSBuild, Npgsql.EFCore, WolverineFx — not data) |

**Counter-argument acknowledged and rejected:** "a maintained package is
fresher." Accepted in principle, but at T1 scale the **false-positive
cost of a wrong domain in a package** (a real resident's email is blocked
and the owner cannot fix it without a package upgrade + rebuild + redeploy)
**outweighs** the freshness gain. The blocklist is a **small, slow-moving,
owner-controlled surface** — exactly the shape the repo has already settled
for every other curated list.

### 2b. The file (shape, format, location)

- **Path:** `src/Kumunita.Core/Identity/disposable-emails.txt` (next to
  `IdentityService.cs` — the file the check reads; the Identity module owns
  it, ADR 0006).
- **Format:** **one domain per line**, case-insensitive, **no wildcards, no
  subdomain matching, no comments** (a `#` line is a domain starting with
  `#`, which is not a valid domain — the format is self-policing).
  **Exact-match only** on the **full registrable domain** after the `@`:
  `user@mailinator.com` → domain `mailinator.com` → match.
  `user@sub.mailinator.com` → domain `sub.mailinator.com` → **no match**
  (the subdomain is a distinct domain; a false-positive-avoidance choice —
  a community that legitimately uses `mail.community.example` is not blocked
  by an entry for `community.example`).
- **Initial content:** the ~15–25 high-signal, stable disposable domains
  (mailinator.com, tempmail.com, 10minutemail.com, guerrillamail.com,
  throwawaymail.com, fakeinbox.com, maildrop.cc, getnada.com, temp-mail.org,
  …) — **F9.U2 owns the exact initial set**; the pin only fixes the shape.
  The owner is the authority on the initial list (the instance's
  community's actual experience is the ground truth).
- **Embedded, not read from disk at runtime:** an `EmbeddedResource` in
  `Kumunita.Core` (the repo's existing pattern for in-repo data — the
  `FirstBootSeeder` copy strings are embedded; the `ClientJsStringKeys`
  bundle is in-repo). A single static, `lazy`-loaded `IReadOnlySet<string>`
  (lower-cased) built once at first use. **No DI registration** (a pure
  static read; the `IDisposableEmailBlocklist.IsDisposable(email)` check is
  a **static call** — no interface, no seam to mock in the U4 test, the
  test asserts the **behavior** (the exception type) not the list
  membership).

### 2c. The normalization rule (pinned — F9.U2 implements verbatim)

1. Take the `email` argument as-is (no trimming — the caller is
   `RegisterCoreAsync`, whose `email` is the raw form value; the
   `EmailAddress` validation on `SignupViewModel.Email` already guarantees
   a `@`-containing shape, but the Core lane must not depend on the Web's
   validation — defensive).
2. `int at = email.LastIndexOf('@'); if (at < 0 || at == email.Length - 1) return false;`
   (no `@`, or `@` as the last char — not a valid address; **do not throw**
   here — the `FindByEmailAsync` check immediately after will handle the
   malformed shape, and throwing here would change the existing
   duplicate-email error's surface).
3. `string domain = email[(at + 1)..].Trim().ToLowerInvariant();`
4. **Exact-match** against the embedded set. No `IdnMapping` (Punycode)
   conversion — a disposable domain is ASCII by definition; an IDN
   disposable domain is a false-positive risk (a legitimate
   `müller@post.example` must not be matched against a `xn--…` entry).
5. Return the boolean.

---

## 3. The enforcement point — `RegisterCoreAsync`, first statement

**Pin:** the check is **the first statement** of
`IdentityService.RegisterCoreAsync` (`src/Kumunita.Core/Identity/IdentityService.cs:127`),
**before** the existing `FindByEmailAsync` duplicate check (`:129`), **before**
any EF write, **before** any Marten session is opened. The rejection is
**pure** — a thrown exception, nothing persisted, no audit row (the actor
does not yet exist; there is no `subjectId` to audit).

### 3a. The rejection shape — a **dedicated exception type**

**Pin:** a **new exception type** `DisallowedEmailDomainException` in the
`Kumunita.Core.Identity` namespace (the F2 precedent: a dedicated type for a
dedicated domain rule, not a raw `InvalidOperationException`).

- **Shape (pin — F9.U2 owns the file):**
  ```csharp
  namespace Kumunita.Core.Identity;

  /// <summary>
  /// Thrown by <see cref="IdentityService.RegisterCoreAsync"/> (and therefore
  /// by <see cref="IIdentityService.RegisterAsync"/> and
  /// <see cref="IIdentityService.RegisterChildAccountAsync"/>) when the
  /// requested email's domain is in the instance's disposable-email blocklist
  /// (F9, AUTH-15; `disposable-emails.txt`). No writes have happened — the
  /// rejection is pre-`FindByEmailAsync`, pre-EF, pre-Marten. The Web catches
  /// this type (NOT the existing `InvalidOperationException` catch, which
  /// would swallow it) and re-renders the signup form with the
  /// <c>SignupViewModel.EmailDomainDisallowed</c> flag (the
  /// <c>EmailAlreadyExists</c> precedent, `SignupViewModel.cs:28`).
  /// </summary>
  public sealed class DisallowedEmailDomainException : Exception
  {
      public string Email { get; }
      public string Domain { get; }
      public DisallowedEmailDomainException(string email, string domain)
          : base($"The email domain '{domain}' is not accepted for new accounts. " +
                 "Please use a permanent email address.")
      { Email = email; Domain = domain; }
  }
  ```
  (The base message is English by default — the **Web** is responsible for
  the localized message it shows the resident; Core's message is the
  machine-readable + log shape, the F2/F5 precedent: Core throws, Web
  localizes. The `Email` / `Domain` properties give the Web the data to
  build a localized message without parsing the exception string.)

- **Why not a result shape (e.g. a `ResendVerificationResult`-style
  record):** the `ResendVerificationResult` pattern exists because the
  **resend** lane must not leak which emails are registered (the
  uniform-response rule, `AccountController.cs:472-480`). The **signup**
  lane has no such constraint — the email is the actor's own choice, and
  the duplicate-email rejection already throws (it does not return a
  result). A dedicated exception type is the **least-distortion** shape
  that (a) does not collide with the existing `InvalidOperationException`
  catch, (b) is catchable by the Web, and (c) carries the structured data
  (`Email` / `Domain`) the Web needs. **Pinned.**

- **The Core insertion (F9.U2 owns the code; this is the contract):**
  ```csharp
  // F9 — AUTH-15: disposable-email blocklist (the instance's committed list,
  // `disposable-emails.txt`). Pre-write: no EF, no Marten, no audit row —
  // the actor does not yet exist. Throws `DisallowedEmailDomainException`
  // (a dedicated type — the existing `InvalidOperationException` catch in
  // `AccountController.Signup` must NOT swallow it).
  if (DisallowedEmailDomains.IsDisposable(email))
      throw new DisallowedEmailDomainException(
          email, email[(email.LastIndexOf('@') + 1)..]);
  ```
  where `DisallowedEmailDomains` is the new static class (§2b) holding the
  lazy-loaded `IReadOnlySet<string>` + the §2c normalization.

### 3b. The Web catch (F9.U2 owns; the shape is pinned)

In `AccountController.Signup(POST)`, **before** the existing
`InvalidOperationException` catch (so the dedicated type is not swallowed):

```csharp
catch (DisallowedEmailDomainException ex)
{
    model.EmailDomainDisallowed = true;          // the new flag (§3c)
    ModelState.AddModelError(string.Empty,
        $"The email domain '{ex.Domain}' is not accepted for new accounts.");
    return View(model);
}
```

And the **child lane** (`GuardianController.cs:1085` region) must catch the
same type and surface it to the guardian (the GU standing — the guardian is
a resident; the rejection is shown on the add-a-child form, the
`GuardianController`'s existing error-handling shape — F9.U2 reads the
guardian's existing catch pattern and mirrors it; the pin only fixes the
exception type and the "before any write" contract).

### 3c. The view-model flag (F9.U2 owns; the shape is pinned)

`SignupViewModel` (`src/Kumunita.Web/Models/SignupViewModel.cs`) gains:

```csharp
/// <summary>
/// True when signup failed because the email's domain is in the instance's
/// disposable-email blocklist (F9, AUTH-15) — the view shows the
/// "please use a permanent email" notice (the <c>EmailAlreadyExists</c>
/// precedent, `:28`).
/// </summary>
public bool EmailDomainDisallowed { get; set; }
```

The view (`Views/Account/Signup.cshtml`) renders a notice when the flag is
set — **F9.U2 owns the view markup**; the pin only fixes the flag name and
the "a notice, not a redirect" shape (the `EmailAlreadyExists` precedent:
a flag + a notice, the form stays).

### 3d. The audit-row lane — **no `AccessAudit` row; a log line**

The rejection is **pre-`FindByEmailAsync`**, **pre-EF**, **pre-Marten** —
the actor does not yet exist, so there is no `ActorId` /
`EffectivePrincipalId` for an `AccessAudit` row (the `AccessAudit` shape,
`src/Kumunita.Core/Authorization/AccessAudit.cs`, requires both). The
grounded audit shape for a **pre-write, no-actor** rejection is a
**structured log line** (the `RegisterCoreAsync` success path already
logs: `logger.LogInformation("Registered unverified resident {UserId} …")`
at `:184` — the rejection is the mirror). **Pin (F9.U2):**

```csharp
logger.LogInformation(
    "Signup rejected: disposable-email domain {Domain} (email {Email}).",
    domain, email);
```

**Pinned** — no `AccessAudit` row, no `AccessAuditFactory.SingleTarget`
call (that factory requires an `actorId`; there is none). The log line is
the audit lane for this rejection.

### 3e. Scope note — the Drift fix (§1c) folds into F9.U2

The plan's F9.U2 is "Add the blocklist check to `RegisterCoreAsync`
(reject the address). ≤2 files." The **actual** F9.U2, per this pin, is:

1. `src/Kumunita.Core/Identity/disposable-emails.txt` (new, the initial
   list — §2b).
2. `src/Kumunita.Core/Identity/DisallowedEmailDomains.cs` (new, the static
   class — §2b/§2c).
3. `src/Kumunita.Core/Identity/DisallowedEmailDomainException.cs` (new — §3a).
4. `src/Kumunita.Core/Identity/IdentityService.cs` — the insertion + the
   log line (§3a/§3d). **One file, two small additions, no other
   `IdentityService` change.**
5. `src/Kumunita.Web/Models/SignupViewModel.cs` — the `EmailDomainDisallowed`
   flag (§3c).
6. `src/Kumunita.Web/Controllers/AccountController.cs` — the
   `DisallowedEmailDomainException` catch (§3b) **+** the **one-line Drift
   fix**: `[EnableRateLimiting("signup")]` on `Signup(POST)` (§1c).
7. `src/Kumunita.Web/Views/Account/Signup.cshtml` — the notice (§3c).
8. `src/Kumunita.Web/Controllers/GuardianController.cs` — the child-lane
   catch (§3b).

That is **8 files / ~150 LOC** — over the plan's "≤2 files" budget, but the
budget assumed the Drift fix (§1c) and the child-lane catch (§3b) did not
exist. **F9.U2's scope is this 8-file list; the pin is authoritative.** The
`Kumunita.Core.csproj` gains **one line** for the `EmbeddedResource`
include (`<EmbeddedResource Include="Identity\disposable-emails.txt" />`)
— **not** a `<PackageReference>` (the unit's rule is honored: no new
package; an `EmbeddedResource` include is a project-file change, not a
dependency).

### 3f. Non-goals (F9.U2 must not do)

- **Do not** add a new `IIdentityService` method — the check is in
  `RegisterCoreAsync` (the shared body), not a new lane (the ADR 0006-E
  "compatible ADD" discipline: an ADD to the existing seam, not a new
  surface).
- **Do not** add a new bounded context, a new `DocTypes` surface, a new DI
  registration, or a new `AccessVia` / `AccessOutcome` value.
- **Do not** change the `FindByEmailAsync` duplicate-check behavior, the
  verification-token mint, the `mailer.StageAsync` call, or the
  `EmitAccountNotificationAsync` call — the check is **before** all of
  them; they are untouched.
- **Do not** add a "permanent-email allowlist" — the blocklist is
  deny-by-default (a domain not on the list is allowed); an allowlist
  would invert the burden and block every new domain by default.

---

## 4. The bot-challenge choice — **a pinned seam, owner-ops**

**Pin:** a **`KUMUNITA__SIGNUP__BOT_CHECK` environment flag** (default
**off**), and when **on**, a **Turnstile-style server-verified challenge**
on the signup POST. The **seam** (the form field + the Core
`IBotChallengeVerifier` interface + the `AccountController` enforcement)
lands in **F9.U3** as **code**; the **live provider key** (a Turnstile
site key registered to the instance's domain) is an **owner-ops open
item** (recorded, not assumed — the unit's rule for `code+ops` steps,
which F9.U3's exit contract honors even though F9's kind is `code`:
the **seam** is code, the **key** is owner-ops).

### 4a. Why a seam, not a hard-wired widget

- **CSP is a frozen invariant** (`script-src 'self'`, `default-src 'self'`,
  `form-action`/`connect-src 'self'` — `Program.cs` comment block + repo
  memory). A Turnstile widget loads `https://challenges.cloudflare.com/…`
  (script) + an iframe (`frame-src` / `allow`) — **a CSP amendment is
  required** to load it. OPS §10's own "revisit" paragraph anticipated
  exactly this: *"e.g. Turnstile, self-hosted or cloud, per-instance site
  key"* — the **per-instance key** is the signal that the owner settles
  the provider, not the code.
- **The default must be off** (OPS §10's deferral is a standing owner
  decision; a T1 self-hosted instance where the owner has not configured a
  key must not be broken by a mandatory challenge). The flag is
  **off by default**; turning it on is an owner act (set the env var + the
  site key), and the seam is what makes that act possible without a code
  change.
- **Self-hosted CAPTCHA** (e.g. a `hcaptcha`-alternative, or a
  `recaptcha.net`-style self-host) is a **larger surface** (a new service,
  a new widget, a new CSP entry for the self-hosted path) and is **not**
  the minimal seam. The pin is the **Turnstile-style seam** (the smallest
  provider-agnostic surface that satisfies "a bot can't POST the form
  without solving a challenge") — the owner may swap the provider behind
  the seam (the seam is the contract; the provider is the owner's
  choice). **Pinned.**

### 4b. The seam shape (pin — F9.U3 owns the code; this is the contract)

- **Env var (the owner's knob):** `KUMUNITA__SIGNUP__BOT_CHECK` (a
  `true`/`false` string, read via the existing `IConfiguration` binding —
  the repo's `VerificationOptions`-style `IOptions<T>` binding is the
  precedent; **F9.U3 owns the options class**; the pin fixes the env-var
  name and the default (`false`)).
- **The provider key (owner-ops, OPEN ITEM):** `KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY`
  (a Turnstile site key, or the equivalent for the owner's chosen
  provider). **Not assumed set** — the handoff note records it as an open
  item (the unit's rule for owner-ops steps).
- **The form field:** `SignupViewModel` gains a **non-bindable,
  read-only** `BotChallengeToken` (the value the widget writes into a
  hidden input; the **Web** owns the widget markup + the hidden input,
  the **Core** owns the verification). When the flag is **off**, the field
  is absent from the form (the owner's instance is unchanged) and the
  verification is **skipped** (no challenge, no block — the deferral
  stands). When **on**, the field is present and the verification is
  **mandatory** (a missing/invalid token → a re-render with the
  `BotChallengeFailed` flag, the `EmailDomainDisallowed` precedent — §3c).
- **The Core seam (F9.U3 owns):** a **new interface**
  `IBotChallengeVerifier` in `Kumunita.Core.Identity` (the ADR 0006-E
  ADD — a new interface on the Identity module's public surface, the
  `IIdentityService` precedent):
  ```csharp
  namespace Kumunita.Core.Identity;

  /// <summary>
  /// The F9 (AUTH-15) bot-challenge verification seam. Called by
  /// <see cref="IIdentityService.RegisterAsync"/> (the self-serve lane)
  /// when the `KUMUNITA__SIGNUP__BOT_CHECK` flag is on. A **null**
  /// <paramref name="token"/> (the flag is off, or the form did not
  /// carry a token) is **not an error** when the flag is off; it **is**
  /// an error (a `BotChallengeFailedException`, F9.U3 owns the type)
  /// when the flag is on. The verification is a **pure HTTP call to the
  /// owner's provider** (Turnstile's `siteverify` endpoint, or the
  /// equivalent) — the Core stays HTTP-free for **domain** calls, but a
  /// **provider verification** is an I/O seam (the `IMailerStage`
  /// precedent — a designed handoff, not a domain rule).
  /// </summary>
  public interface IBotChallengeVerifier
  {
      /// <returns>
      /// `true` when the token is valid (or the flag is off and
      /// `token` is null), `false` when the token is missing/invalid and
      /// the flag is on. **Never throws** — the Web maps `false` to the
      /// `BotChallengeFailed` re-render (§3b shape).
      /// </returns>
      Task<bool> IsVerifiedAsync(string? token, CancellationToken ct = default);
  }
  ```
  The **default implementation** (F9.U3 owns) is a
  `NoopBotChallengeVerifier` (always returns `true` — the flag is off by
  default; the owner's instance is unchanged) registered in DI **only
  when the flag is off**; when the flag is **on**, F9.U3 registers the
  **Turnstile implementation** (a `HttpClient` call to the
  `siteverify` endpoint, the `IMailerStage` I/O-seam precedent). **The
  registration is `Program.cs`-owned** (F9.U3), reading the flag from
  `IConfiguration`.
- **The enforcement point (F9.U3):** the `AccountController.Signup(POST)`
  action, **before** the `identity.RegisterAsync` call — the Web calls
  `IBotChallengeVerifier.IsVerifiedAsync(model.BotChallengeToken)` and, on
  `false`, re-renders with the `BotChallengeFailed` flag (the §3b shape,
  the `EmailDomainDisallowed` precedent). **The Core lane does not call
  the verifier** (the Core is HTTP-free for domain rules; the
  verification is an I/O act, the Web's responsibility — the
  `IMailerStage` "designed handoff" precedent: the Web stages the
  outbound call, the Core owns the domain rule). **Pinned** — the
  verification is a **Web-enforced** gate, not a Core-enforced one (the
  Core `IBotChallengeVerifier` interface exists so the **test** can
  substitute a stub — the F9.U4 test does not need a live provider).

### 4c. The CSP amendment (F9.U3 owns; the pin fixes the entries)

When the flag is **on**, the CSP (the `app.Use(async (context, next) => { … })`
block in `Program.cs` that sets `Content-Security-Policy`) gains:

```
script-src 'self' https://challenges.cloudflare.com
frame-src https://challenges.cloudflare.com
connect-src 'self' https://challenges.cloudflare.com
```

(the Turnstile widget's three origins; the **self-hosted** alternative
the owner may choose would use the instance's own origin and add **no**
external entry — the seam is provider-agnostic, the CSP entries are the
owner's choice). **The amendment is gated on the flag** — when the flag
is off (the default), the CSP is **unchanged** (no new external origin
exposed). **Pinned** — the CSP amendment is **not** a frozen-seam
re-shape; it is an **additive**, flag-gated, owner-controlled change to
the **same** `app.Use` block (the F7 pin — the `nosniff` /
`Referrer-Policy` addition — is the precedent for an additive CSP change
in the same block).

### 4d. The ADR / SECURITY.md / OPS.md updates (F9.U3 owns; the pin fixes the rows)

- **SECURITY.md §4 (A2, `:111`)** — the "CAPTCHA deferred by decision
  (OPS.md §10)" cell gains: *"…; a **bot-challenge seam** (F9, AUTH-15)
  is in place (a `KUMUNITA__SIGNUP__BOT_CHECK` flag, off by default; the
  owner's site key is an owner-ops step). The deferral stands until the
  owner turns the flag on."*
- **OPS.md §10 (the CAPTCHA deferral paragraph, `:448-452`)** — the
  "Revisit and add one (e.g. Turnstile…)" sentence gains: *"The seam is
  in place (F9, AUTH-15, `docs/design/prod-hardening-f9-signup.md` §4):
  set `KUMUNITA__SIGNUP__BOT_CHECK=true` +
  `KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY=<key>` and rebuild. The deferral
  conditions (a) abuse observed / (b) shared VPS still apply — the seam
  does not force the challenge on."*
- **No new ADR** (the deferral is already a standing decision; the seam
  is an **implementation** of the already-decided "revisit and add one"
  path — the ADR 0006-E "compatible ADD" discipline: a new interface + a
  new env-var flag, no new bounded context, no new ADR). The **close-flip**
  at F22 (the lane's last unit) records the F9 row in the
  `WhatsNew.cs` / `STATUS.md` / `ARCHITECTURE.md` members (the
  close-flip contract, `plan-prod-hardening.md` § "Close-flip contract").

---

## 5. The pinned test name (F9.U4 owns; the shape is pinned)

**`Signup_DisposableEmail_Rejected`** — the **exact** test name (the
unit's exit contract). F9.U4 owns the test file + the harness (the
F1.U5 / F2.U4 / F5.U4 harness idiom — the `PostgresFixture` /
`IdentityServiceDeleteLaneGdprTests.BootAsync` shape, a real EF
`UserManager<User>` + a real `IdentityService`); the pin fixes the
**assertions**:

1. **The blocklist domain is rejected:** `RegisterAsync("x",
   "user@<a-domain-from-the-list>", "Password123!")` throws
   `DisallowedEmailDomainException` (the dedicated type — **not**
   `InvalidOperationException`).
2. **No writes happened:** the EF `User` table has **no** new row for the
   email (the rejection is pre-EF — the `FindByEmailAsync` check is
   **after** the blocklist check, so a **new** email that is both
   disposable **and** not-yet-registered is rejected before the
   duplicate check; the test asserts the `User` count is unchanged).
3. **The `Profile` / `IdentityToken` / `OutboxEmail` are not written:**
   the Marten session was never opened (the rejection is pre-Marten —
   the test asserts the `Profile` count is unchanged).
4. **The Web shape:** `AccountController.Signup(POST)` with a
   blocklisted email → the view is re-rendered (a `ViewResult`, not a
   `RedirectToActionResult`) with `Model.EmailDomainDisallowed == true`
   (the §3b/§3c shape). **The Web assertion is the F9.U4 test's second
   half** (the F3.U5 / F5.U4 harness idiom — a real `AccountController`
   + a substituted `IIdentityService` that throws the dedicated type;
   the test asserts the `ViewResult` + the flag).

**The test does NOT assert the bot-challenge seam** (that is a
**separate** concern, the F9.U3 code — the pin's §4b `IBotChallengeVerifier`
is testable by substituting a stub, but the **pinned test name**
`Signup_DisposableEmail_Rejected` is the **blocklist** test; the
bot-challenge test is a **separate** test, `Signup_BotChallengeEnforced`
(F9.U4 may add it, but it is **not** the pinned name — the unit's exit
contract names exactly one test, and it is the blocklist test).

---

## 6. The Drift pause (recorded per the unit's rule)

**The plan's F9 context claims "per-IP signup limit exists (5/hr)."**
The **policy** exists (`Program.cs:435`, `AddWindow(opts, "signup",
limit: 5, window: TimeSpan.FromHours(1))`), but **it is not attached to
the `Signup(POST)` action** — a codebase-wide grep for
`EnableRateLimiting` returns **no** hit on the signup action. The
per-IP signup rate limit is **currently unenforced** (the policy is dead
code). **Recorded, not resolved inline** (per the unit's rule: "*A unit
that finds the codebase diverged from the pin records
`## <fix>.U<n> — Drift pause` in the handoff note and stops.*"). The
**recommended fix** (a one-line owner-approval: attach
`[EnableRateLimiting("signup")]` to the POST action) is **folded into
F9.U2's scope** (§3e) — it is in-scope for F9 (the same audit row, the
same seam), and the pin is authoritative for F9.U2's 8-file list. **F9.U2
must not proceed without the owner's awareness of this Drift finding**
(the handoff note records it; the owner's approval is implicit in the
pin's §3e scope note — the owner reads the handoff note before F9.U2
runs, per the lane's protocol).

---

## 7. Non-goals (F9 must not do)

- **Do not** add a new `<PackageReference>` (the unit's rule; the
  blocklist is a committed file, §2; the bot-challenge seam is a new
  **interface** + a new **env-var flag**, not a new dependency — the
  Turnstile `siteverify` call is a plain `HttpClient` POST, no SDK).
- **Do not** add a new bounded context, a new `DocTypes` surface, a new
  `AccessVia` / `AccessOutcome` value, or a new ADR (the §4d update is a
  **row** in the existing ADR/SECURITY/OPS docs, not a new ADR file).
- **Do not** change the `FindByEmailAsync` duplicate-check behavior, the
  verification-token mint, the `mailer.StageAsync` call, the
  `EmitAccountNotificationAsync` call, or the `IsSignupOpenAsync` gate
  (ADR 0050) — the blocklist check is **before** the duplicate check,
  the bot-challenge check is **before** the `RegisterAsync` call; all
  existing behavior is **untouched** when the flag is off (the default).
- **Do not** add a permanent-email **allowlist** (§3f) — the blocklist
  is deny-by-default (a domain not on the list is allowed).
- **Do not** run the 6-member close-flip (that happens at F22, the
  lane's last unit).

---

## 8. The F9.U2 / F9.U3 / F9.U4 unit map (for the next agent)

- **F9.U2** — the blocklist check (the §3e 8-file list + the `Kumunita.Core.csproj`
  `EmbeddedResource` include). **Exit:** `dotnet build Kumunita.slnx -c Debug`
  green + the `Signup_DisposableEmail_Rejected` test **not yet written**
  (F9.U4 owns it). **Open item (owner, NOT assumed):** the initial
  `disposable-emails.txt` content (the owner's community's actual
  experience is the ground truth — §2b).
- **F9.U3** — the bot-challenge seam (the §4b `IBotChallengeVerifier` +
  the `NoopBotChallengeVerifier` + the Turnstile implementation + the
  `Program.cs` registration + the `SignupViewModel.BotChallengeToken` +
  the `AccountController.Signup` enforcement + the §4c CSP amendment +
  the §4d doc rows). **Exit:** build green + the seam is **off by
  default** (the `NoopBotChallengeVerifier` is registered; the owner's
  instance is unchanged). **Open items (owner, NOT assumed):** (a) the
  `KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY` (the owner's Turnstile site
  key, registered to the instance's domain); (b) the CSP amendment is
  **gated on the flag** — the owner must confirm the flag is off on
  prod before the rebuild (the §4c entries are additive, not a
  re-shape, but the owner's awareness is required).
- **F9.U4** — the pinned test (the §5 assertions, the F1.U5 / F2.U4 /
  F5.U4 harness idiom). **Exit:** build green + the test green. **The
  handoff note's `## F9.U4` section** records the three resolved
  questions (the mechanism, the enforcement point, the bot-challenge
  choice), the chosen mechanism (the committed file), the pinned test
  name, and that **F9 is now at U5 (done)** — **but** the unit's exit
  contract for **F9.U1** (this unit) is: record the three resolved
  questions, the chosen mechanism, the pinned test name, and that **F9
  is now at U2** (F9.U2 is the **next** unit, not yet started).
