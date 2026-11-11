using Kumunita.Core.Privacy;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The P-1 coverage gate (IMPROVE-SEC IS20h, design §2.3): every registered
/// <c>Schema.For&lt;T&gt;</c> doc type must carry a <see cref="DataClassAttribute"/>.
/// The test builds a <see cref="StoreOptions"/> by calling every
/// <c>*DocTypes.Configure</c> surface, then asserts that
/// <see cref="DataClassCatalog.For(Type)"/> returns non-null for each registered
/// type.
///
/// <b>Expected state after IS20 (79/81):</b> two types — <c>Profile</c>
/// (IS17 pause) and <c>InventoryCheckout</c> (IS19 pause) — are
/// <see cref="Erasure.Pseudonymize"/> rows that name no principal field, so
/// they are deliberately unannotated. IS21 must resolve both by naming the
/// field, at which point this gate moves 79 → 81.
///
/// <b>Form (a) — red gate:</b> the assertion is strict (all 81 covered); the
/// test currently <b>fails</b> naming those two types. It is not frozen at 79.
/// D7: "start from the measured baseline and retire it to zero; never freeze it."
/// </summary>
public class DataClassCoverageTests
{
    /// <summary>
    /// The 24 <c>*DocTypes</c> classes with a <c>Configure(StoreOptions)</c>
    /// method. <c>PortabilityDocTypes</c> is excluded — it declares no
    /// <c>Configure</c> method (its rows are an inventory map, not Marten
    /// registrations).
    /// </summary>
    private static readonly Type[] DocTypesClasses =
    {
        typeof(Kumunita.Core.AdminOnboardingDocTypes),
        typeof(Kumunita.Core.BookmarkDocTypes),
        typeof(Kumunita.Core.CoreDocTypes),
        typeof(Kumunita.Core.DocumentDocTypes),
        typeof(Kumunita.Core.ErrorReportDocTypes),
        typeof(Kumunita.Core.EscalationDocTypes),
        typeof(Kumunita.Core.EventDocTypes),
        typeof(Kumunita.Core.GroupMessagingDocTypes),
        typeof(Kumunita.Core.InventoryDocTypes),
        typeof(Kumunita.Core.KioskDocTypes),
        typeof(Kumunita.Core.MediaDocTypes),
        typeof(Kumunita.Core.MessagingDocTypes),
        typeof(Kumunita.Core.NotificationDocTypes),
        typeof(Kumunita.Core.PageDocTypes),
        typeof(Kumunita.Core.PersonalNotesDocTypes),
        typeof(Kumunita.Core.PostDocTypes),
        typeof(Kumunita.Core.ProjectDocTypes),
        typeof(Kumunita.Core.SessionDocTypes),
        typeof(Kumunita.Core.SiteContentDocTypes),
        typeof(Kumunita.Core.SurfaceLabelsDocTypes),
        typeof(Kumunita.Core.TagDocTypes),
        typeof(Kumunita.Core.Usage.StorageHistoryDocTypes),
        typeof(Kumunita.Core.Usage.StorageSettingsDocTypes),
        typeof(Kumunita.Core.UsageDocTypes),
    };

    /// <summary>
    /// Build a <see cref="StoreOptions"/> by calling every
    /// <c>*DocTypes.Configure</c>, then collect the deduplicated set of
    /// registered root types via <c>AllKnownDocumentTypes()</c>.
    /// </summary>
    private static HashSet<Type> BuildRegisteredTypes()
    {
        var opts = new StoreOptions();
        foreach (Type docTypesClass in DocTypesClasses)
        {
            docTypesClass
                .GetMethod("Configure", new[] { typeof(StoreOptions) })!
                .Invoke(null, new object[] { opts });
        }
        return ((IReadOnlyStoreOptions)opts)
            .AllKnownDocumentTypes()
            .Select(dt => dt.DocumentType)
            .ToHashSet();
    }

    [Fact]
    public void EveryRegisteredDocType_HasADataClass()
    {
        HashSet<Type> registered = BuildRegisteredTypes();

        // Sanity: the registered set must be exactly 84 (the design doc's
        // count, +1 for ADR 0301 / IMPROVE-UX R11's DeletionRequest, +1 for
        // ADR 0302 / IMPROVE-UX R15's KioskLink, +1 for ADR 0304 /
        // IMPROVE-UX A07's Invitation).
        if (registered.Count != 84)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected 84 registered doc types; got {registered.Count}. " +
                $"Types: {string.Join(", ", registered.Select(t => t.Name).OrderBy(n => n))}");
        }

        // The gate: every registered type must carry a [DataClass].
        List<string> uncovered = registered
            .Where(t => DataClassCatalog.For(t) is null)
            .Select(t => t.FullName ?? t.Name)
            .OrderBy(n => n)
            .ToList();

        if (uncovered.Count > 0)
        {
            throw new Xunit.Sdk.XunitException(
                "Every registered Schema.For<T> doc type must carry [DataClass] (P-1, ADR 0298). " +
                $"Uncovered: {string.Join(", ", uncovered)}. " +
                "If a type is a Pseudonymize row awaiting a principal field decision, " +
                "resolve it in the successor unit (IS21) before annotating.");
        }
    }
}
