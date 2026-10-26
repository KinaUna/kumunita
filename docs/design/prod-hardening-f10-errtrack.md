# F10 — OBS-02: error tracker (Sentry) — Part-0 design pin

> **Fix:** F10 (report row **OBS-02**) — kind **`code+ops`**.
> **Status:** this is the **Part-0 design pin** (F10.U1). It is **design-only**:
> **no code, no build, no `.cs` touched, no `<PackageReference>`, no migration,
> no `.slnx` change, no build run** (the unit's exit contract). F10.U2 lands the
> `Sentry.AspNetCore` package + the `UseSentry` registration + the PII-scrubbing
> callback, F10.U3 the pinned test. The close-flip (six members) happens at
> **F22**, the lane's last unit — **not** F10.
> **Why it's a FAIL today:** the app has **no error tracker** — only the M31/M32
> issue form (`/report-a-problem`, the resident-facing lane) + structured log
> lines. Unhandled exceptions, unobserved task failures, and handler faults are
> surfaced to the operator **only** via the `/health` poll and the log stream;
> there is no remote capture, no PII-safe envelope, no per-error breadcrumbs.
> The standing owner deferral (no external telemetry by default) is honored by
> construction: **the DSN is unset by default** (the owner's instance is
> unchanged until they set `SENTRY_DSN` + rebuild — the F9.U3
> flag-off-deferred idiom, the §4c CSP additive + flag-gated precedent).

> **Repo doctrine applied (from `AGENTS.md` + the F9 precedent):**
> *"Core stays HTTP-free"* (ADR 0006) — so the registration and the
> PII-scrubber are **Web** concerns. The scrubber is a **Web** `SendCallback`
> on the Sentry options (a `Program.cs`-owned registration), **not** a Core
> interface. The `SmtpSender` / `IMailerStage` Core I/O-seam precedent is
> **not** the precedent for this unit — the scrubber never touches a Core type.
> The **one** `<PackageReference>` ADD this lane permits is `Sentry.AspNetCore`
> (pin §7) — the plan explicitly allows it; it is **F10.U2's job**, not F10.U1's.

---

## 1. Current state (grounded, not assumed)

- **No error tracker today.** `grep -rn "Sentry\|SentryDsn\|UseSentry\|AddSentry" src/`
  returns **zero** hits; `Kumunita.Web.csproj` carries **6** `PackageReference`s
  (TypeScript.MSBuild, Npgsql.EFCore.PostgreSQL, WolverineFx, WolverineFx.Marten,
  WolverineFx.RuntimeCompilation — 5 functional + the build tooling), **none** of
  which is an error-tracker. The `Kumunita.Web` project is `net10.0`
  (`Kumunita.Web.csproj:53`).
- **The `Program.cs` registration conventions the pin mirrors** (the F9.U3
  precedent, `Program.cs:320-350`):
  - The **DSN / flag read** uses `builder.Configuration["KUMUNITA:SIGNUP:BOT_CHECK"]`
    (single-bracket, `string`-typed, `string.Equals(…, "true",
    StringComparison.OrdinalIgnoreCase)` — the **exact** idiom for an
    owner-controlled boolean knob, `Program.cs:336-338`). F10's DSN read follows
    the same shape (a `string`, not a `bool`).
  - The **conditional registration** uses `builder.Services.AddSingleton<T>(sp => …)`
    with a flag-branching closure (the F9.U3 `IBotChallengeVerifier` precedent,
    `Program.cs:341-344`). F10's `UseSentry` call is a **middleware-pipeline**
    registration (a `builder.WebHost.UseSentry(…)` call), not a DI
    registration — but the **guard idiom** (a local `bool`/`string` read from
    `builder.Configuration`, a conditional call) is the same shape.
  - The **CSP middleware block** (`Program.cs:798-832`, the
    `app.Use(async (context, next) => { context.Response.Headers["Content-Security-Policy"] = csp;
    await next(); })`) is the **nearest** additive-header precedent — F7's
    `nosniff` / `Referrer-Policy` ride the **same** `context.Response.Headers[…]=…`
    shape in that block. **F10 adds no header**; the `UseSentry` call belongs in
    the **same region** of `Program.cs` (the `builder`-phase registration block,
    before `app.Build()`), not in the CSP middleware block.
- **The Web-harness idiom for `Program.cs`-wired assertions** (the F9.U4
  precedent, `tests/Kumunita.Web.Tests/F9U4_SignupTests.cs`): a **real
  controller** (the `AccountController` direct-construction idiom) with a
  `ServiceCollection`-backed `RequestServices` that registers the **one**
  seam-under-test (the `IBotChallengeVerifier`) **plus** the no-op
  `IUrlHelperFactory` / `ITempDataDictionaryFactory` (the
  `UploadGateTestSupport` no-op-factory idiom). **The F10.U3 test does NOT
  construct a `Program.cs` pipeline** (the `builder` phase is not unit-testable
  without a full host); instead it **exercises the one conditional
  registration** in isolation — the exact shape pinned in §5.

---

## 2. The `UseSentry` call shape — the exact pin (F10.U2 lands it)

**The registration is a Web concern** (ADR 0006 — Core stays HTTP-free; the
registration is a `Program.cs`-owned `builder`-phase call). **The DSN is
owner-ops** (the owner's Sentry DSN, registered to the instance's domain).
**When the DSN is unset, Sentry is a no-op** — the `builder.WebHost.UseSentry(…)`
call is **not executed**, the pipeline is byte-identical to today's (the F9.U3
flag-off-deferred idiom, the §4c CSP additive + flag-gated precedent).

