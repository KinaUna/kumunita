using Kumunita.Shared.Kernel.Events;
using Wolverine.Configuration;
using Wolverine.Runtime;
using Wolverine.Runtime.Routing;

namespace Kumunita.Shared.Infrastructure.Messaging;

/// <summary>
/// Routes every <see cref="IDomainEvent"/> implementation to the local queue
/// that matches its namespace prefix (e.g. Kumunita.Identity.* → "identity").
/// </summary>
public sealed class DomainEventModuleRoutingConvention : IMessageRoutingConvention
{
    private static readonly (string Prefix, string Queue)[] ModuleQueues =
    [
        ("Kumunita.Identity",      "identity"),
        ("Kumunita.Localization",  "localization"),
        ("Kumunita.Announcements", "announcements"),
    ];

    public void DiscoverListeners(
        IWolverineRuntime runtime,
        IReadOnlyList<Type> handledMessageTypes) { }

    public IEnumerable<Endpoint> DiscoverSenders(Type messageType, IWolverineRuntime runtime)
    {
        if (!typeof(IDomainEvent).IsAssignableFrom(messageType))
            yield break;

        foreach ((string prefix, string queue) in ModuleQueues)
        {
            if (messageType.FullName?.StartsWith(prefix, StringComparison.Ordinal) == true)
            {
                yield return runtime.Endpoints
                    .GetOrBuildSendingAgent(new Uri($"local://{queue}"))
                    .Endpoint;

                yield break;
            }
        }
    }
}