namespace Kumunita.Core.Notifications;

/// <summary>
/// The recipient's notification preference (ADR 0076 D9 — lean: **one**
/// field). <see cref="KindsEnabled"/> is the subset of the nine
/// <see cref="NotificationKinds"/> constants the resident has **enabled**;
/// <c>null</c> / empty = **all enabled** (the lean-default — the M5
/// <c>KanbanLane.MaxItems?</c> nullable precedent: <c>null</c> means "no
/// limit", here "all on"). The preference governs the **email** nudge,
/// never the inbox row (D7).
/// </summary>
public sealed class NotificationPreference
{
    public string RecipientId { get; set; } = string.Empty;    // the document id — one preference row per recipient (Marten identity)

    public IReadOnlyList<string>? KindsEnabled { get; set; }   // subset of <see cref="NotificationKinds.Known"/>; `null` / empty = all enabled (D9)

    public DateTimeOffset? Updated { get; set; }
}
