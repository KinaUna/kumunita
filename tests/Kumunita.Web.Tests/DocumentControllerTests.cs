using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Documents;
using Kumunita.Core.Identity;
using Kumunita.Core.Media;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M21 (ADR 0122, U03) — the <see cref="DocumentController"/> seam tests
/// (GATE-1 / GATE-2 / GATE-5 / GATE-6 + the D3 allowlist assertions). The
/// <see cref="DocumentService"/> is a **real sealed** object (like
/// <see cref="Kumunita.Core.Posts.PostService"/>) — the read paths
/// (<c>ListAsync</c> / <c>GetAsync</c>) are driven against its
/// <see cref="IDocumentStore"/> seams:
/// <list type="bullet">
/// <item><b>Feed</b> (<c>ListAsync</c> → <c>Query&lt;Document&gt;()
/// .ToListAsync()</c>) — Marten 9's <c>ToListAsync()</c> casts to the
/// internal <c>MartenLinqQueryable</c> and cannot be NSubstituted, so the
/// feed tests run a **real** scratch-Postgres store (the
/// <see cref="ProjectsControllerTests"/> / <see cref="M14InterlockTests"/>
/// precedent, <see cref="PostgresFixture"/> in this assembly).</item>
/// <item><b>Detail / serve / upload</b> (<c>GetAsync</c> →
/// <c>LoadAsync&lt;Document&gt;</c> + <c>UploadAsync</c> →
/// <c>LightweightSession().Store</c>) — directly stubbable on the
/// <see cref="IQuerySession"/> / <see cref="IDocumentSession"/> interfaces
/// (the <see cref="AnnouncementControllerTests"/> idiom — pure NSubstitute).</item>
/// </list>
/// The frozen <see cref="IAuthorizationService"/> Read path is NSubstituted
/// (the <c>CanSeeAsync</c> feed is never reached on the empty-feed early
/// return; the <c>CanAsync</c> detail/serve is stubbed Allow / Deny). The
/// upload writes **no <c>AccessAudit</c> row** (U00 §1.a A2 — the
/// <see cref="Kumunita.Core.Posts.PostService"/>.CreatePostAsync /
/// <c>AttachmentController.Upload</c> convention): the write is the single
/// <c>Document</c> store in the caller's session.
/// </summary>
public sealed class DocumentControllerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Actor = "subj-m21-actor";

    /// <summary>A 32-char lowercase-hex doc id (the <c>Guid.ToString("N")</c> shape — passes <c>IsValidDocId</c>).</summary>
    private const string DocId = "0123456789abcdef0123456789abcdef";

    /// <summary>A 64-char lowercase-hex content-hash media id (the ADR 0011 SHA-256 shape).</summary>
    private const string MediaId = "deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef";

    /// <summary>A small valid PDF payload (the PDF magic header <c>%PDF-1.</c> + filler).</summary>
    private static readonly byte[] Pdf =
    {
        0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x00, 0x01, 0x02,
    };

    // ══════════════════════════════════════════════════════════════════════
    // Feed (GATE-1) — real scratch-Postgres (the ListAsync ToListAsync wall)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GATE-1 / F1 — <c>GET /documents</c> for a **GlobalAdmin** returns
    /// 200 + the feed view, and <see cref="DocumentIndexViewModel.CanUpload"/>
    /// is **true** (the D5 standing — the view (U04) shows the "Upload" link
    /// without re-resolving standing per-row). The empty-feed early return
    /// (Total: 0) is driven against a real store (the
    /// <c>Query&lt;Document&gt;().ToListAsync()</c> seam — un-NSubstitutable).
    /// </summary>
    [Fact]
    public async Task Index_GlobalAdmin_CanUploadTrue_FeedView()
    {
        var controller = await BuildFeedControllerAsync(roles: new[] { "GlobalAdmin" });

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<DocumentIndexViewModel>(view.ViewData.Model);
        Assert.Empty(vm.Visible);
        Assert.False(vm.HasMore);
        Assert.True(vm.CanUpload, "A GlobalAdmin may upload (D5 standing); the view (U04) shows the Upload link.");
    }

    /// <summary>
    /// GATE-1 / F1 — <c>GET /documents</c> for a **plain resident** (no
    /// elevated standing) returns 200 + the feed view, and
    /// <see cref="DocumentIndexViewModel.CanUpload"/> is **false** (the
    /// non-privileged actor sees the feed but no upload affordance — D5).
    /// </summary>
    [Fact]
    public async Task Index_Resident_CanUploadFalse_FeedView()
    {
        var controller = await BuildFeedControllerAsync(roles: Array.Empty<string>());

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<DocumentIndexViewModel>(view.ViewData.Model);
        Assert.Empty(vm.Visible);
        Assert.False(vm.CanUpload, "A non-privileged resident may not upload (D5 standing).");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Detail (GATE-2) — NSubstitute (LoadAsync + CanAsync stubs)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GATE-2 / D7 — <c>GET /documents/{id}</c> for a **Deny** returns
    /// <b>404</b> (not 403 — no existence leak). <see
    /// cref="DocumentService.GetAsync"/> runs the single
    /// <see cref="IAuthorizationService.CanAsync"/> decision (one decision
    /// audit row, emitted by the seam) and returns <c>Document = null</c> on
    /// Deny; the controller maps that to <see cref="NotFoundResult"/>. The
    /// missing and the Deny are the **same** 404 (C-M21·5 — feed and detail
    /// agree on the deny posture).
    /// </summary>
    [Fact]
    public async Task Detail_Deny_Returns_404_Not_403()
    {
        var (controller, authz) = BuildDetailController(allow: false);

        var result = await controller.Detail(DocId);

        Assert.IsType<NotFoundResult>(result); // 404, not 403 (D7 — no existence leak)
        // The single CanAsync decision ran (one decision row, by the seam — C-M21·4).
        await authz.Received(1).CanAsync(
            Actor, AccessAction.Read, Arg.Any<IAuditableResource>());
    }

    /// <summary>
    /// GATE-2 / D7 — <c>GET /documents/{id}</c> for a **missing** document
    /// returns 404. <see cref="DocumentService.GetAsync"/> loads null →
    /// <c>Document = null</c> (no decision ran, no audit row — the M2 detail
    /// shape); the controller maps that to <see cref="NotFoundResult"/>.
    /// </summary>
    [Fact]
    public async Task Detail_Missing_Returns_404()
    {
        var (controller, authz) = BuildDetailController(allow: false, missing: true);

        var result = await controller.Detail(DocId);

        Assert.IsType<NotFoundResult>(result);
        // A missing document runs NO decision (no CanAsync, no audit row).
        await authz.DidNotReceiveWithAnyArgs().CanAsync(
            Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>());
    }

    /// <summary>
    /// GATE-2 / C-M21·5 — <c>GET /documents/{id}</c> for an **Allow** returns
    /// 200 + the detail view, with <see cref="DocumentDetailViewModel
    /// .DownloadUrl"/> pointing at <c>/documents/{id}/download</c> and
    /// <see cref="DocumentDetailViewModel.CanDownload"/> true (the view (U04)
    /// renders the download link). The single <see
    /// cref="IAuthorizationService.CanAsync"/> Allow decision ran (one decision
    /// row, by the seam — C-M21·4).
    /// </summary>
    [Fact]
    public async Task Detail_Allow_Returns_200_With_DownloadUrl()
    {
        var (controller, authz) = BuildDetailController(allow: true);

        var result = await controller.Detail(DocId);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<DocumentDetailViewModel>(view.ViewData.Model);
        Assert.Equal(DocId, vm.Document.Id);
        Assert.True(vm.CanDownload, "The GetAsync decision allowed; the download re-runs the same Read (C-M21·5).");
        Assert.Equal($"/documents/{DocId}/download", vm.DownloadUrl);
        await authz.Received(1).CanAsync(
            Actor, AccessAction.Read, Arg.Any<IAuditableResource>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // Serve / download (GATE-5) — NSubstitute
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GATE-5 / D7 — <c>GET /documents/{id}/download</c> for a **Deny**
    /// returns 404 (not 403). The single <see cref="IAuthorizationService
    /// .CanAsync"/> Deny decision ran inside <see cref="DocumentService
    /// .GetAsync"/> (one decision row, by the seam); the controller maps the
    /// null to <see cref="NotFoundResult"/> and **never streams the bytes**
    /// (no <c>OpenReadAsync</c> on Deny).
    /// </summary>
    [Fact]
    public async Task Serve_Deny_Returns_404()
    {
        var (controller, media, authz) = BuildServeController(allow: false);

        var result = await controller.Serve(DocId);

        Assert.IsType<NotFoundResult>(result); // 404, not 403 (D7)
        await authz.Received(1).CanAsync(
            Actor, AccessAction.Read, Arg.Any<IAuditableResource>());
        // The bytes never stream on Deny (the decision is the gate).
        await media.DidNotReceiveWithAnyArgs().OpenReadAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// GATE-5 / D7 — <c>GET /documents/{id}/download</c> for a **missing**
    /// document returns 404 (no decision ran, no audit row; the store is
    /// never consulted).
    /// </summary>
    [Fact]
    public async Task Serve_Missing_Returns_404()
    {
        var (controller, media, authz) = BuildServeController(allow: false, missing: true);

        var result = await controller.Serve(DocId);

        Assert.IsType<NotFoundResult>(result);
        await authz.DidNotReceiveWithAnyArgs().CanAsync(
            Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>());
        await media.DidNotReceiveWithAnyArgs().GetAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// GATE-5 / C-M21·6 — <c>GET /documents/{id}/download</c> where the
    /// document **exists + is allowed** but its blob is **missing** (an
    /// orphan) returns 404. The decision ran (Allow, one decision row); the
    /// <see cref="IMediaStore.GetAsync"/> miss → the document is the authority
    /// for access, not the blob (C-M21·6 — an orphan blob is a 404).
    /// </summary>
    [Fact]
    public async Task Serve_Orphan_Blob_Returns_404()
    {
        var (controller, media, authz) = BuildServeController(allow: true, orphan: true);

        var result = await controller.Serve(DocId);

        Assert.IsType<NotFoundResult>(result); // orphan blob → 404 (C-M21·6)
        await authz.Received(1).CanAsync(
            Actor, AccessAction.Read, Arg.Any<IAuditableResource>());
        await media.Received(1).GetAsync(MediaId, Arg.Any<CancellationToken>());
        // The bytes never stream on an orphan (the GetAsync miss is the gate).
        await media.DidNotReceiveWithAnyArgs().OpenReadAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// GATE-5 / D6 / C-M21·6 — <c>GET /documents/{id}/download</c> for an
    /// **Allow** returns a <see cref="FileResult"/> with the **stored**
    /// <c>Content-Type</c> (already validated at write — no second allowlist
    /// check), the response carries <c>X-Content-Type-Options: nosniff</c> +
    /// <c>Content-Disposition: attachment; filename=…</c> (D6 — the download,
    /// never an inline render). The single <see cref="IAuthorizationService
    /// .CanAsync"/> Allow decision ran (one decision row, by the seam);
    /// <c>PutAsync</c> is never called (a read surface).
    /// </summary>
    [Fact]
    public async Task Serve_Allow_Serves_Attachment_Nosniff_StoredContentType()
    {
        var (controller, media, authz) = BuildServeController(allow: true);

        var result = await controller.Serve(DocId);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/pdf", file.ContentType); // the stored Content-Type (D6)
        Assert.NotNull(file.FileStream); // the open blob stream (OpenReadAsync)

        // The response headers (set on the ControllerContext's Response — D6).
        var response = controller.ControllerContext.HttpContext.Response;
        Assert.Equal("nosniff", response.Headers["X-Content-Type-Options"]);
        var disposition = response.Headers["Content-Disposition"];
        Assert.StartsWith("attachment;", disposition, StringComparison.Ordinal);
        Assert.Contains("filename=\"", disposition, StringComparison.Ordinal);

        // One Allow decision (by the seam), one store read, one stream open.
        await authz.Received(1).CanAsync(
            Actor, AccessAction.Read, Arg.Any<IAuditableResource>());
        await media.Received(1).GetAsync(MediaId, Arg.Any<CancellationToken>());
        await media.Received(1).OpenReadAsync(MediaId, Arg.Any<CancellationToken>());
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // Upload standing (GATE-6) — NSubstitute
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// GATE-6 / D5 — <c>POST /documents</c> for a **non-privileged** actor
    /// (a plain resident) returns <b>404</b> (not 403 — the form's existence
    /// is not leaked). The standing gate fires at the Web boundary **before**
    /// any guard or write; <see cref="IMediaStore.PutAsync"/> is **never**
    /// called (no file written). The Core seam is standing-agnostic (C-M21·7)
    /// — it is never reached.
    /// </summary>
    [Fact]
    public async Task Upload_NonPrivileged_Returns_404_Not_403()
    {
        var (controller, media, _, _) = BuildUploadController(roles: Array.Empty<string>());

        var form = ValidUploadForm(TestFile("bylaws.pdf", "application/pdf", Pdf));
        var result = await controller.Upload(form);

        Assert.IsType<NotFoundResult>(result); // 404, not 403 (D5 — no existence leak)
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // Upload allowlist (D3) — NSubstitute (guards-before-write, no Put)
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// D3 / C-M21·7 — <c>POST /documents</c> with a **null or zero-byte**
    /// file → 400, and <em>no file written</em>: the guard fires before
    /// <c>PutAsync</c>. A **GlobalAdmin** passes the standing gate first.
    /// Mirrors <see cref="AttachmentUploadTests.AttachUpload_F6_Empty400"/>.
    /// </summary>
    [Fact]
    public async Task Upload_Empty_File_400_NoPut()
    {
        var (controller, media, _, _) = BuildUploadController(roles: new[] { "GlobalAdmin" });

        // A null file …
        var nullResult = await controller.Upload(ValidUploadForm(null));
        Assert.IsType<BadRequestObjectResult>(nullResult);
        // … and a zero-byte file (both 400).
        var emptyResult = await controller.Upload(ValidUploadForm(TestFile("empty.pdf", "application/pdf", Array.Empty<byte>())));
        Assert.IsType<BadRequestObjectResult>(emptyResult);

        // D3: no file written on the empty guard (the guard fires before PutAsync).
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// D3 / C-M21·7 — <c>POST /documents</c> with an **oversize** payload
    /// (over <see cref="MediaOptions.MaxBytes"/>) → 413, and <em>no file
    /// written</em>: the guard fires before <c>PutAsync</c>. Mirrors
    /// <see cref="AttachmentUploadTests.AttachUpload_F6_Oversize413"/>.
    /// </summary>
    [Fact]
    public async Task Upload_Oversize_413_NoPut()
    {
        var (controller, media, _, _) = BuildUploadController(roles: new[] { "GlobalAdmin" }, maxBytes: 16);

        // A 32-byte PDF payload (over the 16-byte cap):
        var result = await controller.Upload(ValidUploadForm(TestFile("big.pdf", "application/pdf", new byte[32])));

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, status.StatusCode);

        // D3: no file written on the oversize guard.
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// M25 U9 (item 17, F2) — the **oversize** reject on the document lane,
    /// driven through U8's <see cref="Kumunita.Web.Security.IUploadGate"/>. A
    /// small <see cref="CommunityStorageSettings.MaxFileBytes"/> + a large
    /// quota makes the reject purely oversize (not over-quota). The 413 is the
    /// gate's exact <see cref="StatusCodeResult"/> (the regression-suite pin),
    /// and <see cref="IMediaStore.PutAsync"/> is <em>never</em> called
    /// (guards-before-write, C-UP·2 — no byte written, F9). Mirrors the
    /// <see cref="Upload_Oversize_413_NoPut"/> pin but as the M25 named test.
    /// </summary>
    [Fact]
    public async Task DocumentUpload_Oversize_413()
    {
        var (controller, media, _, _) = BuildUploadController(
            roles: new[] { "GlobalAdmin" },
            settings: new Kumunita.Core.Usage.CommunityStorageSettings { MaxFileBytes = 16, PerUserQuotaBytes = long.MaxValue });

        var result = await controller.Upload(ValidUploadForm(TestFile("big.pdf", "application/pdf", new byte[32])));

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, status.StatusCode);
        // C-UP·2 / F9: no byte written — the gate rejects before PutAsync.
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// M25 U9 (item 18, F3) — the **over-quota** reject on the document lane.
    /// Pre-seed the subject's usage (a positive <see cref
    /// "IStorageSettingsService.GetPerUserUsageBytesAsync"/> value) + a small
    /// <see cref="CommunityStorageSettings.PerUserQuotaBytes"/> so
    /// <c>usage + incoming &gt; quota</c>, while <see
    /// cref="CommunityStorageSettings.MaxFileBytes"/> is large so the file is
    /// <em>not</em> oversize — the reject is over-quota, not oversize. 413 +
    /// no byte written (C-UP·2 / F9).
    /// </summary>
    [Fact]
    public async Task DocumentUpload_OverQuota_413()
    {
        var (controller, media, _, _) = BuildUploadController(
            roles: new[] { "GlobalAdmin" },
            settings: new Kumunita.Core.Usage.CommunityStorageSettings { MaxFileBytes = long.MaxValue, PerUserQuotaBytes = 50 },
            currentUsageBytes: 100);

        var result = await controller.Upload(ValidUploadForm(TestFile("bylaws.pdf", "application/pdf", Pdf)));

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, status.StatusCode);
        // C-UP·2 / F9: no byte written — the gate rejects before PutAsync.
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// D3 / C-M21·7 — <c>POST /documents</c> with a **disallowed** content
    /// type → 415, and <em>no file written</em>: the guard fires before
    /// <c>PutAsync</c>. Two asserts (the document allowlist is closed):
    /// <list type="bullet">
    /// <item><b>SVG</b> — <c>image/svg+xml</c> — a scriptable vector type the
    /// document allowlist explicitly refuses (a document is a download, never
    /// an inline render — D6).</item>
    /// <item><b>Video</b> — <c>video/mp4</c> — a type <b>not</b> in the
    /// document allowlist at all; the allowlist is closed, so an
    /// otherwise-harmless media type is still refused.</item>
    /// </list>
    /// Mirrors <see cref="AttachmentUploadTests.AttachUpload_F6_WrongType415"/>.
    /// </summary>
    [Fact]
    public async Task Upload_WrongType_415_NoPut()
    {
        var (controller, media, _, _) = BuildUploadController(roles: new[] { "GlobalAdmin" });

        var svg = await controller.Upload(ValidUploadForm(TestFile("logo.svg", "image/svg+xml", Pdf)));
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeResult>(svg).StatusCode);

        var video = await controller.Upload(ValidUploadForm(TestFile("clip.mp4", "video/mp4", Pdf)));
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeResult>(video).StatusCode);

        // D3: no file written on the disallowed-type guard (either assert).
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// <c>POST /documents</c> with a **malformed audience** (a mode of
    /// <c>null</c> — <see cref="AudienceEditorModel.IsValid"/> false) is a
    /// <b>form error</b> (re-render of the <c>New</c> view with the bound
    /// form + an <c>Audience.Mode</c> error), not a 404 — the M2
    /// mode-required pin, the PostComposeViewModel precedent. The guard
    /// fires before any media write.
    /// </summary>
    [Fact]
    public async Task Upload_MalformedAudience_Renders_ErrorView()
    {
        var (controller, media, _, _) = BuildUploadController(roles: new[] { "GlobalAdmin" });

        var form = ValidUploadForm(TestFile("bylaws.pdf", "application/pdf", Pdf));
        form.Audience = new AudienceEditorModel { Mode = null, Grants = "[]" };
        var result = await controller.Upload(form);

        // A form error re-renders the view (not a 404).
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("New", view.ViewName);
        Assert.Same(form, view.ViewData.Model);
        Assert.NotEmpty(controller.ModelState["Audience.Mode"]!.Errors);
        // No media write (the guard fires before PutAsync).
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// GATE-6 / C-M21·7 / §1.a A2 — <c>POST /documents</c> happy path for a
    /// **GlobalAdmin**: the guards pass, one <see cref="IMediaStore
    /// .PutAsync"/> write (store-first, orphan-safe — D3), then one
    /// <see cref="DocumentService.UploadAsync"/> write in the caller's
    /// session (one <c>Document</c> row, <c>SaveChangesAsync</c>, **no
    /// <c>AccessAudit</c> row** — A2: the write is authenticated, not an
    /// audience-restricted read). The controller redirects to the detail and
    /// sets the flash (the closed <c>documents.flash_uploaded</c> key, U04).
    /// The <see cref="Document"/> is written verbatim (C-M21·1): the
    /// uploader's <c>OwnerId</c> + the form's <c>Audience</c> (the M2
    /// <c>BuildAudience</c> single deserialization site).
    /// </summary>
    [Fact]
    public async Task Upload_Valid_Redirects_OneWrite_NoAuditRow()
    {
        var (controller, media, store, session) = BuildUploadController(roles: new[] { "GlobalAdmin" });

        var stored = new MediaObject
        {
            Id = MediaId, ContentType = "application/pdf",
            SizeBytes = Pdf.Length, CreatedById = Actor,
        };
        media.PutAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(Pdf)), "bylaws.pdf", "application/pdf", Actor,
            Arg.Any<CancellationToken>())
            .Returns(stored);

        var form = ValidUploadForm(TestFile("bylaws.pdf", "application/pdf", Pdf));
        var result = await controller.Upload(form);

        // Redirect to the detail (the flash documents.flash_uploaded is set).
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("/documents/", redirect.Url, StringComparison.Ordinal);
        Assert.Equal("documents.flash_uploaded", controller.TempData["info"]);

        // Exactly one media write (store-first, D3), with the validated args:
        await media.Received(1).PutAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(Pdf)), "bylaws.pdf", "application/pdf", Actor,
            Arg.Any<CancellationToken>());

        // The single Document write (the caller's session — C-M21·4's one
        // SaveChangesAsync), with the verbatim C-M21·1 fields (C-M21·7):
        session.Received(1).Store(Arg.Is<Document>(d =>
            d.MediaId == MediaId &&
            d.OwnerId == Actor &&
            d.ContentType == "application/pdf" &&
            d.Title == "Bylaws" &&
            d.Audience != null));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        // A2: no AccessAudit row (the write lane is authenticated, not a read).
        session.DidNotReceive().Store(Arg.Is<AccessAudit>(_ => true));
    }

    // ══════════════════════════════════════════════════════════════════════
    // Edit (ADR 0125 — the owner-only edit lane) — NSubstitute
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ADR 0125 / D1 / D7 — <c>GET /documents/{id}/edit</c> for a
    /// **non-owner** (including a non-owner GlobalAdmin) returns <b>404</b>
    /// (not 403 — the form's existence is not leaked, the ADR 0122 D7 posture).
    /// The Read path (audience) allows the actor to <em>see</em> the document,
    /// but the owner gate (<c>OwnerId == actorId</c>) refuses the form; the
    /// Core write lane is never reached.
    /// </summary>
    [Fact]
    public async Task EditGet_NonOwner_Returns_404_Not_403()
    {
        const string other = "subj-m21-other";
        var (controller, _, _, _, write, authz) = BuildEditController(subject: other);

        var result = await controller.Edit(DocId);

        Assert.IsType<NotFoundResult>(result); // 404, not 403 (D7 — no existence leak)
        // The Read decision ran (the actor may see it); the owner gate refused.
        await authz.Received(1).CanAsync(
            other, AccessAction.Read, Arg.Any<IAuditableResource>());
        // The Core write lane never loads (the owner gate is before any write).
        write.DidNotReceive().Store(Arg.Is<Document>(_ => true));
    }

    /// <summary>
    /// ADR 0125 / D7 — <c>GET /documents/{id}/edit</c> for a **missing**
    /// document returns 404 (the Read path loads null → the controller 404s
    /// before the owner gate; no decision ran).
    /// </summary>
    [Fact]
    public async Task EditGet_Missing_Returns_404()
    {
        var (controller, _, _, _, _, authz) = BuildEditController(missing: true);

        var result = await controller.Edit(DocId);

        Assert.IsType<NotFoundResult>(result);
        await authz.DidNotReceiveWithAnyArgs().CanAsync(
            Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>());
    }

    /// <summary>
    /// ADR 0125 / D1 / D2 — <c>GET /documents/{id}/edit</c> for the
    /// **owner** returns 200 + the <c>Edit</c> view, with the form pre-filled
    /// from the stored document (the title <c>Bylaws</c>, the audience via
    /// <see cref="AudienceEditorModel.FromAudience"/> — the ADR 0036 single
    /// source). The single <see cref="IAuthorizationService.CanAsync"/> Read
    /// decision ran (one decision row, by the seam).
    /// </summary>
    [Fact]
    public async Task EditGet_Owner_Returns_200_Prefilled()
    {
        var (controller, _, _, _, _, authz) = BuildEditController();

        var result = await controller.Edit(DocId);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<DocumentEditViewModel>(view.ViewData.Model);
        Assert.Equal(DocId, vm.DocumentId);
        Assert.Equal("Bylaws", vm.Title);
        Assert.NotNull(vm.Audience);
        Assert.True(vm.Audience.IsValid, "The stored audience pre-fills a valid editor (ADR 0036).");
        await authz.Received(1).CanAsync(
            Actor, AccessAction.Read, Arg.Any<IAuditableResource>());
    }

    /// <summary>
    /// ADR 0125 / D1 / D7 — <c>POST /documents/{id}/edit</c> for a
    /// **non-owner** returns <b>404</b> (the owner gate is re-checked on the
    /// write, before any guard or write). The <see cref="IMediaStore
    /// .PutAsync"/> is **never** called and the Core write lane never loads.
    /// </summary>
    [Fact]
    public async Task EditPost_NonOwner_Returns_404_Not_403()
    {
        const string other = "subj-m21-other";
        var (controller, media, _, _, write, authz) = BuildEditController(subject: other);

        var form = ValidEditForm(TestFile("revised.pdf", "application/pdf", Pdf));
        var result = await controller.Edit(DocId, form);

        Assert.IsType<NotFoundResult>(result); // 404, not 403 (D7)
        await authz.Received(1).CanAsync(
            other, AccessAction.Read, Arg.Any<IAuditableResource>());
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        write.DidNotReceive().Store(Arg.Is<Document>(_ => true));
    }

    /// <summary>
    /// ADR 0125 / D3 — <c>POST /documents/{id}/edit</c> for the owner with
    /// **no file** (a <c>null</c> <c>IFormFile</c>) is a **no-op on the byte
    /// surface**: the stored blob is kept (the document's
    /// <c>MediaId</c>/<c>ContentType</c> are unchanged), the
    /// <see cref="IMediaStore.PutAsync"/> is **never** called, and the
    /// <see cref="DocumentService.UpdateAsync"/> write stamps <c>Modified</c>
    /// with one <c>Document</c> store + <c>SaveChangesAsync</c> and **no
    /// <c>AccessAudit</c> row** (the write lane is authenticated, not a read —
    /// ADR 0122 §1.a A2). The controller redirects to the detail + sets the
    /// flash (<c>documents.flash_edited</c>).
    /// </summary>
    [Fact]
    public async Task EditPost_Owner_NoFile_KeepsBlob_Redirects_NoPut()
    {
        var (controller, media, _, _, write, _) = BuildEditController();

        var form = ValidEditForm(file: null); // no replacement file
        var result = await controller.Edit(DocId, form);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("/documents/", redirect.Url, StringComparison.Ordinal);
        Assert.Equal("documents.flash_edited", controller.TempData["info"]);

        // D3: the stored blob is kept (no new media write) — the document's
        // MediaId is unchanged in the written row.
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        write.Received(1).Store(Arg.Is<Document>(d => d.MediaId == MediaId && d.OwnerId == Actor));
        await write.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        // A2: no AccessAudit row (the write lane is authenticated, not a read).
        write.DidNotReceive().Store(Arg.Is<AccessAudit>(_ => true));
    }

    /// <summary>
    /// ADR 0125 / D3 — <c>POST /documents/{id}/edit</c> for the owner with a
    /// **valid** replacement file: the guards pass, one
    /// <see cref="IMediaStore.PutAsync"/> write (store-first, orphan-safe —
    /// D3/C-M21·6), then the <see cref="DocumentService.UpdateAsync"/> write
    /// stores the document with the **new** <c>MediaId</c> (the blob reference
    /// is replaced). No <c>AccessAudit</c> row (A2).
    /// </summary>
    [Fact]
    public async Task EditPost_Owner_ValidFile_StoresNewMedia_Redirects()
    {
        var (controller, media, _, _, write, _) = BuildEditController();

        var stored = new MediaObject
        {
            Id = MediaId, ContentType = "application/pdf",
            SizeBytes = Pdf.Length, CreatedById = Actor,
        };
        media.PutAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(Pdf)), "revised.pdf", "application/pdf", Actor,
            Arg.Any<CancellationToken>())
            .Returns(stored);

        var form = ValidEditForm(TestFile("revised.pdf", "application/pdf", Pdf));
        var result = await controller.Edit(DocId, form);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.StartsWith("/documents/", redirect.Url, StringComparison.Ordinal);
        Assert.Equal("documents.flash_edited", controller.TempData["info"]);

        // Exactly one media write (store-first, D3):
        await media.Received(1).PutAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(Pdf)), "revised.pdf", "application/pdf", Actor,
            Arg.Any<CancellationToken>());
        // The document's MediaId is now the replaced reference (D3 — the blob
        // surface changed):
        write.Received(1).Store(Arg.Is<Document>(d => d.MediaId == MediaId && d.OwnerId == Actor));
        await write.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        write.DidNotReceive().Store(Arg.Is<AccessAudit>(_ => true));
    }

    /// <summary>
    /// ADR 0125 / D3 / C-M21·7 — <c>POST /documents/{id}/edit</c> with a
    /// present **oversize** replacement file (over
    /// <see cref="MediaOptions.MaxBytes"/>) → 413, and <em>no file
    /// written</em>: the guard fires before <c>PutAsync</c>. The owner gate
    /// passes first (a non-owner would have 404'd earlier).
    /// </summary>
    [Fact]
    public async Task EditPost_Owner_OversizeFile_413_NoPut()
    {
        var (controller, media, _, _, write, _) = BuildEditController(maxBytes: 16);

        var result = await controller.Edit(DocId, ValidEditForm(TestFile("big.pdf", "application/pdf", new byte[32])));

        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, Assert.IsType<StatusCodeResult>(result).StatusCode);
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        write.DidNotReceive().Store(Arg.Is<Document>(_ => true));
    }

    /// <summary>
    /// ADR 0125 / D3 / C-M21·7 — <c>POST /documents/{id}/edit</c> with a
    /// present **disallowed** replacement file (an <c>image/svg+xml</c> —
    /// outside the closed document allowlist) → 415, and <em>no file
    /// written</em>: the guard fires before <c>PutAsync</c>.
    /// </summary>
    [Fact]
    public async Task EditPost_Owner_WrongTypeFile_415_NoPut()
    {
        var (controller, media, _, _, write, _) = BuildEditController();

        var result = await controller.Edit(DocId, ValidEditForm(TestFile("logo.svg", "image/svg+xml", Pdf)));

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, Assert.IsType<StatusCodeResult>(result).StatusCode);
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        write.DidNotReceive().Store(Arg.Is<Document>(_ => true));
    }

    /// <summary>
    /// ADR 0125 — <c>POST /documents/{id}/edit</c> with a **malformed
    /// audience** (a mode of <c>null</c> — <see
    /// cref="AudienceEditorModel.IsValid"/> false) is a <b>form error</b>
    /// (re-render of the <c>Edit</c> view with the bound form + a
    /// <c>Audience.Mode</c> error), not a 404 — the M2 mode-required pin. The
    /// owner gate passes first; the guard fires before any media write or the
    /// Core write lane.
    /// </summary>
    [Fact]
    public async Task EditPost_Owner_MalformedAudience_Renders_ErrorView()
    {
        var (controller, media, _, _, write, _) = BuildEditController();

        var form = ValidEditForm(file: null);
        form.Audience = new AudienceEditorModel { Mode = null, Grants = "[]" };
        var result = await controller.Edit(DocId, form);

        // A form error re-renders the view (not a 404).
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Edit", view.ViewName);
        Assert.Same(form, view.ViewData.Model);
        Assert.NotEmpty(controller.ModelState["Audience.Mode"]!.Errors);
        // No media write, no Core write (the guard fires before either).
        await media.DidNotReceiveWithAnyArgs().PutAsync(
            Arg.Any<byte[]>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        write.DidNotReceive().Store(Arg.Is<Document>(_ => true));
    }

    // ══════════════════════════════════════════════════════════════════════
    // fixtures
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A well-formed <see cref="DocumentUploadViewModel"/> for the upload
    /// tests: a non-blank title + a valid <see cref="AudienceEditorModel"/>
    /// (Mode "Any", empty grants — <see cref="AudienceEditorModel.IsValid"/>
    /// true, so the audience guard does not fire) + the file under test. The
    /// <c>Audience</c> is written verbatim via <see
    /// cref="AudienceEditorModel.BuildAudience"/> (C-M21·1).
    /// </summary>
    private static DocumentUploadViewModel ValidUploadForm(IFormFile? file) =>
        new()
        {
            Title = "Bylaws",
            Summary = null,
            File = file,
            Audience = new AudienceEditorModel { Mode = "Any", Grants = "[]" },
        };

    /// <summary>
    /// A well-formed <see cref="DocumentEditViewModel"/> for the edit tests:
    /// a non-blank title + a valid <see cref="AudienceEditorModel"/> (Mode
    /// "Any", empty grants — <see cref="AudienceEditorModel.IsValid"/> true) +
    /// the optional replacement file (null = keep the stored blob, ADR 0125 D3).
    /// </summary>
    private static DocumentEditViewModel ValidEditForm(IFormFile? file, string title = "Bylaws") =>
        new()
        {
            DocumentId = DocId,
            Title = title,
            Summary = null,
            File = file,
            Audience = new AudienceEditorModel { Mode = "Any", Grants = "[]" },
        };

    /// <summary>
    /// Builds a <see cref="DocumentController"/> for the **edit** lane (ADR
    /// 0125): the Read path (<c>GetAsync</c> → <c>store.QuerySession()
    /// .LoadAsync&lt;Document&gt;</c>, stubbed present/missing) + one
    /// <see cref="IAuthorizationService.CanAsync"/> decision (Allow) for the
    /// actor's <paramref name="subject"/> (the owner unless overridden) + one
    /// <see cref="IMediaStore.PutAsync"/> (the replacement-file write) + one
    /// <see cref="DocumentService.UpdateAsync"/> write in the caller's
    /// <see cref="IDocumentSession"/> (the real service's
    /// <c>session.LoadAsync</c> + <c>Store</c> + <c>SaveChangesAsync</c> —
    /// stubbed present so the owner gate passes). Pure NSubstitute.
    /// <para>
    /// Returns <c>(controller, media, store, readSession, writeSession,
    /// authz)</c>. The owner gate (the Core <c>UpdateAsync</c>) re-checks
    /// <c>OwnerId == actorId</c>, so the principal's subject must match the
    /// stored <c>OwnerId</c> (= <see cref="Actor"/>) for the happy path — pass
    /// <paramref name="subject"/> = another id to exercise the non-owner 404.
    /// </para>
    /// </summary>
    private static (DocumentController Controller, IMediaStore Media, IDocumentStore Store,
        IQuerySession Read, IDocumentSession Write, IAuthorizationService Authz) BuildEditController(
        bool missing = false, long maxBytes = 0, string subject = Actor)
    {
        var doc = MakeDoc();

        var store = Substitute.For<IDocumentStore>();
        var read = Substitute.For<IQuerySession>();
        read.LoadAsync<Document>(DocId)
            .Returns(Task.FromResult<Document?>(missing ? null : doc));
        store.QuerySession().Returns(read);

        var write = Substitute.For<IDocumentSession>();
        write.LoadAsync<Document>(DocId).Returns(doc); // the owner gate sees the stored row
        store.LightweightSession().Returns(write);

        var authz = Substitute.For<IAuthorizationService>();
        if (!missing)
        {
            authz.CanAsync(subject, AccessAction.Read, Arg.Any<IAuditableResource>())
                .Returns(Task.FromResult(new Decision(true, AccessVia.Owner, subject)));
        }

        var media = Substitute.For<IMediaStore>();
        var httpContext = PrincipalHttpContext(new[] { "GlobalAdmin" }, subject);
        var controller = new DocumentController(
            new DocumentService(Substitute.For<IUserInfoService>(), authz, store),
            media, Options.Create(new MediaOptions { MaxBytes = maxBytes }), store);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        // The happy path sets TempData["info"] (the flash key) — close the bag
        // with a no-op provider (the AdminDateFormatControllerTests idiom).
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        return (controller, media, store, read, write, authz);
    }

    /// <summary>
    /// Boots a real Marten <see cref="IDocumentStore"/> over a fresh scratch
    /// Postgres database (the <see cref="M14InterlockTests.BuildRealStoreAsync"/>
    /// precedent) with the <see cref="DocumentDocTypes"/> schema, so the feed
    /// lane's <c>Query&lt;Document&gt;().ToListAsync()</c> read has its table.
    /// </summary>
    private async Task<IDocumentStore> BuildRealStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            DocumentDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    /// <summary>
    /// Builds a <see cref="DocumentController"/> for the **feed** lane: a
    /// <em>real</em> <see cref="DocumentService"/> (sealed — unproxyable) over
    /// the real scratch-Postgres store (the <c>ListAsync</c>
    /// <c>Query&lt;Document&gt;().ToListAsync()</c> seam — un-NSubstitutable),
    /// NSubstitute for the unused seams, and a principal carrying the
    /// <see cref="ClaimTypes.Subject"/> claim + the given role claims (the
    /// D5 standing).
    /// </summary>
    private async Task<DocumentController> BuildFeedControllerAsync(string[] roles)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var store = await BuildRealStoreAsync();

        var docs = new DocumentService(userInfo, authz, store);
        var controller = new DocumentController(docs, media, Options.Create(new MediaOptions()), store);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = PrincipalHttpContext(roles),
        };
        return controller;
    }

    /// <summary>
    /// Builds a <see cref="DocumentController"/> for the **detail** lane:
    /// <see cref="DocumentService.GetAsync"/> → <c>store.QuerySession()
    /// .LoadAsync&lt;Document&gt;</c> (stubbed present/missing) + one
    /// <see cref="IAuthorizationService.CanAsync"/> decision (stubbed
    /// Allow / Deny). Pure NSubstitute (no Postgres — the read is the
    /// <c>LoadAsync</c> seam, directly stubbable).
    /// </summary>
    private static (DocumentController Controller, IAuthorizationService Authz) BuildDetailController(
        bool allow, bool missing = false)
    {
        var doc = MakeDoc();

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Document>(DocId)
            .Returns(Task.FromResult<Document?>(missing ? null : doc));
        store.QuerySession().Returns(readSession);

        var authz = Substitute.For<IAuthorizationService>();
        if (!missing)
        {
            authz.CanAsync(Actor, AccessAction.Read, Arg.Any<IAuditableResource>())
                .Returns(Task.FromResult(new Decision(allow, AccessVia.Owner, Actor)));
        }

        var docs = new DocumentService(
            Substitute.For<IUserInfoService>(), authz, store);
        var controller = new DocumentController(docs, Substitute.For<IMediaStore>(), Options.Create(new MediaOptions()), store);
        controller.ControllerContext = new ControllerContext { HttpContext = PrincipalHttpContext(new[] { "GlobalAdmin" }) };
        return (controller, authz);
    }

    /// <summary>
    /// Builds a <see cref="DocumentController"/> for the **serve** lane:
    /// <see cref="DocumentService.GetAsync"/> (stubbed Allow / Deny /
    /// missing) + <see cref="IMediaStore.GetAsync"/> (stubbed present /
    /// orphan-miss) + <see cref="IMediaStore.OpenReadAsync"/> (the byte
    /// stream). Pure NSubstitute.
    /// </summary>
    private static (DocumentController Controller, IMediaStore Media, IAuthorizationService Authz) BuildServeController(
        bool allow, bool missing = false, bool orphan = false)
    {
        var doc = MakeDoc();

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Document>(DocId)
            .Returns(Task.FromResult<Document?>(missing ? null : doc));
        store.QuerySession().Returns(readSession);

        var authz = Substitute.For<IAuthorizationService>();
        if (!missing)
        {
            authz.CanAsync(Actor, AccessAction.Read, Arg.Any<IAuditableResource>())
                .Returns(Task.FromResult(new Decision(allow, AccessVia.Owner, Actor)));
        }

        var media = Substitute.For<IMediaStore>();
        if (allow && !missing)
        {
            media.GetAsync(MediaId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<MediaObject?>(orphan ? null : new MediaObject
                {
                    Id = MediaId, ContentType = "application/pdf",
                    Filename = "bylaws.pdf", SizeBytes = Pdf.Length,
                }));
            media.OpenReadAsync(MediaId, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Stream>(new MemoryStream(Pdf, false)));
        }

        var docs = new DocumentService(
            Substitute.For<IUserInfoService>(), authz, store);
        var controller = new DocumentController(docs, media, Options.Create(new MediaOptions()), store);
        controller.ControllerContext = new ControllerContext { HttpContext = PrincipalHttpContext(new[] { "GlobalAdmin" }) };
        return (controller, media, authz);
    }

    /// <summary>
    /// Builds a <see cref="DocumentController"/> for the **upload** lane:
    /// the D5 standing (the given roles) + the D3 guards (<see
    /// cref="MediaOptions.MaxBytes"/>) + one <see cref="IMediaStore
    /// .PutAsync"/> write (stubbed) + one <see cref="DocumentService
    /// .UploadAsync"/> write in the caller's <see cref="IDocumentSession"/>
    /// (the real service's <c>session.Store</c> + <c>SaveChangesAsync</c> —
    /// the substitute session is a no-op). Pure NSubstitute.
    /// </summary>
    private static (DocumentController Controller, IMediaStore Media, IDocumentStore Store, IDocumentSession Session) BuildUploadController(
        string[] roles, long maxBytes = 0,
        Kumunita.Core.Usage.CommunityStorageSettings? settings = null, long currentUsageBytes = 0)
    {
        var mediaOpts = new MediaOptions { MaxBytes = maxBytes };

        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var docs = new DocumentService(
            Substitute.For<IUserInfoService>(), Substitute.For<IAuthorizationService>(), store);
        var media = Substitute.For<IMediaStore>();

        // M25 U9 — the two enforcement tests pass an explicit settings doc +
        // pre-seeded usage; the pre-existing tests keep the U8 default (null →
        // env fallback, 0 → quota disabled), so their assertions are unchanged.
        var httpContext = PrincipalHttpContext(roles, settings: settings, currentUsageBytes: currentUsageBytes);
        var controller = new DocumentController(docs, media, Options.Create(mediaOpts), store);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        // The Upload happy path sets TempData["info"] — close the bag with a
        // no-op provider (the AdminDateFormatControllerTests idiom) so the
        // flash write does not NRE on a missing session store.
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        return (controller, media, store, session);
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect + the flash key, not the bag
        }
    }

    /// <summary>A <see cref="Document"/> for the detail/serve lanes.</summary>
    private static Document MakeDoc() => new()
    {
        Id = DocId,
        Title = "Bylaws",
        Summary = "The community's bylaws",
        MediaId = MediaId,
        Filename = "bylaws.pdf",
        ContentType = "application/pdf",
        SizeBytes = Pdf.Length,
        OwnerId = Actor,
        Audience = new Audience(AudienceMode.Any, Array.Empty<AudienceGrant>()),
        Created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
    };

    /// <summary>
    /// A <see cref="DefaultHttpContext"/> whose principal carries the
    /// <see cref="ClaimTypes.Subject"/> claim <paramref name="actor"/> + the
    /// given <see cref="ClaimTypes.Role"/> claims (the D5 standing the
    /// <see cref="KumunitaPrincipal"/> helpers read).
    /// </summary>
    private static DefaultHttpContext PrincipalHttpContext(string[] roles, string subject = Actor,
        Kumunita.Core.Usage.CommunityStorageSettings? settings = null, long currentUsageBytes = 0)
    {
        var claims = new List<Claim> { new(Kumunita.Core.Identity.ClaimTypes.Subject, subject) };
        claims.AddRange(roles.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r)));
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticationType: "test"));
        // M25 (U8) — the upload + edit lanes adopted the Web-only IUploadGate,
        // which the action resolves from HttpContext.RequestServices. Wire a
        // minimal provider (stub IStorageSettingsService + the real UploadGate)
        // here so every harness in this class — which all funnel through this
        // helper — keeps running against the gate (MaxFileBytes = null → the
        // harness MediaOptions.MaxBytes is the env fallback; PerUserQuotaBytes
        // = 0 → quota disabled, the C-UP·5 sentinel).
        // M25 (U9) — the two new document enforcement tests pass an explicit
        // settings doc (small MaxFileBytes / small PerUserQuotaBytes) + a
        // pre-seeded usage; every pre-existing call keeps the U8 default (null
        // → env fallback, 0 → quota disabled), so their assertions are
        // unchanged.
        var gateSettings = settings
            ?? new Kumunita.Core.Usage.CommunityStorageSettings { MaxFileBytes = null, PerUserQuotaBytes = 0 };
        httpContext.RequestServices = UploadGateTestSupport.ServicesWith(
            gateSettings, currentUsageBytes);
        return httpContext;
    }

    /// <summary>
    /// A minimal <see cref="IFormFile"/> carrier backed by a byte array: the
    /// upload action reads <c>Length</c> / <c>ContentType</c> /
    /// <c>FileName</c> and copies the bytes — no multipart parsing at this
    /// seam. The <see cref="AttachmentUploadTests"/> precedent.
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

        public void CopyTo(Stream target) => target.Write(_content, 0, _content.Length);
        public void CopyTo(Stream target, CancellationToken cancellationToken) => target.Write(_content, 0, _content.Length);
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
