namespace Kumunita.Core.Notifications;

/// <summary>
/// Notification delivery options (ADR 0078).
/// <para>
/// <see cref="SuppressForSampleAccountsInProduction"/> — when <c>true</c>, the
/// <see cref="NotificationService.EmitAsync"/> writer is a **no-op** for any
/// recipient whose profile e-mail matches one of the code-owned sample-account
/// addresses (<c>SampleDataSeeder.SampleAccountEmails</c>). The guard is
/// environment-aware: the host binds this property to
/// <c>!IsDevelopment()</c> in <c>Program.cs</c>, so in the <c>Development</c>
/// environment (where Mailpit collects all outbound mail) sample accounts
/// behave exactly like real residents. In a <c>Production</c> or
/// <c>Staging</c> environment (where mail reaches a real SMTP relay) sample
/// accounts are silently skipped — no inbox row is stored, no e-mail is
/// staged.
/// </para>
/// <para>
/// <b>Honor absence (ADR 0056 posture):</b> if the host does not bind this
/// section, the property defaults to <c>false</c> and sample accounts are
/// notified normally — the safe, permissive default for the local dev loop
/// and any test harness that does not register the option.
/// </para>
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// When <c>true</c> (the non-Development production path),
    /// <see cref="NotificationService.EmitAsync"/> is a no-op for any recipient
    /// whose profile e-mail is in <c>SampleDataSeeder.SampleAccountEmails</c>.
    /// Bound by <c>Program.cs</c> to <c>!app.Environment.IsDevelopment()</c>.
    /// </summary>
    public bool SuppressForSampleAccountsInProduction { get; set; }
}
