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

        // The "documents organization" lane — the ADR 0039 Pages <c>ParentId</c>
        // forest carried to the Documents context (a <c>null</c> <c>ParentId</c>
        // is a valid, intended root — no <c>(ParentId, Name)</c> DB-level
        // unique index because a folder's display name is a **label**, not a
        // business key — the uniqueness guard is the write lane, the ADR 0044
        // tag <c>Slug</c> write-lane idiom: a second actor creating the same
        // name under the same parent is a form error, not a DB violation).
        opts.Schema.For<DocumentFolder>();
    }
}
