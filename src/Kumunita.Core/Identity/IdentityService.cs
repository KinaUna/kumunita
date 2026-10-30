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
    IOptions<SampleDataOptions>? sampleDataOptions = null,
    // F1 (DB-13 GDPR, U4) — the byte-store delete seam (F1.U2's
    // <c>IMediaFileStore.DeleteIfExistsAsync</c>). Optional (default null) for
    // the same reason the other trailing seams are: the two direct-construction
    // Core test harnesses build <see cref="IdentityService"/> positionally and
    // omit it (a null store skips the post-commit byte-delete; the catalog-row
    // delete in the Marten session is unaffected). DI always supplies the live
    // <c>IMediaFileStore</c> (registered in DependencyInjection.cs), so the
    // delete lane runs the byte-delete in production.
    Kumunita.Core.Media.IMediaFileStore? mediaStore = null) : IIdentityService
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
        => await RegisterCoreAsync(displayName, email, password, requiresPassword: false);

    /// <inheritdoc />
    public async Task<ThinPrincipal> RegisterChildAccountAsync(string displayName, string email)
        => await RegisterCoreAsync(displayName, email, password: null, requiresPassword: true);

    /// <summary>
    /// Shared body of <see cref="RegisterAsync"/> (the self-serve lane, the credential
    /// set at signup) and <see cref="RegisterChildAccountAsync"/> (ADR 0146, the GU
    /// add-a-child lane, the credential **not** set — the child owns it, set at the
    /// verify link). <paramref name="password"/> null ⇒ the account is created
    /// password-less (the child lane; the credential is set later by
    /// <see cref="VerifyAndSetPasswordAsync"/>). <paramref name="requiresPassword"/>
    /// selects the verification email's body (the child body names "set your password"
    /// — <c>email.verify_child_body</c>).
    /// </summary>
    private async Task<ThinPrincipal> RegisterCoreAsync(
        string displayName, string email, string? password, bool requiresPassword)
    {
        // F9 (AUTH-15) — disposable-email blocklist (the instance's committed list,
        // `disposable-emails.txt`). Pre-write: no EF, no Marten, no audit row — the
        // actor does not yet exist (no `subjectId` to audit). Throws
        // `DisallowedEmailDomainException` (a dedicated type — the existing
        // `InvalidOperationException` catch in `AccountController.Signup` must NOT
        // swallow it). The structured log line is the audit lane for this rejection
        // (the `:184` "Registered unverified resident …" mirror, pin §3d).
        if (DisallowedEmailDomains.IsDisposable(email))
        {
            var blockedDomain = email[(email.LastIndexOf('@') + 1)..];
            logger.LogInformation(
                "Signup rejected: disposable-email domain {Domain} (email {Email}).",
                blockedDomain, email);
            throw new DisallowedEmailDomainException(email, blockedDomain);
        }

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
        // ADR 0146 — the credential is optional: the self-serve lane sets it at signup;
        // the child lane leaves the account password-less (the child sets it at the
        // verify link — the guardian never held it, the ADR 0028 "supervision rides the
        // link, not the password" shape).
        if (password is not null)
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
            displayName, verifyLink, preferredLanguage: null, requiresPassword: requiresPassword);
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
            // ADR 0146 — a password-less account (the child lane) gets the
            // "set your password" body; a credential-bearing account gets the
            // classic "confirm" body. The User object's PasswordHash is the
            // single-source read (no extra EF query needed).
            var requiresPassword = string.IsNullOrEmpty(user.PasswordHash);
            var (resendSubject, resendBody) = await BuildVerificationEmailAsync(
                resendName, resendLink, preferredLanguage: profile?.EmailLanguage,
                requiresPassword: requiresPassword);
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

    /// <inheritdoc />
    public async Task<Profile> VerifyAndSetPasswordAsync(string tokenValue, string password)
    {
        // ADR 0146 — the child-account activation handoff. The token is the
        // same single-use <c>KindVerify</c> row the child lane staged (the
        // <c>RegisterChildAccountAsync</c> shape), so the resolution is
        // identical to <see cref="VerifyWithTokenAsync"/>; the difference is
        // the credential write: the child sets their own password here
        // (the guardian never held it — the ADR 0028 "supervision rides the
        // link, not the password" shape). The credential write + the verified
        // flip + the token consumption + the audit row land in one commit
        // (the <c>CompleteSeedAdminSetupAsync</c> shape: a credential
        // write that also flips <c>Verified</c>).
        var token = await FindTokenAsync(
            tokenValue, kind: IdentityToken.KindVerify, subjectId: null);
        if (token is null)
            throw new InvalidOperationException("Verification token is invalid, already used, or expired.");

        var user = await userManager.FindByIdAsync(token.UserId)
            ?? throw new InvalidOperationException($"No account for verification token user {token.UserId}.");

        // The credential: set the child's own password (the ADR 0138
        // ChangePasswordAsync credential-write shape — a fresh hash, the
        // security stamp rotated so no stale session survives).
        var pwResult = await userManager.AddPasswordAsync(user, password);
        if (!pwResult.Succeeded)
            throw new InvalidOperationException(
                "Failed to set the account password: " +
                string.Join("; ", pwResult.Errors.Select(e => e.Description)));

        // Same session: flip verified, consume the token, append the audit row
        // (via: Owner — the resident activating their own account), one commit.
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

        // The credential write rotated nothing yet (a fresh account, no prior
        // sessions) — the stamp rotation is belt-and-braces, matching the
        // ChangePasswordAsync lane.
        await userManager.UpdateSecurityStampAsync(user);

        logger.LogInformation("Verified resident {UserId} (password set at the verify link).", token.UserId);
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
        // out of changing its own password?" rule lives, so the resident
        // /account/password surface and the ChangePasswordAsync enforcement
        // guard below agree. Four conjuncts (1)+(2) are this method's, (3)+(4)
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

        // 8c. F1 (DB-13 GDPR) — delete the target's resident-authored content
        //     docs in this SAME session / SAME commit (C3 single-write-lane,
        //     ADR 0006; the §2A closed list in
        //     docs/design/prod-hardening-f1-gdpr-deletion.md). Only the
        //     resident's own rows are removed (the listed owner field ==
        //     targetSubjectId); platform announcements (AuthorId == "") and
        //     every other resident's content are untouched.
        //
        //     Owner-field-less sub-entities travel with their parent and have
        //     no resident field to query by: KanbanLane / BoardItemPlacement
        //     are cascade-deleted off the KanbanBoard rows below (the exact
        //     ProjectService.DeleteBoardAsync cascade shape). GroupTranslation
        //     / CommunityTranslation / TranslationResource are NOT removed here
        //     — they sit under Group / Component parents this fix does not
        //     delete (§2C — those parents are out of scope), and
        //     TranslationResource is a global UI-string resource with no
        //     principal field at all. MediaObject rows + their bytes are U4's
        //     set — not touched here (only IdentityService.cs, this one commit).
        int contentDocs = 0;
        async Task<int> DeleteResidentRows<T>(
            System.Linq.Expressions.Expression<System.Func<T, bool>> predicate)
            where T : notnull
        {
            var rows = await session.Query<T>().Where(predicate)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var row in rows)
                session.Delete(row);
            return rows.Count;
        }

        // §2A primary set — one query per doc type, owner field == resident.
        contentDocs += await DeleteResidentRows<Kumunita.Core.Posts.Post>(p => p.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Posts.PostReply>(p => p.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Posts.PostTranslation>(p => p.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Posts.ReplyTranslation>(p => p.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Announcements.Announcement>(a => a.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Announcements.AnnouncementTranslation>(a => a.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Announcements.AnnouncementComment>(a => a.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Events.Event>(e => e.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Events.EventTranslation>(e => e.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Events.EventRsvp>(r => r.UserId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.Project>(p => p.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.ProjectGoal>(p => p.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.TodoItem>(t => t.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.TodoComment>(t => t.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.TodoTranslation>(t => t.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.BoardTranslation>(b => b.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.ProjectTranslation>(p => p.AuthorId == targetSubjectId);
        // KanbanBoard + its owner-field-less sub-entities (cascade — the
        // DeleteBoardAsync shape: the board's placements + lanes go first,
        // then the board itself). The to-dos themselves are deleted above by
        // their own AuthorId (a to-do is a standalone row, not a board child).
        {
            var boardIds = await session.Query<Kumunita.Core.Projects.KanbanBoard>()
                .Where(b => b.AuthorId == targetSubjectId)
                .Select(b => b.Id)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var boardId in boardIds)
            {
                var placements = await session.Query<Kumunita.Core.Projects.BoardItemPlacement>()
                    .Where(p => p.BoardId == boardId)
                    .ToListAsync(ct).ConfigureAwait(false);
                foreach (var placement in placements)
                    session.Delete(placement);

                var lanes = await session.Query<Kumunita.Core.Projects.KanbanLane>()
                    .Where(l => l.BoardId == boardId)
                    .ToListAsync(ct).ConfigureAwait(false);
                foreach (var lane in lanes)
                    session.Delete(lane);
            }
            contentDocs += await DeleteResidentRows<Kumunita.Core.Projects.KanbanBoard>(b => b.AuthorId == targetSubjectId);
        }
        contentDocs += await DeleteResidentRows<Kumunita.Core.Pages.Page>(p => p.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Pages.PageTranslation>(p => p.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Inventory.InventoryItem>(i => i.AuthorId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Documents.Document>(d => d.OwnerId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Documents.DocumentFolder>(d => d.OwnerId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Bookmarks.Bookmark>(b => b.OwnerId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Bookmarks.BookmarkFolder>(b => b.OwnerId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Tags.Tag>(t => t.CreatedBy == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Tags.TagTranslation>(t => t.AuthorId == targetSubjectId);
        // Conversation — the two-principal union (the resident holds a seat).
        contentDocs += await DeleteResidentRows<Kumunita.Core.Messaging.Conversation>(
            c => c.ParticipantA == targetSubjectId || c.ParticipantB == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.Messaging.Message>(m => m.SenderId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.PersonalNotes.PersonalNote>(n => n.OwnerId == targetSubjectId);
        contentDocs += await DeleteResidentRows<Kumunita.Core.PersonalNotes.NoteFolder>(n => n.OwnerId == targetSubjectId);

        // F1.U4 (DB-13 GDPR) — the resident's MediaObject catalog rows, in this
        // SAME session / SAME commit as the content-doc deletes above (C3
        // single-write-lane, ADR 0006; the §4 catalog pin in
        // docs/design/prod-hardening-f1-gdpr-deletion.md). The owner field is
        // <c>CreatedById</c> (the resident who first stored the unique file).
        // We collect each row's <c>Id</c> (the lowercase-hex SHA-256 content id
        // — the exact byte-store key for F1.U2's DeleteIfExistsAsync) into
        // <c>mediaIds</c> so the post-commit byte-delete loop below can run;
        // the catalog rows themselves are session-deleted here and committed by
        // the single SaveChangesAsync (step 9). Orphan policy (pinned §4,
        // owner-accepted): a dedup'd byte referenced by another resident's doc
        // is deleted anyway — the dangling ref degrades to a 404 (C-MED·7),
        // not a crash — so NO reference check is performed.
        var mediaRows = await session.Query<Kumunita.Core.Media.MediaObject>()
            .Where(o => o.CreatedById == targetSubjectId)
            .ToListAsync(ct).ConfigureAwait(false);
        var mediaIds = mediaRows.Select(o => o.Id).ToList();
        foreach (var mediaRow in mediaRows)
            session.Delete(mediaRow);
        contentDocs += mediaRows.Count;

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

        // 9b. F1.U4 (DB-13 GDPR) — delete the resident's media BYTES on the
        //     local volume, AFTER the Marten commit and BEFORE the EF Identity
        //     delete (the §5 ordering pin: Marten commit → byte deletes → EF
        //     Identity delete). These are filesystem ops, NOT in the Marten
        //     session — the catalog rows were committed in step 9's single
        //     SaveChangesAsync; only the bytes are removed here. Each
        //     <c>DeleteIfExistsAsync</c> (F1.U2) is idempotent and never throws
        //     for a missing file; we additionally swallow + log any unexpected
        //     failure (a byte-delete failure after the commit leaves a
        //     reclaimable orphan — orphan-safe per C-MED·7 — the §5 "accepted
        //     cross-store window" for this store) so the account deletion
        //     ALWAYS completes. Skipped entirely when the store seam was not
        //     supplied (the two direct-construction test harnesses), which
        //     leaves only the already-committed catalog-row deletes in place.
        var mediaDeleted = 0;
        if (mediaStore is not null)
        {
            foreach (var mediaId in mediaIds)
            {
                try
                {
                    if (await mediaStore.DeleteIfExistsAsync(mediaId, ct).ConfigureAwait(false))
                        mediaDeleted++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "F1.U4 — byte-delete failed for media id {MediaId} during account {Target} deletion; leaving a reclaimable orphan (orphan-safe per C-MED·7) and continuing.",
                        mediaId, targetSubjectId);
                }
            }
        }

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
            "Actor {Actor} (via {Via}) deleted account {Target}. {AuditRows} audit rows pseudonymized, {GroupMemberships} group memberships removed, {CompMemberships} component memberships removed, {ModAssignments} moderator assignments removed, {ContentDocs} resident content docs removed (incl. {MediaRows} MediaObject catalog rows), {MediaBytes} media bytes deleted.",
            actorId, via, targetSubjectId,
            auditRows.Count, groupMemberships.Count,
            compMemberships.Count, modAssignments.Count, contentDocs,
            mediaRows.Count, mediaDeleted);
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

    // ── Session kill-all (F5, AUTH-04) ──────────────────────────────────────

    /// <inheritdoc />
    public async Task KillAllSessionsAsync(string subjectId)
    {
        var user = await userManager.FindByIdAsync(subjectId)
            ?? throw new InvalidOperationException($"No account '{subjectId}'.");

        // Rotate the security stamp — the SAME seam ChangePasswordAsync uses at the
        // password-change lane (IdentityService.cs:1477). No migration: User already
        // carries the SecurityStamp column (inherited from IdentityUser, the InitialIdentity
        // migration). The gate that makes this rotation operative (RevocationStampMiddleware
        // in the Web) is the repo's actual invalidation idiom (ADR 0006-B "fat authorization,
        // per request"; the SecurityStampValidator is intentionally neutered by the 14-day
        // ValidationInterval — see Program.cs + PrivilegedStampMiddleware). The stamp
        // rotation is the server-side revocation mark; the Web gate reads it per-request
        // and signs out any session whose AuthenticationProperties.IssuedUtc (the
        // session's birth time, set by the cookie handler at sign-in and NOT refreshed by
        // sliding expiration) predates the stamp.
        await userManager.UpdateSecurityStampAsync(user);

        // Audit (C3 single-commit, same Marten session as the EF write — the same
        // shape ChangePasswordAsync uses for its audit row; ADR 0006 "audit-by-default").
        await using var session = documentStore.OpenSession(new SessionOptions());
        session.Store(AuditRow(DateTimeOffset.UtcNow, subjectId, subjectId,
            "session.kill_all", AccountKind, subjectId,
            Authorization.AccessVia.Owner,
            Authorization.AccessOutcome.Allow));
        await session.SaveChangesAsync();

        logger.LogInformation("Kill-all: security stamp rotated for {UserId}.", subjectId);
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

        // F2 — DUAL match (pin §5). The verify lanes present the STORED column value
        // (Web loads the row by Id and passes token.Token); the setup lane presents the
        // RAW env secret the operator typed. Match either so both work against a hashed
        // column, and legacy cleartext rows (presented as-is) still verify (pin §6).
        // CRITICAL: `Sha256Hex` is a .NET call — compute it as a LOCAL constant BEFORE
        // the query. Marten cannot translate SHA256.HashData into SQL. Do NOT put
        // Sha256Hex(...) inside the .Where(...) expression.
        var presented = tokenValue;
        var presentedHash = Sha256Hex(presented);   // local, precomputed — translatable as a constant

        var query = session
            .Query<IdentityToken>()
            .Where(t => t.Kind == kind
                && (t.Token == presented || t.Token == presentedHash)   // stored-value OR raw-secret
                && t.ConsumedAt == null
                && t.ExpiresAt > now);
        if (subjectId is not null)
            query = query.Where(t => t.UserId == subjectId);
        return await query.FirstOrDefaultAsync().ConfigureAwait(false);
    }

    private IdentityToken NewVerifyToken(string userId, DateTimeOffset now, int attempt = 1) => new()
{
        Id = Guid.NewGuid().ToString("N"),
        Kind = IdentityToken.KindVerify,
        UserId = userId,
        // F2 — AUTH-02: store the SHA-256 hex digest, never the raw secret at rest.
        // The raw NewSecret() is a throwaway: generated, hashed, discarded. The
        // high-entropy secret is never in a URL (the URL carries the row Id).
        Token = Sha256Hex(NewSecret()),
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

    /// <summary>
    /// F2 — AUTH-02: one-way hash of a high-entropy secret (pin §2). SHA-256 over the
    /// UTF-8 bytes, returned as exactly 64 lowercase hex chars. <c>internal</c> so the
    /// <see cref="Bootstrap.FirstBootSeeder"/> setup mint (pin §8.2) shares the SAME
    /// implementation rather than duplicating the hashing in a second file. Not a
    /// KDF (no salt/iterations): these tokens are 32 random bytes, so we need
    /// one-way-ness (a DB leak must not yield the token), not key-stretching.
    /// </summary>
    internal static string Sha256Hex(string value)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();   // exactly 64 lowercase hex chars
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
    /// ADR 0146 — the child-account verification body (the no-provider fallback):
    /// names the one remaining step — <b>setting your own password</b> — that the
    /// self-serve body does not (the child's guardian set up the account but never
    /// held the credential, so the child owns it from the start).
    /// </summary>
    private static string VerificationChildBody(string displayName, string verifyLink) =>
        $"Hi {displayName},\n\nYour Kumunita account is ready. Open this one-time link to " +
        $"confirm the account and set your password (it also signs you in):\n\n{verifyLink}\n\n" +
        "If you didn't create this account, you can ignore this message.";

    /// <summary>
    /// ADR 0061 — the verification email's subject + body, resolved through the
    /// <see cref="Kumunita.Core.Localization.ITranslationProvider"/> when one is
    /// available (per-recipient preferred language → instance default →
    /// <c>en</c> floor), with the English literals as the fallback for the two
    /// test sites that construct <see cref="IdentityService"/> directly without a
    /// provider. The body template's <c>{0}</c>/<c>{1}</c> placeholders are the
    /// resident's display name and the one-time verify link (ADR 0061 / the
    /// <c>email.verify_body</c> registry entry). ADR 0146 — <c>requiresPassword</c>
    /// selects the child body (<c>email.verify_child_body</c>) when the account has
    /// no password to set at the verify link (the child lane, the guardian never
    /// held the credential).
    /// </summary>
    private async Task<(string Subject, string Body)> BuildVerificationEmailAsync(
        string displayName, string verifyLink, string? preferredLanguage, bool requiresPassword = false)
    {
        string bodyKey = requiresPassword ? "email.verify_child_body" : "email.verify_body";
        string fallbackBody = requiresPassword
            ? VerificationChildBody(displayName, verifyLink)
            : VerificationBody(displayName, verifyLink);

        if (translationProvider is null)
            return ("Verify your Kumunita account", fallbackBody);

        string subject = await translationProvider.GetAsync("email.verify_subject", preferredLanguage);
        string bodyTemplate = await translationProvider.GetAsync(bodyKey, preferredLanguage);
        // A missing <c>email.verify_child_body</c> entry (a provider without the key)
        // would surface the raw key — fall back to the standard body rather than the
        // literal key.
        if (string.IsNullOrWhiteSpace(bodyTemplate) || bodyTemplate == bodyKey)
            bodyTemplate = requiresPassword ? fallbackBody : VerificationBody(displayName, verifyLink);
        string body = string.Format(bodyTemplate, displayName, verifyLink);
        return (subject, body);
    }
}


