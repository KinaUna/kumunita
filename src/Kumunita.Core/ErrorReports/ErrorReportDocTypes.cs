using Marten;

namespace Kumunita.Core;

/// <summary>
/// The M31 error-report bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1, the UsageDocTypes / M3DocTypes
/// parallel-surface shape). ErrorReport uses the conventional string Id
/// identity (Marten default), so only the (TriageStatus, Created) index
/// for the admin list ordering needs pinning.
/// </summary>
public static class ErrorReportDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        // One For<ErrorReport>() call + the (TriageStatus, Created) indexes
        // for the admin list ordering — the repo's established chained
        // .Index() convention (the M6 Notification (RecipientId, Created) /
        // M17 Bookmark (OwnerId, Created) feed-ordering-index shape), NOT a
        // lambda-config form (this Marten version's For<T>() takes no config
        // lambda — the CS1501 the design-doc §2.2 snippet hit). The semantic
        // intent (index TriageStatus + Created) is unchanged.
        opts.Schema.For<ErrorReports.ErrorReport>()
               .Index(x => x.TriageStatus)
               .Index(x => x.Created);
    }
}
