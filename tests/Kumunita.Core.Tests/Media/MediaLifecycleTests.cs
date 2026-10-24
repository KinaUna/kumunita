using System.IO;
using Kumunita.Core;
using Kumunita.Core.Media;
using Marten;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kumunita.Core.Tests.Media;

/// <summary>
/// F15 (UPL-04 byte-store lifecycle/retention) U3 — the single pinned test for
/// the <see cref="MediaLifecycleService.SweepAsync"/> retention sweep
/// (design <c>prod-hardening-f15-lifecycle.md</c> §6).
/// </summary>
/// <para>
/// The sweep deletes <see cref="MediaObject"/> catalog rows older than the
/// 365-day platform cutoff <b>and</b> their content-addressed bytes on the
/// volume (via the idempotent <see cref="IMediaFileStore.DeleteIfExistsAsync"/>
/// seam F1.U2 added, re-used as-is), while keeping not-expired rows + bytes
/// intact, and is a natural no-op on a second run.
/// </para>
/// <para>
/// **Two load-bearing gotchas (mirrored from the F1.U5 harness, NOT optional):**
/// </para>
/// <list type="number">
/// <item><b>MediaObject registration surface.</b> <see cref="MediaObject"/>
/// lives on the <see cref="MediaDocTypes"/> surface (not
/// <see cref="CoreDocTypes"/>/<see cref="PostDocTypes"/>), so the boot store
/// must call <c>MediaDocTypes.Configure(opts)</c> — otherwise the query hits an
/// unmanaged-table error (pin §8.2 GOTCHA-1).</item>
/// <item><b>A real byte store is required.</b> A null/omitted store would make
/// the byte assertions pass vacuously (a false green). We supply a real
/// <see cref="LocalVolumeFileStore"/> against a unique temp dir so the
/// post-commit byte-delete genuinely runs and is asserted via
/// <c>ExistsAsync(id) == false</c> (pin §6 / GOTCHA-2).</item>
/// </list>
public sealed class MediaLifecycleTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Lifecycle_DeletesExpiredBytes()
    {
        var boot = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;

        var expiredId = NewContentId(); // 64 lowercase-hex chars — the byte-store key
        var liveId = NewContentId();

        // Plant the two bytes first (real files on the volume), then the two
        // catalog rows: one expired (366 days — past the 365-day cutoff) and
        // one not-expired control row (keeps-others-intact).
        await boot.Media.PutAsync(expiredId, new byte[] { 1, 2, 3, 4 }, ct);
        await boot.Media.PutAsync(liveId, new byte[] { 9, 8, 7 }, ct);

        await using (var session = boot.Store.LightweightSession())
        {
            session.Store(new MediaObject
            {
                Id = expiredId,
                ContentType = "image/png",
                SizeBytes = 4,
                Created = now.AddDays(-(MediaLifecycleService.RetentionDays + 1)),
                CreatedById = "resident-expired"
            });
            session.Store(new MediaObject
            {
                Id = liveId,
                ContentType = "image/png",
                SizeBytes = 3,
                Created = now,
                CreatedById = "resident-live"
            });
            await session.SaveChangesAsync(ct);
        }

        // Precondition: both bytes are present before the sweep.
        Assert.True(await boot.Media.ExistsAsync(expiredId));
        Assert.True(await boot.Media.ExistsAsync(liveId));

        // Run the sweep once — it should delete exactly the expired row + byte.
        var deleted = await MediaLifecycleService.SweepAsync(boot.Store, boot.Media, now, ct);
        Assert.Equal(1, deleted);

        // (1) the expired MediaObject row is gone (catalog delete).
        Assert.Null(await LoadAsync(boot.Store, expiredId));
        // (2) the expired byte is gone (byte delete via DeleteIfExistsAsync).
        Assert.False(await boot.Media.ExistsAsync(expiredId));

        // (3) no double-run: a second sweep is a no-op (returns 0, no exception)
        //     and the not-expired row + byte are still present (keeps-others-intact).
        var second = await MediaLifecycleService.SweepAsync(boot.Store, boot.Media, now, ct);
        Assert.Equal(0, second);
        Assert.NotNull(await LoadAsync(boot.Store, liveId));
        Assert.True(await boot.Media.ExistsAsync(liveId));
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private sealed class Boot
    {
        public required IDocumentStore Store { get; init; }
        public required IMediaFileStore Media { get; init; }
    }

    private async Task<Boot> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        // GOTCHA-1 (pin §8.2) — MediaObject is registered on the MediaDocTypes
        // surface, so the boot store must configure it.
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            MediaDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // GOTCHA-2 (pin §6) — a real byte store against a unique temp dir, so
        // the post-commit byte-delete genuinely runs and is assertable.
        var mediaRoot = Path.Combine(Path.GetTempPath(), "kumunita-lifecycle-" + Guid.NewGuid().ToString("n"));
        var media = new LocalVolumeFileStore(Options.Create(new MediaOptions { RootPath = mediaRoot }));

        return new Boot { Store = store, Media = media };
    }

    /// <summary>A fresh content id — 64 lowercase-hex chars, the byte-store key.</summary>
    private static string NewContentId()
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task<MediaObject?> LoadAsync(IDocumentStore store, string id)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.QuerySession();
        return await session.LoadAsync<MediaObject>(id, ct);
    }
}