**The exact `builder`-phase block F10.U2 lands** (pin §2a):

```csharp
// F10 (OBS-02, U2) — the error-tracker seam (pin §2): an owner-controlled,
// OFF-BY-DEFAULT Sentry capture. The standing owner deferral (no external
// telemetry by default) is honored by construction: the DSN is unset unless
// the owner sets SENTRY_DSN, so an unconfigured instance is byte-identical to
// before this seam (the UseSentry call is not executed, the pipeline is
// unchanged). Core is untouched (ADR 0006 — the registration + the PII
// scrubber are Web concerns; the scrubber is a SendCallback on the Sentry
// options, not a Core interface). The DSN is owner-ops (pin §5); when unset
// the instance is unchanged.
var kumunitaSentryDsn = builder.Configuration["SENTRY_DSN"];
if (!string.IsNullOrWhiteSpace(kumunitaSentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = kumunitaSentryDsn;
        // PII-scrubbing contract (pin §3): default PII off, network-log
        // breadcrumbs off, and a SendCallback that redacts the known
        // PII-bearing request surfaces (the never-leave-the-instance
        // invariant, the SmtpSender/IMailerStage Core I/O-seam precedent
        // adapted to a Web SendCallback).
        o.SendDefaultPii = false;
        o.AddNetworkLogBreadcrumbs = false;
        o.SendCallback = (e, _) => { /* pin §3b redaction */ return e; };
        o.AddInAppExclude("Kumunita.Web");
    });
}
```

**The DSN-binding contract** (pin §2b):

- **The DSN is read from `builder.Configuration["SENTRY_DSN"]`** (a
  `string`, `null`-or-`""` → **no DSN**, Sentry is a **no-op**). The **owner's
  instance is unchanged** until they set the key (the F9.U3 flag-off-deferred
  idiom, the §4c CSP additive + flag-gated precedent).
- **The guard is `!string.IsNullOrWhiteSpace(kumunitaSentryDsn)`** — a `null`,
  `""`, or whitespace-only DSN all mean **no DSN**. This is the **exact**
  idiom for an owner-controlled knob (the F9.U3 `string.Equals(…, "true", …)`
  shape, `Program.cs:336-338`, adapted to a `string`-typed DSN).
- **When the guard is `false`, the `builder.WebHost.UseSentry(…)` call is NOT
  executed** — the pipeline is byte-identical to today's (no `Sentry`
  middleware, no `ISentrySdk` / `ISentryEventSink` registration, no
  `/api/sentry` route). **This is the frozen invariant** — the F9.U3
  flag-off CSP byte-identity precedent, applied to the middleware pipeline.

**The `UseSentry` call shape** (pin §2c):

