using System.Security.Cryptography;
using Kumunita.Core.Identity;
using Microsoft.AspNetCore.Identity;

namespace Kumunita.Core.Portability;

/// <summary>
/// The §apply step 1 — the **identity re-creation** (D3 / C-M11·2 import
/// boundary): for every row in the archive's <c>identity/principals.json</c>,
/// re-create the Identity principal **keyed by the exported <c>subjectId</c>**,
/// **reset** the secrets (a fresh, non-portable password + a fresh security
/// stamp — the C-M11·2 import boundary), and **re-apply the roles**.
/// <para>
/// <b>The C-M11·2 boundary on the import side:</b> the archive carries the
/// no-secret graph (the eight allowed §principals fields — the
/// <see cref="PortabilityPrincipal"/> POCO has <em>no</em> credential field).
/// There is no <c>PasswordHash</c> / <c>SecurityStamp</c> / token to
/// restore — the secrets are <em>reset</em>, never replayed (the resident
/// re-authenticates). This unit is where that boundary is enforced on the
/// import side (the U07 no-secret test witnesses it end-to-end).
/// </para>
/// <para>
/// <b>The D1 restore semantic:</b> the import applies into a **fresh**
/// instance (the current instance's state is the operator's pre-import
/// backup, the rollback path). A "principal already exists" collision is a
/// **validate-phase** failure (C-M11·4) — a fresh instance has no rows, so
/// this unit's <see cref="UserManager{TUser}.CreateAsync(TUser, string)"/>
/// runs against a clean identity store (fail-open on the pre-validated
/// shape; a mid-apply failure is the documented restore path, never a
/// silently-accepted half-import).
/// </para>
/// <para>
/// The <c>displayName</c> / <c>verified</c> / <c>blocked</c> fields are the
/// <c>Profile</c> doc's state — the <c>Profile</c> travels as a content doc
/// (order 5 in the §inventory) and is re-materialized by <see
/// cref="PortabilityApplyDocuments.ApplyAsync"/> (step 2); this unit is the
/// <b>Identity <c>User</c> + the role standing</b> only.
/// </para>
/// </summary>
public static class PortabilityApplyIdentity
{
    /// <summary>
    /// Re-creates every exported principal (the eight allowed §principals
    /// fields the U06 re-creator mirrors verbatim) keyed by the exported
    /// <c>subjectId</c>, resets the secrets (a fresh non-portable password
    /// + a fresh security stamp — the C-M11·2 import boundary), and
    /// re-applies the roles.
    /// </summary>
    /// <param name="userManager">The frozen Identity seam (the principal
    /// store — <see cref="UserManager{TUser}.CreateAsync(TUser, string)"/> +
    /// <see cref="UserManager{TUser}.AddToRoleAsync(TUser, string)"/> +
    /// <see cref="UserManager{TUser}.UpdateSecurityStampAsync(TUser)"/>).</param>
    /// <param name="roleManager">The frozen role seam (the role-row source —
    /// <see cref="RoleManager{TRole}.FindByNameAsync(string)"/>; ensures the
    /// elevated role rows exist before <c>AddToRoleAsync</c>).</param>
    /// <param name="principals">The <c>identity/principals.json</c> rows —
    /// the eight allowed fields each, the no-secret set (the credential
    /// columns are structurally absent, C-M11·2).</param>
    /// <param name="ct">Cancellation.</param>
    public static async Task ApplyAsync(
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        IReadOnlyList<PortabilityPrincipal> principals,
        CancellationToken ct)
    {
        foreach (var principal in principals)
        {
            ct.ThrowIfCancellationRequested();

            // The eight allowed §principals fields — the C-M11·2 boundary:
            // no PasswordHash / SecurityStamp / AccessToken / RefreshToken /
            // RecoveryCode is ever read (there is none in the archive, by
            // C-M11·2 — the secrets are reset, never replayed).
            //
            // The D1 restore semantic: a fresh instance — the principal
            // does not pre-exist (a collision is a validate-phase failure,
            // C-M11·4). Create keyed by the exported subjectId (the stable
            // id the rest of the graph references — the §principals "key"
            // column).
            var user = new User
            {
                Id = principal.SubjectId,
                Email = principal.Email,
                NormalizedEmail = principal.NormalizedEmail,
                UserName = string.IsNullOrEmpty(principal.Username)
                    ? principal.Email
                    : principal.Username,
            };

            // The secret reset (the C-M11·2 import boundary — the single most
            // load-bearing M11 invariant enforced on the import side): a
            // fresh, non-portable password (the resident re-authenticates;
            // the operator distributes the reset credential) + a fresh
            // security stamp (a fresh identity, no replayed secret). Create
            // with the generated password (valid against the app's Identity
            // policy); the stamp rotation is the belt-and-braces for "the
            // account is brand new" (the IdentityService
            // CompleteSeedAdminSetupAsync / ResetPasswordAsync idiom).
            var freshPassword = RandomPassword();
            var result = await userManager.CreateAsync(user, freshPassword).ConfigureAwait(false);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to re-create principal '{principal.SubjectId}': " +
                    string.Join(", ", result.Errors.Select(e => e.Description)));

            // The role re-apply — the standing (Member / Moderator /
            // GlobalAdmin / Translator, ADR 0030 composable). The elevated
            // role rows are already created by the first-boot seeder; ensure
            // existence so the re-creator is self-contained (the SampleData
            // EnsureUserAsync shape).
            foreach (var role in principal.Roles)
            {
                if (string.IsNullOrEmpty(role))
                    continue;
                if (await roleManager.FindByNameAsync(role) is null)
                    await roleManager.CreateAsync(new IdentityRole(role));
                await userManager.AddToRoleAsync(user, role);
            }

            // The fresh security stamp — a fresh identity (no replayed
            // secret; the C-M11·2 import boundary).
            await userManager.UpdateSecurityStampAsync(user);
        }
    }

    /// <summary>
    /// A random 32-character high-entropy password for a re-created account.
    /// CSPRNG-backed (<see cref="RandomNumberGenerator"/>) over the full
    /// alphanumeric alphabet, guaranteed to contain at least one uppercase,
    /// one lowercase, and one digit — the app's Identity password policy
    /// requires all three (the non-alphanumeric requirement is relaxed by
    /// <c>Program.cs</c>). The <c>SampleDataSeeder.RandomPassword</c>
    /// idiom, reused verbatim (a fresh non-portable credential, C-M11·2).
    /// </summary>
    private static string RandomPassword()
    {
        const int length = 32;
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digit = "0123456789";
        const string alphabet = upper + lower + digit;

        var chars = new char[length];
        chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        chars[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        chars[2] = digit[RandomNumberGenerator.GetInt32(digit.Length)];
        for (var i = 3; i < length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }
}
