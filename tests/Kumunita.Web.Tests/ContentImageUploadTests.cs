using System.Security.Claims;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Media;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using Marten;

namespace Kumunita.Web.Tests;

/// <summary>
/// RC U07 — the 4 <b>upload</b> seam tests pinned by the design doc
/// (<c>rich-content-design.md</c> §Pinned seam tests, items 17–20), against
/// <see cref="ContentImageController.Upload"/> (<c>POST /content-image</c>).
/// <para>
/// The pin is R·6: <b>ADR 0011's boundary, verbatim</b> — the same allowlist
/// (<c>image/jpeg|png|webp|gif</c>, SVG excluded), the same
/// <see cref="MediaOptions.MaxBytes"/> cap, and the same
/// <b>guards-before-write</b> ordering (empty → 400, oversize → 413,
/// disallowed type → 415) with <b>no file written on any guard</b> — then
/// exactly one <see cref="IMediaStore.PutAsync"/> write (R·6).
/// </para>
/// <para>
/// <b>Drift pause (recorded here + in the handoff note):</b> the 4
/// <c>R4_*</c>/<c>R5_Orphan_*</c> <b>serving</b> tests in
/// <c>ContentImageServingTests.cs</c> are all documented-paused — the
/// serving route's owner resolution calls the concrete, sealed
/// <see cref="PostService"/> reverse-lookup seams
/// (<see cref="PostService.FindPostByImageIdAsync"/> /
/// <see cref="PostService.FindReplyByImageIdAsync"/>), which are
/// Postgres-backed (Marten) and therefore undrivable from this
/// NSubstitute-only Web harness (no <c>PostgresFixture</c> / Testcontainers
/// here, and <c>PostService</c> is <b>sealed</b> — NSubstitute cannot proxy
/// it). The <b>upload</b> action under test in this file <b>never touches
/// <c>PostService</c></b> (R·6 — the write lane is the ADR 0011 boundary;
/// the id it returns is the store's), so all 4 upload tests are drivable:
/// the <c>PostService posts</c> ctor argument is satisfied by a real
/// <see cref="PostService"/> over never-used substitutes (its ctor
/// null-checks only), and the assertions below pin the guard ordering +
/// the single-write shape without ever calling it.
/// </para>
/// <para>
/// Harness: the NSubstitute controller-test idiom from
/// <see cref="ProfileAvatarUploadTests"/> /
/// <see cref="AnnouncementControllerTests"/> — NSubstitute for the
/// interface seams, a <b>real</b> <see cref="PostService"/> (sealed —
/// unproxyable — and unreferenced by the action under test),
/// <c>Options.Create</c> for <see cref="MediaOptions"/>, and a principal
/// carrying the single <c>Kumunita.Sub</c> claim
/// <see cref="Kumunita.Web.Security.KumunitaPrincipal"/> mints.
/// </para>
/// </summary>
public class ContentImageUploadTests
{
    private const string Actor = "subj-rc-u07-uploader";

    /// <summary>A stand-in content hash — 64 lowercase hex chars (the C-MED·4 SHA-256 content-address shape).</summary>
    private const string StoredId = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

