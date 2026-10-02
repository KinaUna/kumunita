# IP · U06 — Login view `?error=` code → localized message (the cognitive-integration seam)

**You are the U06 agent.** This is a **Web** unit (with a **Core** test
assembly in its exit gate — the named exception, see the register). It
changes the `AccountController.Login` GET action to pass the `?error=`
code through verbatim (`Error = error`), adds a 3-row code→`kw-l`-key
table to `Views/Account/Login.cshtml` (one case per known code + a
`default` fallback), and adds the three corresponding `kw-l` keys
(`account.login.error.blocked`, `account.login.error.removed`,
`account.login.error.role_changed`) to `KnownTranslationKeys` in all four
languages (en/de/fr/da). It does **not** add a new `IErrorMessageService`,
does **not** add a new `AccessAction` or `TargetKind`, does **not** change
the middleware's redirect mechanism, and does **not** add any `kw-l` key
outside the three in the locked set.
**Exit: `dotnet build Kumunita.slnx -c Debug` clean + `Kumunita.Core.Tests`
green + `Kumunita.Web.Tests` green.** Core.Tests carries the
`KnownTranslationKeys_ParityTests` (the three new keys present in all four
language dictionaries — a missing `de`/`fr`/`da` value fails there);
Web.Tests carries the `KwLRegistryConsistencyTests` (the Login view's
`kw-l` keys are all registered — an unregistered key fails there). This
is the **named exception** to the "one test assembly per unit" rule (see
the register §unit-series-rules — "U06 is the named exception").

**Read the register first** — D6, C-IP·5, GATE-4, the §drift-guard.

## Goal

The two security middleware redirect to `/Account/Login?error=blocked`,
`?error=account-removed`, or `?error=role-changed`. The Login GET action
currently maps only `"blocked"` to a hardcoded English string
(`const string blockedMessage = …`); `"account-removed"` and
`"role-changed"` fall through to `null` (no error message at all). Even
the `"blocked"` case is **not localized** (a hardcoded English string, not
a `kw-l` key). The resident's mental model of *why they were signed out*
is broken at the seam with the platform's security model. The fix (D6 —
"the mapping is a 3-row table in the view") is:

1. **Controller**: pass the `?error=` code through verbatim
   (`Error = error`) — drop the `blockedMessage` const + the `errorText`
   switch.
2. **View**: a 3-row code→`kw-l`-key table (one case per known code + a
   `default` fallback to `Model.Error` for unknown codes).
3. **Registry**: the three `kw-l` keys in all four languages
   (en/de/fr/da) — the `KwLRegistryConsistencyTests` (Web.Tests) +
   `KnownTranslationKeys_ParityTests` (Core.Tests) pin the closure.

## Entry reads (6)

1. `docs/plans-milestones/plan-ip-integration-polish.md` — the register
   (D6, C-IP·5, the three `kw-l` keys + the three error codes).
2. `src/Kumunita.Web/Controllers/AccountController.cs` lines 191–225 —
   the **real** `Login` GET action: the `[FromQuery] string? error`
   parameter, the `const string blockedMessage = "Your account has been
   blocked…"` (hardcoded English), the `var errorText = error switch {
   "blocked" => blockedMessage, _ => null }` (only one case — the other
   two codes fall through to `null`), and the `LoginViewModel { Error =
   errorText }` render. The POST-Login guard (lines 258–268) already
   redirects to `?error=blocked` — the GET-side mapping is the seam to fix.
3. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` lines 599–615 —
   the **real** `account.*` key block in the `en` section: the existing
   keys (`account.login_title`, `account.login_submit`, etc.) and the
   insertion point (after `account.signup_has_account`, before the
   `// ── posts` comment).
4. `src/Kumunita.Web/Views/Account/Login.cshtml` lines 12–14 — the **real**
   error render: `@if (!string.IsNullOrEmpty(Model.Error)) {
   <div class="alert alert-danger">@Model.Error</div> }`.
5. `src/Kumunita.Web/Security/BlockedAccountMiddleware.cs` line 95 — the
   **real** redirect: `context.Response.Redirect("/Account/Login?error=blocked")`.
6. `src/Kumunita.Web/Security/PrivilegedStampMiddleware.cs` lines 128, 141 —
   the **real** redirects: `"/Account/Login?error=account-removed"` and
   `"/Account/Login?error=role-changed"`.

## Deliverables (3)

### 1. `src/Kumunita.Web/Controllers/AccountController.cs` — pass the code through verbatim

