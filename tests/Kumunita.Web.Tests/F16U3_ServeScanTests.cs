using System.Collections.Generic;
using System.IO;
using Kumunita.Core;
using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Media;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// F16 (UPL-02: scan uploads before serving) — <b>U3</b>, the third and final
/// unit of F16. The single pinned test <c>InfectedUpload_NotServed</c> (design
/// <c>prod-hardening-f16-scan.md</c> §6). <b>The name is pinned — the next
/// audit run diffs against it; do NOT rename it.</b>
/// </summary>
/// <para>
/// F16.U2 landed the seam: <see cref="IVirusScanner"/> (a local-ClamAV
/// <c>bytes in → verdict out</c> contract, HTTP-free / ADR 0006-D, fail-open by
/// construction §2.1) + the <see cref="MediaObject.Quarantined"/> bool + the
/// serve-lane gate in <see cref="AttachmentController.Serve"/>
/// (<c>IsQuarantinedOrInfectedAsync</c>) — a quarantine / infected byte is
/// refused with <b>404</b> (not 403, zero audit rows — the no-leak posture),
/// the flag is persisted (one <c>SaveChangesAsync</c>, C3 single-write-lane),
/// and a re-serve short-circuits <b>before</b> re-scanning. This unit drives
/// that gate with a <b>stub</b> scanner that returns
/// <see cref="VirusScanStatus.Infected"/> and asserts the four §6 legs.
/// </para>
/// <para>
/// <b>Harness (pin §6 + the F16.U2 GOTCHA):</b> one
/// <see cref="AttachmentController"/> per scanner, over a <b>real</b>
/// <see cref="LocalVolumeMediaStore"/> (a temp-dir
/// <see cref="LocalVolumeFileStore"/> + a real <see cref="DocumentStore"/> —
/// NOT a null-skip) so the step-2 <c>GetAsync</c> returns a live
/// <see cref="MediaObject"/> with <c>Quarantined = false</c>. The boot store
/// MUST call <c>MediaDocTypes.Configure(opts)</c> (the §3 GOTCHA —
/// <see cref="MediaObject"/> lives on the <c>MediaDocTypes</c> surface, not
/// <c>CoreDocTypes</c>/<c>PostDocTypes</c>) AND <c>PostDocTypes.Configure</c>
/// (leg-4's real sealed <see cref="PostService"/> reverse-lookup runs over
/// <c>Post</c>/<c>PostReply</c>, which live on <c>PostDocTypes</c>). The
/// <c>Quarantined</c> persist (the gate's <c>SaveChangesAsync</c>) needs the
/// real <c>IDocumentStore</c>, so this is a <see cref="PostgresFixture"/>
/// integration test (the <c>Kumunita.Web.Tests</c> byte-for-byte
/// <see cref="PostgresFixture"/>), not a pure NSubstitute seam test. The
/// <c>IAuthorizationService</c> / <c>IAnnouncementService</c> owner stand-ins
/// are NSubstitute (the quarantine gate fires <b>before</b> they are reached —
/// §4 — so the Infected legs never touch <c>authz</c>; leg-4's Clean leg routes
/// through the announcement branch).
/// </para>
/// <para>
/// <b>The <see cref="VirusScanStatus.Infected"/> path is the pinned contract
/// (legs 1–3). The <see cref="VirusScanStatus.Clean"/> leg (leg-4) is the
/// F15.U3 keeps-clean control (optional — not a pinned leg). The fail-open /
/// unavailable-daemon path (§2.1) is a <b>recorded decision, NOT a pinned
/// leg</b> — a separate optional test for it is a judgment call, not required;
/// this test drives the security-critical <c>Infected</c> path.</b>
/// </para>
public sealed class F16U3_ServeScanTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    /// <summary>
    /// The pinned <c>InfectedUpload_NotServed</c> — all four §6 legs:
    /// <list type="number">
    /// <item><b>leg-1</b> — the serve action returns a <b>404 /
    /// <see cref="NotFoundResult"/></b> (not a 403, not a
    /// <see cref="FileResult"/> — the not-served contract, §4).</item>
    /// <item><b>leg-2</b> — after the serve, a fresh <c>GetAsync</c> read of
    /// the <see cref="MediaObject"/> row shows <c>Quarantined == true</c>
    /// (the §3/§5.4 "persist the flag" step).</item>
    /// <item><b>leg-3</b> — a <b>second</b> serve of the same id returns 404
    /// again <b>without re-invoking the scanner</b> (the §3 idempotent
    /// short-circuit — the recording stub's <c>ScanAsync</c> count is still
    /// exactly 1 after the second serve).</item>
    /// <item><b>leg-4</b> (optional control, F15.U3 keeps-clean) — a
    /// <b>second</b> byte whose stub returns <see
    /// cref="VirusScanStatus.Clean"/> is <b>served</b> (a
    /// <see cref="FileResult"/> / 200-shape) and its <c>Quarantined</c>
    /// stays <c>false</c> (the §5.4 "Clean → fall through unchanged" path).</item>
    /// </list>
    /// </summary>
    [Fact]
    public async Task InfectedUpload_NotServed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = await BootStoreAsync(ct);

        var mediaRoot = Path.Combine(Path.GetTempPath(), "kumunita-f16u3-" + Guid.NewGuid().ToString("n"));
        var files = new LocalVolumeFileStore(Options.Create(new MediaOptions { RootPath = mediaRoot }));
        var media = new LocalVolumeMediaStore(files, store);
        var mediaOpts = Options.Create(new MediaOptions { RootPath = mediaRoot });
        // NSub owner stand-ins — never invoked on the Infected path (the gate
        // fires before the step-3 reverse-lookup, §4); leg-4 uses the canned
        // announcement owner. PostService is sealed → must be real (NSub the
        // IUserInfoService / IAuthorizationService ctor deps it composes).
        var posts = new PostService(
            Substitute.For<IUserInfoService>(),
            Substitute.For<IAuthorizationService>(),
            store);
        var announcements = Substitute.For<IAnnouncementService>();

        try
        {
            // ── Plant: an infected byte (the quarantine target) + a clean control byte ──
            // PutAsync is the store-first, doc-second write (C-MED·7): a real byte
            // on the volume + a real MediaObject catalog row (Quarantined defaults
            // false — the not-yet-flagged shape, pin §6 plant).
            var infected = await media.PutAsync(new byte[] { 1, 2, 3 }, "malware.bin",
                "application/octet-stream", "resident", ct);
            var clean = await media.PutAsync(new byte[] { 9, 8, 7 }, "clean.bin",
                "image/png", "resident", ct);
            var infectedId = infected.Id;   // 64 lowercase-hex (content hash) — IsValidMediaId-valid
            var cleanId = clean.Id;

            // Precondition (pin §6 plant): both rows start unquarantined.
            Assert.False((await media.GetAsync(infectedId, ct))!.Quarantined);
            Assert.False((await media.GetAsync(cleanId, ct))!.Quarantined);

            // ── Legs 1–3 (the pinned contract): a stub scanner returns Infected ──
            var infectedScanner = new InfectedScanner();
            var controller = new AttachmentController(
                media, mediaOpts,
                Substitute.For<IAuthorizationService>(),
                posts, announcements, store,
                infectedScanner);   // ← NON-null (a null scanner is the §2.1 fail-open floor — it would not drive Infected)
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            // leg-1 — the serve lane 404s (the Infected verdict → return NotFound()).
            var first = await controller.Serve(infectedId);
            Assert.IsType<NotFoundResult>(first);            // a 404 (not a 403, not served)
            Assert.False(first is FileResult);               // explicitly NOT a FileResult (the not-served contract, §4)

            // leg-2 — the quarantine flag is persisted (a fresh read shows true).
            Assert.True((await media.GetAsync(infectedId, ct))!.Quarantined);

            // leg-3 — idempotent: a second serve 404s WITHOUT re-invoking the
            // scanner (the §3 short-circuit `if (stored.Quarantined) return true;`).
            var second = await controller.Serve(infectedId);
            Assert.IsType<NotFoundResult>(second);
            Assert.Equal(1, infectedScanner.ScanCount);   // still exactly 1 — no re-scan

            // ── leg-4 (optional control, F15.U3 keeps-clean): a Clean byte is served ──
            // The Clean verdict falls through to the step-3 reverse-lookup; route
            // it to the announcement branch (a canned owner) so ServeFile produces
            // a FileResult — the "Clean → fall through unchanged" path (§5.4).
            var ann = new Announcement
            {
                Id = cleanId,                  // the clean byte's owner (public-scope, visible)
                Scope = AnnouncementScope.Public,
                Title = "clean", Body = "clean body", AuthorId = "resident",
            };
            announcements.FindByAttachmentIdAsync(cleanId).Returns(ann);
            announcements.GetAsync(cleanId, (string?)null, Arg.Any<IReadOnlySet<string>>()).Returns(ann);

            var cleanController = new AttachmentController(
                media, mediaOpts,
                Substitute.For<IAuthorizationService>(),
                posts, announcements, store,
                new CleanScanner());
            cleanController.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            var served = await cleanController.Serve(cleanId);
            Assert.True(served is FileResult);                              // leg-4 — served (a 200-shape FileResult; a FileStreamResult subclass)
            Assert.False((await media.GetAsync(cleanId, ct))!.Quarantined); // and stays unquarantined
        }
        finally
        {
            try { Directory.Delete(mediaRoot, recursive: true); } catch { /* best-effort temp cleanup */ }
        }
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Boot a real <see cref="DocumentStore"/> over a fresh scratch database
    /// (the <see cref="PostgresFixture"/> seam, the F11U3 / F15U3 / F1U5
    /// <c>BootStoreAsync</c> shape) with the <c>mt</c> schema + BOTH the
    /// <see cref="MediaDocTypes"/> surface (the §3 GOTCHA — <see cref="MediaObject"/>
    /// lives here, NOT <c>CoreDocTypes</c>/<c>PostDocTypes</c>) AND the
    /// <see cref="PostDocTypes"/> surface (leg-4's real <see cref="PostService"/>
    /// reverse-lookup over <c>Post</c>/<c>PostReply</c>). Instance method so it
    /// can read the primary-constructor <see cref="PostgresFixture"/> fixture
    /// (the CS9105 a static method hits).
    /// </summary>
    private async Task<DocumentStore> BootStoreAsync(CancellationToken ct)
    {
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            MediaDocTypes.Configure(opts);   // §3 GOTCHA — MediaObject is on THIS surface
            PostDocTypes.Configure(opts);    // leg-4 — the real PostService reverse-lookup (Post / PostReply)
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    // ── The stub scanners (pin §6) ──────────────────────────────────────────

    /// <summary>
    /// pin §6 — a call-counting <see cref="IVirusScanner"/> that always returns
    /// <see cref="VirusScanStatus.Infected"/> (the stub: "always Infected").
    /// <see cref="ScanCount"/> records the number of <c>ScanAsync</c> invocations
    /// so leg-3 can assert the idempotent short-circuit (count still 1 after the
    /// second serve — the <c>if (stored.Quarantined) return true;</c> branch, §3).
    /// </summary>
    private sealed class InfectedScanner : IVirusScanner
    {
        public int ScanCount { get; private set; }

        public Task<VirusScanStatus> ScanAsync(Stream content, CancellationToken ct = default)
        {
            ScanCount++;
            return Task.FromResult(VirusScanStatus.Infected);
        }
    }

    /// <summary>
    /// pin §6 leg-4 (optional control) — a <see cref="IVirusScanner"/> that
    /// always returns <see cref="VirusScanStatus.Clean"/> (the "CleanScanner"
    /// the pin names): the serve proceeds unchanged (the §5.4 fall-through).
    /// </summary>
    private sealed class CleanScanner : IVirusScanner
    {
        public Task<VirusScanStatus> ScanAsync(Stream content, CancellationToken ct = default)
            => Task.FromResult(VirusScanStatus.Clean);
    }
}
