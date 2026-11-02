# IP · U04 — Log + narrow the two silent `catch {}` blocks (the degraded-but-signed-in seam)

**You are the U04 agent.** This is a **Web** unit. It fixes the two silent
`catch {}` blocks in `HomeController.cs:197` and `MessagesController.cs:191`
by (a) injecting `ILogger<T>` into each controller's constructor, (b)
replacing the bare `catch {}` with `catch (Exception ex) when (ex is not
UnauthorizedAccessException)`, and (c) calling `_logger.LogWarning(ex, …)`
before the `profile = null` / `actorDisplayName = null` fallback. It does
**not** rethrow (the "degraded but signed-in" state is a product decision),
does **not** add an `IErrorHandlingService`, does **not** remove the
`catch`, and does **not** touch the Core project. **Exit: `dotnet build
Kumunita.slnx -c Debug` clean + `Kumunita.Web.Tests` green.**

**Read the register first** — D4, C-IP·3, GATE-2, the §drift-guard.

## Goal

The two `catch {}` blocks swallow **all** exceptions (including transient DB
failures, connection resets, OOM) and render a null profile / null display
name — a "degraded but signed-in" state that is **indistinguishable** from
"no profile exists." The operator sees nothing in the logs. The fix is not
to rethrow (a 500 on the home page for a transient DB error is worse than a
degraded render — D4); the fix is to **log** the exception at `Warning`
level (the operator sees it in the structured log) and **narrow** the
`catch` so that `UnauthorizedAccessException` (the expected "not a
participant" / "no access" shape) is still silently handled (the existing
intentional degradation path) while **unexpected** exceptions (DB failures,
NREs, OOM) are logged before degrading.

## Entry reads (5)

1. `docs/plans-milestones/plan-ip-integration-polish.md` — the register
   (D4, C-IP·3, the locked `catch` shape).
2. `src/Kumunita.Web/Controllers/HomeController.cs` lines 195–199 — the
   **real** `catch { profile = null; }` (a bare `catch` with no exception
   type, no variable, no `when` clause). The surrounding `try` block
   (line 196) is `profile = await UserInfo.GetProfileAsync(post.AuthorId)
   .ConfigureAwait(false);` — a single profile read in the feed-loop.
3. `src/Kumunita.Web/Controllers/HomeController.cs` lines 54–72 — the
   **real** `HomeController` constructor (8 optional parameters, no
   `ILogger`).
4. `src/Kumunita.Web/Controllers/MessagesController.cs` lines 190–191 —
   the **real** `try { actorDisplayName = (await
   _userInfo.GetProfileAsync(actorId))?.DisplayName; } catch { /* read
   seam not fatal */ }` (a bare `catch` with a comment, no exception type,
   no variable).
5. `src/Kumunita.Web/Controllers/MessagesController.cs` lines 47–55 — the
   **real** `MessagesController` primary constructor (2 required parameters,
   no `ILogger`).

## Deliverables (2)

### 1. `src/Kumunita.Web/Controllers/HomeController.cs` — inject `ILogger`, fix `catch`

**Add** `ILogger<HomeController>` to the constructor. The constructor
currently has 8 parameters (1 required + 7 optional). Add the logger as
the **first** parameter (required, before `IOptions<CommunityOptions>`),
and add the field:

```csharp
// Add to the using block (confirm it's not already present):
using Microsoft.Extensions.Logging;

// In the constructor — add the logger as the first parameter:
public HomeController(
    ILogger<HomeController> logger,
    IOptions<CommunityOptions> community,
    PostService? posts = null,
    IAnnouncementService? announcements = null,
    IPageService? pages = null,
    IAuthorizationService? authz = null,
    ILocalizationService? localization = null,
    IUserInfoService? userInfo = null,
    ITranslationProvider? translationProvider = null)
{
    _logger = logger;
    _community = community.Value;
    // … (existing field assignments unchanged)
}

// Add the field:
private readonly ILogger<HomeController> _logger;
```

> **Note:** ASP.NET Core's `ControllerBase` does **not** inject
> `ILogger<T>` automatically into arbitrary controllers (only into the
> base `Controller` class, which provides a non-generic `ILogger`).
> The primary-constructor pattern here means we add the parameter
> explicitly. The DI container auto-resolves `ILogger<T>` — no
> `AddScoped` needed.

