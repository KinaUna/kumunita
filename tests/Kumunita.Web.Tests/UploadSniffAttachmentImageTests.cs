using System.Security.Claims;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Media;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// SEC remediation lane — U21 (finding **L2**, wire for the U20 pure sniffer):
/// the two §2.3-pinned regression tests exercising the **`Mismatch` rejection
/// path** of the upload wire in <see cref="AttachmentController.Upload"/>
/// (<c>POST /attachment</c>) and <see cref="ContentImageController.Upload"/>
/// (<c>POST /content-image</c>). The owner decision (2026-10-31, recorded in
/// <c>sec-handoff-notes.md</c>) is that the wire rejects on
/// <see cref="Kumunita.Web.Security.UploadSignatureSniffer.SniffVerdict.Mismatch"/>
/// **only**; <c>Match</c> and <c>Unknown</c> pass through. The landed
/// <see cref="AttachmentUploadTests.AttachUpload_Support_ZipLegacyAlias_IsAllowed"/>
/// (declared <c>application/x-zip-compressed</c> + PDF bytes → <c>Unknown</c>
/// → allowed) is the regression pin that proves the wire is on <c>Mismatch</c>,
/// not <c>Unknown</c>.
/// </summary>
/// <remarks>
/// Harness: the <see cref="AttachmentUploadTests"/> idiom — NSubstitute for
/// the interface seams, a <b>real</b> <see cref="PostService"/> (sealed —
/// unproxyable — and referenced by no action under test here; the
/// <c>Upload</c> action never calls it), <c>Options.Create</c> for
/// <see cref="MediaOptions"/>, and the <see cref="UploadGateTestSupport"/>
/// <c>IUploadGate</c> / <c>IStorageSettingsService</c> pair the M25 quota
/// lane added (the upload action resolves them from
/// <c>HttpContext.RequestServices</c>).
/// </remarks>
public class UploadSniffAttachmentImageTests
{
    // Synthetic leading bytes (16-byte heads, matching U21's 16-byte read).
    private static readonly byte[] PngHead  = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                                                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
    private static readonly byte[] HtmlHead = System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>");

    // The U20-pinned stored-id + actor (synthetic, no PII).
    private const string StoredId = "0f3a9c1e5b7d4a2c8e6f9a1b3c5d7e9f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d";
    private const string Actor    = "user-a";

    /// <summary>
    /// <c>POST /attachment</c> with a declared <c>image/png</c> but HTML
    /// bytes in the head is rejected <b>415</b> — the sniffer's
    /// <see cref="Kumunita.Web.Security.UploadSignatureSniffer.Classify"/>
    /// reports <c>Mismatch</c> for the declared <c>image/png</c> (the head
    /// bytes <c>&lt;script&gt;…</c> do not start with the PNG signature
    /// <c>89 50 4E 47</c>), and the wire rejects on <c>Mismatch</c>. No
    /// <see cref="IMediaStore.PutAsync"/> write happens — the guards-before-
    /// write ordering is preserved (the rejection fires before the
    /// <c>CopyToAsync</c> / <c>PutAsync</c> pair).
    /// </summary>
    [Fact]
    public async Task Attachment_MismatchedBytes_Rejected()
    {
        var (controller, media) = BuildAttachment();

        // No PutAsync expectation: the guard rejects before the write seam.
        var result = await controller.Upload(TestFile("evil.png", "image/png", HtmlHead));

        Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, ((StatusCodeResult)result).StatusCode);

        // The guards-before-write pin: no store write on the 415 path.
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <c>POST /content-image</c> with a declared <c>image/png</c> but HTML
    /// bytes in the head is rejected <b>415</b> — the sniffer's
    /// <see cref="Kumunita.Web.Security.UploadSignatureSniffer.Classify"/>
    /// reports <c>Mismatch</c>, and the wire rejects on <c>Mismatch</c>.
    /// No <see cref="IMediaStore.PutAsync"/> write happens.
    /// </summary>
    [Fact]
    public async Task ContentImage_MismatchedBytes_Rejected()
    {
        var (controller, media) = BuildContentImage();

        // No PutAsync expectation: the guard rejects before the write seam.
        var result = await controller.Upload(TestFile("evil.png", "image/png", HtmlHead));

        Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, ((StatusCodeResult)result).StatusCode);

        // The guards-before-write pin: no store write on the 415 path.
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── fixtures ──────────────────────────────────────────────────────────

    private static (AttachmentController Controller, IMediaStore Media) BuildAttachment()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var announcements = Substitute.For<IAnnouncementService>();
        var store = Substitute.For<Marten.IDocumentStore>();
        var posts = new PostService(userInfo, authz, store);

        var controller = new AttachmentController(
            media,
            Options.Create(new MediaOptions()),
            authz,
            posts,
            announcements,
            store);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Actor) },
                authenticationType: "test"));
        // Quota lane: the default U8 null → env fallback, 0 → quota disabled,
        // so the gate's CheckUpload returns null (no 413) and the test reaches
        // the sniffer guard.
        httpContext.RequestServices = UploadGateTestSupport.ServicesWith(
            new Kumunita.Core.Usage.CommunityStorageSettings { MaxFileBytes = null, PerUserQuotaBytes = 0 },
            0);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return (controller, media);
    }

    private static (ContentImageController Controller, IMediaStore Media) BuildContentImage()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var announcements = Substitute.For<IAnnouncementService>();
        var store = Substitute.For<Marten.IDocumentStore>();
        var posts = new PostService(userInfo, authz, store);

        var controller = new ContentImageController(
            media,
            authz,
            posts,
            announcements,
            Options.Create(new MediaOptions()),
            store);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Actor) },
                authenticationType: "test"));
        httpContext.RequestServices = UploadGateTestSupport.ServicesWith(
            new Kumunita.Core.Usage.CommunityStorageSettings { MaxFileBytes = null, PerUserQuotaBytes = 0 },
            0);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return (controller, media);
    }

    /// <summary>
    /// A minimal <see cref="IFormFile"/> carrier backed by a byte array — the
    /// action under test reads <c>Length</c> / <c>ContentType</c> /
    /// <c>FileName</c> and (for the sniffer wire) <c>OpenReadStream</c>; no
    /// multipart parsing at this seam. The <see cref="AttachmentUploadTests.TestFormFile"/>
    /// idiom (private there; replicated here).
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

        public Stream OpenReadStream(long startingOffset) =>
            startingOffset switch
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
}
