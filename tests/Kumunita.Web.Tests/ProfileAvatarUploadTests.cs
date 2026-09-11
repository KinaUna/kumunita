using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Media;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// U7a/b/c guard tests for the upload lane <see cref="ProfileController.AvatarUpload">AvatarUpload</see>
/// (design doc §2.7 FACES rows U7a–U7c + the test-name pin §2.5 L555–558, verbatim):
/// <list type="number">
/// <item><b>Valid raster (U7a success pin):</b> owner + allowed type + under
///       <see cref="MediaOptions.MaxBytes"/> → <c>PutAsync</c> is called <em>first</em>
///       (store-first, C-MED·4/·7), then <c>SetProfileAvatarAsync</c> with the
///       <em>stored object's</em> id, then the <c>RedirectToAction("Edit")</c>.
///       Self-scoped: actor and subject are the same (C-MED·8) — there is no
///       subject-pick path on this lane.</item>
/// <item><b>Oversize (U7b):</b> over a configured <see cref="MediaOptions.MaxBytes"/> →
///       <c>413</c>, and — the pin that matters for the content-address design —
///       <em>no file is written</em>: the guard fires before <c>PutAsync</c>, so no
///       <c>files/</c> entry, no <c>media_objects</c> row, no <c>Profile.AvatarId</c>
///       (C-MED·5).</item>
/// <item><b>Wrong type (U7c):</b> a non-raster <c>ContentType</c> → <c>415</c>; again
///       no <c>PutAsync</c>, no set, no redirect. This is the seam that makes the M11
///       "no SVG / arbitrary script" line <em>enforced</em>: the content-address name +
///       the <c>nosniff</c> serve (sibling test class's M-pins) are inert without the
///       whitelist guard at the door.</item>
/// <item><b>Empty (U7a defensive pin):</b> <c>null</c> or zero-byte <c>IFormFile</c> →
///       <c>400</c> with the message the design doc §2.4 fixates ("Choose an
///       image."); nothing is stored.</item>
/// </list>
/// <para>
/// <b>Carrier note (transparency of the pin):</b> the action is invoked directly, so the
/// <c>[ValidateAntiForgeryToken]</c> filter it would carry in the real middleware pipeline is
/// out of scope here (no filter pipeline runs under direct invocation). It is
/// belt-and-braces on top of these <em>server-side</em> guards (the FACES rows) —
/// the §2.4 note. The file carrier is a real <see cref="FormFile"/> over a
/// <see cref="MemoryStream"/> — <c>Length</c>, <c>ContentType</c>, <c>FileName</c> and
/// <c>CopyToAsync</c> behave exactly as the real multipart-pipeline file would.
/// </para>
/// <para>
/// Harness: same pattern as <see cref="ProfileAvatarServingTests"/> /
/// <see cref="AnnouncementControllerTests"/> — NSubstitute for the three interface seams,
/// a real <see cref="DirectoryService"/> (sealed, unreferenced by the action), and a
/// principal carrying the single <c>Kumunita.Sub</c> claim.
/// </para>
/// </summary>
public class ProfileAvatarUploadTests
{
    private const string Owner = "subj-owner-001";

    /// <summary>A stand-in avatar id — 64 lowercase hex chars (the C-MED·4 SHA-256 content-address shape).</summary>
    private const string MediaId =
        "9f2c4d6e8a1b3c5d7e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d";

