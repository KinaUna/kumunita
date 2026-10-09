namespace Kumunita.Core.AdminOnboarding;

/// <summary>
/// The admin onboarding completion record (ADR 0153): a **singleton** — one
/// row per instance, <see cref="Id"/> fixed to the sentinel <c>singleton</c>
/// (the exact <c>SiteContent</c> / <c>LocaleSettings</c> shape, ADR 0005 B),
/// so re-resolving it is a plain identity-keyed load. Carries exactly one
/// additive member — <see cref="CompletedAt"/>, <c>null</c> = not-yet-guided
/// (the floor, the banner shows), non-null = completed (the banner clears).
/// The one-field set is the **ceiling** (the ADR 0153 D1 pin); a future lane
/// **adds** fields (additive per ADR 0004 §B.1), it does not re-shape the
/// existing one. The <c>SiteContent</c> + <c>LocaleSettings</c> docs are
/// **untouched** — this is a new doc in a new context, not a new field on an
/// existing one (the ADR 0150 D6 / ADR 0006 module-boundary pin).
/// </summary>
public sealed class AdminOnboarding
{
    public const string SingletonId = "singleton";

    public string Id { get; set; } = SingletonId;

    /// <summary>
    /// When a GlobalAdmin completed the guided walk-through; <c>null</c> =
    /// not-yet-guided (the banner shows), non-null = completed (the banner
    /// clears). The floor (M30·2).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Stamps <see cref="CompletedAt"/> to <see cref="DateTimeOffset.UtcNow"/>
    /// (the single-write-lane helper, ADR 0153 D2).
    /// </summary>
    public void Complete() => CompletedAt = DateTimeOffset.UtcNow;
}
