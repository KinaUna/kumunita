using Marten;

namespace Kumunita.Core.Kiosk;

/// <summary>
/// The read + write seams for the per-resident <see cref="KioskPlaylist"/>
/// (ADR 0164, M35·1 — the 2-method seam; ADR 0164 amendment — one row per
/// signed-in user, not a shared singleton). The
/// <see cref="GetPlaylistAsync"/> read is a **nullable** load (returns
/// <c>null</c> on a missing row — the M35-8 floor renders
/// <c>kiosk.empty</c>), **zero** <c>AccessAudit</c> rows (M35·5, ADR 0164
/// D5). The <see cref="SavePlaylistAsync"/> write is the **per-resident
/// write-lane** (one <c>kiosk.save</c> <c>AccessAudit</c> row,
/// <c>TargetKind = "kiosk"</c>, <c>Via = Owner</c>, keyed on the resident's
/// subject id), **within the caller's session** (in-transaction with any
/// surrounding work — the M3 <c>CreatePostAsync</c> same-transaction
/// precedent), and is an **upsert** (a missing row is created — the
/// register's M35·2 pin; ADR 0170 moved the <c>SiteContentService.SaveAsync</c>
/// template to the same upsert shape, so the two services are now on
/// parity — a save is never a silent no-op).
/// <para>
/// The sign-in gate (any signed-in user edits their own playlist — the ADR
/// 0164 amendment) is enforced by the **Web controller** (<c>[Authorize]</c>,
/// no role restriction), **not** the service (the <c>LocaleSettingsService</c>
/// / per-user preference precedent). The service adds **no** new
/// <c>AccessAction</c> / <c>Decide()</c> branch /
/// <c>IAuditableResource</c> adapter (M35·1 / M35·3).
/// </para>
/// </summary>
public interface IKioskService
{
    /// <summary>
    /// Loads the <see cref="KioskPlaylist"/> row for <paramref name="actorId"/>
    /// (the signed-in resident's own playlist — the ADR 0164 amendment).
    /// Returns the row, or <b><c>null</c></b> when that resident has not
    /// configured one yet (the M35-8 floor: the display renders the
    /// <c>kiosk.empty</c> message — an empty playlist is a legitimate state,
    /// not a degraded one). **Zero** <c>AccessAudit</c> rows (M35·5, ADR 0164
    /// D5 — the read is a config load, not an access decision).
    /// </summary>
    Task<KioskPlaylist?> GetPlaylistAsync(string actorId, CancellationToken ct = default);

    /// <summary>
    /// Saves the <see cref="KioskPlaylist"/> row for <paramref name="actorId"/>
    /// (the acting resident's own playlist) from <paramref name="draft"/>
    /// (ADR 0285 — the entries, default duration, refresh interval, display
    /// mode and scroll speed; all display-only, never an access input, M35·4).
    /// Exactly **one** <c>kiosk.save</c> <c>AccessAudit</c> row
    /// (<c>TargetKind = "kiosk"</c>, <c>Via = Owner</c>, M35·5), **within the
    /// caller's session** (the M3 <c>CreatePostAsync</c> same-transaction
    /// precedent). **Upsert**: a missing row is created (M35·2, ADR 0170
    /// parity). The caller owns the session's lifetime and must dispose it.
    /// </summary>
    Task<KioskPlaylist> SavePlaylistAsync(
        KioskPlaylistDraft draft,
        string actorId,
        IDocumentSession session,
        CancellationToken ct = default);

    /// <summary>
    /// The committed write lane (IMPROVE-CODE C09): saves the resident's
    /// playlist, owning its own session — the <see cref="SavePlaylistAsync
    /// (KioskPlaylistDraft, string, IDocumentSession, CancellationToken)"/>
    /// semantics (one <c>kiosk.save</c> audit row, upsert) opened on a
    /// fresh <c>LightweightSession</c> and committed. Lets the
    /// <c>KioskController</c> drop the <c>IDocumentStore</c> dependency
    /// entirely instead of opening the session itself.
    /// </summary>
    Task<KioskPlaylist> SavePlaylistAsync(
        KioskPlaylistDraft draft,
        string actorId,
        CancellationToken ct = default);
}
