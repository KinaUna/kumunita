using Marten;

namespace Kumunita.Core.Kiosk;

/// <summary>
/// The read + write seams for the <see cref="KioskPlaylist"/> singleton
/// (ADR 0164, M35·1 — the 2-method seam). The
/// <see cref="GetPlaylistAsync"/> read is a **nullable** load (returns
/// <c>null</c> on a missing row — the M35-8 floor renders
/// <c>kiosk.empty</c>), **zero** <c>AccessAudit</c> rows (M35·5, ADR 0164
/// D5). The <see cref="SavePlaylistAsync"/> write is the **GlobalAdmin
/// write-lane** (one <c>kiosk.save</c> <c>AccessAudit</c> row,
/// <c>TargetKind = "kiosk"</c>, <c>Via = Admin</c>), **within the caller's
/// session** (in-transaction with any surrounding work — the M3
/// <c>CreatePostAsync</c> same-transaction precedent), and is an **upsert**
/// (a missing row is created — the register's M35·2 pin, the one intentional
/// departure from the <c>SiteContentService.SaveAsync</c> no-op-on-missing
/// template).
/// <para>
/// The actor-standing gate (GlobalAdmin) is enforced by the **Web
/// controller** (<c>[Authorize(Roles = GlobalAdmin)]</c>), **not** the
/// service (the admin-surface precedent — the <c>SiteContentService</c> /
/// <c>SurfaceLabelsService</c> shape). The service adds **no** new
/// <c>AccessAction</c> / <c>AccessVia</c> / <c>Decide()</c> branch /
/// <c>IAuditableResource</c> adapter (M35·1 / M35·3).
/// </para>
/// </summary>
public interface IKioskService
{
    /// <summary>
    /// Loads the <see cref="KioskPlaylist"/> singleton. Returns the row, or
    /// <b><c>null</c></b> when no row is configured (the M35-8 floor: the
    /// display renders the <c>kiosk.empty</c> message — an empty playlist is
    /// a legitimate state, not a degraded one). **Zero** <c>AccessAudit</c>
    /// rows (M35·5, ADR 0164 D5 — the read is a config load, not an access
    /// decision).
    /// </summary>
    Task<KioskPlaylist?> GetPlaylistAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the <see cref="KioskPlaylist"/> singleton — the GlobalAdmin
    /// write-lane (M35·5 — exactly **one** <c>kiosk.save</c>
    /// <c>AccessAudit</c> row, <c>TargetKind = "kiosk"</c>,
    /// <c>Via = Admin</c>). **Within the caller's session** (in-transaction
    /// with any surrounding work — the M3 <c>CreatePostAsync</c>
    /// same-transaction precedent). **Upsert**: a missing row is created
    /// (the register's M35·2 pin — the one intentional departure from the
    /// <c>SiteContentService.SaveAsync</c> no-op-on-missing template; a
    /// GlobalAdmin can configure the playlist on a fresh instance without a
    /// seeder). The caller owns the session's lifetime and must dispose it.
    /// </summary>
    Task<KioskPlaylist> SavePlaylistAsync(
        IReadOnlyList<KioskView> views,
        int viewIntervalSeconds,
        int refreshIntervalSeconds,
        string actorId,
        IDocumentSession session,
        CancellationToken ct = default);
}
