using Testcontainers.PostgreSql;

namespace Kumunita.Tests.Infrastructure;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
        => await _container.StartAsync();

    public async ValueTask DisposeAsync()
        => await _container.DisposeAsync();
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<DatabaseFixture> { }