- **`builder.WebHost.UseSentry(o => …)`** — the **exact** call. The
  `builder.WebHost` prefix (not `builder.UseSentry`) is required because
  `Program.cs` uses the **`WebApplicationBuilder`** (a `builder.WebHost`
  extension surface — the F9.U3 `builder.Services` / `builder.Configuration`
  registration conventions confirm the `builder.WebHost` shape is available).
- **The `o` callback** is the **`SentryOptions`** configuration surface (the
  `SendDefaultPii` / `AddNetworkLogBreadcrumbs` / `SendCallback` /
  `AddInAppExclude` members are all on `SentryOptions`). **F10.U2 owns the
  callback body** (the §3b redaction); the **pin fixes the shape, not the
  body**.
- **`o.Dsn = kumunitaSentryDsn`** — the DSN is set **inside** the callback (not
  passed to `UseSentry` as a separate argument — the `UseSentry` extension
  method's `Dsn` is a `SentryOptions` property, not a method parameter).

**The `AddInAppExclude` line** (pin §2d):

- **`o.AddInAppExclude("Kumunita.Web")`** — the **stack-trace in-app filter**.
  Without this, Sentry's default in-app heuristic marks **all** `Kumunita.Web`
  frames as "in-app" (the Web assembly is the entry point), which floods the
  owner's Sentry dashboard with `Kumunita.Web` stack frames that are not
  actionable. The `AddInAppExclude("Kumunita.Web")` call tells Sentry's
  symbolication to treat `Kumunita.Web` as **not** in-app (the
  `Kumunita.Core` frames remain in-app — they are the domain). **This is a
  Sentry-options surface, not a code change** — F10.U2 lands it inside the
  `o => …` callback.

---

## 3. The PII-scrubbing contract — the exact pin (F10.U2 lands it)

**The scrubber is a Web concern** (ADR 0006 — Core stays HTTP-free; the
scrubber is a `SendCallback` on the Sentry options, a `Program.cs`-owned
registration, **not** a Core interface). The `SmtpSender` / `IMailerStage`
Core I/O-seam precedent is **the shape** (a thin I/O seam the Web owns the
registration of), **not the location** (the scrubber never touches a Core
type).

**The contract** (pin §3a):

- **`o.SendDefaultPii = false;`** — the **default** PII capture is off. This is
  the **Sentry default** (the `SendDefaultPii` option defaults to `false` in
  `SentryOptions`), but the pin **explicitly sets it** (a frozen invariant — a
  future `SentryOptions` default change must not silently enable PII capture).
- **`o.AddNetworkLogBreadcrumbs = false;`** — the **network-log breadcrumbs**
  are off. The `AddNetworkLogBreadcrumbs` option, when `true`, adds a
  breadcrumb for **every** outbound HTTP request (the `HttpClient` /
  `IMailerStage` SMTP calls, the `TurnstileBotChallengeVerifier` siteverify
  POST, etc.) — **each breadcrumb carries the request URL + the request
  headers** (the `Authorization` header on the siteverify POST is the
  `KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY` — a **secret** the owner's instance
  must not leak). Setting `AddNetworkLogBreadcrumbs = false` disables the
  network-log breadcrumbs **entirely** — no URL, no headers, no body. **This
  is the single most important PII-scrubbing line in the pin.**
- **A `SendCallback`** — the **`o.SendCallback`** member (a `Func<SentryEvent,
  SentryEvent>`). The callback is invoked **after** Sentry has built the
  `SentryEvent` (the request context, the breadcrumbs, the exception chain)
  and **before** it is sent to the Sentry server. **The callback is the
  last-chance redaction** — it can see the full event and redact any
  remaining PII. **F10.U2 owns the callback body** (the §3b redaction); the
  **pin fixes the contract, not the body**.

**The `SendCallback` redaction contract** (pin §3b — F10.U2 implements it, the
pin fixes the **shape**):

```csharp
o.SendCallback = (e, _) =>
{
    // Redact the known PII-bearing request surfaces (the
    // never-leave-the-instance invariant, the SmtpSender/IMailerStage
    // Core I/O-seam precedent adapted to a Web SendCallback). The
    // callback never throws (the Sentry SDK catches and logs a
    // SendCallback exception, but a redaction that throws is a
    // scrubber bug — the owner's instance is unchanged, the event is
    // sent unredacted, the PII leaks).
    try
    {
        RedactRequestHeaders(e.Request?.Headers);
        RedactRequestCookies(e.Request?.Cookies);
        RedactRequestQueryString(e.Request?.QueryString);
        RedactBreadcrumbs(e.Breadcrumbs);
    }
    catch
    {
        // A redaction that throws is a scrubber bug — fail-closed:
        // drop the event entirely (the PII is not sent, the owner's
        // instance is unchanged, the owner sees a "dropped event" in
        // Sentry's dashboard rather than a leaked PII).
        return null;
    }
    return e;
};
```

**The four redaction targets** (pin §3c — the **closed** list, the F9.U1
closed-list precedent):

| # | Target | Why | The redaction |
|---|--------|-----|---------------|
| 1 | `e.Request?.Headers` | The `Authorization` header (the session cookie's Bearer token, the `KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY` on the siteverify POST, the `X-Forwarded-For` header on the proxy hop) | Redact the **value** of any header whose **name** matches `^authorization$`, `^set-cookie$`, `^cookie$` (case-insensitive) — the value is `"REDACTED"`. **Other** headers (the `X-Forwarded-For` / `X-Forwarded-Proto` headers) are **not** redacted (they are the proxy's own metadata, not PII — the `UseForwardedHeaders` comment block, `Program.cs:780-795`, confirms they are the trusted hop's metadata). |
| 2 | `e.Request?.Cookies` | The session cookie (the `Kumunita.Auth` cookie, the ASP.NET Identity's `.AspNetCore.Identity.Cookie`) | Redact the **value** of **every** cookie — the cookie names are not PII (the `Kumunita.Auth` / `.AspNetCore.Identity.Cookie` names are well-known), but the **values** are the session tokens + the anti-forgery token (the `RequestVerificationToken` cookie). **All cookie values** → `"REDACTED"`. |
| 3 | `e.Request?.QueryString` | The `?token=` / `?id=` query params (the email-verification token on the `/Account/ConfirmEmail?token=…` URL, the `?id=` on the `/Documents/{id}` URL) | Parse the query string (the `Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery` idiom, already in `Kumunita.Web`), redact the **value** of any param whose **name** matches `^token$`, `^id$`, `^sessionId$`, `^sessionid$`, `^csrf$`, `^__RequestVerificationToken$` (case-insensitive). **Other** params (the `?page=` / `?sort=` params) are **not** redacted (they are not PII). |
| 4 | `e.Breadcrumbs` | The breadcrumb `data` (the `AddNetworkLogBreadcrumbs` is **off** (pin §3a), so the **only** breadcrumb `data` surfaces are the **manual** breadcrumbs the app adds — the `Sentry.ConfigureScope` / `Sentry.AddBreadcrumb` calls, which are **none** today (a codebase-wide grep for `Sentry.AddBreadcrumb` returns **zero** hits — the app does not add manual breadcrumbs). **The redaction is a belt-and-suspenders**: redact the `data` value of any breadcrumb whose **category** is `"http"` or `"server"` (the network-log / server-error breadcrumb categories). | Redact the **`data` value** of any breadcrumb whose `category` matches `^http$`, `^server$` (case-insensitive) — the `data` dict's values → `"REDACTED"`. |

**The "never-leave-the-instance" invariant** (pin §3d):

- **The scrubber is a Web concern** (ADR 0006 — Core stays HTTP-free; the
  scrubber is a `SendCallback` on the Sentry options, a `Program.cs`-owned
  registration, **not** a Core interface). The **Core** owns the **domain**
  rules (the `IMailerStage` / `SmtpSender` I/O seam, the `IBotChallengeVerifier`
  I/O seam) — the **Web** owns the **transport** rules (the Sentry `SendCallback`,
  the CSP, the `UseForwardedHeaders` metadata). The scrubber never touches a
  Core type.
- **The scrubber is fail-closed** (pin §3b): a redaction that **throws** drops
  the **entire event** (the `return null;` in the `catch` block) — the PII is
  **not** sent, the owner's instance is unchanged, the owner sees a
  "dropped event" in Sentry's dashboard rather than a leaked PII. This is the
  **`TurnstileBotChallengeVerifier` fail-closed** precedent
  (`Kumunita.Core/Identity/TurnstileBotChallengeVerifier.cs`, the F9.U3
  handoff note: "a bot is not proven human").

---

## 4. The pinned test name + assertion contract (F10.U3 owns the test file + harness)

**The pinned test name** (the F9.U4 precedent, `Signup_DisposableEmail_Rejected`,
the F1.U1 pinned-name precedent):

```
Error_TrackerRegisteredWhenDsnSet
```

**The test file** (F10.U3 owns it): `tests/Kumunita.Web.Tests/F10U3_ErrorTrackerTests.cs`
(a **new** file — the `F9U4_SignupTests.cs` precedent, the `tests/Kumunita.Web.Tests`
project, **not** `tests/Kumunita.Core.Tests` — the Sentry registration is a
**Web** concern, the `Sentry.AspNetCore` package is a **Web** package, the
`Kumunita.Core` project does **not** reference `Sentry.AspNetCore`).

**The assertion contract** (pin §4a — the F9.U4 `Signup_DisposableEmail_Rejected`
assertion-4 idiom, the `Program.cs`-wired assertion):

| # | Assertion | The shape | The pin |
|---|-----------|-----------|---------|
| 1 | **`ISentrySdk` / `ISentryEventSink` is in the `IServiceProvider` when the DSN is set.** | A `ServiceCollection` (the F9.U4 `RequestServices` idiom, the `UploadGateTestSupport` no-op-factory idiom) that registers **the one conditional registration** (the `builder.WebHost.UseSentry(…)` call) with a **test DSN** (a `"https://test@o0.ingest.sentry.io/0"` literal — a well-formed DSN shape, not a real DSN). Assert: `provider.GetRequiredService<ISentrySdk>()` **succeeds** (the `ISentrySdk` / `ISentryEventSink` types are **Sentry** types, not Kumunita types — the `Sentry.AspNetCore` package's DI surface). | §4a leg-1 |
| 2 | **`ISentrySdk` / `ISentryEventSink` is NOT in the `IServiceProvider` when the DSN is unset.** | A `ServiceCollection` (the **same** idiom) that registers **the one conditional registration** with a **`null` DSN** (the `builder.Configuration["SENTRY_DSN"]` → `null` case). Assert: `provider.GetService<ISentrySdk>()` **returns `null`** (the `GetService` (nullable) idiom, **not** `GetRequiredService` (which throws on `null`) — the F9.U4 `null!` / `GetService` idiom). | §4a leg-2 |
| 3 | **The scrubber is registered with the §3 contract** (`SendDefaultPii == false`, `AddNetworkLogBreadcrumbs == false`, `SendCallback` is non-null and **redacts** the §3c targets). | A `ServiceCollection` (the **same** idiom) that registers **the one conditional registration** with a **test DSN**. Assert: the `SentryOptions` (resolved via the `IOptions<SentryOptions>` / the `ISentryOptions` surface — the **exact** `SentryOptions` access idiom is a **F10.U3 judgment call**, the pin fixes the **contract** not the **access path**) has `SendDefaultPii == false` (a direct property assert, the F9.U4 `Model.EmailDomainDisallowed == true` direct-property idiom), `AddNetworkLogBreadcrumbs == false` (a direct property assert), `SendCallback != null` (a non-null assert, the F9.U4 `BotChallengeEnabled` non-null idiom). **The §3c redaction behavior** (the `Authorization` / `Set-Cookie` / `?token=` / `?id=` redaction) is **asserted by invoking the callback** on a **synthetic `SentryEvent`** (a `new SentryEvent { Request = new SentryRequest { Headers = …, Cookies = …, QueryString = … } }`) and asserting the redacted values are `"REDACTED"` (the F9.U4 `Signup_BotChallengeEnforced` leg-1 idiom — a stub that returns `false` → the `BotChallengeFailed` flag is set; the F10.U3 leg-3 idiom — a synthetic event → the redacted values are `"REDACTED"`). | §4a leg-3 |

**The harness gotcha** (pin §4b — the F9.U4 `CountAsync` / `NSubstitute`
gotcha precedent):

- **The `SentryOptions` access path** — the `Sentry.AspNetCore` package
  registers the `SentryOptions` via the `IOptions<SentryOptions>` / the
  `ISentryOptions` surface (the **exact** access idiom is a **F10.U3 judgment
  call** — the pin fixes the **contract** (the `SendDefaultPii == false`,
  `AddNetworkLogBreadcrumbs == false`, `SendCallback != null` asserts), **not**
  the **access path** (the `IOptions<SentryOptions>` / the `ISentryOptions` /
  the `SentryOptions` static surface). F10.U3 reads the `Sentry.AspNetCore`
  source (the `SentryBuilder` / the `AddSentry` DI extension) to find the
  exact access idiom. **This is NOT a drift** — the pin is the **contract**,
  the access path is the **implementation**.
- **The `ISentrySdk` / `ISentryEventSink` types** — these are **Sentry** types
  (the `Sentry` package's public surface), **not** Kumunita types. The
  `Kumunita.Web.Tests` project must reference the `Sentry` package (a
  **transitive** reference via `Sentry.AspNetCore` — the `Sentry.AspNetCore`
  package's `PackageDependency` on `Sentry` is **transitive**, the
  `Kumunita.Web.Tests` project does **not** need an explicit `PackageReference`
  to `Sentry` — the `Sentry.AspNetCore` reference in `Kumunita.Web.csproj`
  (F10.U2) is **sufficient** for the test project to resolve the `ISentrySdk` /
  `ISentryEventSink` types via the transitive graph). **If the transitive
  graph does not resolve the types** (a `PackageReference` `PrivateAssets` /
  `IncludeAssets` issue, the `Kumunita.Web.csproj:3-7` `TypeScript.MSBuild`
  precedent), F10.U3 adds an explicit `<PackageReference Include="Sentry"
  Version="…" />` to `tests/Kumunita.Web.Tests/Kumunita.Web.Tests.csproj`
  (a **test-project** package reference, **not** a production package
  reference — the pin §7 exception does **not** cover a test-project
  reference, but a test-project reference is a **test-harness** concern, not a
  production concern — the F9.U4 `Kumunita.Core` test-project
  `PackageReference` precedent).

---

## 5. The owner-ops open items (NOT assumed done — carry them into the `## F10.U1` section)

**(a) `SENTRY_DSN` — the owner's Sentry DSN, registered to the instance's
domain** (pin §2b). The seam is in place in F10.U2 (the
`builder.WebHost.UseSentry(…)` call + the `o.Dsn = kumunitaSentryDsn`
assignment); the DSN is **owner-ops** (the owner's Sentry account, the DSN
string, the domain registration). **Until it is set, the instance is
unchanged** (Sentry is a **no-op**, the standing deferral stands — the F9.U3
`KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY` owner-ops precedent, the F9.U3
open-item (a) idiom).

**(b) Confirm the DSN is unset on prod before the rebuild** (pin §2b). The
scrubber is **additive + flag-gated** (the DSN-unset case is a **no-op**, the
`builder.WebHost.UseSentry(…)` call is **not executed**, the pipeline is
byte-identical to today's) — **but** the owner's **awareness** of the new
`SENTRY_DSN` knob is required (the F9.U3 open-item (b) idiom, the §4c CSP
additive + flag-gated precedent). **The owner must confirm** that the
`SENTRY_DSN` environment variable is **unset** (or blank) on the prod app
**before** the F10.U2 rebuild — if the DSN is **already set** on prod (a
prior manual set, a Coolify env var), the rebuild **enables** Sentry
**immediately** (the scrubber is **not** a no-op, the PII-scrubbing contract
is **live**, the owner's Sentry dashboard starts receiving events). **This is
the owner's call, not the code's** — the code is a **no-op** by default, but
the **environment** is the owner's.

---

## 6. The file list (F10.U2 + F10.U3) — the pin §7 exception

**F10.U2 lands** (the **one** `<PackageReference>` ADD the plan explicitly
allows, pin §7):

- `src/Kumunita.Web/Kumunita.Web.csproj` — the **one** new
  `<PackageReference Include="Sentry.AspNetCore" Version="…" />` line (the
  **exact** version is a **F10.U2 judgment call** — the pin §7 exception
  allows the ADD, the **version pin** is F10.U2's job, a NuGet
  `Sentry.AspNetCore` latest-stable lookup. The F1–F9 handoff notes do **not**
  pin a version — the F9.U2 `disposable-emails.txt` `EmbeddedResource`
  precedent (a committed file, **not** a `<PackageReference>`) is the **only**
  prior "ADD" in this lane, and it is **not** a package reference. **The
  version pin is F10.U2's open item** — the pin fixes the **ADD**, not the
  **version**).
- `src/Kumunita.Web/Program.cs` — the **pin §2a** `builder.WebHost.UseSentry(…)`
  block (the **one** new `builder`-phase registration, the F9.U3
  `IBotChallengeVerifier` registration precedent, the **same region** of
  `Program.cs` as the F9.U3 `builder.Services` / `builder.Configuration`
  registrations — the **`builder`-phase** block, **before** `app.Build()`).
  The **`o => …` callback body** (the §3a/§3b/§3c redaction) is **F10.U2's
  job** — the pin fixes the **contract**, not the **body**. **No other**
  `Program.cs` change (the CSP middleware block, the `UseForwardedHeaders`
  block, the `AddKumunitaCore()` call, the `IBotChallengeVerifier`
  registration are **all untouched**).

**F10.U3 lands** (the test file + harness):

- `tests/Kumunita.Web.Tests/F10U3_ErrorTrackerTests.cs` (NEW) — the
  **pinned** `Error_TrackerRegisteredWhenDsnSet` test (the §4a leg-1/leg-2/leg-3
  asserts) + the **`SentryOptions` access** idiom (the §4b harness gotcha — a
  F10.U3 judgment call, the pin fixes the **contract**, not the **access
  path**). **No other** test file (the `Kumunita.Core.Tests` project is
  **untouched** — the Sentry registration is a **Web** concern, the
  `Kumunita.Core` project does **not** reference `Sentry.AspNetCore`).

**NOT in F10's scope** (the pin §7 boundary):

- **The `IAlertSink` seam** (F11's `JOB-02` alert on dead-letter exhaustion) —
  the F10 pin is the **error tracker**, **not** the alert channel (the F11 /
  F12 units own the sink + the channel). **No `IAlertSink` type, no
  `IAlertSink` registration, no `IAlertSink` test** in F10.
- **The `WhatsNew.cs` / `Milestones.cs` / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `docs/adr/` close-flip** (the six members) — the
  close-flip happens at **F22**, the lane's **last** unit. **F10 is not the
  lane's last unit** (F11–F22 remain). **Do NOT run the close-flip** in F10.U1
  / F10.U2 / F10.U3.
- **The `Kumunita.Core` project** — the Sentry registration is a **Web**
  concern (ADR 0006 — Core stays HTTP-free). **No `Kumunita.Core.csproj`
  change, no `Kumunita.Core` `.cs` change, no `Kumunita.Core` test change** in
  F10.

---

## 7. The invariants held (the F9.U1 invariant checklist precedent)

- **OFF by default** — with `SENTRY_DSN` unset (the default), the
  `builder.WebHost.UseSentry(…)` call is **not executed**, the pipeline is
  **byte-identical** to today's (no `Sentry` middleware, no `ISentrySdk` /
  `ISentryEventSink` registration, no `/api/sentry` route). **The owner's
  instance is unchanged** until they set the DSN + rebuild (the F9.U3
  flag-off-deferred idiom, the §4c CSP additive + flag-gated precedent).
- **Core stays HTTP-free** (ADR 0006) — the registration and the PII-scrubber
  are **Web** concerns (a `Program.cs`-owned `builder`-phase call + a
  `SendCallback` on the Sentry options). **No `Kumunita.Core` type, no
  `Kumunita.Core` `.cs` change, no `Kumunita.Core` test change** in F10.
- **The `SmtpSender` / `IMailerStage` Core I/O-seam precedent is the SHAPE**
  (a thin I/O seam the Web owns the registration of), **not the location**
  (the scrubber never touches a Core type — the scrubber is a `SendCallback`
  on the Sentry options, a `Program.cs`-owned registration).
- **The scrubber is fail-closed** (pin §3b) — a redaction that **throws**
  drops the **entire event** (the `return null;` in the `catch` block) — the
  PII is **not** sent, the owner's instance is unchanged, the owner sees a
  "dropped event" in Sentry's dashboard rather than a leaked PII. **The
  `TurnstileBotChallengeVerifier` fail-closed** precedent
  (`Kumunita.Core/Identity/TurnstileBotChallengeVerifier.cs`).
- **No new bounded context / DocTypes surface / DI registration** (the F9.U1
  invariant checklist precedent) — the `UseSentry` call is a **middleware
  pipeline** registration (the `builder.WebHost` extension surface), **not** a
  new bounded context, **not** a new DocTypes surface, **not** a new DI
  registration (the `ISentrySdk` / `ISentryEventSink` registrations are the
  **Sentry SDK's own** DI surface, not a Kumunita DI registration).
- **No new `<PackageReference>`** **except** the **one** `Sentry.AspNetCore`
  ADD (pin §7) — the F1–F9 handoff notes' **only** prior "ADD" in this lane is
  the F9.U2 `disposable-emails.txt` `EmbeddedResource` (a committed file,
  **not** a `<PackageReference>`). **The `Sentry.AspNetCore` ADD is the
  exception the plan explicitly allows** (the plan's F10 row, the pin §7).
- **The F7 frozen-invariant test stays green** (the CSP string is **unchanged**
  — F10 adds **no header**, the `UseSentry` call is a **middleware**
  registration, **not** a header change). **The F9.U4 `Signup_DisposableEmail_Rejected`
  test stays green** (the `Kumunita.Core` project is **untouched**, the
  `AccountController.Signup` POST is **untouched**, the `IBotChallengeVerifier`
  seam is **untouched**).

---

## 8. The build/test command (repo memory, the F9.U4 precedent)

- `dotnet build Kumunita.slnx -c Debug` — **F10.U1 does NOT run this**
  (the unit's exit contract is **no build**). F10.U2 / F10.U3 run it.
- `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  — the **F10.U3** test assembly (the `Error_TrackerRegisteredWhenDsnSet` test
  is in `Kumunita.Web.Tests`, **not** `Kumunita.Core.Tests`). **F10.U1 does
  NOT run this** (the unit's exit contract is **no test run**).
- `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
  — the `Kumunita.Core.Tests` assembly (Testcontainers Postgres, ~20s,
  `docker container prune -f` after). **F10.U1 does NOT run this** (the
  `Kumunita.Core` project is **untouched**). **F10.U2 / F10.U3 run it**
  (the `Kumunita.Core` project is **untouched**, but the **solution** build
  touches it — the F9.U2 / F9.U3 / F9.U4 handoff notes all ran the
  `Kumunita.Core.Tests` assembly to confirm **no Core test broke**).

---

## 9. The pin's closed list (the F9.U1 closed-list precedent)

- **The `UseSentry` call shape** — pin §2a (the exact block, the DSN binding,
  the `AddInAppExclude` line).
- **The PII-scrubbing contract** — pin §3a (the `SendDefaultPii = false`,
  `AddNetworkLogBreadcrumbs = false`, `SendCallback` contract) + pin §3b (the
  `SendCallback` redaction shape) + pin §3c (the four redaction targets).
- **The pinned test name** — `Error_TrackerRegisteredWhenDsnSet` (pin §4).
- **The assertion contract** — pin §4a (leg-1 / leg-2 / leg-3).
- **The owner-ops open items** — pin §5 (a) + (b).
- **The file list** — pin §6 (F10.U2: `Kumunita.Web.csproj` + `Program.cs`;
  F10.U3: `F10U3_ErrorTrackerTests.cs`).
- **The NOT-in-scope boundary** — pin §6 (the `IAlertSink` seam, the
  close-flip, the `Kumunita.Core` project).
- **The invariants held** — pin §7.

**The pin is the contract, not the code.** F10.U2 / F10.U3 implement the
contract; they do **not** re-derive it. A drift in F10.U2 / F10.U3 (the
`Sentry.AspNetCore` API surface, the `SentryOptions` access path, the
`ISentrySdk` / `ISentryEventSink` DI surface) is a **judgment call** (the
§4b harness gotcha precedent), **not** a drift pause (the F9.U1 / F9.U2 /
F9.U3 / F9.U4 handoff notes' "no drift pause" precedent).
