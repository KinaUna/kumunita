using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Kumunita.Tests.Infrastructure;

/// <summary>
/// STJ type info resolver that enables deserialization of domain types that have:
/// - Private parameterless constructors (required by Marten for document hydration)
/// - Private property setters (used to protect invariants)
///
/// Without this, Marten's STJ serializer returns default values for all properties on load,
/// which is a production bug exposed by these tests.
/// </summary>
public static class PrivateSetterTypeInfoResolver
{
    public static void Configure(JsonSerializerOptions opts)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(ApplyPrivateMemberAccess);
        opts.TypeInfoResolver = resolver;
    }

    private static void ApplyPrivateMemberAccess(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

        // Allow STJ to create instances using private parameterless constructors
        if (typeInfo.CreateObject is null)
        {
            ConstructorInfo? ctor = typeInfo.Type.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance, []);
            if (ctor is not null)
                typeInfo.CreateObject = () => ctor.Invoke(null);
        }

        // Allow STJ to set properties that have private setters
        foreach (JsonPropertyInfo prop in typeInfo.Properties)
        {
            if (prop.Set is not null) continue;

            // Match by exact name first, then case-insensitive (handles camelCase mapping)
            PropertyInfo? pi = typeInfo.Type.GetProperties(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(p =>
                    string.Equals(p.Name, prop.Name, StringComparison.OrdinalIgnoreCase));

            MethodInfo? setter = pi?.GetSetMethod(nonPublic: true);
            if (setter is null) continue;

            prop.Set = (obj, value) => setter.Invoke(obj, [value]);
        }
    }
}
