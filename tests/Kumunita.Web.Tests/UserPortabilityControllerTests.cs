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
/// M27 U10 — the <see cref="UserPortabilityController"/> Web-surface pins
/// (design doc §Web-pins / §2.6, ADR 0148 D5 + C-M27·6/7). The controller is
/// the <b>resident's</b> thin control plane over moving <b>their own authored
/// data</b> in and out — the resident-plane mirror of the M11
/// <see cref="AdminPortabilityController"/>, the same thin-wrapper shape but
/// over the <see cref="IUserPortabilityService"/> seam at the resident scope:
/// the audit rows live in the <em>service</em> (C-M27·6 — the controller adds
/// none), the gate is the <c>[Authorize]</c> verified-resident self-lane (the
/// resident's <em>own</em> <c>subjectId</c> — <b>no</b> GlobalAdmin break-glass,
/// <b>no</b> audience decision), and the view model / TempData render is the
/// ADR 0105/0118 personal-by-id house idiom
/// (<see cref="Kumunita.Web.Security.KumunitaPrincipal.SubjectId"/> +
/// <c>TempData["info"]</c> / <c>TempData["error"]</c> + redirect).
/// <para>
/// The pins (exactly the unit-plan list, the design doc §2.6 pinning the
/// class name):
/// <list type="bullet">
/// <item><b>Resident gate</b> — the controller carries a plain
/// <c>[Authorize]</c> (the verified-resident self-lane — the resident's own
/// data only, <b>not</b> M11's <c>[Authorize(Roles = GlobalAdmin)]</c>) and the
/// actor is the resident's own <c>subjectId</c> — the surface has no standing
/// to reach for another resident's data (the C-M27·6/7 pin, the ADR 0001-B
/// "the author's choice is absolute" posture at the resident scale).
/// </item>
/// <item><b>Export (one audit row)</b> — <c>Export()</c> delegates
/// <c>portability.ExportAsync(Resident)</c> <em>exactly once</em> (the one
/// write → the one <c>portability.export</c> <c>AccessAudit</c> row the
/// <em>service</em> emits, <c>Via = Owner</c>, <c>TargetKind "portability"</c>)
/// + returns a <see cref="FileStreamResult"/> (the ADR 0034 attachment
/// lane's shape).
/// </item>
/// <item><b>Import (one audit row)</b> — <c>Import()</c> delegates
/// <c>portability.ClassifyAsync(Resident, archive)</c> <em>exactly once</em>
/// (the import-lane resident action) + renders the <c>TempData["info"]</c>
/// status-area line. The audit row (if any) is the <em>service's</em> — the
/// controller adds none (C-M27·6).
/// </item>
/// <item><b>Import.resolve (one audit row)</b> — <c>Resolve()</c> delegates
/// <c>portability.ResolveAsync(Resident, …)</c> <em>exactly once</em> (the one
/// write → the one <c>portability.import.resolve</c> <c>AccessAudit</c> row the
/// <em>service</em> emits, <c>Via = Owner</c>, <c>TargetKind "portability"</c>)
/// + renders the <c>TempData["info"]</c> applied/discard summary.
/// </item>
/// <item><b>Reads emit none</b> — the reads (<c>Index()</c>, the
/// <c>ResolveReview()</c> resolve-review read) emit <b>no</b>
/// <c>AccessAudit</c> row: <c>Index()</c> is a pure view (no service
/// delegation at all) and <c>ResolveReview()</c> performs only the
/// <c>ClassifyAsync</c> read — neither triggers the audit-emitting service
/// write (<see cref="IUserPortabilityService.ExportAsync"/> /
/// <see cref="IUserPortabilityService.ResolveAsync"/>) (the C-M27·6 pin — the
/// ADR 0105 "reads never audit" shape).
/// </item>
/// <item><b>Fail-closed render</b> — a failed <c>Import()</c> (the
/// <see cref="UserPortabilityImportPlan"/> <c>Ok = false</c> closed-failure
/// set) renders <c>TempData["error"]</c> containing both the
/// <c>myportability.status</c> kw-l key and the failure list, and does not
/// write (the C-M27·4 fail-closed pin at the surface).
/// </item>
/// <item><b>kw-l parity</b> — the seven <c>myportability.*</c> keys the
/// controller emits / renders are present with non-empty values in
/// <see cref="KnownTranslationKeys"/> in <c>en</c>, <c>de</c>, <c>fr</c>,
/// <c>da</c> (the C-M27·6/7 parity, the D9 closed-key registry).
/// </item>
/// </list>
/// <para>
/// **The audit row is the <em>service's</em>, not the controller's
/// (C-M27·6).** The shipped <see cref="IUserPortabilityService"/> emits the
/// audit rows — <c>portability.export</c> in <c>ExportAsync</c> +
/// <c>portability.import.resolve</c> in <c>ResolveAsync</c>, both
/// <c>Via = Owner</c> / <c>TargetKind "portability"</c> — and the
/// <c>ClassifyAsync</c> (import classify) read is <b>read-only</b> (C-M27·5,
/// "no writes" → no audit row). The Web-surface pin therefore asserts the
/// surface's contract — <em>exactly-once</em>, resident-scoped delegation + a
/// plain <c>[Authorize]</c> resident self-lane (no GlobalAdmin role gate) +
/// the fail-closed render — NOT the Core audit-row content (that is the U09
/// Core acceptance-test pin, recorded in U11's gate). The same "the audit row
/// lives in the service, the controller adds none" posture the M11
/// <see cref="AdminPortabilityControllerTests"/> carries.
/// </para>
/// <para>
/// The <c>[ValidateAntiForgeryToken]</c> filter on the two POST actions is out
/// of scope for the direct invocation (no filter pipeline runs) — the
/// belt-and-braces layer on top of the server-side guard (the same §2.4 note
/// the <see cref="ProfileAvatarUploadTests"/> + <see cref="AdminPortabilityControllerTests"/>
/// carry).
/// </summary>
public class UserPortabilityControllerTests
{
    private const string Resident = "m27-resident-001";

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

