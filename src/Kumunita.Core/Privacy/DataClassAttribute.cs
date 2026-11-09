using System.Reflection;

namespace Kumunita.Core.Privacy;

/// <summary>
/// The data-class vocabulary for a stored document type (ADR 0298, D2).
/// Classes (a)–(e) follow
/// <c>docs/security/data-classes.md</c> (PII, private content, audit &amp;
/// moderation, secrets, media/attachment bytes); <see cref="Operational"/>
/// and <see cref="Platform"/> are the two non-resident classes the data-classes
/// doc names in prose (per-resident operational state vs. platform
/// singletons/aggregates). The member set is fixed by the IMPROVE-SEC plan's
/// decision D2 — a member is never renamed or re-ordered.
/// </summary>
public enum DataClassKind
{
    Pii,
    PrivateContent,
    AuditModeration,
    Secret,
    Media,
    Operational,
    Platform,
}

/// <summary>
/// What account deletion (and retention) does to a type's rows (D2, D3).
/// <see cref="Delete"/> erases the resident-scoped rows; <see
/// cref="Pseudonymize"/> keeps the row but erases the principal (body erased,
/// the <c>PrincipalField</c> → <c>deleted:{id}</c> tombstone — the
/// <c>AccessAudit</c> precedent, D3); <see cref="Keep"/> keeps the row as-is
/// and **must** name a reason.
/// </summary>
public enum Erasure
{
    Delete,
    Pseudonymize,
    Keep,
}

/// <summary>
/// Whether the type is part of the resident (GDPR Art. 15/20) export, an
/// operator/instance-only surface, or never exported (D2).
/// </summary>
public enum ExportScope
{
    Resident,
    InstanceOnly,
    Never,
}

/// <summary>
/// The single source of truth for one registered doc type's data-class policy
/// (ADR 0298; the per-type values are design §2.5, copied verbatim by IS17–IS20).
/// **Inert metadata**: nothing reads it yet — IS20's coverage test, IS21–IS23's
/// erasers and IS24's agreement gate are the consumers. It declares exactly
/// what D2 says a row declares: the class, the erasure, the export scope, a
/// required <see cref="Reason"/> and an optional <see cref="RetentionKey"/>
/// (design §2.2 <c>RetentionOptions</c> field name; <c>0</c> means keep
/// indefinitely).
///
/// The two invariants, pinned by <c>Keep_RequiresReason</c> and
/// <c>Pseudonymize_RequiresPrincipalField</c> in
/// <c>DataClassCatalogTests</c>:
/// <list type="bullet">
///   <item><see cref="Erasure.Keep"/> must name a <see cref="Reason"/> — a
///         "kept" row is a decision, not a default.</item>
///   <item><see cref="Erasure.Pseudonymize"/> must name the
///         <see cref="PrincipalField"/> that holds the principal (the field
///         IS21–IS23's pseudonymizers erase).</item>
/// </list>
/// Both are checked by <see cref="Validate"/>; <see cref="DataClassCatalog"/>
/// calls it on every read, so a drifted attribute cannot be read silently.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DataClassAttribute : Attribute
{
    /// <summary>The data class (a)–(e) / operational / platform (D2).</summary>
    public DataClassKind Kind { get; }

    /// <summary>What account deletion does to the row (D2, D3).</summary>
    public Erasure Erasure { get; }

    /// <summary>Whether the type is in the resident export (D2).</summary>
    public ExportScope Export { get; }

    /// <summary>
    /// Why this policy holds (the §2.5 "Reason" column verbatim: the F1 §2A–C
    /// cross-reference, the D3/D6 decision, the ADR). Required on every row;
    /// for <see cref="Erasure.Keep"/> it must be non-empty (<see cref="Validate"/>).
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// The design §2.2 <c>RetentionOptions</c> field this type's rows are
    /// retained under (e.g. <c>SessionDays</c>); null = no retention key
    /// (D6: a <c>Delete</c>-erased or platform-singleton type needs none).
    /// </summary>
    public string? RetentionKey { get; }

    /// <summary>
    /// The field that holds the resident's principal for a
    /// <see cref="Erasure.Pseudonymize"/> row (e.g. <c>SenderId</c>); null for
    /// <c>Delete</c> (the resident-scoped query field is decided by the eraser)
    /// and <c>Keep</c> rows.
    /// </summary>
    public string? PrincipalField { get; }

    public DataClassAttribute(
        DataClassKind kind,
        Erasure erasure,
        ExportScope export,
        string reason,
        string? retentionKey = null,
        string? principalField = null)
    {
        Kind = kind;
        Erasure = erasure;
        Export = export;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason),
            "A [DataClass] row must carry a Reason (the §2.5 'Reason' column verbatim).");
        RetentionKey = retentionKey;
        PrincipalField = principalField;
    }

    /// <summary>
    /// The two ADR 0298 invariants: <see cref="Erasure.Keep"/> requires a
    /// non-empty <see cref="Reason"/>; <see cref="Erasure.Pseudonymize"/>
    /// requires a non-empty <see cref="PrincipalField"/>. Throws
    /// <see cref="ArgumentException"/> naming the violated rule; the caller
    /// (the catalog) wraps it with the type name.
    /// </summary>
    public void Validate()
    {
        if (Erasure == Erasure.Keep && string.IsNullOrWhiteSpace(Reason))
        {
            throw new ArgumentException(
                "Erasure.Keep requires a non-empty Reason — a kept row is a decision, not a default (ADR 0298).");
        }
        if (Erasure == Erasure.Pseudonymize && string.IsNullOrWhiteSpace(PrincipalField))
        {
            throw new ArgumentException(
                "Erasure.Pseudonymize requires a PrincipalField — the field holding the principal the eraser will tombstone (ADR 0298).");
        }
    }
}

