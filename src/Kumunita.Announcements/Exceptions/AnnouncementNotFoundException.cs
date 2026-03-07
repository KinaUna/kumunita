using Kumunita.Shared.Kernel;

namespace Kumunita.Announcements.Exceptions;

public class AnnouncementNotFoundException : Exception
{
    public AnnouncementId AnnouncementId { get; }

    public AnnouncementNotFoundException(AnnouncementId id)
        : base($"Announcement '{id.Value}' was not found.")
    {
        AnnouncementId = id;
    }
}