    /// <summary>A small valid PNG payload (the real PNG magic header + filler).</summary>
    private static readonly byte[] Png =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03,
    };

    // ── 17 — Upload_Empty_400_NoFileWritten (R·6) ─────────────────────────
    //
    // A null or zero-byte <c>IFormFile</c> → 400, and <em>no file written</em>:
    // the guard fires before <c>PutAsync</c> (R·6's "no file written on any
    // guard"). The <c>IFormFile</c> carrier is a real byte-backed
    // <see cref="IFormFile"/> (the <see cref="ProfileAvatarUploadTests"/>
    // precedent — the action reads <c>Length</c> / <c>ContentType</c> /
    // <c>FileName</c> and copies the bytes).

    [Fact]
    public async Task Upload_Empty_400_NoFileWritten()
    {
        var (controller, media) = Build(mediaOptions: new MediaOptions(), actor: Actor);

        // Two write shapes must both 400: a <b>null</b> file …
        var nullResult = await controller.Upload(null);
        Assert.IsType<BadRequestObjectResult>(nullResult);
        // … and a <b>zero-byte</b> file …
        var emptyResult = await controller.Upload(TestFile("empty.png", "image/png", Array.Empty<byte>()));
        Assert.IsType<BadRequestObjectResult>(emptyResult);

        // R·6: no file written on the empty guard.
        await media.DidNotReceiveWithAnyArgs().PutAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── 18 — Upload_Oversize_413_NoFileWritten (R·6) ──────────────────────
    //
    // A valid content type over <see cref="MediaOptions.MaxBytes"/> → 413,
    // and <em>no file written</em>: the guard fires before <c>PutAsync</c>.
    // The cap is configured low (16 bytes) so the 12-byte-plus payload is
    // deliberately "oversize" — the <c>MaxBytes</c> seam (the avatar test's
    // property shape).

    [Fact]
    public async Task Upload_Oversize_413_NoFileWritten()
    {
        var (controller, media) = Build(mediaOptions: new MediaOptions { MaxBytes = 16 }, actor: Actor);

        // A 32-byte payload (over the 16-byte cap):
        var result = await controller.Upload(TestFile("big.png", "image/png", new byte[32]));

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, status.StatusCode);

        // R·6: no file written on the oversize guard.
        await media.DidNotReceiveWithAnyArgs().PutAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── 19 — Upload_DisallowedType_Svg_415_NoFileWritten (R·6) ────────────
    //
    // A non-allowlisted <c>ContentType</c> → 415, and <em>no file written</em>.
    // The <b>two asserts</b> (per the plan's note: "a second assert for a type
    // not in the allowlist at all"):
    // <list type="bullet">
    // <item><b>SVG</b> — <c>image/svg+xml</c> — ADR 0011's named exclusion
    //       (the canonical "disallowed type" case: a scriptable vector type
    //       that the avatar lane's whitelist explicitly refuses).</item>
    // <item><b>TIFF</b> — <c>image/tiff</c> — a raster type <b>not</b> in the
    //       allowlist at all (<c>image/jpeg|png|webp|gif</c>); the allowlist
    //       is closed, so an otherwise-harmless raster is still refused.</item>
    // </list>
    // Both 415, both with zero writes (R·6).

    [Fact]
    public async Task Upload_DisallowedType_Svg_415_NoFileWritten()
    {
        var (controller, media) = Build(mediaOptions: new MediaOptions(), actor: Actor);

        // Assert 1 — SVG (ADR 0011's named exclusion):
        var svg = await controller.Upload(TestFile("logo.svg", "image/svg+xml", Png));
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeResult>(svg).StatusCode);

        // Assert 2 — TIFF (a raster type not in the allowlist at all):
        var tiff = await controller.Upload(TestFile("photo.tiff", "image/tiff", Png));
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeResult>(tiff).StatusCode);

        // R·6: no file written on the disallowed-type guard (either assert).
        await media.DidNotReceiveWithAnyArgs().PutAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── 20 — Upload_Valid_200_ReturnsJsonId (R·6) ─────────────────────────
    //
    // A small valid PNG under the cap → the store's <c>PutAsync</c> returns a
    // <see cref="MediaObject"/> with <c>Id = "deadbeef…"</c> → 200 + the JSON
    // body <c>{ "id": "deadbeef…" }</c> (the design doc's pinned response
    // shape) + <c>PutAsync</c> received <b>exactly once</b> with the stored
    // filename/content type/actor (R·6's "one write" + guard→write ordering
    // made executable).

    [Fact]
    public async Task Upload_Valid_200_ReturnsJsonId()
    {
        var (controller, media) = Build(mediaOptions: new MediaOptions(), actor: Actor);

        var stored = new MediaObject
        {
            Id = StoredId,
            ContentType = "image/png",
            SizeBytes = Png.Length,
            CreatedById = Actor,
        };
        media.PutAsync(Arg.Is<byte[]>(b => b.SequenceEqual(Png)), "pixel.png", "image/png", Actor, Arg.Any<CancellationToken>())
            .Returns(stored);

        var result = await controller.Upload(TestFile("pixel.png", "image/png", Png));

        // The pinned response shape. The action returns <c>Json(new { id =
        // stored.Id })</c> — a <see cref="JsonResult"/> (a subtype of
        // <c>ObjectResult</c>). Its <c>StatusCode</c> is unset (null): the
        // controller relies on ASP.NET Core's <b>implicit 200</b> for a
        // <c>JsonResult</c> with no explicit status (the "200" in the pinned
        // name is that implicit default, not an explicit status code), so
        // the executable pin is the <b>JSON body</b> — a JSON object
        // containing <c>"id":"deadbeef…"</c> (the design doc's response
        // shape). Serialize the <c>Value</c> to the wire form and assert it.
        var json = Assert.IsType<JsonResult>(result);
        var wire = System.Text.Json.JsonSerializer.Serialize(json.Value);
        Assert.Contains($"\"id\":\"{StoredId}\"", wire);

        // R·6: exactly one write, with the validated filename/content
        // type/actor (the guard→write ordering made executable):
        await media.Received(1).PutAsync(Arg.Is<byte[]>(b => b.SequenceEqual(Png)), "pixel.png", "image/png", Actor, Arg.Any<CancellationToken>());
    }

    // ── fixtures ──────────────────────────────────────────────────────────

    /// <summary>
    /// A minimal <see cref="IFormFile"/> carrier backed by a byte array: the
    /// action under test reads <c>Length</c> / <c>ContentType</c> /
    /// <c>FileName</c> and copies the bytes — no multipart parsing at this
    /// seam. A local implementation is used instead of the ASP.NET
    /// <c>FormFile</c> class (the <see cref="ProfileAvatarUploadTests"/>
    /// precedent — its <c>set_ContentType</c> path throws an NRE when driven
    /// from this harness).
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
    /// Builds the <see cref="ContentImageController"/> the repo's
    /// controller-test idiom does (<see cref="ProfileAvatarUploadTests"/> /
    /// <see cref="AnnouncementControllerTests"/>): NSubstitute for the
    /// interface seams, a <em>real</em> <see cref="PostService"/> (sealed —
    /// unproxyable — and referenced by no action under test here; the
    /// <c>Upload</c> action never calls it, so the substitutes it carries
    /// are never exercised), <c>Options.Create</c> for
    /// <paramref name="mediaOptions"/>, and a principal carrying the single
    /// <c>Kumunita.Sub</c> claim <see cref="Kumunita.Web.Security.KumunitaPrincipal"/> mints.
    /// </summary>
    private static (ContentImageController Controller, IMediaStore Media) Build(
        MediaOptions mediaOptions, string actor)
    {
        // The PostService ctor null-checks its three arguments (no DB work in
        // the ctor); the Upload action never calls it, so substitutes suffice
        // — the concrete-seam requirement is satisfied without a Postgres.
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var announcements = Substitute.For<IAnnouncementService>();
        var pages = Substitute.For<ITranslationProvider>();
        var posts = new PostService(userInfo, authz, Substitute.For<IDocumentStore>());

        var controller = new ContentImageController(
            media,
            authz,
            posts,
            announcements,
            pages,
            Options.Create(mediaOptions));

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, actor) },
                authenticationType: "test"));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return (controller, media);
    }
}