The current Login GET (lines ~198–210) does the *copy* mapping server-side
(the `const string blockedMessage = "Your account has been blocked…"`
hardcoded English + the `var errorText = error switch { "blocked" =>
blockedMessage, _ => null }`). D6 forbids a new `IErrorMessageService` and
*requires* "the mapping is a 3-row table in the view." The correct shape
is therefore: the controller **passes the code through verbatim**
(`Error = error`), and the **view** maps the code to the localized `kw-l`
key. This keeps the controller free of `ITranslationProvider` /
`ILocalizationService` dependency (the existing shape), and keeps the
localization in the view (the `kw-l` idiom — the same idiom the rest of
the Login page already uses for `account.login_title`,
`account.login_submit`, etc.).

**In `AccountController.Login` GET** — replace the `const string
blockedMessage` + the `var errorText = error switch { … }` block (the
two lines above `var showSetupLink = …`) with:

```csharp
// `error` arrives as a short code (not the message text) — the query string is
// user-visible and may be bookmarked/shared, so keep it token-like. The
// code→message table lives in the Login view (D6 — "the mapping is a 3-row
// table in the view"). The three known codes are:
//   "blocked"          — BlockedAccountMiddleware (suspension mid-session)
//                        + the POST-Login block-enforcement guard.
//   "account-removed"  — PrivilegedStampMiddleware (user deleted).
//   "role-changed"     — PrivilegedStampMiddleware (role-set mismatch).
// An unknown code is passed through verbatim — the view falls back to
// `Model.Error` (forward-compatible: a future code still shows something).
```

And in the `LoginViewModel` initializer, change `Error = errorText,` to
`Error = error,`:

```csharp
return View(new LoginViewModel
{
    ReturnUrl = returnUrl,
    Error = error,   // ← the code, not a resolved message (D6: view does the mapping)
    ShowSetupLink = showSetupLink,
    SignupOpen = signupOpen,
});
```

> The controller's `Error` property now carries the **code** (or `null` if
> no code). The view is responsible for mapping the code to a localized
> message.

**In `Views/Account/Login.cshtml`** — replace the current error block
(lines 12–14: `@if (!string.IsNullOrEmpty(Model.Error)) {
<div class="alert alert-danger">@Model.Error</div> }`) with a 3-row
code→message table:

```razor
@if (!string.IsNullOrEmpty(Model.Error))
{
    <div class="alert alert-danger">
        @switch (Model.Error)
        {
            case "blocked":
                <kw-l key="account.login.error.blocked">
                    Your account has been temporarily suspended.
                    Contact an administrator.
                </kw-l>
                break;
            case "account-removed":
                <kw-l key="account.login.error.removed">
                    Your account has been removed.
                    Contact an administrator.
                </kw-l>
                break;
            case "role-changed":
                <kw-l key="account.login.error.role_changed">
                    Your role has changed. Please sign in again.
                </kw-l>
                break;
            default:
                @Model.Error
                break;
        }
    </div>
}
```

> The `default` case renders `Model.Error` as-is (forward-compatible — an
> unknown code still shows something). The three known codes render their
> `kw-l`-localized message. The `kw-l` TagHelper auto-escapes the content
> and resolves the key from `KnownTranslationKeys` — the
> `KwLRegistryConsistencyTests` (Web.Tests) pin that every key the view
> emits is registered.

### 2. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — three new keys in all four languages

**Add** the three keys to the `account.*` block in **each** of the four
language sections. The insertion point is after the last existing
`account.*` key (after `account.signup_has_account` in the `en` section,
and the equivalent in `de`, `fr`, `da`).

**`en` section** (around line 610):

```csharp
["account.login.error.blocked"] =
    "Your account has been temporarily suspended. Contact an administrator.",
["account.login.error.removed"] =
    "Your account has been removed. Contact an administrator.",
["account.login.error.role_changed"] =
    "Your role has changed. Please sign in again.",
```

**`de` section** (around line 2400):

```csharp
["account.login.error.blocked"] =
    "Ihr Konto wurde vorübergehend gesperrt. Wenden Sie sich an einen Administrator.",
["account.login.error.removed"] =
    "Ihr Konto wurde entfernt. Wenden Sie sich an einen Administrator.",
["account.login.error.role_changed"] =
    "Ihre Rolle wurde geändert. Bitte melden Sie sich erneut an.",
```

**`fr` section** (around line 4100):

```csharp
["account.login.error.blocked"] =
    "Votre compte a été suspendu temporairement. Contactez un administrateur.",
["account.login.error.removed"] =
    "Votre compte a été supprimé. Contactez un administrateur.",
["account.login.error.role_changed"] =
    "Votre rôle a été modifié. Veuillez vous reconnecter.",
```

