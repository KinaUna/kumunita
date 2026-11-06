using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Identity;

/// <summary>
/// C3-03 (ADR 0293) — the **platform-gate** surface (the admin-settled
/// instance gates), extracted out of <see cref="IdentityService"/> as a real
/// part-vs-whole surface (the C3-00 <c>EventRsvpGuardianService</c> pattern).
/// The 12 gate methods are a **named domain concept** — the ADR 0050 signup
/// gate, the ADR 0138 / ADR 0194 sample-data password + MFA locks, and the
/// ADR 0077 admin-notify gate — each a <see cref="Kumunita.Core.Localization.LocaleSettings"/>
/// singleton read/write (the timezone / date-format singleton-toggle shape) with
/// its own admin audit row (C3 — no silent, unaudited access), and the shared
/// four-conjunct ADR 0138 / ADR 0194 decision (the sample-account closed set via
/// <see cref="Kumunita.Core.Bootstrap.SampleDataSeeder.SampleAccountEmails"/> +
/// the GlobalAdmin exemption). A C13 cold read of this type passes on its own.
/// <para>
/// The 2 private helpers (<c>IsSampleAccountAsync</c> /
/// <c>IsSampleNonAdminAsync</c>) move with the cluster (they are called only by
/// the 4 lock-decision methods). <see cref="IdentityService"/> keeps thin
/// forwarders for the 12 methods on <see cref="IIdentityService"/> (the C08
/// test-compat facade shape — the existing <c>IIdentityService</c> mocks keep
/// compiling unchanged).
/// </para>
/// </summary>
public sealed class IdentityPlatformGateService : IIdentityPlatformGateService
{
    private readonly Marten.IDocumentStore documentStore;
    private readonly IOptions<SampleDataOptions>? sampleDataOptions;
    private readonly IUserInfoService userInfo;
    private readonly UserManager<User> userManager;

    public IdentityPlatformGateService(
        Marten.IDocumentStore documentStore,
        IOptions<SampleDataOptions>? sampleDataOptions,
        IUserInfoService userInfo,
        UserManager<User> userManager)
    {
        this.documentStore = documentStore;
        this.sampleDataOptions = sampleDataOptions;
        this.userInfo = userInfo;
        this.userManager = userManager;
    }

    // ── Signup policy (ADR 0050 — the admin-managed open / invitation-only gate) ──

    /// <inheritdoc />
    public async Task<bool> IsSignupOpenAsync()
    {
        // ADR 0050 read seam (amended 2026-10-05): the instance gate
        // (LocaleSettings.IsSignupOpen) with the `false` floor — a missing
        // singleton or an unset value both yield `false`, so a fresh instance
        // ships **invitation-only** (an admin sets up the platform before any
        // resident can self-register, then opens the gate explicitly). A read (no
        // audit row), the same shape as the LocaleSettings reads the Localization
        // lane does.
        using var session = documentStore.QuerySession();
        var settings = await session.LoadAsync<Localization.LocaleSettings>(
            Localization.LocaleSettings.SingletonId, CancellationToken.None);

        // `false` floor: a null settings row (never seen — but defensively) keeps
        // the gate closed; only an explicit `true` opens sign-up. The admin is
        // created by the seeder (not this lane) and first-boot completion is a
        // separate check, so the closed floor cannot lock the platform's own
        // accounts out.
        return settings is not null && settings.IsSignupOpen;
    }

