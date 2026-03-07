using Kumunita.Shared.Kernel;

namespace Kumunita.Announcements.Exceptions;

public class PendingSubmissionLimitExceededException : Exception
{
    public UserId MemberId { get; }
    public int Limit { get; }

    public PendingSubmissionLimitExceededException(UserId memberId, int limit)
        : base($"Member '{memberId.Value}' has reached the maximum " +
               $"of {limit} pending submissions.")
    {
        MemberId = memberId;
        Limit = limit;
    }
}