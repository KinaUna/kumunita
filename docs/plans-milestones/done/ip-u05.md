# IP · U05 — Unit tests for `BlockedAccountMiddleware` + `PrivilegedStampMiddleware`

**You are the U05 agent.** This is a **Web** unit. It creates **two** new
test files in `tests/Kumunita.Web.Tests/`:
`BlockedAccountMiddlewareTests.cs` and `PrivilegedStampMiddlewareTests.cs`.
It does **not** modify either middleware (the production code is correct —
the gap is the **absence of unit tests**), does **not** add a new
`IMiddleware` interface, does **not** require a running Postgres (the
middleware's `RequestServices.GetRequiredService<T>` calls are the seam —
a stubbed service provider is sufficient), and does **not** touch the Core
project. **Exit: `dotnet build Kumunita.slnx -c Debug` clean +
`Kumunita.Web.Tests` green.**

**Read the register first** — D5, C-IP·3, GATE-2, the §drift-guard.

## Goal

The two most security-critical per-request gates — block enforcement
(`BlockedAccountMiddleware`) and privilege-revocation
(`PrivilegedStampMiddleware`) — are exercised only via e2e. A regression in
the sign-out + redirect path (e.g. a refactored `SignOutAsync` call, a
wrong redirect URL, a missing `CompleteAsync`) would **not** be caught by
the fast suites. The fix is two unit test files that exercise each
middleware's `InvokeAsync` with a stubbed `HttpContext` + `RequestServices`,
asserting the sign-out + redirect shape for each security path and the
pass-through for the non-trigger paths.

## Entry reads (5)

1. `docs/plans-milestones/plan-ip-integration-polish.md` — the register
   (D5, C-IP·3, the test scope).
2. `src/Kumunita.Web/Security/BlockedAccountMiddleware.cs` — the **real**
   middleware: constructor takes `RequestDelegate next`; `InvokeAsync`
   resolves `IUserInfoService` + `SignInManager<User>` from
   `context.RequestServices`; the three branches:
   - unauthenticated / no subject → `_next(context)`.
   - profile null or not blocked → `_next(context)`.
   - profile blocked → `SignOutAsync()` + `Redirect("/Account/Login?error=blocked")`
     + `CompleteAsync()`.
3. `src/Kumunita.Web/Security/PrivilegedStampMiddleware.cs` — the **real**
   middleware: constructor takes `RequestDelegate next`; `InvokeAsync`
   resolves `UserManager<User>` + `SignInManager<User>` + `IUserInfoService`
   from `context.RequestServices`; the five branches:
   - unauthenticated → `_next(context)`.
   - no elevated role claim → `_next(context)` (fast path).
   - no subject → `_next(context)`.
   - user null (deleted) → `SignOutAsync()` + `Redirect("/Account/Login?error=account-removed")`
     + `CompleteAsync()`.
   - role set mismatch → `SignOutAsync()` + `Redirect("/Account/Login?error=role-changed")`
     + `CompleteAsync()`.
   - role set match → `_next(context)`.
4. `tests/Kumunita.Web.Tests/` (the test harness shape) — confirm the
   xunit.v3 pattern, the `IDisposable` / `IAsyncLifetime` shape, and the
   existing test file naming convention.
5. `src/Kumunita.Core/Identity/ClaimTypes.cs` + `Roles.cs` — the **real**
   claim type constants (`ClaimTypes.Subject`, `ClaimTypes.Role`) and role
   constants (`Roles.GlobalAdmin`, `Roles.Moderator`, `Roles.Translator`,
   `Roles.Member`, `Roles.ModeratorComponent(id)`) so the test's stub
   principal uses the correct strings.

## Deliverables (2)

### 1. `tests/Kumunita.Web.Tests/BlockedAccountMiddlewareTests.cs` — new file

