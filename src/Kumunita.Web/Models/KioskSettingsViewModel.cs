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

    /// <summary>
    /// ADR 0302 (IMPROVE-UX R18) — the owner's active (non-revoked) shared
    /// kiosk links, newest first. Each row carries the metadata the
    /// <c>/settings/kiosk</c> shared-link section displays (the short id,
    /// <c>CreatedAt</c>, <c>LastUsedAt</c>) plus the row's
    /// <see cref="KioskLink"/> so the revoke form can post its <c>Id</c>.
    /// Empty when the owner has no links. **Never the raw token** (ADR 0302
    /// D2 — the row holds only the hash; the token was shown once at
    /// creation) and **never the hash** (a read of the row cannot recover the
    /// token, and the section displays no credential material at all).
    /// </summary>
    public IReadOnlyList<KioskLink> SharedLinks { get; set; } = [];

    /// <summary>
    /// ADR 0302 (IMPROVE-UX R18) — the raw token of the link **just created**
    /// (the copy-once reveal, the ADR 0302 D2 "shown exactly once" rule). Set
    /// for the single render that follows <c>POST /settings/kiosk/link</c>
    /// (carried from TempData, then cleared); <c>null</c> on every other render
    /// (there is no "show again" — the row holds only the hash, so a re-read
    /// cannot recover the token). The view renders it in a read-only input for
    /// copy-once; <c>kiosk-settings.ts</c> assembles the
    /// <c>/kiosk?link=&lt;token&gt;</c> URL client-side (the R16 middleware
    /// parses it) so the token never rides a server-rendered URL.
    /// </summary>
    public string? SharedLinkToken { get; set; }
}

/// <summary>
/// One editor row: the stored <see cref="Entry"/> plus the picked item's
/// title as the viewer can see it now (<c>null</c> when the item is gone or
/// no longer visible — the row then says so; for a calendar, <c>null</c>
/// means "All communities").
/// </summary>
public sealed record KioskEntryEditorRow(KioskEntry Entry, string? ItemTitle, bool ItemVisible);
