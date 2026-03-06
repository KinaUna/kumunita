using Kumunita.Shared.Kernel.ValueObjects;
using Microsoft.AspNetCore.Identity;

namespace Kumunita.Identity.Domain;

public class AppRole : IdentityRole<Guid>
{
    // Localized display name — roles are shown in the UI
    public LocalizedContent DisplayName { get; private set; } = new();

    // System roles that cannot be deleted
    public bool IsSystem { get; init; }

    public static class SystemRoles
    {
        public const string Admin = "Admin";
        public const string Moderator = "Moderator";
        public const string Member = "Member";
        public const string Guest = "Guest";
    }
}