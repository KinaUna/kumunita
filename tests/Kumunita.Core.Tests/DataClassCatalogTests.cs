using Kumunita.Core.Privacy;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The ADR 0298 contract (IMPROVE-SEC IS16, design §2.3): the
/// <see cref="DataClassAttribute"/> declares a doc type's class / erasure /
/// export / reason / retention key, and the <see cref="DataClassCatalog"/>
/// reads it back — with the two invariants pinned: <c>Keep</c> requires a
/// reason, <c>Pseudonymize</c> requires the principal field. A **pure** unit —
/// no <c>PostgresFixture</>, no Marten: the attribute and the catalog only.
/// The fixture types below are throwaway (not registered doc types): IS17–IS20
/// annotate the 81 real types, IS20's coverage gate is the 81/81 check.
/// </summary>
public class DataClassCatalogTests
{
    private sealed class FixtureSessionDoc
    {
        [DataClass(DataClassKind.Pii, Erasure.Delete, ExportScope.Never,
            reason: "F1 §2C decision (now decided): a session credential, not content; D6: 30 days after revoke or expiry",
            retentionKey: "SessionDays")]
        internal sealed class SessionDoc { }
    }

    private sealed class FixtureReport
    {
        [DataClass(DataClassKind.AuditModeration, Erasure.Pseudonymize, ExportScope.Never,
            reason: "moderation state, not authored content (IS23)",
            principalField: "ReporterId")]
        internal sealed class Report { }
    }

    [Fact]
    public void Catalog_ReadsAttribute()
    {
        DataClassAttribute? session = DataClassCatalog.For(typeof(FixtureSessionDoc.SessionDoc));
        Assert.NotNull(session);
        Assert.Equal(DataClassKind.Pii, session!.Kind);
        Assert.Equal(Erasure.Delete, session.Erasure);
        Assert.Equal(ExportScope.Never, session.Export);
        Assert.Equal("SessionDays", session.RetentionKey);
        Assert.Contains("session credential", session.Reason);
        Assert.Null(session.PrincipalField);

        DataClassAttribute? report = DataClassCatalog.For(typeof(FixtureReport.Report));
        Assert.NotNull(report);
        Assert.Equal(Erasure.Pseudonymize, report!.Erasure);
        Assert.Equal("ReporterId", report.PrincipalField);

        // The catalog's assembly walk finds both fixtures, sorted, each validated.
        IReadOnlyList<DataClassEntry> all = DataClassCatalog.All(typeof(DataClassCatalogTests).Assembly);
        Assert.Contains(all, e => e.Type == typeof(FixtureSessionDoc.SessionDoc));
        Assert.Contains(all, e => e.Type == typeof(FixtureReport.Report));
        Assert.True(all.Count >= 2, "both fixture types must be attributed and readable");
    }

    [Fact]
    public void Keep_RequiresReason()
    {
        DataClassAttribute good = new(
            DataClassKind.Platform, Erasure.Keep, ExportScope.InstanceOnly,
            reason: "a platform singleton, not a resident row");
        good.Validate(); // passes — the §2.5 singleton shape

        DataClassAttribute bad = new(
            DataClassKind.Platform, Erasure.Keep, ExportScope.InstanceOnly,
            reason: "   ");
        Assert.Throws<ArgumentException>(() => bad.Validate());

        // The catalog returns the null sentinel for an un-attributed type —
        // IS20's coverage test is exactly "no registered type returns null".
        Assert.Null(DataClassCatalog.For(typeof(DataClassCatalogTests)));
    }

    [Fact]
    public void Pseudonymize_RequiresPrincipalField()
    {
        DataClassAttribute good = new(
            DataClassKind.AuditModeration, Erasure.Pseudonymize, ExportScope.Never,
            reason: "F1 §2B: already pseudonymized (step 5)",
            principalField: "ActorId");
        good.Validate(); // passes — names the field the eraser will tombstone

        DataClassAttribute bad = new(
            DataClassKind.AuditModeration, Erasure.Pseudonymize, ExportScope.Never,
            reason: "a pseudonymized row with no principal field — invalid");
        Assert.Throws<ArgumentException>(() => bad.Validate());
    }
}
