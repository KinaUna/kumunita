namespace Kumunita.Web.Models;

using Kumunita.Core.Kiosk;

/// <summary>
/// The <c>/settings/kiosk</c> playlist editor's view model (ADR 0164
/// amendment — per resident; ADR 0285 — any item type, per-entry duration,
/// scroll and comments). Display-only (M35·4).
/// </summary>
public sealed class KioskSettingsViewModel
{
    /// <summary>The stored entries, in play order, each with its item's
    /// current title for the picker.</summary>
    public IReadOnlyList<KioskEntryEditorRow> Entries { get; set; } = [];

    /// <summary>The duration a new entry starts with (seconds, &gt;= 5).</summary>
    public int DefaultDurationSeconds { get; set; } = 30;

    /// <summary>The change-detection cadence (seconds, &gt;= 2).</summary>
    public int RefreshIntervalSeconds { get; set; } = 10;

    public KioskDisplayMode DisplayMode { get; set; } = KioskDisplayMode.FullPage;

    public KioskScrollSpeed ScrollSpeed { get; set; } = KioskScrollSpeed.Medium;
}

/// <summary>
/// One editor row: the stored <see cref="Entry"/> plus the picked item's
/// title as the viewer can see it now (<c>null</c> when the item is gone or
/// no longer visible — the row then says so; for a calendar, <c>null</c>
/// means "All communities").
/// </summary>
public sealed record KioskEntryEditorRow(KioskEntry Entry, string? ItemTitle, bool ItemVisible);