```csharp
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="BlockedAccountMiddleware"/> (IP · U05,
/// D5 / C-IP·3). The middleware resolves <see cref="IUserInfoService"/>
/// and <see cref="SignInManager{User}"/> from
/// <see cref="HttpContext.RequestServices"/> (the per-request scoped
/// provider) — the test stubs that provider, so no Postgres is needed.
/// </summary>
public class BlockedAccountMiddlewareTests
{
    private static (HttpContext context, Mock<IUserInfoService> userInfo,
                    Mock<SignInManager<User>> signIn, bool nextCalled)
        BuildContext(bool authenticated, string? subject, Profile? profile,
                     bool blocked)
    {
        var nextCalled = false;
        var next = new RequestDelegate((ctx) => { nextCalled = true; return Task.CompletedTask; });

        var context = new DefaultHttpContext();
        if (authenticated)
        {
            var claims = new List<Claim>();
            if (subject is not null)
                claims.Add(new Claim(ClaimTypes.Subject, subject));
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            context.User = new ClaimsPrincipal(identity);
        }

        var userInfo = new Mock<IUserInfoService>();
        userInfo
            .Setup(u => u.GetProfileAsync(It.IsAny<string>()))
            .ReturnsAsync(profile);

        var signIn = new Mock<SignInManager<User>>(
            new Mock<UserManager<User>>().Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<ISecurityTokenValidator>().Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<ILogger<SignInManager<User>>>().Object,
            new Mock<IUserConfirmation<User>>().Object);
        signIn
            .Setup(s => s.SignOutAsync())
            .Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton<IUserInfoService>(userInfo.Object)
            .AddSingleton<SignInManager<User>>(signIn.Object)
            .BuildServiceProvider();
        context.RequestServices = serviceProvider;

        // Capture nextCalled via a closure — the test asserts on it after InvokeAsync.
        return (context, userInfo, signIn, nextCalled);
    }

    [Fact]
    public async Task Unauthenticated_PassesThrough()
    {
        var (context, _, _, _) = BuildContext(
            authenticated: false, subject: null, profile: null, blocked: false);

        var mw = new BlockedAccountMiddleware(
            (ctx) => { context.Response.StatusCode = 200; return Task.CompletedTask; });
        await mw.InvokeAsync(context);

        // No sign-out, no redirect — the response is 200 (the stub next).
        Assert.Equal(200, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task NotBlocked_PassesThrough()
    {
        var profile = new Profile { Id = "p1", SubjectId = "s1", Blocked = false };
        var (context, _, _, _) = BuildContext(
            authenticated: true, subject: "s1", profile: profile, blocked: false);

        var mw = new BlockedAccountMiddleware(
            (ctx) => { context.Response.StatusCode = 200; return Task.CompletedTask; });
        await mw.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task Blocked_SignsOutAndRedirects()
    {
        var profile = new Profile { Id = "p1", SubjectId = "s1", Blocked = true };
        var (context, _, signIn, _) = BuildContext(
            authenticated: true, subject: "s1", profile: profile, blocked: true);

        var mw = new BlockedAccountMiddleware(
            (ctx) => Task.CompletedTask);
        await mw.InvokeAsync(context);

        signIn.Verify(s => s.SignOutAsync(), Times.Once);
        Assert.Equal("/Account/Login?error=blocked",
            context.Response.Headers["Location"].ToString());
    }
}
```

> **Adapt the test to the actual `IUserInfoService` interface** (read the
> real interface to confirm the `GetProfileAsync` signature — it may be
> `Task<Profile?>` or `Task<Profile>`). If `Profile` doesn't have a
> `SubjectId` property, use the correct property name (read
> `Profile.cs`). If `SignInManager<User>`'s constructor is hard to stub
> (it has many required parameters), use a `NullObject` pattern or a
> custom `IServiceProvider` that returns a sealed stub — the key assertion
> is the redirect URL + the sign-out call.

### 2. `tests/Kumunita.Web.Tests/PrivilegedStampMiddlewareTests.cs` — new file

