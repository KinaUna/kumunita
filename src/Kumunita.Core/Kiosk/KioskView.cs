namespace Kumunita.Core.Kiosk;

/// <summary>
/// One entry in the ordered kiosk playlist (M35·2 — the **5-member ceiling**,
/// no field outside this set, ADR 0164 D1). A **value object** nested in the
/// <see cref="KioskPlaylist"/> singleton (not a standalone Marten doc — the
/// <see cref="KioskPlaylist"/> is the only registered doc type, M35·1 / M35·9).
/// <para>
/// The optional members are **kind-conditional** (a <c>null</c> means
/// "not applicable" for the slide's kind): <see cref="BoardId"/> is set
/// **only when** <see cref="Kind"/> is <see cref="KioskViewKind.Board"/>;
/// <see cref="CalendarView"/> is set **only when** <see cref="Kind"/> is
/// <see cref="KioskViewKind.Calendar"/>; <see cref="ComponentId"/> is set
/// **only when** <see cref="Kind"/> is <see cref="KioskViewKind.Todos"/>.
/// The <see cref="Label"/> is an optional per-slide display label;
/// <c>null</c> means the surface's own header is shown.
/// </para>
/// </summary>
public sealed class KioskView
{
    /// <summary>Which surface this slide shows (the closed 3-member set,
    /// <see cref="KioskViewKind"/>).</summary>
    public KioskViewKind Kind { get; set; }

    /// <summary>The board id — **only when** <see cref="Kind"/> is
    /// <see cref="KioskViewKind.Board"/>.</summary>
    public string? BoardId { get; set; }

    /// <summary><c>"day"</c> | <c>"week"</c> | <c>"month"</c> — **only when**
    /// <see cref="Kind"/> is <see cref="KioskViewKind.Calendar"/>; default
    /// <c>"week"</c>.</summary>
    public string? CalendarView { get; set; } = "week";

    /// <summary>The community filter — **only when** <see cref="Kind"/> is
    /// <see cref="KioskViewKind.Todos"/>; <c>null</c> = all the viewer's
    /// visible todos.</summary>
    public string? ComponentId { get; set; }

    /// <summary>An optional per-slide display label; <c>null</c> = the
    /// surface's own header.</summary>
    public string? Label { get; set; }
}
