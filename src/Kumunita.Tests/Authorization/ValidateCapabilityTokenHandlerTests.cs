using Kumunita.Authorization.Domain;
using Kumunita.Authorization.Features.CapabilityTokens;
using Kumunita.Authorization.Infrastructure;
using Kumunita.Shared.Kernel;
using Kumunita.Tests.Infrastructure;
using Marten;

namespace Kumunita.Tests.Authorization;

[Collection("Integration")]
public class ValidateCapabilityTokenHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static IDocumentStore? SharedStore;

    private IDocumentStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        await SchemaLock.WaitAsync();
        try
        {
            if (SharedStore is null)
            {
                SharedStore = DocumentStore.For(opts =>
                {
                    opts.Connection(db.ConnectionString);
                    opts.UseSystemTextJsonForSerialization(configure: PrivateSetterTypeInfoResolver.Configure);
                    opts.AddAuthorizationMartenSchema();
                    opts.RegisterValueType(typeof(UserId));
                    opts.RegisterValueType(typeof(CapabilityTokenId));
                });
                await SharedStore.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
            }
        }
        finally
        {
            SchemaLock.Release();
        }
        _store = SharedStore;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ValidToken_ReturnsIsValidTrue_AndMarksUsed()
    {
        (UserId requesterId, UserId ownerId, CapabilityToken token) = await IssueToken(SensitivityTier.Standard);

        ValidateCapabilityToken cmd = new(token.Id, requesterId, token.ResourceTypeName);

        await using IDocumentSession session = _store.LightweightSession();
        TokenValidationResult result = await ValidateCapabilityTokenHandler.Handle(cmd, session, default);
        await session.SaveChangesAsync();

        Assert.True(result.IsValid);
        Assert.Null(result.FailureReason);

        // Verify token was marked as used
        await using IQuerySession readSession = _store.QuerySession();
        CapabilityToken? saved = await readSession.LoadAsync<CapabilityToken>(token.Id);
        Assert.NotNull(saved);
        Assert.True(saved.IsUsed);
    }

    [Fact]
    public async Task UnknownTokenId_ReturnsIsValidFalse()
    {
        ValidateCapabilityToken cmd = new(
            new CapabilityTokenId(Guid.NewGuid()),
            new UserId(Guid.NewGuid()),
            "UserProfile");

        await using IDocumentSession session = _store.LightweightSession();
        TokenValidationResult result = await ValidateCapabilityTokenHandler.Handle(cmd, session, default);

        Assert.False(result.IsValid);
        Assert.Contains("not found", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrongRequester_ReturnsIsValidFalse()
    {
        (_, UserId ownerId, CapabilityToken token) = await IssueToken(SensitivityTier.Standard);
        UserId wrongRequester = new(Guid.NewGuid());

        ValidateCapabilityToken cmd = new(token.Id, wrongRequester, token.ResourceTypeName);

        await using IDocumentSession session = _store.LightweightSession();
        TokenValidationResult result = await ValidateCapabilityTokenHandler.Handle(cmd, session, default);

        Assert.False(result.IsValid);
        Assert.Contains("mismatch", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RevokedToken_ReturnsIsValidFalse()
    {
        (UserId requesterId, _, CapabilityToken token) = await IssueToken(SensitivityTier.Standard);

        // Revoke the token
        await using (IDocumentSession revokeSession = _store.LightweightSession())
        {
            CapabilityToken? t = await revokeSession.LoadAsync<CapabilityToken>(token.Id);
            t!.Revoke();
            revokeSession.Store(t);
            await revokeSession.SaveChangesAsync();
        }

        ValidateCapabilityToken cmd = new(token.Id, requesterId, token.ResourceTypeName);

        await using IDocumentSession session = _store.LightweightSession();
        TokenValidationResult result = await ValidateCapabilityTokenHandler.Handle(cmd, session, default);

        Assert.False(result.IsValid);
        Assert.Contains("revoked", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SensitiveTierToken_AfterValidation_IsConsumed()
    {
        (UserId requesterId, _, CapabilityToken token) = await IssueToken(SensitivityTier.Sensitive);

        ValidateCapabilityToken cmd = new(token.Id, requesterId, token.ResourceTypeName);

        await using IDocumentSession session = _store.LightweightSession();
        TokenValidationResult result = await ValidateCapabilityTokenHandler.Handle(cmd, session, default);
        await session.SaveChangesAsync();

        Assert.True(result.IsValid);

        // Sensitive tokens are consumed (single-use)
        await using IQuerySession readSession = _store.QuerySession();
        CapabilityToken? saved = await readSession.LoadAsync<CapabilityToken>(token.Id);
        Assert.Equal(CapabilityTokenStatus.Consumed, saved!.Status);
    }

    // ── Helper ─────────────────────────────────────────────────────────────────

    private async Task<(UserId requesterId, UserId ownerId, CapabilityToken token)> IssueToken(
        SensitivityTier tier)
    {
        UserId requesterId = new(Guid.NewGuid());
        UserId ownerId = new(Guid.NewGuid());

        CapabilityToken token = CapabilityToken.Issue(
            requesterId, ownerId,
            resourceTypeName: "UserProfile",
            action: "read",
            sensitivityTier: tier,
            requestContext: null);

        await using IDocumentSession session = _store.LightweightSession();
        session.Store(token);
        await session.SaveChangesAsync();

        return (requesterId, ownerId, token);
    }
}
