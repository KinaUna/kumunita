using Kumunita.Core.Identity;
using Marten;

namespace Kumunita.Core.Identity;

/// <summary>
/// Production <see cref="IEmailDeadLetterCounter"/>: counts the <see cref="EmailDeadLetter"/>
/// rows through a Marten <c>IQuerySession</c>. Wolverine-free — the health probe is a
/// plain read; the store is already a required dependency.
/// </summary>
public sealed class EmailDeadLetterCounter : IEmailDeadLetterCounter
{
    private readonly IDocumentStore _store;

    public EmailDeadLetterCounter(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<int> GetCountAsync(CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        return await session
            .Query<EmailDeadLetter>()
            .CountAsync(ct)
            .ConfigureAwait(false);
    }
}
