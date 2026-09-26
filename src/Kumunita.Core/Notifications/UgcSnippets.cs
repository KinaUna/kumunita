namespace Kumunita.Core.Notifications;

/// <summary>
/// Shared UGC-snippet helpers for the M6 emitters (the F10 "stable,
/// content-derived idempotency key" + the per-recipient localized body
/// composed in <see cref="NotificationService.EmitAsync"/>). The emitters
/// (the group-post / post-reply / announcement / community-post /
/// page-child lanes) all truncate the UGC snippet to a short display
/// value before passing it to <c>EmitAsync</c>; this helper is the single
/// source for that truncation so the emitters stay consistent (a 200-char
/// cap + a single <c>…</c> ellipsis, the codebase's display-snippet
/// idiom).
/// </summary>
public static class UgcSnippets
{
    /// <summary>
    /// Truncate <paramref name="text"/> to at most <paramref name="max"/>
    /// characters (after a <c>Trim</c>), appending a single <c>…</c>
    /// ellipsis when truncation occurred. A null/whitespace input returns
    /// <see cref="string.Empty"/> — the emitter treats an empty snippet as
    /// "no UGC content to display" (the localized template stands alone).
    /// </summary>
    public static string Truncate(string? text, int max = 200)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = text.Trim();
        return text.Length <= max ? text : text[..max] + "…";
    }
}