    /// <inheritdoc />
    public async Task SetSignupOpenAsync(bool open, string adminSubjectId)
    {
        // ADR 0050 write seam: the admin-settled instance gate (LocaleSettings singleton,
        // the same doc the timezone / date-format / editor lanes read and write) + exactly
        // one audit row (via: Admin, action "signup.set-open", target "signup") in the
        // same session (C3 — no silent, unaudited access). The gate is the whole point
        // here — it is *not* an access change, so there is no VisibilityCount to attach
        // (VisibleCount / HiddenCount stay null, the single-target shape ADR 0006 §B
        // prescribes for a singleton toggle). `open` is the authoritative new value.
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Load-or-create the singleton (the SetDefaultTimezoneAsync shape) and set
        // the gate; the other singleton fields are untouched — this is the signup
        // gate only.
        var settings = await session
            .LoadAsync<Localization.LocaleSettings>(
                Localization.LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is null)
        {
            settings = new Localization.LocaleSettings { IsSignupOpen = open };
        }
        else
        {
            settings.IsSignupOpen = open;
        }

        session.Store(settings);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = adminSubjectId,
            EffectivePrincipalId = adminSubjectId,
            Action = "signup.set-open",
            TargetKind = "signup",
            TargetId = "signup",
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── Sample-data change-password lock (ADR 0138) ────────────────────────

    /// <inheritdoc />
    public Task<bool> IsSampleDataEnabledAsync()
    {
        // ADR 0138 read seam: the SampleData__Enabled flag (config, the seeder's
        // own gate — ADR 0056). A missing reader (the test-construction floor)
        // or an unset flag both yield `false`: the sample-data surfaces are
        // unreachable by construction on a real instance (the "unreachable by
        // construction" shape). A pure config read — no audit row, no DB.
        return Task.FromResult(sampleDataOptions?.Value.Enabled ?? false);
    }

    /// <inheritdoc />
    public async Task<bool> IsSamplePasswordChangeLockedAsync()
    {
        // ADR 0138 read seam: the instance gate (LocaleSettings.
        // SamplePasswordChangeLocked) with the `false` floor — a missing
        // singleton or an unset value both yield `false`, so a fresh or real
        // instance never blocks a password change (the deliberate inverse of
        // IsSignupOpenAsync's `true` floor; the MessagingEnabled shape). A read
        // (no audit row); the SampleData__Enabled flag is NOT consulted here —
        // the lock is a pure instance value, and IsChangePasswordLockedForAsync
        // combines it with the sample-account membership.
        using var session = documentStore.QuerySession();
        var settings = await session.LoadAsync<Localization.LocaleSettings>(
            Localization.LocaleSettings.SingletonId, CancellationToken.None);

        // `false` floor: a null settings row (never seen — but defensively)
        // keeps the change open; only an explicit `true` locks sample accounts.
        // `false` floor: a null settings row keeps the change open; only an
        // explicit `true` locks sample accounts.
        return settings is not null && settings.SamplePasswordChangeLocked;
    }

    /// <inheritdoc />
    public async Task SetSamplePasswordChangeLockedAsync(bool locked, string adminSubjectId)
    {
        // ADR 0138 write seam: the admin-settled instance gate (LocaleSettings
        // singleton, the same doc the signup / notify / timezone / date-format
        // lanes read and write) + exactly one audit row (via: Admin, action
        // "sample.set-password-lock", target "sample") in the same session (C3
        // — no silent, unaudited access). The same single-target
        // singleton-toggle shape as SetSignupOpenAsync (the timezone.set-default
        // / dateformat.set-default precedent). `locked` is the authoritative new
        // value.
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Load-or-create the singleton (the SetSignupOpenAsync shape) and set the
        // gate; the other singleton fields are untouched — this is the sample
        // password lock only.
        var settings = await session
            .LoadAsync<Localization.LocaleSettings>(
                Localization.LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is null)
        {
            settings = new Localization.LocaleSettings { SamplePasswordChangeLocked = locked };
        }
        else
        {
            settings.SamplePasswordChangeLocked = locked;
        }

        session.Store(settings);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = adminSubjectId,
            EffectivePrincipalId = adminSubjectId,
            Action = "sample.set-password-lock",
            TargetKind = "sample",
            TargetId = "sample",
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsChangePasswordLockedForAsync(string subjectId)
    {
        // ADR 0138 decision seam: the single place the "is this account locked
        // out of changing its own password?" rule lives, so the resident's
        // self-serve change-password surface (now on the Security tab, ADR
        // 0280) and the ChangePasswordAsync enforcement guard below agree. Four conjuncts (1)+(2) are this method's, (3)+(4)
        // are shared with the ADR 0194 MFA lock via IsSampleNonAdminAsync
        // (the two locks cannot drift):
        //   (1) the instance carries the closed set (SampleData__Enabled);
        //   (2) the admin opted the instance in (the locale gate);
        //   (3) the subject is a sample account (closed set, by profile e-mail);
        //   (4) the subject is NOT a GlobalAdmin (the sample admin is exempt).
        if (!await IsSampleDataEnabledAsync())
            return false;
        if (!await IsSamplePasswordChangeLockedAsync())
            return false;
        return await IsSampleNonAdminAsync(subjectId);
    }

    /// <summary>
    /// Conjunct (3) of the ADR 0138 / 0194 decision: is the subject a sample
    /// account (a member of the closed
    /// <see cref="Bootstrap.SampleDataSeeder.SampleAccountEmails"/> set, by its
    /// profile e-mail)? The ADR 0078 / ADR 0129 single source of truth the
    /// notification-suppression gate compares against — the same set, so the
    /// locks and the suppression cannot drift. A read (no audit row).
    /// </summary>
    private async Task<bool> IsSampleAccountAsync(string subjectId)
    {
        var profile = await userInfo.GetProfileAsync(subjectId);
        var email = profile?.Email;
        if (string.IsNullOrWhiteSpace(email))
            return false;
        return Kumunita.Core.Bootstrap.SampleDataSeeder
            .SampleAccountEmails.Contains(email.Trim());
    }

    /// <summary>
    /// Conjuncts (3)+(4) of the ADR 0138 / 0194 decision: is the subject a
    /// sample account (conjunct 3) <b>AND NOT</b> a
    /// <see cref="Roles.GlobalAdmin"/> (conjunct 4 — the sample admin is
    /// exempt, keeping its own password + MFA lane by construction)? The
    /// shared rule both locks combine with their own admin-toggle gate, so
    /// the ADR 0138 password lock and the ADR 0194 MFA lock cannot drift.
    /// A read (no audit row).
    /// </summary>
    private async Task<bool> IsSampleNonAdminAsync(string subjectId)
    {
        if (!await IsSampleAccountAsync(subjectId))
            return false;
        var user = await userManager.FindByIdAsync(subjectId);
        if (user is null)
            return false;   // no account → not a lockable sample account
        var roles = await userManager.GetRolesAsync(user);
        return !roles.Contains(Roles.GlobalAdmin);
    }

    // ── Sample-data MFA lock (ADR 0194 — amends ADR 0138) ────────────────
    // The MFA (two-factor) settings surface is the MFA analog of the
    // change-password surface. Same four-conjunct decision as ADR 0138
    // (conjugates (1)+(3)+(4) shared via IsSampleNonAdminAsync /
    // IsSampleAccountAsync; (2) is this lane's own admin-toggle gate,
    // LocaleSettings.SampleMfaLocked), plus the IsSampleDataAccountAsync
    // read the login path (leg 1.5b) uses to exempt the sample admin from
    // the forced-MFA enrollment redirect.

    /// <inheritdoc />
    public async Task<bool> IsSampleMfaLockedAsync()
    {
        // ADR 0194 read seam: the instance gate (LocaleSettings.SampleMfaLocked)
        // with the `false` floor — a missing singleton or an unset value both
        // yield `false`, so a fresh or real instance never blocks MFA (the exact
        // IsSamplePasswordChangeLockedAsync shape, re-applied to the MFA
        // surface). A read (no audit row); the SampleData__Enabled flag is NOT
        // consulted here — the lock is a pure instance value, and
        // IsMfaLockedForAsync combines it with the sample-account membership.
        using var session = documentStore.QuerySession();
        var settings = await session.LoadAsync<Localization.LocaleSettings>(
            Localization.LocaleSettings.SingletonId, CancellationToken.None);

        // `false` floor: a null settings row keeps MFA open; only an explicit
        // `true` locks sample accounts out of the MFA surface.
        return settings is not null && settings.SampleMfaLocked;
    }

    /// <inheritdoc />
    public async Task SetSampleMfaLockedAsync(bool locked, string adminSubjectId)
    {
        // ADR 0194 write seam: the admin-settled instance gate (LocaleSettings
        // singleton, the same doc the password lock reads and writes) + exactly
        // one audit row (via: Admin, action "sample.set-mfa-lock", target
        // "sample") in the same session (C3 — no silent, unaudited access).
        // The same single-target singleton-toggle shape as
        // SetSamplePasswordChangeLockedAsync.
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Load-or-create the singleton and set the gate; the other singleton
        // fields (including SamplePasswordChangeLocked) are untouched.
        var settings = await session
            .LoadAsync<Localization.LocaleSettings>(
                Localization.LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is null)
        {
            settings = new Localization.LocaleSettings { SampleMfaLocked = locked };
        }
        else
        {
            settings.SampleMfaLocked = locked;
        }

        session.Store(settings);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = adminSubjectId,
            EffectivePrincipalId = adminSubjectId,
            Action = "sample.set-mfa-lock",
            TargetKind = "sample",
            TargetId = "sample",
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsMfaLockedForAsync(string subjectId)
    {
        // ADR 0194 decision seam: the single place the "is this account locked
        // out of the MFA settings surface?" rule lives, so the resident
        // /settings/security/mfa surface and the MfaEnable / MfaRotateRecovery
        // / MfaDisable enforcement guards agree. The same four conjuncts as
        // IsChangePasswordLockedForAsync (conjugates (1)+(3)+(4) shared via
        // IsSampleNonAdminAsync; (2) is this lane's own admin-toggle gate):
        //   (1) the instance carries the closed set (SampleData__Enabled);
        //   (2) the admin opted the instance in (the MFA locale gate);
        //   (3) the subject is a sample account (closed set, by profile e-mail);
        //   (4) the subject is NOT a GlobalAdmin (the sample admin is exempt).
        if (!await IsSampleDataEnabledAsync())
            return false;
        if (!await IsSampleMfaLockedAsync())
            return false;
        return await IsSampleNonAdminAsync(subjectId);
    }

    /// <inheritdoc />
    public async Task<bool> IsSampleDataAccountAsync(string subjectId)
    {
        // ADR 0194 login-exemption seam: conjunct (1) + (3) only — the instance
        // carries the closed set AND the subject is a member of it (by profile
        // e-mail). Deliberately does NOT consult the admin-toggle gate (a
        // locked instance still lets its admin sign in — that is the point of
        // the exemption) and does NOT apply the GlobalAdmin exemption (the
        // exemption the login path needs is *precisely for* the sample
        // GlobalAdmin — a non-admin sample account never reaches leg 1.5b,
        // since that leg is GlobalAdmin-only). A read (no audit row); the
        // SampleData__Enabled conjunct makes this unreachable by construction
        // on a real instance (the ADR 0056 shape).
        if (!await IsSampleDataEnabledAsync())
            return false;
        return await IsSampleAccountAsync(subjectId);
    }

    // ── Admin account notifications (ADR 0077 — the account.signup /
    //    account.verified notify gate + the GlobalAdmin emitters) ──

    /// <inheritdoc />
    public async Task<bool> IsNotifyAdminsOnSignupAsync()
    {
        // ADR 0077 read seam: the instance gate (LocaleSettings.NotifyAdminsOnSignup)
        // with the `true` floor — a missing singleton or an unset value both yield
        // `true`, so a fresh instance ships with the admin notification on (the M6
        // "null / empty = all enabled" lean-default, the same shape as
        // IsSignupOpenAsync's `true` floor). A read (no audit row).
        using var session = documentStore.QuerySession();
        var settings = await session.LoadAsync<Localization.LocaleSettings>(
            Localization.LocaleSettings.SingletonId, CancellationToken.None);

        return settings is null || settings.NotifyAdminsOnSignup;
    }

    /// <inheritdoc />
    public async Task SetNotifyAdminsOnSignupAsync(bool notify, string adminSubjectId)
    {
        // ADR 0077 write seam: the admin-settled instance gate (LocaleSettings singleton)
        // + exactly one audit row (via: Admin, action "signup.set-notify", target
        // "signup") in the same session (C3 — no silent, unaudited access). The same
        // single-target singleton-toggle shape as SetSignupOpenAsync (the
        // timezone.set-default / dateformat.set-default precedent).
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        var settings = await session
            .LoadAsync<Localization.LocaleSettings>(
                Localization.LocaleSettings.SingletonId, ct)
            .ConfigureAwait(false);

        if (settings is null)
        {
            settings = new Localization.LocaleSettings { NotifyAdminsOnSignup = notify };
        }
        else
        {
            settings.NotifyAdminsOnSignup = notify;
        }

        session.Store(settings);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = adminSubjectId,
            EffectivePrincipalId = adminSubjectId,
            Action = "signup.set-notify",
            TargetKind = "signup",
            TargetId = "signup",
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
