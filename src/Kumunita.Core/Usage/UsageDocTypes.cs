using Kumunita.Core.Usage;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The M13 usage bounded context's Marten-native document registration
/// surface (ADR 0004 §B.1, the <c>M3DocTypes</c> / <c>M5DocTypes</c>
/// parallel-surface shape). <see cref="UsageEvent"/> uses the
/// conventional <c>string Id</c> identity (the M3 "string Id"
/// convention), so no non-default convention (identity, business-key
/// index) needs pinning: Marten's defaults apply.
/// </summary>
public static class UsageDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        opts.Schema.For<UsageEvent>();
    }
}
