using System.Security.Claims;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
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
/// ATT U11 — the 3 <b>upload</b> seam tests pinned by the design doc
/// (<c>file-attachments-design.md</c> §2.9, items 6–8), against
/// <see cref="AttachmentController.Upload"/> (<c>POST /attachment</c>).
/// <para>
/// The pin is C-ATT·6: <b>ADR 0011's boundary, verbatim</b> — the
/// <b>attachment</b> allowlist (<see cref="MediaOptions.AttachmentAllowedContentTypes"/>
/// — a <b>separate</b> gate from the image lane's raster-only
/// <see cref="MediaOptions.AllowedContentTypes"/>, C-ATT·9), the same
/// <see cref="MediaOptions.MaxBytes"/> cap, and the same
/// <b>guards-before-write</b> ordering — empty → 400, oversize → 413,
/// disallowed type → 415 — with <b>no file written on any guard</b> — then
/// exactly one <see cref="IMediaStore.PutAsync"/> write (F6).
/// </para>
/// <para>
/// <b>Executable (F6 ×3):</b> unlike the 5 serve tests (drift-paused in
/// <see cref="AttachmentServingTests"/> — the sealed <see cref="PostService"/>
/// reverse-lookup is undrivable from this NSubstitute-only harness), the
/// <c>Upload</c> action <b>never touches <see cref="PostService"/></b> (the
/// write lane is the ADR 0011 boundary; the id it returns is the store's), so
/// all 3 upload tests are drivable: the <c>PostService posts</c> +
/// <c>IMediaStore</c> + <c>IAnnouncementService</c> ctor arguments are
/// satisfied by NSubstitute (a <b>real</b> <see cref="PostService"/> over
/// never-used substitutes, the <see cref="ContentImageUploadTests"/> idiom —
/// its ctor null-checks only, and <c>Upload</c> never calls it).
/// </para>
/// <para>
/// <b>Allowlist pick (recorded):</b> the default attachment allowlist
/// (12 types: pdf, msword, docx, ms-excel, xlsx, text/plain, text/csv, zip, +
/// the 4 raster image types) <b>excludes</b> SVG (C-ATT·6's named exclusion)
/// and — for the second F6 assert — <c>video/mp4</c> (a type not in the
/// default at all; the allowlist is closed, so an otherwise-harmless media
/// type is still refused). Neither is <i>in</i> the default.
/// </para>
/// </summary>
public class AttachmentUploadTests
{
    private const string Actor = "subj-att-u11-uploader";

    /// <summary>A stand-in content hash — 64 lowercase hex chars (the C-MED·4 SHA-256 content-address shape).</summary>
    private const string StoredId = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

    /// <summary>A small valid PDF payload (the real PDF magic header <c>%PDF-1.</c> + filler).</summary>
    private static readonly byte[] Pdf =
    {
        0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x00, 0x01, 0x02,
    };

    // ── 6 — AttachUpload_F6_Empty400 (C-ATT·6) ─────────────────────────────
    //
    // A null or zero-byte <c>IFormFile</c> → 400, and <em>no file written</em>:
    // the guard fires before <c>PutAsync</c> (F6's "no file written on any
    // guard"). Mirrors <see cref="ContentImageUploadTests.Upload_Empty_400_NoFileWritten"/>.

    [Fact]
    public async Task AttachUpload_F6_Empty400()
    {
        var (controller, media) = Build(mediaOptions: new MediaOptions(), actor: Actor);

        // Two write shapes must both 400: a <b>null</b> file …
        var nullResult = await controller.Upload(null);
        Assert.IsType<BadRequestObjectResult>(nullResult);
        // … and a <b>zero-byte</b> file …
        var emptyResult = await controller.Upload(TestFile("empty.pdf", "application/pdf", Array.Empty<byte>()));
        Assert.IsType<BadRequestObjectResult>(emptyResult);

        // F6: no file written on the empty guard.
        await media.DidNotReceiveWithAnyArgs().PutAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── 7 — AttachUpload_F6_Oversize413 (C-ATT·6) ──────────────────────────
    //
    // An allowed content type over <see cref="MediaOptions.MaxBytes"/> → 413,
    // and <em>no file written</em>: the guard fires before <c>PutAsync</c>.
    // The cap is configured low (16 bytes) so the 32-byte payload is
    // deliberately "oversize" (the <c>MaxBytes</c> seam, the image test's
    // property shape). Mirrors <see cref="ContentImageUploadTests.Upload_Oversize_413_NoFileWritten"/>.

    [Fact]
    public async Task AttachUpload_F6_Oversize413()
    {
        var (controller, media) = Build(mediaOptions: new MediaOptions { MaxBytes = 16 }, actor: Actor);

        // A 32-byte PDF payload (over the 16-byte cap):
        var result = await controller.Upload(TestFile("big.pdf", "application/pdf", new byte[32]));

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, status.StatusCode);

        // F6: no file written on the oversize guard.
        await media.DidNotReceiveWithAnyArgs().PutAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── 8 — AttachUpload_F6_WrongType415 (C-ATT·6) ─────────────────────────
    //
    // A non-allowlisted <c>ContentType</c> → 415, and <em>no file written</em>.
    // The <b>two asserts</b> (per the plan: "a second assert for a type not in
    // the allowlist at all"):
    // <list type="bullet">
    // <item><b>SVG</b> — <c>image/svg+xml</c> — C-ATT·6's named exclusion
    //       (a scriptable vector type the attachment allowlist explicitly
    //       refuses).</item>
    // <item><b>Video</b> — <c>video/mp4</c> — a type <b>not</b> in the
    //       attachment allowlist at all (the 12-type default has no
    //       <c>video/*</c>); the allowlist is closed, so an otherwise-harmless
    //       media type is still refused.</item>
    // </list>
    // Both 415, both with zero writes (F6). Mirrors
    // <see cref="ContentImageUploadTests.Upload_DisallowedType_Svg_415_NoFileWritten"/>.

    [Fact]
    public async Task AttachUpload_F6_WrongType415()
    {
        var (controller, media) = Build(mediaOptions: new MediaOptions(), actor: Actor);

        // Assert 1 — SVG (C-ATT·6's named exclusion):
        var svg = await controller.Upload(TestFile("logo.svg", "image/svg+xml", Pdf));
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeResult>(svg).StatusCode);

        // Assert 2 — video/mp4 (a type not in the attachment allowlist at all):
        var video = await controller.Upload(TestFile("clip.mp4", "video/mp4", Pdf));
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeResult>(video).StatusCode);

