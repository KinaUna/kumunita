namespace Kumunita.Announcements.Exceptions;

public class MemberSubmissionsDisabledException : Exception
{
    public MemberSubmissionsDisabledException()
        : base("Member announcement submissions are currently disabled.") { }
}