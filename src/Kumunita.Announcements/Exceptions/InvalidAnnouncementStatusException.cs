using Kumunita.Announcements.Domain;
using Kumunita.Shared.Kernel;

namespace Kumunita.Announcements.Exceptions;

public class InvalidAnnouncementStatusException : Exception
{
    public AnnouncementId AnnouncementId { get; }
    public AnnouncementStatus CurrentStatus { get; }

    public InvalidAnnouncementStatusException(
        AnnouncementId id,
        AnnouncementStatus currentStatus,
        string attemptedAction)
        : base($"Cannot {attemptedAction} announcement '{id.Value}' " +
               $"with status '{currentStatus}'.")
    {
        AnnouncementId = id;
        CurrentStatus = currentStatus;
    }
}