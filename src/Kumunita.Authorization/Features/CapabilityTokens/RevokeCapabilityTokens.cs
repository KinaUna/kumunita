using Kumunita.Authorization.Domain;
using Kumunita.Shared.Kernel;
using Marten;

namespace Kumunita.Authorization.Features.CapabilityTokens;

public record RevokeAllUserTokens(UserId UserId, string Reason);

public static class RevokeAllUserTokensHandler
{
    public static async Task Handle(
        RevokeAllUserTokens cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        // Load all active tokens for this user
        var activeTokens = await session
            .Query<CapabilityToken>()
            .Where(t =>
                t.RequesterId == cmd.UserId &&
                t.Status == CapabilityTokenStatus.Active)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.Revoke();
            session.Store(token);
            session.Store(AuditEntry.ForTokenUsed(token));
        }
    }
}