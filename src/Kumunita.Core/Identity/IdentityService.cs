using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kumunita.Core.Identity;

/// <summary>
/// Step-6 concrete <see cref="IIdentityService"/> — the IdentityModule (ADR 0006 §A/§B).
/// Only this module knows the identity source (ASP.NET Identity, the <c>identity</c>
/// schema) and only it issues <see cref="ThinPrincipal"/>.
/// <para>
/// **Two stores, one Postgres:** the account (email, password hash, security stamp, roles)
/// is EF/ASP.NET Identity in the <c>identity</c> schema (via <see cref="UserManager{TUser}"/>);
/// the profile, the single-use tokens, the <c>AccessAudit</c> admin-action lane, and the
/// staged <c>OutboxEmail</c> are Marten documents in the <c>mt</c> schema. The audit row
/// for an admin-lane action (manual-verify, seed-admin, role change, break-glass) is
/// written into the **same Marten session** as the other <c>mt</c> writes and committed
/// with one <c>SaveChangesAsync</c> (invariant C3 — no silent, unaudited access). The EF
/// write and the Marten commit are ordered so the *account* (the primary domain op) exists
/// before its derivative <c>mt</c> rows; a rare failure between the two is the accepted
/// cross-store window (the design doc's same-transaction guarantee holds *within* a store,
/// and the seeder / services are the only writers).
/// </para>
/// <para>
/// The "cannot sign in until verified" gate (Profile: "Unverified accounts cannot sign
/// in") is enforced at sign-in by the Web (the <c>ClaimsPrincipalFactory</c> refuses to mint
/// a <c>Kumunita.Verified</c>=true claim for an unverified resident); the <c>mt</c> rows
/// here simply track <c>Profile.Verified</c>.
/// </para>
/// </summary>
public sealed class IdentityService(
    UserManager<User> userManager,
    Marten.IDocumentStore documentStore,
    IUserInfoService userInfo,
    IClaimsSource claimsSource,
    IMailerStage mailer,
    IOptions<VerificationOptions> verificationOptions,
    Microsoft.Extensions.Logging.ILogger<IdentityService> logger,
    Kumunita.Core.Localization.ITranslationProvider? translationProvider = null,
    NotificationService? notifications = null,
    // ADR 0138 — the SampleData__Enabled flag (config; the seeder's gate).
    // Placed last (optional, default null) so no existing positional caller
    // breaks: a null reader resolves as "not a sample-data instance" (the
    // <c>false</c> floor — the lock is unreachable by construction, the
    // ADR 0056 shape). DI always supplies the live value.
    IOptions<SampleDataOptions>? sampleDataOptions = null) : IIdentityService
{
    private const string ComponentKind = "component";
    private const string AccountKind = "account";

    // ── Principal reads (the only issuer of ThinPrincipal) ────────────────

    /// <inheritdoc />
    public Task<ThinPrincipal?> GetCurrentAsync()
    {
        // Request-driven: the claim set (minted by the Web factory at sign-in) is the whole
        // principal — pure mapping, no DB, no HTTP types (ADR 0006-D).
        return Task.FromResult(ClaimShaping.FromClaims(claimsSource.Current));
    }

    /// <inheritdoc />
    public async Task<ThinPrincipal?> GetBySubjectAsync(string subjectId)
    {
        var user = await userManager.FindByIdAsync(subjectId);
        if (user is null)
            return null;

        var profile = await userInfo.GetProfileAsync(subjectId);
        var verified = profile?.Verified ?? false;
        var blocked = profile?.Blocked ?? false;

        var identityRoles = (await userManager.GetRolesAsync(user)).ToList();
        var roles = new List<string>();
        if (blocked)
            return new ThinPrincipal(user.Id ?? subjectId, user.ExternalId, verified, ThinPrincipal.NoRoles);
        if (verified)
            roles.Add(Roles.Member);                    // Member is the verified-resident standing (implicit).
        foreach (var r in identityRoles)
            roles.Add(r);

        // A Moderator's per-component scoping flows on as `moderator:<component>` claim
        // values (ADR 0003) — the claim *set* stays within ClaimTypes.All.
        if (identityRoles.Contains(Roles.Moderator))
        {
            var assignments = await userInfo.GetAssignmentsAsync(subjectId);
            foreach (var a in assignments)
                roles.Add(Roles.ModeratorComponent(a.ComponentId));
        }

        return new ThinPrincipal(user.Id ?? subjectId, user.ExternalId, verified, roles);
    }

    // ── Signup + verification (the one designed handoff: the verification
    //    email, single-send; the only outward-seam exit) ─────────────────────

    /// <inheritdoc />
    public async Task<ThinPrincipal> RegisterAsync(string displayName, string email, string password)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException($"An account with email '{email}' already exists.");

        var now = DateTimeOffset.UtcNow;

        // 1. The account — EF / identity schema (the primary domain op).
        var user = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = email,
            UserName = email
        };
        await userManager.CreateAsync(user);
        await userManager.AddPasswordAsync(user, password);   // unverified: no login yet

        // 2. The mt-side rows — Profile (self-only, unverified), the single-use verify
        //    token (attempt 1), and the one staged OutboxEmail — in ONE session, one commit.
        var token = NewVerifyToken(user.Id, now);
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        session.Store(new Profile
        {
            SubjectId = user.Id,
            DisplayName = displayName,
            Email = email,
            Verified = false,               // cannot sign in until verified
            Visibility = new Authorization.Audience()   // self-only (owner branch author; C1 denies the rest)
        });
        session.Store(token);
        var verifyLink = VerificationLink(token.Id);
        var (verifySubject, verifyBody) = await BuildVerificationEmailAsync(
            displayName, verifyLink, preferredLanguage: null);
        await mailer.StageAsync(session,
                    idempotencyKey: $"verify:{user.Id}:1",
                    recipient: email,
                    subject: verifySubject,
                    body: verifyBody,
                    ct: default);

        // ADR 0077 — notify the GlobalAdmins (inbox + best-effort email) that a new
        // resident signed up. Best-effort (a throw is swallowed — the signup must
        // succeed even if the notification lane is unavailable), gated by the
        // instance NotifyAdminsOnSignup flag, and skipped when no GlobalAdmin
        // exists (a single-admin seed account is not notified about itself).
        await EmitAccountNotificationAsync(
            session, NotificationKinds.AccountSignup, user.Id,
            $"{displayName} <{email}>");

        await session.SaveChangesAsync();

        logger.LogInformation("Registered unverified resident {UserId} (email {Email}).", user.Id, email);
        return new ThinPrincipal(user.Id, user.ExternalId, IsVerifiedResident: false, ThinPrincipal.NoRoles);
    }

    /// <inheritdoc />
    public async Task<ResendVerificationResult> ResendVerificationEmailAsync(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return new ResendVerificationResult(false,
                $"No account found with email '{email}'. Sign up again to create one.");

        var profile = await userInfo.GetProfileAsync(user.Id ?? string.Empty);
        if (profile?.Verified == true)
            return new ResendVerificationResult(false,
                "This account is already verified — sign in instead.");

        await using (var session = documentStore.OpenSession(new Marten.Services.SessionOptions()))
        {
            var attempts = await session
                .Query<IdentityToken>()
                .Where(t => t.Kind == IdentityToken.KindVerify && t.UserId == user.Id)
                .Select(t => t.Attempt)
                .ToListAsync();

            var nextAttempt = attempts.Count == 0 ? 1 : attempts.Max() + 1;
            if (nextAttempt > verificationOptions.Value.MaxVerifyAttempts)
                return new ResendVerificationResult(false,
                    "Automatic verification-email resends for this account are exhausted. " +
                    "Ask an admin to verify your account.");

            var now = DateTimeOffset.UtcNow;
            var token = NewVerifyToken(user.Id ?? string.Empty, now, attempt: nextAttempt);
            session.Store(token);
            var resendName = profile?.DisplayName ?? user.Email ?? "there";
            var resendLink = VerificationLink(token.Id);
            var (resendSubject, resendBody) = await BuildVerificationEmailAsync(
                resendName, resendLink, preferredLanguage: profile?.EmailLanguage);
            await mailer.StageAsync(session,
                idempotencyKey: $"verify:{user.Id}:{nextAttempt}",
                recipient: email,
                subject: resendSubject,
                body: resendBody,
                ct: default);
            await session.SaveChangesAsync();

            logger.LogInformation(
                "Re-issued verification (attempt {Attempt}) for resident {UserId} (email {Email}).",
                nextAttempt, user.Id, email);
        }

        return new ResendVerificationResult(Success: true);
    }

    /// <inheritdoc />
    public async Task<string?> FindSubjectByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        var user = await userManager.FindByEmailAsync(email);
        return user?.Id;
    }

    /// <inheritdoc />
    public async Task<Profile> VerifyWithTokenAsync(string tokenValue)
    {
        var token = await FindTokenAsync(
            tokenValue, kind: IdentityToken.KindVerify, subjectId: null);
        if (token is null)
            throw new InvalidOperationException("Verification token is invalid, already used, or expired.");

        var user = await userManager.FindByIdAsync(token.UserId)
            ?? throw new InvalidOperationException($"No account for verification token user {token.UserId}.");

        // Same session: flip verified, consume the token, append the audit row (via: Owner —
        // the resident verifying their own account), one commit.
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var profile = (await session.LoadAsync<Profile>(token.UserId)) ?? new Profile
        {
            SubjectId = token.UserId,
            Email = user.Email,
            DisplayName = user.UserName ?? user.Email ?? string.Empty
        };
        profile.Verified = true;
        session.Store(profile);

        token.ConsumedAt = DateTimeOffset.UtcNow;
        session.Store(token);

        var now = DateTimeOffset.UtcNow;
        session.Store(AuditRow(now, token.UserId, token.UserId, "verify", AccountKind, token.UserId,
            Authorization.AccessVia.Owner, Authorization.AccessOutcome.Allow));

        // ADR 0077 — notify the GlobalAdmins (inbox + best-effort email) that a
        // resident verified their account. Same best-effort / gated / no-admin
        // shape as the RegisterAsync emitter (the account.verified kind).
        await EmitAccountNotificationAsync(
            session, NotificationKinds.AccountVerified, token.UserId,
            $"{profile.DisplayName} <{user.Email}>");

        await session.SaveChangesAsync();

        // Member is implicit on a verified resident (no EF role needed for the base standing).
        logger.LogInformation("Verified resident {UserId}.", token.UserId);
        return profile;
    }

    // ── Admin lane (each appends its own via-tagged audit row, invariant C3) ─

    /// <inheritdoc />
    public async Task<Profile> ManuallyVerifyAsync(string targetSubjectId, string adminSubjectId)
    {
        var admin = await RequireGlobalAdminAsync(adminSubjectId);
        _ = admin;
        var target = await userManager.FindByIdAsync(targetSubjectId)
            ?? throw new InvalidOperationException($"No account '{targetSubjectId}'.");
        _ = target;

        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var profile = (await session.LoadAsync<Profile>(targetSubjectId)) ?? new Profile
        {
            SubjectId = targetSubjectId,
            Email = target.Email,
            DisplayName = target.UserName ?? target.Email ?? string.Empty
        };
        profile.Verified = true;
        session.Store(profile);

        var now = DateTimeOffset.UtcNow;
        session.Store(AuditRow(now, adminSubjectId, adminSubjectId, "manual-verify", AccountKind, targetSubjectId,
            Authorization.AccessVia.Admin, Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        logger.LogInformation("Admin {Admin} manually verified {Target}.", adminSubjectId, targetSubjectId);
        return profile;
    }

    // ── Block / Unblock (the admin suspension lane — mirrors the verify/role lanes) ────

    /// <inheritdoc />
    public async Task BlockAsync(string targetSubjectId, string adminSubjectId)
    {
        var target = await SetBlockedAsync(targetSubjectId, adminSubjectId, blocked: true);
        logger.LogInformation("Admin {Admin} blocked {Target}.", adminSubjectId, target.Id);
    }

    /// <inheritdoc />
    public async Task UnblockAsync(string targetSubjectId, string adminSubjectId)
    {
        var target = await SetBlockedAsync(targetSubjectId, adminSubjectId, blocked: false);
        logger.LogInformation("Admin {Admin} unblocked {Target}.", adminSubjectId, target.Id);
    }

    private async Task<User> SetBlockedAsync(string targetSubjectId, string adminSubjectId, bool blocked)
    {
        var admin = await RequireGlobalAdminAsync(adminSubjectId);
        _ = admin;
        var target = await userManager.FindByIdAsync(targetSubjectId)
            ?? throw new InvalidOperationException($"No account '{targetSubjectId}'.");

        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var profile = (await session.LoadAsync<Profile>(targetSubjectId)) ?? new Profile
        {
            SubjectId = targetSubjectId,
            Email = target.Email,
            DisplayName = target.UserName ?? target.Email ?? string.Empty
        };
        profile.Blocked = blocked;
        session.Store(profile);

        var now = DateTimeOffset.UtcNow;
        session.Store(AuditRow(now, adminSubjectId, adminSubjectId,
            blocked ? "block" : "unblock", AccountKind, targetSubjectId,
            Authorization.AccessVia.Admin, Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        // The block takes effect at the Identity↔cookie seam: a blocked account mints no
        // roles, so it has no standing (no Member/Moderator/GlobalAdmin). Rotate the
        // security stamp — the same invalidation the demotion/password lanes rely on (OPS §10)
        // — so existing sessions must re-mint to reflect the change.
        await userManager.UpdateSecurityStampAsync(target);

        return target;
    }

    // ── Signup policy (ADR 0050 — the admin-managed open / invitation-only gate) ──

    /// <inheritdoc />
    public async Task<bool> IsSignupOpenAsync()
    {
        // ADR 0050 read seam: the instance gate (LocaleSettings.IsSignupOpen) with
        // the `true` floor — a missing singleton or an unset value both yield
        // `true`, so a fresh instance ships with sign-up open (the development
        // circle keeps working until an admin tightens it). A read (no audit row),
        // the same shape as the LocaleSettings reads the Localization lane does.
        using var session = documentStore.QuerySession();
        var settings = await session.LoadAsync<Localization.LocaleSettings>(
            Localization.LocaleSettings.SingletonId, CancellationToken.None);

        // `true` floor: a null settings row (never seen — but defensively) keeps the
        // gate open; only an explicit `false` closes sign-up.
        return settings is null || settings.IsSignupOpen;
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
        // out of changing its own password?" rule lives, so the resident
        // /account/password surface and the ChangePasswordAsync enforcement
        // guard below agree. Four conjuncts:
        //   (1) the instance carries the closed set (SampleData__Enabled);
        //   (2) the admin opted the instance in (the locale gate);
        //   (3) the subject is a sample account (a member of the closed
        //       SampleAccountEmails set, by its profile e-mail);
        //   (4) the subject is NOT a GlobalAdmin (the sample admin is exempt).
        // Any real (non-sample) account, or the sample admin, is never locked
        // (short-circuit on (3)/(4)) — only a non-admin sample account on an
        // opted-in demo instance is.
        if (!await IsSampleDataEnabledAsync())
            return false;
        if (!await IsSamplePasswordChangeLockedAsync())
            return false;

        // (3) sample-account membership by profile e-mail (the ADR 0078 closed
        // set the notification-suppression gate compares against — the same
        // single source of truth, so the lock and the suppression cannot drift).
        var profile = await userInfo.GetProfileAsync(subjectId);
        var email = profile?.Email;
        if (string.IsNullOrWhiteSpace(email)
            || !Kumunita.Core.Bootstrap.SampleDataSeeder.SampleAccountEmails.Contains(email.Trim()))
            return false;

        // (4) the sample admin is exempt — a sample account holding GlobalAdmin
        // (the seed admin) may still change its own password, by construction.
        var user = await userManager.FindByIdAsync(subjectId);
        if (user is null)
            return false;   // no account → not a lockable sample account
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(Roles.GlobalAdmin))
            return false;

        return true;
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

    // ── M19 guest standing (ADR 0120, D2 — the single audited write lane) ──

    /// <inheritdoc />
    public async Task<GuestAccess?> GetGuestAccessAsync(string subjectId)
    {
        using var session = documentStore.QuerySession();
        // The C-M19·4 empty floor: null is a valid state (a guest with no
        // settled standing), not an error.
        return await session.LoadAsync<GuestAccess>(subjectId, CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task SetGuestAccessAsync(GuestAccess access, string adminSubjectId)
    {
        // ADR 0120 write seam: the admin-settled allowance (the GuestAccess
        // document, id = the guest's SubjectId) + exactly one audit row (via:
        // Admin, action "guest.set-standing", target "guest:{subjectId}") in
        // the same session (C-M19·5 — no silent, unaudited access). The
        // SetSignupOpenAsync body pattern: one write session, store the
        // document + set Profile.IsGuest (the D1 flag) + one audit row, commit.
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var ct = System.Threading.CancellationToken.None;

        // Settle the settling admin (the authoritative ActorId) and store the
        // allowance. access.SubjectId is the guest's subject id (the document
        // identity) — the caller owns it; we do not re-derive it.
        access.SetByAdmin = adminSubjectId;
        session.Store(access);

        // Mark the account a guest (the D1 flag, C-M19·1) in the same session.
        // A guest is still a real account — the Profile row exists for a real
        // Identity account; load-or-create so a guest standing can be settled
        // before the profile row (defensively) rather than failing.
        var profile = await session.LoadAsync<Kumunita.Core.UserInfo.Profile>(
            access.SubjectId, ct).ConfigureAwait(false);
        if (profile is not null)
        {
            profile.IsGuest = true;
            session.Store(profile);
        }

        // Exactly one audit row (C-M19·5, D2) — the singleton-toggle shape
        // (the signup.set-open precedent).
        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = adminSubjectId,
            EffectivePrincipalId = adminSubjectId,
            Action = "guest.set-standing",
            TargetKind = "guest",
            TargetId = $"guest:{access.SubjectId}",
            Via = Authorization.AccessVia.Admin,
            Outcome = Authorization.AccessOutcome.Allow
        });

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ── ADR 0142 — account deletion (the resident-leave / admin-removal lane) ──

    /// <inheritdoc />
    public async Task DeleteAccountAsync(string targetSubjectId, string adminSubjectId)
    {
        // ADR 0142 — the self-serve vs. admin distinction.
        //   * Self-deletion (adminSubjectId == targetSubjectId): the resident
        //     is deleting *their own* account. No GlobalAdmin gate — any
        //     resident may leave the platform. The audit row is Via: Owner.
        //   * Admin-initiated (adminSubjectId != targetSubjectId): a
        //     GlobalAdmin is removing *another* resident. The GlobalAdmin
        //     gate applies (the RequireGlobalAdminAsync shape — the
        //     BlockAsync / UnblockAsync precedent; the fail-closed pin). The
        //     audit row is Via: Admin.
        var isSelfDeletion = string.Equals(
            targetSubjectId, adminSubjectId, StringComparison.Ordinal);

        if (isSelfDeletion)
        {
            // Self-deletion — the resident acts on their own account.
            // ADR 0142 D5 gate: the self-serve lane is reachable only by a
            // GlobalAdmin (a non-GlobalAdmin's crafted POST is refused
            // server-side, the fail-closed pin). The resident's current
            // password was already verified by the Web controller before
            // reaching here, so the lane itself only checks the standing.
            var self = await userManager.FindByIdAsync(targetSubjectId).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"No account '{targetSubjectId}'.");
            var selfRoles = (await userManager.GetRolesAsync(self).ConfigureAwait(false)).ToList();
            if (!selfRoles.Contains(Roles.GlobalAdmin))
                throw new UnauthorizedAccessException(
                    "The self-serve delete-account lane is only available to a " +
                    "GlobalAdmin. A non-GlobalAdmin resident cannot delete " +
                    "their own account — contact an administrator to remove " +
                    "the account.");
            await DeleteCoreAsync(
                targetSubjectId, self,
                actorId: targetSubjectId,
                via: Authorization.AccessVia.Owner);
            return;
        }

        // Admin-initiated deletion of a *different* account.
        // 1. Admin gate (the RequireGlobalAdminAsync shape).
        var admin = await RequireGlobalAdminAsync(adminSubjectId).ConfigureAwait(false);
        _ = admin;

        // 2. The target must exist (the BlockAsync / SetBlockedAsync shape).
        var target = await userManager.FindByIdAsync(targetSubjectId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No account '{targetSubjectId}'.");

        await DeleteCoreAsync(
            targetSubjectId, target,
            actorId: adminSubjectId,
            via: Authorization.AccessVia.Admin);
    }

    // ── ADR 0143 — guardian delete-child (the GU standing over the ADR 0142 core) ──

    /// <inheritdoc />
    public async Task DeleteChildAccountAsync(string childId, string guardianId)
    {
        if (string.IsNullOrWhiteSpace(childId))
            throw new ArgumentException("Child id is required.", nameof(childId));
        if (string.IsNullOrWhiteSpace(guardianId))
            throw new ArgumentException("Guardian id is required.", nameof(guardianId));

        // Standing gate FIRST (G·2/G·3, live — the GuardActiveLinkAsync
        // shape every other GU seam runs, and the house convention: gate
        // before existence). An ACTIVE GuardianLink for this exact
        // (guardian, child) pair must exist, else refuse before any write.
        // A child is never a GlobalAdmin, so there is no GlobalAdmin gate
        // here (C·2 deny-by-default: the lane is reachable only through an
        // active link); a non-guardian's crafted call is refused with
        // UnauthorizedAccessException (the Web's 404). Checking the standing
        // first also keeps the response uniform for an existing vs. a
        // non-existing target (both a 404 — no account-existence oracle),
        // the ADR 0012/0013 "a non-guardian learns nothing" shape.
        await using (var gateSession = documentStore.OpenSession(new Marten.Services.SessionOptions()))
        {
            var link = await gateSession.Query<Kumunita.Core.UserInfo.GuardianLink>()
                .Where(l => l.GuardianId == guardianId && l.ChildId == childId
                            && l.Status == GuardianLinkStatus.Active)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (link is null)
                throw new UnauthorizedAccessException(
                    $"No active guardian link for ({guardianId}, {childId}).");
        }

        // The target must exist (a defensive check AFTER the standing gate —
        // the SuspendChildAsync "load the profile, missing → InvalidOperationException"
        // shape). A second call for an already-deleted child reaches the
        // standing gate first (the successful delete dissolved the link, C·5)
        // and is refused with the same 404 — not a silent no-op.
        var target = await userManager.FindByIdAsync(childId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No account '{childId}'.");

        await DeleteCoreAsync(
            childId, target,
            actorId: guardianId,
            via: Authorization.AccessVia.Guardian,
            // ADR 0143 C·5 — no dangling standing: dissolve every
            // GuardianLink row for the deleted child (any status) in the
            // same session as the core's writes, so a co-guardian's row
            // does not dangle pointing at a deleted account.
            dissolveGuardianLinksForChildAsync: async (session, now) =>
            {
                var rows = await session.Query<Kumunita.Core.UserInfo.GuardianLink>()
                    .Where(l => l.ChildId == childId)
                    .ToListAsync()
                    .ConfigureAwait(false);
                foreach (var row in rows)
                {
                    if (row.Status != GuardianLinkStatus.Dissolved)
                    {
                        row.Status = GuardianLinkStatus.Dissolved;
                        row.DissolvedAt = now;
                        row.DissolvedBy = guardianId;
                    }
                    session.Store(row);
                }
            });
    }

    /// <summary>
    /// ADR 0142 — the shared body of <see cref="DeleteAccountAsync"/> for both
    /// the self-deletion and admin-initiated branches: the last-GlobalAdmin
    /// guard, the pseudonymization, the membership/profile removal, the audit
    /// row, and the Identity account deletion. The branch-specific actor +
    /// <c>Via</c> are passed in (the self-serve row is <c>Via: Owner</c>; the
    /// admin row is <c>Via: Admin</c>).
    /// </summary>
    private async Task DeleteCoreAsync(
        string targetSubjectId, User target, string actorId, Authorization.AccessVia via,
        System.Func<IDocumentSession, DateTimeOffset, Task>? dissolveGuardianLinksForChildAsync = null)
    {
        // Last-GlobalAdmin guard (the lockout pin, the OPS.md §9 "Hand over
        // admin" precedent). If the target is a GlobalAdmin and they are the
        // only one on the instance, refuse before any write — this applies to
        // *both* branches (a lone GlobalAdmin cannot self-delete, and no
        // admin may delete the last GlobalAdmin). The recovery path is to
        // promote a second GlobalAdmin first (the "Hand over admin" procedure).
        var targetRoles = (await userManager.GetRolesAsync(target).ConfigureAwait(false)).ToList();
        if (targetRoles.Contains(Roles.GlobalAdmin))
        {
            var allGAs = (await userManager
                .GetUsersInRoleAsync(Roles.GlobalAdmin).ConfigureAwait(false)).ToList();
            var otherGAs = allGAs.Where(u => u?.Id is string uid && uid != targetSubjectId).ToList();
            if (otherGAs.Count == 0)
                throw new InvalidOperationException(
                    "This is the last GlobalAdmin on the instance. Promote a " +
                    "second GlobalAdmin before deleting this account (the " +
                    "OPS.md §9 'Hand over admin' procedure).");
        }

        // 4. Open the Marten session for the pseudonymization + membership
        //    removal + audit row (the C3 single-commit shape — all in one
        //    SaveChangesAsync).
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var ct = System.Threading.CancellationToken.None;
        var now = DateTimeOffset.UtcNow;

        // 5. Pseudonymize the target's audit rows (the OPS.md §9 /
        //    ARCHITECTURE.md §5 "Deletion-of-account interaction" — the
        //    rows remain, the actor id is rewritten to a tombstone). The
        //    summary row (step 9 below) is the audit-of-the-deletion; the
        //    per-row rewrites are the pseudonymization itself.
        var tombstone = $"deleted:{targetSubjectId}";
        var auditRows = await session.Query<Authorization.AccessAudit>()
            .Where(a => a.ActorId == targetSubjectId)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var row in auditRows)
        {
            row.ActorId = tombstone;
            if (row.EffectivePrincipalId == targetSubjectId)
                row.EffectivePrincipalId = tombstone;
            session.Store(row);
        }

        // 6. Remove the target's group memberships (they leave every group;
        //    the strong-consistency C4 invariant means the next
        //    GetGroupIdsAsync call will not find them). The audit rows for
        //    the *individual* removals are subsumed by the single
        //    "account.delete" summary row (step 9) — the same "one summary,
        //    many rows" shape the purge job's AuditPurgeSummary follows.
        var groupMemberships = await session.Query<Kumunita.Core.UserInfo.GroupMembership>()
            .Where(m => m.UserId == targetSubjectId)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var gm in groupMemberships)
        {
            session.Delete(gm);
        }

        // 7. Remove the target's component memberships (posting rights) and
        //    moderator assignments (governing scope).
        var compMemberships = await session.Query<Kumunita.Core.UserInfo.ComponentMembership>()
            .Where(m => m.UserId == targetSubjectId)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var cm in compMemberships)
        {
            session.Delete(cm);
        }

        var modAssignments = await session.Query<Kumunita.Core.UserInfo.ModeratorAssignment>()
            .Where(m => m.UserId == targetSubjectId)
            .ToListAsync(ct).ConfigureAwait(false);
        foreach (var ma in modAssignments)
        {
            session.Delete(ma);
        }

        // 8. Remove the target's Profile row (their PII — name, email, phone,
        //    address, bio, preferences — is gone). The directory's
        //    non-blocked filter (DirectoryService's `!p.Blocked`) would hide
        //    a Blocked profile, but deletion is the *stronger* signal: the
        //    row is simply absent, so every read path (the directory, the
        //    profile detail, the author-name resolution) falls back to the
        //    raw subject id (the existing null-safe `profile?.DisplayName ??
        //    authorId` idiom).
        var profile = await session.LoadAsync<Kumunita.Core.UserInfo.Profile>(
            targetSubjectId, ct).ConfigureAwait(false);
        if (profile is not null)
        {
            session.Delete(profile);
        }

        // 8b. ADR 0143 — dissolve every GuardianLink row for the deleted
        //     child (any status) in this same session (C·5 — no dangling
        //     standing; C4 strong-consistency). The ADR 0142 self/admin
        //     branches pass no callback (a GlobalAdmin or a self-deleting
        //     resident is never a child in a GuardianLink); the guardian
        //     delete-child branch supplies one. The per-row dissolution
        //     writes nothing of their own — the single "account.delete"
        //     summary row (step 9) subsumes them, the ADR 0142 "one
        //     summary, many rows" shape.
        if (dissolveGuardianLinksForChildAsync is not null)
        {
            await dissolveGuardianLinksForChildAsync(session, now).ConfigureAwait(false);
        }

        // 9. Exactly one audit row — the "account.delete" summary (the
        //    AuditPurgeSummary precedent: one summary row for a bulk
        //    operation). The actor is the one who performed the deletion
        //    (the resident themselves on self-deletion, the GlobalAdmin on
        //    admin-initiated); the target is the deleted account. The
        //    <c>Via</c> tag carries the branch (Owner / Admin).
        session.Store(AuditRow(now, actorId, actorId,
            "account.delete", AccountKind, targetSubjectId,
            via, Authorization.AccessOutcome.Allow));

        await session.SaveChangesAsync(ct).ConfigureAwait(false);

        // 10. Delete the Identity account (EF Core, the identity schema).
        //     This removes the password hash, the role assignments, and the
        //     user row itself. Done AFTER the Marten commit (the
        //     "the account exists before its derivative mt rows" ordering
        //     the class doc's "two stores, one Postgres" paragraph
        //     describes, inverted: the mt cleanup commits first, then the
        //     identity row is removed). A rare failure between the two is
        //     the accepted cross-store window (the same "accepted cross-
        //     store window" the class doc already acknowledges for the
        //     register/verify lanes).
        var result = await userManager.DeleteAsync(target).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to delete the identity account: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        logger.LogInformation(
            "Actor {Actor} (via {Via}) deleted account {Target}. {AuditRows} audit rows pseudonymized, {GroupMemberships} group memberships removed, {CompMemberships} component memberships removed, {ModAssignments} moderator assignments removed.",
            actorId, via, targetSubjectId,
            auditRows.Count, groupMemberships.Count,
            compMemberships.Count, modAssignments.Count);
    }

    // ── ADR 0077 — the account-lane GlobalAdmin emitters ──────────────────

    /// <summary>
    /// ADR 0077 — notify every <c>GlobalAdmin</c> (via the frozen
    /// <see cref="Notifications.NotificationService"/> emitter) of an account
    /// event (the <c>account.signup</c> or <c>account.verified</c> kind). Called
    /// on the caller's open <paramref name="session"/> (the C3 single-commit
    /// shape — the caller commits after). **Best-effort:** any failure (no
    /// emitter wired, no GlobalAdmin, an emit error) is swallowed and logged —
    /// the account operation (register / verify) is the primary domain op and
    /// must succeed even if the notification lane is unavailable (the M6 "email
    /// is best-effort, the inbox is the durable record" posture, D5). Gated by
    /// the instance <see cref="Localization.LocaleSettings.NotifyAdminsOnSignup"/>
    /// flag (the <c>true</c> floor). A fresh instance's single seed-admin
    /// account is not notified about itself (no other admin exists, so the
    /// recipient set is empty — a no-op, not a noise path).
    /// </summary>
    private async Task EmitAccountNotificationAsync(
        IDocumentSession session,
        string kind,
        string accountId,
        string snippet)
    {
        try
        {
            if (notifications is null)
                return;                                          // not wired (test harness) — the account op still succeeds

            if (!await IsNotifyAdminsOnSignupAsync().ConfigureAwait(false))
                return;                                          // the admin disabled the notify — a no-op

            var admins = (await userManager
                .GetUsersInRoleAsync(Roles.GlobalAdmin).ConfigureAwait(false)).ToList();
            if (admins.Count == 0)
                return;                                          // no GlobalAdmin (a fresh instance) — nothing to notify

            // D4 / F10 — a stable, content-derived key per recipient: the same
            // logical event is one inbox row + one email per admin (a re-emission
            // of the same (event, admin) is a no-op, a different admin is a
            // distinct key). The recipient id is part of the key because
            // EmitAsync dedups by key alone (the recipient is a parameter, not part
            // of the key — the §6.3 shape + the per-admin fan-out).
            foreach (var admin in admins)
            {
                var adminId = admin?.Id ?? string.Empty;
                if (adminId.Length == 0)
                    continue;
                await notifications.EmitAsync(
                    session,
                    recipientId: adminId,
                    kind: kind,
                    idempotencyKey: $"notification:{kind}:{accountId}:{adminId}",
                    body: snippet,
                    ct: default).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Best-effort (D5): the account operation must not fail because the
            // notification lane is unavailable — log and let the caller commit the
            // domain write. The inbox row / email are the nudge, not the record.
            logger.LogWarning(ex, "Account-lane notification ({Kind}) for {AccountId} was not emitted.",
                kind, accountId);
        }
    }

    /// <inheritdoc />
    public async Task<ThinPrincipal> CompleteSeedAdminSetupAsync(string email, string setupTokenValue, string newPassword)
    {
        var user = await userManager.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"No seed-admin account for email '{email}'.");

        var token = await FindTokenAsync(setupTokenValue, kind: IdentityToken.KindSetup, subjectId: user.Id);
        if (token is null)
            throw new InvalidOperationException("Setup token is invalid, already used, or expired (single-use).");

        // Password is a single-use credential: replace it with the resident's chosen one.
        await userManager.RemovePasswordAsync(user);
        await userManager.AddPasswordAsync(user, newPassword);

        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        var profile = (await session.LoadAsync<Profile>(user.Id)) ?? new Profile
        {
            SubjectId = user.Id,
            Email = email,
            DisplayName = email
        };
        profile.Verified = true;
        session.Store(profile);

        var now = DateTimeOffset.UtcNow;
        token.ConsumedAt = now;
        session.Store(token);
        session.Store(AuditRow(now, user.Id, user.Id, "seed-admin.setup", AccountKind, user.Id,
            Authorization.AccessVia.Admin, Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        // Invalidate any pre-existing session (the seed token is one-time, but the stamp
        // rotation is the belt-and-braces for "the account is brand new").
        await userManager.UpdateSecurityStampAsync(user);

        var identityRoles = (await userManager.GetRolesAsync(user)).ToList();
        return new ThinPrincipal(user.Id, user.ExternalId, IsVerifiedResident: true, identityRoles);
    }

    /// <inheritdoc />
    public async Task<bool> IsFirstBootSetupCompleteAsync()
    {
        // The first-boot setup lane is "still open" exactly while an unconsumed,
        // unexpired KindSetup token exists. ConsumedAt is set by
        // CompleteSeedAdminSetupAsync in its single commit, so its presence is the
        // completion signal; an unconsumed-but-expired token is dead setup material
        // too — the link must not linger advertising an unusable token either.
        await using var session = documentStore.OpenSession(new SessionOptions());
        var now = DateTimeOffset.UtcNow;
        var liveSetupToken = await session
            .Query<IdentityToken>()
            .Where(t => t.Kind == IdentityToken.KindSetup
                && t.ConsumedAt == null
                && t.ExpiresAt > now)
            .FirstOrDefaultAsync();

        return liveSetupToken is null;
    }

    /// <inheritdoc />
    public async Task ConsumeBreakGlassAsync(string subjectId, string token)
    {
        // AdminOverride is the hand-rolled operator-written row (ADR 0004 §B.1): the app
        // writes back ONLY the consumed flag on first presentation (single-use); every
        // subsequent privileged DECISION under it records via:BreakGlass in the
        // AuthorizationModule's inline read.
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());

        // Two parameterised commands on the same session connection — the UPDATE sets
        // the consumed flag (single-use; the WHERE guards against re-presentation), then
        // a fresh SELECT confirms the row exists and is now consumable (otherwise no
        // UPDATE bound). Both commands run on the same physical connection the session
        // owns, so they see a consistent view.
        var conn = (Npgsql.NpgsqlConnection)session.Connection!;

        await using var update = conn.CreateCommand();
        update.CommandText =
            "UPDATE \"mt\".\"AdminOverride\" " +
            "SET \"consumedAt\" = @now " +
            "WHERE \"userId\" = @userId " +
            "  AND \"token\" = @token " +
            "  AND \"consumedAt\" IS NULL";
        void AddU(string name, object value)
        {
            var p = update.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            update.Parameters.Add(p);
        }
        AddU("@now", DateTimeOffset.UtcNow);
        AddU("@userId", subjectId);
        AddU("@token", token);
        await update.ExecuteNonQueryAsync().ConfigureAwait(false);

        // Confirm the row exists and is now consumed (otherwise the UPDATE bound 0 rows:
        // no such (userId, token) row, or already consumed by someone else).
        await using var verify = conn.CreateCommand();
        verify.CommandText =
            "SELECT (\"consumedAt\" IS NOT NULL) " +
            "FROM \"mt\".\"AdminOverride\" WHERE \"userId\" = @userId AND \"token\" = @token";
        void AddV(string name, object value)
        {
            var p = verify.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            verify.Parameters.Add(p);
        }
        AddV("@userId", subjectId);
        AddV("@token", token);
        var consumed = await verify.ExecuteScalarAsync().ConfigureAwait(false);
        if (consumed is not bool b || !b)
            throw new InvalidOperationException(
                "Break-glass token is not recognized, was already consumed, or expired.");

        // The elevation is in effect from here until ExpiresAt; the decision-time check
        // (AuthorizationService.HasBreakGlassAsync) is the live gate. No row delete — the
        // row stays (history + the consumed flag).
        session.Store(AuditRow(DateTimeOffset.UtcNow, subjectId, subjectId, "break-glass.consume",
            "admin_override", subjectId, Authorization.AccessVia.BreakGlass, Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        logger.LogInformation("Account {UserId} consumed break-glass elevation.", subjectId);
    }

    // ── Role promote/demote + component scope (ADR 0003) ──────────────────

    /// <inheritdoc />
    public async Task SetRoleAsync(string targetSubjectId, string adminSubjectId,
        IReadOnlyCollection<string> roles, IReadOnlyList<string>? componentIds)
    {
        var admin = await RequireGlobalAdminAsync(adminSubjectId);
        _ = admin;
        var target = await userManager.FindByIdAsync(targetSubjectId)
            ?? throw new InvalidOperationException($"No account '{targetSubjectId}'.");

        // ADR 0030 — roles are independent: `roles` is the *set* of elevated roles the
        // target should hold (any subset of the three). `Member` is the implicit
        // verified-resident standing and never appears here, so it is filtered out
        // defensively (a stray "Member" in the set is a no-op either way).
        var wants = (roles ?? [])
            .Where(r => r is Roles.Moderator or Roles.Translator or Roles.GlobalAdmin)
            .ToHashSet();
        var wantsGlobalAdmin = wants.Contains(Roles.GlobalAdmin);
        var wantsModerator = wants.Contains(Roles.Moderator);
        var wantsTranslator = wants.Contains(Roles.Translator);

        var targetRoles = (await userManager.GetRolesAsync(target)).ToHashSet();
        bool rolesChanged =
            wantsGlobalAdmin != targetRoles.Contains(Roles.GlobalAdmin) ||
            wantsModerator   != targetRoles.Contains(Roles.Moderator) ||
            wantsTranslator  != targetRoles.Contains(Roles.Translator);

        // Apply the GlobalAdmin/Moderator/Translator identity roles (Member is the implicit
        // verified standing — no EF role for it). AddTo/RemoveFromRole manage the role
        // membership; the role row itself is created by the host's seed (FirstBootSeeder)
        // when the account is granted the role for the first time.
        if (wantsGlobalAdmin)  await userManager.AddToRoleAsync(target, Roles.GlobalAdmin);
        else                   await userManager.RemoveFromRoleAsync(target, Roles.GlobalAdmin);
        if (wantsModerator)    await userManager.AddToRoleAsync(target, Roles.Moderator);
        else                   await userManager.RemoveFromRoleAsync(target, Roles.Moderator);
        if (wantsTranslator)   await userManager.AddToRoleAsync(target, Roles.Translator);
        else                   await userManager.RemoveFromRoleAsync(target, Roles.Translator);

        // Security stamp: demoted accounts lose elevated access on the NEXT request, not at
        // cookie expiry (OPS §10). Rotate regardless to be safe (a re-signin is required to
        // pick up new claims).
        if (rolesChanged)
            await userManager.UpdateSecurityStampAsync(target);

        // Component-scope assignments (mt): upsert on promote-to-Moderator, delete on demote
        // (strong consistency — the next GetAssignmentsAsync reflects it; the "who cleared,
        // when" history is in the access_audit admin-action row, not in the assignment).
        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        if (wantsModerator && componentIds is { Count: > 0 })
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var componentId in componentIds)
                session.Store(new ModeratorAssignment
                {
                    Id = Guid.NewGuid().ToString("N"),
                    UserId = targetSubjectId,
                    ComponentId = componentId,
                    GrantedBy = adminSubjectId,
                    At = now
                });
        }
        else
        {
            // Delete every assignment for the target (strong consistency, invariant C4).
            var existing = await session
                .Query<ModeratorAssignment>()
                .Where(a => a.UserId == targetSubjectId)
                .ToListAsync()
                .ConfigureAwait(false);
            foreach (var a in existing)
                session.Delete<ModeratorAssignment>(a.Id);
        }

        session.Store(AuditRow(DateTimeOffset.UtcNow, adminSubjectId, adminSubjectId,
            "role", "role", targetSubjectId, Authorization.AccessVia.Admin, Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        logger.LogInformation("Admin {Admin} set {Target}'s roles to {Roles}/{Components}.",
            adminSubjectId, targetSubjectId, string.Join(",", wants), string.Join(",", componentIds ?? []));
    }

    // ── Password (self-serve or admin reset) ───────────────────────────────

    /// <inheritdoc />
    public async Task ChangePasswordAsync(string subjectId, string newPassword, bool byAdmin)
    {
        // ADR 0138 — the enforcement guard (the single place the lock bites,
        // so the resident /account/password surface and this write path can
        // never disagree). It applies to the self-serve lane only (byAdmin
        // false): an admin's reset of a sample account is always allowed (an
        // admin must be able to recover a demo credential they set), so the
        // guard is a no-op for the byAdmin path. A locked, non-admin sample
        // account is denied *before* any write (no audit row for the blocked
        // attempt — the RemoveLanguageAsync / M·7 fail-closed pin) and before
        // any security-stamp rotation (so the account stays usable).
        if (!byAdmin && await IsChangePasswordLockedForAsync(subjectId))
        {
            throw new UnauthorizedAccessException(
                "This is a demo (sample) account and password changes are locked by the " +
                "administrator. Use a different account if you need to test a password change.");
        }

        var user = await userManager.FindByIdAsync(subjectId)
            ?? throw new InvalidOperationException($"No account '{subjectId}'.");

        await userManager.RemovePasswordAsync(user);
        await userManager.AddPasswordAsync(user, newPassword);
        await userManager.UpdateSecurityStampAsync(user);      // invalidate every existing session

        await using var session = documentStore.OpenSession(new Marten.Services.SessionOptions());
        session.Store(AuditRow(DateTimeOffset.UtcNow, subjectId, subjectId,
            "password.change", AccountKind, subjectId,
            byAdmin ? Authorization.AccessVia.Admin : Authorization.AccessVia.Owner,
            Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        logger.LogInformation("Password changed for {UserId} ({ByAdmin}).", subjectId, byAdmin ? "admin" : "self");
    }

    // ── Guards + helpers ───────────────────────────────────────────────────

    private async Task<User> RequireGlobalAdminAsync(string adminSubjectId)
    {
        var admin = await userManager.FindByIdAsync(adminSubjectId)
            ?? throw new InvalidOperationException($"No admin account '{adminSubjectId}'.");
        var roles = await userManager.GetRolesAsync(admin);
        if (!roles.Contains(Roles.GlobalAdmin))
            throw new UnauthorizedAccessException($"Account {adminSubjectId} is not a GlobalAdmin.");
        return admin;
    }

    private async Task<IdentityToken?> FindTokenAsync(string tokenValue, string kind, string? subjectId)
    {
        await using var session = documentStore.OpenSession(new SessionOptions());
        var now = DateTimeOffset.UtcNow;
        var query = session
            .Query<IdentityToken>()
            .Where(t => t.Token == tokenValue && t.Kind == kind)
            .Where(t => t.ConsumedAt == null && t.ExpiresAt > now);
        if (subjectId is not null)
            query = query.Where(t => t.UserId == subjectId);
        return await query.FirstOrDefaultAsync().ConfigureAwait(false);
    }

    private IdentityToken NewVerifyToken(string userId, DateTimeOffset now, int attempt = 1) => new()
{
        Id = Guid.NewGuid().ToString("N"),
        Kind = IdentityToken.KindVerify,
        UserId = userId,
        Token = NewSecret(),   // high-entropy; never in a URL (the URL carries the row Id)
        Attempt = attempt,
        CreatedAt = now,
        // The bound option (Verification__TtlDays in appsettings, default 14) — see the
        // VerificationOptions class doc for the re-verify semantics (each attempt gets its
        // own window).
        ExpiresAt = now.AddDays(verificationOptions.Value.TtlDays)
    };

    private static Authorization.AccessAudit AuditRow(
        DateTimeOffset at, string actorId, string effective, string action,
        string targetKind, string targetId, Authorization.AccessVia via,
        Authorization.AccessOutcome outcome) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        At = at,
        ActorId = actorId,
        EffectivePrincipalId = effective,
        Action = action,
        TargetKind = targetKind,
        TargetId = targetId,
        Via = via,
        Outcome = outcome
    };

    private static string NewSecret()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    // The link target is the Web host's AccountController.Verify action, reached
    // through the conventional "default" route ({controller=Home}/{action=Index}/
    // {id?}) — /account/verify?id={token row id}. The row id is what travels in the
    // URL (§6.2: the high-entropy secret is never in a link); if the operator set
    // Verification__BaseUrl, prefix it to make the path absolute — an email client
    // can't resolve a relative path. Unset (a dev-only shape) keeps the relative
    // path, which a human reading the mail can still copy into the browser's bar.
    private string VerificationLink(string tokenRowId) =>
        (verificationOptions.Value.BaseUrl is string root and not ""
            ? root.TrimEnd('/')
            : string.Empty) + $"/account/verify?id={tokenRowId}";

    private static string VerificationBody(string displayName, string verifyLink) =>
        $"Hi {displayName},\n\nYour Kumunita account is set to verify on its first sign-in. " +
        $"Open this one-time link to confirm the account (it also signs you in):\n\n{verifyLink}\n\n" +
        "If you didn't create this account, you can ignore this message.";

    /// <summary>
    /// ADR 0061 — the verification email's subject + body, resolved through the
    /// <see cref="Kumunita.Core.Localization.ITranslationProvider"/> when one is
    /// available (per-recipient preferred language → instance default →
    /// <c>en</c> floor), with the English literals as the fallback for the two
    /// test sites that construct <see cref="IdentityService"/> directly without a
    /// provider. The body template's <c>{0}</c>/<c>{1}</c> placeholders are the
    /// resident's display name and the one-time verify link (ADR 0061 / the
    /// <c>email.verify_body</c> registry entry).
    /// </summary>
    private async Task<(string Subject, string Body)> BuildVerificationEmailAsync(
        string displayName, string verifyLink, string? preferredLanguage)
    {
        if (translationProvider is null)
            return ("Verify your Kumunita account", VerificationBody(displayName, verifyLink));

        string subject = await translationProvider.GetAsync("email.verify_subject", preferredLanguage);
        string bodyTemplate = await translationProvider.GetAsync("email.verify_body", preferredLanguage);
        string body = string.Format(bodyTemplate, displayName, verifyLink);
        return (subject, body);
    }
}