**Replace** the bare `catch` at line ~197:

```csharp
// Before:
Profile? profile = null;
try { profile = await UserInfo.GetProfileAsync(post.AuthorId).ConfigureAwait(false); }
catch { profile = null; }

// After:
Profile? profile = null;
try
{
    profile = await UserInfo.GetProfileAsync(post.AuthorId).ConfigureAwait(false);
}
catch (Exception ex) when (ex is not UnauthorizedAccessException)
{
    _logger.LogWarning(ex, "Home feed profile read failed for author {AuthorId}; rendering degraded row.", post.AuthorId);
    profile = null;
}
```

> The `when` clause excludes `UnauthorizedAccessException` (the expected
> "not found / no access" shape — that is a **normal** "no profile" result,
> not a degraded state; logging it would be noise). All **other**
> exceptions (DB failures, NREs, OOM, connection resets) are logged at
> `Warning` with the exception attached (the structured log captures the
> stack trace), then the row degrades to a null profile as before.

### 2. `src/Kumunita.Web/Controllers/MessagesController.cs` — inject `ILogger`, fix `catch`

**Add** `ILogger<MessagesController>` to the primary constructor:

```csharp
// Add to the using block:
using Microsoft.Extensions.Logging;

// The primary constructor (currently 2 params) gains a third:
public sealed class MessagesController(
    ILogger<MessagesController> logger,
    IMessagingService messaging,
    IUserInfoService userInfo) : Controller
{
    private readonly ILogger<MessagesController> _logger = logger;
    private readonly IMessagingService _messaging = messaging;
    private readonly IUserInfoService _userInfo = userInfo;
    // … (rest unchanged)
}
```

**Replace** the bare `catch` at line ~191:

```csharp
// Before:
string? actorDisplayName = null;
try { actorDisplayName = (await _userInfo.GetProfileAsync(actorId))?.DisplayName; } catch { /* read seam not fatal */ }

// After:
string? actorDisplayName = null;
try
{
    actorDisplayName = (await _userInfo.GetProfileAsync(actorId))?.DisplayName;
}
catch (Exception ex) when (ex is not UnauthorizedAccessException)
{
    _logger.LogWarning(ex, "Thread actor display-name read failed for {ActorId}; falling back to the word-based label.", actorId);
    actorDisplayName = null;
}
```

> Same pattern: `UnauthorizedAccessException` is the expected "no profile"
> shape (silent); everything else is logged at `Warning` before the
> fallback.

### 3. Pin test — the catch shape is correct

In `tests/Kumunita.Web.Tests/`, the existing tests already exercise the
degraded state (a null profile / null display name). The new behaviour is:
- The degraded state still renders (no 500, no exception thrown to the
  caller) — the existing tests cover this.
- The `UnauthorizedAccessException` path is silent (no log call) —
  a unit test that stubs `IUserInfoService.GetProfileAsync` to throw
  `UnauthorizedAccessException` and asserts the controller returns a
  200 with a null profile / null display name.
- An **unexpected** exception (e.g. `InvalidOperationException`) is
  logged (assert via a test `ILogger`) and the controller still returns
  a 200 with a null profile.

> Follow the existing `Web.Tests` pattern for controller-level tests. If
> the existing tests already cover the degraded state, the new tests
> only need to assert the **log call** (a `NullLogger` or a
> `ListLogger` stub that captures the log level + message).

## Exit (build + Web test assembly)

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

Both must be green. Additionally:

- `grep -n "catch {" src/Kumunita.Web/Controllers/HomeController.cs`
  returns **zero** matches (the bare `catch` is replaced).
- `grep -n "catch {" src/Kumunita.Web/Controllers/MessagesController.cs`
  returns **zero** matches (the bare `catch` is replaced).
- `grep -n "when (ex is not UnauthorizedAccessException)"
  src/Kumunita.Web/Controllers/HomeController.cs` returns **1** match.
- `grep -n "when (ex is not UnauthorizedAccessException)"
  src/Kumunita.Web/Controllers/MessagesController.cs` returns **1** match.
- `grep -n "_logger.LogWarning" src/Kumunita.Web/Controllers/HomeController.cs`
  returns **1** match.
- `grep -n "_logger.LogWarning" src/Kumunita.Web/Controllers/MessagesController.cs`
  returns **1** match.
