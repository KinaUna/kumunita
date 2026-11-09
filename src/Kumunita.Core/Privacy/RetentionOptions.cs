namespace Kumunita.Core.Privacy;

/// <summary>
/// Per-instance retention config (IMPROVE-SEC D6, the "Moderate" defaults —
/// the owner's 2026-11-06 decision; E5's finding is that none of these types
/// had a retention policy). A config-bound POCO mirroring
/// <see cref="Kumunita.Core.Media.MediaOptions"/>: <see cref="SectionName"/>
/// <c>"Retention"</c>, keys bound as <c>Retention__*</c>.
/// <para>
/// Every value is a day-count. A value of <c>0</c> (or an absent key) means
/// **no retention for that type** — the fail-safe: the type's rows are never
/// purged (the <see cref="Kumunita.Core.Media.MediaOptions.MaxPlatformBytes"/>
/// <c>0 = keep</c> sentinel). A positive value means "purge rows older than
/// <c>now − days</c>".
/// </para>
/// <para>
/// The <b>decision</b> of *when* a row crosses a cutoff lives pure in
/// <see cref="RetentionPolicy"/> (the C04 / C2-06 decision-extraction shape —
/// <see cref="Kumunita.Core.Usage.UsagePurgePolicy"/> precedent). This type
/// only supplies the days-per-key; it is inert config until IS34a's
/// <c>RetentionService</c> reads it. Nothing reads these values yet in this
/// unit.
/// </para>
/// </summary>
public sealed class RetentionOptions
{
    /// <summary>The config section name — keys bind as <c>Retention__*&lt;Key&gt;</c>.</summary>
    public const string SectionName = "Retention";

    /// <summary>
    /// <c>Retention__Sessions</c> — D6: sessions 30 days after revoke or
    /// expiry. <c>0</c> = keep (never purge).
    /// </summary>
    public int Sessions { get; set; } = 30;

    /// <summary>
    /// <c>Retention__OutboxBodyNullDays</c> — D6: the outbox email body is
    /// nulled 7 days after delivery (the row itself is kept until
    /// <see cref="OutboxDays"/>). <c>0</c> = never null the body.
    /// </summary>
    public int OutboxBodyNullDays { get; set; } = 7;

    /// <summary>
    /// <c>Retention__OutboxDays</c> — D6: the outbox row is kept 90 days after
    /// delivery, then deleted. <c>0</c> = keep.
    /// </summary>
    public int OutboxDays { get; set; } = 90;

    /// <summary>
    /// <c>Retention__DeadLetters</c> — D6: email dead letters 30 days.
    /// <c>0</c> = keep.
    /// </summary>
    public int DeadLetters { get; set; } = 30;

    /// <summary>
    /// <c>Retention__NotificationsRead</c> — D6: read notifications 90 days.
    /// <c>0</c> = keep.
    /// </summary>
    public int NotificationsRead { get; set; } = 90;

    /// <summary>
    /// <c>Retention__NotificationsUnread</c> — D6: unread notifications 365
    /// days. <c>0</c> = keep.
    /// </summary>
    public int NotificationsUnread { get; set; } = 365;

    /// <summary>
    /// <c>Retention__ErrorReports</c> — D6: resolved error reports 180 days.
    /// <c>0</c> = keep.
    /// </summary>
    public int ErrorReports { get; set; } = 180;

    /// <summary>
    /// <c>Retention__Tokens</c> — D6: consumed or expired identity /
    /// escalation tokens 7 days. <c>0</c> = keep.
    /// </summary>
    public int Tokens { get; set; } = 7;

    /// <summary>
    /// Resolve a retention key (the <c>Retention__*</c> surface, e.g.
    /// <c>"Sessions"</c>) to its configured day-count on this instance.
    /// Unknown keys return <c>0</c> (keep — the fail-safe), so a typo or a
    /// type with no key is never purged. Pure over plain values; the single
    /// source of the key → property mapping.
    /// </summary>
    /// <param name="key">The retention key (e.g. <c>"Sessions"</c>).</param>
    /// <returns>The configured day-count; <c>0</c> when the key is unknown or unset.</returns>
    public int DaysFor(string key) => key switch
    {
        "Sessions" => Sessions,
        "OutboxBodyNullDays" => OutboxBodyNullDays,
        "OutboxDays" => OutboxDays,
        "DeadLetters" => DeadLetters,
        "NotificationsRead" => NotificationsRead,
        "NotificationsUnread" => NotificationsUnread,
        "ErrorReports" => ErrorReports,
        "Tokens" => Tokens,
        _ => 0,
    };
}
