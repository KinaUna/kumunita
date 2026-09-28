using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Portability;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using System.Reflection;
using Claim = System.Security.Claims.Claim;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;

namespace Kumunita.Web.Tests;

/// <summary>
/// M11 U07 — the <see cref="AdminPortabilityController"/> Web-surface pins
/// (design doc §D7 "Web pins", ADR 0108 D5 + C-M11·6/7). The controller is
/// the GlobalAdmin's thin operator-plane control over the
/// <see cref="IPortabilityService"/> export/import seams — the same
/// shape as <see cref="AdminMessagingController"/> /
/// <see cref="AdminSignupController"/>: the audit row lives in the
/// <em>service</em> (C-M11·6 — the controller adds none), the gate is
/// the <c>GlobalAdmin</c> role (C-M11·7 — no new authorization surface),
/// and the view model / TempData render is the operator-plane idiom
/// (<see cref="Kumunita.Web.Security.KumunitaPrincipal.SubjectId"/> +
/// <c>TempData["info"]</c> / <c>TempData["error"]</c> + redirect).
/// <para>
/// The pins (exactly the design doc §D7 list):
/// <list type="bullet">
/// <item><b>Gate</b> — the controller carries
/// <c>[Authorize(Roles = Roles.GlobalAdmin)]</c> (the C-M11·7 gate).
/// </item>
/// <item><b>Export</b> — <c>Export()</c> delegates
/// <c>portability.ExportAsync(Admin)</c> exactly once + returns a
/// <see cref="FileContentResult"/> (the ADR 0034 attachment lane's
/// shape: <c>Content-Disposition: attachment</c> +
/// <c>X-Content-Type-Options: nosniff</c>).
/// </item>
/// <item><b>Import (ok)</b> — a clean <see cref="PortabilityImportResult"/>
/// renders <c>TempData["info"] = "portability.status.ok"</c> + redirects
/// to Index.
/// </item>
/// <item><b>Import (failure)</b> — a failed
/// <see cref="PortabilityImportResult"/> renders
/// <c>TempData["error"]</c> containing both the
/// <c>portability.status.failure</c> kw-l key and the failure list
/// (the C-M11·4 fail-closed pin at the surface).
/// </item>
/// <item><b>Import (empty)</b> — a <c>null</c> or zero-byte
/// <c>IFormFile</c> renders <c>TempData["error"]</c> = the
/// <c>portability.status.failure</c> kw-l key and does <em>not</em>
/// call <c>ImportAsync</c> (the defensive guard).
/// </item>
/// <item><b>kw-l parity</b> — the six <c>portability.*</c> keys the
/// controller emits / renders are present with non-empty values in
/// <see cref="KnownTranslationKeys"/> in <c>en</c>, <c>de</c>,
/// <c>fr</c>, <c>da</c>.
/// </item>
/// </list>
/// The [ValidateAntiForgeryToken] filter is out of scope for the direct
/// invocation (no filter pipeline runs) — the belt-and-braces layer on
/// top of the server-side guard (the same §2.4 note the
/// <see cref="ProfileAvatarUploadTests"/> carries).
/// </summary>
public class AdminPortabilityControllerTests
{
    private const string Admin = "pt-admin-001";
    private const string MediaId =
        "9f2c4d6e8a1b3c5d7e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    /// <summary>A minimal <see cref="IFormFile"/> carrier backed by a byte array.</summary>
    private sealed class TestFormFile : IFormFile
    {
        private readonly byte[] _content;

        public TestFormFile(string fileName, string? contentType, byte[] content)
        {
            _content = content ?? Array.Empty<byte>();
            FileName = fileName;
            Name = "file";
            ContentType = contentType ?? string.Empty;
            ContentDisposition = string.Empty;
            Length = _content.Length;
        }

        public long Length { get; }
        public string FileName { get; }
        public string Name { get; }
        public string ContentType { get; set; }
        public string ContentDisposition { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Open() => new MemoryStream(_content, false);
        public Stream OpenReadStream() => new MemoryStream(_content, false);
        public Stream OpenReadStream(long startingOffset)
            => startingOffset switch
            {
                0 => new MemoryStream(_content, false),
                _ => new MemoryStream(_content[(int)startingOffset..], false),
            };

        public void CopyTo(Stream target) => target.Write(_content, 0, _content.Length);
        public void CopyTo(Stream target, CancellationToken cancellationToken)
            => target.Write(_content, 0, _content.Length);
        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            target.Write(_content, 0, _content.Length);
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }

    private static (AdminPortabilityController controller, IPortabilityService portability) Build()
    {
        var portability = Substitute.For<IPortabilityService>();
        var localization = Substitute.For<ILocalizationService>();
        // translationProvider: null — the test floor (the T() helper
        // falls back to returning the key raw).
        var controller = new AdminPortabilityController(
            portability, localization, null);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, portability);
    }

    // ── Gate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Controller_Carries_GlobalAdmin_Role_Authorize()
    {
        var attr = typeof(AdminPortabilityController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Roles.GlobalAdmin, attr!.Roles);
    }
    // ── Export ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_DelegatesToServiceAndReturnsFile()
    {
        var (controller, portability) = Build();
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 };

        portability.ExportAsync(Admin, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(payload));

        var action = await controller.Export();
        var file = Assert.IsType<FileStreamResult>(action);
        Assert.Equal("application/octet-stream", file.ContentType);
        using var ms = new MemoryStream();
        file.FileStream?.CopyTo(ms);
        Assert.Equal(payload, ms.ToArray());

        await portability.Received(1).ExportAsync(Admin, Arg.Any<CancellationToken>());
    }

    // ── Import (ok) ───────────────────────────────────────────────────────

    [Fact]
    public async Task Import_Ok_DelegatesAndRendersInfo()
    {
        var (controller, portability) = Build();
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var formFile = new TestFormFile("kumunita.kumunita", "application/octet-stream", payload);

        portability.ImportAsync(Admin, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(PortabilityImportResult.Success);

        var action = await controller.Import(formFile);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminPortabilityController.Index), redirect.ActionName);

        Assert.Equal("portability.status.ok", controller.TempData["info"]);

        await portability.Received(1).ImportAsync(
            Admin, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    // ── Import (failure) ──────────────────────────────────────────────────

    [Fact]
    public async Task Import_Failed_DelegatesAndRendersErrorWithFailures()
    {
        var (controller, portability) = Build();
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var formFile = new TestFormFile("kumunita.kumunita", "application/octet-stream", payload);

        portability.ImportAsync(Admin, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new PortabilityImportResult(false, new[] { "format.unsupported" }));

        var action = await controller.Import(formFile);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminPortabilityController.Index), redirect.ActionName);

        var error = Assert.IsType<string>(controller.TempData["error"]);
        Assert.Contains("portability.status.failure", error);
        Assert.Contains("format.unsupported", error);

        await portability.Received(1).ImportAsync(
            Admin, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    // ── Import (null / empty guard) ───────────────────────────────────────

    [Fact]
    public async Task Import_NullFile_RendersErrorAndDoesNotDelegate()
    {
        var (controller, portability) = Build();

        var action = await controller.Import(null!);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminPortabilityController.Index), redirect.ActionName);

        Assert.Equal("portability.status.failure", controller.TempData["error"]);

        await portability.DidNotReceive()
            .ImportAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_ZeroByteFile_RendersErrorAndDoesNotDelegate()
    {
        var (controller, portability) = Build();
        var formFile = new TestFormFile("empty.kumunita", "application/octet-stream", []);

        var action = await controller.Import(formFile);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminPortabilityController.Index), redirect.ActionName);

        Assert.Equal("portability.status.failure", controller.TempData["error"]);

        await portability.DidNotReceive()
            .ImportAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    // ── kw-l parity — the six portability.* keys the controller emits ────

    [Theory]
    [InlineData("portability.index.title")]
    [InlineData("portability.export")]
    [InlineData("portability.import")]
    [InlineData("portability.confirm.import")]
    [InlineData("portability.status.ok")]
    [InlineData("portability.status.failure")]
    public void KwLPortabilityKeys_ArePresent_En(string key)
    {
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.EnValues[key]));
    }

    [Theory]
    [InlineData("portability.index.title")]
    [InlineData("portability.export")]
    [InlineData("portability.import")]
    [InlineData("portability.confirm.import")]
    [InlineData("portability.status.ok")]
    [InlineData("portability.status.failure")]
    public void KwLPortabilityKeys_ArePresent_De(string key)
    {
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DeValues[key]));
    }

    [Theory]
    [InlineData("portability.index.title")]
    [InlineData("portability.export")]
    [InlineData("portability.import")]
    [InlineData("portability.confirm.import")]
    [InlineData("portability.status.ok")]
    [InlineData("portability.status.failure")]
    public void KwLPortabilityKeys_ArePresent_Fr(string key)
    {
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.FrValues[key]));
    }

    [Theory]
    [InlineData("portability.index.title")]
    [InlineData("portability.export")]
    [InlineData("portability.import")]
    [InlineData("portability.confirm.import")]
    [InlineData("portability.status.ok")]
    [InlineData("portability.status.failure")]
    public void KwLPortabilityKeys_ArePresent_Da(string key)
    {
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DaValues[key]));
    }
}