        // F6: no file written on the disallowed-type guard (either assert).
        await media.DidNotReceiveWithAnyArgs().PutAsync(Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── Support (NOT one of the 10 pinned names) ───────────────────────────
    //
    // A small valid PDF under the cap, on the attachment allowlist → the
    // store's <c>PutAsync</c> returns a <see cref="MediaObject"/> → 200 (the
    // implicit <see cref="JsonResult"/> status) + the JSON body
    // <c>{ "id": "…" }</c> (the design doc's pinned response shape) +
    // <c>PutAsync</c> received <b>exactly once</b> (F6's "one write" +
    // guard→write ordering made executable). Mirrors
    // <see cref="ContentImageUploadTests.Upload_Valid_200_ReturnsJsonId"/>.
    // Deliberately <b>not</b> one of the §2.9 pinned names — a support test
    // pinning the positive path so the 3 guard tests have a contrast.

    [Fact]
    public async Task AttachUpload_Support_Valid_Pdf_ReturnsJsonId_AndOneWrite()
    {
        var (controller, media) = Build(mediaOptions: new MediaOptions(), actor: Actor);

        var stored = new MediaObject
        {
            Id = StoredId,
            ContentType = "application/pdf",
            SizeBytes = Pdf.Length,
            CreatedById = Actor,
        };
        media.PutAsync(Arg.Is<byte[]>(b => b.SequenceEqual(Pdf)), "report.pdf", "application/pdf", Actor, Arg.Any<CancellationToken>())
            .Returns(stored);

        var result = await controller.Upload(TestFile("report.pdf", "application/pdf", Pdf));

        // The pinned response shape: a <see cref="JsonResult"/> whose body is a
        // JSON object containing <c>"id":"…"</c> (the implicit 200 is the
        // controller's reliance on ASP.NET Core's default for a JsonResult with
        // no explicit status).
        var json = Assert.IsType<JsonResult>(result);
        var wire = System.Text.Json.JsonSerializer.Serialize(json.Value);
        Assert.Contains($"\"id\":\"{StoredId}\"", wire);

        // Exactly one write, with the validated filename/content type/actor:
        await media.Received(1).PutAsync(Arg.Is<byte[]>(b => b.SequenceEqual(Pdf)), "report.pdf", "application/pdf", Actor, Arg.Any<CancellationToken>());
    }

    // ── fixtures ──────────────────────────────────────────────────────────

    /// <summary>
    /// A minimal <see cref="IFormFile"/> carrier backed by a byte array: the
    /// action under test reads <c>Length</c> / <c>ContentType</c> /
    /// <c>FileName</c> and copies the bytes — no multipart parsing at this
    /// seam. A local implementation (the <see cref="ContentImageUploadTests"/>
    /// precedent).
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
    /// Builds the <see cref="AttachmentController"/> the repo's controller-test
    /// idiom does (<see cref="ContentImageUploadTests"/>): NSubstitute for the
    /// interface seams, a <em>real</em> <see cref="PostService"/> (sealed —
    /// unproxyable — and referenced by no action under test here; the
    /// <c>Upload</c> action never calls it, so the substitutes it carries are
    /// never exercised), <c>Options.Create</c> for
    /// <paramref name="mediaOptions"/>, and a principal carrying the single
    /// <c>Kumunita.Sub</c> claim <see cref="Kumunita.Web.Security.KumunitaPrincipal"/>
    /// reads.
    /// </summary>
    private static (AttachmentController Controller, IMediaStore Media) Build(
        MediaOptions mediaOptions, string actor)
    {
        // The PostService ctor null-checks its three arguments (no DB work in
        // the ctor); the Upload action never calls it, so substitutes suffice
        // — the concrete-seam requirement is satisfied without a Postgres.
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var announcements = Substitute.For<IAnnouncementService>();
        var posts = new PostService(userInfo, authz, Substitute.For<IDocumentStore>());

        var controller = new AttachmentController(
            media,
            Options.Create(mediaOptions),
            authz,
            posts,
            announcements,
            Substitute.For<IDocumentStore>());

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, actor) },
                authenticationType: "test"));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return (controller, media);
    }
}
