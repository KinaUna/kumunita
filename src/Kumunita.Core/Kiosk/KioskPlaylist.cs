namespace Kumunita.Core.Kiosk;

/// <summary>
/// The kiosk playlist singleton (M35·2 — the **4-member ceiling**, no field
/// outside this set, ADR 0164 D1). A **singleton** (the
/// <c>SiteContent</c> / <c>LocaleSettings</c> shape, ADR 0005 B / ADR 0004
/// §B.1): one row per instance, <see cref="Id"/> fixed to the sentinel
/// <c>"kiosk"</c> (the <see cref="SingletonId"/> constant), so re-resolving
/// it is a plain identity-keyed load.
/// <para>
/// The <see cref="Views"/> is the **ordered** slide list (list position =
/// the slide order, not a stored field — the <see cref="KioskView"/> entries
/// are a value object, not a standalone doc, M35·9). The two interval
/// members are **display-only, never an access input** (M35·4, ADR 0164 D3):
/// they configure the kiosk's auto-cycle / auto-refresh cadence; they are
/// never a DB access column and never an authorization input.
/// </para>
/// <para>
/// The <see cref="KioskPlaylist"/> is a **GlobalAdmin-set** config (the
/// admin-surface write-lane convention — the <c>SiteContentService</c> /
/// <c>LocaleSettingsService</c> precedent): the standing to write it is
/// GlobalAdmin, enforced by the Web gate (<c>[Authorize(Roles =
/// GlobalAdmin)]</c>), not re-checked in the Core service (M35·1 — the
/// <c>KioskService</c> adds **no** new <c>AccessAction</c> /
/// <c>AccessVia</c> / <c>Decide()</c> branch / <c>IAuditableResource</c>
/// adapter). The <see cref="KioskPlaylist"/> read emits **zero**
/// <c>AccessAudit</c> rows (M35·5, ADR 0164 D5); the write commits exactly
/// **one** <c>kiosk.save</c> <c>AccessAudit</c> row (M35·5).
/// </para>
/// </summary>
public sealed class KioskPlaylist
{
    /// <summary>The singleton sentinel id (the <c>SiteContent.SingletonId</c>
    /// / <c>LocaleSettings.SingletonId</c> idiom — one row per instance,
    /// keyed on this id).</summary>
    public const string SingletonId = "kiosk";

    /// <summary>The singleton's identity key (fixed to <see cref="SingletonId"/>).</summary>
    public string Id { get; set; } = SingletonId;

    /// <summary>The **ordered** slide list (list position = the slide order,
    /// not a stored field — the <see cref="KioskView"/> entries are a value
    /// object, not a standalone doc, M35·9). An empty list (the default)
    /// means "no slides configured" — the display renders the
    /// <c>kiosk.empty</c> message (M35-8 FACE).</summary>
    public IReadOnlyList<KioskView> Views { get; set; } = [];

    /// <summary>The slide-advance cadence in seconds (the kiosk's
    /// <c>setInterval</c> view-advance timer). **Display-only, never an
    /// access input** (M35·4, ADR 0164 D3). <c>&gt;= 5</c>; default
    /// <c>30</c>.</summary>
    public int ViewIntervalSeconds { get; set; } = 30;

    /// <summary>The content-refresh cadence in seconds (the kiosk's
    /// <c>setInterval</c> content-refresh timer — **faster** than the view
    /// cadence). **Display-only, never an access input** (M35·4, ADR 0164
    /// D3). <c>&gt;= 2</c> and <c>&lt; ViewIntervalSeconds</c>; default
    /// <c>10</c>.</summary>
    public int RefreshIntervalSeconds { get; set; } = 10;
}
