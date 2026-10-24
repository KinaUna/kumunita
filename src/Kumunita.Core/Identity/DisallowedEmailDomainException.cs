namespace Kumunita.Core.Identity;

/// <summary>
/// F9 (AUTH-15) — thrown by <see cref="IdentityService.RegisterCoreAsync"/> (and therefore
/// by <see cref="IIdentityService.RegisterAsync"/> and
/// <see cref="IIdentityService.RegisterChildAccountAsync"/>) when the requested email's
/// domain is in the instance's disposable-email blocklist (<c>disposable-emails.txt</c>).
/// No writes have happened — the rejection is pre-<c>FindByEmailAsync</c>, pre-EF,
/// pre-Marten (the actor does not yet exist).
/// <para>
/// A **dedicated type** (the F2 precedent — a dedicated type for a dedicated domain rule),
/// so the Web catches this type specifically (NOT the existing
/// <see cref="InvalidOperationException"/> catch in
/// <c>AccountController.Signup</c>, which would swallow it). The Web re-renders the signup
/// form with the <c>SignupViewModel.EmailDomainDisallowed</c> flag (the
/// <c>EmailAlreadyExists</c> precedent) and shows the localized "please use a permanent
/// email" notice. The <see cref="Email"/> / <see cref="Domain"/> properties give the Web
/// the data to build that message without parsing the exception string; the base message
/// is the machine-readable + log shape.
/// </para>
/// </summary>
public sealed class DisallowedEmailDomainException : Exception
{
    /// <summary>The full email address that was rejected.</summary>
    public string Email { get; }

    /// <summary>The full domain (after the <c>@</c>) that was in the blocklist.</summary>
    public string Domain { get; }

    public DisallowedEmailDomainException(string email, string domain)
        : base($"The email domain '{domain}' is not accepted for new accounts. " +
               "Please use a permanent email address.")
    {
        Email = email;
        Domain = domain;
    }
}
