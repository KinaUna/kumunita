namespace Kumunita.Core.Identity;

/// <summary>
/// C3-03 (ADR 0293) — the **platform-gate** surface (the admin-settled
/// instance gates), extracted out of <see cref="IdentityService"/> as a real
/// part-vs-whole surface (the <see cref="IdentityPlatformGateService"/> class +
/// this interface — the C3-00 <c>EventRsvpGuardianService</c> pattern). The
/// 12 gate methods are a **named domain concept** — the ADR 0050 signup gate,
/// the ADR 0138 / ADR 0194 sample-data password + MFA locks, and the ADR 0077
/// admin-notify gate — each a <see cref="Kumunita.Core.Localization.LocaleSettings"/>
/// singleton read/write (the timezone / date-format singleton-toggle shape) with
/// its own admin audit row (C3 — no silent, unaudited access), and the shared
/// four-conjunct ADR 0138 / ADR 0194 decision (the sample-account closed set +
/// the GlobalAdmin exemption). A C13 cold read of
/// <see cref="IdentityPlatformGateService"/> passes on its own.
/// <para>
/// <see cref="IdentityService"/> keeps thin forwarders for the 12 methods on
/// <see cref="IIdentityService"/> (the C08 test-compat facade + the existing
/// <c>IIdentityService</c> mocks keep compiling unchanged — the C08
/// test-compat facade shape), and the 2 private helpers
/// (<c>IsSampleAccountAsync</c> / <c>IsSampleNonAdminAsync</c>) move with the
/// cluster (they are called only by the 4 lock-decision methods).
/// </para>
/// </summary>
public interface IIdentityPlatformGateService
{
    /// <summary>The ADR 0050 signup gate read (the <c>false</c> floor — a
    /// fresh instance ships invitation-only).</summary>
    Task<bool> IsSignupOpenAsync();

    /// <summary>The ADR 0050 signup gate write (the admin-settled singleton
    /// toggle + the admin audit row).</summary>
    Task SetSignupOpenAsync(bool open, string adminSubjectId);

    /// <summary>The ADR 0138 sample-data closed-set read (the
    /// <c>SampleData__Enabled</c> config flag; <c>false</c> floor).</summary>
    Task<bool> IsSampleDataEnabledAsync();

    /// <summary>The ADR 0138 change-password lock read (the
    /// <c>SamplePasswordChangeLocked</c> gate; the <c>false</c> floor).</summary>
    Task<bool> IsSamplePasswordChangeLockedAsync();

    /// <summary>The ADR 0138 change-password lock write (the admin-settled
    /// singleton toggle + the admin audit row).</summary>
    Task SetSamplePasswordChangeLockedAsync(bool locked, string adminSubjectId);

    /// <summary>The ADR 0138 decision (the four-conjunct rule: closed set +
    /// gate on + sample account + not GlobalAdmin).</summary>
    Task<bool> IsChangePasswordLockedForAsync(string subjectId);

    /// <summary>The ADR 0194 MFA lock read (the <c>SampleMfaLocked</c> gate;
    /// the <c>false</c> floor).</summary>
    Task<bool> IsSampleMfaLockedAsync();

    /// <summary>The ADR 0194 MFA lock write (the admin-settled singleton toggle
    /// + the admin audit row).</summary>
    Task SetSampleMfaLockedAsync(bool locked, string adminSubjectId);

    /// <summary>The ADR 0194 decision (the four-conjunct rule: closed set +
    /// gate on + sample account + not GlobalAdmin).</summary>
    Task<bool> IsMfaLockedForAsync(string subjectId);

    /// <summary>The ADR 0194 login-exemption read (closed set + sample account;
    /// deliberately does not consult the gate or the GlobalAdmin exemption).</summary>
    Task<bool> IsSampleDataAccountAsync(string subjectId);

    /// <summary>The ADR 0077 admin-notify read (the <c>true</c> floor — a fresh
    /// instance ships with the notification on).</summary>
    Task<bool> IsNotifyAdminsOnSignupAsync();

    /// <summary>The ADR 0077 admin-notify write (the admin-settled singleton
    /// toggle + the admin audit row).</summary>
    Task SetNotifyAdminsOnSignupAsync(bool notify, string adminSubjectId);

    // ── Resident-invite mode (ADR 0304, amended 2026-11-11 for IMPROVE-UX A11) ──

    /// <summary>The ADR 0304 (A11) resident-invite mode read (the <c>Off</c>
    /// floor — a fresh instance ships with resident self-invite off; the
    /// opt-in posture a GlobalAdmin sets). A read (no audit row).</summary>
    Task<ResidentInviteSetting> GetResidentInviteSettingAsync();

    /// <summary>The ADR 0304 (A11) resident-invite mode write (the
    /// admin-settled singleton toggle + the admin audit row
    /// <c>signup.set-resident-invite</c>, <c>TargetKind</c> "signup").</summary>
    Task SetResidentInviteSettingAsync(ResidentInviteSetting mode, string adminSubjectId);
}
