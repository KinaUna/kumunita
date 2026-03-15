using Kumunita.Authorization.Domain;
using Kumunita.Shared.Kernel;
using Marten;

namespace Kumunita.Authorization.Features.CapabilityTokens;

public record ValidateCapabilityToken(
    CapabilityTokenId TokenId,
    UserId RequesterId,
    string ResourceTypeName);

public record TokenValidationResult(bool IsValid, string? FailureReason);

public static class ValidateCapabilityTokenHandler
{
    public static async Task<TokenValidationResult> Handle(
        ValidateCapabilityToken cmd,
        IDocumentSession session,
        CancellationToken ct)
    {
        CapabilityToken? token = await session.LoadAsync<CapabilityToken>(cmd.TokenId, ct);

        if (token is null)
            return new TokenValidationResult(false, "Token not found");

        if (token.RequesterId != cmd.RequesterId)
            return new TokenValidationResult(false, "Token requester mismatch");

        if (token.ResourceTypeName != cmd.ResourceTypeName)
            return new TokenValidationResult(false, "Resource type mismatch");

        if (!token.IsValid())
            return new TokenValidationResult(false,
                $"Token is {token.Status.ToString().ToLower()}");

        // Mark as used — single-use tiers are consumed here
        token.MarkUsed();
        session.Store(token);
        session.Store(AuditEntry.ForTokenUsed(token));

        return new TokenValidationResult(true, null);
    }
}