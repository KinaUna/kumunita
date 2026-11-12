using Kumunita.Core.Logging;
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
public partial class IdentityService(
    UserManager<User> userManager,
    Marten.IDocumentStore documentStore,
    IUserInfoService userInfo,
    IClaimsSource claimsSource,
    IMailerStage mailer,
    IOptions<VerificationOptions> verificationOptions,
    Microsoft.Extensions.Logging.ILogger<IdentityService> logger,
    Kumunita.Core.Localization.ITranslationProvider? translationProvider = null,
    INotificationEmitter? notifications = null,
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
    Kumunita.Core.Media.IMediaFileStore? mediaStore = null,
    // C07 (ADR 0286) — the IProfileSettingsService sibling (the moved
    // GetAssignmentsAsync owner; the access core's IUserInfoService stays for the
    // GetProfileAsync calls). Optional (default null, the CS1736 shape) so the
    // direct-construction test harnesses that build IdentityService positionally
    // keep compiling unchanged; DI always supplies the live sibling.
    IProfileSettingsService? profileSettings = null) : IIdentityService
{
    // C3-03 (ADR 0293) — the platform-gate seam (the C08 test-compat facade shape:
    // the 12 IIdentityService gate methods are thin forwarders to it; the 2 private sample-account
    // helpers moved with the cluster; the existing IIdentityService mocks keep compiling).
    private readonly IdentityPlatformGateService _gates = new(documentStore, sampleDataOptions, userInfo, userManager);

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
            // C07 (ADR 0286) — GetAssignmentsAsync moved to IProfileSettingsService;
            // the optional seam is null only in the direct-construction test
            // harnesses, which never set up per-component moderator scoping
            // (a null reader → an empty list, the same "not configured" floor as
            // the other optional seams).
            var assignments = profileSettings is not null
                ? await profileSettings.GetAssignmentsAsync(subjectId)
                : Array.Empty<ModeratorAssignment>();
            foreach (var a in assignments)
                roles.Add(Roles.ModeratorComponent(a.ComponentId));
        }

        return new ThinPrincipal(user.Id ?? subjectId, user.ExternalId, verified, roles);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListGlobalAdminsAsync()
    {
        // ADR 0303 (IMPROVE-UX A05) — the GlobalAdmin enumeration read seam.
        // The GetUsersInRoleAsync shape (the EmitAccountNotificationAsync
        // precedent, ADR 0077), userId projection only (no PII), null-safe
        // (an account with no Id is skipped — the ADR 0077 fan-out guard).
        var admins = await userManager
            .GetUsersInRoleAsync(Roles.GlobalAdmin).ConfigureAwait(false);
        var ids = new List<string>(admins.Count);
        foreach (var a in admins)
        {
            var id = a?.Id ?? string.Empty;
            if (id.Length > 0)
                ids.Add(id);
        }
        return ids;
    }

    /// <inheritdoc />
    public async Task RequestStorageQuotaIncreaseAsync(string subjectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);

        // IMPROVE-UX A14 — the "request more space" notify is **best-effort**
        // (the ADR 0077 D5 posture, the <c>EmitAccountNotificationAsync</c>
        // shape): the request "was made" is the durable fact (the resident's
        // confirmed action, the Web's flash), and a missing emitter, a fresh
        // instance with no GlobalAdmin, or an emit error is swallowed + logged
        // — the request never fails because the notification lane is
        // unavailable. No audit row (a notify is not an access decision — the
        // A12 <see cref="IInvitationService.ApproveAsync"/> notify precedent).
        // The ADR 0077 <c>NotifyAdminsOnSignup</c> gate does NOT apply: that
        // flag gates the signup-lane notify; this is a distinct loop (the
        // storage-request nudge), so it is always on for a GlobalAdmin.
        try
        {
            if (notifications is null)
                return;                                          // not wired (a test harness) — the request still stands

            // The GlobalAdmin enumeration read (the <c>EmitAccountNotificationAsync</c>
            // <c>GetUsersInRoleAsync</c> shape) — a no-op for a fresh instance
            // (nothing to notify, not a noise path).
            var admins = (await userManager
                .GetUsersInRoleAsync(Roles.GlobalAdmin).ConfigureAwait(false)).ToList();
            if (admins.Count == 0)
                return;                                          // no GlobalAdmin (a fresh instance) — nothing to notify

            // The body carries the requesting resident (so the admin knows
            // *who* is asking) — the display name, then the email, then the
            // raw id as the floor (a profileless account still identifies
            // itself). Resolved through <see cref="IUserInfoService"/>
            // (the ADR 0006 §D seam — the Identity module owns the read).
            var profile = await userInfo.GetProfileAsync(subjectId).ConfigureAwait(false);
            var who = !string.IsNullOrWhiteSpace(profile?.DisplayName) ? profile!.DisplayName
                    : !string.IsNullOrWhiteSpace(profile?.Email) ? profile!.Email!
                    : subjectId;

            // F10 — a stable, content-derived key per recipient: a repeat
            // request by the same resident does not re-flood the same admin
            // (EmitAsync dedups by key alone — the recipient is a parameter,
            // so the admin id is part of the key; the ADR 0077 per-admin
            // fan-out shape).
            await using var session = documentStore.OpenSession(new SessionOptions());
            foreach (var admin in admins)
            {
                var adminId = admin?.Id ?? string.Empty;
                if (adminId.Length == 0)
                    continue;
                await notifications.EmitAsync(
                    session,
                    recipientId: adminId,
                    kind: Notifications.NotificationKinds.StorageRequest,
                    idempotencyKey: $"notification:{Notifications.NotificationKinds.StorageRequest}:{subjectId}:{adminId}",
                    body: who,
                    linkPath: $"/admin/accounts/{subjectId}",
                    ct: ct).ConfigureAwait(false);
            }
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Best-effort (D5): the request must not fail because the
            // notification lane is unavailable — log and let the resident's
            // confirmed action stand. The inbox row is the nudge, not the
            // record.
            logger.LogWarning(ex,
                "IMPROVE-UX A14 — the storage-quota request for {SubjectId} was not notified to the GlobalAdmins.",
                subjectId);
        }
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
                "Signup rejected: disposable-email domain {Domain} (email {EmailDomain}).",
                blockedDomain, LogRedact.EmailDomain(email));
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

        logger.LogInformation("Registered unverified resident {UserId} (email {EmailDomain}).", user.Id, LogRedact.EmailDomain(email));
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
                "Re-issued verification (attempt {Attempt}) for resident {UserId} (email {EmailDomain}).",
                nextAttempt, user.Id, LogRedact.EmailDomain(email));
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

    /// <summary>
    /// C3-05 (ADR 0295) — by-id read of a single <see cref="IdentityToken"/>
    /// row (the <c>AccountController</c> verify-link read, moved here so the
    /// controller retires its own Marten session). Pure read: the caller owns
    /// the <c>Kind</c> + <c>IsUsableAt</c> usability decision. Returns
    /// <c>null</c> for a missing id.
    /// </summary>
    public async Task<IdentityToken?> LoadVerifyTokenAsync(string tokenId)
    {
        await using var session = documentStore.OpenSession(new SessionOptions());
        return await session.LoadAsync<IdentityToken>(tokenId).ConfigureAwait(false);
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

    /// <summary>
    /// The single <c>AccessAudit</c> row shape the whole <c>Identity</c>
    /// context writes (the "one summary" shape, the ADR 0142 D5 / 0301 D1
    /// precedent). <c>internal</c> so the ADR 0301 (IMPROVE-UX R11)
    /// <see cref="DeletionScheduler"/> sibling shares the SAME implementation
    /// rather than duplicating the field mapping in a second file (the
    /// <see cref="Sha256Hex"/> "internal so the sibling shares it" precedent).
    /// </summary>
    internal static Authorization.AccessAudit AuditRow(
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


