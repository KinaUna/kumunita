using Kumunita.Identity.Domain;
using Kumunita.Identity.Domain.Events;
using Kumunita.Identity.Exceptions;
using Kumunita.Shared.Kernel;
using Microsoft.AspNetCore.Identity;

namespace Kumunita.Identity.Features.Roles;

public record AssignRole(UserId TargetUserId, string RoleName, UserId AssignedBy);

public static class AssignRoleHandler
{
    public static async Task<RoleAssigned> Handle(
        AssignRole cmd,
        UserManager<AppUser> userManager,
        CancellationToken ct)
    {
        AppUser? appUser = await userManager.FindByIdAsync(
            cmd.TargetUserId.Value.ToString());

        if (appUser is null)
            throw new UserNotFoundException(cmd.TargetUserId);

        IdentityResult result = await userManager.AddToRoleAsync(appUser, cmd.RoleName);
        if (!result.Succeeded)
            throw new RoleAssignmentException(result.Errors);

        return new RoleAssigned(cmd.TargetUserId, cmd.RoleName, cmd.AssignedBy);
    }
}