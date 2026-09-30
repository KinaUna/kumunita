using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// M19 · U02 — the <see cref="Roles.Guest"/> claim mint at the Identity↔cookie
/// seam (ADR 0120, D5). Pins GATE-1 + GATE-4:
/// <list type="bullet">
/// <item><b>GATE-1 / C-M19·2·3</b> — a live guest (window live, <c>IsGuest =
/// true</c>, not blocked, not verified) mints <see cref="Roles.Guest"/> and
/// **not** <see cref="Roles.Member"/>; a guest outside the window mints **no**
/// <see cref="Roles.Guest"/> (no standing).</item>
/// <item><b>GATE-4 / C-M19·6</b> — the <see cref="ClaimTypes.All"/> set is
/// unchanged (the four frozen claim *types*), and the only new role *string*
/// minted for a guest is <c>"Guest"</c> — a role value on the existing
/// <see cref="ClaimTypes.Role"/> claim type, not a new claim type.</item>
/// </list>
/// The factory is the single place the admissible claim set is minted (ADR
/// 0006-B); the guest branch is independent of the <c>Verified → Member</c>
/// branch and reads the window only when <c>Profile.IsGuest == true</c>
/// (non-guests short-circuit — zero extra mt read on the resident path).
/// </summary>
public class GuestClaimMintTests
{
    private const string GuestSubject = "guest-001";

    /// <summary>The frozen role-string baseline (pre-M19). The claim-shape pin
    /// (C-M19·6) asserts the only role string minted for a live guest that is
    /// *not* in this set is <see cref="Roles.Guest"/> — a role *value*, not a
    /// new claim *type*.</summary>
    private static readonly string[] FrozenRoleStrings =
        [Roles.Member, Roles.Moderator, Roles.GlobalAdmin, Roles.Translator];

    /// <summary>Build a <see cref="KumunitaClaimsPrincipalFactory"/> wired to
    /// NSubstitute-only seams: <see cref="IUserInfoService.GetProfileAsync"/>
    /// returns the given <paramref name="profile"/>, and
    /// <see cref="IIdentityService.GetGuestAccessAsync"/> returns the given
    /// <paramref name="guestAccess"/> (U01's read seam — the mint rides it).
    /// The account has no explicit Identity roles (so the only roles that can
    /// appear are the seam-minted <c>Member</c>/<c>Guest</c>).</summary>
    private static KumunitaClaimsPrincipalFactory BuildFactory(
        Profile profile,
        GuestAccess? guestAccess,
        out IIdentityService identity)
    {
        // UserManager<T>/RoleManager<T> have no parameterless constructor (so
        // they can't be NSubstituted directly) — back a *real* instance with a
        // substituted store interface. The real UserManager.GetRolesAsync
        // delegates to IUserRoleStore.GetRolesAsync (stubbed to empty), so the
        // factory mints only the seam-driven Member/Guest roles.
        var userStore = Substitute.For<IUserRoleStore<User>>();
        userStore.GetRolesAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(new List<string>());

        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            EmptyServiceProvider.Instance,
            NullLogger<UserManager<User>>.Instance);

        var roleManager = new RoleManager<IdentityRole>(
            Substitute.For<IRoleStore<IdentityRole>>(),
            new[] { new RoleValidator<IdentityRole>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(GuestSubject).Returns(profile);

        identity = Substitute.For<IIdentityService>();
        identity.GetGuestAccessAsync(GuestSubject).Returns(guestAccess);

        return new KumunitaClaimsPrincipalFactory(
            userManager,
            roleManager,
            userInfo,
            identity,
            Options.Create(new IdentityOptions()));
    }

    private static User BuildUser() => new()
    {
        Id = GuestSubject,
        UserName = "guest@example.com",
        Email = "guest@example.com",
    };

    private static List<string> RoleValues(System.Security.Claims.ClaimsPrincipal principal) =>
        principal.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

    // ── GATE-1 (C-M19·2 / C-M19·3) ───────────────────────────────────────

    [Fact]
    public async Task Guest_Within_Window_Mints_Guest_Not_Member()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = new Profile
        {
            SubjectId = GuestSubject,
            IsGuest = true,
            Verified = false,   // a guest is NOT a verified resident (C-M19·2)
            Blocked = false,
        };
        var guestAccess = new GuestAccess
        {
            SubjectId = GuestSubject,
            ValidFrom = now.AddHours(-1),   // window live: now >= ValidFrom
            ValidUntil = now.AddHours(1),   // and now < ValidUntil
        };

        var factory = BuildFactory(profile, guestAccess, out var identity);

        var principal = await factory.CreateAsync(BuildUser());
        var roles = RoleValues(principal);

        // The live guest mints the Guest standing …
        Assert.Contains(Roles.Guest, roles);
        // … and never the Member standing (C-M19·2 — the guest is outside the
        //   resident circle).
        Assert.DoesNotContain(Roles.Member, roles);

        // The window read is exactly one, and only on the guest path (the mint
        // rides U01's read seam).
        await identity.Received(1).GetGuestAccessAsync(GuestSubject);
    }

