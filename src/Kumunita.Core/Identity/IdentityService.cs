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
    Microsoft.Extensions.Logging.ILogger<IdentityService> logger) : IIdentityService
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
        await mailer.StageAsync(session,
                    idempotencyKey: $"verify:{user.Id}:1",
                    recipient: email,
                    subject: "Verify your Kumunita account",
                    body: VerificationBody(displayName, token.Id),
                    ct: default);
        await session.SaveChangesAsync();

        logger.LogInformation("Registered unverified resident {UserId} (email {Email}).", user.Id, email);
        return new ThinPrincipal(user.Id, user.ExternalId, IsVerifiedResident: false, ThinPrincipal.NoRoles);
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
    public async Task SetRoleAsync(string targetSubjectId, string adminSubjectId, string role,
        IReadOnlyList<string>? componentIds)
    {
        var admin = await RequireGlobalAdminAsync(adminSubjectId);
        _ = admin;
        var target = await userManager.FindByIdAsync(targetSubjectId)
            ?? throw new InvalidOperationException($"No account '{targetSubjectId}'.");

        var targetRoles = (await userManager.GetRolesAsync(target)).ToList();
        var wantsGlobalAdmin = role == Roles.GlobalAdmin;
        var wantsModerator = role == Roles.Moderator;

        bool rolesChanged =
            (wantsGlobalAdmin && !targetRoles.Contains(Roles.GlobalAdmin)) ||
            (!wantsGlobalAdmin && targetRoles.Contains(Roles.GlobalAdmin)) ||
            (wantsModerator && !targetRoles.Contains(Roles.Moderator)) ||
            (!wantsModerator && targetRoles.Contains(Roles.Moderator));

        // Apply the GlobalAdmin/Moderator identity roles (Member is the implicit verified
        // standing — no EF role for it). AddTo/RemoveFromRole manage the role membership;
        // the role row itself is created by the host's seed (FirstBootSeeder) when the
        // account is granted the role for the first time.
        if (wantsGlobalAdmin)  await userManager.AddToRoleAsync(target, Roles.GlobalAdmin);
        else                   await userManager.RemoveFromRoleAsync(target, Roles.GlobalAdmin);
        if (wantsModerator)    await userManager.AddToRoleAsync(target, Roles.Moderator);
        else                   await userManager.RemoveFromRoleAsync(target, Roles.Moderator);

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

        logger.LogInformation("Admin {Admin} set {Target}'s role to {Role}/{Components}.",
            adminSubjectId, targetSubjectId, role, string.Join(",", componentIds ?? []));
    }

    // ── Password (self-serve or admin reset) ───────────────────────────────

    /// <inheritdoc />
    public async Task ChangePasswordAsync(string subjectId, string newPassword, bool byAdmin)
    {
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

    private IdentityToken NewVerifyToken(string userId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Kind = IdentityToken.KindVerify,
        UserId = userId,
        Token = NewSecret(),   // high-entropy; never in a URL (the URL carries the row Id)
        Attempt = 1,
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

    private static string VerificationBody(string displayName, string tokenRowId) =>
        $"Hi {displayName},\n\nYour Kumunita account is set to verify on its first sign-in. " +
        $"This one-time link (row id {tokenRowId}) returns you to the platform to confirm the account.\n\n" +
        "If you didn't create this account, you can ignore this message.";
}


