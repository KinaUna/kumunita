using Kumunita.Shared.Kernel;
using Microsoft.AspNetCore.Identity;

namespace Kumunita.Identity.Domain;

public class AppUser : IdentityUser<Guid>
{
    // Strongly typed UserId bridges EF Core and the rest of the system
    public UserId DomainId => new(Id);

    // The only domain concern allowed on AppUser —
    // needed to locate the Marten UserProfile document
    public DateTimeOffset CreatedAt { get; private set; }
        = DateTimeOffset.UtcNow;

    public bool IsSuspended { get; private set; }

    public void Suspend() => IsSuspended = true;
    public void Reactivate() => IsSuspended = false;
}