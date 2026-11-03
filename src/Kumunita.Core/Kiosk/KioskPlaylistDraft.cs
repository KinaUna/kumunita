namespace Kumunita.Core.Kiosk;

/// <summary>
/// The values a resident saves for their kiosk playlist (ADR 0285) — the
/// input of <see cref="IKioskService.SavePlaylistAsync"/>. The Web layer
/// validates the floors (entry duration and default duration <c>&gt;= 5</c>,
/// refresh interval <c>&gt;= 2</c>) before it builds the draft.
/// </summary>
public sealed record KioskPlaylistDraft(
    IReadOnlyList<KioskEntry> Entries,
    int DefaultDurationSeconds,
    int RefreshIntervalSeconds,
    KioskDisplayMode DisplayMode,
    KioskScrollSpeed ScrollSpeed);