```csharp
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="PrivilegedStampMiddleware"/> (IP · U05,
/// D5 / C-IP·3). The middleware resolves <see cref="UserManager{User}"/>,
/// <see cref="SignInManager{User}"/>, and <see cref="IUserInfoService"/>
/// from <see cref="HttpContext.RequestServices"/> — the test stubs that
/// provider, so no Postgres is needed.
/// </summary>
public class PrivilegedStampMiddlewareTests
{
    [Fact]
    public async Task Unauthenticated_PassesThrough()
    {
        // Unauthenticated → _next(context) immediately.
        var context = new DefaultHttpContext();
        var mw = new PrivilegedStampMiddleware(
            (ctx) => { context.Response.StatusCode = 200; return Task.CompletedTask; });
        await mw.InvokeAsync(context);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task NoElevatedRole_PassesThrough()
    {
        // Authenticated, but only Member role → fast path (no DB read).
        var claims = new List<Claim>
        {
            new(ClaimTypes.Subject, "s1"),
            new(ClaimTypes.Role, Roles.Member),
        };
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var mw = new PrivilegedStampMiddleware(
            (ctx) => { context.Response.StatusCode = 200; return Task.CompletedTask; });
        await mw.InvokeAsync(context);
        Assert.Equal(200, context.Response.StatusCode);
    }

    [Fact]
    public async Task UserDeleted_SignsOutAndRedirectsAccountRemoved()
    {
        // Authenticated with GlobalAdmin role, but user is null in the DB.
        var claims = new List<Claim>
        {
            new(ClaimTypes.Subject, "s1"),
            new(ClaimTypes.Role, Roles.GlobalAdmin),
        };
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<IPasswordHasher<User>>().Object,
            new Mock<IUserValidator<User>>[0],
            null,
            new Mock<IPasswordTokenProvider<User>>().Object,
            new Mock<ILogger<UserManager<User>>>().Object,
            new Mock<IServiceScopeFactory>().Object);
        userManager.Setup(u => u.FindByIdAsync("s1")).ReturnsAsync((User)null);

        var signIn = new Mock<SignInManager<User>>(
            userManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<ISecurityTokenValidator>().Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<ILogger<SignInManager<User>>>().Object,
            new Mock<IUserConfirmation<User>>().Object);
        signIn.Setup(s => s.SignOutAsync()).Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton(userManager.Object)
            .AddSingleton(signIn.Object)
            .BuildServiceProvider();
        context.RequestServices = serviceProvider;

        var mw = new PrivilegedStampMiddleware((ctx) => Task.CompletedTask);
        await mw.InvokeAsync(context);

        signIn.Verify(s => s.SignOutAsync(), Times.Once);
        Assert.Equal("/Account/Login?error=account-removed",
            context.Response.Headers["Location"].ToString());
    }

    [Fact]
    public async Task RoleSetMismatch_SignsOutAndRedirectsRoleChanged()
    {
        // Authenticated with GlobalAdmin in the cookie, but the DB says
        // only Member → mismatch → sign out + redirect role-changed.
        var claims = new List<Claim>
        {
            new(ClaimTypes.Subject, "s1"),
            new(ClaimTypes.Role, Roles.GlobalAdmin),  // cookie says GlobalAdmin
        };
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var user = new User { Id = "s1" };
        var userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<IPasswordHasher<User>>().Object,
            new Mock<IUserValidator<User>>[0],
            null,
            new Mock<IPasswordTokenProvider<User>>().Object,
            new Mock<ILogger<UserManager<User>>>().Object,
            new Mock<IServiceScopeFactory>().Object);
        userManager.Setup(u => u.FindByIdAsync("s1")).ReturnsAsync(user);
        userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new[] { Roles.Member });

        var signIn = new Mock<SignInManager<User>>(
            userManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<ISecurityTokenValidator>().Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<ILogger<SignInManager<User>>>().Object,
            new Mock<IUserConfirmation<User>>().Object);
        signIn.Setup(s => s.SignOutAsync()).Returns(Task.CompletedTask);

        var serviceProvider = new ServiceCollection()
            .AddSingleton(userManager.Object)
            .AddSingleton(signIn.Object)
            .BuildServiceProvider();
        context.RequestServices = serviceProvider;

        var mw = new PrivilegedStampMiddleware((ctx) => Task.CompletedTask);
        await mw.InvokeAsync(context);

        signIn.Verify(s => s.SignOutAsync(), Times.Once);
        Assert.Equal("/Account/Login?error=role-changed",
            context.Response.Headers["Location"].ToString());
    }

    [Fact]
    public async Task RoleSetMatch_PassesThrough()
    {
        // Authenticated with GlobalAdmin in the cookie, DB also says
        // GlobalAdmin → match → _next(context).
        var claims = new List<Claim>
        {
            new(ClaimTypes.Subject, "s1"),
            new(ClaimTypes.Role, Roles.GlobalAdmin),
        };
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));

        var user = new User { Id = "s1" };
        var userManager = new Mock<UserManager<User>>(
            new Mock<IUserStore<User>>().Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<IPasswordHasher<User>>().Object,
            new Mock<IUserValidator<User>>[0],
            null,
            new Mock<IPasswordTokenProvider<User>>().Object,
            new Mock<ILogger<UserManager<User>>>().Object,
            new Mock<IServiceScopeFactory>().Object);
        userManager.Setup(u => u.FindByIdAsync("s1")).ReturnsAsync(user);
        userManager.Setup(u => u.GetRolesAsync(user)).ReturnsAsync(new[] { Roles.GlobalAdmin });

        var signIn = new Mock<SignInManager<User>>(
            userManager.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<ISecurityTokenValidator>().Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<ILogger<SignInManager<User>>>().Object,
            new Mock<IUserConfirmation<User>>().Object);

        var serviceProvider = new ServiceCollection()
            .AddSingleton(userManager.Object)
            .AddSingleton(signIn.Object)
            .BuildServiceProvider();
        context.RequestServices = serviceProvider;

        var mw = new PrivilegedStampMiddleware(
            (ctx) => { context.Response.StatusCode = 200; return Task.CompletedTask; });
        await mw.InvokeAsync(context);
        Assert.Equal(200, context.Response.StatusCode);
    }
}
```

> **Adapt the test to the actual `UserManager<User>` / `SignInManager<User>`
> constructor signatures** (read the real classes — the constructor
> parameter list varies by ASP.NET Core version). If Moq is not available
> in the test project, use a sealed stub class or a manual `IServiceProvider`
> that returns the stub. The key assertions are:
> - The redirect URL is correct for each error code.
> - `SignOutAsync` is called exactly once.
> - The pass-through paths do **not** call `SignOutAsync` and return 200.

## Exit (build + Web test assembly)

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

Both must be green. The new test files must be included in the
`Kumunita.Web.Tests.csproj` (if the project uses explicit `<Compile>` items;
if it uses the default glob, the files are picked up automatically).
