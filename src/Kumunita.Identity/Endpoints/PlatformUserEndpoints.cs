using Kumunita.Identity.Contracts.Queries;
using Kumunita.Identity.Domain;
using Kumunita.Identity.Domain.Events;
using Kumunita.Identity.Exceptions;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Auth;
using Kumunita.Shared.Kernel.ValueObjects;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Wolverine.Http;

namespace Kumunita.Identity.Endpoints;

public record CreatePlatformUserCommand(string Email, string TemporaryPassword, string DisplayName);
public record CreatePlatformUserResult(Guid UserId);
public record SuspendUserCommand(string? Reason);
public record AssignPlatformRoleCommand(string RoleName);

public static class PlatformUserEndpoints
{
    [WolverinePost("/platform/users")]
    [Authorize(Policy = "PlatformAdmin")]
    public static async Task<(IResult, UserRegistered)> CreateUser(
        CreatePlatformUserCommand command,
        UserManager<AppUser> userManager,
        IDocumentSession session,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        AppUser appUser = new()
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            UserName = command.Email,
            MustChangePassword = true
        };

        IdentityResult result = await userManager.CreateAsync(appUser, command.TemporaryPassword);
        if (!result.Succeeded)
            throw new RegistrationException(result.Errors);

        await userManager.AddToRoleAsync(appUser, AppRole.SystemRoles.Member);

        UserId userId = appUser.DomainId;
        UserId adminId = user.GetUserId();

        UserProfile profile = UserProfile.Create(userId, command.DisplayName, LanguageCode.English);
        session.Store(profile);

        DirectoryEntry directory = DirectoryEntry.Create(userId, command.DisplayName, adminId);
        session.Store(directory);

        return (
            Results.Created($"/platform/users/{appUser.Id}", new CreatePlatformUserResult(appUser.Id)),
            new UserRegistered(userId, command.Email, command.DisplayName, LanguageCode.English));
    }

    [WolverineGet("/platform/users")]
    [Authorize(Policy = "PlatformAdmin")]
    public static async Task<IResult> GetAllUsers(
        UserManager<AppUser> userManager,
        CancellationToken ct)
    {
        List<AppUser> users = await EntityFrameworkQueryableExtensions.ToListAsync(
            userManager.Users.OrderBy(u => u.CreatedAt), ct);

        List<PlatformUserRow> rows = [];
        foreach (AppUser appUser in users)
        {
            IList<string> roles = await userManager.GetRolesAsync(appUser);
            rows.Add(new PlatformUserRow(
                appUser.Id,
                appUser.Email!,
                appUser.IsSuspended,
                appUser.MustChangePassword,
                roles,
                appUser.CreatedAt));
        }

        return Results.Ok(rows);
    }

    [WolverinePut("/platform/users/{userId:guid}/suspension")]
    [Authorize(Policy = "PlatformAdmin")]
    public static async Task<(IResult, object)> ToggleSuspension(
        Guid userId,
        SuspendUserCommand command,
        UserManager<AppUser> userManager,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        AppUser appUser = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new UserNotFoundException(new UserId(userId));

        UserId adminId = user.GetUserId();

        if (appUser.IsSuspended)
        {
            appUser.Reactivate();
            await userManager.UpdateAsync(appUser);
            return (Results.Ok(new { IsSuspended = false }), new AccountReactivated(appUser.DomainId, adminId));
        }
        else
        {
            appUser.Suspend();
            await userManager.UpdateAsync(appUser);
            return (Results.Ok(new { IsSuspended = true }), new AccountSuspended(appUser.DomainId, adminId, command.Reason ?? "Suspended by platform admin"));
        }
    }

    [WolverinePut("/platform/users/{userId:guid}/roles")]
    [Authorize(Policy = "PlatformAdmin")]
    public static async Task<(IResult, RoleAssigned)> AssignRole(
        Guid userId,
        AssignPlatformRoleCommand command,
        UserManager<AppUser> userManager,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        AppUser appUser = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new UserNotFoundException(new UserId(userId));

        IdentityResult result = await userManager.AddToRoleAsync(appUser, command.RoleName);
        if (!result.Succeeded)
            throw new RoleAssignmentException(result.Errors);

        return (
            Results.Ok(),
            new RoleAssigned(appUser.DomainId, command.RoleName, user.GetUserId()));
    }

    [WolverineDelete("/platform/users/{userId:guid}/roles/{roleName}")]
    [Authorize(Policy = "PlatformAdmin")]
    public static async Task<(IResult, RoleRevoked)> RevokeRole(
        Guid userId,
        string roleName,
        UserManager<AppUser> userManager,
        ClaimsPrincipal user,
        CancellationToken ct)
    {
        AppUser appUser = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new UserNotFoundException(new UserId(userId));

        IdentityResult result = await userManager.RemoveFromRoleAsync(appUser, roleName);
        if (!result.Succeeded)
            throw new RoleAssignmentException(result.Errors);

        return (
            Results.Ok(),
            new RoleRevoked(appUser.DomainId, roleName, user.GetUserId()));
    }
}