/// <summary>One [DataClass]-attributed type and its policy (the <see cref="DataClassCatalog.All"/> row).</summary>
public readonly record struct DataClassEntry(Type Type, DataClassAttribute Policy);

/// <summary>
/// The reflection reader over <see cref="DataClassAttribute"/> (ADR 0298) —
/// the shape <c>UserScopeInventory</c> gives a doc-type inventory, but
/// attribute-driven: the policy lives on the type, the catalog only reads.
/// Every read calls <see cref="DataClassAttribute.Validate"/>, so an invalid
/// row (a <c>Keep</c> without a reason, a <c>Pseudonymize</c> without its
/// principal field) throws <see cref="ArgumentException"/> at read time
/// instead of drifting silently. IS20's coverage test leans on
/// <see cref="All"/> to check 81/81; IS24's agreement gate checks the
/// erasers' <c>HandledTypes</c> against the same rows.
/// </summary>
public static class DataClassCatalog
{
    /// <summary>
    /// The policy on one type, or <c>null</c> when the type carries no
    /// <see cref="DataClassAttribute"/> (the "none" sentinel — IS20's coverage
    /// test is exactly the assertion that no registered type returns
    /// <c>null</c>). Validates the row before returning it.
    /// </summary>
    public static DataClassAttribute? For(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        DataClassAttribute? policy = type.GetCustomAttribute<DataClassAttribute>();
        if (policy is null)
        {
            return null;
        }
        try
        {
            policy.Validate();
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"[{type.FullName}] {ex.Message}", nameof(type), ex);
        }
        return policy;
    }

    /// <summary>
    /// Every <see cref="DataClassAttribute"/>-attributed type in the assembly
    /// (sorted by type name for a stable order), each validated. This is the
    /// inventory IS17–IS20's annotations accumulate and IS20's
    /// <c>EveryRegisteredDocType_HasADataClass</c> cross-checks against the
    /// <c>Schema.For&lt;T&gt;</c> registration set.
    /// </summary>
    public static IReadOnlyList<DataClassEntry> All(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Type[] types = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<DataClassAttribute>() is not null)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();
        return types.Select(t => new DataClassEntry(t, For(t)!)).ToList();
    }
}
