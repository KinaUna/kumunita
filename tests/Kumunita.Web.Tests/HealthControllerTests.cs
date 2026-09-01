using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="HealthController"/>. The 200 "ok" path asserts
/// the JSON shape consumed by load balancers / k8s liveness probes (OPS.md §8);
/// the 503 path proves the degraded branch kicks in when the store cannot
/// reach the database.
/// </summary>
public class HealthControllerTests
{
    private static (HealthController controller, IDocumentStore store) CreateController(bool databaseReachable)
    {
        var store = Substitute.For<IDocumentStore>();

        // IDocumentStore.Storage returns IMartenStorage; its .Database returns
        // IAdvancedSql which owns AssertConnectivityAsync (per ADR 0004 + Marten 9).
        // NSubstitute returns null by default for unconfigured members, so we
        // stub the chain explicitly and have it throw or no-op per test.
        var storage = store.Storage!;
        var sql = storage.Database!;

        if (databaseReachable)
        {
            sql.AssertConnectivityAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        }
        else
        {
            sql.AssertConnectivityAsync(Arg.Any<CancellationToken>())
                .Returns(call => throw new InvalidOperationException("connection refused"));
        }

        return (new HealthController(store), store);
    }

    [Fact]
    public async Task Get_When_DatabaseReachable_Returns_200_And_OkShape()
    {
        var (controller, _) = CreateController(databaseReachable: true);

        var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, (int)result.StatusCode!);

        // The controller returns a C# anonymous object — assert on its property
        // names via reflection (the keys the probe consumers read: OPS.md §8).
        var payload = result.Value!;
        var type = payload.GetType();
        var names = type.GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("status", names);
        Assert.Contains("app", names);
        Assert.Contains("database", names);
        Assert.Contains("elapsedMs", names);

        Assert.Equal("ok", type.GetProperty("status")!.GetValue(payload));
        Assert.Equal("Kumunita", type.GetProperty("app")!.GetValue(payload));
        Assert.Equal("ok", type.GetProperty("database")!.GetValue(payload));
    }

    [Fact]
    public async Task Get_When_DatabaseUnreachable_Returns_503_And_DegradedShape()
    {
        var (controller, _) = CreateController(databaseReachable: false);

        var result = await controller.Get(TestContext.Current.CancellationToken) as ObjectResult;

        Assert.NotNull(result);
        Assert.Equal(503, (int)result.StatusCode!);

        var payload = result.Value!;
        var type = payload.GetType();
        var names = type.GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("status", names);
        Assert.Contains("database", names);
        Assert.Equal("degraded", type.GetProperty("status")!.GetValue(payload));
        Assert.Equal("unreachable", type.GetProperty("database")!.GetValue(payload));
    }

    [Fact]
    public async Task Get_Returns_ElapsedMs_On_SucceedPath()
    {
        var (controller, _) = CreateController(databaseReachable: true);

        var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        var elapsed = result!.Value!.GetType().GetProperty("elapsedMs")!.GetValue(result!.Value);
        Assert.IsType<long>(elapsed);
    }
}