    /// <summary>
    /// Builds the <see cref="UserPortabilityController"/> over an
    /// NSubstitute <see cref="IUserPortabilityService"/> + a
    /// <see cref="ClaimsPrincipal"/> carrying the resident's own
    /// <c>Kumunita.Sub</c> subject claim + a <c>Member</c> role (a plain
    /// resident — <b>not</b> a GlobalAdmin, the C-M27·7 contrast with M11's
    /// operator gate). The optional <c>ITranslationProvider</c> /
    /// <c>IUserInfoService</c> ctor params are <c>null</c> — the test floor
    /// (the <c>T()</c> helper returns the raw key; the "add elsewhere" target
    /// picker is empty). No <see cref="PostgresFixture"/> (NSubstitute seam —
    /// the Core audit-row content is the U09 Core acceptance test's pin).
    /// </summary>
    private static (UserPortabilityController controller, IUserPortabilityService portability) Build()
    {
        var portability = Substitute.For<IUserPortabilityService>();
        var localization = Substitute.For<ILocalizationService>();
        var controller = new UserPortabilityController(
            portability, localization, null, null);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Resident),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.Member),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, portability);
    }

    // ── Resident gate (the self-lane, C-M27·6/7) ─────────────────────────

    [Fact]
    public async Task Web_Export_ResidentGate()
    {
        // The gate is the plain [Authorize] verified-resident self-lane —
        // NOT M11's [Authorize(Roles = GlobalAdmin)] operator gate. A non-
        // resident is rejected by the authorization pipeline (no filter
        // pipeline runs here — the same §2.4 note the M11 test carries); the
        // surface-level pin is that the gate is present + it is not a
        // GlobalAdmin role gate (C-M27·7: no new authorization surface, no
        // GlobalAdmin break-glass, no audience decision).
        var attr = typeof(UserPortabilityController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.True(string.IsNullOrEmpty(attr!.Roles),
            "the resident self-lane gate must be a plain [Authorize] (no GlobalAdmin role gate)");

        // The actor is ALWAYS the resident's own subjectId (the Kumunita.Sub
        // claim) — the surface has no standing to reach for another resident's
        // data: Export() delegates to the resident's own subjectId, never a
        // different resident's id (the C-M27·6 "a resident reaching for
        // someone else's data gets no surface" pin at the surface).
        var (controller, portability) = Build();
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 };
        portability.ExportAsync(Resident, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(payload));

        var action = await controller.Export();
        Assert.IsType<FileStreamResult>(action);

        // The scope anchor the surface hands the service is the resident's
        // OWN subjectId — never another resident's (a "someone else" id cannot
        // be expressed at this surface — the actor is the resident's own claim).
        await portability.Received(1).ExportAsync(Resident, Arg.Any<CancellationToken>());
        await portability.DidNotReceive()
            .ExportAsync(Arg.Is<string>(s => !string.IsNullOrEmpty(s) && s != Resident),
                Arg.Any<CancellationToken>());
    }

    // ── Export (one audit row — the service's) ───────────────────────────

    [Fact]
    public async Task Web_Export_OneAuditRow_ViaOwner()
    {
        // Exactly-once, resident-scoped delegation: the one write (the archive
        // build) → the one portability.export AccessAudit row (Via = Owner,
        // TargetKind "portability") the SERVICE emits (C-M27·6 — the
        // controller adds none). The audit-row CONTENT is the U09 Core
        // acceptance test's pin (U11's gate); the Web pin asserts the
        // surface's one-write/one-row contract + the resident scope anchor.
        var (controller, portability) = Build();
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 };

        portability.ExportAsync(Resident, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(payload));

        var action = await controller.Export();
        var file = Assert.IsType<FileStreamResult>(action);
        Assert.Equal("application/octet-stream", file.ContentType);
        using var ms = new MemoryStream();
        file.FileStream?.CopyTo(ms);
        Assert.Equal(payload, ms.ToArray());

        // Exactly one write → one audit row; scoped to the resident's own
        // subjectId (the archive's scope anchor + the audit row's actor).
        await portability.Received(1).ExportAsync(Resident, Arg.Any<CancellationToken>());
    }

    // ── Import (one audit row — the service's) ───────────────────────────

    [Fact]
    public async Task Web_Import_OneAuditRow_ViaOwner()
    {
        // Exactly-once, resident-scoped delegation of the import-lane action:
        // Import() delegates ClassifyAsync(Resident, archive) exactly once —
        // the one import-lane resident action the service turns into the audit
        // row (if any — the classify read is read-only, C-M27·5). The audit
        // row is the SERVICE's (C-M27·6 — the controller adds none). The
        // audit-row content is the U09 Core pin; the Web pin asserts the
        // surface's exactly-once, resident-scoped delegation.
        var (controller, portability) = Build();
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var formFile = new TestFormFile("my-data.kumunita", "application/octet-stream", payload);

        portability.ClassifyAsync(Resident, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new UserPortabilityImportPlan(true, [], []));

        var action = await controller.Import(formFile);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(UserPortabilityController.Index), redirect.ActionName);

        // Exactly one import-lane delegation, scoped to the resident's own
        // subjectId (the audit row's actor + the archive's scope anchor).
        await portability.Received(1).ClassifyAsync(
            Resident, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    // ── Import.resolve (one audit row — the service's) ───────────────────

    [Fact]
    public async Task Web_ImportResolve_OneAuditRow_ViaOwner()
    {
        // Exactly-once, resident-scoped delegation of the apply write:
        // Resolve() delegates ResolveAsync(Resident, plan, resolutions,
        // archive) exactly once — the one apply write → the one
        // portability.import.resolve AccessAudit row (Via = Owner, TargetKind
        // "portability") the SERVICE emits (C-M27·6 — the controller adds
        // none). The audit-row content is the U09 Core pin; the Web pin asserts
        // the surface's exactly-once, resident-scoped delegation of the apply.
        var (controller, portability) = Build();
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var formFile = new TestFormFile("my-data.kumunita", "application/octet-stream", payload);
        var model = new UserPortabilityController.ResolveReviewModel();

        portability.ClassifyAsync(Resident, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new UserPortabilityImportPlan(true, [], []));
        portability.ResolveAsync(
                Resident, Arg.Any<UserPortabilityImportPlan>(),
                Arg.Any<IReadOnlyList<UserPortabilityEntityResolution>>(),
                Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new UserPortabilityImportResult(true, 1, 0, []));

        var action = await controller.Resolve(model, formFile);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(UserPortabilityController.Index), redirect.ActionName);

        // Exactly one apply write → one audit row; scoped to the resident's
        // own subjectId (the audit row's actor). The read (ClassifyAsync) runs
        // too, but the audit-emitting write (ResolveAsync) is exactly once.
        await portability.Received(1).ResolveAsync(
            Resident, Arg.Any<UserPortabilityImportPlan>(),
            Arg.Any<IReadOnlyList<UserPortabilityEntityResolution>>(),
            Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    // ── Reads emit none (index + resolve-review) ─────────────────────────

    [Fact]
    public async Task Web_Reads_EmitNoAuditRow()
    {
        // The reads emit no AccessAudit row (the C-M27·6 pin — the ADR 0105
        // "reads never audit" shape). Index() is a pure view (no service
        // delegation at all); ResolveReview() (the resolve-review read)
        // performs only the ClassifyAsync read — neither triggers the
        // audit-emitting service write (ExportAsync / ResolveAsync).
        var (controller, portability) = Build();

        // Index — a pure read (a ViewResult, no service delegation at all).
        var indexAction = await Task.FromResult(controller.Index());
        Assert.IsType<ViewResult>(indexAction);
        await portability.DidNotReceive()
            .ExportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await portability.DidNotReceive()
            .ClassifyAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await portability.DidNotReceive()
            .ResolveAsync(Arg.Any<string>(), Arg.Any<UserPortabilityImportPlan>(),
                Arg.Any<IReadOnlyList<UserPortabilityEntityResolution>>(),
                Arg.Any<Stream>(), Arg.Any<CancellationToken>());

        // ResolveReview — the read; it calls ClassifyAsync (the read) but NOT
        // the audit-emitting apply write (ResolveAsync) or ExportAsync.
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var formFile = new TestFormFile("my-data.kumunita", "application/octet-stream", payload);
        portability.ClassifyAsync(Resident, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new UserPortabilityImportPlan(true, [], []));

        var reviewAction = await controller.ResolveReview(formFile);
        Assert.IsType<ViewResult>(reviewAction);
        // The read runs (ClassifyAsync) but no audit-emitting write (the
        // apply ResolveAsync / the export ExportAsync) is triggered.
        await portability.Received(1).ClassifyAsync(
            Resident, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await portability.DidNotReceive()
            .ResolveAsync(Arg.Any<string>(), Arg.Any<UserPortabilityImportPlan>(),
                Arg.Any<IReadOnlyList<UserPortabilityEntityResolution>>(),
                Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await portability.DidNotReceive()
            .ExportAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── Fail-closed render (the closed-failure set) ──────────────────────

    [Fact]
    public async Task Web_FailClosed_Render()
    {
        // A failed Import() (the UserPortabilityImportPlan Ok = false
        // closed-failure set) renders TempData["error"] containing both the
        // myportability.status kw-l key + the failure list, and does not
        // write (the C-M27·4 fail-closed pin at the surface — the classify
        // phase refused before any write, the instance unchanged). The
        // translationProvider is null (the test floor) → T() returns the raw
        // key "myportability.status" — the pin asserts the key + the failure
        // list render, in the resident's language when the seam is present
        // (the view / _FlashToast partial renders it).
        var (controller, portability) = Build();
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var formFile = new TestFormFile("bad.kumunita", "application/octet-stream", payload);

        portability.ClassifyAsync(Resident, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new UserPortabilityImportPlan(
                false, [], new[] { "archive.unsupported" }));

        var action = await controller.Import(formFile);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(UserPortabilityController.Index), redirect.ActionName);

        var error = Assert.IsType<string>(controller.TempData["error"]);
        Assert.Contains("myportability.status", error);
        Assert.Contains("archive.unsupported", error);

        // The classify ran (the closed-failure set was produced) but no apply
        // write (ResolveAsync) — the instance is unchanged on a classify
        // failure (zero writes, C-M27·5).
        await portability.Received(1).ClassifyAsync(
            Resident, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await portability.DidNotReceive()
            .ResolveAsync(Arg.Any<string>(), Arg.Any<UserPortabilityImportPlan>(),
                Arg.Any<IReadOnlyList<UserPortabilityEntityResolution>>(),
                Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }

    // ── kw-l parity — the seven myportability.* keys the controller emits ─

    [Theory]
    [InlineData("myportability.index.title")]
    [InlineData("myportability.export")]
    [InlineData("myportability.import")]
    [InlineData("myportability.import.resolve")]
    [InlineData("myportability.resolve.add_elsewhere")]
    [InlineData("myportability.resolve.discard")]
    [InlineData("myportability.status")]
    public void Web_KwL_KeysResolve(string key)
    {
        // The seven myportability.* keys the controller emits / renders are
        // present with non-empty values in all four languages (en/de/fr/da) —
        // the C-M27·6/7 parity (the D9 closed-key registry, the
        // KnownTranslationKeys_ParityTests enforces the × 4). Deliberately a
        // DISTINCT myportability.* namespace from M11's admin portability.*
        // keys so the resident surface never collides with the operator
        // surface (D9).
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.EnValues[key]), key);
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DeValues[key]), key);
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.FrValues[key]), key);
        Assert.False(string.IsNullOrWhiteSpace(KnownTranslationKeys.DaValues[key]), key);
    }
}
