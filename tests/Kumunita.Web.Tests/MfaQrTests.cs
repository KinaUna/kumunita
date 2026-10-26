using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0174 — the MFA enrollment surface's QR code + Security tab pins.
///
/// Drives the **real** <c>SecuritySettingsController.Mfa</c> enrollment-leg
/// action (the <c>GET /settings/security/mfa</c> response when MFA is not
/// yet enabled) over a real <c>UserManager&lt;User&gt;</c> + EF
/// <c>AppDbContext</c> (the <see cref="PostgresFixture"/> shape, the F3.U5
/// harness idiom).
///
/// <b>Pin names:</b>
/// <list type="number">
/// <item><see cref="MfaEnrollment_RespondsWith_QrDataUri_That_Is_A_Png_Encoding_The_OtpAuthUri"/>
///   — the enrollment-leg view model carries a non-null <c>QrDataUri</c>
///   that is a valid <c>data:image/png;base64,……</c> URI (PNG magic bytes
///   + a reasonable byte count), and the <c>OtpAuthUrl</c> field it
///   encodes contains the freshly-minted <c>SetupKey</c> (so the QR
///   encodes the right data, not the raw key or some other string).</item>
/// <item><see cref="MfaSteadyState_Does_Not_Emit_A_QrDataUri"/>
///   — when MFA is already enabled (the steady-state leg), the view model
///   has <c>QrDataUri == null</c> (the QR is scoped to the enrollment leg
///   only; a steady state has no fresh pairing key to encode — ADR 0174 D2).</item>
/// <item><see cref="SettingsTabs_Partial_Contains_SecurityTab_Link"/>
///   — the <c>_SettingsTabs.cshtml</c> partial contains a link to
///   <c>/settings/security/mfa</c> with the <c>settings.security.title</c>
///   kw-l key (the ADR 0174 D3 tab). Static text scan — no view
///   resolution needed (the <c>KwLRegistryConsistencyTests</c> idiom).</item>
/// </list>
/// </summary>
public sealed class MfaQrTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Email = "mfa-qr@examplium.com";
    private const string Password = "Sup3rSecret!";

    // ── Pin 1 — the enrollment leg emits a valid QR data URI ───────────────

    /// <summary>
    /// <c>GET /settings/security/mfa</c> on a user who does not yet have
    /// MFA enabled returns a <see cref="MfaSettingsViewModel"/> with
    /// <c>Enabled == false</c>, a non-null <c>SetupKey</c>, a non-null
    /// <c>OtpAuthUrl</c>, and a non-null <c>QrDataUri</c>. The
    /// <c>QrDataUri</c> starts with the <c>data:image/png;base64,</c>
    /// prefix, decodes to a PNG whose first 8 bytes are the PNG magic
    /// header (<c>0x89 0x50 0x4E 0x47 0x0D 0x0A 0x1A 0x0A</c>), and is
    /// at least 200 bytes (a 220×220 QR PNG is ~1 kB; a corrupt or
    /// empty encode would be well below this). The <c>OtpAuthUrl</c>
    /// field contains the <c>SetupKey</c> value — proving the QR encodes
    /// the <c>otpauth://</c> URI (which embeds the key), not the raw key
    /// or some other string.
    /// </summary>
    [Fact]
    public async Task MfaEnrollment_RespondsWith_QrDataUri_That_Is_A_Png_Encoding_The_OtpAuthUri()
    {
        var b = await BootAsync();
        var controller = await BuildControllerAsync(b);

        // The enrollment leg (MFA not yet enabled).
        var action = await controller.Mfa();
        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<MfaSettingsViewModel>(view.ViewData.Model);

        // The enrollment state.
        Assert.False(model.Enabled);
        Assert.NotNull(model.SetupKey);
        Assert.False(string.IsNullOrEmpty(model.SetupKey));
        Assert.NotNull(model.OtpAuthUrl);

        // The QR data URI is present.
        Assert.NotNull(model.QrDataUri);
        Assert.StartsWith("data:image/png;base64,", model.QrDataUri!);

        // Decode the base64 payload and verify the PNG magic bytes.
        var base64 = model.QrDataUri!["data:image/png;base64,".Length..];
        var pngBytes = Convert.FromBase64String(base64);

        // PNG magic header: 89 50 4E 47 0D 0A 1A 0A
        byte[] magic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        Assert.True(pngBytes.Length >= magic.Length,
            $"QR PNG is too small ({pngBytes.Length} bytes); expected at least {magic.Length}.");
        for (int i = 0; i < magic.Length; i++)
        {
            Assert.True(
                magic[i] == pngBytes[i],
                $"PNG magic byte {i} mismatch: expected 0x{magic[i]:X2}, got 0x{pngBytes[i]:X2}.");
        }

        // A 220×220 QR PNG with a quiet zone is at least ~200 bytes.
        Assert.True(pngBytes.Length >= 200,
            $"QR PNG is suspiciously small ({pngBytes.Length} bytes); " +
            "expected at least 200 bytes for a 5 px/module encode.");

        // The OtpAuthUrl contains the SetupKey (proves the QR encodes the
        // otpauth URI, not the raw key or some other string).
        Assert.True(
            model.OtpAuthUrl!.Contains(model.SetupKey!, StringComparison.Ordinal),
            "The OtpAuthUrl must contain the SetupKey (the otpauth URI embeds it).");

        // The issuer is "Kumunita - {host}" (ADR 0174 D1 errata 2, 2026-10-26).
        // The test sets Host = example.kumunita.com, so the issuer must be
        // "Kumunita - example.kumunita.com" and must NOT be the ASP.NET
        // Identity default "Microsoft.AspNetCore.Identity.UI".
        Assert.True(
            model.OtpAuthUrl.Contains("Kumunita", StringComparison.Ordinal),
            $"The OtpAuthUrl issuer must contain 'Kumunita'. Got: {model.OtpAuthUrl}");
        Assert.True(
            model.OtpAuthUrl.Contains("example.kumunita.com", StringComparison.Ordinal),
            $"The OtpAuthUrl issuer must contain the request host 'example.kumunita.com'. Got: {model.OtpAuthUrl}");
        Assert.True(
            !model.OtpAuthUrl.Contains("Microsoft.AspNetCore.Identity.UI", StringComparison.Ordinal),
            $"The OtpAuthUrl must not use the ASP.NET Identity default issuer. Got: {model.OtpAuthUrl}");
    }

    // ── Pin 2 — the steady state does not emit a QR ─────────────────────────

    /// <summary>
    /// <c>GET /settings/security/mfa</c> on a user who **already** has MFA
    /// enabled returns a <see cref="MfaSettingsViewModel"/> with
    /// <c>Enabled == true</c>, <c>SetupKey == null</c>,
    /// <c>OtpAuthUrl == null</c>, and <c>QrDataUri == null</c> (the QR is
    /// scoped to the enrollment leg only — ADR 0174 D2; a steady state has
    /// no fresh pairing key to encode).
    /// </summary>
    [Fact]
    public async Task MfaSteadyState_Does_Not_Emit_A_QrDataUri()
    {
        var b = await BootAsync();
        var controller = await BuildControllerAsync(b);

        // Enable MFA + set up the key + recovery codes (the enrollment
        // state the steady-state leg expects).
        await b.UserManager.SetTwoFactorEnabledAsync(b.User!, true);
        b.UserManager.GenerateNewAuthenticatorKey();
        await b.UserManager.GenerateNewTwoFactorRecoveryCodesAsync(b.User!, 10);

        // The steady-state leg (MFA already enabled).
        var action = await controller.Mfa();
        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<MfaSettingsViewModel>(view.ViewData.Model);

        Assert.True(model.Enabled);
        Assert.Null(model.SetupKey);
        Assert.Null(model.OtpAuthUrl);
        Assert.True(model.QrDataUri is null,
            "The steady-state leg must not emit a QrDataUri (ADR 0174 D2 — the QR is enrollment-leg only).");
        Assert.Equal(10, model.RemainingRecoveryCodes);
    }

    // ── Pin 3 — the settings tabs partial contains the Security tab ─────────

    /// <summary>
    /// The <c>_SettingsTabs.cshtml</c> partial (the ADR 0080 sub-nav strip)
    /// contains a link to <c>/settings/security/mfa</c> with the
    /// <c>settings.security.title</c> kw-l key (the ADR 0174 D3 Security
    /// tab). A static text scan — no view resolution needed (the
    /// <c>KwLRegistryConsistencyTests</c> idiom).
    /// </summary>
    [Fact]
    public void SettingsTabs_Partial_Contains_SecurityTab_Link()
    {
        // Walk up to the repo root (the Kumunita.slnx dir), then to Views/.
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null,
            "Could not locate the repo root (Kumunita.slnx) above the test output directory.");

        var tabsPath = Path.Combine(
            dir!.FullName, "src", "Kumunita.Web", "Views", "Locale", "_SettingsTabs.cshtml");
        Assert.True(File.Exists(tabsPath), $"_SettingsTabs.cshtml not found at {tabsPath}.");

        var content = File.ReadAllText(tabsPath);

        // The tab link is present.
        Assert.Contains("/settings/security/mfa", content, StringComparison.Ordinal);

        // The kw-l key is present.
        Assert.Contains("settings.security.title", content, StringComparison.Ordinal);
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private sealed record Harness
    {
        public required UserManager<User> UserManager { get; init; }
        public required User?            User        { get; init; }
    }

    private async Task<Harness> BootAsync()
    {
        var ct   = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

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

        var user = new User { Id = Guid.NewGuid().ToString("N"), Email = Email, UserName = Email };
        await userManager.CreateAsync(user, Password);

        return new Harness { UserManager = userManager, User = user };
    }

    private async Task<SecuritySettingsController> BuildControllerAsync(Harness b)
    {
        var ct = TestContext.Current.CancellationToken;

        var signInManager = Substitute.For<SignInManager<User>>(
            b.UserManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>());

        // The Mfa() action builds the issuer from Request.Host (e.g.
        // "Kumunita - example.kumunita.com"). Set a deterministic Host on
        // the test HttpContext so the issuer is predictable.
        var controller = new SecuritySettingsController(
            userManager:    b.UserManager,
            signInManager:  signInManager,
            identity:       Substitute.For<IIdentityService>());

        // Wire the signed-in principal (the Kumunita.Sub claim — the
        // KumunitaPrincipal.SubjectId read path; the repo idiom is
        // new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(...) }))).
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString("example.kumunita.com");
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Kumunita.Sub", b.User!.Id!) },
            authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return controller;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context)
            => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }
}