    /// <summary>A stand-in 12-byte PNG payload (the real PNG magic header + filler).</summary>
    private static readonly byte[] Png =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03,
    };

    /// <summary>U7a success pin — owner's valid raster: store first, then set with the stored id, then the Edit redirect.</summary>
    [Fact]
    public async Task Upload_OwnerValidRaster_SetsAvatarAndServes()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var controller = Build(userInfo, authz, media, new MediaOptions(), Owner);

        var mediaObject = new MediaObject
        {
            Id = MediaId,
            ContentType = "image/png",
            SizeBytes = Png.Length,
            CreatedById = Owner,
        };
        // The store receives the validated payload — the guard already ran (U7a/b/c pins).
        media.PutAsync(Arg.Is<byte[]>(b => b.SequenceEqual(Png)), "pixel.png", "image/png", Owner, Arg.Any<CancellationToken>())
            .Returns(mediaObject);

        var result = await controller.AvatarUpload(
            TestFile("pixel.png", "image/png", Png));

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Edit", redirect.ActionName);
        // Store-first, then set — the ordering pin (C-MED·4/·7):
        await media.Received(1).PutAsync(Arg.Is<byte[]>(b => b.SequenceEqual(Png)), "pixel.png", "image/png", Owner, Arg.Any<CancellationToken>());
        await userInfo.Received(1).SetProfileAvatarAsync(Owner, MediaId, Owner);
    }

    /// <summary>U7b — oversize: 413, and <em>no file written</em> — the guard fires before <c>PutAsync</c>.</summary>
    [Fact]
    public async Task Upload_Oversize_Returns413_NoFileWritten()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        // Configure the limit deliberately low so the test payload is "oversize":
        var controller = Build(userInfo, authz, media, new MediaOptions { MaxBytes = 16 }, Owner);

        var result = await controller.AvatarUpload(
            TestFile("big.png", "image/png", new byte[32]));

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, status.StatusCode);
        await media.DidNotReceiveWithAnyArgs().PutAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await userInfo.DidNotReceiveWithAnyArgs().SetProfileAvatarAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    /// <summary>U7c — wrong type: 415, and no file written.</summary>
    [Fact]
    public async Task Upload_WrongType_Returns415_NoFileWritten()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var controller = Build(userInfo, authz, media, new MediaOptions(), Owner);

        var result = await controller.AvatarUpload(
            TestFile("logo.svg", "image/svg+xml", Png)); // the bytes don't matter — the type does

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, status.StatusCode);
        await media.DidNotReceiveWithAnyArgs().PutAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await userInfo.DidNotReceiveWithAnyArgs().SetProfileAvatarAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    /// <summary>U7a defensive — empty upload: 400, and no file written.</summary>
    [Fact]
    public async Task Upload_Empty_Returns400()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var controller = Build(userInfo, authz, media, new MediaOptions(), Owner);

        var result = await controller.AvatarUpload(
            TestFile("empty.png", "image/png", Array.Empty<byte>()));

        // Note: the §2.4 pin names the client-facing message ("Choose an image."),
        // but ASP.NET Core's `BadRequest(object? error)` does not surface that
        // string on the result object — the message is a source-level pin on the
        // action, so the test can only pin the 400 status (plus the "no writes"
        // half) from this harness.
        // `BadRequest(string)` (unlike the 413/415 `StatusCode(int)` above) produces
        // a `BadRequestObjectResult : ObjectResult` — a *sibling* branch of the
        // `ActionResult` hierarchy, not a `StatusCodeResult` — so the exact type
        // must be `BadRequestObjectResult`; the `StatusCode` property is still
        // the shared status-code surface to pin 400.
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, bad.StatusCode);
        await media.DidNotReceiveWithAnyArgs().PutAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await userInfo.DidNotReceiveWithAnyArgs().SetProfileAvatarAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    // ── fixtures

    /// <summary>
    /// A minimal <see cref="IFormFile"/> carrier backed by a byte array: the
    /// action under test reads <c>Length</c> / <c>ContentType</c> / <c>FileName</c>
    /// and copies the bytes — no multipart parsing at this seam. A local
    /// implementation is used instead of the ASP.NET <c>FormFile</c> class,
    /// whose <c>set_ContentType</c> path throws a <c>NullReferenceException</c>
    /// when driven from this harness.
    /// </summary>
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

        public void CopyTo(Stream target)
            => target.Write(_content, 0, _content.Length);

        public void CopyTo(Stream target, CancellationToken cancellationToken)
            => target.Write(_content, 0, _content.Length);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        {
            target.Write(_content, 0, _content.Length);
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }

    private static IFormFile TestFile(string fileName, string? contentType, byte[] content)
        => new TestFormFile(fileName, contentType, content);

    /// <summary>
    /// Builds the <see cref="ProfileController"/> the repo's controller-test idiom does
    /// (<see cref="AnnouncementControllerTests"/>): NSubstitute for the three interface
    /// seams, a <em>real</em> <see cref="DirectoryService"/> (sealed — unproxyable — and
    /// referenced by no action under test), <c>Options.Create</c> for
    /// <paramref name="mediaOptions"/>, and a principal carrying the single
    /// <c>Kumunita.Sub</c> claim <see cref="KumunitaPrincipal"/> mints.
    /// </summary>
    private static ProfileController Build(
        IUserInfoService userInfo,
        Kumunita.Core.Authorization.IAuthorizationService authz,
        IMediaStore media,
        MediaOptions mediaOptions,
        string principalSubjectId)
    {
        var controller = new ProfileController(
            userInfo,
            new DirectoryService(userInfo, authz),
            authz,
            media,
            Options.Create(mediaOptions));

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, principalSubjectId) },
                authenticationType: "test"));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
