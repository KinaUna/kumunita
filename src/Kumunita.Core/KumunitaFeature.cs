using Marten;
using Weasel.Core;
using Weasel.Core.Migrations;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace Kumunita.Core;

/// <summary>
/// First versioned schema change (M0): the per-instance community identity table in the
/// <c>mt</c> schema.
/// <para>
/// In Marten 9 the pre-9.x <c>IMigration</c>/<c>StoreOptions.Migrations</c> step model no
/// longer exists. The modern equivalent is a <see cref="FeatureSchemaBase"/> subclass that
/// contributes <see cref="ISchemaObject"/>s, registered via <c>StoreOptions.Storage.Add&lt;T&gt;()</c>
/// and applied idempotently by <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> (each object
/// is delta-detected against the live catalog, so a re-run is a no-op). Forward-only; the DDL
/// can be exported for review via <c>store.Storage.Database.WriteMigrationFileAsync()</c>.
/// ADR 0004 (Decision B) is updated to match.
/// </para>
/// </summary>
public sealed class KumunitaFeature : FeatureSchemaBase
{
    private readonly StoreOptions _options;

    public KumunitaFeature(StoreOptions options)
        : base("kumunita", options.Advanced.Migrator)
    {
        _options = options;
    }

    /// <summary>The community identity table lives in the domain schema (<c>mt</c>).</summary>
    protected override IEnumerable<ISchemaObject> schemaObjects()
    {
        var table = new Table(
            new PostgresqlObjectName(_options.DatabaseSchemaName, "community", SchemaUtils.IdentifierUsage.General));

        table.AddColumn("id", "varchar").AsPrimaryKey();
        table.AddColumn("name", "varchar").NotNull();

        yield return table;
    }
}