    [Fact]
    public async Task Guest_Outside_Window_Mints_No_Guest()
    {
        var now = DateTimeOffset.UtcNow;
        var profile = new Profile
        {
            SubjectId = GuestSubject,
            IsGuest = true,
            Verified = false,
            Blocked = false,
        };
        // Window lapsed: now >= ValidUntil → the guest has no standing
        // (C-M19·3 — the window is a live read, not a job).
        var guestAccess = new GuestAccess
        {
            SubjectId = GuestSubject,
            ValidFrom = now.AddHours(-2),
            ValidUntil = now.AddHours(-1),   // expired
        };

        var factory = BuildFactory(profile, guestAccess, out var identity);

        var principal = await factory.CreateAsync(BuildUser());
        var roles = RoleValues(principal);

        // An expired guest mints NO Guest claim (no standing) …
        Assert.DoesNotContain(Roles.Guest, roles);
        // … and no Member either (C-M19·2).
        Assert.DoesNotContain(Roles.Member, roles);
        // … and (with no Identity roles) has no standing at all.
        Assert.Empty(roles);

        // The window was consulted (the branch short-circuits only for
        // non-guests — this one is a guest, so the read runs).
        await identity.Received(1).GetGuestAccessAsync(GuestSubject);
    }

    // ── GATE-4 (C-M19·6) ─────────────────────────────────────────────────

    [Fact]
    public async Task Claim_Shape_Pin_Only_New_Claim_String_Is_Guest()
    {
        // C-M19·6 / D5 — the claim-shape pin. The frozen claim *type* set is
        // exactly the four original types; a guest adds a new role *string*
        // (a value on the existing Kumunita.Role type), not a new claim *type*.
        Assert.Equal(4, ClaimTypes.All.Count);
        Assert.Contains(ClaimTypes.Subject, ClaimTypes.All);
        Assert.Contains(ClaimTypes.ExternalId, ClaimTypes.All);
        Assert.Contains(ClaimTypes.Verified, ClaimTypes.All);
        Assert.Contains(ClaimTypes.Role, ClaimTypes.All);
        // "Guest" is a role *value*, not a claim *type* — it must not be in
        // the admissible claim-type set.
        Assert.DoesNotContain(Roles.Guest, ClaimTypes.All);

        // A live guest mints the Guest role …
        var now = DateTimeOffset.UtcNow;
        var profile = new Profile
        {
            SubjectId = GuestSubject,
            IsGuest = true,
            Verified = false,
            Blocked = false,
        };
        var guestAccess = new GuestAccess
        {
            SubjectId = GuestSubject,
            ValidFrom = now.AddHours(-1),
            ValidUntil = now.AddHours(1),
        };

        var factory = BuildFactory(profile, guestAccess, out _);
        var principal = await factory.CreateAsync(BuildUser());

        // … and the only role string minted for this guest is "Guest" (the
        //   account carries no other Identity roles in the test, so the full
        //   role-string set is exactly ["Guest"]).
        var roles = RoleValues(principal);
        Assert.Equal([Roles.Guest], roles);

        // Every minted role is either a pre-M19 frozen role or the one new
        // guest string — the "only new role string is Guest" guarantee.
        foreach (var role in roles)
            Assert.True(
                FrozenRoleStrings.Contains(role) || role == Roles.Guest,
                $"unexpected role string minted for a guest: {role}");

        // The Guest claim rides the existing Kumunita.Role claim type (not a
        // new claim type) — the no-relational-data invariant holds.
        Assert.Contains(
            principal.Claims,
            c => c.Type == ClaimTypes.Role && c.Value == Roles.Guest);
    }

    /// <summary>The no-op <see cref="IServiceProvider"/> the
    /// <see cref="UserManager{TUser}"/> constructor demands (the factory never
    /// resolves from it — the only seams exercised are the substituted ones).
    /// Mirrors the private helper of the same name in
    /// <c>Kumunita.Core.Tests</c>.</summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();
        public object? GetService(Type serviceType) => null;
    }
}
