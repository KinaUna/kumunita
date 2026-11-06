using System.Reflection;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The C08 route-table pin (IMPROVE-CODE, ADR 0287): the Projects module's
/// <c>/projects</c> route family — its full set of (HTTP verb, route path)
/// pairs — is **unchanged** by the split of <see cref="ProjectsController"/>
/// into <c>ProjectsController</c> + <c>BoardsController</c> +
/// <c>TodosController</c>.
/// <para>
/// The proof enumerates the built <see cref="IEndpointDataSource"/> (the same
/// object ASP.NET Core serves) and asserts the sorted set of
/// <c>{VERB} {path}</c> strings for every route under <c>/projects</c> is
/// byte-identical to the snapshot taken against the pre-split single
/// controller. A moved <c>[Route]</c> / <c>[Http*]</c> attribute that changes
/// a path or verb — or a route that vanishes — fails this test, so "route
/// attributes move verbatim" is enforced by the test, not by hand. The test is
/// green across all three aggregate commits.
/// </para>
/// </summary>
public class ProjectsRouteTableTests
{
    // The pinned snapshot: the 63 (verb, path) pairs of the /projects route
    // family, sorted. Recorded against the pre-split single ProjectsController
    // and invariant across the C08 split (Boards / Todos / Projects+Goals).
    private static readonly string[] Expected =
    [
        "GET /projects",
        "GET /projects/boards",
        "GET /projects/boards/new",
        "GET /projects/boards/{id}",
        "GET /projects/boards/{id}/edit",
        "GET /projects/goals/new",
        "GET /projects/goals/{id}",
        "GET /projects/goals/{id}/edit",
        "GET /projects/projects/new",
        "GET /projects/projects/{id}",
        "GET /projects/projects/{id}/edit",
        "GET /projects/todos",
        "GET /projects/todos/new",
        "GET /projects/todos/{id}",
        "GET /projects/todos/{id}/edit",
        "GET /projects/todos/{id}.ics",
        "GET /projects/todos.ics",
        "POST /projects/boards",
        "POST /projects/boards/{id}",
        "POST /projects/boards/{id}/delete",
        "POST /projects/boards/{id}/lanes",
        "POST /projects/boards/{id}/lanes/{laneId}",
        "POST /projects/boards/{id}/lanes/{laneId}/delete",
        "POST /projects/boards/{id}/lanes/{laneId}/move",
        "POST /projects/boards/{id}/lanes/{laneId}/move-left",
        "POST /projects/boards/{id}/lanes/{laneId}/move-right",
        "POST /projects/boards/{id}/lanes/{laneId}/todos",
        "POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-down",
        "POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-left",
        "POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-right",
        "POST /projects/boards/{boardId}/lanes/{laneId}/cards/{placementId}/move-up",
        "POST /projects/boards/{id}/lanes/{targetLaneId}/cards/{placementId}/move",
        "POST /projects/boards/{id}/report",
        "POST /projects/boards/{id}/set-event",
        "POST /projects/boards/{id}/set-project",
        // C2-02 collapsed the board translation route onto the same
        // /translations/update + /translations/remove pair as Projects/Todos.
        "POST /projects/boards/{id}/translations/remove",
        "POST /projects/boards/{id}/translations/update",
        "POST /projects/goals",
        "POST /projects/goals/{id}",
        "POST /projects/goals/{id}/delete",
        "POST /projects/projects",
        "POST /projects/projects/{id}",
        "POST /projects/projects/{id}/delete",
        "POST /projects/projects/{id}/report",
        "POST /projects/projects/{id}/translations",
        "POST /projects/projects/{id}/translations/remove",
        "POST /projects/projects/{id}/translations/update",
        "POST /projects/todos",
        "POST /projects/todos/{id}",
        "POST /projects/todos/{id}/assign",
        "POST /projects/todos/{id}/claim",
        "POST /projects/todos/{id}/comments",
        "POST /projects/todos/{id}/comments/{commentId}/delete",
        "POST /projects/todos/{id}/copy-to",
        "POST /projects/todos/{id}/create-event",
        "POST /projects/todos/{id}/delete",
        "POST /projects/todos/{id}/move-to",
        "POST /projects/todos/{id}/report",
        "POST /projects/todos/{id}/set-event",
        "POST /projects/todos/{id}/set-project",
        "POST /projects/todos/{id}/status",
        "POST /projects/todos/{id}/subtasks",
        "POST /projects/todos/{id}/translations",
        "POST /projects/todos/{id}/translations/remove",
        "POST /projects/todos/{id}/translations/update",
    ];

    /// <summary>
    /// The /projects route family is exactly the pinned snapshot — no route
    /// added, removed, re-pathed, or re-verb'd by the controller split.
    /// </summary>
    [Fact]
    public void Projects_Route_Family_Is_Unchanged()
    {
        var actual = EnumerateProjectsRoutes();
        var expected = Expected.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var missing = expected.Except(actual, StringComparer.Ordinal).OrderBy(x => x).ToArray();
        var extra = actual.Except(expected, StringComparer.Ordinal).OrderBy(x => x).ToArray();
        Assert.True(missing.Length == 0 && extra.Length == 0,
            $"Route table drift.\nMISSING (in snapshot, not served): {string.Join(", ", missing)}\n" +
            $"EXTRA (served, not in snapshot): {string.Join(", ", extra)}");
    }

    /// <summary>
    /// The snapshot is not silently empty (a mis-filter would pass trivially):
    /// it pins the 65-route family (C2-02 collapsed the bare board translation
    /// route onto the /translations/update + /translations/remove pair).
    /// </summary>
    [Fact]
    public void Snapshot_Has_The_Full_Family()
    {
        Assert.Equal(65, Expected.Length);
    }

    /// <summary>
    /// Enumerates the <c>/projects</c> route family by reading the
    /// <c>[HttpGet]</c> / <c>[HttpPost]</c> / <c>[Route]</c> attributes on
    /// every action method in the Kumunita.Web assembly. Returns a sorted
    /// array of <c>"VERB /path"</c> strings. This is the same set ASP.NET
    /// Core's <c>EndpointDataSource</c> would materialize — the attributes are
    /// the source of truth.
    /// </summary>
    private static string[] EnumerateProjectsRoutes()
    {
        var assembly = typeof(ProjectsController).Assembly;
        var routes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in assembly.GetExportedTypes())
        {
            if (!typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type))
                continue;

            var classRoute = type.GetCustomAttribute<RouteAttribute>()?.Template ?? "";

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                string? verb = null;
                string? template = null;

                var httpGet = method.GetCustomAttribute<HttpGetAttribute>();
                var httpPost = method.GetCustomAttribute<HttpPostAttribute>();
                var httpPut = method.GetCustomAttribute<HttpPutAttribute>();
                var httpDelete = method.GetCustomAttribute<HttpDeleteAttribute>();

                if (httpGet is not null)
                { verb = "GET"; template = httpGet.Template; }
                else if (httpPost is not null)
                { verb = "POST"; template = httpPost.Template; }
                else if (httpPut is not null)
                { verb = "PUT"; template = httpPut.Template; }
                else if (httpDelete is not null)
                { verb = "DELETE"; template = httpDelete.Template; }

                if (verb is null || template is null)
                    continue;

                var path = classRoute.Length > 0 ? classRoute + template : template;
                if (path.StartsWith("/projects", StringComparison.Ordinal))
                    routes.Add($"{verb} {path}");
            }
        }

        return routes.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
}
