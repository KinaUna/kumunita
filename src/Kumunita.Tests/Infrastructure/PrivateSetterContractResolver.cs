using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Kumunita.Tests.Infrastructure;

/// <summary>
/// Newtonsoft.Json contract resolver that allows deserialization into properties
/// with private setters. Required because Marten domain types use { get; private set; }
/// to protect invariants while still needing to round-trip through JSON.
/// </summary>
public class PrivateSetterContractResolver : DefaultContractResolver
{
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty prop = base.CreateProperty(member, memberSerialization);

        if (!prop.Writable && member is PropertyInfo pi)
        {
            MethodInfo? setter = pi.GetSetMethod(nonPublic: true);
            if (setter != null)
                prop.Writable = true;
        }

        return prop;
    }
}
