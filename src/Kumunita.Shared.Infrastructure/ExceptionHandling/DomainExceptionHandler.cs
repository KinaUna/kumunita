using Kumunita.Announcements.Exceptions;
using Kumunita.Authorization.Exceptions;
using Kumunita.Identity.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Kumunita.Shared.Infrastructure.ExceptionHandling;

public class DomainExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        var (statusCode, message) = exception switch
        {
            UserNotFoundException e =>
                (StatusCodes.Status404NotFound, e.Message),
            UserGroupNotFoundException e =>
                (StatusCodes.Status404NotFound, e.Message),
            AuthorizationStateNotFoundException e =>
                (StatusCodes.Status404NotFound, e.Message),
            RegistrationException e =>
                (StatusCodes.Status400BadRequest, e.Message),
            RoleAssignmentException e =>
                (StatusCodes.Status400BadRequest, e.Message),
            AlreadyUserGroupMemberException e =>
                (StatusCodes.Status409Conflict, e.Message),
            AnnouncementNotFoundException e =>
                (StatusCodes.Status404NotFound, e.Message),
            InvalidAnnouncementStatusException e =>
                (StatusCodes.Status409Conflict, e.Message),
            MemberSubmissionsDisabledException e =>
                (StatusCodes.Status403Forbidden, e.Message),
            PendingSubmissionLimitExceededException e =>
                (StatusCodes.Status429TooManyRequests, e.Message),
            _ => (-1, null)
        };

        if (statusCode == -1) return false; // unhandled — let the next handler deal with it

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(
            new { error = message }, ct);

        return true;
    }
}