namespace Kumunita.Core.Pages;

/// <summary>
/// ADR 0128 — the **status** of one seeded page (a member of the
/// four-surface set or the UG guides) as reported by
/// <see cref="IPageService.GetSeededPageStatusAsync"/>: the stored page's
/// identity (slug, id, current title) plus a flag for whether the stored
/// text **differs** from the current code's seeded baseline (the "has a
/// newer shipped text" signal the <c>/admin/help</c> surface shows the
/// GlobalAdmin). <c>PageId</c> is the id of the stored
/// <see cref="Page"/> the per-page reset link (ADR 0058) targets.
/// </summary>
/// <param name="Slug">The page's slug (the registry slug, e.g. "posts").</param>
/// <param name="PageId">The stored <see cref="Page.Id"/> (the reset target).</param>
/// <param name="Title">The stored <see cref="Page.Title"/> (the current title).</param>
/// <param name="HasNewerShippedText">
/// <c>true</c> when the stored text (en body, or any of the de/fr/da
/// <see cref="PageTranslation"/> rows the baseline defines) differs from the
/// code's seeded baseline — i.e. the admin can pull in newer shipped text.
/// <c>false</c> when the stored text already matches the baseline byte-for-byte
/// (a fresh instance, or one whose admin has not customised since the last
/// deploy of the current seed).
/// </param>
public sealed record SeededPageStatus(
    string Slug,
    string PageId,
    string Title,
    bool HasNewerShippedText);
