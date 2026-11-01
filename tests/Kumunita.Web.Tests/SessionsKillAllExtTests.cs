using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Identity;
using Kumunita.Core.Sessions;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// Sessions lane, U10 — the <b>kill-all extension</b> test (design doc
/// <c>sessions-per-session-revocation.md</c> §2.5 / §2.3 shape 2).
/// Mirrors the F5 <c>F5U4_KillAllTests</c> harness shape (the
/// <c>PostgresFixture</c> shape, real EF <c>UserManager</c> over an
/// <c>AppDbContext</c>, the real Core <c>IdentityService</c> — the
/// F5 seam unchanged), and **extends** it with a real
/// <see cref="SessionService"/> over a real <c>IDocumentStore</c> with
/// <c>SessionDocTypes</c> registered — the per-session lane's
/// <c>SessionDoc</c> rows are seeded and the extended
/// <c>SessionsKillAll</c> action is driven end-to-end.
/// <para>
/// The test name is **pinned** by the §2.5 part-vs-whole gate
/// (the M3 §2.5 analog: the 12-test list is the whole; this test
/// is the *handoff* test's companion — the kill-all extension's
/// per-session half).
/// </para>
/// <para>
/// This unit **does not** run the §2.5 acceptance gate (U11 records
/// it) and authors no e2e (U11 records the F5 e2e pass count).
/// The pass/red status of this 1 is the data U11's part-vs-whole
/// test consumes.
/// </para>
/// </summary>
public sealed class SessionsKillAllExtTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Password = "Sup3rSecret!";

    /// <summary>
    /// <b>KillAllExt_RevokesOthersKeepsCurrent</b> — the
    /// <c>POST /settings/security/sessions/kill-all</c> action
    /// (U7's extended action) calls:
    /// <ol>
    /// <li><c>IIdentityService.KillAllSessionsAsync</c> (the F5
    /// path — the stamp rotation + the <c>session.kill_all</c>
    /// audit row), then</li>
    /// <li><c>SessionService.RevokeAllButCurrentAsync</c> (the
    /// per-session lane's U7 extension — the
    /// <c>SessionDoc.RevokedAt</c> writes + the
    /// <c>session.revoke_all_but_current</c> audit row), then</li>
    /// <li><c>SignInManager.SignOutAsync</c> (the F5 "local
    /// sign-out" behavior — the resident is signed out of the
    /// current browser) + the redirect to Login.</li>
    /// </ol>
    /// The test seeds two <c>SessionDoc</c> rows for the resident
    /// (the "current" session — the one the test's principal is
    /// representing, and one "other" session), drives the action,
    /// and asserts:
    /// <ul>
    /// <li>(a) The action returns a redirect to Login (the F5
    /// "local sign-out" behavior, unchanged).</li>
    /// <li>(b) <c>SignInManager.SignOutAsync</c> was called (the
    /// F5 local effect, unchanged).</li>
    /// <li>(c) The <c>UserManager.SecurityStamp</c> was rotated
    /// (the F5 "everywhere" effect, unchanged — the prerequisite
    /// for the <c>RevocationStampMiddleware</c> to reject the
    /// stale session on its next request).</li>
    /// <li>(d) **The per-session lane's extension**: the "current"
    /// <c>SessionDoc</c>'s <c>RevokedAt</c> is <b>still
    /// null</b> (kept active — the per-session mechanism does
    /// **not** revoke the current session), and the "other"
    /// <c>SessionDoc</c>'s <c>RevokedAt</c> is **not null**
    /// (revoked — the all-but-current rule).</li>
    /// <li>(e) **The audit rows**: the <c>mt</c> schema has **both**
    /// a <c>session.kill_all</c> row (the F5 whole-account kill,
    /// unchanged) and a <c>session.revoke_all_but_current</c> row
    /// (the per-session lane's extension, the C3 same-transaction
    /// pin — the audit row is in the same Marten session as the
    /// <c>RevokedAt</c> writes).</li>
    /// </ul>
    /// </summary>
    [Fact]
    public async Task KillAllExt_RevokesOthersKeepsCurrent()
    {
        var b = await BootAsync();
        var user    = b.User;
        var subject = user.Id!;

        // The two SessionDoc rows: one "current" (the one the test's
        // principal represents — the test's <c>kumunita.sid</c> would
        // be this id, but in a direct-construction harness the
        // <c>CurrentSessionIdAsync</c> helper reads the cookie via
        // <c>HttpContext.AuthenticateAsync</c>, which is a no-op in
        // the harness (the F5U4 guard: "resolving IAuthenticationService
        // via HttpContext.RequestServices (null-guarded) and skipping
        // the gate when absent — a no-op in unit tests"). So
        // <c>CurrentSessionIdAsync</c> returns null, coalesced to
        // string.Empty by U7's <c>?? ""</c>, and the
        // <c>RevokeAllButCurrentAsync</c> call degrades to "revoke
        // all" — **no** current session to preserve.
        // <para>
        // To test the *extension* (the all-but-current rule), we
        // plant the "current" session with the id we pass as the
        // <c>currentSessionId</c> to the action, and the "other"
        // session with a different id. The action's
        // <c>CurrentSessionIdAsync</c> returns null (no
        // <c>kumunita.sid</c> cookie in the harness), so
        // <c>RevokeAllButCurrentAsync</c> revokes **all** — including
        // the "current" one. To verify the *extension's* contract
        // (all-but-current, not all), we instead drive the
        // <c>SessionService.RevokeAllButCurrentAsync</c> seam
        // directly with the "current" id, and assert the same
        // postconditions as the action would produce if the
        // <c>kumunita.sid</c> cookie were present.
        // </para>
        // <para>
        // The test therefore asserts the **extension's** contract
        // (the per-session lane's U7 extension) at the service level
        // — the action's HTTP shape (redirect + SignOutAsync) is
        // the F5 half (already tested by F5U4's
        // <c>KillAllSessions_SignsOutAll</c>). The two halves
        // together are the kill-all extension.
        // </para>
        const string currentId = "s-killall-ext-current";
        const string otherId   = "s-killall-ext-other";

        await PlantSession(b.Store, currentId, subject, "Mozilla/5.0 (current)", "10.0.0.1");
        await PlantSession(b.Store, otherId,   subject, "Mozilla/5.0 (other)",   "10.0.0.2");

        // The F5 half — the stamp rotation (the prerequisite for
        // RevocationStampMiddleware to reject the stale session on
        // its next request). The F5 audit row (session.kill_all)
        // is committed by the IdentityService (the ADR 0006 single-
        // write seam).
        var otherStamp = await b.UserManager.GetSecurityStampAsync(user);
        Assert.NotNull(otherStamp);
        Assert.NotEmpty(otherStamp);

        // Drive the real F5.U3 surface: SessionsKillAll → Core seam
        // (KillAllSessionsAsync, the stamp rotation) + the sessions-
        // lane extension (RevokeAllButCurrentAsync, the per-session
        // RevokedAt writes) + local SignOutAsync.
        var signInManager = Substitute.For<SignInManager<User>>(
            b.UserManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>());

        var secController = new SecuritySettingsController(
            userManager:    b.UserManager,
            signInManager:  signInManager,
            identity:       b.Identity,
            sessionService: new SessionService(b.Store));

        var httpCtx = new DefaultHttpContext
        {
            User = BuildPrincipal(subject),
            RequestServices = new DictionaryProvider(
                new Dictionary<Type, object>
                {
                    [typeof(IAuthenticationService)] = new SidAuthenticationService(currentId),
                }),
        };
        secController.ControllerContext = new ControllerContext { HttpContext = httpCtx };
        secController.TempData = new TempDataDictionary(httpCtx, new NoOpTempDataProvider());
        secController.Url = new StubUrlHelper(new ActionContext { HttpContext = httpCtx });

        var killResult = await secController.SessionsKillAll();

        // (a) The F5 local effect: redirect to Login (unchanged).
        var redirect = Assert.IsType<RedirectToActionResult>(killResult);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Account", redirect.ControllerName);

        // (b) The F5 local effect: SignInManager.SignOutAsync was called.
        await signInManager.Received().SignOutAsync();

        // (c) The F5 "everywhere" effect: the stamp was rotated
        //     (unchanged — the prerequisite for the
        //     RevocationStampMiddleware to reject the stale session
        //     on its next request).
        var rotatedStamp = await b.UserManager.GetSecurityStampAsync(user);
        Assert.True(otherStamp != rotatedStamp,
            "KillAllSessionsAsync must rotate the SecurityStamp (the gate reads it). " +
            $"Other={otherStamp}, Rotated={rotatedStamp}");

        // (d) The per-session lane's extension: the action's
        //     CurrentSessionIdAsync() reads the kumunita.sid item via
        //     the SidAuthenticationService stub (which returns
        //     currentId), so RevokeAllButCurrentAsync(subject,
        //     currentId) preserves the "current" session (the
        //     all-but-current rule — the §2.3 shape 2) and revokes
        //     the "other" session.
        var ct = TestContext.Current.CancellationToken;
        await using var q = b.Store.QuerySession();
        var cur   = await q.LoadAsync<SessionDoc>(currentId, ct);
        var other = await q.LoadAsync<SessionDoc>(otherId, ct);

        // (d1) The "current" session is kept active (RevokedAt stays
        //      null — the per-session mechanism does NOT revoke it).
        Assert.NotNull(cur);
        Assert.Null(cur.RevokedAt);

        // (d2) The "other" session is revoked (the all-but-current
        //      rule — the per-session lane's extension).
        Assert.NotNull(other);
        Assert.NotNull(other.RevokedAt);

        // (e) The audit rows: the F5 session.kill_all row is
        //     present (the F5 whole-account kill, unchanged). The F5
        //     IdentityService.AuditRow uses TargetKind = "account"
        //     (the AccountKind constant, IdentityService.cs:62) — not
        //     "session" — so filter by the Action tag only.
        var killAllRows = await AuditsFor(b.Store,
            targetKind: "account", action: "session.kill_all");
        Assert.Contains(killAllRows,
            a => a.ActorId == subject && a.TargetId == subject);

        // (e2) The per-session lane's extension row is present (the
        //      C3 same-transaction pin — the audit row is in the
        //      same Marten session as the RevokedAt writes).
        var allButCurrentRows = await AuditsFor(b.Store,
            targetKind: "session", action: "session.revoke_all_but_current");
        Assert.Contains(allButCurrentRows,
            a => a.ActorId == subject);
    }

    // ── Shared helpers ─────────────────────────────────────────────────

    private sealed record Boot
    {
        public required User              User        { get; init; }
        public required UserManager<User> UserManager { get; init; }
        public required IdentityService   Identity    { get; init; }
        public required IDocumentStore    Store       { get; init; }
    }

    private async Task<Boot> BootAsync()
    {
        var ct   = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        // Real Marten store (the F5U4 shape — the ADR 0006 single-
        // write seam, the C3 same-session audit row) + the
        // sessions lane's SessionDocTypes surface (U3's pin).
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            SessionDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // Real EF store (the Identity schema — the User row, the
        // SecurityStamp).
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.MigrateAsync(ct);
        }

        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(new AppDbContext(dbOptions));

        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions { User = { RequireUniqueEmail = true } }),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        // Seed the resident.
        var email = "killallext-" + Guid.NewGuid().ToString("n")[..10] + "@examplium.com";
        var user  = new User { Id = Guid.NewGuid().ToString("N"), Email = email, UserName = email };
        await userManager.CreateAsync(user, Password);

        // The real Core IdentityService (the ADR 0006 single-write
        // seam — KillAllSessionsAsync rotates the stamp via
        // UpdateSecurityStampAsync + appends an AccessAudit row in
        // the same Marten session).
        var userInfo = new UserInfoService(store);
        var identity = new IdentityService(
            userManager:          userManager,
            documentStore:        store,
            userInfo:             userInfo,
            claimsSource:         Substitute.For<IClaimsSource>(),
            mailer:               Substitute.For<IMailerStage>(),
            verificationOptions:  Options.Create(new VerificationOptions()),
            logger:               NullLogger<IdentityService>.Instance,
            translationProvider:  null,
            notifications:        null,
            sampleDataOptions:    Options.Create(new SampleDataOptions { Enabled = false }),
            mediaStore:           null);

        return new Boot
        {
            User = user,
            UserManager = userManager,
            Identity = identity,
            Store = store,
        };
    }

    /// <summary>
    /// The principal the <c>KumunitaPrincipal.SubjectId(user)</c>
    /// reader resolves: a <c>Kumunita.Sub</c> claim with the user's
    /// id (the admissible claim set — invariant-set-B intact,
    /// ADR 0006-B). The <c>Kumunita.Sub</c> claim type is the
    /// <c>ClaimTypes.Subject</c> constant (the
    /// <c>KumunitaPrincipal.SubjectId</c> reader).
    /// </summary>
    private static ClaimsPrincipal BuildPrincipal(string userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, userId),
            new Claim("Kumunita.Verified", "true"),
        });
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Plant a <see cref="SessionDoc"/> row directly (test fixture
    /// seeding, not a service write seam — the
    /// <c>SessionServiceTests.Plant</c> idiom).
    /// </summary>
    private static async Task PlantSession(
        IDocumentStore store, string id, string userId, string userAgent, string ip)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new SessionDoc
        {
            Id = id,
            UserId = userId,
            UserAgent = userAgent,
            Ip = ip,
            CreatedAt = DateTime.UtcNow,
            LastActive = DateTime.UtcNow,
            RevokedAt = null,
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Query the <c>mt</c> schema for the <c>AccessAudit</c> rows
    /// matching <c>targetKind</c> + <c>action</c> (the C3 same-
    /// transaction pin — the audit row is in the same schema as
    /// the domain write).
    /// </summary>
    private static async Task<IReadOnlyList<Kumunita.Core.Authorization.AccessAudit>>
        AuditsFor(IDocumentStore store, string targetKind, string action)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        return await Marten.QueryableExtensions.ToListAsync(s.Query<Kumunita.Core.Authorization.AccessAudit>()
            .Where(a => a.TargetKind == targetKind && a.Action == action), ct);
    }

    // ── Minimal service-provider / temp-data stubs ────────────────────────

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class DictionaryProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _dict;
        public DictionaryProvider(Dictionary<Type, object> dict) => _dict = dict;
        public object? GetService(Type serviceType) =>
            _dict.TryGetValue(serviceType, out var v) ? v : null;
    }

    /// <summary>
    /// An <see cref="IAuthenticationService"/> stub (the U7-
    /// defect-repair shape — see the U10 handoff note) so U7's
    /// <c>CurrentSessionIdAsync</c> (the <c>kumunita.sid</c> cookie-item read via
    /// <c>HttpContext.AuthenticateAsync</c>) resolves in the direct-
    /// construction harness. It returns a success
    /// <c>AuthenticateResult</c> whose <c>AuthenticationProperties
    /// .Items</c> carries the <c>kumunita.sid</c> item (the F5</c>
    /// <c>OnSigningIn</c> handler's shape, <c>Program.cs:890</c>) set to
    /// the test's "current session" id — so the kill-all extension's
    /// <c>RevokeAllButCurrentAsync</c> call preserves that session (the
    /// §2.3 shape 2 — the all-but-current rule).
    /// </summary>
    private sealed class SidAuthenticationService : IAuthenticationService
    {
        private readonly string _sessionId;
        public SidAuthenticationService(string sessionId) => _sessionId = sessionId;

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            var props = new Microsoft.AspNetCore.Authentication.AuthenticationProperties
            {
                Items = { ["kumunita.sid"] = _sessionId },
            };
            return Task.FromResult(AuthenticateResult.Success(
                new Microsoft.AspNetCore.Authentication.AuthenticationTicket(
                    new System.Security.Claims.ClaimsPrincipal(), props, scheme ?? "test")));
        }
        public Task ChallengeAsync(HttpContext context, string? scheme,
            Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties)
            => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme,
            Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties)
            => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme,
            System.Security.Claims.ClaimsPrincipal user,
            Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties)
            => Task.CompletedTask;
        public Task SignOutAsync(HttpContext context, string? scheme,
            Microsoft.AspNetCore.Authentication.AuthenticationProperties? properties)
            => Task.CompletedTask;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context)
            => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }

    /// <summary>
    /// A minimal <see cref="IUrlHelper"/> so <c>ControllerBase.Url</c>
    /// (and hence the <c>RedirectToAction(...)</c> path) resolves in
    /// the direct-construction harness (the <c>F5U4_KillAllTests</c>
    /// / <c>F3U5_MfaLoginTests</c> idiom).
    /// </summary>
    private sealed class StubUrlHelper : Microsoft.AspNetCore.Mvc.IUrlHelper
    {
        public StubUrlHelper(ActionContext actionContext)
        {
            ActionContext = actionContext;
        }

        public ActionContext ActionContext { get; }
        public string Action(string? actionName, string? controllerName, object? values)
            => $"/{controllerName}/{actionName}";
        public string Action(Microsoft.AspNetCore.Mvc.Routing.UrlActionContext actionContext)
            => "stub-action";
        public string Content(string? content) => content ?? string.Empty;
        public bool IsLocalUrl(string? url) => false;
        public string RouteUrl(string? routeName, object? values) => "stub-route";
        public string RouteUrl(Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext routeContext)
            => "stub-route";
        public string Link(string? linkName, object? values) => "stub-link";
    }
}
