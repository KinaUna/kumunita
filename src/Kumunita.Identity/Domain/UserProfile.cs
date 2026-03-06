using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Domain;
using Kumunita.Shared.Kernel.ValueObjects;
using System.Net;

namespace Kumunita.Identity.Domain;

public class UserProfile : IAuditableEntity
{
    // Identity — matches AppUser.DomainId
    public UserId Id { get; private set; }

    // Personal data
    public string DisplayName { get; private set; } = string.Empty;
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? Bio { get; private set; }

    // Localization preference — flows into token claims
    public LanguageCode PreferredLanguage { get; private set; }
        = LanguageCode.English;

    // Contact details — visibility controlled by Authorization module
    public string? PhoneNumber { get; private set; }
    public string? AlternativeEmail { get; private set; }

    // Address — visibility controlled by Authorization module
    public UserProfileAddress? Address { get; private set; }

    // Audit
    public DateTimeOffset CreatedAt { get; private set; }
        = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; }
        = DateTimeOffset.UtcNow;

    // Required by Marten
    private UserProfile() { }

    public static UserProfile Create(
        UserId id,
        string displayName,
        LanguageCode preferredLanguage)
    {
        return new UserProfile
        {
            Id = id,
            DisplayName = displayName,
            PreferredLanguage = preferredLanguage
        };
    }

    public void Update(
        string? displayName,
        string? firstName,
        string? lastName,
        string? bio,
        LanguageCode? preferredLanguage)
    {
        if (displayName is not null) DisplayName = displayName;
        if (firstName is not null) FirstName = firstName;
        if (lastName is not null) LastName = lastName;
        if (bio is not null) Bio = bio;
        if (preferredLanguage is not null) PreferredLanguage = preferredLanguage.Value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateContactDetails(
        string? phoneNumber,
        string? alternativeEmail)
    {
        PhoneNumber = phoneNumber;
        AlternativeEmail = alternativeEmail;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateAddress(UserProfileAddress? address)
    {
        Address = address;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}