using Kumunita.Core.Identity;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="HealthController"/>. The 200 "ok" path asserts
/// the JSON shape consumed by load balancers / k8s liveness probes (OPS.md §8);
/// the 503 path proves the degraded branch kicks in when the store cannot
/// reach the database; and a test asserts the M1 contract that a non-empty
/// <see cref="EmailDeadLetter"/> set flips the status to "degraded"
/// (OPS §8 + ARCHITECTURE.md §5/§6.2) while keeping the app live.
/// </summary>
public class HealthControllerTests
{
    private static HealthController CreateController(bool databaseReachable, int deadLetterRows)
    {
        // IDocumentStore stub for the connectivity check (per ADR 0004 + Marten 9:
        // store.Storage.Database.AssertConnectivityAsync).
        var store = Substitute.For<IDocumentStore>();
        var sql = store.Storage!.Database!;

        if (databaseReachable)
        {
            sql.AssertConnectivityAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        }
        else
        {
            sql.AssertConnectivityAsync(Arg.Any<CancellationToken>())
                .Returns(call => throw new InvalidOperationException("connection refused"));
        }

        // IEmailDeadLetterCounter is a plain interface seam — trivially stubbable
        // (no Marten queryable / sealed-class plumbing required).
        var deadLetters = Substitute.For<IEmailDeadLetterCounter>();
        deadLetters.GetCountAsync(Arg.Any<CancellationToken>()).Returns(deadLetterRows);

        return new HealthController(store, deadLetters);
    }

    [Fact]
    public async Task Get_When_DatabaseReachable_Returns_200_And_OkShape()
    {
        var controller = CreateController(databaseReachable: true, deadLetterRows: 0);

        var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, (int)result.StatusCode!);

        var payload = result.Value!;
        var type = payload.GetType();
        var names = type.GetProperties().Select(p => p.Name).ToHashSet();
        Assert.Contains("status", names);
        Assert.Contains("app", names);
        Assert.Contains("database", names);
        Assert.Contains("emailDeadLetters", names);
        Assert.Contains("elapsedMs", names);

        Assert.Equal("ok", type.GetProperty("status")!.GetValue(payload));
        Assert.Equal("Kumunita", type.GetProperty("app")!.GetValue(payload));
        Assert.Equal("ok", type.GetProperty("database")!.GetValue(payload));
        Assert.Equal(0, type.GetProperty("emailDeadLetters")!.GetValue(payload));
    }

    [Fact]
    public async Task Get_When_DatabaseUnreachable_Returns_503_And_DegradedShape()
    {
        var controller = CreateController(databaseReachable: false, deadLetterRows: 0);

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
        var controller = CreateController(databaseReachable: true, deadLetterRows: 0);

        var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        var elapsed = result!.Value!.GetType().GetProperty("elapsedMs")!.GetValue(result!.Value);
        Assert.IsType<long>(elapsed);
    }

    [Fact]
    public async Task Get_When_EmailDeadLetters_Returns_DegradedStatus_WithCount()
    {
        // OPS §8: the non-empty EmailDeadLetter set must flip the probe to
        // "degraded" (while the DB is reachable and the app is still up).
        const int seededRows = 3;
        var controller = CreateController(databaseReachable: true, deadLetterRows: seededRows);

        var result = await controller.Get(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        Assert.Equal(200, (int)result.StatusCode!);

        var payload = result!.Value!;
        var type = payload.GetType();
        Assert.Equal("degraded", type.GetProperty("status")!.GetValue(payload));
        Assert.Equal("ok", type.GetProperty("database")!.GetValue(payload));
        Assert.Equal(seededRows, type.GetProperty("emailDeadLetters")!.GetValue(payload));
    }
}