**`da` section** (around line 5810):

```csharp
["account.login.error.blocked"] =
    "Din konto er midlertidigt suspenderet. Kontakt en administrator.",
["account.login.error.removed"] =
    "Din konto er blevet fjernet. Kontakt en administrator.",
["account.login.error.role_changed"] =
    "Din rolle er blevet ændret. Log venligst ind igen.",
```

> **The two pin tests** (in **different** assemblies — this is the
> named-exception exit gate):
> - `KnownTranslationKeys_ParityTests` (**Core.Tests**) — key-for-key
>   parity across the four language dictionaries. A missing `de`/`fr`/`da`
>   value for any of the three new keys fails here.
> - `KwLRegistryConsistencyTests` (**Web.Tests**) — scans every
>   `kw-l key="…"` in `Views/**/*.cshtml` and asserts each key is
>   registered in `KnownTranslationKeys`. An unregistered key in the new
>   `Login.cshtml` block fails here.
>
> **If either test fails because a key is missing from one of the four
> language sections, add it to that section** (the tests are the pin, not
> the guide).

### 3. Pin test — the three codes render distinct localized messages

In `tests/Kumunita.Web.Tests/`, add a test (or extend the existing
`AccountControllerTests`):

```csharp
[Fact]
public async Task Login_GET_ErrorBlocked_RendersLocalizedMessage()
{
    // Build a test host with a stubbed IIdentityService + IUserInfoService.
    // GET /Account/Login?error=blocked.
    // Assert the response contains the "suspended" text (or the kw-l
    // resolved message for "blocked").
    // Follow the existing Web.Tests pattern for controller-level tests.
}

[Fact]
public async Task Login_GET_ErrorAccountRemoved_RendersLocalizedMessage()
{
    // GET /Account/Login?error=account-removed.
    // Assert the response contains the "removed" text.
}

[Fact]
public async Task Login_GET_ErrorRoleChanged_RendersLocalizedMessage()
{
    // GET /Account/Login?error=role-changed.
    // Assert the response contains the "role has changed" text.
}

[Fact]
public async Task Login_GET_UnknownError_RendersCodeAsIs()
{
    // GET /Account/Login?error=some-unknown-code.
    // Assert the response contains "some-unknown-code" (the default case).
}
```

> Follow the existing `Web.Tests` pattern. If the existing tests use
> `TestServer` / `WebApplicationFactory`, use the same harness. The key
> assertion is that each of the three codes renders a **distinct** message
> (not the same generic string).

## Exit (build + Core + Web test assemblies)

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

All must be green. Core.Tests (~20 s, Testcontainers postgres) carries the
`KnownTranslationKeys_ParityTests` (key-for-key parity across the four
language dictionaries — a missing `de`/`fr`/`da` value for any of the
three new keys fails there). Web.Tests carries the
`KwLRegistryConsistencyTests` (every `kw-l` key in `Views/**/*.cshtml`
is registered in `KnownTranslationKeys` — an unregistered key in the new
`Login.cshtml` block fails there). Additionally:

- `grep -n "account.login.error.blocked"
  src/Kumunita.Core/Localization/KnownTranslationKeys.cs` returns
  **4** matches (en/de/fr/da).
- `grep -n "account.login.error.removed"
  src/Kumunita.Core/Localization/KnownTranslationKeys.cs` returns
  **4** matches.
- `grep -n "account.login.error.role_changed"
  src/Kumunita.Core/Localization/KnownTranslationKeys.cs` returns
  **4** matches.
- `grep -n "case \"blocked\""
  src/Kumunita.Web/Views/Account/Login.cshtml` returns **1** match.
- `grep -n "case \"account-removed\""
  src/Kumunita.Web/Views/Account/Login.cshtml` returns **1** match.
- `grep -n "case \"role-changed\""
  src/Kumunita.Web/Views/Account/Login.cshtml` returns **1** match.
- `grep -n "Error = error"
  src/Kumunita.Web/Controllers/AccountController.cs` returns **1** match
  (the controller passes the code through verbatim — the `blockedMessage`
  const and the `errorText` switch are gone).
- The `KnownTranslationKeys_ParityTests` (in Core.Tests) pass — the
  three new keys are present in all four languages.
- The `KwLRegistryConsistencyTests` (in Web.Tests) pass — the three new
  `kw-l` keys in `Login.cshtml` are all registered.
