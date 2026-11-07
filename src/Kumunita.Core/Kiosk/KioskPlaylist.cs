using Kumunita.Core.Privacy;

namespace Kumunita.Core.Kiosk;

/// <summary>
/// A resident's kiosk playlist (ADR 0164, reworked by ADR 0285). A
/// **per-resident** row (the <c>LocaleSettings</c> / per-user preference
/// shape, ADR 0004 §B.1): one row per signed-in user, <see cref="Id"/> set to
/// the user's <b>subject id</b>, so the <c>/kiosk</c> display resolves the
/// <em>viewer's</em> playlist.
/// <para>
/// <see cref="Entries"/> is the **ordered** entry list (list position = the
/// play order; the <see cref="KioskEntry"/> is a value object, not a
/// standalone doc, M35·9). Every member is **display-only, never an access
/// input** (M35·4, ADR 0164 D3).
/// </para>
/// <para>
/// ADR 0285 replaced the old <c>Views</c> (Board / Calendar / Todos slides)
/// and the playlist-wide <c>ViewIntervalSeconds</c> with
/// <see cref="Entries"/> (any item type, each with its own duration, scroll
/// behaviour and comments toggle), <see cref="DefaultDurationSeconds"/> and
/// <see cref="ScrollSpeed"/>. A row saved before the change loads with an
/// empty <see cref="Entries"/> (the old field is ignored), so the resident
/// sees the <c>kiosk.empty</c> state until they save a new playlist — no
/// migration (there were no live deployments).
/// </para>
/// <para>
/// The write standing is any signed-in user over their own row, enforced by
/// the Web gate (<c>[Authorize]</c>), not re-checked in the Core service
/// (M35·1). The read emits **zero** <c>AccessAudit</c> rows (M35·5); the
/// write commits exactly **one** <c>kiosk.save</c> row (<c>Via = Owner</c>).
/// </para>
/// </summary>
[DataClass(DataClassKind.Operational, Erasure.Delete, ExportScope.Never,
    reason: "a per-resident display preference (Id = the resident's subject id, ADR 0164/0285); the resident's own playlist config; IS22's `ResidentAccountRowsEraser` adds the delete")]
public sealed class KioskPlaylist
{
    /// <summary>The per-resident identity key (the subject id of the user who
    /// configured this playlist).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The **ordered** entries (list position = the play order). An
    /// empty list means "nothing configured" — the display renders the
    /// <c>kiosk.empty</c> message (M35-8).</summary>
    public IReadOnlyList<KioskEntry> Entries { get; set; } = [];

    /// <summary>The duration (seconds, <c>&gt;= 5</c>) the editor gives a new
    /// entry. Each entry then carries its own
    /// <see cref="KioskEntry.DurationSeconds"/>. Default <c>30</c>.</summary>
    public int DefaultDurationSeconds { get; set; } = 30;

    /// <summary>The change-detection cadence in seconds (the kiosk checks the
    /// current slide's content fingerprint on this interval and reloads it
    /// only when it changed). <c>&gt;= 2</c>; default <c>10</c>.</summary>
    public int RefreshIntervalSeconds { get; set; } = 10;

    /// <summary>Full page (the browser viewport) or full screen (the browser's
    /// Fullscreen API) — the ADR 0164 amendment. Default
    /// <see cref="KioskDisplayMode.FullPage"/>.</summary>
    public KioskDisplayMode DisplayMode { get; set; } = KioskDisplayMode.FullPage;

    /// <summary>The auto-scroll speed for every entry set to
    /// <see cref="KioskScrollBehavior.Auto"/> (ADR 0285 D4). Default
    /// <see cref="KioskScrollSpeed.Medium"/>.</summary>
    public KioskScrollSpeed ScrollSpeed { get; set; } = KioskScrollSpeed.Medium;
}
