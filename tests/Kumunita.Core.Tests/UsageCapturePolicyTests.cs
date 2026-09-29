using Kumunita.Core.Usage;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The 4 D1 policy pins (ADR 0114 D1; design doc §pinned tests). BCL-only,
/// no Testcontainers — <see cref="UsageCapturePolicy.Decide"/> is pure over
/// POCOs. These pin the two skip rules (C-M13·4) and the two record rules
/// (C-M13·2 / C-M13·4).
/// </summary>
public class UsageCapturePolicyTests
{
    /// <summary>
    /// The C-M13·4 boundary: a request with no recognized endpoint (a true
    /// 404, a malformed path) is skipped.
    /// </summary>
    [Fact]
    public void Policy_Skips_No_Endpoint()
    {
        var input = new UsageCaptureInput
        {
            HasEndpoint = false,
            IsStaticFile = false,
            RouteTemplate = "GET /no/such/route",
            ActorId = "a1"
        };

        var decision = UsageCapturePolicy.Decide(input);

        Assert.False(decision.Record);
    }

    /// <summary>
    /// The C-M13·4 boundary: a static file is noise, not a usage surface.
    /// </summary>
    [Fact]
    public void Policy_Skips_StaticFile_Endpoint()
    {
        var input = new UsageCaptureInput
        {
            HasEndpoint = true,
            IsStaticFile = true,
            RouteTemplate = "GET /css/site.css",
            ActorId = "a1"
        };

        var decision = UsageCapturePolicy.Decide(input);

        Assert.False(decision.Record);
    }

    /// <summary>
    /// The C-M13·2 / C-M13·4 boundary: a recognized, non-static request is
    /// recorded with the <b>route template</b> (never a concrete path) and
    /// the <see cref="UsageCaptureInput.ActorId"/> passed through verbatim.
    /// </summary>
    [Fact]
    public void Policy_Records_Template_Not_Concrete_Path()
    {
        var input = new UsageCaptureInput
        {
            HasEndpoint = true,
            IsStaticFile = false,
            RouteTemplate = "GET /posts/{id}",
            ActorId = "a1"
        };

        var decision = UsageCapturePolicy.Decide(input);

        Assert.True(decision.Record);
        Assert.Equal("GET /posts/{id}", decision.RouteTemplate);
        Assert.Equal("a1", decision.ActorId);
    }

    /// <summary>
    /// The C-M13·2 boundary: an anonymous record carries an empty
    /// <see cref="UsageCaptureDecision.ActorId"/> (the
    /// <c>ActorId</c>-empty-for-anonymous rule).
    /// </summary>
    [Fact]
    public void Policy_Anonymous_Record_Has_Empty_ActorId()
    {
        var input = new UsageCaptureInput
        {
            HasEndpoint = true,
            IsStaticFile = false,
            RouteTemplate = "GET /",
            ActorId = ""
        };

        var decision = UsageCapturePolicy.Decide(input);

        Assert.True(decision.Record);
        Assert.Equal(string.Empty, decision.ActorId);
    }
}
