using Kumunita.Core.Documents;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// M21 (ADR 0122, D1) — the Document bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1, the <c>UsageDocTypes</c> /
/// <c>MediaDocTypes</c> parallel-surface shape). <see cref="Document"/> uses the
/// conventional <c>string Id</c> identity, so no non-default convention (identity,
/// business-key index) is pinned: Marten's defaults apply.
/// </summary>
public static class DocumentDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        opts.Schema.For<Document>();
    }
}
