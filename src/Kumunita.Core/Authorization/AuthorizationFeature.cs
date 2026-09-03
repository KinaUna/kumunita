using Marten;
using Weasel.Core;
using Weasel.Core.Migrations;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace Kumunita.Core.Authorization;

/// <summary>
/// Hand-rolled <see cref="IFeatureSchema"/> for the <c>mt."AdminOverride"</c> table
/// (ADR 0004 §B.1 — M1's one deliberate exception to the "Marten-native documents" rule).
/// <para>
/// <see cref="AdminOverride"/> is **not** a Marten document: the host operator writes its
/// rows directly into Postgres via psql (OPS §9), and the app only ever <em>reads</em> them
/// on the hot inline break-glass path. Forcing it through Marten's document pipeline would
/// require a fake C# <c>[Id]</c> and misrepresent the ownership model, so the DDL lives
/// here, registered alongside M0's <c>KumunitaFeature</c> via
/// <c>StoreOptions.Storage.Add&lt;T&gt;()</c> and applied through the same
/// <c>ApplyAllConfiguredChangesToDatabaseAsync()</c> boot path.
/// </para>
/// <para>
/// Column names and types match the camelCase properties on <see cref="AdminOverride"/>
/// exactly, so any raw-SQL read (break-glass inline check, /admin/break-glass) lines up
/// with the POCO without a translation step.
/// </para>
/// </summary>
public sealed class AuthorizationFeature : FeatureSchemaBase
{
    private readonly Table _table;

    public AuthorizationFeature(StoreOptions options)
        : base("authorization", options.Advanced.Migrator)
    {
        _table = new Table(
            new PostgresqlObjectName(options.DatabaseSchemaName, "AdminOverride",
                SchemaUtils.IdentifierUsage.General));

        // Weasel 9's TableColumn.QuotedName always emits camelCase column names in
        // CREATE TABLE (quoted), but IndexDefinition.ToCreateSql only quotes column
        // identifiers when Table.PreserveIdentifierCase is true — otherwise they are
        // lowercased, and Postgres will not find the quoted CREATE-TABLE columns
        // (42703: column "userId" does not exist). Enabling it makes both DDL
        // statements agree on the camelCase physical identifiers, which is what the
        // ADR's raw-SQL read contract requires.
        _table.PreserveIdentifierCase = true;

        _table.AddColumn("id", "varchar").AsPrimaryKey();
        _table.AddColumn("userId", "varchar").NotNull();
        _table.AddColumn("token", "varchar").NotNull();
        _table.AddColumn("grantedAt", "timestamptz").NotNull();
        _table.AddColumn("expiresAt", "timestamptz").NotNull();
        _table.AddColumn("consumedAt", "timestamptz"); // nullable — set on first consumption

        // Non-unique composite index (ADR 0004 §B.1) for the hot inline break-glass read.
        // Weasel 9 has no Table.AddIndex(...) — indexes are added to Table.Indexes as
        // IndexDefinition objects, and the owning Table (an ISchemaObject) emits both
        // the CREATE TABLE and the CREATE INDEX in its DDL, so a single yield covers both.
        // We do NOT mark .IsUnique — the index is non-unique by design (one row per
        // (userId, token), not per (userId, consumedAt)). The name is deterministic so
        // the delta-detection pass can match it across applies (re-runs are no-ops).
        _table.Indexes.Add(
            new IndexDefinition("idx_admin_override_user_consumed")
                .AgainstColumns(new[] { "userId", "consumedAt" }));
    }

    /// <summary>
    /// The single <c>mt."AdminOverride"</c> table. Its DDL (Table + non-unique
    /// (userId, consumedAt) index) is delta-detected as one <see cref="ISchemaObject"/>,
    /// so a re-apply is a no-op. The index is embedded in the table's own CREATE
    /// statement — Weasel 9 emits both atomically.
    /// </summary>
    protected override IEnumerable<ISchemaObject> schemaObjects()
    {
        yield return _table;
    }
}